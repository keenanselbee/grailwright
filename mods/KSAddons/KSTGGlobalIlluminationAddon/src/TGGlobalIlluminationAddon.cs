using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Graphics.Cutscenes;
using Awaken.TG.Graphics.Transitions;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using Awaken.TG.Main.Utility;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.Utility;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("KS Global Illumination Addon")]
[assembly: AssemblyDescription("Contextual indoor and outdoor performance profiles for Global Illumination")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("KS Global Illumination Addon")]
[assembly: AssemblyVersion("0.1.8.0")]
[assembly: AssemblyFileVersion("0.1.8.0")]
[assembly: AssemblyInformationalVersion("0.1.8")]

namespace KSTGGlobalIlluminationAddon
{
    public enum AddonMode
    {
        Adaptive,
        Full,
        Balanced,
        Performance
    }

    public enum QualityTier
    {
        Performance,
        Balanced,
        Full
    }

    internal enum EnvironmentKind
    {
        Unknown,
        Interior,
        Exterior
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(ParentPluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.tg-global-illumination-addon";
        public const string PluginName =
            "Global Illumination Addon";
        public const string PluginVersion = "0.1.8";
        public const string ParentPluginGuid =
            "com.wessberg.tgglobalillumination";

        private const int ConfigSchemaVersion = 1;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        internal static Plugin Instance { get; private set; }

        private readonly Dictionary<ConfigDefinition, object> _pendingPreservedSettings =
            new Dictionary<ConfigDefinition, object>();
        private readonly Dictionary<string, QualityTier> _sceneTiers =
            new Dictionary<string, QualityTier>(StringComparer.Ordinal);

        private Harmony _harmony;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<AddonMode> _mode;
        private ConfigEntry<float> _targetFps;
        private ConfigEntry<QualityTier> _interiorPreset;
        private ConfigEntry<QualityTier> _exteriorPreset;
        private ConfigEntry<bool> _startAtPerformance;
        private ConfigEntry<bool> _rememberSceneTier;
        private ConfigEntry<float> _sampleWindowSeconds;
        private ConfigEntry<float> _downgradeMarginFps;
        private ConfigEntry<float> _downgradeHoldSeconds;
        private ConfigEntry<float> _upgradeMarginFps;
        private ConfigEntry<float> _upgradeHoldSeconds;
        private ConfigEntry<float> _changeCooldownSeconds;
        private ConfigEntry<float> _sceneWarmupSeconds;
        private ConfigEntry<bool> _showToggleNotifications;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _showGrailFloatingTextDiagnostics;

        private PropertyInfo _parentCurrentConfigProperty;
        private RuntimeConfigAccessor _parentConfigAccessor;
        private object _parentConfigObject;
        private GiConfigSnapshot _fullProfile;
        private GiConfigSnapshot _lastAppliedProfile;
        private object _parentManager;
        private FieldInfo _parentNextApplyTimeField;
        private FieldInfo _parentRuntimeEnabledField;
        private bool _parentRuntimeEnabledKnown;
        private bool _parentRuntimeEnabled;

        private EnvironmentKind _environment = EnvironmentKind.Unknown;
        private string _sceneKey = String.Empty;
        private QualityTier _tier = QualityTier.Full;
        private float _warmupEndsAt;
        private float _cooldownEndsAt;
        private float _sampleElapsed;
        private int _sampleFrames;
        private float _smoothedFps;
        private float _lowFpsSeconds;
        private float _highFpsSeconds;
        private bool _runtimeOverrideActive;

        private void Awake()
        {
            Instance = this;

            try
            {
                InitializeConfig();
                InitializeParentReflection();
                PatchParent();
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Adaptive presets: interiors="
                    + _interiorPreset.Value
                    + ", exteriors="
                    + _exteriorPreset.Value
                    + ", start at Performance="
                    + _startAtPerformance.Value
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(
                    PluginGuid,
                    PluginName,
                    exception);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_enabled == null)
            {
                return;
            }

            if (!_enabled.Value)
            {
                if (_runtimeOverrideActive)
                {
                    RestoreParentProfile("addon disabled");
                }
                return;
            }

            EnvironmentKind environment;
            string sceneKey;
            if (!TryGetEnvironment(out environment, out sceneKey))
            {
                if (_environment != EnvironmentKind.Unknown || _runtimeOverrideActive)
                {
                    LeavePlayableContext();
                }
                return;
            }

            if (environment != _environment
                || !String.Equals(sceneKey, _sceneKey, StringComparison.Ordinal))
            {
                EnterContext(environment, sceneKey);
            }

            if (_mode.Value != AddonMode.Adaptive)
            {
                SetTier(ModeToTier(_mode.Value), "fixed mode");
                return;
            }

            AdvanceAdaptiveController();
        }

        private void OnDestroy()
        {
            UnsubscribeConfigEvents();
            RestoreParentProfile("addon unloaded");
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void InitializeConfig()
        {
            ResetConfigIfSchemaChanged();

            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Internal config schema marker. Do not edit this value.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Enables contextual Global Illumination quality profiles.");
            _mode = Config.Bind(
                "1. Core",
                "Mode",
                AddonMode.Adaptive,
                "Adaptive adjusts quality up or down toward the contextual preset. Fixed modes hold one tier everywhere.");
            _targetFps = Config.Bind(
                "1. Core",
                "TargetFps",
                60f,
                new ConfigDescription(
                    "Target frame rate used by Adaptive mode.",
                    new AcceptableValueRange<float>(30f, 240f)));
            _showToggleNotifications = Config.Bind(
                "1. Core",
                "ShowToggleNotifications",
                true,
                "Shows parent toggle confirmations through Grail Floating Text when it is installed.");
            _diagnostics = Config.Bind(
                "1. Core",
                "Diagnostics",
                false,
                "Logs context, smoothed FPS, parent config reloads, and quality changes.");
            _showGrailFloatingTextDiagnostics = Config.Bind(
                "1. Core",
                "ShowGrailFloatingTextDiagnostics",
                true,
                "When Diagnostics is enabled and Grail Floating Text is installed, show adaptive-tier summaries. Detailed BepInEx logging remains active when this is disabled.");

            _interiorPreset = Config.Bind(
                "2. Adaptive Presets",
                "InteriorPreset",
                QualityTier.Full,
                "Maximum quality tier used by Adaptive mode in interiors.");
            _exteriorPreset = Config.Bind(
                "2. Adaptive Presets",
                "ExteriorPreset",
                QualityTier.Balanced,
                "Maximum quality tier used by Adaptive mode outdoors.");
            _startAtPerformance = Config.Bind(
                "2. Adaptive Presets",
                "StartAtPerformance",
                true,
                "Starts new scenes at Performance, then raises quality when sustained FPS meets the target. Remembered scenes resume their last successful tier.");

            _rememberSceneTier = Config.Bind(
                "2. Adaptive",
                "RememberSceneTier",
                true,
                "Remembers the last successful tier for each scene during the current game session.");
            _sampleWindowSeconds = Config.Bind(
                "2. Adaptive",
                "SampleWindowSeconds",
                5f,
                new ConfigDescription(
                    "Approximate rolling gameplay window used to smooth FPS decisions.",
                    new AcceptableValueRange<float>(2f, 15f)));
            _downgradeMarginFps = Config.Bind(
                "2. Adaptive",
                "DowngradeMarginFps",
                6f,
                new ConfigDescription(
                    "Downgrades below TargetFps minus this margin.",
                    new AcceptableValueRange<float>(1f, 30f)));
            _downgradeHoldSeconds = Config.Bind(
                "2. Adaptive",
                "DowngradeHoldSeconds",
                4f,
                new ConfigDescription(
                    "How long smoothed FPS must remain low before one downgrade.",
                    new AcceptableValueRange<float>(1f, 30f)));
            _upgradeMarginFps = Config.Bind(
                "2. Adaptive",
                "UpgradeMarginFps",
                1f,
                new ConfigDescription(
                    "Upgrades only when FPS is within this amount of TargetFps.",
                    new AcceptableValueRange<float>(0f, 15f)));
            _upgradeHoldSeconds = Config.Bind(
                "2. Adaptive",
                "UpgradeHoldSeconds",
                30f,
                new ConfigDescription(
                    "How long smoothed FPS must remain recovered before one upgrade.",
                    new AcceptableValueRange<float>(5f, 120f)));
            _changeCooldownSeconds = Config.Bind(
                "2. Adaptive",
                "ChangeCooldownSeconds",
                15f,
                new ConfigDescription(
                    "Minimum delay between adaptive quality changes.",
                    new AcceptableValueRange<float>(5f, 60f)));
            _sceneWarmupSeconds = Config.Bind(
                "2. Adaptive",
                "SceneWarmupSeconds",
                5f,
                new ConfigDescription(
                    "Ignores FPS while a newly entered scene settles.",
                    new AcceptableValueRange<float>(2f, 30f)));

            RestorePreservedSettings();
            Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                Config,
                Logger,
                PluginName,
                ConfigSchemaVersion,
                ConfigRecoveryBaselineSchema,
                ConfigRecoveryKeepCurrentDefaultRules,
                ConfigRecoveryPermanentExclusions);
            Config.Save();
            SubscribeConfigEvents();
        }

        private void SubscribeConfigEvents()
        {
            _enabled.SettingChanged += OnControllerSettingChanged;
            _mode.SettingChanged += OnControllerSettingChanged;
            _targetFps.SettingChanged += OnControllerSettingChanged;
            _interiorPreset.SettingChanged += OnControllerSettingChanged;
            _exteriorPreset.SettingChanged += OnControllerSettingChanged;
            _startAtPerformance.SettingChanged += OnControllerSettingChanged;
        }

        private void UnsubscribeConfigEvents()
        {
            if (_enabled == null)
            {
                return;
            }

            _enabled.SettingChanged -= OnControllerSettingChanged;
            _mode.SettingChanged -= OnControllerSettingChanged;
            _targetFps.SettingChanged -= OnControllerSettingChanged;
            _interiorPreset.SettingChanged -= OnControllerSettingChanged;
            _exteriorPreset.SettingChanged -= OnControllerSettingChanged;
            _startAtPerformance.SettingChanged -= OnControllerSettingChanged;
        }

        private void OnControllerSettingChanged(object sender, EventArgs eventArgs)
        {
            ResetFpsSample();
            _lowFpsSeconds = 0f;
            _highFpsSeconds = 0f;

            if (!_enabled.Value)
            {
                RestoreParentProfile("configuration changed");
                return;
            }

            if (_mode.Value != AddonMode.Adaptive)
            {
                SetTier(ModeToTier(_mode.Value), "configuration changed");
            }
            else if (_environment != EnvironmentKind.Unknown)
            {
                EnterContext(_environment, _sceneKey);
            }
            NudgeParentManager();
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (String.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                const string schemaPrefix = "ConfigSchemaVersion =";
                if (!line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                Int32.TryParse(
                    line.Substring(schemaPrefix.Length).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out storedSchemaVersion);
                break;
            }

            if (storedSchemaVersion == ConfigSchemaVersion)
            {
                return;
            }

            CapturePreservedSettings(configPath, storedSchemaVersion);
            string backupPath = configPath
                + ".pre-schema-"
                + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + ".bak";
            try
            {
                File.Copy(configPath, backupPath, false);
                File.WriteAllText(configPath, String.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid,
                    PluginName,
                    storedSchemaVersion,
                    ConfigSchemaVersion);
            }
            catch (Exception exception)
            {
                _pendingPreservedSettings.Clear();
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
                        "Could not restore the previous config after schema reset failure: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset " + PluginName + " config schema.",
                    exception);
            }
        }

        private void CapturePreservedSettings(string configPath, int storedSchemaVersion)
        {
            _pendingPreservedSettings.Clear();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery.ReadCustomizationProfile(
                    configPath,
                    storedSchemaVersion,
                    ConfigSchemaVersion,
                    ConfigRecoveryKeepCurrentDefaultRules,
                    ConfigRecoveryPermanentExclusions);

            CaptureCustomizedValue(profile, "1. Core", "Enabled", false);
            CaptureCustomizedValue(profile, "1. Core", "Mode", AddonMode.Adaptive);
            CaptureCustomizedValue(profile, "1. Core", "TargetFps", 0f);
            CaptureCustomizedValue(profile, "1. Core", "Diagnostics", false);
            CaptureCustomizedValue(profile, "1. Core", "ShowGrailFloatingTextDiagnostics", false);
            CaptureCustomizedValue(
                profile,
                "2. Adaptive Presets",
                "InteriorPreset",
                QualityTier.Full);
            CaptureCustomizedValue(
                profile,
                "2. Adaptive Presets",
                "ExteriorPreset",
                QualityTier.Balanced);
            CaptureCustomizedValue(
                profile,
                "2. Adaptive Presets",
                "StartAtPerformance",
                false);
            CaptureCustomizedValue(profile, "2. Adaptive", "RememberSceneTier", false);
            CaptureCustomizedValue(profile, "2. Adaptive", "SampleWindowSeconds", 0f);
            CaptureCustomizedValue(profile, "2. Adaptive", "DowngradeMarginFps", 0f);
            CaptureCustomizedValue(profile, "2. Adaptive", "DowngradeHoldSeconds", 0f);
            CaptureCustomizedValue(profile, "2. Adaptive", "UpgradeMarginFps", 0f);
            CaptureCustomizedValue(profile, "2. Adaptive", "UpgradeHoldSeconds", 0f);
            CaptureCustomizedValue(profile, "2. Adaptive", "ChangeCooldownSeconds", 0f);
            CaptureCustomizedValue(profile, "2. Adaptive", "SceneWarmupSeconds", 0f);
        }

        private void CaptureCustomizedValue<T>(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            string section,
            string key,
            T ignoredTypeHint)
        {
            T value;
            if (profile.TryGetCustomizedValue(section, key, out value))
            {
                _pendingPreservedSettings[new ConfigDefinition(section, key)] = value;
            }
        }

        private void RestorePreservedSettings()
        {
            if (_pendingPreservedSettings.Count == 0)
            {
                return;
            }

            int restored = 0;
            int clamped = 0;
            RestorePreservedEntry(_enabled, ref restored, ref clamped);
            RestorePreservedEntry(_mode, ref restored, ref clamped);
            RestorePreservedEntry(_targetFps, ref restored, ref clamped);
            RestorePreservedEntry(_diagnostics, ref restored, ref clamped);
            RestorePreservedEntry(_showGrailFloatingTextDiagnostics, ref restored, ref clamped);
            RestorePreservedEntry(_interiorPreset, ref restored, ref clamped);
            RestorePreservedEntry(_exteriorPreset, ref restored, ref clamped);
            RestorePreservedEntry(_startAtPerformance, ref restored, ref clamped);
            RestorePreservedEntry(_rememberSceneTier, ref restored, ref clamped);
            RestorePreservedEntry(_sampleWindowSeconds, ref restored, ref clamped);
            RestorePreservedEntry(_downgradeMarginFps, ref restored, ref clamped);
            RestorePreservedEntry(_downgradeHoldSeconds, ref restored, ref clamped);
            RestorePreservedEntry(_upgradeMarginFps, ref restored, ref clamped);
            RestorePreservedEntry(_upgradeHoldSeconds, ref restored, ref clamped);
            RestorePreservedEntry(_changeCooldownSeconds, ref restored, ref clamped);
            RestorePreservedEntry(_sceneWarmupSeconds, ref restored, ref clamped);

            Logger.LogInfo(
                "Preserved "
                + restored.ToString(CultureInfo.InvariantCulture)
                + " setting(s) across the config reset; clamped="
                + clamped.ToString(CultureInfo.InvariantCulture)
                + ".");
            _pendingPreservedSettings.Clear();
        }

        private void RestorePreservedEntry<T>(
            ConfigEntry<T> entry,
            ref int restored,
            ref int clamped)
        {
            object rawValue;
            if (entry == null
                || !_pendingPreservedSettings.TryGetValue(entry.Definition, out rawValue)
                || !(rawValue is T))
            {
                return;
            }

            bool valueClamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                (T)rawValue,
                out valueClamped))
            {
                restored++;
                if (valueClamped)
                {
                    clamped++;
                }
            }
        }

        private void InitializeParentReflection()
        {
            Type managerType = AccessTools.TypeByName(
                "TGGlobalIllumination.GlobalIlluminationManager");
            Type localConfigType = AccessTools.TypeByName(
                "TGGlobalIllumination.LocalConfig");
            if (managerType == null || localConfigType == null)
            {
                throw new TypeLoadException(
                    "Global Illumination 1.0.0 runtime types were not found.");
            }

            _parentCurrentConfigProperty = AccessTools.Property(
                localConfigType,
                "Current");
            _parentNextApplyTimeField = AccessTools.Field(
                managerType,
                "_nextApplyTime");
            _parentRuntimeEnabledField = AccessTools.Field(
                managerType,
                "_runtimeEnabled");
            if (_parentCurrentConfigProperty == null)
            {
                throw new MissingMemberException(
                    "Global Illumination LocalConfig.Current was not found.");
            }

            _parentConfigAccessor = new RuntimeConfigAccessor(
                _parentCurrentConfigProperty.PropertyType,
                new[]
                {
                    "TryEnableSsgi",
                    "IndirectDiffuseMultiplier",
                    "ReflectionMultiplier",
                    "SampleCount",
                    "BounceCount",
                    "FullResolutionSS"
                });
        }

        private void PatchParent()
        {
            Type managerType = AccessTools.TypeByName(
                "TGGlobalIllumination.GlobalIlluminationManager");
            MethodInfo applyMethod = AccessTools.Method(
                managerType,
                "Apply",
                new[] { typeof(string) });
            MethodInfo updateMethod = AccessTools.Method(managerType, "Update");
            if (applyMethod == null || updateMethod == null)
            {
                throw new MissingMethodException(
                    "Global Illumination Apply or Update method was not found.");
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                applyMethod,
                prefix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.BeforeGlobalIlluminationApply)));
            _harmony.Patch(
                updateMethod,
                postfix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.AfterGlobalIlluminationUpdate)));
            _parentManager = UnityEngine.Object.FindFirstObjectByType(managerType);
            ObserveParentRuntimeState(_parentManager, false);
        }

        internal void BeforeGlobalIlluminationApply(object manager)
        {
            _parentManager = manager;
            object current = _parentCurrentConfigProperty.GetValue(null, null);
            if (current == null)
            {
                return;
            }

            RefreshFullProfile(current);
            if (_enabled == null
                || !_enabled.Value
                || _environment == EnvironmentKind.Unknown)
            {
                RestoreParentProfileValues();
                return;
            }

            GiConfigSnapshot profile = _fullProfile.CreateProfile(_tier);
            profile.Apply(current, _parentConfigAccessor);
            _lastAppliedProfile = profile;
            _runtimeOverrideActive = true;
            LogDiagnostic("Applied " + _tier + " GI profile.");
        }

        internal void AfterGlobalIlluminationUpdate(object manager)
        {
            _parentManager = manager;
            ObserveParentRuntimeState(manager, true);
        }

        private void ObserveParentRuntimeState(object manager, bool notifyChange)
        {
            if (manager == null || _parentRuntimeEnabledField == null)
            {
                return;
            }

            bool runtimeEnabled;
            try
            {
                runtimeEnabled = (bool)_parentRuntimeEnabledField.GetValue(manager);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Could not read the parent GI toggle state: "
                    + exception.Message);
                return;
            }

            if (!_parentRuntimeEnabledKnown)
            {
                _parentRuntimeEnabled = runtimeEnabled;
                _parentRuntimeEnabledKnown = true;
                return;
            }
            if (_parentRuntimeEnabled == runtimeEnabled)
            {
                return;
            }

            _parentRuntimeEnabled = runtimeEnabled;
            if (notifyChange
                && _showToggleNotifications != null
                && _showToggleNotifications.Value)
            {
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowSystemNotification(
                        PluginGuid,
                        runtimeEnabled ? "gi-enabled" : "gi-disabled",
                        "Global Illumination: "
                            + (runtimeEnabled ? "Enabled" : "Disabled"),
                        "Normal",
                        "gi-toggle");
            }
        }

        private void RefreshFullProfile(object current)
        {
            GiConfigSnapshot currentSnapshot = GiConfigSnapshot.Capture(
                current,
                _parentConfigAccessor);
            if (!ReferenceEquals(current, _parentConfigObject))
            {
                _parentConfigObject = current;
                _fullProfile = currentSnapshot;
                _lastAppliedProfile = null;
                return;
            }

            bool differsFromExpectedRuntime = _lastAppliedProfile != null
                ? !_lastAppliedProfile.Matches(current, _parentConfigAccessor)
                : _fullProfile != null
                    && !_fullProfile.Matches(current, _parentConfigAccessor);
            if (differsFromExpectedRuntime)
            {
                _fullProfile = currentSnapshot;
                _lastAppliedProfile = null;
                LogDiagnostic(
                    "Detected a Global Illumination config reload and refreshed the Full profile.");
            }
        }

        private bool TryGetEnvironment(
            out EnvironmentKind environment,
            out string sceneKey)
        {
            environment = EnvironmentKind.Unknown;
            sceneKey = String.Empty;

            Hero hero = Hero.Current;
            if (hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || hero.IsDying
                || World.Services == null)
            {
                return false;
            }

            SceneService sceneService = World.Services.TryGet<SceneService>();
            if (sceneService == null
                || sceneService.ActiveSceneRef == null
                || String.IsNullOrEmpty(sceneService.ActiveSceneRef.Name))
            {
                return false;
            }

            SceneLifetimeEvents lifetime = SceneLifetimeEvents.Get;
            if (lifetime == null || !lifetime.EverythingInitialized)
            {
                return false;
            }

            environment = !sceneService.IsOpenWorld || lifetime.InInterior
                ? EnvironmentKind.Interior
                : EnvironmentKind.Exterior;
            sceneKey = sceneService.ActiveSceneRef.Name
                + "|"
                + environment.ToString();
            return true;
        }

        private void EnterContext(EnvironmentKind environment, string sceneKey)
        {
            RememberCurrentSceneTier();
            _environment = environment;
            _sceneKey = sceneKey;

            QualityTier maximum = ContextMaximumTier(environment);
            QualityTier initial = _startAtPerformance.Value
                ? QualityTier.Performance
                : maximum;
            QualityTier remembered;
            if (_mode.Value == AddonMode.Adaptive
                && _rememberSceneTier.Value
                && _sceneTiers.TryGetValue(sceneKey, out remembered))
            {
                initial = MinTier(remembered, maximum);
            }
            else if (_mode.Value != AddonMode.Adaptive)
            {
                initial = ModeToTier(_mode.Value);
            }

            _tier = initial;
            _warmupEndsAt = Time.unscaledTime + _sceneWarmupSeconds.Value;
            _cooldownEndsAt = _warmupEndsAt;
            ResetAdaptiveTimers();
            NudgeParentManager();
            Logger.LogInfo(
                "Context="
                + environment
                + ", scene="
                + sceneKey
                + ", starting tier="
                + initial
                + ".");
        }

        private void LeavePlayableContext()
        {
            RememberCurrentSceneTier();
            _environment = EnvironmentKind.Unknown;
            _sceneKey = String.Empty;
            ResetAdaptiveTimers();
            RestoreParentProfile("non-gameplay context");
        }

        private void RememberCurrentSceneTier()
        {
            if (_rememberSceneTier != null
                && _rememberSceneTier.Value
                && _environment != EnvironmentKind.Unknown
                && !String.IsNullOrEmpty(_sceneKey))
            {
                _sceneTiers[_sceneKey] = _tier;
            }
        }

        private void AdvanceAdaptiveController()
        {
            Hero hero = Hero.Current;
            if (!Application.isFocused
                || Time.timeScale <= 0f
                || hero == null
                || hero.IsPortaling
                || hero.JustTeleported
                || LoadingScreenUI.IsLoading
                || IsTransitioning()
                || IsCutscenePlaying()
                || Time.unscaledTime < _warmupEndsAt)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f || delta > 0.25f)
            {
                return;
            }

            _sampleElapsed += delta;
            _sampleFrames++;
            if (_sampleElapsed < 1f)
            {
                return;
            }

            float interval = _sampleElapsed;
            float measuredFps = _sampleFrames / interval;
            float window = Mathf.Max(1f, _sampleWindowSeconds.Value);
            float alpha = 1f - Mathf.Exp(-interval / window);
            _smoothedFps = _smoothedFps <= 0f
                ? measuredFps
                : Mathf.Lerp(_smoothedFps, measuredFps, alpha);
            _sampleElapsed = 0f;
            _sampleFrames = 0;

            float lowThreshold = _targetFps.Value - _downgradeMarginFps.Value;
            float highThreshold = _targetFps.Value - _upgradeMarginFps.Value;
            if (_smoothedFps < lowThreshold)
            {
                _lowFpsSeconds += interval;
                _highFpsSeconds = 0f;
            }
            else if (_smoothedFps >= highThreshold)
            {
                _highFpsSeconds += interval;
                _lowFpsSeconds = 0f;
            }
            else
            {
                _lowFpsSeconds = 0f;
                _highFpsSeconds = 0f;
            }

            LogDiagnostic(
                "Smoothed FPS="
                + _smoothedFps.ToString("0.0", CultureInfo.InvariantCulture)
                + ", tier="
                + _tier
                + ".");
            if (Time.unscaledTime < _cooldownEndsAt)
            {
                return;
            }

            if (_lowFpsSeconds >= _downgradeHoldSeconds.Value
                && _tier > QualityTier.Performance)
            {
                SetTier(_tier - 1, "sustained low FPS");
                return;
            }

            QualityTier maximum = ContextMaximumTier(_environment);
            if (_highFpsSeconds >= _upgradeHoldSeconds.Value && _tier < maximum)
            {
                SetTier(_tier + 1, "sustained recovered FPS");
            }
        }

        private static bool IsTransitioning()
        {
            if (World.Services == null)
            {
                return true;
            }

            TransitionService transition = World.Services.TryGet<TransitionService>();
            return transition != null && transition.InTransition;
        }

        private static bool IsCutscenePlaying()
        {
            Cutscene cutscene = World.Any<Cutscene>();
            return cutscene != null
                && !cutscene.HasBeenDiscarded
                && !cutscene.Stopped;
        }

        private void SetTier(QualityTier tier, string reason)
        {
            if (_mode.Value == AddonMode.Adaptive)
            {
                tier = MinTier(tier, ContextMaximumTier(_environment));
            }
            if (_tier == tier)
            {
                return;
            }

            QualityTier previous = _tier;
            _tier = tier;
            _cooldownEndsAt = Time.unscaledTime + _changeCooldownSeconds.Value;
            _lowFpsSeconds = 0f;
            _highFpsSeconds = 0f;
            RememberCurrentSceneTier();
            NudgeParentManager();
            Logger.LogInfo(
                "GI tier changed from "
                + previous
                + " to "
                + tier
                + " ("
                + reason
                + ").");
            ShowAdaptiveTierNotification(previous, tier, reason);
        }

        private void ShowAdaptiveTierNotification(
            QualityTier previous,
            QualityTier current,
            string reason)
        {
            if (_diagnostics == null
                || !_diagnostics.Value
                || _showGrailFloatingTextDiagnostics == null
                || !_showGrailFloatingTextDiagnostics.Value
                || _mode.Value != AddonMode.Adaptive
                || (!String.Equals(reason, "sustained low FPS", StringComparison.Ordinal)
                    && !String.Equals(reason, "sustained recovered FPS", StringComparison.Ordinal)))
            {
                return;
            }

            string direction = current < previous ? "lowered" : "raised";
            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowSystemNotification(
                    PluginGuid,
                    "gi-adaptive-tier",
                    "Global Illumination "
                        + direction
                        + " to "
                        + current
                        + " at "
                        + _smoothedFps.ToString("0.0", CultureInfo.InvariantCulture)
                        + " FPS.",
                    "Normal",
                    "gi-adaptive-tier");
        }

        private void ResetAdaptiveTimers()
        {
            _sampleElapsed = 0f;
            _sampleFrames = 0;
            _smoothedFps = 0f;
            _lowFpsSeconds = 0f;
            _highFpsSeconds = 0f;
        }

        private void ResetFpsSample()
        {
            _sampleElapsed = 0f;
            _sampleFrames = 0;
            _smoothedFps = 0f;
        }

        private void NudgeParentManager()
        {
            if (_parentManager == null || _parentNextApplyTimeField == null)
            {
                return;
            }

            try
            {
                _parentNextApplyTimeField.SetValue(_parentManager, 0f);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Could not request an immediate GI refresh: "
                    + exception.Message);
            }
        }

        private void RestoreParentProfile(string reason)
        {
            RestoreParentProfileValues();
            _runtimeOverrideActive = false;
            NudgeParentManager();
            LogDiagnostic("Restored the parent GI profile: " + reason + ".");
        }

        private void RestoreParentProfileValues()
        {
            if (_fullProfile != null && _parentConfigObject != null)
            {
                _fullProfile.Apply(_parentConfigObject, _parentConfigAccessor);
                _lastAppliedProfile = null;
            }
        }

        private QualityTier ContextMaximumTier(EnvironmentKind environment)
        {
            return environment == EnvironmentKind.Interior
                ? _interiorPreset.Value
                : _exteriorPreset.Value;
        }

        private static QualityTier ModeToTier(AddonMode mode)
        {
            if (mode == AddonMode.Performance)
            {
                return QualityTier.Performance;
            }
            if (mode == AddonMode.Balanced)
            {
                return QualityTier.Balanced;
            }
            return QualityTier.Full;
        }

        private static QualityTier MinTier(QualityTier left, QualityTier right)
        {
            return left < right ? left : right;
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        private sealed class RuntimeConfigAccessor
        {
            private readonly Dictionary<string, FieldInfo> _fields =
                new Dictionary<string, FieldInfo>(StringComparer.Ordinal);

            internal RuntimeConfigAccessor(Type type, IEnumerable<string> names)
            {
                foreach (string name in names)
                {
                    FieldInfo field = AccessTools.Field(type, name);
                    if (field == null)
                    {
                        throw new MissingFieldException(type.FullName, name);
                    }
                    _fields.Add(name, field);
                }
            }

            internal T Get<T>(object target, string name)
            {
                return (T)_fields[name].GetValue(target);
            }

            internal void Set<T>(object target, string name, T value)
            {
                _fields[name].SetValue(target, value);
            }
        }

        private sealed class GiConfigSnapshot
        {
            internal bool TryEnableSsgi;
            internal float IndirectDiffuseMultiplier;
            internal float ReflectionMultiplier;
            internal int SampleCount;
            internal int BounceCount;
            internal bool FullResolution;

            internal static GiConfigSnapshot Capture(
                object target,
                RuntimeConfigAccessor accessor)
            {
                return new GiConfigSnapshot
                {
                    TryEnableSsgi = accessor.Get<bool>(target, "TryEnableSsgi"),
                    IndirectDiffuseMultiplier = accessor.Get<float>(target, "IndirectDiffuseMultiplier"),
                    ReflectionMultiplier = accessor.Get<float>(target, "ReflectionMultiplier"),
                    SampleCount = accessor.Get<int>(target, "SampleCount"),
                    BounceCount = accessor.Get<int>(target, "BounceCount"),
                    FullResolution = accessor.Get<bool>(target, "FullResolutionSS")
                };
            }

            internal GiConfigSnapshot CreateProfile(QualityTier tier)
            {
                GiConfigSnapshot profile = Clone();
                if (tier == QualityTier.Balanced)
                {
                    profile.SampleCount = Math.Min(4, SampleCount);
                    profile.BounceCount = Math.Min(1, BounceCount);
                    profile.FullResolution = false;
                }
                else if (tier == QualityTier.Performance)
                {
                    profile.TryEnableSsgi = false;
                }
                return profile;
            }

            internal void Apply(object target, RuntimeConfigAccessor accessor)
            {
                accessor.Set(target, "TryEnableSsgi", TryEnableSsgi);
                accessor.Set(target, "IndirectDiffuseMultiplier", IndirectDiffuseMultiplier);
                accessor.Set(target, "ReflectionMultiplier", ReflectionMultiplier);
                accessor.Set(target, "SampleCount", SampleCount);
                accessor.Set(target, "BounceCount", BounceCount);
                accessor.Set(target, "FullResolutionSS", FullResolution);
            }

            internal bool Matches(object target, RuntimeConfigAccessor accessor)
            {
                GiConfigSnapshot other = Capture(target, accessor);
                return TryEnableSsgi == other.TryEnableSsgi
                    && Math.Abs(IndirectDiffuseMultiplier - other.IndirectDiffuseMultiplier) < 0.0001f
                    && Math.Abs(ReflectionMultiplier - other.ReflectionMultiplier) < 0.0001f
                    && SampleCount == other.SampleCount
                    && BounceCount == other.BounceCount
                    && FullResolution == other.FullResolution;
            }

            private GiConfigSnapshot Clone()
            {
                return new GiConfigSnapshot
                {
                    TryEnableSsgi = TryEnableSsgi,
                    IndirectDiffuseMultiplier = IndirectDiffuseMultiplier,
                    ReflectionMultiplier = ReflectionMultiplier,
                    SampleCount = SampleCount,
                    BounceCount = BounceCount,
                    FullResolution = FullResolution
                };
            }
        }

        private static class Patches
        {
            internal static void BeforeGlobalIlluminationApply(object __instance)
            {
                Plugin instance = Instance;
                if (instance != null)
                {
                    instance.BeforeGlobalIlluminationApply(__instance);
                }
            }

            internal static void AfterGlobalIlluminationUpdate(object __instance)
            {
                Plugin instance = Instance;
                if (instance != null)
                {
                    instance.AfterGlobalIlluminationUpdate(__instance);
                }
            }
        }
    }
}
