[CmdletBinding()]
param(
    [string]$Mod = "",
    [string]$RepositoryRoot = "",
    [switch]$RequireBuiltDll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

function Assert-VersionContract {
    param(
        [bool]$Condition,
        [string]$ModId,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Mod version consistency contract failed for '$ModId': $Message"
    }
}

function Test-VersionToken {
    param(
        [string]$Text,
        [string]$Version
    )

    return [regex]::IsMatch(
        $Text,
        "(?<![0-9.])$([regex]::Escape($Version))(?![0-9.])")
}

$repositoryRootFull = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
$modsRoot = Join-Path $repositoryRootFull "mods"
$repositoryReadmePath = Join-Path $repositoryRootFull "README.md"

if (-not (Test-Path -LiteralPath $modsRoot -PathType Container)) {
    throw "Mods directory not found: $modsRoot"
}
if (-not (Test-Path -LiteralPath $repositoryReadmePath -PathType Leaf)) {
    throw "Repository README not found: $repositoryReadmePath"
}

$manifestRecords = @(
    Get-ChildItem -LiteralPath $modsRoot -Recurse -File -Filter "mod.json" |
        ForEach-Object {
            [pscustomobject]@{
                File = $_
                Manifest = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
            }
        }
)

if (-not [string]::IsNullOrWhiteSpace($Mod)) {
    $manifestRecords = @(
        $manifestRecords | Where-Object {
            $rootName = Split-Path -Leaf $_.File.Directory.FullName
            @(
                [string]$_.Manifest.id,
                [string]$_.Manifest.displayName,
                [string]$_.Manifest.packageName,
                $rootName
            ) -icontains $Mod
        }
    )

    if ($manifestRecords.Count -eq 0) {
        throw "Could not find a mod manifest matching '$Mod'."
    }
    if ($manifestRecords.Count -gt 1) {
        throw "Multiple mod manifests match '$Mod': $($manifestRecords.File.FullName -join ', ')"
    }
}

$repositoryReadme = Get-Content -LiteralPath $repositoryReadmePath -Raw
foreach ($record in $manifestRecords) {
    $manifestFile = $record.File
    $manifest = $record.Manifest
    $modRoot = $manifestFile.Directory.FullName
    $modId = [string]$manifest.id
    $version = [string]$manifest.version
    $assemblyVersion = "$version.0"

    Assert-VersionContract `
        ($version -match '^[0-9]+\.[0-9]\.[0-9]$') `
        $modId `
        "mod.json version '$version' must use MAJOR.MINOR.PATCH with single-digit MINOR and PATCH components."

    $sourceTexts = New-Object "System.Collections.Generic.List[string]"
    foreach ($sourceFile in @($manifest.sourceFiles)) {
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $modRoot ([string]$sourceFile)))
        if ([IO.Path]::GetExtension($sourcePath) -ieq ".cs" -and
            (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            $sourceTexts.Add((Get-Content -LiteralPath $sourcePath -Raw))
        }
    }
    $source = $sourceTexts -join "`n"

    $pluginVersions = @(
        [regex]::Matches(
            $source,
            '\bPluginVersion\s*=\s*"(?<version>[0-9]+\.[0-9]+\.[0-9]+)"') |
            ForEach-Object { $_.Groups['version'].Value }
    )
    Assert-VersionContract ($pluginVersions.Count -gt 0) $modId "source PluginVersion was not found."
    foreach ($sourceVersion in $pluginVersions) {
        Assert-VersionContract `
            ($sourceVersion -eq $version) `
            $modId `
            "source PluginVersion '$sourceVersion' does not match mod.json '$version'."
    }

    foreach ($attributeName in @("AssemblyVersion", "AssemblyFileVersion")) {
        $attributeVersions = @(
            [regex]::Matches(
                $source,
                "$attributeName\(`"(?<version>[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)`"\)") |
                ForEach-Object { $_.Groups['version'].Value }
        )
        Assert-VersionContract ($attributeVersions.Count -gt 0) $modId "source $attributeName was not found."
        foreach ($sourceVersion in $attributeVersions) {
            Assert-VersionContract `
                ($sourceVersion -eq $assemblyVersion) `
                $modId `
                "source $attributeName '$sourceVersion' does not match '$assemblyVersion'."
        }
    }

    $informationalVersions = @(
        [regex]::Matches(
            $source,
            'AssemblyInformationalVersion\("(?<version>[0-9]+\.[0-9]+\.[0-9]+)"\)') |
            ForEach-Object { $_.Groups['version'].Value }
    )
    foreach ($sourceVersion in $informationalVersions) {
        Assert-VersionContract `
            ($sourceVersion -eq $version) `
            $modId `
            "source AssemblyInformationalVersion '$sourceVersion' does not match '$version'."
    }

    $readmePath = Join-Path $modRoot "README.txt"
    Assert-VersionContract (Test-Path -LiteralPath $readmePath -PathType Leaf) $modId "README.txt is missing."
    $readme = Get-Content -LiteralPath $readmePath -Raw
    Assert-VersionContract `
        (Test-VersionToken -Text $readme -Version $version) `
        $modId `
        "README.txt does not identify version '$version'."

    $changelogPath = Join-Path $modRoot "CHANGELOG.txt"
    Assert-VersionContract (Test-Path -LiteralPath $changelogPath -PathType Leaf) $modId "CHANGELOG.txt is missing."
    $changelogHeading = Get-Content -LiteralPath $changelogPath |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
    Assert-VersionContract `
        (Test-VersionToken -Text $changelogHeading -Version $version) `
        $modId `
        "the first CHANGELOG.txt heading does not identify version '$version'."

    $nexusChangelogPath = Join-Path $modRoot "nexus-changelog.txt"
    if (Test-Path -LiteralPath $nexusChangelogPath -PathType Leaf) {
        $nexusChangelog = Get-Content -LiteralPath $nexusChangelogPath -Raw
        $targetMatch = [regex]::Match(
            $nexusChangelog,
            '(?m)^TargetVersion=(?<version>[0-9]+\.[0-9]+\.[0-9]+)\s*$')
        Assert-VersionContract $targetMatch.Success $modId "nexus-changelog.txt has no TargetVersion."
        Assert-VersionContract `
            ($targetMatch.Groups['version'].Value -eq $version) `
            $modId `
            "nexus-changelog TargetVersion '$($targetMatch.Groups['version'].Value)' does not match '$version'."
    }

    $relativeModRoot = $modRoot.Substring($repositoryRootFull.Length + 1).Replace("\", "/")
    $readmeRowMatch = [regex]::Match(
        $repositoryReadme,
        "(?m)^\|\s*\[[^\]]+\]\($([regex]::Escape($relativeModRoot))\)\s*\|\s*(?<version>[0-9]+\.[0-9]+\.[0-9]+)\s*\|")
    Assert-VersionContract $readmeRowMatch.Success $modId "README.md Current Mods row was not found."
    Assert-VersionContract `
        ($readmeRowMatch.Groups['version'].Value -eq $version) `
        $modId `
        "README.md version '$($readmeRowMatch.Groups['version'].Value)' does not match '$version'."

    if ($RequireBuiltDll) {
        $dllPath = Join-Path $modRoot ([string]$manifest.dll)
        Assert-VersionContract (Test-Path -LiteralPath $dllPath -PathType Leaf) $modId "built DLL is missing: $dllPath"
        $builtAssemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dllPath).Version.ToString()
        Assert-VersionContract `
            ($builtAssemblyVersion -eq $assemblyVersion) `
            $modId `
            "built DLL version '$builtAssemblyVersion' does not match '$assemblyVersion'."
    }
}

Write-Output "Mod version consistency contracts passed: $($manifestRecords.Count) mod(s)."
