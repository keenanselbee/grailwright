using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[assembly: AssemblyTitle("Better Quick Slots")]
[assembly: AssemblyDescription("Smart food, health, mana, and arrow quick-slot HUD for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Better Quick Slots")]
[assembly: AssemblyVersion("0.2.2.0")]
[assembly: AssemblyFileVersion("0.2.2.0")]
[assembly: AssemblyInformationalVersion("0.2.2")]

namespace BetterQuickSlots
{
    public enum SmartSelectionMode
    {
        Biggest,
        SmallestSufficient
    }

    internal enum SmartConsumableKind
    {
        Food,
        HealthPotion,
        ManaPotion
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BetterQuickSlotsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.better-quick-slots";
        public const string PluginName = "Better Quick Slots";
        public const string PluginVersion = "0.2.2";

        private const int ConfigSchemaVersion = 4;
        private const float FoodPinIntervalSeconds = 0.25f;
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

        private static readonly string[] HealthTerms = { "health", "healing", "heal", "vitality" };
        private static readonly string[] ManaTerms = { "mana", "magicka", "magic" };

        internal static BetterQuickSlotsPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _pinHudToFoodSlot;
        private ConfigEntry<bool> _replaceSmallHudSlots;
        private ConfigEntry<bool> _ownArrowSlot;
        private ConfigEntry<bool> _hideCyclePrompt;
        private ConfigEntry<float> _hudOffsetX;
        private ConfigEntry<float> _hudOffsetY;
        private ConfigEntry<float> _hudScale;
        private ConfigEntry<float> _arrowSlotOffsetX;
        private ConfigEntry<float> _arrowSlotOffsetY;
        private ConfigEntry<float> _arrowSlotScale;
        private ConfigEntry<KeyboardShortcut> _healthPotionHotkey;
        private ConfigEntry<KeyboardShortcut> _manaPotionHotkey;
        private ConfigEntry<SmartSelectionMode> _foodSelectionMode;
        private ConfigEntry<SmartSelectionMode> _healthPotionSelectionMode;
        private ConfigEntry<SmartSelectionMode> _manaPotionSelectionMode;
        private ConfigEntry<bool> _preventPotionWasteAtFull;
        private ConfigEntry<bool> _ignoreHotkeysWhenCursorVisible;
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
        private FieldInfo _nextStaticPromptField;
        private FieldInfo _heroHudSelectedQuickSlotField;
        private FieldInfo _heroHudArrowsImageField;
        private FieldInfo _heroHudArrowsCounterField;

        private MethodInfo _selectQuickSlotMethod;
        private MethodInfo _equippedItemMethod;
        private MethodInfo _equipMethod;
        private MethodInfo _itemUseMethod;
        private MethodInfo _getVariableMethod;
        private MethodInfo _getHealValueMethod;
        private MethodInfo _triggerQuickSlotUsedMethod;

        private object _foodQuickSlot;
        private object _quickSlot2;
        private object _quickSlot3;
        private object _quickSlotUsedEvent;
        private Type _heroHudViewType;

        private readonly Dictionary<int, SmartIconOverlay> _smartIconOverlays =
            new Dictionary<int, SmartIconOverlay>();
        private readonly Dictionary<int, HudTransformSnapshot> _hudTransformSnapshots =
            new Dictionary<int, HudTransformSnapshot>();
        private readonly Vector3[] _worldCorners = new Vector3[4];

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

        private void Update()
        {
            if (!IsEnabled() || !_accessorsReady)
            {
                return;
            }

            if (_ignoreHotkeysWhenCursorVisible.Value && Cursor.visible)
            {
                return;
            }

            MaintainFoodSelection();

            if (_healthPotionHotkey.Value.IsDown())
            {
                TryUseSmartConsumable(SmartConsumableKind.HealthPotion);
                return;
            }

            if (_manaPotionHotkey.Value.IsDown())
            {
                TryUseSmartConsumable(SmartConsumableKind.ManaPotion);
            }
        }

        private void OnDestroy()
        {
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

            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch.");
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                "Configuration layout version. Older layouts are backed up and regenerated.");
            _pinHudToFoodSlot = Config.Bind(
                "2. HUD",
                "PinHudToFoodSlot",
                true,
                "Keep the vanilla quick-slot use key focused on the autofill food slot instead of cycling to manual slots 1 and 2.");
            _replaceSmallHudSlots = Config.Bind(
                "2. HUD",
                "ReplaceSmallHudSlots",
                true,
                "Replace the two small vanilla next-slot icons with smart health and mana potion icons.");
            _ownArrowSlot = Config.Bind(
                "2. HUD",
                "OwnArrowSlot",
                true,
                "Move and scale the vanilla arrow counter into the Better Quick Slots cluster.");
            _hideCyclePrompt = Config.Bind(
                "2. HUD",
                "HideCyclePrompt",
                true,
                "Hide the vanilla next-slot prompt beside the small icons.");
            _hudOffsetX = Config.Bind(
                "2. HUD",
                "HudOffsetX",
                0.0f,
                "Horizontal HUD offset in UI pixels from the vanilla quick-slot position. Positive moves right.");
            _hudOffsetY = Config.Bind(
                "2. HUD",
                "HudOffsetY",
                0.0f,
                "Vertical HUD offset in UI pixels from the vanilla quick-slot position. Positive moves up.");
            _hudScale = Config.Bind(
                "2. HUD",
                "HudScale",
                1.0f,
                "Quick-slot HUD scale multiplier. Values below 0.25 are clamped, and neutral is 1.0.");
            _arrowSlotOffsetX = Config.Bind(
                "2. HUD",
                "ArrowSlotOffsetX",
                0.0f,
                "Horizontal arrow slot offset in UI pixels after Better Quick Slots places it. Positive moves right.");
            _arrowSlotOffsetY = Config.Bind(
                "2. HUD",
                "ArrowSlotOffsetY",
                0.0f,
                "Vertical arrow slot offset in UI pixels after Better Quick Slots places it. Positive moves up.");
            _arrowSlotScale = Config.Bind(
                "2. HUD",
                "ArrowSlotScale",
                1.0f,
                "Arrow slot scale multiplier after Better Quick Slots matches the health slot. Values below 0.25 are clamped, and neutral is 1.0.");

            _healthPotionHotkey = Config.Bind(
                "3. Hotkeys",
                "HealthPotionHotkey",
                new KeyboardShortcut(KeyCode.C),
                "Smart health potion hotkey. Food continues to use the game's existing quick-slot use key.");
            _manaPotionHotkey = Config.Bind(
                "3. Hotkeys",
                "ManaPotionHotkey",
                new KeyboardShortcut(KeyCode.V),
                "Smart mana potion hotkey. Set to None to disable.");
            _ignoreHotkeysWhenCursorVisible = Config.Bind(
                "3. Hotkeys",
                "IgnoreHotkeysWhenCursorVisible",
                true,
                "Avoid using smart potions while menu cursors are visible.");

            _foodSelectionMode = Config.Bind(
                "4. Smart Selection",
                "FoodSelectionMode",
                SmartSelectionMode.Biggest,
                "How the autofill food slot chooses food after the current food runs out.");
            _healthPotionSelectionMode = Config.Bind(
                "4. Smart Selection",
                "HealthPotionSelectionMode",
                SmartSelectionMode.SmallestSufficient,
                "How the health potion hotkey chooses a potion.");
            _manaPotionSelectionMode = Config.Bind(
                "4. Smart Selection",
                "ManaPotionSelectionMode",
                SmartSelectionMode.SmallestSufficient,
                "How the mana potion hotkey chooses a potion.");
            _preventPotionWasteAtFull = Config.Bind(
                "4. Smart Selection",
                "PreventPotionWasteAtFull",
                true,
                "Do not use health or mana potions when the corresponding resource is already full.");

            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                "Log smart slot decisions and skipped hotkey uses.");
            _logPatchWarnings = Config.Bind(
                "Diagnostics",
                "LogPatchWarnings",
                true,
                "Log warnings when optional game hooks are unavailable.");
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
                Logger.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
            }
            catch (Exception exception)
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
                catch (Exception restoreException)
                {
                    Logger.LogError(
                        "Could not restore the previous Better Quick Slots config after schema reset failure: "
                        + restoreException.GetBaseException().Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Better Quick Slots config schema. Original config was left in place when possible.",
                    exception);
            }
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

            _heroCurrentProperty = _heroType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            _selectedQuickSlotTypeProperty = RequireProperty(_heroItemsType, "SelectedQuickSlotType");
            _foodQuickSlotField = RequireField(_equipmentSlotType, "FoodQuickSlot");
            _quickSlot2Field = RequireField(_equipmentSlotType, "QuickSlot2");
            _quickSlot3Field = RequireField(_equipmentSlotType, "QuickSlot3");
            _quickSlotUsedEventField = RequireField(heroItemsEventsType, "QuickSlotUsed");
            _itemIconField = AccessTools.Field(selectedQuickSlotViewType, "itemIcon");
            _nextItemIconsField = RequireField(selectedQuickSlotViewType, "nextItemIcons");
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

            _accessorsReady = true;
        }

        private void CacheHeroHudAccessors()
        {
            _heroHudViewType = AccessTools.TypeByName(HeroHudViewTypeName);
            if (_heroHudViewType == null)
            {
                return;
            }

            _heroHudSelectedQuickSlotField = AccessTools.Field(_heroHudViewType, "selectedQuickSlot");
            _heroHudArrowsImageField = AccessTools.Field(_heroHudViewType, "arrowsImage");
            _heroHudArrowsCounterField = AccessTools.Field(_heroHudViewType, "arrowsCounter");
        }

        private bool PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type selectedQuickSlotViewType = AccessTools.TypeByName(SelectedQuickSlotViewTypeName);
            PatchMethod(selectedQuickSlotViewType, "OnAttach", typeof(SelectedQuickSlotOnAttachPatch), "Postfix", false);
            bool requiredPatched = PatchMethod(selectedQuickSlotViewType, "UpdateIcon", typeof(SelectedQuickSlotUpdateIconPatch), "Postfix", true);
            PatchMethod(selectedQuickSlotViewType, "OnDiscard", typeof(SelectedQuickSlotOnDiscardPatch), "Postfix", false);

            Type heroHudViewType = _heroHudViewType ?? AccessTools.TypeByName(HeroHudViewTypeName);
            PatchMethod(heroHudViewType, "AfterFullyInitialized", typeof(HeroHudAfterFullyInitializedPatch), "Postfix", false);
            PatchMethod(heroHudViewType, "Update", typeof(HeroHudUpdatePatch), "Postfix", false);
            PatchMethod(heroHudViewType, "OnDiscard", typeof(HeroHudOnDiscardPatch), "Postfix", false);

            return requiredPatched;
        }

        private bool PatchMethod(Type declaringType, string methodName, Type patchType, string patchMethodName, bool required)
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
                if (string.Equals(patchMethodName, "Prefix", StringComparison.Ordinal))
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
            bool afterInitPatched = PatchMethod(_heroItemsType, "AfterInit", typeof(HeroItemsAfterInitPatch), "Postfix", false);
            bool nextPatched = PatchMethod(_heroItemsType, "SelectNextQuickSlot", typeof(SelectNextQuickSlotPatch), "Prefix", false);
            bool selectPatched = PatchMethod(_heroItemsType, "SelectQuickSlot", typeof(SelectQuickSlotPatch), "Prefix", false);
            bool equipFoodPatched = PatchMethod(_heroItemsType, "EquipFood", typeof(EquipFoodPatch), "Postfix", false);

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
                if (_hideCyclePrompt != null && _hideCyclePrompt.Value)
                {
                    GameObject nextPrompt = _nextStaticPromptField.GetValue(selectedQuickSlotView) as GameObject;
                    if (nextPrompt != null)
                    {
                        nextPrompt.SetActive(false);
                    }
                }

                Image[] nextIcons = _nextItemIconsField.GetValue(selectedQuickSlotView) as Image[];
                if (nextIcons == null || nextIcons.Length == 0)
                {
                    return;
                }

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

        internal void ApplyHudTransform(object selectedQuickSlotView)
        {
            if (selectedQuickSlotView == null)
            {
                return;
            }

            try
            {
                RectTransform rectTransform = GetHudRectTransform(selectedQuickSlotView);
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

                float scale = GetHudScale();
                rectTransform.anchoredPosition = snapshot.AnchoredPosition + new Vector2(GetHudOffsetX(), GetHudOffsetY());
                rectTransform.localScale = new Vector3(
                    snapshot.LocalScale.x * scale,
                    snapshot.LocalScale.y * scale,
                    snapshot.LocalScale.z);
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not apply quick-slot HUD position or scale: " + exception.GetBaseException().Message);
            }
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
                    return;
                }

                if (!HasArrowHudAccessors())
                {
                    RestoreHudTransform(arrowCounterRect);
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
            }
            catch (Exception exception)
            {
                LogAccessorFailure("Could not apply arrow quick-slot HUD layout: " + exception.GetBaseException().Message);
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
            _hudTransformSnapshots.Remove(arrowRect.GetInstanceID());
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

        internal void ReleaseViewHudTransform(object selectedQuickSlotView)
        {
            RectTransform rectTransform = GetHudRectTransform(selectedQuickSlotView);
            if (rectTransform == null)
            {
                return;
            }

            RestoreHudTransform(rectTransform);
            _hudTransformSnapshots.Remove(rectTransform.GetInstanceID());
        }

        private static RectTransform GetHudRectTransform(object selectedQuickSlotView)
        {
            Component component = selectedQuickSlotView as Component;
            if (component != null)
            {
                return component.transform as RectTransform;
            }

            GameObject gameObject = selectedQuickSlotView as GameObject;
            return gameObject == null ? null : gameObject.transform as RectTransform;
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

        private bool HasArrowHudAccessors()
        {
            return _itemIconField != null
                && _nextItemIconsField != null
                && _heroHudSelectedQuickSlotField != null
                && _heroHudArrowsCounterField != null;
        }

        private float GetHudOffsetX()
        {
            return _hudOffsetX == null ? 0.0f : _hudOffsetX.Value;
        }

        private float GetHudOffsetY()
        {
            return _hudOffsetY == null ? 0.0f : _hudOffsetY.Value;
        }

        private float GetHudScale()
        {
            if (_hudScale == null)
            {
                return 1.0f;
            }

            return Mathf.Max(0.25f, _hudScale.Value);
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

            return Mathf.Max(0.25f, _arrowSlotScale.Value);
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
                    rectTransform.localScale);
                _hudTransformSnapshots[id] = snapshot;
            }

            return snapshot;
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
                arrowSnapshot.LocalScale.x * scale * GetArrowSlotScale(),
                arrowSnapshot.LocalScale.y * scale * GetArrowSlotScale(),
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
            arrowCounterRect.anchoredPosition += new Vector2(GetArrowSlotOffsetX(), GetArrowSlotOffsetY());
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
                "BetterQuickSlotsSmartIcon" + index.ToString(CultureInfo.InvariantCulture),
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
            foreach (HudTransformSnapshot snapshot in _hudTransformSnapshots.Values)
            {
                if (snapshot.RectTransform != null)
                {
                    snapshot.RectTransform.anchoredPosition = snapshot.AnchoredPosition;
                    snapshot.RectTransform.localScale = snapshot.LocalScale;
                }
            }

            _hudTransformSnapshots.Clear();
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

        private struct HudTransformSnapshot
        {
            public readonly RectTransform RectTransform;
            public readonly Vector2 AnchoredPosition;
            public readonly Vector3 LocalScale;

            public HudTransformSnapshot(RectTransform rectTransform, Vector2 anchoredPosition, Vector3 localScale)
            {
                RectTransform = rectTransform;
                AnchoredPosition = anchoredPosition;
                LocalScale = localScale;
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
                BetterQuickSlotsPlugin plugin = Instance;
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
                BetterQuickSlotsPlugin plugin = Instance;
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
                BetterQuickSlotsPlugin plugin = Instance;
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
                BetterQuickSlotsPlugin plugin = Instance;
                if (plugin != null && !plugin._refreshingFoodSlot)
                {
                    plugin.RefreshFoodSlotChoice(__instance);
                }
            }
        }

        private static class SelectedQuickSlotOnAttachPatch
        {
            public static void Postfix(object __instance)
            {
                BetterQuickSlotsPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyHudTransform(__instance);
                }
            }
        }

        private static class SelectedQuickSlotUpdateIconPatch
        {
            public static void Postfix(object __instance)
            {
                BetterQuickSlotsPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyHudTransform(__instance);
                    plugin.ApplySmartHudIcons(__instance);
                }
            }
        }

        private static class SelectedQuickSlotOnDiscardPatch
        {
            public static void Postfix(object __instance)
            {
                BetterQuickSlotsPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseViewHudTransform(__instance);
                    plugin.ReleaseViewSmartIcons(__instance);
                }
            }
        }

        private static class HeroHudAfterFullyInitializedPatch
        {
            public static void Postfix(object __instance)
            {
                BetterQuickSlotsPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyArrowHudTransform(__instance);
                }
            }
        }

        private static class HeroHudUpdatePatch
        {
            public static void Postfix(object __instance)
            {
                BetterQuickSlotsPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyArrowHudTransform(__instance);
                }
            }
        }

        private static class HeroHudOnDiscardPatch
        {
            public static void Postfix(object __instance)
            {
                BetterQuickSlotsPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ReleaseViewArrowHudTransform(__instance);
                }
            }
        }
    }
}
