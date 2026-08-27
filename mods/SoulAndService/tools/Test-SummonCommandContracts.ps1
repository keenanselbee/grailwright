$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulAndService.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SummonRuntime.cs") -Raw
$salvageSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$formationSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SummonFormationCoordinator.cs") -Raw
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
if (!$manifest.Contains('src/SummonFormationCoordinator.cs')) {
    throw 'Soul and Service does not compile the host formation coordinator.'
}

foreach ($required in @(
    'ConfigSchemaVersion = 23',
    'public enum SummonBehavior',
    'Guard = 0',
    'Bulwark = 1',
    'Hunt = 2',
    'PlayerAttackPassThroughMode.CombatOnly',
    'internal ConfigEntry<bool> AttackCommandPrompt',
    '"AttackCommandPrompt"',
    'internal ConfigEntry<bool> FormationCommands',
    '"FormationCommands"',
    'internal ConfigEntry<bool> HoldIndividualFormationCommands',
    '"HoldIndividualFormationCommands"',
    '"Hold Individual Formation Commands"',
    'internal ConfigEntry<bool> DirectedHuntEnabled',
    '"EnableDirectedHunt"',
    'internal ConfigEntry<bool> ShowDirectedHuntPreview',
    '"ShowDirectedHuntPreview"',
    'internal ConfigEntry<bool> BulwarkAdvanceEnabled',
    '"EnableBulwarkAdvance"',
    '"Enable Bulwark Advance"',
    'internal ConfigEntry<float> BulwarkAdvanceReleaseSeconds',
    '"BulwarkAdvanceReleaseSeconds"',
    '"Bulwark Advance Release Duration"',
    'internal ConfigEntry<float> BulwarkAdvanceSpeedMultiplier',
    '"BulwarkAdvanceSpeedMultiplier"',
    '"Bulwark Advance Speed Multiplier"',
    'internal ConfigEntry<float> GuardFormationDistance',
    'internal ConfigEntry<float> GuardEngagementRange',
    '"GuardEngagementRange"',
    'internal ConfigEntry<float> HuntFormationDistance',
    'internal ConfigEntry<float> BulwarkCloseGuardDistance',
    'internal ConfigEntry<float> BulwarkAdvanceDistance',
    'internal ConfigEntry<float> BulwarkLocalEngagementRange',
    'internal ConfigEntry<float> BulwarkTargetRetentionRange',
    'internal ConfigEntry<float> BulwarkPlayerLeash',
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

if ($pluginSource -notmatch '(?s)TeleportDistance = BindOrdered\(.*?"TeleportDistance",\s*60\.0f,.*?AcceptableValueRange<float>\(10\.0f, 100\.0f\)') {
    throw 'Automatic summon teleport does not default to 60 meters within its supported range.'
}

if (($runtimeSource -notmatch '(?s)GetGuardAnchor\(.*?formationForward = GetGuardFormationForward\(hero\).*?SummonFormationCoordinator\.GetRadialAnchor\(.*?FormationPurpose\.Guard.*?formationForward') -or
    ($runtimeSource -notmatch '(?s)GetGuardFormationForward\(Hero hero\).*?UpdateGuardFormationFacing\(hero\).*?return _guardForward') -or
    ($runtimeSource -notmatch '(?s)UpdateGuardFormationFacing\(Hero hero\).*?Vector3\.Angle\(_guardFacingCandidate, candidate\).*?FormationFacingCommitDistance.*?FormationFacingCommitSeconds.*?FormationFacingMaximumTurnDegreesPerSecond.*?Vector3\.RotateTowards\(.*?_guardFacingDesired')) {
    throw "Guard formation facing is not committed and rotated gradually from meaningful hero movement."
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
if ($pluginSource -notmatch '(?s)HoldIndividualFormationCommands = BindOrdered\(.*?"Targeting".*?"HoldIndividualFormationCommands".*?false') {
    throw 'Individual formation command hold must remain opt-in.'
}
if (($runtimeSource -notmatch 'IndividualFormationCommandHoldSeconds = 0\.45f') -or
    ($runtimeSource -notmatch '(?s)TryHandleIndividualFormationCommandInput\(.*?UIKeyDownAction.*?CanStartIndividualFormationCommandHold\(\).*?_individualFormationCommandSummon = _commandInteractable\.Summon.*?_individualFormationCommandState = _commandInteractable\.Kind') -or
    ($runtimeSource -notmatch '(?s)TryCompleteIndividualFormationCommandHold\(\).*?_individualFormationCommandResolved = true.*?CanMaintainIndividualFormationCommandHold\(\).*?CommandIndividualFormation\(') -or
    ($runtimeSource -notmatch '(?s)UIKeyUpAction.*?TryCompleteIndividualFormationCommandHold\(\).*?ResetIndividualFormationCommandHold\(\)') -or
    ($runtimeSource -notmatch '(?s)StartInteraction\(.*?HoldIndividualFormationCommands\.Value.*?return false;.*?CanIssueFormationCommand')) {
    throw 'Optional individual Hold/Follow hold input is not latched, cancellable, and single-fire.'
}

foreach ($required in @(
    'NativePatrolRadius = 7.5f',
    'NativeSummonTargetAcquisitionRange = 44.0f',
    'NativeSummonTargetRetentionRange = 44.75f',
    'BaseHuntAwarenessRange = 30.0f',
    'HuntPointMinimumDistance = 5.0f',
    'HuntAttackMoveSearchSeconds = 4.0f',
    'HuntAttackMoveMaximumTravelSeconds = 30.0f',
    'HuntAttackMoveSweepRadius = 3.0f',
    'HuntAttackMoveMaximumSweepLegs = 2',
    'ExplicitCommandMaximumUnreachableSamples = 3',
    'HuntAttackMoveBlockedSeconds = 0.75f',
    'AttackCommandAimRadius = 0.25f',
    'AttackCommandFocusGraceSeconds = 0.30f',
    'patrol.UpdateRadius(0.0f)',
    'patrol.UpdatePlace(summon.ParentModel.Coords)',
    'OwnedTargetOverrides',
    'ClearAllOwnedTargetOverrides()',
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
    'SummonRaiseAllCommandId = "summon_raiseall"',
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
    'DisplayName => _displayName ?? string.Empty',
    'SetOwnedTargetOverride(summon, target, 10, true)',
    'BehaviorCommandHoldSeconds = 0.45f',
    'RecallCommandHoldSeconds = 1.5f',
    'RaiseAllRadius = 30.0f',
    'SummonCommandState.RaiseAll',
    'StandardCommandFeedbackSeconds = 0.675f',
    'ExtendedCommandFeedbackSeconds = 1.35f',
    'SummonCommandState.Behavior',
    'KeyBindings.Gameplay.Interact',
    'GetAvailableActions().Any()',
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
    'DefaultGuardEngagementRange = 15.0f',
    'GuardEngagementRetentionPadding = 5.0f',
    'DefaultGuardFormationDistance = 4.5f',
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
    'RecallPlacementHeroMoveReleaseDistance = 2.0f',
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
    'DefaultBulwarkLocalEngagementRange = 4.0f',
    'DefaultBulwarkTargetRetentionRange = 6.0f',
    'DefaultBulwarkPlayerLeash = 8.0f',
    'BulwarkTargetMinimumCommitmentSeconds = 3.0f',
    'BulwarkTargetCandidateRange = 10.0f',
    'BulwarkAnchorTolerance = 0.75f',
    'BulwarkAdvanceAnchorTolerance = 1.25f',
    'BulwarkAdvanceResumeDistance = 1.75f',
    'BulwarkAdvanceRunDistance = 0.75f',
    'BulwarkAdvanceAnchorUpdateDistance = 0.15f',
    'BulwarkAdvanceProgressDistance = 0.10f',
    'BulwarkAdvanceBlockedSeconds = 0.50f',
    'BulwarkAdvanceFallbackSeconds = 1.0f',
    'BulwarkAdvanceFallbackProbeRadius = 0.75f',
    'BulwarkAdvanceFallbackMinimumOffset = 0.35f',
    'BulwarkAdvanceFallbackCandidateSnapDistance = 0.50f',
    'BulwarkAdvanceMaximumAnchorSnapDistance = 1.5f',
    'BulwarkCatchUpStartDistance = 2.0f',
    'BulwarkCatchUpStopDistance = 1.0f',
    'BulwarkAdvanceCatchUpStartDistance = 0.75f',
    'BulwarkAdvanceCatchUpStopDistance = 0.35f',
    'DefaultBulwarkCloseGuardDistance = 3.5f',
    'DefaultBulwarkAdvanceDistance = 4.5f',
    'BulwarkFormationSlotsPerRing = 4',
    'BulwarkCloseGuardSlotsPerRing = 5',
    'BulwarkAdvancePredictionSeconds = 0.40f',
    'BulwarkAdvanceCatchUpMinimumMultiplier = 1.60f',
    'BulwarkAdvanceMaximumMovementMultiplier = 3.0f',
    'EnsureFormationFacingHero(hero)',
    'DefaultHuntFormationDistance = 5.5f',
    'HuntFormationSlotsPerRing = 6',
    'HuntAnchorTolerance = 1.25f',
    'HuntAnchorRebaseDistance = 2.5f',
    'HuntIdleMinimumHeroDistance = 5.0f',
    'HuntMultipleWandererHostSize = 4',
    'HuntMaximumConcurrentWanderers = 2',
    'UpdateHuntAttackMove(plugin)',
    'TryResolveHuntPointPreview(',
    'TryGetHuntAttackMoveAnchor(',
    'ResetHuntAttackMove()',
    'GuardIdleMasterMinimumStillSeconds = 16.0f',
    'GuardIdleMasterMaximumStillSeconds = 26.0f',
    'HasAutonomousTargetLineOfSight(',
    'SetAutonomousTargetOverride(',
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
    'GetEmpowermentCombatMultiplier(string summonId)',
    'Mathf.Clamp(multiplier, 1.20f, 1.50f)',
    'Mathf.Lerp(',
    'Mathf.InverseLerp(',
    'state.CombatMultiplier - 0.10f',
    '1.10f,',
    '1.30f,',
    '1.40f));',
    'NpcHeroSummon summon = __instance.Npc',
    'UpdateEmpoweredPresentation(__instance, summonId);',
    'GetSummonId(receiver)',
    'GetSummonId(dealer)',
    ': empowerment.MovementMultiplier;',
    'BeforeRootMotionUpdateAnimator(',
    'plugin.RestBehavior.Value == RestHostBehavior.Sustain')) {
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
if (($runtimeSource -notmatch '(?s)class EmpowermentState.*?CombatMultiplier.*?SizeMultiplier.*?MovementMultiplier') -or
    ($runtimeSource -notmatch '(?s)TryEmpowerSummon\(.*?CombatMultiplier = Mathf\.Clamp\(multiplier, 1\.20f, 1\.50f\).*?SizeMultiplier = Mathf\.Lerp\(\s*1\.10f,\s*1\.30f,\s*Mathf\.InverseLerp\(\s*1\.20f,\s*1\.50f,\s*state\.CombatMultiplier\)\).*?MovementMultiplier = Mathf\.Sqrt\(Mathf\.Clamp\(\s*state\.CombatMultiplier - 0\.10f,\s*1\.10f,\s*1\.40f\)\)') -or
    ($runtimeSource -notmatch '(?s)ApplyEmpowermentVisual\(.*?soulforgedMultiplier.*?empowermentSize.*?Vector3\.Scale\(\s*state\.OriginalLocalScale,\s*Vector3\.one \* soulforgedMultiplier \* empowermentSize\)') -or
    ($runtimeSource -notmatch '(?s)AfterApplyDamageModifiers\(.*?receiverEmpowerment\.CombatMultiplier.*?dealerEmpowerment\.CombatMultiplier')) {
    throw 'Empower does not keep its 1.2-1.5 combat and movement scaling while remapping visual size to 1.1-1.3.'
}

if ($pluginSource -notmatch '(?s)ShowDirectedHuntPreview = BindOrdered\(.*?false' -or
    $runtimeSource -notmatch '(?s)CanShowHuntPointPrompt\(.*?CanUseDirectedHunt\(plugin, hero\).*?ShowDirectedHuntPreview\.Value' -or
    $runtimeSource -notmatch '(?s)HandleBehaviorCommandInput\(.*?_commandInteractable == null.*?GetAvailableActions\(\)\.Any\(\).*?CanUseDirectedHunt\(.*?TryResolveHuntPointPreview\(.*?TryBeginBehaviorCommandHold\(\s*hero,\s*huntPoint,\s*huntDestination\)') {
    throw 'Directed Hunt input is not independent from its disabled-by-default preview.'
}

if (($runtimeSource -notmatch 'NativeSummonTargetAcquisitionRange = 44\.0f') -or
    ($runtimeSource -notmatch 'NativeSummonTargetRetentionRange = 44\.75f') -or
    ($runtimeSource -notmatch 'TargetRangeReleaseGraceSeconds = 0\.85f') -or
    ($runtimeSource -notmatch '(?s)SoulAndServiceTargetOverride.*?TemporarilyDisabled.*?IsWithinNativeSummonTargetRetentionRange') -or
    ($runtimeSource -notmatch '(?s)IsTargetWithinOwnedRangeGrace\(.*?OutOfRangeSince.*?TargetRangeReleaseGraceSeconds') -or
    ($runtimeSource -notmatch '(?s)RefreshAutonomousTargets\(.*?!directedHunt.*?!IsWithinNativeSummonTargetAcquisitionRange\(\s*hero,\s*target\)') -or
    ($runtimeSource -notmatch '(?s)HasExplicitCommandTarget\(.*?IsTargetWithinOwnedRangeGrace\(summon, target\).*?IsWithinNativeSummonTargetRetentionRange\(Hero\.Current, target\)')) {
    throw 'Autonomous and explicit targets do not use native-range acquisition, retention, and timed release hysteresis.'
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
if ($runtimeSource -notmatch '(?s)RemoveAwarenessTargetsForSummon\(\s*NpcHeroSummon summon,\s*NpcElement preservedTarget = null,\s*bool preserveOwnedOverride = false\).*?string summonId = \(\(Model\)summon\)\.ID;.*?AwarenessTargets\.TryGetValue\(summonId, out record\).*?!ReferenceEquals\(record\.Target, preservedTarget\).*?record\.Summon\.RemoveCombatTarget\(record\.Target\);.*?AwarenessTargets\.Remove\(summonId\);.*?if \(preserveOwnedOverride\).*?AutonomousTargetOverrides\.Remove\(summonId\).*?else.*?ClearAutonomousTargetOverride\(summon\)') {
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
if ($runtimeSource -notmatch '(?s)HandleBehaviorCommandInput\(.*?UIKeyUpAction && _behaviorCommandHeld.*?CompleteBehaviorCommandHold\(\).*?UIKeyDownAction.*?_commandInteractable\.IsFeedback.*?ClearCommandOverride\(\).*?TryBeginBehaviorCommandHold\(') {
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
if ($runtimeSource -notmatch '(?s)UIKeyUpAction.*?_formationCommandArmedForRelease.*?!_recallCommandAttemptedForHold.*?CommandAllFormation\(Hero\.Current\).*?UpdateTakeAllItemsHold\(\).*?FormationCommandHoldSeconds.*?RecallCommandHoldSeconds.*?RaiseAll\(Hero\.Current\).*?RecallHost\(Hero\.Current\)') {
    throw "Sprint + Take All does not cleanly separate release-based formation commands from 1.5-second Recall Host and Raise All commands."
}
if ($runtimeSource -notmatch '(?s)ShouldOwnTakeAllHoldForInterop\(\).*?!IsSprintActionHeld\(hero\).*?GetFormationHost\(hero\)\.Length > 0.*?HasGlobalFormationControl\(\).*?RaiseAllPower.*?HasEligibleRaiseAllCorpse' -or
    $runtimeSource -notmatch '(?s)CanMaintainTakeAllCommandHold\(\).*?!IsSprintActionHeld\(hero\).*?TakeAllCommandMode\.Formation.*?TakeAllCommandMode\.RaiseAll' -or
    $runtimeSource -notmatch '(?s)RaiseAll\(Hero hero\).*?SoulSalvageRuntime\.RaiseAll\(hero, RaiseAllRadius\).*?raised <= 0.*?SummonCommandState\.RaiseAll.*?SummonRaiseAllCommandId.*?"Raise All"') {
    throw "Raise All and the Sprint-gated Take All command modes are not latched, cancellable, and single-fire."
}
if ($salvageSource -notmatch '(?s)HasEligibleRaiseAllCorpse\(.*?_raiseAllEligibilityFrame == frame.*?_raiseAllEligibilityResult.*?World\.All<Location>\(\).*?TryValidateEligibleCorpse' -or
    $salvageSource -notmatch '(?s)RaiseAll\(Hero hero, float radius\).*?RaiseAllPower.*?World\.All<NpcHeroSummon>\(\).*?TryGetSummonCapacity.*?World\.All<Location>\(\).*?candidates\.Sort.*?remainingCapacity.*?GetReanimationSoulVigorCost\(.*?GetNecromanticPower\(\).*?ShowInsufficientSoulVigor.*?bindingAlreadyWon: true.*?summonLimitAlreadyChecked: true.*?hero\.Coords.*?hero\.Rotation' -or
    $salvageSource -notmatch '(?s)bindingManaCost = sourceItem == null\s*\? 0\.0f\s*:\s*GetHeavyCastManaCost\(sourceItem\)') {
    throw "Raise All does not reuse eligible-corpse validation, nearest-first ordering, current costs, capacity limits, and player-centered VFX."
}
if ($runtimeSource -notmatch '(?s)GetBulwarkVelocityScheme\(.*?bool advanceHeld.*?leader\.HorizontalVelocity\.magnitude.*?if \(advanceHeld\).*?BulwarkAdvanceRunDistance.*?VelocityScheme\.Run.*?BulwarkAdvanceAnchorTolerance.*?VelocityScheme\.Trot.*?VelocityScheme\.Walk.*?BulwarkLeaderRunSpeed.*?VelocityScheme\.Run.*?BulwarkLeaderMovingSpeed.*?VelocityScheme\.Trot.*?VelocityScheme\.Walk') {
    throw "Bulwark locomotion does not run for distant Advance recovery, trot on approach, and retain leader-aware Close Guard movement."
}
if ($runtimeSource -notmatch '(?s)GetBulwarkAnchor\(.*?EnsureFormationFacingHero\(hero\).*?advance = IsBulwarkAdvanceActive\(hero\).*?hero\.Coords \+ hero\.HorizontalVelocity.*?BulwarkAdvancePredictionSeconds.*?GetFormationLeaderAnchor\(hero\).*?UpdateBulwarkFacing\(hero\).*?formationForward = _bulwarkForward.*?formationForward = GetGuardFormationForward\(hero\).*?FormationPurpose\.BulwarkAdvance.*?FormationPurpose\.BulwarkCloseGuard.*?SummonFormationCoordinator\.GetRadialAnchor\(.*?plugin\.BulwarkAdvanceDistance\.Value.*?DefaultBulwarkAdvanceDistance.*?plugin\.BulwarkCloseGuardDistance\.Value.*?DefaultBulwarkCloseGuardDistance.*?BulwarkFormationSlotsPerRing.*?BulwarkCloseGuardSlotsPerRing.*?SummonFormationCoordinator\.ResolveAnchor') {
    throw "Bulwark does not switch between its configurable predictive forward wall and side/rear guard slots."
}
if (($formationSource -notmatch '(?s)class MemberState.*?Purpose.*?StableSlotId.*?NavigationRadius.*?DesiredAnchor.*?ResolvedAnchor.*?LastProgressPosition.*?LastProgressAt.*?FallbackUntil.*?HasAppliedAnchor.*?ArrivalEligible.*?Satisfied.*?Suspended') -or
    ([regex]::Matches($formationSource, 'private static readonly Dictionary<string, MemberState>').Count -ne 1)) {
    throw 'Formation ownership is not consolidated into one per-summon coordinator state.'
}
if (($formationSource -notmatch '(?s)Synchronize\(.*?NewMemberBuffer\.Sort\(LargestFirstComparer\.Instance\).*?FindNextStableSlotId\(\).*?ReadNavigationRadius\(summon\)') -or
    ($formationSource -notmatch '(?s)ReadNavigationRadius\(.*?summon\.ParentModel\.Radius\s*\* SummonRuntime\.GetEmpowermentSizeMultiplier\(summon\).*?MinimumNavigationRadius.*?MaximumNavigationRadius') -or
    ($formationSource -match '\.Where\(|\.OrderBy\(|\.ToArray\(')) {
    throw 'Formation membership is not stable, largest-first, size-aware, and allocation-conscious.'
}
if (($formationSource -notmatch '(?s)GetRadialAnchor\(.*?GetDenseSlotRank\(state, false\).*?GetRequiredSpacing\(state, false\)') -or
    ($formationSource -notmatch '(?s)TryReserveRecallPlacement\(.*?GetDenseSlotRank\(state, true\).*?GetRequiredSpacing\(state, true\)') -or
    ($formationSource -notmatch '(?s)GetDenseSlotRank\(.*?includeSuspended \|\| !other\.Suspended.*?other\.StableSlotId < member\.StableSlotId') -or
    ($formationSource -notmatch '(?s)GetRequiredSpacing\(.*?includeSuspended \|\| !other\.Suspended')) {
    throw 'Formation slots are not densely ranked or participation-aware after host changes.'
}
if (($formationSource -notmatch '(?s)ResolveAnchor\(.*?RecordProgress\(.*?blockedSeconds.*?TryConsumeRecoveryProbe\(\).*?TryResolveFallbackAnchor\(.*?fallbackSeconds') -or
    ($formationSource -notmatch 'MaximumRecoveryProbeHostsPerFrame = 2') -or
    ($formationSource -notmatch '(?s)TryResolveFallbackAnchor\(.*?RecoveryCandidateBuffer.*?PathUtilities\.IsPathPossible.*?IsReservedOrOccupied')) {
    throw 'Formation recovery is not progress-gated, reservation-aware, and limited to two probe batches per frame.'
}
if (($formationSource -notmatch '(?s)settleDistance = Math\.Max\(\s*anchorTolerance,\s*Math\.Max\(ProgressDistance, state\.NavigationRadius\)\)') -or
    ($formationSource -notmatch '(?s)directionalProgress =.*?Vector3\.Dot\(\s*movement,\s*previousDirection\.normalized\).*?bool progressed = directionalProgress >= ProgressDistance\s*\|\| state\.LastDistance - distance >= ProgressDistance')) {
    throw 'Large or empowered servants can still chase an unreachable exact center or count orbiting as formation progress.'
}
if (($formationSource -notmatch '(?s)resolvedFallback = TryResolveFallbackAnchor\(.*?ArrivalEligible = resolvedFallback') -or
    ($formationSource -notmatch '(?s)IsAtResolvedAnchor\(\s*NpcHeroSummon summon,\s*FormationPurpose purpose,.*?state\.Purpose == purpose.*?state\.ArrivalEligible')) {
    throw 'An unresolved stay-in-place fallback can still count as a real formation arrival.'
}
if (($runtimeSource -notmatch '(?s)GetBulwarkAnchor\(.*?FormationPurpose\.BulwarkAdvance.*?FormationPurpose\.BulwarkCloseGuard.*?SummonFormationCoordinator\.ResolveAnchor') -or
    ($runtimeSource -notmatch '(?s)UpdateFormationPatrolPlace\(.*?SummonFormationCoordinator\.ShouldApplyPatrolAnchor')) {
    throw 'Bulwark and patrol destination updates are not routed through the shared coordinator.'
}
if ($runtimeSource -notmatch '(?s)UpdateBulwarkFacing\(Hero hero\).*?_bulwarkFacingFrame == Time\.frameCount.*?SoulProgressionRuntime\.GetSummonBehavior\(\).*?SummonBehavior\.Bulwark.*?Raycaster.*?GetViewRay.*?viewForward\.sqrMagnitude.*?movementForward = hero\.HorizontalVelocity.*?!_hasBulwarkForward.*?_bulwarkForward = movementForward\.normalized.*?if \(!_hasBulwarkForward\).*?_bulwarkForward = viewForward.*?Vector3\.Angle\(_bulwarkForward, viewForward\).*?BulwarkCameraFacingMinimumAngle.*?_bulwarkFacingCooldownUntil.*?Vector3\.Angle\(_bulwarkViewCandidate, viewForward\).*?BulwarkCameraFacingStabilityAngle.*?BulwarkCameraFacingHoldSeconds.*?_bulwarkForward = viewForward.*?BulwarkCameraFacingCooldownSeconds') {
    throw "Bulwark facing does not latch stable camera intent with movement-only fallback and turn cooldown."
}
if ($runtimeSource -notmatch '(?s)bool BeforeFindTarget\(.*?return !RefreshAutonomousTargets\(summon, plugin, behavior\);.*?return true;.*?bool RefreshAutonomousTargets\(.*?owner == null \|\| hero == null \|\| grid == null.*?return false;') {
    throw "Owned servants do not fall back to native targeting only when custom prerequisites are unavailable."
}
if (($runtimeSource -notmatch '(?s)GetAutonomousTargetPriority\(\s*NpcElement target,\s*NpcElement owner,.*?bool retainBulwarkTarget,\s*bool retainGuardTarget\).*?behavior == SummonBehavior\.Bulwark.*?BulwarkLocalEngagementRange.*?BulwarkTargetRetentionRange.*?BulwarkPlayerLeash.*?advanceHeld = IsBulwarkAdvanceActive\(hero\).*?owner\.Coords - hero\.Coords.*?breachRange = retainBulwarkTarget\s*\? targetRetentionRange\s*:\s*localEngagementRange.*?target\.Coords - owner\.Coords.*?return recentAttacker\s*\? 0\s*:\s*targetingProtected \? 1 : 2;.*?localRange = retainBulwarkTarget\s*\? targetRetentionRange\s*:\s*localEngagementRange.*?target\.Coords - owner\.Coords.*?\? 2\s*:\s*int\.MaxValue') -or
    ($runtimeSource -notmatch '(?s)awarenessRange = behavior == SummonBehavior\.Bulwark.*?BulwarkPlayerLeash.*?GetAutonomousTargetPriority\(\s*target,\s*owner,\s*behavior,\s*hero,\s*hostInCombat,\s*retainBulwarkTarget: behavior == SummonBehavior\.Bulwark\s*&& ReferenceEquals\(target, committedTarget\),\s*retainGuardTarget: behavior == SummonBehavior\.Guard\s*&& ReferenceEquals\(target, committedTarget\)\)') -or
    ($runtimeSource -notmatch '(?s)assignmentPenalty = behavior == SummonBehavior\.Bulwark\s*\? 0\.0f.*?BulwarkTargetMinimumCommitmentSeconds')) {
    throw "Guard and both Bulwark stances do not preserve their bounded defensive priorities."
}
$guardPriorityMethod = [regex]::Match(
    $runtimeSource,
    '(?s)private static int GetAutonomousTargetPriority\(.*?(?=\r?\n\s*private static float GetGuardEngagementRange)')
if (($pluginSource -notmatch '(?s)GuardEngagementRange = BindOrdered\(\s*"Summon Behaviors",\s*"GuardEngagementRange",\s*15\.0f.*?AcceptableValueRange<float>\(0\.0f, 30\.0f\)') -or
    ($runtimeSource -notmatch '(?s)GetGuardEngagementRange\(bool retainTarget\).*?GuardEngagementRange.*?DefaultGuardEngagementRange.*?Math\.Max\(0\.0f, engagementRange\).*?engagementRange <= 0\.0f \|\| !retainTarget.*?engagementRange \+ GuardEngagementRetentionPadding') -or
    (!$guardPriorityMethod.Success) -or
    ($guardPriorityMethod.Value -notmatch '(?s)bool retainGuardTarget.*?if \(behavior == SummonBehavior\.Hunt\).*?if \(behavior == SummonBehavior\.Bulwark\)') -or
    ($guardPriorityMethod.Value -notmatch '(?s)if \(recentAttacker\).*?return 0;.*?if \(targetingProtected\).*?return 1;.*?target\.NpcAI.*?hostInCombat.*?GuardMeleeThreatRange.*?return 2;') -or
    ($guardPriorityMethod.Value -notmatch 'GetGuardEngagementRange\(\s*retainGuardTarget') -or
    ($guardPriorityMethod.Value -notmatch '(?s)heroDistanceSqr = hero == null.*?target\.Coords - hero\.Coords.*?guardEngagementRange > 0\.0f.*?heroDistanceSqr.*?guardEngagementRange \* guardEngagementRange.*?return 3;') -or
    ($guardPriorityMethod.Value -notmatch 'return int\.MaxValue') ) {
    throw 'Guard does not use its configurable hero-centered 15-metre proactive engagement range with a zero-disable and bounded 20-metre retention rule.'
}
if (($runtimeSource -notmatch '(?s)RefreshAutonomousTargets\(.*?!WithFactionUtils\.WantToFight\(owner, target\).*?GetAutonomousTargetPriority\(.*?behavior.*?HasAutonomousTargetLineOfSight\(\s*summon,\s*hero,\s*target,\s*behavior') -or
    (!$guardPriorityMethod.Success) -or
    ($guardPriorityMethod.Value -notmatch '(?s)if \(behavior == SummonBehavior\.Hunt\).*?if \(behavior == SummonBehavior\.Bulwark\)')) {
    throw 'Guard proactive targeting does not retain faction hostility, line-of-sight, and distinct Guard/Hunt/Bulwark behavior boundaries.'
}
if ($runtimeSource -notmatch '(?s)UpdateCatchUpSpeed\(.*?GetSummonBehavior\(\).*?SummonBehavior\.Bulwark.*?!HeldSummons\.ContainsKey\(id\).*?!PendingRecallPlacements\.ContainsKey\(id\).*?!HasActivePriorityTarget\(summon\).*?advanceHeld = IsBulwarkAdvanceActive\(hero\).*?GetBulwarkAnchor\(summon\).*?BulwarkAdvanceCatchUpStopDistance.*?BulwarkCatchUpStopDistance.*?BulwarkAdvanceCatchUpStartDistance.*?BulwarkCatchUpStartDistance.*?BulwarkAdvanceCatchUpMinimumMultiplier.*?BulwarkAdvanceMaximumMovementMultiplier.*?existingMovement.*?else if \(!npc\.NpcAI\.InCombat\)') {
    throw "Bulwark servants do not use stance-aware hysteretic catch-up without affecting combat pursuit."
}
if ($runtimeSource -notmatch '(?s)class CatchUpSpeedState.*?Multiplier.*?MovementTweak.*?UpdateCatchUpSpeed\(.*?Math\.Abs\(current\.Multiplier - multiplier\).*?new CatchUpSpeedState.*?Multiplier = multiplier.*?MovementTweak = tweak.*?UpdateEmpoweredPresentation\(.*?SpeedTweaks\.TryGetValue\(id, out catchUpSpeed\).*?playback \*= catchUpSpeed\.Multiplier') {
    throw "Bulwark Advance catch-up does not update safely or keep locomotion playback synchronized."
}
if ($runtimeSource -notmatch '(?s)IsSprintActionHeld\(Hero hero\).*?KeyBindings\.Gameplay\.Sprint.*?IsBulwarkAdvanceActive\(Hero hero\).*?SummonBehavior\.Bulwark.*?IsSprintActionHeld\(hero\).*?IsTargetCommandModifierHeld\(.*?TargetCommandModifierMode\.None.*?return true;.*?return IsSprintActionHeld\(hero\);') {
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
if ($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSightFrom\(.*?AutonomousLineOfSightKey cacheKey.*?ObserverId = observer == null.*?TargetId = \(\(Model\)target\)\.ID.*?AutonomousLineOfSightByTarget\.TryGetValue\(cacheKey.*?cached\.ExpiresAt >= Time\.unscaledTime.*?return cached\.Visible;.*?Physics\.RaycastNonAlloc.*?AutonomousLineOfSightByTarget\[cacheKey\].*?AutonomousLineOfSightCacheSeconds') {
    throw "Autonomous line-of-sight checks are not shared briefly across the host."
}
if (($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSight\(\s*NpcHeroSummon summon,\s*Hero hero,\s*NpcElement target,\s*SummonBehavior behavior,\s*bool directedHunt\).*?behavior == SummonBehavior\.Hunt && directedHunt.*?summonObserver.*?HasAutonomousTargetLineOfSightFrom\(\s*summonObserver.*?return HasAutonomousTargetLineOfSightFrom\(null, hero, target\)\s*\|\|.*?HasAutonomousTargetLineOfSightFrom\(\s*summonObserver') -or
    ($runtimeSource -notmatch 'HasAutonomousTargetLineOfSight\(\s*summon,\s*hero,\s*target,\s*behavior,\s*directedHunt\)')) {
    throw "Ordinary Hunt does not share hero and servant sight, or Directed Hunt does not require the acting servant's sight."
}
if ($runtimeSource -notmatch '(?s)AutonomousTargetCandidateBuffer\.Clear\(\).*?GetAutonomousTargetCandidates\(.*?AutonomousTargetCandidateBuffer\.Add\(.*?AutonomousTargetCandidateBuffer\.Sort\(\s*AutonomousTargetCandidateComparer\.Instance\).*?foreach \(AutonomousTargetCandidate candidate.*?HasAutonomousTargetLineOfSight') {
    throw 'Autonomous candidates are not ranked deterministically before spending the shared line-of-sight budget.'
}
if ($runtimeSource -notmatch '(?s)!AwarenessTargets\.TryGetValue\(summonId, out selectedRecord\).*?!ReferenceEquals\(selectedRecord\.Target, selectedTarget\).*?!owner\.ForceAddCombatTarget\(.*?ClearAutonomousTargetOverride\(summon\);\s*return true;.*?AwarenessTargets\[summonId\].*?SetAutonomousTargetOverride\(\s*summon,\s*selectedTarget,\s*directedHunt\)') {
    throw "Autonomous overrides can still bypass a native combat-target rejection."
}
if ($runtimeSource -notmatch '(?s)CycleSummonBehavior\(\).*?TryCycleSummonBehavior.*?foreach \(NpcHeroSummon summon.*?RefreshAutonomousTargets\(\s*summon,\s*plugin,\s*behavior\);.*?EnforceSummonBehavior\(summon, behavior\);') {
    throw "A completed behavior change does not refresh targets before enforcing its new formation."
}
if ($runtimeSource -match '(?s)CycleSummonBehavior\(\).*?TryCycleSummonBehavior.*?RemoveAllAwarenessTargets\(\).*?PublishCommand') {
    throw "Behavior changes still discard the entire host's valid target state before refreshing."
}
if ($runtimeSource -notmatch '(?s)SetExplicitCommandTarget\(.*?RemoveAwarenessTargetsForSummon\(\s*summon,\s*target,\s*preserveOwnedOverride: true\).*?ExplicitCommandTargets\[summonId\] = target.*?SetOwnedTargetOverride\(summon, target, 10, true\).*?!ReferenceEquals\(previousTarget, target\).*?RemoveCombatTarget\(previousTarget\).*?NpcAI\.InCombat.*?ForceAddCombatTarget\(\s*target,\s*recalculateTarget: true\).*?NpcAI\.EnterCombatWith\(\s*target,\s*forceChange: true\).*?CommandSummons\(.*?SetExplicitCommandTarget\(summon, target\)') {
    throw "Explicit Attack and Swarm commands do not replace stale ownership and enter combat with their ordered target."
}
if (($runtimeSource -notmatch '(?s)HasExplicitCommandTarget\(NpcHeroSummon summon\).*?ExplicitCommandTargets\.TryGetValue.*?SetOwnedTargetOverride\(summon, target, 10, true\).*?ReassertOwnedCombatTarget\(summon, target\)') -or
    ($runtimeSource -notmatch '(?s)ReassertOwnedCombatTarget\(.*?GetCurrentTarget\(\).*?EnterCombatWith\(.*?ForceAddCombatTarget\(')) {
    throw "Explicit Attack and Swarm orders are not reasserted when native AI drops their override or combat target."
}
if (($runtimeSource -notmatch '(?s)UpdateExplicitCommandPursuit\(.*?ExplicitCommandStallSeconds.*?ExplicitCommandPathCheckIntervalSeconds.*?PathUtilities\.IsPathPossible.*?HasAutonomousTargetLineOfSightFrom.*?ExplicitCommandMaximumUnreachableSamples.*?ReleaseExplicitCommandTarget') -or
    ($runtimeSource -notmatch '(?s)ReleaseExplicitCommandTarget\(.*?ExplicitCommandTargets\.Remove.*?ExplicitCommandPursuitStates\.Remove.*?ClearOwnedTargetOverride.*?RemoveCombatTarget.*?ClearSwarm') -or
    ($runtimeSource -notmatch '(?s)HasExplicitCommandTarget\(.*?WantToFight\(summon\.ParentModel, target\).*?ReleaseExplicitCommandTarget')) {
    throw "Explicit commands do not use bounded unreachable-target recovery or faction-safe invalidation."
}
if ($runtimeSource -notmatch '(?s)BeforeStayCloseToAlly\(.*?hasExplicitCommandTarget = HasExplicitCommandTarget\(summon\).*?!hasExplicitCommandTarget\s*&& HeldSummons\.TryGetValue.*?!hasExplicitCommandTarget\s*&& TryGetRecallAnchor.*?hasPriorityTarget = hasExplicitCommandTarget\s*\|\| HasActivePriorityTarget\(summon\)') {
    throw "A saved Hold anchor can still override an active explicit Attack or Swarm order."
}
if ($runtimeSource -notmatch '(?s)BeforeStayCloseToAlly\(.*?hasPriorityTarget.*?SummonFormationCoordinator\.Suspend.*?patrol\.UpdateRadius\(NativePatrolRadius\);\s*return false;.*?HasActivePriorityTarget\(.*?HasExplicitCommandTarget\(summon\).*?AutonomousTargetOverrides\.TryGetValue') {
    throw "Formation following can still override an explicit or autonomous combat target."
}
if ($runtimeSource -notmatch '(?s)HasActivePriorityTarget\(NpcHeroSummon summon\).*?AutonomousTargetOverrides\.TryGetValue.*?IsTargetWithinOwnedRangeGrace\(summon, target\).*?SetOwnedTargetOverride\(summon, target, 5, false\).*?ReassertOwnedCombatTarget\(summon, target\).*?return true;') {
    throw "A valid autonomous Hunt target is not reasserted when native AI drops its override or combat target."
}
if (($runtimeSource -notmatch '(?s)class OwnedTargetOverrideState.*?Target.*?Element.*?Priority.*?Explicit.*?OutOfRangeSince') -or
    ($runtimeSource -notmatch '(?s)SetOwnedTargetOverride\(.*?ReferenceEquals\(current\.Target, target\).*?current\.Priority == priority.*?current\.Explicit == explicitTarget.*?return current;.*?AddElement\(.*?SoulAndServiceTargetOverride.*?OwnedTargetOverrides\[summonId\] = replacementState.*?current\.Element\.Discard\(\)') -or
    ($runtimeSource -notmatch '(?s)ClearOwnedTargetOverride\(.*?OwnedTargetOverrides\.Remove\(summonId\).*?state\.Element\.Discard\(\)') -or
    ($runtimeSource -match 'HeroSummonTargetOverride')) {
    throw 'Soul and Service does not own, reuse, and exactly discard its target override without native combat-exit churn.'
}
if (($runtimeSource -notmatch '(?s)if \(!plugin\.IsEnabled\).*?RemoveAllAwarenessTargets\(\);.*?ClearAllOwnedTargetOverrides\(\);.*?ExplicitCommandTargets\.Clear\(\);') -or
    ($runtimeSource -notmatch '(?s)Shutdown\(\).*?RemoveAllAwarenessTargets\(\);.*?ClearAllOwnedTargetOverrides\(\);.*?ExplicitCommandTargets\.Clear\(\);')) {
    throw 'Plugin disable or shutdown can leave an explicit target override installed.'
}
if (($runtimeSource -notmatch '(?s)directedHunt = behavior == SummonBehavior\.Hunt.*?awarenessRange = behavior == SummonBehavior\.Bulwark.*?behavior == SummonBehavior\.Hunt.*?BaseHuntAwarenessRange\s*\* Math\.Max\(1\.0f, transferredSight\)') -or
    ($runtimeSource -notmatch '(?s)retentionRange = behavior == SummonBehavior\.Bulwark.*?behavior == SummonBehavior\.Hunt\s*\? awarenessRange \+ 5\.0f') -or
    ($runtimeSource -notmatch '(?s)GetAutonomousTargetPriority\(\s*NpcElement target,.*?if \(behavior == SummonBehavior\.Hunt\).*?return recentAttacker\s*\? 0\s*:\s*targetingProtected \? 1 : 2;')) {
    throw "Hunt does not use its uncapped Steel and Bone-aware range and attacker-first aggressive priority."
}
if ($pluginSource -notmatch '(?s)DirectedHuntEnabled = BindOrdered\(\s*"Summon Behaviors",\s*"EnableDirectedHunt",\s*true') {
    throw 'Directed Hunt does not retain its enable toggle.'
}
if ($pluginSource -match 'DirectedHuntReleaseSeconds|Directed Hunt Release Duration' -or
    $runtimeSource -match 'HuntSweep|_huntSweep|DirectedHuntReleaseSeconds') {
    throw 'Legacy held-Sprint Hunt Sweep or its release-duration compatibility path still exists.'
}
if (($runtimeSource -notmatch '(?s)TryResolveHuntPointPreview\(.*?Physics\.RaycastNonAlloc.*?HuntPointRaycastHits.*?HuntPointMinimumDistance.*?hit\.normal\.y < 0\.45f.*?GetNearest\(.*?NNConstraint\.Walkable.*?PathUtilities\.IsPathPossible.*?HuntPointMaximumNavSnapDistance') -or
    ($runtimeSource -notmatch '(?s)CanUseDirectedHunt\(.*?IsSprintActionHeld\(hero\).*?GetFormationHost\(hero\)\.Length > 0.*?CanShowHuntPointPrompt\(.*?ShowDirectedHuntPreview\.Value.*?TryResolveHuntPointPreview\(.*?HuntPointPreviewCacheSeconds') -or
    ($runtimeSource -match '(?s)CanShowHuntPointPrompt\(.*?IsEligibleForHuntAttackMove|CanShowHuntPointPrompt\(.*?HasEligibleHuntAttackMoveParticipant')) {
    throw 'Directed Hunt terrain preview is not nonalloc, Sprint-gated, five-metre minimum, navigation validated, and independent of servant activity.'
}
if (($runtimeSource -notmatch '(?s)CompleteBehaviorCommandHold\(\).*?_huntPointTapArmed.*?Time\.unscaledTime - _behaviorCommandPressedAt\s*< BehaviorCommandHoldSeconds.*?ResetBehaviorCommandHold\(\).*?BeginHuntAttackMove') -or
    ($runtimeSource -notmatch '(?s)UpdateBehaviorCommandHold\(.*?BehaviorCommandHoldSeconds.*?_huntPointTapArmed = false.*?CycleSummonBehavior\(\)') -or
    ($runtimeSource -notmatch '(?s)SummonCommandInteractable\(\s*Vector3 huntPoint.*?_huntPointCommand = true.*?Kind = SummonCommandState\.Attack.*?"Hunt".*?huntPoint') -or
    ($runtimeSource -notmatch '(?s)SummonCommandAction.*?StartInteraction\(.*?if \(_huntPointCommand\).*?TryBeginBehaviorCommandHold\(.*?_huntPoint.*?EndInteraction\(.*?if \(_huntPointCommand\).*?CompleteBehaviorCommandHold\(\)')) {
    throw 'Sprint plus a short Interact tap does not issue Hunt while the existing long hold still cycles behavior.'
}
if (($runtimeSource -notmatch '(?s)BeginHuntAttackMove\(.*?ResetHuntAttackMove\(\).*?NpcHeroSummon\[\] host = GetFormationHost\(hero\).*?for \(int index = 0; index < host\.Length; index\+\+\).*?IsEligibleForHuntAttackMove\(summon, hero\).*?ExitStaleCombatForDirectedHunt\(summon\).*?HuntAttackMoveParticipants\[summonId\].*?TryRedirectCombatHunterToHuntPoint\(summon, hero\).*?if \(HuntAttackMoveParticipants\.Count > 0\).*?else\s*\{\s*ResetHuntAttackMove\(\);\s*\}.*?PublishCommand\(.*?SummonCommandState\.Attack.*?SummonAttackCommandId.*?"Hunt"') -or
    ($runtimeSource -notmatch '(?s)TryRedirectCombatHunterToHuntPoint\(.*?IsOwnedSummon\(summon, hero\).*?IsHeld\(summon\).*?PendingRecallPlacements\.ContainsKey\(summonId\).*?IsRecallTargetSuppressed\(summon\).*?HasExplicitCommandTarget\(summon\).*?GetCurrentTarget\(\) == null.*?NpcAI\.InCombat.*?BaseHuntAwarenessRange.*?GetNpcsInSphere\(\s*summon\.ParentModel\.Coords,\s*awarenessRange\).*?WantToFight.*?HasAutonomousTargetLineOfSightFrom\(\s*summon\.ParentModel.*?IsHuntAttackMoveTargetReachable\(\s*sourceNode,\s*candidate\).*?candidate\.Coords\s*- _huntAttackMoveDestination.*?TrySetPassiveSharedTarget\(summon, closestTarget\)') -or
    ($runtimeSource -notmatch '(?s)IsEligibleForHuntAttackMove\(.*?IsOwnedSummon\(summon, hero\).*?IsHeld\(summon\).*?PendingRecallPlacements\.ContainsKey\(summonId\).*?IsRecallTargetSuppressed\(summon\).*?HasExplicitCommandTarget\(summon\).*?HasActivePriorityTarget\(summon\).*?GetCurrentTarget\(\) == null') -or
    ($runtimeSource -notmatch '(?s)ExitStaleCombatForDirectedHunt\(.*?NpcAI\.InCombat.*?GetCurrentTarget\(\) != null.*?HasExplicitCommandTarget\(summon\).*?HasActivePriorityTarget\(summon\).*?NpcAI\.ExitCombat\(\s*force: true,\s*exitToIdle: true,\s*canBeVictorious: false\)')) {
    throw 'Directed Hunt participant capture, autonomous combat redirection, protected-state exclusions, unconditional Hunt feedback, or Battlecry attack voice identity is incomplete.'
}
if (($runtimeSource -notmatch '(?s)PruneHuntAttackMoveParticipants\(Hero hero\).*?HuntAttackMoveRemovalBuffer\.Clear\(\).*?foreach.*?HuntAttackMoveParticipants.*?HuntAttackMoveRemovalBuffer\.Add.*?RemoveHuntAttackMoveParticipant') -or
    ($runtimeSource -match 'HuntAttackMoveParticipants\.ToArray\(\)')) {
    throw 'Directed Hunt does not prune its original participant snapshot without per-update allocation.'
}
if (($runtimeSource -notmatch '(?s)TryGetHuntAttackMoveAnchor\(.*?FormationPurpose\.HuntAttackMove.*?SummonFormationCoordinator\.ResolveAnchor') -or
    ($runtimeSource -notmatch '(?s)UpdateHuntAttackMove\(.*?HuntAttackMovePhase\.Travelling.*?IsAtResolvedAnchor\(\s*summon,\s*FormationPurpose\.HuntAttackMove,\s*HuntAttackMoveAnchorTolerance\).*?HuntAttackMovePhase\.Searching.*?HuntAttackMoveSearchSeconds.*?HuntAttackMoveMaximumTravelSeconds.*?ResetHuntAttackMove') -or
    ($runtimeSource -notmatch '(?s)class HuntAttackMoveSearchState.*?Arrived.*?HasSweepDestination.*?SweepDestination.*?NextSweepAt.*?SweepLegs') -or
    ($runtimeSource -notmatch '(?s)TryGetHuntAttackMoveAnchor\(.*?HuntAttackMovePhase\.Searching.*?IsAtResolvedAnchor.*?searchState\.Arrived = true.*?TryAssignHuntAttackMoveSweepDestination.*?searchState\.SweepDestination') -or
    ($runtimeSource -notmatch '(?s)TryAssignHuntAttackMoveSweepDestination\(.*?TryFindIdleDestination\(\s*_huntAttackMoveDestination,\s*HuntAttackMoveSweepRadius.*?IsHuntAttackMoveSweepDestinationReserved.*?SweepLegs\+\+.*?InvalidateAppliedAnchor') -or
    ($runtimeSource -notmatch '(?s)RemoveHuntAttackMoveParticipant\(.*?HuntAttackMoveSearchStates\.Remove.*?ResetHuntAttackMove\(.*?HuntAttackMoveSearchStates\.Clear') -or
    ($runtimeSource -match 'IsHuntAttackMoveTargetCandidate|HuntAttackMoveCorridorRadius|HuntAttackMoveSearchRadius') -or
    ($runtimeSource -notmatch '(?s)directedHunt = behavior == SummonBehavior\.Hunt.*?BaseHuntAwarenessRange\s*\* Math\.Max\(1\.0f, transferredSight\).*?awarenessCenter = held.*?: owner\.Coords') -or
    ($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSight\(.*?behavior == SummonBehavior\.Hunt && directedHunt.*?summonObserver.*?HasAutonomousTargetLineOfSightFrom') -or
    ($runtimeSource -notmatch '(?s)if \(directedHunt\s*&& \(candidate\.Committed \|\| beatsBest\)\s*&& !IsHuntAttackMoveTargetReachable') -or
    ($runtimeSource -notmatch '(?s)if \(directedHunt\).*?RemoveHuntAttackMoveParticipant\(summonId\).*?SetAutonomousTargetOverride\(\s*summon,\s*selectedTarget,\s*directedHunt\)') -or
    ($runtimeSource -notmatch '(?s)class OwnedTargetOverrideState.*?IgnoreHeroLeash.*?IsTargetWithinOwnedRangeGrace\(.*?state\.IgnoreHeroLeash.*?return true;.*?IsWithinNativeSummonTargetRetentionRange')) {
    throw 'Directed Hunt does not use coordinated arrival, per-servant search sweeps, servant-centered en-route acquisition, bounded timeout, reachable targets, and immediate attack-move exit after commitment.'
}
if (($runtimeSource -notmatch '(?s)RegisterReciprocalServantThreat\(.*?WantToFight\(target, summon\.ParentModel\).*?target\.NpcAI\.InCombat.*?target\.ForceAddCombatTarget\(\s*summon\.ParentModel,\s*recalculateTarget: false\).*?target\.NpcAI\.EnterCombatWith\(\s*summon\.ParentModel,\s*forceChange: false\)') -or
    ($runtimeSource -notmatch '(?s)SetExplicitCommandTarget\(.*?RegisterReciprocalServantThreat\(summon, target\)') -or
    ($runtimeSource -notmatch '(?s)SetAutonomousTargetOverride\(.*?RegisterReciprocalServantThreat\(summon, target\)')) {
    throw 'Servant target commitments do not register reciprocal hostile threat for reliable combat initiation.'
}
if (($pluginSource -notmatch '(?s)BulwarkAdvanceEnabled = BindOrdered\(\s*"Summon Behaviors",\s*"EnableBulwarkAdvance",\s*true') -or
    ($pluginSource -notmatch '(?s)BulwarkAdvanceReleaseSeconds = BindOrdered\(\s*"Summon Behaviors",\s*"BulwarkAdvanceReleaseSeconds",\s*0\.0f.*?AcceptableValueRange<float>\(0\.0f, 10\.0f\)') -or
    ($pluginSource -notmatch '(?s)BulwarkAdvanceSpeedMultiplier = BindOrdered\(\s*"Summon Behaviors",\s*"BulwarkAdvanceSpeedMultiplier",\s*2\.0f.*?AcceptableValueRange<float>\(1\.0f, 3\.0f\)') -or
    ($pluginSource -notmatch '(?s)GuardFormationDistance = BindOrdered\(.*?4\.5f.*?HuntFormationDistance = BindOrdered\(.*?5\.5f.*?BulwarkCloseGuardDistance = BindOrdered\(.*?3\.5f.*?BulwarkAdvanceDistance = BindOrdered\(.*?4\.5f') -or
    ($pluginSource -notmatch '(?s)BulwarkLocalEngagementRange = BindOrdered\(.*?4\.0f.*?BulwarkTargetRetentionRange = BindOrdered\(.*?6\.0f.*?BulwarkPlayerLeash = BindOrdered\(.*?8\.0f')) {
    throw "Bulwark Advance speed, behavior spacing, or engagement-range configuration is incomplete."
}
if (($runtimeSource -notmatch '(?s)private static void UpdateBulwarkAdvanceState\(.*?plugin\.BulwarkAdvanceEnabled.*?SummonBehavior\.Bulwark.*?IsSprintActionHeld\(hero\).*?Mathf\.Clamp\(.*?BulwarkAdvanceReleaseSeconds\.Value.*?0\.0f,\s*10\.0f\).*?_bulwarkAdvanceReleasedUntil = Time\.unscaledTime \+ duration.*?bool active = sprintHeld\s*\|\| Time\.unscaledTime < _bulwarkAdvanceReleasedUntil') -or
    ($runtimeSource -notmatch '(?s)IsBulwarkAdvanceActive\(Hero hero\).*?BulwarkAdvanceEnabled.*?IsSprintActionHeld\(hero\).*?_bulwarkAdvanceReleasedUntil') -or
    ($runtimeSource -notmatch '(?s)IsBulwarkAdvanceReleaseGraceActive\(Hero hero\).*?!IsSprintActionHeld\(hero\).*?IsBulwarkAdvanceActive\(hero\)') -or
    ($runtimeSource -notmatch '(?s)UpdateBulwarkFacing\(Hero hero\).*?IsBulwarkAdvanceReleaseGraceActive\(hero\).*?_hasBulwarkViewCandidate = false.*?return;')) {
    throw "Bulwark Advance active state, release grace, or release-facing lock is incomplete."
}
if (($runtimeSource -notmatch '(?s)GetGuardAnchor\(.*?SummonFormationCoordinator\.GetRadialAnchor\(.*?FormationPurpose\.Guard') -or
    ($runtimeSource -notmatch '(?s)GetHuntAnchor\(.*?SummonFormationCoordinator\.GetRadialAnchor\(.*?FormationPurpose\.Hunt') -or
    ($runtimeSource -notmatch '(?s)GetRecoveredFormationAnchor\(.*?SummonFormationCoordinator\.ResolveAnchor')) {
    throw 'Guard and Hunt do not share coordinator reservations and blocked-slot recovery.'
}
if (($runtimeSource -notmatch 'struct AutonomousLineOfSightKey') -or
    ($runtimeSource -notmatch 'Dictionary<AutonomousLineOfSightKey,') -or
    ($runtimeSource -match '"host\|" \+ targetId') -or
    ($runtimeSource -notmatch 'AutonomousTargetAssignmentPenalty = 0\.25f') -or
    ($runtimeSource -notmatch 'GetAutonomousTargetAssignmentCount\(')) {
    throw "Multi-summon sensing or autonomous target distribution still performs avoidable allocation or crowding work."
}
if (($runtimeSource -notmatch 'MaximumPathChecksPerFrame = 4') -or
    ($runtimeSource -notmatch 'MaximumLineOfSightRaycastsPerFrame = 8') -or
    ($runtimeSource -notmatch '(?s)UpdateExplicitCommandPursuit\(.*?TryConsumeFrameBudget\(\s*ref _pathWorkBudgetFrame') -or
    ($runtimeSource -match '(?s)IsHuntAttackMoveTargetReachable\(.*?TryConsumeFrameBudget\(\s*ref _pathWorkBudgetFrame') -or
    ($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSightFrom\(.*?TryConsumeFrameBudget\(\s*ref _lineOfSightBudgetFrame')) {
    throw 'Explicit pursuit and LOS work are not capped, or Directed Hunt still rejects reachable targets when the shared path budget is exhausted.'
}
if (($runtimeSource -notmatch '(?s)HasAutonomousTargetLineOfSightFrom\(.*?if \(cached == null\).*?cached = new AutonomousLineOfSightRecord\(\).*?cached\.Origin = origin.*?cached\.ExpiresAt =') -or
    ($runtimeSource -notmatch 'AutonomousLineOfSightRetentionSeconds = 5\.0f')) {
    throw 'Line-of-sight cache refreshes do not reuse records or retain them briefly for reuse.'
}
if (($runtimeSource -notmatch '(?s)GetAiTickInterval\(NpcAlly ally\).*?AiDecisionIntervalJitterFraction') -or
    ($runtimeSource -notmatch '(?s)class SummonCommandInteractable.*?IHeroAction\[\] _actions.*?return _actions;') -or
    ($runtimeSource -notmatch '(?s)class SummonCommandAction.*?InfoFrame _actionFrame.*?ActionFrame => _actionFrame')) {
    throw "AI work is not staggered or command presentation still allocates during repeated UI polling."
}
if ($runtimeSource -notmatch '(?s)HasExplicitCommandTarget\(summon\).*?RemoveAwarenessTargetsForSummon\(\s*summon,\s*ExplicitCommandTargets\[summonId\]\);') {
    throw "Autonomous refresh can still remove the live explicit command target."
}
if ($runtimeSource -notmatch '(?s)bool hardInvalid = record\.Target == null.*?GetAutonomousTargetPriority\(\s*record\.Target,\s*owner,\s*behavior,\s*hero,\s*hostInCombat,\s*retainBulwarkTarget: behavior == SummonBehavior\.Bulwark,\s*retainGuardTarget: behavior == SummonBehavior\.Guard\)\s*== int\.MaxValue.*?bool rangeExpired.*?IsTargetWithinOwnedRangeGrace') {
    throw "Behavior changes do not invalidate autonomous targets forbidden by the new mode."
}
if ($runtimeSource -notmatch '(?s)selectedTarget == null.*?ForceAddCombatTarget\(.*?owner\.NpcAI != null && !owner\.NpcAI\.InCombat.*?owner\.NpcAI\.EnterCombatWith\(\s*selectedTarget,\s*forceChange: true\).*?SetAutonomousTargetOverride') {
    throw "Autonomous target ownership does not explicitly enter combat after native FindTarget is suppressed."
}
if ($runtimeSource -notmatch '(?s)SetAutonomousTargetOverride\(.*?AutonomousTargetOverrides\[summonId\] = target;\s*SetOwnedTargetOverride\(\s*summon,\s*target,\s*5,\s*false,\s*ignoreHeroLeash\);\s*ReassertOwnedCombatTarget') {
    throw "Autonomous override ownership is not established before marker initialization re-enters targeting."
}
if ($runtimeSource -notmatch '(?s)LogSummonControlState\(.*?LastControlDiagnosticBySummon\.TryGetValue.*?string\.Equals.*?NextControlDiagnosticBySummon\.TryGetValue.*?ControlDiagnosticMinimumIntervalSeconds.*?Summon control: summon=') {
    throw "Temporary summon ownership diagnostics are not state-change-only and rate bounded."
}
if ($runtimeSource -notmatch '(?s)LogSummonControlState\(.*?overridePresent=.*?overrideActive=.*?overrideMode=.*?overrideTarget=.*?overrideDistance=') {
    throw 'Summon ownership diagnostics do not distinguish absent, suspended, explicit, and autonomous override state.'
}
if ($runtimeSource -notmatch '(?s)bool BeforeFindTarget\(.*?IsRecallTargetSuppressed\(summon\).*?return false;.*?RefreshAutonomousTargets\(summon, plugin, behavior\).*?AfterFindTarget\(.*?IsRecallTargetSuppressed\(summon\).*?return;.*?EnforceSummonBehavior\(summon, behavior\)') {
    throw "Autonomous priority targets are not injected before native selection or suppressed during Recall Host."
}
if ($runtimeSource -notmatch '(?s)typeof\(EnemyBaseClass\),\s*"UpdateCombatSlotStatus",\s*new\[\] \{ typeof\(ICharacter\) \}.*?nameof\(AfterCombatSlotStatusUpdate\).*?AfterCombatSlotStatusUpdate\(.*?target is Hero.*?OwnedCombatSlotIndex == -1.*?npc\.IsHeroSummon.*?ReleaseCombatSlots\(\)') {
    throw "Hero summons do not release stale hero-centered combat slots while pursuing non-Hero targets."
}
if ($runtimeSource -match 'harmony\.Patch\(\s*TeleportToAllyMethod' -or
    $runtimeSource -notmatch '(?s)QueueTeleportVfx\(NpcHeroSummon summon\).*?pending\.OutstandingRequests\+\+.*?OutstandingRequests = 1.*?AfterTeleportPathCalculated\(.*?!PendingTeleportVfxBySummon\.TryGetValue\(summonId, out pending\).*?return;.*?Coords - pending\.Origin.*?> 0\.25f.*?PendingTeleportVfxBySummon\.Remove\(summonId\).*?RestoreRecallLocomotion\(summon, summonId\).*?SpawnNecromanticSummonVfx\(summon\.ParentModel\).*?pending\.OutstandingRequests--.*?pending\.OutstandingRequests <= 0.*?PendingTeleportVfxBySummon\.Remove\(summonId\).*?RestoreRecallLocomotion\(summon, summonId\)' -or
    ([regex]::Matches($runtimeSource, 'QueueTeleportVfx\(summon\);').Count -ne 3)) {
    throw "Arrival VFX is not limited to explicit Soul and Service teleports, coalesced, and gated on confirmed movement."
}
if ($runtimeSource -notmatch '(?s)RecallHost\(Hero hero\).*?SummonFormationCoordinator\.Suspend\(\(\(Model\)summon\)\.ID\).*?RecallTargetSuppressionUntil\[summonId\].*?RecallTargetSuppressionSeconds.*?ExplicitCommandTargets\.Remove.*?RemoveAwarenessTargetsForSummon.*?SetSummonHeld\(summon, false\).*?ForceRecallCombatExit\(summon\.ParentModel\).*?hasReservedDestination =\s*SummonFormationCoordinator\.TryReserveRecallPlacement\(.*?PendingRecallPlacements\[summonId\] =\s*new PendingRecallPlacement.*?HasReservedDestination = hasReservedDestination.*?LocomotionRestored = false.*?QueueTeleportVfx\(summon\);.*?TeleportToAllyMethod\.Invoke.*?recalled == 1 \? "Recall" : "Recall Host".*?PublishCommand\(\s*plugin,\s*SummonCommandState\.Follow,\s*SummonRecallCommandId,') {
    throw "Recall does not clear combat state, assign safe placements, use native teleporting, select singular or host feedback, and request its dedicated voice."
}
if (($runtimeSource -notmatch '(?s)ForceRecallCombatExit\(NpcElement npc\).*?NpcAI\.InCombat.*?NpcAI\.ExitCombat\(\s*force: true,\s*exitToIdle: true,\s*canBeVictorious: false\).*?ForceEndCombat\(\)') -or
    ($runtimeSource -notmatch '(?s)RestoreRecallLocomotion\(.*?DestinationConsumed.*?LocomotionRestored.*?placement\.LocomotionRestored = true.*?reachedReservedDestination.*?placement\.Position = summon\.ParentModel\.Coords.*?ForceRecallCombatExit\(summon\.ParentModel\).*?Movement\.ResetMainState\(patrol\).*?patrol\.UpdatePlace\(placement\.Position\).*?NpcStateType\.Idle.*?AnimationWatchdogsBySummon\.Remove\(summonId\)')) {
    throw 'Recall does not force a real combat exit and restore clean idle patrol locomotion exactly once after teleport.'
}
if (($formationSource -notmatch '(?s)TryReserveRecallPlacement\(.*?FormationPurpose\.Recall.*?RecallSlotsPerRing.*?RecallArcDegrees.*?RecallArcStartDegrees.*?GetRequiredSpacing.*?AstarPath\.active\.GetNearest.*?NNConstraint\.Walkable.*?PathUtilities\.IsPathPossible.*?RecallMaximumSnapDistance.*?IsReservedOrOccupied')) {
    throw 'Recall placement does not use stable, navigable, size-aware, non-overlapping side-biased reservations.'
}
if ($runtimeSource -notmatch '(?s)BeforeNpcTeleport\(.*?PendingRecallPlacements\.TryGetValue.*?placement\.DestinationConsumed = true.*?placement\.HasReservedDestination = false.*?!placement\.HasReservedDestination.*?destination\.position = placement\.Position.*?TryGetRecallAnchor\(.*?!placement\.LocomotionRestored.*?RecallPlacementHeroMoveReleaseDistance.*?BeforeStayCloseToAlly\(.*?TryGetRecallAnchor\(summon, out recallAnchor\).*?patrol\.UpdatePlace\(recallAnchor\)') {
    throw "Native Recall fallback is not tracked independently from optional reserved placement and preserved as a temporary arrival anchor."
}
if ($runtimeSource -notmatch '(?s)GetAiTickInterval\(NpcAlly ally\).*?return 2\.5f;.*?configuredInterval.*?NoviceAiDecisionInterval.*?GetNecromanticPower\(\) / 100\.0f.*?Mathf\.Lerp\(.*?noviceInterval.*?configuredInterval.*?AiDecisionIntervalRefreshSeconds') {
    throw "Hero-summon decision speed does not progress from novice to configured full-mastery responsiveness."
}
$formationHostBlock = [regex]::Match(
    $runtimeSource,
    '(?s)GetFormationHost\(Hero hero\).*?(?=\s*private static void InvalidateFormationHostCache)')
if (!$formationHostBlock.Success -or
    $formationHostBlock.Value -notmatch '(?s)FormationHostBuildBuffer\.Clear\(\).*?World\.All<NpcHeroSummon>\(\).*?FormationHostBuildBuffer\.Sort\(.*?membershipChanged.*?FormationHostBuildBuffer\.CopyTo\(_formationHostCache\).*?SummonFormationCoordinator\.Synchronize' -or
    $formationHostBlock.Value -match '\.Where\(|\.OrderBy\(|\.ToArray\(') {
    throw 'The shared formation host still allocates on stable refreshes or does not synchronize the coordinator.'
}
if ($runtimeSource -notmatch '(?s)EnsureFormationFacingHero\(Hero hero\).*?ReferenceEquals\(_formationFacingHero, hero\).*?return;.*?_formationFacingHero = hero.*?ResetBulwarkFacingState\(\).*?_hasGuardForward = false') {
    throw "Guard and Bulwark facing caches are not reset when the active hero changes."
}
if ($runtimeSource -notmatch '(?s)IsHostInCombat\(Hero hero\).*?Time\.frameCount.*?_hostCombatCacheFrame == frame.*?NpcHeroSummon\[\] host = GetFormationHost\(hero\).*?for \(int index = 0; index < host\.Length; index\+\+\).*?_hostCombatCacheValue = true') {
    throw "Group combat state is not cached once per frame across servant decisions."
}
if ($runtimeSource -notmatch '(?s)AwarenessTargets\.TryGetValue\(summonId, out record\).*?AwarenessTargets\.Remove\(summonId\).*?AwarenessTargets\[summonId\] = new AwarenessTargetRecord') {
    throw "Autonomous awareness is not maintained as one direct record per summon."
}
if ($runtimeSource -notmatch '(?s)GetGuardIdleAnchor\(.*?IsFormationLeaderMoving\(hero\).*?IsHostInCombat\(hero\).*?if \(hostInCombat\).*?CancelGuardIdleMovement.*?if \(heroMoving\).*?state\.FormationAnchor = liveAnchor.*?GuardAnchorRebaseDistance.*?if \(hostInCombat\)\s*\{\s*return state\.FormationAnchor;.*?state\.Wandering.*?state\.Returning.*?_guardIdleMoverId') {
    throw "Stationary Guard anchors are not latched with bounded wander and return states."
}
if (($runtimeSource -notmatch '(?s)FormationLeaderTravelDeadZone = 1\.5f.*?FormationLeaderElasticOffset = 0\.75f.*?FormationLeaderMovementStartSeconds = 0\.45f.*?FormationFacingCommitDistance = 3\.0f.*?FormationFacingCommitSeconds = 0\.75f.*?FormationFacingMaximumTurnDegreesPerSecond = 90\.0f') -or
    ($runtimeSource -notmatch '(?s)UpdateFormationLeaderMotion\(Hero hero\).*?Time\.frameCount.*?UpdateElasticFormationLeaderAnchor\(hero\).*?traveledBeyondDeadZone.*?sustainedMovement.*?_formationLeaderMoving = true.*?UpdateElasticFormationLeaderAnchor\(hero\)') -or
    ($runtimeSource -notmatch '(?s)UpdateElasticFormationLeaderAnchor\(Hero hero\).*?hero\.Coords - _formationLeaderAnchor.*?FormationLeaderElasticOffset.*?_formationLeaderAnchor = hero\.Coords\s*- offset\.normalized \* FormationLeaderElasticOffset') -or
    ($runtimeSource -notmatch '(?s)GetHuntAnchor\(.*?GetFormationLeaderAnchor\(hero\).*?GetBulwarkAnchor\(.*?leaderAnchor = advance\s*\? hero\.Coords \+ hero\.HorizontalVelocity.*?: GetFormationLeaderAnchor\(hero\).*?GetGuardAnchor\(.*?GetGuardFormationForward\(hero\).*?GetFormationLeaderAnchor\(hero\).*?GetGuardFormationForward\(Hero hero\).*?IsFormationLeaderMoving\(hero\)')) {
    throw "Guard, Hunt, and released Bulwark do not share the elastic leader frame while Advance bypasses it."
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
if ($runtimeSource -notmatch '(?s)GetHuntAnchor\(.*?FormationPurpose\.Hunt.*?plugin\.HuntFormationDistance\.Value.*?DefaultHuntFormationDistance.*?HuntFormationRingSpacing.*?HuntFormationSlotsPerRing') {
    throw 'Hunt does not request stable full-perimeter reservations from the coordinator.'
}
if (($runtimeSource -notmatch '(?s)UpdateFormationPatrolPlace\(.*?SummonFormationCoordinator\.ShouldApplyPatrolAnchor\(.*?updateDistance.*?patrol\.UpdatePlace\(anchor\)') -or
    ($formationSource -notmatch '(?s)ShouldApplyPatrolAnchor\(.*?HasAppliedAnchor.*?updateDistance \* updateDistance')) {
    throw 'Stable formations still request an identical navigation destination every AI decision.'
}
if (($runtimeSource -notmatch '(?s)GetAutonomousTargetCandidates\(.*?behavior != SummonBehavior\.Bulwark.*?grid\.GetNpcsInSphere\(awarenessCenter, awarenessRange\).*?BulwarkTargetCandidates\.Clear\(\).*?foreach \(NpcElement candidate in grid\.GetNpcsInSphere\(.*?BulwarkTargetCandidates\.Add\(candidate\).*?return BulwarkTargetCandidates') -or
    ($runtimeSource -notmatch '(?s)AutonomousTargetCandidateBuffer\.Clear\(\).*?foreach' ) -or
    ($runtimeSource -match 'HuntAttackMoveTargetCandidates|_huntAttackMoveTargetCandidate')) {
    throw 'Servant-centered Hunt querying or reusable autonomous and Bulwark candidate buffers are incomplete.'
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
    ($runtimeSource -notmatch '(?s)BeginSwarm\(.*?bulwarkAdvance = behavior == SummonBehavior\.Bulwark.*?GetBulwarkAdvanceMovementMultiplier\(plugin\).*?movementCeiling = bulwarkAdvance\s*\? BulwarkAdvanceMaximumMovementMultiplier\s*:\s*MaximumCommandMovementMultiplier.*?movementCeiling\s*/ \(empowermentMovement \* behaviorMovement\)') -or
    ($runtimeSource -notmatch '(?s)UpdateBehaviorSpeed\(.*?bulwarkAdvanceBoost.*?GetBulwarkAdvanceMovementMultiplier\(plugin\).*?otherCommandMovement.*?empowerment\.MovementMultiplier.*?swarm\.MovementMultiplier.*?catchUpSpeed\.Multiplier.*?movementCeiling = bulwarkAdvanceBoost\s*\? BulwarkAdvanceMaximumMovementMultiplier\s*:\s*MaximumCommandMovementMultiplier.*?movementCeiling / otherCommandMovement.*?BehaviorSpeedStates\[id\] = state') -or
    ($runtimeSource -notmatch '(?s)UpdateEmpoweredPresentation\(.*?BehaviorSpeedStates\.TryGetValue.*?playback \*= behaviorSpeed\.Multiplier') -or
    ($runtimeSource -notmatch '(?s)AfterSummonDiscard\(.*?RemoveBehaviorSpeedState\(id\).*?RemoveBehaviorSpeedState\(.*?DiscardTweak\(state\.MovementTweak\).*?BehaviorSpeedStates\.Remove\(id\)')) {
    throw "Hunt and Bulwark Advance speed composition is not transient, capped with Swarm, Empower, and catch-up, or cleaned up safely."
}
if ($runtimeSource -notmatch '(?s)RemoveBehaviorSpeedState\(string id\).*?string\.IsNullOrEmpty\(id\).*?return;.*?BehaviorSpeedStates\.TryGetValue\(id, out state\)') {
    throw "Behavior speed cleanup does not reject null or empty IDs before dictionary lookup."
}

foreach ($required in @(
    '45 m',
    'returns its investment but creates',
    'Version under test: 2.9.7',
    'SAS-SMOKE-30',
    'SAS-SMOKE-31',
    'SAS-SMOKE-16',
    'SAS-SMOKE-25',
    'SAS-SMOKE-32',
    'SAS-SMOKE-33',
    'SAS-SMOKE-43',
    'SAS-SMOKE-44',
    'SAS-SMOKE-46',
    'Guard Engagement Range',
    'retains them to 20 m',
    'Override Value to 5,000')) {
    if (!$readme.Contains($required) -and !$matrix.Contains($required)) {
        throw "Summon command documentation is missing: $required"
    }
}

Write-Host "Soul and Service idle, targeting, and Attack command contracts passed."
