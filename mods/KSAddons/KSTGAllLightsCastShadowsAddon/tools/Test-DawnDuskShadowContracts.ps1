param(
    [string]$RepositoryRoot = (
        Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
    ).Path
)

$ErrorActionPreference = 'Stop'

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Dawn/dusk shadow contract failed: $Message"
    }
}

$modRoot = Join-Path $RepositoryRoot `
    'mods\KSAddons\KSTGAllLightsCastShadowsAddon'
$plugin = Get-Content -LiteralPath (
    Join-Path $modRoot 'src\TGAllLightsCastShadowsAddon.cs'
) -Raw
$controller = Get-Content -LiteralPath (
    Join-Path $modRoot 'src\DawnDuskShadowController.cs'
) -Raw
$managedController = Get-Content -LiteralPath (
    Join-Path $modRoot 'src\ManagedShadowController.cs'
) -Raw
$rules = Get-Content -LiteralPath (
    Join-Path $modRoot 'src\SafeShadowSelectionRules.cs'
) -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot 'mod.json'
) -Raw

Assert-Contract ($manifest.Contains('"src/DawnDuskShadowController.cs"')) `
    'the directional-shadow controller is not compiled.'
Assert-Contract ($plugin.Contains(
    'EyesInTheDarkPluginGuid =') -and
    $plugin.Contains('"ks.tgfoa.eyes-in-the-dark"') -and
    $plugin.Contains('BepInDependency(EyesInTheDarkPluginGuid')) `
    'Eyes in the Dark soft integration is missing.'
Assert-Contract ($controller.Contains('"ImproveDawnDuskShadows"') -and
    $controller.Contains('false,') -and
    $controller.Contains('"ShadowBlendMinutes"') -and
    $controller.Contains('"NormalizeForEyesInTheDark"') -and
    $controller.Contains('"EyesBlendSecondsPerSide"')) `
    'optional directional-shadow settings or safe defaults are missing.'
Assert-Contract ($controller.Contains(
    '"shadowIntensityBlendMinutes"') -and
    $controller.Contains('GameDayNightSystem')) `
    'the existing DayNightSystem shadow blend is not the only target.'
Assert-Contract ($controller.Contains('WeatherSecondsPerRealSecond') -and
    $rules.Contains('ResolveDawnDuskBlendMinutes') -and
    $rules.Contains('targetRealSeconds * weatherSecondsPerRealSecond / 60f')) `
    'Eyes real-time normalization does not use the live world-clock rate.'
Assert-Contract ($controller.Contains(
    'FindObjectsByType<GameDayNightSystem>') -and
    $controller.Contains('CalculateLoadedDawnDuskSceneSignature') -and
    $controller.Contains('_hasDawnDuskSceneSnapshot')) `
    'scene-bounded DayNightSystem discovery or caching is missing.'
Assert-Contract ($controller.Contains(
    'current != state.LastAppliedBlendMinutes') -and
    $controller.Contains('state.OriginalBlendMinutes') -and
    $controller.Contains('ownership was released')) `
    'compare-before-restore ownership safety is missing.'
Assert-Contract ($plugin.Contains('UpdateDawnDuskShadows();') -and
    $plugin.Contains('RestoreAllDawnDuskShadowSystems("addon unload")') -and
    $managedController.Contains('BeforeDawnDuskSceneTransition();')) `
    'runtime updates, unload restoration, or scene restoration are missing.'
Assert-Contract (-not $controller.Contains('Time.timeScale') -and
    -not $controller.Contains('LightType.Directional') -and
    -not $controller.Contains('EnableShadows')) `
    'the feature must not change gameplay time or create/enable directional shadows.'

Write-Host 'Dawn/dusk shadow contracts passed.'
