using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EyesInTheDark
{
    internal enum BoundaryThreatReactivity
    {
        Disabled,
        Subtle
    }

    internal enum BoundaryRenderMode
    {
        Single,
        Layered
    }

    internal struct BoundarySettings
    {
        public BoundaryRenderMode RenderMode;
        public Color Color;
        public float HdrIntensity;
        public float NearRadius;
        public float NearIntensityMultiplier;
        public float NearThickness;
        public float MiddleRadius;
        public float MiddleIntensityMultiplier;
        public float MiddleThickness;
        public float OuterRadius;
        public float OuterIntensityMultiplier;
        public float OuterThickness;
        public BoundaryThreatReactivity ThreatReactivity;
        public float MinimumIntensityMultiplier;
        public float MaximumIntensityMultiplier;
        public float MaximumThicknessMultiplier;
        public bool PulseEnabled;
        public float PulseAmount;
        public float PulseMinimumSeconds;
        public float PulseMaximumSeconds;
    }

    internal sealed class BoundaryController
    {
        private const string ControllerTypeName =
            "Awaken.TG.Graphics.DayNightSystem.WyrdNightCustomPassController";
        private const string EdgeTypeName =
            "Awaken.TG.Graphics.DayNightSystem.HeroWyrdNightEdge";
        private static readonly int NativeIntensityId =
            Shader.PropertyToID("_Intensity");

        private readonly ManualLogSource _log;
        private readonly Action<string> _diagnosticFailure;
        private readonly System.Random _pulseRandom =
            new System.Random(unchecked(Environment.TickCount * 1543));

        private Type _controllerType;
        private Type _edgeType;
        private FieldInfo _volumeField;
        private FieldInfo _colorField;
        private FieldInfo _radiusField;
        private FieldInfo _thicknessField;
        private FieldInfo _sourceMaterialField;
        private FieldInfo _targetObjectField;
        private FieldInfo _maskIntensityField;
        private MethodInfo _getRuntimeMaterialMethod;
        private Component _controller;
        private CustomPassVolume _volume;
        private object _edge;
        private CustomPass _nativePass;
        private Material _nativeRuntimeMaterial;
        private LayeredBoundaryPass _layeredPass;
        private Color _originalColor;
        private float _originalRadius;
        private float _originalThickness;
        private bool _originalNativeEnabled;
        private bool _hasOriginals;
        private bool _reflectionResolved;
        private bool _failureLogged;
        private bool _attachedLogged;
        private bool _layeredLogged;
        private float _nextSearchTime;
        private BoundarySettings _settings;
        private float _threat;
        private bool _hasSettings;
        private PulseState _nearPulse;
        private PulseState _middlePulse;
        private PulseState _outerPulse;

        public BoundaryController(
            ManualLogSource log,
            Action<string> diagnosticFailure)
        {
            _log = log;
            _diagnosticFailure = diagnosticFailure;
        }

        public void Update(
            bool enabled,
            BoundarySettings settings,
            float threat)
        {
            if (!enabled)
            {
                Restore();
                return;
            }

            if (!HasLiveEdge())
            {
                if (_hasOriginals || _layeredPass != null)
                {
                    Restore();
                }
                if (Time.unscaledTime < _nextSearchTime)
                {
                    return;
                }

                _nextSearchTime = Time.unscaledTime + 2f;
                if (!TryAttach())
                {
                    return;
                }
            }

            _settings = settings;
            _threat = Mathf.Clamp(threat, 0f, 100f);
            _hasSettings = true;
            try
            {
                if (settings.RenderMode == BoundaryRenderMode.Layered
                    && EnsureLayeredPass())
                {
                    UpdateLayeredVisuals(0f);
                }
                else
                {
                    ReleaseLayeredPass();
                    ApplySingleBoundary();
                }
            }
            catch (Exception exception)
            {
                Fail(
                    "Wyrd boundary values could not be applied",
                    exception);
                Restore();
            }
        }

        public void Tick(float unscaledDelta, bool animate)
        {
            if (_layeredPass == null || !_hasSettings)
            {
                return;
            }

            float delta = animate
                && unscaledDelta > 0f
                && !float.IsNaN(unscaledDelta)
                && !float.IsInfinity(unscaledDelta)
                    ? unscaledDelta
                    : 0f;
            try
            {
                UpdateLayeredVisuals(delta);
            }
            catch (Exception exception)
            {
                Fail(
                    "Layered Wyrd boundary animation failed",
                    exception);
                Restore();
            }
        }

        public void Release()
        {
            Restore();
        }

        private bool TryAttach()
        {
            if (!ResolveReflection())
            {
                return false;
            }

            UnityEngine.Object[] controllers =
                Resources.FindObjectsOfTypeAll(_controllerType);
            for (int index = 0; index < controllers.Length; index++)
            {
                Component controller = controllers[index] as Component;
                if (controller == null
                    || !controller.gameObject.scene.IsValid()
                    || !controller.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CustomPassVolume volume =
                    _volumeField.GetValue(controller) as CustomPassVolume;
                if (volume == null)
                {
                    continue;
                }

                for (int passIndex = 0;
                    passIndex < volume.customPasses.Count;
                    passIndex++)
                {
                    object pass = volume.customPasses[passIndex];
                    if (pass == null || !_edgeType.IsInstanceOfType(pass))
                    {
                        continue;
                    }

                    _controller = controller;
                    _volume = volume;
                    _edge = pass;
                    _nativePass = pass as CustomPass;
                    _originalColor = (Color)_colorField.GetValue(pass);
                    _originalRadius = Convert.ToSingle(
                        _radiusField.GetValue(pass));
                    _originalThickness = Convert.ToSingle(
                        _thicknessField.GetValue(pass));
                    _originalNativeEnabled = _nativePass != null
                        && _nativePass.enabled;
                    _nativeRuntimeMaterial =
                        _getRuntimeMaterialMethod.Invoke(
                            pass,
                            null) as Material;
                    _hasOriginals = _nativePass != null;
                    if (!_attachedLogged)
                    {
                        _attachedLogged = true;
                        _log.LogInfo(
                            "Attached visual-only Wyrd boundary customization to the native HeroWyrdNightEdge pass.");
                    }
                    return _hasOriginals;
                }
            }

            return false;
        }

        private bool EnsureLayeredPass()
        {
            if (_layeredPass != null)
            {
                return true;
            }
            if (_volume == null || _nativePass == null || _edge == null)
            {
                return false;
            }

            Material sourceMaterial =
                _sourceMaterialField.GetValue(_edge) as Material;
            Transform targetObject =
                _targetObjectField.GetValue(_edge) as Transform;
            float maskIntensity = Convert.ToSingle(
                _maskIntensityField.GetValue(_edge));
            LayeredBoundaryPass layered = new LayeredBoundaryPass
            {
                name = "EITD Layered Wyrd Boundary",
                targetColorBuffer = _nativePass.targetColorBuffer,
                targetDepthBuffer = _nativePass.targetDepthBuffer,
                clearFlags = _nativePass.clearFlags
            };
            if (!layered.Initialize(
                sourceMaterial,
                targetObject,
                maskIntensity))
            {
                layered.ReleaseMaterials();
                return false;
            }

            int nativeIndex = _volume.customPasses.IndexOf(_nativePass);
            if (nativeIndex < 0)
            {
                layered.ReleaseMaterials();
                return false;
            }

            _volume.customPasses.Insert(nativeIndex + 1, layered);
            _layeredPass = layered;
            _nativePass.enabled = false;
            ResetPulses();
            if (!_layeredLogged)
            {
                _layeredLogged = true;
                _log.LogInfo(
                    "Activated the EITD-owned three-ring Wyrd boundary pass; the native pass remains available for restoration.");
            }
            return true;
        }

        private void ApplySingleBoundary()
        {
            if (_edge == null)
            {
                return;
            }

            float normalizedThreat = Mathf.Clamp01(_threat / 100f);
            float intensityMultiplier = 1f;
            float thicknessMultiplier = 1f;
            if (_settings.ThreatReactivity
                == BoundaryThreatReactivity.Subtle)
            {
                intensityMultiplier = Mathf.Lerp(
                    Mathf.Max(
                        0f,
                        _settings.MinimumIntensityMultiplier),
                    Mathf.Max(
                        0f,
                        _settings.MaximumIntensityMultiplier),
                    normalizedThreat);
                thicknessMultiplier = Mathf.Lerp(
                    1f,
                    Mathf.Max(
                        1f,
                        _settings.MaximumThicknessMultiplier),
                    normalizedThreat);
            }

            _colorField.SetValue(
                _edge,
                HdrColor(
                    _settings.Color,
                    Mathf.Max(0f, _settings.HdrIntensity)
                        * intensityMultiplier));
            _radiusField.SetValue(
                _edge,
                Mathf.Clamp(_settings.OuterRadius, 0f, 100f));
            _thicknessField.SetValue(
                _edge,
                Mathf.Clamp(
                    _settings.OuterThickness * thicknessMultiplier,
                    0f,
                    1f));
        }

        private void UpdateLayeredVisuals(float activeDelta)
        {
            if (_layeredPass == null)
            {
                return;
            }

            float normalizedThreat = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(_threat / 100f));
            float intensityMultiplier = 1f;
            float thicknessMultiplier = 1f;
            if (_settings.ThreatReactivity
                == BoundaryThreatReactivity.Subtle)
            {
                intensityMultiplier = Mathf.Lerp(
                    Mathf.Max(
                        0f,
                        _settings.MinimumIntensityMultiplier),
                    Mathf.Max(
                        0f,
                        _settings.MaximumIntensityMultiplier),
                    normalizedThreat);
                thicknessMultiplier = Mathf.Lerp(
                    1f,
                    Mathf.Max(
                        1f,
                        _settings.MaximumThicknessMultiplier),
                    normalizedThreat);
            }

            float pulseAmount = _settings.PulseEnabled
                ? Mathf.Clamp(_settings.PulseAmount, 0f, 1f)
                : 0f;
            float minimumSeconds = Mathf.Max(
                0.25f,
                _settings.PulseMinimumSeconds);
            float maximumSeconds = Mathf.Max(
                minimumSeconds,
                _settings.PulseMaximumSeconds);
            float nearPulse = AdvancePulse(
                ref _nearPulse,
                activeDelta,
                minimumSeconds * 0.8f,
                maximumSeconds * 0.8f);
            float middlePulse = AdvancePulse(
                ref _middlePulse,
                activeDelta,
                minimumSeconds,
                maximumSeconds);
            float outerPulse = AdvancePulse(
                ref _outerPulse,
                activeDelta,
                minimumSeconds * 1.25f,
                maximumSeconds * 1.25f);
            float baseHdr = Mathf.Max(0f, _settings.HdrIntensity)
                * intensityMultiplier;

            _layeredPass.SetLayer(
                0,
                _settings.NearRadius,
                _settings.NearThickness * thicknessMultiplier,
                HdrColor(
                    _settings.Color,
                    baseHdr
                        * Mathf.Max(
                            0f,
                            _settings.NearIntensityMultiplier)
                        * Mathf.Max(
                            0f,
                            1f + pulseAmount * nearPulse)));
            _layeredPass.SetLayer(
                1,
                _settings.MiddleRadius,
                _settings.MiddleThickness * thicknessMultiplier,
                HdrColor(
                    _settings.Color,
                    baseHdr
                        * Mathf.Max(
                            0f,
                            _settings.MiddleIntensityMultiplier)
                        * Mathf.Max(
                            0f,
                            1f + pulseAmount * 0.8f * middlePulse)));
            _layeredPass.SetLayer(
                2,
                _settings.OuterRadius,
                _settings.OuterThickness * thicknessMultiplier,
                HdrColor(
                    _settings.Color,
                    baseHdr
                        * Mathf.Max(
                            0f,
                            _settings.OuterIntensityMultiplier)
                        * Mathf.Max(
                            0f,
                            1f + pulseAmount * 0.6f * outerPulse)));

            float nativeIntensity = 1f;
            if (_nativeRuntimeMaterial != null
                && _nativeRuntimeMaterial.HasProperty(NativeIntensityId))
            {
                nativeIntensity = _nativeRuntimeMaterial.GetFloat(
                    NativeIntensityId);
            }
            _layeredPass.SetNativeIntensity(nativeIntensity);
        }

        private float AdvancePulse(
            ref PulseState state,
            float activeDelta,
            float minimumSeconds,
            float maximumSeconds)
        {
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.Start = 0f;
                state.Target = NextPulseTarget();
                state.Duration = NextPulseDuration(
                    minimumSeconds,
                    maximumSeconds);
            }

            state.Elapsed += Mathf.Max(0f, activeDelta);
            while (state.Elapsed >= state.Duration)
            {
                state.Elapsed -= state.Duration;
                state.Start = state.Target;
                state.Target = NextPulseTarget();
                state.Duration = NextPulseDuration(
                    minimumSeconds,
                    maximumSeconds);
            }

            float progress = state.Duration <= 0f
                ? 1f
                : Mathf.Clamp01(state.Elapsed / state.Duration);
            return Mathf.Lerp(
                state.Start,
                state.Target,
                Mathf.SmoothStep(0f, 1f, progress));
        }

        private float NextPulseTarget()
        {
            return (float)(_pulseRandom.NextDouble() * 2d - 1d);
        }

        private float NextPulseDuration(
            float minimumSeconds,
            float maximumSeconds)
        {
            return Mathf.Lerp(
                minimumSeconds,
                maximumSeconds,
                (float)_pulseRandom.NextDouble());
        }

        private static Color HdrColor(Color color, float intensity)
        {
            color.r *= intensity;
            color.g *= intensity;
            color.b *= intensity;
            return color;
        }

        private bool ResolveReflection()
        {
            if (_reflectionResolved)
            {
                return _controllerType != null && _edgeType != null;
            }

            _reflectionResolved = true;
            try
            {
                _controllerType = AccessTools.TypeByName(
                    ControllerTypeName);
                _edgeType = AccessTools.TypeByName(EdgeTypeName);
                _volumeField = _controllerType == null
                    ? null
                    : AccessTools.Field(
                        _controllerType,
                        "_customPassVolume");
                _colorField = _edgeType == null
                    ? null
                    : AccessTools.Field(_edgeType, "color");
                _radiusField = _edgeType == null
                    ? null
                    : AccessTools.Field(_edgeType, "radius");
                _thicknessField = _edgeType == null
                    ? null
                    : AccessTools.Field(_edgeType, "thickness");
                _sourceMaterialField = _edgeType == null
                    ? null
                    : AccessTools.Field(_edgeType, "sourceMaterial");
                _targetObjectField = _edgeType == null
                    ? null
                    : AccessTools.Field(_edgeType, "targetObject");
                _maskIntensityField = _edgeType == null
                    ? null
                    : AccessTools.Field(_edgeType, "maskIntensity");
                _getRuntimeMaterialMethod = _edgeType == null
                    ? null
                    : AccessTools.Method(
                        _edgeType,
                        "GetRuntimeMaterial");
                if (_controllerType == null
                    || _edgeType == null
                    || _volumeField == null
                    || _colorField == null
                    || _radiusField == null
                    || _thicknessField == null
                    || _sourceMaterialField == null
                    || _targetObjectField == null
                    || _maskIntensityField == null
                    || _getRuntimeMaterialMethod == null)
                {
                    throw new MissingMemberException(
                        "Native Wyrd boundary reflection surface is incomplete");
                }

                return true;
            }
            catch (Exception exception)
            {
                Fail(
                    "Native Wyrd boundary customization is unavailable",
                    exception);
                return false;
            }
        }

        private bool HasLiveEdge()
        {
            return _controller != null
                && _volume != null
                && _edge != null
                && _nativePass != null;
        }

        private void Restore()
        {
            ReleaseLayeredPass();
            if (HasLiveEdge() && _hasOriginals)
            {
                try
                {
                    _colorField.SetValue(_edge, _originalColor);
                    _radiusField.SetValue(_edge, _originalRadius);
                    _thicknessField.SetValue(
                        _edge,
                        _originalThickness);
                    _nativePass.enabled = _originalNativeEnabled;
                }
                catch (Exception exception)
                {
                    Fail(
                        "Native Wyrd boundary values could not be restored",
                        exception);
                }
            }
            ClearAttachment();
        }

        private void ReleaseLayeredPass()
        {
            if (_layeredPass != null)
            {
                _layeredPass.enabled = false;
                if (_volume != null)
                {
                    _volume.customPasses.Remove(_layeredPass);
                }
                _layeredPass.ReleaseMaterials();
                _layeredPass = null;
            }
            if (_nativePass != null)
            {
                _nativePass.enabled = _originalNativeEnabled;
            }
            ResetPulses();
        }

        private void ResetPulses()
        {
            _nearPulse = new PulseState();
            _middlePulse = new PulseState();
            _outerPulse = new PulseState();
        }

        private void ClearAttachment()
        {
            _controller = null;
            _volume = null;
            _edge = null;
            _nativePass = null;
            _nativeRuntimeMaterial = null;
            _hasOriginals = false;
            _hasSettings = false;
            _nextSearchTime = 0f;
        }

        private void Fail(string message, Exception exception)
        {
            if (_failureLogged)
            {
                return;
            }

            _failureLogged = true;
            string detail = message
                + ": "
                + exception.GetBaseException().Message;
            _log.LogWarning(detail);
            if (_diagnosticFailure != null)
            {
                _diagnosticFailure("EITD - Boundary unavailable");
            }
        }

        private struct PulseState
        {
            public float Start;
            public float Target;
            public float Elapsed;
            public float Duration;
            public bool Initialized;
        }
    }
}
