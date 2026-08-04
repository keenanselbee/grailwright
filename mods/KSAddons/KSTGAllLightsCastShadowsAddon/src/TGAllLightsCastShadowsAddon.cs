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
[assembly: AssemblyVersion("1.1.9.0")]
[assembly: AssemblyFileVersion("1.1.9.0")]

namespace TGAllLightsCastShadowsAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        ParentPluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.tg-all-lights-cast-shadows-addon";
        public const string PluginName = "TG All Lights Cast Shadows Addon";
        public const string PluginVersion = "1.1.9";
        public const string ParentPluginGuid =
            "com.wessberg.tgalllightscastshadows";
        private const int ConfigSchemaVersion = 2;
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
        private bool _hdrpResolved;
        private Type _hdAdditionalLightDataType;
        private MethodInfo _hdEnableShadowsMethod;
        private MemberInfo _hdShadowDimmerMember;
        private MemberInfo _hdVolumetricShadowDimmerMember;
        private ConfigEntry<bool> _protectBonfireLights;
        private ConfigEntry<string> _additionalExcludedLightPathFragments;
        private ConfigEntry<bool> _verboseExclusionLogging;
        private string[] _excludedFragments = new string[0];
        private string _pendingPreservedAdditionalExcludedLightPathFragments;
        private bool _hasPendingPreservedAdditionalExcludedLightPathFragments;
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
                        nameof(Patches.BeforeApplyAllLights)),
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.AfterApplyAllLights)));
                _harmony.Patch(
                    restoreAllLightsMethod,
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.AfterRestoreAllLights)));

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
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
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
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _protectBonfireLights = Config.Bind(
                "Excluded Lights",
                "ProtectBonfireLights",
                true,
                "Prevents selected bonfire/campfire style lights from being upgraded to cast shadows.");
            _additionalExcludedLightPathFragments = Config.Bind(
                "Excluded Lights",
                "AdditionalExcludedLightPathFragments",
                "",
                "Optional comma-separated transform name fragments to exclude in addition to the addon's built-in bonfire and campfire names.");
            _verboseExclusionLogging = Config.Bind(
                "Excluded Lights",
                "VerboseExclusionLogging",
                false,
                "Logs each excluded light path once per scene. Useful for finding exact runtime names.");

            RestorePreservedAdditionalExcludedLightPathFragments();
            RefreshExcludedFragments();
            _additionalExcludedLightPathFragments.SettingChanged +=
                OnAdditionalExcludedLightPathFragmentsChanged;
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

            CapturePreservedAdditionalExcludedLightPathFragments(
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
                ClearPendingPreservedAdditionalExcludedLightPathFragments();

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

        private void CapturePreservedAdditionalExcludedLightPathFragments(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedAdditionalExcludedLightPathFragments();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                if (!string.Equals(currentSection, "Excluded Lights", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string settingName = line.Substring(0, separatorIndex).Trim();
                if (!string.Equals(
                        settingName,
                        "AdditionalExcludedLightPathFragments",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string preservedValue;
                if (profile.TryGetCustomizedValue(
                    currentSection,
                    settingName,
                    out preservedValue))
                {
                    _pendingPreservedAdditionalExcludedLightPathFragments =
                        preservedValue;
                    _hasPendingPreservedAdditionalExcludedLightPathFragments =
                        true;
                }
            }
        }

        private void RestorePreservedAdditionalExcludedLightPathFragments()
        {
            if (!_hasPendingPreservedAdditionalExcludedLightPathFragments
                || _additionalExcludedLightPathFragments == null)
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                _additionalExcludedLightPathFragments,
                _pendingPreservedAdditionalExcludedLightPathFragments,
                out clamped))
            {
                ClearPendingPreservedAdditionalExcludedLightPathFragments();
                return;
            }
            Logger.LogInfo(
                "Preserved the additional excluded-light path fragments across the config schema reset.");
            ClearPendingPreservedAdditionalExcludedLightPathFragments();
        }

        private void ClearPendingPreservedAdditionalExcludedLightPathFragments()
        {
            _pendingPreservedAdditionalExcludedLightPathFragments = null;
            _hasPendingPreservedAdditionalExcludedLightPathFragments = false;
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
            if (_additionalExcludedLightPathFragments != null)
            {
                _additionalExcludedLightPathFragments.SettingChanged -=
                    OnAdditionalExcludedLightPathFragmentsChanged;
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
