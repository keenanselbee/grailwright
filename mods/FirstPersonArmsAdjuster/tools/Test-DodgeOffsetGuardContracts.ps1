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
    "DodgeRetractionMaximumMeters = 0.25f",
    "DodgeRetractionBlendInSeconds = 0.06f",
    "DodgeRetractionBlendOutSeconds = 0.20f",
    "DodgeRetractionHoldSeconds = 0.12f",
    "DodgeActivitySignalGraceSeconds = 0.05f",
    "UpdateDodgeShoulderRetractionBlend();",
    "hero.Elements<LegsFSM>()",
    "IsDodgeState(legs.CurrentStateType)",
    "legs.CurrentStateToEnterType",
    "_dodgeShoulderRetractionHoldUntil = now",
    "_lastDodgeActivitySignalTime",
    "NotifyDodgeActivity()",
    "typeof(DashBaseState)",
    '"OnHeroDashed"',
    '"OnHeroDashedForward"',
    '"OnUpdate"',
    "DodgeActivityPatch.Postfix",
    "now < _dodgeShoulderRetractionHoldUntil",
    "Time.unscaledDeltaTime / duration",
    "_dodgeShoulderRetractionBlend = Mathf.MoveTowards(",
    "state >= HeroStateType.DashFront",
    "state <= HeroStateType.DashBackRight",
    "configuredShoulderRetraction",
    "DodgeRetractionMaximumMeters",
    "dodgeRetractionRemaining",
    "dodgeRetractionProgress",
    "Fading shoulder retraction toward 0.25 metres for the dodge guard."
)

foreach ($fragment in $requiredFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing dodge offset guard contract: $fragment"
    }
}

foreach ($retiredFragment in @(
    'DodgeMoveTowardVanillaPercent',
    'GetDodgeOffsetBlend(',
    'dodgeRetainedScale',
    '_dodgeOffsetBlend'
)) {
    if ($source.Contains($retiredFragment)) {
        throw "Retired whole-viewmodel dodge behavior remains: $retiredFragment"
    }
}

Write-Host "First Person Arms Adjuster dynamic dodge-retraction guard contracts passed."
