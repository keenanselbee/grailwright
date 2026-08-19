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
$exportScript = Get-Content -LiteralPath (
    Join-Path (Split-Path -Parent (Split-Path -Parent $modRoot)) "tools\Export-VortexPackage.ps1") -Raw

$methodMatch = [regex]::Match(
    $source,
    '(?s)internal bool TryShowCompatibilityNotice\(.+?(?=\r?\n\s*internal bool TryClaimXpGain\()')
if (!$methodMatch.Success) {
    throw "GFT TryShowCompatibilityNotice method was not found."
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

foreach ($required in @(
    'SupportsFeature',
    'TrySetBuiltInEventPresentationClaim',
    'InvokeTryShowDeferredEvent(',
    'InvokeTryShowEvent(')) {
    if (!$api.Contains($required)) {
        throw "GFT packaged API guide is missing current author guidance: $required"
    }
}

foreach ($required in @(
    '[BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]',
    'NotificationApi is currently v13',
    'TryShowCompatibilityNotice',
    'TrySetBuiltInEventPresentationClaim',
    'TrySetBuiltInEventClaim',
    'GrailFloatingText/docs/API.md')) {
    if (!$nexus.Contains($required)) {
        throw "GFT Nexus author quick start is missing current guidance: $required"
    }
}

foreach ($required in @(
    'function Copy-ApiReferenceToPackage',
    'Join-Path $Root "docs\API.md"',
    'Copy-ApiReferenceToPackage -Root $ModRoot -PackageRoot $packageRoot')) {
    if (!$exportScript.Contains($required)) {
        throw "GFT API reference is not guaranteed to enter direct-DLL packages: $required"
    }
}

Write-Host "GFT compatibility notification contracts passed."
