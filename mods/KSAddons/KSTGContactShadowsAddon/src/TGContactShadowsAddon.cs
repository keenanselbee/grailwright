using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.Utility;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[assembly: AssemblyTitle("Contact Shadows Addon")]
[assembly: AssemblyDescription("Stable interior contact shadows with exact runtime restoration")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Contact Shadows Addon")]
[assembly: AssemblyVersion("0.1.4.0")]
[assembly: AssemblyFileVersion("0.1.4.0")]
[assembly: AssemblyInformationalVersion("0.1.4")]

namespace TGContactShadowsAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        ParentPluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.tg-contact-shadows-addon";
        public const string PluginName = "Contact Shadows Addon";
        public const string PluginVersion = "0.1.4";
        public const string ParentPluginGuid =
            "com.wessberg.tgcontactshadows";

        private const int ConfigSchemaVersion = 2;
        private const int ConfigRecoveryBaselineSchema = 1;
        private const float SelectionIntervalSeconds = 0.25f;
        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        internal static Plugin Instance { get; private set; }

        private readonly Dictionary<ConfigDefinition, object> _pendingPreservedSettings =
            new Dictionary<ConfigDefinition, object>();
        private readonly List<Light> _candidateLights = new List<Light>();
        private readonly List<ScoredLight> _scoredLights = new List<ScoredLight>();
        private readonly List<int> _selectionIdsToRestore = new List<int>();
        private readonly Dictionary<int, SelectedLight> _selectedLights =
            new Dictionary<int, SelectedLight>();
        private readonly Dictionary<int, CameraContactShadowState> _cameraStates =
            new Dictionary<int, CameraContactShadowState>();

        private Harmony _harmony;
        private FieldInfo _parentRuntimeEnabledField;
        private FieldInfo _parentNextScanTimeField;
        private PropertyInfo _parentCurrentConfigProperty;
        private MethodInfo _parentApplyVolumeMethod;
        private MethodInfo _parentRevertMethod;
        private MethodInfo _parentDestroyVolumeMethod;
        private MethodInfo _parentSetLightMethod;
        private Type _parentConfigDataType;
        private Type _hdLightDataType;
        private object _parentManager;
        private bool _parentRuntimeEnabled = true;
        private bool _parentRuntimeEnabledKnown;
        private bool _allowParentOriginalRevert;
        private bool _runtimeActive;
        private bool _volumeDirty = true;
        private bool _cameraForceDirty = true;
        private float _nextCameraRefreshTime;
        private float _nextSelectionTime;
        private float _nextCandidateRefreshTime;
        private string _sceneKey = String.Empty;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _interiorsOnly;
        private ConfigEntry<int> _maximumContactShadowLights;
        private ConfigEntry<float> _maximumLightDistanceMeters;
        private ConfigEntry<float> _contactShadowMaxDistance;
        private ConfigEntry<int> _sampleCount;
        private ConfigEntry<float> _length;
        private ConfigEntry<float> _opacity;
        private ConfigEntry<float> _minimumLightHoldSeconds;
        private ConfigEntry<float> _switchAdvantagePercent;
        private ConfigEntry<float> _candidateRefreshSeconds;
        private ConfigEntry<bool> _showToggleNotifications;
        private ConfigEntry<bool> _diagnostics;

        private void Awake()
        {
            Instance = this;

            try
            {
                InitializeConfig();
                InitializeParentIntegration();
                PatchParent();
                ObserveParentRuntimeState(false);
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Up to four stable contact-shadow lights are limited to interiors by default.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowLoadTimeError(
                        PluginGuid,
                        PluginName,
                        exception);
                enabled = false;
            }
        }

        private void Update()
        {
            RunController(false);
        }

        internal bool BeforeParentApply()
        {
            if (_enabled == null || !_enabled.Value)
            {
                Deactivate("addon disabled");
                return true;
            }

            ObserveParentRuntimeState(true);
            _volumeDirty = true;
            RunController(true);
            return false;
        }

        internal bool BeforeParentRevert(string reason)
        {
            if (_allowParentOriginalRevert)
            {
                return true;
            }

            if (_enabled == null || !_enabled.Value)
            {
                return true;
            }

            Deactivate(
                String.IsNullOrEmpty(reason)
                    ? "parent disabled"
                    : reason);
            return false;
        }

        internal void BeforeParentSceneCooldown(string reason)
        {
            Deactivate(
                String.IsNullOrEmpty(reason)
                    ? "scene transition"
                    : reason);
            _candidateLights.Clear();
            _sceneKey = String.Empty;
        }

        internal void AfterParentUpdate(object manager)
        {
            _parentManager = manager;
            ObserveParentRuntimeState(true);
        }

        private void InitializeConfig()
        {
            ResetConfigIfSchemaChanged();

            Config.Bind(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Enables stable contact-shadow management while the parent mod is enabled.",
                    "General", "Enabled", 0, 0));
            _interiorsOnly = Config.Bind(
                "General",
                "InteriorsOnly",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Enables contact shadows only in interiors and restores all touched state outdoors.",
                    "General", "Interiors Only", 0, 10));
            _maximumLightDistanceMeters = Config.Bind(
                "Light Selection",
                "MaximumLightDistanceMeters",
                15f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum camera distance for selected point or spot lights.",
                    "Light Selection", "Maximum Light Distance", 10, 0,
                    new AcceptableValueRange<float>(5f, 50f)));
            _maximumContactShadowLights = Config.Bind(
                "Light Selection",
                "MaximumContactShadowLights",
                4,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum number of stable point or spot lights that can use contact shadows at once.",
                    "Light Selection", "Maximum Contact Shadow Lights", 10, 10,
                    new AcceptableValueRange<int>(1, 8)));
            _minimumLightHoldSeconds = Config.Bind(
                "Light Selection",
                "MinimumLightHoldSeconds",
                1f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Minimum time a valid selected light remains active before a stronger candidate may replace it.",
                    "Light Selection", "Minimum Light Hold", 10, 20,
                    new AcceptableValueRange<float>(0.25f, 5f)));
            _switchAdvantagePercent = Config.Bind(
                "Light Selection",
                "SwitchAdvantagePercent",
                25f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How much better a challenger must score before replacing the weakest selected light.",
                    "Light Selection", "Switch Advantage", 10, 30,
                    new AcceptableValueRange<float>(0f, 100f)));
            _candidateRefreshSeconds = Config.Bind(
                "Light Selection",
                "CandidateRefreshSeconds",
                5f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How often the active scene light cache is refreshed. Selection from the cache remains responsive between refreshes.",
                    "Light Selection", "Candidate Refresh Interval", 10, 40,
                    new AcceptableValueRange<float>(1f, 30f)));
            _contactShadowMaxDistance = Config.Bind(
                "Visuals",
                "ContactShadowMaxDistance",
                20f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Camera distance at which contact shadows finish fading out.",
                    "Visuals", "Contact Shadow Max Distance", 20, 0,
                    new AcceptableValueRange<float>(5f, 50f)));
            _sampleCount = Config.Bind(
                "Visuals",
                "SampleCount",
                16,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Screen-space ray samples. Sixteen favors visual stability; eight is a lighter manual alternative.",
                    "Visuals", "Sample Count", 20, 10,
                    new AcceptableValueList<int>(4, 8, 16, 32)));
            _length = Config.Bind(
                "Visuals",
                "Length",
                0.075f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Contact-shadow ray length. The conservative default reduces long screen-space artifacts.",
                    "Visuals", "Length", 20, 20,
                    new AcceptableValueRange<float>(0f, 0.25f)));
            _opacity = Config.Bind(
                "Visuals",
                "Opacity",
                0.6f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Contact-shadow opacity. The softer default makes residual screen-space artifacts less distracting.",
                    "Visuals", "Opacity", 20, 30,
                    new AcceptableValueRange<float>(0f, 1f)));
            _showToggleNotifications = Config.Bind(
                "Notifications",
                "ShowToggleNotifications",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Shows parent toggle confirmations through Grail Floating Text when it is installed.",
                    "Notifications", "Show Toggle Notifications", 30, 0));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Logs context changes, candidate counts, selected light paths, and exact restoration activity.",
                    "Diagnostics", "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 0));

            RestorePreservedSettings();
            SubscribeConfigEvents();
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

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (String.IsNullOrWhiteSpace(configPath)
                || !File.Exists(configPath))
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
                + DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture)
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
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowConfigReset(
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
                        "Could not restore the previous Contact Shadows Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Contact Shadows Addon config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedSettings(
            string configPath,
            int storedSchemaVersion)
        {
            _pendingPreservedSettings.Clear();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            CaptureCustomizedValue(profile, "General", "Enabled", false);
            CaptureCustomizedValue(profile, "General", "InteriorsOnly", false);
            CaptureCustomizedValue(profile, "Light Selection", "MaximumContactShadowLights", 0);
            CaptureCustomizedValue(profile, "Light Selection", "MaximumLightDistanceMeters", 0f);
            CaptureCustomizedValue(profile, "Light Selection", "MinimumLightHoldSeconds", 0f);
            CaptureCustomizedValue(profile, "Light Selection", "SwitchAdvantagePercent", 0f);
            CaptureCustomizedValue(profile, "Light Selection", "CandidateRefreshSeconds", 0f);
            CaptureCustomizedValue(profile, "Visuals", "ContactShadowMaxDistance", 0f);
            CaptureCustomizedValue(profile, "Visuals", "SampleCount", 0);
            CaptureCustomizedValue(profile, "Visuals", "Length", 0f);
            CaptureCustomizedValue(profile, "Visuals", "Opacity", 0f);
            CaptureCustomizedValue(profile, "Diagnostics", "Diagnostics", false);
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
                _pendingPreservedSettings[new ConfigDefinition(section, key)] =
                    value;
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
            RestorePreservedEntry(_interiorsOnly, ref restored, ref clamped);
            RestorePreservedEntry(_maximumContactShadowLights, ref restored, ref clamped);
            RestorePreservedEntry(_maximumLightDistanceMeters, ref restored, ref clamped);
            RestorePreservedEntry(_minimumLightHoldSeconds, ref restored, ref clamped);
            RestorePreservedEntry(_switchAdvantagePercent, ref restored, ref clamped);
            RestorePreservedEntry(_candidateRefreshSeconds, ref restored, ref clamped);
            RestorePreservedEntry(_contactShadowMaxDistance, ref restored, ref clamped);
            RestorePreservedEntry(_sampleCount, ref restored, ref clamped);
            RestorePreservedEntry(_length, ref restored, ref clamped);
            RestorePreservedEntry(_opacity, ref restored, ref clamped);
            RestorePreservedEntry(_diagnostics, ref restored, ref clamped);
            _pendingPreservedSettings.Clear();

            Logger.LogInfo(
                "Restored "
                + restored.ToString(CultureInfo.InvariantCulture)
                + " customized setting(s) after schema reset"
                + (clamped > 0
                    ? "; clamped "
                        + clamped.ToString(CultureInfo.InvariantCulture)
                        + " to current ranges"
                    : String.Empty)
                + ".");
        }

        private void RestorePreservedEntry<T>(
            ConfigEntry<T> entry,
            ref int restored,
            ref int clamped)
        {
            object value;
            if (entry == null
                || !_pendingPreservedSettings.TryGetValue(
                    entry.Definition,
                    out value)
                || !(value is T))
            {
                return;
            }

            bool wasClamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    entry,
                    (T)value,
                    out wasClamped))
            {
                restored++;
                if (wasClamped)
                {
                    clamped++;
                }
            }
        }

        private void SubscribeConfigEvents()
        {
            _enabled.SettingChanged += OnControlSettingChanged;
            _interiorsOnly.SettingChanged += OnControlSettingChanged;
            _maximumContactShadowLights.SettingChanged += OnSelectionSettingChanged;
            _maximumLightDistanceMeters.SettingChanged += OnSelectionSettingChanged;
            _minimumLightHoldSeconds.SettingChanged += OnSelectionSettingChanged;
            _switchAdvantagePercent.SettingChanged += OnSelectionSettingChanged;
            _candidateRefreshSeconds.SettingChanged += OnSelectionSettingChanged;
            _contactShadowMaxDistance.SettingChanged += OnVisualSettingChanged;
            _sampleCount.SettingChanged += OnVisualSettingChanged;
            _length.SettingChanged += OnVisualSettingChanged;
            _opacity.SettingChanged += OnVisualSettingChanged;
        }

        private void UnsubscribeConfigEvents()
        {
            if (_enabled != null)
            {
                _enabled.SettingChanged -= OnControlSettingChanged;
                _interiorsOnly.SettingChanged -= OnControlSettingChanged;
                _maximumContactShadowLights.SettingChanged -= OnSelectionSettingChanged;
                _maximumLightDistanceMeters.SettingChanged -= OnSelectionSettingChanged;
                _minimumLightHoldSeconds.SettingChanged -= OnSelectionSettingChanged;
                _switchAdvantagePercent.SettingChanged -= OnSelectionSettingChanged;
                _candidateRefreshSeconds.SettingChanged -= OnSelectionSettingChanged;
                _contactShadowMaxDistance.SettingChanged -= OnVisualSettingChanged;
                _sampleCount.SettingChanged -= OnVisualSettingChanged;
                _length.SettingChanged -= OnVisualSettingChanged;
                _opacity.SettingChanged -= OnVisualSettingChanged;
            }
        }

        private void OnControlSettingChanged(object sender, EventArgs args)
        {
            if (ReferenceEquals(sender, _enabled) && _enabled.Value)
            {
                PrepareParentForTakeover();
            }
            _nextSelectionTime = 0f;
            RunController(true);
            NudgeParentScan();
        }

        private void OnSelectionSettingChanged(object sender, EventArgs args)
        {
            _nextSelectionTime = 0f;
            _nextCandidateRefreshTime = 0f;
        }

        private void OnVisualSettingChanged(object sender, EventArgs args)
        {
            _volumeDirty = true;
            NudgeParentScan();
        }

        private void InitializeParentIntegration()
        {
            Type managerType = AccessTools.TypeByName(
                "TGContactShadows.ContactShadowManager");
            Type localConfigType = AccessTools.TypeByName(
                "TGContactShadows.LocalConfig");
            _parentConfigDataType = AccessTools.TypeByName(
                "TGContactShadows.LocalConfigData");
            Type hdrpSupportType = AccessTools.TypeByName(
                "TGContactShadows.HdrpSupport");
            if (managerType == null
                || localConfigType == null
                || _parentConfigDataType == null
                || hdrpSupportType == null)
            {
                throw new TypeLoadException(
                    "Could not resolve the Contact Shadows 1.0.0-mono runtime types.");
            }

            _parentRuntimeEnabledField = AccessTools.Field(
                managerType,
                "RuntimeEnabled");
            _parentNextScanTimeField = AccessTools.Field(
                managerType,
                "_nextScanTime");
            _parentCurrentConfigProperty = AccessTools.Property(
                localConfigType,
                "Current");
            _parentApplyVolumeMethod = AccessTools.Method(
                hdrpSupportType,
                "TryApplyVolumeSettings",
                new[] { _parentConfigDataType });
            _parentDestroyVolumeMethod = AccessTools.Method(
                hdrpSupportType,
                "DestroyVolume");
            _parentSetLightMethod = AccessTools.Method(
                hdrpSupportType,
                "TrySetPerLightContactShadow",
                new[] { typeof(Light), typeof(bool) });
            _parentRevertMethod = AccessTools.Method(
                managerType,
                "RevertAll",
                new[] { typeof(string) });

            if (_parentRuntimeEnabledField == null
                || _parentCurrentConfigProperty == null
                || _parentApplyVolumeMethod == null
                || _parentDestroyVolumeMethod == null
                || _parentSetLightMethod == null
                || _parentRevertMethod == null)
            {
                throw new MissingMemberException(
                    "Could not resolve the Contact Shadows 1.0.0-mono control methods.");
            }

            ResolveHdrpStateMembers();
            _parentManager = UnityEngine.Object.FindFirstObjectByType(managerType);
        }

        private void PatchParent()
        {
            Type managerType = AccessTools.TypeByName(
                "TGContactShadows.ContactShadowManager");
            MethodInfo applyMethod = AccessTools.Method(
                managerType,
                "ApplyAll",
                new[] { typeof(string) });
            MethodInfo revertMethod = AccessTools.Method(
                managerType,
                "RevertAll",
                new[] { typeof(string) });
            MethodInfo cooldownMethod = AccessTools.Method(
                managerType,
                "BeginSceneCooldown",
                new[] { typeof(string) });
            MethodInfo updateMethod = AccessTools.Method(managerType, "Update");
            if (applyMethod == null
                || revertMethod == null
                || cooldownMethod == null
                || updateMethod == null)
            {
                throw new MissingMethodException(
                    "Could not find the Contact Shadows 1.0.0-mono manager methods.");
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                applyMethod,
                prefix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.BeforeParentApply)));
            _harmony.Patch(
                revertMethod,
                prefix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.BeforeParentRevert)));
            _harmony.Patch(
                cooldownMethod,
                prefix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.BeforeParentSceneCooldown)));
            _harmony.Patch(
                updateMethod,
                postfix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.AfterParentUpdate)));
        }

        private void ResolveHdrpStateMembers()
        {
            _hdLightDataType = AccessTools.TypeByName(
                "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
            if (_hdLightDataType == null)
            {
                throw new TypeLoadException(
                    "Could not resolve the game's HDRP light type.");
            }
        }

        private void RunController(bool force)
        {
            if (_enabled == null)
            {
                return;
            }

            ObserveParentRuntimeState(false);
            string sceneKey;
            bool isInterior;
            bool playable = TryGetEnvironment(out isInterior, out sceneKey);
            bool shouldRun = _enabled.Value
                && _parentRuntimeEnabled
                && playable
                && (!_interiorsOnly.Value || isInterior);
            if (!shouldRun)
            {
                Deactivate(
                    !_enabled.Value
                        ? "addon disabled"
                        : !_parentRuntimeEnabled
                            ? "parent disabled"
                            : playable
                                ? "exterior"
                                : "non-gameplay context");
                return;
            }

            if (!String.Equals(_sceneKey, sceneKey, StringComparison.Ordinal))
            {
                Deactivate("context changed");
                _sceneKey = sceneKey;
                _candidateLights.Clear();
                _nextCandidateRefreshTime = 0f;
                _nextSelectionTime = 0f;
                _volumeDirty = true;
                _cameraForceDirty = true;
                _nextCameraRefreshTime = 0f;
                LogDiagnostic(
                    "Entered "
                    + (isInterior ? "Interior" : "Exterior")
                    + " context "
                    + sceneKey
                    + ".");
            }

            if (!_runtimeActive)
            {
                _runtimeActive = true;
                _volumeDirty = true;
                _nextCandidateRefreshTime = 0f;
                _nextSelectionTime = 0f;
                _cameraForceDirty = true;
                _nextCameraRefreshTime = 0f;
            }

            EnsureVolumeAndCameras();
            if (!force && Time.unscaledTime < _nextSelectionTime)
            {
                return;
            }

            _nextSelectionTime = Time.unscaledTime + SelectionIntervalSeconds;
            if (Time.unscaledTime >= _nextCandidateRefreshTime)
            {
                RefreshCandidateCache();
            }

            SelectStableLight();
        }

        private void PrepareParentForTakeover()
        {
            if (_parentRevertMethod == null || !_parentRuntimeEnabled)
            {
                return;
            }

            try
            {
                _allowParentOriginalRevert = true;
                _parentRevertMethod.Invoke(
                    null,
                    new object[] { "addon took control" });
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not clear the parent light selection before taking control: "
                    + UnwrapException(exception).Message);
            }
            finally
            {
                _allowParentOriginalRevert = false;
            }
        }

        private bool TryGetEnvironment(
            out bool isInterior,
            out string sceneKey)
        {
            isInterior = false;
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
            SceneLifetimeEvents lifetime = SceneLifetimeEvents.Get;
            if (sceneService == null
                || sceneService.ActiveSceneRef == null
                || String.IsNullOrEmpty(sceneService.ActiveSceneRef.Name)
                || lifetime == null
                || !lifetime.EverythingInitialized)
            {
                return false;
            }

            isInterior = !sceneService.IsOpenWorld || lifetime.InInterior;
            sceneKey = sceneService.ActiveSceneRef.Name
                + "|"
                + (isInterior ? "Interior" : "Exterior");
            return true;
        }

        private void EnsureVolumeAndCameras()
        {
            bool capturedCamera = false;
            if (Time.unscaledTime >= _nextCameraRefreshTime)
            {
                capturedCamera = CaptureUnseenCameraStates();
                _nextCameraRefreshTime = Time.unscaledTime + 1f;
            }
            if (_cameraStates.Count > 0
                && (_cameraForceDirty || capturedCamera))
            {
                try
                {
                    foreach (CameraContactShadowState state in _cameraStates.Values)
                    {
                        state.Force();
                    }
                    _cameraForceDirty = false;
                }
                catch (Exception exception)
                {
                    LogDiagnostic(
                        "Could not force contact shadows for active cameras: "
                        + UnwrapException(exception).Message);
                }
            }

            if (!_volumeDirty)
            {
                return;
            }

            object config = _parentCurrentConfigProperty.GetValue(null, null);
            if (config == null)
            {
                return;
            }

            ParentVisualSnapshot snapshot = ParentVisualSnapshot.Capture(
                config,
                _parentConfigDataType);
            try
            {
                snapshot.ApplyAddonValues(
                    _length.Value,
                    _opacity.Value,
                    _contactShadowMaxDistance.Value,
                    _sampleCount.Value);
                _parentApplyVolumeMethod.Invoke(null, new[] { config });
                _volumeDirty = false;
                LogDiagnostic(
                    "Applied contact-shadow visuals: length="
                    + _length.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", opacity="
                    + _opacity.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", maxDistance="
                    + _contactShadowMaxDistance.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", samples="
                    + _sampleCount.Value.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not apply the stable contact-shadow volume: "
                    + UnwrapException(exception).Message);
            }
            finally
            {
                snapshot.Restore();
            }
        }

        private bool CaptureUnseenCameraStates()
        {
            bool captured = false;
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                int id = camera.GetInstanceID();
                if (_cameraStates.ContainsKey(id))
                {
                    continue;
                }

                HDAdditionalCameraData hdData =
                    camera.GetComponent<HDAdditionalCameraData>();
                if (hdData == null)
                {
                    continue;
                }

                bool overrideValue;
                object overrideContainer =
                    hdData.renderingPathCustomFrameSettingsOverrideMask;
                bool hasOverride = TryReadCompositeBit(
                    overrideContainer,
                    (int)FrameSettingsField.ContactShadows,
                    out overrideValue);
                if (hasOverride)
                {
                    _cameraStates[id] = new CameraContactShadowState(
                        camera,
                        hdData,
                        overrideValue,
                        hdData.renderingPathCustomFrameSettings.IsEnabled(
                            FrameSettingsField.ContactShadows));
                    captured = true;
                }
            }
            return captured;
        }

        private void RefreshCandidateCache()
        {
            _candidateLights.Clear();
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (IsBaseEligible(lights[i]))
                {
                    _candidateLights.Add(lights[i]);
                }
            }

            _nextCandidateRefreshTime = Time.unscaledTime
                + _candidateRefreshSeconds.Value;
            LogDiagnostic(
                "Cached "
                + _candidateLights.Count.ToString(CultureInfo.InvariantCulture)
                + " active point/spot light candidate(s).");
        }

        private void SelectStableLight()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsSortMode.None);
                if (cameras.Length > 0)
                {
                    camera = cameras[0];
                }
            }

            if (camera == null)
            {
                RestoreSelectedLights("no active camera");
                return;
            }

            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
            _scoredLights.Clear();
            for (int i = 0; i < _candidateLights.Count; i++)
            {
                Light candidate = _candidateLights[i];
                float score;
                if (!TryScoreLight(candidate, camera, frustum, out score))
                {
                    continue;
                }

                _scoredLights.Add(new ScoredLight(candidate, score));
            }
            _scoredLights.Sort(CompareScoredLights);

            _selectionIdsToRestore.Clear();
            foreach (KeyValuePair<int, SelectedLight> pair in _selectedLights)
            {
                float score;
                if (!TryScoreLight(
                        pair.Value.State.Light,
                        camera,
                        frustum,
                        out score))
                {
                    _selectionIdsToRestore.Add(pair.Key);
                }
                else
                {
                    pair.Value.Score = score;
                }
            }
            RestoreQueuedSelections("no longer visible or eligible");

            int maximumLights = _maximumContactShadowLights.Value;
            while (_selectedLights.Count > maximumLights)
            {
                SelectedLight weakest = FindWeakestSelectedLight(false);
                if (weakest == null)
                {
                    break;
                }
                RestoreSelectedLight(
                    weakest.State.Light.GetInstanceID(),
                    "configured light budget reduced");
            }

            for (int i = 0;
                i < _scoredLights.Count
                    && _selectedLights.Count < maximumLights;
                i++)
            {
                ScoredLight candidate = _scoredLights[i];
                if (!_selectedLights.ContainsKey(candidate.Id))
                {
                    ActivateSelectedLight(candidate.Light, candidate.Score);
                }
            }

            for (int i = 0; i < _scoredLights.Count; i++)
            {
                ScoredLight challenger = _scoredLights[i];
                if (_selectedLights.ContainsKey(challenger.Id))
                {
                    continue;
                }

                SelectedLight weakest = FindWeakestSelectedLight(true);
                if (weakest == null)
                {
                    break;
                }

                float requiredScore = weakest.Score
                    * (1f + (_switchAdvantagePercent.Value / 100f));
                if (challenger.Score < requiredScore)
                {
                    break;
                }

                RestoreSelectedLight(
                    weakest.State.Light.GetInstanceID(),
                    "stable handoff");
                ActivateSelectedLight(challenger.Light, challenger.Score);
            }
        }

        private static int CompareScoredLights(
            ScoredLight left,
            ScoredLight right)
        {
            int scoreComparison = right.Score.CompareTo(left.Score);
            return scoreComparison != 0
                ? scoreComparison
                : left.Id.CompareTo(right.Id);
        }

        private SelectedLight FindWeakestSelectedLight(bool requireExpiredHold)
        {
            SelectedLight weakest = null;
            foreach (SelectedLight selected in _selectedLights.Values)
            {
                if (requireExpiredHold
                    && Time.unscaledTime
                        < selected.SelectedSince
                            + _minimumLightHoldSeconds.Value)
                {
                    continue;
                }

                if (weakest == null
                    || selected.Score < weakest.Score
                    || (Mathf.Approximately(selected.Score, weakest.Score)
                        && selected.State.Light.GetInstanceID()
                            > weakest.State.Light.GetInstanceID()))
                {
                    weakest = selected;
                }
            }
            return weakest;
        }

        private bool TryScoreLight(
            Light light,
            Camera camera,
            Plane[] frustum,
            out float score)
        {
            score = 0f;
            if (!IsBaseEligible(light) || camera == null)
            {
                return false;
            }

            Vector3 cameraPosition = camera.transform.position;
            Vector3 lightPosition = light.transform.position;
            float distance = Vector3.Distance(cameraPosition, lightPosition);
            if (distance > _maximumLightDistanceMeters.Value)
            {
                return false;
            }

            float diameter = Mathf.Max(1f, light.range * 2f);
            Bounds bounds = new Bounds(
                lightPosition,
                new Vector3(diameter, diameter, diameter));
            if (!GeometryUtility.TestPlanesAABB(frustum, bounds))
            {
                return false;
            }

            float influence = light.range / Mathf.Max(1f, distance);
            float intensity = Mathf.Clamp(light.intensity, 0f, 8f);
            score = influence * influence * 100f
                + intensity * 0.25f
                + Mathf.Clamp(light.range, 0f, 30f) * 0.05f;
            return true;
        }

        private static bool IsBaseEligible(Light light)
        {
            if (light == null
                || !light.enabled
                || !light.gameObject.activeInHierarchy
                || light.intensity <= 0f
                || light.range <= 0f
                || (light.type != LightType.Point
                    && light.type != LightType.Spot))
            {
                return false;
            }

            UnityEngine.SceneManagement.Scene scene = light.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private void ActivateSelectedLight(Light light, float score)
        {
            int lightId = light.GetInstanceID();
            if (_selectedLights.ContainsKey(lightId))
            {
                return;
            }

            LightContactShadowState state = CaptureLightState(light);
            if (state == null)
            {
                return;
            }

            try
            {
                bool enabledResult = (bool)_parentSetLightMethod.Invoke(
                    null,
                    new object[] { light, true });
                if (!enabledResult)
                {
                    state.Restore();
                    return;
                }

                _selectedLights[lightId] = new SelectedLight(
                    state,
                    Time.unscaledTime,
                    score);
                LogDiagnostic(
                    "Selected contact-shadow light "
                    + GetTransformPath(light.transform)
                    + " (score="
                    + score.ToString("0.0", CultureInfo.InvariantCulture)
                    + ").");
            }
            catch (Exception exception)
            {
                state.Restore();
                Logger.LogWarning(
                    "Could not enable the selected contact-shadow light: "
                    + UnwrapException(exception).Message);
            }
        }

        private LightContactShadowState CaptureLightState(Light light)
        {
            Component hdData = light.GetComponent(_hdLightDataType);
            if (hdData == null)
            {
                return null;
            }

            MemberInfo containerMember = FindMember(
                _hdLightDataType,
                "m_UseContactShadow",
                "m_UseContactShadows");
            MemberInfo directMember = null;
            MemberInfo overrideMember = null;
            MemberInfo useOverrideMember = null;
            object container = null;
            bool hasDirect = false;
            bool directValue = false;
            bool hasOverride = false;
            bool overrideValue = false;
            bool hasUseOverride = false;
            bool useOverrideValue = false;

            if (containerMember != null
                && GetMemberType(containerMember) == typeof(bool))
            {
                directMember = containerMember;
                hasDirect = TryReadBoolMember(
                    directMember,
                    hdData,
                    out directValue);
            }
            else if (containerMember != null)
            {
                container = GetMemberValue(containerMember, hdData);
                if (container != null)
                {
                    overrideMember = FindBoolMember(
                        container.GetType(),
                        "override",
                        "m_Override",
                        "value",
                        "m_Value");
                    useOverrideMember = FindBoolMember(
                        container.GetType(),
                        "useOverride",
                        "m_UseOverride",
                        "overrideState");
                    hasOverride = TryReadBoolMember(
                        overrideMember,
                        container,
                        out overrideValue);
                    hasUseOverride = TryReadBoolMember(
                        useOverrideMember,
                        container,
                        out useOverrideValue);
                }
            }

            if (directMember == null)
            {
                directMember = FindBoolMember(
                    _hdLightDataType,
                    "m_ObsoleteContactShadows",
                    "m_ObsoleteContactShadow",
                    "useContactShadow",
                    "useContactShadows");
                hasDirect = TryReadBoolMember(
                    directMember,
                    hdData,
                    out directValue);
            }

            if (!hasDirect && !hasOverride && !hasUseOverride)
            {
                Logger.LogWarning(
                    "Could not capture the original HDRP contact-shadow state for "
                    + light.name
                    + ".");
                return null;
            }

            return new LightContactShadowState(
                light,
                hdData,
                containerMember,
                container,
                overrideMember,
                hasOverride,
                overrideValue,
                useOverrideMember,
                hasUseOverride,
                useOverrideValue,
                directMember,
                hasDirect,
                directValue);
        }

        private void RestoreQueuedSelections(string reason)
        {
            for (int i = 0; i < _selectionIdsToRestore.Count; i++)
            {
                RestoreSelectedLight(_selectionIdsToRestore[i], reason);
            }
            _selectionIdsToRestore.Clear();
        }

        private void RestoreSelectedLight(int lightId, string reason)
        {
            SelectedLight selected;
            if (!_selectedLights.TryGetValue(lightId, out selected))
            {
                return;
            }

            Light light = selected.State.Light;
            selected.State.Restore();
            _selectedLights.Remove(lightId);
            if (light != null)
            {
                LogDiagnostic(
                    "Restored contact-shadow light "
                    + GetTransformPath(light.transform)
                    + " ("
                    + reason
                    + ").");
            }
        }

        private void RestoreSelectedLights(string reason)
        {
            _selectionIdsToRestore.Clear();
            foreach (int lightId in _selectedLights.Keys)
            {
                _selectionIdsToRestore.Add(lightId);
            }
            RestoreQueuedSelections(reason);
        }

        private void Deactivate(string reason)
        {
            if (!_runtimeActive
                && _selectedLights.Count == 0
                && _cameraStates.Count == 0)
            {
                return;
            }

            RestoreSelectedLights(reason);
            RestoreCameraStates();
            try
            {
                if (_parentDestroyVolumeMethod != null)
                {
                    _parentDestroyVolumeMethod.Invoke(null, null);
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not destroy the parent contact-shadow volume: "
                    + UnwrapException(exception).Message);
            }

            _runtimeActive = false;
            _volumeDirty = true;
            _cameraForceDirty = true;
            _nextCameraRefreshTime = 0f;
            LogDiagnostic("Contact shadows inactive: " + reason + ".");
        }

        private void RestoreCameraStates()
        {
            foreach (CameraContactShadowState state in _cameraStates.Values)
            {
                state.Restore();
            }

            if (_cameraStates.Count > 0)
            {
                LogDiagnostic(
                    "Restored "
                    + _cameraStates.Count.ToString(CultureInfo.InvariantCulture)
                    + " camera frame-setting state(s).");
            }
            _cameraStates.Clear();
        }

        private void ObserveParentRuntimeState(bool notifyChange)
        {
            if (_parentRuntimeEnabledField == null)
            {
                return;
            }

            bool runtimeEnabled;
            try
            {
                runtimeEnabled = (bool)_parentRuntimeEnabledField.GetValue(null);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Could not read the parent contact-shadow toggle: "
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
            if (!runtimeEnabled)
            {
                Deactivate("parent hotkey disabled");
            }
            else
            {
                _nextSelectionTime = 0f;
                _nextCandidateRefreshTime = 0f;
            }

            if (notifyChange
                && _showToggleNotifications != null
                && _showToggleNotifications.Value)
            {
                string message = "Contact Shadows: "
                    + (runtimeEnabled ? "Enabled" : "Disabled");
                if (runtimeEnabled
                    && _enabled != null
                    && _enabled.Value
                    && _interiorsOnly != null
                    && _interiorsOnly.Value)
                {
                    message += " (interiors only)";
                }

                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowSystemNotification(
                        PluginGuid,
                        runtimeEnabled
                            ? "contact-shadows-enabled"
                            : "contact-shadows-disabled",
                        message,
                        "Normal",
                        "contact-shadows-toggle");
            }
        }

        private void NudgeParentScan()
        {
            if (_parentManager == null || _parentNextScanTimeField == null)
            {
                return;
            }

            try
            {
                _parentNextScanTimeField.SetValue(_parentManager, 0f);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Could not request a parent refresh: "
                    + exception.Message);
            }
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        private static MemberInfo FindMember(Type type, params string[] names)
        {
            Type current = type;
            while (current != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    PropertyInfo property = current.GetProperty(
                        names[i],
                        InstanceFlags | BindingFlags.DeclaredOnly);
                    if (property != null)
                    {
                        return property;
                    }
                }
                for (int i = 0; i < names.Length; i++)
                {
                    FieldInfo field = current.GetField(
                        names[i],
                        InstanceFlags | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        return field;
                    }
                }
                current = current.BaseType;
            }
            return null;
        }

        private static MemberInfo FindBoolMember(
            Type type,
            params string[] names)
        {
            Type current = type;
            while (current != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    PropertyInfo property = current.GetProperty(
                        names[i],
                        InstanceFlags | BindingFlags.DeclaredOnly);
                    if (property != null
                        && property.PropertyType == typeof(bool)
                        && property.CanWrite)
                    {
                        return property;
                    }
                }
                for (int i = 0; i < names.Length; i++)
                {
                    FieldInfo field = current.GetField(
                        names[i],
                        InstanceFlags | BindingFlags.DeclaredOnly);
                    if (field != null
                        && field.FieldType == typeof(bool)
                        && !field.IsInitOnly)
                    {
                        return field;
                    }
                }
                current = current.BaseType;
            }
            return null;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            PropertyInfo property = member as PropertyInfo;
            if (property != null)
            {
                return property.PropertyType;
            }
            FieldInfo field = member as FieldInfo;
            return field == null ? null : field.FieldType;
        }

        private static object GetMemberValue(MemberInfo member, object target)
        {
            PropertyInfo property = member as PropertyInfo;
            if (property != null)
            {
                return property.GetValue(target, null);
            }
            FieldInfo field = member as FieldInfo;
            return field == null ? null : field.GetValue(target);
        }

        private static bool SetMemberValue(
            MemberInfo member,
            object target,
            object value)
        {
            try
            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    if (!property.CanWrite)
                    {
                        return false;
                    }
                    property.SetValue(target, value, null);
                    return true;
                }
                FieldInfo field = member as FieldInfo;
                if (field != null && !field.IsInitOnly)
                {
                    field.SetValue(target, value);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool TryReadBoolMember(
            MemberInfo member,
            object target,
            out bool value)
        {
            value = false;
            if (member == null || target == null)
            {
                return false;
            }
            try
            {
                object raw = GetMemberValue(member, target);
                if (raw is bool)
                {
                    value = (bool)raw;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool TryReadCompositeBit(
            object container,
            int bitIndex,
            out bool value)
        {
            value = false;
            if (container == null || bitIndex < 0)
            {
                return false;
            }

            Type type = container.GetType();
            FieldInfo[] fields = type.GetFields(InstanceFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                string name = fields[i].FieldType.Name;
                if (name.IndexOf("BitArray", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                object nested = fields[i].GetValue(container);
                if (TryReadBit(nested, bitIndex, out value))
                {
                    return true;
                }
            }
            return TryReadBitFromUlongs(container, type, bitIndex, out value);
        }

        private static bool TryReadBit(
            object bitArray,
            int bitIndex,
            out bool value)
        {
            value = false;
            if (bitArray == null)
            {
                return false;
            }
            try
            {
                PropertyInfo item = bitArray.GetType().GetProperty("Item");
                if (item != null && item.CanRead)
                {
                    object raw = item.GetValue(
                        bitArray,
                        new object[] { (uint)bitIndex });
                    if (raw is bool)
                    {
                        value = (bool)raw;
                        return true;
                    }
                }
            }
            catch
            {
            }
            return TryReadBitFromUlongs(
                bitArray,
                bitArray.GetType(),
                bitIndex,
                out value);
        }

        private static bool TryReadBitFromUlongs(
            object target,
            Type type,
            int bitIndex,
            out bool value)
        {
            value = false;
            List<FieldInfo> fields = new List<FieldInfo>();
            FieldInfo[] allFields = type.GetFields(InstanceFlags);
            for (int i = 0; i < allFields.Length; i++)
            {
                if (allFields[i].FieldType == typeof(ulong))
                {
                    fields.Add(allFields[i]);
                }
            }
            if (fields.Count == 0)
            {
                return false;
            }
            fields.Sort(
                delegate(FieldInfo left, FieldInfo right)
                {
                    return left.MetadataToken.CompareTo(right.MetadataToken);
                });
            int fieldIndex = bitIndex / 64;
            int offset = bitIndex % 64;
            if (fieldIndex >= fields.Count)
            {
                return false;
            }
            ulong raw = (ulong)fields[fieldIndex].GetValue(target);
            value = (raw & (1UL << offset)) != 0;
            return true;
        }

        private static bool TrySetCompositeBit(
            object container,
            int bitIndex,
            bool value)
        {
            if (container == null || bitIndex < 0)
            {
                return false;
            }
            Type type = container.GetType();
            FieldInfo[] fields = type.GetFields(InstanceFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                string name = fields[i].FieldType.Name;
                if (name.IndexOf("BitArray", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                object nested = fields[i].GetValue(container);
                if (TrySetBit(nested, bitIndex, value))
                {
                    fields[i].SetValue(container, nested);
                    return true;
                }
            }
            return TrySetBitInUlongs(container, type, bitIndex, value);
        }

        private static bool TrySetBit(
            object bitArray,
            int bitIndex,
            bool value)
        {
            if (bitArray == null)
            {
                return false;
            }
            try
            {
                PropertyInfo item = bitArray.GetType().GetProperty("Item");
                if (item != null && item.CanWrite)
                {
                    item.SetValue(
                        bitArray,
                        value,
                        new object[] { (uint)bitIndex });
                    return true;
                }
            }
            catch
            {
            }
            return TrySetBitInUlongs(
                bitArray,
                bitArray.GetType(),
                bitIndex,
                value);
        }

        private static bool TrySetBitInUlongs(
            object target,
            Type type,
            int bitIndex,
            bool value)
        {
            List<FieldInfo> fields = new List<FieldInfo>();
            FieldInfo[] allFields = type.GetFields(InstanceFlags);
            for (int i = 0; i < allFields.Length; i++)
            {
                if (allFields[i].FieldType == typeof(ulong))
                {
                    fields.Add(allFields[i]);
                }
            }
            if (fields.Count == 0)
            {
                return false;
            }
            fields.Sort(
                delegate(FieldInfo left, FieldInfo right)
                {
                    return left.MetadataToken.CompareTo(right.MetadataToken);
                });
            int fieldIndex = bitIndex / 64;
            int offset = bitIndex % 64;
            if (fieldIndex >= fields.Count)
            {
                return false;
            }
            ulong raw = (ulong)fields[fieldIndex].GetValue(target);
            raw = value
                ? raw | (1UL << offset)
                : raw & ~(1UL << offset);
            fields[fieldIndex].SetValue(target, raw);
            return true;
        }

        private static string GetTransformPath(Transform transform)
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return String.Join("/", names.ToArray());
        }

        private static Exception UnwrapException(Exception exception)
        {
            TargetInvocationException invocation =
                exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null
                ? invocation.InnerException
                : exception;
        }

        private void OnDestroy()
        {
            UnsubscribeConfigEvents();
            Deactivate("addon unloaded");
            NudgeParentScan();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private struct ScoredLight
        {
            internal readonly Light Light;
            internal readonly float Score;
            internal readonly int Id;

            internal ScoredLight(Light light, float score)
            {
                Light = light;
                Score = score;
                Id = light.GetInstanceID();
            }
        }

        private sealed class SelectedLight
        {
            internal readonly LightContactShadowState State;
            internal readonly float SelectedSince;
            internal float Score;

            internal SelectedLight(
                LightContactShadowState state,
                float selectedSince,
                float score)
            {
                State = state;
                SelectedSince = selectedSince;
                Score = score;
            }
        }

        private sealed class ParentVisualSnapshot
        {
            private readonly object _target;
            private readonly FieldInfo _length;
            private readonly FieldInfo _opacity;
            private readonly FieldInfo _maxDistance;
            private readonly FieldInfo _fadeDistance;
            private readonly FieldInfo _sampleCount;
            private readonly float _originalLength;
            private readonly float _originalOpacity;
            private readonly float _originalMaxDistance;
            private readonly float _originalFadeDistance;
            private readonly int _originalSampleCount;

            private ParentVisualSnapshot(object target, Type configType)
            {
                _target = target;
                _length = AccessTools.Field(configType, "Length");
                _opacity = AccessTools.Field(configType, "Opacity");
                _maxDistance = AccessTools.Field(configType, "MaxDistance");
                _fadeDistance = AccessTools.Field(configType, "FadeDistance");
                _sampleCount = AccessTools.Field(configType, "SampleCount");
                if (_length == null
                    || _opacity == null
                    || _maxDistance == null
                    || _fadeDistance == null
                    || _sampleCount == null)
                {
                    throw new MissingFieldException(
                        "Contact Shadows visual config fields changed.");
                }

                _originalLength = (float)_length.GetValue(target);
                _originalOpacity = (float)_opacity.GetValue(target);
                _originalMaxDistance = (float)_maxDistance.GetValue(target);
                _originalFadeDistance = (float)_fadeDistance.GetValue(target);
                _originalSampleCount = (int)_sampleCount.GetValue(target);
            }

            internal static ParentVisualSnapshot Capture(
                object target,
                Type configType)
            {
                return new ParentVisualSnapshot(target, configType);
            }

            internal void ApplyAddonValues(
                float length,
                float opacity,
                float maxDistance,
                int sampleCount)
            {
                _length.SetValue(_target, length);
                _opacity.SetValue(_target, opacity);
                _maxDistance.SetValue(_target, maxDistance);
                _fadeDistance.SetValue(
                    _target,
                    Mathf.Min(5f, maxDistance));
                _sampleCount.SetValue(_target, sampleCount);
            }

            internal void Restore()
            {
                _length.SetValue(_target, _originalLength);
                _opacity.SetValue(_target, _originalOpacity);
                _maxDistance.SetValue(_target, _originalMaxDistance);
                _fadeDistance.SetValue(_target, _originalFadeDistance);
                _sampleCount.SetValue(_target, _originalSampleCount);
            }
        }

        private sealed class CameraContactShadowState
        {
            private readonly Camera _camera;
            private readonly HDAdditionalCameraData _hdData;
            private readonly bool _overrideValue;
            private readonly bool _frameValue;

            internal CameraContactShadowState(
                Camera camera,
                HDAdditionalCameraData hdData,
                bool overrideValue,
                bool frameValue)
            {
                _camera = camera;
                _hdData = hdData;
                _overrideValue = overrideValue;
                _frameValue = frameValue;
            }

            internal void Force()
            {
                if (_camera == null || _hdData == null)
                {
                    return;
                }

                object overrideMask =
                    _hdData.renderingPathCustomFrameSettingsOverrideMask;
                if (TrySetCompositeBit(
                        overrideMask,
                        (int)FrameSettingsField.ContactShadows,
                        true))
                {
                    _hdData.renderingPathCustomFrameSettingsOverrideMask =
                        (FrameSettingsOverrideMask)overrideMask;
                }
                _hdData.renderingPathCustomFrameSettings.SetEnabled(
                    FrameSettingsField.ContactShadows,
                    true);
            }

            internal void Restore()
            {
                if (_camera == null || _hdData == null)
                {
                    return;
                }

                object overrideMask =
                    _hdData.renderingPathCustomFrameSettingsOverrideMask;
                if (TrySetCompositeBit(
                        overrideMask,
                        (int)FrameSettingsField.ContactShadows,
                        _overrideValue))
                {
                    _hdData.renderingPathCustomFrameSettingsOverrideMask =
                        (FrameSettingsOverrideMask)overrideMask;
                }
                _hdData.renderingPathCustomFrameSettings.SetEnabled(
                    FrameSettingsField.ContactShadows,
                    _frameValue);
            }
        }

        private sealed class LightContactShadowState
        {
            internal readonly Light Light;
            private readonly Component _hdData;
            private readonly MemberInfo _containerMember;
            private readonly object _container;
            private readonly MemberInfo _overrideMember;
            private readonly bool _hasOverride;
            private readonly bool _overrideValue;
            private readonly MemberInfo _useOverrideMember;
            private readonly bool _hasUseOverride;
            private readonly bool _useOverrideValue;
            private readonly MemberInfo _directMember;
            private readonly bool _hasDirect;
            private readonly bool _directValue;

            internal LightContactShadowState(
                Light light,
                Component hdData,
                MemberInfo containerMember,
                object container,
                MemberInfo overrideMember,
                bool hasOverride,
                bool overrideValue,
                MemberInfo useOverrideMember,
                bool hasUseOverride,
                bool useOverrideValue,
                MemberInfo directMember,
                bool hasDirect,
                bool directValue)
            {
                Light = light;
                _hdData = hdData;
                _containerMember = containerMember;
                _container = container;
                _overrideMember = overrideMember;
                _hasOverride = hasOverride;
                _overrideValue = overrideValue;
                _useOverrideMember = useOverrideMember;
                _hasUseOverride = hasUseOverride;
                _useOverrideValue = useOverrideValue;
                _directMember = directMember;
                _hasDirect = hasDirect;
                _directValue = directValue;
            }

            internal void Restore()
            {
                if (Light == null || _hdData == null)
                {
                    return;
                }
                try
                {
                    if (_container != null)
                    {
                        if (_hasOverride)
                        {
                            SetMemberValue(
                                _overrideMember,
                                _container,
                                _overrideValue);
                        }
                        if (_hasUseOverride)
                        {
                            SetMemberValue(
                                _useOverrideMember,
                                _container,
                                _useOverrideValue);
                        }
                        if (_containerMember != null
                            && GetMemberType(_containerMember).IsValueType)
                        {
                            SetMemberValue(
                                _containerMember,
                                _hdData,
                                _container);
                        }
                    }
                    if (_hasDirect)
                    {
                        SetMemberValue(
                            _directMember,
                            _hdData,
                            _directValue);
                    }
                }
                catch
                {
                }
            }
        }
    }

    internal static class Patches
    {
        internal static bool BeforeParentApply()
        {
            Plugin plugin = Plugin.Instance;
            return plugin == null || plugin.BeforeParentApply();
        }

        internal static bool BeforeParentRevert(string reason)
        {
            Plugin plugin = Plugin.Instance;
            return plugin == null || plugin.BeforeParentRevert(reason);
        }

        internal static void BeforeParentSceneCooldown(string reason)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.BeforeParentSceneCooldown(reason);
            }
        }

        internal static void AfterParentUpdate(object __instance)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.AfterParentUpdate(__instance);
            }
        }
    }
}
