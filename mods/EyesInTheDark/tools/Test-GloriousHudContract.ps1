$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$eyesSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$gloriousSource = Get-Content -LiteralPath (
    Join-Path $repoRoot "mods\GloriousUI\src\GloriousUI.cs") -Raw
$meterSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatMeter.cs") -Raw

foreach ($required in @(
    "public static class EyesInTheDarkHudApi",
    "ContractVersion",
    "RequestBelowVanillaBars(",
    '"ks.tgfoa.glorious-ui"',
    "_placeMeterBelowResourceBars")) {
    if (!$eyesSource.Contains($required)) {
        throw "Eyes in the Dark is missing HUD ownership contract token: $required"
    }
}

foreach ($required in @(
    "private const float StandaloneMeterBaselineOffsetX = 9.0f;",
    "private const float StandaloneMeterBaselineOffsetY = -9.0f;",
    "if (!_placeMeterBelowResourceBars)",
    "meterOffsetX += StandaloneMeterBaselineOffsetX;",
    "meterOffsetY += StandaloneMeterBaselineOffsetY;")) {
    if (!$eyesSource.Contains($required)) {
        throw "Eyes in the Dark is missing standalone meter-baseline token: $required"
    }
}

foreach ($setting in @("MeterOffsetX", "MeterOffsetY")) {
    if ($eyesSource -notmatch ('"' + $setting + '",\s*0f,')) {
        throw "Eyes in the Dark meter adjustment no longer defaults to zero: $setting"
    }
}

foreach ($required in @(
    '[BepInDependency("ks.tgfoa.eyes-in-the-dark"',
    '"EyesInTheDark.EyesInTheDarkHudApi"',
    '"RequestBelowVanillaBars"',
    "ResolveEyesInTheDarkIntegration();",
    "UpdateEyesInTheDarkPlacementRequest();",
    "ReleaseEyesInTheDarkPlacementRequest();")) {
    if (!$gloriousSource.Contains($required)) {
        throw "Glorious UI is missing Eyes placement-bridge token: $required"
    }
}

foreach ($retired in @(
    '[BepInDependency("kane.tgfoa.wyrd-hunt"',
    '"ShowWyrdHuntScentBar",',
    "ResolveWyrdHuntIntegration();",
    "UpdateWyrdHuntScentBar();",
    "PatchWyrdHuntMeterVisibility();",
    "WyrdHuntHudApi",
    "_wyrdHuntScentBarRoot",
    "TryMirrorWyrdHuntScentBarVisuals(")) {
    if ($gloriousSource.Contains($retired)) {
        throw "Glorious UI still activates retired Wyrd Hunt meter behavior: $retired"
    }
}

foreach ($required in @(
    '"The optional Wyrd Threat meter failed and was disabled; threat and encounters remain active:',
    '"The optional Wyrd boundary presentation failed and was disabled; threat and encounters remain active:',
    '_meter.Release();',
    '_meter = null;',
    '_boundary.Release();',
    '_boundary = null;',
    '_hasParsedBoundaryColor',
    '_parsedBoundaryColorText')) {
    if (!$eyesSource.Contains($required)) {
        throw "Eyes in the Dark is missing optional-presentation hardening token: $required"
    }
}

foreach ($required in @(
    '_lastExactThreatValue',
    'rounded != _lastExactThreatValue')) {
    if (!$meterSource.Contains($required)) {
        throw "Eyes in the Dark meter still lacks its exact-text allocation guard: $required"
    }
}

foreach ($removed in @(
    'TryPreserveTextureScrollDirection',
    'GetComponentsInChildren<TextureScroller>(true)',
    '_ownedScrollerMaterials')) {
    if ($meterSource.Contains($removed)) {
        throw "Eyes in the Dark retains ineffective mirrored texture-scroll code: $removed"
    }
}

Write-Host "Eyes in the Dark ownership and Glorious placement contracts passed."
