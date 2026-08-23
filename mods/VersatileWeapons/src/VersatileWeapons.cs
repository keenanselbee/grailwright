using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Awaken.TG.Graphics.Animations;
using Awaken.TG.Assets;
using Awaken.TG.MVC;
using Awaken.TG.MVC.UI;
using Awaken.TG.MVC.UI.Events;
using Awaken.TG.Main.Animations.FSM.Heroes.Base;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.Main.Animations.FSM.Heroes.States.Shared;
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.General.Configs;
using Awaken.TG.Main.General.StatTypes;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Animations;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Attachments;
using Awaken.TG.Main.Heroes.Items.Loadouts;
using Awaken.TG.Main.Heroes.Items.Weapons;
using Awaken.TG.Main.Heroes.Stats.Observers;
using Awaken.TG.Main.Heroes.Stats.Utils;
using Awaken.TG.Main.Settings.Gameplay;
using Awaken.TG.Main.Utility;
using Awaken.TG.Main.Utility.Audio;
using Awaken.TG.Main.Utility.Animations;
using Awaken.TG.Main.Utility.Animations.ARAnimator;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using FMODUnity;
using UnityEngine;

[assembly: AssemblyTitle("Versatile Weapons")]
[assembly: AssemblyDescription("Strength-scaled one-handed greatweapons and switchable melee grips for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Versatile Weapons")]
[assembly: AssemblyVersion("0.8.7.0")]
[assembly: AssemblyFileVersion("0.8.7.0")]
[assembly: AssemblyInformationalVersion("0.8.7")]

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
        public const string PluginVersion = "0.8.7";

        private const int ConfigSchemaVersion = 13;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        private const int WeaponTransitionRefreshWindowFrames = 600;
        private const int GripFsmRecoveryFrames = 12;
        private const float PairedRefreshTimeoutSeconds = 4.0f;
        private const float MagicVisualRecoveryDelaySeconds = 1.0f;
        private const float HiddenDrawnWeaponRecoverySeconds = 1.5f;
        private const float GripEquipInputGuardTimeoutSeconds = 3.0f;
        private const float AudioDiagnosticTransitionWindowSeconds = 4.0f;
        private const float WeaponAudioCallbackGuardSeconds = 4.0f;
        private const int WeaponAudioStableFrames = 2;
        private const string SuspectedSoulRendWhisperEvent =
            "{c7f89e29-2578-47cc-b28b-1ede9750d7a7}";
        private const string GripMemoryFileName =
            "VersatileWeaponsGrips.dat";
        private const int GripMemoryFormat = 1;
        private const int MaximumGripMemoryRecords = 16;
        private const string GloriousUiPluginGuid =
            "ks.tgfoa.glorious-ui";

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
        private static readonly string[] OffHandMeleeLayers =
            { "Magic_MeleeOffHand" };
        private static readonly string[] TwoHandedLayers =
            { "2H" };
        private static readonly MethodInfo HeroWeaponsVisibleSetter =
            AccessTools.PropertySetter(typeof(Hero), "WeaponsVisible");
        private static readonly MethodInfo EquipMagicGloveToHeroMethod =
            AccessTools.Method(
                typeof(CharacterMagic),
                "EquipMagicGloveToHero",
                new Type[] { typeof(Hero), typeof(bool) });

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _fullPotencyStrengthMultiplier;
        private ConfigEntry<float> _zeroRequirementFullPotencyStrength;
        private ConfigEntry<float> _gripHoldSeconds;
        private ConfigEntry<bool> _proficiencyFollowsGrip;
        private ConfigEntry<bool> _rememberGripPerLoadout;
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
        private readonly Dictionary<string, int> _configSettingOrders =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedConfigValues =
                new Dictionary<ConfigDefinition, object>();
        private readonly Dictionary<string, GripMemoryRecord>
            _gripMemories =
                new Dictionary<string, GripMemoryRecord>(
                    StringComparer.Ordinal);

        internal static Plugin Instance { get; private set; }

        private Harmony _harmony;
        private CharacterHand _observedWeapon;
        private bool _observedAnimationState;
        private bool _observedAnimationStateKnown;
        private int _gripFsmMismatchFrames;
        private CharacterHandBase _readyMainHand;
        private CharacterHandBase _readyOffHand;
        private CharacterMagic _loadingMainHandMagicVisual;
        private CharacterMagic _loadingOffHandMagicVisual;
        private int _mainHandMagicVisualLoads;
        private int _offHandMagicVisualLoads;
        private CharacterMagic _readyMainHandMagicVisualHand;
        private CharacterMagic _readyOffHandMagicVisualHand;
        private GameObject _readyMainHandMagicVisual;
        private GameObject _readyOffHandMagicVisual;
        private CharacterMagic _magicVisualRecoveryHand;
        private bool _oneHandedReconciliationPending;
        private bool _weaponTransitionRefreshPending;
        private int _weaponTransitionRefreshFramesRemaining;
        private float _weaponTransitionStartedAt;
        private bool _observedLoadoutIndexKnown;
        private int _observedLoadoutIndex;
        private PairedRefreshStage _pairedRefreshStage;
        private CharacterHand _pairedRefreshWeapon;
        private CharacterHandBase _pairedRefreshPairedHand;
        private int _pairedRefreshWaitFrames;
        private float _pairedRefreshStartedAt;
        private EquipFsmResetStage _equipFsmResetStage;
        private CharacterHand _equipFsmResetWeapon;
        private Item _equipFsmResetPairedItem;
        private GripCombatMode _equipFsmResetMode;
        private int _equipFsmResetWaitFrames;
        private float _equipFsmResetStartedAt;
        private int _weaponTransitionGeneration;
        private int _equipFsmResetGeneration;
        private float _audioDiagnosticTransitionStartedAt = -1.0f;
        private float _audioDiagnosticTransitionUntil = -1.0f;
        [ThreadStatic]
        private static HeroAnimatorSubstateMachine
            _unsheatheAudioDiagnosticFsm;
        [ThreadStatic]
        private static bool _weaponAudioPlaybackBypass;
        private readonly List<Item> _weaponAudioPreviousParticipants =
            new List<Item>();
        private readonly List<Item> _weaponAudioPreviousAudible =
            new List<Item>();
        private readonly List<Item> _weaponAudioGuardedItems =
            new List<Item>();
        private bool _weaponAudioTransitionActive;
        private bool _weaponAudioTransitionMuted;
        private float _weaponAudioTransitionStartedAt = -1.0f;
        private float _weaponAudioGuardUntil = -1.0f;
        private int _weaponAudioTransitionStartedFrame;
        private int _weaponAudioStableFrames;
        private Item _weaponAudioObservedMainItem;
        private Item _weaponAudioObservedOffItem;
        private bool _weaponAudioObservedMainSuppressed;
        private bool _weaponAudioObservedOffSuppressed;
        private Item _gripItem;
        private Item _gripPairedItem;
        private string _gripMemoryContextKey;
        private Item _selectedGripControllerItem;
        private bool _selectedGripControllerTwoHanded;
        private bool _selectedGripControllerKnown;
        private Item _rememberedGripAnimationRefreshItem;
        private Item _rememberedGripAnimationRefreshPairedItem;
        private string _rememberedGripAnimationRefreshContextKey;
        private bool _rememberedGripAnimationRefreshTwoHanded;
        private int _rememberedGripAnimationRefreshGeneration;
        private string _pendingGripMemoryInvalidationContextKey;
        private int _pendingGripMemoryInvalidationStableFrames;
        private string _activeGripMemorySaveSlot;
        private bool _twoHandedGrip;
        private CharacterHandBase _hiddenPairedHand;
        private CharacterHandBase _pairedHandVisibilityRecoveryCandidate;
        private float _drawnWeaponHiddenSince = -1.0f;
        private float _drawnPairedHandHiddenSince = -1.0f;
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
        private CharacterHandBase _offHandTwoHandedPresentationWeapon;
        private Transform _offHandTwoHandedPresentationOriginalParent;
        private Vector3 _offHandTwoHandedPresentationLocalPosition;
        private Quaternion _offHandTwoHandedPresentationLocalRotation;
        private Vector3 _offHandTwoHandedPresentationLocalScale;
        private Type _gloriousUiPluginType;
        private MethodInfo _gloriousUiOwnsLoadoutsMethod;
        private FieldInfo _gloriousUiCurrentLoadoutField;
        private bool _gloriousUiReflectionUnavailable;
        private bool _gloriousUiReflectionWarningLogged;

        private sealed class GripMemoryRecord
        {
            public string OwnerHand;
            public string WeaponId;
            public string PairedItemId;
            public bool TwoHandedGrip;
        }

        private enum PairedRefreshStage
        {
            None,
            Hidden,
            WaitingForGripWeapon,
            WaitingForPairedHand
        }

        private enum EquipFsmResetStage
        {
            None,
            WaitingOneFrame,
            WaitingForStableFsms
        }


        private enum GripCombatMode
        {
            None,
            OneHanded,
            OneHandedWithOffHandMelee,
            OffHandMelee,
            DualWielding,
            TwoHanded
        }

        private void Awake()
        {
            Instance = this;

            try
            {
                BindConfig();
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                PatchGripMemoryPersistenceHooks();
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

            int order;
            if (!_configSettingOrders.TryGetValue(section, out order))
            {
                order = 0;
            }
            _configSettingOrders[section] = order + 10;

            return base.Config.Bind(
                section,
                key,
                defaultValue,
                Grailwright.Shared.ConfigUiDescription.Create(
                    description.Description,
                    section,
                    HumanizeConfigKey(key),
                    GetConfigSectionOrder(section),
                    order,
                    description.AcceptableValues));
        }

        private static int GetConfigSectionOrder(string section)
        {
            switch (section)
            {
                case "General":
                    return 0;
                case "Grip Switching":
                    return 10;
                case "Native Two-Handed Weapon - One-Handed Grip":
                    return 20;
                case "Native One-Handed Weapon - Two-Handed Grip":
                    return 30;
                case "Advanced First-Person Alignment":
                    return 40;
                case "Reverse Hands Compatibility":
                    return 50;
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

            _enabled = BindOrdered(
                "General",
                "Enabled",
                true,
                "Master switch. Disabling this restores native equipment and grip behavior after the current weapon state refreshes.");
            BindOrdered(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Do not edit manually; the plugin backs up stale configs and regenerates defaults when this changes.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _fullPotencyStrengthMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "FullPotencyStrengthMultiplier",
                2.0f,
                new ConfigDescription(
                    "Strength at which the full-potency damage, speed, poise, and force values apply. Scaling begins at the weapon's normal Strength requirement. 2 means full potency at 200 percent of that requirement.",
                    new AcceptableValueRange<float>(1.0f, 5.0f)));
            _zeroRequirementFullPotencyStrength = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "ZeroRequirementFullPotencyStrength",
                10.0f,
                new ConfigDescription(
                    "Strength at which a weapon with no normal Strength requirement reaches the full-potency damage, speed, poise, and force values. Scaling begins at 0 Strength. Set to 0 for immediate full potency.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));
            _gripHoldSeconds = BindOrdered(
                "Grip Switching",
                "GripHoldSeconds",
                0.45f,
                new ConfigDescription(
                    "Seconds the game's Toggle Weapon action must be held to change grip on a supported weapon. A shorter press keeps normal sheathe or draw behavior.",
                    new AcceptableValueRange<float>(0.2f, 2.0f)));
            _proficiencyFollowsGrip = BindOrdered(
                "Grip Switching",
                "ProficiencyFollowsGrip",
                true,
                "Use One-Handed proficiency damage scaling and XP in a one-handed grip, and Two-Handed proficiency damage scaling and XP in a two-handed grip. Weapon requirements, stamina costs, and template-filtered item effects remain native.");
            _rememberGripPerLoadout = BindOrdered(
                "Grip Switching",
                "RememberGripPerLoadout",
                true,
                "Remember the last manually selected grip separately for each native or Glorious UI weapon loadout. Memory applies only while that loadout still contains the exact same grip weapon, paired item, and owning hand; changed equipment uses the normal default grip until changed manually.");
            _oneHandedSwordPositionY = BindOrdered(
                "Advanced First-Person Alignment",
                "OneHandedSwordPositionY",
                0.02f,
                new ConfigDescription(
                    "Local weapon-space Y offset in meters for native one-handed swords used with both hands in first person. Set to 0 for no correction.",
                    new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _oneHandedMacePositionY = BindOrdered(
                "Advanced First-Person Alignment",
                "OneHandedMacePositionY",
                -0.35f,
                new ConfigDescription(
                    "Local weapon-space Y offset in meters for native one-handed maces and other blunt weapons used with both hands in first person. Set to 0 for no correction.",
                    new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _oneHandedAxePositionY = BindOrdered(
                "Advanced First-Person Alignment",
                "OneHandedAxePositionY",
                -0.35f,
                new ConfigDescription(
                    "Local weapon-space Y offset in meters for native one-handed axes used with both hands in first person. Set to 0 for no correction.",
                    new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _twoHandedOneHandedWeaponDamageMultiplier = BindOrdered(
                "Native One-Handed Weapon - Two-Handed Grip",
                "DamageMultiplier",
                1.5f,
                new ConfigDescription(
                    "Melee damage while a native one-handed weapon is used with both hands. 1.5 means 150 percent damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _twoHandedOneHandedWeaponAttackSpeedMultiplier = BindOrdered(
                "Native One-Handed Weapon - Two-Handed Grip",
                "AttackSpeedMultiplier",
                1.2f,
                new ConfigDescription(
                    "Attack-animation speed while a native one-handed weapon is used with both hands. 1.2 means 120 percent speed.",
                    new AcceptableValueRange<float>(0.5f, 1.5f)));
            _twoHandedOneHandedWeaponPoiseMultiplier = BindOrdered(
                "Native One-Handed Weapon - Two-Handed Grip",
                "PoiseMultiplier",
                1.2f,
                new ConfigDescription(
                    "Poise damage while a native one-handed weapon is used with both hands. 1.2 means 120 percent poise damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _twoHandedOneHandedWeaponForceMultiplier = BindOrdered(
                "Native One-Handed Weapon - Two-Handed Grip",
                "ForceMultiplier",
                1.1f,
                new ConfigDescription(
                    "Impact force while a native one-handed weapon is used with both hands. 1.1 means 110 percent force.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _twoHandedOneHandedAxeMeleeRangeMultiplier = BindOrdered(
                "Native One-Handed Weapon - Two-Handed Grip",
                "AxeMeleeRangeMultiplier",
                1.5f,
                new ConfigDescription(
                    "Melee hit-detection range for native one-handed axes used with both hands. 1.5 means 150 percent range; 1 keeps vanilla range. This does not resize or move the visible weapon.",
                    new AcceptableValueRange<float>(0.5f, 3.0f)));
            _twoHandedOneHandedMaceMeleeRangeMultiplier = BindOrdered(
                "Native One-Handed Weapon - Two-Handed Grip",
                "MaceMeleeRangeMultiplier",
                1.5f,
                new ConfigDescription(
                    "Melee hit-detection range for native one-handed maces and other blunt weapons used with both hands. 1.5 means 150 percent range; 1 keeps vanilla range. This does not resize or move the visible weapon.",
                    new AcceptableValueRange<float>(0.5f, 3.0f)));
            _oneHandedTwoHandedWeaponRequirementDamageMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "DamageAtWeaponRequirement",
                0.75f,
                new ConfigDescription(
                    "Melee damage at the weapon's normal Strength requirement. 0.75 means 75 percent damage.",
                    new AcceptableValueRange<float>(0.1f, 1.5f)));
            _oneHandedTwoHandedWeaponFullDamageMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "DamageAtFullPotency",
                1.0f,
                new ConfigDescription(
                    "Melee damage at or above FullPotencyStrengthMultiplier. 1 means full native damage.",
                    new AcceptableValueRange<float>(0.1f, 1.5f)));
            _oneHandedTwoHandedWeaponRequirementAttackSpeedMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "AttackSpeedAtWeaponRequirement",
                0.5f,
                new ConfigDescription(
                    "Attack-animation speed at the weapon's normal Strength requirement. 0.5 means 50 percent speed.",
                    new AcceptableValueRange<float>(0.25f, 1.5f)));
            _oneHandedTwoHandedWeaponFullAttackSpeedMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "AttackSpeedAtFullPotency",
                0.75f,
                new ConfigDescription(
                    "Attack-animation speed at or above FullPotencyStrengthMultiplier. 0.75 means 75 percent speed.",
                    new AcceptableValueRange<float>(0.25f, 1.5f)));
            _oneHandedTwoHandedWeaponRequirementPoiseMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "PoiseAtWeaponRequirement",
                0.6f,
                new ConfigDescription(
                    "Poise damage at the weapon's normal Strength requirement. 0.6 means 60 percent poise damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponFullPoiseMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "PoiseAtFullPotency",
                0.95f,
                new ConfigDescription(
                    "Poise damage at or above FullPotencyStrengthMultiplier. 0.95 means 95 percent poise damage.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponRequirementForceMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "ForceAtWeaponRequirement",
                0.65f,
                new ConfigDescription(
                    "Impact force at the weapon's normal Strength requirement. 0.65 means 65 percent force.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _oneHandedTwoHandedWeaponFullForceMultiplier = BindOrdered(
                "Native Two-Handed Weapon - One-Handed Grip",
                "ForceAtFullPotency",
                1.0f,
                new ConfigDescription(
                    "Impact force at or above FullPotencyStrengthMultiplier. 1 means full native force.",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
            _diagnostics = BindOrdered(
                "Diagnostics",
                "Enabled",
                false,
                "Write grip recognition, input, and animation-transition details to the BepInEx log.");
            _strengthTestMode = BindOrdered(
                "Diagnostics",
                "StrengthTestMode",
                StrengthTestMode.Actual,
                "Test native two-handed weapons used in one hand at Actual Strength, WeaponRequirement, or FullPotency. This simulation works only while Diagnostics is enabled and never changes the character's Strength.");
            _showGrailFloatingTextDiagnostics = BindOrdered(
                "Diagnostics",
                "ShowGrailFloatingTextDiagnostics",
                true,
                "When Diagnostics is enabled and Grail Floating Text is installed, show all Versatile Weapons System messages, including completed grip changes, weapon recognition, blocked transitions, pairing changes, and recoveries. Detailed BepInEx logging remains active when this is disabled.");
            _twoHandedGripUsesNormalHands = BindOrdered(
                "Reverse Hands Compatibility",
                "TwoHandedGripUsesNormalHands",
                true,
                "When the game's Reverse Hands setting is enabled, use normal hand input while a supported weapon uses both hands and its paired spell is stowed. Disable this to retain the game's reversed input in that specific grip.");
            _singleSpellUsesNormalHands = BindOrdered(
                "Reverse Hands Compatibility",
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

            CapturePreservedValue<bool>(profile, "General", "Enabled");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "FullPotencyStrengthMultiplier");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "ZeroRequirementFullPotencyStrength");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "DamageAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "DamageAtFullPotency");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "AttackSpeedAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "AttackSpeedAtFullPotency");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "PoiseAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "PoiseAtFullPotency");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "ForceAtWeaponRequirement");
            CapturePreservedValue<float>(profile, "Native Two-Handed Weapon - One-Handed Grip", "ForceAtFullPotency");
            CapturePreservedValue<float>(profile, "Native One-Handed Weapon - Two-Handed Grip", "DamageMultiplier");
            CapturePreservedValue<float>(profile, "Native One-Handed Weapon - Two-Handed Grip", "AttackSpeedMultiplier");
            CapturePreservedValue<float>(profile, "Native One-Handed Weapon - Two-Handed Grip", "PoiseMultiplier");
            CapturePreservedValue<float>(profile, "Native One-Handed Weapon - Two-Handed Grip", "ForceMultiplier");
            CapturePreservedValue<float>(profile, "Native One-Handed Weapon - Two-Handed Grip", "AxeMeleeRangeMultiplier");
            CapturePreservedValue<float>(profile, "Native One-Handed Weapon - Two-Handed Grip", "MaceMeleeRangeMultiplier");
            CapturePreservedValue<float>(profile, "Advanced First-Person Alignment", "OneHandedSwordPositionY");
            CapturePreservedValue<float>(profile, "Advanced First-Person Alignment", "OneHandedMacePositionY");
            CapturePreservedValue<float>(profile, "Advanced First-Person Alignment", "OneHandedAxePositionY");
            CapturePreservedValue<float>(profile, "Grip Switching", "GripHoldSeconds");
            CapturePreservedValue<bool>(profile, "Grip Switching", "ProficiencyFollowsGrip");
            CapturePreservedValue<bool>(profile, "Grip Switching", "RememberGripPerLoadout");
            CapturePreservedValue<bool>(profile, "Diagnostics", "Enabled");
            CapturePreservedValue<StrengthTestMode>(profile, "Diagnostics", "StrengthTestMode");
            CapturePreservedValue<bool>(profile, "Diagnostics", "ShowGrailFloatingTextDiagnostics");
            CapturePreservedValue<bool>(profile, "Reverse Hands Compatibility", "TwoHandedGripUsesNormalHands");
            CapturePreservedValue<bool>(profile, "Reverse Hands Compatibility", "SingleSpellUsesNormalHands");
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
            RestorePreservedValue(_zeroRequirementFullPotencyStrength, ref restored, ref clamped, ref invalid);
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
            RestorePreservedValue(_rememberGripPerLoadout, ref restored, ref clamped, ref invalid);
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

            if (args != null
                && _rememberGripPerLoadout != null
                && ReferenceEquals(
                    args.ChangedSetting,
                    _rememberGripPerLoadout))
            {
                _gripMemoryContextKey = null;
                _pendingGripMemoryInvalidationContextKey = null;
                _pendingGripMemoryInvalidationStableFrames = 0;
            }

            if (_enabled != null && !_enabled.Value)
            {
                CancelGripEquipInputGuard();
                RestoreOffHandTwoHandedPresentation();
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

        private void PatchGripMemoryPersistenceHooks()
        {
            Type newGameLoadingType = AccessTools.TypeByName(
                "Awaken.TG.Main.UI.TitleScreen.Loading.LoadingTypes.NewGameLoading");
            PatchOptionalDeclaredMethod(
                newGameLoadingType,
                "DropPreviousDomains",
                typeof(NewGameGripMemoryPatch),
                nameof(NewGameGripMemoryPatch.Prefix));

            string[] cloudServiceTypes =
            {
                "Awaken.TG.Main.Saving.Cloud.Services.SteamCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.SteamNoCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.DebugCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.GogCloudService"
            };
            for (int i = 0; i < cloudServiceTypes.Length; i++)
            {
                Type type = AccessTools.TypeByName(cloudServiceTypes[i]);
                PatchOptionalDeclaredMethod(
                    type,
                    "EndLoadSlot",
                    typeof(CloudServiceLoadGripMemoryPatch),
                    nameof(CloudServiceLoadGripMemoryPatch.Prefix));
                PatchOptionalDeclaredMethod(
                    type,
                    "EndSave",
                    typeof(CloudServiceSaveGripMemoryPatch),
                    nameof(CloudServiceSaveGripMemoryPatch.Prefix));
            }
        }

        private void PatchOptionalDeclaredMethod(
            Type declaringType,
            string methodName,
            Type patchType,
            string patchMethodName)
        {
            if (declaringType == null)
            {
                return;
            }

            MethodInfo original =
                AccessTools.Method(declaringType, methodName);
            MethodInfo patch =
                AccessTools.Method(patchType, patchMethodName);
            if (original == null
                || original.DeclaringType != declaringType
                || patch == null)
            {
                return;
            }

            try
            {
                _harmony.Patch(
                    original,
                    prefix: new HarmonyMethod(patch));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not install the optional grip-memory hook for "
                    + declaringType.FullName
                    + "."
                    + methodName
                    + ": "
                    + exception.GetBaseException().Message);
            }
        }

        internal void ClearGripMemorySession()
        {
            _gripMemories.Clear();
            _activeGripMemorySaveSlot = null;
            _gripMemoryContextKey = null;
            _pendingGripMemoryInvalidationContextKey = null;
            _pendingGripMemoryInvalidationStableFrames = 0;
        }

        internal void LoadGripMemoryState(
            object cloudService,
            string slotId)
        {
            _activeGripMemorySaveSlot = slotId;
            _gripMemories.Clear();
            _gripMemoryContextKey = null;
            _pendingGripMemoryInvalidationContextKey = null;
            _pendingGripMemoryInvalidationStableFrames = 0;

            byte[] archiveData = null;
            try
            {
                MethodInfo tryLoad = cloudService == null
                    ? null
                    : AccessTools.Method(
                        cloudService.GetType(),
                        "TryLoadSlotFile",
                        new[]
                        {
                            typeof(string),
                            typeof(byte[]).MakeByRefType()
                        });
                if (tryLoad != null)
                {
                    object[] args =
                    {
                        GripMemoryFileName,
                        null
                    };
                    object result = tryLoad.Invoke(cloudService, args);
                    if (result is bool && (bool)result)
                    {
                        archiveData = args[1] as byte[];
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not read grip memory from the save archive: "
                    + exception.GetBaseException().Message);
            }

            bool loaded = TryParseGripMemoryState(archiveData);
            if (!loaded)
            {
                try
                {
                    string localPath =
                        GetGripMemoryLocalPath(slotId);
                    if (File.Exists(localPath))
                    {
                        loaded = TryParseGripMemoryState(
                            File.ReadAllBytes(localPath));
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(
                        "Could not read the local grip-memory backup: "
                        + exception.GetBaseException().Message);
                }
            }

            LogDiagnostic(
                loaded
                    ? "Loaded "
                        + _gripMemories.Count.ToString(
                            CultureInfo.InvariantCulture)
                        + " exact-equipment grip-memory record(s) for save slot "
                        + slotId
                        + "."
                    : "The loaded save has no valid Versatile Weapons grip memory; loadouts will use their normal defaults.");
        }

        internal void SaveGripMemoryState(
            bool writeToArchive,
            object cloudService = null,
            string slotId = null)
        {
            if (!String.IsNullOrEmpty(slotId))
            {
                _activeGripMemorySaveSlot = slotId;
            }
            if (String.IsNullOrEmpty(_activeGripMemorySaveSlot))
            {
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(
                SerializeGripMemoryState());
            try
            {
                string localPath = GetGripMemoryLocalPath(
                    _activeGripMemorySaveSlot);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(localPath));
                File.WriteAllBytes(localPath, data);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not write the local grip-memory backup: "
                    + exception.GetBaseException().Message);
            }

            if (!writeToArchive || cloudService == null)
            {
                return;
            }

            try
            {
                MethodInfo save = AccessTools.Method(
                    cloudService.GetType(),
                    "SaveSlotFile",
                    new[] { typeof(string), typeof(byte[]) });
                if (save != null)
                {
                    save.Invoke(
                        cloudService,
                        new object[] { GripMemoryFileName, data });
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not write grip memory into the save archive: "
                    + exception.GetBaseException().Message);
            }
        }

        private string SerializeGripMemoryState()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Format=")
                .Append(GripMemoryFormat)
                .AppendLine();
            List<string> keys =
                new List<string>(_gripMemories.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0;
                i < keys.Count && i < MaximumGripMemoryRecords;
                i++)
            {
                GripMemoryRecord record = _gripMemories[keys[i]];
                if (record == null)
                {
                    continue;
                }
                builder.Append("R.")
                    .Append(keys[i])
                    .Append('=')
                    .Append(record.OwnerHand)
                    .Append('|')
                    .Append(EncodeGripMemoryValue(record.WeaponId))
                    .Append('|')
                    .Append(EncodeGripMemoryValue(record.PairedItemId))
                    .Append('|')
                    .Append(record.TwoHandedGrip ? '1' : '0')
                    .AppendLine();
            }
            return builder.ToString();
        }

        private bool TryParseGripMemoryState(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return false;
            }

            Dictionary<string, GripMemoryRecord> parsed =
                new Dictionary<string, GripMemoryRecord>(
                    StringComparer.Ordinal);
            bool validFormat = false;
            try
            {
                string[] lines = Encoding.UTF8.GetString(data).Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (String.Equals(
                        lines[i],
                        "Format="
                            + GripMemoryFormat.ToString(
                                CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                    {
                        validFormat = true;
                        continue;
                    }
                    if (!lines[i].StartsWith(
                        "R.",
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int separator = lines[i].IndexOf('=');
                    if (separator <= 2)
                    {
                        continue;
                    }
                    string contextKey =
                        lines[i].Substring(2, separator - 2);
                    string[] fields = lines[i]
                        .Substring(separator + 1)
                        .Split('|');
                    string weaponId;
                    string pairedItemId;
                    if (!IsValidGripMemoryContextKey(contextKey)
                        || fields.Length != 4
                        || (fields[0] != "M" && fields[0] != "O")
                        || (fields[3] != "0" && fields[3] != "1")
                        || !TryDecodeGripMemoryValue(
                            fields[1],
                            out weaponId)
                        || !TryDecodeGripMemoryValue(
                            fields[2],
                            out pairedItemId)
                        || String.IsNullOrEmpty(weaponId)
                        || weaponId.Length > 512
                        || pairedItemId.Length > 512)
                    {
                        continue;
                    }
                    parsed[contextKey] = new GripMemoryRecord
                    {
                        OwnerHand = fields[0],
                        WeaponId = weaponId,
                        PairedItemId = pairedItemId,
                        TwoHandedGrip = fields[3] == "1"
                    };
                    if (parsed.Count >= MaximumGripMemoryRecords)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not parse grip memory: "
                    + exception.GetBaseException().Message);
                return false;
            }

            if (!validFormat)
            {
                return false;
            }
            _gripMemories.Clear();
            foreach (KeyValuePair<string, GripMemoryRecord> pair in parsed)
            {
                _gripMemories[pair.Key] = pair.Value;
            }
            return true;
        }

        private static string EncodeGripMemoryValue(string value)
        {
            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value ?? String.Empty));
        }

        private static bool TryDecodeGripMemoryValue(
            string value,
            out string decoded)
        {
            decoded = null;
            try
            {
                decoded = Encoding.UTF8.GetString(
                    Convert.FromBase64String(value ?? String.Empty));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidGripMemoryContextKey(string key)
        {
            int index;
            return !String.IsNullOrEmpty(key)
                && key.Length >= 2
                && (key[0] == 'N' || key[0] == 'G')
                && Int32.TryParse(
                    key.Substring(1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out index)
                && index >= 0
                && index <= 99;
        }

        private static string GetGripMemoryLocalPath(string slotId)
        {
            string safeName = slotId ?? "Unknown";
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalid, '_');
            }
            return Path.Combine(
                Paths.ConfigPath,
                "VersatileWeapons",
                "GripMemory",
                safeName + ".grips");
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        internal void BeginUnsheatheAudioDiagnostic(
            EquipWeaponBase<HeroAnimatorSubstateMachine> equipState)
        {
            _unsheatheAudioDiagnosticFsm = null;
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            Hero hero = Hero.Current;
            HeroAnimatorSubstateMachine sourceFsm = equipState == null
                ? null
                : equipState.ParentModel;
            _unsheatheAudioDiagnosticFsm = sourceFsm;
            Item sourceItem = sourceFsm == null
                ? (hero == null ? null : hero.MainHandItem)
                : sourceFsm.MainHandItem;
            MagicFSM magicFsm = sourceFsm as MagicFSM;
            List<HeroAnimatorSubstateMachine> currentFsms =
                new List<HeroAnimatorSubstateMachine>();
            if (hero != null)
            {
                AddEquipFsm(
                    currentFsms,
                    hero.TryGetElement<OneHandedFSM>());
                AddEquipFsm(
                    currentFsms,
                    hero.TryGetElement<TwoHandedFSM>());
                AddEquipFsm(
                    currentFsms,
                    hero.TryGetElement<DualHandedFSM>());
                AddEquipFsm(
                    currentFsms,
                    hero.TryGetElement<MagicMeleeOffHandFSM>());
                AddEquipFsm(
                    currentFsms,
                    hero.TryGetElement<MagicMainHandFSM>());
                AddEquipFsm(
                    currentFsms,
                    hero.TryGetElement<MagicOffHandFSM>());
            }

            LogDiagnostic(
                "Unsheathe audio diagnostic: request=game-equip-state"
                + "; state="
                + (equipState == null
                    ? "none"
                    : equipState.GetType().Name)
                + "; sourceFsm="
                + (sourceFsm == null
                    ? "none"
                    : sourceFsm.GetType().Name)
                + "; castingHand="
                + (magicFsm == null
                    ? "not-magic"
                    : magicFsm.CastingHand.ToString())
                + "; sourceItem="
                + DescribeAudioDiagnosticItem(sourceItem)
                + "; mainItem="
                + DescribeAudioDiagnosticItem(
                    hero == null ? null : hero.MainHandItem)
                + "; offItem="
                + DescribeAudioDiagnosticItem(
                    hero == null ? null : hero.OffHandItem)
                + "; mainHand="
                + DescribeAudioDiagnosticHand(
                    hero == null ? null : hero.MainHandWeapon)
                + "; offHand="
                + DescribeAudioDiagnosticHand(
                    hero == null ? null : hero.OffHandWeapon)
                + "; mainSuppressed="
                + IsMainHandSuppressed()
                + "; offSuppressed="
                + IsOffHandSuppressed()
                + "; heroDrawn="
                + (hero != null && hero.IsWeaponEquipped)
                + "; loadout="
                + (_observedLoadoutIndexKnown
                    ? _observedLoadoutIndex.ToString(
                        CultureInfo.InvariantCulture)
                    : "unknown")
                + "; refreshStage="
                + _pairedRefreshStage
                + "; equipFsmResetStage="
                + _equipFsmResetStage
                + "; transitionGeneration="
                + _weaponTransitionGeneration
                + "; fsms="
                + DescribeEquipFsms(currentFsms)
                + ".");
        }

        internal static void EndUnsheatheAudioDiagnostic()
        {
            _unsheatheAudioDiagnosticFsm = null;
        }

        internal void RecordFmodTransitionAudioDiagnostic(
            string route,
            EventReference eventReference,
            object[] arguments)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            bool unsheatheCorrelated =
                _unsheatheAudioDiagnosticFsm != null;
            bool transitionWindowActive =
                _audioDiagnosticTransitionUntil >= Time.unscaledTime;
            if (!unsheatheCorrelated && !transitionWindowActive)
            {
                return;
            }

            GameObject attachedObject = null;
            UnityEngine.Object debugObject = null;
            string position = "none";
            int parameterCount = 0;
            if (arguments != null)
            {
                foreach (object argument in arguments)
                {
                    GameObject candidateObject = argument as GameObject;
                    if (candidateObject != null)
                    {
                        attachedObject = candidateObject;
                        continue;
                    }

                    if (argument is Vector3)
                    {
                        Vector3 value = (Vector3)argument;
                        position = value.x.ToString(
                                "0.00",
                                CultureInfo.InvariantCulture)
                            + ","
                            + value.y.ToString(
                                "0.00",
                                CultureInfo.InvariantCulture)
                            + ","
                            + value.z.ToString(
                                "0.00",
                                CultureInfo.InvariantCulture);
                        continue;
                    }

                    FMODParameter[] parameterArray =
                        argument as FMODParameter[];
                    if (parameterArray != null)
                    {
                        parameterCount = parameterArray.Length;
                        continue;
                    }

                    ICollection<FMODParameter> parameterCollection =
                        argument as ICollection<FMODParameter>;
                    if (parameterCollection != null)
                    {
                        parameterCount = parameterCollection.Count;
                        continue;
                    }

                    UnityEngine.Object candidateDebugObject =
                        argument as UnityEngine.Object;
                    if (candidateDebugObject != null)
                    {
                        debugObject = candidateDebugObject;
                    }
                }
            }

            Hero hero = Hero.Current;
            bool heroAttached = IsHeroAudioObject(
                hero,
                attachedObject);
            if (String.Equals(
                    route,
                    "attached",
                    StringComparison.Ordinal)
                && !unsheatheCorrelated
                && !heroAttached)
            {
                return;
            }

            LogDiagnostic(
                "FMOD transition audio diagnostic: route="
                + route
                + "; event="
                + (eventReference.IsNull
                    ? "null"
                    : eventReference.PathOrGuid)
                + "; unsheatheCorrelated="
                + unsheatheCorrelated
                + "; unsheatheSourceFsm="
                + (_unsheatheAudioDiagnosticFsm == null
                    ? "none"
                    : _unsheatheAudioDiagnosticFsm.GetType().Name)
                + "; attachedObject="
                + DescribeAudioDiagnosticObject(attachedObject)
                + "; heroAttached="
                + heroAttached
                + "; debugObject="
                + DescribeAudioDiagnosticObject(debugObject)
                + "; position="
                + position
                + "; parameters="
                + parameterCount
                + "; windowGeneration="
                + _weaponTransitionGeneration
                + "; windowAge="
                + (_audioDiagnosticTransitionStartedAt < 0.0f
                    ? "none"
                    : (Time.unscaledTime
                        - _audioDiagnosticTransitionStartedAt).ToString(
                            "0.000",
                            CultureInfo.InvariantCulture))
                + ".");
        }

        internal void RecordSoulRendAudioLifecycleDiagnostic(
            string route,
            EventReference eventReference,
            UnityEngine.Object owner,
            bool? asOneShot)
        {
            if (_diagnostics == null
                || !_diagnostics.Value
                || _audioDiagnosticTransitionUntil < Time.unscaledTime)
            {
                return;
            }

            string eventId = eventReference.IsNull
                ? "null"
                : eventReference.PathOrGuid;
            bool suspectedWhisper = String.Equals(
                eventId,
                SuspectedSoulRendWhisperEvent,
                StringComparison.OrdinalIgnoreCase);
            if (!suspectedWhisper
                && !String.Equals(
                    route,
                    "magic-idle",
                    StringComparison.Ordinal))
            {
                return;
            }

            CharacterHandBase hand = owner as CharacterHandBase;
            Hero hero = Hero.Current;
            List<string> callChain = new List<string>();
            System.Diagnostics.StackFrame[] frames =
                new System.Diagnostics.StackTrace(2, false).GetFrames();
            if (frames != null)
            {
                int frameCount = Math.Min(10, frames.Length);
                for (int index = 0; index < frameCount; index++)
                {
                    MethodBase method = frames[index].GetMethod();
                    if (method != null)
                    {
                        callChain.Add(
                            (method.DeclaringType == null
                                ? "unknown"
                                : method.DeclaringType.Name)
                            + "."
                            + method.Name);
                    }
                }
            }

            LogDiagnostic(
                "Soul Rend audio lifecycle diagnostic: route="
                + route
                + "; event="
                + eventId
                + "; suspectedWhisper="
                + suspectedWhisper
                + "; asOneShot="
                + (asOneShot.HasValue
                    ? asOneShot.Value.ToString()
                    : "not-applicable")
                + "; owner="
                + DescribeAudioDiagnosticObject(owner)
                + "; hand="
                + DescribeAudioDiagnosticHand(hand)
                + "; currentMainHand="
                + (hero != null
                    && hand != null
                    && ReferenceEquals(hero.MainHandWeapon, hand))
                + "; currentOffHand="
                + (hero != null
                    && hand != null
                    && ReferenceEquals(hero.OffHandWeapon, hand))
                + "; mainSuppressed="
                + IsMainHandSuppressed()
                + "; offSuppressed="
                + IsOffHandSuppressed()
                + "; transitionGeneration="
                + _weaponTransitionGeneration
                + "; windowAge="
                + (Time.unscaledTime
                    - _audioDiagnosticTransitionStartedAt).ToString(
                        "0.000",
                        CultureInfo.InvariantCulture)
                + "; callChain="
                + (callChain.Count == 0
                    ? "unavailable"
                    : string.Join(" <- ", callChain.ToArray()))
                + ".");
        }

        internal void BeginWeaponAudioTransition(string route)
        {
            if (_enabled == null || !_enabled.Value)
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null)
            {
                return;
            }

            if (!_weaponAudioTransitionActive)
            {
                if (Time.unscaledTime > _weaponAudioGuardUntil)
                {
                    _weaponAudioGuardedItems.Clear();
                }

                _weaponAudioPreviousParticipants.Clear();
                _weaponAudioPreviousAudible.Clear();
                CollectWeaponAudioState(
                    hero,
                    _weaponAudioPreviousParticipants,
                    _weaponAudioPreviousAudible);
                AddExactItems(
                    _weaponAudioGuardedItems,
                    _weaponAudioPreviousParticipants);
                _weaponAudioTransitionActive = true;
                _weaponAudioTransitionMuted = hero.MuteEquips;
            }
            else
            {
                List<Item> currentParticipants = new List<Item>();
                CollectWeaponAudioState(
                    hero,
                    currentParticipants,
                    null);
                AddExactItems(
                    _weaponAudioGuardedItems,
                    currentParticipants);
                _weaponAudioTransitionMuted =
                    _weaponAudioTransitionMuted || hero.MuteEquips;
            }

            _weaponAudioTransitionStartedAt = Time.unscaledTime;
            _weaponAudioTransitionStartedFrame = Time.frameCount;
            _weaponAudioStableFrames = 0;
            _weaponAudioObservedMainItem = null;
            _weaponAudioObservedOffItem = null;
            LogDiagnostic(
                "Started or extended hero weapon-audio transition: route="
                + route
                + "; previousParticipants="
                + DescribeWeaponAudioItems(
                    _weaponAudioPreviousParticipants)
                + "; previousAudible="
                + DescribeWeaponAudioItems(
                    _weaponAudioPreviousAudible)
                + ".");
        }

        internal bool ShouldAllowWeaponToggleAudio(
            ItemEquip itemEquip,
            IAlive owner,
            bool equip)
        {
            if (_weaponAudioPlaybackBypass
                || _enabled == null
                || !_enabled.Value
                || itemEquip == null
                || itemEquip.Item == null)
            {
                return true;
            }

            Hero hero = Hero.Current;
            if (hero == null || !ReferenceEquals(owner, hero))
            {
                return true;
            }

            Item item = itemEquip.Item;
            bool currentParticipant = IsCurrentHeroHandItem(hero, item);
            bool managedParticipant = ContainsExact(
                    _weaponAudioGuardedItems,
                    item)
                || ContainsExact(
                    _weaponAudioPreviousParticipants,
                    item);
            if (_weaponAudioTransitionActive
                && (currentParticipant || managedParticipant))
            {
                AddExactItem(_weaponAudioGuardedItems, item);
                LogDiagnostic(
                    "Deferred vanilla hero weapon toggle audio: direction="
                    + (equip ? "equip" : "unequip")
                    + "; item="
                    + DescribeAudioDiagnosticItem(item)
                    + ".");
                return false;
            }

            if (Time.unscaledTime <= _weaponAudioGuardUntil
                && managedParticipant)
            {
                LogDiagnostic(
                    "Rejected a late hero weapon toggle callback already owned by the settled audio transition: direction="
                    + (equip ? "equip" : "unequip")
                    + "; item="
                    + DescribeAudioDiagnosticItem(item)
                    + ".");
                return false;
            }

            return true;
        }

        private void ProcessWeaponAudioTransition(Hero hero)
        {
            if (_enabled == null || !_enabled.Value)
            {
                CancelWeaponAudioTransition();
                return;
            }

            if (!_weaponAudioTransitionActive)
            {
                if (_weaponAudioGuardedItems.Count > 0
                    && Time.unscaledTime > _weaponAudioGuardUntil)
                {
                    _weaponAudioGuardedItems.Clear();
                }
                return;
            }

            if (hero == null)
            {
                CancelWeaponAudioTransition();
                return;
            }
            if (Time.timeScale <= 0.0f)
            {
                _weaponAudioTransitionStartedAt = Time.unscaledTime;
                return;
            }

            bool mainSuppressed = WeaponAudioSlotIsSuppressed(
                hero,
                hero.MainHandItem,
                true);
            bool offSuppressed = WeaponAudioSlotIsSuppressed(
                hero,
                hero.OffHandItem,
                false);
            bool stateUnchanged = ReferenceEquals(
                    _weaponAudioObservedMainItem,
                    hero.MainHandItem)
                && ReferenceEquals(
                    _weaponAudioObservedOffItem,
                    hero.OffHandItem)
                && _weaponAudioObservedMainSuppressed == mainSuppressed
                && _weaponAudioObservedOffSuppressed == offSuppressed;
            if (stateUnchanged)
            {
                _weaponAudioStableFrames++;
            }
            else
            {
                _weaponAudioObservedMainItem = hero.MainHandItem;
                _weaponAudioObservedOffItem = hero.OffHandItem;
                _weaponAudioObservedMainSuppressed = mainSuppressed;
                _weaponAudioObservedOffSuppressed = offSuppressed;
                _weaponAudioStableFrames = 0;
            }

            List<Item> currentParticipants = new List<Item>();
            CollectWeaponAudioState(hero, currentParticipants, null);
            AddExactItems(
                _weaponAudioGuardedItems,
                currentParticipants);

            bool timedOut = Time.unscaledTime
                - _weaponAudioTransitionStartedAt
                >= WeaponAudioCallbackGuardSeconds;
            if (!timedOut
                && (Time.frameCount <= _weaponAudioTransitionStartedFrame
                    || _weaponAudioStableFrames
                        < WeaponAudioStableFrames
                    || !WeaponAudioStateIsSettled(hero)))
            {
                return;
            }

            FinalizeWeaponAudioTransition(hero, timedOut);
        }

        private bool WeaponAudioStateIsSettled(Hero hero)
        {
            if (hero == null || Time.timeScale <= 0.0f)
            {
                return false;
            }

            CharacterHandBase mainHand = hero.MainHandWeapon;
            CharacterHandBase offHand = hero.OffHandWeapon;
            if ((mainHand != null && mainHand.IsLoadingAnimator)
                || (offHand != null && offHand.IsLoadingAnimator)
                || HeroWeaponEvents.Current.IsLoadingAnimations())
            {
                return false;
            }

            CharacterHand gripWeapon = FindGripSwitchWeapon(hero);
            return gripWeapon == null
                || (!_weaponTransitionRefreshPending
                    && _pairedRefreshStage == PairedRefreshStage.None
                    && _equipFsmResetStage == EquipFsmResetStage.None
                    && _rememberedGripAnimationRefreshItem == null);
        }

        private void FinalizeWeaponAudioTransition(
            Hero hero,
            bool timedOut)
        {
            List<Item> finalParticipants = new List<Item>();
            List<Item> finalAudible = new List<Item>();
            CollectWeaponAudioState(
                hero,
                finalParticipants,
                finalAudible);
            AddExactItems(
                _weaponAudioGuardedItems,
                finalParticipants);

            _weaponAudioTransitionActive = false;
            _weaponAudioGuardUntil = Time.unscaledTime
                + WeaponAudioCallbackGuardSeconds;

            foreach (Item previous in _weaponAudioPreviousAudible)
            {
                if (!_weaponAudioTransitionMuted
                    && !ContainsExact(finalParticipants, previous))
                {
                    PlayManagedWeaponToggleAudio(
                        hero,
                        previous,
                        false);
                }
            }
            foreach (Item current in finalAudible)
            {
                if (!_weaponAudioTransitionMuted
                    && !ContainsExact(
                    _weaponAudioPreviousAudible,
                    current))
                {
                    PlayManagedWeaponToggleAudio(
                        hero,
                        current,
                        true);
                }
            }

            LogDiagnostic(
                "Finalized hero weapon-audio transition: timedOut="
                + timedOut
                + "; muted="
                + _weaponAudioTransitionMuted
                + "; previousAudible="
                + DescribeWeaponAudioItems(
                    _weaponAudioPreviousAudible)
                + "; finalParticipants="
                + DescribeWeaponAudioItems(finalParticipants)
                + "; finalAudible="
                + DescribeWeaponAudioItems(finalAudible)
                + ".");
            _weaponAudioPreviousParticipants.Clear();
            _weaponAudioPreviousAudible.Clear();
            _weaponAudioTransitionMuted = false;
        }

        private void PlayManagedWeaponToggleAudio(
            Hero hero,
            Item item,
            bool equip)
        {
            ItemEquip itemEquip = item == null
                ? null
                : item.TryGetElement<ItemEquip>();
            if (hero == null || itemEquip == null)
            {
                return;
            }

            _weaponAudioPlaybackBypass = true;
            try
            {
                itemEquip.PlayEquipToggleSound(hero, equip);
            }
            finally
            {
                _weaponAudioPlaybackBypass = false;
            }
            LogDiagnostic(
                "Played settled hero weapon toggle audio: direction="
                + (equip ? "equip" : "unequip")
                + "; item="
                + DescribeAudioDiagnosticItem(item)
                + ".");
        }

        private void CollectWeaponAudioState(
            Hero hero,
            List<Item> participants,
            List<Item> audible)
        {
            if (hero == null)
            {
                return;
            }

            AddWeaponAudioSlot(
                hero,
                hero.MainHandItem,
                true,
                participants,
                audible);
            AddWeaponAudioSlot(
                hero,
                hero.OffHandItem,
                false,
                participants,
                audible);
        }

        private void AddWeaponAudioSlot(
            Hero hero,
            Item item,
            bool mainHand,
            List<Item> participants,
            List<Item> audible)
        {
            if (item == null || IsEmptyHandPlaceholder(item))
            {
                return;
            }

            AddExactItem(participants, item);
            if (audible != null
                && !WeaponAudioSlotIsSuppressed(
                    hero,
                    item,
                    mainHand))
            {
                AddExactItem(audible, item);
            }
        }

        private bool WeaponAudioSlotIsSuppressed(
            Hero hero,
            Item item,
            bool mainHand)
        {
            return item != null
                && ((mainHand
                        ? IsMainHandSuppressed()
                        : IsOffHandSuppressed())
                    || (_hiddenPairedHand != null
                        && ReferenceEquals(
                            _hiddenPairedHand.Item,
                            item)));
        }

        private static bool IsCurrentHeroHandItem(
            Hero hero,
            Item item)
        {
            return hero != null
                && item != null
                && (ReferenceEquals(hero.MainHandItem, item)
                    || ReferenceEquals(hero.OffHandItem, item));
        }

        private static bool ContainsExact(
            List<Item> items,
            Item candidate)
        {
            if (items == null || candidate == null)
            {
                return false;
            }

            foreach (Item item in items)
            {
                if (ReferenceEquals(item, candidate))
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddExactItem(
            List<Item> items,
            Item candidate)
        {
            if (items != null
                && candidate != null
                && !ContainsExact(items, candidate))
            {
                items.Add(candidate);
            }
        }

        private static void AddExactItems(
            List<Item> destination,
            List<Item> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (Item item in source)
            {
                AddExactItem(destination, item);
            }
        }

        private static string DescribeWeaponAudioItems(List<Item> items)
        {
            if (items == null || items.Count == 0)
            {
                return "none";
            }

            List<string> descriptions = new List<string>();
            foreach (Item item in items)
            {
                descriptions.Add(DescribeAudioDiagnosticItem(item));
            }
            return string.Join(",", descriptions.ToArray());
        }

        private void CancelWeaponAudioTransition()
        {
            _weaponAudioTransitionActive = false;
            _weaponAudioTransitionMuted = false;
            _weaponAudioTransitionStartedAt = -1.0f;
            _weaponAudioStableFrames = 0;
            _weaponAudioPreviousParticipants.Clear();
            _weaponAudioPreviousAudible.Clear();
            _weaponAudioGuardedItems.Clear();
            _weaponAudioGuardUntil = -1.0f;
        }

        private static bool IsHeroAudioObject(
            Hero hero,
            GameObject gameObject)
        {
            return hero != null
                && hero.ParentTransform != null
                && gameObject != null
                && (ReferenceEquals(
                        gameObject,
                        hero.ParentTransform.gameObject)
                    || gameObject.transform.IsChildOf(
                        hero.ParentTransform));
        }

        private static string DescribeAudioDiagnosticObject(
            UnityEngine.Object value)
        {
            return value == null
                ? "none"
                : value.GetType().Name
                    + "{name="
                    + value.name
                    + "}";
        }

        private static string DescribeAudioDiagnosticItem(Item item)
        {
            return item == null
                ? "none"
                : "runtime-"
                    + item.GetHashCode().ToString(
                        "X8",
                        CultureInfo.InvariantCulture)
                    + "{template="
                    + (item.Template == null
                        ? "none"
                        : item.Template.GUID)
                    + ",magic="
                    + item.IsMagic
                    + ",melee="
                    + item.IsMelee
                    + "}";
        }

        private static string DescribeAudioDiagnosticHand(
            CharacterHandBase hand)
        {
            return hand == null
                ? "none"
                : hand.GetType().Name
                    + "{item="
                    + DescribeAudioDiagnosticItem(hand.Item)
                    + ",hidden="
                    + hand.IsHidden
                    + ",loading="
                    + hand.IsLoadingAnimator
                    + "}";
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
            bool supportedPairing = IsSupportedPairedHandItem(pairedItem);
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
                    + " | grip switching blocked: paired item requires both hands.";
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
                && !IsSupportedPairedHandItem(pairedItem))
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

            if (IsShield(pairedItem))
            {
                return "shield equipped";
            }
            if (pairedItem.IsMagic)
            {
                return "spell equipped";
            }
            if (pairedItem.IsMelee)
            {
                return "melee weapon equipped";
            }

            return "offhand occupied";
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
                RestoreOffHandTwoHandedPresentation();
                RestoreHiddenPairedHand();
                ClearObservedWeapon();
                _pairedHandVisibilityRecoveryCandidate = null;
                _drawnPairedHandHiddenSince = -1.0f;
                return;
            }

            if (_weaponTransitionRefreshPending
                && Time.timeScale > 0.0f
                && --_weaponTransitionRefreshFramesRemaining <= 0)
            {
                _weaponTransitionRefreshPending = false;
                Logger.LogWarning(
                    "Equipment transition readiness timed out; leaving the game's loaded controllers untouched and retaining normal visibility recovery.");
            }

            Hero hero = Hero.Current;
            ObserveLoadoutIndex(hero);
            ObserveDiagnosticWeaponState(hero);

            CharacterHand gripWeapon = FindGripSwitchWeapon(hero);
            if (gripWeapon == null || gripWeapon.Item == null)
            {
                ObserveGripMemoryWithoutSupportedWeapon(hero);
                _drawnWeaponHiddenSince = -1.0f;
                if (_gripItem != null)
                {
                    RestoreHiddenPairedHand();
                    ClearRememberedGripAnimationRefresh();
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

            UpdateOffHandTwoHandedPresentation(hero);

            MonitorCanceledPairedHandVisibility(hero);
            ProcessPendingGripMemoryInvalidation(hero);

            if (ProcessRememberedGripAnimationRefresh(
                    hero,
                    gripWeapon))
            {
                return;
            }

            if (_pairedRefreshStage != PairedRefreshStage.None
                && ProcessPairedRefresh(
                    hero,
                    _pairedRefreshWeapon))
            {
                return;
            }

            if (_equipFsmResetStage
                    != EquipFsmResetStage.None
                && ProcessEquipFsmReset(hero, gripWeapon))
            {
                return;
            }

            if (TryFinalizeNativeOneHandedAfterWeaponTransition(
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
                    || IsSupportedPairedHandItem(pairedItem);
                if (!canReconcileNativeGrip
                    || gripWeapon.IsHidden
                    || !NativeGripAnimatorIsReady(hero, gripWeapon))
                {
                    _gripFsmMismatchFrames = 0;
                    return;
                }

                GripCombatMode combatMode = GetGripCombatMode(
                    hero,
                    gripWeapon);
                if (GripFsmMatches(hero, combatMode))
                {
                    _gripFsmMismatchFrames = 0;
                    return;
                }

                ReconcileGripFsmState(hero, combatMode);
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
                && Time.timeScale > 0.0f
                && !hero.IsPerformingAction
                && !weapon.IsHidden
                && HandAnimatorsAreSettled(
                    hero,
                    weapon,
                    true))
            {
                Item transitionedPairedItem =
                    GetPairedItem(hero, weapon.Item);
                if (transitionedPairedItem != null
                    && !IsSupportedPairedHandItem(
                        transitionedPairedItem))
                {
                    _weaponTransitionRefreshPending = false;
                    return;
                }

                CharacterHandBase transitionedPairedHand =
                    FindHandBaseForItem(
                        hero,
                        transitionedPairedItem);
                if (!MagicVisualIsReady(
                        hero,
                        transitionedPairedHand))
                {
                    TryRecoverMissingMagicVisualAfterTransition(
                        hero,
                        transitionedPairedHand,
                        _weaponTransitionStartedAt);
                    return;
                }

                bool selectedControllerMismatch =
                    _selectedGripControllerKnown
                    && ReferenceEquals(
                        _selectedGripControllerItem,
                        weapon.Item)
                    && _selectedGripControllerTwoHanded
                        != !desiredState;
                if (selectedControllerMismatch)
                {
                    _weaponTransitionRefreshPending = false;
                    LogDiagnostic(
                        "Correcting the grip controller selected before the equipment pairing finished settling. "
                        + DescribeGripContext(hero, weapon));
                    RefreshWeaponAnimations(
                        hero,
                        weapon,
                        desiredState);
                    return;
                }

                if (_observedAnimationStateKnown
                    && _observedAnimationState != desiredState)
                {
                    return;
                }

                BeginEquipFsmReset(
                    hero,
                    weapon,
                    transitionedPairedItem);
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
                ReconcileGripFsmState(
                    hero,
                    GetGripCombatMode(hero, weapon));
                _observedAnimationState = true;
                _observedAnimationStateKnown = true;
                _oneHandedReconciliationPending = false;

                Logger.LogInfo(
                    "Finalized the one-handed grip and paired-hand animation state after both animators settled.");
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

            GripCombatMode desiredCombatMode = GetGripCombatMode(
                hero,
                weapon);
            if (GripFsmMatches(hero, desiredCombatMode))
            {
                _gripFsmMismatchFrames = 0;
                return;
            }

            ReconcileGripFsmState(hero, desiredCombatMode);
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
            ProcessWeaponAudioTransition(Hero.Current);
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

        private void UpdateOffHandTwoHandedPresentation(Hero hero)
        {
            CharacterHandBase weapon =
                GetActiveOffHandTwoHandedGripWeapon(hero);
            if (weapon == null)
            {
                RestoreOffHandTwoHandedPresentation();
                return;
            }

            Transform weaponTransform = weapon.transform;
            Transform mainHandSocket = hero.MainHand;
            if (weaponTransform == null || mainHandSocket == null)
            {
                return;
            }

            if (!ReferenceEquals(
                _offHandTwoHandedPresentationWeapon,
                weapon))
            {
                RestoreOffHandTwoHandedPresentation();
                _offHandTwoHandedPresentationWeapon = weapon;
                _offHandTwoHandedPresentationOriginalParent =
                    weaponTransform.parent;
                _offHandTwoHandedPresentationLocalPosition =
                    weaponTransform.localPosition;
                _offHandTwoHandedPresentationLocalRotation =
                    weaponTransform.localRotation;
                _offHandTwoHandedPresentationLocalScale =
                    weaponTransform.localScale;
            }

            Transform offHandSocket = hero.OffHand;
            if (offHandSocket != null
                && !ReferenceEquals(offHandSocket, mainHandSocket))
            {
                _offHandTwoHandedPresentationOriginalParent =
                    offHandSocket;
            }

            if (ReferenceEquals(weaponTransform.parent, mainHandSocket))
            {
                return;
            }

            weaponTransform.SetParent(mainHandSocket, false);
            weaponTransform.localPosition =
                _offHandTwoHandedPresentationLocalPosition;
            weaponTransform.localRotation =
                _offHandTwoHandedPresentationLocalRotation;
            weaponTransform.localScale =
                _offHandTwoHandedPresentationLocalScale;
            LogDiagnostic(
                "Moved the offhand weapon view to the main-hand socket for its two-handed grip.");
        }

        private void RestoreOffHandTwoHandedPresentation()
        {
            CharacterHandBase weapon =
                _offHandTwoHandedPresentationWeapon;
            Transform originalParent =
                _offHandTwoHandedPresentationOriginalParent;
            Vector3 localPosition =
                _offHandTwoHandedPresentationLocalPosition;
            Quaternion localRotation =
                _offHandTwoHandedPresentationLocalRotation;
            Vector3 localScale =
                _offHandTwoHandedPresentationLocalScale;

            _offHandTwoHandedPresentationWeapon = null;
            _offHandTwoHandedPresentationOriginalParent = null;

            if (weapon == null || originalParent == null)
            {
                return;
            }

            Transform weaponTransform = weapon.transform;
            if (weaponTransform == null)
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero != null
                && ReferenceEquals(weapon, hero.OffHandWeapon)
                && hero.OffHand != null)
            {
                originalParent = hero.OffHand;
            }

            weaponTransform.SetParent(originalParent, false);
            weaponTransform.localPosition = localPosition;
            weaponTransform.localRotation = localRotation;
            weaponTransform.localScale = localScale;
            LogDiagnostic(
                "Restored the offhand weapon view to its offhand socket.");
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
            RestoreOffHandTwoHandedPresentation();
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
            _pairedHandVisibilityRecoveryCandidate = null;
            _drawnPairedHandHiddenSince = -1.0f;
            Instance = null;
        }

        private bool GripMemoryEnabled()
        {
            return _rememberGripPerLoadout != null
                && _rememberGripPerLoadout.Value;
        }

        private string GetGripMemoryContextKey(Hero hero)
        {
            int gloriousUiSlot;
            if (TryGetGloriousUiWeaponLoadout(
                out gloriousUiSlot))
            {
                return "G"
                    + gloriousUiSlot.ToString(
                        CultureInfo.InvariantCulture);
            }

            HeroItems heroItems =
                hero == null ? null : hero.TryGetElement<HeroItems>();
            return heroItems == null
                ? null
                : "N"
                    + heroItems.CurrentLoadoutIndex.ToString(
                        CultureInfo.InvariantCulture);
        }

        private bool TryGetGloriousUiWeaponLoadout(out int slot)
        {
            slot = 0;
            if (_gloriousUiReflectionUnavailable
                || !BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(
                    GloriousUiPluginGuid))
            {
                return false;
            }

            object gloriousUi =
                BepInEx.Bootstrap.Chainloader
                    .PluginInfos[GloriousUiPluginGuid]
                    .Instance;
            if (gloriousUi == null)
            {
                return false;
            }

            try
            {
                Type pluginType = gloriousUi.GetType();
                if (_gloriousUiPluginType != pluginType)
                {
                    _gloriousUiPluginType = pluginType;
                    _gloriousUiOwnsLoadoutsMethod =
                        AccessTools.Method(
                            pluginType,
                            "ShouldControlEquipmentWeaponLoadouts");
                    _gloriousUiCurrentLoadoutField =
                        AccessTools.Field(
                            pluginType,
                            "_currentVirtualWeaponSlot");
                }
                if (_gloriousUiOwnsLoadoutsMethod == null
                    || _gloriousUiCurrentLoadoutField == null)
                {
                    _gloriousUiReflectionUnavailable = true;
                    if (!_gloriousUiReflectionWarningLogged)
                    {
                        _gloriousUiReflectionWarningLogged = true;
                        Logger.LogWarning(
                            "Glorious UI is loaded, but its active weapon-loadout identity could not be read. Grip memory will fall back to native loadout indices for this session.");
                    }
                    return false;
                }

                object ownsLoadouts =
                    _gloriousUiOwnsLoadoutsMethod.Invoke(
                        gloriousUi,
                        null);
                if (!(ownsLoadouts is bool)
                    || !(bool)ownsLoadouts)
                {
                    return false;
                }

                object currentSlot =
                    _gloriousUiCurrentLoadoutField.GetValue(
                        gloriousUi);
                if (!(currentSlot is int))
                {
                    return false;
                }
                slot = (int)currentSlot;
                return slot >= 1 && slot <= 6;
            }
            catch (Exception exception)
            {
                if (!_gloriousUiReflectionWarningLogged)
                {
                    _gloriousUiReflectionWarningLogged = true;
                    Logger.LogWarning(
                        "Could not read Glorious UI's active weapon loadout; native loadout grip memory remains available. "
                        + exception.GetBaseException().Message);
                }
                return false;
            }
        }

        private static string GetGripMemoryItemId(Item item)
        {
            if (item == null)
            {
                return String.Empty;
            }
            Model model = item;
            return model.ID ?? String.Empty;
        }

        private static string GetGripOwnerHand(
            Hero hero,
            CharacterHand weapon)
        {
            return hero != null
                && ReferenceEquals(hero.OffHandWeapon, weapon)
                ? "O"
                : "M";
        }

        private bool TryGetRememberedGrip(
            Hero hero,
            CharacterHand weapon,
            Item pairedItem,
            string contextKey,
            out bool twoHandedGrip)
        {
            twoHandedGrip = false;
            if (!GripMemoryEnabled()
                || hero == null
                || weapon == null
                || weapon.Item == null
                || String.IsNullOrEmpty(contextKey))
            {
                return false;
            }

            GripMemoryRecord record;
            if (!_gripMemories.TryGetValue(
                contextKey,
                out record)
                || record == null
                || !String.Equals(
                    record.OwnerHand,
                    GetGripOwnerHand(hero, weapon),
                    StringComparison.Ordinal)
                || !String.Equals(
                    record.WeaponId,
                    GetGripMemoryItemId(weapon.Item),
                    StringComparison.Ordinal)
                || !String.Equals(
                    record.PairedItemId,
                    GetGripMemoryItemId(pairedItem),
                    StringComparison.Ordinal))
            {
                return false;
            }

            twoHandedGrip = record.TwoHandedGrip;
            _pendingGripMemoryInvalidationContextKey = null;
            _pendingGripMemoryInvalidationStableFrames = 0;
            return true;
        }

        private void RememberCurrentGrip(
            Hero hero,
            CharacterHand weapon,
            Item pairedItem)
        {
            if (!GripMemoryEnabled()
                || hero == null
                || weapon == null
                || weapon.Item == null)
            {
                return;
            }

            string contextKey = GetGripMemoryContextKey(hero);
            string weaponId = GetGripMemoryItemId(weapon.Item);
            if (String.IsNullOrEmpty(contextKey)
                || String.IsNullOrEmpty(weaponId))
            {
                return;
            }

            _gripMemories[contextKey] = new GripMemoryRecord
            {
                OwnerHand = GetGripOwnerHand(hero, weapon),
                WeaponId = weaponId,
                PairedItemId = GetGripMemoryItemId(pairedItem),
                TwoHandedGrip = _twoHandedGrip
            };
            _gripMemoryContextKey = contextKey;
            _pendingGripMemoryInvalidationContextKey = null;
            _pendingGripMemoryInvalidationStableFrames = 0;
            SaveGripMemoryState(writeToArchive: false);
            LogDiagnostic(
                "Remembered the manually selected "
                + (_twoHandedGrip
                    ? "two-handed"
                    : "one-handed")
                + " grip for "
                + contextKey
                + " with exact weapon and paired-item validation.");
        }

        private void ObserveGripMemoryWithoutSupportedWeapon(
            Hero hero)
        {
            if (!GripMemoryEnabled())
            {
                _gripMemoryContextKey = null;
                _pendingGripMemoryInvalidationContextKey = null;
                _pendingGripMemoryInvalidationStableFrames = 0;
                return;
            }

            string contextKey = GetGripMemoryContextKey(hero);
            if (!String.Equals(
                _gripMemoryContextKey,
                contextKey,
                StringComparison.Ordinal))
            {
                _gripMemoryContextKey = contextKey;
                _pendingGripMemoryInvalidationContextKey = null;
                _pendingGripMemoryInvalidationStableFrames = 0;
            }
            ScheduleGripMemoryInvalidationIfNeeded(
                hero,
                contextKey);
        }

        private void ScheduleGripMemoryInvalidationIfNeeded(
            Hero hero,
            string contextKey)
        {
            if (!GripMemoryEnabled()
                || hero == null
                || String.IsNullOrEmpty(contextKey))
            {
                _pendingGripMemoryInvalidationContextKey = null;
                _pendingGripMemoryInvalidationStableFrames = 0;
                return;
            }

            GripMemoryRecord record;
            if (!_gripMemories.TryGetValue(
                contextKey,
                out record)
                || record == null
                || GripMemoryRecordMatchesCurrentEquipment(
                    hero,
                    record))
            {
                _pendingGripMemoryInvalidationContextKey = null;
                _pendingGripMemoryInvalidationStableFrames = 0;
                return;
            }
            if (!String.Equals(
                _pendingGripMemoryInvalidationContextKey,
                contextKey,
                StringComparison.Ordinal))
            {
                _pendingGripMemoryInvalidationContextKey = contextKey;
                _pendingGripMemoryInvalidationStableFrames = 0;
            }
        }

        private static bool GripMemoryRecordMatchesCurrentEquipment(
            Hero hero,
            GripMemoryRecord record)
        {
            if (hero == null || record == null)
            {
                return false;
            }

            CharacterHandBase ownerHand = record.OwnerHand == "O"
                ? hero.OffHandWeapon
                : hero.MainHandWeapon;
            Item ownerItem = ownerHand == null
                ? null
                : ownerHand.Item;
            if (!String.Equals(
                record.WeaponId,
                GetGripMemoryItemId(ownerItem),
                StringComparison.Ordinal))
            {
                return false;
            }

            Item pairedItem = GetPairedItem(hero, ownerItem);
            return String.Equals(
                record.PairedItemId,
                GetGripMemoryItemId(pairedItem),
                StringComparison.Ordinal);
        }

        private void ProcessPendingGripMemoryInvalidation(Hero hero)
        {
            string contextKey =
                _pendingGripMemoryInvalidationContextKey;
            if (!GripMemoryEnabled()
                || hero == null
                || String.IsNullOrEmpty(contextKey)
                || Time.timeScale <= 0.0f
                || !String.Equals(
                    contextKey,
                    GetGripMemoryContextKey(hero),
                    StringComparison.Ordinal))
            {
                return;
            }

            GripMemoryRecord record;
            if (!_gripMemories.TryGetValue(contextKey, out record)
                || record == null)
            {
                _pendingGripMemoryInvalidationContextKey = null;
                _pendingGripMemoryInvalidationStableFrames = 0;
                return;
            }
            if (GripMemoryRecordMatchesCurrentEquipment(hero, record))
            {
                _pendingGripMemoryInvalidationContextKey = null;
                _pendingGripMemoryInvalidationStableFrames = 0;
                return;
            }

            CharacterHandBase mainHand = hero.MainHandWeapon;
            CharacterHandBase offHand = hero.OffHandWeapon;
            if (_pairedRefreshStage != PairedRefreshStage.None
                || _equipFsmResetStage != EquipFsmResetStage.None
                || (mainHand != null && mainHand.IsLoadingAnimator)
                || (offHand != null && offHand.IsLoadingAnimator))
            {
                _pendingGripMemoryInvalidationStableFrames = 0;
                return;
            }
            if (++_pendingGripMemoryInvalidationStableFrames < 3)
            {
                return;
            }

            _gripMemories.Remove(contextKey);
            _pendingGripMemoryInvalidationContextKey = null;
            _pendingGripMemoryInvalidationStableFrames = 0;
            SaveGripMemoryState(writeToArchive: false);
            LogDiagnostic(
                "Discarded stale grip memory for "
                + contextKey
                + " because its exact weapon, paired item, or owning hand changed; the current equipment uses its default grip.");
        }

        private void ApplyObservedGripTransition(
            Hero hero,
            CharacterHand weapon,
            Item pairedItem)
        {
            if (hero == null
                || weapon == null
                || weapon.Item == null
                || !hero.IsWeaponEquipped
                || weapon.IsHidden
                || weapon.IsLoadingAnimator
                || Time.timeScale <= 0.0f)
            {
                return;
            }

            StartGripEquipInputGuard(weapon.Item);
            UpdateOffHandTwoHandedPresentation(hero);
            if (IsNativeOneHandedGripWeapon(weapon))
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
                    RefreshWeaponAnimations(hero, weapon, false);
                }
                else if (pairedItem == null
                    || !BeginPairedRefresh(hero, weapon))
                {
                    RefreshWeaponAnimations(hero, weapon, true);
                }
            }
        }

        private bool RequiresRememberedGripAnimationRefresh(
            CharacterHand weapon,
            Item pairedItem)
        {
            if (_selectedGripControllerKnown
                && ReferenceEquals(
                    _selectedGripControllerItem,
                    weapon.Item))
            {
                return _selectedGripControllerTwoHanded
                    != _twoHandedGrip;
            }

            if (IsNativeOneHandedGripWeapon(weapon))
            {
                return _twoHandedGrip;
            }

            return IsConvertedTwoHandedGripWeapon(weapon)
                && !_twoHandedGrip
                && IsSupportedPairedHandItem(pairedItem);
        }

        private void ScheduleRememberedGripAnimationRefresh(
            CharacterHand weapon,
            Item pairedItem,
            string contextKey)
        {
            _rememberedGripAnimationRefreshItem = weapon.Item;
            _rememberedGripAnimationRefreshPairedItem = pairedItem;
            _rememberedGripAnimationRefreshContextKey = contextKey;
            _rememberedGripAnimationRefreshTwoHanded = _twoHandedGrip;
            _rememberedGripAnimationRefreshGeneration =
                _weaponTransitionGeneration;
            LogDiagnostic(
                "Scheduled a settled animator refresh because the remembered non-default grip was restored after the game's initial controller selection. generation="
                + _rememberedGripAnimationRefreshGeneration
                + "; "
                + DescribeGripContext(Hero.Current, weapon));
        }

        private void ClearRememberedGripAnimationRefresh()
        {
            _rememberedGripAnimationRefreshItem = null;
            _rememberedGripAnimationRefreshPairedItem = null;
            _rememberedGripAnimationRefreshContextKey = null;
            _rememberedGripAnimationRefreshTwoHanded = false;
            _rememberedGripAnimationRefreshGeneration = 0;
        }

        private bool ProcessRememberedGripAnimationRefresh(
            Hero hero,
            CharacterHand weapon)
        {
            if (_rememberedGripAnimationRefreshItem == null)
            {
                return false;
            }

            bool exactContextStillActive = hero != null
                && weapon != null
                && ReferenceEquals(
                    weapon.Item,
                    _rememberedGripAnimationRefreshItem)
                && ReferenceEquals(
                    GetPairedItem(hero, weapon.Item),
                    _rememberedGripAnimationRefreshPairedItem)
                && String.Equals(
                    GetGripMemoryContextKey(hero),
                    _rememberedGripAnimationRefreshContextKey,
                    StringComparison.Ordinal)
                && _twoHandedGrip
                    == _rememberedGripAnimationRefreshTwoHanded
                && _weaponTransitionGeneration
                    == _rememberedGripAnimationRefreshGeneration;
            if (!exactContextStillActive)
            {
                ClearRememberedGripAnimationRefresh();
                return false;
            }

            bool rememberedTwoHandedGrip;
            if (!TryGetRememberedGrip(
                    hero,
                    weapon,
                    _rememberedGripAnimationRefreshPairedItem,
                    _rememberedGripAnimationRefreshContextKey,
                    out rememberedTwoHandedGrip)
                || rememberedTwoHandedGrip
                    != _rememberedGripAnimationRefreshTwoHanded)
            {
                ClearRememberedGripAnimationRefresh();
                return false;
            }

            bool waitForPairedHand =
                !_rememberedGripAnimationRefreshTwoHanded;
            if (!hero.IsWeaponEquipped
                || hero.IsPerformingAction
                || weapon.IsHidden
                || Time.timeScale <= 0.0f
                || !HandAnimatorsAreSettled(
                    hero,
                    weapon,
                    waitForPairedHand))
            {
                return true;
            }

            CharacterHandBase pairedHand = FindHandBaseForItem(
                hero,
                _rememberedGripAnimationRefreshPairedItem);
            if (waitForPairedHand
                && !MagicVisualIsReady(hero, pairedHand))
            {
                TryRecoverMissingMagicVisualAfterTransition(
                    hero,
                    pairedHand,
                    _weaponTransitionStartedAt);
                return true;
            }

            Item pairedItem = _rememberedGripAnimationRefreshPairedItem;
            int generation = _rememberedGripAnimationRefreshGeneration;
            ClearRememberedGripAnimationRefresh();
            _weaponTransitionRefreshPending = false;
            LogDiagnostic(
                "Applying the settled animator refresh for the remembered non-default grip. generation="
                + generation
                + "; "
                + DescribeGripContext(hero, weapon));
            ApplyObservedGripTransition(hero, weapon, pairedItem);
            return true;
        }

        private void ObserveGripItem(Item item)
        {
            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, item);
            Item pairedItem = GetPairedItem(hero, item);
            string contextKey = GetGripMemoryContextKey(hero);
            bool sameItem = ReferenceEquals(_gripItem, item);
            bool pairingChanged =
                !ReferenceEquals(_gripPairedItem, pairedItem);
            bool contextChanged = !String.Equals(
                _gripMemoryContextKey,
                contextKey,
                StringComparison.Ordinal);
            if (sameItem && !pairingChanged && !contextChanged)
            {
                return;
            }

            Item previousPairedItem = _gripPairedItem;
            bool previousTwoHandedGrip = _twoHandedGrip;
            CancelGripEquipInputGuard();
            RestoreHiddenPairedHand();
            _gripItem = item;
            _gripPairedItem = pairedItem;
            _gripMemoryContextKey = contextKey;
            RecordWeaponTransition();

            bool rememberedTwoHandedGrip;
            bool restoredMemory = TryGetRememberedGrip(
                hero,
                weapon,
                pairedItem,
                contextKey,
                out rememberedTwoHandedGrip);
            _twoHandedGrip = restoredMemory
                ? rememberedTwoHandedGrip
                : IsConvertedTwoHandedGripWeapon(weapon)
                    && pairedItem == null;

            if (restoredMemory
                && !sameItem
                && RequiresRememberedGripAnimationRefresh(
                    weapon,
                    pairedItem))
            {
                ScheduleRememberedGripAnimationRefresh(
                    weapon,
                    pairedItem,
                    contextKey);
            }

            if (!restoredMemory)
            {
                ScheduleGripMemoryInvalidationIfNeeded(
                    hero,
                    contextKey);
            }

            if (pairingChanged
                && IsConvertedTwoHandedGripWeapon(weapon)
                && ((previousPairedItem != null
                        && previousPairedItem.EquipmentType
                            == EquipmentType.Magic)
                    || (pairedItem != null
                        && pairedItem.EquipmentType
                            == EquipmentType.Magic)))
            {
                _observedAnimationStateKnown = false;
                RequestOneHandedReconciliation();
                LogDiagnostic(
                    "Invalidated the observed grip controller because the opposite-hand transition entered or left a spell pairing. "
                    + DescribeGripContext(hero, weapon));
            }

            if (previousTwoHandedGrip != _twoHandedGrip)
            {
                _observedAnimationStateKnown = false;
                if (sameItem)
                {
                    ApplyObservedGripTransition(hero, weapon, pairedItem);
                }
                else if (!_twoHandedGrip)
                {
                    RequestOneHandedReconciliation();
                }
            }

            _drawnWeaponHiddenSince = -1.0f;
            _drawnPairedHandHiddenSince = -1.0f;
            ResetToggleWeaponHold();
            LogDiagnostic(
                (restoredMemory
                    ? "Observed a supported weapon; restored its exact-equipment loadout grip. "
                    : "Observed a supported weapon; selected its default grip. ")
                + DescribeGripContext(hero, weapon));
        }

        internal bool IsUsingTwoHandedGrip(Item item)
        {
            if (item == null)
            {
                return false;
            }

            Hero hero = Hero.Current;
            CharacterHand weapon = FindHandForItem(hero, item);
            Item pairedItem = GetPairedItem(hero, item);
            string contextKey = GetGripMemoryContextKey(hero);
            if (ReferenceEquals(_gripItem, item)
                && ReferenceEquals(_gripPairedItem, pairedItem)
                && String.Equals(
                    _gripMemoryContextKey,
                    contextKey,
                    StringComparison.Ordinal))
            {
                return _twoHandedGrip;
            }

            bool rememberedTwoHandedGrip;
            if (TryGetRememberedGrip(
                hero,
                weapon,
                pairedItem,
                contextKey,
                out rememberedTwoHandedGrip))
            {
                return rememberedTwoHandedGrip;
            }

            return IsConvertedTwoHandedGripWeapon(weapon)
                && pairedItem == null;
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
                    && IsSupportedPairedHandItem(pairedItem));
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
                        || !IsSupportedPairedHandItem(pairedItem))))
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
            RememberCurrentGrip(hero, weapon, pairedItem);
            StartGripEquipInputGuard(weapon.Item);
            _drawnWeaponHiddenSince = -1.0f;
            _drawnPairedHandHiddenSince = -1.0f;
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
                    if (pairedItem == null
                        || !BeginPairedRefresh(hero, weapon))
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
                            : pairedItem == null
                                ? "Changed the weapon to its native two-handed grip."
                                : "Changed the weapon to its native two-handed grip and stowed the paired hand.")
                        : (IsShield(pairedItem)
                            ? "Changed the two-handed weapon to a one-handed grip and restored its shield."
                            : pairedItem == null
                                ? "Changed the two-handed weapon to a one-handed grip with an empty opposite hand."
                                : "Changed the two-handed weapon to a one-handed grip and restored the paired hand.")));
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
                        FindGripSwitchWeapon(hero)
                        ?? hero.MainHandWeapon as CharacterHand
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

        internal void RecordAnimatorLoad(CharacterHandBase hand)
        {
            MarkHandAnimatorLoading(hand);

            if (hand != null
                && hand.Item != null
                && (ReferenceEquals(hand.Item, _gripItem)
                    || ReferenceEquals(hand.Item, _gripPairedItem)))
            {
                LogDiagnostic(
                    "Animator override reload requested for the "
                    + (ReferenceEquals(hand.Item, _gripItem)
                        ? "grip weapon. "
                        : "paired hand. ")
                    + DescribeGripContext(Hero.Current, FindHandForItem(Hero.Current, _gripItem)));
            }

            CharacterHand weapon = hand as CharacterHand;
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

        internal void RecordAnimatorControllerSelection(
            CharacterHand hand,
            string profile,
            ARAssetReference controller)
        {
            if (hand == null
                || hand.Item == null
                || (!IsNativeOneHandedGripWeapon(hand)
                    && !IsConvertedTwoHandedGripWeapon(hand)))
            {
                return;
            }

            _selectedGripControllerItem = hand.Item;
            _selectedGripControllerTwoHanded =
                IsNativeOneHandedGripWeapon(hand)
                    ? String.Equals(
                        profile,
                        "converted-two-handed",
                        StringComparison.Ordinal)
                    : !String.Equals(
                        profile,
                        "converted-one-handed",
                        StringComparison.Ordinal);
            _selectedGripControllerKnown = true;

            LogDiagnostic(
                "Controller diagnostic: selectedProfile="
                + profile
                + "; selectedGrip="
                + (_selectedGripControllerTwoHanded
                    ? "two-handed"
                    : "one-handed")
                + "; controllerAddress="
                + (controller == null
                    ? "null"
                    : controller.Address)
                + "; controllerIsSet="
                + (controller != null && controller.IsSet)
                + "; "
                + DescribeGripContext(Hero.Current, hand));
        }

        internal void RecordAnimatorLayersApplied(
            CharacterHandBase hand,
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

        internal void RecordMagicVisualLoadStarted(
            CharacterMagic hand,
            bool mainHand)
        {
            if (hand == null)
            {
                return;
            }

            if (mainHand)
            {
                if (!ReferenceEquals(
                    _loadingMainHandMagicVisual,
                    hand))
                {
                    _loadingMainHandMagicVisual = hand;
                    _mainHandMagicVisualLoads = 0;
                }

                _mainHandMagicVisualLoads++;
                _readyMainHandMagicVisualHand = null;
                _readyMainHandMagicVisual = null;
            }
            else
            {
                if (!ReferenceEquals(
                    _loadingOffHandMagicVisual,
                    hand))
                {
                    _loadingOffHandMagicVisual = hand;
                    _offHandMagicVisualLoads = 0;
                }

                _offHandMagicVisualLoads++;
                _readyOffHandMagicVisualHand = null;
                _readyOffHandMagicVisual = null;
            }
        }

        internal void RecordMagicVisualLoadCompleted(
            CharacterMagic hand,
            GameObject glove,
            Item owningItem)
        {
            Hero hero = Hero.Current;
            if (hero == null
                || hand == null
                || owningItem == null
                || !ReferenceEquals(hand.Item, owningItem))
            {
                return;
            }

            if (ReferenceEquals(hero.MainHandWeapon, hand)
                && ReferenceEquals(owningItem, hero.MainHandItem))
            {
                if (!ReferenceEquals(
                    _loadingMainHandMagicVisual,
                    hand))
                {
                    _loadingMainHandMagicVisual = hand;
                    _mainHandMagicVisualLoads = 0;
                }
                else if (_mainHandMagicVisualLoads > 0)
                {
                    _mainHandMagicVisualLoads--;
                }

                if (_mainHandMagicVisualLoads == 0)
                {
                    _readyMainHandMagicVisualHand = hand;
                    _readyMainHandMagicVisual = glove;
                }
            }
            else if (ReferenceEquals(hero.OffHandWeapon, hand)
                && ReferenceEquals(owningItem, hero.OffHandItem))
            {
                if (!ReferenceEquals(
                    _loadingOffHandMagicVisual,
                    hand))
                {
                    _loadingOffHandMagicVisual = hand;
                    _offHandMagicVisualLoads = 0;
                }
                else if (_offHandMagicVisualLoads > 0)
                {
                    _offHandMagicVisualLoads--;
                }

                if (_offHandMagicVisualLoads == 0)
                {
                    _readyOffHandMagicVisualHand = hand;
                    _readyOffHandMagicVisual = glove;
                }
            }
            else
            {
                return;
            }

            if (glove != null
                && !MagicVisualIsLoading(hero, hand))
            {
                LogDiagnostic(
                    "The visible magic gauntlet became ready for the paired spell hand. "
                    + DescribeGripContext(
                        hero,
                        FindHandForItem(hero, _gripItem)));
            }
        }

        internal void RecordWeaponTransition()
        {
            ClearRememberedGripAnimationRefresh();
            if (_pairedRefreshStage != PairedRefreshStage.None)
            {
                LogDiagnostic(
                    "Canceled the ordered grip restoration because a newer equipment transition took ownership of both hands. "
                    + DescribeGripContext(
                        Hero.Current,
                        _pairedRefreshWeapon));
                _pairedHandVisibilityRecoveryCandidate =
                    _pairedRefreshPairedHand != null
                    && _pairedRefreshPairedHand.IsHidden
                        ? _pairedRefreshPairedHand
                        : null;
                _drawnPairedHandHiddenSince = -1.0f;
                CancelPairedRefresh();
                _observedAnimationStateKnown = false;
                RequestOneHandedReconciliation();
            }

            if (_equipFsmResetStage
                != EquipFsmResetStage.None)
            {
                LogDiagnostic(
                    "Canceled the settled equip-FSM reset because a newer equipment transition took ownership. generation="
                    + _equipFsmResetGeneration
                    + "; "
                    + DescribeGripContext(
                        Hero.Current,
                        _equipFsmResetWeapon));
                CancelEquipFsmReset();
                _observedAnimationStateKnown = false;
                RequestOneHandedReconciliation();
            }

            _weaponTransitionGeneration++;
            if (_diagnostics != null && _diagnostics.Value)
            {
                _audioDiagnosticTransitionStartedAt = Time.unscaledTime;
                _audioDiagnosticTransitionUntil = Time.unscaledTime
                    + AudioDiagnosticTransitionWindowSeconds;
            }
            _weaponTransitionRefreshPending = true;
            _weaponTransitionRefreshFramesRemaining =
                WeaponTransitionRefreshWindowFrames;
            _weaponTransitionStartedAt = Time.unscaledTime;
            _magicVisualRecoveryHand = null;
            LogDiagnostic(
                "Recorded equipment transition ownership. generation="
                + _weaponTransitionGeneration
                + "; "
                + DescribeGripContext(
                    Hero.Current,
                    FindGripSwitchWeapon(Hero.Current)));
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

        private bool TryFinalizeNativeOneHandedAfterWeaponTransition(
            Hero hero,
            CharacterHand weapon)
        {
            if (!_weaponTransitionRefreshPending
                || hero == null
                || weapon == null
                || weapon.Item == null
                || !IsNativeOneHandedGripWeapon(weapon)
                || !hero.IsWeaponEquipped
                || hero.IsPerformingAction
                || weapon.IsHidden
                || _pairedRefreshStage != PairedRefreshStage.None
                || !HandAnimatorsAreSettled(
                    hero,
                    weapon,
                    true))
            {
                return false;
            }

            CharacterHandBase pairedHand = FindHandBaseForItem(
                hero,
                GetPairedItem(hero, weapon.Item));
            if (!MagicVisualIsReady(hero, pairedHand))
            {
                TryRecoverMissingMagicVisualAfterTransition(
                    hero,
                    pairedHand,
                    _weaponTransitionStartedAt);
                return true;
            }

            BeginEquipFsmReset(
                hero,
                weapon,
                GetPairedItem(hero, weapon.Item));
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
            return IsSupportedPairedHandItem(pairedItem)
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
                && IsSupportedPairedHandItem(pairedItem);
        }

        internal static bool ShouldSuppressDualWielding()
        {
            Hero hero = Hero.Current;
            CharacterHand gripWeapon = FindGripSwitchWeapon(hero);
            if (GetGripCombatMode(hero, gripWeapon)
                == GripCombatMode.TwoHanded)
            {
                return true;
            }

            if (HasSupportedConvertedPairing())
            {
                CharacterHand convertedWeapon =
                    FindConvertedTwoHandedGripWeapon(hero);
                return GetGripCombatMode(hero, convertedWeapon)
                    != GripCombatMode.DualWielding;
            }

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
            return IsSupportedPairedHandItem(pairedItem)
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
            CharacterHand nativeOneHandedWeapon =
                FindNativeOneHandedGripWeapon(hero);
            CharacterHand gripWeapon = FindGripSwitchWeapon(hero);
            bool convertedGripActive =
                ShouldUseOneHandedAnimations(convertedWeapon)
                || (convertedWeapon != null
                    && convertedWeapon.Item != null
                    && Instance != null
                    && Instance.IsUsingTwoHandedGrip(
                        convertedWeapon.Item));
            bool nativeTwoHandedGripActive =
                ShouldUseTwoHandedAnimations(nativeOneHandedWeapon)
                || (gripWeapon != null
                    && gripWeapon.Item != null
                    && Instance != null
                    && Instance.IsUsingTwoHandedGrip(gripWeapon.Item));
            if (!convertedGripActive && !nativeTwoHandedGripActive)
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
            MagicMeleeOffHandFSM offHandMelee =
                hero.TryGetElement<MagicMeleeOffHandFSM>();
            GripCombatMode combatMode = GetGripCombatMode(
                hero,
                gripWeapon);
            bool usesOneHandedFsm =
                combatMode == GripCombatMode.OneHanded
                || combatMode
                    == GripCombatMode.OneHandedWithOffHandMelee;
            bool usesOffHandMeleeFsm =
                combatMode == GripCombatMode.OffHandMelee
                || combatMode
                    == GripCombatMode.OneHandedWithOffHandMelee;

            if (!usesOneHandedFsm
                && oneHanded != null
                && oneHanded.IsLayerActive)
            {
                oneHanded.DisableFSM();
            }
            if (combatMode != GripCombatMode.TwoHanded
                && twoHanded != null
                && twoHanded.IsLayerActive)
            {
                twoHanded.DisableFSM();
            }
            if (combatMode != GripCombatMode.DualWielding
                && dualHanded != null
                && dualHanded.IsLayerActive)
            {
                dualHanded.DisableFSM();
            }
            if (!usesOffHandMeleeFsm
                && offHandMelee != null
                && offHandMelee.IsLayerActive)
            {
                offHandMelee.DisableFSM();
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
                        if (magicFsm.IsLayerActive)
                        {
                            magicFsm.DisableFSM();
                        }
                    }
                }

                if (suppressOffHand)
                {
                    MagicMeleeOffHandFSM magicMeleeOffHand =
                        hero.TryGetElement<MagicMeleeOffHandFSM>();
                    if (magicMeleeOffHand != null
                        && magicMeleeOffHand.IsLayerActive)
                    {
                        magicMeleeOffHand.DisableFSM();
                    }
                }
            }
        }

        private static void ReconcileGripFsmState(
            Hero hero,
            GripCombatMode combatMode)
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
            MagicMeleeOffHandFSM offHandMelee =
                hero.TryGetElement<MagicMeleeOffHandFSM>();
            bool usesOneHandedFsm =
                combatMode == GripCombatMode.OneHanded
                || combatMode
                    == GripCombatMode.OneHandedWithOffHandMelee;
            bool usesOffHandMeleeFsm =
                combatMode == GripCombatMode.OffHandMelee
                || combatMode
                    == GripCombatMode.OneHandedWithOffHandMelee;

            if (oneHanded != null)
            {
                if (usesOneHandedFsm
                    && !oneHanded.IsLayerActive)
                {
                    oneHanded.EnableFSM();
                }
                else if (!usesOneHandedFsm
                    && oneHanded.IsLayerActive)
                {
                    oneHanded.DisableFSM();
                }
            }
            if (twoHanded != null)
            {
                if (combatMode == GripCombatMode.TwoHanded
                    && !twoHanded.IsLayerActive)
                {
                    twoHanded.EnableFSM();
                }
                else if (combatMode != GripCombatMode.TwoHanded
                    && twoHanded.IsLayerActive)
                {
                    twoHanded.DisableFSM();
                }
            }
            if (dualHanded != null)
            {
                if (combatMode == GripCombatMode.DualWielding
                    && !dualHanded.IsLayerActive)
                {
                    dualHanded.EnableFSM();
                }
                else if (combatMode != GripCombatMode.DualWielding
                    && dualHanded.IsLayerActive)
                {
                    dualHanded.DisableFSM();
                }
            }
            if (offHandMelee != null)
            {
                if (usesOffHandMeleeFsm
                    && !offHandMelee.IsLayerActive)
                {
                    offHandMelee.EnableFSM();
                }
                else if (!usesOffHandMeleeFsm
                    && offHandMelee.IsLayerActive)
                {
                    offHandMelee.DisableFSM();
                }
            }
        }

        private static bool GripFsmMatches(
            Hero hero,
            GripCombatMode combatMode)
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
            MagicMeleeOffHandFSM offHandMelee =
                hero.TryGetElement<MagicMeleeOffHandFSM>();
            bool offHandMeleeActive = offHandMelee != null
                && offHandMelee.IsLayerActive;
            if (combatMode
                == GripCombatMode.OneHandedWithOffHandMelee)
            {
                return oneHandedActive
                    && offHandMeleeActive
                    && !twoHandedActive
                    && !dualHandedActive;
            }

            return combatMode == GripCombatMode.OneHanded
                ? oneHandedActive
                    && !twoHandedActive
                    && !dualHandedActive
                    && !offHandMeleeActive
                : combatMode == GripCombatMode.TwoHanded
                    ? twoHandedActive
                        && !oneHandedActive
                        && !dualHandedActive
                        && !offHandMeleeActive
                    : combatMode == GripCombatMode.DualWielding
                        ? dualHandedActive
                            && !oneHandedActive
                            && !twoHandedActive
                            && !offHandMeleeActive
                        : combatMode == GripCombatMode.OffHandMelee
                            && offHandMeleeActive
                            && !oneHandedActive
                            && !twoHandedActive
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

        internal static string[] GetOneHandedLayers(CharacterHand weapon)
        {
            Hero hero = Hero.Current;
            if (hero != null
                && weapon != null
                && ReferenceEquals(weapon.Item, hero.OffHandItem)
                && !UsesNativeDualWieldingMode(hero))
            {
                return OffHandMeleeLayers;
            }

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
            if (hero == null)
            {
                return null;
            }

            CharacterHand mainHand = hero.MainHandWeapon as CharacterHand;
            if (IsConvertedTwoHandedGripWeapon(mainHand))
            {
                Item pairedItem = GetPairedItem(hero, mainHand.Item);
                if (IsSupportedPairedHandItem(pairedItem))
                {
                    return mainHand;
                }
            }

            if (IsNativeOneHandedGripWeapon(mainHand))
            {
                return mainHand;
            }

            CharacterHand offHand = hero.OffHandWeapon as CharacterHand;
            if (IsConvertedTwoHandedGripWeapon(offHand))
            {
                Item pairedItem = GetPairedItem(hero, offHand.Item);
                if (IsSupportedPairedHandItem(pairedItem))
                {
                    return offHand;
                }
            }

            return null;
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

        private void MonitorCanceledPairedHandVisibility(Hero hero)
        {
            CharacterHandBase pairedHand =
                _pairedHandVisibilityRecoveryCandidate;
            if (pairedHand == null)
            {
                _drawnPairedHandHiddenSince = -1.0f;
                return;
            }

            if (hero == null)
            {
                _pairedHandVisibilityRecoveryCandidate = null;
                _drawnPairedHandHiddenSince = -1.0f;
                return;
            }

            bool pairedHandStillCurrent = ReferenceEquals(
                    hero.MainHandWeapon,
                    pairedHand)
                || ReferenceEquals(
                    hero.OffHandWeapon,
                    pairedHand);
            if (!pairedHandStillCurrent)
            {
                _drawnPairedHandHiddenSince = -1.0f;
                if (Time.timeScale > 0.0f
                    && !_weaponTransitionRefreshPending
                    && !HeroWeaponEvents.Current.IsLoadingAnimations())
                {
                    _pairedHandVisibilityRecoveryCandidate = null;
                }
                return;
            }

            if (_twoHandedGrip
                || Time.timeScale <= 0.0f
                || !hero.IsWeaponEquipped
                || !pairedHand.IsHidden
                || pairedHand.IsLoadingAnimator
                || HeroWeaponEvents.Current.IsLoadingAnimations()
                || MagicVisualIsLoading(
                    hero,
                    pairedHand as CharacterMagic)
                || hero.IsPerformingAction
                || _pairedRefreshStage != PairedRefreshStage.None)
            {
                _drawnPairedHandHiddenSince = -1.0f;
                if (pairedHand != null && !pairedHand.IsHidden)
                {
                    _pairedHandVisibilityRecoveryCandidate = null;
                }
                return;
            }

            if (_drawnPairedHandHiddenSince < 0.0f)
            {
                _drawnPairedHandHiddenSince = Time.unscaledTime;
                LogDiagnostic(
                    "Started paired-hand visibility recovery watch. "
                    + DescribeGripContext(
                        hero,
                        FindGripSwitchWeapon(hero)));
                return;
            }

            if (Time.unscaledTime - _drawnPairedHandHiddenSince
                < HiddenDrawnWeaponRecoverySeconds)
            {
                return;
            }

            Logger.LogWarning(
                "Recovered a paired hand that remained hidden after the hero finished drawing it. "
                + DescribeGripContext(
                    hero,
                    FindGripSwitchWeapon(hero)));
            ShowDiagnosticNotification(
                "paired-hand-visibility-recovery",
                "VW recovered: hidden paired hand; check the BepInEx log.",
                "High",
                "vw-recovery");
            _drawnPairedHandHiddenSince = -1.0f;
            _pairedHandVisibilityRecoveryCandidate = null;
            SetPairedHandHiddenPreservingDrawnState(
                pairedHand,
                false);
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
                : (IsShield(pairedItem)
                    ? "shield"
                    : (pairedItem.IsMagic
                        ? "spell"
                        : (pairedItem.IsMelee
                            ? "melee"
                            : "other")))));
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
            else if (hero != null
                && hero.HeroRPGStats != null
                && requirements != null
                && requirements.StrengthRequired != null)
            {
                float zeroRequirementFullPotencyStrength =
                    GetZeroRequirementFullPotencyStrength();
                if (zeroRequirementFullPotencyStrength > 0.0f)
                {
                    actualStrengthRatio = (
                        hero.HeroRPGStats.Strength.ModifiedValue
                        / zeroRequirementFullPotencyStrength)
                            .ToString("0.00", CultureInfo.InvariantCulture)
                        + "x-zero-floor";
                    effectiveStrengthRatio = strengthTestMode
                        == StrengthTestMode.WeaponRequirement
                            ? "0.00x-zero-floor"
                            : strengthTestMode
                                == StrengthTestMode.FullPotency
                                    ? "1.00x-zero-floor"
                                    : actualStrengthRatio;
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
                + "; equipFsmResetStage="
                + _equipFsmResetStage
                + "; transitionGeneration="
                + _weaponTransitionGeneration
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

        private static bool IsSupportedPairedHandItem(Item item)
        {
            if (item == null || IsEmptyHandPlaceholder(item))
            {
                return true;
            }

            EquipmentType equipmentType = item.EquipmentType;
            return equipmentType == EquipmentType.Fists
                || equipmentType == EquipmentType.OneHanded
                || equipmentType == EquipmentType.Shield
                || equipmentType == EquipmentType.Magic
                || equipmentType == EquipmentType.Rod
                || (item.Template != null
                    && item.Template.IsTwoHanded
                    && IsSupportedWeaponFamily(item.Template));
        }

        private static bool UsesNativeDualWieldingMode(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            Item mainHandItem = hero.MainHandItem;
            Item offHandItem = hero.OffHandItem;
            EquipmentType mainType = mainHandItem == null
                ? null
                : mainHandItem.EquipmentType;
            EquipmentType offType = offHandItem == null
                || IsEmptyHandPlaceholder(offHandItem)
                    ? null
                    : offHandItem.EquipmentType;
            bool mainHandForcesDual = mainType == EquipmentType.Shield
                || mainType == EquipmentType.Rod;
            bool offHandIsPassive = offType == EquipmentType.Fists
                || offType == EquipmentType.Magic;
            return (mainHandForcesDual
                    && offType != null
                    && !offHandIsPassive)
                || (offType == EquipmentType.OneHanded
                    && mainType != EquipmentType.Magic);
        }

        private static bool UsesParallelOffHandMeleeMode(
            Hero hero,
            CharacterHand gripWeapon)
        {
            if (hero == null
                || gripWeapon == null
                || gripWeapon.Item == null
                || !ReferenceEquals(
                    gripWeapon.Item,
                    hero.MainHandItem)
                || UsesNativeDualWieldingMode(hero))
            {
                return false;
            }

            Item offHandItem = hero.OffHandItem;
            CharacterHand offHandWeapon =
                hero.OffHandWeapon as CharacterHand;
            return offHandItem != null
                && offHandWeapon != null
                && ReferenceEquals(offHandWeapon.Item, offHandItem)
                && !IsEmptyHandPlaceholder(offHandItem)
                && !IsShield(offHandItem)
                && IsSupportedPairedHandItem(offHandItem);
        }

        private static GripCombatMode GetGripCombatMode(
            Hero hero,
            CharacterHand weapon)
        {
            if (hero == null || weapon == null || weapon.Item == null)
            {
                return GripCombatMode.None;
            }

            if (Instance != null
                && Instance.IsUsingTwoHandedGrip(weapon.Item))
            {
                return GripCombatMode.TwoHanded;
            }

            if (UsesNativeDualWieldingMode(hero))
            {
                return GripCombatMode.DualWielding;
            }

            if (UsesParallelOffHandMeleeMode(hero, weapon))
            {
                return GripCombatMode.OneHandedWithOffHandMelee;
            }

            if (ReferenceEquals(weapon.Item, hero.OffHandItem)
                && hero.MainHandItem != null
                && hero.MainHandItem.EquipmentType == EquipmentType.Magic)
            {
                return GripCombatMode.OffHandMelee;
            }

            return GripCombatMode.OneHanded;
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
            bool waitForPairedHand)
        {
            if (!HandAnimatorsAreSettled(
                    hero,
                    weapon,
                    waitForPairedHand))
            {
                return false;
            }

            Item pairedItem = GetPairedItem(hero, weapon.Item);
            CharacterHandBase pairedHand =
                FindHandBaseForItem(hero, pairedItem);
            return !waitForPairedHand
                || pairedItem == null
                || MagicVisualIsReady(hero, pairedHand);
        }

        private bool HandAnimatorsAreSettled(
            Hero hero,
            CharacterHand weapon,
            bool waitForPairedHand)
        {
            if (weapon == null
                || weapon.IsLoadingAnimator
                || HeroWeaponEvents.Current.IsLoadingAnimations())
            {
                return false;
            }

            Item pairedItem = GetPairedItem(hero, weapon.Item);
            CharacterHandBase pairedHand =
                FindHandBaseForItem(hero, pairedItem);
            if (!waitForPairedHand || pairedItem == null)
            {
                return AnimatorLayersAreReady(hero, weapon);
            }

            if (pairedHand == null)
            {
                return false;
            }

            return ReferenceEquals(pairedHand.Item, pairedItem)
                && !pairedHand.IsHidden
                && !pairedHand.IsLoadingAnimator
                && AnimatorLayersAreReady(hero, weapon)
                && AnimatorLayersAreReady(hero, pairedHand);
        }

        private bool MagicVisualIsReady(
            Hero hero,
            CharacterHandBase hand)
        {
            CharacterMagic magicHand = hand as CharacterMagic;
            if (hero == null || magicHand == null || magicHand.Item == null)
            {
                return true;
            }

            if (ReferenceEquals(hero.MainHandWeapon, magicHand)
                && ReferenceEquals(magicHand.Item, hero.MainHandItem))
            {
                ARAssetReference glove = magicHand.mainHandMagicGlove;
                return glove == null
                    || !glove.IsSet
                    || (_mainHandMagicVisualLoads == 0
                        && ReferenceEquals(
                            _readyMainHandMagicVisualHand,
                            magicHand)
                        && _readyMainHandMagicVisual != null
                        && _readyMainHandMagicVisual.activeInHierarchy);
            }

            if (ReferenceEquals(hero.OffHandWeapon, magicHand)
                && ReferenceEquals(magicHand.Item, hero.OffHandItem))
            {
                ARAssetReference glove = magicHand.offHandMagicGlove;
                return glove == null
                    || !glove.IsSet
                    || (_offHandMagicVisualLoads == 0
                        && ReferenceEquals(
                            _readyOffHandMagicVisualHand,
                            magicHand)
                        && _readyOffHandMagicVisual != null
                        && _readyOffHandMagicVisual.activeInHierarchy);
            }

            return false;
        }

        private bool TryRecoverMissingMagicVisualAfterTransition(
            Hero hero,
            CharacterHandBase hand,
            float recoveryStartedAt)
        {
            CharacterMagic magicHand = hand as CharacterMagic;
            if (hero == null
                || magicHand == null
                || magicHand.Item == null
                || Time.timeScale <= 0.0f
                || ReferenceEquals(_magicVisualRecoveryHand, magicHand)
                || MagicVisualIsLoading(hero, magicHand)
                || Time.unscaledTime - recoveryStartedAt
                    < MagicVisualRecoveryDelaySeconds)
            {
                return false;
            }

            bool mainHand = ReferenceEquals(
                hero.MainHandWeapon,
                magicHand);
            if ((!mainHand
                    && !ReferenceEquals(
                        hero.OffHandWeapon,
                        magicHand))
                || !ReferenceEquals(
                    magicHand.Item,
                    mainHand
                        ? hero.MainHandItem
                        : hero.OffHandItem))
            {
                return false;
            }

            _magicVisualRecoveryHand = magicHand;
            try
            {
                if (EquipMagicGloveToHeroMethod == null)
                {
                    throw new MissingMethodException(
                        typeof(CharacterMagic).FullName,
                        "EquipMagicGloveToHero");
                }

                EquipMagicGloveToHeroMethod.Invoke(
                    magicHand,
                    new object[] { hero, mainHand });
                Logger.LogWarning(
                    "Requested a visual-only spell-hand recovery after its animator settled without a magic gauntlet. Loaded animation controllers were left untouched. "
                    + DescribeGripContext(
                        hero,
                        FindHandForItem(hero, _gripItem)));
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Failed to request visual-only spell-hand recovery: "
                    + exception);
                return false;
            }
        }

        private bool MagicVisualIsLoading(
            Hero hero,
            CharacterMagic hand)
        {
            if (hero == null || hand == null || hand.Item == null)
            {
                return false;
            }

            return ReferenceEquals(hero.MainHandWeapon, hand)
                && ReferenceEquals(hand.Item, hero.MainHandItem)
                ? ReferenceEquals(
                        _loadingMainHandMagicVisual,
                        hand)
                    && _mainHandMagicVisualLoads > 0
                : ReferenceEquals(hero.OffHandWeapon, hand)
                    && ReferenceEquals(hand.Item, hero.OffHandItem)
                    && ReferenceEquals(
                        _loadingOffHandMagicVisual,
                        hand)
                    && _offHandMagicVisualLoads > 0;
        }

        private bool AnimatorLayersAreReady(
            Hero hero,
            CharacterHandBase hand)
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

        private void MarkHandAnimatorLoading(CharacterHandBase hand)
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

        private static void DisableActiveGripFsms(Hero hero)
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
            MagicMeleeOffHandFSM offHandMelee =
                hero.TryGetElement<MagicMeleeOffHandFSM>();

            if (oneHanded != null && oneHanded.IsLayerActive)
            {
                oneHanded.DisableFSM();
            }
            if (twoHanded != null && twoHanded.IsLayerActive)
            {
                twoHanded.DisableFSM();
            }
            if (dualHanded != null && dualHanded.IsLayerActive)
            {
                dualHanded.DisableFSM();
            }
            if (offHandMelee != null && offHandMelee.IsLayerActive)
            {
                offHandMelee.DisableFSM();
            }
        }

        private static bool IsStableGripFsmState(
            HeroAnimatorSubstateMachine fsm)
        {
            if (fsm == null)
            {
                return false;
            }

            HeroStateType currentState = fsm.CurrentStateType;
            return currentState == HeroStateType.Idle
                || currentState == HeroStateType.IdleAlternate
                || currentState == HeroStateType.Movement
                || currentState == HeroStateType.MovementAlternate;
        }

        private static void AddEquipFsm(
            List<HeroAnimatorSubstateMachine> fsms,
            HeroAnimatorSubstateMachine fsm)
        {
            if (fsm != null && !fsms.Contains(fsm))
            {
                fsms.Add(fsm);
            }
        }

        private static List<HeroAnimatorSubstateMachine> GetEquipFsms(
            Hero hero,
            GripCombatMode combatMode,
            Item pairedItem)
        {
            List<HeroAnimatorSubstateMachine> fsms =
                new List<HeroAnimatorSubstateMachine>();
            if (hero == null)
            {
                return fsms;
            }

            if (combatMode == GripCombatMode.OneHanded
                || combatMode
                    == GripCombatMode.OneHandedWithOffHandMelee)
            {
                AddEquipFsm(fsms, hero.TryGetElement<OneHandedFSM>());
            }
            if (combatMode == GripCombatMode.TwoHanded)
            {
                AddEquipFsm(fsms, hero.TryGetElement<TwoHandedFSM>());
            }
            if (combatMode == GripCombatMode.DualWielding)
            {
                AddEquipFsm(fsms, hero.TryGetElement<DualHandedFSM>());
            }
            if (combatMode == GripCombatMode.OffHandMelee
                || combatMode
                    == GripCombatMode.OneHandedWithOffHandMelee)
            {
                AddEquipFsm(
                    fsms,
                    hero.TryGetElement<MagicMeleeOffHandFSM>());
            }

            if (pairedItem != null
                && pairedItem.EquipmentType == EquipmentType.Magic)
            {
                if (ReferenceEquals(pairedItem, hero.MainHandItem))
                {
                    AddEquipFsm(
                        fsms,
                        hero.TryGetElement<MagicMainHandFSM>());
                }
                else if (ReferenceEquals(
                    pairedItem,
                    hero.OffHandItem))
                {
                    AddEquipFsm(
                        fsms,
                        hero.TryGetElement<MagicOffHandFSM>());
                }
            }

            return fsms;
        }

        private static bool EquipFsmsAreStable(
            List<HeroAnimatorSubstateMachine> fsms)
        {
            if (fsms == null || fsms.Count == 0)
            {
                return false;
            }

            foreach (HeroAnimatorSubstateMachine fsm in fsms)
            {
                if (fsm == null
                    || !fsm.IsLayerActive
                    || !IsStableGripFsmState(fsm))
                {
                    return false;
                }
            }

            return true;
        }

        private static string DescribeEquipFsms(
            List<HeroAnimatorSubstateMachine> fsms)
        {
            if (fsms == null || fsms.Count == 0)
            {
                return "none";
            }

            List<string> states = new List<string>();
            foreach (HeroAnimatorSubstateMachine fsm in fsms)
            {
                states.Add(
                    fsm.GetType().Name
                    + "{active="
                    + fsm.IsLayerActive
                    + ",current="
                    + fsm.CurrentStateType
                    + ",target="
                    + fsm.CurrentStateToEnterType
                    + "}");
            }

            return string.Join(",", states.ToArray());
        }

        private static void EnableEquipFsms(
            Hero hero,
            GripCombatMode combatMode,
            Item pairedItem)
        {
            ReconcileGripFsmState(hero, combatMode);
            List<HeroAnimatorSubstateMachine> fsms =
                GetEquipFsms(hero, combatMode, pairedItem);
            foreach (HeroAnimatorSubstateMachine fsm in fsms)
            {
                if (!fsm.IsLayerActive)
                {
                    fsm.EnableFSM();
                }
            }
        }

        private bool BeginEquipFsmReset(
            Hero hero,
            CharacterHand weapon,
            Item pairedItem)
        {
            if (hero == null
                || weapon == null
                || weapon.Item == null
                || !ReferenceEquals(
                    GetPairedItem(hero, weapon.Item),
                    pairedItem))
            {
                return false;
            }

            GripCombatMode combatMode = GetGripCombatMode(hero, weapon);
            List<HeroAnimatorSubstateMachine> equipFsms =
                GetEquipFsms(hero, combatMode, pairedItem);
            bool expectsMagicFsm = pairedItem != null
                && pairedItem.EquipmentType == EquipmentType.Magic;
            bool hasMagicFsm = equipFsms.Exists(
                fsm => fsm is MagicMainHandFSM
                    || fsm is MagicOffHandFSM);
            if (combatMode == GripCombatMode.None
                || equipFsms.Count == 0
                || (expectsMagicFsm && !hasMagicFsm))
            {
                return false;
            }

            string beforeStates = DescribeEquipFsms(equipFsms);
            DisableActiveGripFsms(hero);
            foreach (HeroAnimatorSubstateMachine fsm in equipFsms)
            {
                if (fsm.IsLayerActive)
                {
                    fsm.DisableFSM();
                }
            }

            _weaponTransitionRefreshPending = false;
            _oneHandedReconciliationPending = false;
            _observedAnimationStateKnown = false;
            _equipFsmResetWeapon = weapon;
            _equipFsmResetPairedItem = pairedItem;
            _equipFsmResetMode = combatMode;
            _equipFsmResetWaitFrames = 0;
            _equipFsmResetStartedAt = Time.unscaledTime;
            _equipFsmResetGeneration = _weaponTransitionGeneration;
            _equipFsmResetStage =
                EquipFsmResetStage.WaitingOneFrame;

            Logger.LogInfo(
                "Exact current hands settled; restarting their gameplay FSMs together without hiding or reloading either hand or animator controller. generation="
                + _equipFsmResetGeneration
                + "; fsmsBefore="
                + beforeStates
                + "; "
                + DescribeGripContext(hero, weapon));
            return true;
        }

        private bool ProcessEquipFsmReset(
            Hero hero,
            CharacterHand currentWeapon)
        {
            if (_equipFsmResetStage
                == EquipFsmResetStage.None)
            {
                return false;
            }

            if (Time.timeScale <= 0.0f)
            {
                _equipFsmResetStartedAt = Time.unscaledTime;
                return true;
            }

            if (Time.unscaledTime - _equipFsmResetStartedAt
                >= PairedRefreshTimeoutSeconds)
            {
                CharacterHand timedOutWeapon =
                    _equipFsmResetWeapon;
                Logger.LogWarning(
                    "Settled equip-FSM reset timed out; restoring the expected combat FSMs without touching either hand or animator controller. generation="
                    + _equipFsmResetGeneration
                    + "; "
                    + DescribeGripContext(hero, timedOutWeapon));
                if (hero != null)
                {
                    EnableEquipFsms(
                        hero,
                        _equipFsmResetMode,
                        _equipFsmResetPairedItem);
                }

                CancelEquipFsmReset();
                _observedAnimationStateKnown = false;
                RequestOneHandedReconciliation();
                return false;
            }

            if (hero == null
                || _equipFsmResetGeneration
                    != _weaponTransitionGeneration
                || !ReferenceEquals(
                    currentWeapon,
                    _equipFsmResetWeapon)
                || _equipFsmResetWeapon == null
                || _equipFsmResetWeapon.Item == null
                || !ReferenceEquals(
                    GetPairedItem(
                        hero,
                        _equipFsmResetWeapon.Item),
                    _equipFsmResetPairedItem)
                || GetGripCombatMode(
                    hero,
                    _equipFsmResetWeapon)
                    != _equipFsmResetMode)
            {
                CancelEquipFsmReset();
                _observedAnimationStateKnown = false;
                return false;
            }

            if (_equipFsmResetStage
                == EquipFsmResetStage.WaitingOneFrame)
            {
                if (_equipFsmResetWaitFrames++ == 0)
                {
                    return true;
                }

                EnableEquipFsms(
                    hero,
                    _equipFsmResetMode,
                    _equipFsmResetPairedItem);
                _equipFsmResetStage =
                    EquipFsmResetStage.WaitingForStableFsms;
                List<HeroAnimatorSubstateMachine> requestedFsms =
                    GetEquipFsms(
                        hero,
                        _equipFsmResetMode,
                        _equipFsmResetPairedItem);
                LogDiagnostic(
                    "Settled equip-FSM diagnostic: requested the exact current hands' gameplay FSMs together; waiting for stable idle or movement states. generation="
                    + _equipFsmResetGeneration
                    + "; fsms="
                    + DescribeEquipFsms(requestedFsms)
                    + "; "
                    + DescribeGripContext(
                        hero,
                        _equipFsmResetWeapon));
                return true;
            }

            List<HeroAnimatorSubstateMachine> equipFsms =
                GetEquipFsms(
                    hero,
                    _equipFsmResetMode,
                    _equipFsmResetPairedItem);
            if (!GripFsmMatches(hero, _equipFsmResetMode)
                || !EquipFsmsAreStable(equipFsms))
            {
                return true;
            }

            _observedAnimationState =
                ShouldUseOneHandedAnimations(_equipFsmResetWeapon);
            _observedAnimationStateKnown = true;
            _oneHandedReconciliationPending = false;
            Logger.LogInfo(
                "Completed the settled equip-FSM reset without reloading either hand or animator controller. generation="
                + _equipFsmResetGeneration
                + "; fsms="
                + DescribeEquipFsms(equipFsms)
                + "; "
                + DescribeGripContext(
                    hero,
                    _equipFsmResetWeapon));
            CancelEquipFsmReset();
            return true;
        }

        private void CancelEquipFsmReset()
        {
            _equipFsmResetStage = EquipFsmResetStage.None;
            _equipFsmResetWeapon = null;
            _equipFsmResetPairedItem = null;
            _equipFsmResetMode = GripCombatMode.None;
            _equipFsmResetWaitFrames = 0;
            _equipFsmResetStartedAt = 0.0f;
            _equipFsmResetGeneration = 0;
        }

        private bool BeginPairedRefresh(
            Hero hero,
            CharacterHand weapon)
        {
            Item pairedItem = GetPairedItem(hero, weapon.Item);
            if (pairedItem == null
                || !IsSupportedPairedHandItem(pairedItem))
            {
                _weaponTransitionRefreshPending = false;
                return false;
            }

            CharacterHandBase pairedHand = FindHandBaseForItem(
                hero,
                pairedItem);
            if (pairedHand == null
                && _hiddenPairedHand != null
                && ReferenceEquals(_hiddenPairedHand.Item, pairedItem))
            {
                pairedHand = _hiddenPairedHand;
            }
            if (pairedHand == null)
            {
                return false;
            }

            DisableActiveGripFsms(hero);

            _weaponTransitionRefreshPending = false;
            _oneHandedReconciliationPending = false;
            _observedAnimationStateKnown = false;
            _pairedRefreshWeapon = weapon;
            _pairedRefreshPairedHand = pairedHand;
            _pairedRefreshWaitFrames = 0;
            _pairedRefreshStartedAt = Time.unscaledTime;
            _hiddenPairedHand = null;
            _pairedHandVisibilityRecoveryCandidate = null;
            _drawnPairedHandHiddenSince = -1.0f;
            _magicVisualRecoveryHand = null;

            MarkHandAnimatorLoading(pairedHand);
            MarkHandAnimatorLoading(weapon);
            if (!pairedHand.IsHidden)
            {
                SetPairedHandHiddenPreservingDrawnState(
                    pairedHand,
                    true);
            }
            weapon.HideWeapon(true);
            _pairedRefreshStage = PairedRefreshStage.Hidden;

            Logger.LogInfo(
                "Restarting grip weapon and paired-hand animations with an ordered controller reload. "
                + DescribeGripContext(hero, weapon));
            return true;
        }

        private bool ProcessPairedRefresh(
            Hero hero,
            CharacterHand currentWeapon)
        {
            if (_pairedRefreshStage == PairedRefreshStage.None)
            {
                return false;
            }

            if (Time.timeScale <= 0.0f)
            {
                _pairedRefreshStartedAt = Time.unscaledTime;
                return true;
            }

            if (Time.unscaledTime - _pairedRefreshStartedAt
                >= PairedRefreshTimeoutSeconds)
            {
                Logger.LogWarning(
                    "Ordered grip weapon and paired-hand animation reload timed out; restoring both drawn models and allowing the normal controller state to recover. "
                    + DescribeGripContext(hero, _pairedRefreshWeapon));
                ShowDiagnosticNotification(
                    "animation-reload-timeout",
                    "VW recovery warning: animation reload timed out; check the BepInEx log.",
                    "High",
                    "vw-recovery");
                CharacterHand timedOutWeapon = _pairedRefreshWeapon;
                CharacterHandBase timedOutPairedHand =
                    _pairedRefreshPairedHand;
                CancelPairedRefresh();
                if (hero != null && hero.IsWeaponEquipped)
                {
                    if (timedOutWeapon != null && timedOutWeapon.IsHidden)
                    {
                        timedOutWeapon.ShowWeapon();
                    }

                    if (timedOutPairedHand != null
                        && timedOutPairedHand.IsHidden)
                    {
                        SetPairedHandHiddenPreservingDrawnState(
                            timedOutPairedHand,
                            false);
                    }
                }

                _observedAnimationStateKnown = false;
                RequestOneHandedReconciliation();
                return false;
            }

            if (!ReferenceEquals(currentWeapon, _pairedRefreshWeapon)
                || _pairedRefreshWeapon == null
                || _pairedRefreshPairedHand == null
                || _pairedRefreshWeapon.Item == null
                || _pairedRefreshPairedHand.Item == null
                || !ReferenceEquals(
                    GetPairedItem(hero, _pairedRefreshWeapon.Item),
                    _pairedRefreshPairedHand.Item))
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
                    PairedRefreshStage.WaitingForGripWeapon;
                LogDiagnostic(
                    "Ordered animation reload advanced to WaitingForGripWeapon. "
                    + DescribeGripContext(hero, _pairedRefreshWeapon));
                return true;
            }

            if (_pairedRefreshStage
                == PairedRefreshStage.WaitingForGripWeapon)
            {
                if (_pairedRefreshWeapon.IsHidden
                    || _pairedRefreshWeapon.IsLoadingAnimator
                    || !AnimatorLayersAreReady(
                        hero,
                        _pairedRefreshWeapon))
                {
                    return true;
                }

                SetPairedHandHiddenPreservingDrawnState(
                    _pairedRefreshPairedHand,
                    false);
                _pairedRefreshStage =
                    PairedRefreshStage.WaitingForPairedHand;
                LogDiagnostic(
                    "Ordered animation reload advanced to WaitingForPairedHand. "
                    + DescribeGripContext(hero, _pairedRefreshWeapon));
                return true;
            }

            if (HandAnimatorsAreSettled(
                    hero,
                    _pairedRefreshWeapon,
                    true)
                && !MagicVisualIsReady(
                    hero,
                    _pairedRefreshPairedHand))
            {
                TryRecoverMissingMagicVisualAfterTransition(
                    hero,
                    _pairedRefreshPairedHand,
                    _pairedRefreshStartedAt);
                return true;
            }

            if (!HandAnimationsAreSettled(
                hero,
                _pairedRefreshWeapon,
                true))
            {
                return true;
            }

            CharacterHand settledWeapon = _pairedRefreshWeapon;
            Item settledPairedItem = _pairedRefreshPairedHand.Item;
            CancelPairedRefresh();
            if (!BeginEquipFsmReset(
                    hero,
                    settledWeapon,
                    settledPairedItem))
            {
                ReconcileGripFsmState(
                    hero,
                    GetGripCombatMode(hero, settledWeapon));
                _observedAnimationStateKnown = false;
                RequestOneHandedReconciliation();
                Logger.LogWarning(
                    "The ordered controller reload settled, but its synchronized equip-FSM reset could not start. "
                    + DescribeGripContext(hero, settledWeapon));
            }
            return true;
        }

        private void CancelPairedRefresh()
        {
            _pairedRefreshStage = PairedRefreshStage.None;
            _pairedRefreshWeapon = null;
            _pairedRefreshPairedHand = null;
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
                DisableActiveGripFsms(hero);

                Item pairedItem = GetPairedItem(hero, weapon.Item);
                CharacterHandBase pairedHand = FindHandBaseForItem(
                    hero,
                    pairedItem);
                if (pairedHand == null)
                {
                    pairedHand = _hiddenPairedHand;
                }

                _observedAnimationStateKnown = false;
                _oneHandedReconciliationPending = desiredState;
                weapon.HideWeapon(true);

                if (pairedHand != null
                    && !ReferenceEquals(pairedHand, weapon))
                {
                    SetPairedHandHiddenPreservingDrawnState(
                        pairedHand,
                        true);
                }

                weapon.ShowWeapon();

                if (desiredState)
                {
                    _hiddenPairedHand = null;
                    if (pairedHand != null
                        && !ReferenceEquals(pairedHand, weapon))
                    {
                        SetPairedHandHiddenPreservingDrawnState(
                            pairedHand,
                            false);
                    }
                }
                else
                {
                    _hiddenPairedHand = pairedHand;
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
                DisableActiveGripFsms(hero);

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
                || requirements.StrengthRequired == null)
            {
                return 1.0f;
            }

            float strengthRequirement = Math.Max(
                0.0f,
                requirements.StrengthRequired.ModifiedValue);
            float fullPotencyStrength = strengthRequirement > 0.0f
                ? strengthRequirement * GetFullPotencyStrengthMultiplier()
                : GetZeroRequirementFullPotencyStrength();
            if (fullPotencyStrength <= strengthRequirement)
            {
                return 1.0f;
            }

            float currentStrength;
            StrengthTestMode testMode = GetActiveStrengthTestMode();
            if (testMode == StrengthTestMode.WeaponRequirement)
            {
                currentStrength = strengthRequirement;
            }
            else if (testMode == StrengthTestMode.FullPotency)
            {
                currentStrength = fullPotencyStrength;
            }
            else
            {
                Hero hero = Hero.Current;
                if (hero == null || hero.HeroRPGStats == null)
                {
                    return 0.0f;
                }

                currentStrength = hero.HeroRPGStats.Strength.ModifiedValue;
            }

            return Mathf.Clamp01(
                (currentStrength - strengthRequirement)
                / (fullPotencyStrength - strengthRequirement));
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

        private float GetZeroRequirementFullPotencyStrength()
        {
            return _zeroRequirementFullPotencyStrength == null
                ? 10.0f
                : Math.Max(
                    0.0f,
                    _zeroRequirementFullPotencyStrength.Value);
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
            return IsSupportedPairedHandItem(pairedItem)
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

        internal static CharacterHandBase GetActiveOffHandTwoHandedGripWeapon(
            Hero hero)
        {
            CharacterHandBase weapon = GetActiveTwoHandedGripWeapon(hero);
            return hero != null
                && weapon != null
                && weapon.Item != null
                && ReferenceEquals(weapon.Item, hero.OffHandItem)
                    ? weapon
                    : null;
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
            CancelEquipFsmReset();
            _observedWeapon = null;
            _observedAnimationStateKnown = false;
            _oneHandedReconciliationPending = false;
            _gripFsmMismatchFrames = 0;
        }

        private static class CloudServiceLoadGripMemoryPatch
        {
            public static void Prefix(
                object __instance,
                string slotId)
            {
                Plugin plugin = Instance;
                if (plugin == null)
                {
                    return;
                }
                try
                {
                    plugin.LoadGripMemoryState(__instance, slotId);
                }
                catch (Exception exception)
                {
                    plugin.Logger.LogWarning(
                        "Could not restore grip memory; vanilla save loading will continue. "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private static class CloudServiceSaveGripMemoryPatch
        {
            public static void Prefix(
                object __instance,
                string slotId)
            {
                Plugin plugin = Instance;
                if (plugin == null)
                {
                    return;
                }
                try
                {
                    plugin.SaveGripMemoryState(
                        writeToArchive: true,
                        cloudService: __instance,
                        slotId: slotId);
                }
                catch (Exception exception)
                {
                    plugin.Logger.LogWarning(
                        "Could not store grip memory; vanilla save finalization will continue. "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private static class NewGameGripMemoryPatch
        {
            public static void Prefix()
            {
                Plugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ClearGripMemorySession();
                }
            }
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
    internal static class HeroLoadoutActivateWeaponAudioPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(HeroLoadout),
                nameof(HeroLoadout.Activate),
                Type.EmptyTypes);
        }

        private static void Prefix()
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.BeginWeaponAudioTransition(
                    "loadout-activate");
            }
        }
    }

    [HarmonyPatch]
    internal static class HeroLoadoutEquipItemWeaponAudioPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(HeroLoadout),
                nameof(HeroLoadout.EquipItem),
                new Type[]
                {
                    typeof(EquipmentSlotType),
                    typeof(Item)
                });
        }

        private static void Prefix(
            HeroLoadout __instance,
            EquipmentSlotType slot)
        {
            if (Plugin.Instance != null
                && __instance != null
                && __instance.IsEquipped
                && (slot == EquipmentSlotType.MainHand
                    || slot == EquipmentSlotType.OffHand))
            {
                Plugin.Instance.BeginWeaponAudioTransition(
                    "active-loadout-slot");
            }
        }
    }

    [HarmonyPatch]
    internal static class ItemEquipToggleWeaponAudioPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ItemEquip),
                nameof(ItemEquip.PlayEquipToggleSound),
                new Type[]
                {
                    typeof(IAlive),
                    typeof(bool)
                });
        }

        private static bool Prefix(
            ItemEquip __instance,
            IAlive owner,
            bool equip)
        {
            return Plugin.Instance == null
                || Plugin.Instance.ShouldAllowWeaponToggleAudio(
                    __instance,
                    owner,
                    equip);
        }
    }

    [HarmonyPatch]
    internal static class EquipWeaponUnsheatheAudioDiagnosticPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(EquipWeaponBase<HeroAnimatorSubstateMachine>),
                "PlayUnsheatheAudio",
                Type.EmptyTypes);
        }

        private static void Prefix(
            EquipWeaponBase<HeroAnimatorSubstateMachine> __instance)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null)
            {
                plugin.BeginUnsheatheAudioDiagnostic(__instance);
            }
        }

        private static void Postfix()
        {
            Plugin.EndUnsheatheAudioDiagnostic();
        }
    }

    [HarmonyPatch]
    internal static class FmodAttachedOneShotDiagnosticPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(FMODManager),
                "PlayAttachedOneShotWithParameters",
                new Type[]
                {
                    typeof(EventReference),
                    typeof(GameObject),
                    typeof(UnityEngine.Object),
                    typeof(FMODParameter[])
                });
        }

        private static void Prefix(
            EventReference eventReference,
            object[] __args)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordFmodTransitionAudioDiagnostic(
                    "attached",
                    eventReference,
                    __args);
            }
        }
    }

    [HarmonyPatch]
    internal static class FmodOneShotDiagnosticPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodBase> targets = new List<MethodBase>();
            foreach (MethodInfo method in typeof(FMODManager).GetMethods(
                BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (String.Equals(
                        method.Name,
                        "PlayOneShot",
                        StringComparison.Ordinal)
                    && parameters.Length > 0
                    && parameters[0].ParameterType
                        == typeof(EventReference))
                {
                    targets.Add(method);
                }
            }
            return targets;
        }

        private static void Prefix(
            EventReference eventReference,
            object[] __args)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordFmodTransitionAudioDiagnostic(
                    "one-shot",
                    eventReference,
                    __args);
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterMagicIdleAudioDiagnosticPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterMagic),
                "PlayIdleAudioEvent",
                new Type[] { typeof(EventReference) });
        }

        private static void Prefix(
            CharacterMagic __instance,
            EventReference eventRef)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordSoulRendAudioLifecycleDiagnostic(
                    "magic-idle",
                    eventRef,
                    __instance,
                    null);
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterHandAudioDiagnosticPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterHandBase),
                "PlayAudioClip",
                new Type[]
                {
                    typeof(EventReference),
                    typeof(bool),
                    typeof(FMODParameter[])
                });
        }

        private static void Prefix(
            CharacterHandBase __instance,
            EventReference eventReference,
            bool asOneShot)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordSoulRendAudioLifecycleDiagnostic(
                    "character-hand",
                    eventReference,
                    __instance,
                    asOneShot);
            }
        }
    }

    [HarmonyPatch]
    internal static class HeroControllerAudioDiagnosticPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(VHeroController),
                "PlayAudioClip",
                new Type[]
                {
                    typeof(EventReference),
                    typeof(bool),
                    typeof(GameObject),
                    typeof(FMODParameter[])
                });
        }

        private static void Prefix(
            VHeroController __instance,
            EventReference eventReference,
            bool asOneShot)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordSoulRendAudioLifecycleDiagnostic(
                    "hero-controller",
                    eventReference,
                    __instance,
                    asOneShot);
            }
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

        private static void Postfix(
            CharacterMagic __instance,
            bool activate)
        {
            if (activate)
            {
                Plugin.DisableConflictingFsms();
            }

            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordAnimatorLayersApplied(
                    __instance,
                    activate);
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterMagicEquipMagicGlovePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterMagic),
                "EquipMagicGloveToHero",
                new Type[] { typeof(Hero), typeof(bool) });
        }

        private static void Prefix(
            CharacterMagic __instance,
            bool mainHand)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordMagicVisualLoadStarted(
                    __instance,
                    mainHand);
            }
        }
    }

    [HarmonyPatch]
    internal static class CharacterMagicSetupMagicGauntletPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CharacterMagic),
                "SetupMagicGauntlet",
                new Type[] { typeof(GameObject), typeof(Item) });
        }

        private static void Postfix(
            CharacterMagic __instance,
            GameObject glove,
            Item owningItem)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordMagicVisualLoadCompleted(
                    __instance,
                    glove,
                    owningItem);
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
            string selectedProfile = "native";
            if (ReferenceEquals(
                Plugin.GetActiveOffHandTwoHandedGripWeapon(Hero.Current),
                __instance))
            {
                __result = Plugin.CreateTwoHandedWeaponController(
                    __instance);
                selectedProfile = "offhand-two-handed";
            }
            else if (Plugin.ShouldUseOneHandedAnimations(__instance))
            {
                __result = Plugin.CreateOneHandedWeaponController(
                    __instance,
                    __result);
                selectedProfile = "converted-one-handed";
            }
            else if (Plugin.ShouldUseTwoHandedAnimations(__instance))
            {
                __result = Plugin.CreateTwoHandedWeaponController(
                    __instance);
                selectedProfile = "converted-two-handed";
            }

            if (Plugin.Instance != null)
            {
                Plugin.Instance.RecordAnimatorControllerSelection(
                    __instance,
                    selectedProfile,
                    __result);
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
            if (ReferenceEquals(
                Plugin.GetActiveOffHandTwoHandedGripWeapon(Hero.Current),
                __instance))
            {
                __result = Plugin.GetTwoHandedLayers();
            }
            else if (Plugin.ShouldUseOneHandedAnimations(__instance))
            {
                __result = Plugin.GetOneHandedLayers(__instance);
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
            if (ReferenceEquals(
                Plugin.GetActiveOffHandTwoHandedGripWeapon(Hero.Current),
                __instance))
            {
                __result = null;
            }
            else if (Plugin.ShouldUseOneHandedAnimations(__instance))
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
    internal static class TwoHandedFsmStatsItemPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(
                typeof(HeroAnimatorSubstateMachine),
                nameof(HeroAnimatorSubstateMachine.StatsItem));
        }

        private static void Postfix(
            HeroAnimatorSubstateMachine __instance,
            ref Item __result)
        {
            if (!(__instance is TwoHandedFSM))
            {
                return;
            }

            CharacterHandBase weapon =
                Plugin.GetActiveOffHandTwoHandedGripWeapon(Hero.Current);
            if (weapon != null && weapon.Item != null)
            {
                __result = weapon.Item;
            }
        }
    }

    [HarmonyPatch]
    internal static class OffHandTwoHandedAnimationSpeedPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(AnimatorUtils),
                nameof(AnimatorUtils.StartProcessingAnimationSpeed));
        }

        private static void Prefix(ref WeaponRestriction weaponRestriction)
        {
            if (weaponRestriction == WeaponRestriction.MainHand
                && Plugin.GetActiveOffHandTwoHandedGripWeapon(Hero.Current)
                    != null)
            {
                weaponRestriction = WeaponRestriction.OffHand;
            }
        }
    }

    [HarmonyPatch]
    internal static class OffHandTwoHandedRestrictionPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(AnimatorRestrictionExtension),
                nameof(AnimatorRestrictionExtension.Match),
                new Type[]
                {
                    typeof(WeaponRestriction),
                    typeof(CharacterHandBase)
                });
        }

        private static void Postfix(
            WeaponRestriction restriction,
            CharacterHandBase hand,
            ref bool __result)
        {
            CharacterHandBase weapon =
                Plugin.GetActiveOffHandTwoHandedGripWeapon(Hero.Current);
            if (weapon != null
                && restriction == WeaponRestriction.MainHand)
            {
                __result = ReferenceEquals(hand, weapon);
            }
        }
    }

    [HarmonyPatch]
    internal static class AnimatorLoadPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(
                typeof(CharacterHand),
                "LoadHeroAnimatorOverrides");
            yield return AccessTools.Method(
                typeof(CharacterMagic),
                "LoadHeroAnimatorOverrides");
        }

        private static void Prefix(CharacterHandBase __instance)
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
