using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Awaken.TG.Graphics.Scene;
using Awaken.TG.Main.AI.Movement.Controllers;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Attachments;
using Awaken.TG.Main.Locations.Actions.Lockpicking;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Locations.Views;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.Scenes.SceneConstructors;
using Awaken.TG.Main.UI.ObjectCloseup;
using Awaken.TG.Main.UI.RawImageRendering;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TGAllLightsCastShadowsAddon
{
    // Safe selector architecture adapted from TGAllLightsCastShadowsSafe by
    // Nexus Mods user pupkidze007: https://forums.nexusmods.com/profile/194045963-pupkidze007/
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _safeSelectionController;
        private ConfigEntry<int> _maximumUpgradedLights;
        private ConfigEntry<float> _maximumDistanceMeters;
        private ConfigEntry<int> _maximumShadowMapFaces;
        private ConfigEntry<float> _selectionHysteresisMeters;
        private ConfigEntry<bool> _preferViewRelevantLights;
        private ConfigEntry<float> _selectionRefreshSeconds;
        private ConfigEntry<float> _viewExitDelaySeconds;
        private ConfigEntry<int> _offscreenReserveLights;
        private ConfigEntry<int> _maximumSelectionSwapsPerRefresh;
        private ConfigEntry<float> _selectionRetentionMeters;
        private ConfigEntry<float> _screenCenterPriorityMeters;
        private ConfigEntry<float> _shadowHandoffSeconds;
        private ConfigEntry<int> _initialFillBatchSize;
        private ConfigEntry<bool> _suppressAddedVolumetricShadows;
        private ConfigEntry<bool> _excludeHeroLight;
        private ConfigEntry<bool> _excludeWyrdSightLights;
        private ConfigEntry<bool> _excludeSummonLights;
        private ConfigEntry<bool> _excludeInterfacePreviewLights;
        private ConfigEntry<bool> _excludeLockpickingLights;
        private ConfigEntry<bool> _excludePlacedBonfireLights;
        private ConfigEntry<bool> _respectExternalPlayerLightOwnership;
        private ConfigEntry<bool> _interiorPerformanceEnabled;
        private ConfigEntry<int> _interiorMaximumUpgradedLights;
        private ConfigEntry<float> _interiorMaximumDistanceMeters;
        private ConfigEntry<int> _interiorMaximumShadowMapFaces;
        private ConfigEntry<int> _interiorPromotedShadowResolution;
        private bool _managedControllerEnabledForSession;
        private bool _mageLightInstalled;
        private bool _noPlayerLightInstalled;

        private readonly Dictionary<int, ManagedLightState> _managedLightStates =
            new Dictionary<int, ManagedLightState>();
        private readonly Dictionary<int, CachedManagedLight> _managedLightCache =
            new Dictionary<int, CachedManagedLight>();
        private readonly HashSet<int> _managedActiveLights = new HashSet<int>();
        private readonly HashSet<int> _managedDiscoveryIds = new HashSet<int>();
        private readonly HashSet<int> _managedDesiredIds = new HashSet<int>();
        private readonly Dictionary<int, ManagedCandidate> _managedCandidateLookup =
            new Dictionary<int, ManagedCandidate>();
        private readonly List<ManagedCandidate> _managedViewCandidates =
            new List<ManagedCandidate>();
        private readonly List<ManagedCandidate> _managedOffscreenCandidates =
            new List<ManagedCandidate>();
        private readonly List<ManagedCandidate> _managedDesiredCandidates =
            new List<ManagedCandidate>();
        private readonly List<ManagedCandidate> _managedCandidateScratch =
            new List<ManagedCandidate>();
        private readonly List<int> _managedIdScratch = new List<int>();
        private readonly List<Light> _managedExternalPlayerLights =
            new List<Light>();
        private readonly HashSet<int> _managedExternalPlayerLightIds =
            new HashSet<int>();
        private readonly Plane[] _managedFrustumPlanes = new Plane[6];

        private Camera _managedGameCamera;
        private int _managedSceneHandle = -1;
        private float _nextManagedSelectionRefresh;
        private bool _managedSettingsDirty;
        private bool _managedInitialFillPending = true;
        private bool _managedInteriorActive;
        private ManagedShadowHandoff _managedShadowHandoff;
        private Type _wyrdSightGlowEffectType;
        private bool _wyrdSightTypeResolved;
        private MethodInfo _hdShadowsEnabledMethod;
        private MethodInfo _hdRequestShadowMapRenderingMethod;
        private PropertyInfo _parentDesiredShadowModeProperty;
        private FieldInfo _parentShadowStrengthField;
        private FieldInfo _parentIncludeInactiveLightsField;
        private FieldInfo _parentOnlyPointAndSpotField;
        private FieldInfo _parentOnlyEnabledLightsField;
        private bool _managedHdrpResolved;

        private int _managedScanCandidateCount;
        private int _managedScanViewRelevantCount;
        private int _managedScanPointLights;
        private int _managedScanSpotLights;
        private int _managedScanShadowMapFaces;
        private int _managedScanActivatedLights;
        private int _managedScanRestoredLights;
        private int _managedScanSwaps;
        private int _managedExcludedWyrdSightLights;
        private int _managedExcludedSummonLights;
        private int _managedExcludedInterfaceLights;
        private int _managedExcludedLockpickingLights;
        private int _managedExcludedPlacedBonfireLights;
        private int _managedExcludedConfiguredLights;
        private int _managedExcludedExternalPlayerLights;
        private int _managedExcludedHeroLights;
        private int _managedExternalPlayerShadowMapFaces;

        private void BindManagedShadowConfig()
        {
            _safeSelectionController = Config.Bind(
                "Performance",
                "UseSafeSelectionController",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Lets the addon own light discovery and selection while preserving the original mod's toggle and settings. This avoids the original all-light state capture and bounds shadow work before a light is changed. Restart the game after changing this setting.",
                    "Performance", "Use Safe Selection Controller", 5, 0));
            _maximumUpgradedLights = Config.Bind(
                "Performance",
                "MaximumUpgradedLights",
                16,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Permanent maximum selected lights. A lower limit in the original mod or the active combat settings still wins.",
                    "Performance", "Maximum Upgraded Lights", 5, 10,
                    new AcceptableValueRange<int>(0, 256)));
            _maximumDistanceMeters = Config.Bind(
                "Performance",
                "MaximumDistanceMeters",
                25f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Permanent acquisition distance. A lower distance in the original mod or the active combat settings still wins.",
                    "Performance", "Maximum Distance", 5, 20,
                    new AcceptableValueRange<float>(1f, 200f)));
            _maximumShadowMapFaces = Config.Bind(
                "Performance",
                "MaximumShadowMapFaces",
                48,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum estimated shadow-map faces across selected lights. Point lights cost six faces and spot lights cost one, so this controls atlas and draw pressure more directly than a light count alone.",
                    "Performance", "Maximum Shadow Map Faces", 5, 30,
                    new AcceptableValueRange<int>(0, 1536)));
            _suppressAddedVolumetricShadows = Config.Bind(
                "Performance",
                "SuppressAddedVolumetricShadows",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Keeps parent-promoted lights from adding shadow maps to volumetric fog while retaining ordinary surface shadows. The exact authored fog value is restored when a light is released.",
                    "Performance", "Suppress Added Volumetric Shadows", 5, 40));
            _selectionHysteresisMeters = Config.Bind(
                "View Priority",
                "HysteresisMeters",
                8f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Extra retention distance for an already selected light, reducing rapid changes while moving.",
                    "View Priority", "Hysteresis", 6, 0,
                    new AcceptableValueRange<float>(0f, 100f)));
            _selectionRetentionMeters = Config.Bind(
                "View Priority",
                "SelectionRetentionMeters",
                2f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Makes an already selected light rank as if it were this many metres closer. This stabilizes nearby choices without extending the acquisition distance or view-exit grace.",
                    "View Priority", "Selection Retention", 6, 10,
                    new AcceptableValueRange<float>(0f, 10f)));
            _preferViewRelevantLights = Config.Bind(
                "View Priority",
                "PreferViewRelevantLights",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Prioritizes lights whose illumination volume intersects the camera view while retaining a small offscreen reserve.",
                    "View Priority", "Prefer View Relevant Lights", 6, 20));
            _screenCenterPriorityMeters = Config.Bind(
                "View Priority",
                "ScreenCenterPriorityMeters",
                4f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Gives a visible light near the centre of the screen a moderate ranking advantage. Set zero to keep view relevance without centre preference.",
                    "View Priority", "Screen Centre Priority", 6, 30,
                    new AcceptableValueRange<float>(0f, 15f)));
            _selectionRefreshSeconds = Config.Bind(
                "View Priority",
                "SelectionRefreshSeconds",
                0.2f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Interval for lightweight selection refreshes from the checked nearby cache. This does not run another global light search.",
                    "View Priority", "Selection Refresh", 6, 40,
                    new AcceptableValueRange<float>(0.05f, 2f)));
            _viewExitDelaySeconds = Config.Bind(
                "View Priority",
                "ViewExitDelaySeconds",
                0.75f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How long a selected light remains view-relevant after its influence leaves the camera view.",
                    "View Priority", "View Exit Delay", 6, 50,
                    new AcceptableValueRange<float>(0f, 5f)));
            _offscreenReserveLights = Config.Bind(
                "View Priority",
                "OffscreenReserveLights",
                2,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum nearby offscreen lights reserved within the selected budget so camera turns do not begin with an empty view.",
                    "View Priority", "Offscreen Reserve", 6, 60,
                    new AcceptableValueRange<int>(0, 256)));
            _maximumSelectionSwapsPerRefresh = Config.Bind(
                "View Priority",
                "MaximumSelectionSwapsPerRefresh",
                2,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum selected-light replacements during one lightweight refresh. Lower values reduce simultaneous atlas churn and visible popping.",
                    "View Priority", "Maximum Selection Swaps", 6, 70,
                    new AcceptableValueRange<int>(1, 64)));
            _shadowHandoffSeconds = Config.Bind(
                "View Priority",
                "ShadowHandoffSeconds",
                0.6f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Fades one outgoing shadow to zero before transferring its budget slot and fading the replacement in. Set zero for immediate swaps.",
                    "View Priority", "Shadow Handoff", 6, 80,
                    new AcceptableValueRange<float>(0f, 2f)));
            _initialFillBatchSize = Config.Bind(
                "View Priority",
                "InitialFillBatchSize",
                4,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum new shadows enabled per selection refresh after loading or re-enabling. Set zero to fill every free slot immediately.",
                    "View Priority", "Initial Fill Batch Size", 6, 90,
                    new AcceptableValueRange<int>(0, 64)));

            _respectExternalPlayerLightOwnership = Config.Bind(
                "Excluded Lights",
                "RespectExternalPlayerLightOwnership",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Leaves the HeroLight hierarchy entirely under MageLight or No Player Light when either plugin is installed. Active MageLight point-light shadows also reserve six faces from the permanent face budget.",
                    "Excluded Lights", "Respect External Player Light Ownership", 0, 20));
            _excludeHeroLight = Config.Bind(
                "Excluded Lights",
                "ExcludeHeroLight",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Prevents the vanilla indoor and outdoor HeroLight hierarchy from receiving added shadows, even when no separate player-light mod is installed.",
                    "Excluded Lights", "Exclude Hero Light", 0, 25));
            _excludeWyrdSightLights = Config.Bind(
                "Excluded Lights",
                "ExcludeWyrdSightLights",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Prevents Wyrd Sight's generated WyrdLight highlights from being promoted into world shadow casters.",
                    "Excluded Lights", "Exclude Wyrd Sight Lights", 0, 30));
            _excludeSummonLights = Config.Bind(
                "Excluded Lights",
                "ExcludeSummonLights",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Prevents the addon from adding shadows to lights attached to hero or NPC summons while preserving their original glow.",
                    "Excluded Lights", "Exclude Summon Lights", 0, 40));
            _excludeInterfacePreviewLights = Config.Bind(
                "Excluded Lights",
                "ExcludeInterfacePreviewLights",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Leaves character, item, and object preview lights under their authored interface lighting.",
                    "Excluded Lights", "Exclude Interface Preview Lights", 0, 50));
            _excludeLockpickingLights = Config.Bind(
                "Excluded Lights",
                "ExcludeLockpickingLights",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Leaves the close-up lockpicking light under its authored lighting.",
                    "Excluded Lights", "Exclude Lockpicking Lights", 0, 60));
            _excludePlacedBonfireLights = Config.Bind(
                "Excluded Lights",
                "ExcludePlacedBonfireLights",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Prevents the exact portable Bonfire placed by the player from shadowing itself. Stationary world fires are handled separately by ProtectBonfireLights.",
                    "Excluded Lights", "Exclude Placed Bonfire Lights", 0, 70));

            _interiorPerformanceEnabled = Config.Bind(
                "Interior Performance",
                "InteriorPerformanceEnabled",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Uses the tightening-only interior limits below. Defaults match the permanent limits, so enabling this profile does not change existing behavior until its values are lowered.",
                    "Interior Performance", "Enabled", 8, 0));
            _interiorMaximumUpgradedLights = Config.Bind(
                "Interior Performance",
                "InteriorMaximumUpgradedLights",
                16,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Interior-only light-count ceiling. It can tighten but never raise the permanent, parent, or combat limit.",
                    "Interior Performance", "Maximum Upgraded Lights", 8, 10,
                    new AcceptableValueRange<int>(0, 256)));
            _interiorMaximumDistanceMeters = Config.Bind(
                "Interior Performance",
                "InteriorMaximumDistanceMeters",
                25f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Interior-only acquisition distance. It can tighten but never raise the permanent, parent, or combat distance.",
                    "Interior Performance", "Maximum Distance", 8, 20,
                    new AcceptableValueRange<float>(1f, 200f)));
            _interiorMaximumShadowMapFaces = Config.Bind(
                "Interior Performance",
                "InteriorMaximumShadowMapFaces",
                48,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Interior-only hard shadow-map face ceiling before external player-light reservation. Point lights cost six faces and spot lights one.",
                    "Interior Performance", "Maximum Shadow Map Faces", 8, 30,
                    new AcceptableValueRange<int>(0, 1536)));
            _interiorPromotedShadowResolution = Config.Bind(
                "Interior Performance",
                "InteriorPromotedShadowResolution",
                256,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Interior-only per-face resolution ceiling. It can lower but never raise the normal or combat cap.",
                    "Interior Performance", "Promoted Shadow Resolution", 8, 40,
                    new AcceptableValueList<int>(128, 256, 512, 1024)));

            _managedControllerEnabledForSession = _safeSelectionController.Value;
        }

        private void SubscribeManagedShadowConfigEvents()
        {
            Config.SettingChanged += OnManagedShadowSettingChanged;
        }

        private void UnsubscribeManagedShadowConfigEvents()
        {
            if (Config != null)
            {
                Config.SettingChanged -= OnManagedShadowSettingChanged;
            }
        }

        private void OnManagedShadowSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (args != null && args.ChangedSetting == _safeSelectionController)
            {
                Logger.LogInfo(
                    "UseSafeSelectionController will take effect after the game restarts.");
                return;
            }
            _managedSettingsDirty = true;
            _nextManagedSelectionRefresh = 0f;
            NudgeParentScan();
        }

        private bool UseManagedShadowController()
        {
            return _managedControllerEnabledForSession;
        }

        private void InitializeManagedParentReflection(Type shadowManagerType)
        {
            Type localConfigDataType = AccessTools.TypeByName(
                "TGAllLightsCastShadows.LocalConfigData");
            if (localConfigDataType != null)
            {
                _parentDesiredShadowModeProperty = AccessTools.Property(
                    localConfigDataType,
                    "DesiredShadowMode");
                _parentShadowStrengthField = AccessTools.Field(
                    localConfigDataType,
                    "ShadowStrength");
                _parentIncludeInactiveLightsField = AccessTools.Field(
                    localConfigDataType,
                    "IncludeInactiveLights");
                _parentOnlyPointAndSpotField = AccessTools.Field(
                    localConfigDataType,
                    "OnlyPointAndSpot");
                _parentOnlyEnabledLightsField = AccessTools.Field(
                    localConfigDataType,
                    "OnlyEnabledLights");
            }

            _managedSceneHandle = SceneManager.GetActiveScene().handle;
            DetectExternalPlayerLightOwners();
        }

        private void DetectExternalPlayerLightOwners()
        {
            _mageLightInstalled = Chainloader.PluginInfos.ContainsKey(
                MageLightPluginGuid);
            _noPlayerLightInstalled = Chainloader.PluginInfos.ContainsKey(
                NoPlayerLightPluginGuid);

            if (_mageLightInstalled || _noPlayerLightInstalled)
            {
                Logger.LogInfo(
                    "External HeroLight ownership detected: MageLight="
                    + _mageLightInstalled
                    + ", No Player Light="
                    + _noPlayerLightInstalled
                    + ".");
            }
            if (_mageLightInstalled && _noPlayerLightInstalled)
            {
                const string warning =
                    "MageLight and No Player Light are both installed. MageLight can reactivate HeroLight when toggled on; disable or uninstall MageLight if No Player Light should remain authoritative.";
                Logger.LogWarning(warning);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowCompatibilityWarning(
                        PluginGuid,
                        "mage-light-no-player-light-conflict",
                        warning);
            }
        }

        internal bool BeforeManagedParentApply(string reason)
        {
            if (!UseManagedShadowController())
            {
                return true;
            }

            try
            {
                ApplyManagedShadowSelection(reason ?? string.Empty);
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Safe shadow selection failed; restored addon-owned lights and skipped the unsafe parent scan: "
                    + exception);
                RestoreAllManagedLights("safe selection failure");
            }
            return false;
        }

        internal void BeforeParentSceneCooldown(string reason)
        {
            BeforeDawnDuskSceneTransition();
            if (UseManagedShadowController())
            {
                RestoreAllManagedLights(
                    string.IsNullOrEmpty(reason)
                        ? "parent scene transition"
                        : reason);
            }
        }

        internal bool BeforeParentRestore(string reason)
        {
            if (!UseManagedShadowController())
            {
                return true;
            }
            RestoreAllManagedLights(
                string.IsNullOrEmpty(reason) ? "parent restore" : reason);
            return false;
        }

        private void UpdateManagedShadowController()
        {
            if (!UseManagedShadowController())
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.handle != _managedSceneHandle)
            {
                RestoreAllManagedLights("active scene changed");
                _managedSceneHandle = scene.handle;
                return;
            }

            if (_parentRuntimeEnabledKnown && !_parentRuntimeEnabled)
            {
                RestoreAllManagedLights("parent disabled");
                return;
            }

            AdvanceManagedShadowHandoff();

            if (Time.unscaledTime < _nextManagedSelectionRefresh)
            {
                return;
            }

            _nextManagedSelectionRefresh =
                Time.unscaledTime + _selectionRefreshSeconds.Value;
            bool interiorChanged = RefreshManagedInteriorState();
            if (_managedLightCache.Count == 0
                && _managedExternalPlayerLights.Count == 0)
            {
                return;
            }
            RefreshManagedSelection(
                interiorChanged,
                interiorChanged,
                false);
        }

        private void ApplyManagedShadowSelection(string reason)
        {
            _managedSceneHandle = SceneManager.GetActiveScene().handle;
            RefreshManagedInteriorState();
            if (_managedActiveLights.Count == 0
                && _managedLightCache.Count == 0)
            {
                _managedInitialFillPending = true;
            }
            DiscoverManagedLights();
            bool settingsChanged = _managedSettingsDirty || !string.IsNullOrEmpty(reason);
            _managedSettingsDirty = false;
            RefreshManagedSelection(
                settingsChanged || !_preferViewRelevantLights.Value,
                true,
                settingsChanged);
            _nextManagedSelectionRefresh =
                Time.unscaledTime + _selectionRefreshSeconds.Value;
            ReportManagedDiagnostics(reason);
        }

        private void DiscoverManagedLights()
        {
            _managedDiscoveryIds.Clear();
            _managedExternalPlayerLights.Clear();
            _managedExternalPlayerLightIds.Clear();
            ResetManagedExclusionCounts();

            Camera camera = FindManagedGameCamera();
            bool hasCamera = camera != null;
            Vector3 cameraPosition = hasCamera
                ? camera.transform.position
                : Vector3.zero;
            float discoveryDistance = EffectiveMaximumDistance()
                + _selectionHysteresisMeters.Value;
            LocationTemplate placedBonfireTemplate =
                _excludePlacedBonfireLights.Value
                    ? FindPlacedBonfireTemplate()
                    : null;
            object parentConfig = GetParentConfig();
            bool includeInactive = ReadParentBool(
                parentConfig,
                _parentIncludeInactiveLightsField,
                false);
            FindObjectsInactive inactiveMode = includeInactive
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                inactiveMode,
                FindObjectsSortMode.None);

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (!IsLoadedSceneLight(light))
                {
                    continue;
                }

                float distance = hasCamera
                    ? Vector3.Distance(cameraPosition, light.transform.position)
                    : 0f;
                if (hasCamera && distance > discoveryDistance)
                {
                    continue;
                }

                if (IsExternallyOwnedPlayerLight(light))
                {
                    TrackExternalPlayerLights(light);
                    _managedExcludedExternalPlayerLights++;
                    LogExcludedLightOnce(light, "external HeroLight owner");
                    continue;
                }
                if (!IsEligibleManagedLight(light, parentConfig, includeInactive))
                {
                    continue;
                }

                int id = light.GetInstanceID();
                bool wasManaged = _managedLightStates.ContainsKey(id);
                if (!wasManaged && light.shadows != LightShadows.None)
                {
                    continue;
                }

                if (ShouldExcludeManagedLight(light, placedBonfireTemplate))
                {
                    continue;
                }

                _managedDiscoveryIds.Add(id);
                CachedManagedLight cached;
                if (_managedLightCache.TryGetValue(id, out cached))
                {
                    cached.Light = light;
                }
                else
                {
                    _managedLightCache.Add(id, new CachedManagedLight(light, id));
                }
            }

            RefreshExternalPlayerShadowFaceReservation();

            _managedIdScratch.Clear();
            foreach (int id in _managedLightCache.Keys)
            {
                if (!_managedDiscoveryIds.Contains(id))
                {
                    _managedIdScratch.Add(id);
                }
            }
            for (int i = 0; i < _managedIdScratch.Count; i++)
            {
                _managedLightCache.Remove(_managedIdScratch[i]);
            }
        }

        private bool IsExternallyOwnedPlayerLight(Light light)
        {
            if (_respectExternalPlayerLightOwnership == null
                || !_respectExternalPlayerLightOwnership.Value
                || (!_mageLightInstalled && !_noPlayerLightInstalled))
            {
                return false;
            }
            return IsHeroLight(light);
        }

        private static bool IsHeroLight(Light light)
        {
            try
            {
                IndoorGameObjectSwapper swapper =
                    light.GetComponentInParent<IndoorGameObjectSwapper>(true);
                if (swapper == null)
                {
                    return false;
                }
                Transform current = light.transform;
                while (current != null)
                {
                    if (current.name.IndexOf(
                        "HeroLight",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                    if (current == swapper.transform)
                    {
                        break;
                    }
                    current = current.parent;
                }
            }
            catch
            {
            }
            return false;
        }

        private void TrackExternalPlayerLights(Light discoveredLight)
        {
            IndoorGameObjectSwapper swapper = null;
            try
            {
                swapper = discoveredLight.GetComponentInParent<
                    IndoorGameObjectSwapper>(true);
            }
            catch
            {
            }
            Light[] ownedLights = swapper != null
                ? swapper.GetComponentsInChildren<Light>(true)
                : new[] { discoveredLight };
            for (int i = 0; i < ownedLights.Length; i++)
            {
                Light ownedLight = ownedLights[i];
                if (!IsLoadedSceneLight(ownedLight)
                    || !IsExternallyOwnedPlayerLight(ownedLight))
                {
                    continue;
                }
                int id = ownedLight.GetInstanceID();
                if (_managedExternalPlayerLightIds.Add(id))
                {
                    _managedExternalPlayerLights.Add(ownedLight);
                }
            }
        }

        private void RefreshExternalPlayerShadowFaceReservation()
        {
            _managedExternalPlayerShadowMapFaces = 0;
            if (_respectExternalPlayerLightOwnership == null
                || !_respectExternalPlayerLightOwnership.Value
                || !_mageLightInstalled)
            {
                return;
            }
            for (int i = 0; i < _managedExternalPlayerLights.Count; i++)
            {
                Light light = _managedExternalPlayerLights[i];
                if (!IsLightAliveManaged(light)
                    || !light.gameObject.activeInHierarchy
                    || !light.enabled
                    || light.shadows == LightShadows.None)
                {
                    continue;
                }
                if (light.type == LightType.Point || light.type == LightType.Spot)
                {
                    _managedExternalPlayerShadowMapFaces +=
                        ShadowMapFaceCost(light);
                }
            }
        }

        private bool IsEligibleManagedLight(
            Light light,
            object parentConfig,
            bool includeInactive)
        {
            if (!includeInactive && !light.gameObject.activeInHierarchy)
            {
                return false;
            }
            if (ReadParentBool(parentConfig, _parentOnlyEnabledLightsField, true)
                && !light.enabled)
            {
                return false;
            }
            if (ReadParentBool(parentConfig, _parentOnlyPointAndSpotField, true)
                && light.type != LightType.Point
                && light.type != LightType.Spot)
            {
                return false;
            }
            return light.type == LightType.Point || light.type == LightType.Spot;
        }

        private bool ShouldExcludeManagedLight(
            Light light,
            LocationTemplate placedBonfireTemplate)
        {
            if (_excludeHeroLight.Value && IsHeroLight(light))
            {
                _managedExcludedHeroLights++;
                LogExcludedLightOnce(light, "HeroLight");
                return true;
            }
            if (_excludeWyrdSightLights.Value && IsWyrdSightLight(light))
            {
                _managedExcludedWyrdSightLights++;
                LogExcludedLightOnce(light, "Wyrd Sight");
                return true;
            }
            if (_excludeSummonLights.Value && IsAttachedToSummon(light))
            {
                _managedExcludedSummonLights++;
                LogExcludedLightOnce(light, "summon");
                return true;
            }
            if (_excludeInterfacePreviewLights.Value
                && IsAttachedToInterfacePreview(light))
            {
                _managedExcludedInterfaceLights++;
                LogExcludedLightOnce(light, "interface preview");
                return true;
            }
            if (_excludeLockpickingLights.Value
                && IsAttachedToLockpickingView(light))
            {
                _managedExcludedLockpickingLights++;
                LogExcludedLightOnce(light, "lockpicking");
                return true;
            }
            if (_excludePlacedBonfireLights.Value
                && IsAttachedToPlacedBonfire(light, placedBonfireTemplate))
            {
                _managedExcludedPlacedBonfireLights++;
                LogExcludedLightOnce(light, "placed Bonfire");
                return true;
            }
            if (_protectBonfireLights.Value && ShouldExcludeLight(light))
            {
                _managedExcludedConfiguredLights++;
                LogExcludedLightOnce(light, "configured path");
                return true;
            }
            return false;
        }

        private bool IsWyrdSightLight(Light light)
        {
            if (!string.Equals(light.name, "WyrdLight", StringComparison.Ordinal))
            {
                return false;
            }

            if (!_wyrdSightTypeResolved)
            {
                _wyrdSightTypeResolved = true;
                _wyrdSightGlowEffectType = AccessTools.TypeByName(
                    "WyrdSight.Glow.ItemGlowEffect");
            }
            if (_wyrdSightGlowEffectType == null)
            {
                return true;
            }

            try
            {
                return light.GetComponentInParent(
                    _wyrdSightGlowEffectType,
                    true) != null;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsAttachedToSummon(Light light)
        {
            try
            {
                NpcController controller =
                    light.GetComponentInParent<NpcController>(true);
                return controller != null
                    && controller.Npc != null
                    && controller.Npc.IsSummon;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAttachedToInterfacePreview(Light light)
        {
            try
            {
                return light.GetComponentInParent<VHeroRenderer>(true) != null
                    || light.GetComponentInParent<VItemRenderer>(true) != null
                    || light.GetComponentInParent<VObjectCloseup>(true) != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAttachedToLockpickingView(Light light)
        {
            try
            {
                return light.GetComponentInParent<VLockpicking3D>(true) != null;
            }
            catch
            {
                return false;
            }
        }

        private static LocationTemplate FindPlacedBonfireTemplate()
        {
            try
            {
                ItemTemplate bonfireItem = CommonReferences.Get != null
                    && CommonReferences.Get.Bonfire != null
                        ? CommonReferences.Get.Bonfire.ItemTemplate(Hero.Current)
                        : null;
                ItemPlaceLocationSpec placement = bonfireItem != null
                    ? bonfireItem.GetComponentInChildren<ItemPlaceLocationSpec>(true)
                    : null;
                return placement != null ? placement.LocationTemplate : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAttachedToPlacedBonfire(
            Light light,
            LocationTemplate placedBonfireTemplate)
        {
            if (placedBonfireTemplate == null)
            {
                return false;
            }
            try
            {
                VSpawnedLocation spawnedLocation =
                    light.GetComponentInParent<VSpawnedLocation>(true);
                return spawnedLocation != null
                    && spawnedLocation.Target != null
                    && spawnedLocation.Target.Template == placedBonfireTemplate;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshManagedSelection(
            bool forceAllReplacements,
            bool reapplyActiveSettings,
            bool forceUnknownHdrpWrites)
        {
            bool interiorChanged = RefreshManagedInteriorState();
            if (interiorChanged)
            {
                forceAllReplacements = true;
                reapplyActiveSettings = true;
            }
            RefreshExternalPlayerShadowFaceReservation();
            Camera camera = FindManagedGameCamera();
            bool hasCamera = camera != null;
            Vector3 cameraPosition = hasCamera
                ? camera.transform.position
                : Vector3.zero;
            Plane[] frustumPlanes = null;
            if (_preferViewRelevantLights.Value && hasCamera)
            {
                GeometryUtility.CalculateFrustumPlanes(
                    camera,
                    _managedFrustumPlanes);
                frustumPlanes = _managedFrustumPlanes;
            }

            float now = Time.unscaledTime;
            float maximumDistance = EffectiveMaximumDistance();
            object parentConfig = GetParentConfig();
            bool includeInactive = ReadParentBool(
                parentConfig,
                _parentIncludeInactiveLightsField,
                false);
            _managedViewCandidates.Clear();
            _managedOffscreenCandidates.Clear();
            _managedCandidateLookup.Clear();
            _managedScanViewRelevantCount = 0;
            _managedScanPointLights = 0;
            _managedScanSpotLights = 0;

            foreach (CachedManagedLight cached in _managedLightCache.Values)
            {
                Light light = cached.Light;
                if (!IsLoadedSceneLight(light))
                {
                    continue;
                }
                if (IsExternallyOwnedPlayerLight(light))
                {
                    TrackExternalPlayerLights(light);
                    continue;
                }
                if (!IsEligibleManagedLight(
                        light,
                        parentConfig,
                        includeInactive))
                {
                    continue;
                }
                int id = cached.Id;
                bool wasActive = _managedActiveLights.Contains(id);
                if (!wasActive && light.shadows != LightShadows.None)
                {
                    continue;
                }

                float distance = hasCamera
                    ? Vector3.Distance(cameraPosition, light.transform.position)
                    : 0f;
                if (hasCamera
                    && !SafeShadowSelectionRules.IsWithinSelectionDistance(
                        distance,
                        wasActive,
                        maximumDistance,
                        _selectionHysteresisMeters.Value))
                {
                    continue;
                }

                bool intersectsView = !_preferViewRelevantLights.Value
                    || !hasCamera
                    || LightVolumeIntersectsView(light, frustumPlanes);
                if (intersectsView)
                {
                    cached.LastViewIntersectionTime = now;
                }
                bool viewRelevant = !_preferViewRelevantLights.Value
                    || !hasCamera
                    || SafeShadowSelectionRules.IsEffectivelyViewRelevant(
                        intersectsView,
                        wasActive,
                        now - cached.LastViewIntersectionTime,
                        _viewExitDelaySeconds.Value);
                float screenCenterWeight = intersectsView && hasCamera
                    ? CalculateManagedScreenCenterWeight(light, camera)
                    : 0f;
                float score = SafeShadowSelectionRules.CalculateCandidateScore(
                    distance,
                    light.intensity,
                    light.range,
                    wasActive,
                    _selectionRetentionMeters.Value,
                    screenCenterWeight,
                    _screenCenterPriorityMeters.Value);
                int faceCost = ShadowMapFaceCost(light);
                ManagedCandidate candidate = new ManagedCandidate(
                    light,
                    id,
                    score,
                    viewRelevant,
                    faceCost);
                _managedCandidateLookup[id] = candidate;
                if (viewRelevant)
                {
                    _managedViewCandidates.Add(candidate);
                    _managedScanViewRelevantCount++;
                }
                else
                {
                    _managedOffscreenCandidates.Add(candidate);
                }
                if (light.type == LightType.Point)
                {
                    _managedScanPointLights++;
                }
                else
                {
                    _managedScanSpotLights++;
                }
            }

            RefreshExternalPlayerShadowFaceReservation();
            _managedViewCandidates.Sort(CompareManagedCandidates);
            _managedOffscreenCandidates.Sort(CompareManagedCandidates);
            BuildManagedDesiredSelection();
            ReconcileManagedSelection(forceAllReplacements);

            if (reapplyActiveSettings)
            {
                _managedIdScratch.Clear();
                foreach (int id in _managedActiveLights)
                {
                    _managedIdScratch.Add(id);
                }
                for (int i = 0; i < _managedIdScratch.Count; i++)
                {
                    int id = _managedIdScratch[i];
                    ManagedCandidate candidate;
                    ManagedLightState state;
                    if (_managedCandidateLookup.TryGetValue(id, out candidate)
                        && _managedLightStates.TryGetValue(id, out state))
                    {
                        ApplyManagedLight(
                            candidate,
                            state,
                            false,
                            forceUnknownHdrpWrites,
                            ManagedStrengthMultiplier(id));
                    }
                }
            }

            MirrorManagedActiveLightsToParent();
            UpdateManagedScanTotals();
        }

        private void BuildManagedDesiredSelection()
        {
            _managedDesiredCandidates.Clear();
            _managedDesiredIds.Clear();
            int maximumLights = EffectiveMaximumLights();
            int maximumFaces = EffectiveMaximumShadowMapFaces();
            int reserve = _preferViewRelevantLights.Value
                ? Math.Min(
                    _offscreenReserveLights.Value,
                    _managedOffscreenCandidates.Count)
                : 0;
            int initialViewLimit = Math.Max(0, maximumLights - reserve);
            int facesUsed = 0;

            AddManagedCandidatesWithinBudget(
                _managedViewCandidates,
                0,
                initialViewLimit,
                maximumLights,
                maximumFaces,
                ref facesUsed);
            AddManagedCandidatesWithinBudget(
                _managedOffscreenCandidates,
                0,
                maximumLights,
                maximumLights,
                maximumFaces,
                ref facesUsed);
            AddManagedCandidatesWithinBudget(
                _managedViewCandidates,
                initialViewLimit,
                maximumLights,
                maximumLights,
                maximumFaces,
                ref facesUsed);
        }

        private void AddManagedCandidatesWithinBudget(
            List<ManagedCandidate> source,
            int startIndex,
            int maximumToConsider,
            int maximumLights,
            int maximumFaces,
            ref int facesUsed)
        {
            int considered = 0;
            for (int i = startIndex;
                i < source.Count
                    && considered < maximumToConsider
                    && _managedDesiredCandidates.Count < maximumLights;
                i++)
            {
                considered++;
                ManagedCandidate candidate = source[i];
                if (!SafeShadowSelectionRules.FitsSelectionBudget(
                    _managedDesiredCandidates.Count,
                    facesUsed,
                    candidate.FaceCost,
                    maximumLights,
                    maximumFaces))
                {
                    continue;
                }
                _managedDesiredCandidates.Add(candidate);
                _managedDesiredIds.Add(candidate.Id);
                facesUsed += candidate.FaceCost;
            }
        }

        private void ReconcileManagedSelection(bool forceAllReplacements)
        {
            _managedScanActivatedLights = 0;
            _managedScanRestoredLights = 0;
            _managedScanSwaps = 0;
            ValidateManagedShadowHandoff();

            _managedIdScratch.Clear();
            foreach (int id in _managedActiveLights)
            {
                if (!_managedCandidateLookup.ContainsKey(id))
                {
                    _managedIdScratch.Add(id);
                }
            }
            RestoreManagedIds(_managedIdScratch);
            ValidateManagedShadowHandoff();

            int desiredCount = _managedDesiredCandidates.Count;
            int maximumFaces = EffectiveMaximumShadowMapFaces();
            while (_managedActiveLights.Count > desiredCount
                || CurrentManagedFaceCount() > maximumFaces)
            {
                ManagedCandidate worst;
                if (!TryFindWorstManagedActiveOutsideDesired(out worst)
                    && !TryFindWorstManagedActive(out worst))
                {
                    break;
                }
                if (RestoreManagedLight(worst.Id))
                {
                    _managedScanRestoredLights++;
                }
            }

            int activationLimit =
                SafeShadowSelectionRules.ResolveInitialFillActivationLimit(
                    _managedInitialFillPending,
                    _initialFillBatchSize.Value,
                    desiredCount - _managedActiveLights.Count);
            int activatedThisFill = 0;
            for (int i = 0;
                i < _managedDesiredCandidates.Count
                    && _managedActiveLights.Count < desiredCount
                    && activatedThisFill < activationLimit;
                i++)
            {
                ManagedCandidate candidate = _managedDesiredCandidates[i];
                if (_managedActiveLights.Contains(candidate.Id)
                    || CurrentManagedFaceCount() + candidate.FaceCost > maximumFaces)
                {
                    continue;
                }
                ActivateNewManagedLight(candidate);
                _managedScanActivatedLights++;
                activatedThisFill++;
            }

            if (_managedActiveLights.Count >= desiredCount
                || _initialFillBatchSize.Value <= 0)
            {
                _managedInitialFillPending = false;
            }
            if (_managedInitialFillPending || _managedShadowHandoff != null)
            {
                return;
            }

            int replacementLimit = forceAllReplacements
                ? int.MaxValue
                : _maximumSelectionSwapsPerRefresh.Value;
            for (int replacement = 0; replacement < replacementLimit; replacement++)
            {
                ManagedCandidate desired;
                ManagedCandidate current;
                if (!TryFindBestMissingDesired(out desired)
                    || !TryFindWorstManagedActiveOutsideDesired(out current))
                {
                    break;
                }
                int resultingFaces = CurrentManagedFaceCount()
                    - current.FaceCost
                    + desired.FaceCost;
                if (resultingFaces > maximumFaces)
                {
                    break;
                }
                if (_shadowHandoffSeconds.Value > 0f)
                {
                    if (StartManagedShadowHandoff(current, desired))
                    {
                        _managedScanSwaps++;
                    }
                    break;
                }
                if (RestoreManagedLight(current.Id))
                {
                    _managedScanRestoredLights++;
                }
                ActivateNewManagedLight(desired);
                _managedScanActivatedLights++;
                _managedScanSwaps++;
            }
        }

        private bool StartManagedShadowHandoff(
            ManagedCandidate outgoing,
            ManagedCandidate incoming)
        {
            if (_managedShadowHandoff != null
                || !_managedActiveLights.Contains(outgoing.Id)
                || _managedActiveLights.Contains(incoming.Id)
                || !IsLightAliveManaged(incoming.Light))
            {
                return false;
            }

            int resultingFaces = CurrentManagedFaceCount()
                - outgoing.FaceCost
                + incoming.FaceCost;
            if (resultingFaces > EffectiveMaximumShadowMapFaces())
            {
                return false;
            }

            _managedShadowHandoff = new ManagedShadowHandoff(
                outgoing.Id,
                incoming,
                Time.unscaledTime);
            return true;
        }

        private void ValidateManagedShadowHandoff()
        {
            ManagedShadowHandoff handoff = _managedShadowHandoff;
            if (handoff == null)
            {
                return;
            }

            int activeId = handoff.IncomingActivated
                ? handoff.Incoming.Id
                : handoff.OutgoingId;
            if (!_managedActiveLights.Contains(activeId)
                || !_managedDesiredIds.Contains(handoff.Incoming.Id)
                || !_managedCandidateLookup.ContainsKey(handoff.Incoming.Id)
                || (!handoff.IncomingActivated
                    && !_managedCandidateLookup.ContainsKey(
                        handoff.OutgoingId)))
            {
                CancelManagedShadowHandoff("selection changed");
            }
        }

        private void AdvanceManagedShadowHandoff()
        {
            ManagedShadowHandoff handoff = _managedShadowHandoff;
            if (handoff == null)
            {
                return;
            }

            ShadowHandoffProgress progress =
                SafeShadowSelectionRules.ResolveShadowHandoffProgress(
                    Time.unscaledTime - handoff.StartedAt,
                    _shadowHandoffSeconds.Value);
            if (!handoff.IncomingActivated)
            {
                ManagedCandidate outgoingCandidate;
                ManagedLightState outgoingState;
                if (!_managedActiveLights.Contains(handoff.OutgoingId)
                    || !_managedCandidateLookup.TryGetValue(
                        handoff.OutgoingId,
                        out outgoingCandidate)
                    || !_managedLightStates.TryGetValue(
                        handoff.OutgoingId,
                        out outgoingState)
                    || !IsLightAliveManaged(outgoingState.Light))
                {
                    _managedShadowHandoff = null;
                    return;
                }
                if (!_managedDesiredIds.Contains(handoff.Incoming.Id)
                    || !_managedCandidateLookup.ContainsKey(handoff.Incoming.Id)
                    || !IsLightAliveManaged(handoff.Incoming.Light))
                {
                    CancelManagedShadowHandoff(
                        "incoming light became unavailable");
                    return;
                }
                if (progress.Phase == ShadowHandoffPhase.FadeOut)
                {
                    handoff.CurrentStrengthMultiplier =
                        progress.StrengthMultiplier;
                    ApplyManagedLight(
                        outgoingCandidate,
                        outgoingState,
                        false,
                        false,
                        handoff.CurrentStrengthMultiplier);
                    return;
                }

                ApplyManagedLight(
                    outgoingCandidate,
                    outgoingState,
                    false,
                    false,
                    0f);
                if (RestoreManagedLight(handoff.OutgoingId))
                {
                    _managedScanRestoredLights++;
                }
                handoff.IncomingActivated = true;
                handoff.CurrentStrengthMultiplier = 0f;
                ActivateNewManagedLight(handoff.Incoming, 0f);
                _managedScanActivatedLights++;
                MirrorManagedActiveLightsToParent();
                UpdateManagedScanTotals();
            }

            if (progress.Phase == ShadowHandoffPhase.Complete)
            {
                handoff.CurrentStrengthMultiplier = 1f;
                ApplyManagedHandoffIncoming(handoff, 1f);
                _managedShadowHandoff = null;
                return;
            }

            handoff.CurrentStrengthMultiplier = progress.StrengthMultiplier;
            ApplyManagedHandoffIncoming(
                handoff,
                handoff.CurrentStrengthMultiplier);
        }

        private void ApplyManagedHandoffIncoming(
            ManagedShadowHandoff handoff,
            float strengthMultiplier)
        {
            ManagedCandidate candidate;
            ManagedLightState state;
            if (_managedCandidateLookup.TryGetValue(
                    handoff.Incoming.Id,
                    out candidate)
                && _managedLightStates.TryGetValue(
                    handoff.Incoming.Id,
                    out state))
            {
                ApplyManagedLight(
                    candidate,
                    state,
                    false,
                    false,
                    strengthMultiplier);
            }
        }

        private void CancelManagedShadowHandoff(string reason)
        {
            ManagedShadowHandoff handoff = _managedShadowHandoff;
            if (handoff == null)
            {
                return;
            }
            _managedShadowHandoff = null;

            int activeId = handoff.IncomingActivated
                ? handoff.Incoming.Id
                : handoff.OutgoingId;
            ManagedCandidate candidate;
            ManagedLightState state;
            if (_managedCandidateLookup.TryGetValue(activeId, out candidate)
                && _managedLightStates.TryGetValue(activeId, out state))
            {
                ApplyManagedLight(candidate, state, false, false, 1f);
            }
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo("Cancelled shadow handoff: " + reason + ".");
            }
        }

        private float ManagedStrengthMultiplier(int id)
        {
            ManagedShadowHandoff handoff = _managedShadowHandoff;
            if (handoff == null)
            {
                return 1f;
            }
            if ((!handoff.IncomingActivated && id == handoff.OutgoingId)
                || (handoff.IncomingActivated && id == handoff.Incoming.Id))
            {
                return handoff.CurrentStrengthMultiplier;
            }
            return 1f;
        }

        private void RestoreManagedIds(List<int> ids)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (RestoreManagedLight(ids[i]))
                {
                    _managedScanRestoredLights++;
                }
            }
        }

        private bool TryFindWorstManagedActiveOutsideDesired(
            out ManagedCandidate worst)
        {
            _managedCandidateScratch.Clear();
            foreach (int id in _managedActiveLights)
            {
                ManagedCandidate candidate;
                if (!_managedDesiredIds.Contains(id)
                    && _managedCandidateLookup.TryGetValue(id, out candidate))
                {
                    _managedCandidateScratch.Add(candidate);
                }
            }
            return TryTakeWorstManagedCandidate(_managedCandidateScratch, out worst);
        }

        private bool TryFindWorstManagedActive(out ManagedCandidate worst)
        {
            _managedCandidateScratch.Clear();
            foreach (int id in _managedActiveLights)
            {
                ManagedCandidate candidate;
                if (_managedCandidateLookup.TryGetValue(id, out candidate))
                {
                    _managedCandidateScratch.Add(candidate);
                }
            }
            return TryTakeWorstManagedCandidate(_managedCandidateScratch, out worst);
        }

        private static bool TryTakeWorstManagedCandidate(
            List<ManagedCandidate> candidates,
            out ManagedCandidate worst)
        {
            worst = default(ManagedCandidate);
            if (candidates.Count == 0)
            {
                return false;
            }
            candidates.Sort(CompareManagedCandidatesWorstFirst);
            worst = candidates[0];
            return true;
        }

        private bool TryFindBestMissingDesired(out ManagedCandidate desired)
        {
            for (int i = 0; i < _managedDesiredCandidates.Count; i++)
            {
                if (!_managedActiveLights.Contains(_managedDesiredCandidates[i].Id))
                {
                    desired = _managedDesiredCandidates[i];
                    return true;
                }
            }
            desired = default(ManagedCandidate);
            return false;
        }

        private void ActivateNewManagedLight(
            ManagedCandidate candidate,
            float strengthMultiplier = 1f)
        {
            ManagedLightState state;
            if (!_managedLightStates.TryGetValue(candidate.Id, out state))
            {
                state = CaptureManagedLightState(candidate.Light);
                _managedLightStates.Add(candidate.Id, state);
            }
            ApplyManagedLight(
                candidate,
                state,
                true,
                true,
                strengthMultiplier);
            _managedActiveLights.Add(candidate.Id);
        }

        private ManagedLightState CaptureManagedLightState(Light light)
        {
            ResolveManagedHdrpMembers();
            Component hdData = _hdAdditionalLightDataType != null
                ? light.GetComponent(_hdAdditionalLightDataType)
                : null;
            return new ManagedLightState(
                light,
                light.shadows,
                light.shadowStrength,
                CaptureManagedHdrpState(hdData));
        }

        private ManagedHdrpState CaptureManagedHdrpState(Component hdData)
        {
            if (hdData == null)
            {
                return ManagedHdrpState.Empty;
            }

            bool? shadowsEnabled = ReadManagedHdrpShadowsEnabled(hdData);
            float? shadowDimmer = ReadManagedFloatMember(
                _hdShadowDimmerMember,
                hdData);
            float? volumetricDimmer = ReadManagedFloatMember(
                _hdVolumetricShadowDimmerMember,
                hdData);
            int resolutionOverride = 0;
            int resolutionLevel = 0;
            bool resolutionUseOverride = false;
            bool hasResolution = TryCaptureManagedResolution(
                hdData,
                out resolutionOverride,
                out resolutionLevel,
                out resolutionUseOverride);
            return new ManagedHdrpState(
                hdData,
                shadowsEnabled,
                shadowDimmer,
                volumetricDimmer,
                hasResolution,
                resolutionOverride,
                resolutionLevel,
                resolutionUseOverride);
        }

        private void ApplyManagedLight(
            ManagedCandidate candidate,
            ManagedLightState state,
            bool newlyActivated,
            bool forceUnknownHdrpWrites,
            float strengthMultiplier = 1f)
        {
            Light light = candidate.Light;
            if (!IsLightAliveManaged(light))
            {
                return;
            }

            LightShadows desiredMode = ParentDesiredShadowMode();
            float desiredStrength = ParentShadowStrength()
                * Mathf.Clamp01(strengthMultiplier);
            bool nativeChanged = false;
            if (light.shadows != desiredMode)
            {
                light.shadows = desiredMode;
                nativeChanged = true;
            }
            if (!Mathf.Approximately(light.shadowStrength, desiredStrength))
            {
                light.shadowStrength = desiredStrength;
                nativeChanged = true;
            }

            ManagedHdrpState hdrp = state.Hdrp;
            if (hdrp.HdData != null)
            {
                bool? currentShadowsEnabled =
                    ReadManagedHdrpShadowsEnabled(hdrp.HdData);
                if ((currentShadowsEnabled.HasValue
                        && !currentShadowsEnabled.Value)
                    || (!currentShadowsEnabled.HasValue
                        && (newlyActivated
                            || nativeChanged
                            || forceUnknownHdrpWrites)))
                {
                    TryEnableManagedHdrpShadows(hdrp.HdData, true);
                }
                WriteManagedFloatIfChanged(
                    _hdShadowDimmerMember,
                    hdrp.HdData,
                    desiredStrength);
                WriteManagedFloatIfChanged(
                    _hdVolumetricShadowDimmerMember,
                    hdrp.HdData,
                    _suppressAddedVolumetricShadows.Value
                        ? 0f
                        : desiredStrength);
                ApplyManagedResolutionCap(hdrp);
                if (newlyActivated || nativeChanged)
                {
                    RequestManagedShadowMap(hdrp.HdData);
                }
            }
        }

        private bool RestoreManagedLight(int id)
        {
            ManagedLightState state;
            _managedActiveLights.Remove(id);
            if (!_managedLightStates.TryGetValue(id, out state))
            {
                return false;
            }
            _managedLightStates.Remove(id);
            Light light = state.Light;
            if (!IsLightAliveManaged(light))
            {
                return false;
            }

            try
            {
                light.shadows = state.OriginalShadows;
                light.shadowStrength = state.OriginalShadowStrength;
                RestoreManagedHdrpState(state.Hdrp);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not restore managed light '"
                    + light.name
                    + "': "
                    + exception.Message);
                return false;
            }
        }

        private void RestoreManagedHdrpState(ManagedHdrpState state)
        {
            Component hdData = state.HdData;
            if (hdData == null)
            {
                return;
            }
            if (state.ShadowsEnabled.HasValue)
            {
                bool? current = ReadManagedHdrpShadowsEnabled(hdData);
                if (!current.HasValue
                    || current.Value != state.ShadowsEnabled.Value)
                {
                    TryEnableManagedHdrpShadows(
                        hdData,
                        state.ShadowsEnabled.Value);
                }
            }
            if (state.ShadowDimmer.HasValue)
            {
                WriteManagedFloatIfChanged(
                    _hdShadowDimmerMember,
                    hdData,
                    state.ShadowDimmer.Value);
            }
            if (state.VolumetricShadowDimmer.HasValue)
            {
                WriteManagedFloatIfChanged(
                    _hdVolumetricShadowDimmerMember,
                    hdData,
                    state.VolumetricShadowDimmer.Value);
            }
            if (state.HasResolution)
            {
                RestoreManagedResolution(state);
            }
        }

        private void RestoreAllManagedLights(string reason)
        {
            if (_managedLightStates.Count == 0
                && _managedLightCache.Count == 0
                && _managedExternalPlayerLights.Count == 0)
            {
                return;
            }

            _managedIdScratch.Clear();
            foreach (int id in _managedLightStates.Keys)
            {
                _managedIdScratch.Add(id);
            }
            int restored = 0;
            for (int i = 0; i < _managedIdScratch.Count; i++)
            {
                if (RestoreManagedLight(_managedIdScratch[i]))
                {
                    restored++;
                }
            }
            ClearManagedSelectionCollections();
            MirrorManagedActiveLightsToParent();
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(
                    "Safe shadow controller restored "
                    + restored.ToString(CultureInfo.InvariantCulture)
                    + " light(s) ("
                    + reason
                    + ").");
            }
        }

        private void ClearManagedSelectionCollections()
        {
            _managedShadowHandoff = null;
            _managedInitialFillPending = true;
            _managedInteriorActive = false;
            _managedLightStates.Clear();
            _managedActiveLights.Clear();
            _managedLightCache.Clear();
            _managedDiscoveryIds.Clear();
            _managedDesiredIds.Clear();
            _managedCandidateLookup.Clear();
            _managedViewCandidates.Clear();
            _managedOffscreenCandidates.Clear();
            _managedDesiredCandidates.Clear();
            _managedCandidateScratch.Clear();
            _managedExternalPlayerLights.Clear();
            _managedExternalPlayerLightIds.Clear();
            _managedExternalPlayerShadowMapFaces = 0;
            _managedGameCamera = null;
        }

        private void ResolveManagedHdrpMembers()
        {
            if (_managedHdrpResolved)
            {
                return;
            }
            ResolveHdrpMembers();
            if (_hdAdditionalLightDataType == null)
            {
                return;
            }

            BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            _hdShadowsEnabledMethod = _hdAdditionalLightDataType.GetMethod(
                "ShadowsEnabled",
                flags,
                null,
                Type.EmptyTypes,
                null);
            _hdRequestShadowMapRenderingMethod =
                _hdAdditionalLightDataType.GetMethod(
                    "RequestShadowMapRendering",
                    flags,
                    null,
                    Type.EmptyTypes,
                    null);
            _managedHdrpResolved = true;
        }

        private bool? ReadManagedHdrpShadowsEnabled(Component hdData)
        {
            try
            {
                if (_hdShadowsEnabledMethod != null)
                {
                    object value = _hdShadowsEnabledMethod.Invoke(hdData, null);
                    if (value is bool)
                    {
                        return (bool)value;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private void TryEnableManagedHdrpShadows(Component hdData, bool enabledValue)
        {
            try
            {
                if (_hdEnableShadowsMethod != null)
                {
                    _hdEnableShadowsMethod.Invoke(
                        hdData,
                        new object[] { enabledValue });
                }
            }
            catch (Exception exception)
            {
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogWarning(
                        "Could not change HDRP shadow enablement: "
                        + exception.Message);
                }
            }
        }

        private static float? ReadManagedFloatMember(
            MemberInfo member,
            object target)
        {
            try
            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null && property.CanRead)
                {
                    object value = property.GetValue(target, null);
                    return value is float ? (float?)value : null;
                }
                FieldInfo field = member as FieldInfo;
                if (field != null)
                {
                    object value = field.GetValue(target);
                    return value is float ? (float?)value : null;
                }
            }
            catch
            {
            }
            return null;
        }

        private void WriteManagedFloatIfChanged(
            MemberInfo member,
            object target,
            float value)
        {
            float? current = ReadManagedFloatMember(member, target);
            if (current.HasValue && Mathf.Approximately(current.Value, value))
            {
                return;
            }
            SetFloatMember(member, target, value);
        }

        private bool TryCaptureManagedResolution(
            Component hdData,
            out int resolutionOverride,
            out int resolutionLevel,
            out bool resolutionUseOverride)
        {
            resolutionOverride = 0;
            resolutionLevel = 0;
            resolutionUseOverride = false;
            try
            {
                if (_hdShadowResolutionProperty == null
                    || _hdSetShadowResolutionMethod == null
                    || _hdSetShadowResolutionLevelMethod == null
                    || _hdSetShadowResolutionOverrideMethod == null)
                {
                    return false;
                }
                object resolution = _hdShadowResolutionProperty.GetValue(
                    hdData,
                    null);
                return resolution != null
                    && TryReadRuntimeMember(
                        resolution,
                        "override",
                        out resolutionOverride)
                    && TryReadRuntimeMember(
                        resolution,
                        "level",
                        out resolutionLevel)
                    && TryReadRuntimeMember(
                        resolution,
                        "useOverride",
                        out resolutionUseOverride);
            }
            catch
            {
                return false;
            }
        }

        private void ApplyManagedResolutionCap(ManagedHdrpState state)
        {
            if (!state.HasResolution || state.HdData == null)
            {
                return;
            }
            if (!ShouldProtectShadowAtlas())
            {
                RestoreManagedResolution(state);
                return;
            }
            int cap = CurrentShadowResolutionCap();
            int target = state.ResolutionUseOverride
                ? Math.Min(state.ResolutionOverride, cap)
                : cap;
            try
            {
                object resolution = _hdShadowResolutionProperty.GetValue(
                    state.HdData,
                    null);
                int currentOverride = 0;
                bool currentUseOverride = false;
                bool currentKnown = resolution != null
                    && TryReadRuntimeMember(
                        resolution,
                        "override",
                        out currentOverride)
                    && TryReadRuntimeMember(
                        resolution,
                        "useOverride",
                        out currentUseOverride);
                if (!currentKnown || currentOverride != target)
                {
                    _hdSetShadowResolutionMethod.Invoke(
                        state.HdData,
                        new object[] { target });
                }
                if (!currentKnown || !currentUseOverride)
                {
                    _hdSetShadowResolutionOverrideMethod.Invoke(
                        state.HdData,
                        new object[] { true });
                }
            }
            catch (Exception exception)
            {
                ReportAtlasUnavailable(
                    "Could not constrain a managed light: "
                    + exception.Message);
            }
        }

        private void RestoreManagedResolution(ManagedHdrpState state)
        {
            try
            {
                object resolution = _hdShadowResolutionProperty.GetValue(
                    state.HdData,
                    null);
                int currentOverride = 0;
                int currentLevel = 0;
                bool currentUseOverride = false;
                bool currentKnown = resolution != null
                    && TryReadRuntimeMember(
                        resolution,
                        "override",
                        out currentOverride)
                    && TryReadRuntimeMember(
                        resolution,
                        "level",
                        out currentLevel)
                    && TryReadRuntimeMember(
                        resolution,
                        "useOverride",
                        out currentUseOverride);
                if (!currentKnown
                    || currentOverride != state.ResolutionOverride)
                {
                    _hdSetShadowResolutionMethod.Invoke(
                        state.HdData,
                        new object[] { state.ResolutionOverride });
                }
                if (!currentKnown || currentLevel != state.ResolutionLevel)
                {
                    _hdSetShadowResolutionLevelMethod.Invoke(
                        state.HdData,
                        new object[] { state.ResolutionLevel });
                }
                if (!currentKnown
                    || currentUseOverride != state.ResolutionUseOverride)
                {
                    _hdSetShadowResolutionOverrideMethod.Invoke(
                        state.HdData,
                        new object[] { state.ResolutionUseOverride });
                }
            }
            catch (Exception exception)
            {
                ReportAtlasUnavailable(
                    "Could not restore a selected light's resolution: "
                    + exception.Message);
            }
        }

        private void RequestManagedShadowMap(Component hdData)
        {
            try
            {
                if (_hdRequestShadowMapRenderingMethod != null)
                {
                    _hdRequestShadowMapRenderingMethod.Invoke(hdData, null);
                }
            }
            catch (Exception exception)
            {
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogWarning(
                        "Could not request an HDRP shadow-map refresh: "
                        + exception.Message);
                }
            }
        }

        private object GetParentConfig()
        {
            try
            {
                return _parentCurrentConfigProperty != null
                    ? _parentCurrentConfigProperty.GetValue(null, null)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool ReadParentBool(
            object parentConfig,
            FieldInfo field,
            bool fallback)
        {
            try
            {
                return parentConfig != null && field != null
                    ? (bool)field.GetValue(parentConfig)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private bool RefreshManagedInteriorState()
        {
            try
            {
                if (World.Services == null)
                {
                    return false;
                }
                SceneService sceneService = World.Services.TryGet<SceneService>();
                SceneLifetimeEvents lifetime = SceneLifetimeEvents.Get;
                if (sceneService == null
                    || lifetime == null
                    || !lifetime.EverythingInitialized)
                {
                    return false;
                }

                bool interior = !sceneService.IsOpenWorld || lifetime.InInterior;
                if (_managedInteriorActive == interior)
                {
                    return false;
                }
                _managedInteriorActive = interior;
                _managedSettingsDirty = true;
                _lastAtlasDiagnosticSignature = string.Empty;
                NudgeParentScan();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int EffectiveMaximumLights()
        {
            int maximum = _maximumUpgradedLights.Value;
            object parentConfig = GetParentConfig();
            if (ReadParentBool(parentConfig, _parentUseBudgetField, true)
                && parentConfig != null
                && _parentMaximumUpgradedLightsField != null)
            {
                maximum = Math.Min(
                    maximum,
                    (int)_parentMaximumUpgradedLightsField.GetValue(parentConfig));
            }
            if (_managedInteriorActive && _interiorPerformanceEnabled.Value)
            {
                maximum = Math.Min(
                    maximum,
                    _interiorMaximumUpgradedLights.Value);
            }
            if (_combatPerformanceActive && _combatLimitLightBudget.Value)
            {
                maximum = Math.Min(
                    maximum,
                    _combatMaximumUpgradedLights.Value);
            }
            return Math.Max(0, maximum);
        }

        private float EffectiveMaximumDistance()
        {
            float maximum = _maximumDistanceMeters.Value;
            object parentConfig = GetParentConfig();
            if (ReadParentBool(parentConfig, _parentUseBudgetField, true)
                && parentConfig != null
                && _parentMaximumDistanceMetersField != null)
            {
                maximum = Math.Min(
                    maximum,
                    (float)_parentMaximumDistanceMetersField.GetValue(parentConfig));
            }
            if (_managedInteriorActive && _interiorPerformanceEnabled.Value)
            {
                maximum = Math.Min(
                    maximum,
                    _interiorMaximumDistanceMeters.Value);
            }
            if (_combatPerformanceActive && _combatLimitDistance.Value)
            {
                maximum = Math.Min(
                    maximum,
                    _combatMaximumDistanceMeters.Value);
            }
            return Math.Max(1f, maximum);
        }

        private int EffectiveMaximumShadowMapFaces()
        {
            int maximum = _maximumShadowMapFaces.Value;
            if (_managedInteriorActive && _interiorPerformanceEnabled.Value)
            {
                maximum = Math.Min(
                    maximum,
                    _interiorMaximumShadowMapFaces.Value);
            }
            return SafeShadowSelectionRules.AvailableShadowMapFaces(
                maximum,
                _managedExternalPlayerShadowMapFaces);
        }

        private LightShadows ParentDesiredShadowMode()
        {
            try
            {
                object config = GetParentConfig();
                if (config != null && _parentDesiredShadowModeProperty != null)
                {
                    object value = _parentDesiredShadowModeProperty.GetValue(
                        config,
                        null);
                    if (value is LightShadows)
                    {
                        return (LightShadows)value;
                    }
                }
            }
            catch
            {
            }
            return LightShadows.Soft;
        }

        private float ParentShadowStrength()
        {
            try
            {
                object config = GetParentConfig();
                return config != null && _parentShadowStrengthField != null
                    ? Mathf.Clamp01(
                        (float)_parentShadowStrengthField.GetValue(config))
                    : 0.8f;
            }
            catch
            {
                return 0.8f;
            }
        }

        private Camera FindManagedGameCamera()
        {
            if (IsUsableManagedCamera(_managedGameCamera))
            {
                return _managedGameCamera;
            }
            Camera fallback = null;
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (!IsUsableManagedCamera(camera))
                {
                    continue;
                }
                if (camera == Camera.main)
                {
                    _managedGameCamera = camera;
                    return camera;
                }
                if (fallback == null)
                {
                    fallback = camera;
                }
            }
            _managedGameCamera = fallback;
            return fallback;
        }

        private static bool IsUsableManagedCamera(Camera camera)
        {
            try
            {
                return camera != null
                    && camera.enabled
                    && camera.gameObject.activeInHierarchy
                    && camera.cameraType == CameraType.Game;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLoadedSceneLight(Light light)
        {
            if (!IsLightAliveManaged(light))
            {
                return false;
            }
            Scene scene = light.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool IsLightAliveManaged(Light light)
        {
            try
            {
                return light != null && light.gameObject != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool LightVolumeIntersectsView(
            Light light,
            Plane[] frustumPlanes)
        {
            if (frustumPlanes == null)
            {
                return true;
            }
            float range = Mathf.Max(0.1f, light.range);
            Vector3 position = light.transform.position;
            for (int i = 0; i < frustumPlanes.Length; i++)
            {
                if (SafeShadowSelectionRules.IsSphereOutsideFrustumPlane(
                    frustumPlanes[i].GetDistanceToPoint(position),
                    range))
                {
                    return false;
                }
            }
            return true;
        }

        private static float CalculateManagedScreenCenterWeight(
            Light light,
            Camera camera)
        {
            try
            {
                Vector3 viewport = camera.WorldToViewportPoint(
                    light.transform.position);
                return SafeShadowSelectionRules.CalculateScreenCenterWeight(
                    viewport.x,
                    viewport.y,
                    viewport.z);
            }
            catch
            {
                return 0f;
            }
        }

        private static int ShadowMapFaceCost(Light light)
        {
            return SafeShadowSelectionRules.ShadowMapFaceCost(
                light.type == LightType.Point);
        }

        private static int CompareManagedCandidates(
            ManagedCandidate left,
            ManagedCandidate right)
        {
            int view = right.ViewRelevant.CompareTo(left.ViewRelevant);
            if (view != 0)
            {
                return view;
            }
            int score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.Id.CompareTo(right.Id);
        }

        private static int CompareManagedCandidatesWorstFirst(
            ManagedCandidate left,
            ManagedCandidate right)
        {
            int view = left.ViewRelevant.CompareTo(right.ViewRelevant);
            if (view != 0)
            {
                return view;
            }
            int score = left.Score.CompareTo(right.Score);
            return score != 0 ? score : right.Id.CompareTo(left.Id);
        }

        private int CurrentManagedFaceCount()
        {
            int faces = 0;
            foreach (int id in _managedActiveLights)
            {
                ManagedCandidate candidate;
                if (_managedCandidateLookup.TryGetValue(id, out candidate))
                {
                    faces += candidate.FaceCost;
                }
                else
                {
                    ManagedLightState state;
                    if (_managedLightStates.TryGetValue(id, out state)
                        && state.Light != null)
                    {
                        faces += ShadowMapFaceCost(state.Light);
                    }
                }
            }
            return faces;
        }

        private void MirrorManagedActiveLightsToParent()
        {
            HashSet<int> parentActive = GetActiveLights();
            if (parentActive == null)
            {
                return;
            }
            parentActive.Clear();
            foreach (int id in _managedActiveLights)
            {
                parentActive.Add(id);
            }
        }

        private void ResetManagedExclusionCounts()
        {
            _managedExcludedHeroLights = 0;
            _managedExcludedWyrdSightLights = 0;
            _managedExcludedSummonLights = 0;
            _managedExcludedInterfaceLights = 0;
            _managedExcludedLockpickingLights = 0;
            _managedExcludedPlacedBonfireLights = 0;
            _managedExcludedConfiguredLights = 0;
            _managedExcludedExternalPlayerLights = 0;
        }

        private void UpdateManagedScanTotals()
        {
            _managedScanCandidateCount = _managedCandidateLookup.Count;
            _managedScanShadowMapFaces = CurrentManagedFaceCount();
            _atlasScanPointLights = _managedScanPointLights;
            _atlasScanSpotLights = _managedScanSpotLights;
            _atlasScanOtherLights = 0;
            _atlasScanEstimatedMaps = _managedScanShadowMapFaces
                + _managedExternalPlayerShadowMapFaces;
            _atlasScanConstrainedLights = _managedActiveLights.Count;
            _atlasScanRestoredLights = _managedScanRestoredLights;
        }

        private int TrackedShadowResolutionCount()
        {
            return UseManagedShadowController()
                ? _managedLightStates.Count
                : _shadowResolutionStates.Count;
        }

        private void ReportManagedDiagnostics(string reason)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }
            Logger.LogInfo(
                "Safe shadow scan: cached="
                + _managedLightCache.Count.ToString(CultureInfo.InvariantCulture)
                + ", candidates="
                + _managedScanCandidateCount.ToString(CultureInfo.InvariantCulture)
                + ", viewRelevant="
                + _managedScanViewRelevantCount.ToString(CultureInfo.InvariantCulture)
                + ", active="
                + _managedActiveLights.Count.ToString(CultureInfo.InvariantCulture)
                + ", point="
                + _managedScanPointLights.ToString(CultureInfo.InvariantCulture)
                + ", spot="
                + _managedScanSpotLights.ToString(CultureInfo.InvariantCulture)
                + ", faces="
                + _managedScanShadowMapFaces.ToString(CultureInfo.InvariantCulture)
                + "/"
                + EffectiveMaximumShadowMapFaces().ToString(CultureInfo.InvariantCulture)
                + ", externalPlayerFaces="
                + _managedExternalPlayerShadowMapFaces.ToString(CultureInfo.InvariantCulture)
                + ", totalFaces="
                + (_managedScanShadowMapFaces
                    + _managedExternalPlayerShadowMapFaces)
                    .ToString(CultureInfo.InvariantCulture)
                + "/"
                + _maximumShadowMapFaces.Value.ToString(CultureInfo.InvariantCulture)
                + ", activated="
                + _managedScanActivatedLights.ToString(CultureInfo.InvariantCulture)
                + ", restored="
                + _managedScanRestoredLights.ToString(CultureInfo.InvariantCulture)
                + ", swaps="
                + _managedScanSwaps.ToString(CultureInfo.InvariantCulture)
                + ", handoffActive="
                + (_managedShadowHandoff != null)
                + ", initialFillPending="
                + _managedInitialFillPending
                + ", interiorProfile="
                + (_managedInteriorActive
                    && _interiorPerformanceEnabled.Value)
                + ", excludedHero="
                + _managedExcludedHeroLights.ToString(CultureInfo.InvariantCulture)
                + ", excludedWyrdSight="
                + _managedExcludedWyrdSightLights.ToString(CultureInfo.InvariantCulture)
                + ", excludedSummons="
                + _managedExcludedSummonLights.ToString(CultureInfo.InvariantCulture)
                + ", excludedInterface="
                + _managedExcludedInterfaceLights.ToString(CultureInfo.InvariantCulture)
                + ", excludedLockpicking="
                + _managedExcludedLockpickingLights.ToString(CultureInfo.InvariantCulture)
                + ", excludedPlacedBonfire="
                + _managedExcludedPlacedBonfireLights.ToString(CultureInfo.InvariantCulture)
                + ", excludedConfigured="
                + _managedExcludedConfiguredLights.ToString(CultureInfo.InvariantCulture)
                + ", excludedExternalPlayer="
                + _managedExcludedExternalPlayerLights.ToString(CultureInfo.InvariantCulture)
                + (string.IsNullOrEmpty(reason) ? "." : " (" + reason + ")."));
            ReportAtlasDiagnostics();
        }

        private struct ManagedCandidate
        {
            internal readonly Light Light;
            internal readonly int Id;
            internal readonly float Score;
            internal readonly bool ViewRelevant;
            internal readonly int FaceCost;

            internal ManagedCandidate(
                Light light,
                int id,
                float score,
                bool viewRelevant,
                int faceCost)
            {
                Light = light;
                Id = id;
                Score = score;
                ViewRelevant = viewRelevant;
                FaceCost = faceCost;
            }
        }

        private sealed class CachedManagedLight
        {
            internal Light Light;
            internal readonly int Id;
            internal float LastViewIntersectionTime;

            internal CachedManagedLight(Light light, int id)
            {
                Light = light;
                Id = id;
                LastViewIntersectionTime = float.NegativeInfinity;
            }
        }

        private sealed class ManagedLightState
        {
            internal readonly Light Light;
            internal readonly LightShadows OriginalShadows;
            internal readonly float OriginalShadowStrength;
            internal readonly ManagedHdrpState Hdrp;

            internal ManagedLightState(
                Light light,
                LightShadows originalShadows,
                float originalShadowStrength,
                ManagedHdrpState hdrp)
            {
                Light = light;
                OriginalShadows = originalShadows;
                OriginalShadowStrength = originalShadowStrength;
                Hdrp = hdrp;
            }
        }

        private sealed class ManagedShadowHandoff
        {
            internal readonly int OutgoingId;
            internal readonly ManagedCandidate Incoming;
            internal readonly float StartedAt;
            internal bool IncomingActivated;
            internal float CurrentStrengthMultiplier;

            internal ManagedShadowHandoff(
                int outgoingId,
                ManagedCandidate incoming,
                float startedAt)
            {
                OutgoingId = outgoingId;
                Incoming = incoming;
                StartedAt = startedAt;
                IncomingActivated = false;
                CurrentStrengthMultiplier = 1f;
            }
        }

        private struct ManagedHdrpState
        {
            internal static readonly ManagedHdrpState Empty =
                new ManagedHdrpState(
                    null,
                    null,
                    null,
                    null,
                    false,
                    0,
                    0,
                    false);

            internal readonly Component HdData;
            internal readonly bool? ShadowsEnabled;
            internal readonly float? ShadowDimmer;
            internal readonly float? VolumetricShadowDimmer;
            internal readonly bool HasResolution;
            internal readonly int ResolutionOverride;
            internal readonly int ResolutionLevel;
            internal readonly bool ResolutionUseOverride;

            internal ManagedHdrpState(
                Component hdData,
                bool? shadowsEnabled,
                float? shadowDimmer,
                float? volumetricShadowDimmer,
                bool hasResolution,
                int resolutionOverride,
                int resolutionLevel,
                bool resolutionUseOverride)
            {
                HdData = hdData;
                ShadowsEnabled = shadowsEnabled;
                ShadowDimmer = shadowDimmer;
                VolumetricShadowDimmer = volumetricShadowDimmer;
                HasResolution = hasResolution;
                ResolutionOverride = resolutionOverride;
                ResolutionLevel = resolutionLevel;
                ResolutionUseOverride = resolutionUseOverride;
            }
        }
    }
}
