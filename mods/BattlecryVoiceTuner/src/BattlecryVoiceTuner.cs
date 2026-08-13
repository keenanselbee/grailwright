using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.MVC.UI;
using Awaken.TG.MVC.UI.Events;
using Awaken.TG.Main.AI;
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Character.Features;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.VolumeCheckers;
using Awaken.TG.Main.Utility;
using Awaken.Utility;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using Grailwright.Shared;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Battlecry Voice Tuner")]
[assembly: AssemblyDescription("Tunes player voice audio and adds configurable battlecries in Tainted Grail: The Fall of Avalon.")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Battlecry Voice Tuner")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("1.1.3.0")]
[assembly: AssemblyFileVersion("1.1.3.0")]

namespace BattlecryVoiceTuner
{
    internal sealed class FoASettingUiMetadata
    {
        public string DisplaySection { get; set; }
        public string DisplayName { get; set; }
        public int SectionOrder { get; set; }
        public int Order { get; set; }
        public bool Hidden { get; set; }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.eyes-in-the-dark", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BattlecryVoiceTunerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.battlecry-voice-tuner";
        public const string PluginName = "Battlecry Voice Tuner";
        public const string PluginVersion = "1.1.3";

        private const int CurrentConfigSchemaVersion = 7;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new[]
                {
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        6,
                        "3. Battlecry",
                        "BattlecryAggroRangeMultiplier",
                        "The existing hearing range multiplier now applies only outdoors.")
                };
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new[]
            {
                new ConfigDefinition(
                    "4. Testing",
                    "PlayRandomTestSound")
            };

        private const string CategoryAttack = "Attack";
        private const string CategoryHurt = "Hurt";
        private const string CategoryDeath = "Death";
        private const string CategoryStatus = "Status";
        private const string CategoryHitFeedback = "HitFeedback";
        private const string CategoryStamina = "Stamina";
        private const int MaximumBattlecryFilesPerGender = 15;
        private const float ChallengeScanIntervalSeconds = 0.25f;
        private const int OutdoorProbeDirectionCount = 24;
        private const int MaximumOutdoorReflectionTaps = 3;
        private const float OutdoorProbeHeight = 1.5f;
        private const float OutdoorProbeMaximumDistance = 80f;
        private const float OutdoorReflectionSpeedOfSound = 343f;
        private const float OutdoorReflectionDirectionSeparation = 0.72f;
        private const int InteriorProbeDirectionCount = 30;
        private const float InteriorProbeHeight = 1.4f;
        private const float InteriorProbeMaximumDistance = 50f;
        private const float InteriorDiscreteReflectionMinimumDelay = 0.06f;
        private const int OutdoorAcousticLayerMask =
            RenderLayers.Mask.Default
            | RenderLayers.Mask.Walkable
            | RenderLayers.Mask.Objects
            | RenderLayers.Mask.Terrain
            | RenderLayers.Mask.Vegetation
            | RenderLayers.Mask.RainObstacle;
        private const string EyesInTheDarkApiTypeName =
            "EyesInTheDark.EyesInTheDarkBattlecryApi, EyesInTheDark";

        private static readonly SupportedVoiceEvent[] SupportedEvents = new SupportedVoiceEvent[]
        {
            new SupportedVoiceEvent("e717278c-662d-4df1-a985-ae328d2b5182", "event:/SFX/Player/Nonverbal_Male/SFX_NonverbalMale2_Attack", CategoryAttack, "Male 2 attack grunt"),
            new SupportedVoiceEvent("970a6fbb-d8ce-4e2f-9312-147a8df4f39d", "event:/SFX/Player/Nonverbal_Male/SFX_NonverbalMale2_Hurt", CategoryHurt, "Male 2 hurt grunt"),
            new SupportedVoiceEvent("49c9fb8d-7128-404a-8c24-52c85b29445c", "event:/SFX/Player/Nonverbal_Male/SFX_NonverbalMale2_Die", CategoryDeath, "Male 2 death grunt"),
            new SupportedVoiceEvent("d639e6f2-370a-49a7-8cde-86a15dd562db", "event:/SFX/Player/Nonverbal_Male/SFX_NonverbalMale2_Burn", CategoryStatus, "Male 2 burn grunt"),
            new SupportedVoiceEvent("3440b273-9c04-4a91-991c-772021916580", "event:/SFX/Player/Nonverbal_Male/SFX_NonverbalMale2_Bleed", CategoryStatus, "Male 2 bleed grunt"),
            new SupportedVoiceEvent("5c041e61-ee21-4188-971f-07ab86aaa460", "event:/SFX/Player/Nonverbal_Male/SFX_NonverbalMale2_Poison", CategoryStatus, "Male 2 poison grunt"),
            new SupportedVoiceEvent("f8bbe031-77ae-4e9b-9a8c-5d543611464c", "event:/SFX/Player/Nonverbal_Male/SFX_NonverbalMale2_Drown", CategoryStatus, "Male 2 drown grunt"),

            new SupportedVoiceEvent("6f81cc51-a603-4980-8a8d-a3126d4ba025", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale_Attack", CategoryAttack, "Female attack grunt"),
            new SupportedVoiceEvent("a530cf4a-66b6-45bf-9ec7-238933945c09", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale2_Attack", CategoryAttack, "Female 2 attack grunt"),
            new SupportedVoiceEvent("087d44cb-0add-4f14-867f-c4400c794779", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale_Hurt", CategoryHurt, "Female hurt grunt"),
            new SupportedVoiceEvent("9a221479-dc2d-4f14-918a-c2971e2974da", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale2_Hurt", CategoryHurt, "Female 2 hurt grunt"),
            new SupportedVoiceEvent("d1543542-5d8b-4a9b-87b6-7b8bd1a82fb0", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale2_Die", CategoryDeath, "Female death grunt"),
            new SupportedVoiceEvent("031b7974-3f51-4c59-9dfc-315f7e3d057d", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale2_Burn", CategoryStatus, "Female 2 burn grunt"),
            new SupportedVoiceEvent("0d56796a-e1be-49a8-84e6-b21a31fbfa5f", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale2_Bleed", CategoryStatus, "Female 2 bleed grunt"),
            new SupportedVoiceEvent("4da68f9f-5a53-411d-9309-d9e70018d286", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale2_Poison", CategoryStatus, "Female 2 poison grunt"),
            new SupportedVoiceEvent("4a7f4767-d60e-4ddb-bbe8-6c1ff066c2f9", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale2_Drown", CategoryStatus, "Female 2 drown grunt"),
            new SupportedVoiceEvent("2e88a4ca-0a17-498a-a691-f83682a5e032", "event:/SFX/Player/NonverbalFemale/SFX_NonverbalFemale_StaminaDepleted", CategoryStamina, "Female stamina-depleted breathing"),

            new SupportedVoiceEvent("46b384de-6ab9-464b-8e24-21e3a889777f", "event:/SFX/Player/SFX_Player_Hit", CategoryHitFeedback, "Player hit feedback")
        };
        private static readonly Vector3[] OutdoorProbeDirections =
            BuildOutdoorProbeDirections();
        private static readonly Vector3[] InteriorProbeDirections =
            BuildInteriorProbeDirections();

        private static BattlecryVoiceTunerPlugin _instance;

        private readonly System.Random _random = new System.Random();

        private ManualLogSource _log;
        private Harmony _harmony;
        private ConfigEntry<int> _configSchemaVersion;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _pitchSemitones;
        private ConfigEntry<float> _randomPitchSemitones;
        private ConfigEntry<float> _volumeMultiplier;
        private ConfigEntry<bool> _includeAttackGrunts;
        private ConfigEntry<bool> _includeHurtGrunts;
        private ConfigEntry<bool> _includeDeathGrunts;
        private ConfigEntry<bool> _includeStatusPainGrunts;
        private ConfigEntry<bool> _includePlayerHitFeedback;
        private ConfigEntry<bool> _includeStaminaDepletedBreathing;
        private ConfigEntry<bool> _battlecryEnabled;
        private ConfigEntry<float> _battlecryVolumeMultiplier;
        private ConfigEntry<bool> _battlecryReverbEnabled;
        private ConfigEntry<float> _outdoorBattlecryReverbAmount;
        private ConfigEntry<float> _indoorBattlecryReverbAmount;
        private ConfigEntry<float> _maleBattlecryPitchOffsetSemitones;
        private ConfigEntry<float> _femaleBattlecryPitchOffsetSemitones;
        private ConfigEntry<bool> _holdTakeAllItemsForBattlecry;
        private ConfigEntry<float> _battlecryHoldSeconds;
        private ConfigEntry<KeyboardShortcut> _battlecryHotkey;
        private ConfigEntry<float> _battlecryCooldownSeconds;
        private ConfigEntry<float> _battlecryAggroRangeMultiplier;
        private ConfigEntry<float> _indoorBattlecryAggroRangeMultiplier;
        private ConfigEntry<float> _battlecryAggroDurationSeconds;
        private ConfigEntry<float> _eyesInTheDarkThreat;
        private ConfigEntry<bool> _playRandomTestSound;
        private ConfigEntry<bool> _diagnostics;
        private readonly List<string> _maleBattlecryPaths =
            new List<string>();
        private readonly List<string> _femaleBattlecryPaths =
            new List<string>();
        private readonly Dictionary<string, FMOD.Sound> _battlecrySoundsByPath =
            new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private FMOD.Studio.Bus _battlecrySfxBus;
        private FMOD.ChannelGroup _battlecrySfxChannelGroup;
        private FMOD.ChannelGroup _outdoorBattlecryChannelGroup;
        private FMOD.ChannelGroup _indoorBattlecryChannelGroup;
        private FMOD.DSP _outdoorBattlecryReverb;
        private FMOD.DSP _indoorBattlecryReverb;
        private bool _battlecrySfxBusLocked;
        private readonly HashSet<NpcAI> _challengedNpcs =
            new HashSet<NpcAI>();
        private readonly Dictionary<string, float> _pendingPreservedVoiceTuning =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;
        private int _lastMaleBattlecryIndex = -1;
        private int _lastFemaleBattlecryIndex = -1;
        private float _lastBattlecryTime = float.NegativeInfinity;
        private float _challengeEndsAt;
        private float _nextChallengeScanAt;
        private Hero _challengeHero;
        private bool _takeAllItemsHeld;
        private bool _battlecryAttemptedForHold;
        private float _takeAllItemsPressedAt;
        private bool _eyesApiResolved;
        private MethodInfo _eyesBattlecryMethod;
        private bool _noBattlecryFilesWarningLogged;
        private bool _resettingTestButton;
        private bool _heroUnderRoof;

        private void Awake()
        {
            _instance = this;
            _log = Logger;

            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
                DiscoverBattlecryFiles();
                PatchGame();

                _log.LogInfo(PluginName + " " + PluginVersion + " loaded.");
            }
            catch (Exception ex)
            {
                _log.LogError(PluginName + " failed to load: " + ex);
                GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, ex);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_enabled == null
                || !_enabled.Value
                || _battlecryEnabled == null
                || !_battlecryEnabled.Value)
            {
                EndChallenge();
                return;
            }

            if (_battlecryHotkey != null
                && IsShortcutDown(_battlecryHotkey.Value))
            {
                TryPerformBattlecry(Hero.Current, "custom hotkey");
            }

            AdvanceChallenge();
        }

        private void OnDestroy()
        {
            if (_playRandomTestSound != null)
            {
                _playRandomTestSound.SettingChanged -= OnPlayRandomTestSoundChanged;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            ReleaseBattlecrySounds();

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
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

            if (storedSchemaVersion == CurrentConfigSchemaVersion)
            {
                return;
            }

            CapturePreservedVoiceTuning(
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
                File.WriteAllText(configPath, String.Empty);
                Config.Clear();
                Config.Reload();

                _log.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + CurrentConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid, PluginName, storedSchemaVersion, CurrentConfigSchemaVersion);
            }
            catch (Exception ex)
            {
                ClearPendingPreservedVoiceTuning();

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
                    _log.LogError("Failed to restore " + PluginName + " config backup after schema reset failure: " + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException("Failed to reset " + PluginName + " config schema. Original config was left in place when possible.", ex);
            }
        }

        private void CapturePreservedVoiceTuning(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedVoiceTuning();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        CurrentConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            string currentSection = String.Empty;
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

                if (!String.Equals(currentSection, "1. Core", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string settingName = line.Substring(0, separatorIndex).Trim();
                if (!IsPreservedVoiceTuningSetting(settingName))
                {
                    continue;
                }

                float parsedValue;
                if (profile.TryGetCustomizedValue(
                    currentSection,
                    settingName,
                    out parsedValue))
                {
                    _pendingPreservedVoiceTuning[settingName] = parsedValue;
                }
            }
        }

        private static bool IsPreservedVoiceTuningSetting(string settingName)
        {
            return String.Equals(settingName, "PitchSemitones", StringComparison.Ordinal)
                || String.Equals(settingName, "RandomPitchSemitones", StringComparison.Ordinal)
                || String.Equals(settingName, "VolumeMultiplier", StringComparison.Ordinal);
        }

        private void RestorePreservedVoiceTuning()
        {
            if (_pendingPreservedVoiceTuning.Count == 0
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreservedFloat(
                "PitchSemitones",
                _pitchSemitones,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                "RandomPitchSemitones",
                _randomPitchSemitones,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                "VolumeMultiplier",
                _volumeMultiplier,
                ref restoredCount,
                ref clampedCount);

            _log.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " voice tuning value(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + _pendingPreservedInvalidValueCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            ClearPendingPreservedVoiceTuning();
        }

        private void RestorePreservedFloat(
            string settingName,
            ConfigEntry<float> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            float preservedValue;
            if (entry == null
                || !_pendingPreservedVoiceTuning.TryGetValue(settingName, out preservedValue))
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

        private void ClearPendingPreservedVoiceTuning()
        {
            _pendingPreservedVoiceTuning.Clear();
            _pendingPreservedInvalidValueCount = 0;
        }

        private void BindConfig()
        {
            _configSchemaVersion = Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                CurrentConfigSchemaVersion,
                new ConfigDescription(
                    "Internal config schema marker. Do not edit this value.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false),
                    new FoASettingUiMetadata
                    {
                        DisplaySection = "General",
                        DisplayName = "Config Schema Version",
                        SectionOrder = 0,
                        Order = -1,
                        Hidden = true
                    }));

            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                UiDescription(
                    "Master toggle for player voice tuning and battlecries.",
                    "General",
                    "Enabled",
                    0,
                    0));

            _pitchSemitones = Config.Bind(
                "1. Core",
                "PitchSemitones",
                0.0f,
                UiDescription(
                    "Overall pitch shift applied to supported player voice sounds and battlecries. Gender-specific battlecry offsets are added to this value.",
                    "Voice Tuning",
                    "Overall Pitch (Semitones)",
                    1,
                    0,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _randomPitchSemitones = Config.Bind(
                "1. Core",
                "RandomPitchSemitones",
                0.15f,
                UiDescription(
                    "Maximum random pitch variation added per played sound, in semitones. Set 0 for a fixed pitch.",
                    "Voice Tuning",
                    "Random Pitch (Semitones)",
                    1,
                    1,
                    new AcceptableValueRange<float>(0.0f, 3.0f)));

            _volumeMultiplier = Config.Bind(
                "1. Core",
                "VolumeMultiplier",
                1.0f,
                UiDescription(
                    "Overall volume multiplier for supported native voice events and custom battlecries. Battlecries also apply their own volume multiplier.",
                    "Voice Tuning",
                    "Overall Voice Volume",
                    1,
                    2,
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            _includeAttackGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeAttackGrunts",
                true,
                UiDescription(
                    "Tune player attack/exertion grunts.",
                    "Native Voice Events",
                    "Attack and Exertion Grunts",
                    2,
                    0));

            _includeHurtGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeHurtGrunts",
                true,
                UiDescription(
                    "Tune player hurt grunts.",
                    "Native Voice Events",
                    "Hurt Grunts",
                    2,
                    1));

            _includeDeathGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeDeathGrunts",
                true,
                UiDescription(
                    "Tune player death grunts.",
                    "Native Voice Events",
                    "Death Grunts",
                    2,
                    2));

            _includeStatusPainGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeStatusPainGrunts",
                true,
                UiDescription(
                    "Tune player burn, bleed, poison, and drown grunts.",
                    "Native Voice Events",
                    "Status Pain Grunts",
                    2,
                    3));

            _includePlayerHitFeedback = Config.Bind(
                "2. Supported Sounds",
                "IncludePlayerHitFeedback",
                true,
                UiDescription(
                    "Tune SFX_Player_Hit, the player hit-feedback sound used when the player lands a hit.",
                    "Native Voice Events",
                    "Player Hit Feedback",
                    2,
                    4));

            _includeStaminaDepletedBreathing = Config.Bind(
                "2. Supported Sounds",
                "IncludeStaminaDepletedBreathing",
                false,
                UiDescription(
                    "Tune stamina-depleted breathing loops. Off by default because these are longer/looping sounds.",
                    "Native Voice Events",
                    "Stamina-Depleted Breathing",
                    2,
                    5));

            _battlecryEnabled = Config.Bind(
                "3. Battlecry",
                "BattlecryEnabled",
                true,
                UiDescription(
                    "Enable custom battlecry audio and its enemy challenge effect.",
                    "Battlecry Audio",
                    "Enabled",
                    3,
                    0));

            _battlecryVolumeMultiplier = Config.Bind(
                "3. Battlecry",
                "BattlecryVolumeMultiplier",
                0.5f,
                UiDescription(
                    "Additional volume multiplier applied only to custom battlecries after Overall Voice Volume. Battlecries also follow the game's SFX volume category.",
                    "Battlecry Audio",
                    "Battlecry Volume",
                    3,
                    1,
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            _battlecryReverbEnabled = Config.Bind(
                "3. Battlecry",
                "BattlecryReverbEnabled",
                true,
                UiDescription(
                    "Apply environment-aware reverb only to custom battlecries. Full interiors and the game's roof volumes use the indoor amount; other open-world areas use the outdoor amount.",
                    "Battlecry Audio",
                    "Dynamic Reverb",
                    3,
                    2));

            _outdoorBattlecryReverbAmount = Config.Bind(
                "3. Battlecry",
                "OutdoorBattlecryReverbAmount",
                0.15f,
                UiDescription(
                    "Light reverb amount for battlecries in unroofed open-world areas. Zero is dry; one is the strongest supported effect.",
                    "Battlecry Audio",
                    "Outdoor Reverb Amount",
                    3,
                    3,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            _indoorBattlecryReverbAmount = Config.Bind(
                "3. Battlecry",
                "IndoorBattlecryReverbAmount",
                0.70f,
                UiDescription(
                    "Strength multiplier for room-scaled reverb and qualifying long reflections in interiors, caves, and the game's roof volumes. Zero is dry; one is the strongest supported effect.",
                    "Battlecry Audio",
                    "Indoor Reverb Amount",
                    3,
                    4,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            _maleBattlecryPitchOffsetSemitones = Config.Bind(
                "3. Battlecry",
                "MaleBattlecryPitchOffsetSemitones",
                0.0f,
                UiDescription(
                    "Additional pitch shift applied only to male battlecries after the overall PitchSemitones setting.",
                    "Battlecry Audio",
                    "Male Pitch Offset (Semitones)",
                    3,
                    5,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _femaleBattlecryPitchOffsetSemitones = Config.Bind(
                "3. Battlecry",
                "FemaleBattlecryPitchOffsetSemitones",
                0.0f,
                UiDescription(
                    "Additional pitch shift applied only to female battlecries after the overall PitchSemitones setting.",
                    "Battlecry Audio",
                    "Female Pitch Offset (Semitones)",
                    3,
                    6,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _holdTakeAllItemsForBattlecry = Config.Bind(
                "3. Battlecry",
                "HoldTakeAllItemsForBattlecry",
                true,
                UiDescription(
                    "Hold the game's Take All Items action to battlecry. Uses the game's current remapped keyboard or controller binding and does not interfere with taking items from an open container.",
                    "Battlecry Input",
                    "Hold Take All Items",
                    4,
                    0));

            _battlecryHoldSeconds = Config.Bind(
                "3. Battlecry",
                "BattlecryHoldSeconds",
                0.45f,
                UiDescription(
                    "Seconds the Take All Items action must be held before attempting a battlecry.",
                    "Battlecry Input",
                    "Hold Time (Seconds)",
                    4,
                    1,
                    new AcceptableValueRange<float>(0.2f, 2.0f)));

            _battlecryHotkey = Config.Bind(
                "3. Battlecry",
                "BattlecryHotkey",
                new KeyboardShortcut(KeyCode.None),
                UiDescription(
                    "Optional separate keyboard or joystick-button shortcut. None disables the separate shortcut.",
                    "Battlecry Input",
                    "Separate Hotkey",
                    4,
                    2));

            _battlecryCooldownSeconds = Config.Bind(
                "3. Battlecry",
                "BattlecryCooldownSeconds",
                1.5f,
                UiDescription(
                    "Minimum active gameplay seconds between battlecries.",
                    "Battlecry Input",
                    "Cooldown (Seconds)",
                    4,
                    3,
                    new AcceptableValueRange<float>(0.0f, 30.0f)));

            _battlecryAggroRangeMultiplier = Config.Bind(
                "3. Battlecry",
                "BattlecryAggroRangeMultiplier",
                3.0f,
                UiDescription(
                    "Multiplier applied to each hostile NPC's normal maximum hearing range for battlecries in unroofed open-world areas.",
                    "Battlecry Challenge",
                    "Outdoor Hearing Range Multiplier",
                    5,
                    0,
                    new AcceptableValueRange<float>(0.0f, 5.0f)));

            _indoorBattlecryAggroRangeMultiplier = Config.Bind(
                "3. Battlecry",
                "IndoorBattlecryAggroRangeMultiplier",
                4.0f,
                UiDescription(
                    "Multiplier applied to each hostile NPC's normal maximum hearing range in interiors, caves, and the game's roof volumes.",
                    "Battlecry Challenge",
                    "Indoor Hearing Range Multiplier",
                    5,
                    1,
                    new AcceptableValueRange<float>(0.0f, 5.0f)));

            _battlecryAggroDurationSeconds = Config.Bind(
                "3. Battlecry",
                "BattlecryAggroDurationSeconds",
                3.0f,
                UiDescription(
                    "Active gameplay seconds during which newly reached hostile NPCs can hear the challenge.",
                    "Battlecry Challenge",
                    "Challenge Duration (Seconds)",
                    5,
                    2,
                    new AcceptableValueRange<float>(0.1f, 10.0f)));

            _eyesInTheDarkThreat = Config.Bind(
                "3. Battlecry",
                "EyesInTheDarkThreat",
                10.0f,
                UiDescription(
                    "Wyrd Threat requested from Eyes in the Dark for each successful battlecry. Has no effect when Eyes is absent or its Wyrdnight activity rules reject the request.",
                    "Optional Integrations",
                    "Eyes in the Dark Threat",
                    6,
                    0,
                    new AcceptableValueRange<float>(0.0f, 100.0f)));

            _playRandomTestSound = Config.Bind(
                "4. Testing",
                "PlayRandomTestSound",
                false,
                UiDescription(
                    "Pseudo-button. Toggle on to play one random supported one-shot sound, then the mod resets this to false.",
                    "Testing",
                    "Play Random Native Voice Sound",
                    7,
                    0));
            _playRandomTestSound.SettingChanged += OnPlayRandomTestSoundChanged;

            _diagnostics = Config.Bind(
                "5. Diagnostics",
                "Diagnostics",
                false,
                UiDescription(
                    "Write detailed match and FMOD result information to the BepInEx log.",
                    "Diagnostics",
                    "Diagnostics",
                    8,
                    0));

            RestorePreservedVoiceTuning();
            Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                Config,
                Logger,
                PluginName,
                CurrentConfigSchemaVersion,
                ConfigRecoveryBaselineSchema,
                ConfigRecoveryKeepCurrentDefaultRules,
                ConfigRecoveryPermanentExclusions);
            Config.Save();
        }

        private static ConfigDescription UiDescription(
            string description,
            string displaySection,
            string displayName,
            int sectionOrder,
            int order,
            AcceptableValueBase acceptableValues = null)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new FoASettingUiMetadata
                {
                    DisplaySection = displaySection,
                    DisplayName = displayName,
                    SectionOrder = sectionOrder,
                    Order = order
                });
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);
            MethodInfo target = AccessTools.Method(
                typeof(RuntimeManager),
                "TryCreateInstance",
                new Type[] { typeof(EventDescription), typeof(EventInstance).MakeByRefType() });
            if (target == null)
            {
                throw new MissingMethodException("FMODUnity.RuntimeManager.TryCreateInstance(EventDescription, out EventInstance)");
            }

            MethodInfo postfix = AccessTools.Method(
                typeof(RuntimeManagerTryCreateInstancePatch),
                nameof(RuntimeManagerTryCreateInstancePatch.Postfix));
            if (postfix == null)
            {
                throw new MissingMethodException(typeof(RuntimeManagerTryCreateInstancePatch).FullName + ".Postfix");
            }

            _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            _log.LogInfo("Patched FMOD RuntimeManager.TryCreateInstance(EventDescription, out EventInstance).");

            MethodInfo heroKeysTarget = AccessTools.Method(
                typeof(VHeroKeys),
                nameof(VHeroKeys.Handle),
                new Type[] { typeof(UIEvent) });
            MethodInfo heroKeysPrefix = AccessTools.Method(
                typeof(VHeroKeysHandlePatch),
                nameof(VHeroKeysHandlePatch.Prefix));
            if (heroKeysTarget == null || heroKeysPrefix == null)
            {
                throw new MissingMethodException(
                    "Awaken.TG.Main.Heroes.VHeroKeys.Handle(UIEvent)");
            }

            _harmony.Patch(
                heroKeysTarget,
                prefix: new HarmonyMethod(heroKeysPrefix));

            MethodInfo heroKeyBindingsTarget = AccessTools.PropertyGetter(
                typeof(VHeroKeys),
                nameof(VHeroKeys.PlayerKeyBindings));
            MethodInfo heroKeyBindingsPostfix = AccessTools.Method(
                typeof(VHeroKeysPlayerKeyBindingsPatch),
                nameof(VHeroKeysPlayerKeyBindingsPatch.Postfix));
            if (heroKeyBindingsTarget == null || heroKeyBindingsPostfix == null)
            {
                throw new MissingMethodException(
                    "Awaken.TG.Main.Heroes.VHeroKeys.PlayerKeyBindings");
            }

            _harmony.Patch(
                heroKeyBindingsTarget,
                postfix: new HarmonyMethod(heroKeyBindingsPostfix));
            _log.LogInfo(
                "Patched the remappable Take All Items action for hold-to-battlecry input.");

            MethodInfo roofEnterTarget = AccessTools.Method(
                typeof(VCRainChecker),
                "OnFirstVolumeEnter");
            MethodInfo roofEnterPostfix = AccessTools.Method(
                typeof(VCRainCheckerRoofEnterPatch),
                nameof(VCRainCheckerRoofEnterPatch.Postfix));
            MethodInfo roofExitTarget = AccessTools.Method(
                typeof(VCRainChecker),
                "OnAllVolumesExit");
            MethodInfo roofExitPostfix = AccessTools.Method(
                typeof(VCRainCheckerRoofExitPatch),
                nameof(VCRainCheckerRoofExitPatch.Postfix));
            MethodInfo roofDiscardTarget = AccessTools.Method(
                typeof(VCRainChecker),
                "OnDiscard");
            MethodInfo roofDiscardPostfix = AccessTools.Method(
                typeof(VCRainCheckerRoofExitPatch),
                nameof(VCRainCheckerRoofExitPatch.Postfix));
            if (roofEnterTarget == null
                || roofEnterPostfix == null
                || roofExitTarget == null
                || roofExitPostfix == null
                || roofDiscardTarget == null
                || roofDiscardPostfix == null)
            {
                throw new MissingMethodException(
                    "Awaken.TG.Main.Heroes.VolumeCheckers.VCRainChecker roof-volume callbacks");
            }

            _harmony.Patch(
                roofEnterTarget,
                postfix: new HarmonyMethod(roofEnterPostfix));
            _harmony.Patch(
                roofExitTarget,
                postfix: new HarmonyMethod(roofExitPostfix));
            _harmony.Patch(
                roofDiscardTarget,
                postfix: new HarmonyMethod(roofDiscardPostfix));
            _log.LogInfo(
                "Patched the game's Hero roof-volume state for dynamic battlecry reverb.");
        }

        private static void TuneCreatedEvent(EventDescription eventDescription, ref EventInstance eventInstance)
        {
            BattlecryVoiceTunerPlugin instance = _instance;
            if (instance != null)
            {
                instance.TryTuneEvent(eventDescription, ref eventInstance);
            }
        }

        private void TryTuneEvent(EventDescription eventDescription, ref EventInstance eventInstance)
        {
            if (_enabled == null || !_enabled.Value || !eventInstance.isValid())
            {
                return;
            }

            GUID eventGuid;
            RESULT idResult = eventDescription.getID(out eventGuid);
            if (idResult != RESULT.OK)
            {
                LogDiagnostic("Could not resolve FMOD event GUID. Result=" + idResult + ".");
                return;
            }

            SupportedVoiceEvent supportedEvent;
            if (!TryGetSupportedEvent(eventGuid, out supportedEvent) || !IsCategoryEnabled(supportedEvent.Category))
            {
                return;
            }

            float semitones = GetShiftedSemitones();
            float pitchMultiplier = SemitonesToPitchMultiplier(semitones);
            RESULT pitchResult = eventInstance.setPitch(pitchMultiplier);

            float volume = Math.Max(0.0f, _volumeMultiplier.Value);
            RESULT volumeResult = RESULT.OK;
            if (Math.Abs(volume - 1.0f) > 0.001f)
            {
                volumeResult = eventInstance.setVolume(volume);
            }

            LogDiagnostic(
                "Tuned " + supportedEvent.Label +
                " (" + supportedEvent.Path + ") semitones=" + semitones.ToString("0.00") +
                " pitch=" + pitchMultiplier.ToString("0.000") +
                " pitchResult=" + pitchResult +
                " volume=" + volume.ToString("0.00") +
                " volumeResult=" + volumeResult + ".");
        }

        private float GetShiftedSemitones(
            float baselineSemitones = 0.0f)
        {
            float semitones = baselineSemitones
                + _pitchSemitones.Value;
            float randomRange = Math.Max(0.0f, _randomPitchSemitones.Value);
            if (randomRange > 0.0f)
            {
                semitones += (float)((_random.NextDouble() * 2.0 - 1.0) * randomRange);
            }

            return Math.Max(-24.0f, Math.Min(24.0f, semitones));
        }

        private static float SemitonesToPitchMultiplier(float semitones)
        {
            return (float)Math.Pow(2.0, semitones / 12.0);
        }

        private bool IsCategoryEnabled(string category)
        {
            if (category == CategoryAttack)
            {
                return _includeAttackGrunts.Value;
            }

            if (category == CategoryHurt)
            {
                return _includeHurtGrunts.Value;
            }

            if (category == CategoryDeath)
            {
                return _includeDeathGrunts.Value;
            }

            if (category == CategoryStatus)
            {
                return _includeStatusPainGrunts.Value;
            }

            if (category == CategoryHitFeedback)
            {
                return _includePlayerHitFeedback.Value;
            }

            if (category == CategoryStamina)
            {
                return _includeStaminaDepletedBreathing.Value;
            }

            return false;
        }

        private static bool IsTestableCategory(string category)
        {
            return category != CategoryStamina;
        }

        private static bool TryGetSupportedEvent(GUID eventGuid, out SupportedVoiceEvent supportedEvent)
        {
            for (int index = 0; index < SupportedEvents.Length; index++)
            {
                if (SupportedEvents[index].Guid == eventGuid)
                {
                    supportedEvent = SupportedEvents[index];
                    return true;
                }
            }

            supportedEvent = null;
            return false;
        }

        private bool HandleTakeAllItemsInput(
            UIEvent inputEvent,
            ref UIResult result)
        {
            if (_enabled == null
                || !_enabled.Value
                || _battlecryEnabled == null
                || !_battlecryEnabled.Value
                || _holdTakeAllItemsForBattlecry == null
                || !_holdTakeAllItemsForBattlecry.Value)
            {
                return true;
            }

            UIKeyAction keyAction = inputEvent as UIKeyAction;
            if (keyAction == null
                || !String.Equals(
                    keyAction.Name,
                    KeyBindings.UI.Items.TransferItems,
                    StringComparison.Ordinal))
            {
                return true;
            }

            Hero hero = Hero.Current;
            if (hero == null || hero.HasBeenDiscarded || !hero.IsAlive)
            {
                ResetTakeAllItemsHold();
                return true;
            }

            if (inputEvent is UIKeyDownAction)
            {
                _takeAllItemsHeld = true;
                _battlecryAttemptedForHold = false;
                _takeAllItemsPressedAt = Time.unscaledTime;
                result = UIResult.Accept;
                return false;
            }

            if (!_takeAllItemsHeld)
            {
                return true;
            }

            if (inputEvent is UIKeyHeldAction)
            {
                float holdSeconds = _battlecryHoldSeconds == null
                    ? 0.45f
                    : Math.Max(0.2f, _battlecryHoldSeconds.Value);
                if (!_battlecryAttemptedForHold
                    && Time.unscaledTime - _takeAllItemsPressedAt
                        >= holdSeconds)
                {
                    _battlecryAttemptedForHold = true;
                    TryPerformBattlecry(
                        hero,
                        "held Take All Items action");
                }

                result = UIResult.Accept;
                return false;
            }

            if (inputEvent is UIKeyUpAction)
            {
                ResetTakeAllItemsHold();
                result = UIResult.Accept;
                return false;
            }

            return true;
        }

        private void ResetTakeAllItemsHold()
        {
            _takeAllItemsHeld = false;
            _battlecryAttemptedForHold = false;
            _takeAllItemsPressedAt = 0f;
        }

        private bool TryPerformBattlecry(Hero hero, string inputSource)
        {
            if (hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || Time.timeScale <= 0f)
            {
                return false;
            }

            float cooldown = _battlecryCooldownSeconds == null
                ? 3f
                : Math.Max(0f, _battlecryCooldownSeconds.Value);
            if (Time.time - _lastBattlecryTime < cooldown)
            {
                LogDiagnostic(
                    "Battlecry ignored during its cooldown; input="
                    + inputSource
                    + ".");
                return false;
            }

            string selectedPath;
            float pitch;
            if (!TryPlayBattlecry(hero, out selectedPath, out pitch))
            {
                if (!_noBattlecryFilesWarningLogged)
                {
                    _noBattlecryFilesWarningLogged = true;
                    _log.LogWarning(
                        "No playable battlecry WAV is available for the current player gender. Add one to audio\\battlecry\\male or audio\\battlecry\\female.");
                }
                return false;
            }

            _lastBattlecryTime = Time.time;
            _noBattlecryFilesWarningLogged = false;
            BeginChallenge(hero);
            NotifyEyesInTheDark();
            LogDiagnostic(
                "Battlecry performed from "
                + inputSource
                + "; file="
                + Path.GetFileName(selectedPath)
                + "; pitch="
                + pitch.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
            return true;
        }

        private void DiscoverBattlecryFiles()
        {
            _maleBattlecryPaths.Clear();
            _femaleBattlecryPaths.Clear();

            string pluginDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            if (String.IsNullOrEmpty(pluginDirectory))
            {
                return;
            }

            DiscoverBattlecryFiles(
                Path.Combine(
                    Path.Combine(
                        Path.Combine(pluginDirectory, "audio"),
                        "battlecry"),
                    "male"),
                _maleBattlecryPaths);
            DiscoverBattlecryFiles(
                Path.Combine(
                    Path.Combine(
                        Path.Combine(pluginDirectory, "audio"),
                        "battlecry"),
                    "female"),
                _femaleBattlecryPaths);

            _log.LogInfo(
                "Discovered battlecry WAV files: male="
                + _maleBattlecryPaths.Count.ToString(
                    CultureInfo.InvariantCulture)
                + "; female="
                + _femaleBattlecryPaths.Count.ToString(
                    CultureInfo.InvariantCulture)
                + ".");
        }

        private static void DiscoverBattlecryFiles(
            string directory,
            List<string> destination)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            string[] paths = Directory.GetFiles(
                directory,
                "*.wav",
                SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            int count = Math.Min(
                MaximumBattlecryFilesPerGender,
                paths.Length);
            for (int index = 0; index < count; index++)
            {
                destination.Add(paths[index]);
            }
        }

        private bool TryPlayBattlecry(
            Hero hero,
            out string selectedPath,
            out float pitch)
        {
            selectedPath = null;
            pitch = 1f;
            bool female = hero.GetGender() == Gender.Female;
            List<string> paths = female
                ? _femaleBattlecryPaths
                : _maleBattlecryPaths;
            if (paths.Count == 0)
            {
                DiscoverBattlecryFiles();
                paths = female
                    ? _femaleBattlecryPaths
                    : _maleBattlecryPaths;
            }
            if (paths.Count == 0)
            {
                return false;
            }

            int lastIndex = female
                ? _lastFemaleBattlecryIndex
                : _lastMaleBattlecryIndex;
            int firstIndex = PickBattlecryIndex(paths.Count, lastIndex);
            for (int offset = 0; offset < paths.Count; offset++)
            {
                int index = (firstIndex + offset) % paths.Count;
                float candidatePitch = SemitonesToPitchMultiplier(
                    GetShiftedSemitones(
                        female
                            ? _femaleBattlecryPitchOffsetSemitones.Value
                            : _maleBattlecryPitchOffsetSemitones.Value));
                if (!TryPlayBattlecrySound(
                    paths[index],
                    candidatePitch,
                    hero))
                {
                    continue;
                }

                if (female)
                {
                    _lastFemaleBattlecryIndex = index;
                }
                else
                {
                    _lastMaleBattlecryIndex = index;
                }

                selectedPath = paths[index];
                pitch = candidatePitch;
                return true;
            }

            return false;
        }

        private int PickBattlecryIndex(int count, int lastIndex)
        {
            if (count <= 1)
            {
                return 0;
            }

            int selected = _random.Next(count - 1);
            return selected >= lastIndex
                ? selected + 1
                : selected;
        }

        private bool TryPlayBattlecrySound(
            string path,
            float pitch,
            Hero hero)
        {
            try
            {
                FMOD.Sound sound;
                if (!_battlecrySoundsByPath.TryGetValue(
                    path,
                    out sound))
                {
                    RESULT createResult =
                        RuntimeManager.CoreSystem.createSound(
                            path,
                            MODE.DEFAULT
                                | MODE._2D
                                | MODE.CREATESAMPLE,
                            out sound);
                    if (createResult != RESULT.OK)
                    {
                        _log.LogWarning(
                            "FMOD could not load battlecry "
                            + path
                            + ": "
                            + createResult
                            + ".");
                        return false;
                    }

                    _battlecrySoundsByPath[path] = sound;
                }

                FMOD.ChannelGroup channelGroup;
                string environment;
                float reverbAmount;
                BattlecryAcousticProfile acousticProfile;
                if (!TryGetBattlecryChannelGroup(
                    hero,
                    out channelGroup,
                    out environment,
                    out reverbAmount,
                    out acousticProfile))
                {
                    return false;
                }

                FMOD.Channel channel;
                RESULT playResult = RuntimeManager.CoreSystem.playSound(
                    sound,
                    channelGroup,
                    true,
                    out channel);
                if (playResult != RESULT.OK)
                {
                    _log.LogWarning(
                        "FMOD could not play battlecry "
                        + path
                        + ": "
                        + playResult
                        + ".");
                    return false;
                }

                float volumeScale =
                    Math.Max(0f, _volumeMultiplier.Value)
                    * Math.Max(
                        0f,
                        _battlecryVolumeMultiplier.Value);
                RESULT volumeResult = channel.setVolume(volumeScale);
                RESULT pitchResult = channel.setPitch(
                    Math.Max(0.01f, pitch));
                RESULT unpauseResult = channel.setPaused(false);
                if (unpauseResult == RESULT.OK
                    && acousticProfile != null)
                {
                    ScheduleAcousticReflections(
                        sound,
                        pitch,
                        volumeScale,
                        acousticProfile);
                }
                LogDiagnostic(
                    "Battlecry FMOD results: volume="
                    + volumeResult
                    + " (scale="
                    + volumeScale.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + ", bus=SFX, environment="
                    + environment
                    + ", reverb="
                    + reverbAmount.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + ")"
                    + "; pitch="
                    + pitchResult
                    + "; unpause="
                    + unpauseResult
                    + ".");
                return unpauseResult == RESULT.OK;
            }
            catch (Exception exception)
            {
                _log.LogWarning(
                    "Battlecry playback failed for "
                    + path
                    + ": "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool TryGetBattlecryChannelGroup(
            Hero hero,
            out FMOD.ChannelGroup channelGroup,
            out string environment,
            out float reverbAmount,
            out BattlecryAcousticProfile acousticProfile)
        {
            acousticProfile = null;
            bool indoors = IsBattlecryIndoors(
                out environment);
            if (!TryGetBattlecrySfxChannelGroup(
                out channelGroup))
            {
                reverbAmount = 0f;
                return false;
            }

            reverbAmount = 0f;
            if (_battlecryReverbEnabled == null
                || !_battlecryReverbEnabled.Value)
            {
                return true;
            }

            float configuredAmount = Math.Max(
                0f,
                Math.Min(
                    1f,
                    indoors
                        ? _indoorBattlecryReverbAmount.Value
                        : _outdoorBattlecryReverbAmount.Value));
            reverbAmount = configuredAmount;
            if (configuredAmount > 0.001f)
            {
                BattlecryAcousticProfile measuredProfile;
                bool measured = indoors
                    ? TryProbeInteriorAcoustics(
                        hero,
                        configuredAmount,
                        environment,
                        out measuredProfile)
                    : TryProbeOutdoorAcoustics(
                        hero,
                        configuredAmount,
                        out measuredProfile);
                if (measured)
                {
                    acousticProfile = measuredProfile;
                    reverbAmount = measuredProfile.DiffuseReverbAmount;
                }
            }
            if (reverbAmount <= 0.001f)
            {
                reverbAmount = 0f;
                return true;
            }

            if (!TryEnsureBattlecryReverbPaths())
            {
                _log.LogWarning(
                    "Battlecry reverb setup failed; playing this battlecry dry through the game's SFX bus.");
                reverbAmount = 0f;
                return true;
            }

            FMOD.DSP reverb = indoors
                ? _indoorBattlecryReverb
                : _outdoorBattlecryReverb;
            if (!TryConfigureBattlecryReverb(
                reverb,
                reverbAmount,
                acousticProfile))
            {
                reverb.setBypass(true);
                _log.LogWarning(
                    "Battlecry reverb parameters could not be applied; playing this battlecry dry through the game's SFX bus.");
                reverbAmount = 0f;
                return true;
            }

            channelGroup = indoors
                ? _indoorBattlecryChannelGroup
                : _outdoorBattlecryChannelGroup;
            return true;
        }

        private bool IsBattlecryIndoors(
            out string environment)
        {
            SceneService sceneService =
                World.Services.TryGet<SceneService>();
            if (sceneService != null
                && !sceneService.IsOpenWorld)
            {
                environment = "interior";
                return true;
            }
            if (_heroUnderRoof)
            {
                environment = "roofed";
                return true;
            }

            environment = "outdoor";
            return false;
        }

        private bool TryProbeOutdoorAcoustics(
            Hero hero,
            float configuredAmount,
            out BattlecryAcousticProfile profile)
        {
            profile = null;
            if (hero == null
                || hero.HasBeenDiscarded
                || OutdoorProbeDirections.Length
                    != OutdoorProbeDirectionCount)
            {
                return false;
            }

            try
            {
                Vector3 origin = hero.Coords
                    + Vector3.up * OutdoorProbeHeight;
                List<AcousticReflectionCandidate> candidates =
                    new List<AcousticReflectionCandidate>();
                int hitCount = 0;
                float totalHitDistance = 0f;
                float totalSurfaceReflectivity = 0f;

                for (int index = 0;
                    index < OutdoorProbeDirections.Length;
                    index++)
                {
                    Vector3 direction =
                        OutdoorProbeDirections[index];
                    RaycastHit hit;
                    if (!Physics.Raycast(
                        origin,
                        direction,
                        out hit,
                        OutdoorProbeMaximumDistance,
                        OutdoorAcousticLayerMask,
                        QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    float reflectivity = GetAcousticSurfaceReflectivity(
                        hit.collider);
                    float facing = Mathf.Clamp01(
                        Vector3.Dot(hit.normal, -direction));
                    hitCount++;
                    totalHitDistance += hit.distance;
                    totalSurfaceReflectivity += reflectivity;
                    if (facing <= 0.1f || reflectivity <= 0.1f)
                    {
                        continue;
                    }

                    candidates.Add(
                        new AcousticReflectionCandidate
                        {
                            Direction = direction,
                            Position = hit.point
                                + hit.normal * 0.15f,
                            Distance = hit.distance,
                            Facing = facing,
                            Reflectivity = reflectivity,
                            Score = reflectivity
                                * facing
                                / (1f + hit.distance * 0.0125f)
                        });
                }

                float openness = 1f
                    - hitCount
                        / (float)OutdoorProbeDirections.Length;
                float averageDistance = hitCount <= 0
                    ? OutdoorProbeMaximumDistance
                    : totalHitDistance / hitCount;
                float averageReflectivity = hitCount <= 0
                    ? 1f
                    : totalSurfaceReflectivity / hitCount;
                float enclosure = 1f - openness;
                float diffuseAmount = Mathf.Clamp01(
                    configuredAmount
                    * Mathf.Lerp(0.12f, 1.35f, enclosure)
                    * Mathf.Lerp(
                        0.65f,
                        1f,
                        averageReflectivity));

                profile = new BattlecryAcousticProfile
                {
                    Origin = origin,
                    Environment = "outdoor",
                    Openness = openness,
                    AverageHitDistance = averageDistance,
                    AverageReflectivity = averageReflectivity,
                    DiffuseReverbAmount = diffuseAmount
                };

                candidates.Sort(
                    delegate(
                        AcousticReflectionCandidate left,
                        AcousticReflectionCandidate right)
                    {
                        return right.Score.CompareTo(left.Score);
                    });
                int tapLimit = openness > 0.75f
                    ? 1
                    : openness > 0.4f
                        ? 2
                        : MaximumOutdoorReflectionTaps;
                for (int candidateIndex = 0;
                    candidateIndex < candidates.Count
                        && profile.Reflections.Count < tapLimit;
                    candidateIndex++)
                {
                    AcousticReflectionCandidate candidate =
                        candidates[candidateIndex];
                    bool duplicatesDirection = false;
                    for (int selectedIndex = 0;
                        selectedIndex < profile.Reflections.Count;
                        selectedIndex++)
                    {
                        if (Vector3.Dot(
                            candidate.Direction,
                            profile.Reflections[selectedIndex].Direction)
                            > OutdoorReflectionDirectionSeparation)
                        {
                            duplicatesDirection = true;
                            break;
                        }
                    }
                    if (duplicatesDirection)
                    {
                        continue;
                    }

                    float distanceRatio = Mathf.Clamp01(
                        candidate.Distance
                            / OutdoorProbeMaximumDistance);
                    profile.Reflections.Add(
                        new AcousticReflectionTap
                        {
                            Direction = candidate.Direction,
                            Position = candidate.Position,
                            Distance = candidate.Distance,
                            DelaySeconds = Mathf.Clamp(
                                2f * candidate.Distance
                                    / OutdoorReflectionSpeedOfSound,
                                0.018f,
                                0.65f),
                            Gain = Mathf.Clamp(
                                configuredAmount
                                    * candidate.Reflectivity
                                    * candidate.Facing
                                    * Mathf.Lerp(
                                        0.9f,
                                        0.45f,
                                        distanceRatio),
                                0f,
                                0.35f),
                            LowPassGain = Mathf.Clamp(
                                0.35f
                                    + 0.55f
                                        * candidate.Reflectivity
                                    - 0.25f * distanceRatio,
                                0.2f,
                                0.9f)
                        });
                }

                LogDiagnostic(
                    "Outdoor battlecry acoustics: openness="
                    + openness.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + "; hits="
                    + hitCount.ToString(
                        CultureInfo.InvariantCulture)
                    + "/"
                    + OutdoorProbeDirections.Length.ToString(
                        CultureInfo.InvariantCulture)
                    + "; averageDistance="
                    + averageDistance.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "m; diffuse="
                    + diffuseAmount.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + "; reflections="
                    + profile.Reflections.Count.ToString(
                        CultureInfo.InvariantCulture)
                    + ".");
                return true;
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Outdoor acoustic probe failed; using the fixed outdoor reverb fallback. "
                    + exception.GetBaseException().Message);
                profile = null;
                return false;
            }
        }

        private bool TryProbeInteriorAcoustics(
            Hero hero,
            float configuredAmount,
            string environment,
            out BattlecryAcousticProfile profile)
        {
            profile = null;
            if (hero == null
                || hero.HasBeenDiscarded
                || InteriorProbeDirections.Length
                    != InteriorProbeDirectionCount)
            {
                return false;
            }

            try
            {
                Vector3 origin = hero.Coords
                    + Vector3.up * InteriorProbeHeight;
                List<AcousticReflectionCandidate> candidates =
                    new List<AcousticReflectionCandidate>();
                int hitCount = 0;
                int roughSurfaceHitCount = 0;
                int horizontalHitCount = 0;
                float totalHitDistance = 0f;
                float totalSquaredHitDistance = 0f;
                float totalSurfaceReflectivity = 0f;
                float minimumHorizontalDistance =
                    InteriorProbeMaximumDistance;
                float maximumHorizontalDistance = 0f;
                float ceilingDistance =
                    InteriorProbeMaximumDistance;

                for (int index = 0;
                    index < InteriorProbeDirections.Length;
                    index++)
                {
                    Vector3 direction =
                        InteriorProbeDirections[index];
                    RaycastHit hit;
                    if (!Physics.Raycast(
                        origin,
                        direction,
                        out hit,
                        InteriorProbeMaximumDistance,
                        OutdoorAcousticLayerMask,
                        QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    float reflectivity = GetAcousticSurfaceReflectivity(
                        hit.collider);
                    float facing = Mathf.Clamp01(
                        Vector3.Dot(hit.normal, -direction));
                    hitCount++;
                    totalHitDistance += hit.distance;
                    totalSquaredHitDistance +=
                        hit.distance * hit.distance;
                    totalSurfaceReflectivity += reflectivity;
                    if (reflectivity <= 0.61f)
                    {
                        roughSurfaceHitCount++;
                    }
                    if (index < 12)
                    {
                        horizontalHitCount++;
                        minimumHorizontalDistance = Math.Min(
                            minimumHorizontalDistance,
                            hit.distance);
                        maximumHorizontalDistance = Math.Max(
                            maximumHorizontalDistance,
                            hit.distance);
                    }
                    if (index == InteriorProbeDirectionCount - 2)
                    {
                        ceilingDistance = hit.distance;
                    }
                    if (facing <= 0.1f || reflectivity <= 0.1f)
                    {
                        continue;
                    }

                    candidates.Add(
                        new AcousticReflectionCandidate
                        {
                            Direction = direction,
                            Position = hit.point
                                + hit.normal * 0.15f,
                            Distance = hit.distance,
                            Facing = facing,
                            Reflectivity = reflectivity,
                            Score = reflectivity
                                * facing
                                * Mathf.Lerp(
                                    0.7f,
                                    1f,
                                    Mathf.Clamp01(
                                        hit.distance
                                            / InteriorProbeMaximumDistance))
                        });
                }

                int minimumUsefulHits = String.Equals(
                    environment,
                    "interior",
                    StringComparison.Ordinal)
                        ? 6
                        : 3;
                if (hitCount < minimumUsefulHits)
                {
                    LogDiagnostic(
                        "Interior acoustic probe found only "
                        + hitCount.ToString(
                            CultureInfo.InvariantCulture)
                        + " useful surface(s); using the fixed "
                        + environment
                        + " reverb fallback.");
                    return false;
                }

                float openness = 1f
                    - hitCount
                        / (float)InteriorProbeDirections.Length;
                float averageDistance =
                    totalHitDistance / hitCount;
                float averageReflectivity =
                    totalSurfaceReflectivity / hitCount;
                float distanceVariance = Mathf.Sqrt(
                    Math.Max(
                        0f,
                        totalSquaredHitDistance / hitCount
                            - averageDistance * averageDistance));
                float horizontalAnisotropy =
                    horizontalHitCount < 2
                        || minimumHorizontalDistance <= 0.1f
                            ? 1f
                            : maximumHorizontalDistance
                                / minimumHorizontalDistance;
                float roughSurfaceRatio =
                    roughSurfaceHitCount / (float)hitCount;
                string spaceKind;
                if (String.Equals(
                    environment,
                    "roofed",
                    StringComparison.Ordinal)
                    && openness >= 0.35f)
                {
                    spaceKind = "open-roof";
                }
                else if (horizontalAnisotropy >= 3f
                    && maximumHorizontalDistance >= 14f)
                {
                    spaceKind = "corridor";
                }
                else if (averageDistance >= 18f
                    || ceilingDistance >= 14f)
                {
                    spaceKind = roughSurfaceRatio >= 0.4f
                        ? "large-cavern"
                        : "large-hall";
                }
                else if (averageDistance <= 6f
                    && ceilingDistance <= 6f)
                {
                    spaceKind = "small-room";
                }
                else
                {
                    spaceKind = roughSurfaceRatio >= 0.45f
                        ? "cave"
                        : "medium-room";
                }

                float enclosure = 1f - openness;
                float roomScale = Mathf.Clamp01(
                    averageDistance / 25f);
                float diffuseAmount = Mathf.Clamp01(
                    configuredAmount
                    * Mathf.Lerp(0.55f, 1.1f, roomScale)
                    * Mathf.Lerp(0.7f, 1.15f, enclosure)
                    * Mathf.Lerp(
                        0.85f,
                        1.05f,
                        averageReflectivity));
                if (String.Equals(
                    spaceKind,
                    "open-roof",
                    StringComparison.Ordinal))
                {
                    diffuseAmount *= 0.8f;
                }

                profile = new BattlecryAcousticProfile
                {
                    Origin = origin,
                    Environment = environment,
                    SpaceKind = spaceKind,
                    IsInterior = true,
                    Openness = openness,
                    AverageHitDistance = averageDistance,
                    AverageReflectivity = averageReflectivity,
                    CeilingDistance = ceilingDistance,
                    DistanceVariance = distanceVariance,
                    HorizontalAnisotropy = horizontalAnisotropy,
                    DiffuseReverbAmount = diffuseAmount
                };

                candidates.Sort(
                    delegate(
                        AcousticReflectionCandidate left,
                        AcousticReflectionCandidate right)
                    {
                        return right.Score.CompareTo(left.Score);
                    });
                int tapLimit = String.Equals(
                    spaceKind,
                    "small-room",
                    StringComparison.Ordinal)
                        ? 0
                        : String.Equals(
                            spaceKind,
                            "medium-room",
                            StringComparison.Ordinal)
                            || String.Equals(
                                spaceKind,
                                "open-roof",
                                StringComparison.Ordinal)
                                ? 2
                                : MaximumOutdoorReflectionTaps;
                for (int candidateIndex = 0;
                    candidateIndex < candidates.Count
                        && profile.Reflections.Count < tapLimit;
                    candidateIndex++)
                {
                    AcousticReflectionCandidate candidate =
                        candidates[candidateIndex];
                    float delaySeconds =
                        2f * candidate.Distance
                            / OutdoorReflectionSpeedOfSound;
                    if (delaySeconds
                        < InteriorDiscreteReflectionMinimumDelay)
                    {
                        continue;
                    }

                    bool duplicatesDirection = false;
                    for (int selectedIndex = 0;
                        selectedIndex < profile.Reflections.Count;
                        selectedIndex++)
                    {
                        if (Vector3.Dot(
                            candidate.Direction,
                            profile.Reflections[selectedIndex].Direction)
                            > OutdoorReflectionDirectionSeparation)
                        {
                            duplicatesDirection = true;
                            break;
                        }
                    }
                    if (duplicatesDirection)
                    {
                        continue;
                    }

                    float distanceRatio = Mathf.Clamp01(
                        candidate.Distance
                            / InteriorProbeMaximumDistance);
                    profile.Reflections.Add(
                        new AcousticReflectionTap
                        {
                            Direction = candidate.Direction,
                            Position = candidate.Position,
                            Distance = candidate.Distance,
                            DelaySeconds = Mathf.Clamp(
                                delaySeconds,
                                InteriorDiscreteReflectionMinimumDelay,
                                0.5f),
                            Gain = Mathf.Clamp(
                                configuredAmount
                                    * candidate.Reflectivity
                                    * candidate.Facing
                                    * Mathf.Lerp(
                                        0.22f,
                                        0.14f,
                                        distanceRatio),
                                0f,
                                0.22f),
                            LowPassGain = Mathf.Clamp(
                                0.3f
                                    + 0.6f
                                        * candidate.Reflectivity
                                    - 0.2f * distanceRatio,
                                0.2f,
                                0.9f)
                        });
                }

                LogDiagnostic(
                    "Interior battlecry acoustics: environment="
                    + environment
                    + "; space="
                    + spaceKind
                    + "; openness="
                    + openness.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + "; hits="
                    + hitCount.ToString(
                        CultureInfo.InvariantCulture)
                    + "/"
                    + InteriorProbeDirections.Length.ToString(
                        CultureInfo.InvariantCulture)
                    + "; averageDistance="
                    + averageDistance.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "m; ceiling="
                    + ceilingDistance.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "m; spread="
                    + distanceVariance.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "m; anisotropy="
                    + horizontalAnisotropy.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "; diffuse="
                    + diffuseAmount.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + "; reflections="
                    + profile.Reflections.Count.ToString(
                        CultureInfo.InvariantCulture)
                    + ".");
                return true;
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Interior acoustic probe failed; using the fixed "
                    + environment
                    + " reverb fallback. "
                    + exception.GetBaseException().Message);
                profile = null;
                return false;
            }
        }

        private static float GetAcousticSurfaceReflectivity(
            Collider collider)
        {
            if (collider == null)
            {
                return 0.5f;
            }

            int layer = collider.gameObject.layer;
            if (layer == RenderLayers.Vegetation)
            {
                return 0.3f;
            }
            if (layer == RenderLayers.Terrain
                || layer == RenderLayers.Walkable)
            {
                return 0.6f;
            }

            return 0.9f;
        }

        private void ScheduleAcousticReflections(
            FMOD.Sound sound,
            float pitch,
            float directVolumeScale,
            BattlecryAcousticProfile profile)
        {
            if (profile.Reflections.Count == 0
                || !_battlecrySfxChannelGroup.hasHandle())
            {
                return;
            }

            int sampleRate;
            SPEAKERMODE speakerMode;
            int rawSpeakerCount;
            RESULT formatResult =
                RuntimeManager.CoreSystem.getSoftwareFormat(
                    out sampleRate,
                    out speakerMode,
                    out rawSpeakerCount);
            if (formatResult != RESULT.OK || sampleRate <= 0)
            {
                LogDiagnostic(
                    "Battlecry reflections skipped because FMOD's output sample rate was unavailable. Result="
                    + formatResult
                    + ".");
                return;
            }

            int scheduled = 0;
            for (int index = 0;
                index < profile.Reflections.Count;
                index++)
            {
                AcousticReflectionTap tap =
                    profile.Reflections[index];
                if (tap.Gain <= 0.001f)
                {
                    continue;
                }

                FMOD.Channel reflectionChannel;
                RESULT playResult =
                    RuntimeManager.CoreSystem.playSound(
                        sound,
                        _battlecrySfxChannelGroup,
                        true,
                        out reflectionChannel);
                if (playResult != RESULT.OK)
                {
                    continue;
                }

                FMOD.VECTOR position =
                    tap.Position.ToFMODVector();
                FMOD.VECTOR velocity =
                    Vector3.zero.ToFMODVector();
                ulong channelClock = 0UL;
                ulong parentClock = 0UL;
                RESULT result = reflectionChannel.setMode(
                    MODE._3D
                        | MODE._3D_WORLDRELATIVE
                        | MODE._3D_LINEARROLLOFF);
                if (result == RESULT.OK)
                {
                    result = reflectionChannel.set3DAttributes(
                        ref position,
                        ref velocity);
                }
                if (result == RESULT.OK)
                {
                    result = reflectionChannel.set3DMinMaxDistance(
                        2f,
                        (profile.IsInterior
                            ? InteriorProbeMaximumDistance
                            : OutdoorProbeMaximumDistance)
                            * 1.5f);
                }
                if (result == RESULT.OK)
                {
                    result = reflectionChannel.setVolume(
                        directVolumeScale * tap.Gain);
                }
                if (result == RESULT.OK)
                {
                    result = reflectionChannel.setPitch(
                        Math.Max(0.01f, pitch));
                }
                if (result == RESULT.OK)
                {
                    result = reflectionChannel.setLowPassGain(
                        tap.LowPassGain);
                }
                if (result == RESULT.OK)
                {
                    result = reflectionChannel.getDSPClock(
                        out channelClock,
                        out parentClock);
                }
                if (result == RESULT.OK)
                {
                    ulong delaySamples = (ulong)Math.Max(
                        1d,
                        tap.DelaySeconds * sampleRate);
                    result = reflectionChannel.setDelay(
                        parentClock + delaySamples,
                        0UL,
                        true);
                }
                if (result == RESULT.OK)
                {
                    result = reflectionChannel.setPaused(false);
                }

                if (result == RESULT.OK)
                {
                    scheduled++;
                    LogDiagnostic(
                        "Scheduled "
                        + profile.Environment
                        + " "
                        + (String.IsNullOrEmpty(profile.SpaceKind)
                            ? String.Empty
                            : profile.SpaceKind + " ")
                        + "reflection: distance="
                        + tap.Distance.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture)
                        + "m; delay="
                        + (tap.DelaySeconds * 1000f).ToString(
                            "0",
                            CultureInfo.InvariantCulture)
                        + "ms; gain="
                        + tap.Gain.ToString(
                            "0.00",
                            CultureInfo.InvariantCulture)
                        + "; lowPass="
                        + tap.LowPassGain.ToString(
                            "0.00",
                            CultureInfo.InvariantCulture)
                        + ".");
                }
                else
                {
                    reflectionChannel.stop();
                    LogDiagnostic(
                        "Battlecry reflection setup failed. Result="
                        + result
                        + ".");
                }
            }

            LogDiagnostic(
                "Scheduled "
                + scheduled.ToString(CultureInfo.InvariantCulture)
                + " "
                + profile.Environment
                + " battlecry reflection tap(s).");
        }

        private bool TryGetBattlecrySfxChannelGroup(
            out FMOD.ChannelGroup channelGroup)
        {
            if (_battlecrySfxBusLocked
                && _battlecrySfxChannelGroup.hasHandle())
            {
                channelGroup = _battlecrySfxChannelGroup;
                return true;
            }

            ReleaseBattlecrySfxBus();

            FMOD.Studio.Bus sfxBus;
            if (!BusGroup.SFX.TryGetBus(out sfxBus))
            {
                _log.LogWarning(
                    "FMOD could not resolve the game's SFX bus for battlecry playback.");
                channelGroup = default(FMOD.ChannelGroup);
                return false;
            }

            RESULT lockResult = sfxBus.lockChannelGroup();
            if (lockResult != RESULT.OK)
            {
                _log.LogWarning(
                    "FMOD could not lock the game's SFX bus channel group for battlecry playback: "
                    + lockResult
                    + ".");
                channelGroup = default(FMOD.ChannelGroup);
                return false;
            }

            RESULT groupResult = sfxBus.getChannelGroup(
                out channelGroup);
            if (groupResult != RESULT.OK
                || !channelGroup.hasHandle())
            {
                sfxBus.unlockChannelGroup();
                _log.LogWarning(
                    "FMOD could not access the game's SFX bus channel group for battlecry playback: "
                    + groupResult
                    + ".");
                channelGroup = default(FMOD.ChannelGroup);
                return false;
            }

            _battlecrySfxBus = sfxBus;
            _battlecrySfxChannelGroup = channelGroup;
            _battlecrySfxBusLocked = true;
            LogDiagnostic(
                "Battlecry playback connected to the game's SFX bus.");
            return true;
        }

        private bool TryEnsureBattlecryReverbPaths()
        {
            if (_outdoorBattlecryChannelGroup.hasHandle()
                && _indoorBattlecryChannelGroup.hasHandle()
                && _outdoorBattlecryReverb.hasHandle()
                && _indoorBattlecryReverb.hasHandle())
            {
                return true;
            }

            ReleaseBattlecryReverbPaths();
            if (!TryCreateBattlecryReverbPath(
                    "Battlecry Voice Tuner - Outdoor",
                    out _outdoorBattlecryChannelGroup,
                    out _outdoorBattlecryReverb)
                || !TryCreateBattlecryReverbPath(
                    "Battlecry Voice Tuner - Indoor",
                    out _indoorBattlecryChannelGroup,
                    out _indoorBattlecryReverb))
            {
                ReleaseBattlecryReverbPaths();
                return false;
            }

            LogDiagnostic(
                "Created reusable outdoor and indoor battlecry reverb paths under the game's SFX bus.");
            return true;
        }

        private bool TryCreateBattlecryReverbPath(
            string name,
            out FMOD.ChannelGroup channelGroup,
            out FMOD.DSP reverb)
        {
            channelGroup = default(FMOD.ChannelGroup);
            reverb = default(FMOD.DSP);

            RESULT groupCreateResult =
                RuntimeManager.CoreSystem.createChannelGroup(
                    name,
                    out channelGroup);
            if (groupCreateResult != RESULT.OK)
            {
                _log.LogWarning(
                    "FMOD could not create the "
                    + name
                    + " channel group: "
                    + groupCreateResult
                    + ".");
                return false;
            }

            RESULT attachResult =
                _battlecrySfxChannelGroup.addGroup(
                    channelGroup,
                    true);
            if (attachResult != RESULT.OK)
            {
                channelGroup.release();
                channelGroup = default(FMOD.ChannelGroup);
                _log.LogWarning(
                    "FMOD could not attach the "
                    + name
                    + " channel group to the game's SFX bus: "
                    + attachResult
                    + ".");
                return false;
            }

            RESULT dspCreateResult =
                RuntimeManager.CoreSystem.createDSPByType(
                    DSP_TYPE.SFXREVERB,
                    out reverb);
            if (dspCreateResult != RESULT.OK)
            {
                channelGroup.release();
                channelGroup = default(FMOD.ChannelGroup);
                _log.LogWarning(
                    "FMOD could not create the "
                    + name
                    + " reverb DSP: "
                    + dspCreateResult
                    + ".");
                return false;
            }

            RESULT addDspResult = channelGroup.addDSP(
                0,
                reverb);
            if (addDspResult != RESULT.OK)
            {
                reverb.release();
                channelGroup.release();
                reverb = default(FMOD.DSP);
                channelGroup = default(FMOD.ChannelGroup);
                _log.LogWarning(
                    "FMOD could not attach reverb to the "
                    + name
                    + " channel group: "
                    + addDspResult
                    + ".");
                return false;
            }

            reverb.setActive(true);
            return true;
        }

        private bool TryConfigureBattlecryReverb(
            FMOD.DSP reverb,
            float amount,
            BattlecryAcousticProfile acousticProfile)
        {
            if (!reverb.hasHandle())
            {
                return false;
            }

            float decayMilliseconds;
            float earlyDelayMilliseconds;
            float lateDelayMilliseconds;
            float highFrequencyDecayRatio;
            float diffusion;
            float density;
            float highCutHertz;
            float earlyLateMix;
            if (acousticProfile == null)
            {
                decayMilliseconds = 600f
                    + 2400f * amount;
                earlyDelayMilliseconds = 2f
                    + 18f * amount;
                lateDelayMilliseconds = 8f
                    + 42f * amount;
                highFrequencyDecayRatio = 80f
                    - 25f * amount;
                diffusion = 60f + 40f * amount;
                density = 55f + 45f * amount;
                highCutHertz = 14000f
                    - 7000f * amount;
                earlyLateMix = 25f + 50f * amount;
            }
            else if (acousticProfile.IsInterior)
            {
                float enclosure = 1f
                    - acousticProfile.Openness;
                float roomScale = Mathf.Clamp01(
                    acousticProfile.AverageHitDistance / 25f);
                float ceilingScale = Mathf.Clamp01(
                    acousticProfile.CeilingDistance / 20f);
                bool cavern = String.Equals(
                    acousticProfile.SpaceKind,
                    "cave",
                    StringComparison.Ordinal)
                    || String.Equals(
                        acousticProfile.SpaceKind,
                        "large-cavern",
                        StringComparison.Ordinal);
                decayMilliseconds = Mathf.Clamp(
                    (500f
                        + 2600f * roomScale
                        + 800f * ceilingScale)
                        * Mathf.Lerp(0.75f, 1f, enclosure)
                        + (cavern ? 350f : 0f),
                    450f,
                    4800f);
                earlyDelayMilliseconds = Mathf.Clamp(
                    1f
                        + acousticProfile.AverageHitDistance
                            * 0.45f,
                    2f,
                    30f);
                lateDelayMilliseconds = Mathf.Clamp(
                    4f
                        + acousticProfile.AverageHitDistance
                            * 1.5f,
                    8f,
                    100f);
                highFrequencyDecayRatio = Mathf.Lerp(
                    cavern ? 45f : 55f,
                    88f,
                    acousticProfile.AverageReflectivity);
                diffusion = Mathf.Clamp(
                    Mathf.Lerp(72f, 98f, enclosure)
                        - (cavern ? 10f : 0f),
                    45f,
                    98f);
                density = Mathf.Clamp(
                    Mathf.Lerp(65f, 100f, enclosure)
                        - (cavern ? 8f : 0f),
                    45f,
                    100f);
                highCutHertz = Mathf.Lerp(
                    cavern ? 4500f : 6000f,
                    13500f,
                    acousticProfile.AverageReflectivity);
                earlyLateMix = Mathf.Clamp(
                    Mathf.Lerp(35f, 75f, roomScale)
                        + Mathf.Clamp(
                            acousticProfile.HorizontalAnisotropy
                                - 2f,
                            0f,
                            3f)
                            * 3f,
                    30f,
                    85f);
            }
            else
            {
                float enclosure = 1f
                    - acousticProfile.Openness;
                float distanceRatio = Mathf.Clamp01(
                    acousticProfile.AverageHitDistance
                        / OutdoorProbeMaximumDistance);
                decayMilliseconds = 350f
                    + 1800f
                        * enclosure
                        * Mathf.Sqrt(distanceRatio);
                earlyDelayMilliseconds = Mathf.Clamp(
                    2f
                        + acousticProfile.AverageHitDistance
                            * 0.35f,
                    2f,
                    25f);
                lateDelayMilliseconds = Mathf.Clamp(
                    10f
                        + acousticProfile.AverageHitDistance
                            * 0.8f,
                    10f,
                    80f);
                highFrequencyDecayRatio = Mathf.Lerp(
                    55f,
                    85f,
                    acousticProfile.AverageReflectivity);
                diffusion = Mathf.Lerp(
                    45f,
                    90f,
                    enclosure);
                density = Mathf.Lerp(
                    40f,
                    92f,
                    enclosure);
                highCutHertz = Mathf.Lerp(
                    6500f,
                    14000f,
                    acousticProfile.AverageReflectivity);
                earlyLateMix = Mathf.Lerp(
                    35f,
                    65f,
                    enclosure);
            }
            float wetLevelDecibels = -24f
                + 24f * amount;

            RESULT result = reverb.setParameterFloat(
                (int)DSP_SFXREVERB.DECAYTIME,
                decayMilliseconds);
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.EARLYDELAY,
                    earlyDelayMilliseconds);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.LATEDELAY,
                    lateDelayMilliseconds);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.HFDECAYRATIO,
                    highFrequencyDecayRatio);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.DIFFUSION,
                    diffusion);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.DENSITY,
                    density);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.HIGHCUT,
                    highCutHertz);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.EARLYLATEMIX,
                    earlyLateMix);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.WETLEVEL,
                    wetLevelDecibels);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setParameterFloat(
                    (int)DSP_SFXREVERB.DRYLEVEL,
                    0f);
            }
            if (result == RESULT.OK)
            {
                result = reverb.setBypass(false);
            }

            return result == RESULT.OK;
        }

        private void ReleaseBattlecryReverbPaths()
        {
            ReleaseBattlecryReverbPath(
                ref _outdoorBattlecryChannelGroup,
                ref _outdoorBattlecryReverb);
            ReleaseBattlecryReverbPath(
                ref _indoorBattlecryChannelGroup,
                ref _indoorBattlecryReverb);
        }

        private static void ReleaseBattlecryReverbPath(
            ref FMOD.ChannelGroup channelGroup,
            ref FMOD.DSP reverb)
        {
            if (channelGroup.hasHandle()
                && reverb.hasHandle())
            {
                channelGroup.removeDSP(reverb);
            }
            if (reverb.hasHandle())
            {
                reverb.release();
            }
            if (channelGroup.hasHandle())
            {
                channelGroup.release();
            }

            reverb = default(FMOD.DSP);
            channelGroup = default(FMOD.ChannelGroup);
        }

        private void ReleaseBattlecrySfxBus()
        {
            ReleaseBattlecryReverbPaths();
            _battlecrySfxChannelGroup =
                default(FMOD.ChannelGroup);
            if (_battlecrySfxBusLocked
                && _battlecrySfxBus.isValid())
            {
                RESULT unlockResult =
                    _battlecrySfxBus.unlockChannelGroup();
                LogDiagnostic(
                    "Battlecry SFX bus unlock result="
                    + unlockResult
                    + ".");
            }

            _battlecrySfxBus = default(FMOD.Studio.Bus);
            _battlecrySfxBusLocked = false;
        }

        private void ReleaseBattlecrySounds()
        {
            foreach (KeyValuePair<string, FMOD.Sound> pair
                in _battlecrySoundsByPath)
            {
                try
                {
                    pair.Value.release();
                }
                catch
                {
                }
            }

            _battlecrySoundsByPath.Clear();
            ReleaseBattlecrySfxBus();
            EndChallenge();
        }

        private void BeginChallenge(Hero hero)
        {
            _challengeHero = hero;
            _challengedNpcs.Clear();
            _nextChallengeScanAt = Time.time;
            _challengeEndsAt = Time.time
                + Math.Max(
                    0.1f,
                    _battlecryAggroDurationSeconds.Value);
            AdvanceChallenge();
        }

        private void AdvanceChallenge()
        {
            Hero hero = _challengeHero;
            if (hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || Time.time > _challengeEndsAt)
            {
                EndChallenge();
                return;
            }
            if (Time.timeScale <= 0f
                || Time.time < _nextChallengeScanAt)
            {
                return;
            }

            _nextChallengeScanAt = Time.time
                + ChallengeScanIntervalSeconds;
            ChallengeNearbyEnemies(hero);
        }

        private void EndChallenge()
        {
            _challengeHero = null;
            _challengeEndsAt = 0f;
            _nextChallengeScanAt = 0f;
            _challengedNpcs.Clear();
        }

        private void ChallengeNearbyEnemies(Hero hero)
        {
            string environment;
            bool indoors = IsBattlecryIndoors(
                out environment);
            float rangeMultiplier = Math.Max(
                0f,
                indoors
                    ? _indoorBattlecryAggroRangeMultiplier.Value
                    : _battlecryAggroRangeMultiplier.Value);
            if (rangeMultiplier <= 0f)
            {
                return;
            }

            List<NpcAI> workingAi = NpcAI.AllWorkingAI;
            for (int index = workingAi.Count - 1;
                index >= 0;
                index--)
            {
                if (index >= workingAi.Count)
                {
                    continue;
                }

                NpcAI ai = workingAi[index];
                NpcElement npc = ai == null
                    ? null
                    : ai.NpcElement;
                if (ai == null
                    || npc == null
                    || _challengedNpcs.Contains(ai)
                    || !ai.Working
                    || ai.InCombat
                    || npc.HasBeenDiscarded
                    || !npc.IsAlive
                    || npc.IsHeroSummon
                    || !npc.IsHostileToHero()
                    || ai.Data == null)
                {
                    continue;
                }

                float hearingRange =
                    ai.Data.perception.MaxHearingRange
                    * npc.NpcStats.Hearing
                    * rangeMultiplier;
                if (hearingRange <= 0f
                    || (npc.Coords - hero.Coords).sqrMagnitude
                        > hearingRange * hearingRange)
                {
                    continue;
                }

                float wallThickness;
                if (AINoises.BlockedByWalls(
                    ai,
                    false,
                    hero.Coords,
                    out wallThickness))
                {
                    continue;
                }

                bool wasAlert = ai.InAlert || ai.AlertValue > 0f;
                _challengedNpcs.Add(ai);
                if (wasAlert)
                {
                    ai.EnterCombatWith(hero);
                }
                else
                {
                    ai.AlertStack.NewPoi(
                        AlertStack.AlertStrength.Strong,
                        hero);
                }

                LogDiagnostic(
                    "Battlecry challenged NPC "
                    + npc.ID
                    + "; priorAlert="
                    + wasAlert
                    + "; range="
                    + hearingRange.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "; environment="
                    + environment
                    + ".");
            }
        }

        private void NotifyEyesInTheDark()
        {
            if (!_eyesApiResolved)
            {
                _eyesApiResolved = true;
                Type apiType = Type.GetType(
                    EyesInTheDarkApiTypeName,
                    false);
                _eyesBattlecryMethod = apiType == null
                    ? null
                    : apiType.GetMethod(
                        "TryRegisterBattlecry",
                        BindingFlags.Public
                            | BindingFlags.Static,
                        null,
                        new Type[] { typeof(float) },
                        null);
            }

            if (_eyesBattlecryMethod == null)
            {
                return;
            }

            try
            {
                float amount = _eyesInTheDarkThreat == null
                    ? 10f
                    : Math.Max(0f, _eyesInTheDarkThreat.Value);
                object accepted = _eyesBattlecryMethod.Invoke(
                    null,
                    new object[] { amount });
                LogDiagnostic(
                    "Eyes in the Dark battlecry request accepted="
                    + (accepted is bool && (bool)accepted)
                    + "; requestedThreat="
                    + amount.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + ".");
            }
            catch (Exception exception)
            {
                _log.LogWarning(
                    "Eyes in the Dark battlecry integration failed: "
                    + exception.GetBaseException().Message);
            }
        }

        private static bool IsShortcutDown(
            KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None
                || !Input.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnPlayRandomTestSoundChanged(object sender, EventArgs e)
        {
            if (_resettingTestButton || _playRandomTestSound == null || !_playRandomTestSound.Value)
            {
                return;
            }

            try
            {
                PlayRandomTestSound();
            }
            catch (Exception ex)
            {
                _log.LogWarning("Could not play random " + PluginName + " test sound: " + ex.Message);
            }
            finally
            {
                ResetTestButton();
            }
        }

        private void ResetTestButton()
        {
            _resettingTestButton = true;
            try
            {
                _playRandomTestSound.Value = false;
                Config.Save();
            }
            finally
            {
                _resettingTestButton = false;
            }
        }

        private void PlayRandomTestSound()
        {
            List<SupportedVoiceEvent> candidates = new List<SupportedVoiceEvent>();
            for (int index = 0; index < SupportedEvents.Length; index++)
            {
                SupportedVoiceEvent supportedEvent = SupportedEvents[index];
                if (IsTestableCategory(supportedEvent.Category) && IsCategoryEnabled(supportedEvent.Category))
                {
                    candidates.Add(supportedEvent);
                }
            }

            if (candidates.Count == 0)
            {
                _log.LogWarning("No enabled one-shot " + PluginName + " sounds are available for the random test button.");
                return;
            }

            SupportedVoiceEvent selected = candidates[_random.Next(candidates.Count)];
            EventInstance eventInstance;
            if (!RuntimeManager.TryCreateInstance(selected.Guid, out eventInstance) || !eventInstance.isValid())
            {
                _log.LogWarning("Could not create random " + PluginName + " test sound: " + selected.Path + ".");
                return;
            }

            RESULT startResult = eventInstance.start();
            RuntimeManager.ReleaseInstance(eventInstance);

            if (startResult == RESULT.OK)
            {
                _log.LogInfo("Played random " + PluginName + " test sound: " + selected.Label + " (" + selected.Path + ").");
            }
            else
            {
                _log.LogWarning("Random " + PluginName + " test sound failed to start: " + selected.Path + ". Result=" + startResult + ".");
            }
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                _log.LogInfo(message);
            }
        }

        private static class VHeroKeysHandlePatch
        {
            internal static bool Prefix(
                UIEvent evt,
                ref UIResult __result)
            {
                BattlecryVoiceTunerPlugin instance = _instance;
                return instance == null
                    || instance.HandleTakeAllItemsInput(
                        evt,
                        ref __result);
            }
        }

        private static class VHeroKeysPlayerKeyBindingsPatch
        {
            internal static void Postfix(
                ref IEnumerable<KeyBindings> __result)
            {
                __result = AppendTakeAllItemsBinding(__result);
            }

            private static IEnumerable<KeyBindings> AppendTakeAllItemsBinding(
                IEnumerable<KeyBindings> bindings)
            {
                foreach (KeyBindings binding in bindings)
                {
                    yield return binding;
                }

                yield return KeyBindings.UI.Items.TransferItems;
            }
        }

        private static class RuntimeManagerTryCreateInstancePatch
        {
            internal static void Postfix(bool __result, EventDescription eventDesc, ref EventInstance newInstance)
            {
                if (__result)
                {
                    TuneCreatedEvent(eventDesc, ref newInstance);
                }
            }
        }

        private static class VCRainCheckerRoofEnterPatch
        {
            internal static void Postfix()
            {
                BattlecryVoiceTunerPlugin instance = _instance;
                if (instance != null)
                {
                    instance._heroUnderRoof = true;
                }
            }
        }

        private static class VCRainCheckerRoofExitPatch
        {
            internal static void Postfix()
            {
                BattlecryVoiceTunerPlugin instance = _instance;
                if (instance != null)
                {
                    instance._heroUnderRoof = false;
                }
            }
        }

        private static Vector3[] BuildOutdoorProbeDirections()
        {
            Vector3[] directions =
                new Vector3[OutdoorProbeDirectionCount];
            const int horizontalDirectionCount = 16;
            for (int index = 0;
                index < horizontalDirectionCount;
                index++)
            {
                float angle = 2f
                    * Mathf.PI
                    * index
                    / horizontalDirectionCount;
                directions[index] = new Vector3(
                    Mathf.Cos(angle),
                    0.04f,
                    Mathf.Sin(angle)).normalized;
            }

            for (int index = horizontalDirectionCount;
                index < directions.Length;
                index++)
            {
                int elevatedIndex =
                    index - horizontalDirectionCount;
                float angle = 2f
                    * Mathf.PI
                    * elevatedIndex
                    / (directions.Length
                        - horizontalDirectionCount);
                directions[index] = new Vector3(
                    Mathf.Cos(angle),
                    0.4f,
                    Mathf.Sin(angle)).normalized;
            }

            return directions;
        }

        private static Vector3[] BuildInteriorProbeDirections()
        {
            Vector3[] directions =
                new Vector3[InteriorProbeDirectionCount];
            const int horizontalDirectionCount = 12;
            const int elevatedDirectionCount = 8;
            const int loweredDirectionCount = 8;
            for (int index = 0;
                index < horizontalDirectionCount;
                index++)
            {
                float angle = 2f
                    * Mathf.PI
                    * index
                    / horizontalDirectionCount;
                directions[index] = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));
            }

            for (int index = 0;
                index < elevatedDirectionCount;
                index++)
            {
                float angle = 2f
                    * Mathf.PI
                    * index
                    / elevatedDirectionCount;
                directions[horizontalDirectionCount + index] =
                    new Vector3(
                        Mathf.Cos(angle),
                        0.55f,
                        Mathf.Sin(angle)).normalized;
            }

            for (int index = 0;
                index < loweredDirectionCount;
                index++)
            {
                float angle = 2f
                    * Mathf.PI
                    * (index + 0.5f)
                    / loweredDirectionCount;
                directions[horizontalDirectionCount
                    + elevatedDirectionCount
                    + index] = new Vector3(
                        Mathf.Cos(angle),
                        -0.4f,
                        Mathf.Sin(angle)).normalized;
            }

            directions[InteriorProbeDirectionCount - 2] =
                Vector3.up;
            directions[InteriorProbeDirectionCount - 1] =
                Vector3.down;
            return directions;
        }

        private sealed class BattlecryAcousticProfile
        {
            internal Vector3 Origin;
            internal string Environment;
            internal string SpaceKind;
            internal bool IsInterior;
            internal float Openness;
            internal float AverageHitDistance;
            internal float AverageReflectivity;
            internal float CeilingDistance;
            internal float DistanceVariance;
            internal float HorizontalAnisotropy;
            internal float DiffuseReverbAmount;
            internal readonly List<AcousticReflectionTap> Reflections =
                new List<AcousticReflectionTap>();
        }

        private sealed class AcousticReflectionCandidate
        {
            internal Vector3 Direction;
            internal Vector3 Position;
            internal float Distance;
            internal float Facing;
            internal float Reflectivity;
            internal float Score;
        }

        private sealed class AcousticReflectionTap
        {
            internal Vector3 Direction;
            internal Vector3 Position;
            internal float Distance;
            internal float DelaySeconds;
            internal float Gain;
            internal float LowPassGain;
        }

        private sealed class SupportedVoiceEvent
        {
            public readonly GUID Guid;
            public readonly string Path;
            public readonly string Category;
            public readonly string Label;

            public SupportedVoiceEvent(string guid, string path, string category, string label)
            {
                Guid = GUID.Parse(guid);
                Path = path;
                Category = category;
                Label = label;
            }
        }
    }
}
