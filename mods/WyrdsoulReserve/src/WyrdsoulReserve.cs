using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Development.WyrdPowers;
using Awaken.TG.Main.Heroes.HUD;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Saving;
using Awaken.TG.Main.Saving.Cloud.Services;
using Awaken.TG.Main.Saving.SaveSlots;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[assembly: AssemblyTitle("Wyrdsoul Reserve")]
[assembly: AssemblyDescription("Adds three overflow reserves to Wyrd Power")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Wyrdsoul Reserve")]
[assembly: AssemblyVersion("1.0.5.0")]
[assembly: AssemblyFileVersion("1.0.5.0")]
[assembly: AssemblyInformationalVersion("1.0.5")]

namespace WyrdsoulReserve
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "ks.tgfoa.glorious-ui",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class WyrdsoulReservePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.wyrdsoul-reserve";
        public const string PluginName = "Wyrdsoul Reserve";
        public const string PluginVersion = "1.0.5";

        private const int ConfigSchemaVersion = 2;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[]
            ConfigRecoveryPermanentExclusions =
                new ConfigDefinition[0];

        private const int ReserveCount = 3;
        private const int ReserveSpriteCount = 10;
        private const int SaveDataSchema = 1;
        private const string SaveDataFileName = "WyrdsoulReserve.json";
        private const float Epsilon = 0.001f;
        private const float DefaultActivationCostPercent = 3.0f;
        private const float DefaultRechargeDelaySeconds = 5.0f;
        private const float DefaultPassiveFullRechargeMinutes = 20.0f;
        private const float DefaultWyrdNightMultiplier = 3.0f;
        private const float DefaultReserveGainEfficiencyPercent = 100.0f;
        private const float DefaultTransferSecondsPerReserve = 0.75f;
        private const float DefaultHudOffsetX = 0.0f;
        private const float DefaultHudOffsetY = 0.0f;
        private const float DefaultHudScale = 1.0f;
        private const float DefaultIconSize = 42.0f;
        private const float ReserveStepX = 28.0f;
        private const float ReserveStepY = 34.0f;
        private const float FirstReserveX = -92.0f;
        private const float FirstReserveY = -18.0f;

        internal static WyrdsoulReservePlugin Instance { get; private set; }

        private readonly object _stateLock = new object();
        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedConfigValues =
                new Dictionary<ConfigDefinition, object>();
        private readonly Texture2D[] _reserveTextures =
            new Texture2D[ReserveSpriteCount];
        private readonly Sprite[] _reserveSprites =
            new Sprite[ReserveSpriteCount];
        private readonly Image[] _reserveImages =
            new Image[ReserveCount];
        private readonly int[] _displayedReserveFrames =
            new[] { -1, -1, -1 };

        private ConfigEntry<bool> _featureEnabled;
        private ConfigEntry<float> _activationCostPercent;
        private ConfigEntry<float> _rechargeDelaySeconds;
        private ConfigEntry<float> _passiveFullRechargeMinutes;
        private ConfigEntry<float> _wyrdNightMultiplier;
        private ConfigEntry<float> _reserveGainEfficiencyPercent;
        private ConfigEntry<float> _transferSecondsPerReserve;
        private ConfigEntry<float> _hudOffsetX;
        private ConfigEntry<float> _hudOffsetY;
        private ConfigEntry<float> _hudScale;
        private ConfigEntry<float> _iconSize;
        private ConfigEntry<bool> _diagnostics;

        private Harmony _harmony;
        private MethodInfo _loadImageMethod;
        private GameObject _reserveRoot;
        private VHeroHUD _attachedHeroHud;
        private Hero _trackedHero;
        private float _reserveEquivalentFraction;
        private float _rechargeDelayRemaining;
        private bool _internalStatChange;
        private int _expectedSlotLoadDepth;
        private bool _pendingLoadReady;
        private bool _pendingLoadHasData;
        private Guid _pendingLoadHeroId;
        private float _pendingLoadReserveFraction;
        private byte[] _pendingSaveBytes;

        private void Awake()
        {
            Instance = this;

            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
                LoadReserveSprites();
                PatchGame();
                BeginRechargeDelay();

                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. Reserve capacity=100% of the main bar; passive recharge="
                    + GetPassiveFullRechargeMinutes().ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " minutes per main bar; Wyrd-night multiplier="
                    + GetWyrdNightMultiplier().ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + "x.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    PluginName + " failed during startup: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowLoadTimeError(
                        PluginGuid,
                        PluginName,
                        exception);
                enabled = false;
            }
        }

        private void Update()
        {
            Hero hero = Hero.Current;
            if (!ReferenceEquals(hero, _trackedHero))
            {
                TrackCurrentHero(hero);
            }

            if (_reserveRoot != null)
            {
                ApplyHudLayout();
                UpdateReserveVisuals();
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            ReleaseHud();
            ReleaseReserveSprites();
            lock (_stateLock)
            {
                _pendingSaveBytes = null;
                _pendingLoadReady = false;
                _pendingLoadHasData = false;
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void BindConfig()
        {
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Internal config schema marker. Do not edit this value.",
                    null,
                    new BrowsableAttribute(false)));
            _featureEnabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Master switch. Disabling restores vanilla Wyrd Power behavior and hides the reserve diamonds without deleting saved reserve charge.");
            _activationCostPercent = Config.Bind(
                "2. Resource",
                "ActivationCostPercent",
                DefaultActivationCostPercent,
                new ConfigDescription(
                    "Percent of the main Wyrd Power bar spent immediately on every activation. This limits free repeated activation effects.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));
            _rechargeDelaySeconds = Config.Bind(
                "2. Resource",
                "RechargeDelaySeconds",
                DefaultRechargeDelaySeconds,
                new ConfigDescription(
                    "Inactive gameplay seconds before reserve transfer or passive regeneration begins.",
                    new AcceptableValueRange<float>(0.0f, 60.0f)));
            _passiveFullRechargeMinutes = Config.Bind(
                "2. Resource",
                "PassiveFullRechargeMinutes",
                DefaultPassiveFullRechargeMinutes,
                new ConfigDescription(
                    "Active gameplay minutes required for passive regeneration to produce one complete main bar before the Wyrd-night multiplier. Passive regeneration fills the main bar first, then the reserves.",
                    new AcceptableValueRange<float>(1.0f, 240.0f)));
            _wyrdNightMultiplier = Config.Bind(
                "2. Resource",
                "WyrdNightRegenerationMultiplier",
                DefaultWyrdNightMultiplier,
                new ConfigDescription(
                    "Multiplier applied only to passive regeneration during a native Wyrd night.",
                    new AcceptableValueRange<float>(0.0f, 10.0f)));
            _reserveGainEfficiencyPercent = Config.Bind(
                "2. Resource",
                "ReserveGainEfficiencyPercent",
                DefaultReserveGainEfficiencyPercent,
                new ConfigDescription(
                    "Percent of overflow and passive energy retained when it fills the reserves. 100 keeps all overflow; lower values add a Hollow Knight-style reserve penalty.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));
            _transferSecondsPerReserve = Config.Bind(
                "2. Resource",
                "TransferSecondsPerReserve",
                DefaultTransferSecondsPerReserve,
                new ConfigDescription(
                    "Seconds required for one full reserve diamond to pour back into the inactive main bar after the recharge delay.",
                    new AcceptableValueRange<float>(0.05f, 10.0f)));
            _hudOffsetX = Config.Bind(
                "3. HUD",
                "ReserveOffsetX",
                DefaultHudOffsetX,
                new ConfigDescription(
                    "Horizontal adjustment in local Wyrd-indicator pixels. Positive values move all reserve diamonds right.",
                    new AcceptableValueRange<float>(-500.0f, 500.0f)));
            _hudOffsetY = Config.Bind(
                "3. HUD",
                "ReserveOffsetY",
                DefaultHudOffsetY,
                new ConfigDescription(
                    "Vertical adjustment in local Wyrd-indicator pixels. Positive values move all reserve diamonds up.",
                    new AcceptableValueRange<float>(-500.0f, 500.0f)));
            _hudScale = Config.Bind(
                "3. HUD",
                "ReserveScale",
                DefaultHudScale,
                new ConfigDescription(
                    "Scale of the complete reserve group relative to the owned Wyrd indicator.",
                    new AcceptableValueRange<float>(0.25f, 3.0f)));
            _iconSize = Config.Bind(
                "3. HUD",
                "ReserveIconSize",
                DefaultIconSize,
                new ConfigDescription(
                    "Square size of each reserve diamond in local Wyrd-indicator pixels.",
                    new AcceptableValueRange<float>(12.0f, 160.0f)));
            _diagnostics = Config.Bind(
                "4. Diagnostics",
                "Diagnostics",
                false,
                "Log overflow capture, reserve transfer, passive regeneration, HUD attachment, and save-data details.");

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
            if (string.IsNullOrWhiteSpace(configPath)
                || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1
                    && line[0] == '['
                    && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if (string.Equals(
                        currentSection,
                        "1. Core",
                        StringComparison.Ordinal)
                    && line.StartsWith(
                        schemaPrefix,
                        StringComparison.Ordinal))
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

            CapturePreservedConfigValues(
                configPath,
                storedSchemaVersion);
            string backupPath = configPath
                + ".pre-schema-"
                + storedSchemaVersion.ToString(
                    CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture)
                + ".bak";

            try
            {
                File.Copy(configPath, backupPath, false);
                File.WriteAllText(configPath, string.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowConfigReset(
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
                        "Could not restore the previous Wyrdsoul Reserve config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset the Wyrdsoul Reserve config schema. The original config was left in place when possible.",
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

            CapturePreservedValue<bool>(profile, "1. Core", "Enabled");
            CapturePreservedValue<float>(profile, "2. Resource", "ActivationCostPercent");
            CapturePreservedValue<float>(profile, "2. Resource", "RechargeDelaySeconds");
            CapturePreservedValue<float>(profile, "2. Resource", "PassiveFullRechargeMinutes");
            CapturePreservedValue<float>(profile, "2. Resource", "WyrdNightRegenerationMultiplier");
            CapturePreservedValue<float>(profile, "2. Resource", "ReserveGainEfficiencyPercent");
            CapturePreservedValue<float>(profile, "2. Resource", "TransferSecondsPerReserve");
            CapturePreservedValue<float>(profile, "3. HUD", "ReserveOffsetX");
            CapturePreservedValue<float>(profile, "3. HUD", "ReserveOffsetY");
            CapturePreservedValue<float>(profile, "3. HUD", "ReserveScale");
            CapturePreservedValue<float>(profile, "3. HUD", "ReserveIconSize");
            CapturePreservedValue<bool>(profile, "4. Diagnostics", "Diagnostics");
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
            RestorePreservedValue(_featureEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_activationCostPercent, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_rechargeDelaySeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_passiveFullRechargeMinutes, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_wyrdNightMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_reserveGainEfficiencyPercent, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_transferSecondsPerReserve, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_hudOffsetX, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_hudOffsetY, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_hudScale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_iconSize, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(_diagnostics, ref restored, ref clamped, ref invalid);

            Logger.LogInfo(
                "Preserved "
                + restored.ToString(CultureInfo.InvariantCulture)
                + " Wyrdsoul Reserve setting(s) across the config schema reset; clamped="
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

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            MethodInfo activate = RequireMethod(
                typeof(WyrdSkillActivation),
                "Activate",
                Type.EmptyTypes);
            MethodInfo deactivate = RequireMethod(
                typeof(WyrdSkillActivation),
                "Deactivate",
                Type.EmptyTypes);
            MethodInfo update = RequireMethod(
                typeof(WyrdSkillActivation),
                "Update",
                new[] { typeof(float) });
            MethodInfo limitedStatSetTo = RequireMethod(
                typeof(LimitedStat),
                "SetTo",
                new[]
                {
                    typeof(float),
                    typeof(bool),
                    typeof(ContractContext)
                });

            _harmony.Patch(
                activate,
                prefix: new HarmonyMethod(
                    typeof(WyrdSkillActivationPatch),
                    nameof(WyrdSkillActivationPatch.BeforeActivate)));
            _harmony.Patch(
                deactivate,
                prefix: new HarmonyMethod(
                    typeof(WyrdSkillActivationPatch),
                    nameof(WyrdSkillActivationPatch.BeforeDeactivate)),
                postfix: new HarmonyMethod(
                    typeof(WyrdSkillActivationPatch),
                    nameof(WyrdSkillActivationPatch.AfterDeactivate)));
            _harmony.Patch(
                update,
                postfix: new HarmonyMethod(
                    typeof(WyrdSkillActivationPatch),
                    nameof(WyrdSkillActivationPatch.AfterUpdate)));
            _harmony.Patch(
                limitedStatSetTo,
                prefix: new HarmonyMethod(
                    typeof(LimitedStatPatch),
                    nameof(LimitedStatPatch.BeforeSetTo)),
                postfix: new HarmonyMethod(
                    typeof(LimitedStatPatch),
                    nameof(LimitedStatPatch.AfterSetTo)));

            PatchHeroHud();
            PatchSaveIntegration();
        }

        private void PatchHeroHud()
        {
            MethodInfo afterInitialized = RequireMethod(
                typeof(VHeroHUD),
                "AfterFullyInitialized",
                Type.EmptyTypes);
            MethodInfo onDiscard = RequireMethod(
                typeof(VHeroHUD),
                "OnDiscard",
                Type.EmptyTypes);

            _harmony.Patch(
                afterInitialized,
                postfix: new HarmonyMethod(
                    typeof(HeroHudPatch),
                    nameof(HeroHudPatch.AfterFullyInitialized)));
            _harmony.Patch(
                onDiscard,
                postfix: new HarmonyMethod(
                    typeof(HeroHudPatch),
                    nameof(HeroHudPatch.AfterDiscard)));
        }

        private void PatchSaveIntegration()
        {
            MethodInfo save = RequireMethod(
                typeof(LoadSave),
                "Save",
                new[] { typeof(SaveSlot), typeof(bool) });
            MethodInfo saveDomains = RequireMethod(
                typeof(SaveSystem),
                "SaveDomainsAsync",
                new[] { typeof(string[]), typeof(byte[]) });
            MethodInfo loadToCache = RequireMethod(
                typeof(LoadSave),
                "LoadSaveSlotToCache",
                new[] { typeof(SaveSlot) });
            MethodInfo loadGameplayToCache = RequireMethod(
                typeof(LoadSave),
                "LoadOnlyGameplayToCache",
                new[] { typeof(SaveSlot) });

            _harmony.Patch(
                save,
                prefix: new HarmonyMethod(
                    typeof(SavePatch),
                    nameof(SavePatch.BeforeSave)));
            _harmony.Patch(
                saveDomains,
                postfix: new HarmonyMethod(
                    typeof(SavePatch),
                    nameof(SavePatch.AfterSaveDomains)));
            _harmony.Patch(
                loadToCache,
                prefix: new HarmonyMethod(
                    typeof(SavePatch),
                    nameof(SavePatch.BeforeGameplaySlotLoad)),
                postfix: new HarmonyMethod(
                    typeof(SavePatch),
                    nameof(SavePatch.AfterGameplaySlotLoad)));
            _harmony.Patch(
                loadGameplayToCache,
                prefix: new HarmonyMethod(
                    typeof(SavePatch),
                    nameof(SavePatch.BeforeGameplaySlotLoad)),
                postfix: new HarmonyMethod(
                    typeof(SavePatch),
                    nameof(SavePatch.AfterGameplaySlotLoad)));

            HashSet<MethodInfo> patchedMethods = new HashSet<MethodInfo>();
            string[] cloudTypeNames =
            {
                "Awaken.TG.Main.Saving.Cloud.Services.DebugCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.GogCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.SteamCloudService",
                "Awaken.TG.Main.Saving.Cloud.Services.SteamNoCloudService"
            };
            foreach (string typeName in cloudTypeNames)
            {
                Type cloudType = AccessTools.TypeByName(typeName);
                MethodInfo beginLoad = cloudType == null
                    ? null
                    : AccessTools.Method(
                        cloudType,
                        "BeginLoadSlot",
                        new[] { typeof(string) });
                if (beginLoad == null || !patchedMethods.Add(beginLoad))
                {
                    continue;
                }

                _harmony.Patch(
                    beginLoad,
                    postfix: new HarmonyMethod(
                        typeof(SavePatch),
                        nameof(SavePatch.AfterCloudBeginLoad)));
            }

            if (patchedMethods.Count == 0)
            {
                throw new MissingMethodException(
                    "Could not resolve any CloudService.BeginLoadSlot implementation for Wyrdsoul Reserve save data.");
            }
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            Type[] parameterTypes)
        {
            MethodInfo method = AccessTools.Method(
                type,
                name,
                parameterTypes);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }

            return method;
        }

        internal bool IsFeatureEnabled
        {
            get
            {
                return _featureEnabled != null && _featureEnabled.Value;
            }
        }

        internal void ApplyActivationCost()
        {
            if (!IsFeatureEnabled)
            {
                return;
            }

            LimitedStat duration = GetWyrdSkillDuration();
            if (duration == null)
            {
                return;
            }

            float before = duration.BaseValue;
            float cost = duration.UpperLimit
                * (GetActivationCostPercent() / 100.0f);
            SetDurationTo(duration, before - cost);
            BeginRechargeDelay();
            LogDiagnostic(
                "Activation cost: "
                + FormatCharge(before, duration.UpperLimit)
                + " -> "
                + FormatCharge(duration.BaseValue, duration.UpperLimit)
                + ".");
        }

        internal float CaptureChargeBeforeDeactivation()
        {
            if (!IsFeatureEnabled)
            {
                return -1.0f;
            }

            LimitedStat duration = GetWyrdSkillDuration();
            return duration == null ? -1.0f : duration.BaseValue;
        }

        internal void FinishDeactivation(float chargeBeforeVanillaReset)
        {
            if (!IsFeatureEnabled)
            {
                return;
            }

            BeginRechargeDelay();
            if (chargeBeforeVanillaReset <= Epsilon)
            {
                return;
            }

            LimitedStat duration = GetWyrdSkillDuration();
            if (duration == null)
            {
                return;
            }

            SetDurationTo(duration, chargeBeforeVanillaReset);
            LogDiagnostic(
                "Manual cancellation preserved "
                + FormatCharge(
                    duration.BaseValue,
                    duration.UpperLimit)
                + ".");
        }

        internal void ProcessRecharge(
            WyrdSkillActivation activation,
            float deltaTime)
        {
            if (!IsFeatureEnabled
                || activation == null
                || activation.HasBeenDiscarded
                || LoadingScreenUI.IsLoading)
            {
                return;
            }

            if (activation.IsActive)
            {
                BeginRechargeDelay();
                return;
            }

            float elapsed = Math.Max(0.0f, deltaTime);
            if (elapsed <= 0.0f)
            {
                return;
            }

            if (_rechargeDelayRemaining > 0.0f)
            {
                _rechargeDelayRemaining = Math.Max(
                    0.0f,
                    _rechargeDelayRemaining - elapsed);
                return;
            }

            LimitedStat duration = GetWyrdSkillDuration();
            if (duration == null || duration.UpperLimit <= Epsilon)
            {
                return;
            }

            float missing = Math.Max(
                0.0f,
                duration.UpperLimit - duration.BaseValue);
            float reserveFraction = GetReserveFraction();
            if (missing > Epsilon && reserveFraction > Epsilon)
            {
                float transferRate =
                    (duration.UpperLimit / ReserveCount)
                    / GetTransferSecondsPerReserve();
                float amount = Math.Min(
                    missing,
                    Math.Min(
                        reserveFraction * duration.UpperLimit,
                        transferRate * elapsed));
                if (amount > Epsilon)
                {
                    SetDurationTo(
                        duration,
                        duration.BaseValue + amount);
                    AddReserveFraction(
                        -(amount / duration.UpperLimit),
                        "automatic transfer");
                }
                return;
            }

            if (duration.BaseValue >= duration.UpperLimit - Epsilon
                && reserveFraction >= 1.0f - Epsilon)
            {
                return;
            }

            float passivePerSecond = duration.UpperLimit
                / (GetPassiveFullRechargeMinutes() * 60.0f);
            if (IsNativeWyrdNight())
            {
                passivePerSecond *= GetWyrdNightMultiplier();
            }

            AddGeneratedResource(
                duration,
                passivePerSecond * elapsed,
                "passive regeneration");
        }

        internal void CaptureOverflowBeforeClamp(
            LimitedStat stat,
            float desiredValue)
        {
            if (!IsFeatureEnabled
                || _internalStatChange
                || !IsHeroWyrdDuration(stat)
                || stat.UpperLimit <= Epsilon
                || IsWyrdPowerActive())
            {
                return;
            }

            float overflow = desiredValue - stat.UpperLimit;
            if (overflow <= Epsilon)
            {
                return;
            }

            float retainedFraction =
                (overflow / stat.UpperLimit)
                * GetReserveGainEfficiency();
            AddReserveFraction(retainedFraction, "gameplay overflow");
        }

        internal void ObserveExternalStatChange(
            LimitedStat stat,
            float previousValue)
        {
            if (!IsFeatureEnabled
                || _internalStatChange
                || !IsHeroWyrdDuration(stat)
                || stat.BaseValue >= previousValue - Epsilon
                || IsWyrdPowerActive())
            {
                return;
            }

            BeginRechargeDelay();
        }

        private void AddGeneratedResource(
            LimitedStat duration,
            float amount,
            string reason)
        {
            if (amount <= Epsilon)
            {
                return;
            }

            float missing = Math.Max(
                0.0f,
                duration.UpperLimit - duration.BaseValue);
            float toMain = Math.Min(missing, amount);
            if (toMain > Epsilon)
            {
                SetDurationTo(
                    duration,
                    duration.BaseValue + toMain);
            }

            float overflow = amount - toMain;
            if (overflow > Epsilon)
            {
                AddReserveFraction(
                    (overflow / duration.UpperLimit)
                    * GetReserveGainEfficiency(),
                    reason);
            }
        }

        private void SetDurationTo(
            LimitedStat duration,
            float value)
        {
            _internalStatChange = true;
            try
            {
                duration.SetTo(value);
            }
            finally
            {
                _internalStatChange = false;
            }
        }

        private void AddReserveFraction(
            float amount,
            string reason)
        {
            if (Math.Abs(amount) <= Epsilon)
            {
                return;
            }

            float before;
            float after;
            lock (_stateLock)
            {
                before = _reserveEquivalentFraction;
                _reserveEquivalentFraction = Mathf.Clamp01(
                    _reserveEquivalentFraction + amount);
                after = _reserveEquivalentFraction;
            }

            if (Math.Abs(after - before) > Epsilon)
            {
                LogDiagnostic(
                    reason
                    + ": reserve "
                    + FormatPercent(before)
                    + " -> "
                    + FormatPercent(after)
                    + ".");
            }
        }

        private float GetReserveFraction()
        {
            lock (_stateLock)
            {
                return _reserveEquivalentFraction;
            }
        }

        private void SetReserveFraction(float value)
        {
            lock (_stateLock)
            {
                _reserveEquivalentFraction = Mathf.Clamp01(value);
            }
        }

        private void BeginRechargeDelay()
        {
            _rechargeDelayRemaining = GetRechargeDelaySeconds();
        }

        private static LimitedStat GetWyrdSkillDuration()
        {
            Hero hero = Hero.Current;
            return hero == null || hero.HasBeenDiscarded
                ? null
                : hero.WyrdSkillDuration;
        }

        private static bool IsHeroWyrdDuration(LimitedStat stat)
        {
            Hero hero = Hero.Current;
            return hero != null
                && !hero.HasBeenDiscarded
                && stat != null
                && ReferenceEquals(stat, hero.WyrdSkillDuration);
        }

        private static bool CanSafelyInterceptWyrdDuration(LimitedStat stat)
        {
            return !LoadingScreenUI.IsLoading
                && IsHeroWyrdDuration(stat);
        }

        private static bool IsWyrdPowerActive()
        {
            Hero hero = Hero.Current;
            WyrdSkillActivation activation = hero == null
                || hero.HasBeenDiscarded
                || hero.Development == null
                ? null
                : hero.Development.WyrdSkillActivation;
            return activation != null
                && !activation.HasBeenDiscarded
                && activation.IsActive;
        }

        private static bool IsNativeWyrdNight()
        {
            Hero hero = Hero.Current;
            HeroWyrdNight wyrdNight = hero == null
                || hero.HasBeenDiscarded
                ? null
                : hero.HeroWyrdNight;
            return hero != null
                && wyrdNight != null
                && !wyrdNight.HasBeenDiscarded
                && wyrdNight.Night;
        }

        private float GetActivationCostPercent()
        {
            return _activationCostPercent == null
                ? DefaultActivationCostPercent
                : Mathf.Clamp(_activationCostPercent.Value, 0.0f, 100.0f);
        }

        private float GetRechargeDelaySeconds()
        {
            return _rechargeDelaySeconds == null
                ? DefaultRechargeDelaySeconds
                : Mathf.Clamp(_rechargeDelaySeconds.Value, 0.0f, 60.0f);
        }

        private float GetPassiveFullRechargeMinutes()
        {
            return _passiveFullRechargeMinutes == null
                ? DefaultPassiveFullRechargeMinutes
                : Mathf.Clamp(
                    _passiveFullRechargeMinutes.Value,
                    1.0f,
                    240.0f);
        }

        private float GetWyrdNightMultiplier()
        {
            return _wyrdNightMultiplier == null
                ? DefaultWyrdNightMultiplier
                : Mathf.Clamp(_wyrdNightMultiplier.Value, 0.0f, 10.0f);
        }

        private float GetReserveGainEfficiency()
        {
            float percent = _reserveGainEfficiencyPercent == null
                ? DefaultReserveGainEfficiencyPercent
                : Mathf.Clamp(
                    _reserveGainEfficiencyPercent.Value,
                    0.0f,
                    100.0f);
            return percent / 100.0f;
        }

        private float GetTransferSecondsPerReserve()
        {
            return _transferSecondsPerReserve == null
                ? DefaultTransferSecondsPerReserve
                : Mathf.Clamp(
                    _transferSecondsPerReserve.Value,
                    0.05f,
                    10.0f);
        }

        private void LoadReserveSprites()
        {
            Type imageConversionType = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (imageConversionType == null)
            {
                throw new TypeLoadException(
                    "Could not find UnityEngine.ImageConversion.");
            }

            _loadImageMethod = imageConversionType.GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(Texture2D),
                    typeof(byte[]),
                    typeof(bool)
                },
                null);
            if (_loadImageMethod == null)
            {
                throw new MissingMethodException(
                    imageConversionType.FullName,
                    "LoadImage(Texture2D, byte[], bool)");
            }

            string assemblyDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                throw new DirectoryNotFoundException(
                    "Could not resolve the Wyrdsoul Reserve plugin directory.");
            }

            string iconDirectory = Path.Combine(
                assemblyDirectory,
                "reserve-icons");
            for (int index = 0; index < ReserveSpriteCount; index++)
            {
                string path = Path.Combine(
                    iconDirectory,
                    "reserve-"
                    + index.ToString(CultureInfo.InvariantCulture)
                    + ".png");
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "A Wyrdsoul Reserve HUD frame is missing.",
                        path);
                }

                Texture2D texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false);
                texture.name = "WyrdsoulReserveTexture" + index;
                texture.hideFlags = HideFlags.DontSave;
                object loaded = _loadImageMethod.Invoke(
                    null,
                    new object[]
                    {
                        texture,
                        File.ReadAllBytes(path),
                        false
                    });
                if (!(loaded is bool) || !((bool)loaded))
                {
                    UnityEngine.Object.Destroy(texture);
                    throw new InvalidDataException(
                        "Unity could not decode the Wyrdsoul Reserve HUD frame: "
                        + path);
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0.0f, 0.0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100.0f,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = "WyrdsoulReserveSprite" + index;
                sprite.hideFlags = HideFlags.DontSave;
                _reserveTextures[index] = texture;
                _reserveSprites[index] = sprite;
            }
        }

        private void ReleaseReserveSprites()
        {
            for (int index = 0; index < ReserveSpriteCount; index++)
            {
                if (_reserveSprites[index] != null)
                {
                    UnityEngine.Object.Destroy(_reserveSprites[index]);
                    _reserveSprites[index] = null;
                }
                if (_reserveTextures[index] != null)
                {
                    UnityEngine.Object.Destroy(_reserveTextures[index]);
                    _reserveTextures[index] = null;
                }
            }
        }

        internal void AttachHud(VHeroHUD heroHud)
        {
            if (heroHud == null)
            {
                return;
            }

            VCHeroWyrdSkillBar wyrdSkillBar =
                heroHud.GetComponentInChildren<VCHeroWyrdSkillBar>(true);
            if (wyrdSkillBar == null)
            {
                throw new MissingMemberException(
                    "The active Hero HUD did not contain VCHeroWyrdSkillBar.");
            }

            ReleaseHud();
            _attachedHeroHud = heroHud;
            _reserveRoot = new GameObject(
                "Wyrdsoul Reserve",
                typeof(RectTransform));
            _reserveRoot.hideFlags = HideFlags.DontSave;
            RectTransform rootRect =
                (RectTransform)_reserveRoot.transform;
            rootRect.SetParent(wyrdSkillBar.transform, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;
            rootRect.SetAsLastSibling();

            for (int index = 0; index < ReserveCount; index++)
            {
                GameObject icon = new GameObject(
                    "Reserve "
                    + (index + 1).ToString(
                        CultureInfo.InvariantCulture),
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                icon.hideFlags = HideFlags.DontSave;
                RectTransform rect = (RectTransform)icon.transform;
                rect.SetParent(rootRect, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                Image image = icon.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.color = Color.white;
                _reserveImages[index] = image;
                _displayedReserveFrames[index] = -1;
            }

            ApplyHudLayout();
            UpdateReserveVisuals();
            LogDiagnostic(
                "Attached three reserve diamonds beneath "
                + wyrdSkillBar.name
                + "; Glorious UI transforms and fades will be inherited.");
        }

        internal void DetachHud(VHeroHUD heroHud)
        {
            if (heroHud == null
                || ReferenceEquals(heroHud, _attachedHeroHud))
            {
                ReleaseHud();
            }
        }

        private void ReleaseHud()
        {
            if (_reserveRoot != null)
            {
                UnityEngine.Object.Destroy(_reserveRoot);
            }
            _reserveRoot = null;
            _attachedHeroHud = null;
            for (int index = 0; index < ReserveCount; index++)
            {
                _reserveImages[index] = null;
                _displayedReserveFrames[index] = -1;
            }
        }

        private void ApplyHudLayout()
        {
            if (_reserveRoot == null)
            {
                return;
            }

            RectTransform rootRect =
                (RectTransform)_reserveRoot.transform;
            rootRect.anchoredPosition = new Vector2(
                _hudOffsetX == null
                    ? DefaultHudOffsetX
                    : _hudOffsetX.Value,
                _hudOffsetY == null
                    ? DefaultHudOffsetY
                    : _hudOffsetY.Value);
            float scale = _hudScale == null
                ? DefaultHudScale
                : Mathf.Clamp(_hudScale.Value, 0.25f, 3.0f);
            rootRect.localScale = new Vector3(scale, scale, 1.0f);

            float iconSize = _iconSize == null
                ? DefaultIconSize
                : Mathf.Clamp(_iconSize.Value, 12.0f, 160.0f);
            for (int index = 0; index < ReserveCount; index++)
            {
                Image image = _reserveImages[index];
                if (image == null)
                {
                    continue;
                }

                RectTransform rect = image.rectTransform;
                rect.anchoredPosition = new Vector2(
                    FirstReserveX + (ReserveStepX * index),
                    FirstReserveY + (ReserveStepY * index));
                rect.sizeDelta = new Vector2(iconSize, iconSize);
            }
        }

        private void UpdateReserveVisuals()
        {
            if (_reserveRoot == null)
            {
                return;
            }

            _reserveRoot.SetActive(IsFeatureEnabled);
            if (!IsFeatureEnabled)
            {
                return;
            }

            float reserveFraction = GetReserveFraction();
            for (int index = 0; index < ReserveCount; index++)
            {
                float segmentFill = Mathf.Clamp01(
                    (reserveFraction * ReserveCount) - index);
                int frame = segmentFill >= 1.0f - Epsilon
                    ? ReserveSpriteCount - 1
                    : Mathf.Clamp(
                        Mathf.FloorToInt(
                            segmentFill * (ReserveSpriteCount - 1)),
                        0,
                        ReserveSpriteCount - 1);
                if (_reserveImages[index] != null
                    && _displayedReserveFrames[index] != frame)
                {
                    _reserveImages[index].sprite =
                        _reserveSprites[frame];
                    _displayedReserveFrames[index] = frame;
                }
            }
        }

        private void TrackCurrentHero(Hero hero)
        {
            _trackedHero = hero;
            if (hero == null)
            {
                return;
            }

            bool loadReady;
            bool hasData;
            Guid savedHeroId;
            float savedReserve;
            lock (_stateLock)
            {
                loadReady = _pendingLoadReady;
                hasData = _pendingLoadHasData;
                savedHeroId = _pendingLoadHeroId;
                savedReserve = _pendingLoadReserveFraction;
                _pendingLoadReady = false;
                _pendingLoadHasData = false;
            }

            if (!loadReady)
            {
                SetReserveFraction(0.0f);
                LogDiagnostic(
                    "Initialized a hero without pending Wyrdsoul Reserve save data; reserve starts empty.");
                return;
            }

            if (hasData && savedHeroId == hero.HeroID)
            {
                SetReserveFraction(savedReserve);
                LogDiagnostic(
                    "Restored reserve "
                    + FormatPercent(savedReserve)
                    + " for hero "
                    + hero.HeroID.ToString("D")
                    + ".");
            }
            else
            {
                SetReserveFraction(0.0f);
                if (hasData)
                {
                    Logger.LogWarning(
                        "Ignored Wyrdsoul Reserve save data because its hero ID did not match the loaded hero.");
                }
                else
                {
                    LogDiagnostic(
                        "The loaded save had no Wyrdsoul Reserve data; reserve starts empty.");
                }
            }
            BeginRechargeDelay();
        }

        internal void CaptureSaveSnapshot()
        {
            Hero hero = Hero.Current;
            if (hero == null)
            {
                return;
            }

            float reserve = GetReserveFraction();
            string json = "{\"schema\":"
                + SaveDataSchema.ToString(CultureInfo.InvariantCulture)
                + ",\"hero\":\""
                + hero.HeroID.ToString("D")
                + "\",\"reserve\":"
                + reserve.ToString("R", CultureInfo.InvariantCulture)
                + "}";
            lock (_stateLock)
            {
                _pendingSaveBytes = Encoding.UTF8.GetBytes(json);
            }
        }

        internal void WriteSaveSnapshotToActiveArchive()
        {
            byte[] data;
            lock (_stateLock)
            {
                data = _pendingSaveBytes;
            }
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                CloudService.Get.SaveSlotFile(
                    SaveDataFileName,
                    data);
                LogDiagnostic(
                    "Added "
                    + SaveDataFileName
                    + " to the active save archive.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not add Wyrdsoul Reserve state to the active save archive: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void BeginExpectedGameplaySlotLoad()
        {
            lock (_stateLock)
            {
                _expectedSlotLoadDepth++;
                _pendingLoadReady = false;
                _pendingLoadHasData = false;
                _pendingLoadHeroId = Guid.Empty;
                _pendingLoadReserveFraction = 0.0f;
            }
        }

        internal void FinishExpectedGameplaySlotLoad()
        {
            lock (_stateLock)
            {
                _expectedSlotLoadDepth = Math.Max(
                    0,
                    _expectedSlotLoadDepth - 1);
            }
        }

        internal void ReadSaveDataFromActiveArchive(
            CloudService cloudService)
        {
            lock (_stateLock)
            {
                if (_expectedSlotLoadDepth <= 0)
                {
                    return;
                }
            }

            bool hasData = false;
            Guid heroId = Guid.Empty;
            float reserve = 0.0f;
            try
            {
                byte[] data;
                if (cloudService != null
                    && cloudService.TryLoadSlotFile(
                        SaveDataFileName,
                        out data)
                    && data != null
                    && data.Length > 0)
                {
                    hasData = TryParseSaveData(
                        data,
                        out heroId,
                        out reserve);
                    if (!hasData)
                    {
                        Logger.LogWarning(
                            "The loaded save contained invalid or unsupported Wyrdsoul Reserve data; reserve will start empty.");
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not read Wyrdsoul Reserve state from the loaded save: "
                    + exception.GetBaseException().Message);
            }

            lock (_stateLock)
            {
                _pendingLoadReady = true;
                _pendingLoadHasData = hasData;
                _pendingLoadHeroId = heroId;
                _pendingLoadReserveFraction = reserve;
            }
        }

        private static bool TryParseSaveData(
            byte[] data,
            out Guid heroId,
            out float reserve)
        {
            heroId = Guid.Empty;
            reserve = 0.0f;
            string json = Encoding.UTF8.GetString(data);
            Match schemaMatch = Regex.Match(
                json,
                "\\\"schema\\\"\\s*:\\s*(?<value>[0-9]+)",
                RegexOptions.CultureInvariant);
            Match heroMatch = Regex.Match(
                json,
                "\\\"hero\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"",
                RegexOptions.CultureInvariant);
            Match reserveMatch = Regex.Match(
                json,
                "\\\"reserve\\\"\\s*:\\s*(?<value>[-+0-9.eE]+)",
                RegexOptions.CultureInvariant);
            int schema;
            float parsedReserve;
            if (!schemaMatch.Success
                || !heroMatch.Success
                || !reserveMatch.Success
                || !int.TryParse(
                    schemaMatch.Groups["value"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out schema)
                || schema != SaveDataSchema
                || !Guid.TryParse(
                    heroMatch.Groups["value"].Value,
                    out heroId)
                || !float.TryParse(
                    reserveMatch.Groups["value"].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsedReserve)
                || float.IsNaN(parsedReserve)
                || float.IsInfinity(parsedReserve))
            {
                heroId = Guid.Empty;
                return false;
            }

            reserve = Mathf.Clamp01(parsedReserve);
            return true;
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        private static string FormatCharge(
            float value,
            float upperLimit)
        {
            return value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "/"
                + upperLimit.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(float fraction)
        {
            return (fraction * 100.0f).ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "%";
        }

        private static class WyrdSkillActivationPatch
        {
            internal static void BeforeActivate()
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.ApplyActivationCost();
                }
            }

            internal static void BeforeDeactivate(out float __state)
            {
                WyrdsoulReservePlugin instance = Instance;
                __state = instance == null
                    ? -1.0f
                    : instance.CaptureChargeBeforeDeactivation();
            }

            internal static void AfterDeactivate(float __state)
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.FinishDeactivation(__state);
                }
            }

            internal static void AfterUpdate(
                WyrdSkillActivation __instance,
                float deltaTime)
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.ProcessRecharge(
                        __instance,
                        deltaTime);
                }
            }
        }

        private static class LimitedStatPatch
        {
            internal static void BeforeSetTo(
                LimitedStat __instance,
                float __0,
                out float __state)
            {
                __state = float.NaN;
                WyrdsoulReservePlugin instance = Instance;
                if (instance == null
                    || !CanSafelyInterceptWyrdDuration(__instance))
                {
                    return;
                }

                __state = __instance.ValueForSave;
                instance.CaptureOverflowBeforeClamp(
                    __instance,
                    __0);
            }

            internal static void AfterSetTo(
                LimitedStat __instance,
                float __state)
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null
                    && !float.IsNaN(__state)
                    && CanSafelyInterceptWyrdDuration(__instance))
                {
                    instance.ObserveExternalStatChange(
                        __instance,
                        __state);
                }
            }
        }

        private static class HeroHudPatch
        {
            internal static void AfterFullyInitialized(
                VHeroHUD __instance)
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.AttachHud(__instance);
                }
            }

            internal static void AfterDiscard(VHeroHUD __instance)
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.DetachHud(__instance);
                }
            }
        }

        private static class SavePatch
        {
            internal static void BeforeSave()
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.CaptureSaveSnapshot();
                }
            }

            internal static void AfterSaveDomains()
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.WriteSaveSnapshotToActiveArchive();
                }
            }

            internal static void BeforeGameplaySlotLoad()
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.BeginExpectedGameplaySlotLoad();
                }
            }

            internal static void AfterGameplaySlotLoad()
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.FinishExpectedGameplaySlotLoad();
                }
            }

            internal static void AfterCloudBeginLoad(
                CloudService __instance)
            {
                WyrdsoulReservePlugin instance = Instance;
                if (instance != null)
                {
                    instance.ReadSaveDataFromActiveArchive(__instance);
                }
            }
        }
    }
}
