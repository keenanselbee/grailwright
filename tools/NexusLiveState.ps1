Set-StrictMode -Version Latest

$script:NexusLiveStateSchemaVersion = 1

function Get-NexusLiveStatePath {
    param([string]$RepoRoot)
    return Join-Path ([System.IO.Path]::GetFullPath($RepoRoot)) "nexus-live-state.local.json"
}

function Normalize-NexusLiveText {
    param([AllowNull()][object]$Value)
    $text = ([string]$Value) -replace "`r`n", "`n" -replace "`r", "`n" -replace [char]0x00a0, " "
    $text = $text -replace "`n\[/code\]", "[/code]"
    $inCodeBlock = $false
    $lines = foreach ($line in ($text.Trim() -split "`n")) {
        if ($line -match '(?i)\[code\]') { $inCodeBlock = $true }
        $normalized = if ($inCodeBlock) { $line.TrimStart() } else { $line }
        if ($normalized -match '(?i)\[/code\]') { $inCodeBlock = $false }
        $normalized
    }
    return ($lines -join "`n").Trim()
}

function Get-NexusLiveTextHash {
    param([AllowNull()][object]$Value)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes((Normalize-NexusLiveText -Value $Value))
    return ([System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
}

function Test-NexusLiveVersionIdMatch {
    param([object]$Version, [string]$ExpectedVersionId)
    return $Version -ne $null -and (Test-NexusLiveProperty -Object $Version -Name 'id') -and [string]$Version.id -eq $ExpectedVersionId
}

function New-NexusUploadStateUpdates {
    param(
        [string]$Version,
        [string]$FileDescription,
        [string[]]$ChangelogEntries = @(),
        [string]$ObservedAt,
        [switch]$VersionReadVerified,
        [switch]$FileDescriptionReadVerified,
        [switch]$IncludeChangelog
    )
    $updates = @(
        [pscustomobject]@{
            Surface = 'version'
            Content = $Version
            ObservedAt = $ObservedAt
            Source = if ($VersionReadVerified) { 'nexus-v3-upload-read-verified' } else { 'nexus-v3-upload-verified-write' }
            Status = if ($VersionReadVerified) { 'verified-read' } else { 'verified-write' }
        },
        [pscustomobject]@{
            Surface = 'fileDescription'
            Content = $FileDescription
            ObservedAt = $ObservedAt
            Source = if ($FileDescriptionReadVerified) { 'nexus-v3-upload-read-verified' } else { 'nexus-v3-upload-verified-write' }
            Status = if ($FileDescriptionReadVerified) { 'verified-read' } else { 'verified-write' }
        }
    )
    if ($IncludeChangelog) {
        $updates += [pscustomobject]@{
            Surface = 'changelog'
            Content = $ChangelogEntries -join "`n"
            ObservedAt = $ObservedAt
            Source = 'nexus-v3-upload-verified-write'
            Status = 'verified-write'
        }
    }
    return $updates
}

function New-NexusBrowserFileGroupStateUpdates {
    param(
        [string]$Version,
        [AllowEmptyString()][string]$FileDescription,
        [AllowEmptyString()][string]$Changelog,
        [string]$ObservedAt = ((Get-Date).ToUniversalTime().ToString('o'))
    )
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw 'Browser file-group evidence requires a non-empty version.'
    }

    return @(
        [pscustomobject]@{
            Surface = 'version'
            Content = $Version
            ObservedAt = $ObservedAt
            Source = 'nexus-browser-file-review'
            Status = 'verified-read'
        },
        [pscustomobject]@{
            Surface = 'fileDescription'
            Content = $FileDescription
            ObservedAt = $ObservedAt
            Source = 'nexus-browser-file-review'
            Status = 'verified-read'
        },
        [pscustomobject]@{
            Surface = 'changelog'
            Content = $Changelog
            ObservedAt = $ObservedAt
            Source = 'nexus-browser-file-review'
            Status = 'verified-read'
        }
    )
}

function New-NexusLiveState {
    return [pscustomobject]@{
        schemaVersion = $script:NexusLiveStateSchemaVersion
        updatedAt = $null
        pages = @()
        fileGroups = @()
    }
}

function Test-NexusLiveProperty {
    param([object]$Object, [string]$Name)
    return $null -ne $Object -and @($Object.PSObject.Properties | ForEach-Object Name) -contains $Name
}

function Read-NexusLiveState {
    param([string]$RepoRoot, [switch]$AllowMissing)
    $path = Get-NexusLiveStatePath -RepoRoot $RepoRoot
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($AllowMissing) { return New-NexusLiveState }
        throw "Nexus live state does not exist: $path"
    }
    $state = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    if ($state.schemaVersion -ne $script:NexusLiveStateSchemaVersion) {
        throw "Unsupported Nexus live-state schema '$($state.schemaVersion)' in $path."
    }
    if ($null -eq $state.pages) { $state | Add-Member -NotePropertyName pages -NotePropertyValue @() }
    if ($null -eq $state.fileGroups) { $state | Add-Member -NotePropertyName fileGroups -NotePropertyValue @() }
    return $state
}

function Write-NexusLiveState {
    param([string]$RepoRoot, [object]$State)
    $path = Get-NexusLiveStatePath -RepoRoot $RepoRoot
    $State.schemaVersion = $script:NexusLiveStateSchemaVersion
    $State.updatedAt = (Get-Date).ToUniversalTime().ToString('o')
    $temporaryPath = "$path.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText($temporaryPath, ($State | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $temporaryPath -Destination $path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
}

function Get-NexusLivePageKey {
    param([string]$GameDomain, [string]$PageId)
    return "$GameDomain/mods/$PageId"
}

function Get-NexusLivePageFromUrl {
    param([string]$NexusUrl)
    $uri = [uri]$NexusUrl
    $parts = @($uri.AbsolutePath.Trim('/') -split '/')
    $mods = [array]::IndexOf($parts, 'mods')
    if ($mods -lt 1 -or $mods + 1 -ge $parts.Count) { throw "Cannot identify Nexus game/page from '$NexusUrl'." }
    return [pscustomobject]@{ GameDomain = $parts[$mods - 1]; PageId = $parts[$mods + 1]; NexusUrl = "https://www.nexusmods.com/games/$($parts[$mods - 1])/mods/$($parts[$mods + 1])" }
}

function New-NexusLiveSurface {
    param(
        [AllowNull()][object]$Content,
        [string]$ObservedAt,
        [string]$Source,
        [ValidateSet('verified-read', 'verified-write')]
        [string]$Status = 'verified-read'
    )
    $normalized = Normalize-NexusLiveText -Value $Content
    return [pscustomobject]@{ status = $Status; content = $normalized; normalizedSha256 = Get-NexusLiveTextHash -Value $normalized; observedAt = $ObservedAt; source = $Source }
}

function Set-NexusLivePageSurface {
    param([string]$RepoRoot, [string]$NexusUrl, [ValidateSet('shortDescription','fullDescription')][string]$Surface, [AllowNull()][object]$Content, [string]$ObservedAt = ((Get-Date).ToUniversalTime().ToString('o')), [string]$Source = 'nexus-browser-review', [ValidateSet('verified-read', 'verified-write')][string]$Status = 'verified-read')
    Set-NexusLivePageSurfaces -RepoRoot $RepoRoot -NexusUrl $NexusUrl -Updates @([pscustomobject]@{ Surface=$Surface; Content=$Content; ObservedAt=$ObservedAt; Source=$Source; Status=$Status })
}

function Set-NexusLivePageSurfaces {
    param([string]$RepoRoot, [string]$NexusUrl, [object[]]$Updates)
    if (@($Updates).Count -eq 0) { return }
    $state = Read-NexusLiveState -RepoRoot $RepoRoot -AllowMissing
    $identity = Get-NexusLivePageFromUrl -NexusUrl $NexusUrl
    $key = Get-NexusLivePageKey -GameDomain $identity.GameDomain -PageId $identity.PageId
    $page = @($state.pages | Where-Object { $_.key -eq $key }) | Select-Object -First 1
    if ($null -eq $page) { $page = [pscustomobject]@{ key=$key; gameDomain=$identity.GameDomain; pageId=$identity.PageId; nexusUrl=$identity.NexusUrl; surfaces=[pscustomobject]@{} }; $state.pages = @($state.pages) + @($page) }
    if ($null -eq $page.surfaces) { $page.surfaces = [pscustomobject]@{} }
    foreach ($update in $Updates) {
        if ([string]$update.Surface -notin @('shortDescription', 'fullDescription')) { throw "Unsupported Nexus page surface '$($update.Surface)'." }
        $surface = [string]$update.Surface
        $liveSurface = New-NexusLiveSurface -Content $update.Content -ObservedAt $update.ObservedAt -Source $update.Source -Status $update.Status
        if (Test-NexusLiveProperty -Object $page.surfaces -Name $surface) { $page.surfaces.$surface = $liveSurface }
        else { $page.surfaces | Add-Member -NotePropertyName $surface -NotePropertyValue $liveSurface }
    }
    Write-NexusLiveState -RepoRoot $RepoRoot -State $state
}

function Set-NexusLiveFileGroupSurface {
    param([string]$RepoRoot, [string]$NexusUrl, [string]$GroupId, [string]$PackageName, [ValidateSet('version','fileDescription','changelog')][string]$Surface, [AllowNull()][object]$Content, [string]$ObservedAt = ((Get-Date).ToUniversalTime().ToString('o')), [string]$Source = 'nexus-v3-read', [ValidateSet('verified-read', 'verified-write')][string]$Status = 'verified-read')
    Set-NexusLiveFileGroupSurfaces -RepoRoot $RepoRoot -NexusUrl $NexusUrl -GroupId $GroupId -PackageName $PackageName -Updates @([pscustomobject]@{ Surface=$Surface; Content=$Content; ObservedAt=$ObservedAt; Source=$Source; Status=$Status })
}

function Set-NexusLiveFileGroupSurfaces {
    param([string]$RepoRoot, [string]$NexusUrl, [string]$GroupId, [string]$PackageName, [object[]]$Updates)
    if (@($Updates).Count -eq 0) { return }
    $state = Read-NexusLiveState -RepoRoot $RepoRoot -AllowMissing
    $identity = Get-NexusLivePageFromUrl -NexusUrl $NexusUrl
    $pageKey = Get-NexusLivePageKey -GameDomain $identity.GameDomain -PageId $identity.PageId
    $key = "$pageKey/groups/$GroupId"
    $group = @($state.fileGroups | Where-Object { $_.key -eq $key }) | Select-Object -First 1
    if ($null -eq $group) { $group = [pscustomobject]@{ key=$key; pageKey=$pageKey; groupId=[string]$GroupId; packageName=$PackageName; surfaces=[pscustomobject]@{} }; $state.fileGroups = @($state.fileGroups) + @($group) }
    if (-not [string]::IsNullOrWhiteSpace($PackageName)) { $group.packageName = $PackageName }
    if ($null -eq $group.surfaces) { $group.surfaces = [pscustomobject]@{} }
    foreach ($update in $Updates) {
        if ([string]$update.Surface -notin @('version', 'fileDescription', 'changelog')) { throw "Unsupported Nexus file-group surface '$($update.Surface)'." }
        $surface = [string]$update.Surface
        $liveSurface = New-NexusLiveSurface -Content $update.Content -ObservedAt $update.ObservedAt -Source $update.Source -Status $update.Status
        if (Test-NexusLiveProperty -Object $group.surfaces -Name $surface) { $group.surfaces.$surface = $liveSurface }
        else { $group.surfaces | Add-Member -NotePropertyName $surface -NotePropertyValue $liveSurface }
    }
    Write-NexusLiveState -RepoRoot $RepoRoot -State $state
}

function Get-NexusLiveConfiguredGroupId {
    param([string]$Root)
    $path = Join-Path $Root 'API.txt'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return '' }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*GroupId\s*=\s*(?<value>[^#\s]+)') { return $Matches.value }
    }
    return ''
}

function Get-NexusLiveConfiguredNexusUrl {
    param([string]$Root)
    $path = Join-Path $Root 'API.txt'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return '' }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*NexusUrl\s*=\s*(?<value>\S+)') { return $Matches.value }
    }
    return ''
}

function Read-NexusDescriptionResult {
    param([string]$ResultPath, [string[]]$ExpectedStatuses)
    if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) { throw "Nexus browser invocation succeeded but did not create its required result file: $ResultPath" }
    try { $result = [System.IO.File]::ReadAllText($ResultPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json }
    catch { throw "Nexus browser invocation produced an invalid result file '$ResultPath': $($_.Exception.Message)" }
    if ($result -eq $null -or -not (Test-NexusLiveProperty -Object $result -Name 'status')) { throw "Nexus browser result file '$ResultPath' has no status." }
    if ([string]$result.status -notin $ExpectedStatuses) { throw "Nexus browser result status '$($result.status)' was not expected. Expected: $($ExpectedStatuses -join ', ')." }
    foreach ($name in @('observedShortDescription', 'observedFullDescription')) {
        if (-not (Test-NexusLiveProperty -Object $result -Name $name)) { throw "Nexus browser result file '$ResultPath' is missing $name." }
    }
    return $result
}

function Get-NexusLiveDesiredText {
    param([string]$Root, [string]$FileName, [string]$RepoRoot, [switch]$SearchParents)
    $current = $Root
    $modsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'mods')).TrimEnd('\')
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $path = Join-Path $current $FileName
        if (Test-Path -LiteralPath $path -PathType Leaf) { return Get-Content -LiteralPath $path -Raw }
        if (-not $SearchParents -or $current.TrimEnd('\') -eq $modsRoot) { break }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or -not $parent.StartsWith($modsRoot, [System.StringComparison]::OrdinalIgnoreCase)) { break }
        $current = $parent
    }
    return ''
}

function Get-NexusLiveDesiredChangelog {
    param([string]$Root, [object]$Manifest)
    $path = Join-Path $Root 'nexus-changelog.txt'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return '' }
    $lines = @(Get-Content -LiteralPath $path | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($lines.Count -lt 3 -or $lines[0] -notmatch '^TargetVersion=' -or $lines[1] -notmatch '^BaselineVersion=') { return '' }
    $entries = @($lines | Select-Object -Skip 2)
    if ($Root -match '[\\/]mods[\\/]KSAddons[\\/]' -and $entries.Count -gt 0 -and $entries[0] -ne [string]$Manifest.displayName) {
        $entries = @([string]$Manifest.displayName) + $entries
    }
    return $entries -join "`n"
}

function Get-NexusLiveComparison {
    param([AllowNull()][object]$Live, [AllowNull()][object]$Desired, [double]$MaxAgeHours)
    if ($null -eq $Live) { return [pscustomobject]@{ Status='unknown'; Comparison='unknown'; AgeHours=$null } }
    $comparison = if ((Get-NexusLiveTextHash $Desired) -eq $Live.normalizedSha256) { 'current' } else { 'drift' }
    $ageHours = if ([string]::IsNullOrWhiteSpace([string]$Live.observedAt)) { [double]::PositiveInfinity } else { ([datetime]::UtcNow - ([datetime]$Live.observedAt).ToUniversalTime()).TotalHours }
    $status = if ($MaxAgeHours -gt 0 -and $ageHours -gt $MaxAgeHours) { 'stale' } else { $comparison }
    return [pscustomobject]@{ Status=$status; Comparison=$comparison; AgeHours=[math]::Round($ageHours, 1) }
}

function Get-NexusLiveStateReport {
    param([string]$RepoRoot, [string]$Mod = '', [double]$MaxAgeHours = 168)
    $state = Read-NexusLiveState -RepoRoot $RepoRoot -AllowMissing
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($manifestFile in Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'mods') -Recurse -Filter mod.json -File) {
        $root = Split-Path -Parent $manifestFile.FullName; $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
        $package = [string]$manifest.packageName
        if (-not [string]::IsNullOrWhiteSpace($Mod) -and $package -notlike $Mod -and [string]$manifest.displayName -notlike $Mod) { continue }
        $configuredNexusUrl = Get-NexusLiveConfiguredNexusUrl -Root $root
        $configuredPage = if (-not [string]::IsNullOrWhiteSpace($configuredNexusUrl)) { Get-NexusLivePageFromUrl -NexusUrl $configuredNexusUrl } else { $null }
        $configuredPageKey = if ($configuredPage) { Get-NexusLivePageKey -GameDomain $configuredPage.GameDomain -PageId $configuredPage.PageId } else { '' }
        $configuredGroupId = Get-NexusLiveConfiguredGroupId -Root $root
        $group = if ([string]::IsNullOrWhiteSpace($configuredGroupId) -or [string]::IsNullOrWhiteSpace($configuredPageKey)) {
            $null
        }
        else {
            @($state.fileGroups | Where-Object { [string]$_.groupId -eq $configuredGroupId -and [string]$_.pageKey -eq $configuredPageKey }) | Select-Object -First 1
        }
        $pageKey = if ($group) { $group.pageKey } else { $configuredPageKey }
        $page = if ($pageKey) { @($state.pages | Where-Object { $_.key -eq $pageKey }) | Select-Object -First 1 } else { $null }
        $desired = @{
            version = [string]$manifest.version
            fileDescription = Get-NexusLiveDesiredText -Root $root -FileName 'nexus-file-desc.txt' -RepoRoot $RepoRoot
            shortDescription = Get-NexusLiveDesiredText -Root $root -FileName 'nexus-short-desc.txt' -RepoRoot $RepoRoot -SearchParents
            fullDescription = Get-NexusLiveDesiredText -Root $root -FileName 'nexus-full-desc.txt' -RepoRoot $RepoRoot -SearchParents
            changelog = Get-NexusLiveDesiredChangelog -Root $root -Manifest $manifest
        }
        $nexusVersion = if ($group -and (Test-NexusLiveProperty -Object $group.surfaces -Name 'version')) { [string]$group.surfaces.version.content } else { '' }
        foreach ($surface in @('version','fileDescription','changelog')) {
            $live = if ($group -and (Test-NexusLiveProperty -Object $group.surfaces -Name $surface)) { $group.surfaces.$surface } else { $null }
            $comparison = Get-NexusLiveComparison -Live $live -Desired $desired[$surface] -MaxAgeHours $MaxAgeHours
            $rows.Add([pscustomobject]@{Package=$package; NexusVersion=$nexusVersion; LocalVersion=$desired.version; Surface=$surface; Status=$comparison.Status; Comparison=$comparison.Comparison; AgeHours=$comparison.AgeHours; Evidence=if($live){$live.status}else{''}; ObservedAt=if($live){$live.observedAt}else{''}; Source=if($live){$live.source}else{''}})
        }
        foreach ($surface in @('shortDescription','fullDescription')) {
            $live = if ($page -and (Test-NexusLiveProperty -Object $page.surfaces -Name $surface)) { $page.surfaces.$surface } else { $null }
            $comparison = Get-NexusLiveComparison -Live $live -Desired $desired[$surface] -MaxAgeHours $MaxAgeHours
            $rows.Add([pscustomobject]@{Package=$package; NexusVersion=$nexusVersion; LocalVersion=$desired.version; Surface=$surface; Status=$comparison.Status; Comparison=$comparison.Comparison; AgeHours=$comparison.AgeHours; Evidence=if($live){$live.status}else{''}; ObservedAt=if($live){$live.observedAt}else{''}; Source=if($live){$live.source}else{''}})
        }
    }
    return $rows
}
