$ErrorActionPreference = 'Stop'

$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulAndService.cs') -Raw
$rank = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulforgedRuntime.cs') -Raw
$summons = Get-Content -LiteralPath (Join-Path $modRoot 'src\SummonRuntime.cs') -Raw
$salvage = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulSalvageRuntime.cs') -Raw
$glyph = Get-Content -LiteralPath (Join-Path $modRoot 'src\ReanimationGlyphRuntime.cs') -Raw

foreach ($required in @(
    'ConfigSchemaVersion = 28',
    'RestHostBehavior.Sustain',
    'SoulforgedRankOverride.Disabled',
    'public enum SoulforgedPromotionTarget',
    'PromoteActiveSummonsToRealRank',
    '"#FFFFFF"',
    'ConfigRecoveryKeepCurrentDefaultRule(',
    '"Persistence",',
    '"PersistentServants"')) {
    if (!$plugin.Contains($required)) {
        throw "Soulforged configuration contract is missing: $required"
    }
}

foreach ($required in @(
    'internal const int MaximumRank = 17;',
    '2.0f, 4.0f, 6.0f, 8.0f, 11.0f, 14.0f, 17.0f, 20.0f,',
    '23.0f, 26.0f, 30.0f, 34.0f, 38.0f, 42.0f, 46.0f, 50.0f,',
    '54.0f',
    '30.0f + (next * 10.0f)',
    'outcome.FinalAmount',
    'SoulProgressionRuntime.GetNecromanticPower() < 40.0f',
    'plugin == null || !plugin.IsEnabled',
    'WithFactionUtils.WantToFight',
    '1.0f + (GetEffectiveRank(summonId) * 0.01f)',
    '1.0f + (GetEffectiveRank(summonId) * 0.005f)',
    'internal static bool TryReduceRealRanks(',
    'internal static int PromoteActiveSummonsToRealRank(',
    'state.DamageDealt = currentRank <= 0',
    '"HP: "',
    '"% | Rank: "',
    '"Unranked"',
    '"MAX"',
    '": Soulforged "')) {
    if (!$rank.Contains($required)) {
        throw "Soulforged progression contract is missing: $required"
    }
}

if ($plugin -notmatch '(?s)new ConfigDefinition\("Diagnostics", "PromoteActiveSummonsToRealRank"\).*?PromoteActiveSummonsToRealRank = BindOrdered\(.*?SoulforgedPromotionTarget\.None.*?PromoteActiveSummonsToRealRank\.Value =\s*SoulforgedPromotionTarget\.None.*?RestorePreservedConfigValues\(\).*?BindBalancePresetEvents\(\)' -or
    $plugin -notmatch '(?s)OnPromoteActiveSummonsToRealRankChanged\(.*?SoulforgedRuntime\.PromoteActiveSummonsToRealRank\(.*?PromoteActiveSummonsToRealRank\.Value =\s*SoulforgedPromotionTarget\.None.*?Config\.Save\(\).*?_foaModManagerRefreshPending = true' -or
    $rank -notmatch '(?s)PromoteActiveSummonsToRealRank\(.*?foreach \(SoulforgedState state in States\.Values\).*?IsOwnedSummon\(state\.Summon\).*?state\.EarnedRank >= targetRank.*?state\.EarnedRank = targetRank.*?state\.DamageDealt = Math\.Max\(.*?DamageEquivalents\[targetRank - 1\].*?WriteInt\(summonId, "rank".*?WriteFloat\(summonId, "damage".*?RefreshPresentation\(state\)') {
    throw 'The one-shot diagnostic promotion must persist genuine rank progress, never lower ranks, reset itself, and refresh presentation.'
}

if ($rank.Contains('"OVERRIDE"') -or
    $rank -notmatch '(?s)string progress = overridden\s*\? rank <= 0\s*\? "Unranked"\s*:\s*RomanRanks\[rank\]') {
    throw 'Soulforged hover does not show the selected effective rank while its override is active.'
}
if ($rank -notmatch '(?s)GetRankLabel\(int rank\).*?return clamped <= 0 \? "Base" : RomanRanks\[clamped\]') {
    throw 'Soulforged rank-removal feedback does not label rank zero as Base.'
}

if ($summons -notmatch '(?s)AfterApplyDamageModifiers\(.*?GetSummonDamageTakenMultiplier\(\).*?SoulforgedRuntime\.GetMultiplier.*?GetSummonDamageMultiplier\(\).*?SoulforgedRuntime\.GetMultiplier') {
    throw 'Soulforged combat scaling is not composed with both outgoing and incoming summon scaling.'
}
if ($summons -notmatch '(?s)GetCombinedVisualSizeMultiplier\(.*?Mathf\.Min\(\s*1\.30f,\s*SoulforgedRuntime\.GetVisualSizeMultiplier\(summonId\)\s*\* GetEmpowermentSizeMultiplier\(summon\)\)' -or
    $summons -notmatch '(?s)ApplyEmpowermentVisual\(.*?combinedSize = controller\.Npc == null.*?GetCombinedVisualSizeMultiplier\(.*?Vector3\.one \* combinedSize') {
    throw 'Soulforged and Empowerment visible size are not composed through the 1.30x cap.'
}
if ($summons -notmatch '(?s)GetRestAttritionPercent\(.*?45\.0f \+ \(18\.0f.*?activeServants - 1.*?hours\) / 8\.0f.*?necromanticPower / 100\.0f.*?Math\.Min\(.*?90\.0f,.*?basePercent.*?\* duration.*?\* powerFactor.*?\* SoulAndServicePlugin\.GetEffectiveBalanceTuning\(\).*?\.ServantUpkeepMultiplier\)') {
    throw 'Rest attrition does not use the approved host-size, duration, Power, balance-profile, and 90% cap formula.'
}

foreach ($required in @(
    '0.75f * (GetEffectiveRank(summonId) / (float)MaximumRank)',
    '+ (empowered ? 0.25f : 0.0f)',
    'configuredIntensity',
    'configuredFullPotentialIntensity',
    'Mathf.Lerp(',
    'BlendLinear(',
    'DefaultFullPotentialColor')) {
    if (!$rank.Contains($required) -and !$glyph.Contains($required)) {
        throw "Soulforged visual contract is missing: $required"
    }
}
if ($plugin -notmatch '(?s)ReanimationUseCustomFullPotentialColor = BindOrdered\(.*?"UseCustomFullPotentialColor",\s*false,.*?ReanimationFullPotentialColor = BindOrdered\(.*?"#FFFFFF"' -or
    $glyph -notmatch '(?s)DefaultFullPotentialColor =\s*new Color\(0\.0f, 0\.90196080f, 0\.46274510f\).*?Color fullPotentialColor = DefaultFullPotentialColor;.*?ReanimationUseCustomFullPotentialColor\.Value.*?ResolveConfiguredColor\(.*?ReanimationFullPotentialColor\.Value,\s*DefaultFullPotentialColor\)') {
    throw 'Soulforged progression does not default to emerald or gate the custom full-potential endpoint behind its opt-in setting.'
}
if ($plugin -notmatch '(?s)ReanimationAuraIntensity = BindOrdered\(.*?"AuraIntensity",\s*5\.0f,.*?"Base Brightness".*?ReanimationFullPotentialBrightness = BindOrdered\(.*?"FullPotentialBrightness",\s*20\.0f,.*?"Full Potential Brightness"' -or
    $plugin -notmatch '(?s)ConfigRecoveryKeepCurrentDefaultRule\(\s*26,\s*"Reanimation VFX",\s*"AuraIntensity"' -or
    $glyph -notmatch '(?s)configuredIntensity.*?5\.0f.*?configuredFullPotentialIntensity.*?20\.0f.*?settings\.AuraIntensity = Mathf\.Min\(\s*MaximumAuraIntensity,\s*Mathf\.Lerp\(\s*configuredIntensity,\s*configuredFullPotentialIntensity,\s*potential\)') {
    throw 'Soulforged brightness no longer interpolates from the 5.0 base to the 20.0 full-potential endpoint under schema-27 defaults.'
}

foreach ($required in @(
    'SetSummonSavedState(summon, persistent);',
    'SavePersistentReanimation(summonId, record);',
    'AfterLocationInitialized',
    'persistent_source.',
    'ScheduleDeferredSourceRestoration(record);',
    'RestoreExecutedServantCorpse(record);',
    'fromDomainDrop',
    'persistent_servant.',
    'persistent_raised.payload',
    'BeforeGameplayMemorySerialize',
    'AfterGameplayMemoryDeserialize',
    'AfterSceneFullyInitializedForRaisedPersistence',
    'TryRehydrateRaisedServant(',
    'MigrateLegacyRaisedServants()',
    'TryResolveCanonicalPersistentSpawnTemplate',
    'SerializeRaisedPersistencePayload(snapshots)',
    'facts.Set(RaisedPersistencePayloadKey, serializedPayload)',
    '((Model)raised).MarkedNotSaved = true;',
    'SoulforgedRuntime.GetPersistenceState(',
    'SoulforgedRuntime.RestorePersistenceState(')) {
    if (!$salvage.Contains($required)) {
        throw "Persistent host contract is missing: $required"
    }
}

if ($salvage -notmatch '(?s)OnSummonInitialized\(NpcHeroSummon summon\).*?SetSummonSavedState\(summon, persistent\);.*?PendingLegacyRaisedRestores\.Add\(summon\).*?if \(!plugin\.SoulSalvageOverhaul\.Value\)') {
    throw 'Persistent host initialization still depends on the optional Soul Rend overhaul.'
}
if ($salvage -notmatch '(?s)SetSummonSavedState\(.*?if \(!persistent && location != null\).*?MarkedNotSaved = true' -or
    $salvage -match '(?s)SetSummonSavedState\(.*?MarkedNotSaved = !persistent') {
    throw 'Ordinary persistent summons no longer remain entirely on the native save path.'
}
if ($salvage -match 'SourceCorpse\)\.MarkedNotSaved = false' -or
    $salvage -match 'source\)\.MarkedNotSaved = false') {
    throw 'Raised-servant persistence still forces source corpses into the native save graph.'
}
if ($salvage -notmatch '(?s)BeforeGameplayMemorySerialize\(\s*GameplayMemory __instance\).*?_loadedRaisedPersistence == null.*?WriteRaisedPersistencePayload\(__instance\)' -or
    $salvage -notmatch '(?s)AfterGameplayMemoryDeserialize\(\s*GameplayMemory __instance\).*?GetPersistenceFacts\(__instance\).*?DeserializeRaisedPersistencePayload\(.*?JsonUtility\.FromJson<RaisedPersistencePayload>.*?EnsureRaisedPersistenceSceneListener\(\)' -or
    $salvage -notmatch '(?s)AfterSceneFullyInitializedForRaisedPersistence\(.*?data\.IsMainScene.*?MigrateLegacyRaisedServants\(\).*?TryRehydrateRaisedServant\(\s*snapshot,\s*recovery,\s*placementSlot,\s*restoredPlacementReservations,\s*out string rejectionReason\).*?RestoreLoadedRaisedSource\(.*?refundVigor: validSnapshot.*?trustedSnapshot: validSnapshot' -or
    $salvage -notmatch '(?s)WriteRaisedPersistencePayload\(\s*GameplayMemory memory = null\).*?GetPersistenceFacts\(memory\).*?foreach \(ReanimationRecord record in Reanimations\.Values\).*?CaptureRaisedPersistenceSnapshot\(record, preserveHost\).*?if \(!IsValidRaisedPersistenceSnapshot\(snapshot\)\).*?throw new InvalidOperationException.*?SerializeRaisedPersistencePayload\(snapshots\).*?facts\.Set\(RaisedPersistencePayloadKey, serializedPayload\)') {
    throw 'Raised-servant surrogate persistence is not atomic, post-scene, and fail-closed.'
}
foreach ($required in @(
    'internal static void GetPersistenceState(',
    'internal static void RestorePersistenceState(',
    'state.OriginalMaximumHealth =',
    'state.DamageDealt = Math.Max(0.0f, damageDealt);',
    'state.EarnedRank = Mathf.Clamp(earnedRank, 0, MaximumRank);',
    'SummonRuntime.TryEmpowerSummon(')) {
    if (!$rank.Contains($required)) {
        throw "Soulforged surrogate transfer contract is missing: $required"
    }
}
if ($salvage -notmatch '(?s)PersistentServants\.Value\).*?ExecutedServantRemains\.Values\.ToArray\(\).*?RestoreExecutedServantCorpse\(record\);.*?ExecutedServantRemains\.Clear\(\);') {
    throw 'Persistent shutdown does not restore executed-servant source corpses before clearing runtime state.'
}
if ($summons -notmatch '(?s)restPreviewPostfix\.after = new\[\] \{ "ks\.tgfoa\.glorious-ui" \};.*?typeof\(VRestPopupUI\), "Refresh"\).*?postfix: restPreviewPostfix') {
    throw 'Rest preview composition is not ordered after Glorious UI.'
}
if ($summons -notmatch '(?s)ConditionalWeakTable<Component, RestWarningState>.*?AfterRestPreviewRefreshed\(.*?warningState\.NativeText = current;.*?WillBeSurprisedByWyrdNight.*?hasMiniboss.*?hasOrdinary.*?showOrdinaryLoss.*?showMinibossLoss.*?if \(showOrdinaryLoss \|\| showMinibossLoss\).*?string nativeText = showNativeWarning.*?warningState\.NativeText.*?warningState\.RenderedText = combined;.*?SetActive\(!string\.IsNullOrWhiteSpace\(combined\)\)') {
    throw 'Rest preview does not preserve native text and compose one current host-specific warning.'
}
if ($summons -notmatch '(?s)message = string\.Empty;.*?if \(showOrdinaryLoss \|\| showMinibossLoss\).*?"Necromantic upkeep: miniboss -"' -or
    $summons -notmatch '(?s)FormatRestLossPercent\(float percent\).*?percent < 0\.1f.*?"<0\.1"' -or
    $summons.Contains('IndexOf("\nNecromantic "')) {
    throw 'Rest preview does not suppress zero loss, preserve tiny positive loss, and retire line-prefix cleanup.'
}

Write-Host 'Soul and Service Soulforged, persistent-host, rest-attrition, and visual contracts passed.'
