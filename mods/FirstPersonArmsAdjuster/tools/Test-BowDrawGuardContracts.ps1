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
$requiredFragments = @(
    'BowDrawGuardBlendInSeconds = 0.18f',
    'BowDrawGuardBlendOutSeconds = 0.40f',
    'BowPullGuardFullNormalizedTime = 0.65f',
    'BowReleaseProjectileNormalizedTime = 0.05f',
    'UpdateBowDrawGuardBlend();',
    'GetBowDrawGuardTarget(Hero.Current)',
    '&& !_bowDrawGuardActive',
    'float effectiveHeldMeleeBlend = _bowDrawGuardActive',
    '? 0.0f',
    ': _heldMeleeMitigationBlend;',
    'animatorState.TimeElapsedNormalized',
    'easedPullProgress',
    '_bowDrawGuardBlend = Mathf.MoveTowards(',
    'HeroStateType.BowPull',
    'HeroStateType.BowHold',
    'HeroStateType.BowRelease',
    'normalizedTime',
    '< BowReleaseProjectileNormalizedTime',
    '_bowForwardOffset.Value',
    'bowDrawMaximumOffsetPercent / 100.0f',
    'Mathf.Min(',
    'configuredForwardOffset,',
    'bowDrawDepthCeiling',
    'Applying the bow-draw depth ceiling.',
    'Restoring the normal configured depth after bow draw.'
)

foreach ($fragment in $requiredFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing bow-draw guard contract: $fragment"
    }
}

if ($source -notmatch '(?s)UpdateFireplaceOffsetBlend\(\);\s*UpdateBowDrawGuardBlend\(\);\s*UpdateHeldMeleeOffsetBlend\(\);') {
    throw "Bow guard state must update before the melee guard can evaluate bow priority."
}

if ($source -notmatch '(?s)IsHeldMeleeAttackActive\(Hero hero\).+?!melee\.IsLayerActive' -or
    $source -notmatch '(?s)IsExpandedMeleeAttackActive\(Hero hero\).+?melee\.IsLayerActive') {
    throw "Melee guards must ignore inactive FSM layers that can retain stale attack states."
}

if ($source -notmatch '(?s)"EnableBowDrawGuard",\s*true,.+?DisplaySection = "Advanced - Animation Guards".+?DisplayName = "Enable Bow Draw Guard"' -or
    $source -notmatch '(?s)"BowDrawMaximumOffsetPercent",\s*33\.0f,.+?AcceptableValueRange<float>\(0\.0f, 100\.0f\).+?DisplayName = "Bow Draw Maximum Offset \(%\)"' -or
    $source -notmatch '(?s)"BowForwardOffset",\s*0\.30f,.+?DisplayName = "Bow Depth Offset \(m\)"') {
    throw "Bow-draw controls must remain enabled by default and tied to a 0-100 percent bow-depth ceiling."
}

if ($source -notmatch '(?s)TryGetCustomizedValue\(.+?"EnableBowDrawGuard".+?_pendingEnableBowDrawGuard' -or
    $source -notmatch '(?s)TryGetCustomizedValue\(.+?"BowDrawMaximumOffsetPercent".+?_pendingBowDrawMaximumOffsetPercent' -or
    $source -notmatch '(?s)TryRestore\(\s*_enableBowDrawGuard,\s*_pendingEnableBowDrawGuard' -or
    $source -notmatch '(?s)RestorePreservedFloat\(\s*_hasPendingBowDrawMaximumOffsetPercent,\s*_bowDrawMaximumOffsetPercent') {
    throw "Bow-draw controls must participate in typed config preservation."
}

Write-Host "First Person Arms Adjuster bow-draw guard contracts passed."
