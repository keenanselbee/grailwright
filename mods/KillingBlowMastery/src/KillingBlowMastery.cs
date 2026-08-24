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
[assembly: AssemblyVersion("1.9.3.0")]
[assembly: AssemblyFileVersion("1.9.3.0")]
[assembly: AssemblyInformationalVersion("1.9.3")]

namespace KillingBlowMastery
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(GrailFloatingTextPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(VersatileWeaponsPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class KillingBlowMasteryPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.killing-blow-mastery";
        public const string PluginName = "Killing Blow Mastery";
        public const string PluginVersion = "1.9.3";

        private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";
        private const string VersatileWeaponsPluginGuid = "ks.tgfoa.versatile-weapons";
        private const string VersatileWeaponsApiTypeName = "VersatileWeapons.VersatileWeaponsApi";
        private const string GrailFloatingTextKillingBlowEventId = "killing-blow";
        private const string GrailFloatingTextShortDurationBucket = "Short";
        private const string NpcElementTypeName = "Awaken.TG.Main.Fights.NPCs.NpcElement";
        private const string HealthElementTypeName = "Awaken.TG.Main.Character.HealthElement";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";
        private const string ItemTypeName = "Awaken.TG.Main.Heroes.Items.Item";
        private const string ProfUtilsTypeName = "Awaken.TG.Main.Heroes.Stats.Utils.ProfUtils";
        private const string ProfStatTypeName = "Awaken.TG.Main.General.StatTypes.ProfStatType";
        private const string FinisherHandlingElementTypeName = "Awaken.TG.Main.Heroes.FinisherHandlingElement";
        private const string FinisherExecutionActionTypeName = "Awaken.TG.Main.Heroes.FinisherExecutionAction";
        private const string FinisherStateTypeName = "Awaken.TG.Main.Animations.FSM.Heroes.States.Overrides.FinisherState";
        private const string FinishersListTypeName = "Awaken.TG.Main.Fights.Finishers.FinishersList";
        private const string FinisherDataTypeName = "Awaken.TG.Main.Fights.Finishers.FinisherData";
        private const string CustomDeathAnimationTypeName = "Awaken.TG.Main.AI.Combat.CustomDeath.CustomDeathAnimation";
        private const string WithFactionUtilsTypeName = "Awaken.TG.Main.Fights.Factions.WithFactionUtils";
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
        private const string CombatExecutionModeVanilla = "Vanilla";
        private const string CombatExecutionModeExecution = "Execution";
        private const string CombatExecutionModeOff = "Off";
        private const string DefaultExpandedExecutionExcludedAbstracts =
            "Animal;Animal_Prey";
        private const string KnownExecutionTargetAbstracts =
            "Animal, Animal_Prey, Bandit, BigHumanoid, Bloody, BoneMask, Boss, " +
            "ChallengeModeSpawn, Cultist, DalRiataBody, Female, Foredweller, " +
            "Ghost, Giant, Human, Humanoid, Male, MiniBoss, Monster, " +
            "ReefboundBody, Scourge, Skeleton, Summon, Tainted, WyrdnessBound, Zombie";
        private const float ExecutionDiagnosticRepeatSeconds = 3.0f;
        private const float ExecutionLifecycleDiagnosticRepeatSeconds = 3.0f;
        private const int DefaultRewardSoundSlots = 5;
        private const string AudioSourceObjectName = "Killing Blow Mastery Audio";
        private const string DefaultNotificationTextFormat = "Killing blow: +{xp} {skill}";
        private const int ConfigSchemaVersion = 19;
        private const int ConfigRecoveryBaselineSchema = 13;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        internal static KillingBlowMasteryPlugin Instance;
        internal static ManualLogSource Log;
        [ThreadStatic]
        private static ExecutionEvaluationState _activeExecutionEvaluation;
        [ThreadStatic]
        private static ExecutionFinisherStartState _activeExecutionFinisherStart;

        private Harmony _harmony;
        private Type _npcElementType;
        private Type _heroType;
        private Type _itemType;
        private Type _profStatType;
        private MethodInfo _heroCurrentGetter;
        private MethodInfo _profFromAbstractsMethod;
        private MethodInfo _isHostileToHeroMethod;
        private MethodInfo _tryFindFinisherMethod;
        private object _automaticFinisherTrigger;
        private MethodInfo _versatileWeaponsGetEffectiveProficiencyMethod;
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
        private ConfigEntry<bool> _automaticCombatFinishersEnabled;
        private ConfigEntry<string> _combatExecutionMode;
        private ConfigEntry<int> _executionMinimumProficiency;
        private ConfigEntry<float> _executionHealthPercentAtUnlock;
        private ConfigEntry<float> _executionHealthPercentAtMastery;
        private ConfigEntry<bool> _expandedExecutionTargets;
        private ConfigEntry<string> _expandedExecutionExcludedAbstracts;
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
        private ConfigEntry<bool> _diagnostics;
        private readonly Dictionary<string, int> _configSettingOrders =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Dictionary<string, List<RewardSoundClip>> _rewardSoundClipsByPool = new Dictionary<string, List<RewardSoundClip>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<object, KillSourceMemory> _recentKillSourcesByKey = new Dictionary<object, KillSourceMemory>(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, FMOD.Sound> _fmodSoundsByPath = new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<string>> _recentRewardSoundPathsByPool = new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Random _random = new System.Random();
        private AudioSource _rewardAudioSource;
        private bool _rewardSoundLoadStarted;
        private bool _grailFloatingTextBridgeResolved;
        private bool _grailFloatingTextSupportsSpecificWeaponIcons;
        private bool _grailFloatingTextUnavailableLogged;
        private bool _versatileWeaponsBridgeResolved;
        private bool _versatileWeaponsBridgeFailureLogged;
        private object _lastExecutionDiagnosticTarget;
        private string _lastExecutionDiagnosticStatus;
        private float _lastExecutionDiagnosticTime = -9999.0f;
        private ExecutionFinisherLifecycleState _activeExecutionFinisher;
        private float _lastRewardSoundTime = -9999.0f;
        private string _cachedBloodlessSoundBlacklistTermsRaw;
        private string[] _cachedBloodlessSoundBlacklistTerms = new string[0];
        private string _cachedBloodlessSoundWhitelistTermsRaw;
        private string[] _cachedBloodlessSoundWhitelistTerms = new string[0];
        private string _cachedNonCorporealSoundTermsRaw;
        private string[] _cachedNonCorporealSoundTerms = new string[0];
        private string _cachedNonCorporealSoundExclusionTermsRaw;
        private string[] _cachedNonCorporealSoundExclusionTerms = new string[0];
        private string _cachedExpandedExecutionExcludedAbstractsRaw;
        private string[] _cachedExpandedExecutionExcludedAbstracts = new string[0];
        private readonly Dictionary<string, float> _pendingPreservedCalibrationFloats =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pendingPreservedManualOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _pendingPreservedBoolOverrides =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _pendingPreservedIntOverrides =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                BindConfig();
                CacheGameAccessors();
                if (!PatchGame())
                {
                    enabled = false;
                    return;
                }

                EnsureRewardSoundLoadStarted();

                Log.LogInfo(PluginName + " " + PluginVersion + " loaded. BonusPercentOfEnemyXP=" +
                    _bonusPercentOfEnemyXp.Value.ToString("0.###", CultureInfo.InvariantCulture) +
                    "; MaxBonusXP=" + _maximumBonusXp.Value.ToString("0.###", CultureInfo.InvariantCulture) + ".");
            }
            catch (Exception ex)
            {
                Log.LogError(PluginName + " " + PluginVersion + " failed during startup: " + ex.GetBaseException().Message);
                Log.LogError(ex.ToString());
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, ex);
                enabled = false;
            }
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

        private void Update()
        {
            ReportActiveExecutionFinisherLifecycle();
        }

        private ConfigEntry<T> BindOrdered<T>(
            string section,
            string key,
            T defaultValue,
            string description)
        {
            return BindOrdered(
                section,
                key,
                defaultValue,
                new ConfigDescription(description));
        }

        private ConfigEntry<T> BindOrdered<T>(
            string section,
            string key,
            T defaultValue,
            ConfigDescription description)
        {
            if (String.Equals(
                    key,
                    "ConfigSchemaVersion",
                    StringComparison.Ordinal))
            {
                return base.Config.Bind(section, key, defaultValue, description);
            }

            string displaySection = GetConfigDisplaySection(section, key);
            int order;
            if (!_configSettingOrders.TryGetValue(displaySection, out order))
            {
                order = 0;
            }
            _configSettingOrders[displaySection] = order + 10;

            return base.Config.Bind(
                section,
                key,
                defaultValue,
                Grailwright.Shared.ConfigUiDescription.Create(
                    description.Description,
                    displaySection,
                    GetConfigDisplayName(key),
                    GetConfigSectionOrder(displaySection),
                    order,
                    description.AcceptableValues));
        }

        private static string GetConfigDisplaySection(
            string storageSection,
            string key)
        {
            switch (key)
            {
                case "AutomaticCombatFinishersEnabled":
                case "CombatExecutionMode":
                case "ExecutionMinimumProficiency":
                case "ExecutionHealthPercentAtUnlock":
                case "ExecutionHealthPercentAtMastery":
                case "ExpandedExecutionTargets":
                case "ExpandedExecutionExcludedAbstracts":
                    return "Combat Finishers";
                case "FinisherSoundMode":
                case "FinisherSoundRangeVolume":
                    return "Reward Audio";
                case "UseKillingBlowFallbackForClassifiedKills":
                case "UseNonCorporealEnemySounds":
                case "NonCorporealSoundTerms":
                case "NonCorporealSoundExclusionTerms":
                case "UseBloodlessSoundVariants":
                case "BloodlessSoundBlacklistTerms":
                case "BloodlessSoundWhitelistTerms":
                    return "Advanced Audio Routing";
            }

            return string.Equals(
                    storageSection,
                    "Audio",
                    StringComparison.Ordinal)
                ? "Reward Audio"
                : storageSection;
        }

        private static string GetConfigDisplayName(string key)
        {
            switch (key)
            {
                case "AutomaticCombatFinishersEnabled":
                    return "Automatic Kill-Cam Animations";
                case "CombatExecutionMode":
                    return "Combat Execution Mode";
                case "ExecutionMinimumProficiency":
                    return "Execution Unlock Proficiency";
                case "ExecutionHealthPercentAtUnlock":
                    return "Health Threshold at Unlock (%)";
                case "ExecutionHealthPercentAtMastery":
                    return "Health Threshold at Mastery (%)";
                case "ExpandedExecutionTargets":
                    return "Expand Target Types";
                case "ExpandedExecutionExcludedAbstracts":
                    return "Excluded Target Families";
                case "FinisherSoundMode":
                    return "Sound Style";
                case "FinisherSoundRangeVolume":
                    return "Sound Distance Fade";
                case "BonusPercentOfEnemyXP":
                    return "Bonus XP (% of Enemy XP)";
                case "RandomPitchSemitones":
                    return "Pitch Variation (Semitones)";
                default:
                    return HumanizeConfigKey(key);
            }
        }

        private static int GetConfigSectionOrder(string section)
        {
            switch (section)
            {
                case "General":
                    return 0;
                case "Combat Finishers":
                    return 10;
                case "Weapon Skills":
                    return 20;
                case "Notifications":
                    return 30;
                case "Reward Audio":
                    return 40;
                case "Advanced Audio Routing":
                    return 50;
                case "Advanced":
                    return 60;
                case "Diagnostics":
                    return Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder;
                default:
                    throw new InvalidOperationException(
                        "Missing config section order for " + section + ".");
            }
        }

        private static string HumanizeConfigKey(string key)
        {
            StringBuilder builder = new StringBuilder(key.Length + 8);
            for (int index = 0; index < key.Length; index++)
            {
                char current = key[index];
                if (index > 0
                    && Char.IsUpper(current)
                    && (!Char.IsUpper(key[index - 1])
                        || (index + 1 < key.Length
                            && Char.IsLower(key[index + 1]))))
                {
                    builder.Append(' ');
                }
                builder.Append(current);
            }
            return builder.ToString();
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();
            _configSettingOrders.Clear();

            _enabled = BindOrdered("General", "Enabled", true, "Master switch.");
            BindOrdered(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. It changes only when an update requires fresh defaults.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _automaticCombatFinishersEnabled = BindOrdered(
                "General",
                "AutomaticCombatFinishersEnabled",
                true,
                "Allow the game's automatic kill-cam animations after normal melee killing blows. This is independent of Combat Execution Mode. Disable both controls to remove combat finishers without affecting story executions.");
            _combatExecutionMode = BindOrdered(
                "General",
                "CombatExecutionMode",
                CombatExecutionModeExecution,
                new ConfigDescription(
                    "Controls the interactive Execute prompt: Vanilla keeps the game's rules, Execution offers Execute against eligible hostile combatants at low health, and Off removes only the interactive combat prompt. Automatic kill-cam animations are controlled separately.",
                    new AcceptableValueList<string>(
                        CombatExecutionModeExecution,
                        CombatExecutionModeVanilla,
                        CombatExecutionModeOff)));
            _executionMinimumProficiency = BindOrdered(
                "General",
                "ExecutionMinimumProficiency",
                25,
                new ConfigDescription(
                    "Minimum proficiency required with the weapon selected for an Execution. Below this level, the Execute prompt is unavailable.",
                    new AcceptableValueRange<int>(0, 100)));
            _executionHealthPercentAtUnlock = BindOrdered(
                "General",
                "ExecutionHealthPercentAtUnlock",
                10.0f,
                new ConfigDescription(
                    "Target-health threshold when the selected weapon proficiency first unlocks Executions.",
                    new AcceptableValueRange<float>(1.0f, 30.0f)));
            _executionHealthPercentAtMastery = BindOrdered(
                "General",
                "ExecutionHealthPercentAtMastery",
                25.0f,
                new ConfigDescription(
                    "Target-health threshold at 100 proficiency. Values below the unlock threshold are clamped up so higher proficiency never reduces the Execution range.",
                    new AcceptableValueRange<float>(1.0f, 30.0f)));
            _expandedExecutionTargets = BindOrdered(
                "General",
                "ExpandedExecutionTargets",
                true,
                "Used only when Combat Execution Mode is Execution. Try humanoid finisher animations on additional hostile enemy templates after applying Excluded Target Families. Non-humanoid rigs may misalign if their family is allowed.");
            _expandedExecutionExcludedAbstracts = BindOrdered(
                "General",
                "ExpandedExecutionExcludedAbstracts",
                DefaultExpandedExecutionExcludedAbstracts,
                "Used only when Expand Target Types is enabled. Rejects a target when any inherited abstract family matches this list; matching is exact and case-insensitive. Default: Animal;Animal_Prey. Separate names with semicolons, commas, pipes, or new lines. Remove a name to allow that family, or leave the list empty to exclude none. Known families (26): "
                    + KnownExecutionTargetAbstracts
                    + ".");
            _finisherSoundMode = BindOrdered(
                "General",
                "FinisherSoundMode",
                FinisherSoundModeWeaponSpecific,
                new ConfigDescription(
                    "Reward sound style: WeaponSpecific uses contextual pools, Soulslike uses the shared dramatic pool, GoatTest is a novelty/testing sound, and Off disables reward sounds.",
                    new AcceptableValueList<string>(
                        FinisherSoundModeWeaponSpecific,
                        FinisherSoundModeSoulslike,
                        FinisherSoundModeGoatTest,
                        FinisherSoundModeOff)));
            _finisherSoundRangeVolume = BindOrdered(
                "General",
                "FinisherSoundRangeVolume",
                1.0f,
                new ConfigDescription(
                    "How strongly reward sounds fade with target distance. 0 disables distance fade; 1 uses the full 0m=100%, 30m+=10% curve. This does not change the base volume.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));
            _bonusPercentOfEnemyXp = BindOrdered(
                "General",
                "BonusPercentOfEnemyXP",
                4.0f,
                new ConfigDescription(
                    "Extra combat proficiency XP awarded on a killing blow, as a percent of the enemy's XP reward.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));
            _maximumBonusXp = BindOrdered(
                "General",
                "MaximumBonusXP",
                100.0f,
                "Maximum extra proficiency XP from one killing blow. Zero or less disables the cap.");

            _allowOneHanded = BindOrdered("Weapon Skills", "AllowOneHanded", true, "Award One-Handed proficiency from one-handed weapon killing blows.");
            _allowTwoHanded = BindOrdered("Weapon Skills", "AllowTwoHanded", true, "Award Two-Handed proficiency from two-handed weapon killing blows.");
            _allowUnarmed = BindOrdered("Weapon Skills", "AllowUnarmed", true, "Award Unarmed proficiency from fist killing blows.");
            _allowArchery = BindOrdered("Weapon Skills", "AllowArchery", true, "Award Archery proficiency from bow killing blows.");
            _allowShield = BindOrdered("Weapon Skills", "AllowShield", true, "Award Shield proficiency from shield killing blows.");
            _allowMagic = BindOrdered("Weapon Skills", "AllowMagic", true, "Award Magic proficiency from spell, rod, or magic-item killing blows.");

            _minimumBonusXp = BindOrdered(
                "Advanced",
                "MinimumBonusXP",
                1.0f,
                "Minimum bonus paid when the computed bonus is greater than zero.");
            _roundBonusXpTo = BindOrdered(
                "Advanced",
                "RoundBonusXPTo",
                1.0f,
                "Round bonus proficiency XP to this increment. One rounds to whole XP; zero disables rounding.");
            _fallbackBonusXp = BindOrdered(
                "Advanced",
                "FallbackBonusXP",
                0.0f,
                "Bonus proficiency XP to use when enemy XP cannot be resolved. Zero skips unresolved enemies.");
            _requirePrimaryDamage = BindOrdered("Advanced", "RequirePrimaryDamage", true, "Only award bonuses for primary damage events, matching the game's normal weapon-proficiency rules.");
            _allowDamageOverTimeKills = BindOrdered("Advanced", "AllowDamageOverTimeKills", true, "Allow bleed, burn, poison, and other delayed damage to count when it can be traced to a recent supported hero damage source.");
            _damageOverTimeMemorySeconds = BindOrdered(
                "Advanced",
                "DamageOverTimeMemorySeconds",
                12.0f,
                "How long a recent supported hero damage source can credit a later damage-over-time death.");
            _ignoreThrowable = BindOrdered("Advanced", "IgnoreThrowable", true, "Do not award killing-blow proficiency from thrown items.");
            _requireXpRewardAllowedWhenPresent = BindOrdered("Advanced", "RequireXPRewardAllowedWhenPresent", true, "If the killed target exposes XpRewardAllowed, require it to be true.");
            _notificationsEnabled = BindOrdered("Notifications", "NotificationsEnabled", true, "Show an in-game HUD notification when killing-blow proficiency XP is awarded.");
            _notificationMinimumXp = BindOrdered("Notifications", "NotificationMinimumXP", 1.0f, "Minimum awarded bonus XP required before showing an in-game notification.");
            _notificationTextFormat = BindOrdered("Notifications", "NotificationTextFormat", DefaultNotificationTextFormat, "HUD notification text. Tokens: {xp}, {skill}, {enemy}, {weapon}, {enemyXP}.");
            _notificationMode = BindOrdered(
                "Notifications",
                "NotificationMode",
                "GrailFloatingText",
                "Where killing-blow reward notifications appear. Use GrailFloatingText, GameHud, Both, or Off.");
            _rewardSoundVolume = BindOrdered(
                "Audio",
                "RewardSoundVolume",
                0.65f,
                new ConfigDescription(
                    "Volume multiplier for the killing-blow reward sound.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));
            _rewardSoundCooldownSeconds = BindOrdered("Audio", "RewardSoundCooldownSeconds", 0.35f, "Minimum real-time seconds between reward sounds.");
            _useKillingBlowFallbackForClassifiedKills = BindOrdered("Audio", "UseKillingBlowFallbackForClassifiedKills", false, "Allow classified weapon, shield, and magic kills to fall back to the killing_blow pool when their category pool is missing.");
            _useNonCorporealEnemySounds = BindOrdered("Audio", "UseNonCorporealEnemySounds", true, "Use the target-only non_corporeal sound pool for matched spirit/Wyrd enemies. This overrides weapon, magic, and _dry routing for those targets.");
            _nonCorporealSoundTerms = BindOrdered("Audio", "NonCorporealSoundTerms", DefaultNonCorporealSoundTerms, "Semicolon, comma, pipe, or newline separated target terms that force the non_corporeal sound pool.");
            _nonCorporealSoundExclusionTerms = BindOrdered("Audio", "NonCorporealSoundExclusionTerms", DefaultNonCorporealSoundExclusionTerms, "Optional target terms that prevent non_corporeal routing when both inclusion and exclusion terms match.");
            _useBloodlessSoundVariants = BindOrdered("Audio", "UseBloodlessSoundVariants", true, "Use *_dry.wav sound variants for targets whose names, templates, or type text match the bloodless sound terms.");
            _bloodlessSoundBlacklistTerms = BindOrdered("Audio", "BloodlessSoundBlacklistTerms", DefaultBloodlessSoundBlacklistTerms, "Semicolon, comma, pipe, or newline separated terms that make a killed target use bloodless sound variants when available.");
            _bloodlessSoundWhitelistTerms = BindOrdered("Audio", "BloodlessSoundWhitelistTerms", "", "Optional terms that force normal sounds even if a bloodless sound term also matches.");
            _avoidRecentSoundRepeats = BindOrdered("Audio", "AvoidRecentSoundRepeats", true, "Avoid replaying reward sounds that were recently used in the same sound pool.");
            _recentSoundMemory = BindOrdered(
                "Audio",
                "RecentSoundMemory",
                2,
                new ConfigDescription(
                    "How many recent sounds to avoid repeating per sound pool. Falls back gracefully when too few sounds are available.",
                    new AcceptableValueRange<int>(0, 4)));
            _randomPitchSemitones = BindOrdered(
                "Audio",
                "RandomPitchSemitones",
                0.35f,
                new ConfigDescription(
                    "Random reward-sound pitch variation in semitones. Zero disables pitch randomization.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));
            _diagnostics = BindOrdered("Diagnostics", "Diagnostics", false, "Log kill source, rewards, audio routing, and throttled per-target Execution eligibility decisions.");
            RestorePreservedConfigValues();
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

            CapturePreservedConfigValues(
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
                Log.LogInfo(
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
            catch (Exception ex)
            {
                ClearPendingPreservedConfigValues();

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

        private void CapturePreservedConfigValues(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedConfigValues();
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

                if (IsPreservedCalibrationFloat(settingId))
                {
                    float parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedCalibrationFloats[settingId] = parsedValue;
                    }
                }
                else if (IsPreservedIntOverride(settingId))
                {
                    int preservedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out preservedValue))
                    {
                        _pendingPreservedIntOverrides[settingId] =
                            preservedValue;
                    }
                }
                else if (IsPreservedBoolOverride(settingId))
                {
                    bool preservedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out preservedValue))
                    {
                        _pendingPreservedBoolOverrides[settingId] =
                            preservedValue;
                    }
                }
                else if (IsPreservedManualOverride(settingId))
                {
                    string preservedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out preservedValue))
                    {
                        _pendingPreservedManualOverrides[settingId] =
                            preservedValue;
                    }
                }
            }
        }

        private static bool IsPreservedCalibrationFloat(string settingId)
        {
            return string.Equals(settingId, "General\nExecutionHealthPercentAtUnlock", StringComparison.Ordinal)
                || string.Equals(settingId, "General\nExecutionHealthPercentAtMastery", StringComparison.Ordinal)
                || string.Equals(settingId, "General\nFinisherSoundRangeVolume", StringComparison.Ordinal)
                || string.Equals(settingId, "Audio\nRewardSoundVolume", StringComparison.Ordinal)
                || string.Equals(settingId, "Audio\nRandomPitchSemitones", StringComparison.Ordinal);
        }

        private static bool IsPreservedIntOverride(string settingId)
        {
            return string.Equals(
                settingId,
                "General\nExecutionMinimumProficiency",
                StringComparison.Ordinal);
        }

        private static bool IsPreservedBoolOverride(string settingId)
        {
            return string.Equals(settingId, "General\nAutomaticCombatFinishersEnabled", StringComparison.Ordinal)
                || string.Equals(settingId, "General\nExpandedExecutionTargets", StringComparison.Ordinal);
        }

        private static bool IsPreservedManualOverride(string settingId)
        {
            return string.Equals(settingId, "General\nCombatExecutionMode", StringComparison.Ordinal)
                || string.Equals(settingId, "General\nExpandedExecutionExcludedAbstracts", StringComparison.Ordinal)
                || string.Equals(settingId, "Notifications\nNotificationTextFormat", StringComparison.Ordinal)
                || string.Equals(settingId, "Audio\nBloodlessSoundWhitelistTerms", StringComparison.Ordinal);
        }

        private void RestorePreservedConfigValues()
        {
            if (_pendingPreservedCalibrationFloats.Count == 0
                && _pendingPreservedIntOverrides.Count == 0
                && _pendingPreservedBoolOverrides.Count == 0
                && _pendingPreservedManualOverrides.Count == 0
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreservedInt("General\nExecutionMinimumProficiency", _executionMinimumProficiency, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("General\nExecutionHealthPercentAtUnlock", _executionHealthPercentAtUnlock, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("General\nExecutionHealthPercentAtMastery", _executionHealthPercentAtMastery, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("General\nFinisherSoundRangeVolume", _finisherSoundRangeVolume, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Audio\nRewardSoundVolume", _rewardSoundVolume, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("Audio\nRandomPitchSemitones", _randomPitchSemitones, ref restoredCount, ref clampedCount);
            RestorePreservedBool("General\nAutomaticCombatFinishersEnabled", _automaticCombatFinishersEnabled, ref restoredCount);
            RestorePreservedBool("General\nExpandedExecutionTargets", _expandedExecutionTargets, ref restoredCount);
            RestorePreservedString("General\nCombatExecutionMode", _combatExecutionMode, ref restoredCount);
            RestorePreservedString("General\nExpandedExecutionExcludedAbstracts", _expandedExecutionExcludedAbstracts, ref restoredCount);
            RestorePreservedString("Notifications\nNotificationTextFormat", _notificationTextFormat, ref restoredCount);
            RestorePreservedString("Audio\nBloodlessSoundWhitelistTerms", _bloodlessSoundWhitelistTerms, ref restoredCount);

            Log.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " calibration/manual override value(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + _pendingPreservedInvalidValueCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            ClearPendingPreservedConfigValues();
        }

        private void RestorePreservedFloat(
            string settingId,
            ConfigEntry<float> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            float preservedValue;
            if (entry == null
                || !_pendingPreservedCalibrationFloats.TryGetValue(settingId, out preservedValue))
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

        private void RestorePreservedString(
            string settingId,
            ConfigEntry<string> entry,
            ref int restoredCount)
        {
            string preservedValue;
            if (entry == null
                || !_pendingPreservedManualOverrides.TryGetValue(settingId, out preservedValue))
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

        private void RestorePreservedInt(
            string settingId,
            ConfigEntry<int> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            int preservedValue;
            if (entry == null
                || !_pendingPreservedIntOverrides.TryGetValue(
                    settingId,
                    out preservedValue))
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
                || !_pendingPreservedBoolOverrides.TryGetValue(
                    settingId,
                    out preservedValue))
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

        private void ClearPendingPreservedConfigValues()
        {
            _pendingPreservedCalibrationFloats.Clear();
            _pendingPreservedIntOverrides.Clear();
            _pendingPreservedBoolOverrides.Clear();
            _pendingPreservedManualOverrides.Clear();
            _pendingPreservedInvalidValueCount = 0;
        }

        private void CacheGameAccessors()
        {
            _npcElementType = AccessTools.TypeByName(NpcElementTypeName);
            _heroType = AccessTools.TypeByName(HeroTypeName);
            _itemType = AccessTools.TypeByName(ItemTypeName);
            _profStatType = AccessTools.TypeByName(ProfStatTypeName);
            if (_heroType != null)
            {
                _heroCurrentGetter = AccessTools.PropertyGetter(_heroType, "Current");
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

            Type withFactionUtilsType = AccessTools.TypeByName(WithFactionUtilsTypeName);
            if (withFactionUtilsType != null && _npcElementType != null)
            {
                MethodInfo[] factionMethods = withFactionUtilsType.GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < factionMethods.Length; i++)
                {
                    ParameterInfo[] parameters = factionMethods[i].GetParameters();
                    if (factionMethods[i].Name == "IsHostileToHero"
                        && factionMethods[i].ReturnType == typeof(bool)
                        && parameters.Length == 1
                        && parameters[0].ParameterType.IsAssignableFrom(_npcElementType))
                    {
                        _isHostileToHeroMethod = factionMethods[i];
                        break;
                    }
                }
            }

            CacheNotificationAccessors();
        }

        private bool PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type npcElementType = _npcElementType;
            if (npcElementType == null)
            {
                Log.LogError("Could not find " + NpcElementTypeName + ". " + PluginName + " is inactive.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Mod inactive; check BepInEx log.");
                return false;
            }

            MethodInfo original = AccessTools.Method(npcElementType, "DeathNonCriticalFunctions");
            MethodInfo postfix = AccessTools.Method(
                typeof(NpcDeathPatch),
                nameof(NpcDeathPatch.Postfix));
            if (original == null || postfix == null)
            {
                Log.LogError("Could not patch NPC death handling. " + PluginName + " is inactive.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Mod inactive; check BepInEx log.");
                return false;
            }

            _harmony.Patch(original, null, new HarmonyMethod(postfix));
            if (_diagnostics.Value)
            {
                Log.LogInfo("Patched " + npcElementType.FullName + ".DeathNonCriticalFunctions.");
            }

            PatchCombatFinisherControls();

            Type healthElementType = AccessTools.TypeByName(HealthElementTypeName);
            MethodInfo damageOriginal = healthElementType == null ? null : AccessTools.Method(healthElementType, "BeforeHealthDecreaseEvents");
            MethodInfo damagePostfix = AccessTools.Method(
                typeof(HealthElementBeforeHealthDecreasePatch),
                nameof(HealthElementBeforeHealthDecreasePatch.Postfix));
            if (damageOriginal == null || damagePostfix == null)
            {
                LogDiagnostic("Could not patch HealthElement.BeforeHealthDecreaseEvents; damage-over-time kill source memory is unavailable.");
                return true;
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

            return true;
        }

        private void PatchCombatFinisherControls()
        {
            Type finisherHandlingType = AccessTools.TypeByName(FinisherHandlingElementTypeName);
            CacheAutomaticFinisherFallbackAccessor(finisherHandlingType);
            MethodInfo automaticOriginal = finisherHandlingType == null
                ? null
                : AccessTools.Method(finisherHandlingType, "TryTriggerFinisherBeforeAttack");
            MethodInfo automaticPrefix = AccessTools.Method(
                typeof(AutomaticCombatFinisherPatch),
                nameof(AutomaticCombatFinisherPatch.Prefix));
            if (automaticOriginal == null || automaticPrefix == null)
            {
                Log.LogWarning("Could not patch automatic combat finishers; AutomaticCombatFinishersEnabled is unavailable.");
            }
            else
            {
                try
                {
                    _harmony.Patch(automaticOriginal, new HarmonyMethod(automaticPrefix));
                    LogDiagnostic("Patched " + FinisherHandlingElementTypeName + ".TryTriggerFinisherBeforeAttack.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to patch automatic combat finishers: " + ex.GetBaseException().Message);
                }
            }

            Type executionActionType = AccessTools.TypeByName(FinisherExecutionActionTypeName);
            MethodInfo availabilityOriginal = executionActionType == null
                ? null
                : AccessTools.Method(executionActionType, "CanBeTriggered");
            MethodInfo availabilityPrefix = AccessTools.Method(
                typeof(CombatExecutionAvailabilityPatch),
                nameof(CombatExecutionAvailabilityPatch.Prefix));
            MethodInfo availabilityPostfix = AccessTools.Method(
                typeof(CombatExecutionAvailabilityPatch),
                nameof(CombatExecutionAvailabilityPatch.Postfix));
            MethodInfo availabilityFinalizer = AccessTools.Method(
                typeof(CombatExecutionAvailabilityPatch),
                nameof(CombatExecutionAvailabilityPatch.Finalizer));
            if (availabilityOriginal == null
                || availabilityPrefix == null
                || availabilityPostfix == null
                || availabilityFinalizer == null)
            {
                Log.LogWarning("Could not patch combat interaction finishers; CombatExecutionMode is unavailable.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        availabilityOriginal,
                        prefix: new HarmonyMethod(availabilityPrefix),
                        postfix: new HarmonyMethod(availabilityPostfix),
                        finalizer: new HarmonyMethod(availabilityFinalizer));
                    LogDiagnostic("Patched " + FinisherExecutionActionTypeName + ".CanBeTriggered.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to patch combat interaction finisher availability: " + ex.GetBaseException().Message);
                }
            }

            MethodInfo actionNameGetter = executionActionType == null
                ? null
                : AccessTools.PropertyGetter(executionActionType, "DefaultActionName");
            MethodInfo actionNamePostfix = AccessTools.Method(
                typeof(CombatExecutionActionNamePatch),
                nameof(CombatExecutionActionNamePatch.Postfix));
            if (actionNameGetter == null || actionNamePostfix == null)
            {
                Log.LogWarning("Could not patch the Execution interaction label; the game default label will remain in use.");
            }
            else
            {
                try
                {
                    _harmony.Patch(actionNameGetter, null, new HarmonyMethod(actionNamePostfix));
                    LogDiagnostic("Patched " + FinisherExecutionActionTypeName + ".DefaultActionName.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to patch the Execution interaction label: " + ex.GetBaseException().Message);
                }
            }

            PatchExecutionFinisherStart(executionActionType);

            Type customDeathAnimationType = AccessTools.TypeByName(
                CustomDeathAnimationTypeName);
            MethodInfo conditionsOriginal = customDeathAnimationType == null
                ? null
                : AccessTools.Method(customDeathAnimationType, "CheckConditions");
            MethodInfo conditionsPrefix = AccessTools.Method(
                typeof(ExecutionAnimationConditionsPatch),
                nameof(ExecutionAnimationConditionsPatch.Prefix));
            if (conditionsOriginal == null || conditionsPrefix == null)
            {
                Log.LogWarning("Could not patch Execution animation conditions; Execution may retain the game's situational animation restrictions.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        conditionsOriginal,
                        prefix: new HarmonyMethod(conditionsPrefix));
                    LogDiagnostic("Patched " + CustomDeathAnimationTypeName + ".CheckConditions.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to patch Execution animation conditions: " + ex.GetBaseException().Message);
                }
            }

            Type finishersListType = AccessTools.TypeByName(FinishersListTypeName);
            MethodInfo globalConditionsOriginal = finishersListType == null
                ? null
                : AccessTools.Method(finishersListType, "CheckGlobalConditions");
            MethodInfo globalConditionsPrefix = AccessTools.Method(
                typeof(ExecutionGlobalConditionsPatch),
                nameof(ExecutionGlobalConditionsPatch.Prefix));
            if (globalConditionsOriginal == null || globalConditionsPrefix == null)
            {
                Log.LogWarning("Could not patch Execution list-level conditions; native global finisher filters may still block the prompt.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        globalConditionsOriginal,
                        prefix: new HarmonyMethod(globalConditionsPrefix));
                    LogDiagnostic("Patched " + FinishersListTypeName + ".CheckGlobalConditions.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to patch Execution list-level conditions: " + ex.GetBaseException().Message);
                }
            }

            MethodInfo defaultHpConditionOriginal = finishersListType == null
                ? null
                : AccessTools.Method(finishersListType, "CheckDefaultHpCondition");
            MethodInfo defaultHpConditionPrefix = AccessTools.Method(
                typeof(ExecutionDefaultHpConditionPatch),
                nameof(ExecutionDefaultHpConditionPatch.Prefix));
            if (defaultHpConditionOriginal == null || defaultHpConditionPrefix == null)
            {
                Log.LogWarning("Could not patch Execution default health conditions; native default-health rejection may still block the prompt.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        defaultHpConditionOriginal,
                        prefix: new HarmonyMethod(defaultHpConditionPrefix));
                    LogDiagnostic("Patched " + FinishersListTypeName + ".CheckDefaultHpCondition.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to patch Execution default health conditions: " + ex.GetBaseException().Message);
                }
            }

            Type finisherDataType = AccessTools.TypeByName(FinisherDataTypeName);
            MethodInfo finisherDataConditionsOriginal = finisherDataType == null
                ? null
                : AccessTools.Method(finisherDataType, "CheckConditions");
            MethodInfo finisherDataConditionsPostfix = AccessTools.Method(
                typeof(ExecutionFinisherDataConditionsPatch),
                nameof(ExecutionFinisherDataConditionsPatch.Postfix));
            if (finisherDataConditionsOriginal == null
                || finisherDataConditionsPostfix == null)
            {
                Log.LogWarning("Could not patch Execution candidate diagnostics; per-animation condition results will be unavailable.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        finisherDataConditionsOriginal,
                        postfix: new HarmonyMethod(finisherDataConditionsPostfix));
                    LogDiagnostic("Patched " + FinisherDataTypeName + ".CheckConditions diagnostics.");
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to patch Execution candidate diagnostics: " + ex.GetBaseException().Message);
                }
            }
        }

        private void PatchExecutionFinisherStart(Type executionActionType)
        {
            MethodInfo onStartOriginal = executionActionType == null
                ? null
                : AccessTools.Method(executionActionType, "OnStart");
            MethodInfo onStartPrefix = AccessTools.Method(
                typeof(ExecutionFinisherStartPatch),
                nameof(ExecutionFinisherStartPatch.Prefix));
            MethodInfo onStartPostfix = AccessTools.Method(
                typeof(ExecutionFinisherStartPatch),
                nameof(ExecutionFinisherStartPatch.Postfix));
            MethodInfo onStartFinalizer = AccessTools.Method(
                typeof(ExecutionFinisherStartPatch),
                nameof(ExecutionFinisherStartPatch.Finalizer));
            if (onStartOriginal == null
                || onStartPrefix == null
                || onStartPostfix == null
                || onStartFinalizer == null)
            {
                Log.LogWarning("Could not patch Execution finisher start; scoped slow-motion suppression and lifecycle diagnostics are unavailable.");
                return;
            }

            try
            {
                _harmony.Patch(
                    onStartOriginal,
                    prefix: new HarmonyMethod(onStartPrefix),
                    postfix: new HarmonyMethod(onStartPostfix),
                    finalizer: new HarmonyMethod(onStartFinalizer));
                LogDiagnostic("Patched " + FinisherExecutionActionTypeName + ".OnStart.");
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to patch Execution finisher start: " + ex.GetBaseException().Message);
                return;
            }

            Type finisherStateType = AccessTools.TypeByName(FinisherStateTypeName);
            MethodInfo finisherStartedOriginal = finisherStateType == null
                ? null
                : AccessTools.Method(finisherStateType, "OnFinisherStarted");
            MethodInfo finisherStartedPostfix = AccessTools.Method(
                typeof(ExecutionFinisherLifecyclePatch),
                nameof(ExecutionFinisherLifecyclePatch.FinisherStartedPostfix));
            MethodInfo finisherExitedOriginal = finisherStateType == null
                ? null
                : AccessTools.Method(finisherStateType, "OnExit");
            MethodInfo finisherExitedPostfix = AccessTools.Method(
                typeof(ExecutionFinisherLifecyclePatch),
                nameof(ExecutionFinisherLifecyclePatch.FinisherExitedPostfix));
            if (finisherStartedOriginal == null
                || finisherStartedPostfix == null
                || finisherExitedOriginal == null
                || finisherExitedPostfix == null)
            {
                Log.LogWarning("Could not patch Execution finisher lifecycle diagnostics; FinisherStarted/OnExit telemetry is unavailable.");
                return;
            }

            try
            {
                _harmony.Patch(
                    finisherStartedOriginal,
                    postfix: new HarmonyMethod(finisherStartedPostfix));
                _harmony.Patch(
                    finisherExitedOriginal,
                    postfix: new HarmonyMethod(finisherExitedPostfix));
                LogDiagnostic("Patched " + FinisherStateTypeName + " lifecycle diagnostics.");
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to patch Execution finisher lifecycle diagnostics: " + ex.GetBaseException().Message);
            }
        }

        private void CacheAutomaticFinisherFallbackAccessor(Type finisherHandlingType)
        {
            if (finisherHandlingType == null)
            {
                return;
            }

            MethodInfo[] methods = finisherHandlingType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (methods[i].Name != "TryFindFinisher"
                    || methods[i].ReturnType != typeof(bool)
                    || parameters.Length != 5
                    || !parameters[0].ParameterType.IsEnum)
                {
                    continue;
                }

                try
                {
                    _automaticFinisherTrigger = Enum.Parse(
                        parameters[0].ParameterType,
                        "AttackTriesToStart");
                    _tryFindFinisherMethod = methods[i];
                    LogDiagnostic(
                        "Resolved the normal combat-finisher fallback for Execution selection.");
                    return;
                }
                catch
                {
                    _tryFindFinisherMethod = null;
                    _automaticFinisherTrigger = null;
                }
            }

            Log.LogWarning(
                "Could not resolve the normal combat-finisher fallback; Execution will use only the game's interaction execution lists.");
        }

        private bool AutomaticCombatFinishersAllowed
        {
            get
            {
                return !_enabled.Value || _automaticCombatFinishersEnabled.Value;
            }
        }

        private string GetCombatExecutionMode()
        {
            if (!_enabled.Value)
            {
                return CombatExecutionModeVanilla;
            }

            string mode = _combatExecutionMode.Value == null
                ? string.Empty
                : _combatExecutionMode.Value.Trim();
            if (string.Equals(mode, CombatExecutionModeExecution, StringComparison.OrdinalIgnoreCase))
            {
                return CombatExecutionModeExecution;
            }
            if (string.Equals(mode, CombatExecutionModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return CombatExecutionModeOff;
            }
            return CombatExecutionModeExecution;
        }

        private ExecutionFinisherStartState BeginExecutionFinisherStart(
            object executionAction)
        {
            if (!_enabled.Value
                || !string.Equals(
                    GetCombatExecutionMode(),
                    CombatExecutionModeExecution,
                    StringComparison.Ordinal))
            {
                return null;
            }

            object cachedData = GetOptionalFieldValue(
                executionAction,
                "_cachedData");
            if (cachedData == null)
            {
                LogDiagnostic("Execution FinisherStart: selected data was unavailable.");
                return null;
            }

            FieldInfo slowDownTimeField = AccessTools.Field(
                cachedData.GetType(),
                "slowDownTime");
            bool? originalSlowDownTime = null;
            if (slowDownTimeField == null
                || slowDownTimeField.FieldType != typeof(bool))
            {
                Log.LogWarning("Could not read Execution slowDownTime; the selected Execution asset may retain native slow motion.");
            }
            else
            {
                try
                {
                    originalSlowDownTime = (bool)slowDownTimeField.GetValue(cachedData);
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Could not read Execution slowDownTime: " + ex.GetBaseException().Message);
                    slowDownTimeField = null;
                }
            }

            ExecutionFinisherStartState state = new ExecutionFinisherStartState(
                cachedData,
                slowDownTimeField,
                originalSlowDownTime);
            state.Activate();
            if (state.HasSlowDownTimeField)
            {
                state.DisableSlowDownTime();
            }

            LogDiagnostic(
                "Execution FinisherStart: assetSlowDownTime="
                + state.DescribeOriginalSlowDownTime()
                + ", temporaryAssetFlag="
                + state.DescribeTemporarySlowDownTime()
                + ".");
            return state;
        }

        private void OnExecutionFinisherStarted(
            object finisherState,
            object runtimeData,
            ExecutionFinisherStartState startState)
        {
            if (startState == null
                || !ReferenceEquals(_activeExecutionFinisherStart, startState))
            {
                return;
            }

            object payloadValue = GetOptionalFieldValue(runtimeData, "slowDownTime");
            bool? payloadSlowDownTime = payloadValue is bool
                ? (bool)payloadValue
                : (bool?)null;

            _activeExecutionFinisher = new ExecutionFinisherLifecycleState(
                finisherState,
                payloadSlowDownTime,
                Time.unscaledTime,
                Time.realtimeSinceStartup);
            LogDiagnostic(
                "Execution FinisherStarted: assetSlowDownTime="
                + startState.DescribeOriginalSlowDownTime()
                + ", payloadSlowDownTime="
                + DescribeNullableBool(payloadSlowDownTime)
                + ", timeScale="
                + FormatFloat(Time.timeScale)
                + ".");
        }

        private void OnExecutionFinisherExited(object finisherState)
        {
            ExecutionFinisherLifecycleState state = _activeExecutionFinisher;
            if (state == null || !ReferenceEquals(state.FinisherState, finisherState))
            {
                return;
            }

            _activeExecutionFinisher = null;
            LogDiagnostic(
                "Execution FinisherEnded/OnExit: elapsedUnscaled="
                + FormatFloat(Time.unscaledTime - state.StartedUnscaledTime)
                + "s, elapsedRealtime="
                + FormatFloat(Time.realtimeSinceStartup - state.StartedRealtime)
                + "s, timeScale="
                + FormatFloat(Time.timeScale)
                + ".");
        }

        private void ReportActiveExecutionFinisherLifecycle()
        {
            ExecutionFinisherLifecycleState state = _activeExecutionFinisher;
            if (!_diagnostics.Value || state == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - state.LastLifecycleDiagnosticUnscaledTime
                < ExecutionLifecycleDiagnosticRepeatSeconds)
            {
                return;
            }

            state.LastLifecycleDiagnosticUnscaledTime = now;
            Log.LogInfo(
                "Execution Finisher still active: payloadSlowDownTime="
                + DescribeNullableBool(state.PayloadSlowDownTime)
                + ", elapsedUnscaled="
                + FormatFloat(now - state.StartedUnscaledTime)
                + "s, elapsedRealtime="
                + FormatFloat(Time.realtimeSinceStartup - state.StartedRealtime)
                + "s, timeScale="
                + FormatFloat(Time.timeScale)
                + ".");
        }

        private static string DescribeNullableBool(bool? value)
        {
            return value.HasValue ? value.Value.ToString() : "unavailable";
        }

        private bool TryPrepareExecutionEvaluation(
            object executionAction,
            out ExecutionEvaluationState state)
        {
            state = null;
            if (executionAction == null)
            {
                LogExecutionEligibility(
                    null,
                    "blocked: combat execution action was unavailable");
                return false;
            }
            if (_isHostileToHeroMethod == null)
            {
                LogExecutionEligibility(
                    executionAction,
                    "blocked: IsHostileToHero accessor was unavailable");
                return false;
            }

            object finisherHandling = GetOptionalFieldValue(
                executionAction,
                "_finisherHandlingElement");
            object npc = GetOptionalFieldValue(
                finisherHandling,
                "_npcPointingTowards");
            if (finisherHandling == null)
            {
                LogExecutionEligibility(
                    executionAction,
                    "blocked: FinisherHandlingElement was unavailable");
                return false;
            }
            if (npc == null)
            {
                LogExecutionEligibility(
                    executionAction,
                    "blocked: no NPC is under the combat targeting ray");
                return false;
            }
            if (GetBoolProperty(npc, "HasBeenDiscarded", false))
            {
                LogExecutionEligibility(npc, "blocked: target was discarded");
                return false;
            }
            if (!GetBoolProperty(npc, "IsAlive", false))
            {
                LogExecutionEligibility(npc, "blocked: target was not alive");
                return false;
            }
            if (GetBoolProperty(npc, "IsDying", false))
            {
                LogExecutionEligibility(npc, "blocked: target was already dying");
                return false;
            }
            if (GetBoolProperty(npc, "IsUnconscious", false))
            {
                LogExecutionEligibility(
                    npc,
                    "blocked: target was unconscious; story executions remain separate");
                return false;
            }
            if (GetBoolProperty(npc, "IsInRagdoll", false))
            {
                LogExecutionEligibility(npc, "blocked: target was ragdolled");
                return false;
            }
            if (!GetBoolProperty(npc, "CanUseExternalCustomDeath", false))
            {
                LogExecutionEligibility(
                    npc,
                    "blocked: target disallows external custom-death animations");
                return false;
            }

            try
            {
                if (!(bool)_isHostileToHeroMethod.Invoke(null, new[] { npc }))
                {
                    LogExecutionEligibility(npc, "blocked: target was not hostile to the hero");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic("Could not verify Execution target hostility: " + ex.GetBaseException().Message);
                LogExecutionEligibility(
                    npc,
                    "blocked: hostility check threw "
                    + ex.GetBaseException().GetType().Name);
                return false;
            }

            object npcAi = GetOptionalPropertyValue(npc, "NpcAI");
            if (npcAi == null)
            {
                LogExecutionEligibility(npc, "blocked: target had no active NpcAI");
                return false;
            }
            if (!GetBoolProperty(npcAi, "InCombat", false))
            {
                LogExecutionEligibility(npc, "blocked: target NpcAI was not in combat");
                return false;
            }

            bool foundXpRewardAllowed;
            bool xpRewardAllowed = GetBoolProperty(
                npc,
                "XpRewardAllowed",
                true,
                out foundXpRewardAllowed);
            if (_requireXpRewardAllowedWhenPresent.Value
                && foundXpRewardAllowed
                && !xpRewardAllowed)
            {
                LogExecutionEligibility(
                    npc,
                    "blocked: XpRewardAllowed was false and the matching safeguard is enabled");
                return false;
            }

            object health = GetOptionalPropertyValue(npc, "Health");
            float currentHealth = GetOptionalFloatProperty(
                health,
                "ModifiedValue",
                -1.0f);
            object maxHealth = GetOptionalPropertyValue(npc, "MaxHealth");
            float maximumHealth = GetOptionalFloatProperty(
                maxHealth,
                "ModifiedValue",
                -1.0f);
            if (currentHealth <= 0.0f || maximumHealth <= 0.0f)
            {
                LogExecutionEligibility(
                    npc,
                    "blocked: health values were unavailable; current="
                    + FormatFloat(currentHealth)
                    + ", maximum="
                    + FormatFloat(maximumHealth));
                return false;
            }
            float healthPercent = currentHealth * 100.0f / maximumHealth;
            float maximumExecutionHealthPercent =
                GetExecutionMaximumHealthPercent();
            if (healthPercent <= 0.0f
                || healthPercent > maximumExecutionHealthPercent)
            {
                LogExecutionEligibility(
                    npc,
                    "blocked: health="
                    + FormatFloat(currentHealth)
                    + "/"
                    + FormatFloat(maximumHealth)
                    + " ("
                    + FormatFloat(healthPercent)
                    + "%), threshold="
                    + FormatFloat(maximumExecutionHealthPercent)
                    + "% maximum at mastery");
                return false;
            }

            if (_expandedExecutionTargets.Value)
            {
                string exclusionReason;
                if (!IsExpandedExecutionTargetAllowed(
                    npc,
                    out exclusionReason))
                {
                    LogExecutionEligibility(
                        npc,
                        "blocked: expanded Execution " + exclusionReason);
                    return false;
                }
            }

            object hero = GetOptionalPropertyValue(finisherHandling, "ParentModel");
            object mainHandItem = GetOptionalPropertyValue(hero, "MainHandItem");
            object offHandItem = GetOptionalPropertyValue(hero, "OffHandItem");
            bool mainHandEligible = GetBoolProperty(mainHandItem, "IsMelee", false);
            bool offHandEligible = GetBoolProperty(offHandItem, "IsMelee", false);
            if (!mainHandEligible && !offHandEligible)
            {
                LogExecutionEligibility(
                    npc,
                    "blocked: no equipped melee item; main="
                    + DescribeObject(mainHandItem)
                    + ", off="
                    + DescribeObject(offHandItem));
                return false;
            }

            List<object> executionLists = new List<object>();
            object mainHandWeapon = GetOptionalPropertyValue(
                hero,
                "MainHandWeapon");
            object offHandWeapon = GetOptionalPropertyValue(
                hero,
                "OffHandWeapon");
            object mainExecutionList = GetOptionalPropertyValue(
                mainHandWeapon,
                "ExecutionsList");
            object offExecutionList = GetOptionalPropertyValue(
                offHandWeapon,
                "ExecutionsList");
            object mainFinisherList = GetOptionalPropertyValue(
                mainHandWeapon,
                "FinishersList");
            object offFinisherList = GetOptionalPropertyValue(
                offHandWeapon,
                "FinishersList");
            AddUniqueReference(
                executionLists,
                mainExecutionList);
            AddUniqueReference(
                executionLists,
                offExecutionList);
            AddUniqueReference(
                executionLists,
                GetOptionalFieldValue(executionAction, "_cachedFinisherList"));
            List<object> automaticFinisherLists = new List<object>();
            AddUniqueReference(
                automaticFinisherLists,
                mainFinisherList);
            AddUniqueReference(
                automaticFinisherLists,
                offFinisherList);
            if (executionLists.Count == 0
                && automaticFinisherLists.Count == 0)
            {
                LogExecutionEligibility(
                    npc,
                    "blocked: equipped melee weapon had no loaded execution or normal finisher list; mainWeapon="
                    + DescribeObject(mainHandWeapon)
                    + ", mainList="
                    + DescribeObject(mainExecutionList)
                    + ", mainFallbackList="
                    + DescribeObject(mainFinisherList)
                    + ", offWeapon="
                    + DescribeObject(offHandWeapon)
                    + ", offList="
                    + DescribeObject(offExecutionList)
                    + ", offFallbackList="
                    + DescribeObject(offFinisherList));
                return false;
            }

            ExecutionEvaluationState preparedState = new ExecutionEvaluationState();
            try
            {
                for (int i = 0; i < executionLists.Count; i++)
                {
                    preparedState.RegisterExecutionList(executionLists[i]);
                    PrepareExecutionList(
                        executionLists[i],
                        preparedState,
                        _expandedExecutionTargets.Value);
                }
                for (int i = 0; i < automaticFinisherLists.Count; i++)
                {
                    preparedState.RegisterAutomaticFinisherList(
                        automaticFinisherLists[i]);
                    if (!ContainsReference(
                        executionLists,
                        automaticFinisherLists[i]))
                    {
                        PrepareExecutionList(
                            automaticFinisherLists[i],
                            preparedState,
                            _expandedExecutionTargets.Value);
                    }
                }
                preparedState.Activate();
                state = preparedState;
                return true;
            }
            catch (Exception ex)
            {
                preparedState.Restore();
                LogDiagnostic("Could not prepare Execution finisher conditions: " + ex.GetBaseException().Message);
                LogExecutionEligibility(
                    npc,
                    "blocked: temporary finisher-condition preparation threw "
                    + ex.GetBaseException().GetType().Name);
                return false;
            }
        }

        private bool TryCacheAutomaticFinisherFallback(
            object executionAction,
            ExecutionEvaluationState state)
        {
            if (_tryFindFinisherMethod == null
                || _automaticFinisherTrigger == null
                || executionAction == null
                || state == null)
            {
                return false;
            }

            object finisherHandling = GetOptionalFieldValue(
                executionAction,
                "_finisherHandlingElement");
            if (finisherHandling == null)
            {
                return false;
            }

            FieldInfo cachedDataField = AccessTools.Field(
                executionAction.GetType(),
                "_cachedData");
            FieldInfo cachedDamageOutcomeField = AccessTools.Field(
                executionAction.GetType(),
                "_cachedDamageOutcome");
            FieldInfo cachedFinisherListField = AccessTools.Field(
                executionAction.GetType(),
                "_cachedFinisherList");
            FieldInfo cachedDamageField = AccessTools.Field(
                executionAction.GetType(),
                "_cachedDmg");
            if (cachedDataField == null
                || cachedDamageOutcomeField == null
                || cachedFinisherListField == null
                || cachedDamageField == null)
            {
                LogExecutionEligibility(
                    executionAction,
                    "blocked: normal-finisher fallback could not access the native execution cache");
                return false;
            }

            try
            {
                ParameterInfo[] parameters = _tryFindFinisherMethod.GetParameters();
                object damageOutcome = Activator.CreateInstance(
                    parameters[2].ParameterType.GetElementType());
                object[] arguments =
                {
                    _automaticFinisherTrigger,
                    null,
                    damageOutcome,
                    null,
                    0.0f
                };
                bool found = (bool)_tryFindFinisherMethod.Invoke(
                    finisherHandling,
                    arguments);
                if (!found
                    || arguments[1] == null
                    || arguments[3] == null
                    || !state.IsAutomaticFinisherList(arguments[3]))
                {
                    return false;
                }

                cachedDataField.SetValue(executionAction, arguments[1]);
                cachedDamageOutcomeField.SetValue(executionAction, arguments[2]);
                cachedFinisherListField.SetValue(executionAction, arguments[3]);
                cachedDamageField.SetValue(executionAction, arguments[4]);
                return true;
            }
            catch (Exception ex)
            {
                LogDiagnostic(
                    "Normal combat-finisher fallback failed: "
                    + ex.GetBaseException().Message);
                return false;
            }
        }

        private void OnExecutionNativeEvaluationCompleted(
            object executionAction,
            ref bool available,
            ExecutionEvaluationState state)
        {
            object finisherHandling = GetOptionalFieldValue(
                executionAction,
                "_finisherHandlingElement");
            object npc = GetOptionalFieldValue(
                finisherHandling,
                "_npcPointingTowards");
            object cachedData = GetOptionalFieldValue(
                executionAction,
                "_cachedData");
            object cachedList = GetOptionalFieldValue(
                executionAction,
                "_cachedFinisherList");
            if (!available
                && cachedData == null
                && TryCacheAutomaticFinisherFallback(
                    executionAction,
                    state))
            {
                cachedData = GetOptionalFieldValue(
                    executionAction,
                    "_cachedData");
                cachedList = GetOptionalFieldValue(
                    executionAction,
                    "_cachedFinisherList");
            }
            if (cachedData != null)
            {
                string source = state != null
                    && state.IsAutomaticFinisherList(cachedList)
                        ? "normal-finisher fallback"
                        : "native execution";
                string progressionStatus;
                if (!TryValidateExecutionProgression(
                    executionAction,
                    npc,
                    out progressionStatus))
                {
                    available = false;
                    ClearExecutionCandidate(executionAction);
                    LogExecutionEligibility(
                        npc ?? executionAction,
                        "blocked: "
                        + progressionStatus
                        + "; selected "
                        + source
                        + "; "
                        + (state == null
                            ? "condition evaluation unavailable"
                            : state.DescribeConditionEvaluation()));
                    return;
                }

                if (available)
                {
                    LogExecutionEligibility(
                        npc ?? executionAction,
                        "available: "
                        + source
                        + " accepted; "
                        + progressionStatus
                        + "; Execute prompt should be visible; "
                        + (state == null
                            ? "condition evaluation unavailable"
                            : state.DescribeConditionEvaluation()));
                    return;
                }

                LogExecutionEligibility(
                    npc ?? executionAction,
                    "pending: "
                    + source
                    + " candidate="
                    + DescribeObject(cachedData)
                    + ", list="
                    + DescribeObject(cachedList)
                    + "; "
                    + progressionStatus
                    + "; waiting for the native 0.6-second activation delay; "
                    + (state == null
                        ? "condition evaluation unavailable"
                        : state.DescribeConditionEvaluation()));
                return;
            }

            string failureStatus =
                "blocked: native execution and normal-finisher fallback found no compatible loaded animation";
            available = false;
            if (_diagnostics.Value)
            {
                failureStatus += "; "
                    + (state == null
                        ? "animation readiness unavailable"
                        : state.DescribeAnimationReadiness()
                            + "; "
                            + state.DescribeConditionEvaluation());
            }
            LogExecutionEligibility(
                npc ?? executionAction,
                failureStatus);
        }

        private bool TryValidateExecutionProgression(
            object executionAction,
            object npc,
            out string status)
        {
            object cachedDamageOutcome = GetOptionalFieldValue(
                executionAction,
                "_cachedDamageOutcome");
            object damage = GetOptionalPropertyValue(
                cachedDamageOutcome,
                "Damage")
                ?? GetOptionalFieldValue(cachedDamageOutcome, "Damage");
            object item = GetOptionalPropertyValue(damage, "Item")
                ?? GetOptionalFieldValue(damage, "Item");
            if (item == null)
            {
                status = "selected execution weapon was unavailable";
                return false;
            }

            object proficiency = ResolveItemProficiency(item);
            string proficiencyName = DescribeProficiency(proficiency);
            object finisherHandling = GetOptionalFieldValue(
                executionAction,
                "_finisherHandlingElement");
            object hero = GetOptionalPropertyValue(
                finisherHandling,
                "ParentModel");
            int proficiencyLevel = GetExecutionProficiencyLevel(
                hero,
                proficiency);
            if (proficiencyLevel < 0)
            {
                status = "could not resolve a supported melee proficiency for "
                    + DescribeObject(item);
                return false;
            }

            int minimumProficiency = Math.Max(
                0,
                Math.Min(100, _executionMinimumProficiency.Value));
            if (proficiencyLevel < minimumProficiency)
            {
                status = proficiencyName
                    + " proficiency="
                    + proficiencyLevel.ToString(CultureInfo.InvariantCulture)
                    + ", required="
                    + minimumProficiency.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            object health = GetOptionalPropertyValue(npc, "Health");
            object maxHealth = GetOptionalPropertyValue(npc, "MaxHealth");
            float currentHealth = GetOptionalFloatProperty(
                health,
                "ModifiedValue",
                -1.0f);
            float maximumHealth = GetOptionalFloatProperty(
                maxHealth,
                "ModifiedValue",
                -1.0f);
            if (currentHealth <= 0.0f || maximumHealth <= 0.0f)
            {
                status = "target health was unavailable during proficiency validation";
                return false;
            }

            float healthPercent = currentHealth * 100.0f / maximumHealth;
            float threshold = GetExecutionHealthPercent(
                proficiencyLevel,
                minimumProficiency);
            status = proficiencyName
                + " proficiency="
                + proficiencyLevel.ToString(CultureInfo.InvariantCulture)
                + ", threshold="
                + FormatFloat(threshold)
                + "%, health="
                + FormatFloat(healthPercent)
                + "%";
            return healthPercent > 0.0f && healthPercent <= threshold;
        }

        private int GetExecutionProficiencyLevel(
            object hero,
            object proficiency)
        {
            string propertyName = null;
            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                propertyName = "OneHanded";
            }
            else if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                propertyName = "TwoHanded";
            }
            else if (ReferenceEquals(proficiency, _unarmedProf))
            {
                propertyName = "Unarmed";
            }
            else if (ReferenceEquals(proficiency, _shieldProf))
            {
                propertyName = "Shield";
            }
            if (propertyName == null)
            {
                return -1;
            }

            object proficiencyStats = GetOptionalPropertyValue(
                hero,
                "ProficiencyStats");
            object stat = GetOptionalPropertyValue(
                proficiencyStats,
                propertyName);
            int level = GetOptionalIntProperty(stat, "ModifiedInt", -1);
            if (level >= 0)
            {
                return Math.Min(100, level);
            }

            float modifiedValue = GetOptionalFloatProperty(
                stat,
                "ModifiedValue",
                -1.0f);
            return modifiedValue < 0.0f
                ? -1
                : Math.Min(100, (int)Math.Floor(modifiedValue));
        }

        private float GetExecutionHealthPercent(
            int proficiencyLevel,
            int minimumProficiency)
        {
            float unlockThreshold = Math.Max(
                1.0f,
                Math.Min(30.0f, _executionHealthPercentAtUnlock.Value));
            float masteryThreshold = GetExecutionMaximumHealthPercent();
            float progression = minimumProficiency >= 100
                ? 1.0f
                : Math.Max(
                    0.0f,
                    Math.Min(
                        1.0f,
                        (proficiencyLevel - minimumProficiency)
                            / (100.0f - minimumProficiency)));
            return unlockThreshold
                + (masteryThreshold - unlockThreshold) * progression;
        }

        private float GetExecutionMaximumHealthPercent()
        {
            float unlockThreshold = Math.Max(
                1.0f,
                Math.Min(30.0f, _executionHealthPercentAtUnlock.Value));
            float masteryThreshold = Math.Max(
                1.0f,
                Math.Min(30.0f, _executionHealthPercentAtMastery.Value));
            return Math.Max(unlockThreshold, masteryThreshold);
        }

        private void ClearExecutionCandidate(object executionAction)
        {
            try
            {
                MethodInfo clearCaches = AccessTools.Method(
                    executionAction.GetType(),
                    "ClearCaches");
                if (clearCaches != null)
                {
                    clearCaches.Invoke(executionAction, null);
                }
                FieldInfo activationTime = AccessTools.Field(
                    executionAction.GetType(),
                    "_activationTime");
                if (activationTime != null)
                {
                    activationTime.SetValue(executionAction, null);
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic(
                    "Could not clear a progression-blocked Execution candidate: "
                    + ex.GetBaseException().Message);
            }
        }

        private void LogExecutionEligibility(object target, string status)
        {
            if (!_diagnostics.Value)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (ReferenceEquals(target, _lastExecutionDiagnosticTarget)
                && string.Equals(
                    status,
                    _lastExecutionDiagnosticStatus,
                    StringComparison.Ordinal)
                && now - _lastExecutionDiagnosticTime
                    < ExecutionDiagnosticRepeatSeconds)
            {
                return;
            }

            _lastExecutionDiagnosticTarget = target;
            _lastExecutionDiagnosticStatus = status;
            _lastExecutionDiagnosticTime = now;
            object template = GetOptionalPropertyValue(target, "Template");
            Log.LogInfo(
                "Execution eligibility: target="
                + DescribeObject(target)
                + ", template="
                + DescribeObject(template)
                + "; "
                + status
                + ".");
        }

        private bool IsExpandedExecutionTargetAllowed(
            object npc,
            out string reason)
        {
            reason = string.Empty;
            string[] excludedAbstracts =
                GetExpandedExecutionExcludedAbstracts();
            if (excludedAbstracts.Length == 0)
            {
                return true;
            }

            object template = GetOptionalPropertyValue(npc, "Template");
            if (template == null)
            {
                reason = "could not inspect the target template's abstract families";
                return false;
            }

            object pooledAbstracts = null;
            try
            {
                pooledAbstracts = GetOptionalPropertyValue(
                    template,
                    "AbstractTypes");
                object abstractValues = GetOptionalFieldValue(
                    pooledAbstracts,
                    "value");
                if (abstractValues == null)
                {
                    abstractValues = GetOptionalPropertyValue(
                        pooledAbstracts,
                        "Value");
                }
                if (abstractValues == null)
                {
                    reason = "could not inspect the target template's inherited abstract families";
                    return false;
                }

                foreach (object abstractTemplate in EnumerateObjects(abstractValues))
                {
                    string abstractName = NormalizeExecutionAbstractName(
                        GetOptionalPropertyValue(
                            abstractTemplate,
                            "DebugName") as string);
                    if (abstractName.Length == 0)
                    {
                        abstractName = NormalizeExecutionAbstractName(
                            abstractTemplate == null
                                ? string.Empty
                                : abstractTemplate.ToString());
                    }

                    for (int i = 0; i < excludedAbstracts.Length; i++)
                    {
                        if (string.Equals(
                            abstractName,
                            excludedAbstracts[i],
                            StringComparison.OrdinalIgnoreCase))
                        {
                            reason = "excluded abstract family " + abstractName;
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = "abstract-family inspection failed ("
                    + ex.GetBaseException().GetType().Name
                    + ")";
                return false;
            }
            finally
            {
                if (pooledAbstracts != null)
                {
                    try
                    {
                        MethodInfo release = AccessTools.Method(
                            pooledAbstracts.GetType(),
                            "Release");
                        if (release != null)
                        {
                            release.Invoke(pooledAbstracts, null);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private string[] GetExpandedExecutionExcludedAbstracts()
        {
            string raw = _expandedExecutionExcludedAbstracts == null
                ? string.Empty
                : (_expandedExecutionExcludedAbstracts.Value ?? string.Empty);
            if (raw == _cachedExpandedExecutionExcludedAbstractsRaw)
            {
                return _cachedExpandedExecutionExcludedAbstracts;
            }

            _cachedExpandedExecutionExcludedAbstractsRaw = raw;
            string[] configured = SplitTerms(raw);
            List<string> normalized = new List<string>();
            for (int i = 0; i < configured.Length; i++)
            {
                string abstractName = NormalizeExecutionAbstractName(
                    configured[i]);
                if (abstractName.Length == 0)
                {
                    continue;
                }

                bool alreadyAdded = false;
                for (int j = 0; j < normalized.Count; j++)
                {
                    if (string.Equals(
                        normalized[j],
                        abstractName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyAdded = true;
                        break;
                    }
                }
                if (!alreadyAdded)
                {
                    normalized.Add(abstractName);
                }
            }

            _cachedExpandedExecutionExcludedAbstracts = normalized.ToArray();
            return _cachedExpandedExecutionExcludedAbstracts;
        }

        private static string NormalizeExecutionAbstractName(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
            if (normalized.StartsWith(
                "Abstract:",
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Abstract:".Length);
            }
            if (normalized.StartsWith(
                "Abstract_NPCTemplate_",
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(
                    "Abstract_NPCTemplate_".Length);
            }
            else if (normalized.StartsWith(
                "NPCTemplate_Abstract_",
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(
                    "NPCTemplate_Abstract_".Length);
            }

            return normalized.Trim();
        }

        private static void AddUniqueReference(List<object> values, object value)
        {
            if (value == null)
            {
                return;
            }

            if (!ContainsReference(values, value))
            {
                values.Add(value);
            }
        }

        private static bool ContainsReference(List<object> values, object value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], value))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PrepareExecutionList(
            object executionList,
            ExecutionEvaluationState state,
            bool expandedTargets)
        {
            FieldInfo healthConditionField = AccessTools.Field(
                executionList.GetType(),
                "defaultHealthCondition");
            if (healthConditionField == null)
            {
                throw new MissingFieldException(
                    executionList.GetType().FullName,
                    "defaultHealthCondition");
            }

            object healthCondition = healthConditionField.GetValue(executionList);
            FieldInfo conditionField = AccessTools.Field(
                healthCondition.GetType(),
                "condition");
            FieldInfo hpValueField = AccessTools.Field(
                healthCondition.GetType(),
                "hpValue");
            if (conditionField == null || hpValueField == null)
            {
                throw new MissingFieldException(
                    healthCondition.GetType().FullName,
                    "condition/hpValue");
            }
            conditionField.SetValue(
                healthCondition,
                Enum.ToObject(conditionField.FieldType, 4));
            hpValueField.SetValue(
                healthCondition,
                Instance.GetExecutionMaximumHealthPercent() / 100.0f);
            state.SetField(executionList, healthConditionField, healthCondition);

            object globalConditions = GetFieldValue(executionList, "globalConditions");
            foreach (object condition in EnumerateObjects(globalConditions))
            {
                if (condition != null
                    && string.Equals(
                        condition.GetType().FullName,
                        "Awaken.TG.Main.AI.Combat.CustomDeath.Conditions.TargetStateCustomDeathCondition",
                        StringComparison.Ordinal))
                {
                    SetEnumField(state, condition, "hasToBeStaggered", 0);
                    SetEnumField(state, condition, "hasToBeUnconscious", 2);
                    SetEnumField(state, condition, "hasToBeRagdolled", 2);
                    SetEnumField(state, condition, "requiredState", 8);
                    SetFieldIfPresent(state, condition, "hasToBeTheLastTarget", false);
                    SetFieldIfPresent(state, condition, "hasToBeTheLastEnemy", false);
                }
            }

            object finishers = GetFieldValue(executionList, "finishers");
            foreach (object finisher in EnumerateObjects(finishers))
            {
                if (finisher == null)
                {
                    continue;
                }

                SetFieldIfPresent(state, finisher, "overrideHealthConditions", false);
                if (expandedTargets)
                {
                    FieldInfo targetAbstractsField = AccessTools.Field(
                        finisher.GetType(),
                        "targetAbstracts");
                    if (targetAbstractsField != null
                        && targetAbstractsField.FieldType.IsArray)
                    {
                        Type elementType = targetAbstractsField.FieldType.GetElementType();
                        state.SetField(
                            finisher,
                            targetAbstractsField,
                            Array.CreateInstance(elementType, 0));
                    }
                }

                object targetDeathAnimation = GetFieldValue(
                    finisher,
                    "targetDeathAnimation");
                object animationConditions = GetFieldValue(
                    targetDeathAnimation,
                    "conditions");
                foreach (object animationCondition in EnumerateObjects(animationConditions))
                {
                    if (animationCondition != null
                        && string.Equals(
                            animationCondition.GetType().FullName,
                            "Awaken.TG.Main.AI.Combat.CustomDeath.Conditions.RandomChangeCustomDeathCondition",
                            StringComparison.Ordinal))
                    {
                        SetFieldIfPresent(
                            state,
                            animationCondition,
                            "procChance",
                            1.0f);
                    }
                }
            }
        }

        private static string DescribeFinisherAnimationReadiness(
            List<object> finisherLists)
        {
            int candidateCount = 0;
            int loadedCount = 0;
            int loadingCount = 0;
            int failedCount = 0;
            int invalidCount = 0;
            int completedNullCount = 0;
            int unknownCount = 0;

            for (int listIndex = 0;
                listIndex < finisherLists.Count;
                listIndex++)
            {
                object finishers = GetFieldValue(
                    finisherLists[listIndex],
                    "finishers");
                foreach (object finisher in EnumerateObjects(finishers))
                {
                    if (finisher == null)
                    {
                        continue;
                    }

                    candidateCount++;
                    object handle = GetFieldValue(
                        finisher,
                        "_heroAnimationHandle");
                    if (handle == null)
                    {
                        unknownCount++;
                        continue;
                    }

                    try
                    {
                        MethodInfo isValidMethod = AccessTools.Method(
                            handle.GetType(),
                            "IsValid");
                        if (isValidMethod == null
                            || !(bool)isValidMethod.Invoke(handle, null))
                        {
                            invalidCount++;
                            continue;
                        }

                        PropertyInfo resultProperty = AccessTools.Property(
                            handle.GetType(),
                            "Result");
                        if (resultProperty != null
                            && resultProperty.GetValue(handle, null) != null)
                        {
                            loadedCount++;
                            continue;
                        }

                        string status = Convert.ToString(
                            GetStaticOrInstancePropertyValue(
                                handle,
                                "Status"),
                            CultureInfo.InvariantCulture);
                        if (string.Equals(
                            status,
                            "Failed",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            failedCount++;
                            continue;
                        }

                        object isDoneValue = GetStaticOrInstancePropertyValue(
                            handle,
                            "IsDone");
                        if (isDoneValue is bool && !(bool)isDoneValue)
                        {
                            loadingCount++;
                        }
                        else
                        {
                            completedNullCount++;
                        }
                    }
                    catch
                    {
                        unknownCount++;
                    }
                }
            }

            return "lists="
                + finisherLists.Count.ToString(CultureInfo.InvariantCulture)
                + ", candidates="
                + candidateCount.ToString(CultureInfo.InvariantCulture)
                + ", loaded="
                + loadedCount.ToString(CultureInfo.InvariantCulture)
                + ", loading="
                + loadingCount.ToString(CultureInfo.InvariantCulture)
                + ", failed="
                + failedCount.ToString(CultureInfo.InvariantCulture)
                + ", invalid="
                + invalidCount.ToString(CultureInfo.InvariantCulture)
                + ", completed-null="
                + completedNullCount.ToString(CultureInfo.InvariantCulture)
                + ", unknown="
                + unknownCount.ToString(CultureInfo.InvariantCulture);
        }

        private static object GetStaticOrInstancePropertyValue(
            object instance,
            string propertyName)
        {
            PropertyInfo property = AccessTools.Property(
                instance.GetType(),
                propertyName);
            return property == null
                ? null
                : property.GetValue(instance, null);
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            if (instance == null)
            {
                return null;
            }
            FieldInfo field = AccessTools.Field(instance.GetType(), fieldName);
            return field == null ? null : field.GetValue(instance);
        }

        private static IEnumerable EnumerateObjects(object value)
        {
            IEnumerable enumerable = value as IEnumerable;
            return enumerable ?? new object[0];
        }

        private static void SetEnumField(
            ExecutionEvaluationState state,
            object target,
            string fieldName,
            int value)
        {
            FieldInfo field = AccessTools.Field(target.GetType(), fieldName);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }
            state.SetField(target, field, Enum.ToObject(field.FieldType, value));
        }

        private static void SetFieldIfPresent(
            ExecutionEvaluationState state,
            object target,
            string fieldName,
            object value)
        {
            if (target == null)
            {
                return;
            }
            FieldInfo field = AccessTools.Field(target.GetType(), fieldName);
            if (field != null)
            {
                state.SetField(target, field, value);
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

                ShowAwardNotification(bonus, DescribeNotificationProficiency(proficiency, proficiencyName), enemyName, sourceName, enemyXp, proficiency, item);
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

        private void ShowAwardNotification(float bonus, string proficiencyName, string enemyName, string weaponName, float enemyXp, object proficiency, object item)
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
                if (TryShowGrailFloatingText(text, ResolveNotificationIconId(proficiency, item)))
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
                        new object[] { PluginGuid, GrailFloatingTextKillingBlowEventId, text, "Reward", "Reward", "Normal", string.Empty, iconId, GrailFloatingTextShortDurationBucket, 0.25f, 0.9f });
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

            try
            {
                MethodInfo builtInIconIdsMethod = AccessTools.Method(apiType, "GetBuiltInIconIds", Type.EmptyTypes);
                string[] iconIds = builtInIconIdsMethod == null ? null : builtInIconIdsMethod.Invoke(null, null) as string[];
                _grailFloatingTextSupportsSpecificWeaponIcons = iconIds != null
                    && Array.Exists(iconIds, id => string.Equals(id, "one_handed_sword", StringComparison.OrdinalIgnoreCase))
                    && Array.Exists(iconIds, id => string.Equals(id, "two_handed_spear", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception)
            {
                _grailFloatingTextSupportsSpecificWeaponIcons = false;
                LogDiagnostic("Could not inspect Grail Floating Text weapon icons; broad proficiency icons will be used: " + exception.GetBaseException().Message);
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

        private string ResolveNotificationIconId(object proficiency, object item)
        {
            string fallback = ResolveProficiencyIconId(proficiency);
            if (!TryResolveGrailFloatingTextBridge() || !_grailFloatingTextSupportsSpecificWeaponIcons || item == null)
            {
                return fallback;
            }

            if (ReferenceEquals(proficiency, _oneHandedProf))
            {
                if (GetBoolProperty(item, "IsDagger", false)) return "one_handed_dagger";
                if (GetBoolProperty(item, "IsSword", false)) return "one_handed_sword";
                if (GetBoolProperty(item, "IsAxe", false)) return "one_handed_axe";
                if (GetBoolProperty(item, "IsBlunt", false)) return "one_handed_blunt";
                if (GetBoolProperty(item, "IsPolearm", false)) return "one_handed_spear";
                if (GetBoolProperty(item, "IsSickle", false)) return "one_handed_axe";
            }
            else if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                if (GetBoolProperty(item, "IsSword", false)) return "two_handed_sword";
                if (GetBoolProperty(item, "IsAxe", false)) return "two_handed_axe";
                if (GetBoolProperty(item, "IsBlunt", false)) return "two_handed_blunt";
                if (GetBoolProperty(item, "IsPolearm", false)) return "two_handed_spear";
            }

            return fallback;
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
            float ignoredOffset;
            return GetRandomPitchMultiplier(
                _randomPitchSemitones.Value,
                out ignoredOffset);
        }

        private float GetRandomPitchMultiplier(
            float maximumSemitones,
            out float offset)
        {
            float semitones = Math.Max(0.0f, maximumSemitones);
            if (semitones <= 0.001f)
            {
                offset = 0.0f;
                return 1.0f;
            }

            offset = (float)((_random.NextDouble() * 2.0 - 1.0) * semitones);
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
            float t = Math.Max(0.0f, Math.Min(1.0f, distance / 30.0f));
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
                LogDiagnostic("One-Handed kill had no specific weapon subtype; using one_handed_blade instead of the generic killing_blow pool.");
                return OneHandedBladeSoundPool;
            }

            LogDiagnostic("One-Handed kill had no item subtype data; using one_handed_blade instead of the generic killing_blow pool.");
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
                LogDiagnostic("Two-Handed kill had no specific weapon subtype; using two_handed_blade instead of the generic killing_blow pool.");
                return TwoHandedBladeSoundPool;
            }

            LogDiagnostic("Two-Handed kill had no item subtype data; using two_handed_blade instead of the generic killing_blow pool.");
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
            object proficiency = ResolveVersatileWeaponsEffectiveProficiency(
                item);
            if (proficiency != null)
            {
                return proficiency;
            }

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

        private object ResolveVersatileWeaponsEffectiveProficiency(
            object item)
        {
            if (item == null || !TryResolveVersatileWeaponsBridge())
            {
                return null;
            }

            try
            {
                return _versatileWeaponsGetEffectiveProficiencyMethod.Invoke(
                    null,
                    new[] { item });
            }
            catch (Exception exception)
            {
                _versatileWeaponsGetEffectiveProficiencyMethod = null;
                if (!_versatileWeaponsBridgeFailureLogged)
                {
                    _versatileWeaponsBridgeFailureLogged = true;
                    LogDiagnostic(
                        "Versatile Weapons effective-proficiency API failed; using native proficiency fallback: "
                        + exception.GetBaseException().Message);
                }
                return null;
            }
        }

        private bool TryResolveVersatileWeaponsBridge()
        {
            if (_versatileWeaponsBridgeResolved)
            {
                return _versatileWeaponsGetEffectiveProficiencyMethod != null;
            }

            _versatileWeaponsBridgeResolved = true;
            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    VersatileWeaponsPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null
                || _itemType == null)
            {
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                VersatileWeaponsApiTypeName,
                false);
            _versatileWeaponsGetEffectiveProficiencyMethod = apiType == null
                ? null
                : AccessTools.Method(
                    apiType,
                    "GetEffectiveProficiency",
                    new[] { _itemType });
            if (_versatileWeaponsGetEffectiveProficiencyMethod == null)
            {
                if (!_versatileWeaponsBridgeFailureLogged)
                {
                    _versatileWeaponsBridgeFailureLogged = true;
                    LogDiagnostic(
                        "Versatile Weapons is loaded without its effective-proficiency API; using native proficiency fallback.");
                }
                return false;
            }

            LogDiagnostic(
                "Connected to the Versatile Weapons effective-proficiency API.");
            return true;
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

        private object GetOptionalFieldValue(object instance, string fieldName)
        {
            if (instance == null || string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            try
            {
                FieldInfo field = AccessTools.Field(instance.GetType(), fieldName);
                return field == null ? null : field.GetValue(instance);
            }
            catch
            {
                return null;
            }
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
                return "One-Handed";
            }
            if (ReferenceEquals(proficiency, _twoHandedProf))
            {
                return "Two-Handed";
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

        private void LogDiagnostic(string message)
        {
            if (_diagnostics.Value)
            {
                Log.LogInfo(message);
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

        private sealed class ExecutionFinisherStartState
        {
            private readonly object _cachedData;
            private readonly FieldInfo _slowDownTimeField;
            private ExecutionFinisherStartState _previousActiveState;
            private bool _activated;
            private bool _slowDownTimeDisabled;
            private bool _restored;

            public readonly bool? OriginalSlowDownTime;

            public ExecutionFinisherStartState(
                object cachedData,
                FieldInfo slowDownTimeField,
                bool? originalSlowDownTime)
            {
                _cachedData = cachedData;
                _slowDownTimeField = slowDownTimeField;
                OriginalSlowDownTime = originalSlowDownTime;
            }

            public bool HasSlowDownTimeField
            {
                get { return _slowDownTimeField != null && OriginalSlowDownTime.HasValue; }
            }

            public void Activate()
            {
                if (_activated)
                {
                    return;
                }

                _activated = true;
                _previousActiveState = _activeExecutionFinisherStart;
                _activeExecutionFinisherStart = this;
            }

            public void DisableSlowDownTime()
            {
                if (!HasSlowDownTimeField || _slowDownTimeDisabled)
                {
                    return;
                }

                try
                {
                    _slowDownTimeField.SetValue(_cachedData, false);
                    _slowDownTimeDisabled = true;
                }
                catch (Exception ex)
                {
                    if (Log != null)
                    {
                        Log.LogWarning(
                            "Could not temporarily disable Execution slowDownTime: "
                            + ex.GetBaseException().Message);
                    }
                }
            }

            public void Restore()
            {
                if (_restored)
                {
                    return;
                }
                _restored = true;

                try
                {
                    if (_slowDownTimeDisabled)
                    {
                        _slowDownTimeField.SetValue(
                            _cachedData,
                            OriginalSlowDownTime.Value);
                    }
                }
                catch (Exception ex)
                {
                    if (Log != null)
                    {
                        Log.LogWarning(
                            "Failed to restore Execution slowDownTime: "
                            + ex.GetBaseException().Message);
                    }
                }
                finally
                {
                    if (_activated
                        && ReferenceEquals(_activeExecutionFinisherStart, this))
                    {
                        _activeExecutionFinisherStart = _previousActiveState;
                    }
                    _previousActiveState = null;
                }
            }

            public string DescribeOriginalSlowDownTime()
            {
                return DescribeNullableBool(OriginalSlowDownTime);
            }

            public string DescribeTemporarySlowDownTime()
            {
                return _slowDownTimeDisabled ? "false" : "unchanged";
            }
        }

        private sealed class ExecutionFinisherLifecycleState
        {
            public readonly object FinisherState;
            public readonly bool? PayloadSlowDownTime;
            public readonly float StartedUnscaledTime;
            public readonly float StartedRealtime;
            public float LastLifecycleDiagnosticUnscaledTime;

            public ExecutionFinisherLifecycleState(
                object finisherState,
                bool? payloadSlowDownTime,
                float startedUnscaledTime,
                float startedRealtime)
            {
                FinisherState = finisherState;
                PayloadSlowDownTime = payloadSlowDownTime;
                StartedUnscaledTime = startedUnscaledTime;
                StartedRealtime = startedRealtime;
                LastLifecycleDiagnosticUnscaledTime = startedUnscaledTime;
            }
        }

        private sealed class ExecutionEvaluationState
        {
            private readonly List<FieldMutation> _mutations =
                new List<FieldMutation>();
            private readonly List<object> _executionLists =
                new List<object>();
            private readonly List<object> _automaticFinisherLists =
                new List<object>();
            private readonly List<object> _globalConditionBypassLists =
                new List<object>();
            private int _candidateConditionChecks;
            private int _candidateConditionAccepted;
            private ExecutionEvaluationState _previousActiveState;
            private bool _activated;
            private bool _restored;

            public void RegisterExecutionList(object list)
            {
                AddUniqueReference(_executionLists, list);
            }

            public void RegisterAutomaticFinisherList(object list)
            {
                AddUniqueReference(_automaticFinisherLists, list);
            }

            public bool IsAutomaticFinisherList(object list)
            {
                return ContainsReference(_automaticFinisherLists, list);
            }

            public string DescribeAnimationReadiness()
            {
                return "execution "
                    + DescribeFinisherAnimationReadiness(_executionLists)
                    + "; normal-finisher fallback "
                    + DescribeFinisherAnimationReadiness(_automaticFinisherLists);
            }

            public void RecordGlobalConditionsBypass(object list)
            {
                AddUniqueReference(_globalConditionBypassLists, list);
            }

            public void RecordCandidateConditionResult(bool accepted)
            {
                _candidateConditionChecks++;
                if (accepted)
                {
                    _candidateConditionAccepted++;
                }
            }

            public string DescribeConditionEvaluation()
            {
                return "global-bypassed-lists="
                    + _globalConditionBypassLists.Count.ToString(
                        CultureInfo.InvariantCulture)
                    + ", candidate-checks="
                    + _candidateConditionChecks.ToString(
                        CultureInfo.InvariantCulture)
                    + ", candidate-accepted="
                    + _candidateConditionAccepted.ToString(
                        CultureInfo.InvariantCulture)
                    + ", candidate-rejected="
                    + (_candidateConditionChecks - _candidateConditionAccepted)
                        .ToString(CultureInfo.InvariantCulture);
            }

            public void Activate()
            {
                if (_activated)
                {
                    return;
                }
                _activated = true;
                _previousActiveState = _activeExecutionEvaluation;
                _activeExecutionEvaluation = this;
            }

            public void SetField(
                object target,
                FieldInfo field,
                object replacement)
            {
                object original = field.GetValue(target);
                _mutations.Add(new FieldMutation(target, field, original));
                field.SetValue(target, replacement);
            }

            public void Restore()
            {
                if (_restored)
                {
                    return;
                }
                _restored = true;

                if (_activated
                    && ReferenceEquals(_activeExecutionEvaluation, this))
                {
                    _activeExecutionEvaluation = _previousActiveState;
                }
                _previousActiveState = null;

                for (int i = _mutations.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        _mutations[i].Field.SetValue(
                            _mutations[i].Target,
                            _mutations[i].Original);
                    }
                    catch (Exception ex)
                    {
                        if (Log != null)
                        {
                            Log.LogWarning(
                                "Failed to restore a temporary Execution condition: "
                                + ex.GetBaseException().Message);
                        }
                    }
                }
                _mutations.Clear();
            }

            private sealed class FieldMutation
            {
                public readonly object Target;
                public readonly FieldInfo Field;
                public readonly object Original;

                public FieldMutation(
                    object target,
                    FieldInfo field,
                    object original)
                {
                    Target = target;
                    Field = field;
                    Original = original;
                }
            }
        }

        private static class AutomaticCombatFinisherPatch
        {
            public static bool Prefix(ref bool __result)
            {
                if (Instance == null || Instance.AutomaticCombatFinishersAllowed)
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }

        private static class CombatExecutionAvailabilityPatch
        {
            public static bool Prefix(
                object __instance,
                ref bool __result,
                out ExecutionEvaluationState __state)
            {
                __state = null;
                if (Instance == null)
                {
                    return true;
                }

                string mode = Instance.GetCombatExecutionMode();
                if (string.Equals(
                    mode,
                    CombatExecutionModeOff,
                    StringComparison.Ordinal))
                {
                    __result = false;
                    return false;
                }
                if (!string.Equals(
                    mode,
                    CombatExecutionModeExecution,
                    StringComparison.Ordinal))
                {
                    return true;
                }

                if (!Instance.TryPrepareExecutionEvaluation(
                    __instance,
                    out __state))
                {
                    __result = false;
                    return false;
                }
                return true;
            }

            public static void Postfix(
                object __instance,
                ref bool __result,
                ExecutionEvaluationState __state)
            {
                if (__state != null)
                {
                    if (Instance != null)
                    {
                        Instance.OnExecutionNativeEvaluationCompleted(
                            __instance,
                            ref __result,
                            __state);
                    }
                    __state.Restore();
                }
            }

            public static Exception Finalizer(
                object __instance,
                Exception __exception,
                ExecutionEvaluationState __state)
            {
                if (__state != null)
                {
                    if (__exception != null && Instance != null)
                    {
                        Instance.LogExecutionEligibility(
                            __instance,
                            "blocked: native finisher evaluation threw "
                            + __exception.GetBaseException().GetType().Name);
                    }
                    __state.Restore();
                }
                return __exception;
            }
        }

        private static class CombatExecutionActionNamePatch
        {
            public static void Postfix(ref string __result)
            {
                if (Instance != null
                    && string.Equals(
                        Instance.GetCombatExecutionMode(),
                        CombatExecutionModeExecution,
                        StringComparison.Ordinal))
                {
                    __result = "Execute";
                }
            }
        }

        private static class ExecutionFinisherStartPatch
        {
            public static void Prefix(
                object __instance,
                out ExecutionFinisherStartState __state)
            {
                __state = Instance == null
                    ? null
                    : Instance.BeginExecutionFinisherStart(__instance);
            }

            public static void Postfix(ExecutionFinisherStartState __state)
            {
                if (__state != null)
                {
                    __state.Restore();
                }
            }

            public static Exception Finalizer(
                Exception __exception,
                ExecutionFinisherStartState __state)
            {
                if (__state != null)
                {
                    __state.Restore();
                }
                return __exception;
            }
        }

        private static class ExecutionFinisherLifecyclePatch
        {
            public static void FinisherStartedPostfix(
                object __instance,
                object data)
            {
                ExecutionFinisherStartState startState =
                    _activeExecutionFinisherStart;
                if (Instance != null && startState != null)
                {
                    Instance.OnExecutionFinisherStarted(
                        __instance,
                        data,
                        startState);
                }
            }

            public static void FinisherExitedPostfix(object __instance)
            {
                if (Instance != null)
                {
                    Instance.OnExecutionFinisherExited(__instance);
                }
            }
        }

        private static class ExecutionAnimationConditionsPatch
        {
            public static bool Prefix(ref bool __result)
            {
                if (_activeExecutionEvaluation == null)
                {
                    return true;
                }

                __result = true;
                return false;
            }
        }

        private static class ExecutionGlobalConditionsPatch
        {
            public static bool Prefix(object __instance, ref bool __result)
            {
                ExecutionEvaluationState state = _activeExecutionEvaluation;
                if (state == null)
                {
                    return true;
                }

                state.RecordGlobalConditionsBypass(__instance);
                __result = true;
                return false;
            }
        }

        private static class ExecutionDefaultHpConditionPatch
        {
            public static bool Prefix(ref bool __result)
            {
                if (_activeExecutionEvaluation == null)
                {
                    return true;
                }

                __result = true;
                return false;
            }
        }

        private static class ExecutionFinisherDataConditionsPatch
        {
            public static void Postfix(bool __result)
            {
                ExecutionEvaluationState state = _activeExecutionEvaluation;
                if (state != null)
                {
                    state.RecordCandidateConditionResult(__result);
                }
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
