$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$api = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\API.md") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw

$methodMatch = [regex]::Match(
    $source,
    '(?s)private void ShowCompatibilityNotice\(.+?(?=\r?\n\s*private static bool IsPluginOrAssemblyLoaded\()')
if (!$methodMatch.Success) {
    throw "GFT ShowCompatibilityNotice method was not found."
}

$method = $methodMatch.Value
foreach ($required in @(
    '"Warning"',
    '"System"',
    '"High"',
    'eventId,',
    '"warning"',
    '"OnMainMenu"',
    '-1.0f',
    '1.0f')) {
    if (!$method.Contains($required)) {
        throw "GFT compatibility notification contract is missing token: $required"
    }
}
if ($method.Contains('"Critical"') -or $method.Contains('"system"')) {
    throw "GFT compatibility notifications must not use Critical priority or the neutral system icon."
}

foreach ($document in @(
    @{ Name = "README"; Text = $readme },
    @{ Name = "API guide"; Text = $api },
    @{ Name = "Nexus description"; Text = $nexus })) {
    foreach ($required in @("Warning", "System", "High", "warning", "OnMainMenu")) {
        if (!$document.Text.Contains($required)) {
            throw "$($document.Name) compatibility guidance is missing token: $required"
        }
    }
}

if ($api.Contains('critical compatibility warning') -or
    $nexus.Contains('"System", "System", "High", "compat-other-mod", "system"')) {
    throw "GFT author guidance still contains the superseded compatibility presentation."
}

Write-Host "GFT compatibility notification contracts passed."
