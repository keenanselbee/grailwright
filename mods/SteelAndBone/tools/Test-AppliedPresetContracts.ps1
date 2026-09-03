[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$modRoot = Split-Path -Parent $PSScriptRoot
$main = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'src\SteelAndBone.cs')
$difficulty = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'src\DifficultyOverhaul.cs')
$testMatrix = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'docs\TEST-MATRIX.md')
$source = $main + "`n" + $difficulty

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text -notmatch $Pattern) {
        throw "Steel and Bone applied-preset contract failed: $Message"
    }
}

Assert-Contains $main 'private enum Preset\s*\{\s*Tempered,\s*Hardened,\s*Crucible,\s*Custom\s*\}' 'preset choices must progress from Tempered through Crucible with Custom last.'
Assert-Contains $main 'Config\.Bind\("General", "Preset", Preset\.Hardened' 'Hardened must remain the fresh-config default.'
Assert-Contains $main '"Difficulty Preset", "Difficulty Preset", 10, 0' 'the selector must own its dedicated first preset section.'
Assert-Contains $main 'ConfigRecoveryPermanentExclusions\s*=\s*new ConfigDefinition\[0\];' 'the durable applied selector and values must remain eligible for previous-settings import.'
Assert-Contains $main 'RestorePreservedSetting\(profile, _preset' 'automatic typed recovery must retain eligible schema-31-and-later selector values.'
foreach ($section in @(
    'Preset - Combat and Materials',
    'Preset - Player Pressure and Recovery',
    'Preset - Enemies and Progression')) {
    Assert-Contains $source ([regex]::Escape('"' + $section + '"')) "missing adjacent display section $section."
}

$presetValues = [ordered]@{
    Tempered = [ordered]@{
        MaterialRuleIntensity = '0.55f'
        VanillaAmplificationStrength = '0.0f'
        WeakSpotDamageBonus = '0.10f'
        PositiveBonusMultiplier = '1.0f'
        EffectivenessFeedbackSensitivity = '1.20f'
        PlayerDamagePressure = '0.05f'
        SupportingPressure = '0.0f'
        DashStaminaCostMultiplier = '1.0f'
        PassiveShieldProtectionShare = '0.08f'
        PotionPoisoningWindowSeconds = '5.0f'
        FoodHealthRateMultiplier = '0.50f'
        FoodHealthDurationMultiplier = '4.0f'
        FoodStaminaPerSecond = '1.0f'
        PreventFoodUseInCombat = 'false'
        ArrowVelocityMultiplier = '1.10f'
        HostileArcherAimScatter = '1.50f'
        EnemyAwarenessMultiplier = '1.20f'
        EnemyAttackSlotBonus = '0'
        EnemyMovementSpeedMultiplier = '1.0f'
        TenacityFactor = '0.75f'
        ExperienceMultiplier = '0.95f'
    }
    Hardened = [ordered]@{
        MaterialRuleIntensity = '1.0f'
        VanillaAmplificationStrength = '0.35f'
        WeakSpotDamageBonus = '0.20f'
        PositiveBonusMultiplier = '0.75f'
        EffectivenessFeedbackSensitivity = '1.10f'
        PlayerDamagePressure = '0.10f'
        SupportingPressure = '0.05f'
        DashStaminaCostMultiplier = '1.15f'
        PassiveShieldProtectionShare = '0.10f'
        PotionPoisoningWindowSeconds = '10.0f'
        FoodHealthRateMultiplier = '0.375f'
        FoodHealthDurationMultiplier = '4.0f'
        FoodStaminaPerSecond = '1.0f'
        PreventFoodUseInCombat = 'true'
        ArrowVelocityMultiplier = '1.30f'
        HostileArcherAimScatter = '1.25f'
        EnemyAwarenessMultiplier = '1.40f'
        EnemyAttackSlotBonus = '1'
        EnemyMovementSpeedMultiplier = '1.05f'
        TenacityFactor = '1.0f'
        ExperienceMultiplier = '0.90f'
    }
    Crucible = [ordered]@{
        MaterialRuleIntensity = '1.35f'
        VanillaAmplificationStrength = '0.70f'
        WeakSpotDamageBonus = '0.30f'
        PositiveBonusMultiplier = '0.50f'
        EffectivenessFeedbackSensitivity = '1.0f'
        PlayerDamagePressure = '0.15f'
        SupportingPressure = '0.10f'
        DashStaminaCostMultiplier = '1.30f'
        PassiveShieldProtectionShare = '0.12f'
        PotionPoisoningWindowSeconds = '15.0f'
        FoodHealthRateMultiplier = '0.25f'
        FoodHealthDurationMultiplier = '4.0f'
        FoodStaminaPerSecond = '1.0f'
        PreventFoodUseInCombat = 'true'
        ArrowVelocityMultiplier = '1.50f'
        HostileArcherAimScatter = '1.0f'
        EnemyAwarenessMultiplier = '1.60f'
        EnemyAttackSlotBonus = '2'
        EnemyMovementSpeedMultiplier = '1.10f'
        TenacityFactor = '1.25f'
        ExperienceMultiplier = '0.85f'
    }
}

foreach ($preset in $presetValues.Keys) {
    $match = [regex]::Match(
        $difficulty,
        '(?s)case Preset\.' + [regex]::Escape($preset) + ':\s*tuning = new AppliedPresetTuning\s*\{(?<body>.*?)\};')
    if (!$match.Success) {
        throw "Steel and Bone applied-preset contract failed: missing $preset tuning block."
    }

    $body = $match.Groups['body'].Value
    $assignedProperties = [regex]::Matches($body, '(?m)^\s*(?<property>[A-Za-z0-9]+)\s*=') |
        ForEach-Object { $_.Groups['property'].Value }
    $uniqueAssignedProperties = @($assignedProperties | Select-Object -Unique)
    $expectedPropertyCount = $presetValues[$preset].Count
    $completeAssignmentSet = $assignedProperties.Count -eq $expectedPropertyCount -and
        $uniqueAssignedProperties.Count -eq $expectedPropertyCount
    if (!$completeAssignmentSet) {
        throw "Steel and Bone applied-preset contract failed: $preset must assign the complete tuning matrix exactly once."
    }

    foreach ($entry in $presetValues[$preset].GetEnumerator()) {
        $assignmentPattern = '(?m)^\s*' + [regex]::Escape($entry.Key) +
            '\s*=\s*' + [regex]::Escape($entry.Value) + ',?\s*$'
        if ($body -notmatch $assignmentPattern) {
            throw "Steel and Bone applied-preset contract failed: $preset is missing exact assignment $($entry.Key) = $($entry.Value)."
        }
    }
}

$governedBlock = [regex]::Match(
    $difficulty,
    '(?s)private ConfigEntryBase\[\] GetPresetValueSettings\(\).*?return new ConfigEntryBase\[\]\s*\{(?<body>.*?)\};')
if (!$governedBlock.Success) {
    throw 'Steel and Bone applied-preset contract failed: governed setting list is missing.'
}
$governedFields = [regex]::Matches($governedBlock.Groups['body'].Value, '_[A-Za-z0-9]+') |
    ForEach-Object Value
if ($governedFields.Count -ne 23 -or ($governedFields | Select-Object -Unique).Count -ne 23) {
    throw "Steel and Bone applied-preset contract failed: expected 23 unique governed settings; found $($governedFields.Count)."
}

foreach ($field in $governedFields) {
    $assignments = [regex]::Matches(
        $difficulty,
        [regex]::Escape($field) + '\.Value\s*=\s*tuning\.').Count
    if ($assignments -ne 1) {
        throw "Steel and Bone applied-preset contract failed: $field must have one centralized applied assignment; found $assignments."
    }
}

foreach ($contract in @(
    'private void ApplySelectedPreset\(\)[\s\S]{0,250}_preset\.Value == Preset\.Custom',
    'private void OnDifficultySettingChanged[\s\S]{0,900}_preset\.Value = Preset\.Custom;',
    'private void Update\(\)\s*\{\s*RefreshFoaModManagerIfPending\(\);',
    'AccessTools\.TypeByName\("FoAModManager\.FoAModManagerApi"\)[\s\S]{0,250}AccessTools\.Method\(apiType, "Refresh"\)[\s\S]{0,250}refreshMethod\.Invoke\(null, null\);',
    'PrepareAppliedPresetRecovery\(\);\s*RestorePreservedConfigSettings\(\);\s*CompleteAppliedPresetRecovery\(\);[\s\S]{0,350}ApplySelectedPreset\(\);\s*Config\.Save\(\);',
    'private void Awake\(\)[\s\S]{0,500}BindConfig\(\);[\s\S]{0,500}InitializeDifficultyOverhaul\(\);',
    'private void InitializeDifficultyOverhaul\(\)\s*\{\s*Config\.SettingChanged \+= OnDifficultySettingChanged;',
    'presetPrefix = "Preset ="[\s\S]{0,500}_pendingConfigRecoveryPreset = parsedPreset;',
    'oldAmplificationKey[\s\S]{0,500}_vanillaAmplificationStrength\.Value',
    'matchesNamedPreset[\s\S]{0,250}Preset\.Custom')) {
    Assert-Contains $source $contract "missing lifecycle or matrix contract: $contract"
}

foreach ($runtimeContract in @(
    '_materialRuleIntensity\.Value',
    '_vanillaAmplificationStrength\.Value',
    '_playerDamagePressure\.Value',
    '_supportingPressure\.Value',
    '_arrowVelocityMultiplier\.Value',
    '_enemyAwarenessMultiplier\.Value',
    '_potionPoisoningWindowSeconds\.Value',
    '_foodHealthRateMultiplier\.Value',
    '_foodHealthDurationMultiplier\.Value',
    '_foodStaminaPerSecond\.Value',
    '_tenacityFactor\.Value',
    '_experienceMultiplier\.Value')) {
    Assert-Contains $source $runtimeContract "runtime does not read $runtimeContract."
}

if ($source -match 'switch\s*\(\s*_preset\.Value\s*\)') {
    throw 'Steel and Bone applied-preset contract failed: runtime still branches directly on the selector.'
}
if ($source -match '_temperedVanillaAmplification|_hardenedVanillaAmplification|_crucibleVanillaAmplification') {
    throw 'Steel and Bone applied-preset contract failed: inactive parallel amplification settings remain bound.'
}

Assert-Contains $testMatrix 'SAB-SMOKE-PRESET-01[\s\S]*all 23 governed values[\s\S]*selects Custom[\s\S]*Named and Custom states persist across reload' 'the focused in-game preset smoke row is missing or incomplete.'

Write-Output 'Steel and Bone applied-preset contracts passed.'
