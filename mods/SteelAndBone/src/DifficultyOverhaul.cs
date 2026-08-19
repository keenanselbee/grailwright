using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using Awaken.TG.Main.AI;
using Awaken.TG.Main.AI.Combat.Attachments;
using Awaken.TG.Main.AI.Combat.Utils;
using Awaken.TG.Main.AI.Fights.Projectiles;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.Animations.FSM.Npc.Machines;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Fights.Utils;
using Awaken.TG.Main.General.StatTypes;
using Awaken.TG.Main.Grounds;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Attachments;
using Awaken.TG.Main.Heroes.Items.Tooltips.Descriptors;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Stats.Controls;
using Awaken.TG.Main.Heroes.Statuses;
using Awaken.TG.Main.Heroes.Statuses.BuildUp;
using Awaken.TG.Main.Heroes.Statuses.Duration;
using Awaken.TG.Main.Rendering;
using Awaken.TG.Main.Heroes.Stats.Tweaks;
using Awaken.TG.Main.Settings.Gameplay;
using Awaken.TG.Main.Skills;
using Awaken.TG.Main.Skills.Passives;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Quests.Objectives;
using Awaken.TG.Main.Templates;
using Awaken.TG.Main.UI.HUD.Notifications;
using Awaken.TG.Main.Utility.RichEnums;
using Awaken.TG.Main.VisualGraphUtils;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SteelAndBone
{
    public sealed partial class SteelAndBonePlugin
    {
        private const string AvalonAiOverhaulPluginGuid = "AvalonAIOverhaul";
        private const string CustomDifficultyPluginGuid = "jonanoj.CustomDifficulty";
        private const string FlatArrowsPluginGuid = "RedJohn260.FlatArrows";
        private const string HarderLifePluginGuid = "fuwuvi.HarderLife";
        private const string TaintedCombatPluginGuid = "kane.tgfoa.tainted-combat";
        private const string TaintedInstinctsPluginGuid = "kane.tgfoa.tainted-instincts";
        private const string VersatileWeaponsApiTypeName =
            "VersatileWeapons.VersatileWeaponsApi";
        private const float NeutralTolerance = 0.0001f;
        private const float NativeBaseParryWindowSeconds = 0.05f;
        private const float DifficultyRefreshIntervalSeconds = 1.0f;
        private const float EnemyHearingRangeDiagnosticIntervalSeconds = 2.0f;
        private const string StandardFoodRecoveryGraphGuid = "1c2da8428b5a74142b93ed84593676a9";
        private const string FoodRecoveryStatusGuid = "432685012b6577f48a92c6ae8eb377cb";
        private const string PotionPoisoningStatusGuid = "60a2ed0287e14c944b53b6ab5870becd";
        private const string FlatPotionRestorationGraphGuid = "acd627b35fa17234aa6b00ea69faf646";
        private const string PercentPotionRestorationGraphGuid = "d5ab45e7eb066a84ea55f9ab4f78b92f";
        private const string TimedPotionRestorationGraphGuid = "4d431d204820819429d8f7bac4177644";
        private const float NativePotionPoisoningBuildup = 60.0f;
        private const float NativePotionPoisoningThreshold = 100.0f;
        private const float NativePotionPoisoningDecayPerSecond = 10.0f;
        private const float ResourcePotionPoisoningDrainFraction = 0.30f;
        private const float UtilityPotionPoisoningDrainFraction = 0.15f;
        private const float FoodStaminaTickSeconds = 1.0f;
        private const float FoodStaminaPostOverexertionDelaySeconds = 0.10f;
        private const float FoodOverexertionDurationMultiplier = 0.50f;
        private const float FoodCombatNotificationCooldownSeconds = 0.75f;
        private const float MaterialImpactResistanceInheritance = 0.60f;
        private const float StrongResistanceFlinchThreshold = 0.35f;
        private const float ProgressiveTenacityStartLevel = 20.0f;
        private const float ProgressiveTenacityFullLevel = 35.0f;
        private const string BetterUiPluginGuid = "Better_UI";
        private const string LegacyFoodStaminaStatusSourceId = "ks.tgfoa.steel-and-bone:food-stamina";
        private const string FoodStaminaRateVariable = "SteelAndBoneFoodStaminaRate";
        private const string FoodRecoveryDurationVariable = "SteelAndBoneFoodRecoveryDuration";

        private static readonly FieldInfo SkillVariableOverridesField =
            AccessTools.Field(typeof(Skill), "_variableOverrides");
        private static readonly FieldInfo StaminaVignetteImageField =
            AccessTools.Field(typeof(VHeroStaminaUsedUpEffect), "vignette");
        private static readonly FieldInfo StaminaVignetteFadeStrengthField =
            AccessTools.Field(typeof(VHeroStaminaUsedUpEffect), "vignetteFadeStrength");
        private static readonly FieldInfo StaminaVignetteTweenField =
            AccessTools.Field(typeof(VHeroStaminaUsedUpEffect), "_vignetteFade");
        private static MethodInfo _betterUiLastEffectCountGetter;
        private static MethodInfo _betterUiLastEffectCountSetter;
        [ThreadStatic]
        private static bool _suppressRoutineResistedFlinch;

        private ConfigEntry<bool> _difficultyModifiersEnabled;
        private ConfigEntry<bool> _modifyPlayerDamageDealt;
        private ConfigEntry<float> _weakSpotDamageBonus;
        private ConfigEntry<bool> _modifyPlayerDamageTaken;
        private ConfigEntry<bool> _passiveShieldProtectionEnabled;
        private ConfigEntry<bool> _modifyStaminaUsage;
        private ConfigEntry<bool> _modifyManaUsage;
        private ConfigEntry<bool> _modifyCombatManaRegeneration;
        private ConfigEntry<float> _combatManaRegenerationMultiplier;
        private ConfigEntry<bool> _modifyParryWindowBonus;
        private ConfigEntry<float> _positiveParryWindowBonusMultiplier;
        private ConfigEntry<bool> _modifyPlayerPoiseDamageDealt;
        private ConfigEntry<bool> _progressiveTenacityEnabled;
        private ConfigEntry<bool> _modifyPlayerArrowVelocity;
        private ConfigEntry<bool> _modifyPlayerArrowDrop;
        private ConfigEntry<float> _playerArrowGravityMultiplier;
        private ConfigEntry<bool> _modifyArmorWeightPenalties;
        private ConfigEntry<bool> _modifyLightArmorMobility;
        private ConfigEntry<bool> _modifyArmorPhysicalProtection;
        private ConfigEntry<bool> _modifyFoodRecovery;
        private ConfigEntry<bool> _preventFoodUseInCombat;
        private ConfigEntry<bool> _modifyPotionOverdrinking;
        private ConfigEntry<StaminaDepletedVignetteMode> _staminaDepletedVignetteMode;
        private ConfigEntry<float> _staminaDepletedVignetteFadeSeconds;
        private ConfigEntry<bool> _modifyEnemyAttackSlots;
        private ConfigEntry<int> _enemyAttackSlotCap;
        private ConfigEntry<bool> _modifyEnemyAttackRecovery;
        private ConfigEntry<bool> _modifyEnemyMovementSpeed;
        private ConfigEntry<bool> _modifyHostileArrowVelocity;
        private ConfigEntry<float> _hostileArcherAimScatter;
        private ConfigEntry<bool> _modifyEnemySightRange;
        private ConfigEntry<bool> _modifyEnemyHearingRange;
        private ConfigEntry<bool> _modifyEnemyAggroPersistence;
        private ConfigEntry<bool> _modifyKillExperience;
        private ConfigEntry<bool> _modifyQuestExperience;
        private ConfigEntry<bool> _modifyProficiencyExperience;

        private readonly HashSet<string> _loggedOverlapSignatures = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _notifiedOverlapSignatures = new HashSet<string>(StringComparer.Ordinal);
        private Hero _difficultyTweakHero;
        private float _nextDifficultyRefreshAt;
        private float _nextEnemyHearingRangeDiagnosticAt;
        private bool _resourceStatsPatchAvailable;
        private bool _reportedEnemyRuntimeRefreshFailure;
        private bool _versatileWeaponsBridgeResolved;
        private bool _versatileWeaponsBridgeFailureLogged;
        private bool _reportedFoodStatusCaptureFailure;
        private bool _reportedPotionPoisoningTemplateFailure;
        private bool _loggedFoodStaminaTickDiagnostic;
        private float _foodStaminaTickElapsed;
        private bool _foodStaminaWasSuspendedByOverexertion;
        private float _nextFoodCombatNotificationAt;
        private VHeroStaminaUsedUpEffect _staminaVignetteView;
        private Image _staminaVignetteImage;
        private bool _staminaVignetteExhaustionActive;
        private bool _staminaVignetteFadeActive;
        private float _staminaVignetteFadeStartAlpha;
        private float _staminaVignetteFadeTargetAlpha;
        private float _staminaVignetteFadeElapsed;
        private float _staminaVignetteStrength;
        private Hero _potionPoisoningBucketHero;
        private float _potionPoisoningBucketUpdatedAt = -1.0f;
        private float _healthPotionPoisoningBuildup;
        private float _manaPotionPoisoningBuildup;
        private float _staminaPotionPoisoningBuildup;
        private float _utilityPotionPoisoningBuildup;
        private bool _applyingCompletedPotionPoisoningBuildup;
        private PotionPoisoningBucket _pendingPotionPoisoningPenaltyBuckets;
        private BuildupStatus _activePotionPoisoningDrainStatus;
        private Hero _activePotionPoisoningDrainHero;
        private PotionPoisoningBucket _activePotionPoisoningDrainBuckets;
        private float _potionPoisoningHealthDrainRemaining;
        private float _potionPoisoningManaDrainRemaining;
        private float _potionPoisoningStaminaDrainRemaining;
        private ConsumableRecoveryPatchState _activeConsumableRecoveryState;
        private Func<bool> _versatileWeaponsIsMainHandSuppressed;
        private Func<bool> _versatileWeaponsIsOffHandSuppressed;

        private enum DifficultyStatTarget
        {
            StaminaUsage,
            ManaUsage,
            ArmorPenalty,
            LightArmorMovement
        }

        [Flags]
        private enum PotionPoisoningBucket
        {
            None = 0,
            Health = 1,
            Mana = 2,
            Stamina = 4,
            Utility = 8
        }

        private enum StaminaDepletedVignetteMode
        {
            Smooth,
            Native,
            Off
        }

        private sealed class DifficultyStatTweak : StatTweak
        {
            internal DifficultyStatTarget Target { get; private set; }

            public override bool IsNotSaved
            {
                get { return true; }
            }

            internal DifficultyStatTweak(DifficultyStatTarget target, Stat stat, float multiplier)
                : base(stat, multiplier, TweakPriority.Multiply, OperationType.Multi, null)
            {
                Target = target;
                MarkedNotSaved = true;
            }
        }

        private sealed class EnemySightRangeTweak : StatTweak
        {
            public override bool IsNotSaved
            {
                get { return true; }
            }

            internal EnemySightRangeTweak(Stat stat, float multiplier)
                : base(stat, multiplier, TweakPriority.Multiply, OperationType.Multi, null)
            {
                MarkedNotSaved = true;
            }
        }

        private sealed class EnemyMovementSpeedTweak : StatTweak
        {
            public override bool IsNotSaved
            {
                get { return true; }
            }

            internal EnemyMovementSpeedTweak(Stat stat, float multiplier)
                : base(stat, multiplier, TweakPriority.Multiply, OperationType.Multi, null)
            {
                MarkedNotSaved = true;
            }
        }

        private sealed class PoisePatchState
        {
            internal Damage Damage;
            internal float OriginalPoiseDamage;
            internal bool PoiseChanged;
            internal bool PreviousSuppressRoutineResistedFlinch;
        }

        private sealed class ForcePatchState
        {
            internal Damage Damage;
            internal float OriginalForceDamage;
        }

        private sealed class ParryStaminaPatchState
        {
            internal StatTweak Tweak;
        }

        private sealed class ProgressiveTenacityParryTweak : StatTweak
        {
            public override bool IsNotSaved
            {
                get { return true; }
            }

            internal ProgressiveTenacityParryTweak(Stat stat, float multiplier, Hero hero)
                : base(stat, multiplier, TweakPriority.Multiply, OperationType.Multi, hero)
            {
                MarkedNotSaved = true;
            }
        }

        private sealed class ConsumableRecoveryPatchState
        {
            internal FoodSkillOverrideState Food;
            internal Hero Hero;
            internal string PotionTemplateGuid;
            internal PotionPoisoningBucket PotionPoisoningBuckets;
            internal bool IsPotionConsumption;
            internal ConsumableRecoveryPatchState PreviousActiveState;
        }

        private sealed class SkillVariableSnapshot
        {
            internal Skill Skill;
            internal List<SkillVariable> VariableOverrides;
        }

        private sealed class FoodSkillOverrideState
        {
            internal Item Item;
            internal Hero Hero;
            internal readonly List<SkillVariableSnapshot> Snapshots =
                new List<SkillVariableSnapshot>();
            internal readonly HashSet<Status> ExistingFoodStatuses =
                new HashSet<Status>();
            internal float RecoveryDuration;
            internal bool Restored;
        }

        private void BindDifficultyConfig()
        {
            _difficultyModifiersEnabled = Config.Bind(
                "General",
                "DifficultyModifiersEnabled",
                true,
                ConfigUi("Master switch for Steel and Bone's global damage, resource, armor, projectile, enemy-awareness, enemy-pressure, poise, and experience modifiers. Material matchup rules remain active when this is disabled.", "General", "Difficulty Modifiers", 0, 20));

            _staminaDepletedVignetteMode = Config.Bind(
                "Feedback",
                "StaminaDepletedVignetteMode",
                StaminaDepletedVignetteMode.Smooth,
                ConfigUi(
                    "Presentation for the native Stamina Depleted screen vignette. Smooth replaces its repeating flash and abrupt removal with one fade in and out, Native keeps the game effect, and Off hides both the vignette image and stamina-depleted post-process while preserving audio and gameplay penalties.",
                    "Stamina Depleted",
                    "Vignette Mode",
                    45,
                    0));
            _staminaDepletedVignetteFadeSeconds = Config.Bind(
                "Feedback",
                "StaminaDepletedVignetteFadeSeconds",
                0.30f,
                ConfigUi(
                    "Seconds used for each fade in and fade out when Vignette Mode is Smooth. Unscaled time keeps the transition consistent across gameplay timescale changes.",
                    "Stamina Depleted",
                    "Vignette Fade (Seconds)",
                    45,
                    10,
                    new AcceptableValueRange<float>(0.05f, 2.0f)));

            _modifyPlayerDamageDealt = Config.Bind(
                "Difficulty - Player",
                "ModifyPlayerDamageDealt",
                true,
                ConfigUi("Reduce health damage dealt by the player by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Outgoing Player Damage", 60, 0));
            _weakSpotDamageBonus = Config.Bind(
                "Difficulty - Player",
                "WeakSpotDamageBonus",
                GetPresetWeakSpotDamageBonus(_preset.Value),
                ConfigUi(
                    "Add 10%, 20%, or 30% base damage to confirmed weak-spot hits according to the preset when Difficulty Modifiers is enabled. This is added beside the game's native critical, weak-spot, sneak, and backstab bonuses before Steel and Bone's outgoing and matchup multipliers. Changing Preset resets this value; customize it afterward if desired.",
                    "Difficulty - Player",
                    "Weak Spot Damage Bonus",
                    60,
                    5,
                    new AcceptableValueRange<float>(0.0f, 0.50f)));
            _modifyPlayerDamageTaken = Config.Bind(
                "Difficulty - Player",
                "ModifyPlayerDamageTaken",
                true,
                ConfigUi("Increase health damage taken from all routed damage sources by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Incoming Player Damage", 60, 10));
            _passiveShieldProtectionEnabled = Config.Bind(
                "Difficulty - Player",
                "PassiveShieldProtectionEnabled",
                true,
                ConfigUi("When Difficulty Modifiers is enabled, grant an equipped and readied shield modest passive protection against direct physical attacks from within its forward BlockAngle. Active blocks, rear attacks, magic, status effects, and damage over time are unaffected.", "Difficulty - Player", "Passive Shield Protection", 60, 20));
            _modifyStaminaUsage = Config.Bind(
                "Difficulty - Player",
                "ModifyStaminaUsage",
                true,
                ConfigUi("Increase player stamina usage by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Stamina Usage", 60, 30));
            _modifyManaUsage = Config.Bind(
                "Difficulty - Player",
                "ModifyManaUsage",
                true,
                ConfigUi("Increase player mana usage by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Mana Usage", 60, 40));
            _modifyCombatManaRegeneration = Config.Bind(
                "Difficulty - Player",
                "ModifyCombatManaRegeneration",
                true,
                ConfigUi("Apply Combat Mana Regeneration Multiplier only while the hero is in combat and regenerating positive mana. Mana Shield proportionally relieves this added penalty while its native combat-regeneration reduction and post-hit regeneration lock remain in control.", "Difficulty - Player", "Combat Mana Regeneration", 60, 45));
            _combatManaRegenerationMultiplier = Config.Bind(
                "Difficulty - Player",
                "CombatManaRegenerationMultiplier",
                GetPresetCombatSustainabilityMultiplier(_preset.Value),
                ConfigUi(
                    "Multiplier for positive mana regeneration during combat when Combat Mana Regeneration and Difficulty Modifiers are enabled. Changing Preset sets this to 1.00 for Tempered, 0.75 for Hardened, or 0.50 for Crucible; customize it afterward if desired. Out-of-combat regeneration is unchanged.",
                    "Difficulty - Player",
                    "Combat Mana Regeneration Multiplier",
                    60,
                    46,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));
            _modifyParryWindowBonus = Config.Bind(
                "Difficulty - Player",
                "ModifyParryWindowBonus",
                true,
                ConfigUi("Apply Positive Parry Window Bonus Multiplier to the part of each parry window above the native 0.05-second base when Difficulty Modifiers are enabled. Negative total bonuses remain unchanged.", "Difficulty - Player", "Positive Parry Window Bonus", 60, 47));
            _positiveParryWindowBonusMultiplier = Config.Bind(
                "Difficulty - Player",
                "PositiveParryWindowBonusMultiplier",
                GetPresetCombatSustainabilityMultiplier(_preset.Value),
                ConfigUi(
                    "Multiplier for the accumulated positive parry-window bonus from skills, equipment, and other effects. Changing Preset sets this to 1.00 for Tempered, 0.75 for Hardened, or 0.50 for Crucible; customize it afterward if desired. The native 0.05-second base window is not reduced.",
                    "Difficulty - Player",
                    "Positive Parry Window Bonus Multiplier",
                    60,
                    48,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));
            _modifyPlayerPoiseDamageDealt = Config.Bind(
                "Difficulty - Player",
                "ModifyPlayerPoiseDamageDealt",
                true,
                ConfigUi("Reduce poise damage dealt by the player by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled, making enemies slightly harder to stagger-lock.", "Difficulty - Player", "Player Poise Damage Dealt", 60, 50));
            _modifyPlayerArrowVelocity = Config.Bind(
                "Difficulty - Player",
                "ModifyPlayerArrowVelocity",
                true,
                ConfigUi("Multiply player-fired arrow speed by 1.10, 1.30, or 1.50 according to the preset when Difficulty Modifiers is enabled. This setting does not alter damage; Arrow Material Rules controls the separate material matchup.", "Difficulty - Player", "Player Arrow Speed", 60, 60));
            _modifyPlayerArrowDrop = Config.Bind(
                "Difficulty - Player",
                "ModifyPlayerArrowDrop",
                true,
                ConfigUi("When Difficulty Modifiers is enabled, apply Player Arrow Gravity Multiplier to player-fired arrows. This reduces arrow drop without tilting the launch direction and is independent from the preset.", "Difficulty - Player", "Reduce Player Arrow Drop", 60, 70));
            _playerArrowGravityMultiplier = Config.Bind(
                "Difficulty - Player",
                "PlayerArrowGravityMultiplier",
                0.75f,
                ConfigUi(
                    "Gravity multiplier for player-fired arrows when Reduce Player Arrow Drop and Difficulty Modifiers are enabled. 1 is vanilla gravity and 0.75 applies 25% less gravity. Arrow velocity remains controlled separately by the preset.",
                    "Difficulty - Player",
                    "Player Arrow Gravity Multiplier",
                    60,
                    80,
                    new AcceptableValueRange<float>(0.25f, 1.0f)));
            _modifyArmorWeightPenalties = Config.Bind(
                "Difficulty - Player",
                "ModifyArmorWeightPenalties",
                true,
                ConfigUi("Multiply the game's native armor-weight penalties by 1.00, 1.05, or 1.10 according to the preset when Difficulty Modifiers is enabled. Existing armor proficiency still softens eligible penalties.", "Difficulty - Player", "Armor Weight Penalties", 60, 90));
            _modifyLightArmorMobility = Config.Bind(
                "Difficulty - Player",
                "ModifyLightArmorMobility",
                true,
                ConfigUi("Increase movement speed while in the game's Light armor tier by 0%, 2.5%, or 5% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Light Armor Mobility", 60, 100));
            _modifyArmorPhysicalProtection = Config.Bind(
                "Difficulty - Player",
                "ModifyArmorPhysicalProtection",
                true,
                ConfigUi("Multiply physical armor in Medium by 1.00/1.05/1.10 and in Heavy or Overload by 1.00/1.10/1.20 according to the preset when Difficulty Modifiers is enabled. Magical armor checks are unchanged.", "Difficulty - Player", "Physical Armor Protection", 60, 110));
            _modifyPotionOverdrinking = Config.Bind(
                "Difficulty - Player",
                "ModifyPotionOverdrinking",
                true,
                ConfigUi("Track Potion Poisoning separately for Health, Mana, Stamina, and Utility potions at 60, 65, or 70 buildup according to the preset when Difficulty Modifiers is enabled. A Health, Mana, or Stamina trigger drains 30% of that resource over the native status; Utility drains 15% of all three. Mixing classes does not combine their buildup.", "Difficulty - Player", "Potion Overdrinking", 60, 120));
            _modifyFoodRecovery = Config.Bind(
                "Difficulty - Player",
                "ModifyFoodRecovery",
                true,
                ConfigUi("Reshape standard food healing over time when Difficulty Modifiers is enabled. Every preset uses 4x duration and restores 1 stamina in discrete one-second ticks outside Stamina Depleted. Active food halves native Stamina Depleted duration and pauses its added stamina ticks during it; the first point follows 0.1 seconds after the lock ends, then normal one-second ticks resume. Health rate is 50% on Tempered, 37.5% on Hardened, or 25% on Crucible. Only the food status with the greatest remaining queued healing stays active.", "Difficulty - Player", "Food Recovery", 60, 125));
            _preventFoodUseInCombat = Config.Bind(
                "Difficulty - Player",
                "PreventFoodUseInCombat",
                GetPresetPreventFoodUseInCombat(_preset.Value),
                ConfigUi("Prevent food and dishes from being consumed while the hero is in combat when Difficulty Modifiers is enabled. Changing Preset sets this off for Tempered or on for Hardened and Crucible; customize it afterward if desired. Noncombat use remains unrestricted.", "Difficulty - Player", "Prevent Food Use In Combat", 60, 130));

            _modifyEnemyAttackSlots = Config.Bind(
                "Difficulty - Enemies",
                "ModifyEnemyAttackSlots",
                true,
                ConfigUi("Add 0, 1, or 2 simultaneous enemy attack slots to the current game difficulty according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Enemies", "Enemy Attack Slots", 70, 0));
            _progressiveTenacityEnabled = Config.Bind(
                "Difficulty - Enemies",
                "ProgressiveTenacityEnabled",
                true,
                ConfigUi("From hero level 20 through 35, progressively reduce direct health, poise, force, stamina, and parry stamina damage against hostile enemies. Material weaknesses halve Tenacity. This independent progression layer does not change the selected preset.", "Difficulty - Enemies", "Progressive Tenacity", 70, 5));
            _enemyAttackSlotCap = Config.Bind(
                "Difficulty - Enemies",
                "EnemyAttackSlotCap",
                6,
                ConfigUi(
                    "Safety cap for slots added by Steel and Bone when Enemy Attack Slots and Difficulty Modifiers are enabled. This never lowers a higher value supplied by the game or another mod.",
                    "Difficulty - Enemies",
                    "Maximum Enemy Attack Slots",
                    70,
                    10,
                    new AcceptableValueRange<int>(1, 12)));
            _modifyEnemyAttackRecovery = Config.Bind(
                "Difficulty - Enemies",
                "ModifyEnemyAttackRecovery",
                true,
                ConfigUi("Shorten the delay before enemies release attack slots by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Enemies", "Enemy Attack Recovery Time", 70, 20));
            _modifyEnemyMovementSpeed = Config.Bind(
                "Difficulty - Enemies",
                "ModifyEnemyMovementSpeed",
                true,
                ConfigUi("Increase combat movement speed by up to 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled. Ordinary agile enemies receive the full bonus; Medium-armored, Elite, Beholder, and Slugholder enemies receive at most half; Heavy-armored, massive, boss, and scripted enemies retain their vanilla speed.", "Difficulty - Enemies", "Enemy Movement Speed", 70, 30));
            _modifyHostileArrowVelocity = Config.Bind(
                "Difficulty - Enemies",
                "ModifyHostileArrowVelocity",
                true,
                ConfigUi("Multiply hostile NPC arrow speed by 1.10, 1.30, or 1.50 according to the preset when Difficulty Modifiers is enabled while preserving the game's ballistic aim calculation. Hostile arrow damage is unchanged.", "Difficulty - Enemies", "Hostile Arrow Speed", 70, 40));
            _hostileArcherAimScatter = Config.Bind(
                "Difficulty - Enemies",
                "HostileArcherAimScatter",
                GetPresetHostileArcherAimScatter(_preset.Value),
                ConfigUi(
                    "Minimum random aim-point scatter in meters for hostile NPC arrows when Difficulty Modifiers is enabled. Changing Preset sets this to 1.50 for Tempered, 1.25 for Hardened, or 1.00 for Crucible; customize it afterward or set it to 0 for native accuracy.",
                    "Difficulty - Enemies",
                    "Hostile Archer Aim Scatter (Meters)",
                    70,
                    45,
                    new AcceptableValueRange<float>(0.0f, 2.0f)));
            _modifyEnemySightRange = Config.Bind(
                "Difficulty - Enemies",
                "ModifyEnemySightRange",
                true,
                ConfigUi("Multiply the native sight distance of active hostile NPCs by 1.20, 1.40, or 1.60 according to the preset when Difficulty Modifiers is enabled. Line of sight, visibility, alert behavior, and authored perception distances remain native.", "Difficulty - Enemies", "Hostile Enemy Sight Distance", 70, 50));
            _modifyEnemyHearingRange = Config.Bind(
                "Difficulty - Enemies",
                "ModifyEnemyHearingRange",
                true,
                ConfigUi("Multiply the native range of hero footstep noise by 1.20, 1.40, or 1.60 according to the preset when Difficulty Modifiers is enabled. Native hearing strength, wall checks, armor noise, and NPC hearing differences remain in control.", "Difficulty - Enemies", "Hostile Enemy Hearing Range", 70, 60));
            _modifyEnemyAggroPersistence = Config.Bind(
                "Difficulty - Enemies",
                "ModifyEnemyAggroPersistence",
                true,
                ConfigUi("Multiply native combat aggro persistence by 1.20, 1.40, or 1.60 according to the preset when Difficulty Modifiers is enabled. Chase boundaries, forced combat exit, target-loss rules, and alert behavior remain native.", "Difficulty - Enemies", "Enemy Aggro Persistence", 70, 70));

            _modifyKillExperience = Config.Bind(
                "Difficulty - Progression",
                "ModifyKillExperience",
                true,
                ConfigUi("Reduce experience gained from enemy kills by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Progression", "Kill XP", 80, 0));
            _modifyQuestExperience = Config.Bind(
                "Difficulty - Progression",
                "ModifyQuestExperience",
                true,
                ConfigUi("Reduce experience gained from quest and objective rewards by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Progression", "Quest and Objective XP", 80, 10));
            _modifyProficiencyExperience = Config.Bind(
                "Difficulty - Progression",
                "ModifyProficiencyExperience",
                true,
                ConfigUi("Reduce proficiency experience by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Progression", "Proficiency XP", 80, 20));
        }

        private void InitializeDifficultyOverhaul()
        {
            Config.SettingChanged += OnDifficultySettingChanged;
            ReapplyDifficultyStatTweaks();
            RefreshEnemyRuntimeTweaks();
            EvaluateCompatibilityOverlaps();
        }

        private void ShutdownDifficultyOverhaul()
        {
            Config.SettingChanged -= OnDifficultySettingChanged;
            RemoveDifficultyStatTweaks(Hero.Current);
            RemoveAllEnemySightRangeTweaks();
            RemoveAllEnemyMovementSpeedTweaks();
            RestoreNativeStaminaVignettePresentation();
            _difficultyTweakHero = null;
        }

        private void Update()
        {
            UpdateStaminaVignetteFade();
            if (Time.unscaledTime < _nextDifficultyRefreshAt)
            {
                return;
            }

            _nextDifficultyRefreshAt = Time.unscaledTime + DifficultyRefreshIntervalSeconds;
            Hero hero = Hero.Current;
            if (!ReferenceEquals(hero, _difficultyTweakHero))
            {
                RemoveDifficultyStatTweaks(_difficultyTweakHero);
            }
            ReapplyDifficultyStatTweaks();
            RefreshEnemyRuntimeTweaks();

            EvaluateCompatibilityOverlaps();
        }

        private void OnDifficultySettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (args != null && ReferenceEquals(args.ChangedSetting, _preset))
            {
                ApplyPresetEffectivenessFeedbackSensitivity();
                ApplyPresetWeakSpotDamageBonus();
                ApplyPresetHostileArcherAimScatter();
                ApplyPresetPreventFoodUseInCombat();
                ApplyPresetCombatSustainabilityMultipliers();
            }
            if (args != null
                && (ReferenceEquals(args.ChangedSetting, _enabled)
                    || ReferenceEquals(args.ChangedSetting, _staminaDepletedVignetteMode)
                    || ReferenceEquals(args.ChangedSetting, _staminaDepletedVignetteFadeSeconds)))
            {
                ApplyCurrentStaminaVignetteMode();
            }

            ReapplyDifficultyStatTweaks();
            RefreshEnemyRuntimeTweaks();
            EvaluateCompatibilityOverlaps();
        }

        private void PatchDifficultyOverhaul()
        {
            PatchOptionalPostfix(
                AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.ManaRegen)),
                typeof(CombatManaRegenerationPatch),
                nameof(CombatManaRegenerationPatch.Postfix),
                "Hero.ManaRegen",
                "combat mana-regeneration modifier");
            PatchOptionalPostfix(
                AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.PredictedManaRegen)),
                typeof(PredictedCombatManaRegenerationPatch),
                nameof(PredictedCombatManaRegenerationPatch.Postfix),
                "Hero.PredictedManaRegen",
                "predicted combat mana-regeneration modifier");
            PatchOptionalPrefix(
                AccessTools.Method(
                    typeof(HeroParry),
                    nameof(HeroParry.Parry),
                    new[] { typeof(Hero), typeof(IDuration) }),
                typeof(PositiveParryWindowBonusPatch),
                nameof(PositiveParryWindowBonusPatch.Prefix),
                "HeroParry.Parry",
                "positive parry-window bonus modifier");
            _resourceStatsPatchAvailable = PatchOptionalPostfix(
                AccessTools.Method(typeof(CharacterStats.CharacterStatsWrapper), "Initialize"),
                typeof(CharacterStatsInitializePatch),
                nameof(CharacterStatsInitializePatch.Postfix),
                "CharacterStats.CharacterStatsWrapper.Initialize",
                "stamina and mana modifiers");
            PatchOptionalPrefix(
                AccessTools.Method(
                    typeof(AINoises),
                    "MakeHeroFootstepNoise",
                    new[] { typeof(float), typeof(float), typeof(float), typeof(Vector3) }),
                typeof(EnemyHearingRangePatch),
                nameof(EnemyHearingRangePatch.Prefix),
                "AINoises.MakeHeroFootstepNoise",
                "enemy hearing-range modifier");
            PatchOptionalPostfix(
                AccessTools.Method(
                    typeof(NpcAIDistancesUtils),
                    "CombatAggroDecreaseModifierByDistanceToLastIdlePoint",
                    new[] { typeof(NpcAI) }),
                typeof(EnemyAggroPersistencePatch),
                nameof(EnemyAggroPersistencePatch.Postfix),
                "NpcAIDistancesUtils.CombatAggroDecreaseModifierByDistanceToLastIdlePoint",
                "enemy aggro-persistence modifier");
            PatchOptionalPostfix(
                AccessTools.PropertyGetter(typeof(Difficulty), "MaxEnemiesAttacking"),
                typeof(MaxEnemiesAttackingPatch),
                nameof(MaxEnemiesAttackingPatch.Postfix),
                "Difficulty.MaxEnemiesAttacking",
                "enemy attack-slot modifier");
            PatchOptionalPostfix(
                AccessTools.PropertyGetter(typeof(Difficulty), "AttackActionUnBookProlong"),
                typeof(EnemyAttackRecoveryPatch),
                nameof(EnemyAttackRecoveryPatch.Postfix),
                "Difficulty.AttackActionUnBookProlong",
                "enemy attack-recovery modifier");
            PatchOptionalPostfix(
                AccessTools.Method(typeof(NpcTemplate), "GetExpReward"),
                typeof(KillExperiencePatch),
                nameof(KillExperiencePatch.Postfix),
                "NpcTemplate.GetExpReward",
                "kill-experience modifier");
            PatchOptionalPostfix(
                AccessTools.PropertyGetter(typeof(Quest), "ExperiencePoints"),
                typeof(QuestExperiencePatch),
                nameof(QuestExperiencePatch.Postfix),
                "Quest.ExperiencePoints",
                "quest-experience modifier");
            PatchOptionalPostfix(
                AccessTools.PropertyGetter(typeof(Objective), "ExperiencePoints"),
                typeof(ObjectiveExperiencePatch),
                nameof(ObjectiveExperiencePatch.Postfix),
                "Objective.ExperiencePoints",
                "objective-experience modifier");
            PatchOptionalPrefix(
                AccessTools.Method(typeof(ProficiencyStats), "TryAddXP"),
                typeof(ProficiencyExperiencePatch),
                nameof(ProficiencyExperiencePatch.Prefix),
                "ProficiencyStats.TryAddXP",
                "proficiency-experience modifier");
            PatchOptionalPrefix(
                AccessTools.Method(typeof(BowFSM), "FireProjectileInternal"),
                typeof(PlayerArrowVelocityPatch),
                nameof(PlayerArrowVelocityPatch.Prefix),
                "BowFSM.FireProjectileInternal",
                "player arrow-velocity modifier");
            PatchOptionalPostfix(
                AccessTools.Method(typeof(DamageDealingProjectile), "ProcessFixedUpdate"),
                typeof(PlayerArrowGravityPatch),
                nameof(PlayerArrowGravityPatch.Postfix),
                "DamageDealingProjectile.ProcessFixedUpdate",
                "player arrow-drop modifier");
            PatchOptionalPostfix(
                AccessTools.Method(typeof(Hero), "TotalArmor", new[] { typeof(DamageSubType) }),
                typeof(HeroPhysicalArmorPatch),
                nameof(HeroPhysicalArmorPatch.Postfix),
                "Hero.TotalArmor",
                "armor physical-protection modifier");
            PatchOptionalPrefix(
                AccessTools.Method(typeof(Item), nameof(Item.Use), Type.EmptyTypes),
                typeof(FoodCombatUsePatch),
                nameof(FoodCombatUsePatch.Prefix),
                "Item.Use",
                "food combat-use restriction");
            PatchConsumableRecovery();
            PatchStaminaDepletedVignette();
            PatchHostileArrowBallistics();
            PatchPoiseDamage();
            PatchProgressiveTenacity();
        }

        private void PatchConsumableRecovery()
        {
            MethodInfo original = AccessTools.Method(
                typeof(ItemSkillsInvoker),
                "PerformImmediate");
            MethodInfo prefix = AccessTools.Method(
                typeof(ConsumableRecoveryPatch),
                nameof(ConsumableRecoveryPatch.Prefix));
            MethodInfo postfix = AccessTools.Method(
                typeof(ConsumableRecoveryPatch),
                nameof(ConsumableRecoveryPatch.Postfix));
            MethodInfo finalizer = AccessTools.Method(
                typeof(ConsumableRecoveryPatch),
                nameof(ConsumableRecoveryPatch.Finalizer));
            if (original == null || prefix == null || postfix == null || finalizer == null)
            {
                Warn("Could not patch ItemSkillsInvoker.PerformImmediate; food recovery tuning is disabled.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        original,
                        prefix: new HarmonyMethod(prefix),
                        postfix: new HarmonyMethod(postfix),
                        finalizer: new HarmonyMethod(finalizer));
                    LogDiagnostic("Patched ItemSkillsInvoker.PerformImmediate for food recovery.");
                }
                catch (Exception ex)
                {
                    Warn("Could not patch ItemSkillsInvoker.PerformImmediate; food recovery tuning is disabled. " + ex.GetBaseException().Message);
                }
            }

            MethodInfo descriptionGetter = AccessTools.PropertyGetter(
                typeof(ExistingItemDescriptor),
                nameof(ExistingItemDescriptor.ItemDescription));
            MethodInfo descriptionPrefix = AccessTools.Method(
                typeof(FoodDescriptionPatch),
                nameof(FoodDescriptionPatch.Prefix));
            MethodInfo descriptionPostfix = AccessTools.Method(
                typeof(FoodDescriptionPatch),
                nameof(FoodDescriptionPatch.Postfix));
            MethodInfo descriptionFinalizer = AccessTools.Method(
                typeof(FoodDescriptionPatch),
                nameof(FoodDescriptionPatch.Finalizer));
            if (descriptionGetter == null
                || descriptionPrefix == null
                || descriptionPostfix == null
                || descriptionFinalizer == null)
            {
                Warn("Could not patch ExistingItemDescriptor.ItemDescription; food tooltip tuning is disabled.");
                return;
            }

            try
            {
                _harmony.Patch(
                    descriptionGetter,
                    prefix: new HarmonyMethod(descriptionPrefix),
                    postfix: new HarmonyMethod(descriptionPostfix),
                    finalizer: new HarmonyMethod(descriptionFinalizer));
                LogDiagnostic("Patched ExistingItemDescriptor.ItemDescription for live food recovery text.");
            }
            catch (Exception ex)
            {
                Warn("Could not patch ExistingItemDescriptor.ItemDescription; food tooltip tuning is disabled. " + ex.GetBaseException().Message);
            }

            MethodInfo statusDescriptionGetter = AccessTools.PropertyGetter(
                typeof(Status),
                nameof(Status.StatusDescription));
            PatchOptionalPostfix(
                statusDescriptionGetter,
                typeof(FoodStatusDescriptionPatch),
                nameof(FoodStatusDescriptionPatch.Postfix),
                "Status.StatusDescription",
                "combined food health-and-stamina status description");

            MethodInfo updateStats = AccessTools.Method(
                typeof(VHeroController),
                "UpdateStats",
                new[] { typeof(float) });
            PatchOptionalPostfix(
                updateStats,
                typeof(FoodStaminaRecoveryPatch),
                nameof(FoodStaminaRecoveryPatch.Postfix),
                "VHeroController.UpdateStats",
                "direct food stamina recovery");

            MethodInfo preventStaminaRegenWithStatus = AccessTools.Method(
                typeof(PreventStaminaRegenDuration),
                nameof(PreventStaminaRegenDuration.PreventWithStatus),
                new[]
                {
                    typeof(ICharacter),
                    typeof(StaminaRegenBlockType),
                    typeof(IDuration),
                    typeof(IDuration)
                });
            PatchOptionalPrefix(
                preventStaminaRegenWithStatus,
                typeof(FoodOverexertionDurationPatch),
                nameof(FoodOverexertionDurationPatch.Prefix),
                "PreventStaminaRegenDuration.PreventWithStatus",
                "food-supported overexertion duration");

            MethodInfo buildupStatus = AccessTools.Method(
                typeof(CharacterStatuses),
                nameof(CharacterStatuses.BuildupStatus),
                new[] { typeof(float), typeof(StatusTemplate), typeof(StatusSourceInfo) });
            MethodInfo buildupPrefix = AccessTools.Method(
                typeof(PotionPoisoningBuildupPatch),
                nameof(PotionPoisoningBuildupPatch.Prefix));
            if (buildupStatus == null || buildupPrefix == null)
            {
                Warn("Could not patch CharacterStatuses.BuildupStatus; Potion Poisoning buildup tuning is disabled.");
            }
            else
            {
                try
                {
                    _harmony.Patch(buildupStatus, prefix: new HarmonyMethod(buildupPrefix));
                    LogDiagnostic("Patched CharacterStatuses.BuildupStatus for native Potion Poisoning buildup tuning.");
                }
                catch (Exception ex)
                {
                    Warn("Could not patch CharacterStatuses.BuildupStatus; Potion Poisoning buildup tuning is disabled. " + ex.GetBaseException().Message);
                }
            }

            MethodInfo activateBuildupStatus = AccessTools.Method(
                typeof(BuildupStatus),
                nameof(BuildupStatus.ActivateStatus));
            PatchOptionalPostfix(
                activateBuildupStatus,
                typeof(PotionPoisoningActivationPatch),
                nameof(PotionPoisoningActivationPatch.Postfix),
                "BuildupStatus.ActivateStatus",
                "Potion Poisoning resource-drain setup");

            PatchPotionPoisoningDecay();

            PatchBetterUiConsumableOverlay();
        }

        private void PatchStaminaDepletedVignette()
        {
            MethodInfo startFlash = AccessTools.Method(
                typeof(VHeroStaminaUsedUpEffect),
                nameof(VHeroStaminaUsedUpEffect.StartFlash));
            MethodInfo startPostfix = AccessTools.Method(
                typeof(StaminaVignetteStartPatch),
                nameof(StaminaVignetteStartPatch.Postfix));
            MethodInfo stopFlash = AccessTools.Method(
                typeof(VHeroStaminaUsedUpEffect),
                nameof(VHeroStaminaUsedUpEffect.StopFlash));
            MethodInfo stopPrefix = AccessTools.Method(
                typeof(StaminaVignetteStopPatch),
                nameof(StaminaVignetteStopPatch.Prefix));
            MethodInfo stopPostfix = AccessTools.Method(
                typeof(StaminaVignetteStopPatch),
                nameof(StaminaVignetteStopPatch.Postfix));
            if (startFlash == null
                || startPostfix == null
                || stopFlash == null
                || stopPrefix == null
                || stopPostfix == null)
            {
                Warn("Could not patch the Stamina Depleted vignette; native presentation remains active.");
                return;
            }

            try
            {
                _harmony.Patch(startFlash, null, new HarmonyMethod(startPostfix));
                _harmony.Patch(
                    stopFlash,
                    new HarmonyMethod(stopPrefix),
                    new HarmonyMethod(stopPostfix));
                LogDiagnostic("Patched the native Stamina Depleted vignette for configurable presentation.");
            }
            catch (Exception ex)
            {
                Warn("Could not patch the Stamina Depleted vignette; native presentation remains active. "
                    + ex.GetBaseException().Message);
            }
        }

        private void PatchPotionPoisoningDecay()
        {
            MethodInfo original = AccessTools.Method(
                typeof(BuildupStatus),
                nameof(BuildupStatus.Decay),
                new[] { typeof(float) });
            MethodInfo prefix = AccessTools.Method(
                typeof(PotionPoisoningDecayPatch),
                nameof(PotionPoisoningDecayPatch.Prefix));
            MethodInfo postfix = AccessTools.Method(
                typeof(PotionPoisoningDecayPatch),
                nameof(PotionPoisoningDecayPatch.Postfix));
            if (original == null || prefix == null || postfix == null)
            {
                Warn("Could not patch BuildupStatus.Decay; the Potion Poisoning resource drain is disabled.");
                return;
            }

            try
            {
                _harmony.Patch(
                    original,
                    new HarmonyMethod(prefix),
                    new HarmonyMethod(postfix));
                LogDiagnostic("Patched BuildupStatus.Decay for the Potion Poisoning resource drain.");
            }
            catch (Exception ex)
            {
                Warn("Could not patch BuildupStatus.Decay; the Potion Poisoning resource drain is disabled. " + ex.GetBaseException().Message);
            }
        }

        private void PatchBetterUiConsumableOverlay()
        {
            if (!Chainloader.PluginInfos.ContainsKey(BetterUiPluginGuid))
            {
                return;
            }

            Type helperType = AccessTools.TypeByName("Better_UI.Patches.ConsumableEffectHelper");
            MethodInfo original = helperType == null
                ? null
                : AccessTools.Method(
                    helperType,
                    "GetConsumableEffect",
                    new[] { typeof(Item), typeof(bool) });
            MethodInfo prefix = AccessTools.Method(
                typeof(BetterUiConsumableEffectPatch),
                nameof(BetterUiConsumableEffectPatch.Prefix));
            MethodInfo postfix = AccessTools.Method(
                typeof(BetterUiConsumableEffectPatch),
                nameof(BetterUiConsumableEffectPatch.Postfix));
            MethodInfo finalizer = AccessTools.Method(
                typeof(BetterUiConsumableEffectPatch),
                nameof(BetterUiConsumableEffectPatch.Finalizer));
            if (original == null || prefix == null || postfix == null || finalizer == null)
            {
                Warn("Better UI was detected, but its consumable-effect helper was not compatible; food overlays remain native.");
                return;
            }

            PropertyInfo effectCount = AccessTools.Property(helperType, "LastEffectCount");
            _betterUiLastEffectCountGetter = effectCount == null
                ? null
                : effectCount.GetGetMethod(true);
            _betterUiLastEffectCountSetter = effectCount == null
                ? null
                : effectCount.GetSetMethod(true);

            try
            {
                _harmony.Patch(
                    original,
                    prefix: new HarmonyMethod(prefix),
                    postfix: new HarmonyMethod(postfix),
                    finalizer: new HarmonyMethod(finalizer));
                LogDiagnostic("Patched Better UI consumable overlays for adjusted food health and stamina values.");
            }
            catch (Exception ex)
            {
                Warn("Better UI was detected, but food-overlay compatibility could not be applied. " + ex.GetBaseException().Message);
            }
        }

        private void PatchHostileArrowBallistics()
        {
            MethodInfo original = AccessTools.Method(
                typeof(CombatBehaviourUtils),
                "FireProjectile",
                new[] { typeof(CombatBehaviourUtils.FireProjectileParams), typeof(VGUtils.ShootParams) });
            MethodInfo prefix = AccessTools.Method(typeof(HostileArrowBallisticsPatch), nameof(HostileArrowBallisticsPatch.Prefix));
            MethodInfo transpiler = AccessTools.Method(typeof(HostileArrowBallisticsPatch), nameof(HostileArrowBallisticsPatch.Transpiler));
            MethodInfo finalizer = AccessTools.Method(typeof(HostileArrowBallisticsPatch), nameof(HostileArrowBallisticsPatch.Finalizer));
            if (original == null || prefix == null || transpiler == null || finalizer == null)
            {
                Warn("Could not patch CombatBehaviourUtils.FireProjectile; the hostile arrow ballistics modifiers are disabled.");
                return;
            }

            try
            {
                _harmony.Patch(
                    original,
                    new HarmonyMethod(prefix),
                    null,
                    new HarmonyMethod(transpiler),
                    new HarmonyMethod(finalizer),
                    null);
                LogDiagnostic("Patched CombatBehaviourUtils.FireProjectile for hostile arrow velocity and archer aim scatter.");
            }
            catch (Exception ex)
            {
                Warn("Could not patch CombatBehaviourUtils.FireProjectile; the hostile arrow ballistics modifiers are disabled. " + ex.GetBaseException().Message);
            }
        }

        private bool PatchOptionalPostfix(
            MethodBase original,
            Type patchType,
            string patchMethodName,
            string targetName,
            string featureName)
        {
            MethodInfo postfix = AccessTools.Method(patchType, patchMethodName);
            if (original == null || postfix == null)
            {
                Warn("Could not patch " + targetName + "; the " + featureName + " is disabled.");
                return false;
            }

            try
            {
                _harmony.Patch(original, null, new HarmonyMethod(postfix));
                LogDiagnostic("Patched " + targetName + " for the " + featureName + ".");
                return true;
            }
            catch (Exception ex)
            {
                Warn("Could not patch " + targetName + "; the " + featureName + " is disabled. " + ex.GetBaseException().Message);
                return false;
            }
        }

        private void PatchOptionalPrefix(
            MethodBase original,
            Type patchType,
            string patchMethodName,
            string targetName,
            string featureName)
        {
            MethodInfo prefix = AccessTools.Method(patchType, patchMethodName);
            if (original == null || prefix == null)
            {
                Warn("Could not patch " + targetName + "; the " + featureName + " is disabled.");
                return;
            }

            try
            {
                _harmony.Patch(original, new HarmonyMethod(prefix));
                LogDiagnostic("Patched " + targetName + " for the " + featureName + ".");
            }
            catch (Exception ex)
            {
                Warn("Could not patch " + targetName + "; the " + featureName + " is disabled. " + ex.GetBaseException().Message);
            }
        }

        private void PatchPoiseDamage()
        {
            MethodInfo original = AccessTools.Method(
                typeof(NpcGeneralFSM),
                "OnDamageTaken",
                new[] { typeof(DamageOutcome) });
            MethodInfo prefix = AccessTools.Method(typeof(PlayerPoiseDamagePatch), nameof(PlayerPoiseDamagePatch.Prefix));
            MethodInfo finalizer = AccessTools.Method(typeof(PlayerPoiseDamagePatch), nameof(PlayerPoiseDamagePatch.Finalizer));
            if (original == null || prefix == null || finalizer == null)
            {
                Warn("Could not patch NpcGeneralFSM.OnDamageTaken; the player poise-damage modifier is disabled.");
                return;
            }

            try
            {
                _harmony.Patch(
                    original,
                    new HarmonyMethod(prefix),
                    null,
                    null,
                    new HarmonyMethod(finalizer),
                    null);
                LogDiagnostic("Patched NpcGeneralFSM.OnDamageTaken for the player poise-damage modifier.");
            }
            catch (Exception ex)
            {
                Warn("Could not patch NpcGeneralFSM.OnDamageTaken; the player poise-damage modifier is disabled. " + ex.GetBaseException().Message);
            }

            MethodInfo forceOriginal = AccessTools.Method(
                typeof(EnemyBaseClass),
                "OnDamageTaken",
                new[] { typeof(DamageOutcome) });
            MethodInfo forcePrefix = AccessTools.Method(typeof(PlayerForceDamagePatch), nameof(PlayerForceDamagePatch.Prefix));
            MethodInfo forceFinalizer = AccessTools.Method(typeof(PlayerForceDamagePatch), nameof(PlayerForceDamagePatch.Finalizer));
            if (forceOriginal == null || forcePrefix == null || forceFinalizer == null)
            {
                Warn("Could not patch EnemyBaseClass.OnDamageTaken; resisted-arrow force scaling is disabled.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        forceOriginal,
                        new HarmonyMethod(forcePrefix),
                        null,
                        null,
                        new HarmonyMethod(forceFinalizer),
                        null);
                    LogDiagnostic("Patched EnemyBaseClass.OnDamageTaken for resisted-arrow force scaling.");
                }
                catch (Exception ex)
                {
                    Warn("Could not patch EnemyBaseClass.OnDamageTaken; resisted-arrow force scaling is disabled. " + ex.GetBaseException().Message);
                }
            }

            MethodInfo flinchOriginal = AccessTools.Method(typeof(NpcElement), nameof(NpcElement.DealPoiseDamage));
            MethodInfo flinchPrefix = AccessTools.Method(typeof(RoutineResistedFlinchPatch), nameof(RoutineResistedFlinchPatch.Prefix));
            if (flinchOriginal == null || flinchPrefix == null)
            {
                Warn("Could not patch NpcElement.DealPoiseDamage; strongly resisted hits keep their native small flinch.");
            }
            else
            {
                try
                {
                    _harmony.Patch(flinchOriginal, new HarmonyMethod(flinchPrefix));
                    LogDiagnostic("Patched NpcElement.DealPoiseDamage for strongly resisted hit reactions.");
                }
                catch (Exception ex)
                {
                    Warn("Could not patch NpcElement.DealPoiseDamage; strongly resisted hits keep their native small flinch. " + ex.GetBaseException().Message);
                }
            }
        }

        private void PatchProgressiveTenacity()
        {
            MethodInfo staminaOriginal = AccessTools.Method(
                typeof(HealthElement),
                "BeforeHealthDecreaseEvents",
                new[] { typeof(Damage) });
            MethodInfo staminaPostfix = AccessTools.Method(
                typeof(ProgressiveTenacityStaminaDamagePatch),
                nameof(ProgressiveTenacityStaminaDamagePatch.Postfix));
            if (staminaOriginal == null || staminaPostfix == null)
            {
                Warn("Could not patch HealthElement.BeforeHealthDecreaseEvents; Progressive Tenacity will not reduce direct stamina damage.");
            }
            else
            {
                try
                {
                    _harmony.Patch(staminaOriginal, null, new HarmonyMethod(staminaPostfix));
                    LogDiagnostic("Patched HealthElement.BeforeHealthDecreaseEvents for Progressive Tenacity stamina damage.");
                }
                catch (Exception ex)
                {
                    Warn("Could not patch HealthElement.BeforeHealthDecreaseEvents; Progressive Tenacity will not reduce direct stamina damage. " + ex.GetBaseException().Message);
                }
            }

            MethodInfo parryOriginal = AccessTools.Method(typeof(HeroParry), "OnTakingDamage");
            MethodInfo parryPrefix = AccessTools.Method(
                typeof(ProgressiveTenacityParryPatch),
                nameof(ProgressiveTenacityParryPatch.Prefix));
            MethodInfo parryFinalizer = AccessTools.Method(
                typeof(ProgressiveTenacityParryPatch),
                nameof(ProgressiveTenacityParryPatch.Finalizer));
            if (parryOriginal == null || parryPrefix == null || parryFinalizer == null)
            {
                Warn("Could not patch HeroParry.OnTakingDamage; Progressive Tenacity will not reduce parry stamina damage.");
                return;
            }

            try
            {
                _harmony.Patch(
                    parryOriginal,
                    new HarmonyMethod(parryPrefix),
                    null,
                    null,
                    new HarmonyMethod(parryFinalizer),
                    null);
                LogDiagnostic("Patched HeroParry.OnTakingDamage for Progressive Tenacity parry stamina damage.");
            }
            catch (Exception ex)
            {
                Warn("Could not patch HeroParry.OnTakingDamage; Progressive Tenacity will not reduce parry stamina damage. " + ex.GetBaseException().Message);
            }
        }

        private bool DifficultyModifiersAreEnabled()
        {
            return _enabled != null
                && _enabled.Value
                && _difficultyModifiersEnabled != null
                && _difficultyModifiersEnabled.Value;
        }

        private bool DifficultyModifierIsEnabled(ConfigEntry<bool> setting)
        {
            return DifficultyModifiersAreEnabled() && setting != null && setting.Value;
        }

        private float PresetPenaltyAmount()
        {
            if (_preset == null)
            {
                return 0.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 0.05f;
                case Preset.Crucible:
                    return 0.10f;
                case Preset.Tempered:
                default:
                    return 0.0f;
            }
        }

        private float PresetCostMultiplier()
        {
            return 1.0f + PresetPenaltyAmount();
        }

        private static float GetPresetCombatSustainabilityMultiplier(Preset preset)
        {
            switch (preset)
            {
                case Preset.Hardened:
                    return 0.75f;
                case Preset.Crucible:
                    return 0.50f;
                case Preset.Tempered:
                default:
                    return 1.0f;
            }
        }

        private void ApplyPresetCombatSustainabilityMultipliers()
        {
            if (_preset == null)
            {
                return;
            }

            float presetValue = GetPresetCombatSustainabilityMultiplier(_preset.Value);
            if (_combatManaRegenerationMultiplier != null
                && Math.Abs(_combatManaRegenerationMultiplier.Value - presetValue) > NeutralTolerance)
            {
                _combatManaRegenerationMultiplier.Value = presetValue;
            }
            if (_positiveParryWindowBonusMultiplier != null
                && Math.Abs(_positiveParryWindowBonusMultiplier.Value - presetValue) > NeutralTolerance)
            {
                _positiveParryWindowBonusMultiplier.Value = presetValue;
            }
        }

        private float PresetReductionMultiplier()
        {
            return 1.0f - PresetPenaltyAmount();
        }

        private float PresetPlayerPressureAmount()
        {
            if (_preset == null)
            {
                return 0.05f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 0.10f;
                case Preset.Crucible:
                    return 0.15f;
                case Preset.Tempered:
                default:
                    return 0.05f;
            }
        }

        private float PresetPlayerPressureCostMultiplier()
        {
            return 1.0f + PresetPlayerPressureAmount();
        }

        private float PresetPlayerPressureReductionMultiplier()
        {
            return 1.0f - PresetPlayerPressureAmount();
        }

        private void ApplyCombatManaRegeneration(Hero hero, ref float regeneration)
        {
            if (!DifficultyModifierIsEnabled(_modifyCombatManaRegeneration)
                || _combatManaRegenerationMultiplier == null
                || hero == null
                || hero.HasBeenDiscarded
                || hero.HeroCombat == null
                || !hero.HeroCombat.IsHeroInFight
                || regeneration <= 0.0f)
            {
                return;
            }

            float configuredMultiplier = Mathf.Clamp01(_combatManaRegenerationMultiplier.Value);
            if (ApproximatelyNeutral(configuredMultiplier))
            {
                return;
            }

            float manaShield = hero.ManaShield == null
                ? 0.0f
                : Mathf.Clamp01(hero.ManaShield.ModifiedValue);
            float effectiveMultiplier = Mathf.Lerp(configuredMultiplier, 1.0f, manaShield);
            regeneration *= effectiveMultiplier;
        }

        private void ApplyPositiveParryWindowBonus(Hero hero, ref IDuration duration)
        {
            if (!DifficultyModifierIsEnabled(_modifyParryWindowBonus)
                || _positiveParryWindowBonusMultiplier == null
                || hero == null
                || !ReferenceEquals(hero, Hero.Current))
            {
                return;
            }

            TimeDuration timeDuration = duration as TimeDuration;
            if (timeDuration == null
                || timeDuration.OriginalTime <= NativeBaseParryWindowSeconds + NeutralTolerance)
            {
                return;
            }

            float multiplier = Mathf.Clamp01(_positiveParryWindowBonusMultiplier.Value);
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float positiveBonus = timeDuration.OriginalTime - NativeBaseParryWindowSeconds;
            duration = new TimeDuration(
                NativeBaseParryWindowSeconds + (positiveBonus * multiplier),
                timeDuration.UnscaledTime);
        }

        private float PresetArrowVelocityMultiplier()
        {
            if (_preset == null)
            {
                return 1.10f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.30f;
                case Preset.Crucible:
                    return 1.50f;
                case Preset.Tempered:
                default:
                    return 1.10f;
            }
        }

        private static float GetPresetHostileArcherAimScatter(Preset preset)
        {
            switch (preset)
            {
                case Preset.Tempered:
                    return 1.50f;
                case Preset.Crucible:
                    return 1.00f;
                case Preset.Hardened:
                default:
                    return 1.25f;
            }
        }

        private void ApplyPresetHostileArcherAimScatter()
        {
            if (_hostileArcherAimScatter == null || _preset == null)
            {
                return;
            }

            float presetValue = GetPresetHostileArcherAimScatter(_preset.Value);
            if (Math.Abs(_hostileArcherAimScatter.Value - presetValue) > NeutralTolerance)
            {
                _hostileArcherAimScatter.Value = presetValue;
            }
        }

        private static bool GetPresetPreventFoodUseInCombat(Preset preset)
        {
            switch (preset)
            {
                case Preset.Tempered:
                    return false;
                case Preset.Hardened:
                case Preset.Crucible:
                default:
                    return true;
            }
        }

        private void ApplyPresetPreventFoodUseInCombat()
        {
            if (_preventFoodUseInCombat == null || _preset == null)
            {
                return;
            }

            bool presetValue = GetPresetPreventFoodUseInCombat(_preset.Value);
            if (_preventFoodUseInCombat.Value != presetValue)
            {
                _preventFoodUseInCombat.Value = presetValue;
            }
        }

        private float PresetEnemySightRangeMultiplier()
        {
            if (_preset == null)
            {
                return 1.20f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.40f;
                case Preset.Crucible:
                    return 1.60f;
                case Preset.Tempered:
                default:
                    return 1.20f;
            }
        }

        private float PresetEnemyHearingRangeMultiplier()
        {
            if (_preset == null)
            {
                return 1.20f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.40f;
                case Preset.Crucible:
                    return 1.60f;
                case Preset.Tempered:
                default:
                    return 1.20f;
            }
        }

        private float PresetEnemyAggroPersistenceMultiplier()
        {
            if (_preset == null)
            {
                return 1.20f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.40f;
                case Preset.Crucible:
                    return 1.60f;
                case Preset.Tempered:
                default:
                    return 1.20f;
            }
        }

        private float PresetPotionPoisoningBuildup()
        {
            if (_preset == null)
            {
                return NativePotionPoisoningBuildup;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 65.0f;
                case Preset.Crucible:
                    return 70.0f;
                case Preset.Tempered:
                default:
                    return 60.0f;
            }
        }

        private float PresetFoodHealthRateMultiplier()
        {
            if (_preset == null)
            {
                return 1.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 0.375f;
                case Preset.Crucible:
                    return 0.25f;
                case Preset.Tempered:
                default:
                    return 0.5f;
            }
        }

        private float PresetFoodHealthDurationMultiplier()
        {
            if (_preset == null)
            {
                return 1.0f;
            }

            return 4.0f;
        }

        private float PresetFoodStaminaRate()
        {
            if (_preset == null)
            {
                return 0.0f;
            }

            return 1.0f;
        }

        private float PresetEnemyMovementSpeedMultiplier(NpcElement npc)
        {
            float mobilityShare = EnemyMovementMobilityShare(npc);
            return 1.0f + PresetPenaltyAmount() * mobilityShare;
        }

        private float EnemyMovementMobilityShare(NpcElement npc)
        {
            if (npc == null || npc.Template == null)
            {
                return 0.0f;
            }

            NpcType npcType = npc.Template.NpcType;
            if (npcType == NpcType.Boss
                || npcType == NpcType.MiniBoss
                || npcType == NpcType.Critter
                || !npc.Template.requiresPathToTarget)
            {
                return 0.0f;
            }

            TargetClassification targetClass = GetTargetClassification(npc, npc.HealthElement);
            if (targetClass.IsBossClass
                || targetClass.IsBear
                || targetClass.IsConstruct
                || targetClass.IsFlora)
            {
                return 0.0f;
            }

            int weight = npc.Template.npcWeight;
            float mobilityShare = weight >= 250 ? 0.0f : weight >= 150 ? 0.5f : 1.0f;
            if (npcType == NpcType.Elite || targetClass.IsBulkyMonster)
            {
                mobilityShare = Math.Min(mobilityShare, 0.5f);
            }

            EnemyArmorTier armorTier = targetClass.ArmorProfile == null
                ? EnemyArmorTier.Unknown
                : targetClass.ArmorProfile.Tier;
            string armorEvidence;
            if (armorTier != EnemyArmorTier.Unknown
                || TryGetOrdinaryHumanoidArmorTier(targetClass, out armorTier, out armorEvidence))
            {
                if (armorTier == EnemyArmorTier.Heavy)
                {
                    return 0.0f;
                }
                if (armorTier == EnemyArmorTier.Medium)
                {
                    mobilityShare = Math.Min(mobilityShare, 0.5f);
                }
            }

            return mobilityShare;
        }

        private float PresetLightArmorMovementMultiplier()
        {
            if (_preset == null)
            {
                return 1.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.025f;
                case Preset.Crucible:
                    return 1.05f;
                case Preset.Tempered:
                default:
                    return 1.0f;
            }
        }

        private float PresetPhysicalArmorMultiplier(ItemWeight armorWeight)
        {
            if (_preset == null || armorWeight == null)
            {
                return 1.0f;
            }

            bool heavy = armorWeight == ItemWeight.Heavy || armorWeight == ItemWeight.Overload;
            if (armorWeight != ItemWeight.Medium && !heavy)
            {
                return 1.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return heavy ? 1.10f : 1.05f;
                case Preset.Crucible:
                    return heavy ? 1.20f : 1.10f;
                case Preset.Tempered:
                default:
                    return 1.0f;
            }
        }

        private int PresetAttackSlotBonus()
        {
            if (_preset == null)
            {
                return 0;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1;
                case Preset.Crucible:
                    return 2;
                case Preset.Tempered:
                default:
                    return 0;
            }
        }

        private void ApplyOutgoingHealthDamageModifier(ref float damageModifier)
        {
            if (!DifficultyModifierIsEnabled(_modifyPlayerDamageDealt))
            {
                return;
            }

            float multiplier = PresetPlayerPressureReductionMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = damageModifier;
            damageModifier *= multiplier;
            LogDifficultyDiagnostic("PlayerDamageDealt", before, damageModifier, multiplier);
        }

        private static float GetPresetWeakSpotDamageBonus(Preset preset)
        {
            switch (preset)
            {
                case Preset.Tempered:
                    return 0.10f;
                case Preset.Crucible:
                    return 0.30f;
                case Preset.Hardened:
                default:
                    return 0.20f;
            }
        }

        private void ApplyPresetWeakSpotDamageBonus()
        {
            if (_weakSpotDamageBonus == null || _preset == null)
            {
                return;
            }

            float presetValue = GetPresetWeakSpotDamageBonus(_preset.Value);
            if (Math.Abs(_weakSpotDamageBonus.Value - presetValue) > NeutralTolerance)
            {
                _weakSpotDamageBonus.Value = presetValue;
            }
        }

        private float GetActiveWeakSpotDamageBonus()
        {
            if (!DifficultyModifiersAreEnabled() || _weakSpotDamageBonus == null)
            {
                return 0.0f;
            }

            return Mathf.Clamp(_weakSpotDamageBonus.Value, 0.0f, 0.50f);
        }

        private void ApplyWeakSpotDamageBonus(
            DamageModifiersInfo modifiersInfo,
            ref float damageModifier)
        {
            if (!modifiersInfo.IsWeakSpot)
            {
                return;
            }

            float bonus = GetActiveWeakSpotDamageBonus();
            if (bonus <= NeutralTolerance)
            {
                return;
            }

            float before = damageModifier;
            damageModifier += bonus;
            LogDifficultyDiagnostic(
                "WeakSpotDamageBonus",
                before,
                damageModifier,
                bonus);
        }

        private void ApplyIncomingHealthDamageModifier(ref float damageModifier)
        {
            if (!DifficultyModifierIsEnabled(_modifyPlayerDamageTaken))
            {
                return;
            }

            float multiplier = PresetPlayerPressureCostMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = damageModifier;
            damageModifier *= multiplier;
            LogDifficultyDiagnostic("PlayerDamageTaken", before, damageModifier, multiplier);
        }

        private void ApplyPassiveShieldProtection(Hero hero, Damage damage, ref float damageModifier)
        {
            if (!DifficultyModifierIsEnabled(_passiveShieldProtectionEnabled)
                || hero == null
                || damage == null
                || !hero.WeaponsVisible
                || hero.IsBlocking
                || damage.IsBlocked
                || damage.IsParried
                || !damage.CanBeBlocked
                || damage.Type != DamageType.PhysicalHitSource)
            {
                return;
            }

            bool shieldInOffHand = true;
            Item shield = hero.Inventory.EquippedItem(EquipmentSlotType.OffHand);
            if (shield == null || !shield.IsShield)
            {
                shieldInOffHand = false;
                shield = hero.Inventory.EquippedItem(EquipmentSlotType.MainHand);
            }

            if (shield == null
                || !shield.IsShield
                || shield.ItemStats == null
                || IsVersatileWeaponsHandSuppressed(shieldInOffHand))
            {
                return;
            }

            ICharacter damageDealer = damage.DamageDealer;
            if (damageDealer == null)
            {
                return;
            }

            Vector3 incomingDirection = damageDealer.Coords - hero.Coords;
            incomingDirection.y = 0.0f;
            Vector3 heroForward = hero.Forward();
            heroForward.y = 0.0f;
            if (incomingDirection.sqrMagnitude <= NeutralTolerance
                || heroForward.sqrMagnitude <= NeutralTolerance)
            {
                return;
            }

            float blockHalfAngle = Mathf.Clamp(shield.ItemStats.BlockAngle.ModifiedValue, 0.0f, 90.0f);
            float minimumForwardDot = Mathf.Cos(blockHalfAngle * Mathf.Deg2Rad);
            if (Vector3.Dot(heroForward.normalized, incomingDirection.normalized) <= minimumForwardDot)
            {
                return;
            }

            float effectiveBlock = shield.ItemStats.Block.ModifiedValue
                * ItemRequirementsUtils.GetBlockDamageReductionMultiplier(hero, shield);
            effectiveBlock = Mathf.Clamp(effectiveBlock, 0.0f, 100.0f);

            float presetShare = 0.10f;
            if (_preset != null)
            {
                switch (_preset.Value)
                {
                    case Preset.Tempered:
                        presetShare = 0.08f;
                        break;
                    case Preset.Crucible:
                        presetShare = 0.12f;
                        break;
                }
            }

            float passiveReduction = effectiveBlock * 0.01f * presetShare;
            if (passiveReduction <= 0.0f)
            {
                return;
            }

            float multiplier = 1.0f - passiveReduction;
            float before = damageModifier;
            damageModifier *= multiplier;
            LogDifficultyDiagnostic("PassiveShieldProtection", before, damageModifier, multiplier);
        }

        private bool IsVersatileWeaponsHandSuppressed(bool offHand)
        {
            if (!TryResolveVersatileWeaponsBridge())
            {
                return false;
            }

            try
            {
                return offHand
                    ? _versatileWeaponsIsOffHandSuppressed()
                    : _versatileWeaponsIsMainHandSuppressed();
            }
            catch (Exception exception)
            {
                _versatileWeaponsIsMainHandSuppressed = null;
                _versatileWeaponsIsOffHandSuppressed = null;
                if (!_versatileWeaponsBridgeFailureLogged)
                {
                    _versatileWeaponsBridgeFailureLogged = true;
                    Logger.LogWarning(
                        "Versatile Weapons hand-suppression API failed; passive shield protection is using native equipment state: "
                        + exception.GetBaseException().Message);
                }
                return false;
            }
        }

        private bool TryResolveVersatileWeaponsBridge()
        {
            if (_versatileWeaponsBridgeResolved)
            {
                return _versatileWeaponsIsMainHandSuppressed != null
                    && _versatileWeaponsIsOffHandSuppressed != null;
            }

            _versatileWeaponsBridgeResolved = true;
            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    VersatileWeaponsPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                VersatileWeaponsApiTypeName,
                false);
            MethodInfo mainMethod = apiType == null
                ? null
                : AccessTools.Method(
                    apiType,
                    "IsMainHandSuppressed",
                    Type.EmptyTypes);
            MethodInfo offMethod = apiType == null
                ? null
                : AccessTools.Method(
                    apiType,
                    "IsOffHandSuppressed",
                    Type.EmptyTypes);
            if (mainMethod == null || offMethod == null)
            {
                if (!_versatileWeaponsBridgeFailureLogged)
                {
                    _versatileWeaponsBridgeFailureLogged = true;
                    Logger.LogWarning(
                        "Versatile Weapons is loaded without its hand-suppression API; passive shield protection is using native equipment state.");
                }
                return false;
            }

            try
            {
                _versatileWeaponsIsMainHandSuppressed =
                    (Func<bool>)Delegate.CreateDelegate(
                        typeof(Func<bool>),
                        mainMethod);
                _versatileWeaponsIsOffHandSuppressed =
                    (Func<bool>)Delegate.CreateDelegate(
                        typeof(Func<bool>),
                        offMethod);
                return true;
            }
            catch (Exception exception)
            {
                if (!_versatileWeaponsBridgeFailureLogged)
                {
                    _versatileWeaponsBridgeFailureLogged = true;
                    Logger.LogWarning(
                        "Versatile Weapons hand-suppression API binding failed; passive shield protection is using native equipment state: "
                        + exception.GetBaseException().Message);
                }
                return false;
            }
        }

        private void ApplyPlayerArrowVelocity(ref Vector3 arrowVelocity)
        {
            if (!DifficultyModifierIsEnabled(_modifyPlayerArrowVelocity))
            {
                return;
            }

            float multiplier = PresetArrowVelocityMultiplier();
            float before = arrowVelocity.magnitude;
            arrowVelocity *= multiplier;
            LogDifficultyDiagnostic("PlayerArrowVelocity", before, arrowVelocity.magnitude, multiplier);
        }

        private void ApplyPlayerArrowGravity(DamageDealingProjectile projectile, float deltaTime)
        {
            if (!PlayerArrowDropModifierIsEffective()
                || projectile == null
                || !(projectile is Arrow)
                || Hero.Current == null
                || !ReferenceEquals(projectile.Owner, Hero.Current)
                || deltaTime <= 0.0f)
            {
                return;
            }

            Rigidbody body = projectile.GetComponentInChildren<Rigidbody>(true);
            if (body == null || body.isKinematic || !body.useGravity)
            {
                return;
            }

            float fixedDeltaTime = Time.fixedDeltaTime;
            if (fixedDeltaTime <= 0.0f)
            {
                return;
            }

            float gravityMultiplier = Clamp(_playerArrowGravityMultiplier.Value, 0.25f, 1.0f);
            float localTimeScale = deltaTime / fixedDeltaTime;
            float cancellationScale = (1.0f - gravityMultiplier) * localTimeScale * localTimeScale;
            body.AddForce(-Physics.gravity * cancellationScale, ForceMode.Acceleration);
        }

        private void ApplyPhysicalArmorProtection(Hero hero, DamageSubType damageType, ref float armor)
        {
            if (!DifficultyModifierIsEnabled(_modifyArmorPhysicalProtection)
                || !IsPhysicalDamageSubtype(damageType)
                || armor <= 0.0f)
            {
                return;
            }

            ArmorWeight armorWeight = hero == null ? null : hero.TryGetElement<ArmorWeight>();
            float multiplier = PresetPhysicalArmorMultiplier(armorWeight == null ? null : armorWeight.ArmorWeightType);
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = armor;
            armor *= multiplier;
            LogDifficultyDiagnostic("PhysicalArmor", before, armor, multiplier);
        }

        private static bool IsPhysicalDamageSubtype(DamageSubType damageType)
        {
            return damageType == DamageSubType.GenericPhysical
                || damageType == DamageSubType.Slashing
                || damageType == DamageSubType.Piercing
                || damageType == DamageSubType.Bludgeoning;
        }

        private void ApplyEnemyHearingRange(ref float noiseRange)
        {
            if (!DifficultyModifierIsEnabled(_modifyEnemyHearingRange) || noiseRange <= 0.0f)
            {
                return;
            }

            float multiplier = PresetEnemyHearingRangeMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = noiseRange;
            noiseRange *= multiplier;
            LogEnemyHearingRangeDiagnostic(before, noiseRange, multiplier);
        }

        private void ApplyEnemyAggroPersistence(NpcAI npcAI, ref float aggroDecreaseModifier)
        {
            if (!DifficultyModifierIsEnabled(_modifyEnemyAggroPersistence)
                || npcAI == null
                || !npcAI.Working
                || !npcAI.InCombat
                || npcAI.NpcElement == null
                || !npcAI.NpcElement.IsAlive
                || npcAI.NpcElement.IsSummonOrAlly
                || !WithFactionUtils.IsHostileToHero(npcAI.NpcElement)
                || aggroDecreaseModifier <= 0.0f)
            {
                return;
            }

            float multiplier = PresetEnemyAggroPersistenceMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = aggroDecreaseModifier;
            aggroDecreaseModifier /= multiplier;
            LogDifficultyDiagnostic("EnemyAggroDecrease", before, aggroDecreaseModifier, 1.0f / multiplier);
        }

        private void CaptureConsumableRecovery(
            ItemSkillsInvoker invoker,
            ItemActionType actionType,
            ref ConsumableRecoveryPatchState state)
        {
            state = null;
            if (invoker == null || actionType != ItemActionType.Eat)
            {
                return;
            }

            Item item = invoker.ParentModel;
            Hero hero = Hero.Current;
            if (item == null
                || item.Template == null
                || item.Owner == null
                || hero == null
                || !ReferenceEquals(item.Owner.Character, hero))
            {
                return;
            }

            if (PotionOverdrinkingModifierIsEnabled()
                && item.Template.IsPotion)
            {
                state = new ConsumableRecoveryPatchState
                {
                    Hero = hero,
                    PotionTemplateGuid = item.Template.GUID,
                    PotionPoisoningBuckets = ClassifyPotionPoisoningBuckets(item),
                    IsPotionConsumption = true,
                    PreviousActiveState = _activeConsumableRecoveryState
                };
                _activeConsumableRecoveryState = state;
                return;
            }

            if (!item.IsEdible
                || item.Template.IsPotion
                || !FoodRecoveryModifierIsEffective())
            {
                return;
            }

            FoodSkillOverrideState foodState = ApplyFoodSkillOverrides(item, hero);
            if (foodState == null)
            {
                return;
            }

            RemoveLegacyFoodStaminaStatus(hero);
            state = new ConsumableRecoveryPatchState
            {
                Food = foodState
            };
        }

        private bool ShouldAllowFoodUse(Item item)
        {
            if (!FoodCombatRestrictionIsEffective()
                || item == null
                || item.Template == null
                || (!item.Template.IsPlainFood && !item.Template.IsDish))
            {
                return true;
            }

            Hero hero = Hero.Current;
            if (hero == null
                || item.Owner == null
                || !ReferenceEquals(item.Owner.Character, hero)
                || hero.HeroCombat == null
                || !hero.HeroCombat.IsHeroInFight)
            {
                return true;
            }

            ShowFoodCombatRestrictionNotification();
            LogDiagnostic(
                "Blocked food use during combat: item="
                + item.Template.GUID
                + ", preset="
                + _preset.Value.ToString()
                + ".");
            return false;
        }

        private void ShowFoodCombatRestrictionNotification()
        {
            float now = Time.unscaledTime;
            if (now < _nextFoodCombatNotificationAt)
            {
                return;
            }

            _nextFoodCombatNotificationAt = now + FoodCombatNotificationCooldownSeconds;
            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowEventNotification(
                PluginGuid,
                "food-combat-blocked",
                "Food cannot be consumed during combat.",
                "Gold",
                "Status",
                "Normal",
                "food-combat-blocked",
                "food",
                "VeryShort",
                0.25f,
                0.9f);
        }

        private void ApplyConsumableRecovery(ConsumableRecoveryPatchState state)
        {
            if (state == null)
            {
                return;
            }

            RestoreFoodSkillOverrides(state.Food);
            MarkFoodStatusForStaminaRecovery(state.Food);
            EnforceSingleFoodRecoveryStatus(state.Food == null ? null : state.Food.Hero);
            ApplyClassPotionPoisoningBuildup(state);
        }

        private static PotionPoisoningBucket ClassifyPotionPoisoningBuckets(Item item)
        {
            if (item == null)
            {
                return PotionPoisoningBucket.Utility;
            }

            PotionPoisoningBucket buckets = PotionPoisoningBucket.None;
            foreach (Skill skill in item.ItemEffectsSkills)
            {
                if (skill == null || skill.Graph == null)
                {
                    continue;
                }

                string graphGuid = skill.Graph.GUID;
                if (!string.Equals(graphGuid, FlatPotionRestorationGraphGuid, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(graphGuid, PercentPotionRestorationGraphGuid, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(graphGuid, TimedPotionRestorationGraphGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                StatType restoredStat = skill.GetRichEnum("StatEnum");
                if (restoredStat == AliveStatType.Health)
                {
                    buckets |= PotionPoisoningBucket.Health;
                }
                else if (restoredStat == CharacterStatType.Mana)
                {
                    buckets |= PotionPoisoningBucket.Mana;
                }
                else if (restoredStat == CharacterStatType.Stamina)
                {
                    buckets |= PotionPoisoningBucket.Stamina;
                }
            }

            return buckets == PotionPoisoningBucket.None
                ? PotionPoisoningBucket.Utility
                : buckets;
        }

        private void ApplyClassPotionPoisoningBuildup(ConsumableRecoveryPatchState state)
        {
            if (state == null
                || !state.IsPotionConsumption
                || state.Hero == null
                || state.Hero.Statuses == null
                || !PotionOverdrinkingModifierIsEnabled())
            {
                return;
            }

            DecayPotionPoisoningBuckets(state.Hero);
            if (IsPotionPoisoningActive(state.Hero))
            {
                ClearPotionPoisoningBuckets(state.Hero);
                return;
            }

            float buildup = PresetPotionPoisoningBuildup();
            if ((state.PotionPoisoningBuckets & PotionPoisoningBucket.Health) != 0)
            {
                _healthPotionPoisoningBuildup += buildup;
            }
            if ((state.PotionPoisoningBuckets & PotionPoisoningBucket.Mana) != 0)
            {
                _manaPotionPoisoningBuildup += buildup;
            }
            if ((state.PotionPoisoningBuckets & PotionPoisoningBucket.Stamina) != 0)
            {
                _staminaPotionPoisoningBuildup += buildup;
            }
            if ((state.PotionPoisoningBuckets & PotionPoisoningBucket.Utility) != 0)
            {
                _utilityPotionPoisoningBuildup += buildup;
            }

            PotionPoisoningBucket completedBuckets = PotionPoisoningBucket.None;
            if (_healthPotionPoisoningBuildup >= NativePotionPoisoningThreshold)
            {
                completedBuckets |= PotionPoisoningBucket.Health;
            }
            if (_manaPotionPoisoningBuildup >= NativePotionPoisoningThreshold)
            {
                completedBuckets |= PotionPoisoningBucket.Mana;
            }
            if (_staminaPotionPoisoningBuildup >= NativePotionPoisoningThreshold)
            {
                completedBuckets |= PotionPoisoningBucket.Stamina;
            }
            if (_utilityPotionPoisoningBuildup >= NativePotionPoisoningThreshold)
            {
                completedBuckets |= PotionPoisoningBucket.Utility;
            }
            LogDifficultyDiagnostic(
                "PotionPoisoningBuckets:" + state.PotionPoisoningBuckets,
                0.0f,
                buildup,
                buildup);
            if (completedBuckets == PotionPoisoningBucket.None)
            {
                return;
            }

            TemplatesProvider templates = null;
            if (World.Services == null
                || !World.Services.TryGet<TemplatesProvider>(out templates)
                || !templates.AllLoaded)
            {
                ReportPotionPoisoningTemplateFailure("the native template provider was not ready");
                return;
            }

            StatusTemplate statusTemplate = templates.Get<StatusTemplate>(
                PotionPoisoningStatusGuid);
            if (statusTemplate == null)
            {
                ReportPotionPoisoningTemplateFailure("the native Potion Poisoning template was unavailable");
                return;
            }

            try
            {
                _applyingCompletedPotionPoisoningBuildup = true;
                _pendingPotionPoisoningPenaltyBuckets = completedBuckets;
                state.Hero.Statuses.BuildupStatus(
                    NativePotionPoisoningThreshold,
                    statusTemplate,
                    StatusSourceInfo.FromStatus(statusTemplate).WithCharacter(state.Hero));

                foreach (Status status in state.Hero.Statuses.AllStatuses)
                {
                    BuildupStatus potionPoisoning = status as BuildupStatus;
                    if (IsPotionPoisoningStatus(potionPoisoning)
                        && !potionPoisoning.Active)
                    {
                        potionPoisoning.CompleteBuildup();
                        break;
                    }
                }
            }
            finally
            {
                _applyingCompletedPotionPoisoningBuildup = false;
                _pendingPotionPoisoningPenaltyBuckets = PotionPoisoningBucket.None;
                ClearPotionPoisoningBuckets(state.Hero);
            }
        }

        private void DecayPotionPoisoningBuckets(Hero hero)
        {
            float now = Time.time;
            if (!ReferenceEquals(_potionPoisoningBucketHero, hero)
                || _potionPoisoningBucketUpdatedAt < 0.0f
                || now < _potionPoisoningBucketUpdatedAt)
            {
                ClearPotionPoisoningBuckets(hero);
                return;
            }

            float decay = (now - _potionPoisoningBucketUpdatedAt)
                * NativePotionPoisoningDecayPerSecond;
            _healthPotionPoisoningBuildup = Mathf.Max(0.0f, _healthPotionPoisoningBuildup - decay);
            _manaPotionPoisoningBuildup = Mathf.Max(0.0f, _manaPotionPoisoningBuildup - decay);
            _staminaPotionPoisoningBuildup = Mathf.Max(0.0f, _staminaPotionPoisoningBuildup - decay);
            _utilityPotionPoisoningBuildup = Mathf.Max(0.0f, _utilityPotionPoisoningBuildup - decay);
            _potionPoisoningBucketUpdatedAt = now;
        }

        private void ClearPotionPoisoningBuckets(Hero hero)
        {
            _potionPoisoningBucketHero = hero;
            _potionPoisoningBucketUpdatedAt = Time.time;
            _healthPotionPoisoningBuildup = 0.0f;
            _manaPotionPoisoningBuildup = 0.0f;
            _staminaPotionPoisoningBuildup = 0.0f;
            _utilityPotionPoisoningBuildup = 0.0f;
        }

        private static bool IsPotionPoisoningActive(Hero hero)
        {
            if (hero == null || hero.Statuses == null)
            {
                return false;
            }

            foreach (Status status in hero.Statuses.AllStatuses)
            {
                BuildupStatus buildupStatus = status as BuildupStatus;
                if (IsPotionPoisoningStatus(buildupStatus)
                    && buildupStatus.Active)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsPotionPoisoningStatus(Status status)
        {
            return status != null
                && status.Template != null
                && string.Equals(
                    status.Template.GUID,
                    PotionPoisoningStatusGuid,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void ReportPotionPoisoningTemplateFailure(string reason)
        {
            if (_reportedPotionPoisoningTemplateFailure)
            {
                return;
            }

            _reportedPotionPoisoningTemplateFailure = true;
            Warn("Could not complete class-based Potion Poisoning because " + reason + ".");
        }

        private void FinishConsumableRecovery(ConsumableRecoveryPatchState state)
        {
            if (state != null && ReferenceEquals(_activeConsumableRecoveryState, state))
            {
                _activeConsumableRecoveryState = state.PreviousActiveState;
            }
        }

        private FoodSkillOverrideState ApplyFoodSkillOverrides(Item item, Hero hero)
        {
            if (item == null
                || hero == null
                || SkillVariableOverridesField == null
                || !FoodRecoveryModifierIsEffective())
            {
                return null;
            }

            FoodSkillOverrideState state = new FoodSkillOverrideState
            {
                Item = item,
                Hero = hero
            };
            CaptureActiveFoodStatuses(hero, state.ExistingFoodStatuses);

            try
            {
                float rateMultiplier = PresetFoodHealthRateMultiplier();
                float durationMultiplier = PresetFoodHealthDurationMultiplier();
                foreach (Skill skill in item.ItemEffectsSkills)
                {
                    if (skill == null
                        || skill.Graph == null
                        || !string.Equals(skill.Graph.GUID, StandardFoodRecoveryGraphGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (skill.GetRichEnum("StatEnum") != AliveStatType.HealthRegen)
                    {
                        continue;
                    }

                    float? authoredDuration = skill.GetVariable("Duration", hero);
                    if (authoredDuration.HasValue)
                    {
                        state.RecoveryDuration = Mathf.Max(
                            state.RecoveryDuration,
                            authoredDuration.Value * durationMultiplier);
                    }

                    List<SkillVariable> currentOverrides =
                        SkillVariableOverridesField.GetValue(skill) as List<SkillVariable>;
                    state.Snapshots.Add(new SkillVariableSnapshot
                    {
                        Skill = skill,
                        VariableOverrides = CloneSkillVariables(currentOverrides)
                    });

                    float? addValue = skill.GetVariable("AddValue", hero);
                    float? gain = skill.GetVariable("Gain", hero);
                    if (addValue.HasValue)
                    {
                        skill.OverrideVariable("AddValue", addValue.Value * rateMultiplier);
                    }
                    if (gain.HasValue)
                    {
                        skill.OverrideVariable("Gain", gain.Value * rateMultiplier);
                    }
                    if (authoredDuration.HasValue)
                    {
                        skill.OverrideVariable("Duration", authoredDuration.Value * durationMultiplier);
                    }
                }

                if (state.RecoveryDuration <= NeutralTolerance)
                {
                    RestoreFoodSkillOverrides(state);
                    return null;
                }

                return state;
            }
            catch
            {
                RestoreFoodSkillOverrides(state);
                throw;
            }
        }

        private static List<SkillVariable> CloneSkillVariables(List<SkillVariable> variables)
        {
            if (variables == null)
            {
                return null;
            }

            List<SkillVariable> clone = new List<SkillVariable>(variables.Count);
            for (int i = 0; i < variables.Count; i++)
            {
                SkillVariable variable = variables[i];
                clone.Add(variable == null ? null : variable.Copy());
            }
            return clone;
        }

        private static void RestoreFoodSkillOverrides(FoodSkillOverrideState state)
        {
            if (state == null || state.Restored)
            {
                return;
            }

            if (SkillVariableOverridesField == null)
            {
                state.Restored = true;
                return;
            }

            for (int i = 0; i < state.Snapshots.Count; i++)
            {
                SkillVariableSnapshot snapshot = state.Snapshots[i];
                if (snapshot != null && snapshot.Skill != null)
                {
                    SkillVariableOverridesField.SetValue(snapshot.Skill, snapshot.VariableOverrides);
                }
            }
            state.Restored = true;
        }

        private void MarkFoodStatusForStaminaRecovery(FoodSkillOverrideState state)
        {
            if (state == null
                || state.Hero == null
                || state.Hero.Statuses == null
                || state.RecoveryDuration <= NeutralTolerance)
            {
                return;
            }

            float staminaRate = PresetFoodStaminaRate();
            if (staminaRate <= NeutralTolerance)
            {
                return;
            }

            Status foodStatus = FindNewFoodHealthStatus(state);
            if (foodStatus == null || foodStatus.Skill == null)
            {
                if (!_reportedFoodStatusCaptureFailure)
                {
                    _reportedFoodStatusCaptureFailure = true;
                    Warn("Could not resolve the newly added native food status; added food stamina recovery was skipped.");
                }
                return;
            }

            foodStatus.Skill.OverrideVariable(FoodStaminaRateVariable, staminaRate);
            foodStatus.Skill.OverrideVariable(
                FoodRecoveryDurationVariable,
                state.RecoveryDuration);

            if (DiagnosticsEnabled())
            {
                LogDiagnostic(
                    "Food stamina status marked: item="
                    + (state.Item == null || state.Item.Template == null
                        ? "Unknown"
                        : state.Item.Template.GUID)
                    + ", status="
                    + foodStatus.Template.GUID
                    + ", rate="
                    + staminaRate.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", duration="
                    + state.RecoveryDuration.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static void CaptureActiveFoodStatuses(
            Hero hero,
            HashSet<Status> destination)
        {
            if (hero == null || hero.Statuses == null || destination == null)
            {
                return;
            }

            foreach (Status status in hero.Statuses.AllStatuses)
            {
                if (IsFoodHealthStatus(status))
                {
                    destination.Add(status);
                }
            }
        }

        private static Status FindNewFoodHealthStatus(FoodSkillOverrideState state)
        {
            if (state == null || state.Hero == null || state.Hero.Statuses == null)
            {
                return null;
            }

            foreach (Status status in state.Hero.Statuses.AllStatuses)
            {
                if (IsFoodHealthStatus(status)
                    && !state.ExistingFoodStatuses.Contains(status))
                {
                    return status;
                }
            }

            return null;
        }

        private static bool IsFoodHealthStatus(Status status)
        {
            return status != null
                && status.Template != null
                && string.Equals(
                    status.Template.GUID,
                    FoodRecoveryStatusGuid,
                    StringComparison.OrdinalIgnoreCase)
                && status.Skill != null
                && status.Skill.GetRichEnum("StatEnum") == AliveStatType.HealthRegen;
        }

        private static void RemoveLegacyFoodStaminaStatus(Hero hero)
        {
            if (hero == null || hero.Statuses == null)
            {
                return;
            }

            List<Status> matchingStatuses = new List<Status>();
            foreach (Status status in hero.Statuses.AllStatuses)
            {
                if (status != null
                    && status.SourceInfo != null
                    && string.Equals(
                        status.SourceInfo.SourceUniqueID,
                        LegacyFoodStaminaStatusSourceId,
                        StringComparison.Ordinal))
                {
                    matchingStatuses.Add(status);
                }
            }

            for (int i = 0; i < matchingStatuses.Count; i++)
            {
                hero.Statuses.RemoveStatus(matchingStatuses[i]);
            }
        }

        private void EnforceSingleFoodRecoveryStatus(Hero hero)
        {
            if (hero == null
                || hero.Statuses == null
                || hero.HealthRegen == null
                || !FoodRecoveryModifierIsEffective())
            {
                return;
            }

            List<Status> foodStatuses = new List<Status>();
            foreach (Status status in hero.Statuses.AllStatuses)
            {
                if (IsFoodHealthStatus(status))
                {
                    foodStatuses.Add(status);
                }
            }
            if (foodStatuses.Count <= 1)
            {
                return;
            }

            Status winner = foodStatuses[0];
            float winningHealing = RemainingFoodHealing(hero, winner);
            float winningTime = winner.TimeLeftSeconds ?? 0.0f;
            for (int i = 1; i < foodStatuses.Count; i++)
            {
                Status candidate = foodStatuses[i];
                float candidateHealing = RemainingFoodHealing(hero, candidate);
                float candidateTime = candidate.TimeLeftSeconds ?? 0.0f;
                if (candidateHealing > winningHealing + NeutralTolerance
                    || (Math.Abs(candidateHealing - winningHealing) <= NeutralTolerance
                        && candidateTime > winningTime))
                {
                    winner = candidate;
                    winningHealing = candidateHealing;
                    winningTime = candidateTime;
                }
            }

            for (int i = 0; i < foodStatuses.Count; i++)
            {
                if (!ReferenceEquals(foodStatuses[i], winner))
                {
                    hero.Statuses.RemoveStatus(foodStatuses[i]);
                }
            }

            if (DiagnosticsEnabled())
            {
                LogDiagnostic(
                    "Food recovery arbitration kept one status: remainingHealing="
                    + winningHealing.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", remainingSeconds="
                    + winningTime.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", removed="
                    + (foodStatuses.Count - 1).ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static float RemainingFoodHealing(Hero hero, Status status)
        {
            if (hero == null
                || hero.HealthRegen == null
                || status == null
                || status.Skill == null)
            {
                return 0.0f;
            }

            PassiveStatOverTime passive = status.Skill.TryGetElement<PassiveStatOverTime>();
            if (passive != null)
            {
                return Mathf.Max(0.0f, hero.HealthRegen.GetPrediction(passive));
            }

            float? rate = status.Skill.GetVariable("AddValue", hero);
            float timeLeft = status.TimeLeftSeconds ?? 0.0f;
            return Mathf.Max(0.0f, (rate ?? 0.0f) * timeLeft);
        }

        private bool HasActiveFoodStaminaRecovery(Hero hero)
        {
            if (hero == null
                || hero.Statuses == null
                || !FoodRecoveryModifierIsEffective())
            {
                return false;
            }

            foreach (Status status in hero.Statuses.AllStatuses)
            {
                if (!IsFoodHealthStatus(status))
                {
                    continue;
                }

                float? staminaRate = status.Skill.GetVariable(
                    FoodStaminaRateVariable,
                    hero);
                if (staminaRate.HasValue && staminaRate.Value > NeutralTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasActiveOverexertion(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            foreach (PreventStaminaRegenDuration prevention in
                hero.Elements<PreventStaminaRegenDuration>())
            {
                if (prevention != null
                    && prevention.BlockType == StaminaRegenBlockType.Overexertion)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyFoodOverexertionDuration(
            ICharacter character,
            StaminaRegenBlockType blockType,
            ref IDuration duration,
            ref IDuration depletedStatusDuration)
        {
            Hero hero = character as Hero;
            if (hero == null
                || blockType != StaminaRegenBlockType.Overexertion
                || !HasActiveFoodStaminaRecovery(hero))
            {
                return;
            }

            TimeDuration regenDuration = duration as TimeDuration;
            TimeDuration statusDuration = depletedStatusDuration as TimeDuration;
            if (regenDuration == null || statusDuration == null)
            {
                return;
            }

            duration = new TimeDuration(
                regenDuration.TimeLeft * FoodOverexertionDurationMultiplier,
                regenDuration.UnscaledTime);
            depletedStatusDuration = new TimeDuration(
                statusDuration.TimeLeft * FoodOverexertionDurationMultiplier,
                statusDuration.UnscaledTime);
        }

        private StaminaDepletedVignetteMode CurrentStaminaVignetteMode()
        {
            if (_enabled == null
                || !_enabled.Value
                || _staminaDepletedVignetteMode == null)
            {
                return StaminaDepletedVignetteMode.Native;
            }

            return _staminaDepletedVignetteMode.Value;
        }

        private void CacheStaminaVignette(VHeroStaminaUsedUpEffect view)
        {
            if (view == null)
            {
                return;
            }

            _staminaVignetteView = view;
            _staminaVignetteImage = StaminaVignetteImageField == null
                ? null
                : StaminaVignetteImageField.GetValue(view) as Image;
            if (StaminaVignetteFadeStrengthField != null)
            {
                object strength = StaminaVignetteFadeStrengthField.GetValue(view);
                if (strength is float && (float)strength > NeutralTolerance)
                {
                    _staminaVignetteStrength = (float)strength;
                }
            }
            if (_staminaVignetteStrength <= NeutralTolerance)
            {
                _staminaVignetteStrength = 1.0f;
            }
        }

        private static void KillNativeStaminaVignetteTween(VHeroStaminaUsedUpEffect view)
        {
            if (view == null || StaminaVignetteTweenField == null)
            {
                return;
            }

            Tween tween = StaminaVignetteTweenField.GetValue(view) as Tween;
            if (tween != null)
            {
                tween.Kill(false);
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static void SetStaminaDepletedPostProcess(float weight, bool instant)
        {
            SpecialPostProcessService service =
                World.Services.TryGet<SpecialPostProcessService>();
            if (service == null || service.VolumeStaminaUsedUp == null)
            {
                return;
            }

            if (instant)
            {
                service.VolumeStaminaUsedUp.SetWeightInstant(weight);
            }
            else
            {
                service.VolumeStaminaUsedUp.SetWeight(weight, 1.0f);
            }
        }

        private void BeginStaminaVignetteFade(float targetAlpha)
        {
            if (_staminaVignetteImage == null)
            {
                _staminaVignetteFadeActive = false;
                return;
            }

            _staminaVignetteFadeStartAlpha = _staminaVignetteImage.color.a;
            _staminaVignetteFadeTargetAlpha = Mathf.Clamp01(targetAlpha);
            _staminaVignetteFadeElapsed = 0.0f;
            _staminaVignetteFadeActive = !Mathf.Approximately(
                _staminaVignetteFadeStartAlpha,
                _staminaVignetteFadeTargetAlpha);
            if (!_staminaVignetteFadeActive)
            {
                SetImageAlpha(
                    _staminaVignetteImage,
                    _staminaVignetteFadeTargetAlpha);
            }
        }

        private void UpdateStaminaVignetteFade()
        {
            if (!_staminaVignetteFadeActive)
            {
                return;
            }
            if (_staminaVignetteImage == null)
            {
                _staminaVignetteFadeActive = false;
                return;
            }

            float duration = _staminaDepletedVignetteFadeSeconds == null
                ? 0.30f
                : Mathf.Max(0.05f, _staminaDepletedVignetteFadeSeconds.Value);
            _staminaVignetteFadeElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_staminaVignetteFadeElapsed / duration);
            float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, progress);
            SetImageAlpha(
                _staminaVignetteImage,
                Mathf.Lerp(
                    _staminaVignetteFadeStartAlpha,
                    _staminaVignetteFadeTargetAlpha,
                    easedProgress));
            if (progress >= 1.0f)
            {
                _staminaVignetteFadeActive = false;
            }
        }

        private float CaptureStaminaVignetteAlpha(VHeroStaminaUsedUpEffect view)
        {
            CacheStaminaVignette(view);
            return _staminaVignetteImage == null
                ? 0.0f
                : _staminaVignetteImage.color.a;
        }

        private void OnStaminaVignetteStarted(VHeroStaminaUsedUpEffect view)
        {
            bool wasActive = _staminaVignetteExhaustionActive;
            _staminaVignetteExhaustionActive = true;
            CacheStaminaVignette(view);

            StaminaDepletedVignetteMode mode = CurrentStaminaVignetteMode();
            if (mode == StaminaDepletedVignetteMode.Native)
            {
                _staminaVignetteFadeActive = false;
                if (_staminaVignetteImage != null)
                {
                    _staminaVignetteImage.enabled = true;
                }
                return;
            }
            if (wasActive)
            {
                if (mode == StaminaDepletedVignetteMode.Off)
                {
                    SetStaminaDepletedPostProcess(0.0f, true);
                }
                return;
            }

            KillNativeStaminaVignetteTween(view);
            _staminaVignetteFadeActive = false;
            if (_staminaVignetteImage == null)
            {
                return;
            }

            if (mode == StaminaDepletedVignetteMode.Off)
            {
                SetImageAlpha(_staminaVignetteImage, 0.0f);
                _staminaVignetteImage.enabled = false;
                SetStaminaDepletedPostProcess(0.0f, true);
                return;
            }

            _staminaVignetteImage.enabled = true;
            BeginStaminaVignetteFade(_staminaVignetteStrength);
        }

        private void OnStaminaVignetteStopped(
            VHeroStaminaUsedUpEffect view,
            float previousAlpha)
        {
            _staminaVignetteExhaustionActive = false;
            CacheStaminaVignette(view);

            StaminaDepletedVignetteMode mode = CurrentStaminaVignetteMode();
            if (mode == StaminaDepletedVignetteMode.Native)
            {
                _staminaVignetteFadeActive = false;
                if (_staminaVignetteImage != null)
                {
                    _staminaVignetteImage.enabled = true;
                }
                return;
            }

            KillNativeStaminaVignetteTween(view);
            if (_staminaVignetteImage == null)
            {
                return;
            }

            if (mode == StaminaDepletedVignetteMode.Off)
            {
                _staminaVignetteFadeActive = false;
                SetImageAlpha(_staminaVignetteImage, 0.0f);
                _staminaVignetteImage.enabled = false;
                return;
            }

            _staminaVignetteImage.enabled = true;
            SetImageAlpha(_staminaVignetteImage, previousAlpha);
            BeginStaminaVignetteFade(0.0f);
        }

        private void ApplyCurrentStaminaVignetteMode()
        {
            if (_staminaVignetteView == null || _staminaVignetteImage == null)
            {
                return;
            }

            StaminaDepletedVignetteMode mode = CurrentStaminaVignetteMode();
            if (mode == StaminaDepletedVignetteMode.Native)
            {
                _staminaVignetteFadeActive = false;
                _staminaVignetteImage.enabled = true;
                SetImageAlpha(
                    _staminaVignetteImage,
                    _staminaVignetteExhaustionActive
                        ? _staminaVignetteStrength
                        : 0.0f);
                SetStaminaDepletedPostProcess(
                    _staminaVignetteExhaustionActive ? 1.0f : 0.0f,
                    false);
                return;
            }

            KillNativeStaminaVignetteTween(_staminaVignetteView);
            if (mode == StaminaDepletedVignetteMode.Off)
            {
                _staminaVignetteFadeActive = false;
                SetImageAlpha(_staminaVignetteImage, 0.0f);
                _staminaVignetteImage.enabled = false;
                SetStaminaDepletedPostProcess(0.0f, true);
                return;
            }

            _staminaVignetteImage.enabled = true;
            SetStaminaDepletedPostProcess(
                _staminaVignetteExhaustionActive ? 1.0f : 0.0f,
                false);
            BeginStaminaVignetteFade(
                _staminaVignetteExhaustionActive
                    ? _staminaVignetteStrength
                    : 0.0f);
        }

        private void RestoreNativeStaminaVignettePresentation()
        {
            if (_staminaVignetteImage != null)
            {
                _staminaVignetteFadeActive = false;
                _staminaVignetteImage.enabled = true;
                SetImageAlpha(
                    _staminaVignetteImage,
                    _staminaVignetteExhaustionActive
                        ? _staminaVignetteStrength
                        : 0.0f);
            }
            SetStaminaDepletedPostProcess(
                _staminaVignetteExhaustionActive ? 1.0f : 0.0f,
                false);
        }

        private void RestoreFoodStaminaDirectly(Hero hero, float deltaTime)
        {
            if (hero == null
                || hero.Statuses == null
                || hero.Stamina == null
                || deltaTime <= 0.0f)
            {
                return;
            }

            if (!FoodRecoveryModifierIsEffective())
            {
                _foodStaminaTickElapsed = 0.0f;
                _foodStaminaWasSuspendedByOverexertion = false;
                return;
            }

            EnforceSingleFoodRecoveryStatus(hero);

            float staminaPerTick = 0.0f;
            foreach (Status status in hero.Statuses.AllStatuses)
            {
                if (!IsFoodHealthStatus(status))
                {
                    continue;
                }

                float? staminaRate = status.Skill.GetVariable(
                    FoodStaminaRateVariable,
                    hero);
                if (staminaRate.HasValue && staminaRate.Value > NeutralTolerance)
                {
                    staminaPerTick = Mathf.Max(staminaPerTick, staminaRate.Value);
                }
            }

            if (staminaPerTick <= NeutralTolerance)
            {
                _foodStaminaTickElapsed = 0.0f;
                _foodStaminaWasSuspendedByOverexertion = false;
                return;
            }

            if (HasActiveOverexertion(hero))
            {
                _foodStaminaTickElapsed = 0.0f;
                _foodStaminaWasSuspendedByOverexertion = true;
                return;
            }

            if (_foodStaminaWasSuspendedByOverexertion)
            {
                _foodStaminaWasSuspendedByOverexertion = false;
                _foodStaminaTickElapsed = FoodStaminaTickSeconds
                    - FoodStaminaPostOverexertionDelaySeconds;
            }

            if (hero.Stamina.IsMaxFloat)
            {
                _foodStaminaTickElapsed = 0.0f;
                return;
            }

            _foodStaminaTickElapsed += deltaTime;
            int ticks = Mathf.FloorToInt(_foodStaminaTickElapsed / FoodStaminaTickSeconds);
            if (ticks <= 0)
            {
                return;
            }

            _foodStaminaTickElapsed -= ticks * FoodStaminaTickSeconds;
            hero.Stamina.IncreaseBy(staminaPerTick * ticks);
            if (!_loggedFoodStaminaTickDiagnostic && DiagnosticsEnabled())
            {
                _loggedFoodStaminaTickDiagnostic = true;
                LogDiagnostic(
                    "Direct food stamina recovery ticked through the hero stat lock: rate="
                    + staminaPerTick.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", ticks="
                    + ticks.ToString(CultureInfo.InvariantCulture)
                    + ", interval="
                    + FoodStaminaTickSeconds.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private bool ShouldApplyNativePotionPoisoningBuildup(
            CharacterStatuses statuses,
            StatusTemplate statusTemplate,
            StatusSourceInfo sourceInfo)
        {
            if (statuses == null
                || !ReferenceEquals(statuses.ParentModel, Hero.Current)
                || statusTemplate == null
                || !string.Equals(
                    statusTemplate.GUID,
                    PotionPoisoningStatusGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (_applyingCompletedPotionPoisoningBuildup
                || !PotionOverdrinkingModifierIsEnabled())
            {
                return true;
            }

            ConsumableRecoveryPatchState activeState = _activeConsumableRecoveryState;
            if (activeState == null
                || !activeState.IsPotionConsumption
                || !ReferenceEquals(activeState.Hero, Hero.Current))
            {
                return true;
            }

            LogDifficultyDiagnostic(
                "SuppressedNativePotionPoisoning:" + activeState.PotionTemplateGuid,
                NativePotionPoisoningBuildup,
                0.0f,
                -NativePotionPoisoningBuildup);
            return false;
        }

        private void ApplyPotionPoisoningPenalty(BuildupStatus status)
        {
            Hero hero = Hero.Current;
            if (!PotionOverdrinkingModifierIsEnabled()
                || !IsPotionPoisoningStatus(status)
                || status.Skill == null
                || hero == null
                || !ReferenceEquals(status.Character, hero)
                || _pendingPotionPoisoningPenaltyBuckets == PotionPoisoningBucket.None)
            {
                return;
            }

            List<PassiveStatModifier> modifiers =
                new List<PassiveStatModifier>();
            foreach (PassiveStatModifier modifier in status.Skill.Elements<PassiveStatModifier>())
            {
                modifiers.Add(modifier);
            }

            foreach (PassiveStatModifier modifier in modifiers)
            {
                if (ReferenceEquals(modifier.Stat, hero.CharacterStats.MaxMana)
                    && Mathf.Abs(modifier.Value + 20.0f) <= NeutralTolerance)
                {
                    modifier.Discard();
                }
            }

            _activePotionPoisoningDrainStatus = status;
            _activePotionPoisoningDrainHero = hero;
            _activePotionPoisoningDrainBuckets = _pendingPotionPoisoningPenaltyBuckets;
            float drainFraction = (_activePotionPoisoningDrainBuckets & PotionPoisoningBucket.Utility) != 0
                ? UtilityPotionPoisoningDrainFraction
                : ResourcePotionPoisoningDrainFraction;
            _potionPoisoningHealthDrainRemaining =
                (_activePotionPoisoningDrainBuckets & (PotionPoisoningBucket.Health | PotionPoisoningBucket.Utility)) != 0
                    ? hero.MaxHealth.ModifiedValue * drainFraction
                    : 0.0f;
            _potionPoisoningManaDrainRemaining =
                (_activePotionPoisoningDrainBuckets & (PotionPoisoningBucket.Mana | PotionPoisoningBucket.Utility)) != 0
                    ? hero.MaxMana.ModifiedValue * drainFraction
                    : 0.0f;
            _potionPoisoningStaminaDrainRemaining =
                (_activePotionPoisoningDrainBuckets & (PotionPoisoningBucket.Stamina | PotionPoisoningBucket.Utility)) != 0
                    ? hero.MaxStamina.ModifiedValue * drainFraction
                    : 0.0f;
        }

        private void ApplyPotionPoisoningDrain(BuildupStatus status, float previousProgress)
        {
            Hero hero = _activePotionPoisoningDrainHero;
            if (!PotionOverdrinkingModifierIsEnabled()
                || status == null
                || !ReferenceEquals(status, _activePotionPoisoningDrainStatus)
                || hero == null
                || !ReferenceEquals(hero, Hero.Current))
            {
                return;
            }

            float progressLost = Mathf.Clamp01(previousProgress)
                - Mathf.Clamp01(status.BuildupProgress);
            if (progressLost <= NeutralTolerance)
            {
                return;
            }
            float remainingProgress = Mathf.Max(
                NeutralTolerance,
                Mathf.Clamp01(previousProgress));
            float drainFraction = Mathf.Clamp01(progressLost / remainingProgress);

            ApplyPotionPoisoningStatDrain(
                hero.Health,
                ref _potionPoisoningHealthDrainRemaining,
                drainFraction,
                1.0f);
            ApplyPotionPoisoningStatDrain(
                hero.Mana,
                ref _potionPoisoningManaDrainRemaining,
                drainFraction,
                0.0f);
            ApplyPotionPoisoningStatDrain(
                hero.Stamina,
                ref _potionPoisoningStaminaDrainRemaining,
                drainFraction,
                0.0f);
        }

        private static void ApplyPotionPoisoningStatDrain(
            LimitedStat stat,
            ref float remainingDrain,
            float drainFraction,
            float minimumValue)
        {
            if (stat == null || remainingDrain <= NeutralTolerance)
            {
                return;
            }

            float plannedDrain = remainingDrain * drainFraction;
            float available = Mathf.Max(0.0f, stat.ModifiedValue - minimumValue);
            stat.DecreaseBy(Mathf.Min(plannedDrain, available));
            remainingDrain = Mathf.Max(0.0f, remainingDrain - plannedDrain);
        }

        private string ReplacePotionPoisoningDescription(
            Status status,
            string description)
        {
            if (!PotionOverdrinkingModifierIsEnabled()
                || !IsPotionPoisoningStatus(status)
                || !(status is BuildupStatus)
                || !((BuildupStatus)status).Active
                || !ReferenceEquals(status, _activePotionPoisoningDrainStatus))
            {
                return description;
            }

            if ((_activePotionPoisoningDrainBuckets & PotionPoisoningBucket.Utility) != 0)
            {
                return "Drains 15% of maximum HP, MP, and SP over this status.";
            }

            List<string> resources = new List<string>();
            if ((_activePotionPoisoningDrainBuckets & PotionPoisoningBucket.Health) != 0)
            {
                resources.Add("HP");
            }
            if ((_activePotionPoisoningDrainBuckets & PotionPoisoningBucket.Mana) != 0)
            {
                resources.Add("MP");
            }
            if ((_activePotionPoisoningDrainBuckets & PotionPoisoningBucket.Stamina) != 0)
            {
                resources.Add("SP");
            }
            if (resources.Count == 0)
            {
                return description;
            }
            string resourceText = resources.Count == 1
                ? resources[0]
                : resources.Count == 2
                    ? resources[0] + " and " + resources[1]
                    : resources[0] + ", " + resources[1] + ", and " + resources[2];
            return "Drains 30% of maximum " + resourceText + " over this status.";
        }

        private string AppendFoodStaminaDescription(
            FoodSkillOverrideState state,
            string description)
        {
            if (state == null || state.RecoveryDuration <= NeutralTolerance)
            {
                return description;
            }

            float staminaRate = PresetFoodStaminaRate();
            if (staminaRate <= NeutralTolerance)
            {
                return description;
            }

            string staminaLine = BuildFoodStaminaLine(
                staminaRate,
                state.RecoveryDuration);
            if (!string.IsNullOrEmpty(description)
                && description.IndexOf(staminaLine, StringComparison.Ordinal) >= 0)
            {
                return description;
            }

            return string.IsNullOrWhiteSpace(description)
                ? staminaLine
                : description.TrimEnd() + Environment.NewLine + staminaLine;
        }

        private string AppendActiveFoodStaminaDescription(
            Status status,
            string description)
        {
            if (status == null || status.Skill == null)
            {
                return description;
            }

            float? staminaRate = status.Skill.GetVariable(
                FoodStaminaRateVariable,
                status.Character);
            float? duration = status.Skill.GetVariable(
                FoodRecoveryDurationVariable,
                status.Character);
            if (!staminaRate.HasValue
                || staminaRate.Value <= NeutralTolerance
                || !duration.HasValue
                || duration.Value <= NeutralTolerance)
            {
                return description;
            }

            string staminaLine = BuildFoodStaminaLine(
                staminaRate.Value,
                duration.Value);
            if (!string.IsNullOrEmpty(description)
                && description.IndexOf(staminaLine, StringComparison.Ordinal) >= 0)
            {
                return description;
            }

            return string.IsNullOrWhiteSpace(description)
                ? staminaLine
                : description.TrimEnd() + Environment.NewLine + staminaLine;
        }

        private static string BuildFoodStaminaLine(float staminaRate, float durationSeconds)
        {
            string duration = durationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            return "Restores 1 stamina per second for " + duration
                + "s. While active, halves Stamina Depleted duration; stamina ticks pause during it, and the first point follows 0.1s later.";
        }

        private ValueTuple<string, Color> BuildBetterUiFoodOverlay(
            FoodSkillOverrideState state,
            ValueTuple<string, Color> result)
        {
            if (state == null || state.RecoveryDuration <= NeutralTolerance)
            {
                return result;
            }

            float staminaRate = PresetFoodStaminaRate();
            if (staminaRate <= NeutralTolerance)
            {
                return result;
            }

            int staminaTotal = Mathf.RoundToInt(staminaRate * state.RecoveryDuration);
            int duration = Mathf.RoundToInt(state.RecoveryDuration);
            string staminaText = "+" + staminaTotal + "/" + duration + "s";
            if (string.IsNullOrEmpty(result.Item1))
            {
                return new ValueTuple<string, Color>(staminaText, new Color(0.4f, 1.0f, 0.4f, 1.0f));
            }

            string existingText = result.Item2 == Color.white
                ? result.Item1
                : "<color=#" + ColorUtility.ToHtmlStringRGB(result.Item2) + ">" + result.Item1 + "</color>";
            string combined = existingText
                + " <color=#66FF66>"
                + staminaText
                + "</color>";
            if (_betterUiLastEffectCountGetter != null
                && _betterUiLastEffectCountSetter != null)
            {
                object countValue = _betterUiLastEffectCountGetter.Invoke(null, null);
                int effectCount = countValue is int ? (int)countValue : 1;
                _betterUiLastEffectCountSetter.Invoke(
                    null,
                    new object[] { Math.Max(1, effectCount) + 1 });
            }
            return new ValueTuple<string, Color>(combined, Color.white);
        }

        private float HostileArrowVelocityMultiplier(VGUtils.ShootParams shootParams)
        {
            if (!DifficultyModifierIsEnabled(_modifyHostileArrowVelocity)
                || !IsHostileNpcArrow(shootParams))
            {
                return 1.0f;
            }

            return PresetArrowVelocityMultiplier();
        }

        private void ApplyHostileArcherAimScatter(
            ref CombatBehaviourUtils.FireProjectileParams fireParams,
            VGUtils.ShootParams shootParams)
        {
            if (!DifficultyModifiersAreEnabled()
                || _hostileArcherAimScatter == null
                || !IsHostileNpcArrow(shootParams))
            {
                return;
            }

            float scatter = Mathf.Clamp(_hostileArcherAimScatter.Value, 0.0f, 2.0f);
            float before = fireParams.inaccuracy;
            if (scatter <= before + NeutralTolerance)
            {
                return;
            }

            fireParams.inaccuracy = Mathf.Max(before, scatter);
            LogDifficultyDiagnostic("HostileArcherAimScatter", before, fireParams.inaccuracy, scatter);
        }

        private static bool IsHostileNpcArrow(VGUtils.ShootParams shootParams)
        {
            return shootParams.shooter is NpcElement
                && Hero.Current != null
                && ReferenceEquals(shootParams.projectileSlotType, EquipmentSlotType.Quiver)
                && shootParams.shooter.IsHostileTo(Hero.Current);
        }

        private void ApplyEnemyAttackSlots(ref int value)
        {
            if (!DifficultyModifierIsEnabled(_modifyEnemyAttackSlots))
            {
                return;
            }

            int bonus = PresetAttackSlotBonus();
            if (bonus <= 0)
            {
                return;
            }

            int before = value;
            int cap = _enemyAttackSlotCap == null ? 6 : Mathf.Clamp(_enemyAttackSlotCap.Value, 1, 12);
            int maximumAfterSteelAndBone = Math.Max(before, cap);
            long increased = (long)before + bonus;
            value = (int)Math.Min(increased, maximumAfterSteelAndBone);
            LogDifficultyDiagnostic("EnemyAttackSlots", before, value, bonus);
        }

        private void ApplyEnemyAttackRecovery(ref float value)
        {
            if (!DifficultyModifierIsEnabled(_modifyEnemyAttackRecovery))
            {
                return;
            }

            float multiplier = PresetReductionMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = value;
            value = Mathf.Max(0.0f, value * multiplier);
            LogDifficultyDiagnostic("EnemyAttackRecovery", before, value, multiplier);
        }

        private void ApplyKillExperience(ref int value)
        {
            if (!DifficultyModifierIsEnabled(_modifyKillExperience) || value <= 0)
            {
                return;
            }

            float multiplier = PresetPlayerPressureReductionMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            int before = value;
            value = Math.Max(1, Mathf.FloorToInt(value * multiplier));
            LogDifficultyDiagnostic("KillExperience", before, value, multiplier);
        }

        private void ApplyQuestExperience(ref float value)
        {
            if (!DifficultyModifierIsEnabled(_modifyQuestExperience) || value <= 0.0f)
            {
                return;
            }

            float multiplier = PresetPlayerPressureReductionMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = value;
            value *= multiplier;
            LogDifficultyDiagnostic("QuestExperience", before, value, multiplier);
        }

        private void ApplyProficiencyExperience(ref float value)
        {
            if (!DifficultyModifierIsEnabled(_modifyProficiencyExperience) || value <= 0.0f)
            {
                return;
            }

            float multiplier = PresetPlayerPressureReductionMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = value;
            value *= multiplier;
            LogDifficultyDiagnostic("ProficiencyExperience", before, value, multiplier);
        }

        private void ApplyPlayerPoiseDamage(DamageOutcome damageOutcome, ref PoisePatchState state)
        {
            state = null;
            Damage damage = damageOutcome.Damage;
            NpcElement target = damageOutcome.TargetPure as NpcElement;
            if (damage == null || target == null || !IsHeroDamageSource(damage, Hero.Current))
            {
                return;
            }

            float multiplier = 1.0f;
            if (DifficultyModifierIsEnabled(_modifyPlayerPoiseDamageDealt))
            {
                multiplier *= PresetReductionMultiplier();
            }

            float materialImpactMultiplier;
            if (TryGetMaterialImpactMultiplier(damageOutcome, out materialImpactMultiplier))
            {
                multiplier *= materialImpactMultiplier;
            }

            float tenacity;
            if (TryGetProgressiveTenacity(damage, target, true, out tenacity))
            {
                multiplier *= 1.0f - tenacity;
            }

            bool suppressRoutineResistedFlinch = ShouldSuppressRoutineResistedFlinch(damageOutcome);
            if (!suppressRoutineResistedFlinch
                && (damage.PoiseDamage <= 0.0f || ApproximatelyNeutral(multiplier)))
            {
                return;
            }

            state = new PoisePatchState
            {
                Damage = damage,
                OriginalPoiseDamage = damage.PoiseDamage,
                PreviousSuppressRoutineResistedFlinch = _suppressRoutineResistedFlinch
            };
            _suppressRoutineResistedFlinch = _suppressRoutineResistedFlinch || suppressRoutineResistedFlinch;

            if (damage.PoiseDamage > 0.0f && !ApproximatelyNeutral(multiplier))
            {
                DamageParameters parameters = damage.Parameters;
                parameters.PoiseDamage = Mathf.Max(0.0f, state.OriginalPoiseDamage * multiplier);
                damage.Parameters = parameters;
                state.PoiseChanged = true;
                LogDifficultyDiagnostic("PlayerPoiseDamageDealt", state.OriginalPoiseDamage, parameters.PoiseDamage, multiplier);
            }
        }

        private void RestorePlayerPoiseDamage(PoisePatchState state)
        {
            if (state == null)
            {
                return;
            }

            _suppressRoutineResistedFlinch = state.PreviousSuppressRoutineResistedFlinch;
            if (state.Damage != null && state.PoiseChanged)
            {
                DamageParameters parameters = state.Damage.Parameters;
                parameters.PoiseDamage = state.OriginalPoiseDamage;
                state.Damage.Parameters = parameters;
            }
        }

        private bool TryGetMaterialImpactMultiplier(
            DamageOutcome damageOutcome,
            out float impactMultiplier)
        {
            impactMultiplier = 1.0f;
            if (!MaterialImpactRulesEnabled())
            {
                return false;
            }

            Damage damage = damageOutcome.Damage;
            if (damage == null
                || damage.IsDamageOverTime)
            {
                return false;
            }

            float effectivenessMultiplier;
            if (!TryGetDamageEffectivenessMultiplier(damage, out effectivenessMultiplier))
            {
                return false;
            }

            effectivenessMultiplier = Mathf.Clamp(effectivenessMultiplier, 0.0f, 1.0f);
            if (effectivenessMultiplier >= 0.9999f)
            {
                return false;
            }

            impactMultiplier = Mathf.Clamp(
                1.0f + ((effectivenessMultiplier - 1.0f) * MaterialImpactResistanceInheritance),
                1.0f - MaterialImpactResistanceInheritance,
                1.0f);
            return !ApproximatelyNeutral(impactMultiplier);
        }

        private bool ShouldSuppressRoutineResistedFlinch(DamageOutcome damageOutcome)
        {
            if (!MaterialImpactRulesEnabled())
            {
                return false;
            }

            Damage damage = damageOutcome.Damage;
            if (damage == null
                || damage.IsDamageOverTime)
            {
                return false;
            }

            float effectivenessMultiplier;
            return TryGetDamageEffectivenessMultiplier(damage, out effectivenessMultiplier)
                && effectivenessMultiplier <= StrongResistanceFlinchThreshold;
        }

        private void ApplyPlayerForceDamage(DamageOutcome damageOutcome, ref ForcePatchState state)
        {
            state = null;
            Damage damage = damageOutcome.Damage;
            NpcElement target = damageOutcome.TargetPure as NpcElement;
            if (damage == null
                || damage.ForceDamage <= 0.0f
                || target == null
                || !IsHeroDamageSource(damage, Hero.Current))
            {
                return;
            }

            float multiplier = 1.0f;
            float materialImpactMultiplier;
            if (TryGetMaterialImpactMultiplier(damageOutcome, out materialImpactMultiplier))
            {
                multiplier *= materialImpactMultiplier;
            }

            float tenacity;
            if (TryGetProgressiveTenacity(damage, target, true, out tenacity))
            {
                multiplier *= 1.0f - tenacity;
            }

            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            DamageParameters parameters = damage.Parameters;
            float original = parameters.ForceDamage;
            parameters.ForceDamage = Mathf.Max(0.0f, original * multiplier);
            damage.Parameters = parameters;
            state = new ForcePatchState
            {
                Damage = damage,
                OriginalForceDamage = original
            };
            LogDifficultyDiagnostic("PlayerForceDamage", original, parameters.ForceDamage, multiplier);
        }

        private void RestorePlayerForceDamage(ForcePatchState state)
        {
            if (state == null || state.Damage == null)
            {
                return;
            }

            DamageParameters parameters = state.Damage.Parameters;
            parameters.ForceDamage = state.OriginalForceDamage;
            state.Damage.Parameters = parameters;
        }

        private void ApplyProgressiveTenacityHealthDamage(
            object healthElement,
            Damage damage,
            ref float damageModifier)
        {
            if (damage == null
                || damage.IsDamageOverTime
                || damage.Type == DamageType.Interact
                || (damage.Type != DamageType.PhysicalHitSource
                    && damage.Type != DamageType.MagicalHitSource)
                || !IsHeroDamageSource(damage, Hero.Current))
            {
                return;
            }

            NpcElement target = ResolveDamageTargetOwner(healthElement, damage) as NpcElement;
            float tenacity;
            if (!TryGetProgressiveTenacity(damage, target, true, out tenacity))
            {
                return;
            }

            float multiplier = 1.0f - (tenacity * 0.50f);
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = damageModifier;
            damageModifier *= multiplier;
            LogDifficultyDiagnostic("ProgressiveTenacityHealthDamage", before, damageModifier, multiplier);
        }

        private void ApplyProgressiveTenacityStaminaDamage(HealthElement healthElement, Damage damage)
        {
            if (damage == null
                || damage.IsDamageOverTime
                || (damage.Type != DamageType.PhysicalHitSource
                    && damage.Type != DamageType.MagicalHitSource)
                || damage.StaminaDamageAmount <= 0.0f)
            {
                return;
            }

            NpcElement target = healthElement == null ? null : healthElement.ParentModel as NpcElement;
            float tenacity;
            if (!TryGetProgressiveTenacity(damage, target, true, out tenacity))
            {
                return;
            }

            float before = damage.StaminaDamageAmount;
            float multiplier = 1.0f - tenacity;
            damage.StaminaDamageAmount = Mathf.Max(0.0f, before * multiplier);
            LogDifficultyDiagnostic("ProgressiveTenacityStaminaDamage", before, damage.StaminaDamageAmount, multiplier);
        }

        private bool TryGetProgressiveTenacity(
            Damage damage,
            NpcElement target,
            bool requireHeroDamageSource,
            out float tenacity)
        {
            tenacity = 0.0f;
            Hero hero = Hero.Current;
            if (!ProgressiveTenacityEnabled()
                || hero == null
                || target == null
                || (damage != null && damage.IsDamageOverTime)
                || target.HasBeenDiscarded
                || !target.IsAlive
                || target.IsSummonOrAlly
                || target.Template == null
                || !WithFactionUtils.IsHostileToHero(target)
                || (requireHeroDamageSource && !IsHeroDamageSource(damage, hero)))
            {
                return false;
            }

            float cap = ProgressiveTenacityCap(target.Template.NpcType);
            if (cap <= 0.0f)
            {
                return false;
            }

            float progress = Mathf.Clamp01(
                (hero.Level.ModifiedValue - ProgressiveTenacityStartLevel)
                / (ProgressiveTenacityFullLevel - ProgressiveTenacityStartLevel));
            tenacity = cap * progress;
            if (tenacity <= NeutralTolerance)
            {
                return false;
            }

            if (damage != null && HasConfirmedTenacityWeakness(damage))
            {
                tenacity *= 0.50f;
            }

            return true;
        }

        private bool HasConfirmedTenacityWeakness(Damage damage)
        {
            float effectivenessMultiplier;
            if (TryGetDamageEffectivenessMultiplier(damage, out effectivenessMultiplier)
                && effectivenessMultiplier > 1.0001f)
            {
                return true;
            }

            float nativeMultiplier;
            return TryGetNativeDamageEffectivenessMultiplier(damage, out nativeMultiplier)
                && nativeMultiplier > 1.0001f;
        }

        private static bool TryGetNativeDamageEffectivenessMultiplier(
            Damage damage,
            out float multiplier)
        {
            multiplier = 1.0f;
            if (damage == null
                || damage.DamageTypeData == null
                || damage.DamageTypeData.Parts == null
                || damage.DamageReceivedMultiplierData == null)
            {
                return false;
            }

            float totalWeight = 0.0f;
            float weightedMultiplier = 0.0f;
            var partEnumerator = damage.DamageTypeData.Parts.GetEnumerator();
            try
            {
                while (partEnumerator.MoveNext())
                {
                    DamageTypeDataPart part = partEnumerator.Current;
                    float weight = Math.Max(0.0f, part.PercentageAsFloat);
                    if (weight <= 0.0001f)
                    {
                        continue;
                    }

                    float nativeMultiplier = damage.DamageReceivedMultiplierData.GetMultiplierForSubtype(part.SubType);
                    if (float.IsNaN(nativeMultiplier)
                        || float.IsInfinity(nativeMultiplier)
                        || nativeMultiplier < 0.0f)
                    {
                        return false;
                    }

                    totalWeight += weight;
                    weightedMultiplier += weight * nativeMultiplier;
                }
            }
            finally
            {
                partEnumerator.Dispose();
            }

            if (totalWeight <= 0.0001f)
            {
                return false;
            }

            multiplier = weightedMultiplier / totalWeight;
            return true;
        }

        private bool ProgressiveTenacityEnabled()
        {
            return _enabled != null
                && _enabled.Value
                && _progressiveTenacityEnabled != null
                && _progressiveTenacityEnabled.Value;
        }

        private static float ProgressiveTenacityCap(NpcType npcType)
        {
            switch (npcType)
            {
                case NpcType.Trash:
                    return 0.10f;
                case NpcType.Normal:
                    return 0.15f;
                case NpcType.Elite:
                    return 0.25f;
                case NpcType.MiniBoss:
                    return 0.30f;
                case NpcType.Boss:
                    return 0.40f;
                case NpcType.Critter:
                case NpcType.HeroSummon:
                default:
                    return 0.0f;
            }
        }

        private void ApplyProgressiveTenacityParry(
            HeroParry parry,
            HookResult<HealthElement, Damage> hook,
            ref ParryStaminaPatchState state)
        {
            state = null;
            Damage damage = hook.Value;
            NpcElement target = damage == null ? null : damage.DamageDealerPure as NpcElement;
            float tenacity;
            if (parry == null
                || !TryGetProgressiveTenacity(null, target, false, out tenacity)
                || Hero.Current == null
                || Hero.Current.HeroStats == null
                || Hero.Current.HeroStats.ParryStaminaDamageMultiplier == null)
            {
                return;
            }

            state = new ParryStaminaPatchState
            {
                Tweak = new ProgressiveTenacityParryTweak(
                    Hero.Current.HeroStats.ParryStaminaDamageMultiplier,
                    1.0f - tenacity,
                    Hero.Current)
            };
            LogDifficultyDiagnostic("ProgressiveTenacityParryStaminaDamage", 1.0f, 1.0f - tenacity, 1.0f - tenacity);
        }

        private static void RestoreProgressiveTenacityParry(ParryStaminaPatchState state)
        {
            if (state != null && state.Tweak != null)
            {
                state.Tweak.Discard();
            }
        }

        private void ReapplyDifficultyStatTweaks()
        {
            if (!_resourceStatsPatchAvailable)
            {
                RemoveDifficultyStatTweaks(_difficultyTweakHero);
                _difficultyTweakHero = null;
                return;
            }

            try
            {
                Hero hero = Hero.Current;
                if (hero == null || hero.HasBeenDiscarded)
                {
                    _difficultyTweakHero = null;
                    return;
                }

                _difficultyTweakHero = hero;
                ApplyDifficultyStatTweaks(hero);
            }
            catch (Exception ex)
            {
                Warn("Could not apply player stat difficulty modifiers: " + ex.GetBaseException().Message);
            }
        }

        private void ApplyDifficultyStatTweaks(Hero hero)
        {
            if (hero == null || hero.HasBeenDiscarded)
            {
                return;
            }

            CharacterStats stats = hero.CharacterStats;
            if (stats == null)
            {
                return;
            }

            float staminaMultiplier = DifficultyModifierIsEnabled(_modifyStaminaUsage)
                ? PresetCostMultiplier()
                : 1.0f;
            float manaMultiplier = DifficultyModifierIsEnabled(_modifyManaUsage)
                ? PresetCostMultiplier()
                : 1.0f;
            ApplyDifficultyStatTweak(
                hero,
                DifficultyStatTarget.StaminaUsage,
                stats.StaminaUsageMultiplier,
                staminaMultiplier);
            ApplyDifficultyStatTweak(
                hero,
                DifficultyStatTarget.ManaUsage,
                stats.ManaUsageMultiplier,
                manaMultiplier);

            float armorPenaltyMultiplier = DifficultyModifierIsEnabled(_modifyArmorWeightPenalties)
                ? PresetCostMultiplier()
                : 1.0f;
            ApplyDifficultyStatTweak(
                hero,
                DifficultyStatTarget.ArmorPenalty,
                hero.HeroStats.ArmorPenaltyMultiplier,
                armorPenaltyMultiplier);

            ArmorWeight armorWeight = hero.TryGetElement<ArmorWeight>();
            bool lightArmor = armorWeight != null && armorWeight.ArmorWeightType == ItemWeight.Light;
            float movementMultiplier = DifficultyModifierIsEnabled(_modifyLightArmorMobility) && lightArmor
                ? PresetLightArmorMovementMultiplier()
                : 1.0f;
            ApplyDifficultyStatTweak(
                hero,
                DifficultyStatTarget.LightArmorMovement,
                stats.MovementSpeedMultiplier,
                movementMultiplier);
        }

        private void ApplyDifficultyStatTweak(
            Hero hero,
            DifficultyStatTarget target,
            Stat stat,
            float multiplier)
        {
            List<DifficultyStatTweak> matchingTweaks = new List<DifficultyStatTweak>();
            foreach (DifficultyStatTweak tweak in hero.Elements<DifficultyStatTweak>())
            {
                if (tweak.Target == target)
                {
                    matchingTweaks.Add(tweak);
                }
            }

            DifficultyStatTweak current = matchingTweaks.Count > 0 ? matchingTweaks[0] : null;
            for (int i = 1; i < matchingTweaks.Count; i++)
            {
                matchingTweaks[i].Discard();
            }

            if (stat == null || ApproximatelyNeutral(multiplier))
            {
                if (current != null)
                {
                    current.Discard();
                }
                return;
            }

            if (current == null)
            {
                hero.AddElement(new DifficultyStatTweak(target, stat, multiplier));
                LogDifficultyDiagnostic(target.ToString(), 1.0f, multiplier, multiplier);
                return;
            }

            if (Math.Abs(current.Modifier - multiplier) > NeutralTolerance)
            {
                float before = current.Modifier;
                current.SetModifier(multiplier);
                LogDifficultyDiagnostic(target.ToString(), before, multiplier, multiplier);
            }
        }

        private void RemoveDifficultyStatTweaks(Hero hero)
        {
            if (hero == null || hero.HasBeenDiscarded)
            {
                return;
            }

            List<DifficultyStatTweak> tweaks = new List<DifficultyStatTweak>();
            foreach (DifficultyStatTweak tweak in hero.Elements<DifficultyStatTweak>())
            {
                tweaks.Add(tweak);
            }

            for (int i = 0; i < tweaks.Count; i++)
            {
                tweaks[i].Discard();
            }
        }

        private void RefreshEnemyRuntimeTweaks()
        {
            try
            {
                foreach (NpcElement npc in World.All<NpcElement>())
                {
                    ApplyEnemySightRangeTweak(npc);
                    ApplyEnemyMovementSpeedTweak(npc);
                }
                _reportedEnemyRuntimeRefreshFailure = false;
            }
            catch (Exception ex)
            {
                if (!_reportedEnemyRuntimeRefreshFailure)
                {
                    _reportedEnemyRuntimeRefreshFailure = true;
                    Warn("Could not refresh enemy runtime modifiers: " + ex.GetBaseException().Message);
                }
            }
        }

        private void ApplyEnemySightRangeTweak(NpcElement npc)
        {
            if (npc == null || npc.HasBeenDiscarded)
            {
                return;
            }

            EnemySightRangeTweak current = null;
            List<EnemySightRangeTweak> duplicateTweaks = null;
            foreach (EnemySightRangeTweak tweak in npc.Elements<EnemySightRangeTweak>())
            {
                if (current == null)
                {
                    current = tweak;
                }
                else
                {
                    if (duplicateTweaks == null)
                    {
                        duplicateTweaks = new List<EnemySightRangeTweak>();
                    }
                    duplicateTweaks.Add(tweak);
                }
            }

            if (duplicateTweaks != null)
            {
                for (int i = 0; i < duplicateTweaks.Count; i++)
                {
                    duplicateTweaks[i].Discard();
                }
            }

            if (!EnemySightRangeTargetIsEligible(npc))
            {
                if (current != null)
                {
                    current.Discard();
                }
                return;
            }

            float multiplier = PresetEnemySightRangeMultiplier();
            Stat sightLength = npc.NpcStats == null ? null : npc.NpcStats.SightLengthMultiplier;
            if (sightLength == null)
            {
                if (current != null)
                {
                    current.Discard();
                }
                return;
            }

            if (current == null)
            {
                npc.AddElement(new EnemySightRangeTweak(sightLength, multiplier));
                LogDifficultyDiagnostic("EnemySightRange", 1.0f, multiplier, multiplier);
                return;
            }

            if (Math.Abs(current.Modifier - multiplier) > NeutralTolerance)
            {
                float before = current.Modifier;
                current.SetModifier(multiplier);
                LogDifficultyDiagnostic("EnemySightRange", before, multiplier, multiplier);
            }
        }

        private bool EnemySightRangeTargetIsEligible(NpcElement npc)
        {
            return DifficultyModifierIsEnabled(_modifyEnemySightRange)
                && Hero.Current != null
                && npc.IsAlive
                && !npc.IsSummonOrAlly
                && npc.NpcAI != null
                && npc.NpcAI.Working
                && WithFactionUtils.IsHostileToHero(npc);
        }

        private void ApplyEnemyMovementSpeedTweak(NpcElement npc)
        {
            if (npc == null || npc.HasBeenDiscarded)
            {
                return;
            }

            EnemyMovementSpeedTweak current = null;
            List<EnemyMovementSpeedTweak> duplicateTweaks = null;
            foreach (EnemyMovementSpeedTweak tweak in npc.Elements<EnemyMovementSpeedTweak>())
            {
                if (current == null)
                {
                    current = tweak;
                }
                else
                {
                    if (duplicateTweaks == null)
                    {
                        duplicateTweaks = new List<EnemyMovementSpeedTweak>();
                    }
                    duplicateTweaks.Add(tweak);
                }
            }

            if (duplicateTweaks != null)
            {
                for (int i = 0; i < duplicateTweaks.Count; i++)
                {
                    duplicateTweaks[i].Discard();
                }
            }

            float multiplier = EnemyMovementSpeedTargetIsEligible(npc)
                ? PresetEnemyMovementSpeedMultiplier(npc)
                : 1.0f;
            Stat movementSpeed = npc.CharacterStats == null
                ? null
                : npc.CharacterStats.MovementSpeedMultiplier;
            if (movementSpeed == null || ApproximatelyNeutral(multiplier))
            {
                if (current != null)
                {
                    current.Discard();
                }
                return;
            }

            if (current == null)
            {
                npc.AddElement(new EnemyMovementSpeedTweak(movementSpeed, multiplier));
                LogDifficultyDiagnostic("EnemyMovementSpeed", 1.0f, multiplier, multiplier);
                return;
            }

            if (Math.Abs(current.Modifier - multiplier) > NeutralTolerance)
            {
                float before = current.Modifier;
                current.SetModifier(multiplier);
                LogDifficultyDiagnostic("EnemyMovementSpeed", before, multiplier, multiplier);
            }
        }

        private bool EnemyMovementSpeedTargetIsEligible(NpcElement npc)
        {
            return DifficultyModifierIsEnabled(_modifyEnemyMovementSpeed)
                && Hero.Current != null
                && npc.IsAlive
                && !npc.IsSummonOrAlly
                && npc.NpcAI != null
                && npc.NpcAI.Working
                && npc.IsInCombat()
                && WithFactionUtils.IsHostileToHero(npc);
        }

        private void RemoveAllEnemySightRangeTweaks()
        {
            try
            {
                foreach (NpcElement npc in World.All<NpcElement>())
                {
                    if (npc == null || npc.HasBeenDiscarded)
                    {
                        continue;
                    }

                    List<EnemySightRangeTweak> tweaks = null;
                    foreach (EnemySightRangeTweak tweak in npc.Elements<EnemySightRangeTweak>())
                    {
                        if (tweaks == null)
                        {
                            tweaks = new List<EnemySightRangeTweak>();
                        }
                        tweaks.Add(tweak);
                    }
                    if (tweaks == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < tweaks.Count; i++)
                    {
                        tweaks[i].Discard();
                    }
                }
            }
            catch (Exception ex)
            {
                Warn("Could not remove all enemy sight-range modifiers during shutdown: " + ex.GetBaseException().Message);
            }
        }

        private void RemoveAllEnemyMovementSpeedTweaks()
        {
            try
            {
                foreach (NpcElement npc in World.All<NpcElement>())
                {
                    if (npc == null || npc.HasBeenDiscarded)
                    {
                        continue;
                    }

                    List<EnemyMovementSpeedTweak> tweaks = null;
                    foreach (EnemyMovementSpeedTweak tweak in npc.Elements<EnemyMovementSpeedTweak>())
                    {
                        if (tweaks == null)
                        {
                            tweaks = new List<EnemyMovementSpeedTweak>();
                        }
                        tweaks.Add(tweak);
                    }
                    if (tweaks == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < tweaks.Count; i++)
                    {
                        tweaks[i].Discard();
                    }
                }
            }
            catch (Exception ex)
            {
                Warn("Could not remove all enemy movement-speed modifiers during shutdown: " + ex.GetBaseException().Message);
            }
        }

        private static bool ApproximatelyNeutral(float value)
        {
            return Math.Abs(value - 1.0f) <= NeutralTolerance;
        }

        private void LogDifficultyDiagnostic(string lever, float before, float after, float adjustment)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            LogDiagnostic(
                "Difficulty modifier: lever="
                + lever
                + ", preset="
                + (_preset == null ? "Unknown" : _preset.Value.ToString())
                + ", before="
                + before.ToString("0.###", CultureInfo.InvariantCulture)
                + ", after="
                + after.ToString("0.###", CultureInfo.InvariantCulture)
                + ", adjustment="
                + adjustment.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
        }

        private void LogEnemyHearingRangeDiagnostic(float before, float after, float adjustment)
        {
            if (!DiagnosticsEnabled() || Time.unscaledTime < _nextEnemyHearingRangeDiagnosticAt)
            {
                return;
            }

            _nextEnemyHearingRangeDiagnosticAt = Time.unscaledTime + EnemyHearingRangeDiagnosticIntervalSeconds;
            LogDifficultyDiagnostic("EnemyHearingRange", before, after, adjustment);
        }

        private void EvaluateCompatibilityOverlaps()
        {
            try
            {
                EvaluateAvalonAiOverhaulOverlap();
                EvaluateCustomDifficultyOverlap();
                EvaluateFlatArrowsOverlap();
                EvaluateHarderLifeOverlap();
                EvaluateTaintedCombatOverlap();
                EvaluateTaintedInstinctsOverlap();
            }
            catch (Exception ex)
            {
                if (DiagnosticsEnabled())
                {
                    LogDiagnostic("Compatibility overlap check failed: " + ex.GetBaseException().Message);
                }
            }
        }

        private void EvaluateAvalonAiOverhaulOverlap()
        {
            BaseUnityPlugin plugin;
            if (!TryGetEnabledPlugin(AvalonAiOverhaulPluginGuid, out plugin)
                || !ReadExternalBool(plugin, "General", "Enabled", true))
            {
                return;
            }

            bool sightOverlap = ExternalFloatIsNonNeutral(
                plugin,
                "Vision",
                "NpcVisionDistanceMultiplier",
                1.0f);
            bool hearingOverlap = ReadExternalBool(
                    plugin,
                    "Movement Awareness",
                    "EnableStandingFootstepAwareness",
                    true)
                && (ExternalFloatIsNonNeutral(
                        plugin,
                        "Movement Awareness",
                        "OpenWorldFootstepRangeMultiplier",
                        1.0f)
                    || ExternalFloatIsNonNeutral(
                        plugin,
                        "Movement Awareness",
                        "InteriorFootstepRangeMultiplier",
                        1.0f));
            string combatLeashMode = ReadExternalString(
                plugin,
                "Return Distance",
                "CombatLeashMode",
                "Vanilla");
            bool persistenceOverlap = string.Equals(
                    combatLeashMode,
                    "FixedDistance",
                    StringComparison.OrdinalIgnoreCase)
                || (string.Equals(
                        combatLeashMode,
                        "ScalePerNpc",
                        StringComparison.OrdinalIgnoreCase)
                    && ExternalFloatIsNonNeutral(
                        plugin,
                        "Return Distance",
                        "CombatLeashScale",
                        1.0f));

            List<string> conflicts = new List<string>();
            AddConflictIf(conflicts, EnemySightRangeModifierIsEffective() && sightOverlap, "ModifyEnemySightRange");
            AddConflictIf(conflicts, EnemyHearingRangeModifierIsEffective() && hearingOverlap, "ModifyEnemyHearingRange");
            AddConflictIf(conflicts, EnemyAggroPersistenceModifierIsEffective() && persistenceOverlap, "ModifyEnemyAggroPersistence");
            ReportCompatibilityOverlap("Avalon AI Overhaul", conflicts);
        }

        private void EvaluateCustomDifficultyOverlap()
        {
            BaseUnityPlugin plugin;
            if (!TryGetEnabledPlugin(CustomDifficultyPluginGuid, out plugin))
            {
                return;
            }

            bool outgoingOverlap = ExternalFloatIsNonNeutral(
                plugin,
                "DamageDealtMultipliers",
                "PlayerDamageDealtMultiplier",
                1.0f);
            List<string> conflicts = new List<string>();
            AddConflictIf(
                conflicts,
                OutgoingDamageModifierIsEffective()
                    && outgoingOverlap,
                "ModifyPlayerDamageDealt");
            AddConflictIf(
                conflicts,
                ProgressiveTenacityEnabled() && outgoingOverlap,
                "ProgressiveTenacityEnabled");
            AddConflictIf(
                conflicts,
                PlayerPressureModifierIsEffective(_modifyPlayerDamageTaken)
                    && ExternalFloatIsNonNeutral(plugin, "DamageTakenMultipliers", "PlayerDamageTakenMultiplier", 1.0f),
                "ModifyPlayerDamageTaken");
            AddConflictIf(
                conflicts,
                PresetModifierIsEffective(_modifyStaminaUsage)
                    && ExternalFloatIsNonNeutral(plugin, "DifficultyMultipliers", "StaminaUsageMultiplier", 1.0f),
                "ModifyStaminaUsage");
            AddConflictIf(
                conflicts,
                PresetModifierIsEffective(_modifyManaUsage)
                    && ExternalFloatIsNonNeutral(plugin, "DifficultyMultipliers", "ManaUsageMultiplier", 1.0f),
                "ModifyManaUsage");
            AddConflictIf(
                conflicts,
                AttackSlotsModifierIsEffective()
                    && ExternalIntIsNonZero(plugin, "DifficultySettings", "AdditionalEnemyAttackingCount"),
                "ModifyEnemyAttackSlots");
            AddConflictIf(
                conflicts,
                PlayerPressureModifierIsEffective(_modifyKillExperience)
                    && ExternalFloatIsNonNeutral(plugin, "ExpMultipliers", "KillExpMultiplier", 1.0f),
                "ModifyKillExperience");
            AddConflictIf(
                conflicts,
                PlayerPressureModifierIsEffective(_modifyQuestExperience)
                    && ExternalFloatIsNonNeutral(plugin, "ExpMultipliers", "QuestExpMultiplier", 1.0f),
                "ModifyQuestExperience");
            AddConflictIf(
                conflicts,
                PlayerPressureModifierIsEffective(_modifyProficiencyExperience)
                    && CustomDifficultyChangesProficiencyExperience(plugin),
                "ModifyProficiencyExperience");
            ReportCompatibilityOverlap("Custom Difficulty", conflicts);
        }

        private void EvaluateFlatArrowsOverlap()
        {
            BaseUnityPlugin plugin;
            if (!TryGetEnabledPlugin(FlatArrowsPluginGuid, out plugin)
                || !ReadExternalBool(plugin, "General", "EnableFlatArrows", true)
                || !ReadExternalBool(plugin, "AMOD", "EnableArrowModifications", true))
            {
                return;
            }

            List<string> conflicts = new List<string>();
            AddConflictIf(
                conflicts,
                DifficultyModifierIsEnabled(_modifyPlayerArrowVelocity),
                "ModifyPlayerArrowVelocity");
            AddConflictIf(
                conflicts,
                PlayerArrowDropModifierIsEffective(),
                "ModifyPlayerArrowDrop");
            ReportCompatibilityOverlap("Flat Arrows", conflicts);
        }

        private void EvaluateHarderLifeOverlap()
        {
            BaseUnityPlugin plugin;
            if (!TryGetEnabledPlugin(HarderLifePluginGuid, out plugin)
                || !ReadExternalBool(plugin, "0. Master Switch", "Enabled", true))
            {
                return;
            }

            bool combatScalingEnabled = ReadExternalBool(
                plugin,
                "2. Combat Scaling",
                "CombatScalingEnabled",
                true);
            bool staminaPenaltyEnabled = ReadExternalBool(
                plugin,
                "3. Stamina",
                "StaminaPenaltyEnabled",
                true);
            bool parryEnabled = ReadExternalBool(plugin, "1. Parry", "ParryEnabled", true);
            bool aggroRangeEnabled = ReadExternalBool(plugin, "4. Aggro", "AggroRangeEnabled", true);
            bool potionNerfEnabled = ReadExternalBool(plugin, "5. Potions", "PotionNerfEnabled", true);
            bool enemyPerceptionEnabled = ReadExternalBool(
                plugin,
                "7. Enemy Perception",
                "EnemyPerceptionEnabled",
                true);

            bool outgoingOverlap = combatScalingEnabled
                && (ExternalFloatIsNonNeutral(plugin, "2. Combat Scaling", "OutgoingDamageMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "2. Combat Scaling", "OutgoingMagicDamageMultiplier", 1.0f));
            bool incomingOverlap = combatScalingEnabled
                && ExternalFloatIsNonNeutral(plugin, "2. Combat Scaling", "IncomingDamageMultiplier", 1.0f);
            bool staminaOverlap = (combatScalingEnabled
                    && ExternalFloatIsNonNeutral(plugin, "2. Combat Scaling", "StaminaUsageMultiplier", 1.0f))
                || (staminaPenaltyEnabled
                    && (ExternalFloatIsNonNeutral(plugin, "3. Stamina", "BlockStaminaCostMultiplier", 1.0f)
                        || ExternalFloatIsNonNeutral(plugin, "3. Stamina", "DashStaminaCostMultiplier", 1.0f)))
                || (parryEnabled
                    && ExternalFloatIsNonNeutral(plugin, "1. Parry", "ParryStaminaCostMultiplier", 1.0f));
            bool manaOverlap = combatScalingEnabled
                && ExternalFloatIsNonNeutral(plugin, "2. Combat Scaling", "ManaUsageMultiplier", 1.0f);
            bool sightOverlap = aggroRangeEnabled
                && ExternalFloatIsNonNeutral(plugin, "4. Aggro", "AggroRangeMultiplier", 1.0f);
            bool hearingOverlap = enemyPerceptionEnabled
                && ExternalFloatIsNonNeutral(plugin, "7. Enemy Perception", "HearingRangeMultiplier", 1.0f);
            bool persistenceOverlap = enemyPerceptionEnabled
                && ExternalFloatIsNonNeutral(plugin, "7. Enemy Perception", "AggroPersistenceMultiplier", 1.0f);
            bool consumableOverlap = potionNerfEnabled
                && (ExternalFloatIsNonNeutral(plugin, "5. Potions", "PotionEffectivenessMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "5. Potions", "ConsumableEffectivenessMultiplier", 1.0f));

            List<string> conflicts = new List<string>();
            AddConflictIf(conflicts, OutgoingDamageModifierIsEffective() && outgoingOverlap, "ModifyPlayerDamageDealt");
            AddConflictIf(conflicts, ProgressiveTenacityEnabled() && outgoingOverlap, "ProgressiveTenacityEnabled");
            AddConflictIf(conflicts, PlayerPressureModifierIsEffective(_modifyPlayerDamageTaken) && incomingOverlap, "ModifyPlayerDamageTaken");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyStaminaUsage) && staminaOverlap, "ModifyStaminaUsage");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyManaUsage) && manaOverlap, "ModifyManaUsage");
            AddConflictIf(conflicts, EnemySightRangeModifierIsEffective() && sightOverlap, "ModifyEnemySightRange");
            AddConflictIf(conflicts, EnemyHearingRangeModifierIsEffective() && hearingOverlap, "ModifyEnemyHearingRange");
            AddConflictIf(conflicts, EnemyAggroPersistenceModifierIsEffective() && persistenceOverlap, "ModifyEnemyAggroPersistence");
            AddConflictIf(conflicts, FoodRecoveryModifierIsEffective() && consumableOverlap, "ModifyFoodRecovery");
            ReportCompatibilityOverlap("HarderLife", conflicts);
        }

        private void EvaluateTaintedCombatOverlap()
        {
            BaseUnityPlugin plugin;
            if (!TryGetEnabledPlugin(TaintedCombatPluginGuid, out plugin)
                || !ReadExternalBool(plugin, "General", "Enabled", true))
            {
                return;
            }

            string preset = ReadExternalString(plugin, "General", "Preset", "Unknown");
            bool custom = string.Equals(preset, "Custom", StringComparison.OrdinalIgnoreCase);
            bool momentum = ReadExternalBool(plugin, "Combat Momentum", "Enabled", true);
            bool foodCooldownOverlap = ReadExternalBool(
                plugin,
                "Consumable Rules",
                "AffectFoodAndDishes",
                false);
            bool staminaOverlap;
            bool slotOverlap;
            bool recoveryOverlap;
            bool poiseOverlap;
            bool armorOverlap;
            bool parryWindowOverlap;

            if (custom)
            {
                bool momentumOverlap = momentum
                    && ExternalFloatIsNonNeutral(plugin, "Combat Momentum", "MaxPressureMultiplier", 1.0f);
                staminaOverlap = momentumOverlap
                    || ExternalFloatIsNonNeutral(plugin, "Custom", "ActionStaminaCostMultiplier", 1.0f)
                    || ExternalSectionContainsNonNeutralFloat(plugin, "Custom Action Split", false)
                    || ExternalFloatIsNonNeutral(plugin, "Custom Guard", "ParryStaminaCostMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "Custom Guard", "BlockStaminaDamageMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "Custom Guard", "HoldBlockCostMultiplier", 1.0f)
                    || ExternalFloatIsNonZero(plugin, "Custom Guard", "BlockEnterStaminaCost");
                slotOverlap = ExternalFloatIsNonNeutral(plugin, "Custom", "EnemyAttackSlotsMultiplier", 1.0f)
                    || momentumOverlap;
                recoveryOverlap = momentumOverlap
                    || ExternalFloatIsNonNeutral(plugin, "Custom", "EnemyAttackRecoveryMultiplier", 1.0f);
                parryWindowOverlap = ExternalFloatIsNonZero(
                    plugin,
                    "Custom Guard",
                    "ParryWindowBonus");
                armorOverlap = ExternalFloatIsNonNeutral(plugin, "Custom Armor", "ArmorPenaltyMultiplier", 1.0f);
                poiseOverlap = ExternalFloatIsNonNeutral(plugin, "Custom Poise", "PlayerPoiseDamageMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "Custom Poise", "HeavyAttackPoiseMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "Custom Poise", "PommelPoiseMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "Custom Poise", "ProjectilePoiseMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "Custom Poise", "MagicPoiseMultiplier", 1.0f)
                    || ExternalPositiveFloatIsNonNeutral(plugin, "Custom Poise", "DamageOverTimePoiseMultiplier");
            }
            else
            {
                staminaOverlap = true;
                recoveryOverlap = true;
                poiseOverlap = true;
                slotOverlap = !string.Equals(preset, "VanillaPlus", StringComparison.OrdinalIgnoreCase) || momentum;
                armorOverlap = !string.Equals(preset, "VanillaPlus", StringComparison.OrdinalIgnoreCase);
                parryWindowOverlap = !string.Equals(
                    preset,
                    "VanillaPlus",
                    StringComparison.OrdinalIgnoreCase);
            }

            List<string> conflicts = new List<string>();
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyStaminaUsage) && staminaOverlap, "ModifyStaminaUsage");
            AddConflictIf(conflicts, ParryWindowBonusModifierIsEffective() && parryWindowOverlap, "ModifyParryWindowBonus");
            AddConflictIf(conflicts, AttackSlotsModifierIsEffective() && slotOverlap, "ModifyEnemyAttackSlots");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyEnemyAttackRecovery) && recoveryOverlap, "ModifyEnemyAttackRecovery");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyPlayerPoiseDamageDealt) && poiseOverlap, "ModifyPlayerPoiseDamageDealt");
            AddConflictIf(conflicts, MaterialImpactRulesEnabled() && poiseOverlap, "MaterialImpactRulesEnabled");
            AddConflictIf(conflicts, ProgressiveTenacityEnabled() && poiseOverlap, "ProgressiveTenacityEnabled");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyArmorWeightPenalties) && armorOverlap, "ModifyArmorWeightPenalties");
            AddConflictIf(
                conflicts,
                FoodRecoveryModifierIsEffective() && foodCooldownOverlap,
                "ModifyFoodRecovery");
            AddConflictIf(
                conflicts,
                FoodCombatRestrictionIsEffective() && foodCooldownOverlap,
                "PreventFoodUseInCombat");
            ReportCompatibilityOverlap("Tainted Combat", conflicts);
        }

        private void EvaluateTaintedInstinctsOverlap()
        {
            BaseUnityPlugin plugin;
            if (!TryGetEnabledPlugin(TaintedInstinctsPluginGuid, out plugin))
            {
                return;
            }

            bool wolfEnabled = ReadExternalBool(plugin, "WolfTuning", "Enabled", true);
            bool undeadEnabled = ReadExternalBool(plugin, "UndeadTuning", "Enabled", true);
            bool outlawEnabled = ReadExternalBool(plugin, "OutlawTuning", "Enabled", true);
            bool bearEnabled = ReadExternalBool(plugin, "BearTuning", "Enabled", true);
            bool drownerEnabled = ReadExternalBool(plugin, "DrownerTuning", "Enabled", true);
            bool grindylowEnabled = ReadExternalBool(plugin, "GrindylowTuning", "Enabled", true);
            bool corpseEaterEnabled = ReadExternalBool(plugin, "CorpseEaterTuning", "Enabled", true);

            bool sightOverlap = (wolfEnabled && ExternalFloatIsNonNeutral(plugin, "WolfTuning", "DetectionRangeMultiplier", 1.0f))
                || (undeadEnabled && ExternalFloatIsNonNeutral(plugin, "UndeadTuning", "DetectionRangeMultiplier", 1.0f))
                || (outlawEnabled && (
                    ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "OneHandedDetectionRangeMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "TwoHandedDetectionRangeMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "ArcherDetectionRangeMultiplier", 1.0f)))
                || (bearEnabled && ExternalFloatIsNonNeutral(plugin, "BearTuning", "DetectionRangeMultiplier", 1.0f))
                || (drownerEnabled && ExternalFloatIsNonNeutral(plugin, "DrownerTuning", "DetectionRangeMultiplier", 1.0f))
                || (grindylowEnabled && ExternalFloatIsNonNeutral(plugin, "GrindylowTuning", "DetectionRangeMultiplier", 1.0f))
                || (corpseEaterEnabled && ExternalFloatIsNonNeutral(plugin, "CorpseEaterTuning", "DetectionRangeMultiplier", 1.0f));

            bool damageOverlap = (wolfEnabled && ExternalFloatIsNonNeutral(plugin, "WolfTuning", "MeleeDamageMultiplier", 1.0f))
                || (undeadEnabled && ExternalFloatIsNonNeutral(plugin, "UndeadTuning", "MeleeDamageMultiplier", 1.0f))
                || (outlawEnabled && (
                    ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "OneHandedMeleeDamageMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "TwoHandedMeleeDamageMultiplier", 1.0f)
                    || ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "ArcherRangedDamageMultiplier", 1.0f)))
                || (bearEnabled && ExternalFloatIsNonNeutral(plugin, "BearTuning", "MeleeDamageMultiplier", 1.0f))
                || (drownerEnabled && ExternalFloatIsNonNeutral(plugin, "DrownerTuning", "MeleeDamageMultiplier", 1.0f))
                || (grindylowEnabled && ExternalFloatIsNonNeutral(plugin, "GrindylowTuning", "MeleeDamageMultiplier", 1.0f))
                || (corpseEaterEnabled && ExternalFloatIsNonNeutral(plugin, "CorpseEaterTuning", "MeleeDamageMultiplier", 1.0f));

            bool slotOverlap = (undeadEnabled && ReadExternalBool(plugin, "UndeadTuning", "IgnoreMeleeCombatSlots", true))
                || (outlawEnabled && (
                    ReadExternalBool(plugin, "OutlawTuning", "IgnoreOneHandedMeleeCombatSlots", true)
                    || ReadExternalBool(plugin, "OutlawTuning", "IgnoreTwoHandedMeleeCombatSlots", true)))
                || (drownerEnabled && ReadExternalBool(plugin, "DrownerTuning", "IgnoreMeleeCombatSlots", true))
                || (grindylowEnabled && ReadExternalBool(plugin, "GrindylowTuning", "IgnoreMeleeCombatSlots", true))
                || (corpseEaterEnabled && ReadExternalBool(plugin, "CorpseEaterTuning", "IgnoreMeleeCombatSlots", true));

            bool recoveryOverlap = (wolfEnabled
                    && ExternalFloatIsNonNeutral(plugin, "WolfTuning", "AttackCooldownSeconds", 2.0f))
                || (outlawEnabled && (
                    ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "OneHandedApproachAttackCooldownSeconds", 9.0f)
                    || ExternalFloatIsNonNeutral(plugin, "OutlawTuning", "TwoHandedApproachAttackCooldownSeconds", 10.0f)))
                || (bearEnabled
                    && ExternalFloatIsNonNeutral(plugin, "BearTuning", "TimedPredatorAttackCooldownSeconds", 5.0f))
                || (drownerEnabled
                    && ExternalFloatIsNonNeutral(plugin, "DrownerTuning", "ApproachAttackCooldownSeconds", 12.0f))
                || (grindylowEnabled
                    && ExternalFloatIsNonNeutral(plugin, "GrindylowTuning", "ApproachAttackCooldownSeconds", 5.0f))
                || (corpseEaterEnabled
                    && ExternalFloatIsNonNeutral(plugin, "CorpseEaterTuning", "ApproachAttackCooldownSeconds", 15.0f));
            bool pursuitOverlap = wolfEnabled
                && ExternalFloatIsNonZero(plugin, "WolfTuning", "PursuitMemoryExtraSeconds");

            List<string> conflicts = new List<string>();
            AddConflictIf(conflicts, EnemySightRangeModifierIsEffective() && sightOverlap, "ModifyEnemySightRange");
            AddConflictIf(conflicts, PlayerPressureModifierIsEffective(_modifyPlayerDamageTaken) && damageOverlap, "ModifyPlayerDamageTaken");
            AddConflictIf(conflicts, AttackSlotsModifierIsEffective() && slotOverlap, "ModifyEnemyAttackSlots");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyEnemyAttackRecovery) && recoveryOverlap, "ModifyEnemyAttackRecovery");
            AddConflictIf(conflicts, EnemyAggroPersistenceModifierIsEffective() && pursuitOverlap, "ModifyEnemyAggroPersistence");
            ReportCompatibilityOverlap("Tainted Instincts", conflicts);
        }

        private bool TryGetEnabledPlugin(string pluginGuid, out BaseUnityPlugin plugin)
        {
            plugin = null;
            if (!Chainloader.PluginInfos.ContainsKey(pluginGuid))
            {
                return false;
            }

            plugin = Chainloader.PluginInfos[pluginGuid].Instance;
            return plugin != null && plugin.enabled;
        }

        private bool OutgoingDamageModifierIsEffective()
        {
            if (!DifficultyModifierIsEnabled(_modifyPlayerDamageDealt))
            {
                return false;
            }

            float multiplier = PresetPlayerPressureReductionMultiplier();
            return !ApproximatelyNeutral(multiplier);
        }

        private bool PresetModifierIsEffective(ConfigEntry<bool> setting)
        {
            return DifficultyModifierIsEnabled(setting) && PresetPenaltyAmount() > NeutralTolerance;
        }

        private bool PlayerPressureModifierIsEffective(ConfigEntry<bool> setting)
        {
            return DifficultyModifierIsEnabled(setting) && PresetPlayerPressureAmount() > NeutralTolerance;
        }

        private bool PlayerArrowDropModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyPlayerArrowDrop)
                && _playerArrowGravityMultiplier != null
                && _playerArrowGravityMultiplier.Value < 1.0f - NeutralTolerance;
        }

        private bool ParryWindowBonusModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyParryWindowBonus)
                && _positiveParryWindowBonusMultiplier != null
                && _positiveParryWindowBonusMultiplier.Value < 1.0f - NeutralTolerance;
        }

        private bool EnemySightRangeModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyEnemySightRange)
                && !ApproximatelyNeutral(PresetEnemySightRangeMultiplier());
        }

        private bool EnemyHearingRangeModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyEnemyHearingRange)
                && !ApproximatelyNeutral(PresetEnemyHearingRangeMultiplier());
        }

        private bool EnemyAggroPersistenceModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyEnemyAggroPersistence)
                && !ApproximatelyNeutral(PresetEnemyAggroPersistenceMultiplier());
        }

        private bool PotionOverdrinkingModifierIsEnabled()
        {
            return DifficultyModifierIsEnabled(_modifyPotionOverdrinking);
        }

        private bool FoodRecoveryModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyFoodRecovery)
                && (!ApproximatelyNeutral(PresetFoodHealthRateMultiplier())
                    || !ApproximatelyNeutral(PresetFoodHealthDurationMultiplier())
                    || PresetFoodStaminaRate() > NeutralTolerance);
        }

        private bool FoodCombatRestrictionIsEffective()
        {
            return DifficultyModifierIsEnabled(_preventFoodUseInCombat);
        }

        private bool AttackSlotsModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyEnemyAttackSlots) && PresetAttackSlotBonus() > 0;
        }

        private static void AddConflictIf(List<string> conflicts, bool condition, string settingName)
        {
            if (condition)
            {
                conflicts.Add(settingName);
            }
        }

        private void ReportCompatibilityOverlap(string pluginName, List<string> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                return;
            }

            string settingList = string.Join(", ", conflicts.ToArray());
            string signature = pluginName + "|" + settingList;
            if (_loggedOverlapSignatures.Add(signature))
            {
                Log.LogWarning(
                    "Compatibility overlap with "
                    + pluginName
                    + ". Active Steel and Bone settings: "
                    + settingList
                    + ". Disable one side of each overlap to prevent conflicting behavior.");
            }

            if (_notifiedOverlapSignatures.Contains(signature))
            {
                return;
            }

            string eventId;
            string message;
            if (string.Equals(pluginName, "Avalon AI Overhaul", StringComparison.Ordinal))
            {
                eventId = "compatibility-avalon-ai-overhaul";
                message = "Overlapping enemy perception or pursuit modifiers are active with Avalon AI Overhaul. See the BepInEx log for the settings to disable.";
            }
            else if (string.Equals(pluginName, "Tainted Combat", StringComparison.Ordinal))
            {
                eventId = "compatibility-tainted-combat";
                message = "Overlapping combat or food-use modifiers are active with Tainted Combat. See the BepInEx log for details.";
            }
            else if (string.Equals(pluginName, "Flat Arrows", StringComparison.Ordinal))
            {
                eventId = "compatibility-flat-arrows";
                message = "Overlapping player-arrow modifiers are active with Flat Arrows. See the BepInEx log for the settings to disable.";
            }
            else if (string.Equals(pluginName, "HarderLife", StringComparison.Ordinal))
            {
                eventId = "compatibility-harder-life";
                message = "Overlapping difficulty modifiers are active with HarderLife. See the BepInEx log for the settings to disable.";
            }
            else if (string.Equals(pluginName, "Tainted Instincts", StringComparison.Ordinal))
            {
                eventId = "compatibility-tainted-instincts";
                message = "Overlapping enemy modifiers are active with Tainted Instincts. See the BepInEx log for the settings to disable.";
            }
            else
            {
                eventId = "compatibility-custom-difficulty";
                message = "Overlapping modifiers are active with Custom Difficulty. See the BepInEx log for the settings to disable.";
            }
            if (Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowCompatibilityWarning(
                PluginGuid,
                eventId,
                message))
            {
                _notifiedOverlapSignatures.Add(signature);
            }
        }

        private static ConfigEntryBase FindExternalConfigEntry(BaseUnityPlugin plugin, string section, string key)
        {
            if (plugin == null || plugin.Config == null)
            {
                return null;
            }

            ConfigDefinition definition = new ConfigDefinition(section, key);
            return plugin.Config.ContainsKey(definition) ? plugin.Config[definition] : null;
        }

        private static bool ReadExternalBool(BaseUnityPlugin plugin, string section, string key, bool fallback)
        {
            ConfigEntryBase entry = FindExternalConfigEntry(plugin, section, key);
            if (entry == null || entry.BoxedValue == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(entry.BoxedValue, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadExternalString(BaseUnityPlugin plugin, string section, string key, string fallback)
        {
            ConfigEntryBase entry = FindExternalConfigEntry(plugin, section, key);
            return entry == null || entry.BoxedValue == null
                ? fallback
                : Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture);
        }

        private static float ReadExternalFloat(BaseUnityPlugin plugin, string section, string key, float fallback)
        {
            ConfigEntryBase entry = FindExternalConfigEntry(plugin, section, key);
            if (entry == null || entry.BoxedValue == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(entry.BoxedValue, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ExternalFloatIsNonNeutral(
            BaseUnityPlugin plugin,
            string section,
            string key,
            float fallback)
        {
            return !ApproximatelyNeutral(ReadExternalFloat(plugin, section, key, fallback));
        }

        private static bool ExternalPositiveFloatIsNonNeutral(
            BaseUnityPlugin plugin,
            string section,
            string key)
        {
            float value = ReadExternalFloat(plugin, section, key, 0.0f);
            return value > NeutralTolerance && !ApproximatelyNeutral(value);
        }

        private static bool ExternalIntIsNonZero(BaseUnityPlugin plugin, string section, string key)
        {
            ConfigEntryBase entry = FindExternalConfigEntry(plugin, section, key);
            if (entry == null || entry.BoxedValue == null)
            {
                return false;
            }

            try
            {
                return Convert.ToInt32(entry.BoxedValue, CultureInfo.InvariantCulture) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool ExternalFloatIsNonZero(BaseUnityPlugin plugin, string section, string key)
        {
            return Math.Abs(ReadExternalFloat(plugin, section, key, 0.0f)) > NeutralTolerance;
        }

        private static bool ExternalSectionContainsNonNeutralFloat(
            BaseUnityPlugin plugin,
            string section,
            bool zeroIsNeutral)
        {
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in plugin.Config)
            {
                if (!string.Equals(pair.Key.Section, section, StringComparison.Ordinal)
                    || pair.Value == null
                    || pair.Value.BoxedValue == null)
                {
                    continue;
                }

                try
                {
                    float value = Convert.ToSingle(pair.Value.BoxedValue, CultureInfo.InvariantCulture);
                    if ((zeroIsNeutral && Math.Abs(value) <= NeutralTolerance)
                        || ApproximatelyNeutral(value))
                    {
                        continue;
                    }
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool CustomDifficultyChangesProficiencyExperience(BaseUnityPlugin plugin)
        {
            return ExternalFloatIsNonNeutral(plugin, "ExpMultipliers", "ProficiencyExpMultiplier", 1.0f)
                || ExternalSectionContainsNonNeutralFloat(plugin, "ProficiencyExpMultipliers", false);
        }

        private static class PlayerArrowVelocityPatch
        {
            public static void Prefix(ref Vector3 arrowVelocity)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyPlayerArrowVelocity(ref arrowVelocity);
                }
            }
        }

        private static class PlayerArrowGravityPatch
        {
            public static void Postfix(DamageDealingProjectile __instance, float deltaTime)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyPlayerArrowGravity(__instance, deltaTime);
                }
            }
        }

        private static class HeroPhysicalArmorPatch
        {
            public static void Postfix(Hero __instance, DamageSubType damageType, ref float __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyPhysicalArmorProtection(__instance, damageType, ref __result);
                }
            }
        }

        private static class HostileArrowBallisticsPatch
        {
            [ThreadStatic]
            private static float _velocityMultiplier;

            public static void Prefix(
                ref CombatBehaviourUtils.FireProjectileParams fireParams,
                VGUtils.ShootParams shootParams)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    _velocityMultiplier = 1.0f;
                    return;
                }

                plugin.ApplyHostileArcherAimScatter(ref fireParams, shootParams);
                _velocityMultiplier = plugin.HostileArrowVelocityMultiplier(shootParams);
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> patched = new List<CodeInstruction>();
                MethodInfo clamp = AccessTools.Method(
                    typeof(Mathf),
                    nameof(Mathf.Clamp),
                    new[] { typeof(float), typeof(float), typeof(float) });
                MethodInfo scale = AccessTools.Method(typeof(HostileArrowBallisticsPatch), nameof(ScaleBallisticVelocity));
                int matches = 0;

                foreach (CodeInstruction instruction in instructions)
                {
                    patched.Add(instruction);
                    if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, clamp))
                    {
                        patched.Add(new CodeInstruction(OpCodes.Call, scale));
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    throw new InvalidOperationException(
                        "Expected one ballistic velocity clamp in CombatBehaviourUtils.FireProjectile, found " + matches + ".");
                }

                return patched;
            }

            public static float ScaleBallisticVelocity(float velocity)
            {
                float multiplier = _velocityMultiplier <= 0.0f ? 1.0f : _velocityMultiplier;
                if (ApproximatelyNeutral(multiplier))
                {
                    return velocity;
                }

                float scaled = velocity * multiplier;
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.LogDifficultyDiagnostic("HostileArrowVelocity", velocity, scaled, multiplier);
                }
                return scaled;
            }

            public static Exception Finalizer(Exception __exception)
            {
                _velocityMultiplier = 1.0f;
                return __exception;
            }
        }

        private static class CombatManaRegenerationPatch
        {
            public static void Postfix(Hero __instance, ref float __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyCombatManaRegeneration(__instance, ref __result);
                }
            }
        }

        private static class PredictedCombatManaRegenerationPatch
        {
            public static void Postfix(Hero __instance, ref float __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyCombatManaRegeneration(__instance, ref __result);
                }
            }
        }

        private static class PositiveParryWindowBonusPatch
        {
            public static void Prefix(Hero hero, ref IDuration duration)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyPositiveParryWindowBonus(hero, ref duration);
                }
            }
        }

        private static class EnemyHearingRangePatch
        {
            public static void Prefix(ref float __0)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyEnemyHearingRange(ref __0);
                }
            }
        }

        private static class EnemyAggroPersistencePatch
        {
            public static void Postfix(NpcAI __0, ref float __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyEnemyAggroPersistence(__0, ref __result);
                }
            }
        }

        private static class FoodCombatUsePatch
        {
            public static bool Prefix(Item __instance)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    return true;
                }

                try
                {
                    return plugin.ShouldAllowFoodUse(__instance);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not evaluate the food combat-use restriction: "
                        + ex.GetBaseException().Message);
                    return true;
                }
            }
        }

        private static class ConsumableRecoveryPatch
        {
            public static void Prefix(
                ItemSkillsInvoker __instance,
                ItemActionType __0,
                ref ConsumableRecoveryPatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    return;
                }

                try
                {
                    plugin.CaptureConsumableRecovery(__instance, __0, ref __state);
                }
                catch (Exception ex)
                {
                    RestoreFoodSkillOverrides(__state == null ? null : __state.Food);
                    plugin.FinishConsumableRecovery(__state);
                    __state = null;
                    plugin.Warn("Could not capture consumable recovery: " + ex.GetBaseException().Message);
                }
            }

            public static void Postfix(ConsumableRecoveryPatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __state == null)
                {
                    return;
                }

                try
                {
                    plugin.ApplyConsumableRecovery(__state);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not apply consumable recovery: " + ex.GetBaseException().Message);
                }
            }

            public static Exception Finalizer(
                Exception __exception,
                ConsumableRecoveryPatchState __state)
            {
                try
                {
                    RestoreFoodSkillOverrides(__state == null ? null : __state.Food);
                    SteelAndBonePlugin plugin = Instance;
                    if (plugin != null)
                    {
                        plugin.FinishConsumableRecovery(__state);
                    }
                }
                catch (Exception ex)
                {
                    SteelAndBonePlugin plugin = Instance;
                    if (plugin != null)
                    {
                        plugin.Warn("Could not restore temporary food skill values: " + ex.GetBaseException().Message);
                    }
                }
                return __exception;
            }
        }

        private static class FoodDescriptionPatch
        {
            public static void Prefix(
                ExistingItemDescriptor __instance,
                ref ConsumableRecoveryPatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    Item item = __instance.ExistingItem;
                    Hero hero = Hero.Current;
                    if (item != null
                        && item.Template != null
                        && hero != null
                        && item.IsEdible
                        && !item.Template.IsPotion)
                    {
                        FoodSkillOverrideState foodState =
                            plugin.ApplyFoodSkillOverrides(item, hero);

                        if (foodState != null)
                        {
                            __state = new ConsumableRecoveryPatchState
                            {
                                Food = foodState
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    RestoreFoodSkillOverrides(__state == null ? null : __state.Food);
                    __state = null;
                    plugin.Warn("Could not prepare live food tooltip values: " + ex.GetBaseException().Message);
                }
            }

            public static void Postfix(
                ref string __result,
                ConsumableRecoveryPatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __state == null)
                {
                    return;
                }

                try
                {
                    RestoreFoodSkillOverrides(__state.Food);
                    if (__state.Food != null)
                    {
                        __result = plugin.AppendFoodStaminaDescription(__state.Food, __result);
                    }
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not finish live food tooltip values: " + ex.GetBaseException().Message);
                }
            }

            public static Exception Finalizer(
                Exception __exception,
                ConsumableRecoveryPatchState __state)
            {
                try
                {
                    RestoreFoodSkillOverrides(__state == null ? null : __state.Food);
                }
                catch (Exception ex)
                {
                    SteelAndBonePlugin plugin = Instance;
                    if (plugin != null)
                    {
                        plugin.Warn("Could not restore temporary food tooltip values: " + ex.GetBaseException().Message);
                    }
                }
                return __exception;
            }
        }

        private static class FoodStatusDescriptionPatch
        {
            public static void Postfix(Status __instance, ref string __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    __result = plugin.AppendActiveFoodStaminaDescription(
                        __instance,
                        __result);
                    __result = plugin.ReplacePotionPoisoningDescription(
                        __instance,
                        __result);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not append food stamina recovery to the active-effect description: " + ex.GetBaseException().Message);
                }
            }
        }

        private static class FoodStaminaRecoveryPatch
        {
            public static void Postfix(VHeroController __instance, float __0)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    plugin.RestoreFoodStaminaDirectly(Hero.Current, __0);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not apply direct food stamina recovery: " + ex.GetBaseException().Message);
                }
            }
        }

        private static class FoodOverexertionDurationPatch
        {
            public static void Prefix(
                ICharacter __0,
                StaminaRegenBlockType __1,
                ref IDuration __2,
                ref IDuration __3)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    return;
                }

                try
                {
                    plugin.ApplyFoodOverexertionDuration(
                        __0,
                        __1,
                        ref __2,
                        ref __3);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not apply food-supported overexertion duration: "
                        + ex.GetBaseException().Message);
                }
            }
        }

        private static class StaminaVignetteStartPatch
        {
            public static void Postfix(VHeroStaminaUsedUpEffect __instance)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    plugin.OnStaminaVignetteStarted(__instance);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not apply the configured Stamina Depleted vignette start: "
                        + ex.GetBaseException().Message);
                }
            }
        }

        private static class StaminaVignetteStopPatch
        {
            public static void Prefix(
                VHeroStaminaUsedUpEffect __instance,
                ref float __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    __state = plugin.CaptureStaminaVignetteAlpha(__instance);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not capture the Stamina Depleted vignette opacity: "
                        + ex.GetBaseException().Message);
                }
            }

            public static void Postfix(
                VHeroStaminaUsedUpEffect __instance,
                float __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    plugin.OnStaminaVignetteStopped(__instance, __state);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not apply the configured Stamina Depleted vignette finish: "
                        + ex.GetBaseException().Message);
                }
            }
        }

        private static class PotionPoisoningBuildupPatch
        {
            public static bool Prefix(
                CharacterStatuses __instance,
                ref float __0,
                StatusTemplate __1,
                StatusSourceInfo __2)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    return true;
                }

                try
                {
                    return plugin.ShouldApplyNativePotionPoisoningBuildup(
                        __instance,
                        __1,
                        __2);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not adjust native Potion Poisoning buildup: " + ex.GetBaseException().Message);
                    return true;
                }
            }
        }

        private static class PotionPoisoningActivationPatch
        {
            public static void Postfix(BuildupStatus __instance)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    plugin.ApplyPotionPoisoningPenalty(__instance);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not initialize the Potion Poisoning resource drain: " + ex.GetBaseException().Message);
                }
            }
        }

        private static class PotionPoisoningDecayPatch
        {
            public static void Prefix(BuildupStatus __instance, ref float __state)
            {
                __state = __instance == null ? 0.0f : __instance.BuildupProgress;
            }

            public static void Postfix(BuildupStatus __instance, float __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __instance == null)
                {
                    return;
                }

                try
                {
                    plugin.ApplyPotionPoisoningDrain(__instance, __state);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not apply the Potion Poisoning resource drain: " + ex.GetBaseException().Message);
                }
            }
        }

        private static class BetterUiConsumableEffectPatch
        {
            public static void Prefix(Item __0, ref FoodSkillOverrideState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                Hero hero = Hero.Current;
                if (plugin == null
                    || __0 == null
                    || __0.Template == null
                    || hero == null
                    || !__0.IsEdible
                    || __0.Template.IsPotion)
                {
                    return;
                }

                try
                {
                    __state = plugin.ApplyFoodSkillOverrides(__0, hero);
                }
                catch (Exception ex)
                {
                    RestoreFoodSkillOverrides(__state);
                    __state = null;
                    plugin.Warn("Could not prepare Better UI food-overlay values: " + ex.GetBaseException().Message);
                }
            }

            public static void Postfix(
                ref ValueTuple<string, Color> __result,
                FoodSkillOverrideState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null || __state == null)
                {
                    return;
                }

                try
                {
                    RestoreFoodSkillOverrides(__state);
                    __result = plugin.BuildBetterUiFoodOverlay(__state, __result);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not finish Better UI food-overlay values: " + ex.GetBaseException().Message);
                }
            }

            public static Exception Finalizer(
                Exception __exception,
                FoodSkillOverrideState __state)
            {
                try
                {
                    RestoreFoodSkillOverrides(__state);
                }
                catch (Exception ex)
                {
                    SteelAndBonePlugin plugin = Instance;
                    if (plugin != null)
                    {
                        plugin.Warn("Could not restore temporary Better UI food values: " + ex.GetBaseException().Message);
                    }
                }
                return __exception;
            }
        }

        private static class CharacterStatsInitializePatch
        {
            public static void Postfix(CharacterStats stats)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    try
                    {
                        plugin.ApplyDifficultyStatTweaks(stats == null ? null : stats.ParentModel as Hero);
                    }
                    catch (Exception ex)
                    {
                        plugin.Warn("Could not apply player stat difficulty modifiers after stat initialization: " + ex.GetBaseException().Message);
                    }
                }
            }
        }

        private static class MaxEnemiesAttackingPatch
        {
            public static void Postfix(ref int __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyEnemyAttackSlots(ref __result);
                }
            }
        }

        private static class EnemyAttackRecoveryPatch
        {
            public static void Postfix(ref float __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyEnemyAttackRecovery(ref __result);
                }
            }
        }

        private static class KillExperiencePatch
        {
            public static void Postfix(ref int __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyKillExperience(ref __result);
                }
            }
        }

        private static class QuestExperiencePatch
        {
            public static void Postfix(ref float __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyQuestExperience(ref __result);
                }
            }
        }

        private static class ObjectiveExperiencePatch
        {
            public static void Postfix(ref float __result)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyQuestExperience(ref __result);
                }
            }
        }

        private static class ProficiencyExperiencePatch
        {
            public static void Prefix(ref float amountOfXPToAdd)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyProficiencyExperience(ref amountOfXPToAdd);
                }
            }
        }

        private static class PlayerPoiseDamagePatch
        {
            public static void Prefix(DamageOutcome damageOutcome, ref PoisePatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    __state = null;
                    return;
                }

                try
                {
                    plugin.ApplyPlayerPoiseDamage(damageOutcome, ref __state);
                }
                catch (Exception ex)
                {
                    if (__state != null)
                    {
                        try
                        {
                            plugin.RestorePlayerPoiseDamage(__state);
                        }
                        catch
                        {
                            // Preserve the original patch failure below.
                        }
                    }
                    __state = null;
                    plugin.Warn("Could not apply the player poise-damage modifier: " + ex.GetBaseException().Message);
                }
            }

            public static Exception Finalizer(Exception __exception, PoisePatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null && __state != null)
                {
                    try
                    {
                        plugin.RestorePlayerPoiseDamage(__state);
                    }
                    catch (Exception ex)
                    {
                        plugin.Warn("Could not restore native poise damage after processing: " + ex.GetBaseException().Message);
                    }
                }
                return __exception;
            }
        }

        private static class PlayerForceDamagePatch
        {
            public static void Prefix(DamageOutcome damageOutcome, ref ForcePatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    __state = null;
                    return;
                }

                try
                {
                    plugin.ApplyPlayerForceDamage(damageOutcome, ref __state);
                }
                catch (Exception ex)
                {
                    if (__state != null)
                    {
                        try
                        {
                            plugin.RestorePlayerForceDamage(__state);
                        }
                        catch
                        {
                            // Preserve the original patch failure below.
                        }
                    }
                    __state = null;
                    plugin.Warn("Could not apply resisted-arrow force scaling: " + ex.GetBaseException().Message);
                }
            }

            public static Exception Finalizer(Exception __exception, ForcePatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null && __state != null)
                {
                    try
                    {
                        plugin.RestorePlayerForceDamage(__state);
                    }
                    catch (Exception ex)
                    {
                        plugin.Warn("Could not restore native force damage after processing: " + ex.GetBaseException().Message);
                    }
                }
                return __exception;
            }
        }

        private static class RoutineResistedFlinchPatch
        {
            public static void Prefix(ref bool isDamageOverTime)
            {
                if (_suppressRoutineResistedFlinch)
                {
                    isDamageOverTime = true;
                }
            }
        }

        private static class ProgressiveTenacityStaminaDamagePatch
        {
            public static void Postfix(HealthElement __instance, Damage damage)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    return;
                }

                try
                {
                    plugin.ApplyProgressiveTenacityStaminaDamage(__instance, damage);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not apply Progressive Tenacity stamina damage: " + ex.GetBaseException().Message);
                }
            }
        }

        private static class ProgressiveTenacityParryPatch
        {
            public static void Prefix(
                HeroParry __instance,
                HookResult<HealthElement, Damage> hook,
                ref ParryStaminaPatchState __state)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin == null)
                {
                    __state = null;
                    return;
                }

                try
                {
                    plugin.ApplyProgressiveTenacityParry(__instance, hook, ref __state);
                }
                catch (Exception ex)
                {
                    if (__state != null)
                    {
                        SteelAndBonePlugin.RestoreProgressiveTenacityParry(__state);
                    }
                    __state = null;
                    plugin.Warn("Could not apply Progressive Tenacity parry stamina damage: " + ex.GetBaseException().Message);
                }
            }

            public static Exception Finalizer(Exception __exception, ParryStaminaPatchState __state)
            {
                try
                {
                    SteelAndBonePlugin.RestoreProgressiveTenacityParry(__state);
                }
                catch (Exception ex)
                {
                    SteelAndBonePlugin plugin = Instance;
                    if (plugin != null)
                    {
                        plugin.Warn("Could not restore native parry stamina damage after processing: " + ex.GetBaseException().Message);
                    }
                }
                return __exception;
            }
        }
    }
}
