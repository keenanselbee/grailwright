using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using FMODUnity;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Blood Magic Expansion")]
[assembly: AssemblyDescription("Blood Transfusion and Life Transfusion corpse rituals, live drain rewards, and Spirituality scaling for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Blood Magic Expansion")]
[assembly: AssemblyVersion("2.4.6.0")]
[assembly: AssemblyFileVersion("2.4.6.0")]

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
    public sealed class BloodMagicExpansionPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.blood-magic-expansion";
        public const string PluginName = "Blood Magic Expansion";
        public const string PluginVersion = "2.4.6";
        private const int ConfigSchemaVersion = 11;
        private const int ConfigRecoveryBaselineSchema = 10;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const float CacheCleanupIntervalSeconds = 30f;
        private const float CompletedCorpseRetentionSeconds = 120f;
        private const float ExpiredStrongCastRetentionSeconds = 5f;
        private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string DeedsOfAvalonPluginGuid = "ks.tgfoa.deeds-of-avalon";
        private const string DeedsOfAvalonApiTypeName = "DeedsOfAvalon.StatisticsApi";
        private const string FirstPersonArmsAdjusterPluginGuid = "ks.tgfoa.first-person-arms-adjuster";
        private const string FirstPersonArmsAdjusterApiTypeName = "FirstPersonArmsAdjuster.FirstPersonArmsAdjusterApi";
        private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";
        private const string GrailFloatingTextCorpseXpEventId = "blood-magic-corpse-xp";
        private const string GrailFloatingTextLiveDrainXpEventId = "blood-magic-live-drain-xp";
        private const string GrailFloatingTextShortDurationBucket = "Short";
        private const string BloodSpellInnerLightMainHandObjectName = "BloodMagicExpansionMainHandLight";
        private const string BloodSpellInnerLightOffHandObjectName = "BloodMagicExpansionOffHandLight";
        private const float BloodSpellInnerLightAnchorRetryIntervalSeconds = 0.5f;
        private const float BloodSpellInnerLightDiagnosticIntervalSeconds = 2.0f;
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
        private const string CorpseQualityMeagerLabel = "Meager";
        private const string CorpseQualityWorthyLabel = "Worthy";
        private const string CorpseQualityPotentLabel = "Potent";
        private const string CorpseQualityPrimeLabel = "Prime";
        private const float CorpseLeechMeagerQualityMax = 0.25f;
        private const float CorpseLeechWorthyQualityMax = 0.50f;
        private const float CorpseLeechPotentQualityMax = 0.75f;

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
        private const string GameConstantsTypeName = "Awaken.TG.Main.General.Configs.GameConstants";
        private const string DamageUtilsTypeName = "Awaken.TG.Main.Fights.DamageInfo.DamageUtils";
        private const string HealingUtilsTypeName = "Awaken.TG.Main.Fights.Utils.HealingUtils";
        private const string DamageDealingProjectileTypeName = "Awaken.TG.Main.AI.Fights.Projectiles.DamageDealingProjectile";
        private const string FindAlivesTypeName = "Awaken.TG.Main.Skills.Units.Effects.FindAlives";
        private const string FindDeadBodiesTypeName = "Awaken.TG.Main.Skills.Units.Effects.FindDeadBodies";
        private const string HealFromDeadBodiesTypeName = "Awaken.TG.Main.Skills.Units.Effects.HealFromDeadBodies";
        private const string SkillUnitsTypeName = "Awaken.TG.Main.Skills.SkillUnits";

        internal static BloodMagicExpansionPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private BleedSkillGraphPreloader _bleedSkillGraphPreloader;
        private ConfigFile _resolvedConfig;
        private MethodInfo _grailFloatingTextTryClaimXpGainMethod;
        private MethodInfo _grailFloatingTextTryClaimConsolidatedXpGainMethod;
        private MethodInfo _deedsOfAvalonRecordCorpseDrainMethod;
        private bool _deedsOfAvalonBridgeResolved;
        private bool _deedsOfAvalonFailureLogged;

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
        private ConfigEntry<bool> _avoidRecentCorpseLeechRepeats;
        private ConfigEntry<int> _recentCorpseLeechSoundMemory;
        private ConfigEntry<float> _corpseLeechRandomPitchSemitones;
        private ConfigEntry<float> _corpseQualityReferenceKillXp;
        private ConfigEntry<float> _corpseQualityReferenceMaxHealth;
        private ConfigEntry<bool> _corpseQualityScaleTransfusionHealing;
        private ConfigEntry<bool> _corpseQualityScaleAbhartachEffects;
        private ConfigEntry<float> _corpseQualityMinimumEffectMultiplier;
        private ConfigEntry<float> _corpseQualityMaximumEffectMultiplier;
        private ConfigEntry<float> _corpseQualityEffectMemorySeconds;
        private ConfigEntry<float> _corpseQualityFallbackQuality;

        private ConfigEntry<bool> _liveDrainEnabled;
        private ConfigEntry<bool> _liveDrainAwardCharacterXp;
        private ConfigEntry<float> _liveDrainRawCharacterXpMultiplier;
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
        private ConfigEntry<float> _bloodSpellInnerLightRange;
        private ConfigEntry<float> _bloodSpellInnerLightFadeSeconds;
        private ConfigEntry<bool> _bloodSpellScaleProjectileTravel;
        private ConfigEntry<bool> _bloodSpellScaleHomingTargetSearch;
        private ConfigEntry<bool> _bloodSpellScaleHeldTargetRange;
        private ConfigEntry<float> _customBloodSpellRangeDistanceMultiplier;
        private ConfigEntry<float> _customBloodSpellHomingTargetSearchMultiplier;
        private ConfigEntry<float> _customBloodSpellHeldTargetRangeMultiplier;
        private ConfigEntry<float> _bloodSpellHomingTargetSearchMaximumMultiplier;
        private ConfigEntry<float> _bloodSpellHeldTargetRangeMaximumMultiplier;
        private ConfigEntry<float> _customBloodSpellBleedBuildupMultiplier;
        private ConfigEntry<float> _customBloodSpellTapCastSpeedMultiplier;
        private ConfigEntry<float> _customBloodSpellHeldChannelSpeedMultiplier;
        private ConfigEntry<bool> _bloodSpellSpiritualityScalingEnabled;
        private ConfigEntry<string> _bloodSpellSpiritualityStatTerms;
        private ConfigEntry<string> _bloodSpellRangeBleedTapSpiritualityBonusCurve;
        private ConfigEntry<string> _bloodSpellTargetSearchSpiritualityBonusCurve;
        private ConfigEntry<string> _bloodSpellHeldSpiritualityBonusCurve;
        private ConfigEntry<string> _bleedBuildupStatusTerms;

        private ConfigEntry<bool> _abhartachTuningEnabled;
        private ConfigEntry<string> _abhartachMatchTerms;
        private ConfigEntry<string> _abhartachTemplateGuid;
        private ConfigEntry<bool> _abhartachScaleExplosionDamage;
        private ConfigEntry<bool> _abhartachScaleExplosionRadius;
        private ConfigEntry<bool> _abhartachScaleExplosionBleed;
        private ConfigEntry<bool> _abhartachScaleHeldCorpseHealing;
        private ConfigEntry<bool> _abhartachScaleCorpseSearchRange;
        private ConfigEntry<float> _customAbhartachExplosionDamageMultiplier;
        private ConfigEntry<float> _customAbhartachExplosionRadiusMultiplier;
        private ConfigEntry<float> _customAbhartachExplosionBleedBuildupMultiplier;
        private ConfigEntry<float> _customAbhartachHeldCorpseHealingMultiplier;
        private ConfigEntry<float> _customAbhartachCorpseSearchRangeMultiplier;
        private ConfigEntry<float> _abhartachCorpseSearchMaximumMultiplier;
        private ConfigEntry<string> _abhartachExplosionDamageSpiritualityBonusCurve;
        private ConfigEntry<string> _abhartachExplosionRadiusSpiritualityBonusCurve;
        private ConfigEntry<string> _abhartachExplosionBleedSpiritualityBonusCurve;
        private ConfigEntry<string> _abhartachHeldHealingSpiritualityBonusCurve;
        private ConfigEntry<string> _abhartachCorpseSearchSpiritualityBonusCurve;

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

        private ConfigEntry<bool> _logStartup;
        private ConfigEntry<bool> _logAwards;
        private ConfigEntry<bool> _logRejectedCorpses;
        private ConfigEntry<bool> _logUnresolvedRaycastHits;
        private ConfigEntry<bool> _logHealingResolution;
        private ConfigEntry<bool> _logPatchWarnings;
        private ConfigEntry<bool> _logCorpseQuality;
        private ConfigEntry<bool> _logBloodSpellInnerLight;
        private ConfigEntry<float> _corpseQualityLogIntervalSeconds;
        private ConfigEntry<bool> _claimGrailFloatingTextCorpseXp;
        private ConfigEntry<bool> _claimGrailFloatingTextLiveDrainXp;
        private readonly Dictionary<string, float> _pendingPreservedCalibrationFloats =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pendingPreservedManualOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal);
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
        private readonly Dictionary<string, FMOD.Sound> _corpseLeechFmodSoundsByPath =
            new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _corpseLeechSoundPathsByTier =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _recentCorpseLeechSoundPathsByTier =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Random _random = new System.Random();
        private readonly ConditionalWeakTable<object, ProjectileTuningState> _tunedProjectiles =
            new ConditionalWeakTable<object, ProjectileTuningState>();
        private readonly HashSet<object> _loggedUnresolvedRaycastHits =
            new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<(Type Type, string Name), MethodInfo> _getterCache =
            new Dictionary<(Type Type, string Name), MethodInfo>();
        private readonly Dictionary<(Type Type, string Name), MethodInfo> _setterCache =
            new Dictionary<(Type Type, string Name), MethodInfo>();
        private readonly Dictionary<(Type Type, string Name, int ParameterCount), MethodInfo> _methodCache =
            new Dictionary<(Type Type, string Name, int ParameterCount), MethodInfo>();
        private readonly Dictionary<string, MethodInfo> _exactMethodCache = new Dictionary<string, MethodInfo>();
        private readonly Dictionary<(Type Type, string Name), FieldInfo> _fieldCache =
            new Dictionary<(Type Type, string Name), FieldInfo>();

        private string _cachedMatchTermsRaw;
        private string _cachedTemplateGuidRaw;
        private string _cachedBloodlessTermsRaw;
        private string _cachedWhitelistTermsRaw;
        private string _cachedBleedStatusTermsRaw;
        private string _cachedSpiritualityTermsRaw;
        private string _cachedRangeBleedTapSpiritualityCurveRaw;
        private string _cachedTargetSearchSpiritualityCurveRaw;
        private string _cachedHeldSpiritualityCurveRaw;
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
        private string[] _cachedSpiritualityTerms = new string[0];
        private CurvePoint[] _cachedRangeBleedTapSpiritualityCurve = new CurvePoint[0];
        private CurvePoint[] _cachedTargetSearchSpiritualityCurve = new CurvePoint[0];
        private CurvePoint[] _cachedHeldSpiritualityCurve = new CurvePoint[0];
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
        private float _nextCacheCleanupTime;
        private CorpseState _focusedCorpse;
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
        private float _cachedHeroSpiritualityValue;
        private float _nextHeroSpiritualityRefreshTime;
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
        private int _bloodSpellInnerLightActivationLogsRemaining = 32;
        private bool _corpseLeechSoundPathsResolved;
        private bool _loggedMissingCorpseLeechSounds;
        private bool _loggedBloodSpellInnerLightHdrpUnavailable;
        private bool _loggedVanillaXpFalloffUnavailable;
        private bool _grailFloatingTextBridgeResolved;
        private bool _grailFloatingTextUnavailableLogged;
        private bool _firstPersonArmsAdjusterBridgeResolved;
        private bool _firstPersonArmsAdjusterUnavailableLogged;
        private TryGetFirstPersonArmsVisualWorldOffsetDelegate
            _tryGetFirstPersonArmsVisualWorldOffset;
        private Type _hdAdditionalLightDataType;
        private bool _hdAdditionalLightDataResolved;
        private MethodInfo _heroGetter;
        private MethodInfo _gameConstantsGetter;
        private MethodInfo _skillUnitsSkillMethod;
        private MethodInfo _getHeroItemsMethod;
        private MethodInfo _equippedItemMethod;
        private FieldInfo _allEquipmentSlotsField;

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
            if (_enabled == null)
            {
                return;
            }

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

        private void BindConfig()
        {
            ConfigFile config = ResolveConfigFile();
            ResetConfigIfSchemaChanged(config);

            _enabled = config.Bind("1. Core", "Enabled", true, "Master switch.");
            config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version for this clean Blood Magic Expansion config.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _preset = config.Bind("1. Core", "Preset", Preset.Desecration, "Main profile. BloodRite is quick and restrained, Desecration is the balanced default, SoulFeast is slower and more rewarding, and Custom uses the advanced custom values.");
            _preloadBleedSkillGraphs = config.Bind(
                "1. Core",
                "PreloadBleedSkillGraphs",
                true,
                "Preload and retain the vanilla Bleed status skill graph during gameplay loading instead of its first combat application. This isolated compatibility option can be disabled if a future game update removes the cold-load hitch.");

            _handRequirement = config.Bind("2. Main Loop", "HandRequirement", HandRequirement.AnyHand, "Minimum required Blood/Life Transfusion hold state. AnyHand allows single-hand half payout and dual-hand full payout.");
            _singleHandPayoutMultiplier = config.Bind("2. Main Loop", "SingleHandPayoutMultiplier", 0.5f, "Payout multiplier when only one Blood/Life Transfusion hand is held. Dual-held casts always use the full amount.");
            _awardCharacterXp = config.Bind("2. Main Loop", "AwardCorpseXP", true, "Award character XP when a valid corpse ritual completes.");
            _healCharacter = config.Bind("2. Main Loop", "HealFromCorpses", true, "Heal the player when a valid corpse ritual completes.");
            _liveDrainEnabled = config.Bind("2. Main Loop", "LiveDrainXP", true, "Award small capped XP ticks while held Blood/Life Transfusion damages living enemies.");
            _bloodSpellTuningEnabled = config.Bind("2. Main Loop", "BloodSpellTuning", true, "Tune Blood Transfusion and Life Transfusion with the selected preset plus Spirituality.");
            _abhartachTuningEnabled = config.Bind("2. Main Loop", "AbhartachTuning", true, "Tune Abhartach's Calling corpse effects with the selected preset plus Spirituality.");

            _bloodSpellInnerLightEnabled = config.Bind("2. Blood Spell Inner Light", "Enabled", true, "Show a red no-shadow light from each raised hand that has Blood Transfusion, Life Transfusion, or Abhartach's Calling equipped.");
            _bloodSpellInnerLightIntensity = config.Bind("2. Blood Spell Inner Light", "Intensity", 0.5f, new ConfigDescription("Shared base brightness of each red hand light while its blood spell is readied, before the per-spell and interior multipliers. Actual casting temporarily triples that hand's final value 0.3 seconds after cast start, then drops back quickly when casting performs, ends, or cancels. This is a user-friendly brightness value that BME scales for the game's HDRP renderer. Zero disables visible light without removing the feature.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightBloodTransfusionIntensityMultiplier = config.Bind("2. Blood Spell Inner Light", "BloodTransfusionIntensityMultiplier", 0.8f, new ConfigDescription("Brightness multiplier applied when Blood Transfusion is readied in this hand.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightLifeTransfusionIntensityMultiplier = config.Bind("2. Blood Spell Inner Light", "LifeTransfusionIntensityMultiplier", 1.0f, new ConfigDescription("Brightness multiplier applied when Life Transfusion is readied in this hand.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightAbhartachCallingIntensityMultiplier = config.Bind("2. Blood Spell Inner Light", "AbhartachCallingIntensityMultiplier", 1.2f, new ConfigDescription("Brightness multiplier applied when Abhartach's Calling is readied in this hand.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightInteriorIntensityMultiplier = config.Bind("2. Blood Spell Inner Light", "InteriorIntensityMultiplier", 1.0f, new ConfigDescription("Additional blood hand-light intensity multiplier in full interior scenes. One preserves the configured intensity, two doubles it, and zero disables the visible hand lights only while indoors.", new AcceptableValueRange<float>(0.0f, 8.0f)));
            _bloodSpellInnerLightRange = config.Bind("2. Blood Spell Inner Light", "Range", 5.0f, new ConfigDescription("Range in meters for the red inner player light. Smaller ranges are cheaper and subtler.", new AcceptableValueRange<float>(0.1f, 20.0f)));
            _bloodSpellInnerLightFadeSeconds = config.Bind("2. Blood Spell Inner Light", "FadeSeconds", 0.12f, new ConfigDescription("Seconds used to fade the red inner player light in and out. Zero switches instantly.", new AcceptableValueRange<float>(0.0f, 2.0f)));

            _corpseQualityReferenceKillXp = config.Bind("3. Corpse Quality", "ReferenceKillXP", 300.0f, new ConfigDescription("Kill XP that contributes full XP weight to corpse quality.", new AcceptableValueRange<float>(1.0f, 100000.0f)));
            _corpseQualityReferenceMaxHealth = config.Bind("3. Corpse Quality", "ReferenceMaxHealth", 600.0f, new ConfigDescription("Enemy max HP that contributes full health weight to corpse quality.", new AcceptableValueRange<float>(1.0f, 100000.0f)));
            _corpseQualityScaleTransfusionHealing = config.Bind("3. Corpse Quality", "ScaleTransfusionHealing", true, "Let corpse quality modestly scale Blood/Life Transfusion corpse healing. Character XP is not multiplied again.");
            _corpseQualityScaleAbhartachEffects = config.Bind("3. Corpse Quality", "ScaleAbhartachEffects", true, "Let corpse quality modestly scale Abhartach corpse explosion damage, radius, bleed buildup, and held corpse healing.");
            _corpseQualityMinimumEffectMultiplier = config.Bind("3. Corpse Quality", "MinimumEffectMultiplier", 0.5f, new ConfigDescription("Gameplay effect multiplier used for a very low-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _corpseQualityMaximumEffectMultiplier = config.Bind("3. Corpse Quality", "MaximumEffectMultiplier", 1.5f, new ConfigDescription("Gameplay effect multiplier used for a high-quality corpse.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _corpseQualityFallbackQuality = config.Bind("3. Corpse Quality", "FallbackQuality", 0.0f, new ConfigDescription("Focused corpse quality used when kill XP and max health cannot be resolved.", new AcceptableValueRange<float>(0.0f, 1.0f)));

            _requireBloodPlausible = config.Bind("4. Bloodless Filter", "RequireBloodPlausible", true, "Reject corpses and live targets that plausibly have no blood.");
            _bloodlessBlacklistTerms = config.Bind("4. Bloodless Filter", "BloodlessBlacklistTerms", "Stone;Golem;Statue;Construct;Automaton;Crystal;Wisp;Spirit;Ghost;Wraith;Specter;Spectre;Skeleton;Skull;Bone;Animated Armor;Elemental;Wyrdspawn;Wyrdspirit;Wyrd Spirit;WyrdSlime;Wyrd Slime;Wyrdness", "Semicolon, comma, pipe, or newline separated terms that make a target ineligible unless whitelisted.");
            _bloodWhitelistTerms = config.Bind("4. Bloodless Filter", "BloodWhitelistTerms", "", "Optional terms that force eligibility even if a blacklist term also matches.");

            _fallbackKillXp = config.Bind("5. Advanced - Corpse Rewards", "FallbackKillXP", 0.0f, "Normal kill XP to use when real corpse XP cannot be resolved. Zero skips unresolved corpses.");
            _minimumXpToPay = config.Bind("5. Advanced - Corpse Rewards", "MinimumXPToPay", 1.0f, "Minimum computed corpse XP required to pay.");
            _maximumXp = config.Bind("5. Advanced - Corpse Rewards", "MaximumXP", 0.0f, "Absolute maximum corpse XP per corpse. Zero or less disables this cap.");
            _roundXpTo = config.Bind("5. Advanced - Corpse Rewards", "RoundXPTo", 1.0f, "Round corpse XP to this increment. One rounds to whole XP; zero disables rounding.");
            _requireTargetXpRewardAllowedWhenPresent = config.Bind("5. Advanced - Corpse Rewards", "RequireTargetXPRewardAllowedWhenPresent", true, "If the corpse source exposes XpRewardAllowed, require it to be true. Sources without that property are still allowed.");
            _rawCharacterXpPerCorpseXp = config.Bind("5. Advanced - Corpse Rewards", "RawCharacterXPPerCorpseXP", 1.0f, "Raw character XP awarded per computed corpse XP.");
            _announceRawCharacterXp = config.Bind("5. Advanced - Corpse Rewards", "AnnounceRawCharacterXP", false, "Usually leave off; direct XP stat changes already announce themselves.");
            _healMaxHealthPercentPerXpPercent = config.Bind("5. Advanced - Corpse Rewards", "HealMaxHealthPercentPerXpPercent", 0.5f, "Baseline healing as max-health percent per XP reward percent before enemy power scaling.");
            _healPowerScalingMode = config.Bind("5. Advanced - Corpse Rewards", "HealPowerScalingMode", HealingPowerScalingMode.TargetMaxHealthCurve, "Off uses fixed preset healing. TargetMaxHealthCurve scales healing by the drained enemy's resolved max health.");
            _healReferenceTargetMaxHealth = config.Bind("5. Advanced - Corpse Rewards", "HealReferenceTargetMaxHealth", 300.0f, new ConfigDescription("Enemy max HP that receives unmodified baseline healing.", new AcceptableValueRange<float>(1.0f, 100000.0f)));
            _healPowerExponent = config.Bind("5. Advanced - Corpse Rewards", "HealPowerExponent", 0.5f, new ConfigDescription("Curve exponent for max-HP healing scaling. 0.5 is a smooth square-root curve; 1 is linear.", new AcceptableValueRange<float>(0.05f, 3.0f)));
            _healMinimumPowerScale = config.Bind("5. Advanced - Corpse Rewards", "HealMinimumPowerScale", 0.5f, new ConfigDescription("Lowest multiplier applied to baseline healing when enemy max HP is low.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _healMaximumPowerScale = config.Bind("5. Advanced - Corpse Rewards", "HealMaximumPowerScale", 2.0f, new ConfigDescription("Highest multiplier applied to baseline healing when enemy max HP is high.", new AcceptableValueRange<float>(0.0f, 10.0f)));

            _customPayoutPercentOfKillXp = config.Bind("6. Advanced - Custom Preset", "CustomCorpseXPPercent", 50.0f, "Corpse payout as a percent of the enemy's vanilla effective kill XP when Preset is Custom.");
            _secondsRequired = config.Bind("6. Advanced - Custom Preset", "CustomRitualSeconds", 3.0f, "Seconds of continuous corpse focus required when Preset is Custom.");
            _customLiveDrainXpTickIntervalSeconds = config.Bind("6. Advanced - Custom Preset", "CustomLiveDrainTickSeconds", 1.5f, "Seconds between live-drain XP ticks when Preset is Custom.");
            _customLiveDrainXpPercentPerTick = config.Bind("6. Advanced - Custom Preset", "CustomLiveDrainXPPercentPerTick", 7.0f, "Percent of target kill XP paid per live-drain XP tick when Preset is Custom.");
            _customLiveDrainMaximumXpPercentPerTarget = config.Bind("6. Advanced - Custom Preset", "CustomLiveDrainXPPercentCapPerTarget", 35.0f, "Maximum percent of target kill XP paid by live-drain ticks when Preset is Custom.");
            _customBloodSpellRangeDistanceMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomBloodSpellRangeMultiplier", 1.06f, "Custom preset base projectile travel and spell damage-radius multiplier before Spirituality scaling.");
            _customBloodSpellHomingTargetSearchMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomBloodSpellHomingSearchMultiplier", 1.05f, "Custom preset base homing target-search multiplier before Spirituality scaling.");
            _customBloodSpellHeldTargetRangeMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomBloodSpellHeldRangeMultiplier", 1.03f, "Custom preset base held target-search range multiplier before Spirituality scaling.");
            _customBloodSpellBleedBuildupMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomBloodSpellBleedMultiplier", 1.06f, "Custom preset base bleed buildup multiplier before Spirituality scaling.");
            _customBloodSpellTapCastSpeedMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomBloodSpellTapSpeedMultiplier", 1.06f, "Custom preset base tap/projectile cast-speed multiplier before Spirituality scaling.");
            _customBloodSpellHeldChannelSpeedMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomBloodSpellHeldChannelSpeedMultiplier", 1.01f, "Custom preset base held/channel delta-time multiplier before Spirituality scaling.");
            _customAbhartachExplosionDamageMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomAbhartachExplosionDamageMultiplier", 1.05f, "Custom preset base explosion damage multiplier before Spirituality scaling.");
            _customAbhartachExplosionRadiusMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomAbhartachExplosionRadiusMultiplier", 1.10f, "Custom preset base explosion radius multiplier before Spirituality scaling.");
            _customAbhartachExplosionBleedBuildupMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomAbhartachExplosionBleedMultiplier", 1.12f, "Custom preset base explosion bleed buildup multiplier before Spirituality scaling.");
            _customAbhartachHeldCorpseHealingMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomAbhartachHeldCorpseHealingMultiplier", 1.20f, "Custom preset base held corpse healing multiplier before Spirituality scaling.");
            _customAbhartachCorpseSearchRangeMultiplier = config.Bind("6. Advanced - Custom Preset", "CustomAbhartachCorpseSearchMultiplier", 1.05f, "Custom preset base corpse-search range multiplier before Spirituality scaling.");

            _liveDrainAwardCharacterXp = config.Bind("7. Advanced - Live Drain", "AwardCharacterXP", true, "Award small character XP ticks while held Blood/Life Transfusion damages living enemies.");
            _liveDrainRawCharacterXpMultiplier = config.Bind("7. Advanced - Live Drain", "RawCharacterXPPerComputedXP", 1.0f, "Raw character XP awarded per computed live-drain XP.");

            _bloodSpellScaleProjectileTravel = config.Bind("8. Advanced - Blood Spell Growth", "ScaleProjectileTravel", true, "Scale Blood/Life Transfusion projectile travel distance by increasing projectile lifetime.");
            _bloodSpellScaleHomingTargetSearch = config.Bind("8. Advanced - Blood Spell Growth", "ScaleHomingTargetSearch", true, "Scale Blood/Life Transfusion homing target-search distance when the projectile exposes one.");
            _bloodSpellScaleHeldTargetRange = config.Bind("8. Advanced - Blood Spell Growth", "ScaleHeldTargetRange", true, "Scale Blood/Life Transfusion visual-script target-search range.");
            _bloodSpellHomingTargetSearchMaximumMultiplier = config.Bind("8. Advanced - Blood Spell Growth", "HomingTargetSearchMaximumMultiplier", 1.75f, new ConfigDescription("Maximum final Blood/Life homing target-search multiplier after preset and Spirituality scaling.", new AcceptableValueRange<float>(1.0f, 10.0f)));
            _bloodSpellHeldTargetRangeMaximumMultiplier = config.Bind("8. Advanced - Blood Spell Growth", "HeldTargetRangeMaximumMultiplier", 1.5f, new ConfigDescription("Maximum final Blood/Life held target-search range multiplier after preset and Spirituality scaling.", new AcceptableValueRange<float>(1.0f, 10.0f)));
            _bloodSpellSpiritualityScalingEnabled = config.Bind("8. Advanced - Blood Spell Growth", "SpiritualityScalingEnabled", true, "Scale Blood/Life Transfusion bonuses from the hero's Spirituality stat.");
            _bloodSpellRangeBleedTapSpiritualityBonusCurve = config.Bind("8. Advanced - Blood Spell Growth", "RangeBleedTapSpiritualityBonusCurve", "0:0;5:2;10:5;15:10;20:17;25:25;30:35;35:47;40:60;45:75;50:90", "Spirituality-to-bonus-percent curve for range, bleed buildup, and tap/projectile cast speed.");
            _bloodSpellTargetSearchSpiritualityBonusCurve = config.Bind("8. Advanced - Blood Spell Growth", "TargetSearchSpiritualityBonusCurve", "0:0;5:0;10:2;15:4;20:6;25:9;30:12;35:16;40:22;45:28;50:35", "Gentler Spirituality-to-bonus-percent curve for held and homing target-search range.");
            _bloodSpellHeldSpiritualityBonusCurve = config.Bind("8. Advanced - Blood Spell Growth", "HeldChannelSpiritualityBonusCurve", "0:0;5:0;10:1;15:2;20:3;25:4;30:5;35:6;40:8;45:10;50:12", "Spirituality-to-bonus-percent curve for held/channel speed.");
            _bleedBuildupStatusTerms = config.Bind("8. Advanced - Blood Spell Growth", "BleedBuildupStatusTerms", "Bleed;Bleeding", "Terms used to identify bleed buildup statuses for tuning.");

            _abhartachScaleExplosionDamage = config.Bind("9. Advanced - Abhartach Calling", "ScaleExplosionDamage", true, "Scale Abhartach's Calling corpse explosion damage.");
            _abhartachScaleExplosionRadius = config.Bind("9. Advanced - Abhartach Calling", "ScaleExplosionRadius", true, "Scale Abhartach's Calling corpse explosion radius.");
            _abhartachScaleExplosionBleed = config.Bind("9. Advanced - Abhartach Calling", "ScaleExplosionBleed", true, "Scale Abhartach's Calling corpse explosion bleed buildup.");
            _abhartachScaleHeldCorpseHealing = config.Bind("9. Advanced - Abhartach Calling", "ScaleHeldCorpseHealing", true, "Scale Abhartach's Calling held corpse healing.");
            _abhartachScaleCorpseSearchRange = config.Bind("9. Advanced - Abhartach Calling", "ScaleCorpseSearchRange", true, "Scale Abhartach's Calling corpse-search range.");
            _abhartachCorpseSearchMaximumMultiplier = config.Bind("9. Advanced - Abhartach Calling", "CorpseSearchMaximumMultiplier", 1.5f, new ConfigDescription("Maximum final Abhartach corpse-search range multiplier after preset and Spirituality scaling.", new AcceptableValueRange<float>(1.0f, 10.0f)));
            _corpseQualityEffectMemorySeconds = config.Bind("9. Advanced - Abhartach Calling", "CorpseQualityEffectMemorySeconds", 1.25f, new ConfigDescription("Seconds to remember the last Abhartach-focused corpse quality for delayed spell effects.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _abhartachExplosionDamageSpiritualityBonusCurve = config.Bind("9. Advanced - Abhartach Calling", "ExplosionDamageSpiritualityBonusCurve", "0:0;5:1;10:3;15:6;20:10;25:14;30:18;35:23;40:28;45:34;50:40", "Spirituality-to-bonus-percent curve for explosion damage.");
            _abhartachExplosionRadiusSpiritualityBonusCurve = config.Bind("9. Advanced - Abhartach Calling", "ExplosionRadiusSpiritualityBonusCurve", "0:0;5:1;10:2;15:4;20:7;25:10;30:14;35:18;40:23;45:29;50:35", "Spirituality-to-bonus-percent curve for explosion radius.");
            _abhartachExplosionBleedSpiritualityBonusCurve = config.Bind("9. Advanced - Abhartach Calling", "ExplosionBleedSpiritualityBonusCurve", "0:0;5:1;10:3;15:6;20:10;25:14;30:18;35:23;40:28;45:34;50:40", "Spirituality-to-bonus-percent curve for explosion bleed buildup.");
            _abhartachHeldHealingSpiritualityBonusCurve = config.Bind("9. Advanced - Abhartach Calling", "HeldCorpseHealingSpiritualityBonusCurve", "0:0;5:1;10:4;15:7;20:10;25:14;30:18;35:23;40:28;45:34;50:40", "Spirituality-to-bonus-percent curve for held corpse healing.");
            _abhartachCorpseSearchSpiritualityBonusCurve = config.Bind("9. Advanced - Abhartach Calling", "CorpseSearchSpiritualityBonusCurve", "0:0;5:0;10:2;15:4;20:6;25:9;30:12;35:16;40:22;45:28;50:35", "Gentler Spirituality-to-bonus-percent curve for corpse-search range.");

            _bloodTransfusionMatchTerms = config.Bind("10. Advanced - Matching", "BloodSpellMatchTerms", "BloodTransfusion;Blood Transfusion;ItemTemplate_Magic_Tier1_BloodTransfusion;LifeTransfusion;Life Transfusion;ItemTemplate_Magic_Tier1_LifeTransfusion", "Terms used to identify Blood Transfusion and Life Transfusion items, skills, or templates.");
            _bloodTransfusionTemplateGuid = config.Bind("10. Advanced - Matching", "BloodSpellTemplateGuid", "", "Optional exact Blood/Life Transfusion template GUID.");
            _abhartachMatchTerms = config.Bind("10. Advanced - Matching", "AbhartachMatchTerms", "Abhartach;Abhartach's Calling;ItemTemplate_Magic_Tier2_AbhartachsCalling", "Terms used to identify Abhartach's Calling items, skills, or templates.");
            _abhartachTemplateGuid = config.Bind("10. Advanced - Matching", "AbhartachTemplateGuid", "", "Optional exact Abhartach's Calling template GUID.");
            _bloodSpellSpiritualityStatTerms = config.Bind("10. Advanced - Matching", "SpiritualityStatTerms", "Spirituality;Spirit", "Terms used to find the hero Spirituality stat by reflection.");

            _range = config.Bind("11. Performance", "Range", 7.0f, "Maximum camera raycast distance for detecting the corpse being looked at.");
            _checkIntervalSeconds = config.Bind("11. Performance", "CheckIntervalSeconds", 0.15f, "Seconds between corpse look checks while the required spell hold is active.");
            _focusGraceSeconds = config.Bind("11. Performance", "FocusGraceSeconds", 0.35f, "Short grace window before losing corpse focus resets the preset ritual timer.");
            _strongHoldGraceSeconds = config.Bind("11. Performance", "StrongHoldGraceSeconds", 0.85f, "Grace window used when converting Blood/Life spell cast/hold events into active hand state.");
            _holdTrackerIntervalSeconds = config.Bind("11. Performance", "HoldTrackerIntervalSeconds", 0.15f, "Minimum seconds between MagicFSM held-state probes.");
            _raycastLayerMask = config.Bind("11. Performance", "RaycastLayerMask", -1, "Unity physics layer mask used by the corpse look raycast. -1 checks all layers.");
            _raycastParentSearchDepth = config.Bind("11. Performance", "RaycastParentSearchDepth", 20, "Maximum parent transforms checked when resolving a corpse body collider.");
            _nearestCorpseFallbackRadius = config.Bind("11. Performance", "NearestCorpseFallbackRadius", 2.0f, "Maximum meters from an unresolved corpse-like collider to an unexhausted registered corpse for fallback matching. Zero disables fallback.");
            _raycastAllFallbackMaxHits = config.Bind("11. Performance", "RaycastAllFallbackMaxHits", 10, "Maximum sorted RaycastAll hits checked after the primary corpse raycast cannot resolve a usable corpse. Zero disables fallback.");
            _unresolvedCorpseRefreshIntervalSeconds = config.Bind("11. Performance", "UnresolvedCorpseRefreshIntervalSeconds", 1.5f, "Minimum seconds between cached corpse alias refreshes after an unresolved corpse-like raycast hit.");
            _corpseHierarchyAliasMaxNodes = config.Bind("11. Performance", "CorpseHierarchyAliasMaxNodes", 96, "Maximum child transforms/colliders cached per corpse visual hierarchy.");
            _cacheBloodTransfusionSourceMatches = config.Bind("11. Performance", "CacheBloodSpellSourceMatches", true, "Cache Blood/Life spell item, skill, and template match results by object reference.");

            _playCorpseLeechSound = config.Bind("12. Audio", "PlayCorpseLeechSound", true, "Play a quality-matched FMOD WAV when a corpse ritual successfully completes.");
            _corpseLeechSoundVolume = config.Bind("12. Audio", "CorpseLeechSoundVolume", 0.85f, new ConfigDescription("Global FMOD volume for corpse leech sounds.", new AcceptableValueRange<float>(0.0f, 2.0f)));
            _avoidRecentCorpseLeechRepeats = config.Bind("12. Audio", "AvoidRecentCorpseLeechRepeats", true, "Avoid replaying recently used corpse leech sounds from the same quality tier when enough alternatives are available.");
            _recentCorpseLeechSoundMemory = config.Bind("12. Audio", "RecentCorpseLeechSoundMemory", 2, new ConfigDescription("How many recently played sounds to avoid per quality tier.", new AcceptableValueRange<int>(0, 20)));
            _corpseLeechRandomPitchSemitones = config.Bind("12. Audio", "CorpseLeechRandomPitchSemitones", 0.20f, new ConfigDescription("Random FMOD channel pitch variation in semitones. Zero disables.", new AcceptableValueRange<float>(0.0f, 12.0f)));

            _logStartup = config.Bind("13. Diagnostics", "LogStartup", true, "Log patch/load status.");
            _logAwards = config.Bind("13. Diagnostics", "LogAwards", false, "Log successful corpse and live-drain XP payouts.");
            _logRejectedCorpses = config.Bind("13. Diagnostics", "LogRejectedCorpses", false, "Log rejected or unresolved corpse ritual attempts.");
            _logUnresolvedRaycastHits = config.Bind("13. Diagnostics", "LogUnresolvedRaycastHits", false, "Log collider details when a corpse raycast hits something that cannot be matched to a usable corpse.");
            _logHealingResolution = config.Bind("13. Diagnostics", "LogHealingResolution", false, "Log the first hero health path used, or why healing could not resolve.");
            _logPatchWarnings = config.Bind("13. Diagnostics", "LogPatchWarnings", true, "Log warnings if optional patches or reflection paths are unavailable.");
            _logCorpseQuality = config.Bind("13. Diagnostics", "LogCorpseQuality", false, "Log throttled focused-corpse quality samples for reticle tuning.");
            _logBloodSpellInnerLight = config.Bind("13. Diagnostics", "LogBloodSpellInnerLight", true, "Log limited diagnostics for blood spell inner light readiness, per-hand cast boost, wrist resolution, interior state, and visibility transitions.");
            _corpseQualityLogIntervalSeconds = config.Bind("13. Diagnostics", "CorpseQualityLogIntervalSeconds", 1.0f, new ConfigDescription("Seconds between focused-corpse quality diagnostic logs.", new AcceptableValueRange<float>(0.1f, 10.0f)));
            _claimGrailFloatingTextCorpseXp = config.Bind("14. Integrations", "ClaimGrailFloatingTextCorpseXP", true, "When Grail Floating Text is loaded, show corpse-leech character XP as a red corpse-icon XP event instead of the generic XP event.");
            _claimGrailFloatingTextLiveDrainXp = config.Bind("14. Integrations", "ClaimGrailFloatingTextLiveDrainXP", true, "When Grail Floating Text is loaded, show live-drain character XP as a red magic-icon XP event instead of the generic XP event.");

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
                    + ", range="
                    + FormatFloat(_bloodSpellInnerLightRange.Value)
                    + ", fadeSeconds="
                    + FormatFloat(_bloodSpellInnerLightFadeSeconds.Value)
                    + ", diagnostics="
                    + _logBloodSpellInnerLight.Value
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
            config.Save();
        }

        private void UpdateBloodSpellInnerLight()
        {
            if (_bloodSpellInnerLightEnabled == null)
            {
                return;
            }

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

            ConfigureBloodSpellInnerLight(handState);

            float nextIntensity = targetIntensity;
            float fadeSeconds = GetBloodSpellInnerLightFadeSeconds();
            if (!immediateFadeOut && fadeSeconds > 0.0f)
            {
                float maxReference = Math.Max(1.0f, Math.Max(handState.Light.intensity, targetIntensity));
                float maxDelta = Time.unscaledDeltaTime * maxReference / fadeSeconds;
                nextIntensity = Mathf.MoveTowards(handState.Light.intensity, targetIntensity, maxDelta);
            }

            handState.Light.intensity = nextIntensity;
            ConfigureBloodSpellInnerLightHdrpData(handState, nextIntensity);

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
                !handState.SuppressForNonBloodEquipment &&
                HasBloodSpellInnerLightReadiedState(handState) &&
                GetBloodSpellInnerLightIntensity() > BloodSpellInnerLightMinimumIntensity;
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

            return brightness * boostFactor;
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
            }

            if (handState.LightObject.transform.parent != null)
            {
                handState.LightObject.transform.SetParent(null, true);
                LogBloodSpellInnerLightDiagnostic(
                    "detached "
                    + handState.Hand
                    + " light for world-space hand following.");
            }

            ConfigureBloodSpellInnerLight(handState);
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
            handState.Light.range = GetBloodSpellInnerLightRange();
            handState.Light.shadows = LightShadows.None;
            handState.Light.bounceIntensity = 0.0f;
            handState.Light.cullingMask = ~0;
            handState.Light.renderMode = LightRenderMode.Auto;
        }

        private void ConfigureBloodSpellInnerLightHdrpData(
            BloodSpellInnerLightHandState handState,
            float renderIntensity)
        {
            if (handState.Light == null || handState.LightObject == null)
            {
                return;
            }

            Type hdType = ResolveHdAdditionalLightDataType();
            if (hdType == null)
            {
                if (!_loggedBloodSpellInnerLightHdrpUnavailable)
                {
                    _loggedBloodSpellInnerLightHdrpUnavailable = true;
                    LogBloodSpellInnerLightDiagnostic(
                        "HDRP additional light data type was not found; using Unity Light fields only.");
                }

                return;
            }

            try
            {
                Component hdData = handState.Light.GetComponent(hdType);
                if (hdData == null)
                {
                    hdData = handState.LightObject.AddComponent(hdType);
                    LogBloodSpellInnerLightDiagnostic(
                        "added HDRP additional light data to "
                        + handState.Hand
                        + " Light object.");
                }

                TrySetFirstFloatMember(
                    hdData,
                    new[] { "intensity", "m_Intensity" },
                    renderIntensity);
                TrySetFirstFloatMember(
                    hdData,
                    new[] { "lightDimmer", "m_LightDimmer" },
                    1.0f);
                TrySetFirstFloatMember(
                    hdData,
                    new[] { "volumetricDimmer", "m_VolumetricDimmer" },
                    0.0f);
                TrySetFirstMemberValue(
                    hdData,
                    new[] { "affectDiffuse", "m_AffectDiffuse" },
                    true);
                TrySetFirstMemberValue(
                    hdData,
                    new[] { "affectSpecular", "m_AffectSpecular" },
                    true);
                DisableHdrpShadows(hdType, hdData);
            }
            catch (Exception exception)
            {
                LogBloodSpellInnerLightDiagnosticThrottled(
                    "HDRP additional light data setup failed: "
                    + exception.Message);
            }
        }

        private Type ResolveHdAdditionalLightDataType()
        {
            if (_hdAdditionalLightDataResolved)
            {
                return _hdAdditionalLightDataType;
            }

            _hdAdditionalLightDataResolved = true;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(
                    "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData",
                    false);
                if (type == null)
                {
                    continue;
                }

                _hdAdditionalLightDataType = type;
                return _hdAdditionalLightDataType;
            }

            return null;
        }

        private bool TrySetFirstFloatMember(
            object instance,
            string[] memberNames,
            float value)
        {
            for (int i = 0; i < memberNames.Length; i++)
            {
                if (TrySetFloatMember(instance, memberNames[i], value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TrySetFirstMemberValue(
            object instance,
            string[] memberNames,
            object value)
        {
            for (int i = 0; i < memberNames.Length; i++)
            {
                if (TrySetMemberValue(instance, memberNames[i], value))
                {
                    return true;
                }
            }

            return false;
        }

        private void DisableHdrpShadows(Type hdType, object hdData)
        {
            const BindingFlags Flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            MethodInfo enableShadows = hdType.GetMethod(
                "EnableShadows",
                Flags,
                null,
                new[] { typeof(bool) },
                null);
            if (enableShadows != null)
            {
                enableShadows.Invoke(hdData, new object[] { false });
            }

            TrySetFirstFloatMember(
                hdData,
                new[] { "shadowDimmer", "m_ShadowDimmer", "shadowIntensity" },
                0.0f);
            TrySetFirstFloatMember(
                hdData,
                new[] { "volumetricShadowDimmer", "m_VolumetricShadowDimmer" },
                0.0f);
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
            return _logBloodSpellInnerLight != null && _logBloodSpellInnerLight.Value;
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
            return _bloodSpellInnerLightRange == null
                ? 4.0f
                : Math.Max(0.1f, _bloodSpellInnerLightRange.Value);
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
                string settingId = currentSection + "\n" + settingName;

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
            return string.Equals(settingId, "2. Blood Spell Inner Light\nIntensity", StringComparison.Ordinal)
                || string.Equals(settingId, "2. Blood Spell Inner Light\nBloodTransfusionIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "2. Blood Spell Inner Light\nLifeTransfusionIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "2. Blood Spell Inner Light\nAbhartachCallingIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "2. Blood Spell Inner Light\nInteriorIntensityMultiplier", StringComparison.Ordinal)
                || string.Equals(settingId, "2. Blood Spell Inner Light\nRange", StringComparison.Ordinal)
                || string.Equals(settingId, "2. Blood Spell Inner Light\nFadeSeconds", StringComparison.Ordinal)
                || string.Equals(settingId, "12. Audio\nCorpseLeechSoundVolume", StringComparison.Ordinal)
                || string.Equals(settingId, "12. Audio\nCorpseLeechRandomPitchSemitones", StringComparison.Ordinal);
        }

        private static bool IsPreservedManualOverride(string settingId)
        {
            return string.Equals(settingId, "4. Bloodless Filter\nBloodWhitelistTerms", StringComparison.Ordinal)
                || string.Equals(settingId, "10. Advanced - Matching\nBloodSpellTemplateGuid", StringComparison.Ordinal)
                || string.Equals(settingId, "10. Advanced - Matching\nAbhartachTemplateGuid", StringComparison.Ordinal);
        }

        private void RestorePreservedConfigValues()
        {
            if (_pendingPreservedCalibrationFloats.Count == 0
                && _pendingPreservedManualOverrides.Count == 0
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreservedFloat("2. Blood Spell Inner Light\nIntensity", _bloodSpellInnerLightIntensity, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Blood Spell Inner Light\nBloodTransfusionIntensityMultiplier", _bloodSpellInnerLightBloodTransfusionIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Blood Spell Inner Light\nLifeTransfusionIntensityMultiplier", _bloodSpellInnerLightLifeTransfusionIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Blood Spell Inner Light\nAbhartachCallingIntensityMultiplier", _bloodSpellInnerLightAbhartachCallingIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Blood Spell Inner Light\nInteriorIntensityMultiplier", _bloodSpellInnerLightInteriorIntensityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Blood Spell Inner Light\nRange", _bloodSpellInnerLightRange, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Blood Spell Inner Light\nFadeSeconds", _bloodSpellInnerLightFadeSeconds, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("12. Audio\nCorpseLeechSoundVolume", _corpseLeechSoundVolume, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("12. Audio\nCorpseLeechRandomPitchSemitones", _corpseLeechRandomPitchSemitones, ref restoredCount, ref clampedCount);
            RestorePreservedString("4. Bloodless Filter\nBloodWhitelistTerms", _bloodWhitelistTerms, ref restoredCount);
            RestorePreservedString("10. Advanced - Matching\nBloodSpellTemplateGuid", _bloodTransfusionTemplateGuid, ref restoredCount);
            RestorePreservedString("10. Advanced - Matching\nAbhartachTemplateGuid", _abhartachTemplateGuid, ref restoredCount);

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

        private void ClearPendingPreservedConfigValues()
        {
            _pendingPreservedCalibrationFloats.Clear();
            _pendingPreservedManualOverrides.Clear();
            _pendingPreservedInvalidValueCount = 0;
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            PatchStep("HealthElement corpse aliases", delegate
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
                        false);
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
                        false);
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInSphereWithAdditionalCheckInstantaneous",
                        typeof(SphereDamageRangePatch),
                        nameof(SphereDamageRangePatch.Prefix),
                        nameof(SphereDamageRangePatch.Postfix),
                        false);
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInSphereOverTime",
                        typeof(SphereDamageRangePatch),
                        nameof(SphereDamageRangePatch.Prefix),
                        nameof(SphereDamageRangePatch.Postfix),
                        false);
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInSphereWithAdditionalCheckOverTime",
                        typeof(SphereDamageRangePatch),
                        nameof(SphereDamageRangePatch.Prefix),
                        nameof(SphereDamageRangePatch.Postfix),
                        false);
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeInstantaneous",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false);
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeWithAdditionalCheckInstantaneous",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false);
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeOverTime",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false);
                    PatchMethodsByName(
                        damageUtilsType,
                        "DealDamageInConeWithAdditionalCheckOverTime",
                        typeof(ConeDamageRangePatch),
                        nameof(ConeDamageRangePatch.Prefix),
                        nameof(ConeDamageRangePatch.Postfix),
                        false);
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

        private void PatchMethod(Type declaringType, string methodName, Type patchType, string patchMethodName, bool required)
        {
            MethodInfo original = AccessTools.Method(declaringType, methodName);
            MethodInfo patch = AccessTools.Method(patchType, patchMethodName);

            if (original == null || patch == null)
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
                if (patchMethodName == nameof(MagicFsmUpdatePatch.Prefix))
                {
                    _harmony.Patch(original, new HarmonyMethod(patch), null);
                }
                else
                {
                    _harmony.Patch(original, null, new HarmonyMethod(patch));
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

            if (_logStartup.Value)
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

            if (_logStartup.Value)
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
            bool required)
        {
            MethodInfo prefix = AccessTools.Method(patchType, prefixMethodName);
            MethodInfo postfix = AccessTools.Method(patchType, postfixMethodName);
            if (declaringType == null || prefix == null || postfix == null)
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
                    _harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
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

            if (_logStartup.Value)
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

            if (_logStartup.Value)
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

            if (_logStartup.Value)
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

            if (_logStartup.Value)
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
            RegisterCorpseAliases(corpse, state);
            RegisterCorpseAliases(npc, state);
            RegisterCorpseAliases(character, state);

            if (_logAwards.Value)
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
            state.LastRejectReason = "corpse was restored from save data";
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

            TouchCorpseState(state);

            string rejectReason;
            if (!IsBloodPlausible(state, out rejectReason))
            {
                RejectCorpse(state, rejectReason, true);
                return;
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
            if (!TryGetLookedAtCorpseState(
                out state,
                out unregisteredCorpseCandidate,
                true))
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
                RecordAbhartachCorpseQuality(state);
                return (int)BloodMagicFocusedCorpseState.Usable;
            }

            if (bloodTransfusionEquipped && IsCorpseDrainableForInterop(state))
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
            return TryGetLookedAtCorpseState(out state, true) && IsCorpseBloodMagicEligibleForInterop(state)
                ? GetCorpseQuality01(state)
                : 0f;
        }

        internal int GetFocusedCorpseQualityTierForInterop()
        {
            return GetCorpseQualityTier(GetFocusedCorpseQuality01ForInterop());
        }

        internal float GetFocusedCorpseQualityEffectMultiplierForInterop()
        {
            CorpseState state;
            if (_enabled == null || !_enabled.Value
                || !TryGetLookedAtCorpseState(out state, true)
                || !IsCorpseBloodMagicEligibleForInterop(state))
            {
                return 1f;
            }

            return GetCorpseQualityEffectMultiplier(GetCorpseQuality01(state));
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
            if (!IsCorpseStateUsable(state))
            {
                return false;
            }

            string rejectReason;
            if (!IsBloodPlausible(state, out rejectReason))
            {
                return false;
            }

            bool xpEnabled = _awardCharacterXp.Value && _rawCharacterXpPerCorpseXp.Value > 0f && !state.XpAwarded;
            bool healingEnabled = _healCharacter.Value && _healMaxHealthPercentPerXpPercent.Value > 0f && !state.Healed;
            if (!xpEnabled && !healingEnabled)
            {
                return false;
            }

            return !xpEnabled || ResolveCorpseEffectiveKillXp(state) > 0f;
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

            float payoutMultiplier = GetHandPayoutMultiplier(activeHandCount);
            float xpPercent = GetPayoutPercentOfKillXp() * payoutMultiplier;
            bool xpEnabled = _awardCharacterXp.Value && _rawCharacterXpPerCorpseXp.Value > 0f;
            bool healingEnabled = _healCharacter.Value && _healMaxHealthPercentPerXpPercent.Value > 0f;
            if (!xpEnabled && !healingEnabled)
            {
                RejectCorpse(state, "XP and healing rewards are both disabled", false);
                ResetFocusedCorpse();
                return;
            }

            string failures = "";
            float healBasePercent = xpPercent * Math.Max(0f, _healMaxHealthPercentPerXpPercent.Value);
            float healPowerScale = GetHealingPowerScale(state);
            float healQualityScale = GetTransfusionHealingQualityMultiplier(state);
            float healPercent = healBasePercent * healPowerScale * healQualityScale;
            if (healingEnabled && !state.Healed)
            {
                if (ApplyCorpseLeechHealing(state, healPercent))
                {
                    state.Healed = true;
                    if (_logAwards.Value)
                    {
                        Log.LogInfo("Healed " + healPercent.ToString("0.###", CultureInfo.InvariantCulture) + "% max HP from corpse #" + state.DebugId.ToString(CultureInfo.InvariantCulture) + " " + DescribeCorpse(state) + " (base " + healBasePercent.ToString("0.###", CultureInfo.InvariantCulture) + "%, power scale " + healPowerScale.ToString("0.###", CultureInfo.InvariantCulture) + "x, quality scale " + healQualityScale.ToString("0.###", CultureInfo.InvariantCulture) + "x).");
                    }
                }
                else
                {
                    failures = AppendFailure(failures, "healing failed");
                }
            }

            if (xpEnabled && !state.XpAwarded)
            {
                float baseXp = ResolveCorpseEffectiveKillXp(state);
                if (baseXp <= 0f)
                {
                    failures = AppendFailure(failures, "vanilla kill XP could not be resolved");
                }
                else
                {
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
                        float rawXp = amount * Math.Max(0f, _rawCharacterXpPerCorpseXp.Value);
                        TryClaimGrailFloatingTextCorpseXp(rawXp, state);
                        if (AwardRawCharacterXp(rawXp))
                        {
                            state.XpAwarded = true;
                            if (_logAwards.Value)
                            {
                                Log.LogInfo("Paid " + rawXp.ToString("0.###", CultureInfo.InvariantCulture) + " corpse leech XP from corpse #" + state.DebugId.ToString(CultureInfo.InvariantCulture) + " " + DescribeCorpse(state) + " (" + xpPercent.ToString("0.###", CultureInfo.InvariantCulture) + "% of " + baseXp.ToString("0.###", CultureInfo.InvariantCulture) + ").");
                            }
                        }
                        else
                        {
                            failures = AppendFailure(failures, "character XP award failed");
                        }
                    }
                }
            }

            bool xpComplete = !xpEnabled || state.XpAwarded;
            bool healingComplete = !healingEnabled || state.Healed;
            if (!xpComplete || !healingComplete)
            {
                RejectCorpse(state, string.IsNullOrEmpty(failures) ? "enabled reward did not complete" : failures, false);
                ResetFocusedCorpse();
                return;
            }

            float corpseSoundQuality = GetCorpseQuality01(state);
            state.Exhausted = true;
            state.ChannelStartTime = 0f;
            state.LastFocusTime = Now;
            TouchCorpseState(state);
            _focusedCorpse = null;

            PlayCorpseLeechSound(corpseSoundQuality);
            ReportCorpseDrained(corpseSoundQuality);
            state.LoggedReject = false;
        }

        private void ReportCorpseDrained(float quality)
        {
            if (!_deedsOfAvalonBridgeResolved)
            {
                _deedsOfAvalonBridgeResolved = true;
                PluginInfo pluginInfo;
                if (Chainloader.PluginInfos.TryGetValue(DeedsOfAvalonPluginGuid, out pluginInfo)
                    && pluginInfo != null
                    && pluginInfo.Instance != null)
                {
                    Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(DeedsOfAvalonApiTypeName, false);
                    _deedsOfAvalonRecordCorpseDrainMethod = apiType == null
                        ? null
                        : apiType.GetMethod("TryRecordCorpseDrain", BindingFlags.Public | BindingFlags.Static);
                }
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

        private void PlayCorpseLeechSound(float corpseQuality)
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

            float volume = Math.Max(0f, _corpseLeechSoundVolume == null ? 1f : _corpseLeechSoundVolume.Value);
            float pitch = GetCorpseLeechSoundPitchMultiplier();
            if (TryPlayFmodCorpseLeechSound(path, volume, pitch))
            {
                RememberRecentCorpseLeechSound(selectedTier, path);
            }
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
                FMOD.RESULT groupResult = RuntimeManager.CoreSystem.getMasterChannelGroup(out channelGroup);
                if (groupResult != FMOD.RESULT.OK)
                {
                    channelGroup = default(FMOD.ChannelGroup);
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
        }

        private void LogAudioDiagnostic(string message)
        {
            if (_logAwards != null && _logAwards.Value)
            {
                Log.LogInfo(message);
            }
        }

        private bool ApplyCorpseLeechHealing(CorpseState state, float percentOfMaxHealth)
        {
            if (percentOfMaxHealth <= 0f)
            {
                return false;
            }

            return HealHeroPercentOfMaxHealth(percentOfMaxHealth);
        }

        private void HandleAppliedDamage(object healthElement, object damage)
        {
            TryApplyLiveBloodMagicDrain(healthElement, damage);
        }

        private void TryApplyLiveBloodMagicDrain(object healthElement, object damage)
        {
            if (_enabled == null ||
                !_enabled.Value ||
                _liveDrainEnabled == null ||
                !_liveDrainEnabled.Value ||
                healthElement == null ||
                damage == null)
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
                if (_logAwards.Value)
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
                state.Exhausted ||
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

        internal void ApplyBloodMagicBuildupTuning(ref float buildupStrength, object statusTemplate, object sourceInfo)
        {
            if (buildupStrength <= 0f || statusTemplate == null || sourceInfo == null)
            {
                return;
            }

            if (!IsBleedBuildupStatus(statusTemplate))
            {
                return;
            }

            object sourceItem = GetPropertyValue(sourceInfo, "GetSourceItemSafe");
            string ignored;
            bool isBloodMagicSpell = ShouldTuneBloodSpells() && IsBloodTransfusionItem(sourceItem, out ignored);
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
            }
        }

        internal void ApplyBloodMagicProjectileDistanceTuning(object projectile, object weapon, object sourceProjectile)
        {
            ProjectileTuningState existing;
            if (!ShouldTuneBloodSpells() || projectile == null || _tunedProjectiles.TryGetValue(projectile, out existing))
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

            bool changed = false;
            if (_bloodSpellScaleProjectileTravel.Value)
            {
                float multiplier = GetBloodSpellRangeDistanceMultiplier();
                float lifeTime;
                if (multiplier > 0f &&
                    Math.Abs(multiplier - 1f) > 0.0001f &&
                    TryGetFloatMember(projectile, "LifeTime", out lifeTime) &&
                    lifeTime > 0f &&
                    TrySetFloatMember(projectile, "LifeTime", lifeTime * multiplier))
                {
                    changed = true;
                }
            }

            if (_bloodSpellScaleHomingTargetSearch.Value)
            {
                float multiplier = GetBloodSpellHomingTargetSearchMultiplier();
                float targetFindDistance;
                if (multiplier > 0f &&
                    Math.Abs(multiplier - 1f) > 0.0001f &&
                    TryGetFloatMember(projectile, "targetFindDistance", out targetFindDistance) &&
                    targetFindDistance > 0f &&
                    TrySetFloatMember(projectile, "targetFindDistance", targetFindDistance * multiplier))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                _tunedProjectiles.Add(projectile, new ProjectileTuningState());
            }
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
            bool isBloodMagicSpell = ShouldTuneBloodSpells() && IsBloodTransfusionItem(item, out ignored);
            bool isAbhartach = ShouldTuneAbhartach() && IsAbhartachItem(item, out ignored);
            if (!isBloodMagicSpell && !isAbhartach)
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
            bool isBloodMagicSpell = ShouldTuneBloodSpells() && IsBloodTransfusionItem(item, out ignored);
            bool isAbhartach = ShouldTuneAbhartach() && IsAbhartachItem(item, out ignored);
            if (isAbhartach)
            {
                if (!_abhartachScaleExplosionRadius.Value)
                {
                    return false;
                }

                multiplier = GetAbhartachExplosionRadiusMultiplier() * GetAbhartachCorpseQualityEffectMultiplier();
            }
            else if (isBloodMagicSpell)
            {
                multiplier = GetBloodSpellRangeDistanceMultiplier();
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

            float multiplier = GetAbhartachHeldCorpseHealingMultiplier() * GetAbhartachCorpseQualityEffectMultiplier();
            if (multiplier > 0f && Math.Abs(multiplier - 1f) > 0.0001f)
            {
                healing *= multiplier;
            }
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

            float referenceXp = Math.Max(
                1f,
                _corpseQualityReferenceKillXp == null
                    ? 300f
                    : _corpseQualityReferenceKillXp.Value);
            float referenceMaxHealth = Math.Max(
                1f,
                _corpseQualityReferenceMaxHealth == null
                    ? 600f
                    : _corpseQualityReferenceMaxHealth.Value);

            float baseXp = ResolveCorpseBaseXp(state);
            float xpQuality = baseXp > 0f
                ? Mathf.Clamp01(baseXp / referenceXp)
                : 0f;

            float targetMaxHealth = ResolveCorpseTargetMaxHealth(state);
            float healthQuality = targetMaxHealth > 0f
                ? Mathf.Clamp01(targetMaxHealth / referenceMaxHealth)
                : 0f;

            bool hasXpQuality = baseXp > 0f;
            bool hasHealthQuality = targetMaxHealth > 0f;
            float quality = 0f;
            if (hasXpQuality && hasHealthQuality)
            {
                quality = (xpQuality + healthQuality) * 0.5f;
            }
            else if (hasXpQuality)
            {
                quality = xpQuality;
            }
            else if (hasHealthQuality)
            {
                quality = healthQuality;
            }

            bool fallbackUsed = !hasXpQuality && !hasHealthQuality;
            if (fallbackUsed)
            {
                quality = Mathf.Clamp01(_corpseQualityFallbackQuality == null
                    ? 0f
                    : _corpseQualityFallbackQuality.Value);
            }

            LogCorpseQualitySample(
                state,
                baseXp,
                targetMaxHealth,
                referenceXp,
                referenceMaxHealth,
                xpQuality,
                healthQuality,
                quality,
                fallbackUsed);

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
            float finalQuality,
            bool fallbackUsed)
        {
            if (_logCorpseQuality == null || !_logCorpseQuality.Value)
            {
                return;
            }

            float now = Now;
            if (now < _nextCorpseQualityLogTime)
            {
                return;
            }

            float interval = _corpseQualityLogIntervalSeconds == null
                ? 1.0f
                : Math.Max(0.1f, _corpseQualityLogIntervalSeconds.Value);
            _nextCorpseQualityLogTime = now + interval;

            Log.LogInfo(
                "Corpse quality sample #" + state.DebugId.ToString(CultureInfo.InvariantCulture)
                + " " + DescribeCorpse(state)
                + ": baseXp=" + baseXp.ToString("0.###", CultureInfo.InvariantCulture)
                + "; targetMaxHealth=" + targetMaxHealth.ToString("0.###", CultureInfo.InvariantCulture)
                + "; referenceKillXP=" + referenceXp.ToString("0.###", CultureInfo.InvariantCulture)
                + "; referenceMaxHealth=" + referenceMaxHealth.ToString("0.###", CultureInfo.InvariantCulture)
                + "; xpQuality=" + xpQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; healthQuality=" + healthQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; finalQuality=" + finalQuality.ToString("0.###", CultureInfo.InvariantCulture)
                + "; fallbackUsed=" + fallbackUsed.ToString()
                + ".");
        }

        private int GetCorpseQualityTier(float quality)
        {
            quality = Mathf.Clamp01(quality);
            if (quality <= 0f)
            {
                return 0;
            }

            if (quality < 0.25f)
            {
                return 1;
            }

            if (quality < 0.5f)
            {
                return 2;
            }

            if (quality < 0.75f)
            {
                return 3;
            }

            return 4;
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

        internal float GetCorpseQualityEffectMultiplier(float quality)
        {
            float min = Mathf.Clamp(
                _corpseQualityMinimumEffectMultiplier == null
                    ? 0.5f
                    : _corpseQualityMinimumEffectMultiplier.Value,
                0f,
                10f);
            float max = Mathf.Clamp(
                _corpseQualityMaximumEffectMultiplier == null
                    ? 1.5f
                    : _corpseQualityMaximumEffectMultiplier.Value,
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
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 30.0f;
                case Preset.Desecration:
                    return 40.0f;
                case Preset.SoulFeast:
                    return 50.0f;
                default:
                    return Math.Max(0f, _customPayoutPercentOfKillXp.Value);
            }
        }

        private float GetSecondsRequired()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.0f;
                case Preset.Desecration:
                    return 1.5f;
                case Preset.SoulFeast:
                    return 2.0f;
                default:
                    return Math.Max(0.1f, _secondsRequired.Value);
            }
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
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.0f;
                case Preset.Desecration:
                    return 1.5f;
                case Preset.SoulFeast:
                    return 2.0f;
                default:
                    return Math.Max(0.1f, _customLiveDrainXpTickIntervalSeconds.Value);
            }
        }

        private float GetLiveDrainXpPercentPerTick()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 4.0f;
                case Preset.Desecration:
                    return 8.0f;
                case Preset.SoulFeast:
                    return 12.0f;
                default:
                    return Math.Max(0f, _customLiveDrainXpPercentPerTick.Value);
            }
        }

        private float GetLiveDrainMaximumXpPercentPerTarget()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 20.0f;
                case Preset.Desecration:
                    return 35.0f;
                case Preset.SoulFeast:
                    return 50.0f;
                default:
                    return Math.Max(0f, _customLiveDrainMaximumXpPercentPerTarget.Value);
            }
        }

        private float GetBloodSpellRangeDistanceMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetBloodSpellRangeBleedTapBaseMultiplier(), GetBloodSpellRangeBleedTapSpiritualityMultiplier());
        }

        private float GetBloodSpellHomingTargetSearchMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                GetBloodSpellHomingTargetSearchBaseMultiplier(),
                GetBloodSpellTargetSearchSpiritualityMultiplier(),
                _bloodSpellHomingTargetSearchMaximumMultiplier.Value);
        }

        private float GetBloodSpellHeldTargetRangeMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                GetBloodSpellHeldTargetRangeBaseMultiplier(),
                GetBloodSpellTargetSearchSpiritualityMultiplier(),
                _bloodSpellHeldTargetRangeMaximumMultiplier.Value);
        }

        private float GetBloodSpellBleedBuildupMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetBloodSpellBleedBuildupBaseMultiplier(), GetBloodSpellRangeBleedTapSpiritualityMultiplier());
        }

        private float GetBloodSpellTapCastSpeedMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetBloodSpellTapCastSpeedBaseMultiplier(), GetBloodSpellRangeBleedTapSpiritualityMultiplier());
        }

        private float GetBloodSpellHeldChannelSpeedMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetBloodSpellHeldChannelBaseMultiplier(), GetBloodSpellHeldSpiritualityMultiplier());
        }

        private float GetAbhartachExplosionDamageMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetAbhartachExplosionDamageBaseMultiplier(), GetAbhartachSpiritualityMultiplier(GetAbhartachExplosionDamageCurve()));
        }

        private float GetAbhartachExplosionRadiusMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetAbhartachExplosionRadiusBaseMultiplier(), GetAbhartachSpiritualityMultiplier(GetAbhartachExplosionRadiusCurve()));
        }

        private float GetAbhartachExplosionBleedBuildupMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetAbhartachExplosionBleedBaseMultiplier(), GetAbhartachSpiritualityMultiplier(GetAbhartachExplosionBleedCurve()));
        }

        private float GetAbhartachHeldCorpseHealingMultiplier()
        {
            return GetBloodSpellTunedMultiplier(GetAbhartachHeldHealingBaseMultiplier(), GetAbhartachSpiritualityMultiplier(GetAbhartachHeldHealingCurve()));
        }

        private float GetAbhartachCorpseSearchRangeMultiplier()
        {
            return GetBloodSpellTunedMultiplier(
                GetAbhartachCorpseSearchBaseMultiplier(),
                GetAbhartachSpiritualityMultiplier(GetAbhartachCorpseSearchCurve()),
                _abhartachCorpseSearchMaximumMultiplier.Value);
        }

        private float GetBloodSpellRangeBleedTapBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.03f;
                case Preset.Desecration:
                    return 1.06f;
                case Preset.SoulFeast:
                    return 1.12f;
                default:
                    return Math.Max(0f, _customBloodSpellRangeDistanceMultiplier.Value);
            }
        }

        private float GetBloodSpellHomingTargetSearchBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.0f;
                case Preset.Desecration:
                    return 1.05f;
                case Preset.SoulFeast:
                    return 1.12f;
                default:
                    return Math.Max(0f, _customBloodSpellHomingTargetSearchMultiplier.Value);
            }
        }

        private float GetBloodSpellHeldTargetRangeBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.0f;
                case Preset.Desecration:
                    return 1.03f;
                case Preset.SoulFeast:
                    return 1.08f;
                default:
                    return Math.Max(0f, _customBloodSpellHeldTargetRangeMultiplier.Value);
            }
        }

        private float GetBloodSpellBleedBuildupBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.03f;
                case Preset.Desecration:
                    return 1.06f;
                case Preset.SoulFeast:
                    return 1.12f;
                default:
                    return Math.Max(0f, _customBloodSpellBleedBuildupMultiplier.Value);
            }
        }

        private float GetBloodSpellTapCastSpeedBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.03f;
                case Preset.Desecration:
                    return 1.06f;
                case Preset.SoulFeast:
                    return 1.12f;
                default:
                    return Math.Max(0f, _customBloodSpellTapCastSpeedMultiplier.Value);
            }
        }

        private float GetBloodSpellHeldChannelBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.0f;
                case Preset.Desecration:
                    return 1.01f;
                case Preset.SoulFeast:
                    return 1.02f;
                default:
                    return Math.Max(0f, _customBloodSpellHeldChannelSpeedMultiplier.Value);
            }
        }

        private float GetAbhartachExplosionDamageBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.0f;
                case Preset.Desecration:
                    return 1.05f;
                case Preset.SoulFeast:
                    return 1.12f;
                default:
                    return Math.Max(0f, _customAbhartachExplosionDamageMultiplier.Value);
            }
        }

        private float GetAbhartachExplosionRadiusBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.05f;
                case Preset.Desecration:
                    return 1.10f;
                case Preset.SoulFeast:
                    return 1.20f;
                default:
                    return Math.Max(0f, _customAbhartachExplosionRadiusMultiplier.Value);
            }
        }

        private float GetAbhartachExplosionBleedBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.05f;
                case Preset.Desecration:
                    return 1.12f;
                case Preset.SoulFeast:
                    return 1.20f;
                default:
                    return Math.Max(0f, _customAbhartachExplosionBleedBuildupMultiplier.Value);
            }
        }

        private float GetAbhartachHeldHealingBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.10f;
                case Preset.Desecration:
                    return 1.20f;
                case Preset.SoulFeast:
                    return 1.35f;
                default:
                    return Math.Max(0f, _customAbhartachHeldCorpseHealingMultiplier.Value);
            }
        }

        private float GetAbhartachCorpseSearchBaseMultiplier()
        {
            switch (_preset.Value)
            {
                case Preset.BloodRite:
                    return 1.0f;
                case Preset.Desecration:
                    return 1.05f;
                case Preset.SoulFeast:
                    return 1.10f;
                default:
                    return Math.Max(0f, _customAbhartachCorpseSearchRangeMultiplier.Value);
            }
        }

        private float GetBloodSpellRangeBleedTapSpiritualityMultiplier()
        {
            return GetBloodSpellSpiritualityMultiplier(GetRangeBleedTapSpiritualityCurve());
        }

        private float GetBloodSpellTargetSearchSpiritualityMultiplier()
        {
            return GetBloodSpellSpiritualityMultiplier(GetTargetSearchSpiritualityCurve());
        }

        private float GetBloodSpellHeldSpiritualityMultiplier()
        {
            return GetBloodSpellSpiritualityMultiplier(GetHeldSpiritualityCurve());
        }

        private float GetBloodSpellSpiritualityMultiplier(CurvePoint[] curve)
        {
            if (_bloodSpellSpiritualityScalingEnabled == null || !_bloodSpellSpiritualityScalingEnabled.Value)
            {
                return 1f;
            }

            float spirituality = GetCachedHeroSpiritualityValue();
            if (spirituality <= 0f)
            {
                return 1f;
            }

            float bonusPercent = EvaluateCurve(curve, spirituality, 0f);
            return 1f + (Math.Max(0f, bonusPercent) / 100f);
        }

        private float GetAbhartachSpiritualityMultiplier(CurvePoint[] curve)
        {
            return GetBloodSpellSpiritualityMultiplier(curve);
        }

        private float GetBloodSpellTunedMultiplier(float presetBase, float spiritualityMultiplier)
        {
            return GetBloodSpellTunedMultiplier(presetBase, spiritualityMultiplier, 10f);
        }

        private float GetBloodSpellTunedMultiplier(float presetBase, float spiritualityMultiplier, float maximum)
        {
            float value = Math.Max(0f, presetBase) * Math.Max(0f, spiritualityMultiplier);
            return Mathf.Clamp(value, 0f, Math.Max(0f, maximum));
        }

        private CurvePoint[] GetRangeBleedTapSpiritualityCurve()
        {
            string raw = _bloodSpellRangeBleedTapSpiritualityBonusCurve == null
                ? ""
                : (_bloodSpellRangeBleedTapSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedRangeBleedTapSpiritualityCurveRaw)
            {
                _cachedRangeBleedTapSpiritualityCurveRaw = raw;
                _cachedRangeBleedTapSpiritualityCurve = ParseCurve(raw, GetDefaultRangeBleedTapSpiritualityCurve());
            }

            return _cachedRangeBleedTapSpiritualityCurve;
        }

        private CurvePoint[] GetTargetSearchSpiritualityCurve()
        {
            string raw = _bloodSpellTargetSearchSpiritualityBonusCurve == null
                ? ""
                : (_bloodSpellTargetSearchSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedTargetSearchSpiritualityCurveRaw)
            {
                _cachedTargetSearchSpiritualityCurveRaw = raw;
                _cachedTargetSearchSpiritualityCurve = ParseCurve(raw, GetDefaultTargetSearchSpiritualityCurve());
            }

            return _cachedTargetSearchSpiritualityCurve;
        }

        private CurvePoint[] GetHeldSpiritualityCurve()
        {
            string raw = _bloodSpellHeldSpiritualityBonusCurve == null
                ? ""
                : (_bloodSpellHeldSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedHeldSpiritualityCurveRaw)
            {
                _cachedHeldSpiritualityCurveRaw = raw;
                _cachedHeldSpiritualityCurve = ParseCurve(raw, GetDefaultHeldSpiritualityCurve());
            }

            return _cachedHeldSpiritualityCurve;
        }

        private CurvePoint[] GetAbhartachExplosionDamageCurve()
        {
            string raw = _abhartachExplosionDamageSpiritualityBonusCurve == null
                ? ""
                : (_abhartachExplosionDamageSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachExplosionDamageCurveRaw)
            {
                _cachedAbhartachExplosionDamageCurveRaw = raw;
                _cachedAbhartachExplosionDamageCurve = ParseCurve(raw, GetDefaultAbhartachExplosionDamageCurve());
            }

            return _cachedAbhartachExplosionDamageCurve;
        }

        private CurvePoint[] GetAbhartachExplosionRadiusCurve()
        {
            string raw = _abhartachExplosionRadiusSpiritualityBonusCurve == null
                ? ""
                : (_abhartachExplosionRadiusSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachExplosionRadiusCurveRaw)
            {
                _cachedAbhartachExplosionRadiusCurveRaw = raw;
                _cachedAbhartachExplosionRadiusCurve = ParseCurve(raw, GetDefaultAbhartachExplosionRadiusCurve());
            }

            return _cachedAbhartachExplosionRadiusCurve;
        }

        private CurvePoint[] GetAbhartachExplosionBleedCurve()
        {
            string raw = _abhartachExplosionBleedSpiritualityBonusCurve == null
                ? ""
                : (_abhartachExplosionBleedSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachExplosionBleedCurveRaw)
            {
                _cachedAbhartachExplosionBleedCurveRaw = raw;
                _cachedAbhartachExplosionBleedCurve = ParseCurve(raw, GetDefaultAbhartachExplosionDamageCurve());
            }

            return _cachedAbhartachExplosionBleedCurve;
        }

        private CurvePoint[] GetAbhartachHeldHealingCurve()
        {
            string raw = _abhartachHeldHealingSpiritualityBonusCurve == null
                ? ""
                : (_abhartachHeldHealingSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachHeldHealingCurveRaw)
            {
                _cachedAbhartachHeldHealingCurveRaw = raw;
                _cachedAbhartachHeldHealingCurve = ParseCurve(raw, GetDefaultAbhartachHeldHealingCurve());
            }

            return _cachedAbhartachHeldHealingCurve;
        }

        private CurvePoint[] GetAbhartachCorpseSearchCurve()
        {
            string raw = _abhartachCorpseSearchSpiritualityBonusCurve == null
                ? ""
                : (_abhartachCorpseSearchSpiritualityBonusCurve.Value ?? "");
            if (raw != _cachedAbhartachCorpseSearchCurveRaw)
            {
                _cachedAbhartachCorpseSearchCurveRaw = raw;
                _cachedAbhartachCorpseSearchCurve = ParseCurve(raw, GetDefaultTargetSearchSpiritualityCurve());
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

        private CurvePoint[] GetDefaultRangeBleedTapSpiritualityCurve()
        {
            return new[]
            {
                new CurvePoint(0f, 0f),
                new CurvePoint(5f, 2f),
                new CurvePoint(10f, 5f),
                new CurvePoint(15f, 10f),
                new CurvePoint(20f, 17f),
                new CurvePoint(25f, 25f),
                new CurvePoint(30f, 35f),
                new CurvePoint(35f, 47f),
                new CurvePoint(40f, 60f),
                new CurvePoint(45f, 75f),
                new CurvePoint(50f, 90f)
            };
        }

        private CurvePoint[] GetDefaultTargetSearchSpiritualityCurve()
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

        private CurvePoint[] GetDefaultHeldSpiritualityCurve()
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

        private bool TryClaimGrailFloatingTextCorpseXp(float amount, CorpseState state)
        {
            if (amount <= 0f ||
                _claimGrailFloatingTextCorpseXp == null ||
                !_claimGrailFloatingTextCorpseXp.Value)
            {
                return false;
            }

            string qualityLabel = GetCorpseQualityLabel(GetCorpseQuality01(state));
            return TryClaimGrailFloatingTextXp(
                amount,
                GrailFloatingTextCorpseXpEventId,
                "corpse-xp-" + qualityLabel.ToLowerInvariant(),
                "+" + amount.ToString("F0", CultureInfo.InvariantCulture) + " XP (" + qualityLabel + ")",
                "+{xp} XP (" + qualityLabel + ")",
                "corpse");
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
                "magic");
        }

        private bool TryClaimGrailFloatingTextXp(
            float amount,
            string eventId,
            string consolidationKey,
            string text,
            string textFormat,
            string iconId)
        {
            if (!TryResolveGrailFloatingTextBridge())
            {
                return false;
            }

            try
            {
                if (_grailFloatingTextTryClaimConsolidatedXpGainMethod != null)
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
                    _grailFloatingTextTryClaimXpGainMethod != null;
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

            if (_grailFloatingTextTryClaimConsolidatedXpGainMethod == null &&
                _grailFloatingTextTryClaimXpGainMethod == null)
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text is loaded, but it does not support XP gain text claims.");
            }

            return _grailFloatingTextTryClaimConsolidatedXpGainMethod != null ||
                _grailFloatingTextTryClaimXpGainMethod != null;
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

            if (_logRejectedCorpses.Value && !state.LoggedReject)
            {
                state.LoggedReject = true;
                Log.LogInfo("Rejected corpse leech target #" + state.DebugId.ToString(CultureInfo.InvariantCulture) + " " + DescribeCorpse(state) + ": " + reason + ".");
            }
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

        private bool TryGetLookedAtCorpseState(
            out CorpseState state,
            out bool unregisteredCorpseCandidate,
            bool includeInactive = false)
        {
            state = null;
            unregisteredCorpseCandidate = false;
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            RaycastHit hit;
            float range = Math.Max(0.1f, _range.Value);
            int layerMask = _raycastLayerMask.Value;
            if (!Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, range, layerMask, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            if (ColliderLooksAlive(hit.collider))
            {
                return false;
            }

            if (TryResolveCorpseStateFromCollider(hit.collider, out state, includeInactive))
            {
                return true;
            }

            if (!IsCorpseFallbackCandidateCollider(hit.collider))
            {
                LogUnresolvedRaycastHit(hit.collider);
                return false;
            }

            if (TryResolveCorpseStateFromAllRaycastHits(camera, range, layerMask, out state, includeInactive))
            {
                return true;
            }

            if (TryRefreshCorpseAliasesAfterUnresolvedHit(hit.collider, includeInactive))
            {
                if (TryResolveCorpseStateFromCollider(hit.collider, out state, includeInactive) ||
                    TryResolveCorpseStateFromAllRaycastHits(camera, range, layerMask, out state, includeInactive))
                {
                    return true;
                }
            }

            LogUnresolvedRaycastHit(hit.collider);
            unregisteredCorpseCandidate = true;
            return false;
        }

        private bool TryResolveCorpseStateFromCollider(Collider collider, out CorpseState state, bool includeInactive = false)
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

            if (IsCorpseFallbackCandidateCollider(collider) && TryResolveNearestCorpseState(collider.transform.position, out state, includeInactive))
            {
                CacheResolvedCollider(collider, state);
                return true;
            }

            return false;
        }

        private bool TryResolveCorpseStateFromAllRaycastHits(Camera camera, float range, int layerMask, out CorpseState state, bool includeInactive = false)
        {
            state = null;
            int maxHits = _raycastAllFallbackMaxHits == null ? 0 : Math.Max(0, _raycastAllFallbackMaxHits.Value);
            if (camera == null || maxHits <= 0)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, range, layerMask, QueryTriggerInteraction.Collide);
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

            HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            bool alive;
            if (TryReadAliveState(collider, 0, seen, out alive))
            {
                return alive;
            }

            Transform transform = collider.transform;
            int depth = 0;
            int maxDepth = Math.Max(1, _raycastParentSearchDepth.Value);
            while (transform != null && depth < maxDepth)
            {
                if (TryReadAliveState(transform.gameObject, 0, seen, out alive) ||
                    TryReadAliveState(transform, 0, seen, out alive))
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
            if (_logUnresolvedRaycastHits == null || !_logUnresolvedRaycastHits.Value || collider == null)
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
            if (_logHealingResolution == null || !_logHealingResolution.Value || _loggedHealingResolution)
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
            _equippedItemMethod = GetMethodSilent(inventoryExtensionsType, "EquippedItem", 2);
            if (_getHeroItemsMethod == null || _allEquipmentSlotsField == null || _equippedItemMethod == null)
            {
                Warn("Could not resolve Blood Magic Expansion spell equipped-state reflection members.");
                return false;
            }

            return true;
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

        private string[] GetSpiritualityStatTerms()
        {
            string raw = _bloodSpellSpiritualityStatTerms == null ? "" : (_bloodSpellSpiritualityStatTerms.Value ?? "");
            if (raw != _cachedSpiritualityTermsRaw)
            {
                _cachedSpiritualityTermsRaw = raw;
                _cachedSpiritualityTerms = SplitTerms(raw);
                _nextHeroSpiritualityRefreshTime = 0f;
            }

            return _cachedSpiritualityTerms;
        }

        private float GetCachedHeroSpiritualityValue()
        {
            float now = Now;
            if (now < _nextHeroSpiritualityRefreshTime)
            {
                return _cachedHeroSpiritualityValue;
            }

            _nextHeroSpiritualityRefreshTime = now + 1f;
            float value;
            _cachedHeroSpiritualityValue = TryResolveHeroSpiritualityValue(out value) ? Math.Max(0f, value) : 0f;
            return _cachedHeroSpiritualityValue;
        }

        private bool TryResolveHeroSpiritualityValue(out float value)
        {
            value = 0f;
            object hero = GetHero();
            if (hero == null)
            {
                return false;
            }

            string[] terms = GetSpiritualityStatTerms();
            if (terms.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                object direct = GetMemberValue(hero, terms[i]);
                float directValue = ReadStatValue(direct);
                if (directValue >= 0f)
                {
                    value = directValue;
                    return true;
                }
            }

            HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return TryFindNamedStatValue(hero, terms, 0, seen, out value);
        }

        private bool TryFindNamedStatValue(object obj, string[] terms, int depth, HashSet<object> seen, out float value)
        {
            value = 0f;
            if (obj == null || terms == null || terms.Length == 0 || depth > 4 || IsSimpleStatSearchTerminal(obj))
            {
                return false;
            }

            if (seen.Contains(obj))
            {
                return false;
            }
            seen.Add(obj);

            string matched;
            if (ContainsAnyTerm(BuildObjectSearchText(obj), terms, out matched))
            {
                float direct = ReadStatValue(obj);
                if (direct >= 0f)
                {
                    value = direct;
                    return true;
                }
            }

            IEnumerable enumerable = obj as IEnumerable;
            if (enumerable != null && !(obj is string) && depth > 0)
            {
                int checkedItems = 0;
                foreach (object item in enumerable)
                {
                    if (checkedItems++ >= 80)
                    {
                        break;
                    }

                    if (TryFindNamedStatValue(item, terms, depth + 1, seen, out value))
                    {
                        return true;
                    }
                }
            }

            Type type = obj.GetType();
            MemberInfo[] members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < members.Length; i++)
            {
                MemberInfo member = members[i];
                bool nameMatches = MemberNameMatchesAnyTerm(member.Name, terms);
                if (!nameMatches && !MemberNameLooksLikeStatContainer(member.Name))
                {
                    continue;
                }

                object candidate = ReadStatSearchMember(obj, member);
                if (candidate == null)
                {
                    continue;
                }

                if (nameMatches)
                {
                    float direct = ReadStatValue(candidate);
                    if (direct >= 0f)
                    {
                        value = direct;
                        return true;
                    }
                }

                if (TryFindNamedStatValue(candidate, terms, depth + 1, seen, out value))
                {
                    return true;
                }
            }

            return false;
        }

        private object ReadStatSearchMember(object instance, MemberInfo member)
        {
            try
            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    if (property.GetIndexParameters().Length != 0)
                    {
                        return null;
                    }

                    return property.GetValue(instance, null);
                }

                FieldInfo field = member as FieldInfo;
                return field == null ? null : field.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        private bool MemberNameMatchesAnyTerm(string name, string[] terms)
        {
            string normalizedName = NormalizeStatSearchKey(name);
            for (int i = 0; i < terms.Length; i++)
            {
                string normalizedTerm = NormalizeStatSearchKey(terms[i]);
                if (normalizedTerm.Length > 0 && normalizedName.IndexOf(normalizedTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool MemberNameLooksLikeStatContainer(string name)
        {
            string normalized = NormalizeStatSearchKey(name);
            return normalized.IndexOf("stat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("attribute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("rpg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("proficien", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("development", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("magic", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string NormalizeStatSearchKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }

            return builder.ToString();
        }

        private bool IsSimpleStatSearchTerminal(object obj)
        {
            if (obj == null || obj is string)
            {
                return true;
            }

            Type type = obj.GetType();
            return type.IsPrimitive || type.IsEnum || type == typeof(decimal);
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

        private bool ShouldLogStartup()
        {
            return _logStartup == null || _logStartup.Value;
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
            if (_logPatchWarnings == null || _logPatchWarnings.Value)
            {
                Log.LogWarning(message);
            }
        }

        private enum Preset
        {
            Custom,
            BloodRite,
            Desecration,
            SoulFeast,
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
            public bool LoggedReject;
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

        private sealed class ProjectileTuningState
        {
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
            public static void Prefix(ref float buildupStrength, object statusTemplate, object sourceInfo)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyBloodMagicBuildupTuning(ref buildupStrength, statusTemplate, sourceInfo);
                }
            }
        }

        private static class SphereDamageRangePatch
        {
            public static void Prefix(object[] __args)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyBloodMagicSphereAreaTuning(__args);
                }
            }

            public static void Postfix()
            {
            }
        }

        private static class ConeDamageRangePatch
        {
            public static void Prefix(object[] __args)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyBloodMagicConeAreaTuning(__args);
                }
            }

            public static void Postfix()
            {
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

        private static class HealingUtilsTakeHealingPatch
        {
            public static void Prefix(object character, ref float healing, object healingItem)
            {
                BloodMagicExpansionPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyAbhartachHeldCorpseHealingTuning(character, ref healing, healingItem);
                }
            }
        }
    }

    public static class BloodMagicApi
    {
        public const int ApiVersion = 4;

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
    }
}
