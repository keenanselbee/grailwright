[CmdletBinding()]
param(
    [string]$Mod = "",
    [switch]$RequireApi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot

function Test-JsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    return $Object -ne $null -and $Object.PSObject.Properties.Name -contains $Name
}

function Get-RelativePathCompat {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd("\") + "\"
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if (-not $pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $pathFull
    }

    return $pathFull.Substring($rootFull.Length)
}

function Find-MetadataFile {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$FileName,
        [switch]$SearchParents
    )

    $path = Join-Path $Root $FileName
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        return $path
    }

    if (!$SearchParents) {
        return ""
    }

    $current = [System.IO.DirectoryInfo]::new($Root)
    $repoRootFull = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd("\")
    while ($current.Parent -ne $null -and $current.FullName.StartsWith($repoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        $candidate = Join-Path $current.Parent.FullName $FileName
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }

        $current = $current.Parent
    }

    return ""
}

function Test-MetadataText {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$FileName,
        [int]$MaximumLength,
        [switch]$SearchParents
    )

    $path = Find-MetadataFile -Root $Root -FileName $FileName -SearchParents:$SearchParents
    if ([string]::IsNullOrWhiteSpace($path)) {
        return [pscustomobject]@{
            State = "MISSING"
            Length = 0
            Limit = $MaximumLength
            Path = ""
        }
    }

    $text = (Get-Content -LiteralPath $path -Raw).Trim()
    [pscustomobject]@{
        State = if ($text.Length -le $MaximumLength) { "OK" } else { "OVER" }
        Length = $text.Length
        Limit = $MaximumLength
        Path = Get-RelativePathCompat -Root $RepoRoot -Path $path
    }
}

function Get-NormalizedMetadataText {
    param([string]$Text)

    return (($Text.ToLowerInvariant() -replace '[^a-z0-9]+', ' ').Trim() -replace '\s+', ' ')
}

function Test-FileDescriptionShape {
    param(
        [object]$ShortDescription,
        [object]$FileDescription
    )

    if ($ShortDescription.State -ne "OK" -or $FileDescription.State -ne "OK") {
        return "SKIP"
    }

    if ($FileDescription.Length -ge $ShortDescription.Length) {
        return "NOT_SHORTER"
    }

    $shortText = Get-Content -LiteralPath (Join-Path $RepoRoot $ShortDescription.Path) -Raw
    $fileText = Get-Content -LiteralPath (Join-Path $RepoRoot $FileDescription.Path) -Raw
    if ((Get-NormalizedMetadataText $shortText) -eq (Get-NormalizedMetadataText $fileText)) {
        return "COPIED"
    }

    return "OK"
}

function Get-CurrentChangelogEntryCount {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $path = Join-Path $Root "CHANGELOG.txt"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return -1
    }

    $count = 0
    $inSection = $false
    foreach ($line in Get-Content -LiteralPath $path) {
        $trimmed = $line.Trim()
        $isHeader = $trimmed -match '^(?:Version\s+)?[A-Za-z0-9 ''().:_-]*\b[0-9]+(?:\.[0-9]+){1,3}(?:[-+][0-9A-Za-z][0-9A-Za-z._-]*)?\b\s*$'
        if ($isHeader) {
            if ($inSection) {
                break
            }

            if ($trimmed -match [regex]::Escape($Version)) {
                $inSection = $true
            }

            continue
        }

        if ($inSection -and -not [string]::IsNullOrWhiteSpace($trimmed)) {
            $count++
        }
    }

    return $count
}

function Test-ApiMetadata {
    param([Parameter(Mandatory = $true)][string]$Root)

    $path = Join-Path $Root "API.txt"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [pscustomobject]@{
            State = if ($RequireApi) { "MISSING" } else { "OPTIONAL" }
            Path = ""
        }
    }

    $text = Get-Content -LiteralPath $path -Raw
    $secretPattern = '(?im)^\s*(apiKey|apikey|token|secret|bearerToken|password|nexusApiKey)\s*='
    [pscustomobject]@{
        State = if ($text -match $secretPattern) { "SECRET" } else { "OK" }
        Path = Get-RelativePathCompat -Root $RepoRoot -Path $path
    }
}

$manifestFiles = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "mods") -Recurse -File -Filter "mod.json"
$rows = New-Object "System.Collections.Generic.List[object]"
$hasFailure = $false

foreach ($manifestFile in $manifestFiles) {
    $root = Split-Path -Parent $manifestFile.FullName
    $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
    $names = @(
        $manifest.id,
        $manifest.displayName,
        $manifest.packageName,
        (Split-Path -Leaf $root)
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if (-not [string]::IsNullOrWhiteSpace($Mod) -and -not ($names | Where-Object { $_ -ieq $Mod })) {
        continue
    }

    $shortDescription = Test-MetadataText -Root $root -FileName "nexus-short-desc.txt" -MaximumLength 350 -SearchParents
    $fileDescription = Test-MetadataText -Root $root -FileName "nexus-file-desc.txt" -MaximumLength 255
    $fileDescriptionShape = Test-FileDescriptionShape -ShortDescription $shortDescription -FileDescription $fileDescription
    $api = Test-ApiMetadata -Root $root
    $changelogCount = Get-CurrentChangelogEntryCount -Root $root -Version ([string]$manifest.version)
    $changelogState = if ($changelogCount -gt 0) { "OK" } elseif ($changelogCount -eq 0) { "MISSING" } else { "NO FILE" }

    if ($shortDescription.State -ne "OK" -or $fileDescription.State -ne "OK" -or $fileDescriptionShape -notin @("OK", "SKIP") -or $api.State -eq "SECRET" -or ($RequireApi -and $api.State -ne "OK")) {
        $hasFailure = $true
    }

    $rows.Add([pscustomobject]@{
        Mod = [string]$manifest.packageName
        Version = [string]$manifest.version
        ShortDescription = "$($shortDescription.State) $($shortDescription.Length)/$($shortDescription.Limit)"
        FileDescription = "$($fileDescription.State) $($fileDescription.Length)/$($fileDescription.Limit)"
        FileDescriptionShape = $fileDescriptionShape
        Api = $api.State
        Changelog = "$changelogState $changelogCount"
    })
}

if (-not [string]::IsNullOrWhiteSpace($Mod) -and $rows.Count -eq 0) {
    throw "No mod matched '$Mod'."
}

$rows | Sort-Object Mod
if ($hasFailure) {
    exit 1
}
