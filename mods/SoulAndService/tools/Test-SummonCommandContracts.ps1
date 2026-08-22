$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulAndService.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SummonRuntime.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$matrix = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\TEST-MATRIX.md") -Raw

foreach ($required in @(
    'ConfigSchemaVersion = 10',
    'public enum SummonBehavior',
    'Guard = 0',
    'Bulwark = 1',
    'Hunt = 2',
    'PlayerAttackPassThroughMode.CombatOnly',
    'internal ConfigEntry<bool> AttackCommandPrompt',
    '"AttackCommandPrompt"',
    'internal ConfigEntry<bool> FormationCommands',
    '"FormationCommands"',
    'internal ConfigEntry<TargetCommandModifierMode> TargetCommandModifier',
    'TargetCommandModifierMode.Sprint',
    '"TargetCommandModifier"',
    '"ShareTargetMaxDistance"',
    '"Targeting Range"',
    'ks.tgfoa.battlecry-voice-tuner',
    'internal ConfigEntry<bool> PersistentServants',
    '"PersistentServants"',
    '45.0f')) {
    if (!$pluginSource.Contains($required)) {
        throw "Summon command configuration is missing: $required"
    }
}
if ($pluginSource.Contains('CombatInitiationMode')) {
    throw "Removed Combat Initiation Mode configuration remains in the plugin."
}
if (!$pluginSource.Contains('Maximum hero-to-target distance for passive crosshair sharing and explicit Attack, Hold, and Follow commands')) {
    throw "Targeting Range does not describe every behavior it controls."
}

foreach ($required in @(
    'NativePatrolRadius = 7.5f',
    'patrol.UpdateRadius(0.0f)',
    'patrol.UpdatePlace(summon.ParentModel.Coords)',
    'existingOverride.Target != null',
    'existingOverride.Discard()',
    'class SummonCommandInteractable',
    'SetInteractionOverride(_commandInteractable)',
    'RemoveInteractionOverride(_commandInteractable)',
    'public string DefaultActionName => string.IsNullOrEmpty(_actionName)',
    'CommandSummons(hero, target)',
    'BattlecryVoiceTuner.BattlecryVoiceTunerApi',
    'TryPlayCommandVoice(plugin, commandId);',
    'SummonAttackCommandId = "summon_attack"',
    'SummonHoldCommandId = "summon_hold"',
    'SummonFollowCommandId = "summon_follow"',
    'SummonGuardCommandId = "summon_guard"',
    'SummonBulwarkCommandId = "summon_bulwark"',
    'SummonHuntCommandId = "summon_hunt"',
    'FormationCommandHoldSeconds = 0.45f',
    'GetButtonHeld(KeyBindings.Gameplay.Sprint)',
    'HasAttackCommandControl()',
    'HasIndividualFormationControl()',
    'HasGlobalFormationControl()',
    'GetTargetingRange(plugin)',
    'TryFindFocusedFormationSummon(',
    'ResolveOwnedSummon(',
    'bounds.ClosestPoint(hero.Coords)',
    '"ParticleSystemRenderer"',
    'FormationCommandViewCaches',
    'Physics.RaycastNonAlloc(',
    'ResolveHitLocation(hit.collider)',
    'HasFormationCommandLineOfSight(',
    'Formation command focus: ',
    'HeldSummonCombatLeash = 8.0f',
    'ShouldOwnTakeAllHoldForInterop()',
    'CommandAllFormation(Hero.Current)',
    'nameof(VHeroKeys.PlayerKeyBindings)',
    'AppendCommandBindings(__result)',
    'ReleaseAllHeldSummons()',
    'EnforceHeldSummonLeash(summon)',
    'ReferenceEquals(explicitTarget.Target, currentTarget)',
    'DisplayName => string.Empty',
    'HeroSummonTargetOverride.AddSummonTargetOverrideElement',
    'BehaviorCommandHoldSeconds = 0.45f',
    'RecallCommandHoldSeconds = 2.0f',
    'StandardCommandFeedbackSeconds = 0.675f',
    'ExtendedCommandFeedbackSeconds = 1.35f',
    'SummonCommandState.Behavior',
    'KeyBindings.Gameplay.Interact',
    'Raycaster.GetAvailableActions().Any()',
    'TryCycleSummonBehavior(out behavior)',
    '"Behavior: " + behavior',
    'GetLastCommandPulseSecondsForInterop()',
    'ShowCommandFeedback(state, feedbackText, feedbackSeconds)',
    'SoulProgressionRuntime.ShowSummonCommand(feedbackText)',
    'new SummonCommandInteractable(',
    'ExplicitCommandTargets',
    'GetBulwarkAnchor(summon)',
    'GetGuardAnchor(summon)',
    'GetBulwarkVelocityScheme(',
    'leader.HorizontalVelocity.magnitude',
    'BulwarkLeaderRunSpeed = 3.0f',
    'behavior == SummonBehavior.Hunt',
    'GetAutonomousTargetPriority(',
    'RecentAttackerMemorySeconds = 6.0f',
    'GuardMeleeThreatRange = 8.0f',
    'GuardFormationInnerRadius = 4.5f',
    'GuardFormationRingSpacing = 1.5f',
    'GuardAnchorTolerance = 1.0f',
    'GuardAnchorRebaseDistance = 1.5f',
    'GuardIdleNoviceWanderRadius = 1.5f',
    'GuardIdleMasterWanderRadius = 0.75f',
    'GuardIdleNoviceMinimumStillSeconds = 12.0f',
    'GuardIdleMasterMinimumStillSeconds = 16.0f',
    'GuardIdleMasterMaximumStillSeconds = 26.0f',
    'GuardIdleHostAttemptCooldownSeconds = 1.0f',
    'GuardIdleStates',
    'GetGuardIdleAnchor(',
    'TryFindIdleDestination(',
    'RecallTargetSuppressionSeconds = 3.0f',
    'RecallPlacementInnerRadius = 3.5f',
    'RecallPlacementArcDegrees = 270.0f',
    'RecallPlacementMinimumSpacing = 2.25f',
    'RecallPlacementMaximumSnapDistance = 2.0f',
    'RecallPlacementHeroMoveReleaseDistance = 2.0f',
    'RecallSingleSideMinimumDegrees = 75.0f',
    'RecallSingleSideMaximumDegrees = 135.0f',
    'PendingRecallPlacements',
    'DestinationConsumed',
    'TryGetRecallAnchor(',
    'GetFormationHost(hero)',
    'NoviceAiDecisionInterval = 0.75f',
    'AiDecisionIntervalRefreshSeconds = 0.50f',
    'ControllerRefreshSeconds = 0.10f',
    'TransientStatePruneIntervalSeconds = 0.25f',
    'NextControllerRefreshBySummon',
    'ControlDiagnosticMinimumIntervalSeconds = 0.25f',
    'LastControlDiagnosticBySummon',
    'NextControlDiagnosticBySummon',
    'AutonomousLineOfSightCacheSeconds = 0.25f',
    'AutonomousTargetMinimumCommitmentSeconds = 1.75f',
    'AutonomousTargetSwitchDistanceRatio = 0.80f',
    'BulwarkDefenseRange = 6.0f',
    'BulwarkCombatLeash = 8.0f',
    'BulwarkAnchorTolerance = 0.5f',
    'BulwarkFormationInnerRadius = 3.5f',
    'BulwarkFormationSlotsPerRing = 4',
    'BulwarkFacingRebaseDegrees = 20.0f',
    'HuntFormationInnerRadius = 5.5f',
    'HuntFormationSlotsPerRing = 6',
    'HuntAnchorTolerance = 0.5f',
    'HuntIdleMinimumHeroDistance = 5.0f',
    'HuntMultipleWandererHostSize = 4',
    'HuntMaximumConcurrentWanderers = 2',
    'GuardIdleMasterMinimumStillSeconds = 16.0f',
    'GuardIdleMasterMaximumStillSeconds = 26.0f',
    'HasAutonomousTargetLineOfSight(',
    'SetAutonomousTargetOverride(summon, selectedTarget)',
    'RecallHost(Hero.Current)',
    'SoulSalvageRuntime.SpawnNecromanticSummonVfx(summon.ParentModel)',
    'GetProgressionSummonLimitBonus()')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Summon behavior contract is missing: $required"
    }
}

foreach ($required in @(
    'SwarmDurationSeconds = 5.0f',
    'SwarmMovementMultiplier = 1.25f',
    'SwarmFirstHitMultiplier = 1.25f',
    'MaximumCommandMovementMultiplier = 1.50f',
    'BeginSwarm(summon, target)',
    'TryConsumeSwarmHit(dealer, receiver)',
    'GetUpkeepPercentPerMinute(',
    'npc.HealthElement.Kill()',
    'TryEmpowerSummon(',
    'Mathf.Clamp(multiplier, 1.20f, 1.50f)',
    'string id = GetSummonId(npc);',
    'GetSummonId(receiver)',
    'GetSummonId(dealer)',
    ': empowerment.MovementMultiplier;',
    'BeforeRootMotionUpdateAnimator(',
    'plugin.PersistentServants.Value')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Summon upkeep, Swarm, or Empower contract is missing: $required"
    }
}
if ($pluginSource.Contains('PreventDismissOnRest') -or
    $pluginSource.Contains('PermanentReanimations')) {
    throw 'Removed rest or raised-decay configuration remains in the plugin.'
}

foreach ($required in @(
    'ShouldPassThroughOwnedSummon(',
    'hero.HeroCombat.IsHeroInFight',
    'npc.NpcAI.InCombat')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Hybrid attack-collision behavior is missing: $required"
    }
}

if ($runtimeSource.Contains('SummonAttackAction : AbstractHeroAction')) {
    throw "Attack command must not fire the target NPC's normal interaction scripting."
}
if ($runtimeSource -notmatch '(?s)UpdateEmpoweredPresentation\(NpcController controller\).*?string id = GetSummonId\(npc\);.*?EmpowermentStates\.TryGetValue\(id, out empowerment\).*?SwarmStates\.TryGetValue\(id, out swarm\)') {
    throw 'Empower and Swarm presentation do not resolve the owning summon element consistently.'
}
if ($runtimeSource -notmatch '(?s)ApplyEmpowermentVisual\(.*?GetEmpowermentVisualRoot\(controller\).*?expectedScale.*?OriginalLocalScale.*?OriginalLocalPosition.*?visualRoot\.localScale = expectedScale') {
    throw 'Empower does not continually enforce its visual-only scale from a stable baseline.'
}
if ($runtimeSource -notmatch '(?s)class EmpowermentVisualEnforcer : MonoBehaviour.*?LateUpdate\(\).*?ApplyLateEmpowermentVisual\(Controller\).*?EnsureEmpowermentVisualEnforcer\(.*?AddComponent<EmpowermentVisualEnforcer>.*?UpdateEmpoweredPresentation\(.*?EnsureEmpowermentVisualEnforcer\(controller\)') {
    throw 'Empower growth is not enforced after animation updates.'
}
if ($runtimeSource -notmatch '(?s)LogEmpowermentVisualCorrection\(.*?VisualDiagnosticCount >= 3.*?beforeLocal=.*?expectedLocal=.*?actualLocal=.*?lossy=.*?renderers=.*?boundsHeight=') {
    throw 'Empower visual diagnostics are not bounded or do not report transform and rendered-bounds evidence.'
}
if ($runtimeSource.Contains('1.0f / empowerment.MovementMultiplier')) {
    throw 'Empower still inverts locomotion playback and causes visible skating.'
}
if ($runtimeSource.Contains('class SummonFormationInteractionUI') -or
    $runtimeSource.Contains('_pendingFormationAction')) {
    throw "Individual formation commands still use the old hold interaction."
}
if ($runtimeSource -notmatch '(?s)RemoveAwarenessTargetsForSummon\(\s*string summonId,\s*NpcElement preservedTarget = null\).*?!ReferenceEquals\(record\.Target, preservedTarget\).*?record\.Summon\.RemoveCombatTarget\(record\.Target\);.*?AwarenessTargets\.Remove\(key\);') {
    throw "Awareness cleanup does not remove stale combat targets while preserving an explicit command target."
}
if ($runtimeSource -notmatch '(?s)SummonCommandState\.Attack,\s*SummonAttackCommandId,\s*useSwarm \? "Swarm" : "Attack",\s*StandardCommandFeedbackSeconds,\s*false\)') {
    throw "Attack and Swarm do not publish power-aware native feedback without GFT spam."
}
if ($runtimeSource -notmatch '(?s)new SummonCommandAction\(\s*target,\s*null,\s*Kind,\s*HasSwarmCommandControl\(\) \? "Swarm" : "Attack"\)') {
    throw "The hostile-target interaction prompt does not become Swarm at Power 90."
}
if ($runtimeSource -notmatch '(?s)hold \? "Hold" : "Follow",\s*StandardCommandFeedbackSeconds,\s*true\)') {
    throw "Individual Hold and Follow do not publish standard native and GFT feedback."
}
if ($runtimeSource -notmatch '(?s)followAll \? "Follow All" : "Hold All",\s*ExtendedCommandFeedbackSeconds,\s*true\)') {
    throw "Hold All and Follow All do not publish extended native and GFT feedback."
}
if ($runtimeSource -notmatch '(?s)string commandId = behavior == SummonBehavior\.Guard.*?SummonGuardCommandId.*?SummonBehavior\.Bulwark.*?SummonBulwarkCommandId.*?SummonHuntCommandId.*?SummonCommandState\.Behavior,\s*commandId,\s*feedback,\s*ExtendedCommandFeedbackSeconds,\s*true\)') {
    throw "Behavior does not publish extended native and GFT feedback."
}
if ($runtimeSource -notmatch '(?s)HandleBehaviorCommandInput\(.*?UIKeyUpAction && _behaviorCommandHeld.*?ResetBehaviorCommandHold\(\).*?UIKeyDownAction.*?_commandInteractable\.IsFeedback.*?ClearCommandOverride\(\).*?CanStartBehaviorCommandHold\(\).*?_behaviorCommandHeld = true') {
    throw "Behavior cycling does not release Interact independently or clear prior command feedback before restarting."
}
if ($runtimeSource -notmatch '(?s)UpdateBehaviorCommandHold\(\).*?CanMaintainBehaviorCommandHold\(\).*?Time\.unscaledTime - _behaviorCommandPressedAt.*?BehaviorCommandHoldSeconds.*?CycleSummonBehavior\(\)') {
    throw "Behavior cycling is not latched and evaluated independently of held UI events."
}
if ($runtimeSource -notmatch '(?s)UpdateCommandOverride\(.*?_commandInteractable != null\s*&& _commandInteractable\.IsFeedback.*?Time\.unscaledTime < _commandFeedbackEndsAt.*?return;.*?ClearCommandOverride\(\);.*?if \(_behaviorCommandHeld\)') {
    throw "Latched behavior input clears native feedback before its display interval expires."
}
if ($runtimeSource -match '(?s)HandleBehaviorCommandInput\(.*?CanMaintainBehaviorCommandHold\(\).*?UIKeyHeldAction') {
    throw "Behavior cycling still revalidates the crosshair during held UI events."
}
if ($runtimeSource -notmatch '(?s)UIKeyUpAction.*?_formationCommandArmedForRelease.*?!_recallCommandAttemptedForHold.*?CommandAllFormation\(Hero\.Current\).*?UpdateTakeAllItemsHold\(\).*?FormationCommandHoldSeconds.*?RecallCommandHoldSeconds.*?RecallHost\(Hero\.Current\)') {
    throw "Take All does not cleanly separate release-based formation commands from the two-second Recall Host command."
}
if ($runtimeSource -notmatch '(?s)GetBulwarkVelocityScheme\(.*?leader\.HorizontalVelocity\.magnitude.*?BulwarkLeaderRunSpeed.*?VelocityScheme\.Run.*?BulwarkLeaderMovingSpeed.*?VelocityScheme\.Trot.*?VelocityScheme\.Walk') {
    throw "Bulwark locomotion does not account for leader movement speed."
}
if ($runtimeSource -notmatch '(?s)GetBulwarkAnchor\(.*?countInRing == 2.*?25\.0f.*?countInRing == 3.*?45\.0f.*?60\.0f.*?Mathf\.Lerp\(-halfArc, halfArc.*?BulwarkFormationInnerRadius.*?BulwarkFormationRingSpacing.*?Vector3\.Angle\(_bulwarkForward, forward\).*?BulwarkFacingRebaseDegrees') {
    throw "Bulwark formation is not widened and protected from small stationary facing corrections."
}
if ($runtimeSource -notmatch '(?s)bool BeforeFindTarget\(.*?RefreshAutonomousTargets\(summon, plugin, behavior\);\s*return false;\s*}\s*return true;') {
    throw "Owned servants still run native target recalculation after Soul and Service makes its decision."
}
if ($runtimeSource -notmatch '(?s)GetAutonomousTargetPriority\(.*?behavior == SummonBehavior\.Bulwark.*?BulwarkDefenseRange.*?recentAttacker.*?return 0;.*?targetingProtected \? 1 : int\.MaxValue.*?if \(recentAttacker\).*?return 0;.*?if \(targetingProtected\).*?return 1;.*?target\.NpcAI\.InCombat.*?GuardMeleeThreatRange.*?return 2;.*?return int\.MaxValue;') {
    throw "Guard and Bulwark do not preserve their defensive threat-only priority orders."
}
if ($runtimeSource -match 'return hostInCombat \? 3 : int\.MaxValue') {
    throw "Guard still falls back to pulling any visible faction-hostile once the host enters combat."
}
if ($runtimeSource -notmatch '(?s)committedRecord != null.*?AutonomousTargetMinimumCommitmentSeconds.*?AutonomousTargetSwitchDistanceRatio.*?SelectedAt = now') {
    throw "Autonomous targets do not have bounded commitment and same-priority switch hysteresis."
}
if ($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSightFrom\(.*?string cacheKey = observer == null.*?AutonomousLineOfSightByTarget\.TryGetValue\(cacheKey.*?cached\.ExpiresAt >= Time\.unscaledTime.*?return cached\.Visible;.*?Physics\.RaycastNonAlloc.*?AutonomousLineOfSightByTarget\[cacheKey\].*?AutonomousLineOfSightCacheSeconds') {
    throw "Autonomous line-of-sight checks are not shared briefly across the host."
}
if ($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSight\(\s*NpcHeroSummon summon,\s*Hero hero,\s*NpcElement target,\s*SummonBehavior behavior\).*?summonObserver.*?behavior == SummonBehavior\.Hunt.*?HasAutonomousTargetLineOfSightFrom\(\s*summonObserver.*?return HasAutonomousTargetLineOfSightFrom\(null, hero, target\)\s*\|\|.*?HasAutonomousTargetLineOfSightFrom\(\s*summonObserver') {
    throw "Hunt sight is not servant-local or defensive sight is not shared by the hero and acting servant."
}
if ($runtimeSource -notmatch '(?s)!AwarenessTargets\.TryGetValue\(selectedKey, out selectedRecord\).*?!owner\.ForceAddCombatTarget\(.*?ClearAutonomousTargetOverride\(summon\);\s*return;.*?AwarenessTargets\[selectedKey\].*?SetAutonomousTargetOverride\(summon, selectedTarget\)') {
    throw "Autonomous overrides can still bypass a native combat-target rejection."
}
if ($runtimeSource -notmatch '(?s)CycleSummonBehavior\(\).*?TryCycleSummonBehavior.*?foreach \(NpcHeroSummon summon.*?RefreshAutonomousTargets\(\s*summon,\s*plugin,\s*behavior\);.*?EnforceSummonBehavior\(summon, behavior\);') {
    throw "A completed behavior change does not refresh targets before enforcing its new formation."
}
if ($runtimeSource -match '(?s)CycleSummonBehavior\(\).*?TryCycleSummonBehavior.*?RemoveAllAwarenessTargets\(\).*?PublishCommand') {
    throw "Behavior changes still discard the entire host's valid target state before refreshing."
}
if ($runtimeSource -notmatch '(?s)SetExplicitCommandTarget\(.*?RemoveAwarenessTargetsForSummon\(summonId, target\);.*?!ReferenceEquals\(previousTarget, target\).*?RemoveCombatTarget\(previousTarget\).*?ExplicitCommandTargets\[summonId\] = target.*?AddSummonTargetOverrideElement\(\s*summon,\s*target,\s*10\).*?NpcAI\.InCombat.*?ForceAddCombatTarget\(\s*target,\s*recalculateTarget: true\).*?NpcAI\.EnterCombatWith\(\s*target,\s*forceChange: true\).*?CommandSummons\(.*?SetExplicitCommandTarget\(summon, target\)') {
    throw "Explicit Attack and Swarm commands do not replace stale ownership and enter combat with their ordered target."
}
if ($runtimeSource -notmatch '(?s)HasExplicitCommandTarget\(summon\).*?RemoveAwarenessTargetsForSummon\(\s*summonId,\s*ExplicitCommandTargets\[summonId\]\);') {
    throw "Autonomous refresh can still remove the live explicit command target."
}
if ($runtimeSource -notmatch '(?s)bool invalid = record\.Target == null.*?GetAutonomousTargetPriority\(\s*record\.Target,\s*behavior,\s*hero,\s*hostInCombat\) == int\.MaxValue') {
    throw "Behavior changes do not invalidate autonomous targets forbidden by the new mode."
}
if ($runtimeSource -notmatch '(?s)selectedTarget == null.*?ForceAddCombatTarget\(.*?owner\.NpcAI != null && !owner\.NpcAI\.InCombat.*?owner\.NpcAI\.EnterCombatWith\(\s*selectedTarget,\s*forceChange: true\).*?SetAutonomousTargetOverride') {
    throw "Autonomous target ownership does not explicitly enter combat after native FindTarget is suppressed."
}
if ($runtimeSource -notmatch '(?s)SetAutonomousTargetOverride\(.*?ClearAutonomousTargetOverride\(summon\);\s*AutonomousTargetOverrides\[summonId\] = target;\s*HeroSummonTargetOverride\.AddSummonTargetOverrideElement') {
    throw "Autonomous override ownership is not established before marker initialization re-enters targeting."
}
if ($runtimeSource -notmatch '(?s)LogSummonControlState\(.*?LastControlDiagnosticBySummon\.TryGetValue.*?string\.Equals.*?NextControlDiagnosticBySummon\.TryGetValue.*?ControlDiagnosticMinimumIntervalSeconds.*?Summon control: summon=') {
    throw "Temporary summon ownership diagnostics are not state-change-only and rate bounded."
}
if ($runtimeSource -notmatch '(?s)bool BeforeFindTarget\(.*?IsRecallTargetSuppressed\(summon\).*?return false;.*?RefreshAutonomousTargets\(summon, plugin, behavior\).*?AfterFindTarget\(.*?IsRecallTargetSuppressed\(summon\).*?return;.*?EnforceSummonBehavior\(summon, behavior\)') {
    throw "Autonomous priority targets are not injected before native selection or suppressed during Recall Host."
}
if ($runtimeSource -notmatch '(?s)typeof\(EnemyBaseClass\),\s*"UpdateCombatSlotStatus",\s*new\[\] \{ typeof\(ICharacter\) \}.*?nameof\(AfterCombatSlotStatusUpdate\).*?AfterCombatSlotStatusUpdate\(.*?target is Hero.*?OwnedCombatSlotIndex == -1.*?npc\.IsHeroSummon.*?ReleaseCombatSlots\(\)') {
    throw "Hero summons do not release stale hero-centered combat slots while pursuing non-Hero targets."
}
if ($runtimeSource -notmatch '(?s)BeforeTeleportToAlly\(.*?pending\.OutstandingRequests\+\+.*?OutstandingRequests = 1.*?AfterTeleportPathCalculated\(.*?Coords - pending\.Origin.*?> 0\.25f.*?PendingTeleportVfxBySummon\.Remove\(summonId\).*?SpawnNecromanticSummonVfx\(summon\.ParentModel\).*?pending\.OutstandingRequests--.*?pending\.OutstandingRequests <= 0.*?PendingTeleportVfxBySummon\.Remove\(summonId\)') {
    throw "Native teleport VFX does not coalesce overlapping requests or gate playback on confirmed movement."
}
if ($runtimeSource -notmatch '(?s)RecallHost\(Hero hero\).*?RecallTargetSuppressionUntil\[summonId\].*?RecallTargetSuppressionSeconds.*?ExplicitCommandTargets\.Remove.*?RemoveAwarenessTargetsForSummon.*?SetSummonHeld\(summon, false\).*?ForceEndCombat\(\).*?TryFindRecallPlacement\(.*?PendingRecallPlacements\[summonId\].*?TeleportToAllyMethod\.Invoke.*?recalled == 1 \? "Recall" : "Recall Host"') {
    throw "Recall does not clear combat state, assign safe placements, use native teleporting, and select singular or host feedback."
}
if ($runtimeSource -notmatch '(?s)TryFindRecallPlacement\(.*?RecallPlacementsPerRing.*?RecallPlacementArcDegrees.*?AstarPath\.active\.GetNearest.*?NNConstraint\.Walkable.*?PathUtilities\.IsPathPossible.*?reservedPlacements\.Any.*?RecallPlacementMinimumSpacing') {
    throw "Recall placement does not use navigable, non-overlapping randomized ring slots."
}
if ($runtimeSource -notmatch '(?s)TryFindRecallPlacement\(.*?countInRing == 1.*?RecallSingleSideMinimumDegrees.*?RecallSingleSideMaximumDegrees.*?snapped - candidate.*?RecallPlacementMaximumSnapDistance') {
    throw "Recall does not side-bias a lone servant or reject misleading navigation snaps."
}
if ($runtimeSource -notmatch '(?s)BeforeNpcTeleport\(.*?PendingRecallPlacements\.TryGetValue.*?destination\.position = placement\.Position.*?placement\.DestinationConsumed = true.*?TryGetRecallAnchor\(.*?RecallPlacementHeroMoveReleaseDistance.*?BeforeStayCloseToAlly\(.*?TryGetRecallAnchor\(summon, out recallAnchor\).*?patrol\.UpdatePlace\(recallAnchor\)') {
    throw "Native Recall placement is not consumed by teleport and preserved as a temporary formation anchor."
}
if ($runtimeSource -notmatch '(?s)GetAiTickInterval\(NpcAlly ally\).*?return 2\.5f;.*?configuredInterval.*?NoviceAiDecisionInterval.*?GetNecromanticPower\(\) / 100\.0f.*?Mathf\.Lerp\(.*?noviceInterval.*?configuredInterval.*?AiDecisionIntervalRefreshSeconds') {
    throw "Hero-summon decision speed does not progress from novice to configured full-mastery responsiveness."
}
if ($runtimeSource -notmatch '(?s)GetFormationHost\(Hero hero\).*?Time\.frameCount.*?World\.All<NpcHeroSummon>\(\).*?OrderBy.*?GetHuntAnchor\(.*?GetFormationHost\(hero\).*?GetBulwarkAnchor\(.*?GetFormationHost\(hero\).*?GetGuardAnchor\(.*?GetFormationHost\(hero\)') {
    throw "Guard, Bulwark, and Hunt do not share the ordered formation host once per frame."
}
if ($runtimeSource -notmatch '(?s)GetGuardIdleAnchor\(.*?hero\.HorizontalVelocity\.magnitude.*?IsHostInCombat\(hero\).*?GuardAnchorRebaseDistance.*?state\.FormationAnchor = liveAnchor.*?state\.Wandering.*?state\.Returning.*?_guardIdleMoverId') {
    throw "Stationary Guard anchors are not latched with bounded wander and return states."
}
if ($runtimeSource -notmatch '(?s)GetGuardIdleAnchor\(\s*summon,\s*out anchorTolerance,\s*out gentleIdleMovement\).*?gentleIdleMovement\s*\? VelocityScheme\.Walk') {
    throw "Guard idle wandering and returning do not use gentle walking locomotion."
}
if ($runtimeSource -notmatch '(?s)GetGuardIdleWanderRadius\(\).*?Mathf\.Clamp01\(.*?GetNecromanticPower\(\) / 100\.0f.*?Mathf\.Lerp\(.*?GuardIdleNoviceWanderRadius.*?GuardIdleMasterWanderRadius') {
    throw "Guard idle movement does not narrow through Power 100 and remain capped afterward."
}
if ($runtimeSource -notmatch '(?s)ScheduleNextGuardIdleWander\(.*?Mathf\.Clamp01\(.*?GetNecromanticPower\(\) / 100\.0f.*?GuardIdleNoviceMinimumStillSeconds.*?GuardIdleMasterMinimumStillSeconds.*?GuardIdleNoviceMaximumStillSeconds.*?GuardIdleMasterMaximumStillSeconds') {
    throw "Guard idle still periods do not lengthen through Power 100."
}
if ($runtimeSource -notmatch '(?s)now >= _nextIdleHostAttemptAt.*?_nextIdleHostAttemptAt = now.*?GuardIdleHostAttemptCooldownSeconds.*?TryFindIdleDestination\(.*?AstarPath\.active\.GetNearest.*?PathUtilities\.IsPathPossible') {
    throw "Idle navigation is not limited across the host or validated only when scheduling movement."
}
if ($runtimeSource -notmatch '(?s)GetHuntIdleAnchor\(.*?hero\.HorizontalVelocity\.magnitude.*?IsHostInCombat\(hero\).*?HuntAnchorRebaseDistance.*?HuntIdleMoverIds\.Count < maximumWanderers.*?TryFindIdleDestination\(.*?HuntIdleMinimumHeroDistance.*?ScheduleNextHuntIdleWander') {
    throw "Hunt perimeter anchors do not provide bounded, combat-aware scouting movement."
}
if ($runtimeSource -notmatch '(?s)GetHuntAnchor\(.*?HuntFormationSlotsPerRing.*?90\.0f \+ \(\(360\.0f \* slot\) / countInRing\).*?HuntFormationInnerRadius.*?HuntFormationRingSpacing') {
    throw "Hunt does not distribute servants across stable full-perimeter rings."
}
if ($runtimeSource -notmatch '(?s)UpdateFormationPatrolPlace\(.*?FormationPatrolAnchors\.TryGetValue.*?FormationPatrolAnchorUpdateDistance.*?patrol\.UpdatePlace\(anchor\)') {
    throw 'Stable formations still request an identical navigation destination every AI decision.'
}
if ($runtimeSource -notmatch '(?s)GetAutonomousTargetCandidates\(.*?behavior != SummonBehavior\.Bulwark.*?_bulwarkTargetCandidateExpiresAt.*?grid\.GetNpcsInSphere\(.*?\.ToArray\(\)') {
    throw 'Bulwark does not share its short-lived nearby-target candidate query across the host.'
}
if ($runtimeSource -notmatch '(?s)ReadSteelAndBoneAwarenessMultipliers\(.*?_nextSteelAndBoneAwarenessRefreshAt.*?SteelAndBoneAwarenessCacheSeconds.*?_cachedSteelAndBoneSightMultiplier.*?_cachedSteelAndBoneAggroMultiplier') {
    throw 'Steel and Bone awareness interop is still reflected separately for every servant decision.'
}
if ($runtimeSource -notmatch '(?s)TryFindIdleDestination\(.*?minimumDistance > 0\.0f.*?snapped - minimumDistanceOrigin.*?< minimumDistance \* minimumDistance') {
    throw "Idle scouting destinations do not enforce Hunt's inner boundary."
}
if ($runtimeSource -notmatch '(?s)CommandSummons\(.*?ClearIdleMovementState\(summonId\).*?SetSummonHeld\(.*?ClearIdleMovementState\(summonId\).*?RecallHost\(Hero hero\).*?ClearIdleMovementState\(summonId\)') {
    throw "Attack, formation, and Recall commands do not cancel behavior idle movement."
}
if ($runtimeSource -notmatch '(?s)CycleSummonBehavior\(\).*?GuardIdleStates\.Clear\(\).*?HuntIdleStates\.Clear\(\).*?HuntIdleMoverIds\.Clear\(\).*?_nextIdleHostAttemptAt = 0\.0f') {
    throw "Behavior changes do not clear prior Guard and Hunt idle movement state."
}
if ($runtimeSource -notmatch '(?s)AfterNpcControllerUpdate\(NpcController __instance\).*?NextControllerRefreshBySummon\.TryGetValue.*?ControllerRefreshSeconds.*?ApplyIdleVolume.*?UpdateCatchUpSpeed.*?UpdateEmpoweredPresentation') {
    throw "Per-controller summon presentation work is not bounded to its refresh cadence."
}

foreach ($required in @(
    '45 m',
    'capped at 75% of binding cost',
    'Version under test: 1.0.6',
    'SAS-SMOKE-30',
    'SAS-SMOKE-31',
    'SAS-SMOKE-16',
    'SAS-SMOKE-25',
    'SAS-SMOKE-32',
    'SAS-SMOKE-33')) {
    if (!$readme.Contains($required) -and !$matrix.Contains($required)) {
        throw "Summon command documentation is missing: $required"
    }
}

Write-Host "Soul and Service idle, targeting, and Attack command contracts passed."
