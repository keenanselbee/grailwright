using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EyesInTheDark
{
    internal enum BoundaryThreatReactivity
    {
        Disabled,
        Subtle
    }

    internal struct BoundarySettings
    {
        public Color Color;
        public float HdrIntensity;
        public float VisualRadius;
        public float Thickness;
        public BoundaryThreatReactivity ThreatReactivity;
        public float MinimumIntensityMultiplier;
        public float MaximumIntensityMultiplier;
        public float MaximumThicknessMultiplier;
    }

    internal sealed class BoundaryController
    {
        private const string ControllerTypeName =
            "Awaken.TG.Graphics.DayNightSystem.WyrdNightCustomPassController";
        private const string EdgeTypeName =
            "Awaken.TG.Graphics.DayNightSystem.HeroWyrdNightEdge";

        private readonly ManualLogSource _log;
        private readonly Action<string> _diagnosticFailure;
        private Type _controllerType;
        private Type _edgeType;
        private FieldInfo _volumeField;
        private FieldInfo _passesField;
        private PropertyInfo _passesProperty;
        private FieldInfo _colorField;
        private FieldInfo _radiusField;
        private FieldInfo _thicknessField;
        private Component _controller;
        private object _edge;
        private Color _originalColor;
        private float _originalRadius;
        private float _originalThickness;
        private bool _hasOriginals;
        private bool _reflectionResolved;
        private bool _failureLogged;
        private bool _attachedLogged;
        private float _nextSearchTime;

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

            float normalizedThreat = Mathf.Clamp01(threat / 100f);
            float intensityMultiplier = 1f;
            float thicknessMultiplier = 1f;
            if (settings.ThreatReactivity
                == BoundaryThreatReactivity.Subtle)
            {
                intensityMultiplier = Mathf.Lerp(
                    Mathf.Max(0f, settings.MinimumIntensityMultiplier),
                    Mathf.Max(0f, settings.MaximumIntensityMultiplier),
                    normalizedThreat);
                thicknessMultiplier = Mathf.Lerp(
                    1f,
                    Mathf.Max(1f, settings.MaximumThicknessMultiplier),
                    normalizedThreat);
            }

            try
            {
                Color color = settings.Color;
                float hdr = Mathf.Max(0f, settings.HdrIntensity)
                    * intensityMultiplier;
                color.r *= hdr;
                color.g *= hdr;
                color.b *= hdr;
                _colorField.SetValue(_edge, color);
                _radiusField.SetValue(
                    _edge,
                    Mathf.Clamp(settings.VisualRadius, 0f, 100f));
                _thicknessField.SetValue(
                    _edge,
                    Mathf.Clamp(
                        settings.Thickness * thicknessMultiplier,
                        0f,
                        1f));
            }
            catch (Exception exception)
            {
                Fail(
                    "Wyrd boundary values could not be applied",
                    exception);
                ClearAttachment();
            }
        }

        public void Release()
        {
            Restore();
            ClearAttachment();
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

                object volume = _volumeField.GetValue(controller);
                if (volume == null)
                {
                    continue;
                }

                if (_passesField == null && _passesProperty == null)
                {
                    Type volumeType = volume.GetType();
                    _passesField = AccessTools.Field(
                        volumeType,
                        "customPasses");
                    _passesProperty = _passesField == null
                        ? volumeType.GetProperty(
                            "customPasses",
                            BindingFlags.Public
                                | BindingFlags.NonPublic
                                | BindingFlags.Instance)
                        : null;
                }

                object passesValue = _passesField != null
                    ? _passesField.GetValue(volume)
                    : _passesProperty == null
                        ? null
                        : _passesProperty.GetValue(volume, null);
                IEnumerable passes = passesValue as IEnumerable;
                if (passes == null)
                {
                    continue;
                }

                foreach (object pass in passes)
                {
                    if (pass == null || !_edgeType.IsInstanceOfType(pass))
                    {
                        continue;
                    }

                    _controller = controller;
                    _edge = pass;
                    _originalColor = (Color)_colorField.GetValue(pass);
                    _originalRadius = Convert.ToSingle(
                        _radiusField.GetValue(pass));
                    _originalThickness = Convert.ToSingle(
                        _thicknessField.GetValue(pass));
                    _hasOriginals = true;
                    if (!_attachedLogged)
                    {
                        _attachedLogged = true;
                        _log.LogInfo(
                            "Attached visual-only Wyrd boundary customization to the native HeroWyrdNightEdge pass.");
                    }
                    return true;
                }
            }

            return false;
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
                if (_controllerType == null
                    || _edgeType == null
                    || _volumeField == null
                    || _colorField == null
                    || _radiusField == null
                    || _thicknessField == null)
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
            return _controller != null && _edge != null;
        }

        private void Restore()
        {
            if (!HasLiveEdge() || !_hasOriginals)
            {
                ClearAttachment();
                return;
            }

            try
            {
                _colorField.SetValue(_edge, _originalColor);
                _radiusField.SetValue(_edge, _originalRadius);
                _thicknessField.SetValue(_edge, _originalThickness);
            }
            catch (Exception exception)
            {
                Fail(
                    "Native Wyrd boundary values could not be restored",
                    exception);
            }
            finally
            {
                ClearAttachment();
            }
        }

        private void ClearAttachment()
        {
            _controller = null;
            _edge = null;
            _hasOriginals = false;
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
    }
}
