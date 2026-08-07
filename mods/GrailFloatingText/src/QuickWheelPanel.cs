using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

namespace GrailFloatingText
{
    public static class QuickWheelPanelApi
    {
        public const int ApiVersion = 1;

        public static bool TrySet(
            string sourceId,
            string leftTitle,
            string leftSubtitle,
            string[] leftTexts,
            string[] leftIconIds,
            string[] leftStyles,
            string rightTitle,
            string rightSubtitle,
            string[] rightTexts,
            string[] rightIconIds,
            string[] rightStyles,
            float opacity,
            float tooltipOpacity,
            float fadeSeconds,
            float rightOffset,
            float topOffset,
            float scale)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.SetQuickWheelPanel(
                sourceId,
                leftTitle,
                leftSubtitle,
                leftTexts,
                leftIconIds,
                leftStyles,
                rightTitle,
                rightSubtitle,
                rightTexts,
                rightIconIds,
                rightStyles,
                opacity,
                tooltipOpacity,
                fadeSeconds,
                rightOffset,
                topOffset,
                scale);
        }

        public static bool SetTooltipActive(string sourceId, bool active)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.SetQuickWheelTooltipActive(sourceId, active);
        }

        public static void Clear(string sourceId)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            if (plugin != null)
            {
                plugin.ClearQuickWheelPanel(sourceId);
            }
        }
    }

    public sealed partial class GrailFloatingTextPlugin
    {
        private QuickWheelPanelState _quickWheelPanelState;
        private QuickWheelPanelView _quickWheelPanelView;
        private bool _quickWheelTooltipActive;
        private float _quickWheelPanelAlpha;
        private float _quickWheelPanelLastUpdateTime;

        internal bool SetQuickWheelPanel(
            string sourceId,
            string leftTitle,
            string leftSubtitle,
            string[] leftTexts,
            string[] leftIconIds,
            string[] leftStyles,
            string rightTitle,
            string rightSubtitle,
            string[] rightTexts,
            string[] rightIconIds,
            string[] rightStyles,
            float opacity,
            float tooltipOpacity,
            float fadeSeconds,
            float rightOffset,
            float topOffset,
            float scale)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            _quickWheelPanelState = new QuickWheelPanelState
            {
                SourceId = sourceId.Trim(),
                LeftTitle = leftTitle ?? string.Empty,
                LeftSubtitle = leftSubtitle ?? string.Empty,
                LeftTexts = Copy(leftTexts),
                LeftIconIds = Copy(leftIconIds),
                LeftStyles = Copy(leftStyles),
                RightTitle = rightTitle ?? string.Empty,
                RightSubtitle = rightSubtitle ?? string.Empty,
                RightTexts = Copy(rightTexts),
                RightIconIds = Copy(rightIconIds),
                RightStyles = Copy(rightStyles),
                Opacity = Clamp01(opacity),
                TooltipOpacity = Clamp01(tooltipOpacity),
                FadeSeconds = Math.Max(0.0f, fadeSeconds),
                RightOffset = Math.Max(0.0f, rightOffset),
                TopOffset = Math.Max(0.0f, topOffset),
                Scale = Math.Max(0.5f, Math.Min(2.0f, scale))
            };
            return true;
        }

        internal bool SetQuickWheelTooltipActive(string sourceId, bool active)
        {
            if (_quickWheelPanelState == null
                || !string.Equals(_quickWheelPanelState.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _quickWheelTooltipActive = active;
            return true;
        }

        internal void ClearQuickWheelPanel(string sourceId)
        {
            if (_quickWheelPanelState != null
                && string.Equals(_quickWheelPanelState.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
            {
                _quickWheelPanelState = null;
                _quickWheelTooltipActive = false;
                SetQuickWheelPanelActive(false);
            }
        }

        private void UpdateQuickWheelPanel(float now)
        {
            if (_quickWheelPanelState == null)
            {
                SetQuickWheelPanelActive(false);
                return;
            }

            EnsureQuickWheelPanelView();
            QuickWheelPanelState state = _quickWheelPanelState;
            float target = state.Opacity * (_quickWheelTooltipActive ? state.TooltipOpacity : 1.0f);
            float delta = _quickWheelPanelLastUpdateTime <= 0.0f
                ? 0.0f
                : Math.Max(0.0f, now - _quickWheelPanelLastUpdateTime);
            _quickWheelPanelLastUpdateTime = now;
            if (state.FadeSeconds <= 0.001f || _quickWheelPanelAlpha <= 0.001f)
            {
                _quickWheelPanelAlpha = target;
            }
            else
            {
                _quickWheelPanelAlpha = Mathf.MoveTowards(
                    _quickWheelPanelAlpha,
                    target,
                    delta / state.FadeSeconds);
            }

            QuickWheelPanelView view = _quickWheelPanelView;
            view.Root.gameObject.SetActive(_quickWheelPanelAlpha > 0.001f);
            view.Group.alpha = _quickWheelPanelAlpha;
            if (!view.Root.gameObject.activeSelf)
            {
                return;
            }

            FontAsset font;
            try
            {
                font = ResolveConfiguredFontAsset();
            }
            catch (Exception ex)
            {
                LogFontDiagnosticOnce(
                    "ResolveQuickWheelPanelFontAsset:" + ex.GetType().FullName,
                    "Could not resolve the configured font for the quick-wheel panel; using the safe fallback font. "
                        + ex.GetBaseException().Message);
                font = ResolveFallbackFontAsset();
            }
            float scale = state.Scale;
            float columnWidth = 226.0f * scale;
            float columnGap = 26.0f * scale;
            float rowHeight = 24.0f * scale;
            int rowCount = Math.Max(state.LeftTexts.Length, state.RightTexts.Length);
            float height = (66.0f + rowCount * 24.0f) * scale;
            view.Root.anchorMin = Vector2.one;
            view.Root.anchorMax = Vector2.one;
            view.Root.pivot = Vector2.one;
            view.Root.anchoredPosition = new Vector2(-state.RightOffset, -state.TopOffset);
            view.Root.sizeDelta = new Vector2(columnWidth * 2.0f + columnGap, height);

            ConfigurePanelText(view.LeftTitle, state.LeftTitle, font, 18.0f * scale, TMPro.FontStyles.Bold);
            ConfigurePanelText(view.LeftSubtitle, state.LeftSubtitle, font, 14.0f * scale, TMPro.FontStyles.Normal);
            ConfigurePanelText(view.RightTitle, state.RightTitle, font, 18.0f * scale, TMPro.FontStyles.Bold);
            ConfigurePanelText(view.RightSubtitle, state.RightSubtitle, font, 14.0f * scale, TMPro.FontStyles.Normal);
            LayoutText(view.LeftTitle, 0.0f, 0.0f, columnWidth, 28.0f * scale);
            LayoutText(view.LeftSubtitle, 0.0f, -29.0f * scale, columnWidth, 25.0f * scale);
            float rightX = columnWidth + columnGap;
            LayoutText(view.RightTitle, rightX, 0.0f, columnWidth, 28.0f * scale);
            LayoutText(view.RightSubtitle, rightX, -29.0f * scale, columnWidth, 25.0f * scale);
            view.LeftTitle.color = ResolveStyleColor("Reward", 1.0f);
            view.RightTitle.color = ResolveStyleColor("Combat", 1.0f);
            view.LeftSubtitle.color = ResolveStyleColor("Experience", 1.0f);
            view.RightSubtitle.color = ResolveStyleColor("System", 1.0f);

            UpdatePanelRows(view.LeftRows, state.LeftTexts, state.LeftIconIds, state.LeftStyles, 0.0f, -59.0f * scale, columnWidth, rowHeight, scale, font);
            UpdatePanelRows(view.RightRows, state.RightTexts, state.RightIconIds, state.RightStyles, rightX, -59.0f * scale, columnWidth, rowHeight, scale, font);
            view.Root.SetAsLastSibling();
        }

        private void EnsureQuickWheelPanelView()
        {
            EnsureNotificationCanvas();
            if (_quickWheelPanelView != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("QuickWheelPanel", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(_overlayRoot, false);
            _quickWheelPanelView = new QuickWheelPanelView
            {
                Root = root,
                Group = rootObject.GetComponent<CanvasGroup>(),
                LeftTitle = CreatePanelText(root, "LeftTitle"),
                LeftSubtitle = CreatePanelText(root, "LeftSubtitle"),
                RightTitle = CreatePanelText(root, "RightTitle"),
                RightSubtitle = CreatePanelText(root, "RightSubtitle")
            };
        }

        private void UpdatePanelRows(
            List<QuickWheelPanelRowView> views,
            string[] texts,
            string[] iconIds,
            string[] styles,
            float x,
            float startY,
            float width,
            float rowHeight,
            float scale,
            FontAsset font)
        {
            while (views.Count < texts.Length)
            {
                views.Add(CreatePanelRow(_quickWheelPanelView.Root, views.Count));
            }

            for (int i = 0; i < views.Count; i++)
            {
                QuickWheelPanelRowView row = views[i];
                bool active = i < texts.Length && !string.IsNullOrWhiteSpace(texts[i]);
                row.Root.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                string style = ValueAt(styles, i, "System");
                Color color = ResolveStyleColor(style, 1.0f);
                Texture2D texture = GetIconTexture(ValueAt(iconIds, i, string.Empty));
                row.Root.anchorMin = new Vector2(0.0f, 1.0f);
                row.Root.anchorMax = new Vector2(0.0f, 1.0f);
                row.Root.pivot = new Vector2(0.0f, 1.0f);
                row.Root.anchoredPosition = new Vector2(x, startY - i * rowHeight);
                row.Root.sizeDelta = new Vector2(width, rowHeight);
                row.Icon.gameObject.SetActive(texture != null);
                float iconWidth = texture == null ? 0.0f : 22.0f * scale;
                if (texture != null)
                {
                    row.Icon.texture = texture;
                    row.Icon.color = ResolveIconColor(style, color, 1.0f);
                    row.Icon.rectTransform.anchorMin = new Vector2(0.0f, 0.5f);
                    row.Icon.rectTransform.anchorMax = new Vector2(0.0f, 0.5f);
                    row.Icon.rectTransform.pivot = new Vector2(0.0f, 0.5f);
                    row.Icon.rectTransform.anchoredPosition = Vector2.zero;
                    row.Icon.rectTransform.sizeDelta = new Vector2(17.0f * scale, 17.0f * scale);
                }

                ConfigurePanelText(row.Text, texts[i], font, 14.0f * scale, TMPro.FontStyles.Normal);
                row.Text.color = color;
                LayoutText(row.Text, iconWidth, 0.0f, width - iconWidth, rowHeight);
            }
        }

        private QuickWheelPanelRowView CreatePanelRow(RectTransform parent, int index)
        {
            GameObject rootObject = new GameObject("PanelRow" + index, typeof(RectTransform));
            rootObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            return new QuickWheelPanelRowView
            {
                Root = root,
                Icon = CreateNotificationIcon(root, "Icon"),
                Text = CreatePanelText(root, "Text")
            };
        }

        private static TextMeshProUGUI CreatePanelText(RectTransform parent, string name)
        {
            TextMeshProUGUI text = CreateNotificationText(parent, name);
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void ConfigurePanelText(TextMeshProUGUI text, string value, FontAsset font, float size, TMPro.FontStyles style)
        {
            if (font != null && !ReferenceEquals(text.font, font))
            {
                text.font = font;
            }
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.raycastTarget = false;
        }

        private static void LayoutText(TextMeshProUGUI text, float x, float y, float width, float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(Math.Max(1.0f, width), Math.Max(1.0f, height));
        }

        private void SetQuickWheelPanelActive(bool active)
        {
            if (_quickWheelPanelView != null && _quickWheelPanelView.Root != null)
            {
                _quickWheelPanelView.Root.gameObject.SetActive(active);
            }
            if (!active)
            {
                _quickWheelPanelLastUpdateTime = 0.0f;
            }
        }

        private void ReleaseQuickWheelPanelView()
        {
            _quickWheelPanelView = null;
            _quickWheelPanelState = null;
            _quickWheelTooltipActive = false;
            _quickWheelPanelAlpha = 0.0f;
            _quickWheelPanelLastUpdateTime = 0.0f;
        }

        private static string[] Copy(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new string[0];
            }
            string[] copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static string ValueAt(string[] values, int index, string fallback)
        {
            return values != null && index >= 0 && index < values.Length && !string.IsNullOrWhiteSpace(values[index])
                ? values[index]
                : fallback;
        }

        private sealed class QuickWheelPanelState
        {
            internal string SourceId;
            internal string LeftTitle;
            internal string LeftSubtitle;
            internal string[] LeftTexts;
            internal string[] LeftIconIds;
            internal string[] LeftStyles;
            internal string RightTitle;
            internal string RightSubtitle;
            internal string[] RightTexts;
            internal string[] RightIconIds;
            internal string[] RightStyles;
            internal float Opacity;
            internal float TooltipOpacity;
            internal float FadeSeconds;
            internal float RightOffset;
            internal float TopOffset;
            internal float Scale;
        }

        private sealed class QuickWheelPanelView
        {
            internal RectTransform Root;
            internal CanvasGroup Group;
            internal TextMeshProUGUI LeftTitle;
            internal TextMeshProUGUI LeftSubtitle;
            internal TextMeshProUGUI RightTitle;
            internal TextMeshProUGUI RightSubtitle;
            internal readonly List<QuickWheelPanelRowView> LeftRows = new List<QuickWheelPanelRowView>();
            internal readonly List<QuickWheelPanelRowView> RightRows = new List<QuickWheelPanelRowView>();
        }

        private sealed class QuickWheelPanelRowView
        {
            internal RectTransform Root;
            internal RawImage Icon;
            internal TextMeshProUGUI Text;
        }
    }
}
