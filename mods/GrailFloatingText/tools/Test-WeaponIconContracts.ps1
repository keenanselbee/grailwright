[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$specificIconIds = @(
    "one_handed_sword",
    "one_handed_axe",
    "one_handed_blunt",
    "one_handed_dagger",
    "one_handed_spear",
    "two_handed_sword",
    "two_handed_axe",
    "two_handed_blunt",
    "two_handed_spear"
)

foreach ($iconId in $specificIconIds) {
    if ($source.IndexOf('"' + $iconId + '"', [StringComparison]::Ordinal) -lt 0) {
        throw "The built-in $iconId icon ID is missing."
    }

    $path = Join-Path $modRoot ("icons\" + $iconId + ".png")
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The built-in $iconId icon PNG is missing."
    }

    $image = [System.Drawing.Image]::FromFile($path)
    try {
        if ($image.Width -ne 128 -or $image.Height -ne 128) {
            throw "$iconId must be 128x128; found $($image.Width)x$($image.Height)."
        }
    }
    finally {
        $image.Dispose()
    }
}

if ($source.IndexOf('"one_handed_sickle"', [StringComparison]::Ordinal) -ge 0 -or
    (Test-Path -LiteralPath (Join-Path $modRoot "icons\one_handed_sickle.png"))) {
    throw "The removed one_handed_sickle icon remains in the built-in icon surface."
}

foreach ($broadIconId in @("one_handed", "two_handed")) {
    $path = Join-Path $modRoot ("icons\" + $broadIconId + ".png")
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The broad compatibility icon $broadIconId is missing."
    }
    $image = [System.Drawing.Image]::FromFile($path)
    try {
        if ($image.Width -ne 128 -or $image.Height -ne 128) {
            throw "$broadIconId must be 128x128; found $($image.Width)x$($image.Height)."
        }
    }
    finally {
        $image.Dispose()
    }
}

foreach ($alias in @(
    'normalized == "one_handed_polearm"',
    'normalized = "one_handed_spear"',
    'normalized == "two_handed_polearm"',
    'normalized = "two_handed_spear"'
)) {
    if ($source.IndexOf($alias, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing weapon icon compatibility alias: $alias"
    }
}

Write-Output "Weapon icon contracts passed."
