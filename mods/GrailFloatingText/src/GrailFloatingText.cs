using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Events;
using Awaken.TG.Graphics.Cutscenes;
using Awaken.TG.Graphics.Transitions;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.Factions.Crimes;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Development;
using Awaken.TG.Main.Heroes.Development.WyrdPowers;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Resting;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.Scenes.SceneConstructors;
using Awaken.TG.Main.Settings.Accessibility;
using Awaken.TG.Main.UI.TitleScreen;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using Awaken.TG.Main.Utility.Video;
using Awaken.Utility;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

[assembly: AssemblyTitle("Grail Floating Text")]
[assembly: AssemblyDescription("Shared floating text overlay any Tainted Grail mod author can use")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Grail Floating Text")]
[assembly: AssemblyVersion("1.11.1.0")]
[assembly: AssemblyFileVersion("1.11.1.0")]
[assembly: AssemblyInformationalVersion("1.11.1")]

namespace GrailFloatingText
{
    public static class NotificationApi
    {
        public const int ApiVersion = 9;

        public static bool SupportsFeature(string feature)
        {
            return GrailFloatingTextPlugin.SupportsFeature(feature);
        }

        public static bool TryShow(
            string sourceId,
            string text,
            string style,
            float durationSeconds,
            float fadeSeconds,
            float opacity)
        {
            return TryShow(sourceId, text, style, "General", "Normal", string.Empty, durationSeconds, fadeSeconds, opacity);
        }

        public static bool TryShow(
            string sourceId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            float durationSeconds,
            float fadeSeconds,
            float opacity)
        {
            return TryShow(sourceId, text, style, category, priority, collapseKey, string.Empty, durationSeconds, fadeSeconds, opacity);
        }

        public static bool TryShow(
            string sourceId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            float durationSeconds,
            float fadeSeconds,
            float opacity)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.TryShow(sourceId, text, style, category, priority, collapseKey, iconId, durationSeconds, fadeSeconds, opacity);
        }

        public static bool TryShowEvent(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string durationBucket,
            float fadeSeconds,
            float opacity)
        {
            return TryShowEvent(sourceId, eventId, text, style, category, priority, collapseKey, string.Empty, durationBucket, fadeSeconds, opacity);
        }

        public static bool TryShowEvent(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            string durationBucket,
            float fadeSeconds,
            float opacity)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.TryShowEvent(sourceId, eventId, text, style, category, priority, collapseKey, iconId, durationBucket, fadeSeconds, opacity);
        }

        public static bool TryShowEvent(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            string durationBucket,
            string deliveryPoint,
            float fadeSeconds,
            float opacity)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.TryShowEvent(
                sourceId,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                durationBucket,
                deliveryPoint,
                fadeSeconds,
                opacity);
        }

        public static bool TryClaimXpGain(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string iconId,
            string durationBucket,
            float expectedAmount,
            float fadeSeconds,
            float opacity)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.TryClaimXpGain(sourceId, eventId, text, style, category, priority, iconId, durationBucket, expectedAmount, fadeSeconds, opacity);
        }

        public static bool TryClaimConsolidatedXpGain(
            string sourceId,
            string eventId,
            string consolidationKey,
            string textFormat,
            string style,
            string category,
            string priority,
            string iconId,
            string durationBucket,
            float expectedAmount,
            float fadeSeconds,
            float opacity)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.TryClaimConsolidatedXpGain(
                sourceId,
                eventId,
                consolidationKey,
                textFormat,
                style,
                category,
                priority,
                iconId,
                durationBucket,
                expectedAmount,
                fadeSeconds,
                opacity);
        }

        public static string[] GetBuiltInIconIds()
        {
            return GrailFloatingTextPlugin.GetBuiltInIconIds();
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed partial class GrailFloatingTextPlugin : BaseUnityPlugin, IListenerOwner
    {
        public const string PluginGuid = "ks.tgfoa.grail-floating-text";
        public const string PluginName = "Grail Floating Text";
        public const string PluginVersion = "1.11.1";

        private const string WyrdHuntAddonPluginGuid = "ks.tgfoa.wyrd-hunt-addon";
        private const string GloriousUiPluginGuid = "ks.tgfoa.glorious-ui";
        private const string GloriousUiAssemblyName = "GloriousUI";
        private const string EyesInTheDarkPluginGuid =
            "ks.tgfoa.eyes-in-the-dark";
        private const string EyesInTheDarkAssemblyName =
            "EyesInTheDark";
        private const string WyrdHuntPluginGuid =
            "kane.tgfoa.wyrd-hunt";
        private const string WyrdHuntAssemblyName = "WyrdHunt";
        private const string CustomTimescalePluginGuid =
            "DeathWrench.TimeMod";
        private const string CustomTimescaleAssemblyName = "TimeMod";
        private const string BetterUiPluginGuid = "Better_UI";
        private const string ImmersiveHudPluginGuid = "kane.tgfoa.always-show-hud";
        private const string SteelAndBonePluginGuid = "ks.tgfoa.steel-and-bone";
        private const string SteelAndBoneAssemblyName = "SteelAndBone";
        private const string DynamicCrosshairPluginGuid =
            "ks.tgfoa.dishonored-dynamic-crosshair";
        private const string DynamicCrosshairAssemblyName =
            "DishonoredDynamicCrosshair";
        private const int ConfigSchemaVersion = 24;
        private const int ConfigRecoveryBaselineSchema = 15;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const float DefaultMinimumDurationSeconds = 0.05f;
        private const float DefaultVeryShortDurationSeconds = 3.0f;
        private const float DefaultShortDurationSeconds = 3.5f;
        private const float DefaultMediumDurationSeconds = 4.0f;
        private const float DefaultLongDurationSeconds = 4.5f;
        private const float DefaultVeryLongDurationSeconds = 5.0f;
        private const float DefaultSystemDurationSeconds = 10.0f;
        private const float XpClaimLifetimeSeconds = 2.0f;
        private const float XpClaimImmediateFallbackSeconds = 0.25f;
        private const float XpClaimAmountTolerance = 0.01f;
        private const float DirectXpDuplicateSuppressSeconds = 0.05f;
        private const int MaximumDeferredNotifications = 64;
        private const int MaximumNotificationLayoutCount = 24;
        private const int DeferredNotificationStoreVersion = 1;
        private const int DeferredNotificationMaximumAgeDays = 30;
        private const string DefaultXpGainEventId = "default-xp-gain";
        private const string KillingBlowEventId = "killing-blow";
        private const string ConfigResetEventId = "config-reset";
        private const string LoadTimeErrorEventId = "load-time-error";
        private const string ModCompatibilityEventIdPrefix =
            "mod-compatibility-";
        private const int PriorityLow = 0;
        private const int PriorityNormal = 100;
        private const int PriorityHigh = 200;
        private const int PriorityCritical = 300;
        private static readonly string[] BuiltInIconIds = new[]
        {
            "general",
            "system",
            "status",
            "wyrd",
            "reward",
            "combat",
            "warning",
            "critical",
            "debug",
            "rest",
            "location",
            "one_handed",
            "two_handed",
            "archery",
            "shield",
            "parry",
            "unarmed",
            "magic",
            "crime",
            "pickpocket",
            "weight",
            "experience",
            "corpse"
        };

        private static readonly string[] GloriousUiIncompatibleAssemblyNames =
        {
            "owrocc.ModifyHeroHUD",
            "owrocc.ModifyQuickSlotsHud",
            "owrocc.HideLevelUp",
            "owrocc.MoreWeaponSlots",
            "owrocc.OneMenuEquip",
            "owrocc.RebindQuickWheel",
            "owrocc.BagHotkeys",
            "BetterQuickSlots"
        };

        private static readonly HashSet<string> BuiltInIconIdSet =
            new HashSet<string>(BuiltInIconIds, StringComparer.OrdinalIgnoreCase);

        private static MethodInfo _eventSystemPayloadListenToMethod;

        private enum DurationBucket
        {
            VeryShort,
            Short,
            Medium,
            Long,
            VeryLong,
            System
        }

        private enum FontMode
        {
            GameDefault,
            Sans,
            Serif,
            ImguiDefault
        }

        private enum DeliveryPoint
        {
            Immediate,
            OnMainMenu,
            OnLoad
        }

        internal static GrailFloatingTextPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private readonly List<NotificationEntry> _notifications = new List<NotificationEntry>();
        private readonly List<DeferredNotificationEntry> _deferredNotifications =
            new List<DeferredNotificationEntry>();
        private readonly List<XpDisplayClaim> _xpDisplayClaims = new List<XpDisplayClaim>();
        private readonly List<XpNotificationBatch> _pendingXpBatches = new List<XpNotificationBatch>();

        private Harmony _harmony;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _scale;
        private ConfigEntry<int> _fontSize;
        private ConfigEntry<FontMode> _fontMode;
        private ConfigEntry<float> _centerX;
        private ConfigEntry<float> _baseCenterY;
        private ConfigEntry<float> _width;
        private ConfigEntry<float> _stackSpacing;
        private ConfigEntry<float> _defaultDurationSeconds;
        private ConfigEntry<float> _defaultFadeSeconds;
        private ConfigEntry<float> _veryShortDurationSeconds;
        private ConfigEntry<float> _shortDurationSeconds;
        private ConfigEntry<float> _mediumDurationSeconds;
        private ConfigEntry<float> _longDurationSeconds;
        private ConfigEntry<float> _veryLongDurationSeconds;
        private ConfigEntry<float> _systemDurationSeconds;
        private ConfigEntry<float> _globalOpacity;
        private ConfigEntry<bool> _spawnAnimationEnabled;
        private ConfigEntry<float> _spawnStartScale;
        private ConfigEntry<float> _spawnOvershootScale;
        private ConfigEntry<float> _spawnAnimationSeconds;
        private ConfigEntry<float> _stackMoveAnimationSeconds;
        private ConfigEntry<int> _maximumVisibleNotifications;
        private ConfigEntry<float> _duplicateSuppressSeconds;
        private ConfigEntry<bool> _iconsEnabled;
        private ConfigEntry<float> _iconSize;
        private ConfigEntry<float> _iconGap;
        private ConfigEntry<float> _iconOpacity;
        private ConfigEntry<bool> _iconShadowEnabled;
        private ConfigEntry<float> _iconShadowOpacity;
        private ConfigEntry<bool> _perSourceControlsEnabled;
        private ConfigEntry<float> _defaultSourceThrottleSeconds;
        private ConfigEntry<float> _defaultSourceDurationMultiplier;
        private ConfigEntry<bool> _notifyModCompatibility;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _notifyRestDuration;
        private ConfigEntry<bool> _notifyInterruptedRestDuration;
        private ConfigEntry<string> _restDurationTextFormat;
        private ConfigEntry<string> _restInterruptedTextFormat;
        private ConfigEntry<int> _restNotificationMinimumMinutes;
        private ConfigEntry<bool> _notifyBlockedDamage;
        private ConfigEntry<bool> _notifyParriedDamage;
        private ConfigEntry<float> _combatDefenseMinimumDamage;
        private ConfigEntry<float> _combatDefenseCooldownSeconds;
        private ConfigEntry<bool> _notifyEncumbranceChanged;
        private ConfigEntry<bool> _notifyLocationCleared;
        private ConfigEntry<bool> _notifyPickpocketSuccess;
        private ConfigEntry<bool> _notifyPickpocketFail;
        private ConfigEntry<bool> _notifyBountyChanged;
        private ConfigEntry<bool> _notifyBountyCleared;
        private ConfigEntry<bool> _notifyUnforgivableCrime;
        private ConfigEntry<float> _crimeEventCooldownSeconds;
        private ConfigEntry<bool> _notifyWeakspotHit;
        private ConfigEntry<bool> _notifySneakAttack;
        private ConfigEntry<float> _combatHitMinimumDamage;
        private ConfigEntry<float> _combatHitCooldownSeconds;
        private ConfigEntry<bool> _vanillaWyrdEventsEnabled;
        private ConfigEntry<bool> _notifyWyrdNightChange;
        private ConfigEntry<bool> _notifyWyrdSafetyChange;
        private ConfigEntry<bool> _suppressWyrdSafetyWhenWyrdHuntAddonLoaded;
        private ConfigEntry<bool> _notifyWyrdSoulFragmentCollected;
        private ConfigEntry<bool> _notifyWyrdSkillToggle;
        private ConfigEntry<float> _vanillaWyrdEventCooldownSeconds;
        private ConfigEntry<bool> _notifyXpGained;
        private ConfigEntry<bool> _suppressVanillaXpNotifications;
        private ConfigEntry<string> _xpTextFormat;
        private ConfigEntry<string> _xpDurationBucket;
        private ConfigEntry<bool> _consolidateXpGains;
        private bool _showConfigResetNotification;
        private int _previousConfigSchemaVersion;

        private readonly Dictionary<string, ConfigEntry<bool>> _categoryEnabledByName =
            new Dictionary<string, ConfigEntry<bool>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, SourceSettings> _sourceSettingsById =
            new Dictionary<string, SourceSettings>(StringComparer.OrdinalIgnoreCase);

        private readonly List<ColorGroupSettings> _colorGroups = new List<ColorGroupSettings>();

        private readonly Dictionary<string, ColorGroupSettings> _colorGroupByName =
            new Dictionary<string, ColorGroupSettings>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _invalidIconColorWarnings =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, object> _pendingPreservedPresentation =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingPreservedSourceSections =
            new HashSet<string>(StringComparer.Ordinal);
        private int _pendingPreservedInvalidValueCount;

        private readonly Dictionary<string, float> _lastNotificationTimeBySource =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Texture2D> _iconTexturesById =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly NotificationLayout[] _notificationLayouts =
            new NotificationLayout[MaximumNotificationLayoutCount];
        private readonly float[] _notificationTargetCenterYs =
            new float[MaximumNotificationLayoutCount];

        private readonly NotificationView[] _notificationViews =
            new NotificationView[MaximumNotificationLayoutCount];
        private RectTransform _overlayRoot;
        private FontAsset _imguiDefaultFontAsset;
        private string _lastFontDiagnosticKey = string.Empty;
        private Coroutine _defaultGameEventBindingCoroutine;
        private IEventListener _restingInitiatedListener;
        private IEventListener _restingInterruptedListener;
        private IEventListener _encumberedChangedListener;
        private IEventListener _locationClearedListener;
        private IEventListener _wyrdNightChangedListener;
        private IEventListener _wyrdStatusChangedListener;
        private IEventListener _wyrdSoulFragmentCollectedListener;
        private IEventListener _wyrdSkillToggledListener;
        private IEventListener _blockedDamageListener;
        private IEventListener _parriedDamageListener;
        private IEventListener _pickpocketSuccessListener;
        private IEventListener _pickpocketFailListener;
        private IEventListener _crimeCommittedListener;
        private IEventListener _unforgivableCrimeListener;
        private IEventListener _bountyClearedListener;
        private IEventListener _weakspotHitListener;
        private IEventListener _sneakAttackListener;
        private Hero _vanillaWyrdHero;
        private float _lastWyrdNightNotificationTime = -9999.0f;
        private readonly Dictionary<string, float> _lastDefaultGameEventTimeByKey =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private bool _defaultGameEventBindingFailureLogged;
        private bool _lastRestWasInterrupted;
        private float _lastRestInterruptedTime = -9999.0f;
        private bool _hasObservedWyrdNight;
        private bool _lastObservedWyrdNight;
        private bool _hasObservedWyrdSafety;
        private bool _lastObservedWyrdSafety;
        private long _nextSequence;
        private long _nextXpClaimSequence;
        private long _nextXpEntrySequence;
        private NotificationEntry _activeXpNotification;
        private bool _passThroughNextXpFloatAnnounce;
        private float _passThroughNextXpFloatAmount;
        private float _passThroughNextXpFloatTime = -9999.0f;
        private float _lastHandledXpAmount;
        private float _lastHandledXpTime = -9999.0f;
        private bool _modCompatibilityScanCompleted;

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
                ResetConfigIfSchemaChanged();
                BindConfig();
                RestorePreservedPresentation();
                Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                    Config,
                    Logger,
                    PluginName,
                    ConfigSchemaVersion,
                    ConfigRecoveryBaselineSchema,
                    ConfigRecoveryKeepCurrentDefaultRules,
                    ConfigRecoveryPermanentExclusions);
                LoadDeferredNotifications();
                LoadIconTextures();
                ShowPendingConfigResetNotification();
                PatchXpNotifications();
                StartDefaultGameEventBinding();
                Config.Save();
                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
            }
            catch (Exception ex)
            {
                Logger.LogError(PluginName + " " + PluginVersion + " failed during startup: " + ex.GetBaseException().Message);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, ex);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _colorGroups.Count; i++)
            {
                ColorGroupSettings group = _colorGroups[i];
                if (group != null && group.Color != null)
                {
                    group.Color.SettingChanged -= OnColorSettingChanged;
                }

                if (group != null && group.IconColor != null)
                {
                    group.IconColor.SettingChanged -= OnIconColorSettingChanged;
                }
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            StopDefaultGameEventBinding();
            ReleaseIconTextures();
            ReleaseNotificationViews();

            if (_imguiDefaultFontAsset != null)
            {
                Destroy(_imguiDefaultFontAsset);
                _imguiDefaultFontAsset = null;
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        internal static bool SupportsFeature(string feature)
        {
            if (string.IsNullOrWhiteSpace(feature))
            {
                return false;
            }

            return string.Equals(feature, "ApiVersion2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ApiVersion3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ApiVersion4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ApiVersion6", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ApiVersion7", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ApiVersion8", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ApiVersion9", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "QuickWheelPanels", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "quick-wheel-panels-v1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "Categories", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "Priority", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "CollapseKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "EventIds", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "DurationBuckets", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "SystemDuration", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "DeferredDelivery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "OnMainMenuDelivery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "OnLoadDelivery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ColorGroups", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "XpGainClaims", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "XpConsolidation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "XpNotifications", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "Icons", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "BuiltInIcons", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "Deduplication", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "SourceControls", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "DefaultGameEvents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "RestEvents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "CombatDefenseEvents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "EncumbranceEvents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "LocationClearEvents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "CrimeEvents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "CombatHitEvents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "VanillaWyrdEvents", StringComparison.OrdinalIgnoreCase);
        }

        internal static string[] GetBuiltInIconIds()
        {
            string[] copy = new string[BuiltInIconIds.Length];
            Array.Copy(BuiltInIconIds, copy, BuiltInIconIds.Length);
            return copy;
        }

        private void PatchXpNotifications()
        {
            try
            {
                _harmony = new Harmony(PluginGuid);

                MethodInfo hookOriginal = AccessTools.Method(
                    typeof(HeroDevelopment),
                    "AnnounceXPChanged",
                    new[] { typeof(HookResult<IWithStats, Stat.StatChange>) });
                MethodInfo hookPrefix = AccessTools.Method(
                    typeof(HeroDevelopmentXpHookPatch),
                    nameof(HeroDevelopmentXpHookPatch.Prefix));

                MethodInfo floatOriginal = AccessTools.Method(
                    typeof(HeroDevelopment),
                    "AnnounceXPChanged",
                    new[] { typeof(float) });
                MethodInfo floatPrefix = AccessTools.Method(
                    typeof(HeroDevelopmentXpFloatPatch),
                    nameof(HeroDevelopmentXpFloatPatch.Prefix));

                if (hookOriginal == null || hookPrefix == null || floatOriginal == null || floatPrefix == null)
                {
                    Logger.LogWarning(PluginName + " could not patch vanilla XP notifications. XP takeover is unavailable.");
                    return;
                }

                _harmony.Patch(hookOriginal, new HarmonyMethod(hookPrefix), null);
                _harmony.Patch(floatOriginal, new HarmonyMethod(floatPrefix), null);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(PluginName + " could not patch vanilla XP notifications: " + exception.GetBaseException().Message);
            }
        }

        private void StartDefaultGameEventBinding()
        {
            if (!HasAnyDefaultGameEventsEnabled())
            {
                return;
            }

            if (_defaultGameEventBindingCoroutine == null)
            {
                _defaultGameEventBindingCoroutine = StartCoroutine(BindDefaultGameEventsWhenReady());
            }
        }

        private IEnumerator BindDefaultGameEventsWhenReady()
        {
            WaitForSeconds retryDelay = new WaitForSeconds(1.0f);

            while (this != null && enabled)
            {
                if (!HasAnyDefaultGameEventsEnabled())
                {
                    break;
                }

                try
                {
                    if (World.EventSystem != null)
                    {
                        BindDefaultGameWorldListeners();

                        Hero hero = TryGetCurrentHero();
                        if (hero != null && hero.HasBeenDiscarded)
                        {
                            hero = null;
                        }

                        if (hero == null)
                        {
                            ClearDefaultGameHero();
                        }
                        else
                        {
                            BindDefaultGameHero(hero);
                        }
                    }
                }
                catch (Exception exception)
                {
                    LogDefaultGameEventBindingFailureOnce(exception);
                }

                yield return retryDelay;
            }

            _defaultGameEventBindingCoroutine = null;
        }

        private void BindDefaultGameWorldListeners()
        {
            if (IsRestDurationNotificationEnabled())
            {
                if (_restingInitiatedListener == null)
                {
                    _restingInitiatedListener = World.EventSystem.ListenTo(
                        "*",
                        RestPopupUI.Events.RestingInitiated,
                        this,
                        OnRestingInitiated);
                }

                if (_restingInterruptedListener == null && IsInterruptedRestDurationNotificationEnabled())
                {
                    _restingInterruptedListener = World.EventSystem.ListenTo(
                        "*",
                        RestPopupUI.Events.RestingInterrupted,
                        this,
                        OnRestingInterrupted);
                }
            }

            if (_encumberedChangedListener == null && IsEncumbranceNotificationEnabled())
            {
                _encumberedChangedListener = World.EventSystem.ListenTo(
                    "*",
                    HeroEncumbered.Events.EncumberedChanged,
                    this,
                    OnEncumberedChanged);
            }

            if (_locationClearedListener == null && IsLocationClearedNotificationEnabled())
            {
                _locationClearedListener = World.EventSystem.ListenTo(
                    "*",
                    Location.Events.LocationCleared,
                    this,
                    OnLocationCleared);
            }
        }

        private void BindDefaultGameHero(Hero hero)
        {
            if (hero == null || ReferenceEquals(hero, _vanillaWyrdHero))
            {
                return;
            }

            DisposeDefaultGameHeroListeners();
            _vanillaWyrdHero = hero;

            if (IsVanillaWyrdEventsEnabled())
            {
                CaptureInitialVanillaWyrdState(hero);
                _wyrdNightChangedListener = ModelExtensions.ListenTo(hero, HeroWyrdNight.Events.WyrdNightChanged, OnWyrdNightChanged, this);
                _wyrdStatusChangedListener = ModelExtensions.ListenTo(hero, HeroWyrdNight.Events.StatusChanged, OnWyrdStatusChanged, this);
                _wyrdSoulFragmentCollectedListener = ModelExtensions.ListenTo(hero, Hero.Events.WyrdSoulFragmentCollected, OnWyrdSoulFragmentCollected, this);
                _wyrdSkillToggledListener = ModelExtensions.ListenTo(hero, Hero.Events.WyrdskillToggled, OnWyrdSkillToggled, this);
            }

            if (IsCombatDefenseNotificationEnabled())
            {
                if (_notifyBlockedDamage != null && _notifyBlockedDamage.Value)
                {
                    _blockedDamageListener = ModelExtensions.ListenTo(hero, Hero.Events.HeroBlockedDamage, OnHeroBlockedDamage, this);
                }

                if (_notifyParriedDamage != null && _notifyParriedDamage.Value)
                {
                    _parriedDamageListener = ModelExtensions.ListenTo(hero, Hero.Events.HeroParriedDamage, OnHeroParriedDamage, this);
                }
            }

            if (IsCrimeNotificationEnabled())
            {
                if (_notifyPickpocketSuccess != null && _notifyPickpocketSuccess.Value)
                {
                    _pickpocketSuccessListener = ModelExtensions.ListenTo(hero, CommitCrime.Events.PickpocketSuccess, OnPickpocketSuccess, this);
                }

                if (_notifyPickpocketFail != null && _notifyPickpocketFail.Value)
                {
                    _pickpocketFailListener = ModelExtensions.ListenTo(hero, CommitCrime.Events.PickpocketFail, OnPickpocketFail, this);
                }

                if (_notifyBountyChanged != null && _notifyBountyChanged.Value)
                {
                    _crimeCommittedListener = ModelExtensions.ListenTo(hero, CrimeUtils.Events.CrimeCommitted, OnCrimeCommitted, this);
                }

                if (_notifyUnforgivableCrime != null && _notifyUnforgivableCrime.Value)
                {
                    _unforgivableCrimeListener = ModelExtensions.ListenTo(hero, CrimeUtils.Events.UnforgivableCrimeCommittedAgainst, OnUnforgivableCrimeCommitted, this);
                }

                if (_notifyBountyCleared != null && _notifyBountyCleared.Value)
                {
                    _bountyClearedListener = ModelExtensions.ListenTo(hero, CrimeUtils.Events.BountyClearedFor, OnBountyCleared, this);
                }
            }

            if (IsCombatHitNotificationEnabled())
            {
                if (_notifyWeakspotHit != null && _notifyWeakspotHit.Value)
                {
                    _weakspotHitListener = ListenToHealthElementEvent(hero, "OnWeakspotHit", typeof(DamageModifiersInfo), "OnWeakspotHit");
                }

                if (_notifySneakAttack != null && _notifySneakAttack.Value)
                {
                    _sneakAttackListener = ListenToHealthElementEvent(hero, "OnSneakDamageDealt", typeof(DamageOutcome), "OnSneakAttack");
                }
            }
        }

        private IEventListener ListenToHealthElementEvent(Hero hero, string eventName, Type payloadType, string callbackMethodName)
        {
            try
            {
                Type eventsType = typeof(HealthElement).GetNestedType("Events", BindingFlags.Public);
                FieldInfo eventField = eventsType == null
                    ? null
                    : eventsType.GetField(eventName, BindingFlags.Public | BindingFlags.Static);
                MethodInfo callbackMethod = GetType().GetMethod(callbackMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo listenToMethod = GetEventSystemPayloadListenToMethod();
                if (eventField == null || callbackMethod == null || listenToMethod == null)
                {
                    return null;
                }

                object eventObject = eventField.GetValue(null);
                Type sourceType = eventObject == null ? null : eventObject.GetType().GetGenericArguments()[0];
                if (sourceType == null || string.IsNullOrEmpty(hero.ID))
                {
                    return null;
                }

                Delegate callback = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(payloadType), this, callbackMethod);
                MethodInfo closedListenTo = listenToMethod.MakeGenericMethod(sourceType, payloadType);
                object listener = closedListenTo.Invoke(
                    World.EventSystem,
                    new object[] { hero.ID, eventObject, this, callback });

                return listener as IEventListener;
            }
            catch (Exception exception)
            {
                LogDefaultGameEventBindingFailureOnce(exception);
                return null;
            }
        }

        private static MethodInfo GetEventSystemPayloadListenToMethod()
        {
            if (_eventSystemPayloadListenToMethod != null)
            {
                return _eventSystemPayloadListenToMethod;
            }

            MethodInfo[] methods = typeof(EventSystem).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "ListenTo", StringComparison.Ordinal) || !method.IsGenericMethodDefinition)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 4 &&
                    parameters[0].ParameterType == typeof(string) &&
                    typeof(IListenerOwner).IsAssignableFrom(parameters[2].ParameterType) &&
                    parameters[3].ParameterType.IsGenericType &&
                    parameters[3].ParameterType.GetGenericTypeDefinition() == typeof(Action<>))
                {
                    _eventSystemPayloadListenToMethod = method;
                    return method;
                }
            }

            return null;
        }

        private void CaptureInitialVanillaWyrdState(Hero hero)
        {
            _hasObservedWyrdNight = false;
            _hasObservedWyrdSafety = false;

            try
            {
                HeroWyrdNight wyrdNight = hero == null ? null : hero.HeroWyrdNight;
                if (wyrdNight != null)
                {
                    _lastObservedWyrdNight = wyrdNight.Night;
                    _hasObservedWyrdNight = true;
                    _lastObservedWyrdSafety = !wyrdNight.IsHeroInWyrdness;
                    _hasObservedWyrdSafety = true;
                }
            }
            catch (Exception exception)
            {
                LogDefaultGameEventBindingFailureOnce(exception);
            }
        }

        private void StopDefaultGameEventBinding()
        {
            if (_defaultGameEventBindingCoroutine != null)
            {
                StopCoroutine(_defaultGameEventBindingCoroutine);
                _defaultGameEventBindingCoroutine = null;
            }

            DisposeDefaultGameHeroListeners();
            if (World.EventSystem != null)
            {
                World.EventSystem.TryDisposeListener(ref _restingInitiatedListener);
                World.EventSystem.TryDisposeListener(ref _restingInterruptedListener);
                World.EventSystem.TryDisposeListener(ref _encumberedChangedListener);
                World.EventSystem.TryDisposeListener(ref _locationClearedListener);
                World.EventSystem.RemoveAllListenersOwnedBy(this, true);
            }
            else
            {
                _restingInitiatedListener = null;
                _restingInterruptedListener = null;
                _encumberedChangedListener = null;
                _locationClearedListener = null;
            }

            _vanillaWyrdHero = null;
        }

        private void DisposeDefaultGameHeroListeners()
        {
            if (World.EventSystem != null)
            {
                World.EventSystem.TryDisposeListener(ref _wyrdNightChangedListener);
                World.EventSystem.TryDisposeListener(ref _wyrdStatusChangedListener);
                World.EventSystem.TryDisposeListener(ref _wyrdSoulFragmentCollectedListener);
                World.EventSystem.TryDisposeListener(ref _wyrdSkillToggledListener);
                World.EventSystem.TryDisposeListener(ref _blockedDamageListener);
                World.EventSystem.TryDisposeListener(ref _parriedDamageListener);
                World.EventSystem.TryDisposeListener(ref _pickpocketSuccessListener);
                World.EventSystem.TryDisposeListener(ref _pickpocketFailListener);
                World.EventSystem.TryDisposeListener(ref _crimeCommittedListener);
                World.EventSystem.TryDisposeListener(ref _unforgivableCrimeListener);
                World.EventSystem.TryDisposeListener(ref _bountyClearedListener);
                World.EventSystem.TryDisposeListener(ref _weakspotHitListener);
                World.EventSystem.TryDisposeListener(ref _sneakAttackListener);
            }
            else
            {
                _wyrdNightChangedListener = null;
                _wyrdStatusChangedListener = null;
                _wyrdSoulFragmentCollectedListener = null;
                _wyrdSkillToggledListener = null;
                _blockedDamageListener = null;
                _parriedDamageListener = null;
                _pickpocketSuccessListener = null;
                _pickpocketFailListener = null;
                _crimeCommittedListener = null;
                _unforgivableCrimeListener = null;
                _bountyClearedListener = null;
                _weakspotHitListener = null;
                _sneakAttackListener = null;
            }
        }

        private void ClearDefaultGameHero()
        {
            if (_vanillaWyrdHero != null)
            {
                DisposeDefaultGameHeroListeners();
                _vanillaWyrdHero = null;
                _hasObservedWyrdNight = false;
                _hasObservedWyrdSafety = false;
            }
        }

        private void OnRestingInterrupted(RestPopupUI restPopup)
        {
            _lastRestWasInterrupted = true;
            _lastRestInterruptedTime = Time.unscaledTime;
        }

        private void OnRestingInitiated(float hoursRested)
        {
            if (!IsRestDurationNotificationEnabled())
            {
                _lastRestWasInterrupted = false;
                return;
            }

            int minutes = Mathf.RoundToInt(Mathf.Max(0.0f, hoursRested) * 60.0f);
            int minimumMinutes = _restNotificationMinimumMinutes == null
                ? 1
                : Math.Max(0, _restNotificationMinimumMinutes.Value);
            if (minutes < minimumMinutes)
            {
                _lastRestWasInterrupted = false;
                return;
            }

            bool interrupted = IsInterruptedRestDurationNotificationEnabled() &&
                _lastRestWasInterrupted &&
                Time.unscaledTime - _lastRestInterruptedTime < 2.0f;
            _lastRestWasInterrupted = false;

            string durationText = FormatDurationMinutes(minutes);
            string format = interrupted && _restInterruptedTextFormat != null
                ? _restInterruptedTextFormat.Value
                : (_restDurationTextFormat == null ? string.Empty : _restDurationTextFormat.Value);
            string fallback = interrupted
                ? "Rest interrupted: " + durationText + " slept"
                : "Rested " + durationText;
            string text = ApplyDurationFormat(format, durationText, fallback);

            ShowDefaultGameNotification(
                interrupted ? "default-rest-interrupted" : "default-rest-duration",
                text,
                interrupted ? "Warning" : "System",
                interrupted ? "Status" : "System",
                interrupted ? "High" : "Normal",
                string.Empty,
                "rest",
                DurationBucket.Long,
                0.9f,
                "default-rest-duration",
                0.25f);
        }

        private void OnEncumberedChanged(bool isEncumbered)
        {
            if (!IsEncumbranceNotificationEnabled())
            {
                return;
            }

            ShowDefaultGameNotification(
                isEncumbered ? "default-over-encumbered" : "default-burden-lifted",
                isEncumbered ? "Over-encumbered" : "Burden lifted",
                isEncumbered ? "Warning" : "Status",
                "Status",
                isEncumbered ? "High" : "Normal",
                "default-encumbrance",
                "weight",
                DurationBucket.Medium,
                0.9f,
                "default-encumbrance",
                0.25f);
        }

        private void OnLocationCleared(Location location)
        {
            if (!IsLocationClearedNotificationEnabled())
            {
                return;
            }

            string locationName = TryGetLocationDisplayName(location);
            string text = string.IsNullOrWhiteSpace(locationName)
                ? "Location cleared"
                : locationName + " cleared";

            ShowDefaultGameNotification(
                "default-location-cleared",
                text,
                "Reward",
                "Reward",
                "High",
                string.Empty,
                "location",
                DurationBucket.Long,
                0.9f,
                "default-location-cleared",
                0.25f);
        }

        private void OnHeroBlockedDamage(float damageAmount)
        {
            if (!ShouldShowCombatDefenseNotification(_notifyBlockedDamage, damageAmount))
            {
                return;
            }

            ShowDefaultGameNotification(
                "default-combat-blocked",
                "Blocked " + FormatDamageAmount(damageAmount) + " damage",
                "Combat",
                "Combat",
                "Low",
                "default-combat-blocked",
                "shield",
                DurationBucket.Short,
                0.8f,
                "default-combat-blocked",
                GetConfigCooldown(_combatDefenseCooldownSeconds));
        }

        private void OnHeroParriedDamage(float damageAmount)
        {
            if (!ShouldShowCombatDefenseNotification(_notifyParriedDamage, damageAmount))
            {
                return;
            }

            ShowDefaultGameNotification(
                "default-combat-parried",
                "Parried " + FormatDamageAmount(damageAmount) + " damage",
                "Combat",
                "Combat",
                "Normal",
                "default-combat-parried",
                "parry",
                DurationBucket.Short,
                0.85f,
                "default-combat-parried",
                GetConfigCooldown(_combatDefenseCooldownSeconds));
        }

        private void OnPickpocketSuccess(Item item)
        {
            if (_notifyPickpocketSuccess == null || !_notifyPickpocketSuccess.Value)
            {
                return;
            }

            string itemName = TryGetItemDisplayName(item);
            string text = string.IsNullOrWhiteSpace(itemName)
                ? "Pickpocket succeeded"
                : "Pickpocketed " + itemName;

            ShowDefaultGameNotification(
                "default-pickpocket-success",
                text,
                "Reward",
                "Reward",
                "Normal",
                string.Empty,
                "pickpocket",
                DurationBucket.Medium,
                0.9f,
                "default-pickpocket-success",
                GetConfigCooldown(_crimeEventCooldownSeconds));
        }

        private void OnPickpocketFail(Item item)
        {
            if (_notifyPickpocketFail == null || !_notifyPickpocketFail.Value)
            {
                return;
            }

            ShowDefaultGameNotification(
                "default-pickpocket-fail",
                "Pickpocket failed",
                "Warning",
                "Status",
                "High",
                "default-pickpocket-fail",
                "pickpocket",
                DurationBucket.Medium,
                0.9f,
                "default-pickpocket-fail",
                GetConfigCooldown(_crimeEventCooldownSeconds));
        }

        private void OnCrimeCommitted(CrimeChangeData crimeData)
        {
            if (_notifyBountyChanged == null || !_notifyBountyChanged.Value)
            {
                return;
            }

            float delta;
            if (!TryGetBountyDelta(crimeData, out delta))
            {
                return;
            }

            if (Math.Abs(delta) < 0.5f)
            {
                return;
            }

            string crimeType = FormatCrimeType(crimeData);
            string text = "Bounty " + FormatSignedWholeAmount(delta);
            if (!string.IsNullOrWhiteSpace(crimeType))
            {
                text += ": " + crimeType;
            }

            ShowDefaultGameNotification(
                "default-bounty-changed",
                text,
                "Warning",
                "Status",
                "High",
                string.Empty,
                "crime",
                DurationBucket.Medium,
                0.9f,
                "default-bounty-changed",
                GetConfigCooldown(_crimeEventCooldownSeconds));
        }

        private void OnUnforgivableCrimeCommitted(CrimeOwnerTemplate faction)
        {
            if (_notifyUnforgivableCrime == null || !_notifyUnforgivableCrime.Value)
            {
                return;
            }

            string factionName = TryGetCrimeOwnerDisplayName(faction);
            string text = string.IsNullOrWhiteSpace(factionName)
                ? "Unforgivable crime"
                : "Unforgivable crime: " + factionName;

            ShowDefaultGameNotification(
                "default-unforgivable-crime",
                text,
                "Critical",
                "Status",
                "Critical",
                string.Empty,
                "crime",
                DurationBucket.Long,
                0.95f,
                "default-unforgivable-crime",
                GetConfigCooldown(_crimeEventCooldownSeconds));
        }

        private void OnBountyCleared(CrimeOwnerTemplate faction)
        {
            if (_notifyBountyCleared == null || !_notifyBountyCleared.Value)
            {
                return;
            }

            string factionName = TryGetCrimeOwnerDisplayName(faction);
            string text = string.IsNullOrWhiteSpace(factionName)
                ? "Bounty cleared"
                : "Bounty cleared: " + factionName;

            ShowDefaultGameNotification(
                "default-bounty-cleared",
                text,
                "Reward",
                "Reward",
                "High",
                string.Empty,
                "crime",
                DurationBucket.Medium,
                0.9f,
                "default-bounty-cleared",
                GetConfigCooldown(_crimeEventCooldownSeconds));
        }

        private void OnWeakspotHit(DamageModifiersInfo modifiers)
        {
            if (_notifyWeakspotHit == null || !_notifyWeakspotHit.Value)
            {
                return;
            }

            string text = "Weak spot hit";
            if (modifiers.WeakSpotMultiplier > 1.01f)
            {
                text += " x" + FormatMultiplier(modifiers.WeakSpotMultiplier);
            }

            ShowDefaultGameNotification(
                "default-combat-weakspot",
                text,
                "Critical",
                "Combat",
                "Normal",
                "default-combat-weakspot",
                "critical",
                DurationBucket.Short,
                0.85f,
                "default-combat-weakspot",
                GetConfigCooldown(_combatHitCooldownSeconds));
        }

        private void OnSneakAttack(DamageOutcome outcome)
        {
            if (!ShouldShowCombatHitNotification(_notifySneakAttack, outcome.FinalAmount))
            {
                return;
            }

            string text = "Sneak attack";
            if (outcome.FinalAmount > 0.0f)
            {
                text += ": " + FormatDamageAmount(outcome.FinalAmount) + " damage";
            }

            ShowDefaultGameNotification(
                "default-combat-sneak-attack",
                text,
                "Critical",
                "Combat",
                "Normal",
                "default-combat-sneak-attack",
                "critical",
                DurationBucket.Short,
                0.85f,
                "default-combat-sneak-attack",
                GetConfigCooldown(_combatHitCooldownSeconds));
        }

        private void OnWyrdNightChanged(bool isNight)
        {
            if (!IsVanillaWyrdEventsEnabled() || _notifyWyrdNightChange == null || !_notifyWyrdNightChange.Value)
            {
                return;
            }

            if (_hasObservedWyrdNight && isNight == _lastObservedWyrdNight)
            {
                return;
            }

            _hasObservedWyrdNight = true;
            _lastObservedWyrdNight = isNight;
            _lastWyrdNightNotificationTime = Time.unscaledTime;

            ShowVanillaWyrdNotification(
                "vanilla-wyrd-night",
                isNight ? "Wyrdnight falls" : "Wyrdnight fades",
                "Status",
                "High",
                "vanilla-wyrd-night",
                "vanilla-wyrd-night");
        }

        private void OnWyrdStatusChanged(bool exposed)
        {
            if (!ShouldShowVanillaWyrdSafetyChange())
            {
                return;
            }

            bool safe = !exposed;
            if (_hasObservedWyrdSafety && safe == _lastObservedWyrdSafety)
            {
                return;
            }

            _hasObservedWyrdSafety = true;
            _lastObservedWyrdSafety = safe;

            if (Time.unscaledTime - _lastWyrdNightNotificationTime < 1.0f)
            {
                return;
            }

            ShowVanillaWyrdNotification(
                "vanilla-wyrd-safety",
                safe ? "Safe from Wyrdness" : "Exposed to Wyrdness",
                "Status",
                "Normal",
                "vanilla-wyrd-safety",
                "vanilla-wyrd-safety");
        }

        private void OnWyrdSoulFragmentCollected(WyrdSoulFragmentType fragmentType)
        {
            if (!IsVanillaWyrdEventsEnabled() ||
                _notifyWyrdSoulFragmentCollected == null ||
                !_notifyWyrdSoulFragmentCollected.Value ||
                fragmentType == WyrdSoulFragmentType.Baseline)
            {
                return;
            }

            string fragmentName = FormatEnumName(fragmentType.ToString());
            string text = string.IsNullOrWhiteSpace(fragmentName)
                ? "Wyrd power unlocked"
                : "Wyrd power unlocked: " + fragmentName;

            ShowVanillaWyrdNotification(
                "vanilla-wyrd-fragment",
                text,
                "Reward",
                "High",
                string.Empty,
                "vanilla-wyrd-fragment-" + fragmentType.ToString());
        }

        private void OnWyrdSkillToggled(bool active)
        {
            if (!IsVanillaWyrdEventsEnabled() || _notifyWyrdSkillToggle == null || !_notifyWyrdSkillToggle.Value)
            {
                return;
            }

            ShowVanillaWyrdNotification(
                "vanilla-wyrd-skill",
                active ? "Wyrd Skill active" : "Wyrd Skill ended",
                "Status",
                "Low",
                "vanilla-wyrd-skill",
                "vanilla-wyrd-skill");
        }

        private bool IsVanillaWyrdEventsEnabled()
        {
            return _vanillaWyrdEventsEnabled != null && _vanillaWyrdEventsEnabled.Value;
        }

        private bool HasAnyDefaultGameEventsEnabled()
        {
            return IsVanillaWyrdEventsEnabled() ||
                IsRestDurationNotificationEnabled() ||
                IsCombatDefenseNotificationEnabled() ||
                IsEncumbranceNotificationEnabled() ||
                IsLocationClearedNotificationEnabled() ||
                IsCrimeNotificationEnabled() ||
                IsCombatHitNotificationEnabled();
        }

        private bool IsRestDurationNotificationEnabled()
        {
            return _notifyRestDuration != null && _notifyRestDuration.Value;
        }

        private bool IsInterruptedRestDurationNotificationEnabled()
        {
            return _notifyInterruptedRestDuration != null && _notifyInterruptedRestDuration.Value;
        }

        private bool IsCombatDefenseNotificationEnabled()
        {
            return (_notifyBlockedDamage != null && _notifyBlockedDamage.Value) ||
                (_notifyParriedDamage != null && _notifyParriedDamage.Value);
        }

        private bool IsEncumbranceNotificationEnabled()
        {
            return _notifyEncumbranceChanged != null && _notifyEncumbranceChanged.Value;
        }

        private bool IsLocationClearedNotificationEnabled()
        {
            return _notifyLocationCleared != null && _notifyLocationCleared.Value;
        }

        private bool IsCrimeNotificationEnabled()
        {
            return (_notifyPickpocketSuccess != null && _notifyPickpocketSuccess.Value) ||
                (_notifyPickpocketFail != null && _notifyPickpocketFail.Value) ||
                (_notifyBountyChanged != null && _notifyBountyChanged.Value) ||
                (_notifyBountyCleared != null && _notifyBountyCleared.Value) ||
                (_notifyUnforgivableCrime != null && _notifyUnforgivableCrime.Value);
        }

        private bool IsCombatHitNotificationEnabled()
        {
            return (_notifyWeakspotHit != null && _notifyWeakspotHit.Value) ||
                (_notifySneakAttack != null && _notifySneakAttack.Value);
        }

        private bool ShouldShowVanillaWyrdSafetyChange()
        {
            if (!IsVanillaWyrdEventsEnabled() ||
                _notifyWyrdSafetyChange == null ||
                !_notifyWyrdSafetyChange.Value)
            {
                return false;
            }

            return _suppressWyrdSafetyWhenWyrdHuntAddonLoaded == null ||
                !_suppressWyrdSafetyWhenWyrdHuntAddonLoaded.Value ||
                !IsWyrdHuntAddonLoaded();
        }

        private bool IsWyrdHuntAddonLoaded()
        {
            try
            {
                return Chainloader.PluginInfos.ContainsKey(WyrdHuntAddonPluginGuid);
            }
            catch
            {
                return false;
            }
        }

        private void ShowVanillaWyrdNotification(
            string eventId,
            string text,
            string category,
            string priority,
            string collapseKey,
            string throttleKey)
        {
            if (!IsGameLoadedReadyForNotifications()
                || string.IsNullOrWhiteSpace(text)
                || ShouldThrottleDefaultGameEvent(throttleKey, GetConfigCooldown(_vanillaWyrdEventCooldownSeconds)))
            {
                return;
            }

            TryShowCore(
                PluginGuid,
                eventId,
                text,
                ResolveEyesWyrdStyle(),
                category,
                priority,
                collapseKey,
                "wyrd",
                GetDurationBucketSeconds(DurationBucket.Medium),
                0.25f,
                0.9f);
        }

        private void ShowDefaultGameNotification(
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            DurationBucket durationBucket,
            float opacity,
            string throttleKey,
            float throttleSeconds)
        {
            if (string.IsNullOrWhiteSpace(text) || ShouldThrottleDefaultGameEvent(throttleKey, throttleSeconds))
            {
                return;
            }

            TryShowCore(
                PluginGuid,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                GetDurationBucketSeconds(durationBucket),
                0.25f,
                opacity);
        }

        private bool ShouldShowCombatDefenseNotification(ConfigEntry<bool> enabledEntry, float damageAmount)
        {
            if (enabledEntry == null || !enabledEntry.Value)
            {
                return false;
            }

            float minimumDamage = _combatDefenseMinimumDamage == null
                ? 0.0f
                : Math.Max(0.0f, _combatDefenseMinimumDamage.Value);
            if (damageAmount < minimumDamage)
            {
                return false;
            }

            return true;
        }

        private bool ShouldShowCombatHitNotification(ConfigEntry<bool> enabledEntry, float damageAmount)
        {
            if (enabledEntry == null || !enabledEntry.Value)
            {
                return false;
            }

            float minimumDamage = _combatHitMinimumDamage == null
                ? 0.0f
                : Math.Max(0.0f, _combatHitMinimumDamage.Value);
            if (damageAmount < minimumDamage)
            {
                return false;
            }

            return true;
        }

        private bool ShouldThrottleDefaultGameEvent(string key, float cooldown)
        {
            if (cooldown <= 0.001f || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            float now = Time.unscaledTime;
            float last;
            if (_lastDefaultGameEventTimeByKey.TryGetValue(key, out last) && now - last < cooldown)
            {
                return true;
            }

            _lastDefaultGameEventTimeByKey[key] = now;
            return false;
        }

        private static float GetConfigCooldown(ConfigEntry<float> entry)
        {
            return entry == null ? 0.0f : Math.Max(0.0f, entry.Value);
        }

        private static string FormatDurationMinutes(int minutes)
        {
            minutes = Math.Max(0, minutes);
            int hours = minutes / 60;
            int remainder = minutes % 60;

            if (hours <= 0)
            {
                return minutes == 1 ? "1 minute" : minutes.ToString(CultureInfo.InvariantCulture) + " minutes";
            }

            if (remainder == 0)
            {
                return hours == 1 ? "1 hour" : hours.ToString(CultureInfo.InvariantCulture) + " hours";
            }

            return hours.ToString(CultureInfo.InvariantCulture) + "h " +
                remainder.ToString(CultureInfo.InvariantCulture) + "m";
        }

        private static string ApplyDurationFormat(string format, string durationText, string fallback)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return fallback;
            }

            string text = format.Replace("{duration}", durationText);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string FormatDamageAmount(float damageAmount)
        {
            int rounded = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.0f, damageAmount)));
            return rounded.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatSignedWholeAmount(float amount)
        {
            float absolute = Math.Abs(amount);
            int rounded = Mathf.RoundToInt(absolute);
            if (rounded < 1 && absolute > 0.001f)
            {
                rounded = 1;
            }

            return (amount >= 0.0f ? "+" : "-") + rounded.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatMultiplier(float multiplier)
        {
            multiplier = Math.Max(0.0f, multiplier);
            return multiplier.ToString(multiplier >= 10.0f ? "0" : "0.#", CultureInfo.InvariantCulture);
        }

        private static string FormatCrimeType(CrimeChangeData crimeData)
        {
            try
            {
                object boxedCrimeData = crimeData;
                object crime = InvokeGetter(boxedCrimeData, "get_CrimeCommitted");
                object archetype = GetPropertyValue(crime, "Archetype");
                object crimeType = GetPropertyValue(archetype, "CrimeType");
                string formatted = crimeType == null ? string.Empty : FormatEnumName(crimeType.ToString());
                return string.Equals(formatted, "None", StringComparison.OrdinalIgnoreCase) ? string.Empty : formatted;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryGetBountyDelta(CrimeChangeData crimeData, out float delta)
        {
            delta = 0.0f;

            try
            {
                object boxedCrimeData = crimeData;
                object bountyChange = InvokeGetter(boxedCrimeData, "get_BountyChange");
                if (bountyChange == null)
                {
                    return false;
                }

                Type changeType = bountyChange.GetType();
                FieldInfo fromField = changeType.GetField("from", BindingFlags.Instance | BindingFlags.Public);
                FieldInfo toField = changeType.GetField("to", BindingFlags.Instance | BindingFlags.Public);
                if (fromField == null || toField == null)
                {
                    return false;
                }

                float from = Convert.ToSingle(fromField.GetValue(bountyChange), CultureInfo.InvariantCulture);
                float to = Convert.ToSingle(toField.GetValue(bountyChange), CultureInfo.InvariantCulture);
                delta = to - from;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object InvokeGetter(object instance, string getterName)
        {
            if (instance == null)
            {
                return null;
            }

            MethodInfo getter = instance.GetType().GetMethod(getterName, BindingFlags.Instance | BindingFlags.Public);
            return getter == null ? null : getter.Invoke(instance, null);
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            if (instance == null)
            {
                return null;
            }

            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property == null ? null : property.GetValue(instance, null);
        }

        private static string TryGetItemDisplayName(Item item)
        {
            try
            {
                return item == null ? string.Empty : CleanDisplayName(item.DisplayName);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetLocationDisplayName(Location location)
        {
            try
            {
                return location == null ? string.Empty : CleanDisplayName(location.DisplayName);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetCrimeOwnerDisplayName(CrimeOwnerTemplate owner)
        {
            try
            {
                return owner == null ? string.Empty : CleanDisplayName(owner.DisplayName);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CleanDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static Hero TryGetCurrentHero()
        {
            try
            {
                return Hero.Current;
            }
            catch
            {
                return null;
            }
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (i > 0 && char.IsUpper(ch) && !char.IsWhiteSpace(value[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(ch);
            }

            return builder.ToString().Replace("_", " ").Trim();
        }

        private void LogDefaultGameEventBindingFailureOnce(Exception exception)
        {
            if (_defaultGameEventBindingFailureLogged)
            {
                return;
            }

            _defaultGameEventBindingFailureLogged = true;
            Logger.LogWarning("Could not bind default game events: " + exception.GetBaseException().Message);
        }

        private void LoadIconTextures()
        {
            ReleaseIconTextures();

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = string.IsNullOrEmpty(assemblyPath)
                ? string.Empty
                : Path.GetDirectoryName(assemblyPath);
            string iconDirectory = string.IsNullOrEmpty(assemblyDirectory)
                ? string.Empty
                : Path.Combine(assemblyDirectory, "icons");

            if (string.IsNullOrEmpty(iconDirectory) || !Directory.Exists(iconDirectory))
            {
                Logger.LogWarning(PluginName + " icon directory was not found: " + iconDirectory);
                return;
            }

            MethodInfo loadImageMethod = typeof(ImageConversion).GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);
            if (loadImageMethod == null)
            {
                Logger.LogWarning(PluginName + " could not find Unity image loader for built-in icons.");
                return;
            }

            int loadedIconCount = 0;
            int minimumMipCount = int.MaxValue;
            int maximumMipCount = 0;
            for (int i = 0; i < BuiltInIconIds.Length; i++)
            {
                string iconId = BuiltInIconIds[i];
                string path = Path.Combine(iconDirectory, iconId + ".png");
                if (!File.Exists(path))
                {
                    Logger.LogWarning(PluginName + " built-in icon was not found: " + path);
                    continue;
                }

                Texture2D texture = null;
                try
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    object loadResult = loadImageMethod.Invoke(null, new object[] { texture, File.ReadAllBytes(path), false });
                    if (!(loadResult is bool) || !((bool)loadResult))
                    {
                        UnityEngine.Object.Destroy(texture);
                        texture = null;
                        Logger.LogWarning(PluginName + " could not load icon: " + path);
                        continue;
                    }

                    texture.name = "GrailFloatingTextIcon_" + iconId;
                    texture.hideFlags = HideFlags.DontSave;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    DilateTransparentPixelColors(texture);
                    texture.Apply(true, true);
                    texture.filterMode = FilterMode.Trilinear;
                    _iconTexturesById[iconId] = texture;
                    loadedIconCount++;
                    minimumMipCount = Math.Min(minimumMipCount, texture.mipmapCount);
                    maximumMipCount = Math.Max(maximumMipCount, texture.mipmapCount);
                }
                catch (Exception exception)
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }

                    Logger.LogWarning(PluginName + " could not load icon " + path + ": " + exception.GetBaseException().Message);
                }
            }

            if (loadedIconCount > 0)
            {
                string mipCount = minimumMipCount == maximumMipCount
                    ? minimumMipCount.ToString(CultureInfo.InvariantCulture)
                    : minimumMipCount.ToString(CultureInfo.InvariantCulture)
                        + "-"
                        + maximumMipCount.ToString(CultureInfo.InvariantCulture);
                Logger.LogInfo(
                    PluginName
                    + " loaded "
                    + loadedIconCount.ToString(CultureInfo.InvariantCulture)
                    + " built-in icon textures with trilinear filtering and runtime mipmaps (mipLevels="
                    + mipCount
                    + ").");
            }
        }

        private static void DilateTransparentPixelColors(Texture2D texture)
        {
            int width = texture.width;
            int height = texture.height;
            Color32[] pixels = texture.GetPixels32();
            int pixelCount = pixels.Length;
            if (width <= 0 || height <= 0 || pixelCount != width * height)
            {
                return;
            }

            bool[] resolved = new bool[pixelCount];
            int[] queue = new int[pixelCount];
            int queueHead = 0;
            int queueTail = 0;
            for (int i = 0; i < pixelCount; i++)
            {
                if (pixels[i].a == 0)
                {
                    continue;
                }

                resolved[i] = true;
                queue[queueTail++] = i;
            }

            while (queueHead < queueTail)
            {
                int sourceIndex = queue[queueHead++];
                int x = sourceIndex % width;
                int y = sourceIndex / width;
                if (x > 0)
                {
                    DilateTransparentNeighbor(pixels, resolved, queue, ref queueTail, sourceIndex, sourceIndex - 1);
                }

                if (x + 1 < width)
                {
                    DilateTransparentNeighbor(pixels, resolved, queue, ref queueTail, sourceIndex, sourceIndex + 1);
                }

                if (y > 0)
                {
                    DilateTransparentNeighbor(pixels, resolved, queue, ref queueTail, sourceIndex, sourceIndex - width);
                }

                if (y + 1 < height)
                {
                    DilateTransparentNeighbor(pixels, resolved, queue, ref queueTail, sourceIndex, sourceIndex + width);
                }
            }

            texture.SetPixels32(pixels);
        }

        private static void DilateTransparentNeighbor(
            Color32[] pixels,
            bool[] resolved,
            int[] queue,
            ref int queueTail,
            int sourceIndex,
            int neighborIndex)
        {
            if (resolved[neighborIndex])
            {
                return;
            }

            Color32 source = pixels[sourceIndex];
            pixels[neighborIndex] = new Color32(source.r, source.g, source.b, 0);
            resolved[neighborIndex] = true;
            queue[queueTail++] = neighborIndex;
        }

        private void ReleaseIconTextures()
        {
            foreach (Texture2D texture in _iconTexturesById.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            _iconTexturesById.Clear();
        }

        internal bool TryShow(
            string sourceId,
            string text,
            string style,
            float durationSeconds,
            float fadeSeconds,
            float opacity)
        {
            return TryShow(sourceId, text, style, "General", "Normal", string.Empty, string.Empty, durationSeconds, fadeSeconds, opacity);
        }

        internal bool TryShow(
            string sourceId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            float durationSeconds,
            float fadeSeconds,
            float opacity)
        {
            return TryShow(sourceId, text, style, category, priority, collapseKey, string.Empty, durationSeconds, fadeSeconds, opacity);
        }

        internal bool TryShow(
            string sourceId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            float durationSeconds,
            float fadeSeconds,
            float opacity)
        {
            return TryShowCore(sourceId, string.Empty, text, style, category, priority, collapseKey, iconId, durationSeconds, fadeSeconds, opacity);
        }

        internal bool TryShowEvent(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            string durationBucket,
            float fadeSeconds,
            float opacity)
        {
            return TryShowEventCore(
                sourceId,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                GetDurationBucketSeconds(ParseDurationBucket(durationBucket)),
                ResolveDefaultDeliveryPoint(eventId),
                fadeSeconds,
                opacity);
        }

        internal bool TryShowEvent(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            string durationBucket,
            string deliveryPoint,
            float fadeSeconds,
            float opacity)
        {
            DeliveryPoint parsedDeliveryPoint;
            if (!TryParseDeliveryPoint(deliveryPoint, out parsedDeliveryPoint))
            {
                return false;
            }

            return TryShowEventCore(
                sourceId,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                GetDurationBucketSeconds(ParseDurationBucket(durationBucket)),
                parsedDeliveryPoint,
                fadeSeconds,
                opacity);
        }

        internal bool TryClaimXpGain(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string iconId,
            string durationBucket,
            float expectedAmount,
            float fadeSeconds,
            float opacity)
        {
            return TryClaimXpGainCore(
                sourceId,
                eventId,
                text,
                string.Empty,
                string.Empty,
                style,
                category,
                priority,
                iconId,
                durationBucket,
                expectedAmount,
                fadeSeconds,
                opacity);
        }

        internal bool TryClaimConsolidatedXpGain(
            string sourceId,
            string eventId,
            string consolidationKey,
            string textFormat,
            string style,
            string category,
            string priority,
            string iconId,
            string durationBucket,
            float expectedAmount,
            float fadeSeconds,
            float opacity)
        {
            if (string.IsNullOrWhiteSpace(consolidationKey) || string.IsNullOrWhiteSpace(textFormat))
            {
                return false;
            }

            return TryClaimXpGainCore(
                sourceId,
                eventId,
                string.Empty,
                consolidationKey,
                textFormat,
                style,
                category,
                priority,
                iconId,
                durationBucket,
                expectedAmount,
                fadeSeconds,
                opacity);
        }

        private bool TryClaimXpGainCore(
            string sourceId,
            string eventId,
            string text,
            string consolidationKey,
            string textFormat,
            string style,
            string category,
            string priority,
            string iconId,
            string durationBucket,
            float expectedAmount,
            float fadeSeconds,
            float opacity)
        {
            if (_enabled == null || !_enabled.Value || !IsXpNotificationEnabled() || expectedAmount <= 0.0f)
            {
                return false;
            }

            float now = Time.unscaledTime;
            PruneExpiredXpClaims(now);

            _xpDisplayClaims.Add(new XpDisplayClaim
            {
                SourceId = NormalizeSourceId(sourceId),
                EventId = NormalizeEventId(eventId),
                Text = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim(),
                ConsolidationKey = string.IsNullOrWhiteSpace(consolidationKey) ? string.Empty : consolidationKey.Trim(),
                TextFormat = string.IsNullOrWhiteSpace(textFormat) ? string.Empty : textFormat.Trim(),
                Style = string.IsNullOrWhiteSpace(style) ? "White" : style,
                Category = string.IsNullOrWhiteSpace(category) ? "Reward" : category,
                Priority = string.IsNullOrWhiteSpace(priority) ? "High" : priority,
                IconId = string.IsNullOrWhiteSpace(iconId) ? "experience" : iconId,
                DurationBucket = ParseDurationBucket(durationBucket),
                ExpectedAmount = expectedAmount,
                FadeSeconds = fadeSeconds,
                Opacity = opacity,
                CreatedAt = now,
                Sequence = ++_nextXpClaimSequence
            });

            return true;
        }

        private bool HandleXpChangedFromStatHook(HookResult<IWithStats, Stat.StatChange> hookResult)
        {
            float gainedXp = hookResult.Value.value;
            bool allowVanilla = HandleXpChanged(gainedXp);
            if (allowVanilla)
            {
                _passThroughNextXpFloatAnnounce = true;
                _passThroughNextXpFloatAmount = gainedXp;
                _passThroughNextXpFloatTime = Time.unscaledTime;
            }

            return allowVanilla;
        }

        private bool HandleXpChangedFromDirectFloat(float gainedXp)
        {
            if (ConsumePassThroughXpFloatAnnounce(gainedXp))
            {
                return true;
            }

            if (IsRecentHandledXpDuplicate(gainedXp, Time.unscaledTime))
            {
                return false;
            }

            return HandleXpChanged(gainedXp);
        }

        private bool HandleXpChanged(float gainedXp)
        {
            if (gainedXp <= 0.0f || !IsXpNotificationEnabled())
            {
                return true;
            }

            float now = Time.unscaledTime;
            XpDisplayClaim claim = TakeXpDisplayClaim(gainedXp, now);
            bool shown = ShowXpNotification(gainedXp, claim);
            if (!shown)
            {
                return true;
            }

            _lastHandledXpAmount = gainedXp;
            _lastHandledXpTime = now;
            return !ShouldSuppressVanillaXpNotifications();
        }

        private bool ShowXpNotification(float gainedXp, XpDisplayClaim claim)
        {
            XpNotificationBatch batch = new XpNotificationBatch
            {
                SourceId = claim == null || string.IsNullOrWhiteSpace(claim.SourceId)
                    ? PluginGuid
                    : claim.SourceId,
                EventId = claim == null || string.IsNullOrWhiteSpace(claim.EventId)
                    ? DefaultXpGainEventId
                    : claim.EventId,
                ConsolidationKey = claim == null
                    ? DefaultXpGainEventId
                    : claim.ConsolidationKey,
                Text = claim == null ? string.Empty : claim.Text,
                TextFormat = claim == null ? GetConfiguredXpTextFormat() : claim.TextFormat,
                Style = claim == null || string.IsNullOrWhiteSpace(claim.Style) ? "White" : claim.Style,
                Category = claim == null || string.IsNullOrWhiteSpace(claim.Category) ? "Reward" : claim.Category,
                Priority = claim == null || string.IsNullOrWhiteSpace(claim.Priority) ? "High" : claim.Priority,
                IconId = claim == null || string.IsNullOrWhiteSpace(claim.IconId) ? "experience" : claim.IconId,
                DurationBucket = claim == null
                    ? ParseDurationBucket(_xpDurationBucket == null ? "Short" : _xpDurationBucket.Value)
                    : claim.DurationBucket,
                Amount = gainedXp,
                FadeSeconds = claim == null ? -1.0f : claim.FadeSeconds,
                Opacity = claim == null ? 0.9f : claim.Opacity
            };

            if (!IsXpConsolidationEnabled())
            {
                return TryShowXpBatch(batch, false);
            }

            float now = Time.unscaledTime;
            PruneExpired(now);
            AdvanceXpQueue();
            if (_activeXpNotification == null)
            {
                return TryShowXpBatch(batch, true);
            }

            if (!CanAcceptXpBatch(batch))
            {
                return false;
            }

            QueueXpBatch(batch);
            return true;
        }

        private string GetConfiguredXpTextFormat()
        {
            string format = _xpTextFormat == null ? string.Empty : _xpTextFormat.Value;
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "+{xp} XP";
            }

            return format;
        }

        private static string FormatXpText(float gainedXp, string format)
        {
            string amount = gainedXp.ToString("F0", CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "+{xp} XP";
            }

            return format
                .Replace("{xp}", amount)
                .Replace("{amount}", amount);
        }

        private bool IsXpConsolidationEnabled()
        {
            return _consolidateXpGains != null && _consolidateXpGains.Value;
        }

        private bool CanAcceptXpBatch(XpNotificationBatch batch)
        {
            if (batch == null || !IsCategoryEnabled(NormalizeCategory(batch.Category)))
            {
                return false;
            }

            SourceSettings sourceSettings = GetSourceSettings(NormalizeSourceId(batch.SourceId));
            return IsSourceEnabled(sourceSettings);
        }

        private void QueueXpBatch(XpNotificationBatch incoming)
        {
            if (!string.IsNullOrWhiteSpace(incoming.ConsolidationKey))
            {
                for (int i = 0; i < _pendingXpBatches.Count; i++)
                {
                    XpNotificationBatch existing = _pendingXpBatches[i];
                    if (CanConsolidateXpBatches(existing, incoming))
                    {
                        existing.Amount += incoming.Amount;
                        return;
                    }
                }
            }

            _pendingXpBatches.Add(incoming);
        }

        private static bool CanConsolidateXpBatches(XpNotificationBatch existing, XpNotificationBatch incoming)
        {
            return existing != null &&
                incoming != null &&
                !string.IsNullOrWhiteSpace(existing.ConsolidationKey) &&
                string.Equals(existing.SourceId, incoming.SourceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.ConsolidationKey, incoming.ConsolidationKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.EventId, incoming.EventId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.TextFormat, incoming.TextFormat, StringComparison.Ordinal) &&
                string.Equals(existing.Style, incoming.Style, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Category, incoming.Category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Priority, incoming.Priority, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.IconId, incoming.IconId, StringComparison.OrdinalIgnoreCase) &&
                existing.DurationBucket == incoming.DurationBucket &&
                Math.Abs(existing.FadeSeconds - incoming.FadeSeconds) <= 0.001f &&
                Math.Abs(existing.Opacity - incoming.Opacity) <= 0.001f;
        }

        private bool TryShowXpBatch(XpNotificationBatch batch, bool trackAsActive)
        {
            if (batch == null)
            {
                return false;
            }

            string text = string.IsNullOrWhiteSpace(batch.TextFormat)
                ? batch.Text
                : FormatXpText(batch.Amount, batch.TextFormat);
            string collapseKey = batch.EventId
                + "-entry-"
                + (++_nextXpEntrySequence).ToString(CultureInfo.InvariantCulture);
            bool shown = TryShowCore(
                batch.SourceId,
                batch.EventId,
                text,
                batch.Style,
                batch.Category,
                batch.Priority,
                collapseKey,
                batch.IconId,
                GetDurationBucketSeconds(batch.DurationBucket),
                batch.FadeSeconds,
                batch.Opacity);
            if (!shown || !trackAsActive)
            {
                return shown;
            }

            _activeXpNotification = FindCollapsibleEntry(NormalizeSourceId(batch.SourceId), collapseKey);
            return _activeXpNotification != null;
        }

        private void AdvanceXpQueue()
        {
            if (!IsXpConsolidationEnabled())
            {
                _activeXpNotification = null;
                if (_pendingXpBatches.Count == 0)
                {
                    return;
                }

                XpNotificationBatch[] pending = _pendingXpBatches.ToArray();
                _pendingXpBatches.Clear();
                for (int i = 0; i < pending.Length; i++)
                {
                    TryShowXpBatch(pending[i], false);
                }

                return;
            }

            if (_activeXpNotification != null && _notifications.Contains(_activeXpNotification))
            {
                return;
            }

            _activeXpNotification = null;
            while (_pendingXpBatches.Count > 0)
            {
                XpNotificationBatch next = _pendingXpBatches[0];
                _pendingXpBatches.RemoveAt(0);
                if (TryShowXpBatch(next, true))
                {
                    return;
                }
            }
        }

        private bool IsXpNotificationEnabled()
        {
            return _notifyXpGained != null && _notifyXpGained.Value;
        }

        private bool ShouldSuppressVanillaXpNotifications()
        {
            return _suppressVanillaXpNotifications != null && _suppressVanillaXpNotifications.Value;
        }

        private XpDisplayClaim TakeXpDisplayClaim(float gainedXp, float now)
        {
            PruneExpiredXpClaims(now);

            int bestIndex = -1;
            long bestSequence = long.MaxValue;
            for (int i = 0; i < _xpDisplayClaims.Count; i++)
            {
                XpDisplayClaim claim = _xpDisplayClaims[i];
                if (claim == null)
                {
                    continue;
                }

                float tolerance = Math.Max(XpClaimAmountTolerance, Math.Abs(claim.ExpectedAmount) * 0.001f);
                if (Math.Abs(claim.ExpectedAmount - gainedXp) <= tolerance && claim.Sequence < bestSequence)
                {
                    bestIndex = i;
                    bestSequence = claim.Sequence;
                }
            }

            if (bestIndex < 0)
            {
                for (int i = 0; i < _xpDisplayClaims.Count; i++)
                {
                    XpDisplayClaim claim = _xpDisplayClaims[i];
                    if (claim != null && now - claim.CreatedAt <= XpClaimImmediateFallbackSeconds && claim.Sequence < bestSequence)
                    {
                        bestIndex = i;
                        bestSequence = claim.Sequence;
                    }
                }
            }

            if (bestIndex < 0)
            {
                return null;
            }

            XpDisplayClaim selected = _xpDisplayClaims[bestIndex];
            _xpDisplayClaims.RemoveAt(bestIndex);
            return selected;
        }

        private void PruneExpiredXpClaims(float now)
        {
            for (int i = _xpDisplayClaims.Count - 1; i >= 0; i--)
            {
                XpDisplayClaim claim = _xpDisplayClaims[i];
                if (claim == null || now - claim.CreatedAt > XpClaimLifetimeSeconds)
                {
                    _xpDisplayClaims.RemoveAt(i);
                }
            }
        }

        private bool ConsumePassThroughXpFloatAnnounce(float gainedXp)
        {
            if (!_passThroughNextXpFloatAnnounce)
            {
                return false;
            }

            _passThroughNextXpFloatAnnounce = false;
            return Time.unscaledTime - _passThroughNextXpFloatTime <= XpClaimImmediateFallbackSeconds &&
                Math.Abs(_passThroughNextXpFloatAmount - gainedXp) <= XpClaimAmountTolerance;
        }

        private bool IsRecentHandledXpDuplicate(float gainedXp, float now)
        {
            return now - _lastHandledXpTime <= DirectXpDuplicateSuppressSeconds &&
                Math.Abs(_lastHandledXpAmount - gainedXp) <= XpClaimAmountTolerance;
        }

        private bool TryShowEventCore(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            float durationSeconds,
            DeliveryPoint deliveryPoint,
            float fadeSeconds,
            float opacity)
        {
            if (deliveryPoint == DeliveryPoint.Immediate)
            {
                return TryShowCore(
                    sourceId,
                    eventId,
                    text,
                    style,
                    category,
                    priority,
                    collapseKey,
                    iconId,
                    durationSeconds,
                    fadeSeconds,
                    opacity);
            }

            return QueueDeferredNotification(
                sourceId,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                durationSeconds,
                deliveryPoint,
                fadeSeconds,
                opacity);
        }

        private bool QueueDeferredNotification(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            float durationSeconds,
            DeliveryPoint deliveryPoint,
            float fadeSeconds,
            float opacity)
        {
            if (_enabled == null || !_enabled.Value || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalizedSourceId = NormalizeSourceId(sourceId);
            string normalizedCategory = NormalizeCategory(category);
            if (!IsCategoryEnabled(normalizedCategory))
            {
                return false;
            }

            SourceSettings sourceSettings = GetSourceSettings(normalizedSourceId);
            if (!IsSourceEnabled(sourceSettings))
            {
                return false;
            }

            int priorityValue = ResolvePriorityValue(priority);
            string normalizedCollapseKey = string.IsNullOrWhiteSpace(collapseKey)
                ? string.Empty
                : collapseKey.Trim();
            string normalizedEventId = NormalizeEventId(eventId);
            if (string.IsNullOrEmpty(normalizedEventId) && !string.IsNullOrEmpty(normalizedCollapseKey))
            {
                normalizedEventId = NormalizeEventId(normalizedCollapseKey);
            }

            string normalizedStyle = ResolveEventStyle(
                normalizedEventId,
                NormalizeStyle(style, normalizedCategory, priorityValue));
            string normalizedIconId = NormalizeIconId(
                iconId,
                normalizedStyle,
                normalizedCategory,
                priorityValue);

            DeferredNotificationEntry entry = new DeferredNotificationEntry
            {
                SourceId = normalizedSourceId,
                EventId = normalizedEventId,
                Text = text,
                Style = normalizedStyle,
                Category = normalizedCategory,
                Priority = NormalizePriority(priority),
                CollapseKey = normalizedCollapseKey,
                IconId = normalizedIconId,
                DurationSeconds = durationSeconds,
                DeliveryPoint = deliveryPoint,
                FadeSeconds = fadeSeconds,
                Opacity = opacity,
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            };

            AddOrReplaceDeferredNotification(entry);
            SaveDeferredNotifications();

            if ((_diagnostics != null && _diagnostics.Value) || IsSourceDiagnosticsEnabled(sourceSettings))
            {
                Logger.LogInfo(
                    "Deferred notification from "
                    + normalizedSourceId
                    + " until "
                    + deliveryPoint
                    + ": "
                    + text);
            }

            return true;
        }

        private void AddOrReplaceDeferredNotification(DeferredNotificationEntry incoming)
        {
            for (int i = 0; i < _deferredNotifications.Count; i++)
            {
                DeferredNotificationEntry existing = _deferredNotifications[i];
                bool sameCollapseKey = !string.IsNullOrEmpty(incoming.CollapseKey)
                    && string.Equals(existing.CollapseKey, incoming.CollapseKey, StringComparison.OrdinalIgnoreCase);
                bool exactDuplicate = string.Equals(existing.EventId, incoming.EventId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Text, incoming.Text, StringComparison.Ordinal)
                    && string.Equals(existing.Style, incoming.Style, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Category, incoming.Category, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.IconId, incoming.IconId, StringComparison.OrdinalIgnoreCase);

                if (existing.DeliveryPoint == incoming.DeliveryPoint
                    && string.Equals(existing.SourceId, incoming.SourceId, StringComparison.OrdinalIgnoreCase)
                    && (sameCollapseKey || exactDuplicate))
                {
                    _deferredNotifications[i] = incoming;
                    return;
                }
            }

            _deferredNotifications.Add(incoming);
            while (_deferredNotifications.Count > MaximumDeferredNotifications)
            {
                _deferredNotifications.RemoveAt(0);
            }
        }

        private void ReleaseReadyDeferredNotifications()
        {
            if (_deferredNotifications.Count == 0)
            {
                return;
            }

            bool mainMenuReady = IsMainMenuReadyForNotifications();
            bool gameLoadedReady = IsGameLoadedReadyForNotifications();
            if (!mainMenuReady && !gameLoadedReady)
            {
                return;
            }

            bool changed = false;
            for (int i = _deferredNotifications.Count - 1; i >= 0; i--)
            {
                DeferredNotificationEntry entry = _deferredNotifications[i];
                if ((entry.DeliveryPoint == DeliveryPoint.OnMainMenu && !mainMenuReady)
                    || (entry.DeliveryPoint == DeliveryPoint.OnLoad && !gameLoadedReady))
                {
                    continue;
                }

                _deferredNotifications.RemoveAt(i);
                changed = true;
                bool shown = TryShowCore(
                    entry.SourceId,
                    entry.EventId,
                    entry.Text,
                    entry.Style,
                    entry.Category,
                    entry.Priority,
                    entry.CollapseKey,
                    entry.IconId,
                    entry.DurationSeconds,
                    entry.FadeSeconds,
                    entry.Opacity);

                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        (shown ? "Released" : "Discarded")
                        + " deferred "
                        + entry.DeliveryPoint
                        + " notification from "
                        + entry.SourceId
                        + ": "
                        + entry.Text);
                }
            }

            if (changed)
            {
                SaveDeferredNotifications();
            }
        }

        private static bool IsMainMenuReadyForNotifications()
        {
            if (!IsScreenVisibleForNotifications())
            {
                return false;
            }

            try
            {
                VTitleScreenUI titleScreen = UnityEngine.Object.FindFirstObjectByType<VTitleScreenUI>();
                return titleScreen != null
                    && titleScreen.isActiveAndEnabled
                    && titleScreen.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsGameLoadedReadyForNotifications()
        {
            if (!IsScreenVisibleForNotifications() || LoadingStates.IsLoadingWorld)
            {
                return false;
            }

            try
            {
                Hero hero = Hero.Current;
                if (hero == null || hero.HasBeenDiscarded || !SceneLifetimeEvents.Get.EverythingInitialized)
                {
                    return false;
                }

                Video video = World.Any<Video>();
                return (video == null || !video.IsFullScreen)
                    && !World.HasAny<Cutscene>();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsScreenVisibleForNotifications()
        {
            if (!Application.isFocused
                || Screen.width <= 0
                || Screen.height <= 0
                || LoadingScreenUI.IsLoading)
            {
                return false;
            }

            try
            {
                if (World.HasAny<LoadingScreenUI>())
                {
                    return false;
                }

                TransitionService transition = World.Services.TryGet<TransitionService>();
                return transition == null || !transition.InTransition;
            }
            catch
            {
                return false;
            }
        }

        private bool TryShowCore(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            float durationSeconds,
            float fadeSeconds,
            float opacity)
        {
            if (_enabled == null || !_enabled.Value || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            float now = Time.unscaledTime;
            PruneExpired(now);

            string normalizedSourceId = NormalizeSourceId(sourceId);
            string normalizedCategory = NormalizeCategory(category);
            if (!IsCategoryEnabled(normalizedCategory))
            {
                return false;
            }

            SourceSettings sourceSettings = GetSourceSettings(normalizedSourceId);
            if (!IsSourceEnabled(sourceSettings))
            {
                return false;
            }

            int priorityValue = ResolvePriorityValue(priority);
            if (ShouldThrottleSource(normalizedSourceId, priorityValue, now, GetSourceThrottleSeconds(sourceSettings)))
            {
                return false;
            }

            string normalizedCollapseKey = string.IsNullOrWhiteSpace(collapseKey) ? string.Empty : collapseKey.Trim();
            string normalizedEventId = NormalizeEventId(eventId);
            if (string.IsNullOrEmpty(normalizedEventId) && !string.IsNullOrEmpty(normalizedCollapseKey))
            {
                normalizedEventId = NormalizeEventId(normalizedCollapseKey);
            }

            string normalizedStyle = ResolveEventStyle(normalizedEventId, NormalizeStyle(style, normalizedCategory, priorityValue));
            string normalizedIconId = NormalizeIconId(iconId, normalizedStyle, normalizedCategory, priorityValue);

            if (string.IsNullOrEmpty(normalizedCollapseKey) &&
                IsDuplicateSuppressed(normalizedSourceId, normalizedEventId, text, normalizedStyle, normalizedCategory, normalizedIconId, now))
            {
                return false;
            }

            float duration = durationSeconds > 0.0f
                ? durationSeconds
                : Math.Max(DefaultMinimumDurationSeconds, _defaultDurationSeconds.Value);
            duration *= GetSourceDurationMultiplier(sourceSettings);
            float fade = fadeSeconds >= 0.0f
                ? fadeSeconds
                : Math.Max(0.0f, _defaultFadeSeconds.Value);
            float entryOpacity = opacity > 0.0f ? Clamp(opacity, 0.0f, 1.0f) : 1.0f;

            NotificationEntry entry = FindCollapsibleEntry(normalizedSourceId, normalizedCollapseKey);
            if (entry == null)
            {
                entry = new NotificationEntry
                {
                    SourceId = normalizedSourceId,
                    CollapseKey = normalizedCollapseKey
                };
            }
            else
            {
                _notifications.Remove(entry);
            }

            entry.Text = text;
            entry.EventId = normalizedEventId;
            entry.Style = normalizedStyle;
            entry.Category = normalizedCategory;
            entry.Priority = NormalizePriority(priority);
            entry.PriorityValue = priorityValue;
            entry.IconId = normalizedIconId;
            entry.StartTime = now;
            entry.DurationSeconds = Math.Max(DefaultMinimumDurationSeconds, duration);
            entry.FadeSeconds = fade;
            entry.Opacity = entryOpacity;
            entry.Sequence = ++_nextSequence;

            InsertEntry(entry);
            _lastNotificationTimeBySource[normalizedSourceId] = now;

            TrimToMaximumVisible();

            if ((_diagnostics != null && _diagnostics.Value) || IsSourceDiagnosticsEnabled(sourceSettings))
            {
                ColorGroupSettings diagnosticGroup = ResolveStyleColorGroup(entry.Style);
                string configuredColor = diagnosticGroup != null
                    && diagnosticGroup.Color != null
                    ? diagnosticGroup.Color.Value
                    : "(not a color group)";
                string configuredIconColor = diagnosticGroup != null
                    && diagnosticGroup.IconColor != null
                    && !string.IsNullOrWhiteSpace(diagnosticGroup.IconColor.Value)
                    ? diagnosticGroup.IconColor.Value
                    : diagnosticGroup == null ? "(not a color group)" : "(inherit)";
                Color resolvedColor = ResolveStyleColor(entry.Style, 1.0f);
                Color resolvedIconColor = ResolveIconColor(entry.Style, resolvedColor, 1.0f);
                Logger.LogInfo(
                    "Queued notification from "
                    + normalizedSourceId
                    + " ["
                    + normalizedCategory
                    + "/"
                    + entry.Priority
                    + "] event="
                    + entry.EventId
                    + "; style="
                    + entry.Style
                    + "; configuredColor="
                    + configuredColor
                    + "; resolvedColor=#"
                    + ColorUtility.ToHtmlStringRGBA(resolvedColor)
                    + "; configuredIconColor="
                    + configuredIconColor
                    + "; resolvedIconColor=#"
                    + ColorUtility.ToHtmlStringRGBA(resolvedIconColor)
                    + "; entryOpacity="
                    + entry.Opacity.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; globalOpacity="
                    + _globalOpacity.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; text="
                    + text);
            }

            return true;
        }

        private void LateUpdate()
        {
            if (_enabled == null || !_enabled.Value)
            {
                SetNotificationViewsActive(0);
                SetQuickWheelPanelActive(false);
                return;
            }

            ScanLoadedModCompatibility();
            ReleaseReadyDeferredNotifications();

            float now = Time.unscaledTime;
            UpdateQuickWheelPanel(now);
            PruneExpired(now);
            AdvanceXpQueue();
            if (_notifications.Count == 0)
            {
                SetNotificationViewsActive(0);
                return;
            }

            float scale = Math.Max(0.05f, _scale.Value);
            float fontSize = Math.Max(1.0f, Math.Max(1, _fontSize.Value) * scale);
            FontAsset fontAsset;
            try
            {
                fontAsset = ResolveConfiguredFontAsset();
            }
            catch (Exception ex)
            {
                LogFontDiagnosticOnce(
                    "ResolveConfiguredFontAssetOuter:"
                        + ex.GetType().FullName,
                    "Could not resolve the configured font for the TextMesh Pro overlay; using the safe fallback font. "
                        + ex.GetBaseException().Message);
                fontAsset = ResolveFallbackFontAsset();
            }

            float width = Math.Max(20.0f, _width.Value * scale);
            float height = Math.Max(fontSize + 10.0f, 32.0f * scale);
            float spacing = Math.Max(height, _stackSpacing.Value * scale);
            float iconSize = Math.Max(8.0f, (_iconSize == null ? 32.0f : _iconSize.Value) * scale);
            float iconGap = Math.Max(0.0f, (_iconGap == null ? 10.0f : _iconGap.Value) * scale);
            float centerX = Screen.width * Clamp01(_centerX.Value);
            float baseCenterY = Screen.height * Clamp01(_baseCenterY.Value);
            float shadowOffset = Math.Max(1.0f, 2.0f * scale);
            float safeMargin = Math.Max(8.0f, 16.0f * scale);
            float maximumGroupWidth = Math.Min(
                width,
                Math.Max(20.0f, Screen.width - safeMargin * 2.0f));
            int layoutCount = Math.Min(
                _notifications.Count,
                MaximumNotificationLayoutCount);
            float rowGap = Math.Max(0.0f, spacing - height);

            for (int i = 0; i < layoutCount; i++)
            {
                NotificationView view = GetOrCreateNotificationView(i);
                _notificationLayouts[i] = MeasureNotificationLayout(
                    _notifications[i],
                    view,
                    fontAsset,
                    fontSize,
                    maximumGroupWidth,
                    height,
                    iconSize,
                    iconGap);
                if (i == 0)
                {
                    _notificationTargetCenterYs[i] = baseCenterY;
                }
                else
                {
                    _notificationTargetCenterYs[i] =
                        _notificationTargetCenterYs[i - 1]
                        - _notificationLayouts[i - 1].Height * 0.5f
                        - rowGap
                        - _notificationLayouts[i].Height * 0.5f;
                }
            }

            for (int i = layoutCount - 1; i >= 0; i--)
            {
                NotificationEntry entry = _notifications[i];
                NotificationLayout layout = _notificationLayouts[i];
                NotificationView view = _notificationViews[i];
                float elapsed = now - entry.StartTime;
                float alpha = GetNotificationAlpha(entry, elapsed)
                    * Clamp01(entry.Opacity)
                    * Clamp01(_globalOpacity.Value);
                if (alpha <= 0.001f)
                {
                    view.Root.gameObject.SetActive(false);
                    continue;
                }

                view.Root.gameObject.SetActive(true);

                float minimumCenterX = safeMargin + layout.GroupWidth * 0.5f;
                float maximumCenterX = Screen.width
                    - safeMargin
                    - layout.GroupWidth * 0.5f;
                float visibleCenterX = maximumCenterX >= minimumCenterX
                    ? Clamp(centerX, minimumCenterX, maximumCenterX)
                    : Screen.width * 0.5f;
                float centerY = GetAnimatedCenterY(
                    entry,
                    _notificationTargetCenterYs[i],
                    now,
                    Math.Max(spacing, layout.Height));
                float animationScale = GetSpawnAnimationScale(elapsed);

                Color textColor = ResolveStyleColor(entry.Style, alpha);
                Color shadowColor = new Color(0.0f, 0.0f, 0.0f, alpha * 0.75f);
                UpdateNotificationView(
                    view,
                    entry,
                    layout,
                    fontAsset,
                    fontSize,
                    iconSize,
                    iconGap,
                    shadowOffset,
                    textColor,
                    shadowColor,
                    alpha);
                view.Root.anchoredPosition = new Vector2(
                    visibleCenterX,
                    Screen.height - centerY);
                view.Root.localScale = new Vector3(
                    animationScale,
                    animationScale,
                    1.0f);
                view.Root.SetAsLastSibling();
            }

            SetNotificationViewsActive(layoutCount);
        }

        private NotificationLayout MeasureNotificationLayout(
            NotificationEntry entry,
            NotificationView view,
            FontAsset fontAsset,
            float fontSize,
            float maximumGroupWidth,
            float baseHeight,
            float iconSize,
            float iconGap)
        {
            Texture2D iconTexture = GetIconTexture(entry.IconId);
            float iconWidth = iconTexture == null
                ? 0.0f
                : iconSize + iconGap;
            float maximumTextWidth = Math.Max(
                20.0f,
                maximumGroupWidth - iconWidth);

            ConfigureNotificationText(
                view.Text,
                entry.Text,
                fontAsset,
                fontSize,
                false);
            Vector2 preferred = view.Text.GetPreferredValues(
                entry.Text,
                Mathf.Infinity,
                Mathf.Infinity);
            float naturalTextWidth = Math.Max(
                1.0f,
                preferred.x + 2.0f);
            bool wrapped = naturalTextWidth > maximumTextWidth;
            float textWidth = wrapped
                ? maximumTextWidth
                : naturalTextWidth;
            ConfigureNotificationText(
                view.Text,
                entry.Text,
                fontAsset,
                fontSize,
                wrapped);
            float textHeight = baseHeight;
            if (wrapped)
            {
                Vector2 wrappedPreferred = view.Text.GetPreferredValues(
                    entry.Text,
                    textWidth,
                    Mathf.Infinity);
                textHeight = Math.Max(baseHeight, wrappedPreferred.y + 2.0f);
            }

            return new NotificationLayout
            {
                IconTexture = iconTexture,
                GroupWidth = iconWidth + textWidth,
                TextWidth = textWidth,
                Height = Math.Max(textHeight, iconSize),
                Wrapped = wrapped
            };
        }

        private void UpdateNotificationView(
            NotificationView view,
            NotificationEntry entry,
            NotificationLayout layout,
            FontAsset fontAsset,
            float fontSize,
            float iconSize,
            float iconGap,
            float shadowOffset,
            Color textColor,
            Color shadowColor,
            float notificationAlpha)
        {
            view.Root.sizeDelta = new Vector2(layout.GroupWidth, layout.Height);
            ConfigureNotificationText(
                view.Text,
                entry.Text,
                fontAsset,
                fontSize,
                layout.Wrapped);
            ConfigureNotificationText(
                view.ShadowText,
                entry.Text,
                fontAsset,
                fontSize,
                layout.Wrapped);
            view.Text.color = textColor;
            view.ShadowText.color = shadowColor;

            float textCenterX = 0.0f;
            if (layout.IconTexture != null)
            {
                float left = -layout.GroupWidth * 0.5f;
                float iconCenterX = left + iconSize * 0.5f;
                textCenterX = left + iconSize + iconGap + layout.TextWidth * 0.5f;
                view.Icon.gameObject.SetActive(true);
                view.Icon.texture = layout.IconTexture;
                view.Icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                view.Icon.rectTransform.anchoredPosition = new Vector2(iconCenterX, 0.0f);

                float iconOpacity = _iconOpacity == null ? 1.0f : Clamp01(_iconOpacity.Value);
                Color iconColor = ResolveIconColor(entry.Style, textColor, notificationAlpha);
                view.Icon.color = new Color(
                    iconColor.r,
                    iconColor.g,
                    iconColor.b,
                    iconColor.a * iconOpacity);

                bool showIconShadow = _iconShadowEnabled == null || _iconShadowEnabled.Value;
                view.IconShadow.gameObject.SetActive(showIconShadow);
                if (showIconShadow)
                {
                    float iconShadowOpacity = _iconShadowOpacity == null
                        ? 0.75f
                        : Clamp01(_iconShadowOpacity.Value);
                    view.IconShadow.texture = layout.IconTexture;
                    view.IconShadow.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                    view.IconShadow.rectTransform.anchoredPosition = new Vector2(
                        iconCenterX + shadowOffset,
                        -shadowOffset);
                    view.IconShadow.color = new Color(
                        0.0f,
                        0.0f,
                        0.0f,
                        notificationAlpha * iconShadowOpacity);
                }
            }
            else
            {
                view.Icon.gameObject.SetActive(false);
                view.IconShadow.gameObject.SetActive(false);
            }

            view.Text.rectTransform.sizeDelta = new Vector2(
                layout.TextWidth,
                layout.Height);
            view.Text.rectTransform.anchoredPosition = new Vector2(
                textCenterX,
                0.0f);
            view.ShadowText.rectTransform.sizeDelta = new Vector2(
                layout.TextWidth,
                layout.Height);
            view.ShadowText.rectTransform.anchoredPosition = new Vector2(
                textCenterX + shadowOffset,
                -shadowOffset);
        }

        private static void ConfigureNotificationText(
            TextMeshProUGUI text,
            string value,
            FontAsset fontAsset,
            float fontSize,
            bool wrapped)
        {
            if (fontAsset != null && !ReferenceEquals(text.font, fontAsset))
            {
                text.font = fontAsset;
            }

            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = wrapped
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.raycastTarget = false;
        }

        private NotificationView GetOrCreateNotificationView(int index)
        {
            EnsureNotificationCanvas();
            NotificationView view = _notificationViews[index];
            if (view != null)
            {
                return view;
            }

            GameObject rootObject = new GameObject(
                "Notification" + index.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform));
            rootObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(_overlayRoot, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);

            RawImage iconShadow = CreateNotificationIcon(root, "IconShadow");
            RawImage icon = CreateNotificationIcon(root, "Icon");
            TextMeshProUGUI shadowText = CreateNotificationText(root, "ShadowText");
            TextMeshProUGUI text = CreateNotificationText(root, "Text");
            view = new NotificationView
            {
                Root = root,
                IconShadow = iconShadow,
                Icon = icon,
                ShadowText = shadowText,
                Text = text
            };
            _notificationViews[index] = view;
            return view;
        }

        private void EnsureNotificationCanvas()
        {
            if (_overlayRoot != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                "GrailFloatingTextCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30001;
            _overlayRoot = canvasObject.GetComponent<RectTransform>();
        }

        private static RawImage CreateNotificationIcon(RectTransform parent, string name)
        {
            GameObject iconObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            iconObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            RawImage image = iconObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateNotificationText(
            RectTransform parent,
            string name)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            return text;
        }

        private void SetNotificationViewsActive(int activeCount)
        {
            for (int i = Math.Max(0, activeCount); i < _notificationViews.Length; i++)
            {
                NotificationView view = _notificationViews[i];
                if (view != null && view.Root != null)
                {
                    view.Root.gameObject.SetActive(false);
                }
            }
        }

        private void ReleaseNotificationViews()
        {
            ReleaseQuickWheelPanelView();
            if (_overlayRoot != null)
            {
                Destroy(_overlayRoot.gameObject);
                _overlayRoot = null;
            }

            for (int i = 0; i < _notificationViews.Length; i++)
            {
                _notificationViews[i] = null;
            }
        }

        private Texture2D GetIconTexture(string iconId)
        {
            if (_iconsEnabled == null || !_iconsEnabled.Value || string.IsNullOrEmpty(iconId))
            {
                return null;
            }

            Texture2D texture;
            return _iconTexturesById.TryGetValue(iconId, out texture) ? texture : null;
        }

        private bool IsCategoryEnabled(string category)
        {
            ConfigEntry<bool> entry;
            if (_categoryEnabledByName.TryGetValue(category, out entry) && entry != null)
            {
                return entry.Value;
            }

            return true;
        }

        private SourceSettings GetSourceSettings(string sourceId)
        {
            if (_perSourceControlsEnabled == null || !_perSourceControlsEnabled.Value)
            {
                return null;
            }

            SourceSettings settings;
            if (_sourceSettingsById.TryGetValue(sourceId, out settings))
            {
                return settings;
            }

            string section = "9. Sources." + ToConfigName(sourceId);
            settings = new SourceSettings
            {
                Enabled = Config.Bind(section, "Enabled", true, "Allow this source to show Grail Floating Text messages."),
                ThrottleSeconds = Config.Bind(
                    section,
                    "ThrottleSeconds",
                    GetDefaultSourceThrottleSeconds(),
                    new ConfigDescription(
                        "Minimum seconds between non-high-priority messages from this source.",
                        new AcceptableValueRange<float>(0.0f, 10.0f))),
                DurationMultiplier = Config.Bind(
                    section,
                    "DurationMultiplier",
                    GetDefaultSourceDurationMultiplier(),
                    new ConfigDescription(
                        "Multiplier applied to this source's requested display duration.",
                        new AcceptableValueRange<float>(0.1f, 5.0f))),
                Diagnostics = Config.Bind(section, "Diagnostics", false, "Log accepted messages from this source.")
            };

            _sourceSettingsById[sourceId] = settings;
            Config.Save();
            return settings;
        }

        private bool IsSourceEnabled(SourceSettings settings)
        {
            return settings == null || settings.Enabled == null || settings.Enabled.Value;
        }

        private float GetSourceThrottleSeconds(SourceSettings settings)
        {
            if (settings != null && settings.ThrottleSeconds != null)
            {
                return Math.Max(0.0f, settings.ThrottleSeconds.Value);
            }

            return GetDefaultSourceThrottleSeconds();
        }

        private float GetDefaultSourceThrottleSeconds()
        {
            return _defaultSourceThrottleSeconds == null ? 0.05f : Math.Max(0.0f, _defaultSourceThrottleSeconds.Value);
        }

        private float GetSourceDurationMultiplier(SourceSettings settings)
        {
            if (settings != null && settings.DurationMultiplier != null)
            {
                return Clamp(settings.DurationMultiplier.Value, 0.1f, 5.0f);
            }

            return GetDefaultSourceDurationMultiplier();
        }

        private float GetDefaultSourceDurationMultiplier()
        {
            return _defaultSourceDurationMultiplier == null ? 1.0f : Clamp(_defaultSourceDurationMultiplier.Value, 0.1f, 5.0f);
        }

        private float GetDurationBucketSeconds(DurationBucket bucket)
        {
            switch (bucket)
            {
                case DurationBucket.VeryShort:
                    return GetConfiguredDuration(_veryShortDurationSeconds, DefaultVeryShortDurationSeconds);
                case DurationBucket.Short:
                    return GetConfiguredDuration(_shortDurationSeconds, DefaultShortDurationSeconds);
                case DurationBucket.Long:
                    return GetConfiguredDuration(_longDurationSeconds, DefaultLongDurationSeconds);
                case DurationBucket.VeryLong:
                    return GetConfiguredDuration(_veryLongDurationSeconds, DefaultVeryLongDurationSeconds);
                case DurationBucket.System:
                    return GetConfiguredDuration(_systemDurationSeconds, DefaultSystemDurationSeconds);
                default:
                    return GetConfiguredDuration(_mediumDurationSeconds, DefaultMediumDurationSeconds);
            }
        }

        private void ScanLoadedModCompatibility()
        {
            if (_modCompatibilityScanCompleted)
            {
                return;
            }

            _modCompatibilityScanCompleted = true;
            if (_notifyModCompatibility == null
                || !_notifyModCompatibility.Value)
            {
                return;
            }

            ScanGloriousUiCompatibility();
            ScanEyesInTheDarkCompatibility();
            ScanDamageNumberCompatibility();
            ScanDynamicCrosshairCompatibility();
        }

        private void ScanEyesInTheDarkCompatibility()
        {
            bool eyesLoaded = IsPluginOrAssemblyLoaded(
                    EyesInTheDarkPluginGuid,
                    EyesInTheDarkAssemblyName);
            if (!eyesLoaded)
            {
                return;
            }

            if (IsPluginOrAssemblyLoaded(
                    WyrdHuntPluginGuid,
                    WyrdHuntAssemblyName))
            {
                ShowCompatibilityNotice(
                    "eyes-in-the-dark-wyrd-hunt",
                    "Wyrd Hunt is flagged as incompatible with Eyes in the Dark.");
            }
            if (IsPluginOrAssemblyLoaded(
                    CustomTimescalePluginGuid,
                    CustomTimescaleAssemblyName))
            {
                ShowCompatibilityNotice(
                    "eyes-in-the-dark-custom-timescale",
                    "Custom Timescale is flagged as incompatible with Eyes in the Dark.");
            }
        }

        private void ScanGloriousUiCompatibility()
        {
            if (!IsPluginOrAssemblyLoaded(
                GloriousUiPluginGuid,
                GloriousUiAssemblyName))
            {
                return;
            }

            for (int i = 0;
                i < GloriousUiIncompatibleAssemblyNames.Length;
                i++)
            {
                string assemblyName =
                    GloriousUiIncompatibleAssemblyNames[i];
                if (!IsPluginOrAssemblyLoaded(
                    assemblyName,
                    assemblyName))
                {
                    continue;
                }

                string dllName = assemblyName + ".dll";
                string text = dllName
                    + " is flagged as incompatible with Glorious UI.";
                ShowCompatibilityNotice(
                    "glorious-ui-" + assemblyName,
                    text);
            }

            bool gloriousEnabled;
            bool controlsEquipmentLoadouts;
            bool controlsQuickWheel;
            if (TryGetLoadedBoolean(
                    GloriousUiPluginGuid,
                    "1. Core",
                    "Enabled",
                    out gloriousEnabled)
                && gloriousEnabled
                && TryGetLoadedBoolean(
                    GloriousUiPluginGuid,
                    "5. Equipment Panel",
                    "ControlEquipmentWeaponLoadouts",
                    out controlsEquipmentLoadouts)
                && controlsEquipmentLoadouts
                && TryGetLoadedBoolean(
                    GloriousUiPluginGuid,
                    "5. Equipment Panel",
                    "ControlQuickUseWheelLoadouts",
                    out controlsQuickWheel)
                && controlsQuickWheel
                && IsPluginOrAssemblyLoaded(BetterUiPluginGuid, BetterUiPluginGuid))
            {
                List<string> enabledSettings = new List<string>();
                AddEnabledSetting(
                    BetterUiPluginGuid,
                    "8. Quick Wheel",
                    "QuickSlotEffectEnabled",
                    enabledSettings);
                AddEnabledSetting(
                    BetterUiPluginGuid,
                    "8. Quick Wheel",
                    "AmmoCounterEnabled",
                    enabledSettings);
                AddEnabledSetting(
                    BetterUiPluginGuid,
                    "8. Quick Wheel",
                    "ArrowCycleEnabled",
                    enabledSettings);
                if (enabledSettings.Count > 0)
                {
                    ShowCompatibilityNotice(
                        "glorious-ui-better-ui-quick-wheel",
                        "Certain enabled Better UI settings are flagged as "
                            + "incompatible with Glorious UI. See description.",
                        "Enabled Better UI settings: "
                            + string.Join(", ", enabledSettings.ToArray())
                            + ".");
                }
            }

            bool controlsHeroHud;
            bool immersiveHudEnabled;
            if (gloriousEnabled
                && TryGetLoadedBoolean(
                    GloriousUiPluginGuid,
                    "2. HUD",
                    "ControlHeroHud",
                    out controlsHeroHud)
                && controlsHeroHud
                && TryGetLoadedBoolean(
                    ImmersiveHudPluginGuid,
                    "General",
                    "Enabled",
                    out immersiveHudEnabled)
                && immersiveHudEnabled)
            {
                bool forceVanillaHeroHud;
                bool forceVanillaHeroHudKnown = TryGetLoadedBoolean(
                        ImmersiveHudPluginGuid,
                        "HUD Elements",
                        "ForceVanillaHeroHud",
                        out forceVanillaHeroHud);
                ShowCompatibilityNotice(
                    "glorious-ui-immersive-hud",
                    "Certain enabled Immersive HUD settings are flagged as "
                        + "incompatible with Glorious UI. See description.",
                    "Immersive HUD Enabled=true"
                        + (forceVanillaHeroHudKnown
                            ? "; ForceVanillaHeroHud="
                                + forceVanillaHeroHud.ToString()
                            : string.Empty)
                        + ".");
            }
        }

        private void ScanDamageNumberCompatibility()
        {
            if (!IsPluginOrAssemblyLoaded(
                    SteelAndBonePluginGuid,
                    SteelAndBoneAssemblyName))
            {
                return;
            }

            bool steelAndBoneEnabled;
            bool damageNumbersEnabled;
            if (!TryGetLoadedBoolean(
                    SteelAndBonePluginGuid,
                    "1. Core",
                    "Enabled",
                    out steelAndBoneEnabled)
                || !steelAndBoneEnabled
                || !TryGetLoadedBoolean(
                    SteelAndBonePluginGuid,
                    "3. Feedback",
                    "DamageNumbersEnabled",
                    out damageNumbersEnabled)
                || !damageNumbersEnabled)
            {
                return;
            }

            if (IsPluginOrAssemblyLoaded("DamageNumbers", "DamageNumbers"))
            {
                ShowCompatibilityNotice(
                    "steel-and-bone-damage-numbers",
                    "DamageNumbers.dll is flagged as incompatible with Steel "
                        + "and Bone damage numbers.",
                    "Steel and Bone DamageNumbersEnabled=true.");
            }

            bool immersiveHudEnabled;
            bool immersiveDamageNumbersEnabled;
            if (TryGetLoadedBoolean(
                    ImmersiveHudPluginGuid,
                    "General",
                    "Enabled",
                    out immersiveHudEnabled)
                && immersiveHudEnabled
                && TryGetLoadedBoolean(
                    ImmersiveHudPluginGuid,
                    "HUD Elements",
                    "ShowDamageNumbers",
                    out immersiveDamageNumbersEnabled)
                && immersiveDamageNumbersEnabled)
            {
                ShowCompatibilityNotice(
                    "steel-and-bone-immersive-hud-damage-numbers",
                    "Immersive HUD damage numbers are flagged as incompatible "
                        + "with Steel and Bone. See description.",
                    "Steel and Bone DamageNumbersEnabled=true; Immersive HUD "
                        + "ShowDamageNumbers=true.");
            }
        }

        private void ScanDynamicCrosshairCompatibility()
        {
            if (IsPluginOrAssemblyLoaded(
                    DynamicCrosshairPluginGuid,
                    DynamicCrosshairAssemblyName)
                && IsPluginOrAssemblyLoaded(
                    "owrocc.ModifyCrosshair",
                    "owrocc.ModifyCrosshair"))
            {
                ShowCompatibilityNotice(
                    "dishonored-dynamic-crosshair-modify-crosshair",
                    "owrocc.ModifyCrosshair.dll is flagged as incompatible with "
                        + "Dishonored Dynamic Crosshair.");
            }
        }

        private static void AddEnabledSetting(
            string pluginGuid,
            string section,
            string settingName,
            List<string> enabledSettings)
        {
            bool enabled;
            if (TryGetLoadedBoolean(
                    pluginGuid,
                    section,
                    settingName,
                    out enabled)
                && enabled)
            {
                enabledSettings.Add(settingName);
            }
        }

        private static bool TryGetLoadedBoolean(
            string pluginGuid,
            string section,
            string settingName,
            out bool value)
        {
            value = false;
            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    pluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return false;
            }

            ConfigEntry<bool> entry;
            if (!pluginInfo.Instance.Config.TryGetEntry<bool>(
                    section,
                    settingName,
                    out entry)
                || entry == null)
            {
                return false;
            }

            value = entry.Value;
            return true;
        }

        private void ShowCompatibilityNotice(
            string ruleId,
            string text,
            string diagnosticDetails = null)
        {
            string eventId = ModCompatibilityEventIdPrefix
                + NormalizeEventId(ruleId);
            NotificationApi.TryShowEvent(
                PluginGuid,
                eventId,
                text,
                "System",
                "System",
                "High",
                eventId,
                "system",
                "System",
                "OnMainMenu",
                -1.0f,
                1.0f);
            Logger.LogWarning(
                string.IsNullOrWhiteSpace(diagnosticDetails)
                    ? text
                    : text + " " + diagnosticDetails);
        }

        private static bool IsPluginOrAssemblyLoaded(
            string pluginGuid,
            string assemblyName)
        {
            foreach (KeyValuePair<string, PluginInfo> plugin in
                Chainloader.PluginInfos)
            {
                if ((!string.IsNullOrWhiteSpace(pluginGuid)
                        && string.Equals(
                            plugin.Key,
                            pluginGuid,
                            StringComparison.OrdinalIgnoreCase))
                    || (plugin.Value != null
                        && string.Equals(
                            Path.GetFileNameWithoutExtension(
                                plugin.Value.Location),
                            assemblyName,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                AssemblyName loadedName = assemblies[i].GetName();
                if (loadedName != null
                    && string.Equals(
                        loadedName.Name,
                        assemblyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetConfiguredDuration(ConfigEntry<float> entry, float fallback)
        {
            return entry == null ? fallback : Math.Max(DefaultMinimumDurationSeconds, entry.Value);
        }

        private static DurationBucket ParseDurationBucket(string value)
        {
            string normalized = NormalizeEventId(value);
            if (normalized == "very_short" || normalized == "veryshort" || normalized == "vs")
            {
                return DurationBucket.VeryShort;
            }

            if (normalized == "short" || normalized == "s")
            {
                return DurationBucket.Short;
            }

            if (normalized == "long" || normalized == "l")
            {
                return DurationBucket.Long;
            }

            if (normalized == "very_long" || normalized == "verylong" || normalized == "vl")
            {
                return DurationBucket.VeryLong;
            }

            if (normalized == "system" || normalized == "sys")
            {
                return DurationBucket.System;
            }

            return DurationBucket.Medium;
        }

        private static DeliveryPoint ResolveDefaultDeliveryPoint(string eventId)
        {
            string normalizedEventId = NormalizeEventId(eventId);
            if (EventIdEquals(normalizedEventId, ConfigResetEventId))
            {
                return DeliveryPoint.OnLoad;
            }

            if (EventIdEquals(normalizedEventId, LoadTimeErrorEventId))
            {
                return DeliveryPoint.OnMainMenu;
            }

            return DeliveryPoint.Immediate;
        }

        private static bool TryParseDeliveryPoint(string value, out DeliveryPoint deliveryPoint)
        {
            deliveryPoint = DeliveryPoint.Immediate;
            if (string.IsNullOrWhiteSpace(value)
                || string.Equals(value.Trim(), "Immediate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value.Trim(), "OnMainMenu", StringComparison.OrdinalIgnoreCase))
            {
                deliveryPoint = DeliveryPoint.OnMainMenu;
                return true;
            }

            if (string.Equals(value.Trim(), "OnLoad", StringComparison.OrdinalIgnoreCase))
            {
                deliveryPoint = DeliveryPoint.OnLoad;
                return true;
            }

            return false;
        }

        private bool IsSourceDiagnosticsEnabled(SourceSettings settings)
        {
            return settings != null && settings.Diagnostics != null && settings.Diagnostics.Value;
        }

        private bool ShouldThrottleSource(string sourceId, int priorityValue, float now, float throttleSeconds)
        {
            if (priorityValue >= PriorityHigh || throttleSeconds <= 0.001f)
            {
                return false;
            }

            float lastTime;
            return _lastNotificationTimeBySource.TryGetValue(sourceId, out lastTime) &&
                now - lastTime < throttleSeconds;
        }

        private bool IsDuplicateSuppressed(string sourceId, string eventId, string text, string style, string category, string iconId, float now)
        {
            float suppressSeconds = _duplicateSuppressSeconds == null ? 0.0f : Math.Max(0.0f, _duplicateSuppressSeconds.Value);
            if (suppressSeconds <= 0.001f)
            {
                return false;
            }

            for (int i = 0; i < _notifications.Count; i++)
            {
                NotificationEntry entry = _notifications[i];
                if (now - entry.StartTime > suppressSeconds)
                {
                    continue;
                }

                if (string.Equals(entry.SourceId, sourceId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.EventId, eventId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Text, text, StringComparison.Ordinal) &&
                    string.Equals(entry.Style, style, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.IconId, iconId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private NotificationEntry FindCollapsibleEntry(string sourceId, string collapseKey)
        {
            if (string.IsNullOrEmpty(collapseKey))
            {
                return null;
            }

            for (int i = 0; i < _notifications.Count; i++)
            {
                NotificationEntry entry = _notifications[i];
                if (string.Equals(entry.SourceId, sourceId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.CollapseKey, collapseKey, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private void InsertEntry(NotificationEntry entry)
        {
            int index = 0;
            while (index < _notifications.Count && ShouldDisplayBefore(_notifications[index], entry))
            {
                index++;
            }

            _notifications.Insert(index, entry);
        }

        private static bool ShouldDisplayBefore(NotificationEntry existing, NotificationEntry incoming)
        {
            int eventOrderComparison = CompareEventDisplayOrder(existing.EventId, incoming.EventId);
            if (eventOrderComparison != 0)
            {
                return eventOrderComparison < 0;
            }

            if (existing.PriorityValue != incoming.PriorityValue)
            {
                return existing.PriorityValue > incoming.PriorityValue;
            }

            return existing.Sequence > incoming.Sequence;
        }

        private static int CompareEventDisplayOrder(string leftEventId, string rightEventId)
        {
            bool leftIsKillingBlow = EventIdEquals(leftEventId, KillingBlowEventId);
            bool rightIsKillingBlow = EventIdEquals(rightEventId, KillingBlowEventId);
            bool leftIsDefaultXp = EventIdEquals(leftEventId, DefaultXpGainEventId);
            bool rightIsDefaultXp = EventIdEquals(rightEventId, DefaultXpGainEventId);

            if (leftIsKillingBlow && rightIsDefaultXp)
            {
                return -1;
            }

            if (leftIsDefaultXp && rightIsKillingBlow)
            {
                return 1;
            }

            return 0;
        }

        private static bool EventIdEquals(string eventId, string expectedEventId)
        {
            return string.Equals(eventId, NormalizeEventId(expectedEventId), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSourceId(string sourceId)
        {
            return string.IsNullOrWhiteSpace(sourceId) ? "unknown" : sourceId.Trim();
        }

        private static string NormalizeEventId(string eventId)
        {
            return string.IsNullOrWhiteSpace(eventId) ? string.Empty : NormalizeIconToken(eventId);
        }

        private static string NormalizeCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "General";
            }

            if (category.Equals("Combat", StringComparison.OrdinalIgnoreCase))
            {
                return "Combat";
            }

            if (category.Equals("Reward", StringComparison.OrdinalIgnoreCase))
            {
                return "Reward";
            }

            if (category.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                return "Status";
            }

            if (category.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                return "System";
            }

            if (category.Equals("Debug", StringComparison.OrdinalIgnoreCase))
            {
                return "Debug";
            }

            return "General";
        }

        private static string NormalizeStyle(string style, string category, int priorityValue)
        {
            if (!string.IsNullOrWhiteSpace(style))
            {
                return style.Trim();
            }

            if (priorityValue >= PriorityCritical)
            {
                return "Critical";
            }

            if (string.Equals(category, "Reward", StringComparison.OrdinalIgnoreCase))
            {
                return "Reward";
            }

            if (string.Equals(category, "Status", StringComparison.OrdinalIgnoreCase))
            {
                return "Status";
            }

            if (string.Equals(category, "Combat", StringComparison.OrdinalIgnoreCase))
            {
                return "Combat";
            }

            if (string.Equals(category, "System", StringComparison.OrdinalIgnoreCase))
            {
                return "System";
            }

            if (string.Equals(category, "Debug", StringComparison.OrdinalIgnoreCase))
            {
                return "Debug";
            }

            return "Default";
        }

        private string ResolveEventStyle(string eventId, string fallbackStyle)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return fallbackStyle;
            }

            if (eventId.StartsWith(
                    "vanilla_wyrd_",
                    StringComparison.OrdinalIgnoreCase)
                && IsPluginOrAssemblyLoaded(
                    EyesInTheDarkPluginGuid,
                    EyesInTheDarkAssemblyName))
            {
                return ResolveEyesWyrdStyle();
            }

            for (int i = 0; i < _colorGroups.Count; i++)
            {
                ColorGroupSettings group = _colorGroups[i];
                if (group != null && EventListContains(group.Events == null ? string.Empty : group.Events.Value, eventId))
                {
                    return group.Name;
                }
            }

            return fallbackStyle;
        }

        private static string ResolveEyesWyrdStyle()
        {
            try
            {
                PluginInfo pluginInfo;
                if (Chainloader.PluginInfos.TryGetValue(
                        EyesInTheDarkPluginGuid,
                        out pluginInfo)
                    && pluginInfo != null
                    && pluginInfo.Instance != null)
                {
                    ConfigEntryBase palette = pluginInfo.Instance.Config[
                        "8. Wyrd Visuals",
                        "WyrdnessPalette"];
                    if (palette != null
                        && string.Equals(
                            Convert.ToString(
                                palette.BoxedValue,
                                CultureInfo.InvariantCulture),
                            "NativeOrange",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "Orange";
                    }
                }
            }
            catch
            {
            }

            return "Purple";
        }

        private static bool EventListContains(string eventList, string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventList) || string.IsNullOrEmpty(eventId))
            {
                return false;
            }

            string[] parts = eventList.Split(new[] { ';', ',', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(NormalizeEventId(parts[i]), eventId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeIconId(string iconId, string style, string category, int priorityValue)
        {
            if (string.IsNullOrWhiteSpace(iconId))
            {
                return GetDefaultIconId(style, category, priorityValue);
            }

            string normalized = NormalizeIconToken(iconId);
            if (normalized == "" ||
                normalized == "none" ||
                normalized == "off" ||
                normalized == "hidden")
            {
                return string.Empty;
            }

            if (normalized == "auto" || normalized == "default")
            {
                return GetDefaultIconId(style, category, priorityValue);
            }

            if (normalized == "onehanded" || normalized == "one_handed_weapon")
            {
                normalized = "one_handed";
            }
            else if (normalized == "twohanded" || normalized == "two_handed_weapon")
            {
                normalized = "two_handed";
            }
            else if (normalized == "bow" || normalized == "bow_arrow")
            {
                normalized = "archery";
            }
            else if (normalized == "shield_bash")
            {
                normalized = "shield";
            }
            else if (normalized == "parried")
            {
                normalized = "parry";
            }
            else if (normalized == "fist" || normalized == "fists")
            {
                normalized = "unarmed";
            }
            else if (normalized == "spell" || normalized == "spells" || normalized == "arcane")
            {
                normalized = "magic";
            }
            else if (normalized == "sleep" || normalized == "slept" || normalized == "resting")
            {
                normalized = "rest";
            }
            else if (normalized == "error")
            {
                normalized = "critical";
            }

            return BuiltInIconIdSet.Contains(normalized)
                ? normalized
                : GetDefaultIconId(style, category, priorityValue);
        }

        private static string NormalizeIconToken(string raw)
        {
            StringBuilder builder = new StringBuilder(raw.Length);
            bool previousSeparator = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                    previousSeparator = false;
                    continue;
                }

                if (ch == '_' || ch == '-' || char.IsWhiteSpace(ch))
                {
                    if (!previousSeparator && builder.Length > 0)
                    {
                        builder.Append('_');
                        previousSeparator = true;
                    }
                }
            }

            return builder.ToString().Trim('_');
        }

        private static string GetDefaultIconId(string style, string category, int priorityValue)
        {
            if (StyleEquals(style, "Wyrd"))
            {
                return "wyrd";
            }

            if (StyleEquals(style, "Reward"))
            {
                return "reward";
            }

            if (StyleEquals(style, "Gold"))
            {
                return "reward";
            }

            if (StyleEquals(style, "Combat"))
            {
                return "combat";
            }

            if (StyleEquals(style, "Orange"))
            {
                return "warning";
            }

            if (StyleEquals(style, "Warning"))
            {
                return "warning";
            }

            if (StyleEquals(style, "Red") || StyleEquals(style, "Error") || StyleEquals(style, "Critical") || priorityValue >= PriorityCritical)
            {
                return "critical";
            }

            if (StyleEquals(style, "System"))
            {
                return "system";
            }

            if (StyleEquals(style, "Pale"))
            {
                return "system";
            }

            if (StyleEquals(style, "Debug"))
            {
                return "debug";
            }

            if (StyleEquals(style, "Gray"))
            {
                return "debug";
            }

            if (StyleEquals(style, "Rest"))
            {
                return "rest";
            }

            if (StyleEquals(style, "Blue"))
            {
                return "status";
            }

            if (StyleEquals(style, "Purple"))
            {
                return "wyrd";
            }

            if (string.Equals(category, "Reward", StringComparison.OrdinalIgnoreCase))
            {
                return "reward";
            }

            if (string.Equals(category, "Combat", StringComparison.OrdinalIgnoreCase))
            {
                return "combat";
            }

            if (string.Equals(category, "Status", StringComparison.OrdinalIgnoreCase))
            {
                return "status";
            }

            if (string.Equals(category, "System", StringComparison.OrdinalIgnoreCase))
            {
                return "system";
            }

            if (string.Equals(category, "Debug", StringComparison.OrdinalIgnoreCase))
            {
                return "debug";
            }

            return "general";
        }

        private static string NormalizePriority(string priority)
        {
            int value = ResolvePriorityValue(priority);
            if (value >= PriorityCritical)
            {
                return "Critical";
            }

            if (value >= PriorityHigh)
            {
                return "High";
            }

            if (value <= PriorityLow)
            {
                return "Low";
            }

            return "Normal";
        }

        private static int ResolvePriorityValue(string priority)
        {
            if (string.IsNullOrWhiteSpace(priority))
            {
                return PriorityNormal;
            }

            if (priority.Equals("Critical", StringComparison.OrdinalIgnoreCase) ||
                priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase))
            {
                return PriorityCritical;
            }

            if (priority.Equals("High", StringComparison.OrdinalIgnoreCase))
            {
                return PriorityHigh;
            }

            if (priority.Equals("Low", StringComparison.OrdinalIgnoreCase) ||
                priority.Equals("Quiet", StringComparison.OrdinalIgnoreCase))
            {
                return PriorityLow;
            }

            return PriorityNormal;
        }

        private static string ToConfigName(string value)
        {
            string normalized = NormalizeSourceId(value);
            char[] chars = normalized.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '-' && ch != '_')
                {
                    chars[i] = '-';
                }
            }

            string configName = new string(chars).Trim('-', '.', '_');
            return string.IsNullOrWhiteSpace(configName) ? "unknown" : configName;
        }

        private void BindConfig()
        {
            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch for the shared Grail Floating Text overlay.");
            _notifyModCompatibility = Config.Bind(
                "1. Core",
                "NotifyModCompatibility",
                true,
                "Show soft system notices when a loaded Grailwright mod has documented incompatible DLLs loaded alongside it. Detection treats the Grailwright mod as the preferred implementation but never disables another plugin automatically.");
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));

            _scale = Config.Bind("2. Layout", "Scale", 1.2f, new ConfigDescription("Scale multiplier for all floating text.", new AcceptableValueRange<float>(0.1f, 3.0f)));
            _fontSize = Config.Bind("2. Layout", "FontSize", 20, new ConfigDescription("Base font size before scale is applied.", new AcceptableValueRange<int>(8, 72)));
            _fontMode = Config.Bind("2. Layout", "FontMode", FontMode.GameDefault, "Font used by the overlay. GameDefault follows the game's Accessibility font choice, Sans forces the simple game font, Serif forces the stylized game font, and ImguiDefault keeps Unity's IMGUI fallback font.");
            _centerX = Config.Bind("2. Layout", "CenterX", 0.5f, new ConfigDescription("Horizontal center as a fraction of screen width.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _baseCenterY = Config.Bind("2. Layout", "BaseCenterY", 0.25f, new ConfigDescription("Vertical center for the newest notification as a fraction of screen height.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _width = Config.Bind("2. Layout", "Width", 1040.0f, new ConfigDescription("Maximum centered icon-and-text group width before wrapping, prior to scale and screen-safe limits.", new AcceptableValueRange<float>(100.0f, 1600.0f)));
            _stackSpacing = Config.Bind("2. Layout", "StackSpacing", 34.0f, new ConfigDescription("Vertical distance between stacked active notifications before scale is applied.", new AcceptableValueRange<float>(16.0f, 160.0f)));
            _maximumVisibleNotifications = Config.Bind("2. Layout", "MaximumVisibleNotifications", 16, new ConfigDescription("Maximum active notifications kept on screen at once. Oldest entries are dropped first.", new AcceptableValueRange<int>(1, 24)));

            _defaultDurationSeconds = Config.Bind("3. Timing", "DefaultDurationSeconds", DefaultMediumDurationSeconds, new ConfigDescription("Default display duration used when a caller does not request one.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _defaultFadeSeconds = Config.Bind("3. Timing", "DefaultFadeSeconds", 0.25f, new ConfigDescription("Default fade-in duration and medium-duration fade-out baseline used when a caller does not request one. Fade-out scales with total display duration, up to twice this value.", new AcceptableValueRange<float>(0.0f, 5.0f)));
            _veryShortDurationSeconds = Config.Bind("3. Timing", "VeryShortDurationSeconds", DefaultVeryShortDurationSeconds, new ConfigDescription("Display duration for very short event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _shortDurationSeconds = Config.Bind("3. Timing", "ShortDurationSeconds", DefaultShortDurationSeconds, new ConfigDescription("Display duration for short event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _mediumDurationSeconds = Config.Bind("3. Timing", "MediumDurationSeconds", DefaultMediumDurationSeconds, new ConfigDescription("Display duration for medium event-bucket messages. Event callbacks default to this bucket.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _longDurationSeconds = Config.Bind("3. Timing", "LongDurationSeconds", DefaultLongDurationSeconds, new ConfigDescription("Display duration for long event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _veryLongDurationSeconds = Config.Bind("3. Timing", "VeryLongDurationSeconds", DefaultVeryLongDurationSeconds, new ConfigDescription("Display duration for very long event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _systemDurationSeconds = Config.Bind("3. Timing", "SystemDurationSeconds", DefaultSystemDurationSeconds, new ConfigDescription("Display duration for startup, config reset, and load-time system messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _globalOpacity = Config.Bind("3. Timing", "GlobalOpacity", 0.9f, new ConfigDescription("Opacity multiplier applied to every floating text entry.", new AcceptableValueRange<float>(0.0f, 1.0f)));

            _spawnAnimationEnabled = Config.Bind("4. Animation", "SpawnAnimationEnabled", true, "Play a short pop-in scale animation when notifications appear.");
            _spawnStartScale = Config.Bind("4. Animation", "SpawnStartScale", 0.7f, new ConfigDescription("Initial scale for the notification pop-in animation.", new AcceptableValueRange<float>(0.1f, 3.0f)));
            _spawnOvershootScale = Config.Bind("4. Animation", "SpawnOvershootScale", 1.12f, new ConfigDescription("Largest scale reached before the pop-in animation settles to normal size.", new AcceptableValueRange<float>(0.1f, 3.0f)));
            _spawnAnimationSeconds = Config.Bind("4. Animation", "SpawnAnimationSeconds", 0.2f, new ConfigDescription("Duration of the pop-in scale animation.", new AcceptableValueRange<float>(0.0f, 2.0f)));
            _stackMoveAnimationSeconds = Config.Bind("4. Animation", "StackMoveAnimationSeconds", 0.16f, new ConfigDescription("Time used for existing notifications to glide into their new stacked positions.", new AcceptableValueRange<float>(0.0f, 2.0f)));

            _duplicateSuppressSeconds = Config.Bind("5. Queue", "DuplicateSuppressSeconds", 0.15f, new ConfigDescription("Drop identical non-collapsed messages from the same source within this many seconds. Set to 0 to disable.", new AcceptableValueRange<float>(0.0f, 5.0f)));

            _categoryEnabledByName["General"] = Config.Bind("6. Categories", "GeneralEnabled", true, "Show general floating text entries.");
            _categoryEnabledByName["Combat"] = Config.Bind("6. Categories", "CombatEnabled", true, "Show combat-category floating text entries.");
            _categoryEnabledByName["Reward"] = Config.Bind("6. Categories", "RewardEnabled", true, "Show reward-category floating text entries.");
            _categoryEnabledByName["Status"] = Config.Bind("6. Categories", "StatusEnabled", true, "Show status-category floating text entries.");
            _categoryEnabledByName["System"] = Config.Bind("6. Categories", "SystemEnabled", true, "Show system-category floating text entries.");
            _categoryEnabledByName["Debug"] = Config.Bind("6. Categories", "DebugEnabled", false, "Show debug-category floating text entries.");

            _perSourceControlsEnabled = Config.Bind("7. Source Defaults", "PerSourceControlsEnabled", true, "Create per-source config sections when mods send messages.");
            _defaultSourceThrottleSeconds = Config.Bind("7. Source Defaults", "DefaultThrottleSeconds", 0.05f, new ConfigDescription("Minimum seconds between non-high-priority messages from the same source before a per-source override exists.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _defaultSourceDurationMultiplier = Config.Bind("7. Source Defaults", "DefaultDurationMultiplier", 1.0f, new ConfigDescription("Default multiplier applied to requested display duration before a per-source override exists.", new AcceptableValueRange<float>(0.1f, 5.0f)));

            _iconsEnabled = Config.Bind("8. Icons", "IconsEnabled", true, "Draw built-in icon masks beside floating text when an icon is resolved.");
            _iconSize = Config.Bind("8. Icons", "IconSize", 32.0f, new ConfigDescription("Icon size before scale is applied.", new AcceptableValueRange<float>(8.0f, 96.0f)));
            _iconGap = Config.Bind("8. Icons", "IconGap", 10.0f, new ConfigDescription("Horizontal gap between icon and text before scale is applied.", new AcceptableValueRange<float>(0.0f, 64.0f)));
            _iconOpacity = Config.Bind("8. Icons", "IconOpacity", 0.95f, new ConfigDescription("Opacity multiplier applied to icon masks.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _iconShadowEnabled = Config.Bind("8. Icons", "IconShadowEnabled", true, "Draw a black offset shadow behind icons. Text shadows are controlled separately and remain enabled.");
            _iconShadowOpacity = Config.Bind("8. Icons", "IconShadowOpacity", 0.75f, new ConfigDescription("Opacity multiplier applied to icon shadows.", new AcceptableValueRange<float>(0.0f, 1.0f)));

            _notifyRestDuration = Config.Bind("9. Default Game Events", "NotifyRestDuration", true, "Show how long the hero actually rested after sleep is started.");
            _notifyInterruptedRestDuration = Config.Bind("9. Default Game Events", "NotifyInterruptedRestDuration", true, "Use interrupted wording when rest ends early due to a Wyrd interruption.");
            _restDurationTextFormat = Config.Bind("9. Default Game Events", "RestDurationTextFormat", "Rested {duration}", "Floating text for completed rest. Tokens: {duration}.");
            _restInterruptedTextFormat = Config.Bind("9. Default Game Events", "RestInterruptedTextFormat", "Rest interrupted: {duration} slept", "Floating text for interrupted rest. Tokens: {duration}.");
            _restNotificationMinimumMinutes = Config.Bind("9. Default Game Events", "RestNotificationMinimumMinutes", 1, new ConfigDescription("Minimum actual rest duration in minutes required before showing rest text.", new AcceptableValueRange<int>(0, 1440)));
            _notifyBlockedDamage = Config.Bind("9. Default Game Events", "NotifyBlockedDamage", false, "Show optional throttled combat text when the hero blocks damage.");
            _notifyParriedDamage = Config.Bind("9. Default Game Events", "NotifyParriedDamage", true, "Show optional throttled combat text when the hero parries damage.");
            _combatDefenseMinimumDamage = Config.Bind("9. Default Game Events", "CombatDefenseMinimumDamage", 1.0f, new ConfigDescription("Minimum blocked/parried damage required before showing combat defense text.", new AcceptableValueRange<float>(0.0f, 10000.0f)));
            _combatDefenseCooldownSeconds = Config.Bind("9. Default Game Events", "CombatDefenseCooldownSeconds", 0.75f, new ConfigDescription("Minimum seconds between repeated block or parry messages.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _notifyEncumbranceChanged = Config.Bind("9. Default Game Events", "NotifyEncumbranceChanged", true, "Show Over-encumbered and Burden lifted text when the encumbrance state changes.");
            _notifyLocationCleared = Config.Bind("9. Default Game Events", "NotifyLocationCleared", true, "Show a reward-style message when a location is cleared.");
            _notifyPickpocketSuccess = Config.Bind("9. Default Game Events", "NotifyPickpocketSuccess", true, "Show pickpocket text when a pickpocket succeeds.");
            _notifyPickpocketFail = Config.Bind("9. Default Game Events", "NotifyPickpocketFail", true, "Show pickpocket warning text when a pickpocket fails.");
            _notifyBountyChanged = Config.Bind("9. Default Game Events", "NotifyBountyChanged", true, "Show crime text when a noticed crime changes the hero's bounty.");
            _notifyBountyCleared = Config.Bind("9. Default Game Events", "NotifyBountyCleared", true, "Show crime text when a bounty is cleared.");
            _notifyUnforgivableCrime = Config.Bind("9. Default Game Events", "NotifyUnforgivableCrime", true, "Show critical crime text when an unforgivable crime is committed.");
            _crimeEventCooldownSeconds = Config.Bind("9. Default Game Events", "CrimeEventCooldownSeconds", 0.5f, new ConfigDescription("Minimum seconds between repeated crime, bounty, and pickpocket messages of the same type.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _notifyWeakspotHit = Config.Bind("9. Default Game Events", "NotifyWeakspotHit", false, "Show optional critical combat text when the hero lands a weak spot hit.");
            _notifySneakAttack = Config.Bind("9. Default Game Events", "NotifySneakAttack", false, "Show optional critical combat text when the hero lands sneak attack damage.");
            _combatHitMinimumDamage = Config.Bind("9. Default Game Events", "CombatHitMinimumDamage", 1.0f, new ConfigDescription("Minimum sneak attack damage required before showing sneak attack text.", new AcceptableValueRange<float>(0.0f, 10000.0f)));
            _combatHitCooldownSeconds = Config.Bind("9. Default Game Events", "CombatHitCooldownSeconds", 1.0f, new ConfigDescription("Minimum seconds between repeated weak spot or sneak attack messages.", new AcceptableValueRange<float>(0.0f, 10.0f)));
            _notifyXpGained = Config.Bind("9. Default Game Events", "NotifyXpGained", true, "Show XP gains through Grail Floating Text.");
            _suppressVanillaXpNotifications = Config.Bind("9. Default Game Events", "SuppressVanillaXpNotifications", true, "Hide vanilla XP notifications when Grail Floating Text successfully shows an XP gain.");
            _consolidateXpGains = Config.Bind("9. Default Game Events", "ConsolidateXpGains", true, "Show one XP entry at a time and combine queued compatible gains. Generic XP combines only with generic XP; mod claims require the same source-specific consolidation key.");
            _xpTextFormat = Config.Bind("9. Default Game Events", "XpTextFormat", "+{xp} XP", "Floating text for XP gains. Tokens: {xp}, {amount}.");
            _xpDurationBucket = Config.Bind("9. Default Game Events", "XpDurationBucket", "Short", "Named duration bucket used for XP gain floating text.");
            _vanillaWyrdEventsEnabled = Config.Bind("9. Default Game Events", "VanillaWyrdEventsEnabled", true, "Show built-in Grail Floating Text messages for vanilla Wyrd game events.");
            _notifyWyrdNightChange = Config.Bind("9. Default Game Events", "NotifyWyrdNightChange", true, "Show Wyrdnight falls/fades messages when the vanilla Wyrdnight state changes.");
            _notifyWyrdSafetyChange = Config.Bind("9. Default Game Events", "NotifyWyrdSafetyChange", true, "Show Safe from Wyrdness and Exposed to Wyrdness messages for vanilla Wyrd safety changes.");
            _suppressWyrdSafetyWhenWyrdHuntAddonLoaded = Config.Bind("9. Default Game Events", "SuppressWyrdSafetyWhenWyrdHuntAddonLoaded", true, "Suppress vanilla Wyrd safety messages while Wyrd Hunt Addon is loaded so the addon's Wyrd Scent status remains authoritative.");
            _notifyWyrdSoulFragmentCollected = Config.Bind("9. Default Game Events", "NotifyWyrdSoulFragmentCollected", true, "Show a Wyrd power unlocked message when a Wyrd soul fragment is collected.");
            _notifyWyrdSkillToggle = Config.Bind("9. Default Game Events", "NotifyWyrdSkillToggle", false, "Show Wyrd Skill active/ended messages when the Wyrd skill is toggled.");
            _vanillaWyrdEventCooldownSeconds = Config.Bind("9. Default Game Events", "VanillaWyrdEventCooldownSeconds", 0.75f, new ConfigDescription("Minimum seconds between repeated vanilla Wyrd messages of the same type.", new AcceptableValueRange<float>(0.0f, 10.0f)));

            _diagnostics = Config.Bind("10. Diagnostics", "Diagnostics", false, "Log accepted floating text entries.");

            BindColorGroups();
        }

        private void BindColorGroups()
        {
            _colorGroups.Clear();
            _colorGroupByName.Clear();

            BindColorGroup(
                "Red",
                "#FF3D2E",
                "killing-blow; blood-magic-corpse-xp; default-unforgivable-crime; default-combat-weakspot; default-combat-sneak-attack",
                "High-impact success or danger events.");
            BindColorGroup(
                "Gold",
                "#FFDB47",
                "default-location-cleared; default-pickpocket-success; default-bounty-cleared; vanilla-wyrd-fragment",
                "Reward and progress events.");
            BindColorGroup(
                "Blue",
                "#9EE0FF",
                "default-burden-lifted",
                "Clean status-change events.");
            BindColorGroup(
                "Purple",
                "#C294FF",
                "wyrd-hunt-status; vanilla-wyrd-night; vanilla-wyrd-safety; vanilla-wyrd-skill",
                "Wyrd and mystical status events.");
            BindColorGroup(
                "Orange",
                "#FFB87A",
                "default-rest-interrupted; default-over-encumbered; default-combat-blocked; default-combat-parried; default-pickpocket-fail; default-bounty-changed",
                "Warnings and combat feedback events.");
            BindColorGroup(
                "Pale",
                "#DBE6FF",
                "default-rest-duration",
                "System-like informational events.");
            BindColorGroup(
                "Gray",
                "#B3B3B3",
                "",
                "Muted or diagnostic events.");
            BindColorGroup(
                "White",
                "#FFFFFF",
                DefaultXpGainEventId,
                "Clean neutral XP and informational events.");
            BindColorGroup(
                "Default",
                "#F5E0AD",
                "",
                "Fallback color for ungrouped messages.");
        }

        private void BindColorGroup(string name, string defaultColor, string defaultEvents, string description)
        {
            const string colorSection = "11. Color Groups";
            const string iconColorSection = "12. Icon Color Overrides";
            ColorGroupSettings settings = new ColorGroupSettings
            {
                Name = name,
                Color = Config.Bind(
                    colorSection,
                    name + "Color",
                    defaultColor,
                    description + " Default: " + defaultColor + ". Enter an HTML hex color such as #RRGGBB or #RRGGBBAA."),
                Events = Config.Bind(colorSection, name + "Events", defaultEvents, "Semicolon, comma, pipe, or newline separated event IDs assigned to this color group. First matching group wins."),
                IconColor = Config.Bind(
                    iconColorSection,
                    name + "IconColor",
                    string.Empty,
                    "Optional foreground tint for icons on " + name + "-group notifications. Default: blank, which inherits " + name + "Color. Enter an HTML hex color such as #RRGGBB or #RRGGBBAA.")
            };

            _colorGroups.Add(settings);
            _colorGroupByName[name] = settings;
            settings.Color.SettingChanged += OnColorSettingChanged;
            settings.IconColor.SettingChanged += OnIconColorSettingChanged;
        }

        private void OnColorSettingChanged(object sender, EventArgs eventArgs)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            ConfigEntry<string> entry = sender as ConfigEntry<string>;
            Color resolvedColor = default(Color);
            bool valid = entry != null
                && !string.IsNullOrWhiteSpace(entry.Value)
                && ColorUtility.TryParseHtmlString(entry.Value.Trim(), out resolvedColor);
            Logger.LogInfo(
                "Live color setting changed: "
                + (entry == null ? "unknown" : entry.Definition.Key)
                + "="
                + (entry == null ? "(missing)" : entry.Value)
                + "; resolvedColor="
                + (valid ? "#" + ColorUtility.ToHtmlStringRGBA(resolvedColor) : "invalid"));
        }

        private void OnIconColorSettingChanged(object sender, EventArgs eventArgs)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            ConfigEntry<string> entry = sender as ConfigEntry<string>;
            string value = entry == null ? string.Empty : entry.Value;
            Color resolvedColor = default(Color);
            bool inherits = string.IsNullOrWhiteSpace(value);
            bool valid = !inherits
                && ColorUtility.TryParseHtmlString(value.Trim(), out resolvedColor);
            Logger.LogInfo(
                "Live icon color setting changed: "
                + (entry == null ? "unknown" : entry.Definition.Key)
                + "="
                + (entry == null ? "(missing)" : value)
                + "; resolvedColor="
                + (inherits
                    ? "inherit"
                    : valid ? "#" + ColorUtility.ToHtmlStringRGBA(resolvedColor) : "invalid"));
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

            CapturePreservedPresentation(configPath, storedSchemaVersion);

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
                _previousConfigSchemaVersion = storedSchemaVersion;
                _showConfigResetNotification = true;
            }
            catch (Exception exception)
            {
                ClearPendingPreservedPresentation();

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
                    Logger.LogError("Could not restore the previous " + PluginName + " config after a failed schema reset: " + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset " + PluginName + " config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedPresentation(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedPresentation();
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
                object preservedValue;
                if (!TryGetPreservedPresentationValue(
                    profile,
                    currentSection,
                    settingName,
                    settingId,
                    out preservedValue))
                {
                    continue;
                }

                if (IsLegacyDefaultDurationValue(
                    settingId,
                    preservedValue,
                    storedSchemaVersion))
                {
                    continue;
                }

                _pendingPreservedPresentation[settingId] = preservedValue;
                if (currentSection.StartsWith("9. Sources.", StringComparison.Ordinal))
                {
                    _pendingPreservedSourceSections.Add(currentSection);
                }
            }
        }

        private static bool IsLegacyDefaultDurationValue(
            string settingId,
            object settingValue,
            int storedSchemaVersion)
        {
            if (storedSchemaVersion >= 15)
            {
                return false;
            }

            float legacyDefault;
            switch (settingId)
            {
                case "3. Timing\nDefaultDurationSeconds":
                case "3. Timing\nMediumDurationSeconds":
                    legacyDefault = 2.0f;
                    break;
                case "3. Timing\nVeryShortDurationSeconds":
                    legacyDefault = 1.0f;
                    break;
                case "3. Timing\nShortDurationSeconds":
                    legacyDefault = 1.5f;
                    break;
                case "3. Timing\nLongDurationSeconds":
                    legacyDefault = 2.5f;
                    break;
                case "3. Timing\nVeryLongDurationSeconds":
                    legacyDefault = 3.0f;
                    break;
                default:
                    return false;
            }

            return settingValue is float
                && Math.Abs((float)settingValue - legacyDefault) < 0.0001f;
        }

        private static bool TryGetPreservedPresentationValue(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            string section,
            string settingName,
            string settingId,
            out object preservedValue)
        {
            preservedValue = null;
            if (IsPreservedFloatSetting(settingId))
            {
                float value;
                if (profile.TryGetCustomizedValue(
                    section,
                    settingName,
                    out value))
                {
                    preservedValue = value;
                    return true;
                }
                return false;
            }

            if (IsPreservedIntSetting(settingId))
            {
                int value;
                if (profile.TryGetCustomizedValue(
                    section,
                    settingName,
                    out value))
                {
                    preservedValue = value;
                    return true;
                }
                return false;
            }

            if (string.Equals(
                settingId,
                "2. Layout\nFontMode",
                StringComparison.Ordinal))
            {
                FontMode value;
                if (profile.TryGetCustomizedValue(
                    section,
                    settingName,
                    out value))
                {
                    preservedValue = value;
                    return true;
                }
                return false;
            }

            if (string.Equals(
                    settingId,
                    "4. Animation\nSpawnAnimationEnabled",
                    StringComparison.Ordinal)
                || string.Equals(
                    settingId,
                    "8. Icons\nIconShadowEnabled",
                    StringComparison.Ordinal))
            {
                bool value;
                if (profile.TryGetCustomizedValue(
                    section,
                    settingName,
                    out value))
                {
                    preservedValue = value;
                    return true;
                }
                return false;
            }

            if (section.StartsWith("9. Sources.", StringComparison.Ordinal))
            {
                if (string.Equals(settingName, "Enabled", StringComparison.Ordinal))
                {
                    bool value;
                    if (profile.TryGetCustomizedValue(
                        section,
                        settingName,
                        out value))
                    {
                        preservedValue = value;
                        return true;
                    }
                    return false;
                }

                if (string.Equals(
                            settingName,
                            "ThrottleSeconds",
                            StringComparison.Ordinal)
                    || string.Equals(
                            settingName,
                            "DurationMultiplier",
                            StringComparison.Ordinal))
                {
                    float value;
                    if (profile.TryGetCustomizedValue(
                        section,
                        settingName,
                        out value))
                    {
                        preservedValue = value;
                        return true;
                    }
                }
                return false;
            }

            if (string.Equals(section, "11. Color Groups", StringComparison.Ordinal)
                && settingName.EndsWith("Color", StringComparison.Ordinal))
            {
                string value;
                if (profile.TryGetCustomizedValue(
                    section,
                    settingName,
                    out value))
                {
                    preservedValue = value;
                    return true;
                }
            }

            if (string.Equals(section, "12. Icon Color Overrides", StringComparison.Ordinal)
                && settingName.EndsWith("IconColor", StringComparison.Ordinal))
            {
                string value;
                if (profile.TryGetCustomizedValue(
                    section,
                    settingName,
                    out value))
                {
                    preservedValue = value;
                    return true;
                }
            }
            return false;
        }

        private static bool IsPreservedFloatSetting(string settingId)
        {
            switch (settingId)
            {
                case "2. Layout\nScale":
                    return true;
                case "2. Layout\nCenterX":
                case "2. Layout\nBaseCenterY":
                case "3. Timing\nGlobalOpacity":
                case "8. Icons\nIconOpacity":
                case "8. Icons\nIconShadowOpacity":
                    return true;
                case "2. Layout\nWidth":
                    return true;
                case "2. Layout\nStackSpacing":
                    return true;
                case "3. Timing\nDefaultDurationSeconds":
                case "3. Timing\nVeryShortDurationSeconds":
                case "3. Timing\nShortDurationSeconds":
                case "3. Timing\nMediumDurationSeconds":
                case "3. Timing\nLongDurationSeconds":
                case "3. Timing\nVeryLongDurationSeconds":
                case "3. Timing\nSystemDurationSeconds":
                    return true;
                case "3. Timing\nDefaultFadeSeconds":
                    return true;
                case "4. Animation\nSpawnStartScale":
                case "4. Animation\nSpawnOvershootScale":
                    return true;
                case "4. Animation\nSpawnAnimationSeconds":
                case "4. Animation\nStackMoveAnimationSeconds":
                    return true;
                case "7. Source Defaults\nDefaultThrottleSeconds":
                    return true;
                case "7. Source Defaults\nDefaultDurationMultiplier":
                    return true;
                case "8. Icons\nIconSize":
                    return true;
                case "8. Icons\nIconGap":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPreservedIntSetting(string settingId)
        {
            switch (settingId)
            {
                case "2. Layout\nFontSize":
                    return true;
                case "2. Layout\nMaximumVisibleNotifications":
                    return true;
                default:
                    return false;
            }
        }

        private void RestorePreservedPresentation()
        {
            if (_pendingPreservedPresentation.Count == 0
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            BindPreservedSourceSections();

            int restoredCount = 0;
            int clampedCount = 0;
            foreach (KeyValuePair<string, object> pair in _pendingPreservedPresentation)
            {
                int separatorIndex = pair.Key.IndexOf('\n');
                if (separatorIndex <= 0)
                {
                    _pendingPreservedInvalidValueCount++;
                    continue;
                }

                string section = pair.Key.Substring(0, separatorIndex);
                string settingName = pair.Key.Substring(separatorIndex + 1);
                if (IsPreservedFloatSetting(pair.Key))
                {
                    RestorePreservedFloat(
                        section,
                        settingName,
                        (float)pair.Value,
                        ref restoredCount,
                        ref clampedCount);
                    continue;
                }

                if (IsPreservedIntSetting(pair.Key))
                {
                    RestorePreservedInt(
                        section,
                        settingName,
                        (int)pair.Value,
                        ref restoredCount,
                        ref clampedCount);
                    continue;
                }

                if (section.StartsWith("9. Sources.", StringComparison.Ordinal))
                {
                    if (string.Equals(settingName, "Enabled", StringComparison.Ordinal))
                    {
                        RestorePreservedBool(
                            section,
                            settingName,
                            (bool)pair.Value,
                            ref restoredCount);
                    }
                    else if (string.Equals(settingName, "ThrottleSeconds", StringComparison.Ordinal))
                    {
                        RestorePreservedFloat(
                            section,
                            settingName,
                            (float)pair.Value,
                            ref restoredCount,
                            ref clampedCount);
                    }
                    else if (string.Equals(settingName, "DurationMultiplier", StringComparison.Ordinal))
                    {
                        RestorePreservedFloat(
                            section,
                            settingName,
                            (float)pair.Value,
                            ref restoredCount,
                            ref clampedCount);
                    }

                    continue;
                }

                if (string.Equals(pair.Key, "2. Layout\nFontMode", StringComparison.Ordinal))
                {
                    if (pair.Value is FontMode
                        && Enum.IsDefined(typeof(FontMode), pair.Value))
                    {
                        bool clamped;
                        if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                            _fontMode,
                            (FontMode)pair.Value,
                            out clamped))
                        {
                            restoredCount++;
                        }
                    }
                    else
                    {
                        _pendingPreservedInvalidValueCount++;
                    }

                    continue;
                }

                if (string.Equals(
                        pair.Key,
                        "4. Animation\nSpawnAnimationEnabled",
                        StringComparison.Ordinal)
                    || string.Equals(
                        pair.Key,
                        "8. Icons\nIconShadowEnabled",
                        StringComparison.Ordinal))
                {
                    RestorePreservedBool(
                        section,
                        settingName,
                        (bool)pair.Value,
                        ref restoredCount);
                    continue;
                }

                if (string.Equals(section, "11. Color Groups", StringComparison.Ordinal)
                    && settingName.EndsWith("Color", StringComparison.Ordinal))
                {
                    RestorePreservedString(
                        section,
                        settingName,
                        (string)pair.Value,
                        ref restoredCount);
                    continue;
                }

                if (string.Equals(section, "12. Icon Color Overrides", StringComparison.Ordinal)
                    && settingName.EndsWith("IconColor", StringComparison.Ordinal))
                {
                    RestorePreservedString(
                        section,
                        settingName,
                        (string)pair.Value,
                        ref restoredCount);
                }
            }

            Logger.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " presentation and source value(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + _pendingPreservedInvalidValueCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            ClearPendingPreservedPresentation();
        }

        private void BindPreservedSourceSections()
        {
            foreach (string section in _pendingPreservedSourceSections)
            {
                Config.Bind(section, "Enabled", true, "Allow this source to show Grail Floating Text messages.");
                Config.Bind(
                    section,
                    "ThrottleSeconds",
                    GetDefaultSourceThrottleSeconds(),
                    new ConfigDescription(
                        "Minimum seconds between non-high-priority messages from this source.",
                        new AcceptableValueRange<float>(0.0f, 10.0f)));
                Config.Bind(
                    section,
                    "DurationMultiplier",
                    GetDefaultSourceDurationMultiplier(),
                    new ConfigDescription(
                        "Multiplier applied to this source's requested display duration.",
                        new AcceptableValueRange<float>(0.1f, 5.0f)));
            }
        }

        private void RestorePreservedFloat(
            string section,
            string settingName,
            float settingValue,
            ref int restoredCount,
            ref int clampedCount)
        {
            ConfigEntry<float> entry;
            if (!Config.TryGetEntry<float>(section, settingName, out entry))
            {
                _pendingPreservedInvalidValueCount++;
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                settingValue,
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

        private void RestorePreservedInt(
            string section,
            string settingName,
            int settingValue,
            ref int restoredCount,
            ref int clampedCount)
        {
            ConfigEntry<int> entry;
            if (!Config.TryGetEntry<int>(section, settingName, out entry))
            {
                _pendingPreservedInvalidValueCount++;
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                settingValue,
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
            string section,
            string settingName,
            bool settingValue,
            ref int restoredCount)
        {
            ConfigEntry<bool> entry;
            if (!Config.TryGetEntry<bool>(section, settingName, out entry))
            {
                _pendingPreservedInvalidValueCount++;
                return;
            }

            bool clamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                settingValue,
                out clamped))
            {
                restoredCount++;
            }
            else
            {
                _pendingPreservedInvalidValueCount++;
            }
        }

        private void RestorePreservedString(
            string section,
            string settingName,
            string settingValue,
            ref int restoredCount)
        {
            ConfigEntry<string> entry;
            if (!Config.TryGetEntry<string>(section, settingName, out entry))
            {
                _pendingPreservedInvalidValueCount++;
                return;
            }

            bool clamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                settingValue,
                out clamped))
            {
                restoredCount++;
            }
            else
            {
                _pendingPreservedInvalidValueCount++;
            }
        }

        private void ClearPendingPreservedPresentation()
        {
            _pendingPreservedPresentation.Clear();
            _pendingPreservedSourceSections.Clear();
            _pendingPreservedInvalidValueCount = 0;
        }

        private void ShowPendingConfigResetNotification()
        {
            if (!_showConfigResetNotification)
            {
                return;
            }

            _showConfigResetNotification = false;
            NotificationApi.TryShowEvent(
                PluginGuid,
                "config-reset",
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.BuildConfigResetMessage(
                    PluginName,
                    _previousConfigSchemaVersion,
                    ConfigSchemaVersion),
                "System",
                "System",
                "High",
                "config-reset",
                "system",
                "System",
                -1.0f,
                1.0f);
        }

        private void LoadDeferredNotifications()
        {
            string path = GetDeferredNotificationStorePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            bool sanitized = false;
            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    DeferredNotificationEntry entry;
                    if (!TryParseDeferredNotification(lines[i], out entry))
                    {
                        sanitized = true;
                        continue;
                    }

                    int countBefore = _deferredNotifications.Count;
                    AddOrReplaceDeferredNotification(entry);
                    if (_deferredNotifications.Count == countBefore)
                    {
                        sanitized = true;
                    }
                }

                if (lines.Length > MaximumDeferredNotifications)
                {
                    sanitized = true;
                }

                if (sanitized)
                {
                    SaveDeferredNotifications();
                }

                if (_deferredNotifications.Count > 0 && _diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        "Restored "
                        + _deferredNotifications.Count.ToString(CultureInfo.InvariantCulture)
                        + " deferred notification(s).");
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not restore deferred notifications: "
                    + exception.GetBaseException().Message);
            }
        }

        private void SaveDeferredNotifications()
        {
            string path = GetDeferredNotificationStorePath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (_deferredNotifications.Count == 0)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return;
                }

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string[] lines = new string[_deferredNotifications.Count];
                for (int i = 0; i < _deferredNotifications.Count; i++)
                {
                    lines[i] = SerializeDeferredNotification(_deferredNotifications[i]);
                }

                string temporaryPath = path + ".tmp";
                File.WriteAllLines(temporaryPath, lines, Encoding.UTF8);
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not save deferred notifications: "
                    + exception.GetBaseException().Message);
            }
        }

        private string GetDeferredNotificationStorePath()
        {
            string configPath = Config == null ? null : Config.ConfigFilePath;
            return string.IsNullOrEmpty(configPath) ? null : configPath + ".pending";
        }

        private static string SerializeDeferredNotification(DeferredNotificationEntry entry)
        {
            return string.Join(
                "\t",
                DeferredNotificationStoreVersion.ToString(CultureInfo.InvariantCulture),
                ((int)entry.DeliveryPoint).ToString(CultureInfo.InvariantCulture),
                entry.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture),
                EncodeDeferredValue(entry.SourceId),
                EncodeDeferredValue(entry.EventId),
                EncodeDeferredValue(entry.Text),
                EncodeDeferredValue(entry.Style),
                EncodeDeferredValue(entry.Category),
                EncodeDeferredValue(entry.Priority),
                EncodeDeferredValue(entry.CollapseKey),
                EncodeDeferredValue(entry.IconId),
                entry.DurationSeconds.ToString("R", CultureInfo.InvariantCulture),
                entry.FadeSeconds.ToString("R", CultureInfo.InvariantCulture),
                entry.Opacity.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool TryParseDeferredNotification(
            string line,
            out DeferredNotificationEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            try
            {
                string[] parts = line.Split('\t');
                int storeVersion;
                int deliveryValue;
                long createdUtcTicks;
                float durationSeconds;
                float fadeSeconds;
                float opacity;
                if (parts.Length != 14
                    || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out storeVersion)
                    || storeVersion != DeferredNotificationStoreVersion
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out deliveryValue)
                    || (deliveryValue != (int)DeliveryPoint.OnMainMenu
                        && deliveryValue != (int)DeliveryPoint.OnLoad)
                    || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out createdUtcTicks)
                    || !float.TryParse(parts[11], NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds)
                    || !float.TryParse(parts[12], NumberStyles.Float, CultureInfo.InvariantCulture, out fadeSeconds)
                    || !float.TryParse(parts[13], NumberStyles.Float, CultureInfo.InvariantCulture, out opacity))
                {
                    return false;
                }

                long ageTicks = DateTime.UtcNow.Ticks - createdUtcTicks;
                if (createdUtcTicks <= 0
                    || ageTicks > TimeSpan.FromDays(DeferredNotificationMaximumAgeDays).Ticks
                    || ageTicks < -TimeSpan.FromDays(1).Ticks
                    || durationSeconds <= 0.0f)
                {
                    return false;
                }

                entry = new DeferredNotificationEntry
                {
                    DeliveryPoint = (DeliveryPoint)deliveryValue,
                    CreatedUtcTicks = createdUtcTicks,
                    SourceId = DecodeDeferredValue(parts[3]),
                    EventId = DecodeDeferredValue(parts[4]),
                    Text = DecodeDeferredValue(parts[5]),
                    Style = DecodeDeferredValue(parts[6]),
                    Category = DecodeDeferredValue(parts[7]),
                    Priority = DecodeDeferredValue(parts[8]),
                    CollapseKey = DecodeDeferredValue(parts[9]),
                    IconId = DecodeDeferredValue(parts[10]),
                    DurationSeconds = durationSeconds,
                    FadeSeconds = fadeSeconds,
                    Opacity = opacity
                };
                return !string.IsNullOrWhiteSpace(entry.SourceId)
                    && !string.IsNullOrWhiteSpace(entry.Text);
            }
            catch
            {
                entry = null;
                return false;
            }
        }

        private static string EncodeDeferredValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeDeferredValue(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private void PruneExpired(float now)
        {
            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                NotificationEntry entry = _notifications[i];
                if (now - entry.StartTime > Math.Max(DefaultMinimumDurationSeconds, entry.DurationSeconds))
                {
                    _notifications.RemoveAt(i);
                    if (ReferenceEquals(entry, _activeXpNotification))
                    {
                        _activeXpNotification = null;
                    }
                }
            }
        }

        private void TrimToMaximumVisible()
        {
            int maximum = _maximumVisibleNotifications == null ? 6 : Math.Max(1, _maximumVisibleNotifications.Value);
            while (_notifications.Count > maximum)
            {
                NotificationEntry removed = _notifications[_notifications.Count - 1];
                _notifications.RemoveAt(_notifications.Count - 1);
                if (ReferenceEquals(removed, _activeXpNotification))
                {
                    _activeXpNotification = null;
                }
            }
        }

        private float GetNotificationAlpha(NotificationEntry entry, float elapsed)
        {
            float fadeIn = Math.Max(0.0f, entry.FadeSeconds);
            if (fadeIn <= 0.001f)
            {
                return 1.0f;
            }

            float alpha = 1.0f;
            if (elapsed < fadeIn)
            {
                alpha = Math.Min(alpha, elapsed / fadeIn);
            }

            float fadeOut = GetNotificationFadeOutSeconds(entry.DurationSeconds, fadeIn);
            float remaining = entry.DurationSeconds - elapsed;
            if (remaining < fadeOut)
            {
                alpha = Math.Min(alpha, Math.Max(0.0f, remaining / fadeOut));
            }

            return Clamp01(alpha);
        }

        private static float GetNotificationFadeOutSeconds(float durationSeconds, float fadeInSeconds)
        {
            float anchorRange = DefaultSystemDurationSeconds - DefaultMediumDurationSeconds;
            float scale = 1.0f + ((durationSeconds - DefaultMediumDurationSeconds) / anchorRange);
            scale = Clamp(scale, 0.6f, 2.0f);

            return Math.Min(fadeInSeconds * scale, Math.Max(0.0f, durationSeconds) * 0.5f);
        }

        private float GetAnimatedCenterY(NotificationEntry entry, float targetCenterY, float now, float spacing)
        {
            if (!entry.HasAnimatedCenterY)
            {
                entry.CurrentCenterY = targetCenterY;
                entry.MoveStartCenterY = targetCenterY;
                entry.MoveTargetCenterY = targetCenterY;
                entry.MoveStartTime = now;
                entry.HasAnimatedCenterY = true;
                return targetCenterY;
            }

            float moveSeconds = _stackMoveAnimationSeconds == null ? 0.0f : Math.Max(0.0f, _stackMoveAnimationSeconds.Value);
            if (moveSeconds <= 0.001f || spacing <= 0.001f)
            {
                entry.CurrentCenterY = targetCenterY;
                entry.MoveStartCenterY = targetCenterY;
                entry.MoveTargetCenterY = targetCenterY;
                entry.MoveStartTime = now;
                return targetCenterY;
            }

            if (Math.Abs(entry.MoveTargetCenterY - targetCenterY) > 0.5f)
            {
                entry.MoveStartCenterY = entry.CurrentCenterY;
                entry.MoveTargetCenterY = targetCenterY;
                entry.MoveStartTime = now;
            }

            float progress = Clamp01((now - entry.MoveStartTime) / moveSeconds);
            entry.CurrentCenterY = Lerp(entry.MoveStartCenterY, entry.MoveTargetCenterY, EaseOutCubic(progress));
            if (progress >= 1.0f)
            {
                entry.CurrentCenterY = targetCenterY;
            }

            return entry.CurrentCenterY;
        }

        private float GetSpawnAnimationScale(float elapsed)
        {
            if (_spawnAnimationEnabled == null || !_spawnAnimationEnabled.Value)
            {
                return 1.0f;
            }

            float duration = _spawnAnimationSeconds == null ? 0.0f : Math.Max(0.0f, _spawnAnimationSeconds.Value);
            if (duration <= 0.001f || elapsed >= duration)
            {
                return 1.0f;
            }

            float startScale = _spawnStartScale == null ? 0.7f : Clamp(_spawnStartScale.Value, 0.1f, 3.0f);
            float overshootScale = _spawnOvershootScale == null ? 1.12f : Clamp(_spawnOvershootScale.Value, 0.1f, 3.0f);
            float progress = Clamp01(elapsed / duration);
            if (progress < 0.55f)
            {
                return Lerp(startScale, overshootScale, EaseOutCubic(progress / 0.55f));
            }

            return Lerp(overshootScale, 1.0f, EaseOutCubic((progress - 0.55f) / 0.45f));
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1.0f - Clamp01(value);
            return 1.0f - inverse * inverse * inverse;
        }

        private static float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * Clamp01(amount);
        }

        private FontAsset ResolveConfiguredFontAsset()
        {
            FontMode mode = _fontMode == null ? FontMode.GameDefault : _fontMode.Value;
            if (mode == FontMode.ImguiDefault)
            {
                return ResolveImguiDefaultFontAsset();
            }

            try
            {
                if (mode == FontMode.Sans)
                {
                    return ResolveCommonFontAsset(false, "Sans");
                }

                if (mode == FontMode.Serif)
                {
                    return ResolveCommonFontAsset(true, "Serif");
                }

                FontChooseSetting setting = World.Any<FontChooseSetting>();
                if (setting == null)
                {
                    return ResolveFallbackFontAsset();
                }

                FontFamily activeFont = setting.ActiveFont;
                return ResolveFontFamilyAsset(activeFont, activeFont == null ? "game" : "game " + activeFont.EnumName);
            }
            catch (Exception ex)
            {
                LogFontDiagnosticOnce(
                    "ResolveConfiguredFontAsset:" + mode.ToString() + ":" + ex.GetType().FullName,
                    "Could not resolve " + mode + " font for the TextMesh Pro overlay; using the safe fallback font. "
                        + ex.GetBaseException().Message);
                return ResolveFallbackFontAsset();
            }
        }

        private FontAsset ResolveCommonFontAsset(bool serif, string label)
        {
            CommonReferences references = CommonReferences.Get;
            FontAsset fontAsset = references == null
                ? null
                : serif
                    ? references.SerifFontAsset
                    : references.SansFontAsset;
            if (fontAsset == null)
            {
                LogFontDiagnosticOnce(
                    "CommonFontAssetMissing:" + label,
                    "The " + label + " game FontAsset is not ready for the TextMesh Pro overlay; using the safe fallback font.");
                return ResolveFallbackFontAsset();
            }

            return fontAsset;
        }

        private FontAsset ResolveFontFamilyAsset(FontFamily fontFamily, string label)
        {
            if (fontFamily == null)
            {
                LogFontDiagnosticOnce(
                    "FontFamilyMissing:" + label,
                    "Could not resolve " + label + " font family for the TextMesh Pro overlay; using the safe fallback font.");
                return ResolveFallbackFontAsset();
            }

            FontAsset fontAsset = fontFamily.FontAsset;
            if (fontAsset == null)
            {
                LogFontDiagnosticOnce(
                    "FontAssetMissing:" + fontFamily.EnumName,
                    "Could not resolve " + label + " FontAsset for the TextMesh Pro overlay; using the safe fallback font.");
                return ResolveFallbackFontAsset();
            }

            return fontAsset;
        }

        private FontAsset ResolveFallbackFontAsset()
        {
            FontAsset fontAsset = TMP_Settings.defaultFontAsset;
            return fontAsset ?? ResolveImguiDefaultFontAsset();
        }

        private FontAsset ResolveImguiDefaultFontAsset()
        {
            if (_imguiDefaultFontAsset != null)
            {
                return _imguiDefaultFontAsset;
            }

            Font sourceFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (sourceFont == null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            _imguiDefaultFontAsset = FontAsset.CreateFontAsset(sourceFont);
            if (_imguiDefaultFontAsset != null)
            {
                _imguiDefaultFontAsset.name = "GrailFloatingText-ImguiDefault";
                _imguiDefaultFontAsset.hideFlags = HideFlags.HideAndDontSave;
            }

            return _imguiDefaultFontAsset ?? TMP_Settings.defaultFontAsset;
        }

        private void LogFontDiagnosticOnce(string key, string message)
        {
            if (_diagnostics == null || !_diagnostics.Value || string.Equals(_lastFontDiagnosticKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _lastFontDiagnosticKey = key;
            Logger.LogWarning(message);
        }

        private ColorGroupSettings ResolveStyleColorGroup(string style)
        {
            ColorGroupSettings group;
            if (!string.IsNullOrWhiteSpace(style)
                && _colorGroupByName.TryGetValue(style, out group))
            {
                return group;
            }

            Color parsed;
            if (!string.IsNullOrWhiteSpace(style)
                && ColorUtility.TryParseHtmlString(style, out parsed))
            {
                return null;
            }

            string groupName;
            if (StyleEquals(style, "Reward"))
            {
                groupName = "Gold";
            }
            else if (StyleEquals(style, "Status"))
            {
                groupName = "Blue";
            }
            else if (StyleEquals(style, "Wyrd"))
            {
                groupName = "Purple";
            }
            else if (StyleEquals(style, "Combat")
                || StyleEquals(style, "Resistance")
                || StyleEquals(style, "Warning"))
            {
                groupName = "Orange";
            }
            else if (StyleEquals(style, "Immunity")
                || StyleEquals(style, "Error")
                || StyleEquals(style, "Critical"))
            {
                groupName = "Red";
            }
            else if (StyleEquals(style, "System"))
            {
                groupName = "Pale";
            }
            else if (StyleEquals(style, "Debug"))
            {
                groupName = "Gray";
            }
            else if (StyleEquals(style, "Discovery")
                || StyleEquals(style, "Weakness"))
            {
                return null;
            }
            else
            {
                groupName = "Default";
            }

            return _colorGroupByName.TryGetValue(groupName, out group)
                ? group
                : null;
        }

        private Color ResolveIconColor(string style, Color inheritedColor, float alpha)
        {
            ColorGroupSettings group = ResolveStyleColorGroup(style);
            if (group == null
                || group.IconColor == null
                || string.IsNullOrWhiteSpace(group.IconColor.Value))
            {
                return inheritedColor;
            }

            string value = group.IconColor.Value.Trim();
            Color parsed;
            if (!ColorUtility.TryParseHtmlString(value, out parsed))
            {
                string warningKey = group.Name + "\n" + value;
                if (_invalidIconColorWarnings.Add(warningKey))
                {
                    Logger.LogWarning(
                        group.IconColor.Definition.Key
                        + " has an invalid HTML color value: "
                        + value
                        + ". The icon will inherit the resolved text color.");
                }

                return inheritedColor;
            }

            parsed.a *= alpha;
            return parsed;
        }

        private Color ResolveStyleColor(string style, float alpha)
        {
            ColorGroupSettings group;
            if (!string.IsNullOrWhiteSpace(style) && _colorGroupByName.TryGetValue(style, out group))
            {
                return ResolveConfiguredColor(group.Color, GetFallbackGroupColor(group.Name), alpha);
            }

            Color parsed;
            if (!string.IsNullOrWhiteSpace(style) && ColorUtility.TryParseHtmlString(style, out parsed))
            {
                parsed.a *= alpha;
                return parsed;
            }

            if (StyleEquals(style, "Reward"))
            {
                return ResolveNamedGroupOrFallback("Gold", new Color(1.0f, 0.86f, 0.28f, alpha), alpha);
            }

            if (StyleEquals(style, "Status"))
            {
                return ResolveNamedGroupOrFallback("Blue", new Color(0.62f, 0.88f, 1.0f, alpha), alpha);
            }

            if (StyleEquals(style, "Wyrd"))
            {
                return ResolveNamedGroupOrFallback(
                    "Purple",
                    new Color(
                        152.0f / 255.0f,
                        112.0f / 255.0f,
                        1.0f,
                        alpha),
                    alpha);
            }

            if (StyleEquals(style, "Discovery"))
            {
                return new Color(0.62f, 1.0f, 0.74f, alpha);
            }

            if (StyleEquals(style, "Combat"))
            {
                return ResolveNamedGroupOrFallback("Orange", new Color(1.0f, 0.72f, 0.48f, alpha), alpha);
            }

            if (StyleEquals(style, "Weakness"))
            {
                return new Color(0.22f, 1.0f, 0.48f, alpha);
            }

            if (StyleEquals(style, "Resistance"))
            {
                return ResolveNamedGroupOrFallback("Orange", new Color(1.0f, 0.58f, 0.18f, alpha), alpha);
            }

            if (StyleEquals(style, "Immunity"))
            {
                return ResolveNamedGroupOrFallback("Red", new Color(1.0f, 0.04f, 0.02f, alpha), alpha);
            }

            if (StyleEquals(style, "Warning"))
            {
                return ResolveNamedGroupOrFallback("Orange", new Color(1.0f, 0.72f, 0.18f, alpha), alpha);
            }

            if (StyleEquals(style, "Error") || StyleEquals(style, "Critical"))
            {
                return ResolveNamedGroupOrFallback("Red", new Color(1.0f, 0.24f, 0.18f, alpha), alpha);
            }

            if (StyleEquals(style, "System"))
            {
                return ResolveNamedGroupOrFallback("Pale", new Color(0.86f, 0.90f, 1.0f, alpha), alpha);
            }

            if (StyleEquals(style, "Debug"))
            {
                return ResolveNamedGroupOrFallback("Gray", new Color(0.70f, 0.70f, 0.70f, alpha), alpha);
            }

            return ResolveNamedGroupOrFallback("Default", new Color(0.96f, 0.88f, 0.68f, alpha), alpha);
        }

        private Color ResolveNamedGroupOrFallback(string groupName, Color fallback, float alpha)
        {
            ColorGroupSettings group;
            return _colorGroupByName.TryGetValue(groupName, out group)
                ? ResolveConfiguredColor(group.Color, fallback, alpha)
                : fallback;
        }

        private static Color ResolveConfiguredColor(ConfigEntry<string> entry, Color fallback, float alpha)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
            {
                fallback.a = alpha;
                return fallback;
            }

            Color parsed;
            if (!ColorUtility.TryParseHtmlString(entry.Value.Trim(), out parsed))
            {
                fallback.a = alpha;
                return fallback;
            }

            parsed.a *= alpha;
            return parsed;
        }

        private static Color GetFallbackGroupColor(string groupName)
        {
            if (StyleEquals(groupName, "Red"))
            {
                return new Color(1.0f, 0.24f, 0.18f, 1.0f);
            }

            if (StyleEquals(groupName, "Gold"))
            {
                return new Color(1.0f, 0.86f, 0.28f, 1.0f);
            }

            if (StyleEquals(groupName, "Blue"))
            {
                return new Color(0.62f, 0.88f, 1.0f, 1.0f);
            }

            if (StyleEquals(groupName, "Purple"))
            {
                return new Color(
                    152.0f / 255.0f,
                    112.0f / 255.0f,
                    1.0f,
                    1.0f);
            }

            if (StyleEquals(groupName, "Orange"))
            {
                return new Color(1.0f, 0.72f, 0.18f, 1.0f);
            }

            if (StyleEquals(groupName, "Pale"))
            {
                return new Color(0.86f, 0.90f, 1.0f, 1.0f);
            }

            if (StyleEquals(groupName, "Gray"))
            {
                return new Color(0.70f, 0.70f, 0.70f, 1.0f);
            }

            if (StyleEquals(groupName, "White"))
            {
                return new Color(1.0f, 1.0f, 1.0f, 1.0f);
            }

            return new Color(0.96f, 0.88f, 0.68f, 1.0f);
        }

        private static bool StyleEquals(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0.0f, 1.0f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class HeroDevelopmentXpHookPatch
        {
            internal static bool Prefix(HookResult<IWithStats, Stat.StatChange> hookResult)
            {
                GrailFloatingTextPlugin plugin = Instance;
                return plugin == null || plugin.HandleXpChangedFromStatHook(hookResult);
            }
        }

        private sealed class HeroDevelopmentXpFloatPatch
        {
            internal static bool Prefix(float gainedXP)
            {
                GrailFloatingTextPlugin plugin = Instance;
                return plugin == null || plugin.HandleXpChangedFromDirectFloat(gainedXP);
            }
        }

        private sealed class XpDisplayClaim
        {
            internal string SourceId;
            internal string EventId;
            internal string Text;
            internal string ConsolidationKey;
            internal string TextFormat;
            internal string Style;
            internal string Category;
            internal string Priority;
            internal string IconId;
            internal DurationBucket DurationBucket;
            internal float ExpectedAmount;
            internal float FadeSeconds;
            internal float Opacity;
            internal float CreatedAt;
            internal long Sequence;
        }

        private sealed class XpNotificationBatch
        {
            internal string SourceId;
            internal string EventId;
            internal string ConsolidationKey;
            internal string Text;
            internal string TextFormat;
            internal string Style;
            internal string Category;
            internal string Priority;
            internal string IconId;
            internal DurationBucket DurationBucket;
            internal float Amount;
            internal float FadeSeconds;
            internal float Opacity;
        }

        private struct NotificationLayout
        {
            internal Texture2D IconTexture;
            internal float GroupWidth;
            internal float TextWidth;
            internal float Height;
            internal bool Wrapped;
        }

        private sealed class NotificationView
        {
            internal RectTransform Root;
            internal RawImage IconShadow;
            internal RawImage Icon;
            internal TextMeshProUGUI ShadowText;
            internal TextMeshProUGUI Text;
        }

        private sealed class NotificationEntry
        {
            internal string SourceId;
            internal string CollapseKey;
            internal string EventId;
            internal string Text;
            internal string Style;
            internal string Category;
            internal string Priority;
            internal string IconId;
            internal int PriorityValue;
            internal float StartTime;
            internal float DurationSeconds;
            internal float FadeSeconds;
            internal float Opacity;
            internal long Sequence;
            internal bool HasAnimatedCenterY;
            internal float CurrentCenterY;
            internal float MoveStartCenterY;
            internal float MoveTargetCenterY;
            internal float MoveStartTime;
        }

        private sealed class DeferredNotificationEntry
        {
            internal string SourceId;
            internal string CollapseKey;
            internal string EventId;
            internal string Text;
            internal string Style;
            internal string Category;
            internal string Priority;
            internal string IconId;
            internal float DurationSeconds;
            internal DeliveryPoint DeliveryPoint;
            internal float FadeSeconds;
            internal float Opacity;
            internal long CreatedUtcTicks;
        }

        private sealed class SourceSettings
        {
            internal ConfigEntry<bool> Enabled;
            internal ConfigEntry<float> ThrottleSeconds;
            internal ConfigEntry<float> DurationMultiplier;
            internal ConfigEntry<bool> Diagnostics;
        }

        private sealed class ColorGroupSettings
        {
            internal string Name;
            internal ConfigEntry<string> Color;
            internal ConfigEntry<string> Events;
            internal ConfigEntry<string> IconColor;
        }
    }
}
