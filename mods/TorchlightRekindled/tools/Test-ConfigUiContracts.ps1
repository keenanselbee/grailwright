[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\TorchlightRekindled.cs"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Missing source file: $sourcePath"
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$expectedLabels = @(
    'ConfigUi("General", "Enabled", 0, 0)',
    'ConfigUi("General", "Interior Brightness Preset", 0, 10)',
    'ConfigUi("General", "Exterior Brightness Preset", 0, 20)',
    'ConfigUi("Torch Light", "Range Bonus (m)", 10, 0)',
    'ConfigUi("Torch Light", "World Illumination", 10, 10)',
    'ConfigUi("Torch Light", "Flicker Amount", 10, 20)',
    'ConfigUi("Torch Light", "Flicker Speed", 10, 30)',
    'ConfigUi("Visible Flame", "Visible Flame Brightness", 20, 0)',
    'ConfigUi("Visible Flame", "Flame Bloom Strength", 20, 10)',
    'ConfigUi("Flame Halo", "Halo Strength", 30, 0)',
    'ConfigUi("Flame Halo", "Halo Size (m)", 30, 10)',
    'ConfigUi("Flame Halo", "Halo Height Scale", 30, 20)',
    'ConfigUi("Flame Halo", "Vertical Offset (scaled height)", 30, 30)',
    'ConfigUi("Flame Halo", "Torch-Local Side Offset (width)", 30, 40)',
    'ConfigUi("Halo Alignment - Advanced", "Axis Pitch Offset (deg)", 40, 0)',
    'ConfigUi("Halo Alignment - Advanced", "Axis Yaw Offset (deg)", 40, 10)',
    'ConfigUi("Halo Alignment - Advanced", "Screen Roll Offset (deg)", 40, 20)',
    'ConfigUi("Halo Alignment - Advanced", "Light Parry Roll Offset (deg)", 40, 30)',
    'ConfigUi("Interior Bloom", "Enabled", 50, 0)',
    'ConfigUi("Interior Bloom", "Only While Torch Equipped", 50, 10)',
    'ConfigUi("Interior Bloom", "Threshold", 50, 20)',
    'ConfigUi("Interior Bloom", "Intensity", 50, 30)',
    'ConfigUi("Interior Bloom", "Scatter", 50, 40)',
    'ConfigUi("Audio", "Enabled", 60, 0)',
    'ConfigUi("Audio", "Volume", 60, 10)',
    'ConfigUi("Diagnostics", "Diagnostics", 70, 0)'
)

$metadataCallCount = ([regex]::Matches($source, 'ConfigUi\("')).Count
if ($metadataCallCount -ne $expectedLabels.Count) {
    throw "Expected $($expectedLabels.Count) visible config metadata calls, found $metadataCallCount."
}

foreach ($label in $expectedLabels) {
    if (-not $source.Contains($label)) {
        throw "Missing config UI metadata: $label"
    }
}

if (-not $source.Contains('private const int ConfigSchemaVersion = 11;')) {
    throw "Expected config schema 11."
}
if ($source -notmatch '"InteriorBrightnessPreset",\s+TorchBrightnessPreset\.Bright,') {
    throw "InteriorBrightnessPreset must default to Bright."
}
if ($source -notmatch '"ExteriorBrightnessPreset",\s+TorchBrightnessPreset\.Vanilla,') {
    throw "ExteriorBrightnessPreset must default to Vanilla."
}
if (-not $source.Contains('CurrentBrightnessPreset == TorchBrightnessPreset.Vanilla')) {
    throw "Vanilla must select the half-brightness balance."
}
if (-not $source.Contains('bool brightnessContextChanged = !_sceneContextKnown')) {
    throw "Scene brightness changes must use cached context transitions."
}
if ($source -notmatch '"RangeBonusMeters",\s+20f,') {
    throw "RangeBonusMeters must default to 20 literal metres."
}
if (-not $source.Contains('new AcceptableValueRange<float>(0f, 70f)')) {
    throw "RangeBonusMeters must accept 0 to 70 literal metres."
}
if ($source -notmatch '"FlameHaloVerticalOffset",\s+0\.45f,') {
    throw "FlameHaloVerticalOffset must default to the tested 0.45 position."
}
if ($source -notmatch '"FlameHaloHorizontalOffset",\s+-0\.12f,') {
    throw "FlameHaloHorizontalOffset must default to the tested -0.12 position."
}
if ($source.Contains('10f) * (20f / 3f)')) {
    throw "Legacy compact range amplification is still present."
}
if (-not $source.Contains('"FlameHaloBashRotationOffsetDegrees"')) {
    throw "The legacy stored parry key must remain for config compatibility."
}
if (-not $source.Contains('FlameHaloLightParryRotationOffsetDegrees')) {
    throw "The internal light-parry terminology is missing."
}

Write-Host "Torchlight Rekindled config UI contracts passed."
