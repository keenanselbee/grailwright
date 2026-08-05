using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using Awaken.TG.Main.AI.Combat.Utils;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.MVC;
using Awaken.TG.Main.Animations.FSM.Npc.Machines;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Stats.Tweaks;
using Awaken.TG.Main.Settings.Gameplay;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Quests.Objectives;
using Awaken.TG.Main.UI.HUD.Notifications;
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
        private const string TaintedCombatPluginGuid = "kane.tgfoa.tainted-combat";
        private const string TaintedInstinctsPluginGuid = "kane.tgfoa.tainted-instincts";
        private const float NeutralTolerance = 0.0001f;
        private const float DifficultyRefreshIntervalSeconds = 1.0f;

        private ConfigEntry<bool> _difficultyModifiersEnabled;
        private ConfigEntry<bool> _modifyPlayerDamageDealt;
        private ConfigEntry<float> _playerDamageDealtMultiplier;
        private ConfigEntry<bool> _modifyPlayerDamageTaken;
        private ConfigEntry<bool> _modifyStaminaUsage;
        private ConfigEntry<bool> _modifyManaUsage;
        private ConfigEntry<bool> _modifyPlayerPoiseDamageDealt;
        private ConfigEntry<bool> _modifyPlayerArrowVelocity;
        private ConfigEntry<bool> _modifyArmorWeightPenalties;
        private ConfigEntry<bool> _modifyLightArmorMobility;
        private ConfigEntry<bool> _modifyArmorPhysicalProtection;
        private ConfigEntry<bool> _modifyEnemyAttackSlots;
        private ConfigEntry<int> _enemyAttackSlotCap;
        private ConfigEntry<bool> _modifyEnemyAttackRecovery;
        private ConfigEntry<bool> _modifyHostileArrowVelocity;
        private ConfigEntry<bool> _modifyEnemySightRange;
        private ConfigEntry<bool> _modifyKillExperience;
        private ConfigEntry<bool> _modifyQuestExperience;
        private ConfigEntry<bool> _modifyProficiencyExperience;

        private readonly HashSet<string> _loggedOverlapSignatures = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _notifiedOverlapSignatures = new HashSet<string>(StringComparer.Ordinal);
        private Hero _difficultyTweakHero;
        private float _nextDifficultyRefreshAt;
        private bool _resourceStatsPatchAvailable;
        private bool _reportedEnemySightRefreshFailure;

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

        private sealed class PoisePatchState
        {
            internal Damage Damage;
            internal float OriginalPoiseDamage;
        }

        private void BindDifficultyConfig()
        {
            _difficultyModifiersEnabled = Config.Bind(
                "1. Core",
                "DifficultyModifiersEnabled",
                true,
                "Master switch for Steel and Bone's global damage, resource, armor, projectile, enemy-awareness, enemy-pressure, poise, and experience modifiers. Material matchup rules remain active when this is disabled.");

            _modifyPlayerDamageDealt = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerDamageDealt",
                true,
                "Apply PlayerDamageDealtMultiplier to health damage dealt by the player. This value is independent from the preset.");
            _playerDamageDealtMultiplier = Config.Bind(
                "6. Difficulty - Player",
                "PlayerDamageDealtMultiplier",
                1.0f,
                new ConfigDescription(
                    "Health-damage multiplier for outgoing player damage. 1 is neutral and this value is not changed by presets.",
                    new AcceptableValueRange<float>(0.25f, 3.0f)));
            _modifyPlayerDamageTaken = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerDamageTaken",
                true,
                "Increase health damage taken from all routed damage sources by 0%, 5%, or 10% according to the preset.");
            _modifyStaminaUsage = Config.Bind(
                "6. Difficulty - Player",
                "ModifyStaminaUsage",
                true,
                "Increase player stamina usage by 0%, 5%, or 10% according to the preset.");
            _modifyManaUsage = Config.Bind(
                "6. Difficulty - Player",
                "ModifyManaUsage",
                true,
                "Increase player mana usage by 0%, 5%, or 10% according to the preset.");
            _modifyPlayerPoiseDamageDealt = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerPoiseDamageDealt",
                true,
                "Reduce poise damage dealt by the player by 0%, 5%, or 10% according to the preset, making enemies slightly harder to stagger-lock.");
            _modifyPlayerArrowVelocity = Config.Bind(
                "6. Difficulty - Player",
                "ModifyPlayerArrowVelocity",
                true,
                "Multiply player-fired arrow velocity by 1.10, 1.30, or 1.50 according to the preset. This velocity setting does not alter damage; ArrowMaterialRulesEnabled controls the separate material matchup.");
            _modifyArmorWeightPenalties = Config.Bind(
                "6. Difficulty - Player",
                "ModifyArmorWeightPenalties",
                true,
                "Multiply the game's native armor-weight penalties by 1.00, 1.05, or 1.10 according to the preset. Existing armor proficiency still softens eligible penalties.");
            _modifyLightArmorMobility = Config.Bind(
                "6. Difficulty - Player",
                "ModifyLightArmorMobility",
                true,
                "Increase movement speed while in the game's Light armor tier by 0%, 2.5%, or 5% according to the preset.");
            _modifyArmorPhysicalProtection = Config.Bind(
                "6. Difficulty - Player",
                "ModifyArmorPhysicalProtection",
                true,
                "Multiply physical armor in Medium by 1.00/1.05/1.10 and in Heavy or Overload by 1.00/1.10/1.20 according to the preset. Magical armor checks are unchanged.");

            _modifyEnemyAttackSlots = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyEnemyAttackSlots",
                true,
                "Add 0, 1, or 2 simultaneous enemy attack slots to the current game difficulty according to the preset.");
            _enemyAttackSlotCap = Config.Bind(
                "7. Difficulty - Enemies",
                "EnemyAttackSlotCap",
                6,
                new ConfigDescription(
                    "Safety cap for slots added by Steel and Bone. This never lowers a higher value supplied by the game or another mod.",
                    new AcceptableValueRange<int>(1, 12)));
            _modifyEnemyAttackRecovery = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyEnemyAttackRecovery",
                true,
                "Shorten the delay before enemies release attack slots by 0%, 5%, or 10% according to the preset.");
            _modifyHostileArrowVelocity = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyHostileArrowVelocity",
                true,
                "Multiply hostile NPC arrow velocity by 1.10, 1.30, or 1.50 according to the preset while preserving the game's ballistic aim calculation. Hostile arrow damage is unchanged.");
            _modifyEnemySightRange = Config.Bind(
                "7. Difficulty - Enemies",
                "ModifyEnemySightRange",
                true,
                "Multiply the native sight distance of active hostile NPCs by 1.10, 1.30, or 1.50 according to the preset. Line of sight, visibility, alert behavior, and authored perception distances remain native.");

            _modifyKillExperience = Config.Bind(
                "8. Difficulty - Progression",
                "ModifyKillExperience",
                true,
                "Reduce experience gained from enemy kills by 0%, 5%, or 10% according to the preset.");
            _modifyQuestExperience = Config.Bind(
                "8. Difficulty - Progression",
                "ModifyQuestExperience",
                true,
                "Reduce experience gained from quest and objective rewards by 0%, 5%, or 10% according to the preset.");
            _modifyProficiencyExperience = Config.Bind(
                "8. Difficulty - Progression",
                "ModifyProficiencyExperience",
                true,
                "Reduce proficiency experience by 0%, 5%, or 10% according to the preset.");
        }

        private void InitializeDifficultyOverhaul()
        {
            Config.SettingChanged += OnDifficultySettingChanged;
            ReapplyDifficultyStatTweaks();
            RefreshEnemySightRangeTweaks();
            EvaluateCompatibilityOverlaps();
        }

        private void ShutdownDifficultyOverhaul()
        {
            Config.SettingChanged -= OnDifficultySettingChanged;
            RemoveDifficultyStatTweaks(Hero.Current);
            RemoveAllEnemySightRangeTweaks();
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
            RefreshEnemySightRangeTweaks();

            EvaluateCompatibilityOverlaps();
        }

        private void OnDifficultySettingChanged(object sender, SettingChangedEventArgs args)
        {
            ReapplyDifficultyStatTweaks();
            RefreshEnemySightRangeTweaks();
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
                AccessTools.Method(typeof(Hero), "TotalArmor", new[] { typeof(DamageSubType) }),
                typeof(HeroPhysicalArmorPatch),
                nameof(HeroPhysicalArmorPatch.Postfix),
                "Hero.TotalArmor",
                "armor physical-protection modifier");
            PatchHostileArrowVelocity();
            PatchPoiseDamage();
        }

        private void PatchHostileArrowVelocity()
        {
            MethodInfo original = AccessTools.Method(
                typeof(CombatBehaviourUtils),
                "FireProjectile",
                new[] { typeof(CombatBehaviourUtils.FireProjectileParams), typeof(VGUtils.ShootParams) });
            MethodInfo prefix = AccessTools.Method(typeof(HostileArrowVelocityPatch), nameof(HostileArrowVelocityPatch.Prefix));
            MethodInfo transpiler = AccessTools.Method(typeof(HostileArrowVelocityPatch), nameof(HostileArrowVelocityPatch.Transpiler));
            MethodInfo finalizer = AccessTools.Method(typeof(HostileArrowVelocityPatch), nameof(HostileArrowVelocityPatch.Finalizer));
            if (original == null || prefix == null || transpiler == null || finalizer == null)
            {
                Warn("Could not patch CombatBehaviourUtils.FireProjectile; the hostile arrow-velocity modifier is disabled.");
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
                LogDiagnostic("Patched CombatBehaviourUtils.FireProjectile for the hostile arrow-velocity modifier.");
            }
            catch (Exception ex)
            {
                Warn("Could not patch CombatBehaviourUtils.FireProjectile; the hostile arrow-velocity modifier is disabled. " + ex.GetBaseException().Message);
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
            if (!DifficultyModifierIsEnabled(_modifyPlayerDamageDealt)
                || _playerDamageDealtMultiplier == null)
            {
                return;
            }

            float multiplier = Clamp(_playerDamageDealtMultiplier.Value, 0.25f, 3.0f);
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = damageModifier;
            damageModifier *= multiplier;
            LogDifficultyDiagnostic("PlayerDamageDealt", before, damageModifier, multiplier);
        }

        private void ApplyIncomingHealthDamageModifier(ref float damageModifier)
        {
            if (!DifficultyModifierIsEnabled(_modifyPlayerDamageTaken))
            {
                return;
            }

            float multiplier = PresetCostMultiplier();
            if (ApproximatelyNeutral(multiplier))
            {
                return;
            }

            float before = damageModifier;
            damageModifier *= multiplier;
            LogDifficultyDiagnostic("PlayerDamageTaken", before, damageModifier, multiplier);
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

        private float HostileArrowVelocityMultiplier(VGUtils.ShootParams shootParams)
        {
            if (!DifficultyModifierIsEnabled(_modifyHostileArrowVelocity)
                || shootParams.shooter == null
                || Hero.Current == null
                || !ReferenceEquals(shootParams.projectileSlotType, EquipmentSlotType.Quiver)
                || !shootParams.shooter.IsHostileTo(Hero.Current))
            {
                return 1.0f;
            }

            return PresetArrowVelocityMultiplier();
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

            float multiplier = PresetReductionMultiplier();
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

            float multiplier = PresetReductionMultiplier();
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

            float multiplier = PresetReductionMultiplier();
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

        private void RefreshEnemySightRangeTweaks()
        {
            try
            {
                foreach (NpcElement npc in World.All<NpcElement>())
                {
                    ApplyEnemySightRangeTweak(npc);
                }
                _reportedEnemySightRefreshFailure = false;
            }
            catch (Exception ex)
            {
                if (!_reportedEnemySightRefreshFailure)
                {
                    _reportedEnemySightRefreshFailure = true;
                    Warn("Could not refresh enemy sight-range modifiers: " + ex.GetBaseException().Message);
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
                PresetModifierIsEffective(_modifyPlayerDamageTaken)
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
                PresetModifierIsEffective(_modifyKillExperience)
                    && ExternalFloatIsNonNeutral(plugin, "ExpMultipliers", "KillExpMultiplier", 1.0f),
                "ModifyKillExperience");
            AddConflictIf(
                conflicts,
                PresetModifierIsEffective(_modifyQuestExperience)
                    && ExternalFloatIsNonNeutral(plugin, "ExpMultipliers", "QuestExpMultiplier", 1.0f),
                "ModifyQuestExperience");
            AddConflictIf(
                conflicts,
                PresetModifierIsEffective(_modifyProficiencyExperience)
                    && CustomDifficultyChangesProficiencyExperience(plugin),
                "ModifyProficiencyExperience");
            ReportCompatibilityOverlap("Custom Difficulty", conflicts);
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

            List<string> conflicts = new List<string>();
            AddConflictIf(conflicts, DifficultyModifierIsEnabled(_modifyEnemySightRange) && sightOverlap, "ModifyEnemySightRange");
            AddConflictIf(conflicts, PresetModifierIsEffective(_modifyPlayerDamageTaken) && damageOverlap, "ModifyPlayerDamageTaken");
            AddConflictIf(conflicts, AttackSlotsModifierIsEffective() && slotOverlap, "ModifyEnemyAttackSlots");
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
            return DifficultyModifierIsEnabled(_modifyPlayerDamageDealt)
                && _playerDamageDealtMultiplier != null
                && !ApproximatelyNeutral(_playerDamageDealtMultiplier.Value);
        }

        private bool PresetModifierIsEffective(ConfigEntry<bool> setting)
        {
            return DifficultyModifierIsEnabled(setting) && PresetPenaltyAmount() > NeutralTolerance;
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

        private static class HostileArrowVelocityPatch
        {
            [ThreadStatic]
            private static float _velocityMultiplier;

            public static void Prefix(VGUtils.ShootParams shootParams)
            {
                SteelAndBonePlugin plugin = Instance;
                _velocityMultiplier = plugin == null ? 1.0f : plugin.HostileArrowVelocityMultiplier(shootParams);
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> patched = new List<CodeInstruction>();
                MethodInfo clamp = AccessTools.Method(
                    typeof(Mathf),
                    nameof(Mathf.Clamp),
                    new[] { typeof(float), typeof(float), typeof(float) });
                MethodInfo scale = AccessTools.Method(typeof(HostileArrowVelocityPatch), nameof(ScaleBallisticVelocity));
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
