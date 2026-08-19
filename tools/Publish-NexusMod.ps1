[CmdletBinding()]
param(
    [string]$Mod = "",
    [string]$ModRoot = "",
    [string]$ArchivePath = "",
    [string]$DestinationDirectory = "",
    [switch]$SkipBuild,
    [switch]$SkipCompile,

    [string]$NexusUrl = "",
    [string]$GameDomain = "",
    [string]$GameScopedModId = "",
    [string]$ModId = "",
    [Alias("GroupId")]
    [string]$ModFileId = "",

    [string]$FileName = "",
    [string]$FileVersion = "",
    [string]$FileDescription = "",
    [ValidateSet("main", "optional", "miscellaneous")]
    [string]$FileCategory = "main",
    [bool]$PrimaryModManagerDownload = $true,
    [bool]$AllowModManagerDownload = $true,
    [bool]$ShowRequirementsPopUp = $false,
    [bool]$UpdateModVersion = $true,
    [bool]$ArchiveExistingFile = $true,
    [string]$PreviousVersionId = "",

    [switch]$ListFiles,
    [switch]$AddChangelog,
    [string]$ConsolidatedChangelogPath = "",
    [string]$DryRunChangelogBaselineVersion = "",
    [switch]$DryRun,
    [string]$ApiBaseUrl = "https://api.nexusmods.com/v3",
    [int]$LockWaitSeconds = 0,
    [int]$LockStaleAfterMinutes = 720,
    [switch]$ForceStaleLock
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http | Out-Null

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiKey = $env:NEXUS_API_KEY
$LockScript = Join-Path $PSScriptRoot "Lock-Operation.ps1"
if (-not (Test-Path -LiteralPath $LockScript -PathType Leaf)) {
    throw "Missing lock helper: $LockScript"
}

. $LockScript
. (Join-Path $PSScriptRoot 'NexusLiveState.ps1')
. (Join-Path $PSScriptRoot 'NexusReleaseReceipts.ps1')

function Test-JsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    return $Object -ne $null -and $Object.PSObject.Properties.Name -contains $Name
}

function Read-ModManifest {
    param([string]$Root)

    $manifestPath = Join-Path $Root "mod.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Missing mod manifest: $manifestPath"
    }

    return Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}

function Resolve-ModRoot {
    param(
        [string]$RequestedMod,
        [string]$RequestedModRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedModRoot)) {
        return [System.IO.Path]::GetFullPath($RequestedModRoot)
    }

    if ([string]::IsNullOrWhiteSpace($RequestedMod)) {
        return ""
    }

    $manifests = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "mods") -Recurse -File -Filter "mod.json"
    $matches = New-Object "System.Collections.Generic.List[string]"

    foreach ($file in $manifests) {
        $root = Split-Path -Parent $file.FullName
        $manifest = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        $names = @(
            $manifest.id,
            $manifest.displayName,
            $manifest.packageName,
            (Split-Path -Leaf $root)
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

        if ($names | Where-Object { $_ -ieq $RequestedMod }) {
            $matches.Add($root)
        }
    }

    if ($matches.Count -eq 0) {
        throw "Could not find mod manifest matching '$RequestedMod'."
    }

    if ($matches.Count -gt 1) {
        throw "Multiple mod manifests match '$RequestedMod': $($matches -join ', ')"
    }

    return [System.IO.Path]::GetFullPath($matches[0])
}

function Read-NexusSettings {
    param([object]$Manifest)

    if ($Manifest -ne $null -and (Test-JsonProperty -Object $Manifest -Name "nexus")) {
        Assert-NoNexusSecrets -Settings $Manifest.nexus -Source "mod.json"
        return $Manifest.nexus
    }

    return $null
}

function Read-LocalApiSettings {
    param([string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root)) {
        return $null
    }

    $path = Join-Path $Root "API.txt"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return $null
    }

    $settings = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $separator = $trimmed.IndexOf("=")
        if ($separator -lt 1) {
            continue
        }

        $key = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            $settings[$key] = $value
        }
    }

    $object = [pscustomobject]$settings
    Assert-NoNexusSecrets -Settings $object -Source $path
    return $object
}

function Assert-NoNexusSecrets {
    param(
        [object]$Settings,
        [string]$Source
    )

    if ($Settings -eq $null) {
        return
    }

    foreach ($secretName in @("apiKey", "apikey", "token", "secret", "bearerToken", "password", "nexusApiKey")) {
        if (Test-JsonProperty -Object $Settings -Name $secretName) {
            throw "Refusing to read Nexus secret '$secretName' from $Source. Store secrets in the NEXUS_API_KEY environment variable only."
        }
    }
}

function Convert-ToSettingBool {
    param(
        [object]$Value,
        [string]$Name
    )

    if ($Value -is [bool]) {
        return [bool]$Value
    }

    $text = ([string]$Value).Trim()
    if ($text -match '^(?i:true|1|yes|y|on)$') {
        return $true
    }

    if ($text -match '^(?i:false|0|no|n|off)$') {
        return $false
    }

    throw "Invalid boolean value for ${Name}: $Value"
}

function Apply-NexusManifestDefaults {
    param([object]$Settings)

    if ($Settings -eq $null) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($script:NexusUrl) -and (Test-JsonProperty -Object $Settings -Name "NexusUrl")) {
        $script:NexusUrl = [string]$Settings.NexusUrl
    }

    if ([string]::IsNullOrWhiteSpace($script:NexusUrl) -and (Test-JsonProperty -Object $Settings -Name "url")) {
        $script:NexusUrl = [string]$Settings.url
    }

    if ([string]::IsNullOrWhiteSpace($script:NexusUrl) -and (Test-JsonProperty -Object $Settings -Name "Url")) {
        $script:NexusUrl = [string]$Settings.Url
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain) -and (Test-JsonProperty -Object $Settings -Name "gameDomain")) {
        $script:GameDomain = [string]$Settings.gameDomain
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain) -and (Test-JsonProperty -Object $Settings -Name "GameDomain")) {
        $script:GameDomain = [string]$Settings.GameDomain
    }

    if ([string]::IsNullOrWhiteSpace($script:GameScopedModId) -and (Test-JsonProperty -Object $Settings -Name "gameScopedModId")) {
        $script:GameScopedModId = [string]$Settings.gameScopedModId
    }

    if ([string]::IsNullOrWhiteSpace($script:GameScopedModId) -and (Test-JsonProperty -Object $Settings -Name "GameScopedModId")) {
        $script:GameScopedModId = [string]$Settings.GameScopedModId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModId) -and (Test-JsonProperty -Object $Settings -Name "modId")) {
        $script:ModId = [string]$Settings.modId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModId) -and (Test-JsonProperty -Object $Settings -Name "ModId")) {
        $script:ModId = [string]$Settings.ModId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModFileId) -and (Test-JsonProperty -Object $Settings -Name "modFileId")) {
        $script:ModFileId = [string]$Settings.modFileId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModFileId) -and (Test-JsonProperty -Object $Settings -Name "ModFileId")) {
        $script:ModFileId = [string]$Settings.ModFileId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModFileId) -and (Test-JsonProperty -Object $Settings -Name "GroupId")) {
        $script:ModFileId = [string]$Settings.GroupId
    }

    if ([string]::IsNullOrWhiteSpace($script:FileName) -and (Test-JsonProperty -Object $Settings -Name "fileName")) {
        $script:FileName = [string]$Settings.fileName
    }

    if ([string]::IsNullOrWhiteSpace($script:FileName) -and (Test-JsonProperty -Object $Settings -Name "FileName")) {
        $script:FileName = [string]$Settings.FileName
    }

    if ((Test-JsonProperty -Object $Settings -Name "fileCategory") -and [string]::IsNullOrWhiteSpace($script:FileCategory)) {
        $script:FileCategory = [string]$Settings.fileCategory
    }

    if ((Test-JsonProperty -Object $Settings -Name "FileCategory") -and -not $PSBoundParameters.ContainsKey("FileCategory")) {
        $script:FileCategory = [string]$Settings.FileCategory
    }

    foreach ($property in @(
        "primaryModManagerDownload",
        "allowModManagerDownload",
        "showRequirementsPopUp",
        "updateModVersion",
        "archiveExistingFile"
    )) {
        if (Test-JsonProperty -Object $Settings -Name $property) {
            Set-Variable -Scope Script -Name ($property.Substring(0, 1).ToUpperInvariant() + $property.Substring(1)) -Value (Convert-ToSettingBool -Value $Settings.$property -Name $property)
        }
    }

    foreach ($property in @(
        "PrimaryModManagerDownload",
        "AllowModManagerDownload",
        "ShowRequirementsPopUp",
        "UpdateModVersion",
        "ArchiveExistingFile"
    )) {
        if (Test-JsonProperty -Object $Settings -Name $property) {
            Set-Variable -Scope Script -Name $property -Value (Convert-ToSettingBool -Value $Settings.$property -Name $property)
        }
    }
}

function Resolve-NexusUrl {
    if ([string]::IsNullOrWhiteSpace($script:NexusUrl)) {
        return
    }

    $match = [regex]::Match($script:NexusUrl, 'nexusmods\.com/(?<game>[^/?#]+)/mods/(?<mod>[0-9]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (!$match.Success) {
        throw "Could not parse Nexus URL: $script:NexusUrl"
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain)) {
        $script:GameDomain = $match.Groups["game"].Value
    }

    if ([string]::IsNullOrWhiteSpace($script:GameScopedModId)) {
        $script:GameScopedModId = $match.Groups["mod"].Value
    }
}

function Invoke-NexusApi {
    param(
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    if ([string]::IsNullOrWhiteSpace($script:ApiKey)) {
        throw "Missing Nexus API key. Set NEXUS_API_KEY in your environment. Do not store it in repo files or pass it on the command line."
    }

    $headers = @{
        apikey = $script:ApiKey
        accept = "application/json"
    }

    $uri = $script:ApiBaseUrl.TrimEnd("/") + $Path
    if ($Body -eq $null) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }

    $json = $Body | ConvertTo-Json -Depth 20
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
}

function Resolve-RemoteModId {
    if (-not [string]::IsNullOrWhiteSpace($script:ModId)) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain) -or [string]::IsNullOrWhiteSpace($script:GameScopedModId)) {
        return
    }

    if ($script:DryRun -and [string]::IsNullOrWhiteSpace($script:ApiKey)) {
        return
    }

    $response = Invoke-NexusApi -Method GET -Path ("/games/{0}/mods/{1}" -f $script:GameDomain, $script:GameScopedModId)
    if ($response -ne $null -and $response.data -ne $null -and (Test-JsonProperty -Object $response.data -Name "id")) {
        $script:ModId = [string]$response.data.id
    }
}

function ConvertTo-NexusReleaseVersion {
    param(
        [string]$Version,
        [string]$Context
    )

    $match = [regex]::Match($Version.Trim(), '^(?<numeric>[0-9]+(?:\.[0-9]+){1,3})(?:[-+][0-9A-Za-z][0-9A-Za-z._-]*)?$')
    if (!$match.Success) {
        throw "Invalid release version '$Version'$Context. Expected two to four numeric components with an optional prerelease/build suffix."
    }

    return [version]$match.Groups["numeric"].Value
}

function Get-ChangelogSections {
    param([string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root)) {
        return @()
    }

    $path = Join-Path $Root "CHANGELOG.txt"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return @()
    }

    $lines = Get-Content -LiteralPath $path
    $sections = New-Object "System.Collections.Generic.List[object]"
    $currentVersion = ""
    $currentEntries = New-Object "System.Collections.Generic.List[string]"

    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        $headerMatch = [regex]::Match($trimmed, '^Version\s+(?<version>[0-9]+(?:\.[0-9]+){1,3}(?:[-+][0-9A-Za-z][0-9A-Za-z._-]*)?)$')
        if ($headerMatch.Success) {
            if (-not [string]::IsNullOrWhiteSpace($currentVersion)) {
                $sections.Add([pscustomobject]@{
                    Version = $currentVersion
                    Entries = $currentEntries.ToArray()
                })
            }

            $currentVersion = $headerMatch.Groups["version"].Value
            $currentEntries = New-Object "System.Collections.Generic.List[string]"
            continue
        }

        if ([string]::IsNullOrWhiteSpace($currentVersion) -or [string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $currentEntries.Add(($trimmed -replace '^\s*[-*]\s*', ''))
    }

    if (-not [string]::IsNullOrWhiteSpace($currentVersion)) {
        $sections.Add([pscustomobject]@{
            Version = $currentVersion
            Entries = $currentEntries.ToArray()
        })
    }

    return $sections.ToArray()
}

function Get-CumulativeChangelogSelection {
    param(
        [string]$Root,
        [string]$TargetVersion,
        [string]$PublishedVersion
    )

    $target = ConvertTo-NexusReleaseVersion -Version $TargetVersion -Context " for the upload target"
    $published = ConvertTo-NexusReleaseVersion -Version $PublishedVersion -Context " reported by Nexus"
    if ($target -le $published) {
        throw "Nexus already reports version $PublishedVersion for this file group; upload target $TargetVersion must be newer."
    }

    $sections = @(Get-ChangelogSections -Root $Root)
    $targetSection = @($sections | Where-Object { $_.Version -eq $TargetVersion })
    if ($targetSection.Count -ne 1) {
        throw "Expected exactly one CHANGELOG.txt block for upload target Version $TargetVersion; found $($targetSection.Count)."
    }

    $selected = @($sections | Where-Object {
        $sectionVersion = ConvertTo-NexusReleaseVersion -Version $_.Version -Context " in CHANGELOG.txt"
        $sectionVersion -gt $published -and $sectionVersion -le $target
    })
    if ($selected.Count -eq 0) {
        return [pscustomobject]@{
            Sections = @()
            Entries = @()
        }
    }

    $entries = New-Object "System.Collections.Generic.List[string]"
    foreach ($section in $selected) {
        foreach ($entry in $section.Entries) {
            $entries.Add($entry)
        }
    }

    return [pscustomobject]@{
        Sections = @($selected)
        Entries = $entries.ToArray()
    }
}

function Assert-ConsolidatedChangelogEntries {
    param(
        [string[]]$Entries,
        [string]$Source
    )

    if ($Entries.Count -eq 0) {
        throw "Consolidated Nexus changelog has no entries: $Source"
    }

    $seen = @{}
    foreach ($entry in $Entries) {
        $trimmed = $entry.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($trimmed -match '^Version\s+[0-9]') {
            throw "Consolidated Nexus changelog must not contain embedded version headings: '$trimmed' in $Source"
        }

        if ($trimmed -match '^(?i:work in progress|not yet released)') {
            throw "Consolidated Nexus changelog contains an unreleased-work marker: '$trimmed' in $Source"
        }

        if ($trimmed -match '^[-*]\s+') {
            throw "Consolidated Nexus changelog entries must not include Markdown bullet prefixes: '$trimmed' in $Source"
        }

        $normalized = ($trimmed -replace '\s+', ' ').ToLowerInvariant()
        if ($seen.ContainsKey($normalized)) {
            throw "Consolidated Nexus changelog contains an obvious duplicate entry: '$trimmed' in $Source"
        }

        $seen[$normalized] = $true
    }
}

function Read-ConsolidatedChangelog {
    param(
        [string]$Path,
        [string]$TargetVersion,
        [string]$PublishedVersion
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        return $null
    }

    $candidateRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot ".codex-temp\nexus-changelog-candidates")).TrimEnd("\") + "\"
    if ($resolvedPath.StartsWith($candidateRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Generated changelog candidates cannot be uploaded directly. Review and consolidate the candidate, then save the result as the mod's nexus-changelog.txt or another explicit reviewed path."
    }

    $lines = @(Get-Content -LiteralPath $resolvedPath)
    $contentLines = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($contentLines.Count -lt 3) {
        throw "Reviewed Nexus changelog must contain TargetVersion, BaselineVersion, and at least one change entry: $resolvedPath"
    }

    if ($contentLines[0].Trim() -ne "TargetVersion=$TargetVersion") {
        throw "Reviewed Nexus changelog target is stale. Expected 'TargetVersion=$TargetVersion' as its first nonblank line: $resolvedPath"
    }

    if ($contentLines[1].Trim() -ne "BaselineVersion=$PublishedVersion") {
        throw "Reviewed Nexus changelog baseline is stale. Expected 'BaselineVersion=$PublishedVersion' as its second nonblank line: $resolvedPath"
    }

    $entries = @($contentLines | Select-Object -Skip 2 | ForEach-Object { $_.Trim() })
    Assert-ConsolidatedChangelogEntries -Entries $entries -Source $resolvedPath
    return [pscustomobject]@{
        Path = $resolvedPath
        Entries = $entries
    }
}

function Write-ConsolidatedChangelogCandidate {
    param(
        [string]$PackageName,
        [string]$TargetVersion,
        [string]$PublishedVersion,
        [string[]]$Entries
    )

    $candidateRoot = Join-Path $RepoRoot ".codex-temp\nexus-changelog-candidates"
    New-Item -ItemType Directory -Force -Path $candidateRoot | Out-Null
    $safePackageName = $PackageName -replace '[^A-Za-z0-9._-]', '-'
    $candidatePath = Join-Path $candidateRoot ("{0}-{1}-from-{2}.txt" -f $safePackageName, $TargetVersion, $PublishedVersion)
    $candidateLines = @(
        "TargetVersion=$TargetVersion",
        "BaselineVersion=$PublishedVersion",
        ""
    ) + $Entries
    Set-Content -LiteralPath $candidatePath -Value $candidateLines -Encoding UTF8
    return $candidatePath
}

function Get-NexusChangelogPlan {
    param(
        [string]$Root,
        [string]$PackageName,
        [string]$TargetVersion,
        [string]$PublishedVersion,
        [string]$ReviewedPath
    )

    if ([string]::IsNullOrWhiteSpace($Root)) {
        throw "Cannot prepare a Nexus changelog without a mod root containing CHANGELOG.txt."
    }

    $selection = Get-CumulativeChangelogSelection -Root $Root -TargetVersion $TargetVersion -PublishedVersion $PublishedVersion
    $includedVersions = @($selection.Sections | ForEach-Object { $_.Version })
    $rawEntries = @($selection.Entries)
    if ($rawEntries.Count -eq 0) {
        throw "Cannot add changelog: no entries found after Nexus version $PublishedVersion through target $TargetVersion."
    }

    $resolvedReviewedPath = $ReviewedPath
    if ([string]::IsNullOrWhiteSpace($resolvedReviewedPath)) {
        $resolvedReviewedPath = Join-Path $Root "nexus-changelog.txt"
    }

    try {
        $reviewed = Read-ConsolidatedChangelog -Path $resolvedReviewedPath -TargetVersion $TargetVersion -PublishedVersion $PublishedVersion
    } catch {
        $candidatePath = ""
        if ($selection.Sections.Count -gt 1) {
            $candidatePath = Write-ConsolidatedChangelogCandidate -PackageName $PackageName -TargetVersion $TargetVersion -PublishedVersion $PublishedVersion -Entries $rawEntries
        }

        $candidateNote = if ([string]::IsNullOrWhiteSpace($candidatePath)) { "" } else { " Fresh raw candidate: '$candidatePath'." }
        throw "$($_.Exception.Message)$candidateNote"
    }
    if ($reviewed -ne $null) {
        return [pscustomobject]@{
            Entries = @($reviewed.Entries)
            IncludedVersions = $includedVersions
            RawEntryCount = $rawEntries.Count
            Source = "reviewed-consolidation"
            ReviewedPath = $reviewed.Path
            CandidatePath = ""
        }
    }

    if ($selection.Sections.Count -gt 1) {
        $candidatePath = Write-ConsolidatedChangelogCandidate -PackageName $PackageName -TargetVersion $TargetVersion -PublishedVersion $PublishedVersion -Entries $rawEntries
        throw "Nexus upload spans $($selection.Sections.Count) local versions ($($includedVersions -join ', ')). Review and lightly consolidate repeated or superseded changes in '$candidatePath', then save the reviewed text as '$(Join-Path $Root 'nexus-changelog.txt')' or pass -ConsolidatedChangelogPath."
    }

    Assert-ConsolidatedChangelogEntries -Entries $rawEntries -Source (Join-Path $Root "CHANGELOG.txt")
    return [pscustomobject]@{
        Entries = $rawEntries
        IncludedVersions = $includedVersions
        RawEntryCount = $rawEntries.Count
        Source = "single-version-changelog"
        ReviewedPath = ""
        CandidatePath = ""
    }
}

function Add-KsAddonChangelogHeading {
    param(
        [string]$Root,
        [object]$Manifest,
        [string[]]$Entries
    )

    if ([string]::IsNullOrWhiteSpace($Root)) {
        return @($Entries)
    }

    $ksAddonsRoot = [System.IO.Path]::GetFullPath(
        (Join-Path $RepoRoot "mods\KSAddons")
    ).TrimEnd("\") + "\"
    $resolvedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd("\") + "\"
    if (-not $resolvedRoot.StartsWith(
        $ksAddonsRoot,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        return @($Entries)
    }

    if ($Manifest -eq $null -or
        -not (Test-JsonProperty -Object $Manifest -Name "displayName") -or
        [string]::IsNullOrWhiteSpace([string]$Manifest.displayName)) {
        throw "KS Addons changelogs require a non-empty displayName in mod.json."
    }

    $heading = ([string]$Manifest.displayName).Trim()
    $normalizedEntries = @($Entries | ForEach-Object { [string]$_ })
    if ($normalizedEntries.Count -gt 0 -and
        $normalizedEntries[0].Trim().Equals(
            $heading,
            [System.StringComparison]::OrdinalIgnoreCase
        )) {
        return $normalizedEntries
    }

    return @($heading) + $normalizedEntries
}

function Get-CurrentRemoteFileVersion {
    if ([string]::IsNullOrWhiteSpace($script:ModFileId)) {
        throw "Cannot resolve the current Nexus changelog baseline without ModFileId/GroupId."
    }

    $response = Invoke-NexusApi -Method GET -Path ("/mod-files/{0}/versions" -f $script:ModFileId)
    $versions = @()
    if ($response -ne $null -and $response.data -ne $null -and (Test-JsonProperty -Object $response.data -Name "versions")) {
        $versions = @($response.data.versions)
    }
    if ($versions.Count -eq 0) {
        throw "Nexus file group $($script:ModFileId) has no existing versions to use as a cumulative changelog baseline."
    }

    $current = @($versions | Where-Object { (Test-JsonProperty -Object $_ -Name "is_primary") -and [bool]$_.is_primary } | Select-Object -First 1)
    if ($current.Count -eq 0) {
        $current = @($versions |
            Where-Object { -not (Test-JsonProperty -Object $_ -Name "category") -or [string]$_.category -eq "main" } |
            Sort-Object @{ Expression = { if (Test-JsonProperty -Object $_ -Name "uploaded_at") { [datetime]$_.uploaded_at } else { [datetime]::MinValue } }; Descending = $true } |
            Select-Object -First 1)
    }
    if ($current.Count -eq 0 -or [string]::IsNullOrWhiteSpace([string]$current[0].version)) {
        throw "Could not identify the current Nexus version for file group $($script:ModFileId)."
    }

    return [pscustomobject]@{
        Version = [string]$current[0].version
        VersionId = if (Test-JsonProperty -Object $current[0] -Name "id") { [string]$current[0].id } else { "" }
    }
}

function Get-NexusCreatedVersionId {
    param([object]$CreatedVersion)
    if ($CreatedVersion -ne $null -and $CreatedVersion.data -ne $null -and
        (Test-JsonProperty -Object $CreatedVersion.data -Name 'version') -and
        $CreatedVersion.data.version -ne $null -and
        (Test-JsonProperty -Object $CreatedVersion.data.version -Name 'id') -and
        -not [string]::IsNullOrWhiteSpace([string]$CreatedVersion.data.version.id)) {
        return [string]$CreatedVersion.data.version.id
    }
    return ''
}

function Test-NexusUploadedVersionMatch {
    param([object]$Version, [string]$ExpectedVersionId)
    return Test-NexusLiveVersionIdMatch -Version $Version -ExpectedVersionId $ExpectedVersionId
}

function Confirm-NexusUploadedFileVersion {
    param(
        [string]$ExpectedVersion,
        [string]$ExpectedDescription,
        [string]$ExpectedVersionId,
        [int]$TimeoutSeconds = 120
    )

    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $response = Invoke-NexusApi -Method GET -Path ("/mod-files/{0}/versions" -f $script:ModFileId)
        $versions = if ($response -ne $null -and $response.data -ne $null -and (Test-JsonProperty -Object $response.data -Name "versions")) { @($response.data.versions) } else { @() }
        $match = @($versions | Where-Object { Test-NexusUploadedVersionMatch -Version $_ -ExpectedVersionId $ExpectedVersionId } | Select-Object -First 1)
        if ($match.Count -gt 0) { break }
        Start-Sleep -Seconds 2
    } while ([datetime]::UtcNow -lt $deadline)
    if ($match.Count -eq 0) { throw "The bounded reread did not report exact version id $ExpectedVersionId for file group $script:ModFileId within $TimeoutSeconds seconds." }

    $descriptionVerified = $false
    $descriptionMismatch = $false
    foreach ($name in @('description', 'file_description')) {
        if ((Test-JsonProperty -Object $match[0] -Name $name) -and -not [string]::IsNullOrWhiteSpace([string]$match[0].$name)) {
            $descriptionVerified = [string]$match[0].$name -eq $ExpectedDescription
            $descriptionMismatch = -not $descriptionVerified
            break
        }
    }

    $vortexFileId = if ((Test-JsonProperty -Object $match[0] -Name 'game_scoped_id') -and
        [string]$match[0].game_scoped_id -match '^[1-9][0-9]*$') {
        [string]$match[0].game_scoped_id
    } else { '' }
    $fileGroup = if ((Test-JsonProperty -Object $match[0] -Name 'file') -and $match[0].file -ne $null) { $match[0].file } else { $null }

    return [pscustomobject]@{
        Version = [string]$match[0].version
        VersionId = [string]$match[0].id
        VortexFileId = $vortexFileId
        UploadedAt = if (Test-JsonProperty -Object $match[0] -Name 'uploaded_at') { [string]$match[0].uploaded_at } else { '' }
        Category = if (Test-JsonProperty -Object $match[0] -Name 'category') { [string]$match[0].category } else { '' }
        IsPrimary = if (Test-JsonProperty -Object $match[0] -Name 'is_primary') { [bool]$match[0].is_primary } else { $false }
        RemoteArchiveName = if (Test-JsonProperty -Object $match[0] -Name 'file_name') { [string]$match[0].file_name } else { '' }
        FileGroupId = if ($fileGroup -ne $null -and (Test-JsonProperty -Object $fileGroup -Name 'id')) { [string]$fileGroup.id } else { '' }
        FileGroupName = if ($fileGroup -ne $null -and (Test-JsonProperty -Object $fileGroup -Name 'name')) { [string]$fileGroup.name } else { '' }
        LabelMatches = [string]$match[0].version -eq $ExpectedVersion
        FileDescriptionVerified = $descriptionVerified
        FileDescriptionMismatch = $descriptionMismatch
        ReadVerified = $true
    }
}

function New-NexusReleaseReceiptRecord {
    param(
        [object]$Manifest,
        [System.IO.FileInfo]$Archive,
        [string]$ArchiveMd5,
        [string]$ArchiveSha256,
        [object]$CreatedVersion,
        [string]$CreatedVersionId,
        [AllowNull()][object]$VerifiedUpload,
        [string]$RecordedAt,
        [ValidateSet('not-requested', 'pending', 'posted', 'failed')]
        [string]$ChangelogStatus,
        [string]$ChangelogError = ''
    )

    $createdFile = if ($CreatedVersion -ne $null -and $CreatedVersion.data -ne $null -and
        (Test-JsonProperty -Object $CreatedVersion.data -Name 'file') -and $CreatedVersion.data.file -ne $null) {
        $CreatedVersion.data.file
    } else { $null }
    $fileGroupId = if ($VerifiedUpload -ne $null -and -not [string]::IsNullOrWhiteSpace([string]$VerifiedUpload.FileGroupId)) {
        [string]$VerifiedUpload.FileGroupId
    } elseif ($createdFile -ne $null -and (Test-JsonProperty -Object $createdFile -Name 'id')) {
        [string]$createdFile.id
    } else {
        [string]$script:ModFileId
    }
    $fileGroupName = if ($VerifiedUpload -ne $null -and -not [string]::IsNullOrWhiteSpace([string]$VerifiedUpload.FileGroupName)) {
        [string]$VerifiedUpload.FileGroupName
    } elseif ($createdFile -ne $null -and (Test-JsonProperty -Object $createdFile -Name 'name')) {
        [string]$createdFile.name
    } else { [string]$script:FileName }
    $vortexFileId = if ($VerifiedUpload -ne $null) { [string]$VerifiedUpload.VortexFileId } else { '' }
    $nxmUri = if (-not [string]::IsNullOrWhiteSpace($vortexFileId)) {
        "nxm://$($script:GameDomain)/mods/$($script:GameScopedModId)/files/$vortexFileId"
    } else { '' }

    return [pscustomobject]@{
        receiptSchemaVersion = 1
        key = ''
        recordedAt = $RecordedAt
        updatedAt = $null
        lifecycleStatus = 'version-uploaded'
        packageName = if ($Manifest -ne $null -and (Test-JsonProperty -Object $Manifest -Name 'packageName')) { [string]$Manifest.packageName } else { [string]$script:FileName }
        displayName = if ($Manifest -ne $null -and (Test-JsonProperty -Object $Manifest -Name 'displayName')) { [string]$Manifest.displayName } else { [string]$script:FileName }
        version = [string]$script:FileVersion
        nexus = [pscustomobject]@{
            source = 'nexus'
            url = [string]$script:NexusUrl
            gameDomain = [string]$script:GameDomain
            gameScopedModId = [string]$script:GameScopedModId
            v3ModId = [string]$script:ModId
            fileGroupId = $fileGroupId
            fileGroupName = $fileGroupName
            v3VersionId = $CreatedVersionId
            vortexFileId = $vortexFileId
            vortexFileIdStatus = if ([string]::IsNullOrWhiteSpace($vortexFileId)) { 'pending-resolution' } else { 'resolved' }
            nxmUri = $nxmUri
            logicalFileName = [string]$script:FileName
            remoteArchiveName = if ($VerifiedUpload -ne $null) { [string]$VerifiedUpload.RemoteArchiveName } else { '' }
            uploadedAt = if ($VerifiedUpload -ne $null) { [string]$VerifiedUpload.UploadedAt } else { '' }
            category = if ($VerifiedUpload -ne $null) { [string]$VerifiedUpload.Category } else { [string]$script:FileCategory }
            isPrimary = if ($VerifiedUpload -ne $null) { [bool]$VerifiedUpload.IsPrimary } else { [bool]$script:PrimaryModManagerDownload }
        }
        archive = [pscustomobject]@{
            fileName = $Archive.Name
            sizeBytes = [int64]$Archive.Length
            md5 = $ArchiveMd5
            sha256 = $ArchiveSha256
        }
        changelog = [pscustomobject]@{
            requested = [bool]$script:AddChangelog
            status = $ChangelogStatus
            error = $ChangelogError
        }
        verification = [pscustomobject]@{
            status = if ($VerifiedUpload -ne $null) { 'exact-version-id' } else { 'verified-write-only' }
            versionLabelMatches = if ($VerifiedUpload -ne $null) { [bool]$VerifiedUpload.LabelMatches } else { $null }
            fileDescriptionVerified = if ($VerifiedUpload -ne $null) { [bool]$VerifiedUpload.FileDescriptionVerified } else { $null }
        }
    }
}

function Get-NexusMetadataText {
    param(
        [string]$Root,
        [string]$FileName,
        [int]$MaximumLength,
        [switch]$SearchParents,
        [switch]$Required
    )

    if ([string]::IsNullOrWhiteSpace($Root)) {
        if ($Required) {
            throw "Cannot read $FileName without a mod root."
        }

        return ""
    }

    $path = Join-Path $Root $FileName
    if ($SearchParents -and -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $current = [System.IO.DirectoryInfo]::new($Root)
        $repoRootFull = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd("\")
        while ($current.Parent -ne $null -and $current.FullName.StartsWith($repoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            $candidate = Join-Path $current.Parent.FullName $FileName
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $path = $candidate
                break
            }

            $current = $current.Parent
        }
    }

    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($Required) {
            throw "Missing Nexus metadata file: $path"
        }

        return ""
    }

    $text = (Get-Content -LiteralPath $path -Raw).Trim()
    if ($text.Length -gt $MaximumLength) {
        throw "$FileName is $($text.Length) characters, over Nexus limit $MaximumLength`: $path"
    }

    return $text
}

function Build-Archive {
    param(
        [string]$Root,
        [switch]$NoBuild,
        [switch]$CompileSkip,
        [string]$Destination
    )

    if (-not [string]::IsNullOrWhiteSpace($script:ArchivePath)) {
        $script:ArchivePath = [System.IO.Path]::GetFullPath($script:ArchivePath)
        if (-not (Test-Path -LiteralPath $script:ArchivePath -PathType Leaf)) {
            throw "ArchivePath not found: $script:ArchivePath"
        }

        return
    }

    if ([string]::IsNullOrWhiteSpace($Root)) {
        if ($NoBuild) {
            return
        }

        throw "Pass -Mod/-ModRoot or -ArchivePath."
    }

    if ($NoBuild) {
        throw "Pass -ArchivePath when using -SkipBuild."
    }

    $buildScript = Join-Path $PSScriptRoot "Build-Mod.ps1"
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        throw "Missing build script: $buildScript"
    }

    $buildArgs = @{
        ModRoot = $Root
    }

    if ($CompileSkip) {
        $buildArgs.SkipCompile = $true
    }

    if ([string]::IsNullOrWhiteSpace($Destination)) {
        $Destination = Join-Path $RepoRoot ".codex-temp\builds"
    }
    $buildArgs.DestinationDirectory = $Destination
    $buildArgs.PackageOnly = $true

    $buildArgs.LockWaitSeconds = $script:LockWaitSeconds
    $buildArgs.LockStaleAfterMinutes = $script:LockStaleAfterMinutes
    if ($script:ForceStaleLock) {
        $buildArgs.ForceStaleLock = $true
    }

    $result = & $buildScript @buildArgs | Select-Object -Last 1
    if ($result -eq $null -or [string]::IsNullOrWhiteSpace([string]$result.ZipPath)) {
        throw "Build did not return a zip path."
    }

    $script:ArchivePath = [string]$result.ZipPath
}

function Wait-NexusUploadAvailable {
    param(
        [string]$UploadId,
        [int]$TimeoutSeconds = 180
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $response = Invoke-NexusApi -Method GET -Path ("/uploads/{0}" -f $UploadId)
        if ($response -ne $null -and $response.data -ne $null -and [string]$response.data.state -eq "available") {
            return
        }

        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Upload $UploadId did not become available within $TimeoutSeconds seconds."
}

function Send-PresignedUpload {
    param(
        [string]$PresignedUrl,
        [string]$Path
    )

    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($curl -ne $null) {
        & $curl.Source `
            --fail `
            --silent `
            --show-error `
            --request PUT `
            --header "Content-Type: application/octet-stream" `
            --header "Content-Disposition;" `
            --upload-file $Path `
            $PresignedUrl

        if ($LASTEXITCODE -ne 0) {
            throw "Presigned upload failed with curl exit code $LASTEXITCODE."
        }

        return
    }

    Invoke-WebRequest `
        -Method Put `
        -Uri $PresignedUrl `
        -InFile $Path `
        -ContentType "application/octet-stream" `
        -Headers @{ "Content-Disposition" = "" } |
        Out-Null
}

function Send-MultipartUpload {
    param([string]$Path)

    $archiveItem = Get-Item -LiteralPath $Path
    $uploadRequest = @{
        size_bytes = [string]$archiveItem.Length
        filename = $archiveItem.Name
    }

    $upload = Invoke-NexusApi -Method POST -Path "/uploads/multipart" -Body $uploadRequest
    $uploadId = [string]$upload.data.id
    $partUrls = @($upload.data.part_presigned_urls)
    $partSize = [int64]$upload.data.part_size_bytes
    $completeUrl = [string]$upload.data.complete_presigned_url

    if ([string]::IsNullOrWhiteSpace($uploadId) -or $partUrls.Count -eq 0 -or $partSize -le 0 -or [string]::IsNullOrWhiteSpace($completeUrl)) {
        throw "Nexus did not return a complete multipart upload session."
    }

    $client = [System.Net.Http.HttpClient]::new()
    $stream = [System.IO.File]::OpenRead($archiveItem.FullName)
    $parts = New-Object "System.Collections.Generic.List[object]"

    try {
        for ($index = 0; $index -lt $partUrls.Count; $index++) {
            $offset = [int64]$index * $partSize
            $remaining = $stream.Length - $offset
            $bytesToRead = [int64][Math]::Min($partSize, $remaining)
            if ($bytesToRead -lt 0) {
                $bytesToRead = 0
            }

            if ($bytesToRead -gt [int]::MaxValue) {
                throw "Multipart part is too large for the local uploader buffer: $bytesToRead bytes."
            }

            $buffer = New-Object byte[] ([int]$bytesToRead)
            $stream.Seek($offset, [System.IO.SeekOrigin]::Begin) | Out-Null
            $totalRead = 0
            while ($totalRead -lt $bytesToRead) {
                $read = $stream.Read($buffer, $totalRead, ([int]$bytesToRead - $totalRead))
                if ($read -le 0) {
                    break
                }

                $totalRead += $read
            }

            if ($totalRead -ne $bytesToRead) {
                throw "Could not read multipart part $($index + 1). Expected $bytesToRead bytes, read $totalRead bytes."
            }

            $content = [System.Net.Http.ByteArrayContent]::new($buffer)
            $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/octet-stream")
            $response = $client.PutAsync([string]$partUrls[$index], $content).GetAwaiter().GetResult()
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if (-not $response.IsSuccessStatusCode) {
                throw "Failed to upload multipart part $($index + 1): $([int]$response.StatusCode) $responseBody"
            }

            $etag = ""
            if ($response.Headers.ETag -ne $null) {
                $etag = $response.Headers.ETag.Tag
            } elseif ($response.Headers.Contains("ETag")) {
                $etag = @($response.Headers.GetValues("ETag"))[0]
            }

            if ([string]::IsNullOrWhiteSpace($etag)) {
                throw "No ETag returned for multipart part $($index + 1)."
            }

            $parts.Add([pscustomobject]@{
                PartNumber = $index + 1
                ETag = $etag.Trim('"')
            })
        }
    } finally {
        $stream.Dispose()
        $client.Dispose()
    }

    $partXml = ($parts | ForEach-Object {
        "  <Part>`n    <PartNumber>$($_.PartNumber)</PartNumber>`n    <ETag>$([System.Security.SecurityElement]::Escape($_.ETag))</ETag>`n  </Part>"
    }) -join "`n"
    $xml = "<CompleteMultipartUpload>`n$partXml`n</CompleteMultipartUpload>"

    $completeClient = [System.Net.Http.HttpClient]::new()
    try {
        $completeContent = [System.Net.Http.StringContent]::new($xml, [System.Text.Encoding]::UTF8, "application/xml")
        $completeResponse = $completeClient.PostAsync($completeUrl, $completeContent).GetAwaiter().GetResult()
        $completeBody = $completeResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $completeResponse.IsSuccessStatusCode) {
            throw "Failed to complete multipart upload: $([int]$completeResponse.StatusCode) $completeBody"
        }
    } finally {
        $completeClient.Dispose()
    }

    return $uploadId
}

function Write-PublishPlan {
    param(
        [object]$Manifest,
        [string[]]$ChangelogEntries,
        [string]$ChangelogBaselineVersion,
        [string[]]$ChangelogIncludedVersions,
        [string]$ChangelogSource,
        [string]$ReviewedChangelogPath,
        [string]$ChangelogCandidatePath,
        [int]$RawChangelogEntryCount,
        [string]$ShortDescription,
        [string]$FileDescriptionSource
    )

    [pscustomobject]@{
        Package = if ($Manifest -ne $null) { [string]$Manifest.packageName } else { "" }
        Version = $script:FileVersion
        Archive = $script:ArchivePath
        NexusUrl = $script:NexusUrl
        GameDomain = $script:GameDomain
        GameScopedModId = $script:GameScopedModId
        ModId = $script:ModId
        ModFileId = $script:ModFileId
        FileName = $script:FileName
        FileCategory = $script:FileCategory
        PrimaryModManagerDownload = $script:PrimaryModManagerDownload
        AllowModManagerDownload = $script:AllowModManagerDownload
        ShowRequirementsPopUp = $script:ShowRequirementsPopUp
        UpdateModVersion = $script:UpdateModVersion
        ArchiveExistingFile = $script:ArchiveExistingFile
        FileDescription = $script:FileDescription
        FileDescriptionLength = $script:FileDescription.Length
        FileDescriptionSource = $FileDescriptionSource
        FileDescriptionSourceLength = $FileDescriptionSource.Length
        ShortDescription = $ShortDescription
        ShortDescriptionLength = $ShortDescription.Length
        AddChangelog = [bool]$script:AddChangelog
        ChangelogBaselineVersion = $ChangelogBaselineVersion
        ChangelogIncludedVersions = $ChangelogIncludedVersions
        ChangelogSource = $ChangelogSource
        ReviewedChangelogPath = $ReviewedChangelogPath
        ChangelogCandidatePath = $ChangelogCandidatePath
        RawChangelogEntryCount = $RawChangelogEntryCount
        ChangelogEntries = $ChangelogEntries
        DescriptionUpdate = "Manual/browser step: v3 API does not expose a main mod description update endpoint."
    }
}

$resolvedModRoot = Resolve-ModRoot -RequestedMod $Mod -RequestedModRoot $ModRoot
$manifest = $null
if (-not [string]::IsNullOrWhiteSpace($resolvedModRoot)) {
    $manifest = Read-ModManifest -Root $resolvedModRoot
    Apply-NexusManifestDefaults -Settings (Read-NexusSettings -Manifest $manifest)
    Apply-NexusManifestDefaults -Settings (Read-LocalApiSettings -Root $resolvedModRoot)
}

Resolve-NexusUrl

if (-not [string]::IsNullOrWhiteSpace($DryRunChangelogBaselineVersion) -and -not $DryRun) {
    throw "DryRunChangelogBaselineVersion may only be used with -DryRun. Live uploads must resolve their baseline from Nexus immediately before upload."
}

if ($manifest -ne $null) {
    if ([string]::IsNullOrWhiteSpace($FileName)) {
        $FileName = [string]$manifest.displayName
    }

    if ([string]::IsNullOrWhiteSpace($FileVersion)) {
        $FileVersion = [string]$manifest.version
    }
}

if ([string]::IsNullOrWhiteSpace($FileName) -and -not [string]::IsNullOrWhiteSpace($ArchivePath)) {
    $FileName = [System.IO.Path]::GetFileNameWithoutExtension($ArchivePath)
}

Resolve-RemoteModId

if ($ListFiles) {
    if ([string]::IsNullOrWhiteSpace($ModId)) {
        throw "Could not resolve Nexus mod id. Provide -ModId or -NexusUrl with a valid API key."
    }

    $files = Invoke-NexusApi -Method GET -Path ("/mods/{0}/files" -f $ModId)
    $rows = @()
    $fileItems = if (Test-JsonProperty -Object $files.data -Name "files") { @($files.data.files) } else { @($files.data.mod_files) }
    foreach ($file in $fileItems) {
        $rows += [pscustomobject]@{
            Id = [string]$file.id
            GameScopedId = if (Test-JsonProperty -Object $file -Name "game_scoped_id") { [string]$file.game_scoped_id } else { "" }
            Name = [string]$file.name
            Category = if (Test-JsonProperty -Object $file -Name "category") { [string]$file.category } else { "" }
        }
    }

    $rows
    return
}

Build-Archive -Root $resolvedModRoot -NoBuild:$SkipBuild -CompileSkip:$SkipCompile -Destination $DestinationDirectory

if ([string]::IsNullOrWhiteSpace($FileVersion)) {
    throw "Could not infer FileVersion. Pass -FileVersion or use a mod manifest."
}

if ([string]::IsNullOrWhiteSpace($FileName)) {
    throw "Could not infer FileName. Pass -FileName or use a mod manifest."
}

$changelogEntries = @()
$changelogBaselineVersion = ""
$changelogIncludedVersions = @()
$changelogSource = ""
$reviewedChangelogPath = ""
$changelogCandidatePath = ""
$rawChangelogEntryCount = 0
if ($AddChangelog) {
    $changelogBaselineVersion = if (-not [string]::IsNullOrWhiteSpace($DryRunChangelogBaselineVersion)) {
        $DryRunChangelogBaselineVersion
    } else {
        (Get-CurrentRemoteFileVersion).Version
    }
    $changelogPackageName = if ($manifest -ne $null -and (Test-JsonProperty -Object $manifest -Name "packageName")) { [string]$manifest.packageName } else { $FileName }
    $changelogPlan = Get-NexusChangelogPlan -Root $resolvedModRoot -PackageName $changelogPackageName -TargetVersion $FileVersion -PublishedVersion $changelogBaselineVersion -ReviewedPath $ConsolidatedChangelogPath
    $changelogEntries = @(Add-KsAddonChangelogHeading `
        -Root $resolvedModRoot `
        -Manifest $manifest `
        -Entries @($changelogPlan.Entries))
    $changelogIncludedVersions = @($changelogPlan.IncludedVersions)
    $changelogSource = [string]$changelogPlan.Source
    $reviewedChangelogPath = [string]$changelogPlan.ReviewedPath
    $changelogCandidatePath = [string]$changelogPlan.CandidatePath
    $rawChangelogEntryCount = [int]$changelogPlan.RawEntryCount
}
$shortDescription = Get-NexusMetadataText -Root $resolvedModRoot -FileName "nexus-short-desc.txt" -MaximumLength 350 -SearchParents
$fileDescriptionSource = Get-NexusMetadataText -Root $resolvedModRoot -FileName "nexus-file-desc.txt" -MaximumLength 255
if ([string]::IsNullOrWhiteSpace($FileDescription) -and -not [string]::IsNullOrWhiteSpace($fileDescriptionSource)) {
    $FileDescription = $fileDescriptionSource
}

if ($FileDescription.Length -gt 255) {
    throw "FileDescription is $($FileDescription.Length) characters, over Nexus file description limit 255."
}

if ($DryRun) {
    Write-PublishPlan -Manifest $manifest -ChangelogEntries $changelogEntries -ChangelogBaselineVersion $changelogBaselineVersion -ChangelogIncludedVersions $changelogIncludedVersions -ChangelogSource $changelogSource -ReviewedChangelogPath $reviewedChangelogPath -ChangelogCandidatePath $changelogCandidatePath -RawChangelogEntryCount $rawChangelogEntryCount -ShortDescription $shortDescription -FileDescriptionSource $fileDescriptionSource
    return
}

$nexusLockModName = $FileName
if ($manifest -ne $null -and (Test-JsonProperty -Object $manifest -Name "packageName") -and -not [string]::IsNullOrWhiteSpace([string]$manifest.packageName)) {
    $nexusLockModName = [string]$manifest.packageName
}

$nexusLock = Enter-GrailwrightLock -Name "nexus" -Action "publish-nexus" -Mod $nexusLockModName -RepoRoot $RepoRoot -TimeoutSeconds $LockWaitSeconds -StaleAfterMinutes $LockStaleAfterMinutes -ForceStaleLock:$ForceStaleLock
try {
    if ($manifest -ne $null) {
        $manifest = Read-ModManifest -Root $resolvedModRoot
        if (-not $PSBoundParameters.ContainsKey("FileName") -or [string]::IsNullOrWhiteSpace($FileName)) {
            $FileName = [string]$manifest.displayName
        }

        if (-not $PSBoundParameters.ContainsKey("FileVersion") -or [string]::IsNullOrWhiteSpace($FileVersion)) {
            $FileVersion = [string]$manifest.version
        }
    }

    if ([string]::IsNullOrWhiteSpace($FileName) -and -not [string]::IsNullOrWhiteSpace($ArchivePath)) {
        $FileName = [System.IO.Path]::GetFileNameWithoutExtension($ArchivePath)
    }

    if ([string]::IsNullOrWhiteSpace($FileVersion)) {
        throw "Could not infer FileVersion. Pass -FileVersion or use a mod manifest."
    }

    if ([string]::IsNullOrWhiteSpace($FileName)) {
        throw "Could not infer FileName. Pass -FileName or use a mod manifest."
    }

    $changelogEntries = @()
    $changelogBaselineVersion = ""
    $changelogIncludedVersions = @()
    $changelogSource = ""
    $reviewedChangelogPath = ""
    $changelogCandidatePath = ""
    $rawChangelogEntryCount = 0
    if ($AddChangelog) {
        $currentRemoteVersion = Get-CurrentRemoteFileVersion
        $changelogBaselineVersion = $currentRemoteVersion.Version
        $changelogPackageName = if ($manifest -ne $null -and (Test-JsonProperty -Object $manifest -Name "packageName")) { [string]$manifest.packageName } else { $FileName }
        $changelogPlan = Get-NexusChangelogPlan -Root $resolvedModRoot -PackageName $changelogPackageName -TargetVersion $FileVersion -PublishedVersion $changelogBaselineVersion -ReviewedPath $ConsolidatedChangelogPath
        $changelogEntries = @(Add-KsAddonChangelogHeading `
            -Root $resolvedModRoot `
            -Manifest $manifest `
            -Entries @($changelogPlan.Entries))
        $changelogIncludedVersions = @($changelogPlan.IncludedVersions)
        $changelogSource = [string]$changelogPlan.Source
        $reviewedChangelogPath = [string]$changelogPlan.ReviewedPath
        $changelogCandidatePath = [string]$changelogPlan.CandidatePath
        $rawChangelogEntryCount = [int]$changelogPlan.RawEntryCount
    }
    $shortDescription = Get-NexusMetadataText -Root $resolvedModRoot -FileName "nexus-short-desc.txt" -MaximumLength 350 -SearchParents
    $fileDescriptionSource = Get-NexusMetadataText -Root $resolvedModRoot -FileName "nexus-file-desc.txt" -MaximumLength 255
    if (-not $PSBoundParameters.ContainsKey("FileDescription") -or [string]::IsNullOrWhiteSpace($FileDescription)) {
        $FileDescription = $fileDescriptionSource
    }

    if ($FileDescription.Length -gt 255) {
        throw "FileDescription is $($FileDescription.Length) characters, over Nexus file description limit 255."
    }

    if ([string]::IsNullOrWhiteSpace($FileDescription)) {
        throw "Missing Nexus file upload description. Add nexus-file-desc.txt beside the mod or pass -FileDescription."
    }

    if ([string]::IsNullOrWhiteSpace($ModFileId)) {
        throw "Pass -ModFileId/-GroupId for new versions of an existing Nexus file. Use -ListFiles to inspect candidates."
    }

    $archiveItem = Get-Item -LiteralPath $ArchivePath
    $archiveMd5 = (Get-FileHash -LiteralPath $archiveItem.FullName -Algorithm MD5).Hash.ToLowerInvariant()
    $archiveSha256 = (Get-FileHash -LiteralPath $archiveItem.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $uploadId = Send-MultipartUpload -Path $archiveItem.FullName
    Invoke-NexusApi -Method POST -Path ("/uploads/{0}/finalise" -f $uploadId) | Out-Null
    Wait-NexusUploadAvailable -UploadId $uploadId

    $versionRequest = @{
        upload_id = $uploadId
        name = $FileName
        version = $FileVersion
        description = $FileDescription
        file_category = $FileCategory
        primary_mod_manager_download = $PrimaryModManagerDownload
        allow_mod_manager_download = $AllowModManagerDownload
        show_requirements_pop_up = $ShowRequirementsPopUp
        update_mod_version = $UpdateModVersion
        archive_existing_file = $ArchiveExistingFile
    }

    if (-not [string]::IsNullOrWhiteSpace($PreviousVersionId)) {
        $versionRequest.previous_version_id = $PreviousVersionId
    }

    if ($AddChangelog) {
        if ([string]::IsNullOrWhiteSpace($ModId)) {
            throw "Cannot add changelog without ModId. Provide -ModId or -NexusUrl so it can be resolved."
        }

        if ($changelogEntries.Count -eq 0) {
            throw "Cannot add changelog: no entries found for version $FileVersion."
        }
    }

    $createdVersion = Invoke-NexusApi -Method POST -Path ("/mod-files/{0}/versions" -f $ModFileId) -Body $versionRequest
    $createdVersionId = Get-NexusCreatedVersionId -CreatedVersion $createdVersion

    $verifiedUpload = $null
    $verificationWarning = ''
    if ([string]::IsNullOrWhiteSpace($createdVersionId)) {
        $verificationWarning = 'Nexus accepted the version POST but did not return data.version.id, so the upload is recorded as a verified write without an exact reread.'
    }
    else {
        try {
            $verifiedUpload = Confirm-NexusUploadedFileVersion -ExpectedVersion $FileVersion -ExpectedDescription $FileDescription -ExpectedVersionId $createdVersionId
            if (-not $verifiedUpload.LabelMatches) {
                $verificationWarning = "Nexus reread matched created version id $createdVersionId but reported label '$($verifiedUpload.Version)' instead of '$FileVersion'."
            }
            elseif ($verifiedUpload.FileDescriptionMismatch) {
                $verificationWarning = "Nexus reread matched created version id $createdVersionId but reported a different file description."
            }
            elseif ([string]::IsNullOrWhiteSpace([string]$verifiedUpload.VortexFileId)) {
                $verificationWarning = "Nexus reread matched created version id $createdVersionId but did not report a positive game_scoped_id, so its Vortex file ID remains pending resolution."
            }
        }
        catch {
            $verificationWarning = "Nexus accepted version $FileVersion (id $createdVersionId), but exact reread verification did not complete: $($_.Exception.Message)"
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($verificationWarning)) {
        Write-Warning "$verificationWarning Do not retry the upload."
    }

    $releaseReceipt = $null
    $releaseReceiptPath = ''
    if ([string]::IsNullOrWhiteSpace($createdVersionId)) {
        Write-Warning "Nexus version $FileVersion was uploaded, but no immutable v3 version ID was returned, so a release receipt could not be recorded. Do not retry the upload."
    }
    else {
        $receiptRecordedAt = (Get-Date).ToUniversalTime().ToString('o')
        $releaseReceipt = New-NexusReleaseReceiptRecord `
            -Manifest $manifest `
            -Archive $archiveItem `
            -ArchiveMd5 $archiveMd5 `
            -ArchiveSha256 $archiveSha256 `
            -CreatedVersion $createdVersion `
            -CreatedVersionId $createdVersionId `
            -VerifiedUpload $verifiedUpload `
            -RecordedAt $receiptRecordedAt `
            -ChangelogStatus $(if ($AddChangelog) { 'pending' } else { 'not-requested' })
        try {
            $releaseReceiptPath = Set-NexusReleaseReceipt -RepoRoot $RepoRoot -Receipt $releaseReceipt
        }
        catch {
            throw "Nexus upload succeeded for version $FileVersion (created version id $createdVersionId), but writing its release receipt failed: $($_.Exception.Message). Do not retry the upload."
        }
    }

    if ($AddChangelog) {
        $changelogRequest = @{
            version = $FileVersion
            changelog = ($changelogEntries -join "`n")
        }

        try {
            Invoke-NexusApi -Method POST -Path ("/mods/{0}/changelogs" -f $ModId) -Body $changelogRequest | Out-Null
        }
        catch {
            $changelogError = $_.Exception.Message
            $receiptUpdateError = ''
            if ($releaseReceipt -ne $null) {
                try {
                    $releaseReceipt.changelog.status = 'failed'
                    $releaseReceipt.changelog.error = $changelogError
                    $releaseReceiptPath = Set-NexusReleaseReceipt -RepoRoot $RepoRoot -Receipt $releaseReceipt
                }
                catch {
                    $receiptUpdateError = " Release receipt update also failed: $($_.Exception.Message)"
                }
            }
            $partialStateError = ''
            try {
                $partialUpdates = New-NexusUploadStateUpdates -Version $FileVersion -FileDescription $FileDescription -ObservedAt ((Get-Date).ToUniversalTime().ToString('o'))
                Set-NexusLiveFileGroupSurfaces -RepoRoot $RepoRoot -NexusUrl $NexusUrl -GroupId $ModFileId -PackageName ([string]$manifest.packageName) -Updates $partialUpdates
            }
            catch {
                $partialStateError = " Local live-state recording also failed: $($_.Exception.Message)"
            }
            throw "Nexus version upload succeeded for $FileVersion, but posting its changelog failed: $changelogError. Do not retry the upload; reconcile the changelog separately.$receiptUpdateError$partialStateError"
        }
        if ($releaseReceipt -ne $null) {
            try {
                $releaseReceipt.changelog.status = 'posted'
                $releaseReceiptPath = Set-NexusReleaseReceipt -RepoRoot $RepoRoot -Receipt $releaseReceipt
            }
            catch {
                throw "Nexus upload and changelog succeeded for version $FileVersion (created version id $createdVersionId), but updating its release receipt failed: $($_.Exception.Message). Do not retry either remote operation."
            }
        }
    }

    $statePackageName = if ($manifest -ne $null -and (Test-JsonProperty -Object $manifest -Name "packageName")) { [string]$manifest.packageName } else { $FileName }
    $stateObservedAt = (Get-Date).ToUniversalTime().ToString('o')
    $snapshotUpdates = New-NexusUploadStateUpdates `
        -Version $(if ($verifiedUpload -ne $null) { $verifiedUpload.Version } else { $FileVersion }) `
        -FileDescription $FileDescription `
        -ChangelogEntries $changelogEntries `
        -ObservedAt $stateObservedAt `
        -VersionReadVerified:($verifiedUpload -ne $null) `
        -FileDescriptionReadVerified:($verifiedUpload -ne $null -and $verifiedUpload.FileDescriptionVerified) `
        -IncludeChangelog:$AddChangelog
    try {
        Set-NexusLiveFileGroupSurfaces -RepoRoot $RepoRoot -NexusUrl $NexusUrl -GroupId $ModFileId -PackageName $statePackageName -Updates $snapshotUpdates
    }
    catch {
        throw "Nexus upload succeeded for version $FileVersion (created version id $createdVersionId), but writing the local live-state snapshot failed: $($_.Exception.Message). The upload was not retried."
    }

    $createdFileGroupId = if ($createdVersion -ne $null -and $createdVersion.data -ne $null -and
        (Test-JsonProperty -Object $createdVersion.data -Name 'file') -and $createdVersion.data.file -ne $null -and
        (Test-JsonProperty -Object $createdVersion.data.file -Name 'id')) { [string]$createdVersion.data.file.id } else { "" }
    [pscustomobject]@{
        UploadedArchive = $archiveItem.FullName
        ArchiveMd5 = $archiveMd5
        ArchiveSha256 = $archiveSha256
        UploadId = $uploadId
        ModFileId = $ModFileId
        CreatedFileId = $createdFileGroupId
        CreatedFileGroupId = $createdFileGroupId
        CreatedVersionId = $createdVersionId
        VortexFileId = if ($verifiedUpload -ne $null) { [string]$verifiedUpload.VortexFileId } else { '' }
        ReleaseReceiptPath = $releaseReceiptPath
        ReleaseReceiptStatus = if ($releaseReceipt -eq $null) { 'not-recorded' } elseif ([string]::IsNullOrWhiteSpace([string]$releaseReceipt.nexus.vortexFileId)) { 'pending-vortex-file-id' } else { 'complete' }
        Version = $FileVersion
        ChangelogAdded = [bool]$AddChangelog
        ReadVerification = if ($verifiedUpload -ne $null) { 'exact-version-id' } else { 'verified-write-only' }
        VerificationWarning = $verificationWarning
        DescriptionUpdate = "Manual/browser step: v3 API does not expose a main mod description update endpoint."
    }
} finally {
    Exit-GrailwrightLock -Lock $nexusLock
}
