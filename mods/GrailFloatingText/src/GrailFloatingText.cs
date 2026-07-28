using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Events;
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
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Grail Floating Text")]
[assembly: AssemblyDescription("Shared floating text overlay any Tainted Grail mod author can use")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Grail Floating Text")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]
[assembly: AssemblyInformationalVersion("1.5.0")]

namespace GrailFloatingText
{
    public static class NotificationApi
    {
        public const int ApiVersion = 5;

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

        public static bool TryClaimXpGain(
            string sourceId,
            string eventId,
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
            return plugin != null && plugin.TryClaimXpGain(sourceId, eventId, style, category, priority, iconId, durationBucket, expectedAmount, fadeSeconds, opacity);
        }

        public static string[] GetBuiltInIconIds()
        {
            return GrailFloatingTextPlugin.GetBuiltInIconIds();
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class GrailFloatingTextPlugin : BaseUnityPlugin, IListenerOwner
    {
        public const string PluginGuid = "ks.tgfoa.grail-floating-text";
        public const string PluginName = "Grail Floating Text";
        public const string PluginVersion = "1.5.0";

        private const string WyrdHuntAddonPluginGuid = "ks.tgfoa.wyrd-hunt-addon";
        private const int ConfigSchemaVersion = 9;
        private const float DefaultMinimumDurationSeconds = 0.05f;
        private const float DefaultVeryShortDurationSeconds = 1.0f;
        private const float DefaultShortDurationSeconds = 1.5f;
        private const float DefaultMediumDurationSeconds = 2.0f;
        private const float DefaultLongDurationSeconds = 2.5f;
        private const float DefaultVeryLongDurationSeconds = 3.0f;
        private const float XpClaimLifetimeSeconds = 2.0f;
        private const float XpClaimImmediateFallbackSeconds = 0.25f;
        private const float XpClaimAmountTolerance = 0.01f;
        private const float DirectXpDuplicateSuppressSeconds = 0.05f;
        private const string DefaultXpGainEventId = "default-xp-gain";
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
            "unarmed",
            "magic",
            "crime",
            "pickpocket",
            "weight",
            "experience",
            "corpse"
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
            VeryLong
        }

        internal static GrailFloatingTextPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private readonly List<NotificationEntry> _notifications = new List<NotificationEntry>();
        private readonly List<XpDisplayClaim> _xpDisplayClaims = new List<XpDisplayClaim>();

        private Harmony _harmony;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _scale;
        private ConfigEntry<int> _fontSize;
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
        private ConfigEntry<bool> _perSourceControlsEnabled;
        private ConfigEntry<float> _defaultSourceThrottleSeconds;
        private ConfigEntry<float> _defaultSourceDurationMultiplier;
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

        private readonly Dictionary<string, ConfigEntry<bool>> _categoryEnabledByName =
            new Dictionary<string, ConfigEntry<bool>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, SourceSettings> _sourceSettingsById =
            new Dictionary<string, SourceSettings>(StringComparer.OrdinalIgnoreCase);

        private readonly List<ColorGroupSettings> _colorGroups = new List<ColorGroupSettings>();

        private readonly Dictionary<string, ColorGroupSettings> _colorGroupByName =
            new Dictionary<string, ColorGroupSettings>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, float> _lastNotificationTimeBySource =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Texture2D> _iconTexturesById =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private GUIStyle _textStyle;
        private GUIStyle _shadowStyle;
        private GUIStyle _textLeftStyle;
        private GUIStyle _shadowLeftStyle;
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
        private int _styleFontSize = -1;
        private long _nextSequence;
        private long _nextXpClaimSequence;
        private long _nextXpEntrySequence;
        private bool _passThroughNextXpFloatAnnounce;
        private float _passThroughNextXpFloatAmount;
        private float _passThroughNextXpFloatTime = -9999.0f;
        private float _lastHandledXpAmount;
        private float _lastHandledXpTime = -9999.0f;

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
                LoadIconTextures();
                PatchXpNotifications();
                StartDefaultGameEventBinding();
                Config.Save();
                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
            }
            catch (Exception ex)
            {
                Logger.LogError(PluginName + " " + PluginVersion + " failed during startup: " + ex.GetBaseException().Message);
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            StopDefaultGameEventBinding();
            ReleaseIconTextures();

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
                string.Equals(feature, "ApiVersion5", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "Categories", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "Priority", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "CollapseKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "EventIds", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "DurationBuckets", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "ColorGroups", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(feature, "XpGainClaims", StringComparison.OrdinalIgnoreCase) ||
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
                MethodInfo hookPrefix = AccessTools.Method(typeof(HeroDevelopmentXpHookPatch), "Prefix");

                MethodInfo floatOriginal = AccessTools.Method(
                    typeof(HeroDevelopment),
                    "AnnounceXPChanged",
                    new[] { typeof(float) });
                MethodInfo floatPrefix = AccessTools.Method(typeof(HeroDevelopmentXpFloatPatch), "Prefix");

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
                "combat",
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
                isNight ? "Wyrd Night falls" : "Wyrd Night fades",
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
            if (string.IsNullOrWhiteSpace(text) ||
                ShouldThrottleDefaultGameEvent(throttleKey, GetConfigCooldown(_vanillaWyrdEventCooldownSeconds)))
            {
                return;
            }

            TryShowCore(
                PluginGuid,
                eventId,
                text,
                "Wyrd",
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

            for (int i = 0; i < BuiltInIconIds.Length; i++)
            {
                string iconId = BuiltInIconIds[i];
                string path = Path.Combine(iconDirectory, iconId + ".png");
                if (!File.Exists(path))
                {
                    Logger.LogWarning(PluginName + " built-in icon was not found: " + path);
                    continue;
                }

                try
                {
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    object loadResult = loadImageMethod.Invoke(null, new object[] { texture, File.ReadAllBytes(path), false });
                    if (!(loadResult is bool) || !((bool)loadResult))
                    {
                        UnityEngine.Object.Destroy(texture);
                        Logger.LogWarning(PluginName + " could not load icon: " + path);
                        continue;
                    }

                    texture.name = "GrailFloatingTextIcon_" + iconId;
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    _iconTexturesById[iconId] = texture;
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(PluginName + " could not load icon " + path + ": " + exception.GetBaseException().Message);
                }
            }
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
            return TryShowCore(
                sourceId,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                GetDurationBucketSeconds(ParseDurationBucket(durationBucket)),
                fadeSeconds,
                opacity);
        }

        internal bool TryClaimXpGain(
            string sourceId,
            string eventId,
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
            string eventId = claim == null || string.IsNullOrWhiteSpace(claim.EventId)
                ? DefaultXpGainEventId
                : claim.EventId;
            string sourceId = claim == null || string.IsNullOrWhiteSpace(claim.SourceId)
                ? PluginGuid
                : claim.SourceId;
            string style = claim == null || string.IsNullOrWhiteSpace(claim.Style)
                ? "White"
                : claim.Style;
            string category = claim == null || string.IsNullOrWhiteSpace(claim.Category)
                ? "Reward"
                : claim.Category;
            string priority = claim == null || string.IsNullOrWhiteSpace(claim.Priority)
                ? "High"
                : claim.Priority;
            string iconId = claim == null || string.IsNullOrWhiteSpace(claim.IconId)
                ? "experience"
                : claim.IconId;
            DurationBucket bucket = claim == null
                ? ParseDurationBucket(_xpDurationBucket == null ? "Short" : _xpDurationBucket.Value)
                : claim.DurationBucket;
            float fadeSeconds = claim == null ? -1.0f : claim.FadeSeconds;
            float opacity = claim == null ? 0.9f : claim.Opacity;
            string collapseKey = eventId + "-entry-" + (++_nextXpEntrySequence).ToString(CultureInfo.InvariantCulture);

            return TryShowCore(
                sourceId,
                eventId,
                FormatXpText(gainedXp),
                style,
                category,
                priority,
                collapseKey,
                iconId,
                GetDurationBucketSeconds(bucket),
                fadeSeconds,
                opacity);
        }

        private string FormatXpText(float gainedXp)
        {
            string amount = gainedXp.ToString("F0", CultureInfo.InvariantCulture);
            string format = _xpTextFormat == null ? string.Empty : _xpTextFormat.Value;
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "+{xp} XP";
            }

            return format
                .Replace("{xp}", amount)
                .Replace("{amount}", amount);
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
                Logger.LogInfo(
                    "Queued notification from "
                    + normalizedSourceId
                    + " ["
                    + normalizedCategory
                    + "/"
                    + entry.Priority
                    + "]"
                    + ": "
                    + text);
            }

            return true;
        }

        private void OnGUI()
        {
            if (_enabled == null || !_enabled.Value || _notifications.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            PruneExpired(now);
            if (_notifications.Count == 0)
            {
                return;
            }

            float scale = Math.Max(0.05f, _scale.Value);
            int fontSize = Math.Max(1, (int)Math.Round(Math.Max(1, _fontSize.Value) * scale));
            EnsureStyles(fontSize);

            float width = Math.Max(20.0f, _width.Value * scale);
            float height = Math.Max(fontSize + 10.0f, 32.0f * scale);
            float spacing = Math.Max(height, _stackSpacing.Value * scale);
            float iconSize = Math.Max(8.0f, (_iconSize == null ? 32.0f : _iconSize.Value) * scale);
            float iconGap = Math.Max(0.0f, (_iconGap == null ? 10.0f : _iconGap.Value) * scale);
            float centerX = Screen.width * Clamp01(_centerX.Value);
            float baseCenterY = Screen.height * Clamp01(_baseCenterY.Value);
            float shadowOffset = Math.Max(1.0f, 2.0f * scale);

            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            Color previousTextColor = _textStyle.normal.textColor;
            Color previousShadowColor = _shadowStyle.normal.textColor;
            Color previousLeftTextColor = _textLeftStyle.normal.textColor;
            Color previousLeftShadowColor = _shadowLeftStyle.normal.textColor;

            GUI.depth = -1000;
            GUI.color = Color.white;

            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                NotificationEntry entry = _notifications[i];
                float elapsed = now - entry.StartTime;
                float alpha = GetNotificationAlpha(entry, elapsed)
                    * Clamp01(entry.Opacity)
                    * Clamp01(_globalOpacity.Value);
                if (alpha <= 0.001f)
                {
                    continue;
                }

                float centerY = baseCenterY - (spacing * i);
                centerY = GetAnimatedCenterY(entry, centerY, now, spacing);
                Rect rect = new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
                Rect shadowRect = new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height);
                float animationScale = GetSpawnAnimationScale(elapsed);

                Color textColor = ResolveStyleColor(entry.Style, alpha);
                Color shadowColor = new Color(0.0f, 0.0f, 0.0f, alpha * 0.75f);
                _textStyle.normal.textColor = textColor;
                _shadowStyle.normal.textColor = shadowColor;
                _textLeftStyle.normal.textColor = textColor;
                _shadowLeftStyle.normal.textColor = shadowColor;

                Matrix4x4 previousMatrix = GUI.matrix;
                if (Math.Abs(animationScale - 1.0f) > 0.001f)
                {
                    GUIUtility.ScaleAroundPivot(
                        new Vector2(animationScale, animationScale),
                        new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f));
                }

                Texture2D iconTexture = GetIconTexture(entry.IconId);
                if (iconTexture != null)
                {
                    DrawNotificationWithIcon(entry, iconTexture, rect, iconSize, iconGap, shadowOffset, textColor, shadowColor);
                }
                else
                {
                    GUI.Label(shadowRect, entry.Text, _shadowStyle);
                    GUI.Label(rect, entry.Text, _textStyle);
                }

                GUI.matrix = previousMatrix;
            }

            _textStyle.normal.textColor = previousTextColor;
            _shadowStyle.normal.textColor = previousShadowColor;
            _textLeftStyle.normal.textColor = previousLeftTextColor;
            _shadowLeftStyle.normal.textColor = previousLeftShadowColor;
            GUI.depth = previousDepth;
            GUI.color = previousColor;
        }

        private void DrawNotificationWithIcon(
            NotificationEntry entry,
            Texture2D iconTexture,
            Rect rect,
            float iconSize,
            float iconGap,
            float shadowOffset,
            Color textColor,
            Color shadowColor)
        {
            GUIContent content = new GUIContent(entry.Text);
            float availableTextWidth = Math.Max(20.0f, rect.width - iconSize - iconGap);
            float textWidth = Math.Min(availableTextWidth, _textLeftStyle.CalcSize(content).x + 2.0f);
            float groupWidth = iconSize + iconGap + textWidth;
            float groupX = rect.x + rect.width * 0.5f - groupWidth * 0.5f;

            Rect iconRect = new Rect(groupX, rect.y + rect.height * 0.5f - iconSize * 0.5f, iconSize, iconSize);
            Rect shadowIconRect = new Rect(iconRect.x + shadowOffset, iconRect.y + shadowOffset, iconRect.width, iconRect.height);
            Rect textRect = new Rect(groupX + iconSize + iconGap, rect.y, textWidth, rect.height);
            Rect shadowTextRect = new Rect(textRect.x + shadowOffset, textRect.y + shadowOffset, textRect.width, textRect.height);

            Color previousColor = GUI.color;

            GUI.color = shadowColor;
            GUI.DrawTexture(shadowIconRect, iconTexture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
            GUI.Label(shadowTextRect, entry.Text, _shadowLeftStyle);

            float iconOpacity = _iconOpacity == null ? 1.0f : Clamp01(_iconOpacity.Value);
            GUI.color = new Color(textColor.r, textColor.g, textColor.b, textColor.a * iconOpacity);
            GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
            GUI.Label(textRect, entry.Text, _textLeftStyle);
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
                default:
                    return GetConfiguredDuration(_mediumDurationSeconds, DefaultMediumDurationSeconds);
            }
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

            return DurationBucket.Medium;
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
            if (existing.PriorityValue != incoming.PriorityValue)
            {
                return existing.PriorityValue > incoming.PriorityValue;
            }

            return existing.Sequence > incoming.Sequence;
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
            Config.Bind("1. Core", "ConfigSchemaVersion", ConfigSchemaVersion, "Configuration layout version. Older layouts are backed up and regenerated.");

            _scale = Config.Bind("2. Layout", "Scale", 1.0f, new ConfigDescription("Scale multiplier for all floating text.", new AcceptableValueRange<float>(0.1f, 3.0f)));
            _fontSize = Config.Bind("2. Layout", "FontSize", 20, new ConfigDescription("Base font size before scale is applied.", new AcceptableValueRange<int>(8, 72)));
            _centerX = Config.Bind("2. Layout", "CenterX", 0.5f, new ConfigDescription("Horizontal center as a fraction of screen width.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _baseCenterY = Config.Bind("2. Layout", "BaseCenterY", 0.25f, new ConfigDescription("Vertical center for the newest notification as a fraction of screen height.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            _width = Config.Bind("2. Layout", "Width", 520.0f, new ConfigDescription("Text width before scale is applied.", new AcceptableValueRange<float>(100.0f, 1600.0f)));
            _stackSpacing = Config.Bind("2. Layout", "StackSpacing", 34.0f, new ConfigDescription("Vertical distance between stacked active notifications before scale is applied.", new AcceptableValueRange<float>(16.0f, 160.0f)));
            _maximumVisibleNotifications = Config.Bind("2. Layout", "MaximumVisibleNotifications", 6, new ConfigDescription("Maximum active notifications kept on screen at once. Oldest entries are dropped first.", new AcceptableValueRange<int>(1, 12)));

            _defaultDurationSeconds = Config.Bind("3. Timing", "DefaultDurationSeconds", DefaultMediumDurationSeconds, new ConfigDescription("Default display duration used when a caller does not request one.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _defaultFadeSeconds = Config.Bind("3. Timing", "DefaultFadeSeconds", 0.25f, new ConfigDescription("Default fade-in and fade-out duration used when a caller does not request one.", new AcceptableValueRange<float>(0.0f, 5.0f)));
            _veryShortDurationSeconds = Config.Bind("3. Timing", "VeryShortDurationSeconds", DefaultVeryShortDurationSeconds, new ConfigDescription("Display duration for very short event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _shortDurationSeconds = Config.Bind("3. Timing", "ShortDurationSeconds", DefaultShortDurationSeconds, new ConfigDescription("Display duration for short event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _mediumDurationSeconds = Config.Bind("3. Timing", "MediumDurationSeconds", DefaultMediumDurationSeconds, new ConfigDescription("Display duration for medium event-bucket messages. Event callbacks default to this bucket.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _longDurationSeconds = Config.Bind("3. Timing", "LongDurationSeconds", DefaultLongDurationSeconds, new ConfigDescription("Display duration for long event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
            _veryLongDurationSeconds = Config.Bind("3. Timing", "VeryLongDurationSeconds", DefaultVeryLongDurationSeconds, new ConfigDescription("Display duration for very long event-bucket messages.", new AcceptableValueRange<float>(0.25f, 10.0f)));
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

            _notifyRestDuration = Config.Bind("9. Default Game Events", "NotifyRestDuration", true, "Show how long the hero actually rested after sleep is started.");
            _notifyInterruptedRestDuration = Config.Bind("9. Default Game Events", "NotifyInterruptedRestDuration", true, "Use interrupted wording when rest ends early due to a Wyrd interruption.");
            _restDurationTextFormat = Config.Bind("9. Default Game Events", "RestDurationTextFormat", "Rested {duration}", "Floating text for completed rest. Tokens: {duration}.");
            _restInterruptedTextFormat = Config.Bind("9. Default Game Events", "RestInterruptedTextFormat", "Rest interrupted: {duration} slept", "Floating text for interrupted rest. Tokens: {duration}.");
            _restNotificationMinimumMinutes = Config.Bind("9. Default Game Events", "RestNotificationMinimumMinutes", 1, new ConfigDescription("Minimum actual rest duration in minutes required before showing rest text.", new AcceptableValueRange<int>(0, 1440)));
            _notifyBlockedDamage = Config.Bind("9. Default Game Events", "NotifyBlockedDamage", true, "Show optional throttled combat text when the hero blocks damage.");
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
            _xpTextFormat = Config.Bind("9. Default Game Events", "XpTextFormat", "+{xp} XP", "Floating text for XP gains. Tokens: {xp}, {amount}.");
            _xpDurationBucket = Config.Bind("9. Default Game Events", "XpDurationBucket", "Short", "Named duration bucket used for XP gain floating text.");
            _vanillaWyrdEventsEnabled = Config.Bind("9. Default Game Events", "VanillaWyrdEventsEnabled", true, "Show built-in Grail Floating Text messages for vanilla Wyrd game events.");
            _notifyWyrdNightChange = Config.Bind("9. Default Game Events", "NotifyWyrdNightChange", true, "Show Wyrd Night falls/fades messages when the vanilla Wyrd Night state changes.");
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
            const string section = "11. Color Groups";
            ColorGroupSettings settings = new ColorGroupSettings
            {
                Name = name,
                Color = Config.Bind(section, name + "Color", defaultColor, description + " Use HTML hex color such as #FF3D2E."),
                Events = Config.Bind(section, name + "Events", defaultEvents, "Semicolon, comma, pipe, or newline separated event IDs assigned to this color group. First matching group wins.")
            };

            _colorGroups.Add(settings);
            _colorGroupByName[name] = settings;
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
                    Logger.LogError("Could not restore the previous " + PluginName + " config after a failed schema reset: " + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset " + PluginName + " config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void PruneExpired(float now)
        {
            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                NotificationEntry entry = _notifications[i];
                if (now - entry.StartTime > Math.Max(DefaultMinimumDurationSeconds, entry.DurationSeconds))
                {
                    _notifications.RemoveAt(i);
                }
            }
        }

        private void TrimToMaximumVisible()
        {
            int maximum = _maximumVisibleNotifications == null ? 6 : Math.Max(1, _maximumVisibleNotifications.Value);
            while (_notifications.Count > maximum)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }
        }

        private float GetNotificationAlpha(NotificationEntry entry, float elapsed)
        {
            float fade = Math.Max(0.0f, entry.FadeSeconds);
            if (fade <= 0.001f)
            {
                return 1.0f;
            }

            float alpha = 1.0f;
            if (elapsed < fade)
            {
                alpha = Math.Min(alpha, elapsed / fade);
            }

            float remaining = entry.DurationSeconds - elapsed;
            if (remaining < fade)
            {
                alpha = Math.Min(alpha, Math.Max(0.0f, remaining / fade));
            }

            return Clamp01(alpha);
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

        private void EnsureStyles(int fontSize)
        {
            if (_textStyle != null && _styleFontSize == fontSize)
            {
                return;
            }

            _styleFontSize = fontSize;
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow,
                wordWrap = false
            };
            _shadowStyle = new GUIStyle(_textStyle);
            _textLeftStyle = new GUIStyle(_textStyle)
            {
                alignment = TextAnchor.MiddleLeft
            };
            _shadowLeftStyle = new GUIStyle(_textLeftStyle);
        }

        private Color ResolveStyleColor(string style, float alpha)
        {
            Color parsed;
            if (!string.IsNullOrWhiteSpace(style) && ColorUtility.TryParseHtmlString(style, out parsed))
            {
                parsed.a *= alpha;
                return parsed;
            }

            ColorGroupSettings group;
            if (!string.IsNullOrWhiteSpace(style) && _colorGroupByName.TryGetValue(style, out group))
            {
                return ResolveConfiguredColor(group.Color, GetFallbackGroupColor(group.Name), alpha);
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
                return ResolveNamedGroupOrFallback("Purple", new Color(0.76f, 0.58f, 1.0f, alpha), alpha);
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
                return new Color(0.76f, 0.58f, 1.0f, 1.0f);
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
            private static bool Prefix(HookResult<IWithStats, Stat.StatChange> hookResult)
            {
                GrailFloatingTextPlugin plugin = Instance;
                return plugin == null || plugin.HandleXpChangedFromStatHook(hookResult);
            }
        }

        private sealed class HeroDevelopmentXpFloatPatch
        {
            private static bool Prefix(float gainedXP)
            {
                GrailFloatingTextPlugin plugin = Instance;
                return plugin == null || plugin.HandleXpChangedFromDirectFloat(gainedXP);
            }
        }

        private sealed class XpDisplayClaim
        {
            internal string SourceId;
            internal string EventId;
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
        }
    }
}
