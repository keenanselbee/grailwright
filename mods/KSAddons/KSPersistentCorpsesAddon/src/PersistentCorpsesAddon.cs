using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.Kandra;
using Awaken.TG.Main.Fights.NPCs;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Persistent Corpses Addon")]
[assembly: AssemblyDescription("Conceals restored Persistent Corpses ragdolls until they settle")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Persistent Corpses Addon")]
[assembly: AssemblyVersion("1.0.6.0")]
[assembly: AssemblyFileVersion("1.0.6.0")]

namespace Keenan.TGFoA.PersistentCorpsesAddon
{
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
        public const string PluginName = "Persistent Corpses Addon";
        public const string PluginVersion = "1.0.6";
        public const string ParentPluginGuid =
            "VirusAlex.PersistentCorpses";

        private const int ConfigSchemaVersion = 1;
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

        internal static PersistentCorpsesAddonPlugin Instance { get; private set; }

        private readonly HashSet<CorpseSettleController> _activeControllers =
            new HashSet<CorpseSettleController>();

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _minimumSettleSeconds;
        private ConfigEntry<float> _maximumSettleSeconds;
        private ConfigEntry<bool> _diagnostics;
        private FieldInfo _isRestoringField;
        private FieldInfo _fromAttachmentField;
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

                if (_isRestoringField == null
                    || _fromAttachmentField == null
                    || afterVisualLoadedMethod == null)
                {
                    throw new MissingMemberException(
                        "Could not resolve the NpcDummy corpse-restoration members.");
                }

                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(
                    afterVisualLoadedMethod,
                    prefix: new HarmonyMethod(
                        typeof(NpcDummyRestorePatch),
                        nameof(NpcDummyRestorePatch.BeforeAfterVisualLoaded)));

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
                    + " seconds of physics simulation.");
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
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Master switch. When disabled, restored corpses use the original visible restoration behavior.");
            _minimumSettleSeconds = Config.Bind(
                "2. Settle Timing",
                "MinimumSettleSeconds",
                DefaultMinimumSettleSeconds,
                new ConfigDescription(
                    "Minimum amount of active ragdoll physics time to conceal a restored corpse before it can be revealed.",
                    new AcceptableValueRange<float>(
                        MinimumAllowedSettleSeconds,
                        MaximumAllowedSettleSeconds)));
            _maximumSettleSeconds = Config.Bind(
                "2. Settle Timing",
                "MaximumSettleSeconds",
                DefaultMaximumSettleSeconds,
                new ConfigDescription(
                    "Maximum amount of active physics time to conceal a restored corpse, including bodies that keep moving on slopes.",
                    new AcceptableValueRange<float>(
                        MinimumAllowedSettleSeconds,
                        MaximumAllowedSettleSeconds)));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                "Log restored-corpse concealment and reveal details.");
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
            if (liveRigidbodyCount < 2)
            {
                RefreshRigidbodies();
                settled = AreRigidbodyTransformsSettled(
                    out liveRigidbodyCount);
            }

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

            HideNewRenderers();
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
