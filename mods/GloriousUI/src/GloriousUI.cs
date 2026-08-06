using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Awaken.TG.Graphics.Cutscenes;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.MVC.UI;
using Awaken.TG.MVC.UI.Events;
using Awaken.TG.MVC.UI.Handlers.States;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet;
using Awaken.TG.Main.Heroes.CharacterSheet.Inventory;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Bag;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Equipment;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Loadouts;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Panel;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Panel.Slot;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Panel.Tabs;
using Awaken.TG.Main.Heroes.CharacterSheet.Tabs;
using Awaken.TG.Main.Heroes.CharacterSheet.QuickUseWheels;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Loadouts;
using Awaken.TG.Main.Heroes.Items.Tooltips;
using Awaken.TG.Main.Localization;
using Awaken.TG.Main.Stories;
using Awaken.TG.Main.UI;
using Awaken.TG.Main.UI.ButtonSystem;
using Awaken.TG.Main.UI.Components.PadShortcuts;
using Awaken.TG.Main.UI.TitleScreen;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using Awaken.TG.Main.Utility;
using Awaken.TG.Main.Utility.UI;
using Awaken.TG.Main.Utility.UI.RadialMenu;
using Awaken.TG.Main.Utility.Video;
using Awaken.TG.Utility;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[assembly: AssemblyTitle("Glorious UI")]
[assembly: AssemblyDescription("Immersive HUD and expanded Equipment-panel controls for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Glorious UI")]
[assembly: AssemblyVersion("1.7.1.0")]
[assembly: AssemblyFileVersion("1.7.1.0")]
[assembly: AssemblyInformationalVersion("1.7.1")]

namespace GloriousUI
{
    public enum SmartSelectionMode
    {
        Biggest,
        SmallestSufficient
    }

    public enum HudAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        Center
    }

    public enum CompassVisibilityMode
    {
        Hidden,
        Always,
        ToggleHotkey,
        HoldHotkey
    }

    public enum LevelNotificationMode
    {
        Timed,
        Disabled,
        Vanilla
    }

    internal enum SmartConsumableKind
    {
        Food,
        HealthPotion,
        ManaPotion
    }

    [Flags]
    internal enum HudLayoutDirty
    {
        None = 0,
        QuickSlotContent = 1,
        QuickSlotTransform = 2,
        Arrow = 4,
        WyrdSkillIndicator = 8,
        WyrdSkillPrompt = 16,
        DrawOrder = 32,
        HeroHud = 64,
        StatusHud = 128,
        All = QuickSlotContent
            | QuickSlotTransform
            | Arrow
            | WyrdSkillIndicator
            | WyrdSkillPrompt
            | DrawOrder
            | HeroHud
            | StatusHud
    }

    internal sealed class FoASettingUiMetadata
    {
        public string DisplaySection { get; set; }
        public string DisplayName { get; set; }
        public string ChoiceLabels { get; set; }
        public int SectionOrder { get; set; }
        public int Order { get; set; }
        public bool Hidden { get; set; }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.eyes-in-the-dark", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class GloriousUIPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.glorious-ui";
        public const string PluginName = "Glorious UI";
        public const string PluginVersion = "1.7.1";

        private const int ConfigSchemaVersion = 1;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
        {
        };
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
        {
            new ConfigDefinition("Diagnostics", "BuffDebuffLayoutTestMode")
        };
        private const float FoodPinIntervalSeconds = 0.25f;
        private const float MinimumHorizontalHudOffset = -4000.0f;
        private const float MaximumHorizontalHudOffset = 4000.0f;
        private const float MinimumVerticalHudOffset = -2000.0f;
        private const float MaximumVerticalHudOffset = 2000.0f;
        private const float MinimumHudScale = 0.25f;
        private const float MaximumHudScale = 3.0f;
        private const float MinimumHeroHudVisibleSeconds = 0.0f;
        private const float MaximumHeroHudVisibleSeconds = 3600.0f;
        private const float QuickSlotHudBaselineOffsetX = 196.0f;
        private const float QuickSlotHudBaselineOffsetY = 39.1f;
        private const float QuickSlotHudBaselineScale = 0.8f;
        private const float HeroHudBaselineOffsetX = -72.0f;
        private const float HeroHudBaselineOffsetY = -25.0f;
        private const float HeroHudBaselineScale = 0.9f;
        private const float ArrowSlotBaselineOffsetX = -7.0f;
        private const float ArrowSlotBaselineOffsetY = -7.0f;
        private const float ArrowSlotBaselineScale = 1.38f;
        private const float ArrowCounterTextBaselineScale = 1.3f;
        private const float WyrdSkillIndicatorBaselineOffsetX = -2.0f;
        private const float WyrdSkillIndicatorBaselineOffsetY = -5.0f;
        private const float WyrdSkillIndicatorBaselineScale = 0.9f;
        private const float BuffDebuffHudBaselineOffsetX = 140.0f;
        private const float BuffDebuffHudBaselineOffsetY = -55.0f;
        private const float BuffDebuffHudBaselineSpacingAdjustment = -2.0f;
        private const string OneMenuEquipPluginGuid = "owrocc.OneMenuEquip";
        private const string BagHotkeysPluginGuid = "owrocc.BagHotkeys";
        private const string EyesInTheDarkPluginGuid =
            "ks.tgfoa.eyes-in-the-dark";
        private const string EyesInTheDarkHudApiTypeName =
            "EyesInTheDark.EyesInTheDarkHudApi";
        private const string HeroHudBarTypeName = "Awaken.TG.Main.Heroes.HUD.VCHeroHUDBar";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";
        private const string HeroItemsTypeName = "Awaken.TG.Main.Heroes.Items.HeroItems";
        private const string HeroItemsEventsTypeName = "Awaken.TG.Main.Heroes.Items.HeroItems+Events";
        private const string EquipmentSlotTypeName = "Awaken.TG.Main.Heroes.Items.EquipmentSlotType";
        private const string ItemTypeName = "Awaken.TG.Main.Heroes.Items.Item";
        private const string CharacterInventoryExtensionTypeName = "Awaken.TG.Main.Character.CharacterInventoryExtension";
        private const string ICharacterInventoryTypeName = "Awaken.TG.Main.Character.ICharacterInventory";
        private const string ILoadoutTypeName = "Awaken.TG.Main.Heroes.Items.Loadouts.ILoadout";
        private const string ItemVariableAccessorTypeName = "Awaken.TG.Main.Heroes.Items.ItemVariableAccessor";
        private const string ModelExtensionsTypeName = "Awaken.TG.MVC.ModelExtensions";
        private const string SelectedQuickSlotViewTypeName = "Awaken.TG.Main.Heroes.HUD.VCSelectedQuickSlot";
        private const string HeroHudViewTypeName = "Awaken.TG.Main.Heroes.VHeroHUD";
        private const string HeroStatusHudTypeName =
            "Awaken.TG.Main.Heroes.HUD.HeroStatusHUD";
        private const string StatusHudTypeName =
            "Awaken.TG.Main.Heroes.HUD.StatusHUD";
        private const string QuestNotificationDataTypeName =
            "Awaken.TG.Main.UIToolkit.PresenterData.Notifications.PQuestNotificationData";
        private const string ObjectiveNotificationDataTypeName =
            "Awaken.TG.Main.UIToolkit.PresenterData.Notifications.PObjectiveNotificationData";
        private const string WyrdSkillBarTypeName = "Awaken.TG.Main.Heroes.HUD.VCHeroWyrdSkillBar";
        private const string CompassViewTypeName = "Awaken.TG.Main.Maps.Compasses.VMapCompass";
        private const string CharacterPointsViewTypeName =
            "Awaken.TG.Main.Heroes.CharacterSheet.VCCharacterPointsAvailable";
        private const string QuickUseWheelViewTypeName =
            "Awaken.TG.Main.Heroes.CharacterSheet.QuickUseWheels.VQuickUseWheelUI";
        private const string LoadoutsViewTypeName =
            "Awaken.TG.Main.Heroes.CharacterSheet.Items.Loadouts.VLoadoutsUI";
        private const string LoadoutSlotViewTypeName =
            "Awaken.TG.Main.Heroes.CharacterSheet.Items.Loadouts.VCLoadoutSlot";
        private const string EquipmentViewTypeName =
            "Awaken.TG.Main.Heroes.CharacterSheet.Items.Equipment.VEquipmentUI";
        private const string EquipmentSlotViewTypeName =
            "Awaken.TG.Main.Heroes.CharacterSheet.Items.Equipment.VCEquipmentSlotBase";
        private const string DefaultEquipmentSlotViewTypeName =
            "Awaken.TG.Main.Heroes.CharacterSheet.Items.Equipment.VCDefaultEquipmentSlot";
        private const string EquipmentSessionFileName =
            "GloriousEquipmentSlots.dat";
        private const string FistsMainHandGuid =
            "f254fb3419610b6429a08b2d0e6d9e70";
        private const string FistsOffHandGuid =
            "3bfcd15275f44a743a561a5884638b41";
        private const int EquipmentWeaponLoadoutCount = 6;
        private const int VanillaWeaponLoadoutActionCount = 4;
        private const int ExtendedWeaponLoadoutHotkeyCount =
            EquipmentWeaponLoadoutCount
            - VanillaWeaponLoadoutActionCount;
        private const int EquipmentQuickSlotCount = 6;
        private const int BagCategoryHotkeyCount = 12;
        private const float EquipmentPanelRefreshIntervalSeconds = 0.15f;

        private static readonly string[] HealthTerms = { "health", "healing", "heal", "vitality" };
        private static readonly string[] ManaTerms = { "mana", "magicka", "magic" };
        internal static GloriousUIPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _pinHudToFoodSlot;
        private ConfigEntry<bool> _replaceSmallHudSlots;
        private ConfigEntry<bool> _renderUtilityHudBehindHeroBars;
        private ConfigEntry<bool> _controlHeroHud;
        private ConfigEntry<HudAnchor> _heroHudAnchor;
        private ConfigEntry<float> _heroHudOffsetX;
        private ConfigEntry<float> _heroHudOffsetY;
        private ConfigEntry<float> _heroHudScale;
        private ConfigEntry<float> _heroHudVisibleSeconds;
        private ConfigEntry<HudAnchor> _quickSlotHudAnchor;
        private ConfigEntry<bool> _ownArrowSlot;
        private ConfigEntry<bool> _hideQuickSlotUsePrompt;
        private ConfigEntry<bool> _hideCyclePrompt;
        private ConfigEntry<float> _quickSlotHudOffsetX;
        private ConfigEntry<float> _quickSlotHudOffsetY;
        private ConfigEntry<float> _quickSlotHudScale;
        private ConfigEntry<float> _arrowSlotOffsetX;
        private ConfigEntry<float> _arrowSlotOffsetY;
        private ConfigEntry<float> _arrowSlotScale;
        private ConfigEntry<bool> _ownWyrdSkillIndicator;
        private ConfigEntry<float> _wyrdSkillIndicatorOffsetX;
        private ConfigEntry<float> _wyrdSkillIndicatorOffsetY;
        private ConfigEntry<float> _wyrdSkillIndicatorScale;
        private ConfigEntry<bool> _hideWyrdSkillPrompt;
        private ConfigEntry<bool> _controlBuffDebuffHud;
        private ConfigEntry<float> _buffDebuffHudOffsetX;
        private ConfigEntry<float> _buffDebuffHudOffsetY;
        private ConfigEntry<float> _buffDebuffHudScale;
        private ConfigEntry<int> _buffDebuffIconsPerRow;
        private ConfigEntry<float> _buffDebuffSpacingScale;
        private ConfigEntry<bool> _controlCompass;
        private ConfigEntry<CompassVisibilityMode> _compassVisibilityMode;
        private ConfigEntry<KeyCode> _compassHotkey;
        private ConfigEntry<LevelNotificationMode>
            _levelNotificationMode;
        private ConfigEntry<float>
            _levelNotificationVisibleSeconds;
        private ConfigEntry<float>
            _levelNotificationFadeSeconds;
        private ConfigEntry<float>
            _questNotificationDurationSeconds;
        private ConfigEntry<bool> _hideGameplayHudInQuickUseWheel;
        private ConfigEntry<bool> _controlEquipmentWeaponLoadouts;
        private ConfigEntry<float> _equipmentWeaponRowScale;
        private ConfigEntry<float> _equipmentWeaponRowSpacing;
        private ConfigEntry<bool> _controlQuickUseWheelLoadouts;
        private ConfigEntry<bool> _hideQuickWheelCenterControl;
        private ConfigEntry<bool> _hideQuickWheelControlsLegend;
        private ConfigEntry<bool> _quickWheelLeftClickSelect;
        private ConfigEntry<bool> _ammoCounterEnabled;
        private ConfigEntry<bool> _arrowCycleEnabled;
        private ConfigEntry<bool> _controlEquipmentQuickSlots;
        private readonly ConfigEntry<KeyCode>[] _equipmentQuickSlotHotkeys =
            new ConfigEntry<KeyCode>[EquipmentQuickSlotCount];
        private ConfigEntry<bool> _enableOneMenuEquip;
        private ConfigEntry<KeyboardShortcut> _oneMenuEquipMainHandShortcut;
        private ConfigEntry<KeyboardShortcut> _oneMenuEquipOffHandShortcut;
        private ConfigEntry<bool> _oneMenuEquipShowNotifications;
        private ConfigEntry<bool> _oneMenuEquipToggleEquippedItem;
        private ConfigEntry<bool> _oneMenuEquipApplyToOffHandPicker;
        private ConfigEntry<bool> _oneMenuEquipRedirectOffHandPicker;
        private ConfigEntry<bool> _oneMenuEquipInterceptWeaponClicks;
        private ConfigEntry<KeyCode> _smartInventoryBagHotkey;
        private readonly ConfigEntry<KeyCode>[] _bagCategoryHotkeys =
            new ConfigEntry<KeyCode>[BagCategoryHotkeyCount];
        private readonly ConfigEntry<KeyCode>[]
            _extendedWeaponLoadoutHotkeys =
                new ConfigEntry<KeyCode>[
                    ExtendedWeaponLoadoutHotkeyCount];
        private ConfigEntry<KeyCode> _healthPotionHotkey;
        private ConfigEntry<KeyCode> _manaPotionHotkey;
        private ConfigEntry<SmartSelectionMode> _foodSelectionMode;
        private ConfigEntry<SmartSelectionMode> _healthPotionSelectionMode;
        private ConfigEntry<SmartSelectionMode> _manaPotionSelectionMode;
        private ConfigEntry<bool> _preventPotionWasteAtFull;
        private ConfigEntry<bool> _ignoreHotkeysWhenCursorVisible;
        private ConfigEntry<bool> _layeringDiagnostics;
        private ConfigEntry<bool> _buffDebuffLayoutTestMode;
        private ConfigEntry<int> _buffDebuffLayoutTestIconCount;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _logPatchWarnings;

        private Harmony _harmony;
        private bool _forceSelectingFood;
        private bool _refreshingFoodSlot;
        private bool _accessorsReady;
        private bool _accessorFailureLogged;
        private bool _heroItemsPatchesAttempted;
        private bool _heroItemsPatchesInstalled;
        private float _nextFoodPinTime;
        private HudLayoutDirty _dirtyHudLayout;

        private Type _heroType;
        private Type _heroItemsType;
        private Type _itemType;
        private Type _equipmentSlotType;
        private Type _characterInventoryType;
        private Type _iLoadoutType;

        private PropertyInfo _heroCurrentProperty;
        private PropertyInfo _selectedQuickSlotTypeProperty;
        private FieldInfo _foodQuickSlotField;
        private FieldInfo _quickSlot2Field;
        private FieldInfo _quickSlot3Field;
        private FieldInfo _quickSlotUsedEventField;
        private FieldInfo _itemIconField;
        private FieldInfo _nextItemIconsField;
        private FieldInfo _useStaticPromptField;
        private FieldInfo _nextStaticPromptField;
        private FieldInfo _heroHudSelectedQuickSlotField;
        private FieldInfo _heroHudArrowsImageField;
        private FieldInfo _heroHudArrowsCounterField;
        private FieldInfo _heroHudHeroBarsTransformField;
        private FieldInfo _heroHudShowTimerField;
        private FieldInfo _heroHudRefreshedLastlyField;
        private FieldInfo _compassCanvasGroupField;
        private FieldInfo _characterPointsCanvasGroupField;

        private MethodInfo _selectQuickSlotMethod;
        private MethodInfo _equippedItemMethod;
        private MethodInfo _equipMethod;
        private MethodInfo _itemUseMethod;
        private MethodInfo _getVariableMethod;
        private MethodInfo _getHealValueMethod;
        private MethodInfo _triggerQuickSlotUsedMethod;
        private MethodInfo _heroHudUpdateCanvasGroupsMethod;
        private MethodInfo _characterPointsUpdateVisualMethod;

        private object _foodQuickSlot;
        private object _quickSlot2;
        private object _quickSlot3;
        private object _quickSlotUsedEvent;
        private object _activeSelectedQuickSlotView;
        private object _activeHeroHudView;
        private object _activeHeroStatusHud;
        private object _activeCompassView;
        private object _activeCharacterPointsView;
        private VQuickUseWheelUI _activeQuickUseWheelView;
        private float _characterPointsFadeStartTime = -1.0f;
        private float _characterPointsFadeEndTime = -1.0f;
        private float _suppressCharacterPointsRefreshUntil =
            -1.0f;
        private bool? _lastCharacterPointsMapInteractive;
        private CharacterPointsQuickUseSnapshot
            _characterPointsQuickUseSnapshot;
        private object _activeLoadoutsView;
        private object _activeEquipmentView;
        private Type _heroHudViewType;
        private Type _heroHudBarType;
        private Type _wyrdSkillBarType;
        private MethodInfo _eyesInTheDarkPlacementRequest;
        private bool? _lastEyesInTheDarkPlacementRequest;
        private bool _eyesInTheDarkBridgeFailureLogged;
        private bool _quickUseWheelOpen;
        private bool _compassToggleVisible;
        private bool _restoringVanillaHud;
        private bool? _lastCompassRequestedVisible;
        private readonly Dictionary<int, bool> _compassGameVisibility =
            new Dictionary<int, bool>();
        private readonly Dictionary<int, QuickUseHudObjectSnapshot> _quickUseHudObjectSnapshots =
            new Dictionary<int, QuickUseHudObjectSnapshot>();
        private readonly List<GameObject> _statusHudTestObjects =
            new List<GameObject>();
        private StatusHudLayoutSnapshot _statusHudLayoutSnapshot;

        private readonly Dictionary<int, SmartIconOverlay> _smartIconOverlays =
            new Dictionary<int, SmartIconOverlay>();
        private readonly Dictionary<int, HudTransformSnapshot> _hudTransformSnapshots =
            new Dictionary<int, HudTransformSnapshot>();
        private readonly Dictionary<int, ArrowCounterTextSnapshot>
            _arrowCounterTextSnapshots =
                new Dictionary<int, ArrowCounterTextSnapshot>();
        private readonly Dictionary<int, RectTransform> _wyrdSkillIndicatorRects =
            new Dictionary<int, RectTransform>();
        private readonly Dictionary<int, WyrdSkillPromptSnapshot> _wyrdSkillPromptSnapshots =
            new Dictionary<int, WyrdSkillPromptSnapshot>();
        private readonly Dictionary<int, HudSiblingSnapshot> _hudSiblingSnapshots =
            new Dictionary<int, HudSiblingSnapshot>();
        private readonly Dictionary<string, float> _pendingPreservedHudTuning =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, KeyCode> _pendingPreservedHotkeys =
            new Dictionary<string, KeyCode>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;
        private readonly Vector3[] _worldCorners = new Vector3[4];
        private EquipmentWeaponPanel _equipmentWeaponPanel;
        private EquipmentQuickPanel _equipmentQuickPanel;
        private object _equipmentQuickSlotBackingView;
        private int _selectedEquipmentQuickSlot = 1;
        private bool _syncingEquipmentQuickSlotBacking;
        private float _nextEquipmentPanelRefreshTime;
        private float _nextEquipmentPanelBuildRetryTime;
        private int _equipmentPanelBuildNotBeforeFrame;
        private bool _equipmentQuickPanelBuildFailureLogged;
        private readonly Dictionary<int, VirtualWeaponLoadoutData>
            _virtualWeaponLoadouts =
                new Dictionary<int, VirtualWeaponLoadoutData>();
        private readonly Dictionary<int, QuickWheelLoadoutProxy>
            _quickWheelLoadoutProxies =
                new Dictionary<int, QuickWheelLoadoutProxy>();
        private readonly List<GameObject> _quickWheelLoadoutClones =
            new List<GameObject>();
        private VCQuickLoadout _hoveredQuickWheelLoadout;
        private bool _quickWheelTextFontWarningLogged;
        private int _lastQuickWheelArrowCycleFrame = -1;
        private int _lastQuickWheelArrowCycleSlot;
        private readonly string[] _virtualQuickSlotItemGuids =
            new string[EquipmentQuickSlotCount];
        private readonly string[] _virtualQuickSlotItemIds =
            new string[EquipmentQuickSlotCount];
        private int _currentVirtualWeaponSlot;
        private string _activeEquipmentSaveSlot;
        private bool _pendingApplyLoadedWeaponSlot;
        private float _pendingEquipmentApplyTime;
        private float _nextEquipmentBackendTrackTime;
        private VirtualWeaponLoadoutData
            _lastTrackedWeaponSnapshot;
        private bool _hasTrackedWeaponSnapshot;
        private int _lastWeaponLoadoutActivationFrame = -1;
        private int _lastWeaponLoadoutActivationSlot;
        private Type _arButtonType;
        private PropertyInfo _arButtonTargetGraphicProperty;
        private PropertyInfo _arButtonTransitionProperty;
        private FieldInfo _arButtonHasGraphicField;
        private EventInfo _arButtonOnClickEvent;
        private EventInfo _arButtonOnHoverEvent;
        private EventInfo _arButtonOnSelectedEvent;
        private FieldInfo _quickLoadoutMiddlePointField;
        private FieldInfo _quickLoadoutPrimarySlotField;
        private FieldInfo _quickLoadoutSecondarySlotField;
        private FieldInfo _quickLoadoutPrimaryRectField;
        private FieldInfo _quickLoadoutSecondaryRectField;
        private FieldInfo _quickLoadoutPrimarySelectedField;
        private FieldInfo _quickLoadoutSecondarySelectedField;
        private FieldInfo _radialOptionInitialPositionField;
        private FieldInfo _quickUseOptionChosenIndicatorField;
        private bool _oneMenuHooksInitialized;
        private bool _externalOneMenuEquipDetected;
        private EquipmentChooseUI _oneMenuLastChooseUi;
        private Item _oneMenuLastHoveredItem;
        private bool _oneMenuLastHoveredItemWasEquipped;
        private int _oneMenuLastUnequipFrame = -1;
        private BagUI _oneMenuLastBagUi;
        private Item _oneMenuLastBagHoveredItem;
        private bool _oneMenuLastBagHoveredItemWasEquipped;
        private int _oneMenuLastBagUnequipFrame = -1;
        private FieldInfo _oneMenuTargetSlotField;
        private MethodInfo _oneMenuHoveredItemsChangedMethod;
        private MethodInfo _oneMenuBagRefreshPromptsMethod;
        private GameObject _oneMenuInvisibleOverlay;
        private Item _oneMenuLastOverlayItem;
        private bool _oneMenuExecutingMainHandEquip;
        private float _oneMenuNotificationHideTime;
        private string _oneMenuNotificationText = string.Empty;
        private GUIStyle _oneMenuNotificationStyle;
        private int _smartInventoryBagHandledFrame = -1;
        private InventorySubTabType _pendingInventorySubTab;
        private ItemsTabType _pendingBagCategory;
        private float _pendingInventorySubTabDeadline;
        private CharacterSheetUI _observedCharacterSheet;
        private object _observedCharacterSheetTab;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                BindConfig();
                Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                    Config,
                    Logger,
                    PluginName,
                    ConfigSchemaVersion,
                    ConfigRecoveryBaselineSchema,
                    ConfigRecoveryKeepCurrentDefaultRules,
                    ConfigRecoveryPermanentExclusions);
                if (IsExternalBagHotkeysLoaded())
                {
                    Logger.LogWarning(
                        "owrocc.BagHotkeys is loaded alongside Glorious UI. Both plugins poll the same physical keys, so the external plugin can reopen the Bag after Glorious closes it. Remove or disable owrocc.BagHotkeys; its category hotkeys are included in Glorious UI.");
                }
                ResolveEyesInTheDarkIntegration();
                CacheGameAccessors();
                if (!PatchGame())
                {
                    enabled = false;
                    return;
                }

                Config.Save();
                Logger.LogInfo(
                    PluginName + " " + PluginVersion + " loaded. HealthHotkey="
                    + _healthPotionHotkey.Value + "; ManaHotkey=" + _manaPotionHotkey.Value + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled || !_accessorsReady)
            {
                return;
            }

            try
            {
                PatchOneMenuEquipHooks();
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Could not initialize one-menu equip compatibility: "
                    + exception);
            }
        }

        private void Update()
        {
            UpdateEyesInTheDarkPlacementRequest();
            if (!IsEnabled() || !_accessorsReady)
            {
                return;
            }

            UpdateSmartInventoryBagHotkey();
            UpdateBagCategoryHotkeys();
            UpdatePendingInventorySubTab();
            UpdateQuickWheelMouseSelection();
            UpdateCompassHotkey();
            UpdateCharacterSheetTabLifecycle();
            UpdateCharacterPointsTimedVisibility();
            PruneInactiveEquipmentViews();
            EnsureEquipmentPanels();
            UpdateEquipmentWeaponPanel();
            UpdateStandaloneEquipmentBackend();
            UpdateOneMenuEquipHotkeys();
            UpdateExtendedWeaponLoadoutHotkeys();
            if (_ignoreHotkeysWhenCursorVisible.Value && Cursor.visible)
            {
                return;
            }

            MaintainFoodSelection();
            UpdateEquipmentQuickSlotHotkeys();

            if (_healthPotionHotkey.Value != KeyCode.None
                && Input.GetKeyDown(_healthPotionHotkey.Value))
            {
                TryUseSmartConsumable(SmartConsumableKind.HealthPotion);
                return;
            }

            if (_manaPotionHotkey.Value != KeyCode.None
                && Input.GetKeyDown(_manaPotionHotkey.Value))
            {
                TryUseSmartConsumable(SmartConsumableKind.ManaPotion);
            }
        }

        private void UpdateSmartInventoryBagHotkey()
        {
            if (_smartInventoryBagHandledFrame
                    == Time.frameCount
                || !ShouldOwnSmartInventoryBagHotkey())
            {
                return;
            }

            KeyCode hotkey =
                _smartInventoryBagHotkey == null
                ? KeyCode.None
                : _smartInventoryBagHotkey.Value;
            if (hotkey == KeyCode.None
                || !Input.GetKeyDown(hotkey))
            {
                return;
            }

            _smartInventoryBagHandledFrame =
                Time.frameCount;
            _pendingInventorySubTab = null;
            _pendingBagCategory = null;

            if (TryCloseActiveClosable())
            {
                return;
            }

            CharacterSheetUI characterSheet =
                World.Any<CharacterSheetUI>();
            if (IsLiveModel(characterSheet))
            {
                InventoryUI inventory =
                    characterSheet.TryGetElement<InventoryUI>();
                BagUI bag = inventory == null
                    ? null
                    : inventory.TryGetElement<BagUI>();
                ItemsUI items = bag == null
                    ? null
                    : bag.TryGetElement<ItemsUI>();
                bool bagIsOpen =
                    characterSheet.CurrentType
                        == CharacterSheetTabType.Inventory
                    && inventory != null
                    && inventory.CurrentType
                        == InventorySubTabType.Bag
                    && items != null;
                if (bagIsOpen)
                {
                    ItemsTabType allCategory =
                        ResolveBagAllCategory(bag);
                    if (IsBagAllSelected(
                        items,
                        allCategory))
                    {
                        TryCloseCharacterSheet(characterSheet);
                    }
                    else
                    {
                        QueueBagCategory(ItemsTabType.All);
                    }
                    return;
                }

                QueueBagCategory(ItemsTabType.All);
                return;
            }

            if (TryCloseActiveMenu())
            {
                return;
            }

            characterSheet =
                CharacterSheetUI.ToggleCharacterSheet(
                    CharacterSheetTabType.Inventory);
            if (characterSheet != null)
            {
                QueueBagCategory(ItemsTabType.All);
            }
        }

        private void UpdateBagCategoryHotkeys()
        {
            if (_smartInventoryBagHandledFrame == Time.frameCount)
            {
                return;
            }

            for (int i = 0; i < _bagCategoryHotkeys.Length; i++)
            {
                ConfigEntry<KeyCode> entry = _bagCategoryHotkeys[i];
                if (entry == null
                    || entry.Value == KeyCode.None
                    || !Input.GetKeyDown(entry.Value))
                {
                    continue;
                }

                _smartInventoryBagHandledFrame = Time.frameCount;
                OpenBagCategory(BagCategoryForHotkeyIndex(i));
                return;
            }
        }

        private void OpenBagCategory(ItemsTabType category)
        {
            CharacterSheetUI characterSheet =
                World.Any<CharacterSheetUI>();
            if (!IsLiveModel(characterSheet))
            {
                if (TryCloseActiveMenu())
                {
                    return;
                }

                characterSheet =
                    CharacterSheetUI.ToggleCharacterSheet(
                        CharacterSheetTabType.Inventory);
            }

            if (characterSheet != null)
            {
                QueueBagCategory(category);
            }
        }

        private bool ShouldOwnSmartInventoryBagHotkey()
        {
            return IsEnabled()
                && _smartInventoryBagHotkey != null
                && _smartInventoryBagHotkey.Value
                    != KeyCode.None;
        }

        internal bool ShouldSuppressVanillaCharacterSheetAction(
            UIEvent evt)
        {
            UIKeyDownAction action =
                evt as UIKeyDownAction;
            return ShouldOwnSmartInventoryBagHotkey()
                && action != null
                && action.Name
                    == KeyBindings.UI.CharacterSheets.CharacterSheet
                && Input.GetKeyDown(
                    _smartInventoryBagHotkey.Value);
        }

        internal void HandleVanillaEquipmentAction(
            UIEvent evt)
        {
            UIKeyDownAction action =
                evt as UIKeyDownAction;
            if (!ShouldOwnSmartInventoryBagHotkey()
                || action == null
                || action.Name
                    != KeyBindings.UI.CharacterSheets.Inventory
                || !IsLiveModel(
                    World.Any<CharacterSheetUI>()))
            {
                return;
            }

            QueueInventorySubTab(
                InventorySubTabType.Equipment);
        }

        private void QueueInventorySubTab(
            InventorySubTabType subTab)
        {
            _pendingInventorySubTab = subTab;
            _pendingInventorySubTabDeadline =
                Time.unscaledTime + 2.0f;
            UpdatePendingInventorySubTab();
        }

        private void QueueBagCategory(ItemsTabType category)
        {
            _pendingInventorySubTab = InventorySubTabType.Bag;
            _pendingBagCategory = category;
            _pendingInventorySubTabDeadline =
                Time.unscaledTime + 2.0f;
            UpdatePendingInventorySubTab();
        }

        private void UpdatePendingInventorySubTab()
        {
            InventorySubTabType subTab =
                _pendingInventorySubTab;
            ItemsTabType bagCategory =
                _pendingBagCategory;
            if (subTab == null && bagCategory == null)
            {
                return;
            }
            if (Time.unscaledTime
                > _pendingInventorySubTabDeadline)
            {
                _pendingInventorySubTab = null;
                _pendingBagCategory = null;
                return;
            }

            CharacterSheetUI characterSheet =
                World.Any<CharacterSheetUI>();
            if (!IsLiveModel(characterSheet)
                || characterSheet.TabsController == null)
            {
                return;
            }

            if (!ReferenceEquals(
                characterSheet.CurrentType,
                CharacterSheetTabType.Inventory))
            {
                characterSheet.TabsController.SelectTab(
                    CharacterSheetTabType.Inventory);
                return;
            }

            InventoryUI inventory = characterSheet == null
                ? null
                : characterSheet.TryGetElement<InventoryUI>();
            if (inventory == null
                || inventory.TabsController == null)
            {
                return;
            }

            if (subTab != null
                && !ReferenceEquals(inventory.CurrentType, subTab))
            {
                inventory.TabsController.SelectTab(subTab);
                return;
            }

            _pendingInventorySubTab = null;
            if (bagCategory == null)
            {
                return;
            }

            BagUI bag = inventory.TryGetElement<BagUI>();
            ItemsUI items = bag == null
                ? null
                : bag.TryGetElement<ItemsUI>();
            if (items == null || items.TabsController == null)
            {
                return;
            }

            ItemsTabType targetCategory =
                ReferenceEquals(bagCategory, ItemsTabType.All)
                ? ResolveBagAllCategory(bag)
                : bagCategory;
            if (ReferenceEquals(
                targetCategory,
                ItemsTabType.AllWithRecent))
            {
                items.SetCurrentTabFilter(
                    ItemsTabType.AllWithRecent,
                    ItemsTabType.All);
            }
            items.TabsController.SelectTab(targetCategory);
            if (ReferenceEquals(items.CurrentType, targetCategory))
            {
                items.FullRefresh();
            }
            _pendingBagCategory = null;
        }

        private static bool IsBagAllSelected(
            ItemsUI items,
            ItemsTabType allCategory)
        {
            if (items == null
                || !ReferenceEquals(items.CurrentType, allCategory))
            {
                return false;
            }

            return !ReferenceEquals(
                allCategory,
                ItemsTabType.AllWithRecent)
                || !ReferenceEquals(
                    items.GetCurrentTabFilter(allCategory),
                    ItemsTabType.Recent);
        }

        private static ItemsTabType ResolveBagAllCategory(BagUI bag)
        {
            if (bag != null)
            {
                foreach (ItemsTabType category in bag.Tabs)
                {
                    if (ReferenceEquals(category, ItemsTabType.All))
                    {
                        return ItemsTabType.All;
                    }
                }
            }

            return ItemsTabType.AllWithRecent;
        }

        private static ItemsTabType BagCategoryForHotkeyIndex(int index)
        {
            switch (index)
            {
                case 0: return ItemsTabType.Weapons;
                case 1: return ItemsTabType.Magic;
                case 2: return ItemsTabType.Armor;
                case 3: return ItemsTabType.Jewelry;
                case 4: return ItemsTabType.Gem;
                case 5: return ItemsTabType.Potion;
                case 6: return ItemsTabType.Consumable;
                case 7: return ItemsTabType.Crafting;
                case 8: return ItemsTabType.Readable;
                case 9: return ItemsTabType.Recipes;
                case 10: return ItemsTabType.QuestItems;
                default: return ItemsTabType.Others;
            }
        }

        private static string BagCategoryNameForHotkeyIndex(int index)
        {
            switch (index)
            {
                case 0: return "Weapons";
                case 1: return "Magic";
                case 2: return "Armor";
                case 3: return "Jewelry";
                case 4: return "Gems and Sigils";
                case 5: return "Potions";
                case 6: return "Consumables";
                case 7: return "Crafting";
                case 8: return "Readables";
                case 9: return "Recipes";
                case 10: return "Quest Items";
                default: return "Other";
            }
        }

        private static string BagCategorySettingNameForHotkeyIndex(
            int index)
        {
            switch (index)
            {
                case 0: return "BagWeaponsHotkey";
                case 1: return "BagMagicHotkey";
                case 2: return "BagArmorHotkey";
                case 3: return "BagJewelryHotkey";
                case 4: return "BagGemsHotkey";
                case 5: return "BagPotionsHotkey";
                case 6: return "BagConsumablesHotkey";
                case 7: return "BagCraftingHotkey";
                case 8: return "BagReadablesHotkey";
                case 9: return "BagRecipesHotkey";
                case 10: return "BagQuestItemsHotkey";
                default: return "BagOtherHotkey";
            }
        }

        private void UpdateQuickWheelMouseSelection()
        {
            if (!_quickUseWheelOpen
                || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            VCQuickLoadout option =
                _hoveredQuickWheelLoadout;
            if (option != null
                && option.gameObject.activeInHierarchy)
            {
                if (TryCycleQuickWheelArrowsAtPointer(
                        option))
                {
                    return;
                }
                if (_quickWheelLeftClickSelect != null
                    && _quickWheelLeftClickSelect.Value)
                {
                    SelectQuickWheelLoadout(option);
                }
            }
        }

        private void UpdateExtendedWeaponLoadoutHotkeys()
        {
            if (!ShouldControlEquipmentWeaponLoadouts()
                || _quickUseWheelOpen
                || IsWeaponLoadoutInputBlocked())
            {
                return;
            }

            int slot = GetPressedExtendedWeaponLoadout();
            if (slot == 0)
            {
                return;
            }

            LogDiagnostic(
                "Extended weapon loadout hotkey selected Glorious slot "
                + slot.ToString(
                    CultureInfo.InvariantCulture)
                + ".");
            ActivateEquipmentWeaponLoadout(slot);
        }

        private int GetPressedExtendedWeaponLoadout()
        {
            for (int i = 0;
                i < _extendedWeaponLoadoutHotkeys.Length;
                i++)
            {
                if (IsWeaponLoadoutHotkeyPressed(
                    _extendedWeaponLoadoutHotkeys[i]))
                {
                    return i
                        + VanillaWeaponLoadoutActionCount
                        + 1;
                }
            }
            return 0;
        }

        internal bool TryRedirectVanillaWeaponLoadout(
            int vanillaIndex)
        {
            if (!ShouldControlEquipmentWeaponLoadouts()
                || vanillaIndex < 0
                || vanillaIndex
                    >= VanillaWeaponLoadoutActionCount)
            {
                return false;
            }

            if (_quickUseWheelOpen
                || IsWeaponLoadoutInputBlocked())
            {
                return true;
            }

            int slot = vanillaIndex + 1;
            LogDiagnostic(
                "Vanilla weapon loadout action selected Glorious slot "
                + slot.ToString(
                    CultureInfo.InvariantCulture)
                + ".");
            ActivateEquipmentWeaponLoadout(slot);
            return true;
        }

        private static bool IsWeaponLoadoutHotkeyPressed(
            ConfigEntry<KeyCode> hotkey)
        {
            return hotkey != null
                && hotkey.Value != KeyCode.None
                && Input.GetKeyDown(hotkey.Value);
        }

        private bool IsWeaponLoadoutInputBlocked()
        {
            Hero hero = Hero.Current;
            if (hero == null
                || hero.HeroItems == null
                || ((Model)hero).HasElement<HeroKnockdown>())
            {
                return true;
            }

            LoadingScreenUI loading =
                World.Any<LoadingScreenUI>();
            if ((loading != null
                    && !((Model)loading).HasBeenDiscarded)
                || LoadingScreenUI.IsLoading
                || IsLiveModel(World.Any<Cutscene>())
                || IsLiveModel(World.Any<Video>())
                || IsLiveModel(World.Any<TitleScreenUI>())
                || IsLiveModel(
                    World.Any<HeroDialogueInvolvement>()))
            {
                return true;
            }

            if (Time.timeScale > 0.0f)
            {
                return false;
            }

            return !IsLiveModel(World.Any<QuickUseWheelUI>())
                && !IsLiveModel(World.Any<LoadoutsUI>())
                && !IsLiveModel(World.Any<BagUI>())
                && !IsLiveModel(World.Any<ItemsUI>());
        }

        private static bool IsLiveModel(Model model)
        {
            return model != null && !model.HasBeenDiscarded;
        }

        private bool TryCloseActiveMenu()
        {
            if (TryCloseActiveClosable())
            {
                return true;
            }

            CharacterSheetUI characterSheet =
                World.Any<CharacterSheetUI>();
            if (IsLiveModel(characterSheet))
            {
                return TryCloseCharacterSheet(characterSheet);
            }
            return false;
        }

        private static bool TryCloseActiveClosable()
        {
            IClosable closable = World.LastOrNull<IClosable>();
            if (closable != null && !closable.HasBeenDiscarded)
            {
                ClearItemPickerTooltip();
                closable.Close();
                return true;
            }
            return false;
        }

        private static bool TryCloseCharacterSheet(
            CharacterSheetUI characterSheet)
        {
            if (!IsLiveModel(characterSheet))
            {
                return false;
            }

            ClearItemPickerTooltip();
            CharacterSheetTabs tabs =
                characterSheet.TryGetElement<CharacterSheetTabs>();
            if (tabs != null && !tabs.HasBeenDiscarded)
            {
                tabs.TryHandleBack(characterSheet.TryDiscard);
            }
            else
            {
                characterSheet.TryDiscard();
            }
            return true;
        }

        private static void ClearItemPickerTooltip()
        {
            ItemTooltipUI tooltip = World.Any<ItemTooltipUI>();
            if (tooltip != null && !tooltip.HasBeenDiscarded)
            {
                tooltip.SetDescriptor(null);
            }
        }

        private void OnGUI()
        {
            if (_oneMenuEquipShowNotifications == null
                || !_oneMenuEquipShowNotifications.Value
                || string.IsNullOrEmpty(_oneMenuNotificationText)
                || Time.unscaledTime > _oneMenuNotificationHideTime)
            {
                return;
            }

            if (_oneMenuNotificationStyle == null)
            {
                _oneMenuNotificationStyle =
                    new GUIStyle(GUI.skin.label);
                _oneMenuNotificationStyle.alignment =
                    TextAnchor.MiddleCenter;
                _oneMenuNotificationStyle.fontSize = 16;
                _oneMenuNotificationStyle.normal.textColor =
                    Color.white;
            }

            GUI.Label(
                new Rect(
                    Screen.width * 0.5f - 240.0f,
                    Screen.height * 0.75f,
                    480.0f,
                    32.0f),
                _oneMenuNotificationText,
                _oneMenuNotificationStyle);
        }

        private void LateUpdate()
        {
            MaintainWyrdSkillPromptVisibility();
            MaintainQuickSlotPromptVisibility();
            if (_dirtyHudLayout == HudLayoutDirty.None)
            {
                return;
            }

            HudLayoutDirty dirtyLayout = _dirtyHudLayout;
            _dirtyHudLayout = HudLayoutDirty.None;

            if ((dirtyLayout & HudLayoutDirty.HeroHud) != 0)
            {
                ApplyHeroHudTransform(_activeHeroHudView);
            }

            if ((dirtyLayout & HudLayoutDirty.StatusHud) != 0)
            {
                ApplyStatusHudLayout(_activeHeroStatusHud);
            }

            if ((dirtyLayout & HudLayoutDirty.QuickSlotContent) != 0)
            {
                ApplySmartHudIcons(_activeSelectedQuickSlotView);
            }

            if ((dirtyLayout & HudLayoutDirty.QuickSlotTransform) != 0)
            {
                ApplyQuickSlotHudTransform(_activeSelectedQuickSlotView);
            }

            if ((dirtyLayout & HudLayoutDirty.Arrow) != 0)
            {
                ApplyArrowHudTransform(_activeHeroHudView);
            }

            if ((dirtyLayout & HudLayoutDirty.WyrdSkillIndicator) != 0)
            {
                ApplyWyrdSkillIndicatorTransform(_activeHeroHudView);
            }

            if ((dirtyLayout & HudLayoutDirty.WyrdSkillPrompt) != 0)
            {
                ApplyWyrdSkillPromptVisibility(_activeHeroHudView);
            }

            if ((dirtyLayout & HudLayoutDirty.DrawOrder) != 0)
            {
                ApplyHudDrawOrder(_activeHeroHudView);
            }
        }

        private void OnDestroy()
        {
            _restoringVanillaHud = true;
            _quickUseWheelOpen = false;
            RestoreQuickUseHudObjects();
            RestoreCompassVisibility();
            RestoreCharacterPointsVisibility();
            ReleaseEyesInTheDarkPlacementRequest();
            ReleaseHeroStatusHud(_activeHeroStatusHud);
            ReleaseQuickUseWheelLoadouts(_activeQuickUseWheelView);
            ReleaseEquipmentWeaponPanel();
            ReleaseEquipmentQuickPanel();
            DestroyOneMenuInvisibleOverlay();
            RestoreQuickSlotUsePromptVisibility(_activeSelectedQuickSlotView);
            ReleaseAllHudTransforms();
            ReleaseAllSmartIcons();

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                UiDescription(
                    "Master switch.",
                    "General",
                    "Enable Glorious UI",
                    10,
                    10));
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _pinHudToFoodSlot = Config.Bind(
                "2. HUD",
                "PinHudToFoodSlot",
                true,
                UiDescription(
                    "Keep the vanilla quick-slot use key focused on the autofill food slot instead of cycling to manual slots 1 and 2.",
                    "General",
                    "Keep Food Slot Selected",
                    10,
                    20));
            _replaceSmallHudSlots = Config.Bind(
                "2. HUD",
                "ReplaceSmallHudSlots",
                true,
                UiDescription(
                    "Replace the two small vanilla next-slot icons with smart health and mana potion icons.",
                    "Quick Slot HUD",
                    "Show Smart Potion Previews",
                    20,
                    10));
            _renderUtilityHudBehindHeroBars = Config.Bind(
                "2. HUD",
                "RenderUtilityHudBehindHeroBars",
                false,
                UiDescription(
                    "Render the Quick Slot HUD, arrow counter, and Wyrd Power HUD behind the health, mana, and stamina bars without changing their positions.",
                    "General",
                    "Render Utility HUD Behind Hero Bars",
                    10,
                    30));
            _hideGameplayHudInQuickUseWheel = Config.Bind(
                "2. HUD",
                "HideGameplayHudInQuickUseWheel",
                true,
                UiDescription(
                    "Hide the Hero HUD, quick slots, arrow counter, Wyrd Power HUD, compass, and level notifications while the quick-use wheel is open.",
                    "General",
                    "Hide Gameplay HUD in Quick-Use Wheel",
                    10,
                    40));
            _controlHeroHud = Config.Bind(
                "2. HUD",
                "ControlHeroHud",
                true,
                UiDescription(
                    "Move, scale, and control the fade timer for the health, mana, and stamina HUD.",
                    "Hero HUD",
                    "Control Hero HUD",
                    15,
                    10));
            _heroHudAnchor = Config.Bind(
                "2. HUD",
                "HeroHudAnchor",
                HudAnchor.BottomCenter,
                UiDescription(
                    "Screen anchor used for the Hero HUD. Bottom Center keeps the layout centered at every resolution and aspect ratio.",
                    "Hero HUD",
                    "Screen Anchor",
                    15,
                    20,
                    choiceLabels: "TopLeft=Top Left;TopCenter=Top Center;TopRight=Top Right;BottomLeft=Bottom Left;BottomCenter=Bottom Center;BottomRight=Bottom Right;Center=Center"));
            _heroHudOffsetX = Config.Bind(
                "2. HUD",
                "HeroHudOffsetX",
                0.0f,
                UiDescription(
                    "Horizontal adjustment in UI pixels from Glorious UI's built-in Hero HUD position. The neutral value is 0; positive moves right.",
                    "Hero HUD",
                    "Horizontal Offset",
                    15,
                    30,
                    new AcceptableValueRange<float>(
                        MinimumHorizontalHudOffset,
                        MaximumHorizontalHudOffset)));
            _heroHudOffsetY = Config.Bind(
                "2. HUD",
                "HeroHudOffsetY",
                0.0f,
                UiDescription(
                    "Vertical adjustment in UI pixels from Glorious UI's built-in Hero HUD position. The neutral value is 0; positive moves up.",
                    "Hero HUD",
                    "Vertical Offset",
                    15,
                    40,
                    new AcceptableValueRange<float>(
                        MinimumVerticalHudOffset,
                        MaximumVerticalHudOffset)));
            _heroHudScale = Config.Bind(
                "2. HUD",
                "HeroHudScale",
                1.0f,
                UiDescription(
                    "Scale adjustment relative to Glorious UI's built-in Hero HUD size. The neutral value is 1.0.",
                    "Hero HUD",
                    "Scale",
                    15,
                    50,
                    new AcceptableValueRange<float>(MinimumHudScale, MaximumHudScale)));
            _heroHudVisibleSeconds = Config.Bind(
                "2. HUD",
                "HeroHudVisibleSeconds",
                2.0f,
                UiDescription(
                    "Seconds the Hero HUD remains visible after the game refreshes it. Set to 0 to fade immediately.",
                    "Hero HUD",
                    "Visible Duration",
                    15,
                    60,
                    new AcceptableValueRange<float>(
                        MinimumHeroHudVisibleSeconds,
                        MaximumHeroHudVisibleSeconds)));
            _ownArrowSlot = Config.Bind(
                "2. HUD",
                "OwnArrowSlot",
                true,
                UiDescription(
                    "Move and scale the vanilla arrow counter into the Glorious UI quick-slot cluster.",
                    "Arrow HUD",
                    "Control Arrow Counter",
                    30,
                    10));
            _hideQuickSlotUsePrompt = Config.Bind(
                "2. HUD",
                "HideQuickSlotUsePrompt",
                true,
                UiDescription(
                    "Hide the vanilla quick-slot use prompt without disabling the quick-slot input.",
                    "Quick Slot HUD",
                    "Hide Quick Slot Use Prompt",
                    20,
                    20));
            _hideCyclePrompt = Config.Bind(
                "2. HUD",
                "HideCyclePrompt",
                true,
                UiDescription(
                    "Hide the vanilla next-slot prompt beside the small icons.",
                    "Quick Slot HUD",
                    "Hide Cycle Prompt",
                    20,
                    30));
            _quickSlotHudAnchor = Config.Bind(
                "2. HUD",
                "QuickSlotHudAnchor",
                HudAnchor.BottomCenter,
                UiDescription(
                    "Screen anchor used for the Quick Slot HUD. Bottom Center keeps the utility cluster aligned with a centered Hero HUD at every resolution.",
                    "Quick Slot HUD",
                    "Screen Anchor",
                    20,
                    40,
                    choiceLabels: "TopLeft=Top Left;TopCenter=Top Center;TopRight=Top Right;BottomLeft=Bottom Left;BottomCenter=Bottom Center;BottomRight=Bottom Right;Center=Center"));
            _quickSlotHudOffsetX = Config.Bind(
                "2. HUD",
                "QuickSlotHudOffsetX",
                0.0f,
                UiDescription(
                    "Horizontal adjustment in UI pixels from Glorious UI's built-in centered position. The neutral value is 0; positive moves right.",
                    "Quick Slot HUD",
                    "Horizontal Offset",
                    20,
                    50,
                    new AcceptableValueRange<float>(
                        MinimumHorizontalHudOffset,
                        MaximumHorizontalHudOffset)));
            _quickSlotHudOffsetY = Config.Bind(
                "2. HUD",
                "QuickSlotHudOffsetY",
                0.0f,
                UiDescription(
                    "Vertical adjustment in UI pixels from Glorious UI's built-in centered position. The neutral value is 0; positive moves up.",
                    "Quick Slot HUD",
                    "Vertical Offset",
                    20,
                    60,
                    new AcceptableValueRange<float>(
                        MinimumVerticalHudOffset,
                        MaximumVerticalHudOffset)));
            _quickSlotHudScale = Config.Bind(
                "2. HUD",
                "QuickSlotHudScale",
                1.0f,
                UiDescription(
                    "Scale adjustment relative to Glorious UI's built-in size. The neutral value is 1.0.",
                    "Quick Slot HUD",
                    "Scale",
                    20,
                    70,
                    new AcceptableValueRange<float>(MinimumHudScale, MaximumHudScale)));
            _arrowSlotOffsetX = Config.Bind(
                "2. HUD",
                "ArrowSlotOffsetX",
                0.0f,
                UiDescription(
                    "Horizontal arrow adjustment from Glorious UI's built-in position. The neutral value is 0; positive moves right.",
                    "Arrow HUD",
                    "Horizontal Offset",
                    30,
                    20,
                    new AcceptableValueRange<float>(
                        MinimumHorizontalHudOffset,
                        MaximumHorizontalHudOffset)));
            _arrowSlotOffsetY = Config.Bind(
                "2. HUD",
                "ArrowSlotOffsetY",
                0.0f,
                UiDescription(
                    "Vertical arrow adjustment from Glorious UI's built-in position. The neutral value is 0; positive moves up.",
                    "Arrow HUD",
                    "Vertical Offset",
                    30,
                    30,
                    new AcceptableValueRange<float>(
                        MinimumVerticalHudOffset,
                        MaximumVerticalHudOffset)));
            _arrowSlotScale = Config.Bind(
                "2. HUD",
                "ArrowSlotScale",
                1.0f,
                UiDescription(
                    "Arrow scale adjustment relative to Glorious UI's built-in size. The neutral value is 1.0.",
                    "Arrow HUD",
                    "Scale",
                    30,
                    40,
                    new AcceptableValueRange<float>(MinimumHudScale, MaximumHudScale)));
            _ownWyrdSkillIndicator = Config.Bind(
                "2. HUD",
                "OwnWyrdSkillIndicator",
                true,
                UiDescription(
                    "Allow Glorious UI to move and scale the vanilla Wyrd Skill Indicator, including its visible shortcut prompt.",
                    "Wyrd Power HUD",
                    "Control Wyrd Position and Scale",
                    40,
                    20));
            _wyrdSkillIndicatorOffsetX = Config.Bind(
                "2. HUD",
                "WyrdSkillIndicatorOffsetX",
                0.0f,
                UiDescription(
                    "Horizontal adjustment from Glorious UI's built-in Wyrd Power position. The neutral value is 0; positive moves right.",
                    "Wyrd Power HUD",
                    "Horizontal Offset",
                    40,
                    30,
                    new AcceptableValueRange<float>(
                        MinimumHorizontalHudOffset,
                        MaximumHorizontalHudOffset)));
            _wyrdSkillIndicatorOffsetY = Config.Bind(
                "2. HUD",
                "WyrdSkillIndicatorOffsetY",
                0.0f,
                UiDescription(
                    "Vertical adjustment from Glorious UI's built-in Wyrd Power position. The neutral value is 0; positive moves up.",
                    "Wyrd Power HUD",
                    "Vertical Offset",
                    40,
                    40,
                    new AcceptableValueRange<float>(
                        MinimumVerticalHudOffset,
                        MaximumVerticalHudOffset)));
            _wyrdSkillIndicatorScale = Config.Bind(
                "2. HUD",
                "WyrdSkillIndicatorScale",
                1.0f,
                UiDescription(
                    "Scale adjustment relative to Glorious UI's built-in Wyrd Power size. The neutral value is 1.0.",
                    "Wyrd Power HUD",
                    "Scale",
                    40,
                    50,
                    new AcceptableValueRange<float>(MinimumHudScale, MaximumHudScale)));
            _hideWyrdSkillPrompt = Config.Bind(
                "2. HUD",
                "HideWyrdSkillPrompt",
                true,
                UiDescription(
                    "Hide the vanilla Wyrd power use prompt without disabling its hotkey or hiding the Wyrd Skill Indicator. This works independently of Wyrd position and scale control.",
                    "Wyrd Power HUD",
                    "Hide Wyrd Use Prompt",
                    40,
                    10));
            _controlBuffDebuffHud = Config.Bind(
                "2. HUD",
                "ControlBuffDebuffHud",
                true,
                UiDescription(
                    "Allow Glorious UI to position and arrange the vanilla buff and debuff icons above the Hero HUD.",
                    "Buffs and Debuffs",
                    "Glorious Controls Buffs and Debuffs",
                    43,
                    10));
            _buffDebuffHudOffsetX = Config.Bind(
                "2. HUD",
                "BuffDebuffHudOffsetX",
                0.0f,
                UiDescription(
                    "Horizontal adjustment from Glorious UI's built-in status position. The neutral value is 0; positive moves right.",
                    "Buffs and Debuffs",
                    "Horizontal Offset",
                    43,
                    20,
                    new AcceptableValueRange<float>(
                        MinimumHorizontalHudOffset,
                        MaximumHorizontalHudOffset)));
            _buffDebuffHudOffsetY = Config.Bind(
                "2. HUD",
                "BuffDebuffHudOffsetY",
                0.0f,
                UiDescription(
                    "Vertical adjustment from Glorious UI's built-in status position. The neutral value is 0; positive moves icons upward and negative moves them downward.",
                    "Buffs and Debuffs",
                    "Vertical Offset",
                    43,
                    30,
                    new AcceptableValueRange<float>(
                        MinimumVerticalHudOffset,
                        MaximumVerticalHudOffset)));
            _buffDebuffHudScale = Config.Bind(
                "2. HUD",
                "BuffDebuffHudScale",
                1.0f,
                UiDescription(
                    "Status icon scale relative to the game's native size. The neutral value is 1.0.",
                    "Buffs and Debuffs",
                    "Scale",
                    43,
                    40,
                    new AcceptableValueRange<float>(
                        MinimumHudScale,
                        MaximumHudScale)));
            _buffDebuffIconsPerRow = Config.Bind(
                "2. HUD",
                "BuffDebuffIconsPerRow",
                9,
                UiDescription(
                    "Maximum icons on each row before a new row grows upward.",
                    "Buffs and Debuffs",
                    "Icons Per Row",
                    43,
                    50,
                    new AcceptableValueRange<int>(1, 20)));
            _buffDebuffSpacingScale = Config.Bind(
                "2. HUD",
                "BuffDebuffSpacingScale",
                1.0f,
                UiDescription(
                    "Multiplies Glorious UI's built-in horizontal and vertical status spacing. The neutral value is 1.0; 0 packs icons together.",
                    "Buffs and Debuffs",
                    "Spacing",
                    43,
                    60,
                    new AcceptableValueRange<float>(0.0f, 5.0f)));
            _controlCompass = Config.Bind(
                "2. HUD",
                "ControlCompass",
                true,
                UiDescription(
                    "Allow Glorious UI to control when the vanilla compass is visible.",
                    "Compass",
                    "Control Compass Visibility",
                    45,
                    10));
            _compassVisibilityMode = Config.Bind(
                "2. HUD",
                "CompassVisibilityMode",
                CompassVisibilityMode.Hidden,
                UiDescription(
                    "Choose whether the compass stays hidden, stays visible, toggles with a key press, or appears only while a key is held.",
                    "Compass",
                    "Visibility Mode",
                    45,
                    20,
                    choiceLabels: "Hidden=Hidden;Always=Always Visible;ToggleHotkey=Toggle with Hotkey;HoldHotkey=Hold Hotkey to Show"));
            _compassHotkey = Config.Bind(
                "2. HUD",
                "CompassHotkey",
                KeyCode.None,
                UiDescription(
                    "Hotkey used by Toggle with Hotkey and Hold Hotkey to Show. Set to None to leave the compass hidden in either hotkey mode.",
                    "Compass",
                    "Visibility Hotkey",
                    45,
                    30));
            _levelNotificationMode = Config.Bind(
                "2. HUD",
                "LevelNotificationMode",
                LevelNotificationMode.Disabled,
                UiDescription(
                    "Choose timed five-second reminders, fully disabled reminders, or the game's vanilla sixty-second behavior for the top-right points, bonfire, Arthur memory, and Wyrd whisper widget.",
                    "Notifications",
                    "Level and Point Notifications",
                    47,
                    10,
                    choiceLabels: "Timed=Timed Fade;Disabled=Disabled;Vanilla=Vanilla"));
            _levelNotificationVisibleSeconds = Config.Bind(
                "2. HUD",
                "LevelNotificationVisibleSeconds",
                5.0f,
                UiDescription(
                    "How long a changed level or point notification remains fully visible before Glorious starts fading it. Used only in Timed Fade mode.",
                    "Notifications",
                    "Visible Seconds",
                    47,
                    20,
                    new AcceptableValueRange<float>(
                        0.0f,
                        60.0f)));
            _levelNotificationFadeSeconds = Config.Bind(
                "2. HUD",
                "LevelNotificationFadeSeconds",
                1.0f,
                UiDescription(
                    "How long Glorious takes to fade a timed level or point notification. Set to 0 for an immediate hide after the visible timer.",
                    "Notifications",
                    "Fade Seconds",
                    47,
                    30,
                    new AcceptableValueRange<float>(
                        0.0f,
                        5.0f)));
            _questNotificationDurationSeconds = Config.Bind(
                "2. HUD",
                "QuestNotificationDurationSeconds",
                10.0f,
                UiDescription(
                    "Seconds the game's native new, completed, and failed quest notices plus objective updates remain fully visible. Set to 0 to keep the game duration.",
                    "Notifications",
                    "Quest and Objective Duration",
                    47,
                    40,
                    new AcceptableValueRange<float>(
                        0.0f,
                        60.0f)));
            _controlEquipmentWeaponLoadouts = Config.Bind(
                "5. Equipment Panel",
                "ControlEquipmentWeaponLoadouts",
                true,
                UiDescription(
                    "Replace the extra vanilla weapon rows in the Equipment tab with six standalone Glorious selectors. The top vanilla row remains the active editable loadout.",
                    "Equipment Panel",
                    "Glorious Controls Weapon Loadouts",
                    70,
                    10));
            _equipmentWeaponRowScale = Config.Bind(
                "5. Equipment Panel",
                "EquipmentWeaponRowScale",
                0.72f,
                UiDescription(
                    "Size of the six virtual weapon-loadout rows relative to the active vanilla row.",
                    "Equipment Panel",
                    "Weapon Loadout Row Scale",
                    70,
                    20,
                    new AcceptableValueRange<float>(0.45f, 1.0f)));
            _equipmentWeaponRowSpacing = Config.Bind(
                "5. Equipment Panel",
                "EquipmentWeaponRowSpacing",
                4.0f,
                UiDescription(
                    "Vertical spacing in UI pixels between virtual weapon-loadout rows.",
                    "Equipment Panel",
                    "Weapon Loadout Row Spacing",
                    70,
                    30,
                    new AcceptableValueRange<float>(0.0f, 30.0f)));
            _controlQuickUseWheelLoadouts = Config.Bind(
                "5. Equipment Panel",
                "ControlQuickUseWheelLoadouts",
                true,
                UiDescription(
                    "Replace the quick-use wheel's four native loadout wedges and three quick-item wedges with Glorious's six virtual weapon loadouts. Changes apply the next time the wheel opens.",
                    "Equipment Panel",
                    "Show Six Loadouts in Quick-Use Wheel",
                    70,
                    35));
            _hideQuickWheelCenterControl = Config.Bind(
                "5. Equipment Panel",
                "HideQuickWheelCenterControl",
                true,
                UiDescription(
                    "Hide the mouse or controller diagram in the center of the quick-use wheel.",
                    "Quick-Use Wheel",
                    "Hide Center Control Diagram",
                    71,
                    20));
            _hideQuickWheelControlsLegend = Config.Bind(
                "5. Equipment Panel",
                "HideQuickWheelControlsLegend",
                true,
                UiDescription(
                    "Hide the Select, Equip, and Back control legend below the quick-use wheel.",
                    "Quick-Use Wheel",
                    "Hide Controls Legend",
                    71,
                    30));
            _quickWheelLeftClickSelect = Config.Bind(
                "5. Equipment Panel",
                "QuickWheelLeftClickSelect",
                true,
                UiDescription(
                    "Immediately equip and close the wheel when the highlighted Glorious loadout is left-clicked.",
                    "Quick-Use Wheel",
                    "Left Click Selects Loadout",
                    71,
                    40));
            _ammoCounterEnabled = Config.Bind(
                "5. Equipment Panel",
                "AmmoCounterEnabled",
                true,
                UiDescription(
                    "Show the remaining quantity of the arrow type assigned to each Glorious bow loadout.",
                    "Quick-Use Wheel",
                    "Ammo Counters",
                    71,
                    50));
            _arrowCycleEnabled = Config.Bind(
                "5. Equipment Panel",
                "ArrowCycleEnabled",
                true,
                UiDescription(
                    "Left-click the arrow icon on an Glorious bow loadout to cycle that virtual loadout through available arrow types without selecting or closing the wheel.",
                    "Quick-Use Wheel",
                    "Arrow Cycling",
                    71,
                    60));
            _controlEquipmentQuickSlots = Config.Bind(
                "5. Equipment Panel",
                "ControlEquipmentQuickSlots",
                true,
                UiDescription(
                    "Replace the two visible manual quick slots in the Equipment tab with six assignable virtual quick slots arranged in two columns by three rows. The vanilla food autofill slot remains below them.",
                    "Equipment Panel",
                    "Glorious Controls Quick Slots",
                    70,
                    40));
            for (int i = 0; i < EquipmentQuickSlotCount; i++)
            {
                int slot = i + 1;
                _equipmentQuickSlotHotkeys[i] = Config.Bind(
                    "5. Equipment Panel",
                    "QuickSlot" + slot.ToString(CultureInfo.InvariantCulture) + "Hotkey",
                    KeyCode.None,
                    UiDescription(
                        "Optional gameplay hotkey that directly uses virtual quick slot "
                            + slot.ToString(CultureInfo.InvariantCulture)
                            + ". Set to None to disable.",
                        "Equipment Panel",
                        "Quick Slot "
                            + slot.ToString(CultureInfo.InvariantCulture)
                            + " Hotkey",
                        70,
                        50 + i * 10));
            }
            _enableOneMenuEquip = Config.Bind(
                "6. One-Menu Equip",
                "EnableOneMenuEquip",
                true,
                UiDescription(
                    "Allow weapons and spells to be equipped to either hand from one equipment picker or from the Bag.",
                    "One-Menu Equipping",
                    "Equip Both Hands from One Menu",
                    72,
                    10));
            _oneMenuEquipMainHandShortcut = Config.Bind(
                "6. One-Menu Equip",
                "EquipHoveredToMainHand",
                new KeyboardShortcut(KeyCode.Mouse0),
                UiDescription(
                    "Equip the hovered weapon or spell to the main hand. Mouse0 preserves normal left-click behavior through Glorious's controlled equip path.",
                    "One-Menu Equipping",
                    "Equip Hovered to Main Hand",
                    72,
                    20));
            _oneMenuEquipOffHandShortcut = Config.Bind(
                "6. One-Menu Equip",
                "EquipHoveredToOffHand",
                new KeyboardShortcut(KeyCode.Mouse1),
                UiDescription(
                    "Equip the hovered weapon or spell to the off hand without opening a separate picker.",
                    "One-Menu Equipping",
                    "Equip Hovered to Off Hand",
                    72,
                    30));
            _oneMenuEquipShowNotifications = Config.Bind(
                "6. One-Menu Equip",
                "ShowEquipNotifications",
                false,
                UiDescription(
                    "Show a short on-screen confirmation or failure message after a one-menu equip action.",
                    "One-Menu Equipping",
                    "Show Equip Notifications",
                    72,
                    40));
            _oneMenuEquipToggleEquippedItem = Config.Bind(
                "6. One-Menu Equip",
                "ToggleAlreadyEquippedItem",
                true,
                UiDescription(
                    "Selecting a weapon already equipped in either hand unequips it instead of trying to equip it again.",
                    "One-Menu Equipping",
                    "Selecting Equipped Item Unequips It",
                    72,
                    50));
            _oneMenuEquipApplyToOffHandPicker = Config.Bind(
                "6. One-Menu Equip",
                "ApplyToOffHandPicker",
                true,
                UiDescription(
                    "Enable the same hover, click, comparison, and hand-selection behavior when the off-hand picker is opened directly.",
                    "One-Menu Equipping",
                    "Apply to Off-Hand Picker",
                    72,
                    60));
            _oneMenuEquipRedirectOffHandPicker = Config.Bind(
                "6. One-Menu Equip",
                "RedirectOffHandPickerToMainHand",
                false,
                UiDescription(
                    "Open the main-hand picker when the off-hand slot is selected. Leave disabled to retain the off-hand picker's own comparison tooltips.",
                    "One-Menu Equipping",
                    "Redirect Off-Hand Picker",
                    72,
                    70));
            _oneMenuEquipInterceptWeaponClicks = Config.Bind(
                "6. One-Menu Equip",
                "InterceptWeaponClicks",
                true,
                UiDescription(
                    "Use an invisible raycast target over hovered weapons so Glorious can route Mouse0 and Mouse1 cleanly without also firing the vanilla action.",
                    "One-Menu Equipping",
                    "Intercept Weapon Clicks",
                    72,
                    80));

            _smartInventoryBagHotkey = Config.Bind(
                "3. Hotkeys",
                "SmartInventoryBagHotkey",
                KeyCode.Tab,
                UiDescription(
                    "Physical key that opens Bag > All from gameplay. In a Character Sheet item picker it acts as Back first. Otherwise, in the Character Sheet it navigates to Bag > All, then closes through the game's native Back hierarchy when pressed again there. In another modal menu it acts as Back instead of opening the Bag. Glorious suppresses a duplicate generic Character Sheet action on the same press; the separate Equipment action remains available.",
                    "Hotkeys",
                    "Smart Bag / All Toggle",
                    50,
                    5));
            for (int i = 0; i < _bagCategoryHotkeys.Length; i++)
            {
                string categoryName = BagCategoryNameForHotkeyIndex(i);
                _bagCategoryHotkeys[i] = Config.Bind(
                    "3. Hotkeys",
                    BagCategorySettingNameForHotkeyIndex(i),
                    KeyCode.None,
                    UiDescription(
                        "Optional direct hotkey for the Bag's "
                            + categoryName
                            + " category. It opens or switches to that category and does not toggle the Bag closed. Set to None to disable.",
                        "Bag Category Hotkeys",
                        categoryName,
                        52,
                        10 + i));
            }
            for (int i = 0;
                i < _extendedWeaponLoadoutHotkeys.Length;
                i++)
            {
                int slot = i
                    + VanillaWeaponLoadoutActionCount
                    + 1;
                _extendedWeaponLoadoutHotkeys[i] = Config.Bind(
                    "3. Hotkeys",
                    "WeaponLoadout"
                        + slot.ToString(
                            CultureInfo.InvariantCulture)
                        + "Hotkey",
                    (KeyCode)((int)KeyCode.Alpha1
                        + slot - 1),
                    UiDescription(
                        "Dedicated Glorious hotkey for extended weapon loadout "
                            + slot.ToString(
                                CultureInfo.InvariantCulture)
                            + ". The game provides native configurable actions for Glorious loadouts 1 through 4 only; set this extended key to None to disable it.",
                        "Hotkeys",
                        "Weapon Loadout "
                            + slot.ToString(
                                CultureInfo.InvariantCulture),
                        50,
                        9 + i));
            }
            _healthPotionHotkey = Config.Bind(
                "3. Hotkeys",
                "HealthPotionHotkey",
                KeyCode.C,
                UiDescription(
                    "Smart health potion hotkey. Food continues to use the game's existing quick-slot use key. Set to None to disable.",
                    "Hotkeys",
                    "Health Potion",
                    50,
                    20));
            _manaPotionHotkey = Config.Bind(
                "3. Hotkeys",
                "ManaPotionHotkey",
                KeyCode.V,
                UiDescription(
                    "Smart mana potion hotkey. Set to None to disable.",
                    "Hotkeys",
                    "Mana Potion",
                    50,
                    20));
            _ignoreHotkeysWhenCursorVisible = Config.Bind(
                "3. Hotkeys",
                "IgnoreHotkeysWhenCursorVisible",
                true,
                UiDescription(
                    "Avoid using smart potions while menu cursors are visible.",
                    "Hotkeys",
                    "Ignore Hotkeys While Menus Are Open",
                    50,
                    30));

            _foodSelectionMode = Config.Bind(
                "4. Smart Selection",
                "FoodSelectionMode",
                SmartSelectionMode.Biggest,
                UiDescription(
                    "How the autofill food slot chooses food after the current food runs out.",
                    "Smart Selection",
                    "Food Selection",
                    60,
                    10,
                    choiceLabels: "Biggest=Largest Available;SmallestSufficient=Smallest Sufficient"));
            _healthPotionSelectionMode = Config.Bind(
                "4. Smart Selection",
                "HealthPotionSelectionMode",
                SmartSelectionMode.SmallestSufficient,
                UiDescription(
                    "How the health potion hotkey chooses a potion.",
                    "Smart Selection",
                    "Health Potion Selection",
                    60,
                    20,
                    choiceLabels: "Biggest=Largest Available;SmallestSufficient=Smallest Sufficient"));
            _manaPotionSelectionMode = Config.Bind(
                "4. Smart Selection",
                "ManaPotionSelectionMode",
                SmartSelectionMode.SmallestSufficient,
                UiDescription(
                    "How the mana potion hotkey chooses a potion.",
                    "Smart Selection",
                    "Mana Potion Selection",
                    60,
                    30,
                    choiceLabels: "Biggest=Largest Available;SmallestSufficient=Smallest Sufficient"));
            _preventPotionWasteAtFull = Config.Bind(
                "4. Smart Selection",
                "PreventPotionWasteAtFull",
                true,
                UiDescription(
                    "Do not use health or mana potions when the corresponding resource is already full.",
                    "Smart Selection",
                    "Prevent Potion Waste at Full Resources",
                    60,
                    40));

            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                UiDescription(
                    "Log smart slot decisions and skipped hotkey uses.",
                    "Advanced / Diagnostics",
                    "Diagnostics",
                    90,
                    10));
            _layeringDiagnostics = Config.Bind(
                "Diagnostics",
                "LayeringDiagnostics",
                true,
                UiDescription(
                    "Log the Quick Slot HUD, Wyrd Power HUD, and hero-bar sibling indices plus nested Canvas sorting overrides when draw order is applied.",
                    "Advanced / Diagnostics",
                    "Log HUD Layering Diagnostics",
                    90,
                    20));
            _buffDebuffLayoutTestMode = Config.Bind(
                "Diagnostics",
                "BuffDebuffLayoutTestMode",
                false,
                UiDescription(
                    "Show temporary visual-only buff and debuff placeholders to test wrapping and upward expansion. This does not apply gameplay effects or alter the save.",
                    "Advanced / Diagnostics",
                    "Buff/Debuff Layout Test Mode",
                    90,
                    30));
            _buffDebuffLayoutTestIconCount = Config.Bind(
                "Diagnostics",
                "BuffDebuffLayoutTestIconCount",
                12,
                UiDescription(
                    "Number of visual-only status placeholders shown while Buff/Debuff Layout Test Mode is enabled.",
                    "Advanced / Diagnostics",
                    "Buff/Debuff Test Icon Count",
                    90,
                    40,
                    new AcceptableValueRange<int>(1, 30)));
            _logPatchWarnings = Config.Bind(
                "Diagnostics",
                "LogPatchWarnings",
                true,
                UiDescription(
                    "Log warnings when optional game hooks are unavailable.",
                    "Advanced / Diagnostics",
                    "Log Optional Hook Warnings",
                    90,
                    50));

            RestorePreservedUserSettings();

            SubscribeHudSetting(_enabled, HudLayoutDirty.All);
            _enabled.SettingChanged += delegate
            {
                UpdateEyesInTheDarkPlacementRequest();
            };
            SubscribeHudSetting(_replaceSmallHudSlots, HudLayoutDirty.QuickSlotContent);
            SubscribeHudSetting(_hideQuickSlotUsePrompt, HudLayoutDirty.QuickSlotContent);
            SubscribeHudSetting(_hideCyclePrompt, HudLayoutDirty.QuickSlotContent);
            SubscribeHudSetting(_renderUtilityHudBehindHeroBars, HudLayoutDirty.DrawOrder);
            SubscribeHudSetting(
                _controlHeroHud,
                HudLayoutDirty.HeroHud | HudLayoutDirty.StatusHud);
            _controlHeroHud.SettingChanged += delegate
            {
                RefreshHeroHudTimer(_activeHeroHudView);
            };
            SubscribeHudSetting(
                _heroHudAnchor,
                HudLayoutDirty.HeroHud | HudLayoutDirty.StatusHud);
            SubscribeHudSetting(
                _heroHudOffsetX,
                HudLayoutDirty.HeroHud | HudLayoutDirty.StatusHud);
            SubscribeHudSetting(
                _heroHudOffsetY,
                HudLayoutDirty.HeroHud | HudLayoutDirty.StatusHud);
            SubscribeHudSetting(
                _heroHudScale,
                HudLayoutDirty.HeroHud | HudLayoutDirty.StatusHud);
            _heroHudVisibleSeconds.SettingChanged += delegate
            {
                RefreshHeroHudTimer(_activeHeroHudView);
            };
            SubscribeHudSetting(_quickSlotHudAnchor, HudLayoutDirty.QuickSlotTransform | HudLayoutDirty.Arrow);
            SubscribeHudSetting(
                _quickSlotHudOffsetX,
                HudLayoutDirty.QuickSlotTransform | HudLayoutDirty.Arrow);
            SubscribeHudSetting(
                _quickSlotHudOffsetY,
                HudLayoutDirty.QuickSlotTransform | HudLayoutDirty.Arrow);
            SubscribeHudSetting(
                _quickSlotHudScale,
                HudLayoutDirty.QuickSlotTransform | HudLayoutDirty.Arrow);
            SubscribeHudSetting(_ownArrowSlot, HudLayoutDirty.Arrow);
            SubscribeHudSetting(_arrowSlotOffsetX, HudLayoutDirty.Arrow);
            SubscribeHudSetting(_arrowSlotOffsetY, HudLayoutDirty.Arrow);
            SubscribeHudSetting(_arrowSlotScale, HudLayoutDirty.Arrow);
            SubscribeHudSetting(_ownWyrdSkillIndicator, HudLayoutDirty.WyrdSkillIndicator);
            SubscribeHudSetting(_wyrdSkillIndicatorOffsetX, HudLayoutDirty.WyrdSkillIndicator);
            SubscribeHudSetting(_wyrdSkillIndicatorOffsetY, HudLayoutDirty.WyrdSkillIndicator);
            SubscribeHudSetting(_wyrdSkillIndicatorScale, HudLayoutDirty.WyrdSkillIndicator);
            SubscribeHudSetting(_hideWyrdSkillPrompt, HudLayoutDirty.WyrdSkillPrompt);
            SubscribeHudSetting(_controlBuffDebuffHud, HudLayoutDirty.StatusHud);
            SubscribeHudSetting(_buffDebuffHudOffsetX, HudLayoutDirty.StatusHud);
            SubscribeHudSetting(_buffDebuffHudOffsetY, HudLayoutDirty.StatusHud);
            SubscribeHudSetting(_buffDebuffHudScale, HudLayoutDirty.StatusHud);
            SubscribeHudSetting(_buffDebuffIconsPerRow, HudLayoutDirty.StatusHud);
            SubscribeHudSetting(_buffDebuffSpacingScale, HudLayoutDirty.StatusHud);
            SubscribeHudSetting(_buffDebuffLayoutTestMode, HudLayoutDirty.StatusHud);
            SubscribeHudSetting(_buffDebuffLayoutTestIconCount, HudLayoutDirty.StatusHud);
            _controlEquipmentWeaponLoadouts.SettingChanged += delegate
            {
                _pendingApplyLoadedWeaponSlot =
                    _controlEquipmentWeaponLoadouts.Value
                    && _currentVirtualWeaponSlot >= 1;
                _pendingEquipmentApplyTime =
                    Time.unscaledTime + 0.1f;
                RebuildEquipmentWeaponPanel();
            };
            _equipmentWeaponRowScale.SettingChanged += delegate
            {
                RebuildEquipmentWeaponPanel();
            };
            _equipmentWeaponRowSpacing.SettingChanged += delegate
            {
                RebuildEquipmentWeaponPanel();
            };
            _controlEquipmentQuickSlots.SettingChanged += delegate
            {
                RebuildEquipmentQuickPanel();
            };
            _ammoCounterEnabled.SettingChanged += delegate
            {
                ApplyQuickWheelPresentation(
                    _activeQuickUseWheelView);
                RefreshAllQuickWheelLoadoutProxies();
            };
            _enableOneMenuEquip.SettingChanged += delegate
            {
                _oneMenuLastOverlayItem = null;
                if (!ShouldUseOneMenuEquip())
                {
                    DestroyOneMenuInvisibleOverlay();
                }
                else
                {
                    UpdateOneMenuInvisibleOverlay(
                        _oneMenuLastChooseUi,
                        _oneMenuLastHoveredItem);
                }
            };
            _oneMenuEquipInterceptWeaponClicks.SettingChanged += delegate
            {
                _oneMenuLastOverlayItem = null;
                UpdateOneMenuInvisibleOverlay(
                    _oneMenuLastChooseUi,
                    _oneMenuLastHoveredItem);
            };
            _oneMenuEquipApplyToOffHandPicker.SettingChanged += delegate
            {
                _oneMenuLastOverlayItem = null;
                UpdateOneMenuInvisibleOverlay(
                    _oneMenuLastChooseUi,
                    _oneMenuLastHoveredItem);
            };
            _controlCompass.SettingChanged += delegate
            {
                RefreshCompassVisibility(false);
            };
            _compassVisibilityMode.SettingChanged += delegate
            {
                _compassToggleVisible = false;
                _lastCompassRequestedVisible = null;
                RefreshCompassVisibility(false);
            };
            _compassHotkey.SettingChanged += delegate
            {
                _compassToggleVisible = false;
                _lastCompassRequestedVisible = null;
                RefreshCompassVisibility(false);
            };
            _levelNotificationMode.SettingChanged += delegate
            {
                ApplyCharacterPointsVisibility(
                    _activeCharacterPointsView);
            };
            _levelNotificationVisibleSeconds.SettingChanged += delegate
            {
                ApplyCharacterPointsVisibility(
                    _activeCharacterPointsView);
            };
            _levelNotificationFadeSeconds.SettingChanged += delegate
            {
                ApplyCharacterPointsVisibility(
                    _activeCharacterPointsView);
            };
            _hideGameplayHudInQuickUseWheel.SettingChanged += delegate
            {
                ApplyQuickUseWheelHudVisibility();
            };
            SubscribeHudSetting(_healthPotionSelectionMode, HudLayoutDirty.QuickSlotContent);
            SubscribeHudSetting(_manaPotionSelectionMode, HudLayoutDirty.QuickSlotContent);
        }

        private static ConfigDescription UiDescription(
            string description,
            string displaySection,
            string displayName,
            int sectionOrder,
            int order,
            AcceptableValueBase acceptableValues = null,
            string choiceLabels = "",
            bool hidden = false)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new FoASettingUiMetadata
                {
                    DisplaySection = displaySection,
                    DisplayName = displayName,
                    ChoiceLabels = choiceLabels,
                    SectionOrder = sectionOrder,
                    Order = order,
                    Hidden = hidden
                });
        }

        private void SubscribeHudSetting<T>(ConfigEntry<T> entry, HudLayoutDirty dirtyLayout)
        {
            entry.SettingChanged += delegate
            {
                MarkHudLayoutDirty(dirtyLayout);
            };
        }

        private void MarkHudLayoutDirty(HudLayoutDirty dirtyLayout)
        {
            _dirtyHudLayout |= dirtyLayout;
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

            CapturePreservedUserSettings(
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
                Logger.LogInfo(
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
            catch (Exception exception)
            {
                ClearPendingPreservedUserSettings();

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
                        "Could not restore the previous Glorious UI config after schema reset failure: "
                        + restoreException.GetBaseException().Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Glorious UI config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedUserSettings(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedUserSettings();
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

                if (string.Equals(currentSection, "2. HUD", StringComparison.Ordinal)
                    && IsPreservedHudTuningSetting(settingName))
                {
                    float parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedHudTuning[settingName] = parsedValue;
                    }

                    continue;
                }

                bool potionHotkeySection = string.Equals(
                    currentSection,
                    "3. Hotkeys",
                    StringComparison.Ordinal);
                bool equipmentHotkeySection = string.Equals(
                    currentSection,
                    "5. Equipment Panel",
                    StringComparison.Ordinal);
                if ((potionHotkeySection
                        || equipmentHotkeySection)
                    && IsPreservedHotkeySetting(settingName))
                {
                    KeyCode parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue)
                        && Enum.IsDefined(typeof(KeyCode), parsedValue))
                    {
                        _pendingPreservedHotkeys[settingName] = parsedValue;
                    }
                    else
                    {
                        _pendingPreservedInvalidValueCount++;
                    }
                }
            }
        }

        private static bool IsPreservedHudTuningSetting(string settingName)
        {
            switch (settingName)
            {
                case "QuickSlotHudOffsetX":
                case "QuickSlotHudOffsetY":
                case "QuickSlotHudScale":
                case "HeroHudOffsetX":
                case "HeroHudOffsetY":
                case "HeroHudScale":
                case "HeroHudVisibleSeconds":
                case "ArrowSlotOffsetX":
                case "ArrowSlotOffsetY":
                case "ArrowSlotScale":
                case "WyrdSkillIndicatorOffsetX":
                case "WyrdSkillIndicatorOffsetY":
                case "WyrdSkillIndicatorScale":
                case "BuffDebuffHudOffsetX":
                case "BuffDebuffHudOffsetY":
                case "BuffDebuffHudScale":
                case "BuffDebuffSpacingScale":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPreservedHotkeySetting(
            string settingName)
        {
            if (string.Equals(
                    settingName,
                    "SmartInventoryBagHotkey",
                    StringComparison.Ordinal)
                || string.Equals(
                    settingName,
                    "HealthPotionHotkey",
                    StringComparison.Ordinal)
                || string.Equals(
                    settingName,
                    "ManaPotionHotkey",
                    StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = VanillaWeaponLoadoutActionCount + 1;
                i <= EquipmentWeaponLoadoutCount;
                i++)
            {
                if (string.Equals(
                    settingName,
                    "WeaponLoadout"
                        + i.ToString(CultureInfo.InvariantCulture)
                        + "Hotkey",
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            for (int i = 1; i <= EquipmentQuickSlotCount; i++)
            {
                if (string.Equals(
                    settingName,
                    "QuickSlot"
                        + i.ToString(CultureInfo.InvariantCulture)
                        + "Hotkey",
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RestorePreservedUserSettings()
        {
            if (_pendingPreservedHudTuning.Count == 0
                && _pendingPreservedHotkeys.Count == 0
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            int restoredHudTuning = 0;
            int restoredHotkeys = 0;
            int clampedValues = 0;
            RestorePreservedFloat(
                "QuickSlotHudOffsetX",
                _quickSlotHudOffsetX,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "QuickSlotHudOffsetY",
                _quickSlotHudOffsetY,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "QuickSlotHudScale",
                _quickSlotHudScale,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "HeroHudOffsetX",
                _heroHudOffsetX,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "HeroHudOffsetY",
                _heroHudOffsetY,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "HeroHudScale",
                _heroHudScale,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "HeroHudVisibleSeconds",
                _heroHudVisibleSeconds,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "ArrowSlotOffsetX",
                _arrowSlotOffsetX,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "ArrowSlotOffsetY",
                _arrowSlotOffsetY,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "ArrowSlotScale",
                _arrowSlotScale,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "WyrdSkillIndicatorOffsetX",
                _wyrdSkillIndicatorOffsetX,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "WyrdSkillIndicatorOffsetY",
                _wyrdSkillIndicatorOffsetY,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "WyrdSkillIndicatorScale",
                _wyrdSkillIndicatorScale,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "BuffDebuffHudOffsetX",
                _buffDebuffHudOffsetX,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "BuffDebuffHudOffsetY",
                _buffDebuffHudOffsetY,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "BuffDebuffHudScale",
                _buffDebuffHudScale,
                ref restoredHudTuning,
                ref clampedValues);
            RestorePreservedFloat(
                "BuffDebuffSpacingScale",
                _buffDebuffSpacingScale,
                ref restoredHudTuning,
                ref clampedValues);

            RestorePreservedHotkey(
                "SmartInventoryBagHotkey",
                _smartInventoryBagHotkey,
                ref restoredHotkeys);
            RestorePreservedHotkey(
                "HealthPotionHotkey",
                _healthPotionHotkey,
                ref restoredHotkeys);
            RestorePreservedHotkey(
                "ManaPotionHotkey",
                _manaPotionHotkey,
                ref restoredHotkeys);
            for (int i = 0;
                i < _extendedWeaponLoadoutHotkeys.Length;
                i++)
            {
                int slot = i
                    + VanillaWeaponLoadoutActionCount
                    + 1;
                RestorePreservedHotkey(
                    "WeaponLoadout"
                        + slot.ToString(
                            CultureInfo.InvariantCulture)
                        + "Hotkey",
                    _extendedWeaponLoadoutHotkeys[i],
                    ref restoredHotkeys);
            }
            for (int i = 0; i < _equipmentQuickSlotHotkeys.Length; i++)
            {
                RestorePreservedHotkey(
                    "QuickSlot"
                        + (i + 1).ToString(
                            CultureInfo.InvariantCulture)
                        + "Hotkey",
                    _equipmentQuickSlotHotkeys[i],
                    ref restoredHotkeys);
            }

            Logger.LogInfo(
                "Preserved "
                + restoredHudTuning.ToString(CultureInfo.InvariantCulture)
                + " HUD tuning value(s) and "
                + restoredHotkeys.ToString(CultureInfo.InvariantCulture)
                + " hotkey(s) across the config schema reset; clamped="
                + clampedValues.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + _pendingPreservedInvalidValueCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            ClearPendingPreservedUserSettings();
        }

        private void RestorePreservedFloat(
            string settingName,
            ConfigEntry<float> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            float preservedValue;
            if (entry == null
                || !_pendingPreservedHudTuning.TryGetValue(
                    settingName,
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

        private void RestorePreservedHotkey(
            string settingName,
            ConfigEntry<KeyCode> entry,
            ref int restoredCount)
        {
            KeyCode preservedValue;
            if (entry == null
                || !_pendingPreservedHotkeys.TryGetValue(
                    settingName,
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

        private void ClearPendingPreservedUserSettings()
        {
            _pendingPreservedHudTuning.Clear();
            _pendingPreservedHotkeys.Clear();
            _pendingPreservedInvalidValueCount = 0;
        }

        private void CacheGameAccessors()
        {
            _heroType = RequireType(HeroTypeName);
            _heroItemsType = RequireType(HeroItemsTypeName);
            _itemType = RequireType(ItemTypeName);
            _equipmentSlotType = RequireType(EquipmentSlotTypeName);
            _characterInventoryType = RequireType(ICharacterInventoryTypeName);
            _iLoadoutType = RequireType(ILoadoutTypeName);

            Type heroItemsEventsType = RequireType(HeroItemsEventsTypeName);
            Type inventoryExtensionType = RequireType(CharacterInventoryExtensionTypeName);
            Type itemVariableAccessorType = RequireType(ItemVariableAccessorTypeName);
            Type modelExtensionsType = RequireType(ModelExtensionsTypeName);
            Type selectedQuickSlotViewType = RequireType(SelectedQuickSlotViewTypeName);
            _arButtonType = AccessTools.TypeByName(
                "Awaken.TG.Main.UI.Components.ARButton");
            if (_arButtonType != null)
            {
                _arButtonTargetGraphicProperty =
                    AccessTools.Property(
                        _arButtonType,
                        "TargetGraphic");
                _arButtonTransitionProperty =
                    AccessTools.Property(
                        _arButtonType,
                        "Transition");
                _arButtonHasGraphicField =
                    AccessTools.Field(
                        _arButtonType,
                        "_hasGraphic");
                _arButtonOnClickEvent =
                    _arButtonType.GetEvent(
                        "OnClick",
                        BindingFlags.Public
                        | BindingFlags.Instance);
                _arButtonOnHoverEvent =
                    _arButtonType.GetEvent(
                        "OnHover",
                        BindingFlags.Public
                        | BindingFlags.Instance);
                _arButtonOnSelectedEvent =
                    _arButtonType.GetEvent(
                        "OnSelected",
                        BindingFlags.Public
                        | BindingFlags.Instance);
                if (_arButtonTargetGraphicProperty == null
                    || _arButtonTransitionProperty == null
                    || _arButtonHasGraphicField == null
                    || _arButtonOnClickEvent == null)
                {
                    _arButtonType = null;
                }
            }
            if (_arButtonType == null)
            {
                Logger.LogWarning(
                    "Glorious's native Equipment-panel input component is unavailable. Vanilla Equipment controls will remain visible.");
            }

            _heroCurrentProperty = _heroType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            _selectedQuickSlotTypeProperty = RequireProperty(_heroItemsType, "SelectedQuickSlotType");
            _foodQuickSlotField = RequireField(_equipmentSlotType, "FoodQuickSlot");
            _quickSlot2Field = RequireField(_equipmentSlotType, "QuickSlot2");
            _quickSlot3Field = RequireField(_equipmentSlotType, "QuickSlot3");
            _quickSlotUsedEventField = RequireField(heroItemsEventsType, "QuickSlotUsed");
            _itemIconField = AccessTools.Field(selectedQuickSlotViewType, "itemIcon");
            _nextItemIconsField = RequireField(selectedQuickSlotViewType, "nextItemIcons");
            _useStaticPromptField = RequireField(selectedQuickSlotViewType, "useStaticPrompt");
            _nextStaticPromptField = RequireField(selectedQuickSlotViewType, "nextStaticPrompt");
            CacheHeroHudAccessors();

            _foodQuickSlot = _foodQuickSlotField.GetValue(null);
            _quickSlot2 = _quickSlot2Field.GetValue(null);
            _quickSlot3 = _quickSlot3Field.GetValue(null);

            _selectQuickSlotMethod = RequireMethod(
                _heroItemsType,
                "SelectQuickSlot",
                new[] { _equipmentSlotType });
            _equippedItemMethod = RequireMethod(
                inventoryExtensionType,
                "EquippedItem",
                new[] { _characterInventoryType, _equipmentSlotType });
            _equipMethod = RequireMethod(
                inventoryExtensionType,
                "Equip",
                new[] { _characterInventoryType, _itemType, _equipmentSlotType, _iLoadoutType });
            _itemUseMethod = RequireMethod(_itemType, "Use", Type.EmptyTypes);
            _getVariableMethod = AccessTools.Method(_itemType, "GetVariable");
            _getHealValueMethod = RequireMethod(itemVariableAccessorType, "GetHealValue", new[] { _itemType });
            _triggerQuickSlotUsedMethod = ResolveTriggerMethod(modelExtensionsType);
            _oneMenuTargetSlotField = AccessTools.Field(
                typeof(EquipmentChooseUI),
                "_targetSlot");
            _oneMenuHoveredItemsChangedMethod = AccessTools.Method(
                typeof(EquipmentChooseUI),
                "HoveredItemsChanged",
                new[] { typeof(Item) });
            _oneMenuBagRefreshPromptsMethod = AccessTools.Method(
                typeof(BagUI),
                "RefreshPrompts",
                new[] { typeof(Item) });
            _quickLoadoutMiddlePointField =
                AccessTools.Field(
                    typeof(VCQuickLoadout),
                    "middlePoint");
            _quickLoadoutPrimarySlotField =
                AccessTools.Field(
                    typeof(VCQuickLoadout),
                    "primarySlot");
            _quickLoadoutSecondarySlotField =
                AccessTools.Field(
                    typeof(VCQuickLoadout),
                    "secondarySlot");
            _quickLoadoutPrimaryRectField =
                AccessTools.Field(
                    typeof(VCQuickLoadout),
                    "primarySlotRectTransform");
            _quickLoadoutSecondaryRectField =
                AccessTools.Field(
                    typeof(VCQuickLoadout),
                    "secondarySlotRectTransform");
            _quickLoadoutPrimarySelectedField =
                AccessTools.Field(
                    typeof(VCQuickLoadout),
                    "primarySelectedIndicator");
            _quickLoadoutSecondarySelectedField =
                AccessTools.Field(
                    typeof(VCQuickLoadout),
                    "secondarySelectedIndicator");
            _radialOptionInitialPositionField =
                AccessTools.Field(
                    typeof(VCRadialMenuOption<QuickUseWheelUI>),
                    "_initialPosition");
            _quickUseOptionChosenIndicatorField =
                AccessTools.Field(
                    typeof(VCQuickUseOption),
                    "chosenIndicator");
            _accessorsReady = true;
        }

        private void ResolveEyesInTheDarkIntegration()
        {
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                    EyesInTheDarkPluginGuid,
                    out var pluginInfo))
            {
                return;
            }

            try
            {
                Assembly assembly = pluginInfo.Instance == null
                    ? null
                    : pluginInfo.Instance.GetType().Assembly;
                Type apiType = assembly == null
                    ? null
                    : assembly.GetType(
                        EyesInTheDarkHudApiTypeName,
                        false);
                PropertyInfo contractVersionProperty = apiType == null
                    ? null
                    : apiType.GetProperty(
                        "ContractVersion",
                        BindingFlags.Public | BindingFlags.Static);
                object contractVersionValue = contractVersionProperty == null
                    ? null
                    : contractVersionProperty.GetValue(null, null);
                MethodInfo requestMethod = apiType == null
                    ? null
                    : apiType.GetMethod(
                        "RequestBelowVanillaBars",
                        BindingFlags.Public | BindingFlags.Static);
                ParameterInfo[] parameters = requestMethod == null
                    ? new ParameterInfo[0]
                    : requestMethod.GetParameters();
                if (!(contractVersionValue is int)
                    || (int)contractVersionValue < 1
                    || requestMethod == null
                    || requestMethod.ReturnType != typeof(bool)
                    || parameters.Length != 2
                    || parameters[0].ParameterType != typeof(string)
                    || parameters[1].ParameterType != typeof(bool))
                {
                    Logger.LogWarning(
                        "Eyes in the Dark is loaded, but its HUD placement contract is unavailable. Its threat meter will keep its standalone position.");
                    return;
                }

                _eyesInTheDarkPlacementRequest = requestMethod;
                UpdateEyesInTheDarkPlacementRequest();
                if (_eyesInTheDarkPlacementRequest != null)
                {
                    Logger.LogInfo(
                        "Eyes in the Dark detected. It owns the Wyrd Threat meter; Glorious only supplies its enabled-state placement request.");
                }
            }
            catch (Exception exception)
            {
                DisableEyesInTheDarkPlacementBridge(exception);
            }
        }

        private void UpdateEyesInTheDarkPlacementRequest()
        {
            if (_eyesInTheDarkPlacementRequest == null)
            {
                return;
            }

            bool placeBelow = IsEnabled();
            if (_lastEyesInTheDarkPlacementRequest.HasValue
                && _lastEyesInTheDarkPlacementRequest.Value == placeBelow)
            {
                return;
            }

            try
            {
                object accepted = _eyesInTheDarkPlacementRequest.Invoke(
                    null,
                    new object[] { PluginGuid, placeBelow });
                if (!(accepted is bool) || !(bool)accepted)
                {
                    throw new InvalidOperationException(
                        "Eyes in the Dark rejected the placement request");
                }

                _lastEyesInTheDarkPlacementRequest = placeBelow;
            }
            catch (Exception exception)
            {
                DisableEyesInTheDarkPlacementBridge(exception);
            }
        }

        private void ReleaseEyesInTheDarkPlacementRequest()
        {
            MethodInfo requestMethod = _eyesInTheDarkPlacementRequest;
            _eyesInTheDarkPlacementRequest = null;
            _lastEyesInTheDarkPlacementRequest = null;
            if (requestMethod == null)
            {
                return;
            }

            try
            {
                requestMethod.Invoke(
                    null,
                    new object[] { PluginGuid, false });
            }
            catch (Exception exception)
            {
                if (!_eyesInTheDarkBridgeFailureLogged)
                {
                    _eyesInTheDarkBridgeFailureLogged = true;
                    Logger.LogWarning(
                        "Could not release the Eyes in the Dark HUD placement request: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void DisableEyesInTheDarkPlacementBridge(
            Exception exception)
        {
            _eyesInTheDarkPlacementRequest = null;
            _lastEyesInTheDarkPlacementRequest = null;
            if (_eyesInTheDarkBridgeFailureLogged)
            {
                return;
            }

            _eyesInTheDarkBridgeFailureLogged = true;
            Logger.LogWarning(
                "The optional Eyes in the Dark HUD placement bridge is unavailable; its threat meter will keep its standalone position: "
                + exception.GetBaseException().Message);
        }

        private void CacheHeroHudAccessors()
        {
            _heroHudViewType = AccessTools.TypeByName(HeroHudViewTypeName);
            _heroHudBarType = AccessTools.TypeByName(HeroHudBarTypeName);
            _wyrdSkillBarType = AccessTools.TypeByName(WyrdSkillBarTypeName);
            if (_heroHudViewType == null)
            {
                return;
            }

            _heroHudSelectedQuickSlotField = AccessTools.Field(_heroHudViewType, "selectedQuickSlot");
            _heroHudArrowsImageField = AccessTools.Field(_heroHudViewType, "arrowsImage");
            _heroHudArrowsCounterField = AccessTools.Field(_heroHudViewType, "arrowsCounter");
            _heroHudHeroBarsTransformField = AccessTools.Field(_heroHudViewType, "heroBarsTransform");
            _heroHudShowTimerField = AccessTools.Field(_heroHudViewType, "_showHUDTimer");
            _heroHudRefreshedLastlyField = AccessTools.Field(_heroHudViewType, "_hudRefreshedLastly");
            _heroHudUpdateCanvasGroupsMethod = AccessTools.Method(
                _heroHudViewType,
                "UpdateCanvasGroups");

            Type compassViewType = AccessTools.TypeByName(CompassViewTypeName);
            _compassCanvasGroupField = compassViewType == null
                ? null
                : AccessTools.Field(compassViewType, "_compass");

            Type characterPointsViewType =
                AccessTools.TypeByName(CharacterPointsViewTypeName);
            _characterPointsCanvasGroupField = characterPointsViewType == null
                ? null
                : AccessTools.Field(characterPointsViewType, "canvasGroup");
            _characterPointsUpdateVisualMethod = characterPointsViewType == null
                ? null
                : AccessTools.Method(characterPointsViewType, "UpdateVisual");
        }

        private bool PatchGame()
        {
            _harmony = new Harmony(PluginGuid);
            Type questNotificationDataType =
                AccessTools.TypeByName(QuestNotificationDataTypeName);
            PatchMethod(
                questNotificationDataType,
                "get_VisibilityDuration",
                typeof(QuestNotificationDurationPatch),
                nameof(QuestNotificationDurationPatch.Postfix),
                false);
            Type objectiveNotificationDataType =
                AccessTools.TypeByName(ObjectiveNotificationDataTypeName);
            PatchMethod(
                objectiveNotificationDataType,
                "get_VisibilityDuration",
                typeof(QuestNotificationDurationPatch),
                nameof(QuestNotificationDurationPatch.Postfix),
                false);
            Type selectedQuickSlotViewType = AccessTools.TypeByName(SelectedQuickSlotViewTypeName);
            PatchMethod(
                selectedQuickSlotViewType,
                "OnAttach",
                typeof(SelectedQuickSlotOnAttachPatch),
                nameof(SelectedQuickSlotOnAttachPatch.Postfix),
                false);
            bool requiredPatched = PatchMethod(
                selectedQuickSlotViewType,
                "UpdateIcon",
                typeof(SelectedQuickSlotUpdateIconPatch),
                nameof(SelectedQuickSlotUpdateIconPatch.Postfix),
                true);
            PatchMethod(
                selectedQuickSlotViewType,
                "OnDiscard",
                typeof(SelectedQuickSlotOnDiscardPatch),
                nameof(SelectedQuickSlotOnDiscardPatch.Postfix),
                false);

            Type heroKeysType = AccessTools.TypeByName(
                "Awaken.TG.Main.Heroes.VHeroKeys");
            PatchMethod(
                heroKeysType,
                "EquipLoadout",
                typeof(HeroKeysEquipLoadoutPatch),
                nameof(HeroKeysEquipLoadoutPatch.Prefix),
                true,
                true);
            PatchMethod(
                heroKeysType,
                "Handle",
                typeof(SmartInventoryBagActionPatch),
                nameof(SmartInventoryBagActionPatch.Prefix),
                false,
                true);
            PatchMethod(
                heroKeysType,
                "Handle",
                typeof(SmartInventoryBagActionPatch),
                nameof(SmartInventoryBagActionPatch.Postfix),
                false);
            PatchMethod(
                typeof(CharacterSheetTabs),
                "OnHandle",
                typeof(SmartInventoryBagActionPatch),
                nameof(SmartInventoryBagActionPatch.Prefix),
                false,
                true);
            PatchMethod(
                typeof(CharacterSheetTabs),
                "OnHandle",
                typeof(SmartInventoryBagActionPatch),
                nameof(SmartInventoryBagActionPatch.Postfix),
                false);
            PatchMethod(
                typeof(Story),
                "Handle",
                typeof(SmartInventoryBagActionPatch),
                nameof(SmartInventoryBagActionPatch.Prefix),
                false,
                true);
            PatchMethod(
                typeof(Story),
                "Handle",
                typeof(SmartInventoryBagActionPatch),
                nameof(SmartInventoryBagActionPatch.Postfix),
                false);
            PatchMethod(
                typeof(Prompt),
                "Hold",
                typeof(HeldApplyChangesPromptPatch),
                nameof(HeldApplyChangesPromptPatch.Prefix),
                false,
                true);
            PatchMethod(
                typeof(Prompt),
                "Tap",
                typeof(RestoreDefaultsPromptPatch),
                nameof(RestoreDefaultsPromptPatch.Prefix),
                false,
                true);

            Type heroHudViewType = _heroHudViewType ?? AccessTools.TypeByName(HeroHudViewTypeName);
            PatchMethod(
                heroHudViewType,
                "AfterFullyInitialized",
                typeof(HeroHudAfterFullyInitializedPatch),
                nameof(HeroHudAfterFullyInitializedPatch.Postfix),
                false);
            PatchMethod(
                heroHudViewType,
                "OnDiscard",
                typeof(HeroHudOnDiscardPatch),
                nameof(HeroHudOnDiscardPatch.Postfix),
                false);
            PatchMethod(
                heroHudViewType,
                "UpdateHeroBarsScale",
                typeof(HeroHudUpdateHeroBarsScalePatch),
                nameof(HeroHudUpdateHeroBarsScalePatch.Postfix),
                false);
            PatchMethod(
                heroHudViewType,
                "HandleShowHUDTimer",
                typeof(HeroHudHandleShowHudTimerPatch),
                nameof(HeroHudHandleShowHudTimerPatch.Prefix),
                false,
                true);
            PatchMethod(
                heroHudViewType,
                "ResetTimer",
                typeof(HeroHudResetTimerPatch),
                nameof(HeroHudResetTimerPatch.Postfix),
                false);
            PatchMethod(
                heroHudViewType,
                "InitShowHUDTimer",
                typeof(HeroHudInitShowHudTimerPatch),
                nameof(HeroHudInitShowHudTimerPatch.Postfix),
                false);

            Type heroStatusHudType =
                AccessTools.TypeByName(HeroStatusHudTypeName);
            PatchMethod(
                heroStatusHudType,
                "OnAttach",
                typeof(HeroStatusHudOnAttachPatch),
                nameof(HeroStatusHudOnAttachPatch.Postfix),
                false);
            PatchMethod(
                heroStatusHudType,
                "OnDiscard",
                typeof(HeroStatusHudOnDiscardPatch),
                nameof(HeroStatusHudOnDiscardPatch.Postfix),
                false);

            Type statusHudType =
                AccessTools.TypeByName(StatusHudTypeName);
            PatchMethod(
                statusHudType,
                "UpdateStatusView",
                typeof(StatusHudUpdateStatusViewPatch),
                nameof(StatusHudUpdateStatusViewPatch.Postfix),
                false);

            Type compassViewType = AccessTools.TypeByName(CompassViewTypeName);
            PatchMethod(
                compassViewType,
                "OnInitialize",
                typeof(CompassOnInitializePatch),
                nameof(CompassOnInitializePatch.Postfix),
                false);
            PatchMethod(
                compassViewType,
                "OnUIStateChanged",
                typeof(CompassOnUiStateChangedPatch),
                nameof(CompassOnUiStateChangedPatch.Postfix),
                false);

            Type characterPointsViewType =
                AccessTools.TypeByName(CharacterPointsViewTypeName);
            PatchMethod(
                characterPointsViewType,
                "OnAttach",
                typeof(CharacterPointsOnAttachPatch),
                nameof(CharacterPointsOnAttachPatch.Postfix),
                false);
            PatchMethod(
                characterPointsViewType,
                "UpdateVisual",
                typeof(CharacterPointsUpdateVisualPatch),
                nameof(CharacterPointsUpdateVisualPatch.Prefix),
                false,
                true);
            PatchMethod(
                characterPointsViewType,
                "UpdateVisual",
                typeof(CharacterPointsUpdateVisualPatch),
                nameof(CharacterPointsUpdateVisualPatch.Postfix),
                false);
            PatchMethod(
                characterPointsViewType,
                "OnUIStateChanged",
                typeof(CharacterPointsOnUiStateChangedPatch),
                nameof(CharacterPointsOnUiStateChangedPatch.Prefix),
                false,
                true);
            PatchMethod(
                characterPointsViewType,
                "OnDestroy",
                typeof(CharacterPointsOnDestroyPatch),
                nameof(CharacterPointsOnDestroyPatch.Postfix),
                false);

            Type quickUseWheelViewType =
                AccessTools.TypeByName(QuickUseWheelViewTypeName);
            PatchMethod(
                quickUseWheelViewType,
                "OnInitialize",
                typeof(QuickUseWheelOnInitializePatch),
                nameof(QuickUseWheelOnInitializePatch.Postfix),
                false);
            PatchMethod(
                quickUseWheelViewType,
                "OnDiscard",
                typeof(QuickUseWheelOnDiscardPatch),
                nameof(QuickUseWheelOnDiscardPatch.Postfix),
                false);
            PatchMethod(
                typeof(VCQuickLoadout),
                "Refresh",
                typeof(QuickWheelLoadoutRefreshPatch),
                nameof(QuickWheelLoadoutRefreshPatch.Prefix),
                false,
                true);
            PatchMethod(
                typeof(VCQuickLoadout),
                "OnShow",
                typeof(QuickWheelLoadoutOnShowPatch),
                nameof(QuickWheelLoadoutOnShowPatch.Prefix),
                false,
                true);
            PatchMethod(
                typeof(VCQuickLoadout),
                "OnSelect",
                typeof(QuickWheelLoadoutOnSelectPatch),
                nameof(QuickWheelLoadoutOnSelectPatch.Prefix),
                false,
                true);
            PatchMethod(
                typeof(VCQuickUseOption),
                "OnHoverStart",
                typeof(QuickWheelOptionHoverStartPatch),
                nameof(
                    QuickWheelOptionHoverStartPatch.Postfix),
                false);
            PatchMethod(
                typeof(VCQuickUseOption),
                "OnHoverEnd",
                typeof(QuickWheelOptionHoverEndPatch),
                nameof(
                    QuickWheelOptionHoverEndPatch.Postfix),
                false);
            PatchExactMethod(
                AccessTools.PropertyGetter(
                    typeof(VCQuickLoadout),
                    "Description"),
                typeof(QuickWheelLoadoutDescriptionPatch),
                nameof(QuickWheelLoadoutDescriptionPatch.Prefix),
                true);
            PatchExactMethod(
                AccessTools.Method(
                    typeof(VQuickUseWheelUI),
                    "InitialOptionFrom",
                    new[]
                    {
                        typeof(
                            VCRadialMenuOption<QuickUseWheelUI>[])
                    }),
                typeof(QuickWheelInitialOptionPatch),
                nameof(QuickWheelInitialOptionPatch.Prefix),
                true);

            Type loadoutsViewType =
                AccessTools.TypeByName(LoadoutsViewTypeName);
            PatchMethod(
                loadoutsViewType,
                "OnInitialize",
                typeof(LoadoutsViewOnInitializePatch),
                nameof(LoadoutsViewOnInitializePatch.Postfix),
                false);
            PatchMethod(
                loadoutsViewType,
                "OnDiscard",
                typeof(LoadoutsViewOnDiscardPatch),
                nameof(LoadoutsViewOnDiscardPatch.Postfix),
                false);

            Type loadoutSlotViewType =
                AccessTools.TypeByName(LoadoutSlotViewTypeName);
            PatchMethod(
                loadoutSlotViewType,
                "Equip",
                typeof(WeaponLoadoutSlotEquipPatch),
                nameof(WeaponLoadoutSlotEquipPatch.Postfix),
                false);
            PatchMethod(
                loadoutSlotViewType,
                "Unequip",
                typeof(WeaponLoadoutSlotUnequipPatch),
                nameof(WeaponLoadoutSlotUnequipPatch.Postfix),
                false);

            Type equipmentViewType =
                AccessTools.TypeByName(EquipmentViewTypeName);
            PatchMethod(
                equipmentViewType,
                "OnInitialize",
                typeof(EquipmentViewOnInitializePatch),
                nameof(EquipmentViewOnInitializePatch.Postfix),
                false);
            PatchMethod(
                equipmentViewType,
                "OnDiscard",
                typeof(EquipmentViewOnDiscardPatch),
                nameof(EquipmentViewOnDiscardPatch.Postfix),
                false);

            Type equipmentSlotViewType =
                AccessTools.TypeByName(EquipmentSlotViewTypeName);
            PatchMethod(
                equipmentSlotViewType,
                "Equip",
                typeof(EquipmentSlotEquipPatch),
                nameof(EquipmentSlotEquipPatch.Postfix),
                false);
            PatchMethod(
                equipmentSlotViewType,
                "Unequip",
                typeof(EquipmentSlotUnequipPatch),
                nameof(EquipmentSlotUnequipPatch.Postfix),
                false);

            PatchMethod(
                typeof(EquipmentChooseUI),
                "OnBeforeDiscard",
                typeof(EquipmentChooseUiTooltipCleanupPatch),
                nameof(EquipmentChooseUiTooltipCleanupPatch.Prefix),
                false,
                true);

            PatchEquipmentPersistenceHooks();
            return requiredPatched;
        }

        private void PatchOneMenuEquipHooks()
        {
            if (_oneMenuHooksInitialized)
            {
                return;
            }

            _oneMenuHooksInitialized = true;
            _externalOneMenuEquipDetected =
                IsExternalOneMenuEquipLoaded();
            if (_externalOneMenuEquipDetected)
            {
                Logger.LogWarning(
                    "The standalone Equip both hands from one menu plugin is active. Glorious's duplicate one-menu equip hooks are disabled for this session; disable owrocc.OneMenuEquip to use Glorious's standalone implementation.");
                return;
            }

            PatchMethod(
                typeof(EquipmentChooseUI),
                "SelectCurrent",
                typeof(OneMenuSelectCurrentPatch),
                nameof(OneMenuSelectCurrentPatch.Prefix),
                false,
                true);
            PatchExactMethod(
                AccessTools.Method(
                    typeof(EquipmentChooseUI),
                    "UnequipItem",
                    Type.EmptyTypes),
                typeof(OneMenuUnequipItemPatch),
                nameof(OneMenuUnequipItemPatch.Prefix),
                true);
            PatchMethod(
                typeof(EquipmentChooseUI),
                "HoveredItemsChanged",
                typeof(OneMenuHoveredItemsChangedPatch),
                nameof(OneMenuHoveredItemsChangedPatch.Postfix),
                false);
            PatchMethod(
                typeof(EquipmentChooseUI),
                "OnBeforeDiscard",
                typeof(OneMenuChooseUiDiscardPatch),
                nameof(OneMenuChooseUiDiscardPatch.Prefix),
                false,
                true);
            PatchExactMethod(
                AccessTools.Method(
                    typeof(BagUI),
                    "UseItem",
                    Type.EmptyTypes),
                typeof(OneMenuBagUseItemPatch),
                nameof(OneMenuBagUseItemPatch.Prefix),
                true);
            PatchExactMethod(
                AccessTools.Method(
                    typeof(BagUI),
                    "RefreshPrompts",
                    new[] { typeof(Item) }),
                typeof(OneMenuBagRefreshPromptsPatch),
                nameof(OneMenuBagRefreshPromptsPatch.Postfix),
                false);

            ConstructorInfo chooseConstructor = AccessTools.Constructor(
                typeof(EquipmentChooseUI),
                new[] { typeof(IEquipmentSlot) });
            PatchExactMethod(
                chooseConstructor,
                typeof(OneMenuChooseUiConstructorPatch),
                nameof(OneMenuChooseUiConstructorPatch.Prefix),
                true);
        }

        private static bool IsExternalOneMenuEquipLoaded()
        {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(
                OneMenuEquipPluginGuid))
            {
                return true;
            }

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                AssemblyName assemblyName =
                    assemblies[i].GetName();
                if (assemblyName != null
                    && string.Equals(
                        assemblyName.Name,
                        OneMenuEquipPluginGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExternalBagHotkeysLoaded()
        {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(
                BagHotkeysPluginGuid))
            {
                return true;
            }

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                AssemblyName assemblyName =
                    assemblies[i].GetName();
                if (assemblyName != null
                    && string.Equals(
                        assemblyName.Name,
                        BagHotkeysPluginGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void PatchExactMethod(
            MethodBase original,
            Type patchType,
            string patchMethodName,
            bool prefix)
        {
            MethodInfo patch =
                AccessTools.Method(patchType, patchMethodName);
            if (original == null || patch == null)
            {
                WarnPatch(
                    "Could not install the optional one-menu equip hook "
                    + patchMethodName
                    + ".",
                    false);
                return;
            }

            try
            {
                _harmony.Patch(
                    original,
                    prefix ? new HarmonyMethod(patch) : null,
                    prefix ? null : new HarmonyMethod(patch));
            }
            catch (Exception exception)
            {
                WarnPatch(
                    "Could not install the optional one-menu equip hook "
                    + patchMethodName
                    + ": "
                    + exception.GetBaseException().Message,
                    false);
            }
        }

        private void PatchEquipmentPersistenceHooks()
        {
            Type newGameLoadingType = AccessTools.TypeByName(
                "Awaken.TG.Main.UI.TitleScreen.Loading.LoadingTypes.NewGameLoading");
            PatchDeclaredMethod(
                newGameLoadingType,
                "DropPreviousDomains",
                typeof(NewGameDropPreviousDomainsPatch),
                nameof(NewGameDropPreviousDomainsPatch.Prefix),
                true);

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
                PatchDeclaredMethod(
                    type,
                    "EndLoadSlot",
                    typeof(CloudServiceEndLoadSlotPatch),
                    nameof(CloudServiceEndLoadSlotPatch.Prefix),
                    true);
                PatchDeclaredMethod(
                    type,
                    "EndSave",
                    typeof(CloudServiceEndSavePatch),
                    nameof(CloudServiceEndSavePatch.Prefix),
                    true);
            }
        }

        private void ClearStandaloneEquipmentSession()
        {
            ForgetEquipmentViews();
            _activeEquipmentSaveSlot = null;
            _virtualWeaponLoadouts.Clear();
            Array.Clear(
                _virtualQuickSlotItemGuids,
                0,
                _virtualQuickSlotItemGuids.Length);
            Array.Clear(
                _virtualQuickSlotItemIds,
                0,
                _virtualQuickSlotItemIds.Length);
            _currentVirtualWeaponSlot = 0;
            _selectedEquipmentQuickSlot = 1;
            _pendingApplyLoadedWeaponSlot = false;
            _pendingEquipmentApplyTime = 0.0f;
            _nextEquipmentBackendTrackTime = 0.0f;
            _lastTrackedWeaponSnapshot = null;
            _hasTrackedWeaponSnapshot = false;
            _nextEquipmentPanelRefreshTime = 0.0f;
            _observedCharacterSheet = null;
            _observedCharacterSheetTab = null;
            _pendingInventorySubTab = null;
            _pendingBagCategory = null;
        }

        private void PatchDeclaredMethod(
            Type declaringType,
            string methodName,
            Type patchType,
            string patchMethodName,
            bool prefix)
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
                    prefix ? new HarmonyMethod(patch) : null,
                    prefix ? null : new HarmonyMethod(patch));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not patch "
                    + declaringType.FullName
                    + "."
                    + methodName
                    + ": "
                    + exception.GetBaseException().Message);
            }
        }

        private bool PatchMethod(
            Type declaringType,
            string methodName,
            Type patchType,
            string patchMethodName,
            bool required,
            bool prefix = false)
        {
            if (declaringType == null)
            {
                WarnPatch("Could not patch " + methodName + " because the declaring type was not found.", required);
                return false;
            }

            MethodInfo original = AccessTools.Method(declaringType, methodName);
            MethodInfo patch = AccessTools.Method(patchType, patchMethodName);
            if (original == null || patch == null)
            {
                WarnPatch("Could not patch " + declaringType.FullName + "." + methodName + ".", required);
                return false;
            }

            try
            {
                if (prefix || string.Equals(
                    patchMethodName,
                    nameof(SelectNextQuickSlotPatch.Prefix),
                    StringComparison.Ordinal))
                {
                    _harmony.Patch(original, new HarmonyMethod(patch), null);
                }
                else
                {
                    _harmony.Patch(original, null, new HarmonyMethod(patch));
                }
            }
            catch (Exception exception)
            {
                WarnPatch(
                    "Could not patch "
                    + declaringType.FullName
                    + "."
                    + methodName
                    + ": "
                    + exception.GetBaseException().GetType().Name
                    + ": "
                    + exception.GetBaseException().Message,
                    required);
                return false;
            }

            LogDiagnostic("Patched " + declaringType.FullName + "." + methodName + ".");
            return true;
        }

        private void WarnPatch(string message, bool required)
        {
            if (required || (_logPatchWarnings != null && _logPatchWarnings.Value))
            {
                Logger.LogWarning(message);
            }

            if (required)
            {
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Required patch unavailable; check BepInEx log.");
            }
        }

        private Type RequireType(string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                throw new TypeLoadException("Could not find " + typeName + ".");
            }

            return type;
        }

        private static FieldInfo RequireField(Type type, string fieldName)
        {
            FieldInfo field = AccessTools.Field(type, fieldName);
            if (field == null)
            {
                throw new MissingFieldException(type.FullName, fieldName);
            }

            return field;
        }

        private static PropertyInfo RequireProperty(Type type, string propertyName)
        {
            PropertyInfo property = AccessTools.Property(type, propertyName);
            if (property == null)
            {
                throw new MissingMemberException(type.FullName, propertyName);
            }

            return property;
        }

        private static MethodInfo RequireMethod(Type type, string methodName, Type[] parameterTypes)
        {
            MethodInfo method = AccessTools.Method(type, methodName, parameterTypes);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            return method;
        }

        private MethodInfo ResolveTriggerMethod(Type modelExtensionsType)
        {
            foreach (MethodInfo method in modelExtensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!string.Equals(method.Name, "Trigger", StringComparison.Ordinal) || !method.IsGenericMethodDefinition)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 3)
                {
                    return method.MakeGenericMethod(_heroType, _heroType);
                }
            }

            throw new MissingMethodException(modelExtensionsType.FullName, "Trigger");
        }

        internal bool ShouldPinFoodSlot()
        {
            return IsEnabled() && _pinHudToFoodSlot != null && _pinHudToFoodSlot.Value && _accessorsReady;
        }

        internal bool ShouldReplaceSmallHudSlots()
        {
            return IsEnabled() && _replaceSmallHudSlots != null && _replaceSmallHudSlots.Value && _accessorsReady;
        }

        internal bool ShouldOwnArrowSlot()
        {
            return IsEnabled() && _ownArrowSlot != null && _ownArrowSlot.Value && _accessorsReady;
        }

        internal bool ShouldControlHeroHud()
        {
            return IsEnabled()
                && _controlHeroHud != null
                && _controlHeroHud.Value
                && _heroHudHeroBarsTransformField != null;
        }

        internal bool ShouldControlHeroHudTimer()
        {
            return ShouldControlHeroHud()
                && _heroHudShowTimerField != null
                && _heroHudRefreshedLastlyField != null
                && _heroHudUpdateCanvasGroupsMethod != null;
        }

        internal bool ShouldOwnWyrdSkillIndicator()
        {
            return IsEnabled()
                && _ownWyrdSkillIndicator != null
                && _ownWyrdSkillIndicator.Value
                && _wyrdSkillBarType != null
                && _accessorsReady;
        }

        internal bool IsEnabled()
        {
            return _enabled != null && _enabled.Value;
        }

        internal bool IsFoodSlot(object slot)
        {
            return ReferenceEquals(slot, _foodQuickSlot) || SlotNameEquals(slot, "FoodQuickSlot");
        }

        internal bool IsManualQuickSlot(object slot)
        {
            return ReferenceEquals(slot, _quickSlot2)
                || ReferenceEquals(slot, _quickSlot3)
                || SlotNameEquals(slot, "QuickSlot2")
                || SlotNameEquals(slot, "QuickSlot3");
        }

        private static bool SlotNameEquals(object slot, string name)
        {
            return slot != null && string.Equals(slot.ToString(), name, StringComparison.Ordinal);
        }

        private void MaintainFoodSelection()
        {
            if (!ShouldPinFoodSlot() || Time.unscaledTime < _nextFoodPinTime)
            {
                return;
            }

            _nextFoodPinTime = Time.unscaledTime + FoodPinIntervalSeconds;

            object hero = GetCurrentHero();
            object heroItems = hero == null ? null : GetPropertyValue(hero, "HeroItems");
            if (heroItems == null)
            {
                return;
            }

            EnsureHeroItemsPatches(heroItems);
            ForceFoodSelected(heroItems);
        }

        private void EnsureHeroItemsPatches(object heroItems)
        {
            if (_heroItemsPatchesAttempted || !ShouldPinFoodSlot() || heroItems == null || _harmony == null)
            {
                return;
            }

            _heroItemsPatchesAttempted = true;
            bool afterInitPatched = PatchMethod(
                _heroItemsType,
                "AfterInit",
                typeof(HeroItemsAfterInitPatch),
                nameof(HeroItemsAfterInitPatch.Postfix),
                false);
            bool nextPatched = PatchMethod(
                _heroItemsType,
                "SelectNextQuickSlot",
                typeof(SelectNextQuickSlotPatch),
                nameof(SelectNextQuickSlotPatch.Prefix),
                false);
            bool selectPatched = PatchMethod(
                _heroItemsType,
                "SelectQuickSlot",
                typeof(SelectQuickSlotPatch),
                nameof(SelectQuickSlotPatch.Prefix),
                false);
            bool equipFoodPatched = PatchMethod(
                _heroItemsType,
                "EquipFood",
                typeof(EquipFoodPatch),
                nameof(EquipFoodPatch.Postfix),
                false);

            _heroItemsPatchesInstalled = afterInitPatched && nextPatched && selectPatched && equipFoodPatched;
            if (_heroItemsPatchesInstalled)
            {
                LogDiagnostic("Deferred HeroItems quick-slot patches installed.");
            }
        }

        internal void ForceFoodSelected(object heroItems)
        {
            if (!ShouldPinFoodSlot() || heroItems == null || _forceSelectingFood)
            {
                return;
            }

            try
            {
                if (IsFoodSlot(GetSelectedQuickSlotType(heroItems)))
                {
                    return;
                }

                _forceSelectingFood = true;
                _selectQuickSlotMethod.Invoke(heroItems, new[] { _foodQuickSlot });
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not force-select the food quick slot: " + exception.GetBaseException().Message);
            }
            finally
            {
                _forceSelectingFood = false;
            }
        }

        private object GetSelectedQuickSlotType(object heroItems)
        {
            if (heroItems == null || _selectedQuickSlotTypeProperty == null)
            {
                return null;
            }

            return _selectedQuickSlotTypeProperty.GetValue(heroItems, null);
        }

        internal void RefreshFoodSlotChoice(object heroItems)
        {
            if (!ShouldPinFoodSlot() || heroItems == null)
            {
                return;
            }

            try
            {
                ForceFoodSelected(heroItems);
                object hero = GetCurrentHero();
                if (hero == null)
                {
                    return;
                }

                object preferredFood = FindSmartConsumable(
                    hero,
                    heroItems,
                    SmartConsumableKind.Food,
                    _foodSelectionMode.Value,
                    true);
                if (preferredFood != null)
                {
                    object currentFood = GetEquippedItem(heroItems, _foodQuickSlot);
                    if (!ReferenceEquals(currentFood, preferredFood))
                    {
                        try
                        {
                            _refreshingFoodSlot = true;
                            EquipItem(heroItems, preferredFood, _foodQuickSlot);
                        }
                        finally
                        {
                            _refreshingFoodSlot = false;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not refresh food quick-slot choice: " + exception.GetBaseException().Message);
            }
        }

        internal void ApplySmartHudIcons(object selectedQuickSlotView)
        {
            if (selectedQuickSlotView == null || !_accessorsReady)
            {
                return;
            }

            try
            {
                ApplyQuickSlotUsePromptVisibility(selectedQuickSlotView);

                Image[] nextIcons = _nextItemIconsField.GetValue(selectedQuickSlotView) as Image[];
                if (nextIcons == null || nextIcons.Length == 0)
                {
                    return;
                }

                ApplyCyclePromptVisibility(selectedQuickSlotView, nextIcons);

                if (!ShouldReplaceSmallHudSlots())
                {
                    ReleaseSmartIcons(nextIcons);
                    return;
                }

                object hero = GetCurrentHero();
                object heroItems = hero == null ? null : GetPropertyValue(hero, "HeroItems");
                if (heroItems == null)
                {
                    HideSmartSlotVanillaIcons(nextIcons);
                    return;
                }

                EnsureHeroItemsPatches(heroItems);
                object healthItem = FindSmartConsumable(
                    hero,
                    heroItems,
                    SmartConsumableKind.HealthPotion,
                    _healthPotionSelectionMode.Value,
                    true);
                object manaItem = FindSmartConsumable(
                    hero,
                    heroItems,
                    SmartConsumableKind.ManaPotion,
                    _manaPotionSelectionMode.Value,
                    true);

                ApplySmartIcon(nextIcons, 0, manaItem);
                ApplySmartIcon(nextIcons, 1, healthItem);

                for (int i = 2; i < nextIcons.Length; i++)
                {
                    ApplySmartIcon(nextIcons, i, null);
                }
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not apply smart quick-slot HUD icons: " + exception.GetBaseException().Message);
            }
        }

        private void ApplyQuickSlotUsePromptVisibility(object selectedQuickSlotView)
        {
            GameObject usePrompt =
                _useStaticPromptField.GetValue(selectedQuickSlotView) as GameObject;
            if (usePrompt == null)
            {
                return;
            }

            if (IsEnabled()
                && _hideQuickSlotUsePrompt != null
                && _hideQuickSlotUsePrompt.Value)
            {
                usePrompt.SetActive(false);
                return;
            }

            Image itemIcon = _itemIconField.GetValue(selectedQuickSlotView) as Image;
            usePrompt.SetActive(itemIcon != null && itemIcon.gameObject.activeSelf);
        }

        private void RestoreQuickSlotUsePromptVisibility(object selectedQuickSlotView)
        {
            if (selectedQuickSlotView == null
                || _useStaticPromptField == null
                || _itemIconField == null)
            {
                return;
            }

            GameObject usePrompt =
                _useStaticPromptField.GetValue(selectedQuickSlotView) as GameObject;
            Image itemIcon = _itemIconField.GetValue(selectedQuickSlotView) as Image;
            if (usePrompt != null)
            {
                usePrompt.SetActive(itemIcon != null && itemIcon.gameObject.activeSelf);
            }
        }

        private void ApplyCyclePromptVisibility(object selectedQuickSlotView, Image[] nextIcons)
        {
            GameObject nextPrompt = _nextStaticPromptField.GetValue(selectedQuickSlotView) as GameObject;
            if (nextPrompt == null)
            {
                return;
            }

            if (IsEnabled() && _hideCyclePrompt != null && _hideCyclePrompt.Value)
            {
                nextPrompt.SetActive(false);
                return;
            }

            bool hasVisibleNextSlot = false;
            for (int i = 0; i < nextIcons.Length; i++)
            {
                Image nextIcon = nextIcons[i];
                if (nextIcon != null && nextIcon.gameObject.activeSelf)
                {
                    hasVisibleNextSlot = true;
                    break;
                }
            }

            nextPrompt.SetActive(hasVisibleNextSlot);
        }

        private void MaintainQuickSlotPromptVisibility()
        {
            if (!IsEnabled()
                || _activeSelectedQuickSlotView == null)
            {
                return;
            }

            if (_hideQuickSlotUsePrompt != null
                && _hideQuickSlotUsePrompt.Value
                && _useStaticPromptField != null)
            {
                GameObject usePrompt =
                    _useStaticPromptField.GetValue(
                        _activeSelectedQuickSlotView)
                        as GameObject;
                if (usePrompt != null && usePrompt.activeSelf)
                {
                    usePrompt.SetActive(false);
                }
            }

            if (_hideCyclePrompt != null
                && _hideCyclePrompt.Value
                && _nextStaticPromptField != null)
            {
                GameObject nextPrompt =
                    _nextStaticPromptField.GetValue(
                        _activeSelectedQuickSlotView)
                        as GameObject;
                if (nextPrompt != null && nextPrompt.activeSelf)
                {
                    nextPrompt.SetActive(false);
                }
            }
        }

        internal void ApplyQuickSlotHudTransform(object selectedQuickSlotView)
        {
            if (selectedQuickSlotView == null)
            {
                return;
            }

            try
            {
                RectTransform rectTransform = GetQuickSlotHudRectTransform(selectedQuickSlotView);
                if (rectTransform == null)
                {
                    return;
                }

                if (!IsEnabled())
                {
                    RestoreHudTransform(rectTransform);
                    return;
                }

                HudTransformSnapshot snapshot = GetOrCreateHudTransformSnapshot(rectTransform);
                float scale = GetQuickSlotHudScale();
                Vector2 anchor = GetAnchorVector(
                    _quickSlotHudAnchor == null
                        ? HudAnchor.BottomCenter
                        : _quickSlotHudAnchor.Value);
                rectTransform.anchorMin = anchor;
                rectTransform.anchorMax = anchor;
                rectTransform.pivot = anchor;
                rectTransform.anchoredPosition = new Vector2(
                    QuickSlotHudBaselineOffsetX + GetQuickSlotHudOffsetX(),
                    QuickSlotHudBaselineOffsetY + GetQuickSlotHudOffsetY());
                rectTransform.localScale = new Vector3(
                    snapshot.LocalScale.x * QuickSlotHudBaselineScale * scale,
                    snapshot.LocalScale.y * QuickSlotHudBaselineScale * scale,
                    snapshot.LocalScale.z);
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not apply quick-slot HUD position or scale: " + exception.GetBaseException().Message);
            }
        }

        internal void ApplyHeroHudTransform(object heroHudView)
        {
            RectTransform rectTransform = GetHeroHudRectTransform(heroHudView);
            if (rectTransform == null)
            {
                return;
            }

            try
            {
                if (!ShouldControlHeroHud())
                {
                    RestoreHudTransform(rectTransform);
                    return;
                }

                HudTransformSnapshot snapshot = GetOrCreateHudTransformSnapshot(rectTransform);
                Vector2 anchor = GetAnchorVector(
                    _heroHudAnchor == null
                        ? HudAnchor.BottomCenter
                        : _heroHudAnchor.Value);
                float scale = GetHeroHudScale();
                rectTransform.anchorMin = anchor;
                rectTransform.anchorMax = anchor;
                rectTransform.pivot = anchor;
                rectTransform.anchoredPosition = new Vector2(
                    HeroHudBaselineOffsetX
                        + (_heroHudOffsetX == null
                            ? 0.0f
                            : _heroHudOffsetX.Value),
                    HeroHudBaselineOffsetY
                        + (_heroHudOffsetY == null
                            ? 0.0f
                            : _heroHudOffsetY.Value));
                rectTransform.localScale = new Vector3(
                    snapshot.LocalScale.x
                        * HeroHudBaselineScale
                        * scale,
                    snapshot.LocalScale.y
                        * HeroHudBaselineScale
                        * scale,
                    snapshot.LocalScale.z);
            }
            catch (Exception exception)
            {
                LogAccessorFailure(
                    "Could not apply Hero HUD position or scale: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void RegisterHeroStatusHud(object statusHud)
        {
            if (!IsHeroStatusHud(statusHud))
            {
                return;
            }

            if (!ReferenceEquals(_activeHeroStatusHud, statusHud))
            {
                ReleaseHeroStatusHud(_activeHeroStatusHud);
                _activeHeroStatusHud = statusHud;
            }

            MarkHudLayoutDirty(HudLayoutDirty.StatusHud);
            ApplyQuickUseWheelHudVisibility();
        }

        internal void ApplyStatusHudLayout(object statusHud)
        {
            RectTransform statusRect = GetStatusHudRectTransform(statusHud);
            if (statusRect == null)
            {
                return;
            }

            try
            {
                if (!ShouldControlBuffDebuffHud())
                {
                    ReleaseStatusHudLayout(statusRect);
                    return;
                }

                StatusHudLayoutSnapshot snapshot =
                    GetOrCreateStatusHudLayoutSnapshot(statusRect);
                Canvas.ForceUpdateCanvases();

                Vector2 cellSize = FindStatusHudCellSize(statusRect, snapshot.BaseCellSize);
                snapshot.BaseCellSize = cellSize;
                EnsureStatusHudTestObjects(statusRect, snapshot);
                ResizeStatusHudTestObjects(cellSize);
                float spacingScale = GetBuffDebuffSpacingScale();
                Vector2 spacing = new Vector2(
                    Mathf.Max(
                        0.0f,
                        (snapshot.BaseSpacing.x
                            + BuffDebuffHudBaselineSpacingAdjustment)
                        * spacingScale),
                    Mathf.Max(
                        0.0f,
                        (snapshot.BaseSpacing.y
                            + BuffDebuffHudBaselineSpacingAdjustment)
                        * spacingScale));
                int iconsPerRow = _buffDebuffIconsPerRow == null
                    ? 9
                    : Mathf.Clamp(_buffDebuffIconsPerRow.Value, 1, 20);
                List<RectTransform> children = GetVisibleStatusChildren(statusRect);

                for (int i = 0; i < children.Count; i++)
                {
                    RectTransform child = children[i];
                    if (!IsStatusHudTestObject(child.gameObject))
                    {
                        snapshot.CaptureChild(child);
                    }

                    int column = i % iconsPerRow;
                    int row = i / iconsPerRow;
                    float childWidth = child.rect.width > 0.01f
                        ? child.rect.width
                        : cellSize.x;
                    float childHeight = child.rect.height > 0.01f
                        ? child.rect.height
                        : cellSize.y;
                    child.anchorMin = Vector2.zero;
                    child.anchorMax = Vector2.zero;
                    Vector2 desiredVisualCenter = new Vector2(
                        snapshot.PaddingLeft
                            + column * (cellSize.x + spacing.x)
                            + cellSize.x * 0.5f,
                        snapshot.PaddingBottom
                            + row * (cellSize.y + spacing.y)
                            + cellSize.y * 0.5f);
                    child.anchoredPosition = new Vector2(
                        snapshot.PaddingLeft
                            + column * (cellSize.x + spacing.x)
                            + child.pivot.x * childWidth,
                        snapshot.PaddingBottom
                            + row * (cellSize.y + spacing.y)
                            + child.pivot.y * childHeight);
                    AlignStatusHudPrimaryImage(
                        statusRect,
                        child,
                        desiredVisualCenter);
                }

                int visibleCount = children.Count;
                int columns = visibleCount == 0
                    ? 1
                    : Mathf.Min(iconsPerRow, visibleCount);
                int rows = Mathf.Max(
                    1,
                    Mathf.CeilToInt(visibleCount / (float)iconsPerRow));
                float width = snapshot.PaddingLeft
                    + snapshot.PaddingRight
                    + columns * cellSize.x
                    + Mathf.Max(0, columns - 1) * spacing.x;
                float height = snapshot.PaddingTop
                    + snapshot.PaddingBottom
                    + rows * cellSize.y
                    + Mathf.Max(0, rows - 1) * spacing.y;

                statusRect.pivot = Vector2.zero;
                statusRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    width);
                statusRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    height);
                float scale = _buffDebuffHudScale == null
                    ? 1.0f
                    : Mathf.Clamp(
                        _buffDebuffHudScale.Value,
                        MinimumHudScale,
                        MaximumHudScale);
                statusRect.localScale = new Vector3(
                    snapshot.LocalScale.x * scale,
                    snapshot.LocalScale.y * scale,
                    snapshot.LocalScale.z);

                RectTransform heroBars = GetHeroHudRectTransform(_activeHeroHudView);
                if (heroBars != null)
                {
                    heroBars.GetWorldCorners(_worldCorners);
                    Vector3 target = _worldCorners[1]
                        + heroBars.TransformVector(
                            new Vector3(
                                BuffDebuffHudBaselineOffsetX
                                    + (_buffDebuffHudOffsetX == null
                                        ? 0.0f
                                        : _buffDebuffHudOffsetX.Value),
                                BuffDebuffHudBaselineOffsetY
                                    + (_buffDebuffHudOffsetY == null
                                        ? 0.0f
                                        : _buffDebuffHudOffsetY.Value),
                                0.0f));
                    statusRect.position = target;
                }
            }
            catch (Exception exception)
            {
                LogAccessorFailure(
                    "Could not arrange the buff and debuff HUD: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void ReleaseHeroStatusHud(object statusHud)
        {
            RectTransform statusRect = GetStatusHudRectTransform(statusHud);
            if (statusRect != null)
            {
                ReleaseStatusHudLayout(statusRect);
            }
            else
            {
                DestroyStatusHudTestObjects();
                _statusHudLayoutSnapshot = null;
            }

            if (ReferenceEquals(_activeHeroStatusHud, statusHud))
            {
                _activeHeroStatusHud = null;
            }
        }

        private void ReleaseStatusHudLayout(RectTransform statusRect)
        {
            DestroyStatusHudTestObjects();
            StatusHudLayoutSnapshot snapshot = _statusHudLayoutSnapshot;
            if (snapshot == null || snapshot.RectTransform != statusRect)
            {
                return;
            }

            snapshot.RestoreChildren();
            statusRect.anchorMin = snapshot.AnchorMin;
            statusRect.anchorMax = snapshot.AnchorMax;
            statusRect.pivot = snapshot.Pivot;
            statusRect.anchoredPosition = snapshot.AnchoredPosition;
            statusRect.sizeDelta = snapshot.SizeDelta;
            statusRect.localScale = snapshot.LocalScale;
            if (snapshot.LayoutGroup != null)
            {
                snapshot.LayoutGroup.enabled = snapshot.LayoutGroupEnabled;
            }
            if (snapshot.ContentSizeFitter != null)
            {
                snapshot.ContentSizeFitter.enabled =
                    snapshot.ContentSizeFitterEnabled;
            }

            LayoutRebuilder.MarkLayoutForRebuild(statusRect);
            _statusHudLayoutSnapshot = null;
        }

        private StatusHudLayoutSnapshot GetOrCreateStatusHudLayoutSnapshot(
            RectTransform statusRect)
        {
            if (_statusHudLayoutSnapshot != null
                && _statusHudLayoutSnapshot.RectTransform == statusRect)
            {
                return _statusHudLayoutSnapshot;
            }

            if (_statusHudLayoutSnapshot != null)
            {
                ReleaseStatusHudLayout(
                    _statusHudLayoutSnapshot.RectTransform);
            }
            LayoutGroup layoutGroup = statusRect.GetComponent<LayoutGroup>();
            ContentSizeFitter contentSizeFitter =
                statusRect.GetComponent<ContentSizeFitter>();
            Vector2 spacing = new Vector2(8.0f, 8.0f);
            Vector2 cellSize = FindStatusHudCellSize(
                statusRect,
                new Vector2(40.0f, 40.0f));
            int paddingLeft = 0;
            int paddingRight = 0;
            int paddingTop = 0;
            int paddingBottom = 0;
            if (layoutGroup != null)
            {
                paddingLeft = layoutGroup.padding.left;
                paddingRight = layoutGroup.padding.right;
                paddingTop = layoutGroup.padding.top;
                paddingBottom = layoutGroup.padding.bottom;
            }

            GridLayoutGroup grid = layoutGroup as GridLayoutGroup;
            if (grid != null)
            {
                cellSize = grid.cellSize;
                spacing = grid.spacing;
            }
            else
            {
                HorizontalLayoutGroup horizontal =
                    layoutGroup as HorizontalLayoutGroup;
                VerticalLayoutGroup vertical =
                    layoutGroup as VerticalLayoutGroup;
                if (horizontal != null)
                {
                    spacing = new Vector2(horizontal.spacing, horizontal.spacing);
                }
                else if (vertical != null)
                {
                    spacing = new Vector2(vertical.spacing, vertical.spacing);
                }
            }

            _statusHudLayoutSnapshot = new StatusHudLayoutSnapshot(
                statusRect,
                layoutGroup,
                contentSizeFitter,
                cellSize,
                spacing,
                paddingLeft,
                paddingRight,
                paddingTop,
                paddingBottom);
            if (layoutGroup != null)
            {
                layoutGroup.enabled = false;
            }
            if (contentSizeFitter != null)
            {
                contentSizeFitter.enabled = false;
            }

            return _statusHudLayoutSnapshot;
        }

        private static List<RectTransform> GetVisibleStatusChildren(
            RectTransform statusRect)
        {
            List<RectTransform> children = new List<RectTransform>();
            for (int i = 0; i < statusRect.childCount; i++)
            {
                RectTransform child = statusRect.GetChild(i) as RectTransform;
                if (child != null && child.gameObject.activeSelf)
                {
                    children.Add(child);
                }
            }

            return children;
        }

        private static Vector2 FindStatusHudCellSize(
            RectTransform statusRect,
            Vector2 fallback)
        {
            for (int i = 0; i < statusRect.childCount; i++)
            {
                RectTransform child = statusRect.GetChild(i) as RectTransform;
                if (child == null
                    || IsStatusHudTestObject(child.gameObject)
                    || !child.gameObject.activeSelf)
                {
                    continue;
                }

                float width = child.rect.width;
                float height = child.rect.height;
                if (width > 0.01f && height > 0.01f)
                {
                    return new Vector2(width, height);
                }
            }

            return fallback;
        }

        private void EnsureStatusHudTestObjects(
            RectTransform statusRect,
            StatusHudLayoutSnapshot snapshot)
        {
            bool enabled = _buffDebuffLayoutTestMode != null
                && _buffDebuffLayoutTestMode.Value;
            int requestedCount = enabled && _buffDebuffLayoutTestIconCount != null
                ? Mathf.Clamp(_buffDebuffLayoutTestIconCount.Value, 1, 30)
                : 0;
            while (_statusHudTestObjects.Count > requestedCount)
            {
                int last = _statusHudTestObjects.Count - 1;
                GameObject testObject = _statusHudTestObjects[last];
                _statusHudTestObjects.RemoveAt(last);
                if (testObject != null)
                {
                    testObject.SetActive(false);
                    Destroy(testObject);
                }
            }

            while (_statusHudTestObjects.Count < requestedCount)
            {
                int index = _statusHudTestObjects.Count;
                GameObject testObject = CreateStatusHudTestObject(
                    statusRect,
                    snapshot.BaseCellSize,
                    index);
                _statusHudTestObjects.Add(testObject);
            }
        }

        private static GameObject CreateStatusHudTestObject(
            RectTransform parent,
            Vector2 size,
            int index)
        {
            GameObject testObject = new GameObject(
                "GloriousUI_StatusTest_"
                    + (index + 1).ToString("00", CultureInfo.InvariantCulture),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            RectTransform rect = testObject.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;

            bool buff = index % 2 == 0;
            Image image = testObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = buff
                ? new Color(0.18f, 0.68f, 0.62f, 0.92f)
                : new Color(0.72f, 0.16f, 0.25f, 0.92f);
            Outline outline = testObject.GetComponent<Outline>();
            outline.effectColor = buff
                ? new Color(0.85f, 0.72f, 0.28f, 0.95f)
                : new Color(0.55f, 0.30f, 0.76f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
            return testObject;
        }

        private void ResizeStatusHudTestObjects(Vector2 size)
        {
            for (int i = 0; i < _statusHudTestObjects.Count; i++)
            {
                GameObject testObject = _statusHudTestObjects[i];
                RectTransform rect = testObject == null
                    ? null
                    : testObject.transform as RectTransform;
                if (rect != null)
                {
                    rect.sizeDelta = size;
                }
            }
        }

        private void DestroyStatusHudTestObjects()
        {
            for (int i = 0; i < _statusHudTestObjects.Count; i++)
            {
                GameObject testObject = _statusHudTestObjects[i];
                if (testObject != null)
                {
                    testObject.SetActive(false);
                    Destroy(testObject);
                }
            }

            _statusHudTestObjects.Clear();
        }

        private float GetBuffDebuffSpacingScale()
        {
            return _buffDebuffSpacingScale == null
                ? 1.0f
                : Mathf.Clamp(
                    _buffDebuffSpacingScale.Value,
                    0.0f,
                    5.0f);
        }

        private static void AlignStatusHudPrimaryImage(
            RectTransform statusRect,
            RectTransform child,
            Vector2 desiredCenter)
        {
            Image[] images = child.GetComponentsInChildren<Image>(true);
            RectTransform primary = null;
            float largestArea = 0.0f;
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                RectTransform imageRect = image == null
                    ? null
                    : image.rectTransform;
                if (imageRect == null
                    || !image.gameObject.activeInHierarchy
                    || image.color.a <= 0.001f)
                {
                    continue;
                }

                float area = Mathf.Abs(
                    imageRect.rect.width
                    * imageRect.rect.height);
                if (primary == null || area > largestArea)
                {
                    primary = imageRect;
                    largestArea = area;
                }
            }

            if (primary == null)
            {
                return;
            }

            Vector3 worldCenter = primary.TransformPoint(
                primary.rect.center);
            Vector3 localCenter = statusRect.InverseTransformPoint(
                worldCenter);
            child.anchoredPosition += new Vector2(
                desiredCenter.x - localCenter.x,
                desiredCenter.y - localCenter.y);
        }

        private bool ShouldControlBuffDebuffHud()
        {
            return IsEnabled()
                && _controlBuffDebuffHud != null
                && _controlBuffDebuffHud.Value;
        }

        private static bool IsStatusHudTestObject(GameObject gameObject)
        {
            return gameObject != null
                && gameObject.name.StartsWith(
                    "GloriousUI_StatusTest_",
                    StringComparison.Ordinal);
        }

        private static RectTransform GetStatusHudRectTransform(object statusHud)
        {
            Component component = statusHud as Component;
            return component == null
                ? null
                : component.transform as RectTransform;
        }

        private static bool IsHeroStatusHud(object statusHud)
        {
            return statusHud != null
                && string.Equals(
                    statusHud.GetType().FullName,
                    HeroStatusHudTypeName,
                    StringComparison.Ordinal);
        }

        internal void RefreshHeroHudBaseScale(object heroHudView)
        {
            RectTransform rectTransform = GetHeroHudRectTransform(heroHudView);
            if (rectTransform == null)
            {
                return;
            }

            int id = rectTransform.GetInstanceID();
            HudTransformSnapshot snapshot;
            if (_hudTransformSnapshots.TryGetValue(id, out snapshot))
            {
                _hudTransformSnapshots[id] = snapshot.WithLocalScale(rectTransform.localScale);
            }
            else
            {
                GetOrCreateHudTransformSnapshot(rectTransform);
            }

            ApplyHeroHudTransform(heroHudView);
            MarkHudLayoutDirty(HudLayoutDirty.StatusHud);
        }

        internal void HandleHeroHudTimer(object heroHudView)
        {
            if (!ShouldControlHeroHudTimer() || heroHudView == null)
            {
                return;
            }

            try
            {
                bool refreshedLastly =
                    (bool)_heroHudRefreshedLastlyField.GetValue(heroHudView);
                if (!refreshedLastly)
                {
                    return;
                }

                float duration = GetHeroHudVisibleSeconds();
                float timer = Convert.ToSingle(
                    _heroHudShowTimerField.GetValue(heroHudView),
                    CultureInfo.InvariantCulture);
                timer = Mathf.Clamp(timer - Time.unscaledDeltaTime, 0.0f, duration);
                _heroHudShowTimerField.SetValue(heroHudView, timer);
                if (timer > 0.0f)
                {
                    return;
                }

                _heroHudRefreshedLastlyField.SetValue(heroHudView, false);
                _heroHudShowTimerField.SetValue(heroHudView, duration);
                _heroHudUpdateCanvasGroupsMethod.Invoke(heroHudView, null);
            }
            catch (Exception exception)
            {
                LogAccessorFailure(
                    "Could not update the Hero HUD visibility timer: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void ResetHeroHudTimer(object heroHudView)
        {
            if (!ShouldControlHeroHudTimer() || heroHudView == null)
            {
                return;
            }

            _heroHudShowTimerField.SetValue(
                heroHudView,
                GetHeroHudVisibleSeconds());
        }

        internal void RefreshHeroHudTimer(object heroHudView)
        {
            if (!ShouldControlHeroHudTimer() || heroHudView == null)
            {
                return;
            }

            try
            {
                _heroHudShowTimerField.SetValue(
                    heroHudView,
                    GetHeroHudVisibleSeconds());
                _heroHudRefreshedLastlyField.SetValue(heroHudView, true);
                _heroHudUpdateCanvasGroupsMethod.Invoke(heroHudView, null);
            }
            catch (Exception exception)
            {
                LogAccessorFailure(
                    "Could not refresh the Hero HUD visibility timer: "
                    + exception.GetBaseException().Message);
            }
        }

        private void UpdateCompassHotkey()
        {
            if (_controlCompass == null
                || !_controlCompass.Value
                || _compassVisibilityMode == null)
            {
                return;
            }

            KeyCode hotkey = _compassHotkey == null
                ? KeyCode.None
                : _compassHotkey.Value;
            if (_compassVisibilityMode.Value == CompassVisibilityMode.ToggleHotkey
                && hotkey != KeyCode.None
                && Input.GetKeyDown(hotkey))
            {
                _compassToggleVisible = !_compassToggleVisible;
            }

            bool requestedVisible = IsCompassRequestedVisible(hotkey);
            if (_lastCompassRequestedVisible.HasValue
                && _lastCompassRequestedVisible.Value == requestedVisible)
            {
                return;
            }

            _lastCompassRequestedVisible = requestedVisible;
            RefreshCompassVisibility(false);
        }

        private bool IsCompassRequestedVisible(KeyCode hotkey)
        {
            if (_compassVisibilityMode == null)
            {
                return false;
            }

            switch (_compassVisibilityMode.Value)
            {
                case CompassVisibilityMode.Always:
                    return true;
                case CompassVisibilityMode.ToggleHotkey:
                    return hotkey != KeyCode.None && _compassToggleVisible;
                case CompassVisibilityMode.HoldHotkey:
                    return hotkey != KeyCode.None && Input.GetKey(hotkey);
                default:
                    return false;
            }
        }

        internal void RegisterCompassView(object compassView)
        {
            _activeCompassView = compassView;
            _lastCompassRequestedVisible = null;
            RefreshCompassVisibility(true);
        }

        internal void RefreshCompassVisibility(bool captureGameVisibility)
        {
            Component component = _activeCompassView as Component;
            if (component == null || _compassCanvasGroupField == null)
            {
                return;
            }

            CanvasGroup canvasGroup =
                _compassCanvasGroupField.GetValue(_activeCompassView) as CanvasGroup;
            if (canvasGroup == null)
            {
                return;
            }

            int id = component.GetInstanceID();
            bool gameVisible;
            if (captureGameVisibility
                || !_compassGameVisibility.TryGetValue(id, out gameVisible))
            {
                gameVisible = canvasGroup.alpha > 0.5f;
                _compassGameVisibility[id] = gameVisible;
            }

            bool hideForQuickUse = _quickUseWheelOpen
                && _hideGameplayHudInQuickUseWheel != null
                && _hideGameplayHudInQuickUseWheel.Value;
            if (!IsEnabled() || _controlCompass == null || !_controlCompass.Value)
            {
                canvasGroup.alpha = gameVisible && !hideForQuickUse ? 1.0f : 0.0f;
                return;
            }

            KeyCode hotkey = _compassHotkey == null
                ? KeyCode.None
                : _compassHotkey.Value;
            canvasGroup.alpha = gameVisible
                && !hideForQuickUse
                && IsCompassRequestedVisible(hotkey)
                ? 1.0f
                : 0.0f;
        }

        private void RestoreCompassVisibility()
        {
            Component component = _activeCompassView as Component;
            if (component == null || _compassCanvasGroupField == null)
            {
                return;
            }

            CanvasGroup canvasGroup =
                _compassCanvasGroupField.GetValue(_activeCompassView) as CanvasGroup;
            bool gameVisible;
            if (canvasGroup != null
                && _compassGameVisibility.TryGetValue(
                    component.GetInstanceID(),
                    out gameVisible))
            {
                canvasGroup.alpha = gameVisible ? 1.0f : 0.0f;
            }
        }

        internal bool ShouldShowCharacterPoints()
        {
            if (_restoringVanillaHud || !IsEnabled())
            {
                return true;
            }

            bool hideForQuickUse = _quickUseWheelOpen
                && _hideGameplayHudInQuickUseWheel != null
                && _hideGameplayHudInQuickUseWheel.Value;
            return !hideForQuickUse
                && _levelNotificationMode != null
                && _levelNotificationMode.Value
                    != LevelNotificationMode.Disabled;
        }

        private bool ShouldTimeCharacterPoints()
        {
            return ShouldShowCharacterPoints()
                && !_restoringVanillaHud
                && _levelNotificationMode != null
                && _levelNotificationMode.Value
                    == LevelNotificationMode.Timed;
        }

        internal void RegisterCharacterPointsView(object characterPointsView)
        {
            _activeCharacterPointsView = characterPointsView;
            ApplyCharacterPointsVisibility(characterPointsView);
        }

        internal void ApplyCharacterPointsVisibility(object characterPointsView)
        {
            ResetCharacterPointsTimer();
            if (characterPointsView == null
                || _characterPointsCanvasGroupField == null)
            {
                return;
            }

            CanvasGroup canvasGroup =
                _characterPointsCanvasGroupField.GetValue(characterPointsView) as CanvasGroup;
            if (canvasGroup == null)
            {
                return;
            }

            if (!ShouldShowCharacterPoints())
            {
                canvasGroup.alpha = 0.0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.gameObject.SetActive(false);
                return;
            }

            canvasGroup.gameObject.SetActive(true);
            if (_characterPointsUpdateVisualMethod != null)
            {
                _characterPointsUpdateVisualMethod.Invoke(characterPointsView, null);
            }
        }

        internal void CompleteCharacterPointsVisualUpdate(
            object characterPointsView)
        {
            _activeCharacterPointsView =
                characterPointsView;
            if (!ShouldTimeCharacterPoints()
                || characterPointsView == null
                || _characterPointsCanvasGroupField == null)
            {
                ResetCharacterPointsTimer();
                return;
            }

            CanvasGroup canvasGroup =
                _characterPointsCanvasGroupField.GetValue(
                    characterPointsView) as CanvasGroup;
            if (canvasGroup == null
                || !canvasGroup.gameObject.activeSelf)
            {
                ResetCharacterPointsTimer();
                return;
            }

            float visibleSeconds =
                _levelNotificationVisibleSeconds == null
                    ? 5.0f
                    : Mathf.Clamp(
                        _levelNotificationVisibleSeconds.Value,
                        0.0f,
                        60.0f);
            float fadeSeconds =
                _levelNotificationFadeSeconds == null
                    ? 1.0f
                    : Mathf.Clamp(
                        _levelNotificationFadeSeconds.Value,
                        0.0f,
                        5.0f);
            _characterPointsFadeStartTime =
                Time.unscaledTime + visibleSeconds;
            _characterPointsFadeEndTime =
                _characterPointsFadeStartTime
                + fadeSeconds;
        }

        private void UpdateCharacterPointsTimedVisibility()
        {
            if (!ShouldTimeCharacterPoints()
                || _activeCharacterPointsView == null
                || _characterPointsCanvasGroupField == null
                || _characterPointsFadeStartTime < 0.0f
                || Time.unscaledTime
                    < _characterPointsFadeStartTime)
            {
                return;
            }

            CanvasGroup canvasGroup =
                _characterPointsCanvasGroupField.GetValue(
                    _activeCharacterPointsView) as CanvasGroup;
            if (canvasGroup == null
                || !canvasGroup.gameObject.activeSelf)
            {
                ResetCharacterPointsTimer();
                return;
            }

            float fadeSeconds =
                _characterPointsFadeEndTime
                - _characterPointsFadeStartTime;
            if (fadeSeconds <= 0.0f
                || Time.unscaledTime
                    >= _characterPointsFadeEndTime)
            {
                canvasGroup.alpha = 0.0f;
                canvasGroup.gameObject.SetActive(false);
                ResetCharacterPointsTimer();
                return;
            }

            float progress = Mathf.Clamp01(
                (Time.unscaledTime
                    - _characterPointsFadeStartTime)
                / fadeSeconds);
            canvasGroup.alpha =
                1.0f - progress;
        }

        private void ResetCharacterPointsTimer()
        {
            _characterPointsFadeStartTime = -1.0f;
            _characterPointsFadeEndTime = -1.0f;
        }

        internal void ReleaseCharacterPointsView(object characterPointsView)
        {
            if (ReferenceEquals(_activeCharacterPointsView, characterPointsView))
            {
                _activeCharacterPointsView = null;
                ResetCharacterPointsTimer();
            }
        }

        private void RestoreCharacterPointsVisibility()
        {
            ResetCharacterPointsTimer();
            if (_activeCharacterPointsView == null
                || _characterPointsCanvasGroupField == null)
            {
                return;
            }

            CanvasGroup canvasGroup =
                _characterPointsCanvasGroupField.GetValue(
                    _activeCharacterPointsView) as CanvasGroup;
            if (canvasGroup != null)
            {
                canvasGroup.gameObject.SetActive(true);
            }

            if (_characterPointsUpdateVisualMethod != null)
            {
                _characterPointsUpdateVisualMethod.Invoke(
                    _activeCharacterPointsView,
                    null);
            }
        }

        internal void SetQuickUseWheelOpen(bool open)
        {
            _quickUseWheelOpen = open;
            ApplyQuickUseWheelHudVisibility();
        }

        internal void RegisterQuickUseWheel(
            VQuickUseWheelUI view)
        {
            CaptureCharacterPointsForQuickUse();
            _activeQuickUseWheelView = view;
            SetQuickUseWheelOpen(true);
            ConfigureQuickUseWheelLoadouts(view);
        }

        internal void ReleaseQuickUseWheel(
            VQuickUseWheelUI view)
        {
            ReleaseQuickUseWheelLoadouts(view);
            SetQuickUseWheelOpen(false);
        }

        private void CaptureCharacterPointsForQuickUse()
        {
            if (_characterPointsQuickUseSnapshot != null
                || _activeCharacterPointsView == null
                || _characterPointsCanvasGroupField == null
                || _hideGameplayHudInQuickUseWheel == null
                || !_hideGameplayHudInQuickUseWheel.Value)
            {
                return;
            }

            CanvasGroup canvasGroup =
                _characterPointsCanvasGroupField.GetValue(
                    _activeCharacterPointsView)
                    as CanvasGroup;
            if (canvasGroup != null)
            {
                _characterPointsQuickUseSnapshot =
                    new CharacterPointsQuickUseSnapshot(
                        canvasGroup,
                        canvasGroup.gameObject.activeSelf,
                        canvasGroup.alpha,
                        canvasGroup.interactable,
                        canvasGroup.blocksRaycasts,
                        _characterPointsFadeStartTime,
                        _characterPointsFadeEndTime);
            }
        }

        private void HideCharacterPointsForQuickUse()
        {
            CaptureCharacterPointsForQuickUse();
            CharacterPointsQuickUseSnapshot snapshot =
                _characterPointsQuickUseSnapshot;
            if (snapshot == null
                || snapshot.CanvasGroup == null)
            {
                ApplyCharacterPointsVisibility(
                    _activeCharacterPointsView);
                return;
            }

            snapshot.CanvasGroup.alpha = 0.0f;
            snapshot.CanvasGroup.interactable = false;
            snapshot.CanvasGroup.blocksRaycasts = false;
            snapshot.CanvasGroup.gameObject.SetActive(
                false);
        }

        private void RestoreCharacterPointsAfterQuickUse()
        {
            CharacterPointsQuickUseSnapshot snapshot =
                _characterPointsQuickUseSnapshot;
            _characterPointsQuickUseSnapshot = null;
            if (snapshot == null
                || snapshot.CanvasGroup == null)
            {
                return;
            }

            snapshot.CanvasGroup.alpha = snapshot.Alpha;
            snapshot.CanvasGroup.interactable =
                snapshot.Interactable;
            snapshot.CanvasGroup.blocksRaycasts =
                snapshot.BlocksRaycasts;
            snapshot.CanvasGroup.gameObject.SetActive(
                snapshot.ActiveSelf);
            _characterPointsFadeStartTime =
                snapshot.FadeStartTime;
            _characterPointsFadeEndTime =
                snapshot.FadeEndTime;
            _suppressCharacterPointsRefreshUntil =
                Time.unscaledTime + 0.5f;
        }

        private bool ShouldSuppressCharacterPointsRefresh()
        {
            return !_quickUseWheelOpen
                && Time.unscaledTime
                    < _suppressCharacterPointsRefreshUntil;
        }

        internal void PrepareCharacterPointsUiStateChange(
            UIState state)
        {
            if (state == null)
            {
                return;
            }

            bool mapInteractive = state.IsMapInteractive;
            bool returningFromMenu =
                _lastCharacterPointsMapInteractive == false
                && mapInteractive;
            _lastCharacterPointsMapInteractive = mapInteractive;
            if (returningFromMenu
                && !_quickUseWheelOpen
                && IsEnabled()
                && _levelNotificationMode != null
                && _levelNotificationMode.Value
                    == LevelNotificationMode.Timed)
            {
                _suppressCharacterPointsRefreshUntil =
                    Mathf.Max(
                        _suppressCharacterPointsRefreshUntil,
                        Time.unscaledTime + 0.5f);
            }
        }

        private void ConfigureQuickUseWheelLoadouts(
            VQuickUseWheelUI view)
        {
            ReleaseQuickUseWheelLoadouts(
                _activeQuickUseWheelView);
            _activeQuickUseWheelView = view;
            if (view == null
                || !ShouldControlQuickUseWheelLoadouts())
            {
                return;
            }

            List<VCQuickLoadout> loadouts =
                new List<VCQuickLoadout>();
            VCQuickLoadout[] allLoadouts =
                view.GetComponentsInChildren<VCQuickLoadout>(
                    true);
            for (int i = 0; i < allLoadouts.Length; i++)
            {
                if (allLoadouts[i] != null
                    && allLoadouts[i].gameObject
                        .activeInHierarchy)
                {
                    loadouts.Add(allLoadouts[i]);
                }
            }
            loadouts.Sort(
                delegate(
                    VCQuickLoadout left,
                    VCQuickLoadout right)
                {
                    return left.LoadoutIndex.CompareTo(
                        right.LoadoutIndex);
                });

            List<VCQuickSlot> quickSlots =
                new List<VCQuickSlot>();
            VCQuickSlot[] allQuickSlots =
                view.GetComponentsInChildren<VCQuickSlot>(
                    true);
            for (int i = 0; i < allQuickSlots.Length; i++)
            {
                VCQuickSlot quickSlot = allQuickSlots[i];
                if (quickSlot != null
                    && !quickSlot.isQuickAction
                    && quickSlot.gameObject
                        .activeInHierarchy)
                {
                    quickSlots.Add(quickSlot);
                }
            }
            quickSlots.Sort(
                delegate(
                    VCQuickSlot left,
                    VCQuickSlot right)
                {
                    return left.transform.position.x
                        .CompareTo(
                            right.transform.position.x);
                });

            if (loadouts.Count < 4
                || quickSlots.Count < 3
                || !QuickWheelLoadoutFieldsAvailable())
            {
                Logger.LogWarning(
                    "Glorious could not build the six-loadout quick-use wheel because the current wheel prefab does not match the expected four loadout and three quick-item options.");
                return;
            }

            List<GameObject> hiddenOptions =
                new List<GameObject>();
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    AddQuickWheelLoadoutProxy(
                        loadouts[i],
                        i + 1,
                        false,
                        view);
                }

                Vector3 wheelCenter =
                    (loadouts[0].transform.position
                        + loadouts[3].transform.position)
                    * 0.5f;

                AddQuickWheelLoadoutClone(
                    loadouts[1],
                    quickSlots[quickSlots.Count - 1],
                    wheelCenter,
                    5,
                    view);
                AddQuickWheelLoadoutClone(
                    loadouts[2],
                    quickSlots[0],
                    wheelCenter,
                    6,
                    view);

                for (int i = 0; i < quickSlots.Count; i++)
                {
                    GameObject option =
                        quickSlots[i].gameObject;
                    if (option.activeSelf)
                    {
                        hiddenOptions.Add(option);
                        option.SetActive(false);
                    }
                }

                ApplyQuickWheelPresentation(view);
                RefreshAllQuickWheelLoadoutProxies();
            }
            catch (Exception exception)
            {
                for (int i = 0;
                    i < hiddenOptions.Count;
                    i++)
                {
                    if (hiddenOptions[i] != null)
                    {
                        hiddenOptions[i].SetActive(true);
                    }
                }
                for (int i = 0;
                    i < _quickWheelLoadoutClones.Count;
                    i++)
                {
                    if (_quickWheelLoadoutClones[i] != null)
                    {
                        UnityEngine.Object.Destroy(
                            _quickWheelLoadoutClones[i]);
                    }
                }
                _quickWheelLoadoutClones.Clear();
                _quickWheelLoadoutProxies.Clear();
                Logger.LogWarning(
                    "Glorious left the vanilla quick-use wheel unchanged because the six-loadout layout could not be created: "
                    + exception.GetBaseException().Message);
            }
        }

        private void AddQuickWheelLoadoutClone(
            VCQuickLoadout source,
            VCQuickSlot positionSource,
            Vector3 wheelCenter,
            int slot,
            VQuickUseWheelUI owner)
        {
            GameObject clone = UnityEngine.Object.Instantiate(
                source.gameObject,
                positionSource.transform.parent,
                false);
            clone.name =
                "GloriousUI_QuickWheelLoadout"
                + slot.ToString(CultureInfo.InvariantCulture);
            CopyQuickWheelOptionLayout(
                positionSource.transform,
                clone.transform);
            clone.transform.SetSiblingIndex(
                positionSource.transform.GetSiblingIndex());

            VCQuickLoadout option =
                clone.GetComponent<VCQuickLoadout>();
            if (option == null)
            {
                UnityEngine.Object.Destroy(clone);
                throw new MissingComponentException(
                    "The cloned loadout option has no VCQuickLoadout component.");
            }

            _radialOptionInitialPositionField.SetValue(
                option,
                option.transform.localPosition);
            MirrorQuickWheelLoadoutContent(
                option,
                source,
                wheelCenter);
            CopyQuickWheelChosenIndicator(
                option,
                source,
                wheelCenter);
            _quickWheelLoadoutClones.Add(clone);
            AddQuickWheelLoadoutProxy(
                option,
                slot,
                true,
                owner);
        }

        private static void CopyQuickWheelOptionLayout(
            Transform source,
            Transform destination)
        {
            RectTransform sourceRect =
                source as RectTransform;
            RectTransform destinationRect =
                destination as RectTransform;
            if (sourceRect != null
                && destinationRect != null)
            {
                destinationRect.anchorMin =
                    sourceRect.anchorMin;
                destinationRect.anchorMax =
                    sourceRect.anchorMax;
                destinationRect.pivot =
                    sourceRect.pivot;
                destinationRect.anchoredPosition3D =
                    sourceRect.anchoredPosition3D;
            }
            else
            {
                destination.localPosition =
                    source.localPosition;
            }

            destination.localRotation =
                source.localRotation;
        }

        private void AddQuickWheelLoadoutProxy(
            VCQuickLoadout option,
            int slot,
            bool clone,
            VQuickUseWheelUI owner)
        {
            RectTransform primary =
                _quickLoadoutPrimaryRectField.GetValue(
                    option) as RectTransform;
            RectTransform secondary =
                _quickLoadoutSecondaryRectField.GetValue(
                    option) as RectTransform;
            if (primary == null || secondary == null)
            {
                throw new MissingReferenceException(
                    "A quick-wheel loadout item transform is missing.");
            }

            _quickWheelLoadoutProxies[
                option.GetInstanceID()] =
                    new QuickWheelLoadoutProxy(
                        option,
                        slot,
                        clone,
                        owner,
                        primary.localPosition,
                        secondary.localPosition);
        }

        private bool QuickWheelLoadoutFieldsAvailable()
        {
            return _quickLoadoutMiddlePointField != null
                && _quickLoadoutPrimarySlotField != null
                && _quickLoadoutSecondarySlotField != null
                && _quickLoadoutPrimaryRectField != null
                && _quickLoadoutSecondaryRectField != null
                && _quickLoadoutPrimarySelectedField != null
                && _quickLoadoutSecondarySelectedField != null
                && _radialOptionInitialPositionField != null
                && _quickUseOptionChosenIndicatorField
                    != null;
        }

        private void MirrorQuickWheelLoadoutContent(
            VCQuickLoadout destinationOption,
            VCQuickLoadout sourceOption,
            Vector3 wheelCenter)
        {
            Transform destinationMiddle =
                _quickLoadoutMiddlePointField.GetValue(
                    destinationOption) as Transform;
            Transform sourceMiddle =
                _quickLoadoutMiddlePointField.GetValue(
                    sourceOption) as Transform;
            RectTransform destinationPrimary =
                _quickLoadoutPrimaryRectField.GetValue(
                    destinationOption) as RectTransform;
            RectTransform sourcePrimary =
                _quickLoadoutPrimaryRectField.GetValue(
                    sourceOption) as RectTransform;
            RectTransform destinationSecondary =
                _quickLoadoutSecondaryRectField.GetValue(
                    destinationOption) as RectTransform;
            RectTransform sourceSecondary =
                _quickLoadoutSecondaryRectField.GetValue(
                    sourceOption) as RectTransform;
            if (destinationMiddle == null
                || sourceMiddle == null
                || destinationPrimary == null
                || sourcePrimary == null
                || destinationSecondary == null
                || sourceSecondary == null)
            {
                throw new MissingReferenceException(
                    "A quick-wheel loadout item position is missing.");
            }

            Quaternion halfTurn =
                Quaternion.AngleAxis(
                    180.0f,
                    sourcePrimary.forward);
            MirrorQuickWheelTransformPosition(
                destinationMiddle,
                sourceMiddle,
                wheelCenter,
                halfTurn,
                false);
            MirrorQuickWheelTransformPosition(
                destinationPrimary,
                sourcePrimary,
                wheelCenter,
                halfTurn,
                true);
            MirrorQuickWheelTransformPosition(
                destinationSecondary,
                sourceSecondary,
                wheelCenter,
                halfTurn,
                true);
        }

        private static void MirrorQuickWheelTransformPosition(
            Transform destination,
            Transform source,
            Vector3 wheelCenter,
            Quaternion halfTurn,
            bool keepArtworkUpright)
        {
            destination.position =
                wheelCenter
                + halfTurn
                    * (source.position
                        - wheelCenter);
            destination.rotation =
                keepArtworkUpright
                    ? source.rotation
                    : halfTurn * source.rotation;
            destination.localScale =
                source.localScale;
        }

        private void CopyQuickWheelChosenIndicator(
            VCQuickUseOption destinationOption,
            VCQuickUseOption sourceOption,
            Vector3 wheelCenter)
        {
            Image destination =
                _quickUseOptionChosenIndicatorField.GetValue(
                    destinationOption) as Image;
            Image source =
                _quickUseOptionChosenIndicatorField.GetValue(
                    sourceOption) as Image;
            if (destination == null || source == null)
            {
                throw new MissingReferenceException(
                    "A quick-wheel selected wedge is missing.");
            }

            RectTransform destinationRect =
                destination.rectTransform;
            RectTransform sourceRect =
                source.rectTransform;
            destinationRect.anchorMin =
                sourceRect.anchorMin;
            destinationRect.anchorMax =
                sourceRect.anchorMax;
            destinationRect.pivot =
                sourceRect.pivot;
            destinationRect.sizeDelta =
                sourceRect.sizeDelta;
            destinationRect.localScale =
                sourceRect.localScale;
            Quaternion halfTurn =
                Quaternion.AngleAxis(
                    180.0f,
                    sourceRect.forward);
            destinationRect.position =
                wheelCenter
                + halfTurn
                    * (sourceRect.position
                        - wheelCenter);
            destinationRect.rotation =
                halfTurn * sourceRect.rotation;
            destination.sprite = source.sprite;
            destination.overrideSprite =
                source.overrideSprite;
            destination.material =
                source.material;
            destination.type = source.type;
            destination.preserveAspect =
                source.preserveAspect;
            destination.fillCenter =
                source.fillCenter;
            destination.fillMethod =
                source.fillMethod;
            destination.fillOrigin =
                source.fillOrigin;
            destination.fillClockwise =
                source.fillClockwise;
            destination.fillAmount =
                source.fillAmount;
        }

        private void ApplyQuickWheelPresentation(
            VQuickUseWheelUI view)
        {
            if (view == null)
            {
                return;
            }

            if (_hideQuickWheelControlsLegend != null
                && _hideQuickWheelControlsLegend.Value
                && view.PromptsHost != null)
            {
                view.PromptsHost.gameObject.SetActive(
                    false);
            }

            bool hideCenter =
                _hideQuickWheelCenterControl != null
                && _hideQuickWheelCenterControl.Value;
            if (hideCenter)
            {
                VCPadShortcut[] shortcuts =
                    view.GetComponentsInChildren<
                        VCPadShortcut>(true);
                for (int i = 0;
                    i < shortcuts.Length;
                    i++)
                {
                    Transform parent =
                        shortcuts[i].transform.parent;
                    Transform grandParent =
                        parent == null
                            ? null
                            : parent.parent;
                    if (parent != null
                        && string.Equals(
                            parent.name,
                            "BG",
                            StringComparison.Ordinal)
                        && grandParent != null
                        && string.Equals(
                            grandParent.name,
                            "QuickUseLoadouts",
                            StringComparison.Ordinal))
                    {
                        shortcuts[i].gameObject.SetActive(
                            false);
                    }
                }
            }

            bool showAmmo =
                _ammoCounterEnabled != null
                && _ammoCounterEnabled.Value;
            if (!showAmmo)
            {
                return;
            }

            TMP_Text template = null;
            TMP_Text[] textComponents =
                view.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < textComponents.Length; i++)
            {
                TMP_Text candidate = textComponents[i];
                if (candidate != null
                    && candidate.font != null
                    && candidate.fontSharedMaterial != null)
                {
                    template = candidate;
                    break;
                }
            }
            if (template == null)
            {
                if (!_quickWheelTextFontWarningLogged)
                {
                    _quickWheelTextFontWarningLogged = true;
                    Logger.LogWarning(
                        "Glorious skipped quick-wheel ammo counters because the active wheel has no complete TextMesh Pro font template.");
                }
                return;
            }
            foreach (QuickWheelLoadoutProxy proxy
                in _quickWheelLoadoutProxies.Values)
            {
                CreateQuickWheelAmmoCounter(
                    proxy,
                    template);
            }
        }

        private void CreateQuickWheelAmmoCounter(
            QuickWheelLoadoutProxy proxy,
            TMP_Text template)
        {
            if (proxy == null
                || proxy.Option == null
                || proxy.AmmoCounterRoot != null
                || template == null
                || template.font == null
                || template.fontSharedMaterial == null)
            {
                return;
            }

            ItemSlotUI secondarySlot =
                _quickLoadoutSecondarySlotField.GetValue(
                    proxy.Option) as ItemSlotUI;
            if (secondarySlot == null)
            {
                return;
            }

            Transform quantityHost =
                secondarySlot.transform.Find(
                    "QuantitySlot");
            Transform parent = quantityHost
                ?? secondarySlot.transform;
            GameObject rootObject = new GameObject(
                "GloriousUI_AmmoCounter"
                    + proxy.Slot.ToString(
                        CultureInfo.InvariantCulture),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform root =
                rootObject.transform as RectTransform;
            root.SetParent(parent, false);
            root.anchorMin = quantityHost == null
                ? new Vector2(0.15f, 0.0f)
                : Vector2.zero;
            root.anchorMax = quantityHost == null
                ? new Vector2(0.85f, 0.32f)
                : Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            Image background =
                rootObject.GetComponent<Image>();
            background.color =
                new Color(0.0f, 0.0f, 0.0f, 0.72f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject(
                "AmmoText",
                typeof(RectTransform));
            RectTransform textRect =
                textObject.transform as RectTransform;
            textRect.SetParent(root, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI counter =
                textObject.AddComponent<
                    TextMeshProUGUI>();
            ApplyEquipmentPanelTextStyle(
                counter,
                template);
            counter.fontSize = 22.0f;
            counter.fontStyle |= FontStyles.Bold;
            counter.alignment =
                TextAlignmentOptions.Center;
            counter.textWrappingMode =
                TextWrappingModes.NoWrap;
            counter.raycastTarget = false;
            root.SetAsLastSibling();

            proxy.AmmoCounterRoot = rootObject;
            proxy.AmmoCounter = counter;
        }

        private void ReleaseQuickUseWheelLoadouts(
            VQuickUseWheelUI view)
        {
            if (view != null
                && _activeQuickUseWheelView != null
                && !ReferenceEquals(
                    view,
                    _activeQuickUseWheelView))
            {
                return;
            }

            _quickWheelLoadoutProxies.Clear();
            _quickWheelLoadoutClones.Clear();
            _hoveredQuickWheelLoadout = null;
            _activeQuickUseWheelView = null;
        }

        private bool ShouldControlQuickUseWheelLoadouts()
        {
            return IsEnabled()
                && _controlEquipmentWeaponLoadouts != null
                && _controlEquipmentWeaponLoadouts.Value
                && _controlQuickUseWheelLoadouts != null
                && _controlQuickUseWheelLoadouts.Value;
        }

        private bool TryGetQuickWheelLoadoutProxy(
            VCQuickLoadout option,
            out QuickWheelLoadoutProxy proxy)
        {
            proxy = null;
            return option != null
                && _quickWheelLoadoutProxies.TryGetValue(
                    option.GetInstanceID(),
                    out proxy)
                && ReferenceEquals(proxy.Option, option);
        }

        internal bool RefreshQuickWheelLoadoutProxy(
            VCQuickLoadout option)
        {
            QuickWheelLoadoutProxy proxy;
            if (!TryGetQuickWheelLoadoutProxy(
                    option,
                    out proxy))
            {
                return false;
            }
            VQuickUseWheelUI owner = proxy.Owner;
            if (owner == null)
            {
                return true;
            }

            Item primary;
            Item secondary;
            GetQuickWheelLoadoutItems(
                proxy.Slot,
                out primary,
                out secondary);

            Transform middlePoint =
                _quickLoadoutMiddlePointField.GetValue(
                    option) as Transform;
            ItemSlotUI primarySlot =
                _quickLoadoutPrimarySlotField.GetValue(
                    option) as ItemSlotUI;
            ItemSlotUI secondarySlot =
                _quickLoadoutSecondarySlotField.GetValue(
                    option) as ItemSlotUI;
            RectTransform primaryRect =
                _quickLoadoutPrimaryRectField.GetValue(
                    option) as RectTransform;
            RectTransform secondaryRect =
                _quickLoadoutSecondaryRectField.GetValue(
                    option) as RectTransform;
            GameObject primarySelected =
                _quickLoadoutPrimarySelectedField.GetValue(
                    option) as GameObject;
            GameObject secondarySelected =
                _quickLoadoutSecondarySelectedField.GetValue(
                    option) as GameObject;
            if (middlePoint == null
                || primarySlot == null
                || secondarySlot == null
                || primaryRect == null
                || secondaryRect == null
                || primarySelected == null
                || secondarySelected == null)
            {
                return false;
            }

            primaryRect.localPosition =
                proxy.PrimaryLocalPosition;
            secondaryRect.localPosition =
                proxy.SecondaryLocalPosition;

            bool selected =
                proxy.Slot == _currentVirtualWeaponSlot;
            bool showPrimary = primary != null;
            bool showSecondary = secondary != null
                && (!showPrimary
                    || !primary.IsTwoHanded
                    || primary.EquipmentType
                        == EquipmentType.Bow);
            primarySlot.gameObject.SetActive(showPrimary);
            secondarySlot.gameObject.SetActive(showSecondary);
            primarySelected.SetActive(
                selected && showPrimary);
            secondarySelected.SetActive(
                selected && showSecondary);

            if (showPrimary)
            {
                primarySlot.Setup(
                    primary,
                    owner);
            }
            if (showSecondary)
            {
                secondarySlot.Setup(
                    secondary,
                    owner);
            }
            if (showPrimary && !showSecondary)
            {
                primaryRect.position =
                    middlePoint.position;
            }
            else if (!showPrimary && showSecondary)
            {
                secondaryRect.position =
                    middlePoint.position;
            }

            UpdateQuickWheelAmmoCounter(
                proxy,
                primary,
                secondary);
            return true;
        }

        private void RefreshAllQuickWheelLoadoutProxies()
        {
            if (_quickWheelLoadoutProxies.Count == 0)
            {
                return;
            }

            foreach (QuickWheelLoadoutProxy proxy
                in _quickWheelLoadoutProxies.Values)
            {
                if (proxy != null
                    && proxy.Option != null)
                {
                    RefreshQuickWheelLoadoutProxy(
                        proxy.Option);
                }
            }
        }

        private void UpdateQuickWheelAmmoCounter(
            QuickWheelLoadoutProxy proxy,
            Item primary,
            Item secondary)
        {
            if (proxy == null
                || proxy.AmmoCounterRoot == null
                || proxy.AmmoCounter == null)
            {
                return;
            }

            bool visible =
                _ammoCounterEnabled != null
                && _ammoCounterEnabled.Value
                && primary != null
                && primary.EquipmentType
                    == EquipmentType.Bow
                && secondary != null
                && secondary.IsArrow;
            proxy.AmmoCounterRoot.SetActive(visible);
            if (!visible)
            {
                return;
            }

            int quantity = secondary.Quantity;
            proxy.AmmoCounter.text =
                quantity.ToString(
                    CultureInfo.InvariantCulture);
            proxy.AmmoCounter.color =
                quantity <= 5
                    ? new Color(
                        1.0f,
                        0.3f,
                        0.3f,
                        1.0f)
                    : quantity <= 15
                        ? new Color(
                            1.0f,
                            0.8f,
                            0.3f,
                            1.0f)
                        : new Color(
                            0.72f,
                            0.72f,
                            0.72f,
                            1.0f);
        }

        private void GetQuickWheelLoadoutItems(
            int slot,
            out Item primary,
            out Item secondary)
        {
            primary = null;
            secondary = null;
            VirtualWeaponLoadoutData loadout;
            if (!_virtualWeaponLoadouts.TryGetValue(
                    slot,
                    out loadout)
                || loadout == null)
            {
                return;
            }

            EquipmentDisplayItemLookup displayItems =
                BuildEquipmentDisplayItemLookup();
            primary =
                FindEquipmentWeaponDisplayItem(
                    loadout.MainHandGuid,
                    true,
                    displayItems) as Item;
            if (primary != null && primary.IsFists)
            {
                primary = null;
            }

            bool ranged = primary != null
                && TryReadBool(primary, "IsRanged");
            secondary =
                FindEquipmentWeaponDisplayItem(
                    ranged
                        ? loadout.QuiverGuid
                        : loadout.OffHandGuid,
                    false,
                    displayItems) as Item;
            if (secondary != null
                && secondary.IsFists)
            {
                secondary = null;
            }
        }

        private bool TryCycleQuickWheelArrowsAtPointer(
            VCQuickLoadout option)
        {
            try
            {
                return TryCycleQuickWheelArrowsAtPointerCore(
                    option);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not cycle the Glorious quick-wheel arrow assignment: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool TryCycleQuickWheelArrowsAtPointerCore(
            VCQuickLoadout option)
        {
            if (_arrowCycleEnabled == null
                || !_arrowCycleEnabled.Value
                || !Input.GetMouseButtonDown(0))
            {
                return false;
            }

            QuickWheelLoadoutProxy proxy;
            if (!TryGetQuickWheelLoadoutProxy(
                    option,
                    out proxy))
            {
                return false;
            }
            if (_lastQuickWheelArrowCycleFrame
                    == Time.frameCount
                && _lastQuickWheelArrowCycleSlot
                    == proxy.Slot)
            {
                return true;
            }

            ItemSlotUI secondarySlot =
                _quickLoadoutSecondarySlotField.GetValue(
                    option) as ItemSlotUI;
            RectTransform secondaryRect =
                secondarySlot == null
                    ? null
                    : secondarySlot.transform
                        as RectTransform;
            if (secondaryRect == null
                || !secondarySlot.gameObject
                    .activeInHierarchy
                || !IsPointerInsideRect(
                    secondaryRect))
            {
                return false;
            }

            Item primary;
            Item secondary;
            GetQuickWheelLoadoutItems(
                proxy.Slot,
                out primary,
                out secondary);
            if (primary == null
                || primary.EquipmentType
                    != EquipmentType.Bow)
            {
                return false;
            }

            List<Item> arrows =
                GetAvailableQuickWheelArrowTypes();
            if (arrows.Count < 2)
            {
                return false;
            }

            VirtualWeaponLoadoutData data;
            if (!_virtualWeaponLoadouts.TryGetValue(
                    proxy.Slot,
                    out data)
                || data == null)
            {
                return false;
            }

            int currentIndex = -1;
            for (int i = 0; i < arrows.Count; i++)
            {
                if (string.Equals(
                    GetItemTemplateGuid(arrows[i]),
                    data.QuiverGuid,
                    StringComparison.Ordinal))
                {
                    currentIndex = i;
                    break;
                }
            }
            Item nextArrow =
                arrows[(currentIndex + 1)
                    % arrows.Count];
            data.QuiverGuid =
                GetItemTemplateGuid(nextArrow);

            Hero hero = Hero.Current;
            HeroItems heroItems =
                hero == null ? null : hero.HeroItems;
            if (_currentVirtualWeaponSlot
                    == proxy.Slot
                && heroItems != null)
            {
                HeroLoadout nativeLoadout =
                    heroItems.LoadoutAt(0);
                if (nativeLoadout != null)
                {
                    EquipWeaponFingerprint(
                        nativeLoadout,
                        EquipmentSlotType.Quiver,
                        data.QuiverGuid,
                        heroItems,
                        new HashSet<Item>());
                    if (nativeLoadout.IsEquipped)
                    {
                        nativeLoadout
                            .EquipLoadoutItems();
                    }
                    PrimeStandaloneWeaponTracking(
                        nativeLoadout);
                }
            }

            _lastQuickWheelArrowCycleFrame =
                Time.frameCount;
            _lastQuickWheelArrowCycleSlot =
                proxy.Slot;
            SaveStandaloneEquipmentState(
                writeToArchive: false);
            _nextEquipmentPanelRefreshTime = 0.0f;
            RefreshQuickWheelLoadoutProxy(option);
            ShowQuickWheelLoadoutTooltips(option);
            LogDiagnostic(
                "Quick-wheel arrow cycling assigned "
                + (nextArrow.DisplayName
                    ?? "the next arrow type")
                + " to Glorious loadout "
                + proxy.Slot.ToString(
                    CultureInfo.InvariantCulture)
                + ".");
            return true;
        }

        private static bool IsPointerInsideRect(
            RectTransform rect)
        {
            Canvas canvas =
                rect == null
                    ? null
                    : rect.GetComponentInParent<
                        Canvas>();
            Camera eventCamera =
                canvas != null
                    && canvas.renderMode
                        != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            return rect != null
                && RectTransformUtility
                    .RectangleContainsScreenPoint(
                        rect,
                        Input.mousePosition,
                        eventCamera);
        }

        private static List<Item>
            GetAvailableQuickWheelArrowTypes()
        {
            List<Item> arrows = new List<Item>();
            HashSet<string> templateGuids =
                new HashSet<string>(
                    StringComparer.Ordinal);
            Hero hero = Hero.Current;
            HeroItems heroItems =
                hero == null ? null : hero.HeroItems;
            if (heroItems == null)
            {
                return arrows;
            }

            foreach (Item item in heroItems.Items)
            {
                if (item == null
                    || ((Model)item).HasBeenDiscarded
                    || item.Locked
                    || !item.IsArrow)
                {
                    continue;
                }

                string templateGuid =
                    GetItemTemplateGuid(item);
                if (!string.IsNullOrEmpty(templateGuid)
                    && templateGuids.Add(
                        templateGuid))
                {
                    arrows.Add(item);
                }
            }

            arrows.Sort(
                delegate(Item left, Item right)
                {
                    int leftPriority =
                        left == null
                            || left.Quality == null
                            ? 0
                            : left.Quality.Priority;
                    int rightPriority =
                        right == null
                            || right.Quality == null
                            ? 0
                            : right.Quality.Priority;
                    int priorityComparison =
                        leftPriority.CompareTo(
                            rightPriority);
                    return priorityComparison != 0
                        ? priorityComparison
                        : string.Compare(
                            GetItemTemplateGuid(left),
                            GetItemTemplateGuid(right),
                            StringComparison.Ordinal);
                });
            return arrows;
        }

        internal bool ShowQuickWheelLoadoutTooltips(
            VCQuickLoadout option)
        {
            QuickWheelLoadoutProxy proxy;
            if (!TryGetQuickWheelLoadoutProxy(
                    option,
                    out proxy))
            {
                return false;
            }

            Item primary;
            Item secondary;
            GetQuickWheelLoadoutItems(
                proxy.Slot,
                out primary,
                out secondary);
            VQuickUseWheelUI view = proxy.Owner;
            if (view != null)
            {
                view.QuickItemTooltipUIPrimary
                    .ShowItem(primary);
                view.QuickItemTooltipUISecondary
                    .ShowItem(
                        ReferenceEquals(primary, secondary)
                            ? null
                            : secondary);
            }
            return true;
        }

        internal bool SelectQuickWheelLoadout(
            VCQuickLoadout option)
        {
            QuickWheelLoadoutProxy proxy;
            if (!TryGetQuickWheelLoadoutProxy(
                    option,
                    out proxy))
            {
                return false;
            }

            if (TryCycleQuickWheelArrowsAtPointer(
                    option))
            {
                return true;
            }
            ActivateEquipmentWeaponLoadout(proxy.Slot);
            if (proxy.Owner != null)
            {
                proxy.Owner.Close();
            }
            return true;
        }

        internal void SetHoveredQuickWheelLoadout(
            VCQuickLoadout option,
            bool hovered)
        {
            QuickWheelLoadoutProxy proxy;
            if (!TryGetQuickWheelLoadoutProxy(
                    option,
                    out proxy))
            {
                return;
            }

            if (hovered)
            {
                _hoveredQuickWheelLoadout = option;
            }
            else if (ReferenceEquals(
                _hoveredQuickWheelLoadout,
                option))
            {
                _hoveredQuickWheelLoadout = null;
            }
        }

        internal bool DescribeQuickWheelLoadout(
            VCQuickLoadout option,
            out VCRadialMenuOption<QuickUseWheelUI>
                .OptionDescription description)
        {
            QuickWheelLoadoutProxy proxy;
            if (!TryGetQuickWheelLoadoutProxy(
                    option,
                    out proxy))
            {
                description = default(
                    VCRadialMenuOption<QuickUseWheelUI>
                        .OptionDescription);
                return false;
            }

            description =
                new VCRadialMenuOption<QuickUseWheelUI>
                    .OptionDescription(
                        true,
                        LocTerms.UIItemsEquip.Translate());
            return true;
        }

        internal bool TryGetQuickWheelInitialOption(
            VCRadialMenuOption<QuickUseWheelUI>[] options,
            out VCRadialMenuOption<QuickUseWheelUI> result)
        {
            result = null;
            if (options == null
                || _quickWheelLoadoutProxies.Count == 0)
            {
                return false;
            }

            int desiredSlot =
                _currentVirtualWeaponSlot >= 1
                && _currentVirtualWeaponSlot
                    <= EquipmentWeaponLoadoutCount
                    ? _currentVirtualWeaponSlot
                    : 1;
            for (int i = 0; i < options.Length; i++)
            {
                VCQuickLoadout option =
                    options[i] as VCQuickLoadout;
                QuickWheelLoadoutProxy proxy;
                if (TryGetQuickWheelLoadoutProxy(
                        option,
                        out proxy)
                    && proxy.Slot == desiredSlot)
                {
                    result = option;
                    return true;
                }
            }

            return true;
        }

        internal void RegisterLoadoutsView(object loadoutsView)
        {
            if (!ReferenceEquals(_activeLoadoutsView, loadoutsView))
            {
                ReleaseEquipmentWeaponPanel();
                ReleaseEquipmentQuickPanel();
            }
            _activeLoadoutsView = loadoutsView;
            ScheduleEquipmentPanelBuild();
        }

        internal void ReleaseLoadoutsView(object loadoutsView)
        {
            if (ReferenceEquals(_activeLoadoutsView, loadoutsView))
            {
                ReleaseEquipmentWeaponPanel();
                ReleaseEquipmentQuickPanel();
                _activeLoadoutsView = null;
            }
        }

        internal void RegisterEquipmentView(object equipmentView)
        {
            if (!ReferenceEquals(_activeEquipmentView, equipmentView))
            {
                ReleaseEquipmentQuickPanel();
            }
            _activeEquipmentView = equipmentView;
            ScheduleEquipmentPanelBuild();
        }

        internal void ReleaseEquipmentView(object equipmentView)
        {
            if (ReferenceEquals(_activeEquipmentView, equipmentView))
            {
                ReleaseEquipmentQuickPanel();
                _activeEquipmentView = null;
            }
        }

        private void UpdateCharacterSheetTabLifecycle()
        {
            CharacterSheetUI characterSheet =
                World.Any<CharacterSheetUI>();
            if (characterSheet == null
                || characterSheet.HasBeenDiscarded)
            {
                if (_observedCharacterSheet != null)
                {
                    ForgetEquipmentViews();
                }

                _observedCharacterSheet = null;
                _observedCharacterSheetTab = null;
                return;
            }

            object currentTab =
                characterSheet
                    .TryGetElement<ICharacterSheetTab>();
            if (currentTab == null
                && characterSheet.TabsController != null)
            {
                currentTab = GetPropertyValue(
                    characterSheet.TabsController,
                    "CurrentTab");
            }
            if (!ReferenceEquals(
                    _observedCharacterSheet,
                    characterSheet)
                || !ReferenceEquals(
                    _observedCharacterSheetTab,
                    currentTab))
            {
                _observedCharacterSheet = characterSheet;
                _observedCharacterSheetTab = currentTab;
            }

            if (!ReferenceEquals(
                characterSheet.CurrentType,
                CharacterSheetTabType.Inventory))
            {
                ForgetEquipmentViews();
            }

            HideInactiveCharacterSheetTabViews(
                characterSheet,
                currentTab);
        }

        private void ScheduleEquipmentPanelBuild()
        {
            _equipmentPanelBuildNotBeforeFrame =
                Time.frameCount + 1;
            _nextEquipmentPanelBuildRetryTime = 0.0f;
        }

        private void ForgetEquipmentViews()
        {
            object loadoutsView = _activeLoadoutsView;
            object equipmentView = _activeEquipmentView;
            ReleaseEquipmentWeaponPanel();
            ReleaseEquipmentQuickPanel();
            HideEquipmentCanvasGroup(
                loadoutsView,
                "loadoutsCanvasGroup");
            HideEquipmentCanvasGroup(
                equipmentView,
                "quickAndAutoSlotsCanvasGroup");
            HideEquipmentCanvasGroup(
                equipmentView,
                "armorSlotsCanvasGroup");
            HideEquipmentCanvasGroup(
                equipmentView,
                "armorWeightCanvasGroup");
            HideEquipmentCanvasGroup(
                equipmentView,
                "armorWeightInfoCanvasGroup");
            DeactivateEquipmentView(loadoutsView);
            DeactivateEquipmentView(equipmentView);
            _activeLoadoutsView = null;
            _activeEquipmentView = null;
        }

        private static void DeactivateEquipmentView(object view)
        {
            Component component = view as Component;
            if (component != null
                && component.gameObject != null)
            {
                component.gameObject.SetActive(false);
            }
        }

        private static void HideEquipmentCanvasGroup(
            object view,
            string fieldName)
        {
            CanvasGroup canvasGroup =
                GetFieldValue(view, fieldName) as CanvasGroup;
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0.0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static void HideInactiveCharacterSheetTabViews(
            CharacterSheetUI characterSheet,
            object currentTab)
        {
            if (characterSheet == null
                || characterSheet.ContentHost == null
                || currentTab == null)
            {
                return;
            }

            View[] views =
                characterSheet.ContentHost
                    .GetComponentsInChildren<View>(true);
            for (int i = 0; i < views.Length; i++)
            {
                View view = views[i];
                ICharacterSheetTab tab =
                    view == null
                        ? null
                        : view.GenericTarget
                            as ICharacterSheetTab;
                if (tab != null
                    && !ReferenceEquals(tab, currentTab)
                    && view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }

        private void PruneInactiveEquipmentViews()
        {
            if (_activeLoadoutsView != null
                && !IsEquipmentViewAlive(_activeLoadoutsView))
            {
                ReleaseEquipmentWeaponPanel();
                ReleaseEquipmentQuickPanel();
                _activeLoadoutsView = null;
            }
            else if (_activeLoadoutsView != null
                && !IsEquipmentPanelVisible(
                    _activeLoadoutsView,
                    "loadoutsCanvasGroup"))
            {
                ReleaseEquipmentWeaponPanel();
                ReleaseEquipmentQuickPanel();
            }

            if (_activeEquipmentView != null
                && !IsEquipmentViewAlive(_activeEquipmentView))
            {
                ReleaseEquipmentQuickPanel();
                _activeEquipmentView = null;
            }
            else if (_activeEquipmentView != null
                && !IsEquipmentPanelVisible(
                    _activeEquipmentView,
                    "quickAndAutoSlotsCanvasGroup"))
            {
                ReleaseEquipmentQuickPanel();
            }

            if (_activeLoadoutsView != null
                && _activeEquipmentView != null
                && !EquipmentViewsShareTarget())
            {
                ReleaseEquipmentQuickPanel();
            }
        }

        private static bool IsEquipmentViewAlive(object view)
        {
            Component component = view as Component;
            if (component == null
                || component.gameObject == null
                || !component.gameObject.activeInHierarchy)
            {
                return false;
            }

            try
            {
                object target = GetPropertyValue(view, "Target");
                return target != null
                    && !TryReadBool(
                        target,
                        "HasBeenDiscarded");
            }
            catch
            {
                return false;
            }
        }

        private static bool IsEquipmentPanelVisible(
            object view,
            string canvasGroupField)
        {
            if (!IsEquipmentViewAlive(view))
            {
                return false;
            }

            CanvasGroup canvasGroup =
                GetFieldValue(
                    view,
                    canvasGroupField) as CanvasGroup;
            return canvasGroup == null
                || (canvasGroup.gameObject.activeInHierarchy
                    && canvasGroup.alpha > 0.01f);
        }

        private bool EquipmentViewsShareTarget()
        {
            try
            {
                object loadoutsTarget =
                    GetPropertyValue(_activeLoadoutsView, "Target");
                object equipmentTarget =
                    GetPropertyValue(_activeEquipmentView, "Target");
                return loadoutsTarget != null
                    && ReferenceEquals(
                        loadoutsTarget,
                        equipmentTarget);
            }
            catch
            {
                return false;
            }
        }

        private void RebuildEquipmentWeaponPanel()
        {
            try
            {
                RebuildEquipmentWeaponPanelCore();
            }
            catch (Exception exception)
            {
                ReleaseEquipmentWeaponPanel();
                Logger.LogWarning(
                    "Could not build Glorious's weapon-loadout panel; the vanilla rows were restored. "
                    + exception.GetBaseException().Message);
            }
        }

        private void RebuildEquipmentWeaponPanelCore()
        {
            ReleaseEquipmentWeaponPanel();
            if (!ShouldControlEquipmentWeaponLoadouts()
                || _activeLoadoutsView == null
                || !IsEquipmentPanelVisible(
                    _activeLoadoutsView,
                    "loadoutsCanvasGroup"))
            {
                return;
            }

            Component viewComponent = _activeLoadoutsView as Component;
            if (viewComponent == null)
            {
                return;
            }

            Type loadoutType = AccessTools.TypeByName(
                "Awaken.TG.Main.Heroes.CharacterSheet.Items.Loadouts.VCLoadout");
            if (loadoutType == null)
            {
                return;
            }

            Component[] loadouts =
                viewComponent.GetComponentsInChildren(loadoutType, true);
            Component activeLoadout = null;
            List<VanillaLoadoutVisibilitySnapshot> vanillaRows =
                new List<VanillaLoadoutVisibilitySnapshot>();
            for (int i = 0; i < loadouts.Length; i++)
            {
                Component loadout = loadouts[i];
                int index;
                if (loadout == null
                    || !TryReadInt(loadout, "LoadoutIndex", out index))
                {
                    continue;
                }

                if (index == 0)
                {
                    activeLoadout = loadout;
                    continue;
                }

                if (index > 0 && index < 4)
                {
                    vanillaRows.Add(
                        new VanillaLoadoutVisibilitySnapshot(
                            loadout.gameObject,
                            loadout.gameObject.activeSelf));
                    loadout.gameObject.SetActive(false);
                }
            }

            RectTransform activeRect = activeLoadout == null
                ? null
                : activeLoadout.transform as RectTransform;
            if (activeRect == null || activeRect.parent == null)
            {
                for (int i = 0; i < vanillaRows.Count; i++)
                {
                    vanillaRows[i].Restore();
                }
                return;
            }

            Canvas.ForceUpdateCanvases();
            float activeWidth = activeRect.rect.width > 1.0f
                ? activeRect.rect.width
                : 210.0f;
            float activeHeight = activeRect.rect.height > 1.0f
                ? activeRect.rect.height
                : 82.0f;
            float rowScale = _equipmentWeaponRowScale == null
                ? 0.72f
                : Mathf.Clamp(_equipmentWeaponRowScale.Value, 0.45f, 1.0f);
            float spacing = _equipmentWeaponRowSpacing == null
                ? 4.0f
                : Mathf.Clamp(_equipmentWeaponRowSpacing.Value, 0.0f, 30.0f);
            float rowHeight = Mathf.Max(34.0f, activeHeight * rowScale);
            float rootHeight = EquipmentWeaponLoadoutCount * rowHeight
                + (EquipmentWeaponLoadoutCount - 1) * spacing;

            List<EquipmentWeaponRow> rows =
                new List<EquipmentWeaponRow>();
            _equipmentWeaponPanel = new EquipmentWeaponPanel(
                _activeLoadoutsView,
                null,
                vanillaRows,
                rows);
            GameObject rootObject = new GameObject(
                "GloriousUI_EquipmentWeaponLoadouts",
                typeof(RectTransform),
                typeof(LayoutElement));
            _equipmentWeaponPanel.Root = rootObject;
            RectTransform root = rootObject.transform as RectTransform;
            root.SetParent(activeRect.parent, false);
            rootObject.GetComponent<LayoutElement>().ignoreLayout = true;
            Vector2 activePivotAnchor = new Vector2(
                Mathf.Lerp(
                    activeRect.anchorMin.x,
                    activeRect.anchorMax.x,
                    activeRect.pivot.x),
                Mathf.Lerp(
                    activeRect.anchorMin.y,
                    activeRect.anchorMax.y,
                    activeRect.pivot.y));
            root.anchorMin = activePivotAnchor;
            root.anchorMax = activePivotAnchor;
            root.pivot = new Vector2(0.5f, 1.0f);
            root.sizeDelta = new Vector2(activeWidth, rootHeight);
            root.anchoredPosition = new Vector2(
                activeRect.anchoredPosition.x
                    + (0.5f - activeRect.pivot.x) * activeWidth,
                activeRect.anchoredPosition.y
                    - activeRect.pivot.y * activeHeight
                    - spacing);

            TMP_Text textTemplate =
                GetEquipmentPanelTextTemplate();
            for (int i = 0; i < EquipmentWeaponLoadoutCount; i++)
            {
                rows.Add(
                    CreateEquipmentWeaponRow(
                        root,
                        i + 1,
                        i * (rowHeight + spacing),
                        rowHeight,
                        textTemplate));
            }

            _nextEquipmentPanelRefreshTime = 0.0f;
        }

        private EquipmentWeaponRow CreateEquipmentWeaponRow(
            RectTransform parent,
            int slot,
            float topOffset,
            float height,
            TMP_Text textTemplate)
        {
            GameObject rowObject = new GameObject(
                "WeaponLoadout" + slot.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rowRect = rowObject.transform as RectTransform;
            rowRect.SetParent(parent, false);
            rowRect.anchorMin = new Vector2(0.0f, 1.0f);
            rowRect.anchorMax = new Vector2(1.0f, 1.0f);
            rowRect.pivot = new Vector2(0.5f, 1.0f);
            rowRect.sizeDelta = new Vector2(0.0f, height);
            rowRect.anchoredPosition = new Vector2(0.0f, -topOffset);

            Image background = rowObject.GetComponent<Image>();
            background.color = new Color(0.055f, 0.05f, 0.045f, 0.92f);
            int capturedSlot = slot;
            EquipmentButtonVisualState buttonState =
                AddEquipmentPanelButton(
                rowObject,
                background,
                delegate
                {
                    ActivateEquipmentWeaponLoadout(capturedSlot);
                },
                delegate
                {
                    _nextEquipmentPanelRefreshTime = 0.0f;
                });

            GameObject accentObject = CreateEquipmentPanelImage(
                rowRect,
                "Selection",
                new Color(0.72f, 0.45f, 0.15f, 1.0f));
            RectTransform accentRect =
                accentObject.transform as RectTransform;
            accentRect.anchorMin = new Vector2(0.0f, 0.0f);
            accentRect.anchorMax = new Vector2(0.0f, 1.0f);
            accentRect.pivot = new Vector2(0.0f, 0.5f);
            accentRect.sizeDelta = new Vector2(4.0f, 0.0f);
            accentRect.anchoredPosition = Vector2.zero;

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform labelRect =
                labelObject.transform as RectTransform;
            labelRect.SetParent(rowRect, false);
            labelRect.anchorMin = new Vector2(0.0f, 0.0f);
            labelRect.anchorMax = new Vector2(0.0f, 1.0f);
            labelRect.pivot = new Vector2(0.0f, 0.5f);
            labelRect.sizeDelta = new Vector2(62.0f, 0.0f);
            labelRect.anchoredPosition = new Vector2(6.0f, 0.0f);
            TMP_Text label =
                labelObject.GetComponent<TMP_Text>();
            ApplyEquipmentPanelTextStyle(
                label,
                textTemplate);
            label.fontSize = Mathf.Clamp(
                height * 0.15f,
                8.0f,
                11.0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 7.0f;
            label.fontSizeMax = label.fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.color = new Color(0.78f, 0.73f, 0.65f, 1.0f);
            label.text = "LOADOUT\n"
                + slot.ToString(CultureInfo.InvariantCulture);
            label.raycastTarget = false;

            float iconSize = Mathf.Max(24.0f, height - 8.0f);
            EquipmentIconSlot mainIcon = CreateEquipmentWeaponIcon(
                rowRect,
                "MainHand",
                72.0f,
                iconSize);
            EquipmentIconSlot offIcon = CreateEquipmentWeaponIcon(
                rowRect,
                "Secondary",
                76.0f + iconSize,
                iconSize);

            return new EquipmentWeaponRow(
                slot,
                background,
                accentObject,
                label,
                mainIcon,
                offIcon,
                buttonState);
        }

        private EquipmentButtonVisualState
            AddEquipmentPanelButton(
            GameObject gameObject,
            Image targetGraphic,
            Action onClick,
            Action onFocusChanged)
        {
            if (_arButtonType != null)
            {
                EquipmentButtonVisualState state =
                    new EquipmentButtonVisualState(
                        onFocusChanged);
                Component button =
                    gameObject.AddComponent(_arButtonType);
                _arButtonTransitionProperty.SetValue(
                    button,
                    Enum.ToObject(
                        _arButtonTransitionProperty.PropertyType,
                        0),
                    null);
                _arButtonTargetGraphicProperty.SetValue(
                    button,
                    targetGraphic,
                    null);
                _arButtonHasGraphicField.SetValue(
                    button,
                    true);
                _arButtonOnClickEvent.AddEventHandler(
                    button,
                    onClick);
                if (_arButtonOnHoverEvent != null)
                {
                    _arButtonOnHoverEvent.AddEventHandler(
                        button,
                        new Action<bool>(state.SetHovered));
                }
                if (_arButtonOnSelectedEvent != null)
                {
                    _arButtonOnSelectedEvent.AddEventHandler(
                        button,
                        new Action<bool>(state.SetSelected));
                }
                return state;
            }

            throw new MissingMemberException(
                "The game's native ARButton input component is unavailable.");
        }

        private static GameObject CreateEquipmentPanelImage(
            RectTransform parent,
            string name,
            Color color)
        {
            GameObject imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return imageObject;
        }

        private static EquipmentIconSlot CreateEquipmentWeaponIcon(
            RectTransform parent,
            string name,
            float x,
            float size)
        {
            GameObject frameObject = CreateEquipmentPanelImage(
                parent,
                name + "Frame",
                new Color(0.15f, 0.14f, 0.13f, 0.96f));
            RectTransform frameRect =
                frameObject.transform as RectTransform;
            frameRect.anchorMin = new Vector2(0.0f, 0.5f);
            frameRect.anchorMax = new Vector2(0.0f, 0.5f);
            frameRect.pivot = new Vector2(0.0f, 0.5f);
            frameRect.sizeDelta = new Vector2(size, size);
            frameRect.anchoredPosition = new Vector2(x, 0.0f);

            GameObject iconObject = CreateEquipmentPanelImage(
                frameRect,
                name,
                new Color(1.0f, 1.0f, 1.0f, 0.0f));
            RectTransform iconRect =
                iconObject.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0.08f, 0.08f);
            iconRect.anchorMax = new Vector2(0.92f, 0.92f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            return new EquipmentIconSlot(icon);
        }

        private TMP_Text GetEquipmentPanelTextTemplate()
        {
            TMP_Text template =
                GetFieldValue(
                    _activeLoadoutsView,
                    "loadoutWeaponTitle") as TMP_Text;
            if (template != null)
            {
                return template;
            }

            Component loadoutsView =
                _activeLoadoutsView as Component;
            return loadoutsView == null
                ? null
                : loadoutsView.GetComponentInChildren<TMP_Text>(
                    true);
        }

        private static void ApplyEquipmentPanelTextStyle(
            TMP_Text target,
            TMP_Text template)
        {
            if (template != null)
            {
                target.font = template.font;
                target.fontSharedMaterial =
                    template.fontSharedMaterial;
                target.fontStyle = template.fontStyle;
                target.fontWeight = template.fontWeight;
                target.characterSpacing =
                    template.characterSpacing;
                target.wordSpacing = template.wordSpacing;
                target.lineSpacing = template.lineSpacing;
                target.extraPadding = template.extraPadding;
                return;
            }

            target.font = TMP_Settings.defaultFontAsset;
        }

        private void EnsureEquipmentPanels()
        {
            if (Time.frameCount
                    < _equipmentPanelBuildNotBeforeFrame
                || Time.unscaledTime
                < _nextEquipmentPanelBuildRetryTime)
            {
                return;
            }

            bool needsWeaponPanel =
                _equipmentWeaponPanel == null
                && _activeLoadoutsView != null
                && IsEquipmentPanelVisible(
                    _activeLoadoutsView,
                    "loadoutsCanvasGroup")
                && ShouldControlEquipmentWeaponLoadouts();
            bool needsQuickPanel =
                _equipmentQuickPanel == null
                && _activeLoadoutsView != null
                && _activeEquipmentView != null
                && IsEquipmentPanelVisible(
                    _activeLoadoutsView,
                    "loadoutsCanvasGroup")
                && IsEquipmentPanelVisible(
                    _activeEquipmentView,
                    "quickAndAutoSlotsCanvasGroup")
                && EquipmentViewsShareTarget()
                && ShouldControlEquipmentQuickSlots();
            if (!needsWeaponPanel && !needsQuickPanel)
            {
                return;
            }

            _nextEquipmentPanelBuildRetryTime =
                Time.unscaledTime + 1.0f;
            if (needsWeaponPanel)
            {
                RebuildEquipmentWeaponPanel();
            }
            if (needsQuickPanel)
            {
                RebuildEquipmentQuickPanel();
            }
        }

        private void UpdateEquipmentWeaponPanel()
        {
            if ((_equipmentWeaponPanel == null
                    && _equipmentQuickPanel == null)
                || Time.unscaledTime < _nextEquipmentPanelRefreshTime)
            {
                return;
            }

            _nextEquipmentPanelRefreshTime = Time.unscaledTime
                + EquipmentPanelRefreshIntervalSeconds;
            EquipmentDisplayItemLookup displayItems =
                BuildEquipmentDisplayItemLookup();
            if (_equipmentWeaponPanel != null)
            {
                try
                {
                    RefreshEquipmentWeaponPanel(
                        displayItems);
                }
                catch (Exception exception)
                {
                    ReleaseEquipmentWeaponPanel();
                    Logger.LogWarning(
                        "Glorious's weapon-loadout panel stopped refreshing and the vanilla rows were restored. "
                        + exception.GetBaseException().Message);
                }
            }
            if (_equipmentQuickPanel != null)
            {
                try
                {
                    RefreshEquipmentQuickPanel(
                        displayItems);
                }
                catch (Exception exception)
                {
                    ReleaseEquipmentQuickPanel();
                    Logger.LogWarning(
                        "Glorious's quick-slot panel stopped refreshing and the vanilla slots were restored. "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void RefreshEquipmentWeaponPanel(
            EquipmentDisplayItemLookup displayItems)
        {
            EquipmentWeaponPanel panel = _equipmentWeaponPanel;
            if (panel == null)
            {
                return;
            }

            for (int i = 0; i < panel.Rows.Count; i++)
            {
                EquipmentWeaponRow row = panel.Rows[i];
                bool selected =
                    row.Slot == _currentVirtualWeaponSlot;
                bool focused = row.ButtonState.Focused;
                row.Selection.SetActive(selected);
                row.Background.color =
                    selected && focused
                        ? new Color(
                            0.21f,
                            0.14f,
                            0.065f,
                            0.98f)
                        : selected
                            ? new Color(
                                0.16f,
                                0.105f,
                                0.045f,
                                0.96f)
                            : focused
                                ? new Color(
                                    0.115f,
                                    0.09f,
                                    0.055f,
                                    0.96f)
                                : new Color(
                                    0.055f,
                                    0.05f,
                                    0.045f,
                                    0.92f);
                row.Label.color =
                    selected
                        ? new Color(
                            0.95f,
                            0.72f,
                            0.30f,
                            1.0f)
                        : focused
                            ? new Color(
                                0.92f,
                                0.84f,
                                0.68f,
                                1.0f)
                            : new Color(
                                0.78f,
                                0.73f,
                                0.65f,
                                1.0f);

                VirtualWeaponLoadoutData loadout;
                _virtualWeaponLoadouts.TryGetValue(
                    row.Slot,
                    out loadout);
                string mainGuid =
                    loadout == null ? null : loadout.MainHandGuid;
                object mainItem =
                    FindEquipmentWeaponDisplayItem(
                        mainGuid,
                        true,
                        displayItems);
                bool ranged =
                    mainItem != null
                    && TryReadBool(mainItem, "IsRanged");
                string secondaryGuid =
                    loadout == null
                        ? null
                        : ranged
                            ? loadout.QuiverGuid
                            : loadout.OffHandGuid;
                ApplyEquipmentItemIcon(
                    row.MainHand,
                    mainItem,
                    !string.IsNullOrEmpty(mainGuid));
                ApplyEquipmentItemIcon(
                    row.Secondary,
                    FindEquipmentWeaponDisplayItem(
                        secondaryGuid,
                        false,
                        displayItems),
                    !string.IsNullOrEmpty(secondaryGuid));
            }
        }

        private object FindEquipmentWeaponDisplayItem(
            string templateGuid,
            bool mainHand,
            EquipmentDisplayItemLookup displayItems)
        {
            Hero hero = Hero.Current;
            HeroItems heroItems =
                hero == null ? null : hero.HeroItems;
            if (heroItems != null)
            {
                if (mainHand
                    && string.Equals(
                        templateGuid,
                        FistsMainHandGuid,
                        StringComparison.Ordinal))
                {
                    return heroItems.GetMainHandFist();
                }
                if (!mainHand
                    && string.Equals(
                        templateGuid,
                        FistsOffHandGuid,
                        StringComparison.Ordinal))
                {
                    return heroItems.GetOffHandFist();
                }
            }

            return displayItems == null
                ? FindInventoryItemByReference(
                    null,
                    templateGuid,
                    null)
                : displayItems.Find(
                    null,
                    templateGuid);
        }

        private void ActivateEquipmentWeaponLoadout(int slot)
        {
            if (slot < 1 || slot > EquipmentWeaponLoadoutCount)
            {
                return;
            }
            if (_lastWeaponLoadoutActivationFrame
                    == Time.frameCount
                && _lastWeaponLoadoutActivationSlot == slot)
            {
                return;
            }

            try
            {
                _lastWeaponLoadoutActivationFrame =
                    Time.frameCount;
                _lastWeaponLoadoutActivationSlot = slot;
                ActivateStandaloneWeaponLoadout(
                    slot,
                    captureCurrent: true);
                _nextEquipmentPanelRefreshTime = 0.0f;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not activate Equipment weapon loadout "
                    + slot.ToString(CultureInfo.InvariantCulture)
                    + ": "
                    + exception.GetBaseException().Message);
            }
        }

        private void ApplyEquipmentItemIcon(
            EquipmentIconSlot iconSlot,
            object item,
            bool expectedItem)
        {
            if (iconSlot == null || iconSlot.Image == null)
            {
                return;
            }

            if (ReferenceEquals(iconSlot.Item, item)
                && iconSlot.SpriteReference != null)
            {
                return;
            }

            iconSlot.Release();
            iconSlot.Item = item;
            iconSlot.Image.sprite = null;
            iconSlot.Image.color = expectedItem && item == null
                ? new Color(0.72f, 0.16f, 0.16f, 0.55f)
                : new Color(1.0f, 1.0f, 1.0f, 0.0f);
            if (item == null)
            {
                return;
            }

            object icon = GetPropertyValue(item, "Icon");
            if (icon == null || !TryReadBool(icon, "IsSet"))
            {
                return;
            }

            object spriteReference = InvokeMethod(icon, "Get", null);
            if (spriteReference == null)
            {
                return;
            }

            MethodInfo setSprite =
                FindSetSpriteMethod(spriteReference.GetType());
            if (setSprite == null)
            {
                ReleaseSpriteReference(spriteReference);
                return;
            }

            iconSlot.SpriteReference = spriteReference;
            iconSlot.Image.color = Color.white;
            setSprite.Invoke(
                spriteReference,
                new object[] { iconSlot.Image, null });
        }

        private void ReleaseEquipmentWeaponPanel()
        {
            EquipmentWeaponPanel panel = _equipmentWeaponPanel;
            _equipmentWeaponPanel = null;
            if (panel != null)
            {
                panel.Release();
            }
        }

        private void RebuildEquipmentQuickPanel()
        {
            try
            {
                RebuildEquipmentQuickPanelCore();
            }
            catch (Exception exception)
            {
                ReleaseEquipmentQuickPanel();
                if (!_equipmentQuickPanelBuildFailureLogged)
                {
                    _equipmentQuickPanelBuildFailureLogged = true;
                    Logger.LogWarning(
                        "Could not build Glorious's quick-slot panel; the vanilla slots were restored. "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void RebuildEquipmentQuickPanelCore()
        {
            ReleaseEquipmentQuickPanel();
            if (!ShouldControlEquipmentQuickSlots()
                || _activeEquipmentView == null
                || _activeLoadoutsView == null
                || !IsEquipmentPanelVisible(
                    _activeEquipmentView,
                    "quickAndAutoSlotsCanvasGroup")
                || !IsEquipmentPanelVisible(
                    _activeLoadoutsView,
                    "loadoutsCanvasGroup")
                || !EquipmentViewsShareTarget())
            {
                return;
            }

            Component viewComponent =
                _activeEquipmentView as Component;
            Type defaultSlotType =
                AccessTools.TypeByName(DefaultEquipmentSlotViewTypeName);
            if (viewComponent == null)
            {
                throw new MissingReferenceException(
                    "The active Equipment view is no longer valid.");
            }
            if (defaultSlotType == null)
            {
                throw new TypeLoadException(
                    DefaultEquipmentSlotViewTypeName);
            }

            Component quickSlotsHost =
                GetFieldValue(
                    _activeEquipmentView,
                    "quickAndAutoSlotsCanvasGroup") as Component;
            Component slotSearchRoot =
                quickSlotsHost ?? viewComponent;
            Component[] slots =
                slotSearchRoot.GetComponentsInChildren(
                    defaultSlotType,
                    true);
            Component quickSlot2;
            Component quickSlot3;
            FindEquipmentManualQuickSlots(
                slots,
                out quickSlot2,
                out quickSlot3);
            if (quickSlot2 == null
                && !ReferenceEquals(
                    slotSearchRoot,
                    viewComponent))
            {
                slots =
                    viewComponent.GetComponentsInChildren(
                        defaultSlotType,
                        true);
                slotSearchRoot = viewComponent;
                FindEquipmentManualQuickSlots(
                    slots,
                    out quickSlot2,
                    out quickSlot3);
            }

            RectTransform backingRect = quickSlot2 == null
                ? null
                : quickSlot2.transform as RectTransform;
            if (backingRect == null || backingRect.parent == null)
            {
                throw new InvalidOperationException(
                    "The Equipment view's QuickSlot2 backing control could not be found among "
                    + slots.Length.ToString(
                        CultureInfo.InvariantCulture)
                    + " default slots under "
                    + slotSearchRoot.gameObject.name
                    + ".");
            }

            List<CanvasGroupVisibilitySnapshot> hiddenSlots =
                new List<CanvasGroupVisibilitySnapshot>();
            List<EquipmentQuickSlot> quickSlots =
                new List<EquipmentQuickSlot>();
            _equipmentQuickSlotBackingView = quickSlot2;
            _equipmentQuickPanel = new EquipmentQuickPanel(
                _activeEquipmentView,
                null,
                hiddenSlots,
                quickSlots);
            hiddenSlots.Add(
                HideEquipmentBackingSlot(quickSlot2.gameObject));
            if (quickSlot3 != null)
            {
                hiddenSlots.Add(
                    HideEquipmentBackingSlot(quickSlot3.gameObject));
            }

            Canvas.ForceUpdateCanvases();
            float cellWidth = backingRect.rect.width > 1.0f
                ? backingRect.rect.width
                : 78.0f;
            float cellHeight = backingRect.rect.height > 1.0f
                ? backingRect.rect.height
                : 78.0f;
            float horizontalSpacing = Mathf.Max(6.0f, cellWidth * 0.10f);
            float verticalSpacing = Mathf.Max(5.0f, cellHeight * 0.08f);
            float rootWidth = cellWidth * 2.0f + horizontalSpacing;
            float rootHeight = cellHeight * 3.0f
                + verticalSpacing * 2.0f;
            float left = backingRect.anchoredPosition.x
                - backingRect.pivot.x * cellWidth;
            float top = backingRect.anchoredPosition.y
                + (1.0f - backingRect.pivot.y) * cellHeight;

            GameObject rootObject = new GameObject(
                "GloriousUI_EquipmentQuickSlots",
                typeof(RectTransform),
                typeof(LayoutElement));
            _equipmentQuickPanel.Root = rootObject;
            RectTransform root = rootObject.transform as RectTransform;
            root.SetParent(backingRect.parent, false);
            rootObject.GetComponent<LayoutElement>().ignoreLayout = true;
            Vector2 backingPivotAnchor = new Vector2(
                Mathf.Lerp(
                    backingRect.anchorMin.x,
                    backingRect.anchorMax.x,
                    backingRect.pivot.x),
                Mathf.Lerp(
                    backingRect.anchorMin.y,
                    backingRect.anchorMax.y,
                    backingRect.pivot.y));
            root.anchorMin = backingPivotAnchor;
            root.anchorMax = backingPivotAnchor;
            root.pivot = new Vector2(0.5f, 1.0f);
            root.sizeDelta = new Vector2(rootWidth, rootHeight);
            root.anchoredPosition = new Vector2(
                left + rootWidth * 0.5f,
                top);

            TMP_Text textTemplate =
                GetEquipmentPanelTextTemplate();
            for (int i = 0; i < EquipmentQuickSlotCount; i++)
            {
                int column = i % 2;
                int row = i / 2;
                quickSlots.Add(
                    CreateEquipmentQuickSlot(
                        root,
                        i + 1,
                        column * (cellWidth + horizontalSpacing),
                        row * (cellHeight + verticalSpacing),
                        cellWidth,
                        cellHeight,
                        textTemplate));
            }

            _nextEquipmentPanelRefreshTime = 0.0f;
            _equipmentQuickPanelBuildFailureLogged = false;
        }

        private void FindEquipmentManualQuickSlots(
            Component[] slots,
            out Component quickSlot2,
            out Component quickSlot3)
        {
            quickSlot2 = null;
            quickSlot3 = null;
            for (int i = 0; i < slots.Length; i++)
            {
                Component slot = slots[i];
                object type = GetPropertyValue(slot, "Type");
                if (ReferenceEquals(type, _quickSlot2)
                    || SlotNameEquals(type, "QuickSlot2"))
                {
                    quickSlot2 = slot;
                }
                else if (ReferenceEquals(type, _quickSlot3)
                    || SlotNameEquals(type, "QuickSlot3"))
                {
                    quickSlot3 = slot;
                }
            }
        }

        private static CanvasGroupVisibilitySnapshot
            HideEquipmentBackingSlot(GameObject gameObject)
        {
            CanvasGroup canvasGroup =
                gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            CanvasGroupVisibilitySnapshot snapshot =
                new CanvasGroupVisibilitySnapshot(
                    canvasGroup,
                    gameObject.GetComponentsInChildren<
                        Selectable>(true));
            canvasGroup.alpha = 0.0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            snapshot.DisableSelectables();
            return snapshot;
        }

        private EquipmentQuickSlot CreateEquipmentQuickSlot(
            RectTransform parent,
            int slot,
            float x,
            float y,
            float width,
            float height,
            TMP_Text textTemplate)
        {
            GameObject slotObject = new GameObject(
                "QuickSlot" + slot.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform slotRect =
                slotObject.transform as RectTransform;
            slotRect.SetParent(parent, false);
            slotRect.anchorMin = new Vector2(0.0f, 1.0f);
            slotRect.anchorMax = new Vector2(0.0f, 1.0f);
            slotRect.pivot = new Vector2(0.0f, 1.0f);
            slotRect.sizeDelta = new Vector2(width, height);
            slotRect.anchoredPosition = new Vector2(x, -y);

            Image background = slotObject.GetComponent<Image>();
            background.color =
                new Color(0.055f, 0.05f, 0.045f, 0.94f);
            int capturedSlot = slot;
            EquipmentButtonVisualState buttonState =
                AddEquipmentPanelButton(
                slotObject,
                background,
                delegate
                {
                    EditEquipmentQuickSlot(capturedSlot);
                },
                delegate
                {
                    _nextEquipmentPanelRefreshTime = 0.0f;
                });

            GameObject selection = CreateEquipmentPanelImage(
                slotRect,
                "Selection",
                new Color(0.72f, 0.45f, 0.15f, 1.0f));
            RectTransform selectionRect =
                selection.transform as RectTransform;
            selectionRect.anchorMin = new Vector2(0.0f, 0.0f);
            selectionRect.anchorMax = new Vector2(1.0f, 0.0f);
            selectionRect.pivot = new Vector2(0.5f, 0.0f);
            selectionRect.sizeDelta = new Vector2(0.0f, 4.0f);
            selectionRect.anchoredPosition = Vector2.zero;

            EquipmentIconSlot iconSlot =
                CreateEquipmentQuickSlotIcon(slotRect);

            GameObject quantityObject = new GameObject(
                "Quantity",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform quantityRect =
                quantityObject.transform as RectTransform;
            quantityRect.SetParent(slotRect, false);
            quantityRect.anchorMin = new Vector2(0.0f, 0.0f);
            quantityRect.anchorMax = new Vector2(1.0f, 0.0f);
            quantityRect.pivot = new Vector2(0.5f, 0.0f);
            quantityRect.sizeDelta = new Vector2(-8.0f, 22.0f);
            quantityRect.anchoredPosition =
                new Vector2(0.0f, 3.0f);
            TMP_Text quantity =
                quantityObject.GetComponent<TMP_Text>();
            ApplyEquipmentPanelTextStyle(
                quantity,
                textTemplate);
            quantity.fontSize = Mathf.Clamp(
                height * 0.17f,
                10.0f,
                15.0f);
            quantity.fontStyle |= FontStyles.Bold;
            quantity.alignment = TextAlignmentOptions.BottomRight;
            quantity.textWrappingMode = TextWrappingModes.NoWrap;
            quantity.color = new Color(
                0.92f,
                0.89f,
                0.83f,
                1.0f);
            quantity.raycastTarget = false;

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform labelRect =
                labelObject.transform as RectTransform;
            labelRect.SetParent(slotRect, false);
            labelRect.anchorMin = new Vector2(0.0f, 1.0f);
            labelRect.anchorMax = new Vector2(1.0f, 1.0f);
            labelRect.pivot = new Vector2(0.5f, 1.0f);
            labelRect.sizeDelta = new Vector2(0.0f, 20.0f);
            labelRect.anchoredPosition = Vector2.zero;
            TMP_Text label =
                labelObject.GetComponent<TMP_Text>();
            ApplyEquipmentPanelTextStyle(
                label,
                textTemplate);
            label.fontSize = Mathf.Clamp(
                height * 0.15f,
                9.0f,
                13.0f);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color =
                new Color(0.78f, 0.73f, 0.65f, 0.96f);
            label.text = "Q"
                + slot.ToString(CultureInfo.InvariantCulture);
            label.raycastTarget = false;

            return new EquipmentQuickSlot(
                slot,
                background,
                selection,
                label,
                quantity,
                iconSlot,
                buttonState);
        }

        private static EquipmentIconSlot
            CreateEquipmentQuickSlotIcon(RectTransform parent)
        {
            GameObject iconObject = CreateEquipmentPanelImage(
                parent,
                "Item",
                new Color(1.0f, 1.0f, 1.0f, 0.0f));
            RectTransform iconRect =
                iconObject.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0.12f, 0.12f);
            iconRect.anchorMax = new Vector2(0.88f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            Image image = iconObject.GetComponent<Image>();
            image.preserveAspect = true;
            return new EquipmentIconSlot(image);
        }

        private void RefreshEquipmentQuickPanel(
            EquipmentDisplayItemLookup displayItems)
        {
            EquipmentQuickPanel panel = _equipmentQuickPanel;
            if (panel == null)
            {
                return;
            }

            for (int i = 0; i < panel.Slots.Count; i++)
            {
                EquipmentQuickSlot quickSlot = panel.Slots[i];
                bool selected =
                    quickSlot.Slot == _selectedEquipmentQuickSlot;
                bool focused =
                    quickSlot.ButtonState.Focused;
                quickSlot.Selection.SetActive(selected);
                quickSlot.Background.color =
                    selected && focused
                        ? new Color(
                            0.21f,
                            0.14f,
                            0.065f,
                            0.98f)
                        : selected
                            ? new Color(
                                0.16f,
                                0.105f,
                                0.045f,
                                0.96f)
                            : focused
                                ? new Color(
                                    0.115f,
                                    0.09f,
                                    0.055f,
                                    0.96f)
                                : new Color(
                                    0.055f,
                                    0.05f,
                                    0.045f,
                                    0.94f);
                quickSlot.Label.color =
                    selected
                        ? new Color(
                            0.95f,
                            0.72f,
                            0.30f,
                            1.0f)
                        : focused
                            ? new Color(
                                0.92f,
                                0.84f,
                                0.68f,
                                1.0f)
                            : new Color(
                                0.78f,
                                0.73f,
                                0.65f,
                                0.96f);

                string itemGuid =
                    GetEquipmentQuickSlotItemGuid(quickSlot.Slot);
                object item =
                    displayItems == null
                        ? FindInventoryItemByReference(
                            GetEquipmentQuickSlotItemId(
                                quickSlot.Slot),
                            itemGuid,
                            null)
                        : displayItems.Find(
                            GetEquipmentQuickSlotItemId(
                                quickSlot.Slot),
                            itemGuid);
                ApplyEquipmentItemIcon(
                    quickSlot.Icon,
                    item,
                    !string.IsNullOrEmpty(itemGuid));
                int quantity;
                quickSlot.Quantity.text =
                    item != null
                    && TryReadInt(item, "Quantity", out quantity)
                    && quantity > 1
                        ? quantity.ToString(
                            CultureInfo.InvariantCulture)
                        : string.Empty;
            }
        }

        private void EditEquipmentQuickSlot(int slot)
        {
            if (slot < 1 || slot > EquipmentQuickSlotCount
                || _equipmentQuickSlotBackingView == null
                || _activeLoadoutsView == null)
            {
                return;
            }

            _selectedEquipmentQuickSlot = slot;
            string itemGuid =
                GetEquipmentQuickSlotItemGuid(slot);
            object item =
                FindInventoryItemByReference(
                    GetEquipmentQuickSlotItemId(slot),
                    itemGuid,
                    null);
            try
            {
                _syncingEquipmentQuickSlotBacking = true;
                MethodInfo backingMethod = AccessTools.Method(
                    _equipmentQuickSlotBackingView.GetType(),
                    item == null ? "Unequip" : "Equip");
                if (backingMethod != null)
                {
                    backingMethod.Invoke(
                        _equipmentQuickSlotBackingView,
                        item == null
                            ? null
                            : new object[] { item });
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not prepare virtual quick slot "
                    + slot.ToString(CultureInfo.InvariantCulture)
                    + " for editing: "
                    + exception.GetBaseException().Message);
                return;
            }
            finally
            {
                _syncingEquipmentQuickSlotBacking = false;
            }

            try
            {
                object loadoutsModel =
                    GetPropertyValue(_activeLoadoutsView, "Target");
                object hovered = GetPropertyValue(
                    loadoutsModel,
                    "CurrentlyHoveredSlot");
                MethodInfo startHover = hovered == null
                    ? null
                    : AccessTools.Method(
                        hovered.GetType(),
                        "OnStartHover");
                MethodInfo equip = loadoutsModel == null
                    ? null
                    : AccessTools.Method(
                        loadoutsModel.GetType(),
                        "Equip");
                if (startHover == null || equip == null)
                {
                    throw new MissingMethodException(
                        "LoadoutsUI quick-slot chooser hooks were unavailable.");
                }

                startHover.Invoke(
                    hovered,
                    new[] { _equipmentQuickSlotBackingView });
                equip.Invoke(loadoutsModel, null);
                _nextEquipmentPanelRefreshTime = 0.0f;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not open the item chooser for virtual quick slot "
                    + slot.ToString(CultureInfo.InvariantCulture)
                    + ": "
                    + exception.GetBaseException().Message);
            }
        }

        internal void CaptureEquipmentQuickSlotItem(
            object equipmentSlotView,
            object item)
        {
            if (_syncingEquipmentQuickSlotBacking
                || _equipmentQuickPanel == null
                || !ReferenceEquals(
                    equipmentSlotView,
                    _equipmentQuickSlotBackingView))
            {
                return;
            }

            SetEquipmentQuickSlotItem(
                _selectedEquipmentQuickSlot,
                item);
        }

        private void SetEquipmentQuickSlotItem(int slot, object item)
        {
            if (slot < 1 || slot > EquipmentQuickSlotCount)
            {
                return;
            }

            _virtualQuickSlotItemGuids[slot - 1] =
                GetItemTemplateGuid(item);
            _virtualQuickSlotItemIds[slot - 1] =
                GetItemModelId(item);
            SaveStandaloneEquipmentState(writeToArchive: false);
            _nextEquipmentPanelRefreshTime = 0.0f;
        }

        private string GetEquipmentQuickSlotItemGuid(int slot)
        {
            return slot < 1 || slot > EquipmentQuickSlotCount
                ? null
                : _virtualQuickSlotItemGuids[slot - 1];
        }

        private string GetEquipmentQuickSlotItemId(int slot)
        {
            return slot < 1 || slot > EquipmentQuickSlotCount
                ? null
                : _virtualQuickSlotItemIds[slot - 1];
        }

        private void UpdateEquipmentQuickSlotHotkeys()
        {
            if (_activeEquipmentView != null
                || !ShouldControlEquipmentQuickSlots())
            {
                return;
            }

            for (int i = 0; i < _equipmentQuickSlotHotkeys.Length; i++)
            {
                ConfigEntry<KeyCode> entry =
                    _equipmentQuickSlotHotkeys[i];
                if (entry == null
                    || entry.Value == KeyCode.None
                    || !Input.GetKeyDown(entry.Value))
                {
                    continue;
                }

                try
                {
                    object item = FindInventoryItemByReference(
                        _virtualQuickSlotItemIds[i],
                        _virtualQuickSlotItemGuids[i],
                        null);
                    if (item != null)
                    {
                        _itemUseMethod.Invoke(item, null);
                        TriggerQuickSlotUsed(GetCurrentHero());
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(
                        "Could not use virtual quick slot "
                        + (i + 1).ToString(CultureInfo.InvariantCulture)
                        + ": "
                        + exception.GetBaseException().Message);
                }
                return;
            }
        }

        private bool ShouldUseOneMenuEquip()
        {
            return IsEnabled()
                && !_externalOneMenuEquipDetected
                && _enableOneMenuEquip != null
                && _enableOneMenuEquip.Value;
        }

        private bool ShouldApplyOneMenuEquipTo(
            EquipmentChooseUI chooseUi)
        {
            if (!ShouldUseOneMenuEquip()
                || chooseUi == null
                || chooseUi.HasBeenDiscarded)
            {
                return false;
            }

            return chooseUi.EquipmentSlotType
                    != EquipmentSlotType.OffHand
                || (_oneMenuEquipApplyToOffHandPicker != null
                    && _oneMenuEquipApplyToOffHandPicker.Value);
        }

        private bool ShouldInterceptOneMenuChooseClick()
        {
            return ShouldUseOneMenuEquip()
                && ShouldApplyOneMenuEquipTo(
                    _oneMenuLastChooseUi)
                && _oneMenuEquipInterceptWeaponClicks != null
                && _oneMenuEquipInterceptWeaponClicks.Value
                && IsUsableOneMenuItem(
                    _oneMenuLastHoveredItem,
                    requireWeapon: true);
        }

        private bool ShouldInterceptOneMenuBagClick()
        {
            return ShouldUseOneMenuEquip()
                && _oneMenuEquipInterceptWeaponClicks != null
                && _oneMenuEquipInterceptWeaponClicks.Value
                && IsUsableOneMenuItem(
                    _oneMenuLastBagHoveredItem,
                    requireWeapon: true);
        }

        private void UpdateOneMenuEquipHotkeys()
        {
            if (!ShouldUseOneMenuEquip())
            {
                DestroyOneMenuInvisibleOverlay();
                return;
            }

            if (_oneMenuEquipOffHandShortcut != null
                && IsShortcutDown(
                    _oneMenuEquipOffHandShortcut.Value))
            {
                TryEquipOneMenuHoveredToHand(
                    EquipmentSlotType.OffHand);
                TryEquipOneMenuBagHoveredToHand(
                    EquipmentSlotType.OffHand);
            }

            if (_oneMenuEquipMainHandShortcut != null
                && IsShortcutDown(
                    _oneMenuEquipMainHandShortcut.Value))
            {
                TryEquipOneMenuHoveredToHand(
                    EquipmentSlotType.MainHand);
                TryEquipOneMenuBagHoveredToHand(
                    EquipmentSlotType.MainHand);
            }
        }

        private bool IsOneMenuEquipShortcutDownThisFrame()
        {
            return (_oneMenuEquipMainHandShortcut != null
                    && IsShortcutDown(
                        _oneMenuEquipMainHandShortcut.Value))
                || (_oneMenuEquipOffHandShortcut != null
                    && IsShortcutDown(
                        _oneMenuEquipOffHandShortcut.Value));
        }

        private static bool IsShortcutDown(
            KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None
                || !Input.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }

            foreach (KeyCode modifier
                in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsUsableOneMenuItem(
            Item item,
            bool requireWeapon)
        {
            return item != null
                && !item.HasBeenDiscarded
                && item.IsEquippable
                && item.EquipmentType != null
                && (!requireWeapon || item.IsWeapon);
        }

        private void TryEquipOneMenuHoveredToHand(
            EquipmentSlotType hand)
        {
            EquipmentChooseUI chooseUi =
                World.Any<EquipmentChooseUI>();
            Item item = _oneMenuLastHoveredItem;
            if (chooseUi == null
                || chooseUi.HasBeenDiscarded
                || !ReferenceEquals(
                    chooseUi,
                    _oneMenuLastChooseUi)
                || !ShouldApplyOneMenuEquipTo(chooseUi)
                || !IsUsableOneMenuItem(
                    item,
                    requireWeapon: false))
            {
                return;
            }

            if (ShouldToggleOneMenuEquippedItem()
                && (_oneMenuLastUnequipFrame == Time.frameCount
                    || item.IsEquipped))
            {
                if (_oneMenuLastUnequipFrame
                    != Time.frameCount)
                {
                    PerformOneMenuUnequip(
                        chooseUi,
                        item);
                }
                return;
            }

            HeroLoadout loadout =
                GetOneMenuEditedLoadout(chooseUi);
            if (loadout == null)
            {
                return;
            }

            try
            {
                if (hand == EquipmentSlotType.MainHand
                    && chooseUi.EquipmentSlotType
                        != EquipmentSlotType.OffHand)
                {
                    _oneMenuExecutingMainHandEquip = true;
                    MethodInfo chooseMethod = AccessTools.Method(
                        typeof(EquipmentChooseUI),
                        "Choose",
                        new[] { typeof(Item) });
                    MethodInfo afterChooseMethod = AccessTools.Method(
                        typeof(EquipmentChooseUI),
                        "AfterChoose",
                        Type.EmptyTypes);
                    if (chooseMethod == null
                        || afterChooseMethod == null)
                    {
                        throw new MissingMethodException(
                            typeof(EquipmentChooseUI).FullName,
                            "Choose/AfterChoose");
                    }

                    chooseMethod.Invoke(
                        chooseUi,
                        new object[] { item });
                    afterChooseMethod.Invoke(
                        chooseUi,
                        null);
                }
                else
                {
                    loadout.EquipItem(hand, item);
                    RefreshOneMenuChooseUi(
                        chooseUi,
                        item);
                }

                ShowOneMenuNotification(
                    "Equipped "
                    + item.DisplayName
                    + " to "
                    + GetOneMenuHandName(hand)
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "One-menu equip failed for "
                    + GetOneMenuHandName(hand)
                    + ": "
                    + exception);
                ShowOneMenuNotification(
                    "Glorious UI: equip failed; see log.");
            }
            finally
            {
                _oneMenuExecutingMainHandEquip = false;
            }
        }

        private void TryEquipOneMenuBagHoveredToHand(
            EquipmentSlotType hand)
        {
            BagUI bagUi = World.Any<BagUI>();
            Item item = _oneMenuLastBagHoveredItem;
            if (bagUi == null
                || bagUi.HasBeenDiscarded
                || !ReferenceEquals(
                    bagUi,
                    _oneMenuLastBagUi)
                || !IsUsableOneMenuItem(
                    item,
                    requireWeapon: true)
                || (ShouldToggleOneMenuEquippedItem()
                    && (_oneMenuLastBagUnequipFrame
                            == Time.frameCount
                        || item.IsEquipped)))
            {
                return;
            }

            Hero hero = Hero.Current;
            HeroLoadout loadout = hero == null
                ? null
                : hero.HeroItems.CurrentLoadout
                    as HeroLoadout;
            if (loadout == null)
            {
                return;
            }

            try
            {
                loadout.EquipItem(hand, item);
                RefreshOneMenuBagUi(bagUi, item);
                ShowOneMenuNotification(
                    "Equipped "
                    + item.DisplayName
                    + " to "
                    + GetOneMenuHandName(hand)
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "One-menu Bag equip failed for "
                    + GetOneMenuHandName(hand)
                    + ": "
                    + exception);
                ShowOneMenuNotification(
                    "Glorious UI: Bag equip failed; see log.");
            }
        }

        private bool ShouldToggleOneMenuEquippedItem()
        {
            return _oneMenuEquipToggleEquippedItem != null
                && _oneMenuEquipToggleEquippedItem.Value;
        }

        private void PerformOneMenuUnequip(
            EquipmentChooseUI chooseUi,
            Item item)
        {
            if (chooseUi == null
                || chooseUi.HasBeenDiscarded
                || item == null
                || item.HasBeenDiscarded)
            {
                return;
            }

            HeroLoadout loadout =
                GetOneMenuEditedLoadout(chooseUi);
            if (loadout == null)
            {
                return;
            }

            try
            {
                loadout.Unequip(item);
                RefreshOneMenuChooseUi(
                    chooseUi,
                    item);
                ShowOneMenuNotification(
                    "Unequipped "
                    + item.DisplayName
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "One-menu unequip failed: "
                    + exception);
                ShowOneMenuNotification(
                    "Glorious UI: unequip failed; see log.");
            }
        }

        private HeroLoadout GetOneMenuEditedLoadout(
            EquipmentChooseUI chooseUi)
        {
            try
            {
                VCLoadoutSlot loadoutSlot =
                    _oneMenuTargetSlotField == null
                        ? null
                        : _oneMenuTargetSlotField.GetValue(
                            chooseUi) as VCLoadoutSlot;
                if (loadoutSlot != null)
                {
                    return loadoutSlot.Loadout;
                }
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "One-menu equip is falling back to the active loadout: "
                    + exception.GetBaseException().Message);
            }

            Hero hero = Hero.Current;
            return hero == null
                ? null
                : hero.HeroItems.CurrentLoadout
                    as HeroLoadout;
        }

        private void RefreshOneMenuChooseUi(
            EquipmentChooseUI chooseUi,
            Item item)
        {
            if (_oneMenuHoveredItemsChangedMethod != null)
            {
                _oneMenuHoveredItemsChangedMethod.Invoke(
                    chooseUi,
                    new object[] { item });
            }

            ItemsUI itemsUi =
                chooseUi.TryGetElement<ItemsUI>();
            if (itemsUi != null)
            {
                itemsUi.SoftRefresh();
            }

            chooseUi.TriggerChange();
            TriggerOneMenuLoadoutViewsChanged();
        }

        private void RefreshOneMenuBagUi(
            BagUI bagUi,
            Item item)
        {
            if (_oneMenuBagRefreshPromptsMethod != null)
            {
                _oneMenuBagRefreshPromptsMethod.Invoke(
                    bagUi,
                    new object[] { item });
            }

            ItemsUI itemsUi =
                bagUi.TryGetElement<ItemsUI>();
            if (itemsUi != null)
            {
                itemsUi.SoftRefresh();
            }

            TriggerOneMenuLoadoutViewsChanged();
        }

        private static void TriggerOneMenuLoadoutViewsChanged()
        {
            var enumerator =
                World.All<LoadoutsUI>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                LoadoutsUI loadoutsUi = enumerator.Current;
                if (loadoutsUi != null
                    && !loadoutsUi.HasBeenDiscarded)
                {
                    loadoutsUi.TriggerChange();
                }
            }
        }

        private void TrackOneMenuHoveredItem(
            EquipmentChooseUI chooseUi,
            Item item)
        {
            if (!ShouldApplyOneMenuEquipTo(chooseUi))
            {
                return;
            }

            if (ReferenceEquals(
                    _oneMenuLastChooseUi,
                    chooseUi)
                && ReferenceEquals(
                    _oneMenuLastHoveredItem,
                    item)
                && _oneMenuLastHoveredItemWasEquipped
                && item != null
                && !item.IsEquipped)
            {
                _oneMenuLastUnequipFrame =
                    Time.frameCount;
            }

            _oneMenuLastChooseUi = chooseUi;
            _oneMenuLastHoveredItem = item;
            _oneMenuLastHoveredItemWasEquipped =
                item != null && item.IsEquipped;
            UpdateOneMenuInvisibleOverlay(
                chooseUi,
                item);
        }

        private void TrackOneMenuBagHoveredItem(
            BagUI bagUi,
            Item item)
        {
            if (!ShouldUseOneMenuEquip())
            {
                return;
            }

            if (ReferenceEquals(
                    _oneMenuLastBagUi,
                    bagUi)
                && ReferenceEquals(
                    _oneMenuLastBagHoveredItem,
                    item)
                && _oneMenuLastBagHoveredItemWasEquipped
                && item != null
                && !item.IsEquipped)
            {
                _oneMenuLastBagUnequipFrame =
                    Time.frameCount;
            }

            _oneMenuLastBagUi = bagUi;
            _oneMenuLastBagHoveredItem = item;
            _oneMenuLastBagHoveredItemWasEquipped =
                item != null && item.IsEquipped;
        }

        private void ReleaseOneMenuChooseUi(
            EquipmentChooseUI chooseUi)
        {
            if (ReferenceEquals(
                _oneMenuLastChooseUi,
                chooseUi))
            {
                _oneMenuLastChooseUi = null;
                _oneMenuLastHoveredItem = null;
                _oneMenuLastHoveredItemWasEquipped =
                    false;
            }

            _oneMenuLastOverlayItem = null;
            DestroyOneMenuInvisibleOverlay();
        }

        private void UpdateOneMenuInvisibleOverlay(
            EquipmentChooseUI chooseUi,
            Item item)
        {
            if (ReferenceEquals(
                _oneMenuLastOverlayItem,
                item))
            {
                return;
            }

            _oneMenuLastOverlayItem = item;
            DestroyOneMenuInvisibleOverlay();
            if (!ShouldInterceptOneMenuChooseClick()
                || chooseUi == null
                || chooseUi.HasBeenDiscarded
                || item == null)
            {
                return;
            }

            VItemEqChooseElement[] elements =
                UnityEngine.Object.FindObjectsByType<
                    VItemEqChooseElement>(
                    FindObjectsSortMode.None);
            VItemEqChooseElement matchingElement = null;
            for (int i = 0; i < elements.Length; i++)
            {
                VItemEqChooseElement element = elements[i];
                Type elementType = element.GetType();
                FieldInfo[] fields = elementType.GetFields(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);
                for (int fieldIndex = 0;
                    fieldIndex < fields.Length;
                    fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (field.FieldType == typeof(Item)
                        && ReferenceEquals(
                            field.GetValue(element),
                            item))
                    {
                        matchingElement = element;
                        break;
                    }
                }

                if (matchingElement != null)
                {
                    break;
                }

                PropertyInfo[] properties =
                    elementType.GetProperties(
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic);
                for (int propertyIndex = 0;
                    propertyIndex < properties.Length;
                    propertyIndex++)
                {
                    PropertyInfo property =
                        properties[propertyIndex];
                    if (property.PropertyType != typeof(Item)
                        || property.GetIndexParameters().Length
                            != 0)
                    {
                        continue;
                    }

                    try
                    {
                        if (ReferenceEquals(
                            property.GetValue(
                                element,
                                null),
                            item))
                        {
                            matchingElement = element;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }

                if (matchingElement != null)
                {
                    break;
                }
            }

            if (matchingElement == null)
            {
                return;
            }

            _oneMenuInvisibleOverlay = new GameObject(
                "GloriousUI_OneMenuEquipClickInterceptor",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform overlayRect =
                _oneMenuInvisibleOverlay.transform
                    as RectTransform;
            overlayRect.SetParent(
                matchingElement.transform,
                false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlayImage =
                _oneMenuInvisibleOverlay.GetComponent<Image>();
            overlayImage.color =
                new Color(0.0f, 0.0f, 0.0f, 0.0f);
            overlayImage.raycastTarget = true;
        }

        private void DestroyOneMenuInvisibleOverlay()
        {
            if (_oneMenuInvisibleOverlay == null)
            {
                return;
            }

            _oneMenuInvisibleOverlay.SetActive(false);
            Destroy(_oneMenuInvisibleOverlay);
            _oneMenuInvisibleOverlay = null;
        }

        private void ShowOneMenuNotification(
            string text)
        {
            if (_oneMenuEquipShowNotifications == null
                || !_oneMenuEquipShowNotifications.Value)
            {
                return;
            }

            _oneMenuNotificationText = text;
            _oneMenuNotificationHideTime =
                Time.unscaledTime + 2.5f;
        }

        private static string GetOneMenuHandName(
            EquipmentSlotType hand)
        {
            return hand == EquipmentSlotType.OffHand
                ? "off hand"
                : "main hand";
        }

        private void UpdateStandaloneEquipmentBackend()
        {
            if (!ShouldControlEquipmentWeaponLoadouts())
            {
                _pendingApplyLoadedWeaponSlot = false;
                _lastTrackedWeaponSnapshot = null;
                _hasTrackedWeaponSnapshot = false;
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null || hero.HeroItems == null)
            {
                return;
            }

            if (_pendingApplyLoadedWeaponSlot
                && Time.unscaledTime >= _pendingEquipmentApplyTime
                && _currentVirtualWeaponSlot >= 1
                && _currentVirtualWeaponSlot
                    <= EquipmentWeaponLoadoutCount)
            {
                _pendingApplyLoadedWeaponSlot = false;
                try
                {
                    ActivateStandaloneWeaponLoadout(
                        _currentVirtualWeaponSlot,
                        captureCurrent: false);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(
                        "Could not restore the saved Glorious weapon loadout: "
                        + exception.GetBaseException().Message);
                }
            }

            if (_currentVirtualWeaponSlot < 1
                || Time.unscaledTime
                    < _nextEquipmentBackendTrackTime)
            {
                return;
            }

            _nextEquipmentBackendTrackTime =
                Time.unscaledTime
                + EquipmentPanelRefreshIntervalSeconds;
            CaptureCurrentStandaloneWeaponLoadout();
        }

        private void ActivateStandaloneWeaponLoadout(
            int slot,
            bool captureCurrent)
        {
            if (!ShouldControlEquipmentWeaponLoadouts())
            {
                return;
            }

            Hero hero = Hero.Current;
            HeroItems heroItems =
                hero == null ? null : hero.HeroItems;
            if (heroItems == null)
            {
                return;
            }

            if (captureCurrent
                && _currentVirtualWeaponSlot >= 1
                && _currentVirtualWeaponSlot
                    <= EquipmentWeaponLoadoutCount)
            {
                CaptureCurrentStandaloneWeaponLoadout();
            }

            VirtualWeaponLoadoutData data;
            if (!_virtualWeaponLoadouts.TryGetValue(slot, out data)
                || data == null)
            {
                data = CreateFistsWeaponLoadout();
                _virtualWeaponLoadouts[slot] = data;
            }

            bool reselected =
                _currentVirtualWeaponSlot == slot;
            _currentVirtualWeaponSlot = slot;
            if (heroItems.CurrentLoadoutIndex != 0)
            {
                heroItems.ActivateLoadout(0, true);
            }
            HeroLoadout loadout = heroItems.LoadoutAt(0);
            if (loadout == null)
            {
                return;
            }

            if (reselected)
            {
                ModelExtensions.Trigger<Hero, bool>(
                    hero,
                    hero.IsWeaponEquipped
                        ? Hero.Events.HideWeapons
                        : Hero.Events.ShowWeapons,
                    false);
            }

            HashSet<Item> assigned = new HashSet<Item>();
            EquipWeaponFingerprint(
                loadout,
                EquipmentSlotType.MainHand,
                data.MainHandGuid,
                heroItems,
                assigned);
            Item main = loadout[EquipmentSlotType.MainHand];
            if (main == null || !IsTwoHandedWeapon(main))
            {
                EquipWeaponFingerprint(
                    loadout,
                    EquipmentSlotType.OffHand,
                    data.OffHandGuid,
                    heroItems,
                    assigned);
            }
            EquipWeaponFingerprint(
                loadout,
                EquipmentSlotType.Quiver,
                data.QuiverGuid,
                heroItems,
                assigned);
            PrimeStandaloneWeaponTracking(loadout);
            _nextEquipmentBackendTrackTime =
                Time.unscaledTime
                + EquipmentPanelRefreshIntervalSeconds;
            SaveStandaloneEquipmentState(
                writeToArchive: false);
            _nextEquipmentPanelRefreshTime = 0.0f;
        }

        private void CaptureCurrentStandaloneWeaponLoadout()
        {
            if (!ShouldControlEquipmentWeaponLoadouts())
            {
                return;
            }

            Hero hero = Hero.Current;
            HeroItems heroItems =
                hero == null ? null : hero.HeroItems;
            if (heroItems == null
                || heroItems.CurrentLoadoutIndex != 0
                || _currentVirtualWeaponSlot < 1
                || _currentVirtualWeaponSlot
                    > EquipmentWeaponLoadoutCount)
            {
                return;
            }

            HeroLoadout loadout = heroItems.LoadoutAt(0);
            if (loadout == null)
            {
                return;
            }

            VirtualWeaponLoadoutData physical =
                BuildStandaloneWeaponLoadout(loadout);
            if (_hasTrackedWeaponSnapshot
                && _lastTrackedWeaponSnapshot != null
                && _lastTrackedWeaponSnapshot.Equals(physical))
            {
                return;
            }

            bool shouldPersistChange =
                _hasTrackedWeaponSnapshot;
            _lastTrackedWeaponSnapshot = physical;
            _hasTrackedWeaponSnapshot = true;
            if (!shouldPersistChange)
            {
                return;
            }

            _virtualWeaponLoadouts[
                _currentVirtualWeaponSlot] = physical;
            SaveStandaloneEquipmentState(
                writeToArchive: false);
            _nextEquipmentPanelRefreshTime = 0.0f;
        }

        internal void CaptureEquipmentWeaponSlotChange(
            object loadoutSlotView)
        {
            if (!ShouldControlEquipmentWeaponLoadouts()
                || _currentVirtualWeaponSlot < 1
                || _currentVirtualWeaponSlot
                    > EquipmentWeaponLoadoutCount)
            {
                return;
            }

            try
            {
                object activeTarget =
                    GetPropertyValue(
                        _activeLoadoutsView,
                        "Target");
                object slotTarget =
                    GetPropertyValue(
                        loadoutSlotView,
                        "Target");
                if (activeTarget == null
                    || !ReferenceEquals(
                        activeTarget,
                        slotTarget))
                {
                    return;
                }

                _nextEquipmentBackendTrackTime = 0.0f;
                CaptureCurrentStandaloneWeaponLoadout();
                _nextEquipmentPanelRefreshTime = 0.0f;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not capture the selected weapon for Glorious's active virtual loadout; periodic tracking remains active. "
                    + exception.GetBaseException().Message);
            }
        }

        private void PrimeStandaloneWeaponTracking(
            HeroLoadout loadout)
        {
            _lastTrackedWeaponSnapshot =
                BuildStandaloneWeaponLoadout(loadout);
            _hasTrackedWeaponSnapshot =
                _lastTrackedWeaponSnapshot != null;
        }

        private VirtualWeaponLoadoutData
            BuildStandaloneWeaponLoadout(HeroLoadout loadout)
        {
            return new VirtualWeaponLoadoutData
            {
                MainHandGuid = GetItemTemplateGuid(
                    loadout[EquipmentSlotType.MainHand]),
                OffHandGuid = GetItemTemplateGuid(
                    loadout[EquipmentSlotType.OffHand]),
                QuiverGuid = GetItemTemplateGuid(
                    loadout[EquipmentSlotType.Quiver])
            };
        }

        private void EquipWeaponFingerprint(
            HeroLoadout loadout,
            EquipmentSlotType slot,
            string templateGuid,
            HeroItems heroItems,
            HashSet<Item> assigned)
        {
            if (string.IsNullOrEmpty(templateGuid))
            {
                loadout.EquipItem(slot, null);
                return;
            }
            if (string.Equals(
                    templateGuid,
                    FistsMainHandGuid,
                    StringComparison.Ordinal)
                && slot == EquipmentSlotType.MainHand)
            {
                loadout.EquipItem(
                    slot,
                    heroItems.GetMainHandFist());
                return;
            }
            if (string.Equals(
                    templateGuid,
                    FistsOffHandGuid,
                    StringComparison.Ordinal)
                && slot == EquipmentSlotType.OffHand)
            {
                loadout.EquipItem(
                    slot,
                    heroItems.GetOffHandFist());
                return;
            }

            Item best = null;
            int bestLevel = -1;
            float bestWeight = float.MaxValue;
            foreach (Item item in heroItems.Items)
            {
                if (item == null
                    || ((Model)item).HasBeenDiscarded
                    || assigned.Contains(item)
                    || item.Template == null
                    || !string.Equals(
                        item.Template.GUID,
                        templateGuid,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int level = item.Level == null
                    ? 0
                    : item.Level.ModifiedInt;
                float weight = item.Weight;
                if (best == null
                    || level > bestLevel
                    || (level == bestLevel
                        && weight < bestWeight))
                {
                    best = item;
                    bestLevel = level;
                    bestWeight = weight;
                }
            }

            if (best != null)
            {
                assigned.Add(best);
            }
            loadout.EquipItem(slot, best);
        }

        private static VirtualWeaponLoadoutData
            CreateFistsWeaponLoadout()
        {
            return new VirtualWeaponLoadoutData
            {
                MainHandGuid = FistsMainHandGuid,
                OffHandGuid = FistsOffHandGuid
            };
        }

        private static bool IsTwoHandedWeapon(Item item)
        {
            if (item == null)
            {
                return false;
            }

            EquipmentType equipmentType = item.EquipmentType;
            return equipmentType == EquipmentType.TwoHanded
                || equipmentType == EquipmentType.Bow
                || equipmentType
                    == EquipmentType.MagicTwoHanded;
        }

        private EquipmentDisplayItemLookup
            BuildEquipmentDisplayItemLookup()
        {
            EquipmentDisplayItemLookup lookup =
                new EquipmentDisplayItemLookup();
            Hero hero = Hero.Current;
            HeroItems heroItems =
                hero == null ? null : hero.HeroItems;
            if (heroItems == null)
            {
                return lookup;
            }

            foreach (Item item in heroItems.Items)
            {
                if (item == null
                    || ((Model)item).HasBeenDiscarded)
                {
                    continue;
                }

                lookup.Add(
                    item,
                    GetItemModelId(item),
                    GetItemTemplateGuid(item),
                    item.Level == null
                        ? 0
                        : item.Level.ModifiedInt,
                    item.Weight);
            }

            return lookup;
        }

        private object FindInventoryItemByReference(
            string itemId,
            string templateGuid,
            List<object> excluded)
        {
            if (string.IsNullOrEmpty(itemId)
                && string.IsNullOrEmpty(templateGuid))
            {
                return null;
            }

            object hero = GetCurrentHero();
            object heroItems = GetPropertyValue(hero, "HeroItems");
            IEnumerable items =
                GetPropertyValue(heroItems, "Items") as IEnumerable;
            if (items == null)
            {
                items = GetPropertyValue(
                    heroItems,
                    "Inventory") as IEnumerable;
            }
            if (items == null)
            {
                return null;
            }

            object best = null;
            int bestLevel = int.MinValue;
            float bestWeight = float.MaxValue;
            foreach (object item in items)
            {
                if (item == null
                    || TryReadBool(item, "HasBeenDiscarded")
                    || (excluded != null
                        && excluded.Contains(item)))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(itemId)
                    && string.Equals(
                        GetItemModelId(item),
                        itemId,
                        StringComparison.Ordinal))
                {
                    return item;
                }
                if (string.IsNullOrEmpty(templateGuid)
                    || !string.Equals(
                        GetItemTemplateGuid(item),
                        templateGuid,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int level;
                if (!TryReadInt(
                    GetPropertyValue(item, "Level"),
                    "ModifiedInt",
                    out level))
                {
                    level = 0;
                }
                float weight;
                if (!TryReadFloat(item, "Weight", out weight))
                {
                    weight = 0.0f;
                }
                if (best == null
                    || level > bestLevel
                    || (level == bestLevel
                        && weight < bestWeight))
                {
                    best = item;
                    bestLevel = level;
                    bestWeight = weight;
                }
            }

            return best;
        }

        private static string GetItemModelId(object item)
        {
            object id = GetPropertyValue(item, "ID");
            return id == null ? null : id.ToString();
        }

        private static string GetItemTemplateGuid(object item)
        {
            object template = GetPropertyValue(item, "Template");
            object guid = GetPropertyValue(template, "GUID");
            return guid == null ? null : guid.ToString();
        }

        internal void LoadStandaloneEquipmentState(
            object cloudService,
            string slotId)
        {
            _activeEquipmentSaveSlot = slotId;
            _virtualWeaponLoadouts.Clear();
            Array.Clear(
                _virtualQuickSlotItemGuids,
                0,
                _virtualQuickSlotItemGuids.Length);
            Array.Clear(
                _virtualQuickSlotItemIds,
                0,
                _virtualQuickSlotItemIds.Length);
            _currentVirtualWeaponSlot = 0;
            _selectedEquipmentQuickSlot = 1;
            _lastTrackedWeaponSnapshot = null;
            _hasTrackedWeaponSnapshot = false;

            byte[] data = null;
            string localPath =
                GetEquipmentStateLocalPath(slotId);
            try
            {
                data = ReadBestEquipmentStateFromArchive(
                    cloudService);
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
                if (data == null && tryLoad != null)
                {
                    object[] args =
                    {
                        EquipmentSessionFileName,
                        null
                    };
                    object result =
                        tryLoad.Invoke(cloudService, args);
                    if (result is bool && (bool)result)
                    {
                        data = args[1] as byte[];
                    }
                }
                if (File.Exists(localPath))
                {
                    byte[] localData =
                        File.ReadAllBytes(localPath);
                    if (ScoreEquipmentState(localData)
                        >= ScoreEquipmentState(data))
                    {
                        data = localData;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not read Glorious Equipment data for "
                    + slotId
                    + ": "
                    + exception.GetBaseException().Message);
            }

            if (data != null && data.Length > 0)
            {
                ParseStandaloneEquipmentState(
                    Encoding.UTF8.GetString(data));
            }
            _pendingApplyLoadedWeaponSlot =
                _currentVirtualWeaponSlot >= 1;
            _pendingEquipmentApplyTime =
                Time.unscaledTime + 0.75f;
            _nextEquipmentPanelRefreshTime = 0.0f;
        }

        private static byte[] ReadBestEquipmentStateFromArchive(
            object cloudService)
        {
            if (cloudService == null)
            {
                return null;
            }

            FieldInfo archiveField = AccessTools.Field(
                cloudService.GetType(),
                "_activeSaveArchive");
            ZipArchive archive = archiveField == null
                ? null
                : archiveField.GetValue(cloudService)
                    as ZipArchive;
            if (archive == null)
            {
                return null;
            }

            byte[] best = null;
            int bestScore = int.MinValue;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!string.Equals(
                    entry.FullName,
                    EquipmentSessionFileName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[] candidate;
                using (Stream stream = entry.Open())
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    candidate = memory.ToArray();
                }
                int score = ScoreEquipmentState(candidate);
                if (score >= bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int ScoreEquipmentState(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return int.MinValue;
            }

            int score = 0;
            string[] lines = Encoding.UTF8.GetString(data).Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('=');
                if (separator <= 0
                    || separator == lines[i].Length - 1)
                {
                    continue;
                }

                string key = lines[i].Substring(0, separator);
                string value =
                    lines[i].Substring(separator + 1).Trim();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                if (key.StartsWith(
                    "W",
                    StringComparison.Ordinal)
                    && (key.EndsWith(
                            ".Main",
                            StringComparison.Ordinal)
                        || key.EndsWith(
                            ".Off",
                            StringComparison.Ordinal)
                        || key.EndsWith(
                            ".Quiver",
                            StringComparison.Ordinal)))
                {
                    score += 10;
                }
                else if (key.StartsWith(
                    "Q",
                    StringComparison.Ordinal))
                {
                    score++;
                }
                else if (string.Equals(
                    key,
                    "CurrentWeaponSlot",
                    StringComparison.Ordinal)
                    && value != "0")
                {
                    score += 100;
                }
            }
            return score;
        }

        internal void SaveStandaloneEquipmentState(
            bool writeToArchive,
            object cloudService = null,
            string slotId = null)
        {
            if (!string.IsNullOrEmpty(slotId))
            {
                _activeEquipmentSaveSlot = slotId;
            }
            if (string.IsNullOrEmpty(
                _activeEquipmentSaveSlot))
            {
                return;
            }

            string serialized =
                SerializeStandaloneEquipmentState();
            byte[] data = Encoding.UTF8.GetBytes(serialized);
            try
            {
                string localPath =
                    GetEquipmentStateLocalPath(
                        _activeEquipmentSaveSlot);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(localPath));
                File.WriteAllBytes(localPath, data);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not write the local Glorious Equipment backup: "
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
                        new object[]
                        {
                            EquipmentSessionFileName,
                            data
                        });
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not write Glorious Equipment data into the save archive: "
                    + exception.GetBaseException().Message);
            }
        }

        private string SerializeStandaloneEquipmentState()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Format=3");
            builder.Append("CurrentWeaponSlot=")
                .Append(_currentVirtualWeaponSlot)
                .AppendLine();
            builder.Append("SelectedQuickSlot=")
                .Append(_selectedEquipmentQuickSlot)
                .AppendLine();
            for (int i = 1; i <= EquipmentWeaponLoadoutCount; i++)
            {
                VirtualWeaponLoadoutData data;
                _virtualWeaponLoadouts.TryGetValue(i, out data);
                builder.Append("W").Append(i)
                    .Append(".Main=")
                    .AppendLine(data == null
                        ? string.Empty
                        : data.MainHandGuid ?? string.Empty);
                builder.Append("W").Append(i)
                    .Append(".Off=")
                    .AppendLine(data == null
                        ? string.Empty
                        : data.OffHandGuid ?? string.Empty);
                builder.Append("W").Append(i)
                    .Append(".Quiver=")
                    .AppendLine(data == null
                        ? string.Empty
                        : data.QuiverGuid ?? string.Empty);
            }
            for (int i = 0; i < EquipmentQuickSlotCount; i++)
            {
                builder.Append("Q").Append(i + 1)
                    .Append(".Id=")
                    .AppendLine(
                        _virtualQuickSlotItemIds[i]
                        ?? string.Empty);
                builder.Append("Q").Append(i + 1)
                    .Append("=")
                    .AppendLine(
                        _virtualQuickSlotItemGuids[i]
                        ?? string.Empty);
            }
            return builder.ToString();
        }

        private void ParseStandaloneEquipmentState(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] lines = text.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }
                string key =
                    lines[i].Substring(0, separator).Trim();
                string value =
                    lines[i].Substring(separator + 1).Trim();
                int parsed;
                if (string.Equals(
                    key,
                    "CurrentWeaponSlot",
                    StringComparison.Ordinal)
                    && int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsed))
                {
                    _currentVirtualWeaponSlot =
                        Mathf.Clamp(
                            parsed,
                            0,
                            EquipmentWeaponLoadoutCount);
                    continue;
                }
                if (string.Equals(
                    key,
                    "SelectedQuickSlot",
                    StringComparison.Ordinal)
                    && int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsed))
                {
                    _selectedEquipmentQuickSlot =
                        Mathf.Clamp(
                            parsed,
                            1,
                            EquipmentQuickSlotCount);
                    continue;
                }
                if (key.Length >= 5
                    && key[0] == 'Q'
                    && key.EndsWith(
                        ".Id",
                        StringComparison.Ordinal)
                    && int.TryParse(
                        key.Substring(1, key.Length - 4),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsed)
                    && parsed >= 1
                    && parsed <= EquipmentQuickSlotCount)
                {
                    _virtualQuickSlotItemIds[
                        parsed - 1] =
                            string.IsNullOrEmpty(value)
                                ? null
                                : value;
                    continue;
                }
                if (key.Length >= 2
                    && key[0] == 'Q'
                    && int.TryParse(
                        key.Substring(1),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsed)
                    && parsed >= 1
                    && parsed <= EquipmentQuickSlotCount)
                {
                    _virtualQuickSlotItemGuids[
                        parsed - 1] =
                            string.IsNullOrEmpty(value)
                                ? null
                                : value;
                    continue;
                }
                if (key.Length < 4 || key[0] != 'W')
                {
                    continue;
                }
                int dot = key.IndexOf('.');
                if (dot <= 1
                    || !int.TryParse(
                        key.Substring(1, dot - 1),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsed)
                    || parsed < 1
                    || parsed > EquipmentWeaponLoadoutCount)
                {
                    continue;
                }

                VirtualWeaponLoadoutData loadout;
                if (!_virtualWeaponLoadouts.TryGetValue(
                    parsed,
                    out loadout))
                {
                    loadout =
                        new VirtualWeaponLoadoutData();
                    _virtualWeaponLoadouts[parsed] =
                        loadout;
                }
                string field = key.Substring(dot + 1);
                string guid = string.IsNullOrEmpty(value)
                    ? null
                    : value;
                if (string.Equals(
                    field,
                    "Main",
                    StringComparison.Ordinal))
                {
                    loadout.MainHandGuid = guid;
                }
                else if (string.Equals(
                    field,
                    "Off",
                    StringComparison.Ordinal))
                {
                    loadout.OffHandGuid = guid;
                }
                else if (string.Equals(
                    field,
                    "Quiver",
                    StringComparison.Ordinal))
                {
                    loadout.QuiverGuid = guid;
                }
            }
        }

        private static string GetEquipmentStateLocalPath(
            string slotId)
        {
            string safeName = slotId ?? "Unknown";
            foreach (char invalid
                in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalid, '_');
            }
            return Path.Combine(
                Paths.ConfigPath,
                "GloriousUI",
                "Equipment",
                safeName + ".slots");
        }

        private void ReleaseEquipmentQuickPanel()
        {
            EquipmentQuickPanel panel = _equipmentQuickPanel;
            _equipmentQuickPanel = null;
            _equipmentQuickSlotBackingView = null;
            if (panel != null)
            {
                panel.Release();
            }
        }

        private bool ShouldControlEquipmentWeaponLoadouts()
        {
            return IsEnabled()
                && _controlEquipmentWeaponLoadouts != null
                && _controlEquipmentWeaponLoadouts.Value;
        }

        private bool ShouldControlEquipmentQuickSlots()
        {
            return IsEnabled()
                && _arButtonType != null
                && _controlEquipmentQuickSlots != null
                && _controlEquipmentQuickSlots.Value;
        }

        private void ApplyQuickUseWheelHudVisibility()
        {
            bool shouldHide = IsEnabled()
                && _quickUseWheelOpen
                && _hideGameplayHudInQuickUseWheel != null
                && _hideGameplayHudInQuickUseWheel.Value;
            if (!shouldHide)
            {
                RestoreQuickUseHudObjects();
                RefreshCompassVisibility(false);
                if (!_quickUseWheelOpen)
                {
                    RestoreCharacterPointsAfterQuickUse();
                }
                else
                {
                    ApplyCharacterPointsVisibility(
                        _activeCharacterPointsView);
                }
                return;
            }

            SetQuickUseHudObjectVisible(
                GetHeroHudRectTransform(_activeHeroHudView),
                false);
            SetQuickUseHudObjectVisible(
                GetQuickSlotHudRectTransform(_activeSelectedQuickSlotView),
                false);
            SetQuickUseHudObjectVisible(
                GetArrowCounterRectTransform(_activeHeroHudView),
                false);
            SetQuickUseHudObjectVisible(
                GetWyrdSkillIndicatorRectTransform(_activeHeroHudView),
                false);
            SetQuickUseHudObjectVisible(
                GetStatusHudRectTransform(_activeHeroStatusHud),
                false);
            RefreshCompassVisibility(false);
            HideCharacterPointsForQuickUse();
        }

        private void SetQuickUseHudObjectVisible(
            Component component,
            bool visible)
        {
            if (component == null)
            {
                return;
            }

            GameObject gameObject = component.gameObject;
            int id = gameObject.GetInstanceID();
            if (!_quickUseHudObjectSnapshots.ContainsKey(id))
            {
                _quickUseHudObjectSnapshots[id] =
                    new QuickUseHudObjectSnapshot(gameObject, gameObject.activeSelf);
            }

            gameObject.SetActive(visible);
        }

        private void RestoreQuickUseHudObjects()
        {
            foreach (QuickUseHudObjectSnapshot snapshot
                in _quickUseHudObjectSnapshots.Values)
            {
                if (snapshot.GameObject != null)
                {
                    snapshot.GameObject.SetActive(snapshot.ActiveSelf);
                }
            }

            _quickUseHudObjectSnapshots.Clear();
        }

        internal void ApplyArrowHudTransform(object heroHudView)
        {
            if (heroHudView == null)
            {
                return;
            }

            try
            {
                RectTransform arrowCounterRect = GetArrowCounterRectTransform(heroHudView);
                if (arrowCounterRect == null)
                {
                    return;
                }

                if (!ShouldOwnArrowSlot())
                {
                    RestoreHudTransform(arrowCounterRect);
                    RestoreArrowCounterTextScale(
                        arrowCounterRect);
                    return;
                }

                if (!HasArrowHudAccessors())
                {
                    RestoreHudTransform(arrowCounterRect);
                    RestoreArrowCounterTextScale(
                        arrowCounterRect);
                    return;
                }

                Component selectedQuickSlot = _heroHudSelectedQuickSlotField.GetValue(heroHudView) as Component;
                if (selectedQuickSlot == null)
                {
                    return;
                }

                Image foodIcon = _itemIconField.GetValue(selectedQuickSlot) as Image;
                Image[] nextIcons = _nextItemIconsField.GetValue(selectedQuickSlot) as Image[];
                Image healthIcon = nextIcons != null && nextIcons.Length > 1 ? nextIcons[1] : null;
                if (foodIcon == null || healthIcon == null)
                {
                    return;
                }

                RectTransform selectedQuickSlotRect = selectedQuickSlot.transform as RectTransform;
                RectTransform foodRect = GetSlotFootprintRectTransform(foodIcon, selectedQuickSlotRect);
                RectTransform healthRect = GetSlotFootprintRectTransform(healthIcon, selectedQuickSlotRect);
                RectTransform arrowImageRect = GetArrowImageRectTransform(heroHudView, arrowCounterRect);
                if (foodRect == null || healthRect == null || arrowImageRect == null)
                {
                    return;
                }

                HudTransformSnapshot snapshot = GetOrCreateHudTransformSnapshot(arrowCounterRect);
                MatchArrowSlotSize(arrowCounterRect, arrowImageRect, healthRect, snapshot);
                MirrorArrowSlotPosition(arrowCounterRect, arrowImageRect, foodRect, healthRect);
                ApplyArrowSlotOffset(arrowCounterRect);
                ApplyArrowCounterTextScale(
                    arrowCounterRect);
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not apply arrow quick-slot HUD layout: " + exception.GetBaseException().Message);
            }
        }

        internal void ApplyWyrdSkillIndicatorTransform(object heroHudView)
        {
            if (heroHudView == null)
            {
                return;
            }

            try
            {
                RectTransform rectTransform = GetWyrdSkillIndicatorRectTransform(heroHudView);
                if (rectTransform == null)
                {
                    return;
                }

                if (!ShouldOwnWyrdSkillIndicator())
                {
                    RestoreHudTransform(rectTransform);
                    return;
                }

                HudTransformSnapshot snapshot = GetOrCreateHudTransformSnapshot(rectTransform);
                float scale = GetWyrdSkillIndicatorScale();
                rectTransform.anchoredPosition = snapshot.AnchoredPosition
                    + new Vector2(
                        WyrdSkillIndicatorBaselineOffsetX + GetWyrdSkillIndicatorOffsetX(),
                        WyrdSkillIndicatorBaselineOffsetY + GetWyrdSkillIndicatorOffsetY());
                rectTransform.localScale = new Vector3(
                    snapshot.LocalScale.x * WyrdSkillIndicatorBaselineScale * scale,
                    snapshot.LocalScale.y * WyrdSkillIndicatorBaselineScale * scale,
                    snapshot.LocalScale.z);
            }
            catch (Exception exception)
            {
                LogAccessorFailure(
                    "Could not apply Wyrd Skill Indicator position or scale: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void ApplyWyrdSkillPromptVisibility(object heroHudView)
        {
            GameObject prompt = GetWyrdSkillPromptGameObject(heroHudView);
            if (prompt == null)
            {
                return;
            }

            int promptId = prompt.GetInstanceID();
            WyrdSkillPromptSnapshot snapshot;
            if (!_wyrdSkillPromptSnapshots.TryGetValue(promptId, out snapshot))
            {
                snapshot = new WyrdSkillPromptSnapshot(prompt, prompt.activeSelf);
                _wyrdSkillPromptSnapshots[promptId] = snapshot;
            }

            bool shouldHide = IsEnabled()
                && _hideWyrdSkillPrompt != null
                && _hideWyrdSkillPrompt.Value;
            prompt.SetActive(shouldHide ? false : snapshot.ActiveSelf);
        }

        private void MaintainWyrdSkillPromptVisibility()
        {
            if (!IsEnabled()
                || _hideWyrdSkillPrompt == null
                || !_hideWyrdSkillPrompt.Value)
            {
                return;
            }

            GameObject prompt =
                GetWyrdSkillPromptGameObject(
                    _activeHeroHudView);
            if (prompt != null && prompt.activeSelf)
            {
                ApplyWyrdSkillPromptVisibility(
                    _activeHeroHudView);
            }
        }

        internal void RedirectHeldApplyChangesPrompt(
            ref KeyBindings key)
        {
            if (IsEnabled()
                && ReferenceEquals(
                    key,
                    KeyBindings.UI.Settings.ApplyChanges))
            {
                key = KeyBindings.Gameplay.Interact;
            }
        }

        internal void RedirectRestoreDefaultsPrompt(
            ref KeyBindings key)
        {
            if (!IsEnabled()
                || !ReferenceEquals(
                    key,
                    KeyBindings.UI.Settings.RestoreDefaults))
            {
                return;
            }

            string interactKey = RewiredHelper.KeyIdentifierFor(
                KeyBindings.Gameplay.Interact.EnumName);
            if (string.Equals(
                interactKey,
                "F",
                StringComparison.OrdinalIgnoreCase))
            {
                key = KeyBindings.UI.Settings.ApplyChanges;
            }
        }

        internal void ReleaseViewArrowHudTransform(object heroHudView)
        {
            RectTransform arrowRect = GetArrowCounterRectTransform(heroHudView);
            if (arrowRect == null)
            {
                return;
            }

            RestoreHudTransform(arrowRect);
            RestoreArrowCounterTextScale(arrowRect);
            _hudTransformSnapshots.Remove(arrowRect.GetInstanceID());
        }

        internal void ReleaseViewHeroHudTransform(object heroHudView)
        {
            RectTransform rectTransform = GetHeroHudRectTransform(heroHudView);
            if (rectTransform == null)
            {
                return;
            }

            RestoreHudTransform(rectTransform);
            _hudTransformSnapshots.Remove(rectTransform.GetInstanceID());
        }

        internal void ReleaseViewWyrdSkillIndicatorTransform(object heroHudView)
        {
            Component heroHud = heroHudView as Component;
            int heroHudId = heroHud == null ? 0 : heroHud.GetInstanceID();
            RectTransform rectTransform = null;
            if (heroHudId != 0)
            {
                _wyrdSkillIndicatorRects.TryGetValue(
                    heroHudId,
                    out rectTransform);
                _wyrdSkillIndicatorRects.Remove(heroHudId);
            }

            if (rectTransform == null)
            {
                return;
            }

            RestoreHudTransform(rectTransform);
            _hudTransformSnapshots.Remove(rectTransform.GetInstanceID());
        }

        internal void ReleaseViewWyrdSkillPrompt(object heroHudView)
        {
            GameObject prompt = GetWyrdSkillPromptGameObject(heroHudView);
            if (prompt == null)
            {
                return;
            }

            int promptId = prompt.GetInstanceID();
            WyrdSkillPromptSnapshot snapshot;
            if (_wyrdSkillPromptSnapshots.TryGetValue(promptId, out snapshot))
            {
                prompt.SetActive(snapshot.ActiveSelf);
                _wyrdSkillPromptSnapshots.Remove(promptId);
            }
        }

        internal void ReleaseViewSmartIcons(object selectedQuickSlotView)
        {
            if (selectedQuickSlotView == null || _nextItemIconsField == null)
            {
                return;
            }

            Image[] nextIcons = _nextItemIconsField.GetValue(selectedQuickSlotView) as Image[];
            if (nextIcons == null)
            {
                return;
            }

            for (int i = 0; i < nextIcons.Length; i++)
            {
                ReleaseSmartIcon(nextIcons[i]);
            }
        }

        internal void ReleaseViewQuickSlotHudTransform(object selectedQuickSlotView)
        {
            RectTransform rectTransform = GetQuickSlotHudRectTransform(selectedQuickSlotView);
            if (rectTransform == null)
            {
                return;
            }

            RestoreHudTransform(rectTransform);
            _hudTransformSnapshots.Remove(rectTransform.GetInstanceID());
        }

        private static RectTransform GetQuickSlotHudRectTransform(object selectedQuickSlotView)
        {
            Component component = selectedQuickSlotView as Component;
            if (component != null)
            {
                return component.transform as RectTransform;
            }

            GameObject gameObject = selectedQuickSlotView as GameObject;
            return gameObject == null ? null : gameObject.transform as RectTransform;
        }

        private RectTransform GetHeroHudRectTransform(object heroHudView)
        {
            if (heroHudView == null || _heroHudHeroBarsTransformField == null)
            {
                return null;
            }

            return _heroHudHeroBarsTransformField.GetValue(heroHudView) as RectTransform;
        }

        private RectTransform GetArrowCounterRectTransform(object heroHudView)
        {
            if (heroHudView == null || _heroHudArrowsCounterField == null)
            {
                return null;
            }

            GameObject arrowsCounter = _heroHudArrowsCounterField.GetValue(heroHudView) as GameObject;
            return arrowsCounter == null ? null : arrowsCounter.transform as RectTransform;
        }

        private RectTransform GetArrowImageRectTransform(object heroHudView, RectTransform fallbackRect)
        {
            if (heroHudView == null || _heroHudArrowsImageField == null)
            {
                return fallbackRect;
            }

            Image arrowsImage = _heroHudArrowsImageField.GetValue(heroHudView) as Image;
            return arrowsImage == null ? fallbackRect : arrowsImage.rectTransform;
        }

        private RectTransform GetWyrdSkillIndicatorRectTransform(object heroHudView)
        {
            Component heroHud = heroHudView as Component;
            if (heroHud == null || _wyrdSkillBarType == null)
            {
                return null;
            }

            int heroHudId = heroHud.GetInstanceID();
            RectTransform cached;
            if (_wyrdSkillIndicatorRects.TryGetValue(heroHudId, out cached))
            {
                if (cached != null)
                {
                    return cached;
                }

                _wyrdSkillIndicatorRects.Remove(heroHudId);
            }

            Component wyrdSkillBar = heroHud.GetComponentInChildren(_wyrdSkillBarType, true);
            RectTransform resolved = wyrdSkillBar == null
                ? null
                : wyrdSkillBar.transform as RectTransform;
            if (resolved != null)
            {
                _wyrdSkillIndicatorRects[heroHudId] = resolved;
            }

            return resolved;
        }

        private GameObject GetWyrdSkillPromptGameObject(object heroHudView)
        {
            RectTransform indicatorRect = GetWyrdSkillIndicatorRectTransform(heroHudView);
            if (indicatorRect == null)
            {
                return null;
            }

            Transform prompt = indicatorRect.Find("UsePrompt");
            return prompt == null ? null : prompt.gameObject;
        }

        internal void ApplyHudDrawOrder(object heroHudView)
        {
            Component heroHud = heroHudView as Component;
            if (heroHud == null || _heroHudHeroBarsTransformField == null)
            {
                return;
            }

            Transform heroBars =
                _heroHudHeroBarsTransformField.GetValue(heroHudView) as Transform;
            Component selectedQuickSlot = _heroHudSelectedQuickSlotField == null
                ? null
                : _heroHudSelectedQuickSlotField.GetValue(heroHudView) as Component;
            Transform quickSlot = selectedQuickSlot == null
                ? null
                : selectedQuickSlot.transform;
            RectTransform arrowCounter =
                GetArrowCounterRectTransform(heroHudView);
            RectTransform wyrdSkillIndicator =
                GetWyrdSkillIndicatorRectTransform(heroHudView);

            bool renderBehind = IsEnabled()
                && _renderUtilityHudBehindHeroBars != null
                && _renderUtilityHudBehindHeroBars.Value;
            if (!renderBehind)
            {
                RestoreHudSiblingOrder(quickSlot);
                RestoreHudSiblingOrder(arrowCounter);
                RestoreHudSiblingOrder(wyrdSkillIndicator);
                return;
            }

            MoveSiblingBeforeHeroBars(quickSlot, heroBars, "Quick Slot HUD");
            MoveSiblingBeforeHeroBars(arrowCounter, heroBars, "Arrow HUD");

            if (wyrdSkillIndicator != null
                && heroBars != null
                && wyrdSkillIndicator.parent == heroBars)
            {
                GetOrCreateHudSiblingSnapshot(wyrdSkillIndicator);
                wyrdSkillIndicator.SetAsFirstSibling();
            }
            else
            {
                LogLayeringWarning(
                    "Wyrd Power HUD is not a direct HeroBars child; its hierarchy draw order was not changed.");
            }

            LogLayeringDiagnostic(
                "Applied draw order. QuickSlot="
                + DescribeHudLayer(quickSlot)
                + "; HeroBars="
                + DescribeHudLayer(heroBars)
                + "; Arrow="
                + DescribeHudLayer(arrowCounter)
                + "; WyrdPower="
                + DescribeHudLayer(wyrdSkillIndicator)
                + ".");
        }

        private void ReleaseViewHudDrawOrder(object heroHudView)
        {
            if (heroHudView == null)
            {
                return;
            }

            Component selectedQuickSlot = _heroHudSelectedQuickSlotField == null
                ? null
                : _heroHudSelectedQuickSlotField.GetValue(heroHudView) as Component;
            RestoreHudSiblingOrder(
                selectedQuickSlot == null ? null : selectedQuickSlot.transform);
            RestoreHudSiblingOrder(GetArrowCounterRectTransform(heroHudView));
            RestoreHudSiblingOrder(GetWyrdSkillIndicatorRectTransform(heroHudView));
        }

        private void MoveSiblingBeforeHeroBars(
            Transform transform,
            Transform heroBars,
            string label)
        {
            if (transform == null
                || heroBars == null
                || transform.parent != heroBars.parent)
            {
                LogLayeringWarning(
                    label
                    + " and HeroBars do not share a parent; their hierarchy draw order was not changed.");
                return;
            }

            GetOrCreateHudSiblingSnapshot(transform);
            int heroBarsIndex = heroBars.GetSiblingIndex();
            if (transform.GetSiblingIndex() > heroBarsIndex)
            {
                transform.SetSiblingIndex(heroBarsIndex);
            }
        }

        private HudSiblingSnapshot GetOrCreateHudSiblingSnapshot(Transform transform)
        {
            int id = transform.GetInstanceID();
            HudSiblingSnapshot snapshot;
            if (!_hudSiblingSnapshots.TryGetValue(id, out snapshot))
            {
                snapshot = new HudSiblingSnapshot(
                    transform,
                    transform.parent,
                    transform.GetSiblingIndex());
                _hudSiblingSnapshots[id] = snapshot;
            }

            return snapshot;
        }

        private void RestoreHudSiblingOrder(Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            int id = transform.GetInstanceID();
            HudSiblingSnapshot snapshot;
            if (!_hudSiblingSnapshots.TryGetValue(id, out snapshot))
            {
                return;
            }

            if (snapshot.Transform != null
                && snapshot.Parent != null
                && snapshot.Transform.parent == snapshot.Parent)
            {
                snapshot.Transform.SetSiblingIndex(
                    Mathf.Clamp(
                        snapshot.SiblingIndex,
                        0,
                        snapshot.Parent.childCount - 1));
            }

            _hudSiblingSnapshots.Remove(id);
        }

        private void RestoreAllHudSiblingOrders()
        {
            foreach (HudSiblingSnapshot snapshot in _hudSiblingSnapshots.Values)
            {
                if (snapshot.Transform != null
                    && snapshot.Parent != null
                    && snapshot.Transform.parent == snapshot.Parent)
                {
                    snapshot.Transform.SetSiblingIndex(
                        Mathf.Clamp(
                            snapshot.SiblingIndex,
                            0,
                            snapshot.Parent.childCount - 1));
                }
            }

            _hudSiblingSnapshots.Clear();
        }

        private void LogLayeringDiagnostic(string message)
        {
            if (_layeringDiagnostics != null && _layeringDiagnostics.Value)
            {
                Logger.LogInfo("[HUD Layering] " + message);
            }
        }

        private void LogLayeringWarning(string message)
        {
            Logger.LogWarning("[HUD Layering] " + message);
        }

        private static string DescribeHudLayer(Transform transform)
        {
            if (transform == null)
            {
                return "null";
            }

            Canvas nearestCanvas = transform.GetComponentInParent<Canvas>();
            Canvas[] nestedCanvases = transform.GetComponentsInChildren<Canvas>(true);
            int overrideSortingCanvases = 0;
            for (int i = 0; i < nestedCanvases.Length; i++)
            {
                if (nestedCanvases[i] != null && nestedCanvases[i].overrideSorting)
                {
                    overrideSortingCanvases++;
                }
            }

            return GetHudTransformPath(transform)
                + " sibling="
                + transform.GetSiblingIndex().ToString(CultureInfo.InvariantCulture)
                + "/"
                + (transform.parent == null
                    ? "0"
                    : transform.parent.childCount.ToString(CultureInfo.InvariantCulture))
                + " nearestCanvasOrder="
                + (nearestCanvas == null
                    ? "none"
                    : nearestCanvas.sortingOrder.ToString(CultureInfo.InvariantCulture))
                + " nestedOverrideSortingCanvases="
                + overrideSortingCanvases.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetHudTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

#if false
        private bool TryPlaceQuickSlotBelowHeroBars(
            object selectedQuickSlotView,
            RectTransform quickSlotRoot,
            out RectTransform layoutAnchor)
        {
            layoutAnchor = null;

            Rect heroBarsScreenRect;
            RectTransform heroBarsRect;
            if (!TryGetHeroBarsScreenRect(
                _activeHeroHudView,
                out heroBarsScreenRect,
                out heroBarsRect))
            {
                LogHudLayoutFallback(
                    "Quick Slot HUD",
                    "no plausible hero-bar rectangle was found");
                return false;
            }

            Image foodIcon = _itemIconField == null
                ? null
                : _itemIconField.GetValue(selectedQuickSlotView) as Image;
            layoutAnchor = foodIcon == null ? null : foodIcon.rectTransform;

            Rect foodScreenRect;
            if (!TryGetScreenRect(layoutAnchor, out foodScreenRect)
                || !IsPlausibleVisualRect(foodScreenRect))
            {
                LogHudLayoutFallback(
                    "Quick Slot HUD",
                    "the food-slot icon did not produce a plausible screen rectangle");
                return false;
            }

            Vector2 targetCenter = new Vector2(
                heroBarsScreenRect.center.x,
                heroBarsScreenRect.yMin
                    - GetHudStackSpacingPixels()
                    - foodScreenRect.height * 0.5f);
            if (!TryMoveRectAnchorToScreenPoint(
                quickSlotRoot,
                foodScreenRect.center,
                targetCenter))
            {
                LogHudLayoutFallback(
                    "Quick Slot HUD",
                    "the food-slot screen point could not be converted into its parent rectangle");
                return false;
            }

            LogHudLayoutDiagnostic(
                "Quick Slot HUD sourceBars="
                + FormatScreenRect(heroBarsScreenRect)
                + "; foodBefore="
                + FormatScreenRect(foodScreenRect)
                + "; targetCenter="
                + FormatVector2(targetCenter)
                + "; heroBarsTransform="
                + DescribeRectTransform(heroBarsRect)
                + ".");
            return true;
        }

        private bool TryPlaceWyrdBelowQuickSlots(
            object heroHudView,
            RectTransform wyrdRoot,
            out RectTransform layoutAnchor)
        {
            layoutAnchor = null;

            Rect heroBarsScreenRect;
            RectTransform heroBarsRect;
            if (!TryGetHeroBarsScreenRect(
                heroHudView,
                out heroBarsScreenRect,
                out heroBarsRect))
            {
                LogHudLayoutFallback(
                    "Wyrd Power HUD",
                    "no plausible hero-bar rectangle was found");
                return false;
            }

            Rect precedingScreenRect;
            if (!TryGetQuickSlotClusterScreenRect(heroHudView, out precedingScreenRect))
            {
                precedingScreenRect = heroBarsScreenRect;
            }

            Rect wyrdScreenRect;
            layoutAnchor = GetWyrdVisualAnchor(wyrdRoot, out wyrdScreenRect);
            if (layoutAnchor == null)
            {
                LogHudLayoutFallback(
                    "Wyrd Power HUD",
                    "no plausible Wyrd indicator rectangle was found");
                return false;
            }

            Vector2 targetCenter = new Vector2(
                heroBarsScreenRect.center.x,
                precedingScreenRect.yMin
                    - GetHudStackSpacingPixels()
                    - wyrdScreenRect.height * 0.5f);
            if (!TryMoveRectAnchorToScreenPoint(
                wyrdRoot,
                wyrdScreenRect.center,
                targetCenter))
            {
                LogHudLayoutFallback(
                    "Wyrd Power HUD",
                    "the Wyrd screen point could not be converted into its parent rectangle");
                return false;
            }

            LogHudLayoutDiagnostic(
                "Wyrd Power HUD sourceQuickSlots="
                + FormatScreenRect(precedingScreenRect)
                + "; wyrdBefore="
                + FormatScreenRect(wyrdScreenRect)
                + "; targetCenter="
                + FormatVector2(targetCenter)
                + "; heroBarsTransform="
                + DescribeRectTransform(heroBarsRect)
                + ".");
            return true;
        }

        private bool TryGetHeroBarsScreenRect(
            object heroHudView,
            out Rect screenRect,
            out RectTransform heroBarsRect)
        {
            screenRect = default(Rect);
            heroBarsRect = null;

            Component heroHud = heroHudView as Component;
            if (heroHud == null
                || _heroHudBarType == null
                || _heroHudHeroBarsTransformField == null)
            {
                return false;
            }

            Transform heroBarsTransform =
                _heroHudHeroBarsTransformField.GetValue(heroHudView) as Transform;
            heroBarsRect = heroBarsTransform as RectTransform;
            if (TryGetScreenRect(heroBarsRect, out screenRect)
                && IsPlausibleHeroBarsRect(screenRect))
            {
                return true;
            }

            bool hasBounds = false;
            Component[] heroBars = heroHud.GetComponentsInChildren(_heroHudBarType, true);
            for (int i = 0; i < heroBars.Length; i++)
            {
                Component heroBar = heroBars[i];
                if (heroBar == null
                    || heroBarsTransform == null
                    || !heroBar.transform.IsChildOf(heroBarsTransform))
                {
                    continue;
                }

                Rect barScreenRect;
                if (TryGetScreenRect(heroBar.transform as RectTransform, out barScreenRect)
                    && IsPlausibleVisualRect(barScreenRect))
                {
                    EncapsulateScreenRect(ref screenRect, barScreenRect, ref hasBounds);
                }
            }

            return hasBounds && IsPlausibleHeroBarsRect(screenRect);
        }

        private bool TryGetQuickSlotClusterScreenRect(
            object heroHudView,
            out Rect screenRect)
        {
            screenRect = default(Rect);
            bool hasBounds = false;
            if (heroHudView == null || _heroHudSelectedQuickSlotField == null)
            {
                return false;
            }

            Component selectedQuickSlot =
                _heroHudSelectedQuickSlotField.GetValue(heroHudView) as Component;
            if (selectedQuickSlot == null)
            {
                return false;
            }

            Image foodIcon = _itemIconField == null
                ? null
                : _itemIconField.GetValue(selectedQuickSlot) as Image;
            EncapsulateVisualScreenRect(
                foodIcon == null ? null : foodIcon.rectTransform,
                ref screenRect,
                ref hasBounds);

            Image[] nextIcons = _nextItemIconsField == null
                ? null
                : _nextItemIconsField.GetValue(selectedQuickSlot) as Image[];
            if (nextIcons != null)
            {
                for (int i = 0; i < nextIcons.Length; i++)
                {
                    Image nextIcon = nextIcons[i];
                    EncapsulateVisualScreenRect(
                        nextIcon == null ? null : nextIcon.rectTransform,
                        ref screenRect,
                        ref hasBounds);
                }
            }

            if (ShouldOwnArrowSlot())
            {
                RectTransform arrowCounterRect = GetArrowCounterRectTransform(heroHudView);
                RectTransform arrowImageRect =
                    GetArrowImageRectTransform(heroHudView, arrowCounterRect);
                EncapsulateVisualScreenRect(
                    arrowImageRect,
                    ref screenRect,
                    ref hasBounds);
            }

            return hasBounds;
        }

        private RectTransform GetWyrdVisualAnchor(
            RectTransform wyrdRoot,
            out Rect screenRect)
        {
            screenRect = default(Rect);
            if (TryGetScreenRect(wyrdRoot, out screenRect)
                && IsPlausibleVisualRect(screenRect))
            {
                return wyrdRoot;
            }

            Transform prompt = wyrdRoot == null ? null : wyrdRoot.Find("UsePrompt");
            RectTransform bestRect = null;
            float bestArea = 0.0f;
            Graphic[] graphics = wyrdRoot == null
                ? new Graphic[0]
                : wyrdRoot.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null
                    || (prompt != null && graphic.transform.IsChildOf(prompt)))
                {
                    continue;
                }

                Rect candidateScreenRect;
                if (!TryGetScreenRect(graphic.rectTransform, out candidateScreenRect)
                    || !IsPlausibleVisualRect(candidateScreenRect))
                {
                    continue;
                }

                float area = candidateScreenRect.width * candidateScreenRect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestRect = graphic.rectTransform;
                    screenRect = candidateScreenRect;
                }
            }

            return bestRect;
        }

        private void EncapsulateVisualScreenRect(
            RectTransform rectTransform,
            ref Rect screenRect,
            ref bool hasBounds)
        {
            Rect candidate;
            if (TryGetScreenRect(rectTransform, out candidate)
                && IsPlausibleVisualRect(candidate))
            {
                EncapsulateScreenRect(ref screenRect, candidate, ref hasBounds);
            }
        }

        private static void EncapsulateScreenRect(
            ref Rect screenRect,
            Rect candidate,
            ref bool hasBounds)
        {
            if (!hasBounds)
            {
                screenRect = candidate;
                hasBounds = true;
                return;
            }

            screenRect = Rect.MinMaxRect(
                Mathf.Min(screenRect.xMin, candidate.xMin),
                Mathf.Min(screenRect.yMin, candidate.yMin),
                Mathf.Max(screenRect.xMax, candidate.xMax),
                Mathf.Max(screenRect.yMax, candidate.yMax));
        }

        private bool TryGetScreenRect(RectTransform rectTransform, out Rect screenRect)
        {
            screenRect = default(Rect);
            if (rectTransform == null)
            {
                return false;
            }

            Camera camera = GetCanvasCamera(rectTransform);
            rectTransform.GetWorldCorners(_worldCorners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[0]);
            if (!IsFinite(first))
            {
                return false;
            }

            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int i = 1; i < _worldCorners.Length; i++)
            {
                Vector2 point =
                    RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[i]);
                if (!IsFinite(point))
                {
                    return false;
                }

                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return screenRect.width > 0.5f && screenRect.height > 0.5f;
        }

        private bool TryMoveRectAnchorToScreenPoint(
            RectTransform root,
            Vector2 currentAnchorScreenPoint,
            Vector2 targetScreenPoint)
        {
            RectTransform parentRect = root == null ? null : root.parent as RectTransform;
            if (parentRect == null)
            {
                return false;
            }

            Camera camera = GetCanvasCamera(root);
            Vector3 currentWorldPoint;
            Vector3 targetWorldPoint;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    parentRect,
                    currentAnchorScreenPoint,
                    camera,
                    out currentWorldPoint)
                || !RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    parentRect,
                    targetScreenPoint,
                    camera,
                    out targetWorldPoint))
            {
                return false;
            }

            root.position += targetWorldPoint - currentWorldPoint;
            return true;
        }

        private static Camera GetCanvasCamera(RectTransform rectTransform)
        {
            Canvas canvas = rectTransform == null
                ? null
                : rectTransform.GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        private bool IsPlausibleHeroBarsRect(Rect screenRect)
        {
            return screenRect.width >= 1.0f
                && screenRect.height >= 1.0f
                && screenRect.width <= Screen.width * 0.9f
                && screenRect.height <= Screen.height * 0.75f
                && screenRect.Overlaps(GetPaddedScreenRect(0.25f));
        }

        private bool IsPlausibleVisualRect(Rect screenRect)
        {
            return screenRect.width >= 1.0f
                && screenRect.height >= 1.0f
                && screenRect.width <= Screen.width * 0.6f
                && screenRect.height <= Screen.height * 0.6f
                && screenRect.Overlaps(GetPaddedScreenRect(0.25f));
        }

        private bool IsHudAnchorOnScreen(RectTransform rectTransform)
        {
            Rect screenRect;
            return TryGetScreenRect(rectTransform, out screenRect)
                && screenRect.Overlaps(GetPaddedScreenRect(0.1f))
                && screenRect.center.x >= Screen.width * -0.1f
                && screenRect.center.x <= Screen.width * 1.1f
                && screenRect.center.y >= Screen.height * -0.1f
                && screenRect.center.y <= Screen.height * 1.1f;
        }

        private static Rect GetPaddedScreenRect(float paddingFraction)
        {
            float horizontalPadding = Screen.width * paddingFraction;
            float verticalPadding = Screen.height * paddingFraction;
            return new Rect(
                -horizontalPadding,
                -verticalPadding,
                Screen.width + horizontalPadding * 2.0f,
                Screen.height + verticalPadding * 2.0f);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }

        private float GetHudStackSpacingPixels()
        {
            return _hudStackSpacing == null
                ? 12.0f
                : Mathf.Clamp(
                    _hudStackSpacing.Value,
                    MinimumHudStackSpacing,
                    MaximumHudStackSpacing);
        }

        private void LogHudLayoutResult(
            string label,
            RectTransform root,
            RectTransform anchor)
        {
            Rect anchorScreenRect;
            string screenRect = TryGetScreenRect(anchor, out anchorScreenRect)
                ? FormatScreenRect(anchorScreenRect)
                : "unavailable";
            LogHudLayoutDiagnostic(
                label
                + " result root="
                + DescribeRectTransform(root)
                + "; anchor="
                + DescribeRectTransform(anchor)
                + "; anchorScreen="
                + screenRect
                + ".");
        }

        private void LogHudLayoutFallback(string label, string reason)
        {
            Logger.LogWarning(
                "[HUD Layout] "
                + label
                + " automatic placement skipped or rejected because "
                + reason
                + "; using the vanilla transform.");
        }

        private void LogHudLayoutDiagnostic(string message)
        {
            if (_hudLayoutDiagnostics != null && _hudLayoutDiagnostics.Value)
            {
                Logger.LogInfo("[HUD Layout] " + message);
            }
        }

        private static string DescribeRectTransform(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return "null";
            }

            return GetTransformPath(rectTransform)
                + " anchored="
                + FormatVector2(rectTransform.anchoredPosition)
                + " size="
                + FormatVector2(rectTransform.rect.size)
                + " scale="
                + FormatVector2(new Vector2(
                    rectTransform.localScale.x,
                    rectTransform.localScale.y));
        }

        private static string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static string FormatScreenRect(Rect screenRect)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.0},{1:0.0})-({2:0.0},{3:0.0})",
                screenRect.xMin,
                screenRect.yMin,
                screenRect.xMax,
                screenRect.yMax);
        }

        private static string FormatVector2(Vector2 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.0},{1:0.0})",
                value.x,
                value.y);
        }

#endif

        private bool HasArrowHudAccessors()
        {
            return _itemIconField != null
                && _nextItemIconsField != null
                && _heroHudSelectedQuickSlotField != null
                && _heroHudArrowsCounterField != null;
        }

        private float GetQuickSlotHudOffsetX()
        {
            return _quickSlotHudOffsetX == null ? 0.0f : _quickSlotHudOffsetX.Value;
        }

        private float GetQuickSlotHudOffsetY()
        {
            return _quickSlotHudOffsetY == null ? 0.0f : _quickSlotHudOffsetY.Value;
        }

        private float GetQuickSlotHudScale()
        {
            if (_quickSlotHudScale == null)
            {
                return 1.0f;
            }

            return Mathf.Clamp(_quickSlotHudScale.Value, MinimumHudScale, MaximumHudScale);
        }

        private float GetHeroHudScale()
        {
            if (_heroHudScale == null)
            {
                return 1.0f;
            }

            return Mathf.Clamp(_heroHudScale.Value, MinimumHudScale, MaximumHudScale);
        }

        private float GetHeroHudVisibleSeconds()
        {
            if (_heroHudVisibleSeconds == null)
            {
                return 2.0f;
            }

            return Mathf.Clamp(
                _heroHudVisibleSeconds.Value,
                MinimumHeroHudVisibleSeconds,
                MaximumHeroHudVisibleSeconds);
        }

        private float GetQuestNotificationDuration(float gameDuration)
        {
            if (!IsEnabled()
                || _questNotificationDurationSeconds == null
                || _questNotificationDurationSeconds.Value <= 0.0f)
            {
                return gameDuration;
            }

            return Mathf.Clamp(
                _questNotificationDurationSeconds.Value,
                0.5f,
                60.0f);
        }

        private static Vector2 GetAnchorVector(HudAnchor anchor)
        {
            switch (anchor)
            {
                case HudAnchor.TopLeft:
                    return new Vector2(0.0f, 1.0f);
                case HudAnchor.TopCenter:
                    return new Vector2(0.5f, 1.0f);
                case HudAnchor.TopRight:
                    return new Vector2(1.0f, 1.0f);
                case HudAnchor.BottomLeft:
                    return new Vector2(0.0f, 0.0f);
                case HudAnchor.BottomRight:
                    return new Vector2(1.0f, 0.0f);
                case HudAnchor.Center:
                    return new Vector2(0.5f, 0.5f);
                default:
                    return new Vector2(0.5f, 0.0f);
            }
        }

        private float GetArrowSlotOffsetX()
        {
            return _arrowSlotOffsetX == null ? 0.0f : _arrowSlotOffsetX.Value;
        }

        private float GetArrowSlotOffsetY()
        {
            return _arrowSlotOffsetY == null ? 0.0f : _arrowSlotOffsetY.Value;
        }

        private float GetArrowSlotScale()
        {
            if (_arrowSlotScale == null)
            {
                return 1.0f;
            }

            return Mathf.Clamp(_arrowSlotScale.Value, MinimumHudScale, MaximumHudScale);
        }

        private float GetWyrdSkillIndicatorOffsetX()
        {
            return _wyrdSkillIndicatorOffsetX == null ? 0.0f : _wyrdSkillIndicatorOffsetX.Value;
        }

        private float GetWyrdSkillIndicatorOffsetY()
        {
            return _wyrdSkillIndicatorOffsetY == null ? 0.0f : _wyrdSkillIndicatorOffsetY.Value;
        }

        private float GetWyrdSkillIndicatorScale()
        {
            if (_wyrdSkillIndicatorScale == null)
            {
                return 1.0f;
            }

            return Mathf.Clamp(_wyrdSkillIndicatorScale.Value, MinimumHudScale, MaximumHudScale);
        }

        private void RestoreHudTransform(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            HudTransformSnapshot snapshot;
            if (!_hudTransformSnapshots.TryGetValue(rectTransform.GetInstanceID(), out snapshot))
            {
                return;
            }

            rectTransform.anchorMin = snapshot.AnchorMin;
            rectTransform.anchorMax = snapshot.AnchorMax;
            rectTransform.pivot = snapshot.Pivot;
            rectTransform.anchoredPosition = snapshot.AnchoredPosition;
            rectTransform.localScale = snapshot.LocalScale;
        }

        private HudTransformSnapshot GetOrCreateHudTransformSnapshot(RectTransform rectTransform)
        {
            int id = rectTransform.GetInstanceID();
            HudTransformSnapshot snapshot;
            if (!_hudTransformSnapshots.TryGetValue(id, out snapshot))
            {
                snapshot = new HudTransformSnapshot(
                    rectTransform,
                    rectTransform.anchoredPosition,
                    rectTransform.localScale,
                    rectTransform.anchorMin,
                    rectTransform.anchorMax,
                    rectTransform.pivot);
                _hudTransformSnapshots[id] = snapshot;
            }

            return snapshot;
        }

        private void ApplyArrowCounterTextScale(
            RectTransform arrowCounterRect)
        {
            TMP_Text[] counters =
                arrowCounterRect.GetComponentsInChildren<
                    TMP_Text>(true);
            for (int i = 0; i < counters.Length; i++)
            {
                TMP_Text counter = counters[i];
                if (counter == null)
                {
                    continue;
                }

                int id = counter.GetInstanceID();
                ArrowCounterTextSnapshot snapshot;
                if (!_arrowCounterTextSnapshots.TryGetValue(
                        id,
                        out snapshot))
                {
                    snapshot =
                        new ArrowCounterTextSnapshot(counter);
                    _arrowCounterTextSnapshots[id] = snapshot;
                }

                counter.fontSize = snapshot.FontSize
                    * ArrowCounterTextBaselineScale;
                counter.fontSizeMin = snapshot.FontSizeMin
                    * ArrowCounterTextBaselineScale;
                counter.fontSizeMax = snapshot.FontSizeMax
                    * ArrowCounterTextBaselineScale;
            }
        }

        private void RestoreArrowCounterTextScale(
            RectTransform arrowCounterRect)
        {
            TMP_Text[] counters =
                arrowCounterRect.GetComponentsInChildren<
                    TMP_Text>(true);
            for (int i = 0; i < counters.Length; i++)
            {
                TMP_Text counter = counters[i];
                if (counter == null)
                {
                    continue;
                }

                int id = counter.GetInstanceID();
                ArrowCounterTextSnapshot snapshot;
                if (_arrowCounterTextSnapshots.TryGetValue(
                        id,
                        out snapshot))
                {
                    snapshot.Restore();
                    _arrowCounterTextSnapshots.Remove(id);
                }
            }
        }

        private RectTransform GetSlotFootprintRectTransform(Image image, RectTransform rootRect)
        {
            if (image == null)
            {
                return null;
            }

            RectTransform iconRect = image.rectTransform;
            if (iconRect == null)
            {
                return null;
            }

            float iconWidth = GetWorldWidth(iconRect);
            float iconHeight = GetWorldHeight(iconRect);
            Transform parent = iconRect.parent;
            while (parent != null && parent != rootRect)
            {
                RectTransform parentRect = parent as RectTransform;
                if (IsPlausibleSlotFootprint(parentRect, iconRect, iconWidth, iconHeight))
                {
                    return parentRect;
                }

                parent = parent.parent;
            }

            return iconRect;
        }

        private bool IsPlausibleSlotFootprint(
            RectTransform candidate,
            RectTransform iconRect,
            float iconWidth,
            float iconHeight)
        {
            if (candidate == null || iconWidth <= 0.01f || iconHeight <= 0.01f)
            {
                return false;
            }

            float candidateWidth = GetWorldWidth(candidate);
            float candidateHeight = GetWorldHeight(candidate);
            if (candidateWidth <= 0.01f || candidateHeight <= 0.01f)
            {
                return false;
            }

            float aspect = candidateWidth / candidateHeight;
            if (aspect < 0.65f || aspect > 1.55f)
            {
                return false;
            }

            if (candidateWidth < iconWidth * 1.05f || candidateHeight < iconHeight * 1.05f)
            {
                return false;
            }

            if (candidateWidth > iconWidth * 2.4f || candidateHeight > iconHeight * 2.4f)
            {
                return false;
            }

            return Vector3.Distance(GetWorldCenter(candidate), GetWorldCenter(iconRect)) <= candidateHeight * 0.5f;
        }

        private void MatchArrowSlotSize(
            RectTransform arrowCounterRect,
            RectTransform arrowImageRect,
            RectTransform healthRect,
            HudTransformSnapshot arrowSnapshot)
        {
            float healthHeight = GetWorldHeight(healthRect);
            float arrowHeight = GetSnapshotWorldHeight(arrowImageRect, arrowCounterRect, arrowSnapshot);
            if (healthHeight <= 0.01f || arrowHeight <= 0.01f)
            {
                return;
            }

            float scale = healthHeight / arrowHeight;
            arrowCounterRect.localScale = new Vector3(
                arrowSnapshot.LocalScale.x * scale * ArrowSlotBaselineScale * GetArrowSlotScale(),
                arrowSnapshot.LocalScale.y * scale * ArrowSlotBaselineScale * GetArrowSlotScale(),
                arrowSnapshot.LocalScale.z);
        }

        private void MirrorArrowSlotPosition(
            RectTransform arrowCounterRect,
            RectTransform arrowImageRect,
            RectTransform foodRect,
            RectTransform healthRect)
        {
            Vector3 foodCenter = GetWorldCenter(foodRect);
            Vector3 healthCenter = GetWorldCenter(healthRect);
            Vector3 targetCenter = new Vector3(
                foodCenter.x - (healthCenter.x - foodCenter.x),
                healthCenter.y,
                healthCenter.z);
            arrowCounterRect.position += targetCenter - GetWorldCenter(arrowImageRect);
        }

        private void ApplyArrowSlotOffset(RectTransform arrowCounterRect)
        {
            arrowCounterRect.anchoredPosition += new Vector2(
                ArrowSlotBaselineOffsetX + GetArrowSlotOffsetX(),
                ArrowSlotBaselineOffsetY + GetArrowSlotOffsetY());
        }

        private float GetSnapshotWorldHeight(
            RectTransform measuredRect,
            RectTransform scaledRect,
            HudTransformSnapshot snapshot)
        {
            float currentHeight = GetWorldHeight(measuredRect);
            if (currentHeight <= 0.01f)
            {
                return 0f;
            }

            float snapshotScaleY = Mathf.Abs(snapshot.LocalScale.y);
            float currentScaleY = Mathf.Abs(scaledRect.localScale.y);
            if (snapshotScaleY <= 0.01f || currentScaleY <= 0.01f)
            {
                return currentHeight;
            }

            return currentHeight / (currentScaleY / snapshotScaleY);
        }

        private Vector3 GetWorldCenter(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(_worldCorners);
            return (_worldCorners[0] + _worldCorners[2]) * 0.5f;
        }

        private float GetWorldHeight(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(_worldCorners);
            return Vector3.Distance(_worldCorners[0], _worldCorners[1]);
        }

        private float GetWorldWidth(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(_worldCorners);
            return Vector3.Distance(_worldCorners[0], _worldCorners[3]);
        }

        private void ApplySmartIcon(Image[] icons, int index, object item)
        {
            if (icons == null || index < 0 || index >= icons.Length)
            {
                return;
            }

            Image image = icons[index];
            if (image == null)
            {
                return;
            }

            SmartIconOverlay overlay = EnsureSmartIconOverlay(image, index);
            if (overlay == null)
            {
                return;
            }

            HideVanillaIcon(overlay);

            if (item == null || GetItemQuantity(item) <= 0)
            {
                ClearSmartOverlay(overlay);
                return;
            }

            if (ReferenceEquals(overlay.Item, item)
                && overlay.SpriteReference != null
                && overlay.OverlayObject != null
                && overlay.OverlayImage != null)
            {
                CopyIconLayout(image, overlay.OverlayImage);
                overlay.OverlayObject.SetActive(true);
                overlay.OverlayImage.enabled = true;
                return;
            }

            object icon = GetPropertyValue(item, "Icon");
            if (icon == null || !TryReadBool(icon, "IsSet"))
            {
                ClearSmartOverlay(overlay);
                return;
            }

            object spriteReference = InvokeMethod(icon, "Get", null);
            if (spriteReference == null)
            {
                ClearSmartOverlay(overlay);
                return;
            }

            if (overlay.SpriteReference != null && !ReferenceEquals(overlay.SpriteReference, spriteReference))
            {
                ReleaseSpriteReference(overlay.SpriteReference);
            }

            overlay.Item = item;
            overlay.SpriteReference = spriteReference;
            MethodInfo setSprite = FindSetSpriteMethod(spriteReference.GetType());
            if (setSprite == null)
            {
                ClearSmartOverlay(overlay);
                return;
            }

            CopyIconLayout(image, overlay.OverlayImage);
            overlay.OverlayObject.SetActive(true);
            overlay.OverlayImage.enabled = true;
            setSprite.Invoke(spriteReference, new object[] { overlay.OverlayImage, null });
        }

        private void HideSmartSlotVanillaIcons(Image[] icons)
        {
            if (icons == null)
            {
                return;
            }

            int count = Math.Min(2, icons.Length);
            for (int i = 0; i < count; i++)
            {
                SmartIconOverlay overlay = EnsureSmartIconOverlay(icons[i], i);
                HideVanillaIcon(overlay);
            }
        }

        private SmartIconOverlay EnsureSmartIconOverlay(Image sourceImage, int index)
        {
            if (sourceImage == null)
            {
                return null;
            }

            int imageId = sourceImage.GetInstanceID();
            SmartIconOverlay overlay;
            if (_smartIconOverlays.TryGetValue(imageId, out overlay)
                && overlay != null
                && overlay.OverlayImage != null)
            {
                CopyIconLayout(sourceImage, overlay.OverlayImage);
                return overlay;
            }

            Transform parent = sourceImage.transform.parent;
            if (parent == null)
            {
                return null;
            }

            GameObject overlayObject = new GameObject(
                "GloriousUISmartIcon" + index.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.transform.SetParent(parent, false);

            Image overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.raycastTarget = false;
            overlayImage.preserveAspect = sourceImage.preserveAspect;
            overlayImage.type = sourceImage.type;
            overlayImage.material = sourceImage.material;
            overlayImage.color = Color.white;

            CopyIconLayout(sourceImage, overlayImage);
            overlayObject.transform.SetSiblingIndex(Math.Min(parent.childCount - 1, sourceImage.transform.GetSiblingIndex() + 1));
            overlayObject.SetActive(false);

            overlay = new SmartIconOverlay(
                sourceImage,
                overlayObject,
                overlayImage,
                sourceImage.color,
                sourceImage.enabled);
            _smartIconOverlays[imageId] = overlay;
            return overlay;
        }

        private static void CopyIconLayout(Image sourceImage, Image overlayImage)
        {
            if (sourceImage == null || overlayImage == null)
            {
                return;
            }

            RectTransform sourceRect = sourceImage.rectTransform;
            RectTransform overlayRect = overlayImage.rectTransform;
            overlayRect.anchorMin = sourceRect.anchorMin;
            overlayRect.anchorMax = sourceRect.anchorMax;
            overlayRect.pivot = sourceRect.pivot;
            overlayRect.sizeDelta = sourceRect.sizeDelta;
            overlayRect.anchoredPosition = sourceRect.anchoredPosition;
            overlayRect.localRotation = sourceRect.localRotation;
            overlayRect.localScale = sourceRect.localScale;
        }

        private static void HideVanillaIcon(SmartIconOverlay overlay)
        {
            if (overlay == null || overlay.SourceImage == null)
            {
                return;
            }

            Color color = overlay.SourceImage.color;
            if (color.a > 0.0f)
            {
                color.a = 0.0f;
                overlay.SourceImage.color = color;
            }
        }

        private void ClearSmartOverlay(SmartIconOverlay overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (overlay.SpriteReference != null)
            {
                ReleaseSpriteReference(overlay.SpriteReference);
                overlay.SpriteReference = null;
            }

            overlay.Item = null;

            if (overlay.OverlayObject != null)
            {
                overlay.OverlayObject.SetActive(false);
            }
        }

        private static MethodInfo FindSetSpriteMethod(Type spriteReferenceType)
        {
            foreach (MethodInfo method in spriteReferenceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!string.Equals(method.Name, "SetSprite", StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Image))
                {
                    return method;
                }
            }

            return null;
        }

        private void ReleaseSmartIcon(Image image)
        {
            if (image == null)
            {
                return;
            }

            int imageId = image.GetInstanceID();
            SmartIconOverlay overlay;
            if (_smartIconOverlays.TryGetValue(imageId, out overlay))
            {
                ReleaseSmartOverlay(overlay);
                _smartIconOverlays.Remove(imageId);
            }
        }

        private void ReleaseSmartIcons(Image[] images)
        {
            if (images == null)
            {
                return;
            }

            for (int i = 0; i < images.Length; i++)
            {
                ReleaseSmartIcon(images[i]);
            }
        }

        private void ReleaseAllSmartIcons()
        {
            foreach (SmartIconOverlay overlay in _smartIconOverlays.Values)
            {
                ReleaseSmartOverlay(overlay);
            }

            _smartIconOverlays.Clear();
        }

        private void ReleaseSmartOverlay(SmartIconOverlay overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (overlay.SpriteReference != null)
            {
                ReleaseSpriteReference(overlay.SpriteReference);
                overlay.SpriteReference = null;
            }

            if (overlay.SourceImage != null)
            {
                overlay.SourceImage.color = overlay.SourceColor;
                overlay.SourceImage.enabled = overlay.SourceEnabled;
            }

            if (overlay.OverlayObject != null)
            {
                Destroy(overlay.OverlayObject);
            }
        }

        private void ReleaseAllHudTransforms()
        {
            RestoreAllHudSiblingOrders();

            foreach (WyrdSkillPromptSnapshot snapshot in _wyrdSkillPromptSnapshots.Values)
            {
                if (snapshot.Prompt != null)
                {
                    snapshot.Prompt.SetActive(snapshot.ActiveSelf);
                }
            }

            _wyrdSkillPromptSnapshots.Clear();

            foreach (HudTransformSnapshot snapshot in _hudTransformSnapshots.Values)
            {
                if (snapshot.RectTransform != null)
                {
                    snapshot.RectTransform.anchorMin = snapshot.AnchorMin;
                    snapshot.RectTransform.anchorMax = snapshot.AnchorMax;
                    snapshot.RectTransform.pivot = snapshot.Pivot;
                    snapshot.RectTransform.anchoredPosition = snapshot.AnchoredPosition;
                    snapshot.RectTransform.localScale = snapshot.LocalScale;
                }
            }

            foreach (ArrowCounterTextSnapshot snapshot
                in _arrowCounterTextSnapshots.Values)
            {
                snapshot.Restore();
            }

            _hudTransformSnapshots.Clear();
            _arrowCounterTextSnapshots.Clear();
            _wyrdSkillIndicatorRects.Clear();
        }

        private static void ReleaseSpriteReference(object spriteReference)
        {
            if (spriteReference == null)
            {
                return;
            }

            try
            {
                MethodInfo release = AccessTools.Method(spriteReference.GetType(), "Release", Type.EmptyTypes);
                if (release != null)
                {
                    release.Invoke(spriteReference, null);
                }
            }
            catch
            {
            }
        }

        private bool TryUseSmartConsumable(SmartConsumableKind kind)
        {
            object hero = GetCurrentHero();
            if (hero == null || !TryReadBool(hero, "IsAlive"))
            {
                LogDiagnostic("Skipped " + kind + " hotkey because no living hero is available.");
                return false;
            }

            object heroItems = GetPropertyValue(hero, "HeroItems");
            if (heroItems == null)
            {
                LogDiagnostic("Skipped " + kind + " hotkey because HeroItems is unavailable.");
                return false;
            }

            EnsureHeroItemsPatches(heroItems);

            SmartSelectionMode mode = kind == SmartConsumableKind.ManaPotion
                ? _manaPotionSelectionMode.Value
                : _healthPotionSelectionMode.Value;
            object item = FindSmartConsumable(hero, heroItems, kind, mode);
            if (item == null)
            {
                LogDiagnostic("Skipped " + kind + " hotkey because no matching item was found.");
                return false;
            }

            try
            {
                _itemUseMethod.Invoke(item, null);
                TriggerQuickSlotUsed(hero);
                LogDiagnostic("Used smart " + kind + ": " + GetItemName(item) + ".");
                return true;
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not use smart " + kind + ": " + exception.GetBaseException().Message);
                return false;
            }
        }

        private object FindSmartConsumable(
            object hero,
            object heroItems,
            SmartConsumableKind kind,
            SmartSelectionMode mode)
        {
            return FindSmartConsumable(hero, heroItems, kind, mode, false);
        }

        private object FindSmartConsumable(
            object hero,
            object heroItems,
            SmartConsumableKind kind,
            SmartSelectionMode mode,
            bool allowAtFull)
        {
            IEnumerable inventory = GetPropertyValue(heroItems, "Inventory") as IEnumerable;
            if (inventory == null)
            {
                return null;
            }

            float missing = GetMissingResource(hero, kind);
            if (kind != SmartConsumableKind.Food
                && !allowAtFull
                && _preventPotionWasteAtFull != null
                && _preventPotionWasteAtFull.Value
                && missing <= 0.01f)
            {
                return null;
            }

            Candidate best = new Candidate(null, -1f);
            Candidate bestSufficient = new Candidate(null, float.MaxValue);
            Candidate smallest = new Candidate(null, float.MaxValue);
            bool foundSufficient = false;
            bool foundSmallest = false;
            bool foundAmount = false;

            foreach (object item in inventory)
            {
                if (!IsCandidate(item, kind))
                {
                    continue;
                }

                float amount = GetRestoreAmount(item);
                if (amount > 0.01f)
                {
                    foundAmount = true;
                    if (amount < smallest.Amount)
                    {
                        smallest = new Candidate(item, amount);
                        foundSmallest = true;
                    }
                }

                if (mode == SmartSelectionMode.SmallestSufficient
                    && missing > 0.01f
                    && amount >= missing
                    && amount < bestSufficient.Amount)
                {
                    bestSufficient = new Candidate(item, amount);
                    foundSufficient = true;
                }

                if (best.Item == null || amount > best.Amount)
                {
                    best = new Candidate(item, amount);
                }
            }

            if (mode == SmartSelectionMode.SmallestSufficient && foundSufficient)
            {
                return bestSufficient.Item;
            }

            if (mode == SmartSelectionMode.SmallestSufficient && missing <= 0.01f && foundSmallest)
            {
                return smallest.Item;
            }

            if (!foundAmount && best.Item != null)
            {
                LogDiagnostic("Smart amount metadata was unavailable for " + kind + "; using first matching item.");
            }

            return best.Item;
        }

        private bool IsCandidate(object item, SmartConsumableKind kind)
        {
            if (item == null || GetItemQuantity(item) <= 0 || TryReadBool(item, "HasBeenDiscarded"))
            {
                return false;
            }

            if (kind == SmartConsumableKind.Food)
            {
                return TryReadBool(item, "IsPlainFood");
            }

            if (!TryReadBool(item, "IsPotion"))
            {
                return false;
            }

            object template = GetPropertyValue(item, "Template");
            if (kind == SmartConsumableKind.HealthPotion)
            {
                return TryReadBool(template, "ConsumableModifiesHealth")
                    || ContainsAnyItemText(item, HealthTerms);
            }

            return TryReadBool(template, "ConsumableModifiesMana")
                || ContainsAnyItemText(item, ManaTerms);
        }

        private float GetRestoreAmount(object item)
        {
            if (item == null || _getHealValueMethod == null)
            {
                return 0f;
            }

            try
            {
                object value = _getHealValueMethod.Invoke(null, new[] { item });
                float amount = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                if (amount > 0.01f)
                {
                    return amount;
                }

                return GetItemVariable(item, "AddValue");
            }
            catch
            {
                return GetItemVariable(item, "AddValue");
            }
        }

        private float GetItemVariable(object item, string variableName)
        {
            if (item == null || _getVariableMethod == null)
            {
                return 0f;
            }

            try
            {
                object value = _getVariableMethod.Invoke(item, new object[] { variableName, 0, null });
                return value == null ? 0f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0f;
            }
        }

        private float GetMissingResource(object hero, SmartConsumableKind kind)
        {
            if (kind == SmartConsumableKind.ManaPotion)
            {
                return Math.Max(0f, GetResourceMaximum(hero, "Mana", "MaxManaWithReservation", "MaxMana")
                    - GetResourceCurrent(hero, "Mana"));
            }

            return Math.Max(0f, GetResourceMaximum(hero, "Health", "MaxHealthWithReservation", "MaxHealth")
                - GetResourceCurrent(hero, "Health"));
        }

        private float GetResourceCurrent(object hero, string statProperty)
        {
            object stat = GetPropertyValue(hero, statProperty);
            float value;
            if (TryReadFloat(stat, "BaseValue", out value))
            {
                return value;
            }

            if (TryReadFloat(stat, "ModifiedValue", out value))
            {
                return value;
            }

            return 0f;
        }

        private float GetResourceMaximum(object hero, string statProperty, string preferredMaxProperty, string fallbackMaxProperty)
        {
            float value;
            if (TryReadFloat(hero, preferredMaxProperty, out value) && value > 0f)
            {
                return value;
            }

            object stat = GetPropertyValue(hero, statProperty);
            if (TryReadFloat(stat, "UpperLimit", out value) && value > 0f)
            {
                return value;
            }

            object maxStat = GetPropertyValue(hero, fallbackMaxProperty);
            if (TryReadFloat(maxStat, "ModifiedValue", out value) && value > 0f)
            {
                return value;
            }

            return 0f;
        }

        private object GetCurrentHero()
        {
            if (_heroCurrentProperty == null)
            {
                return null;
            }

            return _heroCurrentProperty.GetValue(null, null);
        }

        private object GetEquippedItem(object heroItems, object slot)
        {
            if (heroItems == null || slot == null)
            {
                return null;
            }

            return _equippedItemMethod.Invoke(null, new[] { heroItems, slot });
        }

        private void EquipItem(object heroItems, object item, object slot)
        {
            if (heroItems == null || item == null || slot == null)
            {
                return;
            }

            _equipMethod.Invoke(null, new[] { heroItems, item, slot, null });
        }

        private void TriggerQuickSlotUsed(object hero)
        {
            object quickSlotUsedEvent = GetQuickSlotUsedEvent();
            if (hero == null || _triggerQuickSlotUsedMethod == null || quickSlotUsedEvent == null)
            {
                return;
            }

            _triggerQuickSlotUsedMethod.Invoke(null, new[] { hero, quickSlotUsedEvent, hero });
        }

        private object GetQuickSlotUsedEvent()
        {
            if (_quickSlotUsedEvent == null && _quickSlotUsedEventField != null)
            {
                _quickSlotUsedEvent = _quickSlotUsedEventField.GetValue(null);
            }

            return _quickSlotUsedEvent;
        }

        private int GetItemQuantity(object item)
        {
            int value;
            return TryReadInt(item, "Quantity", out value) ? value : 0;
        }

        private string GetItemName(object item)
        {
            object displayName = GetPropertyValue(item, "DisplayName");
            if (displayName != null)
            {
                return displayName.ToString();
            }

            object debugName = GetPropertyValue(item, "DebugName");
            return debugName == null ? "<unknown>" : debugName.ToString();
        }

        private bool ContainsAnyItemText(object item, string[] terms)
        {
            foreach (string text in EnumerateItemText(item))
            {
                if (ContainsAny(text, terms))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<string> EnumerateItemText(object item)
        {
            object displayName = GetPropertyValue(item, "DisplayName");
            if (displayName != null)
            {
                yield return displayName.ToString();
            }

            object debugName = GetPropertyValue(item, "DebugName");
            if (debugName != null)
            {
                yield return debugName.ToString();
            }

            object template = GetPropertyValue(item, "Template");
            object templateName = GetPropertyValue(template, "ItemName");
            if (templateName != null)
            {
                yield return templateName.ToString();
            }

            IEnumerable tags = GetPropertyValue(item, "Tags") as IEnumerable;
            if (tags != null)
            {
                foreach (object tag in tags)
                {
                    if (tag != null)
                    {
                        yield return tag.ToString();
                    }
                }
            }
        }

        private static bool ContainsAny(string text, string[] terms)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                if (text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null)
            {
                return null;
            }

            PropertyInfo property = AccessTools.Property(obj.GetType(), propertyName);
            if (property == null || property.GetIndexParameters().Length != 0)
            {
                return null;
            }

            return property.GetValue(obj, null);
        }

        private static object GetFieldValue(object obj, string fieldName)
        {
            if (obj == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(obj.GetType(), fieldName);
            return field == null ? null : field.GetValue(obj);
        }

        private static object InvokeMethod(object obj, string methodName, object[] args)
        {
            if (obj == null)
            {
                return null;
            }

            MethodInfo method = AccessTools.Method(obj.GetType(), methodName, args == null ? Type.EmptyTypes : null);
            if (method == null)
            {
                method = AccessTools.Method(obj.GetType(), methodName);
            }

            return method == null ? null : method.Invoke(obj, args);
        }

        private static bool TryReadBool(object obj, string propertyName)
        {
            object value = GetPropertyValue(obj, propertyName);
            return value is bool && (bool)value;
        }

        private static bool TryReadInt(object obj, string propertyName, out int value)
        {
            value = 0;
            object raw = GetPropertyValue(obj, propertyName);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadFloat(object obj, string propertyName, out float value)
        {
            value = 0f;
            object raw = GetPropertyValue(obj, propertyName);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LogAccessorFailure(string message)
        {
            if (_accessorFailureLogged)
            {
                return;
            }

            _accessorFailureLogged = true;
            Logger.LogWarning(message);
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        private sealed class EquipmentDisplayItemLookup
        {
            private readonly Dictionary<string, object>
                _itemsById =
                    new Dictionary<string, object>(
                        StringComparer.Ordinal);
            private readonly Dictionary<string, DisplayCandidate>
                _itemsByTemplate =
                    new Dictionary<string, DisplayCandidate>(
                        StringComparer.Ordinal);

            public void Add(
                object item,
                string itemId,
                string templateGuid,
                int level,
                float weight)
            {
                if (!string.IsNullOrEmpty(itemId)
                    && !_itemsById.ContainsKey(itemId))
                {
                    _itemsById[itemId] = item;
                }
                if (string.IsNullOrEmpty(templateGuid))
                {
                    return;
                }

                DisplayCandidate current;
                if (!_itemsByTemplate.TryGetValue(
                        templateGuid,
                        out current)
                    || level > current.Level
                    || (level == current.Level
                        && weight < current.Weight))
                {
                    _itemsByTemplate[templateGuid] =
                        new DisplayCandidate(
                            item,
                            level,
                            weight);
                }
            }

            public object Find(
                string itemId,
                string templateGuid)
            {
                object item;
                if (!string.IsNullOrEmpty(itemId)
                    && _itemsById.TryGetValue(
                        itemId,
                        out item))
                {
                    return item;
                }

                DisplayCandidate candidate;
                return !string.IsNullOrEmpty(templateGuid)
                    && _itemsByTemplate.TryGetValue(
                        templateGuid,
                        out candidate)
                        ? candidate.Item
                        : null;
            }

            private sealed class DisplayCandidate
            {
                public readonly object Item;
                public readonly int Level;
                public readonly float Weight;

                public DisplayCandidate(
                    object item,
                    int level,
                    float weight)
                {
                    Item = item;
                    Level = level;
                    Weight = weight;
                }
            }
        }

        private sealed class VirtualWeaponLoadoutData
        {
            public string MainHandGuid;
            public string OffHandGuid;
            public string QuiverGuid;

            public bool Equals(VirtualWeaponLoadoutData other)
            {
                return other != null
                    && string.Equals(
                        MainHandGuid,
                        other.MainHandGuid,
                        StringComparison.Ordinal)
                    && string.Equals(
                        OffHandGuid,
                        other.OffHandGuid,
                        StringComparison.Ordinal)
                    && string.Equals(
                        QuiverGuid,
                        other.QuiverGuid,
                        StringComparison.Ordinal);
            }

        }

        private sealed class QuickWheelLoadoutProxy
        {
            public readonly VCQuickLoadout Option;
            public readonly int Slot;
            public readonly bool IsClone;
            public readonly VQuickUseWheelUI Owner;
            public readonly Vector3 PrimaryLocalPosition;
            public readonly Vector3 SecondaryLocalPosition;
            public GameObject AmmoCounterRoot;
            public TMP_Text AmmoCounter;

            public QuickWheelLoadoutProxy(
                VCQuickLoadout option,
                int slot,
                bool isClone,
                VQuickUseWheelUI owner,
                Vector3 primaryLocalPosition,
                Vector3 secondaryLocalPosition)
            {
                Option = option;
                Slot = slot;
                IsClone = isClone;
                Owner = owner;
                PrimaryLocalPosition =
                    primaryLocalPosition;
                SecondaryLocalPosition =
                    secondaryLocalPosition;
            }
        }

        private sealed class EquipmentQuickPanel
        {
            public readonly object View;
            public GameObject Root;
            public readonly List<CanvasGroupVisibilitySnapshot> HiddenSlots;
            public readonly List<EquipmentQuickSlot> Slots;

            public EquipmentQuickPanel(
                object view,
                GameObject root,
                List<CanvasGroupVisibilitySnapshot> hiddenSlots,
                List<EquipmentQuickSlot> slots)
            {
                View = view;
                Root = root;
                HiddenSlots = hiddenSlots;
                Slots = slots;
            }

            public void Release()
            {
                for (int i = 0; i < Slots.Count; i++)
                {
                    Slots[i].Release();
                }
                for (int i = 0; i < HiddenSlots.Count; i++)
                {
                    HiddenSlots[i].Restore();
                }
                if (Root != null)
                {
                    Root.SetActive(false);
                    UnityEngine.Object.Destroy(Root);
                }
            }
        }

        private sealed class EquipmentQuickSlot
        {
            public readonly int Slot;
            public readonly Image Background;
            public readonly GameObject Selection;
            public readonly TMP_Text Label;
            public readonly TMP_Text Quantity;
            public readonly EquipmentIconSlot Icon;
            public readonly EquipmentButtonVisualState
                ButtonState;

            public EquipmentQuickSlot(
                int slot,
                Image background,
                GameObject selection,
                TMP_Text label,
                TMP_Text quantity,
                EquipmentIconSlot icon,
                EquipmentButtonVisualState buttonState)
            {
                Slot = slot;
                Background = background;
                Selection = selection;
                Label = label;
                Quantity = quantity;
                Icon = icon;
                ButtonState = buttonState;
            }

            public void Release()
            {
                Icon.Release();
            }
        }

        private struct CanvasGroupVisibilitySnapshot
        {
            private readonly CanvasGroup _canvasGroup;
            private readonly float _alpha;
            private readonly bool _interactable;
            private readonly bool _blocksRaycasts;
            private readonly Selectable[] _selectables;
            private readonly bool[] _selectableEnabled;

            public CanvasGroupVisibilitySnapshot(
                CanvasGroup canvasGroup,
                Selectable[] selectables)
            {
                _canvasGroup = canvasGroup;
                _alpha = canvasGroup.alpha;
                _interactable = canvasGroup.interactable;
                _blocksRaycasts = canvasGroup.blocksRaycasts;
                _selectables = selectables
                    ?? new Selectable[0];
                _selectableEnabled =
                    new bool[_selectables.Length];
                for (int i = 0;
                    i < _selectables.Length;
                    i++)
                {
                    _selectableEnabled[i] =
                        _selectables[i] != null
                        && _selectables[i].enabled;
                }
            }

            public void DisableSelectables()
            {
                for (int i = 0;
                    i < _selectables.Length;
                    i++)
                {
                    if (_selectables[i] != null)
                    {
                        _selectables[i].enabled = false;
                    }
                }
            }

            public void Restore()
            {
                if (_canvasGroup == null)
                {
                    return;
                }

                _canvasGroup.alpha = _alpha;
                _canvasGroup.interactable = _interactable;
                _canvasGroup.blocksRaycasts = _blocksRaycasts;
                for (int i = 0;
                    i < _selectables.Length;
                    i++)
                {
                    if (_selectables[i] != null)
                    {
                        _selectables[i].enabled =
                            _selectableEnabled[i];
                    }
                }
            }
        }

        private sealed class EquipmentWeaponPanel
        {
            public readonly object View;
            public GameObject Root;
            public readonly List<VanillaLoadoutVisibilitySnapshot> VanillaRows;
            public readonly List<EquipmentWeaponRow> Rows;

            public EquipmentWeaponPanel(
                object view,
                GameObject root,
                List<VanillaLoadoutVisibilitySnapshot> vanillaRows,
                List<EquipmentWeaponRow> rows)
            {
                View = view;
                Root = root;
                VanillaRows = vanillaRows;
                Rows = rows;
            }

            public void Release()
            {
                for (int i = 0; i < Rows.Count; i++)
                {
                    Rows[i].Release();
                }
                for (int i = 0; i < VanillaRows.Count; i++)
                {
                    VanillaRows[i].Restore();
                }
                if (Root != null)
                {
                    Root.SetActive(false);
                    UnityEngine.Object.Destroy(Root);
                }
            }
        }

        private sealed class EquipmentWeaponRow
        {
            public readonly int Slot;
            public readonly Image Background;
            public readonly GameObject Selection;
            public readonly TMP_Text Label;
            public readonly EquipmentIconSlot MainHand;
            public readonly EquipmentIconSlot Secondary;
            public readonly EquipmentButtonVisualState
                ButtonState;

            public EquipmentWeaponRow(
                int slot,
                Image background,
                GameObject selection,
                TMP_Text label,
                EquipmentIconSlot mainHand,
                EquipmentIconSlot secondary,
                EquipmentButtonVisualState buttonState)
            {
                Slot = slot;
                Background = background;
                Selection = selection;
                Label = label;
                MainHand = mainHand;
                Secondary = secondary;
                ButtonState = buttonState;
            }

            public void Release()
            {
                MainHand.Release();
                Secondary.Release();
            }
        }

        private sealed class EquipmentButtonVisualState
        {
            private readonly Action _changed;
            private bool _hovered;
            private bool _selected;

            public bool Focused
            {
                get
                {
                    return _hovered || _selected;
                }
            }

            public EquipmentButtonVisualState(
                Action changed)
            {
                _changed = changed;
            }

            public void SetHovered(bool hovered)
            {
                if (_hovered == hovered)
                {
                    return;
                }

                _hovered = hovered;
                _changed?.Invoke();
            }

            public void SetSelected(bool selected)
            {
                if (_selected == selected)
                {
                    return;
                }

                _selected = selected;
                _changed?.Invoke();
            }
        }

        private sealed class EquipmentIconSlot
        {
            public readonly Image Image;
            public object Item;
            public object SpriteReference;

            public EquipmentIconSlot(Image image)
            {
                Image = image;
            }

            public void Release()
            {
                if (SpriteReference != null)
                {
                    ReleaseSpriteReference(SpriteReference);
                    SpriteReference = null;
                }
                Item = null;
            }
        }

        private struct VanillaLoadoutVisibilitySnapshot
        {
            private readonly GameObject _gameObject;
            private readonly bool _activeSelf;

            public VanillaLoadoutVisibilitySnapshot(
                GameObject gameObject,
                bool activeSelf)
            {
                _gameObject = gameObject;
                _activeSelf = activeSelf;
            }

            public void Restore()
            {
                if (_gameObject != null)
                {
                    _gameObject.SetActive(_activeSelf);
                }
            }
        }

        private sealed class StatusHudLayoutSnapshot
        {
            public readonly RectTransform RectTransform;
            public readonly Vector2 AnchoredPosition;
            public readonly Vector2 SizeDelta;
            public readonly Vector3 LocalScale;
            public readonly Vector2 AnchorMin;
            public readonly Vector2 AnchorMax;
            public readonly Vector2 Pivot;
            public readonly LayoutGroup LayoutGroup;
            public readonly bool LayoutGroupEnabled;
            public readonly ContentSizeFitter ContentSizeFitter;
            public readonly bool ContentSizeFitterEnabled;
            public readonly Vector2 BaseSpacing;
            public readonly int PaddingLeft;
            public readonly int PaddingRight;
            public readonly int PaddingTop;
            public readonly int PaddingBottom;
            public Vector2 BaseCellSize;

            private readonly Dictionary<int, StatusChildTransformSnapshot>
                _childSnapshots =
                    new Dictionary<int, StatusChildTransformSnapshot>();

            public StatusHudLayoutSnapshot(
                RectTransform rectTransform,
                LayoutGroup layoutGroup,
                ContentSizeFitter contentSizeFitter,
                Vector2 baseCellSize,
                Vector2 baseSpacing,
                int paddingLeft,
                int paddingRight,
                int paddingTop,
                int paddingBottom)
            {
                RectTransform = rectTransform;
                AnchoredPosition = rectTransform.anchoredPosition;
                SizeDelta = rectTransform.sizeDelta;
                LocalScale = rectTransform.localScale;
                AnchorMin = rectTransform.anchorMin;
                AnchorMax = rectTransform.anchorMax;
                Pivot = rectTransform.pivot;
                LayoutGroup = layoutGroup;
                LayoutGroupEnabled =
                    layoutGroup != null && layoutGroup.enabled;
                ContentSizeFitter = contentSizeFitter;
                ContentSizeFitterEnabled =
                    contentSizeFitter != null && contentSizeFitter.enabled;
                BaseCellSize = baseCellSize;
                BaseSpacing = baseSpacing;
                PaddingLeft = paddingLeft;
                PaddingRight = paddingRight;
                PaddingTop = paddingTop;
                PaddingBottom = paddingBottom;
            }

            public void CaptureChild(RectTransform child)
            {
                int id = child.GetInstanceID();
                if (!_childSnapshots.ContainsKey(id))
                {
                    _childSnapshots[id] =
                        new StatusChildTransformSnapshot(child);
                }
            }

            public void RestoreChildren()
            {
                foreach (StatusChildTransformSnapshot snapshot
                    in _childSnapshots.Values)
                {
                    snapshot.Restore();
                }

                _childSnapshots.Clear();
            }
        }

        private struct StatusChildTransformSnapshot
        {
            private readonly RectTransform _rectTransform;
            private readonly Vector2 _anchorMin;
            private readonly Vector2 _anchorMax;
            private readonly Vector2 _anchoredPosition;

            public StatusChildTransformSnapshot(RectTransform rectTransform)
            {
                _rectTransform = rectTransform;
                _anchorMin = rectTransform.anchorMin;
                _anchorMax = rectTransform.anchorMax;
                _anchoredPosition = rectTransform.anchoredPosition;
            }

            public void Restore()
            {
                if (_rectTransform == null)
                {
                    return;
                }

                _rectTransform.anchorMin = _anchorMin;
                _rectTransform.anchorMax = _anchorMax;
                _rectTransform.anchoredPosition = _anchoredPosition;
            }
        }

        private struct HudTransformSnapshot
        {
            public readonly RectTransform RectTransform;
            public readonly Vector2 AnchoredPosition;
            public readonly Vector3 LocalScale;
            public readonly Vector2 AnchorMin;
            public readonly Vector2 AnchorMax;
            public readonly Vector2 Pivot;

            public HudTransformSnapshot(
                RectTransform rectTransform,
                Vector2 anchoredPosition,
                Vector3 localScale,
                Vector2 anchorMin,
                Vector2 anchorMax,
                Vector2 pivot)
            {
                RectTransform = rectTransform;
                AnchoredPosition = anchoredPosition;
                LocalScale = localScale;
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
                Pivot = pivot;
            }

            public HudTransformSnapshot WithLocalScale(Vector3 localScale)
            {
                return new HudTransformSnapshot(
                    RectTransform,
                    AnchoredPosition,
                    localScale,
                    AnchorMin,
                    AnchorMax,
                    Pivot);
            }
        }

        private struct ArrowCounterTextSnapshot
        {
            public readonly TMP_Text Text;
            public readonly float FontSize;
            public readonly float FontSizeMin;
            public readonly float FontSizeMax;

            public ArrowCounterTextSnapshot(TMP_Text text)
            {
                Text = text;
                FontSize = text.fontSize;
                FontSizeMin = text.fontSizeMin;
                FontSizeMax = text.fontSizeMax;
            }

            public void Restore()
            {
                if (Text == null)
                {
                    return;
                }

                Text.fontSize = FontSize;
                Text.fontSizeMin = FontSizeMin;
                Text.fontSizeMax = FontSizeMax;
            }
        }

        private struct HudSiblingSnapshot
        {
            public readonly Transform Transform;
            public readonly Transform Parent;
            public readonly int SiblingIndex;

            public HudSiblingSnapshot(
                Transform transform,
                Transform parent,
                int siblingIndex)
            {
                Transform = transform;
                Parent = parent;
                SiblingIndex = siblingIndex;
            }
        }

        private struct WyrdSkillPromptSnapshot
        {
            public readonly GameObject Prompt;
            public readonly bool ActiveSelf;

            public WyrdSkillPromptSnapshot(GameObject prompt, bool activeSelf)
            {
                Prompt = prompt;
                ActiveSelf = activeSelf;
            }
        }

        private struct QuickUseHudObjectSnapshot
        {
            public readonly GameObject GameObject;
            public readonly bool ActiveSelf;

            public QuickUseHudObjectSnapshot(
                GameObject gameObject,
                bool activeSelf)
            {
                GameObject = gameObject;
                ActiveSelf = activeSelf;
            }
        }

        private sealed class
            CharacterPointsQuickUseSnapshot
        {
            public readonly CanvasGroup CanvasGroup;
            public readonly bool ActiveSelf;
            public readonly float Alpha;
            public readonly bool Interactable;
            public readonly bool BlocksRaycasts;
            public readonly float FadeStartTime;
            public readonly float FadeEndTime;

            public CharacterPointsQuickUseSnapshot(
                CanvasGroup canvasGroup,
                bool activeSelf,
                float alpha,
                bool interactable,
                bool blocksRaycasts,
                float fadeStartTime,
                float fadeEndTime)
            {
                CanvasGroup = canvasGroup;
                ActiveSelf = activeSelf;
                Alpha = alpha;
                Interactable = interactable;
                BlocksRaycasts = blocksRaycasts;
                FadeStartTime = fadeStartTime;
                FadeEndTime = fadeEndTime;
            }
        }

        private sealed class SmartIconOverlay
        {
            public readonly Image SourceImage;
            public readonly GameObject OverlayObject;
            public readonly Image OverlayImage;
            public readonly Color SourceColor;
            public readonly bool SourceEnabled;
            public object Item;
            public object SpriteReference;

            public SmartIconOverlay(
                Image sourceImage,
                GameObject overlayObject,
                Image overlayImage,
                Color sourceColor,
                bool sourceEnabled)
            {
                SourceImage = sourceImage;
                OverlayObject = overlayObject;
                OverlayImage = overlayImage;
                SourceColor = sourceColor;
                SourceEnabled = sourceEnabled;
            }
        }

        private struct Candidate
        {
            public readonly object Item;
            public readonly float Amount;

            public Candidate(object item, float amount)
            {
                Item = item;
                Amount = amount;
            }
        }

        private static class HeroItemsAfterInitPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ForceFoodSelected(__instance);
                }
            }
        }

        private static class SelectNextQuickSlotPatch
        {
            public static bool Prefix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null || !plugin.ShouldPinFoodSlot() || plugin._forceSelectingFood)
                {
                    return true;
                }

                plugin.ForceFoodSelected(__instance);
                return false;
            }
        }

        private static class SelectQuickSlotPatch
        {
            public static bool Prefix(object __instance, object equipmentSlotType)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null || !plugin.ShouldPinFoodSlot() || plugin._forceSelectingFood)
                {
                    return true;
                }

                if (plugin.IsFoodSlot(equipmentSlotType))
                {
                    return true;
                }

                if (plugin.IsManualQuickSlot(equipmentSlotType))
                {
                    plugin.ForceFoodSelected(__instance);
                    return false;
                }

                return true;
            }
        }

        private static class EquipFoodPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null && !plugin._refreshingFoodSlot)
                {
                    plugin.RefreshFoodSlotChoice(__instance);
                }
            }
        }

        private static class EquipmentChooseUiTooltipCleanupPatch
        {
            public static void Prefix()
            {
                ClearItemPickerTooltip();
            }
        }

        private static class SelectedQuickSlotOnAttachPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin._activeSelectedQuickSlotView = __instance;
                    plugin.MarkHudLayoutDirty(
                        HudLayoutDirty.QuickSlotContent
                        | HudLayoutDirty.QuickSlotTransform
                        | HudLayoutDirty.Arrow
                        | HudLayoutDirty.DrawOrder);
                    plugin.ApplyQuickUseWheelHudVisibility();
                }
            }
        }

        private static class SelectedQuickSlotUpdateIconPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin._activeSelectedQuickSlotView = __instance;
                    plugin.MarkHudLayoutDirty(
                        HudLayoutDirty.QuickSlotContent
                        | HudLayoutDirty.QuickSlotTransform
                        | HudLayoutDirty.Arrow
                        | HudLayoutDirty.DrawOrder);
                }
            }
        }

        private static class SelectedQuickSlotOnDiscardPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    Component component = __instance as Component;
                    plugin.RestoreHudSiblingOrder(
                        component == null ? null : component.transform);
                    plugin.ReleaseViewQuickSlotHudTransform(__instance);
                    plugin.ReleaseViewSmartIcons(__instance);
                    if (ReferenceEquals(plugin._activeSelectedQuickSlotView, __instance))
                    {
                        plugin._activeSelectedQuickSlotView = null;
                    }
                }
            }
        }

        private static class HeroHudAfterFullyInitializedPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin._activeHeroHudView = __instance;
                    plugin.MarkHudLayoutDirty(
                        HudLayoutDirty.HeroHud
                        | HudLayoutDirty.StatusHud
                        | HudLayoutDirty.QuickSlotTransform
                        | HudLayoutDirty.Arrow
                        | HudLayoutDirty.WyrdSkillIndicator
                        | HudLayoutDirty.WyrdSkillPrompt
                        | HudLayoutDirty.DrawOrder);
                    plugin.RefreshHeroHudTimer(__instance);
                    plugin.ApplyQuickUseWheelHudVisibility();
                }
            }
        }

        private static class HeroHudOnDiscardPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseViewHudDrawOrder(__instance);
                    plugin.ReleaseViewWyrdSkillPrompt(__instance);
                    plugin.ReleaseViewArrowHudTransform(__instance);
                    plugin.ReleaseViewWyrdSkillIndicatorTransform(__instance);
                    plugin.ReleaseViewHeroHudTransform(__instance);
                    if (ReferenceEquals(plugin._activeHeroHudView, __instance))
                    {
                        plugin._activeHeroHudView = null;
                    }
                }
            }
        }

        private static class HeroHudUpdateHeroBarsScalePatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RefreshHeroHudBaseScale(__instance);
                }
            }
        }

        private static class HeroHudHandleShowHudTimerPatch
        {
            public static bool Prefix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null || !plugin.ShouldControlHeroHudTimer())
                {
                    return true;
                }

                plugin.HandleHeroHudTimer(__instance);
                return false;
            }
        }

        private static class HeroHudResetTimerPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ResetHeroHudTimer(__instance);
                }
            }
        }

        private static class HeroHudInitShowHudTimerPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ResetHeroHudTimer(__instance);
                }
            }
        }

        private static class HeroStatusHudOnAttachPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterHeroStatusHud(__instance);
                }
            }
        }

        private static class HeroStatusHudOnDiscardPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseHeroStatusHud(__instance);
                }
            }
        }

        private static class StatusHudUpdateStatusViewPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null && IsHeroStatusHud(__instance))
                {
                    plugin.RegisterHeroStatusHud(__instance);
                }
            }
        }

        private static class CompassOnInitializePatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterCompassView(__instance);
                }
            }
        }

        private static class CompassOnUiStateChangedPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin._activeCompassView = __instance;
                    plugin.RefreshCompassVisibility(true);
                }
            }
        }

        private static class CharacterPointsOnAttachPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterCharacterPointsView(__instance);
                }
            }
        }

        private static class CharacterPointsUpdateVisualPatch
        {
            public static bool Prefix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null)
                {
                    return true;
                }

                plugin._activeCharacterPointsView = __instance;
                if (plugin
                    .ShouldSuppressCharacterPointsRefresh())
                {
                    return false;
                }
                if (plugin.ShouldShowCharacterPoints())
                {
                    return true;
                }

                plugin.ApplyCharacterPointsVisibility(__instance);
                return false;
            }

            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null
                    && !plugin
                        .ShouldSuppressCharacterPointsRefresh())
                {
                    plugin.CompleteCharacterPointsVisualUpdate(
                        __instance);
                }
            }
        }

        private static class CharacterPointsOnUiStateChangedPatch
        {
            public static void Prefix(UIState state)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.PrepareCharacterPointsUiStateChange(
                        state);
                }
            }
        }

        private static class CharacterPointsOnDestroyPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseCharacterPointsView(__instance);
                }
            }
        }

        private static class HeroKeysEquipLoadoutPatch
        {
            public static bool Prefix(int index)
            {
                GloriousUIPlugin plugin = Instance;
                return plugin == null
                    || !plugin.TryRedirectVanillaWeaponLoadout(
                        index);
            }
        }

        private static class SmartInventoryBagActionPatch
        {
            public static bool Prefix(
                UIEvent evt,
                ref UIResult __result)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null
                    || !plugin
                        .ShouldSuppressVanillaCharacterSheetAction(
                            evt))
                {
                    return true;
                }

                __result = UIResult.Accept;
                return false;
            }

            public static void Postfix(UIEvent evt)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.HandleVanillaEquipmentAction(evt);
                }
            }
        }

        private static class HeldApplyChangesPromptPatch
        {
            public static void Prefix(ref KeyBindings key)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RedirectHeldApplyChangesPrompt(ref key);
                }
            }
        }

        private static class RestoreDefaultsPromptPatch
        {
            public static void Prefix(ref KeyBindings key)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RedirectRestoreDefaultsPrompt(ref key);
                }
            }
        }

        private static class QuickUseWheelOnInitializePatch
        {
            public static void Postfix(
                VQuickUseWheelUI __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterQuickUseWheel(
                        __instance);
                }
            }
        }

        private static class QuickUseWheelOnDiscardPatch
        {
            public static void Postfix(
                VQuickUseWheelUI __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseQuickUseWheel(
                        __instance);
                }
            }
        }

        private static class QuickWheelLoadoutRefreshPatch
        {
            public static bool Prefix(
                VCQuickLoadout __instance)
            {
                GloriousUIPlugin plugin = Instance;
                return plugin == null
                    || !plugin.RefreshQuickWheelLoadoutProxy(
                        __instance);
            }
        }

        private static class QuickWheelLoadoutOnShowPatch
        {
            public static bool Prefix(
                VCQuickLoadout __instance)
            {
                GloriousUIPlugin plugin = Instance;
                return plugin == null
                    || !plugin.ShowQuickWheelLoadoutTooltips(
                        __instance);
            }
        }

        private static class QuickWheelLoadoutOnSelectPatch
        {
            public static bool Prefix(
                VCQuickLoadout __instance)
            {
                GloriousUIPlugin plugin = Instance;
                return plugin == null
                    || !plugin.SelectQuickWheelLoadout(
                        __instance);
            }
        }

        private static class QuickWheelOptionHoverStartPatch
        {
            public static void Postfix(
                VCQuickUseOption __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.SetHoveredQuickWheelLoadout(
                        __instance as VCQuickLoadout,
                        true);
                }
            }
        }

        private static class QuickWheelOptionHoverEndPatch
        {
            public static void Postfix(
                VCQuickUseOption __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.SetHoveredQuickWheelLoadout(
                        __instance as VCQuickLoadout,
                        false);
                }
            }
        }

        private static class QuickWheelLoadoutDescriptionPatch
        {
            public static bool Prefix(
                VCQuickLoadout __instance,
                ref VCRadialMenuOption<QuickUseWheelUI>
                    .OptionDescription __result)
            {
                GloriousUIPlugin plugin = Instance;
                VCRadialMenuOption<QuickUseWheelUI>
                    .OptionDescription description;
                if (plugin == null
                    || !plugin.DescribeQuickWheelLoadout(
                        __instance,
                        out description))
                {
                    return true;
                }

                __result = description;
                return false;
            }
        }

        private static class QuickWheelInitialOptionPatch
        {
            public static bool Prefix(
                VCRadialMenuOption<QuickUseWheelUI>[]
                    options,
                ref VCRadialMenuOption<QuickUseWheelUI>
                    __result)
            {
                GloriousUIPlugin plugin = Instance;
                VCRadialMenuOption<QuickUseWheelUI>
                    result;
                if (plugin == null
                    || !plugin.TryGetQuickWheelInitialOption(
                        options,
                        out result))
                {
                    return true;
                }

                __result = result;
                return false;
            }
        }

        private static class LoadoutsViewOnInitializePatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterLoadoutsView(__instance);
                }
            }
        }

        private static class LoadoutsViewOnDiscardPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseLoadoutsView(__instance);
                }
            }
        }

        private static class EquipmentViewOnInitializePatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.RegisterEquipmentView(__instance);
                }
            }
        }

        private static class EquipmentViewOnDiscardPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseEquipmentView(__instance);
                }
            }
        }

        private static class EquipmentSlotEquipPatch
        {
            public static void Postfix(object __instance, object item)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.CaptureEquipmentQuickSlotItem(
                        __instance,
                        item);
                }
            }
        }

        private static class WeaponLoadoutSlotEquipPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.CaptureEquipmentWeaponSlotChange(
                        __instance);
                }
            }
        }

        private static class WeaponLoadoutSlotUnequipPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.CaptureEquipmentWeaponSlotChange(
                        __instance);
                }
            }
        }

        private static class EquipmentSlotUnequipPatch
        {
            public static void Postfix(object __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.CaptureEquipmentQuickSlotItem(
                        __instance,
                        null);
                }
            }
        }

        private static class OneMenuChooseUiConstructorPatch
        {
            public static void Prefix(
                ref IEquipmentSlot targetSlot)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null
                    || !plugin.ShouldUseOneMenuEquip()
                    || plugin._oneMenuEquipRedirectOffHandPicker
                        == null
                    || !plugin._oneMenuEquipRedirectOffHandPicker
                        .Value
                    || targetSlot == null
                    || targetSlot.Type
                        != EquipmentSlotType.OffHand)
                {
                    return;
                }

                VCLoadoutSlot offHandSlot =
                    targetSlot as VCLoadoutSlot;
                if (offHandSlot == null)
                {
                    return;
                }

                VCLoadoutSlot[] slots =
                    UnityEngine.Object.FindObjectsByType<
                        VCLoadoutSlot>(
                        FindObjectsSortMode.None);
                for (int i = 0; i < slots.Length; i++)
                {
                    VCLoadoutSlot slot = slots[i];
                    if (slot != null
                        && ReferenceEquals(
                            slot.Loadout,
                            offHandSlot.Loadout)
                        && slot.Type
                            == EquipmentSlotType.MainHand)
                    {
                        targetSlot = slot;
                        return;
                    }
                }
            }
        }

        private static class QuestNotificationDurationPatch
        {
            public static void Postfix(ref float __result)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    __result = plugin.GetQuestNotificationDuration(
                        __result);
                }
            }
        }

        private static class OneMenuSelectCurrentPatch
        {
            public static bool Prefix(
                EquipmentChooseUI __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null
                    || !plugin.ShouldApplyOneMenuEquipTo(
                        __instance)
                    || !plugin.ShouldInterceptOneMenuChooseClick()
                    || !plugin.IsOneMenuEquipShortcutDownThisFrame())
                {
                    return true;
                }

                Item item =
                    plugin._oneMenuLastHoveredItem;
                if (plugin.ShouldToggleOneMenuEquippedItem()
                    && ReferenceEquals(
                        plugin._oneMenuLastChooseUi,
                        __instance)
                    && item != null
                    && !item.HasBeenDiscarded
                    && item.IsEquipped)
                {
                    plugin.PerformOneMenuUnequip(
                        __instance,
                        item);
                    return false;
                }

                return plugin
                    ._oneMenuExecutingMainHandEquip;
            }
        }

        private static class OneMenuUnequipItemPatch
        {
            public static bool Prefix(
                EquipmentChooseUI __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null
                    || !plugin.ShouldApplyOneMenuEquipTo(
                        __instance)
                    || !plugin.ShouldToggleOneMenuEquippedItem()
                    || !plugin.ShouldInterceptOneMenuChooseClick()
                    || !plugin.IsOneMenuEquipShortcutDownThisFrame()
                    || !ReferenceEquals(
                        plugin._oneMenuLastChooseUi,
                        __instance))
                {
                    return true;
                }

                Item item =
                    plugin._oneMenuLastHoveredItem;
                if (item == null
                    || item.HasBeenDiscarded
                    || !item.IsEquipped)
                {
                    return true;
                }

                plugin.PerformOneMenuUnequip(
                    __instance,
                    item);
                return false;
            }
        }

        private static class OneMenuHoveredItemsChangedPatch
        {
            public static void Postfix(
                EquipmentChooseUI __instance,
                Item item)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.TrackOneMenuHoveredItem(
                        __instance,
                        item);
                }
            }
        }

        private static class OneMenuChooseUiDiscardPatch
        {
            public static void Prefix(
                EquipmentChooseUI __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseOneMenuChooseUi(
                        __instance);
                }
            }
        }

        private static class OneMenuBagUseItemPatch
        {
            public static bool Prefix(
                BagUI __instance)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin == null
                    || !plugin.ShouldInterceptOneMenuBagClick()
                    || !plugin.IsOneMenuEquipShortcutDownThisFrame()
                    || !ReferenceEquals(
                        plugin._oneMenuLastBagUi,
                        __instance))
                {
                    return true;
                }

                Item item =
                    plugin._oneMenuLastBagHoveredItem;
                return item == null
                    || item.HasBeenDiscarded
                    || item.IsEquipped
                    || !IsUsableOneMenuItem(
                        item,
                        requireWeapon: true);
            }
        }

        private static class OneMenuBagRefreshPromptsPatch
        {
            public static void Postfix(
                BagUI __instance,
                Item item)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.TrackOneMenuBagHoveredItem(
                        __instance,
                        item);
                }
            }
        }

        private static class CloudServiceEndLoadSlotPatch
        {
            public static void Prefix(
                object __instance,
                string slotId)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    try
                    {
                        plugin.LoadStandaloneEquipmentState(
                            __instance,
                            slotId);
                    }
                    catch (Exception exception)
                    {
                        Log?.LogWarning(
                            "Could not restore Glorious Equipment data; vanilla save loading will continue. "
                            + exception.GetBaseException().Message);
                    }
                }
            }
        }

        private static class NewGameDropPreviousDomainsPatch
        {
            public static void Prefix()
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ClearStandaloneEquipmentSession();
                }
            }
        }

        private static class CloudServiceEndSavePatch
        {
            public static void Prefix(
                object __instance,
                string slotId)
            {
                GloriousUIPlugin plugin = Instance;
                if (plugin != null)
                {
                    try
                    {
                        plugin.CaptureCurrentStandaloneWeaponLoadout();
                        plugin.SaveStandaloneEquipmentState(
                            writeToArchive: true,
                            cloudService: __instance,
                            slotId: slotId);
                    }
                    catch (Exception exception)
                    {
                        Log?.LogWarning(
                            "Could not store Glorious Equipment data; vanilla save finalization will continue. "
                            + exception.GetBaseException().Message);
                    }
                }
            }
        }

    }
}
