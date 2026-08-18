[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$sourcePath = Join-Path $modRoot "src\GrailFloatingText.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw
$bloodMagicSource = Get-Content -LiteralPath (
    Join-Path $repoRoot "mods\BloodMagicExpansion\src\BloodMagicExpansion.cs") -Raw

foreach ($requiredToken in @(
    'private const string DefaultHealingEventId = "default-healed";',
    'Stat.Events.StatChangedBy(AliveStatType.Health)',
    'private void OnHeroHealthChanged(Stat.StatChange change)',
    '!IsGameLoadedReadyForNotifications()',
    'private void PatchHealingOverTimeSources()',
    'typeof(VHeroController)',
    'typeof(PassiveStatOverTime)',
    'private sealed class VHeroControllerUpdateStatsPatch',
    'private sealed class PassiveStatOverTimePatch',
    'BeginHealingOverTimeScope();',
    'EndHealingOverTimeScope();',
    'private void AdvanceHealingQueue()',
    'AdvanceHealingQueue();',
    'public const int ApiVersion = 12;',
    'TrySetBuiltInEventPresentationClaim(',
    '"BuiltInEventPresentationClaims"',
    'blood-magic-healed',
    '"Green"',
    '"healing"',
    '.Replace("{health}", formattedAmount)',
    'private const int ConfigSchemaVersion = 27;')) {
    if ($source.IndexOf($requiredToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Healing notification contract is missing $requiredToken."
    }
}

foreach ($binding in @(
    'BindOrdered("Default Game Events", "NotifyHealing", true,',
    'BindOrdered("Default Game Events", "NotifyHealingOverTime", false,',
    'BindOrdered("Default Game Events", "ConsolidateHealing", true,',
    'BindOrdered("Default Game Events", "HealingMinimumAmount", 1.0f,',
    'BindOrdered("Default Game Events", "HealingTextFormat", "Healed {health}",',
    'BindOrdered("Default Game Events", "HealingDurationBucket", "Short",')) {
    if ($source.IndexOf($binding, [StringComparison]::Ordinal) -lt 0) {
        throw "Healing config contract is missing $binding."
    }
}

$handlerStart = $source.IndexOf(
    "private void OnHeroHealthChanged(Stat.StatChange change)",
    [StringComparison]::Ordinal)
$handlerEnd = $source.IndexOf(
    "private bool ShowHealingNotification(HealingNotificationBatch batch)",
    $handlerStart,
    [StringComparison]::Ordinal)
if ($handlerStart -lt 0 -or $handlerEnd -le $handlerStart) {
    throw "Could not locate the complete hero-healing handler."
}

$handler = $source.Substring($handlerStart, $handlerEnd - $handlerStart)
foreach ($requiredHandlerToken in @(
    'IsBuiltInEventClaimed(DefaultHealingEventId)',
    '!IsHealingOverTimeNotificationEnabled()',
    '_healingOverTimeScopeDepth > 0',
    "change.value <= 0.0f",
    "TryResolveBuiltInEventPresentationClaim(",
    "_bufferedHealingBatches",
    "FindMatchingHealingBatch(",
    "GetHealingMinimumAmount()",
    "ShowHealingNotification(buffered)")) {
    if ($handler.IndexOf($requiredHandlerToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Hero-healing handler is missing $requiredHandlerToken."
    }
}

$queueStart = $source.IndexOf(
    "private void AdvanceHealingQueue()",
    [StringComparison]::Ordinal)
$queueEnd = $source.IndexOf(
    "private string GetConfiguredHealingTextFormat()",
    $queueStart,
    [StringComparison]::Ordinal)
if ($queueStart -lt 0 -or $queueEnd -le $queueStart) {
    throw "Could not locate the complete healing queue."
}

$queue = $source.Substring($queueStart, $queueEnd - $queueStart)
if ($queue.IndexOf("StartTime", [StringComparison]::Ordinal) -ge 0) {
    throw "Queued healing consolidation must not restart the visible notification timer."
}
foreach ($presentationIdentity in @("EventId", "Style", "IconId")) {
    if ($queue.IndexOf($presentationIdentity, [StringComparison]::Ordinal) -lt 0) {
        throw "Healing consolidation no longer separates batches by $presentationIdentity."
    }
}
if ($queue.IndexOf("_pendingHealingBatches", [StringComparison]::Ordinal) -lt 0) {
    throw "Healing consolidation no longer retains queued presentation-aware batches."
}

foreach ($bloodMagicToken in @(
    'GrailFloatingTextBloodHealingEventId = "blood-magic-healed"',
    'GrailFloatingTextBloodHealingStyle = "Red"',
    'GrailFloatingTextBloodHealingIconId = "magic_blood"',
    'BeginGrailFloatingTextBloodHealingPresentationClaim()',
    'EndGrailFloatingTextBloodHealingPresentationClaim()',
    'ReleaseGrailFloatingTextBloodHealingPresentationClaim()',
    'IsBloodMagicHealing(',
    'TrySetBuiltInEventPresentationClaim')) {
    if ($bloodMagicSource.IndexOf($bloodMagicToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood Magic healing presentation contract is missing $bloodMagicToken."
    }
}

$corpseHealingStart = $bloodMagicSource.IndexOf(
    "private bool ApplyCorpseLeechHealing(",
    [StringComparison]::Ordinal)
$corpseHealingEnd = $bloodMagicSource.IndexOf(
    "private void HandleAppliedDamage(",
    $corpseHealingStart,
    [StringComparison]::Ordinal)
if ($corpseHealingStart -lt 0 -or $corpseHealingEnd -le $corpseHealingStart) {
    throw "Could not locate the corpse-ritual healing transaction."
}
$corpseHealing = $bloodMagicSource.Substring(
    $corpseHealingStart,
    $corpseHealingEnd - $corpseHealingStart)
foreach ($claimToken in @(
    "BeginGrailFloatingTextBloodHealingPresentationClaim()",
    "try",
    "finally",
    "EndGrailFloatingTextBloodHealingPresentationClaim()")) {
    if ($corpseHealing.IndexOf($claimToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Corpse-ritual healing does not safely scope $claimToken."
    }
}

$builtInStart = $source.IndexOf(
    "private static readonly string[] BuiltInIconIds",
    [StringComparison]::Ordinal)
$builtInEnd = $source.IndexOf(
    "private static readonly string[] GloriousUiIncompatibleAssemblyNames",
    $builtInStart,
    [StringComparison]::Ordinal)
if ($builtInStart -lt 0 -or $builtInEnd -le $builtInStart) {
    throw "Could not locate the built-in icon ID list."
}
if ($source.Substring($builtInStart, $builtInEnd - $builtInStart).IndexOf(
    '"healing"',
    [StringComparison]::Ordinal) -lt 0) {
    throw "The built-in healing icon ID is missing."
}

$iconPath = Join-Path $modRoot "icons\healing.png"
if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
    throw "The built-in healing icon PNG is missing."
}

Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::FromFile($iconPath)
try {
    if ($bitmap.Width -ne 128 -or $bitmap.Height -ne 128) {
        throw "The healing icon must be 128 by 128 pixels."
    }

    $corners = @(
        $bitmap.GetPixel(0, 0),
        $bitmap.GetPixel(127, 0),
        $bitmap.GetPixel(0, 127),
        $bitmap.GetPixel(127, 127)
    )
    if (@($corners | Where-Object { $_.A -gt 2 }).Count -ne 0) {
        throw "The healing icon must retain transparent outer corners."
    }
    if ($bitmap.GetPixel(64, 64).A -lt 240) {
        throw "The healing icon's central heart is unexpectedly transparent."
    }
}
finally {
    $bitmap.Dispose()
}

Write-Output "Healing notification contract passed."
