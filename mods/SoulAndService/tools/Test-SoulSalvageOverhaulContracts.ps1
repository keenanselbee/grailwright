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
$innerLightSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulRendInnerLightRuntime.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw
$matrix = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\TEST-MATRIX.md") -Raw
foreach ($required in @(
    'ConfigSchemaVersion = 10',
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
    'SoulSalvageRuntime.IsVersatileWeaponsHandSuppressed(state.Slot)',
    'fsm.IsCasting',
    'Mathf.MoveTowards(',
    'TryGetCurrentVisualWorldOffset',
    'state.LightObject.transform.position = anchor.position',
    '+ visualWorldOffset',
    'ConfigureHdrpData(state, nextIntensity)',
    '"volumetricDimmer"',
    'progress * progress * (3.0f - (2.0f * progress))')) {
    if (!$innerLightSource.Contains($required)) {
        throw "Soul Rend inner-light runtime contract is missing: $required"
    }
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
    'SoulProgressionRuntime.RollSoulVigorValue(',
    'VersatileWeaponsPluginGuid =',
    'VersatileWeaponsApiTypeName =',
    '"IsMainHandSuppressed"',
    '"IsOffHandSuppressed"',
    'TryResolveVersatileWeaponsApi()',
    'OrdinarySummonVigorCost = 3',
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
if ($runtimeSource -notmatch '(?s)TryHarvestCorpse\(\s*fingerprint,\s*tier,\s*quality01,\s*out harvestReceipt\).*?if \(!TryCreateRemains\(.*?RollbackCorpseHarvest\(harvestReceipt\)') {
    throw "Ordinary corpse harvest does not roll back Soul Vigor when remains creation fails."
}
if ($runtimeSource -notmatch '(?s)bool harvestReady = !record\.Sacrificed\s*\|\| SoulProgressionRuntime\.TryHarvestCorpse\(.*?if \(!simplified && harvestReceipt != null\)\s*\{\s*SoulProgressionRuntime\.RollbackCorpseHarvest\(harvestReceipt\);') {
    throw "Raised-corpse harvest does not commit with the remains transaction."
}

foreach ($required in @(
    'private const int OrdinarySummonVigorCost = 3;',
    'SoulProgressionRuntime.TrySpendSoulVigor(',
    'SoulProgressionRuntime.RestoreSoulVigor(',
    'int committedVigor = vigorAfter < vigorBefore ? vigorCost : 0;',
    'private static int GetReanimationSoulVigorCost(',
    'InvestedSoulVigor = committedVigor',
    'NativeSoulVigor = SoulProgressionRuntime.RollSoulVigorValue(',
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
    'Version under test: 2.2.0',
    'exactly 2x',
    'a true hero summon rises',
    'simplified remains',
    'green/dark skeleton-summon effect',
    'SAS-SMOKE-30',
    'SAS-SMOKE-31',
    'SAS-SMOKE-39',
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
