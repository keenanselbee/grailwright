$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\SteelAndBone.cs") -Raw
$difficulty = Get-Content -LiteralPath (Join-Path $modRoot "src\DifficultyOverhaul.cs") -Raw

foreach ($required in @(
    'public static class SteelAndBoneAwarenessApi',
    'public const int ApiVersion = 1',
    'GetEnemySightRangeMultiplier()',
    'GetEnemyAggroPersistenceMultiplier()')) {
    if (!$source.Contains($required)) { throw "Missing Steel and Bone awareness API contract: $required" }
}
foreach ($required in @(
    'GetEnemySightRangeMultiplierForInterop()',
    'DifficultyModifierIsEnabled(_modifyEnemySightRange)',
    'GetEnemyAggroPersistenceMultiplierForInterop()',
    'DifficultyModifierIsEnabled(_modifyEnemyAggroPersistence)')) {
    if (!$difficulty.Contains($required)) { throw "Missing effective-awareness contract: $required" }
}

Write-Output "Steel and Bone awareness API contracts passed."
