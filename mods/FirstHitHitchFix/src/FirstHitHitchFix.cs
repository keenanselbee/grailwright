using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: AssemblyTitle("First Hit Hitch Fix")]
[assembly: AssemblyDescription("Warms combat VFX addressables to reduce first-hit hitches in Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("First Hit Hitch Fix")]
[assembly: AssemblyVersion("0.1.1.0")]
[assembly: AssemblyFileVersion("0.1.1.0")]
[assembly: AssemblyInformationalVersion("0.1.1")]

namespace FirstHitHitchFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class FirstHitHitchFixPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.first-hit-hitch-fix";
        public const string PluginName = "First Hit Hitch Fix";
        public const string PluginVersion = "0.1.1";
        private const int ConfigSchemaVersion = 1;
        private const float SettledSummaryQuietSeconds = 1.0f;

        private const string ShareableReferenceTypeName = "Awaken.TG.Assets.ShareableARAssetReference";
        private const string AddressablesPooledInstanceTypeName = "Awaken.TG.Assets.AddressablesPooledInstance";
        private const string VfxManagerTypeName = "Awaken.TG.Main.Utility.VFX.VFXManager";
        private const string NpcElementTypeName = "Awaken.TG.Main.Fights.NPCs.NpcElement";
        private const string GameConstantsTypeName = "Awaken.TG.Main.General.Configs.GameConstants";
        private const string SurfaceTypeName = "Awaken.TG.Main.Utility.Animations.SurfaceType";

        private static readonly string[] DamageSurfaceNames =
        {
            "DamageMetal",
            "DamageWood",
            "DamageArrow",
            "DamageMagic",
            "DamageOrganic"
        };

        private static readonly string[] HitSurfaceNames =
        {
            "HitWood",
            "HitStone",
            "HitMetal",
            "HitFlesh",
            "HitGround",
            "HitMagic",
            "HitFabric",
            "HitLeather",
            "HitBones"
        };

        internal static FirstHitHitchFixPlugin Instance;
        internal static ManualLogSource Log;

        private readonly Queue _warmQueue = new Queue();
        private readonly HashSet<string> _queuedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, WarmEntry> _warmEntries = new Dictionary<string, WarmEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _warmReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _warmOrder = new List<string>();
        private readonly HashSet<string> _loggedDiscoveredKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _completedWarmKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _failedWarmKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _releasedWarmKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _discoveredKeys = new HashSet<string>(StringComparer.Ordinal);

        private Harmony _harmony;
        private Type _shareableReferenceType;
        private Type _addressablesPooledInstanceType;
        private ConstructorInfo _pooledInstanceConstructor;
        private PropertyInfo _runtimeKeyProperty;
        private PropertyInfo _isSetProperty;
        private PropertyInfo _instanceLoadedProperty;
        private PropertyInfo _instanceProperty;
        private MethodInfo _releaseMethod;

        private Coroutine _defaultWarmupRoutine;
        private Coroutine _warmupQueueRoutine;
        private Coroutine _maintenanceRoutine;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _warmDefaultCombatVfx;
        private ConfigEntry<bool> _warmNpcCombatVfx;
        private ConfigEntry<bool> _warmDiscoveredCombatVfx;
        private ConfigEntry<bool> _holdWarmInstances;
        private ConfigEntry<int> _maxWarmInstances;
        private ConfigEntry<float> _startupWarmupDelaySeconds;
        private ConfigEntry<float> _defaultWarmupRetrySeconds;
        private ConfigEntry<int> _defaultWarmupMaxAttempts;
        private ConfigEntry<float> _warmupSpacingSeconds;
        private ConfigEntry<float> _maintenanceIntervalSeconds;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _logWarmups;
        private ConfigEntry<bool> _logDiscoveredVfx;

        private int _queuedWarmups;
        private int _startedWarmups;
        private int _completedWarmups;
        private int _releasedWarmups;
        private int _failedWarmups;
        private int _discoveredQueuedWarmups;
        private int _discoveredStartedWarmups;
        private int _discoveredCompletedWarmups;
        private int _discoveredReleasedWarmups;
        private int _discoveredFailedWarmups;
        private int _discoveredColdWarmups;
        private int _discoveredColdQueuedLate;
        private int _lastSummaryActivityTotal;
        private float _lastWarmActivityAt = -1.0f;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();
            CacheTypes();
            PatchGame();

            SceneManager.sceneLoaded += OnSceneLoaded;
            _warmupQueueRoutine = StartCoroutine(WarmupQueueLoop());
            _maintenanceRoutine = StartCoroutine(MaintenanceLoop());
            RestartDefaultWarmup();

            Log.LogInfo(
                PluginName
                + " "
                + PluginVersion
                + " loaded. Enabled="
                + FormatBool(_enabled.Value)
                + "; WarmDefaultCombatVFX="
                + FormatBool(_warmDefaultCombatVfx.Value)
                + "; WarmNpcCombatVFX="
                + FormatBool(_warmNpcCombatVfx.Value)
                + "; MaxWarmInstances="
                + _maxWarmInstances.Value.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            StopRoutine(ref _defaultWarmupRoutine);
            StopRoutine(ref _warmupQueueRoutine);
            StopRoutine(ref _maintenanceRoutine);

            ReleaseAllWarmEntries();

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

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch.");
            Config.Bind("1. Core", "ConfigSchemaVersion", ConfigSchemaVersion, "Configuration layout version. Do not edit manually; the plugin backs up stale configs and regenerates defaults when this changes.");
            _warmDefaultCombatVfx = Config.Bind("1. Core", "WarmDefaultCombatVFX", true, "Warm the game's default combat VFX container after gameplay services are ready and after scene loads.");
            _warmNpcCombatVfx = Config.Bind("1. Core", "WarmNpcCombatVFX", true, "Warm NPC-specific hit, critical, backstab, and death VFX when NPCs initialize.");
            _warmDiscoveredCombatVfx = Config.Bind("1. Core", "WarmDiscoveredCombatVFX", true, "Warm combat VFX references discovered through the normal VFX spawn path.");
            _holdWarmInstances = Config.Bind("1. Core", "HoldWarmInstances", true, "Keep disabled warm instances resident instead of releasing them after the first load completes.");
            _maxWarmInstances = Config.Bind(
                "1. Core",
                "MaxWarmInstances",
                64,
                new ConfigDescription(
                    "Maximum disabled warm VFX instances to keep resident. Lower this if memory is tight.",
                    new AcceptableValueRange<int>(0, 512)));
            _startupWarmupDelaySeconds = Config.Bind(
                "2. Timing",
                "StartupWarmupDelaySeconds",
                8.0f,
                new ConfigDescription(
                    "Real seconds to wait before trying to warm default combat VFX after load or scene change.",
                    new AcceptableValueRange<float>(0.0f, 120.0f)));
            _defaultWarmupRetrySeconds = Config.Bind(
                "2. Timing",
                "DefaultWarmupRetrySeconds",
                5.0f,
                new ConfigDescription(
                    "Real seconds between default combat VFX warmup retries while game services are not ready.",
                    new AcceptableValueRange<float>(0.25f, 120.0f)));
            _defaultWarmupMaxAttempts = Config.Bind(
                "2. Timing",
                "DefaultWarmupMaxAttempts",
                24,
                new ConfigDescription(
                    "Maximum attempts to find and warm the default combat VFX container after each load. Zero disables retries.",
                    new AcceptableValueRange<int>(0, 240)));
            _warmupSpacingSeconds = Config.Bind(
                "2. Timing",
                "WarmupSpacingSeconds",
                0.1f,
                new ConfigDescription(
                    "Real seconds between queued warmups. Increase this if the warmup itself causes spikes.",
                    new AcceptableValueRange<float>(0.0f, 10.0f)));
            _maintenanceIntervalSeconds = Config.Bind(
                "2. Timing",
                "MaintenanceIntervalSeconds",
                2.0f,
                new ConfigDescription(
                    "Real seconds between warm instance maintenance checks.",
                    new AcceptableValueRange<float>(0.25f, 60.0f)));
            _diagnostics = Config.Bind("3. Diagnostics", "Diagnostics", false, "Log patch setup, warmup queue activity, completion, releases, and optional discovered VFX references.");
            _logWarmups = Config.Bind("3. Diagnostics", "LogWarmups", true, "When Diagnostics is enabled, log queued, started, completed, failed, and released warmups.");
            _logDiscoveredVfx = Config.Bind("3. Diagnostics", "LogDiscoveredVFX", false, "When Diagnostics is enabled, log combat VFX references discovered from live VFX spawns.");

            Config.Save();
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (String.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = String.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if ((String.Equals(currentSection, "1. Core", StringComparison.Ordinal)
                    || String.Equals(currentSection, "General", StringComparison.Ordinal))
                    && line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    Int32.TryParse(
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
                Log.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
            }
            catch (Exception ex)
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
                catch (Exception restoreEx)
                {
                    Log.LogError("Failed to restore First Hit Hitch Fix config backup after schema reset failure: " + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException("Failed to reset First Hit Hitch Fix config schema. Original config was left in place when possible.", ex);
            }
        }

        private void CacheTypes()
        {
            _shareableReferenceType = AccessTools.TypeByName(ShareableReferenceTypeName);
            _addressablesPooledInstanceType = AccessTools.TypeByName(AddressablesPooledInstanceTypeName);

            if (_shareableReferenceType != null)
            {
                _runtimeKeyProperty = AccessTools.Property(_shareableReferenceType, "RuntimeKey");
                _isSetProperty = AccessTools.Property(_shareableReferenceType, "IsSet");
            }

            if (_addressablesPooledInstanceType != null && _shareableReferenceType != null)
            {
                _pooledInstanceConstructor = _addressablesPooledInstanceType.GetConstructor(new Type[] { _shareableReferenceType, typeof(CancellationToken) });
                _instanceLoadedProperty = AccessTools.Property(_addressablesPooledInstanceType, "InstanceLoaded");
                _instanceProperty = AccessTools.Property(_addressablesPooledInstanceType, "Instance");
                _releaseMethod = AccessTools.Method(_addressablesPooledInstanceType, "Release");
            }
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type vfxManagerType = AccessTools.TypeByName(VfxManagerTypeName);
            MethodInfo directSpawn = FindDirectCombatVfxSpawn(vfxManagerType);
            if (directSpawn != null)
            {
                _harmony.Patch(directSpawn, new HarmonyMethod(AccessTools.Method(typeof(DirectCombatVfxSpawnPatch), "Prefix")), null);
                LogDiagnostic("Patched " + VfxManagerTypeName + ".SpawnCombatVFX direct VFX overload.");
            }
            else
            {
                Log.LogWarning("Could not find the direct combat VFX spawn overload. Discovered VFX warmup is inactive.");
            }

            Type npcElementType = AccessTools.TypeByName(NpcElementTypeName);
            MethodInfo npcInit = AccessTools.Method(npcElementType, "InitFromAttachment");
            if (npcInit != null)
            {
                _harmony.Patch(npcInit, null, new HarmonyMethod(AccessTools.Method(typeof(NpcInitPatch), "Postfix")));
                LogDiagnostic("Patched " + NpcElementTypeName + ".InitFromAttachment.");
            }
            else
            {
                Log.LogWarning("Could not find NPC initialization. NPC-specific combat VFX warmup is inactive.");
            }
        }

        private MethodInfo FindDirectCombatVfxSpawn(Type vfxManagerType)
        {
            if (vfxManagerType == null)
            {
                return null;
            }

            MethodInfo[] methods = vfxManagerType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "SpawnCombatVFX")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                {
                    continue;
                }

                Type firstType = parameters[0].ParameterType;
                if ((_shareableReferenceType != null && firstType == _shareableReferenceType)
                    || firstType.FullName == ShareableReferenceTypeName)
                {
                    return method;
                }
            }

            return null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PruneDeadWarmEntries();
            RestartDefaultWarmup();
        }

        private void RestartDefaultWarmup()
        {
            StopRoutine(ref _defaultWarmupRoutine);
            if (IsActive() && _warmDefaultCombatVfx.Value)
            {
                _defaultWarmupRoutine = StartCoroutine(DefaultCombatVfxWarmupLoop());
            }
        }

        private IEnumerator DefaultCombatVfxWarmupLoop()
        {
            float startupDelay = Math.Max(0.0f, _startupWarmupDelaySeconds.Value);
            if (startupDelay > 0.0f)
            {
                yield return new WaitForSecondsRealtime(startupDelay);
            }

            int maxAttempts = Math.Max(0, _defaultWarmupMaxAttempts.Value);
            float retrySeconds = Math.Max(0.25f, _defaultWarmupRetrySeconds.Value);

            for (int attempt = 1; IsActive() && _warmDefaultCombatVfx.Value && (maxAttempts == 0 || attempt <= maxAttempts); attempt++)
            {
                int validReferences;
                int uniqueReferences;
                int queuedReferences;
                bool foundDefaults = TryQueueDefaultCombatVfx(out validReferences, out uniqueReferences, out queuedReferences);
                if (foundDefaults)
                {
                    if (DiagnosticsWarmupsEnabled())
                    {
                        Log.LogInfo(
                            "Default combat VFX warmup inspected "
                            + validReferences.ToString(CultureInfo.InvariantCulture)
                            + " valid reference(s), "
                            + uniqueReferences.ToString(CultureInfo.InvariantCulture)
                            + " unique key(s), and queued "
                            + queuedReferences.ToString(CultureInfo.InvariantCulture)
                            + " new warmup(s).");
                    }
                    break;
                }

                if (DiagnosticsWarmupsEnabled())
                {
                    Log.LogInfo(
                        "Default combat VFX warmup attempt "
                        + attempt.ToString(CultureInfo.InvariantCulture)
                        + " did not find GameConstants/default VFX yet.");
                }

                yield return new WaitForSecondsRealtime(retrySeconds);
            }

            _defaultWarmupRoutine = null;
        }

        private bool TryQueueDefaultCombatVfx(out int validReferences, out int uniqueReferences, out int queuedReferences)
        {
            validReferences = 0;
            uniqueReferences = 0;
            queuedReferences = 0;

            object gameConstants = GetGameConstants();
            if (gameConstants == null)
            {
                return false;
            }

            HashSet<string> uniqueKeys = new HashSet<string>(StringComparer.Ordinal);

            QueuePropertyReference(gameConstants, "DefaultCriticalVFX", "default-critical", ref validReferences, ref uniqueReferences, ref queuedReferences, uniqueKeys);
            QueuePropertyReference(gameConstants, "DefaultBackStabVFX", "default-backstab", ref validReferences, ref uniqueReferences, ref queuedReferences, uniqueKeys);
            QueuePropertyReference(gameConstants, "DefaultDeathVFX", "default-death", ref validReferences, ref uniqueReferences, ref queuedReferences, uniqueKeys);

            object container = GetPropertyValue(gameConstants, "DefaultItemVfxContainer");
            if (container != null)
            {
                QueueDefaultItemVfxContainer(container, ref validReferences, ref uniqueReferences, ref queuedReferences, uniqueKeys);
            }

            return validReferences > 0;
        }

        private void QueueDefaultItemVfxContainer(object container, ref int validReferences, ref int uniqueReferences, ref int queuedReferences, HashSet<string> uniqueKeys)
        {
            Type surfaceType = AccessTools.TypeByName(SurfaceTypeName);
            if (surfaceType == null)
            {
                return;
            }

            MethodInfo getVfxMethod = AccessTools.Method(container.GetType(), "GetVFX");
            if (getVfxMethod == null)
            {
                return;
            }

            for (int damageIndex = 0; damageIndex < DamageSurfaceNames.Length; damageIndex++)
            {
                object damageSurface = GetStaticFieldValue(surfaceType, DamageSurfaceNames[damageIndex]);
                if (damageSurface == null)
                {
                    continue;
                }

                for (int hitIndex = 0; hitIndex < HitSurfaceNames.Length; hitIndex++)
                {
                    object hitSurface = GetStaticFieldValue(surfaceType, HitSurfaceNames[hitIndex]);
                    if (hitSurface == null)
                    {
                        continue;
                    }

                    try
                    {
                        object reference = getVfxMethod.Invoke(container, new object[] { damageSurface, hitSurface });
                        QueueWarmReferenceWithStats(
                            reference,
                            "default-" + DamageSurfaceNames[damageIndex] + "-" + HitSurfaceNames[hitIndex],
                            ref validReferences,
                            ref uniqueReferences,
                            ref queuedReferences,
                            uniqueKeys);
                    }
                    catch (Exception ex)
                    {
                        LogDiagnostic("Failed to read default combat VFX for " + DamageSurfaceNames[damageIndex] + "/" + HitSurfaceNames[hitIndex] + ": " + ex.GetBaseException().Message);
                    }
                }
            }
        }

        private object GetGameConstants()
        {
            Type gameConstantsType = AccessTools.TypeByName(GameConstantsTypeName);
            if (gameConstantsType == null)
            {
                return null;
            }

            object gameConstants = GetStaticPropertyValue(gameConstantsType, "Get");
            if (gameConstants != null)
            {
                return gameConstants;
            }

            Type worldType = AccessTools.TypeByName("Awaken.TG.MVC.World");
            object services = worldType == null ? null : GetStaticPropertyValue(worldType, "Services");
            if (services == null)
            {
                return null;
            }

            try
            {
                MethodInfo genericGet = FindGenericServiceMethod(services.GetType(), "TryGet", gameConstantsType);
                if (genericGet != null)
                {
                    return genericGet.Invoke(services, null);
                }

                genericGet = FindGenericServiceMethod(services.GetType(), "Get", gameConstantsType);
                return genericGet == null ? null : genericGet.Invoke(services, null);
            }
            catch
            {
                return null;
            }
        }

        private MethodInfo FindGenericServiceMethod(Type servicesType, string name, Type serviceType)
        {
            MethodInfo[] methods = servicesType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != name || !method.IsGenericMethodDefinition || method.GetParameters().Length != 0)
                {
                    continue;
                }

                try
                {
                    return method.MakeGenericMethod(serviceType);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        internal void OnDirectCombatVfxRequested(object vfxReference)
        {
            if (!IsActive() || !_warmDiscoveredCombatVfx.Value)
            {
                return;
            }

            string runtimeKey;
            if (!TryGetRuntimeKey(vfxReference, out runtimeKey) || String.IsNullOrWhiteSpace(runtimeKey))
            {
                QueueWarmReference(vfxReference, "discovered-combat-vfx", false);
                return;
            }

            string warmStatus = GetWarmStatus(runtimeKey);
            string warmedAs = GetWarmReason(runtimeKey);
            bool newlyDiscovered = _discoveredKeys.Add(runtimeKey);
            bool queuedLate = QueueWarmReference(vfxReference, "discovered-combat-vfx", false);

            if (!newlyDiscovered)
            {
                return;
            }

            RecordDiscoveredWarmStatus(warmStatus, queuedLate);
            MarkWarmActivity();

            if (_diagnostics != null && _diagnostics.Value && _logDiscoveredVfx != null && _logDiscoveredVfx.Value && _loggedDiscoveredKeys.Add(runtimeKey))
            {
                Log.LogInfo(
                    "Discovered live combat VFX: key="
                    + runtimeKey
                    + "; warmStatus="
                    + warmStatus
                    + "; warmedAs="
                    + warmedAs
                    + "; queuedLate="
                    + FormatBool(queuedLate)
                    + ".");
            }

            if (String.Equals(warmStatus, "cold", StringComparison.Ordinal) && _diagnostics != null && _diagnostics.Value)
            {
                Log.LogWarning(
                    "Cold combat VFX discovered: key="
                    + runtimeKey
                    + "; queuedLate="
                    + FormatBool(queuedLate)
                    + ".");
            }
        }

        internal void WarmNpcVfx(object npc)
        {
            if (!IsActive() || !_warmNpcCombatVfx.Value || npc == null)
            {
                return;
            }

            QueuePropertyReference(npc, "HitVFX", "npc-hit");
            QueuePropertyReference(npc, "CriticalVFX", "npc-critical");
            QueuePropertyReference(npc, "BackStabVFX", "npc-backstab");

            bool shouldSpawnDeathVfx = true;
            object rawShouldSpawn = GetPropertyValue(npc, "ShouldSpawnDeathVFX");
            if (rawShouldSpawn is bool)
            {
                shouldSpawnDeathVfx = (bool)rawShouldSpawn;
            }

            if (shouldSpawnDeathVfx)
            {
                QueuePropertyReference(npc, "DeathVFX", "npc-death");
            }
        }

        private void QueuePropertyReference(object owner, string propertyName, string reason)
        {
            int validReferences = 0;
            int queuedReferences = 0;
            QueuePropertyReference(owner, propertyName, reason, ref validReferences, ref queuedReferences);
        }

        private void QueuePropertyReference(object owner, string propertyName, string reason, ref int validReferences, ref int queuedReferences)
        {
            object reference = GetPropertyValue(owner, propertyName);
            if (!IsValidVfxReference(reference))
            {
                return;
            }

            validReferences++;
            if (QueueWarmReference(reference, reason, false))
            {
                queuedReferences++;
            }
        }

        private void QueuePropertyReference(object owner, string propertyName, string reason, ref int validReferences, ref int uniqueReferences, ref int queuedReferences, HashSet<string> uniqueKeys)
        {
            QueueWarmReferenceWithStats(
                GetPropertyValue(owner, propertyName),
                reason,
                ref validReferences,
                ref uniqueReferences,
                ref queuedReferences,
                uniqueKeys);
        }

        private void QueueWarmReferenceWithStats(object reference, string reason, ref int validReferences, ref int uniqueReferences, ref int queuedReferences, HashSet<string> uniqueKeys)
        {
            if (!IsValidVfxReference(reference))
            {
                return;
            }

            validReferences++;

            string runtimeKey;
            if (TryGetRuntimeKey(reference, out runtimeKey)
                && !String.IsNullOrWhiteSpace(runtimeKey)
                && uniqueKeys != null
                && uniqueKeys.Add(runtimeKey))
            {
                uniqueReferences++;
            }

            if (QueueWarmReference(reference, reason, false))
            {
                queuedReferences++;
            }
        }

        private bool QueueWarmReference(object reference, string reason, bool allowDuplicateQueue)
        {
            if (!IsActive() || reference == null || _pooledInstanceConstructor == null)
            {
                return false;
            }

            string runtimeKey;
            if (!TryGetRuntimeKey(reference, out runtimeKey) || String.IsNullOrWhiteSpace(runtimeKey))
            {
                return false;
            }

            lock (_warmQueue)
            {
                if (!allowDuplicateQueue && (_warmEntries.ContainsKey(runtimeKey) || _queuedKeys.Contains(runtimeKey)))
                {
                    return false;
                }

                _warmQueue.Enqueue(new WarmRequest(reference, runtimeKey, reason));
                _queuedKeys.Add(runtimeKey);
                RememberWarmReason(runtimeKey, reason);
                _completedWarmKeys.Remove(runtimeKey);
                _failedWarmKeys.Remove(runtimeKey);
                _releasedWarmKeys.Remove(runtimeKey);
                _queuedWarmups++;
                MarkWarmActivity();
            }

            if (DiagnosticsWarmupsEnabled())
            {
                Log.LogInfo("Queued VFX warmup: key=" + runtimeKey + "; reason=" + reason + ".");
            }

            return true;
        }

        private IEnumerator WarmupQueueLoop()
        {
            while (true)
            {
                if (!IsActive())
                {
                    yield return new WaitForSecondsRealtime(1.0f);
                    continue;
                }

                WarmRequest request = null;
                lock (_warmQueue)
                {
                    if (_warmQueue.Count > 0)
                    {
                        request = _warmQueue.Dequeue() as WarmRequest;
                        if (request != null)
                        {
                            _queuedKeys.Remove(request.RuntimeKey);
                        }
                    }
                }

                if (request == null)
                {
                    yield return null;
                    continue;
                }

                StartWarmup(request);

                float spacing = Math.Max(0.0f, _warmupSpacingSeconds.Value);
                if (spacing > 0.0f)
                {
                    yield return new WaitForSecondsRealtime(spacing);
                }
                else
                {
                    yield return null;
                }
            }
        }

        private void StartWarmup(WarmRequest request)
        {
            if (request == null || String.IsNullOrWhiteSpace(request.RuntimeKey) || _warmEntries.ContainsKey(request.RuntimeKey))
            {
                return;
            }

            int maxWarmInstances = Math.Max(0, _maxWarmInstances.Value);
            if (maxWarmInstances <= 0)
            {
                return;
            }

            while (_warmEntries.Count >= maxWarmInstances && _warmOrder.Count > 0)
            {
                ReleaseWarmEntry(_warmOrder[0], "cap");
            }

            try
            {
                object instance = _pooledInstanceConstructor.Invoke(new object[] { request.Reference, CancellationToken.None });
                WarmEntry entry = new WarmEntry(request.RuntimeKey, request.Reason, instance, Time.realtimeSinceStartup);
                _warmEntries[request.RuntimeKey] = entry;
                _warmOrder.Add(request.RuntimeKey);
                RememberWarmReason(request.RuntimeKey, request.Reason);
                _startedWarmups++;
                MarkWarmActivity();

                if (DiagnosticsWarmupsEnabled())
                {
                    Log.LogInfo("Started VFX warmup: key=" + request.RuntimeKey + "; reason=" + request.Reason + ".");
                }
            }
            catch (Exception ex)
            {
                _failedWarmups++;
                _failedWarmKeys.Add(request.RuntimeKey);
                MarkWarmActivity();
                if (DiagnosticsWarmupsEnabled())
                {
                    Log.LogWarning("Failed to start VFX warmup for " + request.RuntimeKey + ": " + ex.GetBaseException().Message);
                }
            }
        }

        private IEnumerator MaintenanceLoop()
        {
            while (true)
            {
                float interval = Math.Max(0.25f, _maintenanceIntervalSeconds.Value);
                yield return new WaitForSecondsRealtime(interval);
                MaintainWarmEntries();
                MaybeLogSettledSummary();
            }
        }

        private void MaintainWarmEntries()
        {
            List<string> keys = new List<string>(_warmOrder);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                WarmEntry entry;
                if (!_warmEntries.TryGetValue(key, out entry))
                {
                    continue;
                }

                bool loaded = IsEntryLoaded(entry);
                if (!loaded)
                {
                    continue;
                }

                bool alive = IsEntryAlive(entry);
                if (!entry.CompletedLogged)
                {
                    entry.CompletedLogged = true;
                    if (alive)
                    {
                        _completedWarmups++;
                        _completedWarmKeys.Add(key);
                        _failedWarmKeys.Remove(key);
                        MarkWarmActivity();
                        if (DiagnosticsWarmupsEnabled())
                        {
                            Log.LogInfo(
                                "Completed VFX warmup: key="
                                + key
                                + "; reason="
                                + entry.Reason
                                + "; elapsed="
                                + (Time.realtimeSinceStartup - entry.StartedAt).ToString("0.###", CultureInfo.InvariantCulture)
                                + "s.");
                        }
                    }
                    else
                    {
                        _failedWarmups++;
                        _failedWarmKeys.Add(key);
                        MarkWarmActivity();
                        if (DiagnosticsWarmupsEnabled())
                        {
                            Log.LogWarning("VFX warmup finished without an instance: key=" + key + "; reason=" + entry.Reason + ".");
                        }
                    }
                }

                if (!_holdWarmInstances.Value || !alive)
                {
                    ReleaseWarmEntry(key, alive ? "not-held" : "dead-instance");
                }
            }
        }

        private void PruneDeadWarmEntries()
        {
            List<string> keys = new List<string>(_warmOrder);
            for (int i = 0; i < keys.Count; i++)
            {
                WarmEntry entry;
                if (_warmEntries.TryGetValue(keys[i], out entry) && IsEntryLoaded(entry) && !IsEntryAlive(entry))
                {
                    ReleaseWarmEntry(keys[i], "scene-prune");
                }
            }
        }

        private bool IsEntryLoaded(WarmEntry entry)
        {
            if (entry == null || entry.Instance == null || _instanceLoadedProperty == null)
            {
                return false;
            }

            try
            {
                object value = _instanceLoadedProperty.GetValue(entry.Instance, null);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private bool IsEntryAlive(WarmEntry entry)
        {
            if (entry == null || entry.Instance == null || _instanceProperty == null)
            {
                return false;
            }

            try
            {
                object value = _instanceProperty.GetValue(entry.Instance, null);
                UnityEngine.Object unityObject = value as UnityEngine.Object;
                return unityObject != null;
            }
            catch
            {
                return false;
            }
        }

        private void ReleaseWarmEntry(string key, string reason)
        {
            WarmEntry entry;
            if (String.IsNullOrWhiteSpace(key) || !_warmEntries.TryGetValue(key, out entry))
            {
                if (_warmOrder.Count > 0 && _warmOrder[0] == key)
                {
                    _warmOrder.RemoveAt(0);
                }
                return;
            }

            try
            {
                if (entry.Instance != null && _releaseMethod != null)
                {
                    _releaseMethod.Invoke(entry.Instance, null);
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic("Failed to release warm VFX instance " + key + ": " + ex.GetBaseException().Message);
            }

            _warmEntries.Remove(key);
            _warmOrder.Remove(key);
            _releasedWarmKeys.Add(key);
            _releasedWarmups++;
            MarkWarmActivity();

            if (DiagnosticsWarmupsEnabled())
            {
                Log.LogInfo("Released VFX warmup: key=" + key + "; reason=" + reason + ".");
            }
        }

        private void ReleaseAllWarmEntries()
        {
            List<string> keys = new List<string>(_warmOrder);
            for (int i = 0; i < keys.Count; i++)
            {
                ReleaseWarmEntry(keys[i], "unload");
            }

            LogWarmupSummary("unload");
        }

        private void RememberWarmReason(string runtimeKey, string reason)
        {
            if (String.IsNullOrWhiteSpace(runtimeKey) || String.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            string existing;
            if (!_warmReasons.TryGetValue(runtimeKey, out existing)
                || String.IsNullOrWhiteSpace(existing)
                || (String.Equals(existing, "discovered-combat-vfx", StringComparison.Ordinal)
                    && !String.Equals(reason, "discovered-combat-vfx", StringComparison.Ordinal)))
            {
                _warmReasons[runtimeKey] = reason;
            }
        }

        private string GetWarmReason(string runtimeKey)
        {
            if (String.IsNullOrWhiteSpace(runtimeKey))
            {
                return "none";
            }

            string reason;
            if (_warmReasons.TryGetValue(runtimeKey, out reason) && !String.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            WarmEntry entry;
            if (_warmEntries.TryGetValue(runtimeKey, out entry) && entry != null && !String.IsNullOrWhiteSpace(entry.Reason))
            {
                return entry.Reason;
            }

            return "none";
        }

        private string GetWarmStatus(string runtimeKey)
        {
            if (String.IsNullOrWhiteSpace(runtimeKey))
            {
                return "cold";
            }

            lock (_warmQueue)
            {
                if (_queuedKeys.Contains(runtimeKey))
                {
                    return "queued";
                }
            }

            WarmEntry entry;
            if (_warmEntries.TryGetValue(runtimeKey, out entry))
            {
                return entry != null && entry.CompletedLogged && _completedWarmKeys.Contains(runtimeKey) ? "completed" : "started";
            }

            if (_failedWarmKeys.Contains(runtimeKey))
            {
                return "failed";
            }

            if (_releasedWarmKeys.Contains(runtimeKey))
            {
                return _completedWarmKeys.Contains(runtimeKey) ? "released-after-completed" : "released";
            }

            if (_completedWarmKeys.Contains(runtimeKey))
            {
                return "completed";
            }

            return "cold";
        }

        private void RecordDiscoveredWarmStatus(string warmStatus, bool queuedLate)
        {
            if (String.Equals(warmStatus, "queued", StringComparison.Ordinal))
            {
                _discoveredQueuedWarmups++;
            }
            else if (String.Equals(warmStatus, "started", StringComparison.Ordinal))
            {
                _discoveredStartedWarmups++;
            }
            else if (String.Equals(warmStatus, "completed", StringComparison.Ordinal))
            {
                _discoveredCompletedWarmups++;
            }
            else if (String.Equals(warmStatus, "released", StringComparison.Ordinal)
                || String.Equals(warmStatus, "released-after-completed", StringComparison.Ordinal))
            {
                _discoveredReleasedWarmups++;
            }
            else if (String.Equals(warmStatus, "failed", StringComparison.Ordinal))
            {
                _discoveredFailedWarmups++;
            }
            else
            {
                _discoveredColdWarmups++;
                if (queuedLate)
                {
                    _discoveredColdQueuedLate++;
                }
            }
        }

        private void MarkWarmActivity()
        {
            _lastWarmActivityAt = Time.realtimeSinceStartup;
        }

        private void MaybeLogSettledSummary()
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            int activityTotal = GetSummaryActivityTotal();
            if (activityTotal <= 0 || activityTotal == _lastSummaryActivityTotal || HasPendingWarmups())
            {
                return;
            }

            if (_lastWarmActivityAt >= 0.0f && Time.realtimeSinceStartup - _lastWarmActivityAt < SettledSummaryQuietSeconds)
            {
                return;
            }

            LogWarmupSummary("settled");
        }

        private bool HasPendingWarmups()
        {
            lock (_warmQueue)
            {
                if (_queuedKeys.Count > 0)
                {
                    return true;
                }
            }

            foreach (WarmEntry entry in _warmEntries.Values)
            {
                if (entry != null && !entry.CompletedLogged)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetSummaryActivityTotal()
        {
            return _queuedWarmups
                + _startedWarmups
                + _completedWarmups
                + _releasedWarmups
                + _failedWarmups
                + _discoveredKeys.Count;
        }

        private void LogWarmupSummary(string reason)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            int pendingQueue;
            lock (_warmQueue)
            {
                pendingQueue = _queuedKeys.Count;
            }

            int discoveredCovered = _discoveredQueuedWarmups
                + _discoveredStartedWarmups
                + _discoveredCompletedWarmups
                + _discoveredReleasedWarmups;

            Log.LogInfo(
                "First Hit Hitch Fix summary: reason="
                + reason
                + "; queued="
                + _queuedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; started="
                + _startedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; completed="
                + _completedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; released="
                + _releasedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; failed="
                + _failedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; held="
                + _warmEntries.Count.ToString(CultureInfo.InvariantCulture)
                + "; pendingQueue="
                + pendingQueue.ToString(CultureInfo.InvariantCulture)
                + "; discovered="
                + _discoveredKeys.Count.ToString(CultureInfo.InvariantCulture)
                + "; discoveredCovered="
                + discoveredCovered.ToString(CultureInfo.InvariantCulture)
                + "; discoveredQueued="
                + _discoveredQueuedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; discoveredStarted="
                + _discoveredStartedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; discoveredCompleted="
                + _discoveredCompletedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; discoveredReleased="
                + _discoveredReleasedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; discoveredFailed="
                + _discoveredFailedWarmups.ToString(CultureInfo.InvariantCulture)
                + "; discoveredCold="
                + _discoveredColdWarmups.ToString(CultureInfo.InvariantCulture)
                + "; discoveredColdQueuedLate="
                + _discoveredColdQueuedLate.ToString(CultureInfo.InvariantCulture)
                + ".");

            _lastSummaryActivityTotal = GetSummaryActivityTotal();
        }

        private bool IsValidVfxReference(object reference)
        {
            if (reference == null)
            {
                return false;
            }

            if (_shareableReferenceType != null && !_shareableReferenceType.IsInstanceOfType(reference))
            {
                return false;
            }

            try
            {
                if (_isSetProperty != null)
                {
                    object isSet = _isSetProperty.GetValue(reference, null);
                    if (isSet is bool && !(bool)isSet)
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            string runtimeKey;
            return TryGetRuntimeKey(reference, out runtimeKey) && !String.IsNullOrWhiteSpace(runtimeKey);
        }

        private bool TryGetRuntimeKey(object reference, out string runtimeKey)
        {
            runtimeKey = "";
            if (reference == null)
            {
                return false;
            }

            try
            {
                PropertyInfo property = _runtimeKeyProperty;
                if (property == null || !property.DeclaringType.IsInstanceOfType(reference))
                {
                    property = AccessTools.Property(reference.GetType(), "RuntimeKey");
                }

                object value = property == null ? null : property.GetValue(reference, null);
                runtimeKey = value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
                return !String.IsNullOrWhiteSpace(runtimeKey);
            }
            catch
            {
                runtimeKey = "";
                return false;
            }
        }

        private bool IsActive()
        {
            return _enabled != null && _enabled.Value;
        }

        private bool DiagnosticsWarmupsEnabled()
        {
            return _diagnostics != null && _diagnostics.Value && _logWarmups != null && _logWarmups.Value;
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Log.LogInfo(message);
            }
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private static object GetPropertyValue(object owner, string propertyName)
        {
            if (owner == null || String.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            Type type = owner.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (type != null)
            {
                try
                {
                    PropertyInfo property = type.GetProperty(propertyName, flags);
                    if (property != null && property.GetIndexParameters().Length == 0)
                    {
                        return property.GetValue(owner, null);
                    }

                    FieldInfo field = type.GetField(propertyName, flags);
                    if (field != null)
                    {
                        return field.GetValue(owner);
                    }
                }
                catch
                {
                    return null;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object GetStaticPropertyValue(Type type, string propertyName)
        {
            if (type == null || String.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            try
            {
                PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return property == null ? null : property.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static object GetStaticFieldValue(Type type, string fieldName)
        {
            if (type == null || String.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            try
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return field == null ? null : field.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }

        private sealed class WarmRequest
        {
            public readonly object Reference;
            public readonly string RuntimeKey;
            public readonly string Reason;

            public WarmRequest(object reference, string runtimeKey, string reason)
            {
                Reference = reference;
                RuntimeKey = runtimeKey;
                Reason = reason;
            }
        }

        private sealed class WarmEntry
        {
            public readonly string RuntimeKey;
            public readonly string Reason;
            public readonly object Instance;
            public readonly float StartedAt;
            public bool CompletedLogged;

            public WarmEntry(string runtimeKey, string reason, object instance, float startedAt)
            {
                RuntimeKey = runtimeKey;
                Reason = reason;
                Instance = instance;
                StartedAt = startedAt;
            }
        }

        private static class DirectCombatVfxSpawnPatch
        {
            public static void Prefix(object __0)
            {
                FirstHitHitchFixPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.OnDirectCombatVfxRequested(__0);
                }
            }
        }

        private static class NpcInitPatch
        {
            public static void Postfix(object __instance)
            {
                FirstHitHitchFixPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.WarmNpcVfx(__instance);
                }
            }
        }
    }
}
