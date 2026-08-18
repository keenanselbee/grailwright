using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

[assembly: AssemblyTitle("Soul and Service - Summon Overhaul")]
[assembly: AssemblyDescription("A focused overhaul of hero summons and Soul Salvage")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Soul and Service - Summon Overhaul")]
[assembly: AssemblyVersion("0.3.5.0")]
[assembly: AssemblyFileVersion("0.3.5.0")]
[assembly: AssemblyInformationalVersion("0.3.5")]

namespace SoulAndService
{
    public enum PlayerAttackPassThroughMode
    {
        Vanilla,
        MagicOnly,
        AllProjectiles
    }

    public enum SoulSalvageReturnMode
    {
        Split,
        Mana,
        Health
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("kane.tgfoa.avalon-summons")]
    [BepInIncompatibility("com.user.bettersummon")]
    [BepInIncompatibility("ks.tgfoa.summon-pass-through-test")]
    public sealed class SoulAndServicePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.soul-and-service";
        public const string PluginName = "Soul and Service - Summon Overhaul";
        public const string PluginVersion = "0.3.5";

        private const int ConfigSchemaVersion = 2;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[]
            ConfigRecoveryPermanentExclusions =
                new ConfigDefinition[0];

        internal static SoulAndServicePlugin Instance { get; private set; }

        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedConfigValues =
                new Dictionary<ConfigDefinition, object>();
        private readonly Dictionary<string, int> _configSettingOrders =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal ConfigEntry<bool> FeatureEnabled;
        internal ConfigEntry<float> AiTickInterval;
        internal ConfigEntry<float> SpawnRecoverySeconds;
        internal ConfigEntry<float> TrotDistance;
        internal ConfigEntry<float> RunDistance;
        internal ConfigEntry<float> TeleportDistance;
        internal ConfigEntry<float> CatchUpSpeedMultiplier;
        internal ConfigEntry<bool> ShareHeroTarget;
        internal ConfigEntry<float> ShareTargetMaxDistance;
        internal ConfigEntry<bool> SummonPassThrough;
        internal ConfigEntry<PlayerAttackPassThroughMode> PlayerAttackPassThrough;
        internal ConfigEntry<bool> PreventDismissOnRest;
        internal ConfigEntry<int> SummonLimitBonus;
        internal ConfigEntry<bool> RepairInvocationScaling;
        internal ConfigEntry<float> IdleSoundVolumePercent;
        internal ConfigEntry<bool> SoulSalvageOverhaul;
        internal ConfigEntry<SoulSalvageReturnMode> SoulSalvageReturn;
        internal ConfigEntry<float> SoulSalvageEssencePercent;
        internal ConfigEntry<float> ReanimationDurationSeconds;
        internal ConfigEntry<float> ReanimationHealthPercent;
        internal ConfigEntry<bool> PermanentReanimations;
        internal ConfigEntry<bool> Diagnostics;

        private Harmony _harmony;

        internal bool IsEnabled => FeatureEnabled != null && FeatureEnabled.Value;

        private void Awake()
        {
            Instance = this;
            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
                _harmony = new Harmony(PluginGuid);
                SummonRuntime.Patch(_harmony);
                SoulSalvageRuntime.Patch(_harmony);
                Logger.LogInfo(
                    PluginName + " " + PluginVersion
                    + " loaded. Player collision pass-through="
                    + SummonPassThrough.Value
                    + "; attack pass-through="
                    + PlayerAttackPassThrough.Value
                    + "; permanent reanimations="
                    + PermanentReanimations.Value
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed during startup: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
            }
        }

        private void Update()
        {
            SummonRuntime.Update();
            SoulSalvageRuntime.Update();
        }

        private void OnDestroy()
        {
            SoulSalvageRuntime.Shutdown();
            SummonRuntime.Shutdown();
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

        internal void LogDiagnostic(string message)
        {
            if (Diagnostics != null && Diagnostics.Value)
            {
                Logger.LogInfo(message);
            }
        }

        internal void LogWarning(string message)
        {
            Logger.LogWarning(message);
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
                case "Core":
                    return 0;
                case "Soul Salvage":
                    return 10;
                case "Following":
                    return 20;
                case "Targeting":
                    return 30;
                case "Collision":
                    return 40;
                case "Persistence":
                    return 50;
                case "Balance":
                    return 60;
                case "Responsiveness":
                    return 70;
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
            _configSettingOrders.Clear();
            BindOrdered(
                "Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Internal config schema marker. Do not edit this value.",
                    null,
                    new BrowsableAttribute(false)));
            FeatureEnabled = BindOrdered(
                "Core",
                "Enabled",
                true,
                "Master switch for every Soul and Service feature.");

            AiTickInterval = BindOrdered(
                "Responsiveness",
                "AITickInterval",
                0.25f,
                new ConfigDescription(
                    "Seconds between hero-summon AI decisions. Lower values react faster but cost more CPU.",
                    new AcceptableValueRange<float>(0.05f, 2.5f)));
            SpawnRecoverySeconds = BindOrdered(
                "Responsiveness",
                "SpawnRecoverySeconds",
                0.10f,
                new ConfigDescription(
                    "Seconds a newly created summon remains movement-locked.",
                    new AcceptableValueRange<float>(0.0f, 1.5f)));

            TrotDistance = BindOrdered(
                "Following",
                "TrotDistance",
                4.0f,
                new ConfigDescription(
                    "Distance in meters at which an idle summon starts trotting toward the hero.",
                    new AcceptableValueRange<float>(1.0f, 30.0f)));
            RunDistance = BindOrdered(
                "Following",
                "RunDistance",
                8.0f,
                new ConfigDescription(
                    "Distance in meters at which an idle summon starts running toward the hero.",
                    new AcceptableValueRange<float>(2.0f, 45.0f)));
            TeleportDistance = BindOrdered(
                "Following",
                "TeleportDistance",
                35.0f,
                new ConfigDescription(
                    "Distance in meters at which a summon uses the native safe teleport-to-ally route.",
                    new AcceptableValueRange<float>(10.0f, 100.0f)));
            CatchUpSpeedMultiplier = BindOrdered(
                "Following",
                "CatchUpSpeedMultiplier",
                1.25f,
                new ConfigDescription(
                    "Movement-speed multiplier used only while an out-of-combat summon is catching up.",
                    new AcceptableValueRange<float>(1.0f, 2.0f)));

            ShareHeroTarget = BindOrdered(
                "Targeting",
                "ShareHeroTarget",
                true,
                "Let an uncommitted summon adopt a hostile NPC under the hero's crosshair. Native attacker sharing remains intact.");
            ShareTargetMaxDistance = BindOrdered(
                "Targeting",
                "ShareTargetMaxDistance",
                30.0f,
                new ConfigDescription(
                    "Maximum summon-to-target distance for crosshair target sharing.",
                    new AcceptableValueRange<float>(5.0f, 60.0f)));

            SummonPassThrough = BindOrdered(
                "Collision",
                "Summon Pass-Through",
                true,
                "Ignore collision only between the hero's CharacterController and owned summon body colliders. Summons still collide with enemies and the world.");
            PlayerAttackPassThrough = BindOrdered(
                "Collision",
                "Player Attack Pass-Through",
                PlayerAttackPassThroughMode.MagicOnly,
                "Vanilla lets summons intercept attacks, MagicOnly lets confirmed magic-projectile and magic-gauntlet contacts pass through, and AllProjectiles also covers hero arrows and thrown projectiles. Bespoke scripted ray spells retain their native behavior.");

            PreventDismissOnRest = BindOrdered(
                "Persistence",
                "PreventDismissOnRest",
                true,
                "Keep ordinary hero summons when the hero rests.");
            SummonLimitBonus = BindOrdered(
                "Persistence",
                "SummonLimitBonus",
                0,
                new ConfigDescription(
                    "Flat bonus to the native active-summon limit.",
                    new AcceptableValueRange<int>(0, 20)));

            RepairInvocationScaling = BindOrdered(
                "Balance",
                "RepairInvocationOfMightScaling",
                true,
                "Repair replacement-summon Invocation of Might scaling only after the outgoing summon proves the native effect is active. Already-scaled stats are left unchanged.");
            IdleSoundVolumePercent = BindOrdered(
                "Balance",
                "IdleSoundVolumePercent",
                60.0f,
                new ConfigDescription(
                    "Volume of owned summons' idle loop. This does not scale attack, hurt, or death sounds.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));

            SoulSalvageOverhaul = BindOrdered(
                "Soul Salvage",
                "EnableSoulSalvageOverhaul",
                true,
                "Repurpose Soul Salvage: light cast sacrifices an owned summon for essence; heavy cast raises an eligible hostile corpse as a temporary servant.");
            SoulSalvageReturn = BindOrdered(
                "Soul Salvage",
                "LightCastReturn",
                SoulSalvageReturnMode.Split,
                "Choose whether light-cast essence restores mana, health, or an even split of both.");
            SoulSalvageEssencePercent = BindOrdered(
                "Soul Salvage",
                "LightCastEssencePercent",
                50.0f,
                new ConfigDescription(
                    "Percent of the summon's original mana investment returned at full health; current health scales the result.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)));
            ReanimationDurationSeconds = BindOrdered(
                "Soul Salvage",
                "ReanimationDurationSeconds",
                120.0f,
                new ConfigDescription(
                    "Lifetime of a raised servant when PermanentReanimations is disabled.",
                    new AcceptableValueRange<float>(15.0f, 900.0f)));
            ReanimationHealthPercent = BindOrdered(
                "Soul Salvage",
                "ReanimationHealthPercent",
                50.0f,
                new ConfigDescription(
                    "Percent of maximum health with which a raised servant begins.",
                    new AcceptableValueRange<float>(10.0f, 100.0f)));
            PermanentReanimations = BindOrdered(
                "Soul Salvage",
                "PermanentReanimations",
                false,
                "Remove the servant duration for the current play session. Raised servants remain restricted, unsaved runtime copies and never replace the source corpse model.");

            Diagnostics = BindOrdered(
                "Diagnostics",
                "Diagnostics",
                false,
                "Log summon lifecycle, collision, target sharing, scaling repair, and Soul Salvage decisions.");

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
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
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
                if ((string.Equals(currentSection, "Core", StringComparison.Ordinal)
                        || string.Equals(currentSection, "1. Core", StringComparison.Ordinal))
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

            CapturePreservedConfigValues(configPath, storedSchemaVersion);
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
                    + backupPath + ".");
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
                        "Could not restore the previous Soul and Service config after a failed schema reset: "
                        + restoreException.Message);
                }
                throw new InvalidOperationException(
                    "Failed to reset the Soul and Service config schema. The original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedConfigValues(string configPath, int storedSchemaVersion)
        {
            _pendingPreservedConfigValues.Clear();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery.ReadCustomizationProfile(
                    configPath,
                    storedSchemaVersion,
                    ConfigSchemaVersion,
                    ConfigRecoveryKeepCurrentDefaultRules,
                    ConfigRecoveryPermanentExclusions);

            CapturePreservedValue<bool>(profile, "Core", "Enabled");
            CapturePreservedValue<float>(profile, "Responsiveness", "AITickInterval");
            CapturePreservedValue<float>(profile, "Responsiveness", "SpawnRecoverySeconds");
            CapturePreservedValue<float>(profile, "Following", "TrotDistance");
            CapturePreservedValue<float>(profile, "Following", "RunDistance");
            CapturePreservedValue<float>(profile, "Following", "TeleportDistance");
            CapturePreservedValue<float>(profile, "Following", "CatchUpSpeedMultiplier");
            CapturePreservedValue<bool>(profile, "Targeting", "ShareHeroTarget");
            CapturePreservedValue<float>(profile, "Targeting", "ShareTargetMaxDistance");
            CapturePreservedValue<bool>(profile, "Collision", "Summon Pass-Through");
            CapturePreservedValue<PlayerAttackPassThroughMode>(profile, "Collision", "Player Attack Pass-Through");
            CapturePreservedValue<bool>(profile, "Persistence", "PreventDismissOnRest");
            CapturePreservedValue<int>(profile, "Persistence", "SummonLimitBonus");
            CapturePreservedValue<bool>(profile, "Balance", "RepairInvocationOfMightScaling");
            CapturePreservedValue<float>(profile, "Balance", "IdleSoundVolumePercent");
            CapturePreservedValue<bool>(profile, "Soul Salvage", "EnableSoulSalvageOverhaul");
            CapturePreservedValue<SoulSalvageReturnMode>(profile, "Soul Salvage", "LightCastReturn");
            CapturePreservedValue<float>(profile, "Soul Salvage", "LightCastEssencePercent");
            CapturePreservedValue<float>(profile, "Soul Salvage", "ReanimationDurationSeconds");
            CapturePreservedValue<float>(profile, "Soul Salvage", "ReanimationHealthPercent");
            CapturePreservedValue<bool>(profile, "Soul Salvage", "PermanentReanimations");
            CapturePreservedValue<bool>(profile, "Diagnostics", "Diagnostics");
        }

        private void CapturePreservedValue<T>(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            string section,
            string key)
        {
            T previousValue;
            if (profile.TryGetCustomizedValue(section, key, out previousValue))
            {
                _pendingPreservedConfigValues[new ConfigDefinition(section, key)] = previousValue;
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
            RestorePreservedValue(FeatureEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(AiTickInterval, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SpawnRecoverySeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(TrotDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(RunDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(TeleportDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(CatchUpSpeedMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ShareHeroTarget, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ShareTargetMaxDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SummonPassThrough, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(PlayerAttackPassThrough, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(PreventDismissOnRest, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SummonLimitBonus, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(RepairInvocationScaling, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(IdleSoundVolumePercent, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageOverhaul, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageReturn, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageEssencePercent, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationDurationSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationHealthPercent, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(PermanentReanimations, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(Diagnostics, ref restored, ref clamped, ref invalid);
            Logger.LogInfo(
                "Preserved " + restored.ToString(CultureInfo.InvariantCulture)
                + " Soul and Service setting(s) across the config schema reset; clamped="
                + clamped.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + invalid.ToString(CultureInfo.InvariantCulture) + ".");
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
                || !_pendingPreservedConfigValues.TryGetValue(entry.Definition, out boxedValue)
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
    }
}
