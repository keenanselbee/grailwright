using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

[assembly: AssemblyTitle("More Weapon Loadouts Addon")]
[assembly: AssemblyDescription("Companion addon for Owrocc More Weapon Loadouts")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("More Weapon Loadouts Addon")]
[assembly: AssemblyVersion("0.1.4.0")]
[assembly: AssemblyFileVersion("0.1.4.0")]

namespace MoreWeaponLoadoutsAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("owrocc.MoreWeaponSlots", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MoreWeaponLoadoutsAddonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.more-weapon-loadouts-addon";
        public const string PluginName = "More Weapon Loadouts Addon";
        public const string PluginVersion = "0.1.4";
        private const int ConfigSchemaVersion = 1;

        internal static MoreWeaponLoadoutsAddonPlugin Instance { get; private set; }

        private ConfigEntry<bool> _fixDuplicateVirtualLoadoutEntries;
        private ConfigEntry<bool> _redirectVanillaWeaponLoadoutKeys;
        private ConfigEntry<int> _redirectFirstVanillaIndex;
        private ConfigEntry<int> _redirectSlotCount;
        private ConfigEntry<int> _redirectTargetSlotOffset;
        private ConfigEntry<bool> _reprimeAfterLoad;
        private ConfigEntry<bool> _reapplyCurrentSlotAfterLoad;
        private ConfigEntry<float> _postLoadDelaySeconds;
        private ConfigEntry<bool> _logFixes;

        private Harmony _harmony;
        private bool _mwsBridgeResolved;
        private Type _mwsType;
        private PropertyInfo _mwsInstanceProperty;
        private FieldInfo _mwsInstanceField;
        private Action<int> _mwsActivateHotkeySlot;
        private MethodInfo _mwsPrimeAutoTrackBaseline;
        private FieldInfo _mwsCurrentVirtualSlotField;
        private FieldInfo _mwsLoadHotkeysField;
        private bool _missingMwsLogged;
        private bool _duplicateFixLogged;
        private bool _redirectLogged;
        private bool _postLoadRefreshLogged;
        private Coroutine _postLoadCoroutine;

        private void Awake()
        {
            Instance = this;

            try
            {
                BindConfig();
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(MoreWeaponLoadoutsAddonPlugin).Assembly);
                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_postLoadCoroutine != null)
            {
                StopCoroutine(_postLoadCoroutine);
                _postLoadCoroutine = null;
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

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                "Configuration layout version. Older layouts are backed up and regenerated.");
            _fixDuplicateVirtualLoadoutEntries = Config.Bind(
                "Duplicate Save Entry Fix",
                "FixDuplicateVirtualLoadoutEntries",
                true,
                "When a save archive contains duplicate VirtualLoadouts.json entries, read the useful/latest one instead of a stale empty entry.");
            _logFixes = Config.Bind(
                "Duplicate Save Entry Fix",
                "LogFixes",
                true,
                "Log once when this addon corrects a duplicate virtual-loadout save entry.");

            _redirectVanillaWeaponLoadoutKeys = Config.Bind(
                "Input Redirect",
                "RedirectVanillaWeaponLoadoutKeys",
                true,
                "Redirect vanilla gameplay weapon loadout keys to More Weapon Loadouts virtual slots.");
            _redirectFirstVanillaIndex = Config.Bind(
                "Input Redirect",
                "FirstVanillaLoadoutIndex",
                0,
                new ConfigDescription(
                    "First vanilla weapon loadout index to redirect. Vanilla index 0 is the number-row 1 loadout.",
                    new AcceptableValueRange<int>(0, 9)));
            _redirectSlotCount = Config.Bind(
                "Input Redirect",
                "RedirectSlotCount",
                4,
                new ConfigDescription(
                    "Number of consecutive vanilla weapon loadout keys to redirect.",
                    new AcceptableValueRange<int>(0, 10)));
            _redirectTargetSlotOffset = Config.Bind(
                "Input Redirect",
                "TargetMwsSlotOffset",
                1,
                new ConfigDescription(
                    "More Weapon Loadouts slot offset. With the default, vanilla index 0 maps to MWS slot 1.",
                    new AcceptableValueRange<int>(0, 100)));

            _reprimeAfterLoad = Config.Bind(
                "Post Load Refresh",
                "ReprimeAutoTrackAfterLoad",
                true,
                "After More Weapon Loadouts reads a save, refresh its auto-track baseline once after a short delay.");
            _reapplyCurrentSlotAfterLoad = Config.Bind(
                "Post Load Refresh",
                "ReapplyCurrentVirtualSlotAfterLoad",
                false,
                "Also re-activate the saved current virtual slot after loading. Leave false unless the equipped weapons still come back wrong after the duplicate-entry fix.");
            _postLoadDelaySeconds = Config.Bind(
                "Post Load Refresh",
                "PostLoadDelaySeconds",
                0.75f,
                new ConfigDescription(
                    "Delay before the optional post-load More Weapon Loadouts refresh.",
                    new AcceptableValueRange<float>(0.1f, 5f)));

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
                        "Could not restore the previous More Weapon Loadouts Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset More Weapon Loadouts Addon config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        internal bool TryChooseVirtualLoadoutEntry(
            object cloudService,
            string fileName,
            ref byte[] data,
            ref bool result)
        {
            if (!_fixDuplicateVirtualLoadoutEntries.Value
                || !string.Equals(fileName, "VirtualLoadouts.json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ZipArchive archive = GetActiveSaveArchive(cloudService);
            if (archive == null)
            {
                return false;
            }

            List<ZipArchiveEntry> entries = archive.Entries
                .Where(IsVirtualLoadoutEntry)
                .ToList();
            if (entries.Count <= 1)
            {
                return false;
            }

            byte[] bestData = null;
            int bestScore = int.MinValue;
            int bestIndex = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                byte[] entryData = ReadEntry(entries[i]);
                int score = ScoreVirtualLoadoutJson(entryData, i);
                if (score >= bestScore)
                {
                    bestScore = score;
                    bestData = entryData;
                    bestIndex = i;
                }
            }

            if (bestData == null)
            {
                return false;
            }

            data = bestData;
            result = true;

            if (_logFixes.Value && !_duplicateFixLogged)
            {
                _duplicateFixLogged = true;
                Logger.LogInfo(
                    "Using VirtualLoadouts.json duplicate entry "
                    + (bestIndex + 1)
                    + " of "
                    + entries.Count
                    + " from the active save archive.");
            }

            return true;
        }

        internal bool TryRedirectVanillaLoadoutKey(int vanillaIndex)
        {
            if (!_redirectVanillaWeaponLoadoutKeys.Value)
            {
                return false;
            }

            int firstIndex = _redirectFirstVanillaIndex.Value;
            int relativeIndex = vanillaIndex - firstIndex;
            if (relativeIndex < 0 || relativeIndex >= _redirectSlotCount.Value)
            {
                return false;
            }

            int mwsSlot = relativeIndex + _redirectTargetSlotOffset.Value;
            if (mwsSlot <= 0)
            {
                return false;
            }

            if (!ResolveMoreWeaponSlotsBridge()
                || _mwsActivateHotkeySlot == null)
            {
                LogMissingMwsOnce();
                return false;
            }

            object mws = GetMoreWeaponSlotsInstance();
            if (mws == null)
            {
                LogMissingMwsOnce();
                return false;
            }

            try
            {
                if (!IsMwsSlotHotkeyDown(mws, mwsSlot))
                {
                    _mwsActivateHotkeySlot(mwsSlot);
                }

                if (!_redirectLogged)
                {
                    _redirectLogged = true;
                    Logger.LogInfo(
                        "Coordinating vanilla gameplay weapon loadout keys with More Weapon Loadouts slots without duplicate activation.");
                }

                return true;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not redirect vanilla loadout key to More Weapon Loadouts slot "
                    + mwsSlot
                    + ": "
                    + Unwrap(exception).Message);
                return false;
            }
        }

        internal void SchedulePostLoadRefresh()
        {
            if (!_reprimeAfterLoad.Value && !_reapplyCurrentSlotAfterLoad.Value)
            {
                return;
            }

            if (_postLoadCoroutine != null)
            {
                StopCoroutine(_postLoadCoroutine);
            }

            _postLoadCoroutine = StartCoroutine(PostLoadRefreshCoroutine());
        }

        private IEnumerator PostLoadRefreshCoroutine()
        {
            yield return new WaitForSecondsRealtime(
                Mathf.Clamp(_postLoadDelaySeconds.Value, 0.1f, 5f));

            _postLoadCoroutine = null;
            object mws = GetMoreWeaponSlotsInstance();
            if (mws == null)
            {
                yield break;
            }

            if (_reapplyCurrentSlotAfterLoad.Value)
            {
                int? currentSlot = ReadNullableIntField(mws, "_currentVirtualSlot");
                if (currentSlot.HasValue && currentSlot.Value > 0)
                {
                    if (_mwsActivateHotkeySlot != null)
                    {
                        _mwsActivateHotkeySlot(currentSlot.Value);
                    }
                }
            }

            if (_reprimeAfterLoad.Value)
            {
                if (_mwsPrimeAutoTrackBaseline != null)
                {
                    _mwsPrimeAutoTrackBaseline.Invoke(mws, null);
                    if (!_postLoadRefreshLogged)
                    {
                        _postLoadRefreshLogged = true;
                        Logger.LogInfo("Refreshed More Weapon Loadouts auto-track baseline after save load.");
                    }
                }
            }
        }

        private bool ResolveMoreWeaponSlotsBridge()
        {
            if (_mwsBridgeResolved)
            {
                return _mwsType != null;
            }

            _mwsBridgeResolved = true;
            _mwsType = AccessTools.TypeByName(
                "owrocc.MoreWeaponSlots.VirtualLoadoutsPlugin");
            if (_mwsType == null)
            {
                return false;
            }

            _mwsInstanceProperty = AccessTools.Property(_mwsType, "Instance");
            _mwsInstanceField = AccessTools.Field(
                _mwsType,
                "<Instance>k__BackingField");
            _mwsPrimeAutoTrackBaseline = AccessTools.Method(
                _mwsType,
                "PrimeAutoTrackBaseline");
            _mwsCurrentVirtualSlotField = AccessTools.Field(
                _mwsType,
                "_currentVirtualSlot");
            _mwsLoadHotkeysField = AccessTools.Field(
                _mwsType,
                "_loadHotkeys");

            MethodInfo activate = AccessTools.Method(
                _mwsType,
                "ExternalActivateHotkeySlot",
                new[] { typeof(int) });
            if (activate == null)
            {
                return true;
            }

            try
            {
                _mwsActivateHotkeySlot =
                    (Action<int>)Delegate.CreateDelegate(
                        typeof(Action<int>),
                        activate);
            }
            catch
            {
                _mwsActivateHotkeySlot =
                    delegate(int slot)
                    {
                        activate.Invoke(null, new object[] { slot });
                    };
            }

            return true;
        }

        private bool IsMwsSlotHotkeyDown(object mws, int slot)
        {
            if (mws == null || _mwsLoadHotkeysField == null)
            {
                return false;
            }

            IDictionary hotkeys = _mwsLoadHotkeysField.GetValue(mws) as IDictionary;
            if (hotkeys == null || !hotkeys.Contains(slot))
            {
                return false;
            }

            ConfigEntry<KeyboardShortcut> hotkey =
                hotkeys[slot] as ConfigEntry<KeyboardShortcut>;
            return hotkey != null && hotkey.Value.IsDown();
        }

        private static ZipArchive GetActiveSaveArchive(object cloudService)
        {
            if (cloudService == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(
                cloudService.GetType(),
                "_activeSaveArchive");
            return field == null
                ? null
                : field.GetValue(cloudService) as ZipArchive;
        }

        private static bool IsVirtualLoadoutEntry(ZipArchiveEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            return string.Equals(
                    entry.FullName,
                    "VirtualLoadouts.json.data",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    entry.FullName,
                    "VirtualLoadouts.json",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] ReadEntry(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            using (MemoryStream memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static int ScoreVirtualLoadoutJson(byte[] bytes, int entryIndex)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return entryIndex;
            }

            int score = entryIndex + 1;
            try
            {
                JObject json = JObject.Parse(Encoding.UTF8.GetString(bytes));
                JToken loadouts = json["Loadouts"];
                JToken currentSlot = json["CurrentSlot"];
                if (loadouts != null && loadouts.HasValues)
                {
                    score += 1000;
                }

                if (currentSlot != null && currentSlot.Type != JTokenType.Null)
                {
                    score += 100;
                }
            }
            catch
            {
                score += bytes.Length > 0 ? 1 : 0;
            }

            return score;
        }

        private static object GetMoreWeaponSlotsInstance()
        {
            MoreWeaponLoadoutsAddonPlugin plugin = Instance;
            if (plugin == null || !plugin.ResolveMoreWeaponSlotsBridge())
            {
                return null;
            }

            if (plugin._mwsInstanceProperty != null)
            {
                return plugin._mwsInstanceProperty.GetValue(null, null);
            }

            return plugin._mwsInstanceField == null
                ? null
                : plugin._mwsInstanceField.GetValue(null);
        }

        private int? ReadNullableIntField(object instance, string name)
        {
            ResolveMoreWeaponSlotsBridge();
            FieldInfo field = string.Equals(
                    name,
                    "_currentVirtualSlot",
                    StringComparison.Ordinal)
                ? _mwsCurrentVirtualSlotField
                : AccessTools.Field(instance.GetType(), name);
            if (field == null)
            {
                return null;
            }

            object value = field.GetValue(instance);
            return value is int ? (int?)value : value as int?;
        }

        private void LogMissingMwsOnce()
        {
            if (_missingMwsLogged)
            {
                return;
            }

            _missingMwsLogged = true;
            Logger.LogWarning(
                "More Weapon Loadouts was not available; vanilla loadout key redirect is inactive.");
        }

        private static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocationException =
                exception as TargetInvocationException;
            return invocationException != null && invocationException.InnerException != null
                ? invocationException.InnerException
                : exception;
        }
    }

    [HarmonyPatch]
    internal static class CloudServiceTryLoadSlotFilePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] typeNames =
            {
                "Awaken.TG.Main.Saving.Cloud.Services.SteamCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.SteamNoCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.DebugCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.GogCloudService"
            };

            foreach (string typeName in typeNames)
            {
                Type type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    continue;
                }

                MethodInfo method = AccessTools.Method(
                    type,
                    "TryLoadSlotFile",
                    new[] { typeof(string), typeof(byte[]).MakeByRefType() });
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        private static void Postfix(
            object __instance,
            string fileName,
            ref byte[] data,
            ref bool __result)
        {
            MoreWeaponLoadoutsAddonPlugin plugin =
                MoreWeaponLoadoutsAddonPlugin.Instance;
            if (plugin == null)
            {
                return;
            }

            plugin.TryChooseVirtualLoadoutEntry(
                __instance,
                fileName,
                ref data,
                ref __result);
        }
    }

    [HarmonyPatch]
    internal static class VHeroKeysEquipLoadoutPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("Awaken.TG.Main.Heroes.VHeroKeys");
            return type == null
                ? null
                : AccessTools.Method(type, "EquipLoadout", new[] { typeof(int) });
        }

        private static bool Prefix(int index)
        {
            MoreWeaponLoadoutsAddonPlugin plugin =
                MoreWeaponLoadoutsAddonPlugin.Instance;
            return plugin == null || !plugin.TryRedirectVanillaLoadoutKey(index);
        }
    }

    [HarmonyPatch]
    internal static class MoreWeaponSlotsReadSessionPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName(
                "owrocc.MoreWeaponSlots.VirtualLoadoutsPlugin");
            return type == null
                ? null
                : AccessTools.Method(type, "OnEndLoadSlot_Prefix");
        }

        private static void Postfix()
        {
            MoreWeaponLoadoutsAddonPlugin plugin =
                MoreWeaponLoadoutsAddonPlugin.Instance;
            if (plugin != null)
            {
                plugin.SchedulePostLoadRefresh();
            }
        }
    }
}
