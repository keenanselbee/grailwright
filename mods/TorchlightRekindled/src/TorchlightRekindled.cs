using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Graphics.VFX;
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using BepInEx;
using BepInEx.Configuration;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;

[assembly: AssemblyTitle("Torchlight Rekindled")]
[assembly: AssemblyDescription("Configurable held-torch lighting, flame glow, and looping fire audio for Tainted Grail: The Fall of Avalon.")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Torchlight Rekindled")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("0.1.7.0")]
[assembly: AssemblyFileVersion("0.1.7.0")]

namespace TorchlightRekindled
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("VirusAlex.TorchLightControl")]
    public sealed class TorchlightRekindledPlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.torchlight-rekindled";
        public const string PluginName = "Torchlight Rekindled";
        public const string PluginVersion = "0.1.7";

        private const int ConfigSchemaVersion = 5;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new[]
                {
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        2,
                        "2. Torch Light",
                        "RangeBonusMeters",
                        "Current range tuning uses two metres per displayed point."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        2,
                        "2. Torch Light",
                        "BrightnessMultiplier",
                        "Current brightness tuning doubles adjustments away from vanilla."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        2,
                        "4. Audio",
                        "LoopingFireVolume",
                        "Schema 2 used an amplified 0-1 fire-volume control."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        3,
                        "4. Audio",
                        "LoopingFireVolume",
                        "Fire-loop volume now uses direct FMOD gain from 0 to 2."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        4,
                        "2. Torch Light",
                        "RangeBonusMeters",
                        "Current range tuning maps displayed 3 to a 20-metre bonus."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        4,
                        "2. Torch Light",
                        "BrightnessMultiplier",
                        "Current illumination tuning maps displayed 1 to five times vanilla."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        4,
                        "3. Flame",
                        "FlameBrightnessMultiplier",
                        "Current flame tuning maps displayed 1 to three times source brightness."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        4,
                        "3. Flame",
                        "FlameBloomMultiplier",
                        "Current bloom tuning maps displayed 1 to three times source HDR headroom."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        5,
                        "3. Flame",
                        "FlameBrightnessMultiplier",
                        "The recommended flame-brightness default is now 0.75."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        5,
                        "3. Flame",
                        "FlameBloomMultiplier",
                        "The recommended flame-bloom default is now 0.75."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        5,
                        "4. Audio",
                        "LoopingFireVolume",
                        "Displayed 1 is now the recommended fire-loop volume.")
                };
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        internal const string TorchFireLoopEventPath =
            "event:/AmbienceEnviro/3D/SFX_VFX_Fire_Tiny_Loop";
        internal static readonly GUID TorchFireLoopEventGuid =
            GUID.Parse("368b25fb-4c98-47e7-9d68-054da153d1b0");
        internal static TorchlightRekindledPlugin Instance { get; private set; }

        private readonly HashSet<TorchRuntime> _activeTorches =
            new HashSet<TorchRuntime>();
        private readonly Dictionary<LightController, TorchRuntime>
            _trackedLightOwners =
                new Dictionary<LightController, TorchRuntime>();
        private Harmony _harmony;
        private FieldInfo _bakedNativeIntensityField;
        private FieldInfo _rangeField;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _rangeBonusMeters;
        private ConfigEntry<float> _lightBrightnessMultiplier;
        private ConfigEntry<float> _lightFlickerStrength;
        private ConfigEntry<float> _lightFlickerSpeed;
        private ConfigEntry<float> _flameBrightnessMultiplier;
        private ConfigEntry<float> _flameBloomMultiplier;
        private ConfigEntry<float> _flameHaloStrength;
        private ConfigEntry<float> _flameHaloSize;
        private ConfigEntry<bool> _loopingFireAudio;
        private ConfigEntry<float> _loopingFireVolume;
        private ConfigEntry<bool> _diagnostics;

        internal bool FeatureEnabled =>
            _enabled != null && _enabled.Value;

        internal float RangeBonusMeters =>
            _rangeBonusMeters == null ? 3f : _rangeBonusMeters.Value;

        internal float LightBrightnessMultiplier =>
            _lightBrightnessMultiplier == null
                ? 1f
                : _lightBrightnessMultiplier.Value;

        internal float LightFlickerStrength =>
            _lightFlickerStrength == null ? 1f : _lightFlickerStrength.Value;

        internal float LightFlickerSpeed =>
            _lightFlickerSpeed == null ? 1f : _lightFlickerSpeed.Value;

        internal float FlameBrightnessMultiplier =>
            _flameBrightnessMultiplier == null
                ? 0.75f
                : _flameBrightnessMultiplier.Value;

        internal float FlameBloomMultiplier =>
            _flameBloomMultiplier == null
                ? 0.75f
                : _flameBloomMultiplier.Value;

        internal float FlameHaloStrength =>
            _flameHaloStrength == null ? 1f : _flameHaloStrength.Value;

        internal float FlameHaloSize =>
            _flameHaloSize == null ? 0.08f : _flameHaloSize.Value;

        internal bool LoopingFireAudio =>
            _loopingFireAudio != null && _loopingFireAudio.Value;

        internal float LoopingFireVolume =>
            _loopingFireVolume == null ? 1f : _loopingFireVolume.Value;

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

                _bakedNativeIntensityField = AccessTools.Field(
                    typeof(LightController),
                    "bakedNativeIntensity");
                _rangeField = AccessTools.Field(
                    typeof(LightController),
                    "range");
                MethodInfo onMount = AccessTools.Method(
                    typeof(CharacterHandBase),
                    "OnMount");
                MethodInfo activeLightUpdate = AccessTools.Method(
                    typeof(LightController),
                    "ActiveLightUpdate");
                if (_bakedNativeIntensityField == null
                    || _rangeField == null
                    || onMount == null
                    || activeLightUpdate == null)
                {
                    throw new MissingMemberException(
                        "Could not resolve the held-light lifecycle members.");
                }

                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(
                    onMount,
                    postfix: new HarmonyMethod(
                        typeof(CharacterHandMountPatch),
                        nameof(CharacterHandMountPatch.Postfix)));
                _harmony.Patch(
                    activeLightUpdate,
                    postfix: new HarmonyMethod(
                        typeof(LightControllerUpdatePatch),
                        nameof(LightControllerUpdatePatch.Postfix)));

                Config.Save();
                Logger.LogInfo(
                    PluginName + " " + PluginVersion + " loaded.");
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
            UnsubscribeConfigEvents();

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            List<TorchRuntime> activeTorches =
                new List<TorchRuntime>(_activeTorches);
            foreach (TorchRuntime torch in activeTorches)
            {
                if (torch != null)
                {
                    torch.Shutdown();
                    Destroy(torch);
                }
            }

            _activeTorches.Clear();
            _trackedLightOwners.Clear();
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
                    new BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Master switch. Turning this off restores the held torch to its original light, flame, and audio behavior.");
            _rangeBonusMeters = Config.Bind(
                "2. Torch Light",
                "RangeBonusMeters",
                3f,
                new ConfigDescription(
                    "How far the held torch reaches into darkness. The recommended value is 3; use lower values for tighter pools of light or higher values for long sightlines.",
                    new AcceptableValueRange<float>(0f, 10f)));
            _lightBrightnessMultiplier = Config.Bind(
                "2. Torch Light",
                "BrightnessMultiplier",
                1f,
                new ConfigDescription(
                    "How strongly the torch lights walls, floors, and characters while retaining natural flicker. The recommended value is 1.",
                    new AcceptableValueRange<float>(0.25f, 3f)));
            _lightFlickerStrength = Config.Bind(
                "2. Torch Light",
                "LightFlickerStrength",
                1f,
                new ConfigDescription(
                    "Additional irregular variation in the light cast onto the environment. The recommended value is 1; 0 keeps only the original flicker and 2 is strongly turbulent.",
                    new AcceptableValueRange<float>(0f, 2f)));
            _lightFlickerSpeed = Config.Bind(
                "2. Torch Light",
                "LightFlickerSpeed",
                1f,
                new ConfigDescription(
                    "Speed of the additional illumination flicker. The recommended value is 1; lower values feel calmer and higher values feel more restless.",
                    new AcceptableValueRange<float>(0.5f, 2f)));
            _flameBrightnessMultiplier = Config.Bind(
                "3. Flame",
                "FlameBrightnessMultiplier",
                0.75f,
                new ConfigDescription(
                    "Brightness of the visible flame, embers, and sparks without changing the light cast into the world. The recommended value is 0.75.",
                    new AcceptableValueRange<float>(0.25f, 3f)));
            _flameBloomMultiplier = Config.Bind(
                "3. Flame",
                "FlameBloomMultiplier",
                0.75f,
                new ConfigDescription(
                    "Strength of the flame's HDR glare and halo. The recommended value is 0.75; lower it for a crisper flame or raise it for a harder-to-look-at glow.",
                    new AcceptableValueRange<float>(0f, 3f)));
            _flameHaloStrength = Config.Bind(
                "3. Flame",
                "FlameHaloStrength",
                1f,
                new ConfigDescription(
                    "Strength of an additional warm bloom corona inside the flame. The recommended value is 1; use 0 to disable the corona.",
                    new AcceptableValueRange<float>(0f, 3f)));
            _flameHaloSize = Config.Bind(
                "3. Flame",
                "FlameHaloSize",
                0.08f,
                new ConfigDescription(
                    "Diameter in metres of the emissive core that produces the additional corona. The recommended value is 0.08.",
                    new AcceptableValueRange<float>(0.02f, 0.25f)));
            _loopingFireAudio = Config.Bind(
                "4. Audio",
                "LoopingFireAudio",
                true,
                "Play the game's spatial small-fire loop from the held torch.");
            _loopingFireVolume = Config.Bind(
                "4. Audio",
                "LoopingFireVolume",
                1f,
                new ConfigDescription(
                    "Loudness of the fire crackle from the torch in your equipped hand. The recommended value is 1; 0 is silent and values above 1 emphasize the torch over nearby ambience.",
                    new AcceptableValueRange<float>(0f, 2f)));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                "Log torch attachment and runtime details.");

            _enabled.SettingChanged += OnRuntimeSettingChanged;
            _rangeBonusMeters.SettingChanged += OnRuntimeSettingChanged;
            _lightBrightnessMultiplier.SettingChanged += OnRuntimeSettingChanged;
            _lightFlickerStrength.SettingChanged += OnRuntimeSettingChanged;
            _lightFlickerSpeed.SettingChanged += OnRuntimeSettingChanged;
            _flameBrightnessMultiplier.SettingChanged += OnRuntimeSettingChanged;
            _flameBloomMultiplier.SettingChanged += OnRuntimeSettingChanged;
            _flameHaloStrength.SettingChanged += OnRuntimeSettingChanged;
            _flameHaloSize.SettingChanged += OnRuntimeSettingChanged;
            _loopingFireAudio.SettingChanged += OnRuntimeSettingChanged;
            _loopingFireVolume.SettingChanged += OnRuntimeSettingChanged;
        }

        private void UnsubscribeConfigEvents()
        {
            if (_enabled != null)
            {
                _enabled.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_rangeBonusMeters != null)
            {
                _rangeBonusMeters.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_lightBrightnessMultiplier != null)
            {
                _lightBrightnessMultiplier.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_lightFlickerStrength != null)
            {
                _lightFlickerStrength.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_lightFlickerSpeed != null)
            {
                _lightFlickerSpeed.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_flameBrightnessMultiplier != null)
            {
                _flameBrightnessMultiplier.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_flameBloomMultiplier != null)
            {
                _flameBloomMultiplier.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_flameHaloStrength != null)
            {
                _flameHaloStrength.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_flameHaloSize != null)
            {
                _flameHaloSize.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_loopingFireAudio != null)
            {
                _loopingFireAudio.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_loopingFireVolume != null)
            {
                _loopingFireVolume.SettingChanged -= OnRuntimeSettingChanged;
            }
        }

        private void OnRuntimeSettingChanged(object sender, EventArgs eventArgs)
        {
            ApplyToActiveTorches();
        }

        private void ApplyToActiveTorches()
        {
            List<TorchRuntime> activeTorches =
                new List<TorchRuntime>(_activeTorches);
            foreach (TorchRuntime torch in activeTorches)
            {
                if (torch != null)
                {
                    torch.ApplySettings();
                }
            }
        }

        internal void TryAttach(CharacterHandBase hand)
        {
            if (hand == null
                || hand.Owner == null
                || !ReferenceEquals(hand.Owner.Character, Hero.Current)
                || !LooksLikeTorch(hand))
            {
                return;
            }

            TorchRuntime runtime = hand.GetComponent<TorchRuntime>();
            if (runtime == null)
            {
                runtime = hand.gameObject.AddComponent<TorchRuntime>();
            }

            runtime.Initialize(this, hand);
        }

        private static bool LooksLikeTorch(CharacterHandBase hand)
        {
            if (hand.name.IndexOf(
                "Torch",
                StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return hand.GetComponentsInChildren<LightController>(true).Length > 0
                && hand.GetComponentsInChildren<VisualEffect>(true).Length > 0;
        }

        internal LightController.NativeIntensityData ReadBakedIntensity(
            LightController controller)
        {
            return (LightController.NativeIntensityData)
                _bakedNativeIntensityField.GetValue(controller);
        }

        internal void WriteBakedIntensity(
            LightController controller,
            LightController.NativeIntensityData intensity)
        {
            _bakedNativeIntensityField.SetValue(controller, intensity);
        }

        internal LightController.RangeToggleObject ReadRange(
            LightController controller)
        {
            return (LightController.RangeToggleObject)
                _rangeField.GetValue(controller);
        }

        internal void WriteRange(
            LightController controller,
            LightController.RangeToggleObject range)
        {
            _rangeField.SetValue(controller, range);
        }

        internal void Register(TorchRuntime runtime)
        {
            _activeTorches.Add(runtime);
        }

        internal void Unregister(TorchRuntime runtime)
        {
            _activeTorches.Remove(runtime);
        }

        internal void RegisterLightController(
            LightController controller,
            TorchRuntime runtime)
        {
            if (controller != null && runtime != null)
            {
                _trackedLightOwners[controller] = runtime;
            }
        }

        internal void UnregisterLightController(
            LightController controller,
            TorchRuntime runtime)
        {
            TorchRuntime owner;
            if (controller != null
                && _trackedLightOwners.TryGetValue(controller, out owner)
                && ReferenceEquals(owner, runtime))
            {
                _trackedLightOwners.Remove(controller);
            }
        }

        internal void ApplyTrackedLightFlicker(
            LightController controller)
        {
            TorchRuntime runtime;
            if (controller != null
                && _trackedLightOwners.TryGetValue(
                    controller,
                    out runtime)
                && runtime != null)
            {
                runtime.ApplyLightFlicker(controller);
            }
        }

        internal void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        internal void LogWarning(string message)
        {
            Logger.LogWarning(message);
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
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowConfigReset(
                        PluginGuid,
                        PluginName,
                        storedSchemaVersion,
                        ConfigSchemaVersion);
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
                        "Could not restore the previous "
                        + PluginName
                        + " config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset "
                    + PluginName
                    + " config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private static class CharacterHandMountPatch
        {
            internal static void Postfix(CharacterHandBase __instance)
            {
                TorchlightRekindledPlugin instance = Instance;
                if (instance == null)
                {
                    return;
                }

                try
                {
                    instance.TryAttach(__instance);
                }
                catch (Exception exception)
                {
                    instance.LogWarning(
                        "Could not configure a held torch: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private static class LightControllerUpdatePatch
        {
            internal static void Postfix(LightController __instance)
            {
                TorchlightRekindledPlugin instance = Instance;
                if (instance != null)
                {
                    instance.ApplyTrackedLightFlicker(__instance);
                }
            }
        }
    }

    public sealed class TorchRuntime : MonoBehaviour
    {
        private TorchlightRekindledPlugin _plugin;
        private CharacterHandBase _hand;
        private LightState[] _lights = new LightState[0];
        private FlameState[] _flames = new FlameState[0];
        private Transform _audioAnchor;
        private GameObject _audioObject;
        private ARFmodEventEmitter _audioEmitter;
        private GameObject _haloObject;
        private ParticleSystem _haloParticles;
        private ParticleSystemRenderer _haloRenderer;
        private Material _haloMaterial;
        private Texture2D _haloTexture;
        private bool _initialized;
        private bool _shuttingDown;
        private bool _audioFailureLogged;
        private bool _haloFailureLogged;
        private int _lastFlickerFrame = -1;
        private float _currentFlickerFactor = 1f;
        private float _flickerSeed;

        internal void Initialize(
            TorchlightRekindledPlugin plugin,
            CharacterHandBase hand)
        {
            if (_initialized)
            {
                ApplySettings();
                return;
            }

            _plugin = plugin;
            _hand = hand;
            _flickerSeed = 17.31f
                + Mathf.Abs(hand.GetInstanceID() % 10000) * 0.0137f;
            CaptureLights();
            CaptureFlames();
            _initialized = true;
            _plugin.Register(this);
            ApplySettings();
            StartCoroutine(FinishDeferredInitialization());

            _plugin.LogDiagnostic(
                "Attached to "
                + hand.name
                + "; lights="
                + _lights.Length.ToString(CultureInfo.InvariantCulture)
                + "; flame systems="
                + _flames.Length.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private IEnumerator FinishDeferredInitialization()
        {
            yield return null;

            if (_shuttingDown || _plugin == null)
            {
                yield break;
            }

            if (_flames.Length == 0)
            {
                CaptureFlames();
                ApplySettings();
            }

            for (int index = 0; index < _lights.Length; index++)
            {
                _plugin.LogDiagnostic(
                    "Verified torch light "
                    + index.ToString(CultureInfo.InvariantCulture)
                    + ": "
                    + _lights[index].DescribeCurrentRange(_plugin)
                    + ".");
            }

            if (_audioEmitter != null)
            {
                PLAYBACK_STATE playbackState;
                RESULT result = _audioEmitter.EventInstance
                    .getPlaybackState(out playbackState);
                _plugin.LogDiagnostic(
                    "Verified hand-anchored fire audio: anchor="
                    + GetTransformPath(_audioAnchor)
                    + "; result="
                    + result
                    + "; state="
                    + playbackState
                    + ".");
            }

            _plugin.LogDiagnostic(
                "Deferred flame verification: systems="
                + _flames.Length.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private void OnEnable()
        {
            if (!_initialized || _shuttingDown || _plugin == null)
            {
                return;
            }

            _plugin.Register(this);
            ApplySettings();
        }

        private void OnDisable()
        {
            StopAudio();
            if (_plugin != null)
            {
                _plugin.Unregister(this);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        internal void ApplySettings()
        {
            if (!_initialized || _shuttingDown || _plugin == null)
            {
                return;
            }

            if (!_plugin.FeatureEnabled)
            {
                RestoreVisuals();
                DestroyHalo();
                StopAudio();
                return;
            }

            float lightBrightness = Mathf.Clamp(
                _plugin.LightBrightnessMultiplier,
                0.25f,
                3f);
            float effectiveLightBrightness = Mathf.Max(
                0f,
                1f + (lightBrightness - 1f) * 2f) * 5f;
            float rangeBonus = Mathf.Clamp(
                _plugin.RangeBonusMeters,
                0f,
                10f) * (20f / 3f);
            for (int index = 0; index < _lights.Length; index++)
            {
                _lights[index].Apply(
                    _plugin,
                    effectiveLightBrightness,
                    rangeBonus);
            }

            float flameBrightness = Mathf.Clamp(
                _plugin.FlameBrightnessMultiplier,
                0.25f,
                3f) * 3f;
            float flameBloom = Mathf.Clamp(
                _plugin.FlameBloomMultiplier,
                0f,
                3f) * 3f;
            for (int index = 0; index < _flames.Length; index++)
            {
                _flames[index].Apply(
                    flameBrightness,
                    flameBloom);
            }

            ApplyHalo();
            ApplyAudio();
        }

        internal void ApplyLightFlicker(LightController controller)
        {
            if (!_initialized
                || _shuttingDown
                || _plugin == null
                || !_plugin.FeatureEnabled
                || !isActiveAndEnabled)
            {
                return;
            }

            float strength = Mathf.Clamp(
                _plugin.LightFlickerStrength,
                0f,
                2f);
            if (strength <= 0f)
            {
                return;
            }

            if (_lastFlickerFrame != Time.frameCount)
            {
                _lastFlickerFrame = Time.frameCount;
                float speed = Mathf.Clamp(
                    _plugin.LightFlickerSpeed,
                    0.5f,
                    2f);
                float time = Time.time * speed;
                float slowNoise = Mathf.PerlinNoise(
                        _flickerSeed,
                        time * 0.85f)
                    - 0.5f;
                float fastNoise = Mathf.PerlinNoise(
                        _flickerSeed + 37.19f,
                        time * 6.4f)
                    - 0.5f;
                _currentFlickerFactor = Mathf.Clamp(
                    1f + strength
                        * (slowNoise * 0.14f
                            + fastNoise * 0.06f),
                    0.8f,
                    1.2f);
            }

            for (int index = 0; index < _lights.Length; index++)
            {
                if (_lights[index].ApplyFlicker(
                    controller,
                    _currentFlickerFactor))
                {
                    break;
                }
            }
        }

        internal void Shutdown()
        {
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            UnregisterLightControllers();
            RestoreVisuals();
            DestroyHalo();
            StopAudio();
            if (_plugin != null)
            {
                _plugin.Unregister(this);
            }
        }

        private void CaptureLights()
        {
            UnregisterLightControllers();
            LightController[] controllers =
                GetComponentsInChildren<LightController>(true);
            List<LightState> states =
                new List<LightState>(controllers.Length);
            foreach (LightController controller in controllers)
            {
                if (controller == null)
                {
                    continue;
                }

                controller.BakeNativeIntensity();
                Light light = controller.GetComponent<Light>();
                HDAdditionalLightData lightData =
                    controller.GetComponent<HDAdditionalLightData>();
                if (light == null || lightData == null)
                {
                    continue;
                }

                states.Add(
                    new LightState(
                        controller,
                        light,
                        lightData,
                        _plugin.ReadBakedIntensity(controller),
                        _plugin.ReadRange(controller)));
            }

            _lights = states.ToArray();
            for (int index = 0; index < _lights.Length; index++)
            {
                _plugin.RegisterLightController(
                    _lights[index].Controller,
                    this);
            }
        }

        private void UnregisterLightControllers()
        {
            if (_plugin == null)
            {
                return;
            }

            for (int index = 0; index < _lights.Length; index++)
            {
                _plugin.UnregisterLightController(
                    _lights[index].Controller,
                    this);
            }
        }

        private void CaptureFlames()
        {
            VisualEffect[] visualEffects =
                GetComponentsInChildren<VisualEffect>(true);
            List<FlameState> states = new List<FlameState>();
            Transform bestAudioAnchor = null;
            foreach (VisualEffect visualEffect in visualEffects)
            {
                if (visualEffect == null)
                {
                    continue;
                }

                if (bestAudioAnchor == null
                    || IsLikelyTorchFlame(visualEffect.name))
                {
                    bestAudioAnchor = visualEffect.transform;
                }

                FlameState state = new FlameState(visualEffect);
                if (state.HasProperties)
                {
                    states.Add(state);
                    if (IsLikelyTorchFlame(visualEffect.name))
                    {
                        bestAudioAnchor = visualEffect.transform;
                    }
                }
            }

            _audioAnchor = bestAudioAnchor == null
                ? _hand.transform
                : bestAudioAnchor;
            _flames = states.ToArray();

            if (_haloObject != null)
            {
                _haloObject.transform.SetParent(_audioAnchor, false);
            }

            _plugin.LogDiagnostic(
                "Flame discovery: visual effects="
                + visualEffects.Length.ToString(CultureInfo.InvariantCulture)
                + "; controllable systems="
                + _flames.Length.ToString(CultureInfo.InvariantCulture)
                + "; audio anchor="
                + GetTransformPath(_audioAnchor)
                + ".");
        }

        private void ApplyHalo()
        {
            float strength = Mathf.Clamp(
                _plugin.FlameHaloStrength,
                0f,
                3f);
            float size = Mathf.Clamp(
                _plugin.FlameHaloSize,
                0.02f,
                0.25f);
            if (strength <= 0f
                || _audioAnchor == null
                || !isActiveAndEnabled)
            {
                DestroyHalo();
                return;
            }

            if (_haloObject == null)
            {
                Shader shader = Shader.Find("HDRP/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("HDRP/Lit");
                }
                if (shader == null)
                {
                    if (!_haloFailureLogged)
                    {
                        _haloFailureLogged = true;
                        _plugin.LogWarning(
                            "Could not create the flame corona because an HDRP emissive shader was unavailable.");
                    }
                    return;
                }

                _haloObject = new GameObject(
                    "Torchlight Rekindled Flame Corona");
                _haloObject.name =
                    "Torchlight Rekindled Flame Corona";
                _haloObject.layer = _audioAnchor.gameObject.layer;
                _haloObject.transform.SetParent(_audioAnchor, false);

                _haloParticles =
                    _haloObject.AddComponent<ParticleSystem>();
                _haloRenderer = _haloObject.GetComponent<
                    ParticleSystemRenderer>();
                _haloRenderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                _haloRenderer.receiveShadows = false;
                _haloRenderer.lightProbeUsage =
                    UnityEngine.Rendering.LightProbeUsage.Off;
                _haloRenderer.reflectionProbeUsage =
                    UnityEngine.Rendering.ReflectionProbeUsage.Off;

                _haloMaterial = new Material(shader)
                {
                    name = "Torchlight Rekindled Flame Corona Material"
                };
                ConfigureHaloMaterial(_haloMaterial);
                _haloRenderer.sharedMaterial = _haloMaterial;
                _haloRenderer.renderMode =
                    ParticleSystemRenderMode.Billboard;
                _haloRenderer.alignment =
                    ParticleSystemRenderSpace.View;
                _haloRenderer.sortMode =
                    ParticleSystemSortMode.Distance;

                ParticleSystem.MainModule main = _haloParticles.main;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = 999999f;
                main.startSpeed = 0f;
                main.startSize = 1f;
                main.startColor = Color.white;
                main.maxParticles = 1;
                main.simulationSpace =
                    ParticleSystemSimulationSpace.Local;
                main.scalingMode =
                    ParticleSystemScalingMode.Hierarchy;

                ParticleSystem.EmissionModule emissionModule =
                    _haloParticles.emission;
                emissionModule.enabled = false;
                ParticleSystem.ShapeModule shapeModule =
                    _haloParticles.shape;
                shapeModule.enabled = false;
                _haloParticles.Emit(1);
                _haloParticles.Play();
                _plugin.LogDiagnostic(
                    "Created soft additive flame corona at "
                    + GetTransformPath(_audioAnchor)
                    + ".");
            }

            _haloObject.transform.localPosition = Vector3.zero;
            _haloObject.transform.localRotation = Quaternion.identity;
            _haloObject.transform.localScale = Vector3.one * size;
            ApplyHaloMaterial(strength);
        }

        private void ConfigureHaloMaterial(Material material)
        {
            _haloTexture = CreateHaloTexture();
            SetMaterialTextureIfPresent(
                material,
                "_BaseColorMap",
                _haloTexture);
            SetMaterialTextureIfPresent(
                material,
                "_UnlitColorMap",
                _haloTexture);
            SetMaterialTextureIfPresent(
                material,
                "_EmissiveColorMap",
                _haloTexture);
            SetMaterialTextureIfPresent(
                material,
                "_MainTex",
                _haloTexture);

            SetMaterialFloatIfPresent(material, "_SurfaceType", 1f);
            SetMaterialFloatIfPresent(material, "_BlendMode", 1f);
            SetMaterialFloatIfPresent(
                material,
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.One);
            SetMaterialFloatIfPresent(
                material,
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.One);
            SetMaterialFloatIfPresent(
                material,
                "_AlphaSrcBlend",
                (float)UnityEngine.Rendering.BlendMode.One);
            SetMaterialFloatIfPresent(
                material,
                "_AlphaDstBlend",
                (float)UnityEngine.Rendering.BlendMode.One);
            SetMaterialFloatIfPresent(material, "_ZWrite", 0f);
            SetMaterialFloatIfPresent(
                material,
                "_TransparentZWrite",
                0f);
            SetMaterialFloatIfPresent(
                material,
                "_AlphaCutoffEnable",
                0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue =
                (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_BLENDMODE_ADD");
            material.DisableKeyword("_BLENDMODE_ALPHA");
        }

        private static Texture2D CreateHaloTexture()
        {
            const int textureSize = 64;
            Texture2D texture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Torchlight Rekindled Flame Corona Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                float normalizedY = (y + 0.5f) / textureSize * 2f - 1f;
                for (int x = 0; x < textureSize; x++)
                {
                    float normalizedX =
                        (x + 0.5f) / textureSize * 2f - 1f;
                    float radius = Mathf.Sqrt(
                        normalizedX * normalizedX
                        + normalizedY * normalizedY);
                    float falloff = Mathf.Clamp01(1f - radius);
                    falloff = falloff * falloff
                        * (3f - 2f * falloff);
                    pixels[y * textureSize + x] = new Color(
                        falloff,
                        falloff,
                        falloff,
                        falloff);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void ApplyHaloMaterial(float strength)
        {
            if (_haloMaterial == null)
            {
                return;
            }

            Color warmCore = new Color(1f, 0.16f, 0.02f, 1f);
            Color emission = warmCore * (12f * strength);
            emission.a = 1f;

            SetMaterialColorIfPresent(
                _haloMaterial,
                "_BaseColor",
                emission);
            SetMaterialColorIfPresent(
                _haloMaterial,
                "_UnlitColor",
                emission);
            SetMaterialColorIfPresent(
                _haloMaterial,
                "_EmissiveColor",
                emission);
            SetMaterialColorIfPresent(
                _haloMaterial,
                "_EmissionColor",
                emission);
            SetMaterialFloatIfPresent(
                _haloMaterial,
                "_UseEmissiveIntensity",
                0f);
            SetMaterialFloatIfPresent(
                _haloMaterial,
                "_EmissiveExposureWeight",
                0f);
            _haloMaterial.EnableKeyword("_EMISSION");
        }

        private static void SetMaterialColorIfPresent(
            Material material,
            string property,
            Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetMaterialFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetMaterialTextureIfPresent(
            Material material,
            string property,
            Texture value)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, value);
            }
        }

        private void DestroyHalo()
        {
            if (_haloObject != null)
            {
                _haloObject.SetActive(false);
                Destroy(_haloObject);
            }
            if (_haloMaterial != null)
            {
                Destroy(_haloMaterial);
            }
            if (_haloTexture != null)
            {
                Destroy(_haloTexture);
            }
            _haloObject = null;
            _haloParticles = null;
            _haloRenderer = null;
            _haloMaterial = null;
            _haloTexture = null;
        }

        private static bool IsLikelyTorchFlame(string name)
        {
            return !string.IsNullOrEmpty(name)
                && (name.IndexOf(
                        "Fire",
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf(
                        "Torch",
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return "<missing>";
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private void RestoreVisuals()
        {
            for (int index = 0; index < _lights.Length; index++)
            {
                _lights[index].Restore(_plugin);
            }
            for (int index = 0; index < _flames.Length; index++)
            {
                _flames[index].Restore();
            }
        }

        private void ApplyAudio()
        {
            if (!_plugin.LoopingFireAudio
                || _plugin.LoopingFireVolume <= 0f
                || !isActiveAndEnabled)
            {
                StopAudio();
                return;
            }

            if (_audioEmitter == null)
            {
                _audioObject = new GameObject(
                    "Torchlight Rekindled Hand Audio");
                _audioObject.transform.SetParent(
                    _audioAnchor == null ? _hand.transform : _audioAnchor,
                    false);
                _audioEmitter =
                    _audioObject.AddComponent<ARFmodEventEmitter>();
                _audioEmitter.AllowFadeout = true;
                _audioEmitter.EventReference = new EventReference
                {
                    Guid = TorchlightRekindledPlugin.TorchFireLoopEventGuid
                };
                _audioEmitter.PlayCurrentEventWithPauseTracking();
            }

            EventInstance audioInstance = _audioEmitter.EventInstance;
            if (!audioInstance.isValid())
            {
                if (!_audioFailureLogged)
                {
                    _audioFailureLogged = true;
                    _plugin.LogWarning(
                        "Could not play the built-in torch fire loop "
                        + TorchlightRekindledPlugin.TorchFireLoopEventPath
                        + " from the equipped hand.");
                }
                StopAudio();
                return;
            }

            RESULT volumeResult = audioInstance.setVolume(
                Mathf.Clamp(
                    _plugin.LoopingFireVolume * 2f,
                    0f,
                    4f));
            if (volumeResult != RESULT.OK && !_audioFailureLogged)
            {
                _audioFailureLogged = true;
                _plugin.LogWarning(
                    "Could not set the held torch fire volume. Result="
                    + volumeResult
                    + ".");
            }
        }

        private void StopAudio()
        {
            if (_audioEmitter != null)
            {
                _audioEmitter.Stop();
            }

            if (_audioObject != null)
            {
                Destroy(_audioObject);
            }
            _audioEmitter = null;
            _audioObject = null;
        }

        private sealed class LightState
        {
            private readonly LightController _controller;
            private readonly Light _light;
            private readonly HDAdditionalLightData _lightData;
            private readonly LightController.NativeIntensityData _originalIntensity;
            private readonly LightController.RangeToggleObject _originalRange;
            private readonly float _originalLightRange;
            private readonly float _originalHdRange;

            internal LightController Controller => _controller;

            internal LightState(
                LightController controller,
                Light light,
                HDAdditionalLightData lightData,
                LightController.NativeIntensityData originalIntensity,
                LightController.RangeToggleObject originalRange)
            {
                _controller = controller;
                _light = light;
                _lightData = lightData;
                _originalIntensity = CloneIntensity(originalIntensity, 1f);
                _originalRange = originalRange;
                _originalLightRange = light.range;
                _originalHdRange = lightData.range;
            }

            internal void Apply(
                TorchlightRekindledPlugin plugin,
                float brightnessMultiplier,
                float rangeBonus)
            {
                if (_controller == null
                    || _light == null
                    || _lightData == null)
                {
                    return;
                }

                plugin.WriteBakedIntensity(
                    _controller,
                    CloneIntensity(
                        _originalIntensity,
                        brightnessMultiplier));
                plugin.WriteRange(
                    _controller,
                    AddRangeBonus(_originalRange, rangeBonus));
                _lightData.range = _originalHdRange + rangeBonus;
                _light.range = _originalLightRange + rangeBonus;
                _controller.UpdateOnce();

                plugin.LogDiagnostic(
                    "Applied torch light range: base="
                    + _originalHdRange.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; bonus="
                    + rangeBonus.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; current="
                    + _lightData.range.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; controllerManaged="
                    + _originalRange.useRange
                    + ".");
            }

            internal void Restore(TorchlightRekindledPlugin plugin)
            {
                if (_controller == null
                    || _light == null
                    || _lightData == null
                    || plugin == null)
                {
                    return;
                }

                plugin.WriteBakedIntensity(
                    _controller,
                    CloneIntensity(_originalIntensity, 1f));
                plugin.WriteRange(_controller, _originalRange);
                _lightData.range = _originalHdRange;
                _light.range = _originalLightRange;
                _controller.UpdateOnce();
            }

            internal string DescribeCurrentRange(
                TorchlightRekindledPlugin plugin)
            {
                if (_controller == null
                    || _light == null
                    || _lightData == null
                    || plugin == null)
                {
                    return "unavailable";
                }

                LightController.RangeToggleObject currentRange =
                    plugin.ReadRange(_controller);
                return "HDRP="
                    + _lightData.range.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; Unity="
                    + _light.range.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; controllerManaged="
                    + currentRange.useRange
                    + "; static="
                    + currentRange.rangeStaticMin.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + "-"
                    + currentRange.rangeStaticMax.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; dynamic="
                    + currentRange.rangeDynamicMin.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + "-"
                    + currentRange.rangeDynamicMax.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m";
            }

            internal bool ApplyFlicker(
                LightController controller,
                float multiplier)
            {
                if (!ReferenceEquals(_controller, controller))
                {
                    return false;
                }
                if (_light != null)
                {
                    _light.intensity *= multiplier;
                }
                return true;
            }

            private static LightController.RangeToggleObject AddRangeBonus(
                LightController.RangeToggleObject range,
                float bonus)
            {
                if (!range.useRange)
                {
                    return range;
                }

                range.rangeStaticMin += bonus;
                range.rangeStaticMax += bonus;
                range.rangeDynamicMin += bonus;
                range.rangeDynamicMax += bonus;
                if (range.rangeCurve != null
                    && !Mathf.Approximately(range.rangeMultiplier, 0f))
                {
                    range.rangeCurve = OffsetCurve(
                        range.rangeCurve,
                        bonus / range.rangeMultiplier);
                }
                return range;
            }

            private static AnimationCurve OffsetCurve(
                AnimationCurve source,
                float offset)
            {
                Keyframe[] keys = source.keys;
                for (int index = 0; index < keys.Length; index++)
                {
                    Keyframe key = keys[index];
                    key.value += offset;
                    keys[index] = key;
                }

                return new AnimationCurve(keys)
                {
                    preWrapMode = source.preWrapMode,
                    postWrapMode = source.postWrapMode
                };
            }

            private static LightController.NativeIntensityData CloneIntensity(
                LightController.NativeIntensityData source,
                float multiplier)
            {
                return new LightController.NativeIntensityData
                {
                    intensityCurve = CloneCurve(
                        source.intensityCurve,
                        multiplier),
                    intensityMinOrBaseIntensity =
                        source.intensityMinOrBaseIntensity * multiplier,
                    intensityMax = source.intensityMax * multiplier
                };
            }

            private static AnimationCurve CloneCurve(
                AnimationCurve source,
                float multiplier)
            {
                if (source == null)
                {
                    return null;
                }

                Keyframe[] keys = source.keys;
                for (int index = 0; index < keys.Length; index++)
                {
                    Keyframe key = keys[index];
                    key.value *= multiplier;
                    key.inTangent *= multiplier;
                    key.outTangent *= multiplier;
                    keys[index] = key;
                }

                AnimationCurve clone = new AnimationCurve(keys)
                {
                    preWrapMode = source.preWrapMode,
                    postWrapMode = source.postWrapMode
                };
                return clone;
            }
        }

        private sealed class FlameState
        {
            private readonly VisualEffect _visualEffect;
            private readonly string[] _gradientNames;
            private readonly Gradient[] _originalGradients;
            private readonly string[] _colorNames;
            private readonly Color[] _originalColors;

            internal bool HasProperties =>
                _gradientNames.Length > 0 || _colorNames.Length > 0;

            internal FlameState(VisualEffect visualEffect)
            {
                _visualEffect = visualEffect;
                List<string> names = new List<string>();
                List<Gradient> gradients = new List<Gradient>();
                List<string> colorNames = new List<string>();
                List<Color> colors = new List<Color>();
                List<VFXExposedProperty> exposedProperties =
                    new List<VFXExposedProperty>();
                if (visualEffect.visualEffectAsset != null)
                {
                    visualEffect.visualEffectAsset.GetExposedProperties(
                        exposedProperties);
                }

                foreach (VFXExposedProperty property in exposedProperties)
                {
                    if (property.type == typeof(Gradient)
                        && visualEffect.HasGradient(property.name))
                    {
                        names.Add(property.name);
                        gradients.Add(CloneGradient(
                            visualEffect.GetGradient(property.name),
                            1f,
                            1f));
                    }
                    else if ((property.type == typeof(Color)
                            || (property.type == typeof(Vector4)
                                && IsLikelyColorProperty(property.name)))
                        && visualEffect.HasVector4(property.name))
                    {
                        colorNames.Add(property.name);
                        colors.Add(visualEffect.GetVector4(property.name));
                    }
                }

                _gradientNames = names.ToArray();
                _originalGradients = gradients.ToArray();
                _colorNames = colorNames.ToArray();
                _originalColors = colors.ToArray();
            }

            private static bool IsLikelyColorProperty(string name)
            {
                return !string.IsNullOrEmpty(name)
                    && (name.IndexOf(
                            "Color",
                            StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf(
                            "Tint",
                            StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf(
                            "Emission",
                            StringComparison.OrdinalIgnoreCase) >= 0);
            }

            internal void Apply(
                float brightnessMultiplier,
                float bloomMultiplier)
            {
                if (_visualEffect == null)
                {
                    return;
                }

                for (int index = 0;
                    index < _gradientNames.Length;
                    index++)
                {
                    _visualEffect.SetGradient(
                        _gradientNames[index],
                        CloneGradient(
                            _originalGradients[index],
                            brightnessMultiplier,
                            bloomMultiplier));
                }
                for (int index = 0;
                    index < _colorNames.Length;
                    index++)
                {
                    _visualEffect.SetVector4(
                        _colorNames[index],
                        ScaleHdrColor(
                            _originalColors[index],
                            brightnessMultiplier,
                            bloomMultiplier));
                }
            }

            internal void Restore()
            {
                if (_visualEffect == null)
                {
                    return;
                }

                for (int index = 0;
                    index < _gradientNames.Length;
                    index++)
                {
                    _visualEffect.SetGradient(
                        _gradientNames[index],
                        CloneGradient(
                            _originalGradients[index],
                            1f,
                            1f));
                }
                for (int index = 0;
                    index < _colorNames.Length;
                    index++)
                {
                    _visualEffect.SetVector4(
                        _colorNames[index],
                        _originalColors[index]);
                }
            }

            private static Color ScaleHdrColor(
                Color source,
                float brightnessMultiplier,
                float bloomMultiplier)
            {
                source.r = ScaleHdrChannel(
                    source.r,
                    brightnessMultiplier,
                    bloomMultiplier);
                source.g = ScaleHdrChannel(
                    source.g,
                    brightnessMultiplier,
                    bloomMultiplier);
                source.b = ScaleHdrChannel(
                    source.b,
                    brightnessMultiplier,
                    bloomMultiplier);
                return source;
            }

            private static Gradient CloneGradient(
                Gradient source,
                float brightnessMultiplier,
                float bloomMultiplier)
            {
                GradientColorKey[] colorKeys = source.colorKeys;
                for (int index = 0; index < colorKeys.Length; index++)
                {
                    GradientColorKey key = colorKeys[index];
                    Color color = key.color;
                    color.r = ScaleHdrChannel(
                        color.r,
                        brightnessMultiplier,
                        bloomMultiplier);
                    color.g = ScaleHdrChannel(
                        color.g,
                        brightnessMultiplier,
                        bloomMultiplier);
                    color.b = ScaleHdrChannel(
                        color.b,
                        brightnessMultiplier,
                        bloomMultiplier);
                    key.color = color;
                    colorKeys[index] = key;
                }

                Gradient clone = new Gradient();
                clone.SetKeys(colorKeys, source.alphaKeys);
                clone.mode = source.mode;
                return clone;
            }

            private static float ScaleHdrChannel(
                float value,
                float brightnessMultiplier,
                float bloomMultiplier)
            {
                float brightened = value * brightnessMultiplier;
                if (brightened <= 1f)
                {
                    return brightened;
                }

                return 1f + (brightened - 1f) * bloomMultiplier;
            }
        }
    }
}
