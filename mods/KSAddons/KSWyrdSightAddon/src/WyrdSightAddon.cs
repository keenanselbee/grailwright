using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Wyrd Sight Addon")]
[assembly: AssemblyDescription("Pulse-key companion addon for Wyrd Sight")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Wyrd Sight Addon")]
[assembly: AssemblyVersion("1.0.4.0")]
[assembly: AssemblyFileVersion("1.0.4.0")]

namespace Keenan.TGFoA.WyrdSightAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(ParentPluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class WyrdSightAddonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.wyrd-sight-addon";
        public const string PluginName = "Wyrd Sight Addon";
        public const string PluginVersion = "1.0.4";
        public const string ParentPluginGuid = "WyrdSight";

        private const int ConfigSchemaVersion = 2;
        private const float DefaultPulseDurationSeconds = 3.0f;
        private const float DefaultPulseStateCheckIntervalSeconds = 0.25f;
        private const float DefaultOffRetryDelaySeconds = 0.25f;
        private const int DefaultMaximumOffAttempts = 3;
        private const float MinimumPulseDurationSeconds = 0.1f;
        private const float MaximumPulseDurationSeconds = 60.0f;
        private const float MinimumPulseStateCheckIntervalSeconds = 0.05f;
        private const float MaximumPulseStateCheckIntervalSeconds = 5.0f;
        private const float MinimumOffRetryDelaySeconds = 0.05f;
        private const float MaximumOffRetryDelaySeconds = 5.0f;
        private const int MinimumOffAttemptCount = 1;
        private const int MaximumOffAttemptCount = 10;

        internal static WyrdSightAddonPlugin Instance { get; private set; }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _pulseDurationSeconds;
        private ConfigEntry<float> _pulseStateCheckIntervalSeconds;
        private ConfigEntry<float> _offRetryDelaySeconds;
        private ConfigEntry<int> _maximumOffAttempts;
        private ConfigEntry<bool> _diagnostics;

        private Harmony _harmony;
        private BaseUnityPlugin _parentPlugin;
        private ConfigFile _parentConfig;
        private ConfigEntry<KeyCode> _highlightKeyEntry;
        private KeyCode _highlightKey = KeyCode.None;
        private MethodInfo _toggleWyrdSightMethod;
        private MethodInfo _isToggledOnGetter;
        private bool _parentReady;
        private bool _reportedParentFailure;

        private bool _ownsPulse;
        private float _pulseEndsAt;
        private float _nextStateCheckAt;
        private float _nextOffAttemptAt;
        private int _offAttempts;

        private void Awake()
        {
            Instance = this;

            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();

                if (!TryResolveParentFromChainloader())
                {
                    Logger.LogWarning(
                        "Could not resolve the Wyrd Sight parent plugin. The addon will stay inactive until Wyrd Sight is available.");
                    Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Parent plugin unavailable; check BepInEx log.");
                    return;
                }

                MethodInfo handleInputMethod = AccessTools.Method(
                    _parentPlugin.GetType(),
                    "HandleInput",
                    Type.EmptyTypes);
                if (handleInputMethod == null)
                {
                    throw new MissingMethodException(
                        _parentPlugin.GetType().FullName,
                        "HandleInput");
                }

                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(
                    handleInputMethod,
                    prefix: new HarmonyMethod(typeof(WyrdSightInputPatch), "BeforeHandleInput"));

                Config.Save();
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Wyrd Sight highlight key pulses for "
                    + GetPulseDurationSeconds().ToString("0.###", CultureInfo.InvariantCulture)
                    + " seconds; state checks every "
                    + GetPulseStateCheckIntervalSeconds().ToString("0.###", CultureInfo.InvariantCulture)
                    + " seconds.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!_ownsPulse)
            {
                return;
            }

            if (_enabled != null && !_enabled.Value)
            {
                TryEndOwnedPulse(Time.realtimeSinceStartup, "addon disabled");
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= _nextStateCheckAt)
            {
                bool toggledOn;
                if (TryGetParentToggledOn(out toggledOn) && !toggledOn)
                {
                    ClearOwnedPulse();
                    LogDiagnostic("Cleared pulse ownership because Wyrd Sight is already off.");
                    return;
                }

                _nextStateCheckAt = now + GetPulseStateCheckIntervalSeconds();
            }

            if (now >= _pulseEndsAt && now >= _nextOffAttemptAt)
            {
                TryEndOwnedPulse(now, "pulse timer expired");
            }
        }

        private void OnDestroy()
        {
            if (_ownsPulse)
            {
                TryEndOwnedPulse(Time.realtimeSinceStartup, "addon unloading");
            }

            if (_parentConfig != null)
            {
                _parentConfig.SettingChanged -= ParentConfigSettingChanged;
                _parentConfig = null;
            }

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

        internal bool BeforeWyrdSightHandleInput(object parentInstance)
        {
            if (_enabled == null || !_enabled.Value)
            {
                return true;
            }

            if (!TryEnsureParent(parentInstance))
            {
                return true;
            }

            if (_highlightKey == KeyCode.None || !Input.GetKeyDown(_highlightKey))
            {
                return true;
            }

            StartOrExtendPulse();
            return false;
        }

        private void BindConfig()
        {
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                "Configuration layout version. Older layouts are backed up and regenerated.");
            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Master switch. When disabled, Wyrd Sight keeps its original highlight-key toggle behavior.");
            _pulseDurationSeconds = Config.Bind(
                "2. Pulse Timing",
                "PulseDurationSeconds",
                DefaultPulseDurationSeconds,
                new ConfigDescription(
                    "How long a Wyrd Sight pulse stays active before the addon turns it off and lets Wyrd Sight fade normally.",
                    new AcceptableValueRange<float>(MinimumPulseDurationSeconds, MaximumPulseDurationSeconds)));
            _pulseStateCheckIntervalSeconds = Config.Bind(
                "2. Pulse Timing",
                "PulseStateCheckIntervalSeconds",
                DefaultPulseStateCheckIntervalSeconds,
                new ConfigDescription(
                    "How often the addon checks whether an addon-owned pulse was already turned off outside the addon.",
                    new AcceptableValueRange<float>(MinimumPulseStateCheckIntervalSeconds, MaximumPulseStateCheckIntervalSeconds)));
            _offRetryDelaySeconds = Config.Bind(
                "2. Pulse Timing",
                "OffRetryDelaySeconds",
                DefaultOffRetryDelaySeconds,
                new ConfigDescription(
                    "How long to wait between off-toggle retries when Wyrd Sight still reports active or its state cannot be read.",
                    new AcceptableValueRange<float>(MinimumOffRetryDelaySeconds, MaximumOffRetryDelaySeconds)));
            _maximumOffAttempts = Config.Bind(
                "2. Pulse Timing",
                "MaximumOffAttempts",
                DefaultMaximumOffAttempts,
                new ConfigDescription(
                    "How many off-toggle attempts the addon may make before it clears pulse ownership and leaves Wyrd Sight alone.",
                    new AcceptableValueRange<int>(MinimumOffAttemptCount, MaximumOffAttemptCount)));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                "Log pulse ownership and parent-state details.");
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
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

                int.TryParse(
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
            }
            catch (Exception exception)
            {
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
                        "Could not restore the previous Wyrd Sight Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Wyrd Sight Addon config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private bool TryResolveParentFromChainloader()
        {
            PluginInfo parentInfo;
            if (!Chainloader.PluginInfos.TryGetValue(ParentPluginGuid, out parentInfo)
                || parentInfo == null
                || parentInfo.Instance == null)
            {
                return false;
            }

            return TryEnsureParent(parentInfo.Instance);
        }

        private bool TryEnsureParent(object parentInstance)
        {
            if (parentInstance == null)
            {
                return ReportParentFailure("Wyrd Sight parent instance is null.");
            }

            if (ReferenceEquals(_parentPlugin, parentInstance) && _parentReady)
            {
                return true;
            }

            BaseUnityPlugin parentPlugin = parentInstance as BaseUnityPlugin;
            if (parentPlugin == null)
            {
                return ReportParentFailure("Wyrd Sight parent instance is not a BepInEx plugin.");
            }

            if (_parentConfig != null)
            {
                _parentConfig.SettingChanged -= ParentConfigSettingChanged;
            }

            Type parentType = parentInstance.GetType();
            _toggleWyrdSightMethod = AccessTools.Method(
                parentType,
                "ToggleWyrdSight",
                Type.EmptyTypes);
            _isToggledOnGetter = AccessTools.PropertyGetter(parentType, "IsToggledOn");

            if (_toggleWyrdSightMethod == null)
            {
                return ReportParentFailure("Could not find Wyrd Sight's ToggleWyrdSight method.");
            }

            if (_isToggledOnGetter == null)
            {
                return ReportParentFailure("Could not find Wyrd Sight's IsToggledOn property.");
            }

            _parentPlugin = parentPlugin;
            _parentConfig = parentPlugin.Config;
            _parentReady = TryRefreshHighlightKey();
            if (_parentReady)
            {
                _reportedParentFailure = false;
                _parentConfig.SettingChanged += ParentConfigSettingChanged;
            }

            return _parentReady;
        }

        private bool TryRefreshHighlightKey()
        {
            ConfigEntry<KeyCode> highlightKeyEntry;
            if (_parentConfig == null
                || !_parentConfig.TryGetEntry<KeyCode>(
                    "General",
                    "Highlight Key",
                    out highlightKeyEntry))
            {
                return ReportParentFailure("Could not read Wyrd Sight's General / Highlight Key setting.");
            }

            _highlightKeyEntry = highlightKeyEntry;
            _highlightKey = highlightKeyEntry.Value;
            LogDiagnostic("Wyrd Sight highlight key resolved to " + _highlightKey + ".");
            return true;
        }

        private void ParentConfigSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (args == null || args.ChangedSetting == null)
            {
                return;
            }

            ConfigDefinition definition = args.ChangedSetting.Definition;
            if (definition == null
                || !string.Equals(definition.Section, "General", StringComparison.Ordinal)
                || !string.Equals(definition.Key, "Highlight Key", StringComparison.Ordinal))
            {
                return;
            }

            if (_highlightKeyEntry != null)
            {
                _highlightKey = _highlightKeyEntry.Value;
                LogDiagnostic("Wyrd Sight highlight key changed to " + _highlightKey + ".");
            }
            else
            {
                TryRefreshHighlightKey();
            }
        }

        private void StartOrExtendPulse()
        {
            float now = Time.realtimeSinceStartup;
            float duration = GetPulseDurationSeconds();

            if (_ownsPulse)
            {
                bool stillOn;
                if (TryGetParentToggledOn(out stillOn) && stillOn)
                {
                    _pulseEndsAt = now + duration;
                    _nextOffAttemptAt = _pulseEndsAt;
                    _offAttempts = 0;
                    LogDiagnostic("Extended Wyrd Sight pulse.");
                    return;
                }

                ClearOwnedPulse();
            }

            bool toggledOn;
            if (!TryGetParentToggledOn(out toggledOn))
            {
                return;
            }

            if (toggledOn)
            {
                LogDiagnostic("Wyrd Sight is already on; the addon will not take ownership of an external toggle.");
                return;
            }

            if (!TryToggleParent("starting pulse"))
            {
                return;
            }

            if (!TryGetParentToggledOn(out toggledOn) || !toggledOn)
            {
                Logger.LogWarning(
                    "Tried to start a Wyrd Sight pulse, but Wyrd Sight did not report an enabled state.");
                ClearOwnedPulse();
                return;
            }

            _ownsPulse = true;
            _pulseEndsAt = now + duration;
            _nextStateCheckAt = now + GetPulseStateCheckIntervalSeconds();
            _nextOffAttemptAt = _pulseEndsAt;
            _offAttempts = 0;
            LogDiagnostic("Started Wyrd Sight pulse.");
        }

        private void TryEndOwnedPulse(float now, string reason)
        {
            if (!_ownsPulse)
            {
                return;
            }

            int maximumOffAttempts = GetMaximumOffAttempts();
            if (_offAttempts >= maximumOffAttempts)
            {
                Logger.LogWarning(
                    "Wyrd Sight stayed on after "
                    + maximumOffAttempts.ToString(CultureInfo.InvariantCulture)
                    + " addon off attempts. Clearing addon ownership to avoid repeated toggles.");
                ClearOwnedPulse();
                return;
            }

            bool toggledOn;
            if (!TryGetParentToggledOn(out toggledOn))
            {
                _offAttempts++;
                ScheduleOffRetry(now, reason);
                return;
            }

            if (!toggledOn)
            {
                ClearOwnedPulse();
                LogDiagnostic("Ended Wyrd Sight pulse because parent state was already off.");
                return;
            }

            _offAttempts++;
            if (!TryToggleParent(reason))
            {
                ScheduleOffRetry(now, reason);
                return;
            }

            if (!TryGetParentToggledOn(out toggledOn))
            {
                ScheduleOffRetry(now, reason);
                return;
            }

            if (!toggledOn)
            {
                ClearOwnedPulse();
                LogDiagnostic("Ended Wyrd Sight pulse.");
                return;
            }

            ScheduleOffRetry(now, reason);
        }

        private void ScheduleOffRetry(float now, string reason)
        {
            int maximumOffAttempts = GetMaximumOffAttempts();
            if (_offAttempts >= maximumOffAttempts)
            {
                Logger.LogWarning(
                    "Could not turn off the addon-owned Wyrd Sight pulse after "
                    + _offAttempts.ToString(CultureInfo.InvariantCulture)
                    + " attempts while "
                    + reason
                    + ". Clearing addon ownership.");
                ClearOwnedPulse();
                return;
            }

            _nextOffAttemptAt = now + GetOffRetryDelaySeconds();
            LogDiagnostic("Scheduled Wyrd Sight off retry after " + reason + ".");
        }

        private bool TryGetParentToggledOn(out bool toggledOn)
        {
            toggledOn = false;
            if (_parentPlugin == null || _isToggledOnGetter == null)
            {
                ReportParentFailure("Wyrd Sight parent state is unavailable.");
                return false;
            }

            try
            {
                object value = _isToggledOnGetter.Invoke(_parentPlugin, null);
                if (value is bool)
                {
                    toggledOn = (bool)value;
                    return true;
                }

                ReportParentFailure("Wyrd Sight IsToggledOn did not return a Boolean value.");
                return false;
            }
            catch (Exception exception)
            {
                ReportParentFailure(
                    "Could not read Wyrd Sight toggle state: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool TryToggleParent(string reason)
        {
            if (_parentPlugin == null || _toggleWyrdSightMethod == null)
            {
                ReportParentFailure("Wyrd Sight parent toggle method is unavailable.");
                return false;
            }

            try
            {
                _toggleWyrdSightMethod.Invoke(_parentPlugin, null);
                LogDiagnostic("Invoked Wyrd Sight toggle while " + reason + ".");
                return true;
            }
            catch (Exception exception)
            {
                ReportParentFailure(
                    "Could not invoke Wyrd Sight toggle while "
                    + reason
                    + ": "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private float GetPulseDurationSeconds()
        {
            if (_pulseDurationSeconds == null)
            {
                return DefaultPulseDurationSeconds;
            }

            return Mathf.Clamp(
                _pulseDurationSeconds.Value,
                MinimumPulseDurationSeconds,
                MaximumPulseDurationSeconds);
        }

        private float GetPulseStateCheckIntervalSeconds()
        {
            if (_pulseStateCheckIntervalSeconds == null)
            {
                return DefaultPulseStateCheckIntervalSeconds;
            }

            return Mathf.Clamp(
                _pulseStateCheckIntervalSeconds.Value,
                MinimumPulseStateCheckIntervalSeconds,
                MaximumPulseStateCheckIntervalSeconds);
        }

        private float GetOffRetryDelaySeconds()
        {
            if (_offRetryDelaySeconds == null)
            {
                return DefaultOffRetryDelaySeconds;
            }

            return Mathf.Clamp(
                _offRetryDelaySeconds.Value,
                MinimumOffRetryDelaySeconds,
                MaximumOffRetryDelaySeconds);
        }

        private int GetMaximumOffAttempts()
        {
            if (_maximumOffAttempts == null)
            {
                return DefaultMaximumOffAttempts;
            }

            return Mathf.Clamp(
                _maximumOffAttempts.Value,
                MinimumOffAttemptCount,
                MaximumOffAttemptCount);
        }

        private void ClearOwnedPulse()
        {
            _ownsPulse = false;
            _pulseEndsAt = 0f;
            _nextStateCheckAt = 0f;
            _nextOffAttemptAt = 0f;
            _offAttempts = 0;
        }

        private bool ReportParentFailure(string message)
        {
            _parentReady = false;
            if (!_reportedParentFailure)
            {
                Logger.LogWarning(message);
                _reportedParentFailure = true;
            }

            return false;
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        private static class WyrdSightInputPatch
        {
            public static bool BeforeHandleInput(object __instance)
            {
                WyrdSightAddonPlugin instance = Instance;
                if (instance == null)
                {
                    return true;
                }

                return instance.BeforeWyrdSightHandleInput(__instance);
            }
        }
    }
}
