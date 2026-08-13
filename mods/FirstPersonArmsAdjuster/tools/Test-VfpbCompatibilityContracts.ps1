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

Write-Host "First Person Arms Adjuster VFPB compatibility contracts passed."
