$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulAndService.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$glyphSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ReanimationGlyphRuntime.cs") -Raw
$summonRuntimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SummonRuntime.cs") -Raw
$progressionSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulProgressionRuntime.cs") -Raw
$innerLightSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulRendInnerLightRuntime.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw
$matrix = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\TEST-MATRIX.md") -Raw
foreach ($required in @(
    'ConfigSchemaVersion = 27',
    '"ks.tgfoa.versatile-weapons"',
    '"ks.tgfoa.first-person-arms-adjuster"',
    '"EnableLivingTargetSoulSalvage"',
    '"PersistentServants"')) {
    if (!$pluginSource.Contains($required)) {
        throw "Soul Salvage plugin configuration is missing: $required"
    }
}

if ($runtimeSource -notmatch '(?s)float actualStartingHealthFraction = maximumHealth > 0\.0001f.*?npc\.Health\.ModifiedValue / maximumHealth.*?\+ \(actualStartingHealthFraction \* 100\.0f\)\.ToString\(') {
    throw 'Reanimation feedback does not report actual Health after retention and Blood Magic penalties.'
}

foreach ($required in @(
    'internal ConfigEntry<bool> SoulRendInnerLightEnabled',
    'internal ConfigEntry<float> SoulRendInnerLightIntensity',
    'internal ConfigEntry<float> SoulRendInnerLightIntensityMultiplier',
    '"SoulRendIntensityMultiplier"',
    '0.8f',
    'internal ConfigEntry<float> SoulRendInnerLightInteriorIntensityMultiplier',
    'internal ConfigEntry<float> SoulRendInnerLightMinimumPowerBrightnessMultiplier',
    'internal ConfigEntry<float> SoulRendInnerLightMasteryBrightnessMultiplier',
    'internal ConfigEntry<float> SoulRendInnerLightMaximumPowerBrightnessMultiplier',
    'internal ConfigEntry<float> SoulRendInnerLightMinimumPowerRange',
    'internal ConfigEntry<float> SoulRendInnerLightMasteryRange',
    'internal ConfigEntry<float> SoulRendInnerLightMaximumPowerRange',
    'internal ConfigEntry<float> SoulRendInnerLightFadeSeconds')) {
    if (!$pluginSource.Contains($required)) {
        throw "Soul Rend inner-light configuration is missing: $required"
    }
}
foreach ($required in @(
    'CastBoostMultiplier = 3.0f',
    'CastBoostDelaySeconds = 0.3f',
    'LightShadows.None',
    'SoulProgressionRuntime.GetNecromanticPower()',
    'SceneService sceneService = World.Services.TryGet<SceneService>()',
    'SoulSalvageRuntime.IsVersatileWeaponsHandSuppressed(',
    'fsm.IsCasting',
    'Mathf.MoveTowards(',
    'TryGetCurrentVisualWorldOffset',
    'LoadingStates.IsLoadingWorld',
    'LoadingScreenUI.IsLoading',
    'World.HasAny<LoadingScreenUI>()',
    'DestroyHand(MainHand)',
    'DestroyHand(OffHand)',
    'state.LightObject.transform.position = anchor.position',
    '+ visualWorldOffset',
    'GetComponent<HDAdditionalLightData>()',
    'state.HdrpData.volumetricDimmer = 0.0f',
    'state.HdrpData.EnableShadows(false)',
    'progress * progress * (3.0f - (2.0f * progress))')) {
    if (!$innerLightSource.Contains($required)) {
        throw "Soul Rend inner-light runtime contract is missing: $required"
    }
}
if ($innerLightSource.Contains('ConfigureHdrpData(state, nextIntensity)') -or
    $innerLightSource.Contains('ResolveHdAdditionalLightDataType')) {
    throw 'Soul Rend inner lights still perform reflective HDRP setup per frame.'
}
if (($innerLightSource -notmatch '(?s)internal static void Update\(\).*?featureEnabled = plugin\.IsEnabled.*?mainHandEligible = featureEnabled.*?offHandEligible = featureEnabled.*?if \(!mainHandEligible && !offHandEligible\).*?return;.*?foreach \(MagicFSM fsm') -or
    ($innerLightSource -notmatch '(?s)ShouldShow\(bool handEligible, MagicFSM fsm\).*?IsHiddenState\(fsm\.CurrentStateType\).*?IsHiddenState\(fsm\.CurrentStateToEnterType\).*?IsHiddenState\(HeroStateType state\).*?HeroStateType\.Empty.*?HeroStateType\.UnEquipWeapon.*?HeroStateType\.UnEquipWeaponAlternate') -or
    $innerLightSource.Contains('CurrentStateType.ToString()') -or
    $innerLightSource.Contains('CurrentStateToEnterType.ToString()')) {
    throw 'Soul Rend inner lights do not bypass inactive FSM discovery or use allocation-free typed equip-state checks.'
}
if ($pluginSource -notmatch '(?s)private void LateUpdate\(\).*?SoulRendInnerLightRuntime\.LateUpdate\(\)') {
    throw "Soul Rend hand-light positioning is not deferred until LateUpdate."
}
if (($innerLightSource -notmatch '(?s)internal static void LateUpdate\(\).*?IsWorldTransitioning\(\).*?hero == null.*?hero\.HasBeenDiscarded.*?DestroyHand\(MainHand\).*?DestroyHand\(OffHand\).*?return;.*?UpdatePosition\(MainHand, hero, visualWorldOffset\)') -or
    ($innerLightSource -notmatch '(?s)GetTransformProperty\(object owner, string propertyName\).*?property\.GetValue\(owner, null\) as Transform;.*?catch.*?return null;')) {
    throw 'Soul Rend hand lights do not suspend across world loading or safely retry unavailable hand properties.'
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
    "Keep ordinary summons and each raised servant's source identity, Health, Empowerment, investment, and Soulforged progress through saving, loading, and restarting the game",
    'GetConfigDisplaySection(section, key)',
    '"Host and Persistence"',
    '"Commands and Targeting"',
    '"Soul Rend Hand Light"',
    '"Advanced"',
    '"Enable Attack Command"',
    '"Enable Host Commands"',
    '"Passive Crosshair Target Sharing"',
    '"Play Soul Rend Ritual Audio"',
    '"Distance Fade Strength"',
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
    'SoulProgressionRuntime.TrySpendSoulVigor(',
    'SoulProgressionRuntime.GetOrRollCorpseSoulVigorValue(',
    'VersatileWeaponsPluginGuid =',
    'VersatileWeaponsApiTypeName =',
    '"IsMainHandSuppressed"',
    '"IsOffHandSuppressed"',
    'TryResolveVersatileWeaponsApi()',
    'OrdinarySummonVigorCostPerTier = 3',
    'VanillaSummonTiers =',
    'OrdinarySummonCastTiers =',
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
    'GetEmpowermentSoulVigorCost(summon, power)',
    'AddEmpowermentSoulVigorInvestment(',
    '" | -" + committedVigor.ToString(CultureInfo.InvariantCulture)',
    '"Servant Restored and Empowered: "',
    '"Servant Restored: +"',
    '"% health (Power "',
    'record.DismissedAsRemains')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Salvage runtime contract is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)IsSoulSalvageEquipped\(\).*?IsSoulSalvageItem\(mainHandItem\).*?!IsVersatileWeaponsHandSuppressed\(\s*EquipmentSlotType\.MainHand\).*?IsSoulSalvageItem\(offHandItem\).*?!IsVersatileWeaponsHandSuppressed\(\s*EquipmentSlotType\.OffHand\)') {
    throw "Soul Rend equipment detection does not independently reject suppressed main-hand and offhand slots."
}
if ($runtimeSource -notmatch '(?s)TryResolveVersatileWeaponsApi\(\).*?Chainloader\.PluginInfos\.TryGetValue\(\s*VersatileWeaponsPluginGuid.*?Delegate\.CreateDelegate\(\s*typeof\(Func<bool>\).*?mainMethod.*?Delegate\.CreateDelegate\(\s*typeof\(Func<bool>\).*?offMethod') {
    throw "Soul Rend does not bind the optional Versatile Weapons hand-suppression API."
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
if ($runtimeSource -notmatch '(?s)CalculateFullHealthLightManaReturn\(.*?_lightOriginalMana\s*\* \(plugin\.SoulSalvageManaReturnPercent\.Value / 100\.0f\).*?Mathf\.Round\(Math\.Min\(rawReturn, _lightMaximumManaReturn\)\)' -or
    $runtimeSource -notmatch '(?s)BeforeGetManaExpended\(.*?CompleteLightSummonHarvest\(__instance\);.*?__result = NativeManaRefundMultiplier > 0\.0f\s*\? _lightResolvedManaReturn / NativeManaRefundMultiplier') {
    throw "Light Soul Rend does not route its stage-specific Mana award through the native refund path."
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
if ($summonRuntimeSource -notmatch '(?s)internal static bool IsEmpoweredSummon\(.*?state\.IsEmpowered') {
    throw "Heavy Soul Rend cannot distinguish an already-Empowered servant before selecting its one service."
}
foreach ($required in @(
    'Corpses: Harvest for Soul Vigor.',
    'Servants: Strip Empowerment, then two Soulforged ranks per cast; unbind at rank 0.',
    "Enemies: Deal Necrotic damage. Each surviving hit raises that enemy's Soul Claim threshold by 2%, up to 10%.",
    'Corpses: Bind and reanimate; cost scales with soul quality.',
    'Wounded enemies: Claim at or below their Power- and soul-quality threshold.',
    'Servants: Restore Health; at 95%, Empower at 1,000 Soul Vigor for twice base soul value.')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Rend tooltip contract is missing: $required"
    }
}
$tooltipBlock = [regex]::Match(
    $runtimeSource,
    '(?s)private static bool BeforeGetMagicDescription\(.+?(?=\r?\n\s*private static void UpdateSoulSalvageItems\()')
if (!$tooltipBlock.Success -or $tooltipBlock.Value.Contains('Frayed Soul')) {
    throw "Soul Rend tooltips must not expose the removed Frayed Soul name."
}
foreach ($required in @(
    'ComparableLightSpellBaseDamage = 5.0f',
    'SoulRendPowerZeroMultiplier = 0.50f',
    'SoulRendPowerNormalMultiplier = 1.00f',
    'SoulRendPowerMaximumMultiplier = 2.00f',
    'SoulClaimMaximumPreparationHits = 5',
    'SoulClaimThresholdBonusPerHit = 0.02f',
    'SoulClaimMinimumHealthThreshold = 0.01f',
    'SoulClaimMaximumHealthThreshold = 0.40f',
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
if ($runtimeSource -notmatch '(?s)GetSoulClaimPowerThreshold\(power\).*?SoulClaimThresholdBonusPerHit \* Math\.Min\(.*?- GetSoulClaimQualityResistance\(qualityTier\).*?\+ presetAdjustment.*?SoulClaimMinimumHealthThreshold,\s*SoulClaimMaximumHealthThreshold') {
    throw "Soul Claim threshold does not combine Power, permanent hit preparation, quality resistance, preset adjustment, and the final clamp."
}
if ($runtimeSource -notmatch '(?s)GetSoulClaimPowerThreshold\(float power\).*?0\.00f, 0\.05f.*?0\.05f, 0\.10f.*?0\.10f, 0\.15f.*?0\.15f, 0\.20f.*?0\.20f, 0\.25f.*?0\.25f, 0\.30f') {
    throw "Soul Claim Power thresholds must remain 5/10/15/20/25/30% at Power 25/50/75/100/150/200."
}
if ($runtimeSource -notmatch '(?s)GetSoulClaimQualityResistance.*?Worthy:\s*return 0\.03f;.*?Potent:\s*return 0\.06f;.*?Prime:\s*return 0\.09f;') {
    throw "Soul Claim quality resistance must remain 0/3/6/9% for Meager/Worthy/Potent/Prime."
}
$claimBlock = [regex]::Match(
    $runtimeSource,
    '(?s)private static void TryClaimLivingTarget\(.+?(?=\r?\n\s*private static float GetSoulClaimPowerThreshold\()')
if (!$claimBlock.Success -or
    $runtimeSource -match 'FrayedSoul|CalculateSoulClaimChance' -or
    $claimBlock.Value -match 'UnityEngine\.Random\.value') {
    throw "Soul Claim must remain deterministic and free of the retired timed Frayed Soul mechanic."
}
if ($runtimeSource -notmatch '(?s)target\.HealthElement\.TakeDamage\(claimDamage\);.*?targetLocation\.HasElement<Corpse>\(\).*?TryRaiseCorpse\(') {
    throw "Successful Soul Claim does not pass through native killing damage before protected corpse reanimation."
}
if ($runtimeSource -notmatch '(?s)TryHarvestCorpse\(\s*fingerprint,\s*harvestIdentity,\s*tier,\s*quality01,\s*out harvestReceipt\).*?broadCurrentSessionHarvest.*?CanSafelySimplifyOrdinaryCorpse\(corpse\).*?bool simplified.*?if \(canSafelySimplify.*?&& !simplified.*?&& !broadCurrentSessionHarvest\).*?RollbackCorpseHarvest\(harvestReceipt\).*?if \(!simplified\).*?its soul remains spent') {
    throw "Fresh broad corpse harvest must retain its committed reward when protected or when safe simplification fails, while legacy harvest keeps transactional rollback."
}
if ($runtimeSource -notmatch '(?s)AccessTools\.Constructor\(\s*typeof\(Corpse\).*?typeof\(NpcElement\), typeof\(ICharacter\).*?nameof\(AfterCorpseConstructed\).*?AfterCorpseConstructed\(.*?npc\.IsSummon.*?npc\.HasElement<NpcHeroSummon>\(\)' -or
    $runtimeSource -notmatch '(?s)TryValidateEligibleCorpse\(.*?GetCorpseHarvestIdentity\(candidate\).*?IsCorpseHarvested\(harvestIdentity\).*?no Soul Vigor remains in that corpse.*?!needsSpawnTemplate\s*&& IsCurrentSessionCorpseHarvestEligible\(corpse\).*?TryResolveEligibleSoulTargetIdentity\(') {
    throw "Broad harvest must trust only genuine current-session non-summon corpse construction while restored and reanimation paths retain structural validation."
}
if ($progressionSource -notmatch '(?s)TryHarvestCorpse\(\s*string corpseFingerprint,\s*string harvestIdentity.*?TryHarvestCorpse\(\s*harvestIdentity.*?GetOrRollCorpseSoulVigorValue\(\s*corpseFingerprint' -or
    $progressionSource -notmatch '(?s)internal static bool IsCorpseHarvested\(string corpseFingerprint\).*?HarvestKey\(corpseFingerprint\).*?!= 0' -or
    $runtimeSource -notmatch '(?s)GetCorpseHarvestIdentity\(Location source\).*?\(\(Model\)source\)\.ID.*?"corpse-model\|" \+ modelId') {
    throw "Spent-corpse state is not exposed through the durable harvest ledger."
}
foreach ($feedback in @(
    'Soul Rend: no Soul Vigor remains in that corpse.',
    'Soul Rend: no soul remains to bind.',
    'Soul Rend: this soul is too resistant to bind.')) {
    if (!$runtimeSource.Contains($feedback)) {
        throw "Broad corpse harvesting is missing user-facing feedback: $feedback"
    }
}
if ($runtimeSource -notmatch '(?s)bool harvestReady = !record\.Sacrificed\s*\|\| SoulProgressionRuntime\.TryHarvestCorpse\(.*?bool sourceDeferred =.*?if \(!simplified && harvestReceipt != null && !sourceDeferred\)\s*\{\s*SoulProgressionRuntime\.RollbackCorpseHarvest\(harvestReceipt\);.*?ScheduleDeferredSourceRestoration\(record\);') {
    throw "Raised-corpse harvest does not commit with the remains transaction."
}

foreach ($required in @(
    'private const int OrdinarySummonVigorCostPerTier = 3;',
    'private static int GetOrdinarySummonTier(Item item)',
    'private static int GetOrdinarySummonSoulVigorCost(int summonTier, float power)',
    'Soul Vigor Cost: ',
    'AfterGetMagicDescription',
    'SoulProgressionRuntime.TrySpendSoulVigor(',
    'SoulProgressionRuntime.RestoreSoulVigor(',
    'int committedVigor = vigorAfter < vigorBefore ? vigorCost : 0;',
    'private static int GetReanimationSoulVigorCost(',
    'InvestedSoulVigor = committedVigor',
    'NativeSoulVigor = nativeSoulVigor',
    'TryResolveOwnedBloodServantForInterop(',
    'TryResolveOwnedBloodServantIdentityForInterop(',
    'TryExsanguinateOwnedBloodServantForInterop(',
    'TryMaterializeOwnedBloodServantCorpseForAbhartachForInterop(',
    'GetBloodMagicCorpseIdentity(',
    'SetOwnedBloodServantRitualStateForInterop(',
    'TryResolveOwnedLivingSummon(',
    'BloodRitualExecuted',
    'ExecutedServantRemains',
    'RestoreExecutedServantCorpse(',
    'UpdateExecutedServantRemains();',
    'text = "Restore Servant";',
    'text = (affordable ? "Empower: " : "Requires ")',
    'state = (int)HeavySoulRendHoverState.ServantFullyRestored;',
    '&& state != (int)HeavySoulRendHoverState.ServantFullyRestored;')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Spendable Vigor, blood-servant, or heavy-hover contract is missing: $required"
    }
}
if ($progressionSource -notmatch '(?s)GetOrRollCorpseSoulVigorValue\(.*?CorpseSoulVigorKey\(corpseFingerprint\).*?facts\.Get\(key, 0\).*?RollSoulVigorValue\(tier, quality01\).*?facts\.Set\(key, rolled\)') {
    throw "Corpse Soul Vigor is not rolled once and persisted by corpse fingerprint."
}
if ($runtimeSource -notmatch '(?s)GetReanimationSoulVigorCost\(\s*int nativeSoulVigor,\s*float power\).*?GetPowerScaledSoulVigorCost\(nativeSoulVigor, power\)' -or
    $runtimeSource -notmatch '(?s)GetPowerScaledSoulVigorCost\(int baseCost, float power\).*?Mathf\.Lerp\(2\.0f, 1\.0f, safePower / 100\.0f\).*?Mathf\.Lerp\(\s*1\.0f,\s*0\.5f,\s*\(safePower - 100\.0f\) / 100\.0f\).*?Math\.Max\(0, baseCost\).*?\* multiplier.*?\.SoulVigorCostMultiplier') {
    throw "Summon and reanimation costs do not share the 2x/1x/0.5x Power curve."
}
if ($runtimeSource -notmatch '(?s)GetOrdinarySummonSoulVigorCost\(int summonTier, float power\).*?Math\.Max\(1, summonTier\) \* OrdinarySummonVigorCostPerTier.*?power' -or
    $runtimeSource -notmatch '(?s)OnSummonInitialized\(NpcHeroSummon summon\).*?GetOrdinarySummonTier\(summon\.Item\).*?GetOrdinarySummonSoulVigorCost\(.*?TrySpendSoulVigor\(\s*vigorCost') {
    throw "Ordinary summons do not price their authored tier through the shared Power curve before spending."
}
if ($runtimeSource -notmatch '(?s)GetEmpowermentSoulVigorCost\(.*?GetOrdinarySummonTier\(summon == null \? null : summon\.Item\).*?OrdinarySummonVigorCostPerTier.*?Reanimations\.TryGetValue\(.*?record\.NativeSoulVigor.*?GetPowerScaledSoulVigorCost\(baseSoulVigor \* 2, power\)' -or
    $runtimeSource -notmatch '(?s)TryServeHeavyTarget\(.*?GetEmpowermentSoulVigorCost\(summon, power\).*?TrySpendSoulVigor\(.*?SummonRuntime\.TryEmpowerSummon\(.*?RestoreSoulVigor\(committedVigor\).*?AddEmpowermentSoulVigorInvestment\(.*?ShowSoulVigorWanesAfterSpend\(' -or
    $runtimeSource -notmatch '(?s)AddEmpowermentSoulVigorInvestment\(.*?record\.InvestedSoulVigor \+= committedVigor.*?record\.Recovery\.EmpowermentSoulVigorInvestment \+=\s*committedVigor.*?investment\.InvestedSoulVigor \+= committedVigor.*?investment\.Recovery\.EmpowermentSoulVigorInvestment \+=\s*committedVigor') {
    throw 'Empower does not price twice the stable servant soul value through the current Power curve, commit only on success, and record its exact severable payment.'
}

foreach ($required in @(
    'EmpowermentSeverRefundFraction = 0.75f',
    'SoulforgedRecoveryFractionPerRank = 0.03f',
    'SoulforgedRecoveryMinimumHealthFraction = 0.50f',
    'SummonRuntime.TryRemoveEmpowerment(summon)',
    'SoulforgedRuntime.TryReduceRealRanks(',
    'RecoveredSoulforgedRankMask',
    'for (int rank = previousRank; rank > currentRank; rank--)',
    'currentMask |= 1 << (rank - 1)',
    'SoulforgedRecoveryMinimumHealthFraction,',
    'CalculateFullHealthLightManaReturn(plugin)',
    '_lightPreserveTarget = true;',
    'Blocked native light Soul Rend destruction after resolving one servant layer.')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Staged servant Soul Rend contract is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)CompleteLightSummonHarvest\(.*?IsEmpoweredSummon\(summon\).*?TryRemoveEmpowerment\(summon\).*?EmpowermentSeverRefundFraction.*?return;.*?GetRealRank\(summon\).*?TryReduceRealRanks\(\s*summon,\s*2,.*?ApplySoulforgedRecovery\(.*?return;.*?recovery\.RemainingMana.*?_lightHealthFraction' -or
    $runtimeSource -notmatch '(?s)ApplySoulforgedRecovery\(.*?RecoveredSoulforgedRankMask.*?SoulforgedRecoveryFractionPerRank.*?recovery\.RemainingMana = Math\.Max\(.*?SoulforgedRecoveryMinimumHealthFraction.*?RestoreSoulVigor') {
    throw 'Light Soul Rend no longer resolves Empowerment, unique rank tranches, and final unbinding in the approved order.'
}
if ($progressionSource -notmatch '(?s)ShowServantSoulRendStage\(.*?"servant-soul-rend".*?"servant-soul-rend-" \+ summonId.*?eventId,\s*text,\s*"necro",\s*"High",\s*collapseKey,\s*"Status",\s*"Short"' -or
    $runtimeSource -notmatch '(?s)"servant-empowerment-severed",\s*summonId,\s*displayName \+ ": Empowerment severed"\s*\+ \(actualRefund > 0' -or
    $runtimeSource -notmatch '(?s)if \(_lightResolvedManaReturn > 0\.0f\).*?" Mana";.*?if \(vigorAward > 0\).*?" Soul Vigor";.*?ShowServantSoulRendStage\(\s*"servant-rank-reduced",\s*summonId,.*?" -> ".*?GetRankLabel\(currentRank\)') {
    throw 'Staged servant Soul Rend GFT does not use dedicated per-servant High/Short Necrotic Status messages with conditional resource segments.'
}
if ($runtimeSource.Contains('Next cast unbinds')) {
    throw 'Staged servant Soul Rend GFT must not add a next-cast unbinding warning.'
}
if ($runtimeSource -notmatch '(?s)TryGetHeavySoulRendHover\(.*?GetEmpowermentSoulVigorCost\(.*?affordable.*?HeavySoulRendHoverState\.EmpowerServant.*?HeavySoulRendHoverState\.RequiresSoulVigor.*?"Empower: ".*?"Requires ".*?" Soul Vigor"') {
    throw 'Heavy Soul Rend hover does not use the canonical Empower/Requires Soul Vigor price grammar.'
}
if ($runtimeSource -notmatch '(?s)TryGetHeavySoulRendHover\(.*?CalculateSoulClaimThreshold\(.*?int claimThresholdPercent = Mathf\.Clamp\(.*?int targetHealthPercent = Mathf\.Clamp\(.*?HeavySoulRendHoverState\.ClaimSoul.*?"Claim Soul at ".*?"% \(".*?"%\)"' -or
    $runtimeSource.Contains('target.Health.Percentage > threshold')) {
    throw 'Soul Claim hover must remain visible for eligible living targets and show its threshold with current target Health.'
}
if ([regex]::Matches($runtimeSource, '\{ "[0-9a-f]{32}", [1-6] \}').Count -ne 26 -or
    $runtimeSource -notmatch '\{ "7a26e25196836554b88af907781341f3", 3 \}.*Summon Keeper' -or
    $runtimeSource -notmatch '\{ "7ab9829d6ebdcfd4e935fc658a6201f8", 4 \}.*Ghost of Broc Meala' -or
    $runtimeSource -notmatch '\{ "a339badda1efbe841ac49fcd62f13888", 5 \}.*Sir Vast') {
    throw "The 26 active vanilla summon spells do not retain the approved balance tiers and overrides."
}
if ($runtimeSource -notmatch '(?s)TryRaiseCorpse\(.*?preparedCorpseFingerprint.*?preparedNativeSoulVigor.*?preparedVigorCost.*?NativeSoulVigor = nativeSoulVigor') {
    throw "Soul Claim and corpse reanimation do not carry one prepared native value and cost into the raised servant."
}
if ($runtimeSource -notmatch '(?s)TryGetHeavySoulRendHover\(.*?GetOrRollCorpseSoulVigorValue\(.*?GetReanimationSoulVigorCost\(\s*nativeSoulVigor,\s*SoulProgressionRuntime\.GetNecromanticPower\(\)\)') {
    throw "Heavy Soul Rend hover does not preview the stable corpse-specific Power-scaled cost."
}
if ($runtimeSource -notmatch '(?s)GetBloodMagicCorpseIdentity\(Location sourceCorpse\).*?sourceCorpse\.TryGetElement<Corpse>\(\)' -or
    $runtimeSource -notmatch '(?s)TryResolveOwnedBloodServantIdentityForInterop\(.*?sourceLocation = record\.SourceCorpse;.*?sourceCorpse = record\.SourceCorpse\.TryGetElement<Corpse>\(\);.*?servantNpc = record\.RaisedNpc' -or
    $runtimeSource -notmatch '(?s)TryResolveOwnedBloodServantForInterop\(.*?sourceCorpse = sourceCorpseElement \?\? sourceLocation' -or
    $runtimeSource -notmatch '(?s)GetBloodExsanguinationSeverity\(object sourceCorpse\).*?sourceCorpse as Location.*?GetBloodMagicCorpseIdentity\(sourceLocation\)') {
    throw "Blood Magic interop does not expose both the stable source Location and its registered Corpse element."
}
if ($runtimeSource -notmatch '(?s)TryResolveOwnedLivingSummon\(.*?World\.All<NpcHeroSummon>\(\).*?IsComponentWithinNpcVisual\(component, candidateNpc\)' -or
    $runtimeSource -notmatch '(?s)TryResolveReanimationRecord\(.*?IsComponentWithinNpcVisual\(\s*component,\s*candidateRecord\.RaisedNpc\).*?IsComponentWithinNpcVisual\(.*?candidate\.IsChildOf\(root\)') {
    throw 'Animated servant hitbox components are not resolved through the owning summon visual hierarchy.'
}
if ($pluginSource -notmatch 'public const int ApiVersion = 10;' -or
    $pluginSource -notmatch 'public static bool TryResolveOwnedBloodServantIdentity\(' -or
    $pluginSource -notmatch 'public static bool TryMaterializeOwnedBloodServantCorpseForAbhartach\(') {
    throw "Soul and Service API 10 does not publish the dual corpse-identity and Abhartach bridges."
}
if ($runtimeSource -notmatch '(?s)TryMaterializeOwnedBloodServantCorpseForAbhartachForInterop\(.*?TryResolveReanimationRecord.*?SourceCorpse\.TryGetElement<NpcDummy>\(\).*?Reanimations\.Remove\(summonId\).*?MoveAndRotateTo\(coords, rotation, true\).*?SetInteractability\(record\.SourceInteractability\).*?PendingRaisedDiscards\.Add\(record\.RaisedLocation\).*?corpseLocation = record\.SourceCorpse.*?OrdinarySummonInvestments\.Remove.*?npc\.HealthElement\.Kill\(\).*?location\.TryGetElement<NpcDummy>\(\)') {
    throw "Abhartach sacrifice does not safely materialize source-backed and ordinary servants as native corpses."
}
if ($runtimeSource -notmatch '(?s)if \(healthFraction <= 0\.20f\).*?record\.BloodRitualExecuted = true;.*?record\.RaisedNpc\.HealthElement\.Kill\(\);') {
    throw 'Blood ritual execution does not preserve its pre-death soul value and kill at 20% Health.'
}
$bloodRitualVfx = [regex]::Match(
    $runtimeSource,
    '(?s)private static void SpawnBloodRitualVfx\(ReanimationRecord record\).*?(?=\r?\n\s*private static )')
if (!$bloodRitualVfx.Success -or
    $bloodRitualVfx.Value -notmatch '(?s)case Grailwright\.Shared\.CorpseQualityTier\.Meager:.*?case Grailwright\.Shared\.CorpseQualityTier\.Worthy:.*?vfxKey = BloodRitualLesserVfxKey;.*?case Grailwright\.Shared\.CorpseQualityTier\.Potent:.*?case Grailwright\.Shared\.CorpseQualityTier\.Prime:.*?vfxKey = BloodRitualGreaterVfxKey;' -or
    $runtimeSource -notmatch '(?s)BloodRitualLesserVfxKey =\s*"d858e5e33ccd9ec4ea9b3099ee02d32e"' -or
    $runtimeSource -notmatch '(?s)BloodRitualGreaterVfxKey =\s*"bfa9aa86addeec347877ffb0fc0b4315"' -or
    $bloodRitualVfx.Value -notmatch '(?s)VFXBodyMarker\.Mesh\.localBoundingSphere.*?VFXBodyMarker\.transform\.TransformPoint\(.*?PrefabPool\.InstantiateAndReturn\(\s*new ShareableARAssetReference\(vfxKey\),\s*vfxPosition,\s*Quaternion\.identity\)\.Forget\(\);') {
    throw 'Completed reanimated-servant Blood Rituals do not select and spawn the approved target VFX by corpse quality.'
}
$bloodRitualState = [regex]::Match(
    $runtimeSource,
    '(?s)internal static bool SetOwnedReanimatedServantBloodRitualStateForInterop\(.*?(?=\r?\n\s*internal static )')
if (!$bloodRitualState.Success -or
    $bloodRitualState.Value -notmatch '(?s)if \(completed\)\s*\{.*?SpawnBloodRitualVfx\(record\);.*?\}\s*else if \(channeling\)' -or
    [regex]::Matches($bloodRitualState.Value, 'SpawnBloodRitualVfx\(record\);').Count -ne 1) {
    throw 'Reanimated-servant Blood Ritual VFX must spawn exactly once, only when the ritual completes.'
}
if ($runtimeSource -notmatch '(?s)if \(record\.BloodRitualExecuted && !record\.IsMiniboss\).*?ExecutedServantRemains\[\(\(Model\)record\.RaisedLocation\)\.ID\] = record;') {
    throw 'An executed servant is not retained as a later light Soul Rend target.'
}
if ($runtimeSource -notmatch '(?s)bool executedServant = ExecutedServantRemains\.TryGetValue\(.*?ExecutedServantRemains\.Remove\(\(\(Model\)corpse\)\.ID\);') {
    throw 'Executed-servant light harvest is not a once-only remains transaction.'
}
if ($runtimeSource -notmatch '(?s)if \(ExecutedServantRemains\.ContainsKey\(\(\(Model\)candidate\)\.ID\)\).*?rejection = string\.Empty;.*?return true;') {
    throw 'Executed allied servant remains are not eligible for the promised later light Soul Rend.'
}
$bloodTargetResolver = [regex]::Match(
    $runtimeSource,
    '(?s)internal static bool TryResolveOwnedReanimatedServantForInterop\(.+?(?=\r?\n\s*internal static )')
if (!$bloodTargetResolver.Success) {
    throw 'The owned raised-servant Blood Magic resolver is missing.'
}
if ($bloodTargetResolver.Value.Contains('hero.IsInCombat()')) {
    throw 'Raised-servant identity must remain available in combat so Blood Magic can expose a blocked reticle state.'
}
if (!$bloodTargetResolver.Value.Contains('record.SourceCorpse') -or
    !$bloodTargetResolver.Value.Contains('record.RaisedNpc.IsAlive')) {
    throw 'Raised-servant Blood Magic identity lost its source-corpse or living-servant provenance checks.'
}

foreach ($required in @(
    'TryFindFocusedSoulTargetCached(',
    '_focusedTargetCacheFrame != frame',
    'int frame = Time.frameCount',
    'SoulRendAssistRadius = 0.4f',
    'SoulRendAssistColliderBufferSize = 64',
    'Physics.OverlapSphereNonAlloc(',
    'IsSoulRendAssistSurface(hit, candidate)',
    'TryFindNearestEligibleCorpse(',
    'TryResolveEligibleSoulTargetIdentity(',
    'Soul Rend could not finish initializing a raised servant:',
    'reanimation failed - source corpse restored')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Salvage safety or focused-target contract is missing: $required"
    }
}

foreach ($required in @(
    'HighSoulPoolPortions = 6',
    'MinibossReanimationBaseSoulVigor = 120',
    'MinibossBindingResistance = 340.0f',
    'ReanimatableMinibossTemplateGuids',
    'BeforeReplaceHighSoulCorpse',
    'IsPotentialSavedHighSoulCorpse(',
    'TryResolveHighSoulCorpse(',
    'CanReanimateMiniboss(',
    'candidate.Initializer is SceneLocationInitializer',
    'candidate.Initializer is RuntimeLocationInitializer',
    'LocationInitializerField',
    'RuntimeLocationData',
    'Location.DiscardedPlacesKey',
    'TryPrepareMinibossSourceForService(',
    'TryReturnMinibossSourceToCurrentScene(',
    'HasDeferredSourceRestoration(',
    'TryRestoreDeferredSource(',
    'RestoreDeferredSourcesAfterSceneInitialized(',
    'previousInitializer.OverridenLocationPrefab',
    'ResolveSoulTargetSpawnTemplate(',
    'GetOrInitializeHighSoulVigorPool(',
    'TryDrainHighSoulVigorPool(',
    'TryRaiseMiniboss(',
    'TryGetMinibossSummonCapacity(',
    'ApplyHighSoulBindingAttempt(',
    'GetMinibossReanimationSoulVigorCost(',
    'source.MoveToDomain(Domain.Gameplay)',
    'EndMinibossService(',
    'TryConsumeHighSoulServicePortion(',
    'TryGetExistingHighSoulVigorPool(',
    'IsMatchingPersistentMinibossSource(',
    'new SearchAction(items, true)',
    'RollbackHighSoulDrain(',
    'IsReanimatedMiniboss(',
    'IsReanimatedServant(',
    'No soul remains to bind.',
    ' Soul Vigor"')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Miniboss and boss lifecycle contract is missing: $required"
    }
}
if (($runtimeSource -notmatch '(?s)TryResolveHighSoulCorpse\(.*?npcTemplate\.IsSummon.*?NpcType\.MiniBoss.*?NpcType\.Boss.*?corpse\.Faction\.IsHostileTo\(hero\.Faction\).*?TemporaryDeathAttachment.*?KillPreventionAttachment.*?NpcKillOnSpawnAttachment') -or
    ($runtimeSource -notmatch '(?s)BeforeReplaceHighSoulCorpse\(.*?IsPotentialSavedHighSoulCorpse\(location\).*?hero == null.*?sourceCorpse\.Faction == null.*?__result = false;.*?GetOrInitializeHighSoulVigorPool') -or
    ($runtimeSource -notmatch '(?s)CanReanimateMiniboss\(.*?canSafelySimplify.*?SceneLocationInitializer.*?RuntimeLocationInitializer.*?NpcDummy.*?ReanimatableMinibossTemplateGuids.*?TryResolveCanonicalPersistentSpawnTemplate.*?RepetitiveNpcAttachment.*?NpcTemplate\.GUID') -or
    ($runtimeSource -notmatch '(?s)CanSafelySimplifyHighSoulCorpse\(.*?RepetitiveNpcAttachment.*?CrimeReactionArchetype\.Guard.*?GameplayUniqueLocation.*?NpcPresence.*?NpcAlly.*?Shop.*?DialogueAction.*?StoryOnDeath.*?HasProtectedSoulTargetAttachment') -or
    ($runtimeSource -notmatch '(?s)TryDrainHighSoulCorpse\(.*?TryDrainHighSoulVigorPool.*?remainingAfter == 0.*?CanSafelySimplify.*?TryCreateRemains.*?RollbackHighSoulDrain') -or
    ($runtimeSource -notmatch '(?s)EndMinibossService\(.*?TryConsumeHighSoulServicePortion.*?TryGetExistingHighSoulVigorPool.*?MoveAndRotateTo\(.*?TryReturnMinibossSourceToCurrentScene\(.*?remaining <= 0.*?TryCreateRemains.*?serviceCommitted.*?RollbackHighSoulDrain.*?SetInteractability\(record\.SourceInteractability\)') -or
    ($runtimeSource -notmatch '(?s)TryRaiseCorpse\(.*?record\.IsMiniboss.*?0\.50f.*?retainedHealthFraction = record\.IsMiniboss.*?1\.0f.*?CommitHighSoulBinding') -or
    ($runtimeSource -notmatch '(?s)AfterGetSearchActionFrame\(.*?_heavyCastActive.*?IsSoulSalvageEquipped\(\).*?GetOrInitializeHighSoulVigorPool.*?new InfoFrame\(.*?Soul Vigor')) {
    throw 'The high-soul resolver, UI, depletion transaction, or restricted miniboss combat lifecycle regressed.'
}
if (($runtimeSource -notmatch '(?s)if \(!isMiniboss\)\s*\{\s*TriggerRuntimeCorpseVisualEvent\(source, "OnResurrectStarted"\)') -or
    ($runtimeSource -notmatch '(?s)RestoreSourceCorpse\(.*?if \(!record\.IsMiniboss\).*?TriggerRuntimeCorpseVisualEvent\(record\.SourceCorpse, "OnDeath"\)') -or
    ($runtimeSource -notmatch '(?s)TryRehydrateRaisedServant\(.*?templateIsMiniboss != snapshot\.IsMiniboss.*?IsMatchingPersistentMinibossSource\(') -or
    ($runtimeSource -notmatch '(?s)IsMatchingPersistentMinibossSource\(.*?CanReanimateMiniboss\(.*?GetHighSoulCorpseFingerprint\(.*?snapshot\.HighSoulFingerprint.*?snapshot\.HighSoulExtractionValue != expectedExtraction.*?TryGetExistingHighSoulVigorPool\(.*?snapshot\.HighSoulServiceCycle')) {
    throw 'Scene-authored miniboss sources are not protected from synthetic events and forged persistence identities.'
}
if (($runtimeSource -notmatch '(?s)TryPrepareMinibossSourceForService\(.*?SceneLocationInitializer.*?RuntimeLocationData.*?RuntimeLocationInitializer.*?PrepareSpec\(source\).*?LocationInitializerField\.SetValue\(source, runtimeInitializer\).*?discarded\.Set\(sourceId, true\).*?source\.MoveToDomain\(Domain\.Gameplay\)') -or
    ($runtimeSource -notmatch '(?s)catch \(Exception exception\).*?source\.MoveToDomain\(previousDomain\).*?LocationInitializerField\.SetValue\(source, previousInitializer\).*?discarded\.(?:Set|Remove)') -or
    ($runtimeSource -notmatch '(?s)TryRaiseCorpse\(.*?TryPrepareMinibossSourceForService\(.*?SetInteractability\(LocationInteractability\.Hidden\).*?SavePersistentReanimation\(') -or
    ($runtimeSource -notmatch '(?s)TryRehydrateRaisedServant\(.*?IsMatchingPersistentMinibossSource\(.*?source\.CurrentDomain != Domain\.Gameplay') -or
    ($runtimeSource -notmatch '(?s)IsMatchingPersistentMinibossSource\(.*?RuntimeLocationInitializer.*?IsAuthoredSourceDiscarded.*?CanReanimateMiniboss\(') -or
    ($runtimeSource -notmatch '(?s)TryReturnMinibossSourceToCurrentScene\(.*?RuntimeLocationInitializer.*?IsAuthoredSourceDiscarded.*?sceneService\.ActiveDomain.*?source\.MoveToDomain\(currentScene\)') -or
    ($runtimeSource -notmatch 'new SearchAction\(items, true\)')) {
    throw 'Miniboss source promotion, native save persistence, scene return, or terminal cleanup regressed.'
}
if (($runtimeSource -notmatch '(?s)RuntimeLocationData\(.*?source\.SpecInitialScale,\s*previousInitializer\.OverridenLocationPrefab,\s*source\.DisplayName') -or
    ($runtimeSource -notmatch '(?s)ScheduleDeferredSourceRestoration\(.*?WriteDeferredSourceInt\(record\.SourceId, "restore", 1\);\s*EnsureRaisedPersistenceSceneListener\(\)') -or
    ($runtimeSource -notmatch '(?s)AfterSceneFullyInitializedForRaisedPersistence\(.*?RestoreDeferredSourcesAfterSceneInitialized\(\).*?WriteRaisedPersistencePayload\(\)') -or
    ($runtimeSource -notmatch '(?s)TryRestoreDeferredSource\(.*?!sceneSafeVisual.*?TryReturnMinibossSourceToCurrentScene\(.*?SetInteractability.*?WriteDeferredSourceInt\(sourceId, "restore", 0\)') -or
    ($runtimeSource -notmatch '(?s)RestoreLoadedRaisedSource\(.*?snapshot\.IsMiniboss\s*&& !TryReturnMinibossSourceToCurrentScene\(.*?source\.SetInteractability') -or
    ($runtimeSource -notmatch '(?s)RestoreSourceCorpse\(.*?record\.IsMiniboss\s*&& !TryReturnMinibossSourceToCurrentScene\(.*?record\.SourceCorpse\.SetInteractability')) {
    throw 'Deferred miniboss source recovery can expose an unsafe source or lose its scene-ready retry.'
}
foreach ($required in @(
    'attachment is RepetitiveNpcAttachment',
    'case NpcType.Critter:',
    'case NpcType.Trash:',
    'case NpcType.Normal:',
    'case NpcType.Elite:',
    'CrimeReactionArchetype.Guard',
    'CrimeReactionArchetype.Defender',
    'CrimeReactionArchetype.Vigilante',
    'HasProtectedSoulTargetAttachment(candidate.Spec)',
    'candidate.Spec?.GetComponent<LocationTemplate>()',
    'TriggerRuntimeCorpseVisualEvent(source, "OnResurrectStarted")',
    'source.Initializer is RuntimeLocationInitializer')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Scene-safe Soul Rend identity contract is missing: $required"
    }
}
foreach ($required in @(
    'RaisedPersistenceLegacyVersion = 1',
    'RaisedPersistencePreviousVersion = 2',
    'RaisedPersistenceVersion = 3',
    'RaisedPersistencePayloadPrefix = "SASRP2:"',
    'RaisedPersistenceMaximumRecords = 256',
    'public sealed class RaisedPersistencePayload',
    'public sealed class RaisedPersistenceSnapshot',
    'public RaisedPersistenceSnapshot[] Records',
    'public bool RecoveryManaInitialized;',
    'public float RecoveryOriginalMana;',
    'public float RecoveryRemainingMana;',
    'public int RecoveryOriginalSoulVigor;',
    'public int EmpowermentSoulVigorInvestment;',
    'public int RecoveredSoulforgedRankMask;',
    'persistent_raised.payload',
    'GameplayMemory.OnBeforeSerialize',
    'GameplayMemory.OnAfterDeserialize',
    'SceneLifetimeEvents.Events.AfterSceneFullyInitialized',
    'data.IsMainScene',
    'IsValidRaisedPersistenceSnapshot(snapshot)',
    'IsEligiblePersistentSpawnTemplate(spawnTemplate)',
    'IsMatchingPersistentSource(source, spawnTemplate)',
    'IsPersistedInteractability(snapshot.SourceInteractability)',
    'RestoreLoadedRaisedSource(',
    'refundVigor: validSnapshot',
    'trustedSnapshot: validSnapshot',
    'sourceAlreadyServing',
    '((Model)raised).MarkedNotSaved = true;',
    'templates.Get<LocationTemplate>(snapshot.SpawnTemplateGuid)',
    'facts.Set(RaisedPersistencePayloadKey, serializedPayload)')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Raised-servant save-safety contract is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)namespace SoulAndService\s*\{\s*\[Serializable\]\s*public sealed class RaisedPersistencePayload.*?\[Serializable\]\s*public sealed class RaisedPersistenceSnapshot.*?internal static class SoulSalvageRuntime') {
    throw 'Raised-servant persistence must retain its public legacy JSON reader DTOs.'
}
if ($runtimeSource -notmatch '(?s)EnsureRaisedPersistenceSceneListener\(\).*?try.*?World\.EventSystem\.ListenTo.*?catch \(Exception exception\).*?LogRaisedPersistenceWarning' -or
    $runtimeSource -notmatch '(?s)TryResolveCanonicalPersistentSpawnTemplate\(.*?templates\.AllLoaded.*?templates\.Get<LocationTemplate>.*?GetAllOfType<LocationTemplate>.*?matches\.Count == 1' -or
    $runtimeSource -notmatch '(?s)WriteRaisedPersistencePayload\(\s*GameplayMemory memory = null\).*?GetPersistenceFacts\(memory\).*?foreach \(ReanimationRecord record in Reanimations\.Values\).*?CaptureRaisedPersistenceSnapshot\(record, preserveHost\).*?if \(!IsValidRaisedPersistenceSnapshot\(snapshot\)\).*?SerializeRaisedPersistencePayload\(snapshots\).*?DeserializeRaisedPersistencePayload\(.*?facts\.Set\(RaisedPersistencePayloadKey, serializedPayload\)' -or
    $runtimeSource -notmatch '(?s)CaptureRaisedPersistenceSnapshot\(.*?bool preserveHost.*?Phase = RaisedPersistencePending.*?if \(!preserveHost.*?return snapshot;.*?snapshot\.Phase = RaisedPersistenceActive' -or
    $runtimeSource -notmatch '(?s)IsValidRaisedPersistenceSnapshot\(.*?snapshot\.Phase == RaisedPersistencePending.*?return true;.*?SpawnTemplateGuid' -or
    $runtimeSource -notmatch '(?s)RestoreLoadedRaisedSource\(.*?trustedSnapshot.*?LocationInteractability\.Active.*?try.*?source\.SetInteractability\(interactability\).*?catch \(Exception exception\).*?WriteDeferredSourceString.*?WriteDeferredSourceInt.*?catch \(Exception exception\).*?RestoreSoulVigor.*?catch \(Exception exception\)') {
    throw 'Raised-servant persistence is not guarded, canonical, and atomically written.'
}
foreach ($required in @(
    'class RaisedPersistenceRecoveryDiagnostics',
    'snapshot write: version=',
    'load: snapshot=missing',
    'load: snapshot=loaded',
    'rejected: source=',
    'recovery ',
    'plugin.LogDiagnostic("Raised persistence " + message)',
    'attempted=',
    'scheduled=',
    'restored=',
    'rejected=',
    'sourcesRestored=',
    'deferred=',
    'pendingCallbacks=')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Raised-servant persistence diagnostics are missing: $required"
    }
}
foreach ($required in @(
    'format=binary-base64',
    'payloadChars=',
    'storedMatches=',
    'roundTripRecords=',
    'the raised-servant snapshot failed payload round-trip validation')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Raised-servant persistence validation diagnostics are missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)List<RaisedPersistenceSnapshot> records.*?records\.Add\(snapshot\).*?RaisedPersistenceSnapshot\[\] snapshots = records\.ToArray\(\).*?SerializeRaisedPersistencePayload\(snapshots\).*?DeserializeRaisedPersistencePayload\(.*?roundTrip\.Length != snapshots\.Length.*?AreEquivalentRaisedPersistenceSnapshots.*?facts\.Set\(RaisedPersistencePayloadKey, serializedPayload\).*?facts\.Get\(\s*RaisedPersistencePayloadKey,\s*string\.Empty\)') {
    throw 'Raised-servant snapshot writes are not round-trip validated before storage and verified through immediate ContextualFacts readback.'
}
if ($runtimeSource -notmatch '(?s)SerializeRaisedPersistencePayload\(.*?RaisedPersistenceMaximumRecords.*?BinaryWriter.*?RaisedPersistenceMagic.*?RaisedPersistenceVersion.*?Convert\.ToBase64String' -or
    $runtimeSource -notmatch '(?s)DeserializeRaisedPersistencePayload\(.*?RaisedPersistenceMaximumPayloadCharacters.*?Convert\.FromBase64String.*?RaisedPersistenceMagic.*?version != RaisedPersistencePreviousVersion.*?version != RaisedPersistenceVersion.*?recordCount > RaisedPersistenceMaximumRecords.*?version >= RaisedPersistenceVersion.*?RecoveryManaInitialized.*?stream\.Position != stream\.Length' -or
    $runtimeSource -notmatch '(?s)AfterGameplayMemoryDeserialize\(.*?StartsWith\(\s*RaisedPersistencePayloadPrefix.*?DeserializeRaisedPersistencePayload.*?JsonUtility\.FromJson<RaisedPersistencePayload>.*?RaisedPersistenceLegacyVersion') {
    throw 'Raised-servant persistence does not use the v3 bounded binary codec with safe v2 and legacy JSON reads.'
}
if (($runtimeSource -notmatch '(?s)BeforeGameplayMemorySerialize\(\s*GameplayMemory __instance\).*?WriteRaisedPersistencePayload\(__instance\)') -or
    ($runtimeSource -notmatch '(?s)AfterGameplayMemoryDeserialize\(\s*GameplayMemory __instance\).*?GetPersistenceFacts\(__instance\).*?snapshot=missing.*?snapshot=loaded.*?recoveryScheduled=true') -or
    ($runtimeSource -notmatch '(?s)GetPersistenceFacts\(\s*GameplayMemory memory = null\).*?if \(memory == null\).*?World\.Services\.TryGet<GameplayMemory>\(\).*?memory\.Context\("SoulAndService"\)') -or
    ($runtimeSource -notmatch '(?s)AfterSceneFullyInitializedForRaisedPersistence\(.*?RaisedPersistenceRecoveryDiagnostics recovery.*?recovery\.Attempted\+\+.*?LogRaisedPersistenceRejection.*?recovery\.ScenePassComplete = true.*?LogRaisedPersistenceRecoverySummary') -or
    ($runtimeSource -notmatch '(?s)TryRehydrateRaisedServant\(.*?out string rejectionReason.*?plugin is unavailable or disabled.*?Persistent Servants is disabled.*?location templates are not ready.*?spawn template is not persistence-safe.*?source corpse no longer matches its spawn template') -or
    ($runtimeSource -notmatch '(?s)CompleteRehydratedRaisedServant\(.*?RaisedPersistenceRecoveryDiagnostics diagnostics.*?record\.ServiceInitialized = true.*?diagnostics\.Restored\+\+.*?finally.*?diagnostics\.PendingCallbacks') -or
    ($runtimeSource -notmatch '(?s)RestoreLoadedRaisedSource\(.*?RaisedPersistenceRecoveryDiagnostics diagnostics.*?diagnostics\.SourcesRestored\+\+.*?diagnostics\.Deferred\+\+')) {
    throw 'Raised-servant diagnostics no longer distinguish snapshot load, synchronous rejection, scheduled initialization, completed restoration, and deferred source recovery.'
}
if ($runtimeSource -notmatch '(?s)AfterSceneFullyInitializedForRaisedPersistence\(.*?List<Vector3> restoredPlacementReservations.*?int restoredPlacementSlot = 0.*?int placementSlot = restoredPlacementSlot\+\+.*?TryRehydrateRaisedServant\(\s*snapshot,\s*recovery,\s*placementSlot,\s*restoredPlacementReservations' -or
    $runtimeSource -notmatch '(?s)TryRehydrateRaisedServant\(.*?Vector3 position = hero\.Coords.*?Vector3\.forward.*?try.*?TryReserveRestoredPlacement\(\s*hero,\s*heroNode,\s*placementSlot,\s*restoredPlacementReservations,\s*out Vector3 reservedPosition\).*?position = reservedPosition.*?catch \(Exception exception\).*?using the safe recovery fallback.*?SpawnLocation\(position, hero\.Rotation\)' -or
    $runtimeSource -notmatch '(?s)CompleteRehydratedRaisedServant\(.*?ReanimationGlyphRuntime\.Attach.*?SavePersistentReanimation.*?try.*?SpawnNecromanticSummonVfx\(npc\).*?catch \(Exception exception\).*?Could not play a restored servant''s arrival effect.*?diagnostics\.Restored\+\+') {
    throw 'Restored servants do not reserve shared rear-horseshoe arrival slots and play the load-arrival VFX without risking recovery.'
}
if ($runtimeSource -match '(?s)TryRehydrateRaisedServant\(.*?RecallHost\(') {
    throw 'Raised-servant loading must not invoke the state-clearing Recall command.'
}
if ($runtimeSource -match '(?s)SetSummonSavedState\(.*?MarkedNotSaved = !persistent' -or
    $runtimeSource -match 'SourceCorpse\)\.MarkedNotSaved = false' -or
    $runtimeSource -match 'source\)\.MarkedNotSaved = false') {
    throw 'Raised-servant persistence still mutates native source or ordinary-summon save ownership.'
}
foreach ($removed in @(
    'HasProtectedRuntimeIdentity',
    'Unity.VisualScripting.ScriptMachine',
    'string[] protectedTerms')) {
    if ($runtimeSource.Contains($removed)) {
        throw "Soul Rend retains the blanket runtime or string-identity gate: $removed"
    }
}
if ($runtimeSource -notmatch '(?s)TryUseLightCast\(.*?TryFindEligibleCorpse\(\s*hero,\s*needsSpawnTemplate: false' -or
    $runtimeSource -notmatch '(?s)TryUseHeavyCast\(.*?TryFindEligibleCorpse\(\s*hero,\s*needsSpawnTemplate: true.*?TryRaiseCorpse\(\s*sourceItem,\s*source,\s*spawnTemplate' -or
    $runtimeSource -notmatch '(?s)TryFindEligibleLivingTarget\(\s*hero,\s*needsSpawnTemplate: true.*?out livingSpawnTemplate.*?TryClaimLivingTarget\(.*?livingSpawnTemplate' -or
    $runtimeSource -notmatch '(?s)TryRaiseCorpse\(.*?LocationTemplate spawnTemplate.*?raised = spawnTemplate\.SpawnLocation') {
    throw 'Harvest/Rend and reanimation/Claim do not use separate spawn-template requirements.'
}
if ($runtimeSource -notmatch '(?s)TriggerRuntimeCorpseVisualEvent\(\s*Location source,\s*string eventName\).*?source\.Initializer is RuntimeLocationInitializer.*?source\.TriggerVisualScriptingEvent\(eventName\)' -or
    $runtimeSource -notmatch 'TriggerRuntimeCorpseVisualEvent\(record\.SourceCorpse, "OnDeath"\)') {
    throw 'Scene-authored source corpses can still receive synthetic resurrection or death Visual Scripting events.'
}
if (($runtimeSource -notmatch '(?s)IsSoulRendAssistSurface\(.*?hit\.collider == null.*?candidate == null \|\| candidate\.HasBeenDiscarded.*?TryGetElement<Corpse>\(\).*?TryGetElement<NpcElement>\(\)') -or
    ($runtimeSource -match 'GroundTargetMinimumNormalY') -or
    ($runtimeSource -notmatch '(?s)TryFindNearestEligibleCorpse\(.*?Physics\.OverlapSphereNonAlloc\(.*?SoulRendAssistRadius.*?SoulRendAssistColliderBuffer.*?TryValidateEligibleCorpse\(.*?collider\.ClosestPoint\(impactPoint\).*?Vector3\.Dot\(offset, surfaceNormal\).*?nearestDistanceSqr.*?source = candidate') -or
    ($runtimeSource -notmatch '(?s)TryFindEligibleCorpse\(.*?IsSoulRendAssistSurface\(hit, candidate\).*?TryFindNearestEligibleCorpse\(.*?out source') -or
    ($runtimeSource -notmatch '(?s)TryFindFocusedSoulTarget\(.*?IsSoulRendAssistSurface\(hit, candidate\).*?TryFindNearestEligibleCorpse\(.*?out location')) {
    throw 'Heavy Soul Rend does not safely assist to the nearest eligible corpse within 0.4 meters of irrelevant surface hits.'
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

if ($runtimeSource -notmatch '(?s)source\.SetInteractability\(LocationInteractability\.Hidden\);.*?PrefabPool\.InstantiateAndReturn\(\s*new ShareableARAssetReference\(SkeletonSummonVfxKey\),\s*source\.Coords,\s*source\.Rotation\)\.Forget\(\);') {
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
        '2-4',
        '3 minutes',
        '20 minutes',
        '50 minutes',
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
    'progress += GetBindingIncrement(bindingFingerprint, attempt)',
    '+ (0.75f * power)',
    'power >= 199.999f',
    'private const string SoulVigorKey = "soul_vigor.total"',
    'minimum = 24;',
    'maximum = 36;')) {
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
    'ShowSoulClaimFeedback(')) {
    if (!$progressionSource.Contains($required)) {
        throw "Soul Claim feedback contract is missing: $required"
    }
}
foreach ($required in @(
    'ShowSummonCreated(',
    'ShowInsufficientSoulVigor(',
    '"soul-summoning"',
    '" summoned: -"',
    '" reanimated"',
    '"soul-vigor-required"',
    '"Warning"')) {
    if (!$progressionSource.Contains($required)) {
        throw "Summon Soul Vigor GFT presentation is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)committedVigor > 0.*?ShowSummonCreated\(.*?committedVigor' -or
    $runtimeSource -notmatch '(?s)ShowResurrection\(\s*record\.SourceDisplayName,\s*record\.QualityTier,\s*record\.InvestedSoulVigor\)') {
    throw 'Successful summon and reanimation GFT messages do not wait for the durable committed spend.'
}

$heavyCastFeedbackMethod = [regex]::Match(
    $pluginSource,
    '(?s)internal void ShowSoulSalvageHeavyCastFeedback\(\s*string eventId,\s*string message,\s*bool warning = false\).*?(?=\r?\n\s*internal void )')
if (!$heavyCastFeedbackMethod.Success -or
    $heavyCastFeedbackMethod.Value -match 'Diagnostics|ShowGrailFloatingTextDiagnostics|TryShowDiagnosticNotification' -or
    $heavyCastFeedbackMethod.Value -notmatch 'TryShowEventNotification\(') {
    throw 'Ordinary Soul Rend heavy-cast feedback is not an unconditional normal Grail Floating Text event.'
}
$heavyCastDiagnosticMethod = [regex]::Match(
    $pluginSource,
    '(?s)internal void ShowSoulSalvageHeavyCastDiagnostic\(\s*string diagnosticGroup,\s*string message\).*?(?=\r?\n\s*internal void )')
if (!$heavyCastDiagnosticMethod.Success -or
    $heavyCastDiagnosticMethod.Value -notmatch '(?s)Diagnostics == null.*?!Diagnostics\.Value.*?ShowGrailFloatingTextDiagnostics == null.*?!ShowGrailFloatingTextDiagnostics\.Value' -or
    $heavyCastDiagnosticMethod.Value -notmatch '(?s)"soul-rend-"\s*\+\s*diagnosticGroup\s*\+\s*"-diagnostic".*?TryShowDiagnosticNotification\(.*?diagnosticId.*?diagnosticId') {
    throw 'Detailed Soul Rend diagnostics are not gated with their own System collapse metadata.'
}
foreach ($ordinaryFeedback in @(
    'ShowSoulSalvageHeavyCastFeedback\(\s*"[^"]+".{0,700}?"Soul Rend: servant limit full',
    'ShowSoulSalvageHeavyCastFeedback\(\s*"[^"]+".{0,700}?"Raise All: servant limit full',
    'ShowSoulSalvageHeavyCastFeedback\(\s*"[^"]+".{0,700}?"Soul Rend: reanimation failed')) {
    if ($runtimeSource -notmatch "(?s)$ordinaryFeedback") {
        throw "Ordinary actionable Soul Rend feedback is not routed independently of diagnostics: $ordinaryFeedback"
    }
}
foreach ($targetingDiagnostic in @(
    'ShowSoulSalvageHeavyCastDiagnostic\(\s*"targeting".{0,700}?"Soul Rend: no eligible target',
    'ShowSoulSalvageHeavyCastDiagnostic\(\s*"targeting".{0,700}?"Soul Rend: no eligible corpse')) {
    if ($runtimeSource -notmatch "(?s)$targetingDiagnostic") {
        throw "Rejected heavy Soul Rend targeting is not a separately collapsed System diagnostic: $targetingDiagnostic"
    }
}
foreach ($gatedSummary in @(
    'ShowSoulSalvageHeavyCastDiagnostic\(\s*"binding",\s*outcome\)',
    'ShowSoulSalvageHeavyCastDiagnostic\(\s*"lifecycle".{0,700}?service ended; remains were left behind',
    'ShowSoulSalvageHeavyCastDiagnostic\(\s*"lifecycle".{0,700}?service ended; source corpse restored')) {
    if ($runtimeSource -notmatch "(?s)$gatedSummary") {
        throw "Detailed Soul Rend success or lifecycle summary is not retained behind both diagnostics controls: $gatedSummary"
    }
}
if ($runtimeSource -match '(?s)ShowSoulSalvageHeavyCastDiagnostic\(.{0,500}?ShowSoulSalvageHeavyCastFeedback\(\s*"(?:soul-rend-servant-limit|raise-all-servant-limit|soul-rend-reanimation-failed)') {
    throw 'A Soul Rend diagnostic can still precede and throttle ordinary capacity or reanimation-failure feedback.'
}
$insufficientVigorChecks = [regex]::Matches(
    $runtimeSource,
    '(?s)if \(SoulProgressionRuntime\.GetSoulVigor\(\) \+ 0\.001f < vigorCost\)\s*\{(?<body>.*?)\n\s*\}')
if ($insufficientVigorChecks.Count -lt 2 -or
    @($insufficientVigorChecks | Where-Object {
        $_.Groups['body'].Value -notmatch 'ShowInsufficientSoulVigor\(vigorCost\)' -or
        $_.Groups['body'].Value -match 'ShowSoulSalvageHeavyCast(?:Diagnostic|Feedback)\('
    }).Count -gt 0) {
    throw 'An insufficient-Vigor heavy-cast branch can still replace its canonical normal feedback with a same-source diagnostic or duplicate event.'
}
$spendFailure = [regex]::Match(
    $runtimeSource,
    '(?s)if \(!SoulProgressionRuntime\.TrySpendSoulVigor\(.*?out int vigorAfter\)\)\s*\{(?<body>.*?)\n\s*\}')
if (!$spendFailure.Success -or
    $spendFailure.Groups['body'].Value -notmatch 'ShowInsufficientSoulVigor\(vigorCost\)' -or
    $spendFailure.Groups['body'].Value -match 'ShowSoulSalvageHeavyCast(?:Diagnostic|Feedback)\(') {
    throw 'The failed Soul Vigor spend can still throttle away its canonical normal insufficient-Vigor feedback.'
}

foreach ($required in @(
    '0c7757225700cda4db246fd6bc3bc59f',
    'CombinedEffectState',
    'ReanimationEffectSettings',
    'SummonRuntime.GetEmpowermentCombatMultiplier(',
    'effectState.ConfigSignature != settings.Signature',
    '"Smoke-Count"',
    '"Coilor-Smoke"',
    '"Spawn Rate"',
    '"Size Min/Max"',
    'ResolveConfiguredColor(',
    'snapshot?.Restore(effect)',
    'pooled?.Return()')) {
    if (!$glyphSource.Contains($required)) {
        throw "Reanimation glyph runtime is missing: $required"
    }
}
foreach ($required in @(
    'internal ConfigEntry<bool> ReanimationVfxEnabled',
    'internal ConfigEntry<string> ReanimationAuraArcColor',
    'internal ConfigEntry<string> ReanimationAuraGlowColor',
    'internal ConfigEntry<string> ReanimationAuraHazeColor',
    'internal ConfigEntry<bool> ReanimationUseCustomFullPotentialColor',
    'internal ConfigEntry<string> ReanimationFullPotentialColor',
    'internal ConfigEntry<int> ReanimationAuraParticleAmount',
    'internal ConfigEntry<float> ReanimationAuraIntensity',
    'internal ConfigEntry<float> ReanimationFullPotentialBrightness',
    'internal ConfigEntry<float> ReanimationElectricityOpacity',
    'internal ConfigEntry<float> ReanimationSmokeOpacity',
    'internal ConfigEntry<float> ReanimationAuraScale',
    'internal ConfigEntry<bool> ReanimationDynamicParticleBudget',
    '"Reanimation VFX"',
    '"DynamicParticleBudget"',
    '"AuraArcColor"',
    '"AuraGlowColor"',
    '"AuraHazeColor"',
    '"UseCustomFullPotentialColor"',
    '"FullPotentialColor"',
    '"AuraParticleAmount"',
    '"AuraIntensity"',
    '"FullPotentialBrightness"',
    '"ElectricityOpacity"',
    '"SmokeOpacity"',
    '"AuraScale"')) {
    if (!$pluginSource.Contains($required)) {
        throw "Reanimation VFX configuration is missing: $required"
    }
}
if ($pluginSource -notmatch '(?s)ReanimationVfxEnabled = BindOrdered\(.*?true,.*?ReanimationAuraArcColor = BindOrdered\(.*?"#179B43",.*?ReanimationAuraGlowColor = BindOrdered\(.*?"#78C98F",.*?ReanimationAuraHazeColor = BindOrdered\(.*?"#123F2D",.*?ReanimationUseCustomFullPotentialColor = BindOrdered\(.*?"UseCustomFullPotentialColor",\s*false,.*?ReanimationFullPotentialColor = BindOrdered\(.*?"#FFFFFF",.*?ReanimationAuraParticleAmount = BindOrdered\(.*?75,.*?AcceptableValueRange<int>\(0, 200\).*?ReanimationAuraIntensity = BindOrdered\(.*?5\.0f,.*?AcceptableValueRange<float>\(0\.0f, 20\.0f\).*?ReanimationFullPotentialBrightness = BindOrdered\(.*?20\.0f,.*?AcceptableValueRange<float>\(0\.0f, 20\.0f\).*?ReanimationElectricityOpacity = BindOrdered\(.*?1\.0f,.*?AcceptableValueRange<float>\(0\.0f, 1\.0f\).*?ReanimationSmokeOpacity = BindOrdered\(.*?0\.5f,.*?AcceptableValueRange<float>\(0\.0f, 1\.0f\).*?ReanimationAuraScale = BindOrdered\(.*?1\.0f,.*?AcceptableValueRange<float>\(0\.25f, 2\.0f\).*?ReanimationDynamicParticleBudget = BindOrdered\(.*?true' -or
    $pluginSource -notmatch '(?s)ConfigRecoveryKeepCurrentDefaultRule\(\s*20,\s*"Reanimation VFX",\s*"AuraIntensity"' -or
    $pluginSource -notmatch '(?s)ConfigRecoveryKeepCurrentDefaultRule\(\s*26,\s*"Reanimation VFX",\s*"AuraIntensity"') {
    throw 'Reanimation VFX defaults or schema-20/schema-26 brightness recovery rules are incorrect.'
}
if ($glyphSource -notmatch '(?s)ShockAuraVfxKey =\s*"0c7757225700cda4db246fd6bc3bc59f"' -or
    $glyphSource -match 'WeakAuraVfxKey|ArcaneAegisLightningAuraVfxKey|ReanimationAuraStyle' -or
    $glyphSource -match '5f8c0d5d62877c047a9eb4fb79523235|BodyAuraVfxKey' -or
    $glyphSource -notmatch '(?s)class GlyphState.*?readonly CombinedEffectState Aura.*?RefreshAura\(.*?CombinedEffectState effectState = state\.Aura.*?effectState\.AssetKey.*?ReleaseEffect\(effectState\).*?LoadAndConfigureAura\(' -or
    $glyphSource -notmatch '(?s)LoadAndConfigureAura\(.*?VFXBodyMarkerBinder.*?SetBody\(state\.Npc\.VFXBodyMarker\).*?DisableQualityControllers\(.*?true.*?ConfigureAuraEffect' -or
    $glyphSource -notmatch '(?s)ConfigureAuraEffect\(.*?ElectricityOpacity > 0\.0f.*?GetAuraParticleCount\(150, settings\.AuraParticleAmount\).*?SmokeOpacity > 0\.0f.*?GetAuraParticleCount\(100, settings\.AuraParticleAmount\).*?CreateAuraGradient\(\s*settings\.AuraArcColor,\s*settings\.AuraGlowColor,\s*settings\.AuraIntensity\).*?CreateAuraHazeGradient\(\s*settings\.AuraHazeColor,\s*settings\.AuraIntensity\).*?SetInt\(effect, "Count", electricParticleCount\).*?"Smoke-Count".*?SetFloat\(effect, "Spawn Rate", 0\.0f\).*?"Fire Alpha",\s*0\.10f \* settings\.ElectricityOpacity.*?SetFloat\(effect, "Smoke-Alpha", settings\.SmokeOpacity\).*?"Size Min/Max".*?"Fire Size Min Max".*?"Smoke Size".*?"Color-Fire".*?"Coilor-Smoke"' -or
    $glyphSource -notmatch '(?s)DisableQualityControllers\(.*?disableAudioAndLights.*?behaviour is Light.*?behaviour is StudioEventEmitter.*?audioEmitter\.Stop\(true\).*?behaviour\.enabled = false') {
    throw 'The configurable body-bound Reanimation VFX is not configured through the pooled servant VFX lifecycle.'
}
if (!$glyphSource.Contains('new Color(0.09019608f, 0.60784316f, 0.26274510f)') -or
    !$glyphSource.Contains('new Color(0.47058824f, 0.78823530f, 0.56078434f)') -or
    !$glyphSource.Contains('new Color(0.07058824f, 0.24705882f, 0.17647059f)') -or
    !$glyphSource.Contains('new Color(0.0f, 0.90196080f, 0.46274510f)')) {
    throw 'Invalid aura or smoke colors no longer fall back to their configured defaults.'
}
if ($glyphSource -notmatch '(?s)Color fullPotentialColor = DefaultFullPotentialColor;.*?ReanimationUseCustomFullPotentialColor\.Value.*?ResolveConfiguredColor\(.*?ReanimationFullPotentialColor\.Value,\s*DefaultFullPotentialColor\).*?BlendLinear\(\s*settings\.AuraArcColor,\s*fullPotentialColor,\s*potential\)') {
    throw 'The default emerald progression or opt-in custom full-potential endpoint is no longer applied to the aura layers.'
}
if (!$glyphSource.Contains('GetEffectSettings(') -or
    !$glyphSource.Contains('plugin.ReanimationVfxEnabled.Value') -or
    $glyphSource -notmatch '(?s)AuraParticleBudgetEquivalentServants / activeVisualCount.*?MinimumAuraParticleBudgetScale' -or
    $glyphSource -notmatch '(?s)settings\.Enabled = settings\.AuraParticleAmount > 0.*?AuraIntensity > 0\.0f.*?ElectricityOpacity > 0\.0f.*?SmokeOpacity > 0\.0f') {
    throw 'Reanimation VFX zero-cost disabling or dynamic particle budgeting is not wired into the runtime.'
}
if ($summonRuntimeSource -notmatch '(?s)GetEmpowermentCombatMultiplier\(string summonId\).*?EmpowermentStates\.TryGetValue\(summonId, out state\).*?state\.CombatMultiplier' -or
    $glyphSource -notmatch '(?s)settings\.AuraIntensity = Mathf\.Min\(\s*MaximumAuraIntensity,\s*Mathf\.Lerp\(\s*configuredIntensity,\s*configuredFullPotentialIntensity,\s*potential\)' -or
    $glyphSource -notmatch 'MaximumAuraIntensity = 20\.0f') {
    throw 'Soulforged and Empowered servant VFX brightness does not interpolate between its configured endpoints with a final 20.0 cap.'
}
foreach ($removed in @(
    'ReanimationFireEnabled',
    'ReanimationFireIntensity',
    'ReanimationFireParticleAmount',
    'ReanimationRunesEnabled',
    'ReanimationRuneIntensity',
    'ReanimationRuneParticleAmount',
    'ReanimationSmokeEnabled',
    'ReanimationSmokeColor',
    'ReanimationSmokeIntensity',
    'ReanimationSmokeParticleAmount',
    'ReanimationLightningEnabled',
    'ReanimationSparksEnabled',
    'ReanimationEnergyColor',
    'ReanimationSparksVfxKey',
    'TimedEffectState',
    'RefreshSparks',
    'LoadTimedEffect',
    'ConfigureTimedEffect',
    'ReleaseTimedEffect',
    'CreateEnergyGradient',
    'GetBodyBoundsSize',
    'BodySurfaceVfxKey',
    'GetOrCreateRuneAtlas',
    'GetOrCreateGlyphGradient',
    'GetOrCreateSmokeGradient',
    'GetOrCreateSparkleGradient',
    '"FireEnabled"',
    '"FireIntensity"',
    '"FireParticleAmount"',
    '"RunesEnabled"',
    '"RuneIntensity"',
    '"RuneParticleAmount"',
    '"SmokeEnabled"',
    '"SmokeColor"',
    '"SmokeIntensity"',
    '"SmokeParticleAmount"')) {
    if ($pluginSource.Contains($removed) -or $glyphSource.Contains($removed)) {
        throw "Removed reanimation body-effect surface remains: $removed"
    }
}
if ($glyphSource.Contains('22fdfa954ef8f9c4a8fb544e462874b8')) {
    throw 'Reanimation glyph runtime retains the invalid non-addressable asset GUID.'
}
if ($glyphSource.Contains('"Lifetime Min/Max"')) {
    throw 'Reanimation glyph runtime still enlarges the secondary dripping-particle lifetime.'
}
if ($summonRuntimeSource -notmatch '(?s)private static bool IsEffectOnlyRenderer\(Renderer renderer\).*?"ParticleSystemRenderer".*?"TrailRenderer".*?"LineRenderer".*?"VFXRenderer"' -or
    [regex]::Matches($summonRuntimeSource, 'IsEffectOnlyRenderer\(renderer\)').Count -lt 4) {
    throw 'Effect-only renderers are not excluded consistently from servant geometry and command bounds.'
}
foreach ($required in @(
    'ReanimationGlyphRuntime.Update()',
    'ReanimationGlyphRuntime.Attach(summonId, npc)',
    'ReanimationGlyphRuntime.Remove(summonId)',
    'ReanimationGlyphRuntime.Shutdown()',
    'VFXBodyMarker.Mesh.localBoundingSphere',
    'VFXBodyMarker.transform.TransformPoint(',
    'vfxPosition,',
    'Quaternion.identity).Forget()')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Reanimation glyph lifecycle is missing: $required"
    }
}

foreach ($required in @(
    'Version under test: 3.3.0',
    'exactly 2x',
    'raises a native hero summon',
    'simplified remains',
    'green-dark necromancer VFX',
    'SAS-SMOKE-30',
    'SAS-SMOKE-31',
    'SAS-SMOKE-39',
    'SAS-SMOKE-43',
    'SAS-SMOKE-56',
    '75% of its exact recorded cost',
    '3%-per-rank',
    '1.05x-1.20x',
    '1.30x',
    'Pale/System diagnostics',
    'Capacity limits',
    'two-handed grip immediately suppresses targeting',
    'Soul Vigor: X (Y)',
    '15 m',
    '50%/100%/200%',
    '5%/10%/15%/20%/25%/30%',
    'final 1-40% clamp')) {
    if (!$matrix.Contains($required)) {
        throw "Soul Salvage test matrix is missing: $required"
    }
}

Write-Host "Soul Salvage progression, mana, quality, upkeep, Empower, and native-summon contracts passed."
