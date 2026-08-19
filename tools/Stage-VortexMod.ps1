[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ModRoot,
    [Parameter(Mandatory = $true)][string]$PackageArchive,
    [string]$VortexModsRoot = "",
    [string]$VortexMetadataBridgeRoot = "",
    [switch]$KeepScratch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'VortexNexusMetadataPromotions.ps1')

function Read-ModManifest {
    param([Parameter(Mandatory = $true)][string]$Root)

    $manifestPath = Join-Path $Root "mod.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Missing mod manifest: $manifestPath"
    }

    return Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}

function Resolve-VortexModsRoot {
    param([string]$Candidate)

    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        return [System.IO.Path]::GetFullPath($Candidate)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $env:APPDATA "Vortex\taintedgrailthefallofavalon\mods"))
}

function Convert-ToSafePackageName {
    param([Parameter(Mandatory = $true)][string]$Name)

    $safe = $Name -replace '[\\/:*?"<>|]', "-"
    $safe = $safe -replace "\s+", ""
    $safe = $safe.Trim(".- ")
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "Could not infer a safe package name."
    }

    return $safe
}

function Convert-ToSafeArchiveNameStem {
    param([Parameter(Mandatory = $true)][string]$Name)

    $safe = $Name -replace '[\\/:*?"<>|]', "-"
    $safe = $safe -replace "\s+", " "
    $safe = $safe.Trim(".- ")
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "Could not infer a safe archive name."
    }

    return $safe
}

function Test-PathInsideRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd("\") + "\"
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    return $pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)
}

$ModRoot = [System.IO.Path]::GetFullPath($ModRoot)
$PackageArchive = [System.IO.Path]::GetFullPath($PackageArchive)
if (-not (Test-Path -LiteralPath $ModRoot -PathType Container)) {
    throw "Mod root does not exist: $ModRoot"
}

if (-not (Test-Path -LiteralPath $PackageArchive -PathType Leaf)) {
    throw "Package archive does not exist: $PackageArchive"
}

$manifest = Read-ModManifest -Root $ModRoot
$packageName = Convert-ToSafePackageName ([string]$manifest.packageName)
$displayName = [string]$manifest.displayName
if ([string]::IsNullOrWhiteSpace($displayName)) {
    $displayName = $packageName
}

$archiveName = Convert-ToSafeArchiveNameStem $displayName
$version = [string]$manifest.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Manifest $($manifest.id) has no version."
}

$expectedArchiveLeaf = "$archiveName $version.zip"
$actualArchiveLeaf = Split-Path -Leaf $PackageArchive
if (-not [string]::Equals($actualArchiveLeaf, $expectedArchiveLeaf, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Package archive '$actualArchiveLeaf' does not match the required readable archive name '$expectedArchiveLeaf'. Rebuild with tools/Build-Mod.ps1 so displayName and version are used."
}

$variantFolderName = [System.IO.Path]::GetFileNameWithoutExtension($expectedArchiveLeaf)
$resolvedVortexModsRoot = Resolve-VortexModsRoot -Candidate $VortexModsRoot
New-Item -ItemType Directory -Force -Path $resolvedVortexModsRoot | Out-Null

$targetRoot = [System.IO.Path]::GetFullPath((Join-Path $resolvedVortexModsRoot $variantFolderName))
if (-not (Test-PathInsideRoot -Root $resolvedVortexModsRoot -Path $targetRoot)) {
    throw "Refusing to stage outside the Vortex mods root: $targetRoot"
}

if (Test-Path -LiteralPath $targetRoot) {
    throw "Vortex staged mod already exists: $targetRoot. Bump the mod version or remove that staged folder manually."
}

$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ("vortex-stage-" + [System.Guid]::NewGuid().ToString("N"))
$stagingRoot = [System.IO.Path]::GetFullPath((Join-Path $resolvedVortexModsRoot (".grailwright-stage-" + [System.Guid]::NewGuid().ToString("N"))))

try {
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null
    Expand-Archive -LiteralPath $PackageArchive -DestinationPath $scratch -Force

    $topLevelItems = @(Get-ChildItem -LiteralPath $scratch -Force)
    if ($topLevelItems.Count -ne 1 -or -not $topLevelItems[0].PSIsContainer) {
        throw "Package archive must contain one top-level mod folder: $PackageArchive"
    }

    if ($topLevelItems[0].Name -ne $packageName) {
        throw "Package top-level folder '$($topLevelItems[0].Name)' does not match manifest packageName '$packageName'."
    }

    if (Test-Path -LiteralPath $stagingRoot) {
        throw "Temporary staging folder already exists: $stagingRoot"
    }

    New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
    Copy-Item -LiteralPath $topLevelItems[0].FullName -Destination $stagingRoot -Recurse -Force

    if (Test-Path -LiteralPath $targetRoot) {
        throw "Vortex staged mod appeared during copy: $targetRoot"
    }

    Move-Item -LiteralPath $stagingRoot -Destination $targetRoot

    $grouping = Queue-VortexLocalMetadataGrouping `
        -Manifest $manifest `
        -ModRoot $ModRoot `
        -StagedModId $variantFolderName `
        -StagedPath $targetRoot `
        -VortexModsRoot $resolvedVortexModsRoot `
        -BridgeRoot $VortexMetadataBridgeRoot

    [pscustomobject]@{
        StagedId = $variantFolderName
        PackageName = $packageName
        ArchiveName = $archiveName
        Version = $version
        ZipPath = $PackageArchive
        VortexPath = $targetRoot
        GroupingStatus = [string]$grouping.Status
        GroupingRequestId = [string]$grouping.RequestId
    }
} finally {
    if (-not $KeepScratch -and (Test-Path -LiteralPath $scratch)) {
        Remove-Item -LiteralPath $scratch -Recurse -Force
    }

    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
