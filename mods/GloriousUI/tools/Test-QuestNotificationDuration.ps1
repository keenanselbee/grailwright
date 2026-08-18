[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\GloriousUI.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw

$requiredFragments = @(
    'QuestNotificationDataTypeName',
    'ObjectiveNotificationDataTypeName',
    '"get_VisibilityDuration"',
    'typeof(QuestNotificationDurationPatch)',
    '__result = plugin.GetQuestNotificationDuration(',
    '!IsEnabled()',
    'return gameDuration;'
)

foreach ($fragment in $requiredFragments) {
    if ($source.IndexOf($fragment, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing quest notification duration contract: $fragment"
    }
}

if (-not [regex]::IsMatch(
    $source,
    'Config\.Bind\(\s*"HUD",\s*"QuestNotificationDurationSeconds",\s*10\.0f,')) {
    throw "QuestNotificationDurationSeconds must default to 10 seconds."
}

if (-not [regex]::IsMatch(
    $source,
    '_questNotificationDurationSeconds\.Value <= 0\.0f\)\s*\{\s*return gameDuration;')) {
    throw "QuestNotificationDurationSeconds must preserve the game duration when set to 0."
}

Write-Output "Glorious quest notification duration contract passed."
