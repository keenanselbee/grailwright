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

foreach ($requiredCancellationToken in @(
    "public static bool TryCancelXpGainClaim(",
    "internal bool TryCancelXpGainClaim(",
    'string.Equals(feature, "XpClaimCancellation"',
    "_xpDisplayClaims.RemoveAt(i)")) {
    if ($gftSource.IndexOf($requiredCancellationToken, [StringComparison]::Ordinal) -lt 0) {
        throw "The API v10 XP claim cancellation contract is missing $requiredCancellationToken."
    }
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
    '"live-drain-xp"',
    'GrailFloatingTextLiveDrainXpEventId',
    '" XP | +" + essenceAward + " Blood Essence"',
    '"+{xp} XP | +" + essenceAward + " Blood Essence"',
    '"corpse_" + qualityLabel.ToLowerInvariant(),',
    '"magic",' + "`r`n" + '                true)',
    'if (consolidate && _grailFloatingTextTryClaimConsolidatedXpGainMethod != null)',
    '_grailFloatingTextTryCancelXpGainClaimMethod == null',
    'TryCancelGrailFloatingTextXpClaim(',
    'RollbackBloodEssenceAward(essenceReceipt)')) {
    if ($bloodMagicSource.IndexOf($requiredBloodMagicKey, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood Magic XP consolidation is missing $requiredBloodMagicKey."
    }
}

if ($bloodMagicSource.IndexOf('XP (" + qualityLabel', [StringComparison]::Ordinal) -ge 0) {
    throw "Blood Magic corpse reward text still includes its quality label."
}

$corpsePaymentStart = $bloodMagicSource.IndexOf(
    "private void PayCorpseLeech(",
    [StringComparison]::Ordinal)
$corpsePaymentEnd = $bloodMagicSource.IndexOf(
    "private void ReportCorpseDrained(",
    $corpsePaymentStart,
    [StringComparison]::Ordinal)
if ($corpsePaymentStart -lt 0 -or $corpsePaymentEnd -le $corpsePaymentStart) {
    throw "Could not locate the Blood Magic corpse-payment transaction."
}

$corpsePayment = $bloodMagicSource.Substring(
    $corpsePaymentStart,
    $corpsePaymentEnd - $corpsePaymentStart)
$essenceCommit = $corpsePayment.IndexOf(
    "TryAwardBloodEssence(corpseQuality, out essenceReceipt)",
    [StringComparison]::Ordinal)
$xpClaim = $corpsePayment.IndexOf(
    "TryClaimGrailFloatingTextCorpseXp(pendingRawXp, state)",
    [StringComparison]::Ordinal)
$xpMutation = $corpsePayment.IndexOf(
    "AwardRawCharacterXp(pendingRawXp)",
    [StringComparison]::Ordinal)
if ($essenceCommit -lt 0 -or $xpClaim -le $essenceCommit -or $xpMutation -le $xpClaim) {
    throw "Blood Magic must save Essence before reserving the combined line and then award XP."
}

$xpFailureStart = $corpsePayment.IndexOf(
    "if (!AwardRawCharacterXp(pendingRawXp))",
    [StringComparison]::Ordinal)
$xpFailureEnd = $corpsePayment.IndexOf(
    "state.XpAwarded = true;",
    $xpFailureStart,
    [StringComparison]::Ordinal)
if ($xpFailureStart -lt 0 -or $xpFailureEnd -le $xpFailureStart) {
    throw "Could not locate the Blood Magic XP failure recovery branch."
}

$xpFailure = $corpsePayment.Substring(
    $xpFailureStart,
    $xpFailureEnd - $xpFailureStart)
foreach ($requiredRecovery in @(
    "TryCancelGrailFloatingTextXpClaim(",
    "RollbackBloodEssenceAward(essenceReceipt)")) {
    if ($xpFailure.IndexOf($requiredRecovery, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood Magic XP failure recovery is missing $requiredRecovery."
    }
}

Write-Output "XP consolidation contract passed."
