[CmdletBinding()]
param(
    [string]$GameRoot = "",
    [string]$BepInExRoot = "",
    [string]$VortexModsRoot = "",
    [string]$DestinationDirectory = "",
    [switch]$SkipCompile,
    [switch]$StageToVortex,
    [switch]$DesktopOnly,
    [switch]$PackageOnly,
    [int]$LockWaitSeconds = 0,
    [int]$LockStaleAfterMinutes = 720,
    [switch]$ForceStaleLock
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$outputModes = @($StageToVortex.IsPresent, $DesktopOnly.IsPresent, $PackageOnly.IsPresent)
$outputModeCount = @($outputModes | Where-Object { $_ }).Count
if ($outputModeCount -gt 1) {
    throw "Use only one output mode: -StageToVortex, -DesktopOnly, or -PackageOnly."
}
if ($DesktopOnly -and -not [string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    throw "-DesktopOnly cannot be combined with -DestinationDirectory; it always exports to the Windows Desktop."
}
if ($PackageOnly -and [string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    throw "-PackageOnly requires an explicit -DestinationDirectory."
}

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

    if ($DesktopOnly) {
        $args.DesktopOnly = $true
    } elseif ($PackageOnly) {
        $args.PackageOnly = $true
    } else {
        $args.StageToVortex = $true
    }

    $args.LockWaitSeconds = $LockWaitSeconds
    $args.LockStaleAfterMinutes = $LockStaleAfterMinutes
    if ($ForceStaleLock) {
        $args.ForceStaleLock = $true
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
