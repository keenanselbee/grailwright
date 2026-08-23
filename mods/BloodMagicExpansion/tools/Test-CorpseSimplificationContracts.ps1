$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\BloodMagicExpansion.cs")
$readme = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "README.txt")
$nexusDescription = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt")

foreach ($removed in @(
    'private ConfigEntry<bool> _simplifyDrainedCorpses;',
    'BindOrdered("Main Loop", "SimplifyDrainedCorpses"',
    'TrySimplifyDrainedCorpse(state);')) {
    if ($source.Contains($removed)) {
        throw "Blood rituals must leave the drained corpse intact: $removed"
    }
}

foreach ($required in @(
    'state.Exhausted = true;',
    'state.ExsanguinationSeverity = RollExsanguinationSeverity();',
    'BloodRitualSeverityKeyPrefix = "ritual.exsanguination."',
    'PersistCorpseExsanguinationSeverity(state);',
    'TryRestoreCorpseExsanguinationSeverity(state, corpse, out restoredSeverity)',
    'StableHash(modelId).ToString("x8", CultureInfo.InvariantCulture)',
    'GetCorpseExsanguinationSeverityForInterop',
    'GetCorpseExsanguinationSeverity(object corpse)',
    'TryResolveOwnedBloodServant',
    'ServantTargetToleranceRadius = 0.15f',
    'ServantTargetGraceSeconds = 0.18f',
    'Physics.SphereCastNonAlloc(',
    'TryGetFocusedCorpseInteropSnapshot(',
    'TryMaterializeOwnedBloodServantCorpseForAbhartach',
    'IsLiveServantRitualBlocked(state)',
    '"recognized; ritual blocked while hero is in combat"',
    '"recognized; source corpse already drained"',
    '"living target is not an owned Soul and Service servant"',
    '"recognized; owned servant blood available; healing only"',
    'state.LiveServantHasSourceCorpse',
    'TryExsanguinateOwnedBloodServant',
    'SetOwnedBloodServantRitualState',
    'ReportCorpseDrained(corpseQuality);',
    'CorpseLeechSoundRangeVolume", 1.0f',
    'CorpseLeechMaximumRangeDistance = 30.0f',
    'CorpseLeechMinimumRangeVolume = 0.10f',
    'GetCorpseLeechRangeVolumeMultiplier(')) {
    if (!$source.Contains($required)) {
        throw "Missing intact-corpse or exsanguination contract: $required"
    }
}

$constructedCorpseBlock = [regex]::Match(
    $source,
    '(?s)internal void HandleCorpseConstructed\(object corpse, object\[\] args\).+?(?=\r?\n\s*internal void HandleCorpseRestored)')
if (!$constructedCorpseBlock.Success -or
    !$constructedCorpseBlock.Value.Contains('GetOptionalPropertyValue(corpse, "ParentModel")') -or
    !$constructedCorpseBlock.Value.Contains('RegisterCorpseAliases(parentModel, state);')) {
    throw 'New corpses are not aliased to their owning Location for reanimated-servant targeting.'
}

$focusedStateBlock = [regex]::Match(
    $source,
    '(?s)internal int GetFocusedCorpseStateForInterop\(bool requireRelevantSpell\).+?(?=\r?\n\s*internal float GetFocusedCorpseQuality01ForInterop)')
if (!$focusedStateBlock.Success -or
    !$focusedStateBlock.Value.Contains('TryGetFocusedCorpseInteropSnapshot(') -or
    !$focusedStateBlock.Value.Contains('state.LiveServantTarget != null && abhartachHeld') -or
    !$focusedStateBlock.Value.Contains('abhartachEquipped && IsCorpseBloodMagicEligibleForInterop(state)') -or
    !$focusedStateBlock.Value.Contains('!IsLiveServantRitualBlocked(state)')) {
    throw 'Living-servant reticles do not distinguish light Abhartach availability from held or combat-blocked rituals.'
}

$tolerantTargetBlock = [regex]::Match(
    $source,
    '(?s)private bool TryResolveTolerantServantTarget\(.+?(?=\r?\n\s*private )')
if (!$tolerantTargetBlock.Success -or
    !$tolerantTargetBlock.Value.Contains('Physics.SphereCastNonAlloc(') -or
    !$tolerantTargetBlock.Value.Contains('nearestBlockingDistance') -or
    !$tolerantTargetBlock.Value.Contains('TryResolveSoulAndServiceServant(')) {
    throw 'Living-servant targeting does not use the bounded, obstruction-aware tolerance probe.'
}

$interopSnapshotBlock = [regex]::Match(
    $source,
    '(?s)private bool TryGetFocusedCorpseInteropSnapshot\(.+?(?=\r?\n\s*internal float GetBloodEssenceForInterop)')
if (!$interopSnapshotBlock.Success -or
    !$interopSnapshotBlock.Value.Contains('Time.frameCount') -or
    !$interopSnapshotBlock.Value.Contains('_focusedCorpseInteropSnapshotResolved')) {
    throw 'Blood Magic reticle state, quality, and tier do not share one per-frame target snapshot.'
}

$abhartachSacrificeBlock = [regex]::Match(
    $source,
    '(?s)private void TrySacrificeFocusedServantForAbhartach\(\).+?(?=\r?\n\s*private )')
$performCastBlock = [regex]::Match(
    $source,
    '(?s)internal void RegisterPerformCast\(object magicFsm, bool lightCast\).+?(?=\r?\n\s*internal void RegisterCastEnding)')
if (!$abhartachSacrificeBlock.Success -or
    !$abhartachSacrificeBlock.Value.Contains('TryGetLookedAtCorpseState(out state, true)') -or
    !$abhartachSacrificeBlock.Value.Contains('state.LiveServantTarget') -or
    !$abhartachSacrificeBlock.Value.Contains('state.Disabled = true;') -or
    !$performCastBlock.Success -or
    $performCastBlock.Value -notmatch '(?s)if \(isAbhartach\).*?if \(lightCast\).*?TrySacrificeFocusedServantForAbhartach\(\);') {
    throw "Light Abhartach does not materialize only its explicitly focused owned servant before the native cast effect."
}

$restoredCorpseBlock = [regex]::Match(
    $source,
    '(?s)internal void HandleCorpseRestored\(object corpse\).+?(?=\r?\n\s*private )')
if (!$restoredCorpseBlock.Success -or
    !$restoredCorpseBlock.Value.Contains('state.Exhausted = true;') -or
    !$restoredCorpseBlock.Value.Contains('state.ExsanguinationSeverity = restoredSeverity;')) {
    throw 'A saved drained corpse does not restore its spent state and exact exsanguination severity.'
}

$paymentBlock = [regex]::Match(
    $source,
    '(?s)private void PayCorpseLeech\(CorpseState state, int activeHandCount\).+?(?=\r?\n\s*private )')
$servantPreflightIndex = $paymentBlock.Value.IndexOf(
    'TryResolveSoulAndServiceServant(',
    [StringComparison]::Ordinal)
$healingIndex = $paymentBlock.Value.IndexOf(
    'ApplyCorpseLeechHealing(',
    [StringComparison]::Ordinal)
if (!$paymentBlock.Success -or
    $servantPreflightIndex -lt 0 -or
    $healingIndex -lt 0 -or
    $servantPreflightIndex -gt $healingIndex) {
    throw 'Owned servants must be revalidated before corpse-leech healing or progression is committed.'
}

$servantResolver = [regex]::Match(
    $source,
    '(?s)private bool TryResolveSoulAndServiceServant\(.+?(?=\r?\n\s*private )')
if (!$servantResolver.Success -or
    $servantResolver.Value.Contains('IsCorpseStateUsable(state)')) {
    throw 'Raised-servant recognition must retain blocked and spent sources for desaturated reticle feedback.'
}

foreach ($retiredDiagnostic in @(
    '"LogStartup"',
    '"LogAwards"',
    '"LogRejectedCorpses"',
    '"LogUnresolvedRaycastHits"',
    '"LogHealingResolution"',
    '"LogPatchWarnings"',
    '"LogCorpseQuality"',
    '"LogBloodSpellInnerLight"',
    '"CorpseQualityLogIntervalSeconds"')) {
    if ($source.Contains($retiredDiagnostic)) {
        throw "Retired granular Blood Magic diagnostic remains bound: $retiredDiagnostic"
    }
}
foreach ($diagnosticContract in @(
    'BindOrdered("Diagnostics", "Diagnostics", false',
    'BindOrdered("Diagnostics", "ShowGrailFloatingTextDiagnostics", true',
    'BindOrdered("Diagnostics", "OverrideBloodEssence", false',
    'new ConfigDefinition("Diagnostics", "OverrideBloodEssence")')) {
    if (!$source.Contains($diagnosticContract)) {
        throw "Missing consolidated Blood Magic diagnostic contract: $diagnosticContract"
    }
}

$severityBlock = [regex]::Match(
    $source,
    '(?s)private float RollExsanguinationSeverity\(\).+?(?=\r?\n\s*private )')
if (!$severityBlock.Success -or
    !$severityBlock.Value.Contains('GetBloodPower() / 200.0f') -or
    !$severityBlock.Value.Contains('UnityEngine.Random.Range(-0.02f, 0.02f)') -or
    !$severityBlock.Value.Contains('0.20f') -or
    !$severityBlock.Value.Contains('0.30f')) {
    throw 'Exsanguination severity must combine Blood Power with a small random 20-30% Health loss.'
}

$pruningBlock = [regex]::Match(
    $source,
    '(?s)private bool ShouldPruneCorpseState\(.+?(?=\r?\n\s*private )')
if (!$pruningBlock.Success -or $pruningBlock.Value.Contains('state.Exhausted ||')) {
    throw 'An intact drained corpse must retain its one-time exsanguination severity while the source body exists.'
}

foreach ($document in @($readme, $nexusDescription)) {
    foreach ($required in @(
        'drained corpse',
        'CorpseLeechSoundRangeVolume')) {
        if ($document.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Player-facing documentation is missing the intact-corpse/audio contract: $required"
        }
    }
}

Write-Host "Blood Magic Expansion intact-corpse and exsanguination contracts passed."
