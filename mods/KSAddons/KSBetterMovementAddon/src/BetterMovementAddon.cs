using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.MovementSystems;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("KS Better Movement Addon")]
[assembly: AssemblyDescription("Terrain-aware slide audio companion for Better Movement")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("KS Better Movement Addon")]
[assembly: AssemblyVersion("0.1.7.0")]
[assembly: AssemblyFileVersion("0.1.7.0")]

namespace Keenan.TGFoA.BetterMovementAddon
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(ParentPluginGuid, "1.3.0")]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BetterMovementAddonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.better-movement-addon";
        public const string PluginName = "BetterMovement Addon";
        public const string PluginVersion = "0.1.7";
        public const string ParentPluginGuid = "BetterMovement";

        private const int ConfigSchemaVersion = 3;
        private const int ConfigRecoveryBaselineSchema = 1;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        private static readonly FieldInfo IsSlidingField =
            AccessTools.Field(typeof(HumanoidMovementBase), "_isSliding");
        private static readonly PropertyInfo ControllerProperty =
            AccessTools.Property(typeof(HeroMovementSystem), "Controller");

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _volume;
        private ConfigEntry<float> _minimumSpeedVolumeScale;
        private ConfigEntry<float> _pitchBySpeed;
        private ConfigEntry<float> _surfaceCheckInterval;
        private ConfigEntry<float> _crossfadeSeconds;
        private ConfigEntry<float> _minimumDistance;
        private ConfigEntry<float> _maximumDistance;
        private ConfigEntry<bool> _diagnostics;

        private bool _hasPendingEnabled;
        private bool _pendingEnabled;
        private bool _hasPendingVolume;
        private float _pendingVolume;
        private bool _hasPendingMinimumSpeedVolumeScale;
        private float _pendingMinimumSpeedVolumeScale;
        private bool _hasPendingPitchBySpeed;
        private float _pendingPitchBySpeed;
        private bool _hasPendingCrossfadeSeconds;
        private float _pendingCrossfadeSeconds;
        private bool _hasPendingMinimumDistance;
        private float _pendingMinimumDistance;
        private bool _hasPendingMaximumDistance;
        private float _pendingMaximumDistance;
        private bool _hasPendingSurfaceCheckInterval;
        private float _pendingSurfaceCheckInterval;
        private bool _hasPendingDiagnostics;
        private bool _pendingDiagnostics;

        private Harmony _harmony;
        private SlideSurfaceDetector _surfaceDetector;
        private SlideAudioRuntime _audioRuntime;
        private HumanoidMovementBase _activeMovement;
        private int _slideGeneration;
        private float _nextSurfaceCheckAt;
        private Vector3 _lastPosition;

        internal static BetterMovementAddonPlugin Instance { get; private set; }

        internal float Volume
        {
            get { return Mathf.Clamp01(_volume == null ? 0.40f : _volume.Value); }
        }

        internal float MinimumSpeedVolumeScale
        {
            get { return Mathf.Clamp01(_minimumSpeedVolumeScale == null ? 0.55f : _minimumSpeedVolumeScale.Value); }
        }

        internal float PitchBySpeed
        {
            get { return Mathf.Clamp(_pitchBySpeed == null ? 0.12f : _pitchBySpeed.Value, 0f, 0.35f); }
        }

        internal float SurfaceCheckInterval
        {
            get { return Mathf.Clamp(_surfaceCheckInterval == null ? 0.15f : _surfaceCheckInterval.Value, 0.05f, 1f); }
        }

        internal float CrossfadeSeconds
        {
            get { return Mathf.Clamp(_crossfadeSeconds == null ? 0.1f : _crossfadeSeconds.Value, 0f, 0.5f); }
        }

        internal float MinimumDistance
        {
            get { return Mathf.Clamp(_minimumDistance == null ? 1.5f : _minimumDistance.Value, 0.1f, 20f); }
        }

        internal float MaximumDistance
        {
            get
            {
                float configured = _maximumDistance == null ? 14f : _maximumDistance.Value;
                return Mathf.Max(MinimumDistance + 0.1f, configured);
            }
        }

        internal bool DiagnosticsEnabled
        {
            get { return _diagnostics != null && _diagnostics.Value; }
        }

        private void Awake()
        {
            Instance = this;

            try
            {
                ResetConfigIfSchemaChanged();
                BindConfig();
                Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                    Config,
                    Logger,
                    PluginName,
                    ConfigSchemaVersion,
                    ConfigRecoveryBaselineSchema,
                    ConfigRecoveryKeepCurrentDefaultRules,
                    ConfigRecoveryPermanentExclusions);

                if (IsSlidingField == null || ControllerProperty == null)
                {
                    throw new MissingMemberException(
                        "The current game build does not expose the expected HumanoidMovementBase slide state or HeroMovementSystem controller property.");
                }

                _surfaceDetector = new SlideSurfaceDetector(this, Logger);
                _audioRuntime = new SlideAudioRuntime(this, Logger, Info.Location);
                PatchSlideLifecycle();
                Config.Save();
                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded with terrain-aware slide audio. Custom WAVs can be replaced under audio\\slide.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(
                    PluginGuid,
                    PluginName,
                    exception);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_audioRuntime == null)
            {
                return;
            }

            _audioRuntime.TryInitialize();

            if (_enabled == null || !_enabled.Value)
            {
                if (_activeMovement != null)
                {
                    EndSlide(_activeMovement, "addon disabled");
                }

                _audioRuntime.Update(_lastPosition, Vector3.zero, 0f, Time.timeScale <= 0f);
                return;
            }

            VHeroController controller;
            if (_activeMovement == null
                || !IsMovementSliding(_activeMovement)
                || !TryGetController(_activeMovement, out controller)
                || controller == null)
            {
                if (_activeMovement != null)
                {
                    EndSlide(_activeMovement, "slide state ended outside the lifecycle hook");
                }

                _audioRuntime.Update(_lastPosition, Vector3.zero, 0f, Time.timeScale <= 0f);
                return;
            }

            _lastPosition = controller.Transform.position;
            Vector3 velocity = controller.HorizontalVelocity;
            float speed = controller.HorizontalSpeed;
            bool paused = Time.timeScale <= 0f;

            if (!paused && Time.unscaledTime >= _nextSurfaceCheckAt)
            {
                _nextSurfaceCheckAt = Time.unscaledTime + SurfaceCheckInterval;
                _surfaceDetector.RequestSurface(
                    _activeMovement,
                    _slideGeneration,
                    HandleSurfaceResolved);
            }

            _audioRuntime.Update(_lastPosition, velocity, speed, paused);
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (_surfaceDetector != null)
            {
                _surfaceDetector.Dispose();
                _surfaceDetector = null;
            }

            if (_audioRuntime != null)
            {
                _audioRuntime.Dispose();
                _audioRuntime = null;
            }

            _activeMovement = null;
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        internal void BeginSlide(HumanoidMovementBase movement)
        {
            if (movement == null || _enabled == null || !_enabled.Value)
            {
                return;
            }

            _slideGeneration++;
            _activeMovement = movement;
            _nextSurfaceCheckAt = 0f;

            VHeroController controller;
            if (TryGetController(movement, out controller) && controller != null)
            {
                _lastPosition = controller.Transform.position;
            }

            _surfaceDetector.BeginSlide(_slideGeneration);
            LogDiagnostic("Slide begun; terrain sampling generation " + _slideGeneration + ".");
        }

        internal void EndSlide(HumanoidMovementBase movement, string reason)
        {
            if (_activeMovement == null)
            {
                return;
            }

            if (movement != null && !ReferenceEquals(movement, _activeMovement))
            {
                return;
            }

            _slideGeneration++;
            _surfaceDetector.EndSlide(_slideGeneration);
            _audioRuntime.EndSlide(_lastPosition);
            _activeMovement = null;
            _nextSurfaceCheckAt = 0f;
            LogDiagnostic("Slide audio ended: " + reason + ".");
        }

        internal static bool TryGetController(
            HumanoidMovementBase movement,
            out VHeroController controller)
        {
            controller = null;
            if (movement == null || ControllerProperty == null)
            {
                return false;
            }

            try
            {
                controller = ControllerProperty.GetValue(movement, null) as VHeroController;
                return controller != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMovementSliding(HumanoidMovementBase movement)
        {
            if (movement == null || IsSlidingField == null)
            {
                return false;
            }

            try
            {
                object value = IsSlidingField.GetValue(movement);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private void HandleSurfaceResolved(int generation, string surface)
        {
            if (generation != _slideGeneration
                || _activeMovement == null
                || string.IsNullOrWhiteSpace(surface))
            {
                return;
            }

            _audioRuntime.SwitchSurface(surface, _lastPosition);
        }

        private void PatchSlideLifecycle()
        {
            MethodInfo slideBegun = AccessTools.Method(
                typeof(HumanoidMovementBase),
                "SlideBegun",
                Type.EmptyTypes);
            MethodInfo endSliding = AccessTools.Method(
                typeof(HumanoidMovementBase),
                "EndSliding",
                Type.EmptyTypes);
            if (slideBegun == null || endSliding == null)
            {
                throw new MissingMethodException(
                    "Could not find HumanoidMovementBase.SlideBegun or EndSliding.");
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                slideBegun,
                postfix: new HarmonyMethod(
                    typeof(SlideBegunPatch),
                    nameof(SlideBegunPatch.Postfix)));
            _harmony.Patch(
                endSliding,
                postfix: new HarmonyMethod(
                    typeof(EndSlidingPatch),
                    nameof(EndSlidingPatch.Postfix)));
        }

        private void BindConfig()
        {
            Config.Bind(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Master switch for terrain-aware Better Movement slide audio.",
                    "General", "Enabled", 0, 0));
            _volume = Config.Bind(
                "Audio",
                "Volume",
                0.40f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Overall slide-audio volume.",
                    "Audio", "Volume", 10, 0,
                    new AcceptableValueRange<float>(0f, 1f)));
            _minimumSpeedVolumeScale = Config.Bind(
                "Audio",
                "MinimumSpeedVolumeScale",
                0.55f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Volume scale near the minimum sliding speed. Faster slides rise toward full configured volume.",
                    "Audio", "Minimum Speed Volume Scale", 10, 10,
                    new AcceptableValueRange<float>(0f, 1f)));
            _pitchBySpeed = Config.Bind(
                "Audio",
                "PitchBySpeed",
                0.12f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Maximum pitch movement above or below normal as slide speed changes.",
                    "Audio", "Pitch By Speed", 10, 20,
                    new AcceptableValueRange<float>(0f, 0.35f)));
            _crossfadeSeconds = Config.Bind(
                "Audio",
                "SurfaceCrossfadeSeconds",
                0.1f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Fade time when a slide begins, ends, or crosses onto another terrain.",
                    "Audio", "Surface Crossfade Seconds", 10, 30,
                    new AcceptableValueRange<float>(0f, 0.5f)));
            _minimumDistance = Config.Bind(
                "Audio",
                "MinimumDistance",
                1.5f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Distance over which positional slide audio remains at full volume.",
                    "Audio", "Minimum Distance", 10, 40,
                    new AcceptableValueRange<float>(0.1f, 20f)));
            _maximumDistance = Config.Bind(
                "Audio",
                "MaximumDistance",
                14f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Distance at which positional slide audio finishes fading out.",
                    "Audio", "Maximum Distance", 10, 50,
                    new AcceptableValueRange<float>(1f, 100f)));
            _surfaceCheckInterval = Config.Bind(
                "Terrain Detection",
                "SurfaceCheckIntervalSeconds",
                0.15f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "How often a continuing slide checks the ground beneath the player.",
                    "Terrain Detection", "Surface Check Interval Seconds", 20, 0,
                    new AcceptableValueRange<float>(0.05f, 1f)));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Log surface transitions and audio fallback details.",
                    "Diagnostics", "Diagnostics",
                    Grailwright.Shared.ConfigUiDescription.DiagnosticsSectionOrder, 0));

            RestorePreservedSettings();
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

            CapturePreservedSettings(configPath, storedSchemaVersion);

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
                    PluginGuid,
                    PluginName,
                    storedSchemaVersion,
                    ConfigSchemaVersion);
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
                        "Could not restore the previous Better Movement Addon config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset Better Movement Addon config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedSettings(
            string configPath,
            int storedSchemaVersion)
        {
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            _hasPendingEnabled = profile.TryGetCustomizedValue(
                "General", "Enabled", out _pendingEnabled);
            _hasPendingVolume = profile.TryGetCustomizedValue(
                "Audio", "Volume", out _pendingVolume);
            _hasPendingMinimumSpeedVolumeScale = profile.TryGetCustomizedValue(
                "Audio",
                "MinimumSpeedVolumeScale",
                out _pendingMinimumSpeedVolumeScale);
            _hasPendingPitchBySpeed = profile.TryGetCustomizedValue(
                "Audio", "PitchBySpeed", out _pendingPitchBySpeed);
            _hasPendingCrossfadeSeconds = profile.TryGetCustomizedValue(
                "Audio",
                "SurfaceCrossfadeSeconds",
                out _pendingCrossfadeSeconds);
            _hasPendingMinimumDistance = profile.TryGetCustomizedValue(
                "Audio", "MinimumDistance", out _pendingMinimumDistance);
            _hasPendingMaximumDistance = profile.TryGetCustomizedValue(
                "Audio", "MaximumDistance", out _pendingMaximumDistance);
            _hasPendingSurfaceCheckInterval = profile.TryGetCustomizedValue(
                "Terrain Detection",
                "SurfaceCheckIntervalSeconds",
                out _pendingSurfaceCheckInterval);
            _hasPendingDiagnostics = profile.TryGetCustomizedValue(
                "Diagnostics", "Diagnostics", out _pendingDiagnostics);
        }

        private void RestorePreservedSettings()
        {
            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreserved(
                _hasPendingEnabled,
                _enabled,
                _pendingEnabled,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingVolume,
                _volume,
                _pendingVolume,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingMinimumSpeedVolumeScale,
                _minimumSpeedVolumeScale,
                _pendingMinimumSpeedVolumeScale,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingPitchBySpeed,
                _pitchBySpeed,
                _pendingPitchBySpeed,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingCrossfadeSeconds,
                _crossfadeSeconds,
                _pendingCrossfadeSeconds,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingMinimumDistance,
                _minimumDistance,
                _pendingMinimumDistance,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingMaximumDistance,
                _maximumDistance,
                _pendingMaximumDistance,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingSurfaceCheckInterval,
                _surfaceCheckInterval,
                _pendingSurfaceCheckInterval,
                ref restoredCount,
                ref clampedCount);
            RestorePreserved(
                _hasPendingDiagnostics,
                _diagnostics,
                _pendingDiagnostics,
                ref restoredCount,
                ref clampedCount);

            if (restoredCount > 0)
            {
                Logger.LogInfo(
                    "Preserved "
                    + restoredCount.ToString(CultureInfo.InvariantCulture)
                    + " customized slide-audio setting(s) across the config schema reset; clamped="
                    + clampedCount.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static void RestorePreserved<T>(
            bool hasPendingValue,
            ConfigEntry<T> entry,
            T pendingValue,
            ref int restoredCount,
            ref int clampedCount)
        {
            if (!hasPendingValue)
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    entry,
                    pendingValue,
                    out clamped))
            {
                return;
            }

            restoredCount++;
            if (clamped)
            {
                clampedCount++;
            }
        }

        private void LogDiagnostic(string message)
        {
            if (DiagnosticsEnabled)
            {
                Logger.LogInfo(message);
            }
        }

        private static class SlideBegunPatch
        {
            internal static void Postfix(HumanoidMovementBase __instance)
            {
                BetterMovementAddonPlugin instance = Instance;
                if (instance != null)
                {
                    instance.BeginSlide(__instance);
                }
            }
        }

        private static class EndSlidingPatch
        {
            internal static void Postfix(HumanoidMovementBase __instance)
            {
                BetterMovementAddonPlugin instance = Instance;
                if (instance != null)
                {
                    instance.EndSlide(__instance, "HumanoidMovementBase.EndSliding");
                }
            }
        }
    }
}
