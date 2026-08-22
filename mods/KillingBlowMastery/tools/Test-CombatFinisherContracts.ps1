[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\KillingBlowMastery.cs") -Raw
$contracts = @(
    '"AutomaticCombatFinishersEnabled",',
    '"CombatExecutionMode",',
    'CombatExecutionModeVanilla,',
    'CombatExecutionModeGloryKill,',
    'CombatExecutionModeOff))',
    '"GloryKillHealthPercent",',
    '15.0f,',
    'new AcceptableValueRange<float>(1.0f, 30.0f)',
    '"ExpandedGloryKillTargets",',
    'false,',
    '"TryTriggerFinisherBeforeAttack"',
    '"CanBeTriggered"',
    'AccessTools.PropertyGetter(executionActionType, "DefaultActionName")',
    '__result = "Execute";',
    'GetBoolProperty(npcAi, "InCombat", false)',
    '"IsHostileToHero"',
    'GetBoolProperty(npc, "IsUnconscious", false)',
    'GetBoolProperty(npc, "IsInRagdoll", false)',
    'GetBoolProperty(npc, "CanUseExternalCustomDeath", false)',
    'healthPercent > _gloryKillHealthPercent.Value',
    'Instance._gloryKillHealthPercent.Value / 100.0f',
    'CacheAutomaticFinisherFallbackAccessor(finisherHandlingType);',
    '"AttackTriesToStart"',
    '"FinishersList"',
    'TryCacheAutomaticFinisherFallback(',
    'cachedDataField.SetValue(executionAction, arguments[1]);',
    'cachedDamageOutcomeField.SetValue(executionAction, arguments[2]);',
    'cachedFinisherListField.SetValue(executionAction, arguments[3]);',
    'cachedDamageField.SetValue(executionAction, arguments[4]);',
    '"targetAbstracts"',
    'if (expandedTargets)',
    'hasToBeStaggered',
    'hasToBeUnconscious',
    'hasToBeRagdolled',
    'requiredState',
    'public static Exception Finalizer(',
    '__state.Restore();',
    'GloryKillDiagnosticRepeatSeconds = 3.0f',
    'GloryKill eligibility: target=',
    'blocked: health=',
    'blocked: target disallows external custom-death animations',
    'blocked: target NpcAI was not in combat',
    'blocked: equipped melee weapon had no loaded execution or normal finisher list',
    'blocked: native execution and normal-finisher fallback found no compatible loaded animation',
    'normal-finisher fallback',
    'DescribeAnimationReadiness()',
    'completed-null=',
    'waiting for the native 0.6-second activation delay',
    'string availableSource = state != null',
    '"available: "',
    '+ availableSource',
    '+ " accepted; Execute prompt should be visible; "',
    'CustomDeathAnimationTypeName = "Awaken.TG.Main.AI.Combat.CustomDeath.CustomDeathAnimation"',
    'AccessTools.Method(customDeathAnimationType, "CheckConditions")',
    'FinishersListTypeName = "Awaken.TG.Main.Fights.Finishers.FinishersList"',
    'AccessTools.Method(finishersListType, "CheckGlobalConditions")',
    'private static class GloryKillGlobalConditionsPatch',
    'state.RecordGlobalConditionsBypass(__instance);',
    'FinisherDataTypeName = "Awaken.TG.Main.Fights.Finishers.FinisherData"',
    'private static class GloryKillFinisherDataConditionsPatch',
    'state.RecordCandidateConditionResult(__result);',
    'global-bypassed-lists=',
    'candidate-checks=',
    'candidate-accepted=',
    'candidate-rejected=',
    'private static GloryKillEvaluationState _activeGloryKillEvaluation;',
    'preparedState.Activate();',
    'private static class GloryKillAnimationConditionsPatch',
    'if (_activeGloryKillEvaluation == null)'
)

foreach ($contract in $contracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Killing Blow Mastery combat finisher contract: $contract"
    }
}

if ($source.IndexOf('KillUnconsciousAction', [StringComparison]::Ordinal) -ge 0) {
    throw "Combat finisher controls must not patch or reference the story KillUnconsciousAction."
}

if ($source.IndexOf('private const int ConfigSchemaVersion = 15;', [StringComparison]::Ordinal) -lt 0) {
    throw "Adding combat finisher settings must not advance the config schema."
}

Write-Output "Killing Blow Mastery combat finisher contracts passed."
