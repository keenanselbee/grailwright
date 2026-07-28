using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Templates;
using Awaken.TG.Main.Utility;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: AssemblyVersion("1.4.1.0")]
[assembly: AssemblyFileVersion("1.4.1.0")]

namespace Keenan.TGFoA.WyrdHuntAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("kane.tgfoa.wyrd-hunt", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(GrailFloatingTextPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class WyrdHuntAddonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.wyrd-hunt-addon";
        public const string PluginName = "Wyrd Hunt Addon";
        public const string PluginVersion = "1.4.1";

        private const string AddonProfilePrefix = "keenan-random-";
        private const string ParentWyrdHuntPluginGuid = "kane.tgfoa.wyrd-hunt";
        private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";
        private const string GrailFloatingTextWyrdStatusEventId = "wyrd-hunt-status";
        private const string GrailFloatingTextMediumDurationBucket = "Medium";
        private const string WyrdspiritCandidateId = "wyrdspirit";
        private const string ScentMeterModeParent = "Parent";
        private const string ScentMeterModeAutoHide = "AutoHide";
        private const string ScentMeterModeNotificationsOnly = "NotificationsOnly";
        private const float MeterWidth = 340f;
        private const float MeterHeight = 68f;
        private const float HorizontalMargin = 48f;
        private const int ConfigSchemaVersion = 5;

        internal static WyrdHuntAddonPlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<string> ScentMeterMode { get; private set; }
        internal static ConfigEntry<bool> HideWhenSafe { get; private set; }
        internal static ConfigEntry<bool> HideOnLoadingScreens { get; private set; }
        internal static ConfigEntry<float> HorizontalOffset { get; private set; }
        internal static ConfigEntry<float> BottomOffset { get; private set; }

        private enum WyrdHuntTuningPreset
        {
            Custom,
            Default,
            Sparse,
            Stalker,
            CursedNight
        }

        private ConfigEntry<bool> _notificationsEnabled;
        private ConfigEntry<string> _notificationTextFormat;
        private ConfigEntry<bool> _showScentNumberInNotifications;
        private ConfigEntry<bool> _notifyOnStageChange;
        private ConfigEntry<bool> _notifyOnSafetyChange;
        private ConfigEntry<float> _notificationCooldownSeconds;
        private ConfigEntry<WyrdHuntTuningPreset> _huntTuningPreset;
        private ConfigEntry<string> _lastAppliedHuntTuningPreset;
        private ConfigEntry<bool> _enableRandomizedSpawns;
        private ConfigEntry<bool> _autoProfileOnly;
        private ConfigEntry<bool> _preserveWyrdspiritStalking;
        private ConfigEntry<bool> _enableHardHunts;
        private ConfigEntry<bool> _avoidImmediateRepeats;
        private ConfigEntry<int> _immediateRepeatWeightPercent;
        private ConfigEntry<int> _randomSeed;
        private ConfigEntry<bool> _logSelections;
        private ConfigEntry<bool> _disableFailedTemplatesForSession;
        private ConfigEntry<bool> _enableMixedHuntPacks;
        private ConfigEntry<int> _twoEnemyPackChancePercent;
        private ConfigEntry<int> _threeEnemyPackChancePercent;
        private ConfigEntry<bool> _allowHardPackMates;
        private ConfigEntry<int> _packMateSpawnDistanceJitterMeters;
        private ConfigEntry<int> _wyrdspiritSidecarWeight;
        private ConfigEntry<int> _wyrdspiritMaxPackCount;
        private ConfigEntry<int> _sameTierPackMateWeightPercent;
        private ConfigEntry<int> _sameFamilyPackMateWeightPercent;

        private readonly Dictionary<string, SpawnCandidate> _candidatesById =
            new Dictionary<string, SpawnCandidate>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, List<WeightedSpawnOption>> _optionsByLayer =
            new Dictionary<string, List<WeightedSpawnOption>>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _validatedTemplateGuids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _failedTemplateIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _randomLock = new object();
        private Harmony _harmony;
        private System.Random _random;
        private string _lastCandidateId;
        private FieldInfo _managerThreatField;
        private FieldInfo _managerLastInWyrdnessField;
        private FieldInfo _managerLastStageField;
        private FieldInfo _managerLastMeterValueField;
        private FieldInfo _managerHasMeterBaselineField;
        private Type _managerAccessorType;
        private MethodInfo _grailFloatingTextTryShowEventWithIconMethod;
        private MethodInfo _grailFloatingTextTryShowMethod;
        private MethodInfo _grailFloatingTextTryShowWithIconMethod;
        private float _lastNotificationTime = -9999f;
        private float _nextStatusSampleTime;
        private string _lastObservedStage;
        private int _lastObservedScent = -1;
        private bool? _lastObservedInWyrdness;
        private bool _hasObservedStatus;
        private bool _managerAccessorFailureLogged;
        private bool _grailFloatingTextBridgeResolved;
        private bool _grailFloatingTextUnavailableLogged;
        private bool _applyingHuntTuningPreset;

        private Type _profileType;
        private Type _executionModeType;
        private Type _conditionTagType;
        private ConstructorInfo _profileConstructor;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ResetConfigIfSchemaChanged();
            BindHudConfig();
            BindHuntTuningPresetConfig();
            BindRandomizationConfig();
            RegisterCandidates();
            RegisterWeightedOptions();
            Config.Save();

            int seed = _randomSeed.Value;
            _random = seed == 0 ? new System.Random() : new System.Random(seed);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(WyrdHuntAddonPlugin).Assembly);
            WyrdHuntAddonOptionalPatches.TryPatch(_harmony);
            LoadingUiTransitionPatch.TryPatch(_harmony);
            ThreatMeterTransitionHideTracker.Refresh();

            Logger.LogInfo(string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} loaded. Randomized spawns={2}, mixed packs={3}, hard hunts={4}, seed={5}.",
                PluginName,
                PluginVersion,
                _enableRandomizedSpawns.Value,
                _enableMixedHuntPacks.Value,
                _enableHardHunts.Value,
                seed == 0 ? "runtime" : seed.ToString(CultureInfo.InvariantCulture)));
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            ThreatMeterTransitionHideTracker.Shutdown();

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        internal static float GetMeterX()
        {
            float width = Mathf.Min(MeterWidth, Screen.width - HorizontalMargin);
            return Mathf.Max(0f, ((Screen.width - width) * 0.5f) + HorizontalOffset.Value);
        }

        internal static float GetMeterY()
        {
            float maxY = Mathf.Max(0f, Screen.height - MeterHeight);
            return Mathf.Clamp(Screen.height - BottomOffset.Value, 0f, maxY);
        }

        internal static string GetScentMeterMode()
        {
            string mode = ScentMeterMode == null ? null : ScentMeterMode.Value;
            if (string.IsNullOrWhiteSpace(mode))
            {
                return ScentMeterModeNotificationsOnly;
            }

            if (mode.Equals(ScentMeterModeParent, StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("Game", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
            {
                return ScentMeterModeParent;
            }

            if (mode.Equals(ScentMeterModeAutoHide, StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("Auto", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("Addon", StringComparison.OrdinalIgnoreCase))
            {
                return ScentMeterModeAutoHide;
            }

            return ScentMeterModeNotificationsOnly;
        }

        internal static bool ShouldSuppressScentMeter()
        {
            WyrdHuntAddonPlugin instance = Instance;
            return string.Equals(GetScentMeterMode(), ScentMeterModeNotificationsOnly, StringComparison.Ordinal) &&
                instance != null &&
                instance.CanUseGrailFloatingTextStatus();
        }

        internal static bool ShouldUseSafeMeterHide()
        {
            return string.Equals(GetScentMeterMode(), ScentMeterModeAutoHide, StringComparison.Ordinal) &&
                HideWhenSafe != null &&
                HideWhenSafe.Value;
        }

        internal static bool ShouldUseLoadingScreenMeterHide()
        {
            return string.Equals(GetScentMeterMode(), ScentMeterModeAutoHide, StringComparison.Ordinal) &&
                HideOnLoadingScreens != null &&
                HideOnLoadingScreens.Value;
        }

        internal void ObserveWyrdHuntStatus(object manager)
        {
            if (_notificationsEnabled == null || !_notificationsEnabled.Value || manager == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < _nextStatusSampleTime)
            {
                return;
            }

            _nextStatusSampleTime = now + 0.2f;

            WyrdHuntStatus status;
            if (!TryReadWyrdHuntStatus(manager, out status))
            {
                return;
            }

            if (!_hasObservedStatus)
            {
                _hasObservedStatus = true;
                _lastObservedStage = status.StageName;
                _lastObservedScent = status.ScentValue;
                _lastObservedInWyrdness = status.InWyrdness;
                return;
            }

            bool stageChanged = _notifyOnStageChange != null &&
                _notifyOnStageChange.Value &&
                !string.Equals(status.StageName, _lastObservedStage, StringComparison.Ordinal);

            bool safetyChanged = _notifyOnSafetyChange != null &&
                _notifyOnSafetyChange.Value &&
                status.InWyrdness.HasValue &&
                _lastObservedInWyrdness.HasValue &&
                status.InWyrdness.Value != _lastObservedInWyrdness.Value;

            _lastObservedStage = status.StageName;
            _lastObservedScent = status.ScentValue;
            _lastObservedInWyrdness = status.InWyrdness;

            if (!stageChanged && !safetyChanged)
            {
                return;
            }

            float cooldown = _notificationCooldownSeconds == null ? 0.75f : Math.Max(0f, _notificationCooldownSeconds.Value);
            if (now - _lastNotificationTime < cooldown)
            {
                return;
            }

            string text = BuildNotificationText(status, stageChanged, safetyChanged);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _lastNotificationTime = now;
            TryShowGrailFloatingText(text);
        }

        private bool TryReadWyrdHuntStatus(object manager, out WyrdHuntStatus status)
        {
            status = null;

            Type managerType = manager.GetType();
            if (_managerAccessorType != managerType)
            {
                _managerAccessorType = managerType;
                _managerThreatField = AccessTools.Field(managerType, "_threat");
                _managerLastInWyrdnessField = AccessTools.Field(managerType, "_lastInWyrdness");
                _managerLastStageField = AccessTools.Field(managerType, "_lastThreatMeterStage");
                _managerLastMeterValueField = AccessTools.Field(managerType, "_lastThreatMeterValue");
                _managerHasMeterBaselineField = AccessTools.Field(managerType, "_hasThreatMeterBaseline");
            }

            if (_managerThreatField == null &&
                _managerLastInWyrdnessField == null &&
                _managerLastStageField == null &&
                _managerLastMeterValueField == null)
            {
                LogManagerAccessorFailureOnce();
                return false;
            }

            try
            {
                if (_managerHasMeterBaselineField != null)
                {
                    object baseline = _managerHasMeterBaselineField.GetValue(manager);
                    if (baseline is bool && !(bool)baseline)
                    {
                        return false;
                    }
                }

                int scentValue = ReadScentValue(manager);
                string stageName = ReadStageName(manager);
                bool? inWyrdness = ReadNullableBool(_managerLastInWyrdnessField, manager);

                if (scentValue < 0 && string.IsNullOrWhiteSpace(stageName) && !inWyrdness.HasValue)
                {
                    return false;
                }

                status = new WyrdHuntStatus(stageName, scentValue, inWyrdness);
                return true;
            }
            catch (Exception exception)
            {
                if (!_managerAccessorFailureLogged)
                {
                    _managerAccessorFailureLogged = true;
                    Log.LogWarning("Could not read Wyrd Hunt status for compact notifications: " + exception.GetBaseException().Message);
                }

                return false;
            }
        }

        private int ReadScentValue(object manager)
        {
            if (_managerLastMeterValueField != null)
            {
                object value = _managerLastMeterValueField.GetValue(manager);
                if (value is int)
                {
                    return Mathf.Clamp((int)value, 0, 100);
                }
            }

            if (_managerThreatField != null)
            {
                object value = _managerThreatField.GetValue(manager);
                if (value is float)
                {
                    return Mathf.Clamp(Mathf.RoundToInt((float)value), 0, 100);
                }
            }

            return -1;
        }

        private string ReadStageName(object manager)
        {
            if (_managerLastStageField == null)
            {
                return string.Empty;
            }

            object value = _managerLastStageField.GetValue(manager);
            return value == null ? string.Empty : value.ToString();
        }

        private static bool? ReadNullableBool(FieldInfo field, object instance)
        {
            if (field == null)
            {
                return null;
            }

            object value = field.GetValue(instance);
            if (value is bool)
            {
                return (bool)value;
            }

            return null;
        }

        private void LogManagerAccessorFailureOnce()
        {
            if (_managerAccessorFailureLogged)
            {
                return;
            }

            _managerAccessorFailureLogged = true;
            Log.LogWarning("Could not find Wyrd Hunt status fields; compact Wyrd Hunt status notifications are disabled.");
        }

        private string BuildNotificationText(WyrdHuntStatus status, bool stageChanged, bool safetyChanged)
        {
            if (safetyChanged && status.InWyrdness.HasValue)
            {
                return FormatWyrdnessState(status.InWyrdness);
            }

            if (!stageChanged)
            {
                return string.Empty;
            }

            string format = _notificationTextFormat == null ? null : _notificationTextFormat.Value;
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "Wyrd Scent: {stage}";
            }

            string stage = FormatStageName(status.StageName);
            string scent = status.ScentValue >= 0 ? status.ScentValue.ToString(CultureInfo.InvariantCulture) : "?";
            string state = FormatWyrdnessState(status.InWyrdness);
            string safe = status.InWyrdness.HasValue
                ? (status.InWyrdness.Value ? "false" : "true")
                : string.Empty;

            string text = format
                .Replace("{stage}", stage)
                .Replace("{scent}", scent)
                .Replace("{state}", state)
                .Replace("{safe}", safe);

            if (_showScentNumberInNotifications != null &&
                _showScentNumberInNotifications.Value &&
                status.ScentValue >= 0 &&
                format.IndexOf("{scent}", StringComparison.OrdinalIgnoreCase) < 0)
            {
                text = text.TrimEnd() + " " + scent;
            }

            return text;
        }

        private static string FormatStageName(string rawStage)
        {
            if (string.IsNullOrWhiteSpace(rawStage))
            {
                return "Unknown";
            }

            return rawStage.Replace("_", " ");
        }

        private static string FormatWyrdnessState(bool? inWyrdness)
        {
            if (!inWyrdness.HasValue)
            {
                return string.Empty;
            }

            return inWyrdness.Value ? "Exposed to Wyrdness" : "Safe from Wyrdness";
        }

        private bool TryShowGrailFloatingText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!TryResolveGrailFloatingTextBridge())
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text is not loaded; Wyrd Hunt Addon status notifications are unavailable.");
                return false;
            }

            try
            {
                object result;
                if (_grailFloatingTextTryShowEventWithIconMethod != null)
                {
                    result = _grailFloatingTextTryShowEventWithIconMethod.Invoke(
                        null,
                        new object[] { PluginGuid, GrailFloatingTextWyrdStatusEventId, text, "Wyrd", "Status", "Normal", GrailFloatingTextWyrdStatusEventId, "wyrd", GrailFloatingTextMediumDurationBucket, 0.25f, 0.9f });
                }
                else if (_grailFloatingTextTryShowWithIconMethod != null)
                {
                    result = _grailFloatingTextTryShowWithIconMethod.Invoke(
                        null,
                        new object[] { PluginGuid, text, "Wyrd", "Status", "Normal", GrailFloatingTextWyrdStatusEventId, "wyrd", 0.0f, 0.25f, 0.9f });
                }
                else
                {
                    result = _grailFloatingTextTryShowMethod.Invoke(
                        null,
                        new object[] { PluginGuid, text, "Wyrd", "Status", "Normal", GrailFloatingTextWyrdStatusEventId, 0.0f, 0.25f, 0.9f });
                }

                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                LogGrailFloatingTextUnavailableOnce("Grail Floating Text failed to show a Wyrd Hunt status notification: " + exception.GetBaseException().Message);
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

        private bool CanUseGrailFloatingTextStatus()
        {
            if (_notificationsEnabled == null || !_notificationsEnabled.Value)
            {
                return false;
            }

            bool canNotify = (_notifyOnStageChange != null && _notifyOnStageChange.Value) ||
                (_notifyOnSafetyChange != null && _notifyOnSafetyChange.Value);
            return canNotify && TryResolveGrailFloatingTextBridge();
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

        internal void TryRandomizeSelection(ref bool result, object[] args)
        {
            if (!result || !_enableRandomizedSpawns.Value || args == null || args.Length < 6)
            {
                return;
            }

            string configuredProfileId = args[2] as string;
            if (_autoProfileOnly.Value && !IsAutoProfile(configuredProfileId))
            {
                return;
            }

            object originalProfile = args[3];
            if (originalProfile == null)
            {
                return;
            }

            string originalProfileId = GetStringProperty(originalProfile, "ProfileId", string.Empty);
            string originalExecutionMode = GetPropertyString(originalProfile, "ExecutionMode");
            if (_preserveWyrdspiritStalking.Value &&
                string.Equals(originalProfileId, "wyrdspirit-stalking-candidate", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_preserveWyrdspiritStalking.Value ||
                string.Equals(originalExecutionMode, "NativeCombatAmbush", StringComparison.OrdinalIgnoreCase))
            {
                string layerName = GetPropertyString(originalProfile, "Layer");
                SpawnCandidate selected = SelectCandidate(layerName);
                if (selected == null)
                {
                    return;
                }

                string originalGuid = GetStringProperty(originalProfile, "TemplateGuid", string.Empty);
                string originalName = GetStringProperty(originalProfile, "TemplateName", string.Empty);
                string randomReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Wyrd Hunt Addon weighted randomizer selected {0} ({1}) for {2}.",
                    selected.Id,
                    selected.TemplateName,
                    layerName);

                _lastCandidateId = selected.Id;

                if (SameTemplate(selected, originalGuid, originalName))
                {
                    args[4] = AppendReason(args[4] as string, randomReason + " Original selected profile already matches.");
                    LogSelection(originalProfileId, selected, layerName, false);
                    return;
                }

                object replacement = CreateCustomProfile(selected, originalProfile, layerName);
                if (replacement == null)
                {
                    return;
                }

                args[3] = replacement;
                args[4] = AppendReason(args[4] as string, randomReason);
                LogSelection(originalProfileId, selected, layerName, true);
            }
        }

        internal bool IsApprovedCustomPlan(object plan)
        {
            if (plan == null)
            {
                return false;
            }

            string profileId = GetStringProperty(plan, "ProfileId", string.Empty);
            string candidateId;
            if (!TryParseAddonProfileId(profileId, out candidateId))
            {
                return false;
            }

            SpawnCandidate candidate;
            if (!_candidatesById.TryGetValue(candidateId, out candidate))
            {
                return false;
            }

            string guid = GetStringProperty(plan, "TemplateGuid", string.Empty);
            string name = GetStringProperty(plan, "TemplateName", string.Empty);
            int hunterCount = GetIntProperty(plan, "HunterCount", 0);

            return hunterCount == 1 && SameTemplate(candidate, guid, name);
        }

        internal void TrySpawnMixedPackMates(object plan, ref string reason)
        {
            if (plan == null || !_enableMixedHuntPacks.Value)
            {
                return;
            }

            string profileId = GetStringProperty(plan, "ProfileId", string.Empty);
            string candidateId;
            if (!TryParseAddonProfileId(profileId, out candidateId))
            {
                return;
            }

            SpawnCandidate primary;
            if (!_candidatesById.TryGetValue(candidateId, out primary))
            {
                return;
            }

            string layerName = GetPropertyString(plan, "Layer");
            if (!AllowsMixedHuntPackLayer(layerName))
            {
                return;
            }

            int extraCount = RollExtraPackMateCount();
            if (extraCount <= 0)
            {
                return;
            }

            List<SpawnCandidate> packMates = SelectPackMates(primary, layerName, extraCount);
            if (packMates.Count == 0)
            {
                return;
            }

            int distanceMeters = GetIntProperty(plan, "SpawnDistanceMeters", 45);
            List<string> spawnedNames = new List<string>();
            int totalSpawned = 0;
            for (int i = 0; i < packMates.Count; i++)
            {
                int requestedSpawnCount = RollPackMateSpawnCount(packMates[i]);
                int spawnedForCandidate = 0;
                for (int spawnIndex = 0; spawnIndex < requestedSpawnCount; spawnIndex++)
                {
                    if (TrySpawnPackMate(packMates[i], distanceMeters))
                    {
                        spawnedForCandidate++;
                    }
                }

                if (spawnedForCandidate > 0)
                {
                    totalSpawned += spawnedForCandidate;
                    spawnedNames.Add(FormatPackMateSummary(packMates[i], spawnedForCandidate));
                }
            }

            if (totalSpawned == 0)
            {
                return;
            }

            string summary = string.Join(", ", spawnedNames.ToArray());
            reason = AppendReason(
                reason,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Wyrd Hunt Addon added {0} mixed pack mate{1}: {2}.",
                    totalSpawned,
                    totalSpawned == 1 ? string.Empty : "s",
                    summary));

            if (_logSelections.Value)
            {
                Log.LogInfo(string.Format(
                    CultureInfo.InvariantCulture,
                    "Wyrd Hunt Addon spawned mixed pack for {0} ({1}) on {2}: {3}.",
                    primary.Id,
                    primary.TemplateName,
                    layerName,
                    summary));
            }
        }

        internal bool TryDescribeAddonProfile(string profileId, out string roleSummary, out string pressureIntent)
        {
            string candidateId;
            SpawnCandidate candidate;
            roleSummary = null;
            pressureIntent = null;

            if (!TryParseAddonProfileId(profileId, out candidateId) ||
                !_candidatesById.TryGetValue(candidateId, out candidate))
            {
                return false;
            }

            roleSummary = "addon-random-" + candidate.Id;
            pressureIntent = candidate.Hard ? "hard-weighted-native-ambush" : "weighted-native-ambush";
            return true;
        }

        private void BindHudConfig()
        {
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                "Configuration layout version. Older layouts are backed up and regenerated.");

            ScentMeterMode = Config.Bind(
                "Visibility",
                "Scent Meter Mode",
                ScentMeterModeNotificationsOnly,
                "Parent keeps Wyrd Hunt's meter behavior, AutoHide hides it only in safe/loading contexts, and NotificationsOnly hides the meter only when Grail Floating Text is loaded and status notifications are enabled.");

            HideWhenSafe = Config.Bind(
                "Visibility",
                "Hide When Safe From Wyrdness",
                true,
                "In AutoHide meter mode, hide the meter while Wyrd Hunt reports that the player is safe from Wyrdness.");

            HideOnLoadingScreens = Config.Bind(
                "Visibility",
                "Hide On Loading Screens",
                true,
                "In AutoHide meter mode, hide the meter during title, loading, startup, portal transitions, and other non-hero scenes.");

            HorizontalOffset = Config.Bind(
                "Position",
                "Horizontal Offset",
                0f,
                new ConfigDescription(
                    "Horizontal offset in pixels from screen center. Positive values move the meter right.",
                    new AcceptableValueRange<float>(-1000f, 1000f),
                    new object[0]));

            BottomOffset = Config.Bind(
                "Position",
                "Bottom Offset",
                205f,
                new ConfigDescription(
                    "Distance in pixels from the bottom of the screen to the top of the meter.",
                    new AcceptableValueRange<float>(68f, 1000f),
                    new object[0]));

            _notificationsEnabled = Config.Bind(
                "Notifications",
                "Notifications Enabled",
                true,
                "Show Grail Floating Text text when Wyrd Hunt status changes.");

            _notificationTextFormat = Config.Bind(
                "Notifications",
                "Notification Text Format",
                "Wyrd Scent: {stage}",
                "Grail Floating Text stage-change text. Tokens: {stage}, {scent}, {state}, {safe}. Safety changes use fixed clean text.");

            _showScentNumberInNotifications = Config.Bind(
                "Notifications",
                "Show Scent Number",
                false,
                "Append the current Wyrd Scent number to stage-change notifications when the text format does not already include {scent}.");

            _notifyOnStageChange = Config.Bind(
                "Notifications",
                "Notify On Stage Change",
                true,
                "Show Grail Floating Text text when Wyrd Hunt's stage label changes.");

            _notifyOnSafetyChange = Config.Bind(
                "Notifications",
                "Notify On Safety Change",
                true,
                "Show Grail Floating Text text when Wyrd Hunt changes between safe and exposed states.");

            _notificationCooldownSeconds = Config.Bind(
                "Notifications",
                "Notification Cooldown Seconds",
                0.75f,
                new ConfigDescription(
                    "Minimum seconds between Wyrd Hunt status notifications.",
                    new AcceptableValueRange<float>(0f, 10f),
                    new object[0]));
        }

        private void BindHuntTuningPresetConfig()
        {
            _huntTuningPreset = Config.Bind(
                "Preset",
                "Hunt Tuning Preset",
                WyrdHuntTuningPreset.Custom,
                "One-time Wyrd Hunt tuning template. Selecting Default, Sparse, Stalker, or CursedNight immediately writes parent Wyrd Hunt hunt/threat settings, saves them, then resets this selector to Custom.");

            _lastAppliedHuntTuningPreset = Config.Bind(
                "Preset",
                "Last Applied Hunt Tuning Preset",
                "None",
                "Informational only. Shows the last preset that wrote values into kane.tgfoa.wyrd-hunt.cfg.");

            _huntTuningPreset.SettingChanged += HuntTuningPresetSettingChanged;
            if (_huntTuningPreset.Value != WyrdHuntTuningPreset.Custom)
            {
                ApplyHuntTuningPreset(_huntTuningPreset.Value);
            }
        }

        private void HuntTuningPresetSettingChanged(object sender, EventArgs args)
        {
            if (_applyingHuntTuningPreset ||
                _huntTuningPreset == null ||
                _huntTuningPreset.Value == WyrdHuntTuningPreset.Custom)
            {
                return;
            }

            ApplyHuntTuningPreset(_huntTuningPreset.Value);
        }

        private void ApplyHuntTuningPreset(WyrdHuntTuningPreset preset)
        {
            HuntTuningPresetValues values = GetHuntTuningPresetValues(preset);
            if (values == null)
            {
                return;
            }

            _applyingHuntTuningPreset = true;

            try
            {
                bool liveParentConfig;
                ConfigFile parentConfig = ResolveParentWyrdHuntConfig(out liveParentConfig);
                if (parentConfig == null)
                {
                    Log.LogWarning("Could not apply Wyrd Hunt tuning preset because the parent Wyrd Hunt config was not available.");
                    return;
                }

                SetParentIntValue(parentConfig, "Hunt", "HunterPackThreshold", values.HunterPackThreshold, 0, 100, "Threat threshold where stronger hunter-pack hunts may begin.");
                SetParentFloatValue(parentConfig, "Hunt", "BossThreshold", values.BossThreshold, 0f, 100f, "Threat threshold where boss-tier hunts may begin.");
                SetParentIntValue(parentConfig, "Hunt", "MinSecondsBetweenHunts", values.MinSecondsBetweenHunts, 0, 3600, "Fallback cooldown between hunt requests.");
                SetParentIntValue(parentConfig, "Hunt", "MaxHuntersAlive", values.MaxHuntersAlive, 0, 20, "Planned maximum active hunters.");
                SetParentBoolValue(parentConfig, "Hunt", "AllowBossHunter", values.AllowBossHunter, "Allow the named boss hunter once boss spawning is researched.");
                SetParentIntValue(parentConfig, "Hunt", "HunterSpawnDistanceMeters", values.HunterSpawnDistanceMeters, 20, 60, "Requested hunt spawn distance from the player.");
                SetParentIntValue(parentConfig, "Hunt", "ActiveEncounterLockSeconds", values.ActiveEncounterLockSeconds, 0, 600, "Minimum lockout while a hunt encounter is active.");
                SetParentIntValue(parentConfig, "Hunt", "ActiveEncounterTimeoutSeconds", values.ActiveEncounterTimeoutSeconds, 10, 1800, "Maximum active encounter age before the encounter lock can expire.");
                SetParentIntValue(parentConfig, "Hunt", "MaxHuntsPerScene", values.MaxHuntsPerScene, 0, 100, "Maximum hunt budget units allowed in one loaded scene.");
                SetParentIntValue(parentConfig, "Hunt", "MaxHuntsPerSession", values.MaxHuntsPerSession, 0, 100, "Maximum hunt budget units allowed in one plugin session.");
                SetParentIntValue(parentConfig, "Hunt", "GuardAwarenessMaxGuardsPerEncounter", values.GuardAwarenessMaxGuardsPerEncounter, 0, 12, "Maximum nearby guards that can respond to an active hunt.");
                SetParentFloatValue(parentConfig, "Hunt", "CustomEnemyAiRadiusMeters", values.CustomEnemyAiRadiusMeters, 8f, 80f, "Radius for active-hunt enemy combat re-acquisition scans.");
                SetParentFloatValue(parentConfig, "Hunt", "CustomEnemyAiScanIntervalSeconds", values.CustomEnemyAiScanIntervalSeconds, 0.25f, 10f, "Seconds between active-hunt enemy AI scans.");
                SetParentBoolValue(parentConfig, "Hunt", "EnableShargPursuitMovement", values.EnableShargPursuitMovement, "Allow controlled pursuit movement refreshes for the layer-4 Sharg hunt.");
                SetParentFloatValue(parentConfig, "Threat", "ThreatGainPerMinute", values.ThreatGainPerMinute, 0f, 120f, "Wyrd Scent gained per minute during live Wyrdness exposure.");
                SetParentFloatValue(parentConfig, "Threat", "ThreatGainWhileSprintingPerMinute", values.ThreatGainWhileSprintingPerMinute, 0f, 120f, "Extra Wyrd Scent gained per minute while sprinting or fast swimming.");
                SetParentFloatValue(parentConfig, "Threat", "ThreatGainOnCombatAction", values.ThreatGainOnCombatAction, 0f, 100f, "Wyrd Scent gained from each throttled combat proficiency event.");
                SetParentFloatValue(parentConfig, "Threat", "ThreatGainOnKill", values.ThreatGainOnKill, 0f, 100f, "Wyrd Scent gained when killing a Wyrd-converted or Wyrdness-bound enemy.");
                SetParentFloatValue(parentConfig, "Threat", "ThreatGainOnLoot", values.ThreatGainOnLoot, 0f, 100f, "Wyrd Scent gained from looting during Wyrdness.");
                SetParentFloatValue(parentConfig, "Threat", "ThreatDecayNearSafeLight", values.ThreatDecayNearSafeLight, 0f, 120f, "Wyrd Scent decay per minute while safe from Wyrdness.");
                SetParentFloatValue(parentConfig, "Threat", "CombatThreatCooldownSeconds", values.CombatThreatCooldownSeconds, 0f, 10f, "Cooldown between combat-action Wyrd Scent gains.");

                parentConfig.Save();
                _lastAppliedHuntTuningPreset.Value = FormatHuntTuningPresetName(preset);
                _huntTuningPreset.Value = WyrdHuntTuningPreset.Custom;
                Config.Save();

                Log.LogInfo(string.Format(
                    CultureInfo.InvariantCulture,
                    "Applied {0} Wyrd Hunt tuning preset to {1}. Parent config source={2}.",
                    FormatHuntTuningPresetName(preset),
                    parentConfig.ConfigFilePath,
                    liveParentConfig ? "loaded Wyrd Hunt plugin" : "fallback config file"));
            }
            catch (Exception exception)
            {
                Log.LogError("Failed to apply Wyrd Hunt tuning preset " + preset + ": " + exception.GetBaseException().Message);
            }
            finally
            {
                _applyingHuntTuningPreset = false;
            }
        }

        private ConfigFile ResolveParentWyrdHuntConfig(out bool liveParentConfig)
        {
            liveParentConfig = false;

            try
            {
                PluginInfo parentInfo;
                if (Chainloader.PluginInfos.TryGetValue(ParentWyrdHuntPluginGuid, out parentInfo) &&
                    parentInfo != null &&
                    parentInfo.Instance != null)
                {
                    BaseUnityPlugin parentPlugin = parentInfo.Instance as BaseUnityPlugin;
                    if (parentPlugin != null && parentPlugin.Config != null)
                    {
                        liveParentConfig = true;
                        return parentPlugin.Config;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.LogWarning("Could not access loaded Wyrd Hunt config: " + exception.GetBaseException().Message);
            }

            try
            {
                string configPath = Path.Combine(Paths.ConfigPath, ParentWyrdHuntPluginGuid + ".cfg");
                return new ConfigFile(configPath, true);
            }
            catch (Exception exception)
            {
                Log.LogWarning("Could not open Wyrd Hunt fallback config file: " + exception.GetBaseException().Message);
                return null;
            }
        }

        private static HuntTuningPresetValues GetHuntTuningPresetValues(WyrdHuntTuningPreset preset)
        {
            switch (preset)
            {
                case WyrdHuntTuningPreset.Default:
                    return new HuntTuningPresetValues(25, 85f, 300, 4, true, 30, 30, 120, 0, 0, 4, 45f, 0.75f, true, 24f, 4f, 1f, 12f, 4f, 10f, 1f);
                case WyrdHuntTuningPreset.Sparse:
                    return new HuntTuningPresetValues(45, 100f, 900, 1, false, 45, 420, 300, 1, 4, 1, 22f, 4f, false, 10f, 2f, 0.5f, 4f, 1f, 16f, 4f);
                case WyrdHuntTuningPreset.Stalker:
                    return new HuntTuningPresetValues(35, 95f, 720, 1, true, 40, 360, 420, 1, 6, 2, 32f, 2f, true, 14f, 3f, 0.75f, 6f, 2f, 12f, 3f);
                case WyrdHuntTuningPreset.CursedNight:
                    return new HuntTuningPresetValues(25, 90f, 480, 2, true, 35, 240, 360, 2, 10, 3, 38f, 1.25f, true, 20f, 5f, 1f, 10f, 4f, 8f, 2f);
                default:
                    return null;
            }
        }

        private static void SetParentIntValue(
            ConfigFile config,
            string section,
            string key,
            int value,
            int min,
            int max,
            string description)
        {
            ConfigEntry<int> entry;
            if (!config.TryGetEntry<int>(section, key, out entry))
            {
                entry = config.Bind(
                    section,
                    key,
                    value,
                    new ConfigDescription(description, new AcceptableValueRange<int>(min, max), new object[0]));
            }

            entry.Value = value;
        }

        private static void SetParentFloatValue(
            ConfigFile config,
            string section,
            string key,
            float value,
            float min,
            float max,
            string description)
        {
            ConfigEntry<float> entry;
            if (!config.TryGetEntry<float>(section, key, out entry))
            {
                entry = config.Bind(
                    section,
                    key,
                    value,
                    new ConfigDescription(description, new AcceptableValueRange<float>(min, max), new object[0]));
            }

            entry.Value = value;
        }

        private static void SetParentBoolValue(
            ConfigFile config,
            string section,
            string key,
            bool value,
            string description)
        {
            ConfigEntry<bool> entry;
            if (!config.TryGetEntry<bool>(section, key, out entry))
            {
                entry = config.Bind(section, key, value, description);
            }

            entry.Value = value;
        }

        private static string FormatHuntTuningPresetName(WyrdHuntTuningPreset preset)
        {
            return preset == WyrdHuntTuningPreset.CursedNight ? "CursedNight" : preset.ToString();
        }

        private void BindRandomizationConfig()
        {
            _enableRandomizedSpawns = Config.Bind(
                "Randomization",
                "Enable Randomized Spawns",
                true,
                "Replace Wyrd Hunt's selected native hunter with a weighted random whitelisted spawn from the same hunt layer.");

            _autoProfileOnly = Config.Bind(
                "Randomization",
                "Auto Profile Only",
                true,
                "Only randomize when Wyrd Hunt is using HunterProfileId=auto. Disable to also randomize manually configured Wyrd Hunt profiles.");

            _preserveWyrdspiritStalking = Config.Bind(
                "Randomization",
                "Preserve Wyrdspirit Stalking",
                true,
                "Leave Wyrd Hunt's special Wyrdspirit stalking encounter untouched.");

            _enableHardHunts = Config.Bind(
                "Randomization",
                "Enable Hard Hunts",
                false,
                "Allow the hard Pursuit and Marked hunt entries to participate in weighted selection.");

            _avoidImmediateRepeats = Config.Bind(
                "Randomization",
                "Avoid Immediate Repeats",
                true,
                "Reduce or remove the previous selected monster's weight when another valid monster is available.");

            _immediateRepeatWeightPercent = Config.Bind(
                "Randomization",
                "Immediate Repeat Weight Percent",
                0,
                new ConfigDescription(
                    "Percent of normal weight kept for the previous selected monster when immediate repeats are avoided. 0 prevents direct repeats when alternatives exist.",
                    new AcceptableValueRange<int>(0, 100),
                    new object[0]));

            _randomSeed = Config.Bind(
                "Randomization",
                "Random Seed",
                0,
                new ConfigDescription(
                    "0 uses a runtime random seed. Any positive value makes the weighted sequence deterministic for that game launch.",
                    new AcceptableValueRange<int>(0, int.MaxValue),
                    new object[0]));

            _logSelections = Config.Bind(
                "Randomization",
                "Log Selections",
                true,
                "Log each weighted hunt selection and template fallback.");

            _disableFailedTemplatesForSession = Config.Bind(
                "Randomization",
                "Disable Failed Templates For Session",
                true,
                "When a whitelisted template cannot be resolved, remove it from the pool until the game is restarted.");

            _enableMixedHuntPacks = Config.Bind(
                "Mixed Packs",
                "Enable Mixed Hunt Packs",
                true,
                "After a randomized hunt fires, rarely spawn one or two extra same-or-lower-tier pack mates through the native spawn helper. Extras are pressure enemies, not official hunt objectives.");

            _twoEnemyPackChancePercent = Config.Bind(
                "Mixed Packs",
                "Two Enemy Pack Chance Percent",
                18,
                new ConfigDescription(
                    "Chance that a randomized Stalking, Hunter, Pursuit, or Marked hunt becomes a two-enemy pack. Contact hunts remain single-only.",
                    new AcceptableValueRange<int>(0, 100),
                    new object[0]));

            _threeEnemyPackChancePercent = Config.Bind(
                "Mixed Packs",
                "Three Enemy Pack Chance Percent",
                3,
                new ConfigDescription(
                    "Chance that a randomized Stalking, Hunter, Pursuit, or Marked hunt becomes a three-enemy pack. This is checked before the two-enemy chance.",
                    new AcceptableValueRange<int>(0, 100),
                    new object[0]));

            _allowHardPackMates = Config.Bind(
                "Mixed Packs",
                "Allow Hard Pack Mates",
                false,
                "Allow hard-list entries to appear as extra pack mates when Enable Hard Hunts is also enabled.");

            _packMateSpawnDistanceJitterMeters = Config.Bind(
                "Mixed Packs",
                "Pack Mate Spawn Distance Jitter Meters",
                6,
                new ConfigDescription(
                    "Small random distance offset for extra pack mates so they do not all request exactly the same radius.",
                    new AcceptableValueRange<int>(0, 20),
                    new object[0]));

            _wyrdspiritSidecarWeight = Config.Bind(
                "Mixed Packs",
                "Wyrdspirit Sidecar Weight",
                80,
                new ConfigDescription(
                    "Extra sidecar-only weight for Wyrdspirit pack mates. 0 disables Wyrdspirit sidecars.",
                    new AcceptableValueRange<int>(0, 1000),
                    new object[0]));

            _wyrdspiritMaxPackCount = Config.Bind(
                "Mixed Packs",
                "Wyrdspirit Max Pack Count",
                4,
                new ConfigDescription(
                    "When a Wyrdspirit sidecar is selected, spawn a random count from 1 up to this value.",
                    new AcceptableValueRange<int>(1, 4),
                    new object[0]));

            _sameTierPackMateWeightPercent = Config.Bind(
                "Mixed Packs",
                "Same Tier Pack Mate Weight Percent",
                25,
                new ConfigDescription(
                    "Weight percent kept for sidecar candidates at the same tier as the primary hunt target.",
                    new AcceptableValueRange<int>(0, 100),
                    new object[0]));

            _sameFamilyPackMateWeightPercent = Config.Bind(
                "Mixed Packs",
                "Same Family Pack Mate Weight Percent",
                35,
                new ConfigDescription(
                    "Additional weight percent kept when the sidecar candidate appears to be from the same creature family as the primary target.",
                    new AcceptableValueRange<int>(0, 100),
                    new object[0]));
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                const string schemaPrefix = "ConfigSchemaVersion =";
                if (!line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                int.TryParse(
                    line.Substring(schemaPrefix.Length).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out storedSchemaVersion);
                break;
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
                        "Could not restore the previous Wyrd Hunt Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Wyrd Hunt Addon config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void RegisterCandidates()
        {
            AddCandidate("wyrdspirit", "843643575fa01ba4292e60afb9291fea", "Spec_EnemyMonster_T1_Wyrdspirit", 1, false);
            AddCandidate("redcap", "a32e5074492cce34f89ff0667fdb41b7", "Spec_EnemyMonster_T1_Redcap", 2, false);
            AddCandidate("corpseeater", "1a41678c288c2264c8bcfad7a6eb3ba3", "Spec_EnemyMonster_T1_CorpseEater", 3, false);
            AddCandidate("sharg", "324e9b5ed131ce34eb12a520cdb2b52a", "Spec_EnemyMonster_T2_ShargHoS", 4, false);
            AddCandidate("ogre", "3f7d4ccf62c440b40b1fca822ef6ac1b", "Spec_EnemyMonster_T3_Ogre", 5, false);
            AddCandidate("wyrdspawn-t1", "237e11569d32e7540ae8c5207a89dd3d", "Spec_EnemyMonster_T1_Wyrdspawn", 1, false);
            AddCandidate("wyrdspawn-t2", "64ff443515c6aa24caccab167e2bfec5", "Spec_EnemyMonster_T2_Wyrdspawn", 2, false);
            AddCandidate("wyrdspawn-t2-better", "3fd487b2c44772f45955016b27777600", "Spec_EnemyMonster_T2better_Wyrdspawn", 2, false);
            AddCandidate("mistling-hos-t2", "db4a2490470378f49be51ff8848541e9", "Spec_EnemyMonster_T2_Mistling_Hos", 2, false);
            AddCandidate("wyrdslime-t3", "7608d1a60413d53478d18f907ee90381", "Spec_EnemyMonster_T3_Wyrdslime", 3, false);
            AddCandidate("wyrdspawn-t3", "dced14b6fb3424b4f9c37b361cbcc7e8", "Spec_EnemyMonster_T3_Wyrdspawn", 3, false);
            AddCandidate("wyrdspawn-t3-better", "7727306438f4a6b4f88bc9ef61807b69", "Spec_EnemyMonster_T3better_Wyrdspawn", 3, false);
            AddCandidate("mistling-cuanacht-t3", "f673e122b6f7e984fab5758d91f84031", "Spec_EnemyMonster_T3_Mistling_Cuanacht", 3, false);
            AddCandidate("wyrdspawn-t4", "3f1bd5b3d4dad764f93a17fac3cd9b1e", "Spec_EnemyMonster_T4_Wyrdspawn", 4, true);
            AddCandidate("wyrdspawn-t4-better", "376438d3c610f1741a4182319b0819b1", "Spec_EnemyMonster_T4better_Wyrdspawn", 4, true);
            AddCandidate("mistling-forlorn-t4", "6f7fcf075b9e8f64495fc893f853bceb", "Spec_EnemyMonster_T4_Mistling_Forlorn", 4, true);
            AddCandidate("wyrdspawn-t5", "ae8cba51034ffdc478acd3fb2cb9bea6", "Spec_EnemyMonster_T5_Wyrdspawn", 5, true);
            AddCandidate("wyrdspawn-t5-better", "cd0778f1100d8da4d927fb096da53441", "Spec_EnemyMonster_T5better_Wyrdspawn", 5, true);
            AddCandidate("wyrdheir-t6", "11cd7b8d77ff2774092d5f82c6193224", "Spec_EnemyMonster_T6_Wyrdheir", 6, true);
            AddCandidate("wyrdspawn-t6", "529fdba73eae1c9439b5f601225d0446", "Spec_EnemyMonster_T6_Wyrdspawn", 6, true);
        }

        private void RegisterWeightedOptions()
        {
            AddWeightedOption("Contact", "Weights.Contact", "wyrdspirit", 100);

            AddWeightedOption("Stalking", "Weights.Stalking", "redcap", 40);
            AddWeightedOption("Stalking", "Weights.Stalking", "wyrdspawn-t1", 25);
            AddWeightedOption("Stalking", "Weights.Stalking", "wyrdspawn-t2", 15);
            AddWeightedOption("Stalking", "Weights.Stalking", "mistling-hos-t2", 12);
            AddWeightedOption("Stalking", "Weights.Stalking", "wyrdspawn-t2-better", 8);

            AddWeightedOption("Hunter", "Weights.Hunter", "corpseeater", 35);
            AddWeightedOption("Hunter", "Weights.Hunter", "wyrdspawn-t2", 10);
            AddWeightedOption("Hunter", "Weights.Hunter", "mistling-hos-t2", 10);
            AddWeightedOption("Hunter", "Weights.Hunter", "wyrdspawn-t3", 15);
            AddWeightedOption("Hunter", "Weights.Hunter", "wyrdslime-t3", 15);
            AddWeightedOption("Hunter", "Weights.Hunter", "mistling-cuanacht-t3", 10);
            AddWeightedOption("Hunter", "Weights.Hunter", "wyrdspawn-t3-better", 5);

            AddWeightedOption("Pursuit", "Weights.Pursuit", "sharg", 45);
            AddWeightedOption("Pursuit", "Weights.Pursuit", "corpseeater", 10);
            AddWeightedOption("Pursuit", "Weights.Pursuit", "wyrdspawn-t3", 15);
            AddWeightedOption("Pursuit", "Weights.Pursuit", "wyrdslime-t3", 12);
            AddWeightedOption("Pursuit", "Weights.Pursuit", "wyrdspawn-t3-better", 10);
            AddWeightedOption("Pursuit", "Weights.Pursuit", "mistling-cuanacht-t3", 8);
            AddWeightedOption("Pursuit", "Weights.Pursuit Hard", "wyrdspawn-t4", 12);
            AddWeightedOption("Pursuit", "Weights.Pursuit Hard", "mistling-forlorn-t4", 8);
            AddWeightedOption("Pursuit", "Weights.Pursuit Hard", "wyrdspawn-t4-better", 6);
            AddWeightedOption("Pursuit", "Weights.Pursuit Hard", "wyrdspawn-t5", 4);
            AddWeightedOption("Pursuit", "Weights.Pursuit Hard", "wyrdspawn-t5-better", 2);

            AddWeightedOption("Marked", "Weights.Marked", "ogre", 55);
            AddWeightedOption("Marked", "Weights.Marked", "sharg", 10);
            AddWeightedOption("Marked", "Weights.Marked", "wyrdspawn-t3-better", 12);
            AddWeightedOption("Marked", "Weights.Marked", "wyrdslime-t3", 10);
            AddWeightedOption("Marked", "Weights.Marked", "mistling-cuanacht-t3", 8);
            AddWeightedOption("Marked", "Weights.Marked", "wyrdspawn-t3", 5);
            AddWeightedOption("Marked", "Weights.Marked Hard", "wyrdspawn-t4", 14);
            AddWeightedOption("Marked", "Weights.Marked Hard", "mistling-forlorn-t4", 10);
            AddWeightedOption("Marked", "Weights.Marked Hard", "wyrdspawn-t4-better", 8);
            AddWeightedOption("Marked", "Weights.Marked Hard", "wyrdspawn-t5", 7);
            AddWeightedOption("Marked", "Weights.Marked Hard", "wyrdspawn-t5-better", 5);
            AddWeightedOption("Marked", "Weights.Marked Hard", "wyrdheir-t6", 3);
            AddWeightedOption("Marked", "Weights.Marked Hard", "wyrdspawn-t6", 3);
        }

        private void AddCandidate(string id, string templateGuid, string templateName, int tier, bool hard)
        {
            _candidatesById[id] = new SpawnCandidate(id, templateGuid, templateName, tier, hard);
        }

        private void AddWeightedOption(string layer, string section, string candidateId, int defaultWeight)
        {
            SpawnCandidate candidate;
            if (!_candidatesById.TryGetValue(candidateId, out candidate))
            {
                throw new InvalidOperationException("Missing Wyrd Hunt randomizer candidate: " + candidateId);
            }

            ConfigEntry<int> weight = Config.Bind(
                section,
                candidateId,
                defaultWeight,
                new ConfigDescription(
                    string.Format(CultureInfo.InvariantCulture, "Selection weight for {0}. 0 disables this entry.", candidate.TemplateName),
                    new AcceptableValueRange<int>(0, 1000),
                    new object[0]));

            List<WeightedSpawnOption> options;
            if (!_optionsByLayer.TryGetValue(layer, out options))
            {
                options = new List<WeightedSpawnOption>();
                _optionsByLayer[layer] = options;
            }

            options.Add(new WeightedSpawnOption(layer, candidate, weight));
        }

        private SpawnCandidate SelectCandidate(string layerName)
        {
            List<WeightedSpawnOption> layerOptions;
            if (!_optionsByLayer.TryGetValue(layerName, out layerOptions))
            {
                return null;
            }

            HashSet<string> rejectedThisAttempt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int attempts = 0; attempts < layerOptions.Count; attempts++)
            {
                List<SelectionEntry> entries = BuildSelectionEntries(layerOptions, rejectedThisAttempt);
                if (entries.Count == 0)
                {
                    return null;
                }

                SpawnCandidate selected = PickWeighted(entries);
                if (selected == null)
                {
                    return null;
                }

                if (CanResolveTemplate(selected))
                {
                    return selected;
                }

                rejectedThisAttempt.Add(selected.Id);
            }

            return null;
        }

        private List<SelectionEntry> BuildSelectionEntries(List<WeightedSpawnOption> options, HashSet<string> rejectedThisAttempt)
        {
            List<SelectionEntry> entries = new List<SelectionEntry>();
            bool hasRepeatAlternative = false;

            for (int i = 0; i < options.Count; i++)
            {
                WeightedSpawnOption option = options[i];
                if (!IsOptionCurrentlyAvailable(option, rejectedThisAttempt))
                {
                    continue;
                }

                int weight = option.Weight.Value;
                if (weight <= 0)
                {
                    continue;
                }

                if (!string.Equals(option.Candidate.Id, _lastCandidateId, StringComparison.OrdinalIgnoreCase))
                {
                    hasRepeatAlternative = true;
                }
            }

            for (int i = 0; i < options.Count; i++)
            {
                WeightedSpawnOption option = options[i];
                if (!IsOptionCurrentlyAvailable(option, rejectedThisAttempt))
                {
                    continue;
                }

                int adjustedWeight = option.Weight.Value;
                if (adjustedWeight <= 0)
                {
                    continue;
                }

                if (_avoidImmediateRepeats.Value &&
                    hasRepeatAlternative &&
                    string.Equals(option.Candidate.Id, _lastCandidateId, StringComparison.OrdinalIgnoreCase))
                {
                    adjustedWeight = (int)Math.Round(adjustedWeight * (_immediateRepeatWeightPercent.Value / 100.0));
                }

                if (adjustedWeight > 0)
                {
                    entries.Add(new SelectionEntry(option.Candidate, adjustedWeight));
                }
            }

            return entries;
        }

        private bool IsOptionCurrentlyAvailable(WeightedSpawnOption option, HashSet<string> rejectedThisAttempt)
        {
            if (option.Candidate.Hard && !_enableHardHunts.Value)
            {
                return false;
            }

            if (rejectedThisAttempt.Contains(option.Candidate.Id))
            {
                return false;
            }

            if (_disableFailedTemplatesForSession.Value && _failedTemplateIds.Contains(option.Candidate.Id))
            {
                return false;
            }

            return true;
        }

        private SpawnCandidate PickWeighted(List<SelectionEntry> entries)
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                total += entries[i].Weight;
            }

            if (total <= 0)
            {
                return null;
            }

            int roll;
            lock (_randomLock)
            {
                roll = _random.Next(total);
            }

            int cumulative = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                cumulative += entries[i].Weight;
                if (roll < cumulative)
                {
                    return entries[i].Candidate;
                }
            }

            return entries[entries.Count - 1].Candidate;
        }

        private bool CanResolveTemplate(SpawnCandidate candidate)
        {
            if (_validatedTemplateGuids.Contains(candidate.TemplateGuid))
            {
                return true;
            }

            try
            {
                LocationTemplate template = new TemplateReference(candidate.TemplateGuid).Get<LocationTemplate>(null);
                if (template == null)
                {
                    ReportTemplateFailure(candidate, "TemplateReference returned null.");
                    return false;
                }

                _validatedTemplateGuids.Add(candidate.TemplateGuid);
                return true;
            }
            catch (Exception ex)
            {
                ReportTemplateFailure(candidate, ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private void ReportTemplateFailure(SpawnCandidate candidate, string reason)
        {
            if (_disableFailedTemplatesForSession.Value)
            {
                _failedTemplateIds.Add(candidate.Id);
            }

            if (_logSelections.Value)
            {
                Log.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "Wyrd Hunt Addon randomizer rejected {0} [{1}]: {2}",
                    candidate.TemplateName,
                    candidate.TemplateGuid,
                    reason));
            }
        }

        private int RollExtraPackMateCount()
        {
            int threeChance = ClampPercent(_threeEnemyPackChancePercent.Value);
            int twoChance = ClampPercent(_twoEnemyPackChancePercent.Value);
            if (threeChance + twoChance > 100)
            {
                twoChance = Math.Max(0, 100 - threeChance);
            }

            int roll;
            lock (_randomLock)
            {
                roll = _random.Next(100);
            }

            if (roll < threeChance)
            {
                return 2;
            }

            if (roll < threeChance + twoChance)
            {
                return 1;
            }

            return 0;
        }

        private List<SpawnCandidate> SelectPackMates(SpawnCandidate primary, string layerName, int requestedCount)
        {
            List<SpawnCandidate> packMates = new List<SpawnCandidate>();
            HashSet<string> rejectedThisAttempt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedCandidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            usedCandidateIds.Add(primary.Id);

            for (int i = 0; i < requestedCount; i++)
            {
                SpawnCandidate selected = SelectPackMate(primary, layerName, rejectedThisAttempt, usedCandidateIds);
                if (selected == null)
                {
                    break;
                }

                packMates.Add(selected);
                usedCandidateIds.Add(selected.Id);
            }

            return packMates;
        }

        private SpawnCandidate SelectPackMate(
            SpawnCandidate primary,
            string layerName,
            HashSet<string> rejectedThisAttempt,
            HashSet<string> usedCandidateIds)
        {
            List<WeightedSpawnOption> layerOptions;
            if (!_optionsByLayer.TryGetValue(layerName, out layerOptions))
            {
                return null;
            }

            for (int attempts = 0; attempts < layerOptions.Count; attempts++)
            {
                List<SelectionEntry> entries = BuildPackMateSelectionEntries(layerOptions, primary, rejectedThisAttempt, usedCandidateIds);
                if (entries.Count == 0)
                {
                    return null;
                }

                SpawnCandidate selected = PickWeighted(entries);
                if (selected == null)
                {
                    return null;
                }

                if (CanResolveTemplate(selected))
                {
                    return selected;
                }

                rejectedThisAttempt.Add(selected.Id);
            }

            return null;
        }

        private List<SelectionEntry> BuildPackMateSelectionEntries(
            List<WeightedSpawnOption> options,
            SpawnCandidate primary,
            HashSet<string> rejectedThisAttempt,
            HashSet<string> usedCandidateIds)
        {
            List<SelectionEntry> entries = new List<SelectionEntry>();
            for (int i = 0; i < options.Count; i++)
            {
                WeightedSpawnOption option = options[i];
                SpawnCandidate candidate = option.Candidate;
                if (!IsOptionCurrentlyAvailable(option, rejectedThisAttempt) ||
                    usedCandidateIds.Contains(candidate.Id) ||
                    candidate.Tier > primary.Tier)
                {
                    continue;
                }

                if (candidate.Hard && !_allowHardPackMates.Value)
                {
                    continue;
                }

                int weight = option.Weight.Value;
                if (weight <= 0)
                {
                    continue;
                }

                AddPackMateSelectionEntry(entries, primary, candidate, weight);
            }

            AddWyrdspiritPackMateEntry(entries, primary, rejectedThisAttempt, usedCandidateIds);
            return entries;
        }

        private void AddWyrdspiritPackMateEntry(
            List<SelectionEntry> entries,
            SpawnCandidate primary,
            HashSet<string> rejectedThisAttempt,
            HashSet<string> usedCandidateIds)
        {
            if (_wyrdspiritSidecarWeight.Value <= 0 ||
                rejectedThisAttempt.Contains(WyrdspiritCandidateId) ||
                usedCandidateIds.Contains(WyrdspiritCandidateId) ||
                (_disableFailedTemplatesForSession.Value && _failedTemplateIds.Contains(WyrdspiritCandidateId)))
            {
                return;
            }

            SpawnCandidate candidate;
            if (!_candidatesById.TryGetValue(WyrdspiritCandidateId, out candidate) ||
                candidate.Tier > primary.Tier)
            {
                return;
            }

            AddPackMateSelectionEntry(entries, primary, candidate, _wyrdspiritSidecarWeight.Value);
        }

        private void AddPackMateSelectionEntry(
            List<SelectionEntry> entries,
            SpawnCandidate primary,
            SpawnCandidate candidate,
            int baseWeight)
        {
            int adjustedWeight = GetPackMateAdjustedWeight(primary, candidate, baseWeight);
            if (adjustedWeight > 0)
            {
                entries.Add(new SelectionEntry(candidate, adjustedWeight));
            }
        }

        private int GetPackMateAdjustedWeight(SpawnCandidate primary, SpawnCandidate candidate, int baseWeight)
        {
            if (baseWeight <= 0)
            {
                return 0;
            }

            int tierGap = Math.Max(0, primary.Tier - candidate.Tier);
            int tierPercent = tierGap == 0
                ? ClampPercent(_sameTierPackMateWeightPercent.Value)
                : 100 + Math.Min(100, tierGap * 25);

            if (SameCandidateFamily(primary, candidate))
            {
                tierPercent = (int)Math.Round(tierPercent * (ClampPercent(_sameFamilyPackMateWeightPercent.Value) / 100.0));
            }

            return (int)Math.Round(baseWeight * (tierPercent / 100.0));
        }

        private int RollPackMateSpawnCount(SpawnCandidate candidate)
        {
            if (!IsWyrdspirit(candidate))
            {
                return 1;
            }

            int maxCount = Math.Max(1, Math.Min(4, _wyrdspiritMaxPackCount.Value));
            lock (_randomLock)
            {
                return _random.Next(1, maxCount + 1);
            }
        }

        private static string FormatPackMateSummary(SpawnCandidate candidate, int count)
        {
            if (count <= 1)
            {
                return candidate.TemplateName;
            }

            return candidate.TemplateName + " x" + count.ToString(CultureInfo.InvariantCulture);
        }

        private bool TrySpawnPackMate(SpawnCandidate candidate, int baseDistanceMeters)
        {
            LocationTemplate template;
            try
            {
                template = new TemplateReference(candidate.TemplateGuid).Get<LocationTemplate>(null);
            }
            catch (Exception ex)
            {
                ReportTemplateFailure(candidate, "Sidecar template resolve failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }

            if (template == null)
            {
                ReportTemplateFailure(candidate, "Sidecar TemplateReference returned null.");
                return false;
            }

            int clampedDistance = Mathf.Clamp(baseDistanceMeters, 20, 60);
            int jitterMeters = _packMateSpawnDistanceJitterMeters.Value;
            Func<float> distanceFunc = delegate
            {
                if (jitterMeters <= 0)
                {
                    return clampedDistance;
                }

                int jitter;
                lock (_randomLock)
                {
                    jitter = _random.Next(-jitterMeters, jitterMeters + 1);
                }

                return Mathf.Clamp(clampedDistance + jitter, 20, 60);
            };

            try
            {
                LocationSpawnUtils.SpawnEnemiesAroundHero(1, distanceFunc, template);
                return true;
            }
            catch (Exception ex)
            {
                ReportTemplateFailure(candidate, "Sidecar spawn failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private object CreateCustomProfile(SpawnCandidate candidate, object originalProfile, string layerName)
        {
            EnsureProfileReflection();

            object layer = GetPropertyValue(originalProfile, "Layer");
            object minStage = GetPropertyValue(originalProfile, "MinimumStage");
            object maxStage = GetPropertyValue(originalProfile, "MaximumStage");
            object executionMode = Enum.Parse(_executionModeType, "NativeCombatAmbush");
            object noneTags = Enum.Parse(_conditionTagType, "None");

            string profileId = BuildAddonProfileId(layerName, candidate.Id);
            string cooldownTag = GetCooldownTag(layerName);
            int cooldownSeconds = GetIntProperty(originalProfile, "LayerCooldownSeconds", GetDefaultCooldownSeconds(layerName));
            int priority = GetIntProperty(originalProfile, "Priority", 104);
            int selectionWeight = GetIntProperty(originalProfile, "SelectionWeight", candidate.Tier + 100);
            float minThreat = GetFloatProperty(originalProfile, "MinimumThreat", 0f);
            float maxThreat = GetFloatProperty(originalProfile, "MaximumThreat", 100f);

            object[] args = new object[]
            {
                profileId,
                candidate.TemplateGuid,
                candidate.TemplateName,
                layer,
                executionMode,
                candidate.Tier,
                minStage,
                maxStage,
                minThreat,
                maxThreat,
                1,
                1,
                cooldownSeconds,
                priority,
                selectionWeight,
                cooldownTag,
                noneTags,
                noneTags,
                0,
                candidate.Hard ? "addon-random-hard-whitelist" : "addon-random-whitelist",
                string.Empty
            };

            try
            {
                return _profileConstructor.Invoke(args);
            }
            catch (Exception ex)
            {
                Log.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "Wyrd Hunt Addon could not create random profile {0}: {1}",
                    profileId,
                    ex));
                return null;
            }
        }

        private void EnsureProfileReflection()
        {
            if (_profileConstructor != null)
            {
                return;
            }

            _profileType = AccessTools.TypeByName("WyrdHunt.HuntProfile");
            _executionModeType = AccessTools.TypeByName("WyrdHunt.HuntExecutionMode");
            _conditionTagType = AccessTools.TypeByName("WyrdHunt.HuntConditionTag");

            if (_profileType == null || _executionModeType == null || _conditionTagType == null)
            {
                throw new InvalidOperationException("Could not resolve Wyrd Hunt profile reflection types.");
            }

            ConstructorInfo[] constructors = _profileType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < constructors.Length; i++)
            {
                if (constructors[i].GetParameters().Length == 21)
                {
                    _profileConstructor = constructors[i];
                    break;
                }
            }

            if (_profileConstructor == null)
            {
                throw new InvalidOperationException("Could not resolve Wyrd Hunt HuntProfile constructor.");
            }
        }

        private void LogSelection(string originalProfileId, SpawnCandidate selected, string layerName, bool replacedProfile)
        {
            if (!_logSelections.Value)
            {
                return;
            }

            Log.LogInfo(string.Format(
                CultureInfo.InvariantCulture,
                "Wyrd Hunt Addon randomizer {0} {1} with {2} ({3}) for {4}.",
                replacedProfile ? "replaced" : "kept",
                originalProfileId,
                selected.Id,
                selected.TemplateName,
                layerName));
        }

        private static bool IsAutoProfile(string configuredProfileId)
        {
            return string.IsNullOrWhiteSpace(configuredProfileId) ||
                string.Equals(configuredProfileId.Trim(), "auto", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameTemplate(SpawnCandidate candidate, string templateGuid, string templateName)
        {
            return string.Equals(candidate.TemplateGuid, templateGuid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.TemplateName, templateName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWyrdspirit(SpawnCandidate candidate)
        {
            return candidate != null &&
                string.Equals(candidate.Id, WyrdspiritCandidateId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameCandidateFamily(SpawnCandidate first, SpawnCandidate second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return string.Equals(GetCandidateFamily(first.Id), GetCandidateFamily(second.Id), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCandidateFamily(string candidateId)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                return string.Empty;
            }

            if (candidateId.StartsWith("wyrdspawn", StringComparison.OrdinalIgnoreCase))
            {
                return "wyrdspawn";
            }

            if (candidateId.StartsWith("mistling", StringComparison.OrdinalIgnoreCase))
            {
                return "mistling";
            }

            return candidateId;
        }

        private static bool AllowsMixedHuntPackLayer(string layerName)
        {
            return string.Equals(layerName, "Stalking", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layerName, "Hunter", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layerName, "Pursuit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layerName, "Marked", StringComparison.OrdinalIgnoreCase);
        }

        private static int ClampPercent(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        private static string AppendReason(string originalReason, string addonReason)
        {
            if (string.IsNullOrWhiteSpace(originalReason))
            {
                return addonReason;
            }

            return originalReason + " " + addonReason;
        }

        private static string BuildAddonProfileId(string layerName, string candidateId)
        {
            string normalizedLayer = string.IsNullOrWhiteSpace(layerName)
                ? "unknown"
                : layerName.Trim().ToLowerInvariant();

            return AddonProfilePrefix + normalizedLayer + "-" + candidateId;
        }

        private static bool TryParseAddonProfileId(string profileId, out string candidateId)
        {
            candidateId = null;
            if (string.IsNullOrWhiteSpace(profileId) ||
                !profileId.StartsWith(AddonProfilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string suffix = profileId.Substring(AddonProfilePrefix.Length);
            int separator = suffix.IndexOf('-');
            if (separator < 0 || separator == suffix.Length - 1)
            {
                return false;
            }

            candidateId = suffix.Substring(separator + 1);
            return true;
        }

        private static string GetCooldownTag(string layerName)
        {
            if (string.Equals(layerName, "Contact", StringComparison.OrdinalIgnoreCase))
            {
                return "layer-1";
            }

            if (string.Equals(layerName, "Stalking", StringComparison.OrdinalIgnoreCase))
            {
                return "layer-2";
            }

            if (string.Equals(layerName, "Hunter", StringComparison.OrdinalIgnoreCase))
            {
                return "layer-3";
            }

            if (string.Equals(layerName, "Pursuit", StringComparison.OrdinalIgnoreCase))
            {
                return "layer-4";
            }

            if (string.Equals(layerName, "Marked", StringComparison.OrdinalIgnoreCase))
            {
                return "layer-5";
            }

            return "layer-random";
        }

        private static int GetDefaultCooldownSeconds(string layerName)
        {
            if (string.Equals(layerName, "Contact", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (string.Equals(layerName, "Stalking", StringComparison.OrdinalIgnoreCase))
            {
                return 120;
            }

            if (string.Equals(layerName, "Hunter", StringComparison.OrdinalIgnoreCase))
            {
                return 150;
            }

            if (string.Equals(layerName, "Pursuit", StringComparison.OrdinalIgnoreCase))
            {
                return 180;
            }

            if (string.Equals(layerName, "Marked", StringComparison.OrdinalIgnoreCase))
            {
                return 300;
            }

            return 120;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            if (instance == null)
            {
                return null;
            }

            PropertyInfo property = AccessTools.Property(instance.GetType(), propertyName);
            return property == null ? null : property.GetValue(instance, null);
        }

        private static string GetPropertyString(object instance, string propertyName)
        {
            object value = GetPropertyValue(instance, propertyName);
            return value == null ? string.Empty : value.ToString();
        }

        private static string GetStringProperty(object instance, string propertyName, string fallback)
        {
            object value = GetPropertyValue(instance, propertyName);
            if (value == null)
            {
                return fallback;
            }

            string text = value as string;
            return text == null ? value.ToString() : text;
        }

        private static int GetIntProperty(object instance, string propertyName, int fallback)
        {
            object value = GetPropertyValue(instance, propertyName);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static float GetFloatProperty(object instance, string propertyName, float fallback)
        {
            object value = GetPropertyValue(instance, propertyName);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private sealed class WyrdHuntStatus
        {
            internal readonly string StageName;
            internal readonly int ScentValue;
            internal readonly bool? InWyrdness;

            internal WyrdHuntStatus(string stageName, int scentValue, bool? inWyrdness)
            {
                StageName = stageName ?? string.Empty;
                ScentValue = scentValue;
                InWyrdness = inWyrdness;
            }
        }

        private sealed class HuntTuningPresetValues
        {
            internal readonly int HunterPackThreshold;
            internal readonly float BossThreshold;
            internal readonly int MinSecondsBetweenHunts;
            internal readonly int MaxHuntersAlive;
            internal readonly bool AllowBossHunter;
            internal readonly int HunterSpawnDistanceMeters;
            internal readonly int ActiveEncounterLockSeconds;
            internal readonly int ActiveEncounterTimeoutSeconds;
            internal readonly int MaxHuntsPerScene;
            internal readonly int MaxHuntsPerSession;
            internal readonly int GuardAwarenessMaxGuardsPerEncounter;
            internal readonly float CustomEnemyAiRadiusMeters;
            internal readonly float CustomEnemyAiScanIntervalSeconds;
            internal readonly bool EnableShargPursuitMovement;
            internal readonly float ThreatGainPerMinute;
            internal readonly float ThreatGainWhileSprintingPerMinute;
            internal readonly float ThreatGainOnCombatAction;
            internal readonly float ThreatGainOnKill;
            internal readonly float ThreatGainOnLoot;
            internal readonly float ThreatDecayNearSafeLight;
            internal readonly float CombatThreatCooldownSeconds;

            internal HuntTuningPresetValues(
                int hunterPackThreshold,
                float bossThreshold,
                int minSecondsBetweenHunts,
                int maxHuntersAlive,
                bool allowBossHunter,
                int hunterSpawnDistanceMeters,
                int activeEncounterLockSeconds,
                int activeEncounterTimeoutSeconds,
                int maxHuntsPerScene,
                int maxHuntsPerSession,
                int guardAwarenessMaxGuardsPerEncounter,
                float customEnemyAiRadiusMeters,
                float customEnemyAiScanIntervalSeconds,
                bool enableShargPursuitMovement,
                float threatGainPerMinute,
                float threatGainWhileSprintingPerMinute,
                float threatGainOnCombatAction,
                float threatGainOnKill,
                float threatGainOnLoot,
                float threatDecayNearSafeLight,
                float combatThreatCooldownSeconds)
            {
                HunterPackThreshold = hunterPackThreshold;
                BossThreshold = bossThreshold;
                MinSecondsBetweenHunts = minSecondsBetweenHunts;
                MaxHuntersAlive = maxHuntersAlive;
                AllowBossHunter = allowBossHunter;
                HunterSpawnDistanceMeters = hunterSpawnDistanceMeters;
                ActiveEncounterLockSeconds = activeEncounterLockSeconds;
                ActiveEncounterTimeoutSeconds = activeEncounterTimeoutSeconds;
                MaxHuntsPerScene = maxHuntsPerScene;
                MaxHuntsPerSession = maxHuntsPerSession;
                GuardAwarenessMaxGuardsPerEncounter = guardAwarenessMaxGuardsPerEncounter;
                CustomEnemyAiRadiusMeters = customEnemyAiRadiusMeters;
                CustomEnemyAiScanIntervalSeconds = customEnemyAiScanIntervalSeconds;
                EnableShargPursuitMovement = enableShargPursuitMovement;
                ThreatGainPerMinute = threatGainPerMinute;
                ThreatGainWhileSprintingPerMinute = threatGainWhileSprintingPerMinute;
                ThreatGainOnCombatAction = threatGainOnCombatAction;
                ThreatGainOnKill = threatGainOnKill;
                ThreatGainOnLoot = threatGainOnLoot;
                ThreatDecayNearSafeLight = threatDecayNearSafeLight;
                CombatThreatCooldownSeconds = combatThreatCooldownSeconds;
            }
        }

        private sealed class SpawnCandidate
        {
            internal readonly string Id;
            internal readonly string TemplateGuid;
            internal readonly string TemplateName;
            internal readonly int Tier;
            internal readonly bool Hard;

            internal SpawnCandidate(string id, string templateGuid, string templateName, int tier, bool hard)
            {
                Id = id;
                TemplateGuid = templateGuid;
                TemplateName = templateName;
                Tier = tier;
                Hard = hard;
            }
        }

        private sealed class WeightedSpawnOption
        {
            internal readonly string Layer;
            internal readonly SpawnCandidate Candidate;
            internal readonly ConfigEntry<int> Weight;

            internal WeightedSpawnOption(string layer, SpawnCandidate candidate, ConfigEntry<int> weight)
            {
                Layer = layer;
                Candidate = candidate;
                Weight = weight;
            }
        }

        private sealed class SelectionEntry
        {
            internal readonly SpawnCandidate Candidate;
            internal readonly int Weight;

            internal SelectionEntry(SpawnCandidate candidate, int weight)
            {
                Candidate = candidate;
                Weight = weight;
            }
        }
    }

    internal static class ThreatMeterTransitionHideTracker
    {
        private const float SceneTransitionHideSeconds = 1.5f;
        private const float LoadingUiHideSeconds = 2.5f;

        private static float _hideUntilRealtime;
        private static bool _initialized;

        internal static void Initialize()
        {
            if (_initialized || !WyrdHuntAddonPlugin.ShouldUseLoadingScreenMeterHide())
            {
                return;
            }

            _initialized = true;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            MarkSceneTransition();
        }

        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _initialized = false;
            _hideUntilRealtime = 0f;
        }

        internal static void Refresh()
        {
            if (WyrdHuntAddonPlugin.ShouldUseLoadingScreenMeterHide())
            {
                Initialize();
            }
            else
            {
                Shutdown();
            }
        }

        internal static bool IsTransitionHideActive()
        {
            return Time.realtimeSinceStartup < _hideUntilRealtime;
        }

        internal static void MarkLoadingUiActivity()
        {
            ExtendHideWindow(LoadingUiHideSeconds);
        }

        private static void MarkSceneTransition()
        {
            ExtendHideWindow(SceneTransitionHideSeconds);
        }

        private static void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            MarkSceneTransition();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MarkSceneTransition();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            MarkSceneTransition();
        }

        private static void ExtendHideWindow(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float until = Time.realtimeSinceStartup + seconds;
            if (until > _hideUntilRealtime)
            {
                _hideUntilRealtime = until;
            }
        }
    }

    internal static class LoadingUiTransitionPatch
    {
        private static readonly string[] TargetTypeNames =
        {
            "Awaken.TG.Main.UI.TitleScreen.Loading.LoadingScreenUI",
            "Awaken.TG.Main.UI.TitleScreen.Loading.VLoadingScreenUI",
            "Awaken.TG.Main.UI.TitleScreen.Loading.VLoadingBarOverlayUI",
            "Awaken.TG.Main.UI.TitleScreen.Loading.VLoadingWheelOverlayUI",
            "Awaken.TG.Main.UI.TitleScreen.TitleScreenUI",
            "Awaken.TG.Main.UI.TitleScreen.VTitleScreenUI"
        };

        private static readonly string[] TargetMethodNames =
        {
            "OnInitialize",
            "OnFullyInitialized",
            "OnDiscard",
            "OnShow",
            "OnHide",
            "OnOpen",
            "OnClose",
            "OnEnable",
            "OnDisable",
            "Show",
            "Hide",
            "Open",
            "Close",
            "StartLoading",
            "BeginLoading",
            "FinishLoading",
            "CompleteLoading",
            "EndLoading"
        };

        internal static void TryPatch(Harmony harmony)
        {
            if (harmony == null || !WyrdHuntAddonPlugin.ShouldUseLoadingScreenMeterHide())
            {
                return;
            }

            MethodInfo prefix = AccessTools.Method(typeof(LoadingUiTransitionPatch), "MarkLoadingActivity");
            if (prefix == null)
            {
                return;
            }

            int patched = 0;
            try
            {
                foreach (MethodBase method in TargetMethods())
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                    patched++;
                }
            }
            catch (Exception exception)
            {
                WyrdHuntAddonPlugin.Log.LogWarning("Could not patch optional loading UI scent-meter hide hooks: " + exception.GetBaseException().Message);
            }

            if (patched == 0)
            {
                WyrdHuntAddonPlugin.Log.LogWarning("Could not find optional loading UI methods for scent-meter hide hooks; scene-transition hiding remains active.");
            }
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            HashSet<MethodBase> methods = new HashSet<MethodBase>();
            for (int i = 0; i < TargetTypeNames.Length; i++)
            {
                Type type = AccessTools.TypeByName(TargetTypeNames[i]);
                if (type == null)
                {
                    continue;
                }

                foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
                {
                    if (method == null ||
                        method.IsAbstract ||
                        method.IsSpecialName ||
                        method.ContainsGenericParameters ||
                        !IsTargetMethodName(method.Name) ||
                        !methods.Add(method))
                    {
                        continue;
                    }

                    yield return method;
                }
            }
        }

        private static bool IsTargetMethodName(string methodName)
        {
            for (int i = 0; i < TargetMethodNames.Length; i++)
            {
                if (string.Equals(methodName, TargetMethodNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return methodName.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0 &&
                methodName.IndexOf("Update", StringComparison.OrdinalIgnoreCase) < 0;
        }

        [HarmonyPrefix]
        private static void MarkLoadingActivity()
        {
            if (WyrdHuntAddonPlugin.ShouldUseLoadingScreenMeterHide())
            {
                ThreatMeterTransitionHideTracker.MarkLoadingUiActivity();
            }
        }
    }

    [HarmonyPatch]
    internal static class ThreatMeterStatusPatch
    {
        private static MethodBase TargetMethod()
        {
            Type managerType = AccessTools.TypeByName("WyrdHunt.WyrdHuntManager");
            return managerType == null ? null : AccessTools.Method(managerType, "UpdateThreatMeterVisibilityPulse");
        }

        [HarmonyPostfix]
        private static void ObserveStatus(object __instance)
        {
            WyrdHuntAddonPlugin instance = WyrdHuntAddonPlugin.Instance;
            if (instance != null)
            {
                instance.ObserveWyrdHuntStatus(__instance);
            }
        }
    }

    [HarmonyPatch]
    internal static class ThreatMeterVisibilityPatch
    {
        private static FieldInfo _lastInWyrdnessField;
        private static PropertyInfo _heroCurrentProperty;
        private static bool _missingFieldReported;
        private static bool _missingHeroCurrentReported;
        private static bool _heroCurrentReadFailedReported;

        private static MethodBase TargetMethod()
        {
            Type managerType = AccessTools.TypeByName("WyrdHunt.WyrdHuntManager");
            return managerType == null ? null : AccessTools.Method(managerType, "ShouldDrawThreatMeter");
        }

        [HarmonyPostfix]
        private static void HideWhileSafe(object __instance, ref bool __result)
        {
            WyrdHuntAddonPlugin instance = WyrdHuntAddonPlugin.Instance;
            if (instance != null)
            {
                instance.ObserveWyrdHuntStatus(__instance);
            }

            if (WyrdHuntAddonPlugin.ShouldSuppressScentMeter())
            {
                __result = false;
                return;
            }

            if (!__result)
            {
                return;
            }

            if (WyrdHuntAddonPlugin.ShouldUseLoadingScreenMeterHide() &&
                ShouldHideForLoadingOrTitle())
            {
                __result = false;
                return;
            }

            if (!WyrdHuntAddonPlugin.ShouldUseSafeMeterHide())
            {
                return;
            }

            if (_lastInWyrdnessField == null)
            {
                _lastInWyrdnessField = AccessTools.Field(__instance.GetType(), "_lastInWyrdness");
            }

            if (_lastInWyrdnessField == null)
            {
                if (!_missingFieldReported)
                {
                    _missingFieldReported = true;
                    WyrdHuntAddonPlugin.Log.LogWarning("Could not find Wyrd Hunt's exposure-state field; safe-state hiding is disabled.");
                }

                return;
            }

            object value = _lastInWyrdnessField.GetValue(__instance);
            if (value is bool && !(bool)value)
            {
                __result = false;
            }
        }

        private static bool ShouldHideForLoadingOrTitle()
        {
            if (ThreatMeterTransitionHideTracker.IsTransitionHideActive())
            {
                return true;
            }

            if (HeroCurrentUnavailable())
            {
                return true;
            }

            string sceneName;
            try
            {
                sceneName = SceneManager.GetActiveScene().name;
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return true;
            }

            return string.Equals(sceneName, "BuildInitialScene", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sceneName, "TitleScreen", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sceneName, "ApplicationScene", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sceneName, "Scene/", StringComparison.OrdinalIgnoreCase) ||
                sceneName.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Startup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HeroCurrentUnavailable()
        {
            if (_heroCurrentProperty == null)
            {
                Type heroType = AccessTools.TypeByName("Awaken.TG.Main.Heroes.Hero");
                _heroCurrentProperty = heroType == null ? null : AccessTools.Property(heroType, "Current");
            }

            if (_heroCurrentProperty == null)
            {
                if (!_missingHeroCurrentReported)
                {
                    _missingHeroCurrentReported = true;
                    WyrdHuntAddonPlugin.Log.LogWarning("Could not find Hero.Current; loading-screen Wyrd Scent hiding will rely on scene names only.");
                }

                return false;
            }

            try
            {
                return _heroCurrentProperty.GetValue(null, null) == null;
            }
            catch (Exception)
            {
                if (!_heroCurrentReadFailedReported)
                {
                    _heroCurrentReadFailedReported = true;
                    WyrdHuntAddonPlugin.Log.LogWarning("Could not read Hero.Current; loading-screen Wyrd Scent hiding will rely on scene names only.");
                }

                return false;
            }
        }
    }

    [HarmonyPatch]
    internal static class ThreatMeterPositionPatch
    {
        private static MethodBase TargetMethod()
        {
            Type managerType = AccessTools.TypeByName("WyrdHunt.WyrdHuntManager");
            return managerType == null ? null : AccessTools.Method(managerType, "OnGUI");
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> RepositionMeter(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getMeterX = AccessTools.Method(typeof(WyrdHuntAddonPlugin), "GetMeterX");
            MethodInfo getMeterY = AccessTools.Method(typeof(WyrdHuntAddonPlugin), "GetMeterY");
            int xReplacements = 0;
            int yReplacements = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float)
                {
                    float value = (float)instruction.operand;
                    if (xReplacements == 0 && Math.Abs(value - 26f) < 0.001f)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = getMeterX;
                        xReplacements++;
                    }
                    else if (yReplacements == 0 && Math.Abs(value - 84f) < 0.001f)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = getMeterY;
                        yReplacements++;
                    }
                }

                yield return instruction;
            }

            if (xReplacements != 1 || yReplacements != 1)
            {
                WyrdHuntAddonPlugin.Log.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "Wyrd Hunt's HUD method has changed; replaced {0} horizontal and {1} vertical position constants.",
                    xReplacements,
                    yReplacements));
            }
        }
    }

    internal static class WyrdHuntAddonOptionalPatches
    {
        internal static void TryPatch(Harmony harmony)
        {
            if (harmony == null)
            {
                return;
            }

            int patched = 0;
            const int total = 5;
            if (HuntProfileSelectionPatch.TryPatch(harmony))
            {
                patched++;
            }

            if (HuntExecutionPolicyPatch.TryPatch(harmony))
            {
                patched++;
            }

            if (HunterSpawnerMixedPackPatch.TryPatch(harmony))
            {
                patched++;
            }

            if (HuntRoleSummaryPatch.TryPatch(harmony))
            {
                patched++;
            }

            if (HuntPressureIntentPatch.TryPatch(harmony))
            {
                patched++;
            }

            WyrdHuntAddonPlugin.Log.LogInfo(string.Format(
                CultureInfo.InvariantCulture,
                "Wyrd Hunt Addon optional hunt patches active: {0}/{1}.",
                patched,
                total));
        }

        internal static bool TryPatchPostfix(Harmony harmony, string label, MethodBase target, MethodInfo postfix)
        {
            if (harmony == null)
            {
                return false;
            }

            if (target == null)
            {
                WyrdHuntAddonPlugin.Log.LogWarning("Optional Wyrd Hunt Addon patch skipped; target method not found: " + label);
                return false;
            }

            if (postfix == null)
            {
                WyrdHuntAddonPlugin.Log.LogWarning("Optional Wyrd Hunt Addon patch skipped; postfix method not found: " + label);
                return false;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                return true;
            }
            catch (Exception exception)
            {
                WyrdHuntAddonPlugin.Log.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "Optional Wyrd Hunt Addon patch failed: {0}: {1}",
                    label,
                    exception.GetBaseException().Message));
                return false;
            }
        }
    }

    internal static class HuntProfileSelectionPatch
    {
        internal static bool TryPatch(Harmony harmony)
        {
            MethodInfo postfix = AccessTools.Method(typeof(HuntProfileSelectionPatch), "RandomizeSelection");
            return WyrdHuntAddonOptionalPatches.TryPatchPostfix(
                harmony,
                "profile selection randomizer",
                TargetMethod(),
                postfix);
        }

        private static MethodBase TargetMethod()
        {
            Type catalogType = AccessTools.TypeByName("WyrdHunt.HuntProfileCatalog");
            return catalogType == null ? null : AccessTools.Method(catalogType, "TrySelectProfile");
        }

        [HarmonyPostfix]
        private static void RandomizeSelection(ref bool __result, object[] __args)
        {
            WyrdHuntAddonPlugin instance = WyrdHuntAddonPlugin.Instance;
            if (instance == null)
            {
                return;
            }

            try
            {
                instance.TryRandomizeSelection(ref __result, __args);
            }
            catch (Exception ex)
            {
                WyrdHuntAddonPlugin.Log.LogWarning("Wyrd Hunt Addon randomizer failed during profile selection: " + ex);
            }
        }
    }

    internal static class HuntExecutionPolicyPatch
    {
        internal static bool TryPatch(Harmony harmony)
        {
            MethodInfo postfix = AccessTools.Method(typeof(HuntExecutionPolicyPatch), "ApproveAddonProfiles");
            return WyrdHuntAddonOptionalPatches.TryPatchPostfix(
                harmony,
                "custom execution-policy approval",
                TargetMethod(),
                postfix);
        }

        private static MethodBase TargetMethod()
        {
            Type policyType = AccessTools.TypeByName("WyrdHunt.HuntExecutionPolicy");
            return policyType == null ? null : AccessTools.Method(policyType, "IsApprovedCuratedNativePlan");
        }

        [HarmonyPostfix]
        private static void ApproveAddonProfiles(ref bool __result, object[] __args)
        {
            if (__result || __args == null || __args.Length == 0 || WyrdHuntAddonPlugin.Instance == null)
            {
                return;
            }

            try
            {
                if (WyrdHuntAddonPlugin.Instance.IsApprovedCustomPlan(__args[0]))
                {
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                WyrdHuntAddonPlugin.Log.LogWarning("Wyrd Hunt Addon randomizer failed during execution-policy approval: " + ex);
            }
        }
    }

    internal static class HunterSpawnerMixedPackPatch
    {
        internal static bool TryPatch(Harmony harmony)
        {
            MethodInfo postfix = AccessTools.Method(typeof(HunterSpawnerMixedPackPatch), "SpawnMixedPackMates");
            return WyrdHuntAddonOptionalPatches.TryPatchPostfix(
                harmony,
                "mixed-pack spawn companions",
                TargetMethod(),
                postfix);
        }

        private static MethodBase TargetMethod()
        {
            Type spawnerType = AccessTools.TypeByName("WyrdHunt.HunterSpawner");
            return spawnerType == null ? null : AccessTools.Method(spawnerType, "TryRequestHunterPack");
        }

        [HarmonyPostfix]
        private static void SpawnMixedPackMates(bool __result, object __0, ref string __1)
        {
            if (!__result)
            {
                return;
            }

            WyrdHuntAddonPlugin instance = WyrdHuntAddonPlugin.Instance;
            if (instance == null)
            {
                return;
            }

            try
            {
                instance.TrySpawnMixedPackMates(__0, ref __1);
            }
            catch (Exception ex)
            {
                WyrdHuntAddonPlugin.Log.LogWarning("Wyrd Hunt Addon randomizer failed during mixed-pack spawn: " + ex);
            }
        }
    }

    internal static class HuntRoleSummaryPatch
    {
        internal static bool TryPatch(Harmony harmony)
        {
            MethodInfo postfix = AccessTools.Method(typeof(HuntRoleSummaryPatch), "DescribeRole");
            return WyrdHuntAddonOptionalPatches.TryPatchPostfix(
                harmony,
                "custom hunt role summary",
                TargetMethod(),
                postfix);
        }

        private static MethodBase TargetMethod()
        {
            Type catalogType = AccessTools.TypeByName("WyrdHunt.HuntProfileCatalog");
            return catalogType == null ? null : AccessTools.Method(catalogType, "GetRoleSummary");
        }

        [HarmonyPostfix]
        private static void DescribeRole(string profileId, ref string __result)
        {
            WyrdHuntAddonPlugin instance = WyrdHuntAddonPlugin.Instance;
            if (instance == null)
            {
                return;
            }

            string role;
            string pressure;
            if (instance.TryDescribeAddonProfile(profileId, out role, out pressure))
            {
                __result = role;
            }
        }
    }

    internal static class HuntPressureIntentPatch
    {
        internal static bool TryPatch(Harmony harmony)
        {
            MethodInfo postfix = AccessTools.Method(typeof(HuntPressureIntentPatch), "DescribePressure");
            return WyrdHuntAddonOptionalPatches.TryPatchPostfix(
                harmony,
                "custom hunt pressure intent",
                TargetMethod(),
                postfix);
        }

        private static MethodBase TargetMethod()
        {
            Type catalogType = AccessTools.TypeByName("WyrdHunt.HuntProfileCatalog");
            return catalogType == null ? null : AccessTools.Method(catalogType, "GetPressureIntent");
        }

        [HarmonyPostfix]
        private static void DescribePressure(string profileId, ref string __result)
        {
            WyrdHuntAddonPlugin instance = WyrdHuntAddonPlugin.Instance;
            if (instance == null)
            {
                return;
            }

            string role;
            string pressure;
            if (instance.TryDescribeAddonProfile(profileId, out role, out pressure))
            {
                __result = pressure;
            }
        }
    }
}
