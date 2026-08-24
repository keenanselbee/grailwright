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
    'ConfigSchemaVersion = 11',
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
if ($runtimeSource -notmatch '(?s)CalculateLightManaReturn\(.*?_lightOriginalMana\s*\* _lightHealthFraction\s*\* \(plugin\.SoulSalvageManaReturnPercent\.Value / 100\.0f\).*?Mathf\.Round\(Math\.Min\(rawReturn, _lightMaximumManaReturn\)\)') {
    throw "Light Soul Rend mana restoration is not Health-scaled, percentage-scaled, capped, and rounded to a whole actual return."
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
    'Corpses: Bind and reanimate; cost scales with soul quality.',
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
if ($runtimeSource -notmatch '(?s)TryHarvestCorpse\(\s*fingerprint,\s*tier,\s*quality01,\s*out harvestReceipt\).*?if \(!TryCreateRemains\(.*?RollbackCorpseHarvest\(harvestReceipt\)') {
    throw "Ordinary corpse harvest does not roll back Soul Vigor when remains creation fails."
}
if ($runtimeSource -notmatch '(?s)bool harvestReady = !record\.Sacrificed\s*\|\| SoulProgressionRuntime\.TryHarvestCorpse\(.*?if \(!simplified && harvestReceipt != null\)\s*\{\s*SoulProgressionRuntime\.RollbackCorpseHarvest\(harvestReceipt\);') {
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
    'text = "Empower Servant";',
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
    $runtimeSource -notmatch '(?s)GetPowerScaledSoulVigorCost\(int baseCost, float power\).*?Mathf\.Lerp\(2\.0f, 1\.0f, safePower / 100\.0f\).*?Mathf\.Lerp\(\s*1\.0f,\s*0\.5f,\s*\(safePower - 100\.0f\) / 100\.0f\).*?Mathf\.CeilToInt\(Math\.Max\(0, baseCost\) \* multiplier\)') {
    throw "Summon and reanimation costs do not share the 2x/1x/0.5x Power curve."
}
if ($runtimeSource -notmatch '(?s)GetOrdinarySummonSoulVigorCost\(int summonTier, float power\).*?Math\.Max\(1, summonTier\) \* OrdinarySummonVigorCostPerTier.*?power' -or
    $runtimeSource -notmatch '(?s)OnSummonInitialized\(NpcHeroSummon summon\).*?GetOrdinarySummonTier\(summon\.Item\).*?GetOrdinarySummonSoulVigorCost\(.*?TrySpendSoulVigor\(\s*vigorCost') {
    throw "Ordinary summons do not price their authored tier through the shared Power curve before spending."
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
    $runtimeSource -notmatch '(?s)sourceCorpse = GetBloodMagicCorpseIdentity\(record\.SourceCorpse\).*?servantNpc = record\.RaisedNpc' -or
    $runtimeSource -notmatch '(?s)GetBloodExsanguinationSeverity\(object sourceCorpse\).*?sourceCorpse as Location.*?GetBloodMagicCorpseIdentity\(sourceLocation\)') {
    throw "Blood Magic interop does not normalize raised source locations to their registered Corpse elements."
}
if ($pluginSource -notmatch 'public const int ApiVersion = 8;' -or
    $pluginSource -notmatch 'public static bool TryMaterializeOwnedBloodServantCorpseForAbhartach\(') {
    throw "Soul and Service API 8 does not publish the Abhartach servant-corpse bridge."
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
if ($runtimeSource -notmatch '(?s)if \(record\.BloodRitualExecuted\).*?ExecutedServantRemains\[\(\(Model\)record\.RaisedLocation\)\.ID\] = record;') {
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
    'GroundTargetRadius = 0.4f',
    'GroundTargetColliderBufferSize = 64',
    'Physics.OverlapSphereNonAlloc(',
    'IsGroundSoulRendSurface(hit, candidate)',
    'TryFindNearestEligibleCorpse(',
    'TryValidateEligibleCorpse(hero, candidate, out rejection)',
    'Soul Rend could not finish initializing a raised servant:',
    'reanimation failed - source corpse restored')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Salvage safety or focused-target contract is missing: $required"
    }
}
if (($runtimeSource -notmatch '(?s)IsGroundSoulRendSurface\(.*?GroundTargetMinimumNormalY.*?TryGetElement<Corpse>\(\).*?TryGetElement<NpcElement>\(\)') -or
    ($runtimeSource -notmatch '(?s)TryFindNearestEligibleCorpse\(.*?Physics\.OverlapSphereNonAlloc\(.*?GroundTargetRadius.*?GroundTargetColliderBuffer.*?TryValidateEligibleCorpse\(.*?collider\.ClosestPoint\(impactPoint\).*?Vector3\.Dot\(offset, surfaceNormal\).*?nearestDistanceSqr.*?source = candidate') -or
    ($runtimeSource -notmatch '(?s)TryFindEligibleCorpse\(.*?IsGroundSoulRendSurface\(hit, candidate\).*?TryFindNearestEligibleCorpse\(.*?out source') -or
    ($runtimeSource -notmatch '(?s)TryFindFocusedSoulTarget\(.*?IsGroundSoulRendSurface\(hit, candidate\).*?TryFindNearestEligibleCorpse\(.*?out location')) {
    throw 'Heavy Soul Rend does not safely select the nearest eligible corpse within its 0.4-meter ground fallback.'
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
        '2-4',
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
    'GetSoulClaimFailureMessage()',
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

foreach ($required in @(
    '22fdfa954ef8f9c4ea62779e08eedfbf',
    'VFXBodyMarker.BoundsSize',
    'CombinedEffectState',
    'CombinedEffectSettings',
    'GetEffectSettings(CountActiveVisualStates())',
    'effectState.ConfigSignature != settings.Signature',
    '"Fire Texture"',
    '"Smoke-Count"',
    '"Coilor-Smoke"',
    '"Spawn Rate"',
    '"Fire Lifetime", new Vector2(1.5f, 2.5f)',
    '"Size Min/Max"',
    'new Color(1.08f, 15.75f, 5.2875f)',
    'new Color(0.025f, 0.15f, 0.055f)',
    'new GradientColorKey(Color.black, 0.0f)',
    'new GradientColorKey(Color.black, 1.0f)',
    '"Initial Velocity_vector", Vector3.zero',
    'snapshot?.Restore(effect)',
    'pooled?.Return()')) {
    if (!$glyphSource.Contains($required)) {
        throw "Reanimation glyph runtime is missing: $required"
    }
}
foreach ($required in @(
    'internal ConfigEntry<bool> ReanimationVfxEnabled',
    'internal ConfigEntry<bool> ReanimationRunesEnabled',
    'internal ConfigEntry<float> ReanimationRuneIntensity',
    'internal ConfigEntry<int> ReanimationRuneParticleAmount',
    'internal ConfigEntry<bool> ReanimationSmokeEnabled',
    'internal ConfigEntry<float> ReanimationSmokeIntensity',
    'internal ConfigEntry<int> ReanimationSmokeParticleAmount',
    'internal ConfigEntry<bool> ReanimationDynamicParticleBudget',
    '"Reanimation VFX"',
    '"DynamicParticleBudget"',
    '"RunesEnabled"',
    '"SmokeEnabled"',
    '"RuneParticleAmount"',
    '"SmokeParticleAmount"')) {
    if (!$pluginSource.Contains($required)) {
        throw "Reanimation VFX configuration is missing: $required"
    }
}
if ($pluginSource -notmatch '(?s)ReanimationDynamicParticleBudget = BindOrdered\(.*?true,.*?ReanimationRunesEnabled = BindOrdered\(.*?true,.*?ReanimationSmokeEnabled = BindOrdered\(.*?false,') {
    throw 'Reanimation VFX defaults no longer preserve dynamic budgeting with the rune-only presentation.'
}
if (!$glyphSource.Contains('GetEffectSettings(') -or
    !$glyphSource.Contains('plugin.ReanimationVfxEnabled.Value') -or
    !$glyphSource.Contains('plugin.ReanimationRunesEnabled.Value') -or
    !$glyphSource.Contains('plugin.ReanimationSmokeEnabled.Value') -or
    $glyphSource -notmatch '(?s)settings\.RunesEnabled = .*?RuneIntensity > 0\.0f.*?RuneParticleAmount > 0.*?settings\.SmokeEnabled = .*?SmokeIntensity > 0\.0f.*?SmokeParticleAmount > 0.*?settings\.Enabled = settings\.RunesEnabled \|\| settings\.SmokeEnabled' -or
    $glyphSource -notmatch '(?s)RuneParticleBudgetEquivalentServants / activeVisualCount.*?MinimumRuneParticleBudgetScale.*?SmokeParticleBudgetEquivalentServants / activeVisualCount.*?MinimumSmokeParticleBudgetScale' -or
    $glyphSource -notmatch '(?s)GetRuneParticleCount\(.*?particleAmount.*?particleAmount / 100\.0f.*?GetSmokeParticleCount\(.*?particleAmount.*?particleAmount / 100\.0f') {
    throw 'Combined reanimation VFX toggles, zero-cost disabling, or dynamic particle budgeting are not wired into the runtime.'
}
if ($glyphSource -notmatch '(?s)ConfigureEffect\(.*?settings\.SmokeEnabled.*?"Smoke-Count".*?"Count".*?"Coilor-Smoke".*?GetOrCreateSmokeGradient\(\).*?"Fire Texture"' -or
    $glyphSource -notmatch '(?s)int runeParticleCount = settings\.RunesEnabled.*?int smokeParticleCount = settings\.SmokeEnabled' -or
    $glyphSource -notmatch '(?s)class GlyphState.*?readonly CombinedEffectState Effect.*?RefreshEffect\(.*?CombinedEffectState effectState = state\.Effect') {
    throw 'Runes and smoke are not consolidated into one independently configured pooled effect per servant.'
}
foreach ($removed in @(
    'ReanimationFireEnabled',
    'ReanimationFireIntensity',
    'ReanimationFireParticleAmount',
    '"FireEnabled"',
    '"FireIntensity"',
    '"FireParticleAmount"')) {
    if ($pluginSource.Contains($removed) -or $glyphSource.Contains($removed)) {
        throw "Removed reanimation fire configuration remains: $removed"
    }
}
if ($glyphSource.Contains('22fdfa954ef8f9c4a8fb544e462874b8')) {
    throw 'Reanimation glyph runtime retains the invalid non-addressable asset GUID.'
}
if ($glyphSource.Contains('"Lifetime Min/Max"')) {
    throw 'Reanimation glyph runtime still enlarges the secondary dripping-particle lifetime.'
}
if ($glyphSource -notmatch '(?s)GetOrCreateSparkleGradient\(\).*?new GradientAlphaKey\(0\.0f, 0\.0f\).*?new GradientAlphaKey\(0\.0f, 1\.0f\)') {
    throw 'Reanimation glyph runtime does not make the independent sparkle branch transparent.'
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
    'Version under test: 2.5.2',
    'exactly 2x',
    'a true hero summon rises',
    'simplified remains',
    'green/dark skeleton-summon effect',
    'SAS-SMOKE-30',
    'SAS-SMOKE-31',
    'SAS-SMOKE-39',
    'SAS-SMOKE-43',
    'two-handed grip immediately suppresses targeting',
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
