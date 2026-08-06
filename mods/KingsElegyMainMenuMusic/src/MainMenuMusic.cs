using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Main Menu Music")]
[assembly: AssemblyDescription("Controls Tainted Grail: The Fall of Avalon's title music with layered or custom FMOD playback")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Main Menu Music")]
[assembly: AssemblyVersion("2.2.1.0")]
[assembly: AssemblyFileVersion("2.2.1.0")]
[assembly: AssemblyInformationalVersion("2.2.1")]

namespace MainMenuMusic
{
    public enum MusicMode
    {
        LayeredModifiedTaintedGrail,
        CustomFile,
        Off
    }

    internal enum LayerRole
    {
        Base,
        Fire,
        Wind,
        Custom
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.main-menu-music";
        public const string PluginName = "Main Menu Music";
        public const string PluginVersion = "2.2.1";

        private const int ConfigSchemaVersion = 16;
        private const int ConfigRecoveryBaselineSchema = 16;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const float VolumeOutputScale = 0.2f;
        private const uint MinimumLoopLengthMs = 250;

        private const string TitleMusicTypeName =
            "Awaken.TG.Main.UI.TitleScreen.VTitleScreenMusic";
        private const string MusicEmitterFieldName = "musicEmitter";
        private const string NonCopyrightedEmitterFieldName =
            "nonCopyrightedEmitter";

        private static readonly string[] TitleMusicCloseMethodNames =
        {
            "OnDiscard",
            "OnDisable",
            "OnDestroy"
        };

        private static readonly string[] GameLoadingTypeNames =
        {
            "Awaken.TG.Main.UI.TitleScreen.Loading.LoadingTypes.NewGameLoading",
            "Awaken.TG.Main.UI.TitleScreen.Loading.LoadingTypes.NewGamePlusLoading",
            "Awaken.TG.Main.UI.TitleScreen.Loading.LoadingTypes.FullLoading",
            "Awaken.TG.Main.UI.TitleScreen.Loading.LoadingTypes.MapChangeLoading"
        };

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private readonly Dictionary<IntPtr, EventInstance> _mutedOriginals =
            new Dictionary<IntPtr, EventInstance>();
        private readonly HashSet<string> _loggedMissingPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MusicLayer> _layers = new List<MusicLayer>();
        private readonly List<MusicLayer> _fadeOutLayers = new List<MusicLayer>();

        private Harmony _harmony;
        private FieldInfo _musicEmitterField;
        private FieldInfo _nonCopyrightedEmitterField;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<MusicMode> _musicMode;
        private ConfigEntry<string> _baseMusicFile;
        private ConfigEntry<float> _baseMusicVolume;
        private ConfigEntry<bool> _enableFireAmbience;
        private ConfigEntry<string> _fireAmbienceFile;
        private ConfigEntry<float> _fireAmbienceVolume;
        private ConfigEntry<bool> _enableWindAmbience;
        private ConfigEntry<string> _windAmbienceFile;
        private ConfigEntry<float> _windAmbienceVolume;
        private ConfigEntry<string> _customMusicFile;
        private ConfigEntry<float> _customMusicVolume;
        private ConfigEntry<bool> _loop;
        private ConfigEntry<float> _loopStartSeconds;
        private ConfigEntry<float> _loopEndTrimSeconds;
        private ConfigEntry<float> _crossfadeSeconds;
        private ConfigEntry<bool> _applyEffectsToBaseMusic;
        private ConfigEntry<float> _semitones;
        private ConfigEntry<int> _fftSize;
        private ConfigEntry<int> _overlap;
        private ConfigEntry<bool> _enableHighFrequencyRestore;
        private ConfigEntry<float> _highFrequencyGainDb;
        private ConfigEntry<float> _highFrequencyCrossoverHz;
        private ConfigEntry<bool> _demonicMode;
        private ConfigEntry<bool> _enableDistortion;
        private ConfigEntry<float> _distortionLevel;
        private ConfigEntry<bool> _enableLowpass;
        private ConfigEntry<float> _lowpassCutoffHz;
        private ConfigEntry<bool> _enableEcho;
        private ConfigEntry<float> _echoDelayMs;
        private ConfigEntry<float> _echoFeedbackPercent;
        private ConfigEntry<float> _echoWetLevelDb;
        private ConfigEntry<bool> _fadeOutOnGameLoad;
        private ConfigEntry<float> _gameLoadFadeSeconds;
        private ConfigEntry<bool> _muteOriginalTitleMusic;
        private ConfigEntry<bool> _restartWhenTitleMusicPlays;
        private ConfigEntry<bool> _verboseLogging;

        private Coroutine _retryCoroutine;
        private string _loadedSignature = string.Empty;
        private uint _musicLengthMs;
        private uint _loopStartMs;
        private uint _loopEndMs;
        private float _loopFadeStartedAt = -1.0f;
        private float _loopFadeDurationSeconds;
        private bool _exitFadeActive;
        private float _exitFadeStartedAt = -1.0f;
        private float _exitFadeDurationSeconds;
        private string _exitFadeReason = string.Empty;
        private bool _titlePlaybackAllowed;
        private object _activeTitleMusicView;

        private float PitchRatio
        {
            get
            {
                double ratio = Math.Pow(2.0, _semitones.Value / 12.0);
                return Mathf.Clamp((float)ratio, 0.2f, 2.0f);
            }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
                if (!PatchTitleMusic())
                {
                    enabled = false;
                    return;
                }

                PatchGameLoading();

                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded; music mode is "
                    + _musicMode.Value
                    + ".");
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
            _enabled = Config.Bind(
                "1. Playback",
                "Enabled",
                true,
                "Master switch.");
            Config.Bind(
                "9. Internal",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _musicMode = Config.Bind(
                "1. Playback",
                "MusicMode",
                MusicMode.LayeredModifiedTaintedGrail,
                "LayeredModifiedTaintedGrail uses the included title music, fire, and wind layers. CustomFile plays only CustomMusicFile with normal loop controls but no DSP or ambience layers. Off disables replacement music.");

            _semitones = Config.Bind(
                "2. Base Music",
                "Semitones",
                -7.0f,
                new ConfigDescription(
                    "Pitch offset in semitones for the base title music layer.",
                    new AcceptableValueRange<float>(-24.0f, 0.0f)));
            _baseMusicVolume = Config.Bind(
                "2. Base Music",
                "BaseMusicVolume",
                1.0f,
                new ConfigDescription(
                    "Base title music relative volume. 1.0 uses the calibrated music playback level.",
                    new AcceptableValueRange<float>(0.0f, 5.0f)));
            _applyEffectsToBaseMusic = Config.Bind(
                "2. Base Music",
                "ApplyEffectsToBaseMusic",
                true,
                "Apply pitch, EQ, distortion, lowpass, and echo to the base Tainted Grail title music layer. CustomFile mode is never affected by these settings.");
            _baseMusicFile = Config.Bind(
                "2. Base Music",
                "BaseMusicFile",
                "menu_layer_01.ksaudio",
                "Base title music layer file for LayeredModifiedTaintedGrail mode. The packaged file contains WAV data. Relative paths are resolved from the plugin folder first, then the audio folder.");
            _fftSize = Config.Bind(
                "3. Base Music Advanced DSP",
                "FFTSize",
                4096,
                new ConfigDescription(
                    "FMOD pitch-shift FFT size.",
                    new AcceptableValueRange<int>(256, 4096)));
            _overlap = Config.Bind(
                "3. Base Music Advanced DSP",
                "Overlap",
                32,
                new ConfigDescription(
                    "FMOD pitch-shift overlap.",
                    new AcceptableValueRange<int>(1, 32)));
            _enableHighFrequencyRestore = Config.Bind(
                "3. Base Music Advanced DSP",
                "EnableHighFrequencyRestore",
                true,
                "Adds a light high-band EQ after pitch shifting so the treated base music keeps some brightness.");
            _highFrequencyGainDb = Config.Bind(
                "3. Base Music Advanced DSP",
                "HighFrequencyGainDb",
                1.5f,
                new ConfigDescription(
                    "High-band gain in dB for the restore EQ.",
                    new AcceptableValueRange<float>(0.0f, 6.0f)));
            _highFrequencyCrossoverHz = Config.Bind(
                "3. Base Music Advanced DSP",
                "HighFrequencyCrossoverHz",
                5000.0f,
                new ConfigDescription(
                    "Frequency where the restore EQ high band begins.",
                    new AcceptableValueRange<float>(1000.0f, 12000.0f)));
            _demonicMode = Config.Bind(
                "3. Base Music Advanced DSP",
                "DemonicMode",
                true,
                "Adds a subtle distortion, lowpass, and short echo chain after the pitch shift.");
            _enableDistortion = Config.Bind(
                "3. Base Music Advanced DSP",
                "EnableDistortion",
                true,
                "Adds a small amount of FMOD distortion.");
            _distortionLevel = Config.Bind(
                "3. Base Music Advanced DSP",
                "DistortionLevel",
                0.1f,
                new ConfigDescription(
                    "FMOD distortion level.",
                    new AcceptableValueRange<float>(0.0f, 0.5f)));
            _enableLowpass = Config.Bind(
                "3. Base Music Advanced DSP",
                "EnableLowpass",
                true,
                "Darkens the pitched audio by reducing harsh high frequencies.");
            _lowpassCutoffHz = Config.Bind(
                "3. Base Music Advanced DSP",
                "LowpassCutoffHz",
                5500.0f,
                new ConfigDescription(
                    "Lowpass cutoff in Hz.",
                    new AcceptableValueRange<float>(1000.0f, 22000.0f)));
            _enableEcho = Config.Bind(
                "3. Base Music Advanced DSP",
                "EnableEcho",
                true,
                "Adds a quiet short echo for a supernatural tail.");
            _echoDelayMs = Config.Bind(
                "3. Base Music Advanced DSP",
                "EchoDelayMs",
                100.0f,
                new ConfigDescription(
                    "Echo delay in milliseconds.",
                    new AcceptableValueRange<float>(10.0f, 250.0f)));
            _echoFeedbackPercent = Config.Bind(
                "3. Base Music Advanced DSP",
                "EchoFeedbackPercent",
                10.0f,
                new ConfigDescription(
                    "Echo feedback percent.",
                    new AcceptableValueRange<float>(0.0f, 50.0f)));
            _echoWetLevelDb = Config.Bind(
                "3. Base Music Advanced DSP",
                "EchoWetLevelDb",
                -36.0f,
                new ConfigDescription(
                    "Echo wet level in decibels. More negative is subtler.",
                    new AcceptableValueRange<float>(-80.0f, 0.0f)));

            _enableFireAmbience = Config.Bind(
                "4. Ambience Layers",
                "EnableFireAmbience",
                true,
                "Play the included fire ambience layer in LayeredModifiedTaintedGrail mode.");
            _fireAmbienceVolume = Config.Bind(
                "4. Ambience Layers",
                "FireAmbienceVolume",
                1.0f,
                new ConfigDescription(
                    "Fire ambience relative volume. 1.0 uses the calibrated music playback level.",
                    new AcceptableValueRange<float>(0.0f, 5.0f)));
            _fireAmbienceFile = Config.Bind(
                "4. Ambience Layers",
                "FireAmbienceFile",
                "menu_layer_02.ksaudio",
                "Fire ambience layer file for LayeredModifiedTaintedGrail mode. The packaged file contains WAV data.");
            _enableWindAmbience = Config.Bind(
                "4. Ambience Layers",
                "EnableWindAmbience",
                true,
                "Play the included wind ambience layer in LayeredModifiedTaintedGrail mode.");
            _windAmbienceVolume = Config.Bind(
                "4. Ambience Layers",
                "WindAmbienceVolume",
                1.0f,
                new ConfigDescription(
                    "Wind ambience relative volume. 1.0 uses the calibrated music playback level.",
                    new AcceptableValueRange<float>(0.0f, 5.0f)));
            _windAmbienceFile = Config.Bind(
                "4. Ambience Layers",
                "WindAmbienceFile",
                "menu_layer_03.ksaudio",
                "Wind ambience layer file for LayeredModifiedTaintedGrail mode. The packaged file contains WAV data.");

            _customMusicFile = Config.Bind(
                "5. Custom File",
                "CustomMusicFile",
                "main_menu_music.wav",
                "WAV to play when MusicMode is CustomFile. Custom playback is affected by Looping settings but not by layered ambience or DSP settings.");
            _customMusicVolume = Config.Bind(
                "5. Custom File",
                "CustomMusicVolume",
                1.0f,
                new ConfigDescription(
                    "Custom file relative volume. 1.0 uses the calibrated music playback level.",
                    new AcceptableValueRange<float>(0.0f, 5.0f)));

            _loop = Config.Bind(
                "6. Looping",
                "Loop",
                true,
                "Loop the active title music while the title menu is open.");
            _loopStartSeconds = Config.Bind(
                "6. Looping",
                "LoopStartSeconds",
                0.0f,
                new ConfigDescription(
                    "Optional loop start point in seconds. The first play starts at the beginning; repeated loops start here.",
                    new AcceptableValueRange<float>(0.0f, 6000.0f)));
            _loopEndTrimSeconds = Config.Bind(
                "6. Looping",
                "LoopEndTrimSeconds",
                0.0f,
                new ConfigDescription(
                    "Seconds to trim from the end before looping. Useful for removing silence, tails, or export padding.",
                    new AcceptableValueRange<float>(0.0f, 600.0f)));
            _crossfadeSeconds = Config.Bind(
                "6. Looping",
                "CrossfadeSeconds",
                3.0f,
                new ConfigDescription(
                    "Optional loop crossfade duration in seconds. 0 uses FMOD loop points without crossfade.",
                    new AcceptableValueRange<float>(0.0f, 30.0f)));

            _fadeOutOnGameLoad = Config.Bind(
                "7. Loading And Compatibility",
                "FadeOutOnGameLoad",
                true,
                "Fade out replacement title music when a real game load begins.");
            _gameLoadFadeSeconds = Config.Bind(
                "7. Loading And Compatibility",
                "GameLoadFadeSeconds",
                10.0f,
                new ConfigDescription(
                    "Replacement title music fade-out duration when gameplay/loading starts.",
                    new AcceptableValueRange<float>(0.0f, 10.0f)));
            _muteOriginalTitleMusic = Config.Bind(
                "7. Loading And Compatibility",
                "MuteOriginalTitleMusic",
                true,
                "Set the game's original title music emitters to volume 0 while replacement music is active.");
            _restartWhenTitleMusicPlays = Config.Bind(
                "7. Loading And Compatibility",
                "RestartWhenTitleMusicPlays",
                false,
                "Restart replacement music each time the game's title music PlayMusic method runs.");
            _verboseLogging = Config.Bind(
                "8. Diagnostics",
                "VerboseLogging",
                false,
                "Log title music routing, layer playback, DSP, and transition details.");

            RegisterConfigHandlers();

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

        private void RegisterConfigHandlers()
        {
            _enabled.SettingChanged += OnEnabledSettingChanged;
            _musicMode.SettingChanged += OnMusicModeSettingChanged;
            _baseMusicFile.SettingChanged += OnLayeredMusicSettingChanged;
            _enableFireAmbience.SettingChanged += OnLayeredMusicSettingChanged;
            _fireAmbienceFile.SettingChanged += OnLayeredMusicSettingChanged;
            _enableWindAmbience.SettingChanged += OnLayeredMusicSettingChanged;
            _windAmbienceFile.SettingChanged += OnLayeredMusicSettingChanged;
            _customMusicFile.SettingChanged += OnCustomMusicFileSettingChanged;
            _loop.SettingChanged += OnCustomLoopSettingChanged;
            _loopStartSeconds.SettingChanged += OnCustomLoopSettingChanged;
            _loopEndTrimSeconds.SettingChanged += OnCustomLoopSettingChanged;
            _crossfadeSeconds.SettingChanged += OnCustomLoopSettingChanged;
            _applyEffectsToBaseMusic.SettingChanged += OnEffectSettingChanged;
            _semitones.SettingChanged += OnEffectSettingChanged;
            _fftSize.SettingChanged += OnEffectSettingChanged;
            _overlap.SettingChanged += OnEffectSettingChanged;
            _enableHighFrequencyRestore.SettingChanged += OnEffectSettingChanged;
            _highFrequencyGainDb.SettingChanged += OnEffectSettingChanged;
            _highFrequencyCrossoverHz.SettingChanged += OnEffectSettingChanged;
            _demonicMode.SettingChanged += OnEffectSettingChanged;
            _enableDistortion.SettingChanged += OnEffectSettingChanged;
            _distortionLevel.SettingChanged += OnEffectSettingChanged;
            _enableLowpass.SettingChanged += OnEffectSettingChanged;
            _lowpassCutoffHz.SettingChanged += OnEffectSettingChanged;
            _enableEcho.SettingChanged += OnEffectSettingChanged;
            _echoDelayMs.SettingChanged += OnEffectSettingChanged;
            _echoFeedbackPercent.SettingChanged += OnEffectSettingChanged;
            _echoWetLevelDb.SettingChanged += OnEffectSettingChanged;
            _baseMusicVolume.SettingChanged += OnVolumeSettingChanged;
            _fireAmbienceVolume.SettingChanged += OnVolumeSettingChanged;
            _windAmbienceVolume.SettingChanged += OnVolumeSettingChanged;
            _customMusicVolume.SettingChanged += OnVolumeSettingChanged;
            _muteOriginalTitleMusic.SettingChanged += OnMuteOriginalTitleMusicSettingChanged;
        }

        private void UnregisterConfigHandlers()
        {
            Unsubscribe(_enabled, OnEnabledSettingChanged);
            Unsubscribe(_musicMode, OnMusicModeSettingChanged);
            Unsubscribe(_baseMusicFile, OnLayeredMusicSettingChanged);
            Unsubscribe(_enableFireAmbience, OnLayeredMusicSettingChanged);
            Unsubscribe(_fireAmbienceFile, OnLayeredMusicSettingChanged);
            Unsubscribe(_enableWindAmbience, OnLayeredMusicSettingChanged);
            Unsubscribe(_windAmbienceFile, OnLayeredMusicSettingChanged);
            Unsubscribe(_customMusicFile, OnCustomMusicFileSettingChanged);
            Unsubscribe(_loop, OnCustomLoopSettingChanged);
            Unsubscribe(_loopStartSeconds, OnCustomLoopSettingChanged);
            Unsubscribe(_loopEndTrimSeconds, OnCustomLoopSettingChanged);
            Unsubscribe(_crossfadeSeconds, OnCustomLoopSettingChanged);
            Unsubscribe(_applyEffectsToBaseMusic, OnEffectSettingChanged);
            Unsubscribe(_semitones, OnEffectSettingChanged);
            Unsubscribe(_fftSize, OnEffectSettingChanged);
            Unsubscribe(_overlap, OnEffectSettingChanged);
            Unsubscribe(_enableHighFrequencyRestore, OnEffectSettingChanged);
            Unsubscribe(_highFrequencyGainDb, OnEffectSettingChanged);
            Unsubscribe(_highFrequencyCrossoverHz, OnEffectSettingChanged);
            Unsubscribe(_demonicMode, OnEffectSettingChanged);
            Unsubscribe(_enableDistortion, OnEffectSettingChanged);
            Unsubscribe(_distortionLevel, OnEffectSettingChanged);
            Unsubscribe(_enableLowpass, OnEffectSettingChanged);
            Unsubscribe(_lowpassCutoffHz, OnEffectSettingChanged);
            Unsubscribe(_enableEcho, OnEffectSettingChanged);
            Unsubscribe(_echoDelayMs, OnEffectSettingChanged);
            Unsubscribe(_echoFeedbackPercent, OnEffectSettingChanged);
            Unsubscribe(_echoWetLevelDb, OnEffectSettingChanged);
            Unsubscribe(_baseMusicVolume, OnVolumeSettingChanged);
            Unsubscribe(_fireAmbienceVolume, OnVolumeSettingChanged);
            Unsubscribe(_windAmbienceVolume, OnVolumeSettingChanged);
            Unsubscribe(_customMusicVolume, OnVolumeSettingChanged);
            Unsubscribe(_muteOriginalTitleMusic, OnMuteOriginalTitleMusicSettingChanged);
        }

        private static void Unsubscribe<T>(
            ConfigEntry<T> entry,
            System.EventHandler handler)
        {
            if (entry != null)
            {
                entry.SettingChanged -= handler;
            }
        }

        private void OnEnabledSettingChanged(object sender, EventArgs args)
        {
            RefreshPlaybackRoute("enabled changed", true);
        }

        private void OnMusicModeSettingChanged(object sender, EventArgs args)
        {
            RefreshPlaybackRoute("music mode changed", true);
        }

        private void OnLayeredMusicSettingChanged(object sender, EventArgs args)
        {
            RestartLayeredMusicIfActive();
        }

        private void OnCustomMusicFileSettingChanged(object sender, EventArgs args)
        {
            RestartCustomMusicIfActive();
        }

        private void OnCustomLoopSettingChanged(object sender, EventArgs args)
        {
            RestartCustomMusic();
        }

        private void OnEffectSettingChanged(object sender, EventArgs args)
        {
            RefreshLiveEffectSettings();
        }

        private void OnVolumeSettingChanged(object sender, EventArgs args)
        {
            RefreshLayerVolumes();
        }

        private void OnMuteOriginalTitleMusicSettingChanged(
            object sender,
            EventArgs args)
        {
            RefreshPlaybackRoute("original title music mute changed", false);
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            foreach (string rawLine in File.ReadAllLines(configPath))
            {
                string line = rawLine.Trim();
                const string schemaPrefix = "ConfigSchemaVersion =";
                if (line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    int parsed;
                    if (int.TryParse(
                            line.Substring(schemaPrefix.Length).Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out parsed))
                    {
                        storedSchemaVersion = parsed;
                    }

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
                File.WriteAllText(configPath, string.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo(
                    "Reset main-menu music config schema from "
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
                        "Could not restore the previous main-menu music config after a failed schema reset: "
                        + restoreException.Message);
                }

                Logger.LogWarning(
                    "Could not reset the outdated main-menu music config. The previous config was retained when possible: "
                    + exception.Message);
            }
        }

        private bool PatchTitleMusic()
        {
            Type titleMusicType = AccessTools.TypeByName(TitleMusicTypeName);
            if (titleMusicType == null)
            {
                Logger.LogWarning(
                    "Could not find "
                    + TitleMusicTypeName
                    + "; main menu music will not be changed.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Main menu music inactive; check BepInEx log.");
                return false;
            }

            _musicEmitterField = AccessTools.Field(
                titleMusicType,
                MusicEmitterFieldName);
            _nonCopyrightedEmitterField = AccessTools.Field(
                titleMusicType,
                NonCopyrightedEmitterFieldName);

            MethodInfo playMusicMethod = AccessTools.Method(
                titleMusicType,
                "PlayMusic");
            if (_musicEmitterField == null
                || _nonCopyrightedEmitterField == null
                || playMusicMethod == null)
            {
                Logger.LogWarning(
                    "Could not resolve title-screen music fields or PlayMusic method; main menu music will not be changed.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Main menu music inactive; check BepInEx log.");
                return false;
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                playMusicMethod,
                postfix: new HarmonyMethod(
                    typeof(Patches),
                    nameof(Patches.AfterPlayMusic)));

            PatchTitleCloseMethod(titleMusicType);
            return true;
        }

        private void PatchTitleCloseMethod(Type titleMusicType)
        {
            BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            for (int i = 0; i < TitleMusicCloseMethodNames.Length; i++)
            {
                MethodInfo method = titleMusicType.GetMethod(
                    TitleMusicCloseMethodNames[i],
                    flags);
                if (method == null)
                {
                    continue;
                }

                _harmony.Patch(
                    method,
                    postfix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.AfterTitleMusicClosed)));
                LogDiagnostic("Patched title music close method " + method.Name + ".");
                return;
            }

            LogDiagnostic("No title music close method was found.");
        }

        private void PatchGameLoading()
        {
            if (_harmony == null)
            {
                return;
            }

            foreach (string typeName in GameLoadingTypeNames)
            {
                Type loadingType = AccessTools.TypeByName(typeName);
                if (loadingType == null)
                {
                    LogDiagnostic("Game loading type not found: " + typeName + ".");
                    continue;
                }

                MethodInfo method = AccessTools.Method(
                    loadingType,
                    "BeforeDroppingPreviousDomains");
                if (method == null)
                {
                    LogDiagnostic(
                        "BeforeDroppingPreviousDomains was not found on "
                        + typeName
                        + ".");
                    continue;
                }

                _harmony.Patch(
                    method,
                    prefix: new HarmonyMethod(
                        typeof(Patches),
                        nameof(Patches.BeforeGameLoading)));
                LogDiagnostic("Patched game loading transition: " + typeName + ".");
            }
        }

        internal void ApplyToTitleMusic(object titleMusicView)
        {
            if (titleMusicView == null)
            {
                return;
            }

            _activeTitleMusicView = titleMusicView;
            _titlePlaybackAllowed = true;
            _exitFadeActive = false;

            if (!_enabled.Value || _musicMode.Value == MusicMode.Off)
            {
                StopCustomMusic("disabled or off");
                UnmuteOriginals();
                return;
            }

            bool allReady = ApplyOriginalTitleMusicMute();
            EnsureCustomMusicPlaying(_restartWhenTitleMusicPlays.Value);
            RetryApplyIfNeeded(allReady);
        }

        private IEnumerator RetryApply(object titleMusicView)
        {
            float[] delays = { 0.1f, 0.25f, 0.5f, 1.0f, 2.0f, 4.0f };
            for (int i = 0; i < delays.Length; i++)
            {
                yield return new WaitForSecondsRealtime(delays[i]);
                if (titleMusicView == null
                    || !_enabled.Value
                    || _musicMode.Value == MusicMode.Off
                    || !_titlePlaybackAllowed)
                {
                    break;
                }

                _activeTitleMusicView = titleMusicView;
                bool allReady = ApplyOriginalTitleMusicMute();
                EnsureCustomMusicPlaying(false);

                if (allReady)
                {
                    break;
                }
            }

            _retryCoroutine = null;
        }

        private void RefreshPlaybackRoute(string reason, bool restart)
        {
            if (!_enabled.Value || _musicMode.Value == MusicMode.Off)
            {
                StopCustomMusic(reason);
                UnmuteOriginals();
                return;
            }

            bool allReady = ApplyOriginalTitleMusicMute();
            EnsureCustomMusicPlaying(restart);
            RetryApplyIfNeeded(allReady);
        }

        private bool ApplyOriginalTitleMusicMute()
        {
            if (!_muteOriginalTitleMusic.Value)
            {
                UnmuteOriginals();
                return true;
            }

            if (!_titlePlaybackAllowed || _activeTitleMusicView == null)
            {
                return true;
            }

            bool allReady = true;
            allReady &= MuteEmitter(
                _activeTitleMusicView,
                _musicEmitterField,
                "copyrighted title music");
            allReady &= MuteEmitter(
                _activeTitleMusicView,
                _nonCopyrightedEmitterField,
                "non-copyrighted title music");

            return allReady;
        }

        private void RetryApplyIfNeeded(bool allReady)
        {
            if (!allReady && _retryCoroutine == null && _activeTitleMusicView != null)
            {
                _retryCoroutine = StartCoroutine(RetryApply(_activeTitleMusicView));
            }
        }

        private bool MuteEmitter(
            object titleMusicView,
            FieldInfo emitterField,
            string label)
        {
            if (emitterField == null)
            {
                return true;
            }

            StudioEventEmitter emitter =
                emitterField.GetValue(titleMusicView) as StudioEventEmitter;
            if (emitter == null)
            {
                LogDiagnostic("No " + label + " emitter yet.");
                return true;
            }

            EventInstance instance = emitter.EventInstance;
            if (!instance.isValid())
            {
                LogDiagnostic(label + " emitter has no valid FMOD instance yet.");
                return false;
            }

            IntPtr handle = instance.handle;
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            RESULT result = instance.setVolume(0.0f);
            if (result != RESULT.OK)
            {
                Logger.LogWarning(
                    "Failed to mute "
                    + label
                    + ": "
                    + result
                    + ".");
                return false;
            }

            _mutedOriginals[handle] = instance;
            LogDiagnostic("Muted " + label + ".");
            return true;
        }

        private void Update()
        {
            if (_exitFadeActive)
            {
                UpdateExitFade();
                return;
            }

            if (_enabled == null
                || !_enabled.Value
                || _musicMode.Value == MusicMode.Off
                || !_titlePlaybackAllowed
                || string.IsNullOrEmpty(_loadedSignature))
            {
                return;
            }

            UpdateManualCrossfade();

            if (ShouldLoopCurrentMode()
                && !IsLayerSetPlaying(_layers)
                && _fadeOutLayers.Count == 0)
            {
                EnsureCustomMusicPlaying(false);
            }
        }

        private void EnsureCustomMusicPlaying(bool restart)
        {
            if (!_enabled.Value
                || _musicMode.Value == MusicMode.Off
                || !_titlePlaybackAllowed)
            {
                return;
            }

            string signature = BuildPlaybackSignature();
            if (string.IsNullOrEmpty(signature))
            {
                return;
            }

            if (!restart
                && IsLayerSetPlaying(_layers)
                && string.Equals(
                    _loadedSignature,
                    signature,
                    StringComparison.OrdinalIgnoreCase))
            {
                RefreshLayerVolumes();
                return;
            }

            StopCustomMusic("restart");
            _loggedMissingPaths.Clear();

            List<MusicLayer> nextLayers;
            if (!TryStartLayerSet(false, 1.0f, out nextLayers))
            {
                return;
            }

            ReplaceLayerList(_layers, nextLayers);
            _loadedSignature = signature;
            RefreshLayerVolumes();
            Logger.LogInfo(
                "Playing main menu music mode "
                + _musicMode.Value
                + " with "
                + _layers.Count.ToString(CultureInfo.InvariantCulture)
                + " layer(s), length="
                + (_musicLengthMs / 1000.0f).ToString("0.###", CultureInfo.InvariantCulture)
                + "s loopStart="
                + (_loopStartMs / 1000.0f).ToString("0.###", CultureInfo.InvariantCulture)
                + "s loopEnd="
                + (_loopEndMs / 1000.0f).ToString("0.###", CultureInfo.InvariantCulture)
                + "s crossfade="
                + _crossfadeSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)
                + "s.");
        }

        private string BuildPlaybackSignature()
        {
            List<LayerSpec> specs = BuildLayerSpecs(false);
            if (specs.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            parts.Add(_musicMode.Value.ToString());
            parts.Add(_loop.Value.ToString(CultureInfo.InvariantCulture));
            parts.Add(_loopStartSeconds.Value.ToString(CultureInfo.InvariantCulture));
            parts.Add(_loopEndTrimSeconds.Value.ToString(CultureInfo.InvariantCulture));
            parts.Add(_crossfadeSeconds.Value.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < specs.Count; i++)
            {
                parts.Add(specs[i].Role.ToString());
                parts.Add(specs[i].Path);
            }

            return string.Join("|", parts.ToArray());
        }

        private List<LayerSpec> BuildLayerSpecs(bool logMissing)
        {
            List<LayerSpec> specs = new List<LayerSpec>();
            if (_musicMode.Value == MusicMode.CustomFile)
            {
                string customPath = ResolveMusicPath(
                    _customMusicFile.Value,
                    "custom music",
                    logMissing);
                if (!string.IsNullOrEmpty(customPath))
                {
                    specs.Add(
                        new LayerSpec(
                            LayerRole.Custom,
                            "custom music",
                            customPath,
                            GetVolumeForRole(LayerRole.Custom),
                            false,
                            true));
                }

                return specs;
            }

            if (_musicMode.Value != MusicMode.LayeredModifiedTaintedGrail)
            {
                return specs;
            }

            string basePath = ResolveMusicPath(
                _baseMusicFile.Value,
                "base title music",
                logMissing);
            if (string.IsNullOrEmpty(basePath))
            {
                return specs;
            }

            if (!string.IsNullOrEmpty(basePath))
            {
                specs.Add(
                    new LayerSpec(
                        LayerRole.Base,
                        "base title music",
                        basePath,
                        GetVolumeForRole(LayerRole.Base),
                        _applyEffectsToBaseMusic.Value,
                        true));
            }

            if (_enableFireAmbience.Value)
            {
                string firePath = ResolveMusicPath(
                    _fireAmbienceFile.Value,
                    "fire ambience",
                    logMissing);
                if (!string.IsNullOrEmpty(firePath))
                {
                    specs.Add(
                        new LayerSpec(
                            LayerRole.Fire,
                            "fire ambience",
                            firePath,
                            GetVolumeForRole(LayerRole.Fire),
                            false,
                            false));
                }
            }

            if (_enableWindAmbience.Value)
            {
                string windPath = ResolveMusicPath(
                    _windAmbienceFile.Value,
                    "wind ambience",
                    logMissing);
                if (!string.IsNullOrEmpty(windPath))
                {
                    specs.Add(
                        new LayerSpec(
                            LayerRole.Wind,
                            "wind ambience",
                            windPath,
                            GetVolumeForRole(LayerRole.Wind),
                            false,
                            false));
                }
            }

            return specs;
        }

        private bool TryStartLayerSet(
            bool startAtLoopStart,
            float volumeScale,
            out List<MusicLayer> startedLayers)
        {
            startedLayers = new List<MusicLayer>();
            List<LayerSpec> specs = BuildLayerSpecs(true);
            if (specs.Count == 0)
            {
                return false;
            }

            bool useBuiltInLoop = ShouldLoopCurrentMode()
                && !ShouldUseManualCrossfade();
            _musicLengthMs = 0;
            _loopStartMs = 0;
            _loopEndMs = 0;

            for (int i = 0; i < specs.Count; i++)
            {
                LayerSpec spec = specs[i];
                FMOD.Sound sound;
                if (!TryCreateCustomSound(spec.Path, useBuiltInLoop, out sound))
                {
                    if (spec.Required)
                    {
                        ReleaseLayerList(startedLayers);
                        return false;
                    }

                    continue;
                }

                if (i == 0)
                {
                    CalculateLoopPoints(sound);
                }

                if (useBuiltInLoop)
                {
                    ApplyLoopPointsToSound(sound);
                }

                MusicLayer layer;
                if (!TryPlayLayer(spec, sound, startAtLoopStart, volumeScale, out layer))
                {
                    sound.release();
                    if (spec.Required)
                    {
                        ReleaseLayerList(startedLayers);
                        return false;
                    }

                    continue;
                }

                startedLayers.Add(layer);
            }

            if (startedLayers.Count == 0)
            {
                return false;
            }

            return true;
        }

        private bool TryCreateCustomSound(
            string path,
            bool useBuiltInLoop,
            out FMOD.Sound sound)
        {
            FMOD.MODE mode = FMOD.MODE.DEFAULT
                | FMOD.MODE._2D
                | FMOD.MODE.CREATESTREAM;
            mode |= useBuiltInLoop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF;

            RESULT createResult = RuntimeManager.CoreSystem.createSound(
                path,
                mode,
                out sound);
            if (createResult != RESULT.OK)
            {
                Logger.LogWarning(
                    "FMOD createSound failed for main menu music "
                    + path
                    + ": "
                    + createResult
                    + ".");
                sound = default(FMOD.Sound);
                return false;
            }

            return true;
        }

        private bool TryPlayLayer(
            LayerSpec spec,
            FMOD.Sound sound,
            bool startAtLoopStart,
            float volumeScale,
            out MusicLayer layer)
        {
            layer = null;
            FMOD.ChannelGroup channelGroup;
            RESULT groupResult = RuntimeManager.CoreSystem.getMasterChannelGroup(
                out channelGroup);
            if (groupResult != RESULT.OK)
            {
                channelGroup = default(FMOD.ChannelGroup);
            }

            FMOD.Channel channel;
            RESULT playResult = RuntimeManager.CoreSystem.playSound(
                sound,
                channelGroup,
                true,
                out channel);
            if (playResult != RESULT.OK)
            {
                Logger.LogWarning(
                    "FMOD playSound failed for main menu music layer "
                    + spec.Label
                    + ": "
                    + playResult
                    + ".");
                channel = default(FMOD.Channel);
                return false;
            }

            if (startAtLoopStart && _loopStartMs > 0)
            {
                RESULT positionResult = channel.setPosition(_loopStartMs, TIMEUNIT.MS);
                if (positionResult != RESULT.OK)
                {
                    LogDiagnostic(
                        "Could not start looped layer "
                        + spec.Label
                        + " at loop start "
                        + _loopStartMs.ToString(CultureInfo.InvariantCulture)
                        + "ms: "
                        + positionResult
                        + ".");
                }
            }

            layer = new MusicLayer(spec, sound, channel);
            if (spec.ApplyEffects)
            {
                ConfigureLayerDsps(layer);
            }

            channel.setVolume(Math.Max(0.0f, spec.Volume * volumeScale));
            channel.setPaused(false);
            return true;
        }

        private void CalculateLoopPoints(FMOD.Sound sound)
        {
            _musicLengthMs = 0;
            _loopStartMs = 0;
            _loopEndMs = 0;

            uint lengthMs;
            RESULT lengthResult = sound.getLength(out lengthMs, TIMEUNIT.MS);
            if (lengthResult != RESULT.OK || lengthMs == 0)
            {
                Logger.LogWarning(
                    "Could not read main menu music length: "
                    + lengthResult
                    + ".");
                return;
            }

            _musicLengthMs = lengthMs;
            uint startMs = SecondsToMilliseconds(_loopStartSeconds.Value);
            uint endTrimMs = SecondsToMilliseconds(_loopEndTrimSeconds.Value);
            uint endMs = lengthMs > endTrimMs ? lengthMs - endTrimMs : lengthMs;

            if (startMs + MinimumLoopLengthMs >= endMs)
            {
                Logger.LogWarning(
                    "Configured loop trim points leave too little audio to loop; using the full file.");
                startMs = 0;
                endMs = lengthMs;
            }

            _loopStartMs = startMs;
            _loopEndMs = endMs;
        }

        private uint SecondsToMilliseconds(float seconds)
        {
            if (seconds <= 0.0f)
            {
                return 0;
            }

            double milliseconds = seconds * 1000.0;
            if (milliseconds >= uint.MaxValue)
            {
                return uint.MaxValue;
            }

            return (uint)Math.Round(milliseconds);
        }

        private void ApplyLoopPointsToSound(FMOD.Sound sound)
        {
            if (_loopEndMs <= _loopStartMs + MinimumLoopLengthMs)
            {
                return;
            }

            uint loopEndPoint = _loopEndMs > 0 ? _loopEndMs - 1 : _loopEndMs;
            RESULT loopResult = sound.setLoopPoints(
                _loopStartMs,
                TIMEUNIT.MS,
                loopEndPoint,
                TIMEUNIT.MS);
            if (loopResult != RESULT.OK)
            {
                Logger.LogWarning(
                    "Could not apply main menu music loop points: "
                    + loopResult
                    + ".");
            }
        }

        private bool ShouldUseManualCrossfade()
        {
            return _musicMode.Value != MusicMode.Off
                && _loop.Value
                && _crossfadeSeconds.Value > 0.001f;
        }

        private uint GetEffectiveCrossfadeMs()
        {
            if (!ShouldUseManualCrossfade())
            {
                return 0;
            }

            uint requestedMs = SecondsToMilliseconds(_crossfadeSeconds.Value);
            uint loopLengthMs = _loopEndMs > _loopStartMs
                ? _loopEndMs - _loopStartMs
                : _musicLengthMs;
            if (requestedMs == 0 || loopLengthMs <= MinimumLoopLengthMs)
            {
                return 0;
            }

            uint maximumMs = loopLengthMs > 100 ? loopLengthMs - 100 : 0;
            return Math.Min(requestedMs, maximumMs);
        }

        private void UpdateManualCrossfade()
        {
            UpdateLoopCrossfadeVolumes();

            uint crossfadeMs = GetEffectiveCrossfadeMs();
            if (crossfadeMs == 0 || _fadeOutLayers.Count > 0)
            {
                return;
            }

            if (_layers.Count == 0 || _loopEndMs <= _loopStartMs)
            {
                return;
            }

            uint positionMs;
            RESULT positionResult = _layers[0].Channel.getPosition(
                out positionMs,
                TIMEUNIT.MS);
            if (positionResult != RESULT.OK)
            {
                return;
            }

            uint triggerMs = _loopEndMs > crossfadeMs
                ? _loopEndMs - crossfadeMs
                : _loopStartMs;
            if (positionMs >= triggerMs)
            {
                StartLoopCrossfade(crossfadeMs);
            }
        }

        private void StartLoopCrossfade(uint crossfadeMs)
        {
            List<MusicLayer> nextLayers;
            if (!TryStartLayerSet(true, 0.0f, out nextLayers))
            {
                JumpToLoopStart();
                return;
            }

            ReleaseLayerList(_fadeOutLayers);
            ReplaceLayerList(_fadeOutLayers, _layers);
            ReplaceLayerList(_layers, nextLayers);

            _loopFadeStartedAt = Time.unscaledTime;
            _loopFadeDurationSeconds = Math.Max(0.01f, crossfadeMs / 1000.0f);
            LogDiagnostic(
                "Started main menu music loop crossfade over "
                + _loopFadeDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)
                + "s.");
        }

        private void UpdateLoopCrossfadeVolumes()
        {
            if (_fadeOutLayers.Count == 0)
            {
                return;
            }

            float progress = Mathf.Clamp01(
                (Time.unscaledTime - _loopFadeStartedAt)
                / Math.Max(0.01f, _loopFadeDurationSeconds));

            SetLayerSetVolume(_layers, progress);
            SetLayerSetVolume(_fadeOutLayers, 1.0f - progress);

            if (progress < 1.0f && IsLayerSetPlaying(_fadeOutLayers))
            {
                return;
            }

            ReleaseLayerList(_fadeOutLayers);
            RefreshLayerVolumes();
            _loopFadeStartedAt = -1.0f;
            _loopFadeDurationSeconds = 0.0f;
            LogDiagnostic("Completed main menu music loop crossfade.");
        }

        private void JumpToLoopStart()
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].Channel.handle == IntPtr.Zero)
                {
                    continue;
                }

                RESULT result = _layers[i].Channel.setPosition(
                    _loopStartMs,
                    TIMEUNIT.MS);
                if (result != RESULT.OK)
                {
                    LogDiagnostic(
                        "Could not jump layer "
                        + _layers[i].Label
                        + " to loop start: "
                        + result
                        + ".");
                }
            }
        }

        private void ConfigureLayerDsps(MusicLayer layer)
        {
            if (!TryCreateAndAttachDsp(
                    layer.Channel,
                    DSP_TYPE.PITCHSHIFT,
                    CHANNELCONTROL_DSP_INDEX.HEAD,
                    layer.Label + " pitch shifter",
                    out layer.PitchDsp))
            {
                return;
            }

            ConfigurePitchDsp(layer);
            ConfigureHighFrequencyRestoreDsp(layer);

            if (!_demonicMode.Value)
            {
                ReleaseDemonicDsps(layer);
                return;
            }

            ConfigureDistortionDsp(layer);
            ConfigureLowpassDsp(layer);
            ConfigureEchoDsp(layer);
        }

        private void RefreshLiveEffectSettings()
        {
            if (!IsLayeredMode())
            {
                return;
            }

            RefreshLiveEffectSettings(_layers);
            RefreshLiveEffectSettings(_fadeOutLayers);
        }

        private void RefreshLiveEffectSettings(List<MusicLayer> layers)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                MusicLayer layer = layers[i];
                if (layer == null || layer.Role != LayerRole.Base)
                {
                    continue;
                }

                if (!_applyEffectsToBaseMusic.Value)
                {
                    ReleaseLayerDsps(layer);
                    continue;
                }

                if (layer.PitchDsp.handle == IntPtr.Zero)
                {
                    ConfigureLayerDsps(layer);
                    continue;
                }

                ConfigurePitchDsp(layer);
                ConfigureHighFrequencyRestoreDsp(layer);

                if (!_demonicMode.Value)
                {
                    ReleaseDemonicDsps(layer);
                    continue;
                }

                ConfigureDistortionDsp(layer);
                ConfigureLowpassDsp(layer);
                ConfigureEchoDsp(layer);
            }
        }

        private bool TryCreateAndAttachDsp(
            FMOD.Channel channel,
            DSP_TYPE dspType,
            int dspIndex,
            string label,
            out DSP dsp)
        {
            dsp = default(DSP);
            FMOD.System coreSystem = RuntimeManager.CoreSystem;
            RESULT createResult = coreSystem.createDSPByType(dspType, out dsp);
            if (createResult != RESULT.OK)
            {
                Logger.LogWarning(
                    "Could not create FMOD DSP for "
                    + label
                    + ": "
                    + createResult
                    + ".");
                return false;
            }

            RESULT addResult = channel.addDSP(dspIndex, dsp);
            if (addResult != RESULT.OK)
            {
                Logger.LogWarning(
                    "Could not attach FMOD DSP for "
                    + label
                    + ": "
                    + addResult
                    + ".");
                dsp.release();
                dsp = default(DSP);
                return false;
            }

            return true;
        }

        private void ConfigurePitchDsp(MusicLayer layer)
        {
            RESULT pitchResult = layer.PitchDsp.setParameterFloat(
                (int)DSP_PITCHSHIFT.PITCH,
                PitchRatio);
            RESULT fftResult = layer.PitchDsp.setParameterFloat(
                (int)DSP_PITCHSHIFT.FFTSIZE,
                _fftSize.Value);
            RESULT overlapResult = layer.PitchDsp.setParameterFloat(
                (int)DSP_PITCHSHIFT.OVERLAP,
                _overlap.Value);

            LogDiagnostic(
                "Updated pitch DSP for "
                + layer.Label
                + ": pitch="
                + pitchResult
                + ", fft="
                + fftResult
                + ", overlap="
                + overlapResult
                + ", ratio="
                + PitchRatio.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private void ConfigureHighFrequencyRestoreDsp(MusicLayer layer)
        {
            if (!_enableHighFrequencyRestore.Value)
            {
                ReleaseDsp(layer, ref layer.HighFrequencyRestoreDsp);
                return;
            }

            if (layer.HighFrequencyRestoreDsp.handle == IntPtr.Zero)
            {
                TryCreateAndAttachDsp(
                    layer.Channel,
                    DSP_TYPE.THREE_EQ,
                    CHANNELCONTROL_DSP_INDEX.TAIL,
                    layer.Label + " high-frequency restore EQ",
                    out layer.HighFrequencyRestoreDsp);
            }

            if (layer.HighFrequencyRestoreDsp.handle != IntPtr.Zero)
            {
                layer.HighFrequencyRestoreDsp.setParameterFloat(
                    (int)DSP_THREE_EQ.LOWGAIN,
                    0.0f);
                layer.HighFrequencyRestoreDsp.setParameterFloat(
                    (int)DSP_THREE_EQ.MIDGAIN,
                    0.0f);
                layer.HighFrequencyRestoreDsp.setParameterFloat(
                    (int)DSP_THREE_EQ.HIGHGAIN,
                    _highFrequencyGainDb.Value);
                layer.HighFrequencyRestoreDsp.setParameterFloat(
                    (int)DSP_THREE_EQ.HIGHCROSSOVER,
                    _highFrequencyCrossoverHz.Value);
            }
        }

        private void ConfigureDistortionDsp(MusicLayer layer)
        {
            if (!_enableDistortion.Value)
            {
                ReleaseDsp(layer, ref layer.DistortionDsp);
                return;
            }

            if (layer.DistortionDsp.handle == IntPtr.Zero)
            {
                TryCreateAndAttachDsp(
                    layer.Channel,
                    DSP_TYPE.DISTORTION,
                    CHANNELCONTROL_DSP_INDEX.HEAD,
                    layer.Label + " distortion",
                    out layer.DistortionDsp);
            }

            if (layer.DistortionDsp.handle != IntPtr.Zero)
            {
                layer.DistortionDsp.setParameterFloat(
                    (int)DSP_DISTORTION.LEVEL,
                    _distortionLevel.Value);
            }
        }

        private void ConfigureLowpassDsp(MusicLayer layer)
        {
            if (!_enableLowpass.Value)
            {
                ReleaseDsp(layer, ref layer.LowpassDsp);
                return;
            }

            if (layer.LowpassDsp.handle == IntPtr.Zero)
            {
                TryCreateAndAttachDsp(
                    layer.Channel,
                    DSP_TYPE.LOWPASS,
                    CHANNELCONTROL_DSP_INDEX.HEAD,
                    layer.Label + " lowpass",
                    out layer.LowpassDsp);
            }

            if (layer.LowpassDsp.handle != IntPtr.Zero)
            {
                layer.LowpassDsp.setParameterFloat(
                    (int)DSP_LOWPASS.CUTOFF,
                    _lowpassCutoffHz.Value);
                layer.LowpassDsp.setParameterFloat(
                    (int)DSP_LOWPASS.RESONANCE,
                    1.0f);
            }
        }

        private void ConfigureEchoDsp(MusicLayer layer)
        {
            if (!_enableEcho.Value)
            {
                ReleaseDsp(layer, ref layer.EchoDsp);
                return;
            }

            if (layer.EchoDsp.handle == IntPtr.Zero)
            {
                TryCreateAndAttachDsp(
                    layer.Channel,
                    DSP_TYPE.ECHO,
                    CHANNELCONTROL_DSP_INDEX.HEAD,
                    layer.Label + " echo",
                    out layer.EchoDsp);
            }

            if (layer.EchoDsp.handle != IntPtr.Zero)
            {
                layer.EchoDsp.setParameterFloat(
                    (int)DSP_ECHO.DELAY,
                    _echoDelayMs.Value);
                layer.EchoDsp.setParameterFloat(
                    (int)DSP_ECHO.FEEDBACK,
                    _echoFeedbackPercent.Value);
                layer.EchoDsp.setParameterFloat(
                    (int)DSP_ECHO.DRYLEVEL,
                    0.0f);
                layer.EchoDsp.setParameterFloat(
                    (int)DSP_ECHO.WETLEVEL,
                    _echoWetLevelDb.Value);
            }
        }

        private void ReleaseDemonicDsps(MusicLayer layer)
        {
            ReleaseDsp(layer, ref layer.DistortionDsp);
            ReleaseDsp(layer, ref layer.LowpassDsp);
            ReleaseDsp(layer, ref layer.EchoDsp);
        }

        private void ReleaseLayerDsps(MusicLayer layer)
        {
            ReleaseDsp(layer, ref layer.EchoDsp);
            ReleaseDsp(layer, ref layer.LowpassDsp);
            ReleaseDsp(layer, ref layer.DistortionDsp);
            ReleaseDsp(layer, ref layer.HighFrequencyRestoreDsp);
            ReleaseDsp(layer, ref layer.PitchDsp);
        }

        private void ReleaseDsp(MusicLayer layer, ref DSP dsp)
        {
            if (dsp.handle == IntPtr.Zero)
            {
                return;
            }

            if (layer != null && layer.Channel.handle != IntPtr.Zero)
            {
                layer.Channel.removeDSP(dsp);
            }

            dsp.release();
            dsp = default(DSP);
        }

        private string ResolveMusicPath(
            string configured,
            string label,
            bool logMissing)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = "main_menu_music.wav";
            }

            string path = configured.Trim();
            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                return path;
            }

            string pluginDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginDirectory))
            {
                pluginDirectory = Paths.PluginPath;
            }

            string direct = Path.Combine(pluginDirectory, path);
            if (File.Exists(direct))
            {
                return direct;
            }

            string audioPath = Path.Combine(
                Path.Combine(pluginDirectory, "audio"),
                path);
            if (File.Exists(audioPath))
            {
                return audioPath;
            }

            if (logMissing && !_loggedMissingPaths.Contains(label + "|" + path))
            {
                Logger.LogWarning(
                    "Main menu music "
                    + label
                    + " file was not found. Looked for "
                    + direct
                    + " and "
                    + audioPath
                    + ".");
                _loggedMissingPaths.Add(label + "|" + path);
            }

            return string.Empty;
        }

        private bool IsLayerSetPlaying(List<MusicLayer> layers)
        {
            if (layers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Channel.handle == IntPtr.Zero)
                {
                    continue;
                }

                bool playing;
                RESULT result = layers[i].Channel.isPlaying(out playing);
                if (result == RESULT.OK && playing)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshLayerVolumes()
        {
            RefreshLayerVolumes(_layers);
            if (_fadeOutLayers.Count == 0)
            {
                return;
            }

            RefreshLayerVolumes(_fadeOutLayers);
            UpdateLoopCrossfadeVolumes();
        }

        private void RefreshLayerVolumes(List<MusicLayer> layers)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].TargetVolume = GetVolumeForRole(layers[i].Role);
                if (layers[i].Channel.handle != IntPtr.Zero)
                {
                    layers[i].Channel.setVolume(Math.Max(0.0f, layers[i].TargetVolume));
                }
            }
        }

        private void SetLayerSetVolume(List<MusicLayer> layers, float scale)
        {
            float safeScale = Mathf.Clamp01(scale);
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].TargetVolume = GetVolumeForRole(layers[i].Role);
                if (layers[i].Channel.handle != IntPtr.Zero)
                {
                    layers[i].Channel.setVolume(
                        Math.Max(0.0f, layers[i].TargetVolume * safeScale));
                }
            }
        }

        private float GetVolumeForRole(LayerRole role)
        {
            switch (role)
            {
                case LayerRole.Base:
                    return GetScaledVolume(_baseMusicVolume.Value);
                case LayerRole.Fire:
                    return GetScaledVolume(_fireAmbienceVolume.Value);
                case LayerRole.Wind:
                    return GetScaledVolume(_windAmbienceVolume.Value);
                case LayerRole.Custom:
                    return GetScaledVolume(_customMusicVolume.Value);
                default:
                    return 1.0f;
            }
        }

        private static float GetScaledVolume(float configuredVolume)
        {
            return Math.Max(0.0f, configuredVolume) * VolumeOutputScale;
        }

        private void RestartCustomMusic()
        {
            if (!_enabled.Value || !_titlePlaybackAllowed)
            {
                return;
            }

            if (_musicMode.Value == MusicMode.Off)
            {
                StopCustomMusic("off");
                UnmuteOriginals();
                return;
            }

            StopCustomMusic("config changed");
            EnsureCustomMusicPlaying(false);
        }

        private void RestartLayeredMusicIfActive()
        {
            if (IsLayeredMode())
            {
                RestartCustomMusic();
            }
        }

        private void RestartCustomMusicIfActive()
        {
            if (_musicMode.Value == MusicMode.CustomFile)
            {
                RestartCustomMusic();
            }
        }

        private bool IsLayeredMode()
        {
            return _musicMode.Value == MusicMode.LayeredModifiedTaintedGrail;
        }

        private bool ShouldLoopCurrentMode()
        {
            return _musicMode.Value != MusicMode.Off && _loop.Value;
        }

        private void BeginExitFade(string reason)
        {
            _titlePlaybackAllowed = false;
            _activeTitleMusicView = null;

            if (_retryCoroutine != null)
            {
                StopCoroutine(_retryCoroutine);
                _retryCoroutine = null;
            }

            if (!_fadeOutOnGameLoad.Value
                || _gameLoadFadeSeconds.Value <= 0.001f
                || (_layers.Count == 0 && _fadeOutLayers.Count == 0))
            {
                StopCustomMusic(reason);
                StopMutedOriginals(reason);
                return;
            }

            _exitFadeActive = true;
            _exitFadeStartedAt = Time.unscaledTime;
            _exitFadeDurationSeconds = Math.Max(0.01f, _gameLoadFadeSeconds.Value);
            _exitFadeReason = reason;
            LogDiagnostic(
                "Started main menu music exit fade over "
                + _exitFadeDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)
                + "s: "
                + reason
                + ".");
        }

        private void UpdateExitFade()
        {
            float progress = Mathf.Clamp01(
                (Time.unscaledTime - _exitFadeStartedAt)
                / Math.Max(0.01f, _exitFadeDurationSeconds));
            float scale = 1.0f - progress;
            SetLayerSetVolume(_layers, scale);
            SetLayerSetVolume(_fadeOutLayers, scale);

            if (progress < 1.0f)
            {
                return;
            }

            string reason = string.IsNullOrEmpty(_exitFadeReason)
                ? "exit fade complete"
                : _exitFadeReason;
            StopCustomMusic(reason);
            StopMutedOriginals(reason);
        }

        private void StopCustomMusic(string reason)
        {
            bool stopped = false;
            stopped |= ReleaseLayerList(_layers);
            stopped |= ReleaseLayerList(_fadeOutLayers);

            if (stopped)
            {
                LogDiagnostic("Stopped replacement main menu music: " + reason + ".");
            }

            _loadedSignature = string.Empty;
            _musicLengthMs = 0;
            _loopStartMs = 0;
            _loopEndMs = 0;
            _loopFadeStartedAt = -1.0f;
            _loopFadeDurationSeconds = 0.0f;
            _exitFadeActive = false;
            _exitFadeStartedAt = -1.0f;
            _exitFadeDurationSeconds = 0.0f;
            _exitFadeReason = string.Empty;
        }

        private bool ReleaseLayerList(List<MusicLayer> layers)
        {
            bool stopped = false;
            for (int i = 0; i < layers.Count; i++)
            {
                stopped |= ReleaseLayer(layers[i]);
            }

            layers.Clear();
            return stopped;
        }

        private bool ReleaseLayer(MusicLayer layer)
        {
            bool stopped = false;
            if (layer == null)
            {
                return false;
            }

            ReleaseLayerDsps(layer);

            if (layer.Channel.handle != IntPtr.Zero)
            {
                layer.Channel.stop();
                layer.Channel = default(FMOD.Channel);
                stopped = true;
            }

            if (layer.Sound.handle != IntPtr.Zero)
            {
                layer.Sound.release();
                layer.Sound = default(FMOD.Sound);
            }

            return stopped;
        }

        private void ReplaceLayerList(
            List<MusicLayer> destination,
            List<MusicLayer> source)
        {
            destination.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }

            source.Clear();
        }

        internal void OnTitleMusicClosed(object titleMusicView)
        {
            BeginExitFade("title music view closed");
        }

        internal void OnGameLoadStarted(object loadingOperation)
        {
            if (!_enabled.Value)
            {
                return;
            }

            string label = loadingOperation == null
                ? "game loading"
                : loadingOperation.GetType().Name;
            BeginExitFade("game loading started: " + label);
        }

        private void StopMutedOriginals(string reason)
        {
            if (_mutedOriginals.Count == 0)
            {
                return;
            }

            foreach (EventInstance instance in _mutedOriginals.Values)
            {
                try
                {
                    if (instance.isValid())
                    {
                        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                        instance.setVolume(1.0f);
                    }
                }
                catch (Exception exception)
                {
                    LogDiagnostic(
                        "Could not stop original title music: "
                        + exception.Message);
                }
            }

            LogDiagnostic("Stopped muted original title emitters: " + reason + ".");
            _mutedOriginals.Clear();
        }

        private void UnmuteOriginals()
        {
            foreach (EventInstance instance in _mutedOriginals.Values)
            {
                try
                {
                    if (instance.isValid())
                    {
                        instance.setVolume(1.0f);
                    }
                }
                catch (Exception exception)
                {
                    LogDiagnostic(
                        "Could not restore original title music volume: "
                        + exception.Message);
                }
            }

            if (_mutedOriginals.Count > 0)
            {
                LogDiagnostic("Restored original title music volume.");
            }

            _mutedOriginals.Clear();
        }

        private void OnDestroy()
        {
            UnregisterConfigHandlers();

            if (_retryCoroutine != null)
            {
                StopCoroutine(_retryCoroutine);
                _retryCoroutine = null;
            }

            StopCustomMusic("plugin destroyed");
            UnmuteOriginals();

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            Instance = null;
        }

        private void LogDiagnostic(string message)
        {
            if (_verboseLogging != null && _verboseLogging.Value)
            {
                Logger.LogInfo(message);
            }
        }
    }

    internal sealed class LayerSpec
    {
        internal readonly LayerRole Role;
        internal readonly string Label;
        internal readonly string Path;
        internal readonly float Volume;
        internal readonly bool ApplyEffects;
        internal readonly bool Required;

        internal LayerSpec(
            LayerRole role,
            string label,
            string path,
            float volume,
            bool applyEffects,
            bool required)
        {
            Role = role;
            Label = label;
            Path = path;
            Volume = volume;
            ApplyEffects = applyEffects;
            Required = required;
        }
    }

    internal sealed class MusicLayer
    {
        internal readonly LayerRole Role;
        internal readonly string Label;
        internal readonly string Path;
        internal float TargetVolume;
        internal FMOD.Sound Sound;
        internal FMOD.Channel Channel;
        internal DSP PitchDsp;
        internal DSP HighFrequencyRestoreDsp;
        internal DSP DistortionDsp;
        internal DSP LowpassDsp;
        internal DSP EchoDsp;

        internal MusicLayer(
            LayerSpec spec,
            FMOD.Sound sound,
            FMOD.Channel channel)
        {
            Role = spec.Role;
            Label = spec.Label;
            Path = spec.Path;
            TargetVolume = spec.Volume;
            Sound = sound;
            Channel = channel;
        }
    }

    internal static class Patches
    {
        internal static void AfterPlayMusic(object __instance)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.ApplyToTitleMusic(__instance);
            }
        }

        internal static void AfterTitleMusicClosed(object __instance)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.OnTitleMusicClosed(__instance);
            }
        }

        internal static void BeforeGameLoading(object __instance)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.OnGameLoadStarted(__instance);
            }
        }
    }
}
