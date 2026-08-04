using System;
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
using Grailwright.Shared;
using HarmonyLib;

[assembly: AssemblyTitle("Player Voice Tuner")]
[assembly: AssemblyDescription("Tunes player nonverbal voice pitch in Tainted Grail: The Fall of Avalon.")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Player Voice Tuner")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("0.2.2.0")]
[assembly: AssemblyFileVersion("0.2.2.0")]

namespace PlayerVoiceTuner
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class PlayerVoiceTunerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.player-voice-tuner";
        public const string PluginName = "Player Voice Tuner";
        public const string PluginVersion = "0.2.2";

        private const int CurrentConfigSchemaVersion = 1;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new[]
            {
                new ConfigDefinition(
                    "3. Testing",
                    "PlayRandomTestSound")
            };

        private const string CategoryAttack = "Attack";
        private const string CategoryHurt = "Hurt";
        private const string CategoryDeath = "Death";
        private const string CategoryStatus = "Status";
        private const string CategoryHitFeedback = "HitFeedback";
        private const string CategoryStamina = "Stamina";

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

        private static PlayerVoiceTunerPlugin _instance;

        private readonly Random _random = new Random();

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
        private ConfigEntry<bool> _playRandomTestSound;
        private ConfigEntry<bool> _diagnostics;
        private readonly Dictionary<string, float> _pendingPreservedVoiceTuning =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;
        private bool _resettingTestButton;

        private void Awake()
        {
            _instance = this;
            _log = Logger;

            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
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
                "Master toggle for player voice pitch tuning.");

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

            _playRandomTestSound = Config.Bind(
                "3. Testing",
                "PlayRandomTestSound",
                false,
                "Pseudo-button. Toggle on to play one random supported one-shot sound, then the mod resets this to false.");
            _playRandomTestSound.SettingChanged += OnPlayRandomTestSoundChanged;

            _diagnostics = Config.Bind(
                "4. Diagnostics",
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
        }

        private static void TuneCreatedEvent(EventDescription eventDescription, ref EventInstance eventInstance)
        {
            PlayerVoiceTunerPlugin instance = _instance;
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
