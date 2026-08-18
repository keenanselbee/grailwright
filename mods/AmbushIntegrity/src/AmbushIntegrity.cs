using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.MVC;
using Awaken.TG.Main.AI;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Attachments;
using Awaken.TG.Main.Heroes.Setup;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Ambush Integrity")]
[assembly: AssemblyDescription("Experimental stealth consistency overhaul for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Ambush Integrity")]
[assembly: AssemblyVersion("0.1.8.0")]
[assembly: AssemblyFileVersion("0.1.8.0")]
[assembly: AssemblyInformationalVersion("0.1.8")]

namespace AmbushIntegrity
{
    public enum BackstabOpportunityState
    {
        None = 0,
        Ready = 1
    }

    public static class AmbushIntegrityApi
    {
        public const int ApiVersion = 1;

        public static int GetBackstabOpportunityState()
        {
            AmbushIntegrityPlugin plugin = AmbushIntegrityPlugin.Instance;
            return plugin == null
                ? (int)BackstabOpportunityState.None
                : plugin.ReadBackstabOpportunityState();
        }
    }

    public enum NotificationDetail
    {
        Essential,
        Detailed
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AmbushIntegrityPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.ambush-integrity";
        public const string PluginName = "Ambush Integrity";
        public const string PluginVersion = "0.1.8";
        private const string SteelAndBonePluginGuid = "ks.tgfoa.steel-and-bone";
        private const int ConfigSchemaVersion = 3;
        private const int ConfigRecoveryBaselineSchema = 1;
        private const float AwarenessPollSeconds = 0.15f;
        private const float BackstabApiFreshnessSeconds = 0.2f;
        private const float FootstepDiagnosticIntervalSeconds = 2.0f;

        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        internal static AmbushIntegrityPlugin Instance;

        private readonly Dictionary<Damage, PendingResult> _pendingResults =
            new Dictionary<Damage, PendingResult>();
        private Harmony _harmony;
        private GrailFloatingTextBridge _gft;
        private Grailwright.Shared.ConfigRecoveryCustomizationProfile
            _pendingConfigRecoveryProfile;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _committedAmbushEnabled;
        private ConfigEntry<float> _committedAmbushSeconds;
        private ConfigEntry<float> _backstabRangeMultiplier;
        private ConfigEntry<float> _backstabAvailabilityGraceSeconds;
        private ConfigEntry<bool> _footstepAwarenessEnabled;
        private ConfigEntry<bool> _cleanExecutionsEnabled;
        private ConfigEntry<float> _cleanExecutionWitnessRange;
        private ConfigEntry<bool> _gftNotificationsEnabled;
        private ConfigEntry<NotificationDetail> _notificationDetail;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _showGrailFloatingTextDiagnostics;

        private NpcElement _committedTarget;
        private float _committedUntil;
        private NpcElement _backstabReadyTarget;
        private float _backstabReadyUntil;
        private float _nextAwarenessPoll;
        private AwarenessState _awarenessState;
        private bool _awarenessInitialized;
        private bool _backstabRangeDiagnosticInitialized;
        private float _lastBackstabRangeNativeSquared;
        private float _lastBackstabRangeMultiplier;
        private bool _backstabApiDiagnosticInitialized;
        private BackstabOpportunityState _lastBackstabApiDiagnosticState;
        private NpcElement _lastBackstabApiDiagnosticTarget;
        private bool _promptGraceDiagnosticActive;
        private bool _footstepDiagnosticInitialized;
        private bool _lastFootstepDiagnosticWasCrouching;
        private ItemWeight _lastFootstepDiagnosticArmorTier;
        private float _nextFootstepDiagnosticAt;

        private void Awake()
        {
            Instance = this;
            try
            {
                BindConfig();
                _gft = new GrailFloatingTextBridge(Logger, PluginGuid);
                if (_diagnostics.Value)
                {
                    LogDiagnostic(
                        "Startup settings: rangeMultiplier="
                        + _backstabRangeMultiplier.Value.ToString("0.###", CultureInfo.InvariantCulture)
                        + "; committedSeconds="
                        + _committedAmbushSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)
                        + "; promptGraceSeconds="
                        + _backstabAvailabilityGraceSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)
                        + "; footstepAwareness="
                        + _footstepAwarenessEnabled.Value.ToString(CultureInfo.InvariantCulture)
                        + "; witnessRangeMeters="
                        + _cleanExecutionWitnessRange.Value.ToString("0.###", CultureInfo.InvariantCulture)
                        + "; GrailFloatingText="
                        + (_gft.IsAvailable() ? "available" : "unavailable")
                        + ".");
                }
                PatchGame();
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded in experimental test mode. Enabled="
                    + _enabled.Value.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed during startup: " + exception);
                DisableAfterStartupFailure();
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(
                    PluginGuid,
                    PluginName,
                    exception);
            }
        }

        private void DisableAfterStartupFailure()
        {
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
            enabled = false;

            if (_harmony != null)
            {
                try
                {
                    _harmony.UnpatchSelf();
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(
                        "Failed to remove partial startup patches: "
                        + exception.GetBaseException().Message);
                }
                _harmony = null;
            }

            if (_gft != null)
            {
                try
                {
                    _gft.Release();
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(
                        "Failed to release the Grail Floating Text bridge after startup failure: "
                        + exception.GetBaseException().Message);
                }
                _gft = null;
            }

            _pendingResults.Clear();
            _committedTarget = null;
            _committedUntil = 0.0f;
            _backstabReadyTarget = null;
            _backstabReadyUntil = 0.0f;
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (_gft != null)
            {
                _gft.Release();
                _gft = null;
            }

            _pendingResults.Clear();
            _backstabReadyTarget = null;
            _backstabReadyUntil = 0.0f;
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_enabled == null || !_enabled.Value || Time.unscaledTime < _nextAwarenessPoll)
            {
                return;
            }

            _nextAwarenessPoll = Time.unscaledTime + AwarenessPollSeconds;
            PollAwareness();
            ExpireTransientState();
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind("General", "Enabled", true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Master switch for all Ambush Integrity experiments.",
                    "General", "Enabled", 0, 0));
            Config.Bind(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Do not edit manually.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));

            _committedAmbushEnabled = Config.Bind("Committed Ambush", "Enabled", true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Preserve sneak eligibility briefly after a valid backstab target is acquired.",
                    "Committed Ambush", "Enabled", 10, 0));
            _committedAmbushSeconds = Config.Bind(
                "Committed Ambush",
                "CommitmentSeconds",
                0.45f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Seconds that the exact acquired target remains eligible for a committed ambush.",
                    "Committed Ambush", "Commitment Duration", 10, 10,
                    new AcceptableValueRange<float>(0.05f, 1.5f)));
            _backstabRangeMultiplier = Config.Bind(
                "Committed Ambush",
                "BackstabRangeMultiplier",
                1.2f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Multiplier for the native backstab interaction range.",
                    "Committed Ambush", "Backstab Range Multiplier", 10, 20,
                    new AcceptableValueRange<float>(1.0f, 2.0f)));
            _backstabAvailabilityGraceSeconds = Config.Bind(
                "Committed Ambush",
                "BackstabAvailabilityGraceSeconds",
                0.18f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Brief grace for visibility or animation flicker after the same target was valid for backstab.",
                    "Committed Ambush", "Backstab Availability Grace", 10, 30,
                    new AcceptableValueRange<float>(0.0f, 0.5f)));

            _footstepAwarenessEnabled = Config.Bind(
                "Footstep Awareness",
                "Enabled",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Make normal walking and running footsteps build native alert faster according to worn armor. Light or no armor uses 1.2x native strength, Medium uses 1.6x, and Heavy or Overload uses 2.0x. Crouched noise remains native.",
                    "Footstep Awareness", "Enabled", 20, 0));

            _cleanExecutionsEnabled = Config.Bind("Clean Executions", "Enabled", true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Suppress the killed target's immediate hit noise and ally notification only when a lethal sneak melee strike has no nearby witness.",
                    "Clean Executions", "Enabled", 30, 0));
            _cleanExecutionWitnessRange = Config.Bind(
                "Clean Executions",
                "WitnessRangeMeters",
                18.0f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum distance at which a friendly NPC can witness a clean execution. Uncertain cases count as witnessed.",
                    "Clean Executions", "Witness Range", 30, 10,
                    new AcceptableValueRange<float>(5.0f, 40.0f)));

            _gftNotificationsEnabled = Config.Bind("Notifications", "EnableNotifications", true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Show Ambush Integrity state changes and intervention results when Grail Floating Text is installed.",
                    "Notifications", "Enable Notifications", 40, 0));
            _notificationDetail = Config.Bind("Notifications", "NotificationDetail", NotificationDetail.Detailed,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Essential shows ambush outcomes; Detailed also shows awareness transitions and resisted clean executions.",
                    "Notifications", "Notification Detail", 40, 10));

            _diagnostics = Config.Bind("Diagnostics", "Diagnostics", false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Log footstep strength, eligibility decisions, witness checks, awareness transitions, and applied modifiers.",
                    "Diagnostics", "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 0));
            _showGrailFloatingTextDiagnostics = Config.Bind("Diagnostics", "ShowGrailFloatingTextDiagnostics", true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "When Diagnostics is enabled and Grail Floating Text is installed, show concise in-game diagnostic messages.",
                    "Diagnostics", "Show Grail Floating Text Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 10));

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
            string path = Config.ConfigFilePath;
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            int stored = 0;
            string section = String.Empty;
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string prefix = "ConfigSchemaVersion =";
                if (String.Equals(section, "1. Core", StringComparison.Ordinal)
                    && line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    Int32.TryParse(
                        line.Substring(prefix.Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out stored);
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

            string backup = path
                + ".pre-schema-"
                + stored.ToString(CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + ".bak";
            try
            {
                File.Copy(path, backup, false);
                File.WriteAllText(path, String.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo("Configuration schema changed from " + stored + " to " + ConfigSchemaVersion + ". Generated defaults and backed up " + backup + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid,
                    PluginName,
                    stored,
                    ConfigSchemaVersion);
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

            int restored = 0;
            int clamped = 0;
            RestorePreserved(_enabled, ref restored, ref clamped);
            RestorePreserved(_committedAmbushEnabled, ref restored, ref clamped);
            RestorePreserved(_committedAmbushSeconds, ref restored, ref clamped);
            RestorePreserved(_backstabRangeMultiplier, ref restored, ref clamped);
            RestorePreserved(_backstabAvailabilityGraceSeconds, ref restored, ref clamped);
            RestorePreserved(_footstepAwarenessEnabled, ref restored, ref clamped);
            RestorePreserved(_cleanExecutionsEnabled, ref restored, ref clamped);
            RestorePreserved(_cleanExecutionWitnessRange, ref restored, ref clamped);
            RestorePreserved(_gftNotificationsEnabled, ref restored, ref clamped);
            RestorePreserved(_notificationDetail, ref restored, ref clamped);
            RestorePreserved(_diagnostics, ref restored, ref clamped);
            RestorePreserved(_showGrailFloatingTextDiagnostics, ref restored, ref clamped);
            Logger.LogInfo("Preserved " + restored + " customized setting(s) across the config schema reset; clamped=" + clamped + ".");
            _pendingConfigRecoveryProfile = null;
        }

        private void RestorePreserved<T>(ConfigEntry<T> entry, ref int restored, ref int clamped)
        {
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                _pendingConfigRecoveryProfile;
            if (entry == null || profile == null)
            {
                return;
            }

            T value;
            if (!profile.TryGetCustomizedValue(entry.Definition.Section, entry.Definition.Key, out value))
            {
                return;
            }

            bool wasClamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(entry, value, out wasClamped))
            {
                return;
            }

            restored++;
            if (wasClamped)
            {
                clamped++;
            }
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);
            PatchRequired(AccessTools.PropertyGetter(typeof(HeroControllerData), "BackStabRangeSqr"), typeof(BackstabRangePatch), nameof(BackstabRangePatch.Postfix));
            PatchRequired(AccessTools.PropertyGetter(typeof(MeleeFSM), "IsBackStabAvailable"), typeof(BackstabAvailabilityPatch), nameof(BackstabAvailabilityPatch.Postfix));
            PatchRequired(
                AccessTools.Method(
                    typeof(AINoises),
                    "MakeHeroFootstepNoise",
                    new[] { typeof(float), typeof(float), typeof(float), typeof(Vector3) }),
                typeof(HeroFootstepNoisePatch),
                nameof(HeroFootstepNoisePatch.Prefix));
            PatchRequired(
                AccessTools.Method(typeof(HealthElement), "ApplyDamageModifiers"),
                typeof(DamageModifiersPatch),
                nameof(DamageModifiersPatch.Postfix),
                SteelAndBonePluginGuid);
            PatchRequired(AccessTools.Method(typeof(HealthElement), "AfterHealthDecreaseEvents"), typeof(DamageOutcomePatch), nameof(DamageOutcomePatch.Postfix));
            PatchRequired(AccessTools.Method(typeof(NpcAI), "OnDamageTaken"), typeof(NpcDamageTakenPatch), nameof(NpcDamageTakenPatch.Prefix));
        }

        private void PatchRequired(MethodInfo original, Type patchType, string patchName, string beforeOwner = null)
        {
            MethodInfo patch = AccessTools.Method(patchType, patchName);
            if (original == null || patch == null)
            {
                throw new MissingMethodException("Required gameplay hook is unavailable: " + patchType.Name + "." + patchName);
            }

            HarmonyMethod harmonyPatch = new HarmonyMethod(patch);
            if (!string.IsNullOrEmpty(beforeOwner))
            {
                harmonyPatch.before = new[] { beforeOwner };
            }

            if (patchName == "Prefix")
            {
                _harmony.Patch(original, harmonyPatch);
            }
            else
            {
                _harmony.Patch(original, null, harmonyPatch);
            }
            LogDiagnostic("Patched " + original.DeclaringType.FullName + "." + original.Name + ".");
        }

        internal float AdjustBackstabRange(float nativeRangeSquared)
        {
            if (_enabled == null || !_enabled.Value || _backstabRangeMultiplier == null)
            {
                return nativeRangeSquared;
            }
            float multiplier = _backstabRangeMultiplier.Value;
            float adjustedRangeSquared = nativeRangeSquared * multiplier * multiplier;
            if (_diagnostics != null
                && _diagnostics.Value
                && (!_backstabRangeDiagnosticInitialized
                    || !Mathf.Approximately(nativeRangeSquared, _lastBackstabRangeNativeSquared)
                    || !Mathf.Approximately(multiplier, _lastBackstabRangeMultiplier)))
            {
                _backstabRangeDiagnosticInitialized = true;
                _lastBackstabRangeNativeSquared = nativeRangeSquared;
                _lastBackstabRangeMultiplier = multiplier;
                LogDiagnostic(
                    "Backstab range: nativeMeters="
                    + Mathf.Sqrt(Mathf.Max(0.0f, nativeRangeSquared)).ToString("0.###", CultureInfo.InvariantCulture)
                    + "; multiplier="
                    + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; effectiveMeters="
                    + Mathf.Sqrt(Mathf.Max(0.0f, adjustedRangeSquared)).ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }
            return adjustedRangeSquared;
        }

        internal void ApplyFootstepAwareness(ref float noiseStrength)
        {
            if (_enabled == null
                || !_enabled.Value
                || _footstepAwarenessEnabled == null
                || !_footstepAwarenessEnabled.Value)
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null)
            {
                return;
            }

            ArmorWeight armorWeight = hero.TryGetElement<ArmorWeight>();
            ItemWeight armorTier = armorWeight == null
                ? ItemWeight.Light
                : armorWeight.ArmorWeightType;
            float before = noiseStrength;
            if (hero.IsCrouching)
            {
                LogFootstepDiagnostic(armorTier, true, before, noiseStrength);
                return;
            }

            float multiplier;
            if (armorTier == ItemWeight.Heavy || armorTier == ItemWeight.Overload)
            {
                multiplier = 2.0f;
            }
            else if (armorTier == ItemWeight.Medium)
            {
                multiplier = 1.6f;
            }
            else
            {
                multiplier = 1.2f;
            }
            noiseStrength *= multiplier;
            LogFootstepDiagnostic(armorTier, false, before, noiseStrength);
        }

        private void LogFootstepDiagnostic(
            ItemWeight armorTier,
            bool crouching,
            float before,
            float after)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            float now = Time.unscaledTime;
            bool stateChanged = !_footstepDiagnosticInitialized
                || crouching != _lastFootstepDiagnosticWasCrouching
                || armorTier != _lastFootstepDiagnosticArmorTier;
            if (!stateChanged && now < _nextFootstepDiagnosticAt)
            {
                return;
            }

            _footstepDiagnosticInitialized = true;
            _lastFootstepDiagnosticWasCrouching = crouching;
            _lastFootstepDiagnosticArmorTier = armorTier;
            _nextFootstepDiagnosticAt = now + FootstepDiagnosticIntervalSeconds;
            Logger.LogInfo(
                "[diagnostic] Footstep awareness: mode="
                + (crouching ? "native crouch" : "standing armor-scaled")
                + "; armor="
                + armorTier
                + "; strength="
                + before.ToString("0.###", CultureInfo.InvariantCulture)
                + "->"
                + after.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
        }

        internal void TrackBackstabAvailability(ref bool available)
        {
            if (_enabled == null || !_enabled.Value)
            {
                _backstabReadyTarget = null;
                _backstabReadyUntil = 0.0f;
                return;
            }

            NpcElement current = GetCurrentRaycastNpc();
            float now = Time.unscaledTime;
            bool nativeAvailable = available;
            NpcElement previousReadyTarget = _backstabReadyTarget;
            bool previousReadyWasFresh = previousReadyTarget != null && now <= _backstabReadyUntil;
            if (_committedAmbushEnabled.Value && available && current != null)
            {
                _promptGraceDiagnosticActive = false;
                bool newlyCommitted = !ReferenceEquals(current, _committedTarget) || now > _committedUntil;
                _committedTarget = current;
                _committedUntil = now + _committedAmbushSeconds.Value;
                if (newlyCommitted)
                {
                    LogDiagnostic(
                        "Committed Ambush acquired; target="
                        + DescribeNpc(current)
                        + "; windowSeconds="
                        + _committedAmbushSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)
                        + ".");
                }
            }
            else if (_committedAmbushEnabled.Value
                && !available
                && current != null
                && ReferenceEquals(current, _committedTarget)
                && now <= _committedUntil
                && Hero.Current != null
                && Hero.Current.IsCrouching
                && now <= _committedUntil - _committedAmbushSeconds.Value + _backstabAvailabilityGraceSeconds.Value)
            {
                available = true;
                if (!_promptGraceDiagnosticActive)
                {
                    LogDiagnostic(
                        "Backstab availability preserved during prompt grace; target="
                        + DescribeNpc(current)
                        + "; graceSeconds="
                        + _backstabAvailabilityGraceSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)
                        + "; commitmentSecondsRemaining="
                        + Mathf.Max(0.0f, _committedUntil - now).ToString("0.###", CultureInfo.InvariantCulture)
                        + ".");
                }
                _promptGraceDiagnosticActive = true;
            }
            else
            {
                _promptGraceDiagnosticActive = false;
            }

            if (available && current != null)
            {
                _backstabReadyTarget = current;
                _backstabReadyUntil = now + BackstabApiFreshnessSeconds;
            }
            else
            {
                _backstabReadyTarget = null;
                _backstabReadyUntil = 0.0f;
            }

            NpcElement readyTarget = available && current != null ? current : null;
            if (!ReferenceEquals(previousReadyTarget, readyTarget)
                || (!previousReadyWasFresh && readyTarget != null))
            {
                if (previousReadyTarget == null)
                {
                    LogDiagnostic(
                        "Backstab opportunity ready; target="
                        + DescribeNpc(readyTarget)
                        + "; nativeAvailable="
                        + nativeAvailable.ToString(CultureInfo.InvariantCulture)
                        + ".");
                }
                else if (readyTarget == null)
                {
                    LogDiagnostic(
                        "Backstab opportunity cleared; previousTarget="
                        + DescribeNpc(previousReadyTarget)
                        + "; nativeAvailable="
                        + nativeAvailable.ToString(CultureInfo.InvariantCulture)
                        + ".");
                }
                else
                {
                    LogDiagnostic(
                        "Backstab opportunity switched; previousTarget="
                        + DescribeNpc(previousReadyTarget)
                        + "; target="
                        + DescribeNpc(readyTarget)
                        + "; nativeAvailable="
                        + nativeAvailable.ToString(CultureInfo.InvariantCulture)
                        + ".");
                }
            }
        }

        internal int ReadBackstabOpportunityState()
        {
            NpcElement raycastTarget = GetCurrentRaycastNpc();
            BackstabOpportunityState state = _enabled == null
                || !_enabled.Value
                || _backstabReadyTarget == null
                || Time.unscaledTime > _backstabReadyUntil
                || !ReferenceEquals(_backstabReadyTarget, raycastTarget)
                    ? BackstabOpportunityState.None
                    : BackstabOpportunityState.Ready;
            if (_diagnostics != null
                && _diagnostics.Value
                && (!_backstabApiDiagnosticInitialized
                    || state != _lastBackstabApiDiagnosticState
                    || (state == BackstabOpportunityState.Ready
                        && !ReferenceEquals(raycastTarget, _lastBackstabApiDiagnosticTarget))))
            {
                _backstabApiDiagnosticInitialized = true;
                _lastBackstabApiDiagnosticState = state;
                _lastBackstabApiDiagnosticTarget = raycastTarget;
                LogDiagnostic(
                    "Backstab API state: "
                    + state
                    + "; storedTarget="
                    + DescribeNpc(_backstabReadyTarget)
                    + "; raycastTarget="
                    + DescribeNpc(raycastTarget)
                    + ".");
            }

            return (int)state;
        }

        internal void HandleDamageModifiers(
            HealthElement healthElement,
            Damage damage,
            ref DamageModifiersInfo result,
            ref float damageModifier)
        {
            if (_enabled == null || !_enabled.Value || healthElement == null || damage == null)
            {
                return;
            }

            Hero hero = Hero.Current;
            NpcElement target = damage.TargetPure as NpcElement;
            if (hero == null || target == null || !ReferenceEquals(damage.DamageDealerPure, hero))
            {
                return;
            }

            if (!damage.IsPrimary || damage.IsDamageOverTime || damage.Item == null || !damage.Item.IsMelee)
            {
                string reason = !damage.IsPrimary
                    ? "secondary damage"
                    : damage.IsDamageOverTime
                        ? "damage over time"
                        : damage.Item == null
                            ? "no attack item"
                            : "non-melee attack";
                LogDiagnostic(
                    "Damage decision: no change; target="
                    + DescribeNpc(target)
                    + "; reason="
                    + reason
                    + "; primary="
                    + damage.IsPrimary.ToString(CultureInfo.InvariantCulture)
                    + "; damageOverTime="
                    + damage.IsDamageOverTime.ToString(CultureInfo.InvariantCulture)
                    + ".");
                return;
            }

            if (result.IsSneak)
            {
                LogDiagnostic(
                    "Damage decision: no change; target="
                    + DescribeNpc(target)
                    + "; reason=native sneak classification already present.");
                return;
            }

            float bonus = CalculateSneakBonus(hero, damage);
            if (bonus <= 0.0f)
            {
                LogDiagnostic(
                    "Damage decision: no change; target="
                    + DescribeNpc(target)
                    + "; reason=calculated sneak bonus was zero.");
                return;
            }

            PendingResult pending;
            float modifierBefore = damageModifier;
            if (_committedAmbushEnabled.Value
                && ReferenceEquals(target, _committedTarget)
                && Time.unscaledTime <= _committedUntil)
            {
                ApplySneakResult(ref result, ref damageModifier, bonus);
                pending = new PendingResult(PendingKind.CommittedAmbush, bonus);
                _pendingResults[damage] = pending;
                _committedTarget = null;
                LogDiagnostic(
                    "Damage decision: applied Committed Ambush; target="
                    + DescribeNpc(target)
                    + "; bonus="
                    + bonus.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; modifier="
                    + modifierBefore.ToString("0.###", CultureInfo.InvariantCulture)
                    + "->"
                    + damageModifier.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; opportunity=consumed.");
                return;
            }

            LogDiagnostic(
                "Damage decision: no change; target="
                + DescribeNpc(target)
                + "; reason=no matching active opportunity; committedTargetMatch="
                + ReferenceEquals(target, _committedTarget).ToString(CultureInfo.InvariantCulture)
                + "; committedSecondsRemaining="
                + Mathf.Max(0.0f, _committedUntil - Time.unscaledTime).ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
        }

        private static void ApplySneakResult(
            ref DamageModifiersInfo result,
            ref float damageModifier,
            float sneakBonus)
        {
            damageModifier += sneakBonus;
            result = new DamageModifiersInfo(
                result.IsCritical,
                result.CriticalMultiplier,
                true,
                sneakBonus,
                result.IsWeakSpot,
                result.WeakSpotMultiplier,
                result.IsBackStab,
                result.BackStabMultiplier,
                result.IsFinisher);
        }

        private static float CalculateSneakBonus(Hero hero, Damage damage)
        {
            float bonus = (float)hero.HeroStats.SneakDamageMultiplier;
            if (damage.Item != null && damage.Item.IsMelee)
            {
                bonus += (float)hero.HeroStats.MeleeSneakDamageMultiplier;
            }
            if (damage.Item != null && damage.Item.ItemStats != null)
            {
                bonus += (float)damage.Item.ItemStats.SneakDamageMultiplier;
            }
            return Mathf.Max(0.0f, bonus);
        }

        internal void HandleDamageOutcome(DamageOutcome outcome)
        {
            PendingResult pending;
            if (!_pendingResults.TryGetValue(outcome.Damage, out pending))
            {
                return;
            }
            _pendingResults.Remove(outcome.Damage);

            if (!_gftNotificationsEnabled.Value)
            {
                LogDiagnostic("GFT dispatch skipped; event=" + pending.Kind + "; reason=notifications disabled.");
                return;
            }
            if (_gft == null)
            {
                LogDiagnostic("GFT dispatch skipped; event=" + pending.Kind + "; reason=bridge unavailable.");
                return;
            }

            bool delivered;
            if (pending.Kind == PendingKind.CleanExecution)
            {
                delivered = _gft.TryShowCommittedAmbush("CLEAN EXECUTION");
            }
            else if (pending.Kind == PendingKind.AmbushResisted)
            {
                delivered = _gft.TryShowAmbushResisted("AMBUSH RESISTED - WITNESSED");
            }
            else
            {
                delivered = _gft.TryShowCommittedAmbush("COMMITTED AMBUSH");
            }
            LogDiagnostic(
                "GFT dispatch: event="
                + pending.Kind
                + "; accepted="
                + delivered.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        internal bool ShouldRunNpcDamageTaken(NpcAI npcAI, DamageOutcome outcome)
        {
            if (_enabled == null
                || !_enabled.Value
                || !_cleanExecutionsEnabled.Value
                || npcAI == null)
            {
                return true;
            }

            Damage damage = outcome.Damage;
            NpcElement victim = npcAI.NpcElement;
            if (damage == null
                || victim == null
                || !ReferenceEquals(damage.DamageDealerPure, Hero.Current))
            {
                return true;
            }

            string bypassReason = null;
            if (!outcome.DamageModifiersInfo.IsSneak)
            {
                bypassReason = "damage was not sneak-classified";
            }
            else if (damage.Item == null)
            {
                bypassReason = "no attack item";
            }
            else if (!damage.Item.IsMelee)
            {
                bypassReason = "non-melee attack";
            }
            else if (damage.IsDamageOverTime)
            {
                bypassReason = "damage over time";
            }
            else if (!damage.IsPrimary)
            {
                bypassReason = "secondary damage";
            }
            else if (victim.Health.ModifiedValue > 0.0f)
            {
                bypassReason = "target survived";
            }
            if (bypassReason != null)
            {
                LogDiagnostic(
                    "Clean Execution decision: vanilla broadcast; target="
                    + DescribeNpc(victim)
                    + "; reason="
                    + bypassReason
                    + ".");
                return true;
            }

            PendingResult pending;
            bool hadPending = _pendingResults.TryGetValue(damage, out pending);
            string witnessSummary;
            if (HasExecutionWitness(victim, out witnessSummary))
            {
                if (_notificationDetail.Value == NotificationDetail.Detailed)
                {
                    _pendingResults[damage] = new PendingResult(PendingKind.AmbushResisted, hadPending ? pending.Bonus : 0.0f);
                }
                LogDiagnostic(
                    "Clean Execution decision: vanilla broadcast; target="
                    + DescribeNpc(victim)
                    + "; witness="
                    + witnessSummary
                    + ".");
                return true;
            }

            _pendingResults[damage] = new PendingResult(PendingKind.CleanExecution, hadPending ? pending.Bonus : 0.0f);
            LogDiagnostic(
                "Clean Execution decision: suppressed immediate hostile-action broadcast; target="
                + DescribeNpc(victim)
                + "; witness="
                + witnessSummary
                + ".");
            return false;
        }

        private bool HasExecutionWitness(NpcElement victim, out string witnessSummary)
        {
            try
            {
                float rangeSquared = _cleanExecutionWitnessRange.Value * _cleanExecutionWitnessRange.Value;
                int friendlyCandidates = 0;
                for (int index = 0; index < NpcAI.AllWorkingAI.Count; index++)
                {
                    NpcAI observer = NpcAI.AllWorkingAI[index];
                    if (observer == null || ReferenceEquals(observer.NpcElement, victim))
                    {
                        continue;
                    }

                    NpcElement other = observer.NpcElement;
                    if (other == null
                        || other.HasBeenDiscarded
                        || !other.IsAlive
                        || !victim.IsFriendlyTo(other)
                        || (other.Coords - victim.Coords).sqrMagnitude > rangeSquared)
                    {
                        continue;
                    }

                    friendlyCandidates++;
                    bool observerInCombat = observer.InCombat;
                    bool observerAlerted = observer.HeroVisibility > 0.1f;
                    bool seesVictim = false;
                    bool seesHero = false;
                    if (!observerInCombat && !observerAlerted)
                    {
                        seesVictim = AIUtils.CanSee(observer.VisionDetectionOrigin, victim.Head.position);
                        if (!seesVictim && Hero.Current != null)
                        {
                            seesHero = AIUtils.CanSee(observer.VisionDetectionOrigin, Hero.Current.Head.position);
                        }
                    }
                    if (observerInCombat || observerAlerted || seesVictim || seesHero)
                    {
                        witnessSummary = DescribeNpc(other)
                            + "; distanceMeters="
                            + Vector3.Distance(other.Coords, victim.Coords).ToString("0.###", CultureInfo.InvariantCulture)
                            + "; inCombat="
                            + observerInCombat.ToString(CultureInfo.InvariantCulture)
                            + "; heroVisibility="
                            + observer.HeroVisibility.ToString("0.###", CultureInfo.InvariantCulture)
                            + "; seesVictim="
                            + seesVictim.ToString(CultureInfo.InvariantCulture)
                            + "; seesHero="
                            + seesHero.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                }
                witnessSummary = "none; friendlyCandidatesInRange="
                    + friendlyCandidates.ToString(CultureInfo.InvariantCulture)
                    + "; rangeMeters="
                    + _cleanExecutionWitnessRange.Value.ToString("0.###", CultureInfo.InvariantCulture);
                return false;
            }
            catch (Exception exception)
            {
                witnessSummary = "fail-safe; error=" + exception.GetBaseException().Message;
                return true;
            }
        }

        private static NpcElement GetCurrentRaycastNpc()
        {
            Hero hero = Hero.Current;
            if (hero == null || hero.VHeroController == null || hero.VHeroController.Raycaster == null)
            {
                return null;
            }

            var location = hero.VHeroController.Raycaster.NPCRef.Get();
            return location == null ? null : location.TryGetElement<NpcElement>();
        }

        private void PollAwareness()
        {
            Hero hero = Hero.Current;
            if (hero == null || hero.HeroCombat == null)
            {
                _awarenessInitialized = false;
                return;
            }

            AwarenessState current = hero.HeroCombat.IsHeroInFight
                ? AwarenessState.Detected
                : hero.HeroCombat.MaxEnemiesAlert >= 5.0f
                    ? AwarenessState.Searching
                    : AwarenessState.Unaware;
            if (!_awarenessInitialized)
            {
                _awarenessState = current;
                _awarenessInitialized = true;
                LogDiagnostic("Awareness initialized; state=" + current + ".");
                return;
            }
            if (current == _awarenessState)
            {
                return;
            }

            AwarenessState previous = _awarenessState;
            _awarenessState = current;
            LogDiagnostic("Awareness transition: " + previous + "->" + current + ".");
            if (!_gftNotificationsEnabled.Value)
            {
                LogDiagnostic("GFT dispatch skipped; event=AwarenessState; reason=notifications disabled.");
                return;
            }
            if (_notificationDetail.Value != NotificationDetail.Detailed)
            {
                LogDiagnostic("GFT dispatch skipped; event=AwarenessState; reason=notification detail is Essential.");
                return;
            }
            if (_gft == null)
            {
                LogDiagnostic("GFT dispatch skipped; event=AwarenessState; reason=bridge unavailable.");
                return;
            }

            bool delivered = false;
            string eventName = "None";
            if (current == AwarenessState.Detected)
            {
                eventName = "Detected";
                delivered = _gft.TryShowAwarenessState("STEALTH LOST - DETECTED");
            }
            else if (current == AwarenessState.Searching)
            {
                eventName = "Searching";
                delivered = _gft.TryShowAwarenessState("ENEMIES SEARCHING");
            }
            else if (previous != AwarenessState.Unaware)
            {
                eventName = "HiddenAgain";
                delivered = _gft.TryShowAwarenessState("HIDDEN AGAIN");
            }
            LogDiagnostic(
                "GFT dispatch: event=AwarenessState."
                + eventName
                + "; accepted="
                + delivered.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private void ExpireTransientState()
        {
            float now = Time.unscaledTime;
            if (_committedTarget != null && now > _committedUntil)
            {
                LogDiagnostic("Committed Ambush expired; target=" + DescribeNpc(_committedTarget) + ".");
                _committedTarget = null;
            }
            if (_backstabReadyTarget != null && now > _backstabReadyUntil)
            {
                LogDiagnostic("Backstab opportunity expired; target=" + DescribeNpc(_backstabReadyTarget) + ".");
                _backstabReadyTarget = null;
            }
            if (_pendingResults.Count > 64)
            {
                _pendingResults.Clear();
                LogDiagnostic("Cleared stale pending damage results.");
            }
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }
            Logger.LogInfo("[diagnostic] " + message);
            if (_gft != null && _showGrailFloatingTextDiagnostics.Value)
            {
                _gft.TryShowDiagnostic(message);
            }
        }

        private static string DescribeNpc(NpcElement npc)
        {
            if (npc == null)
            {
                return "none";
            }

            return npc.NpcType
                + "#"
                + npc.GetHashCode().ToString("X8", CultureInfo.InvariantCulture);
        }

        private enum AwarenessState
        {
            Unaware,
            Searching,
            Detected
        }

        private enum PendingKind
        {
            CommittedAmbush,
            CleanExecution,
            AmbushResisted
        }

        private struct PendingResult
        {
            public readonly PendingKind Kind;
            public readonly float Bonus;

            public PendingResult(PendingKind kind, float bonus)
            {
                Kind = kind;
                Bonus = bonus;
            }
        }

        private static class BackstabRangePatch
        {
            public static void Postfix(ref float __result)
            {
                AmbushIntegrityPlugin plugin = Instance;
                if (plugin != null)
                {
                    __result = plugin.AdjustBackstabRange(__result);
                }
            }
        }

        private static class BackstabAvailabilityPatch
        {
            public static void Postfix(ref bool __result)
            {
                AmbushIntegrityPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.TrackBackstabAvailability(ref __result);
                }
            }
        }

        private static class HeroFootstepNoisePatch
        {
            public static void Prefix(ref float __1)
            {
                AmbushIntegrityPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyFootstepAwareness(ref __1);
                }
            }
        }

        private static class DamageModifiersPatch
        {
            public static void Postfix(
                HealthElement __instance,
                Damage damage,
                ref DamageModifiersInfo __result,
                ref float dmgModifier)
            {
                AmbushIntegrityPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.HandleDamageModifiers(__instance, damage, ref __result, ref dmgModifier);
                }
            }
        }

        private static class DamageOutcomePatch
        {
            public static void Postfix(DamageOutcome outcome)
            {
                AmbushIntegrityPlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.HandleDamageOutcome(outcome);
                }
            }
        }

        private static class NpcDamageTakenPatch
        {
            public static bool Prefix(NpcAI __instance, DamageOutcome damageOutcome)
            {
                AmbushIntegrityPlugin plugin = Instance;
                return plugin == null || plugin.ShouldRunNpcDamageTaken(__instance, damageOutcome);
            }
        }
    }
}
