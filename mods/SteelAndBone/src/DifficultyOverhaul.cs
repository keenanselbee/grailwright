using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using Awaken.TG.Main.AI;
using Awaken.TG.Main.AI.Combat.Utils;
using Awaken.TG.Main.AI.Fights.Projectiles;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.MVC;
using Awaken.TG.Main.Animations.FSM.Npc.Machines;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.General.StatTypes;
using Awaken.TG.Main.Grounds;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Attachments;
using Awaken.TG.Main.Heroes.Items.Tooltips.Descriptors;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Statuses;
using Awaken.TG.Main.Heroes.Statuses.Duration;
using Awaken.TG.Main.Heroes.Stats.Tweaks;
using Awaken.TG.Main.Settings.Gameplay;
using Awaken.TG.Main.Skills;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Quests.Objectives;
using Awaken.TG.Main.Templates;
using Awaken.TG.Main.UI.HUD.Notifications;
using Awaken.TG.Main.Utility.RichEnums;
using Awaken.TG.Main.VisualGraphUtils;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SteelAndBone
{
    public sealed partial class SteelAndBonePlugin
    {
        private const string CustomDifficultyPluginGuid = "jonanoj.CustomDifficulty";
        private const string FlatArrowsPluginGuid = "RedJohn260.FlatArrows";
        private const string HarderLifePluginGuid = "fuwuvi.HarderLife";
        private const string TaintedCombatPluginGuid = "kane.tgfoa.tainted-combat";
        private const string TaintedInstinctsPluginGuid = "kane.tgfoa.tainted-instincts";
        private const string VersatileWeaponsApiTypeName =
            "VersatileWeapons.VersatileWeaponsApi";
        private const float NeutralTolerance = 0.0001f;
        private const float DifficultyRefreshIntervalSeconds = 1.0f;
        private const string StandardFoodRecoveryGraphGuid = "1c2da8428b5a74142b93ed84593676a9";
        private const string NonStackingFoodStatusGuid = "bf8c8a961f51ba94faa9f5e02a0b9502";
        private const string FoodStaminaStatusSourceId = "ks.tgfoa.steel-and-bone:food-stamina";

        private static readonly FieldInfo SkillVariableOverridesField =
            AccessTools.Field(typeof(Skill), "_variableOverrides");
        private static readonly MethodInfo StatusSourceUniqueIdSetter =
            AccessTools.PropertySetter(typeof(StatusSourceInfo), "SourceUniqueID");

        private ConfigEntry<bool> _difficultyModifiersEnabled;
        private ConfigEntry<bool> _modifyPlayerDamageDealt;
        private ConfigEntry<float> _weakSpotDamageBonus;
        private ConfigEntry<bool> _modifyPlayerDamageTaken;
        private ConfigEntry<bool> _passiveShieldProtectionEnabled;
        private ConfigEntry<bool> _modifyStaminaUsage;
        private ConfigEntry<bool> _modifyManaUsage;
        private ConfigEntry<bool> _modifyPlayerPoiseDamageDealt;
        private ConfigEntry<bool> _modifyPlayerArrowVelocity;
        private ConfigEntry<bool> _modifyPlayerArrowDrop;
        private ConfigEntry<float> _playerArrowGravityMultiplier;
        private ConfigEntry<bool> _modifyArmorWeightPenalties;
        private ConfigEntry<bool> _modifyLightArmorMobility;
        private ConfigEntry<bool> _modifyArmorPhysicalProtection;
        private ConfigEntry<bool> _modifyFoodRecovery;
        private ConfigEntry<bool> _modifyConsumableRecovery;
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
        private bool _resourceStatsPatchAvailable;
        private bool _reportedEnemyRuntimeRefreshFailure;
        private bool _versatileWeaponsBridgeResolved;
        private bool _versatileWeaponsBridgeFailureLogged;
        private Func<bool> _versatileWeaponsIsMainHandSuppressed;
        private Func<bool> _versatileWeaponsIsOffHandSuppressed;

        private enum DifficultyStatTarget
        {
            StaminaUsage,
            ManaUsage,
            ArmorPenalty,
            LightArmorMovement
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
        }

        private sealed class ConsumableRecoveryPatchState
        {
            internal Stat Health;
            internal Stat Mana;
            internal Stat Stamina;
            internal float HealthBefore;
            internal float ManaBefore;
            internal float StaminaBefore;
            internal FoodSkillOverrideState Food;
        }

        private sealed class FoodSkillVariableSnapshot
        {
            internal Skill Skill;
            internal List<SkillVariable> VariableOverrides;
        }

        private sealed class FoodSkillOverrideState
        {
            internal Item Item;
            internal Hero Hero;
            internal readonly List<FoodSkillVariableSnapshot> Snapshots =
                new List<FoodSkillVariableSnapshot>();
            internal float AuthoredDuration;
            internal bool Restored;
        }

        private void BindDifficultyConfig()
        {
            _difficultyModifiersEnabled = Config.Bind(
                "1. Core",
                "DifficultyModifiersEnabled",
                true,
                ConfigUi("Master switch for Steel and Bone's global damage, resource, armor, projectile, enemy-awareness, enemy-pressure, poise, and experience modifiers. Material matchup rules remain active when this is disabled.", "General", "Difficulty Modifiers", 0, 20));

            _modifyPlayerDamageDealt = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerDamageDealt",
                true,
                ConfigUi("Reduce health damage dealt by the player by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Outgoing Player Damage", 60, 0));
            _weakSpotDamageBonus = Config.Bind(
                "6. Difficulty - Player",
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
                "6. Difficulty - Player",
                "ModifyPlayerDamageTaken",
                true,
                ConfigUi("Increase health damage taken from all routed damage sources by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Incoming Player Damage", 60, 10));
            _passiveShieldProtectionEnabled = Config.Bind(
                "6. Difficulty - Player",
                "PassiveShieldProtectionEnabled",
                true,
                ConfigUi("When Difficulty Modifiers is enabled, grant an equipped and readied shield modest passive protection against direct physical attacks from within its forward BlockAngle. Active blocks, rear attacks, magic, status effects, and damage over time are unaffected.", "Difficulty - Player", "Passive Shield Protection", 60, 20));
            _modifyStaminaUsage = Config.Bind(
                "6. Difficulty - Player",
                "ModifyStaminaUsage",
                true,
                ConfigUi("Increase player stamina usage by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Stamina Usage", 60, 30));
            _modifyManaUsage = Config.Bind(
                "6. Difficulty - Player",
                "ModifyManaUsage",
                true,
                ConfigUi("Increase player mana usage by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Mana Usage", 60, 40));
            _modifyPlayerPoiseDamageDealt = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerPoiseDamageDealt",
                true,
                ConfigUi("Reduce poise damage dealt by the player by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled, making enemies slightly harder to stagger-lock.", "Difficulty - Player", "Player Poise Damage Dealt", 60, 50));
            _modifyPlayerArrowVelocity = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerArrowVelocity",
                true,
                ConfigUi("Multiply player-fired arrow speed by 1.10, 1.30, or 1.50 according to the preset when Difficulty Modifiers is enabled. This setting does not alter damage; Arrow Material Rules controls the separate material matchup.", "Difficulty - Player", "Player Arrow Speed", 60, 60));
            _modifyPlayerArrowDrop = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerArrowDrop",
                true,
                ConfigUi("When Difficulty Modifiers is enabled, apply Player Arrow Gravity Multiplier to player-fired arrows. This reduces arrow drop without tilting the launch direction and is independent from the preset.", "Difficulty - Player", "Reduce Player Arrow Drop", 60, 70));
            _playerArrowGravityMultiplier = Config.Bind(
                "6. Difficulty - Player",
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
                "6. Difficulty - Player",
                "ModifyArmorWeightPenalties",
                true,
                ConfigUi("Multiply the game's native armor-weight penalties by 1.00, 1.05, or 1.10 according to the preset when Difficulty Modifiers is enabled. Existing armor proficiency still softens eligible penalties.", "Difficulty - Player", "Armor Weight Penalties", 60, 90));
            _modifyLightArmorMobility = Config.Bind(
                "6. Difficulty - Player",
                "ModifyLightArmorMobility",
                true,
                ConfigUi("Increase movement speed while in the game's Light armor tier by 0%, 2.5%, or 5% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Player", "Light Armor Mobility", 60, 100));
            _modifyArmorPhysicalProtection = Config.Bind(
                "6. Difficulty - Player",
                "ModifyArmorPhysicalProtection",
                true,
                ConfigUi("Multiply physical armor in Medium by 1.00/1.05/1.10 and in Heavy or Overload by 1.00/1.10/1.20 according to the preset when Difficulty Modifiers is enabled. Magical armor checks are unchanged.", "Difficulty - Player", "Physical Armor Protection", 60, 110));
            _modifyConsumableRecovery = Config.Bind(
                "6. Difficulty - Player",
                "ModifyConsumableRecovery",
                true,
                ConfigUi("Multiply positive health, stamina, and mana recovery from restorative consumables by 1.00, 0.90, or 0.80 according to the preset when Difficulty Modifiers is enabled. Non-restorative item effects are unchanged.", "Difficulty - Player", "Restorative Consumable Recovery", 60, 120));
            _modifyFoodRecovery = Config.Bind(
                "6. Difficulty - Player",
                "ModifyFoodRecovery",
                true,
                ConfigUi("Reshape standard food healing over time when Difficulty Modifiers is enabled. Hardened restores 75% per second for 1.5x duration and adds 0.5 stamina per second; Crucible restores 62.5% per second for 2x duration and adds 1 stamina per second. Tempered remains native.", "Difficulty - Player", "Food Recovery", 60, 125));

            _modifyEnemyAttackSlots = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyEnemyAttackSlots",
                true,
                ConfigUi("Add 0, 1, or 2 simultaneous enemy attack slots to the current game difficulty according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Enemies", "Enemy Attack Slots", 70, 0));
            _enemyAttackSlotCap = Config.Bind(
                "7. Difficulty - Enemies",
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
                "7. Difficulty - Enemies",
                "ModifyEnemyAttackRecovery",
                true,
                ConfigUi("Shorten the delay before enemies release attack slots by 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Enemies", "Enemy Attack Recovery Time", 70, 20));
            _modifyEnemyMovementSpeed = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyEnemyMovementSpeed",
                true,
                ConfigUi("Increase combat movement speed by up to 0%, 5%, or 10% according to the preset when Difficulty Modifiers is enabled. Ordinary agile enemies receive the full bonus; Medium-armored, Elite, Beholder, and Slugholder enemies receive at most half; Heavy-armored, massive, boss, and scripted enemies retain their vanilla speed.", "Difficulty - Enemies", "Enemy Movement Speed", 70, 30));
            _modifyHostileArrowVelocity = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyHostileArrowVelocity",
                true,
                ConfigUi("Multiply hostile NPC arrow speed by 1.10, 1.30, or 1.50 according to the preset when Difficulty Modifiers is enabled while preserving the game's ballistic aim calculation. Hostile arrow damage is unchanged.", "Difficulty - Enemies", "Hostile Arrow Speed", 70, 40));
            _hostileArcherAimScatter = Config.Bind(
                "7. Difficulty - Enemies",
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
                "7. Difficulty - Enemies",
                "ModifyEnemySightRange",
                true,
                ConfigUi("Multiply the native sight distance of active hostile NPCs by 1.10, 1.30, or 1.50 according to the preset when Difficulty Modifiers is enabled. Line of sight, visibility, alert behavior, and authored perception distances remain native.", "Difficulty - Enemies", "Hostile Enemy Sight Distance", 70, 50));
            _modifyEnemyHearingRange = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyEnemyHearingRange",
                true,
                ConfigUi("Multiply the native range of hero footstep noise by 1.10, 1.20, or 1.30 according to the preset when Difficulty Modifiers is enabled. Native hearing strength, wall checks, armor noise, and NPC hearing differences remain in control.", "Difficulty - Enemies", "Hostile Enemy Hearing Range", 70, 60));
            _modifyEnemyAggroPersistence = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyEnemyAggroPersistence",
                true,
                ConfigUi("Multiply native combat aggro persistence by 1.00, 1.10, or 1.20 according to the preset when Difficulty Modifiers is enabled. Chase boundaries, forced combat exit, target-loss rules, and alert behavior remain native.", "Difficulty - Enemies", "Enemy Aggro Persistence", 70, 70));

            _modifyKillExperience = Config.Bind(
                "8. Difficulty - Progression",
                "ModifyKillExperience",
                true,
                ConfigUi("Reduce experience gained from enemy kills by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Progression", "Kill XP", 80, 0));
            _modifyQuestExperience = Config.Bind(
                "8. Difficulty - Progression",
                "ModifyQuestExperience",
                true,
                ConfigUi("Reduce experience gained from quest and objective rewards by 5%, 10%, or 15% according to the preset when Difficulty Modifiers is enabled.", "Difficulty - Progression", "Quest and Objective XP", 80, 10));
            _modifyProficiencyExperience = Config.Bind(
                "8. Difficulty - Progression",
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
            _difficultyTweakHero = null;
        }

        private void Update()
        {
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
            }

            ReapplyDifficultyStatTweaks();
            RefreshEnemyRuntimeTweaks();
            EvaluateCompatibilityOverlaps();
        }

        private void PatchDifficultyOverhaul()
        {
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
            PatchConsumableRecovery();
            PatchHostileArrowBallistics();
            PatchPoiseDamage();
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
                Warn("Could not patch ItemSkillsInvoker.PerformImmediate; the restorative-consumable modifier is disabled.");
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
                    LogDiagnostic("Patched ItemSkillsInvoker.PerformImmediate for restorative consumables and food recovery.");
                }
                catch (Exception ex)
                {
                    Warn("Could not patch ItemSkillsInvoker.PerformImmediate; restorative consumable and food modifiers are disabled. " + ex.GetBaseException().Message);
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

        private float PresetEnemySightRangeMultiplier()
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

        private float PresetEnemyHearingRangeMultiplier()
        {
            if (_preset == null)
            {
                return 1.10f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.20f;
                case Preset.Crucible:
                    return 1.30f;
                case Preset.Tempered:
                default:
                    return 1.10f;
            }
        }

        private float PresetEnemyAggroPersistenceMultiplier()
        {
            if (_preset == null)
            {
                return 1.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.10f;
                case Preset.Crucible:
                    return 1.20f;
                case Preset.Tempered:
                default:
                    return 1.0f;
            }
        }

        private float PresetConsumableRecoveryMultiplier()
        {
            if (_preset == null)
            {
                return 1.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 0.90f;
                case Preset.Crucible:
                    return 0.80f;
                case Preset.Tempered:
                default:
                    return 1.0f;
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
                    return 0.75f;
                case Preset.Crucible:
                    return 0.625f;
                case Preset.Tempered:
                default:
                    return 1.0f;
            }
        }

        private float PresetFoodHealthDurationMultiplier()
        {
            if (_preset == null)
            {
                return 1.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 1.5f;
                case Preset.Crucible:
                    return 2.0f;
                case Preset.Tempered:
                default:
                    return 1.0f;
            }
        }

        private float PresetFoodStaminaRate()
        {
            if (_preset == null)
            {
                return 0.0f;
            }

            switch (_preset.Value)
            {
                case Preset.Hardened:
                    return 0.5f;
                case Preset.Crucible:
                    return 1.0f;
                case Preset.Tempered:
                default:
                    return 0.0f;
            }
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
            LogDifficultyDiagnostic("EnemyHearingRange", before, noiseRange, multiplier);
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

            bool isFood = item.IsEdible && !item.Template.IsPotion;
            FoodSkillOverrideState foodState = null;
            if (isFood && FoodRecoveryModifierIsEffective())
            {
                foodState = ApplyFoodSkillOverrides(item, hero);
            }

            if (!ConsumableRecoveryModifierIsEffective() || isFood)
            {
                if (foodState != null)
                {
                    state = new ConsumableRecoveryPatchState { Food = foodState };
                }
                return;
            }

            bool modifiesHealth = item.Template.ConsumableModifiesHealth;
            bool modifiesMana = item.Template.ConsumableModifiesMana;
            bool modifiesStamina = item.Template.ConsumableStamina;
            if (!modifiesHealth && !modifiesMana && !modifiesStamina)
            {
                return;
            }

            CharacterStats stats = hero.CharacterStats;
            Stat health = modifiesHealth ? hero.Health : null;
            Stat mana = modifiesMana && stats != null ? stats.Mana : null;
            Stat stamina = modifiesStamina && stats != null ? stats.Stamina : null;
            if (health == null && mana == null && stamina == null)
            {
                return;
            }

            state = new ConsumableRecoveryPatchState
            {
                Health = health,
                Mana = mana,
                Stamina = stamina,
                HealthBefore = health == null ? 0.0f : health.BaseValue,
                ManaBefore = mana == null ? 0.0f : mana.BaseValue,
                StaminaBefore = stamina == null ? 0.0f : stamina.BaseValue,
                Food = foodState
            };
        }

        private void ApplyConsumableRecovery(ConsumableRecoveryPatchState state)
        {
            if (state == null)
            {
                return;
            }

            RestoreFoodSkillOverrides(state.Food);
            ApplyFoodStaminaRecovery(state.Food);

            if (ConsumableRecoveryModifierIsEffective())
            {
                float multiplier = PresetConsumableRecoveryMultiplier();
                ApplyRestorativeConsumableMultiplier(state.Health, state.HealthBefore, multiplier, "Health");
                ApplyRestorativeConsumableMultiplier(state.Mana, state.ManaBefore, multiplier, "Mana");
                ApplyRestorativeConsumableMultiplier(state.Stamina, state.StaminaBefore, multiplier, "Stamina");
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

                    float? authoredDuration = skill.GetVariable("Duration", hero);
                    if (authoredDuration.HasValue)
                    {
                        state.AuthoredDuration = Mathf.Max(state.AuthoredDuration, authoredDuration.Value);
                    }

                    if (skill.GetRichEnum("StatEnum") != AliveStatType.HealthRegen)
                    {
                        continue;
                    }

                    List<SkillVariable> currentOverrides =
                        SkillVariableOverridesField.GetValue(skill) as List<SkillVariable>;
                    state.Snapshots.Add(new FoodSkillVariableSnapshot
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

                if (state.AuthoredDuration <= NeutralTolerance)
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
                FoodSkillVariableSnapshot snapshot = state.Snapshots[i];
                if (snapshot != null && snapshot.Skill != null)
                {
                    SkillVariableOverridesField.SetValue(snapshot.Skill, snapshot.VariableOverrides);
                }
            }
            state.Restored = true;
        }

        private void ApplyFoodStaminaRecovery(FoodSkillOverrideState state)
        {
            if (state == null
                || state.Hero == null
                || state.Hero.Statuses == null
                || state.AuthoredDuration <= NeutralTolerance)
            {
                return;
            }

            float staminaRate = PresetFoodStaminaRate();
            if (staminaRate <= NeutralTolerance)
            {
                return;
            }

            StatusTemplate template = TemplatesUtil.Load<StatusTemplate>(NonStackingFoodStatusGuid);
            if (template == null || StatusSourceUniqueIdSetter == null)
            {
                Warn("Could not resolve the native non-stacking food status; added food stamina recovery was skipped.");
                return;
            }

            RemoveFoodStaminaStatus(state.Hero);

            StatusSourceInfo sourceInfo = StatusSourceInfo
                .FromStatus(template)
                .WithCharacter(state.Hero)
                .WithItem(state.Item);
            StatusSourceUniqueIdSetter.Invoke(
                sourceInfo,
                new object[] { FoodStaminaStatusSourceId });

            SkillVariablesOverride variables = new SkillVariablesOverride(
                new[] { new SkillVariable("AddValue", staminaRate) },
                new[]
                {
                    new SkillRichEnum(
                        "StatEnum",
                        new RichEnumReference(CharacterStatType.StaminaRegen))
                });
            Status status = new Status(template, sourceInfo, variables);
            state.Hero.Statuses.AddNewStatus(
                status,
                new TimeDuration(state.AuthoredDuration));
        }

        private static void RemoveFoodStaminaStatus(Hero hero)
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
                        FoodStaminaStatusSourceId,
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

        private string AppendFoodStaminaDescription(
            FoodSkillOverrideState state,
            string description)
        {
            if (state == null || state.AuthoredDuration <= NeutralTolerance)
            {
                return description;
            }

            float staminaRate = PresetFoodStaminaRate();
            if (staminaRate <= NeutralTolerance)
            {
                return description;
            }

            string duration = state.AuthoredDuration.ToString("0.###", CultureInfo.InvariantCulture);
            string staminaLine = staminaRate < 0.75f
                ? "Restores 1 stamina every 2 seconds for " + duration + " seconds."
                : "Restores 1 stamina per second for " + duration + " seconds.";
            if (!string.IsNullOrEmpty(description)
                && description.IndexOf(staminaLine, StringComparison.Ordinal) >= 0)
            {
                return description;
            }

            return string.IsNullOrWhiteSpace(description)
                ? staminaLine
                : description.TrimEnd() + Environment.NewLine + staminaLine;
        }

        private void ApplyRestorativeConsumableMultiplier(
            Stat stat,
            float before,
            float multiplier,
            string label)
        {
            if (stat == null || multiplier >= 1.0f - NeutralTolerance)
            {
                return;
            }

            float restored = stat.BaseValue - before;
            if (restored <= 0.0f)
            {
                return;
            }

            float adjusted = before + restored * multiplier;
            stat.SetTo(adjusted, false, null);
            LogDifficultyDiagnostic("Consumable" + label, before + restored, adjusted, multiplier);
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
            if (!DifficultyModifierIsEnabled(_modifyPlayerPoiseDamageDealt))
            {
                return;
            }

            Damage damage = damageOutcome.Damage;
            if (damage == null || damage.PoiseDamage <= 0.0f || !ReferenceEquals(damageOutcome.AttackerPure, Hero.Current))
            {
                return;
            }

            float multiplier = PresetReductionMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            DamageParameters parameters = damage.Parameters;
            float original = parameters.PoiseDamage;
            parameters.PoiseDamage = Mathf.Max(0.0f, original * multiplier);
            damage.Parameters = parameters;
            state = new PoisePatchState
            {
                Damage = damage,
                OriginalPoiseDamage = original
            };
            LogDifficultyDiagnostic("PlayerPoiseDamageDealt", original, parameters.PoiseDamage, multiplier);
        }

        private void RestorePlayerPoiseDamage(PoisePatchState state)
        {
            if (state == null || state.Damage == null)
            {
                return;
            }

            DamageParameters parameters = state.Damage.Parameters;
            parameters.PoiseDamage = state.OriginalPoiseDamage;
            state.Damage.Parameters = parameters;
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

        private void EvaluateCompatibilityOverlaps()
        {
            try
            {
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

        private void EvaluateCustomDifficultyOverlap()
        {
            BaseUnityPlugin plugin;
            if (!TryGetEnabledPlugin(CustomDifficultyPluginGuid, out plugin))
            {
                return;
            }

            List<string> conflicts = new List<string>();
            AddConflictIf(
                conflicts,
                OutgoingDamageModifierIsEffective()
                    && ExternalFloatIsNonNeutral(plugin, "DamageDealtMultipliers", "PlayerDamageDealtMultiplier", 1.0f),
                "ModifyPlayerDamageDealt");
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
            AddConflictIf(conflicts, PlayerPressureModifierIsEffective(_modifyPlayerDamageTaken) && incomingOverlap, "ModifyPlayerDamageTaken");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyStaminaUsage) && staminaOverlap, "ModifyStaminaUsage");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyManaUsage) && manaOverlap, "ModifyManaUsage");
            AddConflictIf(conflicts, EnemySightRangeModifierIsEffective() && sightOverlap, "ModifyEnemySightRange");
            AddConflictIf(conflicts, EnemyHearingRangeModifierIsEffective() && hearingOverlap, "ModifyEnemyHearingRange");
            AddConflictIf(conflicts, EnemyAggroPersistenceModifierIsEffective() && persistenceOverlap, "ModifyEnemyAggroPersistence");
            AddConflictIf(conflicts, ConsumableRecoveryModifierIsEffective() && consumableOverlap, "ModifyConsumableRecovery");
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
            bool staminaOverlap;
            bool slotOverlap;
            bool recoveryOverlap;
            bool poiseOverlap;
            bool armorOverlap;

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
            }

            List<string> conflicts = new List<string>();
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyStaminaUsage) && staminaOverlap, "ModifyStaminaUsage");
            AddConflictIf(conflicts, AttackSlotsModifierIsEffective() && slotOverlap, "ModifyEnemyAttackSlots");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyEnemyAttackRecovery) && recoveryOverlap, "ModifyEnemyAttackRecovery");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyPlayerPoiseDamageDealt) && poiseOverlap, "ModifyPlayerPoiseDamageDealt");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyArmorWeightPenalties) && armorOverlap, "ModifyArmorWeightPenalties");
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

        private bool ConsumableRecoveryModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyConsumableRecovery)
                && !ApproximatelyNeutral(PresetConsumableRecoveryMultiplier());
        }

        private bool FoodRecoveryModifierIsEffective()
        {
            return DifficultyModifierIsEnabled(_modifyFoodRecovery)
                && (!ApproximatelyNeutral(PresetFoodHealthRateMultiplier())
                    || !ApproximatelyNeutral(PresetFoodHealthDurationMultiplier())
                    || PresetFoodStaminaRate() > NeutralTolerance);
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
                    + ". Disable those settings to prevent stacking.");
            }

            if (_notifiedOverlapSignatures.Contains(signature))
            {
                return;
            }

            NotificationBuffer buffer = World.Any<NotificationBuffer>();
            if (buffer == null)
            {
                return;
            }

            string message;
            if (string.Equals(pluginName, "Tainted Combat", StringComparison.Ordinal))
            {
                message = "Overlapping combat modifiers are active with Tainted Combat. See the BepInEx log for the settings to disable.";
            }
            else if (string.Equals(pluginName, "Flat Arrows", StringComparison.Ordinal))
            {
                message = "Overlapping player-arrow modifiers are active with Flat Arrows. See the BepInEx log for the settings to disable.";
            }
            else if (string.Equals(pluginName, "HarderLife", StringComparison.Ordinal))
            {
                message = "Overlapping difficulty modifiers are active with HarderLife. See the BepInEx log for the settings to disable.";
            }
            else if (string.Equals(pluginName, "Tainted Instincts", StringComparison.Ordinal))
            {
                message = "Overlapping enemy modifiers are active with Tainted Instincts. See the BepInEx log for the settings to disable.";
            }
            else
            {
                message = "Overlapping modifiers are active with Custom Difficulty. See the BepInEx log for the settings to disable.";
            }
            if (buffer.PushNotification(PluginName, null, string.Empty, message, null, false) != null)
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
                    __state = null;
                    plugin.Warn("Could not capture restorative consumable recovery: " + ex.GetBaseException().Message);
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
                    plugin.Warn("Could not apply restorative consumable recovery: " + ex.GetBaseException().Message);
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
                ref FoodSkillOverrideState __state)
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
                        && item.IsEdible
                        && !item.Template.IsPotion
                        && hero != null)
                    {
                        __state = plugin.ApplyFoodSkillOverrides(item, hero);
                    }
                }
                catch (Exception ex)
                {
                    RestoreFoodSkillOverrides(__state);
                    __state = null;
                    plugin.Warn("Could not prepare live food tooltip values: " + ex.GetBaseException().Message);
                }
            }

            public static void Postfix(
                ref string __result,
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
                    __result = plugin.AppendFoodStaminaDescription(__state, __result);
                }
                catch (Exception ex)
                {
                    plugin.Warn("Could not finish live food tooltip values: " + ex.GetBaseException().Message);
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
                        plugin.Warn("Could not restore temporary food tooltip values: " + ex.GetBaseException().Message);
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
    }
}
