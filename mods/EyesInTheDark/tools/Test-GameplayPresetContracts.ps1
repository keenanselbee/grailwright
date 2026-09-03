[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs")
$catalog = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\HunterCatalog.cs")

function Get-Block {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Signature
    )

    $match = [regex]::Match(
        $Source,
        "(?s)$Signature.+?(?=\r?\n\s*private )")
    if (!$match.Success) {
        throw "Missing gameplay-preset block: $Signature"
    }
    return $match.Value
}

if ($plugin -notmatch 'private const int ConfigSchemaVersion = 23;') {
    throw 'Applied gameplay presets require config schema 23.'
}
if ($catalog -notmatch
    'internal enum GameplayTuningPreset\s*\{\s*UneasyNight,\s*WatchfulNight,\s*CursedNight,\s*Custom\s*\}') {
    throw 'Gameplay presets must progress from Uneasy through Cursed with Custom last.'
}
if ($plugin -notmatch
    '"ApplyPreset",\s*GameplayTuningPreset\.WatchfulNight,[\s\S]{0,500}"Gameplay Preset",\s*"Gameplay Preset",\s*11,\s*0') {
    throw 'Watchful Night must be the durable default and lead its display section.'
}

$governedFields = @(
    '_allowUnprotectedWyrdnightRest',
    '_restInterruptionChanceAtZeroThreat',
    '_restInterruptionChanceAtMaximumThreat',
    '_passiveThreatPerNight',
    '_sprintThreatPerMinute',
    '_combatThreatPerWindow',
    '_wyrdKillThreat',
    '_corpseDrainThreatAtAverageQuality',
    '_baseDangerBudget',
    '_longNightBonusScale',
    '_maximumLongNightBonus',
    '_baseHazardPerMinute',
    '_threatHazardPerMinute',
    '_nightProgressHazardPerMinute',
    '_minimumHazardTarget',
    '_maximumHazardTarget',
    '_warningSeconds',
    '_maximumPackSize',
    '_sidecarChance',
    '_allowEliteEnemies',
    '_enableAmbientStalkers',
    '_stalkerMinimumCooldown',
    '_stalkerMaximumCooldown',
    '_stalkerMaximumCooldownAtFiftyThreat',
    '_stalkerProvocationThreat',
    '_killRecoverySeconds',
    '_escapeRecoverySeconds',
    '_failedPlacementRecoverySeconds')

$settingsBlock = Get-Block $plugin `
    'private ConfigEntryBase\[\] GetGameplayPresetValueSettings\('
foreach ($field in $governedFields) {
    if ($settingsBlock.IndexOf($field, [StringComparison]::Ordinal) -lt 0) {
        throw "The gameplay preset governed-setting list is missing $field."
    }
}

$applyBlock = Get-Block $plugin `
    'private void ApplySelectedGameplayPreset\('
$normalizedApply = [regex]::Replace($applyBlock, '\s+', ' ')
if ($normalizedApply.Contains(
        '_gameplayPreset.Value = GameplayTuningPreset.Custom;')) {
    throw 'Applying a named gameplay preset must not immediately return it to Custom.'
}
foreach ($field in $governedFields) {
    $occurrences = [regex]::Matches(
        $applyBlock,
        [regex]::Escape("$field.Value =")).Count
    if ($occurrences -ne 3) {
        throw "$field must be assigned exactly once by each of the three named presets; found $occurrences assignments."
    }
}

foreach ($contract in @(
    'ConfigPreviousSettingsRecovery\.Bind\([\s\S]{0,350}ApplySelectedGameplayPreset\(\);\s*Config\.SettingChanged \+= OnConfigSettingChanged;\s*Config\.Save\(\);',
    'private void OnConfigSettingChanged[\s\S]{0,1200}_gameplayPreset\.Value = GameplayTuningPreset\.Custom;',
    'private void ApplySelectedGameplayPreset[\s\S]{0,250}_gameplayPreset\.Value == GameplayTuningPreset\.Custom',
    'private void Update\(\)\s*\{\s*RefreshFoaModManagerIfPending\(\);',
    'AccessTools\.TypeByName\(\s*"FoAModManager\.FoAModManagerApi"\)[\s\S]{0,250}AccessTools\.Method\(apiType, "Refresh"\)[\s\S]{0,250}refreshMethod\.Invoke\(null, null\);',
    '"Preset - Rest and Threat"',
    '"Preset - Hunt Pressure"',
    '"Preset - Stalkers and Recovery"',
    'HasPendingPreservedGameplayPresetValue\(\)[\s\S]{0,200}RestorePreservedConfigValues\(\);[\s\S]{0,200}_gameplayPreset\.Value = GameplayTuningPreset\.Custom;',
    'CapturePreservedValue<GameplayTuningPreset>\([\s\S]{0,150}"2\. Gameplay Preset",\s*"ApplyPreset"\);',
    'RestorePreservedValue\(_gameplayPreset,')) {
    if ($plugin -notmatch $contract) {
        throw "Missing durable gameplay-preset contract: $contract"
    }
}

$exclusions = [regex]::Match(
    $plugin,
    '(?s)ConfigRecoveryPermanentExclusions\s*=.+?;\r?\n')
if (!$exclusions.Success -or
    $exclusions.Value.Contains('"ApplyPreset"')) {
    throw 'The durable gameplay preset must not remain a permanently excluded pseudo-button.'
}

Write-Host "Eyes in the Dark applied gameplay-preset contracts passed."
