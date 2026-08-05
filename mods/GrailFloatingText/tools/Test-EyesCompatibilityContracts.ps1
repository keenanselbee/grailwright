$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

if ($manifest.version -ne "1.9.9") {
    throw "GFT manifest version is not 1.9.9."
}
foreach ($required in @(
    'public const string PluginVersion = "1.9.9";',
    'ResolveEyesWyrdStyle()',
    '"WyrdnessPalette"',
    '"NativeOrange"',
    'private const int ConfigSchemaVersion = 24;',
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

$noticeCount = ([regex]::Matches(
    $source,
    [regex]::Escape(
        'Custom Timescale is flagged as incompatible with Eyes in the Dark.'))).Count
if ($noticeCount -ne 1) {
    throw "Expected the exact Custom Timescale notice text once; found $noticeCount."
}

Write-Host "GFT Eyes/Custom Timescale compatibility contracts passed."
