[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\GrailFloatingText.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw

$requiredContracts = @(
    '"12. Icon Color Overrides"',
    'name + "IconColor"',
    'string.Empty',
    'ResolveIconColor(entry.Style, textColor, notificationAlpha)',
    'ResolveStyleColorGroup(entry.Style)',
    '"IconShadowEnabled"',
    '"IconShadowOpacity"',
    'notificationAlpha * iconShadowOpacity'
)
foreach ($contract in $requiredContracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing icon color or shadow contract: $contract"
    }
}

$drawStart = $source.IndexOf("private void UpdateNotificationView(", [StringComparison]::Ordinal)
$drawEnd = $source.IndexOf("private static void ConfigureNotificationText(", $drawStart, [StringComparison]::Ordinal)
if ($drawStart -lt 0 -or $drawEnd -le $drawStart) {
    throw "Could not locate the icon rendering path."
}

$drawMethod = $source.Substring($drawStart, $drawEnd - $drawStart)
$shadowToggle = $drawMethod.IndexOf("_iconShadowEnabled == null || _iconShadowEnabled.Value", [StringComparison]::Ordinal)
$shadowDraw = $drawMethod.IndexOf("view.IconShadow.gameObject.SetActive(showIconShadow);", [StringComparison]::Ordinal)
$overrideResolve = $drawMethod.IndexOf("ResolveIconColor(entry.Style", [StringComparison]::Ordinal)
$foregroundDraw = $drawMethod.IndexOf("view.Icon.color =", [StringComparison]::Ordinal)
if ($shadowToggle -lt 0 -or $shadowDraw -le $shadowToggle) {
    throw "Icon shadow rendering is not guarded by IconShadowEnabled."
}
if ($overrideResolve -lt 0 -or $foregroundDraw -le $overrideResolve) {
    throw "Icon color overrides are not resolved before foreground rendering."
}

Write-Output "Icon color override and shadow contract passed."
