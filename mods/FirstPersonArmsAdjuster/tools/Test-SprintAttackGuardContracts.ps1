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
    "HeroStateType.LightAttackForward",
    "UpdateSprintAttackOffsetBlend();",
    "SprintAttackBlendOutSeconds = 0.12f",
    "SprintAttackBlendInSeconds = 0.22f",
    "sprintAttackRetainedScale =",
    "* sprintAttackRetainedScale",
    '"Blending the first-person offset to vanilla for a sprint attack."'
)

foreach ($fragment in $requiredFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing sprint-attack guard contract: $fragment"
    }
}

Write-Host "First Person Arms Adjuster sprint-attack guard contracts passed."
