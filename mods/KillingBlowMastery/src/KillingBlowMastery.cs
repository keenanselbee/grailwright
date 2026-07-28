using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using FMODUnity;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[assembly: AssemblyTitle("Killing Blow Mastery")]
[assembly: AssemblyProduct("Killing Blow Mastery")]
[assembly: AssemblyVersion("1.4.5.0")]
[assembly: AssemblyFileVersion("1.4.5.0")]
[assembly: AssemblyInformationalVersion("1.4.5")]

namespace KillingBlowMastery
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(GrailFloatingTextPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class KillingBlowMasteryPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.killing-blow-mastery";
        public const string PluginName = "Killing Blow Mastery";
        public const string PluginVersion = "1.4.5";

        private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";
        private const string GrailFloatingTextKillingBlowEventId = "killing-blow";
        private const string GrailFloatingTextMediumDurationBucket = "Medium";
        private const string NpcElementTypeName = "Awaken.TG.Main.Fights.NPCs.NpcElement";
        private const string HealthElementTypeName = "Awaken.TG.Main.Character.HealthElement";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";
        private const string ItemTypeName = "Awaken.TG.Main.Heroes.Items.Item";
        private const string ProfUtilsTypeName = "Awaken.TG.Main.Heroes.Stats.Utils.ProfUtils";
        private const string ProfStatTypeName = "Awaken.TG.Main.General.StatTypes.ProfStatType";
        private const string WorldTypeName = "Awaken.TG.MVC.World";
        private const string GameplayMemoryTypeName = "Awaken.TG.Main.Memories.GameplayMemory";
        private const string IModelTypeName = "Awaken.TG.MVC.IModel";
        private const string NotificationUtilsTypeName = "Awaken.TG.Main.UI.HUD.AdvancedNotifications.NotificationUtils";
        private const string LowerInfoNotificationTypeName = "Awaken.TG.Main.UI.HUD.AdvancedNotifications.MiddleScreen.FancyPanel.LowerInfoNotification";
        private const string LowerInfoViewTypeName = "Awaken.TG.Main.UI.HUD.AdvancedNotifications.MiddleScreen.FancyPanel.VLowerInfoNotification";
        private const string WyrdInfoNotificationTypeName = "Awaken.TG.Main.UI.HUD.AdvancedNotifications.LeftScreen.WyrdInfo.WyrdInfoNotification";
        private const string GlobalSoundPool = "killing_blow";
        private const string OneHandedBladeSoundPool = "one_handed_blade";
        private const string OneHandedAxeSoundPool = "one_handed_axe";
        private const string OneHandedBluntSoundPool = "one_handed_blunt";
        private const string TwoHandedBladeSoundPool = "two_handed_blade";
        private const string TwoHandedAxeSoundPool = "two_handed_axe";
        private const string TwoHandedBluntSoundPool = "two_handed_blunt";
        private const string UnarmedSoundPool = "unarmed";
        private const string ArcheryShortSoundPool = "archery_short";
        private const string ArcheryMediumSoundPool = "archery_medium";
        private const string ArcheryHeavySoundPool = "archery_heavy";
        private const string ShieldBashSoundPool = "shield_bash";
        private const string MagicBloodSoundPool = "magic_blood";
        private const string MagicFireSoundPool = "magic_fire";
        private const string MagicFrostSoundPool = "magic_frost";
        private const string MagicPoisonSoundPool = "magic_poison";
        private const string MagicElectricSoundPool = "magic_electric";
        private const string MagicWyrdnessSoundPool = "magic_wyrdness";
        private const string MagicWaterSoundPool = "magic_water";
        private const string MagicArcaneSoundPool = "magic_arcane";
        private const string NonCorporealSoundPool = "non_corporeal";
        private const string BloodlessSoundPoolSuffix = "_bloodless";
        private const string BloodlessSoundFileSuffix = "_dry";
        private const string DefaultBloodlessSoundBlacklistTerms = "Stone;Golem;Statue;Construct;Automaton;Crystal;Wisp;Spirit;Ghost;Wraith;Specter;Spectre;Skeleton;Skull;Bone;Animated Armor;Elemental;Wyrdspawn;Wyrdspirit;Wyrd Spirit;WyrdSlime;Wyrd Slime;Wyrdness";
        private const string DefaultNonCorporealSoundTerms =
            "Wyrdspirit;" +
            "EnemyMonster_T1_Wyrdspawn;EnemyMonster_T2_Wyrdspawn;EnemyMonster_T2better_Wyrdspawn;" +
            "EnemyMonster_T3_Wyrdspawn;EnemyMonster_T3better_Wyrdspawn;EnemyMonster_T4_Wyrdspawn;" +
            "EnemyMonster_T4better_Wyrdspawn;EnemyMonster_T5_Wyrdspawn;EnemyMonster_T5better_Wyrdspawn;" +
            "EnemyMonster_T6_Wyrdspawn;EnemyMonster_T5_Wyrdspawn_IceTrial;" +
            "EnemyMonster_T2_Mistling_HoS;EnemyMonster_T2_Mistling_Mistbearer;" +
            "EnemyMonster_T3_Mistling_Cuanacht;EnemyMonster_T4_Mistling_Forlorn;" +
            "Enemy_Elite_Tier5_Banshee;Enemy_Elite_Tier5_Melancholy;Enemy_Elite_Tier5_MelancholySagremor;" +
            "EnemyMonster_T3_Ghost_LancelotSquire;EnemyMonster_T3_Ghost_SagremorAristocratFemale;" +
            "EnemyMonster_T3_Ghost_SagremorAristocratMale;EnemyMonster_T3_Ghost_SagremorServant;" +
            "EnemyMonster_T4_Ghost_BirthdayGuest;EnemyMonster_T5_DalRiataGunnvaldrGhost;" +
            "Special_Perceval_Ghost;EnemyMonster_T4_GhostInPainting";
        private const string DefaultNonCorporealSoundExclusionTerms =
            "EnemyBoss_T3_MistBearer_Base;EnemyBoss_T3_MistBearer_Mimic;SoS_EnemyMonster_T5_Tidewraith;" +
            "EnemyMonster_T4_WyrdspawnCharredConclave;EnemyMonster_T5_Wyrdspawn_ChallengeVariant;" +
            "Enemy_T0_Wyrdspawn_Tutorial";
        private const string DiagnosticGoatSoundPool = "goat";
        private const string DiagnosticGoatSoundFileName = "goat.wav";
        private const string SoulslikeKillingBlowSoundPool = "soulslike_killing_blow";
        private const string SoulslikeKillingBlowSoundFileName = "killing_blow1.wav";
        private const string FinisherSoundModeWeaponSpecific = "WeaponSpecific";
        private const string FinisherSoundModeSoulslike = "Soulslike";
        private const string FinisherSoundModeGoatTest = "GoatTest";
        private const string FinisherSoundModeOff = "Off";
        private const string EnemyStatsModePromoted = "Promoted";
        private const string EnemyStatsModeAll = "All";
        private const string EnemyStatsModeOff = "Off";
        private const string StatisticsMemoryContext = "KillingBlowMastery";
        private const string StatisticsFileName = "ks.tgfoa.killing-blow-mastery.stats.tsv";
        private const int DefaultRewardSoundSlots = 5;
        private const string AudioSourceObjectName = "Killing Blow Mastery Audio";
        private const string DefaultNotificationTextFormat = "Killing blow: +{xp} {skill}";
        private const int ConfigSchemaVersion = 12;

        internal static KillingBlowMasteryPlugin Instance;
        internal static ManualLogSource Log;

        private Harmony _harmony;
        private Type _heroType;
        private Type _itemType;
        private Type _profStatType;
        private Type _gameplayMemoryType;
        private Type _iModelType;
        private MethodInfo _heroCurrentGetter;
        private MethodInfo _worldServicesGetter;
        private MethodInfo _servicesGetGameplayMemoryMethod;
        private MethodInfo _gameplayMemoryContextMethod;
        private MethodInfo _factsGetGenericMethod;
        private MethodInfo _factsSetGenericMethod;
        private MethodInfo _factsGetAllMethod;
        private MethodInfo _profFromAbstractsMethod;
        private MethodInfo _tryAddXpMethod;
        private MethodInfo _notificationPushMethod;
        private MethodInfo _grailFloatingTextTryShowEventWithIconMethod;
        private MethodInfo _grailFloatingTextTryShowMethod;
        private MethodInfo _grailFloatingTextTryShowWithIconMethod;
        private ConstructorInfo _lowerInfoNotificationConstructor;
        private ConstructorInfo _wyrdInfoNotificationConstructor;
        private Type _lowerInfoViewType;
        private object _oneHandedProf;
        private object _twoHandedProf;
        private object _unarmedProf;
        private object _archeryProf;
        private object _shieldProf;
        private object _magicProf;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _bonusPercentOfEnemyXp;
        private ConfigEntry<float> _minimumBonusXp;
        private ConfigEntry<float> _maximumBonusXp;
        private ConfigEntry<float> _roundBonusXpTo;
        private ConfigEntry<float> _fallbackBonusXp;
        private ConfigEntry<bool> _allowOneHanded;
        private ConfigEntry<bool> _allowTwoHanded;
        private ConfigEntry<bool> _allowUnarmed;
        private ConfigEntry<bool> _allowArchery;
        private ConfigEntry<bool> _allowShield;
        private ConfigEntry<bool> _allowMagic;
        private ConfigEntry<bool> _requirePrimaryDamage;
        private ConfigEntry<bool> _allowDamageOverTimeKills;
        private ConfigEntry<float> _damageOverTimeMemorySeconds;
        private ConfigEntry<bool> _ignoreThrowable;
        private ConfigEntry<bool> _requireXpRewardAllowedWhenPresent;
        private ConfigEntry<string> _finisherSoundMode;
        private ConfigEntry<float> _finisherSoundRangeVolume;
        private ConfigEntry<bool> _notificationsEnabled;
        private ConfigEntry<float> _notificationMinimumXp;
        private ConfigEntry<string> _notificationTextFormat;
        private ConfigEntry<string> _notificationMode;
        private ConfigEntry<float> _rewardSoundVolume;
        private ConfigEntry<float> _rewardSoundCooldownSeconds;
        private ConfigEntry<bool> _useKillingBlowFallbackForClassifiedKills;
        private ConfigEntry<bool> _useNonCorporealEnemySounds;
        private ConfigEntry<string> _nonCorporealSoundTerms;
        private ConfigEntry<string> _nonCorporealSoundExclusionTerms;
        private ConfigEntry<bool> _useBloodlessSoundVariants;
        private ConfigEntry<string> _bloodlessSoundBlacklistTerms;
        private ConfigEntry<string> _bloodlessSoundWhitelistTerms;
        private ConfigEntry<bool> _avoidRecentSoundRepeats;
        private ConfigEntry<int> _recentSoundMemory;
        private ConfigEntry<float> _randomPitchSemitones;
        private ConfigEntry<bool> _trackStatistics;
        private ConfigEntry<string> _statisticsCharacterKeyOverride;
        private ConfigEntry<string> _enemyStatsMode;
        private ConfigEntry<int> _enemyPromoteKillCount;
        private ConfigEntry<bool> _exportStatisticsReportOnSave;
        private ConfigEntry<bool> _diagnostics;

        private readonly Dictionary<string, List<RewardSoundClip>> _rewardSoundClipsByPool = new Dictionary<string, List<RewardSoundClip>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<object, KillSourceMemory> _recentKillSourcesByKey = new Dictionary<object, KillSourceMemory>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, FMOD.Sound> _fmodSoundsByPath = new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<string>> _recentRewardSoundPathsByPool = new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Random _random = new System.Random();
        private AudioSource _rewardAudioSource;
        private bool _rewardSoundLoadStarted;
        private bool _statisticsMemoryUnavailableLogged;
        private bool _grailFloatingTextBridgeResolved;
        private bool _grailFloatingTextUnavailableLogged;
        private Array _emptyModelOwners;
        private float _lastRewardSoundTime = -9999.0f;
        private string _cachedBloodlessSoundBlacklistTermsRaw;
        private string[] _cachedBloodlessSoundBlacklistTerms = new string[0];
        private string _cachedBloodlessSoundWhitelistTermsRaw;
        private string[] _cachedBloodlessSoundWhitelistTerms = new string[0];
        private string _cachedNonCorporealSoundTermsRaw;
        private string[] _cachedNonCorporealSoundTerms = new string[0];
        private string _cachedNonCorporealSoundExclusionTermsRaw;
        private string[] _cachedNonCorporealSoundExclusionTerms = new string[0];

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();
            CacheGameAccessors();
            PatchGame();
            EnsureRewardSoundLoadStarted();

            Log.LogInfo(PluginName + " " + PluginVersion + " loaded. BonusPercentOfEnemyXP=" +
                _bonusPercentOfEnemyXp.Value.ToString("0.###", CultureInfo.InvariantCulture) +
                "; MaxBonusXP=" + _maximumBonusXp.Value.ToString("0.###", CultureInfo.InvariantCulture) + ".");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (_rewardAudioSource != null)
            {
                Destroy(_rewardAudioSource.gameObject);
                _rewardAudioSource = null;
            }

            ReleaseFmodRewardSounds();

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch.");
            Config.Bind("1. Core", "ConfigSchemaVersion", ConfigSchemaVersion, "Configuration layout version. It changes only when an update requires fresh defaults.");
            _finisherSoundMode = Config.Bind(
                "1. Core",
                "FinisherSoundMode",
                FinisherSoundModeWeaponSpecific,
                new ConfigDescription(
                    "Reward sound style: WeaponSpecific, Soulslike, GoatTest, or Off.",
                    new AcceptableValueList<string>(
                        FinisherSoundModeWeaponSpecific,
                        FinisherSoundModeSoulslike,
                        FinisherSoundModeGoatTest,
                        FinisherSoundModeOff)));
            _finisherSoundRangeVolume = Config.Bind(
                "1. Core",
                "FinisherSoundRangeVolume",
                0.5f,
                new ConfigDescription(
                    "How strongly finisher sounds fade with target distance. 0 disables distance fade; 1 uses the full 0m=100%, 100m+=10% curve.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));
            _bonusPercentOfEnemyXp = Config.Bind(
                "1. Core",
                "BonusPercentOfEnemyXP",
                4.0f,
                new ConfigDescription(
                    "Extra combat proficiency XP awarded on a killing blow, as a percent of the enemy's XP reward.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));
            _maximumBonusXp = Config.Bind(
                "1. Core",
                "MaximumBonusXP",
                100.0f,
                "Maximum extra proficiency XP from one killing blow. Zero or less disables the cap.");

            _allowOneHanded = Config.Bind("2. Weapon Skills", "AllowOneHanded", true, "Award One Handed proficiency from one-handed weapon killing blows.");
            _allowTwoHanded = Config.Bind("2. Weapon Skills", "AllowTwoHanded", true, "Award Two Handed proficiency from two-handed weapon killing blows.");
            _allowUnarmed = Config.Bind("2. Weapon Skills", "AllowUnarmed", true, "Award Unarmed proficiency from fist killing blows.");
            _allowArchery = Config.Bind("2. Weapon Skills", "AllowArchery", true, "Award Archery proficiency from bow killing blows.");
            _allowShield = Config.Bind("2. Weapon Skills", "AllowShield", true, "Award Shield proficiency from shield killing blows.");
            _allowMagic = Config.Bind("2. Weapon Skills", "AllowMagic", true, "Award Magic proficiency from spell, rod, or magic-item killing blows.");

            _minimumBonusXp = Config.Bind(
                "3. Advanced",
                "MinimumBonusXP",
                1.0f,
                "Minimum bonus paid when the computed bonus is greater than zero.");
            _roundBonusXpTo = Config.Bind(
                "3. Advanced",
                "RoundBonusXPTo",
                1.0f,
                "Round bonus proficiency XP to this increment. One rounds to whole XP; zero disables rounding.");
            _fallbackBonusXp = Config.Bind(
                "3. Advanced",
                "FallbackBonusXP",
                0.0f,
                "Bonus proficiency XP to use when enemy XP cannot be resolved. Zero skips unresolved enemies.");
            _requirePrimaryDamage = Config.Bind("3. Advanced", "RequirePrimaryDamage", true, "Only award bonuses for primary damage events, matching the game's normal weapon-proficiency rules.");
            _allowDamageOverTimeKills = Config.Bind("3. Advanced", "AllowDamageOverTimeKills", true, "Allow bleed, burn, poison, and other delayed damage to count when it can be traced to a recent supported hero damage source.");
            _damageOverTimeMemorySeconds = Config.Bind(
                "3. Advanced",
                "DamageOverTimeMemorySeconds",
                12.0f,
                "How long a recent supported hero damage source can credit a later damage-over-time death.");
            _ignoreThrowable = Config.Bind("3. Advanced", "IgnoreThrowable", true, "Do not award killing-blow proficiency from thrown items.");
            _requireXpRewardAllowedWhenPresent = Config.Bind("3. Advanced", "RequireXPRewardAllowedWhenPresent", true, "If the killed target exposes XpRewardAllowed, require it to be true.");
            _notificationsEnabled = Config.Bind("4. Notifications", "NotificationsEnabled", true, "Show an in-game HUD notification when killing-blow proficiency XP is awarded.");
            _notificationMinimumXp = Config.Bind("4. Notifications", "NotificationMinimumXP", 1.0f, "Minimum awarded bonus XP required before showing an in-game notification.");
            _notificationTextFormat = Config.Bind("4. Notifications", "NotificationTextFormat", DefaultNotificationTextFormat, "HUD notification text. Tokens: {xp}, {skill}, {enemy}, {weapon}, {enemyXP}.");
            _notificationMode = Config.Bind("4. Notifications", "NotificationMode", "GrailFloatingText", "Notification route: GrailFloatingText, GameHud, Both, or Off.");
            _rewardSoundVolume = Config.Bind(
                "5. Audio",
                "RewardSoundVolume",
                0.65f,
                new ConfigDescription(
                    "Volume multiplier for the killing-blow reward sound.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));
            _rewardSoundCooldownSeconds = Config.Bind("5. Audio", "RewardSoundCooldownSeconds", 0.35f, "Minimum real-time seconds between reward sounds.");
            _useKillingBlowFallbackForClassifiedKills = Config.Bind("5. Audio", "UseKillingBlowFallbackForClassifiedKills", false, "Allow classified weapon, shield, and magic kills to fall back to the killing_blow pool when their category pool is missing.");
            _useNonCorporealEnemySounds = Config.Bind("5. Audio", "UseNonCorporealEnemySounds", true, "Use the target-only non_corporeal sound pool for matched spirit/Wyrd enemies. This overrides weapon, magic, and _dry routing for those targets.");
            _nonCorporealSoundTerms = Config.Bind("5. Audio", "NonCorporealSoundTerms", DefaultNonCorporealSoundTerms, "Semicolon, comma, pipe, or newline separated target terms that force the non_corporeal sound pool.");
            _nonCorporealSoundExclusionTerms = Config.Bind("5. Audio", "NonCorporealSoundExclusionTerms", DefaultNonCorporealSoundExclusionTerms, "Optional target terms that prevent non_corporeal routing when both inclusion and exclusion terms match.");
            _useBloodlessSoundVariants = Config.Bind("5. Audio", "UseBloodlessSoundVariants", true, "Use *_dry.wav sound variants for targets whose names, templates, or type text match the bloodless sound terms.");
            _bloodlessSoundBlacklistTerms = Config.Bind("5. Audio", "BloodlessSoundBlacklistTerms", DefaultBloodlessSoundBlacklistTerms, "Semicolon, comma, pipe, or newline separated terms that make a killed target use bloodless sound variants when available.");
            _bloodlessSoundWhitelistTerms = Config.Bind("5. Audio", "BloodlessSoundWhitelistTerms", "", "Optional terms that force normal sounds even if a bloodless sound term also matches.");
            _avoidRecentSoundRepeats = Config.Bind("5. Audio", "AvoidRecentSoundRepeats", true, "Avoid replaying reward sounds that were recently used in the same sound pool.");
            _recentSoundMemory = Config.Bind(
                "5. Audio",
                "RecentSoundMemory",
                2,
                new ConfigDescription(
                    "How many recent sounds to avoid repeating per sound pool. Falls back gracefully when too few sounds are available.",
                    new AcceptableValueRange<int>(0, 4)));
            _randomPitchSemitones = Config.Bind(
                "5. Audio",
                "RandomPitchSemitones",
                0.35f,
                new ConfigDescription(
                    "Random reward-sound pitch variation in semitones. Zero disables pitch randomization.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));
            _trackStatistics = Config.Bind("6. Statistics", "TrackStatistics", true, "Track lightweight killing-blow statistics in the current game's save-backed gameplay memory.");
            _statisticsCharacterKeyOverride = Config.Bind("6. Statistics", "StatisticsCharacterKeyOverride", "", "Optional character key for separating stats. Leave blank for automatic hero-based separation.");
            _enemyStatsMode = Config.Bind("6. Statistics", "EnemyStatsMode", EnemyStatsModePromoted, "Enemy stat detail: Promoted, All, or Off. Promoted keeps repeated enemies as named rows and leaves one-offs as candidates.");
            _enemyPromoteKillCount = Config.Bind(
                "6. Statistics",
                "EnemyPromoteKillCount",
                2,
                new ConfigDescription(
                    "Enemy kills needed before that enemy is treated as a promoted named enemy in the stats file.",
                    new AcceptableValueRange<int>(1, 100)));
            _exportStatisticsReportOnSave = Config.Bind("6. Statistics", "ExportStatisticsReportOnSave", true, "Write the readable TSV statistics report when the game serializes save-backed gameplay memory.");
            _diagnostics = Config.Bind("7. Diagnostics", "Diagnostics", false, "Log kill source, resolved proficiency, enemy XP, awarded bonus, and statistics report export.");
            Config.Save();
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if ((string.Equals(currentSection, "1. Core", StringComparison.Ordinal)
                    || string.Equals(currentSection, "General", StringComparison.Ordinal))
                    && line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    int.TryParse(
                        line.Substring(schemaPrefix.Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out storedSchemaVersion);
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
                Log.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
            }
            catch (Exception ex)
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
                catch (Exception restoreEx)
                {
                    Log.LogError(
                        "Failed to restore Killing Blow Mastery config backup after schema reset failure: "
                        + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Killing Blow Mastery config schema. Original config was left in place when possible.",
                    ex);
            }
        }

        private void CacheGameAccessors()
        {
            _heroType = AccessTools.TypeByName(HeroTypeName);
            _itemType = AccessTools.TypeByName(ItemTypeName);
            _profStatType = AccessTools.TypeByName(ProfStatTypeName);
            _gameplayMemoryType = AccessTools.TypeByName(GameplayMemoryTypeName);
            _iModelType = AccessTools.TypeByName(IModelTypeName);

            if (_heroType != null)
            {
                _heroCurrentGetter = AccessTools.PropertyGetter(_heroType, "Current");
            }
            Type worldType = AccessTools.TypeByName(WorldTypeName);
            if (worldType != null)
            {
                _worldServicesGetter = AccessTools.PropertyGetter(worldType, "Services");
            }
            if (_gameplayMemoryType != null)
            {
                _gameplayMemoryContextMethod = AccessTools.Method(_gameplayMemoryType, "Context", new[] { typeof(string) });
            }
            if (_iModelType != null)
            {
                _emptyModelOwners = Array.CreateInstance(_iModelType, 0);
            }

            Type profUtilsType = AccessTools.TypeByName(ProfUtilsTypeName);
            if (profUtilsType != null && _itemType != null)
            {
                _profFromAbstractsMethod = AccessTools.Method(profUtilsType, "ProfFromAbstracts", new[] { _itemType, typeof(bool) });
            }

            if (_profStatType != null)
            {
                _oneHandedProf = GetStaticFieldValue(_profStatType, "OneHanded");
                _twoHandedProf = GetStaticFieldValue(_profStatType, "TwoHanded");
                _unarmedProf = GetStaticFieldValue(_profStatType, "Unarmed");
                _archeryProf = GetStaticFieldValue(_profStatType, "Archery");
                _shieldProf = GetStaticFieldValue(_profStatType, "Shield");
                _magicProf = GetStaticFieldValue(_profStatType, "Magic");
            }

            CacheNotificationAccessors();
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type npcElementType = AccessTools.TypeByName(NpcElementTypeName);
            if (npcElementType == null)
            {
                Log.LogError("Could not find " + NpcElementTypeName + ". " + PluginName + " is inactive.");
                return;
            }

            MethodInfo original = AccessTools.Method(npcElementType, "DeathNonCriticalFunctions");
            MethodInfo postfix = AccessTools.Method(typeof(NpcDeathPatch), "Postfix");
            if (original == null || postfix == null)
            {
                Log.LogError("Could not patch NPC death handling. " + PluginName + " is inactive.");
                return;
            }

            _harmony.Patch(original, null, new HarmonyMethod(postfix));
            if (_diagnostics.Value)
            {
                Log.LogInfo("Patched " + npcElementType.FullName + ".DeathNonCriticalFunctions.");
            }

            PatchGameplayMemorySerialization();

            Type healthElementType = AccessTools.TypeByName(HealthElementTypeName);
            MethodInfo damageOriginal = healthElementType == null ? null : AccessTools.Method(healthElementType, "BeforeHealthDecreaseEvents");
            MethodInfo damagePostfix = AccessTools.Method(typeof(HealthElementBeforeHealthDecreasePatch), "Postfix");
            if (damageOriginal == null || damagePostfix == null)
            {
                LogDiagnostic("Could not patch HealthElement.BeforeHealthDecreaseEvents; damage-over-time kill source memory is unavailable.");
                return;
            }

            try
            {
                _harmony.Patch(damageOriginal, null, new HarmonyMethod(damagePostfix));
                LogDiagnostic("Patched " + healthElementType.FullName + ".BeforeHealthDecreaseEvents.");
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to patch HealthElement.BeforeHealthDecreaseEvents for damage-over-time kill source memory: " + ex.GetBaseException().Message);
            }
        }

        private void PatchGameplayMemorySerialization()
        {
            if (_gameplayMemoryType == null)
            {
                LogDiagnostic("Could not find " + GameplayMemoryTypeName + "; save-time statistics report export is unavailable.");
                return;
            }

            MethodInfo original = AccessTools.Method(_gameplayMemoryType, "OnBeforeSerialize");
            MethodInfo postfix = AccessTools.Method(typeof(GameplayMemoryBeforeSerializePatch), "Postfix");
            if (original == null || postfix == null)
            {
                LogDiagnostic("Could not patch GameplayMemory.OnBeforeSerialize; save-time statistics report export is unavailable.");
                return;
            }

            try
            {
                _harmony.Patch(original, null, new HarmonyMethod(postfix));
                LogDiagnostic("Patched " + _gameplayMemoryType.FullName + ".OnBeforeSerialize for save-time statistics report export.");
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to patch GameplayMemory.OnBeforeSerialize for statistics export: " + ex.GetBaseException().Message);
            }
        }

        internal void OnNpcDeath(object npc, object damageOutcome)
        {
            if (!_enabled.Value || npc == null || damageOutcome == null)
            {
                return;
            }

            bool foundXpRewardAllowed;
            bool xpRewardAllowed = GetBoolProperty(npc, "XpRewardAllowed", true, out foundXpRewardAllowed);
            if (_requireXpRewardAllowedWhenPresent.Value && foundXpRewardAllowed && !xpRewardAllowed)
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": XpRewardAllowed is false.");
                return;
            }

            object damage = GetOptionalPropertyValue(damageOutcome, "Damage");
            bool damageIsPrimary = GetBoolProperty(damage, "IsPrimary", true);
            bool damageIsOverTime = GetBoolProperty(damage, "IsDamageOverTime", false);
            bool directHeroKill = IsHeroKillingBlow(damageOutcome, damage);
            bool usedSourceMemory = false;
            KillSourceMemory sourceMemory;
            object item = null;
            object proficiency = null;
            object sourceDamage = damage;
            string sourceName = "";

            if ((!directHeroKill || damageIsOverTime || (_requirePrimaryDamage.Value && !damageIsPrimary))
                && _allowDamageOverTimeKills.Value
                && TryGetRecentKillSourceMemory(npc, damageOutcome, damage, out sourceMemory))
            {
                usedSourceMemory = true;
                item = sourceMemory.Item;
                proficiency = sourceMemory.Proficiency;
                sourceDamage = sourceMemory.Damage;
                sourceName = sourceMemory.SourceName;
                LogDiagnostic("Using remembered kill source " + sourceName + " for delayed or non-primary death of " + DescribeObject(npc) + ".");
            }

            if (!usedSourceMemory && !directHeroKill)
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": attacker was not the current hero.");
                return;
            }

            if (!usedSourceMemory && _requirePrimaryDamage.Value && !damageIsPrimary && !(damageIsOverTime && _allowDamageOverTimeKills.Value))
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": damage was not primary.");
                return;
            }

            if (!usedSourceMemory && damageIsOverTime && !_allowDamageOverTimeKills.Value)
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": damage was damage-over-time.");
                return;
            }

            if (!usedSourceMemory)
            {
                item = ResolveKillingItem(damage);
                proficiency = ResolveKillingProficiency(item, damage);
                sourceName = DescribeKillSource(item, damage);
            }

            if (proficiency == null)
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": no supported killing weapon, shield, or magic source was available.");
                return;
            }

            if (item != null && _ignoreThrowable.Value && GetBoolProperty(item, "IsThrowable", false))
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": killing item was throwable.");
                return;
            }

            if (!IsEligibleProficiency(proficiency))
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": resolved proficiency " + DescribeProficiency(proficiency) + " is not enabled.");
                return;
            }

            float enemyXp = TryReadExpReward(npc);
            if (enemyXp <= 0.0f)
            {
                enemyXp = TryReadExpReward(GetOptionalPropertyValue(npc, "Template"));
            }

            float bonus = CalculateBonus(enemyXp);
            if (bonus <= 0.0f)
            {
                LogDiagnostic("Skipped " + DescribeObject(npc) + ": enemy XP was unresolved and FallbackBonusXP is zero.");
                return;
            }

            if (TryAwardProficiencyXp(proficiency, bonus))
            {
                string proficiencyName = DescribeProficiency(proficiency);
                string enemyName = DescribeObject(npc);

                RecordStatistics(npc, proficiency, proficiencyName, item, sourceDamage, sourceName, bonus, damageIsOverTime || usedSourceMemory);
                ShowAwardNotification(bonus, DescribeNotificationProficiency(proficiency, proficiencyName), enemyName, sourceName, enemyXp, proficiency);
                PlayAwardSound(bonus, proficiency, item, sourceDamage, npc);

                LogDiagnostic("Awarded " + bonus.ToString("0.###", CultureInfo.InvariantCulture) + " " +
                    proficiencyName + " XP for killing " + enemyName +
                    " with " + sourceName + " from enemy XP " +
                    enemyXp.ToString("0.###", CultureInfo.InvariantCulture) + ".");
            }
        }

        internal void OnHealthDamageApplied(object healthElement, object damage)
        {
            if (!_enabled.Value ||
                !_allowDamageOverTimeKills.Value ||
                healthElement == null ||
                damage == null)
            {
                return;
            }

            float amount = GetDamageAmount(damage);
            if (amount <= 0.001f)
            {
                return;
            }

            object hero = GetCurrentHero();
            if (hero == null || !IsHeroDamageSource(damage, hero))
            {
                return;
            }

            object heroHealthElement = GetOptionalPropertyValue(hero, "HealthElement");
            if (ReferenceEquals(healthElement, heroHealthElement))
            {
                return;
            }

            object item = ResolveKillingItem(damage);
            object proficiency = ResolveKillingProficiency(item, damage);
            if (proficiency == null ||
                !IsEligibleProficiency(proficiency) ||
                (item != null && _ignoreThrowable.Value && GetBoolProperty(item, "IsThrowable", false)))
            {
                return;
            }

            object target = ResolveDamageTargetOwner(healthElement, damage);
            KillSourceMemory memory = new KillSourceMemory
            {
                HealthElement = healthElement,
                Target = target,
                Damage = damage,
                Item = item,
                Proficiency = proficiency,
                SourceName = DescribeKillSource(item, damage),
                LastSeenTime = Time.unscaledTime
            };

            RememberKillSourceMemory(memory, healthElement, target, damage);
        }

        private void RememberKillSourceMemory(KillSourceMemory memory, params object[] roots)
        {
            if (memory == null)
            {
                return;
            }

            PruneExpiredKillSourceMemory();
            List<object> keys = BuildRelatedKeys(roots);
            for (int i = 0; i < keys.Count; i++)
            {
                _recentKillSourcesByKey[keys[i]] = memory;
            }

            LogDiagnostic("Remembered " + DescribeProficiency(memory.Proficiency) + " source " + memory.SourceName + " for " + DescribeObject(memory.Target) + ".");
        }

        private bool TryGetRecentKillSourceMemory(object npc, object outcome, object damage, out KillSourceMemory memory)
        {
            memory = null;
            PruneExpiredKillSourceMemory();

            List<object> keys = BuildRelatedKeys(npc, outcome, damage);
            for (int i = 0; i < keys.Count; i++)
            {
                KillSourceMemory candidate;
                if (!_recentKillSourcesByKey.TryGetValue(keys[i], out candidate) || !IsKillSourceMemoryFresh(candidate))
                {
                    continue;
                }

                if (memory == null || candidate.LastSeenTime > memory.LastSeenTime)
                {
                    memory = candidate;
                }
            }

            return memory != null;
        }

        private void PruneExpiredKillSourceMemory()
        {
            if (_recentKillSourcesByKey.Count == 0)
            {
                return;
            }

            List<object> staleKeys = null;
            foreach (KeyValuePair<object, KillSourceMemory> pair in _recentKillSourcesByKey)
            {
                if (IsDestroyedUnityObject(pair.Key) || !IsKillSourceMemoryFresh(pair.Value))
                {
                    if (staleKeys == null)
                    {
                        staleKeys = new List<object>();
                    }

                    staleKeys.Add(pair.Key);
                }
            }

            if (staleKeys == null)
            {
                return;
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                _recentKillSourcesByKey.Remove(staleKeys[i]);
            }
        }

        private bool IsKillSourceMemoryFresh(KillSourceMemory memory)
        {
            if (memory == null)
            {
                return false;
            }

            float seconds = Math.Max(0.1f, _damageOverTimeMemorySeconds.Value);
            return Time.unscaledTime - memory.LastSeenTime <= seconds;
        }

        private List<object> BuildRelatedKeys(params object[] roots)
        {
            List<object> keys = new List<object>();
            if (roots == null)
            {
                return keys;
            }

            for (int i = 0; i < roots.Length; i++)
            {
                AddRelatedKeys(keys, roots[i]);
            }

            return keys;
        }

        private void AddRelatedKeys(List<object> keys, object root)
        {
            AddUniqueKey(keys, root);
            if (root == null)
            {
                return;
            }

            string[] properties =
            {
                "HealthElement",
                "NpcElement",
                "Character",
                "CharacterView",
                "Target",
                "TargetPure",
                "Model",
                "ParentModel",
                "GenericParentModel",
                "Element",
                "Owner",
                "Parent"
            };

            for (int i = 0; i < properties.Length; i++)
            {
                AddUniqueKey(keys, GetOptionalPropertyValue(root, properties[i]));
            }
        }

        private void AddUniqueKey(List<object> keys, object key)
        {
            if (key == null || IsDestroyedUnityObject(key))
            {
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                if (ReferenceEquals(keys[i], key))
                {
                    return;
                }
            }

            keys.Add(key);
        }

        private void CacheNotificationAccessors()
        {
            Type notificationUtilsType = AccessTools.TypeByName(NotificationUtilsTypeName);
            if (notificationUtilsType != null)
            {
                _notificationPushMethod = FindNotificationPushMethod(notificationUtilsType);
            }

            Type lowerInfoNotificationType = AccessTools.TypeByName(LowerInfoNotificationTypeName);
            _lowerInfoViewType = AccessTools.TypeByName(LowerInfoViewTypeName);
            _lowerInfoNotificationConstructor = GetConstructorSilent(lowerInfoNotificationType, new[] { typeof(string), typeof(Type) });

            Type wyrdInfoNotificationType = AccessTools.TypeByName(WyrdInfoNotificationTypeName);
            _wyrdInfoNotificationConstructor = GetConstructorSilent(wyrdInfoNotificationType, new[] { typeof(string) });

            LogDiagnostic("Notification accessors: push=" + (_notificationPushMethod != null) +
                ", wyrdInfo=" + (_wyrdInfoNotificationConstructor != null) +
                ", lowerInfo=" + (_lowerInfoNotificationConstructor != null && _lowerInfoViewType != null) + ".");
        }

        private MethodInfo FindNotificationPushMethod(Type notificationUtilsType)
        {
            MethodInfo[] methods = notificationUtilsType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name == "Push" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
                {
                    return method;
                }
            }

            return null;
        }

        private void ShowAwardNotification(float bonus, string proficiencyName, string enemyName, string weaponName, float enemyXp, object proficiency)
        {
            if (!_notificationsEnabled.Value || bonus < Math.Max(0.0f, _notificationMinimumXp.Value))
            {
                return;
            }

            string text = BuildNotificationText(bonus, proficiencyName, enemyName, weaponName, enemyXp);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string mode = NormalizeNotificationMode(_notificationMode.Value);
            if (mode == "Off")
            {
                return;
            }

            if (mode == "GrailFloatingText" || mode == "Both")
            {
                if (TryShowGrailFloatingText(text, ResolveProficiencyIconId(proficiency)))
                {
                    LogDiagnostic("Queued killing-blow notification via Grail Floating Text.");
                }
                else
                {
                    LogDiagnostic("Could not show killing-blow notification via Grail Floating Text.");
                }
            }

            if (mode == "GrailFloatingText")
            {
                return;
            }

            string route = "";
            if (TryPushWyrdInfoNotification(text))
            {
                route = "wyrd-info";
            }
            else if (TryPushLowerInfoNotification(text))
            {
                route = "lower-info";
            }

            if (route == "")
            {
                LogDiagnostic("Could not show killing-blow proficiency HUD notification.");
                return;
            }

            LogDiagnostic("Queued killing-blow HUD notification via " + route + ".");
        }

        private string NormalizeNotificationMode(string rawMode)
        {
            if (string.IsNullOrWhiteSpace(rawMode))
            {
                return "GrailFloatingText";
            }

            if (rawMode.Equals("GrailFloatingText", StringComparison.OrdinalIgnoreCase) ||
                rawMode.Equals("FloatingText", StringComparison.OrdinalIgnoreCase) ||
                rawMode.Equals("KS", StringComparison.OrdinalIgnoreCase) ||
                rawMode.Equals("Shared", StringComparison.OrdinalIgnoreCase))
            {
                return "GrailFloatingText";
            }
            if (rawMode.Equals("GameHud", StringComparison.OrdinalIgnoreCase) ||
                rawMode.Equals("GameHUD", StringComparison.OrdinalIgnoreCase) ||
                rawMode.Equals("Hud", StringComparison.OrdinalIgnoreCase))
            {
                return "GameHud";
            }
            if (rawMode.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                return "Both";
            }
            if (rawMode.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                rawMode.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return "Off";
            }

            return "GrailFloatingText";
        }

        private bool TryShowGrailFloatingText(string text, string iconId)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!TryResolveGrailFloatingTextBridge())
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text is not loaded; Killing Blow Mastery reward notifications are unavailable.");
                return false;
            }

            try
            {
                object result;
                if (_grailFloatingTextTryShowEventWithIconMethod != null)
                {
                    result = _grailFloatingTextTryShowEventWithIconMethod.Invoke(
                        null,
                        new object[] { PluginGuid, GrailFloatingTextKillingBlowEventId, text, "Reward", "Reward", "Normal", string.Empty, iconId, GrailFloatingTextMediumDurationBucket, 0.25f, 0.9f });
                }
                else if (_grailFloatingTextTryShowWithIconMethod != null)
                {
                    result = _grailFloatingTextTryShowWithIconMethod.Invoke(
                        null,
                        new object[] { PluginGuid, text, "Critical", "Reward", "Normal", string.Empty, iconId, 0.0f, 0.25f, 0.9f });
                }
                else
                {
                    result = _grailFloatingTextTryShowMethod.Invoke(
                        null,
                        new object[] { PluginGuid, text, "Critical", "Reward", "Normal", string.Empty, 0.0f, 0.25f, 0.9f });
                }

                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text failed to show a Killing Blow Mastery reward notification: " + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool TryResolveGrailFloatingTextBridge()
        {
            if (_grailFloatingTextBridgeResolved)
            {
                return _grailFloatingTextTryShowEventWithIconMethod != null ||
                    _grailFloatingTextTryShowWithIconMethod != null ||
                    _grailFloatingTextTryShowMethod != null;
            }

            _grailFloatingTextBridgeResolved = true;

            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(GrailFloatingTextPluginGuid, out pluginInfo) ||
                pluginInfo == null ||
                pluginInfo.Instance == null)
            {
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(GrailFloatingTextApiTypeName, false);
            if (apiType == null)
            {
                return false;
            }

            _grailFloatingTextTryShowEventWithIconMethod = AccessTools.Method(
                apiType,
                "TryShowEvent",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float)
                });

            _grailFloatingTextTryShowWithIconMethod = AccessTools.Method(
                apiType,
                "TryShow",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });

            _grailFloatingTextTryShowMethod = AccessTools.Method(
                apiType,
                "TryShow",
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });
            return _grailFloatingTextTryShowEventWithIconMethod != null ||
                _grailFloatingTextTryShowWithIconMethod != null ||
                _grailFloatingTextTryShowMethod != null;
        }

        private string ResolveProficiencyIconId(object proficiency)
        {
            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                return "one_handed";
            }
            if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                return "two_handed";
            }
            if (ReferenceEquals(proficiency, _unarmedProf))
            {
                return "unarmed";
            }
            if (ReferenceEquals(proficiency, _archeryProf))
            {
                return "archery";
            }
            if (ReferenceEquals(proficiency, _shieldProf))
            {
                return "shield";
            }
            if (ReferenceEquals(proficiency, _magicProf))
            {
                return "magic";
            }

            return "reward";
        }

        private void LogGrailFloatingTextUnavailableOnce(string message)
        {
            if (_grailFloatingTextUnavailableLogged)
            {
                return;
            }

            _grailFloatingTextUnavailableLogged = true;
            Log.LogInfo(message);
        }

        private string BuildNotificationText(float bonus, string proficiencyName, string enemyName, string weaponName, float enemyXp)
        {
            string format = _notificationTextFormat.Value;
            if (string.IsNullOrWhiteSpace(format))
            {
                format = DefaultNotificationTextFormat;
            }

            return format
                .Replace("{xp}", FormatFloat(bonus))
                .Replace("{skill}", proficiencyName ?? "")
                .Replace("{enemy}", enemyName ?? "")
                .Replace("{weapon}", weaponName ?? "")
                .Replace("{enemyXP}", FormatFloat(enemyXp));
        }

        private bool TryPushLowerInfoNotification(string text)
        {
            if (_lowerInfoNotificationConstructor == null || _lowerInfoViewType == null)
            {
                return false;
            }

            try
            {
                object notification = _lowerInfoNotificationConstructor.Invoke(new object[] { text, _lowerInfoViewType });
                return TryPushNotification(notification);
            }
            catch (Exception ex)
            {
                LogDiagnostic("Lower-info announcement failed: " + ex.GetType().Name + ".");
                return false;
            }
        }

        private bool TryPushWyrdInfoNotification(string text)
        {
            if (_wyrdInfoNotificationConstructor == null)
            {
                return false;
            }

            try
            {
                object notification = _wyrdInfoNotificationConstructor.Invoke(new object[] { text });
                return TryPushNotification(notification);
            }
            catch (Exception ex)
            {
                LogDiagnostic("Wyrd-info announcement fallback failed: " + ex.GetType().Name + ".");
                return false;
            }
        }

        private bool TryPushNotification(object notification)
        {
            if (_notificationPushMethod == null || notification == null)
            {
                return false;
            }

            try
            {
                MethodInfo method = _notificationPushMethod.MakeGenericMethod(notification.GetType());
                method.Invoke(null, new[] { notification });
                return true;
            }
            catch (Exception ex)
            {
                LogDiagnostic("Notification push failed: " + ex.GetType().Name + ".");
                return false;
            }
        }

        private sealed class RewardSoundFile
        {
            public string PoolName;
            public string Path;
        }

        private sealed class RewardSoundClip
        {
            public string Path;
            public AudioClip Clip;
        }

        private sealed class RewardSoundSelection
        {
            public string PoolName;
            public string Path;
            public AudioClip Clip;
        }

        private sealed class KillSourceMemory
        {
            public object HealthElement;
            public object Target;
            public object Damage;
            public object Item;
            public object Proficiency;
            public string SourceName;
            public float LastSeenTime;
        }

        private void PlayAwardSound(float bonus, object proficiency, object item, object damage, object target)
        {
            string soundMode = GetFinisherSoundMode();
            if (string.Equals(soundMode, FinisherSoundModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            float cooldown = Math.Max(0.0f, _rewardSoundCooldownSeconds.Value);
            if (cooldown > 0.0f && Time.unscaledTime - _lastRewardSoundTime < cooldown)
            {
                return;
            }

            EnsureRewardSoundLoadStarted();
            RewardSoundSelection selection = PickRewardSound(GetRewardSoundPools(soundMode, proficiency, item, damage, target));

            if (selection == null || selection.Clip == null)
            {
                LogDiagnostic("No reward sound was loaded for FinisherSoundMode=" + soundMode + ".");
                return;
            }

            _lastRewardSoundTime = Time.unscaledTime;
            float volume = Math.Max(0.0f, _rewardSoundVolume.Value);
            volume *= GetRangeVolumeMultiplier(target);
            if (TryPlayFmodRewardSound(selection, volume))
            {
                RememberRewardSoundSelection(selection);
                return;
            }

            PlayUnityRewardSound(selection, volume);
            RememberRewardSoundSelection(selection);
        }

        private bool TryPlayFmodRewardSound(RewardSoundSelection selection, float volume)
        {
            if (selection == null || string.IsNullOrWhiteSpace(selection.Path))
            {
                return false;
            }

            try
            {
                FMOD.Sound sound;
                if (!_fmodSoundsByPath.TryGetValue(selection.Path, out sound))
                {
                    FMOD.RESULT createResult = RuntimeManager.CoreSystem.createSound(
                        selection.Path,
                        FMOD.MODE.DEFAULT | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE,
                        out sound);
                    if (createResult != FMOD.RESULT.OK)
                    {
                        Log.LogWarning("FMOD createSound failed for " + selection.Path + ": " + createResult + ".");
                        return false;
                    }

                    _fmodSoundsByPath[selection.Path] = sound;
                }

                FMOD.ChannelGroup channelGroup;
                FMOD.RESULT groupResult = RuntimeManager.CoreSystem.getMasterChannelGroup(out channelGroup);
                if (groupResult != FMOD.RESULT.OK)
                {
                    channelGroup = default(FMOD.ChannelGroup);
                }

                FMOD.Channel channel;
                FMOD.RESULT playResult = RuntimeManager.CoreSystem.playSound(sound, channelGroup, true, out channel);
                if (playResult != FMOD.RESULT.OK)
                {
                    Log.LogWarning("FMOD playSound failed for " + selection.Path + ": " + playResult + ".");
                    return false;
                }

                FMOD.RESULT volumeResult = channel.setVolume(volume);
                if (volumeResult != FMOD.RESULT.OK)
                {
                    LogDiagnostic("FMOD channel volume set failed for " + selection.Path + ": " + volumeResult + ".");
                }

                float pitch = GetRandomPitchMultiplier();
                FMOD.RESULT pitchResult = channel.setPitch(pitch);
                if (pitchResult != FMOD.RESULT.OK)
                {
                    LogDiagnostic("FMOD channel pitch set failed for " + selection.Path + ": " + pitchResult + ".");
                }

                FMOD.RESULT pauseResult = channel.setPaused(false);
                if (pauseResult != FMOD.RESULT.OK)
                {
                    LogDiagnostic("FMOD channel unpause failed for " + selection.Path + ": " + pauseResult + ".");
                }

                LogDiagnostic("Played FMOD reward sound " + Path.GetFileName(selection.Path) + " from pool " + selection.PoolName + " at pitch " + pitch.ToString("0.###", CultureInfo.InvariantCulture) + ".");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning("FMOD reward sound playback failed for " + selection.Path + ": " + ex.GetBaseException().Message);
                return false;
            }
        }

        private void PlayUnityRewardSound(RewardSoundSelection selection, float volume)
        {
            EnsureRewardAudioSource();
            if (_rewardAudioSource == null || selection == null || selection.Clip == null)
            {
                return;
            }

            float pitch = GetRandomPitchMultiplier();
            _rewardAudioSource.pitch = pitch;
            _rewardAudioSource.PlayOneShot(selection.Clip, volume);
            LogDiagnostic("Played Unity reward sound " + selection.Clip.name + " from pool " + selection.PoolName + " at pitch " + pitch.ToString("0.###", CultureInfo.InvariantCulture) + ".");
        }

        private void ReleaseFmodRewardSounds()
        {
            foreach (KeyValuePair<string, FMOD.Sound> pair in _fmodSoundsByPath)
            {
                try
                {
                    pair.Value.release();
                }
                catch
                {
                }
            }

            _fmodSoundsByPath.Clear();
        }

        private void EnsureRewardAudioSource()
        {
            if (_rewardAudioSource != null)
            {
                return;
            }

            GameObject audioObject = new GameObject(AudioSourceObjectName);
            DontDestroyOnLoad(audioObject);
            _rewardAudioSource = audioObject.AddComponent<AudioSource>();
            _rewardAudioSource.playOnAwake = false;
            _rewardAudioSource.loop = false;
            _rewardAudioSource.spatialBlend = 0.0f;
        }

        private void EnsureRewardSoundLoadStarted()
        {
            if (string.Equals(GetFinisherSoundMode(), FinisherSoundModeOff, StringComparison.OrdinalIgnoreCase) || _rewardSoundLoadStarted)
            {
                return;
            }

            _rewardSoundLoadStarted = true;
            RewardSoundFile[] files = FindRewardSoundFiles();
            if (files.Length == 0)
            {
                Log.LogWarning("Finisher sounds are enabled, but no reward sound WAV files were found.");
                return;
            }

            StartCoroutine(LoadRewardSounds(files));
        }

        private RewardSoundFile[] FindRewardSoundFiles()
        {
            List<RewardSoundFile> files = new List<RewardSoundFile>();

            try
            {
                string[] poolNames = GetKnownSoundPoolNames();
                for (int i = 0; i < poolNames.Length; i++)
                {
                    AddRewardSoundPoolFiles(files, poolNames[i]);
                    AddBloodlessRewardSoundPoolFiles(files, poolNames[i]);
                }

                AddRewardSoundPoolFiles(files, NonCorporealSoundPool);
                AddRewardSoundFile(files, DiagnosticGoatSoundPool, DiagnosticGoatSoundFileName);
                AddRewardSoundFile(files, SoulslikeKillingBlowSoundPool, SoulslikeKillingBlowSoundFileName);
                AddRewardSoundFile(files, GetBloodlessSoundPoolName(SoulslikeKillingBlowSoundPool), "killing_blow1" + BloodlessSoundFileSuffix + ".wav");
                AddRewardSoundPoolFiles(files, GlobalSoundPool);
                AddBloodlessRewardSoundPoolFiles(files, GlobalSoundPool);
            }
            catch (Exception ex)
            {
                Log.LogWarning("Could not resolve reward sound path: " + ex.GetType().Name + ".");
            }

            return files.ToArray();
        }

        private void AddRewardSoundPoolFiles(List<RewardSoundFile> files, string poolName)
        {
            if (string.IsNullOrWhiteSpace(poolName))
            {
                return;
            }

            for (int i = 1; i <= DefaultRewardSoundSlots; i++)
            {
                AddRewardSoundFile(files, poolName, poolName + i.ToString(CultureInfo.InvariantCulture) + ".wav");
            }
        }

        private void AddBloodlessRewardSoundPoolFiles(List<RewardSoundFile> files, string poolName)
        {
            if (string.IsNullOrWhiteSpace(poolName))
            {
                return;
            }

            string bloodlessPoolName = GetBloodlessSoundPoolName(poolName);
            for (int i = 1; i <= DefaultRewardSoundSlots; i++)
            {
                AddRewardSoundFile(
                    files,
                    bloodlessPoolName,
                    poolName + i.ToString(CultureInfo.InvariantCulture) + BloodlessSoundFileSuffix + ".wav");
            }
        }

        private void AddRewardSoundFile(List<RewardSoundFile> files, string poolName, string configured)
        {
            string resolved = ResolveRewardSoundPath(configured);
            if (resolved == "")
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                if (string.Equals(files[i].PoolName, poolName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(files[i].Path, resolved, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            files.Add(new RewardSoundFile { PoolName = poolName, Path = resolved });
        }

        private string ResolveRewardSoundPath(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                return "";
            }

            if (Path.IsPathRooted(configured) && File.Exists(configured))
            {
                return configured;
            }

            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(pluginDirectory))
            {
                return "";
            }

            string primary = Path.Combine(pluginDirectory, configured);
            if (File.Exists(primary))
            {
                return primary;
            }

            if (string.IsNullOrEmpty(Path.GetDirectoryName(configured)))
            {
                string audioFolderCandidate = Path.Combine(Path.Combine(pluginDirectory, "audio"), configured);
                if (File.Exists(audioFolderCandidate))
                {
                    return audioFolderCandidate;
                }
            }

            return "";
        }

        private IEnumerator LoadRewardSounds(RewardSoundFile[] files)
        {
            LogDiagnostic("Loading " + files.Length.ToString(CultureInfo.InvariantCulture) + " reward sound file(s).");

            for (int i = 0; i < files.Length; i++)
            {
                yield return StartCoroutine(LoadRewardSound(files[i]));
            }

            LogDiagnostic("Loaded " + CountRewardSoundClips().ToString(CultureInfo.InvariantCulture) +
                " reward sound clip(s) across " + _rewardSoundClipsByPool.Count.ToString(CultureInfo.InvariantCulture) + " pool(s).");
        }

        private IEnumerator LoadRewardSound(RewardSoundFile file)
        {
            string uri = new Uri(file.Path).AbsoluteUri;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Log.LogWarning("Could not load reward sound from " + file.Path + ": " + request.error);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    Log.LogWarning("Could not load reward sound from " + file.Path + ": no audio clip was returned.");
                    yield break;
                }

                clip.name = Path.GetFileName(file.Path);
                List<RewardSoundClip> clips;
                if (!_rewardSoundClipsByPool.TryGetValue(file.PoolName, out clips))
                {
                    clips = new List<RewardSoundClip>();
                    _rewardSoundClipsByPool[file.PoolName] = clips;
                }

                clips.Add(new RewardSoundClip { Path = file.Path, Clip = clip });
                LogDiagnostic("Loaded reward sound " + clip.name + " into pool " + file.PoolName + ".");
            }
        }

        private int CountRewardSoundClips()
        {
            int count = 0;
            foreach (KeyValuePair<string, List<RewardSoundClip>> pair in _rewardSoundClipsByPool)
            {
                count += pair.Value.Count;
            }

            return count;
        }

        private RewardSoundSelection PickRewardSound(string[] poolNames)
        {
            for (int i = 0; i < poolNames.Length; i++)
            {
                List<RewardSoundClip> clips;
                if (_rewardSoundClipsByPool.TryGetValue(poolNames[i], out clips) && clips.Count > 0)
                {
                    RewardSoundClip selected = PickRewardSoundClip(poolNames[i], clips);
                    return new RewardSoundSelection
                    {
                        PoolName = poolNames[i],
                        Path = selected.Path,
                        Clip = selected.Clip
                    };
                }
            }

            return null;
        }

        private RewardSoundClip PickRewardSoundClip(string poolName, List<RewardSoundClip> clips)
        {
            if (clips == null || clips.Count == 0)
            {
                return null;
            }

            if (!_avoidRecentSoundRepeats.Value || GetRecentSoundMemory() <= 0 || clips.Count == 1)
            {
                return clips[_random.Next(clips.Count)];
            }

            List<int> eligible = null;
            for (int i = 0; i < clips.Count; i++)
            {
                if (!WasRewardSoundRecentlyPlayed(poolName, clips[i].Path))
                {
                    if (eligible == null)
                    {
                        eligible = new List<int>();
                    }

                    eligible.Add(i);
                }
            }

            if (eligible != null && eligible.Count > 0)
            {
                return clips[eligible[_random.Next(eligible.Count)]];
            }

            return clips[_random.Next(clips.Count)];
        }

        private void RememberRewardSoundSelection(RewardSoundSelection selection)
        {
            if (selection == null || string.IsNullOrWhiteSpace(selection.PoolName) || string.IsNullOrWhiteSpace(selection.Path))
            {
                return;
            }

            int memory = GetRecentSoundMemory();
            if (!_avoidRecentSoundRepeats.Value || memory <= 0)
            {
                return;
            }

            Queue<string> recent;
            if (!_recentRewardSoundPathsByPool.TryGetValue(selection.PoolName, out recent))
            {
                recent = new Queue<string>();
                _recentRewardSoundPathsByPool[selection.PoolName] = recent;
            }

            recent.Enqueue(selection.Path);
            while (recent.Count > memory)
            {
                recent.Dequeue();
            }
        }

        private bool WasRewardSoundRecentlyPlayed(string poolName, string path)
        {
            if (string.IsNullOrWhiteSpace(poolName) || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            Queue<string> recent;
            if (!_recentRewardSoundPathsByPool.TryGetValue(poolName, out recent))
            {
                return false;
            }

            foreach (string recentPath in recent)
            {
                if (string.Equals(recentPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetRecentSoundMemory()
        {
            return Math.Max(0, Math.Min(4, _recentSoundMemory.Value));
        }

        private float GetRandomPitchMultiplier()
        {
            float semitones = Math.Max(0.0f, _randomPitchSemitones.Value);
            if (semitones <= 0.001f)
            {
                return 1.0f;
            }

            float offset = (float)((_random.NextDouble() * 2.0 - 1.0) * semitones);
            return (float)Math.Pow(2.0, offset / 12.0);
        }

        private string[] GetKnownSoundPoolNames()
        {
            List<string> poolNames = new List<string>();
            AddPoolName(poolNames, OneHandedBladeSoundPool);
            AddPoolName(poolNames, OneHandedAxeSoundPool);
            AddPoolName(poolNames, OneHandedBluntSoundPool);
            AddPoolName(poolNames, TwoHandedBladeSoundPool);
            AddPoolName(poolNames, TwoHandedAxeSoundPool);
            AddPoolName(poolNames, TwoHandedBluntSoundPool);
            AddPoolName(poolNames, UnarmedSoundPool);
            AddPoolName(poolNames, ArcheryShortSoundPool);
            AddPoolName(poolNames, ArcheryMediumSoundPool);
            AddPoolName(poolNames, ArcheryHeavySoundPool);
            AddPoolName(poolNames, ShieldBashSoundPool);
            AddPoolName(poolNames, MagicBloodSoundPool);
            AddPoolName(poolNames, MagicFireSoundPool);
            AddPoolName(poolNames, MagicFrostSoundPool);
            AddPoolName(poolNames, MagicPoisonSoundPool);
            AddPoolName(poolNames, MagicElectricSoundPool);
            AddPoolName(poolNames, MagicWyrdnessSoundPool);
            AddPoolName(poolNames, MagicWaterSoundPool);
            AddPoolName(poolNames, MagicArcaneSoundPool);
            return poolNames.ToArray();
        }

        private string[] GetRewardSoundPools(string soundMode, object proficiency, object item, object damage, object target)
        {
            if (string.Equals(soundMode, FinisherSoundModeGoatTest, StringComparison.OrdinalIgnoreCase))
            {
                return BuildSoundFallbackPools(DiagnosticGoatSoundPool);
            }

            if (IsNonCorporealSoundTarget(target, damage))
            {
                return BuildSoundFallbackPools(NonCorporealSoundPool);
            }

            bool useBloodless = IsBloodlessSoundTarget(target, damage);

            if (string.Equals(soundMode, FinisherSoundModeSoulslike, StringComparison.OrdinalIgnoreCase))
            {
                return BuildBloodlessSoundFallbackPools(useBloodless, BuildSoundFallbackPools(SoulslikeKillingBlowSoundPool));
            }

            string globalPool = GlobalSoundPool;
            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                return BuildBloodlessSoundFallbackPools(useBloodless, BuildClassifiedSoundPools(ResolveOneHandedSpecificSoundPool(item), globalPool));
            }
            if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                return BuildBloodlessSoundFallbackPools(useBloodless, BuildClassifiedSoundPools(ResolveTwoHandedSpecificSoundPool(item), globalPool));
            }
            if (ReferenceEquals(proficiency, _unarmedProf))
            {
                return BuildBloodlessSoundFallbackPools(useBloodless, BuildClassifiedSoundPools(UnarmedSoundPool, globalPool));
            }
            if (ReferenceEquals(proficiency, _archeryProf))
            {
                return BuildBloodlessSoundFallbackPools(useBloodless, BuildClassifiedSoundPools(ResolveArcherySpecificSoundPool(item), globalPool));
            }
            if (ReferenceEquals(proficiency, _shieldProf))
            {
                return BuildBloodlessSoundFallbackPools(useBloodless, BuildClassifiedSoundPools(ShieldBashSoundPool, globalPool));
            }
            if (ReferenceEquals(proficiency, _magicProf))
            {
                return BuildBloodlessSoundFallbackPools(useBloodless, BuildClassifiedSoundPools(ResolveMagicSpecificSoundPool(item, damage), globalPool));
            }

            return BuildBloodlessSoundFallbackPools(useBloodless, BuildSoundFallbackPools(globalPool));
        }

        private string[] BuildClassifiedSoundPools(string primaryPool, string globalPool)
        {
            if (_useKillingBlowFallbackForClassifiedKills.Value)
            {
                return BuildSoundFallbackPools(primaryPool, globalPool);
            }

            return BuildSoundFallbackPools(primaryPool);
        }

        private string[] BuildBloodlessSoundFallbackPools(bool useBloodless, string[] normalPools)
        {
            if (!useBloodless || normalPools == null || normalPools.Length == 0)
            {
                return normalPools;
            }

            List<string> result = new List<string>();
            for (int i = 0; i < normalPools.Length; i++)
            {
                AddPoolName(result, GetBloodlessSoundPoolName(normalPools[i]));
                AddPoolName(result, normalPools[i]);
            }

            return result.ToArray();
        }

        private string GetBloodlessSoundPoolName(string poolName)
        {
            return string.IsNullOrWhiteSpace(poolName) ? "" : poolName + BloodlessSoundPoolSuffix;
        }

        private string GetFinisherSoundMode()
        {
            string mode = _finisherSoundMode == null ? "" : (_finisherSoundMode.Value ?? "").Trim();
            if (string.Equals(mode, FinisherSoundModeWeaponSpecific, StringComparison.OrdinalIgnoreCase))
            {
                return FinisherSoundModeWeaponSpecific;
            }
            if (string.Equals(mode, FinisherSoundModeSoulslike, StringComparison.OrdinalIgnoreCase))
            {
                return FinisherSoundModeSoulslike;
            }
            if (string.Equals(mode, FinisherSoundModeGoatTest, StringComparison.OrdinalIgnoreCase))
            {
                return FinisherSoundModeGoatTest;
            }
            if (string.Equals(mode, FinisherSoundModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return FinisherSoundModeOff;
            }

            LogDiagnostic("Unknown FinisherSoundMode '" + mode + "'; using " + FinisherSoundModeWeaponSpecific + ".");
            return FinisherSoundModeWeaponSpecific;
        }

        private float GetRangeVolumeMultiplier(object target)
        {
            float strength = Math.Max(0.0f, Math.Min(1.0f, _finisherSoundRangeVolume.Value));
            if (strength <= 0.001f)
            {
                return 1.0f;
            }

            Vector3 heroPosition;
            Vector3 targetPosition;
            if (!TryGetWorldPosition(GetCurrentHero(), out heroPosition) || !TryGetWorldPosition(target, out targetPosition))
            {
                LogDiagnostic("Could not resolve hero or target position for FinisherSoundRangeVolume; using full reward-sound volume.");
                return 1.0f;
            }

            float distance = Vector3.Distance(heroPosition, targetPosition);
            float t = Math.Max(0.0f, Math.Min(1.0f, distance / 100.0f));
            float rangeCurveVolume = 1.0f - (0.9f * t);
            float multiplier = 1.0f + ((rangeCurveVolume - 1.0f) * strength);
            LogDiagnostic("FinisherSoundRangeVolume distance=" + distance.ToString("0.##", CultureInfo.InvariantCulture) + "m, multiplier=" + multiplier.ToString("0.###", CultureInfo.InvariantCulture) + ".");
            return multiplier;
        }

        private bool TryGetWorldPosition(object value, out Vector3 position)
        {
            return TryGetWorldPosition(value, out position, 0);
        }

        private bool TryGetWorldPosition(object value, out Vector3 position, int depth)
        {
            position = Vector3.zero;
            if (depth > 4 || value == null || IsDestroyedUnityObject(value))
            {
                return false;
            }

            Transform transform = value as Transform;
            if (transform != null)
            {
                position = transform.position;
                return true;
            }

            GameObject gameObject = value as GameObject;
            if (gameObject != null && gameObject.transform != null)
            {
                position = gameObject.transform.position;
                return true;
            }

            Component component = value as Component;
            if (component != null && component.transform != null)
            {
                position = component.transform.position;
                return true;
            }

            if (TryGetVector3Property(value, "Position", out position)
                || TryGetVector3Property(value, "Coords", out position)
                || TryGetVector3Property(value, "WorldPosition", out position))
            {
                return true;
            }

            string[] propertyNames =
            {
                "Transform",
                "transform",
                "GameObject",
                "gameObject",
                "CharacterView",
                "View",
                "NpcElement",
                "Character",
                "Model",
                "ParentModel",
                "Owner"
            };

            for (int i = 0; i < propertyNames.Length; i++)
            {
                object nested = GetOptionalPropertyValue(value, propertyNames[i]);
                if (!ReferenceEquals(nested, value) && TryGetWorldPosition(nested, out position, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetVector3Property(object value, string propertyName, out Vector3 position)
        {
            position = Vector3.zero;
            object raw = GetOptionalPropertyValue(value, propertyName);
            if (raw is Vector3)
            {
                position = (Vector3)raw;
                return true;
            }

            return false;
        }

        private string ResolveOneHandedSpecificSoundPool(object item)
        {
            if (GetBoolProperty(item, "IsBlunt", false))
            {
                return OneHandedBluntSoundPool;
            }
            if (GetBoolProperty(item, "IsAxe", false))
            {
                return OneHandedAxeSoundPool;
            }
            if (GetBoolProperty(item, "IsDagger", false)
                || GetBoolProperty(item, "IsSword", false)
                || GetBoolProperty(item, "IsSickle", false)
                || GetBoolProperty(item, "IsPolearm", false))
            {
                return OneHandedBladeSoundPool;
            }

            if (GetBoolProperty(item, "IsOneHanded", false))
            {
                LogDiagnostic("One Handed kill had no specific weapon subtype; using one_handed_blade instead of the generic killing_blow pool.");
                return OneHandedBladeSoundPool;
            }

            LogDiagnostic("One Handed kill had no item subtype data; using one_handed_blade instead of the generic killing_blow pool.");
            return OneHandedBladeSoundPool;
        }

        private string ResolveTwoHandedSpecificSoundPool(object item)
        {
            if (GetBoolProperty(item, "IsBlunt", false))
            {
                return TwoHandedBluntSoundPool;
            }
            if (GetBoolProperty(item, "IsAxe", false))
            {
                return TwoHandedAxeSoundPool;
            }
            if (GetBoolProperty(item, "IsSword", false) || GetBoolProperty(item, "IsPolearm", false))
            {
                return TwoHandedBladeSoundPool;
            }

            if (GetBoolProperty(item, "IsTwoHanded", false))
            {
                LogDiagnostic("Two Handed kill had no specific weapon subtype; using two_handed_blade instead of the generic killing_blow pool.");
                return TwoHandedBladeSoundPool;
            }

            LogDiagnostic("Two Handed kill had no item subtype data; using two_handed_blade instead of the generic killing_blow pool.");
            return TwoHandedBladeSoundPool;
        }

        private string ResolveArcherySpecificSoundPool(object item)
        {
            if (GetBoolProperty(item, "IsHeavyBow", false))
            {
                return ArcheryHeavySoundPool;
            }
            if (GetBoolProperty(item, "IsMediumBow", false))
            {
                return ArcheryMediumSoundPool;
            }
            if (GetBoolProperty(item, "IsShortBow", false))
            {
                return ArcheryShortSoundPool;
            }

            if (GetBoolProperty(item, "IsRanged", false))
            {
                LogDiagnostic("Archery kill had no bow tier; using archery_medium instead of the generic killing_blow pool.");
                return ArcheryMediumSoundPool;
            }

            LogDiagnostic("Archery kill had no item subtype data; using archery_medium instead of the generic killing_blow pool.");
            return ArcheryMediumSoundPool;
        }

        private string ResolveMagicSpecificSoundPool(object item, object damage)
        {
            if (LooksLikeBloodMagic(item, damage))
            {
                return MagicBloodSoundPool;
            }
            if (LooksLikeFireMagic(item, damage))
            {
                return MagicFireSoundPool;
            }
            if (DamageHasSubtype(damage, "Cold"))
            {
                return MagicFrostSoundPool;
            }
            if (DamageHasSubtype(damage, "Poison") || ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Poison"))
            {
                return MagicPoisonSoundPool;
            }
            if (DamageHasSubtype(damage, "Electric"))
            {
                return MagicElectricSoundPool;
            }
            if (DamageHasSubtype(damage, "Wyrdness") || SourceTextContains(item, damage, new[] { "wyrd" }))
            {
                return MagicWyrdnessSoundPool;
            }
            if (DamageHasSubtype(damage, "Wet") || SourceTextContains(item, damage, new[] { "water", "wet" }))
            {
                return MagicWaterSoundPool;
            }

            return MagicArcaneSoundPool;
        }

        private bool LooksLikeBloodMagic(object item, object damage)
        {
            return ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Bleed")
                || SourceTextContains(item, damage, new[] { "blood", "bleed", "sanguine", "sanguis", "hematic", "transfusion", "abhartach" });
        }

        private bool LooksLikeFireMagic(object item, object damage)
        {
            return DamageHasSubtype(damage, "Fire")
                || ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Burn")
                || SourceTextContains(item, damage, new[] { "fire", "flame", "burn", "pyro" });
        }

        private bool SourceTextContains(object item, object damage, string[] terms)
        {
            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            string text = (DescribeObject(item) + " "
                + DescribeObject(GetOptionalPropertyValue(item, "Template")) + " "
                + DescribeObject(GetOptionalPropertyValue(damage, "Skill")) + " "
                + DescribeObject(GetOptionalPropertyValue(damage, "Type")) + " "
                + DescribeObject(GetOptionalPropertyValue(damage, "StatusDamageType")) + " "
                + DescribeObject(GetOptionalPropertyValue(projectile, "SourceWeapon")) + " "
                + DescribeObject(GetOptionalPropertyValue(projectile, "SourceProjectile"))).ToLowerInvariant();

            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i]) && text.Contains(terms[i].ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private bool DamageHasSubtype(object damage, string subtypeName)
        {
            return EnumerablePartsContainName(GetOptionalPropertyValue(damage, "SubTypes"), "SubType", subtypeName)
                || EnumerablePartsContainName(GetOptionalPropertyValue(GetOptionalPropertyValue(damage, "DamageTypeData"), "OriginalParts"), "SubType", subtypeName);
        }

        private bool EnumerablePartsContainName(object parts, string propertyName, string expected)
        {
            IEnumerable enumerable = parts as IEnumerable;
            if (enumerable != null)
            {
                foreach (object part in enumerable)
                {
                    if (ValueNameContains(GetOptionalPropertyValue(part, propertyName), expected) || ValueNameContains(part, expected))
                    {
                        return true;
                    }
                }
            }

            int count = GetOptionalIntProperty(parts, "Count", -1);
            if (count > 0)
            {
                PropertyInfo itemProperty = GetIndexerProperty(parts.GetType());
                if (itemProperty != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        object part = GetIndexedValue(itemProperty, parts, i);
                        if (ValueNameContains(GetOptionalPropertyValue(part, propertyName), expected) || ValueNameContains(part, expected))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool ValueNameContains(object value, string expected)
        {
            if (value == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            return value.ToString().IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsBloodlessSoundTarget(object target, object damage)
        {
            if (_useBloodlessSoundVariants == null || !_useBloodlessSoundVariants.Value)
            {
                return false;
            }

            string text = BuildTargetSearchText(target, damage);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string matched;
            if (ContainsAnyTerm(text, GetBloodlessSoundWhitelistTerms(), out matched))
            {
                LogDiagnostic("Bloodless sound routing skipped for " + DescribeObject(target) + ": matched whitelist term '" + matched + "'.");
                return false;
            }

            if (ContainsAnyTerm(text, GetBloodlessSoundBlacklistTerms(), out matched))
            {
                LogDiagnostic("Bloodless sound routing enabled for " + DescribeObject(target) + ": matched term '" + matched + "'.");
                return true;
            }

            return false;
        }

        private bool IsNonCorporealSoundTarget(object target, object damage)
        {
            if (_useNonCorporealEnemySounds == null || !_useNonCorporealEnemySounds.Value)
            {
                return false;
            }

            string text = BuildTargetSearchText(target, damage);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string matched;
            if (ContainsAnyTerm(text, GetNonCorporealSoundExclusionTerms(), out matched))
            {
                LogDiagnostic("Non-corporeal sound routing skipped for " + DescribeObject(target) + ": matched exclusion term '" + matched + "'.");
                return false;
            }

            if (ContainsAnyTerm(text, GetNonCorporealSoundTerms(), out matched))
            {
                LogDiagnostic("Non-corporeal sound routing enabled for " + DescribeObject(target) + ": matched term '" + matched + "'.");
                return true;
            }

            return false;
        }

        private string BuildTargetSearchText(object target, object damage)
        {
            string text = BuildObjectSearchText(target);
            object damageTarget = ResolveDamageTargetOwner(null, damage);
            if (damageTarget != null && !ReferenceEquals(damageTarget, target))
            {
                text = text + " " + BuildObjectSearchText(damageTarget);
            }

            return text;
        }

        private string[] GetBloodlessSoundBlacklistTerms()
        {
            string raw = _bloodlessSoundBlacklistTerms == null ? "" : (_bloodlessSoundBlacklistTerms.Value ?? "");
            if (raw != _cachedBloodlessSoundBlacklistTermsRaw)
            {
                _cachedBloodlessSoundBlacklistTermsRaw = raw;
                _cachedBloodlessSoundBlacklistTerms = SplitTerms(raw);
            }

            return _cachedBloodlessSoundBlacklistTerms;
        }

        private string[] GetBloodlessSoundWhitelistTerms()
        {
            string raw = _bloodlessSoundWhitelistTerms == null ? "" : (_bloodlessSoundWhitelistTerms.Value ?? "");
            if (raw != _cachedBloodlessSoundWhitelistTermsRaw)
            {
                _cachedBloodlessSoundWhitelistTermsRaw = raw;
                _cachedBloodlessSoundWhitelistTerms = SplitTerms(raw);
            }

            return _cachedBloodlessSoundWhitelistTerms;
        }

        private string[] GetNonCorporealSoundTerms()
        {
            string raw = _nonCorporealSoundTerms == null ? "" : (_nonCorporealSoundTerms.Value ?? "");
            if (raw != _cachedNonCorporealSoundTermsRaw)
            {
                _cachedNonCorporealSoundTermsRaw = raw;
                _cachedNonCorporealSoundTerms = SplitTerms(raw);
            }

            return _cachedNonCorporealSoundTerms;
        }

        private string[] GetNonCorporealSoundExclusionTerms()
        {
            string raw = _nonCorporealSoundExclusionTerms == null ? "" : (_nonCorporealSoundExclusionTerms.Value ?? "");
            if (raw != _cachedNonCorporealSoundExclusionTermsRaw)
            {
                _cachedNonCorporealSoundExclusionTermsRaw = raw;
                _cachedNonCorporealSoundExclusionTerms = SplitTerms(raw);
            }

            return _cachedNonCorporealSoundExclusionTerms;
        }

        private bool ContainsAnyTerm(string text, string[] terms, out string matched)
        {
            matched = "";
            if (string.IsNullOrEmpty(text) || terms == null)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i]) && text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched = terms[i];
                    return true;
                }
            }

            return false;
        }

        private string[] SplitTerms(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new string[0];
            }

            string[] pieces = raw.Split(new[] { ';', ',', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> terms = new List<string>();
            for (int i = 0; i < pieces.Length; i++)
            {
                string term = pieces[i].Trim();
                if (term.Length > 0)
                {
                    terms.Add(term);
                }
            }

            return terms.ToArray();
        }

        private string BuildObjectSearchText(object obj)
        {
            if (obj == null || IsDestroyedUnityObject(obj))
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            Type type = obj.GetType();
            builder.Append(type.FullName).Append(' ');
            builder.Append(type.Name).Append(' ');
            AppendStringProperty(builder, obj, "Name");
            AppendStringProperty(builder, obj, "DisplayName");
            AppendStringProperty(builder, obj, "DebugName");
            AppendStringProperty(builder, obj, "TechnicalName");
            AppendStringProperty(builder, obj, "Id");
            AppendStringProperty(builder, obj, "ID");

            object template = GetOptionalPropertyValue(obj, "Template");
            if (template != null && !ReferenceEquals(template, obj) && !IsDestroyedUnityObject(template))
            {
                Type templateType = template.GetType();
                builder.Append(templateType.FullName).Append(' ');
                builder.Append(templateType.Name).Append(' ');
                AppendStringProperty(builder, template, "Name");
                AppendStringProperty(builder, template, "DisplayName");
                AppendStringProperty(builder, template, "DebugName");
                AppendStringProperty(builder, template, "TechnicalName");
                AppendStringProperty(builder, template, "GUID");
                AppendStringProperty(builder, template, "Guid");
            }

            return builder.ToString();
        }

        private void AppendStringProperty(StringBuilder builder, object obj, string propertyName)
        {
            object raw = GetOptionalPropertyValue(obj, propertyName);
            if (raw == null)
            {
                return;
            }

            string value = raw as string;
            if (value == null)
            {
                value = raw.ToString();
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append(value).Append(' ');
            }
        }

        private string[] BuildSoundFallbackPools(params string[] candidates)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < candidates.Length; i++)
            {
                AddPoolName(result, candidates[i]);
            }

            return result.ToArray();
        }

        private void AddPoolName(List<string> poolNames, string poolName)
        {
            if (string.IsNullOrWhiteSpace(poolName))
            {
                return;
            }

            for (int i = 0; i < poolNames.Count; i++)
            {
                if (string.Equals(poolNames[i], poolName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            poolNames.Add(poolName);
        }

        private bool IsHeroKillingBlow(object outcome, object damage)
        {
            object hero = GetCurrentHero();
            if (hero == null)
            {
                return false;
            }

            object attacker = GetOptionalPropertyValue(outcome, "AttackerPure");
            if (attacker == null)
            {
                attacker = GetOptionalPropertyValue(outcome, "Attacker");
            }
            if (IsSameModelOrOwner(attacker, hero))
            {
                return true;
            }

            return IsHeroDamageSource(damage, hero);
        }

        private bool IsHeroDamageSource(object damage, object hero)
        {
            if (damage == null || hero == null)
            {
                return false;
            }

            object damageDealer = GetOptionalPropertyValue(damage, "DamageDealerPure");
            if (damageDealer == null)
            {
                damageDealer = GetOptionalPropertyValue(damage, "DamageDealer");
            }
            if (IsSameModelOrOwner(damageDealer, hero))
            {
                return true;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            object projectileOwner = GetOptionalPropertyValue(projectile, "Owner");
            return IsSameModelOrOwner(projectileOwner, hero);
        }

        private object ResolveKillingItem(object damage)
        {
            object item = GetOptionalPropertyValue(damage, "Item");
            if (LooksLikeWeaponItem(item))
            {
                return item;
            }

            item = GetOptionalPropertyValue(damage, "BlockingItem");
            if (LooksLikeWeaponItem(item))
            {
                return item;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            item = GetOptionalPropertyValue(projectile, "SourceWeapon");
            if (LooksLikeWeaponItem(item))
            {
                return item;
            }

            item = GetOptionalPropertyValue(projectile, "SourceProjectile");
            if (LooksLikeWeaponItem(item))
            {
                return item;
            }

            return null;
        }

        private bool LooksLikeWeaponItem(object item)
        {
            if (item == null)
            {
                return false;
            }

            bool isWeaponFound;
            bool isWeapon = GetBoolProperty(item, "IsWeapon", false, out isWeaponFound);
            if (isWeaponFound && isWeapon)
            {
                return true;
            }

            return GetBoolProperty(item, "IsOneHanded", false) ||
                GetBoolProperty(item, "IsTwoHanded", false) ||
                GetBoolProperty(item, "IsFists", false) ||
                GetBoolProperty(item, "IsDefaultFists", false) ||
                GetBoolProperty(item, "IsRanged", false) ||
                GetBoolProperty(item, "IsShield", false) ||
                GetBoolProperty(item, "IsBlocking", false) ||
                GetBoolProperty(item, "CanBeUsedAsShield", false) ||
                LooksLikeMagicItem(item);
        }

        private bool LooksLikeMagicItem(object item)
        {
            return GetBoolProperty(item, "IsMagic", false) ||
                GetBoolProperty(item, "IsCastMagic", false) ||
                GetBoolProperty(item, "IsRod", false);
        }

        private object ResolveKillingProficiency(object item, object damage)
        {
            object proficiency = ResolveItemProficiency(item);
            if (proficiency != null)
            {
                return proficiency;
            }

            if (LooksLikeMagicDamage(damage))
            {
                return _magicProf;
            }

            return null;
        }

        private object ResolveItemProficiency(object item)
        {
            object proficiency = null;
            if (_profFromAbstractsMethod != null && item != null)
            {
                try
                {
                    proficiency = _profFromAbstractsMethod.Invoke(null, new[] { item, true });
                }
                catch (Exception ex)
                {
                    LogDiagnostic("ProfFromAbstracts failed for " + DescribeObject(item) + ": " + ex.GetType().Name + ".");
                }
            }

            if (proficiency != null)
            {
                return proficiency;
            }

            if (GetBoolProperty(item, "IsOneHanded", false))
            {
                return _oneHandedProf;
            }
            if (GetBoolProperty(item, "IsTwoHanded", false))
            {
                return _twoHandedProf;
            }
            if (GetBoolProperty(item, "IsFists", false) || GetBoolProperty(item, "IsDefaultFists", false))
            {
                return _unarmedProf;
            }
            if (GetBoolProperty(item, "IsRanged", false))
            {
                return _archeryProf;
            }
            if (GetBoolProperty(item, "IsShield", false)
                || GetBoolProperty(item, "IsBlocking", false)
                || GetBoolProperty(item, "CanBeUsedAsShield", false))
            {
                return _shieldProf;
            }
            if (LooksLikeMagicItem(item))
            {
                return _magicProf;
            }

            return null;
        }

        private bool LooksLikeMagicDamage(object damage)
        {
            if (damage == null)
            {
                return false;
            }

            if (ValueNameContains(GetOptionalPropertyValue(damage, "Type"), "MagicalHitSource"))
            {
                return true;
            }

            object skill = GetOptionalPropertyValue(damage, "Skill");
            if (skill != null && SourceTextContains(null, damage, new[] { "magic", "spell", "cast", "blood", "fire", "flame", "wyrd" }))
            {
                return true;
            }

            return DamageHasSubtype(damage, "GenericMagical")
                || DamageHasSubtype(damage, "Fire")
                || DamageHasSubtype(damage, "Cold")
                || DamageHasSubtype(damage, "Electric")
                || DamageHasSubtype(damage, "Wyrdness");
        }

        private bool IsEligibleProficiency(object proficiency)
        {
            if (proficiency == null)
            {
                return false;
            }

            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                return _allowOneHanded.Value;
            }
            if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                return _allowTwoHanded.Value;
            }
            if (ReferenceEquals(proficiency, _unarmedProf))
            {
                return _allowUnarmed.Value;
            }
            if (ReferenceEquals(proficiency, _archeryProf))
            {
                return _allowArchery.Value;
            }
            if (ReferenceEquals(proficiency, _shieldProf))
            {
                return _allowShield.Value;
            }
            if (ReferenceEquals(proficiency, _magicProf))
            {
                return _allowMagic.Value;
            }

            return false;
        }

        private float CalculateBonus(float enemyXp)
        {
            float bonus = 0.0f;
            if (enemyXp > 0.0f)
            {
                bonus = enemyXp * Math.Max(0.0f, _bonusPercentOfEnemyXp.Value) / 100.0f;
            }
            else
            {
                bonus = Math.Max(0.0f, _fallbackBonusXp.Value);
            }

            if (bonus > 0.0f)
            {
                bonus = Math.Max(Math.Max(0.0f, _minimumBonusXp.Value), bonus);
            }

            float cap = _maximumBonusXp.Value;
            if (cap > 0.0f)
            {
                bonus = Math.Min(cap, bonus);
            }

            float roundTo = _roundBonusXpTo.Value;
            if (roundTo > 0.0f && bonus > 0.0f)
            {
                bonus = (float)(Math.Round(bonus / roundTo, MidpointRounding.AwayFromZero) * roundTo);
            }

            return Math.Max(0.0f, bonus);
        }

        private bool TryAwardProficiencyXp(object proficiency, float bonus)
        {
            object hero = GetCurrentHero();
            object profStats = GetOptionalPropertyValue(hero, "ProficiencyStats");
            if (profStats == null || proficiency == null || bonus <= 0.0f)
            {
                return false;
            }

            if (_tryAddXpMethod == null)
            {
                _tryAddXpMethod = AccessTools.Method(profStats.GetType(), "TryAddXP", new[] { _profStatType, typeof(float) });
            }

            if (_tryAddXpMethod == null)
            {
                Log.LogWarning("Could not find ProficiencyStats.TryAddXP.");
                return false;
            }

            try
            {
                _tryAddXpMethod.Invoke(profStats, new[] { proficiency, (object)bonus });
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to award killing-blow proficiency XP: " + ex.GetType().Name + " " + ex.Message);
                return false;
            }
        }

        private object ResolveDamageTargetOwner(object healthElement, object damage)
        {
            object target = GetOptionalPropertyValue(damage, "Target");
            if (target == null)
            {
                target = GetOptionalPropertyValue(damage, "TargetPure");
            }
            if (target == null)
            {
                target = ResolveHealthElementOwner(healthElement);
            }

            return target;
        }

        private object ResolveHealthElementOwner(object healthElement)
        {
            if (healthElement == null)
            {
                return null;
            }

            string[] ownerProperties = { "ParentModel", "GenericParentModel", "NpcElement", "Character", "CharacterView", "Owner", "Parent" };
            for (int i = 0; i < ownerProperties.Length; i++)
            {
                object value = GetOptionalPropertyValue(healthElement, ownerProperties[i]);
                if (value != null && !ReferenceEquals(value, healthElement))
                {
                    return value;
                }
            }

            return null;
        }

        private float GetDamageAmount(object damage)
        {
            float amount = GetOptionalFloatProperty(damage, "Amount", -1.0f);
            if (amount >= 0.0f)
            {
                return amount;
            }

            object rawData = GetOptionalPropertyValue(damage, "RawData");
            return GetOptionalFloatProperty(rawData, "Amount", 0.0f);
        }

        private bool IsDestroyedUnityObject(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return !ReferenceEquals(unityObject, null) && unityObject == null;
        }

        private object GetCurrentHero()
        {
            if (_heroCurrentGetter == null)
            {
                return null;
            }

            try
            {
                return _heroCurrentGetter.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }

        private bool IsSameModelOrOwner(object candidate, object expected)
        {
            if (candidate == null || expected == null)
            {
                return false;
            }

            if (ReferenceEquals(candidate, expected))
            {
                return true;
            }

            string[] properties = { "ParentModel", "GenericParentModel", "Owner", "Character", "Hero" };
            for (int i = 0; i < properties.Length; i++)
            {
                object value = GetOptionalPropertyValue(candidate, properties[i]);
                if (ReferenceEquals(value, expected))
                {
                    return true;
                }
            }

            return false;
        }

        private float TryReadExpReward(object owner)
        {
            if (owner == null)
            {
                return 0.0f;
            }

            object template = GetOptionalPropertyValue(owner, "Template");
            if (template != null && !ReferenceEquals(template, owner))
            {
                float templateValue = TryReadExpRewardDirect(template);
                if (templateValue > 0.0f)
                {
                    return templateValue;
                }
            }

            return TryReadExpRewardDirect(owner);
        }

        private float TryReadExpRewardDirect(object owner)
        {
            if (owner == null)
            {
                return 0.0f;
            }

            string[] properties = { "ExpReward", "XPReward", "XpReward", "ExperienceReward" };
            for (int i = 0; i < properties.Length; i++)
            {
                float value = GetOptionalFloatProperty(owner, properties[i], 0.0f);
                if (value > 0.0f)
                {
                    return value;
                }
            }

            MethodInfo method = GetMethodSilent(owner.GetType(), "GetExpReward", 0);
            if (method == null)
            {
                return 0.0f;
            }

            try
            {
                object result = method.Invoke(owner, null);
                if (result is int)
                {
                    return (int)result;
                }
                if (result is float)
                {
                    return (float)result;
                }
                if (result is double)
                {
                    return (float)(double)result;
                }
            }
            catch
            {
                return 0.0f;
            }

            return 0.0f;
        }

        private object GetOptionalPropertyValue(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            try
            {
                PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private float GetOptionalFloatProperty(object instance, string propertyName, float fallback)
        {
            object value = GetOptionalPropertyValue(instance, propertyName);
            if (value is int)
            {
                return (int)value;
            }
            if (value is float)
            {
                return (float)value;
            }
            if (value is double)
            {
                return (float)(double)value;
            }

            return fallback;
        }

        private int GetOptionalIntProperty(object instance, string propertyName, int fallback)
        {
            object value = GetOptionalPropertyValue(instance, propertyName);
            if (value is int)
            {
                return (int)value;
            }
            if (value is uint)
            {
                uint uintValue = (uint)value;
                return uintValue > int.MaxValue ? int.MaxValue : (int)uintValue;
            }

            return fallback;
        }

        private PropertyInfo GetIndexerProperty(Type type)
        {
            if (type == null)
            {
                return null;
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < properties.Length; i++)
            {
                ParameterInfo[] parameters = properties[i].GetIndexParameters();
                if (properties[i].Name == "Item"
                    && parameters.Length == 1
                    && parameters[0].ParameterType == typeof(int))
                {
                    return properties[i];
                }
            }

            return null;
        }

        private object GetIndexedValue(PropertyInfo property, object instance, int index)
        {
            if (property == null || instance == null)
            {
                return null;
            }

            try
            {
                return property.GetValue(instance, new object[] { index });
            }
            catch
            {
                return null;
            }
        }

        private bool GetBoolProperty(object instance, string propertyName, bool fallback)
        {
            bool found;
            return GetBoolProperty(instance, propertyName, fallback, out found);
        }

        private bool GetBoolProperty(object instance, string propertyName, bool fallback, out bool found)
        {
            found = false;
            object value = GetOptionalPropertyValue(instance, propertyName);
            if (value is bool)
            {
                found = true;
                return (bool)value;
            }

            return fallback;
        }

        private object GetStaticFieldValue(Type type, string fieldName)
        {
            if (type == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(type, fieldName);
            if (field == null)
            {
                return null;
            }

            return field.GetValue(null);
        }

        private MethodInfo GetMethodSilent(Type type, string methodName, int parameterCount)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == methodName && methods[i].GetParameters().Length == parameterCount)
                {
                    return methods[i];
                }
            }

            return null;
        }

        private ConstructorInfo GetConstructorSilent(Type type, Type[] parameterTypes)
        {
            if (type == null || parameterTypes == null)
            {
                return null;
            }

            try
            {
                return type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
            }
            catch
            {
                return null;
            }
        }

        private string FormatFloat(float value)
        {
            if (Math.Abs(value - (float)Math.Round(value)) < 0.001f)
            {
                return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private string DescribeNotificationProficiency(object proficiency, string fallback)
        {
            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                return "One Handed";
            }
            if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                return "Two Handed";
            }

            return string.IsNullOrWhiteSpace(fallback) ? DescribeObject(proficiency) : fallback;
        }

        private string DescribeProficiency(object proficiency)
        {
            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                return "OneHanded";
            }
            if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                return "TwoHanded";
            }
            if (ReferenceEquals(proficiency, _unarmedProf))
            {
                return "Unarmed";
            }
            if (ReferenceEquals(proficiency, _archeryProf))
            {
                return "Archery";
            }
            if (ReferenceEquals(proficiency, _shieldProf))
            {
                return "Shield";
            }
            if (ReferenceEquals(proficiency, _magicProf))
            {
                return "Magic";
            }

            return DescribeObject(proficiency);
        }

        private string DescribeKillSource(object item, object damage)
        {
            string itemName = DescribeObject(item);
            if (!string.Equals(itemName, "null", StringComparison.Ordinal))
            {
                return itemName;
            }

            string skillName = DescribeObject(GetOptionalPropertyValue(damage, "Skill"));
            if (!string.Equals(skillName, "null", StringComparison.Ordinal))
            {
                return skillName;
            }

            if (LooksLikeMagicDamage(damage))
            {
                return "magic";
            }

            return "unknown source";
        }

        private string DescribeObject(object value)
        {
            if (value == null)
            {
                return "null";
            }

            string displayName = GetOptionalPropertyValue(value, "DisplayName") as string;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = GetOptionalPropertyValue(value, "Name") as string;
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = GetOptionalPropertyValue(value, "DebugName") as string;
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName;
            }

            return value.GetType().Name;
        }

        private void RecordStatistics(object npc, object proficiency, string proficiencyName, object item, object damage, string sourceName, float bonus, bool damageOverTimeKill)
        {
            if (!_trackStatistics.Value)
            {
                return;
            }

            object facts = GetStatisticsFacts();
            if (facts == null)
            {
                LogStatisticsMemoryUnavailableOnce("GameplayMemory is unavailable; killing-blow statistics were not recorded.");
                return;
            }

            string characterDisplayName;
            string characterKey = ResolveStatisticsCharacterKey(out characterDisplayName);
            SetFact(facts, MakeStatisticsKey(characterKey, "display"), characterDisplayName);
            AddStatisticsCount(facts, MakeStatisticsKey(characterKey, "total"), bonus, damageOverTimeKill);

            AddStatisticsCount(
                facts,
                MakeStatisticsKey(characterKey, "proficiency", BuildStatsKey(proficiencyName)),
                bonus,
                damageOverTimeKill);

            AddStatisticsCount(
                facts,
                MakeStatisticsKey(characterKey, "source_pool", ResolveStatisticsSourcePool(proficiency, item, damage, npc)),
                bonus,
                damageOverTimeKill);

            string cleanSourceName = CleanStatsDisplayName(sourceName, "unknown source");
            string sourceKey = BuildStatsKey(cleanSourceName);
            SetFact(facts, MakeStatisticsKey(characterKey, "kill_source", sourceKey, "display"), cleanSourceName);
            AddStatisticsCount(facts, MakeStatisticsKey(characterKey, "kill_source", sourceKey), bonus, damageOverTimeKill);

            if (!string.Equals(GetEnemyStatsMode(), EnemyStatsModeOff, StringComparison.OrdinalIgnoreCase))
            {
                string enemyDisplayName = ResolveEnemyStatsDisplayName(npc);
                string enemyKey = ResolveEntityStatsKey(npc, enemyDisplayName);
                SetFact(facts, MakeStatisticsKey(characterKey, "enemy", enemyKey, "display"), enemyDisplayName);
                AddStatisticsCount(facts, MakeStatisticsKey(characterKey, "enemy", enemyKey), bonus, damageOverTimeKill);
            }
        }

        internal void OnGameplayMemoryBeforeSerialize()
        {
            if (!_trackStatistics.Value || !_exportStatisticsReportOnSave.Value)
            {
                return;
            }

            ExportStatisticsReportFromSaveMemory();
        }

        private void ExportStatisticsReportFromSaveMemory()
        {
            object facts = GetStatisticsFacts();
            if (facts == null)
            {
                LogStatisticsMemoryUnavailableOnce("GameplayMemory is unavailable; statistics report was not exported.");
                return;
            }

            Dictionary<string, CharacterStatistics> statistics = BuildStatisticsReport(facts);
            string path = GetStatisticsFilePath();
            string tempPath = path + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (StreamWriter writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("# Killing Blow Mastery statistics v2");
                    writer.WriteLine("# This is a readable report generated from save-backed GameplayMemory when the game saves.");
                    writer.WriteLine("# Deleting this file does not reset save-backed stats. Reloading an older save rolls stats back with that save.");
                    writer.WriteLine("# EnemyStatsMode=" + GetEnemyStatsMode() + "; EnemyPromoteKillCount=" + Math.Max(1, _enemyPromoteKillCount.Value).ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("# kind\tcharacterKey\t...");

                    List<string> characterKeys = SortedKeys(statistics);
                    for (int i = 0; i < characterKeys.Count; i++)
                    {
                        WriteCharacterStatistics(writer, statistics[characterKeys[i]]);
                    }
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
                LogDiagnostic("Exported save-backed Killing Blow Mastery statistics report to " + path + ".");
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to export Killing Blow Mastery statistics report: " + ex.GetBaseException().Message);
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }
            }
        }

        private Dictionary<string, CharacterStatistics> BuildStatisticsReport(object facts)
        {
            Dictionary<string, CharacterStatistics> statistics = new Dictionary<string, CharacterStatistics>(StringComparer.OrdinalIgnoreCase);
            IEnumerable entries = GetFactEntries(facts);
            if (entries == null)
            {
                return statistics;
            }

            foreach (object entry in entries)
            {
                object rawKey = GetOptionalPropertyValue(entry, "Key");
                object rawValue = GetOptionalPropertyValue(entry, "Value");
                string key = rawKey as string;
                if (string.IsNullOrEmpty(key) || !key.StartsWith("c|", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = key.Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                string characterKey = parts[1];
                CharacterStatistics character = GetOrCreateCharacterStatistics(statistics, characterKey, "Current Character");
                string category = parts[2];

                if (parts.Length == 3 && string.Equals(category, "display", StringComparison.Ordinal))
                {
                    character.DisplayName = CleanStatsDisplayName(rawValue == null ? "" : rawValue.ToString(), character.DisplayName);
                    continue;
                }

                if (parts.Length == 4 && string.Equals(category, "total", StringComparison.Ordinal))
                {
                    ApplyStatisticsMetric(character.Totals, parts[3], rawValue);
                    continue;
                }

                if (parts.Length == 5 && string.Equals(category, "proficiency", StringComparison.Ordinal))
                {
                    ApplyStatisticsMetric(GetOrCreateCount(character.Proficiencies, parts[3]), parts[4], rawValue);
                    continue;
                }

                if (parts.Length == 5 && string.Equals(category, "source_pool", StringComparison.Ordinal))
                {
                    ApplyStatisticsMetric(GetOrCreateCount(character.SourcePools, parts[3]), parts[4], rawValue);
                    continue;
                }

                if (parts.Length == 5 && string.Equals(category, "kill_source", StringComparison.Ordinal) && string.Equals(parts[4], "display", StringComparison.Ordinal))
                {
                    GetOrCreateNamedCount(character.KillSources, parts[3], CleanStatsDisplayName(rawValue == null ? "" : rawValue.ToString(), parts[3]));
                    continue;
                }

                if (parts.Length == 5 && string.Equals(category, "enemy", StringComparison.Ordinal) && string.Equals(parts[4], "display", StringComparison.Ordinal))
                {
                    GetOrCreateNamedCount(character.Enemies, parts[3], CleanStatsDisplayName(rawValue == null ? "" : rawValue.ToString(), parts[3]));
                    continue;
                }

                if (parts.Length == 5 && string.Equals(category, "kill_source", StringComparison.Ordinal))
                {
                    ApplyStatisticsMetric(GetOrCreateNamedCount(character.KillSources, parts[3], parts[3]), parts[4], rawValue);
                    continue;
                }

                if (parts.Length == 5 && string.Equals(category, "enemy", StringComparison.Ordinal))
                {
                    ApplyStatisticsMetric(GetOrCreateNamedCount(character.Enemies, parts[3], parts[3]), parts[4], rawValue);
                }
            }

            return statistics;
        }

        private void AddStatisticsCount(object facts, string prefix, float bonus, bool damageOverTimeKill)
        {
            SetFact(facts, prefix + "|kills", GetFact(facts, prefix + "|kills", 0) + 1);
            SetFact(facts, prefix + "|bonus_xp", GetFact(facts, prefix + "|bonus_xp", 0.0f) + bonus);
            if (damageOverTimeKill)
            {
                SetFact(facts, prefix + "|dot_kills", GetFact(facts, prefix + "|dot_kills", 0) + 1);
            }

            float largestBonus = GetFact(facts, prefix + "|largest_bonus_xp", 0.0f);
            if (bonus > largestBonus)
            {
                SetFact(facts, prefix + "|largest_bonus_xp", bonus);
            }
        }

        private object GetStatisticsFacts()
        {
            object gameplayMemory = GetGameplayMemory();
            if (gameplayMemory == null || _gameplayMemoryContextMethod == null)
            {
                return null;
            }

            try
            {
                return _gameplayMemoryContextMethod.Invoke(gameplayMemory, new object[] { StatisticsMemoryContext });
            }
            catch (Exception ex)
            {
                LogDiagnostic("GameplayMemory.Context failed for statistics: " + ex.GetBaseException().Message);
                return null;
            }
        }

        private object GetGameplayMemory()
        {
            if (_gameplayMemoryType == null || _worldServicesGetter == null)
            {
                return null;
            }

            try
            {
                object services = _worldServicesGetter.Invoke(null, null);
                if (services == null)
                {
                    return null;
                }

                if (_servicesGetGameplayMemoryMethod == null)
                {
                    _servicesGetGameplayMemoryMethod = ResolveServicesGetMethod(services.GetType());
                }

                return _servicesGetGameplayMemoryMethod == null
                    ? null
                    : _servicesGetGameplayMemoryMethod.Invoke(services, null);
            }
            catch (Exception ex)
            {
                LogDiagnostic("Could not resolve GameplayMemory service: " + ex.GetBaseException().Message);
                return null;
            }
        }

        private MethodInfo ResolveServicesGetMethod(Type servicesType)
        {
            if (servicesType == null || _gameplayMemoryType == null)
            {
                return null;
            }

            MethodInfo[] methods = servicesType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!method.IsGenericMethodDefinition
                    || method.Name != "Get"
                    || method.GetParameters().Length != 0)
                {
                    continue;
                }

                try
                {
                    return method.MakeGenericMethod(_gameplayMemoryType);
                }
                catch
                {
                }
            }

            return null;
        }

        private T GetFact<T>(object facts, string label, T fallback)
        {
            if (facts == null || string.IsNullOrEmpty(label))
            {
                return fallback;
            }

            try
            {
                MethodInfo method = GetFactsGetMethod(facts.GetType());
                if (method == null)
                {
                    return fallback;
                }

                object value = method.MakeGenericMethod(typeof(T)).Invoke(facts, new object[] { label, fallback });
                return value is T ? (T)value : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void SetFact<T>(object facts, string label, T value)
        {
            if (facts == null || string.IsNullOrEmpty(label))
            {
                return;
            }

            try
            {
                MethodInfo method = GetFactsSetMethod(facts.GetType());
                if (method == null || _emptyModelOwners == null)
                {
                    return;
                }

                method.MakeGenericMethod(typeof(T)).Invoke(facts, new object[] { label, value, _emptyModelOwners });
            }
            catch (Exception ex)
            {
                LogDiagnostic("Failed to set statistics fact '" + label + "': " + ex.GetBaseException().Message);
            }
        }

        private MethodInfo GetFactsGetMethod(Type factsType)
        {
            if (_factsGetGenericMethod != null)
            {
                return _factsGetGenericMethod;
            }

            MethodInfo[] methods = factsType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!method.IsGenericMethodDefinition || method.Name != "Get")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2)
                {
                    _factsGetGenericMethod = method;
                    return method;
                }
            }

            return null;
        }

        private MethodInfo GetFactsSetMethod(Type factsType)
        {
            if (_factsSetGenericMethod != null)
            {
                return _factsSetGenericMethod;
            }

            MethodInfo[] methods = factsType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!method.IsGenericMethodDefinition || method.Name != "Set")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 3)
                {
                    _factsSetGenericMethod = method;
                    return method;
                }
            }

            return null;
        }

        private IEnumerable GetFactEntries(object facts)
        {
            if (facts == null)
            {
                return null;
            }

            try
            {
                if (_factsGetAllMethod == null)
                {
                    _factsGetAllMethod = GetMethodSilent(facts.GetType(), "GetAll", 0);
                }

                return _factsGetAllMethod == null ? null : _factsGetAllMethod.Invoke(facts, null) as IEnumerable;
            }
            catch (Exception ex)
            {
                LogDiagnostic("Could not enumerate statistics facts: " + ex.GetBaseException().Message);
                return null;
            }
        }

        private void LogStatisticsMemoryUnavailableOnce(string message)
        {
            if (_statisticsMemoryUnavailableLogged)
            {
                return;
            }

            _statisticsMemoryUnavailableLogged = true;
            Log.LogWarning(message);
        }

        private CharacterStatistics GetOrCreateCharacterStatistics(Dictionary<string, CharacterStatistics> map, string key, string displayName)
        {
            key = CleanStatsDisplayName(key, "current_character");
            displayName = CleanStatsDisplayName(displayName, "Current Character");

            CharacterStatistics stats;
            if (!map.TryGetValue(key, out stats))
            {
                stats = new CharacterStatistics(key, displayName);
                map.Add(key, stats);
            }
            else if (!string.IsNullOrEmpty(displayName))
            {
                stats.DisplayName = displayName;
            }

            return stats;
        }

        private CountStatistics GetOrCreateCount(Dictionary<string, CountStatistics> map, string key)
        {
            key = CleanStatsDisplayName(key, "Unknown");

            CountStatistics stats;
            if (!map.TryGetValue(key, out stats))
            {
                stats = new CountStatistics();
                map.Add(key, stats);
            }

            return stats;
        }

        private NamedCountStatistics GetOrCreateNamedCount(Dictionary<string, NamedCountStatistics> map, string key, string displayName)
        {
            key = CleanStatsDisplayName(key, "unknown");
            displayName = CleanStatsDisplayName(displayName, key);

            NamedCountStatistics stats;
            if (!map.TryGetValue(key, out stats))
            {
                stats = new NamedCountStatistics(displayName);
                map.Add(key, stats);
            }
            else if (!string.IsNullOrEmpty(displayName))
            {
                stats.DisplayName = displayName;
            }

            return stats;
        }

        private void ApplyStatisticsMetric(CountStatistics stats, string metric, object rawValue)
        {
            if (stats == null || string.IsNullOrEmpty(metric))
            {
                return;
            }

            if (string.Equals(metric, "kills", StringComparison.Ordinal))
            {
                stats.Kills = ConvertStatsInt(rawValue);
                return;
            }
            if (string.Equals(metric, "bonus_xp", StringComparison.Ordinal))
            {
                stats.BonusXp = ConvertStatsFloat(rawValue);
                return;
            }
            if (string.Equals(metric, "dot_kills", StringComparison.Ordinal))
            {
                stats.DamageOverTimeKills = ConvertStatsInt(rawValue);
                return;
            }
            if (string.Equals(metric, "largest_bonus_xp", StringComparison.Ordinal))
            {
                stats.LargestBonusXp = ConvertStatsFloat(rawValue);
            }
        }

        private int ConvertStatsInt(object value)
        {
            if (value is int)
            {
                return (int)value;
            }
            if (value is float)
            {
                return (int)Math.Round((float)value);
            }
            if (value is double)
            {
                return (int)Math.Round((double)value);
            }

            int parsed;
            return value != null && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private float ConvertStatsFloat(object value)
        {
            if (value is float)
            {
                return (float)value;
            }
            if (value is int)
            {
                return (int)value;
            }
            if (value is double)
            {
                return (float)(double)value;
            }

            float parsed;
            return value != null && float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0.0f;
        }

        private void WriteCharacterStatistics(StreamWriter writer, CharacterStatistics character)
        {
            writer.WriteLine();
            WriteCountLine(writer, "character", character.Key, character.DisplayName, character.Totals);

            List<string> proficiencyKeys = SortedKeys(character.Proficiencies);
            for (int i = 0; i < proficiencyKeys.Count; i++)
            {
                WriteCountLine(writer, "proficiency", character.Key, proficiencyKeys[i], character.Proficiencies[proficiencyKeys[i]]);
            }

            List<string> sourcePoolKeys = SortedKeys(character.SourcePools);
            for (int i = 0; i < sourcePoolKeys.Count; i++)
            {
                WriteCountLine(writer, "source_pool", character.Key, sourcePoolKeys[i], character.SourcePools[sourcePoolKeys[i]]);
            }

            List<string> killSourceKeys = SortedKeys(character.KillSources);
            for (int i = 0; i < killSourceKeys.Count; i++)
            {
                NamedCountStatistics stats = character.KillSources[killSourceKeys[i]];
                WriteNamedCountLine(writer, "kill_source", character.Key, killSourceKeys[i], stats.DisplayName, stats);
            }

            string enemyMode = GetEnemyStatsMode();
            if (string.Equals(enemyMode, EnemyStatsModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int promoteKillCount = Math.Max(1, _enemyPromoteKillCount.Value);
            List<string> enemyKeys = SortedKeys(character.Enemies);
            for (int i = 0; i < enemyKeys.Count; i++)
            {
                NamedCountStatistics stats = character.Enemies[enemyKeys[i]];
                string rowKind = "enemy";
                if (string.Equals(enemyMode, EnemyStatsModePromoted, StringComparison.OrdinalIgnoreCase)
                    && stats.Kills < promoteKillCount)
                {
                    rowKind = "enemy_candidate";
                }

                WriteNamedCountLine(writer, rowKind, character.Key, enemyKeys[i], stats.DisplayName, stats);
            }
        }

        private void WriteCountLine(StreamWriter writer, string kind, string characterKey, string keyOrDisplayName, CountStatistics stats)
        {
            writer.Write(kind);
            writer.Write('\t');
            writer.Write(EncodeStatsText(characterKey));
            writer.Write('\t');
            writer.Write(EncodeStatsText(keyOrDisplayName));
            writer.Write('\t');
            WriteCountColumns(writer, stats);
            writer.WriteLine();
        }

        private void WriteNamedCountLine(StreamWriter writer, string kind, string characterKey, string key, string displayName, CountStatistics stats)
        {
            writer.Write(kind);
            writer.Write('\t');
            writer.Write(EncodeStatsText(characterKey));
            writer.Write('\t');
            writer.Write(EncodeStatsText(key));
            writer.Write('\t');
            writer.Write(EncodeStatsText(displayName));
            writer.Write('\t');
            WriteCountColumns(writer, stats);
            writer.WriteLine();
        }

        private void WriteCountColumns(StreamWriter writer, CountStatistics stats)
        {
            writer.Write(stats.Kills.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(FormatFloat(stats.BonusXp));
            writer.Write('\t');
            writer.Write(stats.DamageOverTimeKills.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(FormatFloat(stats.LargestBonusXp));
        }

        private string ResolveStatisticsCharacterKey(out string displayName)
        {
            string overrideValue = _statisticsCharacterKeyOverride == null ? "" : (_statisticsCharacterKeyOverride.Value ?? "").Trim();
            if (!string.IsNullOrEmpty(overrideValue))
            {
                displayName = CleanStatsDisplayName(overrideValue, "Current Character");
                return BuildStatsKey(displayName);
            }

            object hero = GetCurrentHero();
            displayName = CleanStatsDisplayName(DescribeObject(hero), "Current Character");
            if (string.Equals(displayName, "null", StringComparison.OrdinalIgnoreCase)
                || string.Equals(displayName, "Hero", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Current Character";
            }

            string stableId = TryGetStableStatsId(hero);
            if (!string.IsNullOrEmpty(stableId))
            {
                return BuildStatsKey(displayName + "_" + stableId);
            }

            return BuildStatsKey(displayName);
        }

        private string ResolveStatisticsSourcePool(object proficiency, object item, object damage, object target)
        {
            if (IsNonCorporealSoundTarget(target, damage))
            {
                return NonCorporealSoundPool;
            }

            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                return ResolveOneHandedSpecificSoundPool(item);
            }
            if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                return ResolveTwoHandedSpecificSoundPool(item);
            }
            if (ReferenceEquals(proficiency, _unarmedProf))
            {
                return UnarmedSoundPool;
            }
            if (ReferenceEquals(proficiency, _archeryProf))
            {
                return ResolveArcherySpecificSoundPool(item);
            }
            if (ReferenceEquals(proficiency, _shieldProf))
            {
                return ShieldBashSoundPool;
            }
            if (ReferenceEquals(proficiency, _magicProf))
            {
                return ResolveMagicSpecificSoundPool(item, damage);
            }

            return GlobalSoundPool;
        }

        private string ResolveEnemyStatsDisplayName(object npc)
        {
            string npcName = CleanStatsDisplayName(DescribeObject(npc), "Unknown Enemy");
            object template = GetOptionalPropertyValue(npc, "Template");
            string templateName = CleanStatsDisplayName(DescribeObject(template), npcName);

            if (string.Equals(npcName, "NpcElement", StringComparison.OrdinalIgnoreCase)
                || string.Equals(npcName, "null", StringComparison.OrdinalIgnoreCase)
                || string.Equals(npcName, "Unknown Enemy", StringComparison.OrdinalIgnoreCase))
            {
                return templateName;
            }

            return npcName;
        }

        private string ResolveEntityStatsKey(object value, string displayName)
        {
            object template = GetOptionalPropertyValue(value, "Template");
            string stableId = TryGetStableStatsId(template);
            if (string.IsNullOrEmpty(stableId))
            {
                stableId = TryGetStableStatsId(value);
            }

            if (!string.IsNullOrEmpty(stableId))
            {
                return BuildStatsKey(displayName + "_" + stableId);
            }

            return BuildStatsKey(displayName);
        }

        private string TryGetStableStatsId(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string[] propertyNames =
            {
                "ID",
                "Id",
                "Guid",
                "GUID",
                "UniqueID",
                "UniqueId",
                "ModelID",
                "ModelId",
                "TemplateGuid",
                "TemplateID",
                "TemplateId"
            };

            for (int i = 0; i < propertyNames.Length; i++)
            {
                object rawValue = GetOptionalPropertyValue(value, propertyNames[i]);
                string text = rawValue == null ? string.Empty : rawValue.ToString();
                text = CleanStatsDisplayName(text, string.Empty);
                if (!string.IsNullOrEmpty(text) && !string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private string GetEnemyStatsMode()
        {
            string mode = _enemyStatsMode == null ? "" : (_enemyStatsMode.Value ?? "").Trim();
            if (string.Equals(mode, EnemyStatsModeAll, StringComparison.OrdinalIgnoreCase))
            {
                return EnemyStatsModeAll;
            }
            if (string.Equals(mode, EnemyStatsModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return EnemyStatsModeOff;
            }
            if (string.Equals(mode, EnemyStatsModePromoted, StringComparison.OrdinalIgnoreCase))
            {
                return EnemyStatsModePromoted;
            }

            LogDiagnostic("Unknown EnemyStatsMode '" + mode + "'; using " + EnemyStatsModePromoted + ".");
            return EnemyStatsModePromoted;
        }

        private string GetStatisticsFilePath()
        {
            return Path.Combine(Paths.ConfigPath, StatisticsFileName);
        }

        private string MakeStatisticsKey(string characterKey, string category)
        {
            return "c|" + BuildStatsKey(characterKey) + "|" + BuildStatsKey(category);
        }

        private string MakeStatisticsKey(string characterKey, string category, string itemKey)
        {
            return "c|" + BuildStatsKey(characterKey) + "|" + BuildStatsKey(category) + "|" + BuildStatsKey(itemKey);
        }

        private string MakeStatisticsKey(string characterKey, string category, string itemKey, string leafKey)
        {
            return "c|" + BuildStatsKey(characterKey) + "|" + BuildStatsKey(category) + "|" + BuildStatsKey(itemKey) + "|" + BuildStatsKey(leafKey);
        }

        private string CleanStatsDisplayName(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            string text = value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("  ", " ");
            }

            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        private string BuildStatsKey(string value)
        {
            string text = CleanStatsDisplayName(value, "unknown").ToLowerInvariant();
            StringBuilder builder = new StringBuilder(text.Length);
            bool lastWasSeparator = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                    continue;
                }

                if ((c == '_' || c == '-') && !lastWasSeparator)
                {
                    builder.Append(c);
                    lastWasSeparator = true;
                    continue;
                }

                if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            string key = builder.ToString().Trim('_', '-');
            if (key.Length == 0)
            {
                return "unknown";
            }
            if (key.Length > 120)
            {
                return key.Substring(0, 120).Trim('_', '-');
            }

            return key;
        }

        private string EncodeStatsText(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\t", "\\t")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private List<string> SortedKeys<T>(Dictionary<string, T> map)
        {
            List<string> keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            return keys;
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics.Value)
            {
                Log.LogInfo(message);
            }
        }

        private sealed class CharacterStatistics
        {
            public readonly string Key;
            public readonly CountStatistics Totals = new CountStatistics();
            public readonly Dictionary<string, CountStatistics> Proficiencies = new Dictionary<string, CountStatistics>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, CountStatistics> SourcePools = new Dictionary<string, CountStatistics>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, NamedCountStatistics> KillSources = new Dictionary<string, NamedCountStatistics>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, NamedCountStatistics> Enemies = new Dictionary<string, NamedCountStatistics>(StringComparer.OrdinalIgnoreCase);
            public string DisplayName;

            public CharacterStatistics(string key, string displayName)
            {
                Key = key;
                DisplayName = displayName;
            }
        }

        private class CountStatistics
        {
            public int Kills;
            public float BonusXp;
            public int DamageOverTimeKills;
            public float LargestBonusXp;

            public void Add(float bonus, bool damageOverTimeKill)
            {
                Kills++;
                BonusXp += bonus;
                if (damageOverTimeKill)
                {
                    DamageOverTimeKills++;
                }
                if (bonus > LargestBonusXp)
                {
                    LargestBonusXp = bonus;
                }
            }
        }

        private sealed class NamedCountStatistics : CountStatistics
        {
            public string DisplayName;

            public NamedCountStatistics(string displayName)
            {
                DisplayName = displayName;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static class NpcDeathPatch
        {
            public static void Postfix(object __instance, object damageOutcome)
            {
                if (Instance != null)
                {
                    Instance.OnNpcDeath(__instance, damageOutcome);
                }
            }
        }

        private static class GameplayMemoryBeforeSerializePatch
        {
            public static void Postfix()
            {
                if (Instance != null)
                {
                    Instance.OnGameplayMemoryBeforeSerialize();
                }
            }
        }

        private static class HealthElementBeforeHealthDecreasePatch
        {
            public static void Postfix(object __instance, object damage)
            {
                if (Instance != null)
                {
                    Instance.OnHealthDamageApplied(__instance, damage);
                }
            }
        }
    }
}
