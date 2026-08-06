using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.HUD;
using Awaken.TG.Main.Heroes.HUD.Bars;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EyesInTheDark
{
    internal sealed class ThreatMeterController
    {
        public const string DefaultColorText = "#8032FF";
        private const string DefaultThreatRedColor = "#FF3028";
        private const float BrightnessMultiplier = 1.5f;

        private readonly ManualLogSource _log;
        private readonly FieldInfo _barField = AccessTools.Field(
            typeof(VCHeroHUDBar),
            "bar");
        private VHeroHUD _heroHud;
        private VCHeroHealthBar _sourceHealth;
        private VCHeroManaBar _sourceMana;
        private VCHeroStaminaBar _sourceStamina;
        private GameObject _root;
        private Bar _bar;
        private Bar[] _bars;
        private TextMeshProUGUI _exactValue;
        private string _appliedColorText;
        private string _appliedRedColorText;
        private string _lastInvalidColorText;
        private string _lastInvalidRedColorText;
        private Color _appliedColor;
        private float _lastColorThreat = -1f;
        private float _lastMinimumVisualScale = -1f;
        private float _lastMaximumVisualScale = -1f;
        private float _lastMaximumRedBlend = -1f;
        private float _lastThreat = -1f;
        private int _lastExactThreatValue = int.MinValue;
        private bool _visible;
        private bool _buildFailureLogged;
        private bool _hasPositionSnapshot;
        private Transform _lastPositionParent;
        private Vector2 _lastHealthPosition;
        private Vector2 _lastManaPosition;
        private Vector2 _lastStaminaPosition;
        private Vector2 _lastHealthSize;
        private Vector2 _lastManaSize;
        private Vector2 _lastStaminaSize;
        private Vector2 _lastThreatPosition;
        private float _lastOffsetX;
        private float _lastOffsetY;
        private bool _lastPlaceBelowResourceBars;

        public ThreatMeterController(ManualLogSource log)
        {
            _log = log;
        }

        public void Attach(VHeroHUD heroHud)
        {
            if (heroHud == null
                || (ReferenceEquals(heroHud, _heroHud)
                    && _root != null
                    && _bar != null))
            {
                return;
            }

            Release();
            try
            {
                if (_barField == null)
                {
                    throw new MissingFieldException(
                        typeof(VCHeroHUDBar).FullName,
                        "bar");
                }

                _sourceHealth = heroHud.GetComponentInChildren<
                    VCHeroHealthBar>(true);
                _sourceMana = heroHud.GetComponentInChildren<
                    VCHeroManaBar>(true);
                _sourceStamina = heroHud.GetComponentInChildren<
                    VCHeroStaminaBar>(true);
                if (_sourceMana == null
                    || _sourceMana.transform.parent == null)
                {
                    throw new InvalidOperationException(
                        "the vanilla Hero mana bar was not available");
                }

                _heroHud = heroHud;
                _root = UnityEngine.Object.Instantiate(
                    _sourceMana.gameObject,
                    _sourceMana.transform.parent,
                    false);
                _root.name = "EyesInTheDarkWyrdThreatMeter";
                _root.hideFlags = HideFlags.DontSave;

                VCHeroManaBar clonedMana =
                    _root.GetComponent<VCHeroManaBar>();
                _bar = clonedMana == null
                    ? null
                    : _barField.GetValue(clonedMana) as Bar;
                if (_bar == null)
                {
                    throw new InvalidOperationException(
                        "the cloned mana controller did not expose its bar");
                }

                _bars = _root.GetComponentsInChildren<Bar>(true);
                for (int index = 0; index < _bars.Length; index++)
                {
                    Bar bar = _bars[index];
                    if (bar == null)
                    {
                        continue;
                    }

                    bar.SetPrediction(0f);
                    bar.SetPercentInstant(0f);
                }
                ApplyColor(
                    DefaultColorText,
                    DefaultThreatRedColor,
                    0f,
                    0.8f,
                    1.2f,
                    0.8f);

                DisableBehaviour(clonedMana);
                DisableBehaviour(FindComponentByTypeName(
                    _root,
                    "Awaken.TG.Main.Heroes.HUD.VCHeroManaCostPredictionBar"));
                DisableBehaviour(FindComponentByTypeName(
                    _root,
                    "Awaken.TG.Main.Heroes.HUD.VCHeroManaReservationBar"));

                bool mirrored = TryMirrorVisuals(
                    _root,
                    true,
                    true);
                Position(0f, 0f, false);
                _root.SetActive(false);
                _visible = false;
                _lastThreat = -1f;
                _buildFailureLogged = false;
                _log.LogInfo(
                    "Created the Eyes-owned Wyrd Threat meter above the vanilla Hero HUD"
                    + (mirrored
                        ? " with horizontally and vertically mirrored artwork."
                        : "."));
            }
            catch (Exception exception)
            {
                Release();
                if (!_buildFailureLogged)
                {
                    _buildFailureLogged = true;
                    _log.LogWarning(
                        "Could not create the Wyrd Threat meter; threat gameplay remains active: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        public void Detach(VHeroHUD heroHud)
        {
            if (heroHud == null || ReferenceEquals(heroHud, _heroHud))
            {
                Release();
            }
        }

        public void Update(
            float threat,
            bool visible,
            string colorText,
            string redColorText,
            float minimumVisualScale,
            float maximumVisualScale,
            float maximumRedBlend,
            bool showExactValue,
            float offsetX,
            float offsetY,
            bool placeBelowResourceBars)
        {
            if (_root == null || _bar == null)
            {
                return;
            }

            ApplyColor(
                colorText,
                redColorText,
                threat,
                minimumVisualScale,
                maximumVisualScale,
                maximumRedBlend);
            if (NeedsPosition(
                offsetX,
                offsetY,
                placeBelowResourceBars))
            {
                Position(
                    offsetX,
                    offsetY,
                    placeBelowResourceBars);
                CapturePosition(
                    offsetX,
                    offsetY,
                    placeBelowResourceBars);
            }
            float clamped = Mathf.Clamp(threat, 0f, 100f);
            float percent = clamped / 100f;
            if (visible && !_visible)
            {
                _bar.SetPercentInstant(percent);
            }
            else if (visible
                && Math.Abs(clamped - _lastThreat) > 0.0001f)
            {
                _bar.SetPercent(percent);
            }
            else if (!visible && _visible)
            {
                _bar.SetPercentInstant(0f);
            }

            EnsureExactValue(showExactValue);
            if (_exactValue != null)
            {
                bool exactVisible = visible && showExactValue;
                if (_exactValue.gameObject.activeSelf != exactVisible)
                {
                    _exactValue.gameObject.SetActive(exactVisible);
                }
                int rounded = Mathf.RoundToInt(clamped);
                if (visible
                    && showExactValue
                    && rounded != _lastExactThreatValue)
                {
                    _exactValue.text = rounded.ToString(
                        CultureInfo.InvariantCulture);
                    _lastExactThreatValue = rounded;
                }
            }

            _visible = visible;
            _lastThreat = clamped;
            if (_root.activeSelf != visible)
            {
                _root.SetActive(visible);
            }
        }

        public void Release()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _heroHud = null;
            _sourceHealth = null;
            _sourceMana = null;
            _sourceStamina = null;
            _root = null;
            _bar = null;
            _bars = null;
            _exactValue = null;
            _appliedColorText = null;
            _appliedRedColorText = null;
            _lastInvalidColorText = null;
            _lastInvalidRedColorText = null;
            _lastColorThreat = -1f;
            _lastMinimumVisualScale = -1f;
            _lastMaximumVisualScale = -1f;
            _lastMaximumRedBlend = -1f;
            _lastThreat = -1f;
            _lastExactThreatValue = int.MinValue;
            _visible = false;
            _hasPositionSnapshot = false;
            _lastPositionParent = null;
        }

        private bool NeedsPosition(
            float offsetX,
            float offsetY,
            bool placeBelowResourceBars)
        {
            RectTransform health = _sourceHealth == null
                ? null
                : _sourceHealth.transform as RectTransform;
            RectTransform mana = _sourceMana == null
                ? null
                : _sourceMana.transform as RectTransform;
            RectTransform stamina = _sourceStamina == null
                ? null
                : _sourceStamina.transform as RectTransform;
            RectTransform threat = _root == null
                ? null
                : _root.transform as RectTransform;
            Transform parent = threat == null ? null : threat.parent;
            return !_hasPositionSnapshot
                || !ReferenceEquals(parent, _lastPositionParent)
                || !Approximately(offsetX, _lastOffsetX)
                || !Approximately(offsetY, _lastOffsetY)
                || placeBelowResourceBars
                    != _lastPlaceBelowResourceBars
                || !Approximately(
                    health == null
                        ? Vector2.zero
                        : health.anchoredPosition,
                    _lastHealthPosition)
                || !Approximately(
                    mana == null ? Vector2.zero : mana.anchoredPosition,
                    _lastManaPosition)
                || !Approximately(
                    stamina == null
                        ? Vector2.zero
                        : stamina.anchoredPosition,
                    _lastStaminaPosition)
                || !Approximately(
                    health == null ? Vector2.zero : health.rect.size,
                    _lastHealthSize)
                || !Approximately(
                    mana == null ? Vector2.zero : mana.rect.size,
                    _lastManaSize)
                || !Approximately(
                    stamina == null ? Vector2.zero : stamina.rect.size,
                    _lastStaminaSize)
                || !Approximately(
                    threat == null
                        ? Vector2.zero
                        : threat.anchoredPosition,
                    _lastThreatPosition);
        }

        private void CapturePosition(
            float offsetX,
            float offsetY,
            bool placeBelowResourceBars)
        {
            RectTransform health = _sourceHealth == null
                ? null
                : _sourceHealth.transform as RectTransform;
            RectTransform mana = _sourceMana == null
                ? null
                : _sourceMana.transform as RectTransform;
            RectTransform stamina = _sourceStamina == null
                ? null
                : _sourceStamina.transform as RectTransform;
            RectTransform threat = _root == null
                ? null
                : _root.transform as RectTransform;
            _lastPositionParent = threat == null ? null : threat.parent;
            _lastHealthPosition = health == null
                ? Vector2.zero
                : health.anchoredPosition;
            _lastManaPosition = mana == null
                ? Vector2.zero
                : mana.anchoredPosition;
            _lastStaminaPosition = stamina == null
                ? Vector2.zero
                : stamina.anchoredPosition;
            _lastHealthSize = health == null
                ? Vector2.zero
                : health.rect.size;
            _lastManaSize = mana == null
                ? Vector2.zero
                : mana.rect.size;
            _lastStaminaSize = stamina == null
                ? Vector2.zero
                : stamina.rect.size;
            _lastThreatPosition = threat == null
                ? Vector2.zero
                : threat.anchoredPosition;
            _lastOffsetX = offsetX;
            _lastOffsetY = offsetY;
            _lastPlaceBelowResourceBars = placeBelowResourceBars;
            _hasPositionSnapshot = true;
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) <= 0.01f;
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return (left - right).sqrMagnitude <= 0.0001f;
        }

        private void ApplyColor(
            string colorText,
            string redColorText,
            float threat,
            float minimumVisualScale,
            float maximumVisualScale,
            float maximumRedBlend)
        {
            string configuredColor = colorText ?? string.Empty;
            string configuredRed = redColorText ?? string.Empty;
            if (string.Equals(
                configuredColor,
                _appliedColorText,
                StringComparison.Ordinal)
                && string.Equals(
                    configuredRed,
                    _appliedRedColorText,
                    StringComparison.Ordinal)
                && Math.Abs(threat - _lastColorThreat) <= 0.0001f
                && Math.Abs(minimumVisualScale - _lastMinimumVisualScale)
                    <= 0.0001f
                && Math.Abs(maximumVisualScale - _lastMaximumVisualScale)
                    <= 0.0001f
                && Math.Abs(maximumRedBlend - _lastMaximumRedBlend)
                    <= 0.0001f)
            {
                return;
            }

            Color color;
            if (!ColorUtility.TryParseHtmlString(
                configuredColor,
                out color))
            {
                ColorUtility.TryParseHtmlString(
                    DefaultColorText,
                    out color);
                if (!string.Equals(
                    configuredColor,
                    _lastInvalidColorText,
                    StringComparison.Ordinal))
                {
                    _lastInvalidColorText = configuredColor;
                    _log.LogWarning(
                        "ThreatMeterColor is invalid; using "
                        + DefaultColorText
                        + ".");
                }
            }
            else
            {
                _lastInvalidColorText = null;
            }

            Color redColor;
            if (!ColorUtility.TryParseHtmlString(
                configuredRed,
                out redColor))
            {
                ColorUtility.TryParseHtmlString(
                    DefaultThreatRedColor,
                    out redColor);
                if (!string.Equals(
                    configuredRed,
                    _lastInvalidRedColorText,
                    StringComparison.Ordinal))
                {
                    _lastInvalidRedColorText = configuredRed;
                    _log.LogWarning(
                        "ThreatRedColor is invalid; using "
                        + DefaultThreatRedColor
                        + ".");
                }
            }
            else
            {
                _lastInvalidRedColorText = null;
            }

            color = WyrdVisualMath.ShiftTowardRed(
                color,
                redColor,
                threat,
                maximumRedBlend);
            color = WyrdVisualMath.ScaleRgb(
                color,
                BrightnessMultiplier
                    * WyrdVisualMath.ThreatScale(
                        threat,
                        minimumVisualScale,
                        maximumVisualScale));

            if (_bars != null)
            {
                for (int index = 0; index < _bars.Length; index++)
                {
                    Bar bar = _bars[index];
                    if (bar == null)
                    {
                        continue;
                    }

                    Color existing = bar.Color;
                    bar.Color = new Color(
                        color.r,
                        color.g,
                        color.b,
                        existing.a);
                }
            }

            _appliedColor = new Color(
                color.r,
                color.g,
                color.b,
                0.96f);
            if (_exactValue != null)
            {
                _exactValue.color = _appliedColor;
            }

            _appliedColorText = configuredColor;
            _appliedRedColorText = configuredRed;
            _lastColorThreat = threat;
            _lastMinimumVisualScale = minimumVisualScale;
            _lastMaximumVisualScale = maximumVisualScale;
            _lastMaximumRedBlend = maximumRedBlend;
        }

        private void Position(
            float offsetX,
            float offsetY,
            bool placeBelowResourceBars)
        {
            RectTransform healthRect = _sourceHealth == null
                ? null
                : _sourceHealth.transform as RectTransform;
            RectTransform manaRect = _sourceMana == null
                ? null
                : _sourceMana.transform as RectTransform;
            RectTransform staminaRect = _sourceStamina == null
                ? null
                : _sourceStamina.transform as RectTransform;
            RectTransform threatRect = _root == null
                ? null
                : _root.transform as RectTransform;
            if (manaRect == null || threatRect == null)
            {
                return;
            }

            Transform parent = threatRect.parent;
            if (placeBelowResourceBars
                && staminaRect != null
                && staminaRect.parent == parent
                && manaRect.parent == parent)
            {
                threatRect.SetSiblingIndex(
                    Mathf.Min(
                        staminaRect.GetSiblingIndex() + 1,
                        parent.childCount - 1));
                Vector2 spacing =
                    staminaRect.anchoredPosition
                    - manaRect.anchoredPosition;
                if (spacing.sqrMagnitude < 0.01f)
                {
                    spacing = Vector2.down
                        * Mathf.Max(
                            1f,
                            Mathf.Abs(staminaRect.rect.height));
                }

                threatRect.anchorMin = staminaRect.anchorMin;
                threatRect.anchorMax = staminaRect.anchorMax;
                threatRect.pivot = staminaRect.pivot;
                threatRect.anchoredPosition =
                    staminaRect.anchoredPosition + spacing;
                threatRect.anchoredPosition += new Vector2(
                    SafeOffset(offsetX),
                    SafeOffset(offsetY));
                return;
            }

            RectTransform topRect = healthRect != null
                && healthRect.parent == parent
                    ? healthRect
                    : manaRect;
            RectTransform nextRect = ReferenceEquals(
                topRect,
                healthRect)
                    ? manaRect
                    : staminaRect;
            bool sameParent = nextRect != null
                && nextRect.parent == parent;
            if (sameParent)
            {
                threatRect.SetSiblingIndex(topRect.GetSiblingIndex());
                LayoutGroup layout = parent.GetComponent<LayoutGroup>();
                if (layout != null && layout.enabled)
                {
                    threatRect.anchorMin = topRect.anchorMin;
                    threatRect.anchorMax = topRect.anchorMax;
                    threatRect.pivot = topRect.pivot;
                    threatRect.anchoredPosition =
                        topRect.anchoredPosition
                        + Vector2.up
                        * Mathf.Max(
                            1f,
                            Mathf.Abs(topRect.rect.height));
                }
                else
                {
                    Vector2 spacing =
                        nextRect.anchoredPosition
                        - topRect.anchoredPosition;
                    if (spacing.sqrMagnitude < 0.01f)
                    {
                        spacing = Vector2.down
                            * Mathf.Max(1f, Mathf.Abs(topRect.rect.height));
                    }

                    threatRect.anchorMin = topRect.anchorMin;
                    threatRect.anchorMax = topRect.anchorMax;
                    threatRect.pivot = topRect.pivot;
                    threatRect.anchoredPosition =
                        topRect.anchoredPosition - spacing;
                }
            }
            else
            {
                threatRect.SetAsFirstSibling();
                threatRect.anchorMin = topRect.anchorMin;
                threatRect.anchorMax = topRect.anchorMax;
                threatRect.pivot = topRect.pivot;
                threatRect.anchoredPosition =
                    topRect.anchoredPosition
                    + Vector2.up
                    * Mathf.Max(1f, Mathf.Abs(topRect.rect.height));
            }

            threatRect.anchoredPosition += new Vector2(
                SafeOffset(offsetX),
                SafeOffset(offsetY));
        }

        private bool TryMirrorVisuals(
            GameObject root,
            bool mirrorHorizontally,
            bool mirrorVertically)
        {
            if (root == null
                || root.transform.childCount == 0
                || (!mirrorHorizontally && !mirrorVertically))
            {
                return false;
            }

            if (root.GetComponent<Graphic>() != null)
            {
                _log.LogWarning(
                    "Could not mirror the Wyrd Threat meter because its controller root renders UI artwork; the normal artwork remains active.");
                return false;
            }

            Transform[] originalChildren =
                new Transform[root.transform.childCount];
            for (int index = 0;
                index < originalChildren.Length;
                index++)
            {
                originalChildren[index] =
                    root.transform.GetChild(index);
            }

            List<Image> horizontalImages = new List<Image>();
            List<int> originalFillOrigins = new List<int>();
            Image[] images = mirrorHorizontally
                ? root.GetComponentsInChildren<Image>(true)
                : new Image[0];
            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                if (image == null
                    || image.type != Image.Type.Filled
                    || image.fillMethod
                        != Image.FillMethod.Horizontal)
                {
                    continue;
                }

                if (image.fillOrigin != 0 && image.fillOrigin != 1)
                {
                    _log.LogWarning(
                        "Could not mirror the Wyrd Threat meter because a horizontal fill image has an unsupported origin; the normal artwork remains active.");
                    return false;
                }

                horizontalImages.Add(image);
                originalFillOrigins.Add(image.fillOrigin);
            }

            FieldInfo indicatorVerticalField = AccessTools.Field(
                typeof(FillBarWithIndicator),
                "vertical");
            FieldInfo indicatorInvertField = AccessTools.Field(
                typeof(FillBarWithIndicator),
                "invertIndicator");
            FillBarWithIndicator[] indicatorBars = mirrorHorizontally
                ? root.GetComponentsInChildren<
                    FillBarWithIndicator>(true)
                : new FillBarWithIndicator[0];
            List<FillBarWithIndicator> horizontalIndicatorBars =
                new List<FillBarWithIndicator>();
            List<bool> originalIndicatorInversions =
                new List<bool>();
            if (indicatorBars.Length > 0
                && (indicatorVerticalField == null
                    || indicatorInvertField == null))
            {
                _log.LogWarning(
                    "Could not mirror the Wyrd Threat meter because its fill-indicator layout is unavailable; the normal artwork remains active.");
                return false;
            }

            for (int index = 0;
                index < indicatorBars.Length;
                index++)
            {
                FillBarWithIndicator indicator =
                    indicatorBars[index];
                if (indicator == null
                    || (bool)indicatorVerticalField.GetValue(
                        indicator))
                {
                    continue;
                }

                horizontalIndicatorBars.Add(indicator);
                originalIndicatorInversions.Add(
                    (bool)indicatorInvertField.GetValue(
                        indicator));
            }

            FieldInfo anchorAxisField = AccessTools.Field(
                typeof(AnchorBar),
                "axis");
            FieldInfo anchorOriginXField = AccessTools.Field(
                typeof(AnchorBar),
                "originX");
            AnchorBar[] anchorBars = mirrorHorizontally
                ? root.GetComponentsInChildren<AnchorBar>(true)
                : new AnchorBar[0];
            List<AnchorBar> horizontalAnchorBars =
                new List<AnchorBar>();
            List<object> originalAnchorOrigins =
                new List<object>();
            if (anchorBars.Length > 0
                && (anchorAxisField == null
                    || anchorOriginXField == null))
            {
                _log.LogWarning(
                    "Could not mirror the Wyrd Threat meter because its anchor-fill layout is unavailable; the normal artwork remains active.");
                return false;
            }

            for (int index = 0; index < anchorBars.Length; index++)
            {
                AnchorBar anchorBar = anchorBars[index];
                if (anchorBar == null
                    || Convert.ToInt32(
                        anchorAxisField.GetValue(anchorBar),
                        CultureInfo.InvariantCulture) != 0)
                {
                    continue;
                }

                object origin = anchorOriginXField.GetValue(
                    anchorBar);
                int originValue = Convert.ToInt32(
                    origin,
                    CultureInfo.InvariantCulture);
                if (origin == null
                    || (originValue != 0 && originValue != 1))
                {
                    _log.LogWarning(
                        "Could not mirror the Wyrd Threat meter because a horizontal anchor has an unsupported origin; the normal artwork remains active.");
                    return false;
                }

                horizontalAnchorBars.Add(anchorBar);
                originalAnchorOrigins.Add(origin);
            }

            GameObject mirrorObject = null;
            try
            {
                mirrorObject = new GameObject(
                    "EyesInTheDarkWyrdThreatMeterMirror",
                    typeof(RectTransform));
                mirrorObject.layer = root.layer;
                RectTransform mirrorRect =
                    mirrorObject.GetComponent<RectTransform>();
                mirrorRect.SetParent(root.transform, false);
                mirrorRect.anchorMin = Vector2.zero;
                mirrorRect.anchorMax = Vector2.one;
                mirrorRect.offsetMin = Vector2.zero;
                mirrorRect.offsetMax = Vector2.zero;
                mirrorRect.pivot = new Vector2(0.5f, 0.5f);
                mirrorRect.localRotation = Quaternion.identity;
                mirrorRect.localScale = Vector3.one;

                for (int index = 0;
                    index < originalChildren.Length;
                    index++)
                {
                    originalChildren[index].SetParent(
                        mirrorRect,
                        true);
                }

                for (int index = 0;
                    index < horizontalImages.Count;
                    index++)
                {
                    horizontalImages[index].fillOrigin =
                        1 - originalFillOrigins[index];
                }

                for (int index = 0;
                    index < horizontalIndicatorBars.Count;
                    index++)
                {
                    indicatorInvertField.SetValue(
                        horizontalIndicatorBars[index],
                        !originalIndicatorInversions[index]);
                }

                for (int index = 0;
                    index < horizontalAnchorBars.Count;
                    index++)
                {
                    object origin = originalAnchorOrigins[index];
                    int mirroredOrigin = 1 - Convert.ToInt32(
                        origin,
                        CultureInfo.InvariantCulture);
                    anchorOriginXField.SetValue(
                        horizontalAnchorBars[index],
                        Enum.ToObject(
                            origin.GetType(),
                            mirroredOrigin));
                }

                mirrorRect.localScale = new Vector3(
                    mirrorHorizontally ? -1f : 1f,
                    mirrorVertically ? -1f : 1f,
                    1f);
                return true;
            }
            catch (Exception exception)
            {
                for (int index = 0;
                    index < horizontalImages.Count;
                    index++)
                {
                    if (horizontalImages[index] != null)
                    {
                        horizontalImages[index].fillOrigin =
                            originalFillOrigins[index];
                    }
                }

                for (int index = 0;
                    index < horizontalIndicatorBars.Count;
                    index++)
                {
                    if (horizontalIndicatorBars[index] != null)
                    {
                        indicatorInvertField.SetValue(
                            horizontalIndicatorBars[index],
                            originalIndicatorInversions[index]);
                    }
                }

                for (int index = 0;
                    index < horizontalAnchorBars.Count;
                    index++)
                {
                    if (horizontalAnchorBars[index] != null)
                    {
                        anchorOriginXField.SetValue(
                            horizontalAnchorBars[index],
                            originalAnchorOrigins[index]);
                    }
                }

                if (mirrorObject != null)
                {
                    RectTransform mirrorRect =
                        mirrorObject.transform as RectTransform;
                    if (mirrorRect != null)
                    {
                        mirrorRect.localScale = Vector3.one;
                    }

                    for (int index = 0;
                        index < originalChildren.Length;
                        index++)
                    {
                        Transform child = originalChildren[index];
                        if (child == null)
                        {
                            continue;
                        }

                        child.SetParent(root.transform, true);
                        child.SetSiblingIndex(index);
                    }

                    UnityEngine.Object.Destroy(mirrorObject);
                }

                _log.LogWarning(
                    "Could not mirror the Wyrd Threat meter; the normal artwork remains active: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private void EnsureExactValue(bool showExactValue)
        {
            if (!showExactValue
                || _exactValue != null
                || _root == null)
            {
                return;
            }

            GameObject label = new GameObject(
                "Exact Wyrd Threat",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            label.hideFlags = HideFlags.DontSave;
            RectTransform rect = (RectTransform)label.transform;
            rect.SetParent(_root.transform, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(60f, 0f);
            rect.anchoredPosition = new Vector2(8f, 0f);

            _exactValue = label.GetComponent<TextMeshProUGUI>();
            _exactValue.raycastTarget = false;
            _exactValue.fontSize = 18f;
            _exactValue.alignment = TextAlignmentOptions.MidlineLeft;
            _exactValue.color = _appliedColor;

            TMP_Text sourceText = _heroHud == null
                ? null
                : _heroHud.GetComponentInChildren<TMP_Text>(true);
            if (sourceText != null)
            {
                _exactValue.font = sourceText.font;
                _exactValue.fontSharedMaterial =
                    sourceText.fontSharedMaterial;
            }
        }

        private static Component FindComponentByTypeName(
            GameObject root,
            string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            return root == null || type == null
                ? null
                : root.GetComponentInChildren(type, true);
        }

        private static void DisableBehaviour(Component component)
        {
            Behaviour behaviour = component as Behaviour;
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        private static float SafeOffset(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : value;
        }
    }
}
