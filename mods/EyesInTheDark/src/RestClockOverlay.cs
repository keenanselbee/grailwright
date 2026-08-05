using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Awaken.TG.Main.Heroes.Resting;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EyesInTheDark
{
    internal enum RestClockLabelFormat
    {
        TwelveHour,
        TwentyFourHour
    }

    internal sealed class RestClockOverlay : MonoBehaviour
    {
        private const float IconRadius = 52f;
        private const float HourLabelRadius = 86f;
        private static readonly FieldInfo CurrentTimeValueTextField =
            AccessTools.Field(
                typeof(VRestPopupUI),
                "currentTimeValueText");
        private static readonly FieldInfo RestingUntilValueTextField =
            AccessTools.Field(
                typeof(VRestPopupUI),
                "restingTimeUntilValueText");
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
        private bool _halfCircleWasActive;
        private int _viewId;

        internal static void Attach(
            VRestPopupUI view,
            RestClockLabelFormat labelFormat)
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
                existing.FormatNativeTimes(labelFormat);
                return;
            }

            GameObject rootObject = new GameObject(
                "EITD_RestClock",
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
                overlay._halfCircleWasActive =
                    halfCircle.gameObject.activeSelf;
                overlay._viewId = view.GetInstanceID();
                Overlays[overlay._viewId] = overlay;
                overlay.Build(root, textTemplate, labelFormat);

                MovePhaseIcon(moon, root.anchoredPosition, 0f, IconRadius);
                MovePhaseIcon(sun, root.anchoredPosition, 12f, IconRadius);
                halfCircle.gameObject.SetActive(false);
                overlay.ApplyAfterNativeRefresh(labelFormat);
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
            RestClockLabelFormat labelFormat)
        {
            float diameter = Mathf.Min(
                root.sizeDelta.x,
                root.sizeDelta.y);
            if (diameter < 100f)
            {
                diameter = 260f;
                root.sizeDelta = new Vector2(diameter, diameter);
            }

            Color hourColor = textTemplate.color;
            hourColor.a = 0.78f;
            if (labelFormat == RestClockLabelFormat.TwentyFourHour)
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
            RestClockLabelFormat labelFormat)
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
            RestClockLabelFormat labelFormat)
        {
            RotateHalfTurn(_arm);
            RotateHalfTurn(_fill);
            FormatNativeTimes(labelFormat);
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
            if (_halfCircle != null)
            {
                _halfCircle.gameObject.SetActive(
                    _halfCircleWasActive);
            }
        }

        private void FormatNativeTimes(RestClockLabelFormat labelFormat)
        {
            if (labelFormat == RestClockLabelFormat.TwentyFourHour
                || _view == null)
            {
                return;
            }

            FormatNativeTime(CurrentTimeValueTextField, _view);
            FormatNativeTime(RestingUntilValueTextField, _view);
        }

        private static void RotateHalfTurn(RectTransform rect)
        {
            if (rect != null)
            {
                rect.rotation *= Quaternion.Euler(0f, 0f, 180f);
            }
        }

        private static void FormatNativeTime(
            FieldInfo field,
            VRestPopupUI view)
        {
            TextMeshProUGUI label = field == null
                ? null
                : field.GetValue(view) as TextMeshProUGUI;
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
