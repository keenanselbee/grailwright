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
    'ApplyKandraCullingOffset(translation)',
    'typeof(VHeroController)',
    '"ProcessUpdate"',
    'VHeroControllerProcessUpdatePatch',
    'instance.CaptureVisualWorldOffsetAfterCameraRotation(',
    'RefreshCurrentVisualWorldOffset(controller);'
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
    'visualBasis\.TransformVector\(\s*GetEffectiveLocalOffset\(hero\)\s*\)'
).Count
if ($effectiveOffsetSampleCount -ne 1) {
    throw "Expected exactly one arms-pivot-space sample of the effective visual offset; found $effectiveOffsetSampleCount."
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
if ([regex]::IsMatch(
        $refreshMethod,
        'camera\.transform\.TransformVector\(\s*GetEffectiveLocalOffset\(hero\)\s*\)')) {
    throw "The shared visual offset still uses the rendered camera directly instead of the current first-person arms pivot."
}

if ([regex]::IsMatch(
        $source,
        '(?:camera\.transform|controller\.fppParent\.transform)\.TransformVector\(\s*localOffset\s*\)')) {
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
if ($sharedOffsetGetter.Contains('RefreshCurrentVisualWorldOffset')) {
    throw "An early API or visual consumer can still lock a stale camera rotation before the authoritative hero-controller capture."
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
