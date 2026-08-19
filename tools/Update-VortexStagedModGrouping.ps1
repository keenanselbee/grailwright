[CmdletBinding()]
param(
    [string]$Mod = '',
    [string]$VortexModsRoot = '',
    [string]$VortexMetadataBridgeRoot = '',
    [switch]$Repair
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'VortexNexusMetadataPromotions.ps1')

if ([string]::IsNullOrWhiteSpace($VortexModsRoot)) {
    $VortexModsRoot = Get-VortexNexusMetadataStagingRoot -Candidate '' -GameId 'taintedgrailthefallofavalon'
}
$VortexModsRoot = [System.IO.Path]::GetFullPath($VortexModsRoot)
if (-not (Test-Path -LiteralPath $VortexModsRoot -PathType Container)) {
    throw "Vortex staging root does not exist: $VortexModsRoot"
}

$allManifests = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'mods') -Recurse -File -Filter 'mod.json' | Sort-Object FullName)
$manifests = $allManifests
if (-not [string]::IsNullOrWhiteSpace($Mod)) {
    $manifests = @($manifests | Where-Object {
        $manifest = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        @([string]$manifest.id, [string]$manifest.packageName, [string]$manifest.displayName, (Split-Path -Leaf $_.DirectoryName)) -contains $Mod
    })
    if ($manifests.Count -ne 1) {
        throw "Expected one mod manifest matching '$Mod'; found $($manifests.Count)."
    }
}

$results = New-Object 'System.Collections.Generic.List[object]'
$catalogEntries = @($allManifests | ForEach-Object {
    $manifest = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    $identity = Get-VortexLocalGroupingIdentity -Manifest $manifest -ModRoot $_.DirectoryName
    [pscustomobject]@{
        packageName = [string]$manifest.packageName
        displayName = [string]$manifest.displayName
        stagedNamePrefix = Convert-ToVortexPromotionSafeName -Name ([string]$manifest.displayName)
        modId = $identity.ModId
        logicalFileName = [string]$identity.LogicalFileName
        nexusUrl = [string]$identity.NexusUrl
    }
})
$stagedDirectories = @(Get-ChildItem -LiteralPath $VortexModsRoot -Directory -Force)
foreach ($manifestFile in $manifests) {
    $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
    $safeDisplayName = Convert-ToVortexPromotionSafeName -Name ([string]$manifest.displayName)
    $namePattern = '^' + [regex]::Escape($safeDisplayName) + ' (?<version>[0-9]+\.[0-9]+\.[0-9]+)$'
    foreach ($stagedDirectory in @($stagedDirectories | Where-Object Name -Match $namePattern)) {
        $stagedManifest = $manifest.PSObject.Copy()
        $match = [regex]::Match($stagedDirectory.Name, $namePattern)
        $stagedManifest.version = $match.Groups['version'].Value
        $queued = Queue-VortexLocalMetadataGrouping `
            -Manifest $stagedManifest `
            -ModRoot $manifestFile.DirectoryName `
            -StagedModId $stagedDirectory.Name `
            -StagedPath $stagedDirectory.FullName `
            -VortexModsRoot $VortexModsRoot `
            -BridgeRoot $VortexMetadataBridgeRoot `
            -Repair:$Repair
        $results.Add([pscustomobject]@{
            Mod = [string]$manifest.displayName
            Version = [string]$stagedManifest.version
            StagedId = $stagedDirectory.Name
            Status = [string]$queued.Status
            RequestId = [string]$queued.RequestId
        })
    }
}

$catalogRequest = Queue-VortexLocalGroupingCatalog `
    -Entries $catalogEntries `
    -GameId 'taintedgrailthefallofavalon' `
    -BridgeRoot $VortexMetadataBridgeRoot `
    -Repair:$Repair

$queuedCount = @($results | Where-Object Status -eq 'queued').Count
$completedCount = @($results | Where-Object Status -eq 'completed').Count
Write-Host "Vortex grouping requests: $queuedCount queued, $completedCount already completed, $($results.Count) staged versions matched."
Write-Host "Vortex state catalog: $($catalogEntries.Count) mods, request $($catalogRequest.Status) ($($catalogRequest.RequestId))."
$results
