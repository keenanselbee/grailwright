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
[assembly: AssemblyDescription("A focused overhaul of hero summons and Soul Rend")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Soul and Service - Summon Overhaul")]
[assembly: AssemblyVersion("3.3.7.0")]
[assembly: AssemblyFileVersion("3.3.7.0")]
[assembly: AssemblyInformationalVersion("3.3.7")]

namespace SoulAndService
{
    public enum PlayerAttackPassThroughMode
    {
        Vanilla,
        MagicOnly,
        AllProjectiles,
        CombatOnly
    }

    public enum SummonBehavior
    {
        Guard = 0,
        Bulwark = 1,
        Hunt = 2
    }

    public enum SoulSalvageFocusedTargetState
    {
        None = 0,
        Corpse = 1,
        ActiveSummon = 2
    }

    public enum HeavySoulRendHoverState
    {
        None = 0,
        Reanimate = 1,
        RequiresSoulVigor = 2,
        ClaimSoul = 3,
        RestoreServant = 4,
        EmpowerServant = 5,
        ServantFullyRestored = 6
    }

    public enum SummonCommandState
    {
        None = 0,
        Attack = 1,
        Hold = 2,
        Follow = 3,
        Behavior = 4,
        RaiseAll = 5
    }

    public enum TargetCommandModifierMode
    {
        Sprint,
        None
    }

    public enum RestHostBehavior
    {
        Sustain,
        Dismiss
    }

    public enum BalanceProfile
    {
        SoulFamine,
        GravePact,
        Dominion,
        Custom
    }

    internal readonly struct SoulBalanceTuning
    {
        internal SoulBalanceTuning(
            float soulVigorRewardMultiplier,
            float servantUpkeepMultiplier,
            float raisedStartingHealthMultiplier,
            float soulClaimThresholdAdjustment)
        {
            SoulVigorRewardMultiplier = soulVigorRewardMultiplier;
            ServantUpkeepMultiplier = servantUpkeepMultiplier;
            RaisedStartingHealthMultiplier = raisedStartingHealthMultiplier;
            SoulClaimThresholdAdjustment = soulClaimThresholdAdjustment;
        }

        internal float SoulVigorRewardMultiplier { get; }
        internal float ServantUpkeepMultiplier { get; }
        internal float RaisedStartingHealthMultiplier { get; }
        internal float SoulClaimThresholdAdjustment { get; }
    }

    public enum SoulforgedRankOverride
    {
        Disabled = -1,
        Unranked = 0,
        I = 1,
        II = 2,
        III = 3,
        IV = 4,
        V = 5,
        VI = 6,
        VII = 7,
        VIII = 8,
        IX = 9,
        X = 10,
        XI = 11,
        XII = 12,
        XIII = 13,
        XIV = 14,
        XV = 15,
        XVI = 16,
        XVII = 17
    }

    public enum SoulforgedPromotionTarget
    {
        None = 0,
        I = 1,
        II = 2,
        III = 3,
        IV = 4,
        V = 5,
        VI = 6,
        VII = 7,
        VIII = 8,
        IX = 9,
        X = 10,
        XI = 11,
        XII = 12,
        XIII = 13,
        XIV = 14,
        XV = 15,
        XVI = 16,
        XVII = 17
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "ks.tgfoa.deeds-of-avalon",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "ks.tgfoa.steel-and-bone",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "ks.tgfoa.versatile-weapons",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "ks.tgfoa.battlecry-voice-tuner",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "ks.tgfoa.first-person-arms-adjuster",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "ks.tgfoa.eyes-in-the-dark",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("kane.tgfoa.avalon-summons")]
    [BepInIncompatibility("com.user.bettersummon")]
    [BepInIncompatibility("ks.tgfoa.summon-pass-through-test")]
    public sealed class SoulAndServicePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.soul-and-service";
        public const string PluginName = "Soul and Service";
        public const string PluginVersion = "3.3.7";

        private const int ConfigSchemaVersion = 30;
        private const float GravePactSoulVigorRewardBaseline = 1.25f;
        private const float GravePactServantUpkeepBaseline = 0.75f;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new[]
                {
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        20,
                        "Reanimation VFX",
                        "AuraIntensity",
                        "Aura Intensity now controls brightness only; electricity and smoke opacity are independent settings."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        23,
                        "Persistence",
                        "PersistentServants",
                        "Persistent Servants now controls save-and-load continuity and defaults on; rest behavior is configured separately."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        26,
                        "Reanimation VFX",
                        "AuraIntensity",
                        "Aura Intensity now means base brightness; Full Potential Brightness controls the progression endpoint."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        30,
                        "Custom Balance",
                        "SoulVigorRewardMultiplier",
                        "Soul Vigor Reward Multiplier is now relative to the fixed Grave Pact x1.25 reward baseline."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        30,
                        "Custom Balance",
                        "ServantUpkeepMultiplier",
                        "Servant Upkeep Multiplier is now relative to the fixed Grave Pact x0.75 upkeep baseline.")
                };
        private static readonly ConfigDefinition[]
            ConfigRecoveryPermanentExclusions =
            {
                new ConfigDefinition("Diagnostics", "OverrideSoulVigor"),
                new ConfigDefinition("Diagnostics", "SoulVigorOverrideValue"),
                new ConfigDefinition("Diagnostics", "OverrideSoulforgedRank"),
                new ConfigDefinition("Diagnostics", "PromoteActiveSummonsToRealRank")
            };

        internal static SoulAndServicePlugin Instance { get; private set; }

        private readonly Dictionary<ConfigDefinition, object>
            _pendingPreservedConfigValues =
                new Dictionary<ConfigDefinition, object>();
        private readonly Dictionary<string, int> _configSettingOrders =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _applyingBalancePreset;
        private bool _foaModManagerRefreshPending;
        private bool _resettingSoulforgedPromotionAction;

        internal ConfigEntry<bool> FeatureEnabled;
        internal ConfigEntry<BalanceProfile> BalanceProfileSetting;
        internal ConfigEntry<float> CustomSoulVigorRewardMultiplier;
        internal ConfigEntry<float> CustomServantUpkeepMultiplier;
        internal ConfigEntry<float> CustomRaisedStartingHealthMultiplier;
        internal ConfigEntry<float> CustomSoulClaimThresholdAdjustment;
        internal ConfigEntry<float> AiTickInterval;
        internal ConfigEntry<float> SpawnRecoverySeconds;
        internal ConfigEntry<float> TrotDistance;
        internal ConfigEntry<float> RunDistance;
        internal ConfigEntry<float> TeleportDistance;
        internal ConfigEntry<float> CatchUpSpeedMultiplier;
        internal ConfigEntry<float> IdleMovementAmount;
        internal ConfigEntry<bool> ShareHeroTarget;
        internal ConfigEntry<bool> AttackCommandPrompt;
        internal ConfigEntry<bool> FormationCommands;
        internal ConfigEntry<bool> HoldIndividualFormationCommands;
        internal ConfigEntry<bool> DirectedHuntEnabled;
        internal ConfigEntry<bool> ShowDirectedHuntPreview;
        internal ConfigEntry<bool> BulwarkAdvanceEnabled;
        internal ConfigEntry<float> BulwarkAdvanceReleaseSeconds;
        internal ConfigEntry<float> BulwarkAdvanceSpeedMultiplier;
        internal ConfigEntry<float> GuardFormationDistance;
        internal ConfigEntry<float> GuardEngagementRange;
        internal ConfigEntry<float> HuntFormationDistance;
        internal ConfigEntry<float> BulwarkCloseGuardDistance;
        internal ConfigEntry<float> BulwarkAdvanceDistance;
        internal ConfigEntry<float> BulwarkLocalEngagementRange;
        internal ConfigEntry<float> BulwarkTargetRetentionRange;
        internal ConfigEntry<float> BulwarkPlayerLeash;
        internal ConfigEntry<TargetCommandModifierMode> TargetCommandModifier;
        internal ConfigEntry<float> ShareTargetMaxDistance;
        internal ConfigEntry<bool> SummonPassThrough;
        internal ConfigEntry<PlayerAttackPassThroughMode> PlayerAttackPassThrough;
        internal ConfigEntry<bool> PersistentServants;
        internal ConfigEntry<RestHostBehavior> RestBehavior;
        internal ConfigEntry<int> SummonLimitBonus;
        internal ConfigEntry<bool> RepairInvocationScaling;
        internal ConfigEntry<float> IdleSoundVolumePercent;
        internal ConfigEntry<bool> PlaySoulSalvageAudio;
        internal ConfigEntry<float> SoulSalvageAudioVolume;
        internal ConfigEntry<float> SoulSalvageAudioRangeVolume;
        internal ConfigEntry<bool> AvoidRecentSoulSalvageAudioRepeats;
        internal ConfigEntry<int> RecentSoulSalvageAudioMemory;
        internal ConfigEntry<float> SoulSalvageAudioRandomPitchSemitones;
        internal ConfigEntry<float> FemaleSoulSalvageAudioPitchSemitones;
        internal ConfigEntry<float> MaleSoulSalvageAudioPitchSemitones;
        internal ConfigEntry<float> FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones;
        internal ConfigEntry<float> MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones;
        internal ConfigEntry<float> NonHumanoidSoulSalvageAudioPitchSemitones;
        internal ConfigEntry<float> SoulSalvageAudioEchoAmount;
        internal ConfigEntry<bool> PlaySoulRendImpactAudio;
        internal ConfigEntry<float> SoulRendImpactAudioVolume;
        internal ConfigEntry<bool> SoulSalvageOverhaul;
        internal ConfigEntry<bool> LivingTargetSoulSalvage;
        internal ConfigEntry<bool> SoulRendInnerLightEnabled;
        internal ConfigEntry<float> SoulRendInnerLightIntensity;
        internal ConfigEntry<float> SoulRendInnerLightIntensityMultiplier;
        internal ConfigEntry<float> SoulRendInnerLightInteriorIntensityMultiplier;
        internal ConfigEntry<float> SoulRendInnerLightMinimumPowerBrightnessMultiplier;
        internal ConfigEntry<float> SoulRendInnerLightMasteryBrightnessMultiplier;
        internal ConfigEntry<float> SoulRendInnerLightMaximumPowerBrightnessMultiplier;
        internal ConfigEntry<float> SoulRendInnerLightMinimumPowerRange;
        internal ConfigEntry<float> SoulRendInnerLightMasteryRange;
        internal ConfigEntry<float> SoulRendInnerLightMaximumPowerRange;
        internal ConfigEntry<float> SoulRendInnerLightFadeSeconds;
        internal ConfigEntry<bool> ReanimationVfxEnabled;
        internal ConfigEntry<string> ReanimationAuraArcColor;
        internal ConfigEntry<string> ReanimationAuraGlowColor;
        internal ConfigEntry<string> ReanimationAuraHazeColor;
        internal ConfigEntry<bool> ReanimationUseCustomFullPotentialColor;
        internal ConfigEntry<string> ReanimationFullPotentialColor;
        internal ConfigEntry<int> ReanimationAuraParticleAmount;
        internal ConfigEntry<float> ReanimationAuraIntensity;
        internal ConfigEntry<float> ReanimationFullPotentialBrightness;
        internal ConfigEntry<float> ReanimationElectricityOpacity;
        internal ConfigEntry<float> ReanimationSmokeOpacity;
        internal ConfigEntry<float> ReanimationAuraScale;
        internal ConfigEntry<bool> ReanimationDynamicParticleBudget;
        internal ConfigEntry<bool> Diagnostics;
        internal ConfigEntry<bool> ShowGrailFloatingTextDiagnostics;
        internal ConfigEntry<bool> OverrideSoulVigor;
        internal ConfigEntry<float> SoulVigorOverrideValue;
        internal ConfigEntry<SoulforgedRankOverride> OverrideSoulforgedRank;
        internal ConfigEntry<SoulforgedPromotionTarget> PromoteActiveSummonsToRealRank;

        private Harmony _harmony;

        internal bool IsEnabled => FeatureEnabled != null && FeatureEnabled.Value;

        internal static SoulBalanceTuning GetEffectiveBalanceTuning()
        {
            SoulBalanceTuning relative = GetConfiguredBalanceTuning();
            return new SoulBalanceTuning(
                GravePactSoulVigorRewardBaseline
                    * relative.SoulVigorRewardMultiplier,
                GravePactServantUpkeepBaseline
                    * relative.ServantUpkeepMultiplier,
                relative.RaisedStartingHealthMultiplier,
                relative.SoulClaimThresholdAdjustment);
        }

        private static SoulBalanceTuning GetConfiguredBalanceTuning()
        {
            SoulAndServicePlugin plugin = Instance;
            return plugin == null
                ? GetPresetBalanceTuning(BalanceProfile.GravePact)
                : new SoulBalanceTuning(
                    plugin.CustomSoulVigorRewardMultiplier == null
                        ? 1.00f
                        : plugin.CustomSoulVigorRewardMultiplier.Value,
                    plugin.CustomServantUpkeepMultiplier == null
                        ? 1.00f
                        : plugin.CustomServantUpkeepMultiplier.Value,
                    plugin.CustomRaisedStartingHealthMultiplier == null
                        ? 1.00f
                        : plugin.CustomRaisedStartingHealthMultiplier.Value,
                    plugin.CustomSoulClaimThresholdAdjustment == null
                        ? 0.00f
                        : plugin.CustomSoulClaimThresholdAdjustment.Value);
        }

        private static SoulBalanceTuning GetPresetBalanceTuning(
            BalanceProfile profile)
        {
            switch (profile)
            {
                case BalanceProfile.GravePact:
                    return new SoulBalanceTuning(1.00f, 1.00f, 1.00f, 0.00f);
                case BalanceProfile.Dominion:
                    return new SoulBalanceTuning(1.20f, 0.80f, 1.15f, 5.00f);
                case BalanceProfile.SoulFamine:
                default:
                    return new SoulBalanceTuning(0.80f, 1.20f, 0.85f, -5.00f);
            }
        }

        private string GetBalanceSummary()
        {
            SoulBalanceTuning tuning = GetConfiguredBalanceTuning();
            BalanceProfile profile = BalanceProfileSetting == null
                ? BalanceProfile.GravePact
                : BalanceProfileSetting.Value;
            return profile
                + " (reward x"
                + tuning.SoulVigorRewardMultiplier.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + ", upkeep x"
                + tuning.ServantUpkeepMultiplier.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + ", starting Health x"
                + tuning.RaisedStartingHealthMultiplier.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + ", Claim "
                + tuning.SoulClaimThresholdAdjustment.ToString(
                    "+0.##;-0.##;0",
                    CultureInfo.InvariantCulture)
                + "%"
                + ")";
        }

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
                    + "; persistent servants="
                    + PersistentServants.Value
                    + "; balance="
                    + GetBalanceSummary()
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
            RefreshFoaModManagerIfPending();
            SoulProgressionRuntime.Update();
            SoulforgedRuntime.Update();
            SummonRuntime.Update();
            SoulSalvageRuntime.Update();
            SoulRendInnerLightRuntime.Update();
            SoulSalvageAudioRuntime.Update();
        }

        private void LateUpdate()
        {
            SoulRendInnerLightRuntime.LateUpdate();
        }

        private void OnDestroy()
        {
            UnbindBalancePresetEvents();
            SoulSalvageRuntime.Shutdown();
            SoulforgedRuntime.Shutdown();
            SoulRendInnerLightRuntime.Shutdown();
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

        internal void ShowSoulSalvageHeavyCastDiagnostic(
            string diagnosticGroup,
            string message)
        {
            if (Diagnostics == null
                || !Diagnostics.Value
                || ShowGrailFloatingTextDiagnostics == null
                || !ShowGrailFloatingTextDiagnostics.Value)
            {
                return;
            }

            string diagnosticId = "soul-rend-"
                + diagnosticGroup + "-diagnostic";
            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowDiagnosticNotification(
                    PluginGuid,
                    diagnosticId,
                    message,
                    diagnosticId);
        }

        internal void ShowSoulSalvageHeavyCastFeedback(
            string eventId,
            string message,
            bool warning = false)
        {
            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowEventNotification(
                    PluginGuid,
                    eventId,
                    message,
                    warning ? "Warning" : "Necrotic",
                    "Status",
                    "Normal",
                    eventId,
                    "necro",
                    "Short",
                    0.25f,
                    0.95f);
        }

        internal void LogWarning(string message)
        {
            Logger.LogWarning(message);
        }

        private ConfigEntry<T> BindOrdered<T>(
            string section,
            string key,
            T defaultValue,
            string description,
            string displayName = null,
            int? displayOrder = null)
        {
            return BindOrdered(
                section,
                key,
                defaultValue,
                new ConfigDescription(description),
                displayName,
                displayOrder);
        }

        private ConfigEntry<T> BindOrdered<T>(
            string section,
            string key,
            T defaultValue,
            ConfigDescription description,
            string displayName = null,
            int? displayOrder = null)
        {
            if (String.Equals(
                    key,
                    "ConfigSchemaVersion",
                    StringComparison.Ordinal))
            {
                return base.Config.Bind(section, key, defaultValue, description);
            }

            string displaySection = GetConfigDisplaySection(section, key);
            int order;
            if (!_configSettingOrders.TryGetValue(displaySection, out order))
            {
                order = 0;
            }
            _configSettingOrders[displaySection] = order + 10;

            return base.Config.Bind(
                section,
                key,
                defaultValue,
                Grailwright.Shared.ConfigUiDescription.Create(
                    description.Description,
                    displaySection,
                    displayName ?? HumanizeConfigKey(key),
                    GetConfigSectionOrder(displaySection),
                    displayOrder ?? order,
                    description.AcceptableValues));
        }

        private static string GetConfigDisplaySection(string section, string key)
        {
            switch (section)
            {
                case "Core":
                    return string.Equals(
                            key,
                            "BalanceProfile",
                            StringComparison.Ordinal)
                        ? "Balance Preset"
                        : section;
                case "Soul Salvage":
                    return "Soul Rend";
                case "Persistence":
                    return "Host and Persistence";
                case "Targeting":
                    return "Commands and Targeting";
                case "Soul Rend Inner Light":
                    return "Soul Rend Hand Light";
                case "Responsiveness":
                    return "Advanced";
                case "Custom Balance":
                    return "Balance Preset";
                case "Balance":
                    return string.Equals(
                            key,
                            "IdleSoundVolumePercent",
                            StringComparison.Ordinal)
                        ? "Audio"
                        : "Advanced";
                default:
                    return section;
            }
        }

        private static int GetConfigSectionOrder(string displaySection)
        {
            switch (displaySection)
            {
                case "Core":
                    return 0;
                case "Balance Preset":
                    return 10;
                case "Soul Rend":
                    return 20;
                case "Host and Persistence":
                    return 30;
                case "Commands and Targeting":
                    return 40;
                case "Summon Behaviors":
                    return 50;
                case "Following":
                    return 60;
                case "Collision":
                    return 70;
                case "Reanimation VFX":
                    return 80;
                case "Soul Rend Hand Light":
                    return 90;
                case "Audio":
                    return 100;
                case "Advanced":
                    return 110;
                case "Diagnostics":
                    return Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder;
                default:
                    throw new InvalidOperationException(
                        "Missing config section order for " + displaySection + ".");
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
            BalanceProfileSetting = BindOrdered(
                "Core",
                "BalanceProfile",
                BalanceProfile.GravePact,
                "Apply a complete set of the balance values below. Soul Famine is demanding, Grave Pact is the intended balanced experience, Dominion favors an abundant necromancer power fantasy, and Custom preserves the current values.",
                "Balance Preset");

            AiTickInterval = BindOrdered(
                "Responsiveness",
                "AITickInterval",
                0.25f,
                new ConfigDescription(
                    "Seconds between hero-summon AI decisions at 100 Necromantic Power. At Power 0, decisions are no faster than 0.75 seconds and improve smoothly toward this interval as mastery grows. Lower values react faster but cost more CPU.",
                    new AcceptableValueRange<float>(0.05f, 2.5f)));
            SpawnRecoverySeconds = BindOrdered(
                "Responsiveness",
                "SpawnRecoverySeconds",
                0.10f,
                new ConfigDescription(
                    "Minimum seconds a newly created summon remains movement-locked. Movement releases once its native animation is ready.",
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
                    "Distance in meters at which an idle summon starts running toward the hero. Its effective value is never lower than Trot Distance.",
                    new AcceptableValueRange<float>(2.0f, 45.0f)));
            TeleportDistance = BindOrdered(
                "Following",
                "TeleportDistance",
                60.0f,
                new ConfigDescription(
                    "Distance in meters at which a summon uses the native safe teleport-to-ally route.",
                    new AcceptableValueRange<float>(10.0f, 100.0f)));
            CatchUpSpeedMultiplier = BindOrdered(
                "Following",
                "CatchUpSpeedMultiplier",
                1.25f,
                new ConfigDescription(
                    "Movement-speed multiplier used while an out-of-combat summon catches up or an untargeted Bulwark servant closes on its assigned shield-line position.",
                    new AcceptableValueRange<float>(1.0f, 2.0f)));
            IdleMovementAmount = BindOrdered(
                "Following",
                "IdleMovementAmount",
                1.0f,
                new ConfigDescription(
                    "Scales how far and how often Guard and Hunt servants naturally wander while the hero is stationary. Zero disables voluntary idle wandering without affecting combat, formation correction, following, or catch-up movement.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)),
                "Idle Movement Amount");

            ShareHeroTarget = BindOrdered(
                "Targeting",
                "ShareHeroTarget",
                false,
                "Let an uncommitted summon adopt a hostile NPC under the hero's crosshair. Off by default so looking at an enemy does not start a fight; native attacker sharing remains intact.",
                "Passive Crosshair Target Sharing",
                displayOrder: 50);
            AttackCommandPrompt = BindOrdered(
                "Targeting",
                "AttackCommandPrompt",
                true,
                "At 10 Necromantic Power (65 Soul Vigor), hold the configured command modifier while aiming at a nearby hostile NPC and press Interact to order every owned summon to attack it.",
                "Enable Attack Command",
                displayOrder: 10);
            FormationCommands = BindOrdered(
                "Targeting",
                "FormationCommands",
                true,
                "Enable Hold, Follow, group formation, behavior, Recall, and Raise All commands as they unlock through Necromantic Power. Directed Hunt and Bulwark Advance have their own controls in Summon Behaviors.",
                "Enable Host Commands",
                displayOrder: 20);
            HoldIndividualFormationCommands = BindOrdered(
                "Targeting",
                "HoldIndividualFormationCommands",
                false,
                "Require holding Interact for 0.45 seconds to issue an individual Hold or Follow command. Releasing early cancels the command. Attack, Directed Hunt, behavior cycling, Hold All, Follow All, and Recall keep their normal inputs.",
                "Hold Individual Formation Commands",
                displayOrder: 30);
            DirectedHuntEnabled = BindOrdered(
                "Summon Behaviors",
                "EnableDirectedHunt",
                true,
                "While Hunt is selected, hold the remappable Sprint action and tap Interact while aiming at reachable terrain at least 5 meters away. Idle, uncommitted hunters attack-move to the point, then attack any faction-hostile enemy within their normal Hunt awareness, line of sight, native leash, and reachable navigation area. Autonomous combatants may retarget under the same rules, while explicit Attack, Hold, and Recall remain protected. Every valid tap confirms with the Attack pulse and voice. Hold Interact for 0.45 seconds instead to cycle behavior.",
                "Enable Directed Hunt");
            ShowDirectedHuntPreview = BindOrdered(
                "Summon Behaviors",
                "ShowDirectedHuntPreview",
                false,
                "Show the Hunt interaction icon while Sprint is held over valid terrain. Disabled by default; Sprint and Interact still issue Directed Hunt without the preview.",
                "Show Directed Hunt Preview");
            BulwarkAdvanceEnabled = BindOrdered(
                "Summon Behaviors",
                "EnableBulwarkAdvance",
                true,
                "Let the remappable Sprint action switch Bulwark from Close Guard into its forward Advance wall. Disable this to keep Bulwark in Close Guard.",
                "Enable Bulwark Advance");
            BulwarkAdvanceReleaseSeconds = BindOrdered(
                "Summon Behaviors",
                "BulwarkAdvanceReleaseSeconds",
                0.0f,
                new ConfigDescription(
                    "Seconds Bulwark remains in Advance after Sprint is released before returning to Close Guard. The wall retains its last facing while continuing to follow the hero. Zero switches immediately.",
                    new AcceptableValueRange<float>(0.0f, 10.0f)),
                "Bulwark Advance Release Duration");
            BulwarkAdvanceSpeedMultiplier = BindOrdered(
                "Summon Behaviors",
                "BulwarkAdvanceSpeedMultiplier",
                2.0f,
                new ConfigDescription(
                    "Direct movement-speed multiplier while Bulwark Advance is active. Empower, Swarm, and displaced-slot catch-up share a safe 3x total movement ceiling.",
                    new AcceptableValueRange<float>(1.0f, 3.0f)),
                "Bulwark Advance Speed Multiplier");
            GuardFormationDistance = BindOrdered(
                "Summon Behaviors",
                "GuardFormationDistance",
                4.5f,
                new ConfigDescription(
                    "Base distance in meters from the hero to the Guard formation. Larger servants can expand the formation farther when physical spacing requires it.",
                    new AcceptableValueRange<float>(2.0f, 10.0f)),
                "Guard Formation Distance");
            GuardEngagementRange = BindOrdered(
                "Summon Behaviors",
                "GuardEngagementRange",
                15.0f,
                new ConfigDescription(
                    "Hero-centered distance in meters at which Guard servants proactively engage visible faction-hostile enemies. Guard retains those targets for 5 additional meters before returning to formation. Zero restores purely reactive Guard targeting.",
                    new AcceptableValueRange<float>(0.0f, 30.0f)),
                "Guard Engagement Range");
            HuntFormationDistance = BindOrdered(
                "Summon Behaviors",
                "HuntFormationDistance",
                5.5f,
                new ConfigDescription(
                    "Base distance in meters from the hero to the roaming Hunt formation. Directed Hunt destinations are configured separately by their command behavior.",
                    new AcceptableValueRange<float>(2.0f, 10.0f)),
                "Hunt Formation Distance");
            BulwarkCloseGuardDistance = BindOrdered(
                "Summon Behaviors",
                "BulwarkCloseGuardDistance",
                3.5f,
                new ConfigDescription(
                    "Base distance in meters from the hero to the defensive Close Guard wall.",
                    new AcceptableValueRange<float>(2.0f, 10.0f)),
                "Bulwark Close Guard Distance");
            BulwarkAdvanceDistance = BindOrdered(
                "Summon Behaviors",
                "BulwarkAdvanceDistance",
                4.5f,
                new ConfigDescription(
                    "Base distance in meters ahead of the hero for the advancing Bulwark wall.",
                    new AcceptableValueRange<float>(2.0f, 10.0f)),
                "Bulwark Advance Distance");
            BulwarkLocalEngagementRange = BindOrdered(
                "Summon Behaviors",
                "BulwarkLocalEngagementRange",
                4.0f,
                new ConfigDescription(
                    "Distance in meters at which an uncommitted Bulwark servant will engage a nearby hostile.",
                    new AcceptableValueRange<float>(1.0f, 12.0f)),
                "Bulwark Local Engagement Range");
            BulwarkTargetRetentionRange = BindOrdered(
                "Summon Behaviors",
                "BulwarkTargetRetentionRange",
                6.0f,
                new ConfigDescription(
                    "Distance in meters at which a Bulwark servant keeps fighting its current nearby hostile.",
                    new AcceptableValueRange<float>(1.0f, 16.0f)),
                "Bulwark Target Retention Range");
            BulwarkPlayerLeash = BindOrdered(
                "Summon Behaviors",
                "BulwarkPlayerLeash",
                8.0f,
                new ConfigDescription(
                    "Maximum servant-to-player distance in meters for autonomous Bulwark combat. Explicit Attack orders remain authoritative.",
                    new AcceptableValueRange<float>(3.0f, 20.0f)),
                "Bulwark Player Leash");
            TargetCommandModifier = BindOrdered(
                "Targeting",
                "TargetCommandModifier",
                TargetCommandModifierMode.Sprint,
                new ConfigDescription(
                    "Choose whether targeted Attack, Hold, and Follow prompts require the remappable Sprint action to be held. None keeps targeted command prompts visible without a modifier."),
                "Target Command Modifier",
                displayOrder: 0);
            ShareTargetMaxDistance = BindOrdered(
                "Targeting",
                "ShareTargetMaxDistance",
                45.0f,
                new ConfigDescription(
                    "Maximum hero-to-target distance for passive crosshair sharing and explicit Attack, Hold, and Follow commands, capped at the game's native 45 m summon-command tether. Attack acquires targets within a 44 m safety boundary and retains an existing order through 44.75 m with brief release grace.",
                    new AcceptableValueRange<float>(5.0f, 45.0f)),
                "Targeting Range",
                displayOrder: 40);

            SummonPassThrough = BindOrdered(
                "Collision",
                "Summon Pass-Through",
                true,
                "Ignore collision only between the hero's CharacterController and owned summon body colliders. Summons still collide with enemies and the world.");
            PlayerAttackPassThrough = BindOrdered(
                "Collision",
                "Player Attack Pass-Through",
                PlayerAttackPassThroughMode.CombatOnly,
                "CombatOnly lets confirmed hero projectiles and magic-gauntlet contacts pass through owned summons while the hero or summon is in combat, then restores vanilla interception outside combat. MagicOnly always passes confirmed magic contacts, AllProjectiles always passes confirmed arrows, thrown projectiles, and magic, and Vanilla always lets summons intercept. Bespoke scripted ray spells retain their native behavior.");

            PersistentServants = BindOrdered(
                "Persistence",
                "PersistentServants",
                true,
                "Keep ordinary summons and each raised servant's source identity, Health, Empowerment, investment, and Soulforged progress through saving, loading, and restarting the game.",
                "Persistent Servants");
            RestBehavior = BindOrdered(
                "Persistence",
                "RestHostBehavior",
                RestHostBehavior.Sustain,
                "Sustain keeps servants through rest but applies severe Power-scaled Health attrition for the actual hours rested. Dismiss uses the native safe dismissal and remains lifecycle.",
                "Rest Host Behavior");
            SummonLimitBonus = BindOrdered(
                "Persistence",
                "SummonLimitBonus",
                0,
                new ConfigDescription(
                    "Additional flat bonus beyond the native limit and the +1/+2/+3 Summon Capacity bonuses unlocked at Necromantic Power 50/100/150.",
                    new AcceptableValueRange<int>(0, 20)));

            RepairInvocationScaling = BindOrdered(
                "Balance",
                "RepairInvocationOfMightScaling",
                true,
                "Repair replacement-summon Invocations of Might scaling only when the hero has learned the native talent. Raised-servant quality scaling composes normally, and already-scaled stats are left unchanged.");
            IdleSoundVolumePercent = BindOrdered(
                "Balance",
                "IdleSoundVolumePercent",
                60.0f,
                new ConfigDescription(
                    "Volume of owned summons' idle loop. This does not scale attack, hurt, or death sounds.",
                    new AcceptableValueRange<float>(0.0f, 100.0f)),
                "Summon Idle Volume Percent",
                displayOrder: 50);

            PlaySoulSalvageAudio = BindOrdered(
                "Audio",
                "PlaySoulSalvageAudio",
                true,
                "Play a quality-matched FMOD WAV after light Soul Rend successfully harvests a corpse or sacrifices a summon.",
                "Play Soul Rend Ritual Audio",
                displayOrder: 0);
            SoulSalvageAudioVolume = BindOrdered(
                "Audio",
                "SoulSalvageAudioVolume",
                0.85f,
                new ConfigDescription(
                    "Global FMOD volume for Soul Rend ritual sounds. The authored loudness differences between quality tiers remain intact.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)),
                "Ritual Audio Volume",
                displayOrder: 10);
            SoulSalvageAudioRangeVolume = BindOrdered(
                "Audio",
                "SoulSalvageAudioRangeVolume",
                1.0f,
                new ConfigDescription(
                    "How strongly Soul Rend ritual and impact sounds fade with target distance. 0 disables distance fade; 1 uses the full 0m=100%, 30m+=10% curve.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)),
                "Distance Fade Strength",
                displayOrder: 40);
            AvoidRecentSoulSalvageAudioRepeats = BindOrdered(
                "Audio",
                "AvoidRecentSoulSalvageAudioRepeats",
                true,
                "Avoid replaying recently used Soul Rend sounds from the same quality tier when enough alternatives are available.",
                "Avoid Recent Soul Rend Audio Repeats",
                displayOrder: 60);
            RecentSoulSalvageAudioMemory = BindOrdered(
                "Audio",
                "RecentSoulSalvageAudioMemory",
                2,
                new ConfigDescription(
                    "How many recently played Soul Rend sounds to avoid per quality tier.",
                    new AcceptableValueRange<int>(0, 20)),
                "Recent Soul Rend Audio Memory",
                displayOrder: 70);
            SoulSalvageAudioRandomPitchSemitones = BindOrdered(
                "Audio",
                "SoulSalvageAudioRandomPitchSemitones",
                0.20f,
                new ConfigDescription(
                    "Random FMOD pitch variation in semitones. Zero disables it.",
                    new AcceptableValueRange<float>(0.0f, 12.0f)),
                "Soul Rend Audio Random Pitch Semitones",
                displayOrder: 90);
            FemaleSoulSalvageAudioPitchSemitones = BindOrdered(
                "Audio",
                "FemaleSoulSalvageAudioPitchSemitones",
                3.0f,
                new ConfigDescription(
                    "Pitch offset in semitones for Soul Rend targets whose runtime body is explicitly female.",
                    new AcceptableValueRange<float>(-12.0f, 12.0f)),
                "Female Soul Rend Audio Pitch Semitones",
                displayOrder: 100);
            MaleSoulSalvageAudioPitchSemitones = BindOrdered(
                "Audio",
                "MaleSoulSalvageAudioPitchSemitones",
                -3.0f,
                new ConfigDescription(
                    "Pitch offset in semitones for Soul Rend targets whose runtime body is explicitly male.",
                    new AcceptableValueRange<float>(-12.0f, 12.0f)),
                "Male Soul Rend Audio Pitch Semitones",
                displayOrder: 110);
            FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones = BindOrdered(
                "Audio",
                "FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones",
                -1.0f,
                new ConfigDescription(
                    "Additional pitch adjustment for clearly non-humanoid Soul Rend targets whose runtime body is explicitly female. The default combines with the female offset for a final +2 semitones.",
                    new AcceptableValueRange<float>(-12.0f, 12.0f)),
                "Female Monster Soul Rend Audio Pitch Adjustment Semitones",
                displayOrder: 120);
            MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones = BindOrdered(
                "Audio",
                "MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones",
                -3.0f,
                new ConfigDescription(
                    "Additional pitch adjustment for clearly non-humanoid Soul Rend targets whose runtime body is explicitly male. The default combines with the male offset for a final -6 semitones.",
                    new AcceptableValueRange<float>(-12.0f, 12.0f)),
                "Male Monster Soul Rend Audio Pitch Adjustment Semitones",
                displayOrder: 130);
            NonHumanoidSoulSalvageAudioPitchSemitones = BindOrdered(
                "Audio",
                "NonHumanoidSoulSalvageAudioPitchSemitones",
                -6.0f,
                new ConfigDescription(
                    "Fallback pitch offset in semitones for clearly non-humanoid Soul Rend targets whose runtime gender is unknown. Other unknown targets retain normal pitch.",
                    new AcceptableValueRange<float>(-12.0f, 0.0f)),
                "Non-Humanoid Soul Rend Audio Pitch Semitones",
                displayOrder: 140);
            SoulSalvageAudioEchoAmount = BindOrdered(
                "Audio",
                "SoulSalvageAudioEchoAmount",
                0.35f,
                new ConfigDescription(
                    "Strength of two quiet delayed echoes added to successful light Soul Rend ritual sounds. Zero disables the added echoes.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)),
                "Soul Rend Audio Echo Amount",
                displayOrder: 80);
            PlaySoulRendImpactAudio = BindOrdered(
                "Audio",
                "PlaySoulRendImpactAudio",
                true,
                "Play a short tactile impact when a valid light or heavy Soul Rend connects. Invalid and unaffordable casts remain silent.",
                "Play Soul Rend Impact Audio",
                displayOrder: 20);
            SoulRendImpactAudioVolume = BindOrdered(
                "Audio",
                "SoulRendImpactAudioVolume",
                0.8f,
                new ConfigDescription(
                    "Volume of successful light and heavy Soul Rend impact sounds.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)),
                "Impact Audio Volume",
                displayOrder: 30);

            SoulSalvageOverhaul = BindOrdered(
                "Soul Salvage",
                "EnableSoulSalvageOverhaul",
                true,
                "Enable Soul Rend: light cast costs 5 Mana and harvests almost every fresh non-summon corpse for quality-based Mana and Soul Vigor, simplifying only bodies that are safe to replace. Against owned servants it removes Empowerment, then two real Soulforged ranks per cast, before finally unbinding at rank 0 for an immediate quality-based return. Heavy cast binds and raises only eligible hostile corpses as servants. Living-target effects can be controlled separately below.",
                "Enable Soul Rend");
            LivingTargetSoulSalvage = BindOrdered(
                "Soul Salvage",
                "EnableLivingTargetSoulSalvage",
                true,
                "Let light cast deal Necrotic damage to eligible living hostiles. Each surviving hit raises that enemy's Soul Claim threshold by 2%, up to 10%; heavy cast claims at or below the final threshold. Protected NPCs and Soul Vigor awards remain unchanged.",
                "Enable Living-Target Soul Rend");
            SoulRendInnerLightEnabled = BindOrdered(
                "Soul Rend Inner Light",
                "Enabled",
                true,
                "Show a necromantic-green no-shadow light from each raised hand that has Soul Rend equipped.",
                "Enable Soul Rend Hand Light");
            SoulRendInnerLightIntensity = BindOrdered(
                "Soul Rend Inner Light",
                "Intensity",
                0.5f,
                new ConfigDescription(
                    "Base brightness of each green hand light while Soul Rend is readied. Actual casting temporarily triples that hand's final value after 0.3 seconds. Zero disables visible light without disabling the feature.",
                    new AcceptableValueRange<float>(0.0f, 8.0f)),
                "Base Hand-Light Intensity");
            SoulRendInnerLightIntensityMultiplier = BindOrdered(
                "Soul Rend Inner Light",
                "SoulRendIntensityMultiplier",
                0.8f,
                new ConfigDescription(
                    "Perceptual brightness multiplier for Soul Rend's saturated green light, applied after the shared base intensity. The default matches Blood Transfusion's restrained 0.8 multiplier.",
                    new AcceptableValueRange<float>(0.0f, 8.0f)));
            SoulRendInnerLightInteriorIntensityMultiplier = BindOrdered(
                "Soul Rend Inner Light",
                "InteriorIntensityMultiplier",
                1.0f,
                new ConfigDescription(
                    "Additional hand-light intensity multiplier in full interior scenes. One preserves the configured intensity and zero hides the light indoors.",
                    new AcceptableValueRange<float>(0.0f, 8.0f)));
            SoulRendInnerLightMinimumPowerBrightnessMultiplier = BindOrdered(
                "Soul Rend Inner Light",
                "MinimumPowerBrightnessMultiplier",
                0.2f,
                new ConfigDescription(
                    "Necromantic Power 0 brightness multiplier. The light grows smoothly from this faint starting point to the mastery milestone.",
                    new AcceptableValueRange<float>(0.0f, 8.0f)));
            SoulRendInnerLightMasteryBrightnessMultiplier = BindOrdered(
                "Soul Rend Inner Light",
                "MasteryBrightnessMultiplier",
                2.0f,
                new ConfigDescription(
                    "Necromantic Power 100 brightness multiplier.",
                    new AcceptableValueRange<float>(0.0f, 8.0f)));
            SoulRendInnerLightMaximumPowerBrightnessMultiplier = BindOrdered(
                "Soul Rend Inner Light",
                "MaximumPowerBrightnessMultiplier",
                3.0f,
                new ConfigDescription(
                    "Necromantic Power 200 brightness multiplier.",
                    new AcceptableValueRange<float>(0.0f, 8.0f)));
            SoulRendInnerLightMinimumPowerRange = BindOrdered(
                "Soul Rend Inner Light",
                "MinimumPowerRange",
                1.5f,
                new ConfigDescription(
                    "Necromantic Power 0 light range in meters.",
                    new AcceptableValueRange<float>(0.1f, 20.0f)));
            SoulRendInnerLightMasteryRange = BindOrdered(
                "Soul Rend Inner Light",
                "MasteryRange",
                3.0f,
                new ConfigDescription(
                    "Necromantic Power 100 light range in meters.",
                    new AcceptableValueRange<float>(0.1f, 20.0f)));
            SoulRendInnerLightMaximumPowerRange = BindOrdered(
                "Soul Rend Inner Light",
                "MaximumPowerRange",
                4.5f,
                new ConfigDescription(
                    "Necromantic Power 200 light range in meters.",
                    new AcceptableValueRange<float>(0.1f, 20.0f)));
            SoulRendInnerLightFadeSeconds = BindOrdered(
                "Soul Rend Inner Light",
                "FadeSeconds",
                0.12f,
                new ConfigDescription(
                    "Seconds used to fade the green hand lights in and out. Zero switches instantly.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            ReanimationVfxEnabled = BindOrdered(
                "Reanimation VFX",
                "Enabled",
                true,
                "Show the persistent body-bound electricity and smoke on reanimated servants.");
            ReanimationAuraArcColor = BindOrdered(
                "Reanimation VFX",
                "AuraArcColor",
                "#179B43",
                "Color of the reanimation electricity's arc layer. Use a hex color such as #179B43.",
                "Arc Color");
            ReanimationAuraGlowColor = BindOrdered(
                "Reanimation VFX",
                "AuraGlowColor",
                "#78C98F",
                "Color of the reanimation electricity's corpse-green core. Use a hex color such as #78C98F.",
                "Core Color");
            ReanimationAuraHazeColor = BindOrdered(
                "Reanimation VFX",
                "AuraHazeColor",
                "#123F2D",
                "Color of the reanimation effect's integrated smoke. Use a hex color such as #123F2D.",
                "Smoke Color");
            ReanimationUseCustomFullPotentialColor = BindOrdered(
                "Reanimation VFX",
                "UseCustomFullPotentialColor",
                false,
                "Use Full Potential Color as the progression endpoint instead of the default saturated emerald.",
                "Use Custom Full Potential Color");
            ReanimationFullPotentialColor = BindOrdered(
                "Reanimation VFX",
                "FullPotentialColor",
                "#FFFFFF",
                "Custom color approached as a servant gains Soulforged ranks and Empowerment when Use Custom Full Potential Color is enabled.",
                "Full Potential Color");
            ReanimationAuraParticleAmount = BindOrdered(
                "Reanimation VFX",
                "AuraParticleAmount",
                75,
                new ConfigDescription(
                    "Reanimation electricity and smoke particle amount as a percentage of the native effect. Zero disables the body effect.",
                    new AcceptableValueRange<int>(0, 200)),
                "Particle Amount");
            ReanimationAuraIntensity = BindOrdered(
                "Reanimation VFX",
                "AuraIntensity",
                5.0f,
                new ConfigDescription(
                    "Brightness of the reanimation electricity and smoke with no Soulforged rank or Empowerment. Opacity is controlled separately.",
                    new AcceptableValueRange<float>(0.0f, 20.0f)),
                "Base Brightness");
            ReanimationFullPotentialBrightness = BindOrdered(
                "Reanimation VFX",
                "FullPotentialBrightness",
                20.0f,
                new ConfigDescription(
                    "Brightness reached at maximum Soulforged rank with Empowerment.",
                    new AcceptableValueRange<float>(0.0f, 20.0f)),
                "Full Potential Brightness");
            ReanimationElectricityOpacity = BindOrdered(
                "Reanimation VFX",
                "ElectricityOpacity",
                1.0f,
                new ConfigDescription(
                    "Opacity of the reanimation electricity. Zero hides only the electrical layer.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)),
                "Electricity Opacity");
            ReanimationSmokeOpacity = BindOrdered(
                "Reanimation VFX",
                "SmokeOpacity",
                0.5f,
                new ConfigDescription(
                    "Opacity of the integrated reanimation smoke. Zero hides only the smoke layer.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)),
                "Smoke Opacity");
            ReanimationAuraScale = BindOrdered(
                "Reanimation VFX",
                "AuraScale",
                1.0f,
                new ConfigDescription(
                    "Size multiplier for the reanimation electricity and integrated smoke.",
                    new AcceptableValueRange<float>(0.25f, 2.0f)),
                "Scale");
            ReanimationDynamicParticleBudget = BindOrdered(
                "Reanimation VFX",
                "DynamicParticleBudget",
                true,
                "Automatically reduce per-servant reanimation electricity and smoke density as the active reanimated host grows. The full presentation is retained for small hosts.",
                "Dynamic Particle Budget");
            CustomSoulVigorRewardMultiplier = BindOrdered(
                "Custom Balance",
                "SoulVigorRewardMultiplier",
                1.00f,
                new ConfigDescription(
                    "Soul Vigor rewards relative to Grave Pact's x1.25 baseline. Changing it manually sets Balance Preset to Custom. Empowerment refunds are never multiplied.",
                    new AcceptableValueRange<float>(0.25f, 5.0f)),
                "Soul Vigor Rewards vs Grave Pact");
            CustomServantUpkeepMultiplier = BindOrdered(
                "Custom Balance",
                "ServantUpkeepMultiplier",
                1.00f,
                new ConfigDescription(
                    "Active and rest Health upkeep relative to Grave Pact's x0.75 baseline, including miniboss upkeep. Changing it manually sets Balance Preset to Custom. Zero disables upkeep.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)),
                "Servant Upkeep vs Grave Pact");
            CustomRaisedStartingHealthMultiplier = BindOrdered(
                "Custom Balance",
                "RaisedStartingHealthMultiplier",
                1.00f,
                new ConfigDescription(
                    "Current multiplier for the starting Health fraction rolled by newly raised ordinary servants, capped at full Health. Changing it manually sets Balance Preset to Custom. Existing servants and minibosses are unchanged.",
                    new AcceptableValueRange<float>(0.50f, 3.0f)),
                "Raised Starting Health Multiplier");
            CustomSoulClaimThresholdAdjustment = BindOrdered(
                "Custom Balance",
                "SoulClaimThresholdAdjustment",
                0.00f,
                new ConfigDescription(
                    "Flat adjustment to Soul Claim's final Health threshold. For example, 3 raises a 20% threshold to 23%. Changing it manually sets Balance Preset to Custom.",
                    new AcceptableValueRange<float>(-10.0f, 10.0f)),
                "Soul Claim Threshold Adjustment");
            Diagnostics = BindOrdered(
                "Diagnostics",
                "Diagnostics",
                false,
                "Log summon lifecycle, collision, target sharing, scaling repair, and Soul Rend decisions.");
            ShowGrailFloatingTextDiagnostics = BindOrdered(
                "Diagnostics",
                "ShowGrailFloatingTextDiagnostics",
                true,
                "When Diagnostics and Grail Floating Text are enabled, show Pale System diagnostics for Soul Rend targeting, binding details, and servant lifecycle. Ordinary player feedback remains visible when this is disabled.");
            OverrideSoulVigor = BindOrdered(
                "Diagnostics",
                "OverrideSoulVigor",
                false,
                "Temporarily use SoulVigorOverrideValue for Necromantic Power, gameplay scaling, APIs, and optional Deeds display without changing the character's saved Soul Vigor.");
            SoulVigorOverrideValue = BindOrdered(
                "Diagnostics",
                "SoulVigorOverrideValue",
                5000.0f,
                new ConfigDescription(
                    "Temporary effective Soul Vigor used only while OverrideSoulVigor is enabled. Command checkpoints are 65, 133, 206, 826, and 1,000 Soul Vigor for 10, 20, 30, 90, and 100 Necromantic Power; maximum Power 200 is reached at 5,000 and remains capped above it.",
                    new AcceptableValueRange<float>(0.0f, 10000.0f)));
            OverrideSoulforgedRank = BindOrdered(
                "Diagnostics",
                "OverrideSoulforgedRank",
                SoulforgedRankOverride.Disabled,
                "Temporarily force every current and future owned summon to the selected effective Soulforged rank without changing saved rank or damage progress.",
                "Override Soulforged Rank");
            PromoteActiveSummonsToRealRank = BindOrdered(
                "Diagnostics",
                "PromoteActiveSummonsToRealRank",
                SoulforgedPromotionTarget.None,
                "One-shot diagnostic action. Selecting I-XVII promotes every active owned summon below that rank to the selected genuine saved Soulforged rank and matching damage floor, then returns to None. It never lowers ranks, and the promotion persists until stripped or service ends. Disable Override Soulforged Rank to see the real result while testing Soul Rend stripping.",
                "Promote Active Summons To Real Rank");
            if (PromoteActiveSummonsToRealRank.Value
                != SoulforgedPromotionTarget.None)
            {
                PromoteActiveSummonsToRealRank.Value =
                    SoulforgedPromotionTarget.None;
            }

            RestorePreservedConfigValues();
            Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                Config,
                Logger,
                PluginName,
                ConfigSchemaVersion,
                ConfigRecoveryBaselineSchema,
                ConfigRecoveryKeepCurrentDefaultRules,
                ConfigRecoveryPermanentExclusions);
            ApplySelectedBalancePreset();
            BindBalancePresetEvents();
            Config.Save();
        }

        private void BindBalancePresetEvents()
        {
            BalanceProfileSetting.SettingChanged += OnBalancePresetChanged;
            CustomSoulVigorRewardMultiplier.SettingChanged += OnBalanceValueChanged;
            CustomServantUpkeepMultiplier.SettingChanged += OnBalanceValueChanged;
            CustomRaisedStartingHealthMultiplier.SettingChanged += OnBalanceValueChanged;
            CustomSoulClaimThresholdAdjustment.SettingChanged += OnBalanceValueChanged;
            PromoteActiveSummonsToRealRank.SettingChanged +=
                OnPromoteActiveSummonsToRealRankChanged;
        }

        private void UnbindBalancePresetEvents()
        {
            if (BalanceProfileSetting != null)
            {
                BalanceProfileSetting.SettingChanged -= OnBalancePresetChanged;
            }
            if (CustomSoulVigorRewardMultiplier != null)
            {
                CustomSoulVigorRewardMultiplier.SettingChanged -= OnBalanceValueChanged;
            }
            if (CustomServantUpkeepMultiplier != null)
            {
                CustomServantUpkeepMultiplier.SettingChanged -= OnBalanceValueChanged;
            }
            if (CustomRaisedStartingHealthMultiplier != null)
            {
                CustomRaisedStartingHealthMultiplier.SettingChanged -= OnBalanceValueChanged;
            }
            if (CustomSoulClaimThresholdAdjustment != null)
            {
                CustomSoulClaimThresholdAdjustment.SettingChanged -= OnBalanceValueChanged;
            }
            if (PromoteActiveSummonsToRealRank != null)
            {
                PromoteActiveSummonsToRealRank.SettingChanged -=
                    OnPromoteActiveSummonsToRealRankChanged;
            }
        }

        private void OnPromoteActiveSummonsToRealRankChanged(
            object sender,
            EventArgs eventArgs)
        {
            if (_resettingSoulforgedPromotionAction
                || PromoteActiveSummonsToRealRank == null
                || PromoteActiveSummonsToRealRank.Value
                    == SoulforgedPromotionTarget.None)
            {
                return;
            }

            int targetRank = (int)PromoteActiveSummonsToRealRank.Value;
            int eligible;
            int promoted = SoulforgedRuntime.PromoteActiveSummonsToRealRank(
                targetRank,
                out eligible);
            Logger.LogInfo(
                "Diagnostic real-rank promotion: target="
                + PromoteActiveSummonsToRealRank.Value
                + "; active=" + eligible.ToString(CultureInfo.InvariantCulture)
                + "; promoted=" + promoted.ToString(CultureInfo.InvariantCulture)
                + ".");

            _resettingSoulforgedPromotionAction = true;
            try
            {
                PromoteActiveSummonsToRealRank.Value =
                    SoulforgedPromotionTarget.None;
            }
            finally
            {
                _resettingSoulforgedPromotionAction = false;
            }
            Config.Save();
            _foaModManagerRefreshPending = true;
        }

        private void OnBalancePresetChanged(object sender, EventArgs eventArgs)
        {
            if (!_applyingBalancePreset)
            {
                ApplySelectedBalancePreset();
                _foaModManagerRefreshPending = true;
            }
        }

        private void OnBalanceValueChanged(object sender, EventArgs eventArgs)
        {
            if (_applyingBalancePreset
                || BalanceProfileSetting == null
                || BalanceProfileSetting.Value == BalanceProfile.Custom)
            {
                return;
            }

            _applyingBalancePreset = true;
            try
            {
                BalanceProfileSetting.Value = BalanceProfile.Custom;
            }
            finally
            {
                _applyingBalancePreset = false;
            }
            _foaModManagerRefreshPending = true;
        }

        private void RefreshFoaModManagerIfPending()
        {
            if (!_foaModManagerRefreshPending)
            {
                return;
            }

            _foaModManagerRefreshPending = false;
            try
            {
                Type apiType = AccessTools.TypeByName(
                    "FoAModManager.FoAModManagerApi");
                MethodInfo refreshMethod = apiType == null
                    ? null
                    : AccessTools.Method(apiType, "Refresh");
                if (refreshMethod != null)
                {
                    refreshMethod.Invoke(null, null);
                }
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "FoA Mod Manager refresh failed: "
                    + exception.GetBaseException().Message);
            }
        }

        private void ApplySelectedBalancePreset()
        {
            if (BalanceProfileSetting == null
                || BalanceProfileSetting.Value == BalanceProfile.Custom)
            {
                return;
            }

            SoulBalanceTuning tuning = GetPresetBalanceTuning(
                BalanceProfileSetting.Value);
            _applyingBalancePreset = true;
            try
            {
                CustomSoulVigorRewardMultiplier.Value =
                    tuning.SoulVigorRewardMultiplier;
                CustomServantUpkeepMultiplier.Value =
                    tuning.ServantUpkeepMultiplier;
                CustomRaisedStartingHealthMultiplier.Value =
                    tuning.RaisedStartingHealthMultiplier;
                CustomSoulClaimThresholdAdjustment.Value =
                    tuning.SoulClaimThresholdAdjustment;
            }
            finally
            {
                _applyingBalancePreset = false;
            }
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
            CapturePreservedValue<BalanceProfile>(profile, "Core", "BalanceProfile");
            CapturePreservedValue<float>(profile, "Custom Balance", "SoulVigorRewardMultiplier");
            CapturePreservedValue<float>(profile, "Custom Balance", "ServantUpkeepMultiplier");
            CapturePreservedValue<float>(profile, "Custom Balance", "RaisedStartingHealthMultiplier");
            CapturePreservedValue<float>(profile, "Custom Balance", "SoulClaimThresholdAdjustment");
            CapturePreservedValue<float>(profile, "Responsiveness", "AITickInterval");
            CapturePreservedValue<float>(profile, "Responsiveness", "SpawnRecoverySeconds");
            CapturePreservedValue<float>(profile, "Following", "TrotDistance");
            CapturePreservedValue<float>(profile, "Following", "RunDistance");
            CapturePreservedValue<float>(profile, "Following", "TeleportDistance");
            CapturePreservedValue<float>(profile, "Following", "CatchUpSpeedMultiplier");
            CapturePreservedValue<float>(profile, "Following", "IdleMovementAmount");
            CapturePreservedValue<bool>(profile, "Targeting", "ShareHeroTarget");
            CapturePreservedValue<bool>(profile, "Targeting", "AttackCommandPrompt");
            CapturePreservedValue<bool>(profile, "Targeting", "FormationCommands");
            CapturePreservedValue<bool>(profile, "Targeting", "HoldIndividualFormationCommands");
            CapturePreservedValue<bool>(profile, "Summon Behaviors", "EnableDirectedHunt");
            CapturePreservedValue<bool>(profile, "Summon Behaviors", "ShowDirectedHuntPreview");
            CapturePreservedValue<bool>(profile, "Summon Behaviors", "EnableBulwarkAdvance");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "BulwarkAdvanceReleaseSeconds");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "BulwarkAdvanceSpeedMultiplier");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "GuardFormationDistance");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "GuardEngagementRange");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "HuntFormationDistance");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "BulwarkCloseGuardDistance");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "BulwarkAdvanceDistance");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "BulwarkLocalEngagementRange");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "BulwarkTargetRetentionRange");
            CapturePreservedValue<float>(profile, "Summon Behaviors", "BulwarkPlayerLeash");
            CapturePreservedValue<TargetCommandModifierMode>(profile, "Targeting", "TargetCommandModifier");
            CapturePreservedValue<float>(profile, "Targeting", "ShareTargetMaxDistance");
            CapturePreservedValue<bool>(profile, "Collision", "Summon Pass-Through");
            CapturePreservedValue<PlayerAttackPassThroughMode>(profile, "Collision", "Player Attack Pass-Through");
            CapturePreservedValue<bool>(profile, "Persistence", "PersistentServants");
            CapturePreservedValue<RestHostBehavior>(profile, "Persistence", "RestHostBehavior");
            CapturePreservedValue<int>(profile, "Persistence", "SummonLimitBonus");
            CapturePreservedValue<bool>(profile, "Balance", "RepairInvocationOfMightScaling");
            CapturePreservedValue<float>(profile, "Balance", "IdleSoundVolumePercent");
            CapturePreservedValue<bool>(profile, "Audio", "PlaySoulSalvageAudio");
            CapturePreservedValue<float>(profile, "Audio", "SoulSalvageAudioVolume");
            CapturePreservedValue<float>(profile, "Audio", "SoulSalvageAudioRangeVolume");
            CapturePreservedValue<bool>(profile, "Audio", "AvoidRecentSoulSalvageAudioRepeats");
            CapturePreservedValue<int>(profile, "Audio", "RecentSoulSalvageAudioMemory");
            CapturePreservedValue<float>(profile, "Audio", "SoulSalvageAudioRandomPitchSemitones");
            CapturePreservedValue<float>(profile, "Audio", "FemaleSoulSalvageAudioPitchSemitones");
            CapturePreservedValue<float>(profile, "Audio", "MaleSoulSalvageAudioPitchSemitones");
            CapturePreservedValue<float>(profile, "Audio", "FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones");
            CapturePreservedValue<float>(profile, "Audio", "MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones");
            CapturePreservedValue<float>(profile, "Audio", "NonHumanoidSoulSalvageAudioPitchSemitones");
            CapturePreservedValue<float>(profile, "Audio", "SoulSalvageAudioEchoAmount");
            CapturePreservedValue<bool>(profile, "Audio", "PlaySoulRendImpactAudio");
            CapturePreservedValue<float>(profile, "Audio", "SoulRendImpactAudioVolume");
            CapturePreservedValue<bool>(profile, "Soul Salvage", "EnableSoulSalvageOverhaul");
            CapturePreservedValue<bool>(profile, "Soul Salvage", "EnableLivingTargetSoulSalvage");
            CapturePreservedValue<bool>(profile, "Soul Rend Inner Light", "Enabled");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "Intensity");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "SoulRendIntensityMultiplier");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "InteriorIntensityMultiplier");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "MinimumPowerBrightnessMultiplier");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "MasteryBrightnessMultiplier");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "MaximumPowerBrightnessMultiplier");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "MinimumPowerRange");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "MasteryRange");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "MaximumPowerRange");
            CapturePreservedValue<float>(profile, "Soul Rend Inner Light", "FadeSeconds");
            CapturePreservedValue<bool>(profile, "Reanimation VFX", "Enabled");
            CapturePreservedValue<string>(profile, "Reanimation VFX", "AuraArcColor");
            CapturePreservedValue<string>(profile, "Reanimation VFX", "AuraGlowColor");
            CapturePreservedValue<string>(profile, "Reanimation VFX", "AuraHazeColor");
            CapturePreservedValue<bool>(profile, "Reanimation VFX", "UseCustomFullPotentialColor");
            CapturePreservedValue<string>(profile, "Reanimation VFX", "FullPotentialColor");
            CapturePreservedValue<int>(profile, "Reanimation VFX", "AuraParticleAmount");
            CapturePreservedValue<float>(profile, "Reanimation VFX", "AuraIntensity");
            CapturePreservedValue<float>(profile, "Reanimation VFX", "FullPotentialBrightness");
            CapturePreservedValue<float>(profile, "Reanimation VFX", "ElectricityOpacity");
            CapturePreservedValue<float>(profile, "Reanimation VFX", "SmokeOpacity");
            CapturePreservedValue<float>(profile, "Reanimation VFX", "AuraScale");
            CapturePreservedValue<bool>(profile, "Reanimation VFX", "DynamicParticleBudget");
            CapturePreservedValue<bool>(profile, "Diagnostics", "Diagnostics");
            CapturePreservedValue<bool>(profile, "Diagnostics", "ShowGrailFloatingTextDiagnostics");
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
            RestorePreservedValue(BalanceProfileSetting, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(CustomSoulVigorRewardMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(CustomServantUpkeepMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(CustomRaisedStartingHealthMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(CustomSoulClaimThresholdAdjustment, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(AiTickInterval, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SpawnRecoverySeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(TrotDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(RunDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(TeleportDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(CatchUpSpeedMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(IdleMovementAmount, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ShareHeroTarget, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(AttackCommandPrompt, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(FormationCommands, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(HoldIndividualFormationCommands, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(DirectedHuntEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ShowDirectedHuntPreview, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkAdvanceEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkAdvanceReleaseSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkAdvanceSpeedMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(GuardFormationDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(GuardEngagementRange, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(HuntFormationDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkCloseGuardDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkAdvanceDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkLocalEngagementRange, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkTargetRetentionRange, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(BulwarkPlayerLeash, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(TargetCommandModifier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ShareTargetMaxDistance, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SummonPassThrough, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(PlayerAttackPassThrough, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(PersistentServants, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(RestBehavior, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SummonLimitBonus, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(RepairInvocationScaling, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(IdleSoundVolumePercent, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(PlaySoulSalvageAudio, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageAudioVolume, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageAudioRangeVolume, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(AvoidRecentSoulSalvageAudioRepeats, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(RecentSoulSalvageAudioMemory, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageAudioRandomPitchSemitones, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(FemaleSoulSalvageAudioPitchSemitones, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(MaleSoulSalvageAudioPitchSemitones, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(NonHumanoidSoulSalvageAudioPitchSemitones, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageAudioEchoAmount, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(PlaySoulRendImpactAudio, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendImpactAudioVolume, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulSalvageOverhaul, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(LivingTargetSoulSalvage, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightIntensityMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightInteriorIntensityMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightMinimumPowerBrightnessMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightMasteryBrightnessMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightMaximumPowerBrightnessMultiplier, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightMinimumPowerRange, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightMasteryRange, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightMaximumPowerRange, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(SoulRendInnerLightFadeSeconds, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationVfxEnabled, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationAuraArcColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationAuraGlowColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationAuraHazeColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationUseCustomFullPotentialColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationFullPotentialColor, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationAuraParticleAmount, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationAuraIntensity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationFullPotentialBrightness, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationElectricityOpacity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationSmokeOpacity, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationAuraScale, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ReanimationDynamicParticleBudget, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(Diagnostics, ref restored, ref clamped, ref invalid);
            RestorePreservedValue(ShowGrailFloatingTextDiagnostics, ref restored, ref clamped, ref invalid);
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

    public static class SoulAndServiceApi
    {
        public const int ApiVersion = 10;

        public static bool IsLoaded
        {
            get { return SoulAndServicePlugin.Instance != null; }
        }

        public static float GetSoulVigor()
        {
            return SoulProgressionRuntime.GetSoulVigor();
        }

        public static float GetNecromanticPower()
        {
            return SoulProgressionRuntime.GetNecromanticPower();
        }

        public static int GetFocusedSoulSalvageTargetState(bool requireRelevantSpell)
        {
            return SoulSalvageRuntime.GetFocusedTargetStateForInterop(
                requireRelevantSpell);
        }

        public static float GetFocusedSoulSalvageQuality01()
        {
            return SoulSalvageRuntime.GetFocusedTargetQuality01ForInterop();
        }

        public static int GetFocusedSoulSalvageQualityTier()
        {
            return SoulSalvageRuntime.GetFocusedTargetQualityTierForInterop();
        }

        public static float GetFocusedSoulBindingProgress01()
        {
            return SoulSalvageRuntime.GetFocusedBindingProgress01ForInterop();
        }

        public static bool IsNecroticDamage(object damage)
        {
            return SoulSalvageRuntime.IsNecroticDamageForInterop(damage);
        }

        public static int GetHeavySoulRendHoverState()
        {
            return SoulSalvageRuntime.GetHeavySoulRendHoverStateForInterop();
        }

        public static string GetHeavySoulRendHoverText()
        {
            return SoulSalvageRuntime.GetHeavySoulRendHoverTextForInterop();
        }

        public static bool TryResolveOwnedReanimatedServant(
            object candidate,
            out object sourceCorpse)
        {
            return SoulSalvageRuntime.TryResolveOwnedReanimatedServantForInterop(
                candidate,
                out sourceCorpse);
        }

        public static bool TryResolveOwnedBloodServant(
            object candidate,
            out object sourceCorpse,
            out object servantNpc)
        {
            return SoulSalvageRuntime.TryResolveOwnedBloodServantForInterop(
                candidate,
                out sourceCorpse,
                out servantNpc);
        }

        public static bool TryResolveOwnedBloodServantIdentity(
            object candidate,
            out object sourceLocation,
            out object sourceCorpse,
            out object servantNpc)
        {
            return SoulSalvageRuntime
                .TryResolveOwnedBloodServantIdentityForInterop(
                    candidate,
                    out sourceLocation,
                    out sourceCorpse,
                    out servantNpc);
        }

        public static bool TryExsanguinateOwnedReanimatedServant(
            object candidate,
            float severity,
            out bool killed)
        {
            return SoulSalvageRuntime.TryExsanguinateOwnedReanimatedServantForInterop(
                candidate,
                severity,
                out killed);
        }

        public static bool TryExsanguinateOwnedBloodServant(
            object candidate,
            float severity,
            out bool killed)
        {
            return SoulSalvageRuntime.TryExsanguinateOwnedBloodServantForInterop(
                candidate,
                severity,
                out killed);
        }

        public static bool TryMaterializeOwnedBloodServantCorpseForAbhartach(
            object candidate,
            out object corpseLocation)
        {
            return SoulSalvageRuntime
                .TryMaterializeOwnedBloodServantCorpseForAbhartachForInterop(
                    candidate,
                    out corpseLocation);
        }

        public static bool SetOwnedReanimatedServantBloodRitualState(
            object candidate,
            bool channeling,
            bool completed)
        {
            return SoulSalvageRuntime
                .SetOwnedReanimatedServantBloodRitualStateForInterop(
                    candidate,
                    channeling,
                    completed);
        }

        public static bool SetOwnedBloodServantRitualState(
            object candidate,
            bool channeling,
            bool completed)
        {
            return SoulSalvageRuntime.SetOwnedBloodServantRitualStateForInterop(
                candidate,
                channeling,
                completed);
        }

        public static bool ShouldOwnTakeAllHold()
        {
            return SummonRuntime.ShouldOwnTakeAllHoldForInterop();
        }

        public static int GetFocusedSummonCommandState()
        {
            return SummonRuntime.GetFocusedCommandStateForInterop();
        }

        public static int GetLastSummonCommandState()
        {
            return SummonRuntime.GetLastCommandStateForInterop();
        }

        public static int GetSummonCommandSequence()
        {
            return SummonRuntime.GetCommandSequenceForInterop();
        }

        public static float GetLastSummonCommandPulseSeconds()
        {
            return SummonRuntime.GetLastCommandPulseSecondsForInterop();
        }

        public static int GetSummonBehavior()
        {
            return (int)SoulProgressionRuntime.GetSummonBehavior();
        }
    }
}
