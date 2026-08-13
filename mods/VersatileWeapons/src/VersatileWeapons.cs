using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Assets;
using Awaken.TG.MVC;
using Awaken.TG.MVC.UI;
using Awaken.TG.MVC.UI.Events;
using Awaken.TG.Main.Animations.FSM.Heroes.Base;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.General.Configs;
using Awaken.TG.Main.General.StatTypes;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Animations;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Attachments;
using Awaken.TG.Main.Heroes.Items.Weapons;
using Awaken.TG.Main.Heroes.Stats.Observers;
using Awaken.TG.Main.Heroes.Stats.Utils;
using Awaken.TG.Main.Settings.Gameplay;
using Awaken.TG.Main.Utility;
using Awaken.TG.Main.Utility.Animations.ARAnimator;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Versatile Weapons")]
[assembly: AssemblyDescription("Strength-scaled one-handed greatweapons and switchable melee grips for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Versatile Weapons")]
[assembly: AssemblyVersion("0.4.0.0")]
[assembly: AssemblyFileVersion("0.4.0.0")]
[assembly: AssemblyInformationalVersion("0.4.0")]

namespace VersatileWeapons
{
    public enum StrengthTestMode
    {
        Actual,
        WeaponRequirement,
        FullPotency
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("jonanoj.DualTwoHanded")]
    [BepInIncompatibility("ks.tgfoa.dual-two-handed-addon")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.versatile-weapons";
        public const string PluginName = "Versatile Weapons";
        public const string PluginVersion = "0.4.0";

        private const int ConfigSchemaVersion = 11;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        private const int WeaponTransitionRefreshWindowFrames = 600;
        private const int GripFsmRecoveryFrames = 12;
        private const float PairedRefreshTimeoutSeconds = 4.0f;
        private const float HiddenDrawnWeaponRecoverySeconds = 1.5f;
        private const float GripEquipInputGuardTimeoutSeconds = 3.0f;

        private const string OneHandedSwordFppAddress =
            "229dcf6e54720324a8aa1cdecde8bb2c";
        private const string OneHandedSwordTppAddress =
            "45e446f8865138945966df1983ea3d79";
        private const string OneHandedAxeFppAddress =
            "4d58426ec610a184bbab729ba73f9e59";
        private const string OneHandedBluntFppAddress =
            "8ba79c3de5e02f542a8267871f766295";
        private const string OneHandedPolearmFppAddress =
            "08b24655778625349abf2e9d1a5c0b0e";
        private const string OneHandedPolearmTppAddress =
            "db4e026c37861df47b37886f6e66f6da";
        private const string TwoHandedSwordFppAddress =
            "cff13bcac65eb9e4392d0c71a5bf5b53";
        private const string TwoHandedSwordTppAddress =
            "f91b5d793eaa7e7449bd7e9c7f43667e";
        private const string TwoHandedAxeFppAddress =
            "d59bdf9c0fd2a1f4798ce777e5f94131";
        private const string TwoHandedAxeTppAddress =
            "e19c011a05800d542876b357af09ab21";
        private const string TwoHandedPolearmFppAddress =
            "f67a8f9782bd07347910c938202d1e75";
        private const string TwoHandedPolearmTppAddress =
            "5baa3a639f748a34fbe69f70d62d626b";

        private static readonly string[] OneHandedLayers =
            { "1H_MainHand" };
        private static readonly string[] TwoHandedLayers =
            { "2H" };
        private static readonly MethodInfo HeroWeaponsVisibleSetter =
            AccessTools.PropertySetter(typeof(Hero), "WeaponsVisible");

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _fullPotencyStrengthMultiplier;
        private ConfigEntry<float> _gripHoldSeconds;
        private ConfigEntry<bool> _proficiencyFollowsGrip;
        private ConfigEntry<float> _twoHandedOneHandedWeaponDamageMultiplier;
        private ConfigEntry<float> _twoHandedOneHandedWeaponAttackSpeedMultiplier;
        private ConfigEntry<float> _twoHandedOneHandedWeaponPoiseMultiplier;
        private ConfigEntry<float> _twoHandedOneHandedWeaponForceMultiplier;
        private ConfigEntry<float> _twoHandedOneHandedAxeMeleeRangeMultiplier;
        private ConfigEntry<float> _twoHandedOneHandedMaceMeleeRangeMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponRequirementDamageMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponFullDamageMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponRequirementAttackSpeedMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponFullAttackSpeedMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponRequirementPoiseMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponFullPoiseMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponRequirementForceMultiplier;
        private ConfigEntry<float> _oneHandedTwoHandedWeaponFullForceMultiplier;
        private ConfigEntry<float> _oneHandedSwordPositionY;
        private ConfigEntry<float> _oneHandedMacePositionY;
        private ConfigEntry<float> _oneHandedAxePositionY;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<StrengthTestMode> _strengthTestMode;
        private ConfigEntry<bool> _showGrailFloatingTextDiagnostics;
        private ConfigEntry<bool> _twoHandedGripUsesNormalHands;
        private ConfigEntry<bool> _singleSpellUsesNormalHands;
        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedConfigValues =
                new Dictionary<ConfigDefinition, object>();

        internal static Plugin Instance { get; private set; }

        private Harmony _harmony;
        private CharacterHand _observedWeapon;
        private bool _observedAnimationState;
        private bool _observedAnimationStateKnown;
        private int _gripFsmMismatchFrames;
        private CharacterHand _readyMainHand;
        private CharacterHand _readyOffHand;
        private bool _oneHandedReconciliationPending;
        private bool _weaponTransitionRefreshPending;
        private int _weaponTransitionRefreshFramesRemaining;
        private bool _observedLoadoutIndexKnown;
        private int _observedLoadoutIndex;
        private PairedRefreshStage _pairedRefreshStage;
        private CharacterHand _pairedRefreshWeapon;
        private CharacterHand _pairedRefreshShield;
        private int _pairedRefreshWaitFrames;
        private float _pairedRefreshStartedAt;
        private Item _gripItem;
        private Item _gripPairedItem;
        private bool _twoHandedGrip;
        private CharacterHandBase _hiddenPairedHand;
        private float _drawnWeaponHiddenSince = -1.0f;
        private bool _toggleWeaponHeld;
        private bool _gripAttemptedForHold;
        private bool _gripChangedForHold;
        private float _toggleWeaponPressedAt;
        private string _lastDiagnosticWeaponSignature;
        private bool _gripEquipInputGuardActive;
        private Item _gripEquipInputGuardItem;
        private bool _gripEquipInputGuardSawEquipState;
        private float _gripEquipInputGuardStartedAt;
        private Transform _adjustedFirstPersonWeaponTransform;
        private Vector3 _originalFirstPersonWeaponLocalPosition;
        private Quaternion _originalFirstPersonWeaponLocalRotation;

        private enum PairedRefreshStage
        {
            None,
            Hidden,
            WaitingForSword,
            WaitingForShield
        }

        private void Awake()
        {
            Instance = this;

            try
            {
                BindConfig();
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Config.SettingChanged += OnConfigChanged;
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Supported swords, axes, hammers, and spears can change grip by holding Toggle Weapon.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(
                    PluginGuid,
                    PluginName,
                    exception);
                enabled = false;
            }
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind(
                "1. General",
                "Enabled",
                true,
                "Master switch. Disabling this restores native equipment and grip behavior after the current weapon state refreshes.");
            Config.Bind(
                "1. General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Do not edit manually; the plugin backs up stale configs and regenerates defaults when this changes.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _fullPotencyStrengthMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "FullPotencyStrengthMultiplier",
                2.0f,
                new ConfigDescription(
                    "Strength at which the full-potency damage, speed, poise, and force values apply. Scaling begins at the weapon's normal Strength requirement. 2 means full potency at 200 percent of that requirement.",
                    new AcceptableValueRange<float>(1.0f, 5.0f)));
            _gripHoldSeconds = Config.Bind(
                "4. Grip Switching",
                "GripHoldSeconds",
                0.45f,
                new ConfigDescription(
                    "Seconds the game's Toggle Weapon action must be held to change grip on a supported weapon. A shorter press keeps normal sheathe or draw behavior.",
                    new AcceptableValueRange<float>(0.2f, 2.0f)));
            _proficiencyFollowsGrip = Config.Bind(
                "4. Grip Switching",
                "ProficiencyFollowsGrip",
                true,
                "Use One-Handed proficiency damage scaling and XP in a one-handed grip, and Two-Handed proficiency damage scaling and XP in a two-handed grip. Weapon requirements, stamina costs, and template-filtered item effects remain native.");
            _oneHandedSwordPositionY = Config.Bind(
                "5. Advanced First-Person Alignment",
                "OneHandedSwordPositionY",
                0.02f,
                new ConfigDescription(
                    "Local weapon-space Y offset in meters for native one-handed swords used with both hands in first person. Set to 0 for no correction.",
                    new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _oneHandedMacePositionY = Config.Bind(
                "5. Advanced First-Person Alignment",
                "OneHandedMacePositionY",
                -0.35f,
                new ConfigDescription(
                    "Local weapon-space Y offset in meters for native one-handed maces and other blunt weapons used with both hands in first person. Set to 0 for no correction.",
                    new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _oneHandedAxePositionY = Config.Bind(
                "5. Advanced First-Person Alignment",
                "OneHandedAxePositionY",
                -0.35f,
                new ConfigDescription(
                    "Local weapon-space Y offset in meters for native one-handed axes used with both hands in first person. Set to 0 for no correction.",
                    new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _twoHandedOneHandedWeaponDamageMultiplier = Config.Bind(
                "3. Native One-Handed Weapon - Two-Handed Grip",
                "DamageMultiplier",
                1.5f,
                new ConfigDescription(
                    "Melee damage while a native one-handed weapon is used with both hands. 1.5 means 150 percent damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _twoHandedOneHandedWeaponAttackSpeedMultiplier = Config.Bind(
                "3. Native One-Handed Weapon - Two-Handed Grip",
                "AttackSpeedMultiplier",
                1.2f,
                new ConfigDescription(
                    "Attack-animation speed while a native one-handed weapon is used with both hands. 1.2 means 120 percent speed.",
                    new AcceptableValueRange<float>(0.5f, 1.5f)));
            _twoHandedOneHandedWeaponPoiseMultiplier = Config.Bind(
                "3. Native One-Handed Weapon - Two-Handed Grip",
                "PoiseMultiplier",
                1.2f,
                new ConfigDescription(
                    "Poise damage while a native one-handed weapon is used with both hands. 1.2 means 120 percent poise damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _twoHandedOneHandedWeaponForceMultiplier = Config.Bind(
                "3. Native One-Handed Weapon - Two-Handed Grip",
                "ForceMultiplier",
                1.1f,
                new ConfigDescription(
                    "Impact force while a native one-handed weapon is used with both hands. 1.1 means 110 percent force.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _twoHandedOneHandedAxeMeleeRangeMultiplier = Config.Bind(
                "3. Native One-Handed Weapon - Two-Handed Grip",
                "AxeMeleeRangeMultiplier",
                1.5f,
                new ConfigDescription(
                    "Melee hit-detection range for native one-handed axes used with both hands. 1.5 means 150 percent range; 1 keeps vanilla range. This does not resize or move the visible weapon.",
                    new AcceptableValueRange<float>(0.5f, 3.0f)));
            _twoHandedOneHandedMaceMeleeRangeMultiplier = Config.Bind(
                "3. Native One-Handed Weapon - Two-Handed Grip",
                "MaceMeleeRangeMultiplier",
                1.5f,
                new ConfigDescription(
                    "Melee hit-detection range for native one-handed maces and other blunt weapons used with both hands. 1.5 means 150 percent range; 1 keeps vanilla range. This does not resize or move the visible weapon.",
                    new AcceptableValueRange<float>(0.5f, 3.0f)));
            _oneHandedTwoHandedWeaponRequirementDamageMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "DamageAtWeaponRequirement",
                0.75f,
                new ConfigDescription(
                    "Melee damage at the weapon's normal Strength requirement. 0.75 means 75 percent damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponFullDamageMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "DamageAtFullPotency",
                1.0f,
                new ConfigDescription(
                    "Melee damage at or above FullPotencyStrengthMultiplier. 1 means full native damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponRequirementAttackSpeedMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "AttackSpeedAtWeaponRequirement",
                0.5f,
                new ConfigDescription(
                    "Attack-animation speed at the weapon's normal Strength requirement. 0.5 means 50 percent speed.",
                    new AcceptableValueRange<float>(0.5f, 1.5f)));
            _oneHandedTwoHandedWeaponFullAttackSpeedMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "AttackSpeedAtFullPotency",
                0.75f,
                new ConfigDescription(
                    "Attack-animation speed at or above FullPotencyStrengthMultiplier. 0.75 means 75 percent speed.",
                    new AcceptableValueRange<float>(0.5f, 1.5f)));
            _oneHandedTwoHandedWeaponRequirementPoiseMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "PoiseAtWeaponRequirement",
                0.6f,
                new ConfigDescription(
                    "Poise damage at the weapon's normal Strength requirement. 0.6 means 60 percent poise damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponFullPoiseMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "PoiseAtFullPotency",
                0.95f,
                new ConfigDescription(
                    "Poise damage at or above FullPotencyStrengthMultiplier. 0.95 means 95 percent poise damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponRequirementForceMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "ForceAtWeaponRequirement",
                0.65f,
                new ConfigDescription(
                    "Impact force at the weapon's normal Strength requirement. 0.65 means 65 percent force.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponFullForceMultiplier = Config.Bind(
                "2. Native Two-Handed Weapon - One-Handed Grip",
                "ForceAtFullPotency",
                1.0f,
                new ConfigDescription(
                    "Impact force at or above FullPotencyStrengthMultiplier. 1 means full native force.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _diagnostics = Config.Bind(
                "6. Diagnostics",
                "Enabled",
                false,
                "Write grip recognition, input, and animation-transition details to the BepInEx log.");
            _strengthTestMode = Config.Bind(
                "6. Diagnostics",
                "StrengthTestMode",
                StrengthTestMode.Actual,
                "Test native two-handed weapons used in one hand at Actual Strength, WeaponRequirement, or FullPotency. This simulation works only while Diagnostics is enabled and never changes the character's Strength.");
            _showGrailFloatingTextDiagnostics = Config.Bind(
                "6. Diagnostics",
                "ShowGrailFloatingTextDiagnostics",
                true,
                "When Diagnostics is enabled and Grail Floating Text is installed, show all Versatile Weapons System messages, including completed grip changes, weapon recognition, blocked transitions, pairing changes, and recoveries. Detailed BepInEx logging remains active when this is disabled.");
            _twoHandedGripUsesNormalHands = Config.Bind(
                "7. Reverse Hands Compatibility",
                "TwoHandedGripUsesNormalHands",
                true,
                "When the game's Reverse Hands setting is enabled, use normal hand input while a supported weapon uses both hands and its paired spell is stowed. Disable this to retain the game's reversed input in that specific grip.");
            _singleSpellUsesNormalHands = Config.Bind(
                "7. Reverse Hands Compatibility",
                "SingleSpellUsesNormalHands",
                true,
                "When the game's Reverse Hands setting is enabled, use normal hand input whenever exactly one equipped hand is a spell. Reversed input remains available for two-spell loadouts. Disable this to retain the game's behavior for one-spell loadouts.");

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
                    || String.Equals(currentSection, "1. General", StringComparison.Ordinal)
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
                File.WriteAllText(configPath, String.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid,
                    PluginName,
                    storedSchemaVersion,
                    ConfigSchemaVersion);
            }
            catch (Exception exception)
            {
                _pendingPreservedConfigValues.Clear();
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
                        "Failed to restore the Versatile Weapons config backup after a schema reset failure: "
                        + restoreException.GetBaseException().Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset the Versatile Weapons config schema. The original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedConfigValues(
            string configPath,
            int storedSchemaVersion)
        {
            _pendingPreservedConfigValues.Clear();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            CapturePreservedValue<bool>(profile, "1. General", "Enabled");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "FullPotencyStrengthMultiplier");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "DamageAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "DamageAtFullPotency");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "AttackSpeedAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "AttackSpeedAtFullPotency");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "PoiseAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "PoiseAtFullPotency");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "ForceAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "2. Native Two-Handed Weapon - One-Handed Grip", "ForceAtFullPotency");
            CapturePreservedValue<float>(profile, "3. Native One-Handed Weapon - Two-Handed Grip", "DamageMultiplier");
            CapturePreservedValue<float>(profile, "3. Native One-Handed Weapon - Two-Handed Grip", "AttackSpeedMultiplier");
            CapturePreservedValue<float>(profile, "3. Native One-Handed Weapon - Two-Handed Grip", "PoiseMultiplier");
            CapturePreservedValue<float>(profile, "3. Native One-Handed Weapon - Two-Handed Grip", "ForceMultiplier");
            CapturePreservedValue<float>(profile, "3. Native One-Handed Weapon - Two-Handed Grip", "AxeMeleeRangeMultiplier");
            CapturePreservedValue<float>(profile, "3. Native One-Handed Weapon - Two-Handed Grip", "MaceMeleeRangeMultiplier");
            CapturePreservedValue<float>(profile, "5. Advanced First-Person Alignment", "OneHandedSwordPositionY");
            CapturePreservedValue<float>(profile, "5. Advanced First-Person Alignment", "OneHandedMacePositionY");
            CapturePreservedValue<float>(profile, "5. Advanced First-Person Alignment", "OneHandedAxePositionY");
            CapturePreservedValue<float>(profile, "4. Grip Switching", "GripHoldSeconds");
            CapturePreservedValue<bool>(profile, "4. Grip Switching", "ProficiencyFollowsGrip");
            CapturePreservedValue<bool>(profile, "6. Diagnostics", "Enabled");
            CapturePreservedValue<StrengthTestMode>(profile, "6. Diagnostics", "StrengthTestMode");
            CapturePreservedValue<bool>(profile, "6. Diagnostics", "ShowGrailFloatingTextDiagnostics");
            CapturePreservedValue<bool>(profile, "7. Reverse Hands Compatibility", "TwoHandedGripUsesNormalHands");
            CapturePreservedValue<bool>(profile, "7. Reverse Hands Compatibility", "SingleSpellUsesNormalHands");
        }

        private void CapturePreservedValue<T>(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            string section,
            string key)
        {
            T previousValue;
            if (profile.TryGetCustomizedValue(
                section,
                key,
                out previousValue))
            {
                _pendingPreservedConfigValues[
                    new ConfigDefinition(section, key)] = previousValue;
            }
        }

        private void RestorePreservedConfigValues()
        {
            if (_pendingPreservedConfigValues.Count == 0)
            {
                return;
            }

            int restored = 0;
            int clamped = 0;
            int invalid = 0;
            RestorePreservedValue(_enabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_fullPotencyStrengthMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_gripHoldSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_twoHandedOneHandedWeaponDamageMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_twoHandedOneHandedWeaponAttackSpeedMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_twoHandedOneHandedWeaponPoiseMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_twoHandedOneHandedWeaponForceMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_twoHandedOneHandedAxeMeleeRangeMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_twoHandedOneHandedMaceMeleeRangeMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponRequirementDamageMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponFullDamageMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponRequirementAttackSpeedMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponFullAttackSpeedMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponRequirementPoiseMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponFullPoiseMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponRequirementForceMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedTwoHandedWeaponFullForceMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedSwordPositionY, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedMacePositionY, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_oneHandedAxePositionY, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_proficiencyFollowsGrip, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_diagnostics, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_strengthTestMode, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_showGrailFloatingTextDiagnostics, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_twoHandedGripUsesNormalHands, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_singleSpellUsesNormalHands, ref restored, ref clamped, ref invalid);

            Logger.LogInfo(
                "Preserved "
                + restored.ToString(CultureInfo.InvariantCulture)
                + " Versatile Weapons setting(s) across the config schema reset; clamped="
                + clamped.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + invalid.ToString(CultureInfo.InvariantCulture)
                + ".");
            _pendingPreservedConfigValues.Clear();
        }

        private void RestorePreservedValue<T>(
            ConfigEntry<T> entry,
            ref int restored,
            ref int clamped,
            ref int invalid)
        {
            object boxedValue;
            if (entry == null
                || !_pendingPreservedConfigValues.TryGetValue(
                    entry.Definition,
                    out boxedValue)
                || !(boxedValue is T))
            {
                return;
            }

            bool wasClamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                (T)boxedValue,
                out wasClamped))
            {
                invalid++;
                return;
            }

            if (wasClamped)
            {
                clamped++;
            }
            restored++;
        }

        private void OnConfigChanged(object sender, SettingChangedEventArgs args)
        {
            RestoreFirstPersonWeaponPosition();
            ItemEquipEquipmentTypePatch.ClearCache();
            _lastDiagnosticWeaponSignature = null;

            if (_enabled != null && !_enabled.Value)
            {
                CancelGripEquipInputGuard();
                Hero hero = Hero.Current;
                CharacterHand weapon = FindHandForItem(hero, _gripItem)
                    ?? _observedWeapon;
                bool nativeOneHandedWeapon =
                    IsNativeOneHandedGripWeapon(weapon);
                RestoreHiddenPairedHand();
                _twoHandedGrip = false;
                ResetToggleWeaponHold();

                if (hero != null
                    && weapon != null
                    && !weapon.IsHidden
                    && !weapon.IsLoadingAnimator)
                {
                    if (nativeOneHandedWeapon)
                    {
                        RefreshNativeOneHandedWeaponAnimations(
                            hero,
                            weapon,
                            false);
                    }
                    else
                    {
                        RefreshWeaponAnimations(
                            hero,
                            weapon,
                            false);
                    }
                }
            }
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        private bool DiagnosticNotificationsEnabled()
        {
            return _diagnostics != null
                && _diagnostics.Value
                && _showGrailFloatingTextDiagnostics != null
                && _showGrailFloatingTextDiagnostics.Value;
        }

        private void ShowDiagnosticNotification(
            string eventId,
            string text,
            string priority,
            string collapseKey)
        {
            if (!DiagnosticNotificationsEnabled())
            {
                return;
            }

            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowSystemNotification(
                    PluginGuid,
                    eventId,
                    text,
                    priority,
                    collapseKey);
        }

        private void ObserveDiagnosticWeaponState(Hero hero)
        {
            if (!DiagnosticNotificationsEnabled()
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || !hero.IsWeaponEquipped)
            {
                return;
            }

            CharacterHand weapon = FindDiagnosticWeapon(hero);
            Item item = weapon == null ? null : weapon.Item;
            if (item == null || item.Template == null)
            {
                _lastDiagnosticWeaponSignature = null;
                return;
            }

            Item pairedItem = GetPairedItem(hero, item);
            bool nativeOneHanded = IsNativeOneHandedGripWeapon(weapon);
            bool supportedPairing = pairedItem == null
                || IsShield(pairedItem);
            string pairing = DescribeNotificationPairing(pairedItem);
            string signature = item.GetHashCode().ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + pairing
                + "|"
                + supportedPairing;
            if (String.Equals(
                signature,
                _lastDiagnosticWeaponSignature,
                StringComparison.Ordinal))
            {
                return;
            }

            _lastDiagnosticWeaponSignature = signature;
            string family = DescribeWeaponFamily(item.Template);
            string text;
            if (nativeOneHanded)
            {
                text = "VW detected: native one-handed "
                    + family
                    + " | "
                    + pairing
                    + " | two-handed grip ready.";
            }
            else if (!supportedPairing)
            {
                text = "VW detected: native two-handed "
                    + family
                    + " | grip switching blocked: offhand occupied.";
            }
            else
            {
                text = "VW detected: native two-handed "
                    + family
                    + " | "
                    + pairing
                    + " | default "
                    + (pairedItem == null
                        ? "native two-handed"
                        : "one-handed")
                    + " | grip switching ready.";
            }

            ShowDiagnosticNotification(
                "weapon-detected",
                text,
                "Normal",
                "vw-weapon-state");
        }

        private void ShowGripNotification(
            Hero hero,
            CharacterHand weapon,
            bool nativeOneHandedWeapon,
            Item pairedItem)
        {
            if (!DiagnosticNotificationsEnabled()
                || hero == null
                || weapon == null
                || weapon.Item == null)
            {
                return;
            }

            Item item = weapon.Item;
            string grip;
            if (nativeOneHandedWeapon)
            {
                grip = _twoHandedGrip
                    ? "two-handed grip"
                    : "native one-handed grip";
            }
            else
            {
                grip = _twoHandedGrip
                    ? "native two-handed grip"
                    : "one-handed grip";
            }

            string offhand = pairedItem == null
                ? "offhand empty"
                : (_twoHandedGrip
                    ? "offhand stowed"
                    : (IsShield(pairedItem)
                        ? "shield active"
                        : "offhand active"));
            string values = "vanilla combat values";
            if ((nativeOneHandedWeapon && _twoHandedGrip)
                || (!nativeOneHandedWeapon && !_twoHandedGrip))
            {
                values = FormatMultiplier(GetGripDamageMultiplier(item))
                    + " damage, "
                    + FormatMultiplier(GetGripAttackSpeedMultiplier(item))
                    + " speed, "
                    + FormatMultiplier(GetGripPoiseMultiplier(item))
                    + " poise, "
                    + FormatMultiplier(GetGripForceMultiplier(item))
                    + " force, "
                    + FormatMultiplier(GetGripMeleeRangeMultiplier(item))
                    + " melee range";
            }

            values += ", "
                + DescribeProficiency(GetEffectiveProficiency(item, null))
                + " proficiency";

            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowSystemNotification(
                    PluginGuid,
                    "grip-state",
                    "VW: "
                        + DescribeWeaponFamily(item.Template)
                        + " "
                        + grip
                        + " | "
                        + offhand
                        + " | "
                        + values,
                    "Normal",
                    "vw-grip-state");
        }

        private float GetGripAttackSpeedMultiplier(Item item)
        {
            if (IsNativeOneHandedWeaponInTwoHandedGrip(item))
            {
                return _twoHandedOneHandedWeaponAttackSpeedMultiplier == null
                    ? 1.2f
                    : _twoHandedOneHandedWeaponAttackSpeedMultiplier.Value;
            }

            return IsConvertedNativeTwoHandedWeaponInOneHandedGrip(item)
                ? GetStrengthScaledMultiplier(
                    item,
                    _oneHandedTwoHandedWeaponRequirementAttackSpeedMultiplier,
                    0.5f,
                    _oneHandedTwoHandedWeaponFullAttackSpeedMultiplier,
                    0.75f)
                : 1.0f;
        }

        private float GetGripPoiseMultiplier(Item item)
        {
            if (IsNativeOneHandedWeaponInTwoHandedGrip(item))
            {
                return _twoHandedOneHandedWeaponPoiseMultiplier == null
                    ? 1.2f
                    : _twoHandedOneHandedWeaponPoiseMultiplier.Value;
            }

            return IsConvertedNativeTwoHandedWeaponInOneHandedGrip(item)
                ? GetStrengthScaledMultiplier(
                    item,
                    _oneHandedTwoHandedWeaponRequirementPoiseMultiplier,
                    0.6f,
                    _oneHandedTwoHandedWeaponFullPoiseMultiplier,
                    0.95f)
                : 1.0f;
        }

        private float GetGripForceMultiplier(Item item)
        {
            if (IsNativeOneHandedWeaponInTwoHandedGrip(item))
            {
                return _twoHandedOneHandedWeaponForceMultiplier == null
                    ? 1.1f
                    : _twoHandedOneHandedWeaponForceMultiplier.Value;
            }

            return IsConvertedNativeTwoHandedWeaponInOneHandedGrip(item)
                ? GetStrengthScaledMultiplier(
                    item,
                    _oneHandedTwoHandedWeaponRequirementForceMultiplier,
                    0.65f,
                    _oneHandedTwoHandedWeaponFullForceMultiplier,
                    1.0f)
                : 1.0f;
        }

        private float GetGripMeleeRangeMultiplier(Item item)
        {
            if (!IsNativeOneHandedWeaponInTwoHandedGrip(item)
                || item == null)
            {
                return 1.0f;
            }

            if (item.IsAxe)
            {
                return _twoHandedOneHandedAxeMeleeRangeMultiplier == null
                    ? 1.5f
                    : _twoHandedOneHandedAxeMeleeRangeMultiplier.Value;
            }

            if (item.IsBlunt)
            {
                return _twoHandedOneHandedMaceMeleeRangeMultiplier == null
                    ? 1.5f
                    : _twoHandedOneHandedMaceMeleeRangeMultiplier.Value;
            }

            return 1.0f;
        }

        internal float GetCharacterWeaponMeleeRangeMultiplier(
            CharacterWeapon weapon)
        {
            Hero hero = Hero.Current;
            Item item = weapon == null ? null : weapon.Item;
            if (hero == null
                || item == null
                || !ReferenceEquals(FindHandForItem(hero, item), weapon))
            {
                return 1.0f;
            }

            return GetGripMeleeRangeMultiplier(item);
        }

        private static string DescribeGripBlockReason(
            Hero hero,
            CharacterHand weapon,
            bool nativeOneHandedWeapon,
            Item pairedItem)
        {
            if (hero == null || weapon == null || weapon.Item == null)
            {
                return "weapon state changed during the hold";
            }
            if (!hero.IsAlive)
            {
                return "hero is not active";
            }
            if (!hero.IsWeaponEquipped)
            {
                return "weapon is not drawn";
            }
            if (!hero.CanUseEquippedWeapons)
            {
                return "equipped weapons are unavailable";
            }
            if (hero.IsPerformingAction)
            {
                return "character action in progress";
            }
            if (Time.timeScale <= 0f)
            {
                return "game is paused";
            }
            if (!nativeOneHandedWeapon
                && pairedItem != null
                && !IsShield(pairedItem))
            {
                return "offhand pairing is unsupported";
            }

            return "weapon or grip is no longer supported";
        }

        private static string FormatMultiplier(float value)
        {
            return (value * 100.0f).ToString(
                    "0",
                    CultureInfo.InvariantCulture)
                + "%";
        }

        private static string DescribeProficiency(
            ProfStatType proficiency)
        {
            if (proficiency == ProfStatType.OneHanded)
            {
                return "One-Handed";
            }

            if (proficiency == ProfStatType.TwoHanded)
            {
                return "Two-Handed";
            }

            return proficiency == null
                ? "unknown"
                : proficiency.EnumName;
        }

        private static string DescribeNotificationPairing(Item pairedItem)
        {
            if (pairedItem == null)
            {
                return "offhand empty";
            }

            return IsShield(pairedItem)
                ? "shield equipped"
                : "offhand occupied";
        }

        private static string DescribeWeaponFamily(ItemTemplate template)
        {
            if (template == null)
            {
                return "weapon";
            }
            if (template.IsSword)
            {
                return "sword";
            }
            if (template.IsPolearm)
            {
                return "spear";
            }
            if (template.IsAxe)
            {
                return "axe";
            }
            if (template.IsBlunt)
            {
                return "hammer";
            }

            return "weapon";
        }

        internal bool TryCanConvertToOneHanded(
            ItemEquip itemEquip,
            out bool canConvert)
        {
            canConvert = false;
            if (_enabled == null
                || !_enabled.Value
                || itemEquip == null
                || itemEquip.Item == null
                || itemEquip.Item.Template == null
                || !itemEquip.Item.Template.IsMelee)
            {
                return true;
            }

            ItemTemplate template = itemEquip.Item.Template;
            if (!IsSupportedWeaponFamily(template))
            {
                return true;
            }

            canConvert = CanUseNativeTwoHandedWeaponInOneHand(
                itemEquip.Item);
            return true;
        }

        private bool CanUseNativeTwoHandedWeaponInOneHand(Item item)
        {
            return item != null
                && item.Template != null
                && item.Template.IsTwoHanded
                && IsSupportedWeaponFamily(item.Template);
        }

        private void Update()
        {
            if (_enabled == null || !_enabled.Value)
            {
                CancelGripEquipInputGuard();
                RestoreFirstPersonWeaponPosition();
                RestoreHiddenPairedHand();
                ClearObservedWeapon();
                return;
            }

            if (_weaponTransitionRefreshPending
                && --_weaponTransitionRefreshFramesRemaining <= 0)
            {
                _weaponTransitionRefreshPending = false;
            }

            Hero hero = Hero.Current;
            ObserveLoadoutIndex(hero);
            ObserveDiagnosticWeaponState(hero);

            CharacterHand gripWeapon = FindGripSwitchWeapon(hero);
            if (gripWeapon == null || gripWeapon.Item == null)
            {
                _drawnWeaponHiddenSince = -1.0f;
                if (_gripItem != null)
                {
                    RestoreHiddenPairedHand();
                    _gripItem = null;
                    _gripPairedItem = null;
                    _twoHandedGrip = false;
                    ResetToggleWeaponHold();
                }
            }
            else
            {
                ObserveGripItem(gripWeapon.Item);
                if (_twoHandedGrip)
                {
                    HidePairedHandForTwoHandedGrip(hero, gripWeapon);
                }

                MonitorDrawnWeaponVisibility(hero, gripWeapon);
            }

            if (_pairedRefreshStage != PairedRefreshStage.None
                && ProcessPairedRefresh(
                    hero,
                    _pairedRefreshWeapon))
            {
                return;
            }

            if (TryRefreshNativeOneHandedAfterWeaponTransition(
                    hero,
                    gripWeapon))
            {
                return;
            }

            if (hero == null || !hero.IsWeaponEquipped)
            {
                _gripFsmMismatchFrames = 0;
                return;
            }

            if (IsNativeOneHandedGripWeapon(gripWeapon))
            {
                Item pairedItem = GetPairedItem(hero, gripWeapon.Item);
                bool canReconcileNativeGrip = _twoHandedGrip
                    || pairedItem == null
                    || IsShield(pairedItem);
                if (!canReconcileNativeGrip
                    || gripWeapon.IsHidden
                    || !NativeGripAnimatorIsReady(hero, gripWeapon))
                {
                    _gripFsmMismatchFrames = 0;
                    return;
                }

                bool useOneHandedFsm = !_twoHandedGrip;
                if (GripFsmMatches(hero, useOneHandedFsm))
                {
                    _gripFsmMismatchFrames = 0;
                    return;
                }

                ReconcileGripFsmState(hero, useOneHandedFsm);
                if (++_gripFsmMismatchFrames < GripFsmRecoveryFrames)
                {
                    return;
                }

                _gripFsmMismatchFrames = 0;
                Logger.LogWarning(
                    "Recovered a native one-handed weapon whose equipment transition left conflicting melee FSMs active. "
                    + DescribeGripContext(hero, gripWeapon));
                ShowDiagnosticNotification(
                    "grip-fsm-recovery",
                    "VW recovered: equipment animation/FSM mismatch; check the BepInEx log.",
                    "High",
                    "vw-recovery");
                RefreshNativeOneHandedWeaponAnimations(
                    hero,
                    gripWeapon,
                    _twoHandedGrip);
                return;
            }

            CharacterHand weapon = FindConvertedTwoHandedGripWeapon(hero);

            if (weapon == null || weapon.Item == null)
            {
                ClearObservedWeapon();
                return;
            }

            bool desiredState = ShouldUseOneHandedAnimations(weapon);
            if (!ReferenceEquals(_observedWeapon, weapon))
            {
                _observedWeapon = weapon;
                _observedAnimationStateKnown = false;

                if (desiredState)
                {
                    RequestOneHandedReconciliation();
                }
            }

            if (_weaponTransitionRefreshPending
                && desiredState
                && _observedAnimationStateKnown
                && _observedAnimationState
                && !weapon.IsHidden
                && HandAnimationsAreSettled(
                    hero,
                    weapon,
                    true))
            {
                BeginPairedRefresh(hero, weapon);
                return;
            }

            if (!desiredState)
            {
                _oneHandedReconciliationPending = false;
            }
            else if (_oneHandedReconciliationPending
                && _observedAnimationStateKnown
                && _observedAnimationState
                && !weapon.IsHidden
                && HandAnimationsAreSettled(
                    hero,
                    weapon,
                    true))
            {
                ReconcileOneHandedAnimationState(hero);
                _observedAnimationState = true;
                _observedAnimationStateKnown = true;
                _oneHandedReconciliationPending = false;

                Logger.LogInfo(
                    "Finalized the one-handed sword and shield animation state after both hand animators settled.");
            }

            if (!_observedAnimationStateKnown
                || weapon.IsHidden
                || !HandAnimationsAreSettled(
                    hero,
                    weapon,
                    desiredState))
            {
                _gripFsmMismatchFrames = 0;
                return;
            }

            if (_observedAnimationState != desiredState)
            {
                _gripFsmMismatchFrames = 0;
                RefreshWeaponAnimations(hero, weapon, desiredState);
                return;
            }

            if (GripFsmMatches(hero, desiredState))
            {
                _gripFsmMismatchFrames = 0;
                return;
            }

            ReconcileGripFsmState(hero, desiredState);
            if (++_gripFsmMismatchFrames < GripFsmRecoveryFrames)
            {
                return;
            }

            _gripFsmMismatchFrames = 0;
            Logger.LogWarning(
                "Recovered a converted weapon whose visible state did not match its active combat FSM. "
                + DescribeGripContext(hero, weapon));
            ShowDiagnosticNotification(
                "grip-fsm-recovery",
                "VW recovered: grip animation/FSM mismatch; check the BepInEx log.",
                "High",
                "vw-recovery");
            RefreshWeaponAnimations(hero, weapon, desiredState);
        }

        private void LateUpdate()
        {
            UpdateFirstPersonWeaponPosition();
        }

        private void UpdateFirstPersonWeaponPosition()
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindNativeOneHandedGripWeapon(hero);
            Item item = weapon == null ? null : weapon.Item;
            float positionY = 0.0f;
            if (item != null && item.Template != null)
            {
                if (item.Template.IsAxe)
                {
                    positionY = _oneHandedAxePositionY.Value;
                }
                else if (item.Template.IsBlunt)
                {
                    positionY = _oneHandedMacePositionY.Value;
                }
                else if (item.Template.IsSword)
                {
                    positionY = _oneHandedSwordPositionY.Value;
                }
            }
            bool shouldAdjust = _enabled != null
                && _enabled.Value
                && hero != null
                && hero.IsAlive
                && hero.IsWeaponEquipped
                && !Hero.TppActive
                && weapon != null
                && item != null
                && item.Template != null
                && (item.Template.IsSword
                    || item.Template.IsBlunt
                    || item.Template.IsAxe)
                && IsUsingTwoHandedGrip(item)
                && !weapon.IsHidden
                && !weapon.IsLoadingAnimator
                && !Mathf.Approximately(positionY, 0.0f);

            if (!shouldAdjust)
            {
                RestoreFirstPersonWeaponPosition();
                return;
            }

            Transform weaponTransform = weapon.transform;
            if (weaponTransform == null)
            {
                RestoreFirstPersonWeaponPosition();
                return;
            }

            if (_adjustedFirstPersonWeaponTransform != weaponTransform)
            {
                RestoreFirstPersonWeaponPosition();
                _adjustedFirstPersonWeaponTransform = weaponTransform;
                _originalFirstPersonWeaponLocalPosition =
                    weaponTransform.localPosition;
                _originalFirstPersonWeaponLocalRotation =
                    weaponTransform.localRotation;
                LogDiagnostic(
                    "Captured the first-person weapon transform before applying its optional two-handed-grip Y correction.");
            }

            weaponTransform.localPosition =
                _originalFirstPersonWeaponLocalPosition
                + _originalFirstPersonWeaponLocalRotation
                    * new Vector3(0.0f, positionY, 0.0f);
        }

        private void RestoreFirstPersonWeaponPosition()
        {
            if (_adjustedFirstPersonWeaponTransform == null)
            {
                _adjustedFirstPersonWeaponTransform = null;
                return;
            }

            _adjustedFirstPersonWeaponTransform.localPosition =
                _originalFirstPersonWeaponLocalPosition;
            _adjustedFirstPersonWeaponTransform = null;
            LogDiagnostic(
                "Restored the original first-person weapon position.");
        }

        private void OnDestroy()
        {
            CancelGripEquipInputGuard();
            RestoreFirstPersonWeaponPosition();
            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, _gripItem)
                ?? _observedWeapon;
            bool restoreNativeOneHandedWeapon =
                IsNativeOneHandedGripWeapon(weapon);
            _twoHandedGrip = false;

            Config.SettingChanged -= OnConfigChanged;

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (weapon != null
                && !weapon.IsHidden
                && !weapon.IsLoadingAnimator
                && hero != null)
            {
                if (restoreNativeOneHandedWeapon)
                {
                    RefreshNativeOneHandedWeaponAnimations(
                        hero,
                        weapon,
                        false);
                }
                else
                {
                    RefreshWeaponAnimations(
                        hero,
                        weapon,
                        false);
                }
            }

            RestoreHiddenPairedHand();

            ClearObservedWeapon();
            Instance = null;
        }

        private void ObserveGripItem(Item item)
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, item);
            Item pairedItem = GetPairedItem(hero, item);
            if (ReferenceEquals(_gripItem, item))
            {
                if (!ReferenceEquals(_gripPairedItem, pairedItem))
                {
                    CancelGripEquipInputGuard();
                    RestoreHiddenPairedHand();
                    _gripPairedItem = pairedItem;
                    RecordWeaponTransition();
                    bool defaultTwoHandedGrip =
                        IsConvertedTwoHandedGripWeapon(weapon)
                        && pairedItem == null;
                    if (_twoHandedGrip != defaultTwoHandedGrip)
                    {
                        _twoHandedGrip = defaultTwoHandedGrip;
                        _observedAnimationStateKnown = false;
                        if (!defaultTwoHandedGrip)
                        {
                            RequestOneHandedReconciliation();
                        }
                        LogDiagnostic(
                            "The opposite-hand pairing changed; restored the supported weapon's default grip. "
                            + DescribeGripContext(hero, weapon));
                    }
                    else
                    {
                        LogDiagnostic(
                            "The observed weapon's opposite-hand pairing changed. "
                            + DescribeGripContext(hero, weapon));
                    }
                }

                return;
            }

            CancelGripEquipInputGuard();
            RestoreHiddenPairedHand();
            _gripItem = item;
            _gripPairedItem = pairedItem;
            RecordWeaponTransition();
            _twoHandedGrip = IsConvertedTwoHandedGripWeapon(weapon)
                && pairedItem == null;
            _drawnWeaponHiddenSince = -1.0f;
            ResetToggleWeaponHold();
            LogDiagnostic(
                "Observed a supported weapon; selected its default grip. "
                + DescribeGripContext(hero, weapon));
        }

        internal bool IsUsingTwoHandedGrip(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (ReferenceEquals(_gripItem, item))
            {
                return _twoHandedGrip;
            }

            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, item);
            return IsConvertedTwoHandedGripWeapon(weapon)
                && GetPairedItem(hero, item) == null;
        }

        private bool CanClaimGripInput(Hero hero)
        {
            if (hero == null || !hero.IsWeaponEquipped)
            {
                return false;
            }

            CharacterHand weapon = FindGripSwitchWeapon(hero);
            if (weapon == null || weapon.Item == null)
            {
                return false;
            }

            Item pairedItem = GetPairedItem(hero, weapon.Item);
            return IsNativeOneHandedGripWeapon(weapon)
                || (IsConvertedTwoHandedGripWeapon(weapon)
                    && (pairedItem == null || IsShield(pairedItem)));
        }

        private bool TryToggleGrip(Hero hero)
        {
            CharacterHand weapon = FindGripSwitchWeapon(hero);
            bool nativeOneHandedWeapon = IsNativeOneHandedGripWeapon(weapon);
            Item pairedItem = weapon == null
                ? null
                : GetPairedItem(hero, weapon.Item);
            if (hero == null
                || weapon == null
                || weapon.Item == null
                || !hero.IsAlive
                || !hero.IsWeaponEquipped
                || !hero.CanUseEquippedWeapons
                || hero.IsPerformingAction
                || Time.timeScale <= 0f
                || (!nativeOneHandedWeapon
                    && (!IsConvertedTwoHandedGripWeapon(weapon)
                        || (pairedItem != null
                            && !IsShield(pairedItem)))))
            {
                ShowDiagnosticNotification(
                    "grip-blocked",
                    "VW grip blocked: "
                        + DescribeGripBlockReason(
                            hero,
                            weapon,
                            nativeOneHandedWeapon,
                            pairedItem)
                        + ".",
                    "Normal",
                    "vw-grip-blocked");
                LogDiagnostic(
                    "Grip change rejected because the hero, weapon pairing, or action state was not safe. "
                    + DescribeGripContext(hero, weapon));
                return false;
            }

            ObserveGripItem(weapon.Item);
            bool previousTwoHandedGrip = _twoHandedGrip;
            _twoHandedGrip = !_twoHandedGrip;
            StartGripEquipInputGuard(weapon.Item);
            _drawnWeaponHiddenSince = -1.0f;
            LogDiagnostic(
                "Grip transition started; from="
                + (previousTwoHandedGrip ? "two-handed" : "one-handed")
                + "; to="
                + (_twoHandedGrip ? "two-handed" : "one-handed")
                + "; "
                + DescribeGripContext(hero, weapon));

            if (nativeOneHandedWeapon)
            {
                _weaponTransitionRefreshPending = false;
                RefreshNativeOneHandedWeaponAnimations(
                    hero,
                    weapon,
                    _twoHandedGrip);
            }
            else
            {
                _observedAnimationStateKnown = true;
                _observedAnimationState = !_twoHandedGrip;

                if (_twoHandedGrip)
                {
                    RefreshWeaponAnimations(
                        hero,
                        weapon,
                        false);
                }
                else
                {
                    if (IsShield(pairedItem))
                    {
                        BeginPairedRefresh(hero, weapon);
                    }
                    else
                    {
                        RefreshWeaponAnimations(
                            hero,
                            weapon,
                            true);
                    }
                }
            }

            Logger.LogInfo(
                nativeOneHandedWeapon
                    ? (_twoHandedGrip
                        ? "Changed the one-handed weapon to a two-handed grip and stowed its offhand item."
                        : "Restored the weapon's one-handed grip and offhand item.")
                    : (_twoHandedGrip
                        ? (IsShield(pairedItem)
                            ? "Changed the shielded two-handed weapon to its native grip and stowed its shield."
                            : "Changed the weapon to its native two-handed grip.")
                        : (IsShield(pairedItem)
                            ? "Changed the two-handed weapon to a one-handed grip and restored its shield."
                            : "Changed the two-handed weapon to a one-handed grip with an empty opposite hand.")));
            ShowGripNotification(hero, weapon, nativeOneHandedWeapon, pairedItem);
            return true;
        }

        private void StartGripEquipInputGuard(Item item)
        {
            _gripEquipInputGuardActive = item != null;
            _gripEquipInputGuardItem = item;
            _gripEquipInputGuardSawEquipState = false;
            _gripEquipInputGuardStartedAt = Time.unscaledTime;
        }

        private void CancelGripEquipInputGuard()
        {
            _gripEquipInputGuardActive = false;
            _gripEquipInputGuardItem = null;
            _gripEquipInputGuardSawEquipState = false;
            _gripEquipInputGuardStartedAt = 0.0f;
        }

        internal bool ShouldSuppressMeleeInput(MeleeFSM meleeFsm)
        {
            if (!_gripEquipInputGuardActive || meleeFsm == null)
            {
                return false;
            }

            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(
                hero,
                _gripEquipInputGuardItem);
            if (hero == null
                || weapon == null
                || weapon.Item == null
                || !ReferenceEquals(
                    weapon.Item,
                    _gripEquipInputGuardItem)
                || !hero.IsAlive
                || !hero.IsWeaponEquipped)
            {
                CancelGripEquipInputGuard();
                return false;
            }

            bool twoHandedGrip =
                IsUsingTwoHandedGrip(_gripEquipInputGuardItem);
            if ((twoHandedGrip && !(meleeFsm is TwoHandedFSM))
                || (!twoHandedGrip && !(meleeFsm is OneHandedFSM)))
            {
                return false;
            }

            if (Time.unscaledTime - _gripEquipInputGuardStartedAt
                >= GripEquipInputGuardTimeoutSeconds)
            {
                Logger.LogWarning(
                    "Grip equip input guard timed out; restored normal melee input. "
                    + DescribeGripContext(hero, weapon));
                ShowDiagnosticNotification(
                    "grip-equip-input-timeout",
                    "VW recovery warning: grip equip did not settle; check the BepInEx log.",
                    "High",
                    "vw-recovery");
                CancelGripEquipInputGuard();
                return false;
            }

            HeroStateType currentState = meleeFsm.CurrentStateType;
            HeroStateType targetState = meleeFsm.CurrentStateToEnterType;
            if (currentState == HeroStateType.EquipWeapon
                || currentState == HeroStateType.EquipWeaponAlternate
                || targetState == HeroStateType.EquipWeapon
                || targetState == HeroStateType.EquipWeaponAlternate)
            {
                _gripEquipInputGuardSawEquipState = true;
                return true;
            }

            if (_gripEquipInputGuardSawEquipState
                && (currentState == HeroStateType.Idle
                    || currentState == HeroStateType.IdleAlternate
                    || currentState == HeroStateType.Movement
                    || currentState == HeroStateType.MovementAlternate))
            {
                LogDiagnostic(
                    "Released the grip equip input guard after the new melee FSM reached its stable idle or movement state. "
                    + DescribeGripContext(hero, weapon));
                CancelGripEquipInputGuard();
                return false;
            }

            if (!_gripEquipInputGuardSawEquipState
                && !weapon.IsLoadingAnimator
                && AnimatorLayersAreReady(hero, weapon)
                && (currentState == HeroStateType.Idle
                    || currentState == HeroStateType.IdleAlternate
                    || currentState == HeroStateType.Movement
                    || currentState == HeroStateType.MovementAlternate))
            {
                LogDiagnostic(
                    "Released the grip equip input guard after observing an already settled melee FSM. "
                    + DescribeGripContext(hero, weapon));
                CancelGripEquipInputGuard();
                return false;
            }

            return true;
        }

        internal bool HandleToggleWeaponInput(
            UIEvent inputEvent,
            ref UIResult result)
        {
            if (_enabled == null || !_enabled.Value)
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
                if (!CanClaimGripInput(hero))
                {
                    CharacterHand rejectedWeapon =
                        hero.MainHandWeapon as CharacterHand
                        ?? hero.OffHandWeapon as CharacterHand;
                    LogDiagnostic(
                        "Toggle Weapon press was not claimed because the current weapon, Strength, pairing, or action state does not support grip switching. "
                        + DescribeGripContext(hero, rejectedWeapon));
                    return true;
                }

                _toggleWeaponHeld = true;
                _gripAttemptedForHold = false;
                _gripChangedForHold = false;
                _toggleWeaponPressedAt = Time.unscaledTime;
                LogDiagnostic(
                    "Claimed Toggle Weapon press for grip hold detection. "
                    + DescribeGripContext(
                        hero,
                        FindGripSwitchWeapon(hero)));
                result = UIResult.Accept;
                return false;
            }

            if (!_toggleWeaponHeld)
            {
                return true;
            }

            if (inputEvent is UIKeyHeldAction)
            {
                float holdSeconds = _gripHoldSeconds == null
                    ? 0.45f
                    : Math.Max(0.2f, _gripHoldSeconds.Value);
                if (!_gripAttemptedForHold
                    && Time.unscaledTime - _toggleWeaponPressedAt
                        >= holdSeconds)
                {
                    _gripAttemptedForHold = true;
                    _gripChangedForHold = TryToggleGrip(hero);
                    LogDiagnostic(
                        "Grip hold threshold reached; changedGrip="
                        + _gripChangedForHold
                        + ".");
                }

                result = UIResult.Accept;
                return false;
            }

            if (inputEvent is UIKeyUpAction)
            {
                bool toggleWeapon = !_gripChangedForHold;
                ResetToggleWeaponHold();
                if (toggleWeapon)
                {
                    LogDiagnostic(
                        "Toggle Weapon was released without a completed grip change; forwarding the normal sheathe or draw action.");
                    ToggleHeroWeapon(hero);
                }
                else
                {
                    LogDiagnostic(
                        "Toggle Weapon release was consumed after the grip hold, so no sheathe or draw action was forwarded.");
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
            _gripAttemptedForHold = false;
            _gripChangedForHold = false;
            _toggleWeaponPressedAt = 0f;
        }

        internal void RecordAnimatorLoad(CharacterHand weapon)
        {
            MarkHandAnimatorLoading(weapon);

            if (weapon != null
                && weapon.Item != null
                && (ReferenceEquals(weapon.Item, _gripItem)
                    || ReferenceEquals(weapon.Item, _gripPairedItem)))
            {
                LogDiagnostic(
                    "Animator override reload requested for the "
                    + (ReferenceEquals(weapon.Item, _gripItem)
                        ? "grip weapon. "
                        : "paired hand. ")
                    + DescribeGripContext(Hero.Current, FindHandForItem(Hero.Current, _gripItem)));
            }

            if (!IsConvertedTwoHandedGripWeapon(weapon))
            {
                if (HasShieldedConvertedWeapon())
                {
                    RequestOneHandedReconciliation();
                }

                return;
            }

            _observedWeapon = weapon;
            _observedAnimationState =
                ShouldUseOneHandedAnimations(weapon);
            _observedAnimationStateKnown = true;

            if (_observedAnimationState)
            {
                RequestOneHandedReconciliation();
            }
        }

        internal void RecordAnimatorLayersApplied(
            CharacterHand hand,
            bool activate)
        {
            if (!activate || hand == null || hand.Item == null)
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null)
            {
                return;
            }

            if (ReferenceEquals(hand.Item, hero.MainHandItem))
            {
                _readyMainHand = hand;
            }
            else if (ReferenceEquals(hand.Item, hero.OffHandItem))
            {
                _readyOffHand = hand;
            }

            if (ReferenceEquals(hand.Item, _gripItem)
                || ReferenceEquals(hand.Item, _gripPairedItem))
            {
                LogDiagnostic(
                    "Animator layers became ready for the "
                    + (ReferenceEquals(hand.Item, _gripItem)
                        ? "grip weapon. "
                        : "paired hand. ")
                    + DescribeGripContext(hero, FindHandForItem(hero, _gripItem)));
            }

            if (HasShieldedConvertedWeapon())
            {
                RequestOneHandedReconciliation();
            }
        }

        internal void RecordWeaponTransition()
        {
            _weaponTransitionRefreshPending = true;
            _weaponTransitionRefreshFramesRemaining =
                WeaponTransitionRefreshWindowFrames;
        }

        private void ObserveLoadoutIndex(Hero hero)
        {
            HeroItems heroItems =
                hero == null ? null : hero.TryGetElement<HeroItems>();
            if (heroItems == null)
            {
                _observedLoadoutIndexKnown = false;
                return;
            }

            int currentIndex = heroItems.CurrentLoadoutIndex;
            if (!_observedLoadoutIndexKnown)
            {
                _observedLoadoutIndex = currentIndex;
                _observedLoadoutIndexKnown = true;
                return;
            }

            if (_observedLoadoutIndex == currentIndex)
            {
                return;
            }

            _observedLoadoutIndex = currentIndex;
            RecordWeaponTransition();
        }

        private bool TryRefreshNativeOneHandedAfterWeaponTransition(
            Hero hero,
            CharacterHand weapon)
        {
            if (!_weaponTransitionRefreshPending
                || hero == null
                || weapon == null
                || weapon.Item == null
                || _twoHandedGrip
                || !IsNativeOneHandedGripWeapon(weapon)
                || !hero.IsWeaponEquipped
                || hero.IsPerformingAction
                || weapon.IsHidden
                || _pairedRefreshStage != PairedRefreshStage.None
                || !HandAnimationsAreSettled(
                    hero,
                    weapon,
                    false))
            {
                return false;
            }

            _weaponTransitionRefreshPending = false;
            RefreshNativeOneHandedWeaponAnimations(
                hero,
                weapon,
                false);
            Logger.LogInfo(
                "Refreshed a native one-handed weapon once after its equipment transition settled. "
                + DescribeGripContext(hero, weapon));
            return true;
        }

        internal static bool ShouldUseOneHandedAnimations(
            CharacterHand weapon)
        {
            if (!IsConvertedTwoHandedGripWeapon(weapon))
            {
                return false;
            }

            Hero hero = Hero.Current;
            Item pairedItem = GetPairedItem(hero, weapon.Item);
            return (pairedItem == null || IsShield(pairedItem))
                && Instance != null
                && Instance._enabled != null
                && Instance._enabled.Value
                && !Instance.IsUsingTwoHandedGrip(weapon.Item);
        }

        internal static bool ShouldUseTwoHandedAnimations(
            CharacterHand weapon)
        {
            return IsNativeOneHandedGripWeapon(weapon)
                && Instance != null
                && Instance._enabled != null
                && Instance._enabled.Value
                && Instance.IsUsingTwoHandedGrip(weapon.Item);
        }

        internal static bool HasShieldedConvertedWeapon()
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindConvertedTwoHandedGripWeapon(hero);
            return ShouldUseOneHandedAnimations(weapon);
        }

        internal static bool HasSupportedConvertedPairing()
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindConvertedTwoHandedGripWeapon(hero);
            Item pairedItem = weapon == null || weapon.Item == null
                ? null
                : GetPairedItem(hero, weapon.Item);
            return Instance != null
                && Instance._enabled != null
                && Instance._enabled.Value
                && weapon != null
                && weapon.Item != null
                && (pairedItem == null || IsShield(pairedItem));
        }

        internal static bool ShouldSuppressDualWielding()
        {
            if (HasSupportedConvertedPairing())
            {
                return true;
            }

            Hero hero = Hero.Current;
            return ShouldUseTwoHandedAnimations(
                FindNativeOneHandedGripWeapon(hero));
        }

        internal static bool ShouldTreatAsOneHanded(Item item)
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, item);
            if (!IsConvertedTwoHandedGripWeapon(weapon))
            {
                return false;
            }

            Item pairedItem = GetPairedItem(hero, item);
            return (pairedItem == null || IsShield(pairedItem))
                && Instance != null
                && Instance._enabled != null
                && Instance._enabled.Value
                && !Instance.IsUsingTwoHandedGrip(item);
        }

        internal static bool ShouldTreatAsTwoHanded(Item item)
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, item);
            return ShouldUseTwoHandedAnimations(weapon);
        }

        internal static ProfStatType GetNativeProficiency(Item item)
        {
            return item == null
                ? null
                : ProfUtils.ProfFromAbstracts(
                    item.Template,
                    ProfUtils.ProfReferences(),
                    suppressErrorLog: true);
        }

        internal ProfStatType GetEffectiveProficiency(
            Item item,
            ProfStatType nativeProficiency)
        {
            if (nativeProficiency == null && item != null)
            {
                nativeProficiency = GetNativeProficiency(item);
            }

            if (_enabled == null
                || !_enabled.Value
                || _proficiencyFollowsGrip == null
                || !_proficiencyFollowsGrip.Value
                || item == null)
            {
                return nativeProficiency;
            }

            CharacterHand weapon = FindHandForItem(Hero.Current, item);
            if (!IsNativeOneHandedGripWeapon(weapon)
                && !IsConvertedTwoHandedGripWeapon(weapon))
            {
                return nativeProficiency;
            }

            return IsUsingTwoHandedGrip(item)
                ? ProfStatType.TwoHanded
                : ProfStatType.OneHanded;
        }

        internal static void DisableConflictingFsms()
        {
            Hero hero = Hero.Current;
            CharacterHand convertedWeapon =
                FindConvertedTwoHandedGripWeapon(hero);
            bool useOneHanded =
                ShouldUseOneHandedAnimations(convertedWeapon);
            bool useConvertedNativeTwoHanded = convertedWeapon != null
                && convertedWeapon.Item != null
                && Instance != null
                && Instance.IsUsingTwoHandedGrip(convertedWeapon.Item);
            bool useTwoHanded = useConvertedNativeTwoHanded
                || ShouldUseTwoHandedAnimations(
                    FindNativeOneHandedGripWeapon(hero));
            if (!useOneHanded && !useTwoHanded)
            {
                return;
            }

            if (hero == null)
            {
                return;
            }

            TwoHandedFSM twoHanded =
                hero.TryGetElement<TwoHandedFSM>();
            OneHandedFSM oneHanded =
                hero.TryGetElement<OneHandedFSM>();
            DualHandedFSM dualHanded =
                hero.TryGetElement<DualHandedFSM>();

            if (useOneHanded && twoHanded != null)
            {
                twoHanded.DisableFSM();
            }

            if (useTwoHanded && oneHanded != null)
            {
                oneHanded.DisableFSM();
            }

            if (dualHanded != null)
            {
                dualHanded.DisableFSM();
            }

            bool suppressMainHand = IsMainHandSuppressed();
            bool suppressOffHand = IsOffHandSuppressed();
            if (suppressMainHand || suppressOffHand)
            {
                foreach (MagicFSM magicFsm in hero.Elements<MagicFSM>())
                {
                    if ((suppressMainHand
                            && magicFsm.CastingHand == CastingHand.MainHand)
                        || (suppressOffHand
                            && magicFsm.CastingHand == CastingHand.OffHand))
                    {
                        magicFsm.DisableFSM();
                    }
                }

                if (suppressOffHand)
                {
                    MagicMeleeOffHandFSM magicMeleeOffHand =
                        hero.TryGetElement<MagicMeleeOffHandFSM>();
                    if (magicMeleeOffHand != null)
                    {
                        magicMeleeOffHand.DisableFSM();
                    }
                }
            }
        }

        private static void ReconcileOneHandedAnimationState(Hero hero)
        {
            DisableConflictingFsms();

            OneHandedFSM oneHanded =
                hero == null ? null : hero.TryGetElement<OneHandedFSM>();
            if (oneHanded != null)
            {
                oneHanded.EnableFSM();
            }
        }

        private static void ReconcileGripFsmState(
            Hero hero,
            bool useOneHanded)
        {
            if (hero == null)
            {
                return;
            }

            OneHandedFSM oneHanded =
                hero.TryGetElement<OneHandedFSM>();
            TwoHandedFSM twoHanded =
                hero.TryGetElement<TwoHandedFSM>();
            DualHandedFSM dualHanded =
                hero.TryGetElement<DualHandedFSM>();

            if (dualHanded != null)
            {
                dualHanded.DisableFSM();
            }

            if (useOneHanded)
            {
                if (twoHanded != null)
                {
                    twoHanded.DisableFSM();
                }
                if (oneHanded != null)
                {
                    oneHanded.EnableFSM();
                }
                return;
            }

            if (oneHanded != null)
            {
                oneHanded.DisableFSM();
            }
            if (twoHanded != null)
            {
                twoHanded.EnableFSM();
            }
        }

        private static bool GripFsmMatches(
            Hero hero,
            bool useOneHanded)
        {
            if (hero == null)
            {
                return false;
            }

            OneHandedFSM oneHanded =
                hero.TryGetElement<OneHandedFSM>();
            TwoHandedFSM twoHanded =
                hero.TryGetElement<TwoHandedFSM>();
            DualHandedFSM dualHanded =
                hero.TryGetElement<DualHandedFSM>();

            bool oneHandedActive = oneHanded != null
                && oneHanded.IsLayerActive;
            bool twoHandedActive = twoHanded != null
                && twoHanded.IsLayerActive;
            bool dualHandedActive = dualHanded != null
                && dualHanded.IsLayerActive;
            return useOneHanded
                ? oneHandedActive
                    && !twoHandedActive
                    && !dualHandedActive
                : twoHandedActive
                    && !oneHandedActive
                    && !dualHandedActive;
        }

        internal static ARAssetReference CreateOneHandedWeaponController(
            CharacterHand weapon,
            ARAssetReference current)
        {
            Hero hero = Hero.Current;
            if (hero != null
                && ReferenceEquals(weapon.Item, hero.OffHandItem))
            {
                GameConstants constants = GetGameConstants();
                if (constants != null)
                {
                    ARAssetReference offHandController = Hero.TppActive
                        ? constants.defaultMeleeOffHandTpp.Get()
                        : constants.defaultMeleeOffHand.Get();
                    if (offHandController != null
                        && offHandController.IsSet)
                    {
                        return offHandController;
                    }
                }

                return current;
            }

            Item item = weapon == null ? null : weapon.Item;
            ItemTemplate template = item == null ? null : item.Template;
            if (template != null)
            {
                if (template.IsPolearm)
                {
                    return new ARAssetReference(
                        Hero.TppActive
                            ? OneHandedPolearmTppAddress
                            : OneHandedPolearmFppAddress);
                }

                if (template.IsAxe)
                {
                    return new ARAssetReference(
                        Hero.TppActive
                            ? OneHandedSwordTppAddress
                            : OneHandedAxeFppAddress);
                }

                if (template.IsBlunt)
                {
                    return new ARAssetReference(
                        Hero.TppActive
                            ? OneHandedSwordTppAddress
                            : OneHandedBluntFppAddress);
                }
            }

            return new ARAssetReference(
                Hero.TppActive
                    ? OneHandedSwordTppAddress
                    : OneHandedSwordFppAddress);
        }

        internal static ARAssetReference CreateTwoHandedWeaponController(
            CharacterHand weapon)
        {
            Item item = weapon == null ? null : weapon.Item;
            if (item != null && item.Template != null)
            {
                if (item.Template.IsPolearm)
                {
                    return new ARAssetReference(
                        Hero.TppActive
                            ? TwoHandedPolearmTppAddress
                            : TwoHandedPolearmFppAddress);
                }

                if (item.Template.IsAxe || item.Template.IsBlunt)
                {
                    return new ARAssetReference(
                        Hero.TppActive
                            ? TwoHandedAxeTppAddress
                            : TwoHandedAxeFppAddress);
                }

            }

            return new ARAssetReference(
                Hero.TppActive
                    ? TwoHandedSwordTppAddress
                    : TwoHandedSwordFppAddress);
        }

        internal static ARAssetReference CreateStandardDualController(
            bool mainHand,
            ARAssetReference current)
        {
            GameConstants constants = GetGameConstants();
            if (constants == null)
            {
                return current;
            }

            ARAssetReference controller;
            if (Hero.TppActive)
            {
                controller = mainHand
                    ? constants.defaultDualWieldingMainHandTpp.Get()
                    : constants.defaultDualWieldingOffHandTpp.Get();
            }
            else
            {
                controller = mainHand
                    ? constants.defaultDualWieldingMainHand.Get()
                    : constants.defaultDualWieldingOffHand.Get();
            }

            return controller != null && controller.IsSet
                ? controller
                : current;
        }

        internal static string[] GetOneHandedLayers()
        {
            return OneHandedLayers;
        }

        internal static string[] GetTwoHandedLayers()
        {
            return TwoHandedLayers;
        }

        private static bool IsSupportedWeaponFamily(
            ItemTemplate template)
        {
            return template != null
                && template.IsMelee
                && !template.IsTool
                && !template.IsDagger
                && !template.IsRod
                && !template.IsMagic
                && !template.IsRanged
                && (template.IsSword
                    || template.IsAxe
                    || template.IsBlunt
                    || template.IsPolearm);
        }

        private static bool IsConvertedTwoHandedGripWeapon(
            CharacterHand weapon)
        {
            Hero hero = Hero.Current;
            Item item = weapon == null ? null : weapon.Item;

            if (hero == null
                || item == null
                || item.Template == null
                || (!ReferenceEquals(item, hero.MainHandItem)
                    && !ReferenceEquals(item, hero.OffHandItem))
                || !item.Template.IsTwoHanded
                || !IsSupportedWeaponFamily(item.Template)
                || Instance == null
                || Instance._enabled == null
                || !Instance._enabled.Value)
            {
                return false;
            }

            return Instance.CanUseNativeTwoHandedWeaponInOneHand(item);
        }

        private static bool IsNativeOneHandedGripWeapon(
            CharacterHand weapon)
        {
            Hero hero = Hero.Current;
            Item item = weapon == null ? null : weapon.Item;
            return hero != null
                && item != null
                && item.Template != null
                && ReferenceEquals(item, hero.MainHandItem)
                && item.Template.IsOneHanded
                && IsSupportedWeaponFamily(item.Template);
        }

        private static CharacterHand FindConvertedTwoHandedGripWeapon(
            Hero hero)
        {
            if (hero == null)
            {
                return null;
            }

            CharacterHand mainHand = hero.MainHandWeapon as CharacterHand;
            if (IsConvertedTwoHandedGripWeapon(mainHand))
            {
                return mainHand;
            }

            CharacterHand offHand = hero.OffHandWeapon as CharacterHand;
            return IsConvertedTwoHandedGripWeapon(offHand)
                ? offHand
                : null;
        }

        private static CharacterHand FindNativeOneHandedGripWeapon(
            Hero hero)
        {
            if (hero == null)
            {
                return null;
            }

            CharacterHand mainHand = hero.MainHandWeapon as CharacterHand;
            return IsNativeOneHandedGripWeapon(mainHand)
                ? mainHand
                : null;
        }

        private static CharacterHand FindDiagnosticWeapon(Hero hero)
        {
            if (hero == null)
            {
                return null;
            }

            CharacterHand mainHand = hero.MainHandWeapon as CharacterHand;
            if (mainHand != null
                && mainHand.Item != null
                && mainHand.Item.Template != null
                && IsSupportedWeaponFamily(mainHand.Item.Template)
                && (mainHand.Item.Template.IsOneHanded
                    || mainHand.Item.Template.IsTwoHanded))
            {
                return mainHand;
            }

            CharacterHand offHand = hero.OffHandWeapon as CharacterHand;
            return offHand != null
                && offHand.Item != null
                && offHand.Item.Template != null
                && offHand.Item.Template.IsTwoHanded
                && IsSupportedWeaponFamily(offHand.Item.Template)
                ? offHand
                : null;
        }

        private static CharacterHand FindGripSwitchWeapon(Hero hero)
        {
            CharacterHand converted = FindConvertedTwoHandedGripWeapon(hero);
            if (converted != null)
            {
                Item pairedItem = GetPairedItem(hero, converted.Item);
                if (pairedItem == null || IsShield(pairedItem))
                {
                    return converted;
                }
            }

            return FindNativeOneHandedGripWeapon(hero);
        }

        private static CharacterHand FindHandForItem(
            Hero hero,
            Item item)
        {
            if (hero == null || item == null)
            {
                return null;
            }

            CharacterHand mainHand = hero.MainHandWeapon as CharacterHand;
            if (mainHand != null
                && ReferenceEquals(mainHand.Item, item))
            {
                return mainHand;
            }

            CharacterHand offHand = hero.OffHandWeapon as CharacterHand;
            return offHand != null
                && ReferenceEquals(offHand.Item, item)
                ? offHand
                : null;
        }

        private static CharacterHandBase FindHandBaseForItem(
            Hero hero,
            Item item)
        {
            if (hero == null || item == null)
            {
                return null;
            }

            CharacterHandBase mainHand = hero.MainHandWeapon;
            if (mainHand != null
                && ReferenceEquals(mainHand.Item, item))
            {
                return mainHand;
            }

            CharacterHandBase offHand = hero.OffHandWeapon;
            return offHand != null
                && ReferenceEquals(offHand.Item, item)
                ? offHand
                : null;
        }

        private static Item GetPairedItem(Hero hero, Item item)
        {
            Item pairedItem = GetRawPairedItem(hero, item);
            return ReferenceEquals(pairedItem, item)
                || IsEmptyHandPlaceholder(pairedItem)
                ? null
                : pairedItem;
        }

        private static Item GetRawPairedItem(Hero hero, Item item)
        {
            if (hero == null || item == null)
            {
                return null;
            }

            if (ReferenceEquals(item, hero.MainHandItem))
            {
                return hero.OffHandItem;
            }

            return ReferenceEquals(item, hero.OffHandItem)
                ? hero.MainHandItem
                : null;
        }

        private void HidePairedHandForTwoHandedGrip(
            Hero hero,
            CharacterHand weapon)
        {
            if (hero == null || weapon == null || weapon.Item == null)
            {
                return;
            }

            CharacterHandBase pairedHand = FindHandBaseForItem(
                hero,
                GetPairedItem(hero, weapon.Item));
            if (pairedHand == null)
            {
                return;
            }

            if (_hiddenPairedHand != null
                && !ReferenceEquals(_hiddenPairedHand, pairedHand))
            {
                RestoreHiddenPairedHand();
            }

            _hiddenPairedHand = pairedHand;
            if (!pairedHand.IsHidden)
            {
                bool preservedDrawnState =
                    SetPairedHandHiddenPreservingDrawnState(
                        pairedHand,
                        true);
                LogDiagnostic(
                    "Kept the paired offhand item hidden while the weapon used a two-handed grip; preservedHeroDrawnState="
                    + preservedDrawnState
                    + ".");
            }
        }

        private void RestoreHiddenPairedHand()
        {
            CharacterHandBase pairedHand = _hiddenPairedHand;
            _hiddenPairedHand = null;
            if (pairedHand != null && pairedHand.IsHidden)
            {
                SetPairedHandHiddenPreservingDrawnState(
                    pairedHand,
                    false);
            }
        }

        private static bool SetPairedHandHiddenPreservingDrawnState(
            CharacterHandBase pairedHand,
            bool hidden)
        {
            Hero hero = Hero.Current;
            bool heroDrawn = hero != null && hero.WeaponsVisible;
            if (hidden)
            {
                pairedHand.HideWeapon(true);
            }
            else
            {
                pairedHand.ShowWeapon();
            }

            if (hero != null
                && hero.WeaponsVisible != heroDrawn
                && HeroWeaponsVisibleSetter != null)
            {
                HeroWeaponsVisibleSetter.Invoke(
                    hero,
                    new object[] { heroDrawn });
            }

            return heroDrawn;
        }

        private void MonitorDrawnWeaponVisibility(
            Hero hero,
            CharacterHand weapon)
        {
            if (hero == null
                || weapon == null
                || !hero.IsWeaponEquipped
                || !weapon.IsHidden
                || weapon.IsLoadingAnimator
                || HeroWeaponEvents.Current.IsLoadingAnimations()
                || hero.IsPerformingAction
                || _pairedRefreshStage != PairedRefreshStage.None)
            {
                _drawnWeaponHiddenSince = -1.0f;
                return;
            }

            if (_drawnWeaponHiddenSince < 0.0f)
            {
                _drawnWeaponHiddenSince = Time.unscaledTime;
                LogDiagnostic(
                    "Started drawn-weapon visibility recovery watch. "
                    + DescribeGripContext(hero, weapon));
                return;
            }

            if (Time.unscaledTime - _drawnWeaponHiddenSince
                < HiddenDrawnWeaponRecoverySeconds)
            {
                return;
            }

            Logger.LogWarning(
                "Recovered a supported weapon that remained hidden after the hero finished drawing it. "
                + DescribeGripContext(hero, weapon));
            ShowDiagnosticNotification(
                "weapon-visibility-recovery",
                "VW recovered: hidden drawn weapon; check the BepInEx log.",
                "High",
                "vw-recovery");
            _drawnWeaponHiddenSince = -1.0f;
            weapon.ShowWeapon();
            if (_twoHandedGrip)
            {
                HidePairedHandForTwoHandedGrip(hero, weapon);
            }
        }

        private string DescribeGripContext(
            Hero hero,
            CharacterHand weapon)
        {
            Item item = weapon == null ? null : weapon.Item;
            Item rawPairedItem = item == null
                ? null
                : GetRawPairedItem(hero, item);
            bool mirroredPair = ReferenceEquals(rawPairedItem, item);
            bool emptyHandPlaceholder =
                IsEmptyHandPlaceholder(rawPairedItem);
            Item pairedItem = mirroredPair || emptyHandPlaceholder
                ? null
                : rawPairedItem;
            string pairing = mirroredPair
                ? "empty-mirrored"
                : (emptyHandPlaceholder
                    ? "empty-fists"
                : (pairedItem == null
                ? "empty"
                : (IsShield(pairedItem) ? "shield" : "other")));
            bool sharedHand = hero != null
                && hero.MainHandWeapon != null
                && ReferenceEquals(
                    hero.MainHandWeapon,
                    hero.OffHandWeapon);
            OneHandedFSM oneHandedFsm = hero == null
                ? null
                : hero.TryGetElement<OneHandedFSM>();
            TwoHandedFSM twoHandedFsm = hero == null
                ? null
                : hero.TryGetElement<TwoHandedFSM>();
            DualHandedFSM dualHandedFsm = hero == null
                ? null
                : hero.TryGetElement<DualHandedFSM>();
            string rawPairedTemplate = rawPairedItem == null
                || rawPairedItem.Template == null
                ? "none"
                : rawPairedItem.Template.GUID;
            bool explicitGrip = item != null
                && ReferenceEquals(_gripItem, item);
            bool effectiveTwoHanded = IsUsingTwoHandedGrip(item);
            bool supportedFamily = item != null
                && item.Template != null
                && IsSupportedWeaponFamily(item.Template);
            bool oneHandedSupported = item != null
                && item.Template != null
                && item.Template.IsTwoHanded
                ? CanUseNativeTwoHandedWeaponInOneHand(item)
                : supportedFamily;
            float meleeRangeMultiplier = GetGripMeleeRangeMultiplier(item);
            ProfStatType nativeProficiency = GetNativeProficiency(item);
            ProfStatType effectiveProficiency = GetEffectiveProficiency(
                item,
                nativeProficiency);
            StrengthTestMode strengthTestMode = GetActiveStrengthTestMode();
            string actualStrengthRatio = "n/a";
            string effectiveStrengthRatio = "n/a";
            ItemStatsRequirements requirements = item == null
                ? null
                : item.StatsRequirements;
            if (hero != null
                && hero.HeroRPGStats != null
                && requirements != null
                && requirements.StrengthRequired != null
                && requirements.StrengthRequired.ModifiedValue > 0.0f)
            {
                actualStrengthRatio = (
                    hero.HeroRPGStats.Strength.ModifiedValue
                    / requirements.StrengthRequired.ModifiedValue)
                        .ToString("0.00", CultureInfo.InvariantCulture)
                    + "x";
            }

            if (requirements != null
                && requirements.StrengthRequired != null
                && requirements.StrengthRequired.ModifiedValue > 0.0f)
            {
                if (strengthTestMode == StrengthTestMode.WeaponRequirement)
                {
                    effectiveStrengthRatio = "1.00x";
                }
                else if (strengthTestMode == StrengthTestMode.FullPotency)
                {
                    effectiveStrengthRatio = GetFullPotencyStrengthMultiplier()
                        .ToString("0.00", CultureInfo.InvariantCulture)
                        + "x";
                }
                else
                {
                    effectiveStrengthRatio = actualStrengthRatio;
                }
            }

            return "effectiveGrip="
                + (effectiveTwoHanded ? "two-handed" : "one-handed")
                + "; gripSource="
                + (explicitGrip ? "explicit" : "default")
                + "; pairing="
                + pairing
                + "; rawPairSameItem="
                + mirroredPair
                + "; rawPairFists="
                + emptyHandPlaceholder
                + "; handObjectShared="
                + sharedHand
                + "; rawPairTemplate="
                + rawPairedTemplate
                + "; oneHandedFsmActive="
                + (oneHandedFsm != null && oneHandedFsm.IsLayerActive)
                + "; twoHandedFsmActive="
                + (twoHandedFsm != null && twoHandedFsm.IsLayerActive)
                + "; dualHandedFsmActive="
                + (dualHandedFsm != null && dualHandedFsm.IsLayerActive)
                + "; supportedFamily="
                + supportedFamily
                + "; oneHandedSupported="
                + oneHandedSupported
                + "; strengthTestMode="
                + strengthTestMode
                + "; actualStrengthRatio="
                + actualStrengthRatio
                + "; effectiveStrengthRatio="
                + effectiveStrengthRatio
                + "; meleeRangeMultiplier="
                + meleeRangeMultiplier.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture)
                + "x"
                + "; nativeProficiency="
                + DescribeProficiency(nativeProficiency)
                + "; effectiveProficiency="
                + DescribeProficiency(effectiveProficiency)
                + "; perspective="
                + (Hero.TppActive ? "third-person" : "first-person")
                + "; heroDrawn="
                + (hero != null && hero.IsWeaponEquipped)
                + "; weaponHidden="
                + (weapon != null && weapon.IsHidden)
                + "; weaponLoading="
                + (weapon != null && weapon.IsLoadingAnimator)
                + "; refreshStage="
                + _pairedRefreshStage
                + ".";
        }

        private static bool IsShield(Item item)
        {
            return item != null && item.IsShield;
        }

        private static bool IsEmptyHandPlaceholder(Item item)
        {
            return item != null
                && (item.IsFists || item.IsDefaultFists);
        }

        private bool NativeGripAnimatorIsReady(
            Hero hero,
            CharacterHand weapon)
        {
            return weapon != null
                && !weapon.IsLoadingAnimator
                && !HeroWeaponEvents.Current.IsLoadingAnimations()
                && AnimatorLayersAreReady(hero, weapon);
        }

        private bool HandAnimationsAreSettled(
            Hero hero,
            CharacterHand weapon,
            bool desiredState)
        {
            if (weapon == null
                || weapon.IsLoadingAnimator
                || HeroWeaponEvents.Current.IsLoadingAnimations())
            {
                return false;
            }

            Item pairedItem = GetPairedItem(hero, weapon.Item);
            CharacterHand pairedHand =
                FindHandForItem(hero, pairedItem);
            if (pairedItem == null || pairedHand == null)
            {
                return AnimatorLayersAreReady(hero, weapon);
            }

            return pairedHand != null
                && ReferenceEquals(pairedHand.Item, pairedItem)
                && !pairedHand.IsHidden
                && !pairedHand.IsLoadingAnimator
                && AnimatorLayersAreReady(hero, weapon)
                && AnimatorLayersAreReady(hero, pairedHand);
        }

        private bool AnimatorLayersAreReady(
            Hero hero,
            CharacterHand hand)
        {
            if (hero == null || hand == null || hand.Item == null)
            {
                return false;
            }

            if (ReferenceEquals(hand.Item, hero.MainHandItem))
            {
                return ReferenceEquals(_readyMainHand, hand);
            }

            return ReferenceEquals(hand.Item, hero.OffHandItem)
                && ReferenceEquals(_readyOffHand, hand);
        }

        private void MarkHandAnimatorLoading(CharacterHand hand)
        {
            Hero hero = Hero.Current;
            if (hero == null || hand == null || hand.Item == null)
            {
                return;
            }

            if (ReferenceEquals(hand.Item, hero.MainHandItem))
            {
                _readyMainHand = null;
            }
            else if (ReferenceEquals(hand.Item, hero.OffHandItem))
            {
                _readyOffHand = null;
            }
        }

        private void RequestOneHandedReconciliation()
        {
            _oneHandedReconciliationPending = true;
        }

        private void BeginPairedRefresh(
            Hero hero,
            CharacterHand weapon)
        {
            Item pairedItem = GetPairedItem(hero, weapon.Item);
            CharacterHand shield = FindHandForItem(hero, pairedItem);
            if (shield == null || !IsShield(pairedItem))
            {
                _weaponTransitionRefreshPending = false;
                return;
            }

            OneHandedFSM oneHanded =
                hero.TryGetElement<OneHandedFSM>();
            TwoHandedFSM twoHanded =
                hero.TryGetElement<TwoHandedFSM>();
            DualHandedFSM dualHanded =
                hero.TryGetElement<DualHandedFSM>();

            if (oneHanded != null)
            {
                oneHanded.DisableFSM();
            }

            if (twoHanded != null)
            {
                twoHanded.DisableFSM();
            }

            if (dualHanded != null)
            {
                dualHanded.DisableFSM();
            }

            _weaponTransitionRefreshPending = false;
            _oneHandedReconciliationPending = false;
            _observedAnimationStateKnown = false;
            _pairedRefreshWeapon = weapon;
            _pairedRefreshShield = shield;
            _pairedRefreshWaitFrames = 0;
            _pairedRefreshStartedAt = Time.unscaledTime;

            MarkHandAnimatorLoading(shield);
            MarkHandAnimatorLoading(weapon);
            shield.HideWeapon(true);
            weapon.HideWeapon(true);
            _pairedRefreshStage = PairedRefreshStage.Hidden;

            Logger.LogInfo(
                "Restarting sword and shield animations with an ordered controller reload. "
                + DescribeGripContext(hero, weapon));
        }

        private bool ProcessPairedRefresh(
            Hero hero,
            CharacterHand currentWeapon)
        {
            if (_pairedRefreshStage == PairedRefreshStage.None)
            {
                return false;
            }

            if (Time.unscaledTime - _pairedRefreshStartedAt
                >= PairedRefreshTimeoutSeconds)
            {
                Logger.LogWarning(
                    "Ordered sword and shield animation reload timed out; restoring both drawn models and allowing the normal controller state to recover. "
                    + DescribeGripContext(hero, _pairedRefreshWeapon));
                ShowDiagnosticNotification(
                    "animation-reload-timeout",
                    "VW recovery warning: animation reload timed out; check the BepInEx log.",
                    "High",
                    "vw-recovery");
                CharacterHand timedOutWeapon = _pairedRefreshWeapon;
                CharacterHand timedOutShield = _pairedRefreshShield;
                CancelPairedRefresh();
                if (hero != null && hero.IsWeaponEquipped)
                {
                    if (timedOutWeapon != null && timedOutWeapon.IsHidden)
                    {
                        timedOutWeapon.ShowWeapon();
                    }

                    if (timedOutShield != null && timedOutShield.IsHidden)
                    {
                        timedOutShield.ShowWeapon();
                    }
                }

                _observedAnimationStateKnown = false;
                RequestOneHandedReconciliation();
                return false;
            }

            if (!ReferenceEquals(currentWeapon, _pairedRefreshWeapon)
                || _pairedRefreshWeapon == null
                || _pairedRefreshShield == null
                || _pairedRefreshWeapon.Item == null
                || _pairedRefreshShield.Item == null
                || !ReferenceEquals(
                    GetPairedItem(hero, _pairedRefreshWeapon.Item),
                    _pairedRefreshShield.Item))
            {
                CancelPairedRefresh();
                return false;
            }

            if (_pairedRefreshStage == PairedRefreshStage.Hidden)
            {
                if (_pairedRefreshWaitFrames++ == 0)
                {
                    return true;
                }

                _pairedRefreshWeapon.ShowWeapon();
                _pairedRefreshStage =
                    PairedRefreshStage.WaitingForSword;
                LogDiagnostic(
                    "Ordered animation reload advanced to WaitingForSword. "
                    + DescribeGripContext(hero, _pairedRefreshWeapon));
                return true;
            }

            if (_pairedRefreshStage
                == PairedRefreshStage.WaitingForSword)
            {
                if (_pairedRefreshWeapon.IsHidden
                    || _pairedRefreshWeapon.IsLoadingAnimator
                    || !AnimatorLayersAreReady(
                        hero,
                        _pairedRefreshWeapon))
                {
                    return true;
                }

                _pairedRefreshShield.ShowWeapon();
                _pairedRefreshStage =
                    PairedRefreshStage.WaitingForShield;
                LogDiagnostic(
                    "Ordered animation reload advanced to WaitingForShield. "
                    + DescribeGripContext(hero, _pairedRefreshWeapon));
                return true;
            }

            if (!HandAnimationsAreSettled(
                hero,
                _pairedRefreshWeapon,
                true))
            {
                return true;
            }

            ReconcileOneHandedAnimationState(hero);
            _observedAnimationState = true;
            _observedAnimationStateKnown = true;
            _oneHandedReconciliationPending = false;
            Logger.LogInfo(
                "Completed the ordered sword and shield animation reload. "
                + DescribeGripContext(hero, _pairedRefreshWeapon));
            CancelPairedRefresh();
            return true;
        }

        private void CancelPairedRefresh()
        {
            _pairedRefreshStage = PairedRefreshStage.None;
            _pairedRefreshWeapon = null;
            _pairedRefreshShield = null;
            _pairedRefreshWaitFrames = 0;
            _pairedRefreshStartedAt = 0.0f;
        }

        private static GameConstants GetGameConstants()
        {
            return World.Services == null
                ? null
                : World.Services.TryGet<GameConstants>();
        }

        private void RefreshWeaponAnimations(
            Hero hero,
            CharacterHand weapon,
            bool desiredState)
        {
            try
            {
                OneHandedFSM oneHanded =
                    hero.TryGetElement<OneHandedFSM>();
                TwoHandedFSM twoHanded =
                    hero.TryGetElement<TwoHandedFSM>();
                DualHandedFSM dualHanded =
                    hero.TryGetElement<DualHandedFSM>();

                if (oneHanded != null)
                {
                    oneHanded.DisableFSM();
                }

                if (twoHanded != null)
                {
                    twoHanded.DisableFSM();
                }

                if (dualHanded != null)
                {
                    dualHanded.DisableFSM();
                }

                Item pairedItem = GetPairedItem(hero, weapon.Item);
                CharacterHand pairedHand = IsShield(pairedItem)
                    ? FindHandForItem(hero, pairedItem)
                    : null;

                _observedAnimationStateKnown = false;
                _oneHandedReconciliationPending = desiredState;
                weapon.HideWeapon(true);

                if (pairedHand != null
                    && !ReferenceEquals(pairedHand, weapon))
                {
                    pairedHand.HideWeapon(true);
                }

                weapon.ShowWeapon();

                if (desiredState
                    && pairedHand != null
                    && !ReferenceEquals(pairedHand, weapon))
                {
                    pairedHand.ShowWeapon();
                }

                Logger.LogInfo(
                    desiredState
                        ? "Applied matching one-handed animations to the converted two-handed weapon. "
                            + DescribeGripContext(hero, weapon)
                        : "Restored the weapon's native two-handed animations. "
                            + DescribeGripContext(hero, weapon));
            }
            catch (Exception exception)
            {
                _observedAnimationStateKnown = true;
                Logger.LogError(
                    "Failed to refresh the converted weapon animations: "
                    + exception);
            }
        }

        private void RefreshNativeOneHandedWeaponAnimations(
            Hero hero,
            CharacterHand weapon,
            bool useTwoHandedGrip)
        {
            try
            {
                OneHandedFSM oneHanded =
                    hero.TryGetElement<OneHandedFSM>();
                TwoHandedFSM twoHanded =
                    hero.TryGetElement<TwoHandedFSM>();
                DualHandedFSM dualHanded =
                    hero.TryGetElement<DualHandedFSM>();

                if (oneHanded != null)
                {
                    oneHanded.DisableFSM();
                }

                if (twoHanded != null)
                {
                    twoHanded.DisableFSM();
                }

                if (dualHanded != null)
                {
                    dualHanded.DisableFSM();
                }

                CharacterHandBase pairedHand = FindHandBaseForItem(
                    hero,
                    GetPairedItem(hero, weapon.Item));
                if (pairedHand == null)
                {
                    pairedHand = _hiddenPairedHand;
                }

                MarkHandAnimatorLoading(weapon);
                weapon.HideWeapon(true);
                if (pairedHand != null
                    && !ReferenceEquals(pairedHand, weapon)
                    && !pairedHand.IsHidden)
                {
                    pairedHand.HideWeapon(true);
                }

                weapon.ShowWeapon();

                if (useTwoHandedGrip)
                {
                    _hiddenPairedHand = pairedHand;
                }
                else
                {
                    _hiddenPairedHand = null;
                    if (pairedHand != null
                        && !ReferenceEquals(pairedHand, weapon)
                        && pairedHand.IsHidden)
                    {
                        pairedHand.ShowWeapon();
                    }
                }

                Logger.LogInfo(
                    useTwoHandedGrip
                        ? "Applied the matching two-handed animations to the native one-handed weapon."
                        : "Restored the weapon's native one-handed animations.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Failed to refresh the native one-handed weapon animations: "
                    + exception);
            }
        }

        internal float GetGripDamageMultiplier(Item item)
        {
            if (IsNativeOneHandedWeaponInTwoHandedGrip(item))
            {
                return _twoHandedOneHandedWeaponDamageMultiplier == null
                    ? 1.5f
                    : _twoHandedOneHandedWeaponDamageMultiplier.Value;
            }

            if (IsConvertedNativeTwoHandedWeaponInOneHandedGrip(item))
            {
                return GetStrengthScaledMultiplier(
                    item,
                    _oneHandedTwoHandedWeaponRequirementDamageMultiplier,
                    0.75f,
                    _oneHandedTwoHandedWeaponFullDamageMultiplier,
                    1.0f);
            }

            return 1.0f;
        }

        internal void ApplyGripImpactMultipliers(
            Item item,
            ref DamageParameters parameters)
        {
            if (IsNativeOneHandedWeaponInTwoHandedGrip(item))
            {
                parameters.PoiseDamage *=
                    _twoHandedOneHandedWeaponPoiseMultiplier == null
                        ? 1.2f
                        : _twoHandedOneHandedWeaponPoiseMultiplier.Value;
                parameters.ForceDamage *=
                    _twoHandedOneHandedWeaponForceMultiplier == null
                        ? 1.1f
                        : _twoHandedOneHandedWeaponForceMultiplier.Value;
                return;
            }

            if (IsConvertedNativeTwoHandedWeaponInOneHandedGrip(item))
            {
                parameters.PoiseDamage *=
                    GetStrengthScaledMultiplier(
                        item,
                        _oneHandedTwoHandedWeaponRequirementPoiseMultiplier,
                        0.6f,
                        _oneHandedTwoHandedWeaponFullPoiseMultiplier,
                        0.95f);
                parameters.ForceDamage *=
                    GetStrengthScaledMultiplier(
                        item,
                        _oneHandedTwoHandedWeaponRequirementForceMultiplier,
                        0.65f,
                        _oneHandedTwoHandedWeaponFullForceMultiplier,
                        1.0f);
            }
        }

        internal float AdjustAttackAnimationSpeed(
            CharacterWeapon weapon,
            float currentSpeed)
        {
            Item item = weapon == null ? null : weapon.Item;
            if (IsNativeOneHandedWeaponInTwoHandedGrip(item))
            {
                return currentSpeed
                    * (_twoHandedOneHandedWeaponAttackSpeedMultiplier == null
                        ? 1.2f
                        : _twoHandedOneHandedWeaponAttackSpeedMultiplier.Value);
            }

            if (IsConvertedNativeTwoHandedWeaponInOneHandedGrip(item))
            {
                return currentSpeed
                    * GetStrengthScaledMultiplier(
                        item,
                        _oneHandedTwoHandedWeaponRequirementAttackSpeedMultiplier,
                        0.5f,
                        _oneHandedTwoHandedWeaponFullAttackSpeedMultiplier,
                        0.75f);
            }

            return currentSpeed;
        }

        private float GetStrengthScaledMultiplier(
            Item item,
            ConfigEntry<float> baseEntry,
            float defaultBase,
            ConfigEntry<float> fullEntry,
            float defaultFull)
        {
            float baseValue = baseEntry == null
                ? defaultBase
                : baseEntry.Value;
            float fullValue = fullEntry == null
                ? defaultFull
                : fullEntry.Value;
            return Mathf.Lerp(
                baseValue,
                fullValue,
                GetOneHandedTwoHandedWeaponStrengthMastery(item));
        }

        private float GetOneHandedTwoHandedWeaponStrengthMastery(
            Item item)
        {
            ItemStatsRequirements requirements = item == null
                ? null
                : item.StatsRequirements;
            if (requirements == null
                || requirements.StrengthRequired == null
                || requirements.StrengthRequired.ModifiedValue <= 0.0f)
            {
                return 1.0f;
            }

            float fullPotencyStrengthMultiplier =
                GetFullPotencyStrengthMultiplier();
            if (fullPotencyStrengthMultiplier <= 1.0f)
            {
                return 1.0f;
            }

            float currentStrengthMultiplier;
            StrengthTestMode testMode = GetActiveStrengthTestMode();
            if (testMode == StrengthTestMode.WeaponRequirement)
            {
                currentStrengthMultiplier = 1.0f;
            }
            else if (testMode == StrengthTestMode.FullPotency)
            {
                currentStrengthMultiplier = fullPotencyStrengthMultiplier;
            }
            else
            {
                Hero hero = Hero.Current;
                if (hero == null || hero.HeroRPGStats == null)
                {
                    return 0.0f;
                }

                currentStrengthMultiplier =
                    hero.HeroRPGStats.Strength.ModifiedValue
                    / requirements.StrengthRequired.ModifiedValue;
            }

            return Mathf.Clamp01(
                (currentStrengthMultiplier - 1.0f)
                / (fullPotencyStrengthMultiplier - 1.0f));
        }

        private StrengthTestMode GetActiveStrengthTestMode()
        {
            return _diagnostics != null
                && _diagnostics.Value
                && _strengthTestMode != null
                    ? _strengthTestMode.Value
                    : StrengthTestMode.Actual;
        }

        private float GetFullPotencyStrengthMultiplier()
        {
            return _fullPotencyStrengthMultiplier == null
                ? 2.0f
                : Math.Max(1.0f, _fullPotencyStrengthMultiplier.Value);
        }

        private bool IsNativeOneHandedWeaponInTwoHandedGrip(Item item)
        {
            if (_enabled == null
                || !_enabled.Value
                || !IsUsingTwoHandedGrip(item))
            {
                return false;
            }

            return IsNativeOneHandedGripWeapon(
                FindHandForItem(Hero.Current, item));
        }

        private bool IsConvertedNativeTwoHandedWeaponInOneHandedGrip(
            Item item)
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, item);
            if (!IsConvertedTwoHandedGripWeapon(weapon))
            {
                return false;
            }

            Item pairedItem = GetPairedItem(hero, item);
            return (pairedItem == null || IsShield(pairedItem))
                && !IsUsingTwoHandedGrip(item);
        }

        internal static Item GetActiveTwoHandedGripItem(Hero hero)
        {
            if (Instance == null
                || Instance._enabled == null
                || !Instance._enabled.Value
                || hero == null)
            {
                return null;
            }

            CharacterHand weapon = FindGripSwitchWeapon(hero);
            Item item = weapon == null ? null : weapon.Item;
            return Instance.IsUsingTwoHandedGrip(item)
                ? item
                : null;
        }

        internal static CharacterHandBase GetActiveTwoHandedGripWeapon(
            Hero hero)
        {
            Item item = GetActiveTwoHandedGripItem(hero);
            return item == null
                ? null
                : FindHandForItem(hero, item);
        }

        internal static bool IsMainHandSuppressed()
        {
            Hero hero = Hero.Current;
            Item gripItem = GetActiveTwoHandedGripItem(hero);
            return hero != null
                && gripItem != null
                && ReferenceEquals(gripItem, hero.OffHandItem);
        }

        internal static bool IsOffHandSuppressed()
        {
            Hero hero = Hero.Current;
            Item gripItem = GetActiveTwoHandedGripItem(hero);
            return hero != null
                && gripItem != null
                && ReferenceEquals(gripItem, hero.MainHandItem);
        }

        internal static bool ShouldAllowMagicCast(MagicFSM magicFsm)
        {
            if (magicFsm == null)
            {
                return true;
            }

            return (magicFsm.CastingHand != CastingHand.MainHand
                    || !IsMainHandSuppressed())
                && (magicFsm.CastingHand != CastingHand.OffHand
                    || !IsOffHandSuppressed());
        }

        internal static void ApplyInverseHandsCompatibility(
            ref bool inverseHands)
        {
            if (!inverseHands
                || Instance == null
                || Instance._enabled == null
                || !Instance._enabled.Value)
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null)
            {
                return;
            }

            Item mainHandItem = hero.MainHandItem;
            Item offHandItem = hero.OffHandItem;
            bool mainHandSpell = mainHandItem != null
                && mainHandItem.IsMagic;
            bool offHandSpell = offHandItem != null
                && offHandItem.IsMagic;

            if (Instance._singleSpellUsesNormalHands != null
                && Instance._singleSpellUsesNormalHands.Value
                && mainHandSpell != offHandSpell)
            {
                inverseHands = false;
                return;
            }

            if (Instance._twoHandedGripUsesNormalHands == null
                || !Instance._twoHandedGripUsesNormalHands.Value)
            {
                return;
            }

            Item gripItem = GetActiveTwoHandedGripItem(hero);
            Item pairedItem = gripItem == null
                ? null
                : GetPairedItem(hero, gripItem);
            if (pairedItem != null && pairedItem.IsMagic)
            {
                inverseHands = false;
            }
        }

        private void ClearObservedWeapon()
        {
            _observedWeapon = null;
            _observedAnimationStateKnown = false;
            _oneHandedReconciliationPending = false;
            _gripFsmMismatchFrames = 0;
        }
    }

    public static class VersatileWeaponsApi
    {
        public const int ApiVersion = 2;

        public static bool IsLoaded()
        {
            return Plugin.Instance != null;
        }

        public static bool IsMainHandSuppressed()
        {
            return Plugin.IsMainHandSuppressed();
        }

        public static bool IsOffHandSuppressed()
        {
            return Plugin.IsOffHandSuppressed();
        }

        public static bool IsUsingTwoHandedGrip(Item item)
        {
            return Plugin.Instance != null
                && Plugin.Instance.IsUsingTwoHandedGrip(item);
        }

        public static ProfStatType GetEffectiveProficiency(Item item)
        {
            return Plugin.Instance == null
                ? Plugin.GetNativeProficiency(item)
                : Plugin.Instance.GetEffectiveProficiency(item, null);
        }
    }

    [HarmonyPatch]
    internal static class HeroIsDualWieldingPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(Hero),
                "IsDualWielding");
        }

        private static void Postfix(ref bool __result)
        {
            if (__result && Plugin.ShouldSuppressDualWielding())
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch]
    internal static class InverseHandsSettingPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(InverseHandsSetting),
                nameof(InverseHandsSetting.InverseHands));
        }

        private static void Postfix(ref bool __result)
        {
            Plugin.ApplyInverseHandsCompatibility(ref __result);
        }
    }

    [HarmonyPatch]
    internal static class ToggleAnimatorLayersPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterHand),
                "ToggleAnimatorLayers");
        }

        private static void Postfix(
            CharacterHand __instance,
            bool activate)
        {
            if (!activate)
            {
                return;
            }

            Plugin.DisableConflictingFsms();

            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordAnimatorLayersApplied(
                    __instance,
                    true);
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterMagicToggleAnimatorLayersPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterMagic),
                "ToggleAnimatorLayers");
        }

        private static void Postfix(bool activate)
        {
            if (activate)
            {
                Plugin.DisableConflictingFsms();
            }
        }
    }

    [HarmonyPatch]
    internal static class MagicCastGripSuppressionPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(MagicFSM),
                "TryEnterMagicCastState");
        }

        private static bool Prefix(
            MagicFSM __instance,
            ref bool __result)
        {
            if (Plugin.ShouldAllowMagicCast(__instance))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class AnimatorControllerPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(CharacterWeapon),
                "AnimatorControllerRef");
        }

        private static void Postfix(
            CharacterHand __instance,
            ref ARAssetReference __result)
        {
            if (Plugin.ShouldUseOneHandedAnimations(__instance))
            {
                __result = Plugin.CreateOneHandedWeaponController(
                    __instance,
                    __result);
            }
            else if (Plugin.ShouldUseTwoHandedAnimations(__instance))
            {
                __result = Plugin.CreateTwoHandedWeaponController(
                    __instance);
            }
        }
    }

    [HarmonyPatch]
    internal static class AnimationLayersPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(CharacterWeapon),
                "LayersToEnable");
        }

        private static void Postfix(
            CharacterHand __instance,
            ref string[] __result)
        {
            if (Plugin.ShouldUseOneHandedAnimations(__instance))
            {
                __result = Plugin.GetOneHandedLayers();
            }
            else if (Plugin.ShouldUseTwoHandedAnimations(__instance))
            {
                __result = Plugin.GetTwoHandedLayers();
            }
        }
    }

    [HarmonyPatch]
    internal static class DualWieldingMainHandPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(CharacterHand),
                "DualWieldingMainHand");
        }

        private static void Postfix(
            CharacterHand __instance,
            ref ARAssetReference __result)
        {
            if (Plugin.ShouldUseOneHandedAnimations(__instance))
            {
                __result = Plugin.CreateStandardDualController(
                    true,
                    __result);
            }
        }
    }

    [HarmonyPatch]
    internal static class DualWieldingOffHandPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(CharacterHand),
                "DualWieldingOffHand");
        }

        private static void Postfix(
            CharacterHand __instance,
            ref ARAssetReference __result)
        {
            if (Plugin.ShouldUseOneHandedAnimations(__instance))
            {
                __result = Plugin.CreateStandardDualController(
                    false,
                    __result);
            }
        }
    }

    [HarmonyPatch]
    internal static class ItemIsOneHandedPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(Item),
                "IsOneHanded");
        }

        private static void Postfix(Item __instance, ref bool __result)
        {
            if (!__result && Plugin.ShouldTreatAsOneHanded(__instance))
            {
                __result = true;
            }
            else if (__result && Plugin.ShouldTreatAsTwoHanded(__instance))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch]
    internal static class ItemIsTwoHandedPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(Item),
                "IsTwoHanded");
        }

        private static void Postfix(Item __instance, ref bool __result)
        {
            if (__result && Plugin.ShouldTreatAsOneHanded(__instance))
            {
                __result = false;
            }
            else if (!__result && Plugin.ShouldTreatAsTwoHanded(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    internal static class ItemStatsProficiencyPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(ItemStats),
                "ProfFromAbstract");
        }

        private static void Postfix(
            ItemStats __instance,
            ref ProfStatType __result)
        {
            if (Plugin.Instance != null)
            {
                __result = Plugin.Instance.GetEffectiveProficiency(
                    __instance.ParentModel,
                    __result);
            }
        }
    }

    [HarmonyPatch]
    internal static class ProfUtilsGripProficiencyPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ProfUtils),
                nameof(ProfUtils.ProfFromAbstracts),
                new Type[]
                {
                    typeof(Item),
                    typeof(IEnumerable<ProfAbstractRefs>),
                    typeof(bool)
                });
        }

        private static void Postfix(
            Item itemToSearch,
            ref ProfStatType __result)
        {
            if (Plugin.Instance != null)
            {
                __result = Plugin.Instance.GetEffectiveProficiency(
                    itemToSearch,
                    __result);
            }
        }
    }

    [HarmonyPatch]
    internal static class ProfUtilsDefaultGripProficiencyPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ProfUtils),
                nameof(ProfUtils.ProfFromAbstracts),
                new Type[]
                {
                    typeof(Item),
                    typeof(bool)
                });
        }

        private static void Postfix(
            Item itemToSearch,
            ref ProfStatType __result)
        {
            if (Plugin.Instance != null)
            {
                __result = Plugin.Instance.GetEffectiveProficiency(
                    itemToSearch,
                    __result);
            }
        }
    }

    [HarmonyPatch]
    internal static class MeleeGeneralStateGripInputPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(MeleeFSM),
                "GeneralStateUpdate");
        }

        private static bool Prefix(MeleeFSM __instance)
        {
            return Plugin.Instance == null
                || !Plugin.Instance.ShouldSuppressMeleeInput(__instance);
        }
    }

    [HarmonyPatch]
    internal static class CharacterWeaponAttackSpeedPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterWeapon),
                "AfterHeroAnimationSpeedProcessed");
        }

        private static void Postfix(
            CharacterWeapon __instance,
            int parameterHash,
            float modifier)
        {
            Plugin plugin = Plugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || hero == null
                || hero.VHeroController == null)
            {
                return;
            }

            float adjusted = plugin.AdjustAttackAnimationSpeed(
                __instance,
                modifier);
            if (Mathf.Approximately(adjusted, modifier))
            {
                return;
            }

            AnimancerAttackSpeed parameter = null;
            if (parameterHash == AnimancerAttackSpeed.LightAttackMult1H.AnimatorHash)
            {
                parameter = AnimancerAttackSpeed.LightAttackMult1H;
            }
            else if (parameterHash == AnimancerAttackSpeed.HeavyAttackMult1H.AnimatorHash)
            {
                parameter = AnimancerAttackSpeed.HeavyAttackMult1H;
            }
            else if (parameterHash == AnimancerAttackSpeed.LightAttackMult2H.AnimatorHash)
            {
                parameter = AnimancerAttackSpeed.LightAttackMult2H;
            }
            else if (parameterHash == AnimancerAttackSpeed.HeavyAttackMult2H.AnimatorHash)
            {
                parameter = AnimancerAttackSpeed.HeavyAttackMult2H;
            }

            if (parameter != null)
            {
                parameter.SetAttackSpeed(
                    hero.VHeroController.Animancer,
                    Mathf.Max(0.1f, adjusted));
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterWeaponMeleeRangePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(CharacterWeapon),
                "Size");
        }

        private static void Postfix(
            CharacterWeapon __instance,
            ref Vector3 __result)
        {
            if (Plugin.Instance == null)
            {
                return;
            }

            float multiplier = Plugin.Instance
                .GetCharacterWeaponMeleeRangeMultiplier(__instance);
            if (!Mathf.Approximately(multiplier, 1.0f))
            {
                __result.z *= multiplier;
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterDealingDamageGripPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterDealingDamage),
                "GetDamageToDeal");
        }

        private static void Prefix(
            Item item,
            ref DamageParameters parameters)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.ApplyGripImpactMultipliers(
                    item,
                    ref parameters);
            }
        }

        private static void Postfix(
            Item item,
            ref Damage __result)
        {
            if (Plugin.Instance == null || __result == null)
            {
                return;
            }

            float multiplier =
                Plugin.Instance.GetGripDamageMultiplier(item);
            if (!Mathf.Approximately(multiplier, 1.0f))
            {
                __result.RawData.MultiplyMultModifier(multiplier);
            }
        }
    }

    [HarmonyPatch]
    internal static class HeroBlockStatsItemPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(HeroBlock),
                nameof(HeroBlock.GetStatsItem));
        }

        private static void Postfix(Hero hero, ref Item __result)
        {
            Item gripItem = Plugin.GetActiveTwoHandedGripItem(hero);
            if (gripItem != null)
            {
                __result = gripItem;
            }
        }
    }

    [HarmonyPatch]
    internal static class HeroBlockWeaponPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(HeroBlock),
                nameof(HeroBlock.GetBlockingWeapon));
        }

        private static void Postfix(
            Hero hero,
            ref CharacterHandBase __result)
        {
            CharacterHandBase weapon =
                Plugin.GetActiveTwoHandedGripWeapon(hero);
            if (weapon != null)
            {
                __result = weapon;
            }
        }
    }

    [HarmonyPatch]
    internal static class AnimatorLoadPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterHand),
                "LoadHeroAnimatorOverrides");
        }

        private static void Prefix(CharacterHand __instance)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordAnimatorLoad(__instance);
            }
        }
    }

    [HarmonyPatch]
    internal static class ItemEquipEquipmentTypePatch
    {
        private static readonly Dictionary<ItemEquip, bool> ConversionCache =
            new Dictionary<ItemEquip, bool>();

        internal static void ClearCache()
        {
            ConversionCache.Clear();
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(ItemEquip),
                "EquipmentType");
        }

        private static void Postfix(
            ItemEquip __instance,
            ref EquipmentType __result)
        {
            if (__result != EquipmentType.TwoHanded
                || Plugin.Instance == null)
            {
                return;
            }

            bool canConvert;
            if (!ConversionCache.TryGetValue(__instance, out canConvert))
            {
                if (!Plugin.Instance.TryCanConvertToOneHanded(
                    __instance,
                    out canConvert))
                {
                    return;
                }

                ConversionCache[__instance] = canConvert;
            }

            if (canConvert)
            {
                __result = EquipmentType.OneHanded;
            }
        }
    }

    [HarmonyPatch]
    internal static class VHeroKeysHandlePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(VHeroKeys),
                nameof(VHeroKeys.Handle),
                new Type[] { typeof(UIEvent) });
        }

        private static bool Prefix(
            UIEvent evt,
            ref UIResult __result)
        {
            return Plugin.Instance == null
                || Plugin.Instance.HandleToggleWeaponInput(
                    evt,
                    ref __result);
        }
    }
}
