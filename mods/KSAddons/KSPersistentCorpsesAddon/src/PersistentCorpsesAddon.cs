using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.Kandra;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes.Resting;
using Awaken.TG.Main.Locations;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Persistent Corpses Addon")]
[assembly: AssemblyDescription("Improves restored Persistent Corpses ragdolls and limits loaded full corpses")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Persistent Corpses Addon")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

namespace Keenan.TGFoA.PersistentCorpsesAddon
{
    public enum CorpseRetentionMode
    {
        All,
        Limited,
        Vanilla
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        ParentPluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class PersistentCorpsesAddonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.persistent-corpses-addon";
        public const string PluginName = "PersistentCorpses Addon";
        public const string PluginVersion = "1.1.1";
        public const string ParentPluginGuid =
            "VirusAlex.PersistentCorpses";

        private const int ConfigSchemaVersion = 3;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const float DefaultMinimumSettleSeconds = 0.75f;
        private const float DefaultMaximumSettleSeconds = 2.0f;
        private const float MinimumAllowedSettleSeconds = 0.1f;
        private const float MaximumAllowedSettleSeconds = 10.0f;
        private const int DefaultMaximumLoadedFullCorpses = 10;
        private const int MinimumAllowedLoadedFullCorpses = 0;
        private const int MaximumAllowedLoadedFullCorpses = 100;
        private const int DefaultMinimumRestHoursForCleanup = 6;
        private const int MinimumAllowedRestHoursForCleanup = 1;
        private const int MaximumAllowedRestHoursForCleanup = 24;

        internal static PersistentCorpsesAddonPlugin Instance { get; private set; }

        private readonly HashSet<CorpseSettleController> _activeControllers =
            new HashSet<CorpseSettleController>();

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _minimumSettleSeconds;
        private ConfigEntry<float> _maximumSettleSeconds;
        private ConfigEntry<CorpseRetentionMode> _retentionMode;
        private ConfigEntry<int> _maximumLoadedFullCorpses;
        private ConfigEntry<bool> _cleanupAfterLongBonfireRest;
        private ConfigEntry<int> _minimumRestHoursForCleanup;
        private ConfigEntry<bool> _diagnostics;

        private bool _hasPendingEnabled;
        private bool _pendingEnabled;
        private bool _hasPendingMinimumSettleSeconds;
        private float _pendingMinimumSettleSeconds;
        private bool _hasPendingMaximumSettleSeconds;
        private float _pendingMaximumSettleSeconds;
        private bool _hasPendingCleanupAfterLongBonfireRest;
        private bool _pendingCleanupAfterLongBonfireRest;
        private bool _hasPendingMinimumRestHoursForCleanup;
        private int _pendingMinimumRestHoursForCleanup;
        private bool _hasPendingDiagnostics;
        private bool _pendingDiagnostics;

        private FieldInfo _isRestoringField;
        private FieldInfo _fromAttachmentField;
        private MethodInfo _tryCreateReplacementDeadBodyMethod;
        private Coroutine _cleanupCoroutine;
        private bool _pendingBonfireRest;
        private Harmony _harmony;

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

                _isRestoringField = AccessTools.Field(
                    typeof(NpcDummy),
                    "_isRestoring");
                _fromAttachmentField = AccessTools.Field(
                    typeof(NpcDummy),
                    "_fromAttachment");
                MethodInfo afterVisualLoadedMethod = AccessTools.Method(
                    typeof(NpcDummy),
                    "AfterVisualLoaded",
                    new[] { typeof(Transform) });
                MethodInfo tryReplaceWithSimplifiedLocationMethod =
                    AccessTools.Method(
                        typeof(NpcDummy),
                        nameof(NpcDummy.TryReplaceWithSimplifiedLocation));
                _tryCreateReplacementDeadBodyMethod = AccessTools.Method(
                    typeof(NpcDummy),
                    "TryCreateReplacementDeadBody");
                MethodInfo restMethod = AccessTools.Method(
                    typeof(RestPopupUI),
                    nameof(RestPopupUI.Rest));
                MethodInfo skipWeatherTimeMethod = AccessTools.Method(
                    typeof(RestPopupUI),
                    "SkipWeatherTime");

                if (_isRestoringField == null
                    || _fromAttachmentField == null
                    || afterVisualLoadedMethod == null
                    || tryReplaceWithSimplifiedLocationMethod == null
                    || _tryCreateReplacementDeadBodyMethod == null
                    || restMethod == null
                    || skipWeatherTimeMethod == null)
                {
                    throw new MissingMemberException(
                        "Could not resolve the corpse-restoration or rest members.");
                }

                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(
                    afterVisualLoadedMethod,
                    prefix: new HarmonyMethod(
                        typeof(NpcDummyRestorePatch),
                        nameof(NpcDummyRestorePatch.BeforeAfterVisualLoaded)));
                _harmony.Patch(
                    tryReplaceWithSimplifiedLocationMethod,
                    postfix: new HarmonyMethod(
                        typeof(NpcDummySimplificationPatch),
                        nameof(NpcDummySimplificationPatch.AfterTryReplaceWithSimplifiedLocation)));
                _harmony.Patch(
                    restMethod,
                    prefix: new HarmonyMethod(
                        typeof(RestPopupPatch),
                        nameof(RestPopupPatch.BeforeRest)));
                _harmony.Patch(
                    skipWeatherTimeMethod,
                    postfix: new HarmonyMethod(
                        typeof(RestPopupPatch),
                        nameof(RestPopupPatch.AfterSkipWeatherTime)));

                Config.Save();
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Restored ragdolls will remain concealed for "
                    + GetMinimumSettleSeconds().ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " to "
                    + GetMaximumSettleSeconds().ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " seconds of physics simulation. Retention mode: "
                    + GetRetentionMode().ToString()
                    + ".");
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

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (_cleanupCoroutine != null)
            {
                StopCoroutine(_cleanupCoroutine);
                _cleanupCoroutine = null;
            }

            List<CorpseSettleController> controllers =
                new List<CorpseSettleController>(_activeControllers);
            foreach (CorpseSettleController controller in controllers)
            {
                if (controller != null)
                {
                    controller.RevealNow("addon unloading");
                }
            }

            _activeControllers.Clear();
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
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
                    "Master switch. When disabled, restored corpses use the original visible restoration behavior.",
                    "General", "Enabled", 0, 0));
            _minimumSettleSeconds = Config.Bind(
                "Settle Timing",
                "MinimumSettleSeconds",
                DefaultMinimumSettleSeconds,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Minimum amount of active ragdoll physics time to conceal a restored corpse before it can be revealed.",
                    "Settle Timing", "Minimum Settle Time", 10, 0,
                    new AcceptableValueRange<float>(
                        MinimumAllowedSettleSeconds,
                        MaximumAllowedSettleSeconds)));
            _maximumSettleSeconds = Config.Bind(
                "Settle Timing",
                "MaximumSettleSeconds",
                DefaultMaximumSettleSeconds,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum amount of active physics time to conceal a restored corpse, including bodies that keep moving on slopes.",
                    "Settle Timing", "Maximum Settle Time", 10, 10,
                    new AcceptableValueRange<float>(
                        MinimumAllowedSettleSeconds,
                        MaximumAllowedSettleSeconds)));
            _retentionMode = Config.Bind(
                "Corpse Retention",
                "RetentionMode",
                CorpseRetentionMode.Limited,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "All keeps every corpse through Persistent Corpses. Limited keeps up to MaximumLoadedFullCorpses and lets excess distant corpses use vanilla cleanup. Vanilla lets every eligible distant corpse use vanilla cleanup.",
                    "Corpse Retention", "Retention Mode", 20, 0));
            _maximumLoadedFullCorpses = Config.Bind(
                "Corpse Retention",
                "MaximumLoadedFullCorpses",
                DefaultMaximumLoadedFullCorpses,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum number of full loaded corpses retained in Limited mode. Excess bodies simplify only when the game already considers them distant enough for vanilla ragdoll cleanup.",
                    "Corpse Retention", "Maximum Loaded Full Corpses", 20, 10,
                    new AcceptableValueRange<int>(
                        MinimumAllowedLoadedFullCorpses,
                        MaximumAllowedLoadedFullCorpses)));
            _cleanupAfterLongBonfireRest = Config.Bind(
                "Bonfire Cleanup",
                "CleanupAfterLongBonfireRest",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Replace loaded full corpses with the game's lightweight loot-preserving bodies after a sufficiently long bonfire rest. Empty corpses are removed.",
                    "Bonfire Cleanup", "Cleanup After Long Bonfire Rest", 30, 0));
            _minimumRestHoursForCleanup = Config.Bind(
                "Bonfire Cleanup",
                "MinimumRestHoursForCleanup",
                DefaultMinimumRestHoursForCleanup,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Minimum actual bonfire-rest duration required to clean up loaded corpses.",
                    "Bonfire Cleanup", "Minimum Rest Hours", 30, 10,
                    new AcceptableValueRange<int>(
                        MinimumAllowedRestHoursForCleanup,
                        MaximumAllowedRestHoursForCleanup)));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Log restored-corpse concealment and reveal details.",
                    "Diagnostics", "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 0));

            RestorePreservedSettings();
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
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                const string schemaPrefix = "ConfigSchemaVersion =";
                if (!line.StartsWith(
                    schemaPrefix,
                    StringComparison.Ordinal))
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
                        "Could not restore the previous Persistent Corpses Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Persistent Corpses Addon config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedSettings(
            string configPath,
            int storedSchemaVersion)
        {
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            _hasPendingEnabled = profile.TryGetCustomizedValue(
                "General", "Enabled", out _pendingEnabled);
            _hasPendingMinimumSettleSeconds =
                profile.TryGetCustomizedValue(
                    "Settle Timing",
                    "MinimumSettleSeconds",
                    out _pendingMinimumSettleSeconds);
            _hasPendingMaximumSettleSeconds =
                profile.TryGetCustomizedValue(
                    "Settle Timing",
                    "MaximumSettleSeconds",
                    out _pendingMaximumSettleSeconds);
            _hasPendingCleanupAfterLongBonfireRest =
                profile.TryGetCustomizedValue(
                    "Bonfire Cleanup",
                    "CleanupAfterLongBonfireRest",
                    out _pendingCleanupAfterLongBonfireRest);
            _hasPendingMinimumRestHoursForCleanup =
                profile.TryGetCustomizedValue(
                    "Bonfire Cleanup",
                    "MinimumRestHoursForCleanup",
                    out _pendingMinimumRestHoursForCleanup);
            _hasPendingDiagnostics = profile.TryGetCustomizedValue(
                "Diagnostics", "Diagnostics", out _pendingDiagnostics);
        }

        private void RestorePreservedSettings()
        {
            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreserved(
                _hasPendingEnabled,
                _enabled,
                _pendingEnabled,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingMinimumSettleSeconds,
                _minimumSettleSeconds,
                _pendingMinimumSettleSeconds,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingMaximumSettleSeconds,
                _maximumSettleSeconds,
                _pendingMaximumSettleSeconds,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingCleanupAfterLongBonfireRest,
                _cleanupAfterLongBonfireRest,
                _pendingCleanupAfterLongBonfireRest,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingMinimumRestHoursForCleanup,
                _minimumRestHoursForCleanup,
                _pendingMinimumRestHoursForCleanup,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingDiagnostics,
                _diagnostics,
                _pendingDiagnostics,
                ref restoredCount,
                ref clampedCount);

            if (restoredCount > 0)
            {
                Logger.LogInfo(
                    "Preserved "
                    + restoredCount.ToString(
                        CultureInfo.InvariantCulture)
                    + " customized corpse setting(s) across the config schema reset; clamped="
                    + clampedCount.ToString(
                        CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static void RestorePreserved<T>(
            bool hasPendingValue,
            ConfigEntry<T> entry,
            T pendingValue,
            ref int restoredCount,
            ref int clampedCount)
        {
            if (!hasPendingValue)
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    entry,
                    pendingValue,
                    out clamped))
            {
                return;
            }

            restoredCount++;
            if (clamped)
            {
                clampedCount++;
            }
        }

        internal bool IsFeatureEnabled
        {
            get
            {
                return _enabled != null && _enabled.Value;
            }
        }

        internal float GetMinimumSettleSeconds()
        {
            if (_minimumSettleSeconds == null)
            {
                return DefaultMinimumSettleSeconds;
            }

            return Mathf.Clamp(
                _minimumSettleSeconds.Value,
                MinimumAllowedSettleSeconds,
                MaximumAllowedSettleSeconds);
        }

        internal float GetMaximumSettleSeconds()
        {
            float configuredMaximum = _maximumSettleSeconds == null
                ? DefaultMaximumSettleSeconds
                : Mathf.Clamp(
                    _maximumSettleSeconds.Value,
                    MinimumAllowedSettleSeconds,
                    MaximumAllowedSettleSeconds);
            return Mathf.Max(
                GetMinimumSettleSeconds(),
                configuredMaximum);
        }

        private CorpseRetentionMode GetRetentionMode()
        {
            return _retentionMode == null
                ? CorpseRetentionMode.Limited
                : _retentionMode.Value;
        }

        private int GetMaximumLoadedFullCorpses()
        {
            if (_maximumLoadedFullCorpses == null)
            {
                return DefaultMaximumLoadedFullCorpses;
            }

            return Mathf.Clamp(
                _maximumLoadedFullCorpses.Value,
                MinimumAllowedLoadedFullCorpses,
                MaximumAllowedLoadedFullCorpses);
        }

        internal void ApplyRetentionPolicyAfterVanillaAttempt(
            NpcDummy dummy,
            ref bool simplified)
        {
            if (simplified
                || !IsFeatureEnabled
                || dummy == null
                || dummy.HasBeenDiscarded
                || !dummy.HasDied
                || dummy.ParentTransform == null)
            {
                return;
            }

            CorpseRetentionMode mode = GetRetentionMode();
            if (mode == CorpseRetentionMode.All)
            {
                return;
            }

            int maximumLoadedFullCorpses = GetMaximumLoadedFullCorpses();
            if (mode == CorpseRetentionMode.Limited
                && !LoadedFullCorpseLimitExceeded(
                    maximumLoadedFullCorpses))
            {
                return;
            }

            bool failed;
            simplified = TrySimplifyCorpse(
                dummy,
                "for the " + mode.ToString() + " retention policy",
                out failed);
            if (failed)
            {
                return;
            }

            if (simplified)
            {
                string detail = mode == CorpseRetentionMode.Limited
                    ? "; the configured loaded full-corpse limit is "
                        + maximumLoadedFullCorpses.ToString(
                            CultureInfo.InvariantCulture)
                    : string.Empty;
                LogDiagnostic(
                    "Simplified a distant corpse for the "
                    + mode.ToString()
                    + " retention policy"
                    + detail
                    + ".");
            }
        }

        private bool LoadedFullCorpseLimitExceeded(int maximumCount)
        {
            int count = 0;
            ModelsSet<NpcDummy>.ReverseEnumerator enumerator =
                World.All<NpcDummy>().Reverse().GetEnumerator();
            while (enumerator.MoveNext())
            {
                NpcDummy dummy = enumerator.Current;
                if (dummy == null
                    || dummy.HasBeenDiscarded
                    || !dummy.HasDied
                    || dummy.ParentTransform == null)
                {
                    continue;
                }

                count++;
                if (count > maximumCount)
                {
                    return true;
                }
            }

            return false;
        }

        internal void TryBeginCorpseRestore(
            NpcDummy dummy,
            Transform parentTransform)
        {
            if (!IsFeatureEnabled
                || dummy == null
                || parentTransform == null
                || !dummy.HasDied)
            {
                return;
            }

            try
            {
                bool isRestoring =
                    (bool)_isRestoringField.GetValue(dummy);
                bool fromAttachment =
                    (bool)_fromAttachmentField.GetValue(dummy);
                if (!isRestoring || fromAttachment)
                {
                    return;
                }

                CorpseSettleController existing =
                    parentTransform.GetComponent<CorpseSettleController>();
                if (existing != null)
                {
                    return;
                }

                CorpseSettleController controller =
                    parentTransform.gameObject
                        .AddComponent<CorpseSettleController>();
                controller.Initialize(this, dummy, parentTransform);
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Could not prepare a restored corpse for concealed settling: "
                    + exception);
            }
        }

        internal void Register(CorpseSettleController controller)
        {
            if (controller != null)
            {
                _activeControllers.Add(controller);
            }
        }

        internal void Unregister(CorpseSettleController controller)
        {
            if (controller != null)
            {
                _activeControllers.Remove(controller);
            }
        }

        internal void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        internal void BeginRest(RestPopupUI restPopup)
        {
            _pendingBonfireRest = restPopup != null
                && restPopup.ViewParent != null;
        }

        internal void CompleteRest(float actualHoursRested)
        {
            bool wasBonfireRest = _pendingBonfireRest;
            _pendingBonfireRest = false;

            if (!wasBonfireRest
                || !IsFeatureEnabled
                || _cleanupAfterLongBonfireRest == null
                || !_cleanupAfterLongBonfireRest.Value)
            {
                return;
            }

            int minimumHours = _minimumRestHoursForCleanup == null
                ? DefaultMinimumRestHoursForCleanup
                : Mathf.Clamp(
                    _minimumRestHoursForCleanup.Value,
                    MinimumAllowedRestHoursForCleanup,
                    MaximumAllowedRestHoursForCleanup);
            if (actualHoursRested * 60.0f + 0.01f < minimumHours * 60)
            {
                LogDiagnostic(
                    "Skipped bonfire corpse cleanup after "
                    + actualHoursRested.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " actual rest hours; the configured minimum is "
                    + minimumHours.ToString(CultureInfo.InvariantCulture)
                    + ".");
                return;
            }

            if (_cleanupCoroutine != null)
            {
                StopCoroutine(_cleanupCoroutine);
            }

            _cleanupCoroutine = StartCoroutine(
                CleanupLoadedCorpses(actualHoursRested));
        }

        private IEnumerator CleanupLoadedCorpses(float actualHoursRested)
        {
            List<NpcDummy> candidates = new List<NpcDummy>();
            int inspected = 0;
            ModelsSet<NpcDummy>.ReverseEnumerator enumerator =
                World.All<NpcDummy>().Reverse().GetEnumerator();
            while (enumerator.MoveNext())
            {
                NpcDummy dummy = enumerator.Current;
                if (dummy != null
                    && !dummy.HasBeenDiscarded
                    && dummy.HasDied
                    && dummy.ParentTransform != null)
                {
                    candidates.Add(dummy);
                }

                inspected++;
                if (inspected % 32 == 0)
                {
                    yield return null;
                }
            }

            int simplified = 0;
            int retained = 0;
            int failed = 0;
            foreach (NpcDummy dummy in candidates)
            {
                if (dummy == null
                    || dummy.HasBeenDiscarded
                    || !dummy.HasDied
                    || dummy.ParentTransform == null)
                {
                    retained++;
                    yield return null;
                    continue;
                }

                bool failedToSimplify;
                if (TrySimplifyCorpse(
                    dummy,
                    "after bonfire rest",
                    out failedToSimplify))
                {
                    simplified++;
                }
                else if (failedToSimplify)
                {
                    failed++;
                }
                else
                {
                    retained++;
                }

                yield return null;
            }

            Logger.LogInfo(
                "Bonfire cleanup after "
                + actualHoursRested.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + " actual rest hours simplified or removed "
                + simplified.ToString(CultureInfo.InvariantCulture)
                + " loaded corpses; "
                + retained.ToString(CultureInfo.InvariantCulture)
                + " were retained and "
                + failed.ToString(CultureInfo.InvariantCulture)
                + " failed.");
            _cleanupCoroutine = null;
        }

        private bool TrySimplifyCorpse(
            NpcDummy dummy,
            string context,
            out bool failed)
        {
            failed = false;
            if (dummy == null
                || dummy.HasBeenDiscarded
                || !dummy.HasDied
                || dummy.ParentTransform == null)
            {
                return false;
            }

            try
            {
                bool canDiscardOriginal =
                    (bool)_tryCreateReplacementDeadBodyMethod.Invoke(
                        dummy,
                        null);
                if (!canDiscardOriginal
                    || dummy.ParentModel == null
                    || dummy.ParentModel.HasBeenDiscarded)
                {
                    return false;
                }

                dummy.ParentModel.Discard();
                return true;
            }
            catch (Exception exception)
            {
                failed = true;
                Logger.LogWarning(
                    "Could not simplify a loaded corpse "
                    + context
                    + ": "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private static class NpcDummyRestorePatch
        {
            public static void BeforeAfterVisualLoaded(
                NpcDummy __instance,
                Transform parentTransform)
            {
                PersistentCorpsesAddonPlugin instance = Instance;
                if (instance != null)
                {
                    instance.TryBeginCorpseRestore(
                        __instance,
                        parentTransform);
                }
            }
        }

        private static class NpcDummySimplificationPatch
        {
            public static void AfterTryReplaceWithSimplifiedLocation(
                NpcDummy __instance,
                ref bool __result)
            {
                PersistentCorpsesAddonPlugin instance = Instance;
                if (instance != null)
                {
                    instance.ApplyRetentionPolicyAfterVanillaAttempt(
                        __instance,
                        ref __result);
                }
            }
        }

        private static class RestPopupPatch
        {
            public static void BeforeRest(RestPopupUI __instance)
            {
                PersistentCorpsesAddonPlugin instance = Instance;
                if (instance != null)
                {
                    instance.BeginRest(__instance);
                }
            }

            public static void AfterSkipWeatherTime(float hourValue)
            {
                PersistentCorpsesAddonPlugin instance = Instance;
                if (instance != null)
                {
                    instance.CompleteRest(hourValue);
                }
            }
        }
    }

    public sealed class CorpseSettleController : MonoBehaviour
    {
        private const int RequiredStablePhysicsSteps = 3;
        private const float LinearVelocityThreshold = 0.08f;
        private const float AngularVelocityThreshold = 0.15f;

        private readonly Dictionary<KandraRenderer, bool> _kandraRenderers =
            new Dictionary<KandraRenderer, bool>();
        private readonly Dictionary<Renderer, bool> _unityRenderers =
            new Dictionary<Renderer, bool>();

        private PersistentCorpsesAddonPlugin _plugin;
        private Transform _renderRoot;
        private Rigidbody[] _rigidbodies = new Rigidbody[0];
        private string _corpseName = "restored corpse";
        private float _physicsElapsed;
        private int _stablePhysicsSteps;
        private bool _dummyInitialized;
        private bool _revealPending;
        private bool _revealed;

        internal void Initialize(
            PersistentCorpsesAddonPlugin plugin,
            NpcDummy dummy,
            Transform renderRoot)
        {
            _plugin = plugin;
            _renderRoot = renderRoot;
            _corpseName = renderRoot == null
                ? "restored corpse"
                : renderRoot.name;

            _plugin.Register(this);
            HideNewRenderers();
            dummy.OnCompletelyInitialized(OnDummyCompletelyInitialized);
            _plugin.LogDiagnostic(
                "Concealing restored corpse "
                + _corpseName
                + " while its pose settles.");
        }

        private void OnDummyCompletelyInitialized(NpcDummy dummy)
        {
            if (_revealed)
            {
                return;
            }

            _dummyInitialized = true;
            HideNewRenderers();
            RefreshRigidbodies();
        }

        private void FixedUpdate()
        {
            if (_revealed
                || _revealPending
                || !_dummyInitialized)
            {
                return;
            }

            if (_plugin == null || !_plugin.IsFeatureEnabled)
            {
                _revealPending = true;
                return;
            }

            _physicsElapsed += Time.fixedDeltaTime;
            float minimumSettleSeconds =
                _plugin.GetMinimumSettleSeconds();
            float maximumSettleSeconds =
                _plugin.GetMaximumSettleSeconds();

            int liveRigidbodyCount;
            bool settled = AreRigidbodyTransformsSettled(
                out liveRigidbodyCount);

            if (_physicsElapsed >= maximumSettleSeconds)
            {
                _revealPending = true;
                return;
            }

            if (_physicsElapsed < minimumSettleSeconds
                || liveRigidbodyCount < 2)
            {
                _stablePhysicsSteps = 0;
                return;
            }

            if (settled)
            {
                _stablePhysicsSteps++;
                if (_stablePhysicsSteps
                    >= RequiredStablePhysicsSteps)
                {
                    _revealPending = true;
                }
            }
            else
            {
                _stablePhysicsSteps = 0;
            }
        }

        private void LateUpdate()
        {
            if (_revealed)
            {
                return;
            }

            if (_plugin == null || !_plugin.IsFeatureEnabled)
            {
                RevealNow("addon disabled");
                return;
            }

            if (!_dummyInitialized)
            {
                HideNewRenderers();
            }
            if (_revealPending)
            {
                string reason = _stablePhysicsSteps
                    >= RequiredStablePhysicsSteps
                    ? "ragdoll settled"
                    : "maximum settle time reached";
                RevealNow(reason);
            }
        }

        private void HideNewRenderers()
        {
            if (_renderRoot == null)
            {
                return;
            }

            KandraRenderer[] kandraRenderers =
                _renderRoot.GetComponentsInChildren<KandraRenderer>(true);
            foreach (KandraRenderer renderer in kandraRenderers)
            {
                if (renderer == null
                    || _kandraRenderers.ContainsKey(renderer))
                {
                    continue;
                }

                bool wasEnabled = renderer.enabled;
                _kandraRenderers.Add(renderer, wasEnabled);
                if (wasEnabled)
                {
                    renderer.enabled = false;
                }
            }

            Renderer[] unityRenderers =
                _renderRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in unityRenderers)
            {
                if (renderer == null
                    || _unityRenderers.ContainsKey(renderer))
                {
                    continue;
                }

                bool wasForcedOff = renderer.forceRenderingOff;
                _unityRenderers.Add(renderer, wasForcedOff);
                renderer.forceRenderingOff = true;
            }
        }

        private void RefreshRigidbodies()
        {
            _rigidbodies = _renderRoot == null
                ? new Rigidbody[0]
                : _renderRoot.GetComponentsInChildren<Rigidbody>(true);
        }

        private bool AreRigidbodyTransformsSettled(
            out int liveRigidbodyCount)
        {
            liveRigidbodyCount = 0;
            bool settled = true;
            float maximumLinearVelocitySquared =
                LinearVelocityThreshold * LinearVelocityThreshold;
            float maximumAngularVelocitySquared =
                AngularVelocityThreshold * AngularVelocityThreshold;

            foreach (Rigidbody rigidbody in _rigidbodies)
            {
                if (rigidbody == null || rigidbody.isKinematic)
                {
                    continue;
                }

                liveRigidbodyCount++;
                if (!rigidbody.IsSleeping()
                    && (rigidbody.linearVelocity.sqrMagnitude
                            > maximumLinearVelocitySquared
                        || rigidbody.angularVelocity.sqrMagnitude
                            > maximumAngularVelocitySquared))
                {
                    settled = false;
                }
            }

            return settled;
        }

        internal void RevealNow(string reason)
        {
            if (_revealed)
            {
                return;
            }

            _revealed = true;
            RestoreRenderers();

            if (_plugin != null)
            {
                _plugin.LogDiagnostic(
                    "Revealed "
                    + _corpseName
                    + " after "
                    + _physicsElapsed.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " seconds of physics simulation: "
                    + reason
                    + ".");
                _plugin.Unregister(this);
            }

            Destroy(this);
        }

        private void RestoreRenderers()
        {
            foreach (KeyValuePair<KandraRenderer, bool> pair
                in _kandraRenderers)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }

            foreach (KeyValuePair<Renderer, bool> pair
                in _unityRenderers)
            {
                if (pair.Key != null)
                {
                    pair.Key.forceRenderingOff = pair.Value;
                }
            }

            _kandraRenderers.Clear();
            _unityRenderers.Clear();
        }

        private void OnDestroy()
        {
            if (!_revealed)
            {
                _revealed = true;
                RestoreRenderers();
            }

            if (_plugin != null)
            {
                _plugin.Unregister(this);
            }
        }
    }
}
