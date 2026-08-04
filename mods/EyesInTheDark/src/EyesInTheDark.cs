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
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.General.Configs;
using Awaken.TG.Main.Heroes;
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
using Awaken.TG.Main.Wyrdnessing;
using Awaken.Utility;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Eyes in the Dark - Wyrdnight Encounters")]
[assembly: AssemblyDescription("A timescale-aware Wyrdnight threat and encounter overhaul")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Eyes in the Dark - Wyrdnight Encounters")]
[assembly: AssemblyVersion("0.9.1.0")]
[assembly: AssemblyFileVersion("0.9.1.0")]
[assembly: AssemblyInformationalVersion("0.9.1")]

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

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class EyesInTheDarkPlugin : BaseUnityPlugin, IListenerOwner
    {
        public const string PluginGuid = "ks.tgfoa.eyes-in-the-dark";
        public const string PluginName = "Eyes in the Dark";
        public const string PluginVersion = "0.9.1";
        private const string GloriousUiPluginGuid =
            "ks.tgfoa.glorious-ui";

        private const int ConfigSchemaVersion = 6;
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
                        "Standalone placement now provides the former vertical calibration as an internal baseline.")
                };
        private static readonly ConfigDefinition[]
            ConfigRecoveryPermanentExclusions =
                new[]
                {
                    new ConfigDefinition(
                        "2. Gameplay Preset",
                        "ApplyPreset")
                };

        private const float StatePollIntervalSeconds = 0.2f;
        private const float MaximumClockStepSeconds = 0.5f;
        private const float FastSwimMinimumSpeed = 3.0f;
        private const float MeaningfulDamageMinimum = 1.0f;
        private const float ListenerRetryBackoffSeconds = 30.0f;
        private const float ContinuousThreatDiagnosticIntervalSeconds =
            10.0f;

        private const float DefaultDayTimescale = 0.23f;
        private const float DefaultNightTimescale = 0.413f;

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
        private const float StandaloneMeterBaselineOffsetX = 9.0f;
        private const float StandaloneMeterBaselineOffsetY = -9.0f;
        private const string DefaultBoundaryColor = "#B878FF";
        private const float DefaultBoundaryHdrIntensity = 271.529f;
        private const float DefaultBoundaryNearRadius = 12.0f;
        private const float DefaultBoundaryNearIntensity = 0.35f;
        private const float DefaultBoundaryNearThickness = 0.08f;
        private const float DefaultBoundaryMiddleRadius = 22.0f;
        private const float DefaultBoundaryMiddleIntensity = 0.60f;
        private const float DefaultBoundaryMiddleThickness = 0.14f;
        private const float DefaultBoundaryOuterRadius = 32.0f;
        private const float DefaultBoundaryOuterIntensity = 1.0f;
        private const float DefaultBoundaryOuterThickness = 0.25f;
        private const float DefaultBoundaryMinimumIntensity = 1.0f;
        private const float DefaultBoundaryMaximumIntensity = 1.2f;
        private const float DefaultBoundaryMaximumThickness = 1.15f;
        private const float DefaultBoundaryPulseAmount = 0.12f;
        private const float DefaultBoundaryPulseMinimumSeconds = 2.5f;
        private const float DefaultBoundaryPulseMaximumSeconds = 6.0f;
        private const float DefaultGftCooldownSeconds = 8.0f;
        private const float DefaultDiagnosticGftCooldownSeconds = 3.0f;

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
        private readonly HuntDirector _huntDirector =
            new HuntDirector(unchecked(Environment.TickCount * 397));
        private readonly HunterCatalogDirector _hunterCatalog =
            new HunterCatalogDirector(
                unchecked(Environment.TickCount * 613));
        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedConfigValues =
                new Dictionary<ConfigDefinition, object>();

        private ConfigEntry<bool> _featureEnabled;
        private ConfigEntry<bool> _enableDynamicTimescale;
        private ConfigEntry<float> _dayTimescale;
        private ConfigEntry<float> _nightTimescale;
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
        private ConfigEntry<float> _boundaryHdrIntensity;
        private ConfigEntry<float> _boundaryVisualRadius;
        private ConfigEntry<float> _boundaryThickness;
        private ConfigEntry<float> _boundaryNearRadius;
        private ConfigEntry<float> _boundaryNearIntensity;
        private ConfigEntry<float> _boundaryNearThickness;
        private ConfigEntry<float> _boundaryMiddleRadius;
        private ConfigEntry<float> _boundaryMiddleIntensity;
        private ConfigEntry<float> _boundaryMiddleThickness;
        private ConfigEntry<float> _boundaryOuterIntensity;
        private ConfigEntry<BoundaryThreatReactivity>
            _boundaryThreatReactivity;
        private ConfigEntry<float> _boundaryMinimumIntensity;
        private ConfigEntry<float> _boundaryMaximumIntensity;
        private ConfigEntry<float> _boundaryMaximumThickness;
        private ConfigEntry<bool> _boundaryPulseEnabled;
        private ConfigEntry<float> _boundaryPulseAmount;
        private ConfigEntry<float> _boundaryPulseMinimumSeconds;
        private ConfigEntry<float> _boundaryPulseMaximumSeconds;
        private ConfigEntry<bool> _gftEnabled;
        private ConfigEntry<GftNotificationPreset> _gftPreset;
        private ConfigEntry<bool> _gftDetailedExactThreat;
        private ConfigEntry<float> _gftCooldownSeconds;
        private ConfigEntry<float> _diagnosticGftCooldownSeconds;
        private ConfigEntry<bool> _diagnostics;

        private Harmony _harmony;
        private ThreatMeterController _meter;
        private BoundaryController _boundary;
        private GrailFloatingTextBridge _gft;
        private FirstHunterRuntime _hunterRuntime;
        private WorldTimescaleController _worldTimescale;
        private Hero _trackedHero;
        private IEventListener _attackStartListener;
        private IEventListener _environmentHitListener;
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
        private bool _meterFailureLogged;
        private bool _boundaryFailureLogged;
        private bool _worldTimescaleFailureLogged;
        private string _activeHuntSceneName;
        private bool _activeHuntBudgetSpent;
        private float _activeHuntDangerCost;
        private HuntEncounterPlan _pendingHuntPlan;
        private bool _applyingGameplayPreset;
        private float _pendingPassiveThreatDiagnostic;
        private float _pendingProtectedDecayDiagnostic;
        private float _pendingInteriorDecayDiagnostic;
        private double _nextContinuousThreatDiagnosticSeconds;

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
                _hunterRuntime = new FirstHunterRuntime(
                    Logger,
                    unchecked(Environment.TickCount * 911));
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

            UpdateWorldTimescale();

            _activeRealTimeClock.Advance(
                unscaledDelta,
                _hasContext
                    && NightStateEvaluator.CanAdvanceActiveClock(
                        _currentContext.Observation,
                        paused));

            TrackHero(Hero.Current);
            BindAcquisitionListeners();

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
                    _hasKnownProtectionState = false;
                    ResetHuntRuntime("feature disabled", true);
                }
            }
            else
            {
                AdvanceThreat(nextContext, activeDelta);
                AdvanceHunt(nextContext, activeDelta);
            }
            _wasFeatureEnabled = featureEnabled;

            if (!_hasContext
                || !SameDiagnosticState(_currentContext, nextContext))
            {
                LogTransition(nextContext);
                ObserveContextTransition(nextContext);
            }

            _currentContext = nextContext;
            _hasContext = true;
            UpdateMeter(featureEnabled, nextContext);
            UpdateBoundary(featureEnabled);
        }

        private void OnDestroy()
        {
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

        private void UpdateWorldTimescale()
        {
            if (_worldTimescale == null)
            {
                return;
            }

            try
            {
                GameRealTime clock = World.Any<GameRealTime>();
                _worldTimescale.Update(
                    clock,
                    VanillaCycleMinutes(),
                    IsFeatureEnabled()
                        && (_enableDynamicTimescale == null
                            || _enableDynamicTimescale.Value),
                    ValueOrDefault(
                        _dayTimescale,
                        DefaultDayTimescale),
                    ValueOrDefault(
                        _nightTimescale,
                        DefaultNightTimescale));
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
            ThreatUpdateResult update = _threat.Advance(
                frame,
                CurrentThreatTuning());
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
                if (update.Cause == ThreatChangeCause.DawnReset)
                {
                    ResolveHunt(
                        HuntResolution.Dawn,
                        "Wyrdnight ended at dawn",
                        false,
                        true);
                    _pacing.Reset();
                    _notificationCooldowns.Reset();
                    _hasKnownProtectionState = false;
                }
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
            ShowDiagnosticSystem(
                "EITD - Threat +"
                + (result.CurrentThreat - result.PreviousThreat).ToString(
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
            ObserveAtmosphericThreat(result);
            LogStageChange(result);
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
                || cause == ThreatChangeCause.ProtectedDecay
                || cause == ThreatChangeCause.InteriorDecay;
        }

        private void AccumulateContinuousThreatDiagnostic(
            ThreatUpdateResult result)
        {
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
            bool hasPending = Math.Abs(_pendingPassiveThreatDiagnostic)
                    > 0.0001f
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
                + _pendingProtectedDecayDiagnostic
                + _pendingInteriorDecayDiagnostic;
            LogDiagnostic(
                "Continuous threat summary: passive="
                + _pendingPassiveThreatDiagnostic.ToString(
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

        private void OnDamageDealt(DamageOutcome outcome)
        {
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
                    ref _damageTakenListener);
                World.EventSystem.TryDisposeListener(
                    ref _damageDealtListener);
                World.EventSystem.TryDisposeListener(ref _killListener);
            }
            else
            {
                _attackStartListener = null;
                _environmentHitListener = null;
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
            HuntTuning huntTuning = CurrentHuntTuning();
            _huntDirector.ResetNight(huntTuning);
            _activeHuntBudgetSpent = false;
            _activeHuntDangerCost = 0f;
            _pendingHuntPlan = null;
            _activeHuntSceneName = string.Empty;
            NightBudgetSnapshot snapshot = _pacing.BeginNight(
                context.WorldDurationMultiplier,
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
            if (_hunterRuntime == null || !_pacing.IsInitialized)
            {
                return;
            }

            NightObservation observation = context.Observation;
            bool transient = observation.AtTitleScreen
                || observation.IsLoading
                || observation.IsTransitioning
                || observation.IsTraveling
                || observation.IsResting;

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
            if (_gft == null
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
            if (preset == GftNotificationPreset.Detailed
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
                    : eventKind == AtmosphereEventKind.HuntCommitted
                        || eventKind == AtmosphereEventKind.HunterKilled
                        || eventKind == AtmosphereEventKind.HunterEscaped
                            ? "eyes-in-the-dark-hunt"
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
                        == AtmosphereEventKind.HuntCommitted);
            }
        }

        private void ShowDiagnosticSystem(string text)
        {
            if (_gft == null
                || _diagnostics == null
                || !_diagnostics.Value)
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

        private void UpdateBoundary(bool featureEnabled)
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
                    _threat.Value);
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
                Color = _parsedBoundaryColor,
                HdrIntensity = ValueOrDefault(
                    _boundaryHdrIntensity,
                    DefaultBoundaryHdrIntensity),
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
                ThreatReactivity = _boundaryThreatReactivity == null
                    ? BoundaryThreatReactivity.Subtle
                    : _boundaryThreatReactivity.Value,
                MinimumIntensityMultiplier = ValueOrDefault(
                    _boundaryMinimumIntensity,
                    DefaultBoundaryMinimumIntensity),
                MaximumIntensityMultiplier = ValueOrDefault(
                    _boundaryMaximumIntensity,
                    DefaultBoundaryMaximumIntensity),
                MaximumThicknessMultiplier = ValueOrDefault(
                    _boundaryMaximumThickness,
                    DefaultBoundaryMaximumThickness),
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
            PatchHeroHud();
            PatchGameplayLoad();
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
            ResolveHunt(
                HuntResolution.GameplayLoad,
                "native gameplay load started",
                false,
                true);
            _threat.NotifyLoad();
            _activity.ResetNight();
            _pacing.Reset();
            _notificationCooldowns.Reset();
            _hasKnownProtectionState = false;
            LogDiagnostic(
                "Threat state cleared at the native gameplay-load entry point; no catch-up work was retained.");
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
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
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

        private static string FormatCause(ThreatChangeCause cause)
        {
            switch (cause)
            {
                case ThreatChangeCause.SprintOrFastSwim:
                    return "movement";
                case ThreatChangeCause.WyrdKill:
                    return "Wyrd kill";
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
                        _killRecoverySeconds.Value = 120f;
                        _escapeRecoverySeconds.Value = 240f;
                        _failedPlacementRecoverySeconds.Value = 45f;
                        break;
                    case GameplayTuningPreset.WatchfulNight:
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
                        _killRecoverySeconds.Value =
                            DefaultKillRecoverySeconds;
                        _escapeRecoverySeconds.Value =
                            DefaultEscapeRecoverySeconds;
                        _failedPlacementRecoverySeconds.Value =
                            DefaultFailedPlacementRecoverySeconds;
                        break;
                    case GameplayTuningPreset.CursedNight:
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
                        ? "enabled above 75% threat"
                        : "disabled")
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

            _enableDynamicTimescale = Config.Bind(
                "2. World Timescale",
                "EnableDynamicTimescale",
                true,
                UiDescription(
                    "Let Eyes own separate day and night world-weather rates. This never changes Unity gameplay Time.timeScale.",
                    "World Clock",
                    "Use Separate Day and Night Rates",
                    10,
                    10));
            _dayTimescale = Config.Bind(
                "2. World Timescale",
                "DayTimescale",
                DefaultDayTimescale,
                UiDescription(
                    "World-weather multiplier used during the day. 0.23 produces approximately 60 real minutes of daylight.",
                    "World Clock",
                    "Day Timescale",
                    10,
                    20,
                    new AcceptableValueRange<float>(
                        WorldTimescalePolicy.MinimumMultiplier,
                        WorldTimescalePolicy.MaximumMultiplier)));
            _nightTimescale = Config.Bind(
                "2. World Timescale",
                "NightTimescale",
                DefaultNightTimescale,
                UiDescription(
                    "World-weather multiplier used at night. 0.413 produces approximately 15 real minutes of night.",
                    "World Clock",
                    "Night Timescale",
                    10,
                    30,
                    new AcceptableValueRange<float>(
                        WorldTimescalePolicy.MinimumMultiplier,
                        WorldTimescalePolicy.MaximumMultiplier)));

            _gameplayPreset = Config.Bind(
                "2. Gameplay Preset",
                "ApplyPreset",
                GameplayTuningPreset.Custom,
                UiDescription(
                    "One-shot gameplay template. Uneasy Night, Watchful Night, or Cursed Night writes threat and encounter tuning, then returns to Custom. HUD, GFT, boundary, and diagnostic preferences are preserved.",
                    "General",
                    "Apply Gameplay Preset",
                    0,
                    20,
                    choiceLabels: "Custom=Custom;UneasyNight=Uneasy Night;WatchfulNight=Watchful Night;CursedNight=Cursed Night"));

            _passiveThreatPerNight = BindThreatValue(
                "PassiveThreatPerNight",
                DefaultPassiveThreatPerNight,
                0f,
                100f,
                "Threat gained across one complete exposed outdoor Wyrdnight. Progress-based calculation keeps this baseline independent of world timescale.",
                "Threat - Generation",
                "Passive Threat per Night",
                20,
                10);
            _sprintThreatPerMinute = BindThreatValue(
                "SprintThreatPerMinute",
                DefaultSprintThreatPerMinute,
                0f,
                30f,
                "Threat gained per minute of sustained exposed sprinting or fast swimming, committed in non-spammable intervals.",
                "Threat - Generation",
                "Sprint Threat per Minute",
                20,
                20);
            _combatThreatPerWindow = BindThreatValue(
                "CombatThreatPerWindow",
                DefaultCombatThreatPerWindow,
                0f,
                10f,
                "Maximum threat from meaningful damage events in each short aggregation window.",
                "Threat - Generation",
                "Combat Threat per Window",
                20,
                30);
            _combatResponseSeconds = BindThreatValue(
                "CombatResponseSeconds",
                DefaultCombatResponseSeconds,
                0.25f,
                5f,
                "Active real-time delay before queued combat and confirmed environment-impact threat is committed. The same window caps their combined contribution.",
                "Threat - Generation",
                "Combat Response Delay",
                20,
                40);
            _wyrdKillThreat = BindThreatValue(
                "WyrdKillThreat",
                DefaultWyrdKillThreat,
                0f,
                20f,
                "Threat gained when the Hero kills a Wyrd-converted or Wyrdness-bound NPC.",
                "Threat - Generation",
                "Wyrd Kill Threat",
                20,
                50);
            _acquisitionThreatPerItem = BindThreatValue(
                "AcquisitionThreatPerItem",
                DefaultAcquisitionThreatPerItem,
                0f,
                5f,
                "Threat queued for each unique direct pickup or item taken from a location while exposed. Short windows cap bulk looting.",
                "Threat - Generation",
                "Item Acquisition Threat",
                20,
                60);
            _protectedDecayPerMinute = BindThreatValue(
                "ProtectedDecayPerMinute",
                DefaultProtectedDecayPerMinute,
                0f,
                60f,
                "Threat removed per active real-time minute while outdoors and protected from the Wyrdness.",
                "Threat - Decay and Loading",
                "Protected Decay per Minute",
                30,
                10);
            _interiorDecayPerMinute = BindThreatValue(
                "InteriorDecayPerMinute",
                DefaultInteriorDecayPerMinute,
                0f,
                30f,
                "Threat removed per active real-time minute indoors during a valid Wyrdnight.",
                "Threat - Decay and Loading",
                "Interior Decay per Minute",
                30,
                20);
            _loadReconstructionAtDawn = BindThreatValue(
                "LoadReconstructionAtDawn",
                DefaultLoadReconstructionAtDawn,
                0f,
                40f,
                "Maximum modest threat reconstructed by dawn progress after loading during a Wyrdnight.",
                "Threat - Decay and Loading",
                "Load Reconstruction at Dawn",
                30,
                30);
            _graceSeconds = BindThreatValue(
                "LoadAndInteriorExitGraceSeconds",
                DefaultGraceSeconds,
                0f,
                60f,
                "Active real-time seconds during which activity threat is suppressed after a Wyrdnight load or interior exit.",
                "Threat - Decay and Loading",
                "Load and Exit Grace",
                30,
                40);

            _baseDangerBudget = Config.Bind(
                "4. Encounters",
                "BaseNightlyDangerBudget",
                DefaultBaseDangerBudget,
                UiDescription(
                    "Base danger budget calculated once per Wyrdnight and spent only after complete curated encounter placement is confirmed.",
                    "Hunts - Pacing", "Nightly Danger Budget", 40, 10,
                    new AcceptableValueRange<float>(0f, 200f)));
            _longNightBonusScale = Config.Bind(
                "4. Encounters",
                "LongNightBonusScale",
                DefaultLongNightBonusScale,
                UiDescription(
                    "Scales the sublinear square-root budget bonus for world-clock nights longer than the game's default.",
                    "Hunts - Pacing", "Long Night Bonus Scale", 40, 20,
                    new AcceptableValueRange<float>(0f, 2f)));
            _maximumLongNightBonus = Config.Bind(
                "4. Encounters",
                "MaximumLongNightBonus",
                DefaultMaximumLongNightBonus,
                UiDescription(
                    "Maximum extra fraction of the base nightly budget granted for an extended world-clock night. 0.75 caps the total at 175% of base.",
                    "Hunts - Pacing", "Maximum Long Night Bonus", 40, 30,
                    new AcceptableValueRange<float>(0f, 3f)));
            _baseHazardPerMinute = Config.Bind(
                "4. Encounters",
                "BaseHazardPerMinute",
                DefaultBaseHazardPerMinute,
                UiDescription(
                    "Quiet baseline added to accumulated hunt hazard per active exposed minute. This is not an independent random roll.",
                    "Hunts - Pacing", "Base Hazard per Minute", 40, 40,
                    new AcceptableValueRange<float>(0f, 5f)));
            _threatHazardPerMinute = Config.Bind(
                "4. Encounters",
                "ThreatHazardPerMinute",
                DefaultThreatHazardPerMinute,
                UiDescription(
                    "Maximum additional accumulated hazard per exposed minute from Wyrd Threat. Threat uses a rising nonlinear curve.",
                    "Hunts - Pacing", "Threat Hazard per Minute", 40, 50,
                    new AcceptableValueRange<float>(0f, 5f)));
            _nightProgressHazardPerMinute = Config.Bind(
                "4. Encounters",
                "NightProgressHazardPerMinute",
                DefaultNightProgressHazardPerMinute,
                UiDescription(
                    "Maximum additional accumulated hazard per exposed minute as the Wyrdnight advances.",
                    "Hunts - Pacing", "Night Progress Hazard per Minute", 40, 60,
                    new AcceptableValueRange<float>(0f, 5f)));
            _minimumHazardTarget = Config.Bind(
                "4. Encounters",
                "MinimumHazardTarget",
                DefaultMinimumHazardTarget,
                UiDescription(
                    "Lower bound for the randomized accumulated-hazard target selected for each hunt opportunity.",
                    "Hunts - Pacing", "Minimum Hazard Target", 40, 70,
                    new AcceptableValueRange<float>(0.1f, 10f)));
            _maximumHazardTarget = Config.Bind(
                "4. Encounters",
                "MaximumHazardTarget",
                DefaultMaximumHazardTarget,
                UiDescription(
                    "Upper bound for the randomized accumulated-hazard target selected for each hunt opportunity.",
                    "Hunts - Pacing", "Maximum Hazard Target", 40, 80,
                    new AcceptableValueRange<float>(0.1f, 10f)));
            _warningSeconds = Config.Bind(
                "4. Encounters",
                "WarningSeconds",
                DefaultWarningSeconds,
                UiDescription(
                    "Active-real-time warning delay between hunt commitment and placement. Eligibility is checked again before spawning.",
                    "Hunts - Pacing", "Warning Duration", 40, 90,
                    new AcceptableValueRange<float>(1f, 30f)));
            _dangerCostMultiplier = Config.Bind(
                "4. Encounters",
                "DangerCostMultiplier",
                DefaultDangerCostMultiplier,
                UiDescription(
                    "Multiplier applied to each curated profile's reviewed danger cost. Budget is spent only after the complete encounter is confirmed.",
                    "Hunts - Composition", "Danger Cost Multiplier", 50, 10,
                    new AcceptableValueRange<float>(0.5f, 2f)));
            _maximumPackSize = Config.Bind(
                "4. Encounters",
                "MaximumEncounterSize",
                DefaultMaximumPackSize,
                UiDescription(
                    "Maximum official encounter size. Player level, profile safety, composition rules, and remaining danger budget can reduce it.",
                    "Hunts - Composition", "Maximum Encounter Size", 50, 20,
                    new AcceptableValueRange<int>(1, 3)));
            _sidecarChance = Config.Bind(
                "4. Encounters",
                "SidecarChance",
                DefaultSidecarChance,
                UiDescription(
                    "Maximum chance to add each weaker curated sidecar. Actual chance rises smoothly with Wyrd Threat and is capped by level, preset, profile safety, and budget.",
                    "Hunts - Composition", "Sidecar Chance", 50, 30,
                    new AcceptableValueRange<float>(0f, 1f)));
            _allowEliteEnemies = Config.Bind(
                "4. Encounters",
                "AllowEliteEnemies",
                false,
                UiDescription(
                    "Allow reviewed elite official hunters only when Wyrd Threat is greater than 75%. Bosses, minibosses, story actors, summons, and challenge or trial variants remain excluded.",
                    "Hunts - Composition", "Allow Elite Enemies", 50, 35));
            _hunterSpawnDistance = Config.Bind(
                "4. Encounters",
                "HunterSpawnDistanceMeters",
                DefaultHunterSpawnDistance,
                UiDescription(
                    "Requested distance for curated official hunters. Native navigation placement and member separation are verified before spawning.",
                    "Hunts - Composition", "Hunter Spawn Distance", 50, 40,
                    new AcceptableValueRange<float>(20f, 60f)));
            _escapeDistance = Config.Bind(
                "4. Encounters",
                "EscapeDistanceMeters",
                DefaultEscapeDistance,
                UiDescription(
                    "Distance that must be sustained from the exact official hunter to escape outdoors.",
                    "Hunts - Resolution", "Escape Distance", 60, 10,
                    new AcceptableValueRange<float>(30f, 200f)));
            _escapeSustainSeconds = Config.Bind(
                "4. Encounters",
                "EscapeSustainSeconds",
                DefaultEscapeSustainSeconds,
                UiDescription(
                    "Active real-time seconds the escape distance must be sustained.",
                    "Hunts - Resolution", "Escape Sustain Duration", 60, 20,
                    new AcceptableValueRange<float>(1f, 60f)));
            _killThreatRelief = Config.Bind(
                "4. Encounters",
                "OfficialHunterKillThreatRelief",
                DefaultKillThreatRelief,
                UiDescription(
                    "Wyrd Threat removed when the exact official hunter dies. This should remain greater than escape relief.",
                    "Hunts - Resolution", "Kill Threat Relief", 60, 30,
                    new AcceptableValueRange<float>(0f, 100f)));
            _escapeThreatRelief = Config.Bind(
                "4. Encounters",
                "OfficialHunterEscapeThreatRelief",
                DefaultEscapeThreatRelief,
                UiDescription(
                    "Wyrd Threat removed after a sustained outdoor escape or interior escape.",
                    "Hunts - Resolution", "Escape Threat Relief", 60, 40,
                    new AcceptableValueRange<float>(0f, 100f)));
            _killRecoverySeconds = Config.Bind(
                "4. Encounters",
                "KillRecoverySeconds",
                DefaultKillRecoverySeconds,
                UiDescription(
                    "Active real-time recovery after killing the official hunter.",
                    "Hunts - Resolution", "Kill Recovery", 60, 50,
                    new AcceptableValueRange<float>(10f, 600f)));
            _escapeRecoverySeconds = Config.Bind(
                "4. Encounters",
                "EscapeRecoverySeconds",
                DefaultEscapeRecoverySeconds,
                UiDescription(
                    "Longer active real-time Recently Pursued recovery after escaping the official hunter.",
                    "Hunts - Resolution", "Escape Recovery", 60, 60,
                    new AcceptableValueRange<float>(10f, 900f)));
            _failedPlacementRecoverySeconds = Config.Bind(
                "4. Encounters",
                "FailedPlacementRecoverySeconds",
                DefaultFailedPlacementRecoverySeconds,
                UiDescription(
                    "Short active real-time retry protection after an invalid or failed placement. No danger budget is spent.",
                    "Hunts - Resolution", "Failed Placement Recovery", 60, 70,
                    new AcceptableValueRange<float>(5f, 180f)));

            _threatMeterColor = Config.Bind(
                "7. Threat Meter",
                "ThreatMeterColor",
                ThreatMeterController.DefaultColorText,
                UiDescription(
                    "HTML RGB color for the Wyrd Threat meter, such as #B878FF.",
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
                    "Boundary - Appearance",
                    "Customize Wyrd Boundary",
                    80,
                    10));
            _boundaryRenderMode = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryRenderMode",
                BoundaryRenderMode.Layered,
                UiDescription(
                    "Layered draws independent near, middle, and outer visual rings. Single keeps one native-style outer edge.",
                    "Boundary - Appearance",
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
                    "Boundary - Appearance",
                    "Boundary Color",
                    80,
                    30));
            _boundaryHdrIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryHdrIntensity",
                DefaultBoundaryHdrIntensity,
                UiDescription(
                    "HDR multiplier applied to the configured boundary color.",
                    "Boundary - Appearance",
                    "HDR Intensity",
                    80,
                    40,
                    new AcceptableValueRange<float>(0f, 500f)));
            _boundaryNearRadius = Config.Bind(
                "8. Wyrd Boundary",
                "NearRingRadius",
                DefaultBoundaryNearRadius,
                UiDescription(
                    "Visual-only radius of the nearest ring.",
                    "Boundary - Rings",
                    "Near Radius",
                    90,
                    10,
                    new AcceptableValueRange<float>(0f, 100f)));
            _boundaryNearIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "NearRingIntensityMultiplier",
                DefaultBoundaryNearIntensity,
                UiDescription(
                    "Brightness of the near ring relative to the outer ring.",
                    "Boundary - Rings",
                    "Near Brightness",
                    90,
                    20,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryNearThickness = Config.Bind(
                "8. Wyrd Boundary",
                "NearRingThickness",
                DefaultBoundaryNearThickness,
                UiDescription(
                    "Base visual thickness of the near ring.",
                    "Boundary - Rings",
                    "Near Thickness",
                    90,
                    30,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryMiddleRadius = Config.Bind(
                "8. Wyrd Boundary",
                "MiddleRingRadius",
                DefaultBoundaryMiddleRadius,
                UiDescription(
                    "Visual-only radius of the middle ring.",
                    "Boundary - Rings",
                    "Middle Radius",
                    90,
                    40,
                    new AcceptableValueRange<float>(0f, 100f)));
            _boundaryMiddleIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "MiddleRingIntensityMultiplier",
                DefaultBoundaryMiddleIntensity,
                UiDescription(
                    "Brightness of the middle ring relative to the outer ring.",
                    "Boundary - Rings",
                    "Middle Brightness",
                    90,
                    50,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryMiddleThickness = Config.Bind(
                "8. Wyrd Boundary",
                "MiddleRingThickness",
                DefaultBoundaryMiddleThickness,
                UiDescription(
                    "Base visual thickness of the middle ring.",
                    "Boundary - Rings",
                    "Middle Thickness",
                    90,
                    60,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryVisualRadius = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryVisualRadius",
                DefaultBoundaryOuterRadius,
                UiDescription(
                    "Visual-only radius of the outer ring. This does not alter protection or Wyrdness detection.",
                    "Boundary - Rings",
                    "Outer Radius",
                    90,
                    70,
                    new AcceptableValueRange<float>(0f, 100f)));
            _boundaryOuterIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "OuterRingIntensityMultiplier",
                DefaultBoundaryOuterIntensity,
                UiDescription(
                    "Brightness of the outer ring relative to the shared HDR intensity.",
                    "Boundary - Rings",
                    "Outer Brightness",
                    90,
                    80,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryThickness = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryThickness",
                DefaultBoundaryOuterThickness,
                UiDescription(
                    "Base visual thickness of the outer ring.",
                    "Boundary - Rings",
                    "Outer Thickness",
                    90,
                    90,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryThreatReactivity = Config.Bind(
                "8. Wyrd Boundary",
                "ThreatReactivity",
                BoundaryThreatReactivity.Subtle,
                UiDescription(
                    "Subtle gently brightens and thickens every ring as Wyrd Threat rises without changing radius.",
                    "Boundary - Motion",
                    "Threat Response",
                    100,
                    10));
            _boundaryMinimumIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "MinimumThreatIntensityMultiplier",
                DefaultBoundaryMinimumIntensity,
                UiDescription(
                    "Boundary intensity multiplier at zero threat when Subtle reactivity is selected.",
                    "Boundary - Motion",
                    "Minimum Threat Brightness",
                    100,
                    20,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryMaximumIntensity = Config.Bind(
                "8. Wyrd Boundary",
                "MaximumThreatIntensityMultiplier",
                DefaultBoundaryMaximumIntensity,
                UiDescription(
                    "Boundary intensity multiplier at maximum threat when Subtle reactivity is selected.",
                    "Boundary - Motion",
                    "Maximum Threat Brightness",
                    100,
                    30,
                    new AcceptableValueRange<float>(0f, 3f)));
            _boundaryMaximumThickness = Config.Bind(
                "8. Wyrd Boundary",
                "MaximumThreatThicknessMultiplier",
                DefaultBoundaryMaximumThickness,
                UiDescription(
                    "Boundary thickness multiplier at maximum threat when Subtle reactivity is selected.",
                    "Boundary - Motion",
                    "Maximum Threat Thickness",
                    100,
                    40,
                    new AcceptableValueRange<float>(1f, 3f)));
            _boundaryPulseEnabled = Config.Bind(
                "8. Wyrd Boundary",
                "EnableBoundaryPulse",
                true,
                UiDescription(
                    "Let each ring smoothly and independently ebb and swell within the configured limit.",
                    "Boundary - Motion",
                    "Enable Organic Pulse",
                    100,
                    50));
            _boundaryPulseAmount = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryPulseAmount",
                DefaultBoundaryPulseAmount,
                UiDescription(
                    "Maximum random brightness variation around each ring's base intensity. 1.0 permits a range from fully dimmed to roughly double brightness.",
                    "Boundary - Motion",
                    "Pulse Amount",
                    100,
                    60,
                    new AcceptableValueRange<float>(0f, 1f)));
            _boundaryPulseMinimumSeconds = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryPulseMinimumSeconds",
                DefaultBoundaryPulseMinimumSeconds,
                UiDescription(
                    "Shortest time used for a smooth pulse transition.",
                    "Boundary - Motion",
                    "Minimum Pulse Duration",
                    100,
                    70,
                    new AcceptableValueRange<float>(0.5f, 30f)));
            _boundaryPulseMaximumSeconds = Config.Bind(
                "8. Wyrd Boundary",
                "BoundaryPulseMaximumSeconds",
                DefaultBoundaryPulseMaximumSeconds,
                UiDescription(
                    "Longest time used for a smooth pulse transition.",
                    "Boundary - Motion",
                    "Maximum Pulse Duration",
                    100,
                    80,
                    new AcceptableValueRange<float>(0.5f, 30f)));

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
                    "Minimal shows committed hunts and outcomes. Atmospheric adds night and upward-stage messages. Detailed also adds downward stages, protection changes, and major surges.",
                    "Notifications", "Notification Detail", 110, 20));
            _gftDetailedExactThreat = Config.Bind(
                "9. Grail Floating Text",
                "DetailedShowExactThreat",
                false,
                UiDescription(
                    "Append the rounded Wyrd Threat value to Detailed atmospheric notifications.",
                    "Notifications", "Show Exact Threat in Detailed Text", 110, 30));
            _gftCooldownSeconds = Config.Bind(
                "9. Grail Floating Text",
                "NotificationCooldownSeconds",
                DefaultGftCooldownSeconds,
                UiDescription(
                    "Minimum active-real-time spacing within each atmospheric notification lane. Paused time does not advance it.",
                    "Notifications", "Notification Cooldown", 110, 40,
                    new AcceptableValueRange<float>(1f, 60f)));
            _diagnosticGftCooldownSeconds = Config.Bind(
                "10. Diagnostics",
                "GftSystemCooldownSeconds",
                DefaultDiagnosticGftCooldownSeconds,
                UiDescription(
                    "Minimum active-real-time spacing between concise diagnostics-only GFT System summaries.",
                    "Diagnostics", "Diagnostic Text Cooldown", 120, 20,
                    new AcceptableValueRange<float>(1f, 60f)));

            _diagnostics = Config.Bind(
                "10. Diagnostics",
                "Diagnostics",
                false,
                UiDescription(
                    "Log accepted and rejected threat inputs, pacing, and presentation details. When GFT is available, also show concise low-priority System summaries of meaningful behind-the-scenes state changes.",
                    "Diagnostics", "Enable Diagnostics", 120, 10));

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
            CapturePreservedValue<bool>(profile, "2. World Timescale", "EnableDynamicTimescale");
            CapturePreservedValue<float>(profile, "2. World Timescale", "DayTimescale");
            CapturePreservedValue<float>(profile, "2. World Timescale", "NightTimescale");
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
            CapturePreservedValue<string>(profile, "7. Threat Meter", "ThreatMeterColor");
            CapturePreservedValue<bool>(profile, "7. Threat Meter", "ShowExactThreatValue");
            CapturePreservedValue<float>(profile, "7. Threat Meter", "MeterOffsetX");
            CapturePreservedValue<float>(profile, "7. Threat Meter", "MeterOffsetY");
            CapturePreservedValue<bool>(profile, "8. Wyrd Boundary", "EnableBoundaryCustomization");
            CapturePreservedValue<BoundaryRenderMode>(profile, "8. Wyrd Boundary", "BoundaryRenderMode");
            CapturePreservedValue<string>(profile, "8. Wyrd Boundary", "BoundaryColor");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryHdrIntensity");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "NearRingRadius");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "NearRingIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "NearRingThickness");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MiddleRingRadius");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MiddleRingIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MiddleRingThickness");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryVisualRadius");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "OuterRingIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryThickness");
            CapturePreservedValue<BoundaryThreatReactivity>(profile, "8. Wyrd Boundary", "ThreatReactivity");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MinimumThreatIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MaximumThreatIntensityMultiplier");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "MaximumThreatThicknessMultiplier");
            CapturePreservedValue<bool>(profile, "8. Wyrd Boundary", "EnableBoundaryPulse");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryPulseAmount");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryPulseMinimumSeconds");
            CapturePreservedValue<float>(profile, "8. Wyrd Boundary", "BoundaryPulseMaximumSeconds");
            CapturePreservedValue<bool>(profile, "9. Grail Floating Text", "EnableNotifications");
            CapturePreservedValue<GftNotificationPreset>(profile, "9. Grail Floating Text", "NotificationPreset");
            CapturePreservedValue<bool>(profile, "9. Grail Floating Text", "DetailedShowExactThreat");
            CapturePreservedValue<float>(profile, "9. Grail Floating Text", "NotificationCooldownSeconds");
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
            RestorePreservedValue(_enableDynamicTimescale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_dayTimescale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_nightTimescale, ref restored, ref clamped, ref invalid);
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
            RestorePreservedValue(_threatMeterColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_showExactThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_meterOffsetX, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_meterOffsetY, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryHdrIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryVisualRadius, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryThickness, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryThreatReactivity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryMinimumIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryMaximumIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_boundaryMaximumThickness, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftPreset, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftDetailedExactThreat, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gftCooldownSeconds, ref restored, ref clamped, ref invalid);
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
    }
}
