using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.MVC;
using Awaken.TG.MVC.UI;
using Awaken.TG.MVC.UI.Events;
using Awaken.TG.Main.AI;
using Awaken.TG.Main.Character.Features;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Utility;
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
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace BattlecryVoiceTuner
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.eyes-in-the-dark", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BattlecryVoiceTunerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.battlecry-voice-tuner";
        public const string PluginName = "Battlecry Voice Tuner";
        public const string PluginVersion = "1.0.0";

        private const int CurrentConfigSchemaVersion = 1;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
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
        private const int MaximumBattlecryFilesPerGender = 10;
        private const float ChallengeScanIntervalSeconds = 0.25f;
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
        private ConfigEntry<bool> _holdToggleWeaponForBattlecry;
        private ConfigEntry<float> _battlecryHoldSeconds;
        private ConfigEntry<KeyboardShortcut> _battlecryHotkey;
        private ConfigEntry<float> _battlecryCooldownSeconds;
        private ConfigEntry<float> _battlecryAggroRangeMultiplier;
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
        private bool _toggleWeaponHeld;
        private bool _battlecryAttemptedForHold;
        private bool _battlecryTriggeredForHold;
        private float _toggleWeaponPressedAt;
        private bool _eyesApiResolved;
        private MethodInfo _eyesBattlecryMethod;
        private bool _noBattlecryFilesWarningLogged;
        private bool _resettingTestButton;

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
                    new System.ComponentModel.BrowsableAttribute(false)));

            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Master toggle for player voice tuning and battlecries.");

            _pitchSemitones = Config.Bind(
                "1. Core",
                "PitchSemitones",
                -3.0f,
                new ConfigDescription(
                    "Pitch shift applied to supported player voice sounds. Negative values lower the voice; positive values raise it.",
                    new AcceptableValueRange<float>(-12.0f, 12.0f)));

            _randomPitchSemitones = Config.Bind(
                "1. Core",
                "RandomPitchSemitones",
                0.25f,
                new ConfigDescription(
                    "Maximum random pitch variation added per played sound, in semitones. Set 0 for a fixed pitch.",
                    new AcceptableValueRange<float>(0.0f, 3.0f)));

            _volumeMultiplier = Config.Bind(
                "1. Core",
                "VolumeMultiplier",
                1.0f,
                new ConfigDescription(
                    "Volume multiplier for supported player voice sounds.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            _includeAttackGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeAttackGrunts",
                true,
                "Tune player attack/exertion grunts.");

            _includeHurtGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeHurtGrunts",
                true,
                "Tune player hurt grunts.");

            _includeDeathGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeDeathGrunts",
                true,
                "Tune player death grunts.");

            _includeStatusPainGrunts = Config.Bind(
                "2. Supported Sounds",
                "IncludeStatusPainGrunts",
                true,
                "Tune player burn, bleed, poison, and drown grunts.");

            _includePlayerHitFeedback = Config.Bind(
                "2. Supported Sounds",
                "IncludePlayerHitFeedback",
                true,
                "Tune SFX_Player_Hit, the player hit-feedback sound used when the player lands a hit.");

            _includeStaminaDepletedBreathing = Config.Bind(
                "2. Supported Sounds",
                "IncludeStaminaDepletedBreathing",
                false,
                "Tune stamina-depleted breathing loops. Off by default because these are longer/looping sounds.");

            _battlecryEnabled = Config.Bind(
                "3. Battlecry",
                "BattlecryEnabled",
                true,
                "Enable custom battlecry audio and its enemy challenge effect.");

            _holdToggleWeaponForBattlecry = Config.Bind(
                "3. Battlecry",
                "HoldToggleWeaponForBattlecry",
                true,
                "Tap the game's Toggle Weapon action to keep its normal behavior, or hold it to battlecry. Uses the game's current remapped keyboard or controller binding.");

            _battlecryHoldSeconds = Config.Bind(
                "3. Battlecry",
                "BattlecryHoldSeconds",
                0.45f,
                new ConfigDescription(
                    "Seconds the Toggle Weapon action must be held before attempting a battlecry.",
                    new AcceptableValueRange<float>(0.2f, 2.0f)));

            _battlecryHotkey = Config.Bind(
                "3. Battlecry",
                "BattlecryHotkey",
                new KeyboardShortcut(KeyCode.None),
                "Optional separate keyboard or joystick-button shortcut. None disables the separate shortcut.");

            _battlecryCooldownSeconds = Config.Bind(
                "3. Battlecry",
                "BattlecryCooldownSeconds",
                3.0f,
                new ConfigDescription(
                    "Minimum active gameplay seconds between battlecries.",
                    new AcceptableValueRange<float>(0.0f, 30.0f)));

            _battlecryAggroRangeMultiplier = Config.Bind(
                "3. Battlecry",
                "BattlecryAggroRangeMultiplier",
                2.0f,
                new ConfigDescription(
                    "Multiplier applied to each hostile NPC's normal maximum hearing range while resolving the battlecry challenge.",
                    new AcceptableValueRange<float>(0.0f, 5.0f)));

            _battlecryAggroDurationSeconds = Config.Bind(
                "3. Battlecry",
                "BattlecryAggroDurationSeconds",
                3.0f,
                new ConfigDescription(
                    "Active gameplay seconds during which newly reached hostile NPCs can hear the challenge.",
                    new AcceptableValueRange<float>(0.1f, 10.0f)));

            _eyesInTheDarkThreat = Config.Bind(
                "3. Battlecry",
                "EyesInTheDarkThreat",
                20.0f,
                new ConfigDescription(
                    "Wyrd Threat requested from Eyes in the Dark for each successful battlecry. Has no effect when Eyes is absent or its Wyrdnight activity rules reject the request.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));

            _playRandomTestSound = Config.Bind(
                "4. Testing",
                "PlayRandomTestSound",
                false,
                "Pseudo-button. Toggle on to play one random supported one-shot sound, then the mod resets this to false.");
            _playRandomTestSound.SettingChanged += OnPlayRandomTestSoundChanged;

            _diagnostics = Config.Bind(
                "5. Diagnostics",
                "Diagnostics",
                false,
                "Write detailed match and FMOD result information to the BepInEx log.");

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
            _log.LogInfo(
                "Patched the remappable Toggle Weapon action for tap-or-hold battlecry input.");
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

        private float GetShiftedSemitones()
        {
            float semitones = _pitchSemitones.Value;
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

        private bool HandleToggleWeaponInput(
            UIEvent inputEvent,
            ref UIResult result)
        {
            if (_enabled == null
                || !_enabled.Value
                || _battlecryEnabled == null
                || !_battlecryEnabled.Value
                || _holdToggleWeaponForBattlecry == null
                || !_holdToggleWeaponForBattlecry.Value)
            {
                return true;
            }

            UIKeyAction keyAction = inputEvent as UIKeyAction;
            if (keyAction == null
                || !String.Equals(
                    keyAction.Name,
                    KeyBindings.Gameplay.ToggleWeapon,
                    StringComparison.Ordinal))
            {
                return true;
            }

            Hero hero = Hero.Current;
            if (hero == null || hero.HasBeenDiscarded || !hero.IsAlive)
            {
                ResetToggleWeaponHold();
                return true;
            }

            if (inputEvent is UIKeyDownAction)
            {
                _toggleWeaponHeld = true;
                _battlecryAttemptedForHold = false;
                _battlecryTriggeredForHold = false;
                _toggleWeaponPressedAt = Time.unscaledTime;
                result = UIResult.Accept;
                return false;
            }

            if (!_toggleWeaponHeld)
            {
                return true;
            }

            if (inputEvent is UIKeyHeldAction)
            {
                float holdSeconds = _battlecryHoldSeconds == null
                    ? 0.45f
                    : Math.Max(0.2f, _battlecryHoldSeconds.Value);
                if (!_battlecryAttemptedForHold
                    && Time.unscaledTime - _toggleWeaponPressedAt
                        >= holdSeconds)
                {
                    _battlecryAttemptedForHold = true;
                    _battlecryTriggeredForHold =
                        TryPerformBattlecry(
                            hero,
                            "held Toggle Weapon action");
                }

                result = UIResult.Accept;
                return false;
            }

            if (inputEvent is UIKeyUpAction)
            {
                bool toggleWeapon = !_battlecryTriggeredForHold;
                ResetToggleWeaponHold();
                if (toggleWeapon)
                {
                    ToggleHeroWeapon(hero);
                }

                result = UIResult.Accept;
                return false;
            }

            return true;
        }

        private static void ToggleHeroWeapon(Hero hero)
        {
            if (hero == null || hero.HasBeenDiscarded || !hero.IsAlive)
            {
                return;
            }

            ModelExtensions.Trigger(
                hero,
                hero.IsWeaponEquipped
                    ? Hero.Events.HideWeapons
                    : Hero.Events.ShowWeapons,
                false);
        }

        private void ResetToggleWeaponHold()
        {
            _toggleWeaponHeld = false;
            _battlecryAttemptedForHold = false;
            _battlecryTriggeredForHold = false;
            _toggleWeaponPressedAt = 0f;
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
                    GetShiftedSemitones());
                if (!TryPlayBattlecrySound(
                    paths[index],
                    candidatePitch))
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
            float pitch)
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
                RESULT groupResult =
                    RuntimeManager.CoreSystem.getMasterChannelGroup(
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
                    _log.LogWarning(
                        "FMOD could not play battlecry "
                        + path
                        + ": "
                        + playResult
                        + ".");
                    return false;
                }

                RESULT volumeResult = channel.setVolume(
                    Math.Max(0f, _volumeMultiplier.Value));
                RESULT pitchResult = channel.setPitch(
                    Math.Max(0.01f, pitch));
                RESULT unpauseResult = channel.setPaused(false);
                LogDiagnostic(
                    "Battlecry FMOD results: volume="
                    + volumeResult
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
            float rangeMultiplier = Math.Max(
                0f,
                _battlecryAggroRangeMultiplier.Value);
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
                    ? 20f
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
                    || instance.HandleToggleWeaponInput(
                        evt,
                        ref __result);
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
