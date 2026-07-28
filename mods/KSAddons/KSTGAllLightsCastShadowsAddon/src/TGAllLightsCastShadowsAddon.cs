using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("TG All Lights Cast Shadows Addon")]
[assembly: AssemblyDescription("Companion addon for TG All Lights Cast Shadows shadow state and excluded bonfire lights")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("TG All Lights Cast Shadows Addon")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

namespace TGAllLightsCastShadowsAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        ParentPluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.tg-all-lights-cast-shadows-addon";
        public const string PluginName = "TG All Lights Cast Shadows Addon";
        public const string PluginVersion = "1.1.1";
        public const string ParentPluginGuid =
            "com.wessberg.tgalllightscastshadows";
        private const int ConfigSchemaVersion = 1;

        internal static Plugin Instance { get; private set; }

        private Harmony _harmony;
        private bool _originalShadowQualityCaptured;
        private ShadowQuality _originalShadowQuality;
        private FieldInfo _activeLightsField;
        private FieldInfo _originalStatesField;
        private FieldInfo _originalShadowsField;
        private FieldInfo _originalShadowStrengthField;
        private bool _hdrpResolved;
        private Type _hdAdditionalLightDataType;
        private MethodInfo _hdEnableShadowsMethod;
        private MemberInfo _hdShadowDimmerMember;
        private MemberInfo _hdVolumetricShadowDimmerMember;
        private ConfigEntry<bool> _protectBonfireLights;
        private ConfigEntry<string> _excludedLightPathFragments;
        private ConfigEntry<bool> _verboseExclusionLogging;
        private string[] _excludedFragments = new string[0];
        private readonly Dictionary<int, ProtectedLightState> _protectedLights =
            new Dictionary<int, ProtectedLightState>();
        private readonly HashSet<int> _loggedExcludedLights =
            new HashSet<int>();

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
                if (applyAllLightsMethod == null
                    || restoreAllLightsMethod == null)
                {
                    throw new MissingMethodException(
                        "Could not find the parent light mod's shadow methods.");
                    }

                InitializeConfig();
                InitializeParentReflection(shadowManagerType);

                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(
                    applyAllLightsMethod,
                    prefix: new HarmonyMethod(
                        typeof(Patches),
                        "BeforeApplyAllLights"),
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        "AfterApplyAllLights"));
                _harmony.Patch(
                    restoreAllLightsMethod,
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        "AfterRestoreAllLights"));

                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded; global shadow-state restoration and excluded-light protection are active.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    PluginName + " failed to initialize: " + exception);
            }
        }

        internal void BeforeApplyAllLights()
        {
            if (_originalShadowQualityCaptured)
            {
                ProtectExcludedLightsBeforeParentScan();
                return;
            }

            _originalShadowQuality = QualitySettings.shadows;
            _originalShadowQualityCaptured = true;
            Logger.LogInfo(
                "Captured global shadow quality: "
                + _originalShadowQuality);
            ProtectExcludedLightsBeforeParentScan();
        }

        internal void AfterApplyAllLights()
        {
            RestoreProtectedLightsAfterParentScan();
            RestoreExcludedLightsTouchedByParent();
        }

        internal void AfterRestoreAllLights()
        {
            RestoreProtectedLightsAfterParentScan();
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

        private void InitializeConfig()
        {
            ResetConfigIfSchemaChanged();

            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                "Configuration layout version. Older layouts are backed up and regenerated.");
            _protectBonfireLights = Config.Bind(
                "Excluded Lights",
                "ProtectBonfireLights",
                true,
                "Prevents selected bonfire/campfire style lights from being upgraded to cast shadows.");
            _excludedLightPathFragments = Config.Bind(
                "Excluded Lights",
                "ExcludedLightPathFragments",
                "WyrdNight_Repeller_Bonfire,Repeller_Bonfire,Bonfire,Campfire",
                "Comma-separated transform name fragments to exclude from the parent mod's shadow upgrades.");
            _verboseExclusionLogging = Config.Bind(
                "Excluded Lights",
                "VerboseExclusionLogging",
                false,
                "Logs each excluded light path once per scene. Useful for finding exact runtime names.");

            RefreshExcludedFragments();
            _excludedLightPathFragments.SettingChanged +=
                OnExcludedLightPathFragmentsChanged;
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
                        "Could not restore the previous TG All Lights Cast Shadows Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset TG All Lights Cast Shadows Addon config schema. Original config was left in place when possible.",
                    exception);
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
        }

        private void RefreshExcludedFragments()
        {
            string raw = _excludedLightPathFragments != null
                ? _excludedLightPathFragments.Value
                : string.Empty;
            string[] parts = raw.Split(new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries);
            List<string> fragments = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string fragment = parts[i].Trim();
                if (fragment.Length > 0)
                {
                    fragments.Add(fragment);
                }
            }

            _excludedFragments = fragments.ToArray();
        }

        private void OnExcludedLightPathFragmentsChanged(
            object sender,
            EventArgs args)
        {
            RefreshExcludedFragments();
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

            _hdrpResolved = true;
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
            if (_excludedLightPathFragments != null)
            {
                _excludedLightPathFragments.SettingChanged -=
                    OnExcludedLightPathFragmentsChanged;
            }

            RestoreProtectedLightsAfterParentScan();

            if (_originalShadowQualityCaptured)
            {
                QualitySettings.shadows = _originalShadowQuality;
                _originalShadowQualityCaptured = false;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            Instance = null;
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
    }

    internal static class Patches
    {
        internal static void BeforeApplyAllLights()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.BeforeApplyAllLights();
            }
        }

        internal static void AfterApplyAllLights()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.AfterApplyAllLights();
            }
        }

        internal static void AfterRestoreAllLights()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.AfterRestoreAllLights();
            }
        }
    }
}
