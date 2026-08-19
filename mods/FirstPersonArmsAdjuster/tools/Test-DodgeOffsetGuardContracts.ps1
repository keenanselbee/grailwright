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
    "DodgeEndNormalizedTime = 0.90f",
    "hero.TryGetElement<LegsFSM>()",
    "IsDodgeState(legs.CurrentStateType)",
    "animatorState.TimeElapsedNormalized",
    "1.0f - Mathf.Sin(progress * Mathf.PI)",
    "state >= HeroStateType.DashFront",
    "state <= HeroStateType.DashBackRight",
    "* GetDodgeOffsetBlend(hero)"
)

foreach ($fragment in $requiredFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing dodge offset guard contract: $fragment"
    }
}

Write-Host "First Person Arms Adjuster dodge offset guard contracts passed."
