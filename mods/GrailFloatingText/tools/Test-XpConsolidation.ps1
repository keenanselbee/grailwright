[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$gftSource = Get-Content -LiteralPath (Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$bloodMagicSource = Get-Content -LiteralPath (Join-Path $repoRoot "mods\BloodMagicExpansion\src\BloodMagicExpansion.cs") -Raw

if (-not [regex]::IsMatch(
    $gftSource,
    'Config\.Bind\("9\. Default Game Events",\s*"ConsolidateXpGains",\s*true,')) {
    throw "ConsolidateXpGains must be enabled by default."
}

if ($gftSource.IndexOf("TryClaimConsolidatedXpGain", [StringComparison]::Ordinal) -lt 0) {
    throw "The API v8 consolidated XP claim method is missing."
}

$mergeStart = $gftSource.IndexOf("private static bool CanConsolidateXpBatches(", [StringComparison]::Ordinal)
$mergeEnd = $gftSource.IndexOf("private bool TryShowXpBatch(", $mergeStart, [StringComparison]::Ordinal)
if ($mergeStart -lt 0 -or $mergeEnd -le $mergeStart) {
    throw "Could not locate the XP batch compatibility method."
}

$mergeMethod = $gftSource.Substring($mergeStart, $mergeEnd - $mergeStart)
foreach ($requiredIdentity in @("SourceId", "ConsolidationKey", "EventId", "TextFormat")) {
    if ($mergeMethod.IndexOf($requiredIdentity, [StringComparison]::Ordinal) -lt 0) {
        throw "XP batch compatibility no longer includes $requiredIdentity."
    }
}

$queueStart = $gftSource.IndexOf("private void QueueXpBatch(", [StringComparison]::Ordinal)
$queueEnd = $gftSource.IndexOf("private static bool CanConsolidateXpBatches(", $queueStart, [StringComparison]::Ordinal)
$queueMethod = $gftSource.Substring($queueStart, $queueEnd - $queueStart)
if ($queueMethod.IndexOf("StartTime", [StringComparison]::Ordinal) -ge 0) {
    throw "Queued XP consolidation must not restart the visible notification timer."
}

foreach ($requiredBloodMagicKey in @(
    '"corpse-xp-" + qualityLabel.ToLowerInvariant()',
    '"live-drain-xp"',
    'GrailFloatingTextLiveDrainXpEventId')) {
    if ($bloodMagicSource.IndexOf($requiredBloodMagicKey, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood Magic XP consolidation is missing $requiredBloodMagicKey."
    }
}

Write-Output "XP consolidation contract passed."
