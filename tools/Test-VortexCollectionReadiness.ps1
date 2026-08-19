[CmdletBinding()]
param(
    [string]$VortexMetadataBridgeRoot = '',
    [switch]$ReportOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'VortexNexusMetadataPromotions.ps1')

$bridgeRoot = Get-VortexNexusMetadataBridgeRoot -Candidate $VortexMetadataBridgeRoot
$readinessPath = Join-Path $bridgeRoot 'collection-readiness.json'
if (-not (Test-Path -LiteralPath $readinessPath -PathType Leaf)) {
    throw "No Vortex collection-readiness snapshot exists. Install and restart the Grailwright Nexus Metadata extension, then keep Tainted Grail active in Vortex."
}

$snapshot = Get-Content -LiteralPath $readinessPath -Raw | ConvertFrom-Json
if ([int]$snapshot.schemaVersion -ne 2 -or [string]$snapshot.gameId -ne 'taintedgrailthefallofavalon') {
    throw "Unsupported Vortex collection-readiness snapshot: $readinessPath"
}

$rows = @($snapshot.entries | ForEach-Object {
    [pscustomobject]@{
        Mod = [string]$_.displayName
        Version = [string]$_.version
        Source = [string]$_.source
        NexusModId = [string]$_.modId
        ExpectedNexusModId = if ($_.PSObject.Properties.Name -contains 'expectedModId') { [string]$_.expectedModId } else { '' }
        NexusFileId = [string]$_.fileId
        Coverage = [string]$_.coverage
        Ready = [bool]$_.ready
    }
})
$rows

$notReady = @($rows | Where-Object { -not $_.Ready })
$blockingReasons = New-Object 'System.Collections.Generic.List[string]'
if (-not [bool]$snapshot.catalogAvailable) {
    $blockingReasons.Add('No authored Grailwright grouping catalog is available. Run tools/Update-VortexStagedModGrouping.ps1 and let the extension refresh the snapshot.')
}
if ([int]$snapshot.invalidCatalogRecordCount -gt 0) {
    $blockingReasons.Add("$($snapshot.invalidCatalogRecordCount) Grailwright grouping catalog record(s) are invalid.")
}
if ($rows.Count -eq 0) {
    $blockingReasons.Add('No enabled Grailwright mods were recognized, so collection readiness cannot be certified.')
}
if ([int]$snapshot.unaccountedEnabledCount -gt 0) {
    $blockingReasons.Add("$($snapshot.unaccountedEnabledCount) enabled Grailwright mod(s) are missing catalog or Vortex grouping metadata.")
}
$identityMismatches = @($rows | Where-Object Coverage -eq 'nexus-id-mismatch')
if ($identityMismatches.Count -gt 0) {
    $blockingReasons.Add("$($identityMismatches.Count) enabled Grailwright mod(s) point at the wrong Nexus page ID.")
}
$missingRecords = @($rows | Where-Object Coverage -eq 'missing-vortex-record')
if ($missingRecords.Count -gt 0) {
    $blockingReasons.Add("$($missingRecords.Count) enabled Grailwright profile entry or entries have no persisted Vortex mod record.")
}
$coveredLocalBuilds = @($rows | Where-Object { -not $_.Ready -and $_.Coverage -eq 'covered' })
if ($coveredLocalBuilds.Count -gt 0) {
    $blockingReasons.Add("$($coveredLocalBuilds.Count) enabled Grailwright mod(s) are local test builds. Publish and promote those exact versions before updating the collection.")
}
if ($blockingReasons.Count -gt 0) {
    if (-not $ReportOnly) {
        throw ($blockingReasons -join ' ')
    }
    foreach ($reason in $blockingReasons) {
        Write-Warning $reason
    }
}

Write-Host "Collection readiness: $($rows.Count - $notReady.Count)/$($rows.Count) enabled Grailwright mods are exact Nexus-backed releases."
