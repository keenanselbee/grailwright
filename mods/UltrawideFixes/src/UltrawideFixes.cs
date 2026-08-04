using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[assembly: AssemblyTitle("Ultrawide Fixes")]
[assembly: AssemblyDescription("Ultrawide presentation fixes for Tainted Grail title and loading screens")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Ultrawide Fixes")]
[assembly: AssemblyVersion("1.0.8.0")]
[assembly: AssemblyFileVersion("1.0.8.0")]

namespace UltrawideFixes
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.ultrawide-fixes";
        public const string PluginName = "Ultrawide Fixes";
        public const string PluginVersion = "1.0.8";

        private const float SourceVideoAspect = 16.0f / 9.0f;
        private const float DefaultTargetAspect = 21.0f / 9.0f;
        private const int ConfigSchemaVersion = 1;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        internal const string LoadingScreenViewTypeName = "Awaken.TG.Main.UI.TitleScreen.Loading.VLoadingScreenUI";
        internal const string ImageSpriteLoaderTypeName = "Awaken.TG.Assets.SpriteReference+ImageSpriteLoader";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _patchTitleVideo;
        private ConfigEntry<bool> _hideTitleBlackBars;
        private ConfigEntry<bool> _patchLoadingBackground;
        private ConfigEntry<bool> _patchLoadingBlurBackground;
        private ConfigEntry<bool> _fillCurrentScreen;
        private ConfigEntry<bool> _useScreenAspect;
        private ConfigEntry<float> _targetAspect;
        private ConfigEntry<float> _minimumScreenAspect;
        private ConfigEntry<bool> _cropVideoUv;
        private ConfigEntry<float> _stretchBlend;
        private ConfigEntry<float> _verticalCropFocus;
        private ConfigEntry<bool> _resizeRawImageRect;
        private ConfigEntry<bool> _resizeVideoParents;
        private ConfigEntry<float> _loadingStretchBlend;
        private ConfigEntry<float> _loadingVerticalCropFocus;
        private ConfigEntry<float> _scanDurationSeconds;
        private ConfigEntry<float> _scanIntervalSeconds;
        private ConfigEntry<bool> _verboseLogging;

        private readonly HashSet<int> _patchedRawImages = new HashSet<int>();
        private readonly HashSet<int> _patchedLoadingImages = new HashSet<int>();
        private readonly HashSet<int> _hiddenBars = new HashSet<int>();
        private readonly Dictionary<string, float> _pendingPreservedDisplayFloats =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _pendingPreservedDisplayBools =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;
        private Coroutine _scanRoutine;
        private Harmony _harmony;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                BindConfig();

                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;

                try
                {
                    _harmony = new Harmony(PluginGuid);
                    _harmony.PatchAll(typeof(Plugin).Assembly);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(
                        "Could not patch loading screen initialization. "
                        + "Title-screen ultrawide fixes can still run. "
                        + exception.Message);
                }

                SceneManager.sceneLoaded += OnSceneLoaded;
                StartScan("plugin load");

                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
            }
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Master switch.");
            Config.Bind(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _patchTitleVideo = Config.Bind(
                "Title Screen",
                "PatchTitleVideo",
                true,
                "Resizes the main menu title video RawImage to an ultrawide aspect.");
            _hideTitleBlackBars = Config.Bind(
                "Title Screen",
                "HideTitleBlackBars",
                true,
                "Disables title-screen black bar objects.");
            _patchLoadingBackground = Config.Bind(
                "Loading Screens",
                "PatchLoadingBackground",
                true,
                "Resizes the normal loading-screen background image to cover ultrawide displays. The loading bar is not touched.");
            _patchLoadingBlurBackground = Config.Bind(
                "Loading Screens",
                "PatchLoadingBlurBackground",
                true,
                "Resizes the blurred loading-screen background image to cover ultrawide displays. The loading bar is not touched.");
            _fillCurrentScreen = Config.Bind(
                "Aspect",
                "FillCurrentScreen",
                true,
                "Uses the current screen aspect to remove leftover side bars. Disable this for exact TargetAspect behavior.");
            _useScreenAspect = Config.Bind(
                "Aspect",
                "UseScreenAspect",
                false,
                "Legacy alias for FillCurrentScreen. If true, uses the current screen aspect instead of TargetAspect.");
            _targetAspect = Config.Bind(
                "Aspect",
                "TargetAspect",
                DefaultTargetAspect,
                new ConfigDescription(
                    "Target display aspect for title and loading backgrounds. 2.333333 is 21:9.",
                    new AcceptableValueRange<float>(SourceVideoAspect, 4.0f)));
            _minimumScreenAspect = Config.Bind(
                "Aspect",
                "MinimumScreenAspect",
                1.80f,
                new ConfigDescription(
                    "Only applies the patch when the actual screen is wider than this.",
                    new AcceptableValueRange<float>(SourceVideoAspect, 4.0f)));
            _cropVideoUv = Config.Bind(
                "Title Rendering",
                "CropVideoUv",
                true,
                "Applies the crop/stretch UV blend to the 16:9 source video.");
            _stretchBlend = Config.Bind(
                "Title Rendering",
                "StretchBlend",
                0.10f,
                new ConfigDescription(
                    "Blend between pure crop and full stretch. 0 is no stretch, 0.1 is a very light stretch, 1 is full stretch with no crop.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));
            _verticalCropFocus = Config.Bind(
                "Title Rendering",
                "VerticalCropFocus",
                0.50f,
                new ConfigDescription(
                    "Shifts the crop window vertically. 0 is centered, positive values focus upward, negative values focus downward.",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));
            _resizeRawImageRect = Config.Bind(
                "Title Rendering",
                "ResizeRawImageRect",
                true,
                "Also directly sizes the RawImage RectTransform. Keep enabled unless another UI mod conflicts.");
            _resizeVideoParents = Config.Bind(
                "Title Rendering",
                "ResizeVideoParents",
                true,
                "Also sizes the TitleScreenVideo parent RectTransforms so old 16:9 containers do not leave edge bars.");
            _loadingStretchBlend = Config.Bind(
                "Loading Rendering",
                "LoadingStretchBlend",
                0.20f,
                new ConfigDescription(
                    "Blend between pure crop and full stretch for loading-screen paintings. 0 keeps artwork proportions, 0.2 is a light stretch, 1 stretches to the display aspect.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));
            _loadingVerticalCropFocus = Config.Bind(
                "Loading Rendering",
                "LoadingVerticalCropFocus",
                0.50f,
                new ConfigDescription(
                    "Shifts the loading background crop. 0 is centered, positive values focus upward, negative values focus downward.",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));

            RestorePreservedDisplayCalibration();

            _scanDurationSeconds = Config.Bind(
                "Timing",
                "ScanDurationSeconds",
                20.0f,
                new ConfigDescription(
                    "How long after relevant UI events to keep looking for ultrawide targets.",
                    new AcceptableValueRange<float>(1.0f, 120.0f)));
            _scanIntervalSeconds = Config.Bind(
                "Timing",
                "ScanIntervalSeconds",
                0.25f,
                new ConfigDescription(
                    "Seconds between title-screen patch scans.",
                    new AcceptableValueRange<float>(0.05f, 5.0f)));
            _verboseLogging = Config.Bind(
                "Diagnostics",
                "VerboseLogging",
                false,
                "Logs every title video, loading background, and black bar object patched.");

            _enabled.SettingChanged += OnConfigChanged;
            _patchTitleVideo.SettingChanged += OnConfigChanged;
            _hideTitleBlackBars.SettingChanged += OnConfigChanged;
            _patchLoadingBackground.SettingChanged += OnConfigChanged;
            _patchLoadingBlurBackground.SettingChanged += OnConfigChanged;
            _fillCurrentScreen.SettingChanged += OnConfigChanged;
            _useScreenAspect.SettingChanged += OnConfigChanged;
            _targetAspect.SettingChanged += OnConfigChanged;
            _minimumScreenAspect.SettingChanged += OnConfigChanged;
            _cropVideoUv.SettingChanged += OnConfigChanged;
            _stretchBlend.SettingChanged += OnConfigChanged;
            _verticalCropFocus.SettingChanged += OnConfigChanged;
            _resizeRawImageRect.SettingChanged += OnConfigChanged;
            _resizeVideoParents.SettingChanged += OnConfigChanged;
            _loadingStretchBlend.SettingChanged += OnConfigChanged;
            _loadingVerticalCropFocus.SettingChanged += OnConfigChanged;

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

        private void UnregisterConfigHandlers()
        {
            Unsubscribe(_enabled, OnConfigChanged);
            Unsubscribe(_patchTitleVideo, OnConfigChanged);
            Unsubscribe(_hideTitleBlackBars, OnConfigChanged);
            Unsubscribe(_patchLoadingBackground, OnConfigChanged);
            Unsubscribe(_patchLoadingBlurBackground, OnConfigChanged);
            Unsubscribe(_fillCurrentScreen, OnConfigChanged);
            Unsubscribe(_useScreenAspect, OnConfigChanged);
            Unsubscribe(_targetAspect, OnConfigChanged);
            Unsubscribe(_minimumScreenAspect, OnConfigChanged);
            Unsubscribe(_cropVideoUv, OnConfigChanged);
            Unsubscribe(_stretchBlend, OnConfigChanged);
            Unsubscribe(_verticalCropFocus, OnConfigChanged);
            Unsubscribe(_resizeRawImageRect, OnConfigChanged);
            Unsubscribe(_resizeVideoParents, OnConfigChanged);
            Unsubscribe(_loadingStretchBlend, OnConfigChanged);
            Unsubscribe(_loadingVerticalCropFocus, OnConfigChanged);
        }

        private static void Unsubscribe<T>(
            ConfigEntry<T> entry,
            EventHandler handler)
        {
            if (entry != null)
            {
                entry.SettingChanged -= handler;
            }
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

            CapturePreservedDisplayCalibration(
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
                ClearPendingPreservedDisplayCalibration();

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
                        "Could not restore the previous Ultrawide Fixes config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Ultrawide Fixes config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedDisplayCalibration(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedDisplayCalibration();
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

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string settingName = line.Substring(0, separatorIndex).Trim();
                string settingId = currentSection + "\n" + settingName;

                if (IsPreservedDisplayFloat(settingId))
                {
                    float parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedDisplayFloats[settingId] = parsedValue;
                    }

                    continue;
                }

                if (IsPreservedDisplayBool(settingId))
                {
                    bool parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedDisplayBools[settingId] = parsedValue;
                    }
                }
            }
        }

        private static bool IsPreservedDisplayFloat(string settingId)
        {
            switch (settingId)
            {
                case "Aspect\nTargetAspect":
                case "Aspect\nMinimumScreenAspect":
                case "Title Rendering\nStretchBlend":
                case "Title Rendering\nVerticalCropFocus":
                case "Loading Rendering\nLoadingStretchBlend":
                case "Loading Rendering\nLoadingVerticalCropFocus":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPreservedDisplayBool(string settingId)
        {
            switch (settingId)
            {
                case "Aspect\nFillCurrentScreen":
                case "Title Rendering\nCropVideoUv":
                case "Title Rendering\nResizeRawImageRect":
                case "Title Rendering\nResizeVideoParents":
                    return true;
                default:
                    return false;
            }
        }

        private void RestorePreservedDisplayCalibration()
        {
            if (_pendingPreservedDisplayFloats.Count == 0
                && _pendingPreservedDisplayBools.Count == 0
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreservedBool(
                "Aspect\nFillCurrentScreen",
                _fillCurrentScreen,
                ref restoredCount);
            RestorePreservedFloat(
                "Aspect\nTargetAspect",
                _targetAspect,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                "Aspect\nMinimumScreenAspect",
                _minimumScreenAspect,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedBool(
                "Title Rendering\nCropVideoUv",
                _cropVideoUv,
                ref restoredCount);
            RestorePreservedFloat(
                "Title Rendering\nStretchBlend",
                _stretchBlend,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                "Title Rendering\nVerticalCropFocus",
                _verticalCropFocus,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedBool(
                "Title Rendering\nResizeRawImageRect",
                _resizeRawImageRect,
                ref restoredCount);
            RestorePreservedBool(
                "Title Rendering\nResizeVideoParents",
                _resizeVideoParents,
                ref restoredCount);
            RestorePreservedFloat(
                "Loading Rendering\nLoadingStretchBlend",
                _loadingStretchBlend,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                "Loading Rendering\nLoadingVerticalCropFocus",
                _loadingVerticalCropFocus,
                ref restoredCount,
                ref clampedCount);

            Logger.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " display calibration value(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + _pendingPreservedInvalidValueCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            ClearPendingPreservedDisplayCalibration();
        }

        private void RestorePreservedFloat(
            string settingId,
            ConfigEntry<float> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            float preservedValue;
            if (entry == null
                || !_pendingPreservedDisplayFloats.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
                out clamped))
            {
                _pendingPreservedInvalidValueCount++;
                return;
            }

            if (clamped)
            {
                clampedCount++;
            }
            restoredCount++;
        }

        private void RestorePreservedBool(
            string settingId,
            ConfigEntry<bool> entry,
            ref int restoredCount)
        {
            bool preservedValue;
            if (entry == null
                || !_pendingPreservedDisplayBools.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
                out clamped))
            {
                restoredCount++;
            }
            else
            {
                _pendingPreservedInvalidValueCount++;
            }
        }

        private void ClearPendingPreservedDisplayCalibration()
        {
            _pendingPreservedDisplayFloats.Clear();
            _pendingPreservedDisplayBools.Clear();
            _pendingPreservedInvalidValueCount = 0;
        }

        private void OnConfigChanged(object sender, EventArgs args)
        {
            _patchedRawImages.Clear();
            _patchedLoadingImages.Clear();
            _hiddenBars.Clear();
            StartScan("config changed");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_enabled == null || !_enabled.Value)
            {
                return;
            }

            if (IsLikelyTitleScene(scene.name))
            {
                _patchedRawImages.Clear();
                _hiddenBars.Clear();
                StartScan("scene loaded: " + scene.name);
            }
        }

        private void Update()
        {
            if (!_enabled.Value)
            {
                return;
            }

            if (_lastScreenWidth == Screen.width
                && _lastScreenHeight == Screen.height)
            {
                return;
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _patchedRawImages.Clear();
            _patchedLoadingImages.Clear();
            StartScan("resolution changed");
        }

        private void StartScan(string reason)
        {
            if (_enabled == null || !_enabled.Value || !isActiveAndEnabled)
            {
                return;
            }

            if (_scanRoutine != null)
            {
                StopCoroutine(_scanRoutine);
            }

            _scanRoutine = StartCoroutine(ScanRoutine(reason));
        }

        private IEnumerator ScanRoutine(string reason)
        {
            float endTime = Time.realtimeSinceStartup + _scanDurationSeconds.Value;
            if (_verboseLogging.Value)
            {
                Logger.LogInfo("Starting ultrawide fix scan: " + reason);
            }

            while (_enabled.Value && Time.realtimeSinceStartup <= endTime)
            {
                ApplyPatchPass();
                yield return new WaitForSecondsRealtime(_scanIntervalSeconds.Value);
            }

            _scanRoutine = null;
        }

        private void ApplyPatchPass()
        {
            if (GetScreenAspect() < _minimumScreenAspect.Value)
            {
                return;
            }

            float targetAspect = GetTargetAspect();

            if (_patchTitleVideo.Value)
            {
                RawImage[] rawImages = Resources.FindObjectsOfTypeAll<RawImage>();
                for (int i = 0; i < rawImages.Length; i++)
                {
                    RawImage rawImage = rawImages[i];
                    if (IsTitleVideoRawImage(rawImage))
                    {
                        PatchTitleVideoRawImage(rawImage, targetAspect);
                    }
                }
            }

            if (_hideTitleBlackBars.Value)
            {
                HideTitleBlackBars();
            }

            if (_patchLoadingBackground.Value || _patchLoadingBlurBackground.Value)
            {
                PatchKnownLoadingScreens(targetAspect, "scan");
            }
        }

        private float GetScreenAspect()
        {
            if (Screen.height <= 0)
            {
                return SourceVideoAspect;
            }

            return (float)Screen.width / (float)Screen.height;
        }

        private float GetTargetAspect()
        {
            float screenAspect = GetScreenAspect();
            float targetAspect = (_fillCurrentScreen.Value || _useScreenAspect.Value)
                ? screenAspect
                : _targetAspect.Value;

            if (targetAspect < SourceVideoAspect)
            {
                targetAspect = SourceVideoAspect;
            }

            if (screenAspect > SourceVideoAspect && targetAspect > screenAspect)
            {
                targetAspect = screenAspect;
            }

            return targetAspect;
        }

        private bool IsTitleVideoRawImage(RawImage rawImage)
        {
            if (rawImage == null
                || rawImage.gameObject == null
                || rawImage.transform == null
                || !rawImage.gameObject.scene.IsValid())
            {
                return false;
            }

            string path = GetTransformPath(rawImage.transform);
            if (Contains(path, "TitleScreenVideo")
                && Contains(path, "RawImage"))
            {
                return true;
            }

            if (Contains(path, "TitleScreenVisuals")
                && string.Equals(rawImage.name, "RawImage", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Texture texture = rawImage.texture;
            return texture != null && Contains(texture.name, "Title_screen");
        }

        private void PatchTitleVideoRawImage(RawImage rawImage, float targetAspect)
        {
            RectTransform rectTransform = rawImage.rectTransform;
            if (rectTransform == null)
            {
                return;
            }

            AspectRatioFitter fitter = rawImage.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.enabled = true;
                fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fitter.aspectRatio = targetAspect;
                fitter.SetLayoutHorizontal();
                fitter.SetLayoutVertical();
            }

            if (_resizeRawImageRect.Value)
            {
                ResizeRectToAspect(rectTransform, targetAspect);
            }

            if (_resizeVideoParents.Value)
            {
                ResizeTitleVideoParents(rectTransform, targetAspect);
            }

            rawImage.uvRect = _cropVideoUv.Value
                ? GetBlendedUvRect(targetAspect)
                : new Rect(0.0f, 0.0f, 1.0f, 1.0f);
            rawImage.SetVerticesDirty();
            rawImage.SetMaterialDirty();

            int instanceId = rawImage.gameObject.GetInstanceID();
            if (_patchedRawImages.Add(instanceId) && _verboseLogging.Value)
            {
                Logger.LogInfo(
                    "Patched title video RawImage to aspect "
                    + targetAspect.ToString("0.###")
                    + ": "
                    + GetTransformPath(rawImage.transform));
            }
        }

        private void ResizeRectToAspect(RectTransform rectTransform, float aspect)
        {
            RectTransform parent = rectTransform.parent as RectTransform;
            float height = parent != null ? parent.rect.height : 0.0f;
            if (height <= 1.0f)
            {
                height = Screen.height;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(height * aspect, height);
        }

        private void ResizeTitleVideoParents(RectTransform rectTransform, float aspect)
        {
            RectTransform current = rectTransform.parent as RectTransform;
            int steps = 0;
            while (current != null && steps < 4)
            {
                if (IsTitleVideoContainer(current))
                {
                    ResizeRectToAspect(current, aspect);
                }

                current = current.parent as RectTransform;
                steps++;
            }
        }

        private static bool IsTitleVideoContainer(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return false;
            }

            string path = GetTransformPath(rectTransform);
            if (!Contains(path, "TitleScreenVideo"))
            {
                return false;
            }

            return string.Equals(rectTransform.name, "BG", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rectTransform.name, "TitleScreenVideo", StringComparison.OrdinalIgnoreCase);
        }

        private Rect GetBlendedUvRect(float targetAspect)
        {
            if (targetAspect <= SourceVideoAspect)
            {
                return new Rect(0.0f, 0.0f, 1.0f, 1.0f);
            }

            float cropUvHeight = Mathf.Clamp(SourceVideoAspect / targetAspect, 0.1f, 1.0f);
            float blend = Mathf.Clamp01(_stretchBlend.Value);
            float uvHeight = Mathf.Lerp(cropUvHeight, 1.0f, blend);
            float maxUvY = 1.0f - uvHeight;
            float centeredUvY = maxUvY * 0.5f;
            float focus = Mathf.Clamp(_verticalCropFocus.Value, -1.0f, 1.0f);
            float uvY = Mathf.Clamp(centeredUvY + (centeredUvY * focus), 0.0f, maxUvY);
            return new Rect(0.0f, uvY, 1.0f, uvHeight);
        }

        internal void PatchLoadingScreenView(object loadingScreenView, string reason)
        {
            if (!_enabled.Value
                || (!_patchLoadingBackground.Value && !_patchLoadingBlurBackground.Value)
                || GetScreenAspect() < _minimumScreenAspect.Value)
            {
                return;
            }

            float targetAspect = GetTargetAspect();
            PatchLoadingScreenViewImmediate(loadingScreenView, targetAspect, reason);
            StartCoroutine(PatchLoadingScreenViewLater(loadingScreenView, targetAspect, reason));
        }

        private IEnumerator PatchLoadingScreenViewLater(
            object loadingScreenView,
            float targetAspect,
            string reason)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSecondsRealtime(0.15f);
                PatchLoadingScreenViewImmediate(
                    loadingScreenView,
                    targetAspect,
                    reason + " retry " + (i + 1));
            }
        }

        private void PatchKnownLoadingScreens(float targetAspect, string reason)
        {
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                if (type == null
                    || !string.Equals(type.FullName, LoadingScreenViewTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                PatchLoadingScreenViewImmediate(behaviour, targetAspect, reason);
            }
        }

        internal void PatchAssignedLoadingSprite(Image image, string reason)
        {
            if (!_enabled.Value
                || (!_patchLoadingBackground.Value && !_patchLoadingBlurBackground.Value)
                || image == null
                || image.sprite == null
                || GetScreenAspect() < _minimumScreenAspect.Value)
            {
                return;
            }

            string label;
            if (!TryGetLoadingImageLabel(image, out label))
            {
                return;
            }

            PatchLoadingImage(image, GetTargetAspect(), label, reason);
        }

        private bool TryGetLoadingImageLabel(Image image, out string label)
        {
            label = null;
            if (image == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours = image.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                if (type == null
                    || !string.Equals(type.FullName, LoadingScreenViewTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                FieldInfo backgroundField = AccessTools.Field(type, "background");
                if (_patchLoadingBackground.Value
                    && backgroundField != null
                    && object.ReferenceEquals(backgroundField.GetValue(behaviour), image))
                {
                    label = "loading background";
                    return true;
                }

                FieldInfo blurBackgroundField = AccessTools.Field(type, "blurBackground");
                if (_patchLoadingBlurBackground.Value
                    && blurBackgroundField != null
                    && object.ReferenceEquals(blurBackgroundField.GetValue(behaviour), image))
                {
                    label = "loading blur background";
                    return true;
                }
            }

            return false;
        }

        private void PatchLoadingScreenViewImmediate(
            object loadingScreenView,
            float targetAspect,
            string reason)
        {
            if (loadingScreenView == null)
            {
                return;
            }

            Type type = loadingScreenView.GetType();
            if (type == null
                || !string.Equals(type.FullName, LoadingScreenViewTypeName, StringComparison.Ordinal))
            {
                return;
            }

            if (_patchLoadingBackground.Value)
            {
                FieldInfo backgroundField = AccessTools.Field(type, "background");
                PatchLoadingImage(
                    backgroundField == null
                        ? null
                        : backgroundField.GetValue(loadingScreenView) as Image,
                    targetAspect,
                    "loading background",
                    reason);
            }

            if (_patchLoadingBlurBackground.Value)
            {
                FieldInfo blurBackgroundField = AccessTools.Field(type, "blurBackground");
                PatchLoadingImage(
                    blurBackgroundField == null
                        ? null
                        : blurBackgroundField.GetValue(loadingScreenView) as Image,
                    targetAspect,
                    "loading blur background",
                    reason);
            }
        }

        private void PatchLoadingImage(
            Image image,
            float targetAspect,
            string label,
            string reason)
        {
            if (image == null
                || image.gameObject == null
                || image.rectTransform == null
                || image.sprite == null
                || !image.gameObject.scene.IsValid())
            {
                return;
            }

            RectTransform rectTransform = image.rectTransform;
            RectTransform parent = rectTransform.parent as RectTransform;
            float parentWidth = parent != null ? parent.rect.width : 0.0f;
            float parentHeight = parent != null ? parent.rect.height : 0.0f;
            float areaWidth = Mathf.Max(parentWidth, Screen.width);
            float areaHeight = parentHeight;

            if (areaWidth <= 1.0f)
            {
                areaWidth = Screen.width;
            }

            if (areaHeight <= 1.0f)
            {
                areaHeight = Screen.height;
            }

            if (areaWidth <= 1.0f || areaHeight <= 1.0f)
            {
                return;
            }

            float sourceAspect = GetImageAspect(image);
            float blend = Mathf.Clamp01(_loadingStretchBlend.Value);
            float drawAspect = Mathf.Lerp(sourceAspect, targetAspect, blend);
            if (drawAspect <= 0.01f)
            {
                drawAspect = sourceAspect;
            }

            float width = areaWidth;
            float height = width / drawAspect;
            if (height < areaHeight)
            {
                height = areaHeight;
                width = height * drawAspect;
            }

            float extraHeight = Mathf.Max(0.0f, height - areaHeight);
            float focus = Mathf.Clamp(_loadingVerticalCropFocus.Value, -1.0f, 1.0f);
            float yOffset = -extraHeight * 0.5f * focus;

            image.preserveAspect = false;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0.0f, yOffset);
            rectTransform.sizeDelta = new Vector2(width, height);
            image.SetVerticesDirty();
            image.SetMaterialDirty();

            int instanceId = image.gameObject.GetInstanceID();
            if (_patchedLoadingImages.Add(instanceId) && _verboseLogging.Value)
            {
                Logger.LogInfo(
                    "Patched "
                    + label
                    + " to cover aspect "
                    + targetAspect.ToString("0.###")
                    + " from "
                    + reason
                    + ": "
                    + GetTransformPath(image.transform));
            }
        }

        private static float GetImageAspect(Image image)
        {
            if (image != null && image.sprite != null)
            {
                Rect rect = image.sprite.rect;
                if (rect.height > 1.0f && rect.width > 1.0f)
                {
                    return rect.width / rect.height;
                }
            }

            return SourceVideoAspect;
        }

        internal static Type FindGameType(string fullName)
        {
            Type type = AccessTools.TypeByName(fullName);
            if (type != null)
            {
                return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private void HideTitleBlackBars()
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (!IsTitleBlackBar(transform))
                {
                    continue;
                }

                GameObject gameObject = transform.gameObject;
                Graphic graphic = gameObject.GetComponent<Graphic>();
                if (graphic != null)
                {
                    graphic.enabled = false;
                }

                Renderer renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                int instanceId = gameObject.GetInstanceID();
                if (_hiddenBars.Add(instanceId) && _verboseLogging.Value)
                {
                    Logger.LogInfo(
                        "Disabled title black bar: "
                        + GetTransformPath(transform));
                }
            }
        }

        private bool IsTitleBlackBar(Transform transform)
        {
            if (transform == null
                || transform.gameObject == null
                || !transform.gameObject.scene.IsValid())
            {
                return false;
            }

            string path = GetTransformPath(transform);
            if (!Contains(path, "CanvasBG") && !Contains(path, "TitleScreen"))
            {
                return false;
            }

            return Contains(transform.name, "BlackBar")
                || Contains(path, "BlackBar_Left")
                || Contains(path, "BlackBar_Right");
        }

        private static bool IsLikelyTitleScene(string sceneName)
        {
            return Contains(sceneName, "Title") || Contains(sceneName, "Menu");
        }

        private static bool Contains(string text, string fragment)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
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
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnregisterConfigHandlers();

            if (_scanRoutine != null)
            {
                StopCoroutine(_scanRoutine);
                _scanRoutine = null;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            Instance = null;
            Log = null;
        }
    }

    [HarmonyPatch]
    internal static class LoadingScreenViewInitializePatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = Plugin.FindGameType(Plugin.LoadingScreenViewTypeName);
            return type == null ? null : AccessTools.Method(type, "OnInitialize");
        }

        private static void Postfix(object __instance)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.PatchLoadingScreenView(__instance, "VLoadingScreenUI.OnInitialize");
            }
        }
    }

    [HarmonyPatch]
    internal static class LoadingImageSpriteAssignPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = Plugin.FindGameType(Plugin.ImageSpriteLoaderTypeName);
            return type == null
                ? null
                : AccessTools.Method(type, "AssignSprite", new[] { typeof(Image), typeof(Sprite) });
        }

        private static void Postfix(Image __0)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.PatchAssignedLoadingSprite(__0, "ImageSpriteLoader.AssignSprite");
            }
        }
    }
}
