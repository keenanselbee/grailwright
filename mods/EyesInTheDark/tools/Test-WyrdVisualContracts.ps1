[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$plugin = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$visual = Get-Content -LiteralPath (
    Join-Path $modRoot "src\WyrdVisualRuntime.cs") -Raw
$boundary = Get-Content -LiteralPath (
    Join-Path $modRoot "src\BoundaryController.cs") -Raw
$layeredBoundary = Get-Content -LiteralPath (
    Join-Path $modRoot "src\LayeredBoundaryPass.cs") -Raw
$meter = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatMeter.cs") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw

function Assert-VisualContract {
    param([bool]$Condition, [string]$Message)
    if (!$Condition) {
        throw "Eyes Wyrd visual contract failed: $Message"
    }
}

foreach ($required in @(
    'src/WyrdVisualRuntime.cs')) {
    Assert-VisualContract ($manifest.Contains($required)) "manifest omits $required"
}

foreach ($required in @(
    'private const int ConfigSchemaVersion = 22;',
    'DefaultMinimumWorldThreatBrightnessScale = 0.8f;',
    'DefaultMaximumWorldThreatBrightnessScale = 1.2f;',
    'DefaultWyrdnightBrightness = 1.0f;',
    'DefaultThreatVisualSmoothingSeconds = 2.0f;',
    'DefaultThreatMeterBrightness = 1.0f;',
    'DefaultWorldThreatTargetColor = "#FF3028";',
    'DefaultMaximumWorldThreatColorShift = 0.8f;',
    'DefaultMoonSurfaceColor = "#3200FF";',
    'DefaultMoonSurfaceTintStrength = 0.75f;',
    'DefaultMoonSurfaceIntensity = 2.0f;',
    'DefaultMoonCoronaColor = "#8000FF";',
    'DefaultMoonCoronaIntensity = 2.0f;',
    'DefaultMoonlightColor = "#7E47FF";',
    'DefaultMoonlightTintStrength = 0.9f;',
    'DefaultNightSkyAmbientColor = "#401C63";',
    'DefaultNightSkyAmbientTintStrength = 1.0f;',
    'DefaultProtectionBubbleColor = "#B050FF";',
    'DefaultWyrdVisualTransitionSeconds = 60.0f;',
    '"EnableWyrdnightVisuals"',
    '"WyrdVisualTransitionSeconds"',
    '"WyrdnessPalette"',
    '"WyrdnightBrightness"',
    '"PurpleThreatMeterColor"',
    '"OrangeThreatMeterColor"',
    '"PurpleThreatMeterRedColor"',
    '"OrangeThreatMeterRedColor"',
    '"PurpleThreatMeterBrightness"',
    '"OrangeThreatMeterBrightness"',
    '"ThreatVisualSmoothingSeconds"',
    '"MinimumWorldThreatBrightnessScale"',
    '"MaximumWorldThreatBrightnessScale"',
    '"WorldThreatTargetColor"',
    '"MaximumWorldThreatColorShift"')) {
    Assert-VisualContract ($plugin.Contains($required)) "plugin omits $required"
}

foreach ($removed in @(
    'BoundaryThreatReactivity',
    'MinimumThreatIntensityMultiplier',
    'MaximumThreatIntensityMultiplier',
    'MaximumThreatThicknessMultiplier')) {
    Assert-VisualContract (!$plugin.Contains($removed)) "removed boundary threat setting remains: $removed"
    Assert-VisualContract (!$boundary.Contains($removed)) "boundary runtime retains: $removed"
}

foreach ($removedConfigKey in @(
    '"MinimumThreatVisualScale"',
    '"MaximumThreatVisualScale"',
    '"ThreatRedColor"',
    '"MaximumThreatRedBlend"')) {
    Assert-VisualContract (!$plugin.Contains($removedConfigKey)) "retired shared world/meter setting remains: $removedConfigKey"
}

foreach ($required in @(
    'MinimumWorldThreatBrightnessScale',
    'MaximumWorldThreatBrightnessScale',
    'WorldThreatTargetColor',
    'MaximumWorldThreatColorShift')) {
    Assert-VisualContract ($visual.Contains($required)) "world visual runtime omits $required"
}

foreach ($required in @(
    'DayNightSystemTypeName',
    'WyrdnightSphereRepellerTypeName',
    'MoonSurfaceColor',
    'MoonCoronaColor',
    'MoonlightColor',
    'NightSkyAmbientColor',
    'ProtectionBubbleColor',
    'WyrdVisualMath.ThreatScale(',
    'WyrdVisualMath.ShiftTowardRed(',
    'WyrdVisualMath.AdvanceBlend(',
    'public static float CenteredDuskBlend(',
    'TransitionSeconds',
    'DayNightSystemHandleExposurePostfix',
    'ApplyWyrdnightBrightness(',
    'settings.WyrdnightBrightness',
    '_lastReportedWyrdnightBrightness',
    'return brightness * 1.75f;',
    'settings.WyrdnightBrightness',
    '2f) * 0.35f;',
    'exposurePostfix.after',
    '"owrocc.DayNightLightTweaks"',
    'exposure.compensation.value * multiplier + compensation;',
    'exposure.fixedExposure.value * multiplier - compensation;',
    'public static float SmoothThreat(',
    'activeSeconds / halfLifeSeconds',
    'settings.ThreatSmoothingHalfLifeSeconds',
    'WyrdVisualMath.ScaleRgbLinear(',
    'BeginLoadThreatTransition(',
    '_loadThreatTransitionActive',
    '_visualBlend',
    '_transitioning',
    'WyrdnessPalette.NativeOrange',
    'DynamicGI.UpdateEnvironment();',
    'EnvironmentRefreshIntervalSeconds = 0.25f;',
    'FlushEnvironmentRefresh(false);',
    '_parsedColors',
    'CalculationVersion',
    'ReapplyDayNightState(',
    'if (!_active)',
    'RestoreDayNightSystems();',
    'RestoreProtectionBubbles();')) {
    Assert-VisualContract ($visual.Contains($required)) "integrated runtime omits $required"
}

$twoSecondBlend = 1.0 - [Math]::Pow(0.5, 2.0 / 2.0)
$sixSecondBlend = 1.0 - [Math]::Pow(0.5, 6.0 / 2.0)
Assert-VisualContract ([Math]::Abs($twoSecondBlend - 0.5) -lt 0.000001) `
    "default threat smoothing does not apply half a jump after two seconds"
Assert-VisualContract ([Math]::Abs($sixSecondBlend - 0.875) -lt 0.000001) `
    "default threat smoothing does not settle to 87.5 percent after six seconds"
Assert-VisualContract ($plugin.Contains('_threat.Value,`r`n                    settings);') -or $plugin.Contains("_threat.Value,`n                    settings);")) `
    "authoritative gameplay threat is no longer passed separately into visuals"

foreach ($required in @(
    'beginNaturalTransition',
    'canContinueTransition',
    'WorldTimescalePolicy.RemainingNightRealSeconds(',
    'WorldTimescalePolicy.RemainingDaylightRealSeconds(',
    'WorldTimescalePolicy.ElapsedNightRealSeconds(',
    'WyrdVisualMath.PreDawnBlendLimit(',
    'WyrdVisualMath.CenteredDuskBlend(',
    'phaseBlendLimit',
    'LoadThreatVisualTransitionSeconds',
    'IsStableExteriorVisualPhase(',
    'CurrentVisualIntent(',
    'IsKnownValidWyrdNightForVisuals(',
    'IsKnownDaylightForVisuals(',
    'PrimeWyrdVisualsDuringTransientLoad(',
    'TrySampleImmediateVisualState(',
    '_visualLoadContinuityPending',
    '_wyrdVisuals.TargetActive',
    '_wyrdVisuals.Prime(')) {
    Assert-VisualContract ($plugin.Contains($required)) "plugin omits transition ownership token $required"
}

Assert-VisualContract ($plugin.Contains('!observation.IsResting')) "gameplay Wyrdnight validity no longer excludes rest"
$visualNightStart = $plugin.IndexOf('private static bool IsKnownValidWyrdNightForVisuals(', [StringComparison]::Ordinal)
$visualDayStart = $plugin.IndexOf('private static bool IsKnownDaylight(', $visualNightStart, [StringComparison]::Ordinal)
Assert-VisualContract ($visualNightStart -ge 0 -and $visualDayStart -gt $visualNightStart) "visual Wyrdnight predicate was not found"
$visualNightMethod = $plugin.Substring($visualNightStart, $visualDayStart - $visualNightStart)
Assert-VisualContract (!$visualNightMethod.Contains('observation.IsResting')) "rest still disables Wyrdnight visuals"

foreach ($required in @(
    'public bool TargetActive',
    'public void Prime(',
    'ApplyDayNightSystem(state.Component);')) {
    Assert-VisualContract ($visual.Contains($required)) "visual continuity runtime omits $required"
}

$skyStart = $visual.IndexOf('private void ApplySky(', [StringComparison]::Ordinal)
$skyEnd = $visual.IndexOf('private Color PaletteColor(', $skyStart, [StringComparison]::Ordinal)
Assert-VisualContract ($skyStart -ge 0 -and $skyEnd -gt $skyStart) "night-sky method was not found"
$skyMethod = $visual.Substring($skyStart, $skyEnd - $skyStart)
Assert-VisualContract (!$skyMethod.Contains('ShiftTowardRed(')) "night sky incorrectly shifts toward red"
Assert-VisualContract (!$visual.Contains('SkyEmissionMultiplierId')) "Eyes still owns original sky emission"
Assert-VisualContract (!$visual.Contains('MoonlightTintStrength * scale')) "threat still weakens the configured moonlight tint"
Assert-VisualContract (!$visual.Contains('NightSkyAmbientTintStrength * scale')) "threat still weakens the configured sky tint"
Assert-VisualContract (!$visual.Contains('MoonSurfaceTintStrength * scale')) "threat still weakens the configured moon-surface tint"
Assert-VisualContract (!$plugin.Contains('PurpleWyrdnessBrightness')) "retired Purple brightness setting remains in the plugin"
Assert-VisualContract (!$visual.Contains('PurpleWyrdnessBrightness')) "retired Purple brightness plumbing remains in the visual runtime"
foreach ($retired in @(
    'PurpleExposureMultiplier',
    'PurpleExposureCompensation',
    'PurpleIndirectDiffuseMultiplier')) {
    Assert-VisualContract (!$plugin.Contains($retired)) "retired config setting remains in the plugin: $retired"
    Assert-VisualContract (!$visual.Contains($retired)) "retired visual plumbing remains: $retired"
}
Assert-VisualContract (!$visual.Contains('HandleIndirectLighting')) "Eyes still patches native indirect lighting"
Assert-VisualContract (!$visual.Contains('indirectDiffuseLightingMultiplier')) "Eyes still writes native indirect diffuse lighting"
Assert-VisualContract (!$visual.Contains('postExposure')) "Eyes still modifies HDRP post-exposure"
Assert-VisualContract (!$visual.Contains('GammaSetting')) "Eyes still modifies HDRP gamma"
Assert-VisualContract (!$visual.Contains('VolumeProfile')) "Eyes still owns a global volume profile"
Assert-VisualContract ($visual.Contains('Shader.PropertyToID("_SkyTint")')) "night sky does not use the full-sky tint"
Assert-VisualContract (!$visual.Contains('Shader.PropertyToID("_NightSkyTint")')) "narrow night-texture tint remains"

foreach ($required in @(
    'Mathf.Max(0f, brightness)',
    'private const float BrightnessScale = 3.0f;',
    'DefaultPurpleColorText = "#8032FF";',
    'DefaultOrangeColorText = "#FFB87A";',
    'WyrdVisualMath.ShiftTowardRed(',
    'WyrdVisualMath.ThreatScale(')) {
    Assert-VisualContract ($meter.Contains($required)) "threat meter omits $required"
}
foreach ($required in @(
    'ThreatVisualScale',
    'WyrdVisualMath.ShiftTowardRed(',
    'WyrdnessPalette.NativeOrange',
    '_layeredPass.enabled = nativeIntensity > 0.0001f;')) {
    Assert-VisualContract ($boundary.Contains($required)) "boundary does not use unified response: $required"
}

Assert-VisualContract (
    ([regex]::Matches($visual, 'DynamicGI\.UpdateEnvironment\(\);')).Count -eq 1) `
    "environment lighting has more than one refresh call site"
Assert-VisualContract (
    $layeredBoundary.Contains('_nativeIntensity <= 0.0001f')) `
    "inactive layered boundary still reaches the fullscreen draw path"

Assert-VisualContract (!(Test-Path -LiteralPath (
    Join-Path $repoRoot 'mods\PurpleMoonTest'))) "standalone PurpleMoonTest package remains"

Write-Host "Eyes in the Dark Wyrd visual contracts passed."
