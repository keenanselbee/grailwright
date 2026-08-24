[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\FirstPersonArmsAdjuster.cs"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Missing source file: $sourcePath"
}

$source = Get-Content -LiteralPath $sourcePath -Raw

$toggleSettings = @(
    @('EnableAnimationGuards', 'true', 'Enable Animation Guards'),
    @('EnableDodgeGuard', 'true', 'Enable Dodge Guard'),
    @('EnableSheathingGuard', 'true', 'Enable Sheathing Guard'),
    @('EnableBowDrawGuard', 'true', 'Enable Bow Draw Guard'),
    @('UseSharedGuardTarget', 'true', 'Use Shared Guard Target')
)
foreach ($setting in $toggleSettings) {
    $key = [regex]::Escape($setting[0])
    $defaultValue = [regex]::Escape($setting[1])
    $label = [regex]::Escape($setting[2])
    if ($source -notmatch ('(?s)"' + $key + '",\s*' + $defaultValue + '.+?DisplaySection = "Advanced - Animation Guards".+?DisplayName = "' + $label + '"')) {
        throw "Missing animation-guard toggle: $($setting[0])"
    }
}

if ($source -notmatch '(?s)"MitigateHeldMeleeBodyIntrusion",\s*true,.+?DisplaySection = "Advanced - Animation Guards".+?DisplayName = "Enable Attack Guards"') {
    throw "The established attack guard must remain compatible while appearing in the consolidated guard section."
}
if ($source -notmatch '(?s)"SharedMoveTowardVanillaPercent",\s*50\.0f,.+?AcceptableValueRange<float>\(0\.0f, 100\.0f\).+?DisplayName = "Shared Move Toward Vanilla \(%\)"') {
    throw "The shared animation-guard target must default to the validated 50 percent target with a 0-100 range."
}
if ($source -notmatch '(?s)"BowDrawMaximumOffsetPercent",\s*33\.0f,.+?AcceptableValueRange<float>\(0\.0f, 100\.0f\).+?DisplayName = "Bow Draw Maximum Offset \(%\)"') {
    throw "Bow draw must default to a dynamic 33 percent ceiling based on BowForwardOffset."
}

foreach ($fragment in @(
    'IsAnimationGuardEnabled(',
    '_mitigateHeldMeleeBodyIntrusion);',
    '_enableDodgeGuard)',
    '_enableSheathingGuard)',
    '_enableBowDrawGuard)',
    'TryGetSharedAnimationGuardStrength(',
    'IsExpandedMeleeAttackActive(hero)',
    'HeroStateType.LightAttackInitial',
    'HeroStateType.LightAttackSecond',
    'HeroStateType.HeavyAttackEndAlternate',
    'strongestMoveTowardVanilla = Mathf.Max(',
    'sprintAttackRetainedScale = 1.0f',
    'sheathingRetainedScale = 1.0f',
    'heldCorrection = Vector3.zero;',
    '* sprintAttackRetainedScale',
    '* sheathingRetainedScale'
)) {
    if (-not $source.Contains($fragment)) {
        throw "Missing shared animation-guard behavior: $fragment"
    }
}

foreach ($key in @(
    'EnableAnimationGuards',
    'EnableDodgeGuard',
    'EnableSheathingGuard',
    'EnableBowDrawGuard',
    'BowDrawMaximumOffsetPercent',
    'UseSharedGuardTarget',
    'SharedMoveTowardVanillaPercent'
)) {
    if ($source -notmatch ('(?s)TryGetCustomizedValue\(.+?"Advanced - Animation Guards",\s*"' + [regex]::Escape($key) + '"')) {
        throw "Animation-guard setting must participate in typed config preservation: $key"
    }
}

if ($source.Contains('IsUsingTwoHandedMeleeGrip')) {
    throw "Sheathing coverage must no longer be restricted to active two-handed melee grips."
}

foreach ($key in @(
    'MitigateHeldMeleeBodyIntrusion',
    'HeldMeleeOffsetScale',
    'HeldMeleeExtraForwardOffset',
    'HeldMeleeExtraVerticalOffset'
)) {
    if ($source -notmatch ('(?s)Config\.Bind\(\s*"Advanced - Animation Guards",\s*"' + $key + '"')) {
        throw "Animation-guard raw config must be consolidated: $key"
    }
}

if ($source.Contains('Advanced - Melee Guards') -or
    $source.Contains('Advanced - Dodge Guard') -or
    $source.Contains('DodgeMoveTowardVanillaPercent')) {
    throw "Retired split animation-guard sections or dodge offset controls remain."
}

if ($source -notmatch 'private const int ConfigSchemaVersion = 20;') {
    throw "The validated 33 percent bow-draw default must advance the schema to 20."
}

Write-Host "First Person Arms Adjuster animation-guard contracts passed."
