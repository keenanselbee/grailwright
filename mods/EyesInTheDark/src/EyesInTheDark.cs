using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Graphics.Transitions;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.AI.Fights.Projectiles;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Crafting.Fireplace;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.General.Configs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet.QuickUseWheels;
using Awaken.TG.Main.Heroes.Development;
using Awaken.TG.Main.Heroes.HUD;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Resting;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Actions;
using Awaken.TG.Main.Saving;
using Awaken.TG.Main.Saving.SaveSlots;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.Timing;
using Awaken.TG.Main.UI.TitleScreen;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using Awaken.TG.Main.UI.Components;
using Awaken.TG.Main.Wyrdnessing;
using Awaken.Utility;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;

[assembly: AssemblyTitle("Eyes in the Dark - Wyrdnight Overhaul")]
[assembly: AssemblyDescription("A timescale-aware Wyrdnight threat and encounter overhaul")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Eyes in the Dark - Wyrdnight Overhaul")]
[assembly: AssemblyVersion("1.2.2.0")]
[assembly: AssemblyFileVersion("1.2.2.0")]
[assembly: AssemblyInformationalVersion("1.2.2")]

namespace EyesInTheDark
{
    internal sealed class FoASettingUiMetadata
    {
        public string DisplaySection { get; set; }
        public string DisplayName { get; set; }
        public string ChoiceLabels { get; set; }
        public int SectionOrder { get; set; }
        public int Order { get; set; }
        public bool Hidden { get; set; }
    }

    public static class EyesInTheDarkHudApi
    {
        public static int ContractVersion
        {
            get { return 1; }
        }

        public static bool RequestBelowVanillaBars(
            string requesterPluginGuid,
            bool enabled)
        {
            EyesInTheDarkPlugin plugin =
                EyesInTheDarkPlugin.Instance;
            return plugin != null
                && plugin.SetExternalMeterPlacement(
                    requesterPluginGuid,
                    enabled);
        }
    }

    public static class EyesInTheDarkBattlecryApi
    {
        public static int ContractVersion
        {
            get { return 1; }
        }

        public static bool TryRegisterBattlecry(float threatAmount)
        {
            EyesInTheDarkPlugin plugin =
                EyesInTheDarkPlugin.Instance;
            return plugin != null
                && plugin.TryRegisterBattlecry(threatAmount);
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class EyesInTheDarkPlugin : BaseUnityPlugin, IListenerOwner
    {
        public const string PluginGuid = "ks.tgfoa.eyes-in-the-dark";
        public const string PluginName = "Eyes in the Dark";
        public const string PluginVersion = "1.2.2";
        private static readonly FieldInfo FireplaceRestControlField =
            AccessTools.Field(typeof(VFireplaceUI), "goToSleep");
        private static readonly PropertyInfo FireplaceRestButtonProperty =
            FireplaceRestControlField == null
                ? null
                : AccessTools.Property(
                    FireplaceRestControlField.FieldType,
                    "Button");
        private static readonly FieldInfo QuickWeatherTimeTextField =
            AccessTools.Field(
                typeof(VCQuickWeatherTime),
                "gameWeatherTimeText");
        private const string GloriousUiPluginGuid =
            "ks.tgfoa.glorious-ui";

        private const int ConfigSchemaVersion = 15;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new[]
                {
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        5,
                        "7. Threat Meter",
                        "MeterOffsetX",
                        "Standalone placement now provides the former horizontal calibration as an internal baseline."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        5,
                        "7. Threat Meter",
                        "MeterOffsetY",
                        "Standalone placement now provides the former vertical calibration as an internal baseline."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        7,
                        "8. Wyrd Boundary",
                        "BoundaryPulseAmount",
                        "The new visual baseline deliberately replaces prior customized pulse amounts with the tested 0.8 default."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        7,
                        "10. Diagnostics",
                        "Diagnostics",
                        "The new visual baseline deliberately returns diagnostics to its safe off default after regeneration.")
                };
        private static readonly ConfigDefinition[]
            ConfigRecoveryPermanentExclusions =
                new[]
                {
                    new ConfigDefinition(
                        "2. Gameplay Preset",
                        "ApplyPreset"),
                    new ConfigDefinition(
                        "10. Diagnostics",
                        "EnableThreatOverride"),
                    new ConfigDefinition(
                        "10. Diagnostics",
                        "ThreatOverrideValue"),
                    new ConfigDefinition(
                        "10. Diagnostics",
                        "EnableTimescaleOverride"),
                    new ConfigDefinition(
                        "10. Diagnostics",
                        "TimescaleOverrideMultiplier")
                };

        private const float StatePollIntervalSeconds = 0.2f;
        private const float MaximumClockStepSeconds = 0.5f;
        private const float FastSwimMinimumSpeed = 3.0f;
        private const float MeaningfulDamageMinimum = 1.0f;
        private const float ListenerRetryBackoffSeconds = 30.0f;
        private const float ContinuousThreatDiagnosticIntervalSeconds =
            10.0f;
        private const int MinimumBattlecriesPerResponse = 2;
        private const int MaximumBattlecriesPerResponse = 3;
        private const float BattlecryThreatResetSeconds = 30.0f;
        private const float MinimumBattlecryThreatMultiplier = 0.1f;
        private const float LoadThreatVisualTransitionSeconds = 10.0f;

        private const float DefaultDayMinutes = 60f;
        private const float DefaultBaseNightMinutes = 6f;
        private const float DefaultMaximumThreatNightMinutes = 12f;
        private const float DefaultRestInterruptionChanceAtZeroThreat = 45f;
        private const float DefaultRestInterruptionChanceAtMaximumThreat = 75f;

        private const float DefaultPassiveThreatPerNight = 20.0f;
        private const float DefaultSprintThreatPerMinute = 4.0f;
        private const float DefaultCombatThreatPerWindow = 2.0f;
        private const float DefaultCombatResponseSeconds = 1.5f;
        private const float DefaultWyrdKillThreat = 5.0f;
        private const float DefaultAcquisitionThreatPerItem = 0.75f;
        private const float DefaultProtectedDecayPerMinute = 4.0f;
        private const float DefaultInteriorDecayPerMinute = 1.0f;
        private const float DefaultLoadReconstructionAtDawn = 8.0f;
        private const float DefaultGraceSeconds = 15.0f;
        private const float DefaultBaseDangerBudget = 30.0f;
        private const float DefaultLongNightBonusScale = 0.35f;
        private const float DefaultMaximumLongNightBonus = 0.75f;
        private const float DefaultBaseHazardPerMinute = 0.01f;
        private const float DefaultThreatHazardPerMinute = 0.42f;
        private const float DefaultNightProgressHazardPerMinute = 0.08f;
        private const float DefaultMinimumHazardTarget = 0.85f;
        private const float DefaultMaximumHazardTarget = 1.15f;
        private const float DefaultWarningSeconds = 6.0f;
        private const float DefaultDangerCostMultiplier = 1.0f;
        private const int DefaultMaximumPackSize = 2;
        private const float DefaultSidecarChance = 0.55f;
        private const float DefaultHunterSpawnDistance = 35.0f;
        private const float DefaultEscapeDistance = 80.0f;
        private const float DefaultEscapeSustainSeconds = 10.0f;
        private const float DefaultKillThreatRelief = 35.0f;
        private const float DefaultEscapeThreatRelief = 15.0f;
        private const float DefaultKillRecoverySeconds = 90.0f;
        private const float DefaultEscapeRecoverySeconds = 180.0f;
        private const float DefaultFailedPlacementRecoverySeconds = 30.0f;
        private const float DefaultStalkerMinimumCooldownSeconds = 55.0f;
        private const float DefaultStalkerMaximumCooldownSeconds = 165.0f;
        private const float DefaultStalkerMaximumCooldownAtFiftyThreatSeconds =
            70.0f;
        private const float DefaultStalkerProvocationThreat = 6.0f;
        private const float DefaultStalkerMinimumSpawnDistance = 45.0f;
        private const float DefaultStalkerMaximumSpawnDistance = 70.0f;
        private const float DefaultStalkerPassiveDespawnDistance = 65.0f;
        private const float DefaultStalkerOffCameraDespawnSeconds = 2.5f;
        private const float StandaloneMeterBaselineOffsetX = 9.0f;
        private const float StandaloneMeterBaselineOffsetY = -9.0f;
        private const string DefaultBoundaryColor = "#B878FF";
        private const float BoundaryVanillaHdrBaseline = 271.529f;
        private const float DefaultBoundaryBrightness = 1.0f;
        private const float DefaultBoundaryNearRadius = 10.0f;
        private const float DefaultBoundaryNearIntensity = 0.05f;
        private const float DefaultBoundaryNearThickness = 0.25f;
        private const float DefaultBoundaryMiddleRadius = 20.0f;
        private const float DefaultBoundaryMiddleIntensity = 0.05f;
        private const float DefaultBoundaryMiddleThickness = 0.25f;
        private const float DefaultBoundaryOuterRadius = 30.0f;
        private const float DefaultBoundaryOuterIntensity = 0.05f;
        private const float DefaultBoundaryOuterThickness = 0.25f;
        private const float DefaultBoundaryPulseAmount = 0.8f;
        private const float DefaultBoundaryPulseMinimumSeconds = 2.5f;
        private const float DefaultBoundaryPulseMaximumSeconds = 6.0f;
        private const float DefaultGftCooldownSeconds = 8.0f;
        private const float DefaultBattlecryResponseCooldownSeconds = 15.0f;
        private const float DefaultDiagnosticGftCooldownSeconds = 1.0f;
        private const float DefaultMinimumThreatVisualScale = 0.8f;
        private const float DefaultMaximumThreatVisualScale = 1.2f;
        private const float DefaultPurpleExposureMultiplier = 1.2f;
        private const float DefaultPurpleExposureCompensation = 0.35f;
        private const float DefaultPurpleIndirectDiffuseMultiplier = 1.10f;
        private const float DefaultThreatVisualSmoothingSeconds = 2.0f;
        private const string DefaultThreatRedColor = "#FF3028";
        private const float DefaultMaximumThreatRedBlend = 0.8f;
        private const string DefaultMoonSurfaceColor = "#3200FF";
        private const float DefaultMoonSurfaceTintStrength = 0.75f;
        private const float DefaultMoonSurfaceIntensity = 2.0f;
        private const string DefaultMoonCoronaColor = "#8000FF";
        private const float DefaultMoonCoronaIntensity = 2.0f;
        private const string DefaultMoonlightColor = "#7E47FF";
        private const float DefaultMoonlightTintStrength = 0.9f;
        private const string DefaultNightSkyAmbientColor = "#401C63";
        private const float DefaultNightSkyAmbientTintStrength = 1.0f;
        private const string DefaultProtectionBubbleColor = "#B050FF";
        private const float DefaultProtectionBubbleIntensity = 1.0f;
        private const float DefaultProtectionBubbleBorderIntensity = 1.0f;
        private const float DefaultWyrdVisualTransitionSeconds = 60.0f;

        internal static EyesInTheDarkPlugin Instance { get; private set; }

        private readonly ActiveRealTimeClock _activeRealTimeClock =
            new ActiveRealTimeClock(MaximumClockStepSeconds);
        private readonly ThreatState _threat = new ThreatState();
        private readonly ThreatActivityLimiter _activity =
            new ThreatActivityLimiter();
        private readonly NightPacingState _pacing =
            new NightPacingState();
        private readonly AtmosphereTextPools _atmosphereTexts =
            new AtmosphereTextPools(Environment.TickCount);
        private readonly NotificationCooldowns _notificationCooldowns =
            new NotificationCooldowns();
        private readonly System.Random _battlecryResponseRandom =
            new System.Random(unchecked(Environment.TickCount * 1091));
        private readonly RestRiskTracker _restRisk =
            new RestRiskTracker(
                unchecked(Environment.TickCount * 1181));
        private readonly HuntDirector _huntDirector =
            new HuntDirector(unchecked(Environment.TickCount * 397));
        private readonly HunterCatalogDirector _hunterCatalog =
            new HunterCatalogDirector(
                unchecked(Environment.TickCount * 613));
        private readonly AmbientStalkerDirector _stalkerDirector =
            new AmbientStalkerDirector(
                unchecked(Environment.TickCount * 719));
        private readonly AmbientStalkerCatalogDirector _stalkerCatalog =
            new AmbientStalkerCatalogDirector(
                unchecked(Environment.TickCount * 827));
        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedConfigValues =
                new Dictionary<ConfigDefinition, object>();

        private ConfigEntry<bool> _featureEnabled;
        private ConfigEntry<bool> _ownRestMenu;
        private ConfigEntry<bool> _allowUnprotectedWyrdnightRest;
        private ConfigEntry<RestClockLabelFormat> _restClockLabelFormat;
        private ConfigEntry<float> _restInterruptionChanceAtZeroThreat;
        private ConfigEntry<float> _restInterruptionChanceAtMaximumThreat;
        private ConfigEntry<bool> _enableDynamicTimescale;
        private ConfigEntry<float> _dayMinutes;
        private ConfigEntry<float> _baseNightMinutes;
        private ConfigEntry<float> _maximumThreatNightMinutes;
        private ConfigEntry<float> _passiveThreatPerNight;
        private ConfigEntry<float> _sprintThreatPerMinute;
        private ConfigEntry<float> _combatThreatPerWindow;
        private ConfigEntry<float> _combatResponseSeconds;
        private ConfigEntry<float> _wyrdKillThreat;
        private ConfigEntry<float> _acquisitionThreatPerItem;
        private ConfigEntry<float> _protectedDecayPerMinute;
        private ConfigEntry<float> _interiorDecayPerMinute;
        private ConfigEntry<float> _loadReconstructionAtDawn;
        private ConfigEntry<float> _graceSeconds;
        private ConfigEntry<string> _threatMeterColor;
        private ConfigEntry<bool> _showExactThreat;
        private ConfigEntry<float> _meterOffsetX;
        private ConfigEntry<float> _meterOffsetY;
        private ConfigEntry<float> _baseDangerBudget;
        private ConfigEntry<float> _longNightBonusScale;
        private ConfigEntry<float> _maximumLongNightBonus;
        private ConfigEntry<GameplayTuningPreset> _gameplayPreset;
        private ConfigEntry<float> _baseHazardPerMinute;
        private ConfigEntry<float> _threatHazardPerMinute;
        private ConfigEntry<float> _nightProgressHazardPerMinute;
        private ConfigEntry<float> _minimumHazardTarget;
        private ConfigEntry<float> _maximumHazardTarget;
        private ConfigEntry<float> _warningSeconds;
        private ConfigEntry<float> _dangerCostMultiplier;
        private ConfigEntry<int> _maximumPackSize;
        private ConfigEntry<float> _sidecarChance;
        private ConfigEntry<bool> _allowEliteEnemies;
        private ConfigEntry<bool> _enableAmbientStalkers;
        private ConfigEntry<float> _stalkerMinimumCooldown;
        private ConfigEntry<float> _stalkerMaximumCooldown;
        private ConfigEntry<float> _stalkerMaximumCooldownAtFiftyThreat;
        private ConfigEntry<float> _stalkerProvocationThreat;
        private ConfigEntry<float> _stalkerMinimumSpawnDistance;
        private ConfigEntry<float> _stalkerMaximumSpawnDistance;
        private ConfigEntry<float> _stalkerPassiveDespawnDistance;
        private ConfigEntry<float> _stalkerOffCameraDespawnSeconds;
        private ConfigEntry<float> _hunterSpawnDistance;
        private ConfigEntry<float> _escapeDistance;
        private ConfigEntry<float> _escapeSustainSeconds;
        private ConfigEntry<float> _killThreatRelief;
        private ConfigEntry<float> _escapeThreatRelief;
        private ConfigEntry<float> _killRecoverySeconds;
        private ConfigEntry<float> _escapeRecoverySeconds;
        private ConfigEntry<float> _failedPlacementRecoverySeconds;
        private ConfigEntry<bool> _boundaryEnabled;
        private ConfigEntry<BoundaryRenderMode> _boundaryRenderMode;
        private ConfigEntry<string> _boundaryColor;
        private ConfigEntry<float> _boundaryBrightness;
        private ConfigEntry<float> _boundaryVisualRadius;
        private ConfigEntry<float> _boundaryThickness;
        private ConfigEntry<float> _boundaryNearRadius;
        private ConfigEntry<float> _boundaryNearIntensity;
        private ConfigEntry<float> _boundaryNearThickness;
        private ConfigEntry<float> _boundaryMiddleRadius;
        private ConfigEntry<float> _boundaryMiddleIntensity;
        private ConfigEntry<float> _boundaryMiddleThickness;
        private ConfigEntry<float> _boundaryOuterIntensity;
        private ConfigEntry<bool> _boundaryPulseEnabled;
        private ConfigEntry<float> _boundaryPulseAmount;
        private ConfigEntry<float> _boundaryPulseMinimumSeconds;
        private ConfigEntry<float> _boundaryPulseMaximumSeconds;
        private ConfigEntry<bool> _wyrdVisualsEnabled;
        private ConfigEntry<WyrdnessPalette> _wyrdnessPalette;
        private ConfigEntry<float> _purpleExposureMultiplier;
        private ConfigEntry<float> _purpleExposureCompensation;
        private ConfigEntry<float> _purpleIndirectDiffuseMultiplier;
        private ConfigEntry<float> _threatVisualSmoothingSeconds;
        private ConfigEntry<float> _minimumThreatVisualScale;
        private ConfigEntry<float> _maximumThreatVisualScale;
        private ConfigEntry<string> _threatRedColor;
        private ConfigEntry<float> _maximumThreatRedBlend;
        private ConfigEntry<string> _moonSurfaceColor;
        private ConfigEntry<float> _moonSurfaceTintStrength;
        private ConfigEntry<float> _moonSurfaceIntensity;
        private ConfigEntry<bool> _tintMoonCorona;
        private ConfigEntry<string> _moonCoronaColor;
        private ConfigEntry<float> _moonCoronaIntensity;
        private ConfigEntry<string> _moonlightColor;
        private ConfigEntry<float> _moonlightTintStrength;
        private ConfigEntry<bool> _tintNightSkyAmbient;
        private ConfigEntry<string> _nightSkyAmbientColor;
        private ConfigEntry<float> _nightSkyAmbientTintStrength;
        private ConfigEntry<bool> _tintBonfireProtectionBubble;
        private ConfigEntry<string> _protectionBubbleColor;
        private ConfigEntry<float> _protectionBubbleIntensity;
        private ConfigEntry<float> _protectionBubbleBorderIntensity;
        private ConfigEntry<float> _wyrdVisualTransitionSeconds;
        private ConfigEntry<bool> _gftEnabled;
        private ConfigEntry<GftNotificationPreset> _gftPreset;
        private ConfigEntry<bool> _gftDetailedExactThreat;
        private ConfigEntry<float> _gftCooldownSeconds;
        private ConfigEntry<float> _battlecryResponseCooldownSeconds;
        private ConfigEntry<float> _diagnosticGftCooldownSeconds;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _enableThreatOverride;
        private ConfigEntry<float> _threatOverrideValue;
        private ConfigEntry<bool> _enableTimescaleOverride;
        private ConfigEntry<float> _timescaleOverrideMultiplier;

        private Harmony _harmony;
        private ThreatMeterController _meter;
        private BoundaryController _boundary;
        private GrailFloatingTextBridge _gft;
        private FirstHunterRuntime _hunterRuntime;
        private AmbientStalkerRuntime _stalkerRuntime;
        private WorldTimescaleController _worldTimescale;
        private WyrdVisualRuntime _wyrdVisuals;
        private Hero _trackedHero;
        private IEventListener _attackStartListener;
        private IEventListener _environmentHitListener;
        private IEventListener _projectileFiredListener;
        private IEventListener _spellCastListener;
        private IEventListener _damageTakenListener;
        private IEventListener _damageDealtListener;
        private IEventListener _killListener;
        private IEventListener _directPickupListener;
        private IEventListener _containerPickupListener;
        private object _heroListenerEventSystem;
        private object _acquisitionListenerEventSystem;
        private float _nextHeroListenerRetryUnscaled;
        private float _nextAcquisitionListenerRetryUnscaled;
        private bool _heroListenerFailureLogged;
        private bool _acquisitionListenerFailureLogged;
        private RuntimeContext _currentContext;
        private float _pollElapsed = StatePollIntervalSeconds;
        private double _lastThreatClockSeconds;
        private bool _hasContext;
        private bool _environmentImpactSeenThisAttack;
        private bool _placeMeterBelowResourceBars;
        private bool _wasFeatureEnabled;
        private bool _hasKnownProtectionState;
        private bool _lastKnownProtected;
        private string _lastSamplingFailure;
        private string _lastInvalidBoundaryColor;
        private string _parsedBoundaryColorText;
        private Color _parsedBoundaryColor;
        private bool _hasParsedBoundaryColor;
        private string _lastInvalidThreatRedColor;
        private string _parsedThreatRedColorText;
        private Color _parsedThreatRedColor;
        private bool _hasParsedThreatRedColor;
        private bool _meterFailureLogged;
        private bool _boundaryFailureLogged;
        private bool _worldTimescaleFailureLogged;
        private bool _wyrdVisualFailureLogged;
        private string _activeHuntSceneName;
        private bool _activeHuntBudgetSpent;
        private float _activeHuntDangerCost;
        private HuntEncounterPlan _pendingHuntPlan;
        private AmbientStalkerSelection _pendingStalkerSelection;
        private string _activeStalkerSceneName;
        private bool _activeStalkerWasSighted;
        private bool _applyingGameplayPreset;
        private float _pendingPassiveThreatDiagnostic;
        private float _pendingMovementThreatDiagnostic;
        private float _pendingProtectedDecayDiagnostic;
        private float _pendingInteriorDecayDiagnostic;
        private double _nextContinuousThreatDiagnosticSeconds;
        private bool _restAtmosphereReconciliationPending;
        private RestPopupUI _activeRestRiskPopup;
        private RestRiskWindow _activeRestRiskWindow;
        private bool _hasActiveRestRiskWindow;
        private bool _restRiskPreparedForUpcomingNight;
        private bool _pendingRestHunt;
        private bool _restClockFailureLogged;
        private bool _quickWeatherTimeFailureLogged;
        private bool _restAvailabilityFailureLogged;
        private VFireplaceUI _activeFireplaceView;
        private bool _visualLoadContinuityPending;
        private int _battlecriesSinceResponse;
        private int _battlecriesUntilResponse;
        private int _recentBattlecryCount;
        private double _lastBattlecrySeconds = double.NegativeInfinity;
        private double _nextBattlecryResponseSeconds;

        public bool CanReceiveEvents
        {
            get { return this != null && enabled; }
        }

        private void Awake()
        {
            Instance = this;
            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
                _gft = new GrailFloatingTextBridge(Logger);
                _boundary = new BoundaryController(
                    Logger,
                    ShowDiagnosticSystem);
                _meter = new ThreatMeterController(Logger);
                try
                {
                    _wyrdVisuals = new WyrdVisualRuntime(
                        Logger,
                        ShowDiagnosticSystem);
                }
                catch (Exception visualException)
                {
                    Logger.LogWarning(
                        "The optional Wyrdnight environment presentation is unavailable; gameplay remains active: "
                        + visualException.GetBaseException().Message);
                }
                _hunterRuntime = new FirstHunterRuntime(
                    Logger,
                    unchecked(Environment.TickCount * 911));
                _stalkerRuntime = new AmbientStalkerRuntime(
                    Logger,
                    unchecked(Environment.TickCount * 1013));
                _worldTimescale = new WorldTimescaleController(Logger);
                PatchGame();
                _wasFeatureEnabled = IsFeatureEnabled();
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded; the curated regional hunt director is active.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    PluginName + " failed during startup: " + exception);
                enabled = false;
            }
        }

        private void Update()
        {
            float unscaledDelta = Time.unscaledDeltaTime;
            bool paused = Time.timeScale <= 0f;

            PrimeWyrdVisualsDuringTransientLoad();

            _activeRealTimeClock.Advance(
                unscaledDelta,
                _hasContext
                    && NightStateEvaluator.CanAdvanceActiveClock(
                        _currentContext.Observation,
                        paused));

            if (_boundary != null)
            {
                _boundary.Tick(
                    unscaledDelta,
                    !paused
                        && IsFeatureEnabled()
                        && _hasContext
                        && IsKnownValidWyrdNight(_currentContext)
                        && _currentContext.Observation.IsOutdoor);
            }

            if (unscaledDelta > 0f
                && !float.IsNaN(unscaledDelta)
                && !float.IsInfinity(unscaledDelta))
            {
                _pollElapsed += unscaledDelta;
            }

            if (_pollElapsed < StatePollIntervalSeconds)
            {
                return;
            }

            _pollElapsed = 0f;
            TrackHero(Hero.Current);
            BindAcquisitionListeners();
            RuntimeContext nextContext;
            try
            {
                nextContext = SampleContext();
                _lastSamplingFailure = null;
            }
            catch (Exception exception)
            {
                string failure = exception.GetType().Name
                    + ": "
                    + exception.Message;
                if (!string.Equals(
                    failure,
                    _lastSamplingFailure,
                    StringComparison.Ordinal))
                {
                    Logger.LogWarning(
                        "Night-state sample failed closed: " + failure);
                    _lastSamplingFailure = failure;
                }

                nextContext = RuntimeContext.Unknown;
            }

            double activeClockSeconds = _activeRealTimeClock.Seconds;
            float activeDelta = (float)Math.Max(
                0d,
                activeClockSeconds - _lastThreatClockSeconds);
            _lastThreatClockSeconds = activeClockSeconds;

            bool featureEnabled = IsFeatureEnabled();
            if (!featureEnabled)
            {
                if (_wasFeatureEnabled)
                {
                    FlushContinuousThreatDiagnostics(true);
                    _threat.NotifyLoad();
                    _activity.ResetNight();
                    _pacing.Reset();
                    _notificationCooldowns.Reset();
                    ResetBattlecryState();
                    ResetRestRisk();
                    _hasKnownProtectionState = false;
                    _restAtmosphereReconciliationPending = false;
                    ResetHuntRuntime("feature disabled", true);
                }
            }
            else
            {
                AdvanceThreat(nextContext, activeDelta);
                AdvanceHunt(nextContext, activeDelta);
            }
            _wasFeatureEnabled = featureEnabled;
            UpdateWorldTimescale(nextContext);

            bool hadContext = _hasContext;
            RuntimeContext previousContext = _currentContext;
            bool contextChanged = !hadContext
                || !SameDiagnosticState(_currentContext, nextContext);
            bool reconciledAfterRest =
                TryCompleteRestAtmosphereReconciliation(nextContext);
            if (contextChanged)
            {
                LogTransition(nextContext);
                if (!reconciledAfterRest)
                {
                    ObserveContextTransition(nextContext);
                }
            }

            _currentContext = nextContext;
            _hasContext = true;
            RefreshActiveRestAvailability();
            UpdateMeter(featureEnabled, nextContext);
            UpdateBoundary(featureEnabled, nextContext);
            UpdateWyrdVisuals(
                featureEnabled,
                nextContext,
                activeDelta,
                hadContext,
                previousContext);
        }

        private void OnDestroy()
        {
            _activeFireplaceView = null;
            if (_gameplayPreset != null)
            {
                _gameplayPreset.SettingChanged -=
                    OnGameplayPresetChanged;
            }
            DisposeGameListeners();
            if (_worldTimescale != null)
            {
                _worldTimescale.Release(VanillaCycleMinutes());
                _worldTimescale = null;
            }
            if (_wyrdVisuals != null)
            {
                _wyrdVisuals.Release();
                _wyrdVisuals = null;
            }
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (_meter != null)
            {
                _meter.Release();
                _meter = null;
            }

            if (_boundary != null)
            {
                _boundary.Release();
                _boundary = null;
            }

            if (_gft != null)
            {
                _gft.Release();
                _gft = null;
            }

            ResetHuntRuntime("plugin destroyed", true);

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void UpdateWorldTimescale(RuntimeContext context)
        {
            if (_worldTimescale == null)
            {
                return;
            }

            try
            {
                GameRealTime clock = World.Any<GameRealTime>();
                bool featureEnabled = IsFeatureEnabled();
                bool timescaleOverrideEnabled = featureEnabled
                    && _enableTimescaleOverride != null
                    && _enableTimescaleOverride.Value;
                _worldTimescale.Update(
                    clock,
                    VanillaCycleMinutes(),
                    featureEnabled
                        && (timescaleOverrideEnabled
                            || _enableDynamicTimescale == null
                            || _enableDynamicTimescale.Value),
                    ValueOrDefault(
                        _dayMinutes,
                        DefaultDayMinutes),
                    ValueOrDefault(
                        _baseNightMinutes,
                        DefaultBaseNightMinutes),
                    ValueOrDefault(
                        _maximumThreatNightMinutes,
                        DefaultMaximumThreatNightMinutes),
                    context.Observation.GameSaysNight
                        && context.Observation.HeroSaysNight
                            ? _threat.Value
                            : 0f,
                    timescaleOverrideEnabled,
                    ValueOrDefault(
                        _timescaleOverrideMultiplier,
                        1f));
                _worldTimescaleFailureLogged = false;
            }
            catch (Exception exception)
            {
                if (!_worldTimescaleFailureLogged)
                {
                    _worldTimescaleFailureLogged = true;
                    Logger.LogWarning(
                        "Could not sample the world clock for dynamic timescale control: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private static float VanillaCycleMinutes()
        {
            if (World.Services == null)
            {
                return 0f;
            }
            GameConstants gameConstants =
                World.Services.TryGet<GameConstants>();
            return gameConstants == null
                ? 0f
                : gameConstants.dayDurationInMinutes;
        }

        private void AdvanceThreat(
            RuntimeContext context,
            float activeDelta)
        {
            ThreatFrame frame = new ThreatFrame
            {
                IsKnownDaylight = IsKnownDaylight(context),
                IsValidWyrdNight = IsKnownValidWyrdNight(context),
                IsOutdoor = context.Observation.IsOutdoor,
                IsProtected = context.IsProtected,
                CanAdvanceActiveTime = activeDelta > 0f
                    && NightStateEvaluator.CanAdvanceActiveClock(
                        context.Observation,
                        context.IsPaused),
                NightProgress = context.NightProgress,
                ActiveSeconds = activeDelta
            };
            bool threatOverrideActive = IsKnownValidWyrdNight(context)
                && _enableThreatOverride != null
                && _enableThreatOverride.Value;
            ThreatUpdateResult update = _threat.Advance(
                frame,
                CurrentThreatTuning());
            if (threatOverrideActive)
            {
                update = _threat.SetDiagnosticOverride(
                    true,
                    ValueOrDefault(_threatOverrideValue, 0f));
            }
            else
            {
                _threat.SetDiagnosticOverride(false, 0f);
            }
            ObserveThreatUpdate(update);

            if (IsKnownValidWyrdNight(context)
                && !_pacing.IsInitialized)
            {
                BeginNightPacing(context);
            }

            if (update.Cause == ThreatChangeCause.DawnReset
                || update.Cause
                    == ThreatChangeCause.LoadReconstruction
                || update.Cause == ThreatChangeCause.NightStarted)
            {
                _activity.ResetNight();
                ResetBattlecryState();
                if (update.Cause == ThreatChangeCause.DawnReset)
                {
                    ResolveHunt(
                        HuntResolution.Dawn,
                        "Wyrdnight ended at dawn",
                        false,
                        true);
                    ResetHuntRuntime(
                        "Wyrdnight ended at dawn",
                        true);
                    _pacing.Reset();
                    _notificationCooldowns.Reset();
                    ResetRestRisk();
                    _hasKnownProtectionState = false;
                }
            }

            if (threatOverrideActive)
            {
                _activity.Suspend();
                return;
            }

            if (!_threat.CanAcceptActivity)
            {
                _activity.Suspend();
                return;
            }

            Hero hero = _trackedHero;
            bool sustainedMovement = hero != null
                && !hero.HasBeenDiscarded
                && (hero.IsSprinting
                    || (hero.IsSwimming
                        && hero.HorizontalSpeed
                            >= FastSwimMinimumSpeed));
            ApplyActivity(
                _activity.AdvanceMovement(
                    sustainedMovement,
                    activeDelta,
                    ValueOrDefault(
                        _sprintThreatPerMinute,
                        DefaultSprintThreatPerMinute)),
                ThreatChangeCause.SprintOrFastSwim);

            double now = _activeRealTimeClock.Seconds;
            ApplyActivity(
                _activity.FlushCombat(
                    now,
                    ValueOrDefault(
                        _combatThreatPerWindow,
                        DefaultCombatThreatPerWindow),
                    ValueOrDefault(
                        _combatResponseSeconds,
                        DefaultCombatResponseSeconds)),
                ThreatChangeCause.Combat);
            float acquisitionPerItem = ValueOrDefault(
                _acquisitionThreatPerItem,
                DefaultAcquisitionThreatPerItem);
            ApplyActivity(
                _activity.FlushAcquisition(
                    now,
                    acquisitionPerItem * 4f),
                ThreatChangeCause.Acquisition);
        }

        private void ApplyActivity(
            float amount,
            ThreatChangeCause cause)
        {
            ThreatUpdateResult result = _threat.AddActivity(
                amount,
                cause);
            if (!result.Changed)
            {
                return;
            }

            if (cause == ThreatChangeCause.SprintOrFastSwim)
            {
                if (DiagnosticsEnabled())
                {
                    AccumulateContinuousThreatDiagnostic(result);
                    FlushContinuousThreatDiagnostics(
                        result.StageChanged);
                }
            }
            else
            {
                Logger.LogInfo(
                    "Threat source: "
                    + cause
                    + "; accepted="
                    + (result.CurrentThreat - result.PreviousThreat).ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + "; threat="
                    + result.CurrentThreat.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "; stage="
                    + result.CurrentStage
                    + ".");
                if (DiagnosticsEnabled())
                {
                    ShowDiagnosticSystem(
                        "EITD - Threat +"
                        + (result.CurrentThreat
                            - result.PreviousThreat).ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                        + " "
                        + FormatCause(cause)
                        + " -> "
                        + result.CurrentThreat.ToString(
                            "0.#",
                            CultureInfo.InvariantCulture)
                        + " ("
                        + result.CurrentStage
                        + ")");
                }
            }
            ObserveAtmosphericThreat(result);
            LogStageChange(result);
        }

        internal bool TryRegisterBattlecry(float requestedThreat)
        {
            if (!IsFeatureEnabled()
                || !_hasContext
                || !IsKnownValidWyrdNight(_currentContext)
                || !_currentContext.Observation.IsOutdoor
                || _currentContext.IsProtected
                || _currentContext.IsPaused
                || !_threat.CanAcceptActivity)
            {
                LogDiagnostic(
                    "Battlecry ignored because the hero was not exposed during an active Wyrdnight.");
                return false;
            }

            double now = _activeRealTimeClock.Seconds;
            if (double.IsNegativeInfinity(_lastBattlecrySeconds)
                || now - _lastBattlecrySeconds
                    >= BattlecryThreatResetSeconds)
            {
                _recentBattlecryCount = 0;
            }

            float multiplier = Math.Max(
                MinimumBattlecryThreatMultiplier,
                (float)Math.Pow(0.5d, _recentBattlecryCount));
            _recentBattlecryCount++;
            _lastBattlecrySeconds = now;

            float safeRequestedThreat = float.IsNaN(requestedThreat)
                    || float.IsInfinity(requestedThreat)
                    || requestedThreat <= 0f
                ? 0f
                : requestedThreat;
            float appliedThreat = safeRequestedThreat * multiplier;
            if (appliedThreat > 0f)
            {
                ApplyActivity(appliedThreat, ThreatChangeCause.Battlecry);
            }

            RegisterBattlecryResponse(now);
            LogDiagnostic(
                "Battlecry accepted: requestedThreat="
                + safeRequestedThreat.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; diminishingMultiplier="
                + multiplier.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; appliedThreat="
                + appliedThreat.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; recentCry="
                + _recentBattlecryCount.ToString(
                    CultureInfo.InvariantCulture)
                + ".");
            return true;
        }

        private void RegisterBattlecryResponse(double now)
        {
            GftNotificationPreset preset = _gftPreset == null
                ? GftNotificationPreset.Atmospheric
                : _gftPreset.Value;
            if (_gft == null
                || _gftEnabled == null
                || !_gftEnabled.Value
                || !AtmospherePolicy.ShouldNotify(
                    preset,
                    AtmosphereEventKind.BattlecryResponse))
            {
                _battlecriesSinceResponse = 0;
                _battlecriesUntilResponse = 0;
                return;
            }

            if (_battlecriesUntilResponse <= 0)
            {
                _battlecriesUntilResponse = _battlecryResponseRandom.Next(
                    MinimumBattlecriesPerResponse,
                    MaximumBattlecriesPerResponse + 1);
            }

            _battlecriesSinceResponse++;
            if (_battlecriesSinceResponse < _battlecriesUntilResponse
                || now < _nextBattlecryResponseSeconds)
            {
                return;
            }

            ShowAtmosphere(
                AtmosphereEventKind.BattlecryResponse,
                _threat.Stage,
                "eyes-in-the-dark-battlecry-response");
            _nextBattlecryResponseSeconds = now + ValueOrDefault(
                _battlecryResponseCooldownSeconds,
                DefaultBattlecryResponseCooldownSeconds);
            _battlecriesSinceResponse = 0;
            _battlecriesUntilResponse = _battlecryResponseRandom.Next(
                MinimumBattlecriesPerResponse,
                MaximumBattlecriesPerResponse + 1);
        }

        private void ResetBattlecryState()
        {
            _battlecriesSinceResponse = 0;
            _battlecriesUntilResponse = 0;
            _recentBattlecryCount = 0;
            _lastBattlecrySeconds = double.NegativeInfinity;
            _nextBattlecryResponseSeconds = 0d;
        }

        private void ObserveThreatUpdate(ThreatUpdateResult result)
        {
            if (result.Cause == ThreatChangeCause.DawnReset)
            {
                FlushContinuousThreatDiagnostics(true);
                Logger.LogInfo(
                    "Wyrd Threat reset at dawn; nightly activity history was cleared.");
            }
            else if (result.Cause
                == ThreatChangeCause.LoadReconstruction)
            {
                FlushContinuousThreatDiagnostics(true);
                Logger.LogInfo(
                    "Wyrd Threat reconstructed from current night progress: threat="
                    + result.CurrentThreat.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "; grace="
                    + _threat.GraceRemainingSeconds.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + " active seconds.");
            }
            else if (result.Changed)
            {
                if (IsContinuousThreatCause(result.Cause))
                {
                    AccumulateContinuousThreatDiagnostic(result);
                    FlushContinuousThreatDiagnostics(
                        result.StageChanged);
                }
                else
                {
                    FlushContinuousThreatDiagnostics(true);
                    LogDiagnostic(
                        "Threat change: cause="
                        + result.Cause
                        + "; delta="
                        + (result.CurrentThreat - result.PreviousThreat)
                            .ToString("0.###", CultureInfo.InvariantCulture)
                        + "; threat="
                        + result.CurrentThreat.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture)
                        + ".");
                }
            }

            ObserveAtmosphericThreat(result);
            LogStageChange(result);
        }

        private static bool IsContinuousThreatCause(
            ThreatChangeCause cause)
        {
            return cause == ThreatChangeCause.PassiveExposure
                || cause == ThreatChangeCause.SprintOrFastSwim
                || cause == ThreatChangeCause.ProtectedDecay
                || cause == ThreatChangeCause.InteriorDecay;
        }

        private void AccumulateContinuousThreatDiagnostic(
            ThreatUpdateResult result)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            float delta = result.CurrentThreat - result.PreviousThreat;
            if (_nextContinuousThreatDiagnosticSeconds <= 0d)
            {
                _nextContinuousThreatDiagnosticSeconds =
                    _activeRealTimeClock.Seconds
                    + ContinuousThreatDiagnosticIntervalSeconds;
            }

            switch (result.Cause)
            {
                case ThreatChangeCause.PassiveExposure:
                    _pendingPassiveThreatDiagnostic += delta;
                    break;
                case ThreatChangeCause.SprintOrFastSwim:
                    _pendingMovementThreatDiagnostic += delta;
                    break;
                case ThreatChangeCause.ProtectedDecay:
                    _pendingProtectedDecayDiagnostic += delta;
                    break;
                case ThreatChangeCause.InteriorDecay:
                    _pendingInteriorDecayDiagnostic += delta;
                    break;
            }
        }

        private void FlushContinuousThreatDiagnostics(bool force)
        {
            if (!DiagnosticsEnabled())
            {
                _pendingPassiveThreatDiagnostic = 0f;
                _pendingMovementThreatDiagnostic = 0f;
                _pendingProtectedDecayDiagnostic = 0f;
                _pendingInteriorDecayDiagnostic = 0f;
                _nextContinuousThreatDiagnosticSeconds = 0d;
                return;
            }

            bool hasPending = Math.Abs(_pendingPassiveThreatDiagnostic)
                    > 0.0001f
                || Math.Abs(_pendingMovementThreatDiagnostic) > 0.0001f
                || Math.Abs(_pendingProtectedDecayDiagnostic) > 0.0001f
                || Math.Abs(_pendingInteriorDecayDiagnostic) > 0.0001f;
            if (!hasPending)
            {
                return;
            }
            if (!force
                && _activeRealTimeClock.Seconds
                    < _nextContinuousThreatDiagnosticSeconds)
            {
                return;
            }

            float net = _pendingPassiveThreatDiagnostic
                + _pendingMovementThreatDiagnostic
                + _pendingProtectedDecayDiagnostic
                + _pendingInteriorDecayDiagnostic;
            LogDiagnostic(
                "Continuous threat summary: passive="
                + _pendingPassiveThreatDiagnostic.ToString(
                    "+0.###;-0.###;0",
                    CultureInfo.InvariantCulture)
                + "; movement="
                + _pendingMovementThreatDiagnostic.ToString(
                    "+0.###;-0.###;0",
                    CultureInfo.InvariantCulture)
                + "; protectedDecay="
                + _pendingProtectedDecayDiagnostic.ToString(
                    "+0.###;-0.###;0",
                    CultureInfo.InvariantCulture)
                + "; interiorDecay="
                + _pendingInteriorDecayDiagnostic.ToString(
                    "+0.###;-0.###;0",
                    CultureInfo.InvariantCulture)
                + "; net="
                + net.ToString(
                    "+0.###;-0.###;0",
                    CultureInfo.InvariantCulture)
                + "; threat="
                + _threat.Value.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; stage="
                + _threat.Stage
                + ".");
            _pendingPassiveThreatDiagnostic = 0f;
            _pendingMovementThreatDiagnostic = 0f;
            _pendingProtectedDecayDiagnostic = 0f;
            _pendingInteriorDecayDiagnostic = 0f;
            _nextContinuousThreatDiagnosticSeconds = 0d;
        }

        private void LogStageChange(ThreatUpdateResult result)
        {
            if (result.StageChanged)
            {
                Logger.LogInfo(
                    "Wyrd Threat stage: "
                    + result.PreviousStage
                    + " -> "
                    + result.CurrentStage
                    + "; threat="
                    + result.CurrentThreat.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private void OnDamageTaken(DamageOutcome outcome)
        {
            QueueCombatThreat(outcome, "taken");
        }

        private void OnAttackStarted(AttackParameters _)
        {
            _environmentImpactSeenThisAttack = false;
        }

        private void OnEnvironmentHit(EnvironmentHitData data)
        {
            if (_environmentImpactSeenThisAttack)
            {
                return;
            }

            _environmentImpactSeenThisAttack = true;
            if (!_threat.CanAcceptActivity || data.Item == null)
            {
                return;
            }

            float maximum = ValueOrDefault(
                _combatThreatPerWindow,
                DefaultCombatThreatPerWindow);
            bool accepted = _activity.RecordCombat(
                maximum * 0.5f,
                "environment:" + ModelId(data.Item),
                _activeRealTimeClock.Seconds);
            if (accepted)
            {
                LogDiagnostic(
                    "Queued confirmed environment-impact threat for item "
                    + ModelId(data.Item)
                    + ".");
            }
        }

        private void OnProjectileFired(
            DamageDealingProjectile projectile)
        {
            Item sourceWeapon = projectile == null
                ? null
                : projectile.SourceWeapon;
            if (sourceWeapon != null && sourceWeapon.IsMagic)
            {
                return;
            }

            QueueRangedActionThreat(sourceWeapon, "projectile");
        }

        private void OnSpellCast(CastSpellData data)
        {
            QueueRangedActionThreat(data.Item, "spell");
        }

        private void QueueRangedActionThreat(Item item, string kind)
        {
            if (!_threat.CanAcceptActivity
                || item == null
                || item.HasBeenDiscarded)
            {
                return;
            }

            float maximum = ValueOrDefault(
                _combatThreatPerWindow,
                DefaultCombatThreatPerWindow);
            bool accepted = _activity.RecordCombat(
                maximum * 0.25f,
                "ranged-action:" + ModelId(item),
                _activeRealTimeClock.Seconds);
            if (accepted)
            {
                LogDiagnostic(
                    "Queued confirmed "
                    + kind
                    + "-use threat for item "
                    + ModelId(item)
                    + ".");
            }
        }

        private void OnDamageDealt(DamageOutcome outcome)
        {
            NpcElement target = outcome.TargetPure as NpcElement;
            bool applyProvocationThreat;
            string provocationReason;
            if (outcome.FinalAmount > 0f
                && _stalkerRuntime != null
                && _stalkerRuntime.TryProvoke(
                    target,
                    _trackedHero,
                    out applyProvocationThreat,
                    out provocationReason))
            {
                HandleAmbientStalkerRuntimeEvents();
            }
            QueueCombatThreat(outcome, "dealt");
        }

        private void QueueCombatThreat(
            DamageOutcome outcome,
            string direction)
        {
            if (!_threat.CanAcceptActivity
                || outcome.FinalAmount < MeaningfulDamageMinimum)
            {
                return;
            }

            IModel subject = string.Equals(
                direction,
                "taken",
                StringComparison.Ordinal)
                ? outcome.AttackerPure as IModel
                : outcome.TargetPure as IModel;
            string fingerprint = direction
                + ":"
                + ModelId(subject);
            float maximum = ValueOrDefault(
                _combatThreatPerWindow,
                DefaultCombatThreatPerWindow);
            bool accepted = _activity.RecordCombat(
                maximum * 0.25f,
                fingerprint,
                _activeRealTimeClock.Seconds);
            if (accepted)
            {
                LogDiagnostic(
                    "Queued meaningful combat threat: "
                    + direction
                    + "; damage="
                    + outcome.FinalAmount.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private void OnKill(DamageOutcome outcome)
        {
            NpcElement killedNpc = outcome.TargetPure as NpcElement;
            if (_hunterRuntime != null
                && _hunterRuntime.IsOfficialHunter(killedNpc))
            {
                _hunterRuntime.ConfirmOfficialKill();
                HandleHunterRuntimeEvents();
                return;
            }

            if (_stalkerRuntime != null
                && _stalkerRuntime.IsExactStalker(killedNpc))
            {
                _stalkerRuntime.ConfirmKilled(killedNpc);
                HandleAmbientStalkerRuntimeEvents();
            }

            if (!_threat.CanAcceptActivity)
            {
                return;
            }

            NpcElement npc = killedNpc;
            if (npc == null
                || npc.HasBeenDiscarded
                || (!npc.WyrdConverted
                    && (npc.Template == null
                        || !npc.Template.IsWyrdnessBound)))
            {
                return;
            }

            string npcId = ModelId(npc);
            if (_activity.RecordWyrdKill(npcId))
            {
                ApplyActivity(
                    ValueOrDefault(
                        _wyrdKillThreat,
                        DefaultWyrdKillThreat),
                    ThreatChangeCause.WyrdKill);
            }
        }

        private void OnDirectItemPicked(
            PickItemAction.ItemPickedData data)
        {
            if (!ReferenceEquals(data.picker, _trackedHero))
            {
                return;
            }

            QueueAcquisition(data.item);
        }

        private void OnContainerItemPicked(Item item)
        {
            QueueAcquisition(item);
        }

        private void QueueAcquisition(Item item)
        {
            if (!_threat.CanAcceptActivity || item == null)
            {
                return;
            }

            bool accepted = _activity.RecordAcquisition(
                ValueOrDefault(
                    _acquisitionThreatPerItem,
                    DefaultAcquisitionThreatPerItem),
                ModelId(item),
                _activeRealTimeClock.Seconds);
            if (accepted)
            {
                LogDiagnostic(
                    "Queued eligible acquisition threat for item "
                    + ModelId(item)
                    + ".");
            }
        }

        private void TrackHero(Hero hero)
        {
            if (hero != null && hero.HasBeenDiscarded)
            {
                hero = null;
            }

            object eventSystem = World.EventSystem;
            bool eventSystemChanged = !ReferenceEquals(
                eventSystem,
                _heroListenerEventSystem);
            if (eventSystemChanged)
            {
                _attackStartListener = null;
                _environmentHitListener = null;
                _projectileFiredListener = null;
                _spellCastListener = null;
                _damageTakenListener = null;
                _damageDealtListener = null;
                _killListener = null;
                _heroListenerEventSystem = eventSystem;
                _nextHeroListenerRetryUnscaled = 0f;
                _heroListenerFailureLogged = false;
            }

            bool sameHero = ReferenceEquals(hero, _trackedHero);
            if (sameHero
                && (hero == null
                    || (_attackStartListener != null
                        && _environmentHitListener != null
                        && _projectileFiredListener != null
                        && _spellCastListener != null
                        && _damageTakenListener != null
                        && _damageDealtListener != null
                        && _killListener != null)))
            {
                return;
            }

            if (!sameHero)
            {
                bool replacedPlayableHero = _trackedHero != null
                    && hero != null;
                DisposeHeroListeners();
                _trackedHero = hero;
                _environmentImpactSeenThisAttack = false;
                _nextHeroListenerRetryUnscaled = 0f;
                _heroListenerFailureLogged = false;
                if (replacedPlayableHero)
                {
                    _threat.NotifyLoad();
                    _activity.ResetNight();
                    _pacing.Reset();
                    _notificationCooldowns.Reset();
                    ResetBattlecryState();
                    _hasKnownProtectionState = false;
                    ResetHuntRuntime("playable hero replaced", true);
                }
            }
            if (hero == null || eventSystem == null)
            {
                return;
            }

            if (!eventSystemChanged
                && Time.unscaledTime
                    < _nextHeroListenerRetryUnscaled)
            {
                return;
            }

            try
            {
                _attackStartListener = ModelExtensions.ListenTo(
                    hero,
                    ICharacter.Events.OnAttackStart,
                    OnAttackStarted,
                    this);
                _environmentHitListener = ModelExtensions.ListenTo(
                    hero,
                    ICharacter.Events.HitEnvironment,
                    OnEnvironmentHit,
                    this);
                _projectileFiredListener = ModelExtensions.ListenTo(
                    hero,
                    ICharacter.Events.OnFiredProjectile,
                    OnProjectileFired,
                    this);
                _spellCastListener = ModelExtensions.ListenTo(
                    hero,
                    ICharacter.Events.CastingEnded,
                    OnSpellCast,
                    this);
                _damageTakenListener = ModelExtensions.ListenTo(
                    hero.HealthElement,
                    HealthElement.Events.OnDamageTaken,
                    OnDamageTaken,
                    this);
                _damageDealtListener = ModelExtensions.ListenTo(
                    hero,
                    HealthElement.Events.OnDamageDealt,
                    OnDamageDealt,
                    this);
                _killListener = ModelExtensions.ListenTo(
                    hero,
                    HealthElement.Events.OnKill,
                    OnKill,
                    this);
                _nextHeroListenerRetryUnscaled = 0f;
                _heroListenerFailureLogged = false;
            }
            catch (Exception exception)
            {
                DisposeHeroListeners();
                _nextHeroListenerRetryUnscaled = Time.unscaledTime
                    + ListenerRetryBackoffSeconds;
                if (!_heroListenerFailureLogged)
                {
                    _heroListenerFailureLogged = true;
                    Logger.LogWarning(
                        "Could not bind Hero threat events; passive threat remains active and binding will retry in 30 unscaled seconds: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void BindAcquisitionListeners()
        {
            object eventSystem = World.EventSystem;
            bool eventSystemChanged = !ReferenceEquals(
                eventSystem,
                _acquisitionListenerEventSystem);
            if (eventSystemChanged)
            {
                _directPickupListener = null;
                _containerPickupListener = null;
                _acquisitionListenerEventSystem = eventSystem;
                _nextAcquisitionListenerRetryUnscaled = 0f;
                _acquisitionListenerFailureLogged = false;
            }

            if (eventSystem == null
                || (_directPickupListener != null
                    && _containerPickupListener != null))
            {
                return;
            }
            if (!eventSystemChanged
                && Time.unscaledTime
                    < _nextAcquisitionListenerRetryUnscaled)
            {
                return;
            }

            try
            {
                if (_directPickupListener == null)
                {
                    _directPickupListener = World.EventSystem.ListenTo(
                        "*",
                        PickItemAction.Events.ItemPicked,
                        this,
                        OnDirectItemPicked);
                }
                if (_containerPickupListener == null)
                {
                    _containerPickupListener = World.EventSystem.ListenTo(
                        "*",
                        Location.Events.ItemPickedFromLocation,
                        this,
                        OnContainerItemPicked);
                }
                _nextAcquisitionListenerRetryUnscaled = 0f;
                _acquisitionListenerFailureLogged = false;
            }
            catch (Exception exception)
            {
                _nextAcquisitionListenerRetryUnscaled =
                    Time.unscaledTime
                    + ListenerRetryBackoffSeconds;
                if (!_acquisitionListenerFailureLogged)
                {
                    _acquisitionListenerFailureLogged = true;
                    Logger.LogWarning(
                        "Could not bind acquisition threat events; other threat sources remain active and binding will retry in 30 unscaled seconds: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void DisposeGameListeners()
        {
            DisposeHeroListeners();
            if (World.EventSystem != null)
            {
                World.EventSystem.TryDisposeListener(
                    ref _directPickupListener);
                World.EventSystem.TryDisposeListener(
                    ref _containerPickupListener);
                World.EventSystem.RemoveAllListenersOwnedBy(this, true);
            }
            else
            {
                _directPickupListener = null;
                _containerPickupListener = null;
            }
            _trackedHero = null;
            _heroListenerEventSystem = null;
            _acquisitionListenerEventSystem = null;
            _nextHeroListenerRetryUnscaled = 0f;
            _nextAcquisitionListenerRetryUnscaled = 0f;
            _heroListenerFailureLogged = false;
            _acquisitionListenerFailureLogged = false;
        }

        private void DisposeHeroListeners()
        {
            if (World.EventSystem != null)
            {
                World.EventSystem.TryDisposeListener(
                    ref _attackStartListener);
                World.EventSystem.TryDisposeListener(
                    ref _environmentHitListener);
                World.EventSystem.TryDisposeListener(
                    ref _projectileFiredListener);
                World.EventSystem.TryDisposeListener(
                    ref _spellCastListener);
                World.EventSystem.TryDisposeListener(
                    ref _damageTakenListener);
                World.EventSystem.TryDisposeListener(
                    ref _damageDealtListener);
                World.EventSystem.TryDisposeListener(ref _killListener);
            }
            else
            {
                _attackStartListener = null;
                _environmentHitListener = null;
                _projectileFiredListener = null;
                _spellCastListener = null;
                _damageTakenListener = null;
                _damageDealtListener = null;
                _killListener = null;
            }
        }

        private void UpdateMeter(
            bool featureEnabled,
            RuntimeContext context)
        {
            if (_meter == null)
            {
                return;
            }

            bool visible = featureEnabled
                && NightStateEvaluator.ShouldShowThreatMeter(
                    context.Decision);
            try
            {
                float meterOffsetX = ValueOrDefault(_meterOffsetX, 0f);
                float meterOffsetY = ValueOrDefault(_meterOffsetY, 0f);
                if (!_placeMeterBelowResourceBars)
                {
                    meterOffsetX += StandaloneMeterBaselineOffsetX;
                    meterOffsetY += StandaloneMeterBaselineOffsetY;
                }

                _meter.Update(
                    _threat.Value,
                    visible,
                    _threatMeterColor == null
                        ? ThreatMeterController.DefaultColorText
                        : _threatMeterColor.Value,
                    _threatRedColor == null
                        ? DefaultThreatRedColor
                        : _threatRedColor.Value,
                    WyrdVisualResponseEnabled()
                        ? ValueOrDefault(
                            _minimumThreatVisualScale,
                            DefaultMinimumThreatVisualScale)
                        : 1f,
                    WyrdVisualResponseEnabled()
                        ? ValueOrDefault(
                            _maximumThreatVisualScale,
                            DefaultMaximumThreatVisualScale)
                        : 1f,
                    WyrdVisualResponseEnabled()
                        ? ValueOrDefault(
                            _maximumThreatRedBlend,
                            DefaultMaximumThreatRedBlend)
                        : 0f,
                    _showExactThreat != null
                        && _showExactThreat.Value,
                    meterOffsetX,
                    meterOffsetY,
                    _placeMeterBelowResourceBars);
            }
            catch (Exception exception)
            {
                if (!_meterFailureLogged)
                {
                    _meterFailureLogged = true;
                    Logger.LogWarning(
                        "The optional Wyrd Threat meter failed and was disabled; threat and encounters remain active: "
                        + exception.GetBaseException().Message);
                    ShowDiagnosticSystem(
                        "EITD - HUD disabled after an isolated failure; gameplay continues");
                }
                _meter.Release();
                _meter = null;
            }
        }

        private void BeginNightPacing(RuntimeContext context)
        {
            if (!_restRiskPreparedForUpcomingNight
                && !_pendingRestHunt)
            {
                _restRisk.Reset();
            }
            _restRiskPreparedForUpcomingNight = false;
            HuntTuning huntTuning = CurrentHuntTuning();
            _huntDirector.ResetNight(huntTuning);
            _activeHuntBudgetSpent = false;
            _activeHuntDangerCost = 0f;
            _pendingHuntPlan = null;
            _activeHuntSceneName = string.Empty;
            if (_stalkerRuntime != null && _stalkerRuntime.IsBusy)
            {
                _stalkerRuntime.Cancel(
                    "a new Wyrdnight initialized",
                    true);
            }
            _stalkerDirector.ResetNight();
            _pendingStalkerSelection = null;
            _activeStalkerSceneName = string.Empty;
            _activeStalkerWasSighted = false;
            float maximumConfiguredNightMultiplier =
                WorldTimescalePolicy.PhaseDurationMultiplier(
                    VanillaCycleMinutes(),
                    WorldTimescalePolicy.DynamicNightMinutes(
                        ValueOrDefault(
                            _baseNightMinutes,
                            DefaultBaseNightMinutes),
                        ValueOrDefault(
                            _maximumThreatNightMinutes,
                            DefaultMaximumThreatNightMinutes),
                        100f),
                    true);
            NightBudgetSnapshot snapshot = _pacing.BeginNight(
                Math.Max(
                    context.WorldDurationMultiplier,
                    maximumConfiguredNightMultiplier),
                CurrentPacingTuning());
            LogDiagnostic(
                "Night danger budget initialized: worldDurationMultiplier="
                + snapshot.WorldDurationMultiplier.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; longNightBonus="
                + snapshot.BonusFraction.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; budget="
                + snapshot.InitialBudget.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + ". Budget is spent only after confirmed official-hunter placement.");
            ShowAtmosphere(
                AtmosphereEventKind.NightBegin,
                _threat.Stage,
                "eyes-in-the-dark-night-begin");
            ShowDiagnosticSystem(
                "EITD - Night x"
                + snapshot.WorldDurationMultiplier.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "; threat "
                + _threat.Value.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                + "; budget "
                + snapshot.InitialBudget.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture));
        }

        private void AdvanceHunt(
            RuntimeContext context,
            float activeDelta)
        {
            if (_hunterRuntime == null
                || _stalkerRuntime == null
                || !_pacing.IsInitialized)
            {
                return;
            }

            NightObservation observation = context.Observation;
            bool transient = observation.AtTitleScreen
                || observation.IsLoading
                || observation.IsTransitioning
                || observation.IsTraveling
                || observation.IsResting;

            if (_pendingRestHunt && !transient)
            {
                if (!IsKnownValidWyrdNight(context)
                    || !observation.IsOutdoor
                    || !context.IsExposed)
                {
                    if (IsKnownDaylight(context))
                    {
                        _pendingRestHunt = false;
                    }
                }
                else
                {
                    Hero pendingHero = _trackedHero;
                    bool pendingHeroInCombat = pendingHero != null
                        && !pendingHero.HasBeenDiscarded
                        && pendingHero.HeroCombat != null
                        && pendingHero.HeroCombat.IsHeroInFight;
                    if (pendingHeroInCombat
                        || _hunterRuntime.IsInitializing
                        || _hunterRuntime.IsActive)
                    {
                        _pendingRestHunt = false;
                        LogDiagnostic(
                            "Rest interruption hunt was suppressed because combat or an official hunt was already active.");
                    }
                    else
                    {
                        if (_stalkerRuntime.IsBusy)
                        {
                            _stalkerRuntime.Cancel(
                                "an interrupted rest committed an official hunt",
                                true);
                            ClearAmbientStalkerTracking();
                        }
                        HuntTuning restHuntTuning = CurrentHuntTuning();
                        _huntDirector.ResetNight(restHuntTuning);
                        _pendingRestHunt = false;
                        Logger.LogInfo(
                            "Interrupted unprotected rest committed one immediate official hunt; normal eligibility, regional selection, placement confirmation, and budget rules remain authoritative.");
                        ShowAtmosphere(
                            AtmosphereEventKind.HuntCommitted,
                            _threat.Stage,
                            "eyes-in-the-dark-rest-hunt");
                        RequestOfficialHunterPlacement(
                            context,
                            restHuntTuning);
                        return;
                    }
                }
            }

            AdvanceAmbientStalker(
                context,
                activeDelta,
                transient);

            if ((_hunterRuntime.IsInitializing
                    || _hunterRuntime.IsActive)
                && !transient)
            {
                if (!observation.HeroAlive)
                {
                    ResolveHunt(
                        HuntResolution.PlayerDeath,
                        "hero died",
                        false,
                        true);
                    return;
                }

                if (observation.SceneKnown
                    && observation.SceneInitialized
                    && !observation.IsOutdoor)
                {
                    ResolveHunt(
                        _hunterRuntime.IsActive
                            ? HuntResolution.InteriorEscape
                            : HuntResolution.PlacementFailed,
                        "hero entered an interior",
                        _hunterRuntime.IsActive,
                        true);
                    return;
                }

                if (_hunterRuntime.IsActive
                    && !string.IsNullOrEmpty(_activeHuntSceneName)
                    && observation.SceneKnown
                    && observation.SceneInitialized
                    && !string.Equals(
                        _activeHuntSceneName,
                        context.SceneName,
                        StringComparison.Ordinal))
                {
                    ResolveHunt(
                        HuntResolution.LostTarget,
                        "active exterior scene changed",
                        false,
                        true);
                    return;
                }
            }

            Hero hero = _trackedHero;
            bool allowReacquisition = _hunterRuntime.IsActive
                && IsKnownValidWyrdNight(context)
                && observation.IsOutdoor
                && context.IsExposed
                && !transient
                && observation.SceneKnown
                && observation.SceneInitialized
                && !string.IsNullOrEmpty(_activeHuntSceneName)
                && string.Equals(
                    _activeHuntSceneName,
                    context.SceneName,
                    StringComparison.Ordinal);
            _hunterRuntime.Tick(
                activeDelta,
                hero,
                allowReacquisition,
                _hunterRuntime.IsActive
                    && IsKnownValidWyrdNight(context)
                    && observation.IsOutdoor,
                ValueOrDefault(
                    _escapeDistance,
                    DefaultEscapeDistance),
                ValueOrDefault(
                    _escapeSustainSeconds,
                    DefaultEscapeSustainSeconds));
            HandleHunterRuntimeEvents(context.SceneName);

            bool heroInCombat = hero != null
                && !hero.HasBeenDiscarded
                && hero.HeroCombat != null
                && hero.HeroCombat.IsHeroInFight;
            HuntRegion region = HuntRegionResolver.Resolve(
                context.SceneName);
            HuntFrame frame = new HuntFrame
            {
                IsValidWyrdNight = IsKnownValidWyrdNight(context)
                    && observation.IsOutdoor
                    && region != HuntRegion.Unknown,
                IsExposed = context.IsExposed,
                IsProtected = context.IsProtected,
                HeroInUnrelatedCombat = heroInCombat
                    && _huntDirector.State
                        != DirectorState.ActiveHunt,
                EncounterLaneBusy = _stalkerRuntime.IsBusy,
                CanAdvance = activeDelta > 0f && !transient,
                ActiveSeconds = activeDelta,
                Threat = _threat.Value,
                NightProgress = context.NightProgress,
                RemainingDangerBudget = _pacing.RemainingBudget
            };
            HuntTuning tuning = CurrentHuntTuning();
            HuntDirective directive = _huntDirector.Tick(
                frame,
                tuning);
            switch (directive.Kind)
            {
                case HuntDirectiveKind.WarningCommitted:
                    Logger.LogInfo(
                        "Official hunt warning committed: pressure="
                        + directive.Pressure.ToString(
                            "0.###",
                            CultureInfo.InvariantCulture)
                        + "; target="
                        + directive.Target.ToString(
                            "0.###",
                            CultureInfo.InvariantCulture)
                        + "; threat="
                        + _threat.Value.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture)
                        + "; budget="
                        + _pacing.RemainingBudget.ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                        + ".");
                    ShowAtmosphere(
                        AtmosphereEventKind.HuntCommitted,
                        _threat.Stage,
                        "eyes-in-the-dark-hunt-committed");
                    ShowDiagnosticSystem(
                        "EITD - Hunt committed: hazard "
                        + directive.Pressure.ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                        + "/"
                        + directive.Target.ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                        + "; warning "
                        + _huntDirector.WarningRemainingSeconds.ToString(
                            "0.#",
                            CultureInfo.InvariantCulture)
                        + "s");
                    break;
                case HuntDirectiveKind.RequestPlacement:
                    RequestOfficialHunterPlacement(
                        context,
                        tuning);
                    break;
                case HuntDirectiveKind.WarningCancelled:
                    if (_hunterRuntime.IsInitializing)
                    {
                        _hunterRuntime.Cancel(
                            "warning eligibility was lost",
                            true);
                    }
                    _pendingHuntPlan = null;
                    Logger.LogInfo(
                        "Official hunt warning cancelled: "
                        + directive.Reason
                        + "; no danger budget was spent.");
                    ShowDiagnosticSystem(
                        "EITD - Hunt cancelled: "
                        + directive.Reason
                        + "; budget "
                        + _pacing.RemainingBudget.ToString(
                            "0.#",
                            CultureInfo.InvariantCulture));
                    break;
                case HuntDirectiveKind.RecoveryEnded:
                    Logger.LogInfo(
                        "Official hunt recovery ended; roaming resumed.");
                    ShowDiagnosticSystem(
                        "EITD - Recovery ended; roaming resumed");
                    break;
            }
        }

        private void AdvanceAmbientStalker(
            RuntimeContext context,
            float activeDelta,
            bool transient)
        {
            NightObservation observation = context.Observation;
            Hero hero = _trackedHero;
            AmbientStalkerTuning tuning =
                CurrentAmbientStalkerTuning();
            if (!tuning.Enabled)
            {
                if (_stalkerRuntime.IsBusy)
                {
                    _stalkerRuntime.Cancel(
                        "ambient stalkers disabled in config",
                        true);
                    ClearAmbientStalkerTracking();
                }
                _stalkerDirector.ResetNight();
                return;
            }
            bool sceneChanged = _stalkerRuntime.IsBusy
                && !string.IsNullOrEmpty(_activeStalkerSceneName)
                && observation.SceneKnown
                && observation.SceneInitialized
                && !string.Equals(
                    _activeStalkerSceneName,
                    context.SceneName,
                    StringComparison.Ordinal);
            if (_stalkerRuntime.IsBusy && !transient)
            {
                string cleanupReason = !observation.HeroAlive
                    ? "Hero died"
                    : observation.SceneKnown
                        && observation.SceneInitialized
                        && !observation.IsOutdoor
                            ? "Hero entered an interior"
                            : sceneChanged
                                ? "active exterior scene changed"
                                : !IsKnownValidWyrdNight(context)
                                    ? "Wyrdnight ended"
                                    : string.Empty;
                if (!string.IsNullOrEmpty(cleanupReason))
                {
                    _stalkerRuntime.Cancel(cleanupReason, true);
                    _stalkerDirector.Resolve(
                        _threat.Value,
                        CurrentAmbientStalkerTuning());
                    ClearAmbientStalkerTracking();
                }
            }

            _stalkerRuntime.Tick(
                transient ? 0f : activeDelta,
                hero,
                _threat.Value,
                ValueOrDefault(
                    _stalkerPassiveDespawnDistance,
                    DefaultStalkerPassiveDespawnDistance),
                ValueOrDefault(
                    _stalkerOffCameraDespawnSeconds,
                    DefaultStalkerOffCameraDespawnSeconds));
            HandleAmbientStalkerRuntimeEvents(context.SceneName);

            bool heroInCombat = hero != null
                && !hero.HasBeenDiscarded
                && hero.HeroCombat != null
                && hero.HeroCombat.IsHeroInFight;
            HuntRegion region = HuntRegionResolver.Resolve(
                context.SceneName);
            AmbientStalkerFrame frame = new AmbientStalkerFrame
            {
                IsValidWyrdNight = IsKnownValidWyrdNight(context)
                    && observation.IsOutdoor
                    && region != HuntRegion.Unknown,
                IsExposed = context.IsExposed,
                IsProtected = context.IsProtected,
                HeroInCombat = heroInCombat,
                OfficialEncounterLaneBusy =
                    _hunterRuntime.IsInitializing
                    || _hunterRuntime.IsActive
                    || _huntDirector.State == DirectorState.Warning
                    || _huntDirector.State == DirectorState.ActiveHunt,
                RuntimeBusy = _stalkerRuntime.IsBusy,
                AllowHighPressure = _allowEliteEnemies != null
                    && _allowEliteEnemies.Value,
                CanAdvance = activeDelta > 0f && !transient,
                ActiveSeconds = activeDelta,
                Threat = _threat.Value
            };
            AmbientStalkerDirective directive =
                _stalkerDirector.Tick(frame, tuning);
            if (directive.Kind
                == AmbientStalkerDirectiveKind.RequestPlacement)
            {
                RequestAmbientStalkerPlacement(
                    context,
                    tuning);
            }
        }

        private void RequestAmbientStalkerPlacement(
            RuntimeContext context,
            AmbientStalkerTuning tuning)
        {
            Hero hero = _trackedHero;
            HuntRegion region = HuntRegionResolver.Resolve(
                context.SceneName);
            int playerLevel = hero == null
                || hero.HasBeenDiscarded
                || hero.Level == null
                    ? 0
                    : Math.Max(0, hero.Level.ModifiedInt);
            AmbientStalkerSelectionContext selectionContext =
                new AmbientStalkerSelectionContext
                {
                    Region = region,
                    PlayerLevel = playerLevel,
                    Threat = _threat.Value,
                    AllowHighPressure = _allowEliteEnemies != null
                        && _allowEliteEnemies.Value
                };
            AmbientStalkerSelection selection =
                _stalkerCatalog.Select(selectionContext);
            Logger.LogInfo(
                "Ambient stalker selection: region="
                + HuntRegionResolver.ShortName(region)
                + "; playerLevel="
                + playerLevel
                + "; threat="
                + _threat.Value.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; filters=["
                + selection.FilterSummary
                + "]; weights=["
                + selection.WeightSummary
                + "].");
            if (!selection.Success)
            {
                _pendingStalkerSelection = null;
                _stalkerDirector.FailPlacement(
                    _threat.Value,
                    tuning);
                LogDiagnostic(
                    "Ambient stalker selection paused: "
                    + selection.Reason
                    + "; "
                    + selection.FilterSummary
                    + ".");
                return;
            }

            _pendingStalkerSelection = selection;
            string reason;
            if (!_stalkerRuntime.TryStart(
                hero,
                selection,
                ValueOrDefault(
                    _stalkerMinimumSpawnDistance,
                    DefaultStalkerMinimumSpawnDistance),
                ValueOrDefault(
                    _stalkerMaximumSpawnDistance,
                    DefaultStalkerMaximumSpawnDistance),
                out reason))
            {
                _stalkerCatalog.RecordFailure(
                    _stalkerRuntime.LastFailedProfileId);
                _pendingStalkerSelection = null;
                _stalkerDirector.FailPlacement(
                    _threat.Value,
                    tuning);
                Logger.LogWarning(
                    "Ambient stalker placement failed before confirmation: "
                    + reason
                    + ".");
                ShowDiagnosticSystem(
                    "EITD - Stalker spawn failed: "
                    + (string.IsNullOrEmpty(
                            _stalkerRuntime.LastFailedProfileId)
                        ? reason
                        : _stalkerRuntime.LastFailedProfileId));
                return;
            }

            Logger.LogInfo(
                "Ambient stalker placement requested: profile="
                + selection.Profile.Id
                + "; aggression="
                + selection.AggressionThreshold.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; "
                + reason
                + ".");
            ShowDiagnosticSystem(
                "EITD - Stalker plan: "
                + selection.Profile.DisplayName
                + "; aggression "
                + selection.AggressionThreshold.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; budget untouched");
        }

        private void HandleAmbientStalkerRuntimeEvents(
            string currentSceneName = null)
        {
            AmbientStalkerRuntimeEvent runtimeEvent;
            while (_stalkerRuntime.TryConsumeEvent(out runtimeEvent))
            {
                switch (runtimeEvent.Kind)
                {
                    case AmbientStalkerRuntimeEventKind.PlacementConfirmed:
                        if (_pendingStalkerSelection == null
                            || !_pendingStalkerSelection.Success)
                        {
                            _stalkerRuntime.Cancel(
                                "pending ambient selection was lost",
                                true);
                            _stalkerDirector.FailPlacement(
                                _threat.Value,
                                CurrentAmbientStalkerTuning());
                            ClearAmbientStalkerTracking();
                            break;
                        }
                        _stalkerCatalog.RecordConfirmed(
                            _pendingStalkerSelection.Profile);
                        _stalkerDirector.ConfirmPlacement();
                        _activeStalkerSceneName =
                            currentSceneName ?? string.Empty;
                        _activeStalkerWasSighted = false;
                        Logger.LogInfo(
                            "Ambient stalker confirmed: profile="
                            + runtimeEvent.ProfileId
                            + "; aggression="
                            + runtimeEvent.AggressionThreshold.ToString(
                                "0.0",
                                CultureInfo.InvariantCulture)
                            + "; volatile=true; huntBudgetSpent=0.");
                        ShowDiagnosticSystem(
                            "EITD - Stalker active: "
                            + runtimeEvent.DisplayName
                            + "; hidden aggression "
                            + runtimeEvent.AggressionThreshold.ToString(
                                "0.0",
                                CultureInfo.InvariantCulture));
                        _pendingStalkerSelection = null;
                        break;
                    case AmbientStalkerRuntimeEventKind.PlacementFailed:
                        _stalkerCatalog.RecordFailure(
                            runtimeEvent.ProfileId);
                        _stalkerDirector.FailPlacement(
                            _threat.Value,
                            CurrentAmbientStalkerTuning());
                        Logger.LogWarning(
                            "Ambient stalker placement failed: "
                            + runtimeEvent.Reason
                            + ".");
                        ShowDiagnosticSystem(
                            "EITD - Stalker failed: "
                            + runtimeEvent.ProfileId
                            + "; budget untouched");
                        ClearAmbientStalkerTracking();
                        break;
                    case AmbientStalkerRuntimeEventKind.Sighted:
                        _activeStalkerWasSighted = true;
                        ShowAtmosphere(
                            AtmosphereEventKind.StalkerSighted,
                            _threat.Stage,
                            "eyes-in-the-dark-stalker-sighted");
                        LogDiagnostic(
                            "Ambient stalker entered the Hero camera: "
                            + runtimeEvent.ProfileId
                            + ".");
                        break;
                    case AmbientStalkerRuntimeEventKind.Fled:
                        ShowAtmosphere(
                            AtmosphereEventKind.StalkerRetreated,
                            _threat.Stage,
                            "eyes-in-the-dark-stalker-retreated");
                        Logger.LogInfo(
                            "Ambient stalker fled deliberate pursuit: profile="
                            + runtimeEvent.ProfileId
                            + "; distance="
                            + runtimeEvent.DistanceMeters.ToString(
                                "0.0",
                                CultureInfo.InvariantCulture)
                            + "m; huntBudgetSpent=0.");
                        ShowDiagnosticSystem(
                            "EITD - Stalker fled deliberate pursuit at "
                            + runtimeEvent.DistanceMeters.ToString(
                                "0.#",
                                CultureInfo.InvariantCulture)
                            + "m");
                        break;
                    case AmbientStalkerRuntimeEventKind.PassiveDespawned:
                        _stalkerDirector.Resolve(
                            _threat.Value,
                            CurrentAmbientStalkerTuning());
                        if (_activeStalkerWasSighted)
                        {
                            ShowAtmosphere(
                                AtmosphereEventKind.StalkerVanished,
                                _threat.Stage,
                                "eyes-in-the-dark-stalker-vanished");
                        }
                        Logger.LogInfo(
                            "Ambient stalker vanished safely off-camera: "
                            + runtimeEvent.ProfileId
                            + "; huntBudgetSpent=0; relief=0.");
                        ShowDiagnosticSystem(
                            "EITD - Stalker vanished off-camera; next cooldown "
                            + _stalkerDirector.CooldownRemainingSeconds
                                .ToString(
                                    "0.#",
                                    CultureInfo.InvariantCulture)
                            + "s");
                        ClearAmbientStalkerTracking();
                        break;
                    case AmbientStalkerRuntimeEventKind.Escalated:
                        if (runtimeEvent.ProvokedByHero)
                        {
                            ThreatUpdateResult provocation =
                                _threat.AddActivity(
                                    ValueOrDefault(
                                        _stalkerProvocationThreat,
                                        DefaultStalkerProvocationThreat),
                                    ThreatChangeCause.StalkerProvoked);
                            ObserveThreatUpdate(provocation);
                        }
                        ShowAtmosphere(
                            runtimeEvent.ProvokedByHero
                                ? AtmosphereEventKind.StalkerProvoked
                                : AtmosphereEventKind.StalkerAwakened,
                            _threat.Stage,
                            runtimeEvent.ProvokedByHero
                                ? "eyes-in-the-dark-stalker-provoked"
                                : "eyes-in-the-dark-stalker-awakened");
                        Logger.LogInfo(
                            "Ambient stalker escalated: profile="
                            + runtimeEvent.ProfileId
                            + "; trigger="
                            + runtimeEvent.Reason
                            + "; triggerKind="
                            + runtimeEvent.EscalationCause
                            + "; provoked="
                            + (runtimeEvent.ProvokedByHero
                                ? "true"
                                : "false")
                            + "; huntBudgetSpent=0; officialReliefEligible=false.");
                        ShowDiagnosticSystem(
                            "EITD - Stalker hostile: "
                            + runtimeEvent.DisplayName
                            + "; trigger "
                            + (runtimeEvent.ProvokedByHero
                                ? "Hero attack"
                                : runtimeEvent.EscalationCause
                                        == AmbientStalkerEscalationCause.ClosePursuit
                                    ? "close pursuit"
                                    : "threat "
                                        + runtimeEvent.AggressionThreshold
                                            .ToString(
                                                "0.0",
                                                CultureInfo.InvariantCulture))
                            + "; budget untouched");
                        break;
                    case AmbientStalkerRuntimeEventKind.HostileKilled:
                    case AmbientStalkerRuntimeEventKind.LostTarget:
                        _stalkerDirector.Resolve(
                            _threat.Value,
                            CurrentAmbientStalkerTuning());
                        Logger.LogInfo(
                            "Ambient stalker lane resolved: outcome="
                            + runtimeEvent.Kind
                            + "; profile="
                            + runtimeEvent.ProfileId
                            + "; reason="
                            + runtimeEvent.Reason
                            + "; huntBudgetSpent=0; officialRelief=0.");
                        ShowDiagnosticSystem(
                            "EITD - Stalker lane resolved: "
                            + runtimeEvent.Kind
                            + "; relief 0; budget untouched");
                        ClearAmbientStalkerTracking();
                        break;
                }
            }
        }

        private void ClearAmbientStalkerTracking()
        {
            _pendingStalkerSelection = null;
            _activeStalkerSceneName = string.Empty;
            _activeStalkerWasSighted = false;
        }

        private void RequestOfficialHunterPlacement(
            RuntimeContext context,
            HuntTuning tuning)
        {
            Hero hero = _trackedHero;
            HuntRegion region = HuntRegionResolver.Resolve(
                context.SceneName);
            int playerLevel = hero == null
                || hero.HasBeenDiscarded
                || hero.Level == null
                    ? 0
                    : Math.Max(0, hero.Level.ModifiedInt);
            HunterSelectionContext selectionContext =
                new HunterSelectionContext
                {
                    Region = region,
                    PlayerLevel = playerLevel,
                    Threat = _threat.Value,
                    RemainingBudget = _pacing.RemainingBudget,
                    DangerCostMultiplier = ValueOrDefault(
                        _dangerCostMultiplier,
                        DefaultDangerCostMultiplier),
                    SidecarChance = ValueOrDefault(
                        _sidecarChance,
                        DefaultSidecarChance),
                    AllowEliteEnemies = _allowEliteEnemies != null
                        && _allowEliteEnemies.Value,
                    MaximumPackSize = _maximumPackSize == null
                        ? DefaultMaximumPackSize
                        : _maximumPackSize.Value
                };
            HunterSelectionResult selection =
                _hunterCatalog.Select(selectionContext);
            Logger.LogInfo(
                "Official hunt selection: region="
                + HuntRegionResolver.ShortName(region)
                + "; playerLevel="
                + playerLevel
                + "; threat="
                + _threat.Value.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; budget="
                + _pacing.RemainingBudget.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "; elites="
                + (selectionContext.AllowEliteEnemies
                    ? "enabled>75"
                    : "disabled")
                + "; filters=["
                + selection.FilterSummary
                + "]; weights=["
                + selection.WeightSummary
                + "].");
            if (!selection.Success)
            {
                _pendingHuntPlan = null;
                _huntDirector.FailPlacement(tuning);
                Logger.LogWarning(
                    "Official hunt selection skipped: "
                    + selection.Reason
                    + "; no danger budget was spent.");
                ShowDiagnosticSystem(
                    "EITD - Pool empty: "
                    + HuntRegionResolver.ShortName(region)
                    + " L"
                    + playerLevel
                    + "; "
                    + (string.IsNullOrEmpty(selection.FilterSummary)
                        ? selection.Reason
                        : selection.FilterSummary));
                return;
            }

            HuntEncounterPlan plan = selection.Plan;
            _pendingHuntPlan = plan;
            string reason;
            if (!_hunterRuntime.TryStart(
                hero,
                plan,
                ValueOrDefault(
                    _hunterSpawnDistance,
                    DefaultHunterSpawnDistance),
                out reason))
            {
                _hunterCatalog.RecordFailure(
                    _hunterRuntime.LastFailedProfileId);
                _pendingHuntPlan = null;
                _huntDirector.FailPlacement(tuning);
                Logger.LogWarning(
                    "Official encounter placement failed before request confirmation: "
                    + reason
                    + "; no danger budget was spent.");
                ShowDiagnosticSystem(
                    "EITD - Spawn failed: "
                    + (string.IsNullOrEmpty(
                            _hunterRuntime.LastFailedProfileId)
                        ? reason
                        : _hunterRuntime.LastFailedProfileId)
                    + "; cost 0");
                return;
            }

            Logger.LogInfo(
                "Official encounter placement requested: composition="
                + plan.DescribeComposition()
                + "; cost="
                + plan.DangerCost.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "; "
                + reason
                + ".");
            ShowDiagnosticSystem(
                "EITD - Plan "
                + HuntRegionResolver.ShortName(region)
                + " L"
                + playerLevel
                + " T"
                + _threat.Value.ToString(
                    "0",
                    CultureInfo.InvariantCulture)
                + ": "
                + plan.DescribeComposition()
                + "; cost "
                + plan.DangerCost.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                + "/"
                + _pacing.RemainingBudget.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                + "; elites "
                + (selectionContext.AllowEliteEnemies
                    ? "on>75"
                    : "off")
                + "; weights "
                + selection.WeightSummary);
        }

        private void HandleHunterRuntimeEvents(
            string currentSceneName = null)
        {
            if (_hunterRuntime == null)
            {
                return;
            }

            HunterRuntimeEvent runtimeEvent;
            while (_hunterRuntime.TryConsumeEvent(out runtimeEvent))
            {
                HuntTuning tuning = CurrentHuntTuning();
                switch (runtimeEvent.Kind)
                {
                    case HunterRuntimeEventKind.PlacementConfirmed:
                        HuntEncounterPlan confirmedPlan =
                            _pendingHuntPlan;
                        if (confirmedPlan == null)
                        {
                            _hunterRuntime.Cancel(
                                "the pending encounter plan was lost",
                                true);
                            _huntDirector.FailPlacement(tuning);
                            ShowDiagnosticSystem(
                                "EITD - Spawn cancelled: plan lost; cost 0");
                            break;
                        }
                        float before;
                        float after;
                        if (!_pacing.TrySpend(
                            confirmedPlan.DangerCost,
                            out before,
                            out after))
                        {
                            _hunterRuntime.Cancel(
                                "danger budget changed before placement confirmation",
                                true);
                            _huntDirector.FailPlacement(tuning);
                            Logger.LogWarning(
                                "Confirmed native placement was cancelled because the danger budget no longer covered its cost; no budget was spent.");
                            ShowDiagnosticSystem(
                                "EITD - Spawn cancelled: budget changed; cost 0");
                            _pendingHuntPlan = null;
                            break;
                        }

                        _huntDirector.ConfirmPlacement();
                        _hunterCatalog.RecordConfirmed(
                            confirmedPlan);
                        _activeHuntBudgetSpent = true;
                        _activeHuntDangerCost =
                            confirmedPlan.DangerCost;
                        _activeHuntSceneName =
                            currentSceneName ?? string.Empty;
                        Logger.LogInfo(
                            "Official encounter placement confirmed: composition="
                            + confirmedPlan.DescribeComposition()
                            + "; primary="
                            + confirmedPlan.Primary.Id
                            + "; locations="
                            + runtimeEvent.LocationId
                            + "; budget="
                            + before.ToString(
                                "0.##",
                                CultureInfo.InvariantCulture)
                            + " -> "
                            + after.ToString(
                                "0.##",
                                CultureInfo.InvariantCulture)
                            + ".");
                        ShowDiagnosticSystem(
                            "EITD - Active: "
                            + confirmedPlan.DescribeComposition()
                            + "; budget "
                            + before.ToString(
                                "0.#",
                                CultureInfo.InvariantCulture)
                            + " -> "
                            + after.ToString(
                                "0.#",
                                CultureInfo.InvariantCulture));
                        _pendingHuntPlan = null;
                        break;
                    case HunterRuntimeEventKind.PlacementFailed:
                        _hunterCatalog.RecordFailure(
                            runtimeEvent.ProfileId);
                        _pendingHuntPlan = null;
                        _huntDirector.FailPlacement(tuning);
                        Logger.LogWarning(
                            "Official hunter placement failed: "
                            + runtimeEvent.Reason
                            + "; no danger budget was spent.");
                        ShowDiagnosticSystem(
                            "EITD - Spawn failed: "
                            + runtimeEvent.Reason
                            + "; cost 0");
                        break;
                    case HunterRuntimeEventKind.HunterKilled:
                        ResolveHunt(
                            HuntResolution.HunterKilled,
                            runtimeEvent.Reason,
                            true,
                            false);
                        break;
                    case HunterRuntimeEventKind.Escaped:
                        ResolveHunt(
                            HuntResolution.Escaped,
                            runtimeEvent.Reason,
                            true,
                            false);
                        break;
                    case HunterRuntimeEventKind.LostTarget:
                        ResolveHunt(
                            HuntResolution.LostTarget,
                            runtimeEvent.Reason,
                            false,
                            false);
                        break;
                }
            }
        }

        private void ResolveHunt(
            HuntResolution resolution,
            string reason,
            bool grantRelief,
            bool discardLiveTarget)
        {
            if (_hunterRuntime == null)
            {
                return;
            }

            bool hadHunt = _hunterRuntime.IsInitializing
                || _hunterRuntime.IsActive
                || _huntDirector.State == DirectorState.Warning
                || _huntDirector.State == DirectorState.ActiveHunt;
            if (!hadHunt)
            {
                return;
            }

            bool wasActive = _huntDirector.State
                == DirectorState.ActiveHunt;
            if (_hunterRuntime.IsInitializing
                && resolution != HuntResolution.PlacementFailed)
            {
                resolution = HuntResolution.PlacementFailed;
                grantRelief = false;
            }

            _hunterRuntime.Cancel(reason, discardLiveTarget);
            HuntTuning tuning = CurrentHuntTuning();
            if (resolution == HuntResolution.PlacementFailed)
            {
                _huntDirector.FailPlacement(tuning);
            }
            else
            {
                _huntDirector.Resolve(resolution, tuning);
            }

            if (resolution == HuntResolution.LostTarget
                && _activeHuntBudgetSpent)
            {
                _pacing.Refund(_activeHuntDangerCost);
            }

            ThreatUpdateResult relief = new ThreatUpdateResult();
            if (grantRelief
                && resolution == HuntResolution.HunterKilled)
            {
                relief = _threat.Reduce(
                    ValueOrDefault(
                        _killThreatRelief,
                        DefaultKillThreatRelief),
                    ThreatChangeCause.OfficialHunterKilled);
                ObserveThreatUpdate(relief);
                ShowAtmosphere(
                    AtmosphereEventKind.HunterKilled,
                    _threat.Stage,
                    "eyes-in-the-dark-hunter-killed");
            }
            else if (grantRelief
                && (resolution == HuntResolution.Escaped
                    || resolution
                        == HuntResolution.InteriorEscape))
            {
                relief = _threat.Reduce(
                    ValueOrDefault(
                        _escapeThreatRelief,
                        DefaultEscapeThreatRelief),
                    ThreatChangeCause.HunterEscaped);
                ObserveThreatUpdate(relief);
                ShowAtmosphere(
                    AtmosphereEventKind.HunterEscaped,
                    _threat.Stage,
                    "eyes-in-the-dark-hunter-escaped");
            }

            Logger.LogInfo(
                "Official hunt resolved: outcome="
                + resolution
                + "; reason="
                + reason
                + "; wasActive="
                + (wasActive ? "true" : "false")
                + "; relief="
                + Math.Abs(
                    relief.CurrentThreat
                    - relief.PreviousThreat).ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                + "; threat="
                + _threat.Value.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; recovery="
                + _huntDirector.RecoveryRemainingSeconds.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                + "s; budget="
                + _pacing.RemainingBudget.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + ".");
            ShowDiagnosticSystem(
                "EITD - "
                + resolution
                + ": threat "
                + _threat.Value.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                + "; recovery "
                + _huntDirector.RecoveryRemainingSeconds.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                + "s; budget "
                + _pacing.RemainingBudget.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture));
            _activeHuntBudgetSpent = false;
            _activeHuntDangerCost = 0f;
            _pendingHuntPlan = null;
            _activeHuntSceneName = string.Empty;
        }

        private void ResetHuntRuntime(
            string reason,
            bool discardLiveTarget)
        {
            if (_hunterRuntime != null)
            {
                _hunterRuntime.Cancel(reason, discardLiveTarget);
            }
            _huntDirector.ResetNight(CurrentHuntTuning());
            _activeHuntBudgetSpent = false;
            _activeHuntDangerCost = 0f;
            _pendingHuntPlan = null;
            _activeHuntSceneName = string.Empty;
            if (_stalkerRuntime != null)
            {
                _stalkerRuntime.Cancel(
                    reason,
                    discardLiveTarget);
            }
            _stalkerDirector.ResetNight();
            ClearAmbientStalkerTracking();
        }

        private void ObserveContextTransition(RuntimeContext context)
        {
            FlushContinuousThreatDiagnostics(true);
            NightObservation observation = context.Observation;
            bool transient = observation.AtTitleScreen
                || observation.IsLoading
                || observation.IsTransitioning
                || observation.IsTraveling;
            if (!transient)
            {
                HuntRegion region = HuntRegionResolver.Resolve(
                    context.SceneName);
                ShowDiagnosticSystem(
                    "EITD - "
                    + (IsKnownValidWyrdNight(context)
                            && observation.IsOutdoor
                            && region == HuntRegion.Unknown
                        ? "Director suspended: unsupported scene "
                            + context.SceneName
                        : (context.Decision.State
                                == DirectorState.Roaming
                            ? _huntDirector.State
                            : context.Decision.State)
                            + ": "
                            + context.Decision.Reason)
                    + "; threat "
                    + _threat.Value.ToString(
                        "0.#",
                        CultureInfo.InvariantCulture));
            }

            if (IsKnownDaylight(context))
            {
                ShowAtmosphere(
                    AtmosphereEventKind.NightEnd,
                    _threat.Stage,
                    "eyes-in-the-dark-night-end");
                _hasKnownProtectionState = false;
                return;
            }

            if (!IsKnownValidWyrdNight(context)
                || !observation.IsOutdoor
                || (!context.IsProtected && !context.IsExposed))
            {
                return;
            }

            if (_hasKnownProtectionState
                && _lastKnownProtected != context.IsProtected)
            {
                ShowAtmosphere(
                    context.IsProtected
                        ? AtmosphereEventKind.ProtectionEntered
                        : AtmosphereEventKind.ProtectionLeft,
                    _threat.Stage,
                    context.IsProtected
                        ? "eyes-in-the-dark-protection-entered"
                        : "eyes-in-the-dark-protection-left");
            }

            _hasKnownProtectionState = true;
            _lastKnownProtected = context.IsProtected;
        }

        private void ObserveAtmosphericThreat(
            ThreatUpdateResult result)
        {
            if (result.Cause == ThreatChangeCause.DawnReset)
            {
                return;
            }

            if (result.StageChanged)
            {
                bool increased = result.CurrentThreat
                    > result.PreviousThreat;
                ShowAtmosphere(
                    increased
                        ? AtmosphereEventKind.UpwardStage
                        : AtmosphereEventKind.DownwardStage,
                    result.CurrentStage,
                    increased
                        ? "eyes-in-the-dark-threat-rise"
                        : "eyes-in-the-dark-threat-fall");
                return;
            }

            if (result.CurrentThreat - result.PreviousThreat >= 5f)
            {
                ShowAtmosphere(
                    AtmosphereEventKind.MajorThreatSurge,
                    result.CurrentStage,
                    "eyes-in-the-dark-threat-surge");
            }
        }

        private void ShowAtmosphere(
            AtmosphereEventKind eventKind,
            ThreatStage stage,
            string eventId)
        {
            if (_restAtmosphereReconciliationPending
                || _gft == null
                || _gftEnabled == null
                || !_gftEnabled.Value)
            {
                return;
            }

            GftNotificationPreset preset = _gftPreset == null
                ? GftNotificationPreset.Atmospheric
                : _gftPreset.Value;
            if (!AtmospherePolicy.ShouldNotify(preset, eventKind))
            {
                return;
            }

            string text = _atmosphereTexts.Select(eventKind, stage);
            bool stalkerEvent = eventKind
                    == AtmosphereEventKind.StalkerSighted
                || eventKind == AtmosphereEventKind.StalkerRetreated
                || eventKind == AtmosphereEventKind.StalkerVanished
                || eventKind == AtmosphereEventKind.StalkerProvoked
                || eventKind == AtmosphereEventKind.StalkerAwakened;
            if (!stalkerEvent
                && preset == GftNotificationPreset.Detailed
                && _gftDetailedExactThreat != null
                && _gftDetailedExactThreat.Value)
            {
                text += " [Threat "
                    + _threat.Value.ToString(
                        "0",
                        CultureInfo.InvariantCulture)
                    + "]";
            }

            string lane = eventKind == AtmosphereEventKind.NightBegin
                || eventKind == AtmosphereEventKind.NightEnd
                    ? "eyes-in-the-dark-night"
                    : eventKind == AtmosphereEventKind.BattlecryResponse
                        ? "eyes-in-the-dark-battlecry"
                        : eventKind == AtmosphereEventKind.HuntCommitted
                            || eventKind == AtmosphereEventKind.HunterKilled
                            || eventKind == AtmosphereEventKind.HunterEscaped
                                ? "eyes-in-the-dark-hunt"
                                : stalkerEvent
                                    ? "eyes-in-the-dark-stalker"
                                    : "eyes-in-the-dark-threat";
            if (_notificationCooldowns.CanEmit(
                lane,
                text,
                _activeRealTimeClock.Seconds,
                ValueOrDefault(
                    _gftCooldownSeconds,
                    DefaultGftCooldownSeconds)))
            {
                _gft.TryShowAtmosphere(
                    eventId,
                    text,
                    lane,
                    eventKind
                        == AtmosphereEventKind.HuntCommitted,
                    CurrentWyrdnessPalette());
            }
        }

        private WyrdnessPalette CurrentWyrdnessPalette()
        {
            return _wyrdnessPalette == null
                ? WyrdnessPalette.Purple
                : _wyrdnessPalette.Value;
        }

        private void ShowDiagnosticSystem(string text)
        {
            if (_gft == null
                || _diagnostics == null
                || !_diagnostics.Value
                || !CanShowGameplayDiagnostic())
            {
                return;
            }

            if (_notificationCooldowns.CanEmit(
                "eyes-in-the-dark-diagnostics",
                text,
                _activeRealTimeClock.Seconds,
                ValueOrDefault(
                    _diagnosticGftCooldownSeconds,
                    DefaultDiagnosticGftCooldownSeconds)))
            {
                _gft.TryShowDiagnostic(
                    "eyes-in-the-dark-diagnostic",
                    text);
            }
        }

        private static bool CanShowGameplayDiagnostic()
        {
            Hero hero = Hero.Current;
            return hero != null
                && !hero.HasBeenDiscarded
                && !IsLiveModel(World.Any<TitleScreenUI>())
                && !LoadingStates.IsLoadingWorld
                && !LoadingScreenUI.IsLoading
                && !IsLiveModel(World.Any<LoadingScreenUI>());
        }

        private void PrimeWyrdVisualsDuringTransientLoad()
        {
            if (_wyrdVisuals == null
                || !IsFeatureEnabled()
                || _wyrdVisualsEnabled == null
                || !_wyrdVisualsEnabled.Value)
            {
                return;
            }

            TransitionService transition = World.Services == null
                ? null
                : World.Services.TryGet<TransitionService>();
            bool transientNow = LoadingStates.IsLoadingWorld
                || LoadingScreenUI.IsLoading
                || IsLiveModel(World.Any<LoadingScreenUI>())
                || (transition != null && transition.InTransition)
                || _visualLoadContinuityPending
                || !_hasContext
                || (_hasContext
                    && !CurrentVisualIntent(
                        true,
                        _currentContext).HasValue);
            if (!transientNow)
            {
                return;
            }

            bool? active = TrySampleImmediateVisualState();
            if (!active.HasValue)
            {
                return;
            }

            _wyrdVisuals.Prime(
                active.Value,
                _threat.Value,
                CurrentWyrdVisualSettings());
        }

        private static bool? TrySampleImmediateVisualState()
        {
            Hero hero = Hero.Current;
            if (hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || hero.IsDying)
            {
                return null;
            }
            if (IsLiveModel(World.Any<TitleScreenUI>())
                || World.Services == null)
            {
                return false;
            }

            WyrdnessService wyrdnessService =
                World.Services.TryGet<WyrdnessService>();
            SceneService sceneService =
                World.Services.TryGet<SceneService>();
            if (wyrdnessService == null
                || sceneService == null
                || sceneService.ActiveSceneRef == null
                || string.IsNullOrEmpty(sceneService.ActiveSceneRef.Name))
            {
                return null;
            }

            SceneLifetimeEvents sceneLifetime = SceneLifetimeEvents.Get;
            if (!sceneService.IsOpenWorld
                || sceneLifetime.InInterior
                || !sceneService.AllowsWyrdnight
                || sceneService.IsPrologue)
            {
                return false;
            }

            GameRealTime clock = World.Any<GameRealTime>();
            if (clock == null || clock.HasBeenDiscarded)
            {
                return null;
            }
            return clock.WeatherTime.IsNight;
        }

        private void UpdateWyrdVisuals(
            bool featureEnabled,
            RuntimeContext context,
            float activeDelta,
            bool hadPreviousContext,
            RuntimeContext previousContext)
        {
            if (_wyrdVisuals == null)
            {
                return;
            }

            try
            {
                bool visualsEnabled = featureEnabled
                    && _wyrdVisualsEnabled != null
                    && _wyrdVisualsEnabled.Value;
                WyrdVisualSettings settings =
                    CurrentWyrdVisualSettings();
                bool? visualIntent = CurrentVisualIntent(
                    visualsEnabled,
                    context);
                bool stable = visualIntent.HasValue
                    && context.Observation.HasPlayableHero
                    && context.Observation.SceneKnown
                    && context.Observation.SceneInitialized
                    && !context.Observation.IsLoading
                    && !context.Observation.IsTransitioning
                    && !context.Observation.IsTraveling;
                bool beginLoadThreatTransition =
                    _visualLoadContinuityPending
                    && stable
                    && visualIntent.Value;
                if (stable)
                {
                    _visualLoadContinuityPending = false;
                }

                GameRealTime clock = World.Any<GameRealTime>();
                float weatherRate = clock == null
                    ? 0f
                    : clock.WeatherSecondsPerRealSecond;
                float duskBlendLimit = visualIntent.HasValue ? 0f : 1f;
                if (visualIntent.HasValue
                    && visualIntent.Value)
                {
                    duskBlendLimit = WyrdVisualMath.CenteredDuskBlend(
                        true,
                        WorldTimescalePolicy.ElapsedNightRealSeconds(
                            context.NightProgress,
                            weatherRate),
                        settings.TransitionSeconds);
                }
                else if (visualIntent.HasValue
                    && IsKnownDaylightForVisuals(context)
                    && context.Observation.IsOutdoor
                    && context.Observation.AllowsWyrdNight
                    && clock != null)
                {
                    duskBlendLimit = WyrdVisualMath.CenteredDuskBlend(
                        false,
                        WorldTimescalePolicy.RemainingDaylightRealSeconds(
                            clock.WeatherTime.DayTime,
                            weatherRate),
                        settings.TransitionSeconds);
                }

                bool preDuskActive = visualIntent.HasValue
                    && !visualIntent.Value
                    && duskBlendLimit > 0f;
                bool holdingTransientState = !visualIntent.HasValue;
                bool active = visualIntent.HasValue
                    ? visualIntent.Value || preDuskActive
                    : _wyrdVisuals.TargetActive;
                bool canContinueTransition = holdingTransientState
                    || (visualsEnabled
                        && IsStableExteriorVisualPhase(context));
                bool beginNaturalTransition = canContinueTransition
                    && hadPreviousContext
                    && string.Equals(
                        previousContext.SceneName,
                        context.SceneName,
                        StringComparison.Ordinal)
                    && IsStableExteriorVisualPhase(previousContext)
                    && !_wyrdVisuals.TargetActive
                    && active
                    && duskBlendLimit > 0f
                    && duskBlendLimit < 1f;
                float phaseBlendLimit = active
                    ? duskBlendLimit
                    : 1f;
                if (visualIntent.HasValue && visualIntent.Value)
                {
                    phaseBlendLimit = Mathf.Min(
                        phaseBlendLimit,
                        WyrdVisualMath.PreDawnBlendLimit(
                            WorldTimescalePolicy.RemainingNightRealSeconds(
                                context.NightProgress,
                                weatherRate),
                            settings.TransitionSeconds));
                }
                if (beginLoadThreatTransition)
                {
                    _wyrdVisuals.BeginLoadThreatTransition(
                        _threat.Value,
                        LoadThreatVisualTransitionSeconds);
                }
                _wyrdVisuals.Update(
                    active,
                    activeDelta,
                    beginNaturalTransition,
                    canContinueTransition,
                    phaseBlendLimit,
                    _threat.Value,
                    settings);
            }
            catch (Exception exception)
            {
                if (!_wyrdVisualFailureLogged)
                {
                    _wyrdVisualFailureLogged = true;
                    Logger.LogWarning(
                        "The optional Wyrdnight environment presentation failed and was disabled; gameplay remains active: "
                        + exception.GetBaseException().Message);
                    ShowDiagnosticSystem(
                        "EITD - Wyrd visuals disabled after an isolated failure; gameplay continues");
                }
                _wyrdVisuals.Release();
                _wyrdVisuals = null;
            }
        }

        private bool WyrdVisualResponseEnabled()
        {
            return _wyrdVisualsEnabled == null
                || _wyrdVisualsEnabled.Value;
        }

        private static bool IsStableExteriorVisualPhase(
            RuntimeContext context)
        {
            return context.Observation.IsOutdoor
                && (IsKnownValidWyrdNightForVisuals(context)
                    || IsKnownDaylightForVisuals(context));
        }

        private static bool? CurrentVisualIntent(
            bool visualsEnabled,
            RuntimeContext context)
        {
            if (!visualsEnabled)
            {
                return false;
            }

            NightObservation observation = context.Observation;
            if (observation.AtTitleScreen
                || (observation.HasPlayableHero && !observation.HeroAlive)
                || observation.IsPrologue)
            {
                return false;
            }
            if (observation.IsLoading
                || observation.IsTransitioning
                || observation.IsTraveling
                || !observation.HasPlayableHero
                || !observation.SceneKnown
                || !observation.SceneInitialized
                || !observation.HasWorldTime
                || !observation.HasHeroNightState)
            {
                return null;
            }
            if (!observation.IsOutdoor || !observation.AllowsWyrdNight)
            {
                return false;
            }
            if (observation.GameSaysNight && observation.HeroSaysNight)
            {
                return true;
            }
            if (!observation.GameSaysNight && !observation.HeroSaysNight)
            {
                return false;
            }
            return null;
        }

        private WyrdVisualSettings CurrentWyrdVisualSettings()
        {
            return new WyrdVisualSettings
            {
                Palette = WyrdVisualResponseEnabled()
                    && _wyrdnessPalette != null
                        ? _wyrdnessPalette.Value
                        : WyrdnessPalette.Purple,
                PurpleExposureMultiplier = ValueOrDefault(
                    _purpleExposureMultiplier,
                    DefaultPurpleExposureMultiplier),
                PurpleExposureCompensation = ValueOrDefault(
                    _purpleExposureCompensation,
                    DefaultPurpleExposureCompensation),
                PurpleIndirectDiffuseMultiplier = ValueOrDefault(
                    _purpleIndirectDiffuseMultiplier,
                    DefaultPurpleIndirectDiffuseMultiplier),
                ThreatSmoothingHalfLifeSeconds = ValueOrDefault(
                    _threatVisualSmoothingSeconds,
                    DefaultThreatVisualSmoothingSeconds),
                MinimumThreatScale = ValueOrDefault(
                    _minimumThreatVisualScale,
                    DefaultMinimumThreatVisualScale),
                MaximumThreatScale = ValueOrDefault(
                    _maximumThreatVisualScale,
                    DefaultMaximumThreatVisualScale),
                ThreatRedColor = _threatRedColor == null
                    ? DefaultThreatRedColor
                    : _threatRedColor.Value,
                MaximumRedBlend = ValueOrDefault(
                    _maximumThreatRedBlend,
                    DefaultMaximumThreatRedBlend),
                MoonSurfaceColor = _moonSurfaceColor == null
                    ? DefaultMoonSurfaceColor
                    : _moonSurfaceColor.Value,
                MoonSurfaceTintStrength = ValueOrDefault(
                    _moonSurfaceTintStrength,
                    DefaultMoonSurfaceTintStrength),
                MoonSurfaceIntensity = ValueOrDefault(
                    _moonSurfaceIntensity,
                    DefaultMoonSurfaceIntensity),
                TintMoonCorona = _tintMoonCorona == null
                    || _tintMoonCorona.Value,
                MoonCoronaColor = _moonCoronaColor == null
                    ? DefaultMoonCoronaColor
                    : _moonCoronaColor.Value,
                MoonCoronaIntensity = ValueOrDefault(
                    _moonCoronaIntensity,
                    DefaultMoonCoronaIntensity),
                MoonlightColor = _moonlightColor == null
                    ? DefaultMoonlightColor
                    : _moonlightColor.Value,
                MoonlightTintStrength = ValueOrDefault(
                    _moonlightTintStrength,
                    DefaultMoonlightTintStrength),
                TintNightSkyAmbient = _tintNightSkyAmbient == null
                    || _tintNightSkyAmbient.Value,
                NightSkyAmbientColor = _nightSkyAmbientColor == null
                    ? DefaultNightSkyAmbientColor
                    : _nightSkyAmbientColor.Value,
                NightSkyAmbientTintStrength = ValueOrDefault(
                    _nightSkyAmbientTintStrength,
                    DefaultNightSkyAmbientTintStrength),
                TintBonfireProtectionBubble =
                    _tintBonfireProtectionBubble == null
                    || _tintBonfireProtectionBubble.Value,
                ProtectionBubbleColor = _protectionBubbleColor == null
                    ? DefaultProtectionBubbleColor
                    : _protectionBubbleColor.Value,
                ProtectionBubbleIntensity = ValueOrDefault(
                    _protectionBubbleIntensity,
                    DefaultProtectionBubbleIntensity),
                ProtectionBubbleBorderIntensity = ValueOrDefault(
                    _protectionBubbleBorderIntensity,
                    DefaultProtectionBubbleBorderIntensity),
                TransitionSeconds = ValueOrDefault(
                    _wyrdVisualTransitionSeconds,
                    DefaultWyrdVisualTransitionSeconds)
            };
        }

        private void UpdateBoundary(
            bool featureEnabled,
            RuntimeContext context)
        {
            if (_boundary == null)
            {
                return;
            }

            bool enabled = featureEnabled
                && _boundaryEnabled != null
                && _boundaryEnabled.Value;
            try
            {
                _boundary.Update(
                    enabled,
                    CurrentBoundarySettings(),
                    _threat.Value,
                    context.Observation.HasPlayableHero
                        && context.Observation.SceneKnown
                        && context.Observation.SceneInitialized
                        && !context.Observation.AtTitleScreen
                        && !context.Observation.IsLoading
                        && !context.Observation.IsTransitioning);
            }
            catch (Exception exception)
            {
                if (!_boundaryFailureLogged)
                {
                    _boundaryFailureLogged = true;
                    Logger.LogWarning(
                        "The optional Wyrd boundary presentation failed and was disabled; threat and encounters remain active: "
                        + exception.GetBaseException().Message);
                    ShowDiagnosticSystem(
                        "EITD - Boundary disabled after an isolated failure; gameplay continues");
                }
                _boundary.Release();
                _boundary = null;
            }
        }

        private BoundarySettings CurrentBoundarySettings()
        {
            string configuredColor = _boundaryColor == null
                ? DefaultBoundaryColor
                : _boundaryColor.Value;
            if (!_hasParsedBoundaryColor
                || !string.Equals(
                    configuredColor,
                    _parsedBoundaryColorText,
                    StringComparison.Ordinal))
            {
                Color color;
                if (!ColorUtility.TryParseHtmlString(
                    configuredColor,
                    out color))
                {
                    ColorUtility.TryParseHtmlString(
                        DefaultBoundaryColor,
                        out color);
                    if (!string.Equals(
                        configuredColor,
                        _lastInvalidBoundaryColor,
                        StringComparison.Ordinal))
                    {
                        _lastInvalidBoundaryColor = configuredColor;
                        Logger.LogWarning(
                            "BoundaryColor is invalid; using "
                            + DefaultBoundaryColor
                            + ".");
                    }
                }
                else
                {
                    _lastInvalidBoundaryColor = null;
                }

                _parsedBoundaryColorText = configuredColor;
                _parsedBoundaryColor = color;
                _hasParsedBoundaryColor = true;
            }

            return new BoundarySettings
            {
                RenderMode = _boundaryRenderMode == null
                    ? BoundaryRenderMode.Layered
                    : _boundaryRenderMode.Value,
                Palette = WyrdVisualResponseEnabled()
                    && _wyrdnessPalette != null
                        ? _wyrdnessPalette.Value
                        : WyrdnessPalette.Purple,
                Color = _parsedBoundaryColor,
                ThreatRedColor = ParsedThreatRedColor(),
                MaximumRedBlend = WyrdVisualResponseEnabled()
                    ? ValueOrDefault(
                        _maximumThreatRedBlend,
                        DefaultMaximumThreatRedBlend)
                    : 0f,
                ThreatVisualScale = WyrdVisualMath.ThreatScale(
                    _threat.Value,
                    WyrdVisualResponseEnabled()
                        ? ValueOrDefault(
                            _minimumThreatVisualScale,
                            DefaultMinimumThreatVisualScale)
                        : 1f,
                    WyrdVisualResponseEnabled()
                        ? ValueOrDefault(
                            _maximumThreatVisualScale,
                            DefaultMaximumThreatVisualScale)
                        : 1f),
                HdrIntensity = ValueOrDefault(
                    _boundaryBrightness,
                    DefaultBoundaryBrightness)
                    * BoundaryVanillaHdrBaseline,
                NearRadius = ValueOrDefault(
                    _boundaryNearRadius,
                    DefaultBoundaryNearRadius),
                NearIntensityMultiplier = ValueOrDefault(
                    _boundaryNearIntensity,
                    DefaultBoundaryNearIntensity),
                NearThickness = ValueOrDefault(
                    _boundaryNearThickness,
                    DefaultBoundaryNearThickness),
                MiddleRadius = ValueOrDefault(
                    _boundaryMiddleRadius,
                    DefaultBoundaryMiddleRadius),
                MiddleIntensityMultiplier = ValueOrDefault(
                    _boundaryMiddleIntensity,
                    DefaultBoundaryMiddleIntensity),
                MiddleThickness = ValueOrDefault(
                    _boundaryMiddleThickness,
                    DefaultBoundaryMiddleThickness),
                OuterRadius = ValueOrDefault(
                    _boundaryVisualRadius,
                    DefaultBoundaryOuterRadius),
                OuterIntensityMultiplier = ValueOrDefault(
                    _boundaryOuterIntensity,
                    DefaultBoundaryOuterIntensity),
                OuterThickness = ValueOrDefault(
                    _boundaryThickness,
                    DefaultBoundaryOuterThickness),
                PulseEnabled = _boundaryPulseEnabled == null
                    || _boundaryPulseEnabled.Value,
                PulseAmount = ValueOrDefault(
                    _boundaryPulseAmount,
                    DefaultBoundaryPulseAmount),
                PulseMinimumSeconds = ValueOrDefault(
                    _boundaryPulseMinimumSeconds,
                    DefaultBoundaryPulseMinimumSeconds),
                PulseMaximumSeconds = ValueOrDefault(
                    _boundaryPulseMaximumSeconds,
                    DefaultBoundaryPulseMaximumSeconds)
            };
        }

        private Color ParsedThreatRedColor()
        {
            string configured = _threatRedColor == null
                ? DefaultThreatRedColor
                : _threatRedColor.Value;
            if (!_hasParsedThreatRedColor
                || !string.Equals(
                    configured,
                    _parsedThreatRedColorText,
                    StringComparison.Ordinal))
            {
                Color color;
                if (!ColorUtility.TryParseHtmlString(configured, out color))
                {
                    ColorUtility.TryParseHtmlString(
                        DefaultThreatRedColor,
                        out color);
                    if (!string.Equals(
                        configured,
                        _lastInvalidThreatRedColor,
                        StringComparison.Ordinal))
                    {
                        _lastInvalidThreatRedColor = configured;
                        Logger.LogWarning(
                            "ThreatRedColor is invalid; using "
                            + DefaultThreatRedColor
                            + ".");
                    }
                }
                else
                {
                    _lastInvalidThreatRedColor = null;
                }

                _parsedThreatRedColorText = configured;
                _parsedThreatRedColor = color;
                _hasParsedThreatRedColor = true;
            }
            return _parsedThreatRedColor;
        }

        internal bool SetExternalMeterPlacement(
            string requesterPluginGuid,
            bool placeBelow)
        {
            if (!string.Equals(
                requesterPluginGuid,
                GloriousUiPluginGuid,
                StringComparison.Ordinal))
            {
                return false;
            }

            if (_placeMeterBelowResourceBars != placeBelow)
            {
                _placeMeterBelowResourceBars = placeBelow;
                Logger.LogInfo(
                    placeBelow
                        ? "Glorious UI requested Wyrd Threat placement below the vanilla resource bars."
                        : "Glorious UI released Wyrd Threat placement; restored the standalone row above health.");
            }
            return true;
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);
            PatchWyrdVisuals();
            PatchHeroHud();
            PatchGameplayLoad();
            PatchRest();
            PatchRestClock();
            PatchQuickWeatherTime();
        }

        private void PatchWyrdVisuals()
        {
            if (_wyrdVisuals == null)
            {
                return;
            }
            try
            {
                _wyrdVisuals.Patch(_harmony);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not attach the Wyrdnight environment presentation; gameplay remains active: "
                    + exception.GetBaseException().Message);
                _wyrdVisuals.Release();
                _wyrdVisuals = null;
            }
        }

        private void PatchRest()
        {
            try
            {
                MethodInfo canRest = AccessTools.PropertyGetter(
                    typeof(HeroDevelopment),
                    nameof(HeroDevelopment.CanRest));
                MethodInfo rest = AccessTools.Method(
                    typeof(RestPopupUI),
                    nameof(RestPopupUI.Rest),
                    Type.EmptyTypes);
                MethodInfo willBeSurprised = AccessTools.PropertyGetter(
                    typeof(RestPopupUI),
                    "WillBeSurprisedByWyrdNight");
                MethodInfo willSkipBeInterrupted = AccessTools.Method(
                    typeof(GameRealTime),
                    nameof(GameRealTime.WillSkipTimeBeInterrupted),
                    new[]
                    {
                        typeof(float),
                        typeof(bool),
                        typeof(float).MakeByRefType()
                    });
                MethodInfo canRestPostfix = AccessTools.Method(
                    typeof(RestPatch),
                    nameof(RestPatch.AfterCanRest));
                MethodInfo restPrefix = AccessTools.Method(
                    typeof(RestPatch),
                    nameof(RestPatch.BeforeRest));
                MethodInfo surprisePostfix = AccessTools.Method(
                    typeof(RestPatch),
                    nameof(RestPatch.AfterWillBeSurprised));
                MethodInfo interruptionPostfix = AccessTools.Method(
                    typeof(RestPatch),
                    nameof(RestPatch.AfterWillSkipTimeBeInterrupted));
                MethodInfo fireplaceInitialize = AccessTools.Method(
                    typeof(VFireplaceUI),
                    "OnInitialize",
                    Type.EmptyTypes);
                MethodInfo fireplaceRefresh = AccessTools.Method(
                    typeof(VWyrdRepellingFireplaceUI),
                    nameof(VWyrdRepellingFireplaceUI.RefreshActions),
                    Type.EmptyTypes);
                MethodInfo fireplaceDiscard = AccessTools.Method(
                    typeof(VFireplaceUI),
                    "OnDiscard",
                    Type.EmptyTypes);
                MethodInfo fireplaceInitializePostfix = AccessTools.Method(
                    typeof(RestPatch),
                    nameof(RestPatch.AfterFireplaceInitialize));
                MethodInfo fireplaceRefreshPostfix = AccessTools.Method(
                    typeof(RestPatch),
                    nameof(RestPatch.AfterFireplaceRefresh));
                MethodInfo fireplaceDiscardPostfix = AccessTools.Method(
                    typeof(RestPatch),
                    nameof(RestPatch.AfterFireplaceDiscard));
                if (canRest == null
                    || rest == null
                    || willBeSurprised == null
                    || willSkipBeInterrupted == null
                    || canRestPostfix == null
                    || restPrefix == null
                    || surprisePostfix == null
                    || interruptionPostfix == null
                    || fireplaceInitialize == null
                    || fireplaceRefresh == null
                    || fireplaceDiscard == null
                    || fireplaceInitializePostfix == null
                    || fireplaceRefreshPostfix == null
                    || fireplaceDiscardPostfix == null
                    || FireplaceRestControlField == null
                    || FireplaceRestButtonProperty == null)
                {
                    throw new MissingMethodException(
                        "the native rest availability targets were not found");
                }

                _harmony.Patch(
                    canRest,
                    postfix: new HarmonyMethod(canRestPostfix));
                _harmony.Patch(
                    rest,
                    prefix: new HarmonyMethod(restPrefix));
                _harmony.Patch(
                    willBeSurprised,
                    postfix: new HarmonyMethod(surprisePostfix));
                _harmony.Patch(
                    willSkipBeInterrupted,
                    postfix: new HarmonyMethod(interruptionPostfix));
                _harmony.Patch(
                    fireplaceInitialize,
                    postfix: new HarmonyMethod(
                        fireplaceInitializePostfix));
                _harmony.Patch(
                    fireplaceRefresh,
                    postfix: new HarmonyMethod(
                        fireplaceRefreshPostfix));
                _harmony.Patch(
                    fireplaceDiscard,
                    postfix: new HarmonyMethod(
                        fireplaceDiscardPostfix));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not attach Wyrdnight rest safety; native rest behavior remains available: "
                    + exception.GetBaseException().Message);
            }
        }

        private void PatchRestClock()
        {
            try
            {
                MethodInfo initialize = AccessTools.Method(
                    typeof(VRestPopupUI),
                    "OnInitialize",
                    Type.EmptyTypes);
                MethodInfo refresh = AccessTools.Method(
                    typeof(VRestPopupUI),
                    "Refresh",
                    Type.EmptyTypes);
                MethodInfo setHourFromAngle = AccessTools.Method(
                    typeof(VRestPopupUI),
                    "SetHourChangeBasedOnAngle",
                    new[] { typeof(float) });
                MethodInfo initializePostfix = AccessTools.Method(
                    typeof(RestClockPatch),
                    nameof(RestClockPatch.AfterInitialize));
                MethodInfo refreshPostfix = AccessTools.Method(
                    typeof(RestClockPatch),
                    nameof(RestClockPatch.AfterRefresh));
                MethodInfo anglePrefix = AccessTools.Method(
                    typeof(RestClockPatch),
                    nameof(RestClockPatch.BeforeSetHourChangeBasedOnAngle));
                if (initialize == null
                    || refresh == null
                    || setHourFromAngle == null
                    || initializePostfix == null
                    || refreshPostfix == null
                    || anglePrefix == null)
                {
                    throw new MissingMethodException(
                        "the native rest-clock initialization target was not found");
                }

                _harmony.Patch(
                    initialize,
                    postfix: new HarmonyMethod(initializePostfix));
                _harmony.Patch(
                    refresh,
                    postfix: new HarmonyMethod(refreshPostfix));
                _harmony.Patch(
                    setHourFromAngle,
                    prefix: new HarmonyMethod(anglePrefix));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not attach the Wyrdnight rest-clock presentation; the native rest clock remains available: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void AttachRestClock(VRestPopupUI view)
        {
            if (!OwnsRestMenu())
            {
                RestClockOverlay.Detach(view);
                return;
            }

            try
            {
                RestClockOverlay.Attach(
                    view,
                    _restClockLabelFormat == null
                        ? RestClockLabelFormat.TwelveHour
                        : _restClockLabelFormat.Value);
                _restClockFailureLogged = false;
            }
            catch (Exception exception)
            {
                if (!_restClockFailureLogged)
                {
                    _restClockFailureLogged = true;
                    Logger.LogWarning(
                        "The optional Wyrdnight rest-clock presentation failed; the native clock remains usable: "
                        + exception.GetBaseException().Message);
                    ShowDiagnosticSystem(
                        "EITD - Rest clock overlay unavailable; native clock retained");
                }
            }
        }

        internal void RefreshRestClock(VRestPopupUI view)
        {
            if (!OwnsRestMenu())
            {
                RestClockOverlay.Detach(view);
                return;
            }

            RestClockOverlay.RefreshAfterNative(
                view,
                _restClockLabelFormat == null
                    ? RestClockLabelFormat.TwelveHour
                    : _restClockLabelFormat.Value);
        }

        internal bool UsesNoonAtTopRestClock(VRestPopupUI view)
        {
            return OwnsRestMenu()
                && RestClockOverlay.UsesNoonAtTop(view);
        }

        private bool OwnsRestMenu()
        {
            return IsFeatureEnabled()
                && (_ownRestMenu == null || _ownRestMenu.Value);
        }

        private void PatchQuickWeatherTime()
        {
            try
            {
                MethodInfo attach = AccessTools.Method(
                    typeof(VCQuickWeatherTime),
                    "OnAttach",
                    Type.EmptyTypes);
                MethodInfo postfix = AccessTools.Method(
                    typeof(QuickWeatherTimePatch),
                    nameof(QuickWeatherTimePatch.AfterAttach));
                if (attach == null
                    || postfix == null
                    || QuickWeatherTimeTextField == null)
                {
                    throw new MissingMethodException(
                        "the native quick-use weather-time target was not found");
                }

                _harmony.Patch(
                    attach,
                    postfix: new HarmonyMethod(postfix));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not attach the optional 12-hour quick-use time format; the native 24-hour time remains active: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void FormatQuickWeatherTime(VCQuickWeatherTime view)
        {
            if (!IsFeatureEnabled()
                || view == null
                || (_restClockLabelFormat != null
                    && _restClockLabelFormat.Value
                        == RestClockLabelFormat.TwentyFourHour))
            {
                return;
            }

            try
            {
                GameRealTime clock = World.Any<GameRealTime>();
                TextMeshProUGUI timeText = QuickWeatherTimeTextField.GetValue(
                    view) as TextMeshProUGUI;
                if (clock == null || timeText == null)
                {
                    return;
                }

                int hour = clock.WeatherTime.Hour;
                int minute = clock.WeatherTime.Minutes;
                int twelveHour = hour % 12;
                if (twelveHour == 0)
                {
                    twelveHour = 12;
                }
                timeText.SetText(
                    twelveHour.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + minute.ToString("00", CultureInfo.InvariantCulture)
                    + (hour < 12 ? " AM" : " PM"));
                _quickWeatherTimeFailureLogged = false;
            }
            catch (Exception exception)
            {
                if (!_quickWeatherTimeFailureLogged)
                {
                    _quickWeatherTimeFailureLogged = true;
                    Logger.LogWarning(
                        "Could not format the quick-use time; its native 24-hour text remains usable: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void PatchHeroHud()
        {
            try
            {
                MethodInfo afterInitialized = AccessTools.Method(
                    typeof(VHeroHUD),
                    "AfterFullyInitialized",
                    Type.EmptyTypes);
                MethodInfo onDiscard = AccessTools.Method(
                    typeof(VHeroHUD),
                    "OnDiscard",
                    Type.EmptyTypes);
                if (afterInitialized == null || onDiscard == null)
                {
                    throw new MissingMethodException(
                        "VHeroHUD lifecycle methods were not found");
                }

                _harmony.Patch(
                    afterInitialized,
                    postfix: new HarmonyMethod(
                        typeof(HeroHudPatch),
                        nameof(HeroHudPatch.AfterFullyInitialized)));
                _harmony.Patch(
                    onDiscard,
                    postfix: new HarmonyMethod(
                        typeof(HeroHudPatch),
                        nameof(HeroHudPatch.AfterDiscard)));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not attach the Wyrd Threat meter to the Hero HUD; threat gameplay remains active: "
                    + exception.GetBaseException().Message);
            }
        }

        private void PatchGameplayLoad()
        {
            try
            {
                MethodInfo loadSlot = AccessTools.Method(
                    typeof(LoadSave),
                    "LoadSaveSlotToCache",
                    new[] { typeof(SaveSlot) });
                MethodInfo loadGameplay = AccessTools.Method(
                    typeof(LoadSave),
                    "LoadOnlyGameplayToCache",
                    new[] { typeof(SaveSlot) });
                MethodInfo prefix = AccessTools.Method(
                    typeof(GameplayLoadPatch),
                    nameof(GameplayLoadPatch.BeforeLoad));
                if (loadSlot == null
                    || loadGameplay == null
                    || prefix == null)
                {
                    throw new MissingMethodException(
                        "the native gameplay-load entry points were not found");
                }

                HarmonyMethod patch = new HarmonyMethod(prefix);
                _harmony.Patch(loadSlot, prefix: patch);
                _harmony.Patch(loadGameplay, prefix: patch);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not patch gameplay-load reconstruction; startup reconstruction remains active: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void NotifyGameplayLoad()
        {
            _visualLoadContinuityPending = true;
            ResolveHunt(
                HuntResolution.GameplayLoad,
                "native gameplay load started",
                false,
                true);
            ResetHuntRuntime(
                "native gameplay load started",
                true);
            _threat.NotifyLoad();
            _activity.ResetNight();
            _pacing.Reset();
            _notificationCooldowns.Reset();
            ResetBattlecryState();
            ResetRestRisk();
            _hasKnownProtectionState = false;
            _restAtmosphereReconciliationPending = false;
            LogDiagnostic(
                "Threat state cleared at the native gameplay-load entry point; no catch-up work was retained.");
        }

        private void ResetRestRisk()
        {
            _restRisk.Reset();
            _activeRestRiskPopup = null;
            _activeRestRiskWindow = new RestRiskWindow();
            _hasActiveRestRiskWindow = false;
            _restRiskPreparedForUpcomingNight = false;
            _pendingRestHunt = false;
        }

        internal void RegisterRestAvailabilityView(VFireplaceUI view)
        {
            _activeFireplaceView = view;
            RefreshActiveRestAvailability();
        }

        internal void UnregisterRestAvailabilityView(VFireplaceUI view)
        {
            if (_activeFireplaceView == view)
            {
                _activeFireplaceView = null;
            }
        }

        private void RefreshActiveRestAvailability()
        {
            VFireplaceUI view = _activeFireplaceView;
            if (view == null)
            {
                _activeFireplaceView = null;
                return;
            }

            try
            {
                object restControl =
                    FireplaceRestControlField.GetValue(view);
                ARButton restButton = restControl == null
                    ? null
                    : FireplaceRestButtonProperty.GetValue(
                        restControl,
                        null) as ARButton;
                Hero hero = Hero.Current;
                if (restButton == null
                    || hero == null
                    || hero.HasBeenDiscarded)
                {
                    return;
                }

                bool interactable = hero.Development.CanRest;
                if (restButton.Interactable != interactable)
                {
                    restButton.Interactable = interactable;
                }
                _restAvailabilityFailureLogged = false;
            }
            catch (Exception exception)
            {
                if (!_restAvailabilityFailureLogged)
                {
                    _restAvailabilityFailureLogged = true;
                    Logger.LogWarning(
                        "Could not refresh the fireplace REST control; the silent final rest guard remains active: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        internal bool TryBeginRest(RestPopupUI restPopup)
        {
            _activeRestRiskPopup = null;
            _hasActiveRestRiskWindow = false;
            if (restPopup == null || restPopup.HasBeenDiscarded)
            {
                return true;
            }
            if (!IsFeatureEnabled())
            {
                return true;
            }

            RuntimeContext context;
            try
            {
                context = SampleContext();
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not evaluate Wyrdnight rest safety; native rest remains available: "
                    + exception.GetBaseException().Message);
                return true;
            }

            bool safelyResting;
            bool allowUnprotectedWyrdnightRest =
                _allowUnprotectedWyrdnightRest != null
                && _allowUnprotectedWyrdnightRest.Value;
            try
            {
                safelyResting = restPopup.IsSafelyResting;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not evaluate the native fueled-rest protection state; native rest remains available: "
                    + exception.GetBaseException().Message);
                return true;
            }

            bool activeUnprotectedWyrdnight =
                NightStateEvaluator.IsActiveWyrdnightPhaseForRest(
                    context.Observation)
                && !safelyResting;
            if (!NightStateEvaluator.CanBeginRest(
                true,
                allowUnprotectedWyrdnightRest,
                context.Observation,
                safelyResting)
                || (activeUnprotectedWyrdnight
                    && _restRisk.Disturbed))
            {
                restPopup.Close();
                LogDiagnostic(
                    _restRisk.Disturbed
                        ? "Rest remained disabled because unprotected sleep had already been interrupted during this Wyrdnight."
                        : "Rest remained disabled during the active Wyrdnight because the Hero was outside a protective boundary.");
                return false;
            }

            if (!safelyResting
                && context.Observation.IsOutdoor
                && context.Observation.AllowsWyrdNight
                && !context.Observation.IsPrologue)
            {
                GameRealTime clock = World.Any<GameRealTime>();
                RestRiskWindow window;
                if (clock != null
                    && !clock.HasBeenDiscarded
                    && RestRiskPolicy.TryCreateWindow(
                        clock.WeatherTime.DayTime,
                        restPopup.HourValueChange,
                        out window))
                {
                    _activeRestRiskPopup = restPopup;
                    _activeRestRiskWindow = window;
                    _hasActiveRestRiskWindow = true;
                    _restRiskPreparedForUpcomingNight = true;
                }
            }

            _restAtmosphereReconciliationPending = true;
            LogDiagnostic(
                "Rest began; atmospheric transitions will reconcile against the final post-rest phase.");
            return true;
        }

        internal bool CanUseNativeRest(bool nativeCanRest)
        {
            if (!nativeCanRest)
            {
                return nativeCanRest;
            }
            if (!IsFeatureEnabled())
            {
                return nativeCanRest;
            }
            if (!OwnsRestMenu())
            {
                return nativeCanRest;
            }

            RuntimeContext context;
            try
            {
                context = SampleContext();
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not evaluate Wyrdnight rest availability; the native rest result remains authoritative: "
                    + exception.GetBaseException().Message);
                return nativeCanRest;
            }

            if (!NightStateEvaluator.IsActiveWyrdnightPhaseForRest(
                context.Observation))
            {
                return nativeCanRest;
            }

            bool allowUnprotectedWyrdnightRest =
                _allowUnprotectedWyrdnightRest != null
                && _allowUnprotectedWyrdnightRest.Value;
            if (allowUnprotectedWyrdnightRest
                && !_restRisk.Disturbed)
            {
                return nativeCanRest;
            }

            Hero hero = Hero.Current;
            WyrdnessService wyrdnessService = World.Services == null
                ? null
                : World.Services.TryGet<WyrdnessService>();
            if (hero == null
                || hero.HasBeenDiscarded
                || wyrdnessService == null)
            {
                return nativeCanRest;
            }

            bool insideProtectiveBoundary;
            try
            {
                insideProtectiveBoundary =
                    wyrdnessService.IsInRepeller(hero.Coords);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not evaluate the protective boundary for rest availability; the native rest result remains authoritative: "
                    + exception.GetBaseException().Message);
                return nativeCanRest;
            }

            return NightStateEvaluator.CanBeginRest(
                true,
                allowUnprotectedWyrdnightRest
                    && !_restRisk.Disturbed,
                context.Observation,
                insideProtectiveBoundary);
        }

        internal bool ShouldSuppressNativeWyrdnightSurprise(
            RestPopupUI restPopup,
            bool nativeResult)
        {
            return nativeResult
                && _hasActiveRestRiskWindow
                && ReferenceEquals(_activeRestRiskPopup, restPopup);
        }

        internal void ApplyRestInterruptionRisk(
            float requestedHours,
            bool safelySkipping,
            ref float hoursUntilInterrupt,
            ref bool interrupted)
        {
            if (!_hasActiveRestRiskWindow || safelySkipping)
            {
                _activeRestRiskPopup = null;
                _hasActiveRestRiskWindow = false;
                return;
            }

            RestRiskWindow window = _activeRestRiskWindow;
            _activeRestRiskPopup = null;
            _hasActiveRestRiskWindow = false;
            if (requestedHours <= 0f)
            {
                return;
            }

            RestRiskDecision decision = _restRisk.Evaluate(
                window,
                _threat.Value,
                ValueOrDefault(
                    _restInterruptionChanceAtZeroThreat,
                    DefaultRestInterruptionChanceAtZeroThreat),
                ValueOrDefault(
                    _restInterruptionChanceAtMaximumThreat,
                    DefaultRestInterruptionChanceAtMaximumThreat),
                interrupted,
                hoursUntilInterrupt);
            if (decision.InterruptedByEyes)
            {
                hoursUntilInterrupt = Math.Max(
                    0.05f,
                    Math.Min(requestedHours, decision.HoursUntilInterrupt));
                interrupted = true;
                _pendingRestHunt = true;
                Logger.LogInfo(
                    "Unprotected rest was interrupted by cumulative Wyrdnight risk: chance="
                    + (decision.Chance * 100f).ToString(
                        "0.#",
                        CultureInfo.InvariantCulture)
                    + "%; exposure="
                    + decision.ExposureBefore.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + "->"
                    + decision.ExposureAfter.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + "; an official hunt is pending after the native rest transition.");
                ShowDiagnosticSystem(
                    "EITD - Rest interrupted: risk "
                    + (decision.Chance * 100f).ToString(
                        "0",
                        CultureInfo.InvariantCulture)
                    + "%; one hunt pending");
            }
            else if (decision.InterruptedByNative
                && decision.ExposureAfter > decision.ExposureBefore)
            {
                Logger.LogInfo(
                    "Native sleep interruption occurred during unprotected Wyrdnight exposure; Eyes added no duplicate hunt and locked further exposed rest until dawn.");
            }
            else
            {
                LogDiagnostic(
                    "Unprotected Wyrdnight rest risk accumulated: chance="
                    + (decision.Chance * 100f).ToString(
                        "0.#",
                        CultureInfo.InvariantCulture)
                    + "%; exposure="
                    + decision.ExposureAfter.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private bool TryCompleteRestAtmosphereReconciliation(
            RuntimeContext context)
        {
            if (!_restAtmosphereReconciliationPending
                || !NightStateEvaluator.IsStableAfterRest(
                    context.Observation))
            {
                return false;
            }

            _restAtmosphereReconciliationPending = false;
            _notificationCooldowns.Reset();
            ResetBattlecryState();
            if (IsKnownValidWyrdNight(context)
                && context.Observation.IsOutdoor)
            {
                _hasKnownProtectionState = true;
                _lastKnownProtected = context.IsProtected;
            }
            else
            {
                _hasKnownProtectionState = false;
            }

            string finalPhase = IsKnownDaylight(context)
                ? "daylight"
                : IsKnownValidWyrdNight(context)
                    ? "Wyrdnight"
                    : context.Decision.Reason.ToString();
            if (IsKnownDaylight(context))
            {
                ResetRestRisk();
            }
            Logger.LogInfo(
                "Rest atmosphere reconciled without replaying slept-through transitions: finalPhase="
                + finalPhase
                + "; protection="
                + (context.IsProtected ? "protected" : "exposed")
                + ".");
            ShowDiagnosticSystem(
                "EITD - Rest reconciled: "
                + finalPhase
                + "; slept-through transitions suppressed");
            return true;
        }

        internal void AttachMeter(VHeroHUD heroHud)
        {
            if (_meter != null)
            {
                _meter.Attach(heroHud);
            }
        }

        internal void DetachMeter(VHeroHUD heroHud)
        {
            if (_meter != null)
            {
                _meter.Detach(heroHud);
            }
        }

        private static RuntimeContext SampleContext()
        {
            NightObservation observation = new NightObservation();
            Hero hero = Hero.Current;
            observation.HasPlayableHero = hero != null
                && !hero.HasBeenDiscarded;
            observation.HeroAlive = observation.HasPlayableHero
                && hero.IsAlive
                && !hero.IsDying;

            observation.AtTitleScreen = IsLiveModel(
                World.Any<TitleScreenUI>());
            observation.IsLoading = LoadingStates.IsLoadingWorld
                || LoadingScreenUI.IsLoading
                || IsLiveModel(World.Any<LoadingScreenUI>());

            WyrdnessService wyrdnessService = World.Services == null
                ? null
                : World.Services.TryGet<WyrdnessService>();
            if (wyrdnessService == null
                || !observation.HasPlayableHero
                || observation.AtTitleScreen
                || observation.IsLoading)
            {
                return CreateContext(
                    observation,
                    "<unknown>",
                    false,
                    false,
                    0f);
            }

            TransitionService transition =
                World.Services.TryGet<TransitionService>();
            observation.IsTransitioning = transition != null
                && transition.InTransition;
            observation.IsTraveling = observation.HasPlayableHero
                && (hero.IsPortaling
                    || hero.JustTeleported
                    || hero.AllowNpcTeleport);
            observation.IsResting = IsLiveModel(
                World.Any<RestPopupUI>());

            SceneService sceneService =
                World.Services.TryGet<SceneService>();
            observation.SceneKnown = sceneService != null
                && sceneService.ActiveSceneRef != null
                && !string.IsNullOrEmpty(
                    sceneService.ActiveSceneRef.Name);
            if (!observation.SceneKnown)
            {
                return CreateContext(
                    observation,
                    "<unknown>",
                    false,
                    false,
                    0f);
            }

            // WyrdnessService.Init is the game's first legitimate caller of
            // SceneLifetimeEvents.Get. Reaching this point means that service
            // exists, the playable hero exists, and an active scene is known,
            // so this plugin cannot poison the type initializer at startup.
            SceneLifetimeEvents sceneLifetime = SceneLifetimeEvents.Get;
            observation.SceneInitialized = observation.SceneKnown
                && sceneLifetime.EverythingInitialized;
            observation.IsOutdoor = observation.SceneKnown
                && sceneService.IsOpenWorld
                && !sceneLifetime.InInterior;
            observation.AllowsWyrdNight = observation.SceneKnown
                && sceneService.AllowsWyrdnight;
            observation.IsPrologue = observation.SceneKnown
                && sceneService.IsPrologue;

            GameRealTime gameRealTime = World.Any<GameRealTime>();
            observation.HasWorldTime = gameRealTime != null
                && !gameRealTime.HasBeenDiscarded;
            float dayFraction = 0f;
            float worldDurationMultiplier = 1f;
            if (observation.HasWorldTime)
            {
                dayFraction = gameRealTime.WeatherTime.DayTime;
                observation.GameSaysNight =
                    gameRealTime.WeatherTime.IsNight;
                GameConstants gameConstants =
                    World.Services.TryGet<GameConstants>();
                float defaultDayMinutes = gameConstants == null
                    ? 0f
                    : gameConstants.dayDurationInMinutes;
                float currentWeatherRate =
                    gameRealTime.WeatherSecondsPerRealSecond;
                if (defaultDayMinutes > 0f
                    && currentWeatherRate > 0f
                    && !float.IsNaN(currentWeatherRate)
                    && !float.IsInfinity(currentWeatherRate))
                {
                    float defaultWeatherRate = 1440f
                        / defaultDayMinutes;
                    worldDurationMultiplier = Mathf.Max(
                        1f,
                        defaultWeatherRate / currentWeatherRate);
                }
            }

            HeroWyrdNight heroWyrdNight =
                observation.HasPlayableHero
                    ? hero.HeroWyrdNight
                    : null;
            observation.HasHeroNightState = heroWyrdNight != null
                && !heroWyrdNight.HasBeenDiscarded;
            observation.HeroSaysNight =
                observation.HasHeroNightState
                && heroWyrdNight.Night;

            bool isProtected = observation.HasPlayableHero
                && hero.IsSafeFromWyrdness;
            bool isExposed = observation.HasHeroNightState
                && heroWyrdNight.IsHeroInWyrdness
                && !isProtected;
            string sceneName = observation.SceneKnown
                ? sceneService.ActiveSceneRef.Name
                : "<unknown>";
            return CreateContext(
                observation,
                sceneName,
                isProtected,
                isExposed,
                NightStateEvaluator.NormalizeNightProgress(
                    dayFraction,
                    observation.GameSaysNight),
                worldDurationMultiplier);
        }

        private static RuntimeContext CreateContext(
            NightObservation observation,
            string sceneName,
            bool isProtected,
            bool isExposed,
            float nightProgress,
            float worldDurationMultiplier = 1f)
        {
            return new RuntimeContext(
                observation,
                NightStateEvaluator.Evaluate(observation),
                sceneName,
                isProtected,
                isExposed,
                Time.timeScale <= 0f,
                nightProgress,
                worldDurationMultiplier);
        }

        private static bool IsKnownValidWyrdNight(
            RuntimeContext context)
        {
            NightObservation observation = context.Observation;
            return observation.HasPlayableHero
                && observation.HeroAlive
                && !observation.AtTitleScreen
                && !observation.IsLoading
                && !observation.IsTransitioning
                && !observation.IsTraveling
                && !observation.IsResting
                && observation.SceneKnown
                && observation.SceneInitialized
                && observation.HasWorldTime
                && observation.HasHeroNightState
                && observation.AllowsWyrdNight
                && !observation.IsPrologue
                && observation.GameSaysNight
                && observation.HeroSaysNight;
        }

        private static bool IsKnownValidWyrdNightForVisuals(
            RuntimeContext context)
        {
            NightObservation observation = context.Observation;
            return observation.HasPlayableHero
                && observation.HeroAlive
                && !observation.AtTitleScreen
                && !observation.IsLoading
                && !observation.IsTransitioning
                && !observation.IsTraveling
                && observation.SceneKnown
                && observation.SceneInitialized
                && observation.HasWorldTime
                && observation.HasHeroNightState
                && observation.AllowsWyrdNight
                && !observation.IsPrologue
                && observation.GameSaysNight
                && observation.HeroSaysNight;
        }

        private static bool IsKnownDaylight(RuntimeContext context)
        {
            NightObservation observation = context.Observation;
            return observation.HasPlayableHero
                && observation.HeroAlive
                && !observation.AtTitleScreen
                && !observation.IsLoading
                && !observation.IsTransitioning
                && !observation.IsTraveling
                && !observation.IsResting
                && observation.SceneKnown
                && observation.SceneInitialized
                && observation.HasWorldTime
                && observation.HasHeroNightState
                && !observation.GameSaysNight
                && !observation.HeroSaysNight;
        }

        private static bool IsKnownDaylightForVisuals(
            RuntimeContext context)
        {
            NightObservation observation = context.Observation;
            return observation.HasPlayableHero
                && observation.HeroAlive
                && !observation.AtTitleScreen
                && !observation.IsLoading
                && !observation.IsTransitioning
                && !observation.IsTraveling
                && observation.SceneKnown
                && observation.SceneInitialized
                && observation.HasWorldTime
                && observation.HasHeroNightState
                && !observation.GameSaysNight
                && !observation.HeroSaysNight;
        }

        private static bool IsLiveModel(Model model)
        {
            return model != null && !model.HasBeenDiscarded;
        }

        private static bool SameDiagnosticState(
            RuntimeContext left,
            RuntimeContext right)
        {
            return left.Decision.State == right.Decision.State
                && left.Decision.Reason == right.Decision.Reason
                && left.IsProtected == right.IsProtected
                && left.IsExposed == right.IsExposed
                && left.IsPaused == right.IsPaused
                && string.Equals(
                    left.SceneName,
                    right.SceneName,
                    StringComparison.Ordinal);
        }

        private void LogTransition(RuntimeContext context)
        {
            Logger.LogInfo(
                "Night state: "
                + context.Decision.State
                + "; reason="
                + context.Decision.Reason
                + "; huntState="
                + _huntDirector.State
                + "; scene="
                + context.SceneName
                + "; paused="
                + (context.IsPaused ? "true" : "false")
                + "; protection="
                + (context.IsProtected
                    ? "protected"
                    : context.IsExposed ? "exposed" : "unknown")
                + "; nightProgress="
                + context.NightProgress.ToString(
                    "0.000",
                    CultureInfo.InvariantCulture)
                + "; activeRealSeconds="
                + _activeRealTimeClock.Seconds.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; threat="
                + _threat.Value.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture)
                + "; stage="
                + _threat.Stage
                + "; grace="
                + _threat.GraceRemainingSeconds.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture));
        }

        private void LogDiagnostic(string message)
        {
            if (DiagnosticsEnabled())
            {
                Logger.LogInfo(message);
            }
        }

        private bool DiagnosticsEnabled()
        {
            return _diagnostics != null && _diagnostics.Value;
        }

        private bool IsFeatureEnabled()
        {
            return _featureEnabled == null || _featureEnabled.Value;
        }

        private ThreatTuning CurrentThreatTuning()
        {
            return new ThreatTuning
            {
                PassiveThreatPerNight = ValueOrDefault(
                    _passiveThreatPerNight,
                    DefaultPassiveThreatPerNight),
                ProtectedDecayPerMinute = ValueOrDefault(
                    _protectedDecayPerMinute,
                    DefaultProtectedDecayPerMinute),
                InteriorDecayPerMinute = ValueOrDefault(
                    _interiorDecayPerMinute,
                    DefaultInteriorDecayPerMinute),
                LoadReconstructionAtDawn = ValueOrDefault(
                    _loadReconstructionAtDawn,
                    DefaultLoadReconstructionAtDawn),
                GraceSeconds = ValueOrDefault(
                    _graceSeconds,
                    DefaultGraceSeconds)
            };
        }

        private PacingTuning CurrentPacingTuning()
        {
            return new PacingTuning
            {
                BaseDangerBudget = ValueOrDefault(
                    _baseDangerBudget,
                    DefaultBaseDangerBudget),
                LongNightBonusScale = ValueOrDefault(
                    _longNightBonusScale,
                    DefaultLongNightBonusScale),
                MaximumLongNightBonus = ValueOrDefault(
                    _maximumLongNightBonus,
                    DefaultMaximumLongNightBonus)
            };
        }

        private HuntTuning CurrentHuntTuning()
        {
            return new HuntTuning
            {
                BaseHazardPerMinute = ValueOrDefault(
                    _baseHazardPerMinute,
                    DefaultBaseHazardPerMinute),
                ThreatHazardPerMinute = ValueOrDefault(
                    _threatHazardPerMinute,
                    DefaultThreatHazardPerMinute),
                NightProgressHazardPerMinute = ValueOrDefault(
                    _nightProgressHazardPerMinute,
                    DefaultNightProgressHazardPerMinute),
                MinimumHazardTarget = ValueOrDefault(
                    _minimumHazardTarget,
                    DefaultMinimumHazardTarget),
                MaximumHazardTarget = ValueOrDefault(
                    _maximumHazardTarget,
                    DefaultMaximumHazardTarget),
                WarningSeconds = ValueOrDefault(
                    _warningSeconds,
                    DefaultWarningSeconds),
                KillRecoverySeconds = ValueOrDefault(
                    _killRecoverySeconds,
                    DefaultKillRecoverySeconds),
                EscapeRecoverySeconds = ValueOrDefault(
                    _escapeRecoverySeconds,
                    DefaultEscapeRecoverySeconds),
                FailedPlacementRecoverySeconds = ValueOrDefault(
                    _failedPlacementRecoverySeconds,
                    DefaultFailedPlacementRecoverySeconds),
                HunterDangerCost = HunterCatalogDirector
                    .MinimumBaseDangerCost * ValueOrDefault(
                        _dangerCostMultiplier,
                        DefaultDangerCostMultiplier)
            };
        }

        private AmbientStalkerTuning CurrentAmbientStalkerTuning()
        {
            return new AmbientStalkerTuning
            {
                Enabled = _enableAmbientStalkers == null
                    || _enableAmbientStalkers.Value,
                MinimumCooldownSeconds = ValueOrDefault(
                    _stalkerMinimumCooldown,
                    DefaultStalkerMinimumCooldownSeconds),
                MaximumCooldownSeconds = ValueOrDefault(
                    _stalkerMaximumCooldown,
                    DefaultStalkerMaximumCooldownSeconds),
                MaximumCooldownAtFiftyThreatSeconds = ValueOrDefault(
                    _stalkerMaximumCooldownAtFiftyThreat,
                    DefaultStalkerMaximumCooldownAtFiftyThreatSeconds)
            };
        }

        private static string FormatCause(ThreatChangeCause cause)
        {
            switch (cause)
            {
                case ThreatChangeCause.SprintOrFastSwim:
                    return "movement";
                case ThreatChangeCause.WyrdKill:
                    return "Wyrd kill";
                case ThreatChangeCause.Battlecry:
                    return "battlecry";
                case ThreatChangeCause.DiagnosticOverride:
                    return "diagnostic override";
                default:
                    return cause.ToString().ToLowerInvariant();
            }
        }

        private static float ValueOrDefault(
            ConfigEntry<float> entry,
            float fallback)
        {
            return entry == null ? fallback : entry.Value;
        }

        private static string ModelId(IModel model)
        {
            return model == null
                ? "unknown"
                : model.ID.ToString();
        }

        private void OnGameplayPresetChanged(
            object sender,
            EventArgs eventArgs)
        {
            if (_applyingGameplayPreset
                || _gameplayPreset == null
                || _gameplayPreset.Value
                    == GameplayTuningPreset.Custom)
            {
                return;
            }

            GameplayTuningPreset preset = _gameplayPreset.Value;
            _applyingGameplayPreset = true;
            try
            {
                switch (preset)
                {
                    case GameplayTuningPreset.UneasyNight:
                        _allowUnprotectedWyrdnightRest.Value = true;
                        _restInterruptionChanceAtZeroThreat.Value = 0f;
                        _restInterruptionChanceAtMaximumThreat.Value = 0f;
                        _passiveThreatPerNight.Value = 14f;
                        _sprintThreatPerMinute.Value = 3f;
                        _combatThreatPerWindow.Value = 1.5f;
                        _wyrdKillThreat.Value = 3f;
                        _baseDangerBudget.Value = 22f;
                        _longNightBonusScale.Value = 0.25f;
                        _maximumLongNightBonus.Value = 0.5f;
                        _baseHazardPerMinute.Value = 0.005f;
                        _threatHazardPerMinute.Value = 0.28f;
                        _nightProgressHazardPerMinute.Value = 0.05f;
                        _minimumHazardTarget.Value = 1.05f;
                        _maximumHazardTarget.Value = 1.35f;
                        _warningSeconds.Value = 8f;
                        _maximumPackSize.Value = 1;
                        _sidecarChance.Value = 0f;
                        _allowEliteEnemies.Value = false;
                        _enableAmbientStalkers.Value = true;
                        _stalkerMinimumCooldown.Value = 75f;
                        _stalkerMaximumCooldown.Value = 210f;
                        _stalkerMaximumCooldownAtFiftyThreat.Value = 105f;
                        _stalkerProvocationThreat.Value = 4f;
                        _killRecoverySeconds.Value = 120f;
                        _escapeRecoverySeconds.Value = 240f;
                        _failedPlacementRecoverySeconds.Value = 45f;
                        break;
                    case GameplayTuningPreset.WatchfulNight:
                        _allowUnprotectedWyrdnightRest.Value = true;
                        _restInterruptionChanceAtZeroThreat.Value = 45f;
                        _restInterruptionChanceAtMaximumThreat.Value = 75f;
                        _passiveThreatPerNight.Value =
                            DefaultPassiveThreatPerNight;
                        _sprintThreatPerMinute.Value =
                            DefaultSprintThreatPerMinute;
                        _combatThreatPerWindow.Value =
                            DefaultCombatThreatPerWindow;
                        _wyrdKillThreat.Value =
                            DefaultWyrdKillThreat;
                        _baseDangerBudget.Value =
                            DefaultBaseDangerBudget;
                        _longNightBonusScale.Value =
                            DefaultLongNightBonusScale;
                        _maximumLongNightBonus.Value =
                            DefaultMaximumLongNightBonus;
                        _baseHazardPerMinute.Value =
                            DefaultBaseHazardPerMinute;
                        _threatHazardPerMinute.Value =
                            DefaultThreatHazardPerMinute;
                        _nightProgressHazardPerMinute.Value =
                            DefaultNightProgressHazardPerMinute;
                        _minimumHazardTarget.Value =
                            DefaultMinimumHazardTarget;
                        _maximumHazardTarget.Value =
                            DefaultMaximumHazardTarget;
                        _warningSeconds.Value =
                            DefaultWarningSeconds;
                        _maximumPackSize.Value =
                            DefaultMaximumPackSize;
                        _sidecarChance.Value =
                            DefaultSidecarChance;
                        _allowEliteEnemies.Value = false;
                        _enableAmbientStalkers.Value = true;
                        _stalkerMinimumCooldown.Value =
                            DefaultStalkerMinimumCooldownSeconds;
                        _stalkerMaximumCooldown.Value =
                            DefaultStalkerMaximumCooldownSeconds;
                        _stalkerMaximumCooldownAtFiftyThreat.Value =
                            DefaultStalkerMaximumCooldownAtFiftyThreatSeconds;
                        _stalkerProvocationThreat.Value =
                            DefaultStalkerProvocationThreat;
                        _killRecoverySeconds.Value =
                            DefaultKillRecoverySeconds;
                        _escapeRecoverySeconds.Value =
                            DefaultEscapeRecoverySeconds;
                        _failedPlacementRecoverySeconds.Value =
                            DefaultFailedPlacementRecoverySeconds;
                        break;
                    case GameplayTuningPreset.CursedNight:
                        _allowUnprotectedWyrdnightRest.Value = false;
                        _restInterruptionChanceAtZeroThreat.Value = 80f;
                        _restInterruptionChanceAtMaximumThreat.Value = 100f;
                        _passiveThreatPerNight.Value = 28f;
                        _sprintThreatPerMinute.Value = 5.5f;
                        _combatThreatPerWindow.Value = 3f;
                        _wyrdKillThreat.Value = 7f;
                        _baseDangerBudget.Value = 42f;
                        _longNightBonusScale.Value = 0.45f;
                        _maximumLongNightBonus.Value = 1f;
                        _baseHazardPerMinute.Value = 0.02f;
                        _threatHazardPerMinute.Value = 0.58f;
                        _nightProgressHazardPerMinute.Value = 0.12f;
                        _minimumHazardTarget.Value = 0.7f;
                        _maximumHazardTarget.Value = 0.95f;
                        _warningSeconds.Value = 4f;
                        _maximumPackSize.Value = 3;
                        _sidecarChance.Value = 0.8f;
                        _allowEliteEnemies.Value = true;
                        _enableAmbientStalkers.Value = true;
                        _stalkerMinimumCooldown.Value = 40f;
                        _stalkerMaximumCooldown.Value = 125f;
                        _stalkerMaximumCooldownAtFiftyThreat.Value = 55f;
                        _stalkerProvocationThreat.Value = 8f;
                        _killRecoverySeconds.Value = 60f;
                        _escapeRecoverySeconds.Value = 120f;
                        _failedPlacementRecoverySeconds.Value = 20f;
                        break;
                }

                _gameplayPreset.Value = GameplayTuningPreset.Custom;
                Config.Save();
                Logger.LogInfo(
                    "Applied the "
                    + FormatGameplayPreset(preset)
                    + " one-shot gameplay preset and returned ApplyPreset to Custom. Presentation and diagnostic settings were unchanged.");
                ShowDiagnosticSystem(
                    "EITD - Preset applied: "
                    + FormatGameplayPreset(preset)
                    + "; pack cap "
                    + _maximumPackSize.Value
                    + "; elites "
                    + (_allowEliteEnemies.Value
                        ? "high-pressure stalkers at 50-75%; official elites above 75%"
                        : "disabled")
                    + "; stalker cooldown "
                    + _stalkerMinimumCooldown.Value.ToString(
                        "0",
                        CultureInfo.InvariantCulture)
                    + "-"
                    + _stalkerMaximumCooldown.Value.ToString(
                        "0",
                        CultureInfo.InvariantCulture)
                    + "s"
                    + "; next-night budget "
                    + _baseDangerBudget.Value.ToString(
                        "0.#",
                        CultureInfo.InvariantCulture));
            }
            finally
            {
                _applyingGameplayPreset = false;
            }
        }

        private static string FormatGameplayPreset(
            GameplayTuningPreset preset)
        {
            switch (preset)
            {
                case GameplayTuningPreset.UneasyNight:
                    return "Uneasy Night";
                case GameplayTuningPreset.WatchfulNight:
                    return "Watchful Night";
                case GameplayTuningPreset.CursedNight:
                    return "Cursed Night";
                default:
                    return "Custom";
            }
        }

        private void BindConfig()
        {
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Internal config schema marker. Do not edit this value.",
                    null,
                    new BrowsableAttribute(false),
                    new FoASettingUiMetadata
                    {
                        DisplaySection = "General",
                        DisplayName = "Config Schema Version",
                        SectionOrder = 0,
                        Order = 0,
                        Hidden = true
                    }));
            _featureEnabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                UiDescription(
                    "Master switch for Eyes in the Dark world timescale, threat, official hunts, meter, boundary presentation, pacing, and notifications.",
                    "General",
                    "Enable Eyes in the Dark",
                    0,
                    10));
            _ownRestMenu = Config.Bind(
                "1. Core",
                "OwnRestMenu",
                true,
                UiDescription(
                    "Let Eyes present rest availability, rotate and relabel the rest clock, and format rest-popup times. Disable this to leave every rest-menu visual and control untouched; gameplay rest safety and interruption rules still apply silently when rest is accepted.",
                    "General",
                    "Own Rest Menu",
                    0,
                    40));
            _allowUnprotectedWyrdnightRest = Config.Bind(
                "1. Core",
                "AllowUnprotectedWyrdnightRest",
                true,
                UiDescription(
                    "Allow starting rest during an active outdoor Wyrdnight while outside a fueled protective boundary. Presets control this gameplay rule. A rest interruption still locks further exposed rest until dawn; protected and daylight rest remain available under the game's normal rules.",
                    "General",
                    "Allow Unprotected Wyrdnight Rest",
                    0,
                    50));
            _restClockLabelFormat = Config.Bind(
                "1. Core",
                "RestClockLabelFormat",
                RestClockLabelFormat.TwelveHour,
                UiDescription(
                    "Choose the time format used by Eyes for the native rest clock labels and quick-use-wheel time. 12 Hour shows AM/PM; 24 Hour leaves the quick-use time untouched and labels the rest clock 00, 06, 12, and 18.",
                    "General",
                    "Time Display",
                    0,
                    60,
                    choiceLabels: "TwelveHour=12 Hour (AM/PM);TwentyFourHour=24 Hour"));

            _restInterruptionChanceAtZeroThreat = Config.Bind(
                "6. Rest",
                "RestInterruptionChanceAtZeroThreat",
                DefaultRestInterruptionChanceAtZeroThreat,
                UiDescription(
                    "Eyes' added chance that a full Wyrdnight of unprotected sleep is interrupted at zero threat. Native interruptions remain authoritative. Exposure accumulates across repeated rests instead of rerolling each attempt.",
                    "Advanced - Resting",
                    "Interruption Chance at 0 Threat (%)",
                    230,
                    10,
                    new AcceptableValueRange<float>(0f, 100f)));
            _restInterruptionChanceAtMaximumThreat = Config.Bind(
                "6. Rest",
                "RestInterruptionChanceAtMaximumThreat",
                DefaultRestInterruptionChanceAtMaximumThreat,
                UiDescription(
                    "Eyes' added chance that a full Wyrdnight of unprotected sleep is interrupted at 100 threat. Current risk interpolates with threat, and a successful Eyes interruption commits one official hunt after waking.",
                    "Advanced - Resting",
                    "Interruption Chance at 100 Threat (%)",
                    230,
                    20,
                    new AcceptableValueRange<float>(0f, 100f)));

            _enableDynamicTimescale = Config.Bind(
                "2. World Timescale",
                "EnableDynamicTimescale",
                true,
                UiDescription(
                    "Let Eyes own real-minute day and night durations. Wyrdnights stretch with threat. This never changes Unity gameplay Time.timeScale.",
                    "World Clock",
                    "Use Dynamic Day and Night Durations",
                    10,
                    10));
            _dayMinutes = Config.Bind(
                "2. World Timescale",
                "DayMinutes",
                DefaultDayMinutes,
                UiDescription(
                    "Approximate real minutes of daylight. The default is 60 minutes.",
                    "World Clock",
                    "Day Length in Minutes",
                    10,
                    20,
                    new AcceptableValueRange<float>(
                        WorldTimescalePolicy.MinimumPhaseMinutes,
                        WorldTimescalePolicy.MaximumPhaseMinutes)));
            _baseNightMinutes = Config.Bind(
                "2. World Timescale",
                "BaseNightMinutes",
                DefaultBaseNightMinutes,
                UiDescription(
                    "Approximate real minutes in a quiet zero-threat Wyrdnight. The default six minutes stays close to the game's approximately 6.2-minute night.",
                    "World Clock",
                    "Quiet Wyrdnight Length (Minutes)",
                    10,
                    30,
                    new AcceptableValueRange<float>(
                        WorldTimescalePolicy.MinimumPhaseMinutes,
                        WorldTimescalePolicy.MaximumPhaseMinutes)));
            _maximumThreatNightMinutes = Config.Bind(
                "2. World Timescale",
                "MaximumThreatNightMinutes",
                DefaultMaximumThreatNightMinutes,
                UiDescription(
                    "Approximate real minutes in a 100-threat Wyrdnight. Current night length interpolates dynamically from the quiet value to this value. Values below the quiet length are safely clamped to it.",
                    "World Clock",
                    "Maximum-Threat Wyrdnight Length (Minutes)",
                    10,
                    40,
                    new AcceptableValueRange<float>(
                        WorldTimescalePolicy.MinimumPhaseMinutes,
                        WorldTimescalePolicy.MaximumPhaseMinutes)));

            _gameplayPreset = Config.Bind(
                "2. Gameplay Preset",
                "ApplyPreset",
                GameplayTuningPreset.Custom,
                UiDescription(
                    "Apply a gameplay template once. Uneasy Night, recommended Watchful Night, or Cursed Night writes threat and encounter tuning immediately, then returns this selector to Custom. HUD, notifications, boundary, and diagnostic preferences are preserved.",
                    "General",
                    "Apply Gameplay Preset Once",
                    0,
                    20,
                    choiceLabels: "Custom=Custom;UneasyNight=Uneasy Night;WatchfulNight=Watchful Night;CursedNight=Cursed Night"));

            _passiveThreatPerNight = BindThreatValue(
                "PassiveThreatPerNight",
                DefaultPassiveThreatPerNight,
                0f,
                100f,
                "Threat gained across one complete exposed outdoor Wyrdnight. Progress-based calculation keeps this baseline independent of world timescale.",
                "Advanced - Threat Tuning",
                "Passive Threat per Night",
                200,
                10);
            _sprintThreatPerMinute = BindThreatValue(
                "SprintThreatPerMinute",
                DefaultSprintThreatPerMinute,
                0f,
                30f,
                "Threat gained per minute of sustained exposed sprinting or fast swimming, committed in non-spammable intervals.",
                "Advanced - Threat Tuning",
                "Sprint Threat per Minute",
                200,
                20);
            _combatThreatPerWindow = BindThreatValue(
                "CombatThreatPerWindow",
                DefaultCombatThreatPerWindow,
                0f,
                10f,
                "Maximum threat from meaningful damage events in each short aggregation window.",
                "Advanced - Threat Tuning",
                "Combat Threat per Window",
                200,
                30);
            _combatResponseSeconds = BindThreatValue(
                "CombatResponseSeconds",
                DefaultCombatResponseSeconds,
                0.25f,
                5f,
                "Active real-time delay before queued combat and confirmed environment-impact threat is committed. The same window caps their combined contribution.",
                "Advanced - Threat Tuning",
                "Combat Response Delay (Seconds)",
                200,
                40);
            _wyrdKillThreat = BindThreatValue(
                "WyrdKillThreat",
                DefaultWyrdKillThreat,
                0f,
                20f,
                "Threat gained when the Hero kills a Wyrd-converted or Wyrdness-bound NPC.",
                "Advanced - Threat Tuning",
                "Wyrd Kill Threat",
                200,
                50);
            _acquisitionThreatPerItem = BindThreatValue(
                "AcquisitionThreatPerItem",
                DefaultAcquisitionThreatPerItem,
                0f,
                5f,
                "Threat queued for each unique direct pickup or item taken from a location while exposed. Short windows cap bulk looting.",
                "Advanced - Threat Tuning",
                "Item Acquisition Threat",
                200,
                60);
            _protectedDecayPerMinute = BindThreatValue(
                "ProtectedDecayPerMinute",
                DefaultProtectedDecayPerMinute,
                0f,
                60f,
                "Threat removed per active real-time minute while outdoors and protected from the Wyrdness.",
                "Advanced - Threat Tuning",
                "Protected Decay per Minute",
                200,
                70);
            _interiorDecayPerMinute = BindThreatValue(
                "InteriorDecayPerMinute",
                DefaultInteriorDecayPerMinute,
                0f,
                30f,
                "Threat removed per active real-time minute indoors during a valid Wyrdnight.",
                "Advanced - Threat Tuning",
                "Interior Decay per Minute",
                200,
                80);
            _loadReconstructionAtDawn = BindThreatValue(
                "LoadReconstructionAtDawn",
                DefaultLoadReconstructionAtDawn,
                0f,
                40f,
                "Maximum modest threat reconstructed by dawn progress after loading during a Wyrdnight.",
                "Advanced - Threat Tuning",
                "Loaded-Night Threat Reconstruction",
                200,
                90);
            _graceSeconds = BindThreatValue(
                "LoadAndInteriorExitGraceSeconds",
                DefaultGraceSeconds,
                0f,
                60f,
                "Active real-time seconds during which activity threat is suppressed after a Wyrdnight load or interior exit.",
                "Advanced - Threat Tuning",
                "Load and Exit Grace (Seconds)",
                200,
                100);

            _baseDangerBudget = Config.Bind(
                "4. Encounters",
                "BaseNightlyDangerBudget",
                DefaultBaseDangerBudget,
                UiDescription(
                    "Base danger budget calculated once per Wyrdnight and spent only after complete curated encounter placement is confirmed.",
                    "Advanced - Hunt Pacing", "Nightly Encounter Budget", 210, 10,
                    new AcceptableValueRange<float>(0f, 200f)));
            _longNightBonusScale = Config.Bind(
                "4. Encounters",
                "LongNightBonusScale",
                DefaultLongNightBonusScale,
                UiDescription(
                    "Scales the sublinear square-root budget bonus when the configured maximum-threat night is longer than the game's default night.",
                    "Advanced - Hunt Pacing", "Long-Night Budget Bonus Scale", 210, 20,
                    new AcceptableValueRange<float>(0f, 2f)));
            _maximumLongNightBonus = Config.Bind(
                "4. Encounters",
                "MaximumLongNightBonus",
                DefaultMaximumLongNightBonus,
                UiDescription(
                    "Maximum extra fraction of the base nightly budget available for an extended maximum-threat night. 0.75 caps the total at 175% of base.",
                    "Advanced - Hunt Pacing", "Maximum Long-Night Budget Bonus", 210, 30,
                    new AcceptableValueRange<float>(0f, 3f)));
            _baseHazardPerMinute = Config.Bind(
                "4. Encounters",
                "BaseHazardPerMinute",
                DefaultBaseHazardPerMinute,
                UiDescription(
                    "Quiet baseline added to accumulated hunt pressure per active exposed minute. This is not an independent random roll.",
                    "Advanced - Hunt Pacing", "Base Hunt Pressure per Minute", 210, 40,
                    new AcceptableValueRange<float>(0f, 5f)));
            _threatHazardPerMinute = Config.Bind(
                "4. Encounters",
                "ThreatHazardPerMinute",
                DefaultThreatHazardPerMinute,
                UiDescription(
                    "Maximum additional accumulated hunt pressure per exposed minute from Wyrd Threat. Threat uses a rising nonlinear curve.",
                    "Advanced - Hunt Pacing", "Threat-Based Hunt Pressure", 210, 50,
                    new AcceptableValueRange<float>(0f, 5f)));
            _nightProgressHazardPerMinute = Config.Bind(
                "4. Encounters",
                "NightProgressHazardPerMinute",
                DefaultNightProgressHazardPerMinute,
                UiDescription(
                    "Maximum additional accumulated hunt pressure per exposed minute as the Wyrdnight advances.",
                    "Advanced - Hunt Pacing", "Night-Progress Hunt Pressure", 210, 60,
                    new AcceptableValueRange<float>(0f, 5f)));
            _minimumHazardTarget = Config.Bind(
                "4. Encounters",
                "MinimumHazardTarget",
                DefaultMinimumHazardTarget,
                UiDescription(
                    "Lower bound for the randomized accumulated-pressure threshold selected for each hunt opportunity.",
                    "Advanced - Hunt Pacing", "Minimum Hunt Pressure Threshold", 210, 70,
                    new AcceptableValueRange<float>(0.1f, 10f)));
            _maximumHazardTarget = Config.Bind(
                "4. Encounters",
                "MaximumHazardTarget",
                DefaultMaximumHazardTarget,
                UiDescription(
                    "Upper bound for the randomized accumulated-pressure threshold selected for each hunt opportunity.",
                    "Advanced - Hunt Pacing", "Maximum Hunt Pressure Threshold", 210, 80,
                    new AcceptableValueRange<float>(0.1f, 10f)));
            _warningSeconds = Config.Bind(
                "4. Encounters",
                "WarningSeconds",
                DefaultWarningSeconds,
                UiDescription(
                    "Active-real-time warning delay between hunt commitment and placement. Eligibility is checked again before spawning.",
                    "Advanced - Hunt Pacing", "Warning Duration (Seconds)", 210, 90,
                    new AcceptableValueRange<float>(1f, 30f)));
            _dangerCostMultiplier = Config.Bind(
                "4. Encounters",
                "DangerCostMultiplier",
                DefaultDangerCostMultiplier,
                UiDescription(
                    "Multiplier applied to each curated profile's reviewed danger cost. Budget is spent only after the complete encounter is confirmed.",
                    "Advanced - Hunt Composition", "Encounter Cost Multiplier", 220, 10,
                    new AcceptableValueRange<float>(0.5f, 2f)));
            _maximumPackSize = Config.Bind(
                "4. Encounters",
                "MaximumEncounterSize",
                DefaultMaximumPackSize,
                UiDescription(
                    "Maximum official encounter size. Player level, profile safety, composition rules, and remaining danger budget can reduce it.",
                    "Advanced - Hunt Composition", "Maximum Encounter Size", 220, 20,
                    new AcceptableValueRange<int>(1, 3)));
            _sidecarChance = Config.Bind(
                "4. Encounters",
                "SidecarChance",
                DefaultSidecarChance,
                UiDescription(
                    "Maximum chance to add each weaker curated sidecar. Actual chance rises smoothly with Wyrd Threat and is capped by level, preset, profile safety, and budget.",
                    "Advanced - Hunt Composition", "Additional Hunter Chance", 220, 30,
                    new AcceptableValueRange<float>(0f, 1f)));
            _allowEliteEnemies = Config.Bind(
                "4. Encounters",
                "AllowEliteEnemies",
                false,
                UiDescription(
                    "Allow reviewed high-pressure ambient stalkers from 50% to below 75% threat and reviewed elite official hunters only above 75%. Bosses, minibosses, story actors, summons, and challenge or trial variants remain excluded.",
                    "General", "Allow Elite Enemies", 0, 40));
            _hunterSpawnDistance = Config.Bind(
                "4. Encounters",
                "HunterSpawnDistanceMeters",
                DefaultHunterSpawnDistance,
                UiDescription(
                    "Requested distance for curated official hunters. Native navigation placement and member separation are verified before spawning.",
                    "Advanced - Hunt Composition", "Hunter Spawn Distance (Meters)", 220, 40,
                    new AcceptableValueRange<float>(20f, 60f)));
            _escapeDistance = Config.Bind(
                "4. Encounters",
                "EscapeDistanceMeters",
                DefaultEscapeDistance,
                UiDescription(
                    "Distance that must be sustained from the exact official hunter to escape outdoors.",
                    "Advanced - Hunt Outcomes", "Escape Distance (Meters)", 230, 10,
                    new AcceptableValueRange<float>(30f, 200f)));
            _escapeSustainSeconds = Config.Bind(
                "4. Encounters",
                "EscapeSustainSeconds",
                DefaultEscapeSustainSeconds,
                UiDescription(
                    "Active real-time seconds the escape distance must be sustained.",
                    "Advanced - Hunt Outcomes", "Escape Sustain Duration (Seconds)", 230, 20,
                    new AcceptableValueRange<float>(1f, 60f)));
            _killThreatRelief = Config.Bind(
                "4. Encounters",
                "OfficialHunterKillThreatRelief",
                DefaultKillThreatRelief,
                UiDescription(
                    "Wyrd Threat removed when the exact official hunter dies. This should remain greater than escape relief.",
                    "Advanced - Hunt Outcomes", "Kill Threat Relief", 230, 30,
                    new AcceptableValueRange<float>(0f, 100f)));
            _escapeThreatRelief = Config.Bind(
                "4. Encounters",
                "OfficialHunterEscapeThreatRelief",
                DefaultEscapeThreatRelief,
                UiDescription(
                    "Wyrd Threat removed after a sustained outdoor escape or interior escape.",
                    "Advanced - Hunt Outcomes", "Escape Threat Relief", 230, 40,
                    new AcceptableValueRange<float>(0f, 100f)));
            _killRecoverySeconds = Config.Bind(
                "4. Encounters",
                "KillRecoverySeconds",
                DefaultKillRecoverySeconds,
                UiDescription(
                    "Active real-time recovery after killing the official hunter.",
                    "Advanced - Hunt Outcomes", "Kill Recovery (Seconds)", 230, 50,
                    new AcceptableValueRange<float>(10f, 600f)));
            _escapeRecoverySeconds = Config.Bind(
                "4. Encounters",
                "EscapeRecoverySeconds",
                DefaultEscapeRecoverySeconds,
                UiDescription(
                    "Longer active real-time Recently Pursued recovery after escaping the official hunter.",
                    "Advanced - Hunt Outcomes", "Escape Recovery (Seconds)", 230, 60,
                    new AcceptableValueRange<float>(10f, 900f)));
            _failedPlacementRecoverySeconds = Config.Bind(
                "4. Encounters",
                "FailedPlacementRecoverySeconds",
                DefaultFailedPlacementRecoverySeconds,
                UiDescription(
                    "Short active real-time retry protection after an invalid or failed placement. No danger budget is spent.",
                    "Advanced - Hunt Outcomes", "Failed Placement Recovery (Seconds)", 230, 70,
                    new AcceptableValueRange<float>(5f, 180f)));

            _enableAmbientStalkers = Config.Bind(
                "5. Ambient Stalkers",
                "EnableAmbientStalkers",
                true,
                UiDescription(
                    "Allow one volatile map-native creature to watch, follow, and flee from the Hero between official hunts. No stalker is spawned at or above 75% threat.",
                    "General",
                    "Enable Ambient Stalkers",
                    0,
                    30));
            _stalkerMinimumCooldown = Config.Bind(
                "5. Ambient Stalkers",
                "MinimumCooldownSeconds",
                DefaultStalkerMinimumCooldownSeconds,
                UiDescription(
                    "Lower bound for each randomized active-real-time delay between ambient stalkers.",
                    "Advanced - Stalker Tuning",
                    "Minimum Cooldown (Seconds)",
                    240,
                    20,
                    new AcceptableValueRange<float>(15f, 600f)));
            _stalkerMaximumCooldown = Config.Bind(
                "5. Ambient Stalkers",
                "MaximumCooldownSeconds",
                DefaultStalkerMaximumCooldownSeconds,
                UiDescription(
                    "Upper cooldown bound at zero Wyrd Threat. The live upper bound shrinks smoothly as threat approaches 50%.",
                    "Advanced - Stalker Tuning",
                    "Maximum Cooldown at Zero Threat (Seconds)",
                    240,
                    30,
                    new AcceptableValueRange<float>(15f, 900f)));
            _stalkerMaximumCooldownAtFiftyThreat = Config.Bind(
                "5. Ambient Stalkers",
                "MaximumCooldownAtFiftyThreatSeconds",
                DefaultStalkerMaximumCooldownAtFiftyThreatSeconds,
                UiDescription(
                    "Upper cooldown bound as Wyrd Threat reaches 50%. Values below the minimum cooldown are safely clamped at runtime.",
                    "Advanced - Stalker Tuning",
                    "Maximum Cooldown near 50% Threat (Seconds)",
                    240,
                    40,
                    new AcceptableValueRange<float>(15f, 600f)));
            _stalkerProvocationThreat = Config.Bind(
                "5. Ambient Stalkers",
                "ProvocationThreat",
                DefaultStalkerProvocationThreat,
                UiDescription(
                    "One-time Wyrd Threat added when the Hero attacks the exact passive stalker. The hit makes that stalker immediately hostile.",
                    "Advanced - Stalker Tuning",
                    "Threat from Provoking a Stalker",
                    240,
                    40,
                    new AcceptableValueRange<float>(0f, 25f)));
            _stalkerMinimumSpawnDistance = Config.Bind(
                "5. Ambient Stalkers",
                "MinimumSpawnDistanceMeters",
                DefaultStalkerMinimumSpawnDistance,
                UiDescription(
                    "Nearest requested stalker distance. Native walkable placement, path connectivity, Wyrd protection, and off-camera checks still apply.",
                    "Advanced - Stalker Tuning",
                    "Minimum Spawn Distance (Meters)",
                    240,
                    50,
                    new AcceptableValueRange<float>(35f, 80f)));
            _stalkerMaximumSpawnDistance = Config.Bind(
                "5. Ambient Stalkers",
                "MaximumSpawnDistanceMeters",
                DefaultStalkerMaximumSpawnDistance,
                UiDescription(
                    "Farthest requested stalker distance. Runtime ordering safely keeps this at or above the configured minimum.",
                    "Advanced - Stalker Tuning",
                    "Maximum Spawn Distance (Meters)",
                    240,
                    60,
                    new AcceptableValueRange<float>(40f, 100f)));
            _stalkerPassiveDespawnDistance = Config.Bind(
                "5. Ambient Stalkers",
                "PassiveDespawnDistanceMeters",
                DefaultStalkerPassiveDespawnDistance,
                UiDescription(
                    "Minimum distance required before a passive stalker may disappear while continuously outside the Hero camera. Hostile stalkers never use this cleanup.",
                    "Advanced - Stalker Tuning",
                    "Passive Despawn Distance (Meters)",
                    240,
                    70,
                    new AcceptableValueRange<float>(40f, 150f)));
            _stalkerOffCameraDespawnSeconds = Config.Bind(
                "5. Ambient Stalkers",
                "OffCameraDespawnSeconds",
                DefaultStalkerOffCameraDespawnSeconds,
                UiDescription(
                    "Continuous off-camera time required before a sufficiently distant passive stalker disappears.",
                    "Advanced - Stalker Tuning",
                    "Off-Camera Despawn Delay (Seconds)",
                    240,
                    80,
                    new AcceptableValueRange<float>(0.5f, 15f)));

            _threatMeterColor = Config.Bind(
                "7. Threat Meter",
                "ThreatMeterColor",
                ThreatMeterController.DefaultColorText,
                UiDescription(
                    "HTML RGB color for the Wyrd Threat meter, such as #8032FF.",
                    "HUD - Threat Meter", "Meter Color", 70, 10));
            _showExactThreat = Config.Bind(
                "7. Threat Meter",
                "ShowExactThreatValue",
                false,
                UiDescription(
                    "Show the rounded 0-100 Wyrd Threat value beside the meter.",
                    "HUD - Threat Meter", "Show Exact Threat", 70, 20));
            _meterOffsetX = Config.Bind(
                "7. Threat Meter",
                "MeterOffsetX",
                0f,
                UiDescription(
                    "Horizontal adjustment from the automatic placement baseline in local Hero HUD pixels.",
                    "HUD - Threat Meter", "Horizontal Offset", 70, 30,
                    new AcceptableValueRange<float>(-500f, 500f)));
            _meterOffsetY = Config.Bind(
                "7. Threat Meter",
                "MeterOffsetY",
                0f,
                UiDescription(
                    "Vertical adjustment from the automatic placement baseline in local Hero HUD pixels. Positive values move it upward.",
                    "HUD - Threat Meter", "Vertical Offset", 70, 40,
                    new AcceptableValueRange<float>(-500f, 500f)));

            _boundaryEnabled = Config.Bind(
                "8. Wyrd Boundary",
                "EnableBoundaryCustomization",
                true,
                UiDescription(
                    "Customize only the visual Wyrd boundary. Protection, mask intensity, and gameplay detection are never changed.",
                    "Boundary Appearance",
                    "Customize Wyrd Boundary",
                    80,
                    10));
            _boundaryRenderMode = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryRenderMode",
                BoundaryRenderMode.Layered,
                UiDescription(
                    "Layered draws independent near, middle, and outer visual rings. Single keeps one native-style outer edge.",
                    "Boundary Appearance",
                    "Boundary Style",
                    80,
                    20,
                    choiceLabels: "Single=Single Ring;Layered=Three Rings"));
            _boundaryColor = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryColor",
                DefaultBoundaryColor,
                UiDescription(
                    "HTML RGB or RGBA color for the visual Wyrd boundary, such as #B878FF.",
                    "Boundary Appearance",
                    "Boundary Color",
                    80,
                    30));
            _boundaryBrightness = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryBrightness",
                DefaultBoundaryBrightness,
                UiDescription(
                    "Readable brightness multiplier relative to the game's original Wyrd-boundary HDR level. 1.0 preserves the vanilla-equivalent peak brightness.",
                    "Boundary Appearance",
                    "Boundary Brightness",
                    80,
                    40,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryNearRadius = Config.Bind(
                "8. Wyrd Boundary",
                "NearRingRadius",
                DefaultBoundaryNearRadius,
                UiDescription(
                    "Visual-only radius of the nearest ring.",
                    "Advanced - Boundary Tuning",
                    "Near Radius",
                    250,
                    10,
                    new AcceptableValueRange<float>(0f, 100f)));
            _boundaryNearIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "NearRingIntensityMultiplier",
                DefaultBoundaryNearIntensity,
                UiDescription(
                    "Brightness of the near ring relative to the outer ring.",
                    "Advanced - Boundary Tuning",
                    "Near Brightness",
                    250,
                    20,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryNearThickness = Config.Bind(
                "8. Wyrd Boundary",
                "NearRingThickness",
                DefaultBoundaryNearThickness,
                UiDescription(
                    "Base visual thickness of the near ring.",
                    "Advanced - Boundary Tuning",
                    "Near Thickness",
                    250,
                    30,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryMiddleRadius = Config.Bind(
                "8. Wyrd Boundary",
                "MiddleRingRadius",
                DefaultBoundaryMiddleRadius,
                UiDescription(
                    "Visual-only radius of the middle ring.",
                    "Advanced - Boundary Tuning",
                    "Middle Radius",
                    250,
                    40,
                    new AcceptableValueRange<float>(0f, 100f)));
            _boundaryMiddleIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "MiddleRingIntensityMultiplier",
                DefaultBoundaryMiddleIntensity,
                UiDescription(
                    "Brightness of the middle ring relative to the outer ring.",
                    "Advanced - Boundary Tuning",
                    "Middle Brightness",
                    250,
                    50,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryMiddleThickness = Config.Bind(
                "8. Wyrd Boundary",
                "MiddleRingThickness",
                DefaultBoundaryMiddleThickness,
                UiDescription(
                    "Base visual thickness of the middle ring.",
                    "Advanced - Boundary Tuning",
                    "Middle Thickness",
                    250,
                    60,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryVisualRadius = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryVisualRadius",
                DefaultBoundaryOuterRadius,
                UiDescription(
                    "Visual-only radius of the outer ring. This does not alter protection or Wyrdness detection.",
                    "Advanced - Boundary Tuning",
                    "Outer Radius",
                    250,
                    70,
                    new AcceptableValueRange<float>(0f, 100f)));
            _boundaryOuterIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "OuterRingIntensityMultiplier",
                DefaultBoundaryOuterIntensity,
                UiDescription(
                    "Brightness of the outer ring relative to the shared HDR intensity.",
                    "Advanced - Boundary Tuning",
                    "Outer Brightness",
                    250,
                    80,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryThickness = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryThickness",
                DefaultBoundaryOuterThickness,
                UiDescription(
                    "Base visual thickness of the outer ring.",
                    "Advanced - Boundary Tuning",
                    "Outer Thickness",
                    250,
                    90,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryPulseEnabled = Config.Bind(
                "8. Wyrd Boundary",
                "EnableBoundaryPulse",
                true,
                UiDescription(
                    "Let each ring smoothly and independently ebb and swell within the configured limit.",
                    "Boundary Appearance",
                    "Enable Organic Pulse",
                    80,
                    50));
            _boundaryPulseAmount = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryPulseAmount",
                DefaultBoundaryPulseAmount,
                UiDescription(
                    "Maximum random brightness variation around each ring's base intensity. 1.0 permits a range from fully dimmed to roughly double brightness.",
                    "Boundary Appearance",
                    "Pulse Amount",
                    80,
                    60,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryPulseMinimumSeconds = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryPulseMinimumSeconds",
                DefaultBoundaryPulseMinimumSeconds,
                UiDescription(
                    "Shortest time used for a smooth pulse transition.",
                    "Advanced - Boundary Tuning",
                    "Minimum Pulse Duration (Seconds)",
                    250,
                    100,
                    new AcceptableValueRange<float>(0.5f, 30f)));
            _boundaryPulseMaximumSeconds = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryPulseMaximumSeconds",
                DefaultBoundaryPulseMaximumSeconds,
                UiDescription(
                    "Longest time used for a smooth pulse transition.",
                    "Advanced - Boundary Tuning",
                    "Maximum Pulse Duration (Seconds)",
                    250,
                    110,
                    new AcceptableValueRange<float>(0.5f, 30f)));

            _wyrdVisualsEnabled = Config.Bind(
                "8. Wyrd Visuals",
                "EnableWyrdnightVisuals",
                true,
                UiDescription(
                    "Enable the threat-reactive Wyrdnight palette for the environment, protection bubbles, boundary, and threat meter without changing gameplay. The meter and optional boundary remain available in their base presentation when disabled.",
                    "Wyrdnight Appearance",
                    "Enable Wyrdnight Visuals",
                    90,
                    10));
            _wyrdVisualTransitionSeconds = Config.Bind(
                "8. Wyrd Visuals",
                "WyrdVisualTransitionSeconds",
                DefaultWyrdVisualTransitionSeconds,
                UiDescription(
                    "Active-real-time duration used for natural Wyrdnight presentation transitions. The dusk fade is centered on nightfall, while the dawn fade finishes at the phase boundary. Rest and short loading transitions hold the last confirmed presentation; confirmed interiors, daylight, disabling the feature, and visual failures restore immediately.",
                    "Wyrdnight Appearance",
                    "Natural Transition Duration (Seconds)",
                    90,
                    15,
                    new AcceptableValueRange<float>(0f, 300f)));
            _wyrdnessPalette = Config.Bind(
                "8. Wyrd Visuals",
                "WyrdnessPalette",
                WyrdnessPalette.Purple,
                UiDescription(
                    "Purple uses the configured Wyrd palette and GFT Purple text group. Native Orange preserves each region's game-owned low-threat hues and uses GFT's Orange group for Wyrd messages. Both visual palettes shift toward red as threat rises, except the night sky.",
                    "Wyrdnight Appearance",
                    "Wyrdness Palette",
                    90,
                    20,
                    choiceLabels: "Purple=Purple Wyrdness;NativeOrange=Native Orange"));
            _purpleExposureMultiplier = Config.Bind(
                "8. Wyrd Visuals",
                "PurpleExposureMultiplier",
                DefaultPurpleExposureMultiplier,
                UiDescription(
                    "Purple-only multiplier applied to the game's native exposure result before Purple Night Brightness EV compensation. 1.0 leaves the native value unchanged. The effect follows the natural presentation fade and remains independent of threat.",
                    "Wyrdnight Appearance",
                    "Purple Exposure Multiplier",
                    90,
                    25,
                    new AcceptableValueRange<float>(0f, 3f)));
            _purpleExposureCompensation = Config.Bind(
                "8. Wyrd Visuals",
                "PurpleExposureCompensation",
                DefaultPurpleExposureCompensation,
                UiDescription(
                    "Purple-only, mode-aware night brightness compensation in exposure values (EV). Positive values brighten and negative values darken. Eyes applies this after Light Control without changing HDRP post-exposure, gamma, colors, or global volumes. Automatic and physical-camera exposure add the value; fixed exposure subtracts it.",
                    "Wyrdnight Appearance",
                    "Purple Night Brightness (EV)",
                    90,
                    30,
                    new AcceptableValueRange<float>(-2f, 2f)));
            _purpleIndirectDiffuseMultiplier = Config.Bind(
                "8. Wyrd Visuals",
                "PurpleIndirectDiffuseMultiplier",
                DefaultPurpleIndirectDiffuseMultiplier,
                UiDescription(
                    "Purple-only multiplier for the game's indirect diffuse lighting during a Wyrdnight. 1.0 leaves the native value unchanged. The effect follows the natural presentation fade and does not alter direct moonlight, reflections, exposure, gamma, colors, or global volumes.",
                    "Wyrdnight Appearance",
                    "Purple Indirect Diffuse Multiplier",
                    90,
                    35,
                    new AcceptableValueRange<float>(0f, 3f)));
            _threatVisualSmoothingSeconds = Config.Bind(
                "8. Wyrd Visuals",
                "ThreatVisualSmoothingSeconds",
                DefaultThreatVisualSmoothingSeconds,
                UiDescription(
                    "Half-life in active real-time seconds for world lighting and Wyrd palette changes caused by threat. Gameplay threat, the HUD meter, hunts, and dynamic night length remain immediate. 0 applies visual changes immediately.",
                    "Wyrdnight Appearance",
                    "Threat Lighting Smoothing",
                    90,
                    38,
                    new AcceptableValueRange<float>(0f, 10f)));
            _minimumThreatVisualScale = Config.Bind(
                "8. Wyrd Visuals",
                "MinimumThreatVisualScale",
                DefaultMinimumThreatVisualScale,
                UiDescription(
                    "Shared brightness and effect-strength multiplier at zero Wyrd Threat. This replaces the former boundary-only threat response.",
                    "Wyrdnight Appearance",
                    "Visual Strength at No Threat",
                    90,
                    40,
                    new AcceptableValueRange<float>(0f, 3f)));
            _maximumThreatVisualScale = Config.Bind(
                "8. Wyrd Visuals",
                "MaximumThreatVisualScale",
                DefaultMaximumThreatVisualScale,
                UiDescription(
                    "Shared brightness and effect-strength multiplier at 100 Wyrd Threat.",
                    "Wyrdnight Appearance",
                    "Visual Strength at Maximum Threat",
                    90,
                    50,
                    new AcceptableValueRange<float>(0f, 3f)));
            _threatRedColor = Config.Bind(
                "8. Wyrd Visuals",
                "ThreatRedColor",
                DefaultThreatRedColor,
                UiDescription(
                    "Target HTML RGB color approached by the moon, moonlight, bubble, boundary, and threat meter as threat rises. Wyrdnight sky color is excluded.",
                    "Wyrdnight Appearance",
                    "Threat Red Color",
                    90,
                    60));
            _maximumThreatRedBlend = Config.Bind(
                "8. Wyrd Visuals",
                "MaximumThreatRedBlend",
                DefaultMaximumThreatRedBlend,
                UiDescription(
                    "Maximum smooth blend toward Threat Red Color at 100 Wyrd Threat.",
                    "Wyrdnight Appearance",
                    "Maximum Red Shift",
                    90,
                    70,
                    new AcceptableValueRange<float>(0f, 1f)));
            _moonSurfaceColor = Config.Bind(
                "8. Wyrd Visuals",
                "MoonSurfaceColor",
                DefaultMoonSurfaceColor,
                UiDescription(
                    "Purple-palette HTML color for the visible moon disc.",
                    "Advanced - Visual Layers",
                    "Moon Surface Color",
                    260,
                    10));
            _moonSurfaceTintStrength = Config.Bind(
                "8. Wyrd Visuals",
                "MoonSurfaceTintStrength",
                DefaultMoonSurfaceTintStrength,
                UiDescription(
                    "Blend from the region's original moon surface to the selected palette.",
                    "Advanced - Visual Layers",
                    "Moon Surface Tint",
                    260,
                    20,
                    new AcceptableValueRange<float>(0f, 1f)));
            _moonSurfaceIntensity = Config.Bind(
                "8. Wyrd Visuals",
                "MoonSurfaceIntensity",
                DefaultMoonSurfaceIntensity,
                UiDescription(
                    "HDR brightness multiplier for the moon disc before the shared threat scale.",
                    "Advanced - Visual Layers",
                    "Moon Surface Intensity",
                    260,
                    30,
                    new AcceptableValueRange<float>(0f, 8f)));
            _tintMoonCorona = Config.Bind(
                "8. Wyrd Visuals",
                "TintMoonCorona",
                true,
                UiDescription(
                    "Tint the HDR flare surrounding the moon.",
                    "Advanced - Visual Layers",
                    "Tint Moon Corona",
                    260,
                    40));
            _moonCoronaColor = Config.Bind(
                "8. Wyrd Visuals",
                "MoonCoronaColor",
                DefaultMoonCoronaColor,
                UiDescription(
                    "Purple-palette HTML color for the moon corona.",
                    "Advanced - Visual Layers",
                    "Moon Corona Color",
                    260,
                    50));
            _moonCoronaIntensity = Config.Bind(
                "8. Wyrd Visuals",
                "MoonCoronaIntensity",
                DefaultMoonCoronaIntensity,
                UiDescription(
                    "Multiplier for the region's original moon-corona brightness before the shared threat scale.",
                    "Advanced - Visual Layers",
                    "Moon Corona Intensity",
                    260,
                    60,
                    new AcceptableValueRange<float>(0f, 5f)));
            _moonlightColor = Config.Bind(
                "8. Wyrd Visuals",
                "MoonlightColor",
                DefaultMoonlightColor,
                UiDescription(
                    "Purple-palette HTML color for directional and volumetric moonlight.",
                    "Advanced - Visual Layers",
                    "Moonlight Color",
                    260,
                    70));
            _moonlightTintStrength = Config.Bind(
                "8. Wyrd Visuals",
                "MoonlightTintStrength",
                DefaultMoonlightTintStrength,
                UiDescription(
                    "Blend from the region's original nighttime illumination to the selected palette.",
                    "Advanced - Visual Layers",
                    "Moonlight Tint",
                    260,
                    80,
                    new AcceptableValueRange<float>(0f, 1f)));
            _tintNightSkyAmbient = Config.Bind(
                "8. Wyrd Visuals",
                "TintNightSkyAmbient",
                true,
                UiDescription(
                    "Tint the complete visible Wyrdnight sky through the sky material. This does not directly alter fog, clouds, terrain lighting, or reflections, and it does not shift toward red with threat.",
                    "Advanced - Visual Layers",
                    "Tint Wyrdnight Sky",
                    260,
                    90));
            _nightSkyAmbientColor = Config.Bind(
                "8. Wyrd Visuals",
                "NightSkyAmbientColor",
                DefaultNightSkyAmbientColor,
                UiDescription(
                    "Purple-palette HTML color for the complete visible Wyrdnight sky.",
                    "Advanced - Visual Layers",
                    "Wyrdnight Sky Tint Color",
                    260,
                    100));
            _nightSkyAmbientTintStrength = Config.Bind(
                "8. Wyrd Visuals",
                "NightSkyAmbientTintStrength",
                DefaultNightSkyAmbientTintStrength,
                UiDescription(
                    "Blend from the region's original full-sky tint to the selected palette.",
                    "Advanced - Visual Layers",
                    "Wyrdnight Sky Tint Strength",
                    260,
                    110,
                    new AcceptableValueRange<float>(0f, 1f)));
            _tintBonfireProtectionBubble = Config.Bind(
                "8. Wyrd Visuals",
                "TintBonfireProtectionBubble",
                true,
                UiDescription(
                    "Tint fueled-bonfire protection bubbles without changing protection, radius, or fuel behavior.",
                    "Advanced - Visual Layers",
                    "Tint Protection Bubble",
                    260,
                    120));
            _protectionBubbleColor = Config.Bind(
                "8. Wyrd Visuals",
                "ProtectionBubbleColor",
                DefaultProtectionBubbleColor,
                UiDescription(
                    "Purple-palette HTML color for the protection bubble and border.",
                    "Advanced - Visual Layers",
                    "Protection Bubble Color",
                    260,
                    130));
            _protectionBubbleIntensity = Config.Bind(
                "8. Wyrd Visuals",
                "ProtectionBubbleIntensity",
                DefaultProtectionBubbleIntensity,
                UiDescription(
                    "Multiplier for the bubble body's preserved HDR brightness before the shared threat scale.",
                    "Advanced - Visual Layers",
                    "Bubble Intensity",
                    260,
                    140,
                    new AcceptableValueRange<float>(0f, 3f)));
            _protectionBubbleBorderIntensity = Config.Bind(
                "8. Wyrd Visuals",
                "ProtectionBubbleBorderIntensity",
                DefaultProtectionBubbleBorderIntensity,
                UiDescription(
                    "Multiplier for the bubble border's preserved HDR brightness before the shared threat scale.",
                    "Advanced - Visual Layers",
                    "Bubble Border Intensity",
                    260,
                    150,
                    new AcceptableValueRange<float>(0f, 3f)));

            _gftEnabled = Config.Bind(
                "9. Grail Floating Text",
                "EnableNotifications",
                true,
                UiDescription(
                    "Use optional Grail Floating Text for meaningful Wyrdnight transitions. Gameplay remains independent when GFT is absent.",
                    "Notifications", "Enable Notifications", 110, 10));
            _gftPreset = Config.Bind(
                "9. Grail Floating Text",
                "NotificationPreset",
                GftNotificationPreset.Atmospheric,
                UiDescription(
                    "Minimal shows committed official hunts and outcomes. Atmospheric adds night, upward-stage, repeated-battlecry responses, and witnessed stalker-disappearance messages. Detailed also reports stalker sightings and retreats, escalation flavor, downward stages, protection changes, and major surges.",
                    "Notifications", "Notification Preset", 110, 20));
            _gftDetailedExactThreat = Config.Bind(
                "9. Grail Floating Text",
                "DetailedShowExactThreat",
                false,
                UiDescription(
                    "Append the rounded Wyrd Threat value to Detailed non-stalker atmosphere. Stalker text never reveals its hidden aggression threshold.",
                    "Notifications", "Show Exact Threat in Detailed Text", 110, 30));
            _gftCooldownSeconds = Config.Bind(
                "9. Grail Floating Text",
                "NotificationCooldownSeconds",
                DefaultGftCooldownSeconds,
                UiDescription(
                    "Minimum active-real-time spacing within each atmospheric notification lane. Paused time does not advance it.",
                    "Notifications", "Notification Cooldown (Seconds)", 110, 40,
                    new AcceptableValueRange<float>(1f, 60f)));
            _battlecryResponseCooldownSeconds = Config.Bind(
                "9. Grail Floating Text",
                "BattlecryResponseCooldownSeconds",
                DefaultBattlecryResponseCooldownSeconds,
                UiDescription(
                    "Minimum active-real-time spacing between Wyrdnight responses to repeated battlecries. This is separate from the battlecry action cooldown and paused time does not advance it.",
                    "Notifications", "Battlecry Response Cooldown (Seconds)", 110, 50,
                    new AcceptableValueRange<float>(10f, 180f)));
            _diagnosticGftCooldownSeconds = Config.Bind(
                "10. Diagnostics",
                "GftSystemCooldownSeconds",
                DefaultDiagnosticGftCooldownSeconds,
                UiDescription(
                    "Minimum active-real-time spacing between concise diagnostics-only GFT System summaries.",
                    "Advanced - Diagnostics", "Diagnostic Text Cooldown (Seconds)", 270, 20,
                    new AcceptableValueRange<float>(1f, 60f)));

            _diagnostics = Config.Bind(
                "10. Diagnostics",
                "Diagnostics",
                false,
                UiDescription(
                    "Log accepted and rejected threat inputs, pacing, and presentation details. When GFT is available, also show concise low-priority System summaries of meaningful behind-the-scenes state changes.",
                    "Advanced - Diagnostics", "Enable Diagnostics", 270, 10));
            _enableThreatOverride = Config.Bind(
                "10. Diagnostics",
                "EnableThreatOverride",
                false,
                UiDescription(
                    "Testing control. While enabled during a valid Wyrdnight, force Wyrd Threat to the configured value and suppress natural threat gain and relief. Dawn still resets threat.",
                    "Advanced - Diagnostics", "Override Wyrd Threat", 270, 30));
            _threatOverrideValue = Config.Bind(
                "10. Diagnostics",
                "ThreatOverrideValue",
                0f,
                UiDescription(
                    "Forced Wyrd Threat used while Override Wyrd Threat is enabled. This affects the meter, visuals, night length, stalkers, and official hunts.",
                    "Advanced - Diagnostics", "Override Threat Value", 270, 40,
                    new AcceptableValueRange<float>(0f, 100f)));
            _enableTimescaleOverride = Config.Bind(
                "10. Diagnostics",
                "EnableTimescaleOverride",
                false,
                UiDescription(
                    "Testing control. While Eyes is enabled, replace normal dynamic day and Wyrdnight timing with the configured fixed multiplier of vanilla world-clock speed. This never changes Unity gameplay time.",
                    "Advanced - Diagnostics", "Override World Timescale", 270, 50));
            _timescaleOverrideMultiplier = Config.Bind(
                "10. Diagnostics",
                "TimescaleOverrideMultiplier",
                1f,
                UiDescription(
                    "Fixed world-clock speed used while Override World Timescale is enabled. 1 is vanilla speed, 2 is twice as fast, and 0.5 is half speed.",
                    "Advanced - Diagnostics", "Timescale Multiplier", 270, 60,
                    new AcceptableValueRange<float>(
                        WorldTimescalePolicy.MinimumOverrideMultiplier,
                        WorldTimescalePolicy.MaximumOverrideMultiplier)));

            _gameplayPreset.SettingChanged += OnGameplayPresetChanged;

            RestorePreservedConfigValues();
            OnGameplayPresetChanged(null, EventArgs.Empty);
            Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                Config,
                Logger,
                PluginName,
                ConfigSchemaVersion,
                ConfigRecoveryBaselineSchema,
                ConfigRecoveryKeepCurrentDefaultRules,
                ConfigRecoveryPermanentExclusions);
            Config.Save();
        }

        private ConfigEntry<float> BindThreatValue(
            string key,
            float defaultValue,
            float minimum,
            float maximum,
            string description,
            string displaySection,
            string displayName,
            int sectionOrder,
            int order)
        {
            return Config.Bind(
                "3. Wyrd Threat",
                key,
                defaultValue,
                UiDescription(
                    description,
                    displaySection,
                    displayName,
                    sectionOrder,
                    order,
                    new AcceptableValueRange<float>(
                        minimum,
                        maximum)));
        }

        private static ConfigDescription UiDescription(
            string description,
            string displaySection,
            string displayName,
            int sectionOrder,
            int order,
            AcceptableValueBase acceptableValues = null,
            string choiceLabels = "",
            bool hidden = false)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new FoASettingUiMetadata
                {
                    DisplaySection = displaySection,
                    DisplayName = displayName,
                    ChoiceLabels = choiceLabels,
                    SectionOrder = sectionOrder,
                    Order = order,
                    Hidden = hidden
                });
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (string.IsNullOrWhiteSpace(configPath)
                || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1
                    && line[0] == '['
                    && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(
                        1,
                        line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if (string.Equals(
                        currentSection,
                        "1. Core",
                        StringComparison.Ordinal)
                    && line.StartsWith(
                        schemaPrefix,
                        StringComparison.Ordinal))
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
                + storedSchemaVersion.ToString(
                    CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture)
                + ".bak";

            try
            {
                File.Copy(configPath, backupPath, false);
                File.WriteAllText(configPath, string.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
            }
            catch (Exception exception)
            {
                _pendingPreservedConfigValues.Clear();
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, configPath, true);
                        Config.Clear();
                        Config.Reload();
                    }
                }
                catch (Exception restoreException)
                {
                    Logger.LogError(
                        "Could not restore the previous Eyes in the Dark config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset the Eyes in the Dark config schema. The original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedConfigValues(
            string configPath,
            int storedSchemaVersion)
        {
            _pendingPreservedConfigValues.Clear();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            CapturePreservedValue<bool>(profile, "1. Core", "Enabled");
            CapturePreservedValue<bool>(profile, "1. Core", "OwnRestMenu");
            CapturePreservedValue<bool>(profile, "1. Core", "AllowUnprotectedWyrdnightRest");
            CapturePreservedValue<RestClockLabelFormat>(profile, "1. Core", "RestClockLabelFormat");
            CapturePreservedValue<float>(profile, "6. Rest", "RestInterruptionChanceAtZeroThreat");
            CapturePreservedValue<float>(profile, "6. Rest", "RestInterruptionChanceAtMaximumThreat");
            CapturePreservedValue<bool>(profile, "2. World Timescale", "EnableDynamicTimescale");
            CapturePreservedValue<float>(profile, "2. World Timescale", "DayMinutes");
            CapturePreservedValue<float>(profile, "2. World Timescale", "BaseNightMinutes");
            CapturePreservedValue<float>(profile, "2. World Timescale", "MaximumThreatNightMinutes");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "PassiveThreatPerNight");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "SprintThreatPerMinute");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "CombatThreatPerWindow");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "CombatResponseSeconds");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "WyrdKillThreat");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "AcquisitionThreatPerItem");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "ProtectedDecayPerMinute");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "InteriorDecayPerMinute");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "LoadReconstructionAtDawn");
            CapturePreservedValue<float>(profile, "3. Wyrd Threat", "LoadAndInteriorExitGraceSeconds");
            CapturePreservedValue<float>(profile, "4. Encounters", "BaseNightlyDangerBudget");
            CapturePreservedValue<float>(profile, "4. Encounters", "LongNightBonusScale");
            CapturePreservedValue<float>(profile, "4. Encounters", "MaximumLongNightBonus");
            CapturePreservedValue<float>(profile, "4. Encounters", "BaseHazardPerMinute");
            CapturePreservedValue<float>(profile, "4. Encounters", "ThreatHazardPerMinute");
            CapturePreservedValue<float>(profile, "4. Encounters", "NightProgressHazardPerMinute");
            CapturePreservedValue<float>(profile, "4. Encounters", "MinimumHazardTarget");
            CapturePreservedValue<float>(profile, "4. Encounters", "MaximumHazardTarget");
            CapturePreservedValue<float>(profile, "4. Encounters", "WarningSeconds");
            CapturePreservedValue<float>(profile, "4. Encounters", "DangerCostMultiplier");
            CapturePreservedValue<int>(profile, "4. Encounters", "MaximumEncounterSize");
            CapturePreservedValue<float>(profile, "4. Encounters", "SidecarChance");
            CapturePreservedValue<bool>(profile, "4. Encounters", "AllowEliteEnemies");
            CapturePreservedValue<float>(profile, "4. Encounters", "HunterSpawnDistanceMeters");
            CapturePreservedValue<float>(profile, "4. Encounters", "EscapeDistanceMeters");
            CapturePreservedValue<float>(profile, "4. Encounters", "EscapeSustainSeconds");
            CapturePreservedValue<float>(profile, "4. Encounters", "OfficialHunterKillThreatRelief");
            CapturePreservedValue<float>(profile, "4. Encounters", "OfficialHunterEscapeThreatRelief");
            CapturePreservedValue<float>(profile, "4. Encounters", "KillRecoverySeconds");
            CapturePreservedValue<float>(profile, "4. Encounters", "EscapeRecoverySeconds");
            CapturePreservedValue<float>(profile, "4. Encounters", "FailedPlacementRecoverySeconds");
            CapturePreservedValue<bool>(profile, "5. Ambient Stalkers", "EnableAmbientStalkers");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "MinimumCooldownSeconds");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "MaximumCooldownSeconds");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "MaximumCooldownAtFiftyThreatSeconds");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "ProvocationThreat");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "MinimumSpawnDistanceMeters");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "MaximumSpawnDistanceMeters");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "PassiveDespawnDistanceMeters");
            CapturePreservedValue<float>(profile, "5. Ambient Stalkers", "OffCameraDespawnSeconds");
            CapturePreservedValue<string>(profile, "7. Threat Meter", "ThreatMeterColor");
            CapturePreservedValue<bool>(profile, "7. Threat Meter", "ShowExactThreatValue");
            CapturePreservedValue<float>(profile, "7. Threat Meter", "MeterOffsetX");
            CapturePreservedValue<float>(profile, "7. Threat Meter", "MeterOffsetY");
            CapturePreservedValue<bool>(profile, "8. Wyrd Boundary", "EnableBoundaryCustomization");
            CapturePreservedValue<BoundaryRenderMode>(profile, "8. Wyrd Boundary", "BoundaryRenderMode");
            CapturePreservedValue<string>(profile, "8. Wyrd Boundary", "BoundaryColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryBrightness");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "NearRingRadius");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "NearRingIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "NearRingThickness");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MiddleRingRadius");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MiddleRingIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MiddleRingThickness");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryVisualRadius");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "OuterRingIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryThickness");
            CapturePreservedValue<bool>(profile, "8. Wyrd Boundary", "EnableBoundaryPulse");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryPulseAmount");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryPulseMinimumSeconds");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryPulseMaximumSeconds");
            CapturePreservedValue<bool>(profile, "8. Wyrd Visuals", "EnableWyrdnightVisuals");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "WyrdVisualTransitionSeconds");
            CapturePreservedValue<WyrdnessPalette>(profile, "8. Wyrd Visuals", "WyrdnessPalette");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "PurpleExposureMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "PurpleExposureCompensation");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "PurpleIndirectDiffuseMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "ThreatVisualSmoothingSeconds");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "MinimumThreatVisualScale");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "MaximumThreatVisualScale");
            CapturePreservedValue<string>(profile, "8. Wyrd Visuals", "ThreatRedColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "MaximumThreatRedBlend");
            CapturePreservedValue<string>(profile, "8. Wyrd Visuals", "MoonSurfaceColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "MoonSurfaceTintStrength");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "MoonSurfaceIntensity");
            CapturePreservedValue<bool>(profile, "8. Wyrd Visuals", "TintMoonCorona");
            CapturePreservedValue<string>(profile, "8. Wyrd Visuals", "MoonCoronaColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "MoonCoronaIntensity");
            CapturePreservedValue<string>(profile, "8. Wyrd Visuals", "MoonlightColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "MoonlightTintStrength");
            CapturePreservedValue<bool>(profile, "8. Wyrd Visuals", "TintNightSkyAmbient");
            CapturePreservedValue<string>(profile, "8. Wyrd Visuals", "NightSkyAmbientColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "NightSkyAmbientTintStrength");
            CapturePreservedValue<bool>(profile, "8. Wyrd Visuals", "TintBonfireProtectionBubble");
            CapturePreservedValue<string>(profile, "8. Wyrd Visuals", "ProtectionBubbleColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "ProtectionBubbleIntensity");
            CapturePreservedValue<float>(profile, "8. Wyrd Visuals", "ProtectionBubbleBorderIntensity");
            CapturePreservedValue<bool>(profile, "9. Grail Floating Text", "EnableNotifications");
            CapturePreservedValue<GftNotificationPreset>(profile, "9. Grail Floating Text", "NotificationPreset");
            CapturePreservedValue<bool>(profile, "9. Grail Floating Text", "DetailedShowExactThreat");
            CapturePreservedValue<float>(profile, "9. Grail Floating Text", "NotificationCooldownSeconds");
            CapturePreservedValue<float>(profile, "9. Grail Floating Text", "BattlecryResponseCooldownSeconds");
            CapturePreservedValue<float>(profile, "10. Diagnostics", "GftSystemCooldownSeconds");
            CapturePreservedValue<bool>(profile, "10. Diagnostics", "Diagnostics");
        }

        private void CapturePreservedValue<T>(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            string section,
            string key)
        {
            T previousValue;
            if (profile.TryGetCustomizedValue(
                section,
                key,
                out previousValue))
            {
                _pendingPreservedConfigValues[
                    new ConfigDefinition(section, key)] = previousValue;
            }
        }

        private void RestorePreservedConfigValues()
        {
            if (_pendingPreservedConfigValues.Count == 0)
            {
                return;
            }

            int restored = 0;
            int clamped = 0;
            int invalid = 0;
            RestorePreservedValue(_featureEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_ownRestMenu, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_allowUnprotectedWyrdnightRest, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_restClockLabelFormat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_restInterruptionChanceAtZeroThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_restInterruptionChanceAtMaximumThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_enableDynamicTimescale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_dayMinutes, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_baseNightMinutes, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_maximumThreatNightMinutes, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_passiveThreatPerNight, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_sprintThreatPerMinute, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_combatThreatPerWindow, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_combatResponseSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_wyrdKillThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_acquisitionThreatPerItem, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_protectedDecayPerMinute, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_interiorDecayPerMinute, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_loadReconstructionAtDawn, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_graceSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_baseDangerBudget, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_longNightBonusScale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_maximumLongNightBonus, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_baseHazardPerMinute, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_threatHazardPerMinute, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_nightProgressHazardPerMinute, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_minimumHazardTarget, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_maximumHazardTarget, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_warningSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_dangerCostMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_maximumPackSize, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_sidecarChance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_allowEliteEnemies, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_hunterSpawnDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_escapeDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_escapeSustainSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_killThreatRelief, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_escapeThreatRelief, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_killRecoverySeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_escapeRecoverySeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_failedPlacementRecoverySeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_enableAmbientStalkers, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerMinimumCooldown, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerMaximumCooldown, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerMaximumCooldownAtFiftyThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerProvocationThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerMinimumSpawnDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerMaximumSpawnDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerPassiveDespawnDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_stalkerOffCameraDespawnSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_threatMeterColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_showExactThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_meterOffsetX, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_meterOffsetY, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryBrightness, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryVisualRadius, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryThickness, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_wyrdVisualsEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_wyrdVisualTransitionSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_wyrdnessPalette, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_purpleExposureMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_purpleExposureCompensation, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_purpleIndirectDiffuseMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_threatVisualSmoothingSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_minimumThreatVisualScale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_maximumThreatVisualScale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_threatRedColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_maximumThreatRedBlend, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_moonSurfaceColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_moonSurfaceTintStrength, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_moonSurfaceIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_tintMoonCorona, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_moonCoronaColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_moonCoronaIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_moonlightColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_moonlightTintStrength, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_tintNightSkyAmbient, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_nightSkyAmbientColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_nightSkyAmbientTintStrength, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_tintBonfireProtectionBubble, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_protectionBubbleColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_protectionBubbleIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_protectionBubbleBorderIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftPreset, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftDetailedExactThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftCooldownSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_battlecryResponseCooldownSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_diagnosticGftCooldownSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_diagnostics, ref restored, ref clamped, ref invalid);

            Logger.LogInfo(
                "Preserved "
                + restored.ToString(CultureInfo.InvariantCulture)
                + " Eyes in the Dark setting(s) across the config schema reset; clamped="
                + clamped.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + invalid.ToString(CultureInfo.InvariantCulture)
                + ".");
            _pendingPreservedConfigValues.Clear();
        }

        private void RestorePreservedValue<T>(
            ConfigEntry<T> entry,
            ref int restored,
            ref int clamped,
            ref int invalid)
        {
            object boxedValue;
            if (entry == null
                || !_pendingPreservedConfigValues.TryGetValue(
                    entry.Definition,
                    out boxedValue)
                || !(boxedValue is T))
            {
                return;
            }

            bool wasClamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                (T)boxedValue,
                out wasClamped))
            {
                invalid++;
                return;
            }

            restored++;
            if (wasClamped)
            {
                clamped++;
            }
        }

        private struct RuntimeContext
        {
            public static readonly RuntimeContext Unknown =
                new RuntimeContext(
                    new NightObservation(),
                    new NightStateDecision(
                        DirectorState.Inactive,
                        InactiveReason.SceneUnknown),
                    "<unknown>",
                    false,
                    false,
                    true,
                    0f,
                    1f);

            public readonly NightObservation Observation;
            public readonly NightStateDecision Decision;
            public readonly string SceneName;
            public readonly bool IsProtected;
            public readonly bool IsExposed;
            public readonly bool IsPaused;
            public readonly float NightProgress;
            public readonly float WorldDurationMultiplier;

            public RuntimeContext(
                NightObservation observation,
                NightStateDecision decision,
                string sceneName,
                bool isProtected,
                bool isExposed,
                bool isPaused,
                float nightProgress,
                float worldDurationMultiplier)
            {
                Observation = observation;
                Decision = decision;
                SceneName = sceneName;
                IsProtected = isProtected;
                IsExposed = isExposed;
                IsPaused = isPaused;
                NightProgress = nightProgress;
                WorldDurationMultiplier = worldDurationMultiplier;
            }
        }

        private static class HeroHudPatch
        {
            internal static void AfterFullyInitialized(
                VHeroHUD __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.AttachMeter(__instance);
                }
            }

            internal static void AfterDiscard(VHeroHUD __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.DetachMeter(__instance);
                }
            }
        }

        private static class GameplayLoadPatch
        {
            internal static void BeforeLoad()
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.NotifyGameplayLoad();
                }
            }
        }

        private static class RestPatch
        {
            internal static void AfterCanRest(ref bool __result)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    __result = instance.CanUseNativeRest(__result);
                }
            }

            internal static bool BeforeRest(RestPopupUI __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                return instance == null
                    || instance.TryBeginRest(__instance);
            }

            internal static void AfterWillBeSurprised(
                RestPopupUI __instance,
                ref bool __result)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null
                    && instance.ShouldSuppressNativeWyrdnightSurprise(
                        __instance,
                        __result))
                {
                    __result = false;
                }
            }

            internal static void AfterWillSkipTimeBeInterrupted(
                float skipTimeInHours,
                bool safelySkipping,
                ref float skipTimeInHoursTillInterrupt,
                ref bool __result)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.ApplyRestInterruptionRisk(
                        skipTimeInHours,
                        safelySkipping,
                        ref skipTimeInHoursTillInterrupt,
                        ref __result);
                }
            }

            internal static void AfterFireplaceInitialize(
                VFireplaceUI __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.RegisterRestAvailabilityView(__instance);
                }
            }

            internal static void AfterFireplaceRefresh(
                VWyrdRepellingFireplaceUI __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.RegisterRestAvailabilityView(__instance);
                }
            }

            internal static void AfterFireplaceDiscard(
                VFireplaceUI __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.UnregisterRestAvailabilityView(__instance);
                }
            }
        }

        private static class RestClockPatch
        {
            internal static void AfterInitialize(VRestPopupUI __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.AttachRestClock(__instance);
                }
            }

            internal static void AfterRefresh(VRestPopupUI __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.RefreshRestClock(__instance);
                }
            }

            internal static void BeforeSetHourChangeBasedOnAngle(
                VRestPopupUI __instance,
                ref float angle)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null
                    && instance.UsesNoonAtTopRestClock(__instance))
                {
                    angle += 180f;
                }
            }
        }

        private static class QuickWeatherTimePatch
        {
            internal static void AfterAttach(
                VCQuickWeatherTime __instance)
            {
                EyesInTheDarkPlugin instance = Instance;
                if (instance != null)
                {
                    instance.FormatQuickWeatherTime(__instance);
                }
            }
        }
    }
}
