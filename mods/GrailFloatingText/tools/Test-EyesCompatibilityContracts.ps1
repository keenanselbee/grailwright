$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$eyesSource = Get-Content -LiteralPath (
    Join-Path (Split-Path -Parent $modRoot) "EyesInTheDark\src\EyesInTheDark.cs") -Raw
$eyesBridge = Get-Content -LiteralPath (
    Join-Path (Split-Path -Parent $modRoot) "EyesInTheDark\src\GrailFloatingTextBridge.cs") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

if ($source -notmatch 'public const string PluginVersion = "(?<version>[0-9]+\.[0-9]+\.[0-9]+)";') {
    throw "GFT source PluginVersion was not found."
}
if ($manifest.version -ne $matches.version) {
    throw "GFT manifest version '$($manifest.version)' does not match source PluginVersion '$($matches.version)'."
}
foreach ($required in @(
    'ResolveEyesWyrdStyle()',
    '"WyrdnessPalette"',
    '"NativeOrange"',
    'private const int ConfigSchemaVersion = 28;',
    'public const int ApiVersion = 13;',
    '"BuiltInEventClaims"',
    'TrySetBuiltInEventClaim(',
    'IsBuiltInEventClaimed(VanillaWyrdNightEventId)',
    '"ks.tgfoa.eyes-in-the-dark"',
    '"DeathWrench.TimeMod"',
    '"TimeMod"',
    '"eyes-in-the-dark-custom-timescale"',
    '"Custom Timescale is flagged as incompatible with Eyes in the Dark."',
    'ScanEyesInTheDarkCompatibility();',
    'ShowCompatibilityNotice(')) {
    if (!$source.Contains($required)) {
        throw "GFT Eyes compatibility contract is missing token: $required"
    }
}

foreach ($required in @(
    'BindGftBuiltInEventClaims();',
    'AtmosphereEventKind.NightBegin',
    'AtmosphereEventKind.NightEnd',
    '"vanilla-wyrd-night"')) {
    if (!$eyesSource.Contains($required)) {
        throw "Eyes built-in event ownership contract is missing token: $required"
    }
}

foreach ($required in @(
    'TrySetBuiltInEventClaim(',
    'ReleaseBuiltInEventClaims();',
    'EyesInTheDarkPlugin.PluginGuid')) {
    if (!$eyesBridge.Contains($required)) {
        throw "Eyes GFT bridge ownership contract is missing token: $required"
    }
}

$noticeCount = ([regex]::Matches(
    $source,
    [regex]::Escape(
        'Custom Timescale is flagged as incompatible with Eyes in the Dark.'))).Count
if ($noticeCount -ne 1) {
    throw "Expected the exact Custom Timescale notice text once; found $noticeCount."
}

Write-Host "GFT Eyes/Custom Timescale compatibility contracts passed."
