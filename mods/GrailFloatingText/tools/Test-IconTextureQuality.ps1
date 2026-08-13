[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\GrailFloatingText.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw
$goldEarnedIconIds = @(
    "gold_earned_very_low",
    "gold_earned_low",
    "gold_earned_medium",
    "gold_earned_high",
    "gold_earned_very_high"
)
foreach ($iconId in $goldEarnedIconIds) {
    if ($source.IndexOf('"' + $iconId + '"', [StringComparison]::Ordinal) -lt 0) {
        throw "The built-in $iconId icon ID is missing."
    }
    $iconPath = Join-Path $modRoot ("icons\" + $iconId + ".png")
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw "The built-in $iconId icon PNG is missing."
    }
}
$corpseIconIds = @(
    "corpse_meager",
    "corpse_worthy",
    "corpse_potent",
    "corpse_prime"
)
foreach ($iconId in $corpseIconIds) {
    if ($source.IndexOf('"' + $iconId + '"', [StringComparison]::Ordinal) -lt 0) {
        throw "The built-in $iconId icon ID is missing."
    }
    $iconPath = Join-Path $modRoot ("icons\" + $iconId + ".png")
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw "The built-in $iconId icon PNG is missing."
    }
}
if ($source.IndexOf('"corpse"', [StringComparison]::Ordinal) -ge 0) {
    throw "The obsolete generic corpse icon ID remains registered."
}
$summonIconPath = Join-Path $modRoot "icons\summon.png"
if ($source.IndexOf('"summon"', [StringComparison]::Ordinal) -lt 0) {
    throw "The built-in summon icon ID is missing."
}
if (-not (Test-Path -LiteralPath $summonIconPath -PathType Leaf)) {
    throw "The built-in summon icon PNG is missing."
}
Add-Type -AssemblyName System.Drawing
$summonBitmap = [System.Drawing.Bitmap]::FromFile($summonIconPath)
try {
    if ($summonBitmap.Width -ne 128 -or $summonBitmap.Height -ne 128) {
        throw "The summon icon must be 128 by 128 pixels."
    }
    $corners = @(
        $summonBitmap.GetPixel(0, 0),
        $summonBitmap.GetPixel(127, 0),
        $summonBitmap.GetPixel(0, 127),
        $summonBitmap.GetPixel(127, 127)
    )
    if (@($corners | Where-Object { $_.A -gt 2 }).Count -ne 0) {
        throw "The summon icon must retain transparent outer corners."
    }
    if ($summonBitmap.GetPixel(64, 75).A -lt 240) {
        throw "The summon icon's central paw pad is unexpectedly transparent."
    }
}
finally {
    $summonBitmap.Dispose()
}
$quickWheelSourcePath = Join-Path $modRoot "src\QuickWheelPanel.cs"
$quickWheelSource = Get-Content -LiteralPath $quickWheelSourcePath -Raw

$methodStart = $source.IndexOf("private void LoadIconTextures()", [StringComparison]::Ordinal)
$methodEnd = $source.IndexOf("private void ReleaseIconTextures()", $methodStart, [StringComparison]::Ordinal)
if ($methodStart -lt 0 -or $methodEnd -le $methodStart) {
    throw "Could not locate the complete icon texture loading path."
}

$method = $source.Substring($methodStart, $methodEnd - $methodStart)
$requiredContracts = @(
    'new Texture2D(2, 2, TextureFormat.RGBA32, true)',
    'DilateTransparentPixelColors(texture)',
    'texture.Apply(true, true)',
    'texture.filterMode = FilterMode.Trilinear',
    'texture.wrapMode = TextureWrapMode.Clamp',
    'texture.hideFlags = HideFlags.DontSave'
)
foreach ($contract in $requiredContracts) {
    if ($method.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing icon texture quality contract: $contract"
    }
}

if ($method.IndexOf("FilterMode.Bilinear", [StringComparison]::Ordinal) -ge 0) {
    throw "The built-in icon loader still selects bilinear filtering."
}

$quickWheelMethodStart = $quickWheelSource.IndexOf("internal bool RegisterQuickWheelIcons(", [StringComparison]::Ordinal)
$quickWheelMethodEnd = $quickWheelSource.IndexOf("internal bool SetQuickWheelTooltipActive(", $quickWheelMethodStart, [StringComparison]::Ordinal)
if ($quickWheelMethodStart -lt 0 -or $quickWheelMethodEnd -le $quickWheelMethodStart) {
    throw "Could not locate the source-provided quick-wheel icon loading path."
}

$quickWheelMethod = $quickWheelSource.Substring($quickWheelMethodStart, $quickWheelMethodEnd - $quickWheelMethodStart)
foreach ($contract in $requiredContracts) {
    if ($quickWheelMethod.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing source-provided quick-wheel icon texture quality contract: $contract"
    }
}

if ($quickWheelMethod.IndexOf("FilterMode.Bilinear", [StringComparison]::Ordinal) -ge 0) {
    throw "The source-provided quick-wheel icon loader selects bilinear filtering."
}

Write-Output "Icon texture quality contract passed."
