$ErrorActionPreference = 'Stop'

$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulAndService.cs') -Raw
$rank = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulforgedRuntime.cs') -Raw
$summons = Get-Content -LiteralPath (Join-Path $modRoot 'src\SummonRuntime.cs') -Raw
$salvage = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulSalvageRuntime.cs') -Raw
$glyph = Get-Content -LiteralPath (Join-Path $modRoot 'src\ReanimationGlyphRuntime.cs') -Raw

foreach ($required in @(
    'ConfigSchemaVersion = 23',
    'RestHostBehavior.Sustain',
    'SoulforgedRankOverride.Disabled',
    '"#FFFFFF"',
    'ConfigRecoveryKeepCurrentDefaultRule(',
    '"Persistence",',
    '"PersistentServants"')) {
    if (!$plugin.Contains($required)) {
        throw "Soulforged configuration contract is missing: $required"
    }
}

foreach ($required in @(
    'private const int MaximumRank = 17;',
    '2.0f, 4.0f, 6.0f, 8.0f, 11.0f, 14.0f, 17.0f, 20.0f,',
    '23.0f, 26.0f, 30.0f, 34.0f, 38.0f, 42.0f, 46.0f, 50.0f,',
    '54.0f',
    '30.0f + (next * 10.0f)',
    'outcome.FinalAmount',
    'SoulProgressionRuntime.GetNecromanticPower() < 40.0f',
    'plugin == null || !plugin.IsEnabled',
    'WithFactionUtils.WantToFight',
    '1.0f + (GetEffectiveRank(summonId) * 0.01f)',
    '"HP: "',
    '"% | Rank: "',
    '"Unranked"',
    '"MAX"',
    '": Soulforged "')) {
    if (!$rank.Contains($required)) {
        throw "Soulforged progression contract is missing: $required"
    }
}

if ($rank.Contains('"OVERRIDE"') -or
    $rank -notmatch '(?s)string progress = overridden\s*\? rank <= 0\s*\? "Unranked"\s*:\s*RomanRanks\[rank\]') {
    throw 'Soulforged hover does not show the selected effective rank while its override is active.'
}

if ($summons -notmatch '(?s)AfterApplyDamageModifiers\(.*?GetSummonDamageTakenMultiplier\(\).*?SoulforgedRuntime\.GetMultiplier.*?GetSummonDamageMultiplier\(\).*?SoulforgedRuntime\.GetMultiplier') {
    throw 'Soulforged combat scaling is not composed with both outgoing and incoming summon scaling.'
}
if ($summons -notmatch '(?s)ApplyEmpowermentVisual\(.*?soulforgedMultiplier.*?empowermentSize.*?Vector3\.one \* soulforgedMultiplier \* empowermentSize') {
    throw 'Soulforged visible size is not composed exactly with Empowerment size.'
}
if ($summons -notmatch '(?s)GetRestAttritionPercent\(.*?45\.0f \+ \(18\.0f.*?activeServants - 1.*?hours\) / 8\.0f.*?necromanticPower / 100\.0f.*?Math\.Min\(90\.0f, basePercent \* duration \* powerFactor\)') {
    throw 'Rest attrition does not use the approved host-size, duration, Power, and 90% cap formula.'
}

foreach ($required in @(
    '0.75f * (GetEffectiveRank(summonId) / (float)MaximumRank)',
    '+ (empowered ? 0.25f : 0.0f)',
    'configuredIntensity',
    '* SoulforgedRuntime.GetMultiplier(summonId)',
    '* SummonRuntime.GetEmpowermentCombatMultiplier(summonId)',
    'BlendLinear(',
    'Color.white')) {
    if (!$rank.Contains($required) -and !$glyph.Contains($required)) {
        throw "Soulforged visual contract is missing: $required"
    }
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
    'TryRehydrateRaisedServant(snapshot)',
    'MigrateLegacyRaisedServants()',
    'TryResolveCanonicalPersistentSpawnTemplate',
    'JsonUtility.ToJson(payload)',
    'facts.Set(RaisedPersistencePayloadKey, json)',
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
if ($salvage -notmatch '(?s)BeforeGameplayMemorySerialize\(\).*?_loadedRaisedPersistence == null.*?WriteRaisedPersistencePayload\(\)' -or
    $salvage -notmatch '(?s)AfterGameplayMemoryDeserialize\(\).*?JsonUtility\.FromJson<.*?EnsureRaisedPersistenceSceneListener\(\)' -or
    $salvage -notmatch '(?s)AfterSceneFullyInitializedForRaisedPersistence\(.*?data\.IsMainScene.*?MigrateLegacyRaisedServants\(\).*?TryRehydrateRaisedServant\(snapshot\).*?RestoreLoadedRaisedSource\(.*?refundVigor: validSnapshot.*?trustedSnapshot: validSnapshot' -or
    $salvage -notmatch '(?s)WriteRaisedPersistencePayload\(\).*?foreach \(ReanimationRecord record in Reanimations\.Values\).*?CaptureRaisedPersistenceSnapshot\(record, preserveHost\).*?if \(!IsValidRaisedPersistenceSnapshot\(snapshot\)\).*?throw new InvalidOperationException.*?JsonUtility\.ToJson\(payload\).*?facts\.Set\(RaisedPersistencePayloadKey, json\)') {
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
if ($summons -notmatch '(?s)showNativeWarning = __instance\.Target != null.*?WillBeSurprisedByWyrdNight.*?summons\.Length <= 0.*?textProperty\.SetValue\(warning, current, null\)') {
    throw 'Rest preview warning composition does not preserve native state and clear stale necromantic text.'
}

Write-Host 'Soul and Service Soulforged, persistent-host, rest-attrition, and visual contracts passed.'
