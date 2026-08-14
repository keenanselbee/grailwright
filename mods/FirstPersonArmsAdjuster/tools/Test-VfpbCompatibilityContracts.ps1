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
    'ApplyKandraCullingOffset(translation)'
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
    'camera\.transform\.TransformVector\(\s*GetEffectiveLocalOffset\(hero\)\s*\)'
).Count
if ($effectiveOffsetSampleCount -ne 1) {
    throw "Expected exactly one camera-space sample of the effective visual offset; found $effectiveOffsetSampleCount."
}

if ([regex]::IsMatch(
        $source,
        'camera\.transform\.TransformVector\(\s*localOffset\s*\)')) {
    throw "A visual path independently resamples the camera-space offset instead of using the per-frame cache."
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
