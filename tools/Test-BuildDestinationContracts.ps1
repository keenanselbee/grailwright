[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$script = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot "Build-Mod.ps1") -Raw

function Assert-BuildDestinationContract {
    param([bool]$Condition, [string]$Message)
    if (!$Condition) {
        throw "Build destination contract failed: $Message"
    }
}

$defaultStart = $script.IndexOf(
    'if ([string]::IsNullOrWhiteSpace($DestinationDirectory))')
$compileStart = $script.IndexOf('if (-not $SkipCompile)', $defaultStart)
if ($defaultStart -lt 0 -or $compileStart -le $defaultStart) {
    throw "Build destination contract failed: default destination block was not found"
}

$defaultBlock = $script.Substring(
    $defaultStart,
    $compileStart - $defaultStart)
foreach ($required in @(
    'if ($StageToVortex)',
    'Join-Path $RepoRoot ".codex-temp\packages"',
    'Get-DesktopDirectory')) {
    Assert-BuildDestinationContract ($defaultBlock.Contains($required)) "default block omits $required"
}

Write-Host "Build destination contracts passed."
