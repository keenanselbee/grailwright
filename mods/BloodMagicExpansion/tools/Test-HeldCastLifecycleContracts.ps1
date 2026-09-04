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
        "(?s)(?:internal|private) (?:void|bool) $MethodName\(.+?(?=\r?\n\s*(?:internal|private) )")
    if (!$match.Success) {
        throw "Missing held-cast lifecycle method: $MethodName"
    }

    return $match.Value
}

$releaseFallback = Get-MethodBlock "EndReleasedBloodMagicCastIfStillLooping"
foreach ($contract in @(
    '_strongCastStates.TryGetValue(magicFsm, out state)',
    'state.ReleaseFallbackAttempted',
    'GetBoolProperty(magicFsm, "SpellAttackHeld", false)',
    'GetBoolProperty(magicFsm, "IsLayerActive", false)',
    'GetStringProperty(magicFsm, "CurrentStateType")',
    '"MagicHeavyLoop"',
    'IsBloodTransfusionItemOrSkill(item, skill, out summary)',
    'state.ReleaseFallbackAttempted = true;',
    '"SetCurrentState"',
    '"MagicHeavyEnd"',
    'new object[] { heavyEndState, 0.05f, null }')) {
    if ($releaseFallback.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing held-cast release safeguard contract: $contract"
    }
}

$update = Get-MethodBlock "RecordMagicFsmUpdate"
$releaseCheck = $update.IndexOf('if (hasState && !held)', [StringComparison]::Ordinal)
$probeThrottle = $update.IndexOf('if (now < state.NextUpdateProbeTime)', [StringComparison]::Ordinal)
if ($releaseCheck -lt 0 -or $probeThrottle -lt 0 -or $releaseCheck -gt $probeThrottle) {
    throw "Physical input release must clear held-cast state before update throttling."
}
if ($update.IndexOf('state = GetStrongCastState(magicFsm);', [StringComparison]::Ordinal) -ge 0) {
    throw "MagicFSM update evidence must not create a held-cast state without an accepted heavy cast."
}
if ($update.IndexOf('if (hasState && held)', [StringComparison]::Ordinal) -lt 0) {
    throw "Held-cast grace may only be refreshed for an accepted cast with held input."
}

$heldCheck = Get-MethodBlock "IsHeldBloodMagicChannel"
$physicalInput = $heldCheck.IndexOf(
    'GetBoolProperty(magicFsm, "SpellAttackHeld", false)',
    [StringComparison]::Ordinal)
$graceState = $heldCheck.IndexOf(
    '_strongCastStates.TryGetValue(magicFsm, out state)',
    [StringComparison]::Ordinal)
if ($physicalInput -lt 0 -or $graceState -lt 0 -or $physicalInput -gt $graceState) {
    throw "Physical held input must be authoritative over held-cast grace."
}

foreach ($methodName in @("RegisterPerformCast", "RegisterCastEnding")) {
    $method = Get-MethodBlock $methodName
    if ($method.IndexOf('_strongCastStates.Remove(magicFsm);', [StringComparison]::Ordinal) -lt 0) {
        throw "$methodName must terminate Blood/Life held-cast state."
    }
}

$lightStart = Get-MethodBlock "RegisterStrongCastStart"
foreach ($contract in @(
    'if (lightCast)',
    'LightCastRecoveryState lightState = GetLightCastRecoveryState(magicFsm);',
    'lightState.Hand = GetHandKey(magicFsm);',
    'lightState.AcceptedAt = Now;',
    'lightState.PerformObserved = false;',
    'lightState.RecoveryAttempted = false;')) {
    if ($lightStart.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing light-cast start tracking contract: $contract"
    }
}

$lightPerform = Get-MethodBlock "RegisterPerformCast"
foreach ($contract in @(
    'if (isBloodMagicSpell && lightCast)',
    '_lightCastRecoveryStates.TryGetValue(magicFsm, out lightState)',
    'lightState.PerformedAt = Now;',
    'lightState.PerformObserved = true;')) {
    if ($lightPerform.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing performed light-cast tracking contract: $contract"
    }
}

$lightRecovery = Get-MethodBlock "RecoverPerformedBloodMagicLightCastIfStillInitial"
foreach ($contract in @(
    '_lightCastRecoveryStates.TryGetValue(magicFsm, out state)',
    '"MagicLightInitial"',
    '!state.PerformObserved',
    'state.RecoveryAttempted',
    'state.PerformedAt + BloodSpellLightCastRecoveryDelaySeconds',
    'GetBoolProperty(magicFsm, "SpellAttackHeld", false)',
    'GetBoolProperty(magicFsm, "IsCasting", false)',
    'GetBoolProperty(magicFsm, "IsChargingMagic", false)',
    'GetBoolProperty(magicFsm, "IsLayerActive", false)',
    'IsBloodTransfusionItemOrSkill(item, skill, out summary)',
    'state.RecoveryAttempted = true;',
    '"SetCurrentState"',
    '"Idle"',
    'new object[] { idleState, 0.05f, null }')) {
    if ($lightRecovery.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing performed light-cast recovery contract: $contract"
    }
}

$castEnding = Get-MethodBlock "RegisterCastEnding"
if ($castEnding.IndexOf('_lightCastRecoveryStates.Remove(magicFsm);', [StringComparison]::Ordinal) -lt 0) {
    throw "Cast completion or cancellation must terminate light-cast recovery tracking."
}

$patchMatch = [regex]::Match(
    $source,
    '(?s)private static class MagicFsmUpdatePatch.+?(?=\r?\n\s*private static class )')
if (!$patchMatch.Success) {
    throw "Missing MagicFSM update patch."
}
$fallbackCall = $patchMatch.Value.IndexOf(
    'plugin.EndReleasedBloodMagicCastIfStillLooping(__instance);',
    [StringComparison]::Ordinal)
$recordCall = $patchMatch.Value.IndexOf(
    'plugin.RecordMagicFsmUpdate(__instance);',
    [StringComparison]::Ordinal)
$lightRecoveryCall = $patchMatch.Value.IndexOf(
    'plugin.RecoverPerformedBloodMagicLightCastIfStillInitial(__instance);',
    [StringComparison]::Ordinal)
if ($fallbackCall -lt 0 -or
    $lightRecoveryCall -lt 0 -or
    $recordCall -lt 0 -or
    $fallbackCall -gt $lightRecoveryCall -or
    $lightRecoveryCall -gt $recordCall) {
    throw "Post-native cast safeguards must run before casting state is recorded and cleared."
}

Write-Host "Blood Magic Expansion held-cast lifecycle contracts passed."
