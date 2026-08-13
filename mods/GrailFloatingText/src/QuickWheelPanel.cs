using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

namespace GrailFloatingText
{
    public static class QuickWheelPanelApi
    {
        public const int ApiVersion = 15;

        public static bool TryRegisterIcons(
            string sourceId,
            string iconDirectory,
            string[] iconIds,
            string[] fileNames)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.RegisterQuickWheelIcons(sourceId, iconDirectory, iconIds, fileNames);
        }

        public static bool TrySet(
            string sourceId,
            string leftTitle,
            string leftSubtitle,
            string[] leftTexts,
            string[] leftIconIds,
            string[] leftStyles,
            string[] leftResourceTexts,
            string[] leftResourceIconIds,
            string[] leftResourceStyles,
            int leftSummaryRowCount,
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
            float scale,
            float panelColumnWidth,
            float columnGap,
            float panelBackgroundOpacity,
            float panelBackgroundPadding,
            bool textShadowEnabled,
            float textShadowOpacity,
            float textShadowOffset,
            float textShadowSoftness,
            int textShadowStrength,
            bool textOutlineEnabled,
            string textOutlineColor,
            float textOutlineOpacity,
            float textOutlineWidth,
            int textOutlineStrength,
            float whiteTextOutlineStrengthMultiplier,
            string headerColor,
            string subheaderColor)
        {
            GrailFloatingTextPlugin plugin = GrailFloatingTextPlugin.Instance;
            return plugin != null && plugin.SetQuickWheelPanel(
                sourceId,
                leftTitle,
                leftSubtitle,
                leftTexts,
                leftIconIds,
                leftStyles,
                leftResourceTexts,
                leftResourceIconIds,
                leftResourceStyles,
                leftSummaryRowCount,
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
                scale,
                panelColumnWidth,
                columnGap,
                panelBackgroundOpacity,
                panelBackgroundPadding,
                textShadowEnabled,
                textShadowOpacity,
                textShadowOffset,
                textShadowSoftness,
                textShadowStrength,
                textOutlineEnabled,
                textOutlineColor,
                textOutlineOpacity,
                textOutlineWidth,
                textOutlineStrength,
                whiteTextOutlineStrengthMultiplier,
                headerColor,
                subheaderColor);
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

    internal sealed class QuickWheelTextEffect : BaseMeshEffect
    {
        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineSoftnessId = Shader.PropertyToID("_OutlineSoftness");
        private static readonly int GradientScaleId = Shader.PropertyToID("_GradientScale");
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        private const string UnderlayKeyword = "UNDERLAY_ON";

        private TextMeshProUGUI _text;
        private FontAsset _font;
        private Material _sourceMaterial;
        private Material _effectMaterial;
        private Color _faceColor = Color.white;
        private bool _nativeSdf;
        private bool _configured;
        private bool _outlineEnabled;
        private Color _outlineColor;
        private float _outlineWidth;
        private int _outlineStrength = 1;
        private float _outlineStrengthMultiplier = 1.0f;
        private bool _shadowEnabled;
        private Color _shadowColor;
        private float _shadowOffset;
        private float _shadowSoftness;
        private int _shadowStrength = 1;

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (_nativeSdf
                || !IsActive()
                || vertexHelper == null
                || vertexHelper.currentVertCount == 0)
            {
                return;
            }

            List<UIVertex> source = new List<UIVertex>();
            vertexHelper.GetUIVertexStream(source);
            List<UIVertex> output = new List<UIVertex>(
                source.Count * (1 + (_outlineEnabled ? 4 : 0) + (_shadowEnabled ? 1 : 0)));

            if (_shadowEnabled)
            {
                float fallbackShadowOffset = _shadowOffset > 0.001f
                    ? _shadowOffset
                    : Math.Max(1.0f, _shadowStrength * 0.5f);
                AppendOffset(source, output, fallbackShadowOffset, -fallbackShadowOffset, _shadowColor);
            }

            if (_outlineEnabled)
            {
                float outlineWidth = _outlineWidth * _outlineStrengthMultiplier;
                AppendOffset(source, output, outlineWidth, outlineWidth, _outlineColor);
                AppendOffset(source, output, outlineWidth, -outlineWidth, _outlineColor);
                AppendOffset(source, output, -outlineWidth, outlineWidth, _outlineColor);
                AppendOffset(source, output, -outlineWidth, -outlineWidth, _outlineColor);
            }

            output.AddRange(source);
            vertexHelper.Clear();
            vertexHelper.AddUIVertexTriangleStream(output);
        }

        internal void Configure(
            bool outlineEnabled,
            Color outlineColor,
            float outlineWidth,
            int outlineStrength,
            bool shadowEnabled,
            Color shadowColor,
            float shadowOffset,
            float shadowSoftness,
            int shadowStrength)
        {
            EnsureText();
            bool materialChanged = EnsureEffectMaterial();
            outlineWidth = Math.Max(0.0f, Math.Min(16.0f, outlineWidth));
            outlineStrength = Math.Max(1, Math.Min(8, outlineStrength));
            shadowOffset = Math.Max(0.0f, Math.Min(16.0f, shadowOffset));
            shadowSoftness = Mathf.Clamp01(shadowSoftness);
            shadowStrength = Math.Max(1, Math.Min(8, shadowStrength));
            outlineEnabled = outlineEnabled && outlineWidth > 0.001f && outlineColor.a > 0.001f;
            shadowEnabled = shadowEnabled && shadowColor.a > 0.001f;
            bool settingsChanged = !_configured
                || _outlineEnabled != outlineEnabled
                || _outlineColor != outlineColor
                || _outlineWidth != outlineWidth
                || _outlineStrength != outlineStrength
                || _shadowEnabled != shadowEnabled
                || _shadowColor != shadowColor
                || _shadowOffset != shadowOffset
                || _shadowSoftness != shadowSoftness
                || _shadowStrength != shadowStrength;
            if (!materialChanged && !settingsChanged)
            {
                return;
            }

            _configured = true;
            _outlineEnabled = outlineEnabled;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
            _outlineStrength = outlineStrength;
            _shadowEnabled = shadowEnabled;
            _shadowColor = shadowColor;
            _shadowOffset = shadowOffset;
            _shadowSoftness = shadowSoftness;
            _shadowStrength = shadowStrength;

            if (_nativeSdf)
            {
                ApplyNativeSdf();
            }
            else if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        internal void SetFaceColor(Color color)
        {
            EnsureText();
            EnsureEffectMaterial();
            if (_faceColor == color)
            {
                return;
            }

            _faceColor = color;
            if (_effectMaterial != null && _effectMaterial.HasProperty(FaceColorId))
            {
                _effectMaterial.SetColor(FaceColorId, color);
                if (graphic != null)
                {
                    graphic.SetMaterialDirty();
                }
                return;
            }

            if (_text != null)
            {
                _text.color = color;
            }
        }

        internal void SetOutlineStrengthMultiplier(float multiplier)
        {
            multiplier = Mathf.Clamp(multiplier, 0.5f, 2.0f);
            if (Mathf.Approximately(_outlineStrengthMultiplier, multiplier))
            {
                return;
            }

            _outlineStrengthMultiplier = multiplier;
            if (_nativeSdf && _configured)
            {
                ApplyNativeSdf();
            }
            else if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        private void EnsureText()
        {
            if (_text == null)
            {
                _text = GetComponent<TextMeshProUGUI>();
            }
        }

        private bool EnsureEffectMaterial()
        {
            if (_text == null || _text.font == null || _text.fontSharedMaterial == null)
            {
                return false;
            }

            Material current = _text.fontSharedMaterial;
            if (ReferenceEquals(_font, _text.font)
                && _effectMaterial != null
                && ReferenceEquals(current, _effectMaterial))
            {
                return false;
            }

            Material source = ReferenceEquals(current, _effectMaterial)
                ? _text.font.material
                : current;
            if (source == null || ReferenceEquals(source, _effectMaterial))
            {
                source = _sourceMaterial;
            }
            if (source == null)
            {
                return false;
            }

            ReleaseEffectMaterial(false);
            _font = _text.font;
            _sourceMaterial = source;
            _effectMaterial = new Material(source);
            _effectMaterial.name = "GrailFloatingText-QuickWheel-" + source.name;
            _effectMaterial.hideFlags = HideFlags.HideAndDontSave;
            _text.fontSharedMaterial = _effectMaterial;
            _nativeSdf = SupportsNativeSdf(_effectMaterial);
            _text.extraPadding = true;
            _configured = false;
            if (_effectMaterial.HasProperty(FaceColorId))
            {
                _effectMaterial.SetColor(FaceColorId, _faceColor);
            }
            return true;
        }

        private void ApplyNativeSdf()
        {
            float normalizedOutlineWidth = _outlineEnabled
                ? Mathf.Clamp01(
                    (_outlineWidth / 16.0f)
                    * (0.5f + _outlineStrength / 8.0f)
                    * _outlineStrengthMultiplier)
                : 0.0f;
            _effectMaterial.SetColor(
                OutlineColorId,
                _outlineEnabled ? _outlineColor : new Color(0.0f, 0.0f, 0.0f, 0.0f));
            _effectMaterial.SetFloat(OutlineWidthId, normalizedOutlineWidth);
            if (_effectMaterial.HasProperty(OutlineSoftnessId))
            {
                _effectMaterial.SetFloat(OutlineSoftnessId, 0.0f);
            }

            if (_shadowEnabled)
            {
                float normalizedShadowOffset = Mathf.Clamp01(_shadowOffset / 16.0f);
                _effectMaterial.EnableKeyword(UnderlayKeyword);
                _effectMaterial.SetColor(UnderlayColorId, _shadowColor);
                _effectMaterial.SetFloat(UnderlayOffsetXId, normalizedShadowOffset);
                _effectMaterial.SetFloat(UnderlayOffsetYId, -normalizedShadowOffset);
                _effectMaterial.SetFloat(
                    UnderlayDilateId,
                    Mathf.Clamp01(_shadowStrength / 8.0f));
                _effectMaterial.SetFloat(UnderlaySoftnessId, _shadowSoftness);
            }
            else
            {
                _effectMaterial.DisableKeyword(UnderlayKeyword);
                _effectMaterial.SetColor(UnderlayColorId, new Color(0.0f, 0.0f, 0.0f, 0.0f));
                _effectMaterial.SetFloat(UnderlayOffsetXId, 0.0f);
                _effectMaterial.SetFloat(UnderlayOffsetYId, 0.0f);
                _effectMaterial.SetFloat(UnderlayDilateId, 0.0f);
                _effectMaterial.SetFloat(UnderlaySoftnessId, 0.0f);
            }

            ShaderUtilities.UpdateShaderRatios(_effectMaterial);
            _text.UpdateMeshPadding();
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
            }
        }

        private static bool SupportsNativeSdf(Material material)
        {
            return material != null
                && material.HasProperty(GradientScaleId)
                && material.HasProperty(FaceColorId)
                && material.HasProperty(OutlineColorId)
                && material.HasProperty(OutlineWidthId)
                && material.HasProperty(UnderlayColorId)
                && material.HasProperty(UnderlayOffsetXId)
                && material.HasProperty(UnderlayOffsetYId)
                && material.HasProperty(UnderlayDilateId)
                && material.HasProperty(UnderlaySoftnessId);
        }

        protected override void OnDestroy()
        {
            ReleaseEffectMaterial(true);
            base.OnDestroy();
        }

        private void ReleaseEffectMaterial(bool restoreSource)
        {
            if (restoreSource
                && _text != null
                && _effectMaterial != null
                && ReferenceEquals(_text.fontSharedMaterial, _effectMaterial))
            {
                _text.fontSharedMaterial = _sourceMaterial;
            }

            if (_effectMaterial != null)
            {
                UnityEngine.Object.Destroy(_effectMaterial);
            }
            _effectMaterial = null;
            _sourceMaterial = null;
            _nativeSdf = false;
        }

        private static void AppendOffset(
            List<UIVertex> source,
            List<UIVertex> output,
            float x,
            float y,
            Color color)
        {
            Color32 effectColor = color;
            for (int i = 0; i < source.Count; i++)
            {
                UIVertex vertex = source[i];
                Vector3 position = vertex.position;
                position.x += x;
                position.y += y;
                vertex.position = position;
                Color32 vertexColor = vertex.color;
                effectColor.a = (byte)(color.a * vertexColor.a);
                vertex.color = effectColor;
                output.Add(vertex);
            }
        }
    }

    public sealed partial class GrailFloatingTextPlugin
    {
        private QuickWheelPanelState _quickWheelPanelState;
        private QuickWheelPanelView _quickWheelPanelView;
        private Texture2D _quickWheelPanelLeftBackgroundTexture;
        private Texture2D _quickWheelPanelRightBackgroundTexture;
        private bool _quickWheelPanelLayoutDirty;
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
            string[] leftResourceTexts,
            string[] leftResourceIconIds,
            string[] leftResourceStyles,
            int leftSummaryRowCount,
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
            float scale,
            float panelColumnWidth,
            float columnGap,
            float panelBackgroundOpacity,
            float panelBackgroundPadding,
            bool textShadowEnabled,
            float textShadowOpacity,
            float textShadowOffset,
            float textShadowSoftness,
            int textShadowStrength,
            bool textOutlineEnabled,
            string textOutlineColor,
            float textOutlineOpacity,
            float textOutlineWidth,
            int textOutlineStrength,
            float whiteTextOutlineStrengthMultiplier,
            string headerColor,
            string subheaderColor)
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
                LeftResourceTexts = Copy(leftResourceTexts),
                LeftResourceIconIds = Copy(leftResourceIconIds),
                LeftResourceStyles = Copy(leftResourceStyles),
                LeftSummaryRowCount = Math.Max(0, Math.Min(
                    leftTexts == null ? 0 : leftTexts.Length,
                    leftSummaryRowCount)),
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
                Scale = Math.Max(0.5f, Math.Min(2.0f, scale)),
                PanelColumnWidth = Math.Max(160.0f, Math.Min(400.0f, panelColumnWidth)),
                ColumnGap = Math.Max(0.0f, Math.Min(200.0f, columnGap)),
                PanelBackgroundOpacity = Clamp01(panelBackgroundOpacity),
                PanelBackgroundPadding = Math.Max(0.0f, Math.Min(32.0f, panelBackgroundPadding)),
                TextShadowEnabled = textShadowEnabled,
                TextShadowOpacity = Clamp01(textShadowOpacity),
                TextShadowOffset = Math.Max(0.0f, Math.Min(16.0f, textShadowOffset)),
                TextShadowSoftness = Clamp01(textShadowSoftness),
                TextShadowStrength = Math.Max(1, Math.Min(8, textShadowStrength)),
                TextOutlineEnabled = textOutlineEnabled,
                TextOutlineColor = string.IsNullOrWhiteSpace(textOutlineColor) ? "#18130D" : textOutlineColor.Trim(),
                TextOutlineOpacity = Clamp01(textOutlineOpacity),
                TextOutlineWidth = Math.Max(0.0f, Math.Min(16.0f, textOutlineWidth)),
                TextOutlineStrength = Math.Max(1, Math.Min(8, textOutlineStrength)),
                WhiteTextOutlineStrengthMultiplier = Math.Max(0.5f, Math.Min(2.0f, whiteTextOutlineStrengthMultiplier)),
                HeaderColor = string.IsNullOrWhiteSpace(headerColor) ? "#D88B38" : headerColor.Trim(),
                SubheaderColor = string.IsNullOrWhiteSpace(subheaderColor) ? "White" : subheaderColor.Trim()
            };
            _quickWheelPanelLayoutDirty = true;
            return true;
        }

        internal bool RegisterQuickWheelIcons(
            string sourceId,
            string iconDirectory,
            string[] iconIds,
            string[] fileNames)
        {
            if (string.IsNullOrWhiteSpace(sourceId)
                || string.IsNullOrWhiteSpace(iconDirectory)
                || iconIds == null
                || fileNames == null
                || iconIds.Length != fileNames.Length
                || iconIds.Length == 0
                || !Directory.Exists(iconDirectory))
            {
                return false;
            }

            MethodInfo loadImageMethod = typeof(ImageConversion).GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);
            if (loadImageMethod == null)
            {
                return false;
            }

            int loaded = 0;
            for (int i = 0; i < iconIds.Length; i++)
            {
                string iconId = iconIds[i] == null ? string.Empty : iconIds[i].Trim();
                string fileName = fileNames[i] == null ? string.Empty : Path.GetFileName(fileNames[i].Trim());
                if (iconId.Length == 0 || fileName.Length == 0)
                {
                    continue;
                }

                string path = Path.Combine(iconDirectory, fileName);
                if (!File.Exists(path))
                {
                    Logger.LogWarning("Quick-wheel icon was not found for " + sourceId + ": " + path);
                    continue;
                }

                Texture2D texture = null;
                try
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    object loadResult = loadImageMethod.Invoke(null, new object[] { texture, File.ReadAllBytes(path), false });
                    if (!(loadResult is bool) || !((bool)loadResult))
                    {
                        UnityEngine.Object.Destroy(texture);
                        continue;
                    }

                    texture.name = "GrailFloatingTextQuickWheelIcon_" + iconId;
                    texture.hideFlags = HideFlags.DontSave;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    DilateTransparentPixelColors(texture);
                    texture.Apply(true, true);
                    texture.filterMode = FilterMode.Trilinear;

                    Texture2D previous;
                    if (_iconTexturesById.TryGetValue(iconId, out previous) && previous != null)
                    {
                        UnityEngine.Object.Destroy(previous);
                    }
                    _iconTexturesById[iconId] = texture;
                    loaded++;
                }
                catch (Exception exception)
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                    Logger.LogWarning("Could not load quick-wheel icon " + path + ": " + exception.GetBaseException().Message);
                }
            }

            if (loaded > 0)
            {
                Logger.LogInfo(
                    "Loaded "
                    + loaded.ToString(CultureInfo.InvariantCulture)
                    + " quick-wheel icons for "
                    + sourceId
                    + " with trilinear filtering and runtime mipmaps.");
            }
            return loaded == iconIds.Length;
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
                _quickWheelPanelLayoutDirty = false;
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
            if (!_quickWheelPanelLayoutDirty)
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
            float columnWidth = state.PanelColumnWidth * scale;
            float columnGap = state.ColumnGap * scale;
            float rowHeight = 24.0f * scale;
            bool hasLeftResourceLine = state.LeftResourceTexts.Length > 0;
            int leftRowCount = state.LeftTexts.Length + (hasLeftResourceLine ? 1 : 0);
            const float leftSectionGapReference = 6.0f;
            int leftSummaryRowCount = Math.Max(0, Math.Min(state.LeftTexts.Length, state.LeftSummaryRowCount));
            bool hasLeftSectionGap = leftSummaryRowCount > 0 && leftSummaryRowCount < state.LeftTexts.Length;
            float leftSectionGap = hasLeftSectionGap ? leftSectionGapReference * scale : 0.0f;
            float backgroundPadding = state.PanelBackgroundPadding * scale;
            float backgroundTextureBleed = 10.0f * scale;
            float leftBackgroundHeight = (66.0f + leftRowCount * 24.0f) * scale;
            float rightBackgroundHeight = (66.0f + state.RightTexts.Length * 24.0f) * scale;
            float height = Math.Max(leftBackgroundHeight, rightBackgroundHeight);
            view.Root.anchorMin = Vector2.one;
            view.Root.anchorMax = Vector2.one;
            view.Root.pivot = Vector2.one;
            view.Root.anchoredPosition = new Vector2(-state.RightOffset, -state.TopOffset);
            view.Root.sizeDelta = new Vector2(columnWidth * 2.0f + columnGap, height);

            float rightX = columnWidth + columnGap;
            LayoutPanelBackground(
                view.LeftBackground,
                -backgroundPadding - backgroundTextureBleed,
                backgroundPadding + backgroundTextureBleed,
                columnWidth + (backgroundPadding + backgroundTextureBleed) * 2.0f,
                leftBackgroundHeight + (backgroundPadding + backgroundTextureBleed) * 2.0f,
                state.PanelBackgroundOpacity);
            LayoutPanelBackground(
                view.RightBackground,
                rightX - backgroundPadding - backgroundTextureBleed,
                backgroundPadding + backgroundTextureBleed,
                columnWidth + (backgroundPadding + backgroundTextureBleed) * 2.0f,
                rightBackgroundHeight + (backgroundPadding + backgroundTextureBleed) * 2.0f,
                state.PanelBackgroundOpacity);

            ConfigurePanelText(view.LeftTitle, state.LeftTitle, font, 18.0f * scale, TMPro.FontStyles.Bold);
            ConfigurePanelText(view.LeftSubtitle, state.LeftSubtitle, font, 14.0f * scale, TMPro.FontStyles.Normal);
            ConfigurePanelText(view.RightTitle, state.RightTitle, font, 18.0f * scale, TMPro.FontStyles.Bold);
            ConfigurePanelText(view.RightSubtitle, state.RightSubtitle, font, 14.0f * scale, TMPro.FontStyles.Normal);
            ConfigurePanelShadow(view.LeftTitle, state);
            ConfigurePanelShadow(view.LeftSubtitle, state);
            ConfigurePanelShadow(view.RightTitle, state);
            ConfigurePanelShadow(view.RightSubtitle, state);
            LayoutText(view.LeftTitle, 0.0f, 0.0f, columnWidth, 28.0f * scale);
            LayoutText(view.LeftSubtitle, 0.0f, -29.0f * scale, columnWidth, 25.0f * scale);
            LayoutText(view.RightTitle, rightX, 0.0f, columnWidth, 28.0f * scale);
            LayoutText(view.RightSubtitle, rightX, -29.0f * scale, columnWidth, 25.0f * scale);
            Color headerColor = ResolveStyleColor(state.HeaderColor, 1.0f);
            Color subheaderColor = ResolveStyleColor(state.SubheaderColor, 1.0f);
            float headerOutlineMultiplier = StyleEquals(state.HeaderColor, "White") ? state.WhiteTextOutlineStrengthMultiplier : 1.0f;
            float subheaderOutlineMultiplier = StyleEquals(state.SubheaderColor, "White") ? state.WhiteTextOutlineStrengthMultiplier : 1.0f;
            ConfigurePanelColor(view.LeftTitle, headerColor, headerOutlineMultiplier);
            ConfigurePanelColor(view.RightTitle, headerColor, headerOutlineMultiplier);
            ConfigurePanelColor(view.LeftSubtitle, subheaderColor, subheaderOutlineMultiplier);
            ConfigurePanelColor(view.RightSubtitle, subheaderColor, subheaderOutlineMultiplier);

            UpdatePanelRows(view.LeftRows, state.LeftTexts, state.LeftIconIds, state.LeftStyles, 0.0f, -53.0f * scale, columnWidth, rowHeight, scale, font, hasLeftResourceLine, leftSummaryRowCount, leftSectionGap);
            UpdateResourceLine(view, state, 0.0f, -77.0f * scale, columnWidth, rowHeight, scale, font);
            UpdatePanelRows(view.RightRows, state.RightTexts, state.RightIconIds, state.RightStyles, rightX, -59.0f * scale, columnWidth, rowHeight, scale, font, false, 0, 0.0f);
            view.Root.SetAsLastSibling();
            _quickWheelPanelLayoutDirty = false;
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
            RawImage leftBackground = CreatePanelBackground(
                root,
                "LeftBackground",
                EnsureQuickWheelPanelBackgroundTexture(false));
            RawImage rightBackground = CreatePanelBackground(
                root,
                "RightBackground",
                EnsureQuickWheelPanelBackgroundTexture(true));
            _quickWheelPanelView = new QuickWheelPanelView
            {
                Root = root,
                Group = rootObject.GetComponent<CanvasGroup>(),
                LeftBackground = leftBackground,
                RightBackground = rightBackground,
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
            FontAsset font,
            bool leaveResourceLineAfterFirstRow,
            int sectionBreakAfterRowCount,
            float sectionGap)
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
                int visualIndex = leaveResourceLineAfterFirstRow && i > 0 ? i + 1 : i;
                float sectionOffset = sectionGap > 0.0f && i >= sectionBreakAfterRowCount
                    ? sectionGap
                    : 0.0f;
                row.Root.anchoredPosition = new Vector2(x, startY - visualIndex * rowHeight - sectionOffset);
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
                ConfigurePanelShadow(row.Text, _quickWheelPanelState);
                ConfigurePanelColor(
                    row.Text,
                    color,
                    StyleEquals(style, "White") ? _quickWheelPanelState.WhiteTextOutlineStrengthMultiplier : 1.0f);
                LayoutText(row.Text, iconWidth, 0.0f, width - iconWidth, rowHeight);
            }
        }

        private void UpdateResourceLine(
            QuickWheelPanelView view,
            QuickWheelPanelState state,
            float x,
            float y,
            float width,
            float rowHeight,
            float scale,
            FontAsset font)
        {
            bool active = state.LeftResourceTexts.Length > 0;
            if (!active)
            {
                if (view.LeftResourceLine != null)
                {
                    view.LeftResourceLine.Root.gameObject.SetActive(false);
                }
                return;
            }

            if (view.LeftResourceLine == null)
            {
                view.LeftResourceLine = CreateResourceLine(view.Root);
            }
            QuickWheelPanelResourceLineView line = view.LeftResourceLine;
            line.Root.gameObject.SetActive(true);
            line.Root.anchorMin = new Vector2(0.0f, 1.0f);
            line.Root.anchorMax = new Vector2(0.0f, 1.0f);
            line.Root.pivot = new Vector2(0.0f, 1.0f);
            line.Root.anchoredPosition = new Vector2(x, y);
            line.Root.sizeDelta = new Vector2(width, rowHeight);

            int count = Math.Min(3, state.LeftResourceTexts.Length);
            while (line.Items.Count < count)
            {
                line.Items.Add(CreateResourceItem(line.Root, line.Items.Count));
            }

            float itemGap = 8.0f * scale;
            float availableWidth = Math.Max(1.0f, width - itemGap * Math.Max(0, count - 1));
            float[] desiredWidths = new float[count];
            float totalDesiredWidth = 0.0f;
            for (int i = 0; i < line.Items.Count; i++)
            {
                QuickWheelPanelResourceItemView item = line.Items[i];
                bool itemActive = i < count && !string.IsNullOrWhiteSpace(state.LeftResourceTexts[i]);
                item.Root.gameObject.SetActive(itemActive);
                if (!itemActive)
                {
                    continue;
                }

                string style = ValueAt(state.LeftResourceStyles, i, "White");
                Color color = ResolveStyleColor(style, 1.0f);
                Texture2D texture = GetIconTexture(ValueAt(state.LeftResourceIconIds, i, string.Empty));
                item.Icon.gameObject.SetActive(texture != null);
                float iconWidth = texture == null ? 0.0f : 19.0f * scale;
                if (texture != null)
                {
                    item.Icon.texture = texture;
                    item.Icon.color = ResolveIconColor(style, color, 1.0f);
                    item.Icon.rectTransform.anchorMin = new Vector2(0.0f, 0.5f);
                    item.Icon.rectTransform.anchorMax = new Vector2(0.0f, 0.5f);
                    item.Icon.rectTransform.pivot = new Vector2(0.0f, 0.5f);
                    item.Icon.rectTransform.anchoredPosition = Vector2.zero;
                    item.Icon.rectTransform.sizeDelta = new Vector2(15.0f * scale, 15.0f * scale);
                }

                ConfigurePanelText(item.Text, state.LeftResourceTexts[i], font, 13.0f * scale, TMPro.FontStyles.Normal);
                ConfigurePanelShadow(item.Text, state);
                ConfigurePanelColor(
                    item.Text,
                    color,
                    StyleEquals(style, "White") ? state.WhiteTextOutlineStrengthMultiplier : 1.0f);
                desiredWidths[i] = iconWidth + Math.Max(12.0f * scale, item.Text.preferredWidth);
                totalDesiredWidth += desiredWidths[i];
            }

            float cursor = 0.0f;
            for (int i = 0; i < count; i++)
            {
                QuickWheelPanelResourceItemView item = line.Items[i];
                if (!item.Root.gameObject.activeSelf)
                {
                    continue;
                }
                float itemWidth = totalDesiredWidth <= availableWidth
                    ? desiredWidths[i]
                    : availableWidth * desiredWidths[i] / Math.Max(1.0f, totalDesiredWidth);
                item.Root.anchorMin = new Vector2(0.0f, 1.0f);
                item.Root.anchorMax = new Vector2(0.0f, 1.0f);
                item.Root.pivot = new Vector2(0.0f, 1.0f);
                item.Root.anchoredPosition = new Vector2(cursor, 0.0f);
                item.Root.sizeDelta = new Vector2(itemWidth, rowHeight);
                float iconWidth = item.Icon.gameObject.activeSelf ? 19.0f * scale : 0.0f;
                LayoutText(item.Text, iconWidth, 0.0f, itemWidth - iconWidth, rowHeight);
                cursor += itemWidth + itemGap;
            }
        }

        private static QuickWheelPanelResourceLineView CreateResourceLine(RectTransform parent)
        {
            GameObject rootObject = new GameObject("LeftResourceLine", typeof(RectTransform));
            rootObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            return new QuickWheelPanelResourceLineView { Root = root };
        }

        private static QuickWheelPanelResourceItemView CreateResourceItem(RectTransform parent, int index)
        {
            GameObject rootObject = new GameObject("ResourceItem" + index, typeof(RectTransform));
            rootObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            return new QuickWheelPanelResourceItemView
            {
                Root = root,
                Icon = CreateNotificationIcon(root, "Icon"),
                Text = CreatePanelText(root, "Text")
            };
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
            text.gameObject.AddComponent<QuickWheelTextEffect>();
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RawImage CreatePanelBackground(
            RectTransform parent,
            string name,
            Texture2D texture)
        {
            RawImage background = CreateNotificationIcon(parent, name);
            background.texture = texture == null ? Texture2D.whiteTexture : texture;
            background.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            return background;
        }

        private Texture2D EnsureQuickWheelPanelBackgroundTexture(bool alternate)
        {
            Texture2D cached = alternate
                ? _quickWheelPanelRightBackgroundTexture
                : _quickWheelPanelLeftBackgroundTexture;
            if (cached != null)
            {
                return cached;
            }

            const int width = 128;
            const int height = 256;
            float seed = alternate ? 83.0f : 17.0f;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = alternate
                ? "GrailFloatingText-QuickWheel-Charcoal-Right"
                : "GrailFloatingText-QuickWheel-Charcoal-Left";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 0;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float broadNoise = Mathf.PerlinNoise(seed + x * 0.035f, seed * 0.61f + y * 0.024f);
                    float mediumNoise = Mathf.PerlinNoise(seed * 1.37f + x * 0.12f, seed * 0.43f + y * 0.085f);
                    float fineNoise = Mathf.PerlinNoise(seed * 2.11f + x * 0.43f, seed * 1.73f + y * 0.37f);
                    float leftInset = 2.5f + 3.5f * Mathf.PerlinNoise(seed + y * 0.055f, seed + 5.0f);
                    float rightInset = 2.5f + 3.5f * Mathf.PerlinNoise(seed + y * 0.047f, seed + 19.0f);
                    float bottomInset = 2.5f + 3.5f * Mathf.PerlinNoise(seed + x * 0.071f, seed + 31.0f);
                    float topInset = 2.5f + 3.5f * Mathf.PerlinNoise(seed + x * 0.063f, seed + 47.0f);
                    float horizontalInteriorDistance = Math.Min(
                        x - leftInset,
                        width - 1 - x - rightInset);
                    float verticalInteriorDistance = Math.Min(
                        y - bottomInset,
                        height - 1 - y - topInset);
                    const float cornerSoftness = 6.0f;
                    float cornerBlend = Mathf.Clamp01(
                        0.5f
                        + 0.5f * (verticalInteriorDistance - horizontalInteriorDistance) / cornerSoftness);
                    float interiorDistance = Mathf.Lerp(
                        verticalInteriorDistance,
                        horizontalInteriorDistance,
                        cornerBlend)
                        - cornerSoftness * cornerBlend * (1.0f - cornerBlend);
                    float edgeAlpha = Mathf.SmoothStep(
                        0.0f,
                        1.0f,
                        Mathf.Clamp01(interiorDistance / 5.5f));
                    float fibers = 0.5f + 0.5f * Mathf.Sin(y * 0.29f + mediumNoise * 4.0f);
                    float grain = Mathf.Clamp01(
                        0.72f
                        + broadNoise * 0.16f
                        + mediumNoise * 0.07f
                        + fineNoise * 0.035f
                        + fibers * 0.015f);
                    float surfaceAlpha = Mathf.Clamp01(
                        (0.86f + grain * 0.14f)
                        * (0.94f + broadNoise * 0.06f));
                    byte value = 255;
                    byte alpha = (byte)Mathf.RoundToInt(255.0f * edgeAlpha * surfaceAlpha);
                    pixels[y * width + x] = new Color32(value, value, value, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            if (alternate)
            {
                _quickWheelPanelRightBackgroundTexture = texture;
            }
            else
            {
                _quickWheelPanelLeftBackgroundTexture = texture;
            }
            return texture;
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

        private static void ConfigurePanelShadow(TextMeshProUGUI text, QuickWheelPanelState state)
        {
            if (text == null)
            {
                return;
            }

            QuickWheelTextEffect effect = text.GetComponent<QuickWheelTextEffect>();
            if (effect != null)
            {
                bool outlineEnabled = state != null
                    && state.TextOutlineEnabled
                    && state.TextOutlineWidth > 0.001f
                    && state.TextOutlineOpacity > 0.001f;
                Color outlineColor;
                if (!ColorUtility.TryParseHtmlString(
                    state == null ? "#18130D" : state.TextOutlineColor,
                    out outlineColor))
                {
                    ColorUtility.TryParseHtmlString("#18130D", out outlineColor);
                }
                outlineColor.a *= state == null ? 0.0f : state.TextOutlineOpacity;
                bool shadowEnabled = state != null
                    && state.TextShadowEnabled
                    && state.TextShadowOpacity > 0.001f;
                Color shadowColor = new Color(
                    0.0f,
                    0.0f,
                    0.0f,
                    state == null ? 0.0f : state.TextShadowOpacity);
                effect.Configure(
                    outlineEnabled,
                    outlineColor,
                    state == null ? 0.0f : state.TextOutlineWidth,
                    state == null ? 1 : state.TextOutlineStrength,
                    shadowEnabled,
                    shadowColor,
                    state == null ? 0.0f : state.TextShadowOffset,
                    state == null ? 0.0f : state.TextShadowSoftness,
                    state == null ? 1 : state.TextShadowStrength);
                effect.enabled = outlineEnabled || shadowEnabled;
            }
        }

        private static void ConfigurePanelColor(
            TextMeshProUGUI text,
            Color color,
            float outlineStrengthMultiplier)
        {
            if (text == null)
            {
                return;
            }

            QuickWheelTextEffect effect = text.GetComponent<QuickWheelTextEffect>();
            if (effect != null)
            {
                effect.SetOutlineStrengthMultiplier(outlineStrengthMultiplier);
                effect.SetFaceColor(color);
            }
            else
            {
                text.color = color;
            }
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

        private static void LayoutPanelBackground(
            RawImage background,
            float x,
            float y,
            float width,
            float height,
            float opacity)
        {
            RectTransform rect = background.rectTransform;
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(Math.Max(1.0f, width), Math.Max(1.0f, height));
            background.color = new Color(0.0f, 0.0f, 0.0f, Clamp01(opacity * 0.9f));
            background.gameObject.SetActive(opacity > 0.001f);
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
            _quickWheelPanelLayoutDirty = false;
            _quickWheelTooltipActive = false;
            _quickWheelPanelAlpha = 0.0f;
            _quickWheelPanelLastUpdateTime = 0.0f;
            if (_quickWheelPanelLeftBackgroundTexture != null)
            {
                Destroy(_quickWheelPanelLeftBackgroundTexture);
                _quickWheelPanelLeftBackgroundTexture = null;
            }
            if (_quickWheelPanelRightBackgroundTexture != null)
            {
                Destroy(_quickWheelPanelRightBackgroundTexture);
                _quickWheelPanelRightBackgroundTexture = null;
            }
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
            internal string[] LeftResourceTexts;
            internal string[] LeftResourceIconIds;
            internal string[] LeftResourceStyles;
            internal int LeftSummaryRowCount;
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
            internal float PanelColumnWidth;
            internal float ColumnGap;
            internal float PanelBackgroundOpacity;
            internal float PanelBackgroundPadding;
            internal bool TextShadowEnabled;
            internal float TextShadowOpacity;
            internal float TextShadowOffset;
            internal float TextShadowSoftness;
            internal int TextShadowStrength;
            internal bool TextOutlineEnabled;
            internal string TextOutlineColor;
            internal float TextOutlineOpacity;
            internal float TextOutlineWidth;
            internal int TextOutlineStrength;
            internal float WhiteTextOutlineStrengthMultiplier;
            internal string HeaderColor;
            internal string SubheaderColor;
        }

        private sealed class QuickWheelPanelView
        {
            internal RectTransform Root;
            internal CanvasGroup Group;
            internal RawImage LeftBackground;
            internal RawImage RightBackground;
            internal TextMeshProUGUI LeftTitle;
            internal TextMeshProUGUI LeftSubtitle;
            internal TextMeshProUGUI RightTitle;
            internal TextMeshProUGUI RightSubtitle;
            internal QuickWheelPanelResourceLineView LeftResourceLine;
            internal readonly List<QuickWheelPanelRowView> LeftRows = new List<QuickWheelPanelRowView>();
            internal readonly List<QuickWheelPanelRowView> RightRows = new List<QuickWheelPanelRowView>();
        }

        private sealed class QuickWheelPanelRowView
        {
            internal RectTransform Root;
            internal RawImage Icon;
            internal TextMeshProUGUI Text;
        }

        private sealed class QuickWheelPanelResourceLineView
        {
            internal RectTransform Root;
            internal readonly List<QuickWheelPanelResourceItemView> Items = new List<QuickWheelPanelResourceItemView>();
        }

        private sealed class QuickWheelPanelResourceItemView
        {
            internal RectTransform Root;
            internal RawImage Icon;
            internal TextMeshProUGUI Text;
        }
    }
}
