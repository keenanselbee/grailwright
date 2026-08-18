using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[assembly: AssemblyTitle("KS Better Volumetric Fog Addon")]
[assembly: AssemblyDescription("Interior-only volumetric quality and cached fog discovery for Better Volumetric Fog")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("KS Better Volumetric Fog Addon")]
[assembly: AssemblyVersion("0.1.3.0")]
[assembly: AssemblyFileVersion("0.1.3.0")]
[assembly: AssemblyInformationalVersion("0.1.3")]

namespace KSTGVolumetricFixAddon
{
    public enum VolumetricQuality
    {
        Low,
        Medium,
        High,
        Ultra
    }

    internal enum EnvironmentKind
    {
        Unknown,
        Interior,
        Exterior
    }

    internal sealed class ParentApplyState
    {
        public object ConfigObject;
        public object OriginalQuality;
        public bool QualityChanged;
    }

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
            "ks.tgfoa.tg-volumetric-fix-addon";
        public const string PluginName =
            "Better Volumetric Fog Addon";
        public const string PluginVersion = "0.1.3";
        public const string ParentPluginGuid =
            "com.wessberg.tgvolumetricfix";

        private const int ConfigSchemaVersion = 2;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        internal static Plugin Instance { get; private set; }

        private readonly Dictionary<ConfigDefinition, object> _pendingPreservedSettings =
            new Dictionary<ConfigDefinition, object>();

        private Harmony _harmony;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _interiorsOnly;
        private ConfigEntry<VolumetricQuality> _quality;
        private ConfigEntry<bool> _optimizeFogDiscovery;
        private ConfigEntry<bool> _showToggleNotifications;
        private ConfigEntry<bool> _diagnostics;

        private Type _parentManagerType;
        private MethodInfo _parentApplyNowMethod;
        private MethodInfo _parentRestoreAllMethod;
        private PropertyInfo _parentCurrentConfigProperty;
        private FieldInfo _parentQualityField;
        private FieldInfo _parentNextPeriodicApplyTimeField;
        private FieldInfo _parentRuntimeEnabledField;
        private object _parentManager;
        private bool _parentRuntimeEnabledKnown;
        private bool _parentRuntimeEnabled;

        private EnvironmentKind _environment = EnvironmentKind.Unknown;
        private bool _inactiveRestoreAttempted;
        private bool _unloading;
        private float _nextContextCheck;

        internal bool ShouldOptimizeFogDiscovery
        {
            get
            {
                return !_unloading
                    && _enabled != null
                    && _enabled.Value
                    && _optimizeFogDiscovery != null
                    && _optimizeFogDiscovery.Value;
            }
        }

        private void Awake()
        {
            Instance = this;

            try
            {
                InitializeConfig();
                InitializeParentReflection();
                PatchParent();
                _parentManager = UnityEngine.Object.FindFirstObjectByType(
                    _parentManagerType);
                ObserveParentRuntimeState(false);
                RefreshContext(true);
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Default behavior: Quality="
                    + _quality.Value
                    + ", interiorsOnly="
                    + _interiorsOnly.Value
                    + ", optimizedFogDiscovery="
                    + _optimizeFogDiscovery.Value
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(
                    PluginGuid,
                    PluginName,
                    exception);
                UnsubscribeConfigEvents();
                if (_harmony != null)
                {
                    _harmony.UnpatchSelf();
                    _harmony = null;
                }
                FogDiscoveryCache.Clear();
                if (ReferenceEquals(Instance, this))
                {
                    Instance = null;
                }
                enabled = false;
            }
        }

        private void Update()
        {
            if (_enabled == null || Time.unscaledTime < _nextContextCheck)
            {
                return;
            }

            _nextContextCheck = Time.unscaledTime + 0.25f;
            RefreshContext(false);
            ObserveParentRuntimeState(true);
        }

        private void OnDestroy()
        {
            _unloading = true;
            UnsubscribeConfigEvents();

            try
            {
                RestoreParentValues("addon unloaded");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not restore Better Volumetric Fog while unloading: "
                    + exception.Message);
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            FogDiscoveryCache.Clear();
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void InitializeConfig()
        {
            ResetConfigIfSchemaChanged();

            Config.Bind(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Internal config schema marker. Do not edit this value.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Enables contextual volumetric quality and optimized fog discovery.",
                    "General", "Enabled", 0, 0));
            _interiorsOnly = Config.Bind(
                "General",
                "InteriorsOnly",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Applies Better Volumetric Fog only in interiors and restores vanilla volumetrics elsewhere.",
                    "General", "Interiors Only", 0, 10));
            _quality = Config.Bind(
                "Visuals",
                "Quality",
                VolumetricQuality.Low,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Quality applied while the addon allows Better Volumetric Fog to run. Low is the recommended gameplay balance.",
                    "Visuals", "Quality", 10, 0));
            _optimizeFogDiscovery = Config.Bind(
                "Performance",
                "OptimizeFogDiscovery",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Replaces the parent's repeated all-resources fog search with a cached, event-fed snapshot.",
                    "Performance", "Optimize Fog Discovery", 20, 0));
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
                    "Logs environment changes, fog-cache changes, parent application decisions, and restoration.",
                    "Diagnostics", "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 0));

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
            _interiorsOnly.SettingChanged += OnControllerSettingChanged;
            _quality.SettingChanged += OnControllerSettingChanged;
            _optimizeFogDiscovery.SettingChanged += OnControllerSettingChanged;
        }

        private void UnsubscribeConfigEvents()
        {
            if (_enabled != null)
            {
                _enabled.SettingChanged -= OnControllerSettingChanged;
            }
            if (_interiorsOnly != null)
            {
                _interiorsOnly.SettingChanged -= OnControllerSettingChanged;
            }
            if (_quality != null)
            {
                _quality.SettingChanged -= OnControllerSettingChanged;
            }
            if (_optimizeFogDiscovery != null)
            {
                _optimizeFogDiscovery.SettingChanged -= OnControllerSettingChanged;
            }
        }

        private void OnControllerSettingChanged(object sender, EventArgs eventArgs)
        {
            _inactiveRestoreAttempted = false;
            RefreshContext(true);
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

            CaptureCustomizedValue(profile, "General", "Enabled", false);
            CaptureCustomizedValue(profile, "General", "InteriorsOnly", false);
            CaptureCustomizedValue(
                profile,
                "Visuals",
                "Quality",
                VolumetricQuality.Low);
            CaptureCustomizedValue(
                profile,
                "Notifications",
                "OptimizeFogDiscovery",
                false);
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
            RestorePreservedEntry(_interiorsOnly, ref restored, ref clamped);
            RestorePreservedEntry(_quality, ref restored, ref clamped);
            RestorePreservedEntry(_optimizeFogDiscovery, ref restored, ref clamped);
            RestorePreservedEntry(_diagnostics, ref restored, ref clamped);

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
            _parentManagerType = AccessTools.TypeByName(
                "TGVolumetricFix.VolumetricFixManager");
            Type supportType = AccessTools.TypeByName(
                "TGVolumetricFix.VolumetricFixSupport");
            Type localConfigType = AccessTools.TypeByName(
                "TGVolumetricFix.LocalConfig");
            if (_parentManagerType == null
                || supportType == null
                || localConfigType == null)
            {
                throw new TypeLoadException(
                    "Better Volumetric Fog 1.0.2-mono runtime types were not found.");
            }

            _parentApplyNowMethod = AccessTools.Method(
                _parentManagerType,
                "ApplyNow",
                new[] { typeof(string) });
            _parentRestoreAllMethod = AccessTools.Method(supportType, "RestoreAll");
            _parentCurrentConfigProperty = AccessTools.Property(localConfigType, "Current");
            _parentNextPeriodicApplyTimeField = AccessTools.Field(
                _parentManagerType,
                "_nextPeriodicApplyTime");
            _parentRuntimeEnabledField = AccessTools.Field(
                _parentManagerType,
                "_runtimeEnabled");
            if (_parentApplyNowMethod == null
                || _parentRestoreAllMethod == null
                || _parentCurrentConfigProperty == null
                || _parentNextPeriodicApplyTimeField == null
                || _parentRuntimeEnabledField == null)
            {
                throw new MissingMemberException(
                    "Better Volumetric Fog 1.0.2-mono manager or config members were not found.");
            }

            _parentQualityField = AccessTools.Field(
                _parentCurrentConfigProperty.PropertyType,
                "Quality");
            if (_parentQualityField == null
                || _parentQualityField.FieldType != typeof(string))
            {
                throw new MissingFieldException(
                    "Better Volumetric Fog LocalConfigData.Quality was not found.");
            }
        }

        private void PatchParent()
        {
            Type supportType = AccessTools.TypeByName(
                "TGVolumetricFix.VolumetricFixSupport");
            MethodInfo applyMethod = AccessTools.Method(supportType, "Apply");
            MethodInfo registerMethod = AccessTools.Method(
                typeof(VolumeManager),
                "Register",
                new[] { typeof(Volume) });
            MethodInfo reloadMethod = AccessTools.Method(
                typeof(VolumeStack),
                "Reload",
                new[] { typeof(Type[]) });
            MethodInfo addMethod = AccessTools.Method(
                typeof(VolumeProfile),
                "Add",
                new[] { typeof(Type), typeof(bool) });
            MethodInfo globalProfileMethod = AccessTools.Method(
                typeof(VolumeManager),
                "SetGlobalDefaultProfile",
                new[] { typeof(VolumeProfile) });
            MethodInfo qualityProfileMethod = AccessTools.Method(
                typeof(VolumeManager),
                "SetQualityDefaultProfile",
                new[] { typeof(VolumeProfile) });
            MethodInfo customProfilesMethod = AccessTools.Method(
                typeof(VolumeManager),
                "SetCustomDefaultProfiles",
                new[] { typeof(List<VolumeProfile>) });
            if (applyMethod == null
                || registerMethod == null
                || reloadMethod == null
                || addMethod == null
                || globalProfileMethod == null
                || qualityProfileMethod == null
                || customProfilesMethod == null)
            {
                throw new MissingMethodException(
                    "The expected Better Volumetric Fog or HDRP volume methods were not found.");
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                applyMethod,
                transpiler: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.ReplaceFogSearch)));
            _harmony.Patch(
                _parentApplyNowMethod,
                prefix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.BeforeParentApplyNow)),
                finalizer: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.FinalizeParentApplyNow)));
            _harmony.Patch(
                registerMethod,
                postfix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.AfterVolumeRegistered)));
            _harmony.Patch(
                reloadMethod,
                postfix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.AfterVolumeStackReload)));
            _harmony.Patch(
                addMethod,
                postfix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.AfterVolumeProfileAdd)));
            HarmonyMethod profilePostfix = new HarmonyMethod(
                typeof(Patches),
                nameof(Patches.AfterDefaultProfileSet));
            _harmony.Patch(globalProfileMethod, postfix: profilePostfix);
            _harmony.Patch(qualityProfileMethod, postfix: profilePostfix);
            _harmony.Patch(
                customProfilesMethod,
                postfix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.AfterCustomProfilesSet)));
        }

        internal bool BeforeParentApplyNow(
            object parentManager,
            out ParentApplyState state)
        {
            state = null;
            _parentManager = parentManager;
            if (_unloading || _enabled == null || !_enabled.Value)
            {
                return true;
            }

            EnvironmentKind environment;
            if (!TryGetEnvironment(out environment))
            {
                environment = EnvironmentKind.Unknown;
            }
            _environment = environment;

            if (_interiorsOnly.Value && environment != EnvironmentKind.Interior)
            {
                RestoreParentValuesOnce(
                    environment == EnvironmentKind.Exterior
                        ? "exterior"
                        : "non-gameplay context");
                DelayParentPeriodicApply(parentManager);
                LogDiagnostic(
                    "Skipped parent volumetric application in "
                    + environment
                    + ".");
                return false;
            }

            _inactiveRestoreAttempted = false;
            object configObject = _parentCurrentConfigProperty.GetValue(null, null);
            if (configObject == null)
            {
                throw new InvalidOperationException(
                    "Better Volumetric Fog LocalConfig.Current returned null.");
            }

            object originalQuality = _parentQualityField.GetValue(configObject);
            string desiredQuality = _quality.Value.ToString();
            state = new ParentApplyState
            {
                ConfigObject = configObject,
                OriginalQuality = originalQuality,
                QualityChanged = !String.Equals(
                    originalQuality as string,
                    desiredQuality,
                    StringComparison.Ordinal)
            };
            if (state.QualityChanged)
            {
                _parentQualityField.SetValue(configObject, desiredQuality);
            }

            LogDiagnostic(
                "Allowed parent volumetric application in "
                + environment
                + " at "
                + desiredQuality
                + ".");
            return true;
        }

        internal void RestoreTemporaryParentQuality(ParentApplyState state)
        {
            if (state == null
                || !state.QualityChanged
                || state.ConfigObject == null)
            {
                return;
            }

            _parentQualityField.SetValue(
                state.ConfigObject,
                state.OriginalQuality);
            state.QualityChanged = false;
        }

        private void RefreshContext(bool force)
        {
            EnvironmentKind environment;
            if (!TryGetEnvironment(out environment))
            {
                environment = EnvironmentKind.Unknown;
            }

            if (!force && environment == _environment)
            {
                return;
            }

            EnvironmentKind previous = _environment;
            _environment = environment;
            if (_enabled == null || !_enabled.Value)
            {
                NudgeParentManager();
                LogDiagnostic(
                    "Addon disabled; returned control to the parent configuration.");
                return;
            }

            bool shouldApply = !_interiorsOnly.Value
                || environment == EnvironmentKind.Interior;
            if (shouldApply)
            {
                _inactiveRestoreAttempted = false;
                NudgeParentManager();
            }
            else
            {
                RestoreParentValuesOnce(
                    environment == EnvironmentKind.Exterior
                        ? "entered exterior"
                        : "entered non-gameplay context");
            }

            LogDiagnostic(
                "Environment changed from "
                + previous
                + " to "
                + environment
                + "; volumetric enhancement="
                + shouldApply
                + ".");
        }

        private void ObserveParentRuntimeState(bool notifyChange)
        {
            if (_parentRuntimeEnabledField == null)
            {
                return;
            }
            if (_parentManager == null)
            {
                _parentManager = UnityEngine.Object.FindFirstObjectByType(
                    _parentManagerType);
                if (_parentManager == null)
                {
                    return;
                }
            }

            bool runtimeEnabled;
            try
            {
                runtimeEnabled = (bool)_parentRuntimeEnabledField.GetValue(
                    _parentManager);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Could not read the parent volumetric toggle state: "
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
            if (!notifyChange
                || _showToggleNotifications == null
                || !_showToggleNotifications.Value)
            {
                return;
            }

            string message = "Better Volumetric Fog: "
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
                        ? "volumetric-fog-enabled"
                        : "volumetric-fog-disabled",
                    message,
                    "Normal",
                    "volumetric-fog-toggle");
        }

        private static bool TryGetEnvironment(out EnvironmentKind environment)
        {
            environment = EnvironmentKind.Unknown;
            Hero hero = Hero.Current;
            if (LoadingScreenUI.IsLoading
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || hero.IsDying
                || hero.IsPortaling
                || hero.JustTeleported
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

            environment = !sceneService.IsOpenWorld || lifetime.InInterior
                ? EnvironmentKind.Interior
                : EnvironmentKind.Exterior;
            return true;
        }

        private void RestoreParentValuesOnce(string reason)
        {
            if (_inactiveRestoreAttempted)
            {
                return;
            }

            RestoreParentValues(reason);
            _inactiveRestoreAttempted = true;
        }

        private void RestoreParentValues(string reason)
        {
            if (_parentRestoreAllMethod == null)
            {
                return;
            }

            _parentRestoreAllMethod.Invoke(null, null);
            LogDiagnostic("Restored vanilla volumetrics: " + reason + ".");
        }

        private void DelayParentPeriodicApply(object parentManager)
        {
            if (parentManager == null || _parentNextPeriodicApplyTimeField == null)
            {
                return;
            }

            _parentNextPeriodicApplyTimeField.SetValue(
                parentManager,
                Time.unscaledTime + 10f);
        }

        private void NudgeParentManager()
        {
            if (_parentManager == null || _parentNextPeriodicApplyTimeField == null)
            {
                return;
            }

            try
            {
                _parentNextPeriodicApplyTimeField.SetValue(_parentManager, 0f);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Could not request a parent volumetric refresh: "
                    + exception.Message);
            }
        }

        internal void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        internal void RequestParentRefreshForNewFog()
        {
            if (_unloading || _enabled == null || !_enabled.Value)
            {
                return;
            }

            if (_interiorsOnly.Value && _environment != EnvironmentKind.Interior)
            {
                return;
            }

            NudgeParentManager();
            LogDiagnostic("Requested a parent refresh for a newly cached Fog.");
        }
    }

    public static class FogDiscoveryCache
    {
        private static readonly Dictionary<int, Fog> Fogs =
            new Dictionary<int, Fog>();
        private static Fog[] _snapshot = new Fog[0];
        private static bool _seeded;
        private static bool _snapshotDirty;

        public static Fog[] GetSnapshot()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null || !plugin.ShouldOptimizeFogDiscovery)
            {
                return Resources.FindObjectsOfTypeAll<Fog>();
            }

            if (!_seeded)
            {
                Fog[] discovered = Resources.FindObjectsOfTypeAll<Fog>();
                for (int i = 0; i < discovered.Length; i++)
                {
                    Add(discovered[i]);
                }
                _seeded = true;
                plugin.LogDiagnostic(
                    "Seeded fog discovery cache with "
                    + Fogs.Count.ToString(CultureInfo.InvariantCulture)
                    + " instance(s).");
            }

            PruneDestroyedFogs();
            if (_snapshotDirty)
            {
                _snapshot = new Fog[Fogs.Count];
                Fogs.Values.CopyTo(_snapshot, 0);
                _snapshotDirty = false;
            }
            return _snapshot;
        }

        internal static bool Add(Fog fog)
        {
            if (fog == null)
            {
                return false;
            }

            int instanceId = fog.GetInstanceID();
            Fog existing;
            if (Fogs.TryGetValue(instanceId, out existing)
                && ReferenceEquals(existing, fog))
            {
                return false;
            }

            Fogs[instanceId] = fog;
            _snapshotDirty = true;
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.LogDiagnostic(
                    "Cached fog instance "
                    + instanceId.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
            return true;
        }

        internal static bool Add(Volume volume)
        {
            if (volume == null)
            {
                return false;
            }

            VolumeProfile profile = volume.HasInstantiatedProfile()
                ? volume.profile
                : volume.sharedProfile;
            return Add(profile);
        }

        internal static bool Add(VolumeProfile profile)
        {
            if (profile == null || profile.components == null)
            {
                return false;
            }

            bool added = false;
            for (int i = 0; i < profile.components.Count; i++)
            {
                added |= Add(profile.components[i] as Fog);
            }
            return added;
        }

        internal static bool Add(VolumeStack stack)
        {
            if (stack == null)
            {
                return false;
            }
            return Add(stack.GetComponent<Fog>());
        }

        internal static bool AddProfiles(List<VolumeProfile> profiles)
        {
            if (profiles == null)
            {
                return false;
            }

            bool added = false;
            for (int i = 0; i < profiles.Count; i++)
            {
                added |= Add(profiles[i]);
            }
            return added;
        }

        internal static void Clear()
        {
            Fogs.Clear();
            _snapshot = new Fog[0];
            _seeded = false;
            _snapshotDirty = false;
        }

        private static void PruneDestroyedFogs()
        {
            List<int> staleIds = null;
            foreach (KeyValuePair<int, Fog> pair in Fogs)
            {
                if (pair.Value != null)
                {
                    continue;
                }

                if (staleIds == null)
                {
                    staleIds = new List<int>();
                }
                staleIds.Add(pair.Key);
            }

            if (staleIds == null)
            {
                return;
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                Fogs.Remove(staleIds[i]);
            }
            _snapshotDirty = true;
        }
    }

    internal static class Patches
    {
        public static IEnumerable<CodeInstruction> ReplaceFogSearch(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(
                typeof(FogDiscoveryCache),
                nameof(FogDiscoveryCache.GetSnapshot));
            int replacementCount = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo calledMethod = instruction.operand as MethodInfo;
                if (IsFindAllFogsCall(calledMethod))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replacementCount++;
                }
                yield return instruction;
            }

            if (replacementCount != 1)
            {
                throw new InvalidOperationException(
                    "Expected one Resources.FindObjectsOfTypeAll<Fog> call but found "
                    + replacementCount.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        public static bool BeforeParentApplyNow(
            object __instance,
            out ParentApplyState __state)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null)
            {
                __state = null;
                return true;
            }
            return plugin.BeforeParentApplyNow(__instance, out __state);
        }

        public static Exception FinalizeParentApplyNow(
            Exception __exception,
            ParentApplyState __state)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                try
                {
                    plugin.RestoreTemporaryParentQuality(__state);
                }
                catch (Exception exception)
                {
                    plugin.LogDiagnostic(
                        "Could not restore the parent's in-memory Quality value: "
                        + exception.Message);
                }
            }
            return __exception;
        }

        public static void AfterVolumeRegistered(Volume __0)
        {
            NotifyIfFogAdded(FogDiscoveryCache.Add(__0));
        }

        public static void AfterVolumeStackReload(VolumeStack __instance)
        {
            NotifyIfFogAdded(FogDiscoveryCache.Add(__instance));
        }

        public static void AfterVolumeProfileAdd(VolumeComponent __result)
        {
            NotifyIfFogAdded(FogDiscoveryCache.Add(__result as Fog));
        }

        public static void AfterDefaultProfileSet(VolumeProfile __0)
        {
            NotifyIfFogAdded(FogDiscoveryCache.Add(__0));
        }

        public static void AfterCustomProfilesSet(List<VolumeProfile> __0)
        {
            NotifyIfFogAdded(FogDiscoveryCache.AddProfiles(__0));
        }

        private static void NotifyIfFogAdded(bool added)
        {
            Plugin plugin = Plugin.Instance;
            if (added && plugin != null)
            {
                plugin.RequestParentRefreshForNewFog();
            }
        }

        private static bool IsFindAllFogsCall(MethodInfo method)
        {
            if (method == null
                || method.DeclaringType != typeof(Resources)
                || !String.Equals(
                    method.Name,
                    "FindObjectsOfTypeAll",
                    StringComparison.Ordinal)
                || !method.IsGenericMethod)
            {
                return false;
            }

            Type[] genericArguments = method.GetGenericArguments();
            return genericArguments.Length == 1
                && genericArguments[0] == typeof(Fog)
                && method.GetParameters().Length == 0;
        }
    }
}
