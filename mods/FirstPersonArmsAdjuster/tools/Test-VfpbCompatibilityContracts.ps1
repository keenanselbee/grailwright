[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\FirstPersonArmsAdjuster.cs"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Missing source file: $sourcePath"
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$requiredFragments = @(
    'typeof(KandraRendererManager)',
    '"OnPreLateUpdateEnd"',
    'KandraRendererManagerPreLateUpdateEndPatch',
    'instance.ApplyKandraRenderOffset(__instance);',
    'rendererManager.RigManager',
    'ApplyKandraCullingOffset(',
    'torsoRendererRetraction);',
    'typeof(VHeroController)',
    '"ProcessUpdate"',
    'VHeroControllerProcessUpdatePatch',
    'instance.CaptureVisualWorldOffsetAfterCameraRotation(',
    'RefreshCurrentVisualWorldOffset(controller);',
    'GetViewmodelHeadBobFollowWorldOffset(camera)',
    '_currentShoulderRetractionWorldOffset',
    'Time.timeScale <= 0.0f',
    'RefreshCurrentVisualWorldOffset(null);',
    'ReportPausedRenderOffsetFallback();',
    'during paused rendering because VHeroController.ProcessUpdate did not provide a current-frame snapshot'
)

foreach ($fragment in $requiredFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing deterministic native first-person render hook: $fragment"
    }
}

$forbiddenFragments = @(
    'com.thatsitsiryoureleaving.VFPB',
    'VFPB.Systems.VisibleFirstPersonBody',
    '_vfpbOverlayRoot',
    'ApplyVfpbOverlayOffset',
    'overlayRoot.position += worldOffset',
    'ApplyLateKandraOffset(bodyData, translation)'
)

foreach ($fragment in $forbiddenFragments) {
    if ($source.Contains($fragment)) {
        throw "Unsafe or stale VFPB compatibility path remains: $fragment"
    }
}

$allocationReuseFragments = @(
    '_kandraCullingRendererSlots',
    '_kandraCullingRendererTranslations',
    '_retainedDrakeEntities',
    '_restoredDrakeEntities',
    '_weaponEntityAccessScanBuffer',
    '_attachedEffectCandidates',
    '_visualEffectScanBuffer',
    '_particleSystemScanBuffer',
    'bodyData.GetComponentsInChildren(true, bodyRigs);',
    'bodyData.GetComponentsInChildren(true, renderers);',
    'weapon.GetComponentsInChildren(true, weaponAccesses);',
    'root.GetComponentsInChildren(true, visualEffects);',
    'root.GetComponentsInChildren(true, particleSystems);',
    'RestoreDrakeOffsets(entityManager, retainedEntities, true);'
)

foreach ($fragment in $allocationReuseFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing native presentation allocation reuse: $fragment"
    }
}

$sceneCacheCleanup = [regex]::Match(
    $source,
    '(?s)private void ClearSceneCaches\(\).+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
$sceneBufferCleanupFragments = @(
    '_kandraCullingRendererSlots.Clear();',
    '_kandraCullingRendererTranslations.Clear();',
    '_kandraRigRefreshRigs.Clear();',
    '_kandraRigRefreshBodyRigs.Clear();',
    '_kandraRigRefreshRenderers.Clear();',
    '_staleShoulderBoneProfiles.Clear();',
    '_retainedDrakeEntities.Clear();',
    '_restoredDrakeEntities.Clear();',
    '_weaponEntityAccesses.Clear();',
    '_weaponEntityAccessScanBuffer.Clear();',
    '_attachedEffectExcludedRoots.Clear();',
    '_attachedEffectCandidates.Clear();',
    '_visualEffectScanBuffer.Clear();',
    '_particleSystemScanBuffer.Clear();'
)

foreach ($fragment in $sceneBufferCleanupFragments) {
    if (-not $sceneCacheCleanup.Contains($fragment)) {
        throw "Scene teardown must release reusable presentation-buffer references: $fragment"
    }
}

$kandraCullingMethod = [regex]::Match(
    $source,
    '(?s)private int ApplyKandraCullingOffset\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
$drakeOffsetMethod = [regex]::Match(
    $source,
    '(?s)internal void ApplyDrakeWeaponOffset\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ($kandraCullingMethod -match 'new List<' -or
    $drakeOffsetMethod -match 'new HashSet<') {
    throw "Per-frame Kandra or Drake synchronization recreated a managed collection."
}

$sharedOffsetConsumer = 'TryGetCurrentVisualWorldOffset(out worldOffset)'
$sharedOffsetConsumerCount = [regex]::Matches(
    $source,
    [regex]::Escape($sharedOffsetConsumer)
).Count
if ($sharedOffsetConsumerCount -ne 5) {
    throw "Expected the public API and four visual paths to share one cached world offset; found $sharedOffsetConsumerCount consumers."
}

$effectiveOffsetSampleCount = [regex]::Matches(
    $source,
    'GetEffectiveLocalOffset\(hero\)'
).Count
if ($effectiveOffsetSampleCount -ne 1) {
    throw "Expected exactly one sample of the effective configured visual offset; found $effectiveOffsetSampleCount."
}

$refreshMethod = [regex]::Match(
    $source,
    '(?s)private void RefreshCurrentVisualWorldOffset\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
$visualBasisContract = @'
Transform visualBasis = controller.fppParent == null
                ? camera.transform
                : controller.fppParent.transform;
'@
if (-not $refreshMethod.Contains($visualBasisContract)) {
    throw "The shared visual offset does not prefer the current first-person arms pivot with a guarded main-camera fallback."
}
if ($refreshMethod -notmatch '(?s)configuredLocalOffset =\s*GetEffectiveLocalOffset\(hero\).+?configuredWorldOffset = visualBasis\.TransformVector\(\s*configuredLocalOffset\).+?viewmodelHeadBobWorldOffset =\s*GetViewmodelHeadBobFollowWorldOffset\(camera\).+?_currentVisualWorldOffset = configuredWorldOffset\s*\+ viewmodelHeadBobWorldOffset') {
    throw "The shared visual offset must preserve one arms-pivot-space configured sample and add one exact camera-space head-bob compensation sample."
}
if ([regex]::IsMatch(
        $refreshMethod,
        'camera\.transform\.TransformVector\(\s*GetEffectiveLocalOffset\(hero\)\s*\)')) {
    throw "The shared visual offset still uses the rendered camera directly instead of the current first-person arms pivot."
}

if ([regex]::IsMatch(
        $source,
        '(?:camera\.transform|controller\.fppParent\.transform)\.TransformVector\(\s*(?:localOffset|configuredWorldOffset|viewmodelHeadBobWorldOffset)\s*\)')) {
    throw "A visual path independently resamples the presentation offset instead of using the per-frame cache."
}

$updateMethod = [regex]::Match(
    $source,
    '(?s)private void Update\(\).+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ($updateMethod.Contains('RefreshCurrentVisualWorldOffset')) {
    throw "The shared visual offset is still captured before the hero controller applies the current frame's camera rotation."
}

$sharedOffsetGetter = [regex]::Match(
    $source,
    '(?s)internal bool TryGetCurrentVisualWorldOffset\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ($sharedOffsetGetter -notmatch '(?s)if \(_currentVisualWorldOffsetFrame != Time\.frameCount\s*&& Time\.timeScale <= 0\.0f\)\s*\{\s*RefreshCurrentVisualWorldOffset\(null\);\s*ReportPausedRenderOffsetFallback\(\);\s*\}' -or
    $sharedOffsetGetter -notmatch '(?s)ReportPausedRenderOffsetFallback\(\);\s*\}\s*worldOffset = _currentVisualWorldOffset') {
    throw "The render fallback must refresh only a missing paused-frame snapshot before returning the shared offset."
}
if ([regex]::Matches(
        $sharedOffsetGetter,
        'RefreshCurrentVisualWorldOffset').Count -ne 1) {
    throw "The shared offset getter contains an additional path that could lock a stale gameplay camera rotation."
}

$immutableFrameGuard = @'
if (_currentVisualWorldOffsetFrame == Time.frameCount)
            {
                return;
            }
'@
if (-not $source.Contains($immutableFrameGuard)) {
    throw "The visual world-offset cache can be refreshed more than once per frame."
}

Write-Host "First Person Arms Adjuster VFPB compatibility contracts passed."
