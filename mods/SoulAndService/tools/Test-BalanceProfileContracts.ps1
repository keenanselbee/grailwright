param()

$ErrorActionPreference = 'Stop'

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginPath = Join-Path $modRoot 'src\SoulAndService.cs'
$progressionPath = Join-Path $modRoot 'src\SoulProgressionRuntime.cs'
$salvagePath = Join-Path $modRoot 'src\SoulSalvageRuntime.cs'
$summonPath = Join-Path $modRoot 'src\SummonRuntime.cs'

$plugin = Get-Content -Raw -LiteralPath $pluginPath
$progression = Get-Content -Raw -LiteralPath $progressionPath
$salvage = Get-Content -Raw -LiteralPath $salvagePath
$summon = Get-Content -Raw -LiteralPath $summonPath

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

Assert-Contains $plugin 'public enum BalanceProfile\s*\{\s*SoulFamine,\s*GravePact,\s*Dominion,\s*Custom\s*\}' 'BalanceProfile must expose the four authored profiles in order.'
Assert-Contains $plugin '"BalanceProfile",\s*BalanceProfile\.GravePact' 'Grave Pact must be the default profile.'
Assert-Contains $plugin 'case BalanceProfile\.GravePact:\s*return new SoulBalanceTuning\(1\.00f, 1\.00f, 1\.00f, 0\.00f\);' 'Grave Pact relative tuning changed unexpectedly.'
Assert-Contains $plugin 'case BalanceProfile\.Dominion:\s*return new SoulBalanceTuning\(1\.20f, 0\.80f, 1\.15f, 5\.00f\);' 'Dominion relative tuning changed unexpectedly.'
Assert-Contains $plugin 'case BalanceProfile\.SoulFamine:\s*default:\s*return new SoulBalanceTuning\(0\.80f, 1\.20f, 0\.85f, -5\.00f\);' 'Soul Famine relative tuning changed unexpectedly.'
Assert-Contains $plugin 'GravePactSoulVigorRewardBaseline = 1\.25f' 'Grave Pact reward baseline must remain x1.25.'
Assert-Contains $plugin 'GravePactServantUpkeepBaseline = 0\.75f' 'Grave Pact upkeep baseline must remain x0.75.'
Assert-Contains $plugin '"SoulVigorRewardMultiplier",\s*1\.00f,' 'Grave Pact reward modifier must be stored as x1.00.'
Assert-Contains $plugin '"ServantUpkeepMultiplier",\s*1\.00f,' 'Grave Pact upkeep modifier must be stored as x1.00.'
Assert-Contains $plugin '"RaisedStartingHealthMultiplier",\s*1\.00f,' 'Grave Pact starting Health must be the stored custom default.'
Assert-Contains $plugin 'ConfigRecoveryKeepCurrentDefaultRule\(\s*30,\s*"Custom Balance",\s*"SoulVigorRewardMultiplier"' 'Schema 30 must reject the previous direct reward meaning during recovery.'
Assert-Contains $plugin 'ConfigRecoveryKeepCurrentDefaultRule\(\s*30,\s*"Custom Balance",\s*"ServantUpkeepMultiplier"' 'Schema 30 must reject the previous direct upkeep meaning during recovery.'
Assert-Contains $plugin 'case "Core":\s*return string\.Equals\(\s*key,\s*"BalanceProfile",\s*StringComparison\.Ordinal\)\s*\? "Balance Preset"\s*:\s*section;' 'BalanceProfile must appear in the Balance Preset tab.'
Assert-Contains $plugin 'case "Custom Balance":\s*return "Balance Preset";' 'The four balance values must appear with the preset selector.'
Assert-Contains $plugin 'case "Balance Preset":\s*return 10;' 'Balance Preset must appear immediately after Core.'

foreach ($binding in @(
    'CustomSoulVigorRewardMultiplier',
    'CustomServantUpkeepMultiplier',
    'CustomRaisedStartingHealthMultiplier',
    'CustomSoulClaimThresholdAdjustment')) {
    Assert-Contains $plugin ($binding + ' = BindOrdered\(') "Missing durable custom binding $binding."
}

foreach ($capture in @(
    'CapturePreservedValue<BalanceProfile>\(profile, "Core", "BalanceProfile"\);',
    'CapturePreservedValue<float>\(profile, "Custom Balance", "SoulVigorRewardMultiplier"\);',
    'CapturePreservedValue<float>\(profile, "Custom Balance", "ServantUpkeepMultiplier"\);',
    'CapturePreservedValue<float>\(profile, "Custom Balance", "RaisedStartingHealthMultiplier"\);',
    'CapturePreservedValue<float>\(profile, "Custom Balance", "SoulClaimThresholdAdjustment"\);')) {
    Assert-Contains $plugin $capture "Balance profile schema recovery is missing $capture"
}

foreach ($restore in @(
    'BalanceProfileSetting',
    'CustomSoulVigorRewardMultiplier',
    'CustomServantUpkeepMultiplier',
    'CustomRaisedStartingHealthMultiplier',
    'CustomSoulClaimThresholdAdjustment')) {
    Assert-Contains $plugin ("RestorePreservedValue\(" + $restore + ", ref restored, ref clamped, ref invalid\);") "Balance profile schema recovery does not restore $restore."
}

Assert-Contains $plugin 'ApplySelectedBalancePreset\(\);\s*BindBalancePresetEvents\(\);\s*Config\.Save\(\);' 'Startup must apply a named preset before subscribing to manual edits.'
Assert-Contains $plugin 'private void OnBalancePresetChanged[\s\S]{0,300}ApplySelectedBalancePreset\(\);' 'Changing the preset must apply its complete value set.'
Assert-Contains $plugin 'private void OnBalanceValueChanged[\s\S]{0,500}BalanceProfileSetting\.Value = BalanceProfile\.Custom;' 'Changing a governed balance value must select Custom.'
Assert-Contains $plugin 'private void ApplySelectedBalancePreset[\s\S]{0,400}BalanceProfileSetting\.Value == BalanceProfile\.Custom[\s\S]{0,700}CustomSoulClaimThresholdAdjustment\.Value\s*=\s*tuning\.SoulClaimThresholdAdjustment;' 'Named presets must write all four values while Custom preserves them.'
Assert-Contains $plugin '_applyingBalancePreset = true;[\s\S]{0,700}_applyingBalancePreset = false;' 'Preset application must guard its own setting-change events.'
Assert-Contains $plugin 'private void Update\(\)\s*\{\s*RefreshFoaModManagerIfPending\(\);' 'FoA Mod Manager refresh must be deferred until the next frame.'
Assert-Contains $plugin 'private void OnBalancePresetChanged[\s\S]{0,400}_foaModManagerRefreshPending = true;' 'Preset changes must request a manager refresh.'
Assert-Contains $plugin 'private void OnBalanceValueChanged[\s\S]{0,700}_foaModManagerRefreshPending = true;' 'Manual balance edits must refresh the Custom selector.'
Assert-Contains $plugin 'AccessTools\.TypeByName\(\s*"FoAModManager\.FoAModManagerApi"\)[\s\S]{0,250}AccessTools\.Method\(apiType, "Refresh"\)[\s\S]{0,250}refreshMethod\.Invoke\(null, null\);' 'The optional FoA Mod Manager public refresh API must be invoked without a hard dependency.'

$effectiveTuningBody = [regex]::Match(
    $plugin,
    'internal static SoulBalanceTuning GetEffectiveBalanceTuning\(\)(?<body>[\s\S]*?)\n        private static SoulBalanceTuning GetConfiguredBalanceTuning').Groups['body'].Value
if ([string]::IsNullOrEmpty($effectiveTuningBody) -or
    $effectiveTuningBody -notmatch 'GetConfiguredBalanceTuning\(\)' -or
    $effectiveTuningBody -notmatch 'GravePactSoulVigorRewardBaseline\s*\* relative\.SoulVigorRewardMultiplier' -or
    $effectiveTuningBody -notmatch 'GravePactServantUpkeepBaseline\s*\* relative\.ServantUpkeepMultiplier') {
    throw 'Effective balance tuning must apply the fixed Grave Pact baselines to the visible relative values.'
}
$configuredTuningBody = [regex]::Match(
    $plugin,
    'private static SoulBalanceTuning GetConfiguredBalanceTuning\(\)(?<body>[\s\S]*?)\n        private static SoulBalanceTuning GetPresetBalanceTuning').Groups['body'].Value
if ([string]::IsNullOrEmpty($configuredTuningBody) -or
    $configuredTuningBody -notmatch 'CustomSoulVigorRewardMultiplier\.Value' -or
    $configuredTuningBody -notmatch 'CustomSoulClaimThresholdAdjustment\.Value') {
    throw 'Configured balance tuning must always read the four visible relative values.'
}

Assert-Contains $progression 'ApplySoulVigorRewardMultiplier\(\s*GetOrRollCorpseSoulVigorValue\(' 'Ordinary corpse rewards must be multiplied after their native value is resolved.'
Assert-Contains $progression 'int consumed = Math\.Min\(remaining, stableExtraction\);\s*int award = ApplySoulVigorRewardMultiplier\(consumed\);' 'Greater-soul extraction must multiply the player award without changing pool depletion.'
Assert-Contains $progression 'remaining - consumed' 'Greater-soul pools must deplete by the native portion rather than the multiplied award.'
if (($progression | Select-String -Pattern 'ApplySoulVigorRewardMultiplier\(' -AllMatches).Matches.Count -ne 5) {
    throw 'Reward multiplication must remain limited to its helper, corpse harvests, servant fractional awards, and the greater-soul transaction.'
}
$restoreBody = [regex]::Match(
    $progression,
    'internal static int RestoreSoulVigor\(int amount\)(?<body>[\s\S]*?)\n        private static int ApplySoulVigorRewardMultiplier').Groups['body'].Value
if ([string]::IsNullOrEmpty($restoreBody) -or $restoreBody -match 'ApplySoulVigorRewardMultiplier') {
    throw 'Investment refunds must not use the harvest reward multiplier.'
}

Assert-Contains $salvage 'GetPowerScaledSoulVigorCost\(int baseCost, float power\)[\s\S]{0,500}Mathf\.CeilToInt\([\s\S]{0,100}Math\.Max\(0, baseCost\)[\s\S]{0,50}\* multiplier\)' 'Soul Vigor costs must use only their Power scaling.'
if ($plugin.Contains('CustomSoulVigorCostMultiplier') -or
    $plugin.Contains('"SoulVigorCostMultiplier"') -or
    $salvage.Contains('.SoulVigorCostMultiplier')) {
    throw 'Soul Vigor cost must remain fixed at x1.00 with no player-facing multiplier.'
}
Assert-Contains $progression 'RollRaisedHealthFraction[\s\S]{0,600}\.RaisedStartingHealthMultiplier' 'New ordinary raised servants must inherit the starting-Health multiplier.'
Assert-Contains $summon 'GetUpkeepPercentPerMinute[\s\S]{0,1600}\.ServantUpkeepMultiplier' 'Active servant upkeep must inherit the profile multiplier.'
Assert-Contains $summon 'GetRestAttritionPercent[\s\S]{0,900}\.ServantUpkeepMultiplier' 'Rest attrition must inherit the profile multiplier.'

if (($salvage | Select-String -Pattern 'CalculateSoulClaimThreshold\(' -AllMatches).Matches.Count -ne 3) {
    throw 'Soul Claim threshold calculation must be shared by the attempt, hover preview, and helper only.'
}
Assert-Contains $salvage 'float presetAdjustment = SoulAndServicePlugin[\s\S]{0,200}\.SoulClaimThresholdAdjustment\s*/ 100\.0f;[\s\S]{0,500}\+ presetAdjustment,[\s\S]{0,200}SoulClaimMinimumHealthThreshold,[\s\S]{0,100}SoulClaimMaximumHealthThreshold' 'Soul Claim must add the visible preset adjustment before its final threshold clamp.'
Assert-Contains $plugin 'private string GetBalanceSummary\(\)[\s\S]{0,200}GetConfiguredBalanceTuning\(\)' 'The startup summary must report the player-facing relative values.'
Assert-Contains $plugin 'balance="\s*\+ GetBalanceSummary\(\)' 'Startup logging must report the relative profile and values.'

Write-Host 'Soul and Service balance preset contracts passed.'
