$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\GrailFloatingText.cs") -Raw

$requiredFragments = @(
    "private void LateUpdate()",
    "private FontAsset ResolveConfiguredFontAsset()",
    "FontFamily activeFont = setting.ActiveFont;",
    "return fontAsset;",
    "typeof(TextMeshProUGUI)",
    "text.font = fontAsset;"
)

foreach ($fragment in $requiredFragments) {
    if ($source.IndexOf($fragment, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing native FontAsset rendering contract: $fragment"
    }
}

$forbiddenFragments = @(
    "private void OnGUI()",
    ".sourceFontFile"
)

foreach ($fragment in $forbiddenFragments) {
    if ($source.IndexOf($fragment, [StringComparison]::Ordinal) -ge 0) {
        throw "Legacy font rendering path is still present: $fragment"
    }
}

Write-Host "Grail Floating Text native FontAsset rendering contract passed."
