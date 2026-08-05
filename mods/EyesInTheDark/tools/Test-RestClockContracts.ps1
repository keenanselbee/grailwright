[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$overlay = Get-Content -LiteralPath (
    Join-Path $modRoot "src\RestClockOverlay.cs") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw

function Assert-RestClockContract {
    param([bool]$Condition, [string]$Message)
    if (!$Condition) {
        throw "Eyes rest-clock contract failed: $Message"
    }
}

foreach ($required in @(
    'src/RestClockOverlay.cs',
    'UnityEngine.TextRenderingModule.dll',
    '"version": "1.1.0"')) {
    Assert-RestClockContract ($manifest.Contains($required)) "manifest omits $required"
}

foreach ($required in @(
    'PatchRestClock();',
    'PatchQuickWeatherTime();',
    'typeof(VCQuickWeatherTime)',
    'FormatQuickWeatherTime(',
    '== RestClockLabelFormat.TwentyFourHour',
    'hour < 12 ? " AM" : " PM"',
    'typeof(VRestPopupUI)',
    '"OnInitialize"',
    '"Refresh"',
    '"SetHourChangeBasedOnAngle"',
    'RestClockOverlay.Attach(',
    'RestClockOverlay.RefreshAfterNative(',
    'RestClockOverlay.UsesNoonAtTop(',
    'RestClockOverlay.Detach(',
    'OwnRestMenu',
    'nameof(RestClockPatch.AfterRefresh)',
    'nameof(RestClockPatch.BeforeSetHourChangeBasedOnAngle)',
    'angle += 180f;',
    'RestClockLabelFormat.TwelveHour',
    '"RestClockLabelFormat"',
    'TwelveHour=12 Hour (AM/PM);TwentyFourHour=24 Hour',
    'the native clock remains usable')) {
    Assert-RestClockContract ($plugin.Contains($required)) "plugin omits $required"
}

foreach ($required in @(
    'internal enum RestClockLabelFormat',
    'private const float IconRadius = 52f;',
    'TwelveHour',
    'TwentyFourHour',
    '(hour + 12f) / 24f',
    'RefreshAfterNative(',
    'UsesNoonAtTop(',
    'Detach(',
    'RestoreNativePresentation()',
    'RotateHalfTurn(_arm);',
    'RotateHalfTurn(_fill);',
    '"currentTimeValueText"',
    '"restingTimeUntilValueText"',
    'hour < 12 ? " AM" : " PM"',
    '"Content/Clock"',
    '"FillParent/Fill"',
    'CreateHourLabel(root, textTemplate, "12 AM", 0f',
    'CreateHourLabel(root, textTemplate, "6 AM", 6f',
    'CreateHourLabel(root, textTemplate, "12 PM", 12f',
    'CreateHourLabel(root, textTemplate, "6 PM", 18f',
    'CreateHourLabel(root, textTemplate, "00", 0f',
    'CreateHourLabel(root, textTemplate, "06", 6f',
    'CreateHourLabel(root, textTemplate, "12", 12f',
    'CreateHourLabel(root, textTemplate, "18", 18f',
    'MovePhaseIcon(moon, root.anchoredPosition, 0f, IconRadius);',
    'MovePhaseIcon(sun, root.anchoredPosition, 12f, IconRadius);',
    'halfCircle.gameObject.SetActive(false);')) {
    Assert-RestClockContract ($overlay.Contains($required)) "overlay omits $required"
}

foreach ($removed in @(
    'WYRDNIGHT',
    'WyrdnightArc',
    'WyrdnightStart',
    'WyrdnightEnd',
    'MaskableGraphic',
    'wyrdColor')) {
    Assert-RestClockContract (!$overlay.Contains($removed)) "retired colored clock element remains: $removed"
}

Assert-RestClockContract (!$plugin.Contains('CurrentRestClockColor()')) "palette-dependent rest-clock color remains"
Assert-RestClockContract (!$plugin.Contains('"HandleClockArmSetupForMouse"')) "native mouse input is patched"

Write-Host "Eyes in the Dark rest-clock contracts passed."
