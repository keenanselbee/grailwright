using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[assembly: AssemblyTitle("Dishonored Dynamic Crosshair")]
[assembly: AssemblyDescription("Context-aware custom reticles for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Dishonored Dynamic Crosshair")]
[assembly: AssemblyVersion("3.1.4.0")]
[assembly: AssemblyFileVersion("3.1.4.0")]

namespace DishonoredDynamicCrosshair
{
    public enum ReticlePreset
    {
        AlwaysVisible,
        TargetOnly,
        CombatReady,
        HostilesOnly
    }

    public enum ReticleMode
    {
        AlwaysVisibleSmart,
        TargetOnlySmart,
        HostilesOnly
    }

    public enum MagicDetectionMode
    {
        CastMagicOnly,
        AnyMagic
    }

    public enum ReticleSizeMode
    {
        ScreenPixels,
        UIUnits
    }

    public enum ReticleFilteringMode
    {
        MipmappedTrilinear,
        Bilinear
    }

    public enum BloodMagicCorpseReticleMode
    {
        Off,
        Auto
    }

    internal enum BloodMagicFocusedCorpseState
    {
        None = 0,
        Usable = 1,
        Channeling = 2,
        Spent = 3,
        Blocked = 4
    }

    internal enum ReticleContext
    {
        General,
        Bow,
        Magic,
        BloodMagic
    }

    internal enum TargetState
    {
        Default,
        Hostile,
        NonHostile
    }

    internal enum HitMarkerFrame
    {
        Blocked = 0,
        ExtremeResistance = 1,
        StrongResistance = 2,
        MildResistance = 3,
        Neutral = 4,
        MildWeakness = 5,
        StrongWeakness = 6,
        ExtremeWeakness = 7
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ks.tgfoa.steel-and-bone", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DishonoredDynamicCrosshairPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.dishonored-dynamic-crosshair";
        public const string PluginName = "Dishonored Dynamic Crosshair";
        public const string PluginVersion = "3.1.4";
        private const int ConfigSchemaVersion = 9;
        private const int ConfigRecoveryBaselineSchema = 3;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const string BloodMagicExpansionPluginGuid =
            "ks.tgfoa.blood-magic-expansion";
        private const string BloodMagicExpansionApiTypeName =
            "BloodMagicExpansion.BloodMagicApi";
        private const string SteelAndBonePluginGuid = "ks.tgfoa.steel-and-bone";
        private const string SteelAndBoneHitFeedbackApiTypeName =
            "SteelAndBone.SteelAndBoneHitFeedbackApi";
        private const float HitMarkerFadeFraction = 0.25f;
        private const float HitMarkerInitialScale = 1.12f;

        internal static DishonoredDynamicCrosshairPlugin Instance { get; private set; }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<ReticlePreset> _preset;
        private ConfigEntry<MagicDetectionMode> _magicDetection;
        private ConfigEntry<bool> _useGeneralWhenHandsDown;
        private ConfigEntry<BloodMagicCorpseReticleMode> _bloodMagicCorpseReticleMode;
        private ConfigEntry<bool> _bloodMagicRequireRelevantSpell;
        private ConfigEntry<bool> _bloodMagicLogScaleDiagnostics;
        private ConfigEntry<bool> _bloodMagicUseQualityScale;
        private ConfigEntry<bool> _bloodMagicQualityCrosshairsEnabled;
        private ConfigEntry<float> _bloodMagicMaximumQualityScale;
        private ConfigEntry<string> _bloodMagicUsableCorpseColor;
        private ConfigEntry<ReticleSizeMode> _sizeMode;
        private ConfigEntry<ReticleFilteringMode> _textureFiltering;
        private ConfigEntry<float> _baseSizePixels;
        private ConfigEntry<float> _targetDetectionRangeMultiplier;
        private ConfigEntry<float> _hostilityRefreshIntervalSeconds;
        private ConfigEntry<float> _defaultOpacity;
        private ConfigEntry<float> _hostileOpacity;
        private ConfigEntry<float> _nonHostileOpacity;
        private ConfigEntry<float> _mountedOpacityMultiplier;
        private ConfigEntry<bool> _showCrouchIndicator;
        private ConfigEntry<float> _crouchIndicatorOpacity;
        private ConfigEntry<float> _crouchIndicatorVerticalOffset;
        private ConfigEntry<bool> _hideDefaultReticle;
        private ConfigEntry<bool> _hideMeleeReticle;
        private ConfigEntry<bool> _hideBowReticle;
        private ConfigEntry<bool> _hideItemSpecificReticles;
        private ConfigEntry<bool> _steelAndBoneHitMarkersEnabled;
        private ConfigEntry<bool> _killingBlowOverlaysEnabled;
        private ConfigEntry<float> _hitMarkerSizeMultiplier;
        private ConfigEntry<float> _hitMarkerDamageOverTimeSizeMultiplier;
        private ConfigEntry<float> _killingBlowSizeMultiplier;
        private ConfigEntry<float> _hitMarkerDurationMultiplier;
        private ConfigEntry<float> _killingBlowDurationMultiplier;
        private readonly Dictionary<string, float> _pendingPreservedVisualFloats =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pendingPreservedVisualStrings =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _pendingPreservedVisualBools =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private ReticleSizeMode? _pendingPreservedSizeMode;
        private int _pendingPreservedInvalidValueCount;

        private ContextSettings _general;
        private ContextSettings _bow;
        private ContextSettings _magic;
        private ContextSettings _bloodMagic;
        private readonly Dictionary<HitMarkerFrame, ReticleAsset> _hitMarkerAssets =
            new Dictionary<HitMarkerFrame, ReticleAsset>
            {
                { HitMarkerFrame.Blocked, new ReticleAsset() },
                { HitMarkerFrame.ExtremeResistance, new ReticleAsset() },
                { HitMarkerFrame.StrongResistance, new ReticleAsset() },
                { HitMarkerFrame.MildResistance, new ReticleAsset() },
                { HitMarkerFrame.Neutral, new ReticleAsset() },
                { HitMarkerFrame.MildWeakness, new ReticleAsset() },
                { HitMarkerFrame.StrongWeakness, new ReticleAsset() },
                { HitMarkerFrame.ExtremeWeakness, new ReticleAsset() }
            };
        private readonly ReticleAsset _directHitMarkerOverlay = new ReticleAsset();
        private readonly ReticleAsset _weakSpotHitMarkerOverlay = new ReticleAsset();
        private readonly ReticleAsset _criticalHitMarkerOverlay = new ReticleAsset();
        private readonly Dictionary<int, ReticleAsset> _bloodMagicQualityAssets =
            new Dictionary<int, ReticleAsset>
            {
                { 1, new ReticleAsset() },
                { 2, new ReticleAsset() },
                { 3, new ReticleAsset() },
                { 4, new ReticleAsset() }
            };
        private readonly Dictionary<int, ReticleAsset> _killingBlowOverlayAssets =
            new Dictionary<int, ReticleAsset>
            {
                { 1, new ReticleAsset() },
                { 2, new ReticleAsset() },
                { 3, new ReticleAsset() },
                { 4, new ReticleAsset() }
            };

        private Harmony _harmony;
        private MethodInfo _getMainViewMethod;
        private MethodInfo _getCurrentLocationTypeMethod;
        private MethodInfo _getHeroMethod;
        private MethodInfo _getHeroItemsMethod;
        private MethodInfo _weaponsVisibleGetter;
        private MethodInfo _mountedGetter;
        private MethodInfo _equippedItemMethod;
        private MethodInfo _isRangedGetter;
        private MethodInfo _isMagicGetter;
        private MethodInfo _isCastMagicGetter;
        private MethodInfo _bloodMagicGetCorpseStateMethod;
        private MethodInfo _bloodMagicGetCorpseQualityMethod;
        private MethodInfo _bloodMagicGetCorpseQualityTierMethod;
        private MethodInfo _refreshCrosshairMethod;
        private MethodInfo _targetChangedMethod;
        private MethodInfo _loadImageMethod;
        private EventInfo _steelAndBoneHitResolvedEvent;
        private Delegate _steelAndBoneHitResolvedHandler;
        private EventInfo _steelAndBoneKillingBlowResolvedEvent;
        private Delegate _steelAndBoneKillingBlowResolvedHandler;
        private FieldInfo _allEquipmentSlotsField;
        private FieldInfo _npcDetectionMaxDistanceField;
        private object _defaultTargetType;
        private object _hostileTargetType;
        private object _nonHostileTargetType;

        private object _heroCrosshair;
        private object _currentTargetLocation;
        private object _heroRaycaster;
        private float _originalNpcDetectionMaxDistance;
        private bool _hasOriginalNpcDetectionMaxDistance;
        private Transform _crosshairParent;
        private GameObject _reticleObject;
        private RectTransform _reticleRect;
        private Image _reticleImage;
        private Image _directHitMarkerImage;
        private Image _weakSpotHitMarkerImage;
        private Image _criticalHitMarkerImage;
        private Image _killingBlowHitMarkerImage;
        private CanvasGroup _crouchCanvasGroup;
        private RectTransform _crouchRect;
        private GameObject _crouchViewObject;
        private float _originalCrouchAlpha = 1f;
        private Vector2 _originalCrouchAnchoredPosition;
        private bool _hasOriginalCrouchAnchoredPosition;
        private bool _ownsCrouchCanvasGroup;
        private ReticleContext _currentContext;
        private TargetState _currentTargetState;
        private float _nextSpriteCheckTime;
        private float _nextContextCheckTime;
        private float _nextTargetRefreshTime;
        private float _nextBloodMagicCheckTime;
        private float _nextBloodMagicApiResolveTime;
        private float _nextSteelAndBoneApiResolveTime;
        private float _lastCanvasScaleFactor = -1f;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private bool _mainViewFailureLogged;
        private bool _targetReadFailureLogged;
        private bool _targetRefreshFailureLogged;
        private bool _raycasterRangeFailureLogged;
        private bool _equipmentReadFailureLogged;
        private bool _weaponsVisibilityReadFailureLogged;
        private bool _mountedReadFailureLogged;
        private bool _bloodMagicApiFailureLogged;
        private bool _lastBloodMagicCorpseActive;
        private int _lastBloodMagicCorpseState;
        private float _lastBloodMagicCorpseQuality01 = 0.5f;
        private int _lastBloodMagicCorpseQualityTier;
        private float _currentBloodMagicQualityScale = 1f;
        private int _currentBloodMagicCorpseState;
        private int _currentBloodMagicCorpseQualityTier;
        private float _nextBloodMagicScaleDiagnosticLogTime;
        private bool _bloodMagicApiUnavailableForSession;
        private bool _bloodMagicApiUnavailableLogged;
        private bool _steelAndBoneApiUnavailableForSession;
        private bool _steelAndBoneApiFailureLogged;
        private bool _hitMarkerActive;
        private HitMarkerFrame _activeHitMarkerFrame;
        private bool _activeHitMarkerWeakSpot;
        private bool _activeHitMarkerCritical;
        private bool _activeHitMarkerDamageOverTime;
        private int _activeKillingBlowTier;
        private Color _activeHitMarkerColor = Color.white;
        private float _activeHitMarkerStartedAt;
        private float _activeHitMarkerEndsAt;
        private bool _lastHeroMounted;
        private bool _hasLastHeroMounted;
        private readonly HashSet<string> _invalidColorsLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string PluginDirectory
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        private void Awake()
        {
            Instance = this;

            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
                RegisterSettingHandlers();
                ApplyPreset(_preset.Value);
                PatchGame();
                LoadAllSprites();
                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                enabled = false;
            }
        }

        private void BindConfig()
        {
            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Enable Dishonored Dynamic Crosshair.");
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. It changes only when an update requires fresh defaults.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _preset = Config.Bind(
                "1. Core",
                "Preset",
                ReticlePreset.AlwaysVisible,
                "Main visibility profile. AlwaysVisible keeps reticles present; TargetOnly hides them unless an NPC is targeted; CombatReady keeps bow and magic visible; HostilesOnly shows only hostile targets.");
            _baseSizePixels = Config.Bind(
                "2. Reticles",
                "ReticleSizePixels",
                80f,
                new ConfigDescription(
                    "Default square reticle size before Bow, Magic, or Blood Magic scale multipliers.",
                    new AcceptableValueRange<float>(4f, 256f)));

            _bloodMagicCorpseReticleMode = Config.Bind(
                "4. Blood Magic",
                "Mode",
                BloodMagicCorpseReticleMode.Auto,
                "Auto shows the blood-magic reticle when Blood Magic Expansion reports a focused corpse. Off disables this integration.");
            _bloodMagicRequireRelevantSpell = Config.Bind(
                "4. Blood Magic",
                "RequireRelevantBloodSpell",
                true,
                "Only show the corpse reticle when Blood/Life Transfusion or Abhartach's Calling is relevant and available.");
            _bloodMagicUseQualityScale = Config.Bind(
                "4. Blood Magic",
                "UseCorpseQualityScale",
                true,
                "Scale the Blood Magic corpse reticle from Blood Magic Expansion's reported corpse quality.");
            _bloodMagicQualityCrosshairsEnabled = Config.Bind(
                "4. Blood Magic",
                "BloodMagicQualityCrosshairsEnabled",
                true,
                "Use tier-specific Blood Magic corpse reticles when their PNG assets are available.");
            _bloodMagicMaximumQualityScale = Config.Bind(
                "4. Blood Magic",
                "MaximumQualityScale",
                2f,
                new ConfigDescription(
                    "Reticle scale multiplier for a high-quality usable corpse. Low-quality, blocked, and spent corpses stay at 1x.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            _bloodMagicUsableCorpseColor = Config.Bind(
                "4. Blood Magic",
                "UsableCorpseColor",
                "#E8583CFF",
                "Color for usable blood-magic corpses in #RRGGBBAA format. Corpse quality changes scale only, not color.");
            _general = BindContext(
                ReticleContext.General,
                "General",
                "custom_reticle_4.png",
                1f);
            _bow = BindContext(
                ReticleContext.Bow,
                "Bow",
                "custom_reticle_bow.png",
                1f);
            _magic = BindContext(
                ReticleContext.Magic,
                "Magic",
                "custom_reticle_magic.png",
                1f);
            _bloodMagic = BindContext(
                ReticleContext.BloodMagic,
                "BloodMagic",
                "custom_reticle_bloodmagic.png",
                1f);

            _defaultOpacity = Config.Bind(
                "3. Colors and Opacity",
                "IdleOpacity",
                0.1f,
                OpacityDescription("Opacity when no NPC is targeted."));
            _hostileOpacity = Config.Bind(
                "3. Colors and Opacity",
                "TargetOpacity",
                0.3f,
                OpacityDescription("Opacity while targeting a hostile, friendly, or neutral NPC."));
            _nonHostileOpacity = _hostileOpacity;
            _mountedOpacityMultiplier = Config.Bind(
                "3. Colors and Opacity",
                "MountedOpacityMultiplier",
                0f,
                OpacityDescription(
                    "Additional opacity multiplier while the hero is mounted. Set to 1 to keep custom reticles visible on mounts."));

            _steelAndBoneHitMarkersEnabled = Config.Bind(
                "5. Steel and Bone Hit Markers",
                "Enabled",
                true,
                "Temporarily replace the current reticle with Steel and Bone hit feedback when Steel and Bone is installed.");
            _killingBlowOverlaysEnabled = Config.Bind(
                "5. Steel and Bone Hit Markers",
                "KillingBlowOverlaysEnabled",
                true,
                "Show the tier-specific Steel and Bone killing-blow overlay when its PNG asset is available.");
            _hitMarkerSizeMultiplier = Config.Bind(
                "5. Steel and Bone Hit Markers",
                "SizeMultiplier",
                1.15f,
                new ConfigDescription(
                    "Hit-marker size relative to ReticleSizePixels. Bow, Magic, Blood Magic, and corpse-quality scales do not affect it.",
                    new AcceptableValueRange<float>(0.5f, 3f)));
            _hitMarkerDamageOverTimeSizeMultiplier = Config.Bind(
                "5. Steel and Bone Hit Markers",
                "DamageOverTimeSizeMultiplier",
                1.1f,
                new ConfigDescription(
                    "Damage-over-time hit-marker size relative to ReticleSizePixels. This replaces SizeMultiplier for Bleed, Poison, Burn, and Breath ticks.",
                    new AcceptableValueRange<float>(0.5f, 3f)));
            _killingBlowSizeMultiplier = Config.Bind(
                "5. Steel and Bone Hit Markers",
                "KillingBlowSizeMultiplier",
                1.3f,
                new ConfigDescription(
                    "Killing-blow hit-marker size relative to ReticleSizePixels. This replaces SizeMultiplier for the complete killing-blow marker composition.",
                    new AcceptableValueRange<float>(0.5f, 3f)));
            _hitMarkerDurationMultiplier = Config.Bind(
                "5. Steel and Bone Hit Markers",
                "DurationMultiplier",
                1f,
                new ConfigDescription(
                    "Multiplier applied to Steel and Bone's final damage-number duration for each hit marker.",
                    new AcceptableValueRange<float>(0.1f, 2f)));
            _killingBlowDurationMultiplier = Config.Bind(
                "5. Steel and Bone Hit Markers",
                "KillingBlowDurationMultiplier",
                1.5f,
                new ConfigDescription(
                    "Additional multiplier applied to the normal hit-marker duration for killing blows.",
                    new AcceptableValueRange<float>(0.1f, 3f)));

            _magicDetection = Config.Bind(
                "5. Advanced",
                "MagicDetection",
                MagicDetectionMode.CastMagicOnly,
                "CastMagicOnly detects aimed magic. AnyMagic treats every equipped magic item as magic context.");
            _useGeneralWhenHandsDown = Config.Bind(
                "5. Advanced",
                "UseGeneralWhenHandsDown",
                true,
                "Use the General reticle whenever the game's weapons are hidden, even if a bow or magic item is equipped.");
            _targetDetectionRangeMultiplier = Config.Bind(
                "5. Advanced",
                "RangeMultiplier",
                1.2f,
                new ConfigDescription(
                    "Multiplier for the game's NPC target-detection range. This affects NPC coloring and health-bar targeting, not interaction distance. Set to 1 for the vanilla range.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            _hostilityRefreshIntervalSeconds = Config.Bind(
                "5. Advanced",
                "HostilityRefreshIntervalSeconds",
                0.1f,
                new ConfigDescription(
                    "How often to re-evaluate a hovered NPC so hostility changes update without moving the reticle.",
                    new AcceptableValueRange<float>(0.02f, 1f)));
            _sizeMode = Config.Bind(
                "5. Advanced",
                "SizeMode",
                ReticleSizeMode.ScreenPixels,
                "ScreenPixels compensates for the HUD canvas scale. UIUnits follows the game's canvas scaling.");
            _textureFiltering = Config.Bind(
                "5. Advanced",
                "TextureFiltering",
                ReticleFilteringMode.MipmappedTrilinear,
                "MipmappedTrilinear improves minification quality. Bilinear preserves the legacy filtering.");
            _showCrouchIndicator = Config.Bind(
                "5. Advanced",
                "ShowCrouchIndicator",
                true,
                "Keep the game's crouching and detection indicator available.");
            _crouchIndicatorOpacity = Config.Bind(
                "5. Advanced",
                "CrouchIndicatorOpacity",
                0.15f,
                OpacityDescription(
                    "Opacity multiplier for the complete crouching and detection indicator."));
            _crouchIndicatorVerticalOffset = Config.Bind(
                "5. Advanced",
                "CrouchIndicatorVerticalOffset",
                0f,
                "Vertical offset for the complete crouching and detection indicator in UI units. Positive values move it lower; negative values move it higher.");

            _hideDefaultReticle = Config.Bind(
                "5. Advanced",
                "HideVanillaReticles",
                true,
                "Hide the game's default, melee, bow, and item-provided reticles while this plugin is enabled.");
            _hideMeleeReticle = _hideDefaultReticle;
            _hideBowReticle = _hideDefaultReticle;
            _hideItemSpecificReticles = _hideDefaultReticle;

            _bloodMagicLogScaleDiagnostics = Config.Bind(
                "6. Diagnostics",
                "LogBloodMagicScaleDiagnostics",
                false,
                "Log throttled Blood Magic reticle state, quality, and final scale math.");
            RestorePreservedVisualProfile();
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
            if (!File.Exists(configPath))
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

            CapturePreservedVisualProfile(
                configPath,
                storedSchemaVersion);

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
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid, PluginName, storedSchemaVersion, ConfigSchemaVersion);
            }
            catch (Exception exception)
            {
                ClearPendingPreservedVisualProfile();

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
                        "Could not restore the previous crosshair config after a failed schema reset: "
                        + restoreException.Message);
                }

                Logger.LogWarning(
                    "Could not reset the outdated crosshair config. The previous config was retained when possible: "
                    + exception.Message);
            }
        }

        private void CapturePreservedVisualProfile(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedVisualProfile();
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

                if (IsPreservedVisualFloat(settingId))
                {
                    float parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedVisualFloats[settingId] = parsedValue;
                    }

                    continue;
                }

                if (IsPreservedVisualString(settingId))
                {
                    string preservedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out preservedValue))
                    {
                        _pendingPreservedVisualStrings[settingId] =
                            preservedValue;
                    }
                    continue;
                }

                if (string.Equals(
                        settingId,
                        "4. Blood Magic\nUseCorpseQualityScale",
                        StringComparison.Ordinal))
                {
                    bool parsedValue;
                    if (profile.TryGetCustomizedValue(
                        currentSection,
                        settingName,
                        out parsedValue))
                    {
                        _pendingPreservedVisualBools[settingId] = parsedValue;
                    }

                    continue;
                }

                if (string.Equals(
                        settingId,
                        "5. Advanced\nSizeMode",
                        StringComparison.Ordinal))
                {
                    ReticleSizeMode parsedValue;
                    if (profile.TryGetCustomizedValue(
                            currentSection,
                            settingName,
                            out parsedValue)
                        && Enum.IsDefined(typeof(ReticleSizeMode), parsedValue))
                    {
                        _pendingPreservedSizeMode = parsedValue;
                    }
                    else
                    {
                        _pendingPreservedInvalidValueCount++;
                    }
                }
            }
        }

        private static bool IsPreservedVisualFloat(string settingId)
        {
            switch (settingId)
            {
                case "2. Reticles\nReticleSizePixels":
                case "2. Reticles\nBowScale":
                case "2. Reticles\nMagicScale":
                case "2. Reticles\nBloodMagicScale":
                case "3. Colors and Opacity\nIdleOpacity":
                case "3. Colors and Opacity\nTargetOpacity":
                case "3. Colors and Opacity\nMountedOpacityMultiplier":
                case "4. Blood Magic\nMaximumQualityScale":
                case "5. Advanced\nCrouchIndicatorOpacity":
                case "5. Advanced\nCrouchIndicatorVerticalOffset":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPreservedVisualString(string settingId)
        {
            switch (settingId)
            {
                case "2. Reticles\nGeneralSprite":
                case "2. Reticles\nBowSprite":
                case "2. Reticles\nMagicSprite":
                case "2. Reticles\nBloodMagicSprite":
                case "3. Colors and Opacity\nDefaultColor":
                case "3. Colors and Opacity\nHostileColor":
                case "3. Colors and Opacity\nNonHostileColor":
                case "4. Blood Magic\nUsableCorpseColor":
                    return true;
                default:
                    return false;
            }
        }

        private void RestorePreservedVisualProfile()
        {
            if (_pendingPreservedVisualFloats.Count == 0
                && _pendingPreservedVisualStrings.Count == 0
                && _pendingPreservedVisualBools.Count == 0
                && !_pendingPreservedSizeMode.HasValue
                && _pendingPreservedInvalidValueCount == 0)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreservedFloat(
                "2. Reticles\nReticleSizePixels",
                _baseSizePixels,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedString("2. Reticles\nGeneralSprite", _general.SpriteFile, ref restoredCount);
            RestorePreservedString("2. Reticles\nBowSprite", _bow.SpriteFile, ref restoredCount);
            RestorePreservedString("2. Reticles\nMagicSprite", _magic.SpriteFile, ref restoredCount);
            RestorePreservedString("2. Reticles\nBloodMagicSprite", _bloodMagic.SpriteFile, ref restoredCount);
            RestorePreservedFloat("2. Reticles\nBowScale", _bow.ScaleMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Reticles\nMagicScale", _magic.ScaleMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("2. Reticles\nBloodMagicScale", _bloodMagic.ScaleMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedString("3. Colors and Opacity\nDefaultColor", _general.DefaultColor, ref restoredCount);
            RestorePreservedString("3. Colors and Opacity\nHostileColor", _general.HostileColor, ref restoredCount);
            RestorePreservedString("3. Colors and Opacity\nNonHostileColor", _general.NonHostileColor, ref restoredCount);
            RestorePreservedFloat("3. Colors and Opacity\nIdleOpacity", _defaultOpacity, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("3. Colors and Opacity\nTargetOpacity", _hostileOpacity, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("3. Colors and Opacity\nMountedOpacityMultiplier", _mountedOpacityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedBool("4. Blood Magic\nUseCorpseQualityScale", _bloodMagicUseQualityScale, ref restoredCount);
            RestorePreservedFloat("4. Blood Magic\nMaximumQualityScale", _bloodMagicMaximumQualityScale, ref restoredCount, ref clampedCount);
            RestorePreservedString("4. Blood Magic\nUsableCorpseColor", _bloodMagicUsableCorpseColor, ref restoredCount);
            if (_pendingPreservedSizeMode.HasValue)
            {
                bool clamped;
                if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _sizeMode,
                    _pendingPreservedSizeMode.Value,
                    out clamped))
                {
                    restoredCount++;
                }
                else
                {
                    _pendingPreservedInvalidValueCount++;
                }
            }
            RestorePreservedFloat("5. Advanced\nCrouchIndicatorOpacity", _crouchIndicatorOpacity, ref restoredCount, ref clampedCount);
            RestorePreservedFloat("5. Advanced\nCrouchIndicatorVerticalOffset", _crouchIndicatorVerticalOffset, ref restoredCount, ref clampedCount);

            Logger.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " reticle visual value(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + "; skippedInvalid="
                + _pendingPreservedInvalidValueCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            ClearPendingPreservedVisualProfile();
        }

        private void RestorePreservedFloat(
            string settingId,
            ConfigEntry<float> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            float preservedValue;
            if (entry == null
                || !_pendingPreservedVisualFloats.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
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

        private void RestorePreservedString(
            string settingId,
            ConfigEntry<string> entry,
            ref int restoredCount)
        {
            string preservedValue;
            if (entry == null
                || !_pendingPreservedVisualStrings.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
                out clamped))
            {
                restoredCount++;
            }
            else
            {
                _pendingPreservedInvalidValueCount++;
            }
        }

        private void RestorePreservedBool(
            string settingId,
            ConfigEntry<bool> entry,
            ref int restoredCount)
        {
            bool preservedValue;
            if (entry == null
                || !_pendingPreservedVisualBools.TryGetValue(settingId, out preservedValue))
            {
                return;
            }

            bool clamped;
            if (Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                preservedValue,
                out clamped))
            {
                restoredCount++;
            }
            else
            {
                _pendingPreservedInvalidValueCount++;
            }
        }

        private void ClearPendingPreservedVisualProfile()
        {
            _pendingPreservedVisualFloats.Clear();
            _pendingPreservedVisualStrings.Clear();
            _pendingPreservedVisualBools.Clear();
            _pendingPreservedSizeMode = null;
            _pendingPreservedInvalidValueCount = 0;
        }

        private ContextSettings BindContext(
            ReticleContext context,
            string name,
            string defaultSpriteFile,
            float defaultScale)
        {
            ContextSettings settings = new ContextSettings(context);
            string reticleSection = "2. Reticles";

            settings.SpriteFile = Config.Bind(
                reticleSection,
                name + "Sprite",
                defaultSpriteFile,
                "PNG beside this plugin, or an absolute PNG path. Bow and Magic fall back to the general PNG if unavailable.");

            if (context != ReticleContext.General)
            {
                settings.ScaleMultiplier = Config.Bind(
                    reticleSection,
                    name + "Scale",
                    defaultScale,
                    new ConfigDescription(
                        name + " scale multiplier applied after ReticleSizePixels.",
                        new AcceptableValueRange<float>(0.1f, 10f)));
            }

            if (context == ReticleContext.General)
            {
                settings.DefaultColor = Config.Bind(
                    "3. Colors and Opacity",
                    "DefaultColor",
                    "#FFFFFFFF",
                    "Default-state color in #RRGGBBAA format.");
                settings.HostileColor = Config.Bind(
                    "3. Colors and Opacity",
                    "HostileColor",
                    "#E8583CFF",
                    "Hostile-target color in #RRGGBBAA format.");
                settings.NonHostileColor = Config.Bind(
                    "3. Colors and Opacity",
                    "NonHostileColor",
                    "#8DD57AFF",
                    "Friendly and neutral-target color in #RRGGBBAA format.");
            }

            return settings;
        }

        private static ConfigDescription OpacityDescription(string text)
        {
            return new ConfigDescription(
                text,
                new AcceptableValueRange<float>(0f, 1f));
        }

        private void RegisterSettingHandlers()
        {
            _enabled.SettingChanged += OnBehaviorSettingChanged;
            _preset.SettingChanged += OnPresetSettingChanged;
            _magicDetection.SettingChanged += OnBehaviorSettingChanged;
            _useGeneralWhenHandsDown.SettingChanged += OnBehaviorSettingChanged;
            _bloodMagicCorpseReticleMode.SettingChanged += OnBehaviorSettingChanged;
            _bloodMagicRequireRelevantSpell.SettingChanged +=
                OnBehaviorSettingChanged;
            _bloodMagicUseQualityScale.SettingChanged +=
                OnBehaviorSettingChanged;
            _bloodMagicQualityCrosshairsEnabled.SettingChanged +=
                OnBehaviorSettingChanged;
            _bloodMagicMaximumQualityScale.SettingChanged +=
                OnBehaviorSettingChanged;
            _bloodMagicUsableCorpseColor.SettingChanged +=
                OnBehaviorSettingChanged;
            _sizeMode.SettingChanged += OnBehaviorSettingChanged;
            _textureFiltering.SettingChanged +=
                OnTextureFilteringSettingChanged;
            _baseSizePixels.SettingChanged += OnBehaviorSettingChanged;
            _targetDetectionRangeMultiplier.SettingChanged +=
                OnTargetDetectionRangeSettingChanged;
            _defaultOpacity.SettingChanged += OnBehaviorSettingChanged;
            _hostileOpacity.SettingChanged += OnBehaviorSettingChanged;
            if (!ReferenceEquals(_nonHostileOpacity, _hostileOpacity))
            {
                _nonHostileOpacity.SettingChanged += OnBehaviorSettingChanged;
            }
            _mountedOpacityMultiplier.SettingChanged += OnBehaviorSettingChanged;
            _steelAndBoneHitMarkersEnabled.SettingChanged +=
                OnHitMarkerSettingChanged;
            _killingBlowOverlaysEnabled.SettingChanged +=
                OnBehaviorSettingChanged;
            _hitMarkerSizeMultiplier.SettingChanged += OnBehaviorSettingChanged;
            _hitMarkerDamageOverTimeSizeMultiplier.SettingChanged +=
                OnBehaviorSettingChanged;
            _killingBlowSizeMultiplier.SettingChanged +=
                OnBehaviorSettingChanged;
            _hitMarkerDurationMultiplier.SettingChanged += OnBehaviorSettingChanged;
            _killingBlowDurationMultiplier.SettingChanged +=
                OnBehaviorSettingChanged;
            _showCrouchIndicator.SettingChanged += OnBehaviorSettingChanged;
            _crouchIndicatorOpacity.SettingChanged += OnBehaviorSettingChanged;
            _crouchIndicatorVerticalOffset.SettingChanged +=
                OnBehaviorSettingChanged;
            _hideDefaultReticle.SettingChanged += OnBehaviorSettingChanged;
            if (!ReferenceEquals(_hideMeleeReticle, _hideDefaultReticle))
            {
                _hideMeleeReticle.SettingChanged += OnBehaviorSettingChanged;
            }
            if (!ReferenceEquals(_hideBowReticle, _hideDefaultReticle))
            {
                _hideBowReticle.SettingChanged += OnBehaviorSettingChanged;
            }
            if (!ReferenceEquals(_hideItemSpecificReticles, _hideDefaultReticle))
            {
                _hideItemSpecificReticles.SettingChanged += OnBehaviorSettingChanged;
            }

            RegisterContextHandlers(_general);
            RegisterContextHandlers(_bow);
            RegisterContextHandlers(_magic);
            RegisterContextHandlers(_bloodMagic);
        }

        private void RegisterContextHandlers(ContextSettings settings)
        {
            settings.SpriteFile.SettingChanged += OnSpriteSettingChanged;
            if (settings.ScaleMultiplier != null)
            {
                settings.ScaleMultiplier.SettingChanged += OnBehaviorSettingChanged;
            }

            if (settings.DefaultColor != null)
            {
                settings.DefaultColor.SettingChanged += OnBehaviorSettingChanged;
                settings.HostileColor.SettingChanged += OnBehaviorSettingChanged;
                settings.NonHostileColor.SettingChanged += OnBehaviorSettingChanged;
            }

        }

        private void UnregisterSettingHandlers()
        {
            Unsubscribe(_enabled, OnBehaviorSettingChanged);
            Unsubscribe(_preset, OnPresetSettingChanged);
            Unsubscribe(_magicDetection, OnBehaviorSettingChanged);
            Unsubscribe(_useGeneralWhenHandsDown, OnBehaviorSettingChanged);
            Unsubscribe(_bloodMagicCorpseReticleMode, OnBehaviorSettingChanged);
            Unsubscribe(_bloodMagicRequireRelevantSpell, OnBehaviorSettingChanged);
            Unsubscribe(_bloodMagicUseQualityScale, OnBehaviorSettingChanged);
            Unsubscribe(
                _bloodMagicQualityCrosshairsEnabled,
                OnBehaviorSettingChanged);
            Unsubscribe(_bloodMagicMaximumQualityScale, OnBehaviorSettingChanged);
            Unsubscribe(_bloodMagicUsableCorpseColor, OnBehaviorSettingChanged);
            Unsubscribe(_sizeMode, OnBehaviorSettingChanged);
            Unsubscribe(_textureFiltering, OnTextureFilteringSettingChanged);
            Unsubscribe(_baseSizePixels, OnBehaviorSettingChanged);
            Unsubscribe(
                _targetDetectionRangeMultiplier,
                OnTargetDetectionRangeSettingChanged);
            Unsubscribe(_defaultOpacity, OnBehaviorSettingChanged);
            Unsubscribe(_hostileOpacity, OnBehaviorSettingChanged);
            if (!ReferenceEquals(_nonHostileOpacity, _hostileOpacity))
            {
                Unsubscribe(_nonHostileOpacity, OnBehaviorSettingChanged);
            }
            Unsubscribe(_mountedOpacityMultiplier, OnBehaviorSettingChanged);
            Unsubscribe(
                _steelAndBoneHitMarkersEnabled,
                OnHitMarkerSettingChanged);
            Unsubscribe(_killingBlowOverlaysEnabled, OnBehaviorSettingChanged);
            Unsubscribe(_hitMarkerSizeMultiplier, OnBehaviorSettingChanged);
            Unsubscribe(
                _hitMarkerDamageOverTimeSizeMultiplier,
                OnBehaviorSettingChanged);
            Unsubscribe(_killingBlowSizeMultiplier, OnBehaviorSettingChanged);
            Unsubscribe(_hitMarkerDurationMultiplier, OnBehaviorSettingChanged);
            Unsubscribe(_killingBlowDurationMultiplier, OnBehaviorSettingChanged);
            Unsubscribe(_showCrouchIndicator, OnBehaviorSettingChanged);
            Unsubscribe(_crouchIndicatorOpacity, OnBehaviorSettingChanged);
            Unsubscribe(_crouchIndicatorVerticalOffset, OnBehaviorSettingChanged);
            Unsubscribe(_hideDefaultReticle, OnBehaviorSettingChanged);
            if (!ReferenceEquals(_hideMeleeReticle, _hideDefaultReticle))
            {
                Unsubscribe(_hideMeleeReticle, OnBehaviorSettingChanged);
            }
            if (!ReferenceEquals(_hideBowReticle, _hideDefaultReticle))
            {
                Unsubscribe(_hideBowReticle, OnBehaviorSettingChanged);
            }
            if (!ReferenceEquals(_hideItemSpecificReticles, _hideDefaultReticle))
            {
                Unsubscribe(_hideItemSpecificReticles, OnBehaviorSettingChanged);
            }

            UnregisterContextHandlers(_general);
            UnregisterContextHandlers(_bow);
            UnregisterContextHandlers(_magic);
            UnregisterContextHandlers(_bloodMagic);
        }

        private void UnregisterContextHandlers(ContextSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            Unsubscribe(settings.SpriteFile, OnSpriteSettingChanged);
            Unsubscribe(settings.ScaleMultiplier, OnBehaviorSettingChanged);

            if (settings.DefaultColor != null)
            {
                Unsubscribe(settings.DefaultColor, OnBehaviorSettingChanged);
                Unsubscribe(settings.HostileColor, OnBehaviorSettingChanged);
                Unsubscribe(settings.NonHostileColor, OnBehaviorSettingChanged);
            }
        }

        private static void Unsubscribe<T>(
            ConfigEntry<T> entry,
            EventHandler handler)
        {
            if (entry != null)
            {
                entry.SettingChanged -= handler;
            }
        }

        private void ApplyPreset(ReticlePreset preset)
        {
            ApplyReticleState();
            RefreshVanillaCrosshair();
        }

        private static ReticleMode ModeForPreset(
            ReticlePreset preset,
            ReticleContext context)
        {
            switch (preset)
            {
                case ReticlePreset.AlwaysVisible:
                    return ReticleMode.AlwaysVisibleSmart;
                case ReticlePreset.TargetOnly:
                    return ReticleMode.TargetOnlySmart;
                case ReticlePreset.CombatReady:
                    return context == ReticleContext.General
                        ? ReticleMode.TargetOnlySmart
                        : ReticleMode.AlwaysVisibleSmart;
                case ReticlePreset.HostilesOnly:
                    return ReticleMode.HostilesOnly;
                default:
                    return ReticleMode.AlwaysVisibleSmart;
            }
        }

        private void PatchGame()
        {
            Type crosshairPartType = RequireType(
                "Awaken.TG.Main.Heroes.Crosshair.CrosshairPart");
            Type heroCrosshairType = RequireType(
                "Awaken.TG.Main.Heroes.Crosshair.HeroCrosshair");
            Type heroRaycasterType = RequireType(
                "Awaken.TG.Main.Heroes.Combat.VCHeroRaycaster");
            Type targetType = RequireType(
                "Awaken.TG.Main.Heroes.Crosshair.CrosshairTargetType");
            Type heroType = RequireType(
                "Awaken.TG.Main.Heroes.Hero");
            Type itemType = RequireType(
                "Awaken.TG.Main.Heroes.Items.Item");
            Type equipmentSlotType = RequireType(
                "Awaken.TG.Main.Heroes.Items.EquipmentSlotType");
            Type inventoryExtensionsType = RequireType(
                "Awaken.TG.Main.Character.CharacterInventoryExtension");

            MethodInfo setActiveMethod = RequireMethod(
                crosshairPartType,
                "SetActive",
                new[] { typeof(bool) });
            MethodInfo initializedMethod = RequireMethod(
                heroCrosshairType,
                "OnFullyInitialized",
                Type.EmptyTypes);
            _targetChangedMethod = RequireMethod(
                heroCrosshairType,
                "OnPointingTowardsLocationWithHP",
                null);
            MethodInfo perspectiveChangedMethod = RequireMethod(
                heroCrosshairType,
                "HeroPerspectiveChanged",
                Type.EmptyTypes);
            MethodInfo equipmentSlotsChangedMethod = RequireMethod(
                heroCrosshairType,
                "SetCrosshairForEquipmentSlots",
                new[] { typeof(bool) });
            MethodInfo equipmentItemChangedMethod = RequireMethod(
                heroCrosshairType,
                "SetCrosshairForEquipmentItem",
                new[] { equipmentSlotType, itemType });
            MethodInfo crosshairSettingChangedMethod = RequireMethod(
                heroCrosshairType,
                "OnCrosshairSettingChanged",
                Type.EmptyTypes);
            MethodInfo raycasterAttachedMethod = RequireMethod(
                heroRaycasterType,
                "OnAttach",
                Type.EmptyTypes);
            MethodInfo raycasterDiscardingMethod = RequireMethod(
                heroRaycasterType,
                "OnDiscard",
                Type.EmptyTypes);

            _refreshCrosshairMethod = RequireMethod(
                heroCrosshairType,
                "Refresh",
                Type.EmptyTypes);
            _getMainViewMethod = RequirePropertyGetter(crosshairPartType, "MainView");
            _getCurrentLocationTypeMethod = RequirePropertyGetter(
                heroCrosshairType,
                "CurrentLocationType");
            _getHeroMethod = RequirePropertyGetter(heroCrosshairType, "Hero");
            _getHeroItemsMethod = RequirePropertyGetter(heroType, "HeroItems");
            _weaponsVisibleGetter = RequirePropertyGetter(
                heroType,
                "WeaponsVisible");
            _mountedGetter = OptionalPropertyGetter(heroType, "Mounted");
            _equippedItemMethod = RequireStaticMethod(
                inventoryExtensionsType,
                "EquippedItem",
                2);
            _isRangedGetter = RequirePropertyGetter(itemType, "IsRanged");
            _isMagicGetter = RequirePropertyGetter(itemType, "IsMagic");
            _isCastMagicGetter = RequirePropertyGetter(itemType, "IsCastMagic");
            _allEquipmentSlotsField = RequireField(equipmentSlotType, "All");
            _npcDetectionMaxDistanceField = RequireField(
                heroRaycasterType,
                "npcDetectionMaxDistance");
            _defaultTargetType = RequireStaticField(targetType, "Default");
            _hostileTargetType = RequireStaticField(targetType, "Hostile");
            _nonHostileTargetType = RequireStaticField(targetType, "NonHostile");
            _loadImageMethod = ResolveLoadImageMethod();

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                setActiveMethod,
                new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.CrosshairPartSetActivePrefix)),
                new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.CrosshairPartSetActivePostfix)));
            _harmony.Patch(
                initializedMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.HeroCrosshairInitializedPostfix)));
            _harmony.Patch(
                _targetChangedMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.TargetChangedPostfix)));
            _harmony.Patch(
                perspectiveChangedMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.PerspectiveChangedPostfix)));
            _harmony.Patch(
                equipmentSlotsChangedMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.EquipmentChangedPostfix)));
            _harmony.Patch(
                equipmentItemChangedMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.EquipmentChangedPostfix)));
            _harmony.Patch(
                crosshairSettingChangedMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.EquipmentChangedPostfix)));
            _harmony.Patch(
                _refreshCrosshairMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.CrosshairRefreshedPostfix)));
            _harmony.Patch(
                raycasterAttachedMethod,
                postfix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.HeroRaycasterAttachedPostfix)));
            _harmony.Patch(
                raycasterDiscardingMethod,
                prefix: new HarmonyMethod(
                    typeof(DishonoredDynamicCrosshairPatches),
                    nameof(DishonoredDynamicCrosshairPatches.HeroRaycasterDiscardingPrefix)));
        }

        internal void FilterVanillaPartActivation(object part, ref bool active)
        {
            if (!_enabled.Value || part == null)
            {
                return;
            }

            string partName = part.GetType().Name;
            if (partName == "CrouchCrosshairPart")
            {
                if (!_showCrouchIndicator.Value)
                {
                    active = false;
                }

                return;
            }

            if ((partName == "DefaultCrosshairPart" && _hideDefaultReticle.Value)
                || (partName == "MeleeCrosshairPart" && _hideMeleeReticle.Value)
                || (partName == "BowCrosshairPart" && _hideBowReticle.Value)
                || (partName == "CustomCrosshairPart"
                    && _hideItemSpecificReticles.Value))
            {
                active = false;
            }
        }

        internal void ObserveCrosshairPart(object part)
        {
            if (part == null || _getMainViewMethod == null)
            {
                return;
            }

            try
            {
                Component mainView = _getMainViewMethod.Invoke(part, null) as Component;
                if (mainView == null)
                {
                    return;
                }

                if (part.GetType().Name == "CrouchCrosshairPart")
                {
                    AttachCrouchIndicator(mainView.gameObject);
                }

                if (mainView.transform.parent != null)
                {
                    EnsureReticle(mainView.transform.parent);
                }
            }
            catch (Exception exception)
            {
                if (!_mainViewFailureLogged)
                {
                    _mainViewFailureLogged = true;
                    Logger.LogWarning(
                        "Could not attach to the crosshair UI: " + exception.Message);
                }
            }
        }

        internal void OnHeroCrosshairInitialized(object heroCrosshair)
        {
            _heroCrosshair = heroCrosshair;
            ApplyReticleState();
        }

        internal void OnTargetChanged(
            object heroCrosshair,
            object targetLocation)
        {
            _heroCrosshair = heroCrosshair;
            _currentTargetLocation = targetLocation;
            ApplyReticleState(
                ReadCurrentContext(),
                targetLocation == null
                    ? TargetState.Default
                    : ReadCurrentTargetState());
        }

        internal void OnHeroRaycasterAttached(object heroRaycaster)
        {
            if (heroRaycaster == null)
            {
                return;
            }

            RestoreTargetDetectionRange();
            _heroRaycaster = heroRaycaster;

            try
            {
                _originalNpcDetectionMaxDistance = Convert.ToSingle(
                    _npcDetectionMaxDistanceField.GetValue(heroRaycaster));
                _hasOriginalNpcDetectionMaxDistance = true;
                ApplyTargetDetectionRange();
            }
            catch (Exception exception)
            {
                LogRaycasterRangeFailure(
                    "Could not apply the NPC target-detection range multiplier: ",
                    exception);
            }
        }

        internal void OnHeroRaycasterDiscarding(object heroRaycaster)
        {
            if (ReferenceEquals(_heroRaycaster, heroRaycaster))
            {
                RestoreTargetDetectionRange();
            }
        }

        internal void OnCrosshairChanged(object heroCrosshair)
        {
            _heroCrosshair = heroCrosshair;
            ApplyReticleState();
        }

        internal void OnPerspectiveChanged(object heroCrosshair)
        {
            _heroCrosshair = heroCrosshair;
            ApplyReticleLayout(ReadCurrentContext());
            ApplyReticleState();
        }

        private void EnsureReticle(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            if (_reticleObject != null && _crosshairParent == parent)
            {
                _reticleRect.SetAsLastSibling();
                return;
            }

            DestroyReticleObject();
            _crosshairParent = parent;

            _reticleObject = new GameObject(
                "DishonoredDynamicCrosshair",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _reticleObject.hideFlags = HideFlags.DontSave;
            _reticleObject.layer = parent.gameObject.layer;

            _reticleRect = _reticleObject.GetComponent<RectTransform>();
            _reticleRect.SetParent(parent, false);
            _reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
            _reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            _reticleRect.pivot = new Vector2(0.5f, 0.5f);

            _reticleImage = _reticleObject.GetComponent<Image>();
            _reticleImage.raycastTarget = false;
            _reticleImage.preserveAspect = true;
            _reticleImage.type = Image.Type.Simple;
            _directHitMarkerImage = CreateHitMarkerOverlayImage(
                _reticleObject.transform,
                "DishonoredDirectHitMarkerOverlay");
            _weakSpotHitMarkerImage = CreateHitMarkerOverlayImage(
                _reticleObject.transform,
                "DishonoredWeakSpotHitMarkerOverlay");
            _criticalHitMarkerImage = CreateHitMarkerOverlayImage(
                _reticleObject.transform,
                "DishonoredCriticalHitMarkerOverlay");
            _killingBlowHitMarkerImage = CreateHitMarkerOverlayImage(
                _reticleObject.transform,
                "DishonoredKillingBlowHitMarkerOverlay");
            _killingBlowHitMarkerImage.transform.SetAsLastSibling();

            ApplyReticleState();
        }

        private static Image CreateHitMarkerOverlayImage(
            Transform parent,
            string name)
        {
            GameObject overlayObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.hideFlags = HideFlags.DontSave;
            overlayObject.layer = parent.gameObject.layer;

            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            Image image = overlayObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            image.enabled = false;
            return image;
        }

        private void ApplyReticleLayout(ReticleContext context)
        {
            if (_reticleRect == null)
            {
                return;
            }

            ContextSettings settings = SettingsFor(context);
            float baseSize = Mathf.Clamp(_baseSizePixels.Value, 4f, 256f);
            float generalScale = 1f;
            float magicScale = 1f;
            float contextScale = 1f;
            float scale = generalScale;
            float bloodMagicQualityScale = 1f;

            if (context == ReticleContext.BloodMagic)
            {
                magicScale = ContextScale(_magic);
                scale *= magicScale;

                contextScale = ContextScale(settings);
                scale *= contextScale;

                bloodMagicQualityScale = GetBloodMagicQualityScale();
                scale *= bloodMagicQualityScale;
            }
            else if (context != ReticleContext.General)
            {
                contextScale = ContextScale(settings);
                scale *= contextScale;
            }

            float canvasScaleFactor = GetCanvasScaleFactor();
            float unitConversion = _sizeMode.Value
                == ReticleSizeMode.ScreenPixels
                    ? 1f / canvasScaleFactor
                    : 1f;
            float hitMarkerSizeMultiplier = _activeKillingBlowTier >= 1
                ? _killingBlowSizeMultiplier.Value
                : _activeHitMarkerDamageOverTime
                    ? _hitMarkerDamageOverTimeSizeMultiplier.Value
                    : _hitMarkerSizeMultiplier.Value;
            float finalSize = IsHitMarkerActive()
                ? baseSize
                    * Mathf.Clamp(hitMarkerSizeMultiplier, 0.5f, 3f)
                    * unitConversion
                : baseSize * scale * unitConversion;

            _reticleRect.sizeDelta = new Vector2(finalSize, finalSize);
            _reticleRect.localScale = Vector3.one;
            _reticleRect.anchoredPosition = Vector2.zero;
            _reticleRect.localRotation = Quaternion.identity;
            _reticleRect.SetAsLastSibling();
            _lastCanvasScaleFactor = canvasScaleFactor;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _currentBloodMagicQualityScale = context == ReticleContext.BloodMagic
                ? bloodMagicQualityScale
                : 1f;

            LogBloodMagicScaleDiagnostics(
                context,
                baseSize,
                generalScale,
                magicScale,
                contextScale,
                bloodMagicQualityScale,
                scale,
                unitConversion,
                finalSize,
                canvasScaleFactor);
        }

        private static float ContextScale(ContextSettings settings)
        {
            return settings == null || settings.ScaleMultiplier == null
                ? 1f
                : Mathf.Clamp(settings.ScaleMultiplier.Value, 0.1f, 10f);
        }

        private void LogBloodMagicScaleDiagnostics(
            ReticleContext context,
            float baseSize,
            float generalScale,
            float magicScale,
            float bloodMagicScale,
            float qualityScale,
            float finalScale,
            float unitConversion,
            float finalSize,
            float canvasScaleFactor)
        {
            if (context != ReticleContext.BloodMagic
                || _bloodMagicLogScaleDiagnostics == null
                || !_bloodMagicLogScaleDiagnostics.Value)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < _nextBloodMagicScaleDiagnosticLogTime)
            {
                return;
            }

            _nextBloodMagicScaleDiagnosticLogTime = now + 1f;

            Logger.LogInfo(
                "BloodMagic reticle scale sample: state="
                + ((BloodMagicFocusedCorpseState)_lastBloodMagicCorpseState).ToString()
                + "; corpseQuality=" + _lastBloodMagicCorpseQuality01.ToString("0.###", CultureInfo.InvariantCulture)
                + "; qualityVisualT=" + GetBloodMagicQualityVisualT().ToString("0.###", CultureInfo.InvariantCulture)
                + "; qualityDeadZone=" + GetBloodMagicQualityDeadZone().ToString("0.###", CultureInfo.InvariantCulture)
                + "; qualityCurveExponent=" + GetBloodMagicQualityCurveExponent().ToString("0.###", CultureInfo.InvariantCulture)
                + "; generalScale=" + generalScale.ToString("0.###", CultureInfo.InvariantCulture)
                + "; magicScale=" + magicScale.ToString("0.###", CultureInfo.InvariantCulture)
                + "; bloodMagicScale=" + bloodMagicScale.ToString("0.###", CultureInfo.InvariantCulture)
                + "; qualityScale=" + qualityScale.ToString("0.###", CultureInfo.InvariantCulture)
                + "; finalScale=" + finalScale.ToString("0.###", CultureInfo.InvariantCulture)
                + "; baseSize=" + baseSize.ToString("0.###", CultureInfo.InvariantCulture)
                + "; unitConversion=" + unitConversion.ToString("0.###", CultureInfo.InvariantCulture)
                + "; finalSize=" + finalSize.ToString("0.###", CultureInfo.InvariantCulture)
                + "; canvasScaleFactor=" + canvasScaleFactor.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
        }

        private float GetCanvasScaleFactor()
        {
            if (_reticleRect == null)
            {
                return 1f;
            }

            Canvas canvas = _reticleRect.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return 1f;
            }

            Canvas rootCanvas = canvas.rootCanvas;
            float scaleFactor = rootCanvas != null
                ? rootCanvas.scaleFactor
                : canvas.scaleFactor;
            return scaleFactor > 0.0001f ? scaleFactor : 1f;
        }

        private void ApplyReticleState()
        {
            ApplyReticleState(ReadCurrentContext());
        }

        private void ApplyReticleState(ReticleContext context)
        {
            ApplyReticleState(context, ReadCurrentTargetState());
        }

        private void ApplyReticleState(
            ReticleContext context,
            TargetState targetState)
        {
            if (_enabled == null || !_enabled.Value)
            {
                _hitMarkerActive = false;
                _lastBloodMagicCorpseActive = false;
                _lastBloodMagicCorpseState = 0;
                _lastBloodMagicCorpseQualityTier = 0;
                _currentBloodMagicCorpseState = 0;
                _currentBloodMagicCorpseQualityTier = 0;
                ApplyCrouchIndicatorState();
                if (_reticleImage != null)
                {
                    _reticleImage.enabled = false;
                }
                SetHitMarkerOverlaysEnabled(false, false, false, false);

                return;
            }

            bool bloodMagicActive = ReadBloodMagicCorpseActive();
            ReticleContext displayContext = bloodMagicActive
                ? ReticleContext.BloodMagic
                : context;
            TargetState displayTargetState = bloodMagicActive
                ? BloodMagicCorpseUsesUsableVisuals(_lastBloodMagicCorpseState)
                    ? TargetState.Hostile
                    : TargetState.Default
                : targetState;

            _currentContext = displayContext;
            _currentTargetState = displayTargetState;
            _currentBloodMagicCorpseState = bloodMagicActive
                ? _lastBloodMagicCorpseState
                : 0;
            _currentBloodMagicCorpseQualityTier = bloodMagicActive
                ? _lastBloodMagicCorpseQualityTier
                : 0;
            ApplyCrouchIndicatorState();

            if (_reticleObject == null || _reticleImage == null)
            {
                return;
            }

            if (IsHitMarkerActive())
            {
                ApplyHitMarkerVisual();
                return;
            }

            SetHitMarkerOverlaysEnabled(false, false, false, false);

            ContextSettings settings = SettingsFor(displayContext);
            Sprite sprite = settings.Asset.Sprite;
            if (bloodMagicActive)
            {
                sprite = ResolveBloodMagicQualitySprite(sprite);
            }
            if (sprite == null && displayContext != ReticleContext.General)
            {
                sprite = _general.Asset.Sprite;
            }

            if (sprite == null)
            {
                _reticleImage.enabled = false;
                return;
            }

            ReticleMode mode = bloodMagicActive
                ? ReticleMode.AlwaysVisibleSmart
                : ResolveMode(settings);
            bool visible = bloodMagicActive
                || IsVisible(mode, settings, displayTargetState);
            ContextSettings colorSettings = bloodMagicActive
                ? ColorSettingsFor(ReticleContext.Magic, _magic)
                : ColorSettingsFor(displayContext, settings);

            string colorText = ColorFor(
                colorSettings,
                displayTargetState);
            if (bloodMagicActive)
            {
                colorText = ColorForBloodMagicCorpseState(colorText);
            }

            Color color = ParseColor(colorText);
            color.a *= OpacityFor(displayTargetState);
            bool heroMounted = ReadHeroMounted();
            RememberHeroMounted(heroMounted);
            if (heroMounted)
            {
                color.a *= Mathf.Clamp01(_mountedOpacityMultiplier.Value);
            }

            _reticleObject.SetActive(true);
            _reticleImage.sprite = sprite;
            _reticleImage.color = color;
            _reticleImage.enabled = visible && color.a > 0f;
            ApplyReticleLayout(displayContext);
        }

        private ContextSettings ColorSettingsFor(
            ReticleContext context,
            ContextSettings settings)
        {
            if (settings == null || settings.DefaultColor == null)
            {
                return _general;
            }

            return settings;
        }

        private ReticleMode ResolveMode(ContextSettings settings)
        {
            return ModeForPreset(_preset.Value, settings.Context);
        }

        private static bool IsVisible(
            ReticleMode mode,
            ContextSettings settings,
            TargetState targetState)
        {
            switch (mode)
            {
                case ReticleMode.AlwaysVisibleSmart:
                    return true;
                case ReticleMode.TargetOnlySmart:
                    return targetState != TargetState.Default;
                case ReticleMode.HostilesOnly:
                    return targetState == TargetState.Hostile;
                default:
                    return false;
            }
        }

        private static string ColorFor(
            ContextSettings settings,
            TargetState targetState)
        {
            if (targetState == TargetState.Hostile)
            {
                return settings.HostileColor.Value;
            }

            if (targetState == TargetState.NonHostile)
            {
                return settings.NonHostileColor.Value;
            }

            return settings.DefaultColor.Value;
        }

        private float OpacityFor(TargetState targetState)
        {
            if (targetState == TargetState.Hostile)
            {
                return Mathf.Clamp01(_hostileOpacity.Value);
            }

            if (targetState == TargetState.NonHostile)
            {
                return Mathf.Clamp01(_nonHostileOpacity.Value);
            }

            return Mathf.Clamp01(_defaultOpacity.Value);
        }

        private bool ReadBloodMagicCorpseActive()
        {
            if (_enabled == null
                || !_enabled.Value
                || _bloodMagicCorpseReticleMode == null
                || _bloodMagicCorpseReticleMode.Value == BloodMagicCorpseReticleMode.Off)
            {
                _lastBloodMagicCorpseActive = false;
                _lastBloodMagicCorpseState = 0;
                _lastBloodMagicCorpseQuality01 = 0.5f;
                _lastBloodMagicCorpseQualityTier = 0;
                return false;
            }

            float now = Time.unscaledTime;
            if (now < _nextBloodMagicCheckTime)
            {
                return _lastBloodMagicCorpseActive;
            }

            _nextBloodMagicCheckTime = now + 0.15f;
            _lastBloodMagicCorpseActive = QueryBloodMagicCorpseInterop();
            return _lastBloodMagicCorpseActive;
        }

        private bool QueryBloodMagicCorpseInterop()
        {
            if (!ResolveBloodMagicCorpseApi())
            {
                return false;
            }

            try
            {
                if (_bloodMagicGetCorpseStateMethod != null)
                {
                    _lastBloodMagicCorpseState = Convert.ToInt32(
                        _bloodMagicGetCorpseStateMethod.Invoke(
                            null,
                            new object[]
                            {
                                _bloodMagicRequireRelevantSpell.Value
                            }));
                    bool active = BloodMagicCorpseStateShowsReticle(_lastBloodMagicCorpseState);
                    _lastBloodMagicCorpseQuality01 = BloodMagicCorpseUsesQualityScale(_lastBloodMagicCorpseState)
                        ? QueryBloodMagicCorpseQuality()
                        : 0f;
                    _lastBloodMagicCorpseQualityTier = active
                        ? QueryBloodMagicCorpseQualityTier()
                        : 0;
                    return active;
                }
            }
            catch (Exception exception)
            {
                if (!_bloodMagicApiFailureLogged)
                {
                    _bloodMagicApiFailureLogged = true;
                    Exception cause = exception is TargetInvocationException
                        && exception.InnerException != null
                            ? exception.InnerException
                            : exception;
                    Logger.LogWarning(
                        "Blood Magic corpse reticle integration failed: "
                        + cause.Message);
                }
            }

            return false;
        }

        private bool BloodMagicCorpseStateShowsReticle(int state)
        {
            return state == (int)BloodMagicFocusedCorpseState.Usable
                || state == (int)BloodMagicFocusedCorpseState.Channeling
                || state == (int)BloodMagicFocusedCorpseState.Spent
                || state == (int)BloodMagicFocusedCorpseState.Blocked;
        }

        private bool BloodMagicCorpseUsesUsableVisuals(int state)
        {
            return state == (int)BloodMagicFocusedCorpseState.Usable
                || state == (int)BloodMagicFocusedCorpseState.Channeling;
        }

        private bool BloodMagicCorpseUsesQualityScale(int state)
        {
            return BloodMagicCorpseUsesUsableVisuals(state);
        }

        private float QueryBloodMagicCorpseQuality()
        {
            if (_bloodMagicGetCorpseQualityMethod == null)
            {
                return 0.5f;
            }

            object result = _bloodMagicGetCorpseQualityMethod.Invoke(null, null);
            if (result == null)
            {
                return 0.5f;
            }

            try
            {
                return Mathf.Clamp01(Convert.ToSingle(result));
            }
            catch
            {
                return 0.5f;
            }
        }

        private int QueryBloodMagicCorpseQualityTier()
        {
            if (_bloodMagicGetCorpseQualityTierMethod == null)
            {
                return 0;
            }

            try
            {
                object result = _bloodMagicGetCorpseQualityTierMethod.Invoke(
                    null,
                    null);
                return result == null ? 0 : Mathf.Clamp(Convert.ToInt32(result), 0, 4);
            }
            catch
            {
                return 0;
            }
        }

        private Sprite ResolveBloodMagicQualitySprite(Sprite fallback)
        {
            if (_bloodMagicQualityCrosshairsEnabled == null
                || !_bloodMagicQualityCrosshairsEnabled.Value)
            {
                return fallback;
            }

            ReticleAsset asset;
            return _bloodMagicQualityAssets.TryGetValue(
                _lastBloodMagicCorpseQualityTier,
                out asset)
                && asset.Sprite != null
                    ? asset.Sprite
                    : fallback;
        }

        private float GetBloodMagicQualityScale()
        {
            if (_bloodMagicUseQualityScale == null || !_bloodMagicUseQualityScale.Value)
            {
                return 1f;
            }

            if (!BloodMagicCorpseUsesQualityScale(_lastBloodMagicCorpseState))
            {
                return 1f;
            }

            float min = 1f;
            float max = _bloodMagicMaximumQualityScale == null
                ? 2f
                : Mathf.Clamp(_bloodMagicMaximumQualityScale.Value, 0.1f, 5f);
            if (max < min)
            {
                max = min;
            }

            return Mathf.Lerp(min, max, GetBloodMagicQualityVisualT());
        }

        private float GetBloodMagicQualityVisualT()
        {
            if (_bloodMagicUseQualityScale == null || !_bloodMagicUseQualityScale.Value)
            {
                return 0f;
            }

            float deadZone = GetBloodMagicQualityDeadZone();
            float denominator = Mathf.Max(0.0001f, 1f - deadZone);
            float normalized = Mathf.Clamp01(
                (_lastBloodMagicCorpseQuality01 - deadZone) / denominator);
            return Mathf.Pow(normalized, GetBloodMagicQualityCurveExponent());
        }

        private float GetBloodMagicQualityDeadZone()
        {
            return 0.20f;
        }

        private float GetBloodMagicQualityCurveExponent()
        {
            return 1.8f;
        }

        private string ColorForBloodMagicCorpseState(string fallback)
        {
            if (BloodMagicCorpseUsesUsableVisuals(_lastBloodMagicCorpseState))
            {
                return _bloodMagicUsableCorpseColor == null
                    ? fallback
                    : _bloodMagicUsableCorpseColor.Value;
            }

            return _general == null || _general.DefaultColor == null
                ? fallback
                : _general.DefaultColor.Value;
        }

        private bool ResolveBloodMagicCorpseApi()
        {
            if (_bloodMagicGetCorpseStateMethod != null)
            {
                return true;
            }

            if (_bloodMagicApiUnavailableForSession)
            {
                return false;
            }

            float now = Time.unscaledTime;
            if (now < _nextBloodMagicApiResolveTime)
            {
                return false;
            }

            _nextBloodMagicApiResolveTime = now + 0.5f;
            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                BloodMagicExpansionPluginGuid,
                out pluginInfo)
                || pluginInfo == null)
            {
                _bloodMagicApiUnavailableForSession = true;
                LogBloodMagicApiUnavailable(
                    "Blood Magic Expansion is not loaded; optional blood-magic corpse reticle integration is inactive for this session.");
                return false;
            }

            BaseUnityPlugin plugin = pluginInfo.Instance as BaseUnityPlugin;
            if (plugin == null)
            {
                return false;
            }

            Type apiType = plugin.GetType().Assembly.GetType(
                BloodMagicExpansionApiTypeName,
                false);
            if (apiType == null)
            {
                _bloodMagicApiUnavailableForSession = true;
                LogBloodMagicApiUnavailable(
                    "Blood Magic Expansion is loaded, but its API type was not found; optional blood-magic corpse reticle integration is inactive for this session.");
                return false;
            }

            FieldInfo apiVersionField = apiType.GetField(
                "ApiVersion",
                BindingFlags.Public | BindingFlags.Static);
            if (apiVersionField == null
                || !object.Equals(apiVersionField.GetRawConstantValue(), 9))
            {
                _bloodMagicApiUnavailableForSession = true;
                LogBloodMagicApiUnavailable(
                    "Blood Magic Expansion is loaded, but API v9 is unavailable; optional blood-magic corpse reticle integration is inactive for this session.");
                return false;
            }

            _bloodMagicGetCorpseStateMethod = AccessTools.Method(
                apiType,
                "GetFocusedCorpseState",
                new[] { typeof(bool) });
            _bloodMagicGetCorpseQualityMethod = AccessTools.Method(
                apiType,
                "GetFocusedCorpseQuality01",
                new Type[0]);
            _bloodMagicGetCorpseQualityTierMethod = AccessTools.Method(
                apiType,
                "GetFocusedCorpseQualityTier",
                new Type[0]);
            if ((_bloodMagicGetCorpseStateMethod == null
                    || _bloodMagicGetCorpseQualityMethod == null
                    || _bloodMagicGetCorpseQualityTierMethod == null)
                && !_bloodMagicApiFailureLogged)
            {
                _bloodMagicApiFailureLogged = true;
                _bloodMagicApiUnavailableForSession = true;
                Logger.LogWarning(
                    "Blood Magic corpse reticle integration found Blood Magic Expansion but not its status API.");
            }

            return _bloodMagicGetCorpseStateMethod != null
                && _bloodMagicGetCorpseQualityMethod != null
                && _bloodMagicGetCorpseQualityTierMethod != null;
        }

        private void LogBloodMagicApiUnavailable(string message)
        {
            if (_bloodMagicApiUnavailableLogged)
            {
                return;
            }

            _bloodMagicApiUnavailableLogged = true;
            Logger.LogInfo(message);
        }

        private bool ResolveSteelAndBoneHitFeedbackApi()
        {
            if (_steelAndBoneHitResolvedEvent != null
                && _steelAndBoneHitResolvedHandler != null
                && _steelAndBoneKillingBlowResolvedEvent != null
                && _steelAndBoneKillingBlowResolvedHandler != null)
            {
                return true;
            }

            if (_steelAndBoneApiUnavailableForSession
                || _steelAndBoneHitMarkersEnabled == null
                || !_steelAndBoneHitMarkersEnabled.Value)
            {
                return false;
            }

            float now = Time.unscaledTime;
            if (now < _nextSteelAndBoneApiResolveTime)
            {
                return false;
            }

            _nextSteelAndBoneApiResolveTime = now + 0.5f;
            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    SteelAndBonePluginGuid,
                    out pluginInfo)
                || pluginInfo == null)
            {
                _steelAndBoneApiUnavailableForSession = true;
                return false;
            }

            BaseUnityPlugin plugin = pluginInfo.Instance as BaseUnityPlugin;
            if (plugin == null)
            {
                return false;
            }

            Type apiType = plugin.GetType().Assembly.GetType(
                SteelAndBoneHitFeedbackApiTypeName,
                false);
            FieldInfo apiVersionField = apiType == null
                ? null
                : apiType.GetField(
                    "ApiVersion",
                    BindingFlags.Public | BindingFlags.Static);
            if (apiVersionField == null
                || !object.Equals(apiVersionField.GetRawConstantValue(), 5))
            {
                _steelAndBoneApiUnavailableForSession = true;
                LogSteelAndBoneApiFailure(
                    "Steel and Bone is loaded, but hit-feedback API v5 is unavailable.");
                return false;
            }

            EventInfo hitResolvedEvent = apiType == null
                ? null
                : apiType.GetEvent(
                    "HitResolved",
                    BindingFlags.Public | BindingFlags.Static);
            if (hitResolvedEvent == null || hitResolvedEvent.EventHandlerType == null)
            {
                _steelAndBoneApiUnavailableForSession = true;
                LogSteelAndBoneApiFailure(
                    "Steel and Bone is loaded, but its hit-feedback API is unavailable.");
                return false;
            }

            EventInfo killingBlowResolvedEvent = apiType.GetEvent(
                "KillingBlowResolved",
                BindingFlags.Public | BindingFlags.Static);
            if (killingBlowResolvedEvent == null
                || killingBlowResolvedEvent.EventHandlerType == null)
            {
                _steelAndBoneApiUnavailableForSession = true;
                LogSteelAndBoneApiFailure(
                    "Steel and Bone is loaded, but its killing-blow event is unavailable.");
                return false;
            }

            try
            {
                MethodInfo handlerMethod = GetType().GetMethod(
                    nameof(OnSteelAndBoneHitResolved),
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Delegate handler = Delegate.CreateDelegate(
                    hitResolvedEvent.EventHandlerType,
                    this,
                    handlerMethod);
                MethodInfo killingBlowHandlerMethod = GetType().GetMethod(
                    nameof(OnSteelAndBoneKillingBlowResolved),
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Delegate killingBlowHandler = Delegate.CreateDelegate(
                    killingBlowResolvedEvent.EventHandlerType,
                    this,
                    killingBlowHandlerMethod);
                hitResolvedEvent.AddEventHandler(null, handler);
                _steelAndBoneHitResolvedEvent = hitResolvedEvent;
                _steelAndBoneHitResolvedHandler = handler;
                killingBlowResolvedEvent.AddEventHandler(
                    null,
                    killingBlowHandler);
                _steelAndBoneKillingBlowResolvedEvent = killingBlowResolvedEvent;
                _steelAndBoneKillingBlowResolvedHandler = killingBlowHandler;
                Logger.LogInfo(
                    "Steel and Bone hit-marker and killing-blow integration is active.");
                return true;
            }
            catch (Exception exception)
            {
                UnsubscribeSteelAndBoneHitFeedback();
                _steelAndBoneApiUnavailableForSession = true;
                LogSteelAndBoneApiFailure(
                    "Could not subscribe to Steel and Bone hit feedback: "
                    + exception.Message);
                return false;
            }
        }

        private void OnSteelAndBoneHitResolved(
            float effectivenessMultiplier,
            float visualEffectivenessMultiplier,
            bool immune,
            bool critical,
            bool weakSpot,
            bool damageOverTime,
            string color,
            float damageNumberDurationSeconds)
        {
            if (_enabled == null
                || !_enabled.Value
                || _steelAndBoneHitMarkersEnabled == null
                || !_steelAndBoneHitMarkersEnabled.Value)
            {
                return;
            }

            float durationMultiplier = Mathf.Clamp(
                _hitMarkerDurationMultiplier.Value,
                0.1f,
                2f);
            float duration = Mathf.Clamp(
                damageNumberDurationSeconds,
                0.05f,
                10f)
                * durationMultiplier;

            _activeHitMarkerFrame = ResolveHitMarkerFrame(
                visualEffectivenessMultiplier,
                immune);
            _activeHitMarkerCritical = critical;
            _activeHitMarkerWeakSpot = weakSpot;
            _activeHitMarkerDamageOverTime = damageOverTime;
            _activeKillingBlowTier = 0;
            _activeHitMarkerColor = ParseColor(color);
            _activeHitMarkerStartedAt = Time.unscaledTime;
            _activeHitMarkerEndsAt = _activeHitMarkerStartedAt + duration;
            _hitMarkerActive = true;
            ApplyReticleState();
        }

        private void OnSteelAndBoneKillingBlowResolved(
            int tier,
            float quality01,
            float visualEffectiveness,
            bool immune,
            bool critical,
            bool weakSpot,
            bool damageOverTime,
            string color,
            float duration)
        {
            if (_enabled == null
                || !_enabled.Value
                || _steelAndBoneHitMarkersEnabled == null
                || !_steelAndBoneHitMarkersEnabled.Value)
            {
                return;
            }

            float normalDurationMultiplier = Mathf.Clamp(
                _hitMarkerDurationMultiplier.Value,
                0.1f,
                2f);
            float killingBlowDurationMultiplier = _killingBlowDurationMultiplier == null
                ? 1.5f
                : Mathf.Clamp(_killingBlowDurationMultiplier.Value, 0.1f, 3f);

            _activeHitMarkerFrame = ResolveHitMarkerFrame(
                visualEffectiveness,
                immune);
            _activeHitMarkerCritical = critical;
            _activeHitMarkerWeakSpot = weakSpot;
            _activeHitMarkerDamageOverTime = damageOverTime;
            _activeKillingBlowTier = Mathf.Clamp(tier, 0, 4);
                _activeHitMarkerColor = new Color32(0x8C, 0x00, 0x03, 0xFF);
            _activeHitMarkerStartedAt = Time.unscaledTime;
            _activeHitMarkerEndsAt = _activeHitMarkerStartedAt
                + Mathf.Clamp(duration, 0.05f, 10f)
                * normalDurationMultiplier
                * killingBlowDurationMultiplier
                * GetKillingBlowTierDurationMultiplier(_activeKillingBlowTier);
            _hitMarkerActive = true;
            ApplyReticleState();
        }

        private static float GetKillingBlowTierDurationMultiplier(int tier)
        {
            switch (tier)
            {
                case 2: return 1.33f;
                case 3: return 1.67f;
                case 4: return 2.00f;
                default: return 1.0f;
            }
        }

        private static HitMarkerFrame ResolveHitMarkerFrame(
            float effectivenessMultiplier,
            bool immune)
        {
            if (immune)
            {
                return HitMarkerFrame.Blocked;
            }

            if (float.IsNaN(effectivenessMultiplier)
                || float.IsInfinity(effectivenessMultiplier))
            {
                return HitMarkerFrame.Neutral;
            }

            if (effectivenessMultiplier < 0.35f)
            {
                return HitMarkerFrame.ExtremeResistance;
            }
            if (effectivenessMultiplier < 0.70f)
            {
                return HitMarkerFrame.StrongResistance;
            }
            if (effectivenessMultiplier < 0.95f)
            {
                return HitMarkerFrame.MildResistance;
            }
            if (effectivenessMultiplier <= 1.05f)
            {
                return HitMarkerFrame.Neutral;
            }
            if (effectivenessMultiplier <= 1.10f)
            {
                return HitMarkerFrame.MildWeakness;
            }
            if (effectivenessMultiplier <= 1.20f)
            {
                return HitMarkerFrame.StrongWeakness;
            }

            return HitMarkerFrame.ExtremeWeakness;
        }

        private bool IsHitMarkerActive()
        {
            if (!_hitMarkerActive
                || _steelAndBoneHitMarkersEnabled == null
                || !_steelAndBoneHitMarkersEnabled.Value)
            {
                _hitMarkerActive = false;
                return false;
            }

            if (Time.unscaledTime < _activeHitMarkerEndsAt)
            {
                return true;
            }

            _hitMarkerActive = false;
            return false;
        }

        private void ApplyHitMarkerVisual()
        {
            ReticleAsset baseAsset;
            _hitMarkerAssets.TryGetValue(_activeHitMarkerFrame, out baseAsset);
            Sprite sprite = baseAsset == null ? null : baseAsset.Sprite;
            if (sprite == null
                && _hitMarkerAssets.TryGetValue(
                    HitMarkerFrame.Neutral,
                    out baseAsset))
            {
                sprite = baseAsset.Sprite;
            }

            if (sprite == null)
            {
                _reticleImage.enabled = false;
                SetHitMarkerOverlaysEnabled(false, false, false, false);
                return;
            }

            _reticleObject.SetActive(true);
            _reticleImage.sprite = sprite;
            _reticleImage.enabled = true;
            ApplyHitMarkerLayer(
                _directHitMarkerImage,
                _directHitMarkerOverlay.Sprite,
                !_activeHitMarkerDamageOverTime
                    && _activeKillingBlowTier == 0);
            ApplyHitMarkerLayer(
                _weakSpotHitMarkerImage,
                _weakSpotHitMarkerOverlay.Sprite,
                _activeHitMarkerWeakSpot);
            ApplyHitMarkerLayer(
                _criticalHitMarkerImage,
                _criticalHitMarkerOverlay.Sprite,
                _activeHitMarkerCritical);
            ReticleAsset killingBlowAsset;
            _killingBlowOverlayAssets.TryGetValue(
                _activeKillingBlowTier,
                out killingBlowAsset);
            ApplyHitMarkerLayer(
                _killingBlowHitMarkerImage,
                killingBlowAsset == null ? null : killingBlowAsset.Sprite,
                _killingBlowOverlaysEnabled != null
                    && _killingBlowOverlaysEnabled.Value
                    && _activeKillingBlowTier >= 1
                    && _activeKillingBlowTier <= 4);
            if (_killingBlowHitMarkerImage != null)
            {
                _killingBlowHitMarkerImage.transform.SetAsLastSibling();
            }
            ApplyReticleLayout(_currentContext);
            UpdateHitMarkerAnimation();
        }

        private void ApplyHitMarkerLayer(
            Image image,
            Sprite sprite,
            bool enabled)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = enabled && sprite != null;
        }

        private void UpdateHitMarkerAnimation()
        {
            if (!_hitMarkerActive || _reticleImage == null)
            {
                return;
            }

            float duration = Mathf.Max(
                0.05f,
                _activeHitMarkerEndsAt - _activeHitMarkerStartedAt);
            float progress = Mathf.Clamp01(
                (Time.unscaledTime - _activeHitMarkerStartedAt) / duration);
            if (progress >= 1f)
            {
                _hitMarkerActive = false;
                ApplyReticleState();
                return;
            }

            float fadeStart = 1f - HitMarkerFadeFraction;
            float alpha = progress <= fadeStart
                ? 1f
                : 1f - Mathf.InverseLerp(fadeStart, 1f, progress);
            Color color = _activeHitMarkerColor;
            color.a *= alpha;
            _reticleImage.color = color;
            if (_directHitMarkerImage != null)
            {
                _directHitMarkerImage.color = color;
            }
            if (_weakSpotHitMarkerImage != null)
            {
                _weakSpotHitMarkerImage.color = color;
            }
            if (_criticalHitMarkerImage != null)
            {
                _criticalHitMarkerImage.color = color;
            }
            if (_killingBlowHitMarkerImage != null)
            {
                _killingBlowHitMarkerImage.color = color;
            }

            float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.2f));
            float scale = Mathf.Lerp(HitMarkerInitialScale, 1f, settle);
            _reticleRect.localScale = new Vector3(scale, scale, 1f);
        }

        private void SetHitMarkerOverlaysEnabled(
            bool directHit,
            bool weakSpot,
            bool critical,
            bool killingBlow)
        {
            if (_directHitMarkerImage != null)
            {
                _directHitMarkerImage.enabled = directHit;
            }
            if (_weakSpotHitMarkerImage != null)
            {
                _weakSpotHitMarkerImage.enabled = weakSpot;
            }
            if (_criticalHitMarkerImage != null)
            {
                _criticalHitMarkerImage.enabled = critical;
            }
            if (_killingBlowHitMarkerImage != null)
            {
                _killingBlowHitMarkerImage.enabled = killingBlow;
            }
        }

        private void LogSteelAndBoneApiFailure(string message)
        {
            if (_steelAndBoneApiFailureLogged)
            {
                return;
            }

            _steelAndBoneApiFailureLogged = true;
            Logger.LogWarning(message);
        }

        private void UnsubscribeSteelAndBoneHitFeedback()
        {
            if (_steelAndBoneHitResolvedEvent == null
                && _steelAndBoneKillingBlowResolvedEvent == null)
            {
                return;
            }

            try
            {
                if (_steelAndBoneHitResolvedEvent != null
                    && _steelAndBoneHitResolvedHandler != null)
                {
                    _steelAndBoneHitResolvedEvent.RemoveEventHandler(
                        null,
                        _steelAndBoneHitResolvedHandler);
                }
                if (_steelAndBoneKillingBlowResolvedEvent != null
                    && _steelAndBoneKillingBlowResolvedHandler != null)
                {
                    _steelAndBoneKillingBlowResolvedEvent.RemoveEventHandler(
                        null,
                        _steelAndBoneKillingBlowResolvedHandler);
                }
            }
            catch (Exception exception)
            {
                LogSteelAndBoneApiFailure(
                    "Could not unsubscribe from Steel and Bone hit feedback: "
                    + exception.Message);
            }

            _steelAndBoneHitResolvedEvent = null;
            _steelAndBoneHitResolvedHandler = null;
            _steelAndBoneKillingBlowResolvedEvent = null;
            _steelAndBoneKillingBlowResolvedHandler = null;
        }

        private Color ParseColor(string colorText)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(colorText, out color))
            {
                return color;
            }

            string warningKey = colorText ?? "<null>";
            if (_invalidColorsLogged.Add(warningKey))
            {
                Logger.LogWarning(
                    "Invalid reticle color '" + warningKey + "'. Using white.");
            }

            return Color.white;
        }

        private TargetState ReadCurrentTargetState()
        {
            if (_heroCrosshair == null || _getCurrentLocationTypeMethod == null)
            {
                return TargetState.Default;
            }

            try
            {
                object targetType =
                    _getCurrentLocationTypeMethod.Invoke(_heroCrosshair, null);
                if (ReferenceEquals(targetType, _hostileTargetType))
                {
                    return TargetState.Hostile;
                }

                if (ReferenceEquals(targetType, _nonHostileTargetType))
                {
                    return TargetState.NonHostile;
                }
            }
            catch (Exception exception)
            {
                if (!_targetReadFailureLogged)
                {
                    _targetReadFailureLogged = true;
                    Logger.LogWarning(
                        "Could not read the current crosshair target type: "
                        + exception.Message);
                }
            }

            return TargetState.Default;
        }

        private bool ReadWeaponsVisible(object hero)
        {
            if (hero == null || _weaponsVisibleGetter == null)
            {
                return true;
            }

            try
            {
                return (bool)_weaponsVisibleGetter.Invoke(hero, null);
            }
            catch (Exception exception)
            {
                if (!_weaponsVisibilityReadFailureLogged)
                {
                    _weaponsVisibilityReadFailureLogged = true;
                    Logger.LogWarning(
                        "Could not determine whether the hero's weapons are visible: "
                        + exception.Message);
                }

                return true;
            }
        }

        private bool ReadHeroMounted()
        {
            if (_mountedOpacityMultiplier == null
                || _mountedOpacityMultiplier.Value >= 0.999f
                || _heroCrosshair == null
                || _getHeroMethod == null
                || _mountedGetter == null)
            {
                return false;
            }

            try
            {
                object hero = _getHeroMethod.Invoke(_heroCrosshair, null);
                return hero != null && (bool)_mountedGetter.Invoke(hero, null);
            }
            catch (Exception exception)
            {
                if (!_mountedReadFailureLogged)
                {
                    _mountedReadFailureLogged = true;
                    Logger.LogWarning(
                        "Could not determine whether the hero is mounted: "
                        + exception.Message);
                }

                return false;
            }
        }

        private void RememberHeroMounted(bool mounted)
        {
            _lastHeroMounted = mounted;
            _hasLastHeroMounted = true;
        }

        private ReticleContext ReadCurrentContext()
        {
            if (_heroCrosshair == null
                || _getHeroMethod == null
                || _allEquipmentSlotsField == null)
            {
                return ReticleContext.General;
            }

            try
            {
                object hero = _getHeroMethod.Invoke(_heroCrosshair, null);
                if (_useGeneralWhenHandsDown.Value
                    && !ReadWeaponsVisible(hero))
                {
                    return ReticleContext.General;
                }

                object inventory = hero == null
                    ? null
                    : _getHeroItemsMethod.Invoke(hero, null);
                IEnumerable slots =
                    _allEquipmentSlotsField.GetValue(null) as IEnumerable;
                if (inventory == null || slots == null)
                {
                    return ReticleContext.General;
                }

                bool hasMagic = false;
                foreach (object slot in slots)
                {
                    object item = _equippedItemMethod.Invoke(
                        null,
                        new[] { inventory, slot });
                    if (item == null)
                    {
                        continue;
                    }

                    if ((bool)_isRangedGetter.Invoke(item, null))
                    {
                        return ReticleContext.Bow;
                    }

                    bool itemIsMagic = _magicDetection.Value
                        == MagicDetectionMode.AnyMagic
                            ? (bool)_isMagicGetter.Invoke(item, null)
                            : (bool)_isCastMagicGetter.Invoke(item, null);
                    hasMagic |= itemIsMagic;
                }

                return hasMagic
                    ? ReticleContext.Magic
                    : ReticleContext.General;
            }
            catch (Exception exception)
            {
                if (!_equipmentReadFailureLogged)
                {
                    _equipmentReadFailureLogged = true;
                    Logger.LogWarning(
                        "Could not determine the equipped reticle context: "
                        + exception.Message);
                }

                return ReticleContext.General;
            }
        }

        private ContextSettings SettingsFor(ReticleContext context)
        {
            if (context == ReticleContext.Bow)
            {
                return _bow;
            }

            if (context == ReticleContext.Magic)
            {
                return _magic;
            }

            return context == ReticleContext.BloodMagic ? _bloodMagic : _general;
        }

        private void AttachCrouchIndicator(GameObject viewObject)
        {
            if (viewObject == null || _crouchViewObject == viewObject)
            {
                ApplyCrouchIndicatorState();
                return;
            }

            RestoreCrouchIndicator();
            _crouchViewObject = viewObject;
            _crouchRect = viewObject.transform as RectTransform;
            if (_crouchRect != null)
            {
                _originalCrouchAnchoredPosition = _crouchRect.anchoredPosition;
                _hasOriginalCrouchAnchoredPosition = true;
            }
            _crouchCanvasGroup = viewObject.GetComponent<CanvasGroup>();
            if (_crouchCanvasGroup == null)
            {
                _crouchCanvasGroup = viewObject.AddComponent<CanvasGroup>();
                _ownsCrouchCanvasGroup = true;
                _originalCrouchAlpha = 1f;
            }
            else
            {
                _ownsCrouchCanvasGroup = false;
                _originalCrouchAlpha = _crouchCanvasGroup.alpha;
            }

            ApplyCrouchIndicatorState();
        }

        private void ApplyCrouchIndicatorState()
        {
            ApplyCrouchIndicatorOpacity();
            ApplyCrouchIndicatorPosition();
        }

        private void ApplyCrouchIndicatorOpacity()
        {
            if (_crouchCanvasGroup == null)
            {
                return;
            }

            _crouchCanvasGroup.alpha = !_enabled.Value
                ? _originalCrouchAlpha
                : _originalCrouchAlpha
                    * Mathf.Clamp01(_crouchIndicatorOpacity.Value);
        }

        private void ApplyCrouchIndicatorPosition()
        {
            if (_crouchRect == null || !_hasOriginalCrouchAnchoredPosition)
            {
                return;
            }

            if (_enabled == null || !_enabled.Value)
            {
                _crouchRect.anchoredPosition = _originalCrouchAnchoredPosition;
                return;
            }

            float offset = _crouchIndicatorVerticalOffset == null
                ? 0f
                : _crouchIndicatorVerticalOffset.Value;
            if (float.IsNaN(offset) || float.IsInfinity(offset))
            {
                offset = 0f;
            }

            _crouchRect.anchoredPosition =
                _originalCrouchAnchoredPosition + new Vector2(0f, -offset);
        }

        private void RestoreCrouchIndicator()
        {
            if (_crouchRect != null && _hasOriginalCrouchAnchoredPosition)
            {
                _crouchRect.anchoredPosition =
                    _originalCrouchAnchoredPosition;
            }

            if (_crouchCanvasGroup != null)
            {
                _crouchCanvasGroup.alpha = _originalCrouchAlpha;
                if (_ownsCrouchCanvasGroup)
                {
                    UnityEngine.Object.Destroy(_crouchCanvasGroup);
                }
            }

            _crouchCanvasGroup = null;
            _crouchRect = null;
            _crouchViewObject = null;
            _ownsCrouchCanvasGroup = false;
            _originalCrouchAlpha = 1f;
            _originalCrouchAnchoredPosition = Vector2.zero;
            _hasOriginalCrouchAnchoredPosition = false;
        }

        private void LoadAllSprites()
        {
            LoadSprite(_general);
            LoadSprite(_bow);
            LoadSprite(_magic);
            LoadSprite(_bloodMagic);
            LoadAllBloodMagicQualitySprites();
            LoadAllHitMarkerSprites();
        }

        private void LoadAllBloodMagicQualitySprites()
        {
            for (int tier = 1; tier <= 4; tier++)
            {
                LoadBloodMagicQualitySprite(tier);
            }
        }

        private void LoadBloodMagicQualitySprite(int tier)
        {
            ReticleAsset asset = _bloodMagicQualityAssets[tier];
            LoadPngAsset(
                asset,
                ResolveHitMarkerPath(BloodMagicQualityReticleFileName(tier)),
                "Blood Magic quality reticle tier " + tier,
                "The standard Blood Magic reticle will be used as a fallback.",
                "DishonoredDynamicCrosshairBloodMagicQuality" + tier);
        }

        private void LoadSprite(ContextSettings settings)
        {
            string path = ResolveSpritePath(settings);
            string fallback = settings.Context == ReticleContext.General
                ? "The custom reticle will remain hidden."
                : "The general reticle will be used as a fallback.";
            LoadPngAsset(
                settings.Asset,
                path,
                settings.Context + " reticle",
                fallback,
                "DishonoredDynamicCrosshair" + settings.Context);
        }

        private void LoadAllHitMarkerSprites()
        {
            for (int frame = 0; frame <= 7; frame++)
            {
                LoadHitMarkerSprite((HitMarkerFrame)frame);
            }
            LoadPngAsset(
                _directHitMarkerOverlay,
                ResolveHitMarkerPath("hitmarker.png"),
                "direct-hit marker overlay",
                "Direct hits will use only the base result marker.",
                "DishonoredDynamicCrosshairDirectHitMarkerOverlay");
            LoadPngAsset(
                _weakSpotHitMarkerOverlay,
                ResolveHitMarkerPath("hitmarker_weakspot_overlay.png"),
                "weak-spot hit-marker overlay",
                "Weak-spot hits will use only the base result marker.",
                "DishonoredDynamicCrosshairWeakSpotHitMarkerOverlay");
            LoadPngAsset(
                _criticalHitMarkerOverlay,
                ResolveHitMarkerPath("hitmarker_critical_overlay.png"),
                "critical hit-marker overlay",
                "Critical hits will use only the base result marker.",
                "DishonoredDynamicCrosshairCriticalHitMarkerOverlay");
            for (int tier = 1; tier <= 4; tier++)
            {
                LoadKillingBlowOverlaySprite(tier);
            }
        }

        private void LoadKillingBlowOverlaySprite(int tier)
        {
            ReticleAsset asset = _killingBlowOverlayAssets[tier];
            LoadPngAsset(
                asset,
                ResolveHitMarkerPath(KillingBlowOverlayFileName(tier)),
                "killing-blow overlay tier " + tier,
                "Killing blows will use only the base result marker.",
                "DishonoredDynamicCrosshairKillingBlowOverlay" + tier);
        }

        private void LoadHitMarkerSprite(HitMarkerFrame frame)
        {
            ReticleAsset asset = _hitMarkerAssets[frame];
            string fileName = HitMarkerFileName(frame);
            LoadPngAsset(
                asset,
                ResolveHitMarkerPath(fileName),
                "effectiveness frame " + (int)frame,
                frame == HitMarkerFrame.Neutral
                    ? "Steel and Bone hit markers will remain hidden when no numbered marker is available."
                    : "The neutral effectiveness frame will be used as a fallback.",
                "DishonoredDynamicCrosshairHitMarkerFrame" + (int)frame);
        }

        private void LoadPngAsset(
            ReticleAsset asset,
            string path,
            string label,
            string missingDescription,
            string unityName)
        {
            DateTime writeTimeUtc = File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;
            long length = File.Exists(path) ? new FileInfo(path).Length : -1L;

            if (!File.Exists(path))
            {
                bool shouldLog = asset.ResolvedPath != path
                    || !asset.Missing;
                ClearAsset(asset);
                asset.ResolvedPath = path;
                asset.WriteTimeUtc = DateTime.MinValue;
                asset.Length = -1L;
                asset.Missing = true;
                ApplyReticleState();

                if (shouldLog)
                {
                    Logger.LogWarning(
                        label
                        + " PNG was not found: "
                        + path
                        + " "
                        + missingDescription);
                }

                return;
            }

            try
            {
                byte[] pngBytes = File.ReadAllBytes(path);
                bool useMipMaps = _textureFiltering.Value
                    == ReticleFilteringMode.MipmappedTrilinear;
                Texture2D texture =
                    new Texture2D(
                        2,
                        2,
                        TextureFormat.RGBA32,
                        useMipMaps);
                texture.name = unityName + "Texture";
                texture.hideFlags = HideFlags.DontSave;
                texture.wrapMode = TextureWrapMode.Clamp;

                if (!(bool)_loadImageMethod.Invoke(
                    null,
                    new object[] { texture, pngBytes, false }))
                {
                    UnityEngine.Object.Destroy(texture);
                    asset.ResolvedPath = path;
                    asset.WriteTimeUtc = writeTimeUtc;
                    asset.Length = length;
                    asset.Missing = false;
                    Logger.LogError(
                        "Unity could not decode the " + label + " PNG: "
                        + path);
                    return;
                }

                if (useMipMaps)
                {
                    texture.Apply(true, false);
                    texture.filterMode = FilterMode.Trilinear;
                }
                else
                {
                    texture.filterMode = FilterMode.Bilinear;
                }

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = unityName + "Sprite";
                sprite.hideFlags = HideFlags.DontSave;

                Sprite oldSprite = asset.Sprite;
                Texture2D oldTexture = asset.Texture;
                asset.Sprite = sprite;
                asset.Texture = texture;
                asset.ResolvedPath = path;
                asset.WriteTimeUtc = writeTimeUtc;
                asset.Length = length;
                asset.Missing = false;

                ApplyReticleState();
                if (oldSprite != null)
                {
                    UnityEngine.Object.Destroy(oldSprite);
                }

                if (oldTexture != null)
                {
                    UnityEngine.Object.Destroy(oldTexture);
                }

                Logger.LogInfo(
                    "Loaded "
                    + label
                    + " PNG: "
                    + path
                    + " ("
                    + texture.width
                    + "x"
                    + texture.height
                    + ", "
                    + texture.filterMode
                    + ", mipLevels="
                    + texture.mipmapCount
                    + ")");
            }
            catch (Exception exception)
            {
                asset.ResolvedPath = path;
                asset.WriteTimeUtc = writeTimeUtc;
                asset.Length = length;
                asset.Missing = false;
                Logger.LogError(
                    "Failed to load "
                    + label
                    + " PNG '"
                    + path
                    + "': "
                    + exception);
            }
        }

        private string ResolveHitMarkerPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(PluginDirectory, fileName));
        }

        private static string HitMarkerFileName(HitMarkerFrame frame)
        {
            return "custom_reticle_"
                + ((int)frame).ToString(CultureInfo.InvariantCulture)
                + ".png";
        }

        private static string BloodMagicQualityReticleFileName(int tier)
        {
            switch (tier)
            {
                case 1:
                    return "custom_reticle_bloodmagic_meager.png";
                case 2:
                    return "custom_reticle_bloodmagic_worthy.png";
                case 3:
                    return "custom_reticle_bloodmagic_potent.png";
                case 4:
                    return "custom_reticle_bloodmagic_prime.png";
                default:
                    throw new ArgumentOutOfRangeException("tier");
            }
        }

        private static string KillingBlowOverlayFileName(int tier)
        {
            switch (tier)
            {
                case 1:
                    return "hitmarker_killingblow_meager_overlay.png";
                case 2:
                    return "hitmarker_killingblow_worthy_overlay.png";
                case 3:
                    return "hitmarker_killingblow_potent_overlay.png";
                case 4:
                    return "hitmarker_killingblow_prime_overlay.png";
                default:
                    throw new ArgumentOutOfRangeException("tier");
            }
        }

        private string ResolveSpritePath(ContextSettings settings)
        {
            string configuredPath = settings.SpriteFile.Value;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return Path.GetFullPath(configuredPath);
            }

            return Path.GetFullPath(
                Path.Combine(PluginDirectory, configuredPath));
        }

        private void CheckSprite(ContextSettings settings)
        {
            string path = ResolveSpritePath(settings);
            if (AssetChanged(settings.Asset, path))
            {
                LoadSprite(settings);
            }
        }

        private void CheckAllHitMarkerSprites()
        {
            foreach (KeyValuePair<int, ReticleAsset> pair in
                _bloodMagicQualityAssets)
            {
                string path = ResolveHitMarkerPath(
                    BloodMagicQualityReticleFileName(pair.Key));
                if (AssetChanged(pair.Value, path))
                {
                    LoadBloodMagicQualitySprite(pair.Key);
                }
            }

            foreach (KeyValuePair<HitMarkerFrame, ReticleAsset> pair in
                _hitMarkerAssets)
            {
                string path = ResolveHitMarkerPath(
                    HitMarkerFileName(pair.Key));
                if (AssetChanged(pair.Value, path))
                {
                    LoadHitMarkerSprite(pair.Key);
                }
            }

            string directHitPath = ResolveHitMarkerPath("hitmarker.png");
            if (AssetChanged(_directHitMarkerOverlay, directHitPath))
            {
                LoadPngAsset(
                    _directHitMarkerOverlay,
                    directHitPath,
                    "direct-hit marker overlay",
                    "Direct hits will use only the base result marker.",
                    "DishonoredDynamicCrosshairDirectHitMarkerOverlay");
            }

            string weakSpotPath = ResolveHitMarkerPath(
                "hitmarker_weakspot_overlay.png");
            if (AssetChanged(_weakSpotHitMarkerOverlay, weakSpotPath))
            {
                LoadPngAsset(
                    _weakSpotHitMarkerOverlay,
                    weakSpotPath,
                    "weak-spot hit-marker overlay",
                    "Weak-spot hits will use only the base result marker.",
                    "DishonoredDynamicCrosshairWeakSpotHitMarkerOverlay");
            }

            string criticalPath = ResolveHitMarkerPath(
                "hitmarker_critical_overlay.png");
            if (AssetChanged(_criticalHitMarkerOverlay, criticalPath))
            {
                LoadPngAsset(
                    _criticalHitMarkerOverlay,
                    criticalPath,
                    "critical hit-marker overlay",
                    "Critical hits will use only the base result marker.",
                    "DishonoredDynamicCrosshairCriticalHitMarkerOverlay");
            }

            foreach (KeyValuePair<int, ReticleAsset> pair in
                _killingBlowOverlayAssets)
            {
                string path = ResolveHitMarkerPath(
                    KillingBlowOverlayFileName(pair.Key));
                if (AssetChanged(pair.Value, path))
                {
                    LoadKillingBlowOverlaySprite(pair.Key);
                }
            }
        }

        private static bool AssetChanged(ReticleAsset asset, string path)
        {
            bool exists = !string.IsNullOrEmpty(path) && File.Exists(path);
            DateTime writeTimeUtc = exists
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;
            long length = exists ? new FileInfo(path).Length : -1L;

            return asset.ResolvedPath != path
                || asset.Missing == exists
                || asset.WriteTimeUtc != writeTimeUtc
                || asset.Length != length;
        }

        private static void ClearAsset(ReticleAsset asset)
        {
            if (asset.Sprite != null)
            {
                UnityEngine.Object.Destroy(asset.Sprite);
            }

            if (asset.Texture != null)
            {
                UnityEngine.Object.Destroy(asset.Texture);
            }

            asset.Sprite = null;
            asset.Texture = null;
        }

        private void Update()
        {
            if (_enabled == null || !_enabled.Value)
            {
                return;
            }

            ResolveSteelAndBoneHitFeedbackApi();
            if (_hitMarkerActive)
            {
                UpdateHitMarkerAnimation();
            }

            if (Time.unscaledTime >= _nextSpriteCheckTime)
            {
                _nextSpriteCheckTime = Time.unscaledTime + 1f;
                CheckSprite(_general);
                CheckSprite(_bow);
                CheckSprite(_magic);
                CheckSprite(_bloodMagic);
                CheckAllHitMarkerSprites();
            }

            if (Time.unscaledTime >= _nextTargetRefreshTime)
            {
                _nextTargetRefreshTime = Time.unscaledTime
                    + Mathf.Clamp(
                        _hostilityRefreshIntervalSeconds.Value,
                        0.02f,
                        1f);
                RefreshHoveredTargetState();
            }

            if (Time.unscaledTime >= _nextContextCheckTime)
            {
                _nextContextCheckTime = Time.unscaledTime + 0.25f;
                ReticleContext context = ReadCurrentContext();
                TargetState targetState = ReadCurrentTargetState();
                bool bloodMagicActive = ReadBloodMagicCorpseActive();
                ReticleContext displayContext = bloodMagicActive
                    ? ReticleContext.BloodMagic
                    : context;
                TargetState displayTargetState = bloodMagicActive
                    ? BloodMagicCorpseUsesUsableVisuals(_lastBloodMagicCorpseState)
                        ? TargetState.Hostile
                        : TargetState.Default
                    : targetState;
                float bloodMagicQualityScale = bloodMagicActive
                    ? GetBloodMagicQualityScale()
                    : 1f;
                int bloodMagicState = bloodMagicActive
                    ? _lastBloodMagicCorpseState
                    : 0;
                int bloodMagicQualityTier = bloodMagicActive
                    ? _lastBloodMagicCorpseQualityTier
                    : 0;
                bool heroMounted = ReadHeroMounted();
                if (displayContext != _currentContext
                    || displayTargetState != _currentTargetState
                    || bloodMagicState != _currentBloodMagicCorpseState
                    || bloodMagicQualityTier != _currentBloodMagicCorpseQualityTier
                    || !Mathf.Approximately(
                        bloodMagicQualityScale,
                        _currentBloodMagicQualityScale)
                    || !_hasLastHeroMounted
                    || heroMounted != _lastHeroMounted)
                {
                    ApplyReticleState(context, targetState);
                }

                if (_sizeMode.Value == ReticleSizeMode.ScreenPixels)
                {
                    float canvasScaleFactor = GetCanvasScaleFactor();
                    if (Screen.width != _lastScreenWidth
                        || Screen.height != _lastScreenHeight
                        || !Mathf.Approximately(
                            canvasScaleFactor,
                            _lastCanvasScaleFactor))
                    {
                        ApplyReticleLayout(_currentContext);
                    }
                }
            }
        }

        private void RefreshHoveredTargetState()
        {
            if (_heroCrosshair == null
                || _currentTargetLocation == null
                || _targetChangedMethod == null)
            {
                return;
            }

            try
            {
                _targetChangedMethod.Invoke(
                    _heroCrosshair,
                    new[] { _currentTargetLocation });
            }
            catch (Exception exception)
            {
                _currentTargetLocation = null;
                ApplyReticleState(
                    ReadCurrentContext(),
                    TargetState.Default);

                if (!_targetRefreshFailureLogged)
                {
                    _targetRefreshFailureLogged = true;
                    Exception cause = exception is TargetInvocationException
                        && exception.InnerException != null
                            ? exception.InnerException
                            : exception;
                    Logger.LogWarning(
                        "Could not refresh the hovered NPC's hostility: "
                        + cause.Message);
                }
            }
        }

        private void OnPresetSettingChanged(object sender, EventArgs eventArgs)
        {
            ApplyPreset(_preset.Value);
        }

        private void OnBehaviorSettingChanged(object sender, EventArgs eventArgs)
        {
            ApplyReticleState();
            ApplyTargetDetectionRange();
            RefreshVanillaCrosshair();
        }

        private void OnHitMarkerSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            if (_steelAndBoneHitMarkersEnabled == null
                || !_steelAndBoneHitMarkersEnabled.Value)
            {
                _hitMarkerActive = false;
                UnsubscribeSteelAndBoneHitFeedback();
            }
            else
            {
                _steelAndBoneApiUnavailableForSession = false;
                _nextSteelAndBoneApiResolveTime = 0f;
                ResolveSteelAndBoneHitFeedbackApi();
            }

            ApplyReticleState();
        }

        private void OnTargetDetectionRangeSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            ApplyTargetDetectionRange();
        }

        private void ApplyTargetDetectionRange()
        {
            if (_heroRaycaster == null
                || !_hasOriginalNpcDetectionMaxDistance
                || _npcDetectionMaxDistanceField == null)
            {
                return;
            }

            try
            {
                float multiplier = _enabled != null && _enabled.Value
                    ? Mathf.Clamp(_targetDetectionRangeMultiplier.Value, 0.1f, 5f)
                    : 1f;
                _npcDetectionMaxDistanceField.SetValue(
                    _heroRaycaster,
                    _originalNpcDetectionMaxDistance * multiplier);
            }
            catch (Exception exception)
            {
                LogRaycasterRangeFailure(
                    "Could not update the NPC target-detection range: ",
                    exception);
            }
        }

        private void RestoreTargetDetectionRange()
        {
            if (_heroRaycaster != null
                && _hasOriginalNpcDetectionMaxDistance
                && _npcDetectionMaxDistanceField != null)
            {
                try
                {
                    _npcDetectionMaxDistanceField.SetValue(
                        _heroRaycaster,
                        _originalNpcDetectionMaxDistance);
                }
                catch (Exception exception)
                {
                    LogRaycasterRangeFailure(
                        "Could not restore the NPC target-detection range: ",
                        exception);
                }
            }

            _heroRaycaster = null;
            _hasOriginalNpcDetectionMaxDistance = false;
        }

        private void LogRaycasterRangeFailure(
            string message,
            Exception exception)
        {
            if (_raycasterRangeFailureLogged)
            {
                return;
            }

            _raycasterRangeFailureLogged = true;
            Logger.LogWarning(message + exception.Message);
        }

        private void OnTextureFilteringSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            if (_loadImageMethod != null)
            {
                LoadAllSprites();
            }
        }

        private void OnSpriteSettingChanged(object sender, EventArgs eventArgs)
        {
            if (_loadImageMethod == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _bow.SpriteFile))
            {
                LoadSprite(_bow);
            }
            else if (ReferenceEquals(sender, _magic.SpriteFile))
            {
                LoadSprite(_magic);
            }
            else if (ReferenceEquals(sender, _bloodMagic.SpriteFile))
            {
                LoadSprite(_bloodMagic);
            }
            else
            {
                LoadSprite(_general);
            }
        }

        private void RefreshVanillaCrosshair()
        {
            if (_heroCrosshair == null || _refreshCrosshairMethod == null)
            {
                return;
            }

            try
            {
                _refreshCrosshairMethod.Invoke(_heroCrosshair, null);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not refresh the vanilla crosshair: "
                    + exception.Message);
            }
        }

        private void DestroyReticleObject()
        {
            if (_reticleObject != null)
            {
                UnityEngine.Object.Destroy(_reticleObject);
            }

            _reticleObject = null;
            _reticleRect = null;
            _reticleImage = null;
            _directHitMarkerImage = null;
            _weakSpotHitMarkerImage = null;
            _criticalHitMarkerImage = null;
            _killingBlowHitMarkerImage = null;
            _crosshairParent = null;
        }

        private void OnDestroy()
        {
            UnregisterSettingHandlers();
            UnsubscribeSteelAndBoneHitFeedback();
            RestoreTargetDetectionRange();

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            RestoreCrouchIndicator();
            RefreshVanillaCrosshair();
            DestroyReticleObject();
            ClearAsset(_general.Asset);
            ClearAsset(_bow.Asset);
            ClearAsset(_magic.Asset);
            ClearAsset(_bloodMagic.Asset);
            foreach (ReticleAsset asset in _bloodMagicQualityAssets.Values)
            {
                ClearAsset(asset);
            }
            foreach (ReticleAsset asset in _hitMarkerAssets.Values)
            {
                ClearAsset(asset);
            }
            ClearAsset(_directHitMarkerOverlay);
            ClearAsset(_weakSpotHitMarkerOverlay);
            ClearAsset(_criticalHitMarkerOverlay);
            foreach (ReticleAsset asset in _killingBlowOverlayAssets.Values)
            {
                ClearAsset(asset);
            }
            Instance = null;
        }

        private static Type RequireType(string fullName)
        {
            Type type = AccessTools.TypeByName(fullName);
            if (type == null)
            {
                throw new TypeLoadException(
                    "Could not find game type " + fullName + ".");
            }

            return type;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            Type[] argumentTypes)
        {
            MethodInfo method = argumentTypes == null
                ? AccessTools.Method(type, name)
                : AccessTools.Method(type, name, argumentTypes);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }

            return method;
        }

        private static MethodInfo RequireStaticMethod(
            Type type,
            string name,
            int parameterCount)
        {
            const BindingFlags flags = BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (method.Name == name
                    && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            throw new MissingMethodException(type.FullName, name);
        }

        private static MethodInfo RequirePropertyGetter(Type type, string name)
        {
            MethodInfo getter = OptionalPropertyGetter(type, name);
            if (getter != null)
            {
                return getter;
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private static MethodInfo OptionalPropertyGetter(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;

            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, flags);
                if (property == null)
                {
                    continue;
                }

                MethodInfo getter = property.GetGetMethod(true);
                if (getter != null)
                {
                    return getter;
                }
            }

            return null;
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field == null)
            {
                throw new MissingFieldException(type.FullName, name);
            }

            return field;
        }

        private static object RequireStaticField(Type type, string name)
        {
            FieldInfo field = RequireField(type, name);
            object value = field.GetValue(null);
            if (value == null)
            {
                throw new InvalidOperationException(
                    "Game field " + type.FullName + "." + name + " was null.");
            }

            return value;
        }

        private static MethodInfo ResolveLoadImageMethod()
        {
            Type imageConversionType = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (imageConversionType == null)
            {
                throw new TypeLoadException(
                    "Could not find UnityEngine.ImageConversion.");
            }

            MethodInfo method = imageConversionType.GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);
            if (method == null)
            {
                throw new MissingMethodException(
                    imageConversionType.FullName,
                    "LoadImage(Texture2D, byte[], bool)");
            }

            return method;
        }

        private sealed class ContextSettings
        {
            internal ContextSettings(ReticleContext context)
            {
                Context = context;
                Asset = new ReticleAsset();
            }

            internal ReticleContext Context { get; private set; }
            internal ReticleAsset Asset { get; private set; }
            internal ConfigEntry<string> SpriteFile { get; set; }
            internal ConfigEntry<float> ScaleMultiplier { get; set; }
            internal ConfigEntry<string> DefaultColor { get; set; }
            internal ConfigEntry<string> HostileColor { get; set; }
            internal ConfigEntry<string> NonHostileColor { get; set; }
        }

        private sealed class ReticleAsset
        {
            internal Texture2D Texture { get; set; }
            internal Sprite Sprite { get; set; }
            internal string ResolvedPath { get; set; }
            internal DateTime WriteTimeUtc { get; set; }
            internal long Length { get; set; }
            internal bool Missing { get; set; }
        }
    }

    internal static class DishonoredDynamicCrosshairPatches
    {
        internal static void CrosshairPartSetActivePrefix(
            object __instance,
            ref bool __0)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.FilterVanillaPartActivation(__instance, ref __0);
            }
        }

        internal static void CrosshairPartSetActivePostfix(object __instance)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.ObserveCrosshairPart(__instance);
            }
        }

        internal static void HeroCrosshairInitializedPostfix(object __instance)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.OnHeroCrosshairInitialized(__instance);
            }
        }

        internal static void TargetChangedPostfix(
            object __instance,
            object __0)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.OnTargetChanged(__instance, __0);
            }
        }

        internal static void PerspectiveChangedPostfix(object __instance)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.OnPerspectiveChanged(__instance);
            }
        }

        internal static void EquipmentChangedPostfix(object __instance)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.OnCrosshairChanged(__instance);
            }
        }

        internal static void CrosshairRefreshedPostfix(object __instance)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.OnCrosshairChanged(__instance);
            }
        }

        internal static void HeroRaycasterAttachedPostfix(object __instance)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.OnHeroRaycasterAttached(__instance);
            }
        }

        internal static void HeroRaycasterDiscardingPrefix(object __instance)
        {
            DishonoredDynamicCrosshairPlugin plugin =
                DishonoredDynamicCrosshairPlugin.Instance;
            if (plugin != null)
            {
                plugin.OnHeroRaycasterDiscarding(__instance);
            }
        }
    }
}
