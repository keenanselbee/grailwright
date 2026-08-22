$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\Plugin.cs") -Raw
$readme = Get-Content -LiteralPath (Join-Path $modRoot "README.txt") -Raw

foreach ($required in @(
    '[BepInDependency("ks.tgfoa.soul-and-service", BepInDependency.DependencyFlags.SoftDependency)]',
    'SoulAndService.SoulAndServiceApi',
    '!object.Equals(version.GetRawConstantValue(), 5)',
    'GetFocusedSoulSalvageTargetState',
    'GetFocusedSoulSalvageQuality01',
    'GetFocusedSoulSalvageQualityTier',
    'new object[] { true }',
    'SoulSalvageReticleColor = "#22A886FF"',
    'IsTypeOrBaseNamed(action, "SummonCommandAction")',
    'InteractionIconKind.Attack',
    'InteractionIconKind.Hold',
    'InteractionIconKind.Follow',
    'InteractionIconKind.Behavior',
    'return "interaction_command_attack.png";',
    'return "interaction_command_hold.png";',
    'return "interaction_command_follow.png";',
    'return "interaction_command_behavior.png";',
    'GetSummonCommandSequence',
    'DefaultSummonCommandPulseSeconds = 0.675f',
    'GetLastSummonCommandPulseSeconds',
    '_soulAndServiceGetCommandPulseSecondsMethod.Invoke(',
    '_lastSoulSalvageQualityTier',
    '&& !qualityRitualActive',
    '_bloodMagicQualityAssets.TryGetValue(')) {
    if (!$source.Contains($required)) { throw "Missing Soul and Service reticle contract: $required" }
}
if ($source -notmatch '(?s)if \(Time\.unscaledTime >= _nextContextCheckTime\).*?bool soulSalvageActive = ReadSoulSalvageTargetActive\(\);.*?bool bloodMagicActive = !soulSalvageActive\s*&& ReadBloodMagicCorpseActive\(\);') {
    throw "Periodic reticle refresh does not poll Soul and Service before Blood Magic."
}
if ($source -notmatch '(?s)float pulseSeconds = Mathf\.Clamp\(.*?_soulAndServiceGetCommandPulseSecondsMethod\.Invoke\(.*?_summonCommandPulseEndsAt = Time\.unscaledTime.*?pulseSeconds') {
    throw "Soul and Service command pulses do not use the authoritative per-command duration."
}
if ($source -notmatch '(?s)String\.Equals\(\s*actionName as string,\s*"Attack".*?\|\| String\.Equals\(\s*actionName as string,\s*"Swarm".*?return InteractionIconKind\.Attack;') {
    throw "Soul and Service Swarm prompts do not use the Attack interaction icon."
}
foreach ($required in @(
    'soulSalvageActive != _currentSoulSalvageTargetActive',
    'soulSalvageQualityTier != _currentSoulSalvageQualityTier',
    '_currentSoulSalvageQuality01')) {
    if (!$source.Contains($required)) { throw "Missing live Soul reticle refresh contract: $required" }
}
foreach ($required in @(
    'eligible corpses and active owned summons',
    'Soul and Service commands use',
    'interaction_command_attack.png',
    'interaction_command_hold.png',
    'interaction_command_follow.png',
    'interaction_command_behavior.png',
    '#22A886 Necrotic green',
    'perceptual brightness')) {
    if (!$readme.Contains($required)) { throw "Missing Soul and Service reticle documentation: $required" }
}

Write-Output "Dishonored Dynamic Crosshair Soul and Service integration contracts passed."
