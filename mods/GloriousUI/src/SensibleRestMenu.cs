using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Awaken.TG.Main.Heroes.CharacterSheet.QuickUseWheels;
using Awaken.TG.Main.Heroes.Resting;
using Awaken.TG.Main.Timing;
using Awaken.TG.MVC;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace GloriousUI
{
    internal enum RestTimeDisplayFormat
    {
        TwelveHour,
        TwentyFourHour
    }

    internal sealed class SensibleRestMenuController
    {
        private static readonly FieldInfo QuickWeatherTimeTextField =
            AccessTools.Field(
                typeof(VCQuickWeatherTime),
                "gameWeatherTimeText");
        private static SensibleRestMenuController Instance;

        private readonly ManualLogSource _log;
        private readonly ConfigEntry<bool> _gloriousUiEnabled;
        private readonly ConfigEntry<bool> _enabled;
        private readonly ConfigEntry<RestTimeDisplayFormat> _timeFormat;
        private readonly ConfigEntry<bool> _formatQuickMenuTime;
        private bool _restClockFailureLogged;
        private bool _quickWeatherTimeFailureLogged;

        internal SensibleRestMenuController(
            ManualLogSource log,
            ConfigEntry<bool> gloriousUiEnabled,
            ConfigEntry<bool> enabled,
            ConfigEntry<RestTimeDisplayFormat> timeFormat,
            ConfigEntry<bool> formatQuickMenuTime)
        {
            _log = log;
            _gloriousUiEnabled = gloriousUiEnabled;
            _enabled = enabled;
            _timeFormat = timeFormat;
            _formatQuickMenuTime = formatQuickMenuTime;
            _gloriousUiEnabled.SettingChanged += OnPresentationSettingChanged;
            _enabled.SettingChanged += OnPresentationSettingChanged;
            _timeFormat.SettingChanged += OnPresentationSettingChanged;
        }

        internal void Patch(Harmony harmony)
        {
            Instance = this;
            PatchRestClock(harmony);
            PatchQuickWeatherTime(harmony);
        }

        internal void Release()
        {
            _gloriousUiEnabled.SettingChanged -= OnPresentationSettingChanged;
            _enabled.SettingChanged -= OnPresentationSettingChanged;
            _timeFormat.SettingChanged -= OnPresentationSettingChanged;
            RestClockOverlay.DetachAll();
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private bool IsEnabled()
        {
            return _gloriousUiEnabled.Value
                && _enabled.Value;
        }

        private void OnPresentationSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            RestClockOverlay.DetachAll();
        }

        private void PatchRestClock(Harmony harmony)
        {
            try
            {
                MethodInfo initialize = AccessTools.Method(
                    typeof(VRestPopupUI),
                    "OnInitialize",
                    Type.EmptyTypes);
                MethodInfo refresh = AccessTools.Method(
                    typeof(VRestPopupUI),
                    "Refresh",
                    Type.EmptyTypes);
                MethodInfo setHourFromAngle = AccessTools.Method(
                    typeof(VRestPopupUI),
                    "SetHourChangeBasedOnAngle",
                    new[] { typeof(float) });
                MethodInfo initializePostfix = AccessTools.Method(
                    typeof(RestClockPatch),
                    nameof(RestClockPatch.AfterInitialize));
                MethodInfo refreshPostfix = AccessTools.Method(
                    typeof(RestClockPatch),
                    nameof(RestClockPatch.AfterRefresh));
                MethodInfo anglePrefix = AccessTools.Method(
                    typeof(RestClockPatch),
                    nameof(RestClockPatch.BeforeSetHourChangeBasedOnAngle));
                if (initialize == null
                    || refresh == null
                    || setHourFromAngle == null
                    || initializePostfix == null
                    || refreshPostfix == null
                    || anglePrefix == null)
                {
                    throw new MissingMethodException(
                        "the native rest-clock targets were not found");
                }

                harmony.Patch(
                    initialize,
                    postfix: new HarmonyMethod(initializePostfix));
                harmony.Patch(
                    refresh,
                    postfix: new HarmonyMethod(refreshPostfix));
                harmony.Patch(
                    setHourFromAngle,
                    prefix: new HarmonyMethod(anglePrefix));
            }
            catch (Exception exception)
            {
                _log.LogWarning(
                    "Could not attach Sensible Rest Menu's clock presentation; the native rest clock remains available: "
                    + exception.GetBaseException().Message);
            }
        }

        private void PatchQuickWeatherTime(Harmony harmony)
        {
            try
            {
                MethodInfo attach = AccessTools.Method(
                    typeof(VCQuickWeatherTime),
                    "OnAttach",
                    Type.EmptyTypes);
                MethodInfo postfix = AccessTools.Method(
                    typeof(QuickWeatherTimePatch),
                    nameof(QuickWeatherTimePatch.AfterAttach));
                if (attach == null
                    || postfix == null
                    || QuickWeatherTimeTextField == null)
                {
                    throw new MissingMethodException(
                        "the native quick-menu time target was not found");
                }

                harmony.Patch(
                    attach,
                    postfix: new HarmonyMethod(postfix));
            }
            catch (Exception exception)
            {
                _log.LogWarning(
                    "Could not attach Sensible Rest Menu's quick-menu time format; the native time remains available: "
                    + exception.GetBaseException().Message);
            }
        }

        private void ApplyRestClock(VRestPopupUI view)
        {
            if (!IsEnabled())
            {
                RestClockOverlay.Detach(view);
                return;
            }

            try
            {
                RestClockOverlay.Attach(view, _timeFormat.Value);
                RestClockOverlay.RefreshAfterNative(
                    view,
                    _timeFormat.Value);
                _restClockFailureLogged = false;
            }
            catch (Exception exception)
            {
                if (!_restClockFailureLogged)
                {
                    _restClockFailureLogged = true;
                    _log.LogWarning(
                        "Sensible Rest Menu could not update the rest clock; the native clock remains usable: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private bool UsesNoonAtTop(VRestPopupUI view)
        {
            return IsEnabled() && RestClockOverlay.UsesNoonAtTop(view);
        }

        private void FormatQuickWeatherTime(VCQuickWeatherTime view)
        {
            if (view == null)
            {
                return;
            }

            if (!IsEnabled()
                || !_formatQuickMenuTime.Value
                || _timeFormat.Value == RestTimeDisplayFormat.TwentyFourHour)
            {
                return;
            }

            try
            {
                GameRealTime clock = World.Any<GameRealTime>();
                TextMeshProUGUI timeText = QuickWeatherTimeTextField.GetValue(
                    view) as TextMeshProUGUI;
                if (clock == null || timeText == null)
                {
                    return;
                }

                int hour = clock.WeatherTime.Hour;
                int minute = clock.WeatherTime.Minutes;
                int twelveHour = hour % 12;
                if (twelveHour == 0)
                {
                    twelveHour = 12;
                }
                timeText.SetText(
                    twelveHour.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + minute.ToString("00", CultureInfo.InvariantCulture)
                    + (hour < 12 ? " AM" : " PM"));
                _quickWeatherTimeFailureLogged = false;
            }
            catch (Exception exception)
            {
                if (!_quickWeatherTimeFailureLogged)
                {
                    _quickWeatherTimeFailureLogged = true;
                    _log.LogWarning(
                        "Sensible Rest Menu could not format the quick-menu time; the native time remains usable: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private static class RestClockPatch
        {
            internal static void AfterInitialize(VRestPopupUI __instance)
            {
                if (Instance != null)
                {
                    Instance.ApplyRestClock(__instance);
                }
            }

            internal static void AfterRefresh(VRestPopupUI __instance)
            {
                if (Instance != null)
                {
                    Instance.ApplyRestClock(__instance);
                }
            }

            internal static void BeforeSetHourChangeBasedOnAngle(
                VRestPopupUI __instance,
                ref float angle)
            {
                if (Instance != null && Instance.UsesNoonAtTop(__instance))
                {
                    angle += 180f;
                }
            }
        }

        private static class QuickWeatherTimePatch
        {
            internal static void AfterAttach(VCQuickWeatherTime __instance)
            {
                if (Instance != null)
                {
                    Instance.FormatQuickWeatherTime(__instance);
                }
            }
        }
    }

    internal sealed class RestClockOverlay : MonoBehaviour
    {
        private const float IconRadius = 52f;
        private const float HourLabelRadius = 86f;
        private static readonly FieldInfo CurrentTimeValueTextField =
            AccessTools.Field(typeof(VRestPopupUI), "currentTimeValueText");
        private static readonly FieldInfo RestingUntilValueTextField =
            AccessTools.Field(typeof(VRestPopupUI), "restingTimeUntilValueText");
        private static readonly Dictionary<int, RestClockOverlay> Overlays =
            new Dictionary<int, RestClockOverlay>();

        private VRestPopupUI _view;
        private RectTransform _arm;
        private RectTransform _fill;
        private RectTransform _halfCircle;
        private RectTransform _moon;
        private RectTransform _sun;
        private Vector2 _moonPosition;
        private Vector2 _sunPosition;
        private Quaternion _nativeArmRotation;
        private Quaternion _nativeFillRotation;
        private Quaternion _lastAppliedArmRotation;
        private Quaternion _lastAppliedFillRotation;
        private bool _hasLastAppliedArmRotation;
        private bool _hasLastAppliedFillRotation;
        private bool _halfCircleWasActive;
        private string _nativeCurrentTimeText;
        private string _nativeRestingUntilText;
        private int _viewId;

        internal static void Attach(
            VRestPopupUI view,
            RestTimeDisplayFormat labelFormat)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            Transform clock = view.transform.Find("Content/Clock");
            RectTransform arm = FindRect(clock, "Arm");
            RectTransform fill = FindRect(clock, "FillParent/Fill");
            RectTransform halfCircle = FindRect(clock, "HalfCircle");
            RectTransform moon = FindRect(clock, "Moon");
            RectTransform sun = FindRect(clock, "Sun");
            TextMeshProUGUI textTemplate =
                view.GetComponentInChildren<TextMeshProUGUI>(true);
            if (clock == null
                || arm == null
                || fill == null
                || halfCircle == null
                || moon == null
                || sun == null
                || textTemplate == null)
            {
                throw new InvalidOperationException(
                    "the native rest-clock hierarchy is incomplete");
            }

            RestClockOverlay existing =
                clock.GetComponentInChildren<RestClockOverlay>(true);
            if (existing != null)
            {
                return;
            }

            GameObject rootObject = new GameObject(
                "GloriousUI_RestClock",
                typeof(RectTransform));
            rootObject.layer = clock.gameObject.layer;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(clock, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = arm.anchoredPosition;
            root.sizeDelta = fill.sizeDelta;
            root.localScale = Vector3.one;
            root.SetSiblingIndex(fill.parent.GetSiblingIndex());

            try
            {
                RestClockOverlay overlay =
                    rootObject.AddComponent<RestClockOverlay>();
                overlay._view = view;
                overlay._arm = arm;
                overlay._fill = fill;
                overlay._halfCircle = halfCircle;
                overlay._moon = moon;
                overlay._sun = sun;
                overlay._moonPosition = moon.anchoredPosition;
                overlay._sunPosition = sun.anchoredPosition;
                overlay._nativeArmRotation = arm.localRotation;
                overlay._nativeFillRotation = fill.localRotation;
                overlay._halfCircleWasActive = halfCircle.gameObject.activeSelf;
                overlay._viewId = view.GetInstanceID();
                Overlays[overlay._viewId] = overlay;
                overlay.Build(root, textTemplate, labelFormat);

                MovePhaseIcon(moon, root.anchoredPosition, 0f, IconRadius);
                MovePhaseIcon(sun, root.anchoredPosition, 12f, IconRadius);
                halfCircle.gameObject.SetActive(false);
            }
            catch
            {
                Destroy(rootObject);
                throw;
            }
        }

        private void Build(
            RectTransform root,
            TextMeshProUGUI textTemplate,
            RestTimeDisplayFormat labelFormat)
        {
            float diameter = Mathf.Min(root.sizeDelta.x, root.sizeDelta.y);
            if (diameter < 100f)
            {
                diameter = 260f;
                root.sizeDelta = new Vector2(diameter, diameter);
            }

            Color hourColor = textTemplate.color;
            hourColor.a = 0.78f;
            if (labelFormat == RestTimeDisplayFormat.TwentyFourHour)
            {
                CreateHourLabel(root, textTemplate, "00", 0f, hourColor);
                CreateHourLabel(root, textTemplate, "06", 6f, hourColor);
                CreateHourLabel(root, textTemplate, "12", 12f, hourColor);
                CreateHourLabel(root, textTemplate, "18", 18f, hourColor);
                return;
            }

            CreateHourLabel(root, textTemplate, "12 AM", 0f, hourColor);
            CreateHourLabel(root, textTemplate, "6 AM", 6f, hourColor);
            CreateHourLabel(root, textTemplate, "12 PM", 12f, hourColor);
            CreateHourLabel(root, textTemplate, "6 PM", 18f, hourColor);
        }

        private static void CreateHourLabel(
            RectTransform parent,
            TextMeshProUGUI template,
            string text,
            float hour,
            Color color)
        {
            GameObject textObject = new GameObject(
                "Hour" + text.Replace(" ", string.Empty),
                typeof(RectTransform),
                typeof(CanvasRenderer));
            textObject.layer = parent.gameObject.layer;
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = PositionForHour(hour, HourLabelRadius);
            rect.sizeDelta = new Vector2(64f, 24f);
            rect.localScale = Vector3.one;

            TextMeshProUGUI label =
                textObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = color;
            label.text = text;
        }

        private static void MovePhaseIcon(
            RectTransform icon,
            Vector2 center,
            float hour,
            float radius)
        {
            icon.anchoredPosition = center + PositionForHour(hour, radius);
        }

        private static Vector2 PositionForHour(float hour, float radius)
        {
            float radians = (hour + 12f) / 24f * Mathf.PI * 2f;
            return new Vector2(
                Mathf.Sin(radians) * radius,
                Mathf.Cos(radians) * radius);
        }

        internal static void RefreshAfterNative(
            VRestPopupUI view,
            RestTimeDisplayFormat labelFormat)
        {
            if (view == null)
            {
                return;
            }

            RestClockOverlay overlay;
            if (Overlays.TryGetValue(view.GetInstanceID(), out overlay)
                && overlay != null)
            {
                overlay.ApplyAfterNativeRefresh(labelFormat);
            }
        }

        internal static void Detach(VRestPopupUI view)
        {
            if (view == null)
            {
                return;
            }

            RestClockOverlay overlay;
            if (!Overlays.TryGetValue(view.GetInstanceID(), out overlay)
                || overlay == null)
            {
                return;
            }

            overlay.RestoreNativePresentation();
            Destroy(overlay.gameObject);
        }

        internal static void DetachAll()
        {
            foreach (RestClockOverlay overlay in
                new List<RestClockOverlay>(Overlays.Values))
            {
                if (overlay != null)
                {
                    overlay.RestoreNativePresentation();
                    Destroy(overlay.gameObject);
                }
            }
        }

        internal static bool UsesNoonAtTop(VRestPopupUI view)
        {
            if (view == null)
            {
                return false;
            }

            RestClockOverlay overlay;
            return Overlays.TryGetValue(view.GetInstanceID(), out overlay)
                && overlay != null;
        }

        private void ApplyAfterNativeRefresh(
            RestTimeDisplayFormat labelFormat)
        {
            CaptureNativeTimeText();
            ApplyHalfTurn(
                _arm,
                ref _nativeArmRotation,
                ref _lastAppliedArmRotation,
                ref _hasLastAppliedArmRotation);
            ApplyHalfTurn(
                _fill,
                ref _nativeFillRotation,
                ref _lastAppliedFillRotation,
                ref _hasLastAppliedFillRotation);
            FormatNativeTimes(labelFormat);
        }

        private void CaptureNativeTimeText()
        {
            TextMeshProUGUI current = GetTimeLabel(
                CurrentTimeValueTextField,
                _view);
            TextMeshProUGUI until = GetTimeLabel(
                RestingUntilValueTextField,
                _view);
            if (current != null
                && current.text.IndexOf(" AM", StringComparison.Ordinal) < 0
                && current.text.IndexOf(" PM", StringComparison.Ordinal) < 0)
            {
                _nativeCurrentTimeText = current.text;
            }
            if (until != null
                && until.text.IndexOf(" AM", StringComparison.Ordinal) < 0
                && until.text.IndexOf(" PM", StringComparison.Ordinal) < 0)
            {
                _nativeRestingUntilText = until.text;
            }
        }

        private void RestoreNativePresentation()
        {
            if (_moon != null)
            {
                _moon.anchoredPosition = _moonPosition;
            }
            if (_sun != null)
            {
                _sun.anchoredPosition = _sunPosition;
            }
            if (_arm != null)
            {
                _arm.localRotation = _nativeArmRotation;
            }
            if (_fill != null)
            {
                _fill.localRotation = _nativeFillRotation;
            }
            if (_halfCircle != null)
            {
                _halfCircle.gameObject.SetActive(_halfCircleWasActive);
            }

            RestoreTimeLabel(
                CurrentTimeValueTextField,
                _view,
                _nativeCurrentTimeText);
            RestoreTimeLabel(
                RestingUntilValueTextField,
                _view,
                _nativeRestingUntilText);
        }

        private void FormatNativeTimes(RestTimeDisplayFormat labelFormat)
        {
            if (labelFormat == RestTimeDisplayFormat.TwentyFourHour
                || _view == null)
            {
                return;
            }

            FormatNativeTime(CurrentTimeValueTextField, _view);
            FormatNativeTime(RestingUntilValueTextField, _view);
        }

        private static void ApplyHalfTurn(
            RectTransform rect,
            ref Quaternion nativeRotation,
            ref Quaternion lastAppliedRotation,
            ref bool hasLastAppliedRotation)
        {
            if (rect == null)
            {
                return;
            }

            Quaternion current = rect.localRotation;
            if (!hasLastAppliedRotation
                || Quaternion.Angle(current, lastAppliedRotation) > 0.01f)
            {
                nativeRotation = current;
            }

            lastAppliedRotation = nativeRotation
                * Quaternion.Euler(0f, 0f, 180f);
            rect.localRotation = lastAppliedRotation;
            hasLastAppliedRotation = true;
        }

        private static void FormatNativeTime(
            FieldInfo field,
            VRestPopupUI view)
        {
            TextMeshProUGUI label = GetTimeLabel(field, view);
            if (label == null
                || string.IsNullOrWhiteSpace(label.text)
                || label.text.IndexOf(" AM", StringComparison.Ordinal) >= 0
                || label.text.IndexOf(" PM", StringComparison.Ordinal) >= 0)
            {
                return;
            }

            string[] parts = label.text.Trim().Split(':');
            int hour;
            int minute;
            if (parts.Length != 2
                || !int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out hour)
                || !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out minute))
            {
                return;
            }

            hour = ((hour % 24) + 24) % 24;
            minute = ((minute % 60) + 60) % 60;
            int twelveHour = hour % 12;
            if (twelveHour == 0)
            {
                twelveHour = 12;
            }
            label.SetText(
                twelveHour.ToString(CultureInfo.InvariantCulture)
                + ":"
                + minute.ToString("00", CultureInfo.InvariantCulture)
                + (hour < 12 ? " AM" : " PM"));
        }

        private static TextMeshProUGUI GetTimeLabel(
            FieldInfo field,
            VRestPopupUI view)
        {
            return field == null || view == null
                ? null
                : field.GetValue(view) as TextMeshProUGUI;
        }

        private static void RestoreTimeLabel(
            FieldInfo field,
            VRestPopupUI view,
            string nativeText)
        {
            TextMeshProUGUI label = GetTimeLabel(field, view);
            if (label != null && nativeText != null)
            {
                label.SetText(nativeText);
            }
        }

        private void OnDestroy()
        {
            RestClockOverlay overlay;
            if (Overlays.TryGetValue(_viewId, out overlay)
                && ReferenceEquals(overlay, this))
            {
                Overlays.Remove(_viewId);
            }
        }

        private static RectTransform FindRect(
            Transform parent,
            string path)
        {
            return parent == null ? null : parent.Find(path) as RectTransform;
        }
    }
}
