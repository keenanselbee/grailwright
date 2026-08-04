using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.Main.Settings.Accessibility;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

[assembly: AssemblyTitle("Steel and Bone")]
[assembly: AssemblyDescription("Knowledge-based weakness and resistance difficulty mod for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Steel and Bone")]
[assembly: AssemblyVersion("1.0.6.0")]
[assembly: AssemblyFileVersion("1.0.6.0")]
[assembly: AssemblyInformationalVersion("1.0.6")]

namespace SteelAndBone
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SteelAndBonePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.steel-and-bone";
        public const string PluginName = "Steel and Bone";
        public const string PluginVersion = "1.0.6";

        private const int ConfigSchemaVersion = 14;
        private const int ConfigRecoveryBaselineSchema = 14;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const string DefaultDamageNumberBaseColor = "#E3BD02";
        private const int MaxPendingDamageFeedback = 128;
        private const float PendingDamageFeedbackLifetimeSeconds = 4.0f;
        private const string HealthElementTypeName = "Awaken.TG.Main.Character.HealthElement";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";
        private const string DamageSubTypeTypeName = "Awaken.TG.Main.Fights.DamageInfo.DamageSubType";
        private const string DamageReceivedMultiplierDataBaseTypeName = "Awaken.TG.Main.Fights.DamageInfo.DamageReceivedMultiplierDataBase";

        private static readonly Color DefaultDamageNumberColor = new Color32(0xE3, 0xBD, 0x02, 0xFF);
        private static readonly Color ResistedDamageNumberColor = new Color32(0x68, 0x68, 0x66, 0xFF);
        private static readonly Color WeaknessDamageNumberColor = new Color32(0xFF, 0x2F, 0x18, 0xFF);
        private static readonly Color ImmuneDamageNumberColor = new Color32(0xB8, 0xB8, 0xB4, 0xFF);
        private static readonly Color DamageNumberOutlineColor = new Color32(0x16, 0x10, 0x08, 0xFF);

        private static readonly DamageRule[] DamageRules =
        {
            new DamageRule(TargetFamily.BoneUndead, DamageTag.BloodMagic | DamageTag.Bleed, "Bone", "Blood/Bleed", 0.25f, 100),
            new DamageRule(TargetFamily.BoneUndead, DamageTag.Slashing | DamageTag.Piercing, "Bone", "Slash/Pierce", 0.55f, 80),
            new DamageRule(TargetFamily.BoneUndead, DamageTag.Bludgeoning, "Bone", "Blunt", 1.08f, 70),
            new DamageRule(TargetFamily.BoneUndead, DamageTag.GenericPhysical, "Bone", "Physical", 0.85f, 40),
            new DamageRule(TargetFamily.Construct, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Construct", "Blood/Bleed/Poison", 0.25f, 100),
            new DamageRule(TargetFamily.Construct, DamageTag.Slashing | DamageTag.Piercing, "Construct", "Slash/Pierce", 0.75f, 70),
            new DamageRule(TargetFamily.Construct, DamageTag.Bludgeoning, "Construct", "Blunt", 1.15f, 80),
            new DamageRule(TargetFamily.Construct, DamageTag.GenericPhysical, "Construct", "Physical", 0.85f, 40),
            new DamageRule(TargetFamily.ArmoredHumanoid, DamageTag.Slashing | DamageTag.GenericPhysical, "Armor", "Slash/Physical", 0.88f, 65),
            new DamageRule(TargetFamily.ArmoredHumanoid, DamageTag.Bludgeoning, "Armor", "Blunt", 1.10f, 66),
            new DamageRule(TargetFamily.Flesh, DamageTag.BloodMagic, "Flesh", "Blood", 1.10f, 25),
            new DamageRule(TargetFamily.Flesh, DamageTag.Bleed | DamageTag.Poison, "Flesh", "Bleed/Poison", 1.06f, 20),
            new DamageRule(TargetFamily.Flesh, DamageTag.Piercing, "Flesh", "Pierce", 1.06f, 16),
            new DamageRule(TargetFamily.Flesh, DamageTag.Slashing, "Flesh", "Slash", 1.04f, 15),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Undead", "Blood/Bleed/Poison", 0.78f, 55),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.Piercing, "Undead", "Pierce", 0.90f, 56),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.Fire, "Undead", "Fire", 1.08f, 50),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.Bludgeoning, "Undead", "Blunt", 1.05f, 45),
            new DamageRule(TargetFamily.Wyrd, DamageTag.Wyrdness, "Wyrd", "Wyrdness", 0.35f, 70),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.BloodMagic | DamageTag.Bleed, "Drowned", "Blood/Bleed", 0.65f, 80),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.Electric, "Drowned", "Electric", 1.15f, 70),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.Piercing, "Drowned", "Pierce", 0.90f, 65),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.Bludgeoning, "Drowned", "Blunt", 1.10f, 60),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Poison, "Infected", "Poison", 0.66f, 80),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Fire, "Infected", "Fire", 1.15f, 70),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Piercing, "Infected", "Pierce", 1.06f, 62),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Slashing, "Infected", "Slash", 1.04f, 61),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Cold, "Sea", "Cold", 0.70f, 70),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Electric, "Sea", "Electric", 1.12f, 60),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Piercing, "Sea", "Pierce", 1.06f, 56),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Slashing, "Sea", "Slash", 1.04f, 55),
            new DamageRule(TargetFamily.Spirit, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Spirit", "Blood/Bleed/Poison", 0.35f, 90),
            new DamageRule(TargetFamily.Spirit, DamageTag.Wyrdness, "Spirit", "Wyrdness", 1.15f, 60),
            new DamageRule(TargetFamily.Spirit, DamageTag.GenericPhysical | DamageTag.Slashing | DamageTag.Piercing | DamageTag.Bludgeoning, "Spirit", "Physical", 0.85f, 50),
            new DamageRule(TargetFamily.Flora, DamageTag.Poison | DamageTag.Bleed | DamageTag.Piercing, "Flora", "Poison/Bleed/Pierce", 0.70f, 70),
            new DamageRule(TargetFamily.Flora, DamageTag.Fire | DamageTag.Slashing, "Flora", "Fire/Slash", 1.15f, 70)
        };

        private static readonly NativeSubtypeCheck[] NativeSubtypeChecks =
        {
            new NativeSubtypeCheck(DamageTag.Wyrdness, "Wyrdness"),
            new NativeSubtypeCheck(DamageTag.GenericPhysical, "GenericPhysical"),
            new NativeSubtypeCheck(DamageTag.Slashing, "Slashing"),
            new NativeSubtypeCheck(DamageTag.Piercing, "Piercing"),
            new NativeSubtypeCheck(DamageTag.Bludgeoning, "Bludgeoning"),
            new NativeSubtypeCheck(DamageTag.GenericMagical, "GenericMagical"),
            new NativeSubtypeCheck(DamageTag.Fire, "Fire"),
            new NativeSubtypeCheck(DamageTag.Cold, "Cold"),
            new NativeSubtypeCheck(DamageTag.Poison, "Poison"),
            new NativeSubtypeCheck(DamageTag.Electric, "Electric"),
            new NativeSubtypeCheck(DamageTag.Wet, "Wet")
        };

        private static readonly string[] BleedTerms = { "bleed" };
        private static readonly string[] PoisonTerms = { "poison", "toxic", "venom" };
        private static readonly string[] WyrdTerms = { "wyrd" };
        private static readonly string[] BloodMagicTerms = { "blood", "transfusion", "abhartach", "sanguine", "sanguis", "hematic" };
        private static readonly string[] MetadataBoneUndeadTerms = { "HitBones", "Skeleton", "BoneMask" };
        private static readonly string[] MetadataConstructTerms = { "HitStone", "Construct", "Automaton", "Golem" };
        private static readonly string[] MetadataWyrdTerms = { "WyrdnessBound" };
        private static readonly string[] MetadataDrownedZombieTerms = { "Scourge" };
        private static readonly string[] MetadataSeaFleshTerms = { "SarrasCreature", "ReefboundBody" };
        private static readonly string[] MetadataSpiritTerms = { "Ghost" };
        private static readonly string[] MetadataFloraTerms = { "Flora" };
        private static readonly string[] MetadataFleshUndeadTerms = { "Zombie", "Bloody" };
        private static readonly string[] MetadataFleshTerms = { "Animal", "Animal_Prey", "Bandit", "Cultist", "Human", "Humanoid" };
        private static readonly string[] MetadataEliteTerms = { "Elite", "MiniBoss", "Boss", "Type:Elite" };

        internal static SteelAndBonePlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private MethodInfo _heroCurrentGetter;
        private Type _damageSubTypeType;
        private MethodInfo _getMultiplierForSubtypeMethod;
        private DamageNumberRenderer _damageNumberRenderer;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<Preset> _preset;
        private ConfigEntry<bool> _respectVanillaMultipliers;
        private ConfigEntry<bool> _amplifyVanillaMultipliers;
        private ConfigEntry<float> _temperedVanillaAmplification;
        private ConfigEntry<float> _hardenedVanillaAmplification;
        private ConfigEntry<float> _crucibleVanillaAmplification;
        private ConfigEntry<float> _minimumAmplifiedVanillaResistance;
        private ConfigEntry<float> _maximumAmplifiedVanillaWeakness;
        private ConfigEntry<bool> _eliteRuleClampsEnabled;
        private ConfigEntry<float> _eliteWeaknessBonusReduction;
        private ConfigEntry<float> _eliteMinimumResistanceMultiplier;
        private ConfigEntry<bool> _damageNumbersEnabled;
        private ConfigEntry<string> _damageNumberBaseColor;
        private ConfigEntry<int> _damageNumberFontSize;
        private ConfigEntry<DamageNumberFontMode> _damageNumberFontMode;
        private ConfigEntry<float> _damageNumberDurationSeconds;
        private ConfigEntry<float> _damageNumberCriticalDurationSeconds;
        private ConfigEntry<float> _damageNumberHorizontalDrift;
        private ConfigEntry<float> _damageNumberVerticalDrift;
        private ConfigEntry<float> _damageOverTimeNumberHeightMultiplier;
        private ConfigEntry<float> _damageNumberSizeContrast;
        private ConfigEntry<float> _damageNumberColorContrast;
        private ConfigEntry<float> _damageNumberMinimumAmount;
        private ConfigEntry<int> _damageNumberMaximumActive;
        private ConfigEntry<string> _boneUndeadTerms;
        private ConfigEntry<string> _constructTerms;
        private ConfigEntry<string> _wyrdTerms;
        private ConfigEntry<string> _drownedZombieTerms;
        private ConfigEntry<string> _infectedFleshTerms;
        private ConfigEntry<string> _seaFleshTerms;
        private ConfigEntry<string> _spiritTerms;
        private ConfigEntry<string> _floraTerms;
        private ConfigEntry<string> _fleshUndeadTerms;
        private ConfigEntry<string> _fleshTerms;
        private ConfigEntry<string> _armoredHumanoidTerms;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _logPatchWarnings;

        private readonly Dictionary<int, TargetClassification> _targetClassifications =
            new Dictionary<int, TargetClassification>();
        private readonly Dictionary<int, PendingDamageFeedback> _pendingDamageFeedback =
            new Dictionary<int, PendingDamageFeedback>();

        private string _cachedBoneUndeadTermsRaw;
        private string[] _cachedBoneUndeadTerms = new string[0];
        private string _cachedConstructTermsRaw;
        private string[] _cachedConstructTerms = new string[0];
        private string _cachedWyrdTermsRaw;
        private string[] _cachedWyrdTerms = new string[0];
        private string _cachedDrownedZombieTermsRaw;
        private string[] _cachedDrownedZombieTerms = new string[0];
        private string _cachedInfectedFleshTermsRaw;
        private string[] _cachedInfectedFleshTerms = new string[0];
        private string _cachedSeaFleshTermsRaw;
        private string[] _cachedSeaFleshTerms = new string[0];
        private string _cachedSpiritTermsRaw;
        private string[] _cachedSpiritTerms = new string[0];
        private string _cachedFloraTermsRaw;
        private string[] _cachedFloraTerms = new string[0];
        private string _cachedFleshUndeadTermsRaw;
        private string[] _cachedFleshUndeadTerms = new string[0];
        private string _cachedFleshTermsRaw;
        private string[] _cachedFleshTerms = new string[0];
        private string _cachedArmoredHumanoidTermsRaw;
        private string[] _cachedArmoredHumanoidTerms = new string[0];
        private int _targetTermsRevision = 1;
        private string _lastDamageNumberFontDiagnosticKey;
        private FontAsset _imguiDefaultFontAsset;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                BindConfig();
                _damageNumberRenderer = gameObject.AddComponent<DamageNumberRenderer>();
                _damageNumberRenderer.Initialize(this);
                CacheGameAccessors();
                if (!PatchGame())
                {
                    enabled = false;
                    return;
                }

                Log.LogInfo(PluginName + " " + PluginVersion + " loaded. Preset=" + _preset.Value + ".");
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

            _pendingDamageFeedback.Clear();
            if (_damageNumberRenderer != null)
            {
                Destroy(_damageNumberRenderer);
                _damageNumberRenderer = null;
            }

            if (_imguiDefaultFontAsset != null)
            {
                Destroy(_imguiDefaultFontAsset);
                _imguiDefaultFontAsset = null;
            }

            Instance = null;
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch.");
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. It changes only when an update requires fresh defaults.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _preset = Config.Bind("1. Core", "Preset", Preset.Hardened, "Damage-rule strength profile. Tempered is lighter, Hardened is the default, and Crucible makes every Steel and Bone rule matter more.");
            _respectVanillaMultipliers = Config.Bind("1. Core", "RespectVanillaMultipliers", true, "Skip Steel and Bone subtype overlays when the target already has a non-neutral vanilla multiplier for the same damage subtype.");
            _eliteRuleClampsEnabled = Config.Bind("1. Core", "EliteRuleClampsEnabled", true, "Reduce custom Steel and Bone weakness bonuses and floor custom resistances on elite-class targets.");
            _eliteWeaknessBonusReduction = Config.Bind("1. Core", "EliteWeaknessBonusReduction", 0.10f, "Flat reduction applied to custom Steel and Bone weakness bonuses on elite-class targets. 0.10 turns a 1.15 weakness into 1.05.");
            _eliteMinimumResistanceMultiplier = Config.Bind("1. Core", "EliteMinimumResistanceMultiplier", 0.20f, "Lowest custom Steel and Bone non-immunity resistance multiplier allowed on elite-class targets.");

            _amplifyVanillaMultipliers = Config.Bind("2. Vanilla Multipliers", "AmplifyVanillaMultipliers", true, "Amplify vanilla enemy weakness and resistance multipliers according to the Steel and Bone preset.");
            _temperedVanillaAmplification = Config.Bind("2. Vanilla Multipliers", "TemperedVanillaAmplification", 0.00f, "Extra distance from neutral applied to vanilla weakness and resistance multipliers on Tempered. 0 leaves vanilla unchanged.");
            _hardenedVanillaAmplification = Config.Bind("2. Vanilla Multipliers", "HardenedVanillaAmplification", 0.35f, "Extra distance from neutral applied to vanilla weakness and resistance multipliers on Hardened.");
            _crucibleVanillaAmplification = Config.Bind("2. Vanilla Multipliers", "CrucibleVanillaAmplification", 0.70f, "Extra distance from neutral applied to vanilla weakness and resistance multipliers on Crucible.");
            _minimumAmplifiedVanillaResistance = Config.Bind("2. Vanilla Multipliers", "MinimumAmplifiedVanillaResistance", 0.20f, "Lowest non-immune vanilla resistance multiplier Steel and Bone amplification can produce.");
            _maximumAmplifiedVanillaWeakness = Config.Bind("2. Vanilla Multipliers", "MaximumAmplifiedVanillaWeakness", 1.85f, "Highest vanilla weakness multiplier Steel and Bone amplification can produce.");

            _damageNumbersEnabled = Config.Bind("3. Feedback", "DamageNumbersEnabled", true, "Show Steel and Bone floating damage numbers for outgoing player hits.");
            _damageNumberBaseColor = Config.Bind("3. Feedback", "DamageNumberBaseColor", DefaultDamageNumberBaseColor, "Neutral outgoing damage-number color and baseline for resistance/weakness tinting. Use a hex color such as #E3BD02.");
            _damageNumberFontSize = Config.Bind("3. Feedback", "DamageNumberFontSize", 34, "Base floating damage-number font size.");
            _damageNumberFontMode = Config.Bind("3. Feedback", "DamageNumberFontMode", DamageNumberFontMode.GameDefault, "Font used by Steel and Bone damage numbers. GameDefault follows the game's Accessibility font choice, Sans forces the simple game font, Serif forces the stylized game font, and ImguiDefault keeps Unity's IMGUI fallback font.");
            _damageNumberDurationSeconds = Config.Bind("3. Feedback", "DamageNumberDurationSeconds", 0.85f, "Seconds a normal Steel and Bone damage number remains visible.");
            _damageNumberCriticalDurationSeconds = Config.Bind("3. Feedback", "DamageNumberCriticalDurationSeconds", 1.10f, "Seconds a critical Steel and Bone damage number remains visible.");
            _damageNumberHorizontalDrift = Config.Bind("3. Feedback", "DamageNumberHorizontalDrift", 1.0f, new ConfigDescription("Multiplier for floating damage-number left/right travel. 0 disables horizontal travel, 1 keeps the default motion, and values above 1 exaggerate it.", new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageNumberVerticalDrift = Config.Bind("3. Feedback", "DamageNumberVerticalDrift", 1.0f, new ConfigDescription("Multiplier for floating damage-number upward travel and curved settling. 0 disables vertical travel, 1 keeps the default motion, and values above 1 exaggerate it.", new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageOverTimeNumberHeightMultiplier = Config.Bind("3. Feedback", "DamageOverTimeNumberHeightMultiplier", 1.25f, new ConfigDescription("Multiplier for the initial world-space height of Bleed, Poison, Burn, and Breath status-tick damage numbers. 1 uses the ordinary damage-number height, while 1.25 starts damage-over-time numbers 25% higher.", new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageNumberSizeContrast = Config.Bind("3. Feedback", "DamageNumberSizeContrast", 1.0f, new ConfigDescription("Strength of the size difference between resisted, neutral, and weakness damage numbers. 0 uses neutral sizing, 1 keeps the default contrast, and values above 1 exaggerate it. Critical and weak-spot pop remain independent.", new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageNumberColorContrast = Config.Bind("3. Feedback", "DamageNumberColorContrast", 1.0f, new ConfigDescription("Strength of resistance grey and weakness red-orange tinting away from the neutral damage-number color. 0 keeps non-immune numbers neutral, 1 keeps the default contrast, and values above 1 reach the endpoint colors sooner.", new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageNumberMinimumAmount = Config.Bind("3. Feedback", "DamageNumberMinimumAmount", 0.10f, "Suppress non-immune damage numbers below this final damage amount.");
            _damageNumberMaximumActive = Config.Bind("3. Feedback", "DamageNumberMaximumActive", 36, "Maximum Steel and Bone floating damage numbers kept on screen at once.");

            _boneUndeadTerms = Config.Bind(
                "4. Target Families",
                "BoneUndeadTerms",
                "Skeleton;Skull;Bone;Animated Armor;JollySkeleton;Keeper Of The Barrow;KeeperOfTheBarrow",
                "Semicolon, comma, pipe, or newline separated target terms for skeleton, bone, or animated armor enemies.");
            _constructTerms = Config.Bind(
                "4. Target Families",
                "ConstructTerms",
                "Stone;Golem;Construct;Automaton;Statue;Crystal;Lost Knight;LostKnight;Forgeborn;ForgeBorn;Cairnguard;Tibby;Sentinel;Barnaclator",
                "Semicolon, comma, pipe, or newline separated target terms for stone, golem, or construct enemies.");
            _wyrdTerms = Config.Bind(
                "4. Target Families",
                "WyrdTerms",
                "Wyrdspawn;Wyrdspirit;Wyrd Spirit;WyrdSlime;Wyrd Slime;Wyrdness",
                "Semicolon, comma, pipe, or newline separated target terms for Wyrd enemies.");
            _drownedZombieTerms = Config.Bind(
                "4. Target Families",
                "DrownedZombieTerms",
                "Drowner;Drowned;Drowned Knight;Ghost Crew;Scourge",
                "Semicolon, comma, pipe, or newline separated target terms for drowned undead and corpse-sea enemies.");
            _infectedFleshTerms = Config.Bind(
                "4. Target Families",
                "InfectedFleshTerms",
                "Red Death;RedDeath;Infected",
                "Semicolon, comma, pipe, or newline separated target terms for Red Death and infected flesh enemies.");
            _seaFleshTerms = Config.Bind(
                "4. Target Families",
                "SeaFleshTerms",
                "Sarras;Finbled;Tadpole;Tidewraith;Scion;Archivist;Floatling;Reefback;Wailcap;Grindylow;Croakmaw",
                "Semicolon, comma, pipe, or newline separated target terms for sea creatures and Sarras aquatic enemies.");
            _spiritTerms = Config.Bind(
                "4. Target Families",
                "SpiritTerms",
                "Ghost;Spirit;Wraith;Banshee;Melancholy;Mistling;Mistbearer;Strawchild;Strawfather",
                "Semicolon, comma, pipe, or newline separated target terms for spirit, ghost, and mist enemies.");
            _floraTerms = Config.Bind(
                "4. Target Families",
                "FloraTerms",
                "Dryad;Gloomfrond;Fleshtree;Wailcap;Viridian",
                "Semicolon, comma, pipe, or newline separated target terms for plant and fungus enemies.");
            _fleshUndeadTerms = Config.Bind(
                "4. Target Families",
                "FleshUndeadTerms",
                "Zombie;Undead;Wight;Bloody;Frostbitten Warrior;Plaguewraith",
                "Semicolon, comma, pipe, or newline separated target terms for fleshy undead. Specific drowned and infected families win when also detected.");
            _fleshTerms = Config.Bind(
                "4. Target Families",
                "FleshTerms",
                "Bandit;Outlaw;Human;Humanoid;Remor;Redcap;Corpse Eater;Wolf;Bear",
                "Semicolon, comma, pipe, or newline separated target terms for ordinary flesh targets. Specific undead, sea, spirit, flora, construct, and armor families win when also detected.");
            _armoredHumanoidTerms = Config.Bind(
                "4. Target Families",
                "ArmoredHumanoidTerms",
                "Knight;Guard;Squire;Warrior;Deserter;Kamelot;Soldier;Armor;Armored",
                "Semicolon, comma, pipe, or newline separated target terms for armored humanoids. This high-specificity family can override broad flesh metadata.");

            _diagnostics = Config.Bind("5. Diagnostics", "Diagnostics", false, "Log damage-rule classification, vanilla multiplier checks, and multiplier decisions.");
            _logPatchWarnings = Config.Bind("5. Diagnostics", "LogPatchWarnings", true, "Log warnings when required game methods cannot be patched.");

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
                    Log.LogError("Failed to restore Steel and Bone config backup after schema reset failure: " + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException("Failed to reset Steel and Bone config schema. Original config was left in place when possible.", ex);
            }
        }

        private void CacheGameAccessors()
        {
            Type heroType = AccessTools.TypeByName(HeroTypeName);
            if (heroType != null)
            {
                _heroCurrentGetter = AccessTools.PropertyGetter(heroType, "Current");
            }

            _damageSubTypeType = AccessTools.TypeByName(DamageSubTypeTypeName);
            Type multiplierDataBaseType = AccessTools.TypeByName(DamageReceivedMultiplierDataBaseTypeName);
            if (multiplierDataBaseType != null)
            {
                _getMultiplierForSubtypeMethod = AccessTools.Method(multiplierDataBaseType, "GetMultiplierForSubtype");
            }
        }

        private bool PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type healthElementType = AccessTools.TypeByName(HealthElementTypeName);
            if (healthElementType == null)
            {
                Warn("Could not find " + HealthElementTypeName + ". " + PluginName + " is inactive.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Mod inactive; check BepInEx log.");
                return false;
            }

            MethodInfo original = AccessTools.Method(healthElementType, "ApplyDamageModifiers");
            MethodInfo postfix = AccessTools.Method(
                typeof(ApplyDamageModifiersPatch),
                nameof(ApplyDamageModifiersPatch.Postfix));
            if (original == null || postfix == null)
            {
                Warn("Could not patch HealthElement.ApplyDamageModifiers. " + PluginName + " is inactive.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Mod inactive; check BepInEx log.");
                return false;
            }

            _harmony.Patch(original, null, new HarmonyMethod(postfix));
            LogDiagnostic("Patched " + healthElementType.FullName + ".ApplyDamageModifiers.");

            MethodInfo outcomeOriginal = AccessTools.Method(healthElementType, "AfterHealthDecreaseEvents");
            MethodInfo outcomePostfix = AccessTools.Method(
                typeof(AfterHealthDecreaseEventsPatch),
                nameof(AfterHealthDecreaseEventsPatch.Postfix));
            if (outcomeOriginal == null || outcomePostfix == null)
            {
                Warn("Could not patch HealthElement.AfterHealthDecreaseEvents. Steel and Bone damage numbers are unavailable, but damage rules remain active.");
                return true;
            }

            _harmony.Patch(outcomeOriginal, null, new HarmonyMethod(outcomePostfix));
            LogDiagnostic("Patched " + healthElementType.FullName + ".AfterHealthDecreaseEvents.");
            return true;
        }

        internal void ApplyDamageRuleModifier(object healthElement, object damage, ref float damageModifier)
        {
            if (_enabled == null || !_enabled.Value || healthElement == null || damage == null)
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

            object target = ResolveDamageTargetOwner(healthElement, damage);
            if (target != null && IsSameModelOrOwner(target, hero))
            {
                return;
            }

            TargetClassification targetClass = GetTargetClassification(target, healthElement);
            DamageClassification damageClass = ClassifyDamage(damage);
            LogDamageCheckDiagnostic(target ?? healthElement, damage, targetClass, damageClass);

            VanillaMultiplierAmplification vanillaAmplification;
            bool appliedVanillaAmplification =
                TryApplyVanillaMultiplierAmplification(damage, damageClass, ref damageModifier, out vanillaAmplification);

            DamageRuleMatch match;
            bool skippedForVanilla;
            bool skippedForEliteClamp;
            bool matchedRule = TryResolveDamageRule(
                targetClass,
                damageClass,
                damage,
                out match,
                out skippedForVanilla,
                out skippedForEliteClamp);
            if (!matchedRule && !appliedVanillaAmplification)
            {
                LogNoRuleDiagnostic(target ?? healthElement, targetClass, damageClass, skippedForVanilla, skippedForEliteClamp);
                return;
            }

            if (!matchedRule)
            {
                LogNoRuleDiagnostic(target ?? healthElement, targetClass, damageClass, skippedForVanilla, skippedForEliteClamp);
                RememberDamageFeedback(
                    damage,
                    vanillaAmplification.AmplifiedMultiplier,
                    "Vanilla",
                    vanillaAmplification.SubtypeName);
                return;
            }

            float multiplier = Clamp(match.Multiplier, 0.05f, 2.0f);
            float before = damageModifier;
            damageModifier *= multiplier;
            if (multiplier <= 0.0001f)
            {
                damageModifier = 0.0f;
            }

            float feedbackMultiplier = multiplier;
            if (appliedVanillaAmplification)
            {
                feedbackMultiplier *= vanillaAmplification.AmplifiedMultiplier;
            }

            RememberDamageFeedback(damage, feedbackMultiplier, match.TargetLabel, match.DamageLabel);
            if (DiagnosticsEnabled())
            {
                LogDiagnostic(
                    "Applied damage rule: target="
                    + DescribeObject(target ?? healthElement)
                    + ", detectedFamilies="
                    + DescribeTargetFamilies(targetClass)
                    + ", targetFlags="
                    + DescribeTargetFlags(targetClass)
                    + ", detectedDamageTags="
                    + DescribeDamageTags(damageClass)
                    + ", family="
                    + match.TargetLabel
                    + ", damage="
                    + match.DamageLabel
                    + ", preset="
                    + _preset.Value
                    + ", multiplier="
                    + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + (match.WasEliteClamped
                        ? ", eliteClamp="
                            + match.PresetMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                            + " -> "
                            + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                        : "")
                    + ", damageModifier "
                    + before.ToString("0.###", CultureInfo.InvariantCulture)
                    + " -> "
                    + damageModifier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private bool TryApplyVanillaMultiplierAmplification(
            object damage,
            DamageClassification damageClass,
            ref float damageModifier,
            out VanillaMultiplierAmplification amplification)
        {
            amplification = default(VanillaMultiplierAmplification);
            if (_amplifyVanillaMultipliers == null
                || !_amplifyVanillaMultipliers.Value
                || damage == null
                || damageClass == null)
            {
                return false;
            }

            float strength = GetVanillaAmplificationStrength();
            if (strength <= 0.0001f)
            {
                return false;
            }

            if (!TryBuildVanillaMultiplierAmplification(damage, damageClass, strength, out amplification))
            {
                return false;
            }

            if (!HasMeaningfulEffect(amplification.AdjustmentMultiplier))
            {
                amplification = default(VanillaMultiplierAmplification);
                return false;
            }

            float before = damageModifier;
            damageModifier *= amplification.AdjustmentMultiplier;

            if (DiagnosticsEnabled())
            {
                LogDiagnostic(
                    "Amplified vanilla multiplier: subtypes="
                    + amplification.SubtypeName
                    + ", preset="
                    + (_preset == null ? Preset.Hardened : _preset.Value).ToString()
                    + ", vanillaMultiplier="
                    + amplification.NativeMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", amplifiedMultiplier="
                    + amplification.AmplifiedMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", adjustment="
                    + amplification.AdjustmentMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", damageModifier "
                    + before.ToString("0.###", CultureInfo.InvariantCulture)
                    + " -> "
                    + damageModifier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }

            return true;
        }

        private bool TryBuildVanillaMultiplierAmplification(
            object damage,
            DamageClassification damageClass,
            float strength,
            out VanillaMultiplierAmplification amplification)
        {
            amplification = default(VanillaMultiplierAmplification);
            if (damage == null || damageClass == null)
            {
                return false;
            }

            StringBuilder subtypeBuilder = new StringBuilder();
            float nativeProduct = 1.0f;
            float amplifiedProduct = 1.0f;
            float adjustmentProduct = 1.0f;
            bool found = false;

            for (int i = 0; i < NativeSubtypeChecks.Length; i++)
            {
                NativeSubtypeCheck check = NativeSubtypeChecks[i];
                if (!damageClass.HasAny(check.Tag) || !DamageHasSubtype(damage, check.SubtypeName))
                {
                    continue;
                }

                float multiplier;
                if (!TryGetNativeDamageMultiplier(damage, check.SubtypeName, out multiplier) || !HasMeaningfulEffect(multiplier))
                {
                    continue;
                }

                if (multiplier <= 0.0001f)
                {
                    continue;
                }

                float amplifiedMultiplier = AmplifyVanillaMultiplier(multiplier, strength);
                if (!HasMeaningfulEffect(amplifiedMultiplier)
                    || Math.Abs(amplifiedMultiplier - multiplier) <= 0.001f)
                {
                    continue;
                }

                if (subtypeBuilder.Length > 0)
                {
                    subtypeBuilder.Append("|");
                }

                subtypeBuilder.Append(check.SubtypeName);
                nativeProduct *= multiplier;
                amplifiedProduct *= amplifiedMultiplier;
                adjustmentProduct *= amplifiedMultiplier / multiplier;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            amplification = new VanillaMultiplierAmplification(
                subtypeBuilder.ToString(),
                nativeProduct,
                amplifiedProduct,
                adjustmentProduct);
            return true;
        }

        private float AmplifyVanillaMultiplier(float nativeMultiplier, float strength)
        {
            float amplified = 1.0f + ((nativeMultiplier - 1.0f) * (1.0f + strength));
            if (nativeMultiplier < 1.0f)
            {
                return Clamp(amplified, GetMinimumAmplifiedVanillaResistance(), 0.999f);
            }

            return Clamp(amplified, 1.001f, GetMaximumAmplifiedVanillaWeakness());
        }

        private float GetVanillaAmplificationStrength()
        {
            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float value;
            switch (preset)
            {
                case Preset.Tempered:
                    value = _temperedVanillaAmplification == null ? 0.0f : _temperedVanillaAmplification.Value;
                    break;
                case Preset.Crucible:
                    value = _crucibleVanillaAmplification == null ? 0.70f : _crucibleVanillaAmplification.Value;
                    break;
                case Preset.Hardened:
                default:
                    value = _hardenedVanillaAmplification == null ? 0.35f : _hardenedVanillaAmplification.Value;
                    break;
            }

            return Clamp(value, 0.0f, 2.0f);
        }

        private float GetMinimumAmplifiedVanillaResistance()
        {
            float value = _minimumAmplifiedVanillaResistance == null ? 0.20f : _minimumAmplifiedVanillaResistance.Value;
            return Clamp(value, 0.01f, 0.95f);
        }

        private float GetMaximumAmplifiedVanillaWeakness()
        {
            float value = _maximumAmplifiedVanillaWeakness == null ? 1.85f : _maximumAmplifiedVanillaWeakness.Value;
            return Clamp(value, 1.05f, 3.00f);
        }

        private bool EliteRuleClampsEnabled()
        {
            return _eliteRuleClampsEnabled == null || _eliteRuleClampsEnabled.Value;
        }

        private float GetEliteWeaknessBonusReduction()
        {
            float value = _eliteWeaknessBonusReduction == null ? 0.10f : _eliteWeaknessBonusReduction.Value;
            return Clamp(value, 0.0f, 0.50f);
        }

        private float GetEliteMinimumResistanceMultiplier()
        {
            float value = _eliteMinimumResistanceMultiplier == null ? 0.20f : _eliteMinimumResistanceMultiplier.Value;
            return Clamp(value, 0.05f, 0.95f);
        }

        private float ApplyEliteRuleClamp(float multiplier, TargetClassification targetClass)
        {
            if (!EliteRuleClampsEnabled()
                || targetClass == null
                || !targetClass.IsEliteClass
                || !HasMeaningfulEffect(multiplier))
            {
                return multiplier;
            }

            if (multiplier > 1.0f)
            {
                float bonus = multiplier - 1.0f;
                float reducedBonus = Math.Max(0.0f, bonus - GetEliteWeaknessBonusReduction());
                return Clamp(1.0f + reducedBonus, 1.0f, 2.0f);
            }

            return Clamp(Math.Max(multiplier, GetEliteMinimumResistanceMultiplier()), 0.05f, 1.0f);
        }

        private bool TryResolveDamageRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            out DamageRuleMatch match,
            out bool skippedForVanilla,
            out bool skippedForEliteClamp)
        {
            match = default(DamageRuleMatch);
            bool hasMatch = false;
            skippedForVanilla = false;
            skippedForEliteClamp = false;

            if (targetClass == null || damageClass == null)
            {
                return false;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            for (int i = 0; i < DamageRules.Length; i++)
            {
                DamageRule rule = DamageRules[i];
                if (rule.TargetFamily == TargetFamily.FleshUndead
                    && targetClass.IsInfectedFlesh
                    && rule.MatchesDamageTag(
                        DamageTag.GenericPhysical
                        | DamageTag.Slashing
                        | DamageTag.Piercing
                        | DamageTag.Bludgeoning))
                {
                    continue;
                }
                if (!TargetMatchesRule(targetClass, rule.TargetFamily) || !damageClass.HasAny(rule.DamageTags))
                {
                    continue;
                }

                string vanillaSubtype;
                float vanillaMultiplier;
                if (ShouldSkipForVanillaMultiplier(rule, damageClass, damage, out vanillaSubtype, out vanillaMultiplier))
                {
                    skippedForVanilla = true;
                    if (DiagnosticsEnabled())
                    {
                        LogDiagnostic(
                            "Skipped Steel and Bone rule because vanilla already modifies "
                            + vanillaSubtype
                            + ": detectedFamilies="
                            + DescribeTargetFamilies(targetClass)
                            + ", targetFlags="
                            + DescribeTargetFlags(targetClass)
                            + ", detectedDamageTags="
                            + DescribeDamageTags(damageClass)
                            + ", targetFamily="
                            + rule.TargetLabel
                            + ", damage="
                            + rule.DamageLabel
                            + ", vanillaMultiplier="
                            + vanillaMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                            + ".");
                    }
                    continue;
                }

                float ruleMultiplier = ApplyPresetIntensity(rule.BaseMultiplier, preset);
                float presetMultiplier = ruleMultiplier;
                ruleMultiplier = ApplyEliteRuleClamp(ruleMultiplier, targetClass);
                if (!HasMeaningfulEffect(ruleMultiplier))
                {
                    if (Math.Abs(presetMultiplier - ruleMultiplier) > 0.001f)
                    {
                        skippedForEliteClamp = true;
                    }
                    continue;
                }

                DamageRuleMatch candidate = new DamageRuleMatch(
                    ruleMultiplier,
                    rule.TargetLabel,
                    rule.DamageLabel,
                    rule.Priority,
                    GetRuleImpact(ruleMultiplier),
                    presetMultiplier,
                    Math.Abs(presetMultiplier - ruleMultiplier) > 0.001f);

                if (!hasMatch
                    || candidate.Priority > match.Priority
                    || (candidate.Priority == match.Priority && candidate.Impact > match.Impact))
                {
                    match = candidate;
                    hasMatch = true;
                }
            }

            return hasMatch;
        }

        private bool ShouldSkipForVanillaMultiplier(
            DamageRule rule,
            DamageClassification damageClass,
            object damage,
            out string subtypeName,
            out float nativeMultiplier)
        {
            subtypeName = "";
            nativeMultiplier = 1.0f;

            if (_respectVanillaMultipliers == null
                || !_respectVanillaMultipliers.Value
                || damageClass == null
                || damage == null)
            {
                return false;
            }

            for (int i = 0; i < NativeSubtypeChecks.Length; i++)
            {
                NativeSubtypeCheck check = NativeSubtypeChecks[i];
                if (!rule.MatchesDamageTag(check.Tag)
                    || !damageClass.HasAny(check.Tag)
                    || !DamageHasSubtype(damage, check.SubtypeName))
                {
                    continue;
                }

                float multiplier;
                if (TryGetNativeDamageMultiplier(damage, check.SubtypeName, out multiplier) && HasMeaningfulEffect(multiplier))
                {
                    subtypeName = check.SubtypeName;
                    nativeMultiplier = multiplier;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetNativeDamageMultiplier(object damage, string subtypeName, out float multiplier)
        {
            multiplier = 1.0f;

            if (_damageSubTypeType == null || _getMultiplierForSubtypeMethod == null || string.IsNullOrEmpty(subtypeName))
            {
                return false;
            }

            object multiplierData = GetOptionalPropertyValue(damage, "DamageReceivedMultiplierData");
            if (multiplierData == null)
            {
                return false;
            }

            try
            {
                object subtype = Enum.Parse(_damageSubTypeType, subtypeName);
                object raw = _getMultiplierForSubtypeMethod.Invoke(multiplierData, new[] { subtype });
                if (raw is float)
                {
                    multiplier = (float)raw;
                    return true;
                }
                if (raw is double)
                {
                    multiplier = (float)(double)raw;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private float ApplyPresetIntensity(float baseMultiplier, Preset preset)
        {
            float strength = GetPresetIntensity(preset);
            float scaled = 1.0f + ((baseMultiplier - 1.0f) * strength);
            return Clamp(scaled, 0.05f, 2.0f);
        }

        private float GetPresetIntensity(Preset preset)
        {
            switch (preset)
            {
                case Preset.Tempered:
                    return 0.55f;
                case Preset.Crucible:
                    return 1.35f;
                case Preset.Hardened:
                default:
                    return 1.0f;
            }
        }

        private bool HasMeaningfulEffect(float multiplier)
        {
            return GetRuleImpact(multiplier) > 0.001f;
        }

        private float GetRuleImpact(float multiplier)
        {
            return Math.Abs(multiplier - 1.0f);
        }

        private bool TargetMatchesRule(TargetClassification targetClass, TargetFamily family)
        {
            switch (family)
            {
                case TargetFamily.BoneUndead:
                    return targetClass.IsBoneUndead;
                case TargetFamily.Construct:
                    return targetClass.IsConstruct;
                case TargetFamily.ArmoredHumanoid:
                    return targetClass.IsArmoredHumanoid;
                case TargetFamily.Flesh:
                    return targetClass.IsFlesh;
                case TargetFamily.FleshUndead:
                    return targetClass.IsFleshUndead;
                case TargetFamily.Wyrd:
                    return targetClass.IsWyrd;
                case TargetFamily.DrownedZombie:
                    return targetClass.IsDrownedZombie;
                case TargetFamily.InfectedFlesh:
                    return targetClass.IsInfectedFlesh;
                case TargetFamily.SeaFlesh:
                    return targetClass.IsSeaFlesh;
                case TargetFamily.Spirit:
                    return targetClass.IsSpirit;
                case TargetFamily.Flora:
                    return targetClass.IsFlora;
                default:
                    return false;
            }
        }

        private TargetClassification GetTargetClassification(object target, object healthElement)
        {
            object key = target ?? healthElement;
            if (key == null)
            {
                return TargetClassification.Empty;
            }

            int cacheKey = RuntimeHelpers.GetHashCode(key);
            TargetClassification cached;
            if (_targetClassifications.TryGetValue(cacheKey, out cached)
                && cached.Revision == _targetTermsRevision
                && ReferenceEquals(cached.Key, key))
            {
                return cached;
            }

            string text = BuildObjectSearchText(target);
            if (healthElement != null && !ReferenceEquals(healthElement, target))
            {
                text = text + " " + BuildObjectSearchText(healthElement);
            }

            TargetClassification classification = new TargetClassification
            {
                Key = key,
                Revision = _targetTermsRevision
            };

            string metadataText = BuildTargetMetadataSearchText(target, healthElement);
            ApplyMetadataTargetClassification(classification, metadataText);
            if (!classification.HasMetadataFamily())
            {
                ApplyTermTargetClassification(classification, text);
            }
            else
            {
                ApplyBroadFamilyTermOverrides(classification, text);
                ApplySpecificTermTargetClassification(classification, text);
            }

            _targetClassifications[cacheKey] = classification;
            return classification;
        }

        private void ApplyMetadataTargetClassification(TargetClassification classification, string metadataText)
        {
            if (classification == null || string.IsNullOrEmpty(metadataText))
            {
                return;
            }

            if (ContainsAnyTerm(metadataText, MetadataBoneUndeadTerms))
            {
                classification.IsBoneUndead = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:BoneUndead");
            }
            if (ContainsAnyTerm(metadataText, MetadataConstructTerms))
            {
                classification.IsConstruct = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Construct");
            }
            if (ContainsAnyTerm(metadataText, MetadataWyrdTerms))
            {
                classification.IsWyrd = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Wyrd");
            }
            if (ContainsAnyTerm(metadataText, MetadataDrownedZombieTerms))
            {
                classification.IsDrownedZombie = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:DrownedZombie");
            }
            if (ContainsAnyTerm(metadataText, MetadataSeaFleshTerms))
            {
                classification.IsSeaFlesh = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:SeaFlesh");
            }
            if (ContainsAnyTerm(metadataText, MetadataSpiritTerms))
            {
                classification.IsSpirit = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Spirit");
            }
            if (ContainsAnyTerm(metadataText, MetadataFloraTerms))
            {
                classification.IsFlora = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Flora");
            }
            if (!classification.HasAnyFamily() && ContainsAnyTerm(metadataText, MetadataFleshUndeadTerms))
            {
                classification.IsFleshUndead = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:FleshUndead");
            }
            if (!classification.HasAnyFamily() && ContainsAnyTerm(metadataText, MetadataFleshTerms))
            {
                classification.IsFlesh = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Flesh");
            }
            if (ContainsAnyTerm(metadataText, MetadataEliteTerms))
            {
                classification.IsEliteClass = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:EliteClass");
            }
        }

        private void ApplyTermTargetClassification(TargetClassification classification, string text)
        {
            if (classification == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (ContainsAnyTerm(text, GetBoneUndeadTerms()))
            {
                classification.IsBoneUndead = true;
                AppendClassificationEvidence(classification, "terms:BoneUndead");
            }
            if (ContainsAnyTerm(text, GetConstructTerms()))
            {
                classification.IsConstruct = true;
                AppendClassificationEvidence(classification, "terms:Construct");
            }
            if (ContainsAnyTerm(text, GetWyrdTerms()))
            {
                classification.IsWyrd = true;
                AppendClassificationEvidence(classification, "terms:Wyrd");
            }
            if (ContainsAnyTerm(text, GetDrownedZombieTerms()))
            {
                classification.IsDrownedZombie = true;
                AppendClassificationEvidence(classification, "terms:DrownedZombie");
            }
            if (ContainsAnyTerm(text, GetInfectedFleshTerms()))
            {
                classification.IsInfectedFlesh = true;
                AppendClassificationEvidence(classification, "terms:InfectedFlesh");
            }
            if (ContainsAnyTerm(text, GetSeaFleshTerms()))
            {
                classification.IsSeaFlesh = true;
                AppendClassificationEvidence(classification, "terms:SeaFlesh");
            }
            if (ContainsAnyTerm(text, GetSpiritTerms()))
            {
                classification.IsSpirit = true;
                AppendClassificationEvidence(classification, "terms:Spirit");
            }
            if (ContainsAnyTerm(text, GetFloraTerms()))
            {
                classification.IsFlora = true;
                AppendClassificationEvidence(classification, "terms:Flora");
            }
            if (!classification.HasAnyFamily() && ContainsAnyTerm(text, GetFleshUndeadTerms()))
            {
                classification.IsFleshUndead = true;
                AppendClassificationEvidence(classification, "terms:FleshUndead");
            }
            ApplySpecificTermTargetClassification(classification, text);
            if (!classification.HasAnyFamily() && ContainsAnyTerm(text, GetFleshTerms()))
            {
                classification.IsFlesh = true;
                AppendClassificationEvidence(classification, "terms:Flesh");
            }
        }

        private void ApplyBroadFamilyTermOverrides(TargetClassification classification, string text)
        {
            if (classification == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            if (!classification.IsFlesh && !classification.IsFleshUndead)
            {
                return;
            }

            if (ContainsAnyTerm(text, GetBoneUndeadTerms()))
            {
                classification.IsBoneUndead = true;
                AppendClassificationEvidence(classification, "terms:BoneUndead");
            }
            if (ContainsAnyTerm(text, GetConstructTerms()))
            {
                classification.IsConstruct = true;
                AppendClassificationEvidence(classification, "terms:Construct");
            }
            if (ContainsAnyTerm(text, GetWyrdTerms()))
            {
                classification.IsWyrd = true;
                AppendClassificationEvidence(classification, "terms:Wyrd");
            }
            if (ContainsAnyTerm(text, GetDrownedZombieTerms()))
            {
                classification.IsDrownedZombie = true;
                AppendClassificationEvidence(classification, "terms:DrownedZombie");
            }
            if (ContainsAnyTerm(text, GetInfectedFleshTerms()))
            {
                classification.IsInfectedFlesh = true;
                AppendClassificationEvidence(classification, "terms:InfectedFlesh");
            }
            if (ContainsAnyTerm(text, GetSeaFleshTerms()))
            {
                classification.IsSeaFlesh = true;
                AppendClassificationEvidence(classification, "terms:SeaFlesh");
            }
            if (ContainsAnyTerm(text, GetSpiritTerms()))
            {
                classification.IsSpirit = true;
                AppendClassificationEvidence(classification, "terms:Spirit");
            }
            if (ContainsAnyTerm(text, GetFloraTerms()))
            {
                classification.IsFlora = true;
                AppendClassificationEvidence(classification, "terms:Flora");
            }
        }

        private void ApplySpecificTermTargetClassification(TargetClassification classification, string text)
        {
            if (classification == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (classification.HasAnyFamily() && !classification.IsFlesh && !classification.IsFleshUndead)
            {
                return;
            }

            if (ContainsAnyTerm(text, GetArmoredHumanoidTerms()))
            {
                classification.IsArmoredHumanoid = true;
                AppendClassificationEvidence(classification, "terms:ArmoredHumanoid");
            }
        }

        private void AppendClassificationEvidence(TargetClassification classification, string evidence)
        {
            if (classification == null || string.IsNullOrEmpty(evidence))
            {
                return;
            }

            if (classification.Evidence == null)
            {
                classification.Evidence = evidence;
                return;
            }

            if (classification.Evidence.IndexOf(evidence, StringComparison.OrdinalIgnoreCase) < 0)
            {
                classification.Evidence = classification.Evidence + "|" + evidence;
            }
        }

        private DamageClassification ClassifyDamage(object damage)
        {
            if (damage == null)
            {
                return DamageClassification.Empty;
            }

            DamageClassification classification = new DamageClassification();
            string damageSearchText = BuildDamageSearchText(damage).ToLowerInvariant();
            classification.IsBleed = ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Bleed")
                || TextContainsAny(damageSearchText, BleedTerms);
            classification.IsPoison = DamageHasSubtype(damage, "Poison")
                || ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Poison")
                || TextContainsAny(damageSearchText, PoisonTerms);
            classification.IsWyrdness = DamageHasSubtype(damage, "Wyrdness")
                || TextContainsAny(damageSearchText, WyrdTerms);
            classification.IsBloodMagic = TextContainsAny(damageSearchText, BloodMagicTerms);
            classification.IsSlashing = DamageHasSubtype(damage, "Slashing");
            classification.IsPiercing = DamageHasSubtype(damage, "Piercing");
            classification.IsBludgeoning = DamageHasSubtype(damage, "Bludgeoning");
            classification.IsGenericPhysical = DamageHasSubtype(damage, "GenericPhysical");
            if (!classification.HasSpecificPhysicalType()
                && (classification.IsGenericPhysical || ValueNameContains(GetOptionalPropertyValue(damage, "Type"), "PhysicalHitSource")))
            {
                AddPhysicalWeaponTypeHints(damage, classification);
            }
            classification.IsGenericMagical = DamageHasSubtype(damage, "GenericMagical");
            classification.IsBurn = ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Burn");
            classification.IsFire = DamageHasSubtype(damage, "Fire") || classification.IsBurn;
            classification.IsCold = DamageHasSubtype(damage, "Cold");
            classification.IsElectric = DamageHasSubtype(damage, "Electric");
            classification.IsWet = DamageHasSubtype(damage, "Wet");

            if (classification.IsBloodMagic)
            {
                classification.Tags |= DamageTag.BloodMagic;
            }
            if (classification.IsBleed)
            {
                classification.Tags |= DamageTag.Bleed;
            }
            if (classification.IsPoison)
            {
                classification.Tags |= DamageTag.Poison;
            }
            if (classification.IsWyrdness)
            {
                classification.Tags |= DamageTag.Wyrdness;
            }
            if (classification.IsSlashing)
            {
                classification.Tags |= DamageTag.Slashing;
            }
            if (classification.IsPiercing)
            {
                classification.Tags |= DamageTag.Piercing;
            }
            if (classification.IsBludgeoning)
            {
                classification.Tags |= DamageTag.Bludgeoning;
            }
            if (classification.IsGenericPhysical)
            {
                classification.Tags |= DamageTag.GenericPhysical;
            }
            if (classification.IsGenericMagical)
            {
                classification.Tags |= DamageTag.GenericMagical;
            }
            if (classification.IsFire)
            {
                classification.Tags |= DamageTag.Fire;
            }
            if (classification.IsCold)
            {
                classification.Tags |= DamageTag.Cold;
            }
            if (classification.IsElectric)
            {
                classification.Tags |= DamageTag.Electric;
            }
            if (classification.IsWet)
            {
                classification.Tags |= DamageTag.Wet;
            }
            if (classification.IsBurn)
            {
                classification.Tags |= DamageTag.Burn;
            }

            return classification;
        }

        private bool IsDamageOverTime(object damage)
        {
            object statusDamageType = GetOptionalPropertyValue(damage, "StatusDamageType");
            return ValueNameContains(statusDamageType, "Bleed")
                || ValueNameContains(statusDamageType, "Poison")
                || ValueNameContains(statusDamageType, "Burn")
                || ValueNameContains(statusDamageType, "Breath");
        }

        private bool DamageHasSubtype(object damage, string subtypeName)
        {
            return EnumerablePartsContainName(GetOptionalPropertyValue(damage, "SubTypes"), "SubType", subtypeName)
                || EnumerablePartsContainName(GetOptionalPropertyValue(GetOptionalPropertyValue(damage, "DamageTypeData"), "OriginalParts"), "SubType", subtypeName);
        }

        private void AddPhysicalWeaponTypeHints(object damage, DamageClassification classification)
        {
            if (damage == null || classification == null)
            {
                return;
            }

            if (TryApplyPhysicalWeaponTypeHint(GetOptionalPropertyValue(damage, "Item"), classification))
            {
                return;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            if (TryApplyPhysicalWeaponTypeHint(GetOptionalPropertyValue(projectile, "SourceWeapon"), classification))
            {
                return;
            }

            TryApplyPhysicalWeaponTypeHint(GetOptionalPropertyValue(projectile, "SourceProjectile"), classification);
        }

        private bool TryApplyPhysicalWeaponTypeHint(object candidate, DamageClassification classification)
        {
            if (candidate == null || classification == null || IsDestroyedUnityObject(candidate))
            {
                return false;
            }

            if (TryApplyPhysicalWeaponTypeHintFromSingleObject(candidate, classification))
            {
                return true;
            }

            object item = GetOptionalPropertyValue(candidate, "Item");
            if (item != null && !ReferenceEquals(item, candidate) && TryApplyPhysicalWeaponTypeHintFromSingleObject(item, classification))
            {
                return true;
            }

            object parent = GetOptionalPropertyValue(candidate, "ParentModel");
            if (parent != null && !ReferenceEquals(parent, candidate) && TryApplyPhysicalWeaponTypeHintFromSingleObject(parent, classification))
            {
                return true;
            }

            object template = GetOptionalPropertyValue(candidate, "Template");
            return template != null
                && !ReferenceEquals(template, candidate)
                && TryApplyPhysicalWeaponTypeHintFromSingleObject(template, classification);
        }

        private bool TryApplyPhysicalWeaponTypeHintFromSingleObject(object candidate, DamageClassification classification)
        {
            if (candidate == null || classification == null || IsDestroyedUnityObject(candidate))
            {
                return false;
            }

            if (GetOptionalBoolProperty(candidate, "IsBlunt"))
            {
                classification.IsBludgeoning = true;
                classification.PhysicalTypeHint = "Bludgeoning from " + DescribeObject(candidate);
                return true;
            }

            if (GetOptionalBoolProperty(candidate, "IsPolearm")
                || GetOptionalBoolProperty(candidate, "IsDagger")
                || GetOptionalBoolProperty(candidate, "IsRanged")
                || GetOptionalBoolProperty(candidate, "IsArrow"))
            {
                classification.IsPiercing = true;
                classification.PhysicalTypeHint = "Piercing from " + DescribeObject(candidate);
                return true;
            }

            if (GetOptionalBoolProperty(candidate, "IsSword") || GetOptionalBoolProperty(candidate, "IsAxe"))
            {
                classification.IsSlashing = true;
                classification.PhysicalTypeHint = "Slashing from " + DescribeObject(candidate);
                return true;
            }

            return false;
        }

        private void LogDamageCheckDiagnostic(
            object target,
            object damage,
            TargetClassification targetClass,
            DamageClassification damageClass)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            object item = GetOptionalPropertyValue(damage, "Item");
            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            string physicalHint = string.IsNullOrEmpty(damageClass.PhysicalTypeHint)
                ? ""
                : ", physicalHint=" + damageClass.PhysicalTypeHint;
            string targetEvidence = targetClass == null || string.IsNullOrEmpty(targetClass.Evidence)
                ? ""
                : ", familyEvidence=" + targetClass.Evidence;
            string targetFlags = DescribeTargetFlags(targetClass);

            LogDiagnostic(
                "Damage check: target="
                + DescribeObject(target)
                + ", families="
                + DescribeTargetFamilies(targetClass)
                + targetEvidence
                + ", targetFlags="
                + targetFlags
                + ", damageType="
                + DescribeValue(GetOptionalPropertyValue(damage, "Type"))
                + ", statusDamageType="
                + DescribeValue(GetOptionalPropertyValue(damage, "StatusDamageType"))
                + ", item="
                + DescribeObject(item)
                + ", projectile="
                + DescribeObject(projectile)
                + ", damageTags="
                + DescribeDamageTags(damageClass)
                + physicalHint
                + ".");
        }

        private void LogNoRuleDiagnostic(
            object target,
            TargetClassification targetClass,
            DamageClassification damageClass,
            bool skippedForVanilla,
            bool skippedForEliteClamp)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            LogDiagnostic(
                "No Steel and Bone rule matched: target="
                + DescribeObject(target)
                + ", families="
                + DescribeTargetFamilies(targetClass)
                + ", targetFlags="
                + DescribeTargetFlags(targetClass)
                + ", damageTags="
                + DescribeDamageTags(damageClass)
                + ", reason="
                + GetNoRuleReason(targetClass, damageClass, skippedForVanilla, skippedForEliteClamp)
                + ".");
        }

        private string GetNoRuleReason(
            TargetClassification targetClass,
            DamageClassification damageClass,
            bool skippedForVanilla,
            bool skippedForEliteClamp)
        {
            if (skippedForVanilla)
            {
                return "vanilla already handled matching subtype";
            }

            if (skippedForEliteClamp)
            {
                return "elite clamp neutralized custom rule";
            }

            if (targetClass == null || !targetClass.HasAnyFamily())
            {
                return "no target family";
            }

            if (damageClass == null || damageClass.Tags == DamageTag.None)
            {
                return "no damage tags";
            }

            return "no family/tag rule";
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

        private bool TextContainsAny(string text, string[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                string term = terms[i];
                if (!string.IsNullOrEmpty(term) && text.Contains(term.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildDamageSearchText(object damage)
        {
            if (damage == null)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(DescribeObject(damage)).Append(' ');
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Item"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "BlockingItem"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Skill"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Type"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "StatusDamageType"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "DamageTypeData"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Parameters"));

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            AppendObjectSearchText(builder, projectile);
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "SourceWeapon"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "SourceProjectile"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "Skill"));

            return builder.ToString();
        }

        private void AppendObjectSearchText(StringBuilder builder, object obj)
        {
            if (builder == null || obj == null || IsDestroyedUnityObject(obj))
            {
                return;
            }

            builder.Append(BuildObjectSearchText(obj)).Append(' ');
            builder.Append(DescribeObject(obj)).Append(' ');
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
            AppendMemberSearchText(builder, obj, "SurfaceType");
            AppendMemberSearchText(builder, obj, "surfaceType");
            AppendMemberSearchText(builder, obj, "NpcType");
            AppendMemberSearchText(builder, obj, "npcType");
            AppendMemberSearchText(builder, obj, "Tags");
            AppendMemberSearchText(builder, obj, "tags");
            AppendMemberSearchText(builder, obj, "AbstractTypes");
            AppendMemberSearchText(builder, obj, "_abstractTypes");

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
                AppendMemberSearchText(builder, template, "SurfaceType");
                AppendMemberSearchText(builder, template, "surfaceType");
                AppendMemberSearchText(builder, template, "NpcType");
                AppendMemberSearchText(builder, template, "npcType");
                AppendMemberSearchText(builder, template, "Tags");
                AppendMemberSearchText(builder, template, "tags");
                AppendMemberSearchText(builder, template, "AbstractTypes");
                AppendMemberSearchText(builder, template, "_abstractTypes");
            }

            return builder.ToString();
        }

        private string BuildTargetMetadataSearchText(object target, object healthElement)
        {
            StringBuilder builder = new StringBuilder();
            AppendTargetMetadataSearchText(builder, target);
            if (healthElement != null && !ReferenceEquals(healthElement, target))
            {
                AppendTargetMetadataSearchText(builder, healthElement);
            }

            return builder.ToString();
        }

        private void AppendTargetMetadataSearchText(StringBuilder builder, object obj)
        {
            if (builder == null || obj == null || IsDestroyedUnityObject(obj))
            {
                return;
            }

            AppendSingleTargetMetadataSearchText(builder, obj);

            object template = GetOptionalPropertyValue(obj, "Template");
            if (template != null && !ReferenceEquals(template, obj) && !IsDestroyedUnityObject(template))
            {
                AppendSingleTargetMetadataSearchText(builder, template);
            }
        }

        private void AppendSingleTargetMetadataSearchText(StringBuilder builder, object obj)
        {
            AppendMemberSearchText(builder, obj, "SurfaceType");
            AppendMemberSearchText(builder, obj, "surfaceType");
            AppendMemberSearchText(builder, obj, "NpcType");
            AppendMemberSearchText(builder, obj, "npcType");
            AppendMemberSearchText(builder, obj, "Tags");
            AppendMemberSearchText(builder, obj, "tags");
            AppendMemberSearchText(builder, obj, "AbstractTypes");
            AppendMemberSearchText(builder, obj, "_abstractTypes");
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

        private void AppendMemberSearchText(StringBuilder builder, object obj, string memberName)
        {
            object value = GetOptionalMemberValue(obj, memberName);
            AppendSearchValue(builder, value, 0);
        }

        private void AppendSearchValue(StringBuilder builder, object value, int depth)
        {
            if (builder == null || value == null || IsDestroyedUnityObject(value) || depth > 2)
            {
                return;
            }

            string text = value as string;
            if (text != null)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.Append(text).Append(' ');
                }
                return;
            }

            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                builder.Append(value).Append(' ');
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count >= 32)
                    {
                        break;
                    }

                    AppendSearchValue(builder, item, depth + 1);
                    count++;
                }
                return;
            }

            if (depth < 2)
            {
                builder.Append(type.Name).Append(' ');
                AppendStringProperty(builder, value, "Name");
                AppendStringProperty(builder, value, "DisplayName");
                AppendStringProperty(builder, value, "DebugName");
                AppendStringProperty(builder, value, "TechnicalName");
            }
            else
            {
                builder.Append(value).Append(' ');
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

        private string[] GetBoneUndeadTerms()
        {
            return GetTerms(_boneUndeadTerms, ref _cachedBoneUndeadTermsRaw, ref _cachedBoneUndeadTerms);
        }

        private string[] GetConstructTerms()
        {
            return GetTerms(_constructTerms, ref _cachedConstructTermsRaw, ref _cachedConstructTerms);
        }

        private string[] GetWyrdTerms()
        {
            return GetTerms(_wyrdTerms, ref _cachedWyrdTermsRaw, ref _cachedWyrdTerms);
        }

        private string[] GetDrownedZombieTerms()
        {
            return GetTerms(_drownedZombieTerms, ref _cachedDrownedZombieTermsRaw, ref _cachedDrownedZombieTerms);
        }

        private string[] GetInfectedFleshTerms()
        {
            return GetTerms(_infectedFleshTerms, ref _cachedInfectedFleshTermsRaw, ref _cachedInfectedFleshTerms);
        }

        private string[] GetSeaFleshTerms()
        {
            return GetTerms(_seaFleshTerms, ref _cachedSeaFleshTermsRaw, ref _cachedSeaFleshTerms);
        }

        private string[] GetSpiritTerms()
        {
            return GetTerms(_spiritTerms, ref _cachedSpiritTermsRaw, ref _cachedSpiritTerms);
        }

        private string[] GetFloraTerms()
        {
            return GetTerms(_floraTerms, ref _cachedFloraTermsRaw, ref _cachedFloraTerms);
        }

        private string[] GetFleshUndeadTerms()
        {
            return GetTerms(_fleshUndeadTerms, ref _cachedFleshUndeadTermsRaw, ref _cachedFleshUndeadTerms);
        }

        private string[] GetFleshTerms()
        {
            return GetTerms(_fleshTerms, ref _cachedFleshTermsRaw, ref _cachedFleshTerms);
        }

        private string[] GetArmoredHumanoidTerms()
        {
            return GetTerms(_armoredHumanoidTerms, ref _cachedArmoredHumanoidTermsRaw, ref _cachedArmoredHumanoidTerms);
        }

        private string[] GetTerms(ConfigEntry<string> entry, ref string cachedRaw, ref string[] cachedTerms)
        {
            string raw = entry == null ? "" : (entry.Value ?? "");
            if (raw != cachedRaw)
            {
                cachedRaw = raw;
                cachedTerms = SplitTerms(raw);
                _targetTermsRevision++;
                _targetClassifications.Clear();
            }

            return cachedTerms;
        }

        private string[] SplitTerms(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
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

        private bool ContainsAnyTerm(string text, string[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null || terms.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i]) && text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void RememberDamageFeedback(object damage, float multiplier, string targetLabel, string damageLabel)
        {
            if (_damageNumbersEnabled == null || !_damageNumbersEnabled.Value || damage == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            PrunePendingDamageFeedback(now);

            int key = RuntimeHelpers.GetHashCode(damage);
            _pendingDamageFeedback[key] = new PendingDamageFeedback
            {
                Damage = damage,
                Multiplier = multiplier,
                TargetLabel = targetLabel,
                DamageLabel = damageLabel,
                CreatedAt = now
            };

            TrimPendingDamageFeedback();
        }

        internal void HandleDamageOutcome(object healthElement, object damageOutcome)
        {
            if (damageOutcome == null || !DamageNumbersActive())
            {
                return;
            }

            object damage = GetOptionalMemberValue(damageOutcome, "Damage");
            PendingDamageFeedback feedback;
            if (!TryConsumeDamageFeedback(damage, out feedback) && !IsOutgoingHeroDamageOutcome(healthElement, damage))
            {
                return;
            }

            float finalAmount;
            if (!TryGetFloatMemberValue(damageOutcome, "FinalAmount", out finalAmount)
                && !TryGetFloatMemberValue(damage, "Amount", out finalAmount))
            {
                return;
            }

            if (float.IsNaN(finalAmount) || float.IsInfinity(finalAmount) || finalAmount < 0.0f)
            {
                return;
            }

            bool immune = finalAmount <= 0.0001f;
            if (!immune && finalAmount <= GetDamageNumberMinimumAmount())
            {
                return;
            }

            Vector3 position;
            if (!TryGetVector3MemberValue(damageOutcome, "Position", out position)
                && !TryGetVector3MemberValue(damage, "Position", out position))
            {
                position = Vector3.zero;
            }

            object modifiersInfo = GetOptionalMemberValue(damageOutcome, "DamageModifiersInfo");
            bool critical = IsTrueMember(damage, "Critical")
                || IsTrueMember(damage, "IsCritical")
                || IsTrueMember(modifiersInfo, "AnyCritical");
            bool weakSpot = IsTrueMember(damage, "WeakSpotHit")
                || IsTrueMember(damage, "IsWeakSpot")
                || IsTrueMember(modifiersInfo, "IsWeakSpot");

            DamageNumberVisual visual = BuildDamageNumberVisual(
                finalAmount,
                feedback,
                critical,
                weakSpot,
                immune,
                IsDamageOverTime(damage));
            _damageNumberRenderer.ShowDamageNumber(position, visual);
        }

        private bool DamageNumbersActive()
        {
            return _enabled != null
                && _enabled.Value
                && _damageNumbersEnabled != null
                && _damageNumbersEnabled.Value
                && _damageNumberRenderer != null;
        }

        private bool IsOutgoingHeroDamageOutcome(object healthElement, object damage)
        {
            object hero = GetCurrentHero();
            if (hero == null || !IsHeroDamageSource(damage, hero))
            {
                return false;
            }

            object heroHealthElement = GetOptionalPropertyValue(hero, "HealthElement");
            if (ReferenceEquals(healthElement, heroHealthElement))
            {
                return false;
            }

            object target = ResolveDamageTargetOwner(healthElement, damage);
            return target == null || !IsSameModelOrOwner(target, hero);
        }

        private bool TryConsumeDamageFeedback(object damage, out PendingDamageFeedback feedback)
        {
            feedback = null;
            if (damage == null)
            {
                return false;
            }

            PrunePendingDamageFeedback(Time.unscaledTime);

            int key = RuntimeHelpers.GetHashCode(damage);
            PendingDamageFeedback pending;
            if (!_pendingDamageFeedback.TryGetValue(key, out pending) || !ReferenceEquals(pending.Damage, damage))
            {
                return false;
            }

            _pendingDamageFeedback.Remove(key);
            feedback = pending;
            return true;
        }

        private void PrunePendingDamageFeedback(float now)
        {
            if (_pendingDamageFeedback.Count == 0)
            {
                return;
            }

            List<int> expiredKeys = null;
            foreach (KeyValuePair<int, PendingDamageFeedback> pair in _pendingDamageFeedback)
            {
                if (now - pair.Value.CreatedAt <= PendingDamageFeedbackLifetimeSeconds)
                {
                    continue;
                }

                if (expiredKeys == null)
                {
                    expiredKeys = new List<int>();
                }

                expiredKeys.Add(pair.Key);
            }

            if (expiredKeys == null)
            {
                return;
            }

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                _pendingDamageFeedback.Remove(expiredKeys[i]);
            }
        }

        private void TrimPendingDamageFeedback()
        {
            while (_pendingDamageFeedback.Count > MaxPendingDamageFeedback)
            {
                int oldestKey = 0;
                bool foundOldest = false;
                float oldestTime = float.MaxValue;
                foreach (KeyValuePair<int, PendingDamageFeedback> pair in _pendingDamageFeedback)
                {
                    if (!foundOldest || pair.Value.CreatedAt < oldestTime)
                    {
                        foundOldest = true;
                        oldestKey = pair.Key;
                        oldestTime = pair.Value.CreatedAt;
                    }
                }

                if (!foundOldest)
                {
                    return;
                }

                _pendingDamageFeedback.Remove(oldestKey);
            }
        }

        private DamageNumberVisual BuildDamageNumberVisual(
            float finalAmount,
            PendingDamageFeedback feedback,
            bool critical,
            bool weakSpot,
            bool immune,
            bool damageOverTime)
        {
            float multiplier = feedback == null ? 1.0f : feedback.Multiplier;
            float resistance = multiplier < 0.999f ? Mathf.Clamp01((1.0f - multiplier) / 0.95f) : 0.0f;
            float weakness = multiplier > 1.001f ? Mathf.Clamp01(multiplier - 1.0f) : 0.0f;
            float sizeContrast = GetDamageNumberSizeContrast();
            float colorContrast = GetDamageNumberColorContrast();

            Color baseColor = GetDamageNumberBaseColor();
            Color color = baseColor;
            float scale = 1.0f;
            float duration = GetDamageNumberDurationSeconds();
            float fadeStart = 0.58f;
            float horizontalDistance = UnityEngine.Random.Range(18.0f, 42.0f);
            float verticalRise = UnityEngine.Random.Range(68.0f, 92.0f);
            float gravity = UnityEngine.Random.Range(12.0f, 24.0f);

            if (resistance > 0.0f)
            {
                float tone = Mathf.Clamp01(0.18f + (resistance * 0.82f));
                color = Color.Lerp(baseColor, ResistedDamageNumberColor, Mathf.Clamp01(tone * colorContrast));
                float resistedScale = Mathf.Lerp(0.96f, 0.68f, resistance);
                scale = 1.0f + ((resistedScale - 1.0f) * sizeContrast);
                duration = Mathf.Lerp(duration, 0.60f, resistance);
                fadeStart = 0.44f;
                horizontalDistance = UnityEngine.Random.Range(26.0f, 52.0f);
                verticalRise = UnityEngine.Random.Range(42.0f, 66.0f);
                gravity = UnityEngine.Random.Range(18.0f, 32.0f);
            }
            else if (weakness > 0.0f)
            {
                float tone = Mathf.Clamp01(0.30f + (weakness * 0.70f));
                color = Color.Lerp(baseColor, WeaknessDamageNumberColor, Mathf.Clamp01(tone * colorContrast));
                float weaknessScale = Mathf.Lerp(1.12f, 1.46f, weakness);
                scale = 1.0f + ((weaknessScale - 1.0f) * sizeContrast);
                duration = Mathf.Max(duration, Mathf.Lerp(duration, 1.05f, weakness));
                fadeStart = 0.62f;
                horizontalDistance = UnityEngine.Random.Range(8.0f, 24.0f);
                verticalRise = UnityEngine.Random.Range(82.0f, 122.0f);
                gravity = UnityEngine.Random.Range(8.0f, 18.0f);
            }

            if (immune)
            {
                color = ImmuneDamageNumberColor;
                scale = 0.82f;
                duration = 0.72f;
                fadeStart = 0.46f;
                horizontalDistance = UnityEngine.Random.Range(16.0f, 34.0f);
                verticalRise = UnityEngine.Random.Range(36.0f, 52.0f);
                gravity = UnityEngine.Random.Range(18.0f, 30.0f);
            }

            if (critical)
            {
                scale *= 1.25f;
                duration = Mathf.Max(duration, GetDamageNumberCriticalDurationSeconds());
                fadeStart = Mathf.Max(fadeStart, 0.64f);
                horizontalDistance *= 0.58f;
                verticalRise += 26.0f;
                gravity *= 0.72f;
            }

            if (weakSpot)
            {
                scale += 0.08f;
                verticalRise += 8.0f;
            }

            return new DamageNumberVisual
            {
                Text = immune ? "IMMUNE" : FormatDamageAmount(finalAmount),
                Color = color,
                OutlineColor = DamageNumberOutlineColor,
                FontSize = GetDamageNumberFontSize(),
                StartScale = Mathf.Clamp(scale * (critical ? 1.18f : 1.05f), 0.55f, 1.95f),
                EndScale = Mathf.Clamp(scale, 0.55f, 1.80f),
                DurationSeconds = duration,
                FadeStart = Mathf.Clamp(fadeStart, 0.20f, 0.90f),
                Direction = UnityEngine.Random.value < 0.5f ? -1.0f : 1.0f,
                HorizontalDistance = horizontalDistance,
                VerticalRise = verticalRise,
                Gravity = gravity,
                WorldHeightMultiplier = damageOverTime ? GetDamageOverTimeNumberHeightMultiplier() : 1.0f,
                Critical = critical
            };
        }

        private string FormatDamageAmount(float finalAmount)
        {
            if (finalAmount >= 10.0f)
            {
                return Mathf.RoundToInt(finalAmount).ToString("N0", CultureInfo.InvariantCulture);
            }

            return finalAmount.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private Color GetDamageNumberBaseColor()
        {
            Color color;
            string configured = _damageNumberBaseColor == null ? null : _damageNumberBaseColor.Value;
            if (!string.IsNullOrWhiteSpace(configured) && ColorUtility.TryParseHtmlString(configured.Trim(), out color))
            {
                color.a = 1.0f;
                return color;
            }

            return DefaultDamageNumberColor;
        }

        private int GetDamageNumberFontSize()
        {
            int value = _damageNumberFontSize == null ? 34 : _damageNumberFontSize.Value;
            return Math.Max(12, Math.Min(80, value));
        }

        private FontAsset ResolveDamageNumberFontAsset()
        {
            DamageNumberFontMode mode = GetDamageNumberFontMode();
            if (mode == DamageNumberFontMode.ImguiDefault)
            {
                return ResolveImguiDefaultFontAsset();
            }

            try
            {
                if (mode == DamageNumberFontMode.Sans)
                {
                    return ResolveFontFamilyAsset(FontFamily.Sans, "Sans");
                }

                if (mode == DamageNumberFontMode.Serif)
                {
                    return ResolveFontFamilyAsset(FontFamily.Serif, "Serif");
                }

                FontChooseSetting setting = World.Any<FontChooseSetting>();
                if (setting == null)
                {
                    return TMP_Settings.defaultFontAsset;
                }

                FontFamily activeFont = setting.ActiveFont;
                return ResolveFontFamilyAsset(activeFont, activeFont == null ? "game" : "game " + activeFont.EnumName);
            }
            catch (Exception ex)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "ResolveDamageNumberFontAsset:" + mode.ToString() + ":" + ex.GetType().FullName,
                    "Could not resolve " + mode + " font asset for Steel and Bone damage numbers; using the TextMesh Pro default. "
                    + ex.GetBaseException().Message);
                return TMP_Settings.defaultFontAsset;
            }
        }

        private DamageNumberFontMode GetDamageNumberFontMode()
        {
            return _damageNumberFontMode == null ? DamageNumberFontMode.GameDefault : _damageNumberFontMode.Value;
        }

        private FontAsset ResolveFontFamilyAsset(FontFamily fontFamily, string label)
        {
            if (fontFamily == null)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "FontFamilyMissing:" + label,
                    "Could not resolve " + label + " font family for Steel and Bone damage numbers; using the TextMesh Pro default.");
                return TMP_Settings.defaultFontAsset;
            }

            FontAsset fontAsset = fontFamily.FontAsset;
            if (fontAsset == null)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "FontAssetMissing:" + fontFamily.EnumName,
                    "Could not resolve " + label + " FontAsset for Steel and Bone damage numbers; using the TextMesh Pro default.");
                return TMP_Settings.defaultFontAsset;
            }

            return fontAsset;
        }

        private FontAsset ResolveImguiDefaultFontAsset()
        {
            if (_imguiDefaultFontAsset != null)
            {
                return _imguiDefaultFontAsset;
            }

            try
            {
                Font sourceFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (sourceFont != null)
                {
                    _imguiDefaultFontAsset = FontAsset.CreateFontAsset(sourceFont);
                    if (_imguiDefaultFontAsset != null)
                    {
                        _imguiDefaultFontAsset.name = "SteelAndBone-ImguiDefault";
                        _imguiDefaultFontAsset.hideFlags = HideFlags.HideAndDontSave;
                        return _imguiDefaultFontAsset;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "ResolveImguiDefaultFontAsset:" + ex.GetType().FullName,
                    "Could not create the legacy fallback font asset for Steel and Bone damage numbers; using the TextMesh Pro default. "
                    + ex.GetBaseException().Message);
            }

            return TMP_Settings.defaultFontAsset;
        }

        private void LogDamageNumberFontDiagnosticOnce(string key, string message)
        {
            if (!DiagnosticsEnabled() || string.Equals(_lastDamageNumberFontDiagnosticKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _lastDamageNumberFontDiagnosticKey = key;
            Logger.LogWarning(message);
        }

        private float GetDamageNumberDurationSeconds()
        {
            float value = _damageNumberDurationSeconds == null ? 0.85f : _damageNumberDurationSeconds.Value;
            return Clamp(value, 0.35f, 2.50f);
        }

        private float GetDamageNumberCriticalDurationSeconds()
        {
            float value = _damageNumberCriticalDurationSeconds == null ? 1.10f : _damageNumberCriticalDurationSeconds.Value;
            return Clamp(value, 0.45f, 3.00f);
        }

        private float GetDamageNumberHorizontalDrift()
        {
            float value = _damageNumberHorizontalDrift == null ? 1.0f : _damageNumberHorizontalDrift.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageNumberVerticalDrift()
        {
            float value = _damageNumberVerticalDrift == null ? 1.0f : _damageNumberVerticalDrift.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageOverTimeNumberHeightMultiplier()
        {
            float value = _damageOverTimeNumberHeightMultiplier == null ? 1.25f : _damageOverTimeNumberHeightMultiplier.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageNumberSizeContrast()
        {
            float value = _damageNumberSizeContrast == null ? 1.0f : _damageNumberSizeContrast.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageNumberColorContrast()
        {
            float value = _damageNumberColorContrast == null ? 1.0f : _damageNumberColorContrast.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageNumberMinimumAmount()
        {
            float value = _damageNumberMinimumAmount == null ? 0.10f : _damageNumberMinimumAmount.Value;
            return Clamp(value, 0.0f, 1000.0f);
        }

        private int GetDamageNumberMaximumActive()
        {
            int value = _damageNumberMaximumActive == null ? 36 : _damageNumberMaximumActive.Value;
            return Math.Max(1, Math.Min(128, value));
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

        private object GetOptionalMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            object value = GetOptionalPropertyValue(instance, memberName);
            if (value != null)
            {
                return value;
            }

            try
            {
                FieldInfo field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }
            }
            catch
            {
                return null;
            }

            return null;
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

        private bool GetOptionalBoolProperty(object instance, string propertyName)
        {
            object value = GetOptionalPropertyValue(instance, propertyName);
            return value is bool && (bool)value;
        }

        private bool IsTrueMember(object instance, string memberName)
        {
            bool value;
            return TryGetBoolMemberValue(instance, memberName, out value) && value;
        }

        private bool TryGetBoolMemberValue(object instance, string memberName, out bool value)
        {
            value = false;
            object raw = GetOptionalMemberValue(instance, memberName);
            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            return false;
        }

        private bool TryGetFloatMemberValue(object instance, string memberName, out float value)
        {
            value = 0.0f;
            object raw = GetOptionalMemberValue(instance, memberName);
            if (raw == null)
            {
                return false;
            }

            try
            {
                if (raw is float)
                {
                    value = (float)raw;
                    return true;
                }

                if (raw is double)
                {
                    value = (float)(double)raw;
                    return true;
                }

                if (raw is int)
                {
                    value = (int)raw;
                    return true;
                }

                if (raw is long)
                {
                    value = (long)raw;
                    return true;
                }

                if (raw is IConvertible)
                {
                    value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool TryGetVector3MemberValue(object instance, string memberName, out Vector3 value)
        {
            value = Vector3.zero;
            object raw = GetOptionalMemberValue(instance, memberName);
            if (raw is Vector3)
            {
                value = (Vector3)raw;
                return true;
            }

            return false;
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

        private object GetIndexedValue(PropertyInfo indexer, object instance, int index)
        {
            if (indexer == null || instance == null)
            {
                return null;
            }

            try
            {
                return indexer.GetValue(instance, new object[] { index });
            }
            catch
            {
                return null;
            }
        }

        private bool ValueNameContains(object value, string expected)
        {
            if (value == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            return value.ToString().IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsDestroyedUnityObject(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return !ReferenceEquals(unityObject, null) && unityObject == null;
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

        private string DescribeValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            string text = value as string;
            if (text != null)
            {
                return text.Length == 0 ? "\"\"" : text;
            }

            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                return value.ToString();
            }

            return DescribeObject(value);
        }

        private string DescribeTargetFamilies(TargetClassification classification)
        {
            if (classification == null)
            {
                return "None";
            }

            StringBuilder builder = new StringBuilder();
            AppendDiagnosticLabel(builder, classification.IsBoneUndead, "BoneUndead");
            AppendDiagnosticLabel(builder, classification.IsConstruct, "Construct");
            AppendDiagnosticLabel(builder, classification.IsArmoredHumanoid, "ArmoredHumanoid");
            AppendDiagnosticLabel(builder, classification.IsFlesh, "Flesh");
            AppendDiagnosticLabel(builder, classification.IsFleshUndead, "FleshUndead");
            AppendDiagnosticLabel(builder, classification.IsWyrd, "Wyrd");
            AppendDiagnosticLabel(builder, classification.IsDrownedZombie, "DrownedZombie");
            AppendDiagnosticLabel(builder, classification.IsInfectedFlesh, "InfectedFlesh");
            AppendDiagnosticLabel(builder, classification.IsSeaFlesh, "SeaFlesh");
            AppendDiagnosticLabel(builder, classification.IsSpirit, "Spirit");
            AppendDiagnosticLabel(builder, classification.IsFlora, "Flora");
            return builder.Length == 0 ? "None" : builder.ToString();
        }

        private string DescribeTargetFlags(TargetClassification classification)
        {
            if (classification == null || !classification.IsEliteClass)
            {
                return "None";
            }

            return "EliteClass";
        }

        private string DescribeDamageTags(DamageClassification classification)
        {
            if (classification == null || classification.Tags == DamageTag.None)
            {
                return "None";
            }

            return classification.Tags.ToString();
        }

        private void AppendDiagnosticLabel(StringBuilder builder, bool include, string label)
        {
            if (!include)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("|");
            }

            builder.Append(label);
        }

        private float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void LogDiagnostic(string message)
        {
            if (DiagnosticsEnabled())
            {
                Log.LogInfo(message);
            }
        }

        private bool DiagnosticsEnabled()
        {
            return _diagnostics != null && _diagnostics.Value;
        }

        private void Warn(string message)
        {
            if (_logPatchWarnings == null || _logPatchWarnings.Value)
            {
                Log.LogWarning(message);
            }
        }

        private enum Preset
        {
            Tempered,
            Hardened,
            Crucible
        }

        private enum DamageNumberFontMode
        {
            GameDefault,
            Sans,
            Serif,
            ImguiDefault
        }

        private enum TargetFamily
        {
            BoneUndead,
            Construct,
            ArmoredHumanoid,
            Flesh,
            FleshUndead,
            Wyrd,
            DrownedZombie,
            InfectedFlesh,
            SeaFlesh,
            Spirit,
            Flora
        }

        [Flags]
        private enum DamageTag
        {
            None = 0,
            BloodMagic = 1,
            Bleed = 2,
            Poison = 4,
            Wyrdness = 8,
            Slashing = 16,
            Piercing = 32,
            Bludgeoning = 64,
            GenericPhysical = 128,
            GenericMagical = 256,
            Fire = 512,
            Cold = 1024,
            Electric = 2048,
            Wet = 4096,
            Burn = 8192
        }

        private sealed class NativeSubtypeCheck
        {
            public readonly DamageTag Tag;
            public readonly string SubtypeName;

            public NativeSubtypeCheck(DamageTag tag, string subtypeName)
            {
                Tag = tag;
                SubtypeName = subtypeName;
            }
        }

        private sealed class DamageRule
        {
            public readonly TargetFamily TargetFamily;
            public readonly DamageTag DamageTags;
            public readonly string TargetLabel;
            public readonly string DamageLabel;
            public readonly float BaseMultiplier;
            public readonly int Priority;

            public DamageRule(
                TargetFamily targetFamily,
                DamageTag damageTags,
                string targetLabel,
                string damageLabel,
                float baseMultiplier,
                int priority)
            {
                TargetFamily = targetFamily;
                DamageTags = damageTags;
                TargetLabel = targetLabel;
                DamageLabel = damageLabel;
                BaseMultiplier = baseMultiplier;
                Priority = priority;
            }

            public bool MatchesDamageTag(DamageTag tag)
            {
                return (DamageTags & tag) != DamageTag.None;
            }
        }

        private readonly struct DamageRuleMatch
        {
            public readonly float Multiplier;
            public readonly string TargetLabel;
            public readonly string DamageLabel;
            public readonly int Priority;
            public readonly float Impact;
            public readonly float PresetMultiplier;
            public readonly bool WasEliteClamped;

            public DamageRuleMatch(
                float multiplier,
                string targetLabel,
                string damageLabel,
                int priority,
                float impact,
                float presetMultiplier,
                bool wasEliteClamped)
            {
                Multiplier = multiplier;
                TargetLabel = targetLabel;
                DamageLabel = damageLabel;
                Priority = priority;
                Impact = impact;
                PresetMultiplier = presetMultiplier;
                WasEliteClamped = wasEliteClamped;
            }
        }

        private readonly struct VanillaMultiplierAmplification
        {
            public readonly string SubtypeName;
            public readonly float NativeMultiplier;
            public readonly float AmplifiedMultiplier;
            public readonly float AdjustmentMultiplier;

            public VanillaMultiplierAmplification(
                string subtypeName,
                float nativeMultiplier,
                float amplifiedMultiplier,
                float adjustmentMultiplier)
            {
                SubtypeName = subtypeName;
                NativeMultiplier = nativeMultiplier;
                AmplifiedMultiplier = amplifiedMultiplier;
                AdjustmentMultiplier = adjustmentMultiplier;
            }
        }

        private sealed class TargetClassification
        {
            public static readonly TargetClassification Empty = new TargetClassification();

            public object Key;
            public int Revision;
            public bool HasMetadataEvidence;
            public string Evidence;
            public bool IsBoneUndead;
            public bool IsConstruct;
            public bool IsArmoredHumanoid;
            public bool IsFlesh;
            public bool IsFleshUndead;
            public bool IsWyrd;
            public bool IsDrownedZombie;
            public bool IsInfectedFlesh;
            public bool IsSeaFlesh;
            public bool IsSpirit;
            public bool IsFlora;
            public bool IsEliteClass;

            public bool HasAnyFamily()
            {
                return IsBoneUndead
                    || IsConstruct
                    || IsArmoredHumanoid
                    || IsFlesh
                    || IsFleshUndead
                    || IsWyrd
                    || IsDrownedZombie
                    || IsInfectedFlesh
                    || IsSeaFlesh
                    || IsSpirit
                    || IsFlora;
            }

            public bool HasMetadataFamily()
            {
                return HasMetadataEvidence && HasAnyFamily();
            }
        }

        private sealed class DamageClassification
        {
            public static readonly DamageClassification Empty = new DamageClassification();

            public bool IsBloodMagic;
            public bool IsBleed;
            public bool IsPoison;
            public bool IsWyrdness;
            public bool IsSlashing;
            public bool IsPiercing;
            public bool IsBludgeoning;
            public bool IsGenericPhysical;
            public bool IsGenericMagical;
            public bool IsFire;
            public bool IsCold;
            public bool IsElectric;
            public bool IsWet;
            public bool IsBurn;
            public DamageTag Tags;
            public string PhysicalTypeHint;

            public bool HasSpecificPhysicalType()
            {
                return IsSlashing || IsPiercing || IsBludgeoning;
            }

            public bool HasAny(DamageTag tags)
            {
                return (Tags & tags) != DamageTag.None;
            }
        }

        private sealed class PendingDamageFeedback
        {
            public object Damage;
            public float Multiplier;
            public string TargetLabel;
            public string DamageLabel;
            public float CreatedAt;
        }

        private sealed class DamageNumberVisual
        {
            public string Text;
            public Color Color;
            public Color OutlineColor;
            public int FontSize;
            public float StartScale;
            public float EndScale;
            public float DurationSeconds;
            public float FadeStart;
            public float Direction;
            public float HorizontalDistance;
            public float VerticalRise;
            public float Gravity;
            public float WorldHeightMultiplier;
            public bool Critical;
        }

        private sealed class DamageNumberEntry
        {
            public Vector3 WorldPosition;
            public float StartTime;
            public DamageNumberVisual Visual;
            public TextMeshProUGUI Text;
        }

        private sealed class DamageNumberRenderer : MonoBehaviour
        {
            private readonly List<DamageNumberEntry> _entries = new List<DamageNumberEntry>();
            private SteelAndBonePlugin _plugin;
            private RectTransform _canvasRoot;

            public void Initialize(SteelAndBonePlugin plugin)
            {
                _plugin = plugin;
                hideFlags = HideFlags.HideAndDontSave;
                EnsureCanvas();
            }

            public void ShowDamageNumber(Vector3 worldPosition, DamageNumberVisual visual)
            {
                if (_plugin == null || visual == null || string.IsNullOrEmpty(visual.Text))
                {
                    return;
                }

                int maximumActive = _plugin.GetDamageNumberMaximumActive();
                while (_entries.Count >= maximumActive)
                {
                    DestroyEntry(_entries[0]);
                    _entries.RemoveAt(0);
                }

                TextMeshProUGUI text = CreateText(visual);
                if (text == null)
                {
                    return;
                }

                _entries.Add(new DamageNumberEntry
                {
                    WorldPosition = worldPosition
                        + (Vector3.up
                            * UnityEngine.Random.Range(0.25f, 0.65f)
                            * visual.WorldHeightMultiplier),
                    StartTime = Time.unscaledTime,
                    Visual = visual,
                    Text = text
                });
            }

            private void LateUpdate()
            {
                if (_plugin == null || _entries.Count == 0)
                {
                    return;
                }

                Camera camera = Camera.main;
                if (camera == null)
                {
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        if (_entries[i].Text != null)
                        {
                            _entries[i].Text.enabled = false;
                        }
                    }

                    return;
                }

                float now = Time.unscaledTime;
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    DamageNumberEntry entry = _entries[i];
                    DamageNumberVisual visual = entry.Visual;
                    float duration = Math.Max(0.05f, visual.DurationSeconds);
                    float elapsed = now - entry.StartTime;
                    if (elapsed >= duration)
                    {
                        DestroyEntry(entry);
                        _entries.RemoveAt(i);
                        continue;
                    }

                    Vector3 projected = camera.WorldToScreenPoint(entry.WorldPosition);
                    if (projected.z <= 0.0f)
                    {
                        entry.Text.enabled = false;
                        continue;
                    }

                    entry.Text.enabled = true;
                    float t = Mathf.Clamp01(elapsed / duration);
                    UpdateEntry(entry, projected, visual, t);
                }
            }

            private void EnsureCanvas()
            {
                if (_canvasRoot != null)
                {
                    return;
                }

                GameObject canvasObject = new GameObject(
                    "SteelAndBoneDamageNumberCanvas",
                    typeof(RectTransform),
                    typeof(Canvas));
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                canvasObject.transform.SetParent(transform, false);

                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 30000;
                _canvasRoot = canvasObject.GetComponent<RectTransform>();
            }

            private TextMeshProUGUI CreateText(DamageNumberVisual visual)
            {
                EnsureCanvas();
                if (_canvasRoot == null)
                {
                    return null;
                }

                GameObject textObject = new GameObject(
                    "DamageNumber",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textObject.hideFlags = HideFlags.HideAndDontSave;
                RectTransform rect = textObject.GetComponent<RectTransform>();
                rect.SetParent(_canvasRoot, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(640.0f, 220.0f);

                TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
                text.text = visual.Text;
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = TMPro.FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.richText = false;
                text.raycastTarget = false;
                text.outlineWidth = 0.18f;
                return text;
            }

            private void UpdateEntry(DamageNumberEntry entry, Vector3 projected, DamageNumberVisual visual, float t)
            {
                float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
                float scale = Mathf.Lerp(visual.StartScale, visual.EndScale, smoothT);
                if (visual.Critical)
                {
                    scale *= Mathf.Lerp(1.16f, 1.0f, Mathf.Clamp01(t / 0.22f));
                }

                float fadeStart = Mathf.Clamp(visual.FadeStart, 0.01f, 0.99f);
                float alpha = t <= fadeStart ? 1.0f : 1.0f - Mathf.Clamp01((t - fadeStart) / (1.0f - fadeStart));
                if (alpha <= 0.01f)
                {
                    entry.Text.enabled = false;
                    return;
                }

                float xOffset = visual.Direction * visual.HorizontalDistance * Mathf.Sin(t * Mathf.PI * 0.75f) * _plugin.GetDamageNumberHorizontalDrift();
                float yOffset = ((-visual.VerticalRise * t) + (visual.Gravity * t * t)) * _plugin.GetDamageNumberVerticalDrift();
                float centerX = projected.x + xOffset;
                float centerY = projected.y - yOffset;

                FontAsset fontAsset = _plugin.ResolveDamageNumberFontAsset();
                if (fontAsset != null && !ReferenceEquals(entry.Text.font, fontAsset))
                {
                    entry.Text.font = fontAsset;
                }

                entry.Text.fontSize = Math.Max(8.0f, visual.FontSize * scale);
                entry.Text.color = WithAlpha(visual.Color, alpha);
                entry.Text.outlineColor = WithAlpha(visual.OutlineColor, alpha * 0.88f);
                entry.Text.rectTransform.anchoredPosition = new Vector2(centerX, centerY);
            }

            private void DestroyEntry(DamageNumberEntry entry)
            {
                if (entry != null && entry.Text != null)
                {
                    Destroy(entry.Text.gameObject);
                    entry.Text = null;
                }
            }

            private void OnDestroy()
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    DestroyEntry(_entries[i]);
                }

                _entries.Clear();
                if (_canvasRoot != null)
                {
                    Destroy(_canvasRoot.gameObject);
                    _canvasRoot = null;
                }
            }

            private static Color WithAlpha(Color color, float alpha)
            {
                color.a *= Mathf.Clamp01(alpha);
                return color;
            }
        }

        private static class ApplyDamageModifiersPatch
        {
            public static void Postfix(object __instance, object damage, ref float dmgModifier)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyDamageRuleModifier(__instance, damage, ref dmgModifier);
                }
            }
        }

        private static class AfterHealthDecreaseEventsPatch
        {
            public static void Postfix(object __instance, object[] __args)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null && __args != null && __args.Length > 0)
                {
                    plugin.HandleDamageOutcome(__instance, __args[0]);
                }
            }
        }
    }
}
