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
    "DodgeRestoreSeconds = 0.08f",
    "DodgeReentryEpsilon = 0.001f",
    '"DodgeMoveTowardVanillaPercent"',
    "50.0f",
    'new AcceptableValueRange<float>(0.0f, 100.0f)',
    "hero.TryGetElement<LegsFSM>()",
    "IsDodgeState(legs.CurrentStateType)",
    "animatorState.TimeElapsedNormalized",
    "normalizedTime + DodgeReentryEpsilon",
    "_dodgeEntryOffsetBlend = _dodgeOffsetBlend",
    "return _dodgeOffsetBlend",
    "moveTowardVanillaPercent / 100.0f",
    "minimumBlend = 1.0f - strength",
    "Mathf.LerpUnclamped(",
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
