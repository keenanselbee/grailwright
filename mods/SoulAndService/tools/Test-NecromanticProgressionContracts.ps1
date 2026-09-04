$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (Join-Path $modRoot "src\SoulAndService.cs") -Raw
$progression = Get-Content -LiteralPath (Join-Path $modRoot "src\SoulProgressionRuntime.cs") -Raw
$salvage = Get-Content -LiteralPath (Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$summons = Get-Content -LiteralPath (Join-Path $modRoot "src\SummonRuntime.cs") -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw

foreach ($required in @(
    'public static class SoulAndServiceApi',
    'public const int ApiVersion = 10',
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
    'BulwarkBehaviorPower = 60.0f',
    'RecallCommandPower = 70.0f',
    'SwarmCommandPower = 90.0f',
    'EmpowermentPower = 100.0f',
    'MaximumSummonCapacityPower = 150.0f',
    'RaiseAllPower = 200.0f',
    'GuardDamageMultiplier = 1.05f',
    'GuardDamageTakenMultiplier = 0.95f',
    'BulwarkDamageTakenMultiplier = 0.85f',
    'HuntDamageMultiplier = 1.10f',
    'GetProgressionSummonLimitBonus()',
    'GetNecromanticPowerFromSoulVigor(before)',
    'GetNecromanticPowerFromSoulVigor(after)',
    'plugin.OverrideSoulVigor.Value',
    'plugin.SoulVigorOverrideValue.Value',
    'internal static void ShowSoulVigorWanesAfterSpend(int before, int after)',
    'IsSoulVigorOverrideActive()',
    'ShowSoulVigorThresholdMessage(',
    'GetBindingIncrement(bindingFingerprint, attempt)',
    'facts.Set(key + ".progress", progress)',
    'TryHarvestCorpse(',
    'RollbackCorpseHarvest(',
    'TryRestoreCorpseHarvest(',
    'GetOrRollCorpseSoulVigorValue(',
    'CorpseSoulVigorKey(',
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
    'Necromantic Power rises: Attack commands are available.',
    'Necromantic Power wanes: Attack commands are unavailable.',
    'Necromantic Power rises: individual Hold and Follow commands are available.',
    'Necromantic Power wanes: individual Hold and Follow commands are unavailable.',
    'Necromantic Power rises: Hold All and Follow All are available.',
    'Necromantic Power wanes: Hold All and Follow All are unavailable.',
    'Necromantic Power rises: Guard and Hunt behavior control is available; Summon Capacity bonus is +1.',
    'Necromantic Power wanes: Guard and Hunt behavior control is unavailable; Summon Capacity bonus is lost.',
    'Necromantic Power rises: Bulwark behavior is available.',
    'Necromantic Power wanes: Bulwark is unavailable; Guard takes its place.',
    'Necromantic Power rises: Recall Host is available.',
    'Necromantic Power wanes: Recall Host is unavailable.',
    'Necromantic Power rises: Swarm commands are available.',
    'Necromantic Power wanes: Swarm is unavailable; Attack takes its place.',
    'Necromantic Power rises: Empower is available; servant upkeep ends; Summon Capacity bonus is +2.',
    'Necromantic Power wanes: Empower is unavailable; servant upkeep resumes; Summon Capacity bonus is +1.',
    'Necromantic Power rises: Summon Capacity bonus is +3.',
    'Necromantic Power wanes: Summon Capacity bonus falls to +2.',
    'GetCorpseIconId(tier)')) {
    if (!$progression.Contains($required)) { throw "Missing necromantic progression contract: $required" }
}
foreach ($required in @(
    'private const int HighSoulPoolPortions = 6',
    'internal static int GetOrInitializeHighSoulVigorPool(',
    'internal static bool TryGetExistingHighSoulVigorPool(',
    'internal static bool TryDrainHighSoulVigorPool(',
    'internal static bool TryConsumeHighSoulServicePortion(',
    'internal static int GetHighSoulServiceCycle(',
    'internal static bool ApplyHighSoulBindingAttempt(',
    'internal static void CommitHighSoulBinding(',
    'internal static float GetHighSoulBindingProgress01(',
    'SaturatingMultiply(',
    'facts.Set(key + ".initialized", 1)',
    'Math.Max(0, remaining - extractionValue)',
    'TryRestoreHighSoulDrain(receipt)')) {
    if (!$progression.Contains($required)) {
        throw "Missing high-soul persistence contract: $required"
    }
}
if (($progression -notmatch '(?s)GetOrInitializeHighSoulVigorPool\(.*?storedExtraction <= 0.*?return 0;.*?Mathf\.Clamp\(storedRemaining, 0, maximum\)') -or
    ($progression -notmatch '(?s)TryDrainHighSoulVigorPool\(.*?BeforeSoulVigor = beforeSoulVigor.*?BeforeRemaining = remaining.*?facts\.Set\(\s*SoulVigorKey.*?facts\.Set\(remainingKey.*?catch \(Exception exception\).*?TryRestoreHighSoulDrain\(receipt\)') -or
    ($progression -notmatch '(?s)TryGetExistingHighSoulVigorPool\(.*?\.initialized", int\.MinValue\) != 1.*?\.extraction", int\.MinValue\).*?!= expectedExtractionValue.*?storedRemaining % expectedExtractionValue != 0') -or
    ($progression -notmatch '(?s)TryConsumeHighSoulServicePortion\(.*?TryGetExistingHighSoulVigorPool\(.*?Award = 0.*?remaining - extractionValue.*?TryRestoreHighSoulDrain\(receipt\)') -or
    ($progression -notmatch '(?s)ApplyHighSoulBindingAttempt\(.*?service-cycle.*?baseResistance.*?0\.90f.*?1\.10f.*?ApplyBindingAttempt\(')) {
    throw 'High-soul pools are not deterministic, bounded, transactional, and cycle-bound.'
}
if (($progression -notmatch '(?s)private static void ShowSoulVigorThresholdMessages\(.*?if \(IsSoulVigorOverrideActive\(\)\).*?return;') -or
    ($progression -notmatch '(?s)private static void ShowSoulVigorThresholdMessage\(.*?"necro".*?"High".*?string\.Empty.*?rises \? "Reward" : "Status".*?"Medium"')) {
    throw 'Soul Vigor threshold feedback must suppress diagnostic overrides and use the approved Necrotic GFT presentation.'
}
$trySpendBlock = [regex]::Match(
    $progression,
    '(?s)internal static bool TrySpendSoulVigor\(.+?(?=\r?\n\s*(?:internal|private) static)')
if ((-not $trySpendBlock.Success) -or
    $trySpendBlock.Value.Contains('ShowSoulVigor') -or
    ($salvage -notmatch '(?s)OrdinarySummonInvestments\[summonId\] =.*?ShowSoulVigorWanesAfterSpend\(before, after\);') -or
    ($salvage -notmatch '(?s)CommitHighSoulBinding\(.*?CommitSuccessfulBinding\(\s*record\.CorpseFingerprint\);.*?record\.ServiceInitialized = true;.*?SoulProgressionRuntime\.ShowSoulVigorWanesAfterSpend\(\s*vigorBefore,\s*vigorAfter\);') -or
    ($salvage -notmatch '(?s)TryServeHeavyTarget\(.*?TrySpendSoulVigor\(.*?TryEmpowerSummon\(.*?AddEmpowermentSoulVigorInvestment\(.*?ShowSoulVigorWanesAfterSpend\(\s*beforeVigor,\s*afterVigor\);')) {
    throw 'Soul Vigor waning feedback must wait for a durable summon or reanimation investment.'
}
foreach ($required in @(
    'private const string SummonBehaviorKey = "soul_vigor.summon_behavior"',
    'internal static SummonBehavior GetSummonBehavior()',
    'internal static bool TryCycleSummonBehavior(',
    'facts.Set(SummonBehaviorKey, (int)behavior)')) {
    if (!$progression.Contains($required)) { throw "Missing summon behavior progression contract: $required" }
}
if (($progression -notmatch '(?s)GetSummonBehavior\(\).*?behavior == SummonBehavior\.Bulwark.*?power < BulwarkBehaviorPower.*?SummonBehavior\.Guard') -or
    ($progression -notmatch '(?s)TryCycleSummonBehavior\(.*?current == SummonBehavior\.Guard\s*\? SummonBehavior\.Hunt.*?current == SummonBehavior\.Hunt\s*&& power >= BulwarkBehaviorPower\s*\? SummonBehavior\.Bulwark\s*:\s*SummonBehavior\.Guard')) {
    throw 'Behavior cycling does not unlock Guard/Hunt at Power 50 and add Bulwark at Power 60.'
}
if (($progression -notmatch '(?s)GetSummonDamageMultiplier\(\).*?power < BehaviorCommandPower.*?behavior == SummonBehavior\.Hunt.*?HuntDamageMultiplier.*?behavior == SummonBehavior\.Guard.*?GuardDamageMultiplier') -or
    ($progression -notmatch '(?s)GetSummonDamageTakenMultiplier\(\).*?power < BehaviorCommandPower.*?behavior == SummonBehavior\.Bulwark.*?BulwarkDamageTakenMultiplier.*?behavior == SummonBehavior\.Guard.*?GuardDamageTakenMultiplier')) {
    throw 'Behavior damage and mitigation bonuses do not apply only after behavior control unlocks.'
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
    'ServantFinalRewardFraction = 0.75f',
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
    'UpkeepThreeServantPowerZeroPercentPerMinute',
    'UpkeepThreeServantPowerFiftyPercentPerMinute',
    'UpkeepThreeServantPowerNinetyPercentPerMinute',
    'Mathf.Clamp(necromanticPower, 0.0f, 100.0f)',
    'Math.Min(8.0f, activeServants + 1.0f) / 4.0f',
    'SoulProgressionRuntime.GetNecromanticPower()',
    'SwarmFirstHitMultiplier',
    'receiverEmpowerment.CombatMultiplier',
    'dealerEmpowerment.CombatMultiplier')) {
    if (!$summons.Contains($required)) { throw "Missing upkeep, Swarm, or Empower power contract: $required" }
}
if ($summons -notmatch 'SoulProgressionRuntime\s*\.GetProgressionSummonLimitBonus\(\)') {
    throw 'The native summon limit does not include progression Summon Capacity.'
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
if ($plugin -notmatch '(?s)SoulVigorOverrideValue = BindOrdered\(.*?5000\.0f,.*?new AcceptableValueRange<float>\(0\.0f, 10000\.0f\)') {
    throw 'Soul Vigor override does not default to 5,000 or accept values through 10,000.'
}

function Get-NecromanticPower([double]$soulVigor) {
    if ($soulVigor -le 1000.0) {
        $x = [Math]::Max(0.0, $soulVigor) / 1000.0
        return (10.0 * $x * $x * $x) - (70.0 * $x * $x) + (160.0 * $x)
    }
    $y = [Math]::Min(1.0, ($soulVigor - 1000.0) / 4000.0)
    return 100.0 + (100.0 * $y)
}

function Get-UpkeepPercentPerMinute([int]$activeServants, [double]$power) {
    if ($activeServants -le 0) {
        return 0.0
    }
    $safePower = [Math]::Max(0.0, [Math]::Min(100.0, $power))
    if ($safePower -lt 50.0) {
        $threeServantRate = (100.0 / 3.0) +
            ((5.0 - (100.0 / 3.0)) * ($safePower / 50.0))
    } elseif ($safePower -lt 90.0) {
        $threeServantRate = 5.0 +
            ((2.0 - 5.0) * (($safePower - 50.0) / 40.0))
    } else {
        $threeServantRate = 2.0 +
            ((0.0 - 2.0) * (($safePower - 90.0) / 10.0))
    }
    $hostScale = [Math]::Min(8.0, $activeServants + 1.0) / 4.0
    return $threeServantRate * $hostScale
}

foreach ($case in @(
    @{ Servants = 1; Power = 0; Expected = 100.0 / 6.0 },
    @{ Servants = 3; Power = 0; Expected = 100.0 / 3.0 },
    @{ Servants = 7; Power = 0; Expected = 200.0 / 3.0 },
    @{ Servants = 1; Power = 50; Expected = 2.5 },
    @{ Servants = 3; Power = 50; Expected = 5.0 },
    @{ Servants = 7; Power = 50; Expected = 10.0 },
    @{ Servants = 1; Power = 90; Expected = 1.0 },
    @{ Servants = 3; Power = 90; Expected = 2.0 },
    @{ Servants = 7; Power = 90; Expected = 4.0 },
    @{ Servants = 3; Power = 100; Expected = 0.0 })) {
    $actual = Get-UpkeepPercentPerMinute $case.Servants $case.Power
    if ([Math]::Abs($actual - $case.Expected) -gt 0.001) {
        throw "Upkeep mismatch for $($case.Servants) servants at Power $($case.Power): $actual."
    }
}

foreach ($threshold in @(
    @{ Before = 64; At = 65; Power = 10 },
    @{ Before = 132; At = 133; Power = 20 },
    @{ Before = 205; At = 206; Power = 30 },
    @{ Before = 368; At = 369; Power = 50 },
    @{ Before = 462; At = 463; Power = 60 },
    @{ Before = 566; At = 567; Power = 70 },
    @{ Before = 825; At = 826; Power = 90 },
    @{ Before = 999; At = 1000; Power = 100 },
    @{ Before = 2999; At = 3000; Power = 150 })) {
    if ((Get-NecromanticPower $threshold.Before) -ge $threshold.Power -or
        (Get-NecromanticPower $threshold.At) -lt $threshold.Power) {
        throw "Soul Vigor does not cross Power $($threshold.Power) between $($threshold.Before) and $($threshold.At)."
    }
}
if ($progression -notmatch '(?s)GetProgressionSummonLimitBonus\(\).*?power >= MaximumSummonCapacityPower.*?return 3;.*?power >= EmpowermentPower.*?return 2;.*?power >= BehaviorCommandPower \? 1 : 0;') {
    throw 'Summon Capacity does not grant +1/+2/+3 at Power 50/100/150.'
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
    'public const int ApiVersion = 10',
    'RaiseAll = 5',
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
