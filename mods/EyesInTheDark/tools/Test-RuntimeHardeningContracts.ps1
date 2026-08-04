$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\FirstHunterRuntime.cs") -Raw

foreach ($required in @(
    'ListenerRetryBackoffSeconds = 30.0f',
    '_nextHeroListenerRetryUnscaled',
    '_nextAcquisitionListenerRetryUnscaled',
    '_heroListenerFailureLogged',
    '_acquisitionListenerFailureLogged',
    'eventSystemChanged',
    'Time.unscaledTime',
    'binding will retry in 30 unscaled seconds',
    'ContinuousThreatDiagnosticIntervalSeconds =',
    'IsContinuousThreatCause(',
    'AccumulateContinuousThreatDiagnostic(',
    'FlushContinuousThreatDiagnostics(',
    'Continuous threat summary: passive=',
    '_pendingProtectedDecayDiagnostic',
    '_pendingInteriorDecayDiagnostic')) {
    if (!$pluginSource.Contains($required)) {
        throw "Runtime hardening integration is missing token: $required"
    }
}

foreach ($required in @(
    'HasExactHeroTarget(',
    'member.Npc.NpcAI.InCombat',
    'ReferenceEquals(member.Npc.GetCurrentTarget(), hero)',
    'native combat entry did not acquire the exact Hero target',
    'ReacquisitionIntervalSeconds = 2f',
    'ReacquisitionDistanceMeters = 60f',
    'MaximumReacquisitionAttemptsPerMember = 3',
    'member.ReacquisitionAttempts++',
    'EnterCombatWith(hero, true)')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Official-hunt hardening is missing token: $required"
    }
}

if ($pluginSource -notmatch
    'if \(!_heroListenerFailureLogged\)[\s\S]+?_heroListenerFailureLogged = true;') {
    throw "Hero listener failure warnings are not episode-gated."
}
if ($pluginSource -notmatch
    'if \(!_acquisitionListenerFailureLogged\)[\s\S]+?_acquisitionListenerFailureLogged = true;') {
    throw "Acquisition listener failure warnings are not episode-gated."
}

& (Join-Path $PSScriptRoot "Test-RuntimeStateLog.ps1") -ParserOnly

Write-Host "Eyes in the Dark runtime hardening contracts passed."
