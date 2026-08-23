using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Crafting;
using Awaken.TG.Main.Crafting.Recipes;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.Factions.Crimes;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.General.StatTypes;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet;
using Awaken.TG.Main.Heroes.CharacterSheet.QuickUseWheels;
using Awaken.TG.Main.Heroes.CharacterSheet.WyrdArthur;
using Awaken.TG.Main.Heroes.Development.Talents;
using Awaken.TG.Main.Heroes.Fishing;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Actions;
using Awaken.TG.Main.Locations.Actions.Lockpicking;
using Awaken.TG.Main.Locations.Discovery;
using Awaken.TG.Main.Locations.Gems.GemManagement;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Saving;
using Awaken.TG.Main.Saving.Cloud.Services;
using Awaken.TG.Main.Saving.SaveSlots;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.UI.Menu;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Deeds of Avalon - Character Statistics")]
[assembly: AssemblyDescription("Save-bounded character statistics and menu presentation for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Deeds of Avalon")]
[assembly: AssemblyVersion("1.9.3.0")]
[assembly: AssemblyFileVersion("1.9.3.0")]
[assembly: AssemblyInformationalVersion("1.9.3")]

namespace DeedsOfAvalon
{
    public enum BloodMagicStatisticsMode
    {
        Simple,
        Detailed
    }

    public enum SoulAndServiceStatisticsMode
    {
        Simple,
        Detailed
    }

    public enum WeaponStatisticsMode
    {
        Detailed,
        Grouped
    }

    public static class StatisticsApi
    {
        public const int ApiVersion = 7;

        public static bool TryRecordCorpseDrain(string sourceId, string tier, float quality)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            return plugin != null && plugin.RecordCorpseDrain(sourceId, tier, quality);
        }

        public static bool TryRecordBloodMagicEssence(string sourceId, float bloodEssence)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            return plugin != null && plugin.RecordBloodMagicEssence(sourceId, bloodEssence);
        }

        public static bool TryRecordBloodMagicProgression(
            string sourceId,
            float bloodEssence,
            float bloodPower)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            return plugin != null
                && plugin.RecordBloodMagicProgression(sourceId, bloodEssence, bloodPower);
        }

        public static bool TryRecordSoulVigorStatistics(
            string sourceId,
            float soulVigor,
            float necromanticPower,
            int meager,
            int worthy,
            int potent,
            int prime)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            return plugin != null
                && plugin.RecordSoulVigorStatistics(
                    sourceId,
                    soulVigor,
                    necromanticPower,
                    meager,
                    worthy,
                    potent,
                    prime);
        }

        public static bool TryGetCorpseDrainCounts(
            string sourceId,
            out int meager,
            out int worthy,
            out int potent,
            out int prime)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            if (plugin == null)
            {
                meager = 0;
                worthy = 0;
                potent = 0;
                prime = 0;
                return false;
            }
            return plugin.TryGetCorpseDrainCounts(sourceId, out meager, out worthy, out potent, out prime);
        }

        public static bool TryGetCorpseDrainStatistics(
            string sourceId,
            out int meager,
            out int worthy,
            out int potent,
            out int prime,
            out float qualitySum)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            if (plugin == null)
            {
                meager = 0;
                worthy = 0;
                potent = 0;
                prime = 0;
                qualitySum = 0.0f;
                return false;
            }
            return plugin.TryGetCorpseDrainStatistics(
                sourceId,
                out meager,
                out worthy,
                out potent,
                out prime,
                out qualitySum);
        }

        public static bool TrySetCorpseDrainStatistics(
            string sourceId,
            int meager,
            int worthy,
            int potent,
            int prime,
            float qualitySum)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            return plugin != null
                && plugin.SetCorpseDrainStatistics(
                    sourceId,
                    meager,
                    worthy,
                    potent,
                    prime,
                    qualitySum);
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.glorious-ui", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DeedsOfAvalonPlugin : BaseUnityPlugin, IListenerOwner
    {
        public const string PluginGuid = "ks.tgfoa.deeds-of-avalon";
        public const string PluginName = "Deeds of Avalon";
        public const string PluginVersion = "1.9.3";
        private const string MemoryContext = "DeedsOfAvalon";
        private const string GftPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string GloriousUiPluginGuid = "ks.tgfoa.glorious-ui";
        private const string BloodMagicPluginGuid = "ks.tgfoa.blood-magic-expansion";
        private const string SoulAndServicePluginGuid = "ks.tgfoa.soul-and-service";
        private const int ConfigSchemaVersion = 13;
        private const float ReferenceScreenHeight = 1440.0f;
        private const float PanelHeaderHeight = 66.0f;
        private const float PanelRowHeight = 24.0f;
        private const float WhiteTextOutlineStrengthMultiplier = 1.1f;
        private const int GoldEarnedLowMinimum = 1000;
        private const int GoldEarnedMediumMinimum = 5000;
        private const int GoldEarnedHighMinimum = 15000;
        private const int GoldEarnedVeryHighMinimum = 40000;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[] ConfigRecoveryKeepCurrentDefaultRules =
            new[]
            {
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                    6,
                    "2. Quick Wheel",
                    "TextOutlineStrength",
                    "Strength now controls native signed-distance-field outline weight instead of copied geometry layers."),
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                    6,
                    "2. Quick Wheel",
                    "TextShadowStrength",
                    "Strength now controls native signed-distance-field underlay spread instead of copied geometry layers.")
            };
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions = new ConfigDefinition[0];
        private static readonly FieldInfo FishingRodFishField = AccessTools.Field(typeof(CharacterFishingRod), "_fish");

        internal static DeedsOfAvalonPlugin Instance;
        internal static ManualLogSource Log;

        private readonly List<IEventListener> _heroListeners = new List<IEventListener>();
        private readonly HashSet<int> _visibleTooltipIds = new HashSet<int>();
        private Harmony _harmony;
        private Hero _boundHero;
        private IEventListener _questListener;
        private IEventListener _locationListener;
        private IEventListener _summonKillListener;
        private IEventListener _quickWheelDiscardListener;
        private bool _globalEventsBound;
        private bool _wheelWasOpen;
        private VMenuUI _pauseMenuView;
        private float _nextPanelRefresh;
        private float _nextBindAttempt;
        private float _pendingLoadedExportAt = -1.0f;
        private StatisticsSnapshot _pendingSaveSnapshot;
        private PanelContent _pendingSavePanelContent;
        private PanelContent _loadingPanelContent;
        private string _loadingPanelSlotId;
        private bool _loadingGameplayDeserialized;
        private bool _loadingPanelWasVisible;
        private CharacterPointsSnapshot _characterPointsSnapshot;
        private bool _statisticsWereTracking;
        private int _crimeActionWrapperDepth;
        private int _crimeReportDepth;
        private bool _crimeReportedInCurrentAttempt;

        private MethodInfo _gftTrySetMethod;
        private MethodInfo _gftTryRegisterIconsMethod;
        private MethodInfo _gftSetTooltipActiveMethod;
        private MethodInfo _gftClearMethod;
        private bool _gftIconsRegistered;
        private bool _panelPresentationActive;
        private MethodInfo _bloodMagicIsDamageMethod;
        private MethodInfo _bloodMagicIsDisplayNameMethod;
        private MethodInfo _soulAndServiceIsNecroticDamageMethod;
        private FieldInfo _characterPointsCanvasGroupField;
        private FieldInfo _characterPointsWhispersVisibleField;
        private VCCharacterPointsAvailable _characterPointsView;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _trackStatistics;
        private ConfigEntry<bool> _exportOnSuccessfulSave;
        private ConfigEntry<bool> _showQuickWheelStatistics;
        private ConfigEntry<bool> _showPauseMenuStatistics;
        private ConfigEntry<bool> _showLoadingScreenStatistics;
        private ConfigEntry<bool> _hideItemTooltipText;
        private ConfigEntry<float> _panelOpacity;
        private ConfigEntry<float> _tooltipPanelOpacity;
        private ConfigEntry<float> _tooltipFadeSeconds;
        private ConfigEntry<float> _panelScale;
        private ConfigEntry<float> _panelColumnWidth;
        private ConfigEntry<float> _columnGap;
        private ConfigEntry<float> _panelBackgroundOpacity;
        private ConfigEntry<float> _panelBackgroundPadding;
        private ConfigEntry<float> _rightOffset;
        private ConfigEntry<float> _verticalOffset;
        private ConfigEntry<string> _headerColor;
        private ConfigEntry<string> _subheaderColor;
        private ConfigEntry<bool> _textShadowEnabled;
        private ConfigEntry<float> _textShadowOpacity;
        private ConfigEntry<float> _textShadowOffset;
        private ConfigEntry<float> _textShadowSoftness;
        private ConfigEntry<int> _textShadowStrength;
        private ConfigEntry<bool> _textOutlineEnabled;
        private ConfigEntry<string> _textOutlineColor;
        private ConfigEntry<float> _textOutlineOpacity;
        private ConfigEntry<float> _textOutlineWidth;
        private ConfigEntry<int> _textOutlineStrength;
        private ConfigEntry<int> _maximumDeedRows;
        private ConfigEntry<int> _maximumWeaponRows;
        private ConfigEntry<int> _maximumMagicRows;
        private ConfigEntry<WeaponStatisticsMode> _weaponStatisticsMode;
        private ConfigEntry<bool> _sortFoesByKillCount;
        private ConfigEntry<bool> _showCollapsedRows;
        private ConfigEntry<bool> _hidePointsAvailable;
        private ConfigEntry<bool> _showBloodMagicStatistics;
        private ConfigEntry<BloodMagicStatisticsMode> _bloodMagicStatisticsMode;
        private ConfigEntry<bool> _showSoulAndServiceStatistics;
        private ConfigEntry<SoulAndServiceStatisticsMode>
            _soulAndServiceStatisticsMode;
        private ConfigEntry<bool> _diagnostics;
        private Grailwright.Shared.ConfigRecoveryCustomizationProfile _pendingConfigRecoveryProfile;

        public bool CanReceiveEvents
        {
            get { return this != null && enabled; }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            try
            {
                BindConfig();
                _characterPointsCanvasGroupField = AccessTools.Field(typeof(VCCharacterPointsAvailable), "canvasGroup");
                _characterPointsWhispersVisibleField = AccessTools.Field(typeof(VCCharacterPointsAvailable), "_whispersVisible");
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(DeedsOfAvalonPlugin).Assembly);
                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
            }
            catch (Exception ex)
            {
                Logger.LogError(PluginName + " failed during startup: " + ex.GetBaseException().Message);
                Logger.LogError(ex);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, ex);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_enabled == null || !_enabled.Value)
            {
                _statisticsWereTracking = false;
                _visibleTooltipIds.Clear();
                RestoreCharacterPoints();
                ClearGftPanel();
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _nextBindAttempt)
            {
                _nextBindAttempt = now + 1.0f;
                TryBindEvents();
            }

            bool trackingNow = ShouldTrack();
            if (trackingNow && !_statisticsWereTracking)
            {
                ReconcileStatistics(Hero.Current);
            }
            _statisticsWereTracking = trackingNow;

            if (_pendingLoadedExportAt >= 0.0f && now >= _pendingLoadedExportAt)
            {
                _pendingLoadedExportAt = ExportCurrentSavedStatistics("load")
                    ? -1.0f
                    : now + 1.0f;
            }

            bool pauseMenuVisible = IsPauseMenuVisible();
            bool loadingScreenStatisticsEnabled = LoadingScreenStatisticsEnabled();
            if (!loadingScreenStatisticsEnabled
                && !string.IsNullOrWhiteSpace(_loadingPanelSlotId))
            {
                ClearLoadingPanelCache();
            }
            bool loadingScreenVisible = IsLoadingScreenVisible();
            if (loadingScreenVisible)
            {
                _loadingPanelWasVisible = !string.IsNullOrWhiteSpace(_loadingPanelSlotId);
            }
            else if (_loadingPanelWasVisible)
            {
                ClearLoadingPanelCache();
            }
            if (!_wheelWasOpen && !pauseMenuVisible && !loadingScreenVisible)
            {
                ClearGftPanel();
                return;
            }

            if (_wheelWasOpen)
            {
                ApplyPointsAvailableVisibility();
            }
            if (ShouldShowPanel(pauseMenuVisible, loadingScreenVisible) && now >= _nextPanelRefresh)
            {
                _nextPanelRefresh = now + 0.2f;
                PublishPanel();
            }
            else if (!ShouldShowPanel(pauseMenuVisible, loadingScreenVisible))
            {
                ClearGftPanel();
            }
        }

        private void OnDestroy()
        {
            _wheelWasOpen = false;
            _pauseMenuView = null;
            _visibleTooltipIds.Clear();
            RestoreCharacterPoints();
            ClearGftPanel();
            DisposeHeroListeners();
            RemoveListener(ref _questListener);
            RemoveListener(ref _locationListener);
            RemoveListener(ref _summonKillListener);
            RemoveListener(ref _quickWheelDiscardListener);
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

        private static ConfigDescription ConfigUi(
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
                new Grailwright.Shared.ConfigRecoveryUiMetadata
                {
                    DisplaySection = displaySection,
                    DisplayName = displayName,
                    SectionOrder = sectionOrder,
                    Order = order
                });
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();
            _enabled = Config.Bind("General", "Enabled", true, ConfigUi("Master switch for tracking, export, and menu presentation.", "General", "Enabled", 0, 0));
            Config.Bind("General", "ConfigSchemaVersion", ConfigSchemaVersion, new ConfigDescription("Configuration layout version. Do not edit manually.", null, new System.ComponentModel.BrowsableAttribute(false)));
            _trackStatistics = Config.Bind("General", "TrackStatistics", true, ConfigUi("Record character statistics in the active save game's GameplayMemory.", "General", "Track Statistics", 0, 10));
            _exportOnSuccessfulSave = Config.Bind("General", "ExportOnSuccessfulSave", true, ConfigUi("Write the readable character file only after a save succeeds, and refresh it after loading saved data.", "General", "Export After Successful Save", 0, 20));

            _showQuickWheelStatistics = Config.Bind("Quick Wheel", "ShowCharacterStatistics", true, ConfigUi("Show the two-column Deeds of Avalon panel while the quick wheel is open. Requires Grail Floating Text.", "General", "Show In Quick Wheel", 0, 30));
            _showPauseMenuStatistics = Config.Bind("Pause Menu", "ShowCharacterStatistics", true, ConfigUi("Show the same Deeds of Avalon panel on the root ESC system menu. Requires Grail Floating Text.", "General", "Show In Pause Menu", 0, 40));
            _showLoadingScreenStatistics = Config.Bind("Loading Screen", "ShowCharacterStatistics", false, ConfigUi("Show the same Deeds of Avalon panel during gameplay loading screens when character statistics are available. Enabling this also activates per-save loading-screen snapshots. Requires Grail Floating Text.", "General", "Show On Loading Screens", 0, 50));
            _hideItemTooltipText = Config.Bind("Quick Wheel", "HideItemTooltipText", false, ConfigUi("Hide the normal weapon and spell tooltip on the quick wheel. Disabled by default.", "Tooltip Behavior", "Hide Item Tooltip Text", 10, 0));
            _tooltipPanelOpacity = Config.Bind("Quick Wheel", "TooltipPanelOpacity", 0.0f, ConfigUi("Multiplier applied to the statistics panel while a weapon or spell tooltip is visible.", "Tooltip Behavior", "Panel Opacity With Tooltip", 10, 10, new AcceptableValueRange<float>(0.0f, 1.0f)));
            _tooltipFadeSeconds = Config.Bind("Quick Wheel", "TooltipFadeSeconds", 0.15f, ConfigUi("Seconds used to fade the statistics panel when tooltips open or close.", "Tooltip Behavior", "Tooltip Fade Seconds", 10, 20, new AcceptableValueRange<float>(0.0f, 2.0f)));
            _panelOpacity = Config.Bind("Quick Wheel", "PanelOpacity", 1.0f, ConfigUi("Normal statistics-panel opacity.", "Panel Layout", "Panel Opacity", 20, 0, new AcceptableValueRange<float>(0.0f, 1.0f)));
            _panelScale = Config.Bind("Quick Wheel", "PanelScale", 1.5f, ConfigUi("Statistics panel scale at 1440p. Shorter displays automatically reduce it to preserve the same vertical proportions.", "Panel Layout", "Panel Scale", 20, 10, new AcceptableValueRange<float>(0.5f, 2.0f)));
            _panelColumnWidth = Config.Bind("Quick Wheel", "PanelColumnWidth", 190.0f, ConfigUi("Width of each statistics column in 1440p reference pixels.", "Panel Layout", "Column Width", 20, 20, new AcceptableValueRange<float>(160.0f, 400.0f)));
            _columnGap = Config.Bind("Quick Wheel", "ColumnGap", 30.0f, ConfigUi("Space between the two statistics columns in 1440p reference pixels.", "Panel Layout", "Column Gap", 20, 30, new AcceptableValueRange<float>(0.0f, 200.0f)));
            _rightOffset = Config.Bind("Quick Wheel", "RightOffset", 28.0f, ConfigUi("Distance in pixels from the right edge.", "Panel Layout", "Right Offset", 20, 40, new AcceptableValueRange<float>(0.0f, 800.0f)));
            _verticalOffset = Config.Bind("Quick Wheel", "VerticalOffset", 0.0f, ConfigUi("Vertical adjustment from automatic centering in 1440p reference pixels. Positive values move the panel down.", "Panel Layout", "Vertical Offset", 20, 50, new AcceptableValueRange<float>(-600.0f, 600.0f)));
            _panelBackgroundOpacity = Config.Bind("Quick Wheel", "PanelBackgroundOpacity", 0.95f, ConfigUi("Opacity of the two charcoal column backplates behind the statistics panel. Set to zero to disable them.", "Panel Background", "Background Opacity", 30, 0, new AcceptableValueRange<float>(0.0f, 1.0f)));
            _panelBackgroundPadding = Config.Bind("Quick Wheel", "PanelBackgroundPadding", 16.0f, ConfigUi("Padding around each charcoal column backplate in 1440p reference pixels.", "Panel Background", "Background Padding", 30, 10, new AcceptableValueRange<float>(0.0f, 32.0f)));
            _headerColor = Config.Bind("Quick Wheel", "HeaderColor", "#D88B38", ConfigUi("Color shared by the character-name and Foes Defeated headers. Enter a Grail Floating Text color-pool name or an HTML hex color such as #RRGGBB or #RRGGBBAA.", "Panel Colors", "Header Color", 40, 0));
            _subheaderColor = Config.Bind("Quick Wheel", "SubheaderColor", "White", ConfigUi("Color shared by the Level/XP and Total subheaders. Enter a Grail Floating Text color-pool name or an HTML hex color such as #RRGGBB or #RRGGBBAA.", "Panel Colors", "Subheader Color", 40, 10));
            _textOutlineEnabled = Config.Bind("Quick Wheel", "TextOutlineEnabled", true, ConfigUi("Draw a native signed-distance-field outline around all statistics-panel text.", "Text Outline", "Enabled", 50, 0));
            _textOutlineColor = Config.Bind("Quick Wheel", "TextOutlineColor", "#000000", ConfigUi("Statistics-panel text-outline color. Enter an HTML hex color such as #RRGGBB or #RRGGBBAA.", "Text Outline", "Color", 50, 10));
            _textOutlineOpacity = Config.Bind("Quick Wheel", "TextOutlineOpacity", 0.5f, ConfigUi("Opacity of the statistics-panel text outline.", "Text Outline", "Opacity", 50, 20, new AcceptableValueRange<float>(0.0f, 1.0f)));
            _textOutlineWidth = Config.Bind("Quick Wheel", "TextOutlineWidth", 5.0f, ConfigUi("Approximate statistics-panel outline reach. Five is the readability baseline; sixteen is the maximum native SDF expansion.", "Text Outline", "Width", 50, 30, new AcceptableValueRange<float>(0.0f, 16.0f)));
            _textOutlineStrength = Config.Bind("Quick Wheel", "TextOutlineStrength", 2, ConfigUi("Native outline weight. Two is the readability baseline; eight is the maximum weight without copied text geometry.", "Text Outline", "Strength", 50, 40, new AcceptableValueRange<int>(1, 8)));
            _textShadowEnabled = Config.Bind("Quick Wheel", "TextShadowEnabled", true, ConfigUi("Draw a native soft black SDF backing behind all statistics-panel text.", "Text Backing", "Enabled", 60, 0));
            _textShadowOpacity = Config.Bind("Quick Wheel", "TextShadowOpacity", 1.0f, ConfigUi("Opacity of the soft black text backing.", "Text Backing", "Opacity", 60, 10, new AcceptableValueRange<float>(0.0f, 1.0f)));
            _textShadowOffset = Config.Bind("Quick Wheel", "TextShadowOffset", 4.0f, ConfigUi("Diagonal offset of the black backing. Zero centers it behind the text; higher values turn it into a conventional drop shadow.", "Text Backing", "Offset", 60, 20, new AcceptableValueRange<float>(0.0f, 16.0f)));
            _textShadowSoftness = Config.Bind("Quick Wheel", "TextShadowSoftness", 0.5f, ConfigUi("Blur-like softness of the black text backing.", "Text Backing", "Softness", 60, 30, new AcceptableValueRange<float>(0.0f, 1.0f)));
            _textShadowStrength = Config.Bind("Quick Wheel", "TextShadowStrength", 8, ConfigUi("Native backing spread. Eight is the broadest backing without copied text geometry.", "Text Backing", "Spread", 60, 40, new AcceptableValueRange<int>(1, 8)));
            _maximumDeedRows = Config.Bind("Quick Wheel", "MaximumDeedRows", 32, ConfigUi("Maximum non-XP rows in the left column.", "Panel Content", "Maximum Deed Rows", 70, 0, new AcceptableValueRange<int>(1, 32)));
            _maximumWeaponRows = Config.Bind("Quick Wheel", "MaximumWeaponRows", 28, ConfigUi("Maximum weapon-category rows in Foes Defeated.", "Panel Content", "Maximum Weapon Rows", 70, 10, new AcceptableValueRange<int>(1, 28)));
            _maximumMagicRows = Config.Bind("Quick Wheel", "MaximumMagicRows", 20, ConfigUi("Maximum magic-category rows in Foes Defeated.", "Panel Content", "Maximum Magic Rows", 70, 20, new AcceptableValueRange<int>(1, 20)));
            _weaponStatisticsMode = Config.Bind("Quick Wheel", "WeaponStatisticsMode", WeaponStatisticsMode.Detailed, ConfigUi("Choose Detailed for individual weapon types or Grouped for combined One-Handed, Two-Handed, and Bows rows. Tracking remains detailed in either mode.", "Panel Content", "Weapon Statistics Mode", 70, 30));
            _sortFoesByKillCount = Config.Bind("Quick Wheel", "SortFoesByKillCount", true, ConfigUi("Order the visible Foes Defeated rows from highest to lowest displayed kill count. Disable to retain the authored weapon and magic grouping order.", "Panel Content", "Sort Foes By Kill Count", 70, 40));
            _showCollapsedRows = Config.Bind("Quick Wheel", "ShowCollapsedOtherRows", true, ConfigUi("Combine positive categories beyond a column limit into an Other row.", "Panel Content", "Show Collapsed Other Rows", 70, 50));
            _hidePointsAvailable = Config.Bind("Quick Wheel", "HidePointsAvailable", true, ConfigUi("Hide the top-right Points available widget only while the quick wheel is open. Defers to Glorious UI when Glorious UI owns this behavior.", "Panel Content", "Hide Points Available", 70, 60));
            _showBloodMagicStatistics = Config.Bind("Integrations", "ShowBloodMagicStatistics", true, ConfigUi("Show Blood Essence and Blood Power above Corpses Drained in the Grail Floating Text statistics panel.", "Integrations", "Show Blood Magic Statistics", 80, 0));
            _bloodMagicStatisticsMode = Config.Bind("Integrations", "BloodMagicStatisticsMode", BloodMagicStatisticsMode.Detailed, ConfigUi("Choose Simple for Blood Essence and Blood Power plus the total Corpses Drained row, or Detailed for the progression row plus Meager, Worthy, Potent, and Prime rows. The total and tiers are never shown together.", "Integrations", "Blood Magic Statistics Mode", 80, 10));
            _showSoulAndServiceStatistics = Config.Bind("Integrations", "ShowSoulAndServiceStatistics", true, ConfigUi("Show Soul Vigor and Necromantic Power plus corpse-harvest statistics in the Grail Floating Text statistics panel.", "Integrations", "Show Soul and Service Statistics", 80, 20));
            _soulAndServiceStatisticsMode = Config.Bind("Integrations", "SoulAndServiceStatisticsMode", SoulAndServiceStatisticsMode.Detailed, ConfigUi("Choose Simple for Soul Vigor and Necromantic Power plus the total Corpses Harvested row, or Detailed for the progression row plus Meager, Worthy, Potent, and Prime harvest rows. The total and tiers are never shown together.", "Integrations", "Soul and Service Statistics Mode", 80, 30));
            _diagnostics = Config.Bind("Diagnostics", "Diagnostics", false, ConfigUi("Log event binding, panel integration, save export, and compatibility details.", "Diagnostics", "Diagnostics", 90, 0));
            RestorePreservedConfigValues();
            Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(Config, Logger, PluginName, ConfigSchemaVersion, ConfigRecoveryBaselineSchema, ConfigRecoveryKeepCurrentDefaultRules, ConfigRecoveryPermanentExclusions);
            Config.Save();
        }

        private void ResetConfigIfSchemaChanged()
        {
            string path = Config.ConfigFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }
            int stored = 0;
            string section = string.Empty;
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2);
                    continue;
                }
                const string prefix = "ConfigSchemaVersion =";
                if (string.Equals(section, "General", StringComparison.Ordinal) && line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring(prefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out stored);
                    break;
                }
            }
            if (stored == ConfigSchemaVersion)
            {
                return;
            }

            _pendingConfigRecoveryProfile = Grailwright.Shared.ConfigPreviousSettingsRecovery
                .ReadCustomizationProfile(
                    path,
                    stored,
                    ConfigSchemaVersion,
                    ConfigRecoveryKeepCurrentDefaultRules,
                    ConfigRecoveryPermanentExclusions);

            string backup = path + ".pre-schema-" + stored.ToString(CultureInfo.InvariantCulture) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".bak";
            try
            {
                File.Copy(path, backup, false);
                File.WriteAllText(path, string.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo("Configuration schema changed from " + stored + " to " + ConfigSchemaVersion + ". Generated defaults and backed up " + backup + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(PluginGuid, PluginName, stored, ConfigSchemaVersion);
            }
            catch
            {
                _pendingConfigRecoveryProfile = null;
                if (File.Exists(backup))
                {
                    File.Copy(backup, path, true);
                    Config.Clear();
                    Config.Reload();
                }
                throw;
            }
        }

        private void RestorePreservedConfigValues()
        {
            if (_pendingConfigRecoveryProfile == null)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreserved(_enabled, ref restoredCount, ref clampedCount);
            RestorePreserved(_trackStatistics, ref restoredCount, ref clampedCount);
            RestorePreserved(_exportOnSuccessfulSave, ref restoredCount, ref clampedCount);
            RestorePreserved(_showQuickWheelStatistics, ref restoredCount, ref clampedCount);
            RestorePreserved(_showPauseMenuStatistics, ref restoredCount, ref clampedCount);
            RestorePreserved(_showLoadingScreenStatistics, ref restoredCount, ref clampedCount);
            RestorePreserved(_hideItemTooltipText, ref restoredCount, ref clampedCount);
            RestorePreserved(_panelOpacity, ref restoredCount, ref clampedCount);
            RestorePreserved(_tooltipPanelOpacity, ref restoredCount, ref clampedCount);
            RestorePreserved(_tooltipFadeSeconds, ref restoredCount, ref clampedCount);
            RestorePreserved(_panelScale, ref restoredCount, ref clampedCount);
            RestorePreserved(_panelColumnWidth, ref restoredCount, ref clampedCount);
            RestorePreserved(_columnGap, ref restoredCount, ref clampedCount);
            RestorePreserved(_panelBackgroundOpacity, ref restoredCount, ref clampedCount);
            RestorePreserved(_panelBackgroundPadding, ref restoredCount, ref clampedCount);
            RestorePreserved(_rightOffset, ref restoredCount, ref clampedCount);
            RestorePreserved(_verticalOffset, ref restoredCount, ref clampedCount);
            RestorePreserved(_headerColor, ref restoredCount, ref clampedCount);
            RestorePreserved(_subheaderColor, ref restoredCount, ref clampedCount);
            RestorePreserved(_textOutlineEnabled, ref restoredCount, ref clampedCount);
            RestorePreserved(_textOutlineColor, ref restoredCount, ref clampedCount);
            RestorePreserved(_textOutlineOpacity, ref restoredCount, ref clampedCount);
            RestorePreserved(_textOutlineWidth, ref restoredCount, ref clampedCount);
            RestorePreserved(_textOutlineStrength, ref restoredCount, ref clampedCount);
            RestorePreserved(_textShadowEnabled, ref restoredCount, ref clampedCount);
            RestorePreserved(_textShadowOpacity, ref restoredCount, ref clampedCount);
            RestorePreserved(_textShadowOffset, ref restoredCount, ref clampedCount);
            RestorePreserved(_textShadowSoftness, ref restoredCount, ref clampedCount);
            RestorePreserved(_textShadowStrength, ref restoredCount, ref clampedCount);
            RestorePreserved(_maximumDeedRows, ref restoredCount, ref clampedCount);
            RestorePreserved(_maximumWeaponRows, ref restoredCount, ref clampedCount);
            RestorePreserved(_maximumMagicRows, ref restoredCount, ref clampedCount);
            RestorePreserved(_weaponStatisticsMode, ref restoredCount, ref clampedCount);
            RestorePreserved(_sortFoesByKillCount, ref restoredCount, ref clampedCount);
            RestorePreserved(_showCollapsedRows, ref restoredCount, ref clampedCount);
            RestorePreserved(_hidePointsAvailable, ref restoredCount, ref clampedCount);
            RestorePreserved(_showBloodMagicStatistics, ref restoredCount, ref clampedCount);
            RestorePreserved(_bloodMagicStatisticsMode, ref restoredCount, ref clampedCount);
            RestorePreserved(_showSoulAndServiceStatistics, ref restoredCount, ref clampedCount);
            RestorePreserved(_soulAndServiceStatisticsMode, ref restoredCount, ref clampedCount);
            RestorePreserved(_diagnostics, ref restoredCount, ref clampedCount);

            Logger.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " customized setting(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            _pendingConfigRecoveryProfile = null;
        }

        private void RestorePreserved<T>(
            ConfigEntry<T> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                _pendingConfigRecoveryProfile;
            if (entry == null || profile == null)
            {
                return;
            }

            T preservedValue;
            if (!profile.TryGetCustomizedValue(
                entry.Definition.Section,
                entry.Definition.Key,
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
                return;
            }

            restoredCount++;
            if (clamped)
            {
                clampedCount++;
            }
        }

        private void TryBindEvents()
        {
            if (!_globalEventsBound)
            {
                try
                {
                    _questListener = World.EventSystem.ListenTo("*", QuestUtils.Events.QuestCompleted, this, OnQuestCompleted);
                    _locationListener = World.EventSystem.ListenTo("*", LocationDiscovery.Events.LocationDiscovered, this, OnLocationDiscovered);
                    _summonKillListener = World.EventSystem.ListenTo("*", HealthElement.Events.OnHeroSummonKill, this, OnHeroSummonKill);
                    _quickWheelDiscardListener = World.EventSystem.ListenTo<IModel, Model>(
                        "*",
                        World.Events.ModelDiscarded<QuickUseWheelUI>(),
                        this,
                        OnQuickWheelDiscarded);
                    _globalEventsBound = true;
                }
                catch (Exception ex)
                {
                    RemoveListener(ref _questListener);
                    RemoveListener(ref _locationListener);
                    RemoveListener(ref _summonKillListener);
                    RemoveListener(ref _quickWheelDiscardListener);
                    _globalEventsBound = false;
                    LogDiagnostic("Global event binding will retry: " + ex.GetBaseException().Message);
                    return;
                }
            }

            try
            {
                Hero hero = Hero.Current;
                if (hero == null || hero.HasBeenDiscarded || ReferenceEquals(hero, _boundHero))
                {
                    return;
                }

                DisposeHeroListeners();
                _boundHero = hero;
                _heroListeners.Add(ModelExtensions.ListenTo(hero, HealthElement.Events.OnKill, OnHeroKill, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, Hero.Events.Died, OnHeroDied, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, CrimeUtils.Events.CrimeCommitted, OnCrimeCommitted, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, CrimeUtils.Events.BountyClearedFor, OnBountyCleared, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, HeroWyrdNight.Events.WyrdNightChanged, OnWyrdNightChanged, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, Hero.Events.AfterHeroRested, OnHeroRested, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, Hero.Events.HeroUsedItem, OnHeroUsedItem, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, Stat.Events.StatChangedBy(CurrencyStatType.Wealth), OnWealthChanged, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, Awaken.TG.Main.Crafting.Crafting.Events.Created, OnItemCrafted, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, CommitCrime.Events.PickpocketSuccess, OnItemPickpocketed, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, LockpickingInteraction.Events.HeroLockUnlocked, OnLockPicked, this));
                HeroRecipes recipes = hero.Element<HeroRecipes>();
                _heroListeners.Add(ModelExtensions.ListenTo(recipes, HeroRecipes.Events.RecipeLearned, OnRecipeLearned, this));
                ReconcileStatistics(hero);
                LogDiagnostic("Bound statistics events for " + hero.Name + ".");
            }
            catch (Exception ex)
            {
                DisposeHeroListeners();
                LogDiagnostic("Hero event binding will retry: " + ex.GetBaseException().Message);
            }
        }

        private void DisposeHeroListeners()
        {
            for (int i = 0; i < _heroListeners.Count; i++)
            {
                IEventListener listener = _heroListeners[i];
                if (listener != null)
                {
                    World.EventSystem.RemoveListener(listener);
                }
            }
            _heroListeners.Clear();
            _boundHero = null;
        }

        private static void RemoveListener(ref IEventListener listener)
        {
            if (listener != null)
            {
                World.EventSystem.RemoveListener(listener);
                listener = null;
            }
        }

        private void OnHeroKill(DamageOutcome outcome)
        {
            NpcElement target = outcome.TargetPure as NpcElement;
            if (!ShouldTrack() || target == null || target.NpcType == NpcType.HeroSummon)
            {
                return;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return;
            }
            string category;
            try
            {
                category = ClassifyKill(outcome.Damage, facts);
            }
            catch (Exception ex)
            {
                category = "foes.weapon.other";
                LogDiagnostic("Fatal-hit classification failed and used Other: " + ex.GetBaseException().Message);
            }
            Increment(facts, category);
            Increment(facts, "foes.total");
            LogFallbackKill(outcome.Damage, category);
        }

        private void OnHeroSummonKill(DamageOutcome outcome)
        {
            NpcElement target = outcome.TargetPure as NpcElement;
            if (!ShouldTrack() || target == null || target.NpcType == NpcType.HeroSummon)
            {
                return;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return;
            }
            Increment(facts, "foes.summon");
            Increment(facts, "foes.total");
        }

        private void OnHeroDied(DamageOutcome outcome)
        {
            if (ShouldTrack())
            {
                Increment(Facts(), "deeds.deaths");
            }
        }

        private void OnHeroUsedItem(Item item)
        {
            if (!ShouldTrack() || item == null || item.Template == null)
            {
                return;
            }
            if (item.Template.IsPotion)
            {
                Increment(Facts(), "deeds.potions_used");
            }
            else if (item.IsEdible)
            {
                Increment(Facts(), "deeds.food_eaten");
            }
        }

        private void OnWealthChanged(Stat.StatChange change)
        {
            if (!ShouldTrack() || change.value <= 0.0f)
            {
                return;
            }

            int earned = Math.Max(0, Mathf.RoundToInt(change.value));
            if (earned <= 0)
            {
                return;
            }

            ContextualFacts facts = Facts();
            if (facts != null)
            {
                AddCounter(facts, "deeds.total_gold_earned", earned);
            }
        }

        private void OnItemCrafted(CreatedEvent created)
        {
            if (ShouldTrack())
            {
                AddCounter(
                    Facts(),
                    "deeds.items_crafted",
                    created.Item == null ? 1 : Math.Max(1, created.Item.Quantity));
            }
        }

        private void OnItemPickpocketed(Item item)
        {
            if (ShouldTrack())
            {
                AddCounter(
                    Facts(),
                    "deeds.items_pickpocketed",
                    item == null ? 1 : Math.Max(1, item.Quantity));
            }
        }

        private void OnLockPicked(LockAction action)
        {
            if (ShouldTrack())
            {
                Increment(Facts(), "deeds.locks_picked");
            }
        }

        private void OnRecipeLearned(IRecipe recipe)
        {
            if (ShouldTrack())
            {
                Increment(Facts(), "deeds.recipes_learned");
            }
        }

        internal void RecordFishCaught(CharacterFishingRod fishingRod)
        {
            if (!ShouldTrack() || fishingRod == null)
            {
                return;
            }
            if (FishingRodFishField == null)
            {
                LogDiagnostic("Fish Caught tracking could not resolve CharacterFishingRod._fish.");
                return;
            }
            object value = FishingRodFishField.GetValue(fishingRod);
            if (value is FishData.FightingFish && ((FishData.FightingFish)value).isFish)
            {
                Increment(Facts(), "deeds.fishes_caught");
            }
        }

        private string ClassifyKill(Damage damage, ContextualFacts facts)
        {
            Item item = damage == null ? null : damage.Item;
            if (item != null)
            {
                if (item.IsShield) return "foes.weapon.shield";
                if (item.IsFists || item.IsDefaultFists) return "foes.weapon.unarmed";
                if (item.IsThrowable) return "foes.weapon.throwable";
                if (item.IsRanged)
                {
                    if (item.IsShortBow) return "foes.weapon.short_bow";
                    if (item.IsMediumBow) return "foes.weapon.long_bow";
                    if (item.IsHeavyBow) return "foes.weapon.heavy_bow";
                    return "foes.weapon.ranged";
                }
                if (item.IsMagic || item.IsRod)
                {
                    return ClassifyMagic(damage, facts);
                }
                if (item.IsOneHanded)
                {
                    if (item.IsDagger) return "foes.weapon.one_handed_dagger";
                    if (item.IsSword) return "foes.weapon.one_handed_sword";
                    if (item.IsAxe) return "foes.weapon.one_handed_axe";
                    if (item.IsBlunt) return "foes.weapon.one_handed_blunt";
                    if (item.IsPolearm) return "foes.weapon.one_handed_polearm";
                    if (item.IsSickle) return "foes.weapon.one_handed_axe";
                    return "foes.weapon.one_handed_other";
                }
                if (item.IsTwoHanded)
                {
                    if (item.IsSword) return "foes.weapon.two_handed_sword";
                    if (item.IsAxe) return "foes.weapon.two_handed_axe";
                    if (item.IsPolearm) return "foes.weapon.two_handed_polearm";
                    if (item.IsBlunt) return "foes.weapon.two_handed_blunt";
                    return "foes.weapon.two_handed_other";
                }
                if (!item.IsMagic && !item.IsRod && damage.Type != DamageType.MagicalHitSource && damage.Type != DamageType.Status)
                {
                    return "foes.weapon.other";
                }
            }

            if (damage != null && (damage.Type == DamageType.MagicalHitSource || damage.Type == DamageType.Status || damage.Skill != null || (item != null && (item.IsMagic || item.IsRod))))
            {
                return ClassifyMagic(damage, facts);
            }
            return "foes.weapon.other";
        }

        private void LogFallbackKill(Damage damage, string category)
        {
            if (!DiagnosticPreviewEnabled()
                || (!string.Equals(category, "foes.weapon.one_handed_other", StringComparison.Ordinal)
                    && !string.Equals(category, "foes.weapon.two_handed_other", StringComparison.Ordinal)
                    && !string.Equals(category, "foes.weapon.ranged", StringComparison.Ordinal)
                    && !string.Equals(category, "foes.weapon.other", StringComparison.Ordinal)
                    && !string.Equals(category, "foes.magic.damage.other", StringComparison.Ordinal)))
            {
                return;
            }

            if (damage == null)
            {
                LogDiagnostic("Fallback kill: category=" + category + "; damage=null.");
                return;
            }

            try
            {
                Item item = damage.Item;
                StringBuilder details = new StringBuilder(320);
                details.Append("Fallback kill: category=").Append(category)
                    .Append("; type=").Append(damage.Type)
                    .Append("; status=").Append(damage.StatusDamageType)
                    .Append("; primary=").Append(damage.IsPrimary)
                    .Append("; item=").Append(item == null ? "none" : CleanDisplayName(item.DisplayName))
                    .Append("; itemFlags=");
                if (item == null)
                {
                    details.Append("none");
                }
                else
                {
                    details.Append("1H:").Append(item.IsOneHanded)
                        .Append(",2H:").Append(item.IsTwoHanded)
                        .Append(",dagger:").Append(item.IsDagger)
                        .Append(",sword:").Append(item.IsSword)
                        .Append(",axe:").Append(item.IsAxe)
                        .Append(",blunt:").Append(item.IsBlunt)
                        .Append(",polearm:").Append(item.IsPolearm)
                        .Append(",sickle:").Append(item.IsSickle)
                        .Append(",ranged:").Append(item.IsRanged)
                        .Append(",magic:").Append(item.IsMagic)
                        .Append(",rod:").Append(item.IsRod);
                }
                details.Append("; subtypes=");
                if (damage.SubTypes.Count == 0)
                {
                    details.Append("none");
                }
                else
                {
                    for (int i = 0; i < damage.SubTypes.Count; i++)
                    {
                        if (i > 0) details.Append(',');
                        DamageTypeDataPart part = damage.SubTypes[i];
                        details.Append(part.SubType).Append(':')
                            .Append((part.DamageTaken > 0.0f ? part.DamageTaken : part.Percentage).ToString("0.###", CultureInfo.InvariantCulture));
                    }
                }
                details.Append("; projectile=").Append(damage.Projectile == null ? "none" : damage.Projectile.GetType().FullName)
                    .Append("; skill=").Append(damage.Skill == null ? "none" : damage.Skill.GetType().FullName);
                LogDiagnostic(details.ToString());
            }
            catch (Exception ex)
            {
                LogDiagnostic("Fallback kill: category=" + category + "; diagnostic detail failed: " + ex.GetBaseException().Message);
            }
        }

        private string ClassifyMagic(Damage damage, ContextualFacts facts)
        {
            if (IsBloodMagicDamage(damage))
            {
                return "foes.magic.damage.blood_magic";
            }
            if (IsNecroticDamage(damage))
            {
                return "foes.magic.damage.necrotic";
            }
            string spellName = ResolveSpellName(damage);
            if (!string.IsNullOrEmpty(spellName))
            {
                string spellKey = ResolveSpellKey(facts, spellName);
                string magicType = ResolveMagicType(damage);
                facts.Set("display.magic." + spellKey, spellName);
                Increment(facts, "meta.magic.type." + spellKey + "." + magicType);
                return "foes.magic.spell." + spellKey;
            }
            return "foes.magic.damage." + ResolveMagicType(damage);
        }

        private static string ResolveSpellKey(ContextualFacts facts, string spellName)
        {
            string baseKey = SafeKey(spellName);
            string existing = facts.Get("display.magic." + baseKey, string.Empty);
            if (string.IsNullOrEmpty(existing)
                || string.Equals(existing, spellName, StringComparison.Ordinal))
            {
                return baseKey;
            }
            return baseKey + "_" + StableKeySuffix(spellName);
        }

        private static string StableKeySuffix(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string normalized = value ?? string.Empty;
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= char.ToLowerInvariant(normalized[i]);
                    hash *= 16777619u;
                }
                return hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        private static string ResolveSpellName(Damage damage)
        {
            if (damage.Item != null && (damage.Item.IsMagic || damage.Item.IsRod))
            {
                return CleanDisplayName(damage.Item.DisplayName);
            }
            object skill = damage.Skill;
            if (skill == null) return string.Empty;
            string[] names = { "DisplayName", "Name", "DebugName" };
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo property = skill.GetType().GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object value = property == null ? null : property.GetValue(skill, null);
                string display = CleanDisplayName(value == null ? string.Empty : value.ToString());
                if (!string.IsNullOrEmpty(display) && !string.Equals(display, "Skill", StringComparison.OrdinalIgnoreCase)) return display;
            }
            return string.Empty;
        }

        private static string CleanDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            return cleaned.Length <= 48 ? cleaned : cleaned.Substring(0, 48).Trim();
        }

        private static string ResolveMagicType(Damage damage)
        {
            DamageSubType best = DamageSubType.Default;
            float bestWeight = -1.0f;
            for (int i = 0; i < damage.SubTypes.Count; i++)
            {
                DamageTypeDataPart part = damage.SubTypes[i];
                float weight = part.DamageTaken > 0.0f ? part.DamageTaken : part.Percentage;
                if (weight > bestWeight && part.SubType != DamageSubType.Default)
                {
                    best = part.SubType;
                    bestWeight = weight;
                }
            }
            switch (best)
            {
                case DamageSubType.Fire: return "fire";
                case DamageSubType.Cold: return "cold";
                case DamageSubType.Poison: return "poison";
                case DamageSubType.Electric: return "electric";
                case DamageSubType.Wyrdness: return "wyrdness";
                case DamageSubType.Pure: return "pure";
                case DamageSubType.Wet: return "wet";
                default: return "other";
            }
        }

        private void OnQuestCompleted(QuestUtils.QuestStateChange change)
        {
            if (ShouldTrack()) Increment(Facts(), "deeds.quests_completed");
        }

        private void OnLocationDiscovered(Location location)
        {
            if (ShouldTrack()) Increment(Facts(), "deeds.locations_discovered");
        }

        private void OnHeroRested(int minutes)
        {
            if (!ShouldTrack()) return;
            AddCounter(Facts(), "deeds.minutes_rested", Math.Max(0, minutes));
        }

        private void OnWyrdNightChanged(bool isNight)
        {
            if (!ShouldTrack()) return;
            ContextualFacts facts = Facts();
            if (facts == null) return;
            bool wasActive = facts.Get("state.wyrdnight_active", false);
            if (!isNight && wasActive)
            {
                Increment(facts, "deeds.wyrdnights_survived");
            }
            facts.Set("state.wyrdnight_active", isNight);
        }

        private void OnCrimeCommitted(CrimeChangeData data)
        {
            if (!ShouldTrack()) return;
            if (_crimeActionWrapperDepth == 0
                && (_crimeReportDepth == 0 || !_crimeReportedInCurrentAttempt))
            {
                Increment(Facts(), "deeds.crimes_committed");
            }
            if (_crimeReportDepth > 0)
            {
                _crimeReportedInCurrentAttempt = true;
            }
            ReconcileActiveBounty(Facts());
        }

        private void OnBountyCleared(CrimeOwnerTemplate faction)
        {
            if (ShouldTrack()) ReconcileActiveBounty(Facts());
        }

        internal void BeginCrimeActionWrapper()
        {
            _crimeActionWrapperDepth++;
        }

        internal void EndCrimeActionWrapper(bool committed)
        {
            if (_crimeActionWrapperDepth <= 0)
            {
                _crimeActionWrapperDepth = 0;
                return;
            }
            _crimeActionWrapperDepth--;
            if (committed && _crimeActionWrapperDepth == 0 && ShouldTrack())
            {
                Increment(Facts(), "deeds.crimes_committed");
            }
        }

        internal void BeginCrimeReportAttempt()
        {
            if (_crimeReportDepth++ == 0)
            {
                _crimeReportedInCurrentAttempt = false;
            }
        }

        internal void EndCrimeReportAttempt()
        {
            if (_crimeReportDepth > 0)
            {
                _crimeReportDepth--;
            }
            if (_crimeReportDepth == 0)
            {
                _crimeReportedInCurrentAttempt = false;
            }
        }

        internal bool RecordCorpseDrain(string sourceId, string tier, float quality)
        {
            if (!ShouldTrack() || !string.Equals(sourceId, BloodMagicPluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string normalized = NormalizeCorpseTier(tier, quality);
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return false;
            }
            Increment(facts, "blood.corpses_drained." + normalized);
            ReconcileCorpseDrainTotal(facts);
            facts.Set(
                "blood.corpses_drained.quality_sum",
                SaturatingAdd(
                    Math.Max(0.0f, facts.Get("blood.corpses_drained.quality_sum", 0.0f)),
                    Mathf.Clamp01(quality)));
            return true;
        }

        internal bool SetCorpseDrainStatistics(
            string sourceId,
            int meager,
            int worthy,
            int potent,
            int prime,
            float qualitySum)
        {
            if (!ShouldTrack() || !string.Equals(sourceId, BloodMagicPluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return false;
            }
            facts.Set("blood.corpses_drained.meager", Math.Max(0, meager));
            facts.Set("blood.corpses_drained.worthy", Math.Max(0, worthy));
            facts.Set("blood.corpses_drained.potent", Math.Max(0, potent));
            facts.Set("blood.corpses_drained.prime", Math.Max(0, prime));
            facts.Set("blood.corpses_drained.quality_sum", Math.Max(0.0f, qualitySum));
            ReconcileCorpseDrainTotal(facts);
            return true;
        }

        internal bool RecordBloodMagicEssence(string sourceId, float bloodEssence)
        {
            if (!ShouldTrack() || !string.Equals(sourceId, BloodMagicPluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return false;
            }
            facts.Set("blood.essence", Math.Max(0, Mathf.RoundToInt(bloodEssence)));
            return true;
        }

        internal bool RecordBloodMagicProgression(
            string sourceId,
            float bloodEssence,
            float bloodPower)
        {
            if (!ShouldTrack() || !string.Equals(sourceId, BloodMagicPluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return false;
            }
            facts.Set("blood.essence", Math.Max(0, Mathf.RoundToInt(bloodEssence)));
            facts.Set("blood.power", Mathf.Clamp(bloodPower, 0.0f, 200.0f));
            return true;
        }

        internal bool RecordSoulVigorStatistics(
            string sourceId,
            float soulVigor,
            float necromanticPower,
            int meager,
            int worthy,
            int potent,
            int prime)
        {
            if (!ShouldTrack()
                || !string.Equals(
                    sourceId,
                    SoulAndServicePluginGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return false;
            }
            facts.Set("soul.soul_vigor", Math.Max(0.0f, soulVigor));
            facts.Set(
                "soul.necromantic_power",
                Mathf.Clamp(necromanticPower, 0.0f, 200.0f));
            facts.Set("soul.harvests.meager", Math.Max(0, meager));
            facts.Set("soul.harvests.worthy", Math.Max(0, worthy));
            facts.Set("soul.harvests.potent", Math.Max(0, potent));
            facts.Set("soul.harvests.prime", Math.Max(0, prime));
            return true;
        }

        internal bool TryGetCorpseDrainCounts(
            string sourceId,
            out int meager,
            out int worthy,
            out int potent,
            out int prime)
        {
            meager = 0;
            worthy = 0;
            potent = 0;
            prime = 0;
            if (!string.Equals(sourceId, BloodMagicPluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return false;
            }
            meager = Math.Max(0, facts.Get("blood.corpses_drained.meager", 0));
            worthy = Math.Max(0, facts.Get("blood.corpses_drained.worthy", 0));
            potent = Math.Max(0, facts.Get("blood.corpses_drained.potent", 0));
            prime = Math.Max(0, facts.Get("blood.corpses_drained.prime", 0));
            return true;
        }

        internal bool TryGetCorpseDrainStatistics(
            string sourceId,
            out int meager,
            out int worthy,
            out int potent,
            out int prime,
            out float qualitySum)
        {
            qualitySum = 0.0f;
            if (!TryGetCorpseDrainCounts(
                sourceId,
                out meager,
                out worthy,
                out potent,
                out prime))
            {
                return false;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return false;
            }
            qualitySum = Math.Max(0.0f, facts.Get("blood.corpses_drained.quality_sum", 0.0f));
            return true;
        }

        private static string NormalizeCorpseTier(string tier, float quality)
        {
            string value = (tier ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "meager" || value == "worthy" || value == "potent" || value == "prime") return value;
            if (quality <= 0.25f) return "meager";
            if (quality <= 0.50f) return "worthy";
            if (quality <= 0.75f) return "potent";
            return "prime";
        }

        private bool ShouldTrack()
        {
            return _enabled != null && _enabled.Value && _trackStatistics != null && _trackStatistics.Value && Hero.Current != null;
        }

        private static ContextualFacts Facts()
        {
            Services services = World.Services;
            GameplayMemory memory = services == null ? null : services.TryGet<GameplayMemory>();
            return memory == null ? null : memory.Context(MemoryContext);
        }

        private static void Increment(ContextualFacts facts, string key)
        {
            AddCounter(facts, key, 1);
        }

        private static void AddCounter(ContextualFacts facts, string key, int amount)
        {
            if (facts == null || amount <= 0)
            {
                return;
            }
            facts.Set(key, SaturatingAdd(Math.Max(0, facts.Get(key, 0)), amount));
        }

        private static int SaturatingAdd(int current, int amount)
        {
            long sum = (long)Math.Max(0, current) + Math.Max(0, amount);
            return sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }

        private static float SaturatingAdd(float current, float amount)
        {
            double sum = Math.Max(0.0, current) + Math.Max(0.0, amount);
            return double.IsNaN(sum) || sum <= 0.0
                ? 0.0f
                : (sum >= float.MaxValue ? float.MaxValue : (float)sum);
        }

        private void ReconcileStatistics(Hero hero)
        {
            if (hero == null || !ShouldTrack())
            {
                return;
            }
            ContextualFacts facts = Facts();
            if (facts == null)
            {
                return;
            }

            List<string> negativeCounters = new List<string>();
            foreach (KeyValuePair<string, object> entry in facts.GetAll())
            {
                if (entry.Value is int
                    && (int)entry.Value < 0
                    && IsCounterKey(entry.Key))
                {
                    negativeCounters.Add(entry.Key);
                }
            }
            for (int i = 0; i < negativeCounters.Count; i++)
            {
                facts.Set(negativeCounters[i], 0);
            }

            ReconcileFoeTotal(facts);
            ReconcileCorpseDrainTotal(facts);
            ReconcileActiveBounty(facts);
            facts.Set("state.wyrdnight_active", hero.HeroWyrdNight.Night);
        }

        private static bool IsCounterKey(string key)
        {
            return key.StartsWith("foes.", StringComparison.Ordinal)
                || key.StartsWith("deeds.", StringComparison.Ordinal)
                || key.StartsWith("blood.corpses_drained.", StringComparison.Ordinal)
                || key.StartsWith("meta.magic.", StringComparison.Ordinal);
        }

        private static void ReconcileFoeTotal(ContextualFacts facts)
        {
            if (facts == null)
            {
                return;
            }
            int total = 0;
            foreach (KeyValuePair<string, object> entry in facts.GetAll())
            {
                bool isCategory = string.Equals(entry.Key, "foes.summon", StringComparison.Ordinal)
                    || entry.Key.StartsWith("foes.weapon.", StringComparison.Ordinal)
                    || entry.Key.StartsWith("foes.magic.damage.", StringComparison.Ordinal)
                    || entry.Key.StartsWith("foes.magic.spell.", StringComparison.Ordinal);
                if (isCategory && entry.Value is int)
                {
                    total = SaturatingAdd(total, Math.Max(0, (int)entry.Value));
                }
            }
            facts.Set("foes.total", total);
        }

        private static void ReconcileCorpseDrainTotal(ContextualFacts facts)
        {
            if (facts == null)
            {
                return;
            }
            facts.Set(
                "blood.corpses_drained.total",
                SumFacts(
                    facts,
                    "blood.corpses_drained.meager",
                    "blood.corpses_drained.worthy",
                    "blood.corpses_drained.potent",
                    "blood.corpses_drained.prime"));
        }

        private static void ReconcileActiveBounty(ContextualFacts facts)
        {
            if (facts == null)
            {
                return;
            }
            Services services = World.Services;
            GameplayMemory memory = services == null ? null : services.TryGet<GameplayMemory>();
            ContextualFacts crimeFacts = memory == null ? null : memory.Context("faction.crime");
            if (crimeFacts == null)
            {
                return;
            }
            float total = 0.0f;
            foreach (KeyValuePair<string, object> entry in crimeFacts.GetAll())
            {
                if (!entry.Key.StartsWith("Bounty: ", StringComparison.Ordinal))
                {
                    continue;
                }
                float value;
                try
                {
                    value = Convert.ToSingle(entry.Value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    continue;
                }
                total = SaturatingAdd(total, Math.Max(0.0f, value));
            }
            facts.Set("bounty.current", total);
            facts.Set("bounty.highest", Math.Max(total, Math.Max(0.0f, facts.Get("bounty.highest", 0.0f))));
        }

        private void PublishPanel()
        {
            bool loadingScreenVisible = LoadingScreenStatisticsEnabled()
                && IsLoadingScreenVisible();
            PanelContent content = null;
            if (loadingScreenVisible
                && _loadingPanelContent != null
                && !_loadingGameplayDeserialized)
            {
                content = _loadingPanelContent;
            }
            else
            {
                content = CreateLivePanelContent();
                if (content != null
                    && LoadingScreenStatisticsEnabled()
                    && !string.IsNullOrWhiteSpace(_loadingPanelSlotId)
                    && _loadingGameplayDeserialized)
                {
                    string slotId = _loadingPanelSlotId;
                    bool replacedCache = _loadingPanelContent != null;
                    WritePanelCache(slotId, content);
                    LogDiagnostic((replacedCache
                        ? "Replaced cached loading-screen statistics with restored live data for save slot "
                        : "Initialized loading-screen statistics cache from restored live data for save slot ")
                        + slotId
                        + ".");
                    ClearLoadingPanelCache();
                }
                else if (content == null
                    && loadingScreenVisible
                    && _loadingPanelContent != null)
                {
                    content = _loadingPanelContent;
                }
            }

            if (content == null || !ResolveGftApi() || !EnsureGftIconsRegistered())
            {
                return;
            }
            PublishPanel(content);
        }

        private PanelContent CreateLivePanelContent()
        {
            ContextualFacts facts = Facts();
            Hero hero = Hero.Current;
            if (facts == null || hero == null) return null;

            List<PanelRow> deeds = BuildDeedRows(facts);
            LimitDeedRows(deeds, _maximumDeedRows.Value);
            deeds.Insert(0, new PanelRow(
                "HP: " + Math.Max(0, hero.Health.ModifiedInt).ToString("N0", CultureInfo.InvariantCulture)
                    + "   MP: " + Math.Max(0, hero.Mana.ModifiedInt).ToString("N0", CultureInfo.InvariantCulture)
                    + "   SP: " + Math.Max(0, hero.Stamina.ModifiedInt).ToString("N0", CultureInfo.InvariantCulture),
                string.Empty,
                "White",
                0));
            string[] resourceTexts =
            {
                Math.Max(0, hero.Cobweb.ModifiedInt).ToString(CultureInfo.InvariantCulture),
                Math.Max(0, hero.Wealth.ModifiedInt).ToString(CultureInfo.InvariantCulture),
                Mathf.CeilToInt(Math.Max(0.0f, hero.HeroItems.CurrentWeight)).ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + Math.Max(0, hero.HeroStats.EncumbranceLimit.ModifiedInt).ToString(CultureInfo.InvariantCulture)
                    + " kg"
            };
            string[] resourceIconIds = { "deeds-cobweb", "deeds-gold", "deeds-encumbrance" };
            string[] resourceStyles = { "Pale", "Pale", "Pale" };
            int leftSummaryRowCount = 1;
            List<string> availablePoints = new List<string>(4);
            int attributePoints = Math.Max(0, hero.Development.BaseStatPoints.ModifiedInt);
            if (attributePoints > 0)
            {
                availablePoints.Add("Attribute: " + attributePoints.ToString("N0", CultureInfo.InvariantCulture));
            }
            int skillPoints = Math.Max(0, hero.Development.TalentPoints.ModifiedInt);
            if (skillPoints > 0
                && HeroPointsHelper.OwnedPoints(HeroStatType.TalentPoints)
                    < HeroPointsHelper.MaxPoints(HeroStatType.TalentPoints))
            {
                availablePoints.Add("Skill: " + skillPoints.ToString("N0", CultureInfo.InvariantCulture));
            }
            int catalystPoints = Math.Max(0, hero.Stat(HeroStatType.CatalystTalentPoints).ModifiedInt);
            if (catalystPoints > 0
                && HeroPointsHelper.OwnedPoints(HeroStatType.CatalystTalentPoints)
                    < HeroPointsHelper.MaxPoints(HeroStatType.CatalystTalentPoints))
            {
                availablePoints.Add("Catalyst: " + catalystPoints.ToString("N0", CultureInfo.InvariantCulture));
            }
            int arthurMemories = Math.Max(0, hero.Stat(HeroStatType.WyrdMemoryShards).ModifiedInt);
            if (arthurMemories > 0
                && WyrdArthurUI.IsViewAvailable()
                && HeroPointsHelper.OwnedPoints(HeroStatType.WyrdMemoryShards)
                    < HeroPointsHelper.MaxPoints(HeroStatType.WyrdMemoryShards))
            {
                availablePoints.Add("Arthur: " + arthurMemories.ToString("N0", CultureInfo.InvariantCulture));
            }
            if (availablePoints.Count > 0)
            {
                deeds.Insert(leftSummaryRowCount++, new PanelRow(
                    string.Join("   ", availablePoints.ToArray()),
                    string.Empty,
                    "White",
                    0));
            }
            int wyrdWhispers = Math.Max(0, hero.Stat(HeroStatType.WyrdWhispers).ModifiedInt);
            if (wyrdWhispers > 0 && IsWyrdWhispersReminderVisible())
            {
                deeds.Insert(leftSummaryRowCount++, new PanelRow(
                    "Wyrd Whispers: " + wyrdWhispers.ToString("N0", CultureInfo.InvariantCulture),
                    string.Empty,
                    "Wyrd",
                    wyrdWhispers));
            }
            int deaths = Math.Max(0, facts.Get("deeds.deaths", 0));
            deeds.Add(new PanelRow(
                "Deaths: " + deaths.ToString("N0", CultureInfo.InvariantCulture),
                "skull",
                "Pale",
                deaths));
            List<PanelRow> weaponRows = BuildWeaponRows(facts);
            LimitCountRows(weaponRows, _maximumWeaponRows.Value, "Other", "Red", "combat");
            List<PanelRow> magicRows = BuildMagicRows(facts);
            LimitCountRows(magicRows, _maximumMagicRows.Value, "Other", "White", "magic");
            PanelRow trailingOtherMagicRow = null;
            if (magicRows.Count > 0
                && magicRows[magicRows.Count - 1].Text.StartsWith("Other:", StringComparison.Ordinal)
                && string.Equals(magicRows[magicRows.Count - 1].Icon, "magic", StringComparison.Ordinal)
                && string.Equals(magicRows[magicRows.Count - 1].Style, "White", StringComparison.Ordinal))
            {
                trailingOtherMagicRow = magicRows[magicRows.Count - 1];
                magicRows.RemoveAt(magicRows.Count - 1);
            }
            List<PanelRow> foes = new List<PanelRow>();
            foes.AddRange(weaponRows);
            foes.AddRange(magicRows);
            AddRow(foes, facts.Get("foes.summon", 0), "Summons", "summon", "Pink");
            if (trailingOtherMagicRow != null)
            {
                foes.Add(trailingOtherMagicRow);
            }
            if (_sortFoesByKillCount.Value)
            {
                RelabelOtherRows(foes, "combat", "Other Weapons");
                RelabelOtherRows(foes, "magic", "Other Magic");
                foes.Sort((left, right) =>
                {
                    int byValue = right.Value.CompareTo(left.Value);
                    return byValue != 0
                        ? byValue
                        : string.Compare(left.Text, right.Text, StringComparison.OrdinalIgnoreCase);
                });
            }

            string leftSubtitle = "Level: " + DisplayInteger("Level", hero.Level.ModifiedInt).ToString(CultureInfo.InvariantCulture)
                + "   XP: " + DisplayInteger("XP", hero.HeroStats.XP.ModifiedInt).ToString("N0", CultureInfo.InvariantCulture)
                + " / " + DisplayInteger("XP for next level", hero.HeroStats.XPForNextLevel.ModifiedInt).ToString("N0", CultureInfo.InvariantCulture);
            int totalFoes = Math.Max(0, facts.Get("foes.total", 0));
            if (DiagnosticPreviewEnabled())
            {
                totalFoes = 0;
                for (int i = 0; i < foes.Count; i++)
                {
                    totalFoes = SaturatingAdd(totalFoes, Math.Max(0, foes[i].Value));
                }
            }
            string rightSubtitle = "Total: " + totalFoes.ToString("N0", CultureInfo.InvariantCulture);
            string characterName = string.IsNullOrWhiteSpace(hero.Name)
                ? "DEEDS OF AVALON"
                : hero.Name.Trim().ToUpperInvariant();
            return new PanelContent
            {
                LeftTitle = characterName,
                LeftSubtitle = leftSubtitle,
                LeftTexts = Texts(deeds),
                LeftIconIds = Icons(deeds),
                LeftStyles = Styles(deeds),
                LeftResourceTexts = resourceTexts,
                LeftResourceIconIds = resourceIconIds,
                LeftResourceStyles = resourceStyles,
                LeftSummaryRowCount = leftSummaryRowCount,
                RightTitle = "FOES DEFEATED",
                RightSubtitle = rightSubtitle,
                RightTexts = Texts(foes),
                RightIconIds = Icons(foes),
                RightStyles = Styles(foes)
            };
        }

        private void PublishPanel(PanelContent content)
        {
            int leftRowCount = content.LeftTexts.Length
                + (content.LeftResourceTexts.Length > 0 ? 1 : 0);
            float resolutionScale = Mathf.Min(1.0f, Math.Max(1.0f, Screen.height) / ReferenceScreenHeight);
            float leftPanelHeight = PanelHeaderHeight + leftRowCount * PanelRowHeight;
            float rightPanelHeight = PanelHeaderHeight + content.RightTexts.Length * PanelRowHeight;
            float unscaledPanelHeight = Math.Max(leftPanelHeight, rightPanelHeight);
            float availableScreenHeight = Math.Max(1.0f, Screen.height - 64.0f * resolutionScale);
            float fitScale = availableScreenHeight / Math.Max(1.0f, unscaledPanelHeight);
            float panelScale = Mathf.Clamp(
                Math.Min(_panelScale.Value * resolutionScale, fitScale),
                0.5f,
                2.0f);
            float panelHeight = unscaledPanelHeight * panelScale;
            float centeredTopOffset = Math.Max(
                0.0f,
                (Screen.height - panelHeight) * 0.5f + _verticalOffset.Value * resolutionScale);
            List<object> args = new List<object>
            {
                PluginGuid,
                content.LeftTitle,
                content.LeftSubtitle,
                content.LeftTexts, content.LeftIconIds, content.LeftStyles,
                content.LeftResourceTexts, content.LeftResourceIconIds, content.LeftResourceStyles,
                content.LeftSummaryRowCount,
                content.RightTitle,
                content.RightSubtitle,
                content.RightTexts, content.RightIconIds, content.RightStyles,
                _panelOpacity.Value,
                _tooltipPanelOpacity.Value,
                _tooltipFadeSeconds.Value,
                _rightOffset.Value,
                centeredTopOffset,
                panelScale,
                _panelColumnWidth.Value,
                _columnGap.Value,
                _panelBackgroundOpacity.Value,
                _panelBackgroundPadding.Value
            };
            args.Add(_textShadowEnabled.Value);
            args.Add(_textShadowOpacity.Value);
            args.Add(_textShadowOffset.Value);
            args.Add(_textShadowSoftness.Value);
            args.Add(_textShadowStrength.Value);
            args.Add(_textOutlineEnabled.Value);
            args.Add(_textOutlineColor.Value);
            args.Add(_textOutlineOpacity.Value);
            args.Add(_textOutlineWidth.Value);
            args.Add(_textOutlineStrength.Value);
            args.Add(WhiteTextOutlineStrengthMultiplier);
            args.Add(_headerColor.Value);
            args.Add(_subheaderColor.Value);
            try
            {
                _gftTrySetMethod.Invoke(null, args.ToArray());
                _panelPresentationActive = true;
                SetGftTooltipActive(
                    _wheelWasOpen
                    && _visibleTooltipIds.Count > 0
                    && !_hideItemTooltipText.Value);
            }
            catch (Exception ex)
            {
                LogDiagnostic("Grail Floating Text panel update failed: " + ex.GetBaseException().Message);
                _gftTrySetMethod = null;
            }
        }

        private List<PanelRow> BuildDeedRows(ContextualFacts facts)
        {
            List<PanelRow> rows = new List<PanelRow>();
            AddRow(rows, facts.Get("deeds.wyrdnights_survived", 0), "Wyrdnights survived", "wyrd", "Wyrd");
            AddRow(rows, facts.Get("deeds.quests_completed", 0), "Quests completed", "reward", "Gold");
            AddRow(rows, facts.Get("deeds.locations_discovered", 0), "Locations discovered", "location", "Gold");
            AddRow(rows, facts.Get("deeds.recipes_learned", 0), "Recipes learned", "recipe", "Gold");
            AddRow(rows, facts.Get("deeds.items_crafted", 0), "Items crafted", "craft", "Gold");
            int totalGoldEarned = Math.Max(0, facts.Get("deeds.total_gold_earned", 0));
            AddRow(
                rows,
                totalGoldEarned,
                "Total gold earned",
                GoldEarnedIcon(DisplayInteger("Total gold earned", totalGoldEarned)),
                "Gold");
            AddRow(rows, facts.Get("deeds.food_eaten", 0), "Food eaten", "food", "Orange");
            AddRow(rows, facts.Get("deeds.potions_used", 0), "Potions used", "potion", "Blue");
            AddRow(rows, facts.Get("deeds.fishes_caught", 0), "Fish caught", "fish", "Cyan");
            if (_showBloodMagicStatistics.Value)
            {
                int bloodEssence = Math.Max(0, Mathf.RoundToInt(facts.Get("blood.essence", 0.0f)));
                int bloodPower = Mathf.Clamp(Mathf.RoundToInt(facts.Get("blood.power", 0.0f)), 0, 200);
                int displayedBloodEssence = DisplayInteger("Blood Essence", bloodEssence);
                int displayedBloodPower = DisplayInteger("Blood Power", bloodPower);
                string corpseIcon = CorpseTierIcon(facts);
                rows.Add(new PanelRow(
                    "Blood Essence: " + displayedBloodEssence.ToString("N0", CultureInfo.InvariantCulture)
                        + " (" + displayedBloodPower.ToString("N0", CultureInfo.InvariantCulture) + ")",
                    "magic_blood",
                    "Red",
                    displayedBloodEssence));
                if (_bloodMagicStatisticsMode.Value == BloodMagicStatisticsMode.Simple)
                {
                    AddRow(rows, facts.Get("blood.corpses_drained.total", 0), "Corpses Drained", corpseIcon, "Red");
                }
                else
                {
                    AddRow(rows, facts.Get("blood.corpses_drained.meager", 0), "Meager corpses", "corpse_meager", "Red");
                    AddRow(rows, facts.Get("blood.corpses_drained.worthy", 0), "Worthy corpses", "corpse_worthy", "Red");
                    AddRow(rows, facts.Get("blood.corpses_drained.potent", 0), "Potent corpses", "corpse_potent", "Red");
                    AddRow(rows, facts.Get("blood.corpses_drained.prime", 0), "Prime corpses", "corpse_prime", "Red");
                }
            }
            if (_showSoulAndServiceStatistics.Value)
            {
                int soulVigor = Math.Max(
                    0,
                    Mathf.RoundToInt(facts.Get("soul.soul_vigor", 0.0f)));
                int necromanticPower = Mathf.Clamp(
                    Mathf.RoundToInt(
                        facts.Get("soul.necromantic_power", 0.0f)),
                    0,
                    200);
                int displayedSoulVigor = DisplayInteger("Soul Vigor", soulVigor);
                int displayedNecromanticPower = DisplayInteger(
                    "Necromantic Power",
                    necromanticPower);
                rows.Add(new PanelRow(
                    "Soul Vigor: "
                        + displayedSoulVigor.ToString("N0", CultureInfo.InvariantCulture)
                        + " ("
                        + displayedNecromanticPower.ToString("N0", CultureInfo.InvariantCulture)
                        + ")",
                    "necro",
                    "Necrotic",
                    displayedSoulVigor));
                int meagerHarvests = Math.Max(
                    0,
                    facts.Get("soul.harvests.meager", 0));
                int worthyHarvests = Math.Max(
                    0,
                    facts.Get("soul.harvests.worthy", 0));
                int potentHarvests = Math.Max(
                    0,
                    facts.Get("soul.harvests.potent", 0));
                int primeHarvests = Math.Max(
                    0,
                    facts.Get("soul.harvests.prime", 0));
                if (_soulAndServiceStatisticsMode.Value
                    == SoulAndServiceStatisticsMode.Simple)
                {
                    AddRow(
                        rows,
                        SaturatingAdd(
                            SaturatingAdd(meagerHarvests, worthyHarvests),
                            SaturatingAdd(potentHarvests, primeHarvests)),
                        "Corpses Harvested",
                        "necro",
                        "Necrotic");
                }
                else
                {
                    AddRow(rows, meagerHarvests, "Meager harvests", "corpse_meager", "Necrotic");
                    AddRow(rows, worthyHarvests, "Worthy harvests", "corpse_worthy", "Necrotic");
                    AddRow(rows, potentHarvests, "Potent harvests", "corpse_potent", "Necrotic");
                    AddRow(rows, primeHarvests, "Prime harvests", "corpse_prime", "Necrotic");
                }
            }
            AddRow(rows, facts.Get("deeds.crimes_committed", 0), "Crimes committed", "crime", "Orange");
            AddRow(rows, facts.Get("deeds.locks_picked", 0), "Locks picked", "lock", "Orange");
            AddRow(rows, facts.Get("deeds.items_pickpocketed", 0), "Items pickpocketed", "pickpocket", "Orange");
            AddRow(rows, Mathf.RoundToInt(facts.Get("bounty.current", 0.0f)), "Active bounty", "crime", "Orange");
            AddRow(rows, Mathf.RoundToInt(facts.Get("bounty.highest", 0.0f)), "Highest bounty", "crime", "Red");
            int minutes = facts.Get("deeds.minutes_rested", 0);
            if (minutes > 0 || DiagnosticPreviewEnabled())
            {
                rows.Add(new PanelRow(
                    "Hours rested: " + DisplayNumber("Hours rested", minutes / 60.0f),
                    "rest",
                    "Pale",
                    minutes));
            }
            return rows;
        }

        private List<PanelRow> BuildRows(ContextualFacts facts, Category[] categories)
        {
            List<PanelRow> rows = new List<PanelRow>();
            for (int i = 0; i < categories.Length; i++)
            {
                Category category = categories[i];
                AddRow(rows, facts.Get(category.Key, 0), category.Label, category.Icon, category.Style);
            }
            return rows;
        }

        private List<PanelRow> BuildWeaponRows(ContextualFacts facts)
        {
            if (_weaponStatisticsMode.Value == WeaponStatisticsMode.Detailed)
            {
                List<PanelRow> detailedRows = new List<PanelRow>();
                Category[] categories = WeaponCategories();
                for (int i = 0; i < categories.Length; i++)
                {
                    Category category = categories[i];
                    int value = facts.Get(category.Key, 0);
                    if (string.Equals(category.Key, "foes.weapon.one_handed_axe", StringComparison.Ordinal))
                    {
                        value += facts.Get("foes.weapon.one_handed_sickle", 0);
                    }
                    AddRow(detailedRows, value, category.Label, category.Icon, category.Style);
                }
                AddRow(detailedRows, SumFacts(facts,
                    "foes.weapon.one_handed_other",
                    "foes.weapon.two_handed_other",
                    "foes.weapon.ranged",
                    "foes.weapon.other"), "Other", "combat", "Red");
                return detailedRows;
            }

            List<PanelRow> rows = new List<PanelRow>();
            AddRow(rows, SumFacts(facts,
                "foes.weapon.one_handed_sword",
                "foes.weapon.one_handed_axe",
                "foes.weapon.one_handed_blunt",
                "foes.weapon.one_handed_dagger",
                "foes.weapon.one_handed_polearm",
                "foes.weapon.one_handed_sickle",
                "foes.weapon.one_handed_other"), "One-Handed", "one_handed", "Red");
            AddRow(rows, SumFacts(facts,
                "foes.weapon.two_handed_sword",
                "foes.weapon.two_handed_axe",
                "foes.weapon.two_handed_blunt",
                "foes.weapon.two_handed_polearm",
                "foes.weapon.two_handed_other"), "Two-Handed", "two_handed", "Red");
            AddRow(rows, SumFacts(facts,
                "foes.weapon.short_bow",
                "foes.weapon.long_bow",
                "foes.weapon.heavy_bow",
                "foes.weapon.ranged"), "Bows", "archery", "Red");
            AddRow(rows, facts.Get("foes.weapon.shield", 0), "Shield", "shield", "Red");
            AddRow(rows, facts.Get("foes.weapon.unarmed", 0), "Unarmed", "unarmed", "Red");
            AddRow(rows, facts.Get("foes.weapon.throwable", 0), "Throwables", "combat", "Red");
            AddRow(rows, facts.Get("foes.weapon.other", 0), "Other", "combat", "Red");
            return rows;
        }

        private static int SumFacts(ContextualFacts facts, params string[] keys)
        {
            int total = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                total = SaturatingAdd(total, Math.Max(0, facts.Get(keys[i], 0)));
            }
            return total;
        }

        private List<PanelRow> BuildMagicRows(ContextualFacts facts)
        {
            List<PanelRow> rows = new List<PanelRow>();
            int legacyBloodMagicCount = 0;
            const string prefix = "foes.magic.spell.";
            foreach (KeyValuePair<string, object> entry in facts.GetAll())
            {
                if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal)
                    || !(entry.Value is int)
                    || (int)entry.Value < 0
                    || ((int)entry.Value == 0 && !DiagnosticPreviewEnabled())) continue;
                string key = entry.Key.Substring(prefix.Length);
                string name = facts.Get("display.magic." + key, key.Replace('_', ' '));
                if (IsBloodMagicDisplayName(name))
                {
                    legacyBloodMagicCount += Math.Max(0, (int)entry.Value);
                    continue;
                }
                int displayValue = DisplayInteger(name, (int)entry.Value);
                string magicType = ResolveSpellMagicType(facts, key);
                rows.Add(new PanelRow(
                    FormatIntegerStatistic(name, (int)entry.Value),
                    MagicIcon(magicType),
                    MagicStyle(magicType),
                    displayValue));
            }
            rows.Sort((left, right) =>
            {
                int byValue = right.Value.CompareTo(left.Value);
                return byValue != 0 ? byValue : string.Compare(left.Text, right.Text, StringComparison.OrdinalIgnoreCase);
            });
            Category[] categories = MagicCategories();
            for (int i = 0; i < categories.Length; i++)
            {
                Category category = categories[i];
                int value = facts.Get(category.Key, 0);
                if (string.Equals(category.Key, "foes.magic.damage.blood_magic", StringComparison.Ordinal))
                {
                    value += legacyBloodMagicCount;
                }
                AddRow(rows, value, category.Label, category.Icon, category.Style);
            }
            return rows;
        }

        private void LimitDeedRows(List<PanelRow> rows, int maximum)
        {
            maximum = Math.Max(1, maximum);
            if (rows.Count <= maximum) return;
            int hidden = rows.Count - maximum + (_showCollapsedRows.Value ? 1 : 0);
            rows.RemoveRange(maximum - (_showCollapsedRows.Value ? 1 : 0), hidden);
            if (_showCollapsedRows.Value) rows.Add(new PanelRow("Additional deed rows: " + hidden.ToString("N0", CultureInfo.InvariantCulture), "general", "Pale", hidden));
        }

        private void LimitCountRows(List<PanelRow> rows, int maximum, string label, string style, string icon)
        {
            maximum = Math.Max(1, maximum);
            if (rows.Count <= maximum) return;
            int keep = maximum - (_showCollapsedRows.Value ? 1 : 0);
            int other = 0;
            for (int i = keep; i < rows.Count; i++) other = SaturatingAdd(other, rows[i].Value);
            rows.RemoveRange(keep, rows.Count - keep);
            if (_showCollapsedRows.Value && (other > 0 || DiagnosticPreviewEnabled())) rows.Add(new PanelRow(FormatIntegerStatistic(label, other), icon, style, other));
        }

        private void AddRow(List<PanelRow> rows, int value, string label, string icon, string style)
        {
            if (value > 0 || DiagnosticPreviewEnabled())
            {
                int displayValue = DisplayInteger(label, value);
                rows.Add(new PanelRow(FormatIntegerStatistic(label, value), icon, style, displayValue));
            }
        }

        private static void RelabelOtherRows(List<PanelRow> rows, string icon, string label)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                PanelRow row = rows[i];
                if (!string.Equals(row.Icon, icon, StringComparison.Ordinal)
                    || !row.Text.StartsWith("Other:", StringComparison.Ordinal)) continue;
                rows[i] = new PanelRow(
                    label + row.Text.Substring("Other".Length),
                    row.Icon,
                    row.Style,
                    row.Value);
            }
        }

        private static string GoldEarnedIcon(int totalGoldEarned)
        {
            if (totalGoldEarned >= GoldEarnedVeryHighMinimum) return "gold_earned_very_high";
            if (totalGoldEarned >= GoldEarnedHighMinimum) return "gold_earned_high";
            if (totalGoldEarned >= GoldEarnedMediumMinimum) return "gold_earned_medium";
            if (totalGoldEarned >= GoldEarnedLowMinimum) return "gold_earned_low";
            return "gold_earned_very_low";
        }

        private static string CorpseTierIcon(ContextualFacts facts)
        {
            int total = Math.Max(0, facts.Get("blood.corpses_drained.total", 0));
            float quality = total > 0
                ? Mathf.Clamp01(facts.Get("blood.corpses_drained.quality_sum", 0.0f) / total)
                : 0.0f;
            if (quality <= 0.25f) return "corpse_meager";
            if (quality <= 0.50f) return "corpse_worthy";
            if (quality <= 0.75f) return "corpse_potent";
            return "corpse_prime";
        }

        private bool DiagnosticPreviewEnabled()
        {
            return _diagnostics != null && _diagnostics.Value;
        }

        private int DisplayInteger(string previewKey, int value)
        {
            if (value != 0 || !DiagnosticPreviewEnabled()) return value;
            switch (previewKey)
            {
                case "Level": return 19;
                case "XP": return 8450;
                case "XP for next level": return 10000;
                case "Wyrdnights survived": return 14;
                case "Quests completed": return 38;
                case "Locations discovered": return 57;
                case "Recipes learned": return 26;
                case "Items crafted": return 84;
                case "Food eaten": return 63;
                case "Potions used": return 29;
                case "Fish caught": return 17;
                case "Total gold earned": return 12450;
                case "Blood Essence": return 143;
                case "Blood Power": return 21;
                case "Corpses Drained": return 34;
                case "Meager corpses": return 16;
                case "Worthy corpses": return 10;
                case "Potent corpses": return 6;
                case "Prime corpses": return 2;
                case "Soul Vigor": return 116;
                case "Necromantic Power": return 18;
                case "Corpses Harvested": return 23;
                case "Meager harvests": return 12;
                case "Worthy harvests": return 7;
                case "Potent harvests": return 3;
                case "Prime harvests": return 1;
                case "Crimes committed": return 7;
                case "Locks picked": return 31;
                case "Items pickpocketed": return 12;
                case "Active bounty": return 240;
                case "Highest bounty": return 1180;
                case "One-Handed Sword": return 126;
                case "One-Handed Axe": return 48;
                case "One-Handed Blunt": return 37;
                case "One-Handed Dagger": return 29;
                case "One-Handed Spear": return 23;
                case "Two-Handed Sword": return 82;
                case "Two-Handed Axe": return 41;
                case "Two-Handed Blunt": return 24;
                case "Two-Handed Spear": return 33;
                case "Short Bow": return 58;
                case "Long Bow": return 44;
                case "Heavy Bow": return 17;
                case "Shield": return 21;
                case "Unarmed": return 9;
                case "Summons": return 36;
                case "One-Handed": return 251;
                case "Two-Handed": return 180;
                case "Bows": return 119;
                case "Throwables": return 14;
                case "Blood": return 32;
                case "Necrotic": return 24;
                case "Fire": return 76;
                case "Cold": return 43;
                case "Poison": return 27;
                case "Electric": return 31;
                case "Wyrdness": return 18;
                case "Pure": return 22;
                case "Wet": return 14;
                case "Other": return 12;
                default: return 24;
            }
        }

        private string DisplayNumber(string previewKey, float value)
        {
            if (value != 0.0f || !DiagnosticPreviewEnabled()) return value.ToString("0.#", CultureInfo.InvariantCulture);
            switch (previewKey)
            {
                case "Hours rested": return "43.5";
                default: return "24";
            }
        }

        private string FormatIntegerStatistic(string label, int value)
        {
            return label + ": " + DisplayInteger(label, value).ToString("N0", CultureInfo.InvariantCulture);
        }

        private static Category[] WeaponCategories()
        {
            return new[]
            {
                new Category("foes.weapon.one_handed_sword", "One-Handed Sword", "one_handed_sword", "Red"),
                new Category("foes.weapon.one_handed_axe", "One-Handed Axe", "one_handed_axe", "Red"),
                new Category("foes.weapon.one_handed_blunt", "One-Handed Blunt", "one_handed_blunt", "Red"),
                new Category("foes.weapon.one_handed_dagger", "One-Handed Dagger", "one_handed_dagger", "Red"),
                new Category("foes.weapon.one_handed_polearm", "One-Handed Spear", "one_handed_spear", "Red"),
                new Category("foes.weapon.two_handed_sword", "Two-Handed Sword", "two_handed_sword", "Red"),
                new Category("foes.weapon.two_handed_axe", "Two-Handed Axe", "two_handed_axe", "Red"),
                new Category("foes.weapon.two_handed_blunt", "Two-Handed Blunt", "two_handed_blunt", "Red"),
                new Category("foes.weapon.two_handed_polearm", "Two-Handed Spear", "two_handed_spear", "Red"),
                new Category("foes.weapon.short_bow", "Short Bow", "archery", "Red"),
                new Category("foes.weapon.long_bow", "Long Bow", "archery", "Red"),
                new Category("foes.weapon.heavy_bow", "Heavy Bow", "archery", "Red"),
                new Category("foes.weapon.shield", "Shield", "shield", "Red"),
                new Category("foes.weapon.unarmed", "Unarmed", "unarmed", "Red"),
                new Category("foes.weapon.throwable", "Throwables", "combat", "Red")
            };
        }

        private static Category[] MagicCategories()
        {
            return new[]
            {
                new Category("foes.magic.damage.fire", "Fire", "magic_fire", "Orange"),
                new Category("foes.magic.damage.cold", "Cold", "magic_cold", "Blue"),
                new Category("foes.magic.damage.wet", "Wet", "magic_wet", "Cyan"),
                new Category("foes.magic.damage.electric", "Electric", "magic_electric", "Gold"),
                new Category("foes.magic.damage.blood_magic", "Blood", "magic_blood", "Red"),
                new Category("foes.magic.damage.necrotic", "Necrotic", "necro", "Necrotic"),
                new Category("foes.magic.damage.poison", "Poison", "magic_poison", "Green"),
                new Category("foes.magic.damage.pure", "Pure", "magic_pure", "Pale"),
                new Category("foes.magic.damage.wyrdness", "Wyrdness", "wyrd", "Wyrd"),
                new Category("foes.magic.damage.other", "Other", "magic", "White")
            };
        }

        private static string ResolveSpellMagicType(ContextualFacts facts, string spellKey)
        {
            string[] magicTypes = { "fire", "cold", "poison", "electric", "wyrdness", "pure", "wet", "other" };
            string bestType = "other";
            int bestCount = 0;
            for (int i = 0; i < magicTypes.Length; i++)
            {
                int count = Math.Max(0, facts.Get("meta.magic.type." + spellKey + "." + magicTypes[i], 0));
                if (count > bestCount)
                {
                    bestType = magicTypes[i];
                    bestCount = count;
                }
            }
            return bestType;
        }

        private static string MagicIcon(string magicType)
        {
            switch (magicType)
            {
                case "blood_magic": return "magic_blood";
                case "fire": return "magic_fire";
                case "cold": return "magic_cold";
                case "poison": return "magic_poison";
                case "electric": return "magic_electric";
                case "wyrdness": return "wyrd";
                case "pure": return "magic_pure";
                case "wet": return "magic_wet";
                default: return "magic";
            }
        }

        private static string MagicStyle(string magicType)
        {
            switch (magicType)
            {
                case "blood_magic": return "Red";
                case "fire": return "Orange";
                case "cold": return "Blue";
                case "poison": return "Green";
                case "electric": return "Gold";
                case "wyrdness": return "Wyrd";
                case "pure": return "Pale";
                case "wet": return "Cyan";
                default: return "White";
            }
        }

        private static string[] Texts(List<PanelRow> rows) { return rows.ConvertAll(row => row.Text).ToArray(); }
        private static string[] Icons(List<PanelRow> rows) { return rows.ConvertAll(row => row.Icon).ToArray(); }
        private static string[] Styles(List<PanelRow> rows) { return rows.ConvertAll(row => row.Style).ToArray(); }

        private bool ResolveGftApi()
        {
            if (_gftTrySetMethod != null) return true;
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(GftPluginGuid, out info) || info == null || info.Instance == null) return false;
            Type api = info.Instance.GetType().Assembly.GetType("GrailFloatingText.QuickWheelPanelApi", false);
            if (api == null) return false;
            FieldInfo apiVersionField = api.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
            if (apiVersionField == null || (int)apiVersionField.GetRawConstantValue() < 15)
            {
                return false;
            }
            _gftTrySetMethod = api.GetMethod("TrySet", BindingFlags.Public | BindingFlags.Static);
            if (_gftTrySetMethod == null || _gftTrySetMethod.GetParameters().Length != 38)
            {
                _gftTrySetMethod = null;
                return false;
            }
            _gftTryRegisterIconsMethod = api.GetMethod("TryRegisterIcons", BindingFlags.Public | BindingFlags.Static);
            if (_gftTryRegisterIconsMethod == null || _gftTryRegisterIconsMethod.GetParameters().Length != 4)
            {
                _gftTrySetMethod = null;
                _gftTryRegisterIconsMethod = null;
                return false;
            }
            _gftSetTooltipActiveMethod = api.GetMethod("SetTooltipActive", BindingFlags.Public | BindingFlags.Static);
            _gftClearMethod = api.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
            return _gftTrySetMethod != null;
        }

        private bool EnsureGftIconsRegistered()
        {
            if (_gftIconsRegistered)
            {
                return true;
            }
            if (_gftTryRegisterIconsMethod == null)
            {
                return false;
            }

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = string.IsNullOrEmpty(assemblyPath)
                ? string.Empty
                : Path.GetDirectoryName(assemblyPath);
            string iconDirectory = string.IsNullOrEmpty(assemblyDirectory)
                ? string.Empty
                : Path.Combine(assemblyDirectory, "icons");
            try
            {
                object result = _gftTryRegisterIconsMethod.Invoke(null, new object[]
                {
                    PluginGuid,
                    iconDirectory,
                    new[] { "deeds-cobweb", "deeds-gold", "deeds-encumbrance" },
                    new[] { "cobweb.png", "gold.png", "encumbrance.png" }
                });
                _gftIconsRegistered = result is bool && (bool)result;
                return _gftIconsRegistered;
            }
            catch (Exception ex)
            {
                LogDiagnostic("Grail Floating Text quick-wheel icon registration failed: " + ex.GetBaseException().Message);
                _gftTryRegisterIconsMethod = null;
                return false;
            }
        }

        private bool IsBloodMagicDamage(Damage damage)
        {
            if (damage == null)
            {
                return false;
            }
            if (_bloodMagicIsDamageMethod == null)
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(BloodMagicPluginGuid, out info)
                    || info == null
                    || info.Instance == null)
                {
                    return false;
                }
                Type api = info.Instance.GetType().Assembly.GetType("BloodMagicExpansion.BloodMagicApi", false);
                _bloodMagicIsDamageMethod = api == null
                    ? null
                    : api.GetMethod("IsBloodMagicDamage", BindingFlags.Public | BindingFlags.Static);
                if (_bloodMagicIsDamageMethod == null)
                {
                    return false;
                }
            }
            try
            {
                object result = _bloodMagicIsDamageMethod.Invoke(null, new object[] { damage });
                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                LogDiagnostic("Blood Magic damage classification failed: " + ex.GetBaseException().Message);
                _bloodMagicIsDamageMethod = null;
                return false;
            }
        }

        private bool IsNecroticDamage(Damage damage)
        {
            if (damage == null)
            {
                return false;
            }
            if (_soulAndServiceIsNecroticDamageMethod == null)
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
                    "SoulAndService.SoulAndServiceApi",
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
                    return false;
                }
                _soulAndServiceIsNecroticDamageMethod = api.GetMethod(
                    "IsNecroticDamage",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(object) },
                    null);
                if (_soulAndServiceIsNecroticDamageMethod == null)
                {
                    return false;
                }
            }

            try
            {
                object result = _soulAndServiceIsNecroticDamageMethod.Invoke(
                    null,
                    new object[] { damage });
                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                LogDiagnostic(
                    "Soul and Service Necrotic damage classification failed: "
                    + ex.GetBaseException().Message);
                _soulAndServiceIsNecroticDamageMethod = null;
                return false;
            }
        }

        private bool IsBloodMagicDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }
            if (_bloodMagicIsDisplayNameMethod == null)
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(BloodMagicPluginGuid, out info)
                    || info == null
                    || info.Instance == null)
                {
                    return false;
                }
                Type api = info.Instance.GetType().Assembly.GetType("BloodMagicExpansion.BloodMagicApi", false);
                _bloodMagicIsDisplayNameMethod = api == null
                    ? null
                    : api.GetMethod("IsBloodMagicDisplayName", BindingFlags.Public | BindingFlags.Static);
                if (_bloodMagicIsDisplayNameMethod == null)
                {
                    return false;
                }
            }
            try
            {
                object result = _bloodMagicIsDisplayNameMethod.Invoke(null, new object[] { displayName });
                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                LogDiagnostic("Blood Magic display-name classification failed: " + ex.GetBaseException().Message);
                _bloodMagicIsDisplayNameMethod = null;
                return false;
            }
        }

        private void SetGftTooltipActive(bool active)
        {
            if (ResolveGftApi() && _gftSetTooltipActiveMethod != null)
            {
                try { _gftSetTooltipActiveMethod.Invoke(null, new object[] { PluginGuid, active }); } catch { }
            }
        }

        private void ClearGftPanel()
        {
            if (!_panelPresentationActive)
            {
                return;
            }
            if (ResolveGftApi() && _gftClearMethod != null)
            {
                try { _gftClearMethod.Invoke(null, new object[] { PluginGuid }); } catch { }
            }
            _panelPresentationActive = false;
        }

        internal bool BeforeTooltipShown(VCQuickItemTooltipUI tooltip)
        {
            if (_hideItemTooltipText != null && _hideItemTooltipText.Value)
            {
                if (tooltip != null) tooltip.HideItem();
                return false;
            }
            if (tooltip != null) _visibleTooltipIds.Add(tooltip.GetInstanceID());
            SetGftTooltipActive(true);
            return true;
        }

        internal void AfterTooltipHidden(VCQuickItemTooltipUI tooltip)
        {
            if (tooltip != null) _visibleTooltipIds.Remove(tooltip.GetInstanceID());
            SetGftTooltipActive(_visibleTooltipIds.Count > 0);
        }

        private void ApplyPointsAvailableVisibility()
        {
            if (!_hidePointsAvailable.Value || GloriousUiOwnsQuickWheelHud())
            {
                RestoreCharacterPoints();
                return;
            }
            if (_characterPointsSnapshot == null)
            {
                VCCharacterPointsAvailable view = FindCharacterPointsView();
                CanvasGroup selectedGroup = view == null || _characterPointsCanvasGroupField == null
                    ? null
                    : _characterPointsCanvasGroupField.GetValue(view) as CanvasGroup;
                if (selectedGroup != null) _characterPointsSnapshot = new CharacterPointsSnapshot(selectedGroup);
            }
            if (_characterPointsSnapshot != null && _characterPointsSnapshot.Group != null)
            {
                CanvasGroup group = _characterPointsSnapshot.Group;
                group.alpha = 0.0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                group.gameObject.SetActive(false);
            }
        }

        private VCCharacterPointsAvailable FindCharacterPointsView()
        {
            if (_characterPointsView != null && _characterPointsView.gameObject.scene.IsValid())
            {
                return _characterPointsView;
            }
            _characterPointsView = null;
            VCCharacterPointsAvailable[] views = Resources.FindObjectsOfTypeAll<VCCharacterPointsAvailable>();
            for (int i = 0; i < views.Length; i++)
            {
                VCCharacterPointsAvailable view = views[i];
                if (view == null || !view.gameObject.scene.IsValid()) continue;
                if (_characterPointsView == null) _characterPointsView = view;
                if (!view.gameObject.activeInHierarchy) continue;
                _characterPointsView = view;
                break;
            }
            return _characterPointsView;
        }

        private bool IsWyrdWhispersReminderVisible()
        {
            VCCharacterPointsAvailable view = FindCharacterPointsView();
            return view != null
                && _characterPointsWhispersVisibleField != null
                && (bool)_characterPointsWhispersVisibleField.GetValue(view);
        }

        private static bool GloriousUiOwnsQuickWheelHud()
        {
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(GloriousUiPluginGuid, out info) || info == null || info.Instance == null) return false;
            ConfigEntry<bool> enabled;
            ConfigEntry<bool> hideGameplayHud;
            return info.Instance.Config.TryGetEntry(new ConfigDefinition("1. Core", "Enabled"), out enabled)
                && enabled.Value
                && info.Instance.Config.TryGetEntry(new ConfigDefinition("2. HUD", "HideGameplayHudInQuickUseWheel"), out hideGameplayHud)
                && hideGameplayHud.Value;
        }

        private void RestoreCharacterPoints()
        {
            CharacterPointsSnapshot snapshot = _characterPointsSnapshot;
            _characterPointsSnapshot = null;
            if (snapshot == null || snapshot.Group == null) return;
            snapshot.Group.alpha = snapshot.Alpha;
            snapshot.Group.interactable = snapshot.Interactable;
            snapshot.Group.blocksRaycasts = snapshot.BlocksRaycasts;
            snapshot.Group.gameObject.SetActive(snapshot.ActiveSelf);
        }

        private void CloseWheelPresentation()
        {
            _wheelWasOpen = false;
            _nextPanelRefresh = 0.0f;
            _visibleTooltipIds.Clear();
            RestoreCharacterPoints();
            RefreshPanelPresentation();
        }

        private bool IsPauseMenuVisible()
        {
            return _pauseMenuView != null
                && _pauseMenuView.gameObject != null
                && _pauseMenuView.gameObject.activeInHierarchy;
        }

        private static bool IsLoadingScreenVisible()
        {
            return LoadingScreenUI.IsLoading
                || World.HasAny<LoadingScreenUI>();
        }

        private bool LoadingScreenStatisticsEnabled()
        {
            return _enabled != null
                && _enabled.Value
                && _showLoadingScreenStatistics != null
                && _showLoadingScreenStatistics.Value;
        }

        private bool ShouldShowPanel(bool pauseMenuVisible, bool loadingScreenVisible)
        {
            if (loadingScreenVisible)
            {
                return LoadingScreenStatisticsEnabled();
            }

            return (_wheelWasOpen
                    && _showQuickWheelStatistics != null
                    && _showQuickWheelStatistics.Value)
                || (pauseMenuVisible
                    && _showPauseMenuStatistics != null
                    && _showPauseMenuStatistics.Value);
        }

        private void RefreshPanelPresentation()
        {
            float now = Time.unscaledTime;
            _nextPanelRefresh = now + 0.2f;
            if (_enabled != null
                && _enabled.Value
                && ShouldShowPanel(
                    IsPauseMenuVisible(),
                    IsLoadingScreenVisible()))
            {
                PublishPanel();
            }
            else
            {
                ClearGftPanel();
            }
        }

        internal void OnQuickWheelAppearing()
        {
            if (_enabled == null || !_enabled.Value)
            {
                return;
            }

            _wheelWasOpen = true;
            ApplyPointsAvailableVisibility();
            RefreshPanelPresentation();
        }

        internal void OnQuickWheelDisappearing()
        {
            if (_wheelWasOpen)
            {
                CloseWheelPresentation();
            }
        }

        private void OnQuickWheelDiscarded(Model model)
        {
            if (_wheelWasOpen)
            {
                CloseWheelPresentation();
            }
        }

        internal void OnPauseMenuAppearing(VMenuUI view)
        {
            _pauseMenuView = view;
            if (_enabled != null && _enabled.Value)
            {
                RefreshPanelPresentation();
            }
        }

        internal void OnPauseMenuDisappearing(VMenuUI view)
        {
            if (view == null || ReferenceEquals(_pauseMenuView, view))
            {
                _pauseMenuView = null;
                RefreshPanelPresentation();
            }
        }

        internal void CapturePendingSaveSnapshot()
        {
            ReconcileStatistics(Hero.Current);
            _pendingSavePanelContent = LoadingScreenStatisticsEnabled()
                ? CreateLivePanelContent()
                : null;
            _pendingSaveSnapshot = null;
            if (_exportOnSuccessfulSave != null && _exportOnSuccessfulSave.Value)
            {
                _pendingSaveSnapshot = CreateStatisticsSnapshot();
            }
        }

        internal void PublishSuccessfulSaveSnapshot(string slotId)
        {
            StatisticsSnapshot snapshot = _pendingSaveSnapshot;
            PanelContent panelContent = _pendingSavePanelContent;
            _pendingSaveSnapshot = null;
            _pendingSavePanelContent = null;
            if (snapshot != null) WriteSnapshot(snapshot, "successful save");
            if (panelContent != null) WritePanelCache(slotId, panelContent);
        }

        internal void DiscardFailedSaveSnapshot()
        {
            _pendingSaveSnapshot = null;
            _pendingSavePanelContent = null;
        }

        internal void ScheduleLoadedStatisticsExport()
        {
            if (!LoadingScreenStatisticsEnabled())
            {
                ClearLoadingPanelCache();
            }
            else if (!string.IsNullOrWhiteSpace(_loadingPanelSlotId))
            {
                _loadingGameplayDeserialized = true;
                _nextPanelRefresh = 0.0f;
            }
            if (_exportOnSuccessfulSave != null && _exportOnSuccessfulSave.Value)
            {
                _pendingLoadedExportAt = Time.unscaledTime + 1.0f;
            }
        }

        internal void PrepareLoadingPanel(SaveSlot saveSlot)
        {
            ClearLoadingPanelCache();
            if (!LoadingScreenStatisticsEnabled())
            {
                _pauseMenuView = null;
                ClearGftPanel();
                return;
            }
            if (saveSlot == null
                || string.IsNullOrWhiteSpace(saveSlot.ID))
            {
                return;
            }

            _loadingPanelSlotId = saveSlot.ID;
            string path = PanelCachePath(saveSlot.ID);
            if (!File.Exists(path))
            {
                LogDiagnostic("No cached loading-screen statistics exist yet for save slot " + saveSlot.ID + ".");
                return;
            }

            try
            {
                SavedPanelCache cache = JsonUtility.FromJson<SavedPanelCache>(File.ReadAllText(path, Encoding.UTF8));
                PanelContent panelContent = PanelContentFromCache(cache);
                if (cache == null
                    || !string.Equals(cache.SlotId, saveSlot.ID, StringComparison.Ordinal)
                    || !IsValidPanelContent(panelContent))
                {
                    throw new InvalidDataException("The cache does not match the selected save slot or panel format.");
                }

                _loadingPanelContent = panelContent;
                _loadingGameplayDeserialized = false;
                LogDiagnostic("Prepared cached loading-screen statistics for save slot " + saveSlot.ID + ".");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not read cached loading-screen statistics for save slot " + saveSlot.ID + ": " + ex.GetBaseException().Message);
                _loadingPanelContent = null;
                _loadingGameplayDeserialized = false;
            }
        }

        private void WritePanelCache(string slotId, PanelContent panelContent)
        {
            if (!LoadingScreenStatisticsEnabled()
                || string.IsNullOrWhiteSpace(slotId)
                || !IsValidPanelContent(panelContent))
            {
                return;
            }

            string path = PanelCachePath(slotId);
            string temp = path + ".tmp";
            try
            {
                SavedPanelCache cache = new SavedPanelCache
                {
                    FormatVersion = 2,
                    SlotId = slotId,
                    WrittenUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    LeftTitle = panelContent.LeftTitle,
                    LeftSubtitle = panelContent.LeftSubtitle,
                    LeftTexts = panelContent.LeftTexts,
                    LeftIconIds = panelContent.LeftIconIds,
                    LeftStyles = panelContent.LeftStyles,
                    LeftResourceTexts = panelContent.LeftResourceTexts,
                    LeftResourceIconIds = panelContent.LeftResourceIconIds,
                    LeftResourceStyles = panelContent.LeftResourceStyles,
                    LeftSummaryRowCount = panelContent.LeftSummaryRowCount,
                    RightTitle = panelContent.RightTitle,
                    RightSubtitle = panelContent.RightSubtitle,
                    RightTexts = panelContent.RightTexts,
                    RightIconIds = panelContent.RightIconIds,
                    RightStyles = panelContent.RightStyles
                };
                string json = JsonUtility.ToJson(cache, true);
                SavedPanelCache roundTrip = JsonUtility.FromJson<SavedPanelCache>(json);
                PanelContent roundTripPanel = PanelContentFromCache(roundTrip);
                if (roundTrip == null
                    || !string.Equals(roundTrip.SlotId, slotId, StringComparison.Ordinal)
                    || !IsValidPanelContent(roundTripPanel))
                {
                    throw new InvalidDataException("The serialized cache failed round-trip validation.");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, json + "\n", new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                LogDiagnostic("Wrote loading-screen statistics cache for save slot " + slotId + ".");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not write cached loading-screen statistics for save slot " + slotId + ": " + ex.GetBaseException().Message);
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        private static string PanelCachePath(string slotId)
        {
            return Path.Combine(
                Paths.ConfigPath,
                "DeedsOfAvalon",
                "PanelCache",
                SafeKey(slotId) + ".json");
        }

        private void ClearLoadingPanelCache()
        {
            _loadingPanelContent = null;
            _loadingPanelSlotId = null;
            _loadingGameplayDeserialized = false;
            _loadingPanelWasVisible = false;
        }

        private static bool IsValidPanelContent(PanelContent content)
        {
            return content != null
                && MatchingPanelArrays(content.LeftTexts, content.LeftIconIds, content.LeftStyles, 256)
                && MatchingPanelArrays(content.LeftResourceTexts, content.LeftResourceIconIds, content.LeftResourceStyles, 16)
                && MatchingPanelArrays(content.RightTexts, content.RightIconIds, content.RightStyles, 256)
                && content.LeftSummaryRowCount >= 0
                && content.LeftSummaryRowCount <= content.LeftTexts.Length;
        }

        private static PanelContent PanelContentFromCache(SavedPanelCache cache)
        {
            if (cache == null || cache.FormatVersion != 2)
            {
                return null;
            }

            return new PanelContent
            {
                LeftTitle = cache.LeftTitle,
                LeftSubtitle = cache.LeftSubtitle,
                LeftTexts = cache.LeftTexts,
                LeftIconIds = cache.LeftIconIds,
                LeftStyles = cache.LeftStyles,
                LeftResourceTexts = cache.LeftResourceTexts,
                LeftResourceIconIds = cache.LeftResourceIconIds,
                LeftResourceStyles = cache.LeftResourceStyles,
                LeftSummaryRowCount = cache.LeftSummaryRowCount,
                RightTitle = cache.RightTitle,
                RightSubtitle = cache.RightSubtitle,
                RightTexts = cache.RightTexts,
                RightIconIds = cache.RightIconIds,
                RightStyles = cache.RightStyles
            };
        }

        private static bool MatchingPanelArrays(string[] texts, string[] iconIds, string[] styles, int maximum)
        {
            return texts != null
                && iconIds != null
                && styles != null
                && texts.Length == iconIds.Length
                && texts.Length == styles.Length
                && texts.Length <= maximum;
        }

        private bool ExportCurrentSavedStatistics(string reason)
        {
            StatisticsSnapshot snapshot = CreateStatisticsSnapshot();
            if (snapshot == null)
            {
                return false;
            }
            WriteSnapshot(snapshot, reason);
            return true;
        }

        private StatisticsSnapshot CreateStatisticsSnapshot()
        {
            Hero hero = Hero.Current;
            ContextualFacts facts = Facts();
            if (hero == null || facts == null) return null;
            string characterId = hero.HeroID == Guid.Empty ? SafeKey(hero.ID) : hero.HeroID.ToString("D");
            string directory = Path.Combine(Paths.ConfigPath, "DeedsOfAvalon", "Characters", characterId);
            string path = Path.Combine(directory, "statistics.json");
            List<KeyValuePair<string, object>> entries = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> entry in facts.GetAll())
            {
                if (!entry.Key.StartsWith("state.", StringComparison.Ordinal)
                    && !entry.Key.StartsWith("bounty.faction.", StringComparison.Ordinal)
                    && !string.Equals(entry.Key, "deeds.times_rested", StringComparison.Ordinal)) entries.Add(entry);
            }
            entries.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));

            StringBuilder json = new StringBuilder(2048);
            json.Append("{\n  \"formatVersion\": 1,\n  \"mod\": \"Deeds of Avalon\",\n  \"characterId\": \"").Append(JsonEscape(characterId)).Append("\",\n");
            json.Append("  \"characterName\": \"").Append(JsonEscape(hero.Name)).Append("\",\n");
            json.Append("  \"level\": ").Append(hero.Level.ModifiedInt.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"currentXp\": ").Append(hero.HeroStats.XP.ModifiedInt.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"xpToNextLevel\": ").Append(hero.HeroStats.XPForNextLevel.ModifiedInt.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"writtenUtc\": \"").Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append("\",\n  \"statistics\": {\n");
            for (int i = 0; i < entries.Count; i++)
            {
                json.Append("    \"").Append(JsonEscape(entries[i].Key)).Append("\": ").Append(JsonValue(entries[i].Value));
                if (i + 1 < entries.Count) json.Append(',');
                json.Append('\n');
            }
            json.Append("  }\n}\n");
            return new StatisticsSnapshot(path, json.ToString());
        }

        private void WriteSnapshot(StatisticsSnapshot snapshot, string reason)
        {
            string temp = snapshot.Path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snapshot.Path));
                File.WriteAllText(temp, snapshot.Json, new UTF8Encoding(false));
                if (File.Exists(snapshot.Path)) File.Replace(temp, snapshot.Path, null);
                else File.Move(temp, snapshot.Path);
                LogDiagnostic("Wrote " + reason + " statistics to " + snapshot.Path + ".");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not write character statistics: " + ex.GetBaseException().Message);
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        private static string JsonValue(object value)
        {
            if (value is bool) return (bool)value ? "true" : "false";
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            return "\"" + JsonEscape(value == null ? string.Empty : value.ToString()) + "\"";
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static string SafeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            StringBuilder result = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                result.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return result.ToString().Trim('_');
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value) Logger.LogInfo(message);
        }

        [Serializable]
        private sealed class SavedPanelCache
        {
            public int FormatVersion;
            public string SlotId;
            public string WrittenUtc;
            public string LeftTitle;
            public string LeftSubtitle;
            public string[] LeftTexts;
            public string[] LeftIconIds;
            public string[] LeftStyles;
            public string[] LeftResourceTexts;
            public string[] LeftResourceIconIds;
            public string[] LeftResourceStyles;
            public int LeftSummaryRowCount;
            public string RightTitle;
            public string RightSubtitle;
            public string[] RightTexts;
            public string[] RightIconIds;
            public string[] RightStyles;
        }

        [Serializable]
        private sealed class PanelContent
        {
            public string LeftTitle;
            public string LeftSubtitle;
            public string[] LeftTexts;
            public string[] LeftIconIds;
            public string[] LeftStyles;
            public string[] LeftResourceTexts;
            public string[] LeftResourceIconIds;
            public string[] LeftResourceStyles;
            public int LeftSummaryRowCount;
            public string RightTitle;
            public string RightSubtitle;
            public string[] RightTexts;
            public string[] RightIconIds;
            public string[] RightStyles;
        }

        private sealed class PanelRow
        {
            internal readonly string Text;
            internal readonly string Icon;
            internal readonly string Style;
            internal readonly int Value;
            internal PanelRow(string text, string icon, string style, int value) { Text = text; Icon = icon; Style = style; Value = value; }
        }

        private sealed class Category
        {
            internal readonly string Key;
            internal readonly string Label;
            internal readonly string Icon;
            internal readonly string Style;
            internal Category(string key, string label, string icon, string style) { Key = key; Label = label; Icon = icon; Style = style; }
        }

        private sealed class StatisticsSnapshot
        {
            internal readonly string Path;
            internal readonly string Json;
            internal StatisticsSnapshot(string path, string json) { Path = path; Json = json; }
        }

        private sealed class CharacterPointsSnapshot
        {
            internal readonly CanvasGroup Group;
            internal readonly bool ActiveSelf;
            internal readonly float Alpha;
            internal readonly bool Interactable;
            internal readonly bool BlocksRaycasts;
            internal CharacterPointsSnapshot(CanvasGroup group)
            {
                Group = group;
                ActiveSelf = group.gameObject.activeSelf;
                Alpha = group.alpha;
                Interactable = group.interactable;
                BlocksRaycasts = group.blocksRaycasts;
            }
        }
    }

    [HarmonyPatch(typeof(GameplayMemory), nameof(GameplayMemory.OnBeforeSerialize))]
    internal static class GameplayMemoryBeforeSerializePatch
    {
        private static void Prefix() { if (DeedsOfAvalonPlugin.Instance != null) DeedsOfAvalonPlugin.Instance.CapturePendingSaveSnapshot(); }
    }

    [HarmonyPatch(typeof(GameplayMemory), nameof(GameplayMemory.OnAfterDeserialize))]
    internal static class GameplayMemoryAfterDeserializePatch
    {
        private static void Postfix() { if (DeedsOfAvalonPlugin.Instance != null) DeedsOfAvalonPlugin.Instance.ScheduleLoadedStatisticsExport(); }
    }

    [HarmonyPatch(typeof(SaveInProgressHandle), nameof(SaveInProgressHandle.MarkSucceeded))]
    internal static class SaveSucceededPatch
    {
        private static void Postfix(SaveInProgressHandle __instance)
        {
            if (DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.PublishSuccessfulSaveSnapshot(__instance.SlotId);
            }
        }
    }

    [HarmonyPatch(typeof(SaveInProgressHandle), nameof(SaveInProgressHandle.Dispose))]
    internal static class SaveFailedPatch
    {
        private static void Postfix(SaveInProgressHandle __instance)
        {
            if (!__instance.Success && DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.DiscardFailedSaveSnapshot();
            }
        }
    }

    [HarmonyPatch(typeof(LoadSave), nameof(LoadSave.Load))]
    internal static class LoadSavePatch
    {
        private static void Prefix(SaveSlot saveSlot)
        {
            if (saveSlot != null
                && saveSlot.CanLoad()
                && DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.PrepareLoadingPanel(saveSlot);
            }
        }
    }

    [HarmonyPatch(typeof(VCQuickItemTooltipUI), nameof(VCQuickItemTooltipUI.ShowItem))]
    internal static class QuickItemTooltipShowPatch
    {
        private static bool Prefix(VCQuickItemTooltipUI __instance)
        {
            return DeedsOfAvalonPlugin.Instance == null || DeedsOfAvalonPlugin.Instance.BeforeTooltipShown(__instance);
        }
    }

    [HarmonyPatch(typeof(VQuickUseWheelUI), "Appear")]
    internal static class QuickUseWheelAppearPatch
    {
        private static void Postfix()
        {
            if (DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.OnQuickWheelAppearing();
            }
        }
    }

    [HarmonyPatch(typeof(VQuickUseWheelUI), "Disappear")]
    internal static class QuickUseWheelDisappearPatch
    {
        private static void Prefix()
        {
            if (DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.OnQuickWheelDisappearing();
            }
        }
    }

    [HarmonyPatch(typeof(VMenuUI), "OnInitialize")]
    internal static class PauseMenuInitializePatch
    {
        private static void Postfix(VMenuUI __instance)
        {
            if (DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.OnPauseMenuAppearing(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(VMenuUI), "OnDiscard")]
    internal static class PauseMenuDiscardPatch
    {
        private static void Prefix(VMenuUI __instance)
        {
            if (DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.OnPauseMenuDisappearing(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(VCQuickItemTooltipUI), nameof(VCQuickItemTooltipUI.HideItem))]
    internal static class QuickItemTooltipHidePatch
    {
        private static void Postfix(VCQuickItemTooltipUI __instance)
        {
            if (DeedsOfAvalonPlugin.Instance != null) DeedsOfAvalonPlugin.Instance.AfterTooltipHidden(__instance);
        }
    }

    [HarmonyPatch(typeof(CharacterFishingRod), nameof(CharacterFishingRod.InspectFish))]
    internal static class CharacterFishingRodInspectFishPatch
    {
        private static void Postfix(CharacterFishingRod __instance)
        {
            if (DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.RecordFishCaught(__instance);
            }
        }
    }

    [HarmonyPatch]
    internal static class CrimeActionWrapperPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] names = { "Theft", "Pickpocket", "Trespassing", "Lockpicking", "Combat", "Murder" };
            MethodInfo[] methods = typeof(CommitCrime).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].ReturnType != typeof(bool))
                {
                    continue;
                }
                for (int n = 0; n < names.Length; n++)
                {
                    if (string.Equals(methods[i].Name, names[n], StringComparison.Ordinal))
                    {
                        yield return methods[i];
                        break;
                    }
                }
            }
        }

        private static void Prefix(out bool __state)
        {
            __state = DeedsOfAvalonPlugin.Instance != null;
            if (__state)
            {
                DeedsOfAvalonPlugin.Instance.BeginCrimeActionWrapper();
            }
        }

        private static void Postfix(bool __result, ref bool __state)
        {
            if (__state && DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.EndCrimeActionWrapper(__result);
            }
            __state = false;
        }

        private static Exception Finalizer(Exception __exception, ref bool __state)
        {
            if (__state && DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.EndCrimeActionWrapper(false);
            }
            __state = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(CrimeUtils), nameof(CrimeUtils.TryCommitCrime))]
    internal static class CrimeReportAttemptPatch
    {
        private static void Prefix(out bool __state)
        {
            __state = DeedsOfAvalonPlugin.Instance != null;
            if (__state)
            {
                DeedsOfAvalonPlugin.Instance.BeginCrimeReportAttempt();
            }
        }

        private static void Postfix(ref bool __state)
        {
            if (__state && DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.EndCrimeReportAttempt();
            }
            __state = false;
        }

        private static Exception Finalizer(Exception __exception, ref bool __state)
        {
            if (__state && DeedsOfAvalonPlugin.Instance != null)
            {
                DeedsOfAvalonPlugin.Instance.EndCrimeReportAttempt();
            }
            __state = false;
            return __exception;
        }
    }
}
