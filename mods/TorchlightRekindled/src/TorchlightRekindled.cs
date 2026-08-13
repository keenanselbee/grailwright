using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Main.Animations.FSM.Heroes.Base;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.Graphics.VFX;
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Scenes;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using BepInEx;
using BepInEx.Configuration;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;

[assembly: AssemblyTitle("Torchlight Rekindled")]
[assembly: AssemblyDescription("Configurable held-torch lighting, flame glow, and looping fire audio for Tainted Grail: The Fall of Avalon.")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Torchlight Rekindled")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("0.4.3.0")]
[assembly: AssemblyFileVersion("0.4.3.0")]

namespace TorchlightRekindled
{
    public enum TorchBrightnessPreset
    {
        Vanilla,
        Bright
    }

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
        public const string PluginVersion = "0.4.3";

        private const int ConfigSchemaVersion = 11;
        private const int ConfigRecoveryBaselineSchema = 1;
        private const float InteriorBloomCheckIntervalSeconds = 1f;
        private const float InteriorBloomPriority = 10010f;
        private const float InteriorBloomEpsilon = 0.001f;
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
                        "Displayed 1 is now the recommended fire-loop volume."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "2. Torch Light",
                        "LightFlickerStrength",
                        "Displayed flicker strength now uses a doubled internal response."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "2. Torch Light",
                        "LightFlickerSpeed",
                        "Displayed flicker speed now uses a doubled internal response."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "3. Flame",
                        "FlameHaloStrength",
                        "The recommended flame-halo strength default is now 3."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "3. Flame",
                        "FlameHaloSize",
                        "The recommended flame-halo size default is now 0.065 metres."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "3. Flame",
                        "FlameHaloVerticalScale",
                        "The recommended flame-halo vertical scale default is now 2.5."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "3. Flame",
                        "FlameHaloVerticalOffset",
                        "The recommended flame-halo vertical offset default is now 0.45."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "4. Audio",
                        "LoopingFireVolume",
                        "Displayed fire-loop volume now uses four-times internal gain."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        7,
                        "2. Torch Light",
                        "LightFlickerStrength",
                        "Displayed flicker strength now uses a four-times internal response."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        9,
                        "2. Torch Light",
                        "RangeBonusMeters",
                        "RangeBonusMeters now stores literal metres instead of an amplified compact control.")
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
        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedSettings =
                new Dictionary<ConfigDefinition, object>();
        private Harmony _harmony;
        private FieldInfo _bakedNativeIntensityField;
        private FieldInfo _rangeField;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<TorchBrightnessPreset> _interiorBrightnessPreset;
        private ConfigEntry<TorchBrightnessPreset> _exteriorBrightnessPreset;
        private ConfigEntry<float> _rangeBonusMeters;
        private ConfigEntry<float> _lightBrightnessMultiplier;
        private ConfigEntry<float> _lightFlickerStrength;
        private ConfigEntry<float> _lightFlickerSpeed;
        private ConfigEntry<float> _flameBrightnessMultiplier;
        private ConfigEntry<float> _flameBloomMultiplier;
        private ConfigEntry<float> _flameHaloStrength;
        private ConfigEntry<float> _flameHaloSize;
        private ConfigEntry<float> _flameHaloVerticalScale;
        private ConfigEntry<float> _flameHaloVerticalOffset;
        private ConfigEntry<float> _flameHaloHorizontalOffset;
        private ConfigEntry<float> _flameHaloAxisPitchOffsetDegrees;
        private ConfigEntry<float> _flameHaloAxisYawOffsetDegrees;
        private ConfigEntry<float> _flameHaloRotationOffsetDegrees;
        private ConfigEntry<float> _flameHaloLightParryRotationOffsetDegrees;
        private ConfigEntry<bool> _enhanceInteriorBloom;
        private ConfigEntry<bool> _interiorBloomOnlyWhileTorchEquipped;
        private ConfigEntry<float> _interiorBloomThreshold;
        private ConfigEntry<float> _interiorBloomIntensity;
        private ConfigEntry<float> _interiorBloomScatter;
        private ConfigEntry<bool> _loopingFireAudio;
        private ConfigEntry<float> _loopingFireVolume;
        private ConfigEntry<bool> _diagnostics;
        private GameObject _interiorBloomObject;
        private Volume _interiorBloomVolume;
        private VolumeProfile _interiorBloomProfile;
        private Bloom _interiorBloomOverride;
        private Coroutine _interiorBloomSampleCoroutine;
        private string _pendingInteriorBloomSceneKey;
        private string _nativeBloomSceneKey;
        private float _nativeBloomThreshold;
        private float _nativeBloomIntensity;
        private float _nativeBloomScatter;
        private float _lastAppliedBloomThreshold = float.NaN;
        private float _lastAppliedBloomIntensity = float.NaN;
        private float _lastAppliedBloomScatter = float.NaN;
        private float _nextInteriorBloomCheckTime;
        private bool _nativeBloomCaptured;
        private bool _wasInInteriorContext;
        private bool _interiorBloomApplied;
        private bool _interiorBloomUnavailable;
        private bool _sceneContextKnown;
        private bool _isInteriorContext;

        internal bool FeatureEnabled =>
            _enabled != null && _enabled.Value;

        internal float TorchBrightnessPresetScale =>
            CurrentBrightnessPreset == TorchBrightnessPreset.Vanilla
                ? 0.5f
                : 1f;

        private TorchBrightnessPreset CurrentBrightnessPreset =>
            _sceneContextKnown && _isInteriorContext
                ? (_interiorBrightnessPreset == null
                    ? TorchBrightnessPreset.Bright
                    : _interiorBrightnessPreset.Value)
                : (_exteriorBrightnessPreset == null
                    ? TorchBrightnessPreset.Vanilla
                    : _exteriorBrightnessPreset.Value);

        internal float RangeBonusMeters =>
            _rangeBonusMeters == null ? 20f : _rangeBonusMeters.Value;

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
            _flameHaloStrength == null ? 5f : _flameHaloStrength.Value;

        internal float FlameHaloSize =>
            _flameHaloSize == null ? 0.07f : _flameHaloSize.Value;

        internal float FlameHaloVerticalScale =>
            _flameHaloVerticalScale == null
                ? 2.2f
                : _flameHaloVerticalScale.Value;

        internal float FlameHaloVerticalOffset =>
            _flameHaloVerticalOffset == null
                ? 0.45f
                : _flameHaloVerticalOffset.Value;

        internal float FlameHaloHorizontalOffset =>
            _flameHaloHorizontalOffset == null
                ? -0.12f
                : _flameHaloHorizontalOffset.Value;

        internal float FlameHaloAxisPitchOffsetDegrees =>
            _flameHaloAxisPitchOffsetDegrees == null
                ? 0f
                : _flameHaloAxisPitchOffsetDegrees.Value;

        internal float FlameHaloAxisYawOffsetDegrees =>
            _flameHaloAxisYawOffsetDegrees == null
                ? 0f
                : _flameHaloAxisYawOffsetDegrees.Value;

        internal float FlameHaloRotationOffsetDegrees =>
            _flameHaloRotationOffsetDegrees == null
                ? -20f
                : _flameHaloRotationOffsetDegrees.Value;

        internal float FlameHaloLightParryRotationOffsetDegrees =>
            _flameHaloLightParryRotationOffsetDegrees == null
                ? 90f
                : _flameHaloLightParryRotationOffsetDegrees.Value;

        internal bool InteriorBloomOnlyWhileTorchEquipped =>
            _interiorBloomOnlyWhileTorchEquipped != null
                && _interiorBloomOnlyWhileTorchEquipped.Value;

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
            DestroyInteriorBloomController();

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

        private void Update()
        {
            if (Time.unscaledTime < _nextInteriorBloomCheckTime)
            {
                return;
            }

            _nextInteriorBloomCheckTime = Time.unscaledTime
                + InteriorBloomCheckIntervalSeconds;
            RefreshSceneContext();
        }

        private static Grailwright.Shared.ConfigRecoveryUiMetadata ConfigUi(
            string displaySection,
            string displayName,
            int sectionOrder,
            int order)
        {
            return new Grailwright.Shared.ConfigRecoveryUiMetadata
            {
                DisplaySection = displaySection,
                DisplayName = displayName,
                SectionOrder = sectionOrder,
                Order = order
            };
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
                new ConfigDescription(
                    "Master switch. Turning this off restores the original held torch.",
                    null,
                    ConfigUi("General", "Enabled", 0, 0)));
            _interiorBrightnessPreset = Config.Bind(
                "2. Torch Light",
                "InteriorBrightnessPreset",
                TorchBrightnessPreset.Bright,
                new ConfigDescription(
                    "Brightness used in interiors. Vanilla is balanced for normal interior brightness; Bright is balanced for darker interiors and is the default.",
                    null,
                    ConfigUi("General", "Interior Brightness Preset", 0, 10)));
            _exteriorBrightnessPreset = Config.Bind(
                "2. Torch Light",
                "ExteriorBrightnessPreset",
                TorchBrightnessPreset.Vanilla,
                new ConfigDescription(
                    "Brightness used outdoors. Vanilla is balanced for normal interior brightness and is the exterior default; Bright is balanced for darker interiors.",
                    null,
                    ConfigUi("General", "Exterior Brightness Preset", 0, 20)));
            _rangeBonusMeters = Config.Bind(
                "2. Torch Light",
                "RangeBonusMeters",
                20f,
                new ConfigDescription(
                    "Additional torch reach in metres. Use less for a tighter pool of light or more for longer sightlines.",
                    new AcceptableValueRange<float>(0f, 70f),
                    ConfigUi("Torch Light", "Range Bonus (m)", 10, 0)));
            _lightBrightnessMultiplier = Config.Bind(
                "2. Torch Light",
                "BrightnessMultiplier",
                1f,
                new ConfigDescription(
                    "How strongly the torch lights walls, floors, and characters.",
                    new AcceptableValueRange<float>(0.25f, 3f),
                    ConfigUi("Torch Light", "World Illumination", 10, 10)));
            _lightFlickerStrength = Config.Bind(
                "2. Torch Light",
                "LightFlickerStrength",
                1f,
                new ConfigDescription(
                    "Additional irregular illumination variation. Zero keeps only the original flicker.",
                    new AcceptableValueRange<float>(0f, 2f),
                    ConfigUi("Torch Light", "Flicker Amount", 10, 20)));
            _lightFlickerSpeed = Config.Bind(
                "2. Torch Light",
                "LightFlickerSpeed",
                1f,
                new ConfigDescription(
                    "Speed of the additional illumination flicker.",
                    new AcceptableValueRange<float>(0.5f, 2f),
                    ConfigUi("Torch Light", "Flicker Speed", 10, 30)));
            _flameBrightnessMultiplier = Config.Bind(
                "3. Flame",
                "FlameBrightnessMultiplier",
                0.75f,
                new ConfigDescription(
                    "Brightness of the visible flame, embers, and sparks without changing world illumination.",
                    new AcceptableValueRange<float>(0.25f, 3f),
                    ConfigUi("Visible Flame", "Visible Flame Brightness", 20, 0)));
            _flameBloomMultiplier = Config.Bind(
                "3. Flame",
                "FlameBloomMultiplier",
                0.75f,
                new ConfigDescription(
                    "Extra HDR glare from the visible flame. Zero removes the extra headroom without removing the flame.",
                    new AcceptableValueRange<float>(0f, 3f),
                    ConfigUi("Visible Flame", "Flame Bloom Strength", 20, 10)));
            _flameHaloStrength = Config.Bind(
                "3. Flame",
                "FlameHaloStrength",
                5f,
                new ConfigDescription(
                    "Strength of the additional warm corona. Zero disables it.",
                    new AcceptableValueRange<float>(0f, 10f),
                    ConfigUi("Flame Halo", "Halo Strength", 30, 0)));
            _flameHaloSize = Config.Bind(
                "3. Flame",
                "FlameHaloSize",
                0.07f,
                new ConfigDescription(
                    "Diameter in metres of the corona's emissive core.",
                    new AcceptableValueRange<float>(0.02f, 0.25f),
                    ConfigUi("Flame Halo", "Halo Size (m)", 30, 10)));
            _flameHaloVerticalScale = Config.Bind(
                "3. Flame",
                "FlameHaloVerticalScale",
                2.2f,
                new ConfigDescription(
                    "Height of the corona relative to its width.",
                    new AcceptableValueRange<float>(0.25f, 4f),
                    ConfigUi("Flame Halo", "Halo Height Scale", 30, 20)));
            _flameHaloVerticalOffset = Config.Bind(
                "3. Flame",
                "FlameHaloVerticalOffset",
                0.45f,
                new ConfigDescription(
                    "Vertical position as a fraction of the scaled halo height.",
                    new AcceptableValueRange<float>(-1f, 1f),
                    ConfigUi("Flame Halo", "Vertical Offset (scaled height)", 30, 30)));
            _flameHaloHorizontalOffset = Config.Bind(
                "3. Flame",
                "FlameHaloHorizontalOffset",
                -0.12f,
                new ConfigDescription(
                    "Torch-local sideways position as a fraction of halo width.",
                    new AcceptableValueRange<float>(-1f, 1f),
                    ConfigUi("Flame Halo", "Torch-Local Side Offset (width)", 30, 40)));
            _flameHaloAxisPitchOffsetDegrees = Config.Bind(
                "3. Flame",
                "FlameHaloAxisPitchOffsetDegrees",
                0f,
                new ConfigDescription(
                    "Forward or backward torch-local correction to the tracked halo axis.",
                    new AcceptableValueRange<float>(-45f, 45f),
                    ConfigUi("Halo Alignment - Advanced", "Axis Pitch Offset (deg)", 40, 0)));
            _flameHaloAxisYawOffsetDegrees = Config.Bind(
                "3. Flame",
                "FlameHaloAxisYawOffsetDegrees",
                0f,
                new ConfigDescription(
                    "Sideways torch-local correction to the tracked halo axis.",
                    new AcceptableValueRange<float>(-45f, 45f),
                    ConfigUi("Halo Alignment - Advanced", "Axis Yaw Offset (deg)", 40, 10)));
            _flameHaloRotationOffsetDegrees = Config.Bind(
                "3. Flame",
                "FlameHaloRotationOffsetDegrees",
                -20f,
                new ConfigDescription(
                    "Screen-space roll correction added to the detected torch angle.",
                    new AcceptableValueRange<float>(-180f, 180f),
                    ConfigUi("Halo Alignment - Advanced", "Screen Roll Offset (deg)", 40, 20)));
            _flameHaloLightParryRotationOffsetDegrees = Config.Bind(
                "3. Flame",
                "FlameHaloBashRotationOffsetDegrees",
                90f,
                new ConfigDescription(
                    "Additional screen-space roll during the quick light-parry animation. The stored key retains its old name for config compatibility.",
                    new AcceptableValueRange<float>(-180f, 180f),
                    ConfigUi("Halo Alignment - Advanced", "Light Parry Roll Offset (deg)", 40, 30)));
            _enhanceInteriorBloom = Config.Bind(
                "3. Interior Bloom",
                "EnhanceInteriorBloom",
                true,
                new ConfigDescription(
                    "Raise weak native interior bloom settings to the configured minimums.",
                    null,
                    ConfigUi("Interior Bloom", "Enabled", 50, 0)));
            _interiorBloomOnlyWhileTorchEquipped = Config.Bind(
                "3. Interior Bloom",
                "InteriorBloomOnlyWhileTorchEquipped",
                false,
                new ConfigDescription(
                    "Apply the interior bloom floor only while a torch is equipped.",
                    null,
                    ConfigUi("Interior Bloom", "Only While Torch Equipped", 50, 10)));
            _interiorBloomThreshold = Config.Bind(
                "3. Interior Bloom",
                "InteriorBloomThreshold",
                1f,
                new ConfigDescription(
                    "Highest brightness threshold allowed for weak interior bloom.",
                    new AcceptableValueRange<float>(0f, 4f),
                    ConfigUi("Interior Bloom", "Threshold", 50, 20)));
            _interiorBloomIntensity = Config.Bind(
                "3. Interior Bloom",
                "InteriorBloomIntensity",
                0.25f,
                new ConfigDescription(
                    "Minimum bloom intensity used in weak interiors.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ConfigUi("Interior Bloom", "Intensity", 50, 30)));
            _interiorBloomScatter = Config.Bind(
                "3. Interior Bloom",
                "InteriorBloomScatter",
                0.65f,
                new ConfigDescription(
                    "Minimum bloom spread used in weak interiors.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ConfigUi("Interior Bloom", "Scatter", 50, 40)));
            _loopingFireAudio = Config.Bind(
                "4. Audio",
                "LoopingFireAudio",
                true,
                new ConfigDescription(
                    "Play the game's spatial small-fire loop from the held torch.",
                    null,
                    ConfigUi("Audio", "Enabled", 60, 0)));
            _loopingFireVolume = Config.Bind(
                "4. Audio",
                "LoopingFireVolume",
                1f,
                new ConfigDescription(
                    "Loudness of the fire crackle from the equipped torch.",
                    new AcceptableValueRange<float>(0f, 2f),
                    ConfigUi("Audio", "Volume", 60, 10)));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                new ConfigDescription(
                    "Log torch attachment, interior bloom, and runtime details.",
                    null,
                    ConfigUi("Diagnostics", "Diagnostics", 70, 0)));

            RestorePreservedSettings();
            _enabled.SettingChanged += OnRuntimeSettingChanged;
            _interiorBrightnessPreset.SettingChanged +=
                OnRuntimeSettingChanged;
            _exteriorBrightnessPreset.SettingChanged +=
                OnRuntimeSettingChanged;
            _rangeBonusMeters.SettingChanged += OnRuntimeSettingChanged;
            _lightBrightnessMultiplier.SettingChanged += OnRuntimeSettingChanged;
            _lightFlickerStrength.SettingChanged += OnRuntimeSettingChanged;
            _lightFlickerSpeed.SettingChanged += OnRuntimeSettingChanged;
            _flameBrightnessMultiplier.SettingChanged += OnRuntimeSettingChanged;
            _flameBloomMultiplier.SettingChanged += OnRuntimeSettingChanged;
            _flameHaloStrength.SettingChanged += OnRuntimeSettingChanged;
            _flameHaloSize.SettingChanged += OnRuntimeSettingChanged;
            _flameHaloVerticalScale.SettingChanged += OnRuntimeSettingChanged;
            _flameHaloVerticalOffset.SettingChanged += OnRuntimeSettingChanged;
            _flameHaloHorizontalOffset.SettingChanged +=
                OnRuntimeSettingChanged;
            _flameHaloAxisPitchOffsetDegrees.SettingChanged +=
                OnRuntimeSettingChanged;
            _flameHaloAxisYawOffsetDegrees.SettingChanged +=
                OnRuntimeSettingChanged;
            _flameHaloRotationOffsetDegrees.SettingChanged +=
                OnRuntimeSettingChanged;
            _flameHaloLightParryRotationOffsetDegrees.SettingChanged +=
                OnRuntimeSettingChanged;
            _enhanceInteriorBloom.SettingChanged += OnRuntimeSettingChanged;
            _interiorBloomOnlyWhileTorchEquipped.SettingChanged +=
                OnRuntimeSettingChanged;
            _interiorBloomThreshold.SettingChanged += OnRuntimeSettingChanged;
            _interiorBloomIntensity.SettingChanged += OnRuntimeSettingChanged;
            _interiorBloomScatter.SettingChanged += OnRuntimeSettingChanged;
            _loopingFireAudio.SettingChanged += OnRuntimeSettingChanged;
            _loopingFireVolume.SettingChanged += OnRuntimeSettingChanged;
        }

        private void UnsubscribeConfigEvents()
        {
            if (_enabled != null)
            {
                _enabled.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_interiorBrightnessPreset != null)
            {
                _interiorBrightnessPreset.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_exteriorBrightnessPreset != null)
            {
                _exteriorBrightnessPreset.SettingChanged -=
                    OnRuntimeSettingChanged;
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
            if (_flameHaloVerticalScale != null)
            {
                _flameHaloVerticalScale.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_flameHaloVerticalOffset != null)
            {
                _flameHaloVerticalOffset.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_flameHaloHorizontalOffset != null)
            {
                _flameHaloHorizontalOffset.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_flameHaloAxisPitchOffsetDegrees != null)
            {
                _flameHaloAxisPitchOffsetDegrees.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_flameHaloAxisYawOffsetDegrees != null)
            {
                _flameHaloAxisYawOffsetDegrees.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_flameHaloRotationOffsetDegrees != null)
            {
                _flameHaloRotationOffsetDegrees.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_flameHaloLightParryRotationOffsetDegrees != null)
            {
                _flameHaloLightParryRotationOffsetDegrees.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_enhanceInteriorBloom != null)
            {
                _enhanceInteriorBloom.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_interiorBloomOnlyWhileTorchEquipped != null)
            {
                _interiorBloomOnlyWhileTorchEquipped.SettingChanged -=
                    OnRuntimeSettingChanged;
            }
            if (_interiorBloomThreshold != null)
            {
                _interiorBloomThreshold.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_interiorBloomIntensity != null)
            {
                _interiorBloomIntensity.SettingChanged -= OnRuntimeSettingChanged;
            }
            if (_interiorBloomScatter != null)
            {
                _interiorBloomScatter.SettingChanged -= OnRuntimeSettingChanged;
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
            _nextInteriorBloomCheckTime = 0f;
            RefreshSceneContext();
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

        private void RefreshSceneContext()
        {
            string sceneKey;
            bool isInterior;
            if (!TryResolveSceneContext(out sceneKey, out isInterior))
            {
                DisableInteriorBloom();
                return;
            }

            bool brightnessContextChanged = !_sceneContextKnown
                || _isInteriorContext != isInterior;
            _sceneContextKnown = true;
            _isInteriorContext = isInterior;
            if (brightnessContextChanged)
            {
                ApplyToActiveTorches();
            }

            RefreshInteriorBloomContext(sceneKey, isInterior);
        }

        private void RefreshInteriorBloomContext(
            string sceneKey,
            bool isInterior)
        {
            if (!ShouldEnhanceInteriorBloom())
            {
                DisableInteriorBloom();
                return;
            }

            if (!isInterior)
            {
                DisableInteriorBloom();
                if (_wasInInteriorContext)
                {
                    InvalidateNativeBloomCapture();
                }
                _wasInInteriorContext = false;
                return;
            }

            _wasInInteriorContext = true;
            if (_nativeBloomCaptured
                && string.Equals(
                    _nativeBloomSceneKey,
                    sceneKey,
                    StringComparison.Ordinal))
            {
                ApplyInteriorBloomFloor();
                return;
            }

            QueueNativeBloomCapture(sceneKey);
        }

        private bool ShouldEnhanceInteriorBloom()
        {
            return FeatureEnabled
                && _enhanceInteriorBloom != null
                && _enhanceInteriorBloom.Value
                && (!InteriorBloomOnlyWhileTorchEquipped
                    || _activeTorches.Count > 0);
        }

        private static bool TryResolveSceneContext(
            out string sceneKey,
            out bool isInterior)
        {
            sceneKey = null;
            isInterior = false;
            try
            {
                if (World.Services == null)
                {
                    return false;
                }

                SceneService sceneService =
                    World.Services.TryGet<SceneService>();
                SceneLifetimeEvents lifetime = SceneLifetimeEvents.Get;
                if (sceneService == null
                    || sceneService.ActiveSceneRef == null
                    || string.IsNullOrEmpty(sceneService.ActiveSceneRef.Name)
                    || lifetime == null
                    || !lifetime.EverythingInitialized)
                {
                    return false;
                }

                sceneKey = sceneService.ActiveSceneRef.Name;
                isInterior = !sceneService.IsOpenWorld || lifetime.InInterior;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void QueueNativeBloomCapture(string sceneKey)
        {
            if (_interiorBloomSampleCoroutine != null
                && string.Equals(
                    _pendingInteriorBloomSceneKey,
                    sceneKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            CancelNativeBloomCapture();
            DisableInteriorBloom();
            _nativeBloomCaptured = false;
            _pendingInteriorBloomSceneKey = sceneKey;
            _interiorBloomSampleCoroutine = StartCoroutine(
                CaptureNativeBloomAfterVolumeUpdate(sceneKey));
        }

        private IEnumerator CaptureNativeBloomAfterVolumeUpdate(
            string sceneKey)
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            _interiorBloomSampleCoroutine = null;
            _pendingInteriorBloomSceneKey = null;
            if (!ShouldEnhanceInteriorBloom())
            {
                yield break;
            }

            string currentSceneKey;
            bool isInterior;
            if (!TryResolveSceneContext(
                    out currentSceneKey,
                    out isInterior)
                || !isInterior
                || !string.Equals(
                    sceneKey,
                    currentSceneKey,
                    StringComparison.Ordinal))
            {
                yield break;
            }

            VolumeManager manager = VolumeManager.instance;
            Bloom nativeBloom = manager == null || manager.stack == null
                ? null
                : manager.stack.GetComponent<Bloom>();
            if (nativeBloom == null || !nativeBloom.active)
            {
                _nativeBloomThreshold = Mathf.Clamp(
                    _interiorBloomThreshold.Value,
                    0f,
                    4f);
                _nativeBloomIntensity = 0f;
                _nativeBloomScatter = 0f;
            }
            else
            {
                _nativeBloomThreshold = nativeBloom.threshold.value;
                _nativeBloomIntensity = nativeBloom.intensity.value;
                _nativeBloomScatter = nativeBloom.scatter.value;
            }

            _nativeBloomSceneKey = sceneKey;
            _nativeBloomCaptured = true;
            ApplyInteriorBloomFloor();
        }

        private void ApplyInteriorBloomFloor()
        {
            if (!_nativeBloomCaptured || !ShouldEnhanceInteriorBloom())
            {
                DisableInteriorBloom();
                return;
            }

            float threshold = Mathf.Min(
                _nativeBloomThreshold,
                Mathf.Clamp(_interiorBloomThreshold.Value, 0f, 4f));
            float intensity = Mathf.Max(
                _nativeBloomIntensity,
                Mathf.Clamp01(_interiorBloomIntensity.Value));
            float scatter = Mathf.Max(
                _nativeBloomScatter,
                Mathf.Clamp01(_interiorBloomScatter.Value));
            bool strengthensNative = threshold
                    < _nativeBloomThreshold - InteriorBloomEpsilon
                || intensity
                    > _nativeBloomIntensity + InteriorBloomEpsilon
                || scatter
                    > _nativeBloomScatter + InteriorBloomEpsilon;
            if (!strengthensNative)
            {
                DisableInteriorBloom();
                return;
            }

            if (!EnsureInteriorBloomController())
            {
                return;
            }

            if (_interiorBloomApplied
                && Mathf.Abs(
                    _lastAppliedBloomThreshold - threshold)
                    <= InteriorBloomEpsilon
                && Mathf.Abs(
                    _lastAppliedBloomIntensity - intensity)
                    <= InteriorBloomEpsilon
                && Mathf.Abs(
                    _lastAppliedBloomScatter - scatter)
                    <= InteriorBloomEpsilon)
            {
                return;
            }

            _interiorBloomOverride.threshold.overrideState = true;
            _interiorBloomOverride.threshold.value = threshold;
            _interiorBloomOverride.intensity.overrideState = true;
            _interiorBloomOverride.intensity.value = intensity;
            _interiorBloomOverride.scatter.overrideState = true;
            _interiorBloomOverride.scatter.value = scatter;
            _interiorBloomVolume.weight = 1f;
            _interiorBloomVolume.enabled = true;
            _lastAppliedBloomThreshold = threshold;
            _lastAppliedBloomIntensity = intensity;
            _lastAppliedBloomScatter = scatter;
            _interiorBloomApplied = true;
            LogDiagnostic(
                "Applied interior bloom floor in "
                + _nativeBloomSceneKey
                + ": native threshold/intensity/scatter="
                + FormatBloomValues(
                    _nativeBloomThreshold,
                    _nativeBloomIntensity,
                    _nativeBloomScatter)
                + "; applied="
                + FormatBloomValues(threshold, intensity, scatter)
                + ".");
        }

        private bool EnsureInteriorBloomController()
        {
            if (_interiorBloomVolume != null
                && _interiorBloomProfile != null
                && _interiorBloomOverride != null)
            {
                return true;
            }
            if (_interiorBloomUnavailable)
            {
                return false;
            }

            try
            {
                _interiorBloomObject = new GameObject(
                    "Torchlight Rekindled Interior Bloom");
                _interiorBloomObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(_interiorBloomObject);
                _interiorBloomVolume =
                    _interiorBloomObject.AddComponent<Volume>();
                _interiorBloomVolume.isGlobal = true;
                _interiorBloomVolume.priority = InteriorBloomPriority;
                _interiorBloomVolume.weight = 1f;
                _interiorBloomVolume.enabled = false;

                _interiorBloomProfile =
                    ScriptableObject.CreateInstance<VolumeProfile>();
                _interiorBloomProfile.name =
                    "Torchlight Rekindled Interior Bloom Profile";
                _interiorBloomProfile.hideFlags = HideFlags.HideAndDontSave;
                _interiorBloomOverride =
                    _interiorBloomProfile.Add<Bloom>(true);
                _interiorBloomOverride.name =
                    "Torchlight Rekindled Interior Bloom Override";
                _interiorBloomOverride.active = true;
                _interiorBloomVolume.sharedProfile = _interiorBloomProfile;
                return true;
            }
            catch (Exception exception)
            {
                _interiorBloomUnavailable = true;
                LogWarning(
                    "Could not create the performance-friendly interior bloom override: "
                    + exception.Message);
                DestroyInteriorBloomController();
                return false;
            }
        }

        private void DisableInteriorBloom()
        {
            if (_interiorBloomVolume != null)
            {
                _interiorBloomVolume.enabled = false;
            }
            _interiorBloomApplied = false;
        }

        private void InvalidateNativeBloomCapture()
        {
            CancelNativeBloomCapture();
            _nativeBloomCaptured = false;
            _nativeBloomSceneKey = null;
        }

        private void CancelNativeBloomCapture()
        {
            if (_interiorBloomSampleCoroutine != null)
            {
                StopCoroutine(_interiorBloomSampleCoroutine);
            }
            _interiorBloomSampleCoroutine = null;
            _pendingInteriorBloomSceneKey = null;
        }

        private void DestroyInteriorBloomController()
        {
            CancelNativeBloomCapture();
            DisableInteriorBloom();
            if (_interiorBloomVolume != null)
            {
                _interiorBloomVolume.sharedProfile = null;
            }
            if (_interiorBloomObject != null)
            {
                Destroy(_interiorBloomObject);
            }
            if (_interiorBloomOverride != null)
            {
                Destroy(_interiorBloomOverride);
            }
            if (_interiorBloomProfile != null)
            {
                Destroy(_interiorBloomProfile);
            }
            _interiorBloomObject = null;
            _interiorBloomVolume = null;
            _interiorBloomProfile = null;
            _interiorBloomOverride = null;
        }

        private static string FormatBloomValues(
            float threshold,
            float intensity,
            float scatter)
        {
            return threshold.ToString("0.###", CultureInfo.InvariantCulture)
                + "/"
                + intensity.ToString("0.###", CultureInfo.InvariantCulture)
                + "/"
                + scatter.ToString("0.###", CultureInfo.InvariantCulture);
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
            _nextInteriorBloomCheckTime = 0f;
        }

        internal void Unregister(TorchRuntime runtime)
        {
            _activeTorches.Remove(runtime);
            _nextInteriorBloomCheckTime = 0f;
            if (_activeTorches.Count == 0
                && InteriorBloomOnlyWhileTorchEquipped)
            {
                DisableInteriorBloom();
            }
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

            CapturePreservedSettings(configPath, storedSchemaVersion);
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
                _pendingPreservedSettings.Clear();
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

        private void CapturePreservedSettings(
            string configPath,
            int storedSchemaVersion)
        {
            _pendingPreservedSettings.Clear();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            CaptureCustomizedValue(profile, "1. Core", "Enabled", false);
            CaptureCustomizedValue(
                profile,
                "2. Torch Light",
                "InteriorBrightnessPreset",
                TorchBrightnessPreset.Bright);
            CaptureCustomizedValue(
                profile,
                "2. Torch Light",
                "ExteriorBrightnessPreset",
                TorchBrightnessPreset.Vanilla);
            CaptureCustomizedValue(
                profile,
                "2. Torch Light",
                "RangeBonusMeters",
                0f);
            CaptureCustomizedValue(
                profile,
                "2. Torch Light",
                "BrightnessMultiplier",
                0f);
            CaptureCustomizedValue(
                profile,
                "2. Torch Light",
                "LightFlickerStrength",
                0f);
            CaptureCustomizedValue(
                profile,
                "2. Torch Light",
                "LightFlickerSpeed",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameBrightnessMultiplier",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameBloomMultiplier",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloStrength",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloSize",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloVerticalScale",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloVerticalOffset",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloHorizontalOffset",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloAxisPitchOffsetDegrees",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloAxisYawOffsetDegrees",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloRotationOffsetDegrees",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Flame",
                "FlameHaloBashRotationOffsetDegrees",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Interior Bloom",
                "EnhanceInteriorBloom",
                false);
            CaptureCustomizedValue(
                profile,
                "3. Interior Bloom",
                "InteriorBloomOnlyWhileTorchEquipped",
                false);
            CaptureCustomizedValue(
                profile,
                "3. Interior Bloom",
                "InteriorBloomThreshold",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Interior Bloom",
                "InteriorBloomIntensity",
                0f);
            CaptureCustomizedValue(
                profile,
                "3. Interior Bloom",
                "InteriorBloomScatter",
                0f);
            CaptureCustomizedValue(
                profile,
                "4. Audio",
                "LoopingFireAudio",
                false);
            CaptureCustomizedValue(
                profile,
                "4. Audio",
                "LoopingFireVolume",
                0f);
            CaptureCustomizedValue(
                profile,
                "Diagnostics",
                "Diagnostics",
                false);
        }

        private void CaptureCustomizedValue<T>(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            string section,
            string key,
            T ignoredTypeHint)
        {
            T value;
            if (profile.TryGetCustomizedValue(section, key, out value))
            {
                _pendingPreservedSettings[new ConfigDefinition(section, key)] =
                    value;
            }
        }

        private void RestorePreservedSettings()
        {
            if (_pendingPreservedSettings.Count == 0)
            {
                return;
            }

            int restored = 0;
            int clamped = 0;
            RestorePreservedEntry(_enabled, ref restored, ref clamped);
            RestorePreservedEntry(
                _interiorBrightnessPreset,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _exteriorBrightnessPreset,
                ref restored,
                ref clamped);
            RestorePreservedEntry(_rangeBonusMeters, ref restored, ref clamped);
            RestorePreservedEntry(
                _lightBrightnessMultiplier,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _lightFlickerStrength,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _lightFlickerSpeed,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameBrightnessMultiplier,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameBloomMultiplier,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameHaloStrength,
                ref restored,
                ref clamped);
            RestorePreservedEntry(_flameHaloSize, ref restored, ref clamped);
            RestorePreservedEntry(
                _flameHaloVerticalScale,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameHaloVerticalOffset,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameHaloHorizontalOffset,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameHaloAxisPitchOffsetDegrees,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameHaloAxisYawOffsetDegrees,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameHaloRotationOffsetDegrees,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _flameHaloLightParryRotationOffsetDegrees,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _enhanceInteriorBloom,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _interiorBloomOnlyWhileTorchEquipped,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _interiorBloomThreshold,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _interiorBloomIntensity,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _interiorBloomScatter,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _loopingFireAudio,
                ref restored,
                ref clamped);
            RestorePreservedEntry(
                _loopingFireVolume,
                ref restored,
                ref clamped);
            RestorePreservedEntry(_diagnostics, ref restored, ref clamped);
            _pendingPreservedSettings.Clear();

            Logger.LogInfo(
                "Restored "
                + restored.ToString(CultureInfo.InvariantCulture)
                + " customized setting(s) after schema reset"
                + (clamped > 0
                    ? "; clamped "
                        + clamped.ToString(CultureInfo.InvariantCulture)
                        + " to current ranges"
                    : string.Empty)
                + ".");
        }

        private void RestorePreservedEntry<T>(
            ConfigEntry<T> entry,
            ref int restored,
            ref int clamped)
        {
            object value;
            if (entry == null
                || !_pendingPreservedSettings.TryGetValue(
                    entry.Definition,
                    out value)
                || !(value is T))
            {
                return;
            }

            bool wasClamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    entry,
                    (T)value,
                    out wasClamped))
            {
                restored++;
                if (wasClamped)
                {
                    clamped++;
                }
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
        private Transform _haloShaftTransform;
        private Vector3 _haloShaftLocalAxis = Vector3.up;
        private DualHandedFSM _dualHandedFsm;
        private GameObject _audioObject;
        private ARFmodEventEmitter _audioEmitter;
        private GameObject _haloObject;
        private ParticleSystem _haloParticles;
        private ParticleSystemRenderer _haloRenderer;
        private Material _haloMaterial;
        private Texture2D _haloTexture;
        private readonly ParticleSystem.Particle[] _haloParticleBuffer =
            new ParticleSystem.Particle[1];
        private readonly HaloAnimationState _haloAnimationState =
            new HaloAnimationState();
        private Camera _haloCamera;
        private float _lastHaloRotationDegrees;
        private float _haloBaseStrength;
        private float _lastHaloMaterialStrength = float.NaN;
        private bool _hasHaloRotation;
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
            CaptureDualHandedFsm();
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
            if (_dualHandedFsm == null)
            {
                CaptureDualHandedFsm();
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

        private void LateUpdate()
        {
            if (!_initialized
                || _shuttingDown
                || _haloObject == null
                || !_haloObject.activeInHierarchy
                || _haloParticles == null
                || _audioAnchor == null)
            {
                return;
            }

            UpdateHaloAnimation();
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
            float brightnessPresetScale =
                _plugin.TorchBrightnessPresetScale;
            float effectiveLightBrightness = Mathf.Max(
                    0f,
                    1f + (lightBrightness - 1f) * 2f)
                * 5f
                * brightnessPresetScale;
            float rangeBonus = Mathf.Clamp(
                _plugin.RangeBonusMeters,
                0f,
                70f);
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
                3f) * 3f * brightnessPresetScale;
            float flameBloom = Mathf.Clamp(
                _plugin.FlameBloomMultiplier,
                0f,
                3f) * 3f * brightnessPresetScale;
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
                _plugin.LightFlickerStrength * 4f,
                0f,
                8f);
            if (strength <= 0f)
            {
                return;
            }

            if (_lastFlickerFrame != Time.frameCount)
            {
                _lastFlickerFrame = Time.frameCount;
                float speed = Mathf.Clamp(
                    _plugin.LightFlickerSpeed * 2f,
                    1f,
                    4f);
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
            CaptureHaloShaftAxis();
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
                + "; halo shaft="
                + GetTransformPath(_haloShaftTransform)
                + ".");
        }

        private void CaptureHaloShaftAxis()
        {
            _haloShaftTransform = null;
            _haloShaftLocalAxis = Vector3.up;
            if (_hand == null || _audioAnchor == null)
            {
                return;
            }

            Transform handTransform = _hand.transform;
            Transform orientationTip = _audioAnchor.parent == null
                ? _audioAnchor
                : _audioAnchor.parent;
            Transform current = orientationTip;
            while (current != null && current != handTransform)
            {
                if (current.name.IndexOf(
                        "Weapon_EquipableTorch",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _haloShaftTransform = current;
                    break;
                }
                current = current.parent;
            }

            if (_haloShaftTransform == null)
            {
                _haloShaftTransform = handTransform;
            }

            Vector3 worldAxis = _audioAnchor.position
                - handTransform.position;
            if (worldAxis.sqrMagnitude <= 0.0001f)
            {
                _haloShaftTransform = null;
                return;
            }

            _haloShaftLocalAxis = _haloShaftTransform
                .InverseTransformDirection(worldAxis.normalized)
                .normalized;
        }

        private void CaptureDualHandedFsm()
        {
            _dualHandedFsm = null;
            Hero hero = Hero.Current;
            if (hero == null)
            {
                return;
            }

            foreach (MeleeFSM melee in hero.Elements<MeleeFSM>())
            {
                DualHandedFSM dualHanded = melee as DualHandedFSM;
                if (dualHanded != null)
                {
                    _dualHandedFsm = dualHanded;
                    return;
                }
            }
        }

        private bool IsTorchLightParryActive()
        {
            if (_dualHandedFsm == null || !_dualHandedFsm.IsLayerActive)
            {
                return false;
            }

            return _dualHandedFsm.CurrentStateType
                    == HeroStateType.BlockParry
                || _dualHandedFsm.CurrentStateType
                    == HeroStateType.BlockParryWithoutShield
                || _dualHandedFsm.CurrentStateToEnterType
                    == HeroStateType.BlockParry
                || _dualHandedFsm.CurrentStateToEnterType
                    == HeroStateType.BlockParryWithoutShield;
        }

        private bool IsTorchBlockPommelActive()
        {
            if (_dualHandedFsm == null || !_dualHandedFsm.IsLayerActive)
            {
                return false;
            }

            return _dualHandedFsm.CurrentStateType
                    == HeroStateType.BlockPommel
                || _dualHandedFsm.CurrentStateType
                    == HeroStateType.BlockPommelWithoutShield
                || _dualHandedFsm.CurrentStateToEnterType
                    == HeroStateType.BlockPommel
                || _dualHandedFsm.CurrentStateToEnterType
                    == HeroStateType.BlockPommelWithoutShield;
        }

        private bool IsTorchBlockActive()
        {
            if (_dualHandedFsm == null || !_dualHandedFsm.IsLayerActive)
            {
                return false;
            }

            return IsTorchBlockState(_dualHandedFsm.CurrentStateType)
                || IsTorchBlockState(
                    _dualHandedFsm.CurrentStateToEnterType);
        }

        private static bool IsTorchBlockState(HeroStateType stateType)
        {
            switch (stateType)
            {
                case HeroStateType.BlockStart:
                case HeroStateType.BlockLoop:
                case HeroStateType.BlockPommel:
                case HeroStateType.BlockImpact:
                case HeroStateType.BlockExit:
                case HeroStateType.BlockStartWithoutShield:
                case HeroStateType.BlockLoopWithoutShield:
                case HeroStateType.BlockPommelWithoutShield:
                case HeroStateType.BlockImpactWithoutShield:
                case HeroStateType.BlockExitWithoutShield:
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyHalo()
        {
            float strength = Mathf.Clamp(
                _plugin.FlameHaloStrength
                    * _plugin.TorchBrightnessPresetScale,
                0f,
                10f);
            float size = Mathf.Clamp(
                _plugin.FlameHaloSize,
                0.02f,
                0.25f);
            float verticalScale = Mathf.Clamp(
                _plugin.FlameHaloVerticalScale,
                0.25f,
                4f);
            float verticalOffset = Mathf.Clamp(
                _plugin.FlameHaloVerticalOffset,
                -1f,
                1f);
            float horizontalOffset = Mathf.Clamp(
                _plugin.FlameHaloHorizontalOffset,
                -1f,
                1f);
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
                _haloRenderer.allowRoll = true;
                _haloRenderer.sortMode =
                    ParticleSystemSortMode.Distance;

                ParticleSystem.MainModule main = _haloParticles.main;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = 999999f;
                main.startSpeed = 0f;
                main.startSize3D = true;
                main.startSizeX = 1f;
                main.startSizeY = verticalScale;
                main.startSizeZ = 1f;
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

            _haloObject.transform.localPosition = Vector3.up
                    * (size * verticalScale * verticalOffset)
                + Vector3.right * (size * horizontalOffset);
            _haloObject.transform.localRotation = Quaternion.identity;
            _haloObject.transform.localScale = Vector3.one * size;
            UpdateHaloParticleShape(verticalScale);
            _haloBaseStrength = strength;
            UpdateHaloAnimation();
        }

        private void UpdateHaloParticleShape(float verticalScale)
        {
            int particleCount = _haloParticles.GetParticles(
                _haloParticleBuffer);
            if (particleCount == 0)
            {
                return;
            }

            ParticleSystem.Particle particle = _haloParticleBuffer[0];
            particle.startSize3D = new Vector3(
                1f,
                verticalScale,
                1f);
            _haloParticleBuffer[0] = particle;
            _haloParticles.SetParticles(_haloParticleBuffer, 1);
        }

        private void UpdateHaloAnimation()
        {
            bool lightParryActive = IsTorchLightParryActive();
            bool blockActive = !lightParryActive && IsTorchBlockActive();
            HaloAnimationFrame animationFrame = _haloAnimationState.Update(
                Time.time,
                lightParryActive,
                blockActive,
                blockActive && IsTorchBlockPommelActive());
            ApplyHaloMaterial(
                _haloBaseStrength
                * animationFrame.BlockVisibilityMultiplier);

            float haloScale = Mathf.Clamp(
                _plugin.FlameHaloSize,
                0.02f,
                0.25f) * animationFrame.LightParryScaleMultiplier;
            Vector3 targetScale = Vector3.one * haloScale;
            if ((_haloObject.transform.localScale - targetScale)
                    .sqrMagnitude > 0.000001f)
            {
                _haloObject.transform.localScale = targetScale;
            }

            Camera camera = _haloCamera;
            if (camera == null || !camera.isActiveAndEnabled)
            {
                camera = Camera.main;
                _haloCamera = camera;
            }
            if (camera == null)
            {
                return;
            }

            UpdateHaloRotation(lightParryActive, camera);
        }

        private Vector3 ResolveTorchAxis()
        {
            Transform axisTransform = _haloShaftTransform == null
                ? _audioAnchor
                : _haloShaftTransform;
            Vector3 localTorchAxis = _haloShaftTransform == null
                ? axisTransform.InverseTransformDirection(
                    _audioAnchor.position - _hand.transform.position)
                : _haloShaftLocalAxis;
            Quaternion localAxisCorrection = Quaternion.Euler(
                Mathf.Clamp(
                    _plugin.FlameHaloAxisPitchOffsetDegrees,
                    -45f,
                    45f),
                Mathf.Clamp(
                    _plugin.FlameHaloAxisYawOffsetDegrees,
                    -45f,
                    45f),
                0f);
            Vector3 torchAxis = axisTransform.TransformDirection(
                localAxisCorrection * localTorchAxis);
            if (torchAxis.sqrMagnitude <= 0.0001f)
            {
                return _audioAnchor.up;
            }

            torchAxis.Normalize();
            return torchAxis;
        }

        private void UpdateHaloRotation(
            bool lightParryActive,
            Camera camera)
        {
            Vector3 torchAxis = ResolveTorchAxis();

            Vector3 viewAxis = camera.transform.InverseTransformDirection(
                torchAxis);
            float projectedSqrMagnitude = viewAxis.x * viewAxis.x
                + viewAxis.y * viewAxis.y;
            if (projectedSqrMagnitude <= 0.0001f)
            {
                return;
            }

            float rotationDegrees = -Mathf.Atan2(
                viewAxis.x,
                viewAxis.y) * Mathf.Rad2Deg
                + Mathf.Clamp(
                    _plugin.FlameHaloRotationOffsetDegrees,
                    -180f,
                    180f)
                + (lightParryActive
                    ? Mathf.Clamp(
                        _plugin.FlameHaloLightParryRotationOffsetDegrees,
                        -180f,
                        180f)
                    : 0f);
            if (_hasHaloRotation
                && Mathf.Abs(Mathf.DeltaAngle(
                    _lastHaloRotationDegrees,
                    rotationDegrees)) < 0.1f)
            {
                return;
            }

            int particleCount = _haloParticles.GetParticles(
                _haloParticleBuffer);
            if (particleCount == 0)
            {
                return;
            }

            ParticleSystem.Particle particle = _haloParticleBuffer[0];
            particle.rotation = rotationDegrees;
            _haloParticleBuffer[0] = particle;
            _haloParticles.SetParticles(_haloParticleBuffer, 1);
            _lastHaloRotationDegrees = rotationDegrees;
            _hasHaloRotation = true;
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
            HDMaterial.ValidateMaterial(material);
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

            strength = Mathf.Max(0f, strength);
            if (!float.IsNaN(_lastHaloMaterialStrength)
                && Mathf.Abs(_lastHaloMaterialStrength - strength) < 0.001f)
            {
                return;
            }
            _lastHaloMaterialStrength = strength;

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
            _haloBaseStrength = 0f;
            _lastHaloMaterialStrength = float.NaN;
            _hasHaloRotation = false;
            _haloAnimationState.Reset();
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
                    _plugin.LoopingFireVolume * 4f,
                    0f,
                    8f));
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
