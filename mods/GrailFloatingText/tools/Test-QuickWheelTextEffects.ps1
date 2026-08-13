[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$gftSource = Get-Content -LiteralPath (Join-Path $modRoot "src\QuickWheelPanel.cs") -Raw
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$deedsSource = Get-Content -LiteralPath (Join-Path $repoRoot "mods\DeedsOfAvalon\src\DeedsOfAvalon.cs") -Raw

$gftContracts = @(
    'public const int ApiVersion = 15;',
    'int leftSummaryRowCount,',
    'LeftSummaryRowCount = Math.Max(0, Math.Min(',
    'float panelColumnWidth,',
    'float panelBackgroundOpacity,',
    'float panelBackgroundPadding,',
    'float columnGap,',
    'float textShadowSoftness,',
    'int textOutlineStrength,',
    'float whiteTextOutlineStrengthMultiplier,',
    'Math.Min(16.0f, textOutlineWidth)',
    'Math.Min(8, textOutlineStrength)',
    'internal sealed class QuickWheelTextEffect : BaseMeshEffect',
    'SupportsNativeSdf(_effectMaterial)',
    'ShaderUtilities.UpdateShaderRatios(_effectMaterial);',
    '_effectMaterial.EnableKeyword(UnderlayKeyword);',
    '_effectMaterial.SetFloat(UnderlaySoftnessId, _shadowSoftness);',
    '_text.UpdateMeshPadding();',
    '* _outlineStrengthMultiplier)',
    'StyleEquals(style, "White") ? _quickWheelPanelState.WhiteTextOutlineStrengthMultiplier : 1.0f',
    'StyleEquals(state.SubheaderColor, "White") ? state.WhiteTextOutlineStrengthMultiplier : 1.0f',
    'Math.Min(2.0f, whiteTextOutlineStrengthMultiplier)',
    'source.Count * (1 + (_outlineEnabled ? 4 : 0) + (_shadowEnabled ? 1 : 0))',
    'text.gameObject.AddComponent<QuickWheelTextEffect>()',
    'EnsureQuickWheelPanelBackgroundTexture(false));',
    'EnsureQuickWheelPanelBackgroundTexture(true));',
    'const int width = 128;',
    'const int height = 256;',
    'float seed = alternate ? 83.0f : 17.0f;',
    'const float cornerSoftness = 6.0f;',
    'const float leftSectionGapReference = 6.0f;',
    'i >= sectionBreakAfterRowCount',
    '- cornerSoftness * cornerBlend * (1.0f - cornerBlend);',
    'new Texture2D(width, height, TextureFormat.RGBA32, false)',
    'Mathf.PerlinNoise',
    'Mathf.Clamp01(interiorDistance / 5.5f)',
    'backgroundTextureBleed = 10.0f * scale;',
    'new Color(0.0f, 0.0f, 0.0f, Clamp01(opacity * 0.9f))',
    'byte value = 255;',
    'float surfaceAlpha = Mathf.Clamp01(',
    'if (!_quickWheelPanelLayoutDirty)',
    '_quickWheelPanelLayoutDirty = false;',
    'Destroy(_quickWheelPanelLeftBackgroundTexture);',
    'Destroy(_quickWheelPanelRightBackgroundTexture);',
    'state.PanelColumnWidth * scale',
    'state.ColumnGap * scale',
    'state.PanelBackgroundOpacity'
)
foreach ($contract in $gftContracts) {
    if ($gftSource.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing quick-wheel text-effect contract: $contract"
    }
}

if ($gftSource.IndexOf('outlineLayers * 4 + shadowLayers', [StringComparison]::Ordinal) -ge 0 -or
    $gftSource.IndexOf('for (int layer =', [StringComparison]::Ordinal) -ge 0 -or
    $gftSource.IndexOf('effect.Refresh()', [StringComparison]::Ordinal) -ge 0 -or
    $gftSource.IndexOf('_quickWheelPanelBackgroundTexture', [StringComparison]::Ordinal) -ge 0) {
    throw "Quick-wheel text still rebuilds layered copied geometry or refreshes it every frame."
}

$deedsContracts = @(
    '"TextOutlineWidth", 5.0f',
    'new AcceptableValueRange<float>(0.0f, 16.0f)',
    '"TextOutlineStrength", 2',
    'new AcceptableValueRange<int>(1, 8)',
    'private const float WhiteTextOutlineStrengthMultiplier = 1.1f;',
    'new AcceptableValueRange<float>(0.5f, 2.0f)',
    '"PanelColumnWidth", 190.0f',
    '"ColumnGap", 30.0f',
    '"PanelBackgroundOpacity", 0.95f',
    '"PanelBackgroundPadding", 16.0f',
    '"TextShadowOpacity", 1.0f',
    '"TextShadowOffset", 4.0f',
    '"TextShadowSoftness", 0.5f',
    '"TextShadowStrength", 8',
    'args.Add(_textOutlineStrength.Value);',
    'args.Add(WhiteTextOutlineStrengthMultiplier);',
    'args.Add(_textShadowSoftness.Value);',
    '_panelColumnWidth.Value,',
    '_columnGap.Value,',
    '_panelBackgroundOpacity.Value,',
    '_panelBackgroundPadding.Value',
    'leftSummaryRowCount,',
    'GetRawConstantValue() < 15',
    'GetParameters().Length != 38'
)
foreach ($contract in $deedsContracts) {
    if ($deedsSource.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Deeds outline contract: $contract"
    }
}

if ($deedsSource.IndexOf('"WhiteTextOutlineStrengthMultiplier"', [StringComparison]::Ordinal) -ge 0) {
    throw "Deeds still exposes WhiteTextOutlineStrengthMultiplier as a user-facing config setting."
}

Write-Output "Quick-wheel native SDF outline, underlay, bounded fallback, and expanded-range contracts passed."
