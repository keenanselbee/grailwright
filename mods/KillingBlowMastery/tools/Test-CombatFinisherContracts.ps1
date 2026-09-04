[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\KillingBlowMastery.cs") -Raw
foreach ($removedAsset in @("audio\slowmo.wav", "audio\slowmo.pkf")) {
    if (Test-Path -LiteralPath (Join-Path $modRoot $removedAsset)) {
        throw "Removed Execution slow-motion asset returned: $removedAsset"
    }
}
$contracts = @(
    '"AutomaticCombatFinishersEnabled",',
    '"CombatExecutionMode",',
    'CombatExecutionModeVanilla,',
    'CombatExecutionModeExecution,',
    'CombatExecutionModeOff))',
    '"ExecutionMinimumProficiency",',
    '25,',
    'new AcceptableValueRange<int>(0, 100)',
    '"ExecutionHealthPercentAtUnlock",',
    '10.0f,',
    '"ExecutionHealthPercentAtMastery",',
    '25.0f,',
    'new AcceptableValueRange<float>(1.0f, 30.0f)',
    '"ExpandedExecutionTargets",',
    '"ExpandedExecutionExcludedAbstracts",',
    'DefaultExpandedExecutionExcludedAbstracts =',
    '"Animal;Animal_Prey";',
    'KnownExecutionTargetAbstracts =',
    'Animal, Animal_Prey, Bandit, BigHumanoid, Bloody, BoneMask, Boss,',
    'ChallengeModeSpawn, Cultist, DalRiataBody, Female, Foredweller,',
    'Ghost, Giant, Human, Humanoid, Male, MiniBoss, Monster,',
    'ReefboundBody, Scourge, Skeleton, Summon, Tainted, WyrdnessBound, Zombie',
    'string displaySection = GetConfigDisplaySection(section, key);',
    'GetConfigDisplayName(key)',
    '"FullPotencyExecutions",',
    'private ConfigEntry<bool> _fullPotencyExecutions;',
    '"ExecuteAtAnyHealth",',
    'private ConfigEntry<bool> _executeAtAnyHealth;',
    'return "Combat Finishers";',
    'return "Reward Audio";',
    'return "Advanced Audio Routing";',
    'return "Automatic Kill-Cam Animations";',
    'return "Combat Execution Mode";',
    'return "Execution Unlock Proficiency";',
    'return "Health Threshold at Unlock (%)";',
    'return "Health Threshold at Mastery (%)";',
    'return "Expand Target Types";',
    'return "Excluded Target Families";',
    'return "Sound Distance Fade";',
    'Known families (26): ',
    'Use GrailFloatingText, GameHud, Both, or Off.',
    '"TryTriggerFinisherBeforeAttack"',
    '"CanBeTriggered"',
    'PatchExecutionFinisherStart(executionActionType);',
    'AccessTools.Method(executionActionType, "OnStart")',
    'private static class ExecutionFinisherStartPatch',
    'private static class CombatFinisherLifecyclePatch',
    'FinisherStateTypeName = "Awaken.TG.Main.Animations.FSM.Heroes.States.Overrides.FinisherState"',
    'AccessTools.Method(finisherStateType, "OnFinisherStarted")',
    'AccessTools.Method(finisherStateType, "OnExit")',
    'AccessTools.Method(',
    '"RemoveSlowdowns")',
    'object cachedData = GetOptionalFieldValue(',
    '"_cachedData")',
    'AccessTools.Field(',
    '"slowDownTime")',
    'state.DisableSlowDownTime();',
    'Execution FinisherStarted:',
    'Execution FinisherEnded:',
    'Execution Finisher still active:',
    'Combat FinisherStarted:',
    'Combat Finisher OnExit begin:',
    'Combat Finisher OnExit complete:',
    'Combat Finisher RemoveSlowdowns:',
    'Combat Finisher still active:',
    'Combat Finisher possible stuck native slowdown:',
    '"_slowDowns"',
    'NativeFinisherStuckWarningSeconds = 6.0f',
    'private static AutomaticFinisherTriggerState _activeAutomaticFinisherTrigger;',
    'BeginAutomaticFinisherTrigger()',
    'private void Update()',
    'ReportActiveExecutionFinisherLifecycle();',
    'AccessTools.PropertyGetter(executionActionType, "DefaultActionName")',
    '__result = "Execute";',
    'GetBoolProperty(npcAi, "InCombat", false)',
    '"IsHostileToHero"',
    'GetBoolProperty(npc, "IsUnconscious", false)',
    'GetBoolProperty(npc, "IsInRagdoll", false)',
    'GetBoolProperty(npc, "CanUseExternalCustomDeath", false)',
    'healthPercent > maximumExecutionHealthPercent',
    'Instance.GetExecutionMaximumHealthPercent() / 100.0f',
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
    'IsExpandedExecutionTargetAllowed(',
    'GetExpandedExecutionExcludedAbstracts();',
    'NormalizeExecutionAbstractName(',
    '"excluded abstract family "',
    'hasToBeStaggered',
    'hasToBeUnconscious',
    'hasToBeRagdolled',
    'requiredState',
    'public static Exception Finalizer(',
    '__state.Restore();',
    'ExecutionDiagnosticRepeatSeconds = 3.0f',
    'Execution eligibility: target=',
    'blocked: health=',
    'blocked: expanded Execution ',
    'blocked: target disallows external custom-death animations',
    'blocked: target NpcAI was not in combat',
    'blocked: equipped melee weapon had no loaded execution or normal finisher list',
    'blocked: native execution and normal-finisher fallback found no compatible loaded animation',
    'normal-finisher fallback',
    'DescribeAnimationReadiness()',
    'completed-null=',
    'waiting for the native 0.6-second activation delay',
    'ExecutionTargetGraceSeconds = 0.18f',
    'TryUseExecutionTargetGrace(',
    'TrackExecutionTargetGraceCandidate(',
    'preparedState.SetField(',
    'Execution target grace preserved the exact target',
    'Execution target grace reacquired the exact target',
    'GetOptionalPropertyValue(',
    '"ParentModel"',
    'TryValidateExecutionProgression(',
    'GetExecutionProficiencyLevel(',
    'GetExecutionHealthPercent(',
    'GetExecutionMaximumHealthPercent()',
    'ClearExecutionCandidate(executionAction);',
    '"_activationTime"',
    'ref bool __result,',
    'ref __result,',
    'CustomDeathAnimationTypeName = "Awaken.TG.Main.AI.Combat.CustomDeath.CustomDeathAnimation"',
    'AccessTools.Method(customDeathAnimationType, "CheckConditions")',
    'FinishersListTypeName = "Awaken.TG.Main.Fights.Finishers.FinishersList"',
    'AccessTools.Method(finishersListType, "CheckGlobalConditions")',
    'AccessTools.Method(finishersListType, "CheckDefaultHpCondition")',
    'private static class ExecutionGlobalConditionsPatch',
    'state.RecordGlobalConditionsBypass(__instance);',
    'private static class ExecutionDefaultHpConditionPatch',
    'FinisherDataTypeName = "Awaken.TG.Main.Fights.Finishers.FinisherData"',
    'private static class ExecutionFinisherDataConditionsPatch',
    'state.RecordCandidateConditionResult(__result);',
    'global-bypassed-lists=',
    'candidate-checks=',
    'candidate-accepted=',
    'candidate-rejected=',
    'private static ExecutionEvaluationState _activeExecutionEvaluation;',
    'preparedState.Activate();',
    'private static class ExecutionAnimationConditionsPatch',
    'if (_activeExecutionEvaluation == null)',
    '"General\nCombatExecutionMode"',
    '"General\nExecutionMinimumProficiency"',
    '"General\nExecutionHealthPercentAtUnlock"',
    '"General\nExecutionHealthPercentAtMastery"',
    '"General\nExpandedExecutionTargets"',
    '"General\nExpandedExecutionExcludedAbstracts"'
)

foreach ($contract in $contracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Killing Blow Mastery combat finisher contract: $contract"
    }
}

if ($source -notmatch '_combatExecutionMode\s*=\s*BindOrdered\s*\(\s*"General"\s*,\s*"CombatExecutionMode"\s*,\s*CombatExecutionModeExecution\s*,') {
    throw "Execution must remain the default combat execution mode."
}

if ($source -notmatch 'actionTarget\s*==\s*null\s*\|\|\s*!ReferenceEquals\(actionTarget, graceTarget\)' -or
    $source -notmatch 'Time\.unscaledTime\s*>\s*_executionTargetGraceExpiresAt' -or
    $source -notmatch 'ClearExecutionTargetGrace\("Execution started"\)' -or
    $source -notmatch 'ExecutionTargetGraceSeconds\s*=\s*0\.18f') {
    throw "Execution target grace must be short-lived, exact-target scoped, and cleared when execution starts."
}

if ($source -notmatch '_expandedExecutionTargets\s*=\s*BindOrdered\s*\(\s*"General"\s*,\s*"ExpandedExecutionTargets"\s*,\s*true\s*,') {
    throw "Expanded enemy selection must remain enabled by default."
}

if ($source -notmatch '_fullPotencyExecutions\s*=\s*BindOrdered\s*\(\s*"Diagnostics"\s*,\s*"FullPotencyExecutions"\s*,\s*false\s*,') {
    throw "Full Potency Executions must remain a disabled-by-default Diagnostics control."
}

if ($source -notmatch 'bool\s+fullPotencyTest\s*=\s*_diagnostics\s*!=\s*null\s*&&\s*_diagnostics\.Value\s*&&\s*_fullPotencyExecutions\s*!=\s*null\s*&&\s*_fullPotencyExecutions\.Value;' -or
    $source -notmatch 'int\s+proficiencyLevel\s*=\s*fullPotencyTest\s*\?\s*100\s*:\s*actualProficiencyLevel;') {
    throw "Full Potency Executions must require Diagnostics and simulate proficiency 100 without replacing the actual proficiency read."
}

if ($source -notmatch 'hpValueField\.SetValue\s*\(\s*healthCondition\s*,\s*Instance\.GetExecutionMaximumHealthPercent\(\)\s*/\s*100\.0f\s*\);') {
    throw "Execution health conditions must receive the normalized mastery ceiling."
}

if ($source -notmatch '\(proficiencyLevel\s*-\s*minimumProficiency\)\s*/\s*\(100\.0f\s*-\s*minimumProficiency\)') {
    throw "Execution health progression must interpolate from the unlock proficiency through 100."
}

if ($source -notmatch 'return\s+healthPercent\s*>\s*0\.0f\s*&&\s*\(\s*anyHealthTest\s*\|\|\s*healthPercent\s*<=\s*threshold\s*\);') {
    throw "The selected weapon proficiency threshold must gate final Execution availability unless the diagnostic any-health test is active."
}

if ($source -notmatch '_executeAtAnyHealth\s*=\s*BindOrdered\s*\(\s*"Diagnostics"\s*,\s*"ExecuteAtAnyHealth"\s*,\s*false\s*,' -or
    $source -notmatch 'return\s+_diagnostics\s*!=\s*null\s*&&\s*_diagnostics\.Value\s*&&\s*_executeAtAnyHealth\s*!=\s*null\s*&&\s*_executeAtAnyHealth\.Value;' -or
    ([regex]::Matches($source, 'if\s*\(\s*IsExecuteAtAnyHealthEnabled\(\)\s*\)\s*\{\s*return\s+100\.0f;').Count -ne 2)) {
    throw "Execute At Any Health must remain disabled by default, require Diagnostics, and replace both explicit Execution thresholds with 100 percent."
}

$expectedThresholds = @{
    25 = 10.0
    50 = 15.0
    75 = 20.0
    100 = 25.0
}
foreach ($entry in $expectedThresholds.GetEnumerator()) {
    $progress = ($entry.Key - 25) / 75.0
    $threshold = 10.0 + (25.0 - 10.0) * $progress
    if ([Math]::Abs($threshold - $entry.Value) -gt 0.001) {
        throw "Execution progression table drifted at proficiency $($entry.Key)."
    }
}

if ($source -notmatch 'private\s+static\s+class\s+ExecutionDefaultHpConditionPatch\s*\{\s*public\s+static\s+bool\s+Prefix\s*\(\s*ref\s+bool\s+__result\s*\)\s*\{\s*if\s*\(\s*_activeExecutionEvaluation\s*==\s*null\s*\)\s*\{\s*return\s+true;\s*\}\s*__result\s*=\s*true;\s*return\s+false;') {
    throw "Execution default-health bypass must remain scoped to active Execution evaluation."
}

if ($source -notmatch 'private\s+ExecutionFinisherStartState\s+BeginExecutionFinisherStart\s*\(\s*object\s+executionAction\s*\)\s*\{\s*if\s*\(\s*!_enabled\.Value\s*\|\|\s*!string\.Equals\s*\(\s*GetCombatExecutionMode\s*\(\s*\)\s*,\s*CombatExecutionModeExecution') {
    throw "Execution slow-motion suppression must remain scoped to enabled Execution interaction starts."
}

if ($source -notmatch 'if\s*\(\s*state\.HasSlowDownTimeField\s*\)\s*\{\s*state\.DisableSlowDownTime\s*\(\s*\);\s*\}') {
    throw "Every KBM Execution must temporarily disable only its selected cached finisher asset's native slow-motion flag."
}

if ($source -notmatch '_slowDownTimeField\.SetValue\s*\(\s*_cachedData\s*,\s*false\s*\);' -or $source -notmatch '_slowDownTimeField\.SetValue\s*\(\s*_cachedData\s*,\s*OriginalSlowDownTime\.Value\s*\);') {
    throw "Execution slowDownTime must be suppressed and restored to its exact original value."
}

if ($source -notmatch 'public\s+static\s+void\s+Postfix\s*\(\s*ExecutionFinisherStartState\s+__state\s*\)' -or $source -notmatch 'public\s+static\s+Exception\s+Finalizer\s*\(\s*Exception\s+__exception\s*,\s*ExecutionFinisherStartState\s+__state\s*\)') {
    throw "Execution slowDownTime restoration must run through both postfix and finalizer paths."
}

if ($source -notmatch 'private\s+static\s+class\s+AutomaticCombatFinisherPatch[\s\S]*?out\s+AutomaticFinisherTriggerState\s+__state[\s\S]*?BeginAutomaticFinisherTrigger\(\)[\s\S]*?public\s+static\s+void\s+Postfix[\s\S]*?__state\.Restore\(\);[\s\S]*?public\s+static\s+Exception\s+Finalizer') {
    throw "Automatic finisher origin tracking must remain scoped and restore through postfix and finalizer paths."
}

if ($source -notmatch 'private\s+static\s+class\s+CombatFinisherLifecyclePatch[\s\S]*?FinisherStartedPrefix[\s\S]*?FinisherStartedPostfix[\s\S]*?FinisherStartedFinalizer[\s\S]*?FinisherExitedPrefix[\s\S]*?FinisherExitedPostfix[\s\S]*?FinisherExitedFinalizer[\s\S]*?RemoveSlowdownsPrefix[\s\S]*?RemoveSlowdownsPostfix[\s\S]*?RemoveSlowdownsFinalizer') {
    throw "All-finisher diagnostics must retain exception-safe start, exit, and native slowdown-cleanup telemetry."
}

if ($source -notmatch 'state\.PayloadSlowDownTime\s*==\s*true[\s\S]*?elapsedRealtime\s*>=\s*NativeFinisherStuckWarningSeconds[\s\S]*?Time\.timeScale\s*<=\s*0\.05f[\s\S]*?slowdownCount\.Value\s*>\s*0') {
    throw "Native finisher diagnostics must warn when an owned slowdown remains stuck beyond the real-time guard."
}

if ($source -notmatch 'if\s*\(\s*_restored\s*\)\s*\{\s*return;\s*\}') {
    throw "Execution temporary slowDownTime restoration must be idempotent."
}

if ($source -notmatch '_expandedExecutionTargets\.Value\s*\)\s*\{\s*string\s+exclusionReason;\s*if\s*\(\s*!IsExpandedExecutionTargetAllowed\s*\(\s*npc\s*,\s*out\s+exclusionReason\s*\)\s*\)') {
    throw "Expanded Execution targets must pass the abstract-family exclusion gate before finisher conditions are modified."
}

if ($source -notmatch 'base\.Config\.Bind\s*\(\s*section\s*,\s*key\s*,\s*defaultValue\s*,\s*Grailwright\.Shared\.ConfigUiDescription\.Create\s*\(\s*description\.Description\s*,\s*displaySection') {
    throw "FoA Mod Manager display sections must remain metadata-only while Config.Bind retains the original storage section and key."
}

if ($source -match 'new\s+AcceptableValueList<string>\s*\(\s*"GrailFloatingText"\s*,\s*"GameHud"') {
    throw "NotificationMode must retain free-text compatibility with its supported legacy aliases unless a schema reset is introduced."
}

if ($source -notmatch 'string\.Equals\s*\(\s*abstractName\s*,\s*excludedAbstracts\[i\]\s*,\s*StringComparison\.OrdinalIgnoreCase\s*\)' -or $source -notmatch 'release\.Invoke\s*\(\s*pooledAbstracts\s*,\s*null\s*\)') {
    throw "Expanded target abstract exclusions must use exact case-insensitive inherited-family matching and release the game's pooled list."
}

$removedSlowMotionContracts = @(
    'GloryKill',
    'Glory Kill',
    '"ExecutionSlowMotion"',
    'DirectTimeMultiplier',
    'ExecutionCinematic',
    'FmodTimeScaleParameterName',
    'ExecutionSlowMotionCue',
    'slowmo.wav',
    'GetRewardSoundCinematicPitchMultiplier',
    'UpdateActiveRewardSoundSlowMotionPitch'
)
foreach ($removed in $removedSlowMotionContracts) {
    if ($source.IndexOf($removed, [StringComparison]::Ordinal) -ge 0) {
        throw "Removed KBM Execution slow-motion code returned: $removed"
    }
}

if ($source -match 'Time\.timeScale\s*=') {
    throw "Killing Blow Mastery must not assign Time.timeScale directly."
}

if ($source.IndexOf('KillUnconsciousAction', [StringComparison]::Ordinal) -ge 0) {
    throw "Combat finisher controls must not patch or reference the story KillUnconsciousAction."
}

if ($source.IndexOf('private const int ConfigSchemaVersion = 19;', [StringComparison]::Ordinal) -lt 0) {
    throw "The new Execution and expanded-target defaults require schema 19."
}

if ($source -notmatch 'public\s+static\s+class\s+ExecutionVisualApi[\s\S]*?public\s+const\s+int\s+ApiVersion\s*=\s*1;[\s\S]*?TryGetState\(') {
    throw "The reflection-safe Execution visual API v1 is missing."
}

if ($source -notmatch 'GetOptionalFieldValue\(\s*finisherHandling,\s*"_npcPointingTowards"\s*\)[\s\S]*?new ExecutionFinisherStartState\([\s\S]*?executionTarget\)') {
    throw "Execution start does not retain the exact selected NPC target."
}

if ($source -notmatch 'progress01\s*=\s*state\.GetMaximumProgress\(this\);' -or
    $source -notmatch 'foreach\s*\(object\s+finisherState\s+in\s+_observedFinisherStates\)[\s\S]*?GetNativeFinisherProgress\(\s*finisherState\s*\)') {
    throw "Execution visual progress does not use the greatest available normalized time across participating finisher states."
}

if ($source -notmatch 'ReferenceEquals\(executionState\.Target, npc\)[\s\S]*?executionState\.ConfirmTargetDeath\(Time\.unscaledTime\);[\s\S]*?ClearDeadExecutionInteraction\(executionState\);') {
    throw "Execution death confirmation is not correlated to the exact active target."
}

if ($source -notmatch 'ReferenceEquals\([\s\S]*?active\.ExecutionAction,[\s\S]*?startState\.ExecutionAction\)[\s\S]*?active\.AddFinisherState\(finisherState\);' -or
    $source -notmatch '_activeFinisherStates\.Count' -or
    $source -notmatch 'state\.RemoveFinisherState\(finisherState\)') {
    throw "Duplicate native finisher-state listeners are not grouped into one logical Execution lifecycle."
}

if ($source -notmatch 'ExecutionDeathCompletionGraceSeconds = 1\.0f' -or
    $source -notmatch 'state\.TargetDeathConfirmed[\s\S]*?CompleteExecutionFinisherLifecycle\([\s\S]*?"target-death fallback"') {
    throw "A target-confirmed Execution cannot recover from a missing native OnExit callback."
}

if ($source -notmatch 'PatchDeadExecutionPromptCleanup\(\);' -or
    $source -notmatch 'discard\.Invoke\(executionAction, null\);' -or
    $source -notmatch '"_npcPointingTowards"[\s\S]*?targetField\.SetValue\(finisherHandling, null\);' -or
    $source -notmatch 'gameObject\.SetActive\(false\);') {
    throw "Dead Execution interactions and their stale native prompt are not cleared together."
}

if ($source -notmatch 'state\.Finished = true;' -or $source -notmatch 'PhaseCompleted' -or $source -notmatch 'ExecutionCompletedStateSeconds') {
    throw "Execution completion is not retained long enough for presentation consumers to observe it."
}

Write-Output "Killing Blow Mastery combat finisher contracts passed."
