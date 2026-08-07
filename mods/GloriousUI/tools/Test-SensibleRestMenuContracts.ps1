[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GloriousUI.cs") -Raw
$restMenu = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SensibleRestMenu.cs") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw
$eyesSource = Get-Content -LiteralPath (
    Join-Path (Split-Path -Parent $modRoot) "EyesInTheDark\src\EyesInTheDark.cs") -Raw

function Assert-SensibleRestMenuContract {
    param([bool]$Condition, [string]$Message)
    if (!$Condition) {
        throw "Glorious UI Sensible Rest Menu contract failed: $Message"
    }
}

foreach ($required in @(
    'src/SensibleRestMenu.cs',
    'UnityEngine.TextRenderingModule.dll',
    '"version": "1.7.3"')) {
    Assert-SensibleRestMenuContract ($manifest.Contains($required)) "manifest omits $required"
}

foreach ($required in @(
    '"EnableSensibleRestMenu"',
    '"RestTimeDisplayFormat"',
    '"FormatQuickMenuTime"',
    'RestTimeDisplayFormat.TwelveHour',
    'TwelveHour=12 Hour (AM/PM);TwentyFourHour=24 Hour',
    'new SensibleRestMenuController(',
    'if (requiredPatched && _sensibleRestMenu != null)',
    '_sensibleRestMenu.Patch(_harmony);',
    '_sensibleRestMenu.Release();')) {
    Assert-SensibleRestMenuContract ($plugin.Contains($required)) "plugin omits $required"
}

foreach ($required in @(
    'internal enum RestTimeDisplayFormat',
    'typeof(VRestPopupUI)',
    'typeof(VCQuickWeatherTime)',
    '"OnInitialize"',
    '"Refresh"',
    '"SetHourChangeBasedOnAngle"',
    'nameof(RestClockPatch.BeforeSetHourChangeBasedOnAngle)',
    'angle += 180f;',
    'RestClockOverlay.Attach(',
    'RestClockOverlay.RefreshAfterNative(',
    'RestClockOverlay.DetachAll();',
    'RestoreNativePresentation()',
    '"gameWeatherTimeText"',
    'hour < 12 ? " AM" : " PM"',
    '"GloriousUI_RestClock"',
    '(hour + 12f) / 24f',
    'MovePhaseIcon(moon, root.anchoredPosition, 0f, IconRadius);',
    'MovePhaseIcon(sun, root.anchoredPosition, 12f, IconRadius);')) {
    Assert-SensibleRestMenuContract ($restMenu.Contains($required)) "runtime omits $required"
}

foreach ($retired in @(
    'Chainloader.PluginInfos',
    'QuickWeatherTimeSnapshot',
    '_quickWeatherTimeSnapshots')) {
    Assert-SensibleRestMenuContract (!$restMenu.Contains($retired)) "runtime retains $retired"
}

$requiredPatchIndex = $plugin.IndexOf('bool requiredPatched = PatchMethod(')
$restMenuPatchIndex = $plugin.IndexOf('_sensibleRestMenu.Patch(_harmony);')
Assert-SensibleRestMenuContract (
    $requiredPatchIndex -ge 0 -and $restMenuPatchIndex -gt $requiredPatchIndex) (
    "Sensible Rest Menu patches before the required Glorious UI hook succeeds")

foreach ($retired in @(
    'PatchRestClock();',
    'PatchQuickWeatherTime();',
    'RestClockLabelFormat',
    'OwnRestMenu',
    'VCQuickWeatherTime',
    'VRestPopupUI')) {
    Assert-SensibleRestMenuContract (!$eyesSource.Contains($retired)) "Eyes still owns $retired"
}

Assert-SensibleRestMenuContract (
    $eyesSource.Contains('"ShowWyrdnightRestAvailability"')) (
    "Eyes no longer exposes its Wyrdnight REST-button setting")
Assert-SensibleRestMenuContract (
    $eyesSource.Contains('nameof(HeroDevelopment.CanRest)')) (
    "Eyes no longer patches native rest availability")
Assert-SensibleRestMenuContract (
    !$restMenu.Contains('rect.rotation *= Quaternion.Euler')) (
    "rest-clock rotation accumulates across refreshes")

Write-Host "Glorious UI Sensible Rest Menu contracts passed."
