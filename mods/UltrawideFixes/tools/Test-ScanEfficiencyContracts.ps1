[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\UltrawideFixes.cs") -Raw

function Assert-ScanEfficiencyContract {
    param([bool]$Condition, [string]$Message)
    if (!$Condition) {
        throw "Ultrawide scan efficiency contract failed: $Message"
    }
}

foreach ($required in @(
    'if (!ApplyPatchPass())',
    'private bool ApplyPatchPass()',
    '_loadingHooksReady = true;',
    'bool titleScene = IsLikelyTitleScene(',
    'bool needsTitleVideo = titleScene',
    'bool needsTitleBars = titleScene',
    'bool needsLoadingFallback = (!_loadingHooksReady',
    '_scanLoadingViewsOnce = false;',
    'return (needsTitleVideo && _patchedRawImages.Count == 0)')) {
    Assert-ScanEfficiencyContract ($source.Contains($required)) "runtime omits $required"
}

Write-Host "Ultrawide scan efficiency contracts passed."
