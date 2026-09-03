[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\BattlecryVoiceTuner.cs")

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($source -notmatch $Pattern) {
        throw $Message
    }
}

Assert-Contains 'CurrentConfigSchemaVersion = 11' `
    'Demonic applied presets require config schema 11.'
Assert-Contains 'public enum DemonicVoicePreset\s*\{\s*Minimal,\s*Demonic,\s*Abyssal,\s*Custom\s*\}' `
    'Demonic Voice choices must progress from Minimal through Abyssal with Custom last.'
Assert-Contains '"DemonicVoicePreset",\s*DemonicVoicePreset\.Demonic,[\s\S]{0,350}"Demonic Voice Preset",\s*"Demonic Voice Profile",\s*4,\s*0' `
    'Demonic must remain the default and the selector must lead the preset section.'

foreach ($contract in @(
    '"DemonicProgressionCurveExponent",\s*0\.80f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"MaximumProgressionPitchSemitones",\s*-3\.25f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"MaximumDemonicDistortion",\s*0\.18f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"MinimumDemonicLowpassCutoffHz",\s*4200\.0f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"DemonicEchoDelayMs",\s*80\.0f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"MaximumDemonicEchoFeedbackPercent",\s*16\.0f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"MaximumDemonicEchoWetLevelDb",\s*-25\.0f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"MaximumDemonicShadowPitchSemitones",\s*-7\.0f,[\s\S]{0,350}"Demonic Voice Preset"',
    '"MaximumDemonicShadowMixDb",\s*-21\.0f,[\s\S]{0,350}"Demonic Voice Preset"')) {
    Assert-Contains $contract `
        "A Demonic default or displayed live value no longer matches the balanced profile: $contract"
}

Assert-Contains 'ConfigPreviousSettingsRecovery\.Bind\([\s\S]{0,350}ApplySelectedDemonicPreset\(\);\s*Config\.SettingChanged \+= OnConfigSettingChanged;\s*Config\.Save\(\);' `
    'Startup must recover config, apply a named preset, then subscribe to manual changes.'
Assert-Contains 'private void OnConfigSettingChanged[\s\S]{0,900}_demonicVoicePreset\.Value = DemonicVoicePreset\.Custom;' `
    'Editing a governed Demonic value must select Custom.'
Assert-Contains 'private void ApplySelectedDemonicPreset[\s\S]{0,350}_demonicVoicePreset\.Value == DemonicVoicePreset\.Custom' `
    'Selecting Custom must preserve the current Demonic values.'
Assert-Contains 'private void Update\(\)\s*\{\s*RefreshFoaModManagerIfPending\(\);' `
    'FoA Mod Manager refresh must be deferred until the next frame.'
Assert-Contains 'AccessTools\.TypeByName\(\s*"FoAModManager\.FoAModManagerApi"\)[\s\S]{0,250}AccessTools\.Method\(apiType, "Refresh"\)[\s\S]{0,250}refreshMethod\.Invoke\(null, null\);' `
    'The optional FoA Mod Manager public refresh API must be called without a hard dependency.'

$applyBlock = [regex]::Match(
    $source,
    '(?s)private void ApplySelectedDemonicPreset\(.+?(?=\r?\n\s*private )')
if (!$applyBlock.Success) {
    throw 'Missing Demonic preset application method.'
}
foreach ($field in @(
    '_demonicProgressionCurveExponent',
    '_maximumProgressionPitchSemitones',
    '_maximumDemonicDistortion',
    '_minimumDemonicLowpassCutoffHz',
    '_demonicEchoDelayMs',
    '_maximumDemonicEchoFeedbackPercent',
    '_maximumDemonicEchoWetLevelDb',
    '_maximumDemonicShadowPitchSemitones',
    '_maximumDemonicShadowMixDb')) {
    if ($applyBlock.Value.IndexOf(
            "$field.Value =",
            [StringComparison]::Ordinal) -lt 0) {
        throw "Named Demonic profiles do not write governed value $field."
    }
}

$resolveBlock = [regex]::Match(
    $source,
    '(?s)private DemonicPresetSettings ResolveDemonicPresetSettings\(.+?(?=\r?\n\s*private )')
if (!$resolveBlock.Success -or
    $resolveBlock.Value.IndexOf(
        '_demonicVoicePreset',
        [StringComparison]::Ordinal) -ge 0) {
    throw 'Demonic runtime settings must read the nine live values without branching on the profile.'
}

Write-Host "Battlecry Voice Tuner Demonic applied-preset contracts passed."
