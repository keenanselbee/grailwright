[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$mainSourcePath = Join-Path $modRoot "src\SteelAndBone.cs"
$difficultySourcePath = Join-Path $modRoot "src\DifficultyOverhaul.cs"
$manifestPath = Join-Path $modRoot "mod.json"
$readmePath = Join-Path $modRoot "README.txt"
$nexusFullPath = Join-Path $modRoot "nexus-full-desc.txt"
$nexusShortPath = Join-Path $modRoot "nexus-short-desc.txt"
$nexusFilePath = Join-Path $modRoot "nexus-file-desc.txt"
$qualityBucketsPath = Join-Path (Split-Path -Parent (Split-Path -Parent $modRoot)) "tools\shared\CorpseQualityBuckets.cs"

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Steel and Bone difficulty contract failed: $Message"
    }
}

$mainSource = Get-Content -LiteralPath $mainSourcePath -Raw
$difficultySource = Get-Content -LiteralPath $difficultySourcePath -Raw
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$readme = Get-Content -LiteralPath $readmePath -Raw
$nexusFull = Get-Content -LiteralPath $nexusFullPath -Raw
$nexusShort = (Get-Content -LiteralPath $nexusShortPath -Raw).Trim()
$nexusFile = (Get-Content -LiteralPath $nexusFilePath -Raw).Trim()
$qualityBuckets = Get-Content -LiteralPath $qualityBucketsPath -Raw

Assert-Contract ($manifest.sourceFiles -contains "src/DifficultyOverhaul.cs") "DifficultyOverhaul.cs is missing from sourceFiles."
Assert-Contract ($manifest.sourceFiles -contains "../../tools/shared/CorpseQualityBuckets.cs") "The shared corpse-quality bucket helper is missing from sourceFiles."
Assert-Contract ($difficultySource.Contains('AvalonAiOverhaulPluginGuid = "AvalonAIOverhaul"')) "Avalon AI Overhaul plugin detection is missing."
Assert-Contract ($difficultySource.Contains('EvaluateAvalonAiOverhaulOverlap();')) "Avalon AI Overhaul overlap evaluation is not wired into the compatibility pass."
Assert-Contract ($difficultySource.Contains('"NpcVisionDistanceMultiplier"') -and $difficultySource.Contains('"EnableStandingFootstepAwareness"') -and $difficultySource.Contains('"CombatLeashMode"')) "Avalon AI Overhaul overlap detection does not cover sight, hearing, and combat pursuit."
Assert-Contract ($difficultySource.Contains('TryShowCompatibilityWarning(') -and $difficultySource.Contains('"compatibility-avalon-ai-overhaul"')) "Compatibility overlaps do not use stable Grail Floating Text main-menu warning events."
Assert-Contract (-not $difficultySource.Contains('buffer.PushNotification(PluginName, null, string.Empty, message, null, false)')) "Compatibility overlaps still use the superseded native notification buffer path."
Assert-Contract ($manifest.references -contains '%GAME%/Fall of Avalon_Data/Managed/DOTween.dll') "mod.json does not reference the native vignette tween assembly."
Assert-Contract ($mainSource.Contains('IsTrueMember(modifiersInfo, "IsCritical")')) "Hit feedback does not use the specific critical modifier."
Assert-Contract (-not $mainSource.Contains('IsTrueMember(modifiersInfo, "AnyCritical")')) "Hit feedback still treats weak spots, sneak attacks, or backstabs as true critical hits."
Assert-Contract ($mainSource.Contains('ConfigSchemaVersion = 26')) "Config schema is not 26."
Assert-Contract ($mainSource.Contains('23,') -and $mainSource.Contains('"ConstructTerms",') -and $mainSource.Contains('The broad Crystal term was replaced with exact crystal-bodied enemy terms')) "Schema 23 does not keep the safe ConstructTerms default."
Assert-Contract ($mainSource.Contains('24,') -and $mainSource.Contains('"FloraTerms",') -and $mainSource.Contains('"FleshUndeadTerms",')) "Schema 24 does not keep the corrected Wailcap and Wight defaults."
Assert-Contract (-not $mainSource.Contains('Statue;Crystal;Lost Knight')) "ConstructTerms still contains the unsafe broad Crystal token."
Assert-Contract ($mainSource.Contains('Statue;CrystalCrawler;Crystal Crawler;CrystalWalker;Crystal Walker;Lost Knight')) "ConstructTerms does not use exact crystal-bodied enemy terms."
Assert-Contract ($mainSource.Contains('"Grindylow_Summon"') -and $mainSource.Contains('"BloodAbominationsSummon"') -and $mainSource.Contains('"BonemaskWarrior_Summon"')) "Cold inheritance repairs are incomplete."
Assert-Contract ($mainSource.Contains('private static readonly string[] FlamegobblerTerms = { "Flamegobbler" };')) "Flamegobbler Cold detection is missing."
Assert-Contract ($mainSource.Contains('private static readonly string[] WyrdSlimeColdWeaknessTerms')) "Wyrd Slime Cold detection is missing."
Assert-Contract ($mainSource.Contains('new DamageRule(TargetFamily.BoneUndead, DamageTag.Cold, "Bone", "Cold", 0.66f, 60)')) "Bone Undead Cold resistance is missing or not x0.66 on Hardened."
Assert-Contract ($mainSource.Contains('new DamageRule(TargetFamily.Construct, DamageTag.Cold, "Construct", "Cold", 0.66f, 60)')) "Construct Cold resistance is missing or not x0.66 on Hardened."
Assert-Contract (-not $mainSource.Contains('MolluscColdWeaknessTerms') -and -not $mainSource.Contains('IsColdSensitiveMollusc')) "Unsupported blanket mollusc Cold weakness is still present."
Assert-Contract ($mainSource.Contains('baseMultiplier = 1.20f;') -and $mainSource.Contains('baseMultiplier = 1.15f;') -and $mainSource.Contains('baseMultiplier = 1.10f;')) "Cold weakness tiers are incomplete."
Assert-Contract ($mainSource.Contains('ShouldSkipForVanillaMultiplier(') -and $mainSource.Contains('"Skipped Steel and Bone Cold weakness because vanilla already modifies "')) "Cold weakness rules do not preserve native subtype reactions."
Assert-Contract ($mainSource.Contains('private static readonly ExactDamageRule[] ExactDamageRules')) "Exact archetype damage rules are missing."
Assert-Contract ($mainSource.Contains('ExactTarget.FrostbittenWarrior, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.FrostbittenWarrior, DamageTag.Cold')) "Frostbitten Warrior elemental reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.Frostgrot, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.Frostgrot, DamageTag.Cold') -and $mainSource.Contains('exact:FrostgrotFlesh')) "Frostgrot elemental reactions or body correction are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.MissingCorpseEaterReaction, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.MissingCorpseEaterReaction, DamageTag.Wyrdness')) "Corpse Eater reaction repairs are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.ElectricStagfatherGolem, DamageTag.Poison')) "Electric Stagfather Golem Poison weakness is missing."
Assert-Contract ($mainSource.Contains('ExactTarget.Mistbearer, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.Mistbearer, DamageTag.Wyrdness')) "Mistbearer reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.WyrdheirChallenge, DamageTag.Cold') -and $mainSource.Contains('ExactTarget.Nivera, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.Rimefiend, DamageTag.Fire')) "Cold-aligned special-enemy reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.FrostWolf, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.FrostWolf, DamageTag.Cold')) "Frost Wolf elemental reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.StrawParent, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.StrawParent, DamageTag.Slashing')) "Straw parent reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.Wyrdspawn, DamageTag.Slashing')) "Wyrdspawn Slash weakness is missing."
Assert-Contract ($mainSource.Contains('ExactTarget.Ogre, DamageTag.Piercing') -and $mainSource.Contains('ExactTarget.Ogre, DamageTag.Bludgeoning')) "Ogre brute reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.FireAligned, DamageTag.Wet') -and $mainSource.Contains('"Cindermar"') -and $mainSource.Contains('"ElementalGolemFire"')) "Wet versus fire-aligned reactions are incomplete."
Assert-Contract ($mainSource.Contains('exact:ElementalStagfatherGolemConstruct') -and $mainSource.Contains('"StagFather_FireGolem"') -and $mainSource.Contains('"StagFather_IceGolem"')) "Elemental Stagfather golems are not corrected to Constructs."
Assert-Contract ($mainSource.Contains('ExactTarget.DrownedSkeletonSailor, DamageTag.Electric') -and $mainSource.Contains('exact:DrownedSkeletonSailorBone')) "Drowned skeleton sailor reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.FrostAngel, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.FrostAngel, DamageTag.Cold')) "Frost Angel reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.IceWeaverChampion, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.IceWeaverWolf, DamageTag.Fire')) "Ice Weaver champion or wolf reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.IceTrialWyrd, DamageTag.Fire') -and $mainSource.Contains('ExactTarget.IceTrialWyrd, DamageTag.Cold')) "Ice Trial Wyrd reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.CharredConclaveWyrdspawn, DamageTag.Cold') -and $mainSource.Contains('ExactTarget.CharredConclaveWyrdspawn, DamageTag.Fire')) "Charred Conclave Wyrdspawn reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.IceStatue, DamageTag.Fire') -and $mainSource.Contains('exact:IceStatueConstruct')) "Trial Ice Statue reactions or family correction are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.AncientBeholder, DamageTag.Piercing') -and $mainSource.Contains('ExactTarget.AncientBeholder, DamageTag.Bludgeoning') -and $mainSource.Contains('exact:AncientBeholderFlesh')) "Ancient Beholder reactions or family correction are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.Singworm, DamageTag.Slashing') -and $mainSource.Contains('ExactTarget.Singworm, DamageTag.Bludgeoning')) "Singworm soft-body reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.LirTentacle, DamageTag.Slashing') -and $mainSource.Contains('ExactTarget.LirTentacle, DamageTag.Bludgeoning')) "Lir Tentacle soft-body reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.BloodAbomination, DamageTag.Slashing') -and $mainSource.Contains('ExactTarget.BloodAbomination, DamageTag.Bludgeoning')) "Blood Abomination soft-body reactions are incomplete."
Assert-Contract ($mainSource.Contains('ExactTarget.BloodAbomination, DamageTag.Slashing, "Blood Abomination", "Slash", 1.20f')) "Blood Abomination authored Slash matchup is not x1.20."
Assert-Contract ($mainSource.Contains('exact:BloodAbominationSoftBody') -and $mainSource.Contains('ClearTargetFamilies(classification);')) "Blood Abomination still inherits a misleading broad body family."
Assert-Contract ($mainSource.Contains('ExactTarget.WyrdSlime, DamageTag.Bludgeoning') -and $mainSource.Contains('0.80f')) "Wyrd Slime Blunt resistance is missing."
Assert-Contract ($mainSource.Contains('exact:WyrdSlimeSoftBody') -and $mainSource.Contains('SetExclusiveTargetFamily(classification, TargetFamily.Wyrd)')) "Wyrd Slime still inherits ordinary flesh rules."
Assert-Contract ($mainSource.Contains('ExactTarget.Tidewraith, DamageTag.Bludgeoning') -and $mainSource.Contains('0.90f')) "Tidewraith Blunt resistance is missing."
Assert-Contract ($mainSource.Contains('SetExclusiveTargetFamily(classification, TargetFamily.Flora)') -and $mainSource.Contains('exact:RootambusherFlora')) "Rootambusher flora correction is missing."
Assert-Contract ($mainSource.Contains('exact:FrostbittenWarriorUndead') -and $mainSource.Contains('exact:WightFlora')) "Frostbitten Warrior or Wight family correction is missing."
Assert-Contract ($mainSource.Contains('exact:GiantFlesh') -and $mainSource.Contains('exact:OgreFlesh')) "Giant or Ogre flesh correction is missing."
Assert-Contract ($mainSource.Contains('TargetFamily.BoneBody') -and $mainSource.Contains('TargetFamily.StoneBody') -and $mainSource.Contains('MetadataBoneBodyTerms = { "HitBones" }')) "Body material and creature family are not separated."
Assert-Contract ($mainSource.Contains('MetadataBoneUndeadTerms = { "Skeleton", "BoneMask" }') -and $mainSource.Contains('MetadataConstructTerms = { "Construct", "Automaton", "Golem" }')) "Hit surfaces still assign Bone Undead or Construct identity directly."
Assert-Contract ($mainSource.Contains('exact:StagfatherSpiritBoneBody') -and $mainSource.Contains('exact:StrawParentSpiritBoneBody') -and $mainSource.Contains('exact:GhostOfBrocMealaSpirit')) "Spirit family corrections are incomplete."
Assert-Contract ($mainSource.Contains('exact:SleepwalkerWyrdStoneBody') -and $mainSource.Contains('exact:WailcapSeaCreature')) "Sleepwalker or Wailcap family correction is incomplete."
Assert-Contract (-not $mainSource.Contains('new DamageRule(TargetFamily.Wyrd, DamageTag.Wyrdness')) "A blanket Wyrdness resistance still penalizes pure Wyrd casters."
Assert-Contract ($mainSource.Contains('classification.HasStoneBody = false;') -and $mainSource.Contains('exact:AncientBeholderFlesh') -and $mainSource.Contains('exact:GiantFlesh')) "False HitStone bodies are not cleared from exact flesh corrections."
Assert-Contract ($mainSource.Contains('private bool TryResolveAxeMaterialRule(') -and $mainSource.Contains('ApplyPresetIntensity(1.20f, preset)')) "The axe versus wood/flora rule is missing or not x1.20 on Hardened."
Assert-Contract ($mainSource.IndexOf('TryResolveAxeMaterialRule(', [StringComparison]::Ordinal) -lt $mainSource.IndexOf('for (int i = 0; i < DamageRules.Length; i++)', [StringComparison]::Ordinal)) "The axe material rule does not replace the ordinary flora Slash rule."
Assert-Contract ($mainSource.Contains('classification.IsAxe = !classification.IsMiningToolCombatHit && IsAxeDamage(damage);') -and $mainSource.Contains('part.IsAxe = overall.IsAxe && physicalPart;')) "Axe identity is not retained through weighted damage parts or suppressed for mining-tool combat hits."
Assert-Contract ($mainSource.Contains('typedDamage.Type != DamageType.PhysicalHitSource') -and $mainSource.Contains('tool.Type == ToolType.Mining')) "Mining-tool classification is not limited to direct PhysicalHitSource combat attacks."
Assert-Contract ($mainSource.Contains('classification.IsMiningToolCombatHit = IsMiningToolCombatHit(damage);') -and $mainSource.Contains('classification.IsPiercing = true;') -and $mainSource.Contains('classification.IsSlashing = false;')) "Mining-tool combat hits are not forced to Pierce before normal weapon fallback."
Assert-Contract ($mainSource.Contains('physicalPart && overall != null && overall.IsMiningToolCombatHit')) "Weighted physical parts do not preserve the mining-tool Pierce override."
Assert-Contract ($mainSource.Contains('typedDamage.Type == DamageType.Interact')) "Non-combat interaction damage is not excluded from Steel and Bone modifiers."
Assert-Contract ($mainSource.IndexOf('typedDamage.Type == DamageType.Interact', [StringComparison]::Ordinal) -lt $mainSource.IndexOf('ApplyOutgoingHealthDamageModifier(ref damageModifier);', [StringComparison]::Ordinal)) "Interaction damage is not excluded before outgoing player-damage pressure."
Assert-Contract ($mainSource.Contains('ConfigRecoveryBaselineSchema = 14')) "Recovery baseline moved from 14."
Assert-Contract ($mainSource.Contains('"DamageOverTimeNumberHeightMultiplier", 3.0f,')) "Damage-over-time number height does not default to 3.0x."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<float>(0.0f, 6.0f)')) "Damage-over-time number height maximum is not 6.0x."
Assert-Contract ($mainSource.Contains('_damageOverTimeNumberHeightMultiplier == null ? 3.0f')) "Damage-over-time number height fallback is not 3.0x."
Assert-Contract ($mainSource.Contains('return Clamp(value, 0.0f, 6.0f);')) "Damage-over-time number height runtime maximum is not 6.0x."
Assert-Contract ($mainSource.Contains('"DamageOverTimeNumberScale", 0.75f,')) "Damage-over-time number text scale does not default to 0.75x."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<float>(0.5f, 2.0f)')) "Damage-over-time number text-scale range is missing."
Assert-Contract ($mainSource.Contains('scale *= GetDamageOverTimeNumberScale();')) "Damage-over-time number text scale is not applied to final visual sizing."
Assert-Contract ($mainSource.Contains('RestorePreservedSetting(profile, _damageOverTimeNumberScale')) "Damage-over-time number text scale is not recoverable."
Assert-Contract ($mainSource.Contains('new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(')) "Preset safety rule is missing."
Assert-Contract ($mainSource.Contains('"Preset"')) "Preset safety rule does not target Preset."
Assert-Contract ($mainSource.Contains('ReadCustomizationProfile(')) "Automatic config preservation does not use the shared customization profile."
Assert-Contract ($mainSource.Contains('profile.TryGetCustomizedValue(')) "Automatic config preservation does not use shared typed customization detection."
Assert-Contract ($mainSource.Contains('ConfigPreviousSettingsRecovery.TryRestore(')) "Automatic config preservation does not use shared current-range clamping."
Assert-Contract ($mainSource.IndexOf('RestorePreservedConfigSettings();', [StringComparison]::Ordinal) -lt $mainSource.IndexOf('ConfigPreviousSettingsRecovery.Bind(', [StringComparison]::Ordinal)) "Automatic preservation does not run before the manual recovery tab is bound."
Assert-Contract ($mainSource.Contains('private static ConfigDescription ConfigUi(')) "Config UI metadata helper is missing."
Assert-Contract ($mainSource.Contains('new Grailwright.Shared.ConfigRecoveryUiMetadata')) "Config UI metadata helper does not use the shared FoA Mod Manager-compatible tag."
Assert-Contract ($mainSource.Contains('ConfigUi("Master switch.", "General", "Enabled", 0, 0)')) "General Enabled UI metadata is missing."
Assert-Contract ($mainSource.Contains('"Combat Rules", "Respect Native Multipliers", 10, 0')) "Combat Rules UI metadata is missing."
Assert-Contract ($mainSource.Contains('"Advanced - Vanilla Multipliers", "Maximum Amplified Weakness", 30, 50')) "Advanced vanilla UI metadata is missing."
Assert-Contract ($mainSource.Contains('"Damage Numbers", "Effectiveness Feedback Sensitivity", 40, 120')) "Effectiveness feedback sensitivity UI metadata is missing."
Assert-Contract ($mainSource.Contains('"Damage Numbers", "Maximum Active", 40, 150')) "Damage Numbers UI ordering is missing."
Assert-Contract ($mainSource.Contains('GetPresetEffectivenessFeedbackSensitivity(_preset.Value)')) "Effectiveness feedback sensitivity does not use the selected preset as its initial default."
Assert-Contract ($mainSource.Contains('case Preset.Tempered:') -and $mainSource.Contains('return 1.20f;')) "Tempered feedback sensitivity is not 1.20."
Assert-Contract ($mainSource.Contains('case Preset.Crucible:') -and $mainSource.Contains('return 1.00f;')) "Crucible feedback sensitivity is not 1.00."
Assert-Contract ($mainSource.Contains('return 1.10f;')) "Hardened feedback sensitivity is not 1.10."
Assert-Contract ($difficultySource.Contains('ReferenceEquals(args.ChangedSetting, _preset)')) "Preset changes do not reset the single feedback sensitivity setting."
Assert-Contract ($mainSource.Contains('ApplyEffectivenessFeedbackSensitivity(effectivenessMultiplier)')) "Presentation effectiveness does not apply feedback sensitivity."
Assert-Contract ($mainSource.Contains('public const int ApiVersion = 6;')) "Hit-feedback API is not v6."
Assert-Contract ([regex]::IsMatch($mainSource, 'public static event Action<float, float, bool, bool, bool, bool, bool, string, float>\s+HitResolved;')) "Hit-feedback API v6 hit signature is incorrect."
Assert-Contract ([regex]::IsMatch($mainSource, 'public static event Action<int, float, float, bool, bool, bool, bool, bool, string, float>\s+KillingBlowResolved;')) "Killing-blow feedback event signature is incorrect."
Assert-Contract ($mainSource.Contains('IsPlayerAttack = IsDirectHeroDamageSource(')) "Pending hit feedback does not retain direct-player attribution."
Assert-Contract ([regex]::IsMatch($mainSource, 'SteelAndBoneHitFeedbackApi\.Publish\([\s\S]*?damageOverTime,\s*playerAttack,')) "Hit feedback does not publish direct-player attribution."
Assert-Contract ($mainSource.Contains('object remainingHealth = GetOptionalPropertyValue(healthElement, "Health");')) "Killing-blow detection does not read resolved remaining health."
Assert-Contract ($mainSource.Contains('ReadStatValue(remainingHealth) <= 0.0001f')) "Killing-blow detection does not recognize the pre-death zero-health state."
Assert-Contract ($mainSource.Contains('resolvedTarget is NpcElement')) "Killing-blow feedback is not limited to defeated NPCs."
Assert-Contract ($mainSource.Contains('CorpseQualityBuckets.CalculateIntrinsicQuality01(')) "Killing-blow quality does not use the shared native-tier calculation."
Assert-Contract ($mainSource.Contains('CorpseQualityBuckets.ApplyThreatClassAdjustment(')) "Killing-blow quality does not apply the shared threat-class adjustment."
Assert-Contract ($mainSource.Contains('CorpseQualityBuckets.ApplyBoundedRelativeLevelAdjustment(')) "Killing-blow quality does not apply the shared bounded relative-level adjustment."
Assert-Contract ($mainSource.Contains('string.Equals(tag, "Tier:" + tier, StringComparison.Ordinal)')) "Killing-blow quality does not require an exact native Tier tag."
Assert-Contract ($mainSource.Contains('SteelAndBoneHitFeedbackApi.PublishKillingBlow(')) "Resolved killing blows are not published."
Assert-Contract ($qualityBuckets.Contains('DefaultReferenceKillXp = 700.0f')) "The shared untagged XP reference is not 700."
Assert-Contract ($qualityBuckets.Contains('DefaultReferenceMaxHealth = 3400.0f')) "The shared untagged health reference is not 3400."
Assert-Contract ($qualityBuckets.Contains('DefaultLevelQualityPerLevel = 0.025f')) "The shared quality adjustment is not 2.5% per level."
Assert-Contract ($qualityBuckets.Contains('DefaultMaximumLevelQualityAdjustment = 0.075f')) "The shared level adjustment cap is not 7.5%."
Assert-Contract ($qualityBuckets.Contains('EliteQualityBonus = 0.10f')) "The Elite quality bonus is not 10%."
Assert-Contract ($qualityBuckets.Contains('MiniBossQualityBonus = 0.175f')) "The MiniBoss quality bonus is not 17.5%."
Assert-Contract ($qualityBuckets.Contains('BossMinimumQuality = 0.875f')) "The Boss quality floor is not Prime."
Assert-Contract ($qualityBuckets.Contains('quality01 <= MeagerMaximumQuality')) "Meager bucket boundary is not inclusive."
Assert-Contract ($qualityBuckets.Contains('quality01 <= WorthyMaximumQuality')) "Worthy bucket boundary is not inclusive."
Assert-Contract ($qualityBuckets.Contains('quality01 <= PotentMaximumQuality')) "Potent bucket boundary is not inclusive."
Assert-Contract ($mainSource.Contains('return typedDamage.IsDamageOverTime;')) "Damage-over-time classification does not use the native Damage.IsDamageOverTime flag."
Assert-Contract ([regex]::IsMatch($mainSource, 'private bool IsOneDamageDirectAttack\(object damage\)[\s\S]*?typedDamage\.IsDamageOverTime[\s\S]*?typedDamage\.Amount != 1\.0f[\s\S]*?return typedDamage\.Type == DamageType\.PhysicalHitSource\s*\|\| typedDamage\.Type == DamageType\.MagicalHitSource;')) "One-damage frame-0 classification is not limited to direct non-DoT physical or magical hits at exactly Damage.Amount 1."
Assert-Contract ([regex]::IsMatch($mainSource, 'bool oneDamageDirectAttack = finalAmount == 1\.0f\s*&& IsOneDamageDirectAttack\(damage\);\s*bool hitMarkerImmune = immune \|\| oneDamageDirectAttack;')) "One-damage direct weapon and spell hits do not select hit-marker frame 0."
Assert-Contract ([regex]::IsMatch($mainSource, 'BuildDamageNumberVisual\([\s\S]*?immune,[\s\S]*?damageOverTime,')) "One-damage frame-0 classification must not change floating damage-number immunity styling."
Assert-Contract ([regex]::IsMatch($mainSource, 'if \(oneDamageDirectAttack\)\s*\{\s*visual\.Text = "RESISTED";')) "One-damage direct weapon and spell hits do not replace the numeric damage text with RESISTED."
Assert-Contract ([regex]::IsMatch($mainSource, 'SteelAndBoneHitFeedbackApi\.Publish\([\s\S]*?hitMarkerImmune,')) "Hit-marker frame-0 classification is not published without an API change."
Assert-Contract ($mainSource.Contains('enum DamageNumberMode') -and $mainSource.Contains('ResistAndImmuneOnly') -and $mainSource.Contains('ResistAndImmuneOnlyOnce')) "DamageNumberMode does not expose both resistance/immunity notice modes."
Assert-Contract ($mainSource.Contains('"DamageNumberMode", DamageNumberMode.AllDamage')) "DamageNumberMode does not preserve all-damage feedback by default."
Assert-Contract ([regex]::IsMatch($mainSource, 'private bool TryPrepareDamageNumberForDisplay\([\s\S]*?if \(damageOverTime\)\s*\{\s*return false;')) "Notice-only damage-number modes do not exclude damage-over-time ticks."
Assert-Contract ($mainSource.Contains('oneDamageDirectAttack || visualEffectivenessMultiplier < 0.95f')) "Resistance notices do not match the hit-marker resistance boundary."
Assert-Contract ($mainSource.Contains('visual.Text = immune ? "IMMUNE" : "RESISTED";')) "Notice-only modes do not replace numeric text with IMMUNE or RESISTED."
Assert-Contract ($mainSource.Contains('classification.ResistanceNoticeShown') -and $mainSource.Contains('classification.ImmunityNoticeShown')) "Once-per-enemy mode does not track resistance and immunity independently."
Assert-Contract ($difficultySource.Contains('"WeakSpotDamageBonus"')) "Weak Spot Damage Bonus is not configurable."
Assert-Contract ($difficultySource.Contains('GetPresetWeakSpotDamageBonus(_preset.Value)')) "Weak Spot Damage Bonus does not use the selected preset as its initial default."
Assert-Contract ($difficultySource.Contains('return 0.10f;') -and $difficultySource.Contains('return 0.20f;') -and $difficultySource.Contains('return 0.30f;')) "Weak Spot Damage Bonus preset values are incomplete."
Assert-Contract ($difficultySource.Contains('ApplyPresetWeakSpotDamageBonus();')) "Preset changes do not reset Weak Spot Damage Bonus."
Assert-Contract ([regex]::IsMatch($difficultySource, 'damageModifier\s*\+=\s*bonus;')) "Weak Spot Damage Bonus is not added beside native precision bonuses."
Assert-Contract ($mainSource.IndexOf('ApplyWeakSpotDamageBonus(modifiersInfo, ref damageModifier);', [StringComparison]::Ordinal) -lt $mainSource.IndexOf('ApplyOutgoingHealthDamageModifier(ref damageModifier);', [StringComparison]::Ordinal)) "Weak Spot Damage Bonus is not applied before outgoing damage pressure."
Assert-Contract ($mainSource.Contains('DamageModifiersInfo __result')) "The damage patch does not receive native precision results."
Assert-Contract ($mainSource.Contains('"CriticalMultiplier"') -and $mainSource.Contains('"WeakSpotMultiplier"')) "Precision feedback does not read the native critical and weak-spot bonuses."
Assert-Contract ($mainSource.Contains('Mathf.Clamp(precisionBonus, 0.0f, 0.50f)')) "Precision feedback is not capped at 50%."
Assert-Contract ($mainSource.Contains('float precisionVisualScale = 1.0f - resistance;')) "Precision feedback does not scale down with normalized resistance."
Assert-Contract ($mainSource.Contains(': Mathf.Clamp(precisionBonus, 0.0f, 0.50f) * precisionVisualScale;')) "Precision color and size do not share the resistance-scaled precision bonus."
Assert-Contract ($mainSource.Contains('Color.Lerp(color, Color.red, precisionVisualBonus)')) "Precision feedback color does not blend toward pure red."
Assert-Contract ($mainSource.Contains('scale *= 1.0f + precisionVisualBonus;')) "Damage-number precision scaling does not follow the resolved bonus."
Assert-Contract ($mainSource.Contains('Critical = critical')) "Resistance scaling must not suppress the critical number pop."
Assert-Contract ([regex]::IsMatch($mainSource, 'SteelAndBoneHitFeedbackApi\.Publish\(\s*effectivenessMultiplier,\s*visualEffectivenessMultiplier,\s*hitMarkerImmune,\s*critical,\s*weakSpot,')) "Resistance scaling must retain critical and weak-spot hit-marker identities."

$weakSpotCases = @(
    @{ Preset = "Tempered"; Bonus = 0.10; Outgoing = 0.95; Expected = 1.045 },
    @{ Preset = "Hardened"; Bonus = 0.20; Outgoing = 0.90; Expected = 1.080 },
    @{ Preset = "Crucible"; Bonus = 0.30; Outgoing = 0.85; Expected = 1.105 }
)
foreach ($case in $weakSpotCases) {
    $resolved = (1.0 + $case.Bonus) * $case.Outgoing
    Assert-Contract ([Math]::Abs($resolved - $case.Expected) -lt 0.000001) ("Weak-spot arithmetic drifted for " + $case.Preset + ".")
}

$defaultCriticalPrecision = [Math]::Min(0.50, 0.50)
Assert-Contract ([Math]::Abs((1.0 + $defaultCriticalPrecision) - 1.50) -lt 0.000001) "Default critical feedback does not resolve to x1.50 size."
$extremeResistance = 1.0
$extremeResistancePrecision = $defaultCriticalPrecision * (1.0 - $extremeResistance)
Assert-Contract ([Math]::Abs($extremeResistancePrecision) -lt 0.000001) "Maximum resistance does not suppress precision color and size emphasis."
Assert-Contract ($mainSource.Contains('"Advanced - Target Families", "Armored Humanoid Terms", 50, 100')) "Target family UI ordering is missing."
Assert-Contract ([regex]::IsMatch($difficultySource, '"Difficulty - Player",\s*"Player Arrow Gravity Multiplier",\s*60,\s*80')) "Player difficulty UI metadata is missing."
Assert-Contract ([regex]::IsMatch($difficultySource, '"Difficulty - Enemies",\s*"Maximum Enemy Attack Slots",\s*70,\s*10')) "Enemy difficulty UI metadata is missing."
Assert-Contract ([regex]::IsMatch($difficultySource, '"Difficulty - Enemies",\s*"Hostile Archer Aim Scatter \(Meters\)",\s*70,\s*45')) "Hostile archer aim-scatter UI metadata is missing."
Assert-Contract ($difficultySource.Contains('"Difficulty - Progression", "Quest and Objective XP", 80, 10')) "Progression UI metadata is missing."
Assert-Contract ($mainSource.Contains('"Diagnostics", "Diagnostics", 90, 0')) "Diagnostics display metadata is missing."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<float>(0.0f, 0.50f)')) "Elite weakness range metadata is missing."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<float>(0.05f, 0.95f)')) "Elite resistance range metadata is missing."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<float>(1.05f, 3.0f)')) "Vanilla weakness range metadata is missing."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<int>(12, 80)')) "Damage-number font range metadata is missing."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<float>(0.35f, 2.50f)')) "Damage-number duration range metadata is missing."
Assert-Contract ($mainSource.Contains('new AcceptableValueRange<int>(1, 128)')) "Damage-number maximum-active range metadata is missing."
Assert-Contract ($mainSource.Contains('class EnemyArmorProfile : Element<NpcElement>')) "NPC armor profiles are not cached on the target."
Assert-Contract ($mainSource.Contains('ICharacterInventory.Events.AfterEquipmentChanged')) "NPC armor profiles do not refresh after equipment changes."
Assert-Contract ($mainSource.Contains('item.Template.IsLightArmor')) "Light armor does not use native item-template evidence."
Assert-Contract ($mainSource.Contains('item.Template.IsMediumArmor')) "Medium armor does not use native item-template evidence."
Assert-Contract ($mainSource.Contains('item.Template.IsHeavyArmor')) "Heavy armor does not use native item-template evidence."
Assert-Contract ($mainSource.Contains('itemAudio.ArmorSurfaceType')) "Armor spell interactions do not use native armor-surface evidence."
Assert-Contract ($mainSource.Contains('SurfaceType.ArmorFabric')) "Fabric armor material is not classified."
Assert-Contract ($mainSource.Contains('SurfaceType.ArmorLeather')) "Leather armor material is not classified."
Assert-Contract ($mainSource.Contains('SurfaceType.ArmorMetal')) "Metal armor material is not classified."
Assert-Contract ($mainSource.Contains('baseMultiplier = 1.08f;')) "Light armor arrow tuning is not x1.08 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.75f;')) "Heavy armor arrow tuning is not x0.75 on Hardened."
Assert-Contract ($mainSource.Contains('DampArmorTierResistanceAgainstNativeArmor')) "Armor-tier resistance does not account for existing numerical armor."
Assert-Contract ($mainSource.Contains('armorTier == EnemyArmorTier.Medium ? 1.00f : 0.90f')) "Heavy armor Pierce tuning is not x0.90 on Hardened."
Assert-Contract ($mainSource.Contains('armorTier == EnemyArmorTier.Medium ? 0.92f : 0.82f')) "Slash is not less effective than Pierce against Medium and Heavy armor."
Assert-Contract ([regex]::IsMatch($mainSource, 'case DamageTag\.Bludgeoning:[\s\S]*?armorTier == EnemyArmorTier\.Exposed\s*\?\s*1\.00f')) "Blunt is not neutral against exposed humanoid flesh."
Assert-Contract ($mainSource.Contains('armorTier == EnemyArmorTier.Medium ? 1.08f : 1.15f')) "Blunt does not improve against Medium and Heavy armor."
Assert-Contract ($mainSource.Contains('"TechniqueMatchupRulesEnabled"')) "Technique matchup rules are not independently toggleable."
Assert-Contract ($mainSource.Contains('typedDamage.IsPush') -and $mainSource.Contains('typedDamage.IsHeavyAttack') -and $mainSource.Contains('typedDamage.Radius > 0.0001f')) "Technique rules do not use the native pommel, heavy-attack, and radius signals."
Assert-Contract ($mainSource.Contains('"Pommel (Blunt)"') -and $mainSource.Contains('baseMultiplier = 1.15f;') -and $mainSource.Contains('baseMultiplier = 1.08f;')) "Pommel strikes do not borrow the existing rigid Blunt matchups."
Assert-Contract ($mainSource.Contains('1.0f + ((match.PresetMultiplier - 1.0f) * 0.60f)')) "Heavy melee does not recover 40% of custom rigid resistance."
Assert-Contract ($mainSource.Contains('"Direct Area"') -and $mainSource.Contains('!targetClass.IsSwarm')) "Direct area attacks do not have the focused swarm fallback."
Assert-Contract ($mainSource.Contains('"MaterialImpactRulesEnabled"')) "Material impact rules are not independently toggleable."
Assert-Contract ($difficultySource.Contains('MaterialImpactResistanceInheritance = 0.60f')) "Direct-hit impact does not inherit 60% of material resistance."
Assert-Contract ($difficultySource.Contains('TryGetDamageEffectivenessMultiplier(damage, out effectivenessMultiplier)')) "Material impact does not use the shared effective resistance result."
Assert-Contract ($difficultySource.Contains('parameters.PoiseDamage = Mathf.Max(0.0f, state.OriginalPoiseDamage * multiplier)')) "Resisted direct hits do not scale player poise damage."
Assert-Contract ($difficultySource.Contains('parameters.ForceDamage = Mathf.Max(0.0f, original * multiplier)')) "Resisted direct hits do not scale player force damage."
Assert-Contract ($difficultySource.Contains('"ProgressiveTenacityEnabled"')) "Progressive Tenacity is not independently toggleable."
Assert-Contract ($difficultySource.Contains('ProgressiveTenacityStartLevel = 20.0f') -and $difficultySource.Contains('ProgressiveTenacityFullLevel = 35.0f')) "Progressive Tenacity does not use the level 20 through 35 progression curve."
Assert-Contract ([regex]::IsMatch($difficultySource, 'ProgressiveTenacityCap\(NpcType npcType\)[\s\S]*?case NpcType\.Trash:\s*return 0\.10f;[\s\S]*?case NpcType\.Normal:\s*return 0\.15f;[\s\S]*?case NpcType\.Elite:\s*return 0\.25f;[\s\S]*?case NpcType\.MiniBoss:\s*return 0\.30f;[\s\S]*?case NpcType\.Boss:\s*return 0\.40f;')) "Progressive Tenacity native NPC caps are incomplete."
Assert-Contract ($difficultySource.Contains('case NpcType.Critter:') -and $difficultySource.Contains('case NpcType.HeroSummon:')) "Progressive Tenacity does not exclude Critters and Hero Summons."
Assert-Contract ($difficultySource.Contains('tenacity *= 0.50f;') -and $difficultySource.Contains('effectivenessMultiplier > 1.0001f')) "Confirmed material weaknesses do not halve Progressive Tenacity."
Assert-Contract ($difficultySource.Contains('float multiplier = 1.0f - (tenacity * 0.50f);')) "Progressive Tenacity health resistance is not half-strength."
Assert-Contract ($difficultySource.Contains('parameters.PoiseDamage = Mathf.Max(0.0f, state.OriginalPoiseDamage * multiplier)') -and $difficultySource.Contains('multiplier *= 1.0f - tenacity;')) "Progressive Tenacity does not reduce player poise damage."
Assert-Contract ($difficultySource.Contains('|| (damage != null && damage.IsDamageOverTime)')) "Progressive Tenacity does not exclude damage-over-time control damage."
Assert-Contract ($difficultySource.Contains('damage.StaminaDamageAmount = Mathf.Max(0.0f, before * multiplier)')) "Progressive Tenacity does not reduce direct stamina damage."
Assert-Contract ($difficultySource.Contains('typeof(HeroParry), "OnTakingDamage"') -and $difficultySource.Contains('ProgressiveTenacityParryTweak')) "Progressive Tenacity does not apply before parry stamina damage."
Assert-Contract ($difficultySource.Contains('typeof(HealthElement),') -and $difficultySource.Contains('"BeforeHealthDecreaseEvents"')) "Progressive Tenacity does not intercept direct stamina damage before its native deduction."
Assert-Contract ($mainSource.Contains('ApplyProgressiveTenacityHealthDamage(')) "Progressive Tenacity health resistance is not applied after material effectiveness resolves."
Assert-Contract ($mainSource.Contains('IsHeroSummonSource(damageDealer)') -and $mainSource.Contains('return npc != null && npc.IsHeroSummon;')) "Progressive Tenacity does not recognize hero-owned summon damage."
Assert-Contract (([regex]::Matches($difficultySource, 'ProgressiveTenacityEnabled\(\) && outgoingOverlap')).Count -ge 2) "Progressive Tenacity outgoing-health overlaps are not reported for Custom Difficulty and HarderLife."
Assert-Contract ($difficultySource.Contains('ProgressiveTenacityEnabled() && poiseOverlap')) "Progressive Tenacity poise overlap is not reported for Tainted Combat."
Assert-Contract ($mainSource.Contains('IsFullyNativeImmune(damage)') -and $mainSource.Contains('weightedMultiplier / totalWeight <= 0.0001f')) "Full native immunity does not bypass invalid normalized damage shares."
Assert-Contract ($difficultySource.Contains('typeof(NpcElement), nameof(NpcElement.DealPoiseDamage)')) "Routine-flinch interception does not occur before enemy virtual poise dispatch."
Assert-Contract ($difficultySource.Contains('StrongResistanceFlinchThreshold = 0.35f') -and $difficultySource.Contains('effectivenessMultiplier <= StrongResistanceFlinchThreshold')) "Strongly resisted direct hits are not classified for routine-flinch suppression."
Assert-Contract ($difficultySource.Contains('isDamageOverTime = true;')) "Strongly resisted direct hits do not suppress only the native routine hit flinch."
Assert-Contract ($difficultySource.Contains('MaterialImpactRulesEnabled() && poiseOverlap')) "Tainted Combat overlap detection omits material-aware poise scaling."
$immuneImpact = 1.0 + ((0.0 - 1.0) * 0.60)
$strongResistanceImpact = 1.0 + ((0.25 - 1.0) * 0.60)
$mildResistanceImpact = 1.0 + ((0.75 - 1.0) * 0.60)
Assert-Contract ([Math]::Abs($immuneImpact - 0.40) -lt 0.000001) "Immune direct-hit impact is not x0.40."
Assert-Contract ([Math]::Abs($strongResistanceImpact - 0.55) -lt 0.000001) "x0.25 resistance does not produce x0.55 impact."
Assert-Contract ([Math]::Abs($mildResistanceImpact - 0.85) -lt 0.000001) "x0.75 resistance does not produce x0.85 impact."
Assert-Contract ($difficultySource.Contains('"PassiveShieldProtectionEnabled"')) "Passive shield protection is not independently toggleable."
Assert-Contract ($difficultySource.Contains('hero.WeaponsVisible')) "Passive shields are not limited to readied weapons."
Assert-Contract ($difficultySource.Contains('hero.IsBlocking') -and $difficultySource.Contains('damage.IsBlocked') -and $difficultySource.Contains('damage.IsParried')) "Passive shield protection can overlap vanilla active blocking or parrying."
Assert-Contract ($difficultySource.Contains('damage.Type != DamageType.PhysicalHitSource')) "Passive shield protection is not limited to direct physical hits."
Assert-Contract ($difficultySource.Contains('ItemRequirementsUtils.GetBlockDamageReductionMultiplier')) "Passive shield protection does not use the vanilla requirement penalty."
Assert-Contract ($difficultySource.Contains('Mathf.Clamp(shield.ItemStats.BlockAngle.ModifiedValue, 0.0f, 90.0f)')) "Passive shield coverage is not capped to the forward 180 degrees."
Assert-Contract ($difficultySource.Contains('Vector3.Dot(heroForward.normalized, incomingDirection.normalized)')) "Passive shield direction filtering is missing."
Assert-Contract ($difficultySource.Contains('presetShare = 0.08f;') -and $difficultySource.Contains('presetShare = 0.10f;') -and $difficultySource.Contains('presetShare = 0.12f;')) "Passive shield preset shares are incomplete."
Assert-Contract ($difficultySource.Contains('class EnemyMovementSpeedTweak : StatTweak')) "Enemy movement does not use an owned native stat tweak."
Assert-Contract ($difficultySource.Contains('npc.CharacterStats.MovementSpeedMultiplier')) "Enemy movement does not use the native movement-speed stat."
Assert-Contract ($difficultySource.Contains('npc.IsInCombat()')) "Enemy movement is not limited to combat."
Assert-Contract ($difficultySource.Contains('npcType == NpcType.Boss') -and $difficultySource.Contains('npcType == NpcType.MiniBoss')) "Boss and miniboss movement is not preserved."
Assert-Contract ($difficultySource.Contains('weight >= 250 ? 0.0f : weight >= 150 ? 0.5f : 1.0f')) "Large and massive enemy movement tiers are missing."
Assert-Contract ($difficultySource.Contains('npcType == NpcType.Critter') -and $difficultySource.Contains('!npc.Template.requiresPathToTarget')) "Scripted critters or non-pathing actors can receive enemy movement scaling."
Assert-Contract ($difficultySource.Contains('targetClass.IsBossClass') -and $difficultySource.Contains('targetClass.IsBear')) "Boss metadata and bear movement exclusions are missing."
Assert-Contract ($difficultySource.Contains('targetClass.IsConstruct') -and $difficultySource.Contains('targetClass.IsFlora')) "Construct and flora movement exclusions are missing."
Assert-Contract ($difficultySource.Contains('npcType == NpcType.Elite || targetClass.IsBulkyMonster')) "Elite, Beholder, and Slugholder movement caps are missing."
Assert-Contract ($mainSource.Contains('EnemyMovementBearTerms = { "AnimalBear", "Forlorn Bear" }')) "Bear templates are not recognized for movement exclusion."
Assert-Contract ($mainSource.Contains('EnemyMovementBulkyMonsterTerms = { "Beholder", "Slugholder" }')) "Bulky monster movement terms are missing."
Assert-Contract ($difficultySource.Contains('targetClass.ArmorProfile.Tier')) "Special humanoid equipment is not considered for enemy movement scaling."
Assert-Contract ($difficultySource.Contains('armorTier == EnemyArmorTier.Heavy')) "Heavy-armored enemy movement is not preserved."
Assert-Contract ($difficultySource.Contains('armorTier == EnemyArmorTier.Medium')) "Medium-armored enemies do not receive the half-strength movement tier."
Assert-Contract ($difficultySource.Contains('ApplyEnemySightRangeTweak(npc);') -and $difficultySource.Contains('ApplyEnemyMovementSpeedTweak(npc);')) "Enemy sight and movement do not share one reconciliation pass."
Assert-Contract ($difficultySource.Contains('"ModifyPlayerArrowDrop"')) "Player arrow-drop control is not independently toggleable."
Assert-Contract ($difficultySource.Contains('"PlayerArrowGravityMultiplier"')) "Player arrow gravity is not independently configurable."
Assert-Contract ($difficultySource.Contains('new AcceptableValueRange<float>(0.25f, 1.0f)')) "Player arrow gravity range is not 0.25 to 1.00."
Assert-Contract ($difficultySource.Contains('projectile is Arrow')) "Player arrow gravity is not limited to arrows."
Assert-Contract ($difficultySource.Contains('ReferenceEquals(projectile.Owner, Hero.Current)')) "Player arrow gravity is not limited to player-owned arrows."
Assert-Contract ($difficultySource.Contains('body.isKinematic || !body.useGravity')) "Player arrow gravity does not preserve inactive or gravity-disabled projectile states."
Assert-Contract ($difficultySource.Contains('float localTimeScale = deltaTime / fixedDeltaTime;')) "Player arrow gravity compensation does not derive the projectile's local time scale."
Assert-Contract ($difficultySource.Contains('float cancellationScale = (1.0f - gravityMultiplier) * localTimeScale * localTimeScale;')) "Player arrow gravity compensation does not follow the game's squared Rigidbody time scaling."
Assert-Contract ($difficultySource.Contains('body.AddForce(-Physics.gravity * cancellationScale, ForceMode.Acceleration);')) "Player arrow gravity compensation does not reduce native gravity acceleration."

$gravityMultiplier = 0.75
foreach ($localTimeScale in @(1.0, 0.5, 0.25)) {
    $nativeGravityScale = $localTimeScale * $localTimeScale
    $cancellationScale = (1.0 - $gravityMultiplier) * $nativeGravityScale
    $effectiveGravityScale = $nativeGravityScale - $cancellationScale
    Assert-Contract ([Math]::Abs($effectiveGravityScale - ($gravityMultiplier * $nativeGravityScale)) -lt 0.000001) "Player arrow gravity compensation drifts at local time scale $localTimeScale."
}

$boundEntryFields = @(
    [regex]::Matches(
        $mainSource + [Environment]::NewLine + $difficultySource,
        '\b(_[A-Za-z][A-Za-z0-9_]*)\s*=\s*Config\.Bind') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
)
$restoredEntryFields = @(
    [regex]::Matches(
        $mainSource,
        'RestorePreservedSetting\(profile,\s*(_[A-Za-z][A-Za-z0-9_]*)\s*,') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
)
$missingAutomaticRecovery = @(
    $boundEntryFields | Where-Object { $restoredEntryFields -notcontains $_ }
)
Assert-Contract ($missingAutomaticRecovery.Count -eq 0) ("Automatic config preservation omits bound fields: " + ($missingAutomaticRecovery -join ", "))

$requiredSettings = @(
    "DifficultyModifiersEnabled",
    "ModifyPlayerDamageDealt",
    "WeakSpotDamageBonus",
    "ModifyPlayerDamageTaken",
    "PassiveShieldProtectionEnabled",
    "ModifyStaminaUsage",
    "ModifyManaUsage",
    "ModifyCombatManaRegeneration",
    "CombatManaRegenerationMultiplier",
    "ModifyParryWindowBonus",
    "PositiveParryWindowBonusMultiplier",
    "ModifyPlayerPoiseDamageDealt",
    "ProgressiveTenacityEnabled",
    "ModifyPlayerArrowVelocity",
    "ModifyPlayerArrowDrop",
    "PlayerArrowGravityMultiplier",
    "ModifyArmorWeightPenalties",
    "ModifyLightArmorMobility",
    "ModifyArmorPhysicalProtection",
    "ModifyPotionOverdrinking",
    "ModifyFoodRecovery",
    "ModifyEnemyAttackSlots",
    "EnemyAttackSlotCap",
    "ModifyEnemyAttackRecovery",
    "ModifyEnemyMovementSpeed",
    "ModifyHostileArrowVelocity",
    "HostileArcherAimScatter",
    "ModifyEnemySightRange",
    "ModifyEnemyHearingRange",
    "ModifyEnemyAggroPersistence",
    "ModifyKillExperience",
    "ModifyQuestExperience",
    "ModifyProficiencyExperience"
)

foreach ($setting in $requiredSettings) {
    Assert-Contract ($difficultySource.Contains('"' + $setting + '"')) "Missing setting $setting."
}

Assert-Contract ($difficultySource.Contains('return 0.05f;')) "Hardened 5 percent preset value is missing."
Assert-Contract ($difficultySource.Contains('return 0.10f;')) "Crucible 10 percent preset value is missing."
Assert-Contract ($difficultySource.Contains('return 1.0f - PresetPenaltyAmount();')) "Preset reduction math is missing."
Assert-Contract ($difficultySource.Contains('return 1.0f + PresetPenaltyAmount();')) "Preset cost math is missing."
Assert-Contract ([regex]::IsMatch($difficultySource, 'GetPresetCombatSustainabilityMultiplier[\s\S]*?case Preset\.Hardened:\s*return 0\.75f;[\s\S]*?case Preset\.Crucible:\s*return 0\.50f;[\s\S]*?case Preset\.Tempered:[\s\S]*?return 1\.0f;')) "Combat mana regeneration and positive parry-window bonuses are not x1.00/x0.75/x0.50 by preset."
Assert-Contract ($difficultySource.Contains('ApplyPresetCombatSustainabilityMultipliers();')) "Preset changes do not reset the combat-sustainability multipliers."
Assert-Contract ($difficultySource.Contains('nameof(Hero.ManaRegen)') -and $difficultySource.Contains('nameof(Hero.PredictedManaRegen)')) "Actual and predicted mana-regeneration getters are not both patched."
Assert-Contract ([regex]::IsMatch($difficultySource, 'ApplyCombatManaRegeneration[\s\S]*?!hero\.HeroCombat\.IsHeroInFight[\s\S]*?regeneration <= 0\.0f[\s\S]*?Mathf\.Lerp\(configuredMultiplier, 1\.0f, manaShield\)[\s\S]*?regeneration \*= effectiveMultiplier;')) "Combat mana regeneration does not preserve noncombat and non-positive values or proportionally relieve its penalty with Mana Shield."
Assert-Contract ($difficultySource.Contains('nameof(HeroParry.Parry)') -and $difficultySource.Contains('NativeBaseParryWindowSeconds = 0.05f')) "The native HeroParry route or 0.05-second base is not recorded."
Assert-Contract ([regex]::IsMatch($difficultySource, 'ApplyPositiveParryWindowBonus[\s\S]*?OriginalTime <= NativeBaseParryWindowSeconds \+ NeutralTolerance[\s\S]*?float positiveBonus = timeDuration\.OriginalTime - NativeBaseParryWindowSeconds;[\s\S]*?NativeBaseParryWindowSeconds \+ \(positiveBonus \* multiplier\)')) "Positive parry-window scaling does not preserve the native base and non-positive totals."
Assert-Contract ($mainSource.Contains('RestorePreservedSetting(profile, _modifyCombatManaRegeneration') -and $mainSource.Contains('RestorePreservedSetting(profile, _combatManaRegenerationMultiplier') -and $mainSource.Contains('RestorePreservedSetting(profile, _modifyParryWindowBonus') -and $mainSource.Contains('RestorePreservedSetting(profile, _positiveParryWindowBonusMultiplier')) "New combat-sustainability settings are not included in previous-settings recovery."
Assert-Contract ([regex]::IsMatch($difficultySource, 'private float PresetPlayerPressureAmount\(\)[\s\S]*?case Preset\.Hardened:\s*return 0\.10f;[\s\S]*?case Preset\.Crucible:\s*return 0\.15f;[\s\S]*?case Preset\.Tempered:[\s\S]*?return 0\.05f;')) "Player pressure is not 5/10/15 percent for Tempered/Hardened/Crucible."
Assert-Contract (-not $difficultySource.Contains('_playerDamageDealtMultiplier')) "Removed PlayerDamageDealtMultiplier field or runtime reference remains."
Assert-Contract (-not [regex]::IsMatch($difficultySource, 'Config\.Bind\(\s*"6\. Difficulty - Player",\s*"PlayerDamageDealtMultiplier"')) "Removed PlayerDamageDealtMultiplier config binding remains."
Assert-Contract (-not $mainSource.Contains('RestorePreservedSetting(profile, _playerDamageDealtMultiplier')) "Removed PlayerDamageDealtMultiplier preservation reference remains."
Assert-Contract ([regex]::IsMatch($difficultySource, 'ApplyOutgoingHealthDamageModifier[\s\S]*?float multiplier = PresetPlayerPressureReductionMultiplier\(\);')) "Outgoing player damage does not use the preset reduction directly."
Assert-Contract ([regex]::IsMatch($difficultySource, 'private bool OutgoingDamageModifierIsEffective\(\)[\s\S]*?float multiplier = PresetPlayerPressureReductionMultiplier\(\);')) "Outgoing overlap effectiveness does not depend only on the toggle/master state and preset pressure."
Assert-Contract ($difficultySource.Contains('float multiplier = PresetPlayerPressureCostMultiplier();')) "Incoming player damage does not use the 5/10/15 pressure mapping."
Assert-Contract ([regex]::IsMatch($difficultySource, 'ApplyKillExperience[\s\S]*?PresetPlayerPressureReductionMultiplier\(\)[\s\S]*?ApplyQuestExperience[\s\S]*?PresetPlayerPressureReductionMultiplier\(\)[\s\S]*?ApplyProficiencyExperience[\s\S]*?PresetPlayerPressureReductionMultiplier\(\)')) "Kill, quest, and proficiency experience do not all use the 5/10/15 pressure mapping."
Assert-Contract ([regex]::IsMatch($difficultySource, 'ApplyEnemyAttackRecovery[\s\S]*?float multiplier = PresetReductionMultiplier\(\);')) "Enemy recovery no longer uses the existing 0/5/10 supporting profile."
Assert-Contract ($difficultySource.Contains('return 1.10f;')) "Tempered arrow velocity is not x1.10."
Assert-Contract ($difficultySource.Contains('return 1.30f;')) "Hardened arrow velocity is not x1.30."
Assert-Contract ($difficultySource.Contains('return 1.50f;')) "Crucible arrow velocity is not x1.50."
Assert-Contract ([regex]::IsMatch($difficultySource, 'GetPresetHostileArcherAimScatter[\s\S]*?case Preset\.Tempered:\s*return 1\.50f;[\s\S]*?case Preset\.Crucible:\s*return 1\.00f;[\s\S]*?case Preset\.Hardened:[\s\S]*?return 1\.25f;')) "Hostile archer aim scatter is not 1.50/1.25/1.00 meters by preset."
Assert-Contract ($difficultySource.Contains('ApplyPresetHostileArcherAimScatter();')) "Preset changes do not reset hostile archer aim scatter."
Assert-Contract ($difficultySource.Contains('new AcceptableValueRange<float>(0.0f, 2.0f)')) "Hostile archer aim-scatter range is not 0.00 to 2.00 meters."
Assert-Contract ([regex]::IsMatch($difficultySource, 'PresetEnemySightRangeMultiplier[\s\S]*?case Preset\.Hardened:\s*return 1\.40f;[\s\S]*?case Preset\.Crucible:\s*return 1\.60f;[\s\S]*?case Preset\.Tempered:[\s\S]*?return 1\.20f;')) "Enemy sight range is not x1.20/x1.40/x1.60 by preset."
Assert-Contract ($difficultySource.Contains('return heavy ? 1.10f : 1.05f;')) "Hardened physical armor values are missing."
Assert-Contract ($difficultySource.Contains('return heavy ? 1.20f : 1.10f;')) "Crucible physical armor values are missing."
Assert-Contract ($difficultySource.Contains('return 1.025f;')) "Hardened Light armor mobility is not x1.025."
Assert-Contract ($difficultySource.Contains('case Preset.Hardened:') -and $difficultySource.Contains('return 1;')) "Hardened attack-slot bonus is missing."
Assert-Contract ($difficultySource.Contains('case Preset.Crucible:') -and $difficultySource.Contains('return 2;')) "Crucible attack-slot bonus is missing."

$requiredHooks = @(
    "CharacterStats.CharacterStatsWrapper",
    "AINoises.MakeHeroFootstepNoise",
    "NpcAIDistancesUtils.CombatAggroDecreaseModifierByDistanceToLastIdlePoint",
    "ItemSkillsInvoker.PerformImmediate",
    "MaxEnemiesAttacking",
    "AttackActionUnBookProlong",
    "GetExpReward",
    "Quest.ExperiencePoints",
    "Objective.ExperiencePoints",
    "TryAddXP",
    "NpcGeneralFSM",
    "BowFSM.FireProjectileInternal",
    "DamageDealingProjectile.ProcessFixedUpdate",
    "CombatBehaviourUtils.FireProjectile",
    "Hero.TotalArmor"
)

foreach ($hook in $requiredHooks) {
    Assert-Contract ($difficultySource.Contains($hook)) "Missing hook contract for $hook."
}

$targetGuard = $mainSource.IndexOf("bool targetIsHero", [StringComparison]::Ordinal)
$incomingApplication = $mainSource.IndexOf("ApplyIncomingHealthDamageModifier", [StringComparison]::Ordinal)
$sourceGuard = $mainSource.IndexOf("if (!IsHeroDamageSource", [StringComparison]::Ordinal)
Assert-Contract ($targetGuard -ge 0 -and $incomingApplication -gt $targetGuard -and $sourceGuard -gt $incomingApplication) "Incoming damage does not run before the hero-source guard."

Assert-Contract (-not $difficultySource.Contains("ChangingStatWealth")) "A coin hook was added to the 3.0 difficulty source."
Assert-Contract ($difficultySource.Contains("HeroStats.ArmorPenaltyMultiplier")) "Native armor-penalty scaling is missing."
Assert-Contract ($difficultySource.Contains("stats.MovementSpeedMultiplier")) "Light armor movement scaling is missing."
Assert-Contract ($difficultySource.Contains("IsPhysicalDamageSubtype")) "Physical-only armor filtering is missing."
Assert-Contract ($difficultySource.Contains("EquipmentSlotType.Quiver")) "Hostile projectile filtering is not limited to arrows."
Assert-Contract ($difficultySource.Contains("IsHostileTo(Hero.Current)")) "Hostile arrow ownership filtering is missing."
Assert-Contract ($difficultySource.Contains('shootParams.shooter is NpcElement')) "Hostile archer tuning is not limited to NPC shooters."
Assert-Contract ($difficultySource.Contains('ref CombatBehaviourUtils.FireProjectileParams fireParams')) "Hostile archer tuning does not modify the native fire parameters by reference."
Assert-Contract ($difficultySource.Contains('fireParams.inaccuracy = Mathf.Max(before, scatter);')) "Hostile archer tuning does not preserve larger authored inaccuracy."
Assert-Contract ($difficultySource.Contains("Mathf.Clamp") -and $difficultySource.Contains("ScaleBallisticVelocity")) "Hostile velocity is not scaled at the ballistic clamp."
Assert-Contract ($difficultySource.Contains("World.All<NpcElement>()")) "Loaded-NPC sight-range reconciliation is missing."
Assert-Contract ($difficultySource.Contains("NpcStats.SightLengthMultiplier")) "Native NPC sight-distance stat is not used."
Assert-Contract ($difficultySource.Contains("EnemySightRangeTweak : StatTweak")) "Owned enemy sight-range tweak is missing."
Assert-Contract ($difficultySource.Contains("MarkedNotSaved = true")) "Enemy sight-range tweak is not marked non-saved."
Assert-Contract ($difficultySource.Contains("npc.IsAlive") -and $difficultySource.Contains("!npc.IsSummonOrAlly")) "Enemy sight eligibility lacks life or ally filtering."
Assert-Contract ($difficultySource.Contains("npc.NpcAI.Working")) "Enemy sight eligibility lacks active-AI filtering."
Assert-Contract ($difficultySource.Contains("WithFactionUtils.IsHostileToHero(npc)")) "Enemy sight eligibility lacks hostility filtering."
Assert-Contract ($difficultySource.Contains("RemoveAllEnemySightRangeTweaks")) "Enemy sight-range shutdown cleanup is missing."
Assert-Contract ($difficultySource.Contains('private float PresetEnemyHearingRangeMultiplier()')) "Enemy hearing-range preset mapping is missing."
Assert-Contract ([regex]::IsMatch($difficultySource, 'PresetEnemyHearingRangeMultiplier[\s\S]*?case Preset\.Hardened:\s*return 1\.40f;[\s\S]*?case Preset\.Crucible:\s*return 1\.60f;[\s\S]*?case Preset\.Tempered:[\s\S]*?return 1\.20f;')) "Enemy hearing range is not x1.20/x1.40/x1.60 by preset."
Assert-Contract ($difficultySource.Contains('typeof(AINoises)') -and $difficultySource.Contains('"MakeHeroFootstepNoise"')) "Enemy hearing does not patch the native hero-footstep noise route."
Assert-Contract ($difficultySource.Contains('noiseRange *= multiplier;')) "Enemy hearing does not preserve native noise strength while scaling range."
Assert-Contract ($difficultySource.Contains('EnemyHearingRangeDiagnosticIntervalSeconds = 2.0f')) "Enemy hearing diagnostics are not capped at one message every two seconds."
Assert-Contract ($difficultySource.Contains('LogEnemyHearingRangeDiagnostic(before, noiseRange, multiplier);')) "Enemy hearing bypasses its dedicated diagnostic throttle."
Assert-Contract ($difficultySource.Contains('DifficultyDiagnosticIntervalSeconds = 1.0f') -and $difficultySource.Contains('_nextDifficultyDiagnosticByLever.TryGetValue')) "High-frequency difficulty diagnostics are not throttled per lever."
Assert-Contract ($difficultySource.Contains('private float PresetEnemyAggroPersistenceMultiplier()')) "Enemy aggro-persistence preset mapping is missing."
Assert-Contract ([regex]::IsMatch($difficultySource, 'PresetEnemyAggroPersistenceMultiplier[\s\S]*?case Preset\.Hardened:\s*return 1\.40f;[\s\S]*?case Preset\.Crucible:\s*return 1\.60f;[\s\S]*?case Preset\.Tempered:[\s\S]*?return 1\.20f;')) "Enemy aggro persistence is not x1.20/x1.40/x1.60 by preset."
Assert-Contract ($difficultySource.Contains('aggroDecreaseModifier /= multiplier;')) "Enemy aggro persistence does not scale native aggro decay."
Assert-Contract (-not [regex]::IsMatch($difficultySource, 'AccessTools\.Method\([^\)]*"ShouldForceEnd(?:Combat|Alert)"')) "Enemy aggro persistence patches a forced combat or alert exit route."
Assert-Contract ($difficultySource.Contains('PotionPoisoningBuildupPerPotion = 40.0f')) "Potion Poisoning does not add 40 buildup per potion."
Assert-Contract ([regex]::IsMatch($difficultySource, 'PresetPotionPoisoningDecayPerSecond[\s\S]*?case Preset\.Hardened:\s*return 2\.0f;[\s\S]*?case Preset\.Crucible:\s*return 4\.0f / 3\.0f;[\s\S]*?case Preset\.Tempered:[\s\S]*?return 4\.0f;')) "Potion Poisoning decay is not 4/2/1.333 per second by preset."
Assert-Contract ($difficultySource.Contains('PotionPoisoningStatusGuid = "60a2ed0287e14c944b53b6ab5870becd"')) "Potion Poisoning tuning does not target the audited native status."
Assert-Contract ($difficultySource.Contains('NativePotionPoisoningBuildup = 60.0f')) "The audited native Potion Poisoning graph buildup is not recorded as 60."
Assert-Contract ($difficultySource.Contains('nameof(CharacterStatuses.BuildupStatus)')) "Potion Poisoning tuning does not patch the native buildup route."
Assert-Contract ($difficultySource.Contains('PotionPoisoningBucket') -and $difficultySource.Contains('Health = 1') -and $difficultySource.Contains('Mana = 2') -and $difficultySource.Contains('Stamina = 4') -and $difficultySource.Contains('Utility = 8')) "Potion Poisoning does not define the four independent class buckets."
Assert-Contract ($difficultySource.Contains('FlatPotionRestorationGraphGuid = "acd627b35fa17234aa6b00ea69faf646"')) "Flat restorative-potion classification does not use the audited graph."
Assert-Contract ($difficultySource.Contains('PercentPotionRestorationGraphGuid = "d5ab45e7eb066a84ea55f9ab4f78b92f"')) "Percentage restorative-potion classification does not use the audited graph."
Assert-Contract ($difficultySource.Contains('TimedPotionRestorationGraphGuid = "4d431d204820819429d8f7bac4177644"')) "Timed restorative-potion classification does not use the audited graph."
Assert-Contract ($difficultySource.Contains('buckets |= PotionPoisoningBucket.Health') -and $difficultySource.Contains('buckets |= PotionPoisoningBucket.Mana') -and $difficultySource.Contains('buckets |= PotionPoisoningBucket.Stamina')) "Direct restoratives do not select their resource buckets."
Assert-Contract ($difficultySource.Contains('? PotionPoisoningBucket.Utility')) "Non-restorative potions do not fall back to the Utility bucket."
Assert-Contract ($difficultySource.Contains('_healthPotionPoisoningBuildup += buildup') -and $difficultySource.Contains('_manaPotionPoisoningBuildup += buildup') -and $difficultySource.Contains('_staminaPotionPoisoningBuildup += buildup') -and $difficultySource.Contains('_utilityPotionPoisoningBuildup += buildup')) "Potion consumption does not update every matching class bucket."
Assert-Contract ($difficultySource.Contains('float buildup = PotionPoisoningBuildupPerPotion;') -and $difficultySource.Contains('* PresetPotionPoisoningDecayPerSecond();')) "Class buildup does not use the 40-point dose and preset decay."
Assert-Contract ($difficultySource.Contains('return false;') -and $difficultySource.Contains('SuppressedNativePotionPoisoning:')) "Potion-originated native buildup is not suppressed during class tracking."
Assert-Contract ($difficultySource.Contains('NativePotionPoisoningThreshold = 100.0f') -and $difficultySource.Contains('potionPoisoning.CompleteBuildup();')) "A completed class bucket does not activate the native status at its exact threshold."
Assert-Contract ($difficultySource.Contains('ClearPotionPoisoningBuckets(state.Hero);')) "Completing or entering Potion Poisoning does not clear every class bucket."
Assert-Contract ($difficultySource.Contains('IsPotionPoisoningActive(state.Hero)')) "Potion buildup does not pause while the native status is active."
Assert-Contract ($difficultySource.Contains('nameof(BuildupStatus.ActivateStatus)')) "Potion Poisoning penalty does not attach through native status activation."
Assert-Contract ($difficultySource.Contains('ResourcePotionPoisoningDrainFraction = 0.30f') -and $difficultySource.Contains('UtilityPotionPoisoningDrainFraction = 0.15f')) "Potion Poisoning does not use 30% resource-class and 15% Utility drains."
Assert-Contract ($difficultySource.Contains('_pendingPotionPoisoningPenaltyBuckets = completedBuckets')) "Completed Potion Poisoning buckets are not carried into native status activation."
Assert-Contract ($difficultySource.Contains('modifier.Value + 20.0f')) "The native flat maximum-Mana modifier is not removed before the timed drain."
Assert-Contract ($difficultySource.Contains('nameof(BuildupStatus.Decay)') -and $difficultySource.Contains('status.BuildupProgress')) "Potion Poisoning drain is not metered from native status progress."
Assert-Contract ($difficultySource.Contains('hero.MaxHealth.ModifiedValue * drainFraction') -and $difficultySource.Contains('hero.MaxMana.ModifiedValue * drainFraction') -and $difficultySource.Contains('hero.MaxStamina.ModifiedValue * drainFraction')) "Potion Poisoning does not snapshot every matching maximum resource."
Assert-Contract ($difficultySource.Contains('stat.DecreaseBy(Mathf.Min(plannedDrain, available))')) "Potion Poisoning does not drain the current resource through its limited-stat route."
Assert-Contract ($difficultySource.Contains('ref _potionPoisoningHealthDrainRemaining') -and $difficultySource.Contains('1.0f);')) "Health Potion Poisoning is not floored at 1 HP."
Assert-Contract ($difficultySource.Contains('Drains 15% of maximum HP, MP, and SP over this status.')) "Utility Potion Poisoning status text does not describe its all-resource drain."
Assert-Contract ($difficultySource.Contains('"Drains 30% of maximum " + resourceText')) "Resource Potion Poisoning status text does not describe its matching drain."
Assert-Contract ($difficultySource.Contains('string staminaText = "+" + staminaTotal + "/" + duration + "s";') -and -not $difficultySource.Contains('staminaTotal + "ST/"')) "Better UI food stamina text is not an unlabeled total over duration."
Assert-Contract (-not $difficultySource.Contains('OrdinaryHealthPotionTemplateGuid')) "Potion classification still relies on the old ordinary-Health-potion exception."
Assert-Contract (-not $difficultySource.Contains('NativePotionOverdrinkGraphGuid')) "Potion classification still relies on native overdrink-graph presence."
Assert-Contract (-not $difficultySource.Contains('TargetedHealthPotionTemplateGuids')) "Potion healing still uses a custom template allowlist."
Assert-Contract (-not $difficultySource.Contains('PotionHealingStatusSourceId')) "A custom potion-healing queue still exists."
Assert-Contract (-not $difficultySource.Contains('BuildPotionHealingDescription')) "Potion tooltip text is still replaced by Steel and Bone."
Assert-Contract ($difficultySource.Contains('ConsumableRecoveryPatchState __state')) "Food recovery is not transactional per invocation."
Assert-Contract (-not $difficultySource.Contains('"ModifyConsumableRecovery"')) "The removed blanket restorative-consumable setting is still bound or reported."
Assert-Contract ($difficultySource.Contains('StandardFoodRecoveryGraphGuid = "1c2da8428b5a74142b93ed84593676a9"')) "Food recovery does not target the audited native standard-food graph."
Assert-Contract ($difficultySource.Contains('FoodRecoveryStatusGuid = "432685012b6577f48a92c6ae8eb377cb"')) "Food stamina recovery does not use the audited native standard-food status."
Assert-Contract ($difficultySource.Contains('!item.IsEdible') -and $difficultySource.Contains('item.Template.IsPotion')) "Food recovery is not classified through native edible and potion ancestry."
Assert-Contract ([regex]::IsMatch($difficultySource, 'PresetFoodHealthRateMultiplier[\s\S]*?case Preset\.Hardened:\s*return 0\.375f;[\s\S]*?case Preset\.Crucible:\s*return 0\.25f;[\s\S]*?case Preset\.Tempered:[\s\S]*?return 0\.5f;')) "Food health rate is not x0.50/x0.375/x0.25 by preset."
Assert-Contract ([regex]::IsMatch($difficultySource, 'PresetFoodHealthDurationMultiplier[\s\S]*?return 4\.0f;')) "Food health duration is not x4 on every preset."
Assert-Contract ([regex]::IsMatch($difficultySource, 'PresetFoodStaminaRate[\s\S]*?return 1\.0f;')) "Food stamina recovery is not 1 point per second on every preset."
Assert-Contract ($difficultySource.Contains('skill.OverrideVariable("AddValue", addValue.Value * rateMultiplier);')) "Food recovery does not scale the native health rate."
Assert-Contract ($difficultySource.Contains('skill.OverrideVariable("Gain", gain.Value * rateMultiplier);')) "Food recovery does not scale level gain with the native health rate."
Assert-Contract ($difficultySource.Contains('skill.OverrideVariable("Duration", authoredDuration.Value * durationMultiplier);')) "Food recovery does not scale the native health duration."
Assert-Contract ($difficultySource.Contains('SkillVariableOverridesField.SetValue(snapshot.Skill, snapshot.VariableOverrides);')) "Temporary food graph overrides are not restored exactly."
Assert-Contract ($difficultySource.Contains('state.RecoveryDuration') -and $difficultySource.Contains('authoredDuration.Value * durationMultiplier')) "Food stamina does not share the adjusted health duration."
Assert-Contract ($difficultySource.Contains('CaptureActiveFoodStatuses(hero, state.ExistingFoodStatuses);')) "Food consumption does not snapshot existing native food statuses."
Assert-Contract ($difficultySource.Contains('!state.ExistingFoodStatuses.Contains(status)')) "Food consumption does not select the newly created native food status."
Assert-Contract ($difficultySource.Contains('foodStatus.Skill.OverrideVariable(FoodStaminaRateVariable, staminaRate);')) "The native food status does not save the added stamina rate."
Assert-Contract ($difficultySource.Contains('FoodRecoveryDurationVariable')) "The native food status does not save the shared recovery duration."
Assert-Contract ($difficultySource.Contains('typeof(VHeroController)') -and $difficultySource.Contains('"UpdateStats"')) "Direct food stamina recovery is not patched into the player-stat update."
Assert-Contract ($difficultySource.Contains('staminaPerTick = Mathf.Max(staminaPerTick, staminaRate.Value);')) "Food stamina can stack simultaneous marked food rates."
Assert-Contract ($difficultySource.Contains('Mathf.FloorToInt(_foodStaminaTickElapsed / FoodStaminaTickSeconds)')) "Food stamina does not use discrete elapsed-second ticks."
Assert-Contract ($difficultySource.Contains('hero.Stamina.IncreaseBy(staminaPerTick * ticks);')) "Food stamina does not restore whole tick values to the limited stamina stat."
Assert-Contract (-not $difficultySource.Contains('hero.Stamina.IncreaseBy(totalStaminaRate * deltaTime);')) "Food stamina still restores fractional values every frame."
Assert-Contract ($difficultySource.Contains('FoodOverexertionDurationMultiplier = 0.50f')) "Active food does not halve native overexertion duration."
Assert-Contract ($difficultySource.Contains('FoodStaminaPostOverexertionDelaySeconds = 0.10f')) "The first post-overexertion food stamina point is not delayed by 0.1 seconds."
Assert-Contract ($difficultySource.Contains('"PreventFoodUseInCombat"') -and $difficultySource.Contains('"Prevent Food Use In Combat"')) "The configurable food combat-use restriction is missing."
Assert-Contract ($difficultySource.Contains('AccessTools.Method(typeof(Item), nameof(Item.Use), Type.EmptyTypes)')) "Food combat-use restriction does not patch Item.Use before consumption."
Assert-Contract ($difficultySource.Contains('!item.Template.IsPlainFood && !item.Template.IsDish')) "Food combat-use restriction does not use native food and dish identity."
Assert-Contract ($difficultySource.Contains('!ReferenceEquals(item.Owner.Character, hero)') -and $difficultySource.Contains('hero.HeroCombat.IsHeroInFight')) "Food combat-use restriction lacks hero ownership or native combat-state guards."
Assert-Contract ([regex]::IsMatch($difficultySource, 'GetPresetPreventFoodUseInCombat[\s\S]*?case Preset\.Tempered:\s*return false;[\s\S]*?case Preset\.Hardened:[\s\S]*?case Preset\.Crucible:[\s\S]*?return true;')) "Food combat-use restriction is not false/true/true by preset."
Assert-Contract ($difficultySource.Contains('ApplyPresetPreventFoodUseInCombat();')) "Changing Preset does not reset the food combat-use setting."
Assert-Contract ($difficultySource.Contains('_preventFoodUseInCombat.Value = presetValue;')) "Preset food combat-use values are not applied to the config entry."
$foodCombatFeedbackMethod = [regex]::Match($difficultySource, 'private void ShowFoodCombatRestrictionNotification\(\)[\s\S]*?(?=\r?\n        private void ApplyConsumableRecovery)').Value
Assert-Contract ($difficultySource.Contains('Food cannot be consumed during combat.')) "Blocked food use lacks concise Grail Floating Text feedback."
Assert-Contract ($difficultySource.Contains('TryShowEventNotification(') -and $difficultySource.Contains('"Gold"') -and $difficultySource.Contains('"food"') -and $difficultySource.Contains('"VeryShort"')) "Blocked food-use feedback does not use the GFT food presentation."
Assert-Contract (-not $difficultySource.Contains('Food cannot be consumed during combat on this preset.')) "Blocked food use still contains the retired native HUD message."
Assert-Contract ($foodCombatFeedbackMethod.Contains('TryShowEventNotification(') -and -not $foodCombatFeedbackMethod.Contains('NotificationBuffer') -and -not $foodCombatFeedbackMethod.Contains('PushNotification')) "Blocked food use still relies on native HUD notification infrastructure."
Assert-Contract ($difficultySource.Contains('FoodCombatNotificationCooldownSeconds = 0.75f')) "Blocked food-use feedback lacks duplicate suppression."
Assert-Contract ($difficultySource.Contains('nameof(PreventStaminaRegenDuration.PreventWithStatus)')) "Food-supported overexertion does not patch the paired native regeneration lock and depleted status."
Assert-Contract ($difficultySource.Contains('blockType != StaminaRegenBlockType.Overexertion')) "Food-supported exhaustion can affect ordinary stamina regeneration lockouts."
Assert-Contract ($difficultySource.Contains('regenDuration.TimeLeft * FoodOverexertionDurationMultiplier') -and $difficultySource.Contains('statusDuration.TimeLeft * FoodOverexertionDurationMultiplier')) "Food-supported exhaustion does not scale the native lock and status together."
Assert-Contract ([regex]::IsMatch($difficultySource, 'if \(HasActiveOverexertion\(hero\)\)\s*\{\s*_foodStaminaTickElapsed = 0\.0f;\s*_foodStaminaWasSuspendedByOverexertion = true;\s*return;')) "Food stamina does not record native overexertion suspension."
Assert-Contract ([regex]::IsMatch($difficultySource, 'if \(_foodStaminaWasSuspendedByOverexertion\)[\s\S]*?_foodStaminaTickElapsed = FoodStaminaTickSeconds\s*- FoodStaminaPostOverexertionDelaySeconds;')) "Food stamina does not preload the half-second post-overexertion interval."
Assert-Contract ([regex]::IsMatch($difficultySource, 'if \(!FoodRecoveryModifierIsEffective\(\)\)[\s\S]*?_foodStaminaWasSuspendedByOverexertion = false;\s*return;')) "Disabling food recovery does not discard the pending post-overexertion point."
Assert-Contract ($difficultySource.Contains('EnforceSingleFoodRecoveryStatus(hero);')) "Food statuses are not reconciled during the player-stat update."
Assert-Contract ($difficultySource.Contains('RemainingFoodHealing(hero, candidate)')) "Food arbitration does not compare remaining queued healing."
Assert-Contract ($difficultySource.Contains('hero.HealthRegen.GetPrediction(passive)')) "Food arbitration does not use each status's native health prediction contribution."
Assert-Contract ($difficultySource.Contains('candidateTime > winningTime')) "Food arbitration does not use remaining duration as its tie-breaker."
Assert-Contract ($difficultySource.Contains('hero.Statuses.RemoveStatus(foodStatuses[i]);')) "Food arbitration does not remove losing native statuses."
Assert-Contract (-not $difficultySource.Contains('new RichEnumReference(CharacterStatType.StaminaRegen)')) "Food stamina still relies on vanilla StaminaRegen."
Assert-Contract ($difficultySource.Contains('nameof(Status.StatusDescription)') -and $difficultySource.Contains('AppendActiveFoodStaminaDescription')) "The combined native food status does not describe its stamina channel."
Assert-Contract ($difficultySource.Contains('LegacyFoodStaminaStatusSourceId')) "Legacy separate food-stamina statuses are not recognized for cleanup."
Assert-Contract ($difficultySource.Contains('typeof(ExistingItemDescriptor)') -and $difficultySource.Contains('nameof(ExistingItemDescriptor.ItemDescription)')) "Food tooltip values are not resolved through the shared item descriptor."
Assert-Contract ($difficultySource.Contains('description.TrimEnd() + Environment.NewLine + staminaLine')) "Food tooltip text does not preserve native lines while adding the stamina effect once."
Assert-Contract ($difficultySource.Contains('"s. While active, halves Stamina Depleted duration; stamina ticks pause during it, and the first point follows 0.1s later."')) "Food stamina text does not explain its post-overexertion timing."
Assert-Contract ($difficultySource.Contains('"StaminaDepletedVignetteMode"') -and $difficultySource.Contains('StaminaDepletedVignetteMode.Smooth')) "The Stamina Depleted vignette does not default to Smooth mode."
Assert-Contract ($difficultySource.Contains('"StaminaDepletedVignetteFadeSeconds"') -and $difficultySource.Contains('0.30f')) "The Smooth Stamina Depleted vignette does not default to a 0.30-second fade."
Assert-Contract ($difficultySource.Contains('StaminaDepletedVignetteMode.Native') -and $difficultySource.Contains('StaminaDepletedVignetteMode.Off')) "The native and disabled Stamina Depleted vignette modes are missing."
Assert-Contract ($difficultySource.Contains('nameof(VHeroStaminaUsedUpEffect.StartFlash)') -and $difficultySource.Contains('nameof(VHeroStaminaUsedUpEffect.StopFlash)')) "Stamina Depleted vignette control does not preserve the native view lifecycle."
Assert-Contract ($difficultySource.Contains('tween.Kill(false);')) "Smooth Stamina Depleted presentation does not stop the native repeating image tween."
Assert-Contract ($difficultySource.Contains('Mathf.SmoothStep(0.0f, 1.0f, progress)') -and $difficultySource.Contains('Time.unscaledDeltaTime')) "Smooth Stamina Depleted presentation does not use an eased unscaled fade."
Assert-Contract ($difficultySource.Contains('_staminaVignetteImage.enabled = false;') -and $difficultySource.Contains('VolumeStaminaUsedUp.SetWeightInstant(weight)')) "Off mode does not hide both native Stamina Depleted visual layers."
Assert-Contract ($mainSource.Contains('RestorePreservedSetting(profile, _staminaDepletedVignetteMode')) "Vignette mode is missing from automatic config preservation."
Assert-Contract ($mainSource.Contains('RestorePreservedSetting(profile, _staminaDepletedVignetteFadeSeconds')) "Vignette fade duration is missing from automatic config preservation."
Assert-Contract ($difficultySource.Contains('RestoreFoodSkillOverrides(__state == null ? null : __state.Food)')) "Food tooltip patches do not restore temporary graph values."
Assert-Contract ($mainSource.Contains('[BepInDependency(BetterUiPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]')) "Better UI is not declared as an optional load-order dependency."
Assert-Contract ($difficultySource.Contains('Better_UI.Patches.ConsumableEffectHelper')) "Better UI consumable-overlay compatibility is missing."
Assert-Contract ($difficultySource.Contains('" <color=#66FF66>"')) "Better UI food overlays do not append a green stamina value."
Assert-Contract ($difficultySource.Contains("CustomDifficultyPluginGuid")) "Custom Difficulty overlap detection is missing."
Assert-Contract ($difficultySource.Contains("HarderLifePluginGuid")) "HarderLife overlap detection is missing."
Assert-Contract ($difficultySource.Contains("TaintedCombatPluginGuid")) "Tainted Combat overlap detection is missing."
Assert-Contract ($difficultySource.Contains("TaintedInstinctsPluginGuid")) "Tainted Instincts overlap detection is missing."
Assert-Contract ($difficultySource.Contains("FlatArrowsPluginGuid")) "Flat Arrows overlap detection is missing."
Assert-Contract ($difficultySource.Contains('ReadExternalBool(plugin, "AMOD", "EnableArrowModifications", true)')) "Flat Arrows overlap detection does not honor its arrow-modification switch."
Assert-Contract ($difficultySource.Contains('ReportCompatibilityOverlap("Flat Arrows", conflicts)')) "Flat Arrows conflicts are not reported through the shared silent-overlap policy."
Assert-Contract ($difficultySource.Contains('ReportCompatibilityOverlap("Tainted Instincts", conflicts)')) "Tainted Instincts conflicts are not reported through the shared silent-overlap policy."
Assert-Contract ($difficultySource.Contains('ReportCompatibilityOverlap("HarderLife", conflicts)')) "HarderLife conflicts are not reported through the shared silent-overlap policy."
Assert-Contract ($difficultySource.Contains('"HearingRangeMultiplier"') -and $difficultySource.Contains('"AggroPersistenceMultiplier"')) "HarderLife enemy-awareness overlap detection is incomplete."
Assert-Contract ($difficultySource.Contains('"PotionEffectivenessMultiplier"') -and $difficultySource.Contains('"ConsumableEffectivenessMultiplier"')) "HarderLife consumable overlap detection is incomplete."
Assert-Contract ($difficultySource.Contains("conflicts.Count == 0")) "Silent no-overlap behavior is missing."
Assert-Contract ($difficultySource.Contains('bool momentumOverlap = momentum')) "Tainted Combat custom momentum detection is missing."
Assert-Contract ($difficultySource.Contains('ReadExternalBool(') -and $difficultySource.Contains('"Consumable Rules"') -and $difficultySource.Contains('"AffectFoodAndDishes"')) "Tainted Combat food-cooldown overlap detection is missing."
Assert-Contract ($difficultySource.Contains('FoodRecoveryModifierIsEffective() && foodCooldownOverlap') -and $difficultySource.Contains('FoodCombatRestrictionIsEffective() && foodCooldownOverlap')) "Tainted Combat food cooldown is not compared with both Steel and Bone food systems."
Assert-Contract ($difficultySource.Contains('staminaOverlap = momentumOverlap')) "Tainted Combat momentum does not report stamina overlap."
Assert-Contract ($difficultySource.Contains('recoveryOverlap = momentumOverlap')) "Tainted Combat momentum does not report enemy-recovery overlap."
Assert-Contract ($difficultySource.Contains('"Custom Guard"') -and $difficultySource.Contains('"ParryWindowBonus"') -and $difficultySource.Contains('ParryWindowBonusModifierIsEffective() && parryWindowOverlap') -and $difficultySource.Contains('"ModifyParryWindowBonus"')) "Tainted Combat parry-window overlap detection is missing."
Assert-Contract ($mainSource.Contains('"ArrowMaterialRulesEnabled"')) "Arrow material rules are not independently toggleable."
Assert-Contract ($mainSource.Contains('"ArmoredSpellWeaknessEnabled"')) "Armored spell weakness is not independently toggleable."
Assert-Contract ($mainSource.Contains('classification.IsArrow = IsArrowDamage(damage)')) "Direct arrow delivery is not classified separately from Pierce."
Assert-Contract ($mainSource.Contains('GetPhysicalDamageShare(damage, damageClass)')) "Arrow rules do not preserve independent elemental payload weighting."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.20f;')) "Confirmed skeleton arrow resistance is not 0.20 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.50f;')) "Construct or stone arrow resistance is not 0.50 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.55f;')) "Spirit arrow resistance is not 0.55 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.60f;')) "Flora or wood arrow resistance is not 0.60 on Hardened."
Assert-Contract ($mainSource.Contains('return "Exposed Flesh";') -and $mainSource.Contains('baseMultiplier = 1.20f;')) "Exposed humanoid flesh arrow weakness is missing."
Assert-Contract ($mainSource.Contains('targetLabel = "Flesh";') -and $mainSource.Contains('baseMultiplier = 1.12f;')) "Ordinary flesh arrow weakness is missing."
Assert-Contract ($mainSource.Contains('case EnemyArmorTier.Heavy:') -and $mainSource.Contains('tierBonus = 0.12f;')) "Heavy armor direct-spell tier bonus is missing."
Assert-Contract ($mainSource.Contains('return 0.10f;') -and $mainSource.Contains('EnemyArmorMaterial.Metal')) "Electric spells do not receive the intended Metal armor bonus."
Assert-Contract ($mainSource.Contains('damageClass.IsBloodMagic') -and $mainSource.Contains('damageClass.IsWyrdness')) "Biological or Wyrd spell identities are not excluded from generic armor bonuses."
Assert-Contract ($mainSource.Contains('GetOptionalBoolProperty(damage, "IgnoreArmor")')) "Armor-ignoring damage is not protected from duplicate armor interactions."
Assert-Contract ($mainSource.Contains('TryApplyWeightedDamageComposition(')) "Mixed hits do not use weighted damage composition."
Assert-Contract ($mainSource.Contains('weightedAdjustment += postVanillaShare * partAdjustment;')) "Per-part adjustments are not weighted by post-vanilla contribution."
Assert-Contract ($mainSource.Contains('amplificationRatio = amplifiedMultiplier / nativeMultiplier;')) "Vanilla amplification does not preserve the existing native multiplier."
Assert-Contract ($mainSource.Contains('part.SetTotalDamageMultiplier(adjustedShare);')) "Adjusted damage-part shares are not exposed to downstream systems."
Assert-Contract ($mainSource.Contains('weightedFeedback / feedbackWeight')) "Damage feedback does not aggregate native and custom per-part reactions."
Assert-Contract ($mainSource.Contains('bool contextualStatusPart = subtype == DamageSubType.Pure')) "Contextual status metadata is not isolated from unrelated mixed-damage parts."
Assert-Contract ($mainSource.Contains('"MeleeDamageNumberDurationMultiplier", 2.0f')) "Melee damage-number duration multiplier does not default to 2x."
Assert-Contract ($mainSource.Contains('duration *= GetMeleeDamageNumberDurationMultiplier();')) "Melee timing is not applied to the final damage-number duration."
Assert-Contract ($mainSource.Contains('ValueNameContains(GetOptionalPropertyValue(damage, "Type"), "PhysicalHitSource")')) "Melee timing is not limited to physical hit sources."
Assert-Contract ($mainSource.Contains('return projectile == null;')) "Melee timing does not exclude projectiles."
Assert-Contract ($mainSource.Contains('IsMelee = IsMeleeDamage(damage)')) "Melee timing is not captured while damage routing still has its projectile context."

$opposingMixedAdjustment = (0.25 * (0.40 / 0.50)) + (0.75 * (1.60 / 1.50))
Assert-Contract ([Math]::Abs($opposingMixedAdjustment - 1.0) -lt 0.000001) "Opposing mixed subtype reactions do not recombine to the expected neutral result."

$physicalPayloadAdjustment = (0.80 * 0.75) + (0.20 * 1.25)
Assert-Contract ([Math]::Abs($physicalPayloadAdjustment - 0.85) -lt 0.000001) "Independent physical and payload rules do not produce the expected weighted result."

Assert-Contract ($readme.Contains("Bone, flesh, stone, and spirit. Know your enemy. Strike with purpose.")) "Packaged README lacks the defining subtext."
Assert-Contract ($readme.Contains("Material weaknesses and resistances define the experience")) "Packaged README does not lead with material combat."
Assert-Contract ($nexusFull.Contains("Bone, flesh, stone, and spirit. Know your enemy. Strike with purpose.")) "Nexus description lacks the defining subtext."
Assert-Contract ($nexusFull.Contains("material system gives bone, flesh, stone, spirit, armor, and other identities distinct reactions")) "Nexus description does not lead with material combat."
Assert-Contract ($nexusFull.Contains("a lightweight, native-first difficulty overhaul built around one idea")) "Nexus introduction does not establish its lightweight, native-first identity."
Assert-Contract ($nexusFull.Contains("Major Features") -and $nexusFull.Contains("Knowledge decides the matchup.") -and $nexusFull.Contains("Every combat tool has a purpose.") -and $nexusFull.Contains("The rules stay readable.") -and $nexusFull.Contains("Combat pressure extends beyond damage.") -and $nexusFull.Contains("Preparation outlasts panic.") -and $nexusFull.Contains("Difficulty remains modular.")) "Nexus description lacks the thesis-style major-features overview."
Assert-Contract ($nexusFull.Contains("eight states ranging from immunity and resistance through neutral damage to weakness") -and -not $nexusFull.Contains("frame 0 for true immunity")) "Nexus combat-feedback section is missing its condensed hit-marker explanation."
Assert-Contract ($nexusFull.Contains("Broad in Scope, Lightweight in Implementation")) "Nexus lightweight-implementation section heading is missing."
Assert-Contract ($nexusFull.Contains("travel at x1.10 / x1.30 / x1.50 speed across Tempered, Hardened, and Crucible")) "Nexus projectile section does not present its preset sequence clearly."
Assert-Contract ($nexusFull.Contains("Expanded Combat Systems")) "Nexus description lacks the expanded combat systems section."
Assert-Contract ($nexusFull.Contains("Preparation and Recovery") -and $nexusFull.Contains("Together, these systems reward preparation without replacing Tainted Grail's native consumables")) "Nexus description lacks the dedicated preparation-and-recovery section or its purpose."
Assert-Contract ($nexusFull.Contains("Player combat pressure") -and $nexusFull.Contains("Progression follows the same curve")) "Nexus description does not separate immediate combat pressure from preset-scaled progression."
Assert-Contract ($nexusFull.Contains("Light/agile enemy movement") -and $nexusFull.Contains("Up to x1.05") -and $nexusFull.Contains("Up to x1.10")) "Nexus description lacks enemy movement preset tuning."
Assert-Contract ($nexusFull.Contains("Heavy enemies, bosses, constructs, plants, bears, scripted creatures, and non-pathing actors retain their native speed")) "Nexus description lacks the condensed enemy movement safety tiers."
Assert-Contract ($nexusFull.Contains("Passive shield protection") -and $nexusFull.Contains("reaches up to 8% / 10% / 12% on Tempered, Hardened, and Crucible")) "Nexus description lacks the practical passive-shield protection range."
Assert-Contract ($nexusFull.Contains("| Blunt            | x1.00   | x1.00 | x1.08  | x1.15 |")) "Nexus description does not show neutral Blunt damage against exposed humanoid flesh."
Assert-Contract ($nexusFull.Contains("[b]Arrows and spells[/b]") -and $nexusFull.Contains("x1.10 to x1.15 physical arrow damage") -and $nexusFull.Contains("x0.20 for confirmed skeletons") -and $nexusFull.Contains("x1.02 / x1.07 / x1.12 against Light, Medium, and Heavy armor")) "Nexus description lacks the condensed arrow and spell matchup ranges."
Assert-Contract (-not $nexusFull.Contains("| Other Hardened target   | Physical arrow damage |") -and -not $nexusFull.Contains("| Armor tier | Direct spell base on Hardened |")) "Nexus Know Your Enemy section still contains the superseded arrow or spell detail table."
Assert-Contract ($nexusFull.Contains("Combat Philosophy")) "Nexus description lacks the combat-philosophy section."
Assert-Contract ($nexusFull.Contains("Arrows pierce exposed flesh. Magic overwhelms plate armor. Armor turns aside arrows.")) "Nexus description does not explain the three-way counter cycle."
Assert-Contract ($nexusFull.Contains("27633-1786131422-324075370.png")) "Nexus description lacks the combat-philosophy image."
Assert-Contract ($nexusFull.Contains("mods/60888")) "Nexus description does not link Requiem's current Special Edition page."
Assert-Contract ($nexusFull.Contains("not an attempt to reproduce the scope of a total conversion")) "Nexus description does not bound the Requiem comparison."
Assert-Contract ($nexusFull.Contains("Custom Difficulty[/url] is incompatible")) "Custom Difficulty is not described as incompatible on Nexus."
Assert-Contract ($nexusFull.Contains("Tainted Instincts[/url] is incompatible")) "Tainted Instincts is not described as incompatible on Nexus."
Assert-Contract (-not $nexusFull.Contains("flagged as incompatible")) "Nexus compatibility wording still says flagged as incompatible."
Assert-Contract ($nexusFull.Contains("conditionally compatible")) "Tainted Combat conditional compatibility note is missing."
Assert-Contract ($nexusFull.Contains("Better Movement")) "Better Movement compatibility note is missing."
Assert-Contract ($nexusFull.Contains("mods/111]Better UI[/url] is softly supported")) "Better UI soft-compatibility link is missing."
Assert-Contract ($nexusFull.Contains("Grail Floating Text[/url] can show compatibility conflicts, critical load errors, and useful debug info in-game when diagnostics are enabled.")) "Standard Grail Floating Text compatibility guidance is missing."
Assert-Contract (-not $nexusFull.Contains("ShowGrailFloatingTextDiagnostics defaults on")) "Nexus compatibility guidance still contains the verbose Grail Floating Text diagnostics explanation."
Assert-Contract ($nexusFull.Contains("mods/105]Flat Arrows[/url] is conditionally compatible")) "Flat Arrows conditional compatibility note is missing."
Assert-Contract ($nexusFull.Contains("mods/284]Avalon AI Overhaul[/url] is conditionally compatible") -and $readme.Contains("Avalon AI Overhaul is conditionally compatible")) "Avalon AI Overhaul compatibility guidance is missing."
Assert-Contract ($nexusFull.Contains("PlayerArrowGravityMultiplier") -and $nexusFull.Contains("x0.75 gravity")) "Nexus description lacks the preset-independent player-arrow gravity control."
Assert-Contract ($nexusFull.Contains("HostileArcherAimScatter") -and $nexusFull.Contains("1.50 m") -and $nexusFull.Contains("1.00 m")) "Nexus description lacks hostile archer aim-scatter tuning."
Assert-Contract ($nexusFull.Contains("| Hostile enemy sight distance    | x1.20    | x1.40       | x1.60       |") -and $nexusFull.Contains("| Hero footstep hearing range     | x1.20    | x1.40       | x1.60       |") -and $nexusFull.Contains("| Native combat aggro persistence | x1.20    | x1.40       | x1.60       |")) "Nexus description lacks the strengthened enemy-awareness preset curve."
Assert-Contract ($nexusFull.Contains("Tainted Instincts") -and $nexusFull.Contains("enemy sight")) "Tainted Instincts incompatibility or enemy-awareness description is missing."
Assert-Contract ($nexusFull.Contains("HarderLife") -and $nexusFull.Contains("hearing") -and $nexusFull.Contains("potion effectiveness")) "HarderLife compatibility note does not distinguish Potion Poisoning from potion effectiveness."
Assert-Contract ($readme.Contains("Potion healing, auxiliary effects, item tooltips, and Better UI presentation remain native")) "Packaged README does not document native potion presentation and healing."
Assert-Contract ($readme.Contains("two same-class potions are safe") -and $readme.Contains("5 seconds on Tempered, 10 seconds on Hardened, or 15 seconds on Crucible")) "Packaged README does not document Potion Poisoning allowances and windows."
Assert-Contract ($readme.Contains("positive combat mana regeneration and accumulated positive parry-window bonuses use x0.75") -and $readme.Contains("Pickaxes count as Pierce on combat hits")) "Packaged README does not document the new sustain or pickaxe combat rules."
Assert-Contract ($nexusFull.Contains("Positive combat mana regen") -and $nexusFull.Contains("Positive parry-window bonus") -and $nexusFull.Contains("continue to mine normally")) "Nexus description does not document the new preset values and pickaxe behavior."
Assert-Contract ($readme.Contains("Health, Mana, Stamina, and Utility potions")) "Packaged README does not document independent potion-class buckets."
Assert-Contract ($readme.Contains("Health, Mana, or Stamina poisoning drains 30%") -and $readme.Contains("Utility drains 15%")) "Packaged README does not document class-specific Potion Poisoning drains."
Assert-Contract ($readme.Contains("Food does not stack")) "Packaged README does not document single-status food arbitration."
Assert-Contract ($nexusFull.Contains("[b]Food recovery[/b]") -and $nexusFull.Contains("greatest remaining health recovery") -and $nexusFull.Contains("faded queued-healing display")) "Nexus description lacks the dedicated food-recovery section or its core UI behavior."
Assert-Contract ($nexusFull.Contains("[b]Potion Poisoning[/b]") -and $nexusFull.Contains("Buffs, cures, locks, regeneration potions, resets, and miscellaneous consumables share this class") -and $nexusFull.Contains("drain 30%") -and $nexusFull.Contains("drains 15%")) "Nexus description lacks the dedicated Potion Poisoning section, Utility class, or class-specific drains."
Assert-Contract ($nexusFull.Contains("Safe same-class potions") -and $nexusFull.Contains("Third-potion poisoning window") -and $nexusFull.Contains("| 5 sec") -and $nexusFull.Contains("| 15 sec")) "Nexus preset table does not document Potion Poisoning allowances and windows."
Assert-Contract ($nexusShort.Length -le 350) "Nexus short description exceeds 350 characters."
Assert-Contract ($nexusFile.Length -lt $nexusShort.Length) "Nexus file description is not shorter than the short description."
Assert-Contract ($nexusFile -ne $nexusShort) "Nexus file description duplicates the short description."

Write-Output "Steel and Bone 3.9.6 difficulty contracts passed."
