using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Awaken.TG.Assets;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Attachments.Elements;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Tooltips;
using Awaken.TG.Main.Heroes.Items.Weapons;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using FMODUnity;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[assembly: AssemblyTitle("Blood Magic Expansion")]
[assembly: AssemblyDescription("Blood Transfusion and Life Transfusion corpse rituals, live drain rewards, and corpse-fed Blood Essence progression for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Blood Magic Expansion")]
[assembly: AssemblyVersion("3.2.6.0")]
[assembly: AssemblyFileVersion("3.2.6.0")]

namespace BloodMagicExpansion
{
    public enum BloodMagicFocusedCorpseState
    {
        None = 0,
        Usable = 1,
        Channeling = 2,
        Spent = 3,
        Blocked = 4
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(GrailFloatingTextPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(FirstPersonArmsAdjusterPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(DeedsOfAvalonPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(EyesInTheDarkPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(VersatileWeaponsPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BloodMagicExpansionPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.blood-magic-expansion";
        public const string PluginName = "Blood Magic Expansion";
        public const string PluginVersion = "3.2.6";
        private const int ConfigSchemaVersion = 30;
        private const int ConfigRecoveryBaselineSchema = 10;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new[]
                {
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        14,
                        "2. Blood Spell Inner Light",
                        "MaximumPowerBrightnessMultiplier",
                        "The progression now starts at baseline brightness and reaches a recalibrated 2x multiplier at Blood Power 100."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        14,
                        "2. Blood Spell Inner Light",
                        "MaximumPowerRangeMultiplier",
                        "The progression now treats this as the Power 100 multiplier over the baseline Range."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        14,
                        "2. Blood Spell Inner Light",
                        "Range",
                        "Range now defines the Blood Power 0 baseline and is recalibrated to 3 meters."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        19,
                        "Blood Spell Inner Light",
                        "MaximumPowerBrightnessMultiplier",
                        "This setting now defines the Power 200 brightness milestone rather than the Power 100 milestone.")
                };
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
        {
            new ConfigDefinition("Diagnostics", "OverrideBloodEssence"),
            new ConfigDefinition("Diagnostics", "BloodEssenceOverrideValue")
        };
        private const float CacheCleanupIntervalSeconds = 30f;
        private const float CompletedCorpseRetentionSeconds = 120f;
        private const float ExpiredStrongCastRetentionSeconds = 5f;
        private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string DeedsOfAvalonPluginGuid = "ks.tgfoa.deeds-of-avalon";
        private const string DeedsOfAvalonApiTypeName = "DeedsOfAvalon.StatisticsApi";
        private const string EyesInTheDarkPluginGuid = "ks.tgfoa.eyes-in-the-dark";
        private const string EyesInTheDarkCorpseDrainApiTypeName =
            "EyesInTheDark.EyesInTheDarkCorpseDrainApi";
        private const string BloodProgressionMemoryContext = "BloodMagicExpansion";
        private const string BloodProgressionInitializedKey = "progression.initialized";
        private const string BloodProgressionEssenceKey = "progression.essence";
        private const string BloodProgressionCorpseCountKey = "progression.corpses_drained";
        private const string BloodProgressionCorpseStatisticsInitializedKey =
            "progression.corpse_statistics_initialized";
        private const string BloodProgressionMeagerCorpseCountKey =
            "progression.corpses_drained.meager";
        private const string BloodProgressionWorthyCorpseCountKey =
            "progression.corpses_drained.worthy";
        private const string BloodProgressionPotentCorpseCountKey =
            "progression.corpses_drained.potent";
        private const string BloodProgressionPrimeCorpseCountKey =
            "progression.corpses_drained.prime";
        private const string BloodProgressionCorpseQualitySumKey =
            "progression.corpses_drained.quality_sum";
        private const string BloodRitualSeverityKeyPrefix = "ritual.exsanguination.";
        private const float NormalMaximumBloodPower = 100.0f;
        private const float AbsoluteMaximumBloodPower = 200.0f;
        private const float BloodEssenceAtNormalMaximumPower = 1000.0f;
        private const float BloodEssenceAtAbsoluteMaximumPower = 5000.0f;
        private const float MaximumOvermasteryBonusFraction = 1.0f;
        private const float BloodSpellProjectileTravelProgressionBase = 1.06f;
        private const float BloodSpellTapDamageProgressionBase = 1.06f;
        private const float BloodSpellHomingSearchProgressionBase = 1.05f;
        private const float BloodSpellHeldRangeProgressionBase = 1.03f;
        private const float BloodSpellBleedProgressionBase = 1.06f;
        private const float BloodSpellTapSpeedProgressionBase = 1.06f;
        private const float BloodSpellHeldSpeedProgressionBase = 1.01f;
        private const float AbhartachExplosionDamageProgressionBase = 1.05f;
        private const float AbhartachExplosionRadiusProgressionBase = 1.10f;
        private const float AbhartachExplosionBleedProgressionBase = 1.12f;
        private const float AbhartachHeldHealingProgressionBase = 1.20f;
        private const float AbhartachCorpseSearchProgressionBase = 1.05f;
        private const float MeagerBloodEssenceAward = 1.0f;
        private const float WorthyBloodEssenceAward = 3.0f;
        private const float PotentBloodEssenceAward = 5.0f;
        private const float PrimeBloodEssenceAward = 10.0f;
        private const float BloodProgressionSyncIntervalSeconds = 1.0f;
        private const string FirstPersonArmsAdjusterPluginGuid = "ks.tgfoa.first-person-arms-adjuster";
        private const string FirstPersonArmsAdjusterApiTypeName = "FirstPersonArmsAdjuster.FirstPersonArmsAdjusterApi";
        private const string VersatileWeaponsPluginGuid = "ks.tgfoa.versatile-weapons";
        private const string VersatileWeaponsApiTypeName = "VersatileWeapons.VersatileWeaponsApi";
        private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";
        private const string SoulAndServicePluginGuid = "ks.tgfoa.soul-and-service";
        private const string SoulAndServiceApiTypeName = "SoulAndService.SoulAndServiceApi";
        private const string GrailFloatingTextCorpseXpEventId = "blood-magic-corpse-xp";
        private const string GrailFloatingTextLiveDrainXpEventId = "blood-magic-live-drain-xp";
        private const string GrailFloatingTextDefaultHealingEventId = "default-healed";
        private const string GrailFloatingTextBloodHealingEventId = "blood-magic-healed";
        private const string GrailFloatingTextBloodHealingStyle = "Red";
        private const string GrailFloatingTextBloodHealingIconId = "magic_blood";
        private const string GrailFloatingTextShortDurationBucket = "Short";
        private const string GrailFloatingTextBloodPowerStyle = "Red";
        private const string GrailFloatingTextBloodPowerIconId = "magic_blood";
        private const string GrailFloatingTextBloodPowerDurationBucket = "Medium";
        private static readonly BloodPowerMilestone[] BloodPowerMilestones =
        {
            new BloodPowerMilestone(25.0f, "blood-magic-power-25", "Blood Power rises: Your blood arts gather strength."),
            new BloodPowerMilestone(50.0f, "blood-magic-power-50", "Blood Power rises: Your command of blood magic deepens."),
            new BloodPowerMilestone(75.0f, "blood-magic-power-75", "Blood Power rises: Your blood rites answer with growing force."),
            new BloodPowerMilestone(100.0f, "blood-magic-power-100", "Blood Power rises: Your blood arts reach a new height."),
            new BloodPowerMilestone(125.0f, "blood-magic-power-125", "Blood Power rises: Your blood arts surpass their former limits."),
            new BloodPowerMilestone(150.0f, "blood-magic-power-150", "Blood Power rises: Your command of blood magic grows formidable."),
            new BloodPowerMilestone(175.0f, "blood-magic-power-175", "Blood Power rises: Your blood arts approach their peak."),
            new BloodPowerMilestone(200.0f, "blood-magic-power-200", "Blood Power rises: Your command of blood magic reaches its apex.")
        };
        private const float LiveDrainHealingEligibilitySeconds = 0.25f;
        private const string BloodSpellInnerLightMainHandObjectName = "BloodMagicExpansionMainHandLight";
        private const string BloodSpellInnerLightOffHandObjectName = "BloodMagicExpansionOffHandLight";
        private const float BloodSpellInnerLightAnchorRetryIntervalSeconds = 0.5f;
        private const float BloodSpellInnerLightDiagnosticIntervalSeconds = 2.0f;
        private const float BloodSpellTuningDiagnosticIntervalSeconds = 1.0f;
        private const float BloodSpellInnerLightMinimumIntensity = 0.001f;
        private const float BloodSpellInnerLightHdrpIntensityMultiplier = 50000.0f;
        private const float BloodSpellInnerLightCastBoostMultiplier = 3.0f;
        private const float BloodSpellInnerLightCastBoostStartDelaySeconds = 0.3f;
        private const float BloodSpellInnerLightCastBoostRampUpSeconds = 0.01f;
        private const float BloodSpellInnerLightCastBoostRampDownSeconds = 0.25f;
        private const float BloodSpellInnerLightCastBoostFinishLeadSeconds = 0.5f;
        private const float BloodSpellInnerLightReadyGraceSeconds = 0.75f;
        private const int CorpseLeechTierSoundSlots = 5;
        private const string CorpseLeechLowTier = "low";
        private const string CorpseLeechMediumTier = "medium";
        private const string CorpseLeechHighTier = "high";
        private const string CorpseLeechMaxTier = "max";
        private const string CorpseRitualLesserVfxKey =
            "d858e5e33ccd9ec4ea9b3099ee02d32e";
        private const string CorpseRitualGreaterVfxKey =
            "bfa9aa86addeec347877ffb0fc0b4315";
        private const float CorpseLeechMaximumRangeDistance = 30.0f;
        private const float CorpseLeechMinimumRangeVolume = 0.10f;
        private const float ServantTargetToleranceRadius = 0.15f;
        private const float ServantTargetGraceSeconds = 0.18f;
        private const int ServantTargetHitCapacity = 24;
        private const float CorpseTargetAssistRadius = 0.4f;
        private const int CorpseTargetAssistColliderCapacity = 24;
        private const string CorpseQualityMeagerLabel = "Meager";
        private const string CorpseQualityWorthyLabel = "Worthy";
        private const string CorpseQualityPotentLabel = "Potent";
        private const string CorpseQualityPrimeLabel = "Prime";
        private const float CorpseLeechMeagerQualityMax =
            Grailwright.Shared.CorpseQualityBuckets.MeagerMaximumQuality;
        private const float CorpseLeechWorthyQualityMax =
            Grailwright.Shared.CorpseQualityBuckets.WorthyMaximumQuality;
        private const float CorpseLeechPotentQualityMax =
            Grailwright.Shared.CorpseQualityBuckets.PotentMaximumQuality;

        private const string HealthElementTypeName = "Awaken.TG.Main.Character.HealthElement";
        private const string MagicFsmTypeName = "Awaken.TG.Main.Animations.FSM.Heroes.Machines.MagicFSM";
        private const string HeroAnimatorSubstateMachineTypeName = "Awaken.TG.Main.Animations.FSM.Heroes.Base.HeroAnimatorSubstateMachine";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";
        private const string InventoryExtensionsTypeName = "Awaken.TG.Main.Character.CharacterInventoryExtension";
        private const string EquipmentSlotTypeName = "Awaken.TG.Main.Heroes.Items.EquipmentSlotType";
        private const string NpcElementTypeName = "Awaken.TG.Main.Fights.NPCs.NpcElement";
        private const string CharacterTypeName = "Awaken.TG.Main.Character.ICharacter";
        private const string CorpseTypeName = "Awaken.TG.Main.Locations.Attachments.Elements.Corpse";
        private const string CharacterStatusesTypeName = "Awaken.TG.Main.Heroes.Statuses.CharacterStatuses";
        private const string BuildupStatusTypeName = "Awaken.TG.Main.Heroes.Statuses.BuildUp.BuildupStatus";
        private const string GameConstantsTypeName = "Awaken.TG.Main.General.Configs.GameConstants";
        private const string DamageUtilsTypeName = "Awaken.TG.Main.Fights.DamageInfo.DamageUtils";
        private const string HealingUtilsTypeName = "Awaken.TG.Main.Fights.Utils.HealingUtils";
        private const string DamageDealingProjectileTypeName = "Awaken.TG.Main.AI.Fights.Projectiles.DamageDealingProjectile";
        private const string MagicProjectileTypeName = "Awaken.TG.Main.AI.Fights.Projectiles.MagicProjectile";
        private const string FindAlivesTypeName = "Awaken.TG.Main.Skills.Units.Effects.FindAlives";
        private const string FindDeadBodiesTypeName = "Awaken.TG.Main.Skills.Units.Effects.FindDeadBodies";
        private const string HealFromDeadBodiesTypeName = "Awaken.TG.Main.Skills.Units.Effects.HealFromDeadBodies";
        private const string SkillUnitsTypeName = "Awaken.TG.Main.Skills.SkillUnits";

        internal static BloodMagicExpansionPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private BleedSkillGraphPreloader _bleedSkillGraphPreloader;
        private VCHeroRaycaster _heroRaycaster;
        private bool _heroViewRayFailureLogged;
        private bool _applyingPreset;
        private bool _foaModManagerRefreshPending;
        private ConfigFile _resolvedConfig;
        private MethodInfo _grailFloatingTextTryClaimXpGainMethod;
        private MethodInfo _grailFloatingTextTryClaimConsolidatedXpGainMethod;
        private MethodInfo _grailFloatingTextTryCancelXpGainClaimMethod;
        private MethodInfo _grailFloatingTextTrySetBuiltInEventClaimMethod;
        private MethodInfo _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod;
        private MethodInfo _grailFloatingTextTryShowEventMethod;
        private MethodInfo _deedsOfAvalonRecordCorpseDrainMethod;
        private MethodInfo _deedsOfAvalonSetCorpseDrainStatisticsMethod;
        private MethodInfo _deedsOfAvalonRecordBloodMagicProgressionMethod;
        private MethodInfo _deedsOfAvalonRecordBloodMagicEssenceMethod;
        private MethodInfo _deedsOfAvalonGetCorpseDrainCountsMethod;
        private MethodInfo _deedsOfAvalonGetCorpseDrainStatisticsMethod;
        private bool _deedsOfAvalonBridgeResolved;
        private bool _deedsOfAvalonFailureLogged;
        private MethodInfo _eyesInTheDarkRegisterCorpseDrainMethod;
        private bool _eyesInTheDarkBridgeResolved;
        private bool _eyesInTheDarkFailureLogged;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _preloadBleedSkillGraphs;
        private ConfigEntry<Preset> _preset;
        private ConfigEntry<float> _customPayoutPercentOfKillXp;
        private ConfigEntry<float> _secondsRequired;
        private ConfigEntry<HandRequirement> _handRequirement;
        private ConfigEntry<float> _singleHandPayoutMultiplier;
        private ConfigEntry<float> _fallbackKillXp;
        private ConfigEntry<float> _minimumXpToPay;
        private ConfigEntry<float> _maximumXp;
        private ConfigEntry<float> _roundXpTo;
        private ConfigEntry<bool> _requireTargetXpRewardAllowedWhenPresent;

        private ConfigEntry<bool> _requireBloodPlausible;
        private ConfigEntry<string> _bloodlessBlacklistTerms;
        private ConfigEntry<string> _bloodWhitelistTerms;

        private ConfigEntry<string> _bloodTransfusionMatchTerms;
        private ConfigEntry<string> _bloodTransfusionTemplateGuid;

        private ConfigEntry<bool> _awardCharacterXp;
        private ConfigEntry<float> _rawCharacterXpPerCorpseXp;
        private ConfigEntry<bool> _announceRawCharacterXp;
        private ConfigEntry<bool> _healCharacter;
        private ConfigEntry<float> _healMaxHealthPercentPerXpPercent;
        private ConfigEntry<HealingPowerScalingMode> _healPowerScalingMode;
        private ConfigEntry<float> _healReferenceTargetMaxHealth;
        private ConfigEntry<float> _healPowerExponent;
        private ConfigEntry<float> _healMinimumPowerScale;
        private ConfigEntry<float> _healMaximumPowerScale;
        private ConfigEntry<bool> _playCorpseLeechSound;
        private ConfigEntry<float> _corpseLeechSoundVolume;
        private ConfigEntry<float> _corpseLeechSoundRangeVolume;
        private ConfigEntry<bool> _avoidRecentCorpseLeechRepeats;
        private ConfigEntry<int> _recentCorpseLeechSoundMemory;
        private ConfigEntry<float> _corpseLeechRandomPitchSemitones;
        private ConfigEntry<bool> _corpseQualityScaleTransfusionHealing;
        private ConfigEntry<bool> _corpseQualityScaleAbhartachEffects;
        private ConfigEntry<float> _corpseQualityMinimumEffectMultiplier;
        private ConfigEntry<float> _corpseQualityMaximumEffectMultiplier;
        private ConfigEntry<float> _abhartachRadiusMinimumQualityMultiplier;
        private ConfigEntry<float> _abhartachRadiusMaximumQualityMultiplier;
        private ConfigEntry<float> _abhartachHealingMinimumQualityMultiplier;
        private ConfigEntry<float> _abhartachHealingMaximumQualityMultiplier;
        private ConfigEntry<float> _corpseQualityEffectMemorySeconds;
        private ConfigEntry<float> _corpseQualityFallbackQuality;

        private ConfigEntry<bool> _liveDrainEnabled;
        private ConfigEntry<bool> _liveDrainAwardCharacterXp;
        private ConfigEntry<float> _liveDrainRawCharacterXpMultiplier;
        private ConfigEntry<float> _liveDrainHealingMultiplier;
        private ConfigEntry<float> _customLiveDrainXpTickIntervalSeconds;
        private ConfigEntry<float> _customLiveDrainXpPercentPerTick;
        private ConfigEntry<float> _customLiveDrainMaximumXpPercentPerTarget;

        private ConfigEntry<bool> _bloodSpellTuningEnabled;
        private ConfigEntry<bool> _bloodSpellInnerLightEnabled;
        private ConfigEntry<float> _bloodSpellInnerLightIntensity;
        private ConfigEntry<float> _bloodSpellInnerLightBloodTransfusionIntensityMultiplier;
        private ConfigEntry<float> _bloodSpellInnerLightLifeTransfusionIntensityMultiplier;
        private ConfigEntry<float> _bloodSpellInnerLightAbhartachCallingIntensityMultiplier;
        private ConfigEntry<float> _bloodSpellInnerLightInteriorIntensityMultiplier;
        private ConfigEntry<float> _bloodSpellInnerLightMinimumPowerBrightnessMultiplier;
        private ConfigEntry<float> _bloodSpellInnerLightMasteryBrightnessMultiplier;
        private ConfigEntry<float> _bloodSpellInnerLightMaximumPowerBrightnessMultiplier;
        private ConfigEntry<float> _bloodSpellInnerLightMinimumPowerRange;
        private ConfigEntry<float> _bloodSpellInnerLightMasteryRange;
        private ConfigEntry<float> _bloodSpellInnerLightMaximumPowerRange;
        private ConfigEntry<float> _bloodSpellInnerLightFadeSeconds;
        private ConfigEntry<bool> _bloodSpellScaleProjectileTravel;
        private ConfigEntry<bool> _bloodSpellScaleHomingTargetSearch;
        private ConfigEntry<bool> _bloodSpellScaleHeldTargetRange;
        private ConfigEntry<float> _bloodSpellHomingTargetSearchMaximumMultiplier;
        private ConfigEntry<float> _bloodSpellHeldTargetRangeMaximumMultiplier;
        private ConfigEntry<bool> _bloodSpellScaleBleedDuration;
        private ConfigEntry<float> _bloodSpellMaximumBleedDurationMultiplier;
        private ConfigEntry<string> _bloodSpellProjectileTravelBloodPowerBonusCurve;
        private ConfigEntry<string> _bloodSpellTapDamageBloodPowerBonusCurve;
        private ConfigEntry<string> _bloodSpellBleedBuildupBloodPowerBonusCurve;
        private ConfigEntry<string> _bloodSpellTapCastSpeedBloodPowerBonusCurve;
        private ConfigEntry<string> _bloodSpellTargetSearchBloodPowerBonusCurve;
        private ConfigEntry<string> _bloodSpellHeldBloodPowerBonusCurve;
        private ConfigEntry<string> _bleedBuildupStatusTerms;

        private ConfigEntry<bool> _abhartachTuningEnabled;
        private ConfigEntry<string> _abhartachMatchTerms;
        private ConfigEntry<string> _abhartachTemplateGuid;
        private ConfigEntry<bool> _abhartachScaleExplosionDamage;
        private ConfigEntry<bool> _abhartachScaleExplosionRadius;
        private ConfigEntry<bool> _abhartachScaleExplosionBleed;
        private ConfigEntry<bool> _abhartachScaleHeldCorpseHealing;
        private ConfigEntry<bool> _abhartachScaleCorpseSearchRange;
        private ConfigEntry<float> _abhartachCorpseSearchMaximumMultiplier;
        private ConfigEntry<string> _abhartachExplosionDamageBloodPowerBonusCurve;
        private ConfigEntry<string> _abhartachExplosionRadiusBloodPowerBonusCurve;
        private ConfigEntry<string> _abhartachExplosionBleedBloodPowerBonusCurve;
        private ConfigEntry<string> _abhartachHeldHealingBloodPowerBonusCurve;
        private ConfigEntry<string> _abhartachCorpseSearchBloodPowerBonusCurve;

        private ConfigEntry<float> _range;
        private ConfigEntry<float> _checkIntervalSeconds;
        private ConfigEntry<float> _focusGraceSeconds;
        private ConfigEntry<float> _strongHoldGraceSeconds;
        private ConfigEntry<float> _holdTrackerIntervalSeconds;
        private ConfigEntry<int> _raycastLayerMask;
        private ConfigEntry<int> _raycastParentSearchDepth;
        private ConfigEntry<float> _nearestCorpseFallbackRadius;
        private ConfigEntry<int> _raycastAllFallbackMaxHits;
        private ConfigEntry<float> _unresolvedCorpseRefreshIntervalSeconds;
        private ConfigEntry<int> _corpseHierarchyAliasMaxNodes;
        private ConfigEntry<bool> _cacheBloodTransfusionSourceMatches;

        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _showGrailFloatingTextDiagnostics;
        private ConfigEntry<bool> _overrideBloodEssence;
        private ConfigEntry<float> _bloodEssenceOverrideValue;
        private ConfigEntry<bool> _claimGrailFloatingTextCorpseXp;
        private ConfigEntry<bool> _claimGrailFloatingTextLiveDrainXp;
        private ConfigEntry<bool> _suppressGrailFloatingTextLiveDrainHealing;
        private readonly Dictionary<string, float> _pendingPreservedCalibrationFloats =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _pendingPreservedDiagnosticBools =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pendingPreservedManualOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _configSettingOrders =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;

        private static readonly Color BloodSpellInnerLightColor = new Color(1.0f, 0.02f, 0.0f, 1.0f);
        private readonly Dictionary<object, CorpseState> _corpseStates =
            new Dictionary<object, CorpseState>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, CorpseState> _corpseRaycastCache =
            new Dictionary<object, CorpseState>(ReferenceEqualityComparer.Instance);
        private readonly List<CorpseState> _allCorpseStates = new List<CorpseState>();
        private readonly Dictionary<object, StrongCastState> _strongCastStates =
            new Dictionary<object, StrongCastState>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, BloodSpellInnerLightReadyState> _bloodSpellInnerLightReadyStates =
            new Dictionary<object, BloodSpellInnerLightReadyState>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, LiveDrainState> _liveDrainStates =
            new Dictionary<object, LiveDrainState>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, SourceMatchCacheEntry> _sourceMatchCache =
            new Dictionary<object, SourceMatchCacheEntry>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, SourceMatchCacheEntry> _abhartachSourceMatchCache =
            new Dictionary<object, SourceMatchCacheEntry>(ReferenceEqualityComparer.Instance);
        private readonly HashSet<MagicItemTemplateInfo> _bloodTransfusionLightCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private readonly HashSet<MagicItemTemplateInfo> _bloodTransfusionHeavyCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private readonly HashSet<MagicItemTemplateInfo> _abhartachLightCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private readonly HashSet<MagicItemTemplateInfo> _abhartachHeavyCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private readonly Dictionary<string, FMOD.Sound> _corpseLeechFmodSoundsByPath =
            new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private FMOD.Studio.Bus _corpseLeechSfxBus;
        private FMOD.ChannelGroup _corpseLeechSfxChannelGroup;
        private bool _corpseLeechSfxBusLocked;
        private readonly Dictionary<string, List<string>> _corpseLeechSoundPathsByTier =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _recentCorpseLeechSoundPathsByTier =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Random _random = new System.Random();
        private readonly ConditionalWeakTable<object, BloodMagicProjectileState> _bloodMagicProjectiles =
            new ConditionalWeakTable<object, BloodMagicProjectileState>();
        private readonly ConditionalWeakTable<object, BloodMagicBleedDurationState> _bloodMagicBleedDurationStates =
            new ConditionalWeakTable<object, BloodMagicBleedDurationState>();
        [ThreadStatic]
        private static BloodMagicAreaBuildupContext _currentBloodMagicAreaBuildupContext;
        [ThreadStatic]
        private static BloodMagicProjectileImpactContext _currentBloodMagicProjectileImpactContext;
        [ThreadStatic]
        private static BloodMagicBuildupApplicationContext _currentBloodMagicBuildupApplicationContext;
        private readonly HashSet<object> _loggedUnresolvedRaycastHits =
            new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly HashSet<object> _aliveStateProbeSeen =
            new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly RaycastHit[] _servantTargetHits =
            new RaycastHit[ServantTargetHitCapacity];
        private readonly Collider[] _corpseTargetAssistColliders =
            new Collider[CorpseTargetAssistColliderCapacity];
        private readonly Dictionary<(Type Type, string Name), MethodInfo> _getterCache =
            new Dictionary<(Type Type, string Name), MethodInfo>();
        private readonly Dictionary<(Type Type, string Name), MethodInfo> _setterCache =
            new Dictionary<(Type Type, string Name), MethodInfo>();
        private readonly Dictionary<(Type Type, string Name, int ParameterCount), MethodInfo> _methodCache =
            new Dictionary<(Type Type, string Name, int ParameterCount), MethodInfo>();
        private readonly Dictionary<string, MethodInfo> _exactMethodCache = new Dictionary<string, MethodInfo>();
        private MethodInfo _soulAndServiceResolveServantIdentityMethod;
        private MethodInfo _soulAndServiceResolveServantMethod;
        private MethodInfo _soulAndServiceExsanguinateServantMethod;
        private MethodInfo _soulAndServiceSetRitualStateMethod;
        private MethodInfo _soulAndServiceMaterializeAbhartachCorpseMethod;
        private bool _soulAndServiceApiUnavailable;
        private readonly Dictionary<(Type Type, string Name), FieldInfo> _fieldCache =
            new Dictionary<(Type Type, string Name), FieldInfo>();

        private string _cachedMatchTermsRaw;
        private string _cachedTemplateGuidRaw;
        private string _cachedBloodlessTermsRaw;
        private string _cachedWhitelistTermsRaw;
        private string _cachedBleedStatusTermsRaw;
        private string _cachedProjectileTravelBloodPowerCurveRaw;
        private string _cachedTapDamageBloodPowerCurveRaw;
        private string _cachedBleedBuildupBloodPowerCurveRaw;
        private string _cachedTapCastSpeedBloodPowerCurveRaw;
        private string _cachedTargetSearchBloodPowerCurveRaw;
        private string _cachedHeldBloodPowerCurveRaw;
        private string _cachedAbhartachMatchTermsRaw;
        private string _cachedAbhartachTemplateGuidRaw;
        private string _cachedAbhartachExplosionDamageCurveRaw;
        private string _cachedAbhartachExplosionRadiusCurveRaw;
        private string _cachedAbhartachExplosionBleedCurveRaw;
        private string _cachedAbhartachHeldHealingCurveRaw;
        private string _cachedAbhartachCorpseSearchCurveRaw;
        private string[] _cachedMatchTerms = new string[0];
        private string[] _cachedAbhartachMatchTerms = new string[0];
        private string[] _cachedBloodlessTerms = new string[0];
        private string[] _cachedWhitelistTerms = new string[0];
        private string[] _cachedBleedStatusTerms = new string[0];
        private CurvePoint[] _cachedProjectileTravelBloodPowerCurve = new CurvePoint[0];
        private CurvePoint[] _cachedTapDamageBloodPowerCurve = new CurvePoint[0];
        private CurvePoint[] _cachedBleedBuildupBloodPowerCurve = new CurvePoint[0];
        private CurvePoint[] _cachedTapCastSpeedBloodPowerCurve = new CurvePoint[0];
        private CurvePoint[] _cachedTargetSearchBloodPowerCurve = new CurvePoint[0];
        private CurvePoint[] _cachedHeldBloodPowerCurve = new CurvePoint[0];
        private CurvePoint[] _cachedAbhartachExplosionDamageCurve = new CurvePoint[0];
        private CurvePoint[] _cachedAbhartachExplosionRadiusCurve = new CurvePoint[0];
        private CurvePoint[] _cachedAbhartachExplosionBleedCurve = new CurvePoint[0];
        private CurvePoint[] _cachedAbhartachHeldHealingCurve = new CurvePoint[0];
        private CurvePoint[] _cachedAbhartachCorpseSearchCurve = new CurvePoint[0];
        private int _matchSettingsRevision;
        private int _abhartachMatchSettingsRevision;
        private int _nextCorpseStateId = 1;
        private float _nextCorpseCheckTime;
        private float _nextGlobalHoldProbeTime;
        private float _nextUnresolvedCorpseRefreshTime;
        private float _nextLiveDrainCleanupTime;
        private object _lastServantTargetDiagnosticCandidate;
        private string _lastServantTargetDiagnosticStatus;
        private float _nextServantTargetDiagnosticTime;
        private float _liveDrainHealingEligibleUntil;
        private int _pendingLiveDrainHealingCount;
        private float _nextCacheCleanupTime;
        private CorpseState _focusedCorpse;
        private CorpseState _recentServantTarget;
        private float _recentServantTargetTime;
        private int _focusedCorpseInteropSnapshotFrame = -1;
        private CorpseState _focusedCorpseInteropSnapshotState;
        private bool _focusedCorpseInteropSnapshotResolved;
        private bool _focusedCorpseInteropSnapshotUnregisteredCandidate;
        private bool _loggedHealingResolution;
        private bool _heroGetterResolved;
        private bool _gameConstantsGetterResolved;
        private bool _equipmentReflectionResolved;
        private bool _lastBloodTransfusionEquipped;
        private bool _lastAbhartachEquipped;
        private float _nextBloodTransfusionEquippedCheckTime;
        private float _nextAbhartachEquippedCheckTime;
        private float _lastAbhartachCorpseQuality01 = 0.5f;
        private float _lastAbhartachCorpseQualityUntil;
        private float _nextCorpseQualityLogTime;
        private string _lastGftCorpseQualitySignature;
        private float _nextBloodProgressionSyncTime;
        private float _nextCorpseStatisticsImportAttemptTime;
        private float _lastReportedBloodEssence = -1.0f;
        private string _lastReportedCorpseStatistics;
        private bool _bloodProgressionUnavailableLogged;
        private float _abhartachHeldHealingActiveUntil;
        private readonly BloodSpellInnerLightHandState _bloodSpellInnerLightMainHandState =
            new BloodSpellInnerLightHandState(
                BloodSpellInnerLightHand.MainHand,
                BloodSpellInnerLightMainHandObjectName);
        private readonly BloodSpellInnerLightHandState _bloodSpellInnerLightOffHandState =
            new BloodSpellInnerLightHandState(
                BloodSpellInnerLightHand.OffHand,
                BloodSpellInnerLightOffHandObjectName);
        private float _nextBloodSpellInnerLightDiagnosticTime;
        private float _nextBloodSpellTuningDiagnosticTime;
        private int _bloodSpellInnerLightActivationLogsRemaining = 32;
        private bool _corpseLeechSoundPathsResolved;
        private bool _loggedMissingCorpseLeechSounds;
        private bool _loggedVanillaXpFalloffUnavailable;
        private bool _grailFloatingTextBridgeResolved;
        private bool _grailFloatingTextUnavailableLogged;
        private int _grailFloatingTextHealingClaimDepth;
        private int _grailFloatingTextHealingPresentationClaimDepth;
        private bool _firstPersonArmsAdjusterBridgeResolved;
        private bool _firstPersonArmsAdjusterUnavailableLogged;
        private bool _versatileWeaponsBridgeResolved;
        private bool _versatileWeaponsUnavailableLogged;
        private bool _versatileWeaponsMainHandWasSuppressed;
        private bool _versatileWeaponsOffHandWasSuppressed;
        private Func<bool> _versatileWeaponsIsMainHandSuppressed;
        private Func<bool> _versatileWeaponsIsOffHandSuppressed;
        private TryGetFirstPersonArmsVisualWorldOffsetDelegate
            _tryGetFirstPersonArmsVisualWorldOffset;
        private MethodInfo _heroGetter;
        private MethodInfo _gameConstantsGetter;
        private MethodInfo _skillUnitsSkillMethod;
        private MethodInfo _getHeroItemsMethod;
        private MethodInfo _equippedItemMethod;
        private FieldInfo _allEquipmentSlotsField;
        private FieldInfo _mainHandEquipmentSlotField;
        private FieldInfo _offHandEquipmentSlotField;

        private float Now
        {
            get { return Time.unscaledTime; }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo(PluginName + " " + PluginVersion + " startup begin.");

            try
            {
                BindConfig();
            }
            catch (Exception ex)
            {
                LogStartupException("BindConfig", ex);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, ex);
                ClearStaticReferences();
                enabled = false;
                return;
            }

            if (ShouldLogStartup())
            {
                Log.LogInfo(PluginName + " config bound.");
            }

            try
            {
                PatchGame();
                _bleedSkillGraphPreloader =
                    new BleedSkillGraphPreloader(
                        this,
                        Logger,
                        _preloadBleedSkillGraphs,
                        () => _enabled != null
                            && _enabled.Value);
                _bleedSkillGraphPreloader.Start();
            }
            catch (Exception ex)
            {
                LogStartupException("PatchGame", ex);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, ex);
                enabled = false;
                return;
            }

            if (ShouldLogStartup())
            {
                Log.LogInfo(PluginName + " " + PluginVersion + " loaded. Corpse leech, live drain XP, blood spell tuning, and blood spell inner light are active.");
            }
        }

        private void OnDestroy()
        {
            ConfigFile config = _resolvedConfig;
            if (config != null)
            {
                config.SettingChanged -= OnConfigSettingChanged;
            }

            ReleaseGrailFloatingTextDefaultHealingClaim();
            ReleaseGrailFloatingTextBloodHealingPresentationClaim();

            if (_bleedSkillGraphPreloader != null)
            {
                _bleedSkillGraphPreloader.Dispose();
                _bleedSkillGraphPreloader = null;
            }

            DestroyBloodSpellInnerLight();
            ReleaseCorpseLeechFmodSounds();

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            ClearStaticReferences();
        }

        private void ClearStaticReferences()
        {
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }

            if (ReferenceEquals(Log, Logger))
            {
                Log = null;
            }
        }

        private void Update()
        {
            RefreshFoaModManagerIfPending();

            if (_enabled == null)
            {
                return;
            }

            UpdateBloodProgression();
            UpdateCorpseLeech();
            UpdateBloodSpellInnerLight();
            CleanupLiveDrainStates();
            CleanupCachedStates();
        }

        private void LateUpdate()
        {
            if (_enabled == null
                || (!ShouldUpdateBloodSpellInnerLightPosition(
                        _bloodSpellInnerLightMainHandState)
                    && !ShouldUpdateBloodSpellInnerLightPosition(
                        _bloodSpellInnerLightOffHandState)))
            {
                return;
            }

            Vector3 visualWorldOffset = Vector3.zero;
            TryGetFirstPersonArmsVisualWorldOffset(out visualWorldOffset);
            UpdateBloodSpellInnerLightPosition(
                _bloodSpellInnerLightMainHandState,
                visualWorldOffset);
            UpdateBloodSpellInnerLightPosition(
                _bloodSpellInnerLightOffHandState,
                visualWorldOffset);
        }

        private ConfigEntry<T> BindOrdered<T>(
            string section,
            string key,
            T defaultValue,
            string description,
            string displayName = null,
            int? displayOrder = null)
        {
            return BindOrdered(
                section,
                key,
                defaultValue,
                new ConfigDescription(description),
                displayName,
                displayOrder);
        }

        private ConfigEntry<T> BindOrdered<T>(
            string section,
            string key,
            T defaultValue,
            ConfigDescription description,
            string displayName = null,
            int? displayOrder = null)
        {
            section = GetCleanConfigSection(section);
            ConfigFile config = ResolveConfigFile();
            if (String.Equals(
                    key,
                    "ConfigSchemaVersion",
                    StringComparison.Ordinal))
            {
                return config.Bind(section, key, defaultValue, description);
            }

            string displaySection = GetConfigDisplaySection(section, key);
            int order;
            if (!_configSettingOrders.TryGetValue(displaySection, out order))
            {
                order = 0;
            }
            _configSettingOrders[displaySection] = order + 10;

            return config.Bind(
                section,
                key,
                defaultValue,
                Grailwright.Shared.ConfigUiDescription.Create(
                    description.Description,
                    displaySection,
                    displayName ?? HumanizeConfigKey(key),
                    GetConfigSectionOrder(displaySection),
                    displayOrder ?? order,
                    description.AcceptableValues));
        }

        private static string GetConfigDisplaySection(string section, string key)
        {
            if (String.Equals(section, "General", StringComparison.Ordinal)
                && String.Equals(key, "Preset", StringComparison.Ordinal))
            {
                return "Blood Magic Preset";
            }

            if (!String.Equals(
                    section,
                    "Advanced - Custom Preset",
                    StringComparison.Ordinal))
            {
                return section;
            }

            return "Blood Magic Preset";
        }

        private static int GetConfigSectionOrder(string section)
        {
            switch (section)
            {
                case "General":
                    return 0;
                case "Blood Magic Preset":
                    return 10;
                case "Main Loop":
                    return 40;
                case "Blood Power":
                    return 50;
                case "Corpse Quality":
                    return 60;
                case "Bloodless Filter":
                    return 70;
                case "Blood Spell Inner Light":
                    return 80;
                case "Audio":
                    return 90;
                case "Integrations":
                    return 100;
                case "Advanced - Corpse Rewards":
                    return 130;
                case "Advanced - Live Drain":
                    return 140;
                case "Advanced - Blood Spell Growth":
                    return 150;
                case "Advanced - Abhartach Calling":
                    return 160;
                case "Advanced - Matching":
                    return 170;
                case "Performance":
                    return 180;
                case "Diagnostics":
                    return Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder;
                default:
                    throw new InvalidOperationException(
                        "Missing config section order for " + section + ".");
            }
        }

        private static string HumanizeConfigKey(string key)
        {
            StringBuilder builder = new StringBuilder(key.Length + 8);
            for (int index = 0; index < key.Length; index++)
            {
                char current = key[index];
                if (index > 0
                    && Char.IsUpper(current)
                    && (!Char.IsUpper(key[index - 1])
                        || (index + 1 < key.Length
                            && Char.IsLower(key[index + 1]))))
                {
                    builder.Append(' ');
                }
                builder.Append(current);
            }
            return builder.ToString();
        }

        private static string GetCleanConfigSection(string section)
        {
            int separatorIndex = section.IndexOf(". ", StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                return section;
            }

            for (int index = 0; index < separatorIndex; index++)
            {
                if (!Char.IsDigit(section[index]))
                {
                    return section;
                }
            }

            return section.Substring(separatorIndex + 2);
        }

        private void BindConfig()
        {
            ConfigFile config = ResolveConfigFile();
            ResetConfigIfSchemaChanged(config);
            _configSettingOrders.Clear();

            _enabled = BindOrdered("General", "Enabled", true, "Master switch.");
            BindOrdered(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version for this clean Blood Magic Expansion config.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _preset = BindOrdered("General", "Preset", Preset.Desecration, "Apply a complete ritual-economy profile to the visible values below. BloodRite is quick and restrained, Desecration is the balanced default, Exsanguination is slower and more rewarding, and Custom preserves the current values. Blood Essence progression governs spell strength independently.", "Blood Magic Preset");
            _preloadBleedSkillGraphs = BindOrdered(
                "General",
                "PreloadBleedSkillGraphs",
                true,
                "Preload and retain the vanilla Bleed status skill graph during gameplay loading instead of its first combat application. This isolated compatibility option can be disabled if a future game update removes the cold-load hitch.");

            _handRequirement = BindOrdered("Main Loop", "HandRequirement", HandRequirement.AnyHand, "Minimum required Blood/Life Transfusion hold state. AnyHand allows single-hand half payout and dual-hand full payout.");
            _singleHandPayoutMultiplier = BindOrdered("Main Loop", "SingleHandPayoutMultiplier", 0.5f, "Payout multiplier when only one Blood/Life Transfusion hand is held. Dual-held casts always use the full amount.");
            _awardCharacterXp = BindOrdered("Main Loop", "AwardCorpseXP", true, "Award character XP when a valid corpse ritual completes.");
            _healCharacter = BindOrdered("Main Loop", "HealFromCorpses", true, "Heal the player when a valid corpse ritual completes.");
            _liveDrainEnabled = BindOrdered("Main Loop", "LiveDrainXP", true, "Award small capped XP ticks while held Blood/Life Transfusion damages living enemies.");
            _bloodSpellTuningEnabled = BindOrdered("Main Loop", "BloodSpellTuning", true, "Develop Blood Transfusion and Life Transfusion through Blood Essence and Blood Power.");
            _abhartachTuningEnabled = BindOrdered("Main Loop", "AbhartachTuning", true, "Develop Abhartach's Calling corpse effects through Blood Essence and Blood Power.");

            _bloodSpellInnerLightEnabled = BindOrdered("Blood Spell Inner Light", "Enabled", true, "Show a red no-shadow light from each raised hand that has Blood Transfusion, Life Transfusion, or Abhartach's Calling equipped.");
            _bloodSpellInnerLightIntensity = BindOrdered("Blood Spell Inner Light", "Intensity", 0.5f, new ConfigDescription("Shared base brightness of each red hand light while its blood spell is readied, before the per-spell and interior multipliers. Actual casting temporarily triples that hand's final value 0.3 seconds after cast start, then drops back quickly when casting performs, ends, or cancels. This is a user-friendly brightness value that BME scales for the game's HDRP renderer. Zero disables visible light without removing the feature.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightBloodTransfusionIntensityMultiplier = BindOrdered("Blood Spell Inner Light", "BloodTransfusionIntensityMultiplier", 0.8f, new ConfigDescription("Brightness multiplier applied when Blood Transfusion is readied in this hand.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightLifeTransfusionIntensityMultiplier = BindOrdered("Blood Spell Inner Light", "LifeTransfusionIntensityMultiplier", 1.0f, new ConfigDescription("Brightness multiplier applied when Life Transfusion is readied in this hand.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightAbhartachCallingIntensityMultiplier = BindOrdered("Blood Spell Inner Light", "AbhartachCallingIntensityMultiplier", 1.2f, new ConfigDescription("Brightness multiplier applied when Abhartach's Calling is readied in this hand.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightInteriorIntensityMultiplier = BindOrdered("Blood Spell Inner Light", "InteriorIntensityMultiplier", 1.0f, new ConfigDescription("Additional blood hand-light intensity multiplier in full interior scenes. One preserves the configured intensity, two doubles it, and zero disables the visible hand lights only while indoors.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightMinimumPowerBrightnessMultiplier = BindOrdered("Blood Spell Inner Light", "MinimumPowerBrightnessMultiplier", 0.2f, new ConfigDescription("Blood Power 0 brightness multiplier applied after the independent intensity, spell, interior, and cast settings. The light grows smoothly from this faint starting point to the mastery milestone.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightMasteryBrightnessMultiplier = BindOrdered("Blood Spell Inner Light", "MasteryBrightnessMultiplier", 2.0f, new ConfigDescription("Blood Power 100 brightness multiplier applied after the independent intensity, spell, interior, and cast settings.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightMaximumPowerBrightnessMultiplier = BindOrdered("Blood Spell Inner Light", "MaximumPowerBrightnessMultiplier", 3.0f, new ConfigDescription("Blood Power 200 brightness multiplier applied after the independent intensity, spell, interior, and cast settings. The default is 50% brighter than the Power 100 milestone.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightMinimumPowerRange = BindOrdered("Blood Spell Inner Light", "MinimumPowerRange", 1.5f, new ConfigDescription("Blood Power 0 range in meters for the red inner player light.", new AcceptableValueRange<float>(0.1f, 20.0f)));
            _bloodSpellInnerLightMasteryRange = BindOrdered("Blood Spell Inner Light", "MasteryRange", 3.0f, new ConfigDescription("Blood Power 100 range in meters for the red inner player light.", new AcceptableValueRange<float>(0.1f, 20.0f)));
            _bloodSpellInnerLightMaximumPowerRange = BindOrdered("Blood Spell Inner Light", "MaximumPowerRange", 4.5f, new ConfigDescription("Blood Power 200 range in meters for the red inner player light.", new AcceptableValueRange<float>(0.1f, 20.0f)));
            _bloodSpellInnerLightFadeSeconds = BindOrdered("Blood Spell Inner Light", "FadeSeconds", 0.12f, new ConfigDescription("Seconds used to fade the red inner player light in and out. Zero switches instantly.", new AcceptableValueRange<float>(0.0f, 2.0f)));

            _corpseQualityScaleTransfusionHealing = BindOrdered("Corpse Quality", "ScaleTransfusionHealing", true, "Let corpse quality modestly scale Blood/Life Transfusion corpse healing. Character XP is not multiplied again.");
            _corpseQualityScaleAbhartachEffects = BindOrdered("Corpse Quality", "ScaleAbhartachEffects", true, "Let corpse quality modestly scale Abhartach corpse explosion damage, radius, bleed buildup, and held corpse healing.");
            _corpseQualityMinimumEffectMultiplier = BindOrdered("Corpse Quality", "MinimumEffectMultiplier", 0.5f, new ConfigDescription("Gameplay effect multiplier used for a very low-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _corpseQualityMaximumEffectMultiplier = BindOrdered("Corpse Quality", "MaximumEffectMultiplier", 1.5f, new ConfigDescription("Gameplay effect multiplier used for a high-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _abhartachRadiusMinimumQualityMultiplier = BindOrdered("Corpse Quality", "AbhartachRadiusMinimumMultiplier", 0.85f, new ConfigDescription("Abhartach explosion-radius multiplier used for a very low-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _abhartachRadiusMaximumQualityMultiplier = BindOrdered("Corpse Quality", "AbhartachRadiusMaximumMultiplier", 1.15f, new ConfigDescription("Abhartach explosion-radius multiplier used for a high-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _abhartachHealingMinimumQualityMultiplier = BindOrdered("Corpse Quality", "AbhartachHealingMinimumMultiplier", 0.75f, new ConfigDescription("Abhartach held-healing multiplier used for a very low-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _abhartachHealingMaximumQualityMultiplier = BindOrdered("Corpse Quality", "AbhartachHealingMaximumMultiplier", 1.25f, new ConfigDescription("Abhartach held-healing multiplier used for a high-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _corpseQualityFallbackQuality = BindOrdered("Corpse Quality", "FallbackQuality", 0.0f, new ConfigDescription("Focused corpse quality used when native tier, kill XP, and max health cannot be resolved.", new AcceptableValueRange<float>(0.0f, 1.0f)));

            _requireBloodPlausible = BindOrdered("Bloodless Filter", "RequireBloodPlausible", true, "Reject corpses and live targets that plausibly have no blood.");
            _bloodlessBlacklistTerms = BindOrdered("Bloodless Filter", "BloodlessBlacklistTerms", "Stone;Golem;Statue;Construct;Automaton;Crystal;Wisp;Spirit;Ghost;Wraith;Specter;Spectre;Skeleton;Skull;Bone;Animated Armor;Elemental;Wyrdspawn;Wyrdspirit;Wyrd Spirit;WyrdSlime;Wyrd Slime;Wyrdness", "Semicolon, comma, pipe, or newline separated terms that make a target ineligible unless whitelisted.");
            _bloodWhitelistTerms = BindOrdered("Bloodless Filter", "BloodWhitelistTerms", "", "Optional terms that force eligibility even if a blacklist term also matches.");

            _fallbackKillXp = BindOrdered("Advanced - Corpse Rewards", "FallbackKillXP", 0.0f, "Normal kill XP to use when real corpse XP cannot be resolved. Zero skips unresolved corpses.");
            _minimumXpToPay = BindOrdered("Advanced - Corpse Rewards", "MinimumXPToPay", 1.0f, "Minimum computed corpse XP required to pay.");
            _maximumXp = BindOrdered("Advanced - Corpse Rewards", "MaximumXP", 0.0f, "Absolute maximum corpse XP per corpse. Zero or less disables this cap.");
            _roundXpTo = BindOrdered("Advanced - Corpse Rewards", "RoundXPTo", 1.0f, "Round corpse XP to this increment. One rounds to whole XP; zero disables rounding.");
            _requireTargetXpRewardAllowedWhenPresent = BindOrdered("Advanced - Corpse Rewards", "RequireTargetXPRewardAllowedWhenPresent", true, "If the corpse source exposes XpRewardAllowed, require it to be true. Sources without that property are still allowed.");
            _rawCharacterXpPerCorpseXp = BindOrdered("Advanced - Corpse Rewards", "RawCharacterXPPerCorpseXP", 1.0f, "Raw character XP awarded per computed corpse XP.");
            _announceRawCharacterXp = BindOrdered("Advanced - Corpse Rewards", "AnnounceRawCharacterXP", false, "Usually leave off; direct XP stat changes already announce themselves.");
            _healMaxHealthPercentPerXpPercent = BindOrdered("Advanced - Corpse Rewards", "HealMaxHealthPercentPerXpPercent", 0.5f, "Baseline healing as max-health percent per XP reward percent before enemy power scaling.");
            _healPowerScalingMode = BindOrdered("Advanced - Corpse Rewards", "HealPowerScalingMode", HealingPowerScalingMode.TargetMaxHealthCurve, "Off uses fixed preset healing. TargetMaxHealthCurve scales healing by the drained enemy's resolved max health.");
            _healReferenceTargetMaxHealth = BindOrdered("Advanced - Corpse Rewards", "HealReferenceTargetMaxHealth", 300.0f, new ConfigDescription("Enemy max HP that receives unmodified baseline healing.", new AcceptableValueRange<float>(1.0f, 100000.0f)));
            _healPowerExponent = BindOrdered("Advanced - Corpse Rewards", "HealPowerExponent", 0.5f, new ConfigDescription("Curve exponent for max-HP healing scaling. 0.5 is a smooth square-root curve; 1 is linear.", new AcceptableValueRange<float>(0.05f, 3.0f)));
            _healMinimumPowerScale = BindOrdered("Advanced - Corpse Rewards", "HealMinimumPowerScale", 0.5f, new ConfigDescription("Lowest multiplier applied to baseline healing when enemy max HP is low.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _healMaximumPowerScale = BindOrdered("Advanced - Corpse Rewards", "HealMaximumPowerScale", 2.0f, new ConfigDescription("Highest multiplier applied to baseline healing when enemy max HP is high.", new AcceptableValueRange<float>(0.0f, 10.0f)));

            _customPayoutPercentOfKillXp = BindOrdered("Advanced - Custom Preset", "CustomCorpseXPPercent", 40.0f, "Corpse payout as a percent of the enemy's vanilla effective kill XP. Changing this value manually selects Custom.", "Corpse XP Percent");
            _secondsRequired = BindOrdered("Advanced - Custom Preset", "CustomRitualSeconds", 1.5f, "Seconds of continuous corpse focus required. Changing this value manually selects Custom.", "Ritual Seconds");
            _customLiveDrainXpTickIntervalSeconds = BindOrdered("Advanced - Custom Preset", "CustomLiveDrainTickSeconds", 1.5f, "Seconds between live-drain XP ticks. Changing this value manually selects Custom.", "Live Drain Tick Seconds");
            _customLiveDrainXpPercentPerTick = BindOrdered("Advanced - Custom Preset", "CustomLiveDrainXPPercentPerTick", 6.0f, "Percent of target kill XP paid per live-drain XP tick. Changing this value manually selects Custom.", "Live Drain XP Percent Per Tick");
            _customLiveDrainMaximumXpPercentPerTarget = BindOrdered("Advanced - Custom Preset", "CustomLiveDrainXPPercentCapPerTarget", 30.0f, "Maximum percent of target kill XP paid by live-drain ticks. Changing this value manually selects Custom.", "Live Drain XP Percent Cap Per Target");
            _liveDrainAwardCharacterXp = BindOrdered("Advanced - Live Drain", "AwardCharacterXP", true, "Award small character XP ticks while held Blood/Life Transfusion damages living enemies.");
            _liveDrainRawCharacterXpMultiplier = BindOrdered("Advanced - Live Drain", "RawCharacterXPPerComputedXP", 1.0f, "Raw character XP awarded per computed live-drain XP.");
            _liveDrainHealingMultiplier = BindOrdered("Advanced - Live Drain", "HeldHealingMultiplier", 2.0f, new ConfigDescription("Multiplier applied to Blood/Life Transfusion healing that follows confirmed held-channel damage. Tap projectile healing is not changed.", new AcceptableValueRange<float>(0.0f, 10.0f)));

            _bloodSpellScaleProjectileTravel = BindOrdered("Advanced - Blood Spell Growth", "ScaleProjectileTravel", true, "Scale Blood/Life Transfusion projectile travel distance by increasing projectile lifetime.");
            _bloodSpellScaleHomingTargetSearch = BindOrdered("Advanced - Blood Spell Growth", "ScaleHomingTargetSearch", true, "Scale Blood/Life Transfusion homing target-search distance when the projectile exposes one.");
            _bloodSpellScaleHeldTargetRange = BindOrdered("Advanced - Blood Spell Growth", "ScaleHeldTargetRange", true, "Scale Blood/Life Transfusion visual-script target-search range.");
            _bloodSpellHomingTargetSearchMaximumMultiplier = BindOrdered("Advanced - Blood Spell Growth", "HomingTargetSearchMaximumMultiplier", 2.1f, new ConfigDescription("Maximum final Blood/Life homing target-search multiplier.", new AcceptableValueRange<float>(1.0f, 10.0f)));
            _bloodSpellHeldTargetRangeMaximumMultiplier = BindOrdered("Advanced - Blood Spell Growth", "HeldTargetRangeMaximumMultiplier", 2.0f, new ConfigDescription("Maximum final Blood/Life held target-search range multiplier.", new AcceptableValueRange<float>(1.0f, 10.0f)));
            _bloodSpellScaleBleedDuration = BindOrdered("Advanced - Blood Spell Growth", "ScaleBleedDuration", true, "Extend active Bleed duration from Blood/Life Transfusion and Abhartach's Calling with Blood Power. Incomplete buildup decay and Bleed tick strength are unchanged.");
            _bloodSpellMaximumBleedDurationMultiplier = BindOrdered("Advanced - Blood Spell Growth", "MaximumBleedDurationMultiplier", 2.0f, new ConfigDescription("Active Bleed duration multiplier at Blood Power 200. Power 0 always preserves native duration and successful new Bleed procs refresh the scaled duration.", new AcceptableValueRange<float>(1.0f, 10.0f)));
            _bloodSpellProjectileTravelBloodPowerBonusCurve = BindOrdered("Advanced - Blood Spell Growth", "ProjectileTravelBloodPowerBonusCurve", "0:0;5:1;10:3;15:6;20:11;25:16;30:22;35:29;40:37;45:47;50:56", "Blood-Power-to-bonus-percent curve for projectile travel distance.");
            _bloodSpellTapDamageBloodPowerBonusCurve = BindOrdered("Advanced - Blood Spell Growth", "TapDamageBloodPowerBonusCurve", "0:0;5:1;10:2;15:4;20:6;25:9;30:13;35:18;40:23;45:28;50:34", "Blood-Power-to-bonus-percent curve for Blood/Life tap projectile damage.");
            _bloodSpellBleedBuildupBloodPowerBonusCurve = BindOrdered("Advanced - Blood Spell Growth", "BleedBuildupBloodPowerBonusCurve", "0:0;5:1;10:3;15:6;20:11;25:16;30:22;35:29;40:37;45:47;50:56", "Blood-Power-to-bonus-percent curve for Bleed buildup.");
            _bloodSpellTapCastSpeedBloodPowerBonusCurve = BindOrdered("Advanced - Blood Spell Growth", "TapCastSpeedBloodPowerBonusCurve", "0:0;5:0;10:1;15:2;20:4;25:6;30:8;35:11;40:14;45:18;50:21", "Blood-Power-to-bonus-percent curve for tap/projectile cast speed.");
            _bloodSpellTargetSearchBloodPowerBonusCurve = BindOrdered("Advanced - Blood Spell Growth", "TargetSearchBloodPowerBonusCurve", "0:0;5:0;10:2;15:4;20:6;25:9;30:12;35:16;40:22;45:28;50:35", "Gentler Blood-Power-to-bonus-percent curve for held and homing target-search range.");
            _bloodSpellHeldBloodPowerBonusCurve = BindOrdered("Advanced - Blood Spell Growth", "HeldChannelBloodPowerBonusCurve", "0:0;5:0;10:1;15:2;20:3;25:4;30:5;35:6;40:8;45:10;50:12", "Blood-Power-to-bonus-percent curve for held/channel speed.");
            _bleedBuildupStatusTerms = BindOrdered("Advanced - Blood Spell Growth", "BleedBuildupStatusTerms", "Bleed;Bleeding", "Terms used to identify bleed buildup statuses for tuning.");

            _abhartachScaleExplosionDamage = BindOrdered("Advanced - Abhartach Calling", "ScaleExplosionDamage", true, "Scale Abhartach's Calling corpse explosion damage.");
            _abhartachScaleExplosionRadius = BindOrdered("Advanced - Abhartach Calling", "ScaleExplosionRadius", true, "Scale Abhartach's Calling corpse explosion radius.");
            _abhartachScaleExplosionBleed = BindOrdered("Advanced - Abhartach Calling", "ScaleExplosionBleed", true, "Scale Abhartach's Calling corpse explosion bleed buildup.");
            _abhartachScaleHeldCorpseHealing = BindOrdered("Advanced - Abhartach Calling", "ScaleHeldCorpseHealing", true, "Scale Abhartach's Calling held corpse healing.");
            _abhartachScaleCorpseSearchRange = BindOrdered("Advanced - Abhartach Calling", "ScaleCorpseSearchRange", true, "Scale Abhartach's Calling corpse-search range.");
            _abhartachCorpseSearchMaximumMultiplier = BindOrdered("Advanced - Abhartach Calling", "CorpseSearchMaximumMultiplier", 2.0f, new ConfigDescription("Maximum final Abhartach corpse-search range multiplier.", new AcceptableValueRange<float>(1.0f, 10.0f)));
            _corpseQualityEffectMemorySeconds = BindOrdered("Advanced - Abhartach Calling", "CorpseQualityEffectMemorySeconds", 1.25f, new ConfigDescription("Seconds to remember the last Abhartach-focused corpse quality for delayed spell effects.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _abhartachExplosionDamageBloodPowerBonusCurve = BindOrdered("Advanced - Abhartach Calling", "ExplosionDamageBloodPowerBonusCurve", "0:0;5:1;10:3;15:6;20:10;25:14;30:18;35:23;40:28;45:34;50:40", "Blood-Power-to-bonus-percent curve for explosion damage.");
            _abhartachExplosionRadiusBloodPowerBonusCurve = BindOrdered("Advanced - Abhartach Calling", "ExplosionRadiusBloodPowerBonusCurve", "0:0;5:1;10:2;15:4;20:7;25:10;30:14;35:18;40:23;45:29;50:35", "Blood-Power-to-bonus-percent curve for explosion radius.");
            _abhartachExplosionBleedBloodPowerBonusCurve = BindOrdered("Advanced - Abhartach Calling", "ExplosionBleedBloodPowerBonusCurve", "0:0;5:1;10:3;15:6;20:10;25:14;30:18;35:23;40:28;45:34;50:40", "Blood-Power-to-bonus-percent curve for explosion bleed buildup.");
            _abhartachHeldHealingBloodPowerBonusCurve = BindOrdered("Advanced - Abhartach Calling", "HeldCorpseHealingBloodPowerBonusCurve", "0:0;5:1;10:4;15:7;20:10;25:14;30:18;35:23;40:28;45:34;50:40", "Blood-Power-to-bonus-percent curve for held corpse healing.");
            _abhartachCorpseSearchBloodPowerBonusCurve = BindOrdered("Advanced - Abhartach Calling", "CorpseSearchBloodPowerBonusCurve", "0:0;5:0;10:2;15:4;20:6;25:9;30:12;35:16;40:22;45:28;50:35", "Gentler Blood-Power-to-bonus-percent curve for corpse-search range.");

            _bloodTransfusionMatchTerms = BindOrdered("Advanced - Matching", "BloodSpellMatchTerms", "BloodTransfusion;Blood Transfusion;ItemTemplate_Magic_Tier1_BloodTransfusion;LifeTransfusion;Life Transfusion;ItemTemplate_Magic_Tier1_LifeTransfusion", "Terms used to identify Blood Transfusion and Life Transfusion items, skills, or templates.");
            _bloodTransfusionTemplateGuid = BindOrdered("Advanced - Matching", "BloodSpellTemplateGuid", "", "Optional exact Blood/Life Transfusion template GUID.");
            _abhartachMatchTerms = BindOrdered("Advanced - Matching", "AbhartachMatchTerms", "Abhartach;Abhartach's Calling;ItemTemplate_Magic_Tier2_AbhartachsCalling", "Terms used to identify Abhartach's Calling items, skills, or templates.");
            _abhartachTemplateGuid = BindOrdered("Advanced - Matching", "AbhartachTemplateGuid", "", "Optional exact Abhartach's Calling template GUID.");
            _range = BindOrdered("Performance", "Range", 7.0f, "Maximum camera raycast distance for detecting the corpse being looked at.");
            _checkIntervalSeconds = BindOrdered("Performance", "CheckIntervalSeconds", 0.15f, "Seconds between corpse look checks while the required spell hold is active.");
            _focusGraceSeconds = BindOrdered("Performance", "FocusGraceSeconds", 0.35f, "Short grace window before losing corpse focus resets the preset ritual timer.");
            _strongHoldGraceSeconds = BindOrdered("Performance", "StrongHoldGraceSeconds", 0.85f, "Grace window used when converting Blood/Life spell cast/hold events into active hand state.");
            _holdTrackerIntervalSeconds = BindOrdered("Performance", "HoldTrackerIntervalSeconds", 0.15f, "Minimum seconds between MagicFSM held-state probes.");
            _raycastLayerMask = BindOrdered("Performance", "RaycastLayerMask", -1, "Unity physics layer mask used by the corpse look raycast. -1 checks all layers.");
            _raycastParentSearchDepth = BindOrdered("Performance", "RaycastParentSearchDepth", 20, "Maximum parent transforms checked when resolving a corpse body collider.");
            _nearestCorpseFallbackRadius = BindOrdered("Performance", "NearestCorpseFallbackRadius", 2.0f, "Maximum meters from an unresolved corpse-like collider to an unexhausted registered corpse for fallback matching. Zero disables fallback.");
            _raycastAllFallbackMaxHits = BindOrdered("Performance", "RaycastAllFallbackMaxHits", 10, "Maximum sorted RaycastAll hits checked after the primary corpse raycast cannot resolve a usable corpse. Zero disables fallback.");
            _unresolvedCorpseRefreshIntervalSeconds = BindOrdered("Performance", "UnresolvedCorpseRefreshIntervalSeconds", 1.5f, "Minimum seconds between cached corpse alias refreshes after an unresolved corpse-like raycast hit.");
            _corpseHierarchyAliasMaxNodes = BindOrdered("Performance", "CorpseHierarchyAliasMaxNodes", 96, "Maximum child transforms/colliders cached per corpse visual hierarchy.");
            _cacheBloodTransfusionSourceMatches = BindOrdered("Performance", "CacheBloodSpellSourceMatches", true, "Cache Blood/Life spell item, skill, and template match results by object reference.");

            _playCorpseLeechSound = BindOrdered("Audio", "PlayCorpseLeechSound", true, "Play a quality-matched FMOD WAV when a corpse ritual successfully completes.");
            _corpseLeechSoundVolume = BindOrdered("Audio", "CorpseLeechSoundVolume", 0.85f, new ConfigDescription("Global FMOD volume for corpse leech sounds.", new AcceptableValueRange<float>(0.0f, 2.0f)));
            _corpseLeechSoundRangeVolume = BindOrdered("Audio", "CorpseLeechSoundRangeVolume", 1.0f, new ConfigDescription("How strongly corpse leech sounds fade with corpse distance. 0 disables distance fade; 1 uses the full 0m=100%, 30m+=10% curve.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _avoidRecentCorpseLeechRepeats = BindOrdered("Audio", "AvoidRecentCorpseLeechRepeats", true, "Avoid replaying recently used corpse leech sounds from the same quality tier when enough alternatives are available.");
            _recentCorpseLeechSoundMemory = BindOrdered("Audio", "RecentCorpseLeechSoundMemory", 2, new ConfigDescription("How many recently played sounds to avoid per quality tier.", new AcceptableValueRange<int>(0, 20)));
            _corpseLeechRandomPitchSemitones = BindOrdered("Audio", "CorpseLeechRandomPitchSemitones", 0.20f, new ConfigDescription("Random FMOD channel pitch variation in semitones. Zero disables.", new AcceptableValueRange<float>(0.0f, 12.0f)));

            _diagnostics = BindOrdered("Diagnostics", "Diagnostics", false, "Log throttled targeting, ritual, reward, healing, corpse-quality, and blood-light evidence. Startup errors and warnings remain enabled independently.");
            _showGrailFloatingTextDiagnostics = BindOrdered("Diagnostics", "ShowGrailFloatingTextDiagnostics", true, "When Diagnostics and Grail Floating Text are enabled, show concise targeting, ritual, corpse-quality, and blood-light outcomes in-game.");
            _overrideBloodEssence = BindOrdered("Diagnostics", "OverrideBloodEssence", false, "Temporarily use BloodEssenceOverrideValue for Blood Power, APIs, and optional Deeds display without changing the character's saved Blood Essence.");
            _bloodEssenceOverrideValue = BindOrdered("Diagnostics", "BloodEssenceOverrideValue", 5000.0f, new ConfigDescription("Temporary effective Blood Essence used only while OverrideBloodEssence is enabled. Useful checkpoints include 0, 250, 1000, 2000, 3000, 4000, 5000, and 10000.", new AcceptableValueRange<float>(0.0f, 10000.0f)));
            _claimGrailFloatingTextCorpseXp = BindOrdered("Integrations", "ClaimGrailFloatingTextCorpseXP", true, "When Grail Floating Text is loaded, show corpse-leech character XP as a red corpse-icon XP event instead of the generic XP event.");
            _claimGrailFloatingTextLiveDrainXp = BindOrdered("Integrations", "ClaimGrailFloatingTextLiveDrainXP", true, "When Grail Floating Text is loaded, show live-drain character XP as a red magic-icon XP event instead of the generic XP event.");
            _suppressGrailFloatingTextLiveDrainHealing = BindOrdered("Integrations", "SuppressGrailFloatingTextLiveDrainHealing", true, "When supported by Grail Floating Text, keep frequent held-channel Blood/Life Transfusion healing ticks out of its generic Healed notifications.");

            if (ShouldLogStartup())
            {
                Log.LogInfo(
                    "Blood spell inner light config: enabled="
                    + _bloodSpellInnerLightEnabled.Value
                    + ", intensity="
                    + FormatFloat(_bloodSpellInnerLightIntensity.Value)
                    + ", bloodTransfusionMultiplier="
                    + FormatFloat(_bloodSpellInnerLightBloodTransfusionIntensityMultiplier.Value)
                    + ", lifeTransfusionMultiplier="
                    + FormatFloat(_bloodSpellInnerLightLifeTransfusionIntensityMultiplier.Value)
                    + ", abhartachCallingMultiplier="
                    + FormatFloat(_bloodSpellInnerLightAbhartachCallingIntensityMultiplier.Value)
                    + ", interiorIntensityMultiplier="
                    + FormatFloat(_bloodSpellInnerLightInteriorIntensityMultiplier.Value)
                    + ", minimumPowerBrightnessMultiplier="
                    + FormatFloat(_bloodSpellInnerLightMinimumPowerBrightnessMultiplier.Value)
                    + ", masteryBrightnessMultiplier="
                    + FormatFloat(_bloodSpellInnerLightMasteryBrightnessMultiplier.Value)
                    + ", maximumPowerBrightnessMultiplier="
                    + FormatFloat(_bloodSpellInnerLightMaximumPowerBrightnessMultiplier.Value)
                    + ", minimumPowerRange="
                    + FormatFloat(_bloodSpellInnerLightMinimumPowerRange.Value)
                    + ", masteryRange="
                    + FormatFloat(_bloodSpellInnerLightMasteryRange.Value)
                    + ", maximumPowerRange="
                    + FormatFloat(_bloodSpellInnerLightMaximumPowerRange.Value)
                    + ", fadeSeconds="
                    + FormatFloat(_bloodSpellInnerLightFadeSeconds.Value)
                    + ", diagnostics="
                    + DiagnosticsEnabled()
                    + ".");
            }

            RestorePreservedConfigValues();
            Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                config,
                Logger,
                PluginName,
                ConfigSchemaVersion,
                ConfigRecoveryBaselineSchema,
                ConfigRecoveryKeepCurrentDefaultRules,
                ConfigRecoveryPermanentExclusions);
            ApplySelectedPreset();
            config.SettingChanged += OnConfigSettingChanged;
            config.Save();
        }

        private void OnConfigSettingChanged(
            object sender,
            SettingChangedEventArgs eventArgs)
        {
            if (_applyingPreset || eventArgs == null)
            {
                return;
            }

            ConfigEntryBase changedSetting = eventArgs.ChangedSetting;
            if (ReferenceEquals(changedSetting, _preset))
            {
                ApplySelectedPreset();
                _foaModManagerRefreshPending = true;
                return;
            }

            if (!IsPresetValueSetting(changedSetting)
                || _preset == null
                || _preset.Value == Preset.Custom)
            {
                return;
            }

            _applyingPreset = true;
            try
            {
                _preset.Value = Preset.Custom;
            }
            finally
            {
                _applyingPreset = false;
            }
            _foaModManagerRefreshPending = true;
        }

        private bool IsPresetValueSetting(ConfigEntryBase setting)
        {
            return ReferenceEquals(setting, _customPayoutPercentOfKillXp)
                || ReferenceEquals(setting, _secondsRequired)
                || ReferenceEquals(
                    setting,
                    _customLiveDrainXpTickIntervalSeconds)
                || ReferenceEquals(setting, _customLiveDrainXpPercentPerTick)
                || ReferenceEquals(
                    setting,
                    _customLiveDrainMaximumXpPercentPerTarget);
        }

        private void ApplySelectedPreset()
        {
            if (_preset == null || _preset.Value == Preset.Custom)
            {
                return;
            }

            _applyingPreset = true;
            try
            {
                switch (_preset.Value)
                {
                    case Preset.BloodRite:
                        ApplyBloodRitePreset();
                        break;
                    case Preset.Exsanguination:
                        ApplyExsanguinationPreset();
                        break;
                    default:
                        ApplyDesecrationPreset();
                        break;
                }
            }
            finally
            {
                _applyingPreset = false;
            }
        }

        private void ApplyBloodRitePreset()
        {
            _customPayoutPercentOfKillXp.Value = 30.0f;
            _secondsRequired.Value = 1.0f;
            _customLiveDrainXpTickIntervalSeconds.Value = 1.0f;
            _customLiveDrainXpPercentPerTick.Value = 4.0f;
            _customLiveDrainMaximumXpPercentPerTarget.Value = 20.0f;
        }

        private void ApplyDesecrationPreset()
        {
            _customPayoutPercentOfKillXp.Value = 40.0f;
            _secondsRequired.Value = 1.5f;
            _customLiveDrainXpTickIntervalSeconds.Value = 1.5f;
            _customLiveDrainXpPercentPerTick.Value = 6.0f;
            _customLiveDrainMaximumXpPercentPerTarget.Value = 30.0f;
        }

        private void ApplyExsanguinationPreset()
        {
            _customPayoutPercentOfKillXp.Value = 45.0f;
            _secondsRequired.Value = 2.0f;
            _customLiveDrainXpTickIntervalSeconds.Value = 2.0f;
            _customLiveDrainXpPercentPerTick.Value = 8.0f;
            _customLiveDrainMaximumXpPercentPerTarget.Value = 40.0f;
        }

        private void RefreshFoaModManagerIfPending()
        {
            if (!_foaModManagerRefreshPending)
            {
                return;
            }

            _foaModManagerRefreshPending = false;
            try
            {
                Type apiType = AccessTools.TypeByName(
                    "FoAModManager.FoAModManagerApi");
                MethodInfo refreshMethod = apiType == null
                    ? null
                    : AccessTools.Method(apiType, "Refresh");
                if (refreshMethod != null)
                {
                    refreshMethod.Invoke(null, null);
                }
            }
            catch (Exception exception)
            {
                if (DiagnosticsEnabled())
                {
                    Log.LogWarning(
                        "FoA Mod Manager refresh failed: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void UpdateBloodSpellInnerLight()
        {
            if (_bloodSpellInnerLightEnabled == null)
            {
                return;
            }

            ObserveVersatileWeaponsSuppressedHands();
            ObserveBloodSpellInnerLightEquippedItems();
            UpdateBloodSpellInnerLight(_bloodSpellInnerLightMainHandState);
            UpdateBloodSpellInnerLight(_bloodSpellInnerLightOffHandState);
        }

        private void UpdateBloodSpellInnerLight(BloodSpellInnerLightHandState handState)
        {
            float targetBrightness = GetBloodSpellInnerLightTargetBrightness(handState);
            float targetIntensity = GetBloodSpellInnerLightRenderIntensity(targetBrightness);
            bool immediateFadeOut = handState.ImmediateFadeOutRequested;
            handState.ImmediateFadeOutRequested = false;

            if (handState.Light == null && targetIntensity <= BloodSpellInnerLightMinimumIntensity)
            {
                return;
            }

            if (!EnsureBloodSpellInnerLight(handState))
            {
                if (targetIntensity > BloodSpellInnerLightMinimumIntensity)
                {
                    LogBloodSpellInnerLightDiagnosticThrottled(
                        "wanted visible "
                        + handState.Hand
                        + " light but could not resolve its wrist transform.");
                }

                return;
            }

            if (handState.LightObject == null || handState.Light == null)
            {
                return;
            }

            if (!handState.LightObject.activeSelf)
            {
                handState.LightObject.SetActive(true);
            }

            float nextIntensity = targetIntensity;
            float fadeSeconds = GetBloodSpellInnerLightFadeSeconds();
            if (!immediateFadeOut && fadeSeconds > 0.0f)
            {
                float maxReference = Math.Max(1.0f, Math.Max(handState.Light.intensity, targetIntensity));
                float maxDelta = Time.unscaledDeltaTime * maxReference / fadeSeconds;
                nextIntensity = Mathf.MoveTowards(handState.Light.intensity, targetIntensity, maxDelta);
            }

            handState.Light.intensity = nextIntensity;
            handState.Light.range = GetBloodSpellInnerLightRange();

            bool visible = targetBrightness > BloodSpellInnerLightMinimumIntensity ||
                nextIntensity > BloodSpellInnerLightMinimumIntensity;
            handState.Light.enabled = visible;
            if (visible != handState.LastVisible)
            {
                handState.LastVisible = visible;
                LogBloodSpellInnerLightDiagnostic(
                    "hand="
                    + handState.Hand
                    + ", visibility="
                    + visible
                    + ", configuredBrightness="
                    + FormatFloat(targetBrightness)
                    + ", targetIntensity="
                    + FormatFloat(targetIntensity)
                    + ", currentIntensity="
                    + FormatFloat(nextIntensity)
                    + ", range="
                    + FormatFloat(handState.Light.range)
                    + ", interior="
                    + IsBloodSpellInnerLightInterior()
                    + ", parent="
                    + DescribeTransform(handState.LightObject.transform.parent)
                    + ".");
                ShowBloodMagicDiagnostic(
                    "blood-magic-inner-light",
                    "Blood Magic: "
                        + handState.Hand
                        + " inner light "
                        + (visible ? "visible" : "hidden")
                        + "; "
                        + (IsBloodSpellInnerLightInterior()
                            ? "interior"
                            : "exterior")
                        + "; intensity "
                        + FormatFloat(nextIntensity)
                        + ".");
            }

            if (!visible)
            {
                handState.LightObject.SetActive(false);
            }
        }

        private bool ShouldShowBloodSpellInnerLight(BloodSpellInnerLightHandState handState)
        {
            return _enabled != null &&
                _enabled.Value &&
                _bloodSpellInnerLightEnabled != null &&
                _bloodSpellInnerLightEnabled.Value &&
                !IsVersatileWeaponsHandSuppressed(handState.Hand) &&
                !handState.SuppressForNonBloodEquipment &&
                HasBloodSpellInnerLightReadiedState(handState) &&
                GetBloodSpellInnerLightIntensity() > BloodSpellInnerLightMinimumIntensity;
        }

        private void ObserveVersatileWeaponsSuppressedHands()
        {
            bool mainHandSuppressed = IsVersatileWeaponsHandSuppressed(
                BloodSpellInnerLightHand.MainHand);
            bool offHandSuppressed = IsVersatileWeaponsHandSuppressed(
                BloodSpellInnerLightHand.OffHand);

            if (mainHandSuppressed
                && !_versatileWeaponsMainHandWasSuppressed)
            {
                ClearMagicTrackingForSuppressedHand(
                    BloodSpellInnerLightHand.MainHand);
            }

            if (offHandSuppressed
                && !_versatileWeaponsOffHandWasSuppressed)
            {
                ClearMagicTrackingForSuppressedHand(
                    BloodSpellInnerLightHand.OffHand);
            }

            if ((mainHandSuppressed
                    != _versatileWeaponsMainHandWasSuppressed)
                || (offHandSuppressed
                    != _versatileWeaponsOffHandWasSuppressed))
            {
                _nextBloodTransfusionEquippedCheckTime = 0.0f;
                _nextAbhartachEquippedCheckTime = 0.0f;
            }

            if ((mainHandSuppressed
                    && !_versatileWeaponsMainHandWasSuppressed)
                || (offHandSuppressed
                    && !_versatileWeaponsOffHandWasSuppressed))
            {
                _abhartachHeldHealingActiveUntil = 0.0f;
            }

            _versatileWeaponsMainHandWasSuppressed = mainHandSuppressed;
            _versatileWeaponsOffHandWasSuppressed = offHandSuppressed;
        }

        private bool IsVersatileWeaponsHandSuppressed(
            BloodSpellInnerLightHand hand)
        {
            if (!TryResolveVersatileWeaponsBridge())
            {
                return false;
            }

            try
            {
                return hand == BloodSpellInnerLightHand.MainHand
                    ? _versatileWeaponsIsMainHandSuppressed()
                    : _versatileWeaponsIsOffHandSuppressed();
            }
            catch (Exception exception)
            {
                _versatileWeaponsIsMainHandSuppressed = null;
                _versatileWeaponsIsOffHandSuppressed = null;
                LogVersatileWeaponsUnavailableOnce(
                    "Versatile Weapons hand-suppression API failed: "
                    + exception.GetBaseException().Message
                    + ".");
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
            PluginInfo pluginInfo;
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
                : apiType.GetMethod(
                    "IsMainHandSuppressed",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
            MethodInfo offMethod = apiType == null
                ? null
                : apiType.GetMethod(
                    "IsOffHandSuppressed",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
            if (mainMethod == null
                || mainMethod.ReturnType != typeof(bool)
                || offMethod == null
                || offMethod.ReturnType != typeof(bool))
            {
                LogVersatileWeaponsUnavailableOnce(
                    "Versatile Weapons is loaded, but its hand-suppression API could not be found.");
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
                LogBloodSpellInnerLightDiagnostic(
                    "connected to the Versatile Weapons hand-suppression API.");
                return true;
            }
            catch (Exception exception)
            {
                LogVersatileWeaponsUnavailableOnce(
                    "Versatile Weapons hand-suppression API binding failed: "
                    + exception.GetBaseException().Message
                    + ".");
                return false;
            }
        }

        private void LogVersatileWeaponsUnavailableOnce(string message)
        {
            if (_versatileWeaponsUnavailableLogged)
            {
                return;
            }

            _versatileWeaponsUnavailableLogged = true;
            Log.LogWarning(message);
        }

        private void ClearMagicTrackingForSuppressedHand(
            BloodSpellInnerLightHand hand)
        {
            List<object> remove = new List<object>();
            foreach (KeyValuePair<object, BloodSpellInnerLightReadyState> pair
                in _bloodSpellInnerLightReadyStates)
            {
                bool mainHand;
                bool offHand;
                GetBloodSpellInnerLightHandFlags(
                    pair.Value == null ? null : pair.Value.Hand,
                    out mainHand,
                    out offHand);
                if ((hand == BloodSpellInnerLightHand.MainHand && mainHand)
                    || (hand == BloodSpellInnerLightHand.OffHand && offHand))
                {
                    remove.Add(pair.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _bloodSpellInnerLightReadyStates.Remove(remove[i]);
            }

            remove.Clear();
            foreach (KeyValuePair<object, StrongCastState> pair
                in _strongCastStates)
            {
                bool mainHand;
                bool offHand;
                GetBloodSpellInnerLightHandFlags(
                    pair.Value == null ? null : pair.Value.Hand,
                    out mainHand,
                    out offHand);
                if ((hand == BloodSpellInnerLightHand.MainHand && mainHand)
                    || (hand == BloodSpellInnerLightHand.OffHand && offHand))
                {
                    remove.Add(pair.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _strongCastStates.Remove(remove[i]);
            }

            BloodSpellInnerLightHandState handState =
                hand == BloodSpellInnerLightHand.MainHand
                    ? _bloodSpellInnerLightMainHandState
                    : _bloodSpellInnerLightOffHandState;
            handState.ImmediateFadeOutRequested = true;
            handState.CastBoostState.Clear();
            handState.CastBoostFactor = 1.0f;
            ClearUnusedBloodSpellInnerLightCastBoostStates();
            LogBloodSpellInnerLightDiagnostic(
                "hand="
                + hand
                + " was suspended while Versatile Weapons used the opposite weapon with both hands.");
        }

        private bool IsVersatileWeaponsMagicFsmSuppressed(object magicFsm)
        {
            bool mainHand;
            bool offHand;
            GetBloodSpellInnerLightHandFlags(
                GetHandKey(magicFsm),
                out mainHand,
                out offHand);
            return (mainHand
                    && IsVersatileWeaponsHandSuppressed(
                        BloodSpellInnerLightHand.MainHand))
                || (offHand
                    && IsVersatileWeaponsHandSuppressed(
                        BloodSpellInnerLightHand.OffHand));
        }

        private void ObserveBloodSpellInnerLightEquippedItems()
        {
            object hero = GetHero();
            if (hero == null)
            {
                return;
            }

            ObserveBloodSpellInnerLightEquippedItem(
                _bloodSpellInnerLightMainHandState,
                GetPropertyValue(hero, "MainHandItem"));
            ObserveBloodSpellInnerLightEquippedItem(
                _bloodSpellInnerLightOffHandState,
                GetPropertyValue(hero, "OffHandItem"));
        }

        private void ObserveBloodSpellInnerLightEquippedItem(
            BloodSpellInnerLightHandState handState,
            object equippedItem)
        {
            if (handState.EquipmentObservationInitialized
                && ReferenceEquals(handState.ObservedEquippedItem, equippedItem))
            {
                return;
            }

            handState.EquipmentObservationInitialized = true;
            handState.ObservedEquippedItem = equippedItem;
            if (equippedItem == null)
            {
                return;
            }

            BloodSpellInnerLightSpellKind spellKind =
                ClassifyBloodSpellInnerLightItem(equippedItem);
            bool supportsLight = spellKind != BloodSpellInnerLightSpellKind.None;
            bool switchedFromBloodToNonBlood =
                handState.HasObservedNonNullEquippedItem
                && handState.LastNonNullEquippedSpellKind
                    != BloodSpellInnerLightSpellKind.None
                && !supportsLight;

            handState.HasObservedNonNullEquippedItem = true;
            handState.LastNonNullEquippedSpellKind = spellKind;
            handState.SuppressForNonBloodEquipment = !supportsLight;
            if (!switchedFromBloodToNonBlood)
            {
                return;
            }

            handState.ImmediateFadeOutRequested = true;
            LogBloodSpellInnerLightDiagnostic(
                "hand="
                + handState.Hand
                + " switched from a blood spell to non-blood equipment; fading its light instantly.");
        }

        private float GetBloodSpellInnerLightTargetBrightness(BloodSpellInnerLightHandState handState)
        {
            float boostFactor = UpdateBloodSpellInnerLightCastBoostFactor(handState);
            if (!ShouldShowBloodSpellInnerLight(handState))
            {
                return 0.0f;
            }

            float brightness = GetBloodSpellInnerLightIntensity();
            brightness *= GetBloodSpellInnerLightSpellIntensityMultiplier(handState);
            if (IsBloodSpellInnerLightInterior())
            {
                brightness *= GetBloodSpellInnerLightInteriorIntensityMultiplier();
            }

            return brightness * boostFactor * GetBloodSpellInnerLightPowerBrightnessMultiplier();
        }

        private float UpdateBloodSpellInnerLightCastBoostFactor(BloodSpellInnerLightHandState handState)
        {
            float now = Now;
            BloodSpellInnerLightCastBoostState boostState = handState.CastBoostState;
            bool boostActive = boostState.HasWindow &&
                now >= boostState.StartAt &&
                now <= boostState.ActiveUntil;
            float target = boostActive
                ? BloodSpellInnerLightCastBoostMultiplier
                : 1.0f;
            float rampSeconds = target > handState.CastBoostFactor
                ? Math.Max(0.01f, BloodSpellInnerLightCastBoostRampUpSeconds)
                : Math.Max(0.01f, BloodSpellInnerLightCastBoostRampDownSeconds);
            float maxDelta = Time.unscaledDeltaTime *
                (BloodSpellInnerLightCastBoostMultiplier - 1.0f) /
                rampSeconds;
            handState.CastBoostFactor = Mathf.MoveTowards(
                handState.CastBoostFactor,
                target,
                maxDelta);

            if (!boostActive &&
                boostState.HasWindow &&
                now > Math.Max(
                    boostState.ActiveUntil,
                    boostState.FinishedSuppressionUntil) + rampSeconds &&
                handState.CastBoostFactor <= 1.0001f)
            {
                boostState.Clear();
            }

            return handState.CastBoostFactor;
        }

        private bool EnsureBloodSpellInnerLight(BloodSpellInnerLightHandState handState)
        {
            Transform handTransform = GetBloodSpellInnerLightHandTransform(handState);
            if (handState.LightObject == null)
            {
                if (handTransform == null)
                {
                    return false;
                }

                handState.LightObject = new GameObject(handState.ObjectName);
                handState.LightObject.SetActive(false);
                handState.LightObject.transform.position = handTransform.position;
                handState.Light = handState.LightObject.AddComponent<Light>();
                if (!handState.LoggedCreated)
                {
                    handState.LoggedCreated = true;
                    LogBloodSpellInnerLightDiagnostic(
                        "created custom "
                        + handState.Hand
                        + " Light object; vanilla HeroLight is not modified.");
                }
            }
            else if (handState.Light == null)
            {
                handState.Light = handState.LightObject.GetComponent<Light>();
                if (handState.Light == null)
                {
                    handState.Light = handState.LightObject.AddComponent<Light>();
                }
                handState.HdrpData = null;
            }

            if (handState.LightObject.transform.parent != null)
            {
                handState.LightObject.transform.SetParent(null, true);
                LogBloodSpellInnerLightDiagnostic(
                    "detached "
                    + handState.Hand
                    + " light for world-space hand following.");
            }

            if (handState.HdrpData == null)
            {
                ConfigureBloodSpellInnerLight(handState);
            }
            return true;
        }

        private Transform GetBloodSpellInnerLightHandTransform(BloodSpellInnerLightHandState handState)
        {
            if (handState.Anchor != null)
            {
                return handState.Anchor;
            }

            float now = Now;
            if (now < handState.NextAnchorProbeTime)
            {
                return null;
            }

            handState.NextAnchorProbeTime = now + BloodSpellInnerLightAnchorRetryIntervalSeconds;
            object hero = GetHero();
            string handPropertyName = handState.Hand == BloodSpellInnerLightHand.MainHand
                ? "MainHand"
                : "OffHand";
            handState.Anchor = GetPropertyValue(hero, handPropertyName) as Transform;
            handState.AnchorPropertyName = handPropertyName;
            if (handState.Anchor == null)
            {
                object controller = GetPropertyValue(hero, "VHeroController");
                string wristPropertyName = handState.Hand == BloodSpellInnerLightHand.MainHand
                ? "MainHandWrist"
                : "OffHandWrist";
                handState.Anchor = GetPropertyValue(
                    controller,
                    wristPropertyName) as Transform;
                handState.AnchorPropertyName = wristPropertyName;
            }

            if (handState.Anchor == null)
            {
                handState.AnchorPropertyName = null;
                return null;
            }

            LogBloodSpellInnerLightDiagnostic(
                "resolved "
                + handState.Hand
                + " "
                + handState.AnchorPropertyName
                + " marker "
                + DescribeTransform(handState.Anchor)
                + ".");
            return handState.Anchor;
        }

        private static bool ShouldUpdateBloodSpellInnerLightPosition(
            BloodSpellInnerLightHandState handState)
        {
            return handState.LightObject != null
                && handState.LightObject.activeSelf;
        }

        private void UpdateBloodSpellInnerLightPosition(
            BloodSpellInnerLightHandState handState,
            Vector3 visualWorldOffset)
        {
            if (!ShouldUpdateBloodSpellInnerLightPosition(handState))
            {
                return;
            }

            Transform handTransform =
                GetBloodSpellInnerLightHandTransform(handState);
            if (handTransform == null)
            {
                return;
            }

            Transform lightTransform = handState.LightObject.transform;
            if (lightTransform.parent != null)
            {
                lightTransform.SetParent(null, true);
            }

            lightTransform.position = handTransform.position + visualWorldOffset;
        }

        private bool TryGetFirstPersonArmsVisualWorldOffset(
            out Vector3 visualWorldOffset)
        {
            visualWorldOffset = Vector3.zero;
            if (!TryResolveFirstPersonArmsAdjusterBridge())
            {
                return false;
            }

            try
            {
                return _tryGetFirstPersonArmsVisualWorldOffset(
                    out visualWorldOffset);
            }
            catch (Exception exception)
            {
                _tryGetFirstPersonArmsVisualWorldOffset = null;
                LogFirstPersonArmsAdjusterUnavailableOnce(
                    "First Person Arms Adjuster visual-offset API failed: "
                    + exception.GetBaseException().Message
                    + ".");
                visualWorldOffset = Vector3.zero;
                return false;
            }
        }

        private bool TryResolveFirstPersonArmsAdjusterBridge()
        {
            if (_firstPersonArmsAdjusterBridgeResolved)
            {
                return _tryGetFirstPersonArmsVisualWorldOffset != null;
            }

            _firstPersonArmsAdjusterBridgeResolved = true;

            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    FirstPersonArmsAdjusterPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                FirstPersonArmsAdjusterApiTypeName,
                false);
            MethodInfo method = apiType == null
                ? null
                : apiType.GetMethod(
                    "TryGetCurrentVisualWorldOffset",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Vector3).MakeByRefType() },
                    null);
            if (method == null || method.ReturnType != typeof(bool))
            {
                LogFirstPersonArmsAdjusterUnavailableOnce(
                    "First Person Arms Adjuster is loaded, but its visual-offset API could not be found.");
                return false;
            }

            try
            {
                _tryGetFirstPersonArmsVisualWorldOffset =
                    (TryGetFirstPersonArmsVisualWorldOffsetDelegate)
                        Delegate.CreateDelegate(
                            typeof(TryGetFirstPersonArmsVisualWorldOffsetDelegate),
                            method);
                LogBloodSpellInnerLightDiagnostic(
                    "connected to the First Person Arms Adjuster visual-offset API.");
                return true;
            }
            catch (Exception exception)
            {
                LogFirstPersonArmsAdjusterUnavailableOnce(
                    "First Person Arms Adjuster visual-offset API binding failed: "
                    + exception.GetBaseException().Message
                    + ".");
                return false;
            }
        }

        private void LogFirstPersonArmsAdjusterUnavailableOnce(string message)
        {
            if (_firstPersonArmsAdjusterUnavailableLogged)
            {
                return;
            }

            _firstPersonArmsAdjusterUnavailableLogged = true;
            Warn(message);
        }

        private bool IsBloodSpellInnerLightInterior()
        {
            try
            {
                SceneService sceneService = World.Services.TryGet<SceneService>();
                return sceneService != null && !sceneService.IsOpenWorld;
            }
            catch (Exception exception)
            {
                LogBloodSpellInnerLightDiagnosticThrottled(
                    "could not resolve interior state: "
                    + exception.Message
                    + ".");
                return false;
            }
        }

        private void ConfigureBloodSpellInnerLight(BloodSpellInnerLightHandState handState)
        {
            if (handState.Light == null)
            {
                return;
            }

            handState.Light.type = LightType.Point;
            handState.Light.color = BloodSpellInnerLightColor;
            handState.Light.shadows = LightShadows.None;
            handState.Light.bounceIntensity = 0.0f;
            handState.Light.cullingMask = ~0;
            handState.Light.renderMode = LightRenderMode.Auto;
            try
            {
                handState.HdrpData = handState.Light
                    .GetComponent<HDAdditionalLightData>();
                if (handState.HdrpData == null)
                {
                    handState.HdrpData = handState.LightObject
                        .AddComponent<HDAdditionalLightData>();
                    LogBloodSpellInnerLightDiagnostic(
                        "added HDRP additional light data to "
                        + handState.Hand
                        + " Light object.");
                }
                handState.HdrpData.lightDimmer = 1.0f;
                handState.HdrpData.volumetricDimmer = 0.0f;
                handState.HdrpData.affectDiffuse = true;
                handState.HdrpData.affectSpecular = true;
                handState.HdrpData.EnableShadows(false);
                handState.HdrpData.shadowDimmer = 0.0f;
                handState.HdrpData.volumetricShadowDimmer = 0.0f;
            }
            catch (Exception exception)
            {
                LogBloodSpellInnerLightDiagnosticThrottled(
                    "HDRP additional light data setup failed: "
                    + exception.Message);
            }
        }

        private void DestroyBloodSpellInnerLight()
        {
            DestroyBloodSpellInnerLight(_bloodSpellInnerLightMainHandState);
            DestroyBloodSpellInnerLight(_bloodSpellInnerLightOffHandState);
        }

        private static void DestroyBloodSpellInnerLight(BloodSpellInnerLightHandState handState)
        {
            if (handState.LightObject != null)
            {
                UnityEngine.Object.Destroy(handState.LightObject);
            }

            handState.LightObject = null;
            handState.Light = null;
            handState.HdrpData = null;
            handState.Anchor = null;
            handState.AnchorPropertyName = null;
            handState.NextAnchorProbeTime = 0.0f;
            handState.LastVisible = false;
            handState.EquipmentObservationInitialized = false;
            handState.ObservedEquippedItem = null;
            handState.HasObservedNonNullEquippedItem = false;
            handState.LastNonNullEquippedSpellKind = BloodSpellInnerLightSpellKind.None;
            handState.SuppressForNonBloodEquipment = false;
            handState.ImmediateFadeOutRequested = false;
            handState.CastBoostState.Clear();
            handState.CastBoostFactor = 1.0f;
        }

        internal void RegisterBloodSpellWeaponsShown(object magicFsm, bool instant)
        {
            if (!_enabled.Value || magicFsm == null)
            {
                return;
            }

            if (IsVersatileWeaponsMagicFsmSuppressed(magicFsm))
            {
                _bloodSpellInnerLightReadyStates.Remove(magicFsm);
                _strongCastStates.Remove(magicFsm);
                return;
            }

            bool isBloodMagicSpell;
            bool isAbhartach;
            string summary;
            if (!TryGetBloodSpellInnerLightCandidate(
                magicFsm,
                out isBloodMagicSpell,
                out isAbhartach,
                out summary))
            {
                ClearBloodSpellInnerLightReadyState(
                    magicFsm,
                    "shown weapon is not a blood spell",
                    false);
                return;
            }

            string hand = MarkBloodSpellInnerLightReadied(
                magicFsm,
                summary,
                BloodSpellInnerLightReadyGraceSeconds);

            LogBloodSpellInnerLightTransition(
                "readied from MagicFSM.OnShowWeapons",
                isBloodMagicSpell,
                isAbhartach,
                summary,
                "instant=" + instant + ", hand=" + hand);
        }

        internal void RegisterBloodSpellWeaponsHidden(object magicFsm, bool instant)
        {
            if (magicFsm == null)
            {
                return;
            }

            BloodSpellInnerLightReadyState state;
            if (!_bloodSpellInnerLightReadyStates.TryGetValue(magicFsm, out state))
            {
                return;
            }

            string currentState = GetStringProperty(magicFsm, "CurrentStateType");
            string stateToEnter = GetStringProperty(magicFsm, "CurrentStateToEnterType");
            bool sheathed =
                string.Equals(currentState, "UnEquipWeapon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentState, "UnEquipWeaponAlternate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateToEnter, "UnEquipWeapon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateToEnter, "UnEquipWeaponAlternate", StringComparison.OrdinalIgnoreCase);
            if (sheathed)
            {
                ClearBloodSpellInnerLightReadyState(
                    magicFsm,
                    "blood spell weapons entered the sheathed state",
                    false);
            }

            LogBloodSpellInnerLightDiagnosticThrottled(
                "hidden from MagicFSM.OnHideWeapons; summary="
                + state.Summary
                + ", instant="
                + instant
                + ", hand="
                + state.Hand
                + ", sheathed="
                + sheathed
                + ", state="
                + FormatStateName(currentState)
                + ", enter="
                + FormatStateName(stateToEnter)
                + ".");
        }

        private void ClearBloodSpellInnerLightReadyState(
            object magicFsm,
            string reason,
            bool log)
        {
            if (magicFsm == null ||
                !_bloodSpellInnerLightReadyStates.ContainsKey(magicFsm))
            {
                return;
            }

            _bloodSpellInnerLightReadyStates.Remove(magicFsm);
            ClearUnusedBloodSpellInnerLightCastBoostStates();

            if (log)
            {
                LogBloodSpellInnerLightDiagnosticThrottled(
                    "cleared readied blood spell inner light state: "
                    + reason
                    + ".");
            }
        }

        private void RegisterBloodSpellInnerLightCastBoost(
            string context,
            object magicFsm,
            bool isBloodMagicSpell,
            bool isAbhartach,
            string summary,
            string stateSummary,
            BloodSpellInnerLightCastBoostEvent boostEvent)
        {
            float now = Now;
            bool wasReadied = HasBloodSpellInnerLightReadiedState(magicFsm) ||
                HasBloodSpellInnerLightReadiedState();
            string hand = MarkBloodSpellInnerLightReadied(
                magicFsm,
                summary,
                BloodSpellInnerLightReadyGraceSeconds);

            if (boostEvent == BloodSpellInnerLightCastBoostEvent.Finished)
            {
                FinishBloodSpellInnerLightCastBoostWindow(magicFsm, hand, now);
            }
            else
            {
                RefreshBloodSpellInnerLightCastBoostWindow(
                    magicFsm,
                    hand,
                    now,
                    boostEvent == BloodSpellInnerLightCastBoostEvent.Started);
            }

            if (!ShouldLogBloodSpellInnerLightDiagnostics() ||
                _bloodSpellInnerLightActivationLogsRemaining <= 0)
            {
                return;
            }

            _bloodSpellInnerLightActivationLogsRemaining--;
            LogBloodSpellInnerLightDiagnostic(
                "cast boost event from "
                + context
                + "; bloodSpell="
                + isBloodMagicSpell
                + ", abhartach="
                + isAbhartach
                + ", summary="
                + summary
                + ", "
                + stateSummary
                + ", event="
                + boostEvent
                + ", multiplier="
                + FormatFloat(BloodSpellInnerLightCastBoostMultiplier)
                + ", wasReadied="
                + wasReadied
                + ", hand="
                + hand
                + ", boostWindows="
                + DescribeBloodSpellInnerLightCastBoostWindows(hand)
                + ".");
        }

        private void RefreshBloodSpellInnerLightCastBoostWindow(
            object magicFsm,
            string hand,
            float now,
            bool restartWindow)
        {
            bool mainHand;
            bool offHand;
            GetBloodSpellInnerLightHandFlags(hand, out mainHand, out offHand);
            if (mainHand)
            {
                RefreshBloodSpellInnerLightCastBoostWindow(
                    _bloodSpellInnerLightMainHandState,
                    magicFsm,
                    now,
                    restartWindow);
            }
            if (offHand)
            {
                RefreshBloodSpellInnerLightCastBoostWindow(
                    _bloodSpellInnerLightOffHandState,
                    magicFsm,
                    now,
                    restartWindow);
            }
        }

        private void RefreshBloodSpellInnerLightCastBoostWindow(
            BloodSpellInnerLightHandState handState,
            object magicFsm,
            float now,
            bool restartWindow)
        {
            BloodSpellInnerLightCastBoostState boostState = handState.CastBoostState;
            if (restartWindow)
            {
                boostState.ClearFinishedSuppression();
            }
            else if (IsBloodSpellInnerLightCastBoostFinishedSuppressed(handState, now))
            {
                return;
            }

            if (restartWindow ||
                !boostState.HasWindow ||
                now > boostState.ActiveUntil + BloodSpellInnerLightCastBoostRampDownSeconds)
            {
                boostState.HasWindow = true;
                boostState.StartAt =
                    now + Math.Max(0.0f, BloodSpellInnerLightCastBoostStartDelaySeconds);
                boostState.ActiveUntil = boostState.StartAt;
            }

            float evidenceGrace = GetBloodSpellInnerLightCastBoostEvidenceGraceSeconds();
            float activeUntil = Math.Max(
                now + evidenceGrace,
                boostState.StartAt + evidenceGrace);
            boostState.ActiveUntil = Math.Max(
                boostState.ActiveUntil,
                activeUntil);
        }

        private void FinishBloodSpellInnerLightCastBoostWindow(
            object magicFsm,
            string hand,
            float now)
        {
            bool mainHand;
            bool offHand;
            GetBloodSpellInnerLightHandFlags(hand, out mainHand, out offHand);
            if (mainHand)
            {
                FinishBloodSpellInnerLightCastBoostWindow(
                    _bloodSpellInnerLightMainHandState,
                    magicFsm,
                    now);
            }
            if (offHand)
            {
                FinishBloodSpellInnerLightCastBoostWindow(
                    _bloodSpellInnerLightOffHandState,
                    magicFsm,
                    now);
            }
        }

        private static void FinishBloodSpellInnerLightCastBoostWindow(
            BloodSpellInnerLightHandState handState,
            object magicFsm,
            float now)
        {
            BloodSpellInnerLightCastBoostState boostState = handState.CastBoostState;
            if (!boostState.HasWindow)
            {
                boostState.HasWindow = true;
                boostState.StartAt = now;
                boostState.ActiveUntil = now;
            }
            else
            {
                boostState.ActiveUntil = Math.Min(boostState.ActiveUntil, now);
            }

            boostState.FinishedMagicFsm = magicFsm;
            boostState.FinishedSuppressionUntil =
                now + GetBloodSpellInnerLightCastBoostFinishedSuppressionSeconds();
        }

        private float GetBloodSpellInnerLightCastBoostEvidenceGraceSeconds()
        {
            float probeGrace = 0.10f;
            if (_holdTrackerIntervalSeconds != null)
            {
                probeGrace = Math.Max(probeGrace, _holdTrackerIntervalSeconds.Value + 0.05f);
            }

            return Math.Max(
                probeGrace,
                BloodSpellInnerLightCastBoostRampUpSeconds -
                    BloodSpellInnerLightCastBoostFinishLeadSeconds);
        }

        private static float GetBloodSpellInnerLightCastBoostFinishedSuppressionSeconds()
        {
            return Math.Max(
                1.0f,
                BloodSpellInnerLightCastBoostRampDownSeconds +
                    BloodSpellInnerLightCastBoostFinishLeadSeconds);
        }

        private bool IsBloodSpellInnerLightCastBoostFinishedSuppressed(object magicFsm, float now)
        {
            string hand = GetHandKey(magicFsm);
            bool mainHand;
            bool offHand;
            GetBloodSpellInnerLightHandFlags(hand, out mainHand, out offHand);
            bool found = mainHand || offHand;
            bool allSuppressed = true;
            if (mainHand)
            {
                allSuppressed &= IsBloodSpellInnerLightCastBoostFinishedSuppressed(
                    _bloodSpellInnerLightMainHandState,
                    now);
            }
            if (offHand)
            {
                allSuppressed &= IsBloodSpellInnerLightCastBoostFinishedSuppressed(
                    _bloodSpellInnerLightOffHandState,
                    now);
            }

            return found && allSuppressed;
        }

        private static bool IsBloodSpellInnerLightCastBoostFinishedSuppressed(
            BloodSpellInnerLightHandState handState,
            float now)
        {
            BloodSpellInnerLightCastBoostState boostState = handState.CastBoostState;
            if (boostState.FinishedSuppressionUntil <= 0.0f)
            {
                return false;
            }

            if (boostState.FinishedSuppressionUntil < now)
            {
                boostState.ClearFinishedSuppression();
                return false;
            }

            return true;
        }

        private string DescribeBloodSpellInnerLightCastBoostWindows(string hand)
        {
            bool mainHand;
            bool offHand;
            GetBloodSpellInnerLightHandFlags(hand, out mainHand, out offHand);
            StringBuilder builder = new StringBuilder();
            if (mainHand)
            {
                AppendBloodSpellInnerLightCastBoostWindow(
                    builder,
                    _bloodSpellInnerLightMainHandState);
            }
            if (offHand)
            {
                AppendBloodSpellInnerLightCastBoostWindow(
                    builder,
                    _bloodSpellInnerLightOffHandState);
            }

            return builder.Length == 0 ? "<unknown>" : builder.ToString();
        }

        private void AppendBloodSpellInnerLightCastBoostWindow(
            StringBuilder builder,
            BloodSpellInnerLightHandState handState)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(handState.Hand)
                .Append('(')
                .Append(FormatFloat(handState.CastBoostState.StartAt))
                .Append("-")
                .Append(FormatFloat(handState.CastBoostState.ActiveUntil))
                .Append(')');
        }

        private void LogBloodSpellInnerLightTransition(
            string context,
            bool isBloodMagicSpell,
            bool isAbhartach,
            string summary,
            string stateSummary)
        {
            if (!ShouldLogBloodSpellInnerLightDiagnostics() ||
                _bloodSpellInnerLightActivationLogsRemaining <= 0)
            {
                return;
            }

            _bloodSpellInnerLightActivationLogsRemaining--;
            LogBloodSpellInnerLightDiagnostic(
                context
                + "; bloodSpell="
                + isBloodMagicSpell
                + ", abhartach="
                + isAbhartach
                + ", summary="
                + summary
                + ", "
                + stateSummary
                + ".");
        }

        private bool TryGetBloodSpellInnerLightCandidate(
            object magicFsm,
            out bool isBloodMagicSpell,
            out bool isAbhartach,
            out string summary)
        {
            isBloodMagicSpell = false;
            isAbhartach = false;
            summary = string.Empty;

            object item = GetPropertyValue(magicFsm, "Item");
            object skill = GetPropertyValue(magicFsm, "Skill");
            string bloodSummary;
            string abhartachSummary;
            isBloodMagicSpell = IsBloodTransfusionItemOrSkill(item, skill, out bloodSummary);
            isAbhartach = IsAbhartachItemOrSkill(item, skill, out abhartachSummary);
            if (!isBloodMagicSpell && !isAbhartach)
            {
                return false;
            }

            summary = isBloodMagicSpell ? bloodSummary : abhartachSummary;
            return true;
        }

        private string MarkBloodSpellInnerLightReadied(
            object magicFsm,
            string summary,
            float graceSeconds)
        {
            BloodSpellInnerLightReadyState state;
            if (!_bloodSpellInnerLightReadyStates.TryGetValue(magicFsm, out state))
            {
                state = new BloodSpellInnerLightReadyState();
                _bloodSpellInnerLightReadyStates[magicFsm] = state;
            }

            state.Summary = summary;
            state.Hand = GetHandKey(magicFsm);
            state.SpellKind = ClassifyBloodSpellInnerLightMagicFsm(
                magicFsm,
                summary);
            state.UpdatedAt = Now;
            state.Until = Math.Max(state.Until, Now + Math.Max(0.01f, graceSeconds));
            return state.Hand;
        }

        private BloodSpellInnerLightSpellKind ClassifyBloodSpellInnerLightMagicFsm(
            object magicFsm,
            string summary)
        {
            if (ContainsBloodSpellInnerLightName(summary, "Abhartach"))
            {
                return BloodSpellInnerLightSpellKind.AbhartachCalling;
            }

            if (IsLifeTransfusionText(summary))
            {
                return BloodSpellInnerLightSpellKind.LifeTransfusion;
            }

            object item = GetPropertyValue(magicFsm, "Item");
            BloodSpellInnerLightSpellKind itemKind =
                ClassifyBloodSpellInnerLightItem(item);
            if (itemKind != BloodSpellInnerLightSpellKind.None)
            {
                return itemKind;
            }

            object skill = GetPropertyValue(magicFsm, "Skill");
            string skillText = BuildObjectSearchText(skill);
            if (ContainsBloodSpellInnerLightName(skillText, "Abhartach"))
            {
                return BloodSpellInnerLightSpellKind.AbhartachCalling;
            }

            return IsLifeTransfusionText(skillText)
                ? BloodSpellInnerLightSpellKind.LifeTransfusion
                : BloodSpellInnerLightSpellKind.BloodTransfusion;
        }

        private BloodSpellInnerLightSpellKind ClassifyBloodSpellInnerLightItem(
            object item)
        {
            string summary;
            if (IsAbhartachItem(item, out summary))
            {
                return BloodSpellInnerLightSpellKind.AbhartachCalling;
            }

            if (!IsBloodTransfusionItem(item, out summary))
            {
                return BloodSpellInnerLightSpellKind.None;
            }

            return IsLifeTransfusionText(summary)
                || IsLifeTransfusionText(BuildObjectSearchText(item))
                ? BloodSpellInnerLightSpellKind.LifeTransfusion
                : BloodSpellInnerLightSpellKind.BloodTransfusion;
        }

        private static bool IsLifeTransfusionText(string text)
        {
            return ContainsBloodSpellInnerLightName(text, "LifeTransfusion")
                || ContainsBloodSpellInnerLightName(text, "Life Transfusion");
        }

        private static bool ContainsBloodSpellInnerLightName(
            string text,
            string name)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool HasBloodSpellInnerLightReadiedState()
        {
            if (_bloodSpellInnerLightReadyStates.Count == 0)
            {
                return false;
            }

            foreach (KeyValuePair<object, BloodSpellInnerLightReadyState> pair in _bloodSpellInnerLightReadyStates)
            {
                if (pair.Value != null &&
                    pair.Value.Until >= Now &&
                    !IsDestroyedUnityObject(pair.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasBloodSpellInnerLightReadiedState(object magicFsm)
        {
            BloodSpellInnerLightReadyState state;
            return magicFsm != null &&
                _bloodSpellInnerLightReadyStates.TryGetValue(magicFsm, out state) &&
                state != null &&
                state.Until >= Now &&
                !IsDestroyedUnityObject(magicFsm);
        }

        private bool HasBloodSpellInnerLightReadiedState(
            BloodSpellInnerLightHandState handState)
        {
            foreach (KeyValuePair<object, BloodSpellInnerLightReadyState> pair in _bloodSpellInnerLightReadyStates)
            {
                if (pair.Value != null &&
                    pair.Value.Until >= Now &&
                    !IsDestroyedUnityObject(pair.Key) &&
                    BloodSpellInnerLightReadyStateMatchesHand(pair.Value, handState.Hand))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BloodSpellInnerLightReadyStateMatchesHand(
            BloodSpellInnerLightReadyState readyState,
            BloodSpellInnerLightHand hand)
        {
            bool mainHand;
            bool offHand;
            GetBloodSpellInnerLightHandFlags(
                readyState == null ? null : readyState.Hand,
                out mainHand,
                out offHand);
            return hand == BloodSpellInnerLightHand.MainHand
                ? mainHand
                : offHand;
        }

        private void ClearUnusedBloodSpellInnerLightCastBoostStates()
        {
            if (!HasBloodSpellInnerLightReadiedState(_bloodSpellInnerLightMainHandState))
            {
                _bloodSpellInnerLightMainHandState.CastBoostState.Clear();
            }
            if (!HasBloodSpellInnerLightReadiedState(_bloodSpellInnerLightOffHandState))
            {
                _bloodSpellInnerLightOffHandState.CastBoostState.Clear();
            }
        }

        private static void GetBloodSpellInnerLightHandFlags(
            string hand,
            out bool mainHand,
            out bool offHand)
        {
            string value = hand ?? string.Empty;
            bool both = value.IndexOf("Both", StringComparison.OrdinalIgnoreCase) >= 0;
            mainHand = both ||
                value.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0;
            offHand = both ||
                value.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsBloodSpellInnerLightMagicLayerReadied(
            object magicFsm,
            out string stateSummary)
        {
            bool foundLayerActive;
            bool layerActive = GetBoolProperty(
                magicFsm,
                "IsLayerActive",
                false,
                out foundLayerActive);
            string currentState = GetStringProperty(magicFsm, "CurrentStateType");
            string stateToEnter = GetStringProperty(magicFsm, "CurrentStateToEnterType");
            string generalState = GetStringProperty(magicFsm, "GeneralStateType");

            stateSummary =
                "layerActive="
                + layerActive
                + ", state="
                + FormatStateName(currentState)
                + ", enter="
                + FormatStateName(stateToEnter)
                + ", general="
                + FormatStateName(generalState);

            if (!foundLayerActive || !layerActive)
            {
                return false;
            }

            if (IsBloodSpellInnerLightSheathedState(currentState) ||
                IsBloodSpellInnerLightSheathedState(stateToEnter) ||
                string.Equals(generalState, "UnEquip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool IsBloodSpellInnerLightSheathedState(string stateName)
        {
            return string.IsNullOrEmpty(stateName) ||
                string.Equals(stateName, "UnEquipWeapon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "UnEquipWeaponAlternate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "Empty", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "Invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "None", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatStateName(string stateName)
        {
            return string.IsNullOrEmpty(stateName) ? "<unknown>" : stateName;
        }

        private void LogBloodSpellInnerLightDiagnostic(string message)
        {
            if (!ShouldLogBloodSpellInnerLightDiagnostics())
            {
                return;
            }

            Log.LogInfo("Blood spell inner light diagnostic: " + message);
        }

        private void LogBloodSpellInnerLightDiagnosticThrottled(string message)
        {
            if (!ShouldLogBloodSpellInnerLightDiagnostics())
            {
                return;
            }

            float now = Now;
            if (now < _nextBloodSpellInnerLightDiagnosticTime)
            {
                return;
            }

            _nextBloodSpellInnerLightDiagnosticTime = now + BloodSpellInnerLightDiagnosticIntervalSeconds;
            Log.LogInfo("Blood spell inner light diagnostic: " + message);
        }

        private bool ShouldLogBloodSpellInnerLightDiagnostics()
        {
            return DiagnosticsEnabled();
        }

        private string DescribeTransform(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string objectName = transform.gameObject == null ? "<no gameObject>" : transform.gameObject.name;
            return objectName + " path='" + BuildTransformPath(transform) + "'";
        }

        private string FormatVector(Vector3 value)
        {
            return "("
                + FormatFloat(value.x)
                + ", "
                + FormatFloat(value.y)
                + ", "
                + FormatFloat(value.z)
                + ")";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private float GetBloodSpellInnerLightIntensity()
        {
            return _bloodSpellInnerLightIntensity == null
                ? 0.0f
                : Math.Max(0.0f, _bloodSpellInnerLightIntensity.Value);
        }

        private float GetBloodSpellInnerLightSpellIntensityMultiplier(
            BloodSpellInnerLightHandState handState)
        {
            BloodSpellInnerLightSpellKind spellKind =
                handState.LastNonNullEquippedSpellKind;
            if (spellKind == BloodSpellInnerLightSpellKind.None)
            {
                float newestUpdate = float.MinValue;
                foreach (KeyValuePair<object, BloodSpellInnerLightReadyState> pair
                    in _bloodSpellInnerLightReadyStates)
                {
                    BloodSpellInnerLightReadyState readyState = pair.Value;
                    if (readyState != null
                        && readyState.Until >= Now
                        && readyState.UpdatedAt >= newestUpdate
                        && !IsDestroyedUnityObject(pair.Key)
                        && BloodSpellInnerLightReadyStateMatchesHand(
                            readyState,
                            handState.Hand))
                    {
                        spellKind = readyState.SpellKind;
                        newestUpdate = readyState.UpdatedAt;
                    }
                }
            }

            if (spellKind == BloodSpellInnerLightSpellKind.LifeTransfusion)
            {
                return _bloodSpellInnerLightLifeTransfusionIntensityMultiplier == null
                    ? 1.0f
                    : Math.Max(
                        0.0f,
                        _bloodSpellInnerLightLifeTransfusionIntensityMultiplier.Value);
            }

            if (spellKind == BloodSpellInnerLightSpellKind.AbhartachCalling)
            {
                return _bloodSpellInnerLightAbhartachCallingIntensityMultiplier == null
                    ? 1.2f
                    : Math.Max(
                        0.0f,
                        _bloodSpellInnerLightAbhartachCallingIntensityMultiplier.Value);
            }

            return _bloodSpellInnerLightBloodTransfusionIntensityMultiplier == null
                ? 0.8f
                : Math.Max(
                    0.0f,
                    _bloodSpellInnerLightBloodTransfusionIntensityMultiplier.Value);
        }

        private float GetBloodSpellInnerLightInteriorIntensityMultiplier()
        {
            return _bloodSpellInnerLightInteriorIntensityMultiplier == null
                ? 1.0f
                : Math.Max(0.0f, _bloodSpellInnerLightInteriorIntensityMultiplier.Value);
        }

        private static float GetBloodSpellInnerLightRenderIntensity(float configuredIntensity)
        {
            return Math.Max(0.0f, configuredIntensity) *
                BloodSpellInnerLightHdrpIntensityMultiplier;
        }

        private float GetBloodSpellInnerLightRange()
        {
            float minimumRange = _bloodSpellInnerLightMinimumPowerRange == null
                ? 1.5f
                : Math.Max(0.1f, _bloodSpellInnerLightMinimumPowerRange.Value);
            float masteryRange = _bloodSpellInnerLightMasteryRange == null
                ? 3.0f
                : Math.Max(0.1f, _bloodSpellInnerLightMasteryRange.Value);
            float maximumRange = _bloodSpellInnerLightMaximumPowerRange == null
                ? 4.5f
                : Math.Max(0.1f, _bloodSpellInnerLightMaximumPowerRange.Value);
            float power = GetBloodPower();
            if (power <= NormalMaximumBloodPower)
            {
                return Mathf.Lerp(
                    minimumRange,
                    masteryRange,
                    GetBloodPowerNormalVisualProgress01(power));
            }

            return Mathf.Lerp(
                masteryRange,
                maximumRange,
                GetBloodPowerOvermasteryProgress01(power));
        }

        private float GetBloodSpellInnerLightPowerBrightnessMultiplier()
        {
            float minimumMultiplier = _bloodSpellInnerLightMinimumPowerBrightnessMultiplier == null
                ? 0.2f
                : Math.Max(0.0f, _bloodSpellInnerLightMinimumPowerBrightnessMultiplier.Value);
            float masteryMultiplier = _bloodSpellInnerLightMasteryBrightnessMultiplier == null
                ? 2.0f
                : Math.Max(0.0f, _bloodSpellInnerLightMasteryBrightnessMultiplier.Value);
            float maximumMultiplier = _bloodSpellInnerLightMaximumPowerBrightnessMultiplier == null
                ? 3.0f
                : Math.Max(0.0f, _bloodSpellInnerLightMaximumPowerBrightnessMultiplier.Value);
            float power = GetBloodPower();
            if (power <= NormalMaximumBloodPower)
            {
                return Mathf.Lerp(
                    minimumMultiplier,
                    masteryMultiplier,
                    GetBloodPowerNormalVisualProgress01(power));
            }

            return Mathf.Lerp(
                masteryMultiplier,
                maximumMultiplier,
                GetBloodPowerOvermasteryProgress01(power));
        }

        private static float GetBloodPowerNormalVisualProgress01(float power)
        {
            float normalProgress = Mathf.Clamp01(power / NormalMaximumBloodPower);
            return normalProgress * normalProgress * (3.0f - (2.0f * normalProgress));
        }

        private float GetBloodSpellInnerLightFadeSeconds()
        {
            return _bloodSpellInnerLightFadeSeconds == null
                ? 0.0f
                : Math.Max(0.0f, _bloodSpellInnerLightFadeSeconds.Value);
        }

        private void ResetConfigIfSchemaChanged(ConfigFile config)
        {
            if (config == null)
            {
                return;
            }

            string configPath = config.ConfigFilePath;
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if ((string.Equals(currentSection, "1. Core", StringComparison.Ordinal)
                    || string.Equals(currentSection, "General", StringComparison.Ordinal))
                    && line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    int.TryParse(
                        line.Substring(schemaPrefix.Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out storedSchemaVersion);
                    break;
                }
            }

            if (storedSchemaVersion == ConfigSchemaVersion)
            {
                return;
            }

            CapturePreservedConfigValues(
                configPath,
                storedSchemaVersion);

            string backupPath = configPath
                + ".pre-schema-"
                + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + ".bak";

            try
            {
                File.Copy(configPath, backupPath, false);
                File.WriteAllText(configPath, string.Empty);
                config.Clear();
                config.Reload();
                Log.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid, PluginName, storedSchemaVersion, ConfigSchemaVersion);
            }
            catch (Exception ex)
            {
                ClearPendingPreservedConfigValues();

                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, configPath, true);
                        config.Clear();
                        config.Reload();
                    }
                }
                catch (Exception restoreEx)
                {
                    Log.LogError(
                        "Failed to restore Blood Magic Expansion config backup after schema reset failure: "
                        + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Blood Magic Expansion config schema. Original config was left in place when possible.",
                    ex);
            }
        }

        private void CapturePreservedConfigValues(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedConfigValues();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string settingName = line.Substring(0, separatorIndex).Trim();
                string settingId = GetCleanConfigSection(currentSection) + "\n" + settingName;

                if (IsPreservedCalibrationFloat(settingId))
                {
                    float parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedCalibrationFloats[settingId] = parsedValue;
                    }
                }
                else if (IsPreservedDiagnosticBool(settingId))
                {
                    bool parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedDiagnosticBools[settingId] = parsedValue;
                    }
                }
                else if (IsPreservedManualOverride(settingId))
                {
                    string preservedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out preservedValue))
                    {
                        _pendingPreservedManualOverrides[settingId] =
                            preservedValue;
                    }
                }
            }
        }

        private static bool IsPreservedCalibrationFloat(string settingId)
        {
            return string.Equals(settingId, "Blood Spell Inner Light\nIntensity", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nBloodTransfusionIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nLifeTransfusionIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nAbhartachCallingIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nInteriorIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nMinimumPowerBrightnessMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nMasteryBrightnessMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nMaximumPowerBrightnessMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nMinimumPowerRange", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nMasteryRange", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nMaximumPowerRange", StringComparison.Ordinal)
                || string.Equals(settingId, "Blood Spell Inner Light\nFadeSeconds", StringComparison.Ordinal)
                || string.Equals(settingId, "Audio\nCorpseLeechSoundVolume", StringComparison.Ordinal)
                || string.Equals(settingId, "Audio\nCorpseLeechSoundRangeVolume", StringComparison.Ordinal)
                || string.Equals(settingId, "Audio\nCorpseLeechRandomPitchSemitones", StringComparison.Ordinal);
        }

        private static bool IsPreservedManualOverride(string settingId)
        {
            return string.Equals(settingId, "Bloodless Filter\nBloodWhitelistTerms", StringComparison.Ordinal)
                || string.Equals(settingId, "Advanced - Matching\nBloodSpellTemplateGuid", StringComparison.Ordinal)
                || string.Equals(settingId, "Advanced - Matching\nAbhartachTemplateGuid", StringComparison.Ordinal);
        }

        private static bool IsPreservedDiagnosticBool(string settingId)
        {
            return string.Equals(
                settingId,
                "Diagnostics\nShowGrailFloatingTextDiagnostics",
                StringComparison.Ordinal);
        }

        private void RestorePreservedConfigValues()
        {
            if (_pendingPreservedCalibrationFloats.Count == 0
                && _pendingPreservedDiagnosticBools.Count == 0
                && _pendingPreservedManualOverrides.Count == 0
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreservedFloat("Blood Spell Inner Light\nIntensity", _bloodSpellInnerLightIntensity, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nBloodTransfusionIntensityMultiplier", _bloodSpellInnerLightBloodTransfusionIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nLifeTransfusionIntensityMultiplier", _bloodSpellInnerLightLifeTransfusionIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nAbhartachCallingIntensityMultiplier", _bloodSpellInnerLightAbhartachCallingIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nInteriorIntensityMultiplier", _bloodSpellInnerLightInteriorIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nMinimumPowerBrightnessMultiplier", _bloodSpellInnerLightMinimumPowerBrightnessMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nMasteryBrightnessMultiplier", _bloodSpellInnerLightMasteryBrightnessMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nMaximumPowerBrightnessMultiplier", _bloodSpellInnerLightMaximumPowerBrightnessMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nMinimumPowerRange", _bloodSpellInnerLightMinimumPowerRange, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nMasteryRange", _bloodSpellInnerLightMasteryRange, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nMaximumPowerRange", _bloodSpellInnerLightMaximumPowerRange, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Blood Spell Inner Light\nFadeSeconds", _bloodSpellInnerLightFadeSeconds, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Audio\nCorpseLeechSoundVolume", _corpseLeechSoundVolume, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Audio\nCorpseLeechSoundRangeVolume", _corpseLeechSoundRangeVolume, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Audio\nCorpseLeechRandomPitchSemitones", _corpseLeechRandomPitchSemitones, ref restoredCount, ref clampedCount);
            RestorePreservedBool("Diagnostics\nShowGrailFloatingTextDiagnostics", _showGrailFloatingTextDiagnostics, ref restoredCount);
            RestorePreservedString("Bloodless Filter\nBloodWhitelistTerms", _bloodWhitelistTerms, ref restoredCount);
            RestorePreservedString("Advanced - Matching\nBloodSpellTemplateGuid", _bloodTransfusionTemplateGuid, ref restoredCount);
            RestorePreservedString("Advanced - Matching\nAbhartachTemplateGuid", _abhartachTemplateGuid, ref restoredCount);

            Log.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " calibration/manual override value(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + _pendingPreservedInvalidValueCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            ClearPendingPreservedConfigValues();
        }

        private void RestorePreservedFloat(
            string settingId,
            ConfigEntry<float> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            float preservedValue;
            if (entry == null
                || !_pendingPreservedCalibrationFloats.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
                out clamped))
            {
                _pendingPreservedInvalidValueCount++;
                return;
            }

            if (clamped)
            {
                clampedCount++;
            }
            restoredCount++;
        }

        private void RestorePreservedString(
            string settingId,
            ConfigEntry<string> entry,
            ref int restoredCount)
        {
            string preservedValue;
            if (entry == null
                || !_pendingPreservedManualOverrides.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
                out clamped))
            {
                restoredCount++;
            }
            else
            {
                _pendingPreservedInvalidValueCount++;
            }
        }

        private void RestorePreservedBool(
            string settingId,
            ConfigEntry<bool> entry,
            ref int restoredCount)
        {
            bool preservedValue;
            if (entry == null
                || !_pendingPreservedDiagnosticBools.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
                out clamped))
            {
                restoredCount++;
            }
            else
            {
                _pendingPreservedInvalidValueCount++;
            }
        }

        private void ClearPendingPreservedConfigValues()
        {
            _pendingPreservedCalibrationFloats.Clear();
            _pendingPreservedDiagnosticBools.Clear();
            _pendingPreservedManualOverrides.Clear();
            _pendingPreservedInvalidValueCount = 0;
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            PatchStep("HealthElement damage and corpse aliases", delegate
            {
                Type healthElementType = AccessTools.TypeByName(HealthElementTypeName);
                if (healthElementType == null)
                {
                    Warn("Could not find " + HealthElementTypeName + ". Death-event corpse aliases are unavailable.");
                }
                else
                {
                    PatchMethod(
                        healthElementType,
                        "OnDamage",
                        typeof(HealthElementOnDamagePatch),
                        nameof(HealthElementOnDamagePatch.Prefix),
                        false);
                    PatchMethod(
                        healthElementType,
                        "OnDeathEvents",
                        typeof(DeathEventsPatch),
                        nameof(DeathEventsPatch.Postfix),
                        false);
                    PatchMethod(
                        healthElementType,
                        "BeforeHealthDecreaseEvents",
                        typeof(HealthElementBeforeHealthDecreasePatch),
                        nameof(HealthElementBeforeHealthDecreasePatch.Postfix),
                        false);
                }
            });

            PatchStep("Hero perspective-aware corpse targeting", delegate
            {
                PatchMethod(
                    typeof(VCHeroRaycaster),
                    "OnAttach",
                    typeof(HeroRaycasterAttachedPatch),
                    nameof(HeroRaycasterAttachedPatch.Postfix),
                    false);
                PatchMethod(
                    typeof(VCHeroRaycaster),
                    "OnDiscard",
                    typeof(HeroRaycasterDiscardingPatch),
                    nameof(HeroRaycasterDiscardingPatch.Prefix),
                    false);
            });

            PatchStep("MagicFSM held spell tracking", delegate
            {
                Type magicFsmType = AccessTools.TypeByName(MagicFsmTypeName);
                Type heroAnimatorSubstateMachineType = AccessTools.TypeByName(HeroAnimatorSubstateMachineTypeName);
                if (magicFsmType == null)
                {
                    Warn("Could not find " + MagicFsmTypeName + ". Dual-held Blood Magic Expansion spell detection is unavailable.");
                }
                else
                {
                    if (heroAnimatorSubstateMachineType == null)
                    {
                        Warn("Could not find " + HeroAnimatorSubstateMachineTypeName + ". Blood spell inner light weapon-show detection is unavailable.");
                    }
                    else
                    {
                        PatchMethod(
                            heroAnimatorSubstateMachineType,
                            "OnShowWeapons",
                            typeof(MagicFsmShowWeaponsPatch),
                            nameof(MagicFsmShowWeaponsPatch.Postfix),
                            false);
                    }

                    PatchMethod(
                        magicFsmType,
                        "OnHideWeapons",
                        typeof(MagicFsmHideWeaponsPatch),
                        nameof(MagicFsmHideWeaponsPatch.Postfix),
                        false);
                    PatchMethod(
                        magicFsmType,
                        "TryEnterMagicCastState",
                        typeof(TryEnterMagicCastStatePatch),
                        nameof(TryEnterMagicCastStatePatch.Postfix),
                        false);
                    PatchMethod(
                        magicFsmType,
                        "OnUpdate",
                        typeof(MagicFsmUpdatePatch),
                        nameof(MagicFsmUpdatePatch.Prefix),
                        false);
                    PatchMethod(
                        magicFsmType,
                        "OnUpdate",
                        typeof(MagicFsmUpdatePatch),
                        nameof(MagicFsmUpdatePatch.Postfix),
                        false);
                    PatchMethod(
                        magicFsmType,
                        "EndCasting",
                        typeof(MagicFsmEndCastingPatch),
                        nameof(MagicFsmEndCastingPatch.Prefix),
                        false);
                    PatchMethod(
                        magicFsmType,
                        "CancelCasting",
                        typeof(MagicFsmCancelCastingPatch),
                        nameof(MagicFsmCancelCastingPatch.Prefix),
                        false);
                    PatchMethod(
                        magicFsmType,
                        "OnPerformCast",
                        typeof(MagicFsmPerformCastPatch),
                        nameof(MagicFsmPerformCastPatch.Prefix),
                        false);
                }
            });

            PatchStep("CharacterStatuses bleed tuning", delegate
            {
                Type characterStatusesType = AccessTools.TypeByName(CharacterStatusesTypeName);
                Type buildupStatusType = AccessTools.TypeByName(BuildupStatusTypeName);
                if (characterStatusesType == null)
                {
                    Warn("Could not find " + CharacterStatusesTypeName + ". Blood Magic Expansion bleed buildup tuning is unavailable.");
                }
                else
                {
                    PatchMethod(
                        characterStatusesType,
                        "BuildupStatus",
                        typeof(CharacterStatusesBuildupStatusPatch),
                        nameof(CharacterStatusesBuildupStatusPatch.Prefix),
                        false,
                        nameof(CharacterStatusesBuildupStatusPatch.Finalizer));
                }

                if (buildupStatusType == null)
                {
                    Warn("Could not find " + BuildupStatusTypeName + ". Blood Magic Expansion active Bleed duration tuning is unavailable.");
                }
                else
                {
                    PatchMethod(
                        buildupStatusType,
                        "Buildup",
                        typeof(BuildupStatusBuildupPatch),
                        nameof(BuildupStatusBuildupPatch.Postfix),
                        false);
                    PatchMethod(
                        buildupStatusType,
                        "Decay",
                        typeof(BuildupStatusDecayPatch),
                        nameof(BuildupStatusDecayPatch.Prefix),
                        false);
                }
            });

            PatchStep("FindAlives held target range", delegate
            {
                Type findAlivesType = AccessTools.TypeByName(FindAlivesTypeName);
                if (findAlivesType == null)
                {
                    Warn("Could not find " + FindAlivesTypeName + ". Blood/Life held target-range tuning is unavailable.");
                }
                else
                {
                    PatchMethodTranspiler(
                        findAlivesType,
                        "Collection",
                        typeof(FindAlivesCollectionRangePatch),
                        nameof(FindAlivesCollectionRangePatch.Transpiler),
                        false);
                }
            });

            PatchStep("FindDeadBodies corpse search range", delegate
            {
                Type findDeadBodiesType = AccessTools.TypeByName(FindDeadBodiesTypeName);
                if (findDeadBodiesType == null)
                {
                    Warn("Could not find " + FindDeadBodiesTypeName + ". Abhartach corpse-search range tuning is unavailable.");
                }
                else
                {
                    PatchMethodTranspiler(
                        findDeadBodiesType,
                        "Collection",
                        typeof(FindDeadBodiesCollectionRangePatch),
                        nameof(FindDeadBodiesCollectionRangePatch.Transpiler),
                        false);
                }
            });

            PatchStep("HealFromDeadBodies held corpse range", delegate
            {
                Type healFromDeadBodiesType = AccessTools.TypeByName(HealFromDeadBodiesTypeName);
                if (healFromDeadBodiesType == null)
                {
                    Warn("Could not find " + HealFromDeadBodiesTypeName + ". Abhartach held corpse-search range tuning is unavailable.");
                }
                else
                {
                    PatchConstructorsPrefix(
                        healFromDeadBodiesType,
                        typeof(HealFromDeadBodiesRangePatch),
                        nameof(HealFromDeadBodiesRangePatch.Prefix),
                        false);
                }
            });

            PatchStep("HealingUtils held corpse healing", delegate
            {
                Type healingUtilsType = AccessTools.TypeByName(HealingUtilsTypeName);
                if (healingUtilsType == null)
                {
                    Warn("Could not find " + HealingUtilsTypeName + ". Abhartach's Calling held healing tuning is unavailable.");
                }
                else
                {
                    PatchMethod(
                        healingUtilsType,
                        "TakeHealing",
                        typeof(HealingUtilsTakeHealingPatch),
                        nameof(HealingUtilsTakeHealingPatch.Prefix),
                        false,
                        nameof(HealingUtilsTakeHealingPatch.Finalizer));
                }
            });

            PatchStep("DamageUtils range tuning", delegate
            {
                Type damageUtilsType = AccessTools.TypeByName(DamageUtilsTypeName);
                if (damageUtilsType == null)
                {
                    Warn("Could not find " + DamageUtilsTypeName + ". Blood Magic Expansion range tuning is unavailable.");
                }
                else
                {
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInSphereInstantaneous",
                        typeof(SphereDamageRangePatch),
                        nameof(SphereDamageRangePatch.Prefix),
                        nameof(SphereDamageRangePatch.Postfix),
                        false,
                        nameof(SphereDamageRangePatch.Finalizer));
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInSphereWithAdditionalCheckInstantaneous",
                        typeof(SphereDamageRangePatch),
                        nameof(SphereDamageRangePatch.Prefix),
                        nameof(SphereDamageRangePatch.Postfix),
                        false,
                        nameof(SphereDamageRangePatch.Finalizer));
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInSphereOverTime",
                        typeof(SphereDamageRangePatch),
                        nameof(SphereDamageRangePatch.Prefix),
                        nameof(SphereDamageRangePatch.Postfix),
                        false,
                        nameof(SphereDamageRangePatch.Finalizer));
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInSphereWithAdditionalCheckOverTime",
                        typeof(SphereDamageRangePatch),
                        nameof(SphereDamageRangePatch.Prefix),
                        nameof(SphereDamageRangePatch.Postfix),
                        false,
                        nameof(SphereDamageRangePatch.Finalizer));
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeInstantaneous",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false,
                        nameof(ConeDamageRangePatch.Finalizer));
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeWithAdditionalCheckInstantaneous",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false,
                        nameof(ConeDamageRangePatch.Finalizer));
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeOverTime",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false,
                        nameof(ConeDamageRangePatch.Finalizer));
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeWithAdditionalCheckOverTime",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false,
                        nameof(ConeDamageRangePatch.Finalizer));
                }
            });

            PatchStep("DamageDealingProjectile travel tuning", delegate
            {
                Type projectileType = AccessTools.TypeByName(DamageDealingProjectileTypeName);
                if (projectileType == null)
                {
                    Warn("Could not find " + DamageDealingProjectileTypeName + ". Blood Magic Expansion projectile distance tuning is unavailable.");
                }
                else
                {
                    PatchMethod(
                        projectileType,
                        "SetBaseDamageParams",
                        typeof(DamageDealingProjectilePatch),
                        nameof(DamageDealingProjectilePatch.Postfix),
                        false);
                }

                Type magicProjectileType = AccessTools.TypeByName(MagicProjectileTypeName);
                if (magicProjectileType == null)
                {
                    Warn("Could not find " + MagicProjectileTypeName + ". Blood/Life projectile Bleed attribution is unavailable.");
                }
                else
                {
                    PatchMethodsByName(
                        magicProjectileType,
                        "OnContact",
                        typeof(MagicProjectileImpactPatch),
                        nameof(MagicProjectileImpactPatch.Prefix),
                        nameof(MagicProjectileImpactPatch.Postfix),
                        false,
                        nameof(MagicProjectileImpactPatch.Finalizer));
                }
            });

            PatchStep("Corpse construction and restore tracking", delegate
            {
                Type corpseType = AccessTools.TypeByName(CorpseTypeName);
                if (corpseType == null)
                {
                    Warn("Could not resolve corpse type. Corpse leech will rely on death-event aliases and raycast reflection only.");
                }
                else
                {
                    PatchConstructors(
                        corpseType,
                        typeof(CorpseConstructedPatch),
                        nameof(CorpseConstructedPatch.Postfix),
                        false);
                    PatchMethod(
                        corpseType,
                        "OnRestore",
                        typeof(CorpseRestoredPatch),
                        nameof(CorpseRestoredPatch.Postfix),
                        false);
                }
            });

            PatchStep("Blood magic spell descriptions", delegate
            {
                PatchMethod(
                    typeof(ItemStats),
                    "OnInitialize",
                    typeof(ItemStatsInitializePatch),
                    nameof(ItemStatsInitializePatch.Postfix),
                    true);
                PatchMethod(
                    typeof(MagicItemTemplateInfo),
                    "get_MagicDescription",
                    typeof(MagicDescriptionPatch),
                    nameof(MagicDescriptionPatch.Prefix),
                    true);
            });
        }

        private void PatchStep(string description, Action action)
        {
            try
            {
                if (ShouldLogStartup())
                {
                    Log.LogInfo("Patch step begin: " + description + ".");
                }

                action();

                if (ShouldLogStartup())
                {
                    Log.LogInfo("Patch step finished: " + description + ".");
                }
            }
            catch (Exception ex)
            {
                LogStartupException("Patch step failed: " + description, ex);
            }
        }

        private void PatchMethod(
            Type declaringType,
            string methodName,
            Type patchType,
            string patchMethodName,
            bool required,
            string finalizerMethodName = null)
        {
            MethodInfo original = AccessTools.Method(declaringType, methodName);
            MethodInfo patch = AccessTools.Method(patchType, patchMethodName);
            MethodInfo finalizer = string.IsNullOrEmpty(finalizerMethodName)
                ? null
                : AccessTools.Method(patchType, finalizerMethodName);

            if (original == null
                || patch == null
                || (!string.IsNullOrEmpty(finalizerMethodName) && finalizer == null))
            {
                string message = "Could not patch " + declaringType.FullName + "." + methodName + ".";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            try
            {
                if (patchMethodName == "Prefix")
                {
                    _harmony.Patch(
                        original,
                        prefix: new HarmonyMethod(patch),
                        finalizer: finalizer == null ? null : new HarmonyMethod(finalizer));
                }
                else
                {
                    _harmony.Patch(
                        original,
                        postfix: new HarmonyMethod(patch),
                        finalizer: finalizer == null ? null : new HarmonyMethod(finalizer));
                }
            }
            catch (Exception ex)
            {
                string message = "Failed to patch " + declaringType.FullName + "." + methodName + ": " + ex.GetBaseException().Message;
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            if (DiagnosticsEnabled())
            {
                Log.LogInfo("Patched " + declaringType.FullName + "." + methodName + ".");
            }
        }

        private void PatchMethodTranspiler(
            Type declaringType,
            string methodName,
            Type patchType,
            string patchMethodName,
            bool required)
        {
            MethodInfo original = AccessTools.Method(declaringType, methodName);
            MethodInfo transpiler = AccessTools.Method(patchType, patchMethodName);

            if (original == null || transpiler == null)
            {
                string message = "Could not transpile " + declaringType.FullName + "." + methodName + ".";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            try
            {
                _harmony.Patch(original, null, null, new HarmonyMethod(transpiler));
            }
            catch (Exception ex)
            {
                string message = "Failed to transpile " + declaringType.FullName + "." + methodName + ": " + ex.GetBaseException().Message;
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            if (DiagnosticsEnabled())
            {
                Log.LogInfo("Transpiled " + declaringType.FullName + "." + methodName + ".");
            }
        }

        private void PatchMethodsByName(
            Type declaringType,
            string methodName,
            Type patchType,
            string prefixMethodName,
            string postfixMethodName,
            bool required,
            string finalizerMethodName = null)
        {
            MethodInfo prefix = AccessTools.Method(patchType, prefixMethodName);
            MethodInfo postfix = AccessTools.Method(patchType, postfixMethodName);
            MethodInfo finalizer = string.IsNullOrEmpty(finalizerMethodName)
                ? null
                : AccessTools.Method(patchType, finalizerMethodName);
            if (declaringType == null ||
                prefix == null ||
                postfix == null ||
                (!string.IsNullOrEmpty(finalizerMethodName) && finalizer == null))
            {
                string message = "Could not patch " + (declaringType == null ? "unknown type" : declaringType.FullName) + "." + methodName + ".";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            MethodInfo[] methods = declaringType.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int patched = 0;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo original = methods[i];
                if (original.Name != methodName)
                {
                    continue;
                }

                try
                {
                    _harmony.Patch(
                        original,
                        prefix: new HarmonyMethod(prefix),
                        postfix: new HarmonyMethod(postfix),
                        finalizer: finalizer == null ? null : new HarmonyMethod(finalizer));
                    patched++;
                }
                catch (Exception ex)
                {
                    Warn("Failed to patch " + declaringType.FullName + "." + methodName + ": " + ex.GetBaseException().Message);
                }
            }

            if (patched <= 0)
            {
                string message = "No overloads found for " + declaringType.FullName + "." + methodName + ".";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            if (DiagnosticsEnabled())
            {
                Log.LogInfo("Patched " + patched.ToString(CultureInfo.InvariantCulture) + " overload(s) of " + declaringType.FullName + "." + methodName + ".");
            }
        }

        private void PatchConstructorsPrefix(
            Type declaringType,
            Type patchType,
            string patchMethodName,
            bool required)
        {
            MethodInfo patch = AccessTools.Method(patchType, patchMethodName);
            if (declaringType == null || patch == null)
            {
                string message = "Could not patch " + (declaringType == null ? "unknown" : declaringType.FullName) + " constructors.";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            ConstructorInfo[] constructors = declaringType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int patched = 0;
            for (int i = 0; i < constructors.Length; i++)
            {
                try
                {
                    _harmony.Patch(constructors[i], new HarmonyMethod(patch), null);
                    patched++;
                }
                catch (Exception ex)
                {
                    Warn("Failed to patch " + declaringType.FullName + " constructor: " + ex.GetBaseException().Message);
                }
            }

            if (patched <= 0)
            {
                string message = "No " + declaringType.FullName + " constructors were patched.";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            if (DiagnosticsEnabled())
            {
                Log.LogInfo("Patched " + patched.ToString(CultureInfo.InvariantCulture) + " " + declaringType.FullName + " constructor prefix(es).");
            }
        }

        private void PatchConstructor(Type declaringType, Type[] parameterTypes, Type patchType, string patchMethodName, bool required)
        {
            ConstructorInfo original = AccessTools.Constructor(declaringType, parameterTypes);
            MethodInfo patch = AccessTools.Method(patchType, patchMethodName);

            if (original == null || patch == null)
            {
                string message = "Could not patch " + declaringType.FullName + " constructor.";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            try
            {
                _harmony.Patch(original, null, new HarmonyMethod(patch));
            }
            catch (Exception ex)
            {
                string message = "Failed to patch " + declaringType.FullName + " constructor: " + ex.GetBaseException().Message;
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            if (DiagnosticsEnabled())
            {
                Log.LogInfo("Patched " + declaringType.FullName + " constructor.");
            }
        }

        private void PatchConstructors(Type declaringType, Type patchType, string patchMethodName, bool required)
        {
            MethodInfo patch = AccessTools.Method(patchType, patchMethodName);
            if (declaringType == null || patch == null)
            {
                string message = "Could not patch " + (declaringType == null ? "corpse" : declaringType.FullName) + " constructors.";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            ConstructorInfo[] constructors = declaringType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int patched = 0;
            for (int i = 0; i < constructors.Length; i++)
            {
                try
                {
                    _harmony.Patch(constructors[i], null, new HarmonyMethod(patch));
                    patched++;
                }
                catch (Exception ex)
                {
                    Warn("Failed to patch " + declaringType.FullName + " constructor: " + ex.GetBaseException().Message);
                }
            }

            if (patched <= 0)
            {
                string message = "No " + declaringType.FullName + " constructors were patched.";
                if (required)
                {
                    Log.LogError(message);
                }
                else
                {
                    Warn(message);
                }
                return;
            }

            if (DiagnosticsEnabled())
            {
                Log.LogInfo("Patched " + patched.ToString(CultureInfo.InvariantCulture) + " " + declaringType.FullName + " constructor(s).");
            }
        }

        internal void HandleDeathEvents(object healthElement, object outcome)
        {
            if (!_enabled.Value)
            {
                return;
            }

            DeathContext context;
            if (!TryBuildDeathContext(healthElement, outcome, out context))
            {
                return;
            }

            RememberCorpseSource(context.Target, context.HealthElement);
        }

        internal void HandleCorpseConstructed(object corpse, object[] args)
        {
            if (!_enabled.Value || corpse == null)
            {
                return;
            }

            object npc = FindFirstArgumentByTypeName(args, NpcElementTypeName);
            object character = FindFirstArgumentByTypeName(args, CharacterTypeName);
            if (npc == null && args != null && args.Length > 0)
            {
                npc = args[0];
            }
            if (character == null && args != null && args.Length > 1)
            {
                character = args[1];
            }

            CorpseState state = null;
            if (npc != null)
            {
                TryGetCorpseState(npc, out state);
            }
            if (state == null && character != null)
            {
                TryGetCorpseState(character, out state);
            }
            if (!CanReuseStateForCorpse(state, corpse))
            {
                state = null;
            }
            if (state == null)
            {
                state = CreateCorpseState();
            }

            state.Corpse = corpse;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    UpdateCorpseStateFromSource(state, args[i], null);
                    RegisterCorpseAliases(args[i], state);
                }
            }
            UpdateCorpseStateFromSource(state, npc, null);
            UpdateCorpseStateFromSource(state, character, null);
            UpdateCorpseStateFromSource(state, corpse, null);
            object parentModel = GetOptionalPropertyValue(corpse, "ParentModel")
                ?? GetOptionalPropertyValue(corpse, "GenericParentModel");
            UpdateCorpseStateFromSource(state, parentModel, null);
            RegisterCorpseAliases(corpse, state);
            RegisterCorpseAliases(npc, state);
            RegisterCorpseAliases(character, state);
            RegisterCorpseAliases(parentModel, state);

            if (DiagnosticsEnabled())
            {
                float effectiveKillXp = ResolveCorpseEffectiveKillXp(state);
                Log.LogInfo("Registered corpse #" + state.DebugId.ToString(CultureInfo.InvariantCulture) + " target " + DescribeCorpse(state) + " with base kill XP " + state.TargetKillXp.ToString("0.###", CultureInfo.InvariantCulture) + " and vanilla effective XP " + effectiveKillXp.ToString("0.###", CultureInfo.InvariantCulture) + ".");
            }
        }

        internal void HandleCorpseRestored(object corpse)
        {
            if (!_enabled.Value || corpse == null)
            {
                return;
            }

            CorpseState state;
            if (!TryGetCorpseState(corpse, out state))
            {
                state = CreateCorpseState();
            }

            state.Corpse = corpse;
            object parentModel = GetOptionalPropertyValue(corpse, "ParentModel")
                ?? GetOptionalPropertyValue(corpse, "GenericParentModel");
            if (parentModel != null)
            {
                if (state.TargetObject == null || ReferenceEquals(state.TargetObject, corpse))
                {
                    state.TargetObject = parentModel;
                }

                UpdateCorpseStateFromSource(state, parentModel, null);
                RegisterCorpseAliases(parentModel, state);
            }

            UpdateCorpseStateFromSource(state, corpse, null);
            state.RestoredFromSave = true;
            state.Disabled = true;
            float restoredSeverity;
            if (TryRestoreCorpseExsanguinationSeverity(state, corpse, out restoredSeverity))
            {
                state.Exhausted = true;
                state.ExsanguinationSeverity = restoredSeverity;
                state.LastRejectReason = "drained corpse was restored from save data";
            }
            else
            {
                state.LastRejectReason = "corpse was restored from save data";
            }
            RegisterCorpseAliases(corpse, state);
        }

        private bool CanReuseStateForCorpse(CorpseState state, object corpse)
        {
            if (state == null)
            {
                return false;
            }

            if (state.Corpse == null || corpse == null)
            {
                return true;
            }

            return ReferenceEquals(state.Corpse, corpse);
        }

        private object FindFirstArgumentByTypeName(object[] args, string typeName)
        {
            if (args == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            for (int i = 0; i < args.Length; i++)
            {
                object arg = args[i];
                if (arg != null && TypeMatchesFullName(arg.GetType(), typeName))
                {
                    return arg;
                }
            }

            return null;
        }

        private bool TypeMatchesFullName(Type type, string typeName)
        {
            Type current = type;
            while (current != null)
            {
                if (current.FullName == typeName)
                {
                    return true;
                }

                current = current.BaseType;
            }

            Type[] interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].FullName == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private void RememberCorpseSource(object target, object healthElement)
        {
            object key = healthElement ?? target;
            if (key == null)
            {
                return;
            }

            CorpseState state;
            if (!TryGetCorpseState(key, out state))
            {
                state = CreateCorpseState();
            }

            UpdateCorpseStateFromSource(state, target, healthElement);
            RegisterCorpseAliases(key, state);
            RegisterCorpseAliases(target, state);
            RegisterCorpseAliases(healthElement, state);
        }

        private void UpdateCorpseLeech()
        {
            if (_enabled == null || !_enabled.Value)
            {
                ResetFocusedCorpse();
                return;
            }

            int activeHandCount = GetActiveHeldHandCount();
            if (!MeetsHandRequirement(activeHandCount, _handRequirement.Value))
            {
                ResetFocusedCorpse();
                return;
            }

            float now = Now;
            if (now < _nextCorpseCheckTime)
            {
                return;
            }

            float interval = Math.Max(0.02f, _checkIntervalSeconds.Value);
            _nextCorpseCheckTime = now + interval;

            CorpseState state;
            if (TryGetLookedAtCorpseState(out state))
            {
                UpdateFocusedCorpse(state, now, activeHandCount);
                return;
            }

            if (_focusedCorpse != null && now - _focusedCorpse.LastFocusTime > Math.Max(0f, _focusGraceSeconds.Value))
            {
                ResetFocusedCorpse();
            }
        }

        private void UpdateFocusedCorpse(CorpseState state, float now, int activeHandCount)
        {
            if (state == null || state.Exhausted || state.Disabled)
            {
                return;
            }

            if (IsLiveServantRitualBlocked(state))
            {
                if (ReferenceEquals(_focusedCorpse, state))
                {
                    ResetFocusedCorpse();
                }
                return;
            }

            TouchCorpseState(state);

            string rejectReason;
            if (!IsBloodPlausible(state, out rejectReason))
            {
                RejectCorpse(state, rejectReason, true);
                return;
            }

            if (state.LiveServantTarget != null)
            {
                SetSoulAndServiceServantRitualState(
                    state.LiveServantTarget,
                    channeling: true,
                    completed: false);
            }

            if (!ReferenceEquals(_focusedCorpse, state))
            {
                ResetFocusedCorpse();
                _focusedCorpse = state;
                state.ChannelStartTime = now;
                state.LastFocusTime = now;
                return;
            }

            state.LastFocusTime = now;
            if (state.ChannelStartTime <= 0f)
            {
                state.ChannelStartTime = now;
                return;
            }

            float required = Math.Max(0.1f, GetSecondsRequired());
            if (now - state.ChannelStartTime >= required)
            {
                PayCorpseLeech(state, activeHandCount);
            }
        }

        private void ResetFocusedCorpse()
        {
            if (_focusedCorpse != null
                && _focusedCorpse.LiveServantTarget != null)
            {
                SetSoulAndServiceServantRitualState(
                    _focusedCorpse.LiveServantTarget,
                    channeling: false,
                    completed: false);
            }
            if (_focusedCorpse != null && !_focusedCorpse.Exhausted)
            {
                _focusedCorpse.ChannelStartTime = 0f;
            }

            _focusedCorpse = null;
        }

        internal bool IsBloodTransfusionEquippedForInterop()
        {
            if (_enabled == null || !_enabled.Value)
            {
                return false;
            }

            float now = Now;
            if (now < _nextBloodTransfusionEquippedCheckTime)
            {
                return _lastBloodTransfusionEquipped;
            }

            _nextBloodTransfusionEquippedCheckTime = now + 0.25f;
            _lastBloodTransfusionEquipped = IsBloodTransfusionEquippedUncached();
            return _lastBloodTransfusionEquipped;
        }

        internal float GetFocusedCorpseChannelProgress01ForInterop()
        {
            if (_focusedCorpse == null || !IsCorpseStateUsable(_focusedCorpse) || _focusedCorpse.ChannelStartTime <= 0f)
            {
                return 0f;
            }

            float required = Math.Max(0.1f, GetSecondsRequired());
            return Mathf.Clamp01((Now - _focusedCorpse.ChannelStartTime) / required);
        }

        internal float GetCorpseExsanguinationSeverityForInterop(object corpse)
        {
            CorpseState state;
            return corpse != null
                && TryResolveCorpseStateFromObject(
                    corpse,
                    0,
                    out state,
                    includeInactive: true)
                && state != null
                && state.Exhausted
                    ? Mathf.Clamp(state.ExsanguinationSeverity, 0.20f, 0.30f)
                    : 0.0f;
        }

        internal int GetFocusedCorpseStateForInterop(bool requireRelevantSpell)
        {
            if (_enabled == null || !_enabled.Value)
            {
                return (int)BloodMagicFocusedCorpseState.None;
            }

            bool bloodTransfusionEquipped = IsBloodTransfusionEquippedForInterop();
            bool abhartachEquipped = IsAbhartachEquippedForInterop();
            bool abhartachHeld = IsAbhartachHeldActiveForInterop();
            if (requireRelevantSpell && !bloodTransfusionEquipped && !abhartachEquipped && !abhartachHeld)
            {
                return (int)BloodMagicFocusedCorpseState.None;
            }

            CorpseState state;
            bool unregisteredCorpseCandidate;
            if (!TryGetFocusedCorpseInteropSnapshot(
                out state,
                out unregisteredCorpseCandidate))
            {
                return unregisteredCorpseCandidate
                    ? (int)BloodMagicFocusedCorpseState.Blocked
                    : (int)BloodMagicFocusedCorpseState.None;
            }

            if (state == null)
            {
                return (int)BloodMagicFocusedCorpseState.None;
            }

            if (state.Exhausted)
            {
                return (int)BloodMagicFocusedCorpseState.Spent;
            }

            if (state.Disabled)
            {
                return (int)BloodMagicFocusedCorpseState.Blocked;
            }

            string rejectReason;
            if (!IsBloodPlausible(state, out rejectReason))
            {
                return (int)BloodMagicFocusedCorpseState.Blocked;
            }

            if (state.LiveServantTarget != null && abhartachHeld)
            {
                return (int)BloodMagicFocusedCorpseState.Blocked;
            }

            if (bloodTransfusionEquipped && IsCorpseDrainableForInterop(state) && GetFocusedCorpseChannelProgress01ForInterop() > 0f)
            {
                return (int)BloodMagicFocusedCorpseState.Channeling;
            }

            if (abhartachHeld && IsCorpseBloodMagicEligibleForInterop(state))
            {
                RecordAbhartachCorpseQuality(state);
                return (int)BloodMagicFocusedCorpseState.Usable;
            }

            if (abhartachEquipped && IsCorpseBloodMagicEligibleForInterop(state))
            {
                if (state.LiveServantTarget != null
                    && _soulAndServiceMaterializeAbhartachCorpseMethod == null)
                {
                    return (int)BloodMagicFocusedCorpseState.Blocked;
                }
                RecordAbhartachCorpseQuality(state);
                return (int)BloodMagicFocusedCorpseState.Usable;
            }

            if (bloodTransfusionEquipped
                && !IsLiveServantRitualBlocked(state)
                && IsCorpseDrainableForInterop(state))
            {
                return (int)BloodMagicFocusedCorpseState.Usable;
            }

            if (!requireRelevantSpell && IsCorpseBloodMagicEligibleForInterop(state))
            {
                return (int)BloodMagicFocusedCorpseState.Usable;
            }

            return (int)BloodMagicFocusedCorpseState.None;
        }

        internal float GetFocusedCorpseQuality01ForInterop()
        {
            if (_enabled == null || !_enabled.Value)
            {
                return 0f;
            }

            CorpseState state;
            bool ignoredUnregisteredCorpseCandidate;
            return TryGetFocusedCorpseInteropSnapshot(
                    out state,
                    out ignoredUnregisteredCorpseCandidate)
                && IsCorpseBloodMagicEligibleForInterop(state)
                ? GetCorpseQuality01(state)
                : 0f;
        }

        internal int GetFocusedCorpseQualityTierForInterop()
        {
            if (_enabled == null || !_enabled.Value)
            {
                return (int)Grailwright.Shared.CorpseQualityTier.None;
            }

            CorpseState state;
            bool ignoredUnregisteredCorpseCandidate;
            if (!TryGetFocusedCorpseInteropSnapshot(
                    out state,
                    out ignoredUnregisteredCorpseCandidate)
                || state == null)
            {
                return (int)Grailwright.Shared.CorpseQualityTier.None;
            }

            return GetCorpseQualityTier(CalculateCorpseQuality01(state, false));
        }

        internal float GetFocusedCorpseQualityEffectMultiplierForInterop()
        {
            CorpseState state;
            bool ignoredUnregisteredCorpseCandidate;
            if (_enabled == null || !_enabled.Value
                || !TryGetFocusedCorpseInteropSnapshot(
                    out state,
                    out ignoredUnregisteredCorpseCandidate)
                || !IsCorpseBloodMagicEligibleForInterop(state))
            {
                return 1f;
            }

            return GetCorpseQualityEffectMultiplier(GetCorpseQuality01(state));
        }

        private bool TryGetFocusedCorpseInteropSnapshot(
            out CorpseState state,
            out bool unregisteredCorpseCandidate)
        {
            int frame = Time.frameCount;
            if (_focusedCorpseInteropSnapshotFrame != frame)
            {
                _focusedCorpseInteropSnapshotFrame = frame;
                _focusedCorpseInteropSnapshotResolved = TryGetLookedAtCorpseState(
                    out _focusedCorpseInteropSnapshotState,
                    out _focusedCorpseInteropSnapshotUnregisteredCandidate,
                    true);
            }

            state = _focusedCorpseInteropSnapshotState;
            unregisteredCorpseCandidate =
                _focusedCorpseInteropSnapshotUnregisteredCandidate;
            return _focusedCorpseInteropSnapshotResolved;
        }

        internal float GetBloodEssenceForInterop()
        {
            float essence = GetBloodEssence();
            return essence < 0f ? 0f : essence;
        }

        internal float GetBloodPowerForInterop()
        {
            return GetBloodPower();
        }

        internal bool IsAbhartachEquippedForInterop()
        {
            if (_enabled == null || !_enabled.Value || !ShouldTuneAbhartach())
            {
                return false;
            }

            float now = Now;
            if (now < _nextAbhartachEquippedCheckTime)
            {
                return _lastAbhartachEquipped;
            }

            _nextAbhartachEquippedCheckTime = now + 0.25f;
            _lastAbhartachEquipped = IsAbhartachEquippedUncached();
            return _lastAbhartachEquipped;
        }

        private bool IsCorpseDrainableForInterop(CorpseState state)
        {
            if (!IsCorpseStateUsable(state)
                || IsLiveServantRitualBlocked(state))
            {
                return false;
            }

            string rejectReason;
            if (!IsBloodPlausible(state, out rejectReason))
            {
                return false;
            }

            bool progressionEligible = state.LiveServantTarget == null
                || state.LiveServantHasSourceCorpse;
            bool xpEnabled = progressionEligible
                && _awardCharacterXp.Value
                && _rawCharacterXpPerCorpseXp.Value > 0f
                && !state.XpAwarded;
            bool healingEnabled = _healCharacter.Value && _healMaxHealthPercentPerXpPercent.Value > 0f && !state.Healed;
            if (!xpEnabled && !healingEnabled)
            {
                return false;
            }

            return !xpEnabled || ResolveCorpseEffectiveKillXp(state) > 0f;
        }

        private static bool IsLiveServantRitualBlocked(CorpseState state)
        {
            return state != null
                && state.LiveServantTarget != null
                && (Hero.Current == null || Hero.Current.IsInCombat());
        }

        private bool IsCorpseBloodMagicEligibleForInterop(CorpseState state)
        {
            if (!IsCorpseStateUsable(state))
            {
                return false;
            }

            string rejectReason;
            return IsBloodPlausible(state, out rejectReason);
        }

        private void PayCorpseLeech(CorpseState state, int activeHandCount)
        {
            if (state == null || state.Exhausted || state.Disabled)
            {
                return;
            }

            string rejectReason;
            if (!IsBloodPlausible(state, out rejectReason))
            {
                RejectCorpse(state, rejectReason, true);
                ResetFocusedCorpse();
                return;
            }

            if (state.LiveServantTarget != null)
            {
                CorpseState refreshedServantState;
                if (!TryResolveSoulAndServiceServant(
                        state.LiveServantTarget,
                        out refreshedServantState)
                    || !ReferenceEquals(refreshedServantState, state))
                {
                    RejectCorpse(state, "owned servant became unavailable", false);
                    ResetFocusedCorpse();
                    return;
                }
            }

            float payoutMultiplier = GetHandPayoutMultiplier(activeHandCount);
            float xpPercent = GetPayoutPercentOfKillXp() * payoutMultiplier;
            bool progressionEligible = state.LiveServantTarget == null
                || state.LiveServantHasSourceCorpse;
            bool xpEnabled = progressionEligible
                && _awardCharacterXp.Value
                && _rawCharacterXpPerCorpseXp.Value > 0f;
            bool healingEnabled = _healCharacter.Value && _healMaxHealthPercentPerXpPercent.Value > 0f;
            if (!xpEnabled && !healingEnabled)
            {
                RejectCorpse(state, "XP and healing rewards are both disabled", false);
                ResetFocusedCorpse();
                return;
            }

            string failures = "";
            float corpseQuality = GetCorpseQuality01(state);
            float healBasePercent = xpPercent * Math.Max(0f, _healMaxHealthPercentPerXpPercent.Value);
            float healPowerScale = GetHealingPowerScale(state);
            float healQualityScale = GetTransfusionHealingQualityMultiplier(state);
            float healPercent = healBasePercent * healPowerScale * healQualityScale;
            if (healingEnabled && !state.Healed)
            {
                if (ApplyCorpseLeechHealing(state, healPercent))
                {
                    state.Healed = true;
                    if (DiagnosticsEnabled())
                    {
                        Log.LogInfo("Healed " + healPercent.ToString("0.###", CultureInfo.InvariantCulture) + "% max HP from corpse #" + state.DebugId.ToString(CultureInfo.InvariantCulture) + " " + DescribeCorpse(state) + " (base " + healBasePercent.ToString("0.###", CultureInfo.InvariantCulture) + "%, power scale " + healPowerScale.ToString("0.###", CultureInfo.InvariantCulture) + "x, quality scale " + healQualityScale.ToString("0.###", CultureInfo.InvariantCulture) + "x).");
                    }
                }
                else
                {
                    failures = AppendFailure(failures, "healing failed");
                }
            }

            if (healingEnabled && !state.Healed)
            {
                RejectCorpse(
                    state,
                    string.IsNullOrEmpty(failures) ? "healing did not complete" : failures,
                    false);
                ResetFocusedCorpse();
                return;
            }

            float pendingRawXp = 0.0f;
            float resolvedBaseXp = 0.0f;
            bool xpAwardPending = xpEnabled && !state.XpAwarded;
            if (xpAwardPending)
            {
                float baseXp = ResolveCorpseEffectiveKillXp(state);
                if (baseXp <= 0f)
                {
                    failures = AppendFailure(failures, "vanilla kill XP could not be resolved");
                }
                else
                {
                    resolvedBaseXp = baseXp;
                    float amount = RoundXp(baseXp * (xpPercent / 100f));
                    float absoluteMax = _maximumXp.Value;
                    if (absoluteMax > 0f && amount > absoluteMax)
                    {
                        amount = absoluteMax;
                    }

                    if (amount < Math.Max(0f, _minimumXpToPay.Value))
                    {
                        failures = AppendFailure(failures, "computed XP payout was below MinimumXPToPay");
                    }
                    else
                    {
                        pendingRawXp = amount * Math.Max(0f, _rawCharacterXpPerCorpseXp.Value);
                    }
                }
            }

            if (!string.IsNullOrEmpty(failures))
            {
                RejectCorpse(state, failures, false);
                ResetFocusedCorpse();
                return;
            }

            BloodEssenceAwardReceipt essenceReceipt = null;
            if (progressionEligible
                && !TryAwardBloodEssence(corpseQuality, out essenceReceipt))
            {
                RejectCorpse(state, "Blood Essence could not be saved", false);
                ResetFocusedCorpse();
                return;
            }

            if (xpAwardPending)
            {
                bool xpClaimed = TryClaimGrailFloatingTextCorpseXp(
                    pendingRawXp,
                    state,
                    essenceReceipt == null ? 0.0f : essenceReceipt.Award);
                if (!AwardRawCharacterXp(pendingRawXp))
                {
                    if (xpClaimed)
                    {
                        TryCancelGrailFloatingTextXpClaim(
                            GrailFloatingTextCorpseXpEventId,
                            pendingRawXp);
                    }
                    RollbackBloodEssenceAward(essenceReceipt);
                    RejectCorpse(state, "character XP award failed", false);
                    ResetFocusedCorpse();
                    return;
                }

                state.XpAwarded = true;
                if (DiagnosticsEnabled())
                {
                    Log.LogInfo("Paid " + pendingRawXp.ToString("0.###", CultureInfo.InvariantCulture) + " corpse leech XP from corpse #" + state.DebugId.ToString(CultureInfo.InvariantCulture) + " " + DescribeCorpse(state) + " (" + xpPercent.ToString("0.###", CultureInfo.InvariantCulture) + "% of " + resolvedBaseXp.ToString("0.###", CultureInfo.InvariantCulture) + ").");
                }
            }

            ShowBloodPowerMilestonesAfterProgression(essenceReceipt);

            state.Exhausted = true;
            state.ExsanguinationSeverity = RollExsanguinationSeverity();
            PersistCorpseExsanguinationSeverity(state);
            if (state.LiveServantTarget != null)
            {
                bool killed;
                if (!TryExsanguinateSoulAndServiceServant(
                        state.LiveServantTarget,
                        state.ExsanguinationSeverity,
                        out killed))
                {
                    Warn(
                        "Blood ritual completed, but the servant's Health could not be adjusted.");
                }
                else if (DiagnosticsEnabled())
                {
                    Log.LogInfo(
                        "Exsanguinated servant by "
                        + (state.ExsanguinationSeverity * 100.0f).ToString(
                            "0.#",
                            CultureInfo.InvariantCulture)
                        + "%" + (killed ? " and executed it." : "."));
                }
                SetSoulAndServiceServantRitualState(
                    state.LiveServantTarget,
                    channeling: false,
                    completed: true);
            }
            state.ChannelStartTime = 0f;
            state.LastFocusTime = Now;
            TouchCorpseState(state);
            _focusedCorpse = null;

            if (state.LiveServantTarget == null)
            {
                SpawnCorpseRitualVfx(
                    corpseQuality,
                    state.HasPosition,
                    state.LastKnownPosition);
            }
            PlayCorpseLeechSound(
                corpseQuality,
                state.HasPosition,
                state.LastKnownPosition);
            if (progressionEligible)
            {
                ReportCorpseDrained(corpseQuality);
            }
            state.LoggedReject = false;
        }

        private void SpawnCorpseRitualVfx(
            float corpseQuality,
            bool hasCorpsePosition,
            Vector3 corpsePosition)
        {
            if (!hasCorpsePosition)
            {
                return;
            }

            string vfxKey = Mathf.Clamp01(corpseQuality) <= CorpseLeechWorthyQualityMax
                ? CorpseRitualLesserVfxKey
                : CorpseRitualGreaterVfxKey;
            PrefabPool.InstantiateAndReturn(
                new ShareableARAssetReference(vfxKey),
                corpsePosition,
                Quaternion.identity).Forget();
        }

        private float RollExsanguinationSeverity()
        {
            float center = 0.30f
                - (0.10f * Mathf.Clamp01(GetBloodPower() / 200.0f));
            return Mathf.Clamp(
                center + UnityEngine.Random.Range(-0.02f, 0.02f),
                0.20f,
                0.30f);
        }

        private void ReportCorpseDrained(float quality)
        {
            ReportCorpseDrainThreatToEyes(quality);
            ResolveDeedsOfAvalonBridge();

            if (_deedsOfAvalonSetCorpseDrainStatisticsMethod != null
                && _deedsOfAvalonGetCorpseDrainStatisticsMethod != null)
            {
                ReportCorpseStatisticsToDeeds();
                ReportBloodMagicProgressionToDeeds(GetBloodEssence());
                return;
            }

            if (_deedsOfAvalonRecordCorpseDrainMethod == null)
            {
                return;
            }

            try
            {
                _deedsOfAvalonRecordCorpseDrainMethod.Invoke(
                    null,
                    new object[] { PluginGuid, GetCorpseQualityLabel(quality), quality });
                ReportBloodMagicProgressionToDeeds(GetBloodEssence());
            }
            catch (Exception ex)
            {
                if (!_deedsOfAvalonFailureLogged)
                {
                    _deedsOfAvalonFailureLogged = true;
                    Log.LogWarning("Deeds of Avalon corpse-drain reporting failed: " + ex.GetBaseException().Message);
                }
            }
        }

        private void ReportCorpseDrainThreatToEyes(float quality)
        {
            ResolveEyesInTheDarkBridge();
            if (_eyesInTheDarkRegisterCorpseDrainMethod == null)
            {
                return;
            }

            try
            {
                _eyesInTheDarkRegisterCorpseDrainMethod.Invoke(
                    null,
                    new object[] { Mathf.Clamp01(quality) });
            }
            catch (Exception ex)
            {
                if (!_eyesInTheDarkFailureLogged)
                {
                    _eyesInTheDarkFailureLogged = true;
                    Log.LogWarning(
                        "Eyes in the Dark corpse-drain threat integration failed: "
                        + ex.GetBaseException().Message);
                }
            }
        }

        private void ResolveEyesInTheDarkBridge()
        {
            if (_eyesInTheDarkBridgeResolved)
            {
                return;
            }

            _eyesInTheDarkBridgeResolved = true;
            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    EyesInTheDarkPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                EyesInTheDarkCorpseDrainApiTypeName,
                false);
            _eyesInTheDarkRegisterCorpseDrainMethod = apiType == null
                ? null
                : apiType.GetMethod(
                    "TryRegisterCorpseDrain",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(float) },
                    null);
            if (_eyesInTheDarkRegisterCorpseDrainMethod == null
                || _eyesInTheDarkRegisterCorpseDrainMethod.ReturnType
                    != typeof(bool))
            {
                _eyesInTheDarkRegisterCorpseDrainMethod = null;
                if (!_eyesInTheDarkFailureLogged)
                {
                    _eyesInTheDarkFailureLogged = true;
                    Log.LogWarning(
                        "Eyes in the Dark is loaded, but its corpse-drain threat API could not be found.");
                }
            }
        }

        private void ResolveDeedsOfAvalonBridge()
        {
            if (_deedsOfAvalonBridgeResolved)
            {
                return;
            }

            _deedsOfAvalonBridgeResolved = true;
            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(DeedsOfAvalonPluginGuid, out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                _deedsOfAvalonBridgeResolved = false;
                return;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(DeedsOfAvalonApiTypeName, false);
            if (apiType == null)
            {
                return;
            }

            _deedsOfAvalonRecordCorpseDrainMethod = apiType.GetMethod(
                "TryRecordCorpseDrain",
                BindingFlags.Public | BindingFlags.Static);
            _deedsOfAvalonSetCorpseDrainStatisticsMethod = apiType.GetMethod(
                "TrySetCorpseDrainStatistics",
                BindingFlags.Public | BindingFlags.Static);
            _deedsOfAvalonRecordBloodMagicEssenceMethod = apiType.GetMethod(
                "TryRecordBloodMagicEssence",
                BindingFlags.Public | BindingFlags.Static);
            _deedsOfAvalonRecordBloodMagicProgressionMethod = apiType.GetMethod(
                "TryRecordBloodMagicProgression",
                BindingFlags.Public | BindingFlags.Static);
            _deedsOfAvalonGetCorpseDrainCountsMethod = apiType.GetMethod(
                "TryGetCorpseDrainCounts",
                BindingFlags.Public | BindingFlags.Static);
            _deedsOfAvalonGetCorpseDrainStatisticsMethod = apiType.GetMethod(
                "TryGetCorpseDrainStatistics",
                BindingFlags.Public | BindingFlags.Static);
        }

        private void UpdateBloodProgression()
        {
            if (_enabled == null || !_enabled.Value || Now < _nextBloodProgressionSyncTime)
            {
                return;
            }

            _nextBloodProgressionSyncTime = Now + BloodProgressionSyncIntervalSeconds;
            float essence = GetBloodEssence();
            if (essence >= 0f && Math.Abs(essence - _lastReportedBloodEssence) > 0.0001f)
            {
                ReportBloodMagicProgressionToDeeds(essence);
                _lastReportedBloodEssence = essence;
            }

            ReportCorpseStatisticsToDeeds();
        }

        private bool TryAwardBloodEssence(
            float quality,
            out BloodEssenceAwardReceipt receipt)
        {
            receipt = null;
            ContextualFacts facts = null;
            float beforeEssence = 0.0f;
            int beforeCorpseCount = 0;
            string corpseTierKey = null;
            int beforeTierCount = 0;
            float beforeQualitySum = 0.0f;
            bool previousValuesRead = false;
            try
            {
                facts = GetBloodProgressionFacts();
                if (facts == null)
                {
                    if (!_bloodProgressionUnavailableLogged)
                    {
                        _bloodProgressionUnavailableLogged = true;
                        Log.LogWarning("Blood Essence could not be saved because GameplayMemory was unavailable.");
                    }
                    return false;
                }

                EnsureBloodProgressionInitialized(facts);
                beforeEssence = Math.Max(0f, facts.Get(BloodProgressionEssenceKey, 0.0f));
                beforeCorpseCount = Math.Max(0, facts.Get(BloodProgressionCorpseCountKey, 0));
                corpseTierKey = GetBloodProgressionCorpseTierKey(quality);
                beforeTierCount = Math.Max(0, facts.Get(corpseTierKey, 0));
                beforeQualitySum = Math.Max(
                    0.0f,
                    facts.Get(BloodProgressionCorpseQualitySumKey, 0.0f));
                previousValuesRead = true;
                float quality01 = Mathf.Clamp01(quality);
                float gainedEssence = GetBloodEssenceGainForQuality(quality01);
                float afterEssence = SaturatingAdd(beforeEssence, gainedEssence);
                facts.Set(BloodProgressionEssenceKey, afterEssence);
                facts.Set(
                    BloodProgressionCorpseCountKey,
                    SaturatingIncrement(beforeCorpseCount));
                facts.Set(corpseTierKey, SaturatingIncrement(beforeTierCount));
                facts.Set(
                    BloodProgressionCorpseQualitySumKey,
                    SaturatingAdd(beforeQualitySum, quality01));
                facts.Set(BloodProgressionCorpseStatisticsInitializedKey, 1);
                _lastReportedBloodEssence = -1.0f;
                _lastReportedCorpseStatistics = null;
                receipt = new BloodEssenceAwardReceipt(
                    facts,
                    beforeEssence,
                    beforeCorpseCount,
                    corpseTierKey,
                    beforeTierCount,
                    beforeQualitySum,
                    gainedEssence);

                if (DiagnosticsEnabled())
                {
                    Log.LogInfo(
                        "Gained "
                        + gainedEssence.ToString("0.###", CultureInfo.InvariantCulture)
                        + " Blood Essence from a "
                        + GetCorpseQualityLabel(quality01)
                        + " corpse; total="
                        + afterEssence.ToString("0.###", CultureInfo.InvariantCulture)
                        + ", Blood Power="
                        + GetBloodPowerFromEssence(afterEssence).ToString("0.##", CultureInfo.InvariantCulture)
                        + ".");
                }

                return true;
            }
            catch (Exception exception)
            {
                if (previousValuesRead)
                {
                    TryRestoreBloodEssenceAward(
                        facts,
                        beforeEssence,
                        beforeCorpseCount,
                        corpseTierKey,
                        beforeTierCount,
                        beforeQualitySum);
                }
                Log.LogWarning(
                    "Blood Essence could not be saved: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private void RollbackBloodEssenceAward(BloodEssenceAwardReceipt receipt)
        {
            if (receipt == null || receipt.Facts == null)
            {
                return;
            }

            if (!TryRestoreBloodEssenceAward(
                receipt.Facts,
                receipt.BeforeEssence,
                receipt.BeforeCorpseCount,
                receipt.CorpseTierKey,
                receipt.BeforeTierCount,
                receipt.BeforeQualitySum))
            {
                Log.LogError(
                    "Blood Essence rollback failed after character XP could not be awarded.");
            }
        }

        private void ShowBloodPowerMilestonesAfterProgression(
            BloodEssenceAwardReceipt receipt)
        {
            if (receipt == null ||
                (_overrideBloodEssence != null && _overrideBloodEssence.Value))
            {
                return;
            }

            float beforePower = GetBloodPowerFromEssence(receipt.BeforeEssence);
            float afterPower = GetBloodPowerFromEssence(
                SaturatingAdd(receipt.BeforeEssence, receipt.Award));
            foreach (BloodPowerMilestone milestone in BloodPowerMilestones)
            {
                if (beforePower < milestone.Power && afterPower >= milestone.Power)
                {
                    TryShowBloodPowerMilestone(milestone);
                }
            }
        }

        private bool TryRestoreBloodEssenceAward(
            ContextualFacts facts,
            float essence,
            int corpseCount,
            string corpseTierKey,
            int tierCount,
            float qualitySum)
        {
            if (facts == null)
            {
                return false;
            }

            try
            {
                facts.Set(BloodProgressionEssenceKey, essence);
                facts.Set(BloodProgressionCorpseCountKey, corpseCount);
                if (!string.IsNullOrEmpty(corpseTierKey))
                {
                    facts.Set(corpseTierKey, tierCount);
                }
                facts.Set(BloodProgressionCorpseQualitySumKey, qualitySum);
            }
            catch (Exception exception)
            {
                Log.LogError(
                    "Blood Essence state could not be restored: "
                    + exception.GetBaseException().Message);
                return false;
            }

            _lastReportedBloodEssence = -1.0f;
            _lastReportedCorpseStatistics = null;
            return true;
        }

        private ContextualFacts GetBloodProgressionFacts()
        {
            Services services = World.Services;
            GameplayMemory memory = services == null
                ? null
                : services.TryGet<GameplayMemory>();
            return memory == null ? null : memory.Context(BloodProgressionMemoryContext);
        }

        private void PersistCorpseExsanguinationSeverity(CorpseState state)
        {
            string key;
            if (state == null
                || !TryGetCorpseExsanguinationKey(state, state.Corpse, out key))
            {
                return;
            }

            ContextualFacts facts = GetBloodProgressionFacts();
            if (facts == null)
            {
                return;
            }

            try
            {
                facts.Set(
                    key,
                    Mathf.Clamp(state.ExsanguinationSeverity, 0.20f, 0.30f));
            }
            catch (Exception exception)
            {
                if (DiagnosticsEnabled())
                {
                    Log.LogWarning(
                        "Could not persist drained-corpse severity: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private bool TryRestoreCorpseExsanguinationSeverity(
            CorpseState state,
            object corpse,
            out float severity)
        {
            severity = 0.0f;
            string key;
            if (!TryGetCorpseExsanguinationKey(state, corpse, out key))
            {
                return false;
            }

            ContextualFacts facts = GetBloodProgressionFacts();
            if (facts == null)
            {
                return false;
            }

            try
            {
                float storedSeverity = facts.Get(key, 0.0f);
                if (storedSeverity <= 0.0f)
                {
                    return false;
                }

                severity = Mathf.Clamp(
                    storedSeverity,
                    0.20f,
                    0.30f);
                return true;
            }
            catch (Exception exception)
            {
                if (DiagnosticsEnabled())
                {
                    Log.LogWarning(
                        "Could not restore drained-corpse severity: "
                        + exception.GetBaseException().Message);
                }
                return false;
            }
        }

        private bool TryGetCorpseExsanguinationKey(
            CorpseState state,
            object corpse,
            out string key)
        {
            key = null;
            string modelId = GetStableModelId(corpse);
            if (string.IsNullOrEmpty(modelId) && state != null)
            {
                modelId = GetStableModelId(state.Corpse);
            }
            if (string.IsNullOrEmpty(modelId))
            {
                object parentModel = GetOptionalPropertyValue(corpse, "ParentModel")
                    ?? GetOptionalPropertyValue(corpse, "GenericParentModel");
                modelId = GetStableModelId(parentModel);
            }
            if (string.IsNullOrEmpty(modelId) && state != null)
            {
                modelId = GetStableModelId(state.TargetObject);
            }
            if (string.IsNullOrEmpty(modelId))
            {
                return false;
            }

            key = BloodRitualSeverityKeyPrefix
                + StableHash(modelId).ToString("x8", CultureInfo.InvariantCulture);
            return true;
        }

        private string GetStableModelId(object candidate)
        {
            Model model = candidate as Model;
            if (model != null && !string.IsNullOrEmpty(model.ID))
            {
                return model.ID;
            }

            return SafeToString(
                GetOptionalPropertyValue(candidate, "ID")
                ?? GetOptionalPropertyValue(candidate, "Id")).Trim();
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private float GetBloodEssence()
        {
            float storedEssence = GetStoredBloodEssence();
            if (storedEssence < 0f)
            {
                return storedEssence;
            }

            return _overrideBloodEssence != null && _overrideBloodEssence.Value
                ? Math.Max(0f, _bloodEssenceOverrideValue == null ? 0f : _bloodEssenceOverrideValue.Value)
                : storedEssence;
        }

        private float GetStoredBloodEssence()
        {
            ContextualFacts facts = GetBloodProgressionFacts();
            if (facts == null)
            {
                return -1.0f;
            }

            EnsureBloodProgressionInitialized(facts);
            return Math.Max(0f, facts.Get(BloodProgressionEssenceKey, 0.0f));
        }

        private void EnsureBloodProgressionInitialized(ContextualFacts facts)
        {
            if (facts == null)
            {
                return;
            }

            bool progressionInitialized = facts.Get(BloodProgressionInitializedKey, 0) != 0;
            bool statisticsInitialized =
                facts.Get(BloodProgressionCorpseStatisticsInitializedKey, 0) != 0;
            if (progressionInitialized && statisticsInitialized)
            {
                return;
            }

            if (progressionInitialized
                && !statisticsInitialized
                && Now < _nextCorpseStatisticsImportAttemptTime)
            {
                return;
            }

            int importedMeager;
            int importedWorthy;
            int importedPotent;
            int importedPrime;
            float importedEssence;
            float importedQualitySum;
            bool imported = TryImportDeedsCorpseDrainCounts(
                out importedEssence,
                out importedMeager,
                out importedWorthy,
                out importedPotent,
                out importedPrime,
                out importedQualitySum);
            if (!imported)
            {
                _nextCorpseStatisticsImportAttemptTime = Now + 10.0f;
            }

            if (!progressionInitialized)
            {
                facts.Set(BloodProgressionEssenceKey, imported ? importedEssence : 0.0f);
                facts.Set(BloodProgressionInitializedKey, 1);
                if (importedEssence > 0f)
                {
                    Log.LogInfo(
                        "Initialized Blood Power from existing Deeds of Avalon corpse tiers: essence="
                        + importedEssence.ToString("0.###", CultureInfo.InvariantCulture)
                        + ", power="
                        + GetBloodPowerFromEssence(importedEssence).ToString("0.##", CultureInfo.InvariantCulture)
                        + ".");
                }
            }

            if (!statisticsInitialized && imported)
            {
                facts.Set(BloodProgressionMeagerCorpseCountKey, importedMeager);
                facts.Set(BloodProgressionWorthyCorpseCountKey, importedWorthy);
                facts.Set(BloodProgressionPotentCorpseCountKey, importedPotent);
                facts.Set(BloodProgressionPrimeCorpseCountKey, importedPrime);
                facts.Set(BloodProgressionCorpseQualitySumKey, importedQualitySum);
                facts.Set(
                    BloodProgressionCorpseCountKey,
                    SaturatingAdd(
                        SaturatingAdd(importedMeager, importedWorthy),
                        SaturatingAdd(importedPotent, importedPrime)));
                facts.Set(BloodProgressionCorpseStatisticsInitializedKey, 1);
            }
            else if (!statisticsInitialized
                && facts.Get(BloodProgressionCorpseCountKey, 0) <= 0)
            {
                facts.Set(BloodProgressionCorpseStatisticsInitializedKey, 1);
            }
        }

        private bool TryImportDeedsCorpseDrainCounts(
            out float essence,
            out int meager,
            out int worthy,
            out int potent,
            out int prime,
            out float qualitySum)
        {
            essence = 0.0f;
            meager = 0;
            worthy = 0;
            potent = 0;
            prime = 0;
            qualitySum = 0.0f;
            ResolveDeedsOfAvalonBridge();
            if (_deedsOfAvalonGetCorpseDrainStatisticsMethod == null
                && _deedsOfAvalonGetCorpseDrainCountsMethod == null)
            {
                return false;
            }

            try
            {
                bool includeQuality = _deedsOfAvalonGetCorpseDrainStatisticsMethod != null;
                object[] args = includeQuality
                    ? new object[] { PluginGuid, 0, 0, 0, 0, 0.0f }
                    : new object[] { PluginGuid, 0, 0, 0, 0 };
                MethodInfo method = includeQuality
                    ? _deedsOfAvalonGetCorpseDrainStatisticsMethod
                    : _deedsOfAvalonGetCorpseDrainCountsMethod;
                object result = method.Invoke(null, args);
                if (!(result is bool) || !(bool)result)
                {
                    return false;
                }

                meager = Math.Max(0, Convert.ToInt32(args[1], CultureInfo.InvariantCulture));
                worthy = Math.Max(0, Convert.ToInt32(args[2], CultureInfo.InvariantCulture));
                potent = Math.Max(0, Convert.ToInt32(args[3], CultureInfo.InvariantCulture));
                prime = Math.Max(0, Convert.ToInt32(args[4], CultureInfo.InvariantCulture));
                qualitySum = includeQuality
                    ? Math.Max(0.0f, Convert.ToSingle(args[5], CultureInfo.InvariantCulture))
                    : 0.0f;
                essence = (meager * MeagerBloodEssenceAward)
                    + (worthy * WorthyBloodEssenceAward)
                    + (potent * PotentBloodEssenceAward)
                    + (prime * PrimeBloodEssenceAward);
                return true;
            }
            catch (Exception ex)
            {
                if (!_deedsOfAvalonFailureLogged)
                {
                    _deedsOfAvalonFailureLogged = true;
                    Log.LogWarning("Deeds of Avalon Blood Power import failed: " + ex.GetBaseException().Message);
                }
                return false;
            }
        }

        private void ReportCorpseStatisticsToDeeds()
        {
            ResolveDeedsOfAvalonBridge();
            if (_deedsOfAvalonSetCorpseDrainStatisticsMethod == null
                || _deedsOfAvalonGetCorpseDrainStatisticsMethod == null)
            {
                return;
            }

            ContextualFacts facts = GetBloodProgressionFacts();
            if (facts == null)
            {
                return;
            }

            EnsureBloodProgressionInitialized(facts);
            int meager = Math.Max(0, facts.Get(BloodProgressionMeagerCorpseCountKey, 0));
            int worthy = Math.Max(0, facts.Get(BloodProgressionWorthyCorpseCountKey, 0));
            int potent = Math.Max(0, facts.Get(BloodProgressionPotentCorpseCountKey, 0));
            int prime = Math.Max(0, facts.Get(BloodProgressionPrimeCorpseCountKey, 0));
            float qualitySum = Math.Max(
                0.0f,
                facts.Get(BloodProgressionCorpseQualitySumKey, 0.0f));
            string fingerprint = string.Join(
                ":",
                meager.ToString(CultureInfo.InvariantCulture),
                worthy.ToString(CultureInfo.InvariantCulture),
                potent.ToString(CultureInfo.InvariantCulture),
                prime.ToString(CultureInfo.InvariantCulture),
                qualitySum.ToString("R", CultureInfo.InvariantCulture));
            if (string.Equals(
                fingerprint,
                _lastReportedCorpseStatistics,
                StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                object result = _deedsOfAvalonSetCorpseDrainStatisticsMethod.Invoke(
                    null,
                    new object[] { PluginGuid, meager, worthy, potent, prime, qualitySum });
                if (result is bool && (bool)result)
                {
                    _lastReportedCorpseStatistics = fingerprint;
                    _deedsOfAvalonFailureLogged = false;
                }
            }
            catch (Exception ex)
            {
                if (!_deedsOfAvalonFailureLogged)
                {
                    _deedsOfAvalonFailureLogged = true;
                    Log.LogWarning(
                        "Deeds of Avalon corpse-statistic synchronization failed: "
                        + ex.GetBaseException().Message);
                }
            }
        }

        private static string GetBloodProgressionCorpseTierKey(float quality)
        {
            float quality01 = Mathf.Clamp01(quality);
            if (quality01 <= CorpseLeechMeagerQualityMax)
            {
                return BloodProgressionMeagerCorpseCountKey;
            }

            if (quality01 <= CorpseLeechWorthyQualityMax)
            {
                return BloodProgressionWorthyCorpseCountKey;
            }

            return quality01 <= CorpseLeechPotentQualityMax
                ? BloodProgressionPotentCorpseCountKey
                : BloodProgressionPrimeCorpseCountKey;
        }

        private static int SaturatingIncrement(int value)
        {
            return value >= int.MaxValue ? int.MaxValue : value + 1;
        }

        private static int SaturatingAdd(int left, int right)
        {
            long sum = (long)Math.Max(0, left) + Math.Max(0, right);
            return sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }

        private static float SaturatingAdd(float left, float right)
        {
            double sum = Math.Max(0.0f, left) + Math.Max(0.0f, right);
            return double.IsNaN(sum) || sum <= 0.0
                ? 0.0f
                : sum >= float.MaxValue ? float.MaxValue : (float)sum;
        }

        private float GetBloodEssenceGainForQuality(float quality)
        {
            float quality01 = Mathf.Clamp01(quality);
            int nominal;
            int bonusCap;
            if (quality01 <= CorpseLeechMeagerQualityMax)
            {
                nominal = Mathf.RoundToInt(MeagerBloodEssenceAward);
                bonusCap = 1;
            }
            else if (quality01 <= CorpseLeechWorthyQualityMax)
            {
                nominal = Mathf.RoundToInt(WorthyBloodEssenceAward);
                bonusCap = 1;
            }
            else if (quality01 <= CorpseLeechPotentQualityMax)
            {
                nominal = Mathf.RoundToInt(PotentBloodEssenceAward);
                bonusCap = 2;
            }
            else
            {
                nominal = Mathf.RoundToInt(PrimeBloodEssenceAward);
                bonusCap = 3;
            }

            float bonusChance = 0.05f
                + (0.0005f * Mathf.Clamp(GetBloodPower(), 0.0f, 200.0f));
            int bonus = 0;
            for (int point = 0; point < nominal && bonus < bonusCap; point++)
            {
                if (UnityEngine.Random.value < bonusChance)
                {
                    bonus++;
                }
            }
            return nominal + bonus;
        }

        private float GetBloodPower()
        {
            float essence = GetBloodEssence();
            return essence < 0f ? 0f : GetBloodPowerFromEssence(essence);
        }

        private float GetBloodPowerFromEssence(float essence)
        {
            float safeEssence = Math.Max(0f, essence);
            if (safeEssence <= BloodEssenceAtNormalMaximumPower)
            {
                float masteryProgress = Mathf.Clamp01(
                    safeEssence / BloodEssenceAtNormalMaximumPower);
                return (10.0f * masteryProgress * masteryProgress * masteryProgress)
                    - (70.0f * masteryProgress * masteryProgress)
                    + (160.0f * masteryProgress);
            }

            float overmasteryProgress = Mathf.Clamp01(
                (safeEssence - BloodEssenceAtNormalMaximumPower)
                / (BloodEssenceAtAbsoluteMaximumPower
                    - BloodEssenceAtNormalMaximumPower));
            return NormalMaximumBloodPower
                + ((AbsoluteMaximumBloodPower - NormalMaximumBloodPower)
                    * overmasteryProgress);
        }

        private float GetBloodPowerOvermasteryBonusFraction(float power)
        {
            return MaximumOvermasteryBonusFraction
                * GetBloodPowerOvermasteryProgress01(power);
        }

        private static float GetBloodPowerOvermasteryProgress01(float power)
        {
            return Mathf.Clamp01(
                (power - NormalMaximumBloodPower)
                / (AbsoluteMaximumBloodPower - NormalMaximumBloodPower));
        }

        private void ReportBloodMagicProgressionToDeeds(float essence)
        {
            ResolveDeedsOfAvalonBridge();
            if ((_deedsOfAvalonRecordBloodMagicProgressionMethod == null
                    && _deedsOfAvalonRecordBloodMagicEssenceMethod == null)
                || essence < 0f)
            {
                return;
            }

            try
            {
                if (_deedsOfAvalonRecordBloodMagicProgressionMethod != null)
                {
                    _deedsOfAvalonRecordBloodMagicProgressionMethod.Invoke(
                        null,
                        new object[] { PluginGuid, essence, GetBloodPowerFromEssence(essence) });
                }
                else
                {
                    _deedsOfAvalonRecordBloodMagicEssenceMethod.Invoke(
                        null,
                        new object[] { PluginGuid, essence });
                }
            }
            catch (Exception ex)
            {
                if (!_deedsOfAvalonFailureLogged)
                {
                    _deedsOfAvalonFailureLogged = true;
                    Log.LogWarning("Deeds of Avalon Blood Magic progression reporting failed: " + ex.GetBaseException().Message);
                }
            }
        }

        private void PlayCorpseLeechSound(
            float corpseQuality,
            bool hasCorpsePosition,
            Vector3 corpsePosition)
        {
            if (_playCorpseLeechSound == null || !_playCorpseLeechSound.Value)
            {
                return;
            }

            EnsureCorpseLeechSoundPathsResolved();
            if (CountCorpseLeechSoundPaths() == 0)
            {
                if (!_loggedMissingCorpseLeechSounds)
                {
                    Log.LogWarning("Corpse leech sound is enabled, but no tiered corpse leech WAV files were found.");
                    _loggedMissingCorpseLeechSounds = true;
                }

                return;
            }

            string tier = GetCorpseLeechSoundTier(corpseQuality);
            string selectedTier;
            string path = PickCorpseLeechSoundPath(tier, out selectedTier);
            if (path == "")
            {
                return;
            }

            float baseVolume = Math.Max(0f, _corpseLeechSoundVolume == null ? 1f : _corpseLeechSoundVolume.Value);
            float rangeMultiplier = GetCorpseLeechRangeVolumeMultiplier(
                hasCorpsePosition,
                corpsePosition);
            float volume = baseVolume * rangeMultiplier;
            float pitch = GetCorpseLeechSoundPitchMultiplier();
            if (TryPlayFmodCorpseLeechSound(path, volume, pitch))
            {
                RememberRecentCorpseLeechSound(selectedTier, path);
            }
        }

        private float GetCorpseLeechRangeVolumeMultiplier(
            bool hasCorpsePosition,
            Vector3 corpsePosition)
        {
            float strength = _corpseLeechSoundRangeVolume == null
                ? 1.0f
                : Mathf.Clamp01(_corpseLeechSoundRangeVolume.Value);
            if (strength <= 0.001f)
            {
                return 1.0f;
            }
            Vector3 heroPosition;
            if (!hasCorpsePosition
                || !TryGetPosition(GetHero(), out heroPosition))
            {
                LogAudioDiagnostic(
                    "Corpse leech audio range could not resolve both positions; using full volume.");
                return 1.0f;
            }
            float distance = Vector3.Distance(heroPosition, corpsePosition);
            float progress = Mathf.Clamp01(
                distance / CorpseLeechMaximumRangeDistance);
            float fullCurveVolume = 1.0f
                - ((1.0f - CorpseLeechMinimumRangeVolume) * progress);
            float multiplier = Mathf.Lerp(1.0f, fullCurveVolume, strength);
            LogAudioDiagnostic(
                "Corpse leech audio distance="
                + distance.ToString("0.##", CultureInfo.InvariantCulture)
                + "m; rangeMultiplier="
                + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
            return multiplier;
        }

        private void EnsureCorpseLeechSoundPathsResolved()
        {
            if (_corpseLeechSoundPathsResolved)
            {
                return;
            }

            _corpseLeechSoundPathsResolved = true;
            _corpseLeechSoundPathsByTier.Clear();

            AddCorpseLeechTierSoundFiles(CorpseLeechLowTier);
            AddCorpseLeechTierSoundFiles(CorpseLeechMediumTier);
            AddCorpseLeechTierSoundFiles(CorpseLeechHighTier);
            AddCorpseLeechTierSoundFiles(CorpseLeechMaxTier);

            int count = CountCorpseLeechSoundPaths();
            if (count > 0)
            {
                LogAudioDiagnostic("Resolved " + count.ToString(CultureInfo.InvariantCulture) + " tiered corpse leech sound file(s).");
            }
        }

        private void AddCorpseLeechTierSoundFiles(string tier)
        {
            for (int i = 1; i <= CorpseLeechTierSoundSlots; i++)
            {
                AddCorpseLeechSoundFile(
                    tier,
                    "corpse_leech_" + tier + "_" + i.ToString(CultureInfo.InvariantCulture) + ".wav");
            }
        }

        private void AddCorpseLeechSoundFile(string tier, string configured)
        {
            string resolved = ResolveCorpseLeechSoundPath(configured);
            if (resolved == "")
            {
                return;
            }

            List<string> paths;
            if (!_corpseLeechSoundPathsByTier.TryGetValue(tier, out paths))
            {
                paths = new List<string>();
                _corpseLeechSoundPathsByTier[tier] = paths;
            }

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], resolved, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            paths.Add(resolved);
        }

        private string ResolveCorpseLeechSoundPath(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                return "";
            }

            string trimmed = configured.Trim();
            if (Path.IsPathRooted(trimmed) && File.Exists(trimmed))
            {
                return trimmed;
            }

            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(pluginDirectory))
            {
                return "";
            }

            string primary = Path.Combine(pluginDirectory, trimmed);
            if (File.Exists(primary))
            {
                return primary;
            }

            if (string.IsNullOrEmpty(Path.GetDirectoryName(trimmed)))
            {
                string audioFolderCandidate = Path.Combine(Path.Combine(pluginDirectory, "audio"), trimmed);
                if (File.Exists(audioFolderCandidate))
                {
                    return audioFolderCandidate;
                }
            }

            return "";
        }

        private string GetCorpseLeechSoundTier(float quality)
        {
            quality = Mathf.Clamp01(quality);
            if (quality <= CorpseLeechMeagerQualityMax)
            {
                return CorpseLeechLowTier;
            }

            if (quality <= CorpseLeechWorthyQualityMax)
            {
                return CorpseLeechMediumTier;
            }

            return quality <= CorpseLeechPotentQualityMax
                ? CorpseLeechHighTier
                : CorpseLeechMaxTier;
        }

        private string PickCorpseLeechSoundPath(string preferredTier, out string selectedTier)
        {
            selectedTier = "";
            string[] tiers = GetCorpseLeechTierFallbacks(preferredTier);
            for (int i = 0; i < tiers.Length; i++)
            {
                List<string> paths;
                if (_corpseLeechSoundPathsByTier.TryGetValue(tiers[i], out paths) && paths.Count > 0)
                {
                    selectedTier = tiers[i];
                    return PickCorpseLeechSoundPathFromTier(tiers[i], paths);
                }
            }

            return "";
        }

        private string PickCorpseLeechSoundPathFromTier(string tier, List<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return "";
            }

            if (_avoidRecentCorpseLeechRepeats != null && _avoidRecentCorpseLeechRepeats.Value)
            {
                int memory = GetRecentCorpseLeechSoundMemory();
                List<string> recent;
                if (memory > 0 && _recentCorpseLeechSoundPathsByTier.TryGetValue(tier, out recent) && recent.Count > 0)
                {
                    List<string> candidates = new List<string>();
                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (!ContainsString(recent, paths[i]))
                        {
                            candidates.Add(paths[i]);
                        }
                    }

                    if (candidates.Count > 0)
                    {
                        return candidates[_random.Next(candidates.Count)];
                    }
                }
            }

            return paths[_random.Next(paths.Count)];
        }

        private void RememberRecentCorpseLeechSound(string tier, string path)
        {
            if (_avoidRecentCorpseLeechRepeats == null || !_avoidRecentCorpseLeechRepeats.Value)
            {
                return;
            }

            int memory = GetRecentCorpseLeechSoundMemory();
            if (memory <= 0 || string.IsNullOrWhiteSpace(tier) || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            List<string> recent;
            if (!_recentCorpseLeechSoundPathsByTier.TryGetValue(tier, out recent))
            {
                recent = new List<string>();
                _recentCorpseLeechSoundPathsByTier[tier] = recent;
            }

            for (int i = recent.Count - 1; i >= 0; i--)
            {
                if (string.Equals(recent[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    recent.RemoveAt(i);
                }
            }

            recent.Add(path);
            while (recent.Count > memory)
            {
                recent.RemoveAt(0);
            }
        }

        private int GetRecentCorpseLeechSoundMemory()
        {
            return _recentCorpseLeechSoundMemory == null
                ? 2
                : Math.Max(0, Math.Min(20, _recentCorpseLeechSoundMemory.Value));
        }

        private bool ContainsString(List<string> values, string candidate)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string[] GetCorpseLeechTierFallbacks(string preferredTier)
        {
            if (string.Equals(preferredTier, CorpseLeechMaxTier, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { CorpseLeechMaxTier, CorpseLeechHighTier, CorpseLeechMediumTier, CorpseLeechLowTier };
            }

            if (string.Equals(preferredTier, CorpseLeechHighTier, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { CorpseLeechHighTier, CorpseLeechMediumTier, CorpseLeechLowTier, CorpseLeechMaxTier };
            }

            if (string.Equals(preferredTier, CorpseLeechMediumTier, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { CorpseLeechMediumTier, CorpseLeechLowTier, CorpseLeechHighTier, CorpseLeechMaxTier };
            }

            return new[] { CorpseLeechLowTier, CorpseLeechMediumTier, CorpseLeechHighTier, CorpseLeechMaxTier };
        }

        private int CountCorpseLeechSoundPaths()
        {
            int count = 0;
            foreach (KeyValuePair<string, List<string>> pair in _corpseLeechSoundPathsByTier)
            {
                count += pair.Value.Count;
            }

            return count;
        }

        private float GetCorpseLeechSoundPitchMultiplier()
        {
            float semitoneRange = _corpseLeechRandomPitchSemitones == null
                ? 0f
                : Math.Max(0f, _corpseLeechRandomPitchSemitones.Value);
            if (semitoneRange <= 0f)
            {
                return 1f;
            }

            float semitones = (float)((_random.NextDouble() * 2.0 - 1.0) * semitoneRange);
            return Mathf.Pow(2f, semitones / 12f);
        }

        private bool TryPlayFmodCorpseLeechSound(string path, float volume, float pitch)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                FMOD.Sound sound;
                if (!_corpseLeechFmodSoundsByPath.TryGetValue(path, out sound))
                {
                    FMOD.RESULT createResult = RuntimeManager.CoreSystem.createSound(
                        path,
                        FMOD.MODE.DEFAULT | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE,
                        out sound);
                    if (createResult != FMOD.RESULT.OK)
                    {
                        Log.LogWarning("FMOD createSound failed for corpse leech sound " + path + ": " + createResult + ".");
                        return false;
                    }

                    _corpseLeechFmodSoundsByPath[path] = sound;
                }

                FMOD.ChannelGroup channelGroup;
                if (!TryGetCorpseLeechSfxChannelGroup(out channelGroup))
                {
                    return false;
                }

                FMOD.Channel channel;
                FMOD.RESULT playResult = RuntimeManager.CoreSystem.playSound(sound, channelGroup, true, out channel);
                if (playResult != FMOD.RESULT.OK)
                {
                    Log.LogWarning("FMOD playSound failed for corpse leech sound " + path + ": " + playResult + ".");
                    return false;
                }

                FMOD.RESULT volumeResult = channel.setVolume(volume);
                if (volumeResult != FMOD.RESULT.OK)
                {
                    LogAudioDiagnostic("FMOD corpse leech channel volume set failed for " + path + ": " + volumeResult + ".");
                }

                FMOD.RESULT pitchResult = channel.setPitch(Math.Max(0.01f, pitch));
                if (pitchResult != FMOD.RESULT.OK)
                {
                    LogAudioDiagnostic("FMOD corpse leech channel pitch set failed for " + path + ": " + pitchResult + ".");
                }

                FMOD.RESULT pauseResult = channel.setPaused(false);
                if (pauseResult != FMOD.RESULT.OK)
                {
                    LogAudioDiagnostic("FMOD corpse leech channel unpause failed for " + path + ": " + pauseResult + ".");
                }

                LogAudioDiagnostic("Played corpse leech sound " + Path.GetFileName(path) + " at pitch " + pitch.ToString("0.###", CultureInfo.InvariantCulture) + "x.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning("FMOD corpse leech sound playback failed for " + path + ": " + ex.GetBaseException().Message);
                return false;
            }
        }

        private bool TryGetCorpseLeechSfxChannelGroup(
            out FMOD.ChannelGroup channelGroup)
        {
            if (_corpseLeechSfxBusLocked
                && _corpseLeechSfxChannelGroup.hasHandle())
            {
                channelGroup = _corpseLeechSfxChannelGroup;
                return true;
            }

            ReleaseCorpseLeechSfxBus();

            FMOD.Studio.Bus sfxBus;
            if (!BusGroup.SFX.TryGetBus(out sfxBus))
            {
                Log.LogWarning(
                    "FMOD could not resolve the game's SFX bus for corpse leech playback.");
                channelGroup = default(FMOD.ChannelGroup);
                return false;
            }

            FMOD.RESULT lockResult = sfxBus.lockChannelGroup();
            if (lockResult != FMOD.RESULT.OK)
            {
                Log.LogWarning(
                    "FMOD could not lock the game's SFX bus channel group for corpse leech playback: "
                    + lockResult
                    + ".");
                channelGroup = default(FMOD.ChannelGroup);
                return false;
            }

            FMOD.RESULT groupResult = sfxBus.getChannelGroup(
                out channelGroup);
            if (groupResult != FMOD.RESULT.OK
                || !channelGroup.hasHandle())
            {
                sfxBus.unlockChannelGroup();
                Log.LogWarning(
                    "FMOD could not access the game's SFX bus channel group for corpse leech playback: "
                    + groupResult
                    + ".");
                channelGroup = default(FMOD.ChannelGroup);
                return false;
            }

            _corpseLeechSfxBus = sfxBus;
            _corpseLeechSfxChannelGroup = channelGroup;
            _corpseLeechSfxBusLocked = true;
            LogAudioDiagnostic(
                "Corpse leech playback connected to the game's SFX bus.");
            return true;
        }

        private void ReleaseCorpseLeechFmodSounds()
        {
            foreach (KeyValuePair<string, FMOD.Sound> pair in _corpseLeechFmodSoundsByPath)
            {
                try
                {
                    pair.Value.release();
                }
                catch
                {
                }
            }

            _corpseLeechFmodSoundsByPath.Clear();
            ReleaseCorpseLeechSfxBus();
        }

        private void ReleaseCorpseLeechSfxBus()
        {
            _corpseLeechSfxChannelGroup = default(FMOD.ChannelGroup);
            if (_corpseLeechSfxBusLocked
                && _corpseLeechSfxBus.isValid())
            {
                FMOD.RESULT unlockResult =
                    _corpseLeechSfxBus.unlockChannelGroup();
                LogAudioDiagnostic(
                    "Corpse leech SFX bus unlock result="
                    + unlockResult
                    + ".");
            }

            _corpseLeechSfxBus = default(FMOD.Studio.Bus);
            _corpseLeechSfxBusLocked = false;
        }

        private void LogAudioDiagnostic(string message)
        {
            if (DiagnosticsEnabled())
            {
                Log.LogInfo(message);
            }
        }

        private void LogBloodSpellTuningDiagnosticThrottled(string message)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            float now = Now;
            if (now < _nextBloodSpellTuningDiagnosticTime)
            {
                return;
            }

            _nextBloodSpellTuningDiagnosticTime =
                now + BloodSpellTuningDiagnosticIntervalSeconds;
            Log.LogInfo(message);
        }

        private bool ApplyCorpseLeechHealing(CorpseState state, float percentOfMaxHealth)
        {
            if (percentOfMaxHealth <= 0f)
            {
                return false;
            }

            bool presentationClaimed = BeginGrailFloatingTextBloodHealingPresentationClaim();
            try
            {
                return HealHeroPercentOfMaxHealth(percentOfMaxHealth);
            }
            finally
            {
                if (presentationClaimed)
                {
                    EndGrailFloatingTextBloodHealingPresentationClaim();
                }
            }
        }

        private void HandleAppliedDamage(object healthElement, object damage)
        {
            TryApplyLiveBloodMagicDrain(healthElement, damage);
        }

        internal void ApplyBloodMagicTapDamageTuning(object healthElement, Damage damage)
        {
            if (!ShouldTuneBloodSpells() || healthElement == null || damage == null)
            {
                return;
            }

            object projectile = damage.Projectile;
            string sourceSummary;
            if (projectile == null || !IsBloodMagicProjectileSource(projectile, out sourceSummary))
            {
                return;
            }

            object hero = GetHero();
            if (hero == null || !IsHeroDamageDealer(damage, hero))
            {
                return;
            }

            object heroHealthElement = GetPropertyValue(hero, "HealthElement");
            if (ReferenceEquals(healthElement, heroHealthElement))
            {
                return;
            }

            float multiplier = GetBloodSpellTapDamageMultiplier();
            if (multiplier <= 0f || Math.Abs(multiplier - 1f) <= 0.0001f)
            {
                return;
            }

            damage.RawData.MultiplyMultModifier(multiplier);
            LogBloodSpellTuningDiagnosticThrottled(
                "Blood/Life tap damage tuned: source="
                + sourceSummary
                + " power="
                + GetBloodPower().ToString("0.##", CultureInfo.InvariantCulture)
                + " multiplier="
                + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
        }

        private void TryApplyLiveBloodMagicDrain(object healthElement, object damage)
        {
            if (_enabled == null ||
                !_enabled.Value ||
                healthElement == null ||
                damage == null)
            {
                return;
            }

            bool tuneHeldHealing = ShouldTuneBloodSpells()
                && _liveDrainHealingMultiplier != null;
            bool awardLiveDrainXp = _liveDrainEnabled != null
                && _liveDrainEnabled.Value;
            if (!tuneHeldHealing && !awardLiveDrainXp)
            {
                return;
            }

            int activeHandCount = GetActiveHeldHandCount();
            if (activeHandCount <= 0)
            {
                return;
            }

            string sourceSummary;
            if (!IsBloodMagicDamageSource(damage, out sourceSummary))
            {
                return;
            }

            if (GetPropertyValue(damage, "Projectile") != null)
            {
                return;
            }

            float damageAmount = GetDamageAmount(damage);
            if (damageAmount <= 0.001f)
            {
                return;
            }

            object hero = GetHero();
            if (hero == null)
            {
                return;
            }

            if (!IsHeroDamageDealer(damage, hero))
            {
                return;
            }

            object heroHealthElement = GetPropertyValue(hero, "HealthElement");
            if (ReferenceEquals(healthElement, heroHealthElement))
            {
                return;
            }

            if (!LooksLikeLivingTarget(healthElement))
            {
                return;
            }

            object target = ResolveDamageTargetOwner(healthElement, damage);
            string rejectReason;
            if (!IsBloodPlausibleObject(healthElement, target, out rejectReason))
            {
                return;
            }

            float now = Now;
            if (tuneHeldHealing)
            {
                RegisterLiveDrainHealingEligibility(now);
            }

            if (!awardLiveDrainXp)
            {
                return;
            }

            LiveDrainState state = GetLiveDrainState(healthElement, target, now);
            state.LastSeenTime = now;
            if (state.BaseXp <= 0f)
            {
                state.BaseXp = ResolveLiveDrainBaseXp(healthElement, target, damage);
            }

            float handMultiplier = GetHandPayoutMultiplier(activeHandCount);
            state.LastDrainTime = now;

            TryAwardLiveDrainXp(state, now, handMultiplier);
        }

        private void RegisterLiveDrainHealingEligibility(float now)
        {
            if (now > _liveDrainHealingEligibleUntil)
            {
                _pendingLiveDrainHealingCount = 0;
            }

            _liveDrainHealingEligibleUntil = now + LiveDrainHealingEligibilitySeconds;
            _pendingLiveDrainHealingCount = Math.Min(16, _pendingLiveDrainHealingCount + 1);
        }

        private bool TryConsumeLiveDrainHealingEligibility(object healingItem)
        {
            if (_pendingLiveDrainHealingCount <= 0 || Now > _liveDrainHealingEligibleUntil)
            {
                _pendingLiveDrainHealingCount = 0;
                return false;
            }

            string ignored;
            if (healingItem != null && !IsBloodTransfusionItem(healingItem, out ignored))
            {
                return false;
            }

            _pendingLiveDrainHealingCount--;
            return true;
        }

        private LiveDrainState GetLiveDrainState(object healthElement, object target, float now)
        {
            LiveDrainState state;
            if (!_liveDrainStates.TryGetValue(healthElement, out state))
            {
                state = new LiveDrainState();
                state.Target = target;
                state.NextXpTickTime = now + Math.Max(0.1f, GetLiveDrainXpTickIntervalSeconds());
                _liveDrainStates[healthElement] = state;
            }
            else if (state.Target == null && target != null)
            {
                state.Target = target;
            }

            return state;
        }

        private void TryAwardLiveDrainXp(LiveDrainState state, float now, float handMultiplier)
        {
            if (state == null ||
                _liveDrainAwardCharacterXp == null ||
                !_liveDrainAwardCharacterXp.Value ||
                !_awardCharacterXp.Value ||
                _rawCharacterXpPerCorpseXp.Value <= 0f ||
                state.BaseXp <= 0f ||
                now < state.NextXpTickTime)
            {
                return;
            }

            float cap = state.BaseXp * (GetLiveDrainMaximumXpPercentPerTarget() / 100f) * handMultiplier;
            float remaining = Math.Max(0f, cap - state.LiveXpAwarded);
            if (remaining <= 0f)
            {
                return;
            }

            float computedXp = RoundXp(Math.Min(remaining, state.BaseXp * (GetLiveDrainXpPercentPerTick() / 100f) * handMultiplier));
            if (computedXp < Math.Max(0f, _minimumXpToPay.Value))
            {
                state.NextXpTickTime = now + Math.Max(0.1f, GetLiveDrainXpTickIntervalSeconds());
                return;
            }

            float rawXp = computedXp * Math.Max(0f, _rawCharacterXpPerCorpseXp.Value) * Math.Max(0f, _liveDrainRawCharacterXpMultiplier.Value);
            TryClaimGrailFloatingTextLiveDrainXp(rawXp);
            if (AwardRawCharacterXp(rawXp))
            {
                state.LiveXpAwarded += computedXp;
                if (DiagnosticsEnabled())
                {
                    Log.LogInfo("Paid " + rawXp.ToString("0.###", CultureInfo.InvariantCulture) + " live Blood Magic Expansion XP (" + computedXp.ToString("0.###", CultureInfo.InvariantCulture) + " computed).");
                }
            }

            state.NextXpTickTime = now + Math.Max(0.1f, GetLiveDrainXpTickIntervalSeconds());
        }

        private void CleanupLiveDrainStates()
        {
            if (_liveDrainStates.Count == 0)
            {
                return;
            }

            float now = Now;
            if (now < _nextLiveDrainCleanupTime)
            {
                return;
            }

            _nextLiveDrainCleanupTime = now + 5f;
            List<object> remove = null;
            foreach (KeyValuePair<object, LiveDrainState> pair in _liveDrainStates)
            {
                if (pair.Value == null || now - pair.Value.LastSeenTime > 30f)
                {
                    if (remove == null)
                    {
                        remove = new List<object>();
                    }

                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _liveDrainStates.Remove(remove[i]);
            }
        }

        private void CleanupCachedStates()
        {
            float now = Now;
            if (now < _nextCacheCleanupTime)
            {
                return;
            }

            _nextCacheCleanupTime = now + CacheCleanupIntervalSeconds;
            CleanupStrongCastStates(now);
            CleanupBloodSpellInnerLightReadyStates();
            CleanupCorpseStates(now);
            CleanupDestroyedObjectSet(_loggedUnresolvedRaycastHits);
        }

        private void CleanupBloodSpellInnerLightReadyStates()
        {
            if (_bloodSpellInnerLightReadyStates.Count == 0)
            {
                return;
            }

            List<object> remove = null;
            float now = Now;
            foreach (KeyValuePair<object, BloodSpellInnerLightReadyState> pair in _bloodSpellInnerLightReadyStates)
            {
                if (pair.Value == null ||
                    pair.Value.Until < now - 2.0f ||
                    IsDestroyedUnityObject(pair.Key))
                {
                    if (remove == null)
                    {
                        remove = new List<object>();
                    }

                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _bloodSpellInnerLightReadyStates.Remove(remove[i]);
            }

            ClearUnusedBloodSpellInnerLightCastBoostStates();
        }

        private void CleanupStrongCastStates(float now)
        {
            if (_strongCastStates.Count == 0)
            {
                return;
            }

            List<object> remove = null;
            foreach (KeyValuePair<object, StrongCastState> pair in _strongCastStates)
            {
                if (pair.Value == null ||
                    pair.Value.Until < now - ExpiredStrongCastRetentionSeconds ||
                    IsDestroyedUnityObject(pair.Key))
                {
                    if (remove == null)
                    {
                        remove = new List<object>();
                    }

                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _strongCastStates.Remove(remove[i]);
            }
        }

        private void CleanupCorpseStates(float now)
        {
            if (_allCorpseStates.Count == 0)
            {
                CleanupDestroyedCorpseAliases(_corpseStates);
                CleanupDestroyedCorpseAliases(_corpseRaycastCache);
                return;
            }

            HashSet<CorpseState> staleStates = null;
            for (int i = _allCorpseStates.Count - 1; i >= 0; i--)
            {
                CorpseState state = _allCorpseStates[i];
                if (!ShouldPruneCorpseState(state, now))
                {
                    continue;
                }

                if (staleStates == null)
                {
                    staleStates = new HashSet<CorpseState>();
                }

                staleStates.Add(state);
                if (ReferenceEquals(_focusedCorpse, state))
                {
                    _focusedCorpse = null;
                }

                _allCorpseStates.RemoveAt(i);
            }

            if (staleStates != null)
            {
                RemoveCorpseAliases(_corpseStates, staleStates);
                RemoveCorpseAliases(_corpseRaycastCache, staleStates);
            }

            CleanupDestroyedCorpseAliases(_corpseStates);
            CleanupDestroyedCorpseAliases(_corpseRaycastCache);
        }

        private bool ShouldPruneCorpseState(CorpseState state, float now)
        {
            if (state == null)
            {
                return true;
            }

            if (ReferenceEquals(_focusedCorpse, state))
            {
                return false;
            }

            if (state.RestoredFromSave)
            {
                return false;
            }

            float lastTouched = Math.Max(state.LastTouchedTime, state.LastFocusTime);
            bool oldEnough = lastTouched > 0f && now - lastTouched > CompletedCorpseRetentionSeconds;
            if (!oldEnough)
            {
                return false;
            }

            return state.Disabled ||
                IsDestroyedUnityObject(state.Corpse) ||
                IsDestroyedUnityObject(state.TargetObject);
        }

        private void RemoveCorpseAliases(
            Dictionary<object, CorpseState> aliases,
            HashSet<CorpseState> staleStates)
        {
            if (aliases.Count == 0 || staleStates == null || staleStates.Count == 0)
            {
                return;
            }

            List<object> remove = null;
            foreach (KeyValuePair<object, CorpseState> pair in aliases)
            {
                if (pair.Value == null || staleStates.Contains(pair.Value))
                {
                    if (remove == null)
                    {
                        remove = new List<object>();
                    }

                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0; i < remove.Count; i++)
            {
                aliases.Remove(remove[i]);
            }
        }

        private void CleanupDestroyedCorpseAliases(Dictionary<object, CorpseState> aliases)
        {
            if (aliases.Count == 0)
            {
                return;
            }

            List<object> remove = null;
            foreach (KeyValuePair<object, CorpseState> pair in aliases)
            {
                if (pair.Value == null || IsDestroyedUnityObject(pair.Key))
                {
                    if (remove == null)
                    {
                        remove = new List<object>();
                    }

                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0; i < remove.Count; i++)
            {
                aliases.Remove(remove[i]);
            }
        }

        private void CleanupDestroyedObjectSet(HashSet<object> values)
        {
            if (values.Count == 0)
            {
                return;
            }

            List<object> remove = null;
            foreach (object value in values)
            {
                if (IsDestroyedUnityObject(value))
                {
                    if (remove == null)
                    {
                        remove = new List<object>();
                    }

                    remove.Add(value);
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0; i < remove.Count; i++)
            {
                values.Remove(remove[i]);
            }
        }

        private static bool IsDestroyedUnityObject(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return !ReferenceEquals(unityObject, null) && unityObject == null;
        }

        private bool LooksLikeLivingTarget(object healthElement)
        {
            bool alive;
            HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (TryReadAliveState(healthElement, 0, seen, out alive))
            {
                return alive;
            }

            return true;
        }

        private object ResolveDamageTargetOwner(object healthElement, object damage)
        {
            object target = GetPropertyValue(damage, "Target");
            if (target == null)
            {
                target = GetPropertyValue(damage, "TargetPure");
            }
            if (target == null)
            {
                target = ResolveHealthElementOwner(healthElement);
            }

            return target;
        }

        private float ResolveLiveDrainBaseXp(object healthElement, object target, object damage)
        {
            bool hasLevelContext;
            float value = TryResolveVanillaEffectiveKillXp(target, out hasLevelContext);
            if (value > 0f)
            {
                return value;
            }

            object owner = ResolveHealthElementOwner(healthElement);
            value = TryResolveVanillaEffectiveKillXp(owner, out hasLevelContext);
            if (value > 0f)
            {
                return value;
            }

            value = TryResolveVanillaEffectiveKillXp(healthElement, out hasLevelContext);
            if (value > 0f)
            {
                return value;
            }

            return Math.Max(0f, _fallbackKillXp.Value);
        }

        private bool IsBloodMagicDamageSource(object damage, out string summary)
        {
            summary = "";
            if (damage == null)
            {
                return false;
            }

            object item = GetPropertyValue(damage, "Item");
            object skill = GetPropertyValue(damage, "Skill");
            if (IsBloodTransfusionItemOrSkill(item, skill, out summary))
            {
                return true;
            }

            object projectile = GetPropertyValue(damage, "Projectile");
            return IsBloodMagicProjectileSource(projectile, out summary);
        }

        internal bool IsBloodMagicDamageForInterop(object damage)
        {
            string summary;
            if (IsBloodMagicDamageSource(damage, out summary))
            {
                return true;
            }
            if (damage == null)
            {
                return false;
            }

            object item = GetPropertyValue(damage, "Item");
            object skill = GetPropertyValue(damage, "Skill");
            if (IsAbhartachItemOrSkill(item, skill, out summary))
            {
                return true;
            }

            object projectile = GetPropertyValue(damage, "Projectile");
            object sourceWeapon = GetPropertyValue(projectile, "SourceWeapon");
            object sourceProjectile = GetPropertyValue(projectile, "SourceProjectile");
            return IsAbhartachItemOrSkill(sourceWeapon, null, out summary)
                || IsAbhartachItemOrSkill(sourceProjectile, null, out summary);
        }

        internal bool IsBloodMagicDisplayNameForInterop(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            string text = displayName.Trim();
            return ContainsAnyConfiguredTerm(text, GetMatchTerms())
                || ContainsAnyConfiguredTerm(text, GetAbhartachMatchTerms());
        }

        private static bool ContainsAnyConfiguredTerm(string text, string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i])
                    && text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsBloodMagicProjectileSource(object projectile, out string summary)
        {
            summary = "";
            if (projectile == null)
            {
                return false;
            }

            object sourceWeapon = GetPropertyValue(projectile, "SourceWeapon");
            if (IsBloodTransfusionItem(sourceWeapon, out summary))
            {
                return true;
            }

            object sourceProjectile = GetPropertyValue(projectile, "SourceProjectile");
            return IsBloodTransfusionItem(sourceProjectile, out summary);
        }

        private bool IsHeroDamageDealer(object damage, object hero)
        {
            if (damage == null || hero == null)
            {
                return false;
            }

            object dealer = GetPropertyValue(damage, "DamageDealerPure");
            if (IsSameModelOrOwner(dealer, hero))
            {
                return true;
            }

            dealer = GetPropertyValue(damage, "DamageDealer");
            if (IsSameModelOrOwner(dealer, hero))
            {
                return true;
            }

            object projectile = GetPropertyValue(damage, "Projectile");
            object owner = GetPropertyValue(projectile, "Owner");
            return IsSameModelOrOwner(owner, hero);
        }

        private bool IsSameModelOrOwner(object candidate, object expected)
        {
            if (candidate == null || expected == null)
            {
                return false;
            }

            if (ReferenceEquals(candidate, expected))
            {
                return true;
            }

            string[] properties = { "ParentModel", "GenericParentModel", "Owner", "Character", "Hero" };
            for (int i = 0; i < properties.Length; i++)
            {
                object value = GetOptionalPropertyValue(candidate, properties[i]);
                if (ReferenceEquals(value, expected))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBloodPlausibleObject(object first, object second, out string reason)
        {
            reason = "";
            if (!_requireBloodPlausible.Value)
            {
                return true;
            }

            string text = (BuildObjectSearchText(first) + " " + BuildObjectSearchText(second)).Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            string matched;
            if (ContainsAnyTerm(text, GetWhitelistTerms(), out matched))
            {
                return true;
            }

            if (ContainsAnyTerm(text, GetBloodlessTerms(), out matched))
            {
                reason = "matched bloodless term '" + matched + "'";
                return false;
            }

            return true;
        }

        internal void AdjustBloodMagicMagicFsmDeltaTime(object magicFsm, ref float deltaTime)
        {
            if (!ShouldTuneBloodSpells() || magicFsm == null || deltaTime <= 0f)
            {
                return;
            }

            object item = GetPropertyValue(magicFsm, "Item");
            object skill = GetPropertyValue(magicFsm, "Skill");
            string ignored;
            if (!IsBloodTransfusionItemOrSkill(item, skill, out ignored))
            {
                return;
            }

            float multiplier = IsHeldBloodMagicChannel(magicFsm)
                ? GetBloodSpellHeldChannelSpeedMultiplier()
                : GetBloodSpellTapCastSpeedMultiplier();
            if (multiplier > 0f && Math.Abs(multiplier - 1f) > 0.0001f)
            {
                deltaTime *= multiplier;
            }
        }

        private bool IsHeldBloodMagicChannel(object magicFsm)
        {
            if (magicFsm == null)
            {
                return false;
            }

            float now = Now;
            StrongCastState state;
            if (_strongCastStates.TryGetValue(magicFsm, out state) && state.Until >= now)
            {
                return true;
            }

            if (!GetBoolProperty(magicFsm, "SpellAttackHeld", false))
            {
                return false;
            }

            if (!GetBoolProperty(magicFsm, "IsCasting", false))
            {
                return false;
            }

            if (GetBoolProperty(magicFsm, "IsChargingMagic", false))
            {
                return true;
            }

            return GetIntProperty(magicFsm, "CurrentChargeSteps", 0) > 0;
        }

        internal void ApplyBloodMagicBuildupTuning(
            ref float buildupStrength,
            object statusTemplate,
            object sourceInfo,
            bool isBleed)
        {
            if (buildupStrength <= 0f || statusTemplate == null || sourceInfo == null || !isBleed)
            {
                return;
            }

            object sourceItem = GetPropertyValue(sourceInfo, "GetSourceItemSafe");
            string ignored;
            bool isBloodMagicSpell = ShouldTuneBloodSpells() &&
                (IsBloodTransfusionItem(sourceItem, out ignored) ||
                    _currentBloodMagicProjectileImpactContext.QualifyingPlayerBloodSpell);
            bool isAbhartach = ShouldTuneAbhartach() &&
                _abhartachScaleExplosionBleed.Value &&
                IsAbhartachItem(sourceItem, out ignored);
            if (!isBloodMagicSpell && !isAbhartach)
            {
                return;
            }

            object sourceCharacter = GetPropertyValue(sourceInfo, "GetSourceCharacter");
            if (sourceCharacter != null && !IsSameModelOrOwner(sourceCharacter, GetHero()))
            {
                return;
            }

            float multiplier = isAbhartach
                ? GetAbhartachExplosionBleedBuildupMultiplier() * GetAbhartachCorpseQualityEffectMultiplier()
                : GetBloodSpellBleedBuildupMultiplier();
            if (multiplier > 0f && Math.Abs(multiplier - 1f) > 0.0001f)
            {
                buildupStrength *= multiplier;
                LogBloodSpellTuningDiagnosticThrottled(
                    "Blood magic Bleed buildup tuned: source="
                    + (isAbhartach ? "Abhartach" : "Blood/Life")
                    + " power="
                    + GetBloodPower().ToString("0.##", CultureInfo.InvariantCulture)
                    + " multiplier="
                    + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private BloodMagicAreaBuildupScopeState BeginBloodMagicAreaBuildupScope(
            object[] args,
            bool cone)
        {
            BloodMagicAreaBuildupScopeState state = new BloodMagicAreaBuildupScopeState
            {
                Previous = _currentBloodMagicAreaBuildupContext
            };
            _currentBloodMagicAreaBuildupContext = new BloodMagicAreaBuildupContext
            {
                Scoped = true,
                QualifyingPlayerBloodSpell = IsPlayerBloodMagicAreaBuildup(args, cone)
            };
            return state;
        }

        private void EndBloodMagicAreaBuildupScope(BloodMagicAreaBuildupScopeState state)
        {
            _currentBloodMagicAreaBuildupContext = state.Previous;
        }

        private bool IsPlayerBloodMagicAreaBuildup(object[] args, bool cone)
        {
            if (args == null || args.Length < 2 || args[0] == null ||
                !IsSameModelOrOwner(args[0], GetHero()))
            {
                return false;
            }

            object sphereDamageParameters = cone
                ? GetMemberValue(args[1], "sphereDamageParameters")
                : args[1];
            object item = GetMemberValue(sphereDamageParameters, "item");
            string ignored;
            return ShouldTuneAbhartach() && IsAbhartachItem(item, out ignored);
        }

        private BloodMagicBuildupApplicationScopeState BeginBloodMagicBuildupApplicationScope(
            object statusTemplate,
            object sourceInfo)
        {
            BloodMagicBuildupApplicationScopeState state = new BloodMagicBuildupApplicationScopeState
            {
                Previous = _currentBloodMagicBuildupApplicationContext
            };
            bool isBleed = IsBleedBuildupStatus(statusTemplate);
            bool qualifying = false;
            if (isBleed && sourceInfo != null)
            {
                object sourceCharacter = GetPropertyValue(sourceInfo, "GetSourceCharacter");
                if (sourceCharacter != null && IsSameModelOrOwner(sourceCharacter, GetHero()))
                {
                    object sourceItem = GetPropertyValue(sourceInfo, "GetSourceItemSafe");
                    string ignored;
                    qualifying = (ShouldTuneBloodSpells() &&
                            (IsBloodTransfusionItem(sourceItem, out ignored) ||
                                _currentBloodMagicProjectileImpactContext.QualifyingPlayerBloodSpell)) ||
                        (ShouldTuneAbhartach() &&
                            IsAbhartachItem(sourceItem, out ignored)) ||
                        _currentBloodMagicAreaBuildupContext.QualifyingPlayerBloodSpell;
                }
            }

            _currentBloodMagicBuildupApplicationContext = new BloodMagicBuildupApplicationContext
            {
                Scoped = true,
                IsBleed = isBleed,
                QualifyingPlayerBloodSpell = qualifying
            };
            state.IsBleed = isBleed;
            return state;
        }

        private void EndBloodMagicBuildupApplicationScope(
            BloodMagicBuildupApplicationScopeState state)
        {
            _currentBloodMagicBuildupApplicationContext = state.Previous;
        }

        private void RecordBloodMagicBleedProc(object buildupStatus, bool completed)
        {
            if (buildupStatus == null || !completed)
            {
                return;
            }

            BloodMagicBuildupApplicationContext context =
                _currentBloodMagicBuildupApplicationContext;
            if (!context.Scoped ||
                !context.IsBleed ||
                !context.QualifyingPlayerBloodSpell ||
                _bloodSpellScaleBleedDuration == null ||
                !_bloodSpellScaleBleedDuration.Value)
            {
                _bloodMagicBleedDurationStates.Remove(buildupStatus);
                return;
            }

            float maximumMultiplier = _bloodSpellMaximumBleedDurationMultiplier == null
                ? 2.0f
                : Mathf.Clamp(_bloodSpellMaximumBleedDurationMultiplier.Value, 1.0f, 10.0f);
            float powerProgress = Mathf.Clamp01(GetBloodPower() / AbsoluteMaximumBloodPower);
            float durationMultiplier = Mathf.Lerp(1.0f, maximumMultiplier, powerProgress);
            _bloodMagicBleedDurationStates.Remove(buildupStatus);
            _bloodMagicBleedDurationStates.Add(
                buildupStatus,
                new BloodMagicBleedDurationState
                {
                    Multiplier = durationMultiplier
                });
            LogBloodSpellTuningDiagnosticThrottled(
                "Blood magic Bleed duration tagged: power="
                + GetBloodPower().ToString("0.##", CultureInfo.InvariantCulture)
                + " multiplier="
                + durationMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
        }

        internal void ApplyBloodMagicBleedDurationTuning(object buildupStatus, ref float deltaTime)
        {
            BloodMagicBleedDurationState state;
            if (buildupStatus == null ||
                deltaTime <= 0f ||
                !_bloodMagicBleedDurationStates.TryGetValue(buildupStatus, out state) ||
                state.Multiplier <= 1.0001f)
            {
                return;
            }

            if (_bloodSpellScaleBleedDuration == null || !_bloodSpellScaleBleedDuration.Value)
            {
                _bloodMagicBleedDurationStates.Remove(buildupStatus);
                return;
            }

            deltaTime /= state.Multiplier;
        }

        internal void ApplyBloodMagicProjectileDistanceTuning(object projectile, object weapon, object sourceProjectile)
        {
            BloodMagicProjectileState existing;
            if (!ShouldTuneBloodSpells() || projectile == null || _bloodMagicProjectiles.TryGetValue(projectile, out existing))
            {
                return;
            }

            string ignored;
            if (!IsBloodTransfusionItem(weapon, out ignored) && !IsBloodTransfusionItem(sourceProjectile, out ignored))
            {
                return;
            }

            object owner = GetPropertyValue(projectile, "Owner");
            if (owner != null && !IsSameModelOrOwner(owner, GetHero()))
            {
                return;
            }

            _bloodMagicProjectiles.Add(projectile, new BloodMagicProjectileState());
            if (_bloodSpellScaleProjectileTravel.Value)
            {
                float multiplier = GetBloodSpellProjectileTravelMultiplier();
                float lifeTime;
                if (multiplier > 0f &&
                    Math.Abs(multiplier - 1f) > 0.0001f &&
                    TryGetFloatMember(projectile, "LifeTime", out lifeTime) &&
                    lifeTime > 0f)
                {
                    TrySetFloatMember(projectile, "LifeTime", lifeTime * multiplier);
                }
            }

            if (_bloodSpellScaleHomingTargetSearch.Value)
            {
                float multiplier = GetBloodSpellHomingTargetSearchMultiplier();
                float targetFindDistance;
                if (multiplier > 0f &&
                    Math.Abs(multiplier - 1f) > 0.0001f &&
                    TryGetFloatMember(projectile, "targetFindDistance", out targetFindDistance) &&
                    targetFindDistance > 0f)
                {
                    TrySetFloatMember(projectile, "targetFindDistance", targetFindDistance * multiplier);
                }
            }
        }

        private BloodMagicProjectileImpactScopeState BeginBloodMagicProjectileImpactScope(
            object projectile)
        {
            BloodMagicProjectileImpactScopeState state =
                new BloodMagicProjectileImpactScopeState
                {
                    Previous = _currentBloodMagicProjectileImpactContext
                };
            BloodMagicProjectileState projectileState;
            _currentBloodMagicProjectileImpactContext =
                new BloodMagicProjectileImpactContext
                {
                    Scoped = true,
                    QualifyingPlayerBloodSpell = projectile != null &&
                        _bloodMagicProjectiles.TryGetValue(projectile, out projectileState)
                };
            return state;
        }

        private void EndBloodMagicProjectileImpactScope(
            BloodMagicProjectileImpactScopeState state)
        {
            _currentBloodMagicProjectileImpactContext = state.Previous;
        }

        internal void ApplyBloodMagicSphereAreaTuning(object[] args)
        {
            if (args == null || args.Length < 2)
            {
                return;
            }

            bool hasExplicitRadius = HasExplicitAreaRadiusArgument(args);
            object tunedParameters;
            if (TryTuneSphereDamageParameters(args[0], args[1], !hasExplicitRadius, out tunedParameters))
            {
                args[1] = tunedParameters;
            }

            float radiusMultiplier;
            if (hasExplicitRadius && TryGetAreaRadiusMultiplier(args[0], args[1], out radiusMultiplier))
            {
                TryScaleExplicitAreaRadiusArgument(args, radiusMultiplier);
            }
        }

        internal void ApplyBloodMagicConeAreaTuning(object[] args)
        {
            if (args == null || args.Length < 2)
            {
                return;
            }

            object coneDamageParameters = args[1];
            object sphereDamageParameters = GetMemberValue(coneDamageParameters, "sphereDamageParameters");
            bool hasExplicitRadius = HasExplicitAreaRadiusArgument(args);
            object tunedSphereDamageParameters;
            if (TryTuneSphereDamageParameters(args[0], sphereDamageParameters, !hasExplicitRadius, out tunedSphereDamageParameters) &&
                TrySetMemberValue(coneDamageParameters, "sphereDamageParameters", tunedSphereDamageParameters))
            {
                args[1] = coneDamageParameters;
            }

            float radiusMultiplier;
            if (hasExplicitRadius && TryGetAreaRadiusMultiplier(args[0], sphereDamageParameters, out radiusMultiplier))
            {
                TryScaleExplicitAreaRadiusArgument(args, radiusMultiplier);
            }
        }

        private bool TryTuneSphereDamageParameters(object attacker, object sphereDamageParameters, bool tuneEndRadius, out object tunedParameters)
        {
            tunedParameters = sphereDamageParameters;
            if (sphereDamageParameters == null)
            {
                return false;
            }

            if (attacker != null && !IsSameModelOrOwner(attacker, GetHero()))
            {
                return false;
            }

            object item = GetMemberValue(sphereDamageParameters, "item");
            string ignored;
            bool isAbhartach = ShouldTuneAbhartach() && IsAbhartachItem(item, out ignored);
            if (!isAbhartach)
            {
                return false;
            }

            bool changed = false;
            if (isAbhartach &&
                _abhartachScaleExplosionDamage.Value &&
                TryApplyAbhartachRawDamageDataTuning(sphereDamageParameters))
            {
                changed = true;
            }

            float radius;
            float radiusMultiplier;
            if (tuneEndRadius &&
                TryGetAreaRadiusMultiplier(attacker, sphereDamageParameters, out radiusMultiplier) &&
                TryGetFloatMember(sphereDamageParameters, "endRadius", out radius) &&
                radius > 0f &&
                TrySetFloatMember(sphereDamageParameters, "endRadius", radius * radiusMultiplier))
            {
                changed = true;
            }

            int buildup;
            if (isAbhartach &&
                _abhartachScaleExplosionBleed.Value &&
                TryGetIntMember(sphereDamageParameters, "onHitStatusBuildup", out buildup) &&
                buildup > 0)
            {
                float buildupMultiplier = GetAbhartachExplosionBleedBuildupMultiplier() * GetAbhartachCorpseQualityEffectMultiplier();
                if (buildupMultiplier > 0f && Math.Abs(buildupMultiplier - 1f) > 0.0001f)
                {
                    int scaledBuildup = Math.Max(1, (int)Math.Round(buildup * buildupMultiplier, MidpointRounding.AwayFromZero));
                    if (scaledBuildup != buildup && TrySetIntMember(sphereDamageParameters, "onHitStatusBuildup", scaledBuildup))
                    {
                        changed = true;
                    }
                }
            }

            tunedParameters = sphereDamageParameters;
            return changed;
        }

        private bool HasExplicitAreaRadiusArgument(object[] args)
        {
            return args != null && args.Length > 3 && args[3] is float;
        }

        private bool TryScaleExplicitAreaRadiusArgument(object[] args, float multiplier)
        {
            if (args == null ||
                args.Length <= 3 ||
                !(args[3] is float) ||
                multiplier <= 0f ||
                Math.Abs(multiplier - 1f) <= 0.0001f)
            {
                return false;
            }

            float radius = (float)args[3];
            if (radius <= 0f)
            {
                return false;
            }

            args[3] = radius * multiplier;
            return true;
        }

        private bool TryGetAreaRadiusMultiplier(object attacker, object sphereDamageParameters, out float multiplier)
        {
            multiplier = 1f;
            if (sphereDamageParameters == null)
            {
                return false;
            }

            if (attacker != null && !IsSameModelOrOwner(attacker, GetHero()))
            {
                return false;
            }

            object item = GetMemberValue(sphereDamageParameters, "item");
            string ignored;
            bool isAbhartach = ShouldTuneAbhartach() && IsAbhartachItem(item, out ignored);
            if (isAbhartach)
            {
                if (!_abhartachScaleExplosionRadius.Value)
                {
                    return false;
                }

                multiplier = GetAbhartachExplosionRadiusMultiplier() * GetAbhartachRadiusCorpseQualityMultiplier();
            }
            else
            {
                return false;
            }

            return multiplier > 0f && Math.Abs(multiplier - 1f) > 0.0001f;
        }

        internal float AdjustBloodMagicFindAlivesRange(object unit, object flow, float range)
        {
            if (!ShouldTuneBloodSpells() ||
                !_bloodSpellScaleHeldTargetRange.Value ||
                range <= 0f)
            {
                return range;
            }

            object skill = GetCurrentSkillFromUnitFlow(unit, flow);
            string ignored;
            if (!IsBloodTransfusionItemOrSkill(null, skill, out ignored))
            {
                return range;
            }

            return ScaleRange(range, GetBloodSpellHeldTargetRangeMultiplier());
        }

        internal float AdjustAbhartachFindDeadBodiesRange(object unit, object flow, float range)
        {
            if (!ShouldTuneAbhartach() ||
                !_abhartachScaleCorpseSearchRange.Value ||
                range <= 0f)
            {
                return range;
            }

            object skill = GetCurrentSkillFromUnitFlow(unit, flow);
            string ignored;
            if (!IsAbhartachItemOrSkill(null, skill, out ignored))
            {
                return range;
            }

            return ScaleRange(range, GetAbhartachCorpseSearchRangeMultiplier());
        }

        internal void AdjustAbhartachHeldCorpseSearchRange(object skill, ref float range)
        {
            if (!ShouldTuneAbhartach() ||
                !_abhartachScaleCorpseSearchRange.Value ||
                range <= 0f)
            {
                return;
            }

            string ignored;
            if (IsAbhartachItemOrSkill(null, skill, out ignored))
            {
                range = ScaleRange(range, GetAbhartachCorpseSearchRangeMultiplier());
            }
        }

        private float ScaleRange(float range, float multiplier)
        {
            if (range <= 0f || multiplier <= 0f || Math.Abs(multiplier - 1f) <= 0.0001f)
            {
                return range;
            }

            return range * multiplier;
        }

        private object GetCurrentSkillFromUnitFlow(object unit, object flow)
        {
            if (unit == null || flow == null)
            {
                return null;
            }

            if (_skillUnitsSkillMethod == null)
            {
                Type skillUnitsType = AccessTools.TypeByName(SkillUnitsTypeName);
                if (skillUnitsType == null)
                {
                    return null;
                }

                MethodInfo[] methods = skillUnitsType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name == "Skill" && method.GetParameters().Length == 2)
                    {
                        _skillUnitsSkillMethod = method;
                        break;
                    }
                }
            }

            if (_skillUnitsSkillMethod == null)
            {
                return null;
            }

            try
            {
                return _skillUnitsSkillMethod.Invoke(null, new[] { unit, flow });
            }
            catch
            {
                return null;
            }
        }

        private bool TryApplyAbhartachRawDamageDataTuning(object sphereDamageParameters)
        {
            float multiplier = GetAbhartachExplosionDamageMultiplier() * GetAbhartachCorpseQualityEffectMultiplier();
            if (multiplier <= 0f || Math.Abs(multiplier - 1f) <= 0.0001f)
            {
                return false;
            }

            object rawDamageData = GetMemberValue(sphereDamageParameters, "rawDamageData");
            if (rawDamageData == null)
            {
                return false;
            }

            object clonedRawDamageData = CloneRawDamageData(rawDamageData);
            if (clonedRawDamageData == null)
            {
                return false;
            }

            return TryInvokeNumericMethod(clonedRawDamageData, "AddMultModifier", multiplier - 1f) &&
                TrySetMemberValue(sphereDamageParameters, "rawDamageData", clonedRawDamageData);
        }

        private object CloneRawDamageData(object rawDamageData)
        {
            if (rawDamageData == null)
            {
                return null;
            }

            try
            {
                Type type = rawDamageData.GetType();
                ConstructorInfo copyConstructor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { type },
                    null);
                return copyConstructor == null ? null : copyConstructor.Invoke(new[] { rawDamageData });
            }
            catch
            {
                return null;
            }
        }

        internal void ApplyAbhartachHeldCorpseHealingTuning(object character, ref float healing, object healingItem)
        {
            if (!ShouldTuneAbhartach() ||
                !_abhartachScaleHeldCorpseHealing.Value ||
                healing <= 0f)
            {
                return;
            }

            string ignored;
            bool matchingHealingItem = healingItem != null && IsAbhartachItem(healingItem, out ignored);
            if (!matchingHealingItem && Now > _abhartachHeldHealingActiveUntil)
            {
                return;
            }

            object hero = GetHero();
            if (character != null && hero != null && !IsSameModelOrOwner(character, hero))
            {
                return;
            }

            float multiplier = GetAbhartachHeldCorpseHealingMultiplier() * GetAbhartachHealingCorpseQualityMultiplier();
            if (multiplier > 0f && Math.Abs(multiplier - 1f) > 0.0001f)
            {
                healing *= multiplier;
            }
        }

        internal bool IsBloodMagicHealing(
            object character,
            float healing,
            object healingItem)
        {
            if (healing <= 0f)
            {
                return false;
            }

            object hero = GetHero();
            if (character == null || hero == null || !IsSameModelOrOwner(character, hero))
            {
                return false;
            }

            string ignored;
            if (healingItem != null)
            {
                return IsBloodTransfusionItem(healingItem, out ignored)
                    || IsAbhartachItem(healingItem, out ignored);
            }

            return (_pendingLiveDrainHealingCount > 0
                    && Now <= _liveDrainHealingEligibleUntil)
                || Now <= _abhartachHeldHealingActiveUntil;
        }

        internal bool ApplyLiveDrainHealingTuning(object character, ref float healing, object healingItem)
        {
            if (!ShouldTuneBloodSpells()
                || _liveDrainHealingMultiplier == null
                || healing <= 0f)
            {
                return false;
            }

            object hero = GetHero();
            if (character == null || hero == null || !IsSameModelOrOwner(character, hero))
            {
                return false;
            }

            if (!TryConsumeLiveDrainHealingEligibility(healingItem))
            {
                return false;
            }

            float multiplier = Math.Max(0f, _liveDrainHealingMultiplier.Value);
            healing *= multiplier;
            if (DiagnosticsEnabled())
            {
                Log.LogInfo("Scaled held live-drain healing by "
                    + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + "x.");
            }

            return _suppressGrailFloatingTextLiveDrainHealing != null
                && _suppressGrailFloatingTextLiveDrainHealing.Value
                && BeginGrailFloatingTextDefaultHealingClaim();
        }

        private bool ShouldTuneBloodSpells()
        {
            return _enabled != null &&
                _enabled.Value &&
                _bloodSpellTuningEnabled != null &&
                _bloodSpellTuningEnabled.Value;
        }

        private bool ShouldTuneAbhartach()
        {
            return _enabled != null &&
                _enabled.Value &&
                _abhartachTuningEnabled != null &&
                _abhartachTuningEnabled.Value;
        }

        private void OnItemStatsInitialize(ItemStats itemStats)
        {
            if (itemStats == null || itemStats.ParentModel == null)
            {
                return;
            }

            Item item = itemStats.ParentModel;
            string ignored;
            if (IsBloodTransfusionItem(item, out ignored))
            {
                if (item.LightCastInfo != null)
                {
                    _bloodTransfusionLightCastInfos.Add(item.LightCastInfo);
                }
                if (item.HeavyCastInfo != null)
                {
                    _bloodTransfusionHeavyCastInfos.Add(item.HeavyCastInfo);
                }
                return;
            }

            if (!IsAbhartachItem(item, out ignored))
            {
                return;
            }

            if (item.LightCastInfo != null)
            {
                _abhartachLightCastInfos.Add(item.LightCastInfo);
            }
            if (item.HeavyCastInfo != null)
            {
                _abhartachHeavyCastInfos.Add(item.HeavyCastInfo);
            }
        }

        private bool BeforeGetMagicDescription(
            MagicItemTemplateInfo magicInfo,
            ref string result)
        {
            if (_enabled == null || !_enabled.Value || magicInfo == null)
            {
                return true;
            }

            if (_bloodTransfusionLightCastInfos.Contains(magicInfo))
            {
                if (!ShouldTuneBloodSpells())
                {
                    return true;
                }
                result = BuildBloodTransfusionLightDescription();
                return false;
            }

            if (_bloodTransfusionHeavyCastInfos.Contains(magicInfo))
            {
                result = BuildBloodTransfusionHeavyDescription();
                return false;
            }

            if (!ShouldTuneAbhartach())
            {
                return true;
            }

            if (_abhartachLightCastInfos.Contains(magicInfo))
            {
                result = BuildAbhartachLightDescription();
                return false;
            }

            if (_abhartachHeavyCastInfos.Contains(magicInfo))
            {
                result = BuildAbhartachHeavyDescription();
                return false;
            }

            return true;
        }

        private string BuildBloodTransfusionLightDescription()
        {
            List<string> lines = new List<string>
            {
                "Enemies: Fire a damaging projectile that applies Bleed."
            };
            bool scalesProjectileRange =
                _bloodSpellScaleProjectileTravel != null && _bloodSpellScaleProjectileTravel.Value;
            bool scalesBleedDuration =
                _bloodSpellScaleBleedDuration != null && _bloodSpellScaleBleedDuration.Value;
            List<string> features = new List<string>
            {
                "tap damage"
            };
            if (scalesProjectileRange)
            {
                features.Add("projectile range");
            }
            features.Add(scalesBleedDuration ? "Bleed buildup and duration" : "Bleed buildup");
            features.Add("cast speed");
            lines.Add(
                "Blood Essence: Improves "
                + JoinTooltipFeatures(features)
                + ". Current tap damage bonus: "
                + FormatMultiplierBonus(GetBloodSpellTapDamageMultiplier())
                + ".");

            return string.Join("\n", lines.ToArray());
        }

        private string BuildBloodTransfusionHeavyDescription()
        {
            List<string> lines = new List<string>();
            float corpsePayoutPercent = GetPayoutPercentOfKillXp();
            bool corpseXpEnabled =
                _awardCharacterXp != null && _awardCharacterXp.Value &&
                _rawCharacterXpPerCorpseXp != null && _rawCharacterXpPerCorpseXp.Value > 0f &&
                corpsePayoutPercent > 0f;
            bool corpseHealingEnabled =
                _healCharacter != null && _healCharacter.Value &&
                _healMaxHealthPercentPerXpPercent != null && _healMaxHealthPercentPerXpPercent.Value > 0f &&
                corpsePayoutPercent > 0f;
            bool grantsLiveDrainXp =
                _liveDrainEnabled != null && _liveDrainEnabled.Value &&
                _liveDrainAwardCharacterXp != null && _liveDrainAwardCharacterXp.Value &&
                _awardCharacterXp != null && _awardCharacterXp.Value &&
                _rawCharacterXpPerCorpseXp != null && _rawCharacterXpPerCorpseXp.Value > 0f &&
                _liveDrainRawCharacterXpMultiplier != null && _liveDrainRawCharacterXpMultiplier.Value > 0f &&
                GetLiveDrainXpPercentPerTick() > 0f &&
                GetLiveDrainMaximumXpPercentPerTarget() > 0f;
            lines.Add(grantsLiveDrainXp
                ? "Living enemies: Drain Health to heal yourself; held damage grants limited XP."
                : "Living enemies: Drain Health to heal yourself.");

            if (corpseXpEnabled || corpseHealingEnabled)
            {
                List<string> rewards = new List<string>();
                if (corpseXpEnabled)
                {
                    rewards.Add("XP");
                }
                if (corpseHealingEnabled)
                {
                    rewards.Add("healing");
                }
                rewards.Add("Blood Essence");

                string corpseLine = "Corpses: Drain for " + JoinTooltipFeatures(rewards) + ".";
                if (_handRequirement != null && _handRequirement.Value == HandRequirement.BothHands)
                {
                    corpseLine += " Both hands are required.";
                }
                else if (_singleHandPayoutMultiplier != null &&
                    _singleHandPayoutMultiplier.Value < 0.999f)
                {
                    List<string> scaledRewards = new List<string>();
                    if (corpseXpEnabled)
                    {
                        scaledRewards.Add("XP");
                    }
                    if (corpseHealingEnabled)
                    {
                        scaledRewards.Add("healing");
                    }
                    if (scaledRewards.Count > 0)
                    {
                        corpseLine += " Using both hands grants full "
                            + JoinTooltipFeatures(scaledRewards)
                            + ".";
                    }
                }
                lines.Add(corpseLine);
            }

            if (corpseHealingEnabled &&
                _corpseQualityScaleTransfusionHealing != null &&
                _corpseQualityScaleTransfusionHealing.Value)
            {
                lines.Add("Corpse quality: Improves corpse healing.");
            }

            if (ShouldTuneBloodSpells())
            {
                bool scalesHeldRange =
                    _bloodSpellScaleHeldTargetRange != null && _bloodSpellScaleHeldTargetRange.Value;
                lines.Add(scalesHeldRange
                    ? "Blood Essence: Improves targeting range, Bleed, and channel speed."
                    : "Blood Essence: Improves Bleed and channel speed.");
            }

            return string.Join("\n", lines.ToArray());
        }

        private string BuildAbhartachLightDescription()
        {
            List<string> lines = new List<string>
            {
                "Corpses: Detonate a nearby corpse to damage and Bleed enemies in the area."
            };
            bool scalesSearch =
                _abhartachScaleCorpseSearchRange != null && _abhartachScaleCorpseSearchRange.Value;
            bool scalesDamage =
                _abhartachScaleExplosionDamage != null && _abhartachScaleExplosionDamage.Value;
            bool scalesRadius =
                _abhartachScaleExplosionRadius != null && _abhartachScaleExplosionRadius.Value;
            bool scalesBleed =
                _abhartachScaleExplosionBleed != null && _abhartachScaleExplosionBleed.Value;
            bool scalesBleedDuration =
                _bloodSpellScaleBleedDuration != null && _bloodSpellScaleBleedDuration.Value;

            if (scalesSearch && scalesDamage && scalesRadius && scalesBleed && scalesBleedDuration)
            {
                lines.Add("Blood Essence: Improves corpse search, explosion damage and radius, and Bleed buildup and duration.");
            }
            else
            {
                List<string> powerFeatures = new List<string>();
                if (scalesSearch)
                {
                    powerFeatures.Add("corpse search");
                }
                if (scalesDamage)
                {
                    powerFeatures.Add("explosion damage");
                }
                if (scalesRadius)
                {
                    powerFeatures.Add("explosion radius");
                }
                if (scalesBleed)
                {
                    powerFeatures.Add(scalesBleedDuration ? "Bleed buildup and duration" : "Bleed buildup");
                }
                else if (scalesBleedDuration)
                {
                    powerFeatures.Add("Bleed duration");
                }
                if (powerFeatures.Count > 0)
                {
                    lines.Add("Blood Essence: Improves " + JoinTooltipFeatures(powerFeatures) + ".");
                }
            }

            if (_corpseQualityScaleAbhartachEffects != null &&
                _corpseQualityScaleAbhartachEffects.Value)
            {
                List<string> qualityFeatures = new List<string>();
                if (scalesDamage)
                {
                    qualityFeatures.Add("explosion damage");
                }
                if (scalesRadius)
                {
                    qualityFeatures.Add("radius");
                }
                if (scalesBleed)
                {
                    qualityFeatures.Add("Bleed buildup");
                }
                if (qualityFeatures.Count > 0)
                {
                    lines.Add("Corpse quality: Improves "
                        + JoinTooltipFeatures(qualityFeatures)
                        + ".");
                }
            }

            return string.Join("\n", lines.ToArray());
        }

        private string BuildAbhartachHeavyDescription()
        {
            List<string> lines = new List<string>
            {
                "Corpses: Drain a nearby corpse continuously to heal yourself."
            };
            bool scalesHealing =
                _abhartachScaleHeldCorpseHealing != null && _abhartachScaleHeldCorpseHealing.Value;
            bool scalesSearch =
                _abhartachScaleCorpseSearchRange != null && _abhartachScaleCorpseSearchRange.Value;
            bool scalesQuality = scalesHealing &&
                _corpseQualityScaleAbhartachEffects != null &&
                _corpseQualityScaleAbhartachEffects.Value;
            if (scalesHealing && scalesSearch && scalesQuality)
            {
                lines.Add("Blood Essence and corpse quality improve healing; Blood Essence also extends corpse search.");
            }
            else
            {
                if (scalesHealing)
                {
                    lines.Add(scalesQuality
                        ? "Blood Essence and corpse quality improve healing."
                        : "Blood Essence improves healing.");
                }
                if (scalesSearch)
                {
                    lines.Add("Blood Essence: Extends corpse search.");
                }
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string FormatMultiplierBonus(float multiplier)
        {
            float percent = (multiplier - 1f) * 100f;
            return (percent >= 0f ? "+" : "")
                + percent.ToString("0.#", CultureInfo.InvariantCulture)
                + "%";
        }

        private static string JoinTooltipFeatures(List<string> features)
        {
            if (features == null || features.Count == 0)
            {
                return string.Empty;
            }
            if (features.Count == 1)
            {
                return features[0];
            }
            if (features.Count == 2)
            {
                return features[0] + " and " + features[1];
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < features.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(index == features.Count - 1 ? ", and " : ", ");
                }
                builder.Append(features[index]);
            }
            return builder.ToString();
        }

        private bool IsBleedBuildupStatus(object statusTemplate)
        {
            if (statusTemplate == null)
            {
                return false;
            }

            string matched;
            return ContainsAnyTerm(BuildObjectSearchText(statusTemplate), GetBleedBuildupStatusTerms(), out matched);
        }

        private float GetDamageAmount(object damage)
        {
            float amount = GetOptionalFloatProperty(damage, "Amount", -1f);
            if (amount >= 0f)
            {
                return amount;
            }

            object rawData = GetPropertyValue(damage, "RawData");
            return GetOptionalFloatProperty(rawData, "Amount", 0f);
        }

        private float ResolveCorpseBaseXp(CorpseState state)
        {
            if (state == null)
            {
                return 0f;
            }

            if (state.TargetKillXp <= 0f)
            {
                state.TargetKillXp = TryReadExpReward(state.TargetObject);
            }
            if (state.TargetKillXp <= 0f)
            {
                state.TargetKillXp = TryReadExpReward(state.Corpse);
            }
            if (state.TargetKillXp <= 0f)
            {
                state.TargetKillXp = Math.Max(0f, _fallbackKillXp.Value);
            }

            return state.TargetKillXp;
        }

        private float ResolveCorpseEffectiveKillXp(CorpseState state)
        {
            if (state == null)
            {
                return 0f;
            }

            if (state.TargetEffectiveKillXp <= 0f || !state.TargetEffectiveKillXpHasLevelContext)
            {
                StoreCorpseEffectiveKillXp(state, state.TargetObject);
            }
            if (state.TargetEffectiveKillXp <= 0f || !state.TargetEffectiveKillXpHasLevelContext)
            {
                StoreCorpseEffectiveKillXp(state, state.Corpse);
            }
            if (state.TargetEffectiveKillXp <= 0f)
            {
                state.TargetEffectiveKillXp = Math.Max(0f, _fallbackKillXp.Value);
            }

            return state.TargetEffectiveKillXp;
        }

        private void StoreCorpseEffectiveKillXp(CorpseState state, object owner)
        {
            if (state == null || owner == null)
            {
                return;
            }

            bool hasLevelContext;
            float effectiveKillXp = TryResolveVanillaEffectiveKillXp(owner, out hasLevelContext);
            if (effectiveKillXp <= 0f)
            {
                return;
            }

            if (state.TargetEffectiveKillXp <= 0f ||
                (hasLevelContext && !state.TargetEffectiveKillXpHasLevelContext) ||
                (hasLevelContext == state.TargetEffectiveKillXpHasLevelContext && effectiveKillXp > state.TargetEffectiveKillXp))
            {
                state.TargetEffectiveKillXp = effectiveKillXp;
                state.TargetEffectiveKillXpHasLevelContext = hasLevelContext;
            }
        }

        private float ResolveCorpseTargetMaxHealth(CorpseState state)
        {
            if (state == null)
            {
                return 0f;
            }

            if (state.TargetMaxHealth <= 0f)
            {
                state.TargetMaxHealth = TryGetMaxHealth(state.TargetObject);
            }
            if (state.TargetMaxHealth <= 0f)
            {
                state.TargetMaxHealth = TryGetMaxHealth(state.Corpse);
            }

            return state.TargetMaxHealth;
        }

        private bool TryResolveCorpseNativeTier(CorpseState state, out int nativeTier)
        {
            nativeTier = -1;
            if (state == null)
            {
                return false;
            }

            if (TryReadNativeTier(state.TargetObject, out nativeTier)
                || TryReadNativeTier(state.Corpse, out nativeTier))
            {
                state.NativeTier = nativeTier;
                state.HasNativeTier = true;
                return true;
            }

            if (state.HasNativeTier)
            {
                nativeTier = state.NativeTier;
                return true;
            }

            return false;
        }

        private bool TryReadNativeTier(object owner, out int nativeTier)
        {
            nativeTier = -1;
            if (owner == null)
            {
                return false;
            }

            object template = GetOptionalPropertyValue(owner, "Template");
            if (template != null
                && !ReferenceEquals(template, owner)
                && TryReadNativeTierFromTags(GetMemberValue(template, "Tags"), out nativeTier))
            {
                return true;
            }

            return TryReadNativeTierFromTags(GetMemberValue(owner, "Tags"), out nativeTier);
        }

        private bool TryReadNativeTierFromTags(object tags, out int nativeTier)
        {
            nativeTier = -1;
            IEnumerable enumerable = tags as IEnumerable;
            if (enumerable == null || tags is string)
            {
                return false;
            }

            foreach (object item in enumerable)
            {
                string tag = item as string;
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }

                for (int tier = 0; tier <= 7; tier++)
                {
                    if (string.Equals(
                        tag,
                        "Tier:" + tier.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                    {
                        nativeTier = tier;
                        return true;
                    }
                }
            }

            return false;
        }

        private float ResolveCorpseExpLevel(CorpseState state)
        {
            if (state == null)
            {
                return -1f;
            }

            float expLevel = TryReadExpLevel(state.TargetObject);
            if (expLevel < 0f)
            {
                expLevel = TryReadExpLevel(state.Corpse);
            }
            if (expLevel >= 0f)
            {
                state.TargetExpLevel = expLevel;
                state.HasTargetExpLevel = true;
                return expLevel;
            }

            return state.HasTargetExpLevel ? state.TargetExpLevel : -1f;
        }

        private Grailwright.Shared.CorpseQualityThreatClass ResolveCorpseThreatClass(
            CorpseState state)
        {
            if (state == null)
            {
                return Grailwright.Shared.CorpseQualityThreatClass.Normal;
            }

            Grailwright.Shared.CorpseQualityThreatClass threatClass;
            if (TryReadCorpseThreatClass(state.TargetObject, out threatClass)
                || TryReadCorpseThreatClass(state.Corpse, out threatClass))
            {
                state.TargetThreatClass = threatClass;
                state.HasTargetThreatClass = true;
                return threatClass;
            }

            return state.HasTargetThreatClass
                ? state.TargetThreatClass
                : Grailwright.Shared.CorpseQualityThreatClass.Normal;
        }

        private bool TryReadCorpseThreatClass(
            object owner,
            out Grailwright.Shared.CorpseQualityThreatClass threatClass)
        {
            threatClass = Grailwright.Shared.CorpseQualityThreatClass.Normal;
            if (owner == null)
            {
                return false;
            }

            object template = GetOptionalPropertyValue(owner, "Template");
            if (template != null
                && !ReferenceEquals(template, owner)
                && TryReadCorpseThreatClassFromNpcType(
                    GetMemberValue(template, "NpcType"),
                    out threatClass))
            {
                return true;
            }

            return TryReadCorpseThreatClassFromNpcType(
                GetMemberValue(owner, "NpcType"),
                out threatClass);
        }

        private bool TryReadCorpseThreatClassFromNpcType(
            object npcType,
            out Grailwright.Shared.CorpseQualityThreatClass threatClass)
        {
            threatClass = Grailwright.Shared.CorpseQualityThreatClass.Normal;
            if (npcType == null)
            {
                return false;
            }

            string name = npcType.ToString();
            if (string.Equals(name, "Elite", StringComparison.Ordinal))
            {
                threatClass = Grailwright.Shared.CorpseQualityThreatClass.Elite;
            }
            else if (string.Equals(name, "MiniBoss", StringComparison.Ordinal))
            {
                threatClass = Grailwright.Shared.CorpseQualityThreatClass.MiniBoss;
            }
            else if (string.Equals(name, "Boss", StringComparison.Ordinal))
            {
                threatClass = Grailwright.Shared.CorpseQualityThreatClass.Boss;
            }

            return true;
        }

        private float GetHealingPowerScale(CorpseState state)
        {
            if (_healPowerScalingMode == null || _healPowerScalingMode.Value == HealingPowerScalingMode.Off)
            {
                return 1f;
            }

            float targetMaxHealth = ResolveCorpseTargetMaxHealth(state);
            if (targetMaxHealth <= 0f)
            {
                return 1f;
            }

            float reference = Math.Max(1f, _healReferenceTargetMaxHealth.Value);
            float exponent = Mathf.Clamp(_healPowerExponent.Value, 0.05f, 3f);
            float rawScale = Mathf.Pow(Math.Max(0.0001f, targetMaxHealth / reference), exponent);
            float minScale = Math.Max(0f, _healMinimumPowerScale.Value);
            float maxScale = Math.Max(minScale, _healMaximumPowerScale.Value);
            return Mathf.Clamp(rawScale, minScale, maxScale);
        }

        private float GetCorpseQuality01(CorpseState state)
        {
            if (!IsCorpseBloodMagicEligibleForInterop(state))
            {
                return 0f;
            }

            return CalculateCorpseQuality01(state, true);
        }

        private float CalculateCorpseQuality01(
            CorpseState state,
            bool logSample)
        {
            if (state == null)
            {
                return 0f;
            }

            float referenceXp = Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceKillXp;
            float referenceMaxHealth = Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceMaxHealth;

            float baseXp = ResolveCorpseBaseXp(state);
            float xpQuality = baseXp > 0f
                ? Mathf.Clamp01(baseXp / referenceXp)
                : 0f;

            float targetMaxHealth = ResolveCorpseTargetMaxHealth(state);
            float healthQuality = targetMaxHealth > 0f
                ? Mathf.Clamp01(targetMaxHealth / referenceMaxHealth)
                : 0f;

            int nativeTier;
            bool hasNativeTier = TryResolveCorpseNativeTier(state, out nativeTier);
            bool hasQualityEvidence;
            bool usedNativeTier;
            float intrinsicQuality = Grailwright.Shared.CorpseQualityBuckets.CalculateIntrinsicQuality01(
                hasNativeTier ? nativeTier : -1,
                baseXp,
                referenceXp,
                targetMaxHealth,
                referenceMaxHealth,
                out hasQualityEvidence,
                out usedNativeTier);
            bool fallbackUsed = !hasQualityEvidence;
            if (fallbackUsed)
            {
                intrinsicQuality = Mathf.Clamp01(_corpseQualityFallbackQuality == null
                    ? 0f
                    : _corpseQualityFallbackQuality.Value);
            }

            Grailwright.Shared.CorpseQualityThreatClass threatClass =
                ResolveCorpseThreatClass(state);
            float classAdjustedQuality =
                Grailwright.Shared.CorpseQualityBuckets.ApplyThreatClassAdjustment(
                    intrinsicQuality,
                    threatClass);
            float enemyExpLevel = ResolveCorpseExpLevel(state);
            float heroLevel = TryReadHeroLevel();
            bool levelAdjusted = false;
            float quality =
                Grailwright.Shared.CorpseQualityBuckets.ApplyBoundedRelativeLevelAdjustment(
                    classAdjustedQuality,
                    enemyExpLevel,
                    heroLevel,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultLevelQualityPerLevel,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultMaximumLevelQualityAdjustment,
                    out levelAdjusted);
            if (logSample)
            {
                LogCorpseQualitySample(
                    state,
                    baseXp,
                    targetMaxHealth,
                    referenceXp,
                    referenceMaxHealth,
                    xpQuality,
                    healthQuality,
                    nativeTier,
                    usedNativeTier,
                    intrinsicQuality,
                    threatClass,
                    classAdjustedQuality,
                    enemyExpLevel,
                    heroLevel,
                    levelAdjusted,
                    quality,
                    fallbackUsed);
            }

            return Mathf.Clamp01(quality);
        }

        private void LogCorpseQualitySample(
            CorpseState state,
            float baseXp,
            float targetMaxHealth,
            float referenceXp,
            float referenceMaxHealth,
            float xpQuality,
            float healthQuality,
            int nativeTier,
            bool usedNativeTier,
            float intrinsicQuality,
            Grailwright.Shared.CorpseQualityThreatClass threatClass,
            float classAdjustedQuality,
            float enemyExpLevel,
            float heroLevel,
            bool levelAdjusted,
            float finalQuality,
            bool fallbackUsed)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            float now = Now;
            if (now < _nextCorpseQualityLogTime)
            {
                return;
            }

            _nextCorpseQualityLogTime = now + 1.0f;

            Log.LogInfo(
                "Corpse quality sample #" + state.DebugId.ToString(CultureInfo.InvariantCulture)
                + " " + DescribeCorpse(state)
                + ": baseXp=" + baseXp.ToString("0.###", CultureInfo.InvariantCulture)
                + "; targetMaxHealth=" + targetMaxHealth.ToString("0.###", CultureInfo.InvariantCulture)
                + "; referenceKillXP=" + referenceXp.ToString("0.###", CultureInfo.InvariantCulture)
                + "; referenceMaxHealth=" + referenceMaxHealth.ToString("0.###", CultureInfo.InvariantCulture)
                + "; xpQuality=" + xpQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; healthQuality=" + healthQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; nativeTier=" + (usedNativeTier
                    ? nativeTier.ToString(CultureInfo.InvariantCulture)
                    : "none")
                + "; intrinsicQuality=" + intrinsicQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; threatClass=" + threatClass.ToString()
                + "; classAdjustedQuality=" + classAdjustedQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; enemyExpLevel=" + enemyExpLevel.ToString("0.###", CultureInfo.InvariantCulture)
                + "; heroLevel=" + heroLevel.ToString("0.###", CultureInfo.InvariantCulture)
                + "; levelAdjusted=" + levelAdjusted.ToString()
                + "; finalQuality=" + finalQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; fallbackUsed=" + fallbackUsed.ToString()
                + ".");

            string qualityLabel = GetCorpseQualityLabel(finalQuality);
            string gftSignature = state.DebugId.ToString(CultureInfo.InvariantCulture)
                + "|"
                + qualityLabel
                + "|"
                + fallbackUsed;
            if (ShouldShowBloodMagicDiagnostic()
                && !string.Equals(
                    gftSignature,
                    _lastGftCorpseQualitySignature,
                    StringComparison.Ordinal))
            {
                _lastGftCorpseQualitySignature = gftSignature;
                ShowBloodMagicDiagnostic(
                    "blood-magic-corpse-quality",
                    "Blood Magic: "
                        + DescribeCorpse(state)
                        + " is "
                        + qualityLabel
                        + " quality ("
                        + (finalQuality * 100.0f).ToString(
                            "0",
                            CultureInfo.InvariantCulture)
                        + "%"
                        + (fallbackUsed ? ", fallback" : "")
                        + ").");
            }
        }

        private int GetCorpseQualityTier(float quality)
        {
            return (int)Grailwright.Shared.CorpseQualityBuckets.GetTier(
                quality,
                true);
        }

        private float GetTransfusionHealingQualityMultiplier(CorpseState state)
        {
            if (_corpseQualityScaleTransfusionHealing == null || !_corpseQualityScaleTransfusionHealing.Value)
            {
                return 1f;
            }

            return GetCorpseQualityEffectMultiplier(GetCorpseQuality01(state));
        }

        private float GetAbhartachCorpseQualityEffectMultiplier()
        {
            if (_corpseQualityScaleAbhartachEffects == null || !_corpseQualityScaleAbhartachEffects.Value)
            {
                return 1f;
            }

            return GetCorpseQualityEffectMultiplier(GetCurrentAbhartachCorpseQuality01());
        }

        private float GetAbhartachRadiusCorpseQualityMultiplier()
        {
            if (_corpseQualityScaleAbhartachEffects == null || !_corpseQualityScaleAbhartachEffects.Value)
            {
                return 1f;
            }

            return GetCorpseQualityEffectMultiplier(
                GetCurrentAbhartachCorpseQuality01(),
                _abhartachRadiusMinimumQualityMultiplier,
                _abhartachRadiusMaximumQualityMultiplier,
                0.85f,
                1.15f);
        }

        private float GetAbhartachHealingCorpseQualityMultiplier()
        {
            if (_corpseQualityScaleAbhartachEffects == null || !_corpseQualityScaleAbhartachEffects.Value)
            {
                return 1f;
            }

            return GetCorpseQualityEffectMultiplier(
                GetCurrentAbhartachCorpseQuality01(),
                _abhartachHealingMinimumQualityMultiplier,
                _abhartachHealingMaximumQualityMultiplier,
                0.75f,
                1.25f);
        }

        internal float GetCorpseQualityEffectMultiplier(float quality)
        {
            return GetCorpseQualityEffectMultiplier(
                quality,
                _corpseQualityMinimumEffectMultiplier,
                _corpseQualityMaximumEffectMultiplier,
                0.5f,
                1.5f);
        }

        private static float GetCorpseQualityEffectMultiplier(
            float quality,
            ConfigEntry<float> minimumEntry,
            ConfigEntry<float> maximumEntry,
            float fallbackMinimum,
            float fallbackMaximum)
        {
            float min = Mathf.Clamp(
                minimumEntry == null ? fallbackMinimum : minimumEntry.Value,
                0f,
                10f);
            float max = Mathf.Clamp(
                maximumEntry == null ? fallbackMaximum : maximumEntry.Value,
                0f,
                10f);
            if (max < min)
            {
                max = min;
            }

            return Mathf.Lerp(min, max, Mathf.Clamp01(quality));
        }

        private float GetCurrentAbhartachCorpseQuality01()
        {
            CorpseState state;
            if (TryGetLookedAtCorpseState(out state) && IsCorpseBloodMagicEligibleForInterop(state))
            {
                return RecordAbhartachCorpseQuality(state);
            }

            if (Now <= _lastAbhartachCorpseQualityUntil)
            {
                return Mathf.Clamp01(_lastAbhartachCorpseQuality01);
            }

            return 0.5f;
        }

        private float RecordAbhartachCorpseQuality(CorpseState state)
        {
            float quality = GetCorpseQuality01(state);
            if (quality <= 0f)
            {
                return 0.5f;
            }

            _lastAbhartachCorpseQuality01 = quality;
            float memorySeconds = _corpseQualityEffectMemorySeconds == null
                ? 1.25f
                : _corpseQualityEffectMemorySeconds.Value;
            _lastAbhartachCorpseQualityUntil = Now + Math.Max(0f, memorySeconds);
            return quality;
        }

        private void RecordAbhartachFocusedCorpseQuality()
        {
            CorpseState state;
            if (TryGetLookedAtCorpseState(out state) && IsCorpseBloodMagicEligibleForInterop(state))
            {
                RecordAbhartachCorpseQuality(state);
            }
        }

        private float GetPayoutPercentOfKillXp()
        {
            return Math.Max(0f, _customPayoutPercentOfKillXp.Value);
        }

        private float GetSecondsRequired()
        {
            return Math.Max(0.1f, _secondsRequired.Value);
        }

        private float GetHandPayoutMultiplier(int activeHandCount)
        {
            if (activeHandCount >= 2)
            {
                return 1.0f;
            }

            return Math.Max(0f, _singleHandPayoutMultiplier.Value);
        }

        private float GetLiveDrainXpTickIntervalSeconds()
        {
            return Math.Max(
                0.1f,
                _customLiveDrainXpTickIntervalSeconds.Value);
        }

        private float GetLiveDrainXpPercentPerTick()
        {
            return Math.Max(0f, _customLiveDrainXpPercentPerTick.Value);
        }

        private float GetLiveDrainMaximumXpPercentPerTarget()
        {
            return Math.Max(
                0f,
                _customLiveDrainMaximumXpPercentPerTarget.Value);
        }

        private float GetBloodSpellProjectileTravelMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                BloodSpellProjectileTravelProgressionBase,
                GetBloodSpellProjectileTravelGrowthMultiplier());
        }

        private float GetBloodSpellTapDamageMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                BloodSpellTapDamageProgressionBase,
                GetBloodSpellTapDamageGrowthMultiplier());
        }

        private float GetBloodSpellHomingTargetSearchMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                BloodSpellHomingSearchProgressionBase,
                GetBloodSpellTargetSearchGrowthMultiplier(),
                _bloodSpellHomingTargetSearchMaximumMultiplier.Value);
        }

        private float GetBloodSpellHeldTargetRangeMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                BloodSpellHeldRangeProgressionBase,
                GetBloodSpellTargetSearchGrowthMultiplier(),
                _bloodSpellHeldTargetRangeMaximumMultiplier.Value);
        }

        private float GetBloodSpellBleedBuildupMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                BloodSpellBleedProgressionBase,
                GetBloodSpellBleedBuildupGrowthMultiplier());
        }

        private float GetBloodSpellTapCastSpeedMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                BloodSpellTapSpeedProgressionBase,
                GetBloodSpellTapCastSpeedGrowthMultiplier());
        }

        private float GetBloodSpellHeldChannelSpeedMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                BloodSpellHeldSpeedProgressionBase,
                GetBloodSpellHeldGrowthMultiplier());
        }

        private float GetAbhartachExplosionDamageMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                AbhartachExplosionDamageProgressionBase,
                GetBloodMagicGrowthMultiplier(GetAbhartachExplosionDamageCurve()));
        }

        private float GetAbhartachExplosionRadiusMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                AbhartachExplosionRadiusProgressionBase,
                GetBloodMagicGrowthMultiplier(GetAbhartachExplosionRadiusCurve()));
        }

        private float GetAbhartachExplosionBleedBuildupMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                AbhartachExplosionBleedProgressionBase,
                GetBloodMagicGrowthMultiplier(GetAbhartachExplosionBleedCurve()));
        }

        private float GetAbhartachHeldCorpseHealingMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                AbhartachHeldHealingProgressionBase,
                GetBloodMagicGrowthMultiplier(GetAbhartachHeldHealingCurve()));
        }

        private float GetAbhartachCorpseSearchRangeMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                AbhartachCorpseSearchProgressionBase,
                GetBloodMagicGrowthMultiplier(GetAbhartachCorpseSearchCurve()),
                _abhartachCorpseSearchMaximumMultiplier.Value);
        }

        private float GetBloodSpellProjectileTravelGrowthMultiplier()
        {
            return GetBloodMagicGrowthMultiplier(GetProjectileTravelBloodPowerCurve());
        }

        private float GetBloodSpellTapDamageGrowthMultiplier()
        {
            return GetBloodMagicGrowthMultiplier(GetTapDamageBloodPowerCurve());
        }

        private float GetBloodSpellBleedBuildupGrowthMultiplier()
        {
            return GetBloodMagicGrowthMultiplier(GetBleedBuildupBloodPowerCurve());
        }

        private float GetBloodSpellTapCastSpeedGrowthMultiplier()
        {
            return GetBloodMagicGrowthMultiplier(GetTapCastSpeedBloodPowerCurve());
        }

        private float GetBloodSpellTargetSearchGrowthMultiplier()
        {
            return GetBloodMagicGrowthMultiplier(GetTargetSearchBloodPowerCurve());
        }

        private float GetBloodSpellHeldGrowthMultiplier()
        {
            return GetBloodMagicGrowthMultiplier(GetHeldBloodPowerCurve());
        }

        private float GetBloodMagicGrowthMultiplier(CurvePoint[] curve)
        {
            float growthValue = GetBloodPower();
            if (growthValue <= 0f)
            {
                return 1f;
            }

            float curveGrowthValue = Math.Min(growthValue, NormalMaximumBloodPower);
            float bonusPercent = EvaluateCurve(curve, curveGrowthValue, 0f);
            return 1f + (Math.Max(0f, bonusPercent) / 100f);
        }

        private float GetBloodSpellTunedMultiplier(float progressionBase, float growthMultiplier)
        {
            return GetBloodSpellTunedMultiplier(progressionBase, growthMultiplier, 10f);
        }

        private float GetBloodSpellTunedMultiplier(float progressionBase, float growthMultiplier, float maximum)
        {
            float value = Math.Max(0f, progressionBase) * Math.Max(0f, growthMultiplier);
            float power = GetBloodPower();
            float unlock = Mathf.Clamp01(power / NormalMaximumBloodPower);
            value = 1.0f + ((value - 1.0f) * unlock);
            value = 1.0f
                + ((value - 1.0f) * (1.0f + GetBloodPowerOvermasteryBonusFraction(power)));
            return Mathf.Clamp(value, 0f, Math.Max(0f, maximum));
        }

        private CurvePoint[] GetProjectileTravelBloodPowerCurve()
        {
            string raw = _bloodSpellProjectileTravelBloodPowerBonusCurve == null
                ? ""
                : (_bloodSpellProjectileTravelBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedProjectileTravelBloodPowerCurveRaw)
            {
                _cachedProjectileTravelBloodPowerCurveRaw = raw;
                _cachedProjectileTravelBloodPowerCurve = ParseCurve(raw, GetDefaultProjectileTravelBloodPowerCurve());
            }

            return _cachedProjectileTravelBloodPowerCurve;
        }

        private CurvePoint[] GetTapDamageBloodPowerCurve()
        {
            string raw = _bloodSpellTapDamageBloodPowerBonusCurve == null
                ? ""
                : (_bloodSpellTapDamageBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedTapDamageBloodPowerCurveRaw)
            {
                _cachedTapDamageBloodPowerCurveRaw = raw;
                _cachedTapDamageBloodPowerCurve = ParseCurve(raw, GetDefaultTapDamageBloodPowerCurve());
            }

            return _cachedTapDamageBloodPowerCurve;
        }

        private CurvePoint[] GetBleedBuildupBloodPowerCurve()
        {
            string raw = _bloodSpellBleedBuildupBloodPowerBonusCurve == null
                ? ""
                : (_bloodSpellBleedBuildupBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedBleedBuildupBloodPowerCurveRaw)
            {
                _cachedBleedBuildupBloodPowerCurveRaw = raw;
                _cachedBleedBuildupBloodPowerCurve = ParseCurve(raw, GetDefaultBleedBuildupBloodPowerCurve());
            }

            return _cachedBleedBuildupBloodPowerCurve;
        }

        private CurvePoint[] GetTapCastSpeedBloodPowerCurve()
        {
            string raw = _bloodSpellTapCastSpeedBloodPowerBonusCurve == null
                ? ""
                : (_bloodSpellTapCastSpeedBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedTapCastSpeedBloodPowerCurveRaw)
            {
                _cachedTapCastSpeedBloodPowerCurveRaw = raw;
                _cachedTapCastSpeedBloodPowerCurve = ParseCurve(raw, GetDefaultTapCastSpeedBloodPowerCurve());
            }

            return _cachedTapCastSpeedBloodPowerCurve;
        }

        private CurvePoint[] GetTargetSearchBloodPowerCurve()
        {
            string raw = _bloodSpellTargetSearchBloodPowerBonusCurve == null
                ? ""
                : (_bloodSpellTargetSearchBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedTargetSearchBloodPowerCurveRaw)
            {
                _cachedTargetSearchBloodPowerCurveRaw = raw;
                _cachedTargetSearchBloodPowerCurve = ParseCurve(raw, GetDefaultTargetSearchBloodPowerCurve());
            }

            return _cachedTargetSearchBloodPowerCurve;
        }

        private CurvePoint[] GetHeldBloodPowerCurve()
        {
            string raw = _bloodSpellHeldBloodPowerBonusCurve == null
                ? ""
                : (_bloodSpellHeldBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedHeldBloodPowerCurveRaw)
            {
                _cachedHeldBloodPowerCurveRaw = raw;
                _cachedHeldBloodPowerCurve = ParseCurve(raw, GetDefaultHeldBloodPowerCurve());
            }

            return _cachedHeldBloodPowerCurve;
        }

        private CurvePoint[] GetAbhartachExplosionDamageCurve()
        {
            string raw = _abhartachExplosionDamageBloodPowerBonusCurve == null
                ? ""
                : (_abhartachExplosionDamageBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachExplosionDamageCurveRaw)
            {
                _cachedAbhartachExplosionDamageCurveRaw = raw;
                _cachedAbhartachExplosionDamageCurve = ParseCurve(raw, GetDefaultAbhartachExplosionDamageCurve());
            }

            return _cachedAbhartachExplosionDamageCurve;
        }

        private CurvePoint[] GetAbhartachExplosionRadiusCurve()
        {
            string raw = _abhartachExplosionRadiusBloodPowerBonusCurve == null
                ? ""
                : (_abhartachExplosionRadiusBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachExplosionRadiusCurveRaw)
            {
                _cachedAbhartachExplosionRadiusCurveRaw = raw;
                _cachedAbhartachExplosionRadiusCurve = ParseCurve(raw, GetDefaultAbhartachExplosionRadiusCurve());
            }

            return _cachedAbhartachExplosionRadiusCurve;
        }

        private CurvePoint[] GetAbhartachExplosionBleedCurve()
        {
            string raw = _abhartachExplosionBleedBloodPowerBonusCurve == null
                ? ""
                : (_abhartachExplosionBleedBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachExplosionBleedCurveRaw)
            {
                _cachedAbhartachExplosionBleedCurveRaw = raw;
                _cachedAbhartachExplosionBleedCurve = ParseCurve(raw, GetDefaultAbhartachExplosionDamageCurve());
            }

            return _cachedAbhartachExplosionBleedCurve;
        }

        private CurvePoint[] GetAbhartachHeldHealingCurve()
        {
            string raw = _abhartachHeldHealingBloodPowerBonusCurve == null
                ? ""
                : (_abhartachHeldHealingBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachHeldHealingCurveRaw)
            {
                _cachedAbhartachHeldHealingCurveRaw = raw;
                _cachedAbhartachHeldHealingCurve = ParseCurve(raw, GetDefaultAbhartachHeldHealingCurve());
            }

            return _cachedAbhartachHeldHealingCurve;
        }

        private CurvePoint[] GetAbhartachCorpseSearchCurve()
        {
            string raw = _abhartachCorpseSearchBloodPowerBonusCurve == null
                ? ""
                : (_abhartachCorpseSearchBloodPowerBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachCorpseSearchCurveRaw)
            {
                _cachedAbhartachCorpseSearchCurveRaw = raw;
                _cachedAbhartachCorpseSearchCurve = ParseCurve(raw, GetDefaultTargetSearchBloodPowerCurve());
            }

            return _cachedAbhartachCorpseSearchCurve;
        }

        private CurvePoint[] ParseCurve(string raw, CurvePoint[] fallback)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            string[] entries = raw.Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<CurvePoint> points = new List<CurvePoint>();
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                string[] pair = entry.Split(new[] { ':', '=' }, 2);
                if (pair.Length != 2)
                {
                    continue;
                }

                float x;
                float y;
                if (float.TryParse(pair[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                    float.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                {
                    points.Add(new CurvePoint(x, y));
                }
            }

            if (points.Count == 0)
            {
                return fallback;
            }

            points.Sort(delegate(CurvePoint first, CurvePoint second)
            {
                return first.X.CompareTo(second.X);
            });

            return points.ToArray();
        }

        private float EvaluateCurve(CurvePoint[] points, float x, float fallback)
        {
            if (points == null || points.Length == 0)
            {
                return fallback;
            }

            if (x <= points[0].X)
            {
                return points[0].Y;
            }

            for (int i = 1; i < points.Length; i++)
            {
                if (x <= points[i].X)
                {
                    float span = points[i].X - points[i - 1].X;
                    if (span <= 0.0001f)
                    {
                        return points[i].Y;
                    }

                    float t = Mathf.Clamp01((x - points[i - 1].X) / span);
                    return Mathf.Lerp(points[i - 1].Y, points[i].Y, t);
                }
            }

            return points[points.Length - 1].Y;
        }

        private CurvePoint[] GetDefaultProjectileTravelBloodPowerCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 1f),
                new CurvePoint(10f, 3f),
                new CurvePoint(15f, 6f),
                new CurvePoint(20f, 11f),
                new CurvePoint(25f, 16f),
                new CurvePoint(30f, 22f),
                new CurvePoint(35f, 29f),
                new CurvePoint(40f, 37f),
                new CurvePoint(45f, 47f),
                new CurvePoint(50f, 56f)
            };
        }

        private CurvePoint[] GetDefaultTapDamageBloodPowerCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 1f),
                new CurvePoint(10f, 2f),
                new CurvePoint(15f, 4f),
                new CurvePoint(20f, 6f),
                new CurvePoint(25f, 9f),
                new CurvePoint(30f, 13f),
                new CurvePoint(35f, 18f),
                new CurvePoint(40f, 23f),
                new CurvePoint(45f, 28f),
                new CurvePoint(50f, 34f)
            };
        }

        private CurvePoint[] GetDefaultBleedBuildupBloodPowerCurve()
        {
            return GetDefaultProjectileTravelBloodPowerCurve();
        }

        private CurvePoint[] GetDefaultTapCastSpeedBloodPowerCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 0f),
                new CurvePoint(10f, 1f),
                new CurvePoint(15f, 2f),
                new CurvePoint(20f, 4f),
                new CurvePoint(25f, 6f),
                new CurvePoint(30f, 8f),
                new CurvePoint(35f, 11f),
                new CurvePoint(40f, 14f),
                new CurvePoint(45f, 18f),
                new CurvePoint(50f, 21f)
            };
        }

        private CurvePoint[] GetDefaultTargetSearchBloodPowerCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 0f),
                new CurvePoint(10f, 2f),
                new CurvePoint(15f, 4f),
                new CurvePoint(20f, 6f),
                new CurvePoint(25f, 9f),
                new CurvePoint(30f, 12f),
                new CurvePoint(35f, 16f),
                new CurvePoint(40f, 22f),
                new CurvePoint(45f, 28f),
                new CurvePoint(50f, 35f)
            };
        }

        private CurvePoint[] GetDefaultHeldBloodPowerCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 0f),
                new CurvePoint(10f, 1f),
                new CurvePoint(15f, 2f),
                new CurvePoint(20f, 3f),
                new CurvePoint(25f, 4f),
                new CurvePoint(30f, 5f),
                new CurvePoint(35f, 6f),
                new CurvePoint(40f, 8f),
                new CurvePoint(45f, 10f),
                new CurvePoint(50f, 12f)
            };
        }

        private CurvePoint[] GetDefaultAbhartachExplosionDamageCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 1f),
                new CurvePoint(10f, 3f),
                new CurvePoint(15f, 6f),
                new CurvePoint(20f, 10f),
                new CurvePoint(25f, 14f),
                new CurvePoint(30f, 18f),
                new CurvePoint(35f, 23f),
                new CurvePoint(40f, 28f),
                new CurvePoint(45f, 34f),
                new CurvePoint(50f, 40f)
            };
        }

        private CurvePoint[] GetDefaultAbhartachExplosionRadiusCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 1f),
                new CurvePoint(10f, 2f),
                new CurvePoint(15f, 4f),
                new CurvePoint(20f, 7f),
                new CurvePoint(25f, 10f),
                new CurvePoint(30f, 14f),
                new CurvePoint(35f, 18f),
                new CurvePoint(40f, 23f),
                new CurvePoint(45f, 29f),
                new CurvePoint(50f, 35f)
            };
        }

        private CurvePoint[] GetDefaultAbhartachHeldHealingCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 1f),
                new CurvePoint(10f, 4f),
                new CurvePoint(15f, 7f),
                new CurvePoint(20f, 10f),
                new CurvePoint(25f, 14f),
                new CurvePoint(30f, 18f),
                new CurvePoint(35f, 23f),
                new CurvePoint(40f, 28f),
                new CurvePoint(45f, 34f),
                new CurvePoint(50f, 40f)
            };
        }

        private float RoundXp(float amount)
        {
            float roundTo = _roundXpTo.Value;
            if (roundTo <= 0f || amount <= 0f)
            {
                return amount;
            }

            return (float)(Math.Round(amount / roundTo, MidpointRounding.AwayFromZero) * roundTo);
        }

        private bool TryClaimGrailFloatingTextCorpseXp(
            float amount,
            CorpseState state,
            float essenceAwardValue)
        {
            if (amount <= 0f ||
                _claimGrailFloatingTextCorpseXp == null ||
                !_claimGrailFloatingTextCorpseXp.Value)
            {
                return false;
            }

            if (!TryResolveGrailFloatingTextBridge()
                || _grailFloatingTextTryCancelXpGainClaimMethod == null)
            {
                return false;
            }

            float corpseQuality = GetCorpseQuality01(state);
            string qualityLabel = GetCorpseQualityLabel(corpseQuality);
            string essenceAward = essenceAwardValue
                .ToString("0", CultureInfo.InvariantCulture);
            return TryClaimGrailFloatingTextXp(
                amount,
                GrailFloatingTextCorpseXpEventId,
                string.Empty,
                "+" + amount.ToString("F0", CultureInfo.InvariantCulture) + " XP | +" + essenceAward + " Blood Essence",
                "+{xp} XP | +" + essenceAward + " Blood Essence",
                "corpse_" + qualityLabel.ToLowerInvariant(),
                false);
        }

        private bool TryClaimGrailFloatingTextLiveDrainXp(float amount)
        {
            if (amount <= 0f ||
                _claimGrailFloatingTextLiveDrainXp == null ||
                !_claimGrailFloatingTextLiveDrainXp.Value)
            {
                return false;
            }

            return TryClaimGrailFloatingTextXp(
                amount,
                GrailFloatingTextLiveDrainXpEventId,
                "live-drain-xp",
                "+" + amount.ToString("F0", CultureInfo.InvariantCulture) + " XP (Live Drain)",
                "+{xp} XP (Live Drain)",
                "magic",
                true);
        }

        private bool TryShowBloodPowerMilestone(BloodPowerMilestone milestone)
        {
            if (milestone == null ||
                !TryResolveGrailFloatingTextBridge() ||
                _grailFloatingTextTryShowEventMethod == null)
            {
                return false;
            }

            try
            {
                object result = _grailFloatingTextTryShowEventMethod.Invoke(
                    null,
                    new object[]
                    {
                        PluginGuid,
                        milestone.EventId,
                        milestone.Text,
                        GrailFloatingTextBloodPowerStyle,
                        "Reward",
                        "High",
                        string.Empty,
                        GrailFloatingTextBloodPowerIconId,
                        GrailFloatingTextBloodPowerDurationBucket,
                        0.25f,
                        0.95f
                    });
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                LogGrailFloatingTextUnavailableOnce(
                    "Grail Floating Text failed to show Blood Power progress: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool TryClaimGrailFloatingTextXp(
            float amount,
            string eventId,
            string consolidationKey,
            string text,
            string textFormat,
            string iconId,
            bool consolidate)
        {
            if (!TryResolveGrailFloatingTextBridge())
            {
                return false;
            }

            try
            {
                if (consolidate && _grailFloatingTextTryClaimConsolidatedXpGainMethod != null)
                {
                    object consolidatedResult = _grailFloatingTextTryClaimConsolidatedXpGainMethod.Invoke(
                        null,
                        new object[]
                        {
                            PluginGuid,
                            eventId,
                            consolidationKey,
                            textFormat,
                            "Red",
                            "Reward",
                            "High",
                            iconId,
                            GrailFloatingTextShortDurationBucket,
                            amount,
                            0.25f,
                            0.9f
                        });
                    if (consolidatedResult is bool && (bool)consolidatedResult)
                    {
                        return true;
                    }
                }

                if (_grailFloatingTextTryClaimXpGainMethod == null)
                {
                    return false;
                }

                object result = _grailFloatingTextTryClaimXpGainMethod.Invoke(
                    null,
                    new object[]
                    {
                        PluginGuid,
                        eventId,
                        text,
                        "Red",
                        "Reward",
                        "High",
                        iconId,
                        GrailFloatingTextShortDurationBucket,
                        amount,
                        0.25f,
                        0.9f
                    });
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text failed to claim Blood Magic XP text: " + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool TryCancelGrailFloatingTextXpClaim(
            string eventId,
            float expectedAmount)
        {
            if (_grailFloatingTextTryCancelXpGainClaimMethod == null)
            {
                return false;
            }

            try
            {
                object result = _grailFloatingTextTryCancelXpGainClaimMethod.Invoke(
                    null,
                    new object[] { PluginGuid, eventId, expectedAmount });
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                LogGrailFloatingTextUnavailableOnce(
                    "Grail Floating Text failed to cancel a Blood Magic XP claim: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool BeginGrailFloatingTextDefaultHealingClaim()
        {
            if (_grailFloatingTextHealingClaimDepth > 0)
            {
                _grailFloatingTextHealingClaimDepth++;
                return true;
            }

            if (!TrySetGrailFloatingTextBuiltInEventClaim(
                    GrailFloatingTextDefaultHealingEventId,
                    true))
            {
                return false;
            }

            _grailFloatingTextHealingClaimDepth = 1;
            return true;
        }

        private void EndGrailFloatingTextDefaultHealingClaim()
        {
            if (_grailFloatingTextHealingClaimDepth <= 0)
            {
                return;
            }

            _grailFloatingTextHealingClaimDepth--;
            if (_grailFloatingTextHealingClaimDepth == 0)
            {
                TrySetGrailFloatingTextBuiltInEventClaim(
                    GrailFloatingTextDefaultHealingEventId,
                    false);
            }
        }

        private void ReleaseGrailFloatingTextDefaultHealingClaim()
        {
            if (_grailFloatingTextHealingClaimDepth <= 0)
            {
                return;
            }

            _grailFloatingTextHealingClaimDepth = 0;
            TrySetGrailFloatingTextBuiltInEventClaim(
                GrailFloatingTextDefaultHealingEventId,
                false);
        }

        private bool TrySetGrailFloatingTextBuiltInEventClaim(
            string eventId,
            bool active)
        {
            if (!TryResolveGrailFloatingTextBridge()
                || _grailFloatingTextTrySetBuiltInEventClaimMethod == null)
            {
                return false;
            }

            try
            {
                object result = _grailFloatingTextTrySetBuiltInEventClaimMethod.Invoke(
                    null,
                    new object[] { PluginGuid, eventId, active });
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                LogGrailFloatingTextUnavailableOnce(
                    "Grail Floating Text failed to update the held-drain healing claim: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool BeginGrailFloatingTextBloodHealingPresentationClaim()
        {
            if (_grailFloatingTextHealingPresentationClaimDepth > 0)
            {
                _grailFloatingTextHealingPresentationClaimDepth++;
                return true;
            }

            if (!TrySetGrailFloatingTextBuiltInEventPresentationClaim(true))
            {
                return false;
            }

            _grailFloatingTextHealingPresentationClaimDepth = 1;
            return true;
        }

        private void EndGrailFloatingTextBloodHealingPresentationClaim()
        {
            if (_grailFloatingTextHealingPresentationClaimDepth <= 0)
            {
                return;
            }

            _grailFloatingTextHealingPresentationClaimDepth--;
            if (_grailFloatingTextHealingPresentationClaimDepth == 0)
            {
                TrySetGrailFloatingTextBuiltInEventPresentationClaim(false);
            }
        }

        private void ReleaseGrailFloatingTextBloodHealingPresentationClaim()
        {
            if (_grailFloatingTextHealingPresentationClaimDepth <= 0)
            {
                return;
            }

            _grailFloatingTextHealingPresentationClaimDepth = 0;
            TrySetGrailFloatingTextBuiltInEventPresentationClaim(false);
        }

        private bool TrySetGrailFloatingTextBuiltInEventPresentationClaim(bool active)
        {
            if (!TryResolveGrailFloatingTextBridge()
                || _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod == null)
            {
                return false;
            }

            try
            {
                object result =
                    _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod.Invoke(
                        null,
                        new object[]
                        {
                            PluginGuid,
                            GrailFloatingTextDefaultHealingEventId,
                            GrailFloatingTextBloodHealingEventId,
                            GrailFloatingTextBloodHealingStyle,
                            GrailFloatingTextBloodHealingIconId,
                            active
                        });
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                LogGrailFloatingTextUnavailableOnce(
                    "Grail Floating Text failed to update the Blood Magic healing presentation claim: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private string GetCorpseQualityLabel(float quality)
        {
            string tier = GetCorpseLeechSoundTier(quality);
            if (string.Equals(tier, CorpseLeechMaxTier, StringComparison.OrdinalIgnoreCase))
            {
                return CorpseQualityPrimeLabel;
            }

            if (string.Equals(tier, CorpseLeechHighTier, StringComparison.OrdinalIgnoreCase))
            {
                return CorpseQualityPotentLabel;
            }

            return string.Equals(tier, CorpseLeechMediumTier, StringComparison.OrdinalIgnoreCase)
                ? CorpseQualityWorthyLabel
                : CorpseQualityMeagerLabel;
        }

        private bool TryResolveGrailFloatingTextBridge()
        {
            if (_grailFloatingTextBridgeResolved)
            {
                return _grailFloatingTextTryClaimConsolidatedXpGainMethod != null ||
                    _grailFloatingTextTryClaimXpGainMethod != null ||
                    _grailFloatingTextTrySetBuiltInEventClaimMethod != null ||
                    _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod != null ||
                    _grailFloatingTextTryShowEventMethod != null;
            }

            _grailFloatingTextBridgeResolved = true;

            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(GrailFloatingTextPluginGuid, out pluginInfo) ||
                pluginInfo == null ||
                pluginInfo.Instance == null)
            {
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(GrailFloatingTextApiTypeName, false);
            if (apiType == null)
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text is loaded, but its reflection API could not be found.");
                return false;
            }

            _grailFloatingTextTryClaimConsolidatedXpGainMethod = AccessTools.Method(
                apiType,
                "TryClaimConsolidatedXpGain",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });

            _grailFloatingTextTryClaimXpGainMethod = AccessTools.Method(
                apiType,
                "TryClaimXpGain",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });

            _grailFloatingTextTryCancelXpGainClaimMethod = AccessTools.Method(
                apiType,
                "TryCancelXpGainClaim",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(float)
                });

            _grailFloatingTextTrySetBuiltInEventClaimMethod = AccessTools.Method(
                apiType,
                "TrySetBuiltInEventClaim",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(bool)
                });

            _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod =
                AccessTools.Method(
                    apiType,
                    "TrySetBuiltInEventPresentationClaim",
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(bool)
                    });

            _grailFloatingTextTryShowEventMethod = AccessTools.Method(
                apiType,
                "TryShowEvent",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float)
                });

            if (_grailFloatingTextTryClaimConsolidatedXpGainMethod == null &&
                _grailFloatingTextTryClaimXpGainMethod == null &&
                _grailFloatingTextTrySetBuiltInEventClaimMethod == null &&
                _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod == null &&
                _grailFloatingTextTryShowEventMethod == null)
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text is loaded, but its required claim APIs are unavailable.");
            }

            return _grailFloatingTextTryClaimConsolidatedXpGainMethod != null ||
                _grailFloatingTextTryClaimXpGainMethod != null ||
                _grailFloatingTextTrySetBuiltInEventClaimMethod != null ||
                _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod != null ||
                _grailFloatingTextTryShowEventMethod != null;
        }

        private void LogGrailFloatingTextUnavailableOnce(string message)
        {
            if (_grailFloatingTextUnavailableLogged)
            {
                return;
            }

            _grailFloatingTextUnavailableLogged = true;
            Warn(message);
        }

        private void RejectCorpse(CorpseState state, string reason, bool disable)
        {
            if (state == null)
            {
                return;
            }

            state.LastRejectReason = reason;
            TouchCorpseState(state);
            if (disable)
            {
                state.Disabled = true;
            }

            if (DiagnosticsEnabled() && !state.LoggedReject)
            {
                state.LoggedReject = true;
                Log.LogInfo("Rejected corpse leech target #" + state.DebugId.ToString(CultureInfo.InvariantCulture) + " " + DescribeCorpse(state) + ": " + reason + ".");
                ShowBloodMagicDiagnostic(
                    "blood-magic-corpse-rejected",
                    "Blood Magic: corpse rejected - " + reason + ".");
            }
        }

        private void ShowBloodMagicDiagnostic(
            string eventId,
            string text)
        {
            if (!ShouldShowBloodMagicDiagnostic())
            {
                return;
            }

            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowDiagnosticNotification(
                    PluginGuid,
                    eventId,
                    text,
                    "blood-magic-diagnostics");
        }

        private bool ShouldShowBloodMagicDiagnostic()
        {
            return DiagnosticsEnabled()
                && _showGrailFloatingTextDiagnostics != null
                && _showGrailFloatingTextDiagnostics.Value;
        }

        private string AppendFailure(string failures, string failure)
        {
            if (string.IsNullOrEmpty(failures))
            {
                return failure;
            }

            return failures + "; " + failure;
        }

        private bool TryGetLookedAtCorpseState(out CorpseState state, bool includeInactive = false)
        {
            bool ignoredUnregisteredCorpseCandidate;
            return TryGetLookedAtCorpseState(
                out state,
                out ignoredUnregisteredCorpseCandidate,
                includeInactive);
        }

        private bool TryResolveSoulAndServiceServant(
            object candidate,
            out CorpseState state)
        {
            state = null;
            if (candidate == null)
            {
                return false;
            }
            if (!ResolveSoulAndServiceBridge())
            {
                LogServantTargetDiagnostic(
                    candidate,
                    "Soul and Service bridge unavailable");
                return false;
            }
            try
            {
                object sourceLocation = null;
                object sourceCorpse = null;
                object servantNpc = null;
                object result;
                if (_soulAndServiceResolveServantIdentityMethod != null)
                {
                    object[] identityArgs = { candidate, null, null, null };
                    result = _soulAndServiceResolveServantIdentityMethod.Invoke(
                        null,
                        identityArgs);
                    sourceLocation = identityArgs[1];
                    sourceCorpse = identityArgs[2];
                    servantNpc = identityArgs[3];
                }
                else
                {
                    object[] legacyArgs = { candidate, null, null };
                    result = _soulAndServiceResolveServantMethod.Invoke(
                        null,
                        legacyArgs);
                    sourceCorpse = legacyArgs[1];
                    servantNpc = legacyArgs[2];
                }
                if (!(result is bool) || !(bool)result)
                {
                    LogServantTargetDiagnostic(
                        candidate,
                        "living target is not an owned Soul and Service servant");
                    return false;
                }
                if (servantNpc == null)
                {
                    LogServantTargetDiagnostic(
                        candidate,
                        "owned servant has no resolvable NPC identity");
                    return false;
                }
                bool hasSourceCorpse = sourceLocation != null
                    || sourceCorpse != null;
                if (hasSourceCorpse)
                {
                    if (!TryResolveSoulAndServiceSourceState(
                            sourceLocation,
                            sourceCorpse,
                            servantNpc,
                            out state))
                    {
                        LogServantTargetDiagnostic(
                            candidate,
                            "source corpse could not be registered with Blood Magic");
                        return false;
                    }
                }
                else if (!TryGetCorpseState(servantNpc, out state))
                {
                    state = CreateCorpseState();
                    UpdateCorpseStateFromSource(state, servantNpc, null);
                    state.Disabled = false;
                    state.LastRejectReason = string.Empty;
                    RegisterCorpseAliases(servantNpc, state);
                    RegisterCorpseAliases(candidate, state);
                }
                state.LiveServantTarget = servantNpc;
                state.LiveServantHasSourceCorpse = hasSourceCorpse;
                Component component = candidate as Component;
                if (component != null)
                {
                    UpdateCorpsePositionFromTransform(state, component.transform);
                }
                string rejection;
                string status = state.Exhausted
                    ? hasSourceCorpse
                        ? "recognized; source corpse already drained"
                        : "recognized; servant already drained"
                    : state.Disabled
                        ? "recognized; source corpse disabled"
                        : !IsBloodPlausible(state, out rejection)
                            ? "recognized; servant is bloodless (" + rejection + ")"
                            : IsLiveServantRitualBlocked(state)
                                ? "recognized; ritual blocked while hero is in combat"
                                : hasSourceCorpse
                                    ? "recognized; source blood available"
                                    : "recognized; owned servant blood available; healing only";
                LogServantTargetDiagnostic(candidate, status);
                return true;
            }
            catch (Exception exception)
            {
                Warn(
                    "Soul and Service servant ritual lookup failed: "
                    + exception.GetBaseException().Message);
                _soulAndServiceResolveServantMethod = null;
                _soulAndServiceResolveServantIdentityMethod = null;
                return false;
            }
        }

        private bool TryResolveSoulAndServiceSourceState(
            object sourceLocation,
            object sourceCorpse,
            object servantNpc,
            out CorpseState state)
        {
            if ((sourceLocation != null
                    && TryResolveCorpseStateFromObject(
                        sourceLocation,
                        0,
                        out state,
                        includeInactive: true))
                || (sourceCorpse != null
                    && TryResolveCorpseStateFromObject(
                        sourceCorpse,
                        0,
                        out state,
                        includeInactive: true)))
            {
                RegisterCorpseAliases(sourceLocation, state);
                RegisterCorpseAliases(sourceCorpse, state);
                return state != null;
            }

            Location location = sourceLocation as Location;
            Corpse corpse = sourceCorpse as Corpse;
            if (corpse == null && location != null)
            {
                corpse = location.TryGetElement<Corpse>();
            }
            if (corpse != null)
            {
                HandleCorpseConstructed(
                    corpse,
                    new[] { sourceLocation, servantNpc });
                if (TryGetCorpseState(corpse, out state))
                {
                    RegisterCorpseAliases(sourceLocation, state);
                    RegisterCorpseAliases(sourceCorpse, state);
                    return true;
                }
            }

            state = CreateCorpseState();
            UpdateCorpseStateFromSource(state, sourceLocation, null);
            UpdateCorpseStateFromSource(state, sourceCorpse, null);
            UpdateCorpseStateFromSource(state, servantNpc, null);
            state.Disabled = false;
            state.LastRejectReason = string.Empty;
            RegisterCorpseAliases(sourceLocation, state);
            RegisterCorpseAliases(sourceCorpse, state);
            RegisterCorpseAliases(servantNpc, state);
            return true;
        }

        private void LogServantTargetDiagnostic(
            object candidate,
            string status)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }
            float now = Now;
            if (ReferenceEquals(candidate, _lastServantTargetDiagnosticCandidate)
                && string.Equals(
                    status,
                    _lastServantTargetDiagnosticStatus,
                    StringComparison.Ordinal)
                && now < _nextServantTargetDiagnosticTime)
            {
                return;
            }
            _lastServantTargetDiagnosticCandidate = candidate;
            _lastServantTargetDiagnosticStatus = status;
            _nextServantTargetDiagnosticTime = now + 1.0f;
            Log.LogInfo(
                "Raised-servant blood target: "
                + status
                + "; candidate="
                + DescribeType(candidate)
                + ".");
        }

        private bool TryExsanguinateSoulAndServiceServant(
            object candidate,
            float severity,
            out bool killed)
        {
            killed = false;
            if (candidate == null || !ResolveSoulAndServiceBridge())
            {
                return false;
            }
            try
            {
                object[] args = { candidate, severity, false };
                object result = _soulAndServiceExsanguinateServantMethod.Invoke(
                    null,
                    args);
                killed = args[2] is bool && (bool)args[2];
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                Warn(
                    "Soul and Service servant exsanguination failed: "
                    + exception.GetBaseException().Message);
                _soulAndServiceExsanguinateServantMethod = null;
                return false;
            }
        }

        private void SetSoulAndServiceServantRitualState(
            object candidate,
            bool channeling,
            bool completed)
        {
            if (candidate == null || !ResolveSoulAndServiceBridge())
            {
                return;
            }
            try
            {
                _soulAndServiceSetRitualStateMethod.Invoke(
                    null,
                    new object[] { candidate, channeling, completed });
            }
            catch (Exception exception)
            {
                Warn(
                    "Soul and Service servant ritual hold failed: "
                    + exception.GetBaseException().Message);
                _soulAndServiceSetRitualStateMethod = null;
            }
        }

        private bool TryMaterializeSoulAndServiceServantForAbhartach(
            object candidate,
            out object corpseLocation)
        {
            corpseLocation = null;
            if (candidate == null || !ResolveSoulAndServiceBridge()
                || _soulAndServiceMaterializeAbhartachCorpseMethod == null)
            {
                return false;
            }
            try
            {
                object[] args = { candidate, null };
                object result = _soulAndServiceMaterializeAbhartachCorpseMethod.Invoke(
                    null,
                    args);
                corpseLocation = args[1];
                return result is bool && (bool)result && corpseLocation != null;
            }
            catch (Exception exception)
            {
                Warn(
                    "Soul and Service Abhartach sacrifice failed: "
                    + exception.GetBaseException().Message);
                _soulAndServiceMaterializeAbhartachCorpseMethod = null;
                return false;
            }
        }

        private bool ResolveSoulAndServiceBridge()
        {
            if ((_soulAndServiceResolveServantIdentityMethod != null
                    || _soulAndServiceResolveServantMethod != null)
                && _soulAndServiceExsanguinateServantMethod != null
                && _soulAndServiceSetRitualStateMethod != null)
            {
                return true;
            }
            if (_soulAndServiceApiUnavailable)
            {
                return false;
            }
            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    SoulAndServicePluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return false;
            }
            Type api = pluginInfo.Instance.GetType().Assembly.GetType(
                SoulAndServiceApiTypeName,
                false);
            if (api == null)
            {
                _soulAndServiceApiUnavailable = true;
                return false;
            }
            _soulAndServiceResolveServantIdentityMethod = api.GetMethod(
                "TryResolveOwnedBloodServantIdentity",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(object),
                    typeof(object).MakeByRefType(),
                    typeof(object).MakeByRefType(),
                    typeof(object).MakeByRefType()
                },
                null);
            _soulAndServiceResolveServantMethod = api.GetMethod(
                "TryResolveOwnedBloodServant",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(object),
                    typeof(object).MakeByRefType(),
                    typeof(object).MakeByRefType()
                },
                null);
            _soulAndServiceExsanguinateServantMethod = api.GetMethod(
                "TryExsanguinateOwnedBloodServant",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(object),
                    typeof(float),
                    typeof(bool).MakeByRefType()
                },
                null);
            _soulAndServiceSetRitualStateMethod = api.GetMethod(
                "SetOwnedBloodServantRitualState",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(object), typeof(bool), typeof(bool) },
                null);
            _soulAndServiceMaterializeAbhartachCorpseMethod = api.GetMethod(
                "TryMaterializeOwnedBloodServantCorpseForAbhartach",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(object), typeof(object).MakeByRefType() },
                null);
            if ((_soulAndServiceResolveServantIdentityMethod == null
                    && _soulAndServiceResolveServantMethod == null)
                || _soulAndServiceExsanguinateServantMethod == null
                || _soulAndServiceSetRitualStateMethod == null)
            {
                _soulAndServiceApiUnavailable = true;
                return false;
            }
            return true;
        }

        private void TrySacrificeFocusedServantForAbhartach()
        {
            CorpseState state;
            if (!TryGetLookedAtCorpseState(out state, true)
                || state == null
                || state.LiveServantTarget == null
                || !IsCorpseBloodMagicEligibleForInterop(state))
            {
                return;
            }

            object servant = state.LiveServantTarget;
            object corpseLocation;
            if (!TryMaterializeSoulAndServiceServantForAbhartach(
                    servant,
                    out corpseLocation))
            {
                return;
            }

            state.Disabled = true;
            state.LiveServantTarget = null;
            state.ChannelStartTime = 0.0f;
            state.LastRejectReason = "servant was sacrificed to Abhartach's Calling";
            RegisterCorpseAliases(corpseLocation, state);
            TouchCorpseState(state);
            if (ReferenceEquals(_focusedCorpse, state))
            {
                ResetFocusedCorpse();
            }
            if (DiagnosticsEnabled())
            {
                Log.LogInfo(
                    "Abhartach's Calling sacrificed an owned servant through its native corpse effect.");
            }
        }

        private bool TryGetLookedAtCorpseState(
            out CorpseState state,
            out bool unregisteredCorpseCandidate,
            bool includeInactive = false)
        {
            state = null;
            unregisteredCorpseCandidate = false;
            Vector3 rayPosition;
            Vector3 rayForward;
            if (!TryGetCorpseLookRay(out rayPosition, out rayForward))
            {
                return false;
            }

            RaycastHit hit;
            float range = Math.Max(0.1f, _range.Value);
            int layerMask = _raycastLayerMask.Value;
            if (!Physics.Raycast(rayPosition, rayForward, out hit, range, layerMask, QueryTriggerInteraction.Collide))
            {
                return TryResolveTolerantServantTarget(
                        rayPosition,
                        rayForward,
                        range,
                        layerMask,
                        out state)
                    || TryRetainRecentServantTarget(
                        rayPosition,
                        range,
                        out state);
            }

            if (TryResolveSoulAndServiceServant(hit.collider, out state))
            {
                RememberServantTarget(state);
                return true;
            }

            if (ColliderLooksAlive(hit.collider))
            {
                ClearRecentServantTarget();
                LogUnresolvedRaycastHit(hit.collider);
                return false;
            }

            if (TryResolveCorpseStateFromCollider(hit.collider, out state, includeInactive))
            {
                ClearRecentServantTarget();
                state.LiveServantTarget = null;
                return true;
            }

            if (!IsCorpseFallbackCandidateCollider(hit.collider))
            {
                if (TryResolveTolerantServantTarget(
                        rayPosition,
                        rayForward,
                        hit.distance,
                        layerMask,
                        out state)
                    || TryRetainRecentServantTarget(
                        rayPosition,
                        hit.distance,
                        out state))
                {
                    return true;
                }
                if (IsHarmlessCorpseAssistSurface(hit.collider)
                    && TryResolveNearMissCorpseTarget(
                        hit.point,
                        hit.normal,
                        layerMask,
                        out state,
                        includeInactive))
                {
                    ClearRecentServantTarget();
                    state.LiveServantTarget = null;
                    return true;
                }
                LogUnresolvedRaycastHit(hit.collider);
                return false;
            }

            ClearRecentServantTarget();

            if (TryResolveCorpseStateFromAllRaycastHits(rayPosition, rayForward, range, layerMask, out state, includeInactive))
            {
                return true;
            }

            if (TryRefreshCorpseAliasesAfterUnresolvedHit(hit.collider, includeInactive))
            {
                if (TryResolveCorpseStateFromCollider(hit.collider, out state, includeInactive) ||
                    TryResolveCorpseStateFromAllRaycastHits(rayPosition, rayForward, range, layerMask, out state, includeInactive))
                {
                    return true;
                }
            }

            LogUnresolvedRaycastHit(hit.collider);
            unregisteredCorpseCandidate = true;
            return false;
        }

        private bool TryResolveTolerantServantTarget(
            Vector3 rayPosition,
            Vector3 rayForward,
            float range,
            int layerMask,
            out CorpseState state)
        {
            state = null;
            if (range <= 0.0f || !ResolveSoulAndServiceBridge())
            {
                return false;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                rayPosition,
                ServantTargetToleranceRadius,
                rayForward,
                _servantTargetHits,
                range,
                layerMask,
                QueryTriggerInteraction.Collide);
            float nearestServantDistance = float.MaxValue;
            float nearestBlockingDistance = float.MaxValue;
            CorpseState nearestServant = null;
            int count = Math.Min(hitCount, _servantTargetHits.Length);
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidateHit = _servantTargetHits[i];
                Collider collider = candidateHit.collider;
                if (collider == null)
                {
                    continue;
                }

                if (ColliderLooksAlive(collider))
                {
                    CorpseState candidateState;
                    if (TryResolveSoulAndServiceServant(
                            collider,
                            out candidateState)
                        && candidateHit.distance < nearestServantDistance)
                    {
                        nearestServant = candidateState;
                        nearestServantDistance = candidateHit.distance;
                    }
                    else if (!collider.isTrigger)
                    {
                        nearestBlockingDistance = Math.Min(
                            nearestBlockingDistance,
                            candidateHit.distance);
                    }
                }
                else if (!collider.isTrigger)
                {
                    nearestBlockingDistance = Math.Min(
                        nearestBlockingDistance,
                        candidateHit.distance);
                }
            }

            if (nearestServant == null
                || nearestServantDistance > nearestBlockingDistance + 0.01f)
            {
                return false;
            }

            state = nearestServant;
            RememberServantTarget(state);
            return true;
        }

        private bool IsHarmlessCorpseAssistSurface(Collider collider)
        {
            return collider != null
                && !IsCorpseFallbackCandidateCollider(collider)
                && !IsKnownCorpseCollider(collider, includeInactive: true);
        }

        private bool TryResolveNearMissCorpseTarget(
            Vector3 impactPoint,
            Vector3 surfaceNormal,
            int layerMask,
            out CorpseState state,
            bool includeInactive = false)
        {
            state = null;
            int hitCount = Physics.OverlapSphereNonAlloc(
                impactPoint,
                CorpseTargetAssistRadius,
                _corpseTargetAssistColliders,
                layerMask,
                QueryTriggerInteraction.Ignore);
            int count = Math.Min(hitCount, _corpseTargetAssistColliders.Length);
            float nearestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < _corpseTargetAssistColliders.Length; i++)
            {
                Collider collider = _corpseTargetAssistColliders[i];
                _corpseTargetAssistColliders[i] = null;
                if (i >= count || collider == null || ColliderLooksAlive(collider))
                {
                    continue;
                }

                CorpseState candidate;
                if (!TryResolveCorpseStateFromCollider(
                        collider,
                        out candidate,
                        includeInactive,
                        allowNearestStateFallback: false)
                    || candidate.LiveServantTarget != null)
                {
                    continue;
                }

                Vector3 closestPoint = collider.ClosestPoint(impactPoint);
                Vector3 offset = closestPoint - impactPoint;
                if (Vector3.Dot(offset, surfaceNormal) < -0.10f)
                {
                    continue;
                }

                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestDistanceSqr = distanceSqr;
                state = candidate;
            }

            return state != null;
        }

        private bool TryRetainRecentServantTarget(
            Vector3 rayPosition,
            float unobstructedRange,
            out CorpseState state)
        {
            state = null;
            CorpseState recent = _recentServantTarget;
            if (recent == null
                || Now - _recentServantTargetTime > ServantTargetGraceSeconds
                || recent.LiveServantTarget == null)
            {
                ClearRecentServantTarget();
                return false;
            }

            CorpseState refreshed;
            if (!TryResolveSoulAndServiceServant(
                    recent.LiveServantTarget,
                    out refreshed)
                || !ReferenceEquals(recent, refreshed))
            {
                ClearRecentServantTarget();
                return false;
            }

            Vector3 servantPosition;
            if (!TryGetPosition(refreshed.LiveServantTarget, out servantPosition))
            {
                if (!refreshed.HasPosition)
                {
                    ClearRecentServantTarget();
                    return false;
                }
                servantPosition = refreshed.LastKnownPosition;
            }
            if (Vector3.Distance(rayPosition, servantPosition)
                > unobstructedRange + 0.5f)
            {
                ClearRecentServantTarget();
                return false;
            }

            state = refreshed;
            return true;
        }

        private void RememberServantTarget(CorpseState state)
        {
            if (state == null || state.LiveServantTarget == null)
            {
                return;
            }

            _recentServantTarget = state;
            _recentServantTargetTime = Now;
        }

        private void ClearRecentServantTarget()
        {
            _recentServantTarget = null;
            _recentServantTargetTime = 0.0f;
        }

        private bool TryGetCorpseLookRay(out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.zero;

            if (_heroRaycaster != null)
            {
                try
                {
                    _heroRaycaster.GetViewRay(out position, out forward);
                    if (forward.sqrMagnitude > 0.0001f)
                    {
                        forward.Normalize();
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    if (!_heroViewRayFailureLogged)
                    {
                        _heroViewRayFailureLogged = true;
                        Warn(
                            "Could not read the hero's perspective-aware view ray; falling back to Camera.main: "
                            + exception.GetBaseException().Message);
                    }
                }
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            position = camera.transform.position;
            forward = camera.transform.forward;
            return forward.sqrMagnitude > 0.0001f;
        }

        private bool TryResolveCorpseStateFromCollider(
            Collider collider,
            out CorpseState state,
            bool includeInactive = false,
            bool allowNearestStateFallback = true)
        {
            state = null;
            if (collider == null)
            {
                return false;
            }

            if (ColliderLooksAlive(collider))
            {
                return false;
            }

            if (_corpseRaycastCache.TryGetValue(collider, out state))
            {
                if (ShouldResolveCorpseState(state, includeInactive))
                {
                    return true;
                }

                _corpseRaycastCache.Remove(collider);
                state = null;
            }

            if (TryResolveCorpseStateFromObject(collider, 0, out state, includeInactive))
            {
                CacheResolvedCollider(collider, state);
                return true;
            }

            Transform transform = collider.transform;
            int depth = 0;
            int maxDepth = Math.Max(1, _raycastParentSearchDepth.Value);
            while (transform != null && depth < maxDepth)
            {
                if (TryResolveCorpseStateFromObject(transform.gameObject, 0, out state, includeInactive) ||
                    TryResolveCorpseStateFromObject(transform, 0, out state, includeInactive))
                {
                    CacheResolvedCollider(collider, state);
                    return true;
                }

                transform = transform.parent;
                depth++;
            }

            if (allowNearestStateFallback
                && IsCorpseFallbackCandidateCollider(collider)
                && TryResolveNearestCorpseState(
                    collider.transform.position,
                    out state,
                    includeInactive))
            {
                CacheResolvedCollider(collider, state);
                return true;
            }

            return false;
        }

        private bool TryResolveCorpseStateFromAllRaycastHits(
            Vector3 rayPosition,
            Vector3 rayForward,
            float range,
            int layerMask,
            out CorpseState state,
            bool includeInactive = false)
        {
            state = null;
            int maxHits = _raycastAllFallbackMaxHits == null ? 0 : Math.Max(0, _raycastAllFallbackMaxHits.Value);
            if (maxHits <= 0)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(rayPosition, rayForward, range, layerMask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, delegate(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            });

            int count = Math.Min(maxHits, hits.Length);
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider != null
                    && !ColliderLooksAlive(collider)
                    && (IsCorpseFallbackCandidateCollider(collider) || IsKnownCorpseCollider(collider, includeInactive))
                    && TryResolveCorpseStateFromCollider(collider, out state, includeInactive))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryRefreshCorpseAliasesAfterUnresolvedHit(Collider collider, bool includeInactive = false)
        {
            if (collider == null || !IsCorpseFallbackCandidateCollider(collider))
            {
                return false;
            }

            float interval = _unresolvedCorpseRefreshIntervalSeconds == null ? 1.5f : Math.Max(0.1f, _unresolvedCorpseRefreshIntervalSeconds.Value);
            float now = Now;
            if (now < _nextUnresolvedCorpseRefreshTime)
            {
                return false;
            }

            _nextUnresolvedCorpseRefreshTime = now + interval;
            int refreshed = 0;
            for (int i = 0; i < _allCorpseStates.Count; i++)
            {
                CorpseState candidate = _allCorpseStates[i];
                if (!ShouldResolveCorpseState(candidate, includeInactive))
                {
                    continue;
                }

                RegisterCorpseAliases(candidate.Corpse, candidate);
                RegisterCorpseAliases(candidate.TargetObject, candidate);
                refreshed++;
            }

            return refreshed > 0;
        }

        private void CacheResolvedCollider(Collider collider, CorpseState state)
        {
            if (collider == null || !IsCorpseStateUsable(state))
            {
                return;
            }

            _corpseRaycastCache[collider] = state;
            RegisterCorpseAlias(collider, state);
            RegisterTransformHierarchyAliases(collider.transform, state);
        }

        private bool IsCorpseFallbackCandidateCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            string text = collider.GetType().Name + " " +
                SafeUnityObjectName(collider) + " " +
                SafeUnityObjectName(collider.gameObject) + " " +
                BuildTransformPath(collider.transform);

            string matched;
            string[] terms = { "Corpse", "Ragdoll", "Dead", "Death", "Dying" };
            return ContainsAnyTerm(text, terms, out matched);
        }

        private bool IsKnownCorpseCollider(Collider collider, bool includeInactive = false)
        {
            if (collider == null)
            {
                return false;
            }

            CorpseState state;
            return _corpseRaycastCache.TryGetValue(collider, out state)
                && ShouldResolveCorpseState(state, includeInactive);
        }

        private bool ColliderLooksAlive(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            _aliveStateProbeSeen.Clear();
            bool alive;
            if (TryReadAliveState(collider, 0, _aliveStateProbeSeen, out alive))
            {
                return alive;
            }

            Transform transform = collider.transform;
            int depth = 0;
            int maxDepth = Math.Max(1, _raycastParentSearchDepth.Value);
            while (transform != null && depth < maxDepth)
            {
                if (TryReadAliveState(transform.gameObject, 0, _aliveStateProbeSeen, out alive) ||
                    TryReadAliveState(transform, 0, _aliveStateProbeSeen, out alive))
                {
                    return alive;
                }

                transform = transform.parent;
                depth++;
            }

            return false;
        }

        private bool TryReadAliveState(object obj, int depth, HashSet<object> seen, out bool alive)
        {
            alive = false;
            if (obj == null || depth > 3 || seen.Contains(obj))
            {
                return false;
            }

            seen.Add(obj);

            if (TryReadDeadBool(obj, "IsDead", out alive) ||
                TryReadDeadBool(obj, "Dead", out alive) ||
                TryReadDeadBool(obj, "HasDied", out alive) ||
                TryReadDeadBool(obj, "IsDying", out alive))
            {
                return true;
            }

            bool found;
            bool isAlive = GetBoolProperty(obj, "IsAlive", false, out found);
            if (found)
            {
                alive = isAlive;
                return true;
            }

            isAlive = GetBoolProperty(obj, "Alive", false, out found);
            if (found)
            {
                alive = isAlive;
                return true;
            }

            float currentHealth;
            if (TryReadCurrentHealth(obj, out currentHealth))
            {
                alive = currentHealth > 0.01f;
                return true;
            }

            GameObject gameObject = obj as GameObject;
            if (gameObject != null && depth < 2)
            {
                MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null &&
                        TryReadAliveState(components[i], depth + 1, seen, out alive))
                    {
                        return true;
                    }
                }
            }

            string[] relatedProperties = { "HealthElement", "NpcElement", "Character", "Target", "Model", "ParentModel", "GenericParentModel", "Element" };
            for (int i = 0; i < relatedProperties.Length; i++)
            {
                object related = GetOptionalPropertyValue(obj, relatedProperties[i]);
                if (related != null &&
                    !ReferenceEquals(related, obj) &&
                    TryReadAliveState(related, depth + 1, seen, out alive))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryReadDeadBool(object obj, string propertyName, out bool alive)
        {
            alive = false;
            bool found;
            bool dead = GetBoolProperty(obj, propertyName, false, out found);
            if (!found || !dead)
            {
                return false;
            }

            alive = false;
            return true;
        }

        private bool TryReadCurrentHealth(object obj, out float currentHealth)
        {
            currentHealth = 0f;
            if (obj == null)
            {
                return false;
            }

            string[] properties = { "Health", "CurrentHealth", "CurrentHP", "CurrentHp", "HP", "Hp" };
            for (int i = 0; i < properties.Length; i++)
            {
                object value = GetOptionalPropertyValue(obj, properties[i]);
                if (value == null)
                {
                    continue;
                }

                float direct = ToFloat(value, float.NaN);
                if (!float.IsNaN(direct) && direct >= 0f)
                {
                    currentHealth = direct;
                    return true;
                }

                float stat = ReadStatValue(value);
                if (stat >= 0f)
                {
                    currentHealth = stat;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveNearestCorpseState(Vector3 hitPosition, out CorpseState state, bool includeInactive = false)
        {
            state = null;
            float radius = _nearestCorpseFallbackRadius.Value;
            if (radius <= 0f)
            {
                return false;
            }

            float maxSqrDistance = radius * radius;
            float bestSqrDistance = maxSqrDistance;

            for (int i = 0; i < _allCorpseStates.Count; i++)
            {
                CorpseState candidate = _allCorpseStates[i];
                if (!ShouldResolveCorpseState(candidate, includeInactive) || !candidate.HasPosition)
                {
                    continue;
                }

                float sqrDistance = (candidate.LastKnownPosition - hitPosition).sqrMagnitude;
                if (sqrDistance <= bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    state = candidate;
                }
            }

            return state != null;
        }

        private void LogUnresolvedRaycastHit(Collider collider)
        {
            if (!DiagnosticsEnabled() || collider == null)
            {
                return;
            }

            if (!_loggedUnresolvedRaycastHits.Add(collider))
            {
                return;
            }

            string colliderName = SafeUnityObjectName(collider);
            GameObject gameObject = collider.gameObject;
            string gameObjectName = SafeUnityObjectName(gameObject);
            int layer = gameObject == null ? -1 : gameObject.layer;
            Log.LogInfo("Corpse leech raycast hit unresolved collider: collider=" + collider.GetType().Name + ":" + colliderName + "; gameObject=" + gameObjectName + "; layer=" + layer.ToString(CultureInfo.InvariantCulture) + "; path=" + BuildTransformPath(collider.transform) + ".");
        }

        private string SafeUnityObjectName(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return "";
            }

            try
            {
                return unityObject.name ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string BuildTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "";
            }

            List<string> names = new List<string>();
            Transform current = transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                names.Insert(0, SafeUnityObjectName(current));
                current = current.parent;
                depth++;
            }

            return string.Join("/", names.ToArray());
        }

        private bool TryResolveCorpseStateFromObject(object obj, int depth, out CorpseState state, bool includeInactive = false)
        {
            state = null;
            if (obj == null || depth > 3)
            {
                return false;
            }

            CorpseState knownState;
            bool hasKnownState = TryGetCorpseState(obj, out knownState);
            if (hasKnownState && ShouldResolveCorpseState(knownState, includeInactive))
            {
                state = knownState;
                return true;
            }
            state = null;

            Type type = obj.GetType();
            if (!hasKnownState && type.FullName == CorpseTypeName)
            {
                CorpseState corpseState = CreateCorpseState();
                corpseState.Corpse = obj;
                UpdateCorpseStateFromSource(corpseState, obj, null);
                RegisterCorpseAliases(obj, corpseState);
                state = corpseState;
                return ShouldResolveCorpseState(state, includeInactive);
            }

            GameObject gameObject = obj as GameObject;
            if (gameObject != null)
            {
                MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null && TryResolveCorpseStateFromObject(components[i], depth + 1, out state, includeInactive))
                    {
                        return true;
                    }
                }
            }

            string[] relatedProperties = { "NpcElement", "Character", "CharacterView", "Target", "Model", "ParentModel", "GenericParentModel", "Element", "HealthElement" };
            for (int i = 0; i < relatedProperties.Length; i++)
            {
                object related = GetOptionalPropertyValue(obj, relatedProperties[i]);
                if (related != null && !ReferenceEquals(related, obj) && TryResolveCorpseStateFromObject(related, depth + 1, out state, includeInactive))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCorpseStateUsable(CorpseState state)
        {
            return state != null && !state.Disabled && !state.Exhausted;
        }

        private bool ShouldResolveCorpseState(CorpseState state, bool includeInactive)
        {
            return includeInactive ? state != null : IsCorpseStateUsable(state);
        }

        private void UpdateCorpseStateFromSource(CorpseState state, object target, object healthElement)
        {
            if (state == null)
            {
                return;
            }

            TouchCorpseState(state);

            object source = target ?? healthElement;
            if (source != null && state.TargetObject == null)
            {
                state.TargetObject = source;
            }

            float killXp = TryReadExpReward(target);
            if (killXp <= 0f)
            {
                killXp = TryReadExpReward(healthElement);
            }
            if (killXp > state.TargetKillXp)
            {
                state.TargetKillXp = killXp;
            }
            StoreCorpseEffectiveKillXp(state, target);
            StoreCorpseEffectiveKillXp(state, healthElement);

            float targetMaxHealth = TryGetMaxHealth(target);
            if (targetMaxHealth <= 0f)
            {
                targetMaxHealth = TryGetMaxHealth(healthElement);
            }
            if (targetMaxHealth > state.TargetMaxHealth)
            {
                state.TargetMaxHealth = targetMaxHealth;
            }

            int nativeTier;
            if (TryReadNativeTier(target, out nativeTier)
                || TryReadNativeTier(healthElement, out nativeTier))
            {
                state.NativeTier = nativeTier;
                state.HasNativeTier = true;
            }

            Grailwright.Shared.CorpseQualityThreatClass threatClass;
            if (TryReadCorpseThreatClass(target, out threatClass)
                || TryReadCorpseThreatClass(healthElement, out threatClass))
            {
                state.TargetThreatClass = threatClass;
                state.HasTargetThreatClass = true;
            }

            float expLevel = TryReadExpLevel(target);
            if (expLevel < 0f)
            {
                expLevel = TryReadExpLevel(healthElement);
            }
            if (expLevel >= 0f)
            {
                state.TargetExpLevel = expLevel;
                state.HasTargetExpLevel = true;
            }

            bool xpRewardAllowedFound;
            bool xpRewardAllowed = GetBoolProperty(target, "XpRewardAllowed", true, out xpRewardAllowedFound);
            if (!xpRewardAllowedFound)
            {
                xpRewardAllowed = GetBoolProperty(healthElement, "XpRewardAllowed", true, out xpRewardAllowedFound);
            }
            if (_requireTargetXpRewardAllowedWhenPresent.Value && xpRewardAllowedFound && !xpRewardAllowed)
            {
                state.Disabled = true;
                state.LastRejectReason = "XpRewardAllowed was false";
            }

            AddObjectSearchText(state, target);
            AddObjectSearchText(state, healthElement);
            UpdateCorpsePositionFromSource(state, target);
            UpdateCorpsePositionFromSource(state, healthElement);

            string displayName = GetDisplayName(target);
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = GetDisplayName(healthElement);
            }
            if (!string.IsNullOrEmpty(displayName))
            {
                state.DisplayName = displayName;
            }
            else if (string.IsNullOrEmpty(state.DisplayName) && source != null)
            {
                state.DisplayName = source.GetType().Name;
            }

            RegisterCorpseAliases(target, state);
            RegisterCorpseAliases(healthElement, state);
        }

        private void TouchCorpseState(CorpseState state)
        {
            if (state != null)
            {
                state.LastTouchedTime = Now;
            }
        }

        private CorpseState CreateCorpseState()
        {
            CorpseState state = new CorpseState();
            state.DebugId = _nextCorpseStateId++;
            TouchCorpseState(state);
            _allCorpseStates.Add(state);
            return state;
        }

        private bool TryGetCorpseState(object key, out CorpseState state)
        {
            if (key != null && _corpseStates.TryGetValue(key, out state))
            {
                return state != null;
            }

            state = null;
            return false;
        }

        private void RegisterCorpseAliases(object key, CorpseState state)
        {
            if (key == null || state == null)
            {
                return;
            }

            RegisterCorpseAlias(key, state);
            RegisterCorpseAlias(GetOptionalPropertyValue(key, "HealthElement"), state);
            RegisterCorpseAlias(GetOptionalPropertyValue(key, "NpcElement"), state);
            RegisterCorpseAlias(GetOptionalPropertyValue(key, "Character"), state);
            RegisterCorpseAlias(GetOptionalPropertyValue(key, "CharacterView"), state);
            RegisterTransformAliases(key, state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "ParentTransform"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "ActorTransform"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "SpawnedVisualPrefab"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "Visuals"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "Visual"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "View"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "GameObject"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "Transform"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "Hips"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "Torso"), state);
            RegisterTransformAliases(GetOptionalPropertyValue(key, "Head"), state);
        }

        private void RegisterCorpseAlias(object key, CorpseState state)
        {
            if (key == null || state == null)
            {
                return;
            }

            TouchCorpseState(state);

            CorpseState existing;
            if (!_corpseStates.TryGetValue(key, out existing) || existing == null || existing.Disabled || existing.Exhausted)
            {
                _corpseStates[key] = state;
                if (!ReferenceEquals(existing, state))
                {
                    _corpseRaycastCache.Clear();
                }
            }
        }

        private void RegisterTransformAliases(object value, CorpseState state)
        {
            if (value == null || state == null)
            {
                return;
            }

            Transform transform = value as Transform;
            if (transform != null)
            {
                RegisterTransformHierarchyAliases(transform, state);
                return;
            }

            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                RegisterTransformHierarchyAliases(gameObject.transform, state);
                return;
            }

            Component component = value as Component;
            if (component != null)
            {
                RegisterCorpseAlias(component, state);
                RegisterTransformHierarchyAliases(component.transform, state);
            }
        }

        private void RegisterTransformHierarchyAliases(Transform root, CorpseState state)
        {
            if (root == null || state == null)
            {
                return;
            }

            RegisterTransformOnlyAlias(root, state);

            int maxNodes = _corpseHierarchyAliasMaxNodes == null ? 96 : Math.Max(0, _corpseHierarchyAliasMaxNodes.Value);
            if (maxNodes <= 0)
            {
                return;
            }

            int registered = 0;
            try
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length && registered < maxNodes; i++)
                {
                    RegisterTransformOnlyAlias(transforms[i], state);
                    registered++;
                }
            }
            catch
            {
            }

            registered = 0;
            try
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length && registered < maxNodes; i++)
                {
                    RegisterCorpseAlias(colliders[i], state);
                    RegisterTransformOnlyAlias(colliders[i].transform, state);
                    registered++;
                }
            }
            catch
            {
            }

            registered = 0;
            try
            {
                Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < bodies.Length && registered < maxNodes; i++)
                {
                    RegisterCorpseAlias(bodies[i], state);
                    RegisterTransformOnlyAlias(bodies[i].transform, state);
                    registered++;
                }
            }
            catch
            {
            }
        }

        private void RegisterTransformOnlyAlias(Transform transform, CorpseState state)
        {
            if (transform == null || state == null)
            {
                return;
            }

            RegisterCorpseAlias(transform, state);
            RegisterCorpseAlias(transform.gameObject, state);
            UpdateCorpsePositionFromTransform(state, transform);
        }

        private void UpdateCorpsePositionFromSource(CorpseState state, object source)
        {
            HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            UpdateCorpsePositionFromSource(state, source, 0, seen);
        }

        private void UpdateCorpsePositionFromSource(CorpseState state, object source, int depth, HashSet<object> seen)
        {
            if (state == null || source == null)
            {
                return;
            }
            if (depth > 3 || !seen.Add(source))
            {
                return;
            }

            Vector3 position;
            if (TryGetPosition(source, out position))
            {
                state.LastKnownPosition = position;
                state.HasPosition = true;
            }

            UpdateCorpsePositionFromSource(state, GetOptionalPropertyValue(source, "HealthElement"), depth + 1, seen);
            UpdateCorpsePositionFromSource(state, GetOptionalPropertyValue(source, "ParentModel"), depth + 1, seen);
            UpdateCorpsePositionFromSource(state, GetOptionalPropertyValue(source, "GenericParentModel"), depth + 1, seen);
            UpdateCorpsePositionFromSource(state, GetOptionalPropertyValue(source, "NpcElement"), depth + 1, seen);
        }

        private bool TryGetPosition(object source, out Vector3 position)
        {
            position = Vector3.zero;
            if (source == null)
            {
                return false;
            }

            Transform transform = source as Transform;
            if (transform != null)
            {
                position = transform.position;
                return true;
            }

            GameObject gameObject = source as GameObject;
            if (gameObject != null)
            {
                position = gameObject.transform.position;
                return true;
            }

            Component component = source as Component;
            if (component != null)
            {
                position = component.transform.position;
                return true;
            }

            object coords = GetOptionalPropertyValue(source, "Coords");
            if (coords is Vector3)
            {
                position = (Vector3)coords;
                return true;
            }

            object reflectedPosition = GetOptionalPropertyValue(source, "Position");
            if (reflectedPosition is Vector3)
            {
                position = (Vector3)reflectedPosition;
                return true;
            }

            return false;
        }

        private void UpdateCorpsePositionFromTransform(CorpseState state, Transform transform)
        {
            if (state == null || transform == null)
            {
                return;
            }

            state.LastKnownPosition = transform.position;
            state.HasPosition = true;
        }

        internal void RegisterStrongCastStart(object magicFsm, bool lightCast, bool castWasAccepted)
        {
            if (!_enabled.Value || magicFsm == null)
            {
                return;
            }

            if (IsVersatileWeaponsMagicFsmSuppressed(magicFsm))
            {
                _bloodSpellInnerLightReadyStates.Remove(magicFsm);
                _strongCastStates.Remove(magicFsm);
                return;
            }

            object item = GetPropertyValue(magicFsm, "Item");
            object skill = GetPropertyValue(magicFsm, "Skill");
            string bloodSummary;
            string abhartachSummary;
            bool isBloodMagicSpell = IsBloodTransfusionItemOrSkill(item, skill, out bloodSummary);
            bool isAbhartach = IsAbhartachItemOrSkill(item, skill, out abhartachSummary);
            if (!isBloodMagicSpell && !isAbhartach)
            {
                return;
            }

            string spellSummary = isBloodMagicSpell ? bloodSummary : abhartachSummary;
            if (!castWasAccepted)
            {
                LogBloodSpellInnerLightDiagnosticThrottled(
                    "matched TryEnterMagicCastState but did not boost; lightCast="
                    + lightCast
                    + ", castWasAccepted="
                    + castWasAccepted
                    + ", summary="
                    + spellSummary
                    + ".");
                return;
            }

            RegisterBloodSpellInnerLightCastBoost(
                "TryEnterMagicCastState",
                magicFsm,
                isBloodMagicSpell,
                isAbhartach,
                spellSummary,
                "lightCast=" + lightCast + ", castWasAccepted=" + castWasAccepted,
                BloodSpellInnerLightCastBoostEvent.Started);

            if (isAbhartach)
            {
                RegisterAbhartachHeldHealingActive();
                RecordAbhartachFocusedCorpseQuality();
            }

            if (lightCast || !isBloodMagicSpell)
            {
                return;
            }

            StrongCastState state = GetStrongCastState(magicFsm);
            state.Hand = GetHandKey(magicFsm);
            state.Until = Now + Math.Max(0.05f, _strongHoldGraceSeconds.Value);
        }

        internal void RecordMagicFsmUpdate(object magicFsm)
        {
            if (!_enabled.Value || magicFsm == null)
            {
                return;
            }

            if (IsVersatileWeaponsMagicFsmSuppressed(magicFsm))
            {
                _bloodSpellInnerLightReadyStates.Remove(magicFsm);
                _strongCastStates.Remove(magicFsm);
                return;
            }

            float now = Now;
            float interval = Math.Max(0.01f, _holdTrackerIntervalSeconds.Value);

            StrongCastState state;
            bool hasState = _strongCastStates.TryGetValue(magicFsm, out state);
            if (hasState)
            {
                if (now < state.NextUpdateProbeTime)
                {
                    return;
                }

                state.NextUpdateProbeTime = now + interval;
            }
            else
            {
                if (now < _nextGlobalHoldProbeTime)
                {
                    return;
                }

                _nextGlobalHoldProbeTime = now + interval;
            }

            bool held = GetBoolProperty(magicFsm, "SpellAttackHeld", false);
            bool casting = GetBoolProperty(magicFsm, "IsCasting", false);
            bool charging = GetBoolProperty(magicFsm, "IsChargingMagic", false);
            int chargeSteps = GetIntProperty(magicFsm, "CurrentChargeSteps", 0);
            string layerSummary;
            bool layerReadied = IsBloodSpellInnerLightMagicLayerReadied(magicFsm, out layerSummary);
            bool inputOrCastEvidence = held || casting || charging || chargeSteps > 0;
            bool readyEvidence = layerReadied || inputOrCastEvidence;
            bool recentlyAccepted = hasState && state != null && state.Until >= now;
            bool isBloodMagicSpell;
            bool isAbhartach;
            string spellSummary;
            if (!TryGetBloodSpellInnerLightCandidate(
                magicFsm,
                out isBloodMagicSpell,
                out isAbhartach,
                out spellSummary))
            {
                ClearBloodSpellInnerLightReadyState(
                    magicFsm,
                    "equipped spell is no longer a blood spell",
                    true);
                return;
            }

            if (readyEvidence)
            {
                MarkBloodSpellInnerLightReadied(
                    magicFsm,
                    spellSummary,
                    BloodSpellInnerLightReadyGraceSeconds);
            }

            bool lightReadied = HasBloodSpellInnerLightReadiedState(magicFsm);

            string stateSummary =
                "hasState="
                + hasState
                + ", recentAccepted="
                + recentlyAccepted
                + ", readied="
                + lightReadied
                + ", held="
                + held
                + ", casting="
                + casting
                + ", charging="
                + charging
                + ", chargeSteps="
                + chargeSteps.ToString(CultureInfo.InvariantCulture)
                + ", layerReadied="
                + layerReadied
                + ", "
                + layerSummary;

            bool castBoostFinishedSuppressed = IsBloodSpellInnerLightCastBoostFinishedSuppressed(magicFsm, now);
            if ((casting || charging || chargeSteps > 0) && !castBoostFinishedSuppressed)
            {
                RegisterBloodSpellInnerLightCastBoost(
                    "MagicFSM.OnUpdate",
                    magicFsm,
                    isBloodMagicSpell,
                    isAbhartach,
                    spellSummary,
                    stateSummary,
                    BloodSpellInnerLightCastBoostEvent.Evidence);
            }

            if (!readyEvidence && !recentlyAccepted)
            {
                return;
            }

            if (isAbhartach)
            {
                RegisterAbhartachHeldHealingActive();
                RecordAbhartachFocusedCorpseQuality();
            }

            if (!isBloodMagicSpell)
            {
                return;
            }

            if (!hasState)
            {
                state = GetStrongCastState(magicFsm);
                state.NextUpdateProbeTime = now + interval;
            }

            state.Hand = GetHandKey(magicFsm);
            if (inputOrCastEvidence)
            {
                state.Until = now + Math.Max(0.05f, _strongHoldGraceSeconds.Value);
            }
        }

        internal void RegisterPerformCast(object magicFsm, bool lightCast)
        {
            if (!_enabled.Value || magicFsm == null)
            {
                return;
            }

            if (IsVersatileWeaponsMagicFsmSuppressed(magicFsm))
            {
                _bloodSpellInnerLightReadyStates.Remove(magicFsm);
                _strongCastStates.Remove(magicFsm);
                return;
            }

            object item = GetPropertyValue(magicFsm, "Item");
            object skill = GetPropertyValue(magicFsm, "Skill");
            string bloodSummary;
            string abhartachSummary;
            bool isBloodMagicSpell = IsBloodTransfusionItemOrSkill(item, skill, out bloodSummary);
            bool isAbhartach = IsAbhartachItemOrSkill(item, skill, out abhartachSummary);
            if (!isBloodMagicSpell && !isAbhartach)
            {
                return;
            }

            string spellSummary = isBloodMagicSpell ? bloodSummary : abhartachSummary;
            RegisterBloodSpellInnerLightCastBoost(
                "MagicFSM.OnPerformCast",
                magicFsm,
                isBloodMagicSpell,
                isAbhartach,
                spellSummary,
                "lightCast=" + lightCast,
                BloodSpellInnerLightCastBoostEvent.Finished);

            if (isAbhartach)
            {
                RegisterAbhartachHeldHealingActive();
                RecordAbhartachFocusedCorpseQuality();
                if (lightCast)
                {
                    TrySacrificeFocusedServantForAbhartach();
                }
            }

            if (lightCast || !isBloodMagicSpell)
            {
                return;
            }

            StrongCastState state = GetStrongCastState(magicFsm);
            state.Hand = GetHandKey(magicFsm);
            state.Until = Now + Math.Max(0.05f, _strongHoldGraceSeconds.Value);
        }

        internal void RegisterCastEnding(object magicFsm, string context, bool clearReadied)
        {
            if (!_enabled.Value || magicFsm == null)
            {
                return;
            }

            object item = GetPropertyValue(magicFsm, "Item");
            object skill = GetPropertyValue(magicFsm, "Skill");
            string bloodSummary;
            string abhartachSummary;
            bool isBloodMagicSpell = IsBloodTransfusionItemOrSkill(item, skill, out bloodSummary);
            bool isAbhartach = IsAbhartachItemOrSkill(item, skill, out abhartachSummary);
            if (!isBloodMagicSpell && !isAbhartach)
            {
                return;
            }

            string spellSummary = isBloodMagicSpell ? bloodSummary : abhartachSummary;
            if (clearReadied)
            {
                ClearBloodSpellInnerLightReadyState(
                    magicFsm,
                    context + " ended or canceled casting",
                    true);
                FinishBloodSpellInnerLightCastBoostWindow(
                    magicFsm,
                    GetHandKey(magicFsm),
                    Now);
                return;
            }

            RegisterBloodSpellInnerLightCastBoost(
                context,
                magicFsm,
                isBloodMagicSpell,
                isAbhartach,
                spellSummary,
                "earlyFinish=true",
                BloodSpellInnerLightCastBoostEvent.Finished);
        }

        private void RegisterAbhartachHeldHealingActive()
        {
            if (ShouldTuneAbhartach() && _abhartachScaleHeldCorpseHealing.Value)
            {
                _abhartachHeldHealingActiveUntil = Now + Math.Max(0.15f, _strongHoldGraceSeconds.Value);
                RecordAbhartachFocusedCorpseQuality();
            }
        }

        private StrongCastState GetStrongCastState(object magicFsm)
        {
            StrongCastState state;
            if (!_strongCastStates.TryGetValue(magicFsm, out state))
            {
                state = new StrongCastState();
                _strongCastStates[magicFsm] = state;
            }

            return state;
        }

        private bool MeetsHandRequirement(int activeHandCount, HandRequirement handRequirement)
        {
            return handRequirement == HandRequirement.BothHands ? activeHandCount >= 2 : activeHandCount >= 1;
        }

        private int GetActiveHeldHandCount()
        {
            float now = Now;
            bool hasMain = false;
            bool hasOff = false;
            int unknownCount = 0;
            foreach (StrongCastState state in _strongCastStates.Values)
            {
                if (state.Until < now)
                {
                    continue;
                }

                string hand = state.Hand ?? "";
                if (hand.IndexOf("Both", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasMain = true;
                    hasOff = true;
                }
                else if (hand.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    hand.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasMain = true;
                }
                else if (hand.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    hand.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasOff = true;
                }
                else
                {
                    unknownCount++;
                }
            }

            int knownCount = (hasMain ? 1 : 0) + (hasOff ? 1 : 0);
            return Math.Min(2, knownCount + unknownCount);
        }

        private string GetHandKey(object magicFsm)
        {
            object hand = GetPropertyValue(magicFsm, "CastingHand");
            return hand == null ? "Unknown" : SafeToString(hand);
        }

        private bool TryBuildDeathContext(object healthElement, object outcome, out DeathContext context)
        {
            context = new DeathContext();
            if (healthElement == null && outcome == null)
            {
                return false;
            }

            object target = ResolveHealthElementOwner(healthElement);
            object damage = outcome == null ? null : GetPropertyValue(outcome, "Damage");
            if (target == null && outcome != null)
            {
                target = GetPropertyValue(outcome, "Target");
            }
            if (target == null && outcome != null)
            {
                target = GetPropertyValue(outcome, "TargetPure");
            }
            if (target == null && damage != null)
            {
                target = GetPropertyValue(damage, "Target");
            }
            if (target == null && damage != null)
            {
                target = GetPropertyValue(damage, "TargetPure");
            }
            if (target == null)
            {
                target = GetOptionalPropertyValue(healthElement, "ParentModel");
            }

            context.HealthElement = healthElement;
            context.Target = target;
            return true;
        }

        private object ResolveHealthElementOwner(object healthElement)
        {
            if (healthElement == null)
            {
                return null;
            }

            string[] ownerProperties = { "ParentModel", "GenericParentModel", "NpcElement", "Character", "CharacterView", "Owner", "Parent" };
            for (int i = 0; i < ownerProperties.Length; i++)
            {
                object value = GetOptionalPropertyValue(healthElement, ownerProperties[i]);
                if (value != null && !ReferenceEquals(value, healthElement))
                {
                    return value;
                }
            }

            return null;
        }

        private float TryResolveVanillaEffectiveKillXp(object owner, out bool hasLevelContext)
        {
            hasLevelContext = false;
            if (owner == null)
            {
                return 0f;
            }

            float baseXp = TryReadExpReward(owner);
            if (baseXp <= 0f)
            {
                return 0f;
            }

            float levelMultiplier = ResolveVanillaEnemyLevelMultiplier(owner, out hasLevelContext);
            float killMultiplier = ResolveHeroKillExpMultiplier();
            return Math.Max(0f, baseXp * levelMultiplier * killMultiplier);
        }

        private float ResolveVanillaEnemyLevelMultiplier(object owner, out bool hasLevelContext)
        {
            hasLevelContext = false;

            float enemyExpLevel = TryReadExpLevel(owner);
            if (enemyExpLevel < 0f)
            {
                return 1f;
            }

            float heroLevel = TryReadHeroLevel();
            if (heroLevel < 0f)
            {
                return 1f;
            }

            float levelsBelowHero = heroLevel - enemyExpLevel;
            if (levelsBelowHero <= 0f)
            {
                hasLevelContext = true;
                return 1f;
            }

            float deductedPerLevel;
            float minimumMultiplier;
            if (!TryReadVanillaXpFalloffConstants(out deductedPerLevel, out minimumMultiplier))
            {
                WarnVanillaXpFalloffUnavailable();
                return 1f;
            }

            float multiplier = 1f - levelsBelowHero * deductedPerLevel;
            if (multiplier < minimumMultiplier)
            {
                multiplier = minimumMultiplier;
            }

            hasLevelContext = true;
            return Math.Max(0f, multiplier);
        }

        private float TryReadExpLevel(object owner)
        {
            if (owner == null)
            {
                return -1f;
            }

            object template = GetOptionalPropertyValue(owner, "Template");
            if (template != null && !ReferenceEquals(template, owner))
            {
                float value = GetOptionalFloatProperty(template, "ExpLevel", -1f);
                if (value >= 0f)
                {
                    return value;
                }
            }

            return GetOptionalFloatProperty(owner, "ExpLevel", -1f);
        }

        private float TryReadHeroLevel()
        {
            object hero = GetHero();
            object level = GetOptionalPropertyValue(hero, "Level");
            float value = ReadStatValue(level);
            return value >= 0f ? value : -1f;
        }

        private float ResolveHeroKillExpMultiplier()
        {
            object hero = GetHero();
            object heroMultStats = GetOptionalPropertyValue(hero, "HeroMultStats");
            object killExpMultiplier = GetOptionalPropertyValue(heroMultStats, "KillExpMultiplier");
            float value = ReadStatValue(killExpMultiplier);
            return value >= 0f ? Math.Max(0f, value) : 1f;
        }

        private bool TryReadVanillaXpFalloffConstants(out float deductedPerLevel, out float minimumMultiplier)
        {
            deductedPerLevel = 0f;
            minimumMultiplier = 0f;

            object constants = GetGameConstants();
            if (constants == null)
            {
                return false;
            }

            deductedPerLevel = GetOptionalFloatProperty(constants, "ExpMultiDeductedPerEnemyLevelBelowHero", float.NaN);
            minimumMultiplier = GetOptionalFloatProperty(constants, "MinExpMultiFromEnemyLevelBelowHero", float.NaN);
            return !float.IsNaN(deductedPerLevel) && !float.IsNaN(minimumMultiplier);
        }

        private void WarnVanillaXpFalloffUnavailable()
        {
            if (_loggedVanillaXpFalloffUnavailable)
            {
                return;
            }

            _loggedVanillaXpFalloffUnavailable = true;
            Warn("Could not resolve GameConstants XP falloff; Blood Magic Expansion XP will use raw kill XP until the game's constants are available.");
        }

        private object GetGameConstants()
        {
            if (!_gameConstantsGetterResolved)
            {
                _gameConstantsGetterResolved = true;
                Type gameConstantsType = AccessTools.TypeByName(GameConstantsTypeName);
                if (gameConstantsType != null)
                {
                    _gameConstantsGetter = AccessTools.PropertyGetter(gameConstantsType, "Get");
                }
            }

            if (_gameConstantsGetter == null)
            {
                return null;
            }

            try
            {
                return _gameConstantsGetter.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }

        private float TryReadExpReward(object owner)
        {
            if (owner == null)
            {
                return 0f;
            }

            object template = GetOptionalPropertyValue(owner, "Template");
            if (template != null && !ReferenceEquals(template, owner))
            {
                float value = TryReadExpRewardDirect(template);
                if (value > 0f)
                {
                    return value;
                }
            }

            return TryReadExpRewardDirect(owner);
        }

        private float TryReadExpRewardDirect(object owner)
        {
            if (owner == null)
            {
                return 0f;
            }

            string[] properties = { "ExpReward", "XPReward", "XpReward", "ExperienceReward" };
            for (int i = 0; i < properties.Length; i++)
            {
                float value = GetOptionalFloatProperty(owner, properties[i], 0f);
                if (value > 0f)
                {
                    return value;
                }
            }

            MethodInfo method = GetMethodSilent(owner.GetType(), "GetExpReward", 0);
            if (method == null)
            {
                return 0f;
            }

            try
            {
                return ToFloat(method.Invoke(owner, null), 0f);
            }
            catch
            {
                return 0f;
            }
        }

        private bool AwardRawCharacterXp(float amount)
        {
            if (amount <= 0f)
            {
                return false;
            }

            try
            {
                object hero = GetHero();
                object development = GetPropertyValue(hero, "Development");
                object xp = GetPropertyValue(development, "XP");
                if (development == null || xp == null)
                {
                    Warn("Could not resolve Hero.Development.XP for raw character XP.");
                    return false;
                }

                MethodInfo increaseBy = GetMethodSilent(xp.GetType(), "IncreaseBy", 2);
                if (increaseBy == null)
                {
                    Warn("Could not find XP.IncreaseBy.");
                    return false;
                }

                increaseBy.Invoke(xp, new object[] { amount, null });

                if (_announceRawCharacterXp.Value)
                {
                    try
                    {
                        MethodInfo announce = GetMethod(development.GetType(), "AnnounceXPChanged", new[] { typeof(float) });
                        if (announce != null)
                        {
                            announce.Invoke(development, new object[] { amount });
                        }
                    }
                    catch (Exception ex)
                    {
                        Warn("Raw character XP was awarded, but the XP notification failed: " + ex.GetBaseException().Message);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Warn("Failed to award raw character XP: " + ex.GetBaseException().Message);
                return false;
            }
        }

        private bool HealHeroPercentOfMaxHealth(float percentOfMaxHealth)
        {
            if (percentOfMaxHealth <= 0f)
            {
                return false;
            }

            try
            {
                object hero = GetHero();
                if (hero == null)
                {
                    LogHealingResolution(null, null, 0f, "", "Hero.Current was unavailable");
                    Warn("Could not resolve Hero.Current for healing.");
                    return false;
                }

                object healthElement = GetPropertyValue(hero, "HealthElement");
                float maxHealth = TryGetMaxHealth(hero);
                if (maxHealth <= 0f)
                {
                    maxHealth = TryGetMaxHealth(healthElement);
                }
                if (maxHealth <= 0f)
                {
                    LogHealingResolution(hero, healthElement, maxHealth, "", "hero max health could not be resolved");
                    Warn("Could not resolve hero max health for corpse leech healing.");
                    return false;
                }

                float amount = maxHealth * (percentOfMaxHealth / 100f);
                if (amount <= 0f)
                {
                    return false;
                }

                string path;
                if (TryInvokeHealMethod(hero, amount, "Hero", out path) ||
                    TryInvokeHealMethod(healthElement, amount, "Hero.HealthElement", out path) ||
                    TryIncreaseHealthStat(hero, amount, maxHealth, "Hero", out path) ||
                    TryIncreaseHealthStat(healthElement, amount, maxHealth, "Hero.HealthElement", out path))
                {
                    LogHealingResolution(hero, healthElement, maxHealth, path, "");
                    return true;
                }

                LogHealingResolution(hero, healthElement, maxHealth, "", "no compatible heal method or health stat path was found");
                Warn("Could not find a compatible hero health heal path.");
                return false;
            }
            catch (Exception ex)
            {
                Warn("Failed to heal character from corpse leech: " + ex.GetBaseException().Message);
                return false;
            }
        }

        private void LogHealingResolution(object hero, object healthElement, float maxHealth, string path, string failure)
        {
            if (!DiagnosticsEnabled() || _loggedHealingResolution)
            {
                return;
            }

            _loggedHealingResolution = true;
            string message = "Healing resolution: hero=" + DescribeType(hero) +
                ", healthElement=" + DescribeType(healthElement) +
                ", maxHealth=" + maxHealth.ToString("0.###", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(path))
            {
                message += ", path=" + path;
            }
            if (!string.IsNullOrEmpty(failure))
            {
                message += ", failure=" + failure;
            }

            Log.LogInfo(message + ".");
        }

        private string DescribeType(object obj)
        {
            return obj == null ? "null" : obj.GetType().FullName;
        }

        private bool TryInvokeHealMethod(object target, float amount, string targetLabel, out string path)
        {
            path = "";
            if (target == null)
            {
                return false;
            }

            string[] methodNames =
            {
                "Heal",
                "RestoreHealth",
                "RecoverHealth",
                "GainHealth",
                "IncreaseHealth",
                "RegainHealth",
                "HealBy"
            };

            Type type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int nameIndex = 0; nameIndex < methodNames.Length; nameIndex++)
            {
                Type current = type;
                while (current != null)
                {
                    MethodInfo[] methods = current.GetMethods(flags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo method = methods[i];
                        if (method.Name != methodNames[nameIndex])
                        {
                            continue;
                        }

                        object[] args;
                        if (TryBuildNumericFirstArguments(method, amount, out args))
                        {
                            try
                            {
                                method.Invoke(target, args);
                                path = targetLabel + "." + method.Name + "/" + method.GetParameters().Length.ToString(CultureInfo.InvariantCulture);
                                return true;
                            }
                            catch
                            {
                            }
                        }
                    }

                    current = current.BaseType;
                }
            }

            return false;
        }

        private bool TryInvokeNumericMethod(object target, string methodName, float value)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            Type type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type current = type;
            while (current != null)
            {
                MethodInfo[] methods = current.GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != methodName)
                    {
                        continue;
                    }

                    object[] args;
                    if (!TryBuildNumericFirstArguments(method, value, out args))
                    {
                        continue;
                    }

                    try
                    {
                        method.Invoke(target, args);
                        return true;
                    }
                    catch
                    {
                    }
                }

                current = current.BaseType;
            }

            return false;
        }

        private bool TryIncreaseHealthStat(object owner, float amount, float maxHealth, string ownerLabel, out string path)
        {
            path = "";
            if (owner == null)
            {
                return false;
            }

            string[] propertyNames =
            {
                "Health",
                "CurrentHealth",
                "HP",
                "CurrentHP",
                "HitPoints",
                "CurrentHitPoints"
            };

            for (int i = 0; i < propertyNames.Length; i++)
            {
                object stat = GetPropertyValue(owner, propertyNames[i]);
                if (stat == null)
                {
                    continue;
                }

                string statPath;
                if (TryInvokeStatIncrease(stat, amount, out statPath))
                {
                    path = ownerLabel + "." + propertyNames[i] + "." + statPath;
                    return true;
                }

                if (TrySetStatValueCapped(stat, amount, maxHealth, out statPath))
                {
                    path = ownerLabel + "." + propertyNames[i] + "." + statPath;
                    return true;
                }
            }

            return false;
        }

        private bool TryInvokeStatIncrease(object stat, float amount, out string path)
        {
            path = "";
            if (stat == null)
            {
                return false;
            }

            string[] methodNames = { "IncreaseBy", "Add", "AddValue", "ModifyBy" };
            for (int i = 0; i < methodNames.Length; i++)
            {
                MethodInfo method = GetMethodSilent(stat.GetType(), methodNames[i], 2);
                object[] args;
                if (method != null && TryBuildNumericFirstArguments(method, amount, out args))
                {
                    try
                    {
                        method.Invoke(stat, args);
                        path = method.Name + "/" + method.GetParameters().Length.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch
                    {
                    }
                }

                method = GetMethodSilent(stat.GetType(), methodNames[i], 1);
                if (method != null && TryBuildNumericFirstArguments(method, amount, out args))
                {
                    try
                    {
                        method.Invoke(stat, args);
                        path = method.Name + "/" + method.GetParameters().Length.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private bool TrySetStatValueCapped(object stat, float amount, float maxHealth, out string path)
        {
            path = "";
            float current = ReadStatValue(stat);
            if (current < 0f)
            {
                return false;
            }

            float target = maxHealth > 0f ? Math.Min(maxHealth, current + amount) : current + amount;
            string[] methodNames = { "SetTo", "SetValue", "Set" };
            for (int i = 0; i < methodNames.Length; i++)
            {
                MethodInfo method = GetMethodSilent(stat.GetType(), methodNames[i], 2);
                object[] args;
                if (method != null && TryBuildNumericFirstArguments(method, target, out args))
                {
                    try
                    {
                        method.Invoke(stat, args);
                        path = method.Name + "/" + method.GetParameters().Length.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch
                    {
                    }
                }

                method = GetMethodSilent(stat.GetType(), methodNames[i], 1);
                if (method != null && TryBuildNumericFirstArguments(method, target, out args))
                {
                    try
                    {
                        method.Invoke(stat, args);
                        path = method.Name + "/" + method.GetParameters().Length.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private bool TryBuildNumericFirstArguments(MethodInfo method, float value, out object[] args)
        {
            args = null;
            if (method == null)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length < 1 || parameters.Length > 2)
            {
                return false;
            }

            object first;
            if (!TryConvertNumericArgument(parameters[0].ParameterType, value, out first))
            {
                return false;
            }

            args = new object[parameters.Length];
            args[0] = first;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].HasDefaultValue)
                {
                    args[i] = parameters[i].DefaultValue;
                }
                else if (!parameters[i].ParameterType.IsValueType || Nullable.GetUnderlyingType(parameters[i].ParameterType) != null)
                {
                    args[i] = null;
                }
                else
                {
                    args[i] = Activator.CreateInstance(parameters[i].ParameterType);
                }
            }

            return true;
        }

        private bool TryConvertNumericArgument(Type type, float value, out object converted)
        {
            converted = null;
            Type targetType = Nullable.GetUnderlyingType(type) ?? type;
            try
            {
                if (targetType == typeof(float))
                {
                    converted = value;
                    return true;
                }
                if (targetType == typeof(double))
                {
                    converted = (double)value;
                    return true;
                }
                if (targetType == typeof(decimal))
                {
                    converted = (decimal)value;
                    return true;
                }
                if (targetType == typeof(int))
                {
                    converted = (int)Math.Round(value, MidpointRounding.AwayFromZero);
                    return true;
                }
                if (targetType == typeof(long))
                {
                    converted = (long)Math.Round(value, MidpointRounding.AwayFromZero);
                    return true;
                }
                if (targetType == typeof(short))
                {
                    converted = (short)Math.Round(value, MidpointRounding.AwayFromZero);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool IsBloodTransfusionItemOrSkill(object item, object skill, out string summary)
        {
            summary = "";

            if (IsBloodTransfusionItem(item, out summary))
            {
                return true;
            }

            if (skill != null)
            {
                if (MatchesObjectText(skill, "Skill", out summary))
                {
                    return true;
                }

                object sourceItem = GetPropertyValue(skill, "SourceItem");
                if (IsBloodTransfusionItem(sourceItem, out summary))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBloodTransfusionEquippedUncached()
        {
            if (!ResolveEquipmentReflection())
            {
                return false;
            }

            object hero = GetHero();
            if (hero == null)
            {
                return false;
            }

            try
            {
                object inventory = _getHeroItemsMethod.Invoke(hero, null);
                IEnumerable slots = _allEquipmentSlotsField.GetValue(null) as IEnumerable;
                if (inventory == null || slots == null)
                {
                    return false;
                }

                foreach (object slot in slots)
                {
                    if (IsVersatileWeaponsEquipmentSlotSuppressed(slot))
                    {
                        continue;
                    }

                    object item = _equippedItemMethod.Invoke(null, new[] { inventory, slot });
                    string ignored;
                    if (IsBloodTransfusionItem(item, out ignored))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool ResolveEquipmentReflection()
        {
            if (_equipmentReflectionResolved)
            {
                return _getHeroItemsMethod != null && _equippedItemMethod != null && _allEquipmentSlotsField != null;
            }

            _equipmentReflectionResolved = true;

            Type heroType = AccessTools.TypeByName(HeroTypeName);
            Type equipmentSlotType = AccessTools.TypeByName(EquipmentSlotTypeName);
            Type inventoryExtensionsType = AccessTools.TypeByName(InventoryExtensionsTypeName);
            if (heroType == null || equipmentSlotType == null || inventoryExtensionsType == null)
            {
                Warn("Could not resolve Blood Magic Expansion spell equipped-state reflection types.");
                return false;
            }

            _getHeroItemsMethod = GetGetterSilent(heroType, "HeroItems");
            _allEquipmentSlotsField = AccessTools.Field(equipmentSlotType, "All");
            _mainHandEquipmentSlotField = AccessTools.Field(
                equipmentSlotType,
                "MainHand");
            _offHandEquipmentSlotField = AccessTools.Field(
                equipmentSlotType,
                "OffHand");
            _equippedItemMethod = GetMethodSilent(inventoryExtensionsType, "EquippedItem", 2);
            if (_getHeroItemsMethod == null || _allEquipmentSlotsField == null || _equippedItemMethod == null)
            {
                Warn("Could not resolve Blood Magic Expansion spell equipped-state reflection members.");
                return false;
            }

            return true;
        }

        private bool IsVersatileWeaponsEquipmentSlotSuppressed(object slot)
        {
            if (slot == null)
            {
                return false;
            }

            try
            {
                object mainHandSlot = _mainHandEquipmentSlotField == null
                    ? null
                    : _mainHandEquipmentSlotField.GetValue(null);
                if (Equals(slot, mainHandSlot))
                {
                    return IsVersatileWeaponsHandSuppressed(
                        BloodSpellInnerLightHand.MainHand);
                }

                object offHandSlot = _offHandEquipmentSlotField == null
                    ? null
                    : _offHandEquipmentSlotField.GetValue(null);
                return Equals(slot, offHandSlot)
                    && IsVersatileWeaponsHandSuppressed(
                        BloodSpellInnerLightHand.OffHand);
            }
            catch
            {
                return false;
            }
        }

        private bool IsBloodTransfusionItem(object item, out string summary)
        {
            summary = "";
            if (item == null)
            {
                return false;
            }

            if (MatchesObjectText(item, "Item", out summary))
            {
                return true;
            }

            object template = GetPropertyValue(item, "Template");
            return MatchesObjectText(template, "Template", out summary);
        }

        private bool IsAbhartachItemOrSkill(object item, object skill, out string summary)
        {
            summary = "";

            if (IsAbhartachItem(item, out summary))
            {
                return true;
            }

            if (skill != null)
            {
                if (MatchesAbhartachObjectText(skill, "Skill", out summary))
                {
                    return true;
                }

                object sourceItem = GetPropertyValue(skill, "SourceItem");
                if (IsAbhartachItem(sourceItem, out summary))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAbhartachEquippedUncached()
        {
            if (!ResolveEquipmentReflection())
            {
                return false;
            }

            object hero = GetHero();
            if (hero == null)
            {
                return false;
            }

            try
            {
                object inventory = _getHeroItemsMethod.Invoke(hero, null);
                IEnumerable slots = _allEquipmentSlotsField.GetValue(null) as IEnumerable;
                if (inventory == null || slots == null)
                {
                    return false;
                }

                foreach (object slot in slots)
                {
                    if (IsVersatileWeaponsEquipmentSlotSuppressed(slot))
                    {
                        continue;
                    }

                    object item = _equippedItemMethod.Invoke(null, new[] { inventory, slot });
                    string ignored;
                    if (IsAbhartachItem(item, out ignored))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool IsAbhartachHeldActiveForInterop()
        {
            return ShouldTuneAbhartach() && Now <= _abhartachHeldHealingActiveUntil;
        }

        private bool IsAbhartachItem(object item, out string summary)
        {
            summary = "";
            if (item == null)
            {
                return false;
            }

            if (MatchesAbhartachObjectText(item, "Item", out summary))
            {
                return true;
            }

            object template = GetPropertyValue(item, "Template");
            return MatchesAbhartachObjectText(template, "Template", out summary);
        }

        private bool MatchesObjectText(object obj, string label, out string summary)
        {
            summary = "";
            if (obj == null)
            {
                return false;
            }

            string configuredGuid = GetConfiguredTemplateGuid();
            string[] terms = GetMatchTerms();

            if (_cacheBloodTransfusionSourceMatches.Value)
            {
                SourceMatchCacheEntry cached;
                if (_sourceMatchCache.TryGetValue(obj, out cached) && cached.SettingsRevision == _matchSettingsRevision)
                {
                    if (cached.Matched)
                    {
                        summary = label + cached.SummarySuffix;
                    }

                    return cached.Matched;
                }
            }

            string suffix;
            bool matched = MatchesObjectTextUncached(obj, configuredGuid, terms, out suffix);
            if (_cacheBloodTransfusionSourceMatches.Value)
            {
                SourceMatchCacheEntry entry = new SourceMatchCacheEntry();
                entry.SettingsRevision = _matchSettingsRevision;
                entry.Matched = matched;
                entry.SummarySuffix = suffix;
                _sourceMatchCache[obj] = entry;
            }

            if (matched)
            {
                summary = label + suffix;
            }

            return matched;
        }

        private bool MatchesAbhartachObjectText(object obj, string label, out string summary)
        {
            summary = "";
            if (obj == null)
            {
                return false;
            }

            string configuredGuid = GetConfiguredAbhartachTemplateGuid();
            string[] terms = GetAbhartachMatchTerms();

            if (_cacheBloodTransfusionSourceMatches.Value)
            {
                SourceMatchCacheEntry cached;
                if (_abhartachSourceMatchCache.TryGetValue(obj, out cached) && cached.SettingsRevision == _abhartachMatchSettingsRevision)
                {
                    if (cached.Matched)
                    {
                        summary = label + cached.SummarySuffix;
                    }

                    return cached.Matched;
                }
            }

            string suffix;
            bool matched = MatchesObjectTextUncached(obj, configuredGuid, terms, out suffix);
            if (_cacheBloodTransfusionSourceMatches.Value)
            {
                SourceMatchCacheEntry entry = new SourceMatchCacheEntry();
                entry.SettingsRevision = _abhartachMatchSettingsRevision;
                entry.Matched = matched;
                entry.SummarySuffix = suffix;
                _abhartachSourceMatchCache[obj] = entry;
            }

            if (matched)
            {
                summary = label + suffix;
            }

            return matched;
        }

        private bool MatchesObjectTextUncached(object obj, string configuredGuid, string[] terms, out string suffix)
        {
            suffix = "";
            if (obj == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(configuredGuid))
            {
                object guidValue = GetOptionalPropertyValue(obj, "GUID") ?? GetOptionalPropertyValue(obj, "Guid");
                if (guidValue != null && string.Equals(SafeToString(guidValue).Trim(), configuredGuid, StringComparison.OrdinalIgnoreCase))
                {
                    suffix = " GUID " + configuredGuid;
                    return true;
                }
            }

            string text = BuildObjectSearchText(obj);
            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i]) && text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    suffix = " matched '" + terms[i] + "'";
                    return true;
                }
            }

            return false;
        }

        private string[] GetMatchTerms()
        {
            string raw = _bloodTransfusionMatchTerms.Value ?? "";
            string guidRaw = _bloodTransfusionTemplateGuid.Value ?? "";
            if (raw != _cachedMatchTermsRaw || guidRaw != _cachedTemplateGuidRaw)
            {
                _cachedMatchTermsRaw = raw;
                _cachedTemplateGuidRaw = guidRaw;
                _cachedMatchTerms = SplitTerms(raw);
                _matchSettingsRevision++;
                _sourceMatchCache.Clear();
            }

            return _cachedMatchTerms;
        }

        private string[] GetAbhartachMatchTerms()
        {
            string raw = _abhartachMatchTerms.Value ?? "";
            string guidRaw = _abhartachTemplateGuid.Value ?? "";
            if (raw != _cachedAbhartachMatchTermsRaw || guidRaw != _cachedAbhartachTemplateGuidRaw)
            {
                _cachedAbhartachMatchTermsRaw = raw;
                _cachedAbhartachTemplateGuidRaw = guidRaw;
                _cachedAbhartachMatchTerms = SplitTerms(raw);
                _abhartachMatchSettingsRevision++;
                _abhartachSourceMatchCache.Clear();
            }

            return _cachedAbhartachMatchTerms;
        }

        private string GetConfiguredTemplateGuid()
        {
            GetMatchTerms();
            return string.IsNullOrEmpty(_cachedTemplateGuidRaw) ? "" : _cachedTemplateGuidRaw.Trim();
        }

        private string GetConfiguredAbhartachTemplateGuid()
        {
            GetAbhartachMatchTerms();
            return string.IsNullOrEmpty(_cachedAbhartachTemplateGuidRaw) ? "" : _cachedAbhartachTemplateGuidRaw.Trim();
        }

        private bool IsBloodPlausible(CorpseState state, out string reason)
        {
            reason = "";
            if (!_requireBloodPlausible.Value || state == null)
            {
                return true;
            }

            string text = state.SearchText ?? "";
            string matched;
            if (ContainsAnyTerm(text, GetWhitelistTerms(), out matched))
            {
                return true;
            }

            if (ContainsAnyTerm(text, GetBloodlessTerms(), out matched))
            {
                reason = "matched bloodless blacklist term '" + matched + "'";
                return false;
            }

            return true;
        }

        private string[] GetBloodlessTerms()
        {
            string raw = _bloodlessBlacklistTerms.Value ?? "";
            if (raw != _cachedBloodlessTermsRaw)
            {
                _cachedBloodlessTermsRaw = raw;
                _cachedBloodlessTerms = SplitTerms(raw);
            }

            return _cachedBloodlessTerms;
        }

        private string[] GetWhitelistTerms()
        {
            string raw = _bloodWhitelistTerms.Value ?? "";
            if (raw != _cachedWhitelistTermsRaw)
            {
                _cachedWhitelistTermsRaw = raw;
                _cachedWhitelistTerms = SplitTerms(raw);
            }

            return _cachedWhitelistTerms;
        }

        private string[] GetBleedBuildupStatusTerms()
        {
            string raw = _bleedBuildupStatusTerms.Value ?? "";
            if (raw != _cachedBleedStatusTermsRaw)
            {
                _cachedBleedStatusTermsRaw = raw;
                _cachedBleedStatusTerms = SplitTerms(raw);
            }

            return _cachedBleedStatusTerms;
        }

        private bool ContainsAnyTerm(string text, string[] terms, out string matched)
        {
            matched = "";
            if (string.IsNullOrEmpty(text) || terms == null)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i]) && text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched = terms[i];
                    return true;
                }
            }

            return false;
        }

        private string[] SplitTerms(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new string[0];
            }

            string[] pieces = raw.Split(new[] { ';', ',', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> terms = new List<string>();
            for (int i = 0; i < pieces.Length; i++)
            {
                string term = pieces[i].Trim();
                if (term.Length > 0)
                {
                    terms.Add(term);
                }
            }

            return terms.ToArray();
        }

        private void AddObjectSearchText(CorpseState state, object obj)
        {
            if (state == null || obj == null)
            {
                return;
            }

            string text = BuildObjectSearchText(obj);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (string.IsNullOrEmpty(state.SearchText))
            {
                state.SearchText = text;
            }
            else if (state.SearchText.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)
            {
                state.SearchText = state.SearchText + " " + text;
            }
        }

        private string BuildObjectSearchText(object obj)
        {
            if (obj == null)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            Type type = obj.GetType();
            builder.Append(type.FullName).Append(' ');
            builder.Append(type.Name).Append(' ');
            AppendStringProperty(builder, obj, "Name");
            AppendStringProperty(builder, obj, "DisplayName");
            AppendStringProperty(builder, obj, "DebugName");
            AppendStringProperty(builder, obj, "TechnicalName");
            AppendStringProperty(builder, obj, "Id");
            AppendStringProperty(builder, obj, "ID");

            object template = GetOptionalPropertyValue(obj, "Template");
            if (template != null && !ReferenceEquals(template, obj))
            {
                Type templateType = template.GetType();
                builder.Append(templateType.FullName).Append(' ');
                builder.Append(templateType.Name).Append(' ');
                AppendStringProperty(builder, template, "Name");
                AppendStringProperty(builder, template, "DisplayName");
                AppendStringProperty(builder, template, "DebugName");
                AppendStringProperty(builder, template, "TechnicalName");
                AppendStringProperty(builder, template, "GUID");
                AppendStringProperty(builder, template, "Guid");
            }

            return builder.ToString();
        }

        private string GetDisplayName(object obj)
        {
            if (obj == null)
            {
                return "";
            }

            string value = GetStringProperty(obj, "Name");
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            object template = GetOptionalPropertyValue(obj, "Template");
            value = GetStringProperty(template, "DisplayName");
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = GetStringProperty(template, "DebugName");
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return GetStringProperty(obj, "DebugName");
        }

        private void AppendStringProperty(StringBuilder builder, object obj, string propertyName)
        {
            string value = GetStringProperty(obj, propertyName);
            if (!string.IsNullOrEmpty(value))
            {
                builder.Append(value).Append(' ');
            }
        }

        private string DescribeCorpse(CorpseState state)
        {
            if (state == null)
            {
                return "unknown corpse";
            }

            if (!string.IsNullOrEmpty(state.DisplayName))
            {
                return state.DisplayName;
            }

            if (state.TargetObject != null)
            {
                return state.TargetObject.GetType().Name;
            }

            return state.Corpse == null ? "unknown corpse" : state.Corpse.GetType().Name;
        }

        private object GetHero()
        {
            if (!_heroGetterResolved)
            {
                _heroGetterResolved = true;
                Type heroType = AccessTools.TypeByName(HeroTypeName);
                if (heroType != null)
                {
                    _heroGetter = AccessTools.PropertyGetter(heroType, "Current");
                }

                if (_heroGetter == null)
                {
                    Warn("Could not resolve Hero.Current.");
                }
            }

            if (_heroGetter == null)
            {
                return null;
            }

            try
            {
                return _heroGetter.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }

        private object GetPropertyValue(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            MethodInfo getter = GetGetterSilent(instance.GetType(), propertyName);
            if (getter == null)
            {
                return null;
            }

            try
            {
                return getter.Invoke(instance, null);
            }
            catch
            {
                return null;
            }
        }

        private object GetOptionalPropertyValue(object instance, string propertyName)
        {
            return GetPropertyValue(instance, propertyName);
        }

        private object GetMemberValue(object instance, string memberName)
        {
            object value = GetPropertyValue(instance, memberName);
            if (value != null)
            {
                return value;
            }

            FieldInfo field = GetFieldSilent(instance == null ? null : instance.GetType(), memberName);
            if (field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        private bool TryGetFloatMember(object instance, string memberName, out float value)
        {
            value = 0f;
            object raw = GetMemberValue(instance, memberName);
            if (raw == null)
            {
                return false;
            }

            value = ToFloat(raw, float.NaN);
            return !float.IsNaN(value);
        }

        private bool TrySetFloatMember(object instance, string memberName, float value)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            MethodInfo setter = GetSetterSilent(instance.GetType(), memberName);
            if (setter != null)
            {
                ParameterInfo[] parameters = setter.GetParameters();
                object converted;
                if (parameters.Length == 1 && TryConvertNumericArgument(parameters[0].ParameterType, value, out converted))
                {
                    try
                    {
                        setter.Invoke(instance, new[] { converted });
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            FieldInfo field = GetFieldSilent(instance.GetType(), memberName);
            if (field == null)
            {
                return false;
            }

            object fieldValue;
            if (!TryConvertNumericArgument(field.FieldType, value, out fieldValue))
            {
                return false;
            }

            try
            {
                field.SetValue(instance, fieldValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetIntMember(object instance, string memberName, out int value)
        {
            value = 0;
            object raw = GetMemberValue(instance, memberName);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TrySetIntMember(object instance, string memberName, int value)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            MethodInfo setter = GetSetterSilent(instance.GetType(), memberName);
            if (setter != null)
            {
                ParameterInfo[] parameters = setter.GetParameters();
                object converted;
                if (parameters.Length == 1 && TryConvertNumericArgument(parameters[0].ParameterType, value, out converted))
                {
                    try
                    {
                        setter.Invoke(instance, new[] { converted });
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            FieldInfo field = GetFieldSilent(instance.GetType(), memberName);
            if (field == null)
            {
                return false;
            }

            object fieldValue;
            if (!TryConvertNumericArgument(field.FieldType, value, out fieldValue))
            {
                return false;
            }

            try
            {
                field.SetValue(instance, fieldValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TrySetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            MethodInfo setter = GetSetterSilent(instance.GetType(), memberName);
            if (setter != null)
            {
                try
                {
                    setter.Invoke(instance, new[] { value });
                    return true;
                }
                catch
                {
                }
            }

            FieldInfo field = GetFieldSilent(instance.GetType(), memberName);
            if (field == null)
            {
                return false;
            }

            try
            {
                field.SetValue(instance, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private MethodInfo GetGetterSilent(Type type, string propertyName)
        {
            if (type == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            (Type Type, string Name) key = (type, propertyName);
            MethodInfo getter;
            if (_getterCache.TryGetValue(key, out getter))
            {
                return getter;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type current = type;
            while (current != null)
            {
                PropertyInfo property = current.GetProperty(propertyName, flags);
                if (property != null)
                {
                    getter = property.GetGetMethod(true);
                    break;
                }

                current = current.BaseType;
            }

            _getterCache[key] = getter;
            return getter;
        }

        private MethodInfo GetSetterSilent(Type type, string propertyName)
        {
            if (type == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            (Type Type, string Name) key = (type, propertyName);
            MethodInfo setter;
            if (_setterCache.TryGetValue(key, out setter))
            {
                return setter;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type current = type;
            while (current != null)
            {
                PropertyInfo property = current.GetProperty(propertyName, flags);
                if (property != null)
                {
                    setter = property.GetSetMethod(true);
                    break;
                }

                current = current.BaseType;
            }

            _setterCache[key] = setter;
            return setter;
        }

        private FieldInfo GetFieldSilent(Type type, string fieldName)
        {
            if (type == null || string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            (Type Type, string Name) key = (type, fieldName);
            FieldInfo field;
            if (_fieldCache.TryGetValue(key, out field))
            {
                return field;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type current = type;
            while (current != null)
            {
                field = current.GetField(fieldName, flags);
                if (field != null)
                {
                    break;
                }

                current = current.BaseType;
            }

            _fieldCache[key] = field;
            return field;
        }

        private MethodInfo GetMethodSilent(Type type, string methodName, int parameterCount)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            (Type Type, string Name, int ParameterCount) key = (type, methodName, parameterCount);
            MethodInfo cached;
            if (_methodCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type current = type;
            while (current != null)
            {
                MethodInfo[] methods = current.GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name == methodName && method.GetParameters().Length == parameterCount)
                    {
                        _methodCache[key] = method;
                        return method;
                    }
                }

                current = current.BaseType;
            }

            _methodCache[key] = null;
            return null;
        }

        private MethodInfo GetMethod(Type type, string methodName, Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrEmpty(methodName) || parameterTypes == null)
            {
                return null;
            }

            StringBuilder keyBuilder = new StringBuilder();
            keyBuilder.Append(type.FullName).Append(".").Append(methodName).Append("(");
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                if (i > 0)
                {
                    keyBuilder.Append(",");
                }
                keyBuilder.Append(parameterTypes[i].FullName);
            }
            keyBuilder.Append(")");

            string key = keyBuilder.ToString();
            MethodInfo cached;
            if (_exactMethodCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                bool matches = true;
                for (int j = 0; j < parameters.Length; j++)
                {
                    if (parameters[j].ParameterType != parameterTypes[j])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    _exactMethodCache[key] = method;
                    return method;
                }
            }

            _exactMethodCache[key] = null;
            return null;
        }

        private string GetStringProperty(object instance, string propertyName)
        {
            object value = GetPropertyValue(instance, propertyName);
            return value == null ? "" : SafeToString(value);
        }

        private bool GetBoolProperty(object instance, string propertyName, bool fallback)
        {
            bool found;
            return GetBoolProperty(instance, propertyName, fallback, out found);
        }

        private bool GetBoolProperty(object instance, string propertyName, bool fallback, out bool found)
        {
            found = false;
            object value = GetPropertyValue(instance, propertyName);
            if (value == null)
            {
                return fallback;
            }

            if (value is bool)
            {
                found = true;
                return (bool)value;
            }

            bool parsed;
            if (bool.TryParse(SafeToString(value), out parsed))
            {
                found = true;
                return parsed;
            }

            return fallback;
        }

        private int GetIntProperty(object instance, string propertyName, int fallback)
        {
            object value = GetPropertyValue(instance, propertyName);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private float GetOptionalFloatProperty(object instance, string propertyName, float fallback)
        {
            object value = GetOptionalPropertyValue(instance, propertyName);
            return ToFloat(value, fallback);
        }

        private float TryGetMaxHealth(object owner)
        {
            if (owner == null)
            {
                return 0f;
            }

            object maxHealth = GetPropertyValue(owner, "MaxHealth");
            float value = ReadStatValue(maxHealth);
            if (value > 0f)
            {
                return value;
            }

            object healthElement = GetPropertyValue(owner, "HealthElement");
            if (healthElement != null && !ReferenceEquals(healthElement, owner))
            {
                maxHealth = GetPropertyValue(healthElement, "MaxHealth");
                value = ReadStatValue(maxHealth);
                if (value > 0f)
                {
                    return value;
                }
            }

            return 0f;
        }

        private float ReadStatValue(object stat)
        {
            if (stat == null)
            {
                return -1f;
            }

            float direct = ToFloat(stat, float.NaN);
            if (!float.IsNaN(direct) && direct >= 0f)
            {
                return direct;
            }

            string[] properties = { "ModifiedValue", "BaseValue", "ValueForSave", "PredictedValue", "Value", "CurrentValue" };
            for (int i = 0; i < properties.Length; i++)
            {
                float value = GetOptionalFloatProperty(stat, properties[i], -1f);
                if (value >= 0f)
                {
                    return value;
                }
            }

            return -1f;
        }

        private float ToFloat(object value, float fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                float parsed;
                return float.TryParse(SafeToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
            }
        }

        private string SafeToString(object value)
        {
            if (value == null)
            {
                return "";
            }

            try
            {
                return value.ToString();
            }
            catch
            {
                return "";
            }
        }

        private bool DiagnosticsEnabled()
        {
            return _diagnostics != null && _diagnostics.Value;
        }

        private bool ShouldLogStartup()
        {
            return DiagnosticsEnabled();
        }

        private ConfigFile ResolveConfigFile()
        {
            if (_resolvedConfig != null)
            {
                return _resolvedConfig;
            }

            try
            {
                if (Config != null)
                {
                    _resolvedConfig = Config;
                    Log.LogInfo(PluginName + " using BepInEx plugin config.");
                    return _resolvedConfig;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning(PluginName + " could not access BepInEx plugin config: " + ex.GetBaseException().Message);
            }

            string configPath = "";
            try
            {
                string configDir = Paths.ConfigPath;
                if (string.IsNullOrEmpty(configDir))
                {
                    configDir = Environment.CurrentDirectory;
                }

                configPath = Path.Combine(configDir, PluginGuid + ".cfg");
                _resolvedConfig = new ConfigFile(configPath, true);
                Log.LogWarning(PluginName + " BepInEx plugin Config was null; using fallback config file: " + configPath);
                return _resolvedConfig;
            }
            catch (Exception ex)
            {
                LogStartupException("ResolveConfigFile " + configPath, ex);
                throw;
            }
        }

        private void LogStartupException(string context, Exception ex)
        {
            Exception root = ex == null ? null : ex.GetBaseException();
            string message = root == null ? "unknown error" : root.GetType().FullName + ": " + root.Message;
            Log.LogError(PluginName + " " + PluginVersion + " " + context + " failed: " + message);
            if (ex != null)
            {
                Log.LogError(ex.ToString());
            }
        }

        private void Warn(string message)
        {
            Log.LogWarning(message);
        }

        private enum Preset
        {
            BloodRite,
            Desecration,
            Exsanguination,
            Custom
        }

        private enum HandRequirement
        {
            AnyHand,
            BothHands
        }

        private enum BloodSpellInnerLightCastBoostEvent
        {
            Started,
            Evidence,
            Finished
        }

        private enum BloodSpellInnerLightHand
        {
            MainHand,
            OffHand
        }

        private enum BloodSpellInnerLightSpellKind
        {
            None,
            BloodTransfusion,
            LifeTransfusion,
            AbhartachCalling
        }

        private enum HealingPowerScalingMode
        {
            Off,
            TargetMaxHealthCurve
        }

        private sealed class DeathContext
        {
            public object HealthElement;
            public object Target;
        }

        private sealed class HealingUtilsPatchState
        {
            internal bool DefaultHealingClaimed;
            internal bool BloodHealingPresentationClaimed;
        }

        private sealed class BloodEssenceAwardReceipt
        {
            internal BloodEssenceAwardReceipt(
                ContextualFacts facts,
                float beforeEssence,
                int beforeCorpseCount,
                string corpseTierKey,
                int beforeTierCount,
                float beforeQualitySum,
                float award)
            {
                Facts = facts;
                BeforeEssence = beforeEssence;
                BeforeCorpseCount = beforeCorpseCount;
                CorpseTierKey = corpseTierKey;
                BeforeTierCount = beforeTierCount;
                BeforeQualitySum = beforeQualitySum;
                Award = award;
            }

            internal ContextualFacts Facts { get; private set; }
            internal float BeforeEssence { get; private set; }
            internal int BeforeCorpseCount { get; private set; }
            internal string CorpseTierKey { get; private set; }
            internal int BeforeTierCount { get; private set; }
            internal float BeforeQualitySum { get; private set; }
            internal float Award { get; private set; }
        }

        private sealed class BloodPowerMilestone
        {
            internal BloodPowerMilestone(float power, string eventId, string text)
            {
                Power = power;
                EventId = eventId;
                Text = text;
            }

            internal float Power { get; private set; }
            internal string EventId { get; private set; }
            internal string Text { get; private set; }
        }

        private sealed class CorpseState
        {
            public int DebugId;
            public object Corpse;
            public object TargetObject;
            public string DisplayName;
            public string SearchText;
            public float TargetKillXp;
            public float TargetEffectiveKillXp;
            public float TargetMaxHealth;
            public float TargetExpLevel;
            public int NativeTier;
            public Grailwright.Shared.CorpseQualityThreatClass TargetThreatClass;
            public Vector3 LastKnownPosition;
            public float ChannelStartTime;
            public float LastFocusTime;
            public float LastTouchedTime;
            public string LastRejectReason;
            public bool HasPosition;
            public bool RestoredFromSave;
            public bool Disabled;
            public bool Exhausted;
            public bool XpAwarded;
            public bool Healed;
            public bool TargetEffectiveKillXpHasLevelContext;
            public bool HasTargetExpLevel;
            public bool HasNativeTier;
            public bool HasTargetThreatClass;
            public bool LoggedReject;
            public float ExsanguinationSeverity;
            public object LiveServantTarget;
            public bool LiveServantHasSourceCorpse;
        }

        private sealed class StrongCastState
        {
            public string Hand;
            public float Until;
            public float NextUpdateProbeTime;
        }

        private sealed class BloodSpellInnerLightReadyState
        {
            public string Hand;
            public string Summary;
            public BloodSpellInnerLightSpellKind SpellKind;
            public float UpdatedAt;
            public float Until;
        }

        private sealed class BloodSpellInnerLightHandState
        {
            public readonly BloodSpellInnerLightHand Hand;
            public readonly string ObjectName;
            public readonly BloodSpellInnerLightCastBoostState CastBoostState =
                new BloodSpellInnerLightCastBoostState();
            public float CastBoostFactor = 1.0f;
            public float NextAnchorProbeTime;
            public bool LastVisible;
            public bool LoggedCreated;
            public bool EquipmentObservationInitialized;
            public object ObservedEquippedItem;
            public bool HasObservedNonNullEquippedItem;
            public BloodSpellInnerLightSpellKind LastNonNullEquippedSpellKind;
            public bool SuppressForNonBloodEquipment;
            public bool ImmediateFadeOutRequested;
            public GameObject LightObject;
            public Light Light;
            public HDAdditionalLightData HdrpData;
            public Transform Anchor;
            public string AnchorPropertyName;

            public BloodSpellInnerLightHandState(
                BloodSpellInnerLightHand hand,
                string objectName)
            {
                Hand = hand;
                ObjectName = objectName;
            }
        }

        private delegate bool TryGetFirstPersonArmsVisualWorldOffsetDelegate(
            out Vector3 visualWorldOffset);

        private sealed class BloodSpellInnerLightCastBoostState
        {
            public bool HasWindow;
            public float StartAt;
            public float ActiveUntil;
            public object FinishedMagicFsm;
            public float FinishedSuppressionUntil;

            public void Clear()
            {
                HasWindow = false;
                StartAt = 0.0f;
                ActiveUntil = 0.0f;
                FinishedMagicFsm = null;
                FinishedSuppressionUntil = 0.0f;
            }

            public void ClearFinishedSuppression()
            {
                FinishedMagicFsm = null;
                FinishedSuppressionUntil = 0.0f;
            }
        }

        private sealed class LiveDrainState
        {
            public object Target;
            public float BaseXp;
            public float LiveXpAwarded;
            public float LastDrainTime;
            public float LastSeenTime;
            public float NextXpTickTime;
        }

        private struct CurvePoint
        {
            public float X;
            public float Y;

            public CurvePoint(float x, float y)
            {
                X = x;
                Y = y;
            }
        }

        private sealed class SourceMatchCacheEntry
        {
            public int SettingsRevision;
            public bool Matched;
            public string SummarySuffix;
        }

        private sealed class BloodMagicProjectileState
        {
        }

        private sealed class BloodMagicBleedDurationState
        {
            public float Multiplier;
        }

        private struct BloodMagicAreaBuildupContext
        {
            public bool Scoped;
            public bool QualifyingPlayerBloodSpell;
        }

        private struct BloodMagicProjectileImpactContext
        {
            public bool Scoped;
            public bool QualifyingPlayerBloodSpell;
        }

        private struct BloodMagicBuildupApplicationContext
        {
            public bool Scoped;
            public bool IsBleed;
            public bool QualifyingPlayerBloodSpell;
        }

        private struct BloodMagicAreaBuildupScopeState
        {
            public BloodMagicAreaBuildupContext Previous;
        }

        private struct BloodMagicProjectileImpactScopeState
        {
            public BloodMagicProjectileImpactContext Previous;
        }

        private struct BloodMagicBuildupApplicationScopeState
        {
            public BloodMagicBuildupApplicationContext Previous;
            public bool IsBleed;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        internal static float AdjustFindAlivesRangeStatic(object unit, object flow, float range)
        {
            BloodMagicExpansionPlugin plugin = Instance;
            return plugin == null ? range : plugin.AdjustBloodMagicFindAlivesRange(unit, flow, range);
        }

        internal static float AdjustFindDeadBodiesRangeStatic(object unit, object flow, float range)
        {
            BloodMagicExpansionPlugin plugin = Instance;
            return plugin == null ? range : plugin.AdjustAbhartachFindDeadBodiesRange(unit, flow, range);
        }

        private static IEnumerable<CodeInstruction> InsertRangeAdjustmentAfterRangeLocalStore(
            IEnumerable<CodeInstruction> instructions,
            MethodInfo adjustMethod,
            string label)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            bool inserted = false;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Stloc_1)
                {
                    continue;
                }

                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Call, adjustMethod),
                    new CodeInstruction(OpCodes.Stloc_1)
                });
                inserted = true;
                break;
            }

            if (!inserted && Instance != null)
            {
                Instance.Warn("Could not insert " + label + " range adjustment.");
            }

            return codes;
        }

        private static class ItemStatsInitializePatch
        {
            public static void Postfix(ItemStats __instance)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.OnItemStatsInitialize(__instance);
                }
            }
        }

        private static class MagicDescriptionPatch
        {
            public static bool Prefix(
                MagicItemTemplateInfo __instance,
                ref string __result)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                return plugin == null ||
                    plugin.BeforeGetMagicDescription(__instance, ref __result);
            }
        }

        private static class DeathEventsPatch
        {
            public static void Postfix(object __instance, object outcome)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.HandleDeathEvents(__instance, outcome);
                }
            }
        }

        private static class HeroRaycasterAttachedPatch
        {
            public static void Postfix(VCHeroRaycaster __instance)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin._heroRaycaster = __instance;
                    plugin._heroViewRayFailureLogged = false;
                }
            }
        }

        private static class HeroRaycasterDiscardingPatch
        {
            public static void Prefix(VCHeroRaycaster __instance)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null && ReferenceEquals(plugin._heroRaycaster, __instance))
                {
                    plugin._heroRaycaster = null;
                }
            }
        }

        private static class HealthElementBeforeHealthDecreasePatch
        {
            public static void Postfix(object __instance, object damage)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.HandleAppliedDamage(__instance, damage);
                }
            }
        }

        private static class CorpseConstructedPatch
        {
            public static void Postfix(object __instance, object[] __args)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.HandleCorpseConstructed(__instance, __args);
                }
            }
        }

        private static class HealthElementOnDamagePatch
        {
            public static void Prefix(object __instance, Damage damage)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyBloodMagicTapDamageTuning(__instance, damage);
                }
            }
        }

        private static class CorpseRestoredPatch
        {
            public static void Postfix(object __instance)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.HandleCorpseRestored(__instance);
                }
            }
        }

        private static class FindAlivesCollectionRangePatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo adjustMethod = AccessTools.Method(
                    typeof(BloodMagicExpansionPlugin),
                    nameof(AdjustFindAlivesRangeStatic));
                if (adjustMethod == null)
                {
                    return instructions;
                }

                return InsertRangeAdjustmentAfterRangeLocalStore(
                    instructions,
                    adjustMethod,
                    "Blood/Life FindAlives");
            }
        }

        private static class FindDeadBodiesCollectionRangePatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo adjustMethod = AccessTools.Method(
                    typeof(BloodMagicExpansionPlugin),
                    nameof(AdjustFindDeadBodiesRangeStatic));
                if (adjustMethod == null)
                {
                    return instructions;
                }

                return InsertRangeAdjustmentAfterRangeLocalStore(
                    instructions,
                    adjustMethod,
                    "Abhartach FindDeadBodies");
            }
        }

        private static class HealFromDeadBodiesRangePatch
        {
            public static void Prefix(object skill, ref float range)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.AdjustAbhartachHeldCorpseSearchRange(skill, ref range);
                }
            }
        }

        private static class MagicFsmShowWeaponsPatch
        {
            public static void Postfix(object __instance, bool instant)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterBloodSpellWeaponsShown(__instance, instant);
                }
            }
        }

        private static class MagicFsmHideWeaponsPatch
        {
            public static void Postfix(object __instance, bool instant)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterBloodSpellWeaponsHidden(__instance, instant);
                }
            }
        }

        private static class TryEnterMagicCastStatePatch
        {
            public static void Postfix(object __instance, bool lightCast, bool __result)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterStrongCastStart(__instance, lightCast, __result);
                }
            }
        }

        private static class MagicFsmUpdatePatch
        {
            public static void Prefix(object __instance, ref float deltaTime)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.AdjustBloodMagicMagicFsmDeltaTime(__instance, ref deltaTime);
                }
            }

            public static void Postfix(object __instance)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RecordMagicFsmUpdate(__instance);
                }
            }
        }

        private static class MagicFsmPerformCastPatch
        {
            public static void Prefix(object __instance, bool lightCast)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterPerformCast(__instance, lightCast);
                }
            }
        }

        private static class MagicFsmEndCastingPatch
        {
            public static void Prefix(object __instance)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterCastEnding(__instance, "MagicFSM.EndCasting", false);
                }
            }
        }

        private static class MagicFsmCancelCastingPatch
        {
            public static void Prefix(object __instance)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterCastEnding(__instance, "MagicFSM.CancelCasting", true);
                }
            }
        }

        private static class CharacterStatusesBuildupStatusPatch
        {
            public static void Prefix(
                ref float buildupStrength,
                object statusTemplate,
                object sourceInfo,
                out BloodMagicBuildupApplicationScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                __state = default(BloodMagicBuildupApplicationScopeState);
                if (plugin != null)
                {
                    __state = plugin.BeginBloodMagicBuildupApplicationScope(
                        statusTemplate,
                        sourceInfo);
                    plugin.ApplyBloodMagicBuildupTuning(
                        ref buildupStrength,
                        statusTemplate,
                        sourceInfo,
                        __state.IsBleed);
                }
            }

            public static Exception Finalizer(
                Exception __exception,
                BloodMagicBuildupApplicationScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.EndBloodMagicBuildupApplicationScope(__state);
                }

                return __exception;
            }
        }

        private static class BuildupStatusBuildupPatch
        {
            public static void Postfix(object __instance, bool __result)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RecordBloodMagicBleedProc(__instance, __result);
                }
            }
        }

        private static class BuildupStatusDecayPatch
        {
            public static void Prefix(object __instance, ref float deltaTime)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyBloodMagicBleedDurationTuning(__instance, ref deltaTime);
                }
            }
        }

        private static class SphereDamageRangePatch
        {
            public static void Prefix(
                object[] __args,
                out BloodMagicAreaBuildupScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                __state = default(BloodMagicAreaBuildupScopeState);
                if (plugin != null)
                {
                    __state = plugin.BeginBloodMagicAreaBuildupScope(__args, false);
                    plugin.ApplyBloodMagicSphereAreaTuning(__args);
                }
            }

            public static void Postfix()
            {
            }

            public static Exception Finalizer(
                Exception __exception,
                BloodMagicAreaBuildupScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.EndBloodMagicAreaBuildupScope(__state);
                }

                return __exception;
            }
        }

        private static class ConeDamageRangePatch
        {
            public static void Prefix(
                object[] __args,
                out BloodMagicAreaBuildupScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                __state = default(BloodMagicAreaBuildupScopeState);
                if (plugin != null)
                {
                    __state = plugin.BeginBloodMagicAreaBuildupScope(__args, true);
                    plugin.ApplyBloodMagicConeAreaTuning(__args);
                }
            }

            public static void Postfix()
            {
            }

            public static Exception Finalizer(
                Exception __exception,
                BloodMagicAreaBuildupScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.EndBloodMagicAreaBuildupScope(__state);
                }

                return __exception;
            }
        }

        private static class DamageDealingProjectilePatch
        {
            public static void Postfix(object __instance, object weapon, object projectile)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyBloodMagicProjectileDistanceTuning(__instance, weapon, projectile);
                }
            }
        }

        private static class MagicProjectileImpactPatch
        {
            public static void Prefix(
                object __instance,
                out BloodMagicProjectileImpactScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                __state = default(BloodMagicProjectileImpactScopeState);
                if (plugin != null)
                {
                    __state = plugin.BeginBloodMagicProjectileImpactScope(__instance);
                }
            }

            public static void Postfix()
            {
            }

            public static Exception Finalizer(
                Exception __exception,
                BloodMagicProjectileImpactScopeState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.EndBloodMagicProjectileImpactScope(__state);
                }

                return __exception;
            }
        }

        private static class HealingUtilsTakeHealingPatch
        {
            public static void Prefix(
                object character,
                ref float healing,
                object healingItem,
                out HealingUtilsPatchState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                __state = null;
                if (plugin != null)
                {
                    bool isBloodMagicHealing = plugin.IsBloodMagicHealing(
                        character,
                        healing,
                        healingItem);
                    bool defaultHealingClaimed = plugin.ApplyLiveDrainHealingTuning(
                        character,
                        ref healing,
                        healingItem);
                    plugin.ApplyAbhartachHeldCorpseHealingTuning(character, ref healing, healingItem);
                    bool presentationClaimed = isBloodMagicHealing
                        && plugin.BeginGrailFloatingTextBloodHealingPresentationClaim();
                    if (defaultHealingClaimed || presentationClaimed)
                    {
                        __state = new HealingUtilsPatchState
                        {
                            DefaultHealingClaimed = defaultHealingClaimed,
                            BloodHealingPresentationClaimed = presentationClaimed
                        };
                    }
                }
            }

            public static Exception Finalizer(
                Exception __exception,
                HealingUtilsPatchState __state)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (__state != null && plugin != null)
                {
                    if (__state.BloodHealingPresentationClaimed)
                    {
                        plugin.EndGrailFloatingTextBloodHealingPresentationClaim();
                    }

                    if (__state.DefaultHealingClaimed)
                    {
                        plugin.EndGrailFloatingTextDefaultHealingClaim();
                    }
                }

                return __exception;
            }
        }
    }

    public static class BloodMagicApi
    {
        public const int ApiVersion = 10;

        public static bool IsLoaded
        {
            get { return BloodMagicExpansionPlugin.Instance != null; }
        }

        public static float GetFocusedCorpseChannelProgress01()
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null ? 0f : plugin.GetFocusedCorpseChannelProgress01ForInterop();
        }

        public static int GetFocusedCorpseState(bool requireRelevantSpell)
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null
                ? (int)BloodMagicFocusedCorpseState.None
                : plugin.GetFocusedCorpseStateForInterop(requireRelevantSpell);
        }

        public static float GetFocusedCorpseQuality01()
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null ? 0f : plugin.GetFocusedCorpseQuality01ForInterop();
        }

        public static int GetFocusedCorpseQualityTier()
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null ? 0 : plugin.GetFocusedCorpseQualityTierForInterop();
        }

        public static float GetFocusedCorpseQualityEffectMultiplier()
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null ? 1f : plugin.GetFocusedCorpseQualityEffectMultiplierForInterop();
        }

        public static float GetBloodEssence()
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null ? 0f : plugin.GetBloodEssenceForInterop();
        }

        public static float GetBloodPower()
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null ? 0f : plugin.GetBloodPowerForInterop();
        }

        public static float GetCorpseExsanguinationSeverity(object corpse)
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin == null
                ? 0.0f
                : plugin.GetCorpseExsanguinationSeverityForInterop(corpse);
        }

        public static bool IsBloodMagicDamage(object damage)
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin != null && plugin.IsBloodMagicDamageForInterop(damage);
        }

        public static bool IsBloodMagicDisplayName(string displayName)
        {
            BloodMagicExpansionPlugin plugin = BloodMagicExpansionPlugin.Instance;
            return plugin != null && plugin.IsBloodMagicDisplayNameForInterop(displayName);
        }
    }
}
