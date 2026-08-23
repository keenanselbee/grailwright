$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (Join-Path $modRoot "src\SoulAndService.cs") -Raw
$progression = Get-Content -LiteralPath (Join-Path $modRoot "src\SoulProgressionRuntime.cs") -Raw
$salvage = Get-Content -LiteralPath (Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$summons = Get-Content -LiteralPath (Join-Path $modRoot "src\SummonRuntime.cs") -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw

foreach ($required in @(
    'public static class SoulAndServiceApi',
    'public const int ApiVersion = 7',
    'GetLastSummonCommandPulseSeconds',
    'GetSoulVigor',
    'OverrideSoulVigor',
    'SoulVigorOverrideValue',
    'new ConfigDefinition("Diagnostics", "OverrideSoulVigor")',
    'new ConfigDefinition("Diagnostics", "SoulVigorOverrideValue")',
    'GetFocusedSoulSalvageTargetState',
    'GetFocusedSoulSalvageQualityTier',
    'GetFocusedSoulBindingProgress01')) {
    if (!$plugin.Contains($required)) { throw "Missing Soul and Service API contract: $required" }
}

foreach ($required in @(
    'private const string SoulVigorKey = "soul_vigor.total"',
    'SoulVigorAtNormalMaximumPower = 1000.0f',
    'SoulVigorAtAbsoluteMaximumPower = 5000.0f',
    'AttackCommandPower = 10.0f',
    'IndividualFormationPower = 20.0f',
    'GlobalFormationPower = 30.0f',
    'BehaviorCommandPower = 50.0f',
    'RecallCommandPower = 70.0f',
    'SwarmCommandPower = 90.0f',
    'EmpowermentPower = 100.0f',
    'MaximumCommandCapacityPower = 150.0f',
    'GetProgressionSummonLimitBonus()',
    'GetNecromanticPowerFromSoulVigor(before)',
    'GetNecromanticPowerFromSoulVigor(after)',
    'plugin.OverrideSoulVigor.Value',
    'plugin.SoulVigorOverrideValue.Value',
    'GetBindingIncrement(corpseFingerprint, attempt)',
    'facts.Set(key + ".progress", progress)',
    'TryHarvestCorpse(',
    'RollbackCorpseHarvest(',
    'TryRestoreCorpseHarvest(',
    'RollSoulVigorValue(',
    'TrySpendSoulVigor(',
    'InvalidateReportedProgression();',
    'private const string MeagerHarvestsKey = "soul_vigor.harvests.meager"',
    'private const string WorthyHarvestsKey = "soul_vigor.harvests.worthy"',
    'private const string PotentHarvestsKey = "soul_vigor.harvests.potent"',
    'private const string PrimeHarvestsKey = "soul_vigor.harvests.prime"',
    '"TryRecordSoulVigorStatistics"',
    'if (apiVersion < 7)',
    'case Grailwright.Shared.CorpseQualityTier.Prime:',
    'minimum = 24;',
    'maximum = 36;',
    'nominal = 30;',
    '"Necrotic"',
    'Your servants heed your command: Attack.',
    'Your will can anchor a single servant: Hold and Follow.',
    'Your command reaches the whole host: Hold All and Follow All.',
    'Your will shapes the host: Guard, Bulwark, and Hunt.',
    'Your will can recall the scattered host.',
    'Your host surges at your command: Swarm.',
    'Your will sustains the host and can Empower a servant.',
    'Your overmastered will can sustain a still greater host.',
    'GetCorpseIconId(tier)')) {
    if (!$progression.Contains($required)) { throw "Missing necromantic progression contract: $required" }
}
foreach ($required in @(
    'private const string SummonBehaviorKey = "soul_vigor.summon_behavior"',
    'internal static SummonBehavior GetSummonBehavior()',
    'internal static bool TryCycleSummonBehavior(',
    'facts.Set(SummonBehaviorKey, (int)behavior)')) {
    if (!$progression.Contains($required)) { throw "Missing summon behavior progression contract: $required" }
}

foreach ($legacyContract in @(
    '_deedsRecordProgressionMethod',
    'TryRecordSoulProgression',
    'progression.souls_bound')) {
    if ($progression.Contains($legacyContract)) {
        throw "Legacy Deeds compatibility contract must not remain: $legacyContract"
    }
}

foreach ($required in @(
    'HeavyCastManaCostMultiplier = 2.0f',
    'RaisedSalvageMaximumRefundFraction = 0.75f',
    'CalculateQuality01(source, null)',
    'GetQualityHealthMultiplier(',
    'RollRaisedHealthFraction(',
    'TryCreateRemains(',
    'HarvestCorpse(',
    'GetFocusedTargetStateForInterop')) {
    if (!$salvage.Contains($required)) { throw "Missing quality resurrection contract: $required" }
}
if (!$progression.Contains('displayName + " reanimated"')) {
    throw "Successful raising does not use the reanimated GFT wording."
}

foreach ($required in @(
    'GetSummonDamageTakenMultiplier()',
    'GetSummonDamageMultiplier()',
    'BaseSummonAwarenessRange = 30.0f',
    'SteelAndBoneTransferFraction = 0.80f',
    'owner.ForceAddCombatTarget(',
    'float retentionRange = behavior == SummonBehavior.Bulwark',
    'awarenessRange + 5.0f',
    'SteelAndBoneAwarenessApi')) {
    if (!$summons.Contains($required)) { throw "Missing summon power or awareness contract: $required" }
}
foreach ($required in @(
    'GetUpkeepPercentPerMinute(',
    'Math.Min(8.0f, activeServants + 1.0f)',
    'Mathf.Clamp01(',
    '1.0f - (necromanticPower / 100.0f)',
    'SoulProgressionRuntime.GetNecromanticPower()',
    'SwarmFirstHitMultiplier',
    'receiverEmpowerment.Multiplier',
    'dealerEmpowerment.Multiplier')) {
    if (!$summons.Contains($required)) { throw "Missing upkeep, Swarm, or Empower power contract: $required" }
}
if ($summons -notmatch 'SoulProgressionRuntime\s*\.GetProgressionSummonLimitBonus\(\)') {
    throw 'The native summon limit does not include progression Command Capacity.'
}
foreach ($legacyThreshold in @(
    'AttackCommandSoulVigor',
    'IndividualFormationSoulVigor',
    'GlobalFormationSoulVigor',
    'SwarmCommandSoulVigor',
    'EmpowermentSoulVigor',
    'soulVigor / 100.0f')) {
    if ($progression.Contains($legacyThreshold) -or $summons.Contains($legacyThreshold)) {
        throw "Raw Soul Vigor threshold contract must not remain: $legacyThreshold"
    }
}
if (!$plugin.Contains('new AcceptableValueRange<float>(0.0f, 5000.0f)')) {
    throw 'Soul Vigor override is not capped at 5,000.'
}

function Get-NecromanticPower([double]$soulVigor) {
    if ($soulVigor -le 1000.0) {
        $x = [Math]::Max(0.0, $soulVigor) / 1000.0
        return (10.0 * $x * $x * $x) - (70.0 * $x * $x) + (160.0 * $x)
    }
    $y = [Math]::Min(1.0, ($soulVigor - 1000.0) / 4000.0)
    return 100.0 + (100.0 * $y)
}

foreach ($threshold in @(
    @{ Before = 64; At = 65; Power = 10 },
    @{ Before = 132; At = 133; Power = 20 },
    @{ Before = 205; At = 206; Power = 30 },
    @{ Before = 368; At = 369; Power = 50 },
    @{ Before = 566; At = 567; Power = 70 },
    @{ Before = 825; At = 826; Power = 90 },
    @{ Before = 999; At = 1000; Power = 100 },
    @{ Before = 2999; At = 3000; Power = 150 })) {
    if ((Get-NecromanticPower $threshold.Before) -ge $threshold.Power -or
        (Get-NecromanticPower $threshold.At) -lt $threshold.Power) {
        throw "Soul Vigor does not cross Power $($threshold.Power) between $($threshold.Before) and $($threshold.At)."
    }
}
if ($progression -notmatch '(?s)GetProgressionSummonLimitBonus\(\).*?power >= MaximumCommandCapacityPower.*?return 3;.*?power >= EmpowermentPower.*?return 2;.*?power >= BehaviorCommandPower \? 1 : 0;') {
    throw 'Command capacity does not grant +1/+2/+3 at Power 50/100/150.'
}
if ($summons -notmatch '(?s)if \(!plugin\.IsEnabled\)\s*\{.*?ClearAllServantPowerStates\(\);.*?RestoreAllCollisionPairs\(\);.*?RemoveAllAwarenessTargets\(\);.*?ExplicitCommandTargets\.Clear\(\);.*?return;\s*\}') {
    throw "Disabling Soul and Service does not remove its injected awareness targets."
}
if ($progression -notmatch '(?s)SoulAndServicePlugin plugin = SoulAndServicePlugin\.Instance;\s*if \(plugin == null\s*\|\| !plugin\.IsEnabled') {
    throw "Disabled Soul and Service still reports progression to Deeds."
}

if (!$manifest.Contains('../../tools/shared/CorpseQualityBuckets.cs')) {
    throw "Soul and Service does not compile the shared corpse-quality classifier."
}

foreach ($required in @(
    'public const int ApiVersion = 7',
    'public static bool IsNecroticDamage(object damage)',
    'return SoulSalvageRuntime.IsNecroticDamageForInterop(damage);')) {
    if (!$plugin.Contains($required)) {
        throw "Soul and Service necrotic API contract is missing: $required"
    }
}

foreach ($required in @(
    'private sealed class NecroticDamageMarker',
    'ConditionalWeakTable<Damage, NecroticDamageMarker>',
    'NecroticDamageMarkers =',
    'MarkNecroticDamage(Damage damage)',
    'NecroticDamageMarkers.TryGetValue(typedDamage, out marker)')) {
    if (!$salvage.Contains($required)) {
        throw "Soul Rend is missing exact weak Damage-instance provenance: $required"
    }
}
if ($salvage -notmatch '(?s)NecroticDamageMarkers\.(?:Add\(damage, new NecroticDamageMarker\(\)|GetValue\(\s*damage,\s*ignored\s*=>\s*new NecroticDamageMarker\(\)\s*\))') {
    throw 'Necrotic Damage markers are not associated with the exact Damage instance.'
}

$rendBlock = [regex]::Match(
    $salvage,
    '(?s)private static void ApplySoulRend\(.+?(?=\r?\n\s*private static)')
if (!$rendBlock.Success) {
    throw 'Could not locate the Soul Rend damage path.'
}
if ($rendBlock.Value -notmatch '(?s)MarkNecroticDamage\(damage\);\s*target\.HealthElement\.TakeDamage\(damage\);') {
    throw 'Soul Rend must mark its exact Damage instance immediately before TakeDamage.'
}

$claimBlock = [regex]::Match(
    $salvage,
    '(?s)private static void TryClaimLivingTarget\(.+?(?=\r?\n\s*private static)')
if (!$claimBlock.Success) {
    throw 'Could not locate the Soul Claim damage path.'
}
if ($claimBlock.Value.Contains('MarkNecroticDamage(')) {
    throw 'Soul Claim execution damage must not be marked Necrotic.'
}
if ($claimBlock.Value -notmatch 'target\.HealthElement\.TakeDamage\(claimDamage\);') {
    throw 'Soul Claim native execution damage path is missing.'
}

$interopBlock = [regex]::Match(
    $salvage,
    '(?s)internal static bool IsNecroticDamageForInterop\(object damage\).+?(?=\r?\n\s*(?:internal|private|public) static)')
if ((-not $interopBlock.Success) -or $interopBlock.Value.Contains('IsSoulSalvageItem(typedDamage.Item)')) {
    throw 'Necrotic interop must use the exact Damage marker rather than Soul Salvage item identity.'
}

Write-Output "Soul and Service necromantic progression contracts passed."
