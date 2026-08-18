[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$script = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot "Build-Mod.ps1") -Raw
$buildAllScript = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot "Build-All.ps1") -Raw
$exportScript = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot "Export-VortexPackage.ps1") -Raw
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
    'if ($DesktopOnly)',
    'Join-Path $RepoRoot ".codex-temp\packages"',
    'Get-DesktopDirectory')) {
    Assert-BuildDestinationContract ($defaultBlock.Contains($required)) "default block omits $required"
}

foreach ($required in @(
    '[switch]$DesktopOnly',
    '[switch]$PackageOnly',
    '$shouldStageToVortex = -not $DesktopOnly -and -not $PackageOnly',
    'if ($shouldStageToVortex)')) {
    Assert-BuildDestinationContract ($script.Contains($required)) "Build-Mod omits $required"
}
Assert-BuildDestinationContract ($script.Contains('-DesktopOnly cannot be combined with -DestinationDirectory')) "DesktopOnly does not enforce the Desktop destination"
Assert-BuildDestinationContract ($script.Contains('-PackageOnly requires an explicit -DestinationDirectory')) "PackageOnly permits an implicit destination"

foreach ($required in @(
    '[switch]$DesktopOnly',
    '[switch]$PackageOnly',
    '$args.DesktopOnly = $true',
    '$args.PackageOnly = $true',
    '$args.StageToVortex = $true')) {
    Assert-BuildDestinationContract ($buildAllScript.Contains($required)) "Build-All omits $required"
}

Assert-BuildDestinationContract ($exportScript.Contains('package export never defaults to the Desktop')) "package exporter permits an implicit destination"
Assert-BuildDestinationContract (-not $exportScript.Contains('Remove-PreviousPackageArchives')) "package exporter still removes earlier archives"
Assert-BuildDestinationContract (-not $exportScript.Contains('RemovedArchives')) "package exporter still reports removed archives"

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
    '$buildArgs.DestinationDirectory = $Destination',
    '$buildArgs.PackageOnly = $true')) {
    Assert-BuildDestinationContract ($publishBuildBlock.Contains($required)) "publisher build block omits $required"
}
Assert-BuildDestinationContract (-not $publishBuildBlock.Contains('Get-DesktopDirectory')) "publisher build block defaults to Desktop"

Write-Host "Build destination contracts passed."
