$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulAndService.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw
$matrix = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\TEST-MATRIX.md") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

foreach ($required in @(
    'PluginVersion = "0.3.8"',
    'ConfigSchemaVersion = 3',
    '"ReanimationMinimumLifetimeSeconds"',
    '"ReanimationHealthDecayPercentPerSecond"',
    '"ReanimationFlatHealthDecayPerSecond"')) {
    if (!$pluginSource.Contains($required)) {
        throw "Soul Salvage plugin configuration is missing: $required"
    }
}

foreach ($forbidden in @(
    'internal ConfigEntry<float> ReanimationDurationSeconds',
    'internal ConfigEntry<float> ReanimationHealthPercent')) {
    if ($pluginSource.Contains($forbidden)) {
        throw "Soul Salvage plugin retains replaced configuration: $forbidden"
    }
}

foreach ($required in @(
    'HeavyCastManaCostMultiplier = 3.0f',
    'StatTweak.Multi(',
    'stats.HeavyCastManaCost',
    'get_MagicDescription',
    'LightCastInfos.Contains(__instance)',
    'HeavyCastInfos.Contains(__instance)',
    'npc.Health.SetToFull()',
    'npc.RemoveElementsOfType<NpcHealthRegeneration>()',
    'UpdateReanimationDecay(Time.deltaTime)',
    'ReanimationHealthDecayPercentPerSecond.Value',
    'ReanimationFlatHealthDecayPerSecond.Value',
    'maximumAllowedDrain = maximumHealth / minimumLifetime',
    'npc.HealthElement.Kill()')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Soul Salvage runtime contract is missing: $required"
    }
}

foreach ($forbidden in @(
    'new TimeDuration(',
    'plugin.ReanimationDurationSeconds',
    'plugin.ReanimationHealthPercent')) {
    if ($runtimeSource.Contains($forbidden)) {
        throw "Soul Salvage runtime retains replaced behavior: $forbidden"
    }
}

if ([string]$manifest.version -ne '0.3.8') {
    throw "Soul Salvage manifest version is not 0.3.8."
}

foreach ($document in @($readme, $nexus)) {
    foreach ($required in @(
        'full health',
        '0.25%',
        '0.61 health',
        '90 seconds',
        'about three minutes',
        'stronger servants',
        'Combat damage')) {
        if (!$document.Contains($required)) {
            throw "Soul Salvage documentation is missing: $required"
        }
    }
}

foreach ($required in @(
    'Version under test: 0.3.8',
    'exactly 3x',
    'autonomously acquires',
    'draws enemy aggression',
    'decay alone never kills before 90 seconds')) {
    if (!$matrix.Contains($required)) {
        throw "Soul Salvage test matrix is missing: $required"
    }
}

Write-Host "Soul Salvage mana, tooltip, decay, and native-summon contracts passed."
