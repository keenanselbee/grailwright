$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$readme = Get-Content -LiteralPath (Join-Path $modRoot "README.txt") -Raw
$api = Get-Content -LiteralPath (Join-Path $modRoot "docs\API.md") -Raw
$nexus = Get-Content -LiteralPath (Join-Path $modRoot "nexus-full-desc.txt") -Raw

foreach ($required in @(
    'private ConfigEntry<bool> _showModDiagnosticMessages;',
    '"ShowModDiagnosticMessages",',
    'ShouldSuppressModDiagnosticMessage(',
    'IsModDiagnosticMessage(',
    'string.Equals(category, "Debug"',
    'string.Equals(style, "Debug"',
    'string.Equals(normalizedIconId, "debug"',
    'normalizedEventId.IndexOf("diagnostic"',
    'normalizedCollapseKey.IndexOf("diagnostic"',
    'VersatileWeaponsPluginGuid',
    'AllLightsCastShadowsAddonPluginGuid',
    'GlobalIlluminationAddonPluginGuid',
    '"gi-adaptive-tier"')) {
    if (!$source.Contains($required)) {
        throw "Global GFT diagnostic filter is missing token: $required"
    }
}

$suppressionCalls = [regex]::Matches(
    $source,
    'if \(ShouldSuppressModDiagnosticMessage\(').Count
if ($suppressionCalls -ne 2) {
    throw "Global GFT diagnostic filter must guard both immediate and deferred message ingestion. Found $suppressionCalls guards."
}

foreach ($document in @($readme, $api, $nexus)) {
    if (!$document.Contains("ShowModDiagnosticMessages")) {
        throw "Packaged GFT documentation is missing ShowModDiagnosticMessages."
    }
}

Write-Output "Global GFT diagnostic filter contract passed."
