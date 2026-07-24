using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: AssemblyTitle("No Player Light")]
[assembly: AssemblyDescription("Disables the player HeroLight object in Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("No Player Light")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

namespace NoPlayerLight
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.no-player-light";
        public const string PluginName = "No Player Light";
        public const string PluginVersion = "1.2.1";

        private const float DiagnosticNoMatchesLogIntervalSeconds = 60f;
        private const float FallbackScanIntervalSeconds = 8f;
        private const string HeroLightObjectName = "HeroLight";
        private const int ConfigSchemaVersion = 3;
        private const int MaxExactHeroLightChildrenToLog = 64;

        private static readonly string[] DiagnosticNameFragments =
        {
            "HeroLight",
            "Light_HeroLight",
            "Spotlight_Hero"
        };

        private static readonly float[] SceneLoadRetryDelays = { 0f, 0.25f, 1f, 2f, 5f };

        private readonly HashSet<string> _loggedDiagnosticMatches =
            new HashSet<string>();
        private readonly HashSet<string> _loggedExactHeroLightMessages =
            new HashSet<string>();
        private readonly HashSet<int> _heroLightsDisabledByPlugin =
            new HashSet<int>();

        private ConfigEntry<bool> _disableHeroLight;
        private ConfigEntry<bool> _enableDiagnostics;
        private GameObject _heroLight;
        private Coroutine _sceneLoadRetries;
        private float _fallbackTimer;
        private float _lastNoDiagnosticMatchesLogTime = -DiagnosticNoMatchesLogIntervalSeconds;

        private void Awake()
        {
            BindConfig();

            SceneManager.sceneLoaded += OnSceneLoaded;
            bool foundExactHeroLight = FindAndApplyHeroLightState("startup");
            RunDiagnosticScan("startup", foundExactHeroLight);

            Logger.LogInfo(
                PluginName
                + " "
                + PluginVersion
                + " loaded; exact HeroLight suppression is "
                + (_disableHeroLight.Value ? "enabled" : "disabled")
                + ". Diagnostics: "
                + (_enableDiagnostics.Value ? "enabled" : "disabled")
                + ".");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_disableHeroLight != null)
            {
                _disableHeroLight.SettingChanged -=
                    OnDisableHeroLightSettingChanged;
            }

            if (_sceneLoadRetries != null)
            {
                StopCoroutine(_sceneLoadRetries);
                _sceneLoadRetries = null;
            }
        }

        private void Update()
        {
            if (_heroLight != null)
            {
                ApplyCachedHeroLightState("cached update");
            }

            _fallbackTimer += Time.unscaledDeltaTime;
            if (_fallbackTimer < FallbackScanIntervalSeconds)
            {
                return;
            }

            _fallbackTimer = 0f;
            bool foundExactHeroLight = FindAndApplyHeroLightState("fallback");
            RunDiagnosticScan("fallback", foundExactHeroLight);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _heroLight = null;
            _fallbackTimer = 0f;
            _loggedDiagnosticMatches.Clear();
            _loggedExactHeroLightMessages.Clear();
            _heroLightsDisabledByPlugin.Clear();
            _lastNoDiagnosticMatchesLogTime = -DiagnosticNoMatchesLogIntervalSeconds;

            if (_sceneLoadRetries != null)
            {
                StopCoroutine(_sceneLoadRetries);
            }

            _sceneLoadRetries = StartCoroutine(SceneLoadRetryScans());
        }

        private IEnumerator SceneLoadRetryScans()
        {
            for (int i = 0; i < SceneLoadRetryDelays.Length; i++)
            {
                float delay = SceneLoadRetryDelays[i];
                if (delay > 0f)
                {
                    yield return new WaitForSecondsRealtime(delay);
                }

                string reason =
                    "scene-load retry "
                    + delay.ToString("0.##", CultureInfo.InvariantCulture)
                    + "s";
                bool foundExactHeroLight = FindAndApplyHeroLightState(reason);
                RunDiagnosticScan(reason, foundExactHeroLight);
            }

            _sceneLoadRetries = null;
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                "Configuration layout version. Older layouts are backed up and regenerated.");
            _disableHeroLight = Config.Bind(
                "1. Core",
                "DisableHeroLight",
                true,
                "When true, disables the exact HeroLight GameObject. When false, leaves it enabled and re-enables any HeroLight object this plugin previously disabled.");
            _disableHeroLight.SettingChanged +=
                OnDisableHeroLightSettingChanged;

            _enableDiagnostics = Config.Bind(
                "Diagnostics",
                "EnableRuntimeScan",
                true,
                "When true, logs active GameObjects and Light components whose names contain HeroLight, Light_HeroLight, or Spotlight_Hero.");
            Config.Save();
        }

        private void OnDisableHeroLightSettingChanged(object sender, EventArgs args)
        {
            string reason = _disableHeroLight.Value
                ? "config DisableHeroLight=true"
                : "config DisableHeroLight=false";
            bool foundExactHeroLight = FindAndApplyHeroLightState(reason);
            RunDiagnosticScan(reason, foundExactHeroLight);

            Logger.LogInfo(
                "Exact HeroLight suppression is now "
                + (_disableHeroLight.Value ? "enabled" : "disabled")
                + ".");
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
                        "Could not restore the previous No Player Light config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset No Player Light config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private bool FindAndApplyHeroLightState(string reason)
        {
            GameObject found = GameObject.Find(HeroLightObjectName);
            if (found != null)
            {
                _heroLight = found;
                LogExactHeroLightMatch(found, reason);
            }

            bool hadCachedHeroLight = _heroLight != null;
            ApplyCachedHeroLightState(reason);

            return found != null || hadCachedHeroLight;
        }

        private void ApplyCachedHeroLightState(string reason)
        {
            if (_heroLight == null)
            {
                return;
            }

            if (!_disableHeroLight.Value)
            {
                RestoreCachedHeroLight(reason);
                return;
            }

            if (!_heroLight.activeSelf)
            {
                return;
            }

            LogExactHeroLightMatch(_heroLight, reason);
            _heroLight.SetActive(false);
            _heroLightsDisabledByPlugin.Add(_heroLight.GetInstanceID());
        }

        private void RestoreCachedHeroLight(string reason)
        {
            if (_heroLight == null || _heroLight.activeSelf)
            {
                return;
            }

            int id = _heroLight.GetInstanceID();
            if (!_heroLightsDisabledByPlugin.Contains(id))
            {
                return;
            }

            _heroLight.SetActive(true);
            _heroLightsDisabledByPlugin.Remove(id);
            Logger.LogWarning(
                "[diagnostic:"
                + reason
                + "] exact HeroLight was re-enabled because DisableHeroLight=false: "
                + DescribeGameObject(_heroLight));
        }

        private void RunDiagnosticScan(string reason, bool suppressNoMatchesLog)
        {
            if (!_enableDiagnostics.Value)
            {
                return;
            }

            try
            {
                int matches = 0;

                GameObject[] gameObjects =
                    UnityEngine.Object.FindObjectsByType<GameObject>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                for (int i = 0; i < gameObjects.Length; i++)
                {
                    GameObject gameObject = gameObjects[i];
                    if (gameObject == null || !MatchesDiagnosticName(gameObject.name))
                    {
                        continue;
                    }

                    matches++;
                    LogDiagnosticGameObject(gameObject, reason);
                }

                Light[] lights =
                    UnityEngine.Object.FindObjectsByType<Light>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    Light light = lights[i];
                    if (light == null || light.gameObject == null)
                    {
                        continue;
                    }

                    string path = GetHierarchyPath(light.transform);
                    if (!MatchesDiagnosticName(light.name)
                        && !MatchesDiagnosticName(light.gameObject.name)
                        && !MatchesDiagnosticName(path))
                    {
                        continue;
                    }

                    matches++;
                    LogDiagnosticLight(light, path, reason);
                }

                if (matches == 0 && !suppressNoMatchesLog)
                {
                    LogNoDiagnosticMatches(reason);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    "[diagnostic:"
                    + reason
                    + "] scan failed: "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
            }
        }

        private void LogExactHeroLightMatch(GameObject gameObject, string reason)
        {
            if (!_enableDiagnostics.Value || gameObject == null)
            {
                return;
            }

            string key =
                gameObject.GetInstanceID().ToString(CultureInfo.InvariantCulture)
                + ":"
                + _disableHeroLight.Value;
            if (!_loggedExactHeroLightMessages.Add(key))
            {
                return;
            }

            Logger.LogWarning(
                "[diagnostic:"
                + reason
                + "] exact GameObject.Find(\""
                + HeroLightObjectName
                + "\") match "
                + (_disableHeroLight.Value
                    ? "will be disabled"
                    : "will be left enabled because DisableHeroLight=false")
                + ": "
                + DescribeGameObject(gameObject));

            LogExactHeroLightChildren(gameObject, reason);
        }

        private void LogDiagnosticGameObject(GameObject gameObject, string reason)
        {
            string key =
                "GameObject:"
                + gameObject.GetInstanceID().ToString(CultureInfo.InvariantCulture);
            if (!_loggedDiagnosticMatches.Add(key))
            {
                return;
            }

            Logger.LogInfo(
                "[diagnostic:"
                + reason
                + "] active GameObject match: "
                + DescribeGameObject(gameObject));
        }

        private void LogDiagnosticLight(Light light, string path, string reason)
        {
            string key =
                "Light:"
                + light.GetInstanceID().ToString(CultureInfo.InvariantCulture);
            if (!_loggedDiagnosticMatches.Add(key))
            {
                return;
            }

            Logger.LogInfo(
                "[diagnostic:"
                + reason
                + "] active Light match: "
                + DescribeLight(light, path));
        }

        private void LogExactHeroLightChildren(GameObject root, string reason)
        {
            Transform[] children =
                root.GetComponentsInChildren<Transform>(true);
            Logger.LogInfo(
                "[diagnostic:"
                + reason
                + "] exact HeroLight child scan found "
                + children.Length.ToString(CultureInfo.InvariantCulture)
                + " transform(s), including inactive children.");

            int loggedChildren = 0;
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == root.transform)
                {
                    continue;
                }

                if (loggedChildren >= MaxExactHeroLightChildrenToLog)
                {
                    Logger.LogInfo(
                        "[diagnostic:"
                        + reason
                        + "] exact HeroLight child scan omitted "
                        + (children.Length - i).ToString(CultureInfo.InvariantCulture)
                        + " remaining transform(s).");
                    break;
                }

                loggedChildren++;
                Logger.LogInfo(
                    "[diagnostic:"
                    + reason
                    + "] exact HeroLight child: "
                    + DescribeGameObject(child.gameObject));
            }

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            if (lights.Length == 0)
            {
                Logger.LogInfo(
                    "[diagnostic:"
                    + reason
                    + "] exact HeroLight hierarchy contains no UnityEngine.Light components, including inactive children.");
                return;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null)
                {
                    continue;
                }

                Logger.LogInfo(
                    "[diagnostic:"
                    + reason
                    + "] exact HeroLight child Light: "
                    + DescribeLight(light, GetHierarchyPath(light.transform)));
            }
        }

        private static string DescribeLight(Light light, string path)
        {
            return "path='"
                + path
                + "', enabled="
                + light.enabled
                + ", type="
                + light.type
                + ", intensity="
                + FormatFloat(light.intensity)
                + ", range="
                + FormatFloat(light.range)
                + ", shadows="
                + light.shadows
                + ", object="
                + DescribeGameObject(light.gameObject);
        }

        private void LogNoDiagnosticMatches(string reason)
        {
            float now = Time.unscaledTime;
            if (now - _lastNoDiagnosticMatchesLogTime
                < DiagnosticNoMatchesLogIntervalSeconds)
            {
                return;
            }

            _lastNoDiagnosticMatchesLogTime = now;
            Logger.LogInfo(
                "[diagnostic:"
                + reason
                + "] no active GameObjects or Light components matched HeroLight, Light_HeroLight, or Spotlight_Hero.");
        }

        private static bool MatchesDiagnosticName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < DiagnosticNameFragments.Length; i++)
            {
                if (value.IndexOf(
                    DiagnosticNameFragments[i],
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeGameObject(GameObject gameObject)
        {
            return "path='"
                + GetHierarchyPath(gameObject.transform)
                + "', name='"
                + gameObject.name
                + "', activeSelf="
                + gameObject.activeSelf
                + ", activeInHierarchy="
                + gameObject.activeInHierarchy
                + ", layer="
                + FormatLayer(gameObject.layer)
                + ", components=["
                + GetComponentNames(gameObject)
                + "]";
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "<no transform>";
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            StringBuilder builder = new StringBuilder();
            while (names.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append("/");
                }

                builder.Append(names.Pop());
            }

            return builder.ToString();
        }

        private static string GetComponentNames(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < components.Length; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                Component component = components[i];
                if (component == null)
                {
                    builder.Append("<missing>");
                    continue;
                }

                Type type = component.GetType();
                builder.Append(type.FullName ?? type.Name);
            }

            return builder.ToString();
        }

        private static string FormatLayer(int layer)
        {
            string layerName = LayerMask.LayerToName(layer);
            if (string.IsNullOrEmpty(layerName))
            {
                return layer.ToString(CultureInfo.InvariantCulture);
            }

            return layer.ToString(CultureInfo.InvariantCulture) + ":" + layerName;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
