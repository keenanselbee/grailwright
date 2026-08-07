using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.Factions.Crimes;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet;
using Awaken.TG.Main.Heroes.CharacterSheet.QuickUseWheels;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Discovery;
using Awaken.TG.Main.Locations.Gems.GemManagement;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Saving.Cloud.Services;
using Awaken.TG.Main.Stories.Quests;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Deeds of Avalon - Character Statistics")]
[assembly: AssemblyDescription("Save-bounded character statistics and quick-wheel presentation for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Deeds of Avalon")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: AssemblyInformationalVersion("1.0.1")]

namespace DeedsOfAvalon
{
    public static class StatisticsApi
    {
        public const int ApiVersion = 1;

        public static bool TryRecordCorpseDrain(string sourceId, string tier, float quality)
        {
            DeedsOfAvalonPlugin plugin = DeedsOfAvalonPlugin.Instance;
            return plugin != null && plugin.RecordCorpseDrain(sourceId, tier, quality);
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.glorious-ui", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DeedsOfAvalonPlugin : BaseUnityPlugin, IListenerOwner
    {
        public const string PluginGuid = "ks.tgfoa.deeds-of-avalon";
        public const string PluginName = "Deeds of Avalon - Character Statistics";
        public const string PluginVersion = "1.0.1";
        private const string MemoryContext = "DeedsOfAvalon";
        private const string GftPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string GloriousUiPluginGuid = "ks.tgfoa.glorious-ui";
        private const string BloodMagicPluginGuid = "ks.tgfoa.blood-magic-expansion";
        private const int ConfigSchemaVersion = 1;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[] ConfigRecoveryKeepCurrentDefaultRules =
            new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions = new ConfigDefinition[0];

        internal static DeedsOfAvalonPlugin Instance;
        internal static ManualLogSource Log;

        private readonly List<IEventListener> _heroListeners = new List<IEventListener>();
        private readonly HashSet<int> _visibleTooltipIds = new HashSet<int>();
        private Harmony _harmony;
        private Hero _boundHero;
        private IEventListener _questListener;
        private IEventListener _locationListener;
        private bool _globalEventsBound;
        private bool _wheelWasOpen;
        private float _nextPanelRefresh;
        private float _nextBindAttempt;
        private float _pendingLoadedExportAt = -1.0f;
        private StatisticsSnapshot _pendingSaveSnapshot;
        private CharacterPointsSnapshot _characterPointsSnapshot;

        private MethodInfo _gftTrySetMethod;
        private MethodInfo _gftSetTooltipActiveMethod;
        private MethodInfo _gftClearMethod;
        private FieldInfo _characterPointsCanvasGroupField;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _trackStatistics;
        private ConfigEntry<bool> _exportOnSuccessfulSave;
        private ConfigEntry<bool> _showQuickWheelStatistics;
        private ConfigEntry<bool> _hideItemTooltipText;
        private ConfigEntry<float> _panelOpacity;
        private ConfigEntry<float> _tooltipPanelOpacity;
        private ConfigEntry<float> _tooltipFadeSeconds;
        private ConfigEntry<float> _panelScale;
        private ConfigEntry<float> _rightOffset;
        private ConfigEntry<float> _topOffset;
        private ConfigEntry<int> _maximumDeedRows;
        private ConfigEntry<int> _maximumWeaponRows;
        private ConfigEntry<int> _maximumMagicRows;
        private ConfigEntry<bool> _showCollapsedRows;
        private ConfigEntry<bool> _hidePointsAvailable;
        private ConfigEntry<bool> _showBloodMagicStatistics;
        private ConfigEntry<bool> _diagnostics;

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
                CloseWheelPresentation();
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _nextBindAttempt)
            {
                _nextBindAttempt = now + 1.0f;
                TryBindEvents();
            }

            if (_pendingLoadedExportAt >= 0.0f && now >= _pendingLoadedExportAt)
            {
                _pendingLoadedExportAt = -1.0f;
                ExportCurrentSavedStatistics("load");
            }

            QuickUseWheelUI wheel = World.Any<QuickUseWheelUI>();
            bool wheelOpen = wheel != null && !wheel.HasBeenDiscarded;
            if (!wheelOpen)
            {
                if (_wheelWasOpen)
                {
                    CloseWheelPresentation();
                }
                return;
            }

            _wheelWasOpen = true;
            ApplyPointsAvailableVisibility();
            if (_showQuickWheelStatistics.Value && now >= _nextPanelRefresh)
            {
                _nextPanelRefresh = now + 0.2f;
                PublishPanel();
            }
            else if (!_showQuickWheelStatistics.Value)
            {
                ClearGftPanel();
            }
        }

        private void OnDestroy()
        {
            CloseWheelPresentation();
            DisposeHeroListeners();
            RemoveListener(ref _questListener);
            RemoveListener(ref _locationListener);
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
            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch for tracking, export, and quick-wheel presentation.");
            Config.Bind("1. Core", "ConfigSchemaVersion", ConfigSchemaVersion, new ConfigDescription("Configuration layout version. Do not edit manually.", null, new System.ComponentModel.BrowsableAttribute(false)));
            _trackStatistics = Config.Bind("1. Core", "TrackStatistics", true, "Record character statistics in the active save game's GameplayMemory.");
            _exportOnSuccessfulSave = Config.Bind("1. Core", "ExportOnSuccessfulSave", true, "Write the readable character file only after a save succeeds, and refresh it after loading saved data.");

            _showQuickWheelStatistics = Config.Bind("2. Quick Wheel", "ShowCharacterStatistics", true, "Show the two-column Deeds of Avalon panel while the quick wheel is open. Requires Grail Floating Text.");
            _hideItemTooltipText = Config.Bind("2. Quick Wheel", "HideItemTooltipText", false, "Hide the normal weapon and spell tooltip on the quick wheel. Disabled by default.");
            _panelOpacity = Config.Bind("2. Quick Wheel", "PanelOpacity", 1.0f, new ConfigDescription("Normal statistics-panel opacity.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _tooltipPanelOpacity = Config.Bind("2. Quick Wheel", "TooltipPanelOpacity", 0.5f, new ConfigDescription("Multiplier applied to the statistics panel while a weapon or spell tooltip is visible.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _tooltipFadeSeconds = Config.Bind("2. Quick Wheel", "TooltipFadeSeconds", 0.15f, new ConfigDescription("Seconds used to fade the statistics panel when tooltips open or close.", new AcceptableValueRange<float>(0.0f, 2.0f)));
            _panelScale = Config.Bind("2. Quick Wheel", "PanelScale", 1.0f, new ConfigDescription("Statistics panel scale.", new AcceptableValueRange<float>(0.5f, 2.0f)));
            _rightOffset = Config.Bind("2. Quick Wheel", "RightOffset", 48.0f, new ConfigDescription("Distance in pixels from the right edge.", new AcceptableValueRange<float>(0.0f, 800.0f)));
            _topOffset = Config.Bind("2. Quick Wheel", "TopOffset", 145.0f, new ConfigDescription("Distance in pixels from the top edge.", new AcceptableValueRange<float>(0.0f, 600.0f)));
            _maximumDeedRows = Config.Bind("2. Quick Wheel", "MaximumDeedRows", 9, new ConfigDescription("Maximum non-XP rows in the left column.", new AcceptableValueRange<int>(1, 16)));
            _maximumWeaponRows = Config.Bind("2. Quick Wheel", "MaximumWeaponRows", 7, new ConfigDescription("Maximum weapon-category rows in Foes Defeated.", new AcceptableValueRange<int>(1, 14)));
            _maximumMagicRows = Config.Bind("2. Quick Wheel", "MaximumMagicRows", 5, new ConfigDescription("Maximum magic-category rows in Foes Defeated.", new AcceptableValueRange<int>(1, 10)));
            _showCollapsedRows = Config.Bind("2. Quick Wheel", "ShowCollapsedOtherRows", true, "Combine positive categories beyond a column limit into an Other row.");
            _hidePointsAvailable = Config.Bind("2. Quick Wheel", "HidePointsAvailable", true, "Hide the top-right Points available widget only while the quick wheel is open. Defers to Glorious UI when Glorious UI owns this behavior.");
            _showBloodMagicStatistics = Config.Bind("3. Integrations", "ShowBloodMagicStatistics", true, "Show Corpses Drained totals reported by Blood Magic Expansion.");
            _diagnostics = Config.Bind("4. Diagnostics", "Diagnostics", false, "Log event binding, panel integration, save export, and compatibility details.");
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
                if (string.Equals(section, "1. Core", StringComparison.Ordinal) && line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring(prefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out stored);
                    break;
                }
            }
            if (stored == ConfigSchemaVersion)
            {
                return;
            }

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
                if (File.Exists(backup))
                {
                    File.Copy(backup, path, true);
                    Config.Clear();
                    Config.Reload();
                }
                throw;
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
                    _globalEventsBound = true;
                }
                catch (Exception ex)
                {
                    RemoveListener(ref _questListener);
                    RemoveListener(ref _locationListener);
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
                _heroListeners.Add(ModelExtensions.ListenTo(hero, CrimeUtils.Events.CrimeCommitted, OnCrimeCommitted, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, CrimeUtils.Events.BountyClearedFor, OnBountyCleared, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, HeroWyrdNight.Events.WyrdNightChanged, OnWyrdNightChanged, this));
                _heroListeners.Add(ModelExtensions.ListenTo(hero, Hero.Events.AfterHeroRested, OnHeroRested, this));
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
            if (!ShouldTrack() || !(outcome.TargetPure is NpcElement))
            {
                return;
            }
            ContextualFacts facts = Facts();
            Increment(facts, "foes.total");
            string category = ClassifyKill(outcome.Damage, facts);
            Increment(facts, category);
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

        private static string ClassifyMagic(Damage damage, ContextualFacts facts)
        {
            string spellName = ResolveSpellName(damage);
            if (!string.IsNullOrEmpty(spellName))
            {
                string spellKey = SafeKey(spellName);
                facts.Set("display.magic." + spellKey, spellName);
                return "foes.magic.spell." + spellKey;
            }
            return "foes.magic.damage." + ResolveMagicType(damage);
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
            ContextualFacts facts = Facts();
            Increment(facts, "deeds.times_rested");
            facts.Set("deeds.minutes_rested", facts.Get("deeds.minutes_rested", 0) + Math.Max(0, minutes));
        }

        private void OnWyrdNightChanged(bool isNight)
        {
            if (!ShouldTrack()) return;
            ContextualFacts facts = Facts();
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
            ContextualFacts facts = Facts();
            Increment(facts, "deeds.crimes_committed");
            UpdateFactionBounty(facts, data.Faction, CrimeUtils.Bounty(data.Faction));
        }

        private void OnBountyCleared(CrimeOwnerTemplate faction)
        {
            if (ShouldTrack()) UpdateFactionBounty(Facts(), faction, 0.0f);
        }

        private static void UpdateFactionBounty(ContextualFacts facts, CrimeOwnerTemplate faction, float current)
        {
            if (facts == null || faction == null) return;
            string factionKey = "bounty.faction." + SafeKey(faction.name);
            float previous = facts.Get(factionKey, 0.0f);
            float total = Math.Max(0.0f, facts.Get("bounty.current", 0.0f) + current - previous);
            facts.Set(factionKey, current);
            facts.Set("bounty.current", total);
            facts.Set("bounty.highest", Math.Max(total, facts.Get("bounty.highest", 0.0f)));
        }

        internal bool RecordCorpseDrain(string sourceId, string tier, float quality)
        {
            if (!ShouldTrack() || !string.Equals(sourceId, BloodMagicPluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string normalized = NormalizeCorpseTier(tier, quality);
            ContextualFacts facts = Facts();
            Increment(facts, "blood.corpses_drained.total");
            Increment(facts, "blood.corpses_drained." + normalized);
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
            GameplayMemory memory = World.Services.Get<GameplayMemory>();
            return memory == null ? null : memory.Context(MemoryContext);
        }

        private static void Increment(ContextualFacts facts, string key)
        {
            if (facts != null) facts.Set(key, facts.Get(key, 0) + 1);
        }

        private void PublishPanel()
        {
            if (!ResolveGftApi()) return;
            ContextualFacts facts = Facts();
            Hero hero = Hero.Current;
            if (facts == null || hero == null) return;

            List<PanelRow> deeds = BuildDeedRows(facts);
            LimitDeedRows(deeds, _maximumDeedRows.Value);
            List<PanelRow> weaponRows = BuildRows(facts, WeaponCategories());
            LimitCountRows(weaponRows, _maximumWeaponRows.Value, "Other weapons", "combat", "one_handed");
            List<PanelRow> magicRows = BuildMagicRows(facts);
            LimitCountRows(magicRows, _maximumMagicRows.Value, "Other magic", "wyrd", "magic");
            List<PanelRow> foes = new List<PanelRow>();
            foes.AddRange(weaponRows);
            foes.AddRange(magicRows);

            string leftSubtitle = "Level " + hero.Level.ModifiedInt.ToString(CultureInfo.InvariantCulture)
                + "   " + hero.HeroStats.XP.ModifiedInt.ToString("N0", CultureInfo.InvariantCulture)
                + " / " + hero.HeroStats.XPForNextLevel.ModifiedInt.ToString("N0", CultureInfo.InvariantCulture) + " XP";
            string rightSubtitle = "Total " + facts.Get("foes.total", 0).ToString("N0", CultureInfo.InvariantCulture);
            object[] args =
            {
                PluginGuid,
                "DEEDS OF AVALON",
                leftSubtitle,
                Texts(deeds), Icons(deeds), Styles(deeds),
                "FOES DEFEATED",
                rightSubtitle,
                Texts(foes), Icons(foes), Styles(foes),
                _panelOpacity.Value,
                _tooltipPanelOpacity.Value,
                _tooltipFadeSeconds.Value,
                _rightOffset.Value,
                _topOffset.Value,
                _panelScale.Value
            };
            try
            {
                _gftTrySetMethod.Invoke(null, args);
                SetGftTooltipActive(_visibleTooltipIds.Count > 0 && !_hideItemTooltipText.Value);
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
            AddRow(rows, facts.Get("deeds.wyrdnights_survived", 0), "Wyrdnights survived", "wyrd", "wyrd");
            AddRow(rows, facts.Get("deeds.quests_completed", 0), "Quests completed", "reward", "reward");
            AddRow(rows, facts.Get("deeds.locations_discovered", 0), "Locations discovered", "location", "Discovery");
            AddRow(rows, facts.Get("deeds.crimes_committed", 0), "Crimes committed", "crime", "Warning");
            AddRow(rows, Mathf.RoundToInt(facts.Get("bounty.current", 0.0f)), "Recorded active bounty", "crime", "Warning");
            AddRow(rows, Mathf.RoundToInt(facts.Get("bounty.highest", 0.0f)), "Highest recorded bounty", "crime", "Critical");
            AddRow(rows, facts.Get("deeds.times_rested", 0), "Times rested", "rest", "System");
            int minutes = facts.Get("deeds.minutes_rested", 0);
            if (minutes > 0) rows.Add(new PanelRow("Hours rested  " + (minutes / 60.0f).ToString("0.#", CultureInfo.InvariantCulture), "rest", "System", minutes));
            if (_showBloodMagicStatistics.Value)
            {
                AddRow(rows, facts.Get("blood.corpses_drained.total", 0), "Corpses Drained", "corpse", "Status");
                AddRow(rows, facts.Get("blood.corpses_drained.meager", 0), "Meager corpses", "corpse", "Status");
                AddRow(rows, facts.Get("blood.corpses_drained.worthy", 0), "Worthy corpses", "corpse", "Status");
                AddRow(rows, facts.Get("blood.corpses_drained.potent", 0), "Potent corpses", "corpse", "Wyrd");
                AddRow(rows, facts.Get("blood.corpses_drained.prime", 0), "Prime corpses", "corpse", "Critical");
            }
            return rows;
        }

        private static List<PanelRow> BuildRows(ContextualFacts facts, Category[] categories)
        {
            List<PanelRow> rows = new List<PanelRow>();
            for (int i = 0; i < categories.Length; i++)
            {
                Category category = categories[i];
                AddRow(rows, facts.Get(category.Key, 0), category.Label, category.Icon, category.Style);
            }
            return rows;
        }

        private static List<PanelRow> BuildMagicRows(ContextualFacts facts)
        {
            List<PanelRow> rows = new List<PanelRow>();
            const string prefix = "foes.magic.spell.";
            foreach (KeyValuePair<string, object> entry in facts.GetAll())
            {
                if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal) || !(entry.Value is int) || (int)entry.Value <= 0) continue;
                string key = entry.Key.Substring(prefix.Length);
                string name = facts.Get("display.magic." + key, key.Replace('_', ' '));
                rows.Add(new PanelRow(name + "  " + ((int)entry.Value).ToString("N0", CultureInfo.InvariantCulture), "magic", "Wyrd", (int)entry.Value));
            }
            rows.Sort((left, right) =>
            {
                int byValue = right.Value.CompareTo(left.Value);
                return byValue != 0 ? byValue : string.Compare(left.Text, right.Text, StringComparison.OrdinalIgnoreCase);
            });
            rows.AddRange(BuildRows(facts, MagicCategories()));
            return rows;
        }

        private void LimitDeedRows(List<PanelRow> rows, int maximum)
        {
            maximum = Math.Max(1, maximum);
            if (rows.Count <= maximum) return;
            int hidden = rows.Count - maximum + (_showCollapsedRows.Value ? 1 : 0);
            rows.RemoveRange(maximum - (_showCollapsedRows.Value ? 1 : 0), hidden);
            if (_showCollapsedRows.Value) rows.Add(new PanelRow("+ " + hidden + " more recorded deeds", "general", "System", hidden));
        }

        private void LimitCountRows(List<PanelRow> rows, int maximum, string label, string style, string icon)
        {
            maximum = Math.Max(1, maximum);
            if (rows.Count <= maximum) return;
            int keep = maximum - (_showCollapsedRows.Value ? 1 : 0);
            int other = 0;
            for (int i = keep; i < rows.Count; i++) other += rows[i].Value;
            rows.RemoveRange(keep, rows.Count - keep);
            if (_showCollapsedRows.Value && other > 0) rows.Add(new PanelRow(label + "  " + other.ToString("N0", CultureInfo.InvariantCulture), icon, style, other));
        }

        private static void AddRow(List<PanelRow> rows, int value, string label, string icon, string style)
        {
            if (value > 0) rows.Add(new PanelRow(label + "  " + value.ToString("N0", CultureInfo.InvariantCulture), icon, style, value));
        }

        private static Category[] WeaponCategories()
        {
            return new[]
            {
                new Category("foes.weapon.one_handed_sword", "One-handed sword", "one_handed", "Combat"),
                new Category("foes.weapon.one_handed_axe", "One-handed axe", "one_handed", "Combat"),
                new Category("foes.weapon.one_handed_blunt", "One-handed blunt", "one_handed", "Combat"),
                new Category("foes.weapon.one_handed_dagger", "Dagger", "one_handed", "Combat"),
                new Category("foes.weapon.two_handed_sword", "Two-handed sword", "two_handed", "Combat"),
                new Category("foes.weapon.two_handed_axe", "Two-handed axe", "two_handed", "Combat"),
                new Category("foes.weapon.two_handed_blunt", "Two-handed blunt", "two_handed", "Combat"),
                new Category("foes.weapon.two_handed_polearm", "Polearm", "two_handed", "Combat"),
                new Category("foes.weapon.short_bow", "Short bow", "archery", "Combat"),
                new Category("foes.weapon.long_bow", "Long bow", "archery", "Combat"),
                new Category("foes.weapon.heavy_bow", "Heavy bow", "archery", "Combat"),
                new Category("foes.weapon.shield", "Shield", "shield", "Combat"),
                new Category("foes.weapon.unarmed", "Unarmed", "unarmed", "Combat"),
                new Category("foes.weapon.throwable", "Throwable", "combat", "Combat"),
                new Category("foes.weapon.one_handed_other", "Other one-handed", "one_handed", "Combat"),
                new Category("foes.weapon.two_handed_other", "Other two-handed", "two_handed", "Combat"),
                new Category("foes.weapon.ranged", "Other ranged", "archery", "Combat"),
                new Category("foes.weapon.other", "Other weapon", "combat", "Combat")
            };
        }

        private static Category[] MagicCategories()
        {
            return new[]
            {
                new Category("foes.magic.damage.fire", "Fire damage", "magic", "Critical"),
                new Category("foes.magic.damage.cold", "Cold damage", "magic", "Status"),
                new Category("foes.magic.damage.poison", "Poison damage", "magic", "Status"),
                new Category("foes.magic.damage.electric", "Electric damage", "magic", "Warning"),
                new Category("foes.magic.damage.wyrdness", "Wyrd damage", "magic", "Wyrd"),
                new Category("foes.magic.damage.pure", "Pure magic", "magic", "Pale"),
                new Category("foes.magic.damage.wet", "Wet damage", "magic", "Status"),
                new Category("foes.magic.damage.other", "Other magic", "magic", "Wyrd")
            };
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
            _gftTrySetMethod = api.GetMethod("TrySet", BindingFlags.Public | BindingFlags.Static);
            _gftSetTooltipActiveMethod = api.GetMethod("SetTooltipActive", BindingFlags.Public | BindingFlags.Static);
            _gftClearMethod = api.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
            return _gftTrySetMethod != null;
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
            if (ResolveGftApi() && _gftClearMethod != null)
            {
                try { _gftClearMethod.Invoke(null, new object[] { PluginGuid }); } catch { }
            }
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
                VCCharacterPointsAvailable[] views = Resources.FindObjectsOfTypeAll<VCCharacterPointsAvailable>();
                CanvasGroup selectedGroup = null;
                for (int i = 0; i < views.Length; i++)
                {
                    VCCharacterPointsAvailable view = views[i];
                    if (view == null || !view.gameObject.scene.IsValid()) continue;
                    CanvasGroup group = _characterPointsCanvasGroupField == null ? null : _characterPointsCanvasGroupField.GetValue(view) as CanvasGroup;
                    if (group == null) continue;
                    if (selectedGroup == null) selectedGroup = group;
                    if (!group.gameObject.activeInHierarchy) continue;
                    selectedGroup = group;
                    break;
                }
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
            _visibleTooltipIds.Clear();
            RestoreCharacterPoints();
            ClearGftPanel();
        }

        internal void CapturePendingSaveSnapshot()
        {
            if (_exportOnSuccessfulSave != null && _exportOnSuccessfulSave.Value)
            {
                _pendingSaveSnapshot = CreateStatisticsSnapshot();
            }
        }

        internal void PublishSuccessfulSaveSnapshot()
        {
            StatisticsSnapshot snapshot = _pendingSaveSnapshot;
            _pendingSaveSnapshot = null;
            if (snapshot != null) WriteSnapshot(snapshot, "successful save");
        }

        internal void DiscardFailedSaveSnapshot()
        {
            _pendingSaveSnapshot = null;
        }

        internal void ScheduleLoadedStatisticsExport()
        {
            if (_exportOnSuccessfulSave != null && _exportOnSuccessfulSave.Value)
            {
                _pendingLoadedExportAt = Time.unscaledTime + 1.0f;
            }
        }

        private void ExportCurrentSavedStatistics(string reason)
        {
            StatisticsSnapshot snapshot = CreateStatisticsSnapshot();
            if (snapshot != null) WriteSnapshot(snapshot, reason);
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
                if (!entry.Key.StartsWith("state.", StringComparison.Ordinal)) entries.Add(entry);
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
        private static void Postfix() { if (DeedsOfAvalonPlugin.Instance != null) DeedsOfAvalonPlugin.Instance.PublishSuccessfulSaveSnapshot(); }
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

    [HarmonyPatch(typeof(VCQuickItemTooltipUI), nameof(VCQuickItemTooltipUI.ShowItem))]
    internal static class QuickItemTooltipShowPatch
    {
        private static bool Prefix(VCQuickItemTooltipUI __instance)
        {
            return DeedsOfAvalonPlugin.Instance == null || DeedsOfAvalonPlugin.Instance.BeforeTooltipShown(__instance);
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
}
