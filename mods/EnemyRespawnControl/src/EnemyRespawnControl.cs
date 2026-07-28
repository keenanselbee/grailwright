using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

[assembly: AssemblyVersion("1.0.9.0")]
[assembly: AssemblyFileVersion("1.0.9.0")]
[assembly: AssemblyInformationalVersion("1.0.9")]

namespace EnemyRespawnControl
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EnemyRespawnControlPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.enemy-respawn-control";
        public const string PluginName = "Enemy Respawn Control";
        public const string PluginVersion = "1.0.9";
        private const int ConfigSchemaVersion = 3;

        private const string BaseSpawnerTypeName = "Awaken.TG.Main.Locations.Spawners.BaseLocationSpawner";
        private const string GroupSpawnerTypeName = "Awaken.TG.Main.Locations.Spawners.GroupLocationSpawner";
        private const string LocationSpawnerTypeName = "Awaken.TG.Main.Locations.Spawners.LocationSpawner";
        private const string HideSpotSpawnerTypeName = "Awaken.TG.Main.Locations.Spawners.HideSpotLocationSpawner";
        private const string NpcAttachmentTypeName = "Awaken.TG.Main.Fights.NPCs.NpcAttachment";

        internal static EnemyRespawnControlPlugin Instance;
        internal static ManualLogSource Log;

        private readonly Dictionary<string, RespawnLock> _locks = new Dictionary<string, RespawnLock>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _nextBlockLogUtc = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _nextDiagnosticLogUtc = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly HashSet<string> _expiredKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly ConditionalWeakTable<object, CachedKey> _keyCache = new ConditionalWeakTable<object, CachedKey>();
        private readonly ConditionalWeakTable<object, SpecialSpawnedLocation> _specialSpawnedLocations = new ConditionalWeakTable<object, SpecialSpawnedLocation>();

        private Harmony _harmony;
        private FieldInfo _lastClearOfGroupField;
        private FieldInfo _killedLocationsField;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<RespawnMode> _respawnMode;
        private ConfigEntry<float> _customRespawnHours;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<float> _blockedLogIntervalSeconds;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();
            PatchGame();

            Log.LogInfo(PluginName + " " + PluginVersion + " loaded. Mode=" + _respawnMode.Value +
                "; CustomRespawnHours=" + _customRespawnHours.Value.ToString("0.###", CultureInfo.InvariantCulture) +
                "; TimeSource=weather.");
        }

        private void OnDestroy()
        {
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
            Config.Bind("1. Core", "ConfigSchemaVersion", ConfigSchemaVersion, "Configuration layout version. It changes only when an update requires fresh defaults.");
            _respawnMode = Config.Bind("1. Core", "RespawnMode", RespawnMode.Default24Hours, "Respawn delay after a spawner has produced killed enemies. All durations use in-game/weather time. Vanilla=2h, Fast6Hours=6h, Default24Hours=24h, Slow72Hours=72h, VerySlow168Hours=168h, Custom, or Disabled.");
            _customRespawnHours = Config.Bind("1. Core", "CustomRespawnHours", 168f, "Used when RespawnMode is Custom. Interpreted as in-game/weather hours.");
            _diagnostics = Config.Bind("2. Diagnostics", "Diagnostics", false, "Log spawner keys, lock creation, blocked gate names, allowed spawn attempts, special-spawn bypasses, skipped non-enemy spawners, cleanup, and expiry decisions.");
            _blockedLogIntervalSeconds = Config.Bind("2. Diagnostics", "BlockedLogIntervalSeconds", 15f, "Minimum real seconds between repeated blocked-respawn diagnostics for the same spawner.");
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
                    Log.LogError("Failed to restore Enemy Respawn Control config backup after schema reset failure: " + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException("Failed to reset Enemy Respawn Control config schema. Original config was left in place when possible.", ex);
            }
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type baseSpawnerType = AccessTools.TypeByName(BaseSpawnerTypeName);
            Type groupSpawnerType = AccessTools.TypeByName(GroupSpawnerTypeName);
            Type locationSpawnerType = AccessTools.TypeByName(LocationSpawnerTypeName);
            Type hideSpotSpawnerType = AccessTools.TypeByName(HideSpotSpawnerTypeName);

            if (baseSpawnerType == null)
            {
                Log.LogError("Could not find " + BaseSpawnerTypeName + ". Enemy Respawn Control is inactive.");
                return;
            }

            _lastClearOfGroupField = AccessTools.Field(baseSpawnerType, "lastClearOfGroup");
            _killedLocationsField = AccessTools.Field(baseSpawnerType, "killedLocations");

            PatchMethod(baseSpawnerType, "get_CooldownCondition", typeof(CooldownConditionPatch), true);
            PatchMethod(baseSpawnerType, "get_CanSpawn", typeof(CanSpawnPatch), true);
            PatchMethod(baseSpawnerType, "get_CanSpawnAmbush", typeof(CanSpawnAmbushPatch), false);
            PatchMethod(baseSpawnerType, "get_IsValidState", typeof(IsValidStatePatch), false);
            PatchMethod(baseSpawnerType, "OnLocationSpawned", typeof(OnLocationSpawnedPatch), false);
            PatchMethod(baseSpawnerType, "AfterTimeSkipped", typeof(AfterTimeSkippedPatch), false);
            PatchMethod(baseSpawnerType, "AfterHeroTeleport", typeof(AfterHeroTeleportPatch), false);
            PatchMethod(baseSpawnerType, "OnRestore", typeof(OnRestorePatch), false);
            PatchMethod(baseSpawnerType, "AfterLocationKilled", typeof(AfterLocationKilledPatch), true);
            PatchMethod(baseSpawnerType, "OnLocationDiscardedOrKilled", typeof(OnLocationDiscardedOrKilledPatch), false);
            PatchMethod(baseSpawnerType, "SceneInitializationEndedCallback", typeof(SceneInitializationEndedPatch), false);
            PatchMethodsByName(groupSpawnerType, "InitFromAttachment", typeof(SpawnerInitPatch), false);
            PatchMethodsByName(locationSpawnerType, "InitFromAttachment", typeof(SpawnerInitPatch), false);
            PatchMethodsByName(hideSpotSpawnerType, "InitFromAttachment", typeof(SpawnerInitPatch), false);
            PatchMethodsByName(groupSpawnerType, "ShouldSpawn", typeof(ShouldSpawnPatch), false);
            PatchMethodsByName(locationSpawnerType, "ShouldSpawn", typeof(ShouldSpawnPatch), false);
            PatchMethodsByName(hideSpotSpawnerType, "ShouldSpawn", typeof(ShouldSpawnPatch), false);
            PatchMethodsByNamePrefix(groupSpawnerType, "SpawnPrefabInternal", typeof(SpawnPrefabInternalPatch), false);
            PatchMethodsByNamePrefix(locationSpawnerType, "SpawnPrefabInternal", typeof(SpawnPrefabInternalPatch), false);
            PatchMethodsByNamePrefix(hideSpotSpawnerType, "SpawnPrefabInternal", typeof(SpawnPrefabInternalPatch), false);
        }

        private void PatchMethod(Type declaringType, string methodName, Type patchType, bool required)
        {
            if (declaringType == null)
            {
                if (required)
                {
                    Log.LogError("Could not patch " + methodName + " because the declaring type was not found.");
                }
                return;
            }

            MethodInfo original = AccessTools.Method(declaringType, methodName);
            if (original == null)
            {
                if (required)
                {
                    Log.LogError("Could not find " + declaringType.FullName + "." + methodName + ".");
                }
                return;
            }

            MethodInfo postfix = AccessTools.Method(patchType, "Postfix");
            if (postfix == null)
            {
                Log.LogError("Could not find postfix " + patchType.FullName + ".Postfix.");
                return;
            }

            _harmony.Patch(original, null, new HarmonyMethod(postfix));
            if (_diagnostics.Value)
            {
                Log.LogInfo("Patched " + original.DeclaringType.FullName + "." + original.Name + ".");
            }
        }

        private void PatchMethodsByName(Type declaringType, string methodName, Type patchType, bool required)
        {
            if (declaringType == null)
            {
                if (required)
                {
                    Log.LogError("Could not patch " + methodName + " because the declaring type was not found.");
                }
                return;
            }

            MethodInfo postfix = AccessTools.Method(patchType, "Postfix");
            if (postfix == null)
            {
                Log.LogError("Could not find postfix " + patchType.FullName + ".Postfix.");
                return;
            }

            int patched = 0;
            MethodInfo[] methods = declaringType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != methodName)
                {
                    continue;
                }

                _harmony.Patch(methods[i], null, new HarmonyMethod(postfix));
                patched++;
            }

            if (patched == 0 && required)
            {
                Log.LogError("Could not find any " + declaringType.FullName + "." + methodName + " methods.");
            }
            else if (patched > 0 && _diagnostics.Value)
            {
                Log.LogInfo("Patched " + patched.ToString(CultureInfo.InvariantCulture) + " " + declaringType.FullName + "." + methodName + " method(s).");
            }
        }

        private void PatchMethodsByNamePrefix(Type declaringType, string methodName, Type patchType, bool required)
        {
            if (declaringType == null)
            {
                if (required)
                {
                    Log.LogError("Could not patch " + methodName + " because the declaring type was not found.");
                }
                return;
            }

            MethodInfo prefix = AccessTools.Method(patchType, "Prefix");
            if (prefix == null)
            {
                Log.LogError("Could not find prefix " + patchType.FullName + ".Prefix.");
                return;
            }

            int patched = 0;
            MethodInfo[] methods = declaringType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != methodName)
                {
                    continue;
                }

                _harmony.Patch(methods[i], new HarmonyMethod(prefix), null);
                patched++;
            }

            if (patched == 0 && required)
            {
                Log.LogError("Could not find any " + declaringType.FullName + "." + methodName + " methods.");
            }
            else if (patched > 0 && _diagnostics.Value)
            {
                Log.LogInfo("Patched " + patched.ToString(CultureInfo.InvariantCulture) + " " + declaringType.FullName + "." + methodName + " prefix method(s).");
            }
        }

        internal void RegisterKilledSpawner(object spawner, object location, string reason)
        {
            if (!IsActive() || spawner == null || IsSpecialSpawnedLocation(location) || ShouldBypassRespawnControl(spawner, reason))
            {
                return;
            }

            if (!ShouldControlSpawner(spawner, reason))
            {
                LogNonEnemySpawnerBypass(spawner, reason);
                return;
            }

            double now;
            string timeSource;
            if (!TryGetCurrentTimeSeconds(spawner, out now, out timeSource))
            {
                if (_diagnostics.Value)
                {
                    Log.LogWarning("Could not read game time while registering a respawn lock for " + DescribeObject(spawner) + ".");
                }
                return;
            }

            string key = GetSpawnerKey(spawner);
            RegisterLock(key, spawner, now, timeSource, reason, true);
        }

        internal void RegisterSpawnerIfKilledState(object spawner, string reason)
        {
            if (!IsActive() || spawner == null || ShouldBypassRespawnControl(spawner, reason))
            {
                return;
            }

            if (!ShouldControlSpawner(spawner, reason))
            {
                LogNonEnemySpawnerBypass(spawner, reason);
                return;
            }

            if (!HasKilledLocationState(spawner))
            {
                return;
            }

            string key = GetSpawnerKey(spawner);
            if (_locks.ContainsKey(key) || _expiredKeys.Contains(key))
            {
                return;
            }

            double now;
            string timeSource;
            if (TryGetCurrentTimeSeconds(spawner, out now, out timeSource))
            {
                RegisterLock(key, spawner, now, timeSource, reason, false);
            }
        }

        internal void ApplyCooldownGate(object spawner, ref bool result)
        {
            ApplySpawnGate(spawner, ref result, "cooldown");
        }

        internal void ApplySpawnGate(object spawner, ref bool result, string gateName)
        {
            if (!IsActive() || spawner == null || !result)
            {
                return;
            }

            string key;
            RespawnLock existing;
            double now;
            string timeSource;
            if (TryEvaluateSpawnerBlock(spawner, gateName, out key, out existing, out now, out timeSource))
            {
                result = false;
                LogBlocked(key, existing, now, timeSource, gateName);
            }
        }

        internal bool BeforeSpawnInternal(object spawner, string gateName)
        {
            if (!IsActive() || spawner == null)
            {
                return true;
            }

            string key;
            RespawnLock existing;
            double now;
            string timeSource;
            if (TryEvaluateSpawnerBlock(spawner, gateName, out key, out existing, out now, out timeSource))
            {
                LogBlocked(key, existing, now, timeSource, gateName);
                return false;
            }

            if (_diagnostics.Value && !String.IsNullOrEmpty(key))
            {
                LogAllowedSpawnAttempt(key, spawner, gateName, existing);
            }

            return true;
        }

        internal void AfterLocationSpawned(object spawner, object location, int id)
        {
            if (!IsActive() || spawner == null || location == null)
            {
                return;
            }

            if (ShouldBypassRespawnControl(spawner, "spawned-location-special"))
            {
                MarkSpecialSpawnedLocation(location);
                LogSpecialSpawnBypass(spawner, "spawned-location-special");
                return;
            }

            string key;
            RespawnLock existing;
            double now;
            string timeSource;
            if (!TryEvaluateSpawnerBlock(spawner, "spawned-location-cleanup", out key, out existing, out now, out timeSource))
            {
                return;
            }

            LogBlocked(key, existing, now, timeSource, "spawned-location-cleanup");
            if (TryDiscardLocation(location))
            {
                LogDiagnosticRateLimited(
                    key + "|discarded-spawned-location",
                    "Discarded a location that was spawned by a locked spawner. location=" + DescribeObject(location) +
                    "; id=" + id.ToString(CultureInfo.InvariantCulture) +
                    "; spawner=" + DescribeSpawner(spawner) + ".");
            }
            else if (_diagnostics.Value)
            {
                Log.LogWarning("A locked spawner produced a location, but Enemy Respawn Control could not discard it. location=" +
                    DescribeObject(location) + "; id=" + id.ToString(CultureInfo.InvariantCulture) + "; spawner=" + DescribeSpawner(spawner) + ".");
            }
        }

        private bool TryEvaluateSpawnerBlock(object spawner, string gateName, out string key, out RespawnLock respawnLock, out double now, out string timeSource)
        {
            key = String.Empty;
            respawnLock = null;
            now = 0d;
            timeSource = String.Empty;

            if (ShouldBypassRespawnControl(spawner, gateName))
            {
                LogSpecialSpawnBypass(spawner, gateName);
                return false;
            }

            if (!ShouldControlSpawner(spawner, gateName))
            {
                LogNonEnemySpawnerBypass(spawner, gateName);
                return false;
            }

            if (!TryGetCurrentTimeSeconds(spawner, out now, out timeSource))
            {
                key = GetSpawnerKey(spawner);
                bool hasLock = _locks.TryGetValue(key, out respawnLock);
                bool hasKilledState = HasKilledLocationState(spawner);
                if (hasLock || hasKilledState)
                {
                    if (respawnLock == null)
                    {
                        respawnLock = new RespawnLock();
                        respawnLock.StartSeconds = now;
                        respawnLock.TimeSource = "weather";
                        respawnLock.Reason = "weather-time-unavailable";
                        respawnLock.SpawnerDescription = DescribeSpawner(spawner);
                    }

                    LogDiagnosticRateLimited(
                        "time-read-failed-block|" + key + "|" + gateName,
                        "Could not read in-game/weather time while evaluating respawn gate '" + gateName + "' for " + DescribeSpawner(spawner) +
                        "; blocking because " + (hasLock ? "a respawn lock exists" : "the spawner has killed-state") + ".");
                    timeSource = "weather";
                    return true;
                }

                LogDiagnosticRateLimited(
                    "time-read-failed|" + DescribeObject(spawner),
                    "Could not read in-game/weather time while evaluating respawn gate '" + gateName + "' for " + DescribeSpawner(spawner) +
                    "; allowing because no respawn lock or killed-state is known.");
                return false;
            }

            key = GetSpawnerKey(spawner);
            if (!_locks.TryGetValue(key, out respawnLock) && HasKilledLocationState(spawner) && !_expiredKeys.Contains(key))
            {
                RegisterLock(key, spawner, now, timeSource, "killed-state-" + gateName, false);
            }

            return ShouldBlockRespawn(key, spawner, now, timeSource, out respawnLock);
        }

        private bool ShouldBypassRespawnControl(object spawner, string gateName)
        {
            if (spawner == null)
            {
                return false;
            }

            return String.Equals(gateName, "can-spawn-ambush", StringComparison.Ordinal)
                || IsTruthyMember(spawner, "IsManualSpawner")
                || IsTruthyMember(spawner, "_isManualSpawner")
                || IsTruthyMember(spawner, "IsSpawningWyrdSpawns")
                || IsTruthyMember(spawner, "_isSpawningWyrdSpawns")
                || IsTruthyMember(spawner, "_spawnOnlyOnAmbush");
        }

        private bool ShouldControlSpawner(object spawner, string gateName)
        {
            bool isHostileToHero;
            if (TryReadBoolMember(spawner, "IsHostileToHero", out isHostileToHero))
            {
                return isHostileToHero;
            }

            if (!HasNpcSpawnTemplate(spawner))
            {
                return false;
            }

            return HasLikelyEnemyName(spawner);
        }

        private bool HasNpcSpawnTemplate(object spawner)
        {
            IEnumerable templates = GetMemberValue(spawner, "AllUniqueTemplates") as IEnumerable;
            if (templates == null || templates is string)
            {
                return false;
            }

            Type npcAttachmentType = AccessTools.TypeByName(NpcAttachmentTypeName);
            if (npcAttachmentType == null)
            {
                return false;
            }

            try
            {
                foreach (object template in templates)
                {
                    UnityEngine.Component component = template as UnityEngine.Component;
                    if (component == null)
                    {
                        continue;
                    }

                    if (component.GetComponent(npcAttachmentType) != null || component.GetComponentInChildren(npcAttachmentType) != null)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private bool HasLikelyEnemyName(object spawner)
        {
            string hints = (DescribeSpawner(spawner) + ";" + DescribeTemplates(GetMemberValue(spawner, "AllUniqueTemplates"))).ToLowerInvariant();
            return hints.Contains("enemy")
                || hints.Contains("skeleton")
                || hints.Contains("corpse")
                || hints.Contains("redcap")
                || hints.Contains("gobbler")
                || hints.Contains("grindylow")
                || hints.Contains("wyrd")
                || hints.Contains("zombie")
                || hints.Contains("ghoul")
                || hints.Contains("bandit")
                || hints.Contains("highwayman")
                || hints.Contains("deranged")
                || hints.Contains("wolf")
                || hints.Contains("wolves")
                || hints.Contains("bear")
                || hints.Contains("boar")
                || hints.Contains("spider");
        }

        private void LogNonEnemySpawnerBypass(object spawner, string gateName)
        {
            if (!_diagnostics.Value)
            {
                return;
            }

            string description = DescribeSpawner(spawner);
            LogDiagnosticRateLimited(
                "non-enemy-spawner|" + gateName + "|" + StableHash(description),
                "Ignored non-enemy spawner at " + gateName + ". spawner=" + description + ".");
        }

        private void MarkSpecialSpawnedLocation(object location)
        {
            if (location == null)
            {
                return;
            }

            _specialSpawnedLocations.Remove(location);
            _specialSpawnedLocations.Add(location, new SpecialSpawnedLocation());
        }

        private bool IsSpecialSpawnedLocation(object location)
        {
            SpecialSpawnedLocation ignored;
            return location != null && _specialSpawnedLocations.TryGetValue(location, out ignored);
        }

        private void LogSpecialSpawnBypass(object spawner, string gateName)
        {
            if (!_diagnostics.Value)
            {
                return;
            }

            LogDiagnosticRateLimited(
                "special-spawn-bypass|" + gateName + "|" + DescribeObject(spawner),
                "Ignored special spawn gate at " + gateName + ". spawner=" + DescribeSpawner(spawner) + ".");
        }

        private void LogAllowedSpawnAttempt(string key, object spawner, string gateName, RespawnLock respawnLock)
        {
            double lastClear;
            string lastClearText = TryReadLastClearPlaySeconds(spawner, out lastClear)
                ? (lastClear / 3600d).ToString("0.##", CultureInfo.InvariantCulture) + "h"
                : "none";

            LogDiagnosticRateLimited(
                key + "|allowed|" + gateName,
                "Allowed spawn attempt at " + gateName +
                ". hasLock=" + (respawnLock != null).ToString(CultureInfo.InvariantCulture) +
                "; killedState=" + HasKilledLocationState(spawner).ToString(CultureInfo.InvariantCulture) +
                "; lastClearPlay=" + lastClearText +
                "; spawner=" + DescribeSpawner(spawner) + ".");
        }

        private void LogDiagnosticRateLimited(string key, string message)
        {
            if (!_diagnostics.Value)
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            DateTime next;
            if (_nextDiagnosticLogUtc.TryGetValue(key, out next) && utcNow < next)
            {
                return;
            }

            float interval = Math.Max(0.5f, _blockedLogIntervalSeconds.Value);
            _nextDiagnosticLogUtc[key] = utcNow.AddSeconds(interval);
            Log.LogInfo(message);
        }

        private bool ShouldBlockRespawn(string key, object spawner, double now, string timeSource, out RespawnLock respawnLock)
        {
            respawnLock = null;

            RespawnMode mode = _respawnMode.Value;
            if (mode == RespawnMode.Disabled)
            {
                RespawnLock lockState;
                if (_locks.TryGetValue(key, out lockState))
                {
                    respawnLock = lockState;
                    return true;
                }

                double lastClear;
                if (HasKilledLocationState(spawner) || TryReadLastClearPlaySeconds(spawner, out lastClear))
                {
                    RegisterLock(key, spawner, now, timeSource, "disabled-mode", false);
                    _locks.TryGetValue(key, out respawnLock);
                    return true;
                }

                return false;
            }

            if (!_locks.TryGetValue(key, out respawnLock))
            {
                return false;
            }

            double durationSeconds = GetRespawnDelaySeconds();
            if (durationSeconds <= 0d)
            {
                _locks.Remove(key);
                _expiredKeys.Add(key);
                return false;
            }

            if (!String.Equals(respawnLock.TimeSource, timeSource, StringComparison.Ordinal))
            {
                respawnLock.StartSeconds = now;
                respawnLock.TimeSource = timeSource;
                respawnLock.Reason = "time-source-changed";
                if (_diagnostics.Value)
                {
                    Log.LogInfo("Respawn lock time source changed; restarting lock. " + DescribeLock(key, respawnLock, now, durationSeconds));
                }
            }

            double elapsed = Math.Max(0d, now - respawnLock.StartSeconds);
            if (elapsed < durationSeconds)
            {
                return true;
            }

            _locks.Remove(key);
            _expiredKeys.Add(key);
            if (_diagnostics.Value)
            {
                Log.LogInfo("Respawn lock expired. " + DescribeLock(key, respawnLock, now, durationSeconds));
            }

            return false;
        }

        private void RegisterLock(string key, object spawner, double now, string timeSource, string reason, bool resetExpired)
        {
            if (resetExpired)
            {
                _expiredKeys.Remove(key);
            }

            RespawnLock respawnLock = new RespawnLock();
            respawnLock.StartSeconds = now;
            respawnLock.TimeSource = timeSource;
            respawnLock.Reason = reason;
            respawnLock.SpawnerDescription = DescribeSpawner(spawner);
            _locks[key] = respawnLock;

            if (_diagnostics.Value)
            {
                Log.LogInfo("Registered respawn lock. " + DescribeLock(key, respawnLock, now, GetRespawnDelaySeconds()));
            }
        }

        private void LogBlocked(string key, RespawnLock respawnLock, double now, string timeSource, string gateName)
        {
            if (!_diagnostics.Value || respawnLock == null)
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            DateTime next;
            string logKey = key + "|blocked|" + gateName;
            if (_nextBlockLogUtc.TryGetValue(logKey, out next) && utcNow < next)
            {
                return;
            }

            float interval = Math.Max(0.5f, _blockedLogIntervalSeconds.Value);
            _nextBlockLogUtc[logKey] = utcNow.AddSeconds(interval);
            Log.LogInfo("Blocked respawn at " + gateName + ". " + DescribeLock(key, respawnLock, now, GetRespawnDelaySeconds()));
        }

        private bool IsActive()
        {
            return _enabled != null && _enabled.Value;
        }

        private double GetRespawnDelaySeconds()
        {
            switch (_respawnMode.Value)
            {
                case RespawnMode.Vanilla:
                    return 2d * 3600d;
                case RespawnMode.Fast6Hours:
                    return 6d * 3600d;
                case RespawnMode.Default24Hours:
                    return 24d * 3600d;
                case RespawnMode.Slow72Hours:
                    return 72d * 3600d;
                case RespawnMode.VerySlow168Hours:
                    return 168d * 3600d;
                case RespawnMode.Custom:
                    return Math.Max(0d, _customRespawnHours.Value) * 3600d;
                case RespawnMode.Disabled:
                    return Double.PositiveInfinity;
                default:
                    return 24d * 3600d;
            }
        }

        private bool TryGetCurrentTimeSeconds(object spawner, out double seconds, out string source)
        {
            seconds = 0d;
            source = "weather";

            object gameRealTime = GetMemberValue(spawner, "GameRealTime");
            if (gameRealTime == null)
            {
                return false;
            }

            object weatherTime = GetMemberValue(gameRealTime, "WeatherTime");
            if (TryGetNumericMember(weatherTime, "TotalSeconds", out seconds))
            {
                return true;
            }

            object date = GetMemberValue(weatherTime, "Date");
            if (date is DateTime)
            {
                seconds = ((DateTime)date).Ticks / (double)TimeSpan.TicksPerSecond;
                return true;
            }

            return false;
        }

        private bool TryReadLastClearPlaySeconds(object spawner, out double seconds)
        {
            seconds = 0d;
            if (_lastClearOfGroupField == null || spawner == null)
            {
                return false;
            }

            try
            {
                object value = _lastClearOfGroupField.GetValue(spawner);
                if (value is double)
                {
                    seconds = (double)value;
                    return !Double.IsInfinity(seconds) && !Double.IsNaN(seconds);
                }
            }
            catch
            {
            }

            return false;
        }

        private bool HasKilledLocationState(object spawner)
        {
            if (_killedLocationsField == null || spawner == null)
            {
                return false;
            }

            try
            {
                object value = _killedLocationsField.GetValue(spawner);
                ICollection collection = value as ICollection;
                if (collection != null)
                {
                    return collection.Count > 0;
                }

                IEnumerable enumerable = value as IEnumerable;
                if (enumerable == null)
                {
                    return false;
                }

                IEnumerator enumerator = enumerable.GetEnumerator();
                try
                {
                    return enumerator.MoveNext();
                }
                finally
                {
                    IDisposable disposable = enumerator as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryDiscardLocation(object location)
        {
            if (location == null)
            {
                return false;
            }

            object discarded = GetMemberValue(location, "HasBeenDiscarded");
            if (discarded is bool && (bool)discarded)
            {
                return true;
            }

            MethodInfo discardMethod = FindInstanceMethod(location.GetType(), "Discard");
            if (discardMethod == null)
            {
                return false;
            }

            try
            {
                discardMethod.Invoke(location, null);
                return true;
            }
            catch (Exception ex)
            {
                if (_diagnostics.Value)
                {
                    Log.LogWarning("Failed to discard spawned location from a locked spawner: " + ex.GetBaseException().Message);
                }

                return false;
            }
        }

        private string GetSpawnerKey(object spawner)
        {
            if (spawner == null)
            {
                return "null";
            }

            CachedKey cached;
            if (_keyCache.TryGetValue(spawner, out cached))
            {
                return cached.Value;
            }

            string raw = BuildSpawnerKey(spawner);
            string key = StableHash(raw) + "|" + raw;
            _keyCache.Add(spawner, new CachedKey(key));
            return key;
        }

        private string BuildSpawnerKey(object spawner)
        {
            List<string> parts = new List<string>();
            parts.Add("type=" + DescribeObject(spawner));
            AddPart(parts, "id", GetStringMember(spawner, "ID"));
            AddPart(parts, "coords", FormatVector(GetMemberValue(spawner, "Coords")));

            object parent = GetMemberValue(spawner, "ParentModel");
            if (parent != null)
            {
                AddPart(parts, "parentId", GetStringMember(parent, "ID"));
                AddPart(parts, "parentDebug", GetStringMember(parent, "DebugName"));
                AddPart(parts, "parentName", GetStringMember(parent, "DisplayName"));
            }

            string templates = DescribeTemplates(GetMemberValue(spawner, "AllUniqueTemplates"));
            AddPart(parts, "templates", templates);

            return String.Join(";", parts.ToArray());
        }

        private string DescribeSpawner(object spawner)
        {
            if (spawner == null)
            {
                return "null";
            }

            string id = GetStringMember(spawner, "ID");
            object parent = GetMemberValue(spawner, "ParentModel");
            string parentId = parent == null ? "" : GetStringMember(parent, "ID");
            string parentDebug = parent == null ? "" : GetStringMember(parent, "DebugName");
            string coords = FormatVector(GetMemberValue(spawner, "Coords"));

            List<string> parts = new List<string>();
            parts.Add(DescribeObject(spawner));
            AddPart(parts, "id", id);
            AddPart(parts, "parent", parentId);
            AddPart(parts, "parentDebug", parentDebug);
            AddPart(parts, "coords", coords);
            return String.Join("; ", parts.ToArray());
        }

        private string DescribeTemplates(object templatesObject)
        {
            IEnumerable enumerable = templatesObject as IEnumerable;
            if (enumerable == null || templatesObject is string)
            {
                return "";
            }

            List<string> templates = new List<string>();
            try
            {
                foreach (object template in enumerable)
                {
                    if (template == null)
                    {
                        continue;
                    }

                    string guid = GetStringMember(template, "GUID");
                    if (String.IsNullOrWhiteSpace(guid))
                    {
                        guid = GetStringMember(template, "Guid");
                    }
                    if (String.IsNullOrWhiteSpace(guid))
                    {
                        guid = GetStringMember(template, "TemplateGuid");
                    }

                    string name = GetStringMember(template, "name");
                    if (String.IsNullOrWhiteSpace(name))
                    {
                        name = GetStringMember(template, "Name");
                    }
                    if (String.IsNullOrWhiteSpace(name))
                    {
                        name = GetStringMember(template, "DebugName");
                    }
                    if (String.IsNullOrWhiteSpace(name))
                    {
                        name = GetStringMember(template, "DisplayName");
                    }

                    string value = guid;
                    if (!String.IsNullOrWhiteSpace(name)
                        && (String.IsNullOrWhiteSpace(value) || !String.Equals(value, name, StringComparison.Ordinal)))
                    {
                        value = String.IsNullOrWhiteSpace(value) ? name : value + "|" + name;
                    }
                    if (String.IsNullOrWhiteSpace(value))
                    {
                        value = template.ToString();
                    }

                    templates.Add(value);
                    if (templates.Count >= 12)
                    {
                        break;
                    }
                }
            }
            catch
            {
            }

            templates.Sort(StringComparer.Ordinal);
            return String.Join(",", templates.ToArray());
        }

        private static void AddPart(List<string> parts, string name, string value)
        {
            if (!String.IsNullOrWhiteSpace(value))
            {
                parts.Add(name + "=" + value);
            }
        }

        private static string FormatVector(object vector)
        {
            if (vector == null)
            {
                return "";
            }

            double x;
            double y;
            double z;
            if (TryGetNumericMember(vector, "x", out x) &&
                TryGetNumericMember(vector, "y", out y) &&
                TryGetNumericMember(vector, "z", out z))
            {
                return Math.Round(x, 1).ToString("0.0", CultureInfo.InvariantCulture) + "," +
                    Math.Round(y, 1).ToString("0.0", CultureInfo.InvariantCulture) + "," +
                    Math.Round(z, 1).ToString("0.0", CultureInfo.InvariantCulture);
            }

            return vector.ToString();
        }

        private static string GetStringMember(object instance, string memberName)
        {
            object value = GetMemberValue(instance, memberName);
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null || String.IsNullOrEmpty(memberName))
            {
                return null;
            }

            Type type = instance.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (type != null)
            {
                try
                {
                    PropertyInfo property = type.GetProperty(memberName, flags);
                    if (property != null && property.GetIndexParameters().Length == 0)
                    {
                        return property.GetValue(instance, null);
                    }

                    FieldInfo field = type.GetField(memberName, flags);
                    if (field != null)
                    {
                        return field.GetValue(instance);
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

        private static bool IsTruthyMember(object instance, string memberName)
        {
            object value = GetMemberValue(instance, memberName);
            return value is bool && (bool)value;
        }

        private static bool TryReadBoolMember(object instance, string memberName, out bool result)
        {
            result = false;
            object value = GetMemberValue(instance, memberName);
            if (value is bool)
            {
                result = (bool)value;
                return true;
            }

            return false;
        }

        private static bool TryGetNumericMember(object instance, string memberName, out double value)
        {
            value = 0d;
            object raw = GetMemberValue(instance, memberName);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return !Double.IsNaN(value);
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (type != null)
            {
                MethodInfo method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static string StableHash(string value)
        {
            if (value == null)
            {
                value = "";
            }

            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder builder = new StringBuilder(12);
                for (int i = 0; i < 6 && i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string DescribeObject(object instance)
        {
            return instance == null ? "null" : instance.GetType().FullName;
        }

        private static string DescribeLock(string key, RespawnLock respawnLock, double now, double durationSeconds)
        {
            double elapsed = respawnLock == null ? 0d : Math.Max(0d, now - respawnLock.StartSeconds);
            double remaining = Double.IsPositiveInfinity(durationSeconds) ? Double.PositiveInfinity : Math.Max(0d, durationSeconds - elapsed);
            string remainingText = Double.IsPositiveInfinity(remaining)
                ? "forever"
                : (remaining / 3600d).ToString("0.##", CultureInfo.InvariantCulture) + "h remaining";
            string keyPrefix = key;
            int separator = key.IndexOf('|');
            if (separator >= 0)
            {
                keyPrefix = key.Substring(0, separator);
            }

            return "key=" + keyPrefix +
                "; source=" + (respawnLock == null ? "unknown" : respawnLock.TimeSource) +
                "; reason=" + (respawnLock == null ? "unknown" : respawnLock.Reason) +
                "; elapsed=" + (elapsed / 3600d).ToString("0.##", CultureInfo.InvariantCulture) + "h" +
                "; " + remainingText +
                "; spawner=" + (respawnLock == null ? "unknown" : respawnLock.SpawnerDescription) + ".";
        }

        private sealed class CachedKey
        {
            internal readonly string Value;

            internal CachedKey(string value)
            {
                Value = value;
            }
        }

        private sealed class RespawnLock
        {
            internal double StartSeconds;
            internal string TimeSource;
            internal string Reason;
            internal string SpawnerDescription;
        }

        private sealed class SpecialSpawnedLocation
        {
        }

        private static class CooldownConditionPatch
        {
            public static void Postfix(object __instance, ref bool __result)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyCooldownGate(__instance, ref __result);
                }
            }
        }

        private static class CanSpawnPatch
        {
            public static void Postfix(object __instance, ref bool __result)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplySpawnGate(__instance, ref __result, "can-spawn");
                }
            }
        }

        private static class CanSpawnAmbushPatch
        {
            public static void Postfix(object __instance, ref bool __result)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplySpawnGate(__instance, ref __result, "can-spawn-ambush");
                }
            }
        }

        private static class IsValidStatePatch
        {
            public static void Postfix(object __instance, ref bool __result)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplySpawnGate(__instance, ref __result, "is-valid-state");
                }
            }
        }

        private static class ShouldSpawnPatch
        {
            public static void Postfix(object __instance, ref bool __result)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplySpawnGate(__instance, ref __result, "should-spawn");
                }
            }
        }

        private static class SpawnPrefabInternalPatch
        {
            public static bool Prefix(object __instance)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                return plugin == null || plugin.BeforeSpawnInternal(__instance, "spawn-prefab-internal");
            }
        }

        private static class OnLocationSpawnedPatch
        {
            public static void Postfix(object __instance, object __0, int __1)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.AfterLocationSpawned(__instance, __0, __1);
                }
            }
        }

        private static class AfterTimeSkippedPatch
        {
            public static void Postfix(object __instance)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterSpawnerIfKilledState(__instance, "time-skipped");
                }
            }
        }

        private static class AfterHeroTeleportPatch
        {
            public static void Postfix(object __instance)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterSpawnerIfKilledState(__instance, "hero-teleport");
                }
            }
        }

        private static class OnRestorePatch
        {
            public static void Postfix(object __instance)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterSpawnerIfKilledState(__instance, "restore");
                }
            }
        }

        private static class AfterLocationKilledPatch
        {
            public static void Postfix(object __instance, object __1)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterKilledSpawner(__instance, __1, "after-kill");
                }
            }
        }

        private static class OnLocationDiscardedOrKilledPatch
        {
            public static void Postfix(object __instance)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterSpawnerIfKilledState(__instance, "discard-or-kill");
                }
            }
        }

        private static class SceneInitializationEndedPatch
        {
            public static void Postfix(object __instance)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterSpawnerIfKilledState(__instance, "scene-restore");
                }
            }
        }

        private static class SpawnerInitPatch
        {
            public static void Postfix(object __instance)
            {
                EnemyRespawnControlPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterSpawnerIfKilledState(__instance, "init");
                }
            }
        }
    }

    public enum RespawnMode
    {
        Vanilla,
        Fast6Hours,
        Default24Hours,
        Slow72Hours,
        VerySlow168Hours,
        Custom,
        Disabled
    }
}
