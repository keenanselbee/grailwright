[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$script = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot "Build-Mod.ps1") -Raw
$publishScript = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot "Publish-NexusMod.ps1") -Raw

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

$publishBuildStart = $publishScript.IndexOf('function Build-Archive')
$publishBuildEnd = $publishScript.IndexOf('function Wait-NexusUploadAvailable', $publishBuildStart)
if ($publishBuildStart -lt 0 -or $publishBuildEnd -le $publishBuildStart) {
    throw "Build destination contract failed: publisher build block was not found"
}

$publishBuildBlock = $publishScript.Substring(
    $publishBuildStart,
    $publishBuildEnd - $publishBuildStart)
foreach ($required in @(
    'if ([string]::IsNullOrWhiteSpace($Destination))',
    'Join-Path $RepoRoot ".codex-temp\builds"',
    '$buildArgs.DestinationDirectory = $Destination')) {
    Assert-BuildDestinationContract ($publishBuildBlock.Contains($required)) "publisher build block omits $required"
}
Assert-BuildDestinationContract (-not $publishBuildBlock.Contains('Get-DesktopDirectory')) "publisher build block defaults to Desktop"

Write-Host "Build destination contracts passed."
