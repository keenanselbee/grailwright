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
        private static readonly Color ThreatColor =
            new Color(0.72f, 0.47f, 1.0f, 0.96f);

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
        private TextMeshProUGUI _exactValue;
        private float _lastThreat = -1f;
        private int _lastExactThreatValue = int.MinValue;
        private bool _visible;
        private bool _buildFailureLogged;

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

                Bar[] bars = _root.GetComponentsInChildren<Bar>(true);
                for (int index = 0; index < bars.Length; index++)
                {
                    Bar bar = bars[index];
                    if (bar == null)
                    {
                        continue;
                    }

                    Color color = bar.Color;
                    bar.Color = new Color(
                        ThreatColor.r,
                        ThreatColor.g,
                        ThreatColor.b,
                        color.a);
                    bar.SetPrediction(0f);
                    bar.SetPercentInstant(0f);
                }

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
            bool showExactValue,
            float offsetX,
            float offsetY,
            bool placeBelowResourceBars)
        {
            if (_root == null || _bar == null)
            {
                return;
            }

            Position(
                offsetX,
                offsetY,
                placeBelowResourceBars);
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
                _exactValue.gameObject.SetActive(
                    visible && showExactValue);
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
            _root.SetActive(visible);
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
            _exactValue = null;
            _lastThreat = -1f;
            _lastExactThreatValue = int.MinValue;
            _visible = false;
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
            _exactValue.color = ThreatColor;

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
