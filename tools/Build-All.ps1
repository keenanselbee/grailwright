[CmdletBinding()]
param(
    [string]$GameRoot = "",
    [string]$BepInExRoot = "",
    [string]$VortexModsRoot = "",
    [string]$DestinationDirectory = "",
    [switch]$SkipCompile,
    [switch]$StageToVortex
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "Build-Mod.ps1"

$manifests = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "mods") -Recurse -File -Filter "mod.json" |
    Sort-Object FullName

$failures = New-Object "System.Collections.Generic.List[string]"

foreach ($manifest in $manifests) {
    $modRoot = Split-Path -Parent $manifest.FullName
    Write-Host "Building $modRoot"

    $args = @{
        ModRoot = $modRoot
    }

    if (-not [string]::IsNullOrWhiteSpace($GameRoot)) {
        $args.GameRoot = $GameRoot
    }

    if (-not [string]::IsNullOrWhiteSpace($BepInExRoot)) {
        $args.BepInExRoot = $BepInExRoot
    }

    if (-not [string]::IsNullOrWhiteSpace($VortexModsRoot)) {
        $args.VortexModsRoot = $VortexModsRoot
    }

    if (-not [string]::IsNullOrWhiteSpace($DestinationDirectory)) {
        $args.DestinationDirectory = $DestinationDirectory
    }

    if ($SkipCompile) {
        $args.SkipCompile = $true
    }

    if ($StageToVortex) {
        $args.StageToVortex = $true
    }

    try {
        & $buildScript @args
    } catch {
        $failures.Add($modRoot + ": " + $_.Exception.GetBaseException().Message)
    }
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Build failures:"
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }

    exit 1
}

Write-Host "Built $($manifests.Count) mods."
