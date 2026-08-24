[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'NexusLiveState.ps1')
$temp = Join-Path $repo ('.codex-temp\\nexus-live-state-test-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path (Join-Path $temp 'mods\\Demo') -Force | Out-Null
    '{"packageName":"Demo","displayName":"Demo","version":"1.0.0"}' | Set-Content (Join-Path $temp 'mods\\Demo\\mod.json')
    "NexusUrl=https://www.nexusmods.com/games/demo/mods/1`nGroupId=2" | Set-Content (Join-Path $temp 'mods\\Demo\\API.txt')
    'Short' | Set-Content (Join-Path $temp 'mods\\Demo\\nexus-short-desc.txt')
    'Full' | Set-Content (Join-Path $temp 'mods\\Demo\\nexus-full-desc.txt')
    'File row' | Set-Content (Join-Path $temp 'mods\\Demo\\nexus-file-desc.txt')
    "TargetVersion=1.0.0`nBaselineVersion=0.9.9`nChanged" | Set-Content (Join-Path $temp 'mods\\Demo\\nexus-changelog.txt')
    Set-NexusLivePageSurface -RepoRoot $temp -NexusUrl 'https://www.nexusmods.com/games/demo/mods/1' -Surface shortDescription -Content "Short`r`n" -ObservedAt '2026-08-11T00:00:00.0000000Z'
    Set-NexusLivePageSurface -RepoRoot $temp -NexusUrl 'https://www.nexusmods.com/games/demo/mods/1' -Surface fullDescription -Content 'Elsewhere'
    Set-NexusLiveFileGroupSurfaces -RepoRoot $temp -NexusUrl 'https://www.nexusmods.com/games/demo/mods/1' -GroupId 2 -PackageName Demo -Updates @(
        [pscustomobject]@{ Surface='version'; Content='1.0.0'; ObservedAt='2025-01-01T00:00:00.0000000Z'; Source='nexus-v3-read'; Status='verified-read' },
        [pscustomobject]@{ Surface='fileDescription'; Content='File row'; ObservedAt='2026-08-11T00:00:00.0000000Z'; Source='nexus-v3-write'; Status='verified-write' },
        [pscustomobject]@{ Surface='changelog'; Content='Changed'; ObservedAt='2026-08-11T00:00:00.0000000Z'; Source='nexus-v3-write'; Status='verified-write' }
    )
    Set-NexusLivePageSurface -RepoRoot $temp -NexusUrl 'https://www.nexusmods.com/games/demo/mods/1' -Surface shortDescription -Content ("Caf" + [char]0x00e9)
    Set-NexusLivePageSurface -RepoRoot $temp -NexusUrl 'https://www.nexusmods.com/games/demo/mods/1' -Surface shortDescription -Content 'Short'
    $state = Read-NexusLiveState -RepoRoot $temp
    if ($state.schemaVersion -ne 1 -or $state.pages.Count -ne 1 -or $state.fileGroups.Count -ne 1) { throw 'State schema/entity write failed.' }
    if (@($state.fileGroups[0].surfaces.PSObject.Properties).Count -ne 3) { throw 'Atomic file-group surface transaction failed.' }
    if ($state.fileGroups[0].surfaces.fileDescription.status -ne 'verified-write') { throw 'Verified-write evidence was not retained.' }
    $writeEvidence = @(New-NexusUploadStateUpdates -Version '1.0.1' -FileDescription 'Pitch' -ChangelogEntries @('Changed') -ObservedAt '2026-08-11T00:00:00Z' -IncludeChangelog)
    if (@($writeEvidence | Where-Object Status -eq 'verified-write').Count -ne 3) { throw 'Remote-success verified-write classification failed.' }
    $readEvidence = @(New-NexusUploadStateUpdates -Version '1.0.1' -FileDescription 'Pitch' -ObservedAt '2026-08-11T00:00:00Z' -VersionReadVerified -FileDescriptionReadVerified)
    if (@($readEvidence | Where-Object Status -eq 'verified-read').Count -ne 2) { throw 'Exact-reread evidence classification failed.' }
    if ($state.pages[0].surfaces.shortDescription.normalizedSha256 -ne (Get-NexusLiveTextHash 'Short')) { throw 'Normalized hash failed.' }
    if ((Get-Item -LiteralPath (Get-NexusLiveStatePath -RepoRoot $temp)).Length -gt 10000) { throw 'Repeated UTF-8 state writes expanded unexpectedly.' }
    $report = Get-NexusLiveStateReport -RepoRoot $temp -MaxAgeHours 0
    if ((@($report | Where-Object { $_.Surface -eq 'version' -and $_.Status -eq 'current' }).Count) -ne 1) { throw 'Current version report failed.' }
    $versionRow = @($report | Where-Object Surface -eq 'version')[0]
    if ($versionRow.NexusVersion -ne '1.0.0' -or $versionRow.LocalVersion -ne '1.0.0') { throw 'Version labels were not included in the report.' }
    if ((@($report | Where-Object { $_.Surface -eq 'fullDescription' -and $_.Status -eq 'drift' }).Count) -ne 1) { throw 'Description drift report failed.' }
    $staleReport = Get-NexusLiveStateReport -RepoRoot $temp
    if ((@($staleReport | Where-Object { $_.Surface -eq 'version' -and $_.Status -eq 'stale' }).Count) -ne 1) { throw 'Stale state report failed.' }

    $browserObservedAt = (Get-Date).ToUniversalTime().ToString('o')
    $browserEvidence = @(New-NexusBrowserFileGroupStateUpdates -Version '1.0.0' -FileDescription 'File row' -Changelog 'Changed' -ObservedAt $browserObservedAt)
    if ($browserEvidence.Count -ne 3) { throw 'Browser file-group fallback did not produce all three surfaces.' }
    if (@($browserEvidence | Where-Object { $_.Status -ne 'verified-read' -or $_.Source -ne 'nexus-browser-file-review' }).Count -ne 0) { throw 'Browser file-group fallback did not classify its evidence as verified-read.' }
    if (@($browserEvidence.Surface | Sort-Object -Unique) -join ',' -ne 'changelog,fileDescription,version') { throw 'Browser file-group fallback did not include the expected surfaces.' }
    Set-NexusLiveFileGroupSurfaces -RepoRoot $temp -NexusUrl 'https://www.nexusmods.com/games/demo/mods/1' -GroupId 2 -PackageName Demo -Updates $browserEvidence
    $browserFallbackReport = Get-NexusLiveStateReport -RepoRoot $temp
    $browserFallbackRows = @($browserFallbackReport | Where-Object { $_.Surface -in @('version', 'fileDescription', 'changelog') })
    if ($browserFallbackRows.Count -ne 3) { throw 'Browser file-group fallback report did not include all persisted surfaces.' }
    if (@($browserFallbackRows | Where-Object { $_.Status -ne 'current' -or $_.Comparison -ne 'current' }).Count -ne 0) { throw 'Fresh browser file-group fallback evidence did not report current.' }
    if (@($browserFallbackRows | Where-Object { $_.Evidence -ne 'verified-read' -or $_.Source -ne 'nexus-browser-file-review' }).Count -ne 0) { throw 'Browser file-group fallback provenance was not retained in the report.' }

    "NexusUrl=https://www.nexusmods.com/games/demo/mods/1`nGroupId=999" | Set-Content (Join-Path $temp 'mods\\Demo\\API.txt')
    $unknownReport = Get-NexusLiveStateReport -RepoRoot $temp
    if ((@($unknownReport | Where-Object { $_.Surface -eq 'version' -and $_.Status -eq 'unknown' }).Count) -ne 1) { throw 'Configured GroupId change did not report unknown.' }

    New-Item -ItemType Directory -Path (Join-Path $temp 'mods\\KSAddons\\SharedAddon') -Force | Out-Null
    '{"packageName":"SharedAddon","displayName":"Shared Addon","version":"1.0.0"}' | Set-Content (Join-Path $temp 'mods\\KSAddons\\SharedAddon\\mod.json')
    "NexusUrl=https://www.nexusmods.com/games/demo/mods/55`nGroupId=8" | Set-Content (Join-Path $temp 'mods\\KSAddons\\SharedAddon\\API.txt')
    'Shared short' | Set-Content (Join-Path $temp 'mods\\KSAddons\\nexus-short-desc.txt')
    'Shared full' | Set-Content (Join-Path $temp 'mods\\KSAddons\\nexus-full-desc.txt')
    Set-NexusLivePageSurfaces -RepoRoot $temp -NexusUrl 'https://www.nexusmods.com/games/demo/mods/55' -Updates @(
        [pscustomobject]@{ Surface='shortDescription'; Content='Shared short'; ObservedAt=(Get-Date).ToUniversalTime().ToString('o'); Source='test'; Status='verified-read' },
        [pscustomobject]@{ Surface='fullDescription'; Content='Shared full'; ObservedAt=(Get-Date).ToUniversalTime().ToString('o'); Source='test'; Status='verified-read' }
    )
    $sharedReport = Get-NexusLiveStateReport -RepoRoot $temp -Mod SharedAddon
    if ((@($sharedReport | Where-Object { $_.Surface -eq 'shortDescription' -and $_.Status -eq 'current' }).Count) -ne 1) { throw 'Shared KS parent description report failed.' }

    if (-not (Test-NexusLiveVersionIdMatch -Version ([pscustomobject]@{ id='42'; version='1.0.0' }) -ExpectedVersionId '42')) { throw 'Exact created-version id correlation failed.' }
    if (Test-NexusLiveVersionIdMatch -Version ([pscustomobject]@{ id='42'; version='1.0.0' }) -ExpectedVersionId '43') { throw 'Exact version id correlation accepted a different id.' }
    $missingResult = Join-Path $temp 'missing-result.json'
    try { Read-NexusDescriptionResult -ResultPath $missingResult -ExpectedStatuses @('reviewed'); throw 'Missing browser result was accepted.' } catch { if ($_.Exception.Message -notmatch 'required result file') { throw } }
    '{not json' | Set-Content (Join-Path $temp 'invalid-result.json')
    try { Read-NexusDescriptionResult -ResultPath (Join-Path $temp 'invalid-result.json') -ExpectedStatuses @('reviewed'); throw 'Invalid browser result was accepted.' } catch { if ($_.Exception.Message -notmatch 'invalid result file') { throw } }
    '{"status":"unexpected","observedShortDescription":"","observedFullDescription":""}' | Set-Content (Join-Path $temp 'unexpected-result.json')
    try { Read-NexusDescriptionResult -ResultPath (Join-Path $temp 'unexpected-result.json') -ExpectedStatuses @('reviewed'); throw 'Unexpected browser result status was accepted.' } catch { if ($_.Exception.Message -notmatch 'not expected') { throw } }
    Write-Host 'Nexus live-state tests passed.'
}
finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force } }
