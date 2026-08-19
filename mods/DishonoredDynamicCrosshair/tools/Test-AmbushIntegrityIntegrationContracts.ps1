Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw "Dishonored Ambush Integrity contract failed: $Message"
    }
}

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$source = Get-Content -Raw -LiteralPath (Join-Path $modRoot "src\Plugin.cs")
$ambushSource = Get-Content -Raw -LiteralPath (
    Join-Path $repoRoot "mods\AmbushIntegrity\src\AmbushIntegrity.cs")
$manifest = Get-Content -Raw -LiteralPath (Join-Path $modRoot "mod.json") | ConvertFrom-Json
$assetPath = Join-Path $modRoot "interaction_backstab.png"

Assert-Contract ($manifest.version -eq "3.3.1") "mod.json is not version 3.3.1."
Assert-Contract ($source.Contains('[BepInDependency("ks.tgfoa.ambush-integrity", BepInDependency.DependencyFlags.SoftDependency)]')) "Ambush Integrity is not a soft dependency."
Assert-Contract (-not ((Get-Content -Raw -LiteralPath (Join-Path $modRoot "mod.json")).Contains("AmbushIntegrity.dll"))) "The integration must not take a hard Ambush Integrity assembly reference."
foreach ($token in @(
    '"AmbushIntegrity.AmbushIntegrityApi"',
    'GetRawConstantValue(), 1',
    '"GetBackstabOpportunityState"',
    'BackstabReadyOverlayEnabled',
    'BackstabReadyColor',
    'interaction_backstab.png',
    'ReadAmbushIntegrityBackstabReady()',
    'SetBackstabReadyOverlayEnabled(false);',
    'BackstabUnderlyingOpacityMultiplier = 0.5f',
    '_backstabReadyOverlayImage.rectTransform.localScale = Vector3.one;',
    '_backstabReadyOverlayImage.transform.SetAsLastSibling();')) {
    Assert-Contract ($source.Contains($token)) "Source is missing token: $token"
}
Assert-Contract (-not $source.Contains('BackstabReadyScale')) "The obsolete backstab scale setting remains."
Assert-Contract (-not $source.Contains('BackstabReadyPulse')) "The obsolete backstab pulse remains."
Assert-Contract (-not $source.Contains('custom_reticle_backstab_ready_overlay.png')) "The obsolete backstab asset remains referenced."
Assert-Contract ([regex]::IsMatch($source, '"BackstabReadyColor",\s*"#8C0003FF"')) "The backstab color does not default to killing-blow dark red."
Assert-Contract ([regex]::IsMatch($source, 'color\.a \*= UnderlyingCrosshairOpacityMultiplier\(\);[\s\S]*?ApplyCenterVisuals[\s\S]*?ApplyHitMarkerVisual\(\);[\s\S]*?ApplyBackstabReadyOverlay')) "The base reticle and hit-marker path are not dimmed beneath the topmost backstab indicator."
Assert-Contract ([regex]::Matches($source, 'color\.a \*= UnderlyingCrosshairOpacityMultiplier\(\);').Count -ge 2) "The independently colored reticle and hit-marker layers do not apply backstab dimming."
Assert-Contract ([regex]::IsMatch($source, 'ApplyStealthEyeVisual\(\s*reticleColor\.a,')) "The stealth eye does not inherit the already-dimmed reticle alpha."

Assert-Contract ($ambushSource.Contains('public static class AmbushIntegrityApi')) "Ambush Integrity API is missing."
Assert-Contract ($ambushSource.Contains('public const int ApiVersion = 1;')) "Ambush Integrity API is not v1."
Assert-Contract ($ambushSource.Contains('public static int GetBackstabOpportunityState()')) "Backstab state query is missing."
Assert-Contract ($ambushSource.Contains('NpcElement raycastTarget = GetCurrentRaycastNpc();')) "The API does not read the current raycast target."
Assert-Contract ($ambushSource.Contains('ReferenceEquals(_backstabReadyTarget, raycastTarget)')) "The API does not guard exact current-target identity."
Assert-Contract ($ambushSource.Contains('Time.unscaledTime > _backstabReadyUntil')) "The API does not reject stale state."

Assert-Contract (Test-Path -LiteralPath $assetPath -PathType Leaf) "The backstab-ready overlay PNG is missing."
Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::new($assetPath)
try {
    Assert-Contract ($bitmap.Width -eq 512 -and $bitmap.Height -eq 512) "The backstab image is not 512x512."
    Assert-Contract ($bitmap.GetPixel(0, 0).A -eq 0) "The overlay corner is not transparent."
} finally {
    $bitmap.Dispose()
}

Write-Output "Dishonored Dynamic Crosshair Ambush Integrity contracts passed."
