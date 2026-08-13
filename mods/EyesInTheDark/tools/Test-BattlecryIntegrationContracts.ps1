$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$atmosphereSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\Atmosphere.cs") -Raw
$threatSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatState.cs") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

if ($manifest.version -ne "1.3.4") {
    throw "Eyes in the Dark battlecry integration requires manifest version 1.3.4."
}

foreach ($required in @(
    "public static class EyesInTheDarkBattlecryApi",
    "public static int ContractVersion",
    "public static bool TryRegisterBattlecry(float threatAmount)",
    "internal bool TryRegisterBattlecry(float requestedThreat)",
    "BattlecryThreatResetSeconds = 30.0f",
    "MinimumBattlecryThreatMultiplier = 0.1f",
    "Math.Pow(0.5d, _recentBattlecryCount)",
    "ThreatChangeCause.Battlecry",
    "MinimumBattlecriesPerResponse = 2",
    "MaximumBattlecriesPerResponse = 3",
    "DefaultBattlecryResponseCooldownSeconds = 15.0f",
    '"BattlecryResponseCooldownSeconds"',
    '"eyes-in-the-dark-battlecry"',
    "ResetBattlecryState()")) {
    if (!$pluginSource.Contains($required)) {
        throw "Eyes in the Dark battlecry runtime contract is missing: $required"
    }
}

foreach ($required in @(
    "BattlecryResponse",
    "The Wyrdnight takes notice.",
    "The Wyrdnight reacts to your cries.",
    "Something answers without a voice.")) {
    if (!$atmosphereSource.Contains($required)) {
        throw "Battlecry atmosphere contract is missing: $required"
    }
}

if (!$threatSource.Contains("Battlecry,")) {
    throw "Battlecry threat-change cause is missing."
}

Write-Host "Eyes in the Dark battlecry API, diminishing threat, and atmosphere contracts passed."
