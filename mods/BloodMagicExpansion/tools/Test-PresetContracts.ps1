[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\BloodMagicExpansion.cs")

function Get-MethodBlock {
    param([Parameter(Mandatory = $true)][string]$MethodName)

    $match = [regex]::Match(
        $source,
        "(?s)private (?:void|bool|float) $MethodName\(.+?(?=\r?\n\s*private )")
    if (!$match.Success) {
        throw "Missing preset method: $MethodName"
    }

    return $match.Value
}

$presetValues = [ordered]@{
    BloodRite = [ordered]@{
        _customPayoutPercentOfKillXp = "30.0f"
        _secondsRequired = "1.0f"
        _customLiveDrainXpTickIntervalSeconds = "1.0f"
        _customLiveDrainXpPercentPerTick = "4.0f"
        _customLiveDrainMaximumXpPercentPerTarget = "20.0f"
    }
    Desecration = [ordered]@{
        _customPayoutPercentOfKillXp = "40.0f"
        _secondsRequired = "1.5f"
        _customLiveDrainXpTickIntervalSeconds = "1.5f"
        _customLiveDrainXpPercentPerTick = "6.0f"
        _customLiveDrainMaximumXpPercentPerTarget = "30.0f"
    }
    Exsanguination = [ordered]@{
        _customPayoutPercentOfKillXp = "45.0f"
        _secondsRequired = "2.0f"
        _customLiveDrainXpTickIntervalSeconds = "2.0f"
        _customLiveDrainXpPercentPerTick = "8.0f"
        _customLiveDrainMaximumXpPercentPerTarget = "40.0f"
    }
}

foreach ($preset in $presetValues.Keys) {
    $method = Get-MethodBlock "Apply$($preset)Preset"
    foreach ($entry in $presetValues[$preset].GetEnumerator()) {
        $assignment = "$($entry.Key).Value = $($entry.Value);"
        if ($method.IndexOf($assignment, [StringComparison]::Ordinal) -lt 0) {
            throw "$preset is missing preset assignment: $assignment"
        }
    }
    if ([regex]::Matches($method, '\.Value\s*=').Count -ne 5) {
        throw "$preset must write exactly the five ritual-economy values."
    }
}

foreach ($contract in @(
    'ConfigSchemaVersion = 30',
    'ApplySelectedPreset();',
    'config.SettingChanged += OnConfigSettingChanged;',
    'config.SettingChanged -= OnConfigSettingChanged;',
    '_preset.Value = Preset.Custom;',
    '"CustomCorpseXPPercent", 40.0f',
    '"CustomRitualSeconds", 1.5f',
    '"CustomLiveDrainTickSeconds", 1.5f',
    '"CustomLiveDrainXPPercentPerTick", 6.0f',
    '"CustomLiveDrainXPPercentCapPerTarget", 30.0f',
    'AccessTools.TypeByName(',
    '"FoAModManager.FoAModManagerApi"',
    'AccessTools.Method(apiType, "Refresh")',
    'return "Blood Magic Preset";')) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing applied-preset contract: $contract"
    }
}

if ($source -notmatch
    'private enum Preset\s*\{\s*BloodRite,\s*Desecration,\s*Exsanguination,\s*Custom\s*\}') {
    throw "Blood Magic preset choices must progress from Blood Rite through Exsanguination with Custom last."
}

$customHandler = Get-MethodBlock "OnConfigSettingChanged"
if ($customHandler.IndexOf(
        'IsPresetValueSetting(changedSetting)',
        [StringComparison]::Ordinal) -lt 0) {
    throw "Preset value changes do not select Custom through the governed-setting filter."
}

foreach ($getter in @(
    "GetPayoutPercentOfKillXp",
    "GetSecondsRequired",
    "GetLiveDrainXpTickIntervalSeconds",
    "GetLiveDrainXpPercentPerTick",
    "GetLiveDrainMaximumXpPercentPerTarget")) {
    $method = Get-MethodBlock $getter
    if ($method.IndexOf('_preset', [StringComparison]::Ordinal) -ge 0) {
        throw "$getter still branches on the preset instead of reading the live value."
    }
}

foreach ($strengthGetter in @(
    "GetBloodSpellProjectileTravelMultiplier",
    "GetBloodSpellTapDamageMultiplier",
    "GetBloodSpellHomingTargetSearchMultiplier",
    "GetBloodSpellHeldTargetRangeMultiplier",
    "GetBloodSpellBleedBuildupMultiplier",
    "GetBloodSpellTapCastSpeedMultiplier",
    "GetBloodSpellHeldChannelSpeedMultiplier",
    "GetAbhartachExplosionDamageMultiplier",
    "GetAbhartachExplosionRadiusMultiplier",
    "GetAbhartachExplosionBleedBuildupMultiplier",
    "GetAbhartachHeldCorpseHealingMultiplier",
    "GetAbhartachCorpseSearchRangeMultiplier")) {
    $method = Get-MethodBlock $strengthGetter
    if ($method.IndexOf('_preset', [StringComparison]::Ordinal) -ge 0) {
        throw "$strengthGetter still depends on the ritual preset."
    }
}

foreach ($removedPresetStrengthSetting in @(
    'CustomBloodSpellRangeMultiplier',
    'CustomBloodSpellTapDamageMultiplier',
    'CustomBloodSpellHomingSearchMultiplier',
    'CustomBloodSpellHeldRangeMultiplier',
    'CustomBloodSpellBleedMultiplier',
    'CustomBloodSpellTapSpeedMultiplier',
    'CustomBloodSpellHeldChannelSpeedMultiplier',
    'CustomAbhartachExplosionDamageMultiplier',
    'CustomAbhartachExplosionRadiusMultiplier',
    'CustomAbhartachExplosionBleedMultiplier',
    'CustomAbhartachHeldCorpseHealingMultiplier',
    'CustomAbhartachCorpseSearchMultiplier')) {
    if ($source.IndexOf($removedPresetStrengthSetting, [StringComparison]::Ordinal) -ge 0) {
        throw "Preset still governs spell strength through $removedPresetStrengthSetting."
    }
}

foreach ($progressionBase in @(
    'BloodSpellProjectileTravelProgressionBase = 1.06f',
    'BloodSpellTapDamageProgressionBase = 1.06f',
    'BloodSpellHomingSearchProgressionBase = 1.05f',
    'BloodSpellHeldRangeProgressionBase = 1.03f',
    'BloodSpellBleedProgressionBase = 1.06f',
    'BloodSpellTapSpeedProgressionBase = 1.06f',
    'BloodSpellHeldSpeedProgressionBase = 1.01f',
    'AbhartachExplosionDamageProgressionBase = 1.05f',
    'AbhartachExplosionRadiusProgressionBase = 1.10f',
    'AbhartachExplosionBleedProgressionBase = 1.12f',
    'AbhartachHeldHealingProgressionBase = 1.20f',
    'AbhartachCorpseSearchProgressionBase = 1.05f')) {
    if ($source.IndexOf($progressionBase, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing canonical Blood Power progression base: $progressionBase"
    }
}

Write-Host "Blood Magic Expansion applied-preset contracts passed."
