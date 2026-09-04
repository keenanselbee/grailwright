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
if ($source -notmatch '(?s)"ExecutionMoveTowardVanillaPercent",\s*100\.0f,.+?AcceptableValueRange<float>\(0\.0f, 100\.0f\).+?DisplayName = "Execution Move Toward Vanilla \(%\)"') {
    throw "Executions must default to a configurable full return to vanilla offsets."
}
if ($source -notmatch '(?s)"ExecutionShoulderRetraction",\s*0\.12f,.+?AcceptableValueRange<float>\(0\.0f, 0\.25f\).+?DisplayName = "Execution Shoulder Retraction \(m\)"') {
    throw "Execution shoulder retraction must retain its configurable 0.12 metre default."
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

foreach ($meleeGuardMethodName in @(
    'IsHeldMeleeAttackActive',
    'IsExpandedMeleeAttackActive'
)) {
    $meleeGuardMethod = [regex]::Match(
        $source,
        ('(?s)private static bool ' + $meleeGuardMethodName + '\(Hero hero\).+?\n        \}\n\n        private static bool'))
    if (-not $meleeGuardMethod.Success -or
        -not $meleeGuardMethod.Value.Contains(
            'HasMeleeGuardEligibleItem(melee)')) {
        throw "$meleeGuardMethodName must reject stale attack states owned by non-melee items."
    }
}

$meleeItemGuard = [regex]::Match(
    $source,
    '(?s)private static bool HasMeleeGuardEligibleItem\(MeleeFSM melee\).+?\n        \}\n\n        private static bool IsExpandedMeleeAttackState')
if (-not $meleeItemGuard.Success) {
    throw "Missing per-FSM melee item ownership guard."
}
foreach ($excludedType in @(
    'EquipmentType.Magic',
    'EquipmentType.MagicTwoHanded',
    'EquipmentType.Bow'
)) {
    if (-not $meleeItemGuard.Value.Contains($excludedType)) {
        throw "The melee item ownership guard must exclude $excludedType."
    }
}

foreach ($fragment in @(
    'KillingBlowMastery.ExecutionVisualApi',
    'UpdateExecutionGuardBlend();',
    '_executionMoveTowardVanillaPercent.Value',
    '_executionShoulderRetraction.Value',
    'float dodgeShoulderRetraction =',
    'float executionShoulderRetraction =',
    'fsm.CurrentStateType == HeroStateType.Finisher',
    'ExecutionNativeStateGraceSeconds = 0.25f'
)) {
    if (-not $source.Contains($fragment)) {
        throw "Missing Killing Blow Mastery execution-guard behavior: $fragment"
    }
}

if ($source -notmatch '_executionGuardBlend\s*\*\s*executionMoveTowardVanillaStrength') {
    throw "The execution guard does not apply its configurable move-toward-vanilla target."
}
if ($source -notmatch '_currentShoulderRetractionMeters\s*=\s*Mathf\.Max\(\s*dodgeShoulderRetraction,\s*executionShoulderRetraction\s*\);') {
    throw "Execution and dodge shoulder retraction do not compose by strongest correction."
}

if ($source -match 'HeroStateType\.Finisher[\s\S]*?IsExpandedMeleeAttackState') {
    throw "Execution guarding must remain independent from ordinary melee-state coverage."
}

foreach ($key in @(
    'EnableAnimationGuards',
    'EnableDodgeGuard',
    'EnableSheathingGuard',
    'EnableBowDrawGuard',
    'BowDrawMaximumOffsetPercent',
    'UseSharedGuardTarget',
    'SharedMoveTowardVanillaPercent',
    'ExecutionMoveTowardVanillaPercent',
    'ExecutionShoulderRetraction'
)) {
    if ($source -notmatch ('(?s)TryGetCustomizedValue\(.+?"Advanced - Animation Guards",\s*"' + [regex]::Escape($key) + '"')) {
        throw "Animation-guard setting must participate in typed config preservation: $key"
    }
}

if ($source.Contains('IsUsingTwoHandedMeleeGrip')) {
    throw "Sheathing coverage must no longer be restricted to active two-handed melee grips."
}

$sheathingMethod = [regex]::Match(
    $source,
    '(?s)private static bool TryGetSheathingOffsetBlend\(.+?\n        \}\n\n        private static bool IsSheathingState')
if (-not $sheathingMethod.Success) {
    throw "Missing sheathing offset guard."
}
if ($sheathingMethod.Value -notmatch 'if \(fsm == null \|\| !fsm\.IsLayerActive\)') {
    throw "Inactive animator FSM layers must not keep the sheathing offset guard active."
}
foreach ($diagnosticContract in @(
    'out string sourceSummary',
    'fsm.GetType().FullName',
    'fsm.LayerType',
    'currentState',
    'targetState',
    'normalizedTime',
    'retainedBlend')) {
    if ($sheathingMethod.Value.IndexOf(
            $diagnosticContract,
            [StringComparison]::Ordinal) -lt 0) {
        throw "Missing sheathing-source diagnostic contract: $diagnosticContract"
    }
}

foreach ($diagnosticContract in @(
    'Sheathing guard source changed:',
    'Animation guard offset diagnostic:',
    'configuredLocal=',
    'effectiveLocal=',
    '_sheathingOffsetBlend')) {
    if ($source.IndexOf(
            $diagnosticContract,
            [StringComparison]::Ordinal) -lt 0) {
        throw "Missing animation-guard offset diagnostic contract: $diagnosticContract"
    }
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
