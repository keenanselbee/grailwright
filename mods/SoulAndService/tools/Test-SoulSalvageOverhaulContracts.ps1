$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulAndService.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$summonRuntimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SummonRuntime.cs") -Raw
$progressionSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulProgressionRuntime.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw
$matrix = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\TEST-MATRIX.md") -Raw
foreach ($required in @(
    'ConfigSchemaVersion = 10',
    '"EnableLivingTargetSoulSalvage"',
    '"PersistentServants"')) {
    if (!$pluginSource.Contains($required)) {
        throw "Soul Salvage plugin configuration is missing: $required"
    }
}

foreach ($forbidden in @(
    'internal ConfigEntry<float> ReanimationDurationSeconds',
    'internal ConfigEntry<float> ReanimationHealthPercent',
    'ReanimationMinimumLifetimeSeconds',
    'ReanimationHealthDecayPercentPerSecond',
    'ReanimationFlatHealthDecayPerSecond',
    'PermanentReanimations',
    'PreventDismissOnRest',
    'SoulSalvageReturnMode',
    'SoulSalvageReturn',
    '"LightCastReturn"',
    '"LightCastEssencePercent"')) {
    if ($pluginSource.Contains($forbidden)) {
        throw "Soul Salvage plugin retains replaced configuration: $forbidden"
    }
}
foreach ($required in @(
    'can never restore more than 75% of their binding cost',
    'Keep ordinary and reanimated servants when the hero rests',
    'section == "Soul Salvage" ? "Soul Rend" : section',
    '"Play Soul Rend Audio"',
    '"Enable Soul Rend"',
    '"Enable Living-Target Soul Rend"',
    'internal ConfigEntry<float> SoulSalvageManaReturnPercent',
    '"LightCastManaReturnPercent"',
    '"Mana Return Percent"')) {
    if (!$pluginSource.Contains($required)) {
        throw "Soul Salvage configuration UX is missing: $required"
    }
}

foreach ($required in @(
    'HeavyCastManaCostMultiplier = 2.0f',
    'StatTweak.Multi(',
    'stats.HeavyCastManaCost',
    'SoulRendDisplayName = "Soul Rend"',
    'RequireMethod(typeof(Item), "get_DisplayName")',
    'AfterGetItemDisplayName',
    'get_MagicDescription',
    'LightCastInfos.Contains(__instance)',
    'HeavyCastInfos.Contains(__instance)',
    'SoulProgressionRuntime.ApplyBindingAttempt(',
    'SoulProgressionRuntime.CommitSuccessfulBinding(',
    'SoulProgressionRuntime.TryHarvestCorpse(',
    'SoulProgressionRuntime.RollbackCorpseHarvest(',
    'SoulProgressionRuntime.HarvestOrdinarySummon()',
    'TryCreateRemains(',
    'SoulProgressionRuntime.GetQualityHealthMultiplier(',
    'SoulProgressionRuntime.RollRaisedHealthFraction(',
    'RaisedSalvageMaximumRefundFraction = 0.75f',
    'ReanimationPositionRefreshSeconds = 0.10f',
    '_nextReanimationPositionRefreshTime',
    'Time.unscaledTime < _nextReanimationPositionRefreshTime',
    'npc.RemoveElementsOfType<NpcHealthRegeneration>()',
    'SkeletonSummonVfxKey =',
    '"0d139743aa2c21d4da0c81fb4e609890"',
    'new ShareableARAssetReference(SkeletonSummonVfxKey)',
    'TryServeHeavyTarget(_heavyTarget)',
    '"CastingBegun"',
    'BeforeCastingBegun',
    '"CastingCanceled"',
    'BeforeCastingCanceled',
    'BeginSoulRendCast(hand, lightCast)',
    'RequireMethod(typeof(HealthElement), "Kill")',
    'BeforeKillHeavyTarget',
    'ReferenceEquals(target.HealthElement, __instance)',
    'RequireMethod(typeof(NpcElement), "Destroy")',
    'BeforeDestroyHeavyTarget',
    'ReferenceEquals(target, __instance)',
    'Blocked vanilla heavy Soul Rend NPC destroy',
    'Blocked vanilla heavy Soul Rend servant kill',
    'ServantEmpowerHealthThreshold = 0.95f',
    'ServantHealingPowerZeroFraction = 0.20f',
    'ServantHealingPowerMaximumFraction = 0.50f',
    'Mathf.Clamp01(power / 200.0f)',
    'npc.Health.IncreaseBy(requestedHealing)',
    'SummonRuntime.IsEmpoweredSummon(summon)',
    '1.20f + (0.30f * roll * roll)',
    'SummonRuntime.TryEmpowerSummon(',
    '"Servant Restored and Empowered: "',
    '"Servant Restored: +"',
    '"% health (Power "',
    'record.DismissedAsRemains')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Salvage runtime contract is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)BeforeCastingBegun\(.*?BeginSoulRendCast\(hand, lightCast\).*?BeginSoulRendCast\(.*?_heavyCastActive = !lightCast;.*?TryCaptureFocusedHeavyTarget\(\)') {
    throw "Soul Rend does not arm heavy-cast servant protection before native spell performance begins."
}
if ($runtimeSource -notmatch '(?s)BeforeCastingCanceled\(Item castingItem\).*?ClearLightCastState\(\).*?AfterCastingEnded\(CastState __state\).*?finally\s*\{\s*ClearLightCastState\(\);') {
    throw "Soul Rend cast state is not cleared on both cancellation and completion."
}
if ($runtimeSource -notmatch '(?s)BeforeGetManaExpended\(.*?_heavyCastActive.*?_heavyTarget = __instance;\s*__result = 0\.0f;\s*return false;') {
    throw "Heavy Soul Rend does not suppress the vanilla Health refund while capturing its servant target."
}
if ($runtimeSource -notmatch '(?s)_lightOriginalMana\s*\* _lightHealthFraction\s*\* manaReturnFraction.*?_lightMaximumManaReturn') {
    throw "Light Soul Rend mana restoration does not scale with both current Health and its configured percentage."
}
foreach ($required in @(
    'internal float ManaReturnedOnSacrifice;',
    'raisedRecord.ManaReturnedOnSacrifice = manaReturned;',
    'record.ManaReturnedOnSacrifice);')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Raised-servant combined reward state is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)ShowSoulVigorHarvest\(\s*displayName,\s*qualityTier,\s*soulVigorAward,\s*manaReturned\);') {
    throw 'Ordinary servant unbinding does not pass the exact Mana return into its Soul Vigor reward.'
}
if ($runtimeSource -notmatch '(?s)TryServeHeavyTarget\(.*?beforeFraction.*?ServantEmpowerHealthThreshold.*?requestedHealing = empowerEligibleHealth\s*\? missingHealth\s*:\s*Math\.Min\(missingHealth, maximumHealth \* healingFraction\).*?npc\.Health\.IncreaseBy\(requestedHealing\).*?if \(empowerEligibleHealth.*?SummonRuntime\.TryEmpowerSummon') {
    throw "Heavy Soul Rend does not keep restoration exclusive below 95% while allowing a top-off and Empower at the mercy threshold."
}
if ($summonRuntimeSource -notmatch '(?s)internal static bool IsEmpoweredSummon\(.*?EmpowermentStates\.ContainsKey') {
    throw "Heavy Soul Rend cannot distinguish an already-Empowered servant before selecting its one service."
}
foreach ($required in @(
    'Corpses: Harvest for Soul Vigor.',
    'Servants: Unbind to restore Mana and harvest Soul Vigor.',
    'Enemies: Deal Necrotic damage. Repeated hits strengthen Soul Claim.',
    'Corpses: Bind and reanimate.',
    'Wounded enemies: Attempt Soul Claim below 40% Health.',
    'Servants: Restore Health; at 95%, Empower at 1,000 Soul Vigor.')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Rend tooltip contract is missing: $required"
    }
}
$tooltipBlock = [regex]::Match(
    $runtimeSource,
    '(?s)private static bool BeforeGetMagicDescription\(.+?(?=\r?\n\s*private static void UpdateSoulSalvageItems\()')
if (!$tooltipBlock.Success -or $tooltipBlock.Value.Contains('Frayed Soul')) {
    throw "Soul Rend tooltips must keep Frayed Soul as an internal mechanic."
}
foreach ($required in @(
    'ComparableLightSpellBaseDamage = 5.0f',
    'SoulRendPowerZeroMultiplier = 0.50f',
    'SoulRendPowerNormalMultiplier = 1.00f',
    'SoulRendPowerMaximumMultiplier = 2.00f',
    'FrayedSoulDurationSeconds = 8.0f',
    'FrayedSoulMaximumStacks = 3',
    'SoulClaimHealthThreshold = 0.40f',
    'SoulClaimPowerZeroChance = 0.05f',
    'SoulClaimPowerNormalChance = 0.175f',
    'SoulClaimPowerMaximumChance = 0.30f',
    'SoulClaimAbsoluteChanceCap = 0.35f',
    'DamageType.MagicalHitSource',
    'DamageSubType.GenericMagical',
    'target.HealthElement.TakeDamage(damage)',
    'target.HealthElement.TakeDamage(claimDamage)',
    'TryValidateEligibleLivingTarget(',
    'bindingAlreadyWon: true',
    'summonLimitAlreadyChecked: true')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Living-target Soul Salvage contract is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)healthVulnerability\s*\* powerChance\s*\* qualityFactor\s*\* \(1\.0f\s*\+ \(FrayedSoulChanceBonusPerStack \* frayedStacks\)\)') {
    throw "Soul Claim chance does not combine Health, Power, quality, and Frayed Soul scaling."
}
if ($runtimeSource -notmatch '(?s)target\.HealthElement\.TakeDamage\(claimDamage\);.*?targetLocation\.HasElement<Corpse>\(\).*?TryRaiseCorpse\(') {
    throw "Successful Soul Claim does not pass through native killing damage before protected corpse reanimation."
}
if ($runtimeSource -notmatch '(?s)TryHarvestCorpse\(\s*fingerprint,\s*tier,\s*out harvestReceipt\).*?if \(!TryCreateRemains\(.*?RollbackCorpseHarvest\(harvestReceipt\)') {
    throw "Ordinary corpse harvest does not roll back Soul Vigor when remains creation fails."
}
if ($runtimeSource -notmatch '(?s)bool harvestReady = !record\.Sacrificed\s*\|\| SoulProgressionRuntime\.TryHarvestCorpse\(.*?if \(!simplified && harvestReceipt != null\)\s*\{\s*SoulProgressionRuntime\.RollbackCorpseHarvest\(harvestReceipt\);') {
    throw "Raised-corpse harvest does not commit with the remains transaction."
}

foreach ($required in @(
    'TryFindFocusedSoulTargetCached(',
    '_focusedTargetCacheFrame != frame',
    'int frame = Time.frameCount',
    'TryValidateEligibleCorpse(hero, candidate, out rejection)',
    'Soul Rend could not finish initializing a raised servant:',
    'reanimation failed - source corpse restored')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Salvage safety or focused-target contract is missing: $required"
    }
}
foreach ($required in @(
    'OptionalPropertyCache',
    'OptionalFieldCache',
    'GetPropertySilent(type, name)',
    'GetFieldSilent(type, name)',
    'BindingFlags.DeclaredOnly')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Salvage silent reflection contract is missing: $required"
    }
}
$memberBlock = [regex]::Match(
    $runtimeSource,
    '(?s)private static object GetMemberValue\(.+?(?=\r?\n\s*private static PropertyInfo GetPropertySilent\()')
if (!$memberBlock.Success -or
    $memberBlock.Value.Contains('AccessTools.Property(') -or
    $memberBlock.Value.Contains('AccessTools.Field(')) {
    throw "Optional corpse-quality member probing must remain silent and cached."
}
if ($runtimeSource -notmatch '(?s)npc\.OnCompletelyInitialized\(\s*delegate\s*\{\s*try\s*\{.*?catch \(Exception exception\).*?RestoreSourceCorpse\(\s*summonId,\s*discardRaisedCopy: true,\s*showDiagnostic: false\);') {
    throw "Raised-servant initialization does not restore the source corpse on asynchronous failure."
}

if ($runtimeSource -notmatch '(?s)source\.SetInteractability\(LocationInteractability\.Hidden\);\s*PrefabPool\.InstantiateAndReturn\(\s*new ShareableARAssetReference\(SkeletonSummonVfxKey\),\s*source\.Coords,\s*source\.Rotation\)\.Forget\(\);') {
    throw "Soul Salvage successful raise path does not spawn and return the native skeleton-summon VFX."
}

foreach ($forbidden in @(
    'new TimeDuration(',
    'plugin.ReanimationDurationSeconds',
    'plugin.ReanimationHealthPercent',
    'UpdateReanimationDecay(',
    'CalculateDecayPerSecond(',
    'SoulSalvageReturnMode',
    'SoulSalvageReturn',
    '"LightCastReturn"',
    'GetHealthAllocation(',
    'hero.Health.IncreaseBy(')) {
    if ($runtimeSource.Contains($forbidden)) {
        throw "Soul Salvage runtime retains replaced behavior: $forbidden"
    }
}

foreach ($document in @($readme, $nexus)) {
    foreach ($required in @(
        '40-60%',
        'Power 200',
        '30 base mana',
        '75%',
        '2%',
        '8%',
        '20%/35%/50%',
        'Swarm',
        'Empower')) {
        if (!$document.Contains($required)) {
            throw "Soul Salvage documentation is missing: $required"
        }
    }
}

foreach ($required in @(
    'SoulVigorAtNormalMaximumPower = 1000.0f',
    'SoulVigorAtAbsoluteMaximumPower = 5000.0f',
    '(10.0f * x * x * x) - (70.0f * x * x) + (160.0f * x)',
    'Mathf.Lerp(0.75f, 1.25f, power / 100.0f)',
    'Mathf.Lerp(1.25f, 0.75f, power / 100.0f)',
    'Mathf.Lerp(1.25f, 1.50f, (power - 100.0f) / 100.0f)',
    'Mathf.Lerp(0.75f, 0.50f, (power - 100.0f) / 100.0f)',
    'progress += GetBindingIncrement(corpseFingerprint, attempt)',
    '+ (0.75f * power)',
    'power >= 199.999f',
    'private const string SoulVigorKey = "soul_vigor.total"',
    'return 20.0f;')) {
    if (!$progressionSource.Contains($required)) {
        throw "Soul progression contract is missing: $required"
    }
}
foreach ($required in @(
    '+ " Mana"',
    'text += " | ";',
    '+ " Soul Vigor"',
    '"Reward"',
    '"Short"',
    '"Gained "',
    '", Necromantic Power="')) {
    if (!$progressionSource.Contains($required)) {
        throw "Soul Vigor reward presentation is missing: $required"
    }
}
if ($progressionSource.Contains(' Soul Vigor - ')) {
    throw 'Soul Vigor rewards must not retain the verbose source-name suffix.'
}
if ($progressionSource -notmatch '(?s)"soul-vigor-harvest",\s*text,.*?"High",\s*string\.Empty,\s*"Reward",\s*"Short"') {
    throw 'Soul Vigor rewards are not short, High-priority, non-consolidating Reward events.'
}
foreach ($required in @(
    'GetSoulClaimFailureMessage()',
    'ShowSoulClaimFeedback(')) {
    if (!$progressionSource.Contains($required)) {
        throw "Soul Claim feedback contract is missing: $required"
    }
}

foreach ($required in @(
    'Version under test: 1.0.6',
    'exactly 2x',
    'a true hero summon rises',
    'simplified remains',
    'green/dark skeleton-summon effect',
    'SAS-SMOKE-30',
    'SAS-SMOKE-31',
    'Soul Vigor: X (Y)',
    '30/34.8/39.6/44.4 m',
    '50%/100%/200%',
    '5%/17.5%/30%',
    'never exceed 35%')) {
    if (!$matrix.Contains($required)) {
        throw "Soul Salvage test matrix is missing: $required"
    }
}

Write-Host "Soul Salvage progression, mana, quality, upkeep, Empower, and native-summon contracts passed."
