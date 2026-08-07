using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EyesInTheDark
{
    internal enum WyrdnessPalette
    {
        Purple,
        NativeOrange
    }

    internal struct WyrdVisualSettings
    {
        public WyrdnessPalette Palette;
        public float WyrdnightBrightness;
        public float ThreatSmoothingHalfLifeSeconds;
        public float MinimumThreatScale;
        public float MaximumThreatScale;
        public string ThreatRedColor;
        public float MaximumRedBlend;
        public string MoonSurfaceColor;
        public float MoonSurfaceTintStrength;
        public float MoonSurfaceIntensity;
        public bool TintMoonCorona;
        public string MoonCoronaColor;
        public float MoonCoronaIntensity;
        public string MoonlightColor;
        public float MoonlightTintStrength;
        public bool TintNightSkyAmbient;
        public string NightSkyAmbientColor;
        public float NightSkyAmbientTintStrength;
        public bool TintBonfireProtectionBubble;
        public string ProtectionBubbleColor;
        public float ProtectionBubbleIntensity;
        public float ProtectionBubbleBorderIntensity;
        public float TransitionSeconds;
    }

    internal static class WyrdVisualMath
    {
        public static float ThreatScale(
            float threat,
            float minimum,
            float maximum)
        {
            return Mathf.Lerp(
                Mathf.Max(0f, minimum),
                Mathf.Max(0f, maximum),
                Mathf.Clamp01(threat / 100f));
        }

        public static float RedBlend(float threat, float maximumBlend)
        {
            return Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(threat / 100f))
                * Mathf.Clamp01(maximumBlend);
        }

        public static float SmoothThreat(
            float current,
            float target,
            float activeSeconds,
            float halfLifeSeconds)
        {
            current = Mathf.Clamp(current, 0f, 100f);
            target = Mathf.Clamp(target, 0f, 100f);
            if (float.IsNaN(halfLifeSeconds)
                || float.IsInfinity(halfLifeSeconds)
                || halfLifeSeconds <= 0f)
            {
                return target;
            }
            if (float.IsNaN(activeSeconds)
                || float.IsInfinity(activeSeconds)
                || activeSeconds <= 0f)
            {
                return current;
            }

            float blend = 1f - Mathf.Pow(
                0.5f,
                activeSeconds / halfLifeSeconds);
            float smoothed = Mathf.Lerp(current, target, blend);
            return Mathf.Abs(smoothed - target) <= 0.001f
                ? target
                : smoothed;
        }

        public static Color ShiftTowardRed(
            Color baseColor,
            Color redColor,
            float threat,
            float maximumBlend)
        {
            Color shifted = Color.Lerp(
                baseColor.linear,
                redColor.linear,
                RedBlend(threat, maximumBlend)).gamma;
            shifted.a = baseColor.a;
            return shifted;
        }

        public static Color NormalizeHdrHue(Color color)
        {
            float peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (peak <= 0.0001f)
            {
                return new Color(0f, 0f, 0f, color.a);
            }

            return new Color(
                color.r / peak,
                color.g / peak,
                color.b / peak,
                color.a);
        }

        public static Color ScaleRgb(Color color, float scale)
        {
            color.r *= scale;
            color.g *= scale;
            color.b *= scale;
            return color;
        }

        public static Color ScaleRgbLinear(Color color, float scale)
        {
            Color linear = color.linear;
            linear.r *= scale;
            linear.g *= scale;
            linear.b *= scale;
            Color scaled = linear.gamma;
            scaled.a = color.a;
            return scaled;
        }

        public static float AdvanceBlend(
            float current,
            bool targetActive,
            float activeSeconds,
            float transitionSeconds)
        {
            float target = targetActive ? 1f : 0f;
            if (float.IsNaN(transitionSeconds)
                || float.IsInfinity(transitionSeconds)
                || transitionSeconds <= 0f)
            {
                return target;
            }

            current = Mathf.Clamp01(current);
            if (float.IsNaN(activeSeconds)
                || float.IsInfinity(activeSeconds)
                || activeSeconds <= 0f)
            {
                return current;
            }

            return Mathf.MoveTowards(
                current,
                target,
                activeSeconds / transitionSeconds);
        }

        public static float PreDawnBlendLimit(
            float remainingNightRealSeconds,
            float transitionSeconds)
        {
            if (float.IsNaN(remainingNightRealSeconds)
                || float.IsPositiveInfinity(remainingNightRealSeconds)
                || float.IsNaN(transitionSeconds)
                || float.IsInfinity(transitionSeconds)
                || transitionSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(
                remainingNightRealSeconds / transitionSeconds);
        }

        public static float CenteredDuskBlend(
            bool isNight,
            float phaseRealSeconds,
            float transitionSeconds)
        {
            if (float.IsNaN(transitionSeconds)
                || float.IsInfinity(transitionSeconds)
                || transitionSeconds <= 0f)
            {
                return isNight ? 1f : 0f;
            }
            if (float.IsNaN(phaseRealSeconds)
                || float.IsPositiveInfinity(phaseRealSeconds))
            {
                return isNight ? 1f : 0f;
            }

            float seconds = Mathf.Max(0f, phaseRealSeconds);
            return isNight
                ? Mathf.Clamp01(0.5f + seconds / transitionSeconds)
                : Mathf.Clamp01(0.5f - seconds / transitionSeconds);
        }

        public static bool Approximately(Color left, Color right)
        {
            const float epsilon = 0.0005f;
            return Mathf.Abs(left.r - right.r) <= epsilon
                && Mathf.Abs(left.g - right.g) <= epsilon
                && Mathf.Abs(left.b - right.b) <= epsilon
                && Mathf.Abs(left.a - right.a) <= epsilon;
        }
    }

    internal sealed class WyrdVisualRuntime
    {
        private const string DayNightSystemTypeName =
            "Awaken.TG.Graphics.DayNightSystem.DayNightSystem";
        private const string WyrdnightSphereRepellerTypeName =
            "Awaken.TG.Graphics.DayNightSystem.WyrdnightSphereRepeller";
        private const string BonfireRepellerMaterialToken =
            "Repeller_Bonfire";
        private const string DefaultThreatRedColor = "#FF3028";
        private const string DefaultMoonSurfaceColor = "#3200FF";
        private const string DefaultMoonCoronaColor = "#8000FF";
        private const string DefaultMoonlightColor = "#7E47FF";
        private const string DefaultNightSkyAmbientColor = "#401C63";
        private const string DefaultProtectionBubbleColor = "#B050FF";
        private const float EnvironmentRefreshIntervalSeconds = 0.25f;

        private static readonly int SkyTintId =
            Shader.PropertyToID("_SkyTint");
        private static readonly int BubbleTintId =
            Shader.PropertyToID("_Tint");
        private static readonly int BubbleBorderColorId =
            Shader.PropertyToID("_BorderColor");

        private static WyrdVisualRuntime _instance;

        private readonly ManualLogSource _log;
        private readonly Action<string> _diagnosticFailure;
        private readonly Dictionary<int, DayNightState> _dayNightStates =
            new Dictionary<int, DayNightState>();
        private readonly Dictionary<int, BubbleState> _bubbleStates =
            new Dictionary<int, BubbleState>();
        private readonly List<int> _staleIds = new List<int>();
        private readonly HashSet<string> _invalidColors =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ParsedColor> _parsedColors =
            new Dictionary<string, ParsedColor>(StringComparer.Ordinal);

        private FieldInfo _moonLightField;
        private FieldInfo _exposureField;
        private FieldInfo _shadowCasterLightField;
        private FieldInfo _shadowCasterOwnerLightField;
        private PropertyInfo _lightColorProperty;
        private PropertyInfo _surfaceTintProperty;
        private PropertyInfo _lightDataProperty;
        private FieldInfo _moonFlareTintField;
        private FieldInfo _moonFlareMultiplierField;
        private PropertyInfo _skyboxInstanceProperty;
        private FieldInfo _repellerMaterialInstanceField;
        private bool _active;
        private bool _hasVisualTarget;
        private bool _targetActive;
        private bool _transitioning;
        private float _visualBlend;
        private float _threat;
        private float _loadThreatStart;
        private float _loadThreatTarget;
        private float _loadThreatElapsed;
        private float _loadThreatDuration;
        private bool _loadThreatTransitionActive;
        private WyrdVisualSettings _settings;
        private bool _failureLogged;
        private bool _hasReportedState;
        private bool _lastReportedActive;
        private WyrdnessPalette _lastReportedPalette;
        private float _lastReportedWyrdnightBrightness;
        private float _lastReportedThreatSmoothingSeconds;
        private float _lastReportedMinimumThreatScale;
        private float _lastReportedMaximumThreatScale;
        private string _lastReportedThreatRedColor;
        private float _lastReportedMaximumRedBlend;
        private float _lastReportedTransitionSeconds;
        private bool _environmentRefreshPending;
        private float _nextEnvironmentRefreshTime;
        private int _calculationVersion;

        public WyrdVisualRuntime(
            ManualLogSource log,
            Action<string> diagnosticFailure)
        {
            _log = log;
            _diagnosticFailure = diagnosticFailure;
            ResolveAccessors();
            _instance = this;
        }

        public bool TargetActive
        {
            get { return _hasVisualTarget && _targetActive; }
        }

        public void Prime(
            bool active,
            float threat,
            WyrdVisualSettings settings)
        {
            _threat = Mathf.Clamp(threat, 0f, 100f);
            _loadThreatTransitionActive = false;
            _settings = settings;
            unchecked
            {
                _calculationVersion++;
            }
            if (!_hasVisualTarget || _targetActive != active)
            {
                _hasVisualTarget = true;
                _targetActive = active;
                _visualBlend = active ? 1f : 0f;
                _transitioning = false;
                _active = active;
            }

            RefreshProtectionBubbles();
            foreach (DayNightState state in _dayNightStates.Values)
            {
                if (state.Component != null)
                {
                    ApplyDayNightSystem(state.Component);
                }
            }
        }

        public void BeginLoadThreatTransition(
            float targetThreat,
            float transitionSeconds)
        {
            _loadThreatTarget = Mathf.Clamp(targetThreat, 0f, 100f);
            if (_loadThreatTarget <= 0f
                || float.IsNaN(transitionSeconds)
                || float.IsInfinity(transitionSeconds)
                || transitionSeconds <= 0f)
            {
                _threat = _loadThreatTarget;
                _loadThreatTransitionActive = false;
                return;
            }

            _loadThreatStart = 0f;
            _loadThreatElapsed = 0f;
            _loadThreatDuration = transitionSeconds;
            _threat = 0f;
            _loadThreatTransitionActive = true;
        }

        public void Patch(Harmony harmony)
        {
            MethodInfo dayNightUpdate = AccessTools.Method(
                _moonLightField.DeclaringType,
                "Update");
            MethodInfo handleExposure = AccessTools.Method(
                _moonLightField.DeclaringType,
                "HandleExposure");
            MethodInfo repellerInitialize = AccessTools.Method(
                _repellerMaterialInstanceField.DeclaringType,
                "OnInitialize");
            if (dayNightUpdate == null
                || handleExposure == null
                || repellerInitialize == null)
            {
                throw new MissingMethodException(
                    "Wyrdnight visual update or repeller initialization target was not found");
            }

            harmony.Patch(
                dayNightUpdate,
                postfix: new HarmonyMethod(
                    typeof(WyrdVisualRuntime),
                    nameof(DayNightSystemUpdatePostfix)));
            HarmonyMethod exposurePostfix = new HarmonyMethod(
                typeof(WyrdVisualRuntime),
                nameof(DayNightSystemHandleExposurePostfix));
            exposurePostfix.after = new[]
            {
                "owrocc.DayNightLightTweaks"
            };
            harmony.Patch(
                handleExposure,
                postfix: exposurePostfix);
            harmony.Patch(
                repellerInitialize,
                postfix: new HarmonyMethod(
                    typeof(WyrdVisualRuntime),
                    nameof(WyrdnightSphereRepellerOnInitializePostfix)));
        }

        public void Update(
            bool active,
            float activeDelta,
            bool beginNaturalTransition,
            bool canContinueTransition,
            float phaseBlendLimit,
            float threat,
            WyrdVisualSettings settings)
        {
            ReportStateIfChanged(active, threat, settings);
            float targetThreat = Mathf.Clamp(threat, 0f, 100f);
            if (_loadThreatTransitionActive && active)
            {
                _loadThreatTarget = targetThreat;
                _loadThreatElapsed += Mathf.Max(0f, activeDelta);
                float progress = Mathf.Clamp01(
                    _loadThreatElapsed / _loadThreatDuration);
                _threat = Mathf.Lerp(
                    _loadThreatStart,
                    _loadThreatTarget,
                    progress);
                if (progress >= 1f)
                {
                    _threat = _loadThreatTarget;
                    _loadThreatTransitionActive = false;
                }
            }
            else
            {
                _threat = WyrdVisualMath.SmoothThreat(
                    _threat,
                    targetThreat,
                    activeDelta,
                    settings.ThreatSmoothingHalfLifeSeconds);
                _loadThreatTransitionActive = false;
            }
            _settings = settings;
            unchecked
            {
                _calculationVersion++;
            }

            if (!_hasVisualTarget)
            {
                _hasVisualTarget = true;
                _targetActive = active;
                _visualBlend = active ? 1f : 0f;
                _transitioning = false;
            }
            else if (_targetActive != active)
            {
                _targetActive = active;
                _transitioning = beginNaturalTransition;
                if (!_transitioning)
                {
                    _visualBlend = active ? 1f : 0f;
                }
            }

            if (_transitioning)
            {
                if (!canContinueTransition)
                {
                    _visualBlend = _targetActive ? 1f : 0f;
                    _transitioning = false;
                }
                else
                {
                    _visualBlend = WyrdVisualMath.AdvanceBlend(
                        _visualBlend,
                        _targetActive,
                        activeDelta,
                        settings.TransitionSeconds);
                    if (Mathf.Abs(
                        _visualBlend - (_targetActive ? 1f : 0f))
                        <= 0.00001f)
                    {
                        _visualBlend = _targetActive ? 1f : 0f;
                        _transitioning = false;
                    }
                }
            }

            if (_targetActive)
            {
                _visualBlend = Mathf.Min(
                    _visualBlend,
                    Mathf.Clamp01(phaseBlendLimit));
            }

            _active = _visualBlend > 0.00001f;

            RefreshProtectionBubbles();
            if (!_active)
            {
                RestoreDayNightSystems();
            }
            PruneStates();
        }

        private void ReportStateIfChanged(
            bool active,
            float threat,
            WyrdVisualSettings settings)
        {
            if (_hasReportedState
                && active == _lastReportedActive
                && settings.Palette == _lastReportedPalette
                && Mathf.Abs(
                    settings.WyrdnightBrightness
                        - _lastReportedWyrdnightBrightness) <= 0.0001f
                && Mathf.Abs(
                    settings.ThreatSmoothingHalfLifeSeconds
                        - _lastReportedThreatSmoothingSeconds) <= 0.0001f
                && Mathf.Abs(
                    settings.MinimumThreatScale
                        - _lastReportedMinimumThreatScale) <= 0.0001f
                && Mathf.Abs(
                    settings.MaximumThreatScale
                        - _lastReportedMaximumThreatScale) <= 0.0001f
                && string.Equals(
                    settings.ThreatRedColor ?? string.Empty,
                    _lastReportedThreatRedColor,
                    StringComparison.Ordinal)
                && Mathf.Abs(
                    settings.MaximumRedBlend
                        - _lastReportedMaximumRedBlend) <= 0.0001f
                && Mathf.Abs(
                    settings.TransitionSeconds
                        - _lastReportedTransitionSeconds) <= 0.0001f)
            {
                return;
            }

            if (_diagnosticFailure != null)
            {
                if (active)
                {
                    float scale = WyrdVisualMath.ThreatScale(
                        threat,
                        settings.MinimumThreatScale,
                        settings.MaximumThreatScale);
                    _diagnosticFailure(
                        "EITD - Wyrd visuals "
                        + settings.Palette
                        + "; threat="
                        + Mathf.RoundToInt(Mathf.Clamp(threat, 0f, 100f))
                            .ToString(CultureInfo.InvariantCulture)
                        + "; scale="
                        + scale.ToString(
                            "0.00",
                            CultureInfo.InvariantCulture)
                        + "; brightness="
                        + settings.WyrdnightBrightness.ToString(
                            "0.00",
                            CultureInfo.InvariantCulture)
                        + "; exposure="
                        + ExposureMultiplier(settings).ToString(
                            "0.00",
                            CultureInfo.InvariantCulture)
                        + "x/"
                        + ExposureCompensation(settings).ToString(
                            "+0.00;-0.00;0.00",
                            CultureInfo.InvariantCulture)
                        + " EV"
                        + "; smoothing="
                        + settings.ThreatSmoothingHalfLifeSeconds.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture)
                        + "s");
                }
                else if (_hasReportedState && _lastReportedActive)
                {
                    _diagnosticFailure(
                        "EITD - Wyrd visuals restored");
                }
            }

            _hasReportedState = true;
            _lastReportedActive = active;
            _lastReportedPalette = settings.Palette;
            _lastReportedWyrdnightBrightness =
                settings.WyrdnightBrightness;
            _lastReportedThreatSmoothingSeconds =
                settings.ThreatSmoothingHalfLifeSeconds;
            _lastReportedMinimumThreatScale =
                settings.MinimumThreatScale;
            _lastReportedMaximumThreatScale =
                settings.MaximumThreatScale;
            _lastReportedThreatRedColor =
                settings.ThreatRedColor ?? string.Empty;
            _lastReportedMaximumRedBlend = settings.MaximumRedBlend;
            _lastReportedTransitionSeconds = settings.TransitionSeconds;
        }

        public void Release()
        {
            _active = false;
            _hasVisualTarget = false;
            _targetActive = false;
            _transitioning = false;
            _visualBlend = 0f;
            _loadThreatTransitionActive = false;
            RestoreDayNightSystems();
            RestoreProtectionBubbles();
            _dayNightStates.Clear();
            _bubbleStates.Clear();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        private void ResolveAccessors()
        {
            Type dayNightSystemType = AccessTools.TypeByName(
                DayNightSystemTypeName);
            Type repellerType = AccessTools.TypeByName(
                WyrdnightSphereRepellerTypeName);
            if (dayNightSystemType == null || repellerType == null)
            {
                throw new TypeLoadException(
                    "The game Wyrdnight lighting types were not found");
            }

            _moonLightField = AccessTools.Field(
                dayNightSystemType,
                "_moonLight");
            _exposureField = AccessTools.Field(
                dayNightSystemType,
                "_exposure");
            _shadowCasterLightField = AccessTools.Field(
                dayNightSystemType,
                "_shadowCasterLight");
            _shadowCasterOwnerLightField = AccessTools.Field(
                dayNightSystemType,
                "_shadowCasterOwnerLight");
            _skyboxInstanceProperty = AccessTools.Property(
                dayNightSystemType,
                "SkyboxInstance");
            _repellerMaterialInstanceField = AccessTools.Field(
                repellerType,
                "_materialInstance");
            if (_moonLightField == null
                || _exposureField == null
                || _shadowCasterLightField == null
                || _shadowCasterOwnerLightField == null
                || _skyboxInstanceProperty == null
                || _repellerMaterialInstanceField == null)
            {
                throw new MissingMemberException(
                    "The game Wyrdnight lighting fields were incomplete");
            }

            Type lightWithOverrideType = _moonLightField.FieldType;
            _lightColorProperty = AccessTools.Property(
                lightWithOverrideType,
                "color");
            _surfaceTintProperty = AccessTools.Property(
                lightWithOverrideType,
                "surfaceTint");
            _lightDataProperty = AccessTools.Property(
                lightWithOverrideType,
                "LightData");
            if (_lightColorProperty == null
                || _surfaceTintProperty == null
                || _lightDataProperty == null)
            {
                throw new MissingMemberException(
                    "The game moon-light properties were incomplete");
            }

            Type lightDataType = _lightDataProperty.PropertyType;
            _moonFlareTintField = AccessTools.Field(
                lightDataType,
                "flareTint");
            _moonFlareMultiplierField = AccessTools.Field(
                lightDataType,
                "flareMultiplier");
            if (_moonFlareTintField == null
                || _moonFlareMultiplierField == null)
            {
                throw new MissingMemberException(
                    "The game moon-corona properties were incomplete");
            }
        }

        private void ApplyDayNightSystem(object system)
        {
            Component component = system as Component;
            if (component == null)
            {
                return;
            }
            if (!_active)
            {
                return;
            }

            object moonLight = _moonLightField.GetValue(system);
            object worldLight = _shadowCasterLightField.GetValue(system);
            object ownerLight = _shadowCasterOwnerLightField.GetValue(system);
            Component lightData = moonLight == null
                ? null
                : _lightDataProperty.GetValue(moonLight, null) as Component;
            Material skybox = _skyboxInstanceProperty.GetValue(system, null)
                as Material;
            if (moonLight == null || worldLight == null || lightData == null)
            {
                return;
            }

            int id = component.GetInstanceID();
            DayNightState state;
            if (!_dayNightStates.TryGetValue(id, out state))
            {
                state = new DayNightState(component);
                _dayNightStates.Add(id, state);
            }
            if (state.CalculationVersion == _calculationVersion
                && state.HasLastSurface
                && ReferenceEquals(state.LightData, lightData)
                && ReferenceEquals(state.Skybox, skybox))
            {
                ReapplyDayNightState(
                    state,
                    moonLight,
                    worldLight,
                    lightData);
                FlushEnvironmentRefresh(false);
                return;
            }

            Color currentSurface = (Color)_surfaceTintProperty.GetValue(
                moonLight,
                null);
            Color currentMoonlight = (Color)_lightColorProperty.GetValue(
                moonLight,
                null);
            Color currentWorldLight = (Color)_lightColorProperty.GetValue(
                worldLight,
                null);
            Color currentCorona = (Color)_moonFlareTintField.GetValue(lightData);
            float currentCoronaMultiplier =
                (float)_moonFlareMultiplierField.GetValue(lightData);
            Material currentSkybox = skybox;
            Color currentSky = currentSkybox == null
                || !currentSkybox.HasProperty(SkyTintId)
                ? Color.black
                : currentSkybox.GetColor(SkyTintId);
            state.ObserveOriginals(
                currentSurface,
                currentMoonlight,
                currentWorldLight,
                lightData,
                currentCorona,
                currentCoronaMultiplier,
                currentSkybox,
                currentSky);

            if (!_active)
            {
                RestoreDayNightState(state, moonLight, worldLight);
                return;
            }

            float scale = WyrdVisualMath.ThreatScale(
                _threat,
                _settings.MinimumThreatScale,
                _settings.MaximumThreatScale);
            Color red = ReadColor(
                _settings.ThreatRedColor,
                DefaultThreatRedColor,
                "ThreatRedColor");
            Color surfaceBase = PaletteColor(
                state.OriginalSurface,
                _settings.MoonSurfaceColor,
                DefaultMoonSurfaceColor,
                "MoonSurfaceColor");
            Color fullSurface = Color.Lerp(
                state.OriginalSurface,
                WyrdVisualMath.ShiftTowardRed(
                    surfaceBase,
                    red,
                    _threat,
                    _settings.MaximumRedBlend),
                Mathf.Clamp01(_settings.MoonSurfaceTintStrength));
            fullSurface = WyrdVisualMath.ScaleRgbLinear(
                fullSurface,
                Mathf.Clamp(_settings.MoonSurfaceIntensity, 0f, 8f)
                    * scale);
            Color desiredSurface = Color.Lerp(
                state.OriginalSurface,
                fullSurface,
                _visualBlend);
            SetPropertyColorIfChanged(
                _surfaceTintProperty,
                moonLight,
                desiredSurface);
            state.LastSurface = desiredSurface;
            state.HasLastSurface = true;

            if (ReferenceEquals(ownerLight, moonLight))
            {
                Color moonlightBase = PaletteColor(
                    state.OriginalMoonlight,
                    _settings.MoonlightColor,
                    DefaultMoonlightColor,
                    "MoonlightColor");
                Color shiftedMoonlight = WyrdVisualMath.ShiftTowardRed(
                    moonlightBase,
                    red,
                    _threat,
                    _settings.MaximumRedBlend);
                shiftedMoonlight = WyrdVisualMath.ScaleRgbLinear(
                    shiftedMoonlight,
                    scale);
                float lightStrength = Mathf.Clamp01(
                    _settings.MoonlightTintStrength)
                    * _visualBlend;
                Color desiredMoonlight = Color.Lerp(
                    state.OriginalMoonlight,
                    shiftedMoonlight,
                    lightStrength);
                Color desiredWorldLight = Color.Lerp(
                    state.OriginalWorldLight,
                    shiftedMoonlight,
                    lightStrength);
                SetPropertyColorIfChanged(
                    _lightColorProperty,
                    moonLight,
                    desiredMoonlight);
                SetPropertyColorIfChanged(
                    _lightColorProperty,
                    worldLight,
                    desiredWorldLight);
                state.LastMoonlight = desiredMoonlight;
                state.LastWorldLight = desiredWorldLight;
                state.HasLastLights = true;
            }

            ApplyCorona(state, lightData, red, scale);
            ApplySky(state, scale);
            state.CalculationVersion = _calculationVersion;
            FlushEnvironmentRefresh(false);
        }

        private void ReapplyDayNightState(
            DayNightState state,
            object moonLight,
            object worldLight,
            Component lightData)
        {
            if (state.HasLastSurface)
            {
                _surfaceTintProperty.SetValue(
                    moonLight,
                    state.LastSurface,
                    null);
            }
            if (state.HasLastLights)
            {
                _lightColorProperty.SetValue(
                    moonLight,
                    state.LastMoonlight,
                    null);
                _lightColorProperty.SetValue(
                    worldLight,
                    state.LastWorldLight,
                    null);
            }
            if (state.HasLastCorona && lightData != null)
            {
                _moonFlareTintField.SetValue(
                    lightData,
                    state.LastCorona);
                _moonFlareMultiplierField.SetValue(
                    lightData,
                    state.LastCoronaMultiplier);
            }
        }

        private void ApplyCorona(
            DayNightState state,
            Component lightData,
            Color red,
            float scale)
        {
            Color desiredTint = state.OriginalCorona;
            float desiredMultiplier = state.OriginalCoronaMultiplier;
            if (_settings.TintMoonCorona)
            {
                Color baseColor = PaletteColor(
                    state.OriginalCorona,
                    _settings.MoonCoronaColor,
                    DefaultMoonCoronaColor,
                    "MoonCoronaColor");
                Color shifted = WyrdVisualMath.ShiftTowardRed(
                    baseColor,
                    red,
                    _threat,
                    _settings.MaximumRedBlend);
                desiredTint = ApplyHdrHue(
                    state.OriginalCorona,
                    shifted,
                    1f);
                desiredMultiplier *= Mathf.Clamp(
                    _settings.MoonCoronaIntensity,
                    0f,
                    5f) * scale;
            }
            desiredTint = Color.Lerp(
                state.OriginalCorona,
                desiredTint,
                _visualBlend);
            desiredMultiplier = Mathf.Lerp(
                state.OriginalCoronaMultiplier,
                desiredMultiplier,
                _visualBlend);

            Color currentTint = (Color)_moonFlareTintField.GetValue(lightData);
            if (!WyrdVisualMath.Approximately(currentTint, desiredTint))
            {
                _moonFlareTintField.SetValue(lightData, desiredTint);
            }
            float currentMultiplier =
                (float)_moonFlareMultiplierField.GetValue(lightData);
            if (Mathf.Abs(currentMultiplier - desiredMultiplier) > 0.00001f)
            {
                _moonFlareMultiplierField.SetValue(
                    lightData,
                    desiredMultiplier);
            }
            state.LastCorona = desiredTint;
            state.LastCoronaMultiplier = desiredMultiplier;
            state.HasLastCorona = true;
        }

        private void ApplySky(
            DayNightState state,
            float scale)
        {
            if (state.Skybox == null)
            {
                return;
            }

            if (state.Skybox.HasProperty(SkyTintId))
            {
                Color desired = state.OriginalSky;
                if (_settings.TintNightSkyAmbient)
                {
                    Color baseColor = PaletteColor(
                        state.OriginalSky,
                        _settings.NightSkyAmbientColor,
                        DefaultNightSkyAmbientColor,
                        "NightSkyAmbientColor");
                    baseColor = WyrdVisualMath.ScaleRgbLinear(
                        baseColor,
                        scale);
                    desired = Color.Lerp(
                        state.OriginalSky,
                        baseColor,
                        Mathf.Clamp01(
                            _settings.NightSkyAmbientTintStrength)
                            * _visualBlend);
                }

                if (!WyrdVisualMath.Approximately(
                    state.Skybox.GetColor(SkyTintId),
                    desired))
                {
                    state.Skybox.SetColor(SkyTintId, desired);
                    _environmentRefreshPending = true;
                }
                state.LastSky = desired;
                state.HasLastSky = true;
            }

        }

        private Color PaletteColor(
            Color original,
            string configured,
            string fallback,
            string settingName)
        {
            return _settings.Palette == WyrdnessPalette.NativeOrange
                ? WyrdVisualMath.NormalizeHdrHue(original)
                : ReadColor(configured, fallback, settingName);
        }

        private void RegisterProtectionBubble(object repeller)
        {
            Material material = repeller == null
                ? null
                : _repellerMaterialInstanceField.GetValue(repeller)
                    as Material;
            if (material == null
                || material.name.IndexOf(
                    BonfireRepellerMaterialToken,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            bool hasTint = material.HasProperty(BubbleTintId);
            bool hasBorder = material.HasProperty(BubbleBorderColorId);
            if (!hasTint && !hasBorder)
            {
                return;
            }

            int id = material.GetInstanceID();
            if (!_bubbleStates.ContainsKey(id))
            {
                _bubbleStates.Add(
                    id,
                    new BubbleState(
                        material,
                        hasTint,
                        hasTint ? material.GetColor(BubbleTintId) : Color.white,
                        hasBorder,
                        hasBorder
                            ? material.GetColor(BubbleBorderColorId)
                            : Color.white));
            }
            ApplyProtectionBubble(_bubbleStates[id]);
        }

        private void RefreshProtectionBubbles()
        {
            foreach (BubbleState state in _bubbleStates.Values)
            {
                if (state.Material != null)
                {
                    ApplyProtectionBubble(state);
                }
            }
        }

        private void ApplyProtectionBubble(BubbleState state)
        {
            bool apply = _active
                && _settings.TintBonfireProtectionBubble;
            float scale = WyrdVisualMath.ThreatScale(
                _threat,
                _settings.MinimumThreatScale,
                _settings.MaximumThreatScale);
            Color red = ReadColor(
                _settings.ThreatRedColor,
                DefaultThreatRedColor,
                "ThreatRedColor");

            if (state.HasTint)
            {
                if (!apply)
                {
                    if (state.HasLastTint
                        && WyrdVisualMath.Approximately(
                            state.Material.GetColor(BubbleTintId),
                            state.LastTint))
                    {
                        state.Material.SetColor(
                            BubbleTintId,
                            state.OriginalTint);
                    }
                    state.HasLastTint = false;
                }
                else
                {
                    Color baseColor = PaletteColor(
                        state.OriginalTint,
                        _settings.ProtectionBubbleColor,
                        DefaultProtectionBubbleColor,
                        "ProtectionBubbleColor");
                    Color full = ApplyHdrHue(
                        state.OriginalTint,
                        WyrdVisualMath.ShiftTowardRed(
                            baseColor,
                            red,
                            _threat,
                            _settings.MaximumRedBlend),
                        Mathf.Clamp(
                            _settings.ProtectionBubbleIntensity,
                            0f,
                            3f) * scale);
                    Color desired = Color.Lerp(
                        state.OriginalTint,
                        full,
                        _visualBlend);
                    SetMaterialColorIfChanged(
                        state.Material,
                        BubbleTintId,
                        desired);
                    state.LastTint = desired;
                    state.HasLastTint = true;
                }
            }

            if (state.HasBorder)
            {
                if (!apply)
                {
                    if (state.HasLastBorder
                        && WyrdVisualMath.Approximately(
                            state.Material.GetColor(BubbleBorderColorId),
                            state.LastBorder))
                    {
                        state.Material.SetColor(
                            BubbleBorderColorId,
                            state.OriginalBorder);
                    }
                    state.HasLastBorder = false;
                }
                else
                {
                    Color baseColor = PaletteColor(
                        state.OriginalBorder,
                        _settings.ProtectionBubbleColor,
                        DefaultProtectionBubbleColor,
                        "ProtectionBubbleColor");
                    Color full = ApplyHdrHue(
                        state.OriginalBorder,
                        WyrdVisualMath.ShiftTowardRed(
                            baseColor,
                            red,
                            _threat,
                            _settings.MaximumRedBlend),
                        Mathf.Clamp(
                            _settings.ProtectionBubbleBorderIntensity,
                            0f,
                            3f) * scale);
                    Color desired = Color.Lerp(
                        state.OriginalBorder,
                        full,
                        _visualBlend);
                    SetMaterialColorIfChanged(
                        state.Material,
                        BubbleBorderColorId,
                        desired);
                    state.LastBorder = desired;
                    state.HasLastBorder = true;
                }
            }
        }

        private void RestoreDayNightSystems()
        {
            foreach (DayNightState state in _dayNightStates.Values)
            {
                if (state.Component == null)
                {
                    continue;
                }
                object system = state.Component;
                object moonLight = _moonLightField.GetValue(system);
                object worldLight = _shadowCasterLightField.GetValue(system);
                if (moonLight != null && worldLight != null)
                {
                    RestoreDayNightState(state, moonLight, worldLight);
                }
            }
            FlushEnvironmentRefresh(true);
        }

        private void RestoreDayNightState(
            DayNightState state,
            object moonLight,
            object worldLight)
        {
            if (state.HasLastSurface
                && WyrdVisualMath.Approximately(
                    (Color)_surfaceTintProperty.GetValue(moonLight, null),
                    state.LastSurface))
            {
                _surfaceTintProperty.SetValue(
                    moonLight,
                    state.OriginalSurface,
                    null);
            }
            if (state.HasLastLights)
            {
                if (WyrdVisualMath.Approximately(
                    (Color)_lightColorProperty.GetValue(moonLight, null),
                    state.LastMoonlight))
                {
                    _lightColorProperty.SetValue(
                        moonLight,
                        state.OriginalMoonlight,
                        null);
                }
                if (WyrdVisualMath.Approximately(
                    (Color)_lightColorProperty.GetValue(worldLight, null),
                    state.LastWorldLight))
                {
                    _lightColorProperty.SetValue(
                        worldLight,
                        state.OriginalWorldLight,
                        null);
                }
            }
            if (state.LightData != null && state.HasLastCorona)
            {
                Color current = (Color)_moonFlareTintField.GetValue(
                    state.LightData);
                float multiplier = (float)_moonFlareMultiplierField.GetValue(
                    state.LightData);
                if (WyrdVisualMath.Approximately(current, state.LastCorona))
                {
                    _moonFlareTintField.SetValue(
                        state.LightData,
                        state.OriginalCorona);
                }
                if (Mathf.Abs(multiplier - state.LastCoronaMultiplier)
                    <= 0.00001f)
                {
                    _moonFlareMultiplierField.SetValue(
                        state.LightData,
                        state.OriginalCoronaMultiplier);
                }
            }
            if (state.Skybox != null
                && state.HasLastSky
                && state.Skybox.HasProperty(SkyTintId)
                && WyrdVisualMath.Approximately(
                    state.Skybox.GetColor(SkyTintId),
                    state.LastSky))
            {
                state.Skybox.SetColor(SkyTintId, state.OriginalSky);
                _environmentRefreshPending = true;
            }
            state.ClearLastApplied();
        }

        private void RestoreProtectionBubbles()
        {
            foreach (BubbleState state in _bubbleStates.Values)
            {
                if (state.Material == null)
                {
                    continue;
                }
                if (state.HasTint
                    && state.HasLastTint
                    && WyrdVisualMath.Approximately(
                        state.Material.GetColor(BubbleTintId),
                        state.LastTint))
                {
                    state.Material.SetColor(BubbleTintId, state.OriginalTint);
                }
                if (state.HasBorder
                    && state.HasLastBorder
                    && WyrdVisualMath.Approximately(
                        state.Material.GetColor(BubbleBorderColorId),
                        state.LastBorder))
                {
                    state.Material.SetColor(
                        BubbleBorderColorId,
                        state.OriginalBorder);
                }
            }
        }

        private void PruneStates()
        {
            _staleIds.Clear();
            foreach (KeyValuePair<int, DayNightState> pair in _dayNightStates)
            {
                if (pair.Value.Component == null)
                {
                    _staleIds.Add(pair.Key);
                }
            }
            for (int index = 0; index < _staleIds.Count; index++)
            {
                _dayNightStates.Remove(_staleIds[index]);
            }

            _staleIds.Clear();
            foreach (KeyValuePair<int, BubbleState> pair in _bubbleStates)
            {
                if (pair.Value.Material == null)
                {
                    _staleIds.Add(pair.Key);
                }
            }
            for (int index = 0; index < _staleIds.Count; index++)
            {
                _bubbleStates.Remove(_staleIds[index]);
            }
        }

        private Color ReadColor(
            string configured,
            string fallback,
            string settingName)
        {
            string safeConfigured = configured ?? string.Empty;
            ParsedColor cached;
            if (_parsedColors.TryGetValue(settingName, out cached)
                && string.Equals(
                    cached.Configured,
                    safeConfigured,
                    StringComparison.Ordinal)
                && string.Equals(
                    cached.Fallback,
                    fallback,
                    StringComparison.Ordinal))
            {
                return cached.Value;
            }

            Color color;
            if (ColorUtility.TryParseHtmlString(safeConfigured, out color))
            {
                _invalidColors.Remove(settingName);
                _parsedColors[settingName] = new ParsedColor(
                    safeConfigured,
                    fallback,
                    color);
                return color;
            }

            ColorUtility.TryParseHtmlString(fallback, out color);
            if (_invalidColors.Add(settingName))
            {
                _log.LogWarning(
                    settingName + " is invalid; using " + fallback + ".");
            }
            _parsedColors[settingName] = new ParsedColor(
                safeConfigured,
                fallback,
                color);
            return color;
        }

        private void FlushEnvironmentRefresh(bool force)
        {
            if (!_environmentRefreshPending)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!force && now < _nextEnvironmentRefreshTime)
            {
                return;
            }

            DynamicGI.UpdateEnvironment();
            _environmentRefreshPending = false;
            _nextEnvironmentRefreshTime = now
                + EnvironmentRefreshIntervalSeconds;
        }

        private static Color ApplyHdrHue(
            Color original,
            Color hue,
            float intensityMultiplier)
        {
            float originalPeak = Mathf.Max(
                original.r,
                Mathf.Max(original.g, original.b));
            float huePeak = Mathf.Max(hue.r, Mathf.Max(hue.g, hue.b));
            if (huePeak <= 0.0001f || originalPeak <= 0.0001f)
            {
                return new Color(0f, 0f, 0f, original.a);
            }

            float scale = originalPeak * intensityMultiplier / huePeak;
            return new Color(
                hue.r * scale,
                hue.g * scale,
                hue.b * scale,
                original.a);
        }

        private static void SetPropertyColorIfChanged(
            PropertyInfo property,
            object owner,
            Color desired)
        {
            Color current = (Color)property.GetValue(owner, null);
            if (!WyrdVisualMath.Approximately(current, desired))
            {
                property.SetValue(owner, desired, null);
            }
        }

        private static void SetMaterialColorIfChanged(
            Material material,
            int propertyId,
            Color desired)
        {
            if (!WyrdVisualMath.Approximately(
                material.GetColor(propertyId),
                desired))
            {
                material.SetColor(propertyId, desired);
            }
        }

        private void Fail(string message, Exception exception)
        {
            if (_failureLogged)
            {
                return;
            }
            _failureLogged = true;
            _log.LogWarning(
                message + ": " + exception.GetBaseException().Message);
            if (_diagnosticFailure != null)
            {
                _diagnosticFailure("EITD - Wyrd visuals unavailable");
            }
        }

        private static void DayNightSystemUpdatePostfix(object __instance)
        {
            WyrdVisualRuntime runtime = _instance;
            if (runtime == null)
            {
                return;
            }
            try
            {
                runtime.ApplyDayNightSystem(__instance);
            }
            catch (Exception exception)
            {
                runtime.Fail("Wyrdnight lighting could not be applied", exception);
            }
        }

        private static void DayNightSystemHandleExposurePostfix(
            object __instance)
        {
            WyrdVisualRuntime runtime = _instance;
            if (runtime == null)
            {
                return;
            }
            try
            {
                runtime.ApplyWyrdnightBrightness(__instance);
            }
            catch (Exception exception)
            {
                runtime.Fail(
                    "Wyrdnight brightness could not be applied",
                    exception);
            }
        }

        private void ApplyWyrdnightBrightness(object system)
        {
            if (!_active
                || _visualBlend <= 0.00001f)
            {
                return;
            }

            Exposure exposure = _exposureField.GetValue(system) as Exposure;
            if (exposure == null)
            {
                return;
            }

            float multiplier = Mathf.Lerp(
                1f,
                Mathf.Clamp(
                    ExposureMultiplier(_settings),
                    0f,
                    3.5f),
                _visualBlend);
            float compensation = Mathf.Clamp(
                ExposureCompensation(_settings),
                0f,
                0.7f) * _visualBlend;
            exposure.compensation.value =
                exposure.compensation.value * multiplier + compensation;
            exposure.fixedExposure.value =
                exposure.fixedExposure.value * multiplier - compensation;
        }

        private static float ExposureMultiplier(WyrdVisualSettings settings)
        {
            float brightness = Mathf.Clamp(
                settings.WyrdnightBrightness,
                0f,
                2f);
            if (settings.Palette == WyrdnessPalette.Purple)
            {
                return brightness * 1.75f;
            }
            return brightness;
        }

        private static float ExposureCompensation(WyrdVisualSettings settings)
        {
            if (settings.Palette != WyrdnessPalette.Purple)
            {
                return 0f;
            }
            return Mathf.Clamp(
                settings.WyrdnightBrightness,
                0f,
                2f) * 0.35f;
        }

        private static void WyrdnightSphereRepellerOnInitializePostfix(
            object __instance)
        {
            WyrdVisualRuntime runtime = _instance;
            if (runtime == null)
            {
                return;
            }
            try
            {
                runtime.RegisterProtectionBubble(__instance);
            }
            catch (Exception exception)
            {
                runtime.Fail(
                    "A bonfire protection bubble could not be registered",
                    exception);
            }
        }

        private sealed class DayNightState
        {
            public DayNightState(Component component)
            {
                Component = component;
                CalculationVersion = int.MinValue;
            }

            public Component Component { get; private set; }
            public Color OriginalSurface { get; private set; }
            public Color OriginalMoonlight { get; private set; }
            public Color OriginalWorldLight { get; private set; }
            public Component LightData { get; private set; }
            public Color OriginalCorona { get; private set; }
            public float OriginalCoronaMultiplier { get; private set; }
            public Material Skybox { get; private set; }
            public Color OriginalSky { get; private set; }
            public Color LastSurface;
            public Color LastMoonlight;
            public Color LastWorldLight;
            public Color LastCorona;
            public float LastCoronaMultiplier;
            public Color LastSky;
            public bool HasLastSurface;
            public bool HasLastLights;
            public bool HasLastCorona;
            public bool HasLastSky;
            public int CalculationVersion;

            public void ObserveOriginals(
                Color surface,
                Color moonlight,
                Color worldLight,
                Component lightData,
                Color corona,
                float coronaMultiplier,
                Material skybox,
                Color sky)
            {
                if (!HasLastSurface
                    || !WyrdVisualMath.Approximately(surface, LastSurface))
                {
                    OriginalSurface = surface;
                }
                if (!HasLastLights
                    || !WyrdVisualMath.Approximately(moonlight, LastMoonlight))
                {
                    OriginalMoonlight = moonlight;
                }
                if (!HasLastLights
                    || !WyrdVisualMath.Approximately(worldLight, LastWorldLight))
                {
                    OriginalWorldLight = worldLight;
                }
                if (!ReferenceEquals(LightData, lightData))
                {
                    LightData = lightData;
                    HasLastCorona = false;
                }
                if (!HasLastCorona
                    || !WyrdVisualMath.Approximately(corona, LastCorona))
                {
                    OriginalCorona = corona;
                }
                if (!HasLastCorona
                    || Mathf.Abs(coronaMultiplier - LastCoronaMultiplier)
                        > 0.00001f)
                {
                    OriginalCoronaMultiplier = coronaMultiplier;
                }
                if (!ReferenceEquals(Skybox, skybox))
                {
                    Skybox = skybox;
                    HasLastSky = false;
                }
                if (skybox != null
                    && skybox.HasProperty(SkyTintId)
                    && (!HasLastSky
                        || !WyrdVisualMath.Approximately(sky, LastSky)))
                {
                    OriginalSky = sky;
                }
            }

            public void ClearLastApplied()
            {
                HasLastSurface = false;
                HasLastLights = false;
                HasLastCorona = false;
                HasLastSky = false;
                CalculationVersion = int.MinValue;
            }
        }

        private sealed class BubbleState
        {
            public BubbleState(
                Material material,
                bool hasTint,
                Color originalTint,
                bool hasBorder,
                Color originalBorder)
            {
                Material = material;
                HasTint = hasTint;
                OriginalTint = originalTint;
                HasBorder = hasBorder;
                OriginalBorder = originalBorder;
            }

            public Material Material { get; private set; }
            public bool HasTint { get; private set; }
            public Color OriginalTint { get; private set; }
            public bool HasBorder { get; private set; }
            public Color OriginalBorder { get; private set; }
            public Color LastTint;
            public Color LastBorder;
            public bool HasLastTint;
            public bool HasLastBorder;
        }

        private struct ParsedColor
        {
            public ParsedColor(
                string configured,
                string fallback,
                Color value)
            {
                Configured = configured;
                Fallback = fallback;
                Value = value;
            }

            public string Configured;
            public string Fallback;
            public Color Value;
        }
    }
}
