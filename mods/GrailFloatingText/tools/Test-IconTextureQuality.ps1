[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\GrailFloatingText.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw

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

Write-Output "Icon texture quality contract passed."
