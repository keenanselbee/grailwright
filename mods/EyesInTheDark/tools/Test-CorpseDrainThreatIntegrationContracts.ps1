$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$eyesSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$threatSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatState.cs") -Raw
$bloodMagicRoot = Join-Path $repoRoot "mods\BloodMagicExpansion"
$bloodMagicSource = Get-Content -LiteralPath (
    Join-Path $bloodMagicRoot "src\BloodMagicExpansion.cs") -Raw

foreach ($required in @(
    "public static class EyesInTheDarkCorpseDrainApi",
    "public static bool TryRegisterCorpseDrain(float quality)",
    "internal bool TryRegisterCorpseDrain(float quality)",
    "DefaultCorpseDrainThreatAtAverageQuality = 8.0f",
    '"CorpseDrainThreatAtAverageQuality"',
    "ThreatState.CalculateCorpseDrainThreat(",
    "ThreatChangeCause.CorpseDrain")) {
    if (!$eyesSource.Contains($required)) {
        throw "Eyes in the Dark corpse-drain runtime contract is missing: $required"
    }
}

foreach ($required in @(
    "CorpseDrain,",
    "CalculateCorpseDrainThreat(",
    "averageQualityThreat * (0.5f + safeQuality)")) {
    if (!$threatSource.Contains($required)) {
        throw "Eyes in the Dark corpse-drain threat contract is missing: $required"
    }
}

foreach ($required in @(
    "[BepInDependency(EyesInTheDarkPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]",
    'EyesInTheDarkCorpseDrainApiTypeName =',
    '"EyesInTheDark.EyesInTheDarkCorpseDrainApi"',
    "ReportCorpseDrainThreatToEyes(quality);",
    '"TryRegisterCorpseDrain"')) {
    if (!$bloodMagicSource.Contains($required)) {
        throw "Blood Magic Expansion corpse-drain bridge contract is missing: $required"
    }
}

$completionPattern = '(?s)private void ReportCorpseDrained\(float quality\)\s*\{\s*ReportCorpseDrainThreatToEyes\(quality\);\s*ResolveDeedsOfAvalonBridge\(\);'
if ($bloodMagicSource -notmatch $completionPattern) {
    throw "Blood Magic Expansion must report Eyes threat before the optional Deeds bridge can return early."
}

Write-Host "Eyes in the Dark and Blood Magic Expansion corpse-drain threat contracts passed."
