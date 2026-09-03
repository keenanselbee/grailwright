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
$soulAndServiceRoot = Join-Path $repoRoot "mods\SoulAndService"
$soulAndServicePluginSource = Get-Content -LiteralPath (
    Join-Path $soulAndServiceRoot "src\SoulAndService.cs") -Raw
$soulAndServiceSalvageSource = Get-Content -LiteralPath (
    Join-Path $soulAndServiceRoot "src\SoulSalvageRuntime.cs") -Raw
$soulAndServiceSource = $soulAndServicePluginSource + "`n" + $soulAndServiceSalvageSource

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

if ($soulAndServicePluginSource -notmatch '\[BepInDependency\(\s*(?:EyesInTheDarkPluginGuid|"ks\.tgfoa\.eyes-in-the-dark"),\s*BepInDependency\.DependencyFlags\.SoftDependency\)\]') {
    throw 'Soul and Service must soft-depend on Eyes in the Dark.'
}

foreach ($required in @(
    'EyesInTheDarkCorpseDrainApiTypeName =',
    '"EyesInTheDark.EyesInTheDarkCorpseDrainApi"',
    '"TryRegisterCorpseDrain"',
    'BindingFlags.Public | BindingFlags.Static',
    'new[] { typeof(float) }',
    'Mathf.Clamp01(quality)')) {
    if (!$soulAndServiceSource.Contains($required)) {
        throw "Soul and Service corpse-harvest bridge contract is missing: $required"
    }
}

if ($soulAndServiceSource -notmatch '(?s)Chainloader\.PluginInfos\.TryGetValue\(\s*(?:EyesInTheDarkPluginGuid|"ks\.tgfoa\.eyes-in-the-dark"),.*?EyesInTheDarkCorpseDrainApiTypeName.*?GetMethod\(\s*"TryRegisterCorpseDrain",\s*BindingFlags\.Public \| BindingFlags\.Static,\s*null,\s*new\[\] \{ typeof\(float\) \},\s*null\)') {
    throw 'Soul and Service does not resolve the versioned Eyes corpse-drain API through its loaded plugin assembly.'
}

if ($soulAndServiceSource -notmatch '(?s)(?:private|internal|public)\s+(?:static\s+)?void Report\w*Eyes\(float quality\).*?\.Invoke\(\s*null,\s*new object\[\] \{ Mathf\.Clamp01\(quality\) \}\)') {
    throw 'Soul and Service must clamp harvest quality before invoking the optional Eyes corpse-drain API.'
}

$normalHarvestPattern = '(?s)private static void TryHarvestCorpse\(Location corpse\).*?(?=private static void ApplySoulRend\()'
$normalHarvestBlock = [regex]::Match(
    $soulAndServiceSalvageSource,
    $normalHarvestPattern).Value
$hasSuccessfulHarvestGate = $normalHarvestBlock -match '(?s)bool harvested = executedServant.*?if \(!harvested\).*?return;'
$hasPostRollbackNormalReport = $normalHarvestBlock -match '(?s)if \(canSafelySimplify\s*&& !simplified\s*&& !broadCurrentSessionHarvest\).*?RollbackCorpseHarvest\(harvestReceipt\);.*?return;.*?if \(executedServant\)\s*\{.*?\}\s*else\s*\{\s*Report\w*Eyes\(quality01\);'
if ([string]::IsNullOrWhiteSpace($normalHarvestBlock) -or !$hasSuccessfulHarvestGate -or !$hasPostRollbackNormalReport) {
    throw 'Soul and Service must report Eyes threat only after a successful, non-servant TryHarvestCorpse transaction that cannot be rolled back.'
}

$highSoulDrainBlock = [regex]::Match(
    $soulAndServiceSalvageSource,
    '(?s)private static void TryDrainHighSoulCorpse\(.*?(?=private static void TryHarvestCorpse\()').Value
if ([string]::IsNullOrWhiteSpace($highSoulDrainBlock) -or
    $highSoulDrainBlock -match 'Report\w*Eyes') {
    throw 'Soul and Service must not report Eyes threat for the repeatable TryDrainHighSoulCorpse path.'
}

Write-Host "Eyes in the Dark, Blood Magic Expansion, and Soul and Service corpse-drain threat contracts passed."
