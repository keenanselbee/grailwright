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
using Awaken.TG.Main.General.Configs;
using Awaken.TG.Main.General.StatTypes;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.VolumeCheckers;
using Awaken.TG.Main.Utility;
using Awaken.Utility;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using Grailwright.Shared;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Battlecry Voice Tuner")]
[assembly: AssemblyDescription("Tunes and progressively deepens player voice audio while adding configurable battlecries and summon command voices in Tainted Grail: The Fall of Avalon.")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Battlecry Voice Tuner")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace BattlecryVoiceTuner
{
    public static class BattlecryVoiceTunerApi
    {
        public const int ApiVersion = 2;

        public static bool TryPlayCommand(string commandId)
        {
            return BattlecryVoiceTunerPlugin.TryPlayCommandFromApi(commandId);
        }
    }

    internal sealed class FoASettingUiMetadata
    {
        public string DisplaySection { get; set; }
        public string DisplayName { get; set; }
        public int SectionOrder { get; set; }
        public int Order { get; set; }
        public bool Hidden { get; set; }
    }

    public enum VoiceGrowthPreset
    {
        Disabled,
        Warrior,
        Rogue,
        Mage,
        Warden,
        Artisan,
        Adventurer,
        Custom
    }

    public enum VoiceGrowthAttribute
    {
        Strength,
        Endurance,
        Dexterity,
        Spirituality,
        Practicality,
        Perception
    }

    public enum PitchProcessingMode
    {
        Natural,
        Balanced,
        TempoPreserving
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.eyes-in-the-dark", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BattlecryVoiceTunerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.battlecry-voice-tuner";
        public const string PluginName = "Battlecry Voice Tuner";
        public const string PluginVersion = "1.3.0";

        private const int CurrentConfigSchemaVersion = 10;
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
            new ConfigDefinition[0];

        private const string CategoryAttack = "Attack";
        private const string CategoryHurt = "Hurt";
        private const string CategoryDeath = "Death";
        private const string CategoryStatus = "Status";
        private const string CategoryHitFeedback = "HitFeedback";
        private const string CategoryStamina = "Stamina";
        private const int MaximumBattlecryFilesPerGender = 15;
        private const int MaximumCommandFilesPerPool = 15;
        private const int MaximumCommandReflectionTaps = 1;
        private const float VoiceGrowthFullDepthAttributeValue = 40f;
        private const float VoiceGrowthDeadZone = 0.10f;
        private const float VoiceGrowthCurvePower = 1.5f;
        private const float MaximumSinglePitchDspSemitones = 12f;
        private const float PitchDspAttachTimeoutSeconds = 0.1f;
        private const float PitchDspMinimumSemitones = 0.01f;
        private const float PitchDspFftSize = 2048f;
        private const string SummonAttackCommandId = "summon_attack";
        private const string SummonHoldCommandId = "summon_hold";
        private const string SummonFollowCommandId = "summon_follow";
        private const string SummonRecallCommandId = "summon_recall";
        private const string SummonGuardCommandId = "summon_guard";
        private const string SummonBulwarkCommandId = "summon_bulwark";
        private const string SummonHuntCommandId = "summon_hunt";
        private const string SoulAndServicePluginGuid =
            "ks.tgfoa.soul-and-service";
        private const string SoulAndServiceApiTypeName =
            "SoulAndService.SoulAndServiceApi";
        private const string MaleBattlecryPool = "battlecry:male";
        private const string FemaleBattlecryPool = "battlecry:female";
        private const string MaleSummonAttackPool = "summon_command:male:attack";
        private const string MaleSummonHoldPool = "summon_command:male:hold";
        private const string MaleSummonFollowPool = "summon_command:male:follow";
        private const string MaleSummonRecallPool = "summon_command:male:recall";
        private const string MaleSummonGuardPool = "summon_command:male:guard";
        private const string MaleSummonBulwarkPool = "summon_command:male:bulwark";
        private const string MaleSummonHuntPool = "summon_command:male:hunt";
        private const string FemaleSummonAttackPool = "summon_command:female:attack";
        private const string FemaleSummonHoldPool = "summon_command:female:hold";
        private const string FemaleSummonFollowPool = "summon_command:female:follow";
        private const string FemaleSummonRecallPool = "summon_command:female:recall";
        private const string FemaleSummonGuardPool = "summon_command:female:guard";
        private const string FemaleSummonBulwarkPool = "summon_command:female:bulwark";
        private const string FemaleSummonHuntPool = "summon_command:female:hunt";
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
        private ConfigEntry<PitchProcessingMode> _pitchProcessingMode;
        private ConfigEntry<bool> _voiceGrowthEnabled;
        private ConfigEntry<VoiceGrowthPreset> _voiceGrowthPreset;
        private ConfigEntry<float> _voiceGrowthMaximumSemitones;
        private ConfigEntry<bool> _useTemporaryAttributeModifiers;
        private ConfigEntry<VoiceGrowthAttribute> _customPrimaryAttribute;
        private ConfigEntry<VoiceGrowthAttribute> _customSecondaryAttribute;
        private ConfigEntry<float> _customPrimaryAttributeWeight;
        private ConfigEntry<bool> _nativeVoiceTuningEnabled;
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
        private ConfigEntry<int> _recentBattlecryMemory;
        private ConfigEntry<bool> _commandVoiceEnabled;
        private ConfigEntry<float> _commandVoiceVolumeMultiplier;
        private ConfigEntry<bool> _commandVoiceReverbEnabled;
        private ConfigEntry<float> _outdoorCommandVoiceReverbAmount;
        private ConfigEntry<float> _indoorCommandVoiceReverbAmount;
        private ConfigEntry<float> _maleCommandVoicePitchOffsetSemitones;
        private ConfigEntry<float> _femaleCommandVoicePitchOffsetSemitones;
        private ConfigEntry<int> _recentCommandVoiceMemory;
        private ConfigEntry<float> _commandVoiceCooldownSeconds;
        private ConfigEntry<bool> _holdTakeAllItemsForBattlecry;
        private ConfigEntry<float> _battlecryHoldSeconds;
        private ConfigEntry<KeyboardShortcut> _battlecryHotkey;
        private ConfigEntry<float> _battlecryCooldownSeconds;
        private ConfigEntry<float> _battlecryAggroRangeMultiplier;
        private ConfigEntry<float> _indoorBattlecryAggroRangeMultiplier;
        private ConfigEntry<float> _battlecryAggroDurationSeconds;
        private ConfigEntry<float> _eyesInTheDarkThreat;
        private ConfigEntry<bool> _diagnostics;
        private readonly List<string> _maleBattlecryPaths =
            new List<string>();
        private readonly List<string> _femaleBattlecryPaths =
            new List<string>();
        private readonly List<string> _maleSummonAttackPaths =
            new List<string>();
        private readonly List<string> _maleSummonHoldPaths =
            new List<string>();
        private readonly List<string> _maleSummonFollowPaths =
            new List<string>();
        private readonly List<string> _maleSummonRecallPaths =
            new List<string>();
        private readonly List<string> _maleSummonGuardPaths =
            new List<string>();
        private readonly List<string> _maleSummonBulwarkPaths =
            new List<string>();
        private readonly List<string> _maleSummonHuntPaths =
            new List<string>();
        private readonly List<string> _femaleSummonAttackPaths =
            new List<string>();
        private readonly List<string> _femaleSummonHoldPaths =
            new List<string>();
        private readonly List<string> _femaleSummonFollowPaths =
            new List<string>();
        private readonly List<string> _femaleSummonRecallPaths =
            new List<string>();
        private readonly List<string> _femaleSummonGuardPaths =
            new List<string>();
        private readonly List<string> _femaleSummonBulwarkPaths =
            new List<string>();
        private readonly List<string> _femaleSummonHuntPaths =
            new List<string>();
        private readonly Dictionary<string, FMOD.Sound> _battlecrySoundsByPath =
            new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FMOD.Sound> _commandSoundsByPath =
            new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _recentPathsByPool =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ActiveChannelPitchDsp> _activeChannelPitchDsps =
            new List<ActiveChannelPitchDsp>();
        private readonly List<PendingEventPitchDsp> _pendingEventPitchDsps =
            new List<PendingEventPitchDsp>();
        private readonly List<ActiveEventPitchDsp> _activeEventPitchDsps =
            new List<ActiveEventPitchDsp>();
        private FMOD.Studio.Bus _battlecrySfxBus;
        private FMOD.ChannelGroup _battlecrySfxChannelGroup;
        private FMOD.ChannelGroup _outdoorBattlecryChannelGroup;
        private FMOD.ChannelGroup _indoorBattlecryChannelGroup;
        private FMOD.DSP _outdoorBattlecryReverb;
        private FMOD.DSP _indoorBattlecryReverb;
        private FMOD.ChannelGroup _outdoorCommandChannelGroup;
        private FMOD.ChannelGroup _indoorCommandChannelGroup;
        private FMOD.DSP _outdoorCommandReverb;
        private FMOD.DSP _indoorCommandReverb;
        private bool _battlecrySfxBusLocked;
        private readonly HashSet<NpcAI> _challengedNpcs =
            new HashSet<NpcAI>();
        private readonly Dictionary<string, object> _pendingPreservedVoiceTuning =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;
        private float _lastBattlecryTime = float.NegativeInfinity;
        private float _lastCommandVoiceTime = float.NegativeInfinity;
        private float _challengeEndsAt;
        private float _nextChallengeScanAt;
        private Hero _challengeHero;
        private bool _takeAllItemsHeld;
        private bool _battlecryAttemptedForHold;
        private float _takeAllItemsPressedAt;
        private bool _eyesApiResolved;
        private MethodInfo _eyesBattlecryMethod;
        private MethodInfo _soulAndServiceShouldOwnTakeAllHoldMethod;
        private bool _soulAndServiceCommandApiUnavailable;
        private bool _noBattlecryFilesWarningLogged;
        private bool _noCommandFilesWarningLogged;
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
                DiscoverCommandFiles();
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
            RefreshPitchShiftDsps();

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
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            ReleaseAllPitchShiftDsps();
            ReleaseBattlecrySounds();
            ReleaseCommandSounds();
            _soulAndServiceShouldOwnTakeAllHoldMethod = null;
            _soulAndServiceCommandApiUnavailable = false;

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        internal static bool TryPlayCommandFromApi(string commandId)
        {
            BattlecryVoiceTunerPlugin instance = _instance;
            return instance != null
                && instance.TryPlayCommand(Hero.Current, commandId);
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

                if (!String.Equals(currentSection, "1. Core", StringComparison.Ordinal)
                    && !String.Equals(currentSection, "Voice Tuning", StringComparison.Ordinal)
                    && !String.Equals(currentSection, "Command Voice", StringComparison.Ordinal))
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

            CapturePreservedVoiceTuningSetting<PitchProcessingMode>(
                profile,
                "PitchProcessingMode");
            CapturePreservedVoiceTuningSetting<bool>(
                profile,
                "VoiceGrowthEnabled");
            CapturePreservedVoiceTuningSetting<VoiceGrowthPreset>(
                profile,
                "VoiceGrowthPreset");
            CapturePreservedVoiceTuningSetting<float>(
                profile,
                "VoiceGrowthMaximumSemitones");
            CapturePreservedVoiceTuningSetting<bool>(
                profile,
                "UseTemporaryAttributeModifiers");
            CapturePreservedVoiceTuningSetting<VoiceGrowthAttribute>(
                profile,
                "CustomPrimaryAttribute");
            CapturePreservedVoiceTuningSetting<VoiceGrowthAttribute>(
                profile,
                "CustomSecondaryAttribute");
            CapturePreservedVoiceTuningSetting<float>(
                profile,
                "CustomPrimaryAttributeWeight");
        }

        private void CapturePreservedVoiceTuningSetting<T>(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            string settingName)
        {
            T value;
            if (profile.TryGetCustomizedValue(
                "Voice Tuning",
                settingName,
                out value))
            {
                _pendingPreservedVoiceTuning[settingName] = value;
            }
        }

        private static bool IsPreservedVoiceTuningSetting(string settingName)
        {
            return String.Equals(settingName, "PitchSemitones", StringComparison.Ordinal)
                || String.Equals(settingName, "RandomPitchSemitones", StringComparison.Ordinal)
                || String.Equals(settingName, "VolumeMultiplier", StringComparison.Ordinal)
                || String.Equals(
                    settingName,
                    "CommandVoiceVolumeMultiplier",
                    StringComparison.Ordinal)
                || String.Equals(
                    settingName,
                    "MaleCommandVoicePitchOffsetSemitones",
                    StringComparison.Ordinal);
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
            RestorePreservedVoiceTuningSetting(
                "PitchSemitones",
                _pitchSemitones,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "RandomPitchSemitones",
                _randomPitchSemitones,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "VolumeMultiplier",
                _volumeMultiplier,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "PitchProcessingMode",
                _pitchProcessingMode,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "VoiceGrowthEnabled",
                _voiceGrowthEnabled,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "VoiceGrowthPreset",
                _voiceGrowthPreset,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "VoiceGrowthMaximumSemitones",
                _voiceGrowthMaximumSemitones,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "UseTemporaryAttributeModifiers",
                _useTemporaryAttributeModifiers,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "CustomPrimaryAttribute",
                _customPrimaryAttribute,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "CustomSecondaryAttribute",
                _customSecondaryAttribute,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "CustomPrimaryAttributeWeight",
                _customPrimaryAttributeWeight,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "CommandVoiceVolumeMultiplier",
                _commandVoiceVolumeMultiplier,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedVoiceTuningSetting(
                "MaleCommandVoicePitchOffsetSemitones",
                _maleCommandVoicePitchOffsetSemitones,
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

        private void RestorePreservedVoiceTuningSetting<T>(
            string settingName,
            ConfigEntry<T> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            object preservedValue;
            if (entry == null
                || !_pendingPreservedVoiceTuning.TryGetValue(
                    settingName,
                    out preservedValue)
                || !(preservedValue is T))
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                (T)preservedValue,
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
                "General",
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
                "General",
                "Enabled",
                true,
                UiDescription(
                    "Master toggle for player voice tuning, battlecries, and command voices.",
                    "General",
                    "Master Enable",
                    0,
                    0));

            _pitchSemitones = Config.Bind(
                "Voice Tuning",
                "PitchSemitones",
                0.0f,
                UiDescription(
                    "Manual baseline pitch shift for supported player voice sounds, battlecries, and command voices. Attribute voice growth, gender-specific offsets, and random variation are added to this value.",
                    "Voice Tuning",
                    "Overall Pitch (Semitones)",
                    1,
                    0,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _pitchProcessingMode = Config.Bind(
                "Voice Tuning",
                "PitchProcessingMode",
                PitchProcessingMode.Natural,
                UiDescription(
                    "Controls how pitch affects timing for native voice sounds, battlecries, and command voices. Natural changes playback speed with pitch, Balanced reduces that timing change, and TempoPreserving keeps roughly the original duration but can sound more processed.",
                    "Voice Tuning",
                    "Pitch Processing",
                    1,
                    1));

            _volumeMultiplier = Config.Bind(
                "Voice Tuning",
                "VolumeMultiplier",
                1.0f,
                UiDescription(
                    "Overall volume multiplier for supported native voice events, custom battlecries, and command voices. Custom pools also apply their own volume multipliers.",
                    "Voice Tuning",
                    "Overall Voice Volume",
                    1,
                    2,
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            _randomPitchSemitones = Config.Bind(
                "Voice Tuning",
                "RandomPitchSemitones",
                0.15f,
                UiDescription(
                    "Maximum random pitch variation added per played sound, in semitones. Set 0 for a fixed pitch.",
                    "Voice Tuning",
                    "Random Pitch (Semitones)",
                    1,
                    3,
                    new AcceptableValueRange<float>(0.0f, 3.0f)));

            _voiceGrowthEnabled = Config.Bind(
                "Voice Tuning",
                "VoiceGrowthEnabled",
                true,
                UiDescription(
                    "Let permanent character attributes gradually deepen the Hero's supported voice sounds, battlecries, and command voices.",
                    "Voice Tuning",
                    "Attribute Voice Growth",
                    1,
                    4));

            _voiceGrowthPreset = Config.Bind(
                "Voice Tuning",
                "VoiceGrowthPreset",
                VoiceGrowthPreset.Warrior,
                UiDescription(
                    "Selects the attributes that shape voice progression. Warrior uses 75% Strength and 25% Endurance; other archetypes use their matching attributes.",
                    "Voice Tuning",
                    "Growth Archetype",
                    1,
                    5));

            _voiceGrowthMaximumSemitones = Config.Bind(
                "Voice Tuning",
                "VoiceGrowthMaximumSemitones",
                -6.0f,
                UiDescription(
                    "Deepest additional pitch shift reached at exceptional archetype attributes. Growth eases in above innate values and reaches full depth at attribute value 40.",
                    "Voice Tuning",
                    "Maximum Growth Pitch (Semitones)",
                    1,
                    6,
                    new AcceptableValueRange<float>(-12.0f, 0.0f)));

            _useTemporaryAttributeModifiers = Config.Bind(
                "Voice Tuning",
                "UseTemporaryAttributeModifiers",
                false,
                UiDescription(
                    "Include temporary attribute modifiers in voice growth. Off keeps equipment, consumables, and short effects from making the voice fluctuate.",
                    "Voice Growth - Advanced",
                    "Include Temporary Attributes",
                    2,
                    0));

            _customPrimaryAttribute = Config.Bind(
                "Voice Tuning",
                "CustomPrimaryAttribute",
                VoiceGrowthAttribute.Strength,
                UiDescription(
                    "Primary progression attribute used only by the Custom archetype.",
                    "Voice Growth - Advanced",
                    "Custom Primary Attribute",
                    2,
                    1));

            _customSecondaryAttribute = Config.Bind(
                "Voice Tuning",
                "CustomSecondaryAttribute",
                VoiceGrowthAttribute.Endurance,
                UiDescription(
                    "Secondary progression attribute used only by the Custom archetype.",
                    "Voice Growth - Advanced",
                    "Custom Secondary Attribute",
                    2,
                    2));

            _customPrimaryAttributeWeight = Config.Bind(
                "Voice Tuning",
                "CustomPrimaryAttributeWeight",
                0.75f,
                UiDescription(
                    "Share of Custom archetype growth supplied by the primary attribute; the secondary attribute supplies the remainder.",
                    "Voice Growth - Advanced",
                    "Custom Primary Weight",
                    2,
                    3,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            _nativeVoiceTuningEnabled = Config.Bind(
                "Native Voice Events",
                "NativeVoiceTuningEnabled",
                true,
                UiDescription(
                    "Master toggle for tuning the game's supported native player voice events. Custom battlecries and command voices remain independently controlled.",
                    "Native Voice Events",
                    "Native Voice Tuning",
                    3,
                    0));

            _includeAttackGrunts = Config.Bind(
                "Native Voice Events",
                "IncludeAttackGrunts",
                true,
                UiDescription(
                    "Tune player attack/exertion grunts.",
                    "Native Voice Events",
                    "Attack and Exertion Grunts",
                    3,
                    1));

            _includeHurtGrunts = Config.Bind(
                "Native Voice Events",
                "IncludeHurtGrunts",
                true,
                UiDescription(
                    "Tune player hurt grunts.",
                    "Native Voice Events",
                    "Hurt Grunts",
                    3,
                    2));

            _includeDeathGrunts = Config.Bind(
                "Native Voice Events",
                "IncludeDeathGrunts",
                true,
                UiDescription(
                    "Tune player death grunts.",
                    "Native Voice Events",
                    "Death Grunts",
                    3,
                    3));

            _includeStatusPainGrunts = Config.Bind(
                "Native Voice Events",
                "IncludeStatusPainGrunts",
                true,
                UiDescription(
                    "Tune player burn, bleed, poison, and drown grunts.",
                    "Native Voice Events",
                    "Status Pain Grunts",
                    3,
                    4));

            _includePlayerHitFeedback = Config.Bind(
                "Native Voice Events",
                "IncludePlayerHitFeedback",
                true,
                UiDescription(
                    "Tune SFX_Player_Hit, the player hit-feedback sound used when the player lands a hit.",
                    "Native Voice Events",
                    "Player Hit Feedback",
                    3,
                    5));

            _includeStaminaDepletedBreathing = Config.Bind(
                "Native Voice Events",
                "IncludeStaminaDepletedBreathing",
                false,
                UiDescription(
                    "Tune stamina-depleted breathing loops. Off by default because these are longer/looping sounds.",
                    "Native Voice Events",
                    "Stamina-Depleted Breathing",
                    3,
                    6));

            _battlecryEnabled = Config.Bind(
                "Battlecry Audio",
                "BattlecryEnabled",
                true,
                UiDescription(
                    "Enable custom battlecry audio and its enemy challenge effect.",
                    "Battlecry",
                    "Enabled",
                    4,
                    0));

            _battlecryVolumeMultiplier = Config.Bind(
                "Battlecry Audio",
                "BattlecryVolumeMultiplier",
                0.5f,
                UiDescription(
                    "Additional volume multiplier applied only to custom battlecries after Overall Voice Volume. Battlecries also follow the game's SFX volume category.",
                    "Battlecry Audio",
                    "Additional Battlecry Volume",
                    5,
                    0,
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            _battlecryReverbEnabled = Config.Bind(
                "Battlecry Audio",
                "BattlecryReverbEnabled",
                true,
                UiDescription(
                    "Apply environment-aware reverb only to custom battlecries. Full interiors and the game's roof volumes use the indoor amount; other open-world areas use the outdoor amount.",
                    "Battlecry Audio",
                    "Environment Reverb",
                    5,
                    1));

            _outdoorBattlecryReverbAmount = Config.Bind(
                "Battlecry Audio",
                "OutdoorBattlecryReverbAmount",
                0.15f,
                UiDescription(
                    "Light reverb amount for battlecries in unroofed open-world areas. Zero is dry; one is the strongest supported effect.",
                    "Battlecry Audio",
                    "Outdoor Reverb Amount",
                    5,
                    2,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            _indoorBattlecryReverbAmount = Config.Bind(
                "Battlecry Audio",
                "IndoorBattlecryReverbAmount",
                0.70f,
                UiDescription(
                    "Strength multiplier for room-scaled reverb and qualifying long reflections in interiors, caves, and the game's roof volumes. Zero is dry; one is the strongest supported effect.",
                    "Battlecry Audio",
                    "Indoor Reverb Amount",
                    5,
                    3,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            _maleBattlecryPitchOffsetSemitones = Config.Bind(
                "Battlecry Audio",
                "MaleBattlecryPitchOffsetSemitones",
                0.0f,
                UiDescription(
                    "Additional pitch shift applied only to male battlecries after the overall PitchSemitones setting.",
                    "Battlecry Audio",
                    "Male Pitch Offset (Semitones)",
                    5,
                    4,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _femaleBattlecryPitchOffsetSemitones = Config.Bind(
                "Battlecry Audio",
                "FemaleBattlecryPitchOffsetSemitones",
                0.0f,
                UiDescription(
                    "Additional pitch shift applied only to female battlecries after the overall PitchSemitones setting.",
                    "Battlecry Audio",
                    "Female Pitch Offset (Semitones)",
                    5,
                    5,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _recentBattlecryMemory = Config.Bind(
                "Battlecry Audio",
                "RecentBattlecryMemory",
                2,
                UiDescription(
                    "How many recently played battlecries to avoid for each gender when alternatives remain.",
                    "Battlecry Audio",
                    "Recent Sound Memory",
                    5,
                    6,
                    new AcceptableValueRange<int>(0, 20)));

            _commandVoiceEnabled = Config.Bind(
                "Command Voice",
                "CommandVoiceEnabled",
                true,
                UiDescription(
                    "Play a gender-matched spoken command when a supported mod successfully issues an explicit order.",
                    "Command Voice",
                    "Enabled",
                    6,
                    0));

            _commandVoiceVolumeMultiplier = Config.Bind(
                "Command Voice",
                "CommandVoiceVolumeMultiplier",
                0.50f,
                UiDescription(
                    "Additional command-only volume multiplier after Overall Voice Volume. Commands follow the game's SFX volume category.",
                    "Command Voice",
                    "Additional Command Volume",
                    6,
                    1,
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            _commandVoiceReverbEnabled = Config.Bind(
                "Command Voice",
                "CommandVoiceReverbEnabled",
                true,
                UiDescription(
                    "Apply a lighter environment-aware acoustic response to command voices using separate reusable FMOD paths.",
                    "Command Voice",
                    "Environment Reverb",
                    6,
                    2));

            _outdoorCommandVoiceReverbAmount = Config.Bind(
                "Command Voice",
                "OutdoorCommandVoiceReverbAmount",
                0.10f,
                UiDescription(
                    "Light geometry-shaped command reverb in unroofed open-world areas.",
                    "Command Voice",
                    "Outdoor Reverb Amount",
                    6,
                    3,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            _indoorCommandVoiceReverbAmount = Config.Bind(
                "Command Voice",
                "IndoorCommandVoiceReverbAmount",
                0.45f,
                UiDescription(
                    "Room-scaled command reverb in interiors, caves, and roofed spaces.",
                    "Command Voice",
                    "Indoor Reverb Amount",
                    6,
                    4,
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            _maleCommandVoicePitchOffsetSemitones = Config.Bind(
                "Command Voice",
                "MaleCommandVoicePitchOffsetSemitones",
                5.0f,
                UiDescription(
                    "Additional pitch shift applied only to male command voices after Overall Pitch.",
                    "Command Voice",
                    "Male Pitch Offset (Semitones)",
                    6,
                    5,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _femaleCommandVoicePitchOffsetSemitones = Config.Bind(
                "Command Voice",
                "FemaleCommandVoicePitchOffsetSemitones",
                1.0f,
                UiDescription(
                    "Additional pitch shift applied only to female command voices after Overall Pitch.",
                    "Command Voice",
                    "Female Pitch Offset (Semitones)",
                    6,
                    6,
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _recentCommandVoiceMemory = Config.Bind(
                "Command Voice",
                "RecentCommandVoiceMemory",
                2,
                UiDescription(
                    "How many recently played command voices to avoid within each gender and command-type pool when alternatives remain.",
                    "Command Voice",
                    "Recent Sound Memory",
                    6,
                    7,
                    new AcceptableValueRange<int>(0, 20)));

            _commandVoiceCooldownSeconds = Config.Bind(
                "Command Voice",
                "CommandVoiceCooldownSeconds",
                0.75f,
                UiDescription(
                    "Minimum active gameplay seconds between spoken commands, preventing rapid orders from stacking voices.",
                    "Command Voice",
                    "Cooldown (Seconds)",
                    6,
                    8,
                    new AcceptableValueRange<float>(0.0f, 5.0f)));

            _holdTakeAllItemsForBattlecry = Config.Bind(
                "Battlecry Input",
                "HoldTakeAllItemsForBattlecry",
                true,
                UiDescription(
                    "Hold the game's Take All Items action to battlecry. Uses the game's current remapped keyboard or controller binding and does not interfere with taking items from an open container.",
                    "Battlecry",
                    "Hold Take All Items",
                    4,
                    1));

            _battlecryHoldSeconds = Config.Bind(
                "Battlecry Input",
                "BattlecryHoldSeconds",
                0.45f,
                UiDescription(
                    "Seconds the Take All Items action must be held before attempting a battlecry.",
                    "Battlecry",
                    "Hold Time (Seconds)",
                    4,
                    2,
                    new AcceptableValueRange<float>(0.2f, 2.0f)));

            _battlecryHotkey = Config.Bind(
                "Battlecry Input",
                "BattlecryHotkey",
                new KeyboardShortcut(KeyCode.None),
                UiDescription(
                    "Optional separate keyboard or joystick-button shortcut. None disables the separate shortcut.",
                    "Battlecry",
                    "Separate Hotkey",
                    4,
                    3));

            _battlecryCooldownSeconds = Config.Bind(
                "Battlecry Input",
                "BattlecryCooldownSeconds",
                1.5f,
                UiDescription(
                    "Minimum active gameplay seconds between battlecries.",
                    "Battlecry",
                    "Cooldown (Seconds)",
                    4,
                    4,
                    new AcceptableValueRange<float>(0.0f, 30.0f)));

            _battlecryAggroRangeMultiplier = Config.Bind(
                "Battlecry Challenge",
                "BattlecryAggroRangeMultiplier",
                3.0f,
                UiDescription(
                    "Multiplier applied to each hostile NPC's normal maximum hearing range for battlecries in unroofed open-world areas.",
                    "Battlecry",
                    "Outdoor Hearing Range Multiplier",
                    4,
                    5,
                    new AcceptableValueRange<float>(0.0f, 5.0f)));

            _indoorBattlecryAggroRangeMultiplier = Config.Bind(
                "Battlecry Challenge",
                "IndoorBattlecryAggroRangeMultiplier",
                4.0f,
                UiDescription(
                    "Multiplier applied to each hostile NPC's normal maximum hearing range in interiors, caves, and the game's roof volumes.",
                    "Battlecry",
                    "Indoor Hearing Range Multiplier",
                    4,
                    6,
                    new AcceptableValueRange<float>(0.0f, 5.0f)));

            _battlecryAggroDurationSeconds = Config.Bind(
                "Battlecry Challenge",
                "BattlecryAggroDurationSeconds",
                3.0f,
                UiDescription(
                    "Active gameplay seconds during which newly reached hostile NPCs can hear the challenge.",
                    "Battlecry",
                    "Challenge Duration (Seconds)",
                    4,
                    7,
                    new AcceptableValueRange<float>(0.1f, 10.0f)));

            _eyesInTheDarkThreat = Config.Bind(
                "Optional Integrations",
                "EyesInTheDarkThreat",
                10.0f,
                UiDescription(
                    "Wyrd Threat requested from Eyes in the Dark for each successful battlecry. Has no effect when Eyes is absent or its Wyrdnight activity rules reject the request.",
                    "Optional Integrations",
                    "Eyes in the Dark Threat",
                    7,
                    0,
                    new AcceptableValueRange<float>(0.0f, 100.0f)));

            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                UiDescription(
                    "Write detailed match and FMOD result information to the BepInEx log.",
                    "Diagnostics",
                    "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder,
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
            if (_enabled == null
                || !_enabled.Value
                || _nativeVoiceTuningEnabled == null
                || !_nativeVoiceTuningEnabled.Value
                || !eventInstance.isValid())
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
            if (pitchResult == RESULT.OK)
            {
                QueueEventPitchDsp(
                    eventInstance,
                    BuildPitchProcessing(pitchMultiplier),
                    supportedEvent.Label);
            }

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
            float baselineSemitones = 0.0f,
            Hero hero = null)
        {
            float semitones = baselineSemitones
                + _pitchSemitones.Value
                + GetVoiceGrowthSemitones(hero ?? Hero.Current);
            float randomRange = Math.Max(0.0f, _randomPitchSemitones.Value);
            if (randomRange > 0.0f)
            {
                semitones += (float)((_random.NextDouble() * 2.0 - 1.0) * randomRange);
            }

            return Math.Max(-24.0f, Math.Min(24.0f, semitones));
        }

        private float GetVoiceGrowthSemitones(Hero hero)
        {
            if (_voiceGrowthEnabled == null
                || !_voiceGrowthEnabled.Value
                || _voiceGrowthPreset == null
                || _voiceGrowthPreset.Value == VoiceGrowthPreset.Disabled
                || hero == null
                || hero.HasBeenDiscarded
                || hero.HeroRPGStats == null)
            {
                return 0f;
            }

            float progress;
            switch (_voiceGrowthPreset.Value)
            {
                case VoiceGrowthPreset.Warrior:
                    progress = GetWeightedAttributeProgress(
                        hero,
                        VoiceGrowthAttribute.Strength,
                        VoiceGrowthAttribute.Endurance,
                        0.75f);
                    break;
                case VoiceGrowthPreset.Rogue:
                    progress = GetWeightedAttributeProgress(
                        hero,
                        VoiceGrowthAttribute.Dexterity,
                        VoiceGrowthAttribute.Perception,
                        0.65f);
                    break;
                case VoiceGrowthPreset.Mage:
                    progress = GetWeightedAttributeProgress(
                        hero,
                        VoiceGrowthAttribute.Spirituality,
                        VoiceGrowthAttribute.Perception,
                        0.75f);
                    break;
                case VoiceGrowthPreset.Warden:
                    progress = GetWeightedAttributeProgress(
                        hero,
                        VoiceGrowthAttribute.Endurance,
                        VoiceGrowthAttribute.Spirituality,
                        0.60f);
                    break;
                case VoiceGrowthPreset.Artisan:
                    progress = GetWeightedAttributeProgress(
                        hero,
                        VoiceGrowthAttribute.Practicality,
                        VoiceGrowthAttribute.Dexterity,
                        0.70f);
                    break;
                case VoiceGrowthPreset.Adventurer:
                    progress = (
                        GetAttributeProgress(hero, VoiceGrowthAttribute.Strength)
                        + GetAttributeProgress(hero, VoiceGrowthAttribute.Endurance)
                        + GetAttributeProgress(hero, VoiceGrowthAttribute.Dexterity)
                        + GetAttributeProgress(hero, VoiceGrowthAttribute.Spirituality)
                        + GetAttributeProgress(hero, VoiceGrowthAttribute.Practicality)
                        + GetAttributeProgress(hero, VoiceGrowthAttribute.Perception))
                        / 6f;
                    break;
                case VoiceGrowthPreset.Custom:
                    float primaryWeight = _customPrimaryAttributeWeight == null
                        ? 0.75f
                        : Mathf.Clamp01(_customPrimaryAttributeWeight.Value);
                    progress = GetWeightedAttributeProgress(
                        hero,
                        _customPrimaryAttribute == null
                            ? VoiceGrowthAttribute.Strength
                            : _customPrimaryAttribute.Value,
                        _customSecondaryAttribute == null
                            ? VoiceGrowthAttribute.Endurance
                            : _customSecondaryAttribute.Value,
                        primaryWeight);
                    break;
                default:
                    return 0f;
            }

            float curvedProgress = Mathf.Clamp01(
                (progress - VoiceGrowthDeadZone)
                / (1f - VoiceGrowthDeadZone));
            curvedProgress = Mathf.Pow(
                curvedProgress,
                VoiceGrowthCurvePower);
            float maximumSemitones = _voiceGrowthMaximumSemitones == null
                ? -6f
                : Math.Max(-12f, Math.Min(0f, _voiceGrowthMaximumSemitones.Value));
            return maximumSemitones * curvedProgress;
        }

        private float GetWeightedAttributeProgress(
            Hero hero,
            VoiceGrowthAttribute primary,
            VoiceGrowthAttribute secondary,
            float primaryWeight)
        {
            primaryWeight = Mathf.Clamp01(primaryWeight);
            return GetAttributeProgress(hero, primary) * primaryWeight
                + GetAttributeProgress(hero, secondary) * (1f - primaryWeight);
        }

        private float GetAttributeProgress(
            Hero hero,
            VoiceGrowthAttribute attribute)
        {
            Stat stat;
            HeroRPGStatType statType;
            if (!TryGetVoiceGrowthStat(hero, attribute, out stat, out statType))
            {
                return 0f;
            }

            float innateValue = 1f;
            try
            {
                if (GameConstants.Get.RPGStatParamsByType.ContainsKey(statType))
                {
                    innateValue = GameConstants.Get
                        .RPGStatParamsByType[statType]
                        .InnateStatLevel;
                }
            }
            catch
            {
            }

            float value = _useTemporaryAttributeModifiers != null
                && _useTemporaryAttributeModifiers.Value
                    ? stat.ModifiedValue
                    : stat.BaseValue;
            float fullDepthRange = Math.Max(
                1f,
                VoiceGrowthFullDepthAttributeValue - innateValue);
            return Mathf.Clamp01((value - innateValue) / fullDepthRange);
        }

        private static bool TryGetVoiceGrowthStat(
            Hero hero,
            VoiceGrowthAttribute attribute,
            out Stat stat,
            out HeroRPGStatType statType)
        {
            stat = null;
            statType = null;
            if (hero == null || hero.HeroRPGStats == null)
            {
                return false;
            }

            switch (attribute)
            {
                case VoiceGrowthAttribute.Strength:
                    stat = hero.HeroRPGStats.Strength;
                    statType = HeroRPGStatType.Strength;
                    break;
                case VoiceGrowthAttribute.Endurance:
                    stat = hero.HeroRPGStats.Endurance;
                    statType = HeroRPGStatType.Endurance;
                    break;
                case VoiceGrowthAttribute.Dexterity:
                    stat = hero.HeroRPGStats.Dexterity;
                    statType = HeroRPGStatType.Dexterity;
                    break;
                case VoiceGrowthAttribute.Spirituality:
                    stat = hero.HeroRPGStats.Spirituality;
                    statType = HeroRPGStatType.Spirituality;
                    break;
                case VoiceGrowthAttribute.Practicality:
                    stat = hero.HeroRPGStats.Practicality;
                    statType = HeroRPGStatType.Practicality;
                    break;
                case VoiceGrowthAttribute.Perception:
                    stat = hero.HeroRPGStats.Perception;
                    statType = HeroRPGStatType.Perception;
                    break;
            }

            return stat != null && statType != null;
        }

        private static float SemitonesToPitchMultiplier(float semitones)
        {
            return (float)Math.Pow(2.0, semitones / 12.0);
        }

        private VoicePitchProcessing BuildPitchProcessing(
            float finalPitchMultiplier)
        {
            finalPitchMultiplier = Math.Max(0.01f, finalPitchMultiplier);
            float finalSemitones = (float)(
                12.0
                * Math.Log(finalPitchMultiplier, 2.0));
            PitchProcessingMode mode = _pitchProcessingMode == null
                ? PitchProcessingMode.Natural
                : _pitchProcessingMode.Value;
            float rateShare;
            switch (mode)
            {
                case PitchProcessingMode.Natural:
                    rateShare = 1f;
                    break;
                case PitchProcessingMode.TempoPreserving:
                    rateShare = 0f;
                    break;
                default:
                    rateShare = 0.5f;
                    break;
            }

            float dspSemitones = Math.Max(
                -MaximumSinglePitchDspSemitones,
                Math.Min(
                    MaximumSinglePitchDspSemitones,
                    finalSemitones * (1f - rateShare)));
            float rateSemitones = finalSemitones - dspSemitones;
            return new VoicePitchProcessing(
                finalSemitones,
                rateSemitones,
                dspSemitones,
                finalPitchMultiplier,
                SemitonesToPitchMultiplier(rateSemitones),
                SemitonesToPitchMultiplier(dspSemitones));
        }

        private RESULT ApplyPitchProcessingToChannel(
            FMOD.Channel channel,
            float finalPitchMultiplier,
            string label)
        {
            VoicePitchProcessing processing =
                BuildPitchProcessing(finalPitchMultiplier);
            if (!processing.UsesDsp)
            {
                return channel.setPitch(processing.FinalMultiplier);
            }

            FMOD.DSP pitchDsp;
            if (!TryAttachPitchDsp(
                    channel,
                    processing.DspMultiplier,
                    out pitchDsp))
            {
                LogDiagnostic(
                    label
                    + " pitch DSP was unavailable; using the full natural playback-rate shift.");
                return channel.setPitch(processing.FinalMultiplier);
            }

            RESULT pitchResult = channel.setPitch(
                processing.RateMultiplier);
            if (pitchResult != RESULT.OK)
            {
                channel.removeDSP(pitchDsp);
                pitchDsp.release();
                return channel.setPitch(processing.FinalMultiplier);
            }

            _activeChannelPitchDsps.Add(
                new ActiveChannelPitchDsp(channel, pitchDsp));
            LogDiagnostic(
                label
                + " pitch processing: final="
                + processing.FinalSemitones.ToString("0.00", CultureInfo.InvariantCulture)
                + "st; rate="
                + processing.RateSemitones.ToString("0.00", CultureInfo.InvariantCulture)
                + "st; dsp="
                + processing.DspSemitones.ToString("0.00", CultureInfo.InvariantCulture)
                + "st.");
            return pitchResult;
        }

        private void QueueEventPitchDsp(
            EventInstance eventInstance,
            VoicePitchProcessing processing,
            string label)
        {
            if (!processing.UsesDsp || !eventInstance.isValid())
            {
                return;
            }

            RESULT pauseResult = eventInstance.setPaused(true);
            if (pauseResult != RESULT.OK)
            {
                LogDiagnostic(
                    label
                    + " native event could not pause for pitch DSP attachment; using the full natural playback-rate shift. Result="
                    + pauseResult
                    + ".");
                return;
            }

            _pendingEventPitchDsps.Add(
                new PendingEventPitchDsp(
                    eventInstance,
                    processing,
                    label,
                    Time.realtimeSinceStartup));
        }

        private void RefreshPitchShiftDsps()
        {
            for (int index = _pendingEventPitchDsps.Count - 1;
                index >= 0;
                index--)
            {
                PendingEventPitchDsp pending =
                    _pendingEventPitchDsps[index];
                if (!pending.EventInstance.isValid())
                {
                    _pendingEventPitchDsps.RemoveAt(index);
                    continue;
                }

                FMOD.ChannelGroup channelGroup;
                RESULT groupResult = pending.EventInstance.getChannelGroup(
                    out channelGroup);
                if (groupResult == RESULT.OK
                    && channelGroup.hasHandle())
                {
                    FMOD.DSP pitchDsp;
                    if (TryAttachPitchDsp(
                            channelGroup,
                            pending.Processing.DspMultiplier,
                            out pitchDsp))
                    {
                        RESULT pitchResult = pending.EventInstance.setPitch(
                            pending.Processing.RateMultiplier);
                        RESULT unpauseResult = pitchResult == RESULT.OK
                            ? pending.EventInstance.setPaused(false)
                            : pitchResult;
                        if (pitchResult == RESULT.OK
                            && unpauseResult == RESULT.OK)
                        {
                            _activeEventPitchDsps.Add(
                                new ActiveEventPitchDsp(
                                    pending.EventInstance,
                                    channelGroup,
                                    pitchDsp));
                            LogDiagnostic(
                                pending.Label
                                + " native event pitch processing: final="
                                + pending.Processing.FinalSemitones.ToString(
                                    "0.00",
                                    CultureInfo.InvariantCulture)
                                + "st; rate="
                                + pending.Processing.RateSemitones.ToString(
                                    "0.00",
                                    CultureInfo.InvariantCulture)
                                + "st; dsp="
                                + pending.Processing.DspSemitones.ToString(
                                    "0.00",
                                    CultureInfo.InvariantCulture)
                                + "st.");
                        }
                        else
                        {
                            channelGroup.removeDSP(pitchDsp);
                            pitchDsp.release();
                            ResumePendingEventNaturally(
                                pending,
                                "hybrid pitch activation failed. PitchResult="
                                + pitchResult
                                + "; UnpauseResult="
                                + unpauseResult);
                        }
                    }
                    else
                    {
                        ResumePendingEventNaturally(
                            pending,
                            "pitch DSP attachment was unavailable");
                    }
                    _pendingEventPitchDsps.RemoveAt(index);
                    continue;
                }

                if (Time.realtimeSinceStartup - pending.QueuedAt
                    >= PitchDspAttachTimeoutSeconds)
                {
                    ResumePendingEventNaturally(
                        pending,
                        "channel group did not become available. Result="
                        + groupResult);
                    _pendingEventPitchDsps.RemoveAt(index);
                }
            }

            for (int index = _activeChannelPitchDsps.Count - 1;
                index >= 0;
                index--)
            {
                ActiveChannelPitchDsp active =
                    _activeChannelPitchDsps[index];
                bool playing = false;
                RESULT result = active.Channel.hasHandle()
                    ? active.Channel.isPlaying(out playing)
                    : RESULT.ERR_INVALID_HANDLE;
                if (result == RESULT.OK && playing)
                {
                    continue;
                }

                ReleaseChannelPitchDsp(active);
                _activeChannelPitchDsps.RemoveAt(index);
            }

            for (int index = _activeEventPitchDsps.Count - 1;
                index >= 0;
                index--)
            {
                ActiveEventPitchDsp active =
                    _activeEventPitchDsps[index];
                PLAYBACK_STATE state = PLAYBACK_STATE.STOPPED;
                RESULT result = active.EventInstance.isValid()
                    ? active.EventInstance.getPlaybackState(out state)
                    : RESULT.ERR_INVALID_HANDLE;
                if (result == RESULT.OK
                    && state != PLAYBACK_STATE.STOPPED)
                {
                    continue;
                }

                ReleaseEventPitchDsp(active);
                _activeEventPitchDsps.RemoveAt(index);
            }
        }

        private void ResumePendingEventNaturally(
            PendingEventPitchDsp pending,
            string reason)
        {
            if (!pending.EventInstance.isValid())
            {
                return;
            }

            RESULT pitchResult = pending.EventInstance.setPitch(
                pending.Processing.FinalMultiplier);
            RESULT unpauseResult = pending.EventInstance.setPaused(false);
            LogDiagnostic(
                pending.Label
                + " native event resumed with the full natural pitch shift because "
                + reason
                + ". PitchResult="
                + pitchResult
                + "; UnpauseResult="
                + unpauseResult
                + ".");
        }

        private bool TryAttachPitchDsp(
            FMOD.Channel channel,
            float pitchMultiplier,
            out FMOD.DSP pitchDsp)
        {
            if (!TryCreatePitchDsp(pitchMultiplier, out pitchDsp))
            {
                return false;
            }

            RESULT addResult = channel.addDSP(
                CHANNELCONTROL_DSP_INDEX.HEAD,
                pitchDsp);
            if (addResult == RESULT.OK)
            {
                return true;
            }

            pitchDsp.release();
            pitchDsp = default(FMOD.DSP);
            return false;
        }

        private bool TryAttachPitchDsp(
            FMOD.ChannelGroup channelGroup,
            float pitchMultiplier,
            out FMOD.DSP pitchDsp)
        {
            if (!TryCreatePitchDsp(pitchMultiplier, out pitchDsp))
            {
                return false;
            }

            RESULT addResult = channelGroup.addDSP(
                CHANNELCONTROL_DSP_INDEX.HEAD,
                pitchDsp);
            if (addResult == RESULT.OK)
            {
                return true;
            }

            pitchDsp.release();
            pitchDsp = default(FMOD.DSP);
            return false;
        }

        private static bool TryCreatePitchDsp(
            float pitchMultiplier,
            out FMOD.DSP pitchDsp)
        {
            pitchDsp = default(FMOD.DSP);
            RESULT result = RuntimeManager.CoreSystem.createDSPByType(
                DSP_TYPE.PITCHSHIFT,
                out pitchDsp);
            if (result == RESULT.OK)
            {
                result = pitchDsp.setParameterFloat(
                    (int)DSP_PITCHSHIFT.PITCH,
                    pitchMultiplier);
            }
            if (result == RESULT.OK)
            {
                result = pitchDsp.setParameterFloat(
                    (int)DSP_PITCHSHIFT.FFTSIZE,
                    PitchDspFftSize);
            }
            if (result == RESULT.OK)
            {
                return true;
            }

            if (pitchDsp.hasHandle())
            {
                pitchDsp.release();
            }
            pitchDsp = default(FMOD.DSP);
            return false;
        }

        private void ReleaseAllPitchShiftDsps()
        {
            _pendingEventPitchDsps.Clear();
            for (int index = _activeChannelPitchDsps.Count - 1;
                index >= 0;
                index--)
            {
                ReleaseChannelPitchDsp(_activeChannelPitchDsps[index]);
            }
            _activeChannelPitchDsps.Clear();

            for (int index = _activeEventPitchDsps.Count - 1;
                index >= 0;
                index--)
            {
                ReleaseEventPitchDsp(_activeEventPitchDsps[index]);
            }
            _activeEventPitchDsps.Clear();
        }

        private static void ReleaseChannelPitchDsp(
            ActiveChannelPitchDsp active)
        {
            if (active.Channel.hasHandle() && active.Dsp.hasHandle())
            {
                active.Channel.removeDSP(active.Dsp);
            }
            if (active.Dsp.hasHandle())
            {
                active.Dsp.release();
            }
        }

        private static void ReleaseEventPitchDsp(
            ActiveEventPitchDsp active)
        {
            if (active.ChannelGroup.hasHandle() && active.Dsp.hasHandle())
            {
                active.ChannelGroup.removeDSP(active.Dsp);
            }
            if (active.Dsp.hasHandle())
            {
                active.Dsp.release();
            }
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

            if (ShouldYieldTakeAllItemsToSoulAndService())
            {
                ResetTakeAllItemsHold();
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

        private bool ShouldYieldTakeAllItemsToSoulAndService()
        {
            if (_soulAndServiceCommandApiUnavailable)
            {
                return false;
            }

            if (_soulAndServiceShouldOwnTakeAllHoldMethod == null)
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(
                        SoulAndServicePluginGuid,
                        out info)
                    || info == null
                    || info.Instance == null)
                {
                    return false;
                }

                Type api = info.Instance.GetType().Assembly.GetType(
                    SoulAndServiceApiTypeName,
                    false);
                FieldInfo version = api == null
                    ? null
                    : api.GetField(
                        "ApiVersion",
                        BindingFlags.Public | BindingFlags.Static);
                int apiVersion = version == null
                    ? 0
                    : Convert.ToInt32(
                        version.GetRawConstantValue(),
                        CultureInfo.InvariantCulture);
                if (apiVersion < 3)
                {
                    _soulAndServiceCommandApiUnavailable = true;
                    return false;
                }

                _soulAndServiceShouldOwnTakeAllHoldMethod =
                    AccessTools.Method(
                        api,
                        "ShouldOwnTakeAllHold",
                        new Type[0]);
                _soulAndServiceCommandApiUnavailable =
                    _soulAndServiceShouldOwnTakeAllHoldMethod == null;
            }

            if (_soulAndServiceShouldOwnTakeAllHoldMethod == null)
            {
                return false;
            }

            try
            {
                object result =
                    _soulAndServiceShouldOwnTakeAllHoldMethod.Invoke(
                        null,
                        null);
                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                _soulAndServiceShouldOwnTakeAllHoldMethod = null;
                _soulAndServiceCommandApiUnavailable = true;
                _log.LogWarning(
                    "Soul and Service Take All command arbitration failed: "
                    + exception.GetBaseException().Message);
                return false;
            }
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
            ClearRecentPool(MaleBattlecryPool);
            ClearRecentPool(FemaleBattlecryPool);

            string pluginDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            if (String.IsNullOrEmpty(pluginDirectory))
            {
                return;
            }

            string battlecryDirectory = Path.Combine(
                Path.Combine(pluginDirectory, "audio"),
                "battlecry");
            DiscoverVoiceFiles(
                battlecryDirectory,
                "hero_male_battlecry_*.wav",
                MaximumBattlecryFilesPerGender,
                _maleBattlecryPaths);
            DiscoverVoiceFiles(
                battlecryDirectory,
                "hero_female_battlecry_*.wav",
                MaximumBattlecryFilesPerGender,
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

        private void DiscoverCommandFiles()
        {
            _maleSummonAttackPaths.Clear();
            _maleSummonHoldPaths.Clear();
            _maleSummonFollowPaths.Clear();
            _maleSummonRecallPaths.Clear();
            _maleSummonGuardPaths.Clear();
            _maleSummonBulwarkPaths.Clear();
            _maleSummonHuntPaths.Clear();
            _femaleSummonAttackPaths.Clear();
            _femaleSummonHoldPaths.Clear();
            _femaleSummonFollowPaths.Clear();
            _femaleSummonRecallPaths.Clear();
            _femaleSummonGuardPaths.Clear();
            _femaleSummonBulwarkPaths.Clear();
            _femaleSummonHuntPaths.Clear();
            ClearRecentPool(MaleSummonAttackPool);
            ClearRecentPool(MaleSummonHoldPool);
            ClearRecentPool(MaleSummonFollowPool);
            ClearRecentPool(MaleSummonRecallPool);
            ClearRecentPool(MaleSummonGuardPool);
            ClearRecentPool(MaleSummonBulwarkPool);
            ClearRecentPool(MaleSummonHuntPool);
            ClearRecentPool(FemaleSummonAttackPool);
            ClearRecentPool(FemaleSummonHoldPool);
            ClearRecentPool(FemaleSummonFollowPool);
            ClearRecentPool(FemaleSummonRecallPool);
            ClearRecentPool(FemaleSummonGuardPool);
            ClearRecentPool(FemaleSummonBulwarkPool);
            ClearRecentPool(FemaleSummonHuntPool);

            string pluginDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            if (String.IsNullOrEmpty(pluginDirectory))
            {
                return;
            }

            string commandDirectory = Path.Combine(
                Path.Combine(pluginDirectory, "audio"),
                "command");
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_male_attack_*.wav",
                MaximumCommandFilesPerPool,
                _maleSummonAttackPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_male_hold_*.wav",
                MaximumCommandFilesPerPool,
                _maleSummonHoldPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_male_follow_*.wav",
                MaximumCommandFilesPerPool,
                _maleSummonFollowPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_male_recall_*.wav",
                MaximumCommandFilesPerPool,
                _maleSummonRecallPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_female_attack_*.wav",
                MaximumCommandFilesPerPool,
                _femaleSummonAttackPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_female_hold_*.wav",
                MaximumCommandFilesPerPool,
                _femaleSummonHoldPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_female_follow_*.wav",
                MaximumCommandFilesPerPool,
                _femaleSummonFollowPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_female_recall_*.wav",
                MaximumCommandFilesPerPool,
                _femaleSummonRecallPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_male_guard_*.wav",
                MaximumCommandFilesPerPool,
                _maleSummonGuardPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_male_bulwark_*.wav",
                MaximumCommandFilesPerPool,
                _maleSummonBulwarkPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_male_hunt_*.wav",
                MaximumCommandFilesPerPool,
                _maleSummonHuntPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_female_guard_*.wav",
                MaximumCommandFilesPerPool,
                _femaleSummonGuardPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_female_bulwark_*.wav",
                MaximumCommandFilesPerPool,
                _femaleSummonBulwarkPaths);
            DiscoverVoiceFiles(
                commandDirectory,
                "summon_female_hunt_*.wav",
                MaximumCommandFilesPerPool,
                _femaleSummonHuntPaths);

            _log.LogInfo(
                "Discovered summon command WAV files: male attack="
                + _maleSummonAttackPaths.Count.ToString(
                    CultureInfo.InvariantCulture)
                + ", hold="
                + _maleSummonHoldPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", follow="
                + _maleSummonFollowPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", recall="
                + _maleSummonRecallPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", guard="
                + _maleSummonGuardPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", bulwark="
                + _maleSummonBulwarkPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", hunt="
                + _maleSummonHuntPaths.Count.ToString(CultureInfo.InvariantCulture)
                + "; female attack="
                + _femaleSummonAttackPaths.Count.ToString(
                    CultureInfo.InvariantCulture)
                + ", hold="
                + _femaleSummonHoldPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", follow="
                + _femaleSummonFollowPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", recall="
                + _femaleSummonRecallPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", guard="
                + _femaleSummonGuardPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", bulwark="
                + _femaleSummonBulwarkPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ", hunt="
                + _femaleSummonHuntPaths.Count.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private static void DiscoverVoiceFiles(
            string directory,
            string searchPattern,
            int maximumFiles,
            List<string> destination)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            string[] paths = Directory.GetFiles(
                directory,
                searchPattern,
                SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            int count = Math.Min(
                maximumFiles,
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
            if (paths.Count == 0
                && _maleBattlecryPaths.Count == 0
                && _femaleBattlecryPaths.Count == 0)
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

            string pool = female
                ? FemaleBattlecryPool
                : MaleBattlecryPool;
            List<string> playbackOrder = BuildPlaybackOrder(
                paths,
                pool,
                GetRecentMemory(_recentBattlecryMemory));
            foreach (string path in playbackOrder)
            {
                float candidatePitch = SemitonesToPitchMultiplier(
                    GetShiftedSemitones(
                        female
                            ? _femaleBattlecryPitchOffsetSemitones.Value
                            : _maleBattlecryPitchOffsetSemitones.Value,
                        hero));
                if (!TryPlayBattlecrySound(
                    path,
                    candidatePitch,
                    hero))
                {
                    continue;
                }

                RememberRecentPath(
                    pool,
                    path,
                    GetRecentMemory(_recentBattlecryMemory));
                selectedPath = path;
                pitch = candidatePitch;
                return true;
            }

            return false;
        }

        private bool TryPlayCommand(
            Hero hero,
            string commandId)
        {
            if (_enabled == null
                || !_enabled.Value
                || _commandVoiceEnabled == null
                || !_commandVoiceEnabled.Value
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || Time.timeScale <= 0f
                || !IsSupportedSummonCommand(commandId))
            {
                return false;
            }

            float cooldown = _commandVoiceCooldownSeconds == null
                ? 0.75f
                : Math.Max(0f, _commandVoiceCooldownSeconds.Value);
            if (Time.time - _lastCommandVoiceTime < cooldown)
            {
                LogDiagnostic(
                    "Command voice ignored during its cooldown; command="
                    + commandId
                    + ".");
                return false;
            }

            bool female = hero.GetGender() == Gender.Female;
            GetCommandPool(
                female,
                commandId,
                out List<string> paths,
                out string pool);
            if (paths.Count == 0 && !HasAnyCommandFiles())
            {
                DiscoverCommandFiles();
                GetCommandPool(female, commandId, out paths, out pool);
            }
            if (paths.Count == 0)
            {
                if (!_noCommandFilesWarningLogged)
                {
                    _noCommandFilesWarningLogged = true;
                    _log.LogWarning(
                        "No playable " + commandId
                        + " command WAV is available for the current player gender in audio\\command.");
                }
                return false;
            }

            List<string> playbackOrder = BuildPlaybackOrder(
                paths,
                pool,
                GetRecentMemory(_recentCommandVoiceMemory));
            foreach (string path in playbackOrder)
            {
                float pitch = SemitonesToPitchMultiplier(
                    GetShiftedSemitones(
                        female
                            ? _femaleCommandVoicePitchOffsetSemitones.Value
                            : _maleCommandVoicePitchOffsetSemitones.Value,
                        hero));
                if (!TryPlayCommandSound(path, pitch, hero))
                {
                    continue;
                }

                RememberRecentPath(
                    pool,
                    path,
                    GetRecentMemory(_recentCommandVoiceMemory));
                _lastCommandVoiceTime = Time.time;
                _noCommandFilesWarningLogged = false;
                LogDiagnostic(
                    "Played command voice; command="
                    + commandId
                    + "; file="
                    + Path.GetFileName(path)
                    + "; pitch="
                    + pitch.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
                return true;
            }

            return false;
        }

        private bool HasAnyCommandFiles()
        {
            return _maleSummonAttackPaths.Count > 0
                || _maleSummonHoldPaths.Count > 0
                || _maleSummonFollowPaths.Count > 0
                || _maleSummonRecallPaths.Count > 0
                || _maleSummonGuardPaths.Count > 0
                || _maleSummonBulwarkPaths.Count > 0
                || _maleSummonHuntPaths.Count > 0
                || _femaleSummonAttackPaths.Count > 0
                || _femaleSummonHoldPaths.Count > 0
                || _femaleSummonFollowPaths.Count > 0
                || _femaleSummonRecallPaths.Count > 0
                || _femaleSummonGuardPaths.Count > 0
                || _femaleSummonBulwarkPaths.Count > 0
                || _femaleSummonHuntPaths.Count > 0;
        }

        private void GetCommandPool(
            bool female,
            string commandId,
            out List<string> paths,
            out string pool)
        {
            if (String.Equals(
                commandId,
                SummonHoldCommandId,
                StringComparison.OrdinalIgnoreCase))
            {
                paths = female
                    ? _femaleSummonHoldPaths
                    : _maleSummonHoldPaths;
                pool = female ? FemaleSummonHoldPool : MaleSummonHoldPool;
                return;
            }
            if (String.Equals(
                commandId,
                SummonFollowCommandId,
                StringComparison.OrdinalIgnoreCase))
            {
                paths = female
                    ? _femaleSummonFollowPaths
                    : _maleSummonFollowPaths;
                pool = female ? FemaleSummonFollowPool : MaleSummonFollowPool;
                return;
            }
            if (String.Equals(
                commandId,
                SummonRecallCommandId,
                StringComparison.OrdinalIgnoreCase))
            {
                paths = female
                    ? _femaleSummonRecallPaths
                    : _maleSummonRecallPaths;
                pool = female ? FemaleSummonRecallPool : MaleSummonRecallPool;
                return;
            }
            if (String.Equals(
                commandId,
                SummonGuardCommandId,
                StringComparison.OrdinalIgnoreCase))
            {
                paths = female
                    ? _femaleSummonGuardPaths
                    : _maleSummonGuardPaths;
                pool = female ? FemaleSummonGuardPool : MaleSummonGuardPool;
                return;
            }
            if (String.Equals(
                commandId,
                SummonBulwarkCommandId,
                StringComparison.OrdinalIgnoreCase))
            {
                paths = female
                    ? _femaleSummonBulwarkPaths
                    : _maleSummonBulwarkPaths;
                pool = female
                    ? FemaleSummonBulwarkPool
                    : MaleSummonBulwarkPool;
                return;
            }
            if (String.Equals(
                commandId,
                SummonHuntCommandId,
                StringComparison.OrdinalIgnoreCase))
            {
                paths = female
                    ? _femaleSummonHuntPaths
                    : _maleSummonHuntPaths;
                pool = female ? FemaleSummonHuntPool : MaleSummonHuntPool;
                return;
            }
            paths = female
                ? _femaleSummonAttackPaths
                : _maleSummonAttackPaths;
            pool = female ? FemaleSummonAttackPool : MaleSummonAttackPool;
        }

        private static bool IsSupportedSummonCommand(string commandId)
        {
            return String.Equals(
                    commandId,
                    SummonAttackCommandId,
                    StringComparison.OrdinalIgnoreCase)
                || String.Equals(
                    commandId,
                    SummonHoldCommandId,
                    StringComparison.OrdinalIgnoreCase)
                || String.Equals(
                    commandId,
                    SummonFollowCommandId,
                    StringComparison.OrdinalIgnoreCase)
                || String.Equals(
                    commandId,
                    SummonRecallCommandId,
                    StringComparison.OrdinalIgnoreCase)
                || String.Equals(
                    commandId,
                    SummonGuardCommandId,
                    StringComparison.OrdinalIgnoreCase)
                || String.Equals(
                    commandId,
                    SummonBulwarkCommandId,
                    StringComparison.OrdinalIgnoreCase)
                || String.Equals(
                    commandId,
                    SummonHuntCommandId,
                    StringComparison.OrdinalIgnoreCase);
        }

        private List<string> BuildPlaybackOrder(
            List<string> paths,
            string pool,
            int recentMemory)
        {
            List<string> preferred = new List<string>();
            List<string> fallback = new List<string>();
            List<string> recent = null;
            bool hasRecent = recentMemory > 0
                && _recentPathsByPool.TryGetValue(pool, out recent)
                && recent.Count > 0;
            foreach (string path in paths)
            {
                if (hasRecent && ContainsPath(recent, path))
                {
                    fallback.Add(path);
                }
                else
                {
                    preferred.Add(path);
                }
            }

            Shuffle(preferred);
            Shuffle(fallback);
            if (preferred.Count == 0
                && fallback.Count > 1
                && recent != null
                && recent.Count > 0)
            {
                string mostRecent = recent[recent.Count - 1];
                int mostRecentIndex = fallback.FindIndex(
                    path => String.Equals(
                        path,
                        mostRecent,
                        StringComparison.OrdinalIgnoreCase));
                if (mostRecentIndex >= 0)
                {
                    fallback.RemoveAt(mostRecentIndex);
                    fallback.Add(mostRecent);
                }
            }
            preferred.AddRange(fallback);
            return preferred;
        }

        private void Shuffle(List<string> paths)
        {
            for (int index = paths.Count - 1; index > 0; index--)
            {
                int other = _random.Next(index + 1);
                string value = paths[index];
                paths[index] = paths[other];
                paths[other] = value;
            }
        }

        private void RememberRecentPath(
            string pool,
            string path,
            int recentMemory)
        {
            if (recentMemory <= 0)
            {
                ClearRecentPool(pool);
                return;
            }

            List<string> recent;
            if (!_recentPathsByPool.TryGetValue(pool, out recent))
            {
                recent = new List<string>();
                _recentPathsByPool[pool] = recent;
            }
            recent.RemoveAll(item => String.Equals(
                item,
                path,
                StringComparison.OrdinalIgnoreCase));
            recent.Add(path);
            while (recent.Count > recentMemory)
            {
                recent.RemoveAt(0);
            }
        }

        private void ClearRecentPool(string pool)
        {
            _recentPathsByPool.Remove(pool);
        }

        private static bool ContainsPath(
            List<string> paths,
            string candidate)
        {
            return paths.Exists(path => String.Equals(
                path,
                candidate,
                StringComparison.OrdinalIgnoreCase));
        }

        private static int GetRecentMemory(ConfigEntry<int> entry)
        {
            return entry == null
                ? 2
                : Math.Max(0, Math.Min(20, entry.Value));
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
                RESULT pitchResult = ApplyPitchProcessingToChannel(
                    channel,
                    pitch,
                    "Battlecry");
                RESULT unpauseResult = channel.setPaused(false);
                if (unpauseResult == RESULT.OK
                    && acousticProfile != null)
                {
                    ScheduleAcousticReflections(
                        sound,
                        pitch,
                        volumeScale,
                        acousticProfile,
                        "Battlecry",
                        MaximumOutdoorReflectionTaps);
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

        private bool TryPlayCommandSound(
            string path,
            float pitch,
            Hero hero)
        {
            try
            {
                FMOD.Sound sound;
                if (!_commandSoundsByPath.TryGetValue(path, out sound))
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
                            "FMOD could not load command voice "
                            + path
                            + ": "
                            + createResult
                            + ".");
                        return false;
                    }

                    _commandSoundsByPath[path] = sound;
                }

                FMOD.ChannelGroup channelGroup;
                string environment;
                float reverbAmount;
                BattlecryAcousticProfile acousticProfile;
                if (!TryGetCommandChannelGroup(
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
                        "FMOD could not play command voice "
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
                        _commandVoiceVolumeMultiplier.Value);
                RESULT volumeResult = channel.setVolume(volumeScale);
                RESULT pitchResult = ApplyPitchProcessingToChannel(
                    channel,
                    pitch,
                    "Command voice");
                RESULT unpauseResult = channel.setPaused(false);
                if (unpauseResult == RESULT.OK
                    && acousticProfile != null)
                {
                    ScheduleAcousticReflections(
                        sound,
                        pitch,
                        volumeScale,
                        acousticProfile,
                        "Command voice",
                        MaximumCommandReflectionTaps);
                }
                LogDiagnostic(
                    "Command voice FMOD results: volume="
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
                    "Command voice playback failed for "
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

        private bool TryGetCommandChannelGroup(
            Hero hero,
            out FMOD.ChannelGroup channelGroup,
            out string environment,
            out float reverbAmount,
            out BattlecryAcousticProfile acousticProfile)
        {
            acousticProfile = null;
            bool indoors = IsBattlecryIndoors(out environment);
            if (!TryGetBattlecrySfxChannelGroup(out channelGroup))
            {
                reverbAmount = 0f;
                return false;
            }

            reverbAmount = 0f;
            if (_commandVoiceReverbEnabled == null
                || !_commandVoiceReverbEnabled.Value)
            {
                return true;
            }

            float configuredAmount = Math.Max(
                0f,
                Math.Min(
                    1f,
                    indoors
                        ? _indoorCommandVoiceReverbAmount.Value
                        : _outdoorCommandVoiceReverbAmount.Value));
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

            if (!TryEnsureCommandReverbPaths())
            {
                _log.LogWarning(
                    "Command voice reverb setup failed; playing this command dry through the game's SFX bus.");
                reverbAmount = 0f;
                return true;
            }

            FMOD.DSP reverb = indoors
                ? _indoorCommandReverb
                : _outdoorCommandReverb;
            if (!TryConfigureBattlecryReverb(
                reverb,
                reverbAmount,
                acousticProfile))
            {
                reverb.setBypass(true);
                _log.LogWarning(
                    "Command voice reverb parameters could not be applied; playing this command dry through the game's SFX bus.");
                reverbAmount = 0f;
                return true;
            }

            channelGroup = indoors
                ? _indoorCommandChannelGroup
                : _outdoorCommandChannelGroup;
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
            BattlecryAcousticProfile profile,
            string sourceLabel,
            int maximumTaps)
        {
            if (maximumTaps <= 0
                || profile.Reflections.Count == 0
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
                    sourceLabel
                    + " reflections skipped because FMOD's output sample rate was unavailable. Result="
                    + formatResult
                    + ".");
                return;
            }

            int scheduled = 0;
            for (int index = 0;
                index < profile.Reflections.Count
                    && scheduled < maximumTaps;
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
                    result = ApplyPitchProcessingToChannel(
                        reflectionChannel,
                        pitch,
                        sourceLabel + " reflection");
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
                        sourceLabel
                        + " reflection setup failed. Result="
                        + result
                        + ".");
                }
            }

            LogDiagnostic(
                "Scheduled "
                + scheduled.ToString(CultureInfo.InvariantCulture)
                + " "
                + profile.Environment
                + " "
                + sourceLabel.ToLowerInvariant()
                + " reflection tap(s).");
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

        private bool TryEnsureCommandReverbPaths()
        {
            if (_outdoorCommandChannelGroup.hasHandle()
                && _indoorCommandChannelGroup.hasHandle()
                && _outdoorCommandReverb.hasHandle()
                && _indoorCommandReverb.hasHandle())
            {
                return true;
            }

            ReleaseCommandReverbPaths();
            if (!TryCreateBattlecryReverbPath(
                    "Battlecry Voice Tuner - Command Outdoor",
                    out _outdoorCommandChannelGroup,
                    out _outdoorCommandReverb)
                || !TryCreateBattlecryReverbPath(
                    "Battlecry Voice Tuner - Command Indoor",
                    out _indoorCommandChannelGroup,
                    out _indoorCommandReverb))
            {
                ReleaseCommandReverbPaths();
                return false;
            }

            LogDiagnostic(
                "Created separate reusable outdoor and indoor command reverb paths under the game's SFX bus.");
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

        private void ReleaseCommandReverbPaths()
        {
            ReleaseBattlecryReverbPath(
                ref _outdoorCommandChannelGroup,
                ref _outdoorCommandReverb);
            ReleaseBattlecryReverbPath(
                ref _indoorCommandChannelGroup,
                ref _indoorCommandReverb);
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
            ReleaseCommandReverbPaths();
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

        private void ReleaseCommandSounds()
        {
            foreach (KeyValuePair<string, FMOD.Sound> pair
                in _commandSoundsByPath)
            {
                try
                {
                    pair.Value.release();
                }
                catch
                {
                }
            }

            _commandSoundsByPath.Clear();
            _maleSummonAttackPaths.Clear();
            _maleSummonHoldPaths.Clear();
            _maleSummonFollowPaths.Clear();
            _maleSummonRecallPaths.Clear();
            _maleSummonGuardPaths.Clear();
            _maleSummonBulwarkPaths.Clear();
            _maleSummonHuntPaths.Clear();
            _femaleSummonAttackPaths.Clear();
            _femaleSummonHoldPaths.Clear();
            _femaleSummonFollowPaths.Clear();
            _femaleSummonRecallPaths.Clear();
            _femaleSummonGuardPaths.Clear();
            _femaleSummonBulwarkPaths.Clear();
            _femaleSummonHuntPaths.Clear();
            _recentPathsByPool.Clear();
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
                bool found = false;
                if (bindings != null)
                {
                    foreach (KeyBindings binding in bindings)
                    {
                        found = found || object.Equals(
                            binding,
                            KeyBindings.UI.Items.TransferItems);
                        yield return binding;
                    }
                }
                if (!found)
                {
                    yield return KeyBindings.UI.Items.TransferItems;
                }
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

        private sealed class VoicePitchProcessing
        {
            internal readonly float FinalSemitones;
            internal readonly float RateSemitones;
            internal readonly float DspSemitones;
            internal readonly float FinalMultiplier;
            internal readonly float RateMultiplier;
            internal readonly float DspMultiplier;

            internal bool UsesDsp
            {
                get
                {
                    return Math.Abs(DspSemitones)
                        >= PitchDspMinimumSemitones;
                }
            }

            internal VoicePitchProcessing(
                float finalSemitones,
                float rateSemitones,
                float dspSemitones,
                float finalMultiplier,
                float rateMultiplier,
                float dspMultiplier)
            {
                FinalSemitones = finalSemitones;
                RateSemitones = rateSemitones;
                DspSemitones = dspSemitones;
                FinalMultiplier = finalMultiplier;
                RateMultiplier = rateMultiplier;
                DspMultiplier = dspMultiplier;
            }
        }

        private sealed class ActiveChannelPitchDsp
        {
            internal readonly FMOD.Channel Channel;
            internal readonly FMOD.DSP Dsp;

            internal ActiveChannelPitchDsp(
                FMOD.Channel channel,
                FMOD.DSP dsp)
            {
                Channel = channel;
                Dsp = dsp;
            }
        }

        private sealed class PendingEventPitchDsp
        {
            internal readonly EventInstance EventInstance;
            internal readonly VoicePitchProcessing Processing;
            internal readonly string Label;
            internal readonly float QueuedAt;

            internal PendingEventPitchDsp(
                EventInstance eventInstance,
                VoicePitchProcessing processing,
                string label,
                float queuedAt)
            {
                EventInstance = eventInstance;
                Processing = processing;
                Label = label;
                QueuedAt = queuedAt;
            }
        }

        private sealed class ActiveEventPitchDsp
        {
            internal readonly EventInstance EventInstance;
            internal readonly FMOD.ChannelGroup ChannelGroup;
            internal readonly FMOD.DSP Dsp;

            internal ActiveEventPitchDsp(
                EventInstance eventInstance,
                FMOD.ChannelGroup channelGroup,
                FMOD.DSP dsp)
            {
                EventInstance = eventInstance;
                ChannelGroup = channelGroup;
                Dsp = dsp;
            }
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
