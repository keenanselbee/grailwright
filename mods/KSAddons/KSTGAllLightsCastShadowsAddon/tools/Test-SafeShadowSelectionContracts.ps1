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
        throw "Safe shadow selection contract failed: $Message"
    }
}

$modRoot = Join-Path $RepositoryRoot 'mods\KSAddons\KSTGAllLightsCastShadowsAddon'
$pluginPath = Join-Path $modRoot 'src\TGAllLightsCastShadowsAddon.cs'
$controllerPath = Join-Path $modRoot 'src\ManagedShadowController.cs'
$rulesPath = Join-Path $modRoot 'src\SafeShadowSelectionRules.cs'
$manifestPath = Join-Path $modRoot 'mod.json'

$plugin = Get-Content -LiteralPath $pluginPath -Raw
$controller = Get-Content -LiteralPath $controllerPath -Raw
$rules = Get-Content -LiteralPath $rulesPath -Raw
$manifest = Get-Content -LiteralPath $manifestPath -Raw

Assert-Contract ($manifest.Contains('"src/ManagedShadowController.cs"')) `
    'mod.json does not compile the managed controller.'
Assert-Contract ($plugin.Contains('return BeforeManagedParentApply(reason);')) `
    'the parent ApplyAllLights prefix does not delegate to addon-owned selection.'
Assert-Contract ($controller.Contains('return false;')) `
    'the managed parent prefix does not suppress the legacy scan.'
Assert-Contract ($controller.Contains('"UseSafeSelectionController"') -and
    $controller.Contains('"MaximumUpgradedLights"') -and
    $controller.Contains('"MaximumDistanceMeters"') -and
    $controller.Contains('"MaximumShadowMapFaces"')) `
    'permanent count, distance, or shadow-map-face limits are missing.'
Assert-Contract ($rules.Contains('return isPointLight ? 6 : 1;')) `
    'point and spot shadow-map face costs are not explicit.'
Assert-Contract ($controller.Contains('"WyrdSight.Glow.ItemGlowEffect"') -and
    $controller.Contains('"WyrdLight"')) `
    'Wyrd Sight exclusion is not semantically guarded.'
Assert-Contract ($controller.Contains('NpcController') -and
    $controller.Contains('VHeroRenderer') -and
    $controller.Contains('VItemRenderer') -and
    $controller.Contains('VObjectCloseup') -and
    $controller.Contains('VLockpicking3D') -and
    $controller.Contains('VSpawnedLocation')) `
    'one or more semantic non-world-light exclusions are missing.'
Assert-Contract ($plugin.Contains('MageLightPluginGuid = "Gotik0.magelight"') -and
    $plugin.Contains('NoPlayerLightPluginGuid = "ks.tgfoa.no-player-light"') -and
    $plugin.Contains('BepInDependency(MageLightPluginGuid') -and
    $plugin.Contains('BepInDependency(NoPlayerLightPluginGuid')) `
    'MageLight or No Player Light soft dependency ordering is missing.'
Assert-Contract ($controller.Contains('"RespectExternalPlayerLightOwnership"') -and
    $controller.Contains('IndoorGameObjectSwapper') -and
    $controller.Contains('"HeroLight"') -and
    (([regex]::Matches(
        $controller,
        'IsExternallyOwnedPlayerLight\(light\)'
    )).Count -ge 2)) `
    'external HeroLight ownership is not semantically excluded before capture.'
Assert-Contract ($rules.Contains('AvailableShadowMapFaces') -and
    $controller.Contains('_managedExternalPlayerShadowMapFaces') -and
    $controller.Contains('EffectiveMaximumShadowMapFaces()')) `
    'active MageLight shadow faces are not reserved from the permanent budget.'
Assert-Contract ($controller.Contains('mage-light-no-player-light-conflict') -and
    $controller.Contains('TryShowCompatibilityWarning')) `
    'the conflicting MageLight and No Player Light combination is not reported.'
Assert-Contract ($controller.Contains('CalculateFrustumPlanes') -and
    $controller.Contains('ViewExitDelaySeconds') -and
    $controller.Contains('OffscreenReserveLights') -and
    $controller.Contains('MaximumSelectionSwapsPerRefresh')) `
    'view relevance, exit grace, offscreen reserve, or bounded swaps are missing.'
Assert-Contract (([regex]::Matches(
    $controller,
    'FindObjectsByType<Light>'
)).Count -eq 1) `
    'managed discovery must use exactly one global Light search.'
Assert-Contract (-not $controller.Contains('Resources.FindObjectsOfTypeAll')) `
    'managed discovery must not scan asset and template objects.'
Assert-Contract ($controller.Contains('ShadowsEnabled') -and
    $controller.Contains('shadowDimmer') -and
    $controller.Contains('volumetricShadowDimmer') -and
    $controller.Contains('RequestShadowMapRendering')) `
    'complete HDRP capture and on-demand update behavior are missing.'
Assert-Contract ($controller.Contains('WriteManagedFloatIfChanged') -and
    $controller.Contains('RestoreManagedHdrpState') -and
    $controller.Contains('RestoreManagedResolution')) `
    'conditional HDRP writes or exact restoration are missing.'
Assert-Contract (-not $controller.Contains('EveryFrame')) `
    'the addon must not force HDRP shadow maps to update every frame.'
Assert-Contract (-not $controller.Contains('shadowUpdateMode') -and
    -not $controller.Contains('shadowMapUpdateMode')) `
    'the addon must leave the authored HDRP shadow update mode untouched.'
Assert-Contract ($plugin.Contains('RestoreAllManagedLights("addon unload")') -and
    $controller.Contains('RestoreAllManagedLights("active scene changed")') -and
    $controller.Contains('BeforeParentSceneCooldown')) `
    'unload or scene-transition restoration is incomplete.'
Assert-Contract (-not $controller.Contains('GameDayNightSystem') -and
    -not $controller.Contains('shadowIntensityBlendMinutes') -and
    -not $controller.Contains('WeatherSecondsPerRealSecond') -and
    -not $controller.Contains('DayNightShadowCaster')) `
    'local-light selection must remain independent of directional-shadow behavior.'

Write-Host 'Safe shadow selection contracts passed.'
