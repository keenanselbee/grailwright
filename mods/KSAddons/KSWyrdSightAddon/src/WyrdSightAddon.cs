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
[assembly: AssemblyDescription("Pulse-key and quest-giver companion addon for Wyrd Sight")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Wyrd Sight Addon")]
[assembly: AssemblyVersion("1.2.8.0")]
[assembly: AssemblyFileVersion("1.2.8.0")]

namespace Keenan.TGFoA.WyrdSightAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(ParentPluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class WyrdSightAddonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.wyrd-sight-addon";
        public const string PluginName = "Wyrd Sight Addon";
        public const string PluginVersion = "1.2.8";
        public const string ParentPluginGuid = "WyrdSight";

        private const int ConfigSchemaVersion = 4;
        private const int ConfigRecoveryBaselineSchema = 2;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
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
        private ConfigEntry<bool> _highlightQuestGivers;
        private ConfigEntry<QuestGiverMode> _questGiverMode;
        private ConfigEntry<float> _questGiverMaxDistance;
        private ConfigEntry<int> _questScanFrameBudgetMilliseconds;
        private ConfigEntry<float> _questOutlineBakeFrameBudgetMilliseconds;
        private ConfigEntry<int> _questOutlineRefreshRate;
        private ConfigEntry<float> _questAvailabilityRefreshSeconds;
        private ConfigEntry<bool> _diagnostics;

        private Harmony _harmony;
        private BaseUnityPlugin _parentPlugin;
        private ConfigFile _parentConfig;
        private ConfigEntry<KeyCode> _highlightKeyEntry;
        private KeyCode _highlightKey = KeyCode.None;
        private MethodInfo _toggleWyrdSightMethod;
        private MethodInfo _isToggledOnGetter;
        private MethodInfo _isToggleTargetOnGetter;
        private bool _parentReady;
        private bool _reportedParentFailure;

        private AvalonUntold.GlowController _questGiverRuntime;

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
                Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                    Config,
                    Logger,
                    PluginName,
                    ConfigSchemaVersion,
                    ConfigRecoveryBaselineSchema,
                    ConfigRecoveryKeepCurrentDefaultRules,
                    ConfigRecoveryPermanentExclusions);

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
                    prefix: new HarmonyMethod(
                        typeof(WyrdSightInputPatch),
                        nameof(WyrdSightInputPatch.BeforeHandleInput)));

                _questGiverRuntime = new AvalonUntold.GlowController(Logger);

                Config.Save();
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Wyrd Sight highlight key pulses for "
                    + GetPulseDurationSeconds().ToString("0.###", CultureInfo.InvariantCulture)
                    + " seconds; state checks every "
                    + GetPulseStateCheckIntervalSeconds().ToString("0.###", CultureInfo.InvariantCulture)
                    + " seconds. Quest-giver highlights are "
                    + (_highlightQuestGivers.Value
                        ? ("enabled in " + _questGiverMode.Value + " mode.")
                        : "disabled."));
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
            UpdateOwnedPulse();

            if (_questGiverRuntime != null
                && _enabled != null
                && _enabled.Value
                && _highlightQuestGivers != null
                && _highlightQuestGivers.Value)
            {
                bool wyrdSightActive;
                bool hasState = TryGetParentToggleTargetOn(out wyrdSightActive);
                _questGiverRuntime.Tick(
                    hasState && wyrdSightActive,
                    ToIntegratedGlowMode(),
                    _questGiverMaxDistance == null ? 20f : _questGiverMaxDistance.Value,
                    _questScanFrameBudgetMilliseconds == null ? 5 : _questScanFrameBudgetMilliseconds.Value,
                    _questOutlineBakeFrameBudgetMilliseconds == null
                        ? 1.5f
                        : _questOutlineBakeFrameBudgetMilliseconds.Value,
                    _questOutlineRefreshRate == null ? 30 : _questOutlineRefreshRate.Value,
                    _questAvailabilityRefreshSeconds == null
                        ? 15f
                        : _questAvailabilityRefreshSeconds.Value);
            }
        }

        private void UpdateOwnedPulse()
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

            if (_highlightQuestGivers != null)
            {
                _highlightQuestGivers.SettingChanged -= QuestGiverSettingChanged;
            }

            if (_enabled != null)
            {
                _enabled.SettingChanged -= QuestGiverSettingChanged;
            }

            if (_questGiverMode != null)
            {
                _questGiverMode.SettingChanged -= QuestGiverSettingChanged;
            }

            if (_questGiverRuntime != null)
            {
                _questGiverRuntime.Dispose();
                _questGiverRuntime = null;
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
                    "Master switch. When disabled, Wyrd Sight keeps its original highlight-key toggle behavior and quest-giver highlighting is inactive.",
                    "General", "Enabled", 0, 0));
            _pulseDurationSeconds = Config.Bind(
                "Pulse Timing",
                "PulseDurationSeconds",
                DefaultPulseDurationSeconds,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How long a Wyrd Sight pulse stays active before the addon turns it off and lets Wyrd Sight fade normally.",
                    "Pulse Timing", "Pulse Duration", 10, 0,
                    new AcceptableValueRange<float>(MinimumPulseDurationSeconds, MaximumPulseDurationSeconds)));
            _pulseStateCheckIntervalSeconds = Config.Bind(
                "Pulse Timing",
                "PulseStateCheckIntervalSeconds",
                DefaultPulseStateCheckIntervalSeconds,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How often the addon checks whether an addon-owned pulse was already turned off outside the addon.",
                    "Pulse Timing", "State Check Interval", 10, 10,
                    new AcceptableValueRange<float>(MinimumPulseStateCheckIntervalSeconds, MaximumPulseStateCheckIntervalSeconds)));
            _offRetryDelaySeconds = Config.Bind(
                "Pulse Timing",
                "OffRetryDelaySeconds",
                DefaultOffRetryDelaySeconds,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How long to wait between off-toggle retries when Wyrd Sight still reports active or its state cannot be read.",
                    "Pulse Timing", "Off Retry Delay", 10, 20,
                    new AcceptableValueRange<float>(MinimumOffRetryDelaySeconds, MaximumOffRetryDelaySeconds)));
            _maximumOffAttempts = Config.Bind(
                "Pulse Timing",
                "MaximumOffAttempts",
                DefaultMaximumOffAttempts,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How many off-toggle attempts the addon may make before it clears pulse ownership and leaves Wyrd Sight alone.",
                    "Pulse Timing", "Maximum Off Attempts", 10, 30,
                    new AcceptableValueRange<int>(MinimumOffAttemptCount, MaximumOffAttemptCount)));
            _highlightQuestGivers = Config.Bind(
                "Quest Givers",
                "HighlightQuestGivers",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Show outlines on NPCs with untaken quests while Wyrd Sight is actively on.",
                    "Quest Givers", "Highlight Quest Givers", 20, 0));
            _questGiverMode = Config.Bind(
                "Quest Givers",
                "QuestGiverMode",
                QuestGiverMode.Balanced,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Quest selection preset. Thorough hides nothing, Balanced hides grants blocked by durable story progress, and Precise shows only grants confirmed available now.",
                    "Quest Givers", "Quest Giver Mode", 20, 10));
            _questGiverMaxDistance = Config.Bind(
                "Quest Givers",
                "QuestGiverMaxDistance",
                15f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum distance in metres for drawing quest-giver outlines. Lower values reduce outline work.",
                    "Quest Givers", "Maximum Distance", 20, 20,
                    new AcceptableValueRange<float>(5f, 100f)));
            _questScanFrameBudgetMilliseconds = Config.Bind(
                "Quest Givers",
                "QuestScanFrameBudgetMilliseconds",
                1,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Target frame slice for the background story scan. Individual graph or setup operations can exceed it; lower values are usually smoother but finish more slowly.",
                    "Quest Givers", "Story Scan Frame Budget", 20, 30,
                    new AcceptableValueRange<int>(1, 10)));
            _questOutlineBakeFrameBudgetMilliseconds = Config.Bind(
                "Quest Givers",
                "QuestOutlineBakeFrameBudgetMilliseconds",
                0.25f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Target frame slice for preparing golden outline meshes while Wyrd Sight is active. One NPC mesh operation can exceed it.",
                    "Quest Givers", "Outline Bake Frame Budget", 20, 40,
                    new AcceptableValueRange<float>(0.25f, 4f)));
            _questOutlineRefreshRate = Config.Bind(
                "Quest Givers",
                "QuestOutlineRefreshRate",
                10,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum animated-pose refresh rate for golden quest-giver outlines. Lower values reduce CPU mesh-baking work without changing the render style.",
                    "Quest Givers", "Outline Refresh Rate", 20, 50,
                    new AcceptableValueRange<int>(10, 60)));
            _questAvailabilityRefreshSeconds = Config.Bind(
                "Quest Givers",
                "QuestAvailabilityRefreshSeconds",
                60f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How often quest availability is refreshed while Wyrd Sight remains continuously active. Every new pulse also requests a refresh.",
                    "Quest Givers", "Availability Refresh Interval", 20, 60,
                    new AcceptableValueRange<float>(5f, 60f)));
            _enabled.SettingChanged += QuestGiverSettingChanged;
            _highlightQuestGivers.SettingChanged += QuestGiverSettingChanged;
            _questGiverMode.SettingChanged += QuestGiverSettingChanged;
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Log pulse ownership and parent-state details.",
                    "Diagnostics", "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 0));
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
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid, PluginName, storedSchemaVersion, ConfigSchemaVersion);
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
            _isToggleTargetOnGetter = AccessTools.PropertyGetter(parentType, "IsToggleTargetOn");

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

        private void QuestGiverSettingChanged(object sender, EventArgs args)
        {
            if (_questGiverRuntime != null
                && ((_enabled != null && !_enabled.Value)
                    || (_highlightQuestGivers != null
                        && !_highlightQuestGivers.Value)))
            {
                _questGiverRuntime.Disable();
            }

            LogDiagnostic("Quest-giver settings changed; the integrated detector will refresh on its next frame.");
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

        private bool TryGetParentToggleTargetOn(out bool targetOn)
        {
            targetOn = false;
            if (_parentPlugin == null || _isToggleTargetOnGetter == null)
            {
                return TryGetParentToggledOn(out targetOn);
            }

            try
            {
                object value = _isToggleTargetOnGetter.Invoke(_parentPlugin, null);
                if (value is bool)
                {
                    targetOn = (bool)value;
                    return true;
                }

                ReportParentFailure("Wyrd Sight IsToggleTargetOn did not return a Boolean value.");
                return false;
            }
            catch (Exception exception)
            {
                ReportParentFailure(
                    "Could not read Wyrd Sight target state: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private AvalonUntold.GlowMode ToIntegratedGlowMode()
        {
            if (_questGiverMode == null)
            {
                return AvalonUntold.GlowMode.Balanced;
            }

            switch (_questGiverMode.Value)
            {
                case QuestGiverMode.Thorough:
                    return AvalonUntold.GlowMode.Thorough;
                case QuestGiverMode.Precise:
                    return AvalonUntold.GlowMode.Precise;
                default:
                    return AvalonUntold.GlowMode.Balanced;
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

        private enum QuestGiverMode
        {
            Thorough,
            Balanced,
            Precise
        }
    }
}
