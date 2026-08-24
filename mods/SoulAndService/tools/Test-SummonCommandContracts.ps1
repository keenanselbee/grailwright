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
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw

if (!$manifest.Contains('Awaken.Kandra.dll')) {
    throw 'Soul and Service does not compile against the Kandra renderer assembly used by Empower.'
}
if (!$manifest.Contains('Animancer.dll')) {
    throw 'Soul and Service does not compile against the native animation state assembly used by summon readiness.'
}

foreach ($required in @(
    'ConfigSchemaVersion = 11',
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
    'internal ConfigEntry<float> IdleMovementAmount',
    '"IdleMovementAmount"',
    '"PersistentServants"',
    '45.0f')) {
    if (!$pluginSource.Contains($required)) {
        throw "Summon command configuration is missing: $required"
    }
}

if (($runtimeSource -notmatch '(?s)GetGuardAnchor\(.*?formationForward = GetGuardFormationForward\(hero\).*?Quaternion\.AngleAxis\(angle, Vector3\.up\).*?formationForward') -or
    ($runtimeSource -notmatch '(?s)GetGuardFormationForward\(Hero hero\).*?movementForward = hero\.HorizontalVelocity;.*?_guardForward = movementForward\.normalized;.*?return _guardForward')) {
    throw "Guard formation facing is not latched from meaningful hero movement."
}
if ($runtimeSource -notmatch '(?s)internal static void Update\(\).*?if \(!plugin\.IsEnabled\).*?return;.*?UpdateFormationLeaderMotion\(Hero\.Current\);.*?UpdateSwarmStates\(\);') {
    throw 'Meaningful hero movement is not sampled once per rendered frame before summon upkeep.'
}
foreach ($required in @(
    'private static float GetIdleMovementAmount()',
    'GetGuardIdleWanderRadius()',
    '* GetIdleMovementAmount();',
    'HuntIdleWanderRadius * idleMovementAmount',
    'if (GetIdleMovementAmount() <= 0.001f)',
    'if (idleMovementAmount <= 0.001f)')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Idle movement amount contract is missing: $required"
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
    'HuntAwarenessRange = 45.0f',
    'AttackCommandAimRadius = 0.25f',
    'AttackCommandFocusGraceSeconds = 0.30f',
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
    'SummonRecallCommandId = "summon_recall"',
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
    'RecallCommandHoldSeconds = 1.5f',
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
    'GetGuardFormationForward(hero)',
    'GetBulwarkVelocityScheme(',
    'leader.HorizontalVelocity.magnitude',
    'BulwarkLeaderRunSpeed = 3.0f',
    'BulwarkCameraFacingHoldSeconds = 0.30f',
    'BulwarkCameraFacingCooldownSeconds = 0.45f',
    'BulwarkCameraFacingMinimumAngle = 30.0f',
    'BulwarkCameraFacingStabilityAngle = 12.0f',
    'behavior == SummonBehavior.Hunt',
    'GetAutonomousTargetPriority(',
    'RecentAttackerMemorySeconds = 6.0f',
    'GuardMeleeThreatRange = 8.0f',
    'GuardFormationInnerRadius = 4.5f',
    'GuardFormationRingSpacing = 1.5f',
    'GuardAnchorTolerance = 1.25f',
    'GuardAnchorRebaseDistance = 2.0f',
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
    'AnimationWatchdogMovementSpeed = 0.10f',
    'AnimationWatchdogRecoveryCooldownSeconds = 2.0f',
    'AnimationWatchdogFailureSamples = 3',
    'SpawnReadinessBySummon',
    'AnimationWatchdogsBySummon',
    'TransientStatePruneIntervalSeconds = 0.25f',
    'NextControllerRefreshBySummon',
    'ControlDiagnosticMinimumIntervalSeconds = 0.25f',
    'LastControlDiagnosticBySummon',
    'NextControlDiagnosticBySummon',
    'AutonomousLineOfSightCacheSeconds = 0.25f',
    'AutonomousTargetMinimumCommitmentSeconds = 1.75f',
    'AutonomousTargetSwitchDistanceRatio = 0.80f',
    'BulwarkCloseGuardDefenseRange = 5.0f',
    'BulwarkCloseGuardCombatLeash = 6.0f',
    'BulwarkCloseGuardLocalEngageRange = 2.5f',
    'BulwarkCloseGuardLocalRetentionRange = 4.0f',
    'BulwarkAdvanceBreachRange = 3.0f',
    'BulwarkAdvanceRetentionRange = 4.0f',
    'BulwarkAdvanceCombatLeash = 5.5f',
    'BulwarkTargetCandidateRange = 10.0f',
    'BulwarkAnchorTolerance = 0.75f',
    'BulwarkAdvanceAnchorTolerance = 1.25f',
    'BulwarkAdvanceResumeDistance = 1.75f',
    'BulwarkAdvanceRunDistance = 2.0f',
    'BulwarkAdvanceAnchorUpdateDistance = 0.35f',
    'BulwarkAdvanceProgressDistance = 0.10f',
    'BulwarkAdvanceBlockedSeconds = 0.75f',
    'BulwarkAdvanceFallbackSeconds = 1.0f',
    'BulwarkAdvanceFallbackProbeRadius = 0.75f',
    'BulwarkAdvanceFallbackMinimumOffset = 0.35f',
    'BulwarkAdvanceFallbackCandidateSnapDistance = 0.50f',
    'BulwarkAdvanceMaximumAnchorSnapDistance = 1.5f',
    'BulwarkCatchUpStartDistance = 2.0f',
    'BulwarkCatchUpStopDistance = 1.0f',
    'BulwarkAdvanceCatchUpStartDistance = 2.0f',
    'BulwarkAdvanceCatchUpStopDistance = 1.25f',
    'BulwarkFormationInnerRadius = 3.5f',
    'BulwarkFormationSlotsPerRing = 4',
    'BulwarkCloseGuardSlotsPerRing = 5',
    'BulwarkAdvancePredictionSeconds = 0.40f',
    'BulwarkAdvanceCatchUpMinimumMultiplier = 1.60f',
    'BulwarkAdvanceMaximumMovementMultiplier = 1.75f',
    'EnsureFormationFacingHero(hero)',
    'HuntFormationInnerRadius = 5.5f',
    'HuntFormationSlotsPerRing = 6',
    'HuntAnchorTolerance = 1.25f',
    'HuntAnchorRebaseDistance = 2.5f',
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
    'NpcHeroSummon summon = __instance.Npc',
    'UpdateEmpoweredPresentation(__instance, summonId);',
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
    'npc.NpcAI.InCombat',
    'CollisionColliderBuffer',
    'RefreshPlayerPassThroughColliders(state)')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Hybrid attack-collision behavior is missing: $required"
    }
}
if ($runtimeSource -notmatch '(?s)AfterToggleWalkThroughColliders\(.*?ApplyPlayerPassThrough\(__instance, true\).*?ApplyPlayerPassThrough\(.*?if \(state != null\)\s*\{\s*return;\s*\}.*?RefreshPlayerPassThroughColliders\(state\).*?RefreshPlayerPassThroughColliders\(\s*CollisionState state\).*?CollisionColliderBuffer\.Clear\(\).*?GetComponentsInChildren<Collider>\(\s*true,\s*CollisionColliderBuffer\).*?SummonColliderIds\.Add\(collider\.GetInstanceID\(\)\).*?IgnoreCollision\(state\.HeroCollider, collider, true\)') {
    throw 'Player pass-through does not limit allocation-free hierarchy discovery to initialization and forced native collider refreshes.'
}
if ($runtimeSource.Contains('.GetComponentsInChildren<Collider>(true)')) {
    throw 'Player pass-through restored an allocating summon-collider hierarchy scan.'
}
if ($runtimeSource.Contains('CollisionHierarchySafetyRefreshSeconds') -or
    $runtimeSource.Contains('NextHierarchyRefreshAt')) {
    throw 'Player pass-through retained a recurring collider hierarchy safety scan.'
}

if ($runtimeSource.Contains('SummonAttackAction : AbstractHeroAction')) {
    throw "Attack command must not fire the target NPC's normal interaction scripting."
}
if ($runtimeSource -notmatch '(?s)UpdateEmpoweredPresentation\(\s*NpcController controller,\s*string id\).*?EmpowermentStates\.TryGetValue\(id, out empowerment\).*?SwarmStates\.TryGetValue\(id, out swarm\)') {
    throw 'Empower and Swarm presentation do not reuse the owning summon ID consistently.'
}
if ($runtimeSource -notmatch '(?s)ApplyEmpowermentVisual\(.*?GetEmpowermentVisualRoot\(controller\).*?expectedScale.*?OriginalLocalScale.*?OriginalLocalPosition.*?visualRoot\.localScale = expectedScale') {
    throw 'Empower does not continually enforce its visual-only scale from a stable baseline.'
}
if ($runtimeSource -notmatch '(?s)VisualMarker.*?NextVisualRootLookupTime.*?ApplyEmpowermentVisual\(.*?!ReferenceEquals\(state\.VisualMarker, visualMarker\).*?Time\.unscaledTime < state\.NextVisualRootLookupTime.*?Time\.unscaledTime \+ 0\.5f.*?GetEmpowermentVisualRoot\(controller\).*?GetEmpowermentVisualRoot\(NpcController controller\).*?IsSafeEmpowermentVisualRoot\(controller, visualRoot\).*?while \(ancestor != null && !ReferenceEquals\(ancestor, controller\.transform\)\).*?IsSafeEmpowermentVisualRoot\(controller, ancestor\).*?private static bool IsSafeEmpowermentVisualRoot\(.*?HasRenderableGeometry\(root\).*?GetComponentInChildren<KandraRenderer>\(true\).*?!ReferenceEquals\(root, animatorRoot\).*?animatorRoot\.IsChildOf\(root\).*?private static bool HasRenderableGeometry\(Transform root\).*?GetComponentsInChildren<Renderer>\(true\).*?IsEffectOnlyRenderer\(renderer\).*?GetComponentInChildren<KandraRenderer>\(true\).*?private static bool IsEffectOnlyRenderer\(Renderer renderer\).*?"ParticleSystemRenderer".*?"TrailRenderer".*?"LineRenderer".*?"VFXRenderer"') {
    throw 'Empower does not cache a renderer-bearing visual root below the locomotion controller.'
}
if (!$runtimeSource.Contains('kandraRenderers=') -or
    !$runtimeSource.Contains('GetComponentsInChildren<KandraRenderer>(true)')) {
    throw 'Empower diagnostics do not report bounded Kandra-renderer evidence.'
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
if ($runtimeSource -notmatch '(?s)RemoveAwarenessTargetsForSummon\(\s*NpcHeroSummon summon,\s*NpcElement preservedTarget = null\).*?string summonId = \(\(Model\)summon\)\.ID;.*?AwarenessTargets\.TryGetValue\(summonId, out record\).*?!ReferenceEquals\(record\.Target, preservedTarget\).*?record\.Summon\.RemoveCombatTarget\(record\.Target\);.*?AwarenessTargets\.Remove\(summonId\);') {
    throw "Awareness cleanup does not remove stale combat targets while preserving an explicit command target."
}
if ($runtimeSource -notmatch '(?s)SummonCommandState\.Attack,\s*SummonAttackCommandId,\s*useSwarm \? "Swarm" : "Attack",\s*StandardCommandFeedbackSeconds,\s*false\)') {
    throw "Attack and Swarm do not publish power-aware native feedback without GFT spam."
}
if ($runtimeSource -notmatch '(?s)new SummonCommandAction\(\s*target,\s*null,\s*Kind,\s*HasSwarmCommandControl\(\) \? "Swarm" : "Attack"\)') {
    throw "The hostile-target interaction prompt does not become Swarm at Power 90."
}
if ($runtimeSource -notmatch '(?s)TryFindFreshAttackCommandTarget\(.*?Physics\.SphereCastNonAlloc\(.*?AttackCommandAimRadius.*?nearestBlockingDistance.*?RememberAttackCommandTarget.*?TryGetRecentAttackCommandTarget\(.*?AttackCommandFocusGraceSeconds') {
    throw "Attack and Swarm targeting is not tolerant, obstruction-aware, and briefly sticky."
}
if ($runtimeSource -notmatch '(?s)UpdateCommandOverride\(.*?TryFindFreshAttackCommandTarget\(.*?TryFindFocusedFormationSummon\(.*?TryGetRecentAttackCommandTarget\(') {
    throw "Fresh enemy focus does not outrank formation focus while sticky enemy focus yields to an explicitly aimed servant."
}
if (!$runtimeSource.Contains('public bool IsFeedbackOnly => _feedbackOnly;')) {
    throw "Feedback-only interaction actions do not expose their presentation-only role to soft integrations."
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
if ($runtimeSource -notmatch '(?s)GetBulwarkVelocityScheme\(.*?bool advanceHeld.*?leader\.HorizontalVelocity\.magnitude.*?if \(advanceHeld\).*?BulwarkAdvanceRunDistance.*?VelocityScheme\.Run.*?BulwarkAdvanceAnchorTolerance.*?VelocityScheme\.Trot.*?VelocityScheme\.Walk.*?BulwarkLeaderRunSpeed.*?VelocityScheme\.Run.*?BulwarkLeaderMovingSpeed.*?VelocityScheme\.Trot.*?VelocityScheme\.Walk') {
    throw "Bulwark locomotion does not run for distant Advance recovery, trot on approach, and retain leader-aware Close Guard movement."
}
if ($runtimeSource -notmatch '(?s)GetBulwarkAnchor\(.*?EnsureFormationFacingHero\(hero\).*?advanceHeld = IsBulwarkAdvanceHeld\(hero\).*?BulwarkFormationSlotsPerRing.*?BulwarkCloseGuardSlotsPerRing.*?if \(advanceHeld\).*?countInRing == 2.*?25\.0f.*?countInRing == 3.*?45\.0f.*?60\.0f.*?else.*?180\.0f.*?90\.0f.*?270\.0f.*?BulwarkFormationInnerRadius.*?leaderAnchor = advanceHeld\s*\? hero\.Coords\s*:\s*GetFormationLeaderAnchor\(hero\).*?if \(advanceHeld\)\s*\{.*?UpdateBulwarkFacing\(hero\).*?formationForward = _bulwarkForward.*?hero\.HorizontalVelocity\s*\* BulwarkAdvancePredictionSeconds.*?\}\s*else\s*\{.*?formationForward = GetGuardFormationForward\(hero\).*?GetStabilizedBulwarkAdvanceAnchor\(summon, desiredAnchor\)') {
    throw "Bulwark does not switch between its predictive forward wall and 3.5-meter side/rear guard slots."
}
if (($runtimeSource -notmatch '(?s)class BulwarkAdvanceSlotState.*?DesiredAnchor.*?ResolvedAnchor.*?LastProgressPosition.*?LastProgressAt.*?FallbackUntil.*?HasAnchor.*?Satisfied') -or
    ($runtimeSource -notmatch '(?s)GetStabilizedBulwarkAdvanceAnchor\(.*?BulwarkAdvanceSlotStates\.TryGetValue.*?if \(state\.Satisfied\).*?BulwarkAdvanceResumeDistance.*?state\.Satisfied = false.*?BulwarkAdvanceAnchorUpdateDistance.*?BulwarkAdvanceAnchorTolerance.*?summonPosition - state\.LastProgressPosition.*?BulwarkAdvanceProgressDistance.*?BulwarkAdvanceBlockedSeconds.*?TryResolveReachableBulwarkAnchor.*?BulwarkAdvanceFallbackSeconds')) {
    throw "Bulwark Advance does not retain per-servant arrival hysteresis and invoke blocked-slot recovery only after progress stalls."
}
$desiredMoveBlock = [regex]::Match(
    $runtimeSource,
    '(?s)bool desiredMoved = .*?(?=\s*if \(state\.FallbackUntil > now\))')
if (!$desiredMoveBlock.Success -or
    $desiredMoveBlock.Value.Contains('LastProgressAt = now') -or
    $desiredMoveBlock.Value.Contains('LastProgressPosition = summonPosition')) {
    throw "Moving the desired Bulwark slot still resets servant progress and can suppress blocked-slot recovery."
}
if ($runtimeSource -notmatch '(?s)TryResolveReachableBulwarkAnchor\(.*?sourceNode = AstarPath\.active\.GetNearest\(.*?NNConstraint\.Walkable.*?for \(int attempt = 0; attempt < 5; attempt\+\+\).*?BulwarkAdvanceFallbackProbeRadius.*?PathUtilities\.IsPathPossible.*?BulwarkAdvanceFallbackCandidateSnapDistance.*?BulwarkAdvanceFallbackMinimumOffset.*?BulwarkAdvanceMaximumAnchorSnapDistance.*?resolvedAnchor = nearest\.position.*?return found;') {
    throw "Bulwark blocked-slot recovery does not select a distinct nearby reachable walkable anchor from bounded deterministic probes."
}
if ($runtimeSource -notmatch '(?s)behavior = SoulProgressionRuntime\.GetSummonBehavior\(\);.*?behavior != SummonBehavior\.Bulwark.*?BulwarkAdvanceSlotStates\.Remove') {
    throw "Effective behavior fallback does not clear stale Bulwark Advance arrival state."
}
if ($runtimeSource -notmatch '(?s)UpdateFormationPatrolPlace\(\s*summon,\s*patrol,\s*anchor,\s*bulwarkAdvance\s*\? BulwarkAdvanceAnchorUpdateDistance\s*:\s*FormationPatrolAnchorUpdateDistance\).*?GetBulwarkVelocityScheme\(\s*summon\.Ally,\s*anchorDistanceSqr,\s*bulwarkAdvance\)') {
    throw "Bulwark Advance does not use its lower-churn anchor updates and stance-aware locomotion in the patrol path."
}
if ($runtimeSource -notmatch '(?s)UpdateBulwarkFacing\(Hero hero\).*?_bulwarkFacingFrame == Time\.frameCount.*?SoulProgressionRuntime\.GetSummonBehavior\(\).*?SummonBehavior\.Bulwark.*?Raycaster.*?GetViewRay.*?viewForward\.sqrMagnitude.*?movementForward = hero\.HorizontalVelocity.*?!_hasBulwarkForward.*?_bulwarkForward = movementForward\.normalized.*?if \(!_hasBulwarkForward\).*?_bulwarkForward = viewForward.*?Vector3\.Angle\(_bulwarkForward, viewForward\).*?BulwarkCameraFacingMinimumAngle.*?_bulwarkFacingCooldownUntil.*?Vector3\.Angle\(_bulwarkViewCandidate, viewForward\).*?BulwarkCameraFacingStabilityAngle.*?BulwarkCameraFacingHoldSeconds.*?_bulwarkForward = viewForward.*?BulwarkCameraFacingCooldownSeconds') {
    throw "Bulwark facing does not latch stable camera intent with movement-only fallback and turn cooldown."
}
if ($runtimeSource -notmatch '(?s)bool BeforeFindTarget\(.*?return !RefreshAutonomousTargets\(summon, plugin, behavior\);.*?return true;.*?bool RefreshAutonomousTargets\(.*?owner == null \|\| hero == null \|\| grid == null.*?return false;') {
    throw "Owned servants do not fall back to native targeting only when custom prerequisites are unavailable."
}
if (($runtimeSource -notmatch '(?s)GetAutonomousTargetPriority\(\s*NpcElement target,\s*NpcElement owner,.*?bool retainBulwarkTarget\).*?behavior == SummonBehavior\.Bulwark.*?advanceHeld = IsBulwarkAdvanceHeld\(hero\).*?BulwarkAdvanceCombatLeash.*?retainBulwarkTarget\s*\? BulwarkAdvanceRetentionRange\s*:\s*BulwarkAdvanceBreachRange.*?target\.Coords - owner\.Coords.*?return recentAttacker\s*\? 0\s*:\s*targetingProtected \? 1 : 2;.*?retainBulwarkTarget\s*\? BulwarkCloseGuardCombatLeash\s*:\s*BulwarkCloseGuardDefenseRange.*?owner\.Coords - hero\.Coords.*?recentAttacker.*?return 0;.*?if \(targetingProtected\).*?return 1;.*?retainBulwarkTarget\s*\? BulwarkCloseGuardLocalRetentionRange\s*:\s*BulwarkCloseGuardLocalEngageRange.*?target\.Coords - owner\.Coords.*?\? 2\s*:\s*int\.MaxValue.*?if \(recentAttacker\).*?return 0;.*?if \(targetingProtected\).*?return 1;.*?target\.NpcAI\.InCombat.*?GuardMeleeThreatRange.*?return 2;.*?return int\.MaxValue;') -or
    ($runtimeSource -notmatch '(?s)awarenessRange = behavior == SummonBehavior\.Bulwark\s*\? BulwarkTargetCandidateRange.*?GetAutonomousTargetPriority\(\s*target,\s*owner,\s*behavior,\s*hero,\s*hostInCombat,\s*retainBulwarkTarget: behavior == SummonBehavior\.Bulwark\s*&& ReferenceEquals\(target, committedTarget\)\)')) {
    throw "Guard and both Bulwark stances do not preserve their bounded defensive priorities."
}
if ($runtimeSource -notmatch '(?s)UpdateCatchUpSpeed\(.*?GetSummonBehavior\(\).*?SummonBehavior\.Bulwark.*?!HeldSummons\.ContainsKey\(id\).*?!PendingRecallPlacements\.ContainsKey\(id\).*?!HasActivePriorityTarget\(summon\).*?advanceHeld = IsBulwarkAdvanceHeld\(hero\).*?GetBulwarkAnchor\(summon\).*?BulwarkAdvanceCatchUpStopDistance.*?BulwarkCatchUpStopDistance.*?BulwarkAdvanceCatchUpStartDistance.*?BulwarkCatchUpStartDistance.*?BulwarkAdvanceCatchUpMinimumMultiplier.*?BulwarkAdvanceMaximumMovementMultiplier.*?existingMovement.*?else if \(!npc\.NpcAI\.InCombat\)') {
    throw "Bulwark servants do not use stance-aware hysteretic catch-up without affecting combat pursuit."
}
if ($runtimeSource -notmatch '(?s)class CatchUpSpeedState.*?Multiplier.*?MovementTweak.*?UpdateCatchUpSpeed\(.*?Math\.Abs\(current\.Multiplier - multiplier\).*?new CatchUpSpeedState.*?Multiplier = multiplier.*?MovementTweak = tweak.*?UpdateEmpoweredPresentation\(.*?SpeedTweaks\.TryGetValue\(id, out catchUpSpeed\).*?playback \*= catchUpSpeed\.Multiplier') {
    throw "Bulwark Advance catch-up does not update safely or keep locomotion playback synchronized."
}
if ($runtimeSource -notmatch '(?s)IsSprintActionHeld\(Hero hero\).*?KeyBindings\.Gameplay\.Sprint.*?IsBulwarkAdvanceHeld\(Hero hero\).*?SummonBehavior\.Bulwark.*?IsSprintActionHeld\(hero\).*?IsTargetCommandModifierHeld\(.*?TargetCommandModifierMode\.None.*?return true;.*?return IsSprintActionHeld\(hero\);') {
    throw "Bulwark stance does not read the actual remappable Sprint action independently of the optional target-command modifier."
}
if ($runtimeSource -match 'return hostInCombat \? 3 : int\.MaxValue') {
    throw "Guard still falls back to pulling any visible faction-hostile once the host enters combat."
}
if ($runtimeSource.Contains('|| target.IsSummonOrAlly')) {
    throw "Faction-hostile summoned enemies are still excluded from autonomous targeting."
}
if ($runtimeSource -notmatch '(?s)TrySetPassiveSharedTarget\(.*?HasExplicitCommandTarget\(summon\).*?AwarenessTargets\[summonId\].*?SetAutonomousTargetOverride\(summon, target\).*?AfterFindTarget\(.*?TrySetPassiveSharedTarget\(summon, target\)') {
    throw "Passive crosshair sharing is not represented as replaceable autonomous target state."
}
if ($runtimeSource -notmatch '(?s)committedRecord != null.*?AutonomousTargetMinimumCommitmentSeconds.*?AutonomousTargetSwitchDistanceRatio.*?SelectedAt = now') {
    throw "Autonomous targets do not have bounded commitment and same-priority switch hysteresis."
}
if ($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSightFrom\(.*?string cacheKey = observer == null.*?AutonomousLineOfSightByTarget\.TryGetValue\(cacheKey.*?cached\.ExpiresAt >= Time\.unscaledTime.*?return cached\.Visible;.*?Physics\.RaycastNonAlloc.*?AutonomousLineOfSightByTarget\[cacheKey\].*?AutonomousLineOfSightCacheSeconds') {
    throw "Autonomous line-of-sight checks are not shared briefly across the host."
}
if ($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSight\(\s*NpcHeroSummon summon,\s*Hero hero,\s*NpcElement target,\s*SummonBehavior behavior\).*?summonObserver.*?behavior == SummonBehavior\.Hunt.*?HasAutonomousTargetLineOfSightFrom\(\s*null,\s*hero,\s*target\)\s*\|\|.*?HasAutonomousTargetLineOfSightFrom\(\s*summonObserver') {
    throw "Hunt sight is not shared by the hero and acting servant."
}
if ($runtimeSource -notmatch '(?s)!AwarenessTargets\.TryGetValue\(summonId, out selectedRecord\).*?!ReferenceEquals\(selectedRecord\.Target, selectedTarget\).*?!owner\.ForceAddCombatTarget\(.*?ClearAutonomousTargetOverride\(summon\);\s*return true;.*?AwarenessTargets\[summonId\].*?SetAutonomousTargetOverride\(summon, selectedTarget\)') {
    throw "Autonomous overrides can still bypass a native combat-target rejection."
}
if ($runtimeSource -notmatch '(?s)CycleSummonBehavior\(\).*?TryCycleSummonBehavior.*?foreach \(NpcHeroSummon summon.*?RefreshAutonomousTargets\(\s*summon,\s*plugin,\s*behavior\);.*?EnforceSummonBehavior\(summon, behavior\);') {
    throw "A completed behavior change does not refresh targets before enforcing its new formation."
}
if ($runtimeSource -match '(?s)CycleSummonBehavior\(\).*?TryCycleSummonBehavior.*?RemoveAllAwarenessTargets\(\).*?PublishCommand') {
    throw "Behavior changes still discard the entire host's valid target state before refreshing."
}
if ($runtimeSource -notmatch '(?s)SetExplicitCommandTarget\(.*?RemoveAwarenessTargetsForSummon\(summon, target\);.*?!ReferenceEquals\(previousTarget, target\).*?RemoveCombatTarget\(previousTarget\).*?ExplicitCommandTargets\[summonId\] = target.*?AddSummonTargetOverrideElement\(\s*summon,\s*target,\s*10\).*?NpcAI\.InCombat.*?ForceAddCombatTarget\(\s*target,\s*recalculateTarget: true\).*?NpcAI\.EnterCombatWith\(\s*target,\s*forceChange: true\).*?CommandSummons\(.*?SetExplicitCommandTarget\(summon, target\)') {
    throw "Explicit Attack and Swarm commands do not replace stale ownership and enter combat with their ordered target."
}
if ($runtimeSource -notmatch '(?s)HasExplicitCommandTarget\(NpcHeroSummon summon\).*?ExplicitCommandTargets\.TryGetValue.*?command == null.*?AddSummonTargetOverrideElement\(\s*summon,\s*target,\s*10\).*?GetCurrentTarget\(\).*?EnterCombatWith\(.*?ForceAddCombatTarget\(') {
    throw "Explicit Attack and Swarm orders are not reasserted when native AI drops their override or combat target."
}
if ($runtimeSource -notmatch '(?s)BeforeStayCloseToAlly\(.*?hasExplicitCommandTarget = HasExplicitCommandTarget\(summon\).*?!hasExplicitCommandTarget\s*&& HeldSummons\.TryGetValue.*?!hasExplicitCommandTarget\s*&& TryGetRecallAnchor.*?hasPriorityTarget = hasExplicitCommandTarget\s*\|\| HasActivePriorityTarget\(summon\)') {
    throw "A saved Hold anchor can still override an active explicit Attack or Swarm order."
}
if ($runtimeSource -notmatch '(?s)BeforeStayCloseToAlly\(.*?hasPriorityTarget.*?FormationPatrolAnchors\.Remove.*?patrol\.UpdateRadius\(NativePatrolRadius\);\s*return false;.*?HasActivePriorityTarget\(.*?HasExplicitCommandTarget\(summon\).*?AutonomousTargetOverrides\.TryGetValue') {
    throw "Formation following can still override an explicit or autonomous combat target."
}
if ($runtimeSource -notmatch '(?s)HasActivePriorityTarget\(NpcHeroSummon summon\).*?AutonomousTargetOverrides\.TryGetValue.*?targetOverride == null.*?AddSummonTargetOverrideElement\(\s*summon,\s*target,\s*5\).*?GetCurrentTarget\(\).*?EnterCombatWith\(.*?ForceAddCombatTarget\(.*?return true;') {
    throw "A valid autonomous Hunt target is not reasserted when native AI drops its override or combat target."
}
if (($runtimeSource -notmatch '(?s)awarenessRange = behavior == SummonBehavior\.Bulwark.*?behavior == SummonBehavior\.Hunt\s*\? HuntAwarenessRange') -or
    ($runtimeSource -notmatch '(?s)GetAutonomousTargetPriority\(\s*NpcElement target,.*?if \(behavior == SummonBehavior\.Hunt\).*?return recentAttacker\s*\? 0\s*:\s*targetingProtected \? 1 : 2;')) {
    throw "Hunt does not use its expanded range and attacker-first aggressive priority."
}
if ($runtimeSource -notmatch '(?s)HasExplicitCommandTarget\(summon\).*?RemoveAwarenessTargetsForSummon\(\s*summon,\s*ExplicitCommandTargets\[summonId\]\);') {
    throw "Autonomous refresh can still remove the live explicit command target."
}
if ($runtimeSource -notmatch '(?s)bool invalid = record\.Target == null.*?GetAutonomousTargetPriority\(\s*record\.Target,\s*owner,\s*behavior,\s*hero,\s*hostInCombat,\s*retainBulwarkTarget: true\) == int\.MaxValue') {
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
if ($runtimeSource -notmatch '(?s)RecallHost\(Hero hero\).*?RecallTargetSuppressionUntil\[summonId\].*?RecallTargetSuppressionSeconds.*?ExplicitCommandTargets\.Remove.*?RemoveAwarenessTargetsForSummon.*?SetSummonHeld\(summon, false\).*?ForceEndCombat\(\).*?TryFindRecallPlacement\(.*?PendingRecallPlacements\[summonId\].*?TeleportToAllyMethod\.Invoke.*?recalled == 1 \? "Recall" : "Recall Host".*?PublishCommand\(\s*plugin,\s*SummonCommandState\.Follow,\s*SummonRecallCommandId,') {
    throw "Recall does not clear combat state, assign safe placements, use native teleporting, select singular or host feedback, and request its dedicated voice."
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
if ($runtimeSource -notmatch '(?s)GetFormationHost\(Hero hero\).*?_formationHostCacheExpiresAt.*?World\.All<NpcHeroSummon>\(\).*?OrderBy.*?FormationHostFallbackRefreshSeconds.*?InvalidateFormationHostCache\(\).*?GetHuntAnchor\(.*?GetFormationHost\(hero\).*?GetBulwarkAnchor\(.*?GetFormationHost\(hero\).*?GetGuardAnchor\(.*?GetFormationHost\(hero\)') {
    throw "Guard, Bulwark, and Hunt do not share the lifecycle-invalidated ordered formation host."
}
if ($runtimeSource -notmatch '(?s)EnsureFormationFacingHero\(Hero hero\).*?ReferenceEquals\(_formationFacingHero, hero\).*?return;.*?_formationFacingHero = hero.*?ResetBulwarkFacingState\(\).*?_hasGuardForward = false') {
    throw "Guard and Bulwark facing caches are not reset when the active hero changes."
}
if ($runtimeSource -notmatch '(?s)IsHostInCombat\(Hero hero\).*?Time\.frameCount.*?_hostCombatCacheFrame == frame.*?_hostCombatCacheValue = GetFormationHost\(hero\)\.Any') {
    throw "Group combat state is not cached once per frame across servant decisions."
}
if ($runtimeSource -notmatch '(?s)AwarenessTargets\.TryGetValue\(summonId, out record\).*?AwarenessTargets\.Remove\(summonId\).*?AwarenessTargets\[summonId\] = new AwarenessTargetRecord') {
    throw "Autonomous awareness is not maintained as one direct record per summon."
}
if ($runtimeSource -notmatch '(?s)GetGuardIdleAnchor\(.*?IsFormationLeaderMoving\(hero\).*?IsHostInCombat\(hero\).*?if \(hostInCombat\).*?CancelGuardIdleMovement.*?if \(heroMoving\).*?state\.FormationAnchor = liveAnchor.*?GuardAnchorRebaseDistance.*?if \(hostInCombat\)\s*\{\s*return state\.FormationAnchor;.*?state\.Wandering.*?state\.Returning.*?_guardIdleMoverId') {
    throw "Stationary Guard anchors are not latched with bounded wander and return states."
}
if (($runtimeSource -notmatch '(?s)FormationLeaderTravelDeadZone = 1\.5f.*?FormationLeaderMovementStartSeconds = 0\.45f.*?FormationLeaderSettleSeconds = 0\.35f.*?UpdateFormationLeaderMotion\(Hero hero\).*?Time\.frameCount.*?traveledBeyondDeadZone.*?sustainedMovement.*?_formationLeaderMoving = true') -or
    ($runtimeSource -notmatch '(?s)GetHuntAnchor\(.*?GetFormationLeaderAnchor\(hero\).*?GetBulwarkAnchor\(.*?leaderAnchor = advanceHeld\s*\? hero\.Coords\s*:\s*GetFormationLeaderAnchor\(hero\).*?GetGuardAnchor\(.*?GetGuardFormationForward\(hero\).*?GetFormationLeaderAnchor\(hero\).*?GetGuardFormationForward\(Hero hero\).*?IsFormationLeaderMoving\(hero\)')) {
    throw "Guard, Hunt, and released Bulwark do not share the lazier leader-movement gate while Advance bypasses it."
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
if ($runtimeSource -notmatch '(?s)GetHuntIdleAnchor\(.*?IsFormationLeaderMoving\(hero\).*?IsHostInCombat\(hero\).*?HuntAnchorRebaseDistance.*?HuntIdleMoverIds\.Count < maximumWanderers.*?TryFindIdleDestination\(.*?HuntIdleMinimumHeroDistance.*?ScheduleNextHuntIdleWander') {
    throw "Hunt perimeter anchors do not provide bounded, combat-aware scouting movement."
}
if ($runtimeSource -notmatch '(?s)GetHuntAnchor\(.*?HuntFormationSlotsPerRing.*?90\.0f \+ \(\(360\.0f \* slot\) / countInRing\).*?HuntFormationInnerRadius.*?HuntFormationRingSpacing') {
    throw "Hunt does not distribute servants across stable full-perimeter rings."
}
if ($runtimeSource -notmatch '(?s)UpdateFormationPatrolPlace\(.*?float updateDistance.*?FormationPatrolAnchors\.TryGetValue.*?updateDistance \* updateDistance.*?patrol\.UpdatePlace\(anchor\)') {
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
if ($runtimeSource -notmatch '(?s)AfterNpcControllerUpdate\(NpcController __instance\).*?NextControllerRefreshBySummon\.TryGetValue.*?ControllerRefreshSeconds.*?ApplyIdleVolume.*?UpdateAnimationWatchdog.*?UpdateCatchUpSpeed.*?UpdateBehaviorSpeed.*?UpdateEmpoweredPresentation') {
    throw "Per-controller summon presentation work is not bounded to its refresh cadence."
}
if (($runtimeSource -notmatch '(?s)RequireMethod\(typeof\(NpcAlly\), "UnityUpdate"\).*?BeforeNpcAllyUnityUpdate.*?TranspileAiTick') -or
    ($runtimeSource -notmatch '(?s)AfterSummonInit\(NpcHeroSummon __instance\).*?SpawnReadinessBySummon\[summonId\].*?EarliestReleaseAt = Time\.unscaledTime.*?GetSpawnRecoverySeconds\(\)') -or
    ($runtimeSource -notmatch '(?s)BeforeNpcAllyUnityUpdate\(NpcAlly __instance\).*?SpawnReadinessBySummon\.Count == 0.*?now < readiness\.EarliestReleaseAt.*?HasPlayingGeneralAnimation.*?MovementPreventedField\.SetValue\(__instance, true\).*?MovementPreventedField\.SetValue\(__instance, false\).*?SpawnReadinessBySummon\.Remove\(summonId\)')) {
    throw 'New summons are not held through both the configured minimum and native animation readiness.'
}
if (($runtimeSource -notmatch '(?s)UpdateAnimationWatchdog\(.*?IsOwnedSummon\(summon, hero\).*?SpawnReadinessBySummon\.ContainsKey\(id\).*?IsMovingForAnimationWatchdog\(controller, npc, state\).*?HasPlayingGeneralAnimation\(npc, out generalFsm\).*?idleWhileMoving.*?NpcStateType\.Idle.*?movementStateStalled.*?NpcStateType\.Movement.*?animationState\.TimeD.*?FailedMovingSamples\+\+.*?AnimationWatchdogFailureSamples.*?npc\.SetAnimatorState\(\s*NpcFSMType\.GeneralFSM,\s*NpcStateType\.Movement.*?generalFsm\.EnableFSM\(\).*?Recovered summon locomotion.*?AnimationWatchdogRecoveryCooldownSeconds') -or
    ($runtimeSource -notmatch '(?s)IsMovingForAnimationWatchdog\(.*?controller\.RichAI\.canMove.*?npc\.Coords.*?LastPosition.*?movingByDisplacement.*?controller\.RichAI\.velocity.*?AnimationWatchdogMovementSpeed') -or
    ($runtimeSource -notmatch '(?s)HasPlayingGeneralAnimation\(.*?TryGetElement<NpcGeneralFSM>\(\).*?CurrentAnimatorState\.CurrentState.*?state\.IsValid.*?state\.IsPlaying') -or
    ($runtimeSource -notmatch '(?s)ResetAnimationWatchdog\(AnimationWatchdogState state\).*?FailedMovingSamples = 0.*?LastAnimationState = null.*?LastAnimationTime = 0\.0')) {
    throw 'Owned moving summons do not use the bounded native animation FSM watchdog.'
}
if (($runtimeSource -notmatch '(?s)AfterSummonDiscard\(.*?SpawnReadinessBySummon\.Remove\(id\).*?AnimationWatchdogsBySummon\.Remove\(id\)') -or
    ($runtimeSource -notmatch '(?s)Shutdown\(\).*?ReleaseAllSpawnReadinessLocks\(\).*?AnimationWatchdogsBySummon\.Clear\(\)')) {
    throw 'Summon animation readiness and watchdog state is not cleaned up with runtime ownership.'
}
if (($runtimeSource -notmatch '(?s)HuntBehaviorMovementMultiplier = 1\.10f.*?UpdateBehaviorSpeed\(.*?GetNecromanticPower\(\).*?>= SoulProgressionRuntime\.BehaviorCommandPower.*?GetSummonBehavior\(\).*?SummonBehavior\.Hunt.*?pursuing') -or
    ($runtimeSource -notmatch '(?s)BeginSwarm\(.*?behaviorMovement.*?SummonBehavior\.Hunt.*?HuntBehaviorMovementMultiplier.*?MaximumCommandMovementMultiplier\s*/ \(empowermentMovement \* behaviorMovement\)') -or
    ($runtimeSource -notmatch '(?s)UpdateBehaviorSpeed\(.*?otherCommandMovement.*?empowerment\.MovementMultiplier.*?swarm\.MovementMultiplier.*?MaximumCommandMovementMultiplier / otherCommandMovement.*?BehaviorSpeedStates\[id\] = state') -or
    ($runtimeSource -notmatch '(?s)UpdateEmpoweredPresentation\(.*?BehaviorSpeedStates\.TryGetValue.*?playback \*= behaviorSpeed\.Multiplier') -or
    ($runtimeSource -notmatch '(?s)AfterSummonDiscard\(.*?RemoveBehaviorSpeedState\(id\).*?RemoveBehaviorSpeedState\(.*?DiscardTweak\(state\.MovementTweak\).*?BehaviorSpeedStates\.Remove\(id\)')) {
    throw "Hunt pursuit speed is not transient, capped with Swarm and Empower, or cleaned up safely."
}
if ($runtimeSource -notmatch '(?s)RemoveBehaviorSpeedState\(string id\).*?string\.IsNullOrEmpty\(id\).*?return;.*?BehaviorSpeedStates\.TryGetValue\(id, out state\)') {
    throw "Behavior speed cleanup does not reject null or empty IDs before dictionary lookup."
}

foreach ($required in @(
    '45 m',
    'returns its investment but creates',
    'Version under test: 2.5.2',
    'SAS-SMOKE-30',
    'SAS-SMOKE-31',
    'SAS-SMOKE-16',
    'SAS-SMOKE-25',
    'SAS-SMOKE-32',
    'SAS-SMOKE-33',
    'SAS-SMOKE-43')) {
    if (!$readme.Contains($required) -and !$matrix.Contains($required)) {
        throw "Summon command documentation is missing: $required"
    }
}

Write-Host "Soul and Service idle, targeting, and Attack command contracts passed."
