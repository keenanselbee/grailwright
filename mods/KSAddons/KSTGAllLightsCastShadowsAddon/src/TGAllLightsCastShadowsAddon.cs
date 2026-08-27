using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
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

[assembly: AssemblyTitle("All Lights Cast Shadows Addon")]
[assembly: AssemblyDescription("Bounded, view-aware shadow selection companion for All Lights Cast Shadows")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("All Lights Cast Shadows Addon")]
[assembly: AssemblyVersion("2.0.5.0")]
[assembly: AssemblyFileVersion("2.0.5.0")]
[assembly: AssemblyInformationalVersion("2.0.5")]

namespace TGAllLightsCastShadowsAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        ParentPluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(MageLightPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(NoPlayerLightPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(EyesInTheDarkPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.tg-all-lights-cast-shadows-addon";
        public const string PluginName = "All Lights Cast Shadows Addon";
        public const string PluginVersion = "2.0.5";
        public const string ParentPluginGuid =
            "com.wessberg.tgalllightscastshadows";
        public const string MageLightPluginGuid = "Gotik0.magelight";
        public const string NoPlayerLightPluginGuid = "ks.tgfoa.no-player-light";
        public const string EyesInTheDarkPluginGuid =
            "ks.tgfoa.eyes-in-the-dark";
        private const int ConfigSchemaVersion = 3;
        private const int ConfigRecoveryBaselineSchema = 2;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const string BuiltInExcludedLightPathFragments =
            "WyrdNight_Repeller_Bonfire,Repeller_Bonfire,Bonfire,Campfire";

        internal static Plugin Instance { get; private set; }

        private Harmony _harmony;
        private bool _originalShadowQualityCaptured;
        private ShadowQuality _originalShadowQuality;
        private FieldInfo _activeLightsField;
        private FieldInfo _originalStatesField;
        private FieldInfo _originalShadowsField;
        private FieldInfo _originalShadowStrengthField;
        private FieldInfo _runtimeEnabledField;
        private FieldInfo _nextScanTimeField;
        private PropertyInfo _parentCurrentConfigProperty;
        private FieldInfo _parentUseBudgetField;
        private FieldInfo _parentMaximumUpgradedLightsField;
        private FieldInfo _parentMaximumDistanceMetersField;
        private object _parentManager;
        private bool _parentRuntimeEnabledKnown;
        private bool _parentRuntimeEnabled;
        private bool _hdrpResolved;
        private Type _hdAdditionalLightDataType;
        private MethodInfo _hdEnableShadowsMethod;
        private MemberInfo _hdShadowDimmerMember;
        private MemberInfo _hdVolumetricShadowDimmerMember;
        private PropertyInfo _hdShadowResolutionProperty;
        private MethodInfo _hdSetShadowResolutionMethod;
        private MethodInfo _hdSetShadowResolutionLevelMethod;
        private MethodInfo _hdSetShadowResolutionOverrideMethod;
        private bool _atlasUnavailableReported;
        private ConfigEntry<bool> _protectBonfireLights;
        private ConfigEntry<string> _additionalExcludedLightPathFragments;
        private ConfigEntry<bool> _verboseExclusionLogging;
        private ConfigEntry<bool> _protectShadowAtlas;
        private ConfigEntry<int> _promotedShadowResolution;
        private ConfigEntry<bool> _combatPerformanceEnabled;
        private ConfigEntry<bool> _outdoorCombatOnly;
        private ConfigEntry<float> _combatExitDelaySeconds;
        private ConfigEntry<bool> _combatReduceAtlasResolution;
        private ConfigEntry<int> _combatShadowResolution;
        private ConfigEntry<bool> _combatLimitLightBudget;
        private ConfigEntry<int> _combatMaximumUpgradedLights;
        private ConfigEntry<bool> _combatLimitDistance;
        private ConfigEntry<float> _combatMaximumDistanceMeters;
        private ConfigEntry<bool> _showToggleNotifications;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _showGrailFloatingTextDiagnostics;
        private string[] _excludedFragments = new string[0];
        private readonly Dictionary<ConfigDefinition, object> _pendingPreservedSettings =
            new Dictionary<ConfigDefinition, object>();
        private readonly Dictionary<int, ProtectedLightState> _protectedLights =
            new Dictionary<int, ProtectedLightState>();
        private readonly Dictionary<int, ShadowResolutionState> _shadowResolutionStates =
            new Dictionary<int, ShadowResolutionState>();
        private readonly HashSet<int> _atlasScanLights = new HashSet<int>();
        private readonly HashSet<int> _loggedExcludedLights =
            new HashSet<int>();
        private int _atlasScanPointLights;
        private int _atlasScanSpotLights;
        private int _atlasScanOtherLights;
        private int _atlasScanConstrainedLights;
        private int _atlasScanEstimatedMaps;
        private int _atlasScanRestoredLights;
        private string _lastAtlasDiagnosticSignature = string.Empty;
        private CombatParentConfigSnapshot _combatParentConfigSnapshot;
        private bool _combatParentConfigUnavailableReported;
        private bool _combatPerformanceActive;
        private float _combatExitEligibleAt;
        private float _nextCombatStateCheck;

        private void Awake()
        {
            Instance = this;

            try
            {
                Type shadowManagerType = AccessTools.TypeByName(
                    "TGAllLightsCastShadows.ShadowManager");
                if (shadowManagerType == null)
                {
                    throw new TypeLoadException(
                        "Could not find TGAllLightsCastShadows.ShadowManager.");
                }

                MethodInfo applyAllLightsMethod = AccessTools.Method(
                    shadowManagerType,
                    "ApplyAllLights",
                    new[] { typeof(string) });
                MethodInfo restoreAllLightsMethod = AccessTools.Method(
                    shadowManagerType,
                    "RestoreAllLoadedTrackedLights",
                    new[] { typeof(string) });
                MethodInfo updateMethod = AccessTools.Method(
                    shadowManagerType,
                    "Update");
                MethodInfo beginSceneCooldownMethod = AccessTools.Method(
                    shadowManagerType,
                    "BeginSceneCooldown",
                    new[] { typeof(string) });
                Type hdrpSupportType = AccessTools.TypeByName(
                    "TGAllLightsCastShadows.HdrpSupport");
                MethodInfo hdrpRefreshMethod = hdrpSupportType == null
                    ? null
                    : AccessTools.Method(
                        hdrpSupportType,
                        "TryRefresh",
                        new[] { typeof(Light), typeof(float) });
                MethodInfo hdrpDimmerMethod = hdrpSupportType == null
                    ? null
                    : AccessTools.Method(
                        hdrpSupportType,
                        "TryApplyDimmer",
                        new[] { typeof(Light), typeof(float) });
                if (applyAllLightsMethod == null
                    || restoreAllLightsMethod == null
                    || updateMethod == null
                    || beginSceneCooldownMethod == null
                    || hdrpRefreshMethod == null
                    || hdrpDimmerMethod == null)
                {
                    throw new MissingMethodException(
                        "Could not find the parent light mod's shadow or HDRP methods.");
                    }

                InitializeConfig();
                InitializeParentReflection(shadowManagerType);
                InitializeManagedParentReflection(shadowManagerType);
                InitializeDawnDuskShadowController();

                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(
                    applyAllLightsMethod,
                    prefix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.BeforeApplyAllLights)),
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.AfterApplyAllLights)),
                    finalizer: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.FinalizeApplyAllLights)));
                _harmony.Patch(
                    restoreAllLightsMethod,
                    prefix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.BeforeRestoreAllLights)),
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.AfterRestoreAllLights)));
                _harmony.Patch(
                    updateMethod,
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.AfterShadowManagerUpdate)));
                _harmony.Patch(
                    beginSceneCooldownMethod,
                    prefix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.BeforeParentSceneCooldown)));
                HarmonyMethod atlasPrefix = new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.BeforeHdrpShadowRefresh));
                _harmony.Patch(hdrpRefreshMethod, prefix: atlasPrefix);
                _harmony.Patch(hdrpDimmerMethod, prefix: atlasPrefix);

                ObserveParentRuntimeState(false);

                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded; safe cached selection, semantic exclusions, exact shadow-state restoration, and atlas protection are active.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
            }
        }

        private void Update()
        {
            UpdateDawnDuskShadows();
            UpdateManagedShadowController();
            if (_combatPerformanceEnabled == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextCombatStateCheck)
            {
                _nextCombatStateCheck = Time.unscaledTime + 0.25f;
                RefreshCombatPerformanceState();
            }
        }

        internal bool BeforeApplyAllLights(string reason)
        {
            if (UseManagedShadowController())
            {
                return BeforeManagedParentApply(reason);
            }

            BeginAtlasScan();
            ApplyCombatParentConfigOverrides();
            if (_originalShadowQualityCaptured)
            {
                ProtectExcludedLightsBeforeParentScan();
                return true;
            }

            _originalShadowQuality = QualitySettings.shadows;
            _originalShadowQualityCaptured = true;
            Logger.LogInfo(
                "Captured global shadow quality: "
                + _originalShadowQuality);
            ProtectExcludedLightsBeforeParentScan();
            return true;
        }

        internal void AfterApplyAllLights()
        {
            if (UseManagedShadowController())
            {
                return;
            }
            RestoreCombatParentConfig();
            RestoreProtectedLightsAfterParentScan();
            RestoreExcludedLightsTouchedByParent();
            RestoreInactiveShadowResolutions();
            ReportAtlasDiagnostics();
        }

        internal void FinalizeApplyAllLights()
        {
            if (!UseManagedShadowController())
            {
                RestoreCombatParentConfig();
            }
        }

        internal void AfterRestoreAllLights()
        {
            if (UseManagedShadowController())
            {
                RestoreAllManagedLights("parent restore");
                _loggedExcludedLights.Clear();
                return;
            }
            RestoreProtectedLightsAfterParentScan();
            RestoreAllShadowResolutions();
            _loggedExcludedLights.Clear();

            if (!_originalShadowQualityCaptured)
            {
                return;
            }

            QualitySettings.shadows = _originalShadowQuality;
            Logger.LogInfo(
                "Restored global shadow quality after light upgrades were disabled: "
                + _originalShadowQuality);
            _originalShadowQualityCaptured = false;
        }

        internal void AfterShadowManagerUpdate(object manager)
        {
            _parentManager = manager;
            ObserveParentRuntimeState(true);
        }

        internal void BeforeHdrpShadowRefresh(Light light)
        {
            if (!UseManagedShadowController())
            {
                ApplyShadowAtlasProtection(light);
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
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _protectBonfireLights = Config.Bind(
                "Excluded Lights",
                "ProtectBonfireLights",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Prevents selected bonfire/campfire style lights from being upgraded to cast shadows.",
                    "Excluded Lights", "Protect Bonfire Lights", 0, 0));
            _additionalExcludedLightPathFragments = Config.Bind(
                "Excluded Lights",
                "AdditionalExcludedLightPathFragments",
                "",
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Optional comma-separated transform name fragments to exclude in addition to the addon's built-in bonfire and campfire names.",
                    "Excluded Lights", "Additional Excluded Light Paths", 0, 10));
            _verboseExclusionLogging = Config.Bind(
                "Excluded Lights",
                "VerboseExclusionLogging",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Logs each excluded light path once per scene. Useful for finding exact runtime names.",
                    "Excluded Lights", "Verbose Exclusion Logging", 0, 80));
            BindManagedShadowConfig();
            _protectShadowAtlas = Config.Bind(
                "Shadow Atlas",
                "ProtectShadowAtlas",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Caps only parent-promoted point and spot light shadow maps, reducing HDRP atlas rescaling and flicker.",
                    "Shadow Atlas", "Protect Shadow Atlas", 10, 0));
            _promotedShadowResolution = Config.Bind(
                "Shadow Atlas",
                "PromotedShadowResolution",
                256,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum per-face shadow resolution for parent-promoted point and spot lights. Original lower overrides are never raised.",
                    "Shadow Atlas", "Promoted Shadow Resolution", 10, 10,
                    new AcceptableValueList<int>(128, 256, 512, 1024)));
            BindDawnDuskShadowConfig();
            _combatPerformanceEnabled = Config.Bind(
                "Combat Performance",
                "CombatPerformanceEnabled",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Enables reversible combat-aware shadow adjustments. By default only the lower combat atlas cap is active.",
                    "Combat Performance", "Enabled", 20, 0));
            _outdoorCombatOnly = Config.Bind(
                "Combat Performance",
                "OutdoorCombatOnly",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Applies combat adjustments only while the hero is fighting outdoors.",
                    "Combat Performance", "Outdoor Combat Only", 20, 10));
            _combatExitDelaySeconds = Config.Bind(
                "Combat Performance",
                "CombatExitDelaySeconds",
                5f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How long combat must remain over before normal shadow settings return.",
                    "Combat Performance", "Combat Exit Delay", 20, 20,
                    new AcceptableValueRange<float>(0f, 20f)));
            _combatReduceAtlasResolution = Config.Bind(
                "Combat Performance",
                "CombatReduceAtlasResolution",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Uses the lower combat atlas cap for parent-promoted point and spot lights.",
                    "Combat Performance", "Reduce Atlas Resolution", 20, 30));
            _combatShadowResolution = Config.Bind(
                "Combat Performance",
                "CombatShadowResolution",
                128,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum per-face shadow resolution during qualifying combat.",
                    "Combat Performance", "Combat Shadow Resolution", 20, 40,
                    new AcceptableValueList<int>(128, 256, 512, 1024)));
            _combatLimitLightBudget = Config.Bind(
                "Combat Performance",
                "CombatLimitLightBudget",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Optionally lowers the parent's upgraded-light budget during combat.",
                    "Combat Performance", "Limit Light Budget", 20, 50));
            _combatMaximumUpgradedLights = Config.Bind(
                "Combat Performance",
                "CombatMaximumUpgradedLights",
                30,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum upgraded lights used when CombatLimitLightBudget is enabled. Never raises the parent's current limit.",
                    "Combat Performance", "Maximum Upgraded Lights", 20, 60,
                    new AcceptableValueRange<int>(0, 200)));
            _combatLimitDistance = Config.Bind(
                "Combat Performance",
                "CombatLimitDistance",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Optionally lowers the parent's maximum upgraded-light distance during combat.",
                    "Combat Performance", "Limit Light Distance", 20, 70));
            _combatMaximumDistanceMeters = Config.Bind(
                "Combat Performance",
                "CombatMaximumDistanceMeters",
                20f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum light distance used when CombatLimitDistance is enabled. Never raises the parent's current distance.",
                    "Combat Performance", "Maximum Light Distance", 20, 80,
                    new AcceptableValueRange<float>(1f, 100f)));
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
                    "Logs atlas counts and restored resolution state. Also shows collapsed Grail Floating Text atlas summaries when available.",
                    "Diagnostics", "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 0));
            _showGrailFloatingTextDiagnostics = Config.Bind(
                "Diagnostics",
                "ShowGrailFloatingTextDiagnostics",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "When Diagnostics is enabled and Grail Floating Text is installed, show diagnostic combat and atlas summaries. Detailed BepInEx logging remains active when this is disabled.",
                    "Diagnostics", "Show Grail Floating Text Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 10));

            RestorePreservedSettings();
            RefreshExcludedFragments();
            _additionalExcludedLightPathFragments.SettingChanged +=
                OnAdditionalExcludedLightPathFragmentsChanged;
            _protectShadowAtlas.SettingChanged +=
                OnShadowAtlasSettingChanged;
            _promotedShadowResolution.SettingChanged +=
                OnShadowAtlasSettingChanged;
            SubscribeManagedShadowConfigEvents();
            SubscribeDawnDuskShadowConfigEvents();
            SubscribeCombatConfigEvents();
            _diagnostics.SettingChanged +=
                OnDiagnosticsSettingChanged;
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

            CapturePreservedSettings(
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
                        "Could not restore the previous All Lights Cast Shadows Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset All Lights Cast Shadows Addon config schema. Original config was left in place when possible.",
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

            CaptureCustomizedValue(
                profile,
                "Excluded Lights",
                "ProtectBonfireLights",
                false);
            CaptureCustomizedValue(
                profile,
                "Excluded Lights",
                "AdditionalExcludedLightPathFragments",
                string.Empty);
            CaptureCustomizedValue(
                profile,
                "Excluded Lights",
                "VerboseExclusionLogging",
                false);
            CaptureCustomizedValue(profile, "Excluded Lights", "ExcludeWyrdSightLights", false);
            CaptureCustomizedValue(profile, "Excluded Lights", "ExcludeSummonLights", false);
            CaptureCustomizedValue(profile, "Excluded Lights", "ExcludeInterfacePreviewLights", false);
            CaptureCustomizedValue(profile, "Excluded Lights", "ExcludeLockpickingLights", false);
            CaptureCustomizedValue(profile, "Excluded Lights", "ExcludePlacedBonfireLights", false);
            CaptureCustomizedValue(profile, "Excluded Lights", "RespectExternalPlayerLightOwnership", false);
            CaptureCustomizedValue(profile, "Performance", "UseSafeSelectionController", false);
            CaptureCustomizedValue(profile, "Performance", "MaximumUpgradedLights", 0);
            CaptureCustomizedValue(profile, "Performance", "MaximumDistanceMeters", 0f);
            CaptureCustomizedValue(profile, "Performance", "MaximumShadowMapFaces", 0);
            CaptureCustomizedValue(profile, "Performance", "SuppressAddedVolumetricShadows", false);
            CaptureCustomizedValue(profile, "View Priority", "HysteresisMeters", 0f);
            CaptureCustomizedValue(profile, "View Priority", "PreferViewRelevantLights", false);
            CaptureCustomizedValue(profile, "View Priority", "SelectionRefreshSeconds", 0f);
            CaptureCustomizedValue(profile, "View Priority", "ViewExitDelaySeconds", 0f);
            CaptureCustomizedValue(profile, "View Priority", "OffscreenReserveLights", 0);
            CaptureCustomizedValue(profile, "View Priority", "MaximumSelectionSwapsPerRefresh", 0);
            CaptureCustomizedValue(profile, "Directional Shadows", "ImproveDawnDuskShadows", false);
            CaptureCustomizedValue(profile, "Directional Shadows", "ShadowBlendMinutes", 0);
            CaptureCustomizedValue(profile, "Directional Shadows", "NormalizeForEyesInTheDark", false);
            CaptureCustomizedValue(profile, "Directional Shadows", "EyesBlendSecondsPerSide", 0f);
            CaptureCustomizedValue(
                profile,
                "Shadow Atlas",
                "ProtectShadowAtlas",
                false);
            CaptureCustomizedValue(
                profile,
                "Shadow Atlas",
                "PromotedShadowResolution",
                0);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatPerformanceEnabled", false);
            CaptureCustomizedValue(profile, "Combat Performance", "OutdoorCombatOnly", false);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatExitDelaySeconds", 0f);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatReduceAtlasResolution", false);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatShadowResolution", 0);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatLimitLightBudget", false);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatMaximumUpgradedLights", 0);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatLimitDistance", false);
            CaptureCustomizedValue(profile, "Combat Performance", "CombatMaximumDistanceMeters", 0f);
            CaptureCustomizedValue(
                profile,
                "Diagnostics",
                "Diagnostics",
                false);
            CaptureCustomizedValue(
                profile,
                "Diagnostics",
                "ShowGrailFloatingTextDiagnostics",
                false);
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
            RestorePreservedEntry(_protectBonfireLights, ref restored, ref clamped);
            RestorePreservedEntry(
                _additionalExcludedLightPathFragments,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _verboseExclusionLogging,
                ref restored,
                ref clamped);
            RestorePreservedEntry(_excludeWyrdSightLights, ref restored, ref clamped);
            RestorePreservedEntry(_excludeSummonLights, ref restored, ref clamped);
            RestorePreservedEntry(_excludeInterfacePreviewLights, ref restored, ref clamped);
            RestorePreservedEntry(_excludeLockpickingLights, ref restored, ref clamped);
            RestorePreservedEntry(_excludePlacedBonfireLights, ref restored, ref clamped);
            RestorePreservedEntry(_respectExternalPlayerLightOwnership, ref restored, ref clamped);
            RestorePreservedEntry(_safeSelectionController, ref restored, ref clamped);
            RestorePreservedEntry(_maximumUpgradedLights, ref restored, ref clamped);
            RestorePreservedEntry(_maximumDistanceMeters, ref restored, ref clamped);
            RestorePreservedEntry(_maximumShadowMapFaces, ref restored, ref clamped);
            RestorePreservedEntry(_suppressAddedVolumetricShadows, ref restored, ref clamped);
            RestorePreservedEntry(_selectionHysteresisMeters, ref restored, ref clamped);
            RestorePreservedEntry(_preferViewRelevantLights, ref restored, ref clamped);
            RestorePreservedEntry(_selectionRefreshSeconds, ref restored, ref clamped);
            RestorePreservedEntry(_viewExitDelaySeconds, ref restored, ref clamped);
            RestorePreservedEntry(_offscreenReserveLights, ref restored, ref clamped);
            RestorePreservedEntry(_maximumSelectionSwapsPerRefresh, ref restored, ref clamped);
            RestorePreservedEntry(_improveDawnDuskShadows, ref restored, ref clamped);
            RestorePreservedEntry(_dawnDuskShadowBlendMinutes, ref restored, ref clamped);
            RestorePreservedEntry(_normalizeDawnDuskForEyesInTheDark, ref restored, ref clamped);
            RestorePreservedEntry(_eyesDawnDuskSecondsPerSide, ref restored, ref clamped);
            RestorePreservedEntry(_protectShadowAtlas, ref restored, ref clamped);
            RestorePreservedEntry(
                _promotedShadowResolution,
                ref restored,
                ref clamped);
            RestorePreservedEntry(_combatPerformanceEnabled, ref restored, ref clamped);
            RestorePreservedEntry(_outdoorCombatOnly, ref restored, ref clamped);
            RestorePreservedEntry(_combatExitDelaySeconds, ref restored, ref clamped);
            RestorePreservedEntry(_combatReduceAtlasResolution, ref restored, ref clamped);
            RestorePreservedEntry(_combatShadowResolution, ref restored, ref clamped);
            RestorePreservedEntry(_combatLimitLightBudget, ref restored, ref clamped);
            RestorePreservedEntry(_combatMaximumUpgradedLights, ref restored, ref clamped);
            RestorePreservedEntry(_combatLimitDistance, ref restored, ref clamped);
            RestorePreservedEntry(_combatMaximumDistanceMeters, ref restored, ref clamped);
            RestorePreservedEntry(_diagnostics, ref restored, ref clamped);
            RestorePreservedEntry(_showGrailFloatingTextDiagnostics, ref restored, ref clamped);

            Logger.LogInfo(
                "Preserved "
                + restored.ToString(CultureInfo.InvariantCulture)
                + " setting(s) across the config schema reset; clamped="
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
                || !_pendingPreservedSettings.TryGetValue(
                    entry.Definition,
                    out rawValue)
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

        private void InitializeParentReflection(Type shadowManagerType)
        {
            _activeLightsField = AccessTools.Field(
                shadowManagerType,
                "ActiveLights");
            _originalStatesField = AccessTools.Field(
                shadowManagerType,
                "OriginalStates");
            _runtimeEnabledField = AccessTools.Field(
                shadowManagerType,
                "RuntimeEnabled");
            _nextScanTimeField = AccessTools.Field(
                shadowManagerType,
                "_nextScanTime");

            Type localConfigType = AccessTools.TypeByName(
                "TGAllLightsCastShadows.LocalConfig");
            Type localConfigDataType = AccessTools.TypeByName(
                "TGAllLightsCastShadows.LocalConfigData");
            if (localConfigType != null && localConfigDataType != null)
            {
                _parentCurrentConfigProperty = AccessTools.Property(
                    localConfigType,
                    "Current");
                _parentUseBudgetField = AccessTools.Field(
                    localConfigDataType,
                    "UseBudget");
                _parentMaximumUpgradedLightsField = AccessTools.Field(
                    localConfigDataType,
                    "MaxUpgradedLights");
                _parentMaximumDistanceMetersField = AccessTools.Field(
                    localConfigDataType,
                    "MaxDistanceMeters");
            }

            Type originalStateType = shadowManagerType.GetNestedType(
                "OriginalLightState",
                BindingFlags.Public | BindingFlags.NonPublic);
            if (originalStateType != null)
            {
                _originalShadowsField = AccessTools.Field(
                    originalStateType,
                    "Shadows");
                _originalShadowStrengthField = AccessTools.Field(
                    originalStateType,
                    "ShadowStrength");
            }

            if (_activeLightsField == null
                || _originalStatesField == null
                || _originalShadowsField == null
                || _originalShadowStrengthField == null)
            {
                Logger.LogWarning(
                    "Could not resolve all parent light-state fields; excluded-light cleanup will use fallbacks.");
            }

            if (_runtimeEnabledField == null)
            {
                Logger.LogWarning(
                    "Could not resolve the parent runtime toggle field; toggle notifications are unavailable.");
            }

            if (_parentCurrentConfigProperty == null
                || _parentUseBudgetField == null
                || _parentMaximumUpgradedLightsField == null
                || _parentMaximumDistanceMetersField == null)
            {
                Logger.LogWarning(
                    "Could not resolve all parent config fields; optional combat budget and distance overrides are unavailable.");
            }
        }

        private void RefreshExcludedFragments()
        {
            List<string> fragments = new List<string>();
            AddExcludedFragments(BuiltInExcludedLightPathFragments, fragments);
            AddExcludedFragments(
                _additionalExcludedLightPathFragments != null
                    ? _additionalExcludedLightPathFragments.Value
                    : string.Empty,
                fragments);
            _excludedFragments = fragments.ToArray();
        }

        private static void AddExcludedFragments(
            string raw,
            List<string> fragments)
        {
            string[] parts = raw.Split(
                new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string fragment = parts[i].Trim();
                if (fragment.Length > 0
                    && !fragments.Exists(
                        item => string.Equals(
                            item,
                            fragment,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    fragments.Add(fragment);
                }
            }
        }

        private void OnAdditionalExcludedLightPathFragmentsChanged(
            object sender,
            EventArgs args)
        {
            RefreshExcludedFragments();
            _managedSettingsDirty = true;
            NudgeParentScan();
        }

        private void SubscribeCombatConfigEvents()
        {
            _combatPerformanceEnabled.SettingChanged += OnCombatSettingChanged;
            _outdoorCombatOnly.SettingChanged += OnCombatSettingChanged;
            _combatExitDelaySeconds.SettingChanged += OnCombatSettingChanged;
            _combatReduceAtlasResolution.SettingChanged += OnCombatSettingChanged;
            _combatShadowResolution.SettingChanged += OnCombatSettingChanged;
            _combatLimitLightBudget.SettingChanged += OnCombatSettingChanged;
            _combatMaximumUpgradedLights.SettingChanged += OnCombatSettingChanged;
            _combatLimitDistance.SettingChanged += OnCombatSettingChanged;
            _combatMaximumDistanceMeters.SettingChanged += OnCombatSettingChanged;
        }

        private void UnsubscribeCombatConfigEvents()
        {
            if (_combatPerformanceEnabled == null)
            {
                return;
            }

            _combatPerformanceEnabled.SettingChanged -= OnCombatSettingChanged;
            _outdoorCombatOnly.SettingChanged -= OnCombatSettingChanged;
            _combatExitDelaySeconds.SettingChanged -= OnCombatSettingChanged;
            _combatReduceAtlasResolution.SettingChanged -= OnCombatSettingChanged;
            _combatShadowResolution.SettingChanged -= OnCombatSettingChanged;
            _combatLimitLightBudget.SettingChanged -= OnCombatSettingChanged;
            _combatMaximumUpgradedLights.SettingChanged -= OnCombatSettingChanged;
            _combatLimitDistance.SettingChanged -= OnCombatSettingChanged;
            _combatMaximumDistanceMeters.SettingChanged -= OnCombatSettingChanged;
        }

        private void OnCombatSettingChanged(object sender, EventArgs args)
        {
            _nextCombatStateCheck = 0f;
            _lastAtlasDiagnosticSignature = string.Empty;
            if (!_combatPerformanceEnabled.Value)
            {
                SetCombatPerformanceActive(false);
            }
            else
            {
                RefreshCombatPerformanceState();
                if (!ShouldProtectShadowAtlas())
                {
                    RestoreAllShadowResolutions();
                }
            }

            NudgeParentScan();
        }

        private void RefreshCombatPerformanceState()
        {
            if (!_combatPerformanceEnabled.Value)
            {
                SetCombatPerformanceActive(false);
                return;
            }

            bool qualifies;
            if (!TryGetQualifyingCombatState(out qualifies))
            {
                return;
            }

            if (qualifies)
            {
                _combatExitEligibleAt = 0f;
                SetCombatPerformanceActive(true);
                return;
            }

            if (!_combatPerformanceActive)
            {
                _combatExitEligibleAt = 0f;
                return;
            }

            if (_combatExitEligibleAt <= 0f)
            {
                _combatExitEligibleAt =
                    Time.unscaledTime + _combatExitDelaySeconds.Value;
            }
            if (Time.unscaledTime >= _combatExitEligibleAt)
            {
                SetCombatPerformanceActive(false);
            }
        }

        private bool TryGetQualifyingCombatState(out bool qualifies)
        {
            qualifies = false;
            Hero hero = Hero.Current;
            if (hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || hero.HeroCombat == null
                || !hero.HeroCombat.IsHeroInFight)
            {
                return true;
            }

            if (!_outdoorCombatOnly.Value)
            {
                qualifies = true;
                return true;
            }

            if (World.Services == null)
            {
                return false;
            }

            SceneService sceneService = World.Services.TryGet<SceneService>();
            SceneLifetimeEvents lifetime = SceneLifetimeEvents.Get;
            if (sceneService == null
                || lifetime == null
                || !lifetime.EverythingInitialized)
            {
                return false;
            }

            qualifies = sceneService.IsOpenWorld && !lifetime.InInterior;
            return true;
        }

        private void SetCombatPerformanceActive(bool active)
        {
            if (_combatPerformanceActive == active)
            {
                return;
            }

            _combatPerformanceActive = active;
            _combatExitEligibleAt = 0f;
            _lastAtlasDiagnosticSignature = string.Empty;
            if (!active && !ShouldProtectShadowAtlas())
            {
                RestoreAllShadowResolutions();
            }

            NudgeParentScan();
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            string message = active
                ? "Combat shadow mode active: " + DescribeCombatOverrides() + "."
                : "Combat shadow mode ended; normal settings restored.";
            Logger.LogInfo(message);
            if (_showGrailFloatingTextDiagnostics != null
                && _showGrailFloatingTextDiagnostics.Value)
            {
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowSystemNotification(
                        PluginGuid,
                        active
                            ? "shadow-combat-mode-enabled"
                            : "shadow-combat-mode-disabled",
                        active
                            ? "Combat shadows: " + DescribeCombatOverrides() + "."
                            : "Combat shadows restored.",
                        "Normal",
                        "shadow-combat-mode");
            }
        }

        private string DescribeCombatOverrides()
        {
            List<string> parts = new List<string>();
            if (_combatReduceAtlasResolution.Value)
            {
                parts.Add(
                    "atlas "
                    + _combatShadowResolution.Value.ToString(
                        CultureInfo.InvariantCulture));
            }
            if (_combatLimitLightBudget.Value)
            {
                parts.Add(
                    "budget "
                    + _combatMaximumUpgradedLights.Value.ToString(
                        CultureInfo.InvariantCulture));
            }
            if (_combatLimitDistance.Value)
            {
                parts.Add(
                    "distance "
                    + _combatMaximumDistanceMeters.Value.ToString(
                        "0.#",
                        CultureInfo.InvariantCulture)
                    + "m");
            }
            return parts.Count > 0
                ? string.Join(", ", parts.ToArray())
                : "no performance overrides enabled";
        }

        private void OnShadowAtlasSettingChanged(object sender, EventArgs args)
        {
            _lastAtlasDiagnosticSignature = string.Empty;
            if (!ShouldProtectShadowAtlas())
            {
                RestoreAllShadowResolutions();
            }

            NudgeParentScan();
        }

        private bool ShouldProtectShadowAtlas()
        {
            return (_protectShadowAtlas != null && _protectShadowAtlas.Value)
                || (_combatPerformanceActive
                    && _combatReduceAtlasResolution != null
                    && _combatReduceAtlasResolution.Value);
        }

        private int CurrentShadowResolutionCap()
        {
            if (_combatPerformanceActive
                && _combatReduceAtlasResolution != null
                && _combatReduceAtlasResolution.Value)
            {
                return _protectShadowAtlas != null && _protectShadowAtlas.Value
                    ? Math.Min(
                        _promotedShadowResolution.Value,
                        _combatShadowResolution.Value)
                    : _combatShadowResolution.Value;
            }

            return _promotedShadowResolution.Value;
        }

        private void OnDiagnosticsSettingChanged(object sender, EventArgs args)
        {
            _atlasUnavailableReported = false;
            _combatParentConfigUnavailableReported = false;
            _lastAtlasDiagnosticSignature = string.Empty;
            NudgeParentScan();
        }

        private void ObserveParentRuntimeState(bool notifyChange)
        {
            if (_runtimeEnabledField == null)
            {
                return;
            }

            bool runtimeEnabled;
            try
            {
                runtimeEnabled = (bool)_runtimeEnabledField.GetValue(null);
            }
            catch (Exception exception)
            {
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogWarning(
                        "Could not read the parent shadow toggle state: "
                        + exception.Message);
                }

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
                        runtimeEnabled
                            ? "all-lights-shadows-enabled"
                            : "all-lights-shadows-disabled",
                        "All Lights Cast Shadows: "
                            + (runtimeEnabled ? "Enabled" : "Disabled"),
                        "Normal",
                        "all-lights-shadows-toggle");
            }
        }

        private void NudgeParentScan()
        {
            if (_parentManager == null || _nextScanTimeField == null)
            {
                return;
            }

            try
            {
                _nextScanTimeField.SetValue(_parentManager, 0f);
            }
            catch (Exception exception)
            {
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogWarning(
                        "Could not request an immediate parent light scan: "
                        + exception.Message);
                }
            }
        }

        private void ApplyCombatParentConfigOverrides()
        {
            RestoreCombatParentConfig();
            if (!_combatPerformanceActive
                || (!_combatLimitLightBudget.Value
                    && !_combatLimitDistance.Value))
            {
                return;
            }

            if (_parentCurrentConfigProperty == null
                || _parentUseBudgetField == null
                || _parentMaximumUpgradedLightsField == null
                || _parentMaximumDistanceMetersField == null)
            {
                ReportCombatParentConfigUnavailable(
                    "parent config reflection is incomplete");
                return;
            }

            try
            {
                object current = _parentCurrentConfigProperty.GetValue(
                    null,
                    null);
                if (current == null)
                {
                    ReportCombatParentConfigUnavailable(
                        "the parent current config is null");
                    return;
                }

                _combatParentConfigSnapshot = new CombatParentConfigSnapshot(
                    current,
                    (bool)_parentUseBudgetField.GetValue(current),
                    (int)_parentMaximumUpgradedLightsField.GetValue(current),
                    (float)_parentMaximumDistanceMetersField.GetValue(current));

                if (_combatLimitLightBudget.Value
                    || _combatLimitDistance.Value)
                {
                    _parentUseBudgetField.SetValue(current, true);
                }
                if (!_combatParentConfigSnapshot.UseBudget)
                {
                    if (!_combatLimitLightBudget.Value)
                    {
                        _parentMaximumUpgradedLightsField.SetValue(
                            current,
                            Int32.MaxValue);
                    }
                    if (!_combatLimitDistance.Value)
                    {
                        _parentMaximumDistanceMetersField.SetValue(
                            current,
                            Single.MaxValue);
                    }
                }
                if (_combatLimitLightBudget.Value)
                {
                    int original = _combatParentConfigSnapshot.MaximumUpgradedLights;
                    _parentMaximumUpgradedLightsField.SetValue(
                        current,
                        Math.Min(
                            original,
                            _combatMaximumUpgradedLights.Value));
                }
                if (_combatLimitDistance.Value)
                {
                    float original = _combatParentConfigSnapshot.MaximumDistanceMeters;
                    _parentMaximumDistanceMetersField.SetValue(
                        current,
                        Math.Min(
                            original,
                            _combatMaximumDistanceMeters.Value));
                }
            }
            catch (Exception exception)
            {
                ReportCombatParentConfigUnavailable(exception.Message);
                RestoreCombatParentConfig();
            }
        }

        private void RestoreCombatParentConfig()
        {
            CombatParentConfigSnapshot snapshot = _combatParentConfigSnapshot;
            _combatParentConfigSnapshot = null;
            if (snapshot == null)
            {
                return;
            }

            try
            {
                _parentUseBudgetField.SetValue(
                    snapshot.Target,
                    snapshot.UseBudget);
                _parentMaximumUpgradedLightsField.SetValue(
                    snapshot.Target,
                    snapshot.MaximumUpgradedLights);
                _parentMaximumDistanceMetersField.SetValue(
                    snapshot.Target,
                    snapshot.MaximumDistanceMeters);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not restore the parent config after a combat shadow scan: "
                    + exception.Message);
            }
        }

        private void ReportCombatParentConfigUnavailable(string reason)
        {
            if (_combatParentConfigUnavailableReported)
            {
                return;
            }

            _combatParentConfigUnavailableReported = true;
            Logger.LogWarning(
                "Optional combat budget and distance overrides are unavailable: "
                + reason
                + ". Combat atlas reduction remains independent.");
            if (_diagnostics != null
                && _diagnostics.Value
                && _showGrailFloatingTextDiagnostics != null
                && _showGrailFloatingTextDiagnostics.Value)
            {
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowSystemNotification(
                        PluginGuid,
                        "shadow-combat-parent-config-unavailable",
                        "Advanced combat shadow options unavailable; check the BepInEx log.",
                        "High",
                        "shadow-combat-mode");
            }
        }

        private void BeginAtlasScan()
        {
            _atlasScanLights.Clear();
            _atlasScanPointLights = 0;
            _atlasScanSpotLights = 0;
            _atlasScanOtherLights = 0;
            _atlasScanConstrainedLights = 0;
            _atlasScanEstimatedMaps = 0;
            _atlasScanRestoredLights = 0;
        }

        private void ApplyShadowAtlasProtection(Light light)
        {
            if (light == null
                || !ShouldProtectShadowAtlas())
            {
                return;
            }

            int id = light.GetInstanceID();
            if (!_atlasScanLights.Add(id))
            {
                return;
            }

            int shadowMapCount;
            if (light.type == LightType.Point)
            {
                _atlasScanPointLights++;
                shadowMapCount = 6;
            }
            else if (light.type == LightType.Spot)
            {
                _atlasScanSpotLights++;
                shadowMapCount = 1;
            }
            else
            {
                _atlasScanOtherLights++;
                return;
            }

            _atlasScanEstimatedMaps += shadowMapCount;

            try
            {
                ResolveHdrpMembers();
                if (_hdAdditionalLightDataType == null
                    || _hdShadowResolutionProperty == null
                    || _hdSetShadowResolutionMethod == null
                    || _hdSetShadowResolutionLevelMethod == null
                    || _hdSetShadowResolutionOverrideMethod == null)
                {
                    ReportAtlasUnavailable(
                        "HDRP shadow-resolution members could not be resolved.");
                    return;
                }

                Component hd = light.GetComponent(_hdAdditionalLightDataType);
                if (hd == null)
                {
                    if (_diagnostics != null && _diagnostics.Value)
                    {
                        Logger.LogInfo(
                            "Shadow atlas skipped a promoted light without HDAdditionalLightData: "
                            + GetTransformPath(light.transform)
                            + ".");
                    }

                    return;
                }

                ShadowResolutionState state;
                if (!_shadowResolutionStates.TryGetValue(id, out state))
                {
                    object resolution = _hdShadowResolutionProperty.GetValue(
                        hd,
                        null);
                    int originalOverride;
                    int originalLevel;
                    bool originalUseOverride;
                    if (resolution == null
                        || !TryReadRuntimeMember(
                            resolution,
                            "override",
                            out originalOverride)
                        || !TryReadRuntimeMember(
                            resolution,
                            "level",
                            out originalLevel)
                        || !TryReadRuntimeMember(
                            resolution,
                            "useOverride",
                            out originalUseOverride))
                    {
                        ReportAtlasUnavailable(
                            "HDRP shadow-resolution state could not be read.");
                        return;
                    }

                    int cap = CurrentShadowResolutionCap();
                    if (originalUseOverride && originalOverride <= cap)
                    {
                        return;
                    }

                    state = new ShadowResolutionState(
                        light,
                        hd,
                        originalOverride,
                        originalLevel,
                        originalUseOverride);
                    _shadowResolutionStates.Add(id, state);

                    if (_diagnostics != null && _diagnostics.Value)
                    {
                        Logger.LogInfo(
                            "Shadow atlas captured "
                            + GetTransformPath(light.transform)
                            + ": type="
                            + light.type
                            + ", maps="
                            + shadowMapCount.ToString(CultureInfo.InvariantCulture)
                            + ", originalOverride="
                            + originalOverride.ToString(CultureInfo.InvariantCulture)
                            + ", originalLevel="
                            + originalLevel.ToString(CultureInfo.InvariantCulture)
                            + ", originalUseOverride="
                            + originalUseOverride
                            + ".");
                    }
                }

                int targetResolution = state.OriginalUseOverride
                    ? Math.Min(
                        state.OriginalOverride,
                        CurrentShadowResolutionCap())
                    : CurrentShadowResolutionCap();
                _hdSetShadowResolutionMethod.Invoke(
                    state.HdData,
                    new object[] { targetResolution });
                _hdSetShadowResolutionOverrideMethod.Invoke(
                    state.HdData,
                    new object[] { true });
                _atlasScanConstrainedLights++;
            }
            catch (Exception exception)
            {
                ReportAtlasUnavailable(
                    "Could not constrain "
                    + GetTransformPath(light.transform)
                    + ": "
                    + exception.Message);
            }
        }

        private void RestoreInactiveShadowResolutions()
        {
            if (_shadowResolutionStates.Count == 0)
            {
                return;
            }

            HashSet<int> activeLights = GetActiveLights();
            List<int> ids = new List<int>(_shadowResolutionStates.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                ShadowResolutionState state = _shadowResolutionStates[id];
                if (state.Light != null
                    && activeLights != null
                    && activeLights.Contains(id))
                {
                    continue;
                }

                RestoreShadowResolution(state);
                _shadowResolutionStates.Remove(id);
                _atlasScanRestoredLights++;
            }
        }

        private void RestoreAllShadowResolutions()
        {
            if (_shadowResolutionStates.Count == 0)
            {
                return;
            }

            foreach (ShadowResolutionState state in _shadowResolutionStates.Values)
            {
                RestoreShadowResolution(state);
            }

            int restored = _shadowResolutionStates.Count;
            _shadowResolutionStates.Clear();
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(
                    "Restored original HDRP shadow resolution for "
                    + restored.ToString(CultureInfo.InvariantCulture)
                    + " promoted light(s).");
            }
        }

        private void RestoreShadowResolution(ShadowResolutionState state)
        {
            if (state == null || state.Light == null || state.HdData == null)
            {
                return;
            }

            try
            {
                _hdSetShadowResolutionMethod.Invoke(
                    state.HdData,
                    new object[] { state.OriginalOverride });
                _hdSetShadowResolutionLevelMethod.Invoke(
                    state.HdData,
                    new object[] { state.OriginalLevel });
                _hdSetShadowResolutionOverrideMethod.Invoke(
                    state.HdData,
                    new object[] { state.OriginalUseOverride });
            }
            catch (Exception exception)
            {
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogWarning(
                        "Could not restore an HDRP shadow-resolution state: "
                        + exception.Message);
                }
            }
        }

        private void ReportAtlasDiagnostics()
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            HashSet<int> activeLights = GetActiveLights();
            int activeCount = activeLights != null ? activeLights.Count : 0;
            int punctualCount = _atlasScanPointLights + _atlasScanSpotLights;
            int cap = CurrentShadowResolutionCap();
            string summary =
                "Shadow atlas scan: active="
                + activeCount.ToString(CultureInfo.InvariantCulture)
                + ", punctualSeen="
                + punctualCount.ToString(CultureInfo.InvariantCulture)
                + " (point="
                + _atlasScanPointLights.ToString(CultureInfo.InvariantCulture)
                + ", spot="
                + _atlasScanSpotLights.ToString(CultureInfo.InvariantCulture)
                + ", other="
                + _atlasScanOtherLights.ToString(CultureInfo.InvariantCulture)
                + "), estimatedMaps="
                + _atlasScanEstimatedMaps.ToString(CultureInfo.InvariantCulture)
                + ", constrained="
                + _atlasScanConstrainedLights.ToString(CultureInfo.InvariantCulture)
                + ", tracked="
                + TrackedShadowResolutionCount().ToString(CultureInfo.InvariantCulture)
                + ", restored="
                + _atlasScanRestoredLights.ToString(CultureInfo.InvariantCulture)
                + ", cap="
                + cap.ToString(CultureInfo.InvariantCulture)
                + ", combat="
                + _combatPerformanceActive
                + ".";
            Logger.LogInfo(summary);

            if (!IsActiveGameplaySession())
            {
                _lastAtlasDiagnosticSignature = string.Empty;
                return;
            }

            if (_showGrailFloatingTextDiagnostics == null
                || !_showGrailFloatingTextDiagnostics.Value)
            {
                return;
            }

            string signature =
                activeCount.ToString(CultureInfo.InvariantCulture)
                + "|"
                + _atlasScanPointLights.ToString(CultureInfo.InvariantCulture)
                + "|"
                + _atlasScanSpotLights.ToString(CultureInfo.InvariantCulture)
                + "|"
                + _atlasScanEstimatedMaps.ToString(CultureInfo.InvariantCulture)
                + "|"
                + _atlasScanConstrainedLights.ToString(CultureInfo.InvariantCulture)
                + "|"
                + TrackedShadowResolutionCount().ToString(CultureInfo.InvariantCulture)
                + "|"
                + cap.ToString(CultureInfo.InvariantCulture)
                + "|"
                + _combatPerformanceActive;
            if (string.Equals(
                signature,
                _lastAtlasDiagnosticSignature,
                StringComparison.Ordinal))
            {
                return;
            }

            _lastAtlasDiagnosticSignature = signature;
            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowSystemNotification(
                    PluginGuid,
                    "shadow-atlas-diagnostics",
                    "Shadow atlas guard: "
                        + _atlasScanConstrainedLights.ToString(
                            CultureInfo.InvariantCulture)
                        + "/"
                        + punctualCount.ToString(CultureInfo.InvariantCulture)
                        + " lights constrained; "
                        + _atlasScanEstimatedMaps.ToString(
                            CultureInfo.InvariantCulture)
                        + " shadow maps.",
                    "Normal",
                    "shadow-atlas-diagnostics");
        }

        private static bool IsActiveGameplaySession()
        {
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
            return sceneService != null
                && sceneService.ActiveSceneRef != null
                && !String.IsNullOrEmpty(sceneService.ActiveSceneRef.Name)
                && lifetime != null
                && lifetime.EverythingInitialized;
        }

        private void ReportAtlasUnavailable(string reason)
        {
            if (_atlasUnavailableReported)
            {
                return;
            }

            _atlasUnavailableReported = true;
            Logger.LogWarning("Shadow atlas protection is unavailable: " + reason);
            if (_diagnostics != null
                && _diagnostics.Value
                && _showGrailFloatingTextDiagnostics != null
                && _showGrailFloatingTextDiagnostics.Value)
            {
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowSystemNotification(
                        PluginGuid,
                        "shadow-atlas-unavailable",
                        "Shadow atlas guard unavailable; check the BepInEx log.",
                        "High",
                        "shadow-atlas-diagnostics");
            }
        }

        private static bool TryReadRuntimeMember<T>(
            object target,
            string name,
            out T value)
        {
            value = default(T);
            BindingFlags flags =
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(name, flags);
            object raw = property != null
                ? property.GetValue(target, null)
                : null;
            if (property == null)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field == null)
                {
                    return false;
                }

                raw = field.GetValue(target);
            }

            if (raw is T)
            {
                value = (T)raw;
                return true;
            }

            try
            {
                value = (T)Convert.ChangeType(
                    raw,
                    typeof(T),
                    CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ProtectExcludedLightsBeforeParentScan()
        {
            if (!_protectBonfireLights.Value || _excludedFragments.Length == 0)
            {
                return;
            }

            try
            {
                Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsSortMode.None);
                IDictionary originalStates = GetOriginalStates();
                for (int i = 0; i < lights.Length; i++)
                {
                    Light light = lights[i];
                    if (light == null || !ShouldExcludeLight(light))
                    {
                        continue;
                    }

                    int id = light.GetInstanceID();
                    if (!_protectedLights.ContainsKey(id))
                    {
                        _protectedLights[id] = CreateProtectedLightState(
                            light,
                            id,
                            originalStates);
                    }

                    RemoveParentTracking(id);

                    if (light.shadows == LightShadows.None)
                    {
                        light.shadows = LightShadows.Soft;
                    }

                    LogExcludedLightOnce(light, "protected");
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Excluded-light protection failed before parent scan: "
                    + exception.Message);
            }
        }

        private void RestoreProtectedLightsAfterParentScan()
        {
            if (_protectedLights.Count == 0)
            {
                return;
            }

            foreach (ProtectedLightState state in _protectedLights.Values)
            {
                if (state.Light == null)
                {
                    continue;
                }

                state.Light.shadows = state.Shadows;
                state.Light.shadowStrength = state.ShadowStrength;
                RestoreHdrpShadowState(
                    state.Light,
                    state.Shadows,
                    state.ShadowStrength);
            }

            _protectedLights.Clear();
        }

        private void RestoreExcludedLightsTouchedByParent()
        {
            if (!_protectBonfireLights.Value || _excludedFragments.Length == 0)
            {
                return;
            }

            HashSet<int> activeLights = GetActiveLights();
            if (activeLights == null || activeLights.Count == 0)
            {
                return;
            }

            IDictionary originalStates = GetOriginalStates();
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null)
                {
                    continue;
                }

                int id = light.GetInstanceID();
                if (!activeLights.Contains(id) || !ShouldExcludeLight(light))
                {
                    continue;
                }

                RestoreOriginalLightState(light, id, originalStates);
                activeLights.Remove(id);
                LogExcludedLightOnce(light, "restored");
            }
        }

        private bool ShouldExcludeLight(Light light)
        {
            Transform current = light.transform;
            while (current != null)
            {
                string name = current.name;
                for (int i = 0; i < _excludedFragments.Length; i++)
                {
                    if (name.IndexOf(
                            _excludedFragments[i],
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        private void RestoreOriginalLightState(
            Light light,
            int id,
            IDictionary originalStates)
        {
            LightShadows shadows = LightShadows.None;
            float shadowStrength = light.shadowStrength;

            if (originalStates != null && originalStates.Contains(id))
            {
                object originalState = originalStates[id];
                if (_originalShadowsField != null)
                {
                    object value = _originalShadowsField.GetValue(originalState);
                    if (value is LightShadows)
                    {
                        shadows = (LightShadows)value;
                    }
                }

                if (_originalShadowStrengthField != null)
                {
                    object value =
                        _originalShadowStrengthField.GetValue(originalState);
                    if (value is float)
                    {
                        shadowStrength = (float)value;
                    }
                }
            }

            light.shadows = shadows;
            light.shadowStrength = shadowStrength;
            RestoreHdrpShadowState(light, shadows, shadowStrength);
        }

        private ProtectedLightState CreateProtectedLightState(
            Light light,
            int id,
            IDictionary originalStates)
        {
            LightShadows shadows = light.shadows;
            float shadowStrength = light.shadowStrength;
            TryReadOriginalLightState(
                id,
                originalStates,
                ref shadows,
                ref shadowStrength);
            return new ProtectedLightState(light, shadows, shadowStrength);
        }

        private bool TryReadOriginalLightState(
            int id,
            IDictionary originalStates,
            ref LightShadows shadows,
            ref float shadowStrength)
        {
            if (originalStates == null || !originalStates.Contains(id))
            {
                return false;
            }

            object originalState = originalStates[id];
            if (_originalShadowsField != null)
            {
                object value = _originalShadowsField.GetValue(originalState);
                if (value is LightShadows)
                {
                    shadows = (LightShadows)value;
                }
            }

            if (_originalShadowStrengthField != null)
            {
                object value =
                    _originalShadowStrengthField.GetValue(originalState);
                if (value is float)
                {
                    shadowStrength = (float)value;
                }
            }

            return true;
        }

        private void RestoreHdrpShadowState(
            Light light,
            LightShadows shadows,
            float shadowStrength)
        {
            try
            {
                ResolveHdrpMembers();
                if (_hdAdditionalLightDataType == null)
                {
                    return;
                }

                Component hd = light.GetComponent(_hdAdditionalLightDataType);
                if (hd == null)
                {
                    return;
                }

                bool enabled = shadows != LightShadows.None;
                float dimmer = enabled ? shadowStrength : 0f;
                if (_hdEnableShadowsMethod != null)
                {
                    _hdEnableShadowsMethod.Invoke(
                        hd,
                        new object[] { enabled });
                }

                SetFloatMember(_hdShadowDimmerMember, hd, dimmer);
                SetFloatMember(_hdVolumetricShadowDimmerMember, hd, dimmer);
            }
            catch (Exception exception)
            {
                if (_verboseExclusionLogging.Value)
                {
                    Logger.LogWarning(
                        "HDRP excluded-light restore failed for "
                        + light.name
                        + ": "
                        + exception.Message);
                }
            }
        }

        private void ResolveHdrpMembers()
        {
            if (_hdrpResolved)
            {
                return;
            }

            BindingFlags flags =
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
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
                _hdrpResolved = true;
                _hdEnableShadowsMethod = type.GetMethod(
                    "EnableShadows",
                    flags,
                    null,
                    new[] { typeof(bool) },
                    null);
                _hdShadowDimmerMember = FindFloatMember(
                    type,
                    flags,
                    new[] { "shadowDimmer", "m_ShadowDimmer", "shadowIntensity" });
                _hdVolumetricShadowDimmerMember = FindFloatMember(
                    type,
                    flags,
                    new[] { "volumetricShadowDimmer", "m_VolumetricShadowDimmer" });
                _hdShadowResolutionProperty = type.GetProperty(
                    "shadowResolution",
                    flags);
                _hdSetShadowResolutionMethod = type.GetMethod(
                    "SetShadowResolution",
                    flags,
                    null,
                    new[] { typeof(int) },
                    null);
                _hdSetShadowResolutionLevelMethod = type.GetMethod(
                    "SetShadowResolutionLevel",
                    flags,
                    null,
                    new[] { typeof(int) },
                    null);
                _hdSetShadowResolutionOverrideMethod = type.GetMethod(
                    "SetShadowResolutionOverride",
                    flags,
                    null,
                    new[] { typeof(bool) },
                    null);
                return;
            }
        }

        private static MemberInfo FindFloatMember(
            Type type,
            BindingFlags flags,
            string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo property = type.GetProperty(names[i], flags);
                if (property != null
                    && property.CanWrite
                    && property.PropertyType == typeof(float))
                {
                    return property;
                }

                FieldInfo field = type.GetField(names[i], flags);
                if (field != null && field.FieldType == typeof(float))
                {
                    return field;
                }
            }

            return null;
        }

        private static void SetFloatMember(
            MemberInfo member,
            object target,
            float value)
        {
            PropertyInfo property = member as PropertyInfo;
            if (property != null)
            {
                property.SetValue(target, value, null);
                return;
            }

            FieldInfo field = member as FieldInfo;
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private void RemoveParentTracking(int id)
        {
            HashSet<int> activeLights = GetActiveLights();
            if (activeLights != null)
            {
                activeLights.Remove(id);
            }

            IDictionary originalStates = GetOriginalStates();
            if (originalStates != null && originalStates.Contains(id))
            {
                originalStates.Remove(id);
            }
        }

        private HashSet<int> GetActiveLights()
        {
            return _activeLightsField != null
                ? _activeLightsField.GetValue(null) as HashSet<int>
                : null;
        }

        private IDictionary GetOriginalStates()
        {
            return _originalStatesField != null
                ? _originalStatesField.GetValue(null) as IDictionary
                : null;
        }

        private void LogExcludedLightOnce(Light light, string action)
        {
            if (!_verboseExclusionLogging.Value)
            {
                return;
            }

            int id = light.GetInstanceID();
            if (!_loggedExcludedLights.Add(id))
            {
                return;
            }

            Logger.LogInfo(
                "Excluded light "
                + action
                + ": "
                + GetTransformPath(light.transform));
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
            return string.Join("/", names.ToArray());
        }

        private void OnDestroy()
        {
            UnsubscribeCombatConfigEvents();
            UnsubscribeManagedShadowConfigEvents();
            UnsubscribeDawnDuskShadowConfigEvents();
            if (_additionalExcludedLightPathFragments != null)
            {
                _additionalExcludedLightPathFragments.SettingChanged -=
                    OnAdditionalExcludedLightPathFragmentsChanged;
            }

            if (_protectShadowAtlas != null)
            {
                _protectShadowAtlas.SettingChanged -=
                    OnShadowAtlasSettingChanged;
            }

            if (_promotedShadowResolution != null)
            {
                _promotedShadowResolution.SettingChanged -=
                    OnShadowAtlasSettingChanged;
            }

            if (_diagnostics != null)
            {
                _diagnostics.SettingChanged -=
                    OnDiagnosticsSettingChanged;
            }

            RestoreProtectedLightsAfterParentScan();
            RestoreCombatParentConfig();
            RestoreAllShadowResolutions();
            RestoreAllManagedLights("addon unload");
            RestoreAllDawnDuskShadowSystems("addon unload");

            if (_originalShadowQualityCaptured)
            {
                QualitySettings.shadows = _originalShadowQuality;
                _originalShadowQualityCaptured = false;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private sealed class ProtectedLightState
        {
            internal readonly Light Light;
            internal readonly LightShadows Shadows;
            internal readonly float ShadowStrength;

            internal ProtectedLightState(
                Light light,
                LightShadows shadows,
                float shadowStrength)
            {
                Light = light;
                Shadows = shadows;
                ShadowStrength = shadowStrength;
            }
        }

        private sealed class ShadowResolutionState
        {
            internal readonly Light Light;
            internal readonly Component HdData;
            internal readonly int OriginalOverride;
            internal readonly int OriginalLevel;
            internal readonly bool OriginalUseOverride;

            internal ShadowResolutionState(
                Light light,
                Component hdData,
                int originalOverride,
                int originalLevel,
                bool originalUseOverride)
            {
                Light = light;
                HdData = hdData;
                OriginalOverride = originalOverride;
                OriginalLevel = originalLevel;
                OriginalUseOverride = originalUseOverride;
            }
        }

        private sealed class CombatParentConfigSnapshot
        {
            internal readonly object Target;
            internal readonly bool UseBudget;
            internal readonly int MaximumUpgradedLights;
            internal readonly float MaximumDistanceMeters;

            internal CombatParentConfigSnapshot(
                object target,
                bool useBudget,
                int maximumUpgradedLights,
                float maximumDistanceMeters)
            {
                Target = target;
                UseBudget = useBudget;
                MaximumUpgradedLights = maximumUpgradedLights;
                MaximumDistanceMeters = maximumDistanceMeters;
            }
        }
    }

    internal static class Patches
    {
        internal static bool BeforeApplyAllLights(string __0)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                return plugin.BeforeApplyAllLights(__0);
            }
            return true;
        }

        internal static void AfterApplyAllLights()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.AfterApplyAllLights();
            }
        }

        internal static Exception FinalizeApplyAllLights(
            Exception __exception)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.FinalizeApplyAllLights();
            }

            return __exception;
        }

        internal static void AfterRestoreAllLights()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.AfterRestoreAllLights();
            }
        }

        internal static bool BeforeRestoreAllLights(string __0)
        {
            Plugin plugin = Plugin.Instance;
            return plugin == null || plugin.BeforeParentRestore(__0);
        }

        internal static void AfterShadowManagerUpdate(object __instance)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.AfterShadowManagerUpdate(__instance);
            }
        }

        internal static void BeforeParentSceneCooldown(string __0)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.BeforeParentSceneCooldown(__0);
            }
        }

        internal static void BeforeHdrpShadowRefresh(Light __0)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.BeforeHdrpShadowRefresh(__0);
            }
        }
    }
}
