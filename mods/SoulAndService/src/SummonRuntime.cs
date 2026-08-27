using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Awaken.Kandra;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.MVC.UI;
using Awaken.TG.MVC.UI.Events;
using Awaken.TG.Main.AI;
using Awaken.TG.Main.AI.Combat.Attachments;
using Awaken.TG.Main.AI.Grid;
using Awaken.TG.Main.AI.Fights.Projectiles;
using Awaken.TG.Main.AI.Idle;
using Awaken.TG.Main.AI.Movement.Controllers;
using Awaken.TG.Main.AI.Movement.RootMotions;
using Awaken.TG.Main.AI.Movement.States;
using Awaken.TG.Main.AI.SummonsAndAllies;
using Awaken.TG.Main.AI.Utils;
using Awaken.TG.Main.Animations.FSM.Npc.Base;
using Awaken.TG.Main.Animations.FSM.Npc.Machines;
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Interactions;
using Awaken.TG.Main.Heroes.Resting;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Stats.Tweaks;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Actions;
using Awaken.TG.Main.Locations.Attachments.Elements;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Locations.Views;
using Awaken.TG.Main.Utility;
using Awaken.TG.Main.VisualGraphUtils;
using FMODUnity;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SoulAndService
{
    internal static class SummonRuntime
    {
        private const float NativePatrolRadius = 7.5f;
        private const float NativeSummonCommandRange = 45.0f;
        private const float NativeSummonTargetAcquisitionRange = 44.0f;
        private const float NativeSummonTargetRetentionRange = 44.75f;
        private const float TargetRangeReleaseGraceSeconds = 0.85f;
        private const float BaseSummonAwarenessRange = 30.0f;
        private const float BaseHuntAwarenessRange = 30.0f;
        private const float BaseLostTargetGraceSeconds = 3.0f;
        private const float FormationCommandHoldSeconds = 0.45f;
        private const float RecallCommandHoldSeconds = 1.5f;
        private const float RaiseAllRadius = 30.0f;
        private const float BehaviorCommandHoldSeconds = 0.45f;
        private const float IndividualFormationCommandHoldSeconds = 0.45f;
        private const float RecentAttackerMemorySeconds = 6.0f;
        private const float GuardMeleeThreatRange = 8.0f;
        private const float DefaultGuardFormationDistance = 4.5f;
        private const float DefaultGuardEngagementRange = 15.0f;
        private const float GuardEngagementRetentionPadding = 5.0f;
        private const float GuardFormationRingSpacing = 1.5f;
        private const float GuardAnchorTolerance = 1.25f;
        private const float GuardLeaderMovingSpeed = 0.15f;
        private const float GuardAnchorRebaseDistance = 2.0f;
        private const float GuardIdleNoviceWanderRadius = 1.5f;
        private const float GuardIdleMasterWanderRadius = 0.75f;
        private const float GuardIdleNoviceMinimumStillSeconds = 12.0f;
        private const float GuardIdleNoviceMaximumStillSeconds = 22.0f;
        private const float GuardIdleMasterMinimumStillSeconds = 16.0f;
        private const float GuardIdleMasterMaximumStillSeconds = 26.0f;
        private const float GuardIdleMinimumLingerSeconds = 4.0f;
        private const float GuardIdleMaximumLingerSeconds = 7.0f;
        private const float GuardIdleReturnTimeoutSeconds = 10.0f;
        private const float GuardIdleHostAttemptCooldownSeconds = 1.0f;
        private const float GuardIdleMovementTolerance = 0.20f;
        private const float GuardIdleReturnTolerance = 0.25f;
        private const float DefaultBulwarkLocalEngagementRange = 4.0f;
        private const float DefaultBulwarkTargetRetentionRange = 6.0f;
        private const float DefaultBulwarkPlayerLeash = 8.0f;
        private const float BulwarkTargetCandidateRange = 10.0f;
        private const float BulwarkAnchorTolerance = 0.75f;
        private const float BulwarkAdvanceAnchorTolerance = 1.25f;
        private const float BulwarkAdvanceResumeDistance = 1.75f;
        private const float BulwarkAdvanceRunDistance = 0.75f;
        private const float BulwarkAdvanceAnchorUpdateDistance = 0.15f;
        private const float BulwarkAdvanceProgressDistance = 0.10f;
        private const float BulwarkAdvanceBlockedSeconds = 0.50f;
        private const float BulwarkAdvanceFallbackSeconds = 1.0f;
        private const float BulwarkAdvanceFallbackProbeRadius = 0.75f;
        private const float BulwarkAdvanceFallbackMinimumOffset = 0.35f;
        private const float BulwarkAdvanceFallbackCandidateSnapDistance = 0.50f;
        private const float BulwarkAdvanceMaximumAnchorSnapDistance = 1.5f;
        private const float DefaultBulwarkCloseGuardDistance = 3.5f;
        private const float DefaultBulwarkAdvanceDistance = 4.5f;
        private const float BulwarkFormationRingSpacing = 1.0f;
        private const int BulwarkFormationSlotsPerRing = 4;
        private const int BulwarkCloseGuardSlotsPerRing = 5;
        private const float BulwarkAdvancePredictionSeconds = 0.40f;
        private const float BulwarkCameraFacingHoldSeconds = 0.30f;
        private const float BulwarkCameraFacingCooldownSeconds = 0.45f;
        private const float BulwarkCameraFacingMinimumAngle = 30.0f;
        private const float BulwarkCameraFacingStabilityAngle = 12.0f;
        private const float BulwarkCatchUpStartDistance = 2.0f;
        private const float BulwarkCatchUpStopDistance = 1.0f;
        private const float BulwarkAdvanceCatchUpStartDistance = 0.75f;
        private const float BulwarkAdvanceCatchUpStopDistance = 0.35f;
        private const float BulwarkAdvanceCatchUpMinimumMultiplier = 1.60f;
        private const float BulwarkAdvanceMaximumMovementMultiplier = 3.0f;
        private const float DefaultHuntFormationDistance = 5.5f;
        private const float HuntFormationRingSpacing = 2.0f;
        private const int HuntFormationSlotsPerRing = 6;
        private const float HuntAnchorTolerance = 1.25f;
        private const float HuntAnchorRebaseDistance = 2.5f;
        private const float HuntIdleWanderRadius = 1.5f;
        private const float HuntIdleMinimumHeroDistance = 5.0f;
        private const float HuntIdleMinimumStillSeconds = 8.0f;
        private const float HuntIdleMaximumStillSeconds = 15.0f;
        private const float HuntIdleMinimumLingerSeconds = 3.0f;
        private const float HuntIdleMaximumLingerSeconds = 6.0f;
        private const float HuntIdleReturnTimeoutSeconds = 10.0f;
        private const float HuntIdleMovementTolerance = 0.25f;
        private const float HuntIdleReturnTolerance = 0.35f;
        private const int HuntMultipleWandererHostSize = 4;
        private const int HuntMaximumConcurrentWanderers = 2;
        private const float HuntPointMinimumDistance = 5.0f;
        private const float HuntPointMaximumNavSnapDistance = 2.0f;
        private const float HuntPointPreviewCacheSeconds = 0.12f;
        private const float HuntPointPreviewMoveDistance = 0.35f;
        private const float HuntAttackMoveSearchSeconds = 4.0f;
        private const float HuntAttackMoveMaximumTravelSeconds = 30.0f;
        private const float HuntAttackMoveArrivalRadius = 2.0f;
        private const float HuntAttackMoveRingSpacing = 2.0f;
        private const int HuntAttackMoveSlotsPerRing = 6;
        private const float HuntAttackMoveAnchorTolerance = 1.25f;
        private const float HuntAttackMoveAnchorUpdateDistance = 0.35f;
        private const float HuntAttackMoveBlockedSeconds = 0.75f;
        private const float HuntAttackMoveFallbackSeconds = 1.5f;
        private const float HuntAttackMoveFallbackDistance = 1.5f;
        private const float HuntAttackMoveMaximumAnchorSnapDistance = 3.0f;
        private const float HuntAttackMoveSweepRadius = 3.0f;
        private const float HuntAttackMoveSweepRepathSeconds = 1.6f;
        private const float HuntAttackMoveSweepArrivalTolerance = 0.65f;
        private const float HuntAttackMoveSweepRetrySeconds = 0.5f;
        private const float HuntAttackMoveSweepSpacing = 1.25f;
        private const int HuntAttackMoveMaximumSweepLegs = 2;
        private const int HuntAttackMoveSweepCandidateAttempts = 3;
        private const float ExplicitCommandProgressDistance = 0.35f;
        private const float ExplicitCommandEngagementDistance = 5.0f;
        private const float ExplicitCommandStallSeconds = 3.0f;
        private const float ExplicitCommandPathCheckIntervalSeconds = 1.0f;
        private const int ExplicitCommandMaximumUnreachableSamples = 3;
        private const float FormationRecoveryBlockedSeconds = 1.5f;
        private const float FormationRecoveryFallbackSeconds = 1.5f;
        private const float FormationRecoveryProbeDistance = 1.5f;
        private const float FormationRecoveryMaximumSnapDistance = 3.0f;
        private const float RecallTargetSuppressionSeconds = 3.0f;
        private const float RecallPlacementLifetimeSeconds = 10.0f;
        private const float RecallPlacementHeroMoveReleaseDistance = 2.0f;
        private const float RecallAnchorTolerance = 1.0f;
        private const float AutonomousLineOfSightCacheSeconds = 0.25f;
        private const float AutonomousLineOfSightRetentionSeconds = 5.0f;
        private const float BulwarkTargetCandidateCacheSeconds = 0.10f;
        private const float FormationPatrolAnchorUpdateDistance = 0.10f;
        private const float FormationLeaderTravelDeadZone = 1.5f;
        private const float FormationLeaderElasticOffset = 0.75f;
        private const float FormationLeaderMovementStartSeconds = 0.45f;
        private const float FormationLeaderMovementStopSpeed = 0.15f;
        private const float FormationLeaderSettleSeconds = 0.35f;
        private const float FormationFacingCommitDistance = 3.0f;
        private const float FormationFacingCommitSeconds = 0.75f;
        private const float FormationFacingStabilityAngle = 20.0f;
        private const float FormationFacingMaximumTurnDegreesPerSecond = 90.0f;
        private const float SteelAndBoneAwarenessCacheSeconds = 1.0f;
        private const float AutonomousTargetMinimumCommitmentSeconds = 1.75f;
        private const float BulwarkTargetMinimumCommitmentSeconds = 3.0f;
        private const float AutonomousTargetSwitchDistanceRatio = 0.80f;
        private const float AutonomousTargetAssignmentPenalty = 0.25f;
        private const float NoviceAiDecisionInterval = 0.75f;
        private const float AiDecisionIntervalRefreshSeconds = 0.50f;
        private const float AiDecisionIntervalJitterFraction = 0.05f;
        private const float ControllerRefreshSeconds = 0.10f;
        private const float FormationHostFallbackRefreshSeconds = 1.0f;
        private const int MaximumPathChecksPerFrame = 4;
        private const int MaximumLineOfSightRaycastsPerFrame = 8;
        private const float HeldSummonPruneIntervalSeconds = 1.0f;
        private const float AnimationWatchdogMovementSpeed = 0.10f;
        private const float AnimationWatchdogRecoveryCooldownSeconds = 2.0f;
        private const int AnimationWatchdogFailureSamples = 3;
        private const float ControlDiagnosticMinimumIntervalSeconds = 0.25f;
        private const float TransientStatePruneIntervalSeconds = 0.25f;
        private const float StandardCommandFeedbackSeconds = 0.675f;
        private const float ExtendedCommandFeedbackSeconds = 1.35f;
        private const float AttackCommandAimRadius = 0.25f;
        private const float AttackCommandFocusGraceSeconds = 0.30f;
        private const float FormationCommandMinimumAimRadius = 0.35f;
        private const float FormationCommandMaximumAimRadius = 1.25f;
        private const float HeldSummonCombatLeash = 8.0f;
        private const float SteelAndBoneTransferFraction = 0.80f;
        private const float UpkeepIntervalSeconds = 1.0f;
        private const float SwarmDurationSeconds = 5.0f;
        private const float SwarmMovementMultiplier = 1.25f;
        private const float SwarmFirstHitMultiplier = 1.25f;
        private const float MaximumCommandMovementMultiplier = 1.50f;
        private const float HuntBehaviorMovementMultiplier = 1.10f;
        private const float BulwarkLeaderMovingSpeed = 0.15f;
        private const float BulwarkLeaderRunSpeed = 3.0f;
        private const string SteelAndBonePluginGuid = "ks.tgfoa.steel-and-bone";
        private const string SteelAndBoneAwarenessApiTypeName =
            "SteelAndBone.SteelAndBoneAwarenessApi";
        private const string BattlecryVoiceTunerPluginGuid =
            "ks.tgfoa.battlecry-voice-tuner";
        private const string BattlecryVoiceTunerApiTypeName =
            "BattlecryVoiceTuner.BattlecryVoiceTunerApi";
        private const string SummonAttackCommandId = "summon_attack";
        private const string SummonHoldCommandId = "summon_hold";
        private const string SummonFollowCommandId = "summon_follow";
        private const string SummonRecallCommandId = "summon_recall";
        private const string SummonRaiseAllCommandId = "summon_raiseall";
        private const string SummonGuardCommandId = "summon_guard";
        private const string SummonBulwarkCommandId = "summon_bulwark";
        private const string SummonHuntCommandId = "summon_hunt";

        private sealed class CollisionPair
        {
            internal Collider SummonCollider;
            internal Collider HeroCollider;
        }

        private enum TakeAllCommandMode
        {
            None,
            Formation,
            RaiseAll
        }

        private sealed class CollisionState
        {
            internal GameObject AlivePrefab;
            internal Collider HeroCollider;
            internal readonly List<CollisionPair> Pairs =
                new List<CollisionPair>();
            internal readonly HashSet<int> SummonColliderIds =
                new HashSet<int>();
        }

        private sealed class ScalingTweaks
        {
            internal StatTweak Melee;
            internal StatTweak Ranged;
            internal StatTweak Magic;
            internal StatTweak Health;
        }

        private sealed class AwarenessTargetRecord
        {
            internal string SummonId;
            internal NpcElement Summon;
            internal NpcElement Target;
            internal float LastSeenAt;
            internal float SelectedAt;
        }

        private sealed class OwnedTargetOverrideState
        {
            internal NpcElement Target;
            internal SoulAndServiceTargetOverride Element;
            internal int Priority;
            internal bool Explicit;
            internal bool IgnoreHeroLeash;
            internal float OutOfRangeSince = -1.0f;
        }

        private sealed class SoulAndServiceTargetOverride
            : TargetOverrideElement
        {
            private readonly NpcHeroSummon _summon;

            internal SoulAndServiceTargetOverride(
                NpcHeroSummon summon,
                NpcElement target,
                int priority)
                : base(target, priority)
            {
                _summon = summon;
            }

            public override bool TemporarilyDisabled =>
                _summon == null
                || _summon.ParentModel == null
                || _summon.ParentModel.HasBeenDiscarded
                || !IsWithinNativeSummonTargetRetentionRange(
                    Hero.Current,
                    _target as NpcElement);
        }

        private struct AutonomousTargetCandidate
        {
            internal NpcElement Target;
            internal string TargetId;
            internal int Priority;
            internal float DistanceSqr;
            internal float SelectionScore;
            internal bool Committed;
        }

        private sealed class AutonomousTargetCandidateComparer
            : IComparer<AutonomousTargetCandidate>
        {
            internal static readonly AutonomousTargetCandidateComparer Instance =
                new AutonomousTargetCandidateComparer();

            public int Compare(
                AutonomousTargetCandidate left,
                AutonomousTargetCandidate right)
            {
                int committed = right.Committed.CompareTo(left.Committed);
                if (committed != 0)
                {
                    return committed;
                }
                int priority = left.Priority.CompareTo(right.Priority);
                if (priority != 0)
                {
                    return priority;
                }
                int score = left.SelectionScore.CompareTo(right.SelectionScore);
                return score != 0
                    ? score
                    : StringComparer.Ordinal.Compare(left.TargetId, right.TargetId);
            }
        }

        private sealed class RecentAttackerRecord
        {
            internal NpcElement Target;
            internal float ExpiresAt;
        }

        private sealed class PendingTeleportVfx
        {
            internal Vector3 Origin;
            internal float RequestedAt;
            internal int OutstandingRequests;
        }

        private sealed class PendingRecallPlacement
        {
            internal Vector3 Position;
            internal Vector3 HeroOrigin;
            internal float ExpiresAt;
            internal bool HasReservedDestination;
            internal bool DestinationConsumed;
            internal bool LocomotionRestored;
        }

        private sealed class AutonomousLineOfSightRecord
        {
            internal Vector3 Origin;
            internal Vector3 FocusPoint;
            internal bool Visible;
            internal float ExpiresAt;
        }

        private struct AutonomousLineOfSightKey
            : IEquatable<AutonomousLineOfSightKey>
        {
            internal string ObserverId;
            internal string TargetId;

            public bool Equals(AutonomousLineOfSightKey other)
            {
                return string.Equals(
                        ObserverId,
                        other.ObserverId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        TargetId,
                        other.TargetId,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is AutonomousLineOfSightKey
                    && Equals((AutonomousLineOfSightKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((ObserverId == null
                            ? 0
                            : StringComparer.Ordinal.GetHashCode(ObserverId)) * 397)
                        ^ (TargetId == null
                            ? 0
                            : StringComparer.Ordinal.GetHashCode(TargetId));
                }
            }
        }

        private sealed class HeldSummonState
        {
            internal Vector3 Anchor;
        }

        private sealed class IdleExcursionState
        {
            internal Vector3 FormationAnchor;
            internal Vector3 HeroOrigin;
            internal Vector3 WanderDestination;
            internal float NextWanderAt;
            internal float WanderEndsAt;
            internal float ReturnEndsAt;
            internal bool HasAnchor;
            internal bool Wandering;
            internal bool Returning;
        }

        private enum HuntAttackMovePhase
        {
            Idle,
            Travelling,
            Searching
        }

        private sealed class HuntAttackMoveSearchState
        {
            internal bool Arrived;
            internal bool HasSweepDestination;
            internal Vector3 SweepDestination;
            internal float NextSweepAt;
            internal int SweepLegs;
        }

        private sealed class ExplicitCommandPursuitState
        {
            internal NpcElement Target;
            internal Vector3 LastPosition;
            internal float LastDistance;
            internal float LastProgressAt;
            internal float NextPathCheckAt;
            internal int UnreachableSamples;
        }

        private sealed class FormationCommandViewCache
        {
            internal GameObject ViewObject;
            internal Renderer[] Renderers;
        }

        private sealed class FormationHostIdComparer
            : IComparer<NpcHeroSummon>
        {
            internal static readonly FormationHostIdComparer Instance =
                new FormationHostIdComparer();

            public int Compare(NpcHeroSummon left, NpcHeroSummon right)
            {
                string leftId = left == null ? string.Empty : ((Model)left).ID;
                string rightId = right == null ? string.Empty : ((Model)right).ID;
                return StringComparer.Ordinal.Compare(leftId, rightId);
            }
        }

        private sealed class SwarmState
        {
            internal NpcElement Target;
            internal float ExpiresAt;
            internal float MovementMultiplier;
            internal StatTweak MovementTweak;
        }

        private sealed class BehaviorSpeedState
        {
            internal float Multiplier;
            internal StatTweak MovementTweak;
        }

        private sealed class CatchUpSpeedState
        {
            internal float Multiplier;
            internal StatTweak MovementTweak;
        }

        private sealed class SpawnReadinessState
        {
            internal NpcHeroSummon Summon;
            internal float EarliestReleaseAt;
        }

        private sealed class AnimationWatchdogState
        {
            internal int FailedMovingSamples;
            internal float NextRecoveryAt;
            internal bool HasPositionSample;
            internal Vector3 LastPosition;
            internal float LastPositionSampleAt;
            internal object LastAnimationState;
            internal double LastAnimationTime;
        }

        private sealed class EmpowermentState
        {
            internal bool IsEmpowered;
            internal float CombatMultiplier;
            internal float SizeMultiplier;
            internal float MovementMultiplier;
            internal StatTweak MovementTweak;
            internal Transform VisualMarker;
            internal Transform VisualRoot;
            internal Vector3 OriginalLocalScale;
            internal Vector3 OriginalLocalPosition;
            internal float NextVisualRootLookupTime;
            internal int VisualDiagnosticCount;
            internal bool VisualRootFailureLogged;
        }

        private sealed class EmpowermentVisualEnforcer : MonoBehaviour
        {
            internal NpcController Controller;

            private void LateUpdate()
            {
                if (!ApplyLateEmpowermentVisual(Controller))
                {
                    Destroy(this);
                }
            }
        }

        private static readonly Dictionary<string, CollisionState> CollisionPairs =
            new Dictionary<string, CollisionState>();
        private static readonly List<Collider> CollisionColliderBuffer =
            new List<Collider>();
        private static readonly Dictionary<string, CatchUpSpeedState> SpeedTweaks =
            new Dictionary<string, CatchUpSpeedState>();
        private static readonly Dictionary<string, BehaviorSpeedState>
            BehaviorSpeedStates =
                new Dictionary<string, BehaviorSpeedState>();
        private static readonly Dictionary<string, ScalingTweaks> InvocationTweaks =
            new Dictionary<string, ScalingTweaks>();
        private static readonly HashSet<string> StabilizedPatrols =
            new HashSet<string>();
        private static readonly Dictionary<string, AwarenessTargetRecord>
            AwarenessTargets =
                new Dictionary<string, AwarenessTargetRecord>();
        private static readonly Dictionary<string, RecentAttackerRecord>
            RecentAttackers =
                new Dictionary<string, RecentAttackerRecord>();
        private static readonly Dictionary<string, NpcElement>
            AutonomousTargetOverrides =
                new Dictionary<string, NpcElement>();
        private static readonly Dictionary<string, OwnedTargetOverrideState>
            OwnedTargetOverrides =
                new Dictionary<string, OwnedTargetOverrideState>();
        private static readonly List<AutonomousTargetCandidate>
            AutonomousTargetCandidateBuffer =
                new List<AutonomousTargetCandidate>();
        private static readonly Dictionary<string, PendingTeleportVfx>
            PendingTeleportVfxBySummon =
                new Dictionary<string, PendingTeleportVfx>();
        private static readonly Dictionary<string, PendingRecallPlacement>
            PendingRecallPlacements =
                new Dictionary<string, PendingRecallPlacement>();
        private static readonly Dictionary<string, float>
            RecallTargetSuppressionUntil =
                new Dictionary<string, float>();
        private static readonly Dictionary<AutonomousLineOfSightKey,
            AutonomousLineOfSightRecord>
            AutonomousLineOfSightByTarget =
                new Dictionary<AutonomousLineOfSightKey,
                    AutonomousLineOfSightRecord>();
        private static readonly List<AutonomousLineOfSightKey>
            AutonomousLineOfSightRemovalBuffer =
                new List<AutonomousLineOfSightKey>();
        private static readonly Dictionary<string, NpcElement>
            ExplicitCommandTargets =
                new Dictionary<string, NpcElement>();
        private static readonly Dictionary<string, ExplicitCommandPursuitState>
            ExplicitCommandPursuitStates =
                new Dictionary<string, ExplicitCommandPursuitState>();
        private static readonly Dictionary<string, HeldSummonState>
            HeldSummons =
                new Dictionary<string, HeldSummonState>();
        private static readonly HashSet<string> ActiveHeldSummonIds =
            new HashSet<string>();
        private static readonly List<string> HeldSummonRemovalBuffer =
            new List<string>();
        private static readonly List<string> StateRemovalBuffer =
            new List<string>();
        private static readonly Dictionary<string, IdleExcursionState>
            GuardIdleStates =
                new Dictionary<string, IdleExcursionState>();
        private static readonly Dictionary<string, IdleExcursionState>
            HuntIdleStates =
                new Dictionary<string, IdleExcursionState>();
        private static readonly HashSet<string> HuntIdleMoverIds =
            new HashSet<string>();
        private static readonly Dictionary<string, NpcHeroSummon>
            HuntAttackMoveParticipants =
                new Dictionary<string, NpcHeroSummon>();
        private static readonly Dictionary<string, HuntAttackMoveSearchState>
            HuntAttackMoveSearchStates =
                new Dictionary<string, HuntAttackMoveSearchState>();
        private static readonly List<string> HuntAttackMoveRemovalBuffer =
            new List<string>();
        private static readonly RaycastHit[] HuntPointRaycastHits =
            new RaycastHit[16];
        private static readonly Dictionary<string, FormationCommandViewCache>
            FormationCommandViewCaches =
                new Dictionary<string, FormationCommandViewCache>();
        private static readonly Dictionary<string, SwarmState> SwarmStates =
            new Dictionary<string, SwarmState>();
        private static readonly Dictionary<string, EmpowermentState>
            EmpowermentStates =
                new Dictionary<string, EmpowermentState>();
        private static readonly Dictionary<int, float>
            LocomotionPlaybackMultipliers =
                new Dictionary<int, float>();
        private static readonly Dictionary<string, float>
            NextControllerRefreshBySummon =
                new Dictionary<string, float>();
        private static readonly Dictionary<string, SpawnReadinessState>
            SpawnReadinessBySummon =
                new Dictionary<string, SpawnReadinessState>();
        private static readonly Dictionary<string, AnimationWatchdogState>
            AnimationWatchdogsBySummon =
                new Dictionary<string, AnimationWatchdogState>();
        private static readonly Dictionary<string, string>
            LastControlDiagnosticBySummon =
                new Dictionary<string, string>();
        private static readonly Dictionary<string, float>
            NextControlDiagnosticBySummon =
                new Dictionary<string, float>();
        private static readonly RaycastHit[] FormationCommandRaycastHits =
            new RaycastHit[32];
        private static readonly RaycastHit[] AttackCommandRaycastHits =
            new RaycastHit[32];
        private static readonly RaycastHit[] AutonomousTargetRaycastHits =
            new RaycastHit[32];
        private static float _nextCollisionRefreshTime;
        private static float _nextTransientStatePruneTime;
        private static float _nextAiDecisionIntervalRefreshTime;
        private static float _cachedAiDecisionInterval = NoviceAiDecisionInterval;
        private static Hero _formationHostCacheHero;
        private static NpcHeroSummon[] _formationHostCache =
            new NpcHeroSummon[0];
        private static readonly List<NpcHeroSummon> FormationHostBuildBuffer =
            new List<NpcHeroSummon>();
        private static float _formationHostCacheExpiresAt;
        private static float _nextHeldSummonPruneTime;
        private static int _hostCombatCacheFrame = -1;
        private static Hero _hostCombatCacheHero;
        private static bool _hostCombatCacheValue;
        private static int _pathWorkBudgetFrame = -1;
        private static int _pathChecksThisFrame;
        private static int _lineOfSightBudgetFrame = -1;
        private static int _lineOfSightRaycastsThisFrame;
        private static MethodInfo _steelAndBoneSightMultiplierMethod;
        private static MethodInfo _steelAndBoneAggroMultiplierMethod;
        private static bool _steelAndBoneAwarenessUnavailable;
        private static readonly FieldInfo RestWarningTextField =
            AccessTools.Field(typeof(VRestPopupUI), "warningText");
        private static float _nextSteelAndBoneAwarenessRefreshAt;
        private static float _cachedSteelAndBoneSightMultiplier = 1.0f;
        private static float _cachedSteelAndBoneAggroMultiplier = 1.0f;
        private static NpcGrid _bulwarkTargetCandidateGrid;
        private static Vector3 _bulwarkTargetCandidateCenter;
        private static float _bulwarkTargetCandidateExpiresAt;
        private static readonly List<NpcElement> BulwarkTargetCandidates =
            new List<NpcElement>();
        private static MethodInfo _battlecryTryPlayCommandMethod;
        private static bool _battlecryCommandApiUnavailable;
        private static SummonCommandInteractable _commandInteractable;
        private static NpcElement _recentAttackCommandTarget;
        private static GameObject _recentAttackCommandViewObject;
        private static float _recentAttackCommandFocusedAt;
        private static string _lastFormationFocusDiagnostic = string.Empty;
        private static bool _takeAllItemsHeld;
        private static TakeAllCommandMode _takeAllCommandMode;
        private static bool _takeAllCommandCancelled;
        private static bool _formationCommandArmedForRelease;
        private static bool _recallCommandAttemptedForHold;
        private static float _takeAllItemsPressedAt;
        private static bool _behaviorCommandHeld;
        private static bool _behaviorCommandAttemptedForHold;
        private static float _behaviorCommandPressedAt;
        private static bool _individualFormationCommandHeld;
        private static bool _individualFormationCommandResolved;
        private static float _individualFormationCommandPressedAt;
        private static NpcHeroSummon _individualFormationCommandSummon;
        private static SummonCommandState _individualFormationCommandState;
        private static float _commandFeedbackEndsAt;
        private static bool _commandFeedbackModifierReleased;
        private static SummonCommandState _lastCommandState;
        private static float _lastCommandPulseSeconds =
            StandardCommandFeedbackSeconds;
        private static int _commandSequence;
        private static string _guardIdleMoverId;
        private static float _nextIdleHostAttemptAt;
        private static bool _hasBulwarkForward;
        private static Vector3 _bulwarkForward = Vector3.forward;
        private static Hero _bulwarkFacingHero;
        private static int _bulwarkFacingFrame = -1;
        private static bool _hasBulwarkViewCandidate;
        private static Vector3 _bulwarkViewCandidate = Vector3.forward;
        private static float _bulwarkViewCandidateSince = -1.0f;
        private static float _bulwarkFacingCooldownUntil;
        private static Hero _bulwarkAdvanceHero;
        private static int _bulwarkAdvanceStateFrame = -1;
        private static bool _bulwarkAdvanceInputWasHeld;
        private static bool _bulwarkAdvanceWasActive;
        private static float _bulwarkAdvanceReleasedUntil;
        private static HuntAttackMovePhase _huntAttackMovePhase;
        private static Hero _huntAttackMoveHero;
        private static Vector3 _huntAttackMoveOrigin;
        private static Vector3 _huntAttackMoveDestination;
        private static Vector3 _huntAttackMoveDirection = Vector3.forward;
        private static float _huntAttackMoveStartedAt;
        private static float _huntAttackMoveSearchUntil;
        private static bool _huntPointTapArmed;
        private static Vector3 _huntPointArmedDestination;
        private static Collider _huntPointPreviewCollider;
        private static Vector3 _huntPointPreviewHit;
        private static Vector3 _huntPointPreviewDestination;
        private static float _huntPointPreviewExpiresAt;
        private static bool _huntPointPreviewValid;
        private static bool _hasGuardForward;
        private static Vector3 _guardForward = Vector3.forward;
        private static Vector3 _guardFacingDesired = Vector3.forward;
        private static Vector3 _guardFacingCandidate = Vector3.forward;
        private static float _guardFacingCandidateStartedAt = -1.0f;
        private static int _guardFacingUpdateFrame = -1;
        private static Hero _formationFacingHero;
        private static int _formationLeaderMotionFrame = -1;
        private static Hero _formationLeaderMotionHero;
        private static Vector3 _formationLeaderAnchor;
        private static bool _hasFormationLeaderAnchor;
        private static bool _formationLeaderMoving;
        private static Vector3 _formationLeaderMovementOrigin;
        private static float _formationLeaderMovementStartedAt = -1.0f;
        private static float _formationLeaderStoppedAt = -1.0f;
        private static float _upkeepElapsed;
        private static int _lastUpkeepHostSize = -1;
        private static float _lastUpkeepPercentPerMinute = -1.0f;

        private static readonly FieldInfo PatrolField =
            AccessTools.Field(typeof(NpcAlly), "_patrol");
        private static readonly MethodInfo TeleportToAllyMethod =
            AccessTools.Method(typeof(NpcAlly), "TeleportToAlly");
        private static readonly FieldInfo NpcDetectionField =
            AccessTools.Field(typeof(VCHeroRaycaster), "npcDetection");
        private static readonly Type CharacterLocationsType =
            RequireNestedType(typeof(CharacterLimitedLocations), "CharacterLocations");
        private static readonly FieldInfo LimitedLocationsField =
            AccessTools.Field(CharacterLocationsType, "_locations");
        private static readonly FieldInfo OldestIndexField =
            AccessTools.Field(CharacterLocationsType, "_oldestIndex");
        private static readonly FieldInfo EmptyCountField =
            AccessTools.Field(CharacterLocationsType, "_emptyCount");
        private static readonly FieldInfo MovementPreventedField =
            AccessTools.Field(typeof(NpcAlly), "_movementPrevented");

        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "UnityUpdate"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeNpcAllyUnityUpdate)),
                transpiler: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(TranspileAiTick)));
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "StayCloseToAlly"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeStayCloseToAlly)));
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "FindTarget"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeFindTarget)),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterFindTarget)));
            harmony.Patch(
                RequireMethod(
                    typeof(EnemyBaseClass),
                    "UpdateCombatSlotStatus",
                    new[] { typeof(ICharacter) }),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterCombatSlotStatusUpdate)));
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "OnTeleportPathCalculated"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterTeleportPathCalculated)));
            harmony.Patch(
                RequireMethod(
                    typeof(NpcTeleporter),
                    "Teleport",
                    new[]
                    {
                        typeof(NpcElement),
                        typeof(TeleportDestination),
                        typeof(TeleportContext)
                    }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeNpcTeleport)));
            MethodInfo preventMovement = RequireMethod(
                typeof(NpcHeroSummon),
                "PreventMovement");
            AsyncStateMachineAttribute asyncAttribute =
                preventMovement.GetCustomAttribute<AsyncStateMachineAttribute>();
            if (asyncAttribute == null)
            {
                throw new MissingMethodException(
                    "NpcHeroSummon.PreventMovement async state machine was not found.");
            }
            harmony.Patch(
                RequireMethod(asyncAttribute.StateMachineType, "MoveNext"),
                transpiler: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(TranspileSpawnRecovery)));

            MethodInfo summonInit = RequireMethod(typeof(NpcHeroSummon), "Init");
            harmony.Patch(
                summonInit,
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterSummonInit)));
            harmony.Patch(
                RequireMethod(
                    CharacterLocationsType,
                    "AddLocation",
                    new[] { typeof(ICharacterLimitedLocation) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeAddLimitedLocation)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "ToggleWalkThroughColliders"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterToggleWalkThroughColliders)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "OnDiscard"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterSummonDiscard)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "get_DestroyOnRest"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeGetDestroyOnRest)));
            harmony.Patch(
                RequireMethod(typeof(RestPopupUI), "SkipWeatherTime"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterRestTimeSkipped)));
            harmony.Patch(
                RequireMethod(typeof(VRestPopupUI), "Refresh"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterRestPreviewRefreshed)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "LimitForCharacter"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterGetSummonLimit)));

            harmony.Patch(
                RequireMethod(typeof(NpcController), "Update"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterNpcControllerUpdate)));
            harmony.Patch(
                RequireMethod(
                    typeof(RootMotion),
                    "UpdateAnimator",
                    new[] { typeof(Vector2), typeof(float) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeRootMotionUpdateAnimator)));
            harmony.Patch(
                RequireMethod(typeof(HealthElement), "ApplyDamageModifiers"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterApplyDamageModifiers)));

            harmony.Patch(
                RequireMethod(
                    typeof(DamageDealingProjectile),
                    "CheckCastResult",
                    new[] { typeof(HitResult) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeProjectileHitResult)));
            harmony.Patch(
                RequireMethod(
                    typeof(DamageDealingProjectile),
                    "CheckCastResult",
                    new[] { typeof(Collider) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeProjectileCollider)));
            harmony.Patch(
                RequireMethod(
                    typeof(CharacterMagicGauntlet),
                    "OnBoxCastHit"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeMagicGauntletHit)));
            harmony.Patch(
                RequireMethod(
                    typeof(VHeroKeys),
                    nameof(VHeroKeys.Handle),
                    new[] { typeof(UIEvent) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeHeroKeysHandle)));
            MethodInfo heroKeyBindings = AccessTools.PropertyGetter(
                typeof(VHeroKeys),
                nameof(VHeroKeys.PlayerKeyBindings));
            if (heroKeyBindings == null)
            {
                throw new MissingMethodException(
                    "Awaken.TG.Main.Heroes.VHeroKeys.PlayerKeyBindings");
            }
            harmony.Patch(
                heroKeyBindings,
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                nameof(AfterHeroKeyBindings)));
        }

        internal static void Update()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null)
            {
                return;
            }

            if (!plugin.IsEnabled
                || plugin.FormationCommands == null
                || !plugin.FormationCommands.Value
                || !HasIndividualFormationControl())
            {
                ReleaseAllHeldSummons();
                ResetTakeAllItemsHold();
                ResetIndividualFormationCommandHold();
            }
            UpdateIndividualFormationCommandHold();
            UpdateBehaviorCommandHold();
            UpdateTakeAllItemsHold();
            UpdateCommandOverride(plugin);
            PruneHeldSummons();

            if (!plugin.IsEnabled)
            {
                ClearRecentAttackCommandTarget();
                ClearAllServantPowerStates();
                RestoreAllCollisionPairs();
                RemoveAllAwarenessTargets();
                ClearAllOwnedTargetOverrides();
                ExplicitCommandTargets.Clear();
                ExplicitCommandPursuitStates.Clear();
                RecentAttackers.Clear();
                PendingTeleportVfxBySummon.Clear();
                PendingRecallPlacements.Clear();
                RecallTargetSuppressionUntil.Clear();
                AutonomousLineOfSightByTarget.Clear();
                GuardIdleStates.Clear();
                HuntIdleStates.Clear();
                HuntIdleMoverIds.Clear();
                ResetHuntAttackMove();
                SummonFormationCoordinator.Reset();
                LastControlDiagnosticBySummon.Clear();
                NextControlDiagnosticBySummon.Clear();
                ReleaseAllSpawnReadinessLocks();
                AnimationWatchdogsBySummon.Clear();
                _guardIdleMoverId = null;
                _nextIdleHostAttemptAt = 0.0f;
                ResetBulwarkAdvanceState();
                ResetBulwarkFacingState();
                ResetFormationLeaderMotion();
                ResetIndividualFormationCommandHold();
                ResetBehaviorCommandHold();
                return;
            }

            UpdateFormationLeaderMotion(Hero.Current);
            UpdateBulwarkAdvanceState(plugin, Hero.Current);
            UpdateBulwarkFacing(Hero.Current);
            UpdateHuntAttackMove(plugin);

            if (Time.unscaledTime >= _nextTransientStatePruneTime)
            {
                _nextTransientStatePruneTime = Time.unscaledTime
                    + TransientStatePruneIntervalSeconds;
                PruneRecentAttackers();
            }
            UpdateSwarmStates();
            UpdateServantUpkeep(plugin);

            if (!plugin.SummonPassThrough.Value)
            {
                RestoreAllCollisionPairs();
                return;
            }

            if (Time.unscaledTime < _nextCollisionRefreshTime)
            {
                return;
            }
            _nextCollisionRefreshTime = Time.unscaledTime + 1.0f;
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                ApplyPlayerPassThrough(summon);
            }
        }

        internal static void Shutdown()
        {
            ClearCommandOverride();
            ClearRecentAttackCommandTarget();
            RestoreAllCollisionPairs();
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                RestorePatrolRadius(summon);
            }
            StabilizedPatrols.Clear();
            RemoveAllAwarenessTargets();
            ClearAllOwnedTargetOverrides();
            ExplicitCommandTargets.Clear();
            ExplicitCommandPursuitStates.Clear();
            RecentAttackers.Clear();
            PendingTeleportVfxBySummon.Clear();
            PendingRecallPlacements.Clear();
            RecallTargetSuppressionUntil.Clear();
            AutonomousLineOfSightByTarget.Clear();
            HeldSummons.Clear();
            GuardIdleStates.Clear();
            HuntIdleStates.Clear();
            HuntIdleMoverIds.Clear();
            ResetHuntAttackMove();
            SummonFormationCoordinator.Reset();
            _bulwarkTargetCandidateExpiresAt = 0.0f;
            BulwarkTargetCandidates.Clear();
            _guardIdleMoverId = null;
            _nextIdleHostAttemptAt = 0.0f;
            ResetBulwarkAdvanceState();
            ResetBulwarkFacingState();
            _hasGuardForward = false;
            _guardForward = Vector3.forward;
            _formationFacingHero = null;
            ResetFormationLeaderMotion();
            FormationCommandViewCaches.Clear();
            NextControllerRefreshBySummon.Clear();
            ReleaseAllSpawnReadinessLocks();
            AnimationWatchdogsBySummon.Clear();
            LastControlDiagnosticBySummon.Clear();
            NextControlDiagnosticBySummon.Clear();
            ClearAllServantPowerStates();
            ResetTakeAllItemsHold();
            ResetIndividualFormationCommandHold();
            ResetBehaviorCommandHold();
            foreach (CatchUpSpeedState state in SpeedTweaks.Values.ToArray())
            {
                DiscardTweak(state.MovementTweak);
            }
            SpeedTweaks.Clear();
            foreach (BehaviorSpeedState state in BehaviorSpeedStates.Values.ToArray())
            {
                DiscardTweak(state.MovementTweak);
            }
            BehaviorSpeedStates.Clear();
            foreach (ScalingTweaks tweaks in InvocationTweaks.Values.ToArray())
            {
                DiscardScalingTweaks(tweaks);
            }
            InvocationTweaks.Clear();
            _battlecryTryPlayCommandMethod = null;
            _battlecryCommandApiUnavailable = false;
            _upkeepElapsed = 0.0f;
            _lastUpkeepHostSize = -1;
            _lastUpkeepPercentPerMinute = -1.0f;
            _commandFeedbackEndsAt = 0.0f;
            _nextTransientStatePruneTime = 0.0f;
            _nextAiDecisionIntervalRefreshTime = 0.0f;
            _cachedAiDecisionInterval = NoviceAiDecisionInterval;
            _formationHostCacheHero = null;
            _formationHostCache = new NpcHeroSummon[0];
            _formationHostCacheExpiresAt = 0.0f;
            _nextHeldSummonPruneTime = 0.0f;
            ActiveHeldSummonIds.Clear();
            HeldSummonRemovalBuffer.Clear();
            _hostCombatCacheFrame = -1;
            _hostCombatCacheHero = null;
            _hostCombatCacheValue = false;
            _nextSteelAndBoneAwarenessRefreshAt = 0.0f;
            _cachedSteelAndBoneSightMultiplier = 1.0f;
            _cachedSteelAndBoneAggroMultiplier = 1.0f;
            _bulwarkTargetCandidateGrid = null;
            _bulwarkTargetCandidateExpiresAt = 0.0f;
            BulwarkTargetCandidates.Clear();
        }

        internal static bool TryEmpowerSummon(
            NpcHeroSummon summon,
            float multiplier)
        {
            Hero hero = Hero.Current;
            if (!IsOwnedSummon(summon, hero))
            {
                return false;
            }

            string id = ((Model)summon).ID;
            EmpowermentState state;
            if (EmpowermentStates.TryGetValue(id, out state)
                && state.IsEmpowered)
            {
                return false;
            }

            NpcElement npc = summon.ParentModel;
            if (state == null)
            {
                state = CreateServantPowerState();
            }
            state.IsEmpowered = true;
            state.CombatMultiplier = Mathf.Clamp(multiplier, 1.20f, 1.50f);
            state.SizeMultiplier = Mathf.Lerp(
                1.10f,
                1.30f,
                Mathf.InverseLerp(
                    1.20f,
                    1.50f,
                    state.CombatMultiplier));
            state.MovementMultiplier = Mathf.Sqrt(Mathf.Clamp(
                state.CombatMultiplier - 0.10f,
                1.10f,
                1.40f));
            if (npc.CharacterStats != null
                && npc.CharacterStats.MovementSpeedMultiplier != null)
            {
                state.MovementTweak = StatTweak.Multi(
                    npc.CharacterStats.MovementSpeedMultiplier,
                    state.MovementMultiplier,
                    null,
                    npc);
                ((Model)state.MovementTweak).MarkedNotSaved = true;
            }
            EmpowermentStates[id] = state;
            SoulforgedRuntime.SaveEmpowerment(summon, state.CombatMultiplier);
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (npc.Controller != null)
            {
                UpdateCatchUpSpeed(npc.Controller, plugin, summon, id);
                UpdateBehaviorSpeed(npc.Controller, plugin, summon, id);
            }
            EnsureEmpowermentVisualEnforcer(npc.Controller);
            ApplyEmpowermentVisual(npc.Controller, state);

            if (plugin != null)
            {
                plugin.LogDiagnostic(
                    "Empowered summon " + id + " at "
                    + state.SizeMultiplier.ToString("0.###")
                    + "x size and "
                    + state.CombatMultiplier.ToString("0.###")
                    + "x outgoing damage and resistance.");
            }
            return true;
        }

        internal static bool IsEmpoweredSummon(NpcHeroSummon summon)
        {
            return summon != null
                && TryGetEmpowermentState(
                    ((Model)summon).ID,
                    out EmpowermentState state)
                && state.IsEmpowered;
        }

        internal static float GetEmpowermentSizeMultiplier(
            NpcHeroSummon summon)
        {
            if (summon == null)
            {
                return 1.0f;
            }
            EmpowermentState state;
            return EmpowermentStates.TryGetValue(((Model)summon).ID, out state)
                && state.IsEmpowered
                ? state.SizeMultiplier
                : 1.0f;
        }

        internal static float GetEmpowermentCombatMultiplier(string summonId)
        {
            if (string.IsNullOrEmpty(summonId))
            {
                return 1.0f;
            }
            EmpowermentState state;
            return EmpowermentStates.TryGetValue(summonId, out state)
                && state.IsEmpowered
                ? state.CombatMultiplier
                : 1.0f;
        }

        private static bool TryGetEmpowermentState(
            string id,
            out EmpowermentState state)
        {
            return EmpowermentStates.TryGetValue(id, out state);
        }

        private static EmpowermentState CreateServantPowerState()
        {
            return new EmpowermentState
            {
                CombatMultiplier = 1.0f,
                SizeMultiplier = 1.0f,
                MovementMultiplier = 1.0f
            };
        }

        internal static void RefreshSoulforgedPresentation(
            NpcHeroSummon summon)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return;
            }
            string id = ((Model)summon).ID;
            EmpowermentState state;
            if (!EmpowermentStates.TryGetValue(id, out state))
            {
                if (SoulforgedRuntime.GetEffectiveRank(id) <= 0)
                {
                    return;
                }
                state = CreateServantPowerState();
                EmpowermentStates[id] = state;
            }
            NpcController controller = summon.ParentModel.Controller;
            EnsureEmpowermentVisualEnforcer(controller);
            ApplyEmpowermentVisual(controller, state);
        }

        private static void UpdateServantUpkeep(SoulAndServicePlugin plugin)
        {
            _upkeepElapsed += Math.Max(0.0f, Time.deltaTime);
            if (_upkeepElapsed < UpkeepIntervalSeconds)
            {
                return;
            }

            float elapsed = Math.Min(_upkeepElapsed, 2.0f);
            _upkeepElapsed = 0.0f;
            Hero hero = Hero.Current;
            NpcHeroSummon[] summons = GetFormationHost(hero);
            float percentPerMinute = GetUpkeepPercentPerMinute(
                summons.Length,
                SoulProgressionRuntime.GetNecromanticPower());
            if (summons.Length != _lastUpkeepHostSize
                || Math.Abs(percentPerMinute - _lastUpkeepPercentPerMinute)
                    > 0.0001f)
            {
                plugin.LogDiagnostic(
                    "Necromantic upkeep: servants=" + summons.Length
                    + "; drain="
                    + percentPerMinute.ToString("0.###")
                    + "% max health per minute.");
                _lastUpkeepHostSize = summons.Length;
                _lastUpkeepPercentPerMinute = percentPerMinute;
            }
            if (percentPerMinute <= 0.0f)
            {
                return;
            }

            foreach (NpcHeroSummon summon in summons)
            {
                NpcElement npc = summon.ParentModel;
                if (npc == null
                    || npc.Health == null
                    || npc.HealthElement == null)
                {
                    continue;
                }
                float drain = Math.Max(0.0f, npc.Health.UpperLimit)
                    * (percentPerMinute / 100.0f)
                    * (elapsed / 60.0f);
                if (drain <= 0.0f)
                {
                    continue;
                }
                if (npc.Health.ModifiedValue <= drain + 0.001f)
                {
                    npc.HealthElement.Kill();
                }
                else
                {
                    npc.Health.DecreaseBy(drain);
                }
            }
        }

        internal static float GetUpkeepPercentPerMinute(
            int activeServants,
            float necromanticPower)
        {
            if (activeServants <= 0)
            {
                return 0.0f;
            }
            float basePercent = Math.Min(8.0f, activeServants + 1.0f);
            float powerFactor = Mathf.Clamp01(
                1.0f - (necromanticPower / 100.0f));
            return basePercent * powerFactor;
        }

        internal static float GetRestAttritionPercent(
            int activeServants,
            float hours,
            float necromanticPower)
        {
            if (activeServants <= 0 || hours <= 0.0f)
            {
                return 0.0f;
            }
            float basePercent = Math.Min(
                90.0f,
                45.0f + (18.0f * Math.Max(0, activeServants - 1)));
            float duration = Math.Max(0.0f, hours) / 8.0f;
            float powerFactor = Mathf.Clamp01(
                1.0f - (necromanticPower / 100.0f));
            return Math.Min(90.0f, basePercent * duration * powerFactor);
        }

        private static void AfterRestTimeSkipped(
            Hero hero,
            float hourValue)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || plugin.RestBehavior == null
                || plugin.RestBehavior.Value != RestHostBehavior.Sustain)
            {
                return;
            }
            NpcHeroSummon[] summons = GetFormationHost(hero);
            float percent = GetRestAttritionPercent(
                summons.Length,
                hourValue,
                SoulProgressionRuntime.GetNecromanticPower());
            if (percent <= 0.0f)
            {
                return;
            }
            foreach (NpcHeroSummon summon in summons)
            {
                NpcElement npc = summon.ParentModel;
                if (npc == null || npc.Health == null || npc.HealthElement == null)
                {
                    continue;
                }
                float damage = Math.Max(0.0f, npc.Health.UpperLimit)
                    * (percent / 100.0f);
                if (npc.Health.ModifiedValue <= damage + 0.001f)
                {
                    npc.HealthElement.Kill();
                }
                else
                {
                    npc.Health.DecreaseBy(damage);
                }
            }
            plugin.LogDiagnostic(
                "Rest attrition: servants=" + summons.Length
                + "; hours=" + hourValue.ToString("0.##")
                + "; loss=" + percent.ToString("0.##") + "% max Health.");
        }

        private static void AfterRestPreviewRefreshed(VRestPopupUI __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (__instance == null
                || plugin == null
                || !plugin.IsEnabled
                || hero == null
                || RestWarningTextField == null)
            {
                return;
            }
            Component warning = RestWarningTextField.GetValue(__instance) as Component;
            PropertyInfo textProperty = warning == null
                ? null
                : warning.GetType().GetProperty("text");
            if (warning == null || textProperty == null)
            {
                return;
            }
            string current = textProperty.GetValue(warning, null) as string
                ?? string.Empty;
            int prior = current.IndexOf(
                "\nNecromantic ",
                StringComparison.Ordinal);
            if (prior >= 0)
            {
                current = current.Substring(0, prior);
            }
            bool showNativeWarning = __instance.Target != null
                && __instance.Target.WillBeSurprisedByWyrdNight;
            if (!showNativeWarning)
            {
                current = string.Empty;
            }
            NpcHeroSummon[] summons = GetFormationHost(hero);
            if (summons.Length <= 0)
            {
                textProperty.SetValue(warning, current, null);
                warning.gameObject.SetActive(
                    showNativeWarning && !string.IsNullOrWhiteSpace(current));
                return;
            }
            string message;
            if (plugin.RestBehavior != null
                && plugin.RestBehavior.Value == RestHostBehavior.Dismiss)
            {
                message = "Necromantic host: resting will dismiss every servant.";
            }
            else
            {
                float percent = GetRestAttritionPercent(
                    summons.Length,
                    __instance.Target.HourValueChange,
                    SoulProgressionRuntime.GetNecromanticPower());
                int mayPerish = summons.Count(summon =>
                    summon.ParentModel != null
                    && summon.ParentModel.Health != null
                    && summon.ParentModel.Health.ModifiedValue
                        <= summon.ParentModel.Health.UpperLimit
                            * (percent / 100.0f) + 0.001f);
                message = "Necromantic upkeep: -"
                    + percent.ToString("0.#")
                    + "% max Health"
                    + (mayPerish > 0
                        ? "; " + mayPerish + " servant(s) may perish."
                        : ".");
            }
            string combined = string.IsNullOrWhiteSpace(current)
                ? message
                : current + "\n" + message;
            textProperty.SetValue(warning, combined, null);
            warning.gameObject.SetActive(true);
        }

        private static void BeginSwarm(
            NpcHeroSummon summon,
            NpcElement target)
        {
            if (summon == null || summon.ParentModel == null || target == null)
            {
                return;
            }
            string id = ((Model)summon).ID;
            ClearSwarm(id);

            float empowermentMovement = 1.0f;
            EmpowermentState empowerment;
            if (EmpowermentStates.TryGetValue(id, out empowerment))
            {
                empowermentMovement = empowerment.MovementMultiplier;
            }
            SummonBehavior behavior = SoulProgressionRuntime.GetSummonBehavior();
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            bool bulwarkAdvance = behavior == SummonBehavior.Bulwark
                && IsBulwarkAdvanceActive(Hero.Current);
            float behaviorMovement = SoulProgressionRuntime.GetNecromanticPower()
                    < SoulProgressionRuntime.BehaviorCommandPower
                ? 1.0f
                : behavior == SummonBehavior.Hunt
                    ? HuntBehaviorMovementMultiplier
                    : bulwarkAdvance
                        ? GetBulwarkAdvanceMovementMultiplier(plugin)
                        : 1.0f;
            float movementCeiling = bulwarkAdvance
                ? BulwarkAdvanceMaximumMovementMultiplier
                : MaximumCommandMovementMultiplier;
            behaviorMovement = Math.Min(
                behaviorMovement,
                Math.Max(1.0f, movementCeiling / empowermentMovement));
            float movementMultiplier = Math.Min(
                SwarmMovementMultiplier,
                Math.Max(
                    1.0f,
                    movementCeiling
                        / (empowermentMovement * behaviorMovement)));
            SwarmState state = new SwarmState
            {
                Target = target,
                ExpiresAt = Time.time + SwarmDurationSeconds,
                MovementMultiplier = movementMultiplier
            };
            NpcElement npc = summon.ParentModel;
            if (movementMultiplier > 1.0f
                && npc.CharacterStats != null
                && npc.CharacterStats.MovementSpeedMultiplier != null)
            {
                state.MovementTweak = StatTweak.Multi(
                    npc.CharacterStats.MovementSpeedMultiplier,
                    movementMultiplier,
                    null,
                    npc);
                ((Model)state.MovementTweak).MarkedNotSaved = true;
            }
            SwarmStates[id] = state;
            RemoveSpeedTweak(id);
            if (npc.Controller != null)
            {
                UpdateBehaviorSpeed(npc.Controller, plugin, summon, id);
                UpdateEmpoweredPresentation(npc.Controller, id);
            }
        }

        private static void UpdateSwarmStates()
        {
            StateRemovalBuffer.Clear();
            foreach (KeyValuePair<string, SwarmState> pair in SwarmStates)
            {
                SwarmState state = pair.Value;
                if (state.Target == null
                    || state.Target.HasBeenDiscarded
                    || !state.Target.IsAlive
                    || Time.time >= state.ExpiresAt)
                {
                    StateRemovalBuffer.Add(pair.Key);
                }
            }
            foreach (string summonId in StateRemovalBuffer)
            {
                ClearSwarm(summonId);
            }
        }

        private static bool TryConsumeSwarmHit(
            NpcElement dealer,
            NpcElement receiver)
        {
            if (dealer == null || receiver == null)
            {
                return false;
            }
            string id = GetSummonId(dealer);
            SwarmState state;
            if (string.IsNullOrEmpty(id)
                || !SwarmStates.TryGetValue(id, out state)
                || !ReferenceEquals(state.Target, receiver)
                || Time.time >= state.ExpiresAt)
            {
                return false;
            }
            ClearSwarm(id);
            return true;
        }

        private static void ClearSwarm(string id)
        {
            SwarmState state;
            if (!SwarmStates.TryGetValue(id, out state))
            {
                return;
            }
            SwarmStates.Remove(id);
            DiscardTweak(state.MovementTweak);
        }

        private static void ApplyEmpowermentVisual(
            NpcController controller,
            EmpowermentState state)
        {
            if (controller == null
                || controller.AlivePrefab == null
                || state == null)
            {
                return;
            }
            Transform visualMarker = controller.AlivePrefab.transform;
            if (!ReferenceEquals(state.VisualMarker, visualMarker))
            {
                RestoreEmpowermentVisual(state);
                state.VisualMarker = visualMarker;
                state.NextVisualRootLookupTime = 0.0f;
            }
            if (state.VisualRoot == null)
            {
                if (Time.unscaledTime < state.NextVisualRootLookupTime)
                {
                    return;
                }
                state.NextVisualRootLookupTime = Time.unscaledTime + 0.5f;
                Transform resolvedVisualRoot = GetEmpowermentVisualRoot(controller);
                if (resolvedVisualRoot == null)
                {
                    LogEmpowermentVisualRootFailure(controller, state);
                    return;
                }
                state.VisualRoot = resolvedVisualRoot;
                state.OriginalLocalScale = resolvedVisualRoot.localScale;
                state.OriginalLocalPosition = resolvedVisualRoot.localPosition;
            }
            Transform visualRoot = state.VisualRoot;

            float soulforgedMultiplier = controller.Npc == null
                ? 1.0f
                : SoulforgedRuntime.GetMultiplier(GetSummonId(controller.Npc));
            float empowermentSize = state.IsEmpowered
                ? state.SizeMultiplier
                : 1.0f;
            Vector3 expectedScale = Vector3.Scale(
                state.OriginalLocalScale,
                Vector3.one * soulforgedMultiplier * empowermentSize);
            Vector3 scaleBeforeCorrection = visualRoot.localScale;
            if ((visualRoot.localScale - expectedScale).sqrMagnitude <= 0.000001f)
            {
                return;
            }

            visualRoot.localScale = state.OriginalLocalScale;
            visualRoot.localPosition = state.OriginalLocalPosition;
            float beforeMinY;
            bool hadGround = TryGetVisibleMinimumY(visualRoot, out beforeMinY);
            float beforeHeight;
            int rendererCount;
            TryGetVisibleHeight(
                visualRoot,
                out beforeHeight,
                out rendererCount);
            visualRoot.localScale = expectedScale;
            float afterMinY;
            if (hadGround && TryGetVisibleMinimumY(visualRoot, out afterMinY))
            {
                visualRoot.position += Vector3.up * (beforeMinY - afterMinY);
            }
            float afterHeight;
            int ignoredRendererCount;
            TryGetVisibleHeight(
                visualRoot,
                out afterHeight,
                out ignoredRendererCount);
            LogEmpowermentVisualCorrection(
                controller,
                state,
                scaleBeforeCorrection,
                expectedScale,
                beforeHeight,
                afterHeight,
                rendererCount);
        }

        private static Transform GetEmpowermentVisualRoot(NpcController controller)
        {
            if (controller == null || controller.AlivePrefab == null)
            {
                return null;
            }
            Transform visualRoot = controller.AlivePrefab.transform;
            if (!ReferenceEquals(visualRoot, controller.transform)
                && IsSafeEmpowermentVisualRoot(controller, visualRoot))
            {
                return visualRoot;
            }
            Transform ancestor = visualRoot.parent;
            while (ancestor != null && !ReferenceEquals(ancestor, controller.transform))
            {
                if (IsSafeEmpowermentVisualRoot(controller, ancestor))
                {
                    return ancestor;
                }
                ancestor = ancestor.parent;
            }
            if (controller.Animator != null
                && !ReferenceEquals(
                    controller.Animator.transform,
                    controller.transform)
                && IsSafeEmpowermentVisualRoot(
                    controller,
                    controller.Animator.transform))
            {
                return controller.Animator.transform;
            }
            return controller.RootMotion != null
                && !ReferenceEquals(
                    controller.RootMotion.transform,
                    controller.transform)
                && IsSafeEmpowermentVisualRoot(
                    controller,
                    controller.RootMotion.transform)
                    ? controller.RootMotion.transform
                    : null;
        }

        private static bool IsSafeEmpowermentVisualRoot(
            NpcController controller,
            Transform root)
        {
            if (!HasRenderableGeometry(root))
            {
                return false;
            }
            if (root.GetComponentInChildren<KandraRenderer>(true) == null)
            {
                return true;
            }

            Transform animatorRoot = controller == null
                || controller.Animator == null
                    ? null
                    : controller.Animator.transform;
            return animatorRoot != null
                && !ReferenceEquals(root, animatorRoot)
                && animatorRoot.IsChildOf(root);
        }

        private static bool HasRenderableGeometry(Transform root)
        {
            if (root == null)
            {
                return false;
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsEffectOnlyRenderer(renderer))
                {
                    return true;
                }
            }
            return root.GetComponentInChildren<KandraRenderer>(true) != null;
        }

        private static bool IsEffectOnlyRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return true;
            }
            string rendererType = renderer.GetType().Name;
            return string.Equals(
                    rendererType,
                    "ParticleSystemRenderer",
                    StringComparison.Ordinal)
                || string.Equals(
                    rendererType,
                    "TrailRenderer",
                    StringComparison.Ordinal)
                || string.Equals(
                    rendererType,
                    "LineRenderer",
                    StringComparison.Ordinal)
                || string.Equals(
                    rendererType,
                    "VFXRenderer",
                    StringComparison.Ordinal);
        }

        private static void EnsureEmpowermentVisualEnforcer(
            NpcController controller)
        {
            if (controller == null)
            {
                return;
            }
            EmpowermentVisualEnforcer enforcer =
                controller.GetComponent<EmpowermentVisualEnforcer>();
            if (enforcer == null)
            {
                enforcer = controller.gameObject
                    .AddComponent<EmpowermentVisualEnforcer>();
            }
            enforcer.Controller = controller;
        }

        private static bool ApplyLateEmpowermentVisual(
            NpcController controller)
        {
            if (controller == null || controller.Npc == null)
            {
                return false;
            }
            string summonId = GetSummonId(controller.Npc);
            EmpowermentState state;
            if (string.IsNullOrEmpty(summonId)
                || !EmpowermentStates.TryGetValue(summonId, out state))
            {
                return false;
            }
            ApplyEmpowermentVisual(controller, state);
            return true;
        }

        private static bool TryGetVisibleHeight(
            Transform visualRoot,
            out float height,
            out int rendererCount)
        {
            height = 0.0f;
            rendererCount = 0;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            foreach (Renderer renderer in visualRoot
                .GetComponentsInChildren<Renderer>(true))
            {
                if (IsEffectOnlyRenderer(renderer)
                    || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                minimumY = Math.Min(minimumY, renderer.bounds.min.y);
                maximumY = Math.Max(maximumY, renderer.bounds.max.y);
                rendererCount++;
            }
            if (rendererCount == 0)
            {
                return false;
            }
            height = Math.Max(0.0f, maximumY - minimumY);
            return true;
        }

        private static void LogEmpowermentVisualRootFailure(
            NpcController controller,
            EmpowermentState state)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || plugin.Diagnostics == null
                || !plugin.Diagnostics.Value
                || state.VisualRootFailureLogged)
            {
                return;
            }
            state.VisualRootFailureLogged = true;
            plugin.LogDiagnostic(
                "Empower visual root unresolved: controller="
                + GetTransformPath(controller == null
                    ? null
                    : controller.transform)
                + "; alive=" + GetTransformPath(controller == null
                    || controller.AlivePrefab == null
                        ? null
                        : controller.AlivePrefab.transform)
                + "; animator=" + GetTransformPath(controller == null
                    || controller.Animator == null
                        ? null
                        : controller.Animator.transform)
                + "; rootMotion=" + GetTransformPath(controller == null
                    || controller.RootMotion == null
                        ? null
                        : controller.RootMotion.transform) + ".");
        }

        private static void LogEmpowermentVisualCorrection(
            NpcController controller,
            EmpowermentState state,
            Vector3 beforeScale,
            Vector3 expectedScale,
            float beforeHeight,
            float afterHeight,
            int rendererCount)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || plugin.Diagnostics == null
                || !plugin.Diagnostics.Value
                || state.VisualDiagnosticCount >= 3)
            {
                return;
            }
            state.VisualDiagnosticCount++;
            int kandraRendererCount = state.VisualRoot
                .GetComponentsInChildren<KandraRenderer>(true)
                .Count(renderer => renderer != null && !renderer.Destroyed);
            plugin.LogDiagnostic(
                "Empower visual correction " + state.VisualDiagnosticCount
                + "/3: root=" + GetTransformPath(state.VisualRoot)
                + "; controller=" + GetTransformPath(controller.transform)
                + "; beforeLocal=" + FormatVector3(beforeScale)
                + "; expectedLocal=" + FormatVector3(expectedScale)
                + "; actualLocal=" + FormatVector3(state.VisualRoot.localScale)
                + "; lossy=" + FormatVector3(state.VisualRoot.lossyScale)
                + "; renderers=" + rendererCount
                + "; kandraRenderers=" + kandraRendererCount
                + "; boundsHeight=" + beforeHeight.ToString("0.###")
                + "->" + afterHeight.ToString("0.###") + ".");
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "none";
            }
            List<string> path = new List<string>();
            Transform current = transform;
            while (current != null && path.Count < 12)
            {
                path.Add(current.name);
                current = current.parent;
            }
            path.Reverse();
            return string.Join("/", path.ToArray());
        }

        private static string FormatVector3(Vector3 value)
        {
            return value.x.ToString("0.###") + ","
                + value.y.ToString("0.###") + ","
                + value.z.ToString("0.###");
        }

        private static bool TryGetVisibleMinimumY(
            Transform visualRoot,
            out float minimumY)
        {
            minimumY = float.PositiveInfinity;
            bool found = false;
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (IsEffectOnlyRenderer(renderer)
                    || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                minimumY = Math.Min(minimumY, renderer.bounds.min.y);
                found = true;
            }
            return found;
        }

        private static void RestoreEmpowermentVisual(EmpowermentState state)
        {
            if (state == null || state.VisualRoot == null)
            {
                return;
            }
            state.VisualRoot.localScale = state.OriginalLocalScale;
            state.VisualRoot.localPosition = state.OriginalLocalPosition;
            state.VisualRoot = null;
            state.VisualMarker = null;
            state.NextVisualRootLookupTime = 0.0f;
        }

        private static void ClearEmpowerment(string id)
        {
            EmpowermentState state;
            if (!EmpowermentStates.TryGetValue(id, out state))
            {
                return;
            }
            EmpowermentStates.Remove(id);
            DiscardTweak(state.MovementTweak);
            RestoreEmpowermentVisual(state);
        }

        private static void ClearAllServantPowerStates()
        {
            foreach (string id in SwarmStates.Keys.ToArray())
            {
                ClearSwarm(id);
            }
            foreach (string id in EmpowermentStates.Keys.ToArray())
            {
                ClearEmpowerment(id);
            }
            LocomotionPlaybackMultipliers.Clear();
        }

        private static IEnumerable<CodeInstruction> TranspileAiTick(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> result = new List<CodeInstruction>();
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4
                    && instruction.operand is float
                    && Math.Abs((float)instruction.operand - 2.5f) < 0.0001f)
                {
                    result.Add(new CodeInstruction(OpCodes.Ldarg_0)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction));
                    result.Add(new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(
                            typeof(SummonRuntime),
                            nameof(GetAiTickInterval))));
                    replaced++;
                }
                else
                {
                    result.Add(instruction);
                }
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException(
                    "Expected one NpcAlly.UnityUpdate AI tick literal but found "
                    + replaced + ".");
            }
            return result;
        }

        private static void BeforeNpcAllyUnityUpdate(NpcAlly __instance)
        {
            if (SpawnReadinessBySummon.Count == 0
                || __instance == null
                || __instance.ParentModel == null)
            {
                return;
            }
            string summonId = GetSummonId(__instance.ParentModel);
            SpawnReadinessState readiness;
            if (string.IsNullOrEmpty(summonId)
                || !SpawnReadinessBySummon.TryGetValue(
                    summonId,
                    out readiness))
            {
                return;
            }

            float now = Time.unscaledTime;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null || !plugin.IsEnabled)
            {
                if (now >= readiness.EarliestReleaseAt)
                {
                    MovementPreventedField.SetValue(__instance, false);
                }
                SpawnReadinessBySummon.Remove(summonId);
                return;
            }
            NpcGeneralFSM generalFsm;
            if (now < readiness.EarliestReleaseAt
                || !HasPlayingGeneralAnimation(
                    __instance.ParentModel,
                    out generalFsm))
            {
                MovementPreventedField.SetValue(__instance, true);
                return;
            }
            MovementPreventedField.SetValue(__instance, false);
            SpawnReadinessBySummon.Remove(summonId);
        }

        private static IEnumerable<CodeInstruction> TranspileSpawnRecovery(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceFloatLiteral(
                instructions,
                1.5f,
                AccessTools.Method(typeof(SummonRuntime), nameof(GetSpawnRecoverySeconds)),
                "NpcHeroSummon.PreventMovement duration");
        }

        private static IEnumerable<CodeInstruction> ReplaceFloatLiteral(
            IEnumerable<CodeInstruction> instructions,
            float expected,
            MethodInfo replacement,
            string label)
        {
            List<CodeInstruction> result = instructions.ToList();
            int replaced = 0;
            for (int index = 0; index < result.Count; index++)
            {
                CodeInstruction instruction = result[index];
                if (instruction.opcode == OpCodes.Ldc_R4
                    && instruction.operand is float
                    && Math.Abs((float)instruction.operand - expected) < 0.0001f)
                {
                    result[index] = new CodeInstruction(OpCodes.Call, replacement)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction);
                    replaced++;
                }
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException(
                    "Expected one " + label + " literal but found " + replaced + ".");
            }
            return result;
        }

        private static float GetAiTickInterval(NpcAlly ally)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcHeroSummon summon = ally as NpcHeroSummon;
            if (plugin == null
                || !plugin.IsEnabled
                || summon == null)
            {
                return 2.5f;
            }
            if (Time.unscaledTime >= _nextAiDecisionIntervalRefreshTime)
            {
                float configuredInterval = plugin.AiTickInterval.Value;
                float noviceInterval = Math.Max(
                    configuredInterval,
                    NoviceAiDecisionInterval);
                float mastery = Mathf.Clamp01(
                    SoulProgressionRuntime.GetNecromanticPower() / 100.0f);
                _cachedAiDecisionInterval = Mathf.Lerp(
                    noviceInterval,
                    configuredInterval,
                    mastery);
                _nextAiDecisionIntervalRefreshTime = Time.unscaledTime
                    + AiDecisionIntervalRefreshSeconds;
            }
            string summonId = ((Model)summon).ID;
            int hash = string.IsNullOrEmpty(summonId)
                ? summon.GetHashCode()
                : StringComparer.Ordinal.GetHashCode(summonId);
            float normalizedJitter = ((hash & 1023) / 1023.0f) * 2.0f - 1.0f;
            return _cachedAiDecisionInterval
                * (1.0f
                    + normalizedJitter * AiDecisionIntervalJitterFraction);
        }

        private static float GetSpawnRecoverySeconds()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin != null && plugin.IsEnabled
                ? plugin.SpawnRecoverySeconds.Value
                : 1.5f;
        }

        private static void QueueTeleportVfx(NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || summon == null
                || summon.ParentModel == null
                || summon.ParentModel.HasBeenDiscarded)
            {
                return;
            }

            string summonId = ((Model)summon).ID;
            PendingTeleportVfx pending;
            if (PendingTeleportVfxBySummon.TryGetValue(summonId, out pending)
                && pending != null
                && Time.unscaledTime - pending.RequestedAt <= 10.0f)
            {
                pending.OutstandingRequests++;
                pending.RequestedAt = Time.unscaledTime;
            }
            else
            {
                PendingTeleportVfxBySummon[summonId] = new PendingTeleportVfx
                {
                    Origin = summon.ParentModel.Coords,
                    RequestedAt = Time.unscaledTime,
                    OutstandingRequests = 1
                };
            }
        }

        private static void AfterTeleportPathCalculated(NpcAlly __instance)
        {
            NpcHeroSummon summon = __instance as NpcHeroSummon;
            if (summon == null || summon.ParentModel == null)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            PendingTeleportVfx pending;
            if (!PendingTeleportVfxBySummon.TryGetValue(summonId, out pending))
            {
                return;
            }
            if ((summon.ParentModel.Coords - pending.Origin).sqrMagnitude
                > 0.25f)
            {
                PendingTeleportVfxBySummon.Remove(summonId);
                RestoreRecallLocomotion(summon, summonId);
                SoulSalvageRuntime.SpawnNecromanticSummonVfx(summon.ParentModel);
                return;
            }
            pending.OutstandingRequests--;
            if (pending.OutstandingRequests <= 0)
            {
                PendingTeleportVfxBySummon.Remove(summonId);
                RestoreRecallLocomotion(summon, summonId);
            }
        }

        private static void RestoreRecallLocomotion(
            NpcHeroSummon summon,
            string summonId)
        {
            PendingRecallPlacement placement;
            if (summon == null
                || summon.ParentModel == null
                || summon.ParentModel.Movement == null
                || string.IsNullOrEmpty(summonId)
                || !PendingRecallPlacements.TryGetValue(
                    summonId,
                    out placement)
                || placement == null
                || !placement.DestinationConsumed
                || placement.LocomotionRestored)
            {
                return;
            }

            placement.LocomotionRestored = true;
            bool reachedReservedDestination = placement.HasReservedDestination
                && (summon.ParentModel.Coords - placement.Position).sqrMagnitude
                    <= RecallAnchorTolerance * RecallAnchorTolerance;
            if (!reachedReservedDestination)
            {
                placement.Position = summon.ParentModel.Coords;
            }
            ForceRecallCombatExit(summon.ParentModel);
            Patrol patrol = PatrolField.GetValue(summon) as Patrol;
            if (patrol != null)
            {
                summon.ParentModel.Movement.ResetMainState(patrol);
                patrol.UpdatePlace(placement.Position);
                patrol.UpdateVelocityScheme(VelocityScheme.Walk);
            }
            summon.ParentModel.SetAnimatorState(
                NpcFSMType.GeneralFSM,
                NpcStateType.Idle,
                0.0f);
            AnimationWatchdogsBySummon.Remove(summonId);

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogDiagnostic(
                    "Recall restored idle patrol and locomotion for "
                    + summonId + ".");
            }
        }

        private static void ForceRecallCombatExit(NpcElement npc)
        {
            if (npc == null)
            {
                return;
            }
            if (npc.NpcAI != null && npc.NpcAI.InCombat)
            {
                npc.NpcAI.ExitCombat(
                    force: true,
                    exitToIdle: true,
                    canBeVictorious: false);
            }
            else
            {
                npc.ForceEndCombat();
            }
        }

        private static void BeforeNpcTeleport(
            NpcElement npc,
            ref TeleportDestination destination)
        {
            string summonId = GetSummonId(npc);
            PendingRecallPlacement placement;
            if (string.IsNullOrEmpty(summonId)
                || !PendingRecallPlacements.TryGetValue(
                    summonId,
                    out placement))
            {
                return;
            }
            if (placement == null || placement.DestinationConsumed)
            {
                if (placement == null)
                {
                    PendingRecallPlacements.Remove(summonId);
                }
                return;
            }
            placement.DestinationConsumed = true;
            if (placement.ExpiresAt < Time.unscaledTime)
            {
                placement.HasReservedDestination = false;
            }
            if (!placement.HasReservedDestination)
            {
                return;
            }
            destination.position = placement.Position;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogDiagnostic(
                    "Recall placed summon " + summonId + " at "
                    + placement.Position.ToString("F2") + ".");
            }
        }

        private static bool TryGetRecallAnchor(
            NpcHeroSummon summon,
            out Vector3 anchor)
        {
            anchor = Vector3.zero;
            Hero hero = Hero.Current;
            if (summon == null || hero == null)
            {
                return false;
            }
            string summonId = ((Model)summon).ID;
            PendingRecallPlacement placement;
            if (!PendingRecallPlacements.TryGetValue(summonId, out placement)
                || placement == null
                || !placement.DestinationConsumed
                || !placement.LocomotionRestored)
            {
                return false;
            }
            if ((hero.Coords - placement.HeroOrigin).sqrMagnitude
                > RecallPlacementHeroMoveReleaseDistance
                    * RecallPlacementHeroMoveReleaseDistance)
            {
                PendingRecallPlacements.Remove(summonId);
                return false;
            }
            anchor = placement.Position;
            return true;
        }

        private static bool BeforeStayCloseToAlly(NpcAlly __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcHeroSummon summon = __instance as NpcHeroSummon;
            Patrol patrol = PatrolField.GetValue(__instance) as Patrol;
            if (plugin == null
                || !plugin.IsEnabled
                || summon == null
                || Time.timeScale == 0.0f
                || summon.Ally == null
                || summon.ParentModel == null
                || summon.ParentModel.Movement == null
                || patrol == null)
            {
                if (summon != null && patrol != null)
                {
                    RestorePatrolRadius(summon);
                }
                return true;
            }

            bool hasExplicitCommandTarget = HasExplicitCommandTarget(summon);
            if (hasExplicitCommandTarget
                && !UpdateExplicitCommandPursuit(summon))
            {
                hasExplicitCommandTarget = false;
            }
            HeldSummonState heldState;
            if (!hasExplicitCommandTarget
                && HeldSummons.TryGetValue(
                    ((Model)summon).ID,
                    out heldState))
            {
                SummonFormationCoordinator.Suspend(((Model)summon).ID);
                ClearIdleMovementState(((Model)summon).ID);
                float anchorDistanceSqr =
                    (heldState.Anchor - summon.ParentModel.Coords).sqrMagnitude;
                StabilizedPatrols.Add(((Model)summon).ID);
                patrol.UpdateRadius(0.0f);
                patrol.UpdatePlace(heldState.Anchor);
                patrol.UpdateVelocityScheme(
                    anchorDistanceSqr <= 4.0f
                        ? VelocityScheme.Walk
                        : anchorDistanceSqr <= 64.0f
                            ? VelocityScheme.Trot
                            : VelocityScheme.Run);
                return false;
            }

            Vector3 recallAnchor;
            if (!hasExplicitCommandTarget
                && TryGetRecallAnchor(summon, out recallAnchor))
            {
                SummonFormationCoordinator.Suspend(((Model)summon).ID);
                ClearIdleMovementState(((Model)summon).ID);
                float anchorDistanceSqr =
                    (recallAnchor - summon.ParentModel.Coords).sqrMagnitude;
                StabilizedPatrols.Add(((Model)summon).ID);
                patrol.UpdateRadius(RecallAnchorTolerance);
                patrol.UpdatePlace(recallAnchor);
                patrol.UpdateVelocityScheme(
                    anchorDistanceSqr <= 4.0f
                        ? VelocityScheme.Walk
                        : anchorDistanceSqr <= 64.0f
                            ? VelocityScheme.Trot
                            : VelocityScheme.Run);
                return false;
            }

            SummonBehavior behavior = SoulProgressionRuntime.GetSummonBehavior();
            bool usesGuardFormation = behavior == SummonBehavior.Guard
                && HasGlobalFormationControl();
            bool hasPriorityTarget = hasExplicitCommandTarget
                || HasActivePriorityTarget(summon);
            bool usesHuntFormation = behavior == SummonBehavior.Hunt;
            if (!usesGuardFormation || hasPriorityTarget)
            {
                ClearGuardIdleState(((Model)summon).ID);
            }
            if (!usesHuntFormation || hasPriorityTarget)
            {
                ClearHuntIdleState(((Model)summon).ID);
            }
            if (hasPriorityTarget)
            {
                string summonId = ((Model)summon).ID;
                SummonFormationCoordinator.Suspend(summonId);
                StabilizedPatrols.Remove(summonId);
                patrol.UpdateRadius(NativePatrolRadius);
                return false;
            }
            if ((behavior == SummonBehavior.Bulwark
                    || usesGuardFormation
                    || usesHuntFormation)
                && !hasPriorityTarget)
            {
                float anchorTolerance = 0.0f;
                bool gentleIdleMovement = false;
                bool bulwarkAdvance = false;
                bool huntAttackMove = false;
                Vector3 anchor;
                if (behavior == SummonBehavior.Bulwark)
                {
                    bulwarkAdvance = IsBulwarkAdvanceActive(Hero.Current);
                    anchor = GetBulwarkAnchor(summon);
                    anchorTolerance = bulwarkAdvance
                        ? BulwarkAdvanceAnchorTolerance
                        : BulwarkAnchorTolerance;
                }
                else if (behavior == SummonBehavior.Hunt)
                {
                    huntAttackMove = TryGetHuntAttackMoveAnchor(
                        summon,
                        out anchor);
                    if (huntAttackMove)
                    {
                        ClearHuntIdleState(((Model)summon).ID);
                        anchorTolerance = HuntAttackMoveAnchorTolerance;
                    }
                    else
                    {
                        anchor = GetHuntIdleAnchor(
                            summon,
                            out anchorTolerance,
                            out gentleIdleMovement);
                    }
                }
                else
                {
                    anchor = GetGuardIdleAnchor(
                        summon,
                        out anchorTolerance,
                        out gentleIdleMovement);
                }
                if (behavior != SummonBehavior.Bulwark && !huntAttackMove)
                {
                    anchor = GetRecoveredFormationAnchor(
                        summon,
                        anchor,
                        anchorTolerance);
                }
                float anchorDistanceSqr =
                    (anchor - summon.ParentModel.Coords).sqrMagnitude;
                float heroDistanceSqr =
                    (summon.Ally.Coords - summon.ParentModel.Coords).sqrMagnitude;
                float formationTeleportDistance = plugin.TeleportDistance.Value;
                if (heroDistanceSqr
                    > formationTeleportDistance * formationTeleportDistance)
                {
                    RestorePatrolRadius(summon);
                    object[] arguments =
                    {
                        heroDistanceSqr,
                        TeleportContext.AllyRanAway,
                        Vector3.zero
                    };
                    QueueTeleportVfx(summon);
                    TeleportToAllyMethod.Invoke(__instance, arguments);
                    return false;
                }

                StabilizedPatrols.Add(((Model)summon).ID);
                patrol.UpdateRadius(anchorTolerance);
                UpdateFormationPatrolPlace(
                    summon,
                    patrol,
                    anchor,
                    bulwarkAdvance
                        ? BulwarkAdvanceAnchorUpdateDistance
                        : huntAttackMove
                            ? HuntAttackMoveAnchorUpdateDistance
                            : FormationPatrolAnchorUpdateDistance);
                patrol.UpdateVelocityScheme(
                    gentleIdleMovement
                        ? VelocityScheme.Walk
                        : GetBulwarkVelocityScheme(
                            summon.Ally,
                            anchorDistanceSqr,
                            bulwarkAdvance));
                return false;
            }

            SummonFormationCoordinator.Suspend(((Model)summon).ID);

            float distanceSqr =
                (summon.Ally.Coords - summon.ParentModel.Coords).sqrMagnitude;
            float teleportDistance = plugin.TeleportDistance.Value;
            if (distanceSqr > teleportDistance * teleportDistance)
            {
                RestorePatrolRadius(summon);
                object[] arguments =
                {
                    distanceSqr,
                    TeleportContext.AllyRanAway,
                    Vector3.zero
                };
                QueueTeleportVfx(summon);
                TeleportToAllyMethod.Invoke(__instance, arguments);
                return false;
            }

            float trotDistance = plugin.TrotDistance.Value;
            float runDistance = Math.Max(trotDistance, plugin.RunDistance.Value);
            if (distanceSqr <= trotDistance * trotDistance)
            {
                if (behavior == SummonBehavior.Hunt)
                {
                    RestorePatrolRadius(summon);
                    patrol.UpdateRadius(NativePatrolRadius);
                    patrol.UpdatePlace(summon.Ally.Coords);
                }
                else
                {
                    StabilizedPatrols.Add(((Model)summon).ID);
                    patrol.UpdateRadius(0.0f);
                    patrol.UpdatePlace(summon.ParentModel.Coords);
                }
                patrol.UpdateVelocityScheme(VelocityScheme.Walk);
            }
            else
            {
                RestorePatrolRadius(summon);
                patrol.UpdatePlace(summon.Ally.Coords);
                patrol.UpdateVelocityScheme(
                    distanceSqr <= runDistance * runDistance
                        ? VelocityScheme.Trot
                        : VelocityScheme.Run);
            }
            return false;
        }

        private static bool TryGetOwnedTargetOverride(
            NpcHeroSummon summon,
            out OwnedTargetOverrideState state)
        {
            state = null;
            return summon != null
                && OwnedTargetOverrides.TryGetValue(
                    ((Model)summon).ID,
                    out state)
                && state != null;
        }

        private static OwnedTargetOverrideState SetOwnedTargetOverride(
            NpcHeroSummon summon,
            NpcElement target,
            int priority,
            bool explicitTarget,
            bool ignoreHeroLeash = false)
        {
            if (summon == null || summon.ParentModel == null || target == null)
            {
                return null;
            }
            string summonId = ((Model)summon).ID;
            OwnedTargetOverrideState current;
            if (OwnedTargetOverrides.TryGetValue(summonId, out current)
                && current != null
                && ReferenceEquals(current.Target, target)
                && current.Priority == priority
                && current.Explicit == explicitTarget
                && current.IgnoreHeroLeash == ignoreHeroLeash
                && current.Element != null
                && !current.Element.HasBeenDiscarded)
            {
                current.OutOfRangeSince = -1.0f;
                return current;
            }

            SoulAndServiceTargetOverride replacement =
                summon.ParentModel.AddElement(
                    new SoulAndServiceTargetOverride(
                        summon,
                        target,
                        priority));
            OwnedTargetOverrideState replacementState =
                new OwnedTargetOverrideState
                {
                    Target = target,
                    Element = replacement,
                    Priority = priority,
                    Explicit = explicitTarget,
                    IgnoreHeroLeash = ignoreHeroLeash,
                    OutOfRangeSince = -1.0f
                };
            OwnedTargetOverrides[summonId] = replacementState;
            if (current != null
                && current.Element != null
                && !current.Element.HasBeenDiscarded
                && !ReferenceEquals(current.Element, replacement))
            {
                current.Element.Discard();
            }
            return replacementState;
        }

        private static void ClearOwnedTargetOverride(
            NpcHeroSummon summon,
            NpcElement expectedTarget = null)
        {
            if (summon == null)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            OwnedTargetOverrideState state;
            if (!OwnedTargetOverrides.TryGetValue(summonId, out state)
                || state == null
                || (expectedTarget != null
                    && !ReferenceEquals(state.Target, expectedTarget)))
            {
                return;
            }
            OwnedTargetOverrides.Remove(summonId);
            if (state.Element != null && !state.Element.HasBeenDiscarded)
            {
                state.Element.Discard();
            }
        }

        private static bool IsTargetWithinOwnedRangeGrace(
            NpcHeroSummon summon,
            NpcElement target)
        {
            OwnedTargetOverrideState state;
            if (TryGetOwnedTargetOverride(summon, out state)
                && ReferenceEquals(state.Target, target)
                && state.IgnoreHeroLeash)
            {
                state.OutOfRangeSince = -1.0f;
                return true;
            }
            if (IsWithinNativeSummonTargetRetentionRange(Hero.Current, target))
            {
                OwnedTargetOverrideState inRangeState;
                if (TryGetOwnedTargetOverride(summon, out inRangeState)
                    && ReferenceEquals(inRangeState.Target, target))
                {
                    inRangeState.OutOfRangeSince = -1.0f;
                }
                return true;
            }

            if (!TryGetOwnedTargetOverride(summon, out state)
                || !ReferenceEquals(state.Target, target))
            {
                return false;
            }
            if (state.OutOfRangeSince < 0.0f)
            {
                state.OutOfRangeSince = Time.unscaledTime;
            }
            return Time.unscaledTime - state.OutOfRangeSince
                <= TargetRangeReleaseGraceSeconds;
        }

        private static void ReassertOwnedCombatTarget(
            NpcHeroSummon summon,
            NpcElement target)
        {
            if (summon == null
                || summon.ParentModel == null
                || target == null
                || !IsWithinNativeSummonTargetRetentionRange(
                    Hero.Current,
                    target)
                || ReferenceEquals(
                    summon.ParentModel.GetCurrentTarget(),
                    target))
            {
                return;
            }
            if (summon.ParentModel.NpcAI != null
                && !summon.ParentModel.NpcAI.InCombat)
            {
                summon.ParentModel.NpcAI.EnterCombatWith(
                    target,
                    forceChange: true);
            }
            else
            {
                summon.ParentModel.ForceAddCombatTarget(
                    target,
                    recalculateTarget: true);
            }
        }

        private static void RegisterReciprocalServantThreat(
            NpcHeroSummon summon,
            NpcElement target)
        {
            if (summon == null
                || summon.ParentModel == null
                || target == null
                || target.HasBeenDiscarded
                || !target.IsAlive
                || target.IsUnconscious
                || target.NpcAI == null
                || !WithFactionUtils.WantToFight(target, summon.ParentModel))
            {
                return;
            }

            bool registered;
            if (target.NpcAI.InCombat)
            {
                registered = target.ForceAddCombatTarget(
                    summon.ParentModel,
                    recalculateTarget: false);
            }
            else
            {
                target.NpcAI.EnterCombatWith(
                    summon.ParentModel,
                    forceChange: false);
                registered = target.NpcAI.InCombat;
            }
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (registered && plugin != null)
            {
                plugin.LogDiagnostic(
                    "Registered servant " + ((Model)summon).ID
                    + " as a hostile threat for " + ((Model)target).ID + ".");
            }
        }

        private static void ReleaseExplicitCommandTarget(
            NpcHeroSummon summon,
            NpcElement target,
            string reason)
        {
            if (summon == null)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            ExplicitCommandTargets.Remove(summonId);
            ExplicitCommandPursuitStates.Remove(summonId);
            ClearOwnedTargetOverride(summon, target);
            if (summon.ParentModel != null
                && target != null
                && !target.HasBeenDiscarded)
            {
                summon.ParentModel.RemoveCombatTarget(target);
            }
            ClearSwarm(summonId);
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogDiagnostic(
                    "Released explicit target for summon " + summonId
                    + ": " + reason + ".");
            }
        }

        private static bool HasExplicitCommandTarget(NpcHeroSummon summon)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return false;
            }
            string id = ((Model)summon).ID;
            NpcElement target;
            if (!ExplicitCommandTargets.TryGetValue(id, out target))
            {
                return false;
            }
            if (target == null
                || target.HasBeenDiscarded
                || !target.IsAlive
                || target.IsUnconscious
                || !WithFactionUtils.WantToFight(summon.ParentModel, target))
            {
                ReleaseExplicitCommandTarget(
                    summon,
                    target,
                    "target is no longer a valid hostile");
                return false;
            }
            if (!IsTargetWithinOwnedRangeGrace(summon, target))
            {
                ReleaseExplicitCommandTarget(
                    summon,
                    target,
                    "target remained outside the summon leash");
                return false;
            }
            if (IsWithinNativeSummonTargetRetentionRange(Hero.Current, target))
            {
                SetOwnedTargetOverride(summon, target, 10, true);
                ReassertOwnedCombatTarget(summon, target);
            }
            return true;
        }

        private static bool UpdateExplicitCommandPursuit(
            NpcHeroSummon summon)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return false;
            }
            string summonId = ((Model)summon).ID;
            NpcElement target;
            if (!ExplicitCommandTargets.TryGetValue(summonId, out target)
                || target == null)
            {
                ExplicitCommandPursuitStates.Remove(summonId);
                return false;
            }

            Vector3 position = summon.ParentModel.Coords;
            float distance = Vector3.Distance(position, target.Coords);
            float now = Time.unscaledTime;
            ExplicitCommandPursuitState state;
            if (!ExplicitCommandPursuitStates.TryGetValue(
                    summonId,
                    out state)
                || state == null
                || !ReferenceEquals(state.Target, target))
            {
                state = new ExplicitCommandPursuitState
                {
                    Target = target,
                    LastPosition = position,
                    LastDistance = distance,
                    LastProgressAt = now
                };
                ExplicitCommandPursuitStates[summonId] = state;
                return true;
            }

            bool progressed = distance <= ExplicitCommandEngagementDistance
                || (position - state.LastPosition).sqrMagnitude
                    >= ExplicitCommandProgressDistance
                        * ExplicitCommandProgressDistance
                || state.LastDistance - distance
                    >= ExplicitCommandProgressDistance;
            if (progressed)
            {
                state.LastPosition = position;
                state.LastDistance = distance;
                state.LastProgressAt = now;
                state.NextPathCheckAt = 0.0f;
                state.UnreachableSamples = 0;
                return true;
            }
            if (now - state.LastProgressAt < ExplicitCommandStallSeconds
                || now < state.NextPathCheckAt
                || AstarPath.active == null)
            {
                return true;
            }
            if (!TryConsumeFrameBudget(
                    ref _pathWorkBudgetFrame,
                    ref _pathChecksThisFrame,
                    MaximumPathChecksPerFrame))
            {
                return true;
            }

            state.NextPathCheckAt = now
                + ExplicitCommandPathCheckIntervalSeconds;
            Pathfinding.GraphNode sourceNode = AstarPath.active.GetNearest(
                position,
                Pathfinding.NNConstraint.Walkable).node;
            Pathfinding.GraphNode targetNode = AstarPath.active.GetNearest(
                target.Coords,
                Pathfinding.NNConstraint.Walkable).node;
            bool pathPossible = sourceNode != null
                && targetNode != null
                && Pathfinding.PathUtilities.IsPathPossible(
                    sourceNode,
                    targetNode);
            bool directlyVisible = Hero.Current != null
                && HasAutonomousTargetLineOfSightFrom(
                    summon.ParentModel,
                    Hero.Current,
                    target);
            if (pathPossible || directlyVisible)
            {
                state.UnreachableSamples = 0;
                state.LastProgressAt = now;
                if (IsWithinNativeSummonTargetRetentionRange(
                        Hero.Current,
                        target))
                {
                    SetOwnedTargetOverride(summon, target, 10, true);
                    summon.ParentModel.ForceAddCombatTarget(
                        target,
                        recalculateTarget: true);
                }
                return true;
            }

            state.UnreachableSamples++;
            if (state.UnreachableSamples
                < ExplicitCommandMaximumUnreachableSamples)
            {
                return true;
            }

            ReleaseExplicitCommandTarget(
                summon,
                target,
                "unreachable after " + state.UnreachableSamples.ToString()
                    + " failed path checks");
            return false;
        }

        private static bool HasActivePriorityTarget(NpcHeroSummon summon)
        {
            if (HasExplicitCommandTarget(summon))
            {
                return true;
            }
            if (summon == null || summon.ParentModel == null)
            {
                return false;
            }
            string summonId = ((Model)summon).ID;
            NpcElement target;
            if (!AutonomousTargetOverrides.TryGetValue(
                    summonId,
                    out target)
                || target == null
                || target.HasBeenDiscarded
                || !target.IsAlive
                || target.IsUnconscious
                || !WithFactionUtils.WantToFight(summon.ParentModel, target))
            {
                return false;
            }
            if (!IsTargetWithinOwnedRangeGrace(summon, target))
            {
                ClearAutonomousTargetOverride(summon);
                return false;
            }
            if (IsWithinNativeSummonTargetRetentionRange(Hero.Current, target))
            {
                SetOwnedTargetOverride(summon, target, 5, false);
                ReassertOwnedCombatTarget(summon, target);
            }
            return true;
        }

        private static void SetExplicitCommandTarget(
            NpcHeroSummon summon,
            NpcElement target)
        {
            string summonId = ((Model)summon).ID;
            NpcElement previousTarget;
            ExplicitCommandTargets.TryGetValue(
                summonId,
                out previousTarget);
            RemoveAwarenessTargetsForSummon(
                summon,
                target,
                preserveOwnedOverride: true);
            ExplicitCommandTargets[summonId] = target;
            ExplicitCommandPursuitStates[summonId] =
                new ExplicitCommandPursuitState
                {
                    Target = target,
                    LastPosition = summon.ParentModel.Coords,
                    LastDistance = Vector3.Distance(
                        summon.ParentModel.Coords,
                        target.Coords),
                    LastProgressAt = Time.unscaledTime
                };
            SetOwnedTargetOverride(summon, target, 10, true);
            if (previousTarget != null
                && !previousTarget.HasBeenDiscarded
                && !ReferenceEquals(previousTarget, target))
            {
                summon.ParentModel.RemoveCombatTarget(previousTarget);
            }
            if (summon.ParentModel.NpcAI != null)
            {
                if (summon.ParentModel.NpcAI.InCombat)
                {
                    summon.ParentModel.ForceAddCombatTarget(
                        target,
                        recalculateTarget: true);
                }
                else
                {
                    summon.ParentModel.NpcAI.EnterCombatWith(
                        target,
                        forceChange: true);
                }
            }
            RegisterReciprocalServantThreat(summon, target);
        }

        private static bool TrySetPassiveSharedTarget(
            NpcHeroSummon summon,
            NpcElement target)
        {
            if (summon == null
                || summon.ParentModel == null
                || target == null
                || target.HasBeenDiscarded
                || !target.IsAlive
                || HasExplicitCommandTarget(summon))
            {
                return false;
            }
            NpcElement owner = summon.ParentModel;
            RemoveAwarenessTargetsForSummon(summon);
            if (!owner.ForceAddCombatTarget(target, recalculateTarget: true))
            {
                return false;
            }
            string summonId = ((Model)summon).ID;
            float now = Time.unscaledTime;
            AwarenessTargets[summonId] = new AwarenessTargetRecord
            {
                SummonId = summonId,
                Summon = owner,
                Target = target,
                LastSeenAt = now,
                SelectedAt = now
            };
            if (owner.NpcAI != null && !owner.NpcAI.InCombat)
            {
                owner.NpcAI.EnterCombatWith(target, forceChange: true);
            }
            SetAutonomousTargetOverride(summon, target);
            return true;
        }

        private static void LogSummonControlState(
            NpcHeroSummon summon,
            string trigger)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || plugin.Diagnostics == null
                || !plugin.Diagnostics.Value
                || summon == null
                || summon.ParentModel == null
                || summon.ParentModel.HasBeenDiscarded)
            {
                return;
            }

            string summonId = ((Model)summon).ID;
            NpcElement explicitTarget;
            ExplicitCommandTargets.TryGetValue(
                summonId,
                out explicitTarget);
            NpcElement autonomousTarget;
            AutonomousTargetOverrides.TryGetValue(
                summonId,
                out autonomousTarget);
            OwnedTargetOverrideState targetOverride;
            TryGetOwnedTargetOverride(summon, out targetOverride);
            ICharacter currentTarget = summon.ParentModel.GetCurrentTarget();
            bool overridePresent = targetOverride != null
                && targetOverride.Element != null
                && !targetOverride.Element.HasBeenDiscarded;
            bool overrideActive = overridePresent
                && !targetOverride.Element.TemporarilyDisabled;
            float overrideDistance = targetOverride == null
                    || targetOverride.Target == null
                    || Hero.Current == null
                ? -1.0f
                : Vector3.Distance(
                    Hero.Current.Coords,
                    targetOverride.Target.Coords);
            string signature =
                SoulProgressionRuntime.GetSummonBehavior()
                + "|held=" + IsHeld(summon)
                + "|combat=" + (summon.ParentModel.NpcAI != null
                    && summon.ParentModel.NpcAI.InCombat)
                + "|current=" + GetControlTargetId(currentTarget)
                + "|explicit=" + GetControlTargetId(explicitTarget)
                + "|autonomous=" + GetControlTargetId(autonomousTarget)
                + "|overridePresent=" + overridePresent
                + "|overrideActive=" + overrideActive
                + "|overrideMode=" + (targetOverride == null
                    ? "none"
                    : targetOverride.Explicit ? "explicit" : "autonomous")
                + "|overrideTarget=" + GetControlTargetId(
                    targetOverride == null ? null : targetOverride.Target)
                + "|overrideDistance=" + (overrideDistance < 0.0f
                    ? "none"
                    : overrideDistance.ToString("0.0"));
            string previousSignature;
            if (LastControlDiagnosticBySummon.TryGetValue(
                    summonId,
                    out previousSignature)
                && string.Equals(
                    previousSignature,
                    signature,
                    StringComparison.Ordinal))
            {
                return;
            }

            float now = Time.unscaledTime;
            float nextDiagnosticAt;
            if (NextControlDiagnosticBySummon.TryGetValue(
                    summonId,
                    out nextDiagnosticAt)
                && now < nextDiagnosticAt)
            {
                return;
            }
            LastControlDiagnosticBySummon[summonId] = signature;
            NextControlDiagnosticBySummon[summonId] = now
                + ControlDiagnosticMinimumIntervalSeconds;
            plugin.LogDiagnostic(
                "Summon control: summon=" + summonId
                + "; trigger=" + trigger
                + "; " + signature + ".");
        }

        private static string GetControlTargetId(ICharacter target)
        {
            Model model = target as Model;
            return target == null
                ? "none"
                : model == null
                    ? target.GetType().Name
                    : model.ID;
        }

        private static NpcHeroSummon[] GetFormationHost(Hero hero)
        {
            if (!ReferenceEquals(_formationHostCacheHero, hero)
                || Time.unscaledTime >= _formationHostCacheExpiresAt)
            {
                _formationHostCacheHero = hero;
                FormationHostBuildBuffer.Clear();
                if (hero != null)
                {
                    foreach (NpcHeroSummon candidate
                        in World.All<NpcHeroSummon>())
                    {
                        if (IsOwnedSummon(candidate, hero))
                        {
                            FormationHostBuildBuffer.Add(candidate);
                        }
                    }
                }
                FormationHostBuildBuffer.Sort(
                    FormationHostIdComparer.Instance);
                bool membershipChanged = _formationHostCache.Length
                    != FormationHostBuildBuffer.Count;
                if (!membershipChanged)
                {
                    for (int index = 0;
                        index < FormationHostBuildBuffer.Count;
                        index++)
                    {
                        if (!ReferenceEquals(
                                _formationHostCache[index],
                                FormationHostBuildBuffer[index]))
                        {
                            membershipChanged = true;
                            break;
                        }
                    }
                }
                if (membershipChanged)
                {
                    _formationHostCache =
                        new NpcHeroSummon[FormationHostBuildBuffer.Count];
                    FormationHostBuildBuffer.CopyTo(_formationHostCache);
                }
                FormationHostBuildBuffer.Clear();
                SummonFormationCoordinator.Synchronize(
                    hero,
                    _formationHostCache);
                _formationHostCacheExpiresAt = Time.unscaledTime
                    + FormationHostFallbackRefreshSeconds;
            }
            return _formationHostCache;
        }

        private static void InvalidateFormationHostCache()
        {
            _formationHostCacheHero = null;
            _formationHostCacheExpiresAt = 0.0f;
        }

        private static Vector3 GetGuardIdleAnchor(
            NpcHeroSummon summon,
            out float anchorTolerance,
            out bool gentleIdleMovement)
        {
            anchorTolerance = GuardAnchorTolerance;
            gentleIdleMovement = false;
            Hero hero = Hero.Current;
            Vector3 liveAnchor = GetGuardAnchor(summon);
            if (summon == null || summon.ParentModel == null || hero == null)
            {
                return liveAnchor;
            }

            string summonId = ((Model)summon).ID;
            IdleExcursionState state;
            if (!GuardIdleStates.TryGetValue(summonId, out state))
            {
                state = new IdleExcursionState();
                GuardIdleStates[summonId] = state;
            }

            bool heroMoving = IsFormationLeaderMoving(hero);
            bool hostInCombat = IsHostInCombat(hero);
            if (hostInCombat)
            {
                CancelGuardIdleMovement(summonId, state);
                state.NextWanderAt = 0.0f;
            }
            if (heroMoving)
            {
                state.FormationAnchor = liveAnchor;
                state.HeroOrigin = hero.Coords;
                state.HasAnchor = true;
                CancelGuardIdleMovement(summonId, state);
                state.NextWanderAt = 0.0f;
                return liveAnchor;
            }

            if (!state.HasAnchor
                || (hero.Coords - state.HeroOrigin).sqrMagnitude
                    > GuardAnchorRebaseDistance * GuardAnchorRebaseDistance)
            {
                CancelGuardIdleMovement(summonId, state);
                state.FormationAnchor = liveAnchor;
                state.HeroOrigin = hero.Coords;
                state.HasAnchor = true;
                ScheduleNextGuardIdleWander(state);
            }

            if (hostInCombat)
            {
                return state.FormationAnchor;
            }

            if (GetIdleMovementAmount() <= 0.001f)
            {
                CancelGuardIdleMovement(summonId, state);
                state.NextWanderAt = 0.0f;
                return state.FormationAnchor;
            }

            float now = Time.unscaledTime;
            if (state.Wandering)
            {
                if (now < state.WanderEndsAt)
                {
                    anchorTolerance = GuardIdleMovementTolerance;
                    gentleIdleMovement = true;
                    return state.WanderDestination;
                }
                state.Wandering = false;
                state.Returning = true;
                state.ReturnEndsAt = now + GuardIdleReturnTimeoutSeconds;
            }

            if (state.Returning)
            {
                if ((summon.ParentModel.Coords - state.FormationAnchor).sqrMagnitude
                        > GuardIdleReturnTolerance * GuardIdleReturnTolerance
                    && now < state.ReturnEndsAt)
                {
                    anchorTolerance = GuardIdleMovementTolerance;
                    gentleIdleMovement = true;
                    return state.FormationAnchor;
                }
                state.Returning = false;
                ReleaseGuardIdleMover(summonId);
                ScheduleNextGuardIdleWander(state);
            }

            if (state.NextWanderAt <= 0.0f)
            {
                ScheduleNextGuardIdleWander(state);
            }
            if (now >= state.NextWanderAt
                && now >= _nextIdleHostAttemptAt
                && (string.IsNullOrEmpty(_guardIdleMoverId)
                    || string.Equals(
                        _guardIdleMoverId,
                        summonId,
                        StringComparison.Ordinal)))
            {
                _nextIdleHostAttemptAt = now
                    + GuardIdleHostAttemptCooldownSeconds;
                Vector3 destination;
                if (TryFindIdleDestination(
                        state.FormationAnchor,
                        GetGuardIdleWanderRadius(),
                        Vector3.zero,
                        0.0f,
                        out destination))
                {
                    _guardIdleMoverId = summonId;
                    state.WanderDestination = destination;
                    state.WanderEndsAt = now + UnityEngine.Random.Range(
                        GuardIdleMinimumLingerSeconds,
                        GuardIdleMaximumLingerSeconds);
                    state.Wandering = true;
                    anchorTolerance = GuardIdleMovementTolerance;
                    gentleIdleMovement = true;
                    return destination;
                }
                ScheduleNextGuardIdleWander(state);
            }
            return state.FormationAnchor;
        }

        private static float GetGuardIdleWanderRadius()
        {
            float mastery = Mathf.Clamp01(
                SoulProgressionRuntime.GetNecromanticPower() / 100.0f);
            return Mathf.Lerp(
                GuardIdleNoviceWanderRadius,
                GuardIdleMasterWanderRadius,
                mastery) * GetIdleMovementAmount();
        }

        private static void ScheduleNextGuardIdleWander(IdleExcursionState state)
        {
            if (state == null)
            {
                return;
            }
            float mastery = Mathf.Clamp01(
                SoulProgressionRuntime.GetNecromanticPower() / 100.0f);
            float minimum = Mathf.Lerp(
                GuardIdleNoviceMinimumStillSeconds,
                GuardIdleMasterMinimumStillSeconds,
                mastery);
            float maximum = Mathf.Lerp(
                GuardIdleNoviceMaximumStillSeconds,
                GuardIdleMasterMaximumStillSeconds,
                mastery);
            float amount = Math.Max(0.1f, GetIdleMovementAmount());
            state.NextWanderAt = Time.unscaledTime
                + (UnityEngine.Random.Range(minimum, maximum) / amount);
        }

        private static bool TryFindIdleDestination(
            Vector3 formationAnchor,
            float maximumRadius,
            Vector3 minimumDistanceOrigin,
            float minimumDistance,
            out Vector3 destination)
        {
            destination = formationAnchor;
            if (AstarPath.active == null)
            {
                return false;
            }
            Pathfinding.GraphNode anchorNode = AstarPath.active.GetNearest(
                formationAnchor,
                Pathfinding.NNConstraint.Walkable).node;
            if (anchorNode == null)
            {
                return false;
            }

            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = UnityEngine.Random.Range(0.0f, 360.0f);
                float radius = UnityEngine.Random.Range(
                    maximumRadius * 0.55f,
                    maximumRadius);
                Vector3 candidate = formationAnchor
                    + (Quaternion.AngleAxis(angle, Vector3.up)
                        * Vector3.forward * radius);
                Pathfinding.NNInfo nearest = AstarPath.active.GetNearest(
                    candidate,
                    Pathfinding.NNConstraint.Walkable);
                Vector3 snapped = nearest.position;
                if (nearest.node == null
                    || !Pathfinding.PathUtilities.IsPathPossible(
                        anchorNode,
                        nearest.node)
                    || (snapped - candidate).sqrMagnitude > 0.25f
                    || (snapped - formationAnchor).sqrMagnitude
                        > maximumRadius * maximumRadius
                    || (minimumDistance > 0.0f
                        && (snapped - minimumDistanceOrigin).sqrMagnitude
                            < minimumDistance * minimumDistance)
                    || Math.Abs(snapped.y - formationAnchor.y) > 2.0f)
                {
                    continue;
                }
                destination = snapped;
                return true;
            }
            return false;
        }

        private static void CancelGuardIdleMovement(
            string summonId,
            IdleExcursionState state)
        {
            if (state != null)
            {
                state.Wandering = false;
                state.Returning = false;
            }
            ReleaseGuardIdleMover(summonId);
        }

        private static void ClearGuardIdleState(string summonId)
        {
            if (string.IsNullOrEmpty(summonId))
            {
                return;
            }
            IdleExcursionState state;
            if (GuardIdleStates.TryGetValue(summonId, out state))
            {
                CancelGuardIdleMovement(summonId, state);
                GuardIdleStates.Remove(summonId);
            }
        }

        private static void ReleaseGuardIdleMover(string summonId)
        {
            if (string.Equals(
                    _guardIdleMoverId,
                    summonId,
                    StringComparison.Ordinal))
            {
                _guardIdleMoverId = null;
            }
        }

        private static Vector3 GetHuntIdleAnchor(
            NpcHeroSummon summon,
            out float anchorTolerance,
            out bool gentleIdleMovement)
        {
            anchorTolerance = HuntAnchorTolerance;
            gentleIdleMovement = false;
            Hero hero = Hero.Current;
            Vector3 liveAnchor = GetHuntAnchor(summon);
            if (summon == null || summon.ParentModel == null || hero == null)
            {
                return liveAnchor;
            }

            string summonId = ((Model)summon).ID;
            IdleExcursionState state;
            if (!HuntIdleStates.TryGetValue(summonId, out state))
            {
                state = new IdleExcursionState();
                HuntIdleStates[summonId] = state;
            }

            bool heroMoving = IsFormationLeaderMoving(hero);
            bool hostInCombat = IsHostInCombat(hero);
            if (hostInCombat)
            {
                CancelHuntIdleMovement(summonId, state);
                state.NextWanderAt = 0.0f;
            }
            if (heroMoving)
            {
                state.FormationAnchor = liveAnchor;
                state.HeroOrigin = hero.Coords;
                state.HasAnchor = true;
                CancelHuntIdleMovement(summonId, state);
                state.NextWanderAt = 0.0f;
                return liveAnchor;
            }

            if (!state.HasAnchor
                || (hero.Coords - state.HeroOrigin).sqrMagnitude
                    > HuntAnchorRebaseDistance * HuntAnchorRebaseDistance)
            {
                CancelHuntIdleMovement(summonId, state);
                state.FormationAnchor = liveAnchor;
                state.HeroOrigin = hero.Coords;
                state.HasAnchor = true;
                ScheduleNextHuntIdleWander(state);
            }

            if (hostInCombat)
            {
                return state.FormationAnchor;
            }

            float idleMovementAmount = GetIdleMovementAmount();
            if (idleMovementAmount <= 0.001f)
            {
                CancelHuntIdleMovement(summonId, state);
                state.NextWanderAt = 0.0f;
                return state.FormationAnchor;
            }

            float now = Time.unscaledTime;
            if (state.Wandering)
            {
                if (now < state.WanderEndsAt)
                {
                    anchorTolerance = HuntIdleMovementTolerance;
                    gentleIdleMovement = true;
                    return state.WanderDestination;
                }
                state.Wandering = false;
                state.Returning = true;
                state.ReturnEndsAt = now + HuntIdleReturnTimeoutSeconds;
            }

            if (state.Returning)
            {
                if ((summon.ParentModel.Coords - state.FormationAnchor).sqrMagnitude
                        > HuntIdleReturnTolerance * HuntIdleReturnTolerance
                    && now < state.ReturnEndsAt)
                {
                    anchorTolerance = HuntIdleMovementTolerance;
                    gentleIdleMovement = true;
                    return state.FormationAnchor;
                }
                state.Returning = false;
                HuntIdleMoverIds.Remove(summonId);
                ScheduleNextHuntIdleWander(state);
            }

            if (state.NextWanderAt <= 0.0f)
            {
                ScheduleNextHuntIdleWander(state);
            }
            int maximumWanderers = GetFormationHost(hero).Length
                    >= HuntMultipleWandererHostSize
                ? HuntMaximumConcurrentWanderers
                : 1;
            if (now >= state.NextWanderAt
                && now >= _nextIdleHostAttemptAt
                && (HuntIdleMoverIds.Contains(summonId)
                    || HuntIdleMoverIds.Count < maximumWanderers))
            {
                _nextIdleHostAttemptAt = now + GuardIdleHostAttemptCooldownSeconds;
                Vector3 destination;
                if (TryFindIdleDestination(
                        state.FormationAnchor,
                        HuntIdleWanderRadius * idleMovementAmount,
                        hero.Coords,
                        HuntIdleMinimumHeroDistance,
                        out destination))
                {
                    HuntIdleMoverIds.Add(summonId);
                    state.WanderDestination = destination;
                    state.WanderEndsAt = now + UnityEngine.Random.Range(
                        HuntIdleMinimumLingerSeconds,
                        HuntIdleMaximumLingerSeconds);
                    state.Wandering = true;
                    anchorTolerance = HuntIdleMovementTolerance;
                    gentleIdleMovement = true;
                    return destination;
                }
                ScheduleNextHuntIdleWander(state);
            }
            return state.FormationAnchor;
        }

        private static void ScheduleNextHuntIdleWander(IdleExcursionState state)
        {
            if (state == null)
            {
                return;
            }
            float amount = Math.Max(0.1f, GetIdleMovementAmount());
            state.NextWanderAt = Time.unscaledTime
                + (UnityEngine.Random.Range(
                    HuntIdleMinimumStillSeconds,
                    HuntIdleMaximumStillSeconds) / amount);
        }

        private static float GetIdleMovementAmount()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin == null || plugin.IdleMovementAmount == null
                ? 1.0f
                : Mathf.Clamp(plugin.IdleMovementAmount.Value, 0.0f, 2.0f);
        }

        private static void CancelHuntIdleMovement(
            string summonId,
            IdleExcursionState state)
        {
            if (state != null)
            {
                state.Wandering = false;
                state.Returning = false;
            }
            HuntIdleMoverIds.Remove(summonId);
        }

        private static void ClearHuntIdleState(string summonId)
        {
            if (string.IsNullOrEmpty(summonId))
            {
                return;
            }
            IdleExcursionState state;
            if (HuntIdleStates.TryGetValue(summonId, out state))
            {
                CancelHuntIdleMovement(summonId, state);
                HuntIdleStates.Remove(summonId);
            }
        }

        private static void ClearIdleMovementState(string summonId)
        {
            ClearGuardIdleState(summonId);
            ClearHuntIdleState(summonId);
        }

        private static Vector3 GetRecoveredFormationAnchor(
            NpcHeroSummon summon,
            Vector3 desiredAnchor,
            float anchorTolerance)
        {
            FormationPurpose purpose =
                SoulProgressionRuntime.GetSummonBehavior()
                    == SummonBehavior.Hunt
                    ? FormationPurpose.Hunt
                    : FormationPurpose.Guard;
            return SummonFormationCoordinator.ResolveAnchor(
                summon,
                purpose,
                desiredAnchor,
                anchorTolerance,
                FormationRecoveryBlockedSeconds,
                FormationRecoveryFallbackSeconds,
                FormationRecoveryProbeDistance,
                FormationRecoveryMaximumSnapDistance,
                false,
                anchorTolerance);
        }

        private static void ResetFormationLeaderMotion()
        {
            _formationLeaderMotionFrame = -1;
            _formationLeaderMotionHero = null;
            _formationLeaderAnchor = Vector3.zero;
            _hasFormationLeaderAnchor = false;
            _formationLeaderMoving = false;
            _formationLeaderMovementOrigin = Vector3.zero;
            _formationLeaderMovementStartedAt = -1.0f;
            _formationLeaderStoppedAt = -1.0f;
        }

        private static void UpdateFormationLeaderMotion(Hero hero)
        {
            if (hero == null)
            {
                ResetFormationLeaderMotion();
                return;
            }
            if (!ReferenceEquals(_formationLeaderMotionHero, hero)
                || !_hasFormationLeaderAnchor)
            {
                _formationLeaderMotionHero = hero;
                _formationLeaderAnchor = hero.Coords;
                _hasFormationLeaderAnchor = true;
                _formationLeaderMoving = false;
                _formationLeaderMovementOrigin = hero.Coords;
                _formationLeaderMovementStartedAt = -1.0f;
                _formationLeaderStoppedAt = -1.0f;
                _formationLeaderMotionFrame = Time.frameCount;
                return;
            }
            if (_formationLeaderMotionFrame == Time.frameCount)
            {
                return;
            }
            _formationLeaderMotionFrame = Time.frameCount;

            float now = Time.unscaledTime;
            float speed = hero.HorizontalVelocity.magnitude;
            if (_formationLeaderMoving)
            {
                UpdateElasticFormationLeaderAnchor(hero);
                if (speed >= FormationLeaderMovementStopSpeed)
                {
                    _formationLeaderStoppedAt = -1.0f;
                }
                else if (_formationLeaderStoppedAt < 0.0f)
                {
                    _formationLeaderStoppedAt = now;
                }
                else if (now - _formationLeaderStoppedAt
                    >= FormationLeaderSettleSeconds)
                {
                    _formationLeaderMoving = false;
                    _formationLeaderMovementOrigin = hero.Coords;
                    _formationLeaderMovementStartedAt = -1.0f;
                    _formationLeaderStoppedAt = -1.0f;
                }
                return;
            }

            if (speed >= FormationLeaderMovementStopSpeed)
            {
                if (_formationLeaderMovementStartedAt < 0.0f)
                {
                    _formationLeaderMovementStartedAt = now;
                    _formationLeaderMovementOrigin = hero.Coords;
                }
            }
            else
            {
                _formationLeaderMovementStartedAt = -1.0f;
            }

            bool traveledBeyondDeadZone =
                (hero.Coords - _formationLeaderAnchor).sqrMagnitude
                    >= FormationLeaderTravelDeadZone
                        * FormationLeaderTravelDeadZone;
            bool sustainedMovement = _formationLeaderMovementStartedAt >= 0.0f
                && now - _formationLeaderMovementStartedAt
                    >= FormationLeaderMovementStartSeconds;
            if (traveledBeyondDeadZone || sustainedMovement)
            {
                _formationLeaderMoving = true;
                UpdateElasticFormationLeaderAnchor(hero);
                _formationLeaderStoppedAt = -1.0f;
            }
        }

        private static void UpdateElasticFormationLeaderAnchor(Hero hero)
        {
            Vector3 offset = hero.Coords - _formationLeaderAnchor;
            offset.y = 0.0f;
            if (offset.sqrMagnitude
                > FormationLeaderElasticOffset * FormationLeaderElasticOffset)
            {
                _formationLeaderAnchor = hero.Coords
                    - offset.normalized * FormationLeaderElasticOffset;
            }
            else
            {
                _formationLeaderAnchor.y = hero.Coords.y;
            }
        }

        private static bool IsFormationLeaderMoving(Hero hero)
        {
            UpdateFormationLeaderMotion(hero);
            return _formationLeaderMoving;
        }

        private static Vector3 GetFormationLeaderAnchor(Hero hero)
        {
            UpdateFormationLeaderMotion(hero);
            return _hasFormationLeaderAnchor
                ? _formationLeaderAnchor
                : hero == null ? Vector3.zero : hero.Coords;
        }

        private static Vector3 GetHuntAnchor(NpcHeroSummon summon)
        {
            Hero hero = Hero.Current;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (hero == null || summon == null)
            {
                return summon == null || summon.ParentModel == null
                    ? Vector3.zero
                    : summon.ParentModel.Coords;
            }

            GetFormationHost(hero);
            return SummonFormationCoordinator.GetRadialAnchor(
                summon,
                FormationPurpose.Hunt,
                GetFormationLeaderAnchor(hero),
                Vector3.forward,
                plugin != null && plugin.HuntFormationDistance != null
                    ? plugin.HuntFormationDistance.Value
                    : DefaultHuntFormationDistance,
                HuntFormationRingSpacing,
                HuntFormationSlotsPerRing);
        }

        private static Vector3 GetBulwarkAnchor(NpcHeroSummon summon)
        {
            Hero hero = Hero.Current;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (hero == null || summon == null)
            {
                return summon == null || summon.ParentModel == null
                    ? Vector3.zero
                    : summon.ParentModel.Coords;
            }
            EnsureFormationFacingHero(hero);
            GetFormationHost(hero);

            bool advance = IsBulwarkAdvanceActive(hero);
            Vector3 leaderAnchor = advance
                ? hero.Coords + hero.HorizontalVelocity
                    * BulwarkAdvancePredictionSeconds
                : GetFormationLeaderAnchor(hero);
            Vector3 formationForward;
            if (advance)
            {
                UpdateBulwarkFacing(hero);
                formationForward = _bulwarkForward;
            }
            else
            {
                formationForward = GetGuardFormationForward(hero);
            }

            FormationPurpose purpose = advance
                ? FormationPurpose.BulwarkAdvance
                : FormationPurpose.BulwarkCloseGuard;
            Vector3 desiredAnchor =
                SummonFormationCoordinator.GetRadialAnchor(
                    summon,
                    purpose,
                    leaderAnchor,
                    formationForward,
                    advance
                        ? plugin != null && plugin.BulwarkAdvanceDistance != null
                            ? plugin.BulwarkAdvanceDistance.Value
                            : DefaultBulwarkAdvanceDistance
                        : plugin != null
                            && plugin.BulwarkCloseGuardDistance != null
                                ? plugin.BulwarkCloseGuardDistance.Value
                                : DefaultBulwarkCloseGuardDistance,
                    BulwarkFormationRingSpacing,
                    advance
                        ? BulwarkFormationSlotsPerRing
                        : BulwarkCloseGuardSlotsPerRing);
            return SummonFormationCoordinator.ResolveAnchor(
                summon,
                purpose,
                desiredAnchor,
                advance
                    ? BulwarkAdvanceAnchorTolerance
                    : BulwarkAnchorTolerance,
                advance
                    ? BulwarkAdvanceBlockedSeconds
                    : FormationRecoveryBlockedSeconds,
                advance
                    ? BulwarkAdvanceFallbackSeconds
                    : FormationRecoveryFallbackSeconds,
                advance
                    ? BulwarkAdvanceFallbackProbeRadius
                    : FormationRecoveryProbeDistance,
                advance
                    ? BulwarkAdvanceMaximumAnchorSnapDistance
                    : FormationRecoveryMaximumSnapDistance,
                advance,
                advance
                    ? BulwarkAdvanceResumeDistance
                    : BulwarkAnchorTolerance);
        }

        private static void UpdateBulwarkFacing(Hero hero)
        {
            if (hero == null)
            {
                ResetBulwarkFacingState();
                return;
            }
            EnsureFormationFacingHero(hero);
            if (ReferenceEquals(_bulwarkFacingHero, hero)
                && _bulwarkFacingFrame == Time.frameCount)
            {
                return;
            }
            if (!ReferenceEquals(_bulwarkFacingHero, hero))
            {
                _bulwarkFacingHero = hero;
                _hasBulwarkViewCandidate = false;
                _bulwarkViewCandidateSince = -1.0f;
            }
            _bulwarkFacingFrame = Time.frameCount;

            if (SoulProgressionRuntime.GetSummonBehavior()
                != SummonBehavior.Bulwark)
            {
                _hasBulwarkViewCandidate = false;
                _bulwarkViewCandidateSince = -1.0f;
                return;
            }
            if (IsBulwarkAdvanceReleaseGraceActive(hero))
            {
                _hasBulwarkViewCandidate = false;
                _bulwarkViewCandidateSince = -1.0f;
                return;
            }

            Vector3 viewForward = hero.VHeroController == null
                ? Vector3.zero
                : hero.VHeroController.transform.forward;
            VCHeroRaycaster raycaster = hero.VHeroController == null
                ? null
                : hero.VHeroController.Raycaster;
            if (raycaster != null)
            {
                Vector3 viewOrigin;
                raycaster.GetViewRay(out viewOrigin, out viewForward);
            }
            viewForward.y = 0.0f;
            if (viewForward.sqrMagnitude <= 0.0001f)
            {
                Vector3 movementForward = hero.HorizontalVelocity;
                movementForward.y = 0.0f;
                if (!_hasBulwarkForward
                    && IsFormationLeaderMoving(hero)
                    && movementForward.magnitude >= BulwarkLeaderMovingSpeed)
                {
                    _bulwarkForward = movementForward.normalized;
                    _hasBulwarkForward = true;
                }
                return;
            }
            viewForward.Normalize();

            if (!_hasBulwarkForward)
            {
                _bulwarkForward = viewForward;
                _hasBulwarkForward = true;
                return;
            }
            if (Vector3.Angle(_bulwarkForward, viewForward)
                < BulwarkCameraFacingMinimumAngle)
            {
                _hasBulwarkViewCandidate = false;
                _bulwarkViewCandidateSince = -1.0f;
                return;
            }
            if (Time.unscaledTime < _bulwarkFacingCooldownUntil)
            {
                return;
            }
            if (!_hasBulwarkViewCandidate
                || Vector3.Angle(_bulwarkViewCandidate, viewForward)
                    > BulwarkCameraFacingStabilityAngle)
            {
                _bulwarkViewCandidate = viewForward;
                _hasBulwarkViewCandidate = true;
                _bulwarkViewCandidateSince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - _bulwarkViewCandidateSince
                < BulwarkCameraFacingHoldSeconds)
            {
                return;
            }

            _bulwarkForward = viewForward;
            _bulwarkFacingCooldownUntil = Time.unscaledTime
                + BulwarkCameraFacingCooldownSeconds;
            _hasBulwarkViewCandidate = false;
            _bulwarkViewCandidateSince = -1.0f;
        }

        private static void ResetBulwarkFacingState()
        {
            _hasBulwarkForward = false;
            _bulwarkForward = Vector3.forward;
            _bulwarkFacingHero = null;
            _bulwarkFacingFrame = -1;
            _hasBulwarkViewCandidate = false;
            _bulwarkViewCandidate = Vector3.forward;
            _bulwarkViewCandidateSince = -1.0f;
            _bulwarkFacingCooldownUntil = 0.0f;
        }

        private static void UpdateBulwarkAdvanceState(
            SoulAndServicePlugin plugin,
            Hero hero)
        {
            int frame = Time.frameCount;
            if (ReferenceEquals(_bulwarkAdvanceHero, hero)
                && _bulwarkAdvanceStateFrame == frame)
            {
                return;
            }
            bool valid = plugin != null
                && plugin.IsEnabled
                && plugin.BulwarkAdvanceEnabled != null
                && plugin.BulwarkAdvanceEnabled.Value
                && SoulProgressionRuntime.GetSummonBehavior()
                    == SummonBehavior.Bulwark
                && hero != null
                && !hero.HasBeenDiscarded
                && hero.IsAlive
                && Time.timeScale > 0.0f;
            if (!valid)
            {
                ResetBulwarkAdvanceState();
                _bulwarkAdvanceStateFrame = frame;
                return;
            }
            if (_bulwarkAdvanceHero != null
                && !ReferenceEquals(_bulwarkAdvanceHero, hero))
            {
                ResetBulwarkAdvanceState();
            }
            _bulwarkAdvanceHero = hero;
            _bulwarkAdvanceStateFrame = frame;

            bool sprintHeld = IsSprintActionHeld(hero);
            if (sprintHeld)
            {
                _bulwarkAdvanceInputWasHeld = true;
                _bulwarkAdvanceReleasedUntil = 0.0f;
            }
            else if (_bulwarkAdvanceInputWasHeld)
            {
                _bulwarkAdvanceInputWasHeld = false;
                float duration = plugin.BulwarkAdvanceReleaseSeconds == null
                    ? 0.0f
                    : Mathf.Clamp(
                        plugin.BulwarkAdvanceReleaseSeconds.Value,
                        0.0f,
                        10.0f);
                _bulwarkAdvanceReleasedUntil = Time.unscaledTime + duration;
            }

            bool active = sprintHeld
                || Time.unscaledTime < _bulwarkAdvanceReleasedUntil;
            _bulwarkAdvanceWasActive = active;
        }

        private static void ResetBulwarkAdvanceState()
        {
            _bulwarkAdvanceHero = null;
            _bulwarkAdvanceStateFrame = -1;
            _bulwarkAdvanceInputWasHeld = false;
            _bulwarkAdvanceWasActive = false;
            _bulwarkAdvanceReleasedUntil = 0.0f;
        }

        private static void UpdateHuntAttackMove(SoulAndServicePlugin plugin)
        {
            if (!IsHuntAttackMoveActive())
            {
                return;
            }
            Hero hero = Hero.Current;
            if (plugin == null
                || plugin.DirectedHuntEnabled == null
                || !plugin.DirectedHuntEnabled.Value
                || SoulProgressionRuntime.GetSummonBehavior()
                    != SummonBehavior.Hunt
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || !ReferenceEquals(hero, _huntAttackMoveHero)
                || Time.timeScale <= 0.0f)
            {
                ResetHuntAttackMove();
                return;
            }

            PruneHuntAttackMoveParticipants(hero);
            if (HuntAttackMoveParticipants.Count == 0)
            {
                ResetHuntAttackMove();
                return;
            }

            float now = Time.unscaledTime;
            if (_huntAttackMovePhase == HuntAttackMovePhase.Travelling)
            {
                bool arrived = false;
                foreach (NpcHeroSummon summon
                    in HuntAttackMoveParticipants.Values)
                {
                    if (SummonFormationCoordinator.IsAtResolvedAnchor(
                            summon,
                            FormationPurpose.HuntAttackMove,
                            HuntAttackMoveAnchorTolerance))
                    {
                        arrived = true;
                        break;
                    }
                }
                if (arrived)
                {
                    _huntAttackMovePhase = HuntAttackMovePhase.Searching;
                    _huntAttackMoveSearchUntil = now
                        + HuntAttackMoveSearchSeconds;
                    plugin.LogDiagnostic(
                        "Hunt attack-move reached its search area.");
                }
                else if (now - _huntAttackMoveStartedAt
                    >= HuntAttackMoveMaximumTravelSeconds)
                {
                    plugin.LogDiagnostic(
                        "Hunt attack-move expired before reaching its search area.");
                    ResetHuntAttackMove();
                }
                return;
            }

            if (_huntAttackMovePhase == HuntAttackMovePhase.Searching
                && now >= _huntAttackMoveSearchUntil)
            {
                plugin.LogDiagnostic(
                    "Hunt attack-move search finished without a target.");
                ResetHuntAttackMove();
            }
        }

        private static bool BeginHuntAttackMove(
            SoulAndServicePlugin plugin,
            Hero hero,
            Vector3 destination)
        {
            if (plugin == null || hero == null)
            {
                return false;
            }

            ResetHuntAttackMove();
            _huntAttackMoveHero = hero;
            _huntAttackMoveOrigin = hero.Coords;
            _huntAttackMoveDestination = destination;
            _huntAttackMoveDirection = destination - hero.Coords;
            _huntAttackMoveDirection.y = 0.0f;
            if (_huntAttackMoveDirection.sqrMagnitude <= 0.0001f)
            {
                _huntAttackMoveDirection = Vector3.forward;
            }
            else
            {
                _huntAttackMoveDirection.Normalize();
            }

            NpcHeroSummon[] host = GetFormationHost(hero);
            int redirectedHunters = 0;
            int recoveredHunters = 0;
            for (int index = 0; index < host.Length; index++)
            {
                NpcHeroSummon summon = host[index];
                if (IsEligibleForHuntAttackMove(summon, hero))
                {
                    if (ExitStaleCombatForDirectedHunt(summon))
                    {
                        recoveredHunters++;
                    }
                    string summonId = ((Model)summon).ID;
                    HuntAttackMoveParticipants[summonId] = summon;
                    HuntAttackMoveSearchStates[summonId] =
                        new HuntAttackMoveSearchState();
                    ClearIdleMovementState(summonId);
                    SummonFormationCoordinator.InvalidateAppliedAnchor(summonId);
                    continue;
                }
                if (TryRedirectCombatHunterToHuntPoint(summon, hero))
                {
                    redirectedHunters++;
                }
            }
            if (HuntAttackMoveParticipants.Count > 0)
            {
                _huntAttackMovePhase = HuntAttackMovePhase.Travelling;
                _huntAttackMoveStartedAt = Time.unscaledTime;
                _huntAttackMoveSearchUntil = 0.0f;
            }
            else
            {
                ResetHuntAttackMove();
            }

            PublishCommand(
                plugin,
                SummonCommandState.Attack,
                SummonAttackCommandId,
                "Hunt",
                StandardCommandFeedbackSeconds,
                false);
            plugin.LogDiagnostic(
                "Directed " + HuntAttackMoveParticipants.Count.ToString()
                + " idle hunter(s) to a terrain attack point and redirected "
                + redirectedHunters.ToString()
                + " autonomous combat hunter(s) toward nearby hostiles; recovered "
                + recoveredHunters.ToString()
                + " targetless stale-combat hunter(s).");
            return true;
        }

        private static bool TryRedirectCombatHunterToHuntPoint(
            NpcHeroSummon summon,
            Hero hero)
        {
            if (!IsOwnedSummon(summon, hero)
                || summon.ParentModel == null
                || IsHeld(summon))
            {
                return false;
            }
            string summonId = ((Model)summon).ID;
            if (PendingRecallPlacements.ContainsKey(summonId)
                || IsRecallTargetSuppressed(summon)
                || HasExplicitCommandTarget(summon)
                || (summon.ParentModel.GetCurrentTarget() == null
                    && (summon.ParentModel.NpcAI == null
                        || !summon.ParentModel.NpcAI.InCombat)))
            {
                return false;
            }

            NpcGrid grid = World.Services == null
                ? null
                : World.Services.TryGet<NpcGrid>();
            if (grid == null)
            {
                return false;
            }

            float sightMultiplier;
            float unusedAggroMultiplier;
            ReadSteelAndBoneAwarenessMultipliers(
                out sightMultiplier,
                out unusedAggroMultiplier);
            float awarenessRange = BaseHuntAwarenessRange
                * Math.Max(
                    1.0f,
                    1.0f + SteelAndBoneTransferFraction
                        * (sightMultiplier - 1.0f));
            Pathfinding.GraphNode sourceNode = AstarPath.active == null
                ? null
                : AstarPath.active.GetNearest(
                    summon.ParentModel.Coords,
                    Pathfinding.NNConstraint.Walkable).node;
            NpcElement closestTarget = null;
            float closestDistanceSqr = float.PositiveInfinity;
            foreach (NpcElement candidate in grid.GetNpcsInSphere(
                summon.ParentModel.Coords,
                awarenessRange))
            {
                if (candidate == null
                    || candidate.HasBeenDiscarded
                    || ReferenceEquals(candidate, summon.ParentModel)
                    || !candidate.IsAlive
                    || candidate.IsUnconscious
                    || !IsWithinNativeSummonTargetAcquisitionRange(
                        hero,
                        candidate)
                    || !WithFactionUtils.WantToFight(
                        summon.ParentModel,
                        candidate)
                    || !HasAutonomousTargetLineOfSightFrom(
                        summon.ParentModel,
                        hero,
                        candidate)
                    || !IsHuntAttackMoveTargetReachable(
                        sourceNode,
                        candidate))
                {
                    continue;
                }
                float distanceSqr = (candidate.Coords
                    - _huntAttackMoveDestination).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestTarget = candidate;
                    closestDistanceSqr = distanceSqr;
                }
            }
            if (closestTarget == null)
            {
                return false;
            }

            return TrySetPassiveSharedTarget(summon, closestTarget);
        }

        private static bool IsEligibleForHuntAttackMove(
            NpcHeroSummon summon,
            Hero hero)
        {
            if (!IsOwnedSummon(summon, hero)
                || IsHeld(summon)
                || summon.ParentModel == null)
            {
                return false;
            }
            string summonId = ((Model)summon).ID;
            return !PendingRecallPlacements.ContainsKey(summonId)
                && !IsRecallTargetSuppressed(summon)
                && !HasExplicitCommandTarget(summon)
                && !HasActivePriorityTarget(summon)
                && summon.ParentModel.GetCurrentTarget() == null;
        }

        private static bool ExitStaleCombatForDirectedHunt(
            NpcHeroSummon summon)
        {
            if (summon == null
                || summon.ParentModel == null
                || summon.ParentModel.NpcAI == null
                || !summon.ParentModel.NpcAI.InCombat
                || summon.ParentModel.GetCurrentTarget() != null
                || HasExplicitCommandTarget(summon)
                || HasActivePriorityTarget(summon))
            {
                return false;
            }
            summon.ParentModel.NpcAI.ExitCombat(
                force: true,
                exitToIdle: true,
                canBeVictorious: false);
            return true;
        }

        private static bool IsHuntAttackMoveActive()
        {
            return _huntAttackMovePhase != HuntAttackMovePhase.Idle;
        }

        private static bool IsHuntAttackMoveParticipant(
            NpcHeroSummon summon)
        {
            return summon != null
                && HuntAttackMoveParticipants.ContainsKey(((Model)summon).ID);
        }

        private static void PruneHuntAttackMoveParticipants(Hero hero)
        {
            HuntAttackMoveRemovalBuffer.Clear();
            foreach (KeyValuePair<string, NpcHeroSummon> pair
                in HuntAttackMoveParticipants)
            {
                NpcHeroSummon summon = pair.Value;
                if (summon == null
                    || summon.HasBeenDiscarded
                    || !IsOwnedSummon(summon, hero)
                    || !IsEligibleForHuntAttackMove(summon, hero))
                {
                    HuntAttackMoveRemovalBuffer.Add(pair.Key);
                }
            }
            for (int index = 0;
                index < HuntAttackMoveRemovalBuffer.Count;
                index++)
            {
                RemoveHuntAttackMoveParticipant(
                    HuntAttackMoveRemovalBuffer[index]);
            }
            HuntAttackMoveRemovalBuffer.Clear();
        }

        private static void RemoveHuntAttackMoveParticipant(string summonId)
        {
            if (string.IsNullOrEmpty(summonId))
            {
                return;
            }
            bool removed = HuntAttackMoveParticipants.Remove(summonId);
            HuntAttackMoveSearchStates.Remove(summonId);
            if (!removed)
            {
                return;
            }
            SummonFormationCoordinator.Suspend(summonId);
        }

        private static bool TryGetHuntAttackMoveAnchor(
            NpcHeroSummon summon,
            out Vector3 anchor)
        {
            anchor = Vector3.zero;
            if (!IsHuntAttackMoveActive()
                || !IsHuntAttackMoveParticipant(summon)
                || summon.ParentModel == null)
            {
                return false;
            }
            if (!IsEligibleForHuntAttackMove(summon, Hero.Current))
            {
                RemoveHuntAttackMoveParticipant(((Model)summon).ID);
                return false;
            }

            string summonId = ((Model)summon).ID;
            Vector3 arrivalAnchor =
                SummonFormationCoordinator.GetRadialAnchor(
                    summon,
                    FormationPurpose.HuntAttackMove,
                    _huntAttackMoveDestination,
                    _huntAttackMoveDirection,
                    HuntAttackMoveArrivalRadius,
                    HuntAttackMoveRingSpacing,
                    HuntAttackMoveSlotsPerRing);
            Vector3 desiredAnchor = arrivalAnchor;
            HuntAttackMoveSearchState searchState;
            if (_huntAttackMovePhase == HuntAttackMovePhase.Searching
                && HuntAttackMoveSearchStates.TryGetValue(
                    summonId,
                    out searchState))
            {
                if (!searchState.Arrived
                    && SummonFormationCoordinator.IsAtResolvedAnchor(
                        summon,
                        FormationPurpose.HuntAttackMove,
                        HuntAttackMoveAnchorTolerance))
                {
                    searchState.Arrived = true;
                    searchState.NextSweepAt = 0.0f;
                }
                if (searchState.Arrived)
                {
                    float now = Time.unscaledTime;
                    bool reachedSweepDestination =
                        searchState.HasSweepDestination
                        && (summon.ParentModel.Coords
                            - searchState.SweepDestination).sqrMagnitude
                            <= HuntAttackMoveSweepArrivalTolerance
                                * HuntAttackMoveSweepArrivalTolerance;
                    if (searchState.SweepLegs
                            < HuntAttackMoveMaximumSweepLegs
                        && (now >= searchState.NextSweepAt
                            || (searchState.HasSweepDestination
                                && reachedSweepDestination)))
                    {
                        TryAssignHuntAttackMoveSweepDestination(
                            summonId,
                            searchState,
                            now);
                    }
                    if (searchState.HasSweepDestination)
                    {
                        desiredAnchor = searchState.SweepDestination;
                    }
                }
            }
            anchor = SummonFormationCoordinator.ResolveAnchor(
                summon,
                FormationPurpose.HuntAttackMove,
                desiredAnchor,
                HuntAttackMoveAnchorTolerance,
                HuntAttackMoveBlockedSeconds,
                HuntAttackMoveFallbackSeconds,
                HuntAttackMoveFallbackDistance,
                HuntAttackMoveMaximumAnchorSnapDistance,
                false,
                HuntAttackMoveAnchorTolerance);
            return true;
        }

        private static bool TryAssignHuntAttackMoveSweepDestination(
            string summonId,
            HuntAttackMoveSearchState searchState,
            float now)
        {
            for (int attempt = 0;
                attempt < HuntAttackMoveSweepCandidateAttempts;
                attempt++)
            {
                Vector3 candidate;
                if (!TryFindIdleDestination(
                        _huntAttackMoveDestination,
                        HuntAttackMoveSweepRadius,
                        Vector3.zero,
                        0.0f,
                        out candidate)
                    || IsHuntAttackMoveSweepDestinationReserved(
                        summonId,
                        candidate))
                {
                    continue;
                }
                searchState.HasSweepDestination = true;
                searchState.SweepDestination = candidate;
                searchState.NextSweepAt = now
                    + HuntAttackMoveSweepRepathSeconds;
                searchState.SweepLegs++;
                SummonFormationCoordinator.InvalidateAppliedAnchor(summonId);
                return true;
            }
            searchState.NextSweepAt = now + HuntAttackMoveSweepRetrySeconds;
            return false;
        }

        private static bool IsHuntAttackMoveSweepDestinationReserved(
            string summonId,
            Vector3 candidate)
        {
            foreach (KeyValuePair<string, HuntAttackMoveSearchState> pair
                in HuntAttackMoveSearchStates)
            {
                HuntAttackMoveSearchState other = pair.Value;
                if (string.Equals(pair.Key, summonId, StringComparison.Ordinal)
                    || other == null
                    || !other.HasSweepDestination)
                {
                    continue;
                }
                if ((other.SweepDestination - candidate).sqrMagnitude
                    < HuntAttackMoveSweepSpacing
                        * HuntAttackMoveSweepSpacing)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ResetHuntAttackMove()
        {
            foreach (string summonId in HuntAttackMoveParticipants.Keys)
            {
                SummonFormationCoordinator.Suspend(summonId);
            }
            HuntAttackMoveParticipants.Clear();
            HuntAttackMoveSearchStates.Clear();
            HuntAttackMoveRemovalBuffer.Clear();
            _huntAttackMovePhase = HuntAttackMovePhase.Idle;
            _huntAttackMoveHero = null;
            _huntAttackMoveOrigin = Vector3.zero;
            _huntAttackMoveDestination = Vector3.zero;
            _huntAttackMoveDirection = Vector3.forward;
            _huntAttackMoveStartedAt = 0.0f;
            _huntAttackMoveSearchUntil = 0.0f;
        }

        private static bool CanUseDirectedHunt(
            SoulAndServicePlugin plugin,
            Hero hero)
        {
            return plugin != null
                && plugin.IsEnabled
                && plugin.DirectedHuntEnabled != null
                && plugin.DirectedHuntEnabled.Value
                && SoulProgressionRuntime.GetSummonBehavior()
                    == SummonBehavior.Hunt
                && hero != null
                && !hero.HasBeenDiscarded
                && hero.IsAlive
                && hero.VHeroController != null
                && IsSprintActionHeld(hero)
                && GetFormationHost(hero).Length > 0;
        }

        private static bool CanShowHuntPointPrompt(
            SoulAndServicePlugin plugin,
            Hero hero)
        {
            return CanUseDirectedHunt(plugin, hero)
                && plugin.ShowDirectedHuntPreview != null
                && plugin.ShowDirectedHuntPreview.Value;
        }

        private static bool TryResolveHuntPointPreview(
            SoulAndServicePlugin plugin,
            Hero hero,
            Vector3 origin,
            Vector3 direction,
            out Vector3 destination,
            out GameObject viewObject)
        {
            destination = Vector3.zero;
            viewObject = null;
            if (!CanUseDirectedHunt(plugin, hero)
                || AstarPath.active == null)
            {
                InvalidateHuntPointPreview();
                return false;
            }

            float range = GetTargetingRange(plugin);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                HuntPointRaycastHits,
                range,
                ~0,
                QueryTriggerInteraction.Ignore);
            RaycastHit bestHit = default(RaycastHit);
            float bestDistance = float.PositiveInfinity;
            int count = Math.Min(hitCount, HuntPointRaycastHits.Length);
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = HuntPointRaycastHits[index];
                if (hit.collider == null
                    || hit.distance < HuntPointMinimumDistance
                    || hit.distance >= bestDistance
                    || hit.normal.y < 0.45f
                    || ResolveHitLocation(hit.collider) != null)
                {
                    continue;
                }
                bestHit = hit;
                bestDistance = hit.distance;
            }
            if (bestHit.collider == null)
            {
                InvalidateHuntPointPreview();
                return false;
            }

            bool sameCandidate = ReferenceEquals(
                    _huntPointPreviewCollider,
                    bestHit.collider)
                && (_huntPointPreviewHit - bestHit.point).sqrMagnitude
                    <= HuntPointPreviewMoveDistance
                        * HuntPointPreviewMoveDistance
                && Time.unscaledTime < _huntPointPreviewExpiresAt;
            if (sameCandidate)
            {
                if (!_huntPointPreviewValid)
                {
                    return false;
                }
                destination = _huntPointPreviewDestination;
                viewObject = bestHit.collider.gameObject;
                return true;
            }

            _huntPointPreviewCollider = bestHit.collider;
            _huntPointPreviewHit = bestHit.point;
            _huntPointPreviewExpiresAt = Time.unscaledTime
                + HuntPointPreviewCacheSeconds;
            _huntPointPreviewValid = false;

            Pathfinding.NNInfo nearest = AstarPath.active.GetNearest(
                bestHit.point,
                Pathfinding.NNConstraint.Walkable);
            Pathfinding.GraphNode heroNode = AstarPath.active.GetNearest(
                hero.Coords,
                Pathfinding.NNConstraint.Walkable).node;
            if (nearest.node == null
                || heroNode == null
                || !Pathfinding.PathUtilities.IsPathPossible(
                    heroNode,
                    nearest.node)
                || (nearest.position - bestHit.point).sqrMagnitude
                    > HuntPointMaximumNavSnapDistance
                        * HuntPointMaximumNavSnapDistance
                || Math.Abs(nearest.position.y - hero.Coords.y) > 8.0f
                || (nearest.position - hero.Coords).sqrMagnitude
                    < HuntPointMinimumDistance * HuntPointMinimumDistance)
            {
                return false;
            }

            _huntPointPreviewValid = true;
            _huntPointPreviewDestination = nearest.position;
            destination = nearest.position;
            viewObject = bestHit.collider.gameObject;
            return true;
        }

        private static void InvalidateHuntPointPreview()
        {
            _huntPointPreviewCollider = null;
            _huntPointPreviewHit = Vector3.zero;
            _huntPointPreviewDestination = Vector3.zero;
            _huntPointPreviewExpiresAt = 0.0f;
            _huntPointPreviewValid = false;
        }

        private static bool IsHuntAttackMoveTargetReachable(
            Pathfinding.GraphNode sourceNode,
            NpcElement target)
        {
            if (AstarPath.active == null)
            {
                return true;
            }
            if (sourceNode == null || target == null)
            {
                return false;
            }
            Pathfinding.GraphNode targetNode = AstarPath.active.GetNearest(
                target.Coords,
                Pathfinding.NNConstraint.Walkable).node;
            return targetNode != null
                && Pathfinding.PathUtilities.IsPathPossible(
                    sourceNode,
                    targetNode);
        }

        private static Vector3 GetGuardAnchor(NpcHeroSummon summon)
        {
            Hero hero = Hero.Current;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (hero == null || summon == null)
            {
                return summon == null || summon.ParentModel == null
                    ? Vector3.zero
                    : summon.ParentModel.Coords;
            }
            EnsureFormationFacingHero(hero);

            GetFormationHost(hero);
            Vector3 formationForward = GetGuardFormationForward(hero);
            return SummonFormationCoordinator.GetRadialAnchor(
                summon,
                FormationPurpose.Guard,
                GetFormationLeaderAnchor(hero),
                formationForward,
                plugin != null && plugin.GuardFormationDistance != null
                    ? plugin.GuardFormationDistance.Value
                    : DefaultGuardFormationDistance,
                GuardFormationRingSpacing,
                5);
        }

        private static Vector3 GetGuardFormationForward(Hero hero)
        {
            if (!_hasGuardForward)
            {
                Vector3 initialForward = hero.VHeroController == null
                    ? Vector3.forward
                    : hero.VHeroController.transform.forward;
                initialForward.y = 0.0f;
                _guardForward = initialForward.sqrMagnitude <= 0.0001f
                    ? Vector3.forward
                    : initialForward.normalized;
                _guardFacingDesired = _guardForward;
                _guardFacingCandidate = _guardForward;
                _guardFacingCandidateStartedAt = -1.0f;
                _guardFacingUpdateFrame = -1;
                _hasGuardForward = true;
            }
            UpdateGuardFormationFacing(hero);
            return _guardForward;
        }

        private static void UpdateGuardFormationFacing(Hero hero)
        {
            if (_guardFacingUpdateFrame == Time.frameCount)
            {
                return;
            }
            _guardFacingUpdateFrame = Time.frameCount;

            Vector3 movementForward = hero.HorizontalVelocity;
            movementForward.y = 0.0f;
            if (IsFormationLeaderMoving(hero)
                && movementForward.magnitude >= GuardLeaderMovingSpeed)
            {
                Vector3 candidate = movementForward.normalized;
                float now = Time.unscaledTime;
                if (_guardFacingCandidateStartedAt < 0.0f)
                {
                    _guardFacingCandidate = candidate;
                    _guardFacingCandidateStartedAt =
                        _formationLeaderMovementStartedAt >= 0.0f
                            ? _formationLeaderMovementStartedAt
                            : now;
                }
                else if (Vector3.Angle(_guardFacingCandidate, candidate)
                    > FormationFacingStabilityAngle)
                {
                    _guardFacingCandidate = candidate;
                    _guardFacingCandidateStartedAt = now;
                }

                Vector3 committedTravel = hero.Coords
                    - _formationLeaderMovementOrigin;
                committedTravel.y = 0.0f;
                bool traveledFarEnough = committedTravel.sqrMagnitude
                    >= FormationFacingCommitDistance
                        * FormationFacingCommitDistance;
                bool heldHeadingLongEnough = now
                        - _guardFacingCandidateStartedAt
                    >= FormationFacingCommitSeconds;
                if (traveledFarEnough || heldHeadingLongEnough)
                {
                    _guardFacingDesired = traveledFarEnough
                            && committedTravel.sqrMagnitude > 0.0001f
                        ? committedTravel.normalized
                        : _guardFacingCandidate;
                    _formationLeaderMovementOrigin = hero.Coords;
                    _guardFacingCandidate = candidate;
                    _guardFacingCandidateStartedAt = now;
                }
            }
            else
            {
                _guardFacingCandidateStartedAt = -1.0f;
            }

            float maximumRadians =
                FormationFacingMaximumTurnDegreesPerSecond
                * Mathf.Deg2Rad
                * Mathf.Max(0.0f, Time.unscaledDeltaTime);
            _guardForward = Vector3.RotateTowards(
                _guardForward,
                _guardFacingDesired,
                maximumRadians,
                0.0f).normalized;
        }

        private static void EnsureFormationFacingHero(Hero hero)
        {
            if (ReferenceEquals(_formationFacingHero, hero))
            {
                return;
            }
            _formationFacingHero = hero;
            ResetBulwarkFacingState();
            _hasGuardForward = false;
            _guardForward = Vector3.forward;
            _guardFacingDesired = Vector3.forward;
            _guardFacingCandidate = Vector3.forward;
            _guardFacingCandidateStartedAt = -1.0f;
            _guardFacingUpdateFrame = -1;
        }

        private static VelocityScheme GetBulwarkVelocityScheme(
            ICharacter leader,
            float anchorDistanceSqr,
            bool advanceHeld)
        {
            float leaderSpeed = leader == null
                ? 0.0f
                : leader.HorizontalVelocity.magnitude;
            if (advanceHeld)
            {
                if (anchorDistanceSqr
                    > BulwarkAdvanceRunDistance * BulwarkAdvanceRunDistance)
                {
                    return VelocityScheme.Run;
                }
                if (anchorDistanceSqr
                    > BulwarkAdvanceAnchorTolerance
                        * BulwarkAdvanceAnchorTolerance)
                {
                    return VelocityScheme.Trot;
                }
                return VelocityScheme.Walk;
            }
            if (anchorDistanceSqr > 25.0f
                || leaderSpeed >= BulwarkLeaderRunSpeed)
            {
                return VelocityScheme.Run;
            }
            if (anchorDistanceSqr > 1.0f
                || leaderSpeed >= BulwarkLeaderMovingSpeed)
            {
                return VelocityScheme.Trot;
            }
            return VelocityScheme.Walk;
        }

        private static bool BeforeFindTarget(NpcAlly __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcHeroSummon summon = __instance as NpcHeroSummon;
            if (plugin != null
                && plugin.IsEnabled
                && summon != null
                && summon.ParentModel != null
                && !summon.ParentModel.HasBeenDiscarded)
            {
                if (IsRecallTargetSuppressed(summon))
                {
                    return false;
                }
                SummonBehavior behavior =
                    SoulProgressionRuntime.GetSummonBehavior();
                return !RefreshAutonomousTargets(summon, plugin, behavior);
            }
            return true;
        }

        private static void AfterFindTarget(NpcAlly __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcHeroSummon summon = __instance as NpcHeroSummon;
            if (plugin != null
                && plugin.IsEnabled
                && summon != null
                && summon.ParentModel != null
                && !summon.ParentModel.HasBeenDiscarded)
            {
                if (IsRecallTargetSuppressed(summon))
                {
                    return;
                }
                SummonBehavior behavior =
                    SoulProgressionRuntime.GetSummonBehavior();
                EnforceSummonBehavior(summon, behavior);
                EnforceHeldSummonLeash(summon);
                LogSummonControlState(summon, "ai-decision");
            }
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.ShareHeroTarget.Value
                || IsHeld(summon)
                || summon == null
                || summon.ParentModel == null
                || summon.ParentModel.HasBeenDiscarded
                || summon.ParentModel.GetCurrentTarget() != null)
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null || hero.VHeroController == null || hero.VHeroController.Raycaster == null)
            {
                return;
            }

            RaycastCheck detection = NpcDetectionField == null
                ? null
                : NpcDetectionField.GetValue(hero.VHeroController.Raycaster)
                    as RaycastCheck;
            if (detection == null)
            {
                return;
            }
            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            Collider targetCollider = detection.Detected(
                origin,
                direction,
                GetTargetingRange(plugin));
            VLocation targetView = targetCollider == null
                ? null
                : targetCollider.GetComponentInParent<LocationParent>()
                    ?.GetComponentInChildren<VLocation>();
            Location targetLocation = targetView == null ? null : targetView.Target;
            NpcElement target = null;
            if (targetLocation == null
                || !targetLocation.TryGetElement<NpcElement>(out target)
                || target == null
                || target == summon.ParentModel
                || !target.IsAlive
                || target.IsUnconscious
                || !WithFactionUtils.WantToFight(summon.ParentModel, target)
                || GetAutonomousTargetPriority(
                    target,
                    summon.ParentModel,
                    SoulProgressionRuntime.GetSummonBehavior(),
                    hero,
                    IsHostInCombat(hero),
                    retainBulwarkTarget: false,
                    retainGuardTarget: false) == int.MaxValue)
            {
                return;
            }

            if (!TrySetPassiveSharedTarget(summon, target))
            {
                return;
            }
            LogSummonControlState(summon, "crosshair-share");
            plugin.LogDiagnostic(
                "Shared crosshair target " + ((Model)target).ID
                + " with summon " + ((Model)summon.ParentModel).ID + ".");
        }

        private static float GetTargetingRange(SoulAndServicePlugin plugin)
        {
            return Math.Min(
                plugin.ShareTargetMaxDistance.Value,
                NativeSummonCommandRange);
        }

        private static bool HasAttackCommandControl()
        {
            return SoulProgressionRuntime.GetNecromanticPower()
                >= SoulProgressionRuntime.AttackCommandPower;
        }

        private static bool HasSwarmCommandControl()
        {
            return SoulProgressionRuntime.GetNecromanticPower()
                >= SoulProgressionRuntime.SwarmCommandPower;
        }

        private static bool HasIndividualFormationControl()
        {
            return SoulProgressionRuntime.GetNecromanticPower()
                >= SoulProgressionRuntime.IndividualFormationPower;
        }

        private static bool HasGlobalFormationControl()
        {
            return SoulProgressionRuntime.GetNecromanticPower()
                >= SoulProgressionRuntime.GlobalFormationPower;
        }

        private static bool HasRecallCommandControl()
        {
            return SoulProgressionRuntime.GetNecromanticPower()
                >= SoulProgressionRuntime.RecallCommandPower;
        }

        private static bool IsSprintActionHeld(Hero hero)
        {
            PlayerInput input = hero == null || hero.VHeroController == null
                ? null
                : hero.VHeroController.Input;
            return input != null
                && input.GetButtonHeld(KeyBindings.Gameplay.Sprint);
        }

        private static bool IsBulwarkAdvanceActive(Hero hero)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            UpdateBulwarkAdvanceState(plugin, hero);
            return plugin != null
                && plugin.IsEnabled
                && plugin.BulwarkAdvanceEnabled != null
                && plugin.BulwarkAdvanceEnabled.Value
                && SoulProgressionRuntime.GetSummonBehavior()
                    == SummonBehavior.Bulwark
                && hero != null
                && (IsSprintActionHeld(hero)
                    || (ReferenceEquals(_bulwarkAdvanceHero, hero)
                        && Time.unscaledTime < _bulwarkAdvanceReleasedUntil));
        }

        private static bool IsBulwarkAdvanceReleaseGraceActive(Hero hero)
        {
            return !IsSprintActionHeld(hero)
                && IsBulwarkAdvanceActive(hero);
        }

        private static float GetBulwarkAdvanceMovementMultiplier(
            SoulAndServicePlugin plugin)
        {
            return plugin == null
                    || plugin.BulwarkAdvanceSpeedMultiplier == null
                ? 2.0f
                : Mathf.Clamp(
                    plugin.BulwarkAdvanceSpeedMultiplier.Value,
                    1.0f,
                    BulwarkAdvanceMaximumMovementMultiplier);
        }

        private static bool IsTargetCommandModifierHeld(
            SoulAndServicePlugin plugin,
            Hero hero)
        {
            if (plugin == null || hero == null)
            {
                return false;
            }
            TargetCommandModifierMode mode = plugin.TargetCommandModifier == null
                ? TargetCommandModifierMode.Sprint
                : plugin.TargetCommandModifier.Value;
            if (mode == TargetCommandModifierMode.None)
            {
                return true;
            }
            return IsSprintActionHeld(hero);
        }

        private static bool CanCommandSummons(
            Hero hero,
            NpcElement target)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.AttackCommandPrompt.Value
                || !HasAttackCommandControl()
                || !IsTargetCommandModifierHeld(plugin, hero)
                || !IsAttackCommandTarget(
                    hero,
                    target,
                    GetTargetingRange(plugin)))
            {
                return false;
            }

            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                if (IsCommandableSummon(summon, hero, target))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAttackCommandTarget(
            Hero hero,
            NpcElement target,
            float commandRange)
        {
            return hero != null
                && target != null
                && !target.HasBeenDiscarded
                && target.IsAlive
                && !target.IsUnconscious
                && !target.IsHeroSummon
                && (target.Coords - hero.Coords).sqrMagnitude
                    <= Math.Min(
                            commandRange,
                            NativeSummonTargetAcquisitionRange)
                        * Math.Min(
                            commandRange,
                            NativeSummonTargetAcquisitionRange);
        }

        private static bool IsWithinNativeSummonTargetAcquisitionRange(
            Hero hero,
            NpcElement target)
        {
            return hero != null
                && target != null
                && (target.Coords - hero.Coords).sqrMagnitude
                    <= NativeSummonTargetAcquisitionRange
                        * NativeSummonTargetAcquisitionRange;
        }

        private static bool IsWithinNativeSummonTargetRetentionRange(
            Hero hero,
            NpcElement target)
        {
            return hero != null
                && target != null
                && (target.Coords - hero.Coords).sqrMagnitude
                    <= NativeSummonTargetRetentionRange
                        * NativeSummonTargetRetentionRange;
        }

        private static bool TryFindFreshAttackCommandTarget(
            Hero hero,
            RaycastCheck detection,
            Vector3 origin,
            Vector3 direction,
            float commandRange,
            out NpcElement target,
            out GameObject viewObject)
        {
            target = null;
            viewObject = null;
            Collider directCollider = detection == null
                ? null
                : detection.Detected(origin, direction, commandRange);
            Location directLocation = ResolveHitLocation(directCollider);
            NpcElement directTarget = directLocation == null
                ? null
                : directLocation.TryGetElement<NpcElement>();
            if (CanCommandSummons(hero, directTarget))
            {
                target = directTarget;
                viewObject = ResolveAttackCommandView(
                    directTarget,
                    directCollider);
                RememberAttackCommandTarget(target, viewObject);
                return true;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                AttackCommandAimRadius,
                direction,
                AttackCommandRaycastHits,
                commandRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            NpcElement bestTarget = null;
            Collider bestCollider = null;
            float bestTargetDistance = float.PositiveInfinity;
            float nearestBlockingDistance = float.PositiveInfinity;
            int count = Math.Min(hitCount, AttackCommandRaycastHits.Length);
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = AttackCommandRaycastHits[index];
                Collider collider = hit.collider;
                if (collider == null)
                {
                    continue;
                }
                Location location = ResolveHitLocation(collider);
                NpcElement candidate = location == null
                    ? null
                    : location.TryGetElement<NpcElement>();
                if (location == hero || IsOwnedHeroSummon(candidate))
                {
                    continue;
                }
                if (IsAttackCommandTarget(hero, candidate, commandRange))
                {
                    if (hit.distance < bestTargetDistance)
                    {
                        bestTarget = candidate;
                        bestCollider = collider;
                        bestTargetDistance = hit.distance;
                    }
                    continue;
                }
                nearestBlockingDistance = Math.Min(
                    nearestBlockingDistance,
                    hit.distance);
            }

            if (bestTarget == null
                || bestTargetDistance > nearestBlockingDistance + 0.01f
                || !CanCommandSummons(hero, bestTarget))
            {
                return false;
            }

            target = bestTarget;
            viewObject = ResolveAttackCommandView(bestTarget, bestCollider);
            RememberAttackCommandTarget(target, viewObject);
            return true;
        }

        private static bool TryGetRecentAttackCommandTarget(
            Hero hero,
            out NpcElement target,
            out GameObject viewObject)
        {
            target = _recentAttackCommandTarget;
            viewObject = _recentAttackCommandViewObject;
            if (target == null
                || Time.unscaledTime - _recentAttackCommandFocusedAt
                    > AttackCommandFocusGraceSeconds
                || !CanCommandSummons(hero, target))
            {
                ClearRecentAttackCommandTarget();
                target = null;
                viewObject = null;
                return false;
            }
            if (viewObject == null)
            {
                viewObject = ResolveAttackCommandView(target, null);
                _recentAttackCommandViewObject = viewObject;
            }
            return viewObject != null;
        }

        private static GameObject ResolveAttackCommandView(
            NpcElement target,
            Collider collider)
        {
            return target != null
                && target.Controller != null
                && target.Controller.AlivePrefab != null
                    ? target.Controller.AlivePrefab
                    : collider == null ? null : collider.gameObject;
        }

        private static void RememberAttackCommandTarget(
            NpcElement target,
            GameObject viewObject)
        {
            _recentAttackCommandTarget = target;
            _recentAttackCommandViewObject = viewObject;
            _recentAttackCommandFocusedAt = Time.unscaledTime;
        }

        private static void ClearRecentAttackCommandTarget()
        {
            _recentAttackCommandTarget = null;
            _recentAttackCommandViewObject = null;
            _recentAttackCommandFocusedAt = 0.0f;
        }

        private static int CommandSummons(Hero hero, NpcElement target)
        {
            int commanded = 0;
            bool useSwarm = HasSwarmCommandControl();
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                if (!IsCommandableSummon(summon, hero, target))
                {
                    continue;
                }
                string summonId = ((Model)summon).ID;
                SetExplicitCommandTarget(summon, target);
                PendingRecallPlacements.Remove(summonId);
                ClearIdleMovementState(summonId);
                if (useSwarm)
                {
                    BeginSwarm(summon, target);
                }
                else
                {
                    ClearSwarm(summonId);
                }
                LogSummonControlState(summon, "explicit-command");
                commanded++;
            }

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null && commanded > 0)
            {
                ResetHuntAttackMove();
                PublishCommand(
                    plugin,
                    SummonCommandState.Attack,
                    SummonAttackCommandId,
                    useSwarm ? "Swarm" : "Attack",
                    StandardCommandFeedbackSeconds,
                    false);
                plugin.LogDiagnostic(
                    "Ordered " + commanded + " summon(s) to attack "
                    + ((Model)target).ID
                    + (useSwarm ? " with Swarm." : "."));
            }
            return commanded;
        }

        private static void PublishCommand(
            SoulAndServicePlugin plugin,
            SummonCommandState state,
            string commandId,
            string feedbackText,
            float feedbackSeconds,
            bool showGft)
        {
            _lastCommandState = state;
            _lastCommandPulseSeconds = feedbackSeconds;
            unchecked
            {
                _commandSequence++;
            }
            if (!string.IsNullOrEmpty(commandId))
            {
                TryPlayCommandVoice(plugin, commandId);
            }
            if (showGft && !string.IsNullOrEmpty(feedbackText))
            {
                SoulProgressionRuntime.ShowSummonCommand(feedbackText);
            }
            ShowCommandFeedback(state, feedbackText, feedbackSeconds);
        }

        private static void TryPlayCommandVoice(
            SoulAndServicePlugin plugin,
            string commandId)
        {
            ResolveBattlecryCommandApi();
            if (_battlecryTryPlayCommandMethod == null)
            {
                return;
            }

            try
            {
                object result = _battlecryTryPlayCommandMethod.Invoke(
                    null,
                    new object[] { commandId });
                plugin.LogDiagnostic(
                    "Battlecry Voice Tuner " + commandId + " accepted="
                    + (result is bool && (bool)result)
                    + ".");
            }
            catch (Exception exception)
            {
                _battlecryTryPlayCommandMethod = null;
                _battlecryCommandApiUnavailable = true;
                plugin.LogWarning(
                    "Battlecry Voice Tuner command integration failed: "
                    + exception.GetBaseException().Message);
            }
        }

        private static void AfterCombatSlotStatusUpdate(
            EnemyBaseClass __instance,
            ICharacter target)
        {
            if (__instance == null
                || target is Hero
                || __instance.OwnedCombatSlotIndex == -1)
            {
                return;
            }
            NpcElement npc = __instance.NpcElement;
            if (npc != null && npc.IsHeroSummon)
            {
                __instance.ReleaseCombatSlots();
            }
        }

        private static void ResolveBattlecryCommandApi()
        {
            if (_battlecryTryPlayCommandMethod != null
                || _battlecryCommandApiUnavailable)
            {
                return;
            }

            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(
                    BattlecryVoiceTunerPluginGuid,
                    out info)
                || info == null
                || info.Instance == null)
            {
                return;
            }

            Type api = info.Instance.GetType().Assembly.GetType(
                BattlecryVoiceTunerApiTypeName,
                false);
            FieldInfo version = api == null
                ? null
                : api.GetField(
                    "ApiVersion",
                    BindingFlags.Public | BindingFlags.Static);
            if (version == null
                || !object.Equals(version.GetRawConstantValue(), 2))
            {
                _battlecryCommandApiUnavailable = true;
                return;
            }

            _battlecryTryPlayCommandMethod = AccessTools.Method(
                api,
                "TryPlayCommand",
                new[] { typeof(string) });
            _battlecryCommandApiUnavailable =
                _battlecryTryPlayCommandMethod == null;
        }

        private static bool IsCommandableSummon(
            NpcHeroSummon summon,
            Hero hero,
            NpcElement target)
        {
            return IsOwnedSummon(summon, hero)
                && target != null
                && WithFactionUtils.WantToFight(summon.ParentModel, target);
        }

        private static bool IsOwnedSummon(
            NpcHeroSummon summon,
            Hero hero)
        {
            return summon != null
                && !summon.HasBeenDiscarded
                && summon.ParentModel != null
                && !summon.ParentModel.HasBeenDiscarded
                && summon.ParentModel.IsAlive
                && ReferenceEquals(summon.Ally, hero);
        }

        private static bool IsHeld(NpcHeroSummon summon)
        {
            return summon != null
                && HeldSummons.ContainsKey(((Model)summon).ID);
        }

        private static bool CanIssueFormationCommand(
            Hero hero,
            NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin != null
                && plugin.IsEnabled
                && plugin.FormationCommands != null
                && plugin.FormationCommands.Value
                && HasIndividualFormationControl()
                && IsTargetCommandModifierHeld(plugin, hero)
                && IsOwnedSummon(summon, hero)
                && IsWithinFormationCommandRange(
                    hero,
                    summon,
                    GetTargetingRange(plugin));
        }

        private static bool IsWithinFormationCommandRange(
            Hero hero,
            NpcHeroSummon summon,
            float commandRange)
        {
            if (hero == null || summon == null || summon.ParentModel == null)
            {
                return false;
            }

            Vector3 nearestPoint = summon.ParentModel.Coords;
            GameObject viewObject = summon.ParentModel.Controller == null
                ? null
                : summon.ParentModel.Controller.AlivePrefab;
            if (TryGetFormationCommandBounds(
                    summon,
                    viewObject,
                    out Bounds bounds))
            {
                nearestPoint = bounds.ClosestPoint(hero.Coords);
            }
            return (nearestPoint - hero.Coords).sqrMagnitude
                <= commandRange * commandRange;
        }

        private static bool TryGetFormationCommandBounds(
            NpcHeroSummon summon,
            GameObject viewObject,
            out Bounds bounds)
        {
            bounds = default(Bounds);
            if (viewObject == null)
            {
                return false;
            }

            string summonId = ((Model)summon).ID;
            FormationCommandViewCache cache;
            if (!FormationCommandViewCaches.TryGetValue(summonId, out cache)
                || cache == null
                || cache.ViewObject != viewObject)
            {
                List<Renderer> filteredRenderers = new List<Renderer>();
                foreach (Renderer renderer
                    in viewObject.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsEffectOnlyRenderer(renderer))
                    {
                        continue;
                    }
                    filteredRenderers.Add(renderer);
                }
                cache = new FormationCommandViewCache
                {
                    ViewObject = viewObject,
                    Renderers = filteredRenderers.ToArray()
                };
                FormationCommandViewCaches[summonId] = cache;
            }

            bool found = false;
            foreach (Renderer renderer in cache.Renderers)
            {
                if (renderer == null
                    || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private static Location ResolveHitLocation(Collider collider)
        {
            VLocation view = collider == null
                ? null
                : collider.GetComponentInParent<LocationParent>()
                    ?.GetComponentInChildren<VLocation>();
            return view == null ? null : view.Target;
        }

        private static NpcHeroSummon ResolveOwnedSummon(
            Collider collider,
            Hero hero)
        {
            Location location = ResolveHitLocation(collider);
            NpcElement npc = location == null
                ? null
                : location.TryGetElement<NpcElement>();
            NpcHeroSummon summon = npc == null
                ? null
                : npc.TryGetElement<NpcHeroSummon>();
            return IsOwnedSummon(summon, hero) ? summon : null;
        }

        private static bool TryFindFocusedFormationSummon(
            Hero hero,
            RaycastCheck detection,
            Vector3 origin,
            Vector3 direction,
            float commandRange,
            out NpcHeroSummon focusedSummon,
            out GameObject focusedViewObject,
            out string diagnostic,
            bool requireCommandEligibility = true)
        {
            focusedSummon = null;
            focusedViewObject = null;
            diagnostic = "no owned summon is within the targeting range";
            Collider directCollider = detection == null
                ? null
                : detection.Detected(origin, direction, commandRange);
            NpcHeroSummon directSummon = ResolveOwnedSummon(
                directCollider,
                hero);
            if (directSummon != null)
            {
                if (requireCommandEligibility
                    && !CanIssueFormationCommand(hero, directSummon))
                {
                    diagnostic = "the summon under the crosshair is outside the targeting range";
                }
                else
                {
                    GameObject directView = directSummon.ParentModel.Controller == null
                    ? null
                    : directSummon.ParentModel.Controller.AlivePrefab;
                    if (directView == null)
                    {
                        diagnostic = "the summon under the crosshair has no active view";
                    }
                    else
                    {
                        focusedSummon = directSummon;
                        focusedViewObject = directView;
                        diagnostic = "focused " + ((Model)directSummon).ID
                            + " through its NPC collider";
                        return true;
                    }
                }
            }

            float bestScore = float.PositiveInfinity;
            bool foundOwnedSummon = false;
            bool foundInRangeSummon = false;
            bool foundAimedSummon = false;
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                if (!IsOwnedSummon(summon, hero))
                {
                    continue;
                }
                foundOwnedSummon = true;
                if (summon.ParentModel.Controller == null
                    || summon.ParentModel.Controller.AlivePrefab == null)
                {
                    diagnostic = "a nearby summon has no active view";
                    continue;
                }

                GameObject viewObject = summon.ParentModel.Controller.AlivePrefab;
                bool hasBounds = TryGetFormationCommandBounds(
                    summon,
                    viewObject,
                    out Bounds bounds);
                Vector3 nearestPoint = hasBounds
                    ? bounds.ClosestPoint(hero.Coords)
                    : summon.ParentModel.Coords;
                if ((nearestPoint - hero.Coords).sqrMagnitude
                    > commandRange * commandRange)
                {
                    continue;
                }
                foundInRangeSummon = true;
                Vector3 focusPoint = summon.ParentModel.Coords + Vector3.up;
                float aimRadius = FormationCommandMinimumAimRadius;
                if (hasBounds)
                {
                    focusPoint = bounds.center;
                    aimRadius = Mathf.Clamp(
                        Math.Max(bounds.extents.x, bounds.extents.y * 0.5f),
                        FormationCommandMinimumAimRadius,
                        FormationCommandMaximumAimRadius);
                }

                Vector3 toFocus = focusPoint - origin;
                float distanceAlongRay = Vector3.Dot(toFocus, direction);
                if (distanceAlongRay <= 0.0f)
                {
                    continue;
                }
                float distanceFromRaySquared = Math.Max(
                    0.0f,
                    toFocus.sqrMagnitude
                        - distanceAlongRay * distanceAlongRay);
                if (distanceFromRaySquared > aimRadius * aimRadius)
                {
                    continue;
                }
                foundAimedSummon = true;
                if (!HasFormationCommandLineOfSight(
                        hero,
                        summon,
                        origin,
                        focusPoint,
                        viewObject))
                {
                    continue;
                }

                float score = distanceFromRaySquared
                    / (aimRadius * aimRadius)
                    + distanceAlongRay * 0.001f;
                if (score >= bestScore)
                {
                    continue;
                }
                bestScore = score;
                focusedSummon = summon;
                focusedViewObject = viewObject;
            }
            if (focusedSummon != null)
            {
                diagnostic = "focused " + ((Model)focusedSummon).ID
                    + " through its visible body";
            }
            else if (!foundOwnedSummon)
            {
                diagnostic = "no owned summons are active";
            }
            else if (!foundInRangeSummon)
            {
                diagnostic = "owned summons are outside the targeting range";
            }
            else if (!foundAimedSummon)
            {
                diagnostic = "no nearby summon is under the crosshair";
            }
            else
            {
                diagnostic = "line of sight to the nearby summon is blocked";
            }
            return focusedSummon != null;
        }

        private static bool HasFormationCommandLineOfSight(
            Hero hero,
            NpcHeroSummon summon,
            Vector3 origin,
            Vector3 focusPoint,
            GameObject viewObject)
        {
            Vector3 offset = focusPoint - origin;
            float distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                offset / distance,
                FormationCommandRaycastHits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Collider nearestCollider = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = FormationCommandRaycastHits[index];
                if (hit.collider == null)
                {
                    continue;
                }
                Location hitLocation = ResolveHitLocation(hit.collider);
                if (hitLocation == hero)
                {
                    continue;
                }
                if (hit.distance < nearestDistance)
                {
                    nearestCollider = hit.collider;
                    nearestDistance = hit.distance;
                }
            }
            if (nearestCollider == null)
            {
                return true;
            }
            return ResolveHitLocation(nearestCollider) == summon.ParentModel
                || nearestCollider.transform.IsChildOf(viewObject.transform);
        }

        private static void LogFormationFocusDiagnostic(
            SoulAndServicePlugin plugin,
            string diagnostic)
        {
            if (string.Equals(
                    _lastFormationFocusDiagnostic,
                    diagnostic,
                    StringComparison.Ordinal))
            {
                return;
            }
            _lastFormationFocusDiagnostic = diagnostic;
            plugin.LogDiagnostic("Formation command focus: " + diagnostic + ".");
        }

        private static bool SetSummonHeld(
            NpcHeroSummon summon,
            bool held)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return false;
            }

            string summonId = ((Model)summon).ID;
            ClearIdleMovementState(summonId);
            RemoveHuntAttackMoveParticipant(summonId);
            if (held)
            {
                if (HeldSummons.ContainsKey(summonId))
                {
                    return false;
                }
                HeldSummons[summonId] = new HeldSummonState
                {
                    Anchor = summon.ParentModel.Coords
                };
                RemoveAwarenessTargetsForSummon(summon);
                ClearOwnedTargetOverride(summon);
                ExplicitCommandTargets.Remove(summonId);
                ExplicitCommandPursuitStates.Remove(summonId);
                PendingRecallPlacements.Remove(summonId);
                ICharacter currentTarget =
                    summon.ParentModel.GetCurrentTarget();
                if (currentTarget != null
                    && (currentTarget.Coords - summon.ParentModel.Coords)
                        .sqrMagnitude
                        > HeldSummonCombatLeash * HeldSummonCombatLeash)
                {
                    summon.ParentModel.RemoveCombatTarget(currentTarget);
                }
                LogSummonControlState(summon, "hold");
                return true;
            }

            if (!HeldSummons.Remove(summonId))
            {
                return false;
            }
            PendingRecallPlacements.Remove(summonId);
            RestorePatrolRadius(summon);
            LogSummonControlState(summon, "follow");
            return true;
        }

        private static bool CommandIndividualFormation(
            Hero hero,
            NpcHeroSummon summon,
            SummonCommandState state)
        {
            if (!CanIssueFormationCommand(hero, summon))
            {
                return false;
            }

            bool hold = state == SummonCommandState.Hold;
            if (!SetSummonHeld(summon, hold))
            {
                return false;
            }

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            PublishCommand(
                plugin,
                state,
                hold ? SummonHoldCommandId : SummonFollowCommandId,
                hold ? "Hold" : "Follow",
                StandardCommandFeedbackSeconds,
                true);
            plugin.LogDiagnostic(
                "Ordered summon " + ((Model)summon).ID + " to "
                + (hold ? "hold position." : "follow."));
            return true;
        }

        private static int CommandAllFormation(Hero hero)
        {
            if (!HasGlobalFormationControl())
            {
                return 0;
            }
            List<NpcHeroSummon> summons = World.All<NpcHeroSummon>()
                .Where(summon => IsOwnedSummon(summon, hero))
                .ToList();
            if (summons.Count == 0)
            {
                return 0;
            }

            bool followAll = summons.Any(IsHeld);
            int changed = 0;
            foreach (NpcHeroSummon summon in summons)
            {
                if (SetSummonHeld(summon, !followAll))
                {
                    changed++;
                }
            }

            if (changed > 0)
            {
                ResetHuntAttackMove();
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                PublishCommand(
                    plugin,
                    followAll
                        ? SummonCommandState.Follow
                        : SummonCommandState.Hold,
                    followAll
                        ? SummonFollowCommandId
                        : SummonHoldCommandId,
                    followAll ? "Follow All" : "Hold All",
                    ExtendedCommandFeedbackSeconds,
                    true);
                plugin.LogDiagnostic(
                    "Ordered " + changed + " summon(s) to "
                    + (followAll ? "follow." : "hold position."));
            }
            return changed;
        }

        private static int RecallHost(Hero hero)
        {
            if (!HasRecallCommandControl() || hero == null)
            {
                return 0;
            }

            ResetHuntAttackMove();
            ResetBulwarkAdvanceState();

            NpcHeroSummon[] summons = GetFormationHost(hero);
            Pathfinding.GraphNode heroNode = AstarPath.active == null
                ? null
                : AstarPath.active.GetNearest(
                    hero.Coords,
                    Pathfinding.NNConstraint.Walkable).node;
            for (int index = 0; index < summons.Length; index++)
            {
                NpcHeroSummon summon = summons[index];
                if (summon != null)
                {
                    SummonFormationCoordinator.Suspend(((Model)summon).ID);
                }
            }
            float recallRotation = UnityEngine.Random.Range(-15.0f, 15.0f);
            int recalled = 0;
            for (int index = 0; index < summons.Length; index++)
            {
                NpcHeroSummon summon = summons[index];
                if (summon == null
                    || summon.ParentModel == null
                    || summon.ParentModel.Movement == null)
                {
                    continue;
                }
                string summonId = ((Model)summon).ID;
                ClearIdleMovementState(summonId);
                RecallTargetSuppressionUntil[summonId] =
                    Time.unscaledTime + RecallTargetSuppressionSeconds;
                ExplicitCommandTargets.Remove(summonId);
                ExplicitCommandPursuitStates.Remove(summonId);
                AutonomousTargetOverrides.Remove(summonId);
                RemoveAwarenessTargetsForSummon(summon);
                ClearSwarm(summonId);
                SetSummonHeld(summon, false);
                ClearOwnedTargetOverride(summon);
                ForceRecallCombatExit(summon.ParentModel);
                LogSummonControlState(summon, "recall");

                Vector3 recallPlacement;
                bool hasReservedDestination =
                    SummonFormationCoordinator.TryReserveRecallPlacement(
                        summon,
                        hero,
                        heroNode,
                        recallRotation,
                        out recallPlacement);
                PendingRecallPlacements[summonId] =
                    new PendingRecallPlacement
                    {
                        Position = hasReservedDestination
                            ? recallPlacement
                            : summon.ParentModel.Coords,
                        HeroOrigin = hero.Coords,
                        ExpiresAt = Time.unscaledTime
                            + RecallPlacementLifetimeSeconds,
                        HasReservedDestination = hasReservedDestination,
                        DestinationConsumed = false,
                        LocomotionRestored = false
                    };
                float distanceSqr =
                    (hero.Coords - summon.ParentModel.Coords).sqrMagnitude;
                object[] arguments =
                {
                    distanceSqr,
                    TeleportContext.AllyRanAway,
                    Vector3.zero
                };
                QueueTeleportVfx(summon);
                TeleportToAllyMethod.Invoke(summon, arguments);
                recalled++;
            }

            if (recalled > 0)
            {
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                string feedback = recalled == 1 ? "Recall" : "Recall Host";
                PublishCommand(
                    plugin,
                    SummonCommandState.Follow,
                    SummonRecallCommandId,
                    feedback,
                    ExtendedCommandFeedbackSeconds,
                    true);
                plugin.LogDiagnostic(
                    "Recalled " + recalled + " summon(s) to the hero.");
            }
            return recalled;
        }

        private static void PruneHeldSummons()
        {
            if (HeldSummons.Count == 0)
            {
                _nextHeldSummonPruneTime = 0.0f;
                return;
            }
            if (Time.unscaledTime < _nextHeldSummonPruneTime)
            {
                return;
            }
            _nextHeldSummonPruneTime = Time.unscaledTime
                + HeldSummonPruneIntervalSeconds;

            ActiveHeldSummonIds.Clear();
            Hero hero = Hero.Current;
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                if (IsOwnedSummon(summon, hero))
                {
                    ActiveHeldSummonIds.Add(((Model)summon).ID);
                }
            }
            HeldSummonRemovalBuffer.Clear();
            foreach (string summonId in HeldSummons.Keys)
            {
                if (!ActiveHeldSummonIds.Contains(summonId))
                {
                    HeldSummonRemovalBuffer.Add(summonId);
                }
            }
            foreach (string summonId in HeldSummonRemovalBuffer)
            {
                HeldSummons.Remove(summonId);
            }
        }

        private static void ReleaseAllHeldSummons()
        {
            if (HeldSummons.Count == 0)
            {
                return;
            }

            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>()
                .Where(IsHeld)
                .ToArray())
            {
                SetSummonHeld(summon, false);
            }
            HeldSummons.Clear();
        }

        private static void RestorePatrolRadius(NpcHeroSummon summon)
        {
            if (summon != null)
            {
                string summonId = ((Model)summon).ID;
                SummonFormationCoordinator.Suspend(summonId);
            }
            if (summon == null
                || !StabilizedPatrols.Remove(((Model)summon).ID))
            {
                return;
            }
            Patrol patrol = summon == null
                ? null
                : PatrolField.GetValue(summon) as Patrol;
            if (patrol != null)
            {
                patrol.UpdateRadius(NativePatrolRadius);
                patrol.UpdatePlace(
                    summon.Ally == null
                        ? summon.ParentModel.Coords
                        : summon.Ally.Coords);
            }
        }

        private static void UpdateFormationPatrolPlace(
            NpcHeroSummon summon,
            Patrol patrol,
            Vector3 anchor,
            float updateDistance)
        {
            if (summon == null || patrol == null)
            {
                return;
            }
            if (!SummonFormationCoordinator.ShouldApplyPatrolAnchor(
                    summon,
                    anchor,
                    updateDistance))
            {
                return;
            }
            patrol.UpdatePlace(anchor);
        }

        private sealed class SummonCommandInteractable : IInteractableWithHero
        {
            private readonly NpcElement _target;
            private readonly NpcHeroSummon _summon;
            private readonly GameObject _viewObject;
            private readonly SummonCommandAction _action;
            private readonly IHeroAction[] _actions;
            private readonly string _feedbackText;
            private readonly bool _soulRendHover;
            private readonly bool _huntPointCommand;
            private readonly bool _passiveInfo;
            private readonly string _displayName;
            private readonly Vector3 _huntPoint;

            internal SummonCommandInteractable(
                NpcElement target,
                GameObject viewObject)
            {
                _target = target;
                _viewObject = viewObject;
                Kind = SummonCommandState.Attack;
                _action = new SummonCommandAction(
                    target,
                    null,
                    Kind,
                    HasSwarmCommandControl() ? "Swarm" : "Attack");
                _actions = new IHeroAction[] { _action };
            }

            internal SummonCommandInteractable(
                NpcHeroSummon summon,
                GameObject viewObject,
                string displayName,
                string detail)
            {
                _summon = summon;
                _viewObject = viewObject;
                _passiveInfo = true;
                _displayName = displayName;
                Kind = SummonCommandState.None;
                _action = new SummonCommandAction(
                    null,
                    summon,
                    Kind,
                    detail,
                    true,
                    false,
                    default(Vector3),
                    false);
                _actions = new IHeroAction[] { _action };
            }

            internal SummonCommandInteractable(
                NpcHeroSummon summon,
                GameObject viewObject,
                SummonCommandState kind)
            {
                _summon = summon;
                _viewObject = viewObject;
                Kind = kind;
                _action = new SummonCommandAction(null, summon, Kind);
                _actions = new IHeroAction[] { _action };
            }

            internal SummonCommandInteractable(
                Vector3 huntPoint,
                GameObject viewObject)
            {
                _huntPoint = huntPoint;
                _viewObject = viewObject;
                _huntPointCommand = true;
                Kind = SummonCommandState.Attack;
                _action = new SummonCommandAction(
                    null,
                    null,
                    Kind,
                    "Hunt",
                    false,
                    true,
                    huntPoint);
                _actions = new IHeroAction[] { _action };
            }

            internal SummonCommandInteractable(
                GameObject viewObject,
                SummonCommandState kind,
                string feedbackText,
                bool soulRendHover = false)
            {
                _viewObject = viewObject;
                _feedbackText = feedbackText;
                _soulRendHover = soulRendHover;
                Kind = kind;
                _action = new SummonCommandAction(
                    null,
                    null,
                    Kind,
                    feedbackText,
                    true);
                _actions = new IHeroAction[] { _action };
            }

            internal NpcElement Target => _target;

            internal NpcHeroSummon Summon => _summon;

            internal SummonCommandState Kind { get; private set; }

            internal bool IsFeedback => !string.IsNullOrEmpty(_feedbackText);

            internal bool IsSoulRendHover => _soulRendHover;

            internal bool IsHuntPoint => _huntPointCommand;

            internal bool IsPassiveInfo => _passiveInfo;

            internal Vector3 HuntPoint => _huntPoint;

            internal string FeedbackText => _feedbackText;

            internal GameObject ViewObject => _viewObject;

            internal string PassiveDetail => _passiveInfo
                ? _action.DefaultActionName
                : string.Empty;

            public bool Interactable => _passiveInfo
                ? true
                : IsFeedback
                ? true
                : IsHuntPoint
                    ? true
                    : Kind == SummonCommandState.Attack
                    ? _target != null
                        && !_target.HasBeenDiscarded
                        && _target.IsAlive
                    : _summon != null
                        && !_summon.HasBeenDiscarded
                        && _summon.ParentModel != null
                        && !_summon.ParentModel.HasBeenDiscarded
                        && _summon.ParentModel.IsAlive;

            public string DisplayName => _displayName ?? string.Empty;

            public GameObject InteractionVSGameObject => _viewObject;

            public Vector3 InteractionPosition => IsFeedback
                ? Hero.Current == null ? Vector3.zero : Hero.Current.Coords
                : IsHuntPoint
                    ? _huntPoint
                : _target != null
                ? _target.Coords
                : _summon == null || _summon.ParentModel == null
                    ? Vector3.zero
                    : _summon.ParentModel.Coords;

            public IEnumerable<IHeroAction> AvailableActions(Hero hero)
            {
                return _actions;
            }

            public IHeroAction DefaultAction(Hero hero)
            {
                return _action;
            }

            public void DestroyInteraction()
            {
            }
        }

        private sealed class SummonCommandAction : IHeroAction
        {
            private readonly NpcElement _target;
            private readonly NpcHeroSummon _summon;
            private readonly SummonCommandState _kind;
            private readonly string _actionName;
            private readonly bool _feedbackOnly;
            private readonly bool _huntPointCommand;
            private readonly Vector3 _huntPoint;
            private readonly InfoFrame _actionFrame;

            internal SummonCommandAction(
                NpcElement target,
                NpcHeroSummon summon,
                SummonCommandState kind,
                string actionName = null,
                bool feedbackOnly = false,
                bool huntPointCommand = false,
                Vector3 huntPoint = default(Vector3),
                bool showButton = true)
            {
                _target = target;
                _summon = summon;
                _kind = kind;
                _actionName = actionName;
                _feedbackOnly = feedbackOnly;
                _huntPointCommand = huntPointCommand;
                _huntPoint = huntPoint;
                _actionFrame = new InfoFrame(DefaultActionName, showButton);
            }

            public bool IsValidAction => _feedbackOnly
                || _huntPointCommand
                || (_kind == SummonCommandState.Attack
                    ? _target != null && !_target.HasBeenDiscarded
                    : _summon != null && !_summon.HasBeenDiscarded);

            public bool IsIllegal => false;

            public bool IsFeedbackOnly => _feedbackOnly;

            public InfoFrame ActionFrame => _actionFrame;

            public InfoFrame InfoFrame1 => InfoFrame.Empty;

            public InfoFrame InfoFrame2 => InfoFrame.Empty;

            public string DefaultActionName => string.IsNullOrEmpty(_actionName)
                ? _kind.ToString()
                : _actionName;

            public bool StartInteraction(
                Hero hero,
                IInteractableWithHero interactable)
            {
                if (_feedbackOnly)
                {
                    return false;
                }
                if (_huntPointCommand)
                {
                    return TryBeginBehaviorCommandHold(
                        hero,
                        true,
                        _huntPoint);
                }
                if (_kind == SummonCommandState.Attack)
                {
                    return CanCommandSummons(hero, _target)
                        && CommandSummons(hero, _target) > 0;
                }
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null
                    && plugin.HoldIndividualFormationCommands != null
                    && plugin.HoldIndividualFormationCommands.Value)
                {
                    return false;
                }
                if (!CanIssueFormationCommand(hero, _summon))
                {
                    return false;
                }
                return CommandIndividualFormation(
                    hero,
                    _summon,
                    _kind);
            }

            public void FinishInteraction(
                Hero hero,
                IInteractableWithHero interactable)
            {
                if (_huntPointCommand)
                {
                    CompleteBehaviorCommandHold();
                }
            }

            public void EndInteraction(
                Hero hero,
                IInteractableWithHero interactable)
            {
                if (_huntPointCommand)
                {
                    CompleteBehaviorCommandHold();
                }
            }

            public ActionAvailability GetAvailability(
                Hero hero,
                IInteractableWithHero interactable)
            {
                bool available = _feedbackOnly
                    || (_huntPointCommand
                        && CanShowHuntPointPrompt(
                            SoulAndServicePlugin.Instance,
                            hero))
                    || (_kind == SummonCommandState.Attack
                    ? CanCommandSummons(hero, _target)
                    : CanIssueFormationCommand(hero, _summon));
                return available
                    ? ActionAvailability.Available
                    : ActionAvailability.Disabled;
            }

            public IHeroInteractionUI InteractionUIToShow(
                IInteractableWithHero interactable)
            {
                return new HeroInteractionUI(interactable);
            }
        }

        private static void UpdateCommandOverride(
            SoulAndServicePlugin plugin)
        {
            Hero hero = Hero.Current;
            VCHeroRaycaster raycaster = hero == null
                || hero.VHeroController == null
                    ? null
                    : hero.VHeroController.Raycaster;
            string soulRendHoverText;
            GameObject soulRendHoverView;
            if (raycaster != null
                && SoulSalvageRuntime.TryGetHeavySoulRendHoverForInteraction(
                    out soulRendHoverText,
                    out soulRendHoverView))
            {
                if (_commandInteractable != null
                    && _commandInteractable.IsSoulRendHover
                    && string.Equals(
                        _commandInteractable.FeedbackText,
                        soulRendHoverText,
                        StringComparison.Ordinal)
                    && ReferenceEquals(
                        _commandInteractable.ViewObject,
                        soulRendHoverView))
                {
                    return;
                }
                ClearCommandOverride();
                _commandInteractable = new SummonCommandInteractable(
                    soulRendHoverView,
                    SummonCommandState.None,
                    soulRendHoverText,
                    soulRendHover: true);
                raycaster.SetInteractionOverride(_commandInteractable);
                return;
            }
            if (_commandInteractable != null
                && _commandInteractable.IsSoulRendHover)
            {
                ClearCommandOverride();
            }
            if (_commandInteractable != null
                && _commandInteractable.IsFeedback)
            {
                bool modifierHeld = IsTargetCommandModifierHeld(plugin, hero);
                if (!modifierHeld)
                {
                    _commandFeedbackModifierReleased = true;
                }
                if (raycaster != null
                    && Time.unscaledTime < _commandFeedbackEndsAt)
                {
                    if (!modifierHeld || !_commandFeedbackModifierReleased)
                    {
                        return;
                    }
                }
                ClearCommandOverride();
            }
            if (_behaviorCommandHeld)
            {
                ClearRecentAttackCommandTarget();
                if (_huntPointTapArmed
                    && _commandInteractable != null
                    && _commandInteractable.IsHuntPoint)
                {
                    return;
                }
                ClearCommandOverride();
                return;
            }
            if (!plugin.IsEnabled || raycaster == null)
            {
                ClearRecentAttackCommandTarget();
                ClearCommandOverride();
                return;
            }

            RaycastCheck detection = NpcDetectionField == null
                ? null
                : NpcDetectionField.GetValue(raycaster) as RaycastCheck;
            raycaster.GetViewRay(out Vector3 origin, out Vector3 direction);

            bool targetModifierHeld =
                IsTargetCommandModifierHeld(plugin, hero);
            bool huntPointAvailable = CanShowHuntPointPrompt(plugin, hero);
            if ((!targetModifierHeld
                    || (!HasAttackCommandControl()
                        && !HasIndividualFormationControl()))
                && !huntPointAvailable)
            {
                ClearRecentAttackCommandTarget();
            }

            float commandRange = GetTargetingRange(plugin);
            bool attackCommandsAvailable = plugin.AttackCommandPrompt.Value
                && HasAttackCommandControl()
                && targetModifierHeld;
            NpcElement attackTarget;
            GameObject attackViewObject;
            if (attackCommandsAvailable
                && TryFindFreshAttackCommandTarget(
                    hero,
                    detection,
                    origin,
                    direction,
                    commandRange,
                    out attackTarget,
                    out attackViewObject))
            {
                ShowAttackCommandOverride(
                    raycaster,
                    attackTarget,
                    attackViewObject);
                return;
            }

            if (plugin.FormationCommands != null
                && plugin.FormationCommands.Value
                && targetModifierHeld
                && HasIndividualFormationControl())
            {
                if (TryFindFocusedFormationSummon(
                        hero,
                        detection,
                        origin,
                        direction,
                        commandRange,
                        out NpcHeroSummon summon,
                        out GameObject summonViewObject,
                        out string diagnostic))
                {
                    ClearRecentAttackCommandTarget();
                    LogFormationFocusDiagnostic(plugin, diagnostic);
                    SummonCommandState kind = IsHeld(summon)
                        ? SummonCommandState.Follow
                        : SummonCommandState.Hold;
                    if (_commandInteractable != null
                        && ReferenceEquals(
                            _commandInteractable.Summon,
                            summon)
                        && _commandInteractable.Kind == kind)
                    {
                        return;
                    }
                    ClearCommandOverride();
                    _commandInteractable = new SummonCommandInteractable(
                        summon,
                        summonViewObject,
                        kind);
                    raycaster.SetInteractionOverride(_commandInteractable);
                    return;
                }
                LogFormationFocusDiagnostic(plugin, diagnostic);
            }

            if (attackCommandsAvailable
                && TryGetRecentAttackCommandTarget(
                    hero,
                    out attackTarget,
                    out attackViewObject))
            {
                ShowAttackCommandOverride(
                    raycaster,
                    attackTarget,
                    attackViewObject);
                return;
            }

            Vector3 huntPoint;
            GameObject huntPointViewObject;
            if (huntPointAvailable
                && TryResolveHuntPointPreview(
                    plugin,
                    hero,
                    origin,
                    direction,
                    out huntPoint,
                    out huntPointViewObject))
            {
                ClearRecentAttackCommandTarget();
                ShowHuntPointCommandOverride(
                    raycaster,
                    huntPoint,
                    huntPointViewObject);
                return;
            }

            ClearRecentAttackCommandTarget();
            InvalidateHuntPointPreview();
            if (TryFindFocusedFormationSummon(
                    hero,
                    detection,
                    origin,
                    direction,
                    commandRange,
                    out NpcHeroSummon passiveSummon,
                    out GameObject passiveView,
                    out string ignoredDiagnostic,
                    false)
                && SoulforgedRuntime.TryGetHoverText(
                    passiveSummon,
                    out string passiveTitle,
                    out string passiveDetail))
            {
                if (_commandInteractable != null
                    && _commandInteractable.IsPassiveInfo
                    && ReferenceEquals(
                        _commandInteractable.Summon,
                        passiveSummon)
                    && string.Equals(
                        _commandInteractable.DisplayName,
                        passiveTitle,
                        StringComparison.Ordinal)
                    && string.Equals(
                        _commandInteractable.PassiveDetail,
                        passiveDetail,
                        StringComparison.Ordinal))
                {
                    return;
                }
                ClearCommandOverride();
                _commandInteractable = new SummonCommandInteractable(
                    passiveSummon,
                    passiveView,
                    passiveTitle,
                    passiveDetail);
                raycaster.SetInteractionOverride(_commandInteractable);
                return;
            }
            ClearCommandOverride();
        }

        private static void ShowAttackCommandOverride(
            VCHeroRaycaster raycaster,
            NpcElement target,
            GameObject viewObject)
        {
            if (_commandInteractable != null
                && ReferenceEquals(_commandInteractable.Target, target))
            {
                return;
            }
            ClearCommandOverride();
            _commandInteractable = new SummonCommandInteractable(
                target,
                viewObject);
            raycaster.SetInteractionOverride(_commandInteractable);
        }

        private static void ShowHuntPointCommandOverride(
            VCHeroRaycaster raycaster,
            Vector3 huntPoint,
            GameObject viewObject)
        {
            if (_commandInteractable != null
                && _commandInteractable.IsHuntPoint
                && (_commandInteractable.HuntPoint - huntPoint).sqrMagnitude
                    <= HuntPointPreviewMoveDistance
                        * HuntPointPreviewMoveDistance
                && ReferenceEquals(
                    _commandInteractable.ViewObject,
                    viewObject))
            {
                return;
            }
            ClearCommandOverride();
            _commandInteractable = new SummonCommandInteractable(
                huntPoint,
                viewObject);
            raycaster.SetInteractionOverride(_commandInteractable);
        }

        private static void ClearCommandOverride()
        {
            if (_commandInteractable == null)
            {
                return;
            }
            Hero hero = Hero.Current;
            VCHeroRaycaster raycaster = hero == null
                || hero.VHeroController == null
                    ? null
                    : hero.VHeroController.Raycaster;
            if (raycaster != null)
            {
                raycaster.RemoveInteractionOverride(_commandInteractable);
            }
            _commandInteractable.DestroyInteraction();
            _commandInteractable = null;
            _commandFeedbackEndsAt = 0.0f;
            _commandFeedbackModifierReleased = false;
        }

        private static bool BeforeHeroKeysHandle(
            UIEvent evt,
            ref UIResult __result)
        {
            UIKeyAction keyAction = evt as UIKeyAction;
            if (keyAction == null)
            {
                return true;
            }
            if (string.Equals(
                    keyAction.Name,
                    KeyBindings.Gameplay.Interact,
                    StringComparison.Ordinal))
            {
                return HandleBehaviorCommandInput(evt, ref __result);
            }
            if (!string.Equals(
                    keyAction.Name,
                    KeyBindings.UI.Items.TransferItems,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (!_takeAllItemsHeld
                && !ShouldOwnTakeAllHoldForInterop())
            {
                ResetTakeAllItemsHold();
                return true;
            }

            if (evt is UIKeyDownAction)
            {
                _takeAllItemsHeld = true;
                _takeAllCommandMode = GetFormationHost(Hero.Current).Length > 0
                    ? TakeAllCommandMode.Formation
                    : TakeAllCommandMode.RaiseAll;
                _takeAllCommandCancelled = false;
                _formationCommandArmedForRelease = false;
                _recallCommandAttemptedForHold = false;
                _takeAllItemsPressedAt = Time.unscaledTime;
                __result = UIResult.Accept;
                return false;
            }

            if (!_takeAllItemsHeld)
            {
                return true;
            }

            if (evt is UIKeyHeldAction)
            {
                __result = UIResult.Accept;
                return false;
            }

            if (evt is UIKeyUpAction)
            {
                bool issueFormationCommand = _formationCommandArmedForRelease
                    && !_recallCommandAttemptedForHold
                    && !_takeAllCommandCancelled
                    && _takeAllCommandMode == TakeAllCommandMode.Formation;
                ResetTakeAllItemsHold();
                if (issueFormationCommand)
                {
                    CommandAllFormation(Hero.Current);
                }
                __result = UIResult.Accept;
                return false;
            }

            return true;
        }

        private static bool HandleBehaviorCommandInput(
            UIEvent evt,
            ref UIResult result)
        {
            if (TryHandleIndividualFormationCommandInput(evt, ref result))
            {
                return false;
            }
            if (evt is UIKeyUpAction && _behaviorCommandHeld)
            {
                CompleteBehaviorCommandHold();
                result = UIResult.Accept;
                return false;
            }
            if (evt is UIKeyDownAction)
            {
                if (_commandInteractable != null
                    && _commandInteractable.IsFeedback)
                {
                    ClearCommandOverride();
                }
                bool huntPoint = _commandInteractable != null
                    && _commandInteractable.IsHuntPoint;
                Vector3 huntDestination = huntPoint
                    ? _commandInteractable.HuntPoint
                    : Vector3.zero;
                Hero hero = Hero.Current;
                VCHeroRaycaster raycaster = hero == null
                    || hero.VHeroController == null
                        ? null
                        : hero.VHeroController.Raycaster;
                if (!huntPoint
                    && _commandInteractable == null
                    && raycaster != null
                    && !raycaster.GetAvailableActions().Any()
                    && CanUseDirectedHunt(
                        SoulAndServicePlugin.Instance,
                        hero))
                {
                    raycaster.GetViewRay(
                        out Vector3 origin,
                        out Vector3 direction);
                    huntPoint = TryResolveHuntPointPreview(
                        SoulAndServicePlugin.Instance,
                        hero,
                        origin,
                        direction,
                        out huntDestination,
                        out GameObject ignoredViewObject);
                }
                if (!TryBeginBehaviorCommandHold(
                        hero,
                        huntPoint,
                        huntDestination))
                {
                    return true;
                }
                result = UIResult.Accept;
                return false;
            }

            if (!_behaviorCommandHeld)
            {
                return true;
            }
            result = UIResult.Accept;
            return false;
        }

        private static bool TryHandleIndividualFormationCommandInput(
            UIEvent evt,
            ref UIResult result)
        {
            if (_individualFormationCommandHeld)
            {
                if (evt is UIKeyUpAction)
                {
                    TryCompleteIndividualFormationCommandHold();
                    ResetIndividualFormationCommandHold();
                }
                result = UIResult.Accept;
                return true;
            }
            if (!(evt is UIKeyDownAction)
                || !CanStartIndividualFormationCommandHold())
            {
                return false;
            }

            ResetBehaviorCommandHold();
            _individualFormationCommandHeld = true;
            _individualFormationCommandResolved = false;
            _individualFormationCommandPressedAt = Time.unscaledTime;
            _individualFormationCommandSummon = _commandInteractable.Summon;
            _individualFormationCommandState = _commandInteractable.Kind;
            result = UIResult.Accept;
            return true;
        }

        private static bool TryBeginBehaviorCommandHold(
            Hero hero,
            bool huntPoint,
            Vector3 huntDestination)
        {
            if (!ReferenceEquals(hero, Hero.Current)
                || !CanStartBehaviorCommandHold(huntPoint))
            {
                ResetBehaviorCommandHold();
                return false;
            }

            _behaviorCommandHeld = true;
            _behaviorCommandAttemptedForHold = false;
            _behaviorCommandPressedAt = Time.unscaledTime;
            _huntPointTapArmed = huntPoint;
            _huntPointArmedDestination = huntPoint
                ? huntDestination
                : Vector3.zero;
            return true;
        }

        private static void CompleteBehaviorCommandHold()
        {
            if (!_behaviorCommandHeld)
            {
                return;
            }

            bool issueHuntPoint = _huntPointTapArmed
                && !_behaviorCommandAttemptedForHold
                && Time.unscaledTime - _behaviorCommandPressedAt
                    < BehaviorCommandHoldSeconds;
            Vector3 huntPoint = _huntPointArmedDestination;
            ResetBehaviorCommandHold();
            if (issueHuntPoint)
            {
                BeginHuntAttackMove(
                    SoulAndServicePlugin.Instance,
                    Hero.Current,
                    huntPoint);
            }
        }

        private static bool CanStartIndividualFormationCommandHold()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            return plugin != null
                && plugin.HoldIndividualFormationCommands != null
                && plugin.HoldIndividualFormationCommands.Value
                && _commandInteractable != null
                && !_commandInteractable.IsFeedback
                && !_commandInteractable.IsHuntPoint
                && _commandInteractable.Summon != null
                && (_commandInteractable.Kind == SummonCommandState.Hold
                    || _commandInteractable.Kind == SummonCommandState.Follow)
                && CanIssueFormationCommand(
                    hero,
                    _commandInteractable.Summon);
        }

        private static bool CanMaintainIndividualFormationCommandHold()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            return plugin != null
                && plugin.HoldIndividualFormationCommands != null
                && plugin.HoldIndividualFormationCommands.Value
                && (_individualFormationCommandState
                        == SummonCommandState.Hold
                    || _individualFormationCommandState
                        == SummonCommandState.Follow)
                && CanIssueFormationCommand(
                    hero,
                    _individualFormationCommandSummon);
        }

        private static void UpdateIndividualFormationCommandHold()
        {
            if (!_individualFormationCommandHeld
                || _individualFormationCommandResolved)
            {
                return;
            }
            if (!CanMaintainIndividualFormationCommandHold())
            {
                _individualFormationCommandResolved = true;
                return;
            }
            TryCompleteIndividualFormationCommandHold();
        }

        private static void TryCompleteIndividualFormationCommandHold()
        {
            if (_individualFormationCommandResolved
                || Time.unscaledTime - _individualFormationCommandPressedAt
                    < IndividualFormationCommandHoldSeconds)
            {
                return;
            }

            _individualFormationCommandResolved = true;
            if (CanMaintainIndividualFormationCommandHold())
            {
                CommandIndividualFormation(
                    Hero.Current,
                    _individualFormationCommandSummon,
                    _individualFormationCommandState);
            }
        }

        private static bool CanStartBehaviorCommandHold(bool resolvedHuntPoint)
        {
            Hero hero = Hero.Current;
            PlayerInput input = hero == null || hero.VHeroController == null
                ? null
                : hero.VHeroController.Input;
            return CanMaintainBehaviorCommandHold()
                && input != null
                && IsSprintActionHeld(hero)
                && (resolvedHuntPoint
                    || (_commandInteractable == null
                        && !hero.VHeroController.Raycaster
                            .GetAvailableActions().Any()));
        }

        private static bool CanMaintainBehaviorCommandHold()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || SoulProgressionRuntime.GetNecromanticPower()
                    < SoulProgressionRuntime.BehaviorCommandPower
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || hero.VHeroController == null
                || hero.VHeroController.Raycaster == null
                || Time.timeScale <= 0.0f)
            {
                return false;
            }
            return GetFormationHost(hero).Length > 0;
        }

        private static void UpdateBehaviorCommandHold()
        {
            if (!_behaviorCommandHeld || _behaviorCommandAttemptedForHold)
            {
                return;
            }
            if (!CanMaintainBehaviorCommandHold())
            {
                ResetBehaviorCommandHold();
                return;
            }
            if (Time.unscaledTime - _behaviorCommandPressedAt
                < BehaviorCommandHoldSeconds)
            {
                return;
            }
            _behaviorCommandAttemptedForHold = true;
            _huntPointTapArmed = false;
            CycleSummonBehavior();
        }

        private static void UpdateTakeAllItemsHold()
        {
            if (!_takeAllItemsHeld)
            {
                return;
            }
            if (!CanMaintainTakeAllCommandHold())
            {
                _takeAllCommandCancelled = true;
                _formationCommandArmedForRelease = false;
                return;
            }

            float elapsed = Time.unscaledTime - _takeAllItemsPressedAt;
            if (_takeAllCommandMode == TakeAllCommandMode.Formation
                && elapsed >= FormationCommandHoldSeconds)
            {
                _formationCommandArmedForRelease = true;
            }
            if (!_recallCommandAttemptedForHold
                && elapsed >= RecallCommandHoldSeconds
                && (_takeAllCommandMode == TakeAllCommandMode.RaiseAll
                    || HasRecallCommandControl()))
            {
                _recallCommandAttemptedForHold = true;
                _formationCommandArmedForRelease = false;
                if (_takeAllCommandMode == TakeAllCommandMode.RaiseAll)
                {
                    RaiseAll(Hero.Current);
                }
                else
                {
                    RecallHost(Hero.Current);
                }
            }
        }

        private static void RaiseAll(Hero hero)
        {
            int raised = SoulSalvageRuntime.RaiseAll(hero, RaiseAllRadius);
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null || raised <= 0)
            {
                return;
            }

            PublishCommand(
                plugin,
                SummonCommandState.RaiseAll,
                SummonRaiseAllCommandId,
                "Raise All",
                ExtendedCommandFeedbackSeconds,
                true);
            plugin.LogDiagnostic(
                "Raise All began reanimating " + raised + " corpse(s).");
        }

        private static void CycleSummonBehavior()
        {
            SummonBehavior behavior;
            if (!SoulProgressionRuntime.TryCycleSummonBehavior(out behavior))
            {
                return;
            }

            PendingRecallPlacements.Clear();
            GuardIdleStates.Clear();
            HuntIdleStates.Clear();
            HuntIdleMoverIds.Clear();
            ResetHuntAttackMove();
            ResetBulwarkAdvanceState();
            _bulwarkTargetCandidateExpiresAt = 0.0f;
            BulwarkTargetCandidates.Clear();
            _guardIdleMoverId = null;
            _nextIdleHostAttemptAt = 0.0f;
            ResetBulwarkFacingState();
            _hasGuardForward = false;
            _guardForward = Vector3.forward;
            ResetFormationLeaderMotion();
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                if (IsOwnedSummon(summon, Hero.Current))
                {
                    if (plugin != null && plugin.IsEnabled)
                    {
                        RefreshAutonomousTargets(
                            summon,
                            plugin,
                            behavior);
                    }
                    EnforceSummonBehavior(summon, behavior);
                    LogSummonControlState(summon, "behavior-change");
                }
            }

            string feedback = "Behavior: " + behavior;
            string commandId = behavior == SummonBehavior.Guard
                ? SummonGuardCommandId
                : behavior == SummonBehavior.Bulwark
                    ? SummonBulwarkCommandId
                    : SummonHuntCommandId;
            PublishCommand(
                plugin,
                SummonCommandState.Behavior,
                commandId,
                feedback,
                ExtendedCommandFeedbackSeconds,
                true);
            plugin.LogDiagnostic("Changed summon behavior to " + behavior + ".");
        }

        private static void ShowCommandFeedback(
            SummonCommandState state,
            string feedback,
            float durationSeconds)
        {
            Hero hero = Hero.Current;
            VCHeroRaycaster raycaster = hero == null
                || hero.VHeroController == null
                    ? null
                    : hero.VHeroController.Raycaster;
            if (raycaster == null)
            {
                return;
            }
            ClearCommandOverride();
            _commandInteractable = new SummonCommandInteractable(
                hero.VHeroController.gameObject,
                state,
                feedback);
            _commandFeedbackModifierReleased = false;
            _commandFeedbackEndsAt = Time.unscaledTime
                + Math.Max(0.0f, durationSeconds);
            raycaster.SetInteractionOverride(_commandInteractable);
        }

        private static void AfterHeroKeyBindings(
            ref IEnumerable<KeyBindings> __result)
        {
            __result = AppendCommandBindings(__result);
        }

        private static IEnumerable<KeyBindings> AppendCommandBindings(
            IEnumerable<KeyBindings> bindings)
        {
            bool foundTakeAll = false;
            bool foundInteract = false;
            if (bindings != null)
            {
                foreach (KeyBindings binding in bindings)
                {
                    foundTakeAll = foundTakeAll || object.Equals(
                        binding,
                        KeyBindings.UI.Items.TransferItems);
                    foundInteract = foundInteract || object.Equals(
                        binding,
                        KeyBindings.Gameplay.Interact);
                    yield return binding;
                }
            }
            if (!foundTakeAll)
            {
                yield return KeyBindings.UI.Items.TransferItems;
            }
            if (!foundInteract)
            {
                yield return KeyBindings.Gameplay.Interact;
            }
        }

        private static void ResetTakeAllItemsHold()
        {
            _takeAllItemsHeld = false;
            _takeAllCommandMode = TakeAllCommandMode.None;
            _takeAllCommandCancelled = false;
            _formationCommandArmedForRelease = false;
            _recallCommandAttemptedForHold = false;
            _takeAllItemsPressedAt = 0.0f;
        }

        private static void ResetBehaviorCommandHold()
        {
            _behaviorCommandHeld = false;
            _behaviorCommandAttemptedForHold = false;
            _behaviorCommandPressedAt = 0.0f;
            _huntPointTapArmed = false;
            _huntPointArmedDestination = Vector3.zero;
        }

        private static void ResetIndividualFormationCommandHold()
        {
            _individualFormationCommandHeld = false;
            _individualFormationCommandResolved = false;
            _individualFormationCommandPressedAt = 0.0f;
            _individualFormationCommandSummon = null;
            _individualFormationCommandState = SummonCommandState.None;
        }

        internal static bool ShouldOwnTakeAllHoldForInterop()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || plugin.FormationCommands == null
                || !plugin.FormationCommands.Value
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || Time.timeScale <= 0.0f
                || !IsSprintActionHeld(hero))
            {
                return false;
            }

            if (GetFormationHost(hero).Length > 0)
            {
                return HasGlobalFormationControl();
            }
            return SoulProgressionRuntime.GetNecromanticPower()
                    >= SoulProgressionRuntime.RaiseAllPower
                && SoulSalvageRuntime.HasEligibleRaiseAllCorpse(
                    hero,
                    RaiseAllRadius);
        }

        private static bool CanMaintainTakeAllCommandHold()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || plugin.FormationCommands == null
                || !plugin.FormationCommands.Value
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || Time.timeScale <= 0.0f
                || !IsSprintActionHeld(hero))
            {
                return false;
            }

            if (_takeAllCommandMode == TakeAllCommandMode.Formation)
            {
                return HasGlobalFormationControl()
                    && GetFormationHost(hero).Length > 0;
            }
            return _takeAllCommandMode == TakeAllCommandMode.RaiseAll
                && SoulProgressionRuntime.GetNecromanticPower()
                    >= SoulProgressionRuntime.RaiseAllPower
                && GetFormationHost(hero).Length == 0;
        }

        private static void EnforceHeldSummonLeash(
            NpcHeroSummon summon)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return;
            }

            HeldSummonState heldState;
            if (!HeldSummons.TryGetValue(
                    ((Model)summon).ID,
                    out heldState))
            {
                return;
            }

            ICharacter currentTarget = summon.ParentModel.GetCurrentTarget();
            if (currentTarget == null)
            {
                return;
            }
            OwnedTargetOverrideState explicitTarget;
            if (TryGetOwnedTargetOverride(summon, out explicitTarget)
                && explicitTarget.Explicit
                && ReferenceEquals(explicitTarget.Target, currentTarget))
            {
                return;
            }
            if ((currentTarget.Coords - heldState.Anchor).sqrMagnitude
                > HeldSummonCombatLeash * HeldSummonCombatLeash)
            {
                summon.ParentModel.RemoveCombatTarget(currentTarget);
            }
        }

        internal static int GetFocusedCommandStateForInterop()
        {
            return _commandInteractable == null
                || !_commandInteractable.Interactable
                    ? (int)SummonCommandState.None
                    : (int)_commandInteractable.Kind;
        }

        internal static int GetLastCommandStateForInterop()
        {
            return (int)_lastCommandState;
        }

        internal static int GetCommandSequenceForInterop()
        {
            return _commandSequence;
        }

        internal static float GetLastCommandPulseSecondsForInterop()
        {
            return _lastCommandPulseSeconds;
        }

        private static void BeforeAddLimitedLocation(
            object __instance,
            ICharacterLimitedLocation location)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.RepairInvocationScaling.Value
                || __instance == null
                || !(location is NpcHeroSummon))
            {
                return;
            }

            ICharacterLimitedLocation[] locations = LimitedLocationsField == null
                ? null
                : LimitedLocationsField.GetValue(__instance)
                    as ICharacterLimitedLocation[];
            int emptyCount = EmptyCountField == null
                ? 1
                : (int)EmptyCountField.GetValue(__instance);
            int oldestIndex = OldestIndexField == null
                ? -1
                : (int)OldestIndexField.GetValue(__instance);
            if (emptyCount != 0
                || locations == null
                || oldestIndex < 0
                || oldestIndex >= locations.Length)
            {
                return;
            }

            NpcHeroSummon outgoing = locations[oldestIndex] as NpcHeroSummon;
            if (outgoing != null)
            {
                RepairInvocationScaling(
                    (NpcHeroSummon)location,
                    outgoing,
                    plugin);
            }
        }

        private static void AfterSummonInit(NpcHeroSummon __instance)
        {
            if (__instance == null)
            {
                return;
            }
            string summonId = GetSummonId(__instance.ParentModel);
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (!string.IsNullOrEmpty(summonId)
                && plugin != null
                && plugin.IsEnabled)
            {
                SpawnReadinessBySummon[summonId] = new SpawnReadinessState
                {
                    Summon = __instance,
                    EarliestReleaseAt = Time.unscaledTime
                        + GetSpawnRecoverySeconds()
                };
                AnimationWatchdogsBySummon.Remove(summonId);
            }
            InvalidateFormationHostCache();
            __instance.ParentModel.OnCompletelyInitialized(
                delegate
                {
                    InvalidateFormationHostCache();
                    ApplyPlayerPassThrough(__instance, true);
                });
            SoulSalvageRuntime.OnSummonInitialized(__instance);
            SoulforgedRuntime.OnSummonInitialized(__instance);

        }

        private static void RepairInvocationScaling(
            NpcHeroSummon incoming,
            NpcHeroSummon outgoing,
            SoulAndServicePlugin plugin)
        {
            Hero hero = Hero.Current;
            if (hero == null
                || incoming == null
                || outgoing == null
                || incoming.ParentModel == null
                || outgoing.ParentModel == null)
            {
                return;
            }

            float spirituality = hero.HeroRPGStats.Spirituality.ModifiedValue;
            if (spirituality <= 0.0f)
            {
                return;
            }

            NpcElement source = outgoing.ParentModel;
            NpcElement target = incoming.ParentModel;
            if (source.AliveStats == null
                || source.NpcStats == null
                || target.AliveStats == null
                || target.NpcStats == null)
            {
                return;
            }

            float multiplier = 1.0f + spirituality * 0.05f;
            if (!HasExpectedMultiplier(source.AliveStats.MaxHealth, multiplier))
            {
                plugin.LogDiagnostic(
                    "Skipped Invocation of Might repair for replacement summon "
                    + ((Model)target).ID
                    + " because the outgoing summon did not prove that the native scaling was active.");
                return;
            }

            ScalingTweaks tweaks = new ScalingTweaks();
            tweaks.Melee = AddMissingMultiplier(
                target.NpcStats.MeleeDamage,
                multiplier,
                target);
            tweaks.Ranged = AddMissingMultiplier(
                target.NpcStats.RangedDamage,
                multiplier,
                target);
            tweaks.Magic = AddMissingMultiplier(
                target.NpcStats.MagicDamage,
                multiplier,
                target);
            tweaks.Health = AddMissingMultiplier(
                target.AliveStats.MaxHealth,
                multiplier,
                target);
            if (tweaks.Melee == null
                && tweaks.Ranged == null
                && tweaks.Magic == null
                && tweaks.Health == null)
            {
                plugin.LogDiagnostic(
                    "Replacement summon " + ((Model)target).ID
                    + " already retained Invocation of Might scaling.");
                return;
            }

            InvocationTweaks[((Model)incoming).ID] = tweaks;
            if (tweaks.Health != null)
            {
                target.Health.SetToFull();
            }
            plugin.LogDiagnostic(
                "Repaired Invocation of Might scaling for replacement summon "
                + ((Model)target).ID
                + " after confirming it on the outgoing summon.");
        }

        private static bool HasExpectedMultiplier(Stat stat, float multiplier)
        {
            return stat != null
                && stat.BaseValue > 0.0001f
                && Math.Abs(stat.ModifiedValue / stat.BaseValue - multiplier) <= 0.02f;
        }

        private static StatTweak AddMissingMultiplier(
            Stat stat,
            float targetMultiplier,
            NpcElement owner)
        {
            if (stat == null
                || stat.BaseValue <= 0.0001f
                || targetMultiplier <= 1.0f)
            {
                return null;
            }
            float currentMultiplier = stat.ModifiedValue / stat.BaseValue;
            if (currentMultiplier >= targetMultiplier - 0.01f)
            {
                return null;
            }
            float missingMultiplier = targetMultiplier / Math.Max(currentMultiplier, 0.0001f);
            StatTweak tweak = StatTweak.Multi(
                stat,
                missingMultiplier,
                null,
                owner);
            ((Model)tweak).MarkedNotSaved = true;
            return tweak;
        }

        private static void AfterToggleWalkThroughColliders(NpcHeroSummon __instance)
        {
            ApplyPlayerPassThrough(__instance, true);
        }

        private static void ApplyPlayerPassThrough(
            NpcHeroSummon summon,
            bool forceRefresh = false)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SummonPassThrough.Value
                || summon == null
                || ((Model)summon).HasBeenDiscarded
                || summon.ParentModel == null
                || summon.ParentModel.Controller == null
                || summon.ParentModel.Controller.AlivePrefab == null
                || hero == null
                || hero.VHeroController == null
                || hero.VHeroController.Controller == null)
            {
                return;
            }

            string id = ((Model)summon).ID;
            GameObject alivePrefab = summon.ParentModel.Controller.AlivePrefab;
            Collider heroCollider = hero.VHeroController.Controller;
            CollisionState state;
            if (CollisionPairs.TryGetValue(id, out state)
                && (forceRefresh
                    || !ReferenceEquals(state.AlivePrefab, alivePrefab)
                    || !ReferenceEquals(state.HeroCollider, heroCollider)))
            {
                RestoreCollisionPairs(id);
                state = null;
            }
            if (state != null)
            {
                return;
            }
            state = new CollisionState
            {
                AlivePrefab = alivePrefab,
                HeroCollider = heroCollider
            };
            CollisionPairs[id] = state;
            RefreshPlayerPassThroughColliders(state);
        }

        private static void RefreshPlayerPassThroughColliders(
            CollisionState state)
        {
            CollisionColliderBuffer.Clear();
            state.AlivePrefab.GetComponentsInChildren<Collider>(
                true,
                CollisionColliderBuffer);
            foreach (Collider collider in CollisionColliderBuffer)
            {
                if (collider == null
                    || collider.isTrigger
                    || ReferenceEquals(collider, state.HeroCollider)
                    || !state.SummonColliderIds.Add(collider.GetInstanceID()))
                {
                    continue;
                }
                Physics.IgnoreCollision(state.HeroCollider, collider, true);
                state.Pairs.Add(new CollisionPair
                {
                    HeroCollider = state.HeroCollider,
                    SummonCollider = collider
                });
            }
            CollisionColliderBuffer.Clear();
        }

        private static void RestoreCollisionPairs(string id)
        {
            CollisionState state;
            if (!CollisionPairs.TryGetValue(id, out state))
            {
                return;
            }
            foreach (CollisionPair pair in state.Pairs)
            {
                if (pair.HeroCollider != null && pair.SummonCollider != null)
                {
                    Physics.IgnoreCollision(
                        pair.HeroCollider,
                        pair.SummonCollider,
                        false);
                }
            }
            CollisionPairs.Remove(id);
        }

        private static void RestoreAllCollisionPairs()
        {
            foreach (string id in CollisionPairs.Keys.ToArray())
            {
                RestoreCollisionPairs(id);
            }
        }

        private static bool BeforeGetDestroyOnRest(ref bool __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null
                && plugin.IsEnabled
                && plugin.RestBehavior != null
                && plugin.RestBehavior.Value == RestHostBehavior.Sustain)
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static void AfterGetSummonLimit(ref int __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null && plugin.IsEnabled)
            {
                __result += SoulProgressionRuntime
                    .GetProgressionSummonLimitBonus()
                    + plugin.SummonLimitBonus.Value;
            }
        }

        private static void AfterNpcControllerUpdate(NpcController __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (__instance == null || __instance.Npc == null)
            {
                return;
            }
            if (!__instance.Npc.IsHeroSummon)
            {
                return;
            }
            string summonId = GetSummonId(__instance.Npc);
            NpcHeroSummon summon = __instance.Npc
                .TryGetElement<NpcHeroSummon>();
            float nextRefresh;
            if (string.IsNullOrEmpty(summonId)
                || (NextControllerRefreshBySummon.TryGetValue(
                        summonId,
                        out nextRefresh)
                    && Time.unscaledTime < nextRefresh))
            {
                return;
            }
            NextControllerRefreshBySummon[summonId] = Time.unscaledTime
                + ControllerRefreshSeconds;

            ApplyIdleVolume(__instance, plugin);
            UpdateAnimationWatchdog(
                __instance,
                plugin,
                summon,
                summonId);
            UpdateCatchUpSpeed(__instance, plugin, summon, summonId);
            UpdateBehaviorSpeed(__instance, plugin, summon, summonId);
            UpdateEmpoweredPresentation(__instance, summonId);
        }

        private static void UpdateAnimationWatchdog(
            NpcController controller,
            SoulAndServicePlugin plugin,
            NpcHeroSummon summon,
            string id)
        {
            NpcElement npc = controller == null ? null : controller.Npc;
            Hero hero = Hero.Current;
            if (string.IsNullOrEmpty(id)
                || plugin == null
                || !plugin.IsEnabled
                || !IsOwnedSummon(summon, hero)
                || SpawnReadinessBySummon.ContainsKey(id))
            {
                if (!string.IsNullOrEmpty(id))
                {
                    AnimationWatchdogsBySummon.Remove(id);
                }
                return;
            }

            AnimationWatchdogState state;
            if (!AnimationWatchdogsBySummon.TryGetValue(id, out state))
            {
                state = new AnimationWatchdogState();
                AnimationWatchdogsBySummon[id] = state;
            }
            if (!IsMovingForAnimationWatchdog(controller, npc, state))
            {
                ResetAnimationWatchdog(state);
                return;
            }

            NpcGeneralFSM generalFsm;
            bool playing = HasPlayingGeneralAnimation(npc, out generalFsm);
            if (generalFsm == null)
            {
                return;
            }
            NpcAnimatorState animatorState = generalFsm.CurrentAnimatorState;
            var animationState = animatorState == null
                ? null
                : animatorState.CurrentState;
            bool idleWhileMoving = animatorState != null
                && animatorState.Type == NpcStateType.Idle;
            bool movementStateStalled = playing
                && animatorState != null
                && animatorState.Type == NpcStateType.Movement
                && ReferenceEquals(state.LastAnimationState, animationState)
                && Math.Abs(animationState.TimeD - state.LastAnimationTime)
                    <= 0.001;
            state.LastAnimationState = animationState;
            state.LastAnimationTime = animationState == null
                ? 0.0
                : animationState.TimeD;
            if (playing && !idleWhileMoving && !movementStateStalled)
            {
                state.FailedMovingSamples = 0;
                return;
            }

            state.FailedMovingSamples++;
            if (state.FailedMovingSamples < AnimationWatchdogFailureSamples
                || Time.unscaledTime < state.NextRecoveryAt)
            {
                return;
            }

            string recovery;
            if (idleWhileMoving || movementStateStalled)
            {
                npc.SetAnimatorState(
                    NpcFSMType.GeneralFSM,
                    NpcStateType.Movement,
                    0.0f);
                recovery = idleWhileMoving
                    ? "moving-idle mismatch"
                    : "stalled movement state";
            }
            else
            {
                generalFsm.EnableFSM();
                recovery = "stopped animation state";
            }
            plugin.LogDiagnostic(
                "Recovered summon locomotion for " + id + ": " + recovery + ".");
            state.FailedMovingSamples = 0;
            state.NextRecoveryAt = Time.unscaledTime
                + AnimationWatchdogRecoveryCooldownSeconds;
            state.LastAnimationState = null;
            state.LastAnimationTime = 0.0;
        }

        private static bool IsMovingForAnimationWatchdog(
            NpcController controller,
            NpcElement npc,
            AnimationWatchdogState state)
        {
            if (controller == null
                || npc == null
                || state == null
                || controller.RichAI == null
                || !controller.RichAI.canMove)
            {
                return false;
            }
            float now = Time.unscaledTime;
            Vector3 position = npc.Coords;
            Vector3 displacement = state.HasPositionSample
                ? position - state.LastPosition
                : Vector3.zero;
            displacement.y = 0.0f;
            float sampleSeconds = state.HasPositionSample
                ? now - state.LastPositionSampleAt
                : 0.0f;
            bool movingByDisplacement = sampleSeconds > 0.0001f
                && sampleSeconds <= 1.0f
                && displacement.sqrMagnitude
                    >= AnimationWatchdogMovementSpeed
                        * AnimationWatchdogMovementSpeed
                        * sampleSeconds * sampleSeconds;
            state.HasPositionSample = true;
            state.LastPosition = position;
            state.LastPositionSampleAt = now;

            Vector3 velocity = controller.RichAI.velocity;
            velocity.y = 0.0f;
            return movingByDisplacement
                || velocity.sqrMagnitude
                    >= AnimationWatchdogMovementSpeed
                        * AnimationWatchdogMovementSpeed;
        }

        private static bool HasPlayingGeneralAnimation(
            NpcElement npc,
            out NpcGeneralFSM generalFsm)
        {
            generalFsm = npc == null
                ? null
                : npc.TryGetElement<NpcGeneralFSM>();
            if (generalFsm == null || generalFsm.CurrentAnimatorState == null)
            {
                return false;
            }
            var state = generalFsm.CurrentAnimatorState.CurrentState;
            return state != null && state.IsValid && state.IsPlaying;
        }

        private static void ResetAnimationWatchdog(AnimationWatchdogState state)
        {
            if (state == null)
            {
                return;
            }
            state.FailedMovingSamples = 0;
            state.LastAnimationState = null;
            state.LastAnimationTime = 0.0;
        }

        private static void ApplyIdleVolume(
            NpcController controller,
            SoulAndServicePlugin plugin)
        {
            ARFmodEventEmitter emitter = controller.IdleAudioEmitter;
            if (emitter == null || !emitter.EventInstance.isValid())
            {
                return;
            }
            float volume = plugin != null && plugin.IsEnabled
                ? plugin.IdleSoundVolumePercent.Value / 100.0f
                : 1.0f;
            emitter.EventInstance.setVolume(volume);
        }

        private static void UpdateCatchUpSpeed(
            NpcController controller,
            SoulAndServicePlugin plugin,
            NpcHeroSummon summon,
            string id)
        {
            NpcElement npc = controller.Npc;
            bool shouldBoost = false;
            float multiplier = 1.0f;
            Hero hero = Hero.Current;
            if (plugin != null
                && plugin.IsEnabled
                && hero != null
                && npc.NpcAI != null
                && npc.GetCurrentTarget() == null)
            {
                multiplier = plugin.CatchUpSpeedMultiplier.Value;
                bool bulwarkCatchUp = summon != null
                    && SoulProgressionRuntime.GetSummonBehavior()
                        == SummonBehavior.Bulwark
                    && !HeldSummons.ContainsKey(id)
                    && !PendingRecallPlacements.ContainsKey(id)
                    && !HasActivePriorityTarget(summon);
                if (bulwarkCatchUp)
                {
                    bool advanceHeld = IsBulwarkAdvanceActive(hero);
                    Vector3 anchor = GetBulwarkAnchor(summon);
                    float threshold = SpeedTweaks.ContainsKey(id)
                        ? advanceHeld
                            ? BulwarkAdvanceCatchUpStopDistance
                            : BulwarkCatchUpStopDistance
                        : advanceHeld
                            ? BulwarkAdvanceCatchUpStartDistance
                            : BulwarkCatchUpStartDistance;
                    shouldBoost = (anchor - npc.Coords).sqrMagnitude
                        > threshold * threshold;
                    if (advanceHeld)
                    {
                        float existingMovement = 1.0f;
                        EmpowermentState empowerment;
                        if (EmpowermentStates.TryGetValue(id, out empowerment))
                        {
                            existingMovement *= empowerment.MovementMultiplier;
                        }
                        SwarmState swarm;
                        if (SwarmStates.TryGetValue(id, out swarm))
                        {
                            existingMovement *= swarm.MovementMultiplier;
                        }
                        existingMovement *=
                            GetBulwarkAdvanceMovementMultiplier(plugin);
                        multiplier = Math.Min(
                            Math.Max(
                                multiplier,
                                BulwarkAdvanceCatchUpMinimumMultiplier),
                            Math.Max(
                                1.0f,
                                BulwarkAdvanceMaximumMovementMultiplier
                                    / existingMovement));
                    }
                }
                else if (!npc.NpcAI.InCombat)
                {
                    float threshold = plugin.TrotDistance.Value;
                    shouldBoost = (hero.Coords - npc.Coords).sqrMagnitude
                        > threshold * threshold;
                }
            }

            if (!shouldBoost || multiplier <= 1.0f)
            {
                RemoveSpeedTweak(id);
                return;
            }

            CatchUpSpeedState current;
            if (SpeedTweaks.TryGetValue(id, out current)
                && Math.Abs(current.Multiplier - multiplier) <= 0.0001f)
            {
                return;
            }
            RemoveSpeedTweak(id);
            if (npc.CharacterStats != null
                && npc.CharacterStats.MovementSpeedMultiplier != null)
            {
                StatTweak tweak = StatTweak.Multi(
                    npc.CharacterStats.MovementSpeedMultiplier,
                    multiplier,
                    null,
                    npc);
                ((Model)tweak).MarkedNotSaved = true;
                SpeedTweaks[id] = new CatchUpSpeedState
                {
                    Multiplier = multiplier,
                    MovementTweak = tweak
                };
            }
        }

        private static void UpdateEmpoweredPresentation(
            NpcController controller,
            string id)
        {
            EmpowermentState empowerment;
            if (EmpowermentStates.TryGetValue(id, out empowerment))
            {
                EnsureEmpowermentVisualEnforcer(controller);
                ApplyEmpowermentVisual(controller, empowerment);
            }

            RootMotion rootMotion = controller.RootMotion;
            if (rootMotion == null)
            {
                return;
            }
            float playback = empowerment == null
                ? 1.0f
                : empowerment.MovementMultiplier;
            SwarmState swarm;
            if (SwarmStates.TryGetValue(id, out swarm))
            {
                playback *= swarm.MovementMultiplier;
            }
            CatchUpSpeedState catchUpSpeed;
            if (SpeedTweaks.TryGetValue(id, out catchUpSpeed))
            {
                playback *= catchUpSpeed.Multiplier;
            }
            BehaviorSpeedState behaviorSpeed;
            if (BehaviorSpeedStates.TryGetValue(id, out behaviorSpeed))
            {
                playback *= behaviorSpeed.Multiplier;
            }
            int rootMotionId = rootMotion.GetInstanceID();
            if (Math.Abs(playback - 1.0f) <= 0.0001f)
            {
                LocomotionPlaybackMultipliers.Remove(rootMotionId);
            }
            else
            {
                LocomotionPlaybackMultipliers[rootMotionId] = playback;
            }
        }

        private static void UpdateBehaviorSpeed(
            NpcController controller,
            SoulAndServicePlugin plugin,
            NpcHeroSummon summon,
            string id)
        {
            NpcElement npc = controller.Npc;
            bool pursuing = npc.NpcAI != null
                && (npc.NpcAI.InCombat
                    || npc.GetCurrentTarget() != null
                    || (summon != null && HasActivePriorityTarget(summon)));
            bool behaviorUnlocked = plugin != null
                && plugin.IsEnabled
                && SoulProgressionRuntime.GetNecromanticPower()
                    >= SoulProgressionRuntime.BehaviorCommandPower;
            SummonBehavior behavior = SoulProgressionRuntime.GetSummonBehavior();
            bool huntBoost = behaviorUnlocked
                && behavior == SummonBehavior.Hunt
                && pursuing;
            Hero hero = Hero.Current;
            bool bulwarkAdvanceBoost = behaviorUnlocked
                && behavior == SummonBehavior.Bulwark
                && summon != null
                && !HeldSummons.ContainsKey(id)
                && !PendingRecallPlacements.ContainsKey(id)
                && IsBulwarkAdvanceActive(hero);
            float desiredMultiplier = huntBoost
                ? HuntBehaviorMovementMultiplier
                : bulwarkAdvanceBoost
                    ? GetBulwarkAdvanceMovementMultiplier(plugin)
                    : 1.0f;
            bool shouldBoost = desiredMultiplier > 1.0f;
            if (!shouldBoost)
            {
                RemoveBehaviorSpeedState(id);
                return;
            }

            float otherCommandMovement = 1.0f;
            EmpowermentState empowerment;
            if (EmpowermentStates.TryGetValue(id, out empowerment))
            {
                otherCommandMovement *= empowerment.MovementMultiplier;
            }
            SwarmState swarm;
            if (SwarmStates.TryGetValue(id, out swarm))
            {
                otherCommandMovement *= swarm.MovementMultiplier;
            }
            CatchUpSpeedState catchUpSpeed;
            if (SpeedTweaks.TryGetValue(id, out catchUpSpeed))
            {
                otherCommandMovement *= catchUpSpeed.Multiplier;
            }
            float movementCeiling = bulwarkAdvanceBoost
                ? BulwarkAdvanceMaximumMovementMultiplier
                : MaximumCommandMovementMultiplier;
            float multiplier = Math.Min(
                desiredMultiplier,
                Math.Max(
                    1.0f,
                    movementCeiling / otherCommandMovement));
            BehaviorSpeedState current;
            if (BehaviorSpeedStates.TryGetValue(id, out current)
                && Math.Abs(current.Multiplier - multiplier) <= 0.0001f)
            {
                return;
            }
            RemoveBehaviorSpeedState(id);
            BehaviorSpeedState state = new BehaviorSpeedState
            {
                Multiplier = multiplier
            };
            if (multiplier > 1.0f
                && npc.CharacterStats != null
                && npc.CharacterStats.MovementSpeedMultiplier != null)
            {
                state.MovementTweak = StatTweak.Multi(
                    npc.CharacterStats.MovementSpeedMultiplier,
                    multiplier,
                    null,
                    npc);
                ((Model)state.MovementTweak).MarkedNotSaved = true;
            }
            BehaviorSpeedStates[id] = state;
        }

        private static void BeforeRootMotionUpdateAnimator(
            RootMotion __instance,
            ref Vector2 velocity)
        {
            if (__instance == null)
            {
                return;
            }
            float multiplier;
            if (LocomotionPlaybackMultipliers.TryGetValue(
                    __instance.GetInstanceID(),
                    out multiplier))
            {
                velocity *= multiplier;
            }
        }

        private static void AfterSummonDiscard(
            NpcHeroSummon __instance,
            bool fromDomainDrop)
        {
            if (__instance == null)
            {
                return;
            }
            string id = ((Model)__instance).ID;
            ClearOwnedTargetOverride(__instance);
            InvalidateFormationHostCache();
            StabilizedPatrols.Remove(id);
            RemoveHuntAttackMoveParticipant(id);
            SummonFormationCoordinator.Remove(id);
            ExplicitCommandTargets.Remove(id);
            ExplicitCommandPursuitStates.Remove(id);
            AutonomousTargetOverrides.Remove(id);
            HeldSummons.Remove(id);
            ClearIdleMovementState(id);
            FormationCommandViewCaches.Remove(id);
            NextControllerRefreshBySummon.Remove(id);
            SpawnReadinessBySummon.Remove(id);
            AnimationWatchdogsBySummon.Remove(id);
            LastControlDiagnosticBySummon.Remove(id);
            NextControlDiagnosticBySummon.Remove(id);
            PendingTeleportVfxBySummon.Remove(id);
            PendingRecallPlacements.Remove(id);
            RecallTargetSuppressionUntil.Remove(id);
            RestoreCollisionPairs(id);
            RemoveSpeedTweak(id);
            RemoveBehaviorSpeedState(id);
            ClearSwarm(id);
            ClearEmpowerment(id);
            if (__instance.ParentModel != null
                && __instance.ParentModel.Controller != null
                && __instance.ParentModel.Controller.RootMotion != null)
            {
                LocomotionPlaybackMultipliers.Remove(
                    __instance.ParentModel.Controller.RootMotion.GetInstanceID());
            }
            ScalingTweaks tweaks;
            if (InvocationTweaks.TryGetValue(id, out tweaks))
            {
                DiscardScalingTweaks(tweaks);
                InvocationTweaks.Remove(id);
            }
            SoulSalvageRuntime.OnSummonDiscarded(__instance, fromDomainDrop);
            SoulforgedRuntime.OnSummonDiscarded(__instance, fromDomainDrop);
            RemoveAwarenessTargetsForSummon(__instance);
        }

        private static void AfterApplyDamageModifiers(
            HealthElement __instance,
            Damage damage,
            ref float dmgModifier)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null || !plugin.IsEnabled || __instance == null)
            {
                return;
            }

            NpcElement receiver = __instance.ParentModel as NpcElement;
            if (IsOwnedHeroSummon(receiver))
            {
                dmgModifier *= SoulProgressionRuntime
                    .GetSummonDamageTakenMultiplier();
                dmgModifier /= SoulforgedRuntime.GetMultiplier(
                    GetSummonId(receiver));
                EmpowermentState receiverEmpowerment;
                if (EmpowermentStates.TryGetValue(
                        GetSummonId(receiver),
                        out receiverEmpowerment))
                {
                    dmgModifier /= receiverEmpowerment.CombatMultiplier;
                }
            }

            NpcElement dealer = damage == null
                ? null
                : damage.DamageDealerPure as NpcElement;
            if (dealer != null
                && !dealer.HasBeenDiscarded
                && dealer.IsAlive
                && IsHeroOrOwnedSummon(__instance.ParentModel))
            {
                RecentAttackers[((Model)dealer).ID] = new RecentAttackerRecord
                {
                    Target = dealer,
                    ExpiresAt = Time.unscaledTime + RecentAttackerMemorySeconds
                };
            }
            if (IsOwnedHeroSummon(dealer))
            {
                dmgModifier *= SoulProgressionRuntime
                    .GetSummonDamageMultiplier();
                dmgModifier *= SoulforgedRuntime.GetMultiplier(
                    GetSummonId(dealer));
                EmpowermentState dealerEmpowerment;
                if (EmpowermentStates.TryGetValue(
                        GetSummonId(dealer),
                        out dealerEmpowerment))
                {
                    dmgModifier *= dealerEmpowerment.CombatMultiplier;
                }
                if (TryConsumeSwarmHit(dealer, receiver))
                {
                    dmgModifier *= SwarmFirstHitMultiplier;
                }
            }
        }

        private static bool IsOwnedHeroSummon(NpcElement npc)
        {
            if (npc == null || !npc.IsHeroSummon)
            {
                return false;
            }
            NpcHeroSummon summon = npc.TryGetElement<NpcHeroSummon>();
            return summon != null && ReferenceEquals(summon.Ally, Hero.Current);
        }

        private static string GetSummonId(NpcElement npc)
        {
            NpcHeroSummon summon = npc == null
                ? null
                : npc.TryGetElement<NpcHeroSummon>();
            return summon == null ? null : ((Model)summon).ID;
        }

        private static bool RefreshAutonomousTargets(
            NpcHeroSummon summon,
            SoulAndServicePlugin plugin,
            SummonBehavior behavior)
        {
            NpcElement owner = summon.ParentModel;
            Hero hero = Hero.Current;
            NpcGrid grid = World.Services == null
                ? null
                : World.Services.TryGet<NpcGrid>();
            if (owner == null || hero == null || grid == null)
            {
                return false;
            }

            string summonId = ((Model)summon).ID;
            if (HasExplicitCommandTarget(summon))
            {
                RemoveAwarenessTargetsForSummon(
                    summon,
                    ExplicitCommandTargets[summonId]);
                return true;
            }

            HeldSummonState heldState;
            bool held = HeldSummons.TryGetValue(summonId, out heldState);

            float sightMultiplier;
            float aggroMultiplier;
            ReadSteelAndBoneAwarenessMultipliers(
                out sightMultiplier,
                out aggroMultiplier);
            float transferredSight = 1.0f
                + (SteelAndBoneTransferFraction * (sightMultiplier - 1.0f));
            bool directedHunt = behavior == SummonBehavior.Hunt
                && !held
                && IsHuntAttackMoveActive()
                && IsHuntAttackMoveParticipant(summon);
            float awarenessRange = behavior == SummonBehavior.Bulwark
                ? Math.Max(
                    BulwarkTargetCandidateRange,
                    plugin != null && plugin.BulwarkPlayerLeash != null
                        ? plugin.BulwarkPlayerLeash.Value
                        : DefaultBulwarkPlayerLeash)
                : behavior == SummonBehavior.Hunt
                    ? BaseHuntAwarenessRange
                        * Math.Max(1.0f, transferredSight)
                    : BaseSummonAwarenessRange
                        * Math.Max(1.0f, transferredSight);
            if (held)
            {
                awarenessRange = Math.Min(
                    awarenessRange,
                    HeldSummonCombatLeash);
            }
            bool bulwarkAdvanceHeld = behavior == SummonBehavior.Bulwark
                && IsBulwarkAdvanceActive(hero);
            float retentionRange = behavior == SummonBehavior.Bulwark
                ? plugin != null && plugin.BulwarkPlayerLeash != null
                    ? plugin.BulwarkPlayerLeash.Value
                    : DefaultBulwarkPlayerLeash
                : behavior == SummonBehavior.Hunt
                    ? awarenessRange + 5.0f
                    : GetGuardEngagementRange(retainTarget: false) > 0.0f
                        ? GetGuardEngagementRange(retainTarget: true)
                        : Math.Min(
                            NativeSummonCommandRange,
                            awarenessRange + 5.0f);
            if (held)
            {
                retentionRange = HeldSummonCombatLeash;
            }
            float graceSeconds = BaseLostTargetGraceSeconds
                * (1.0f
                    + (SteelAndBoneTransferFraction
                        * (aggroMultiplier - 1.0f)));
            float now = Time.unscaledTime;
            Vector3 awarenessCenter = held
                ? heldState.Anchor
                : behavior == SummonBehavior.Bulwark
                    ? hero.Coords
                    : behavior == SummonBehavior.Guard
                        && GetGuardEngagementRange(retainTarget: false) > 0.0f
                        ? hero.Coords
                    : owner.Coords;
            bool hostInCombat = IsHostInCombat(hero);
            NpcElement bestTarget = null;
            int bestPriority = int.MaxValue;
            float bestDistanceSqr = float.PositiveInfinity;
            float bestSelectionScore = float.PositiveInfinity;
            NpcElement committedTarget;
            AwarenessTargetRecord committedRecord = null;
            if (AutonomousTargetOverrides.TryGetValue(
                    summonId,
                    out committedTarget)
                && committedTarget != null)
            {
                AwarenessTargets.TryGetValue(
                    summonId,
                    out committedRecord);
                if (committedRecord != null
                    && !ReferenceEquals(
                        committedRecord.Target,
                        committedTarget))
                {
                    committedRecord = null;
                }
            }
            bool committedTargetVisible = false;
            int committedPriority = int.MaxValue;
            float committedDistanceSqr = float.PositiveInfinity;
            Pathfinding.GraphNode directedSourceNode = null;
            if (directedHunt && AstarPath.active != null)
            {
                directedSourceNode = AstarPath.active.GetNearest(
                    owner.Coords,
                    Pathfinding.NNConstraint.Walkable).node;
            }

            AutonomousTargetCandidateBuffer.Clear();
            foreach (NpcElement target in GetAutonomousTargetCandidates(
                grid,
                awarenessCenter,
                awarenessRange,
                behavior))
            {
                if (target == null
                    || target.HasBeenDiscarded
                    || ReferenceEquals(target, owner)
                    || !target.IsAlive
                    || target.IsUnconscious
                    || (!directedHunt
                        && !IsWithinNativeSummonTargetAcquisitionRange(
                            hero,
                            target))
                    || !WithFactionUtils.WantToFight(owner, target))
                {
                    continue;
                }
                int priority = GetAutonomousTargetPriority(
                    target,
                    owner,
                    behavior,
                    hero,
                    hostInCombat,
                    retainBulwarkTarget: behavior == SummonBehavior.Bulwark
                        && ReferenceEquals(target, committedTarget),
                    retainGuardTarget: behavior == SummonBehavior.Guard
                        && ReferenceEquals(target, committedTarget));
                if (priority == int.MaxValue)
                {
                    continue;
                }
                float distanceSqr =
                    (target.Coords - awarenessCenter).sqrMagnitude;
                bool isCommitted = ReferenceEquals(target, committedTarget);
                int assignmentCount = GetAutonomousTargetAssignmentCount(
                    target,
                    summonId);
                float assignmentPenalty = behavior == SummonBehavior.Bulwark
                    ? 0.0f
                    : AutonomousTargetAssignmentPenalty;
                float selectionScore = distanceSqr
                    * (1.0f
                        + assignmentCount
                            * assignmentPenalty);
                AutonomousTargetCandidateBuffer.Add(
                    new AutonomousTargetCandidate
                    {
                        Target = target,
                        TargetId = ((Model)target).ID,
                        Priority = priority,
                        DistanceSqr = distanceSqr,
                        SelectionScore = selectionScore,
                        Committed = isCommitted
                    });
            }
            AutonomousTargetCandidateBuffer.Sort(
                AutonomousTargetCandidateComparer.Instance);

            foreach (AutonomousTargetCandidate candidate
                in AutonomousTargetCandidateBuffer)
            {
                NpcElement target = candidate.Target;
                if (!HasAutonomousTargetLineOfSight(
                        summon,
                        hero,
                        target,
                        behavior,
                        directedHunt))
                {
                    continue;
                }
                bool beatsBest = candidate.Priority < bestPriority
                    || (candidate.Priority == bestPriority
                        && candidate.SelectionScore < bestSelectionScore);
                if (directedHunt
                    && (candidate.Committed || beatsBest)
                    && !IsHuntAttackMoveTargetReachable(
                        directedSourceNode,
                        target))
                {
                    continue;
                }
                if (candidate.Committed)
                {
                    committedTargetVisible = true;
                    committedPriority = candidate.Priority;
                    committedDistanceSqr = candidate.DistanceSqr;
                }
                if (beatsBest)
                {
                    bestTarget = target;
                    bestPriority = candidate.Priority;
                    bestDistanceSqr = candidate.DistanceSqr;
                    bestSelectionScore = candidate.SelectionScore;
                }
            }
            AutonomousTargetCandidateBuffer.Clear();

            if (committedRecord != null
                && committedTargetVisible
                && !ReferenceEquals(bestTarget, committedTarget)
                && (bestTarget == null
                    || bestPriority > committedPriority
                    || (bestPriority == committedPriority
                        && (now - committedRecord.SelectedAt
                                < (behavior == SummonBehavior.Bulwark
                                    ? BulwarkTargetMinimumCommitmentSeconds
                                    : AutonomousTargetMinimumCommitmentSeconds)
                            || bestDistanceSqr
                                >= committedDistanceSqr
                                    * AutonomousTargetSwitchDistanceRatio
                                    * AutonomousTargetSwitchDistanceRatio))))
            {
                bestTarget = committedTarget;
                bestPriority = committedPriority;
                bestDistanceSqr = committedDistanceSqr;
                bestSelectionScore = committedDistanceSqr;
            }

            NpcElement retainedTarget = null;
            AwarenessTargetRecord record;
            if (AwarenessTargets.TryGetValue(summonId, out record))
            {
                bool hardInvalid = record.Target == null
                    || record.Target.HasBeenDiscarded
                    || !record.Target.IsAlive
                    || record.Target.IsUnconscious
                    || !WithFactionUtils.WantToFight(owner, record.Target)
                    || GetAutonomousTargetPriority(
                        record.Target,
                        owner,
                        behavior,
                        hero,
                        hostInCombat,
                        retainBulwarkTarget: behavior == SummonBehavior.Bulwark,
                        retainGuardTarget: behavior == SummonBehavior.Guard)
                            == int.MaxValue;
                bool rangeExpired = !hardInvalid
                    && !IsTargetWithinOwnedRangeGrace(
                        summon,
                        record.Target);
                bool invalid = hardInvalid || rangeExpired;
                float distanceSqr = invalid
                    ? float.PositiveInfinity
                    : behavior == SummonBehavior.Bulwark
                        ? (owner.Coords - hero.Coords).sqrMagnitude
                        : behavior == SummonBehavior.Guard
                            && GetGuardEngagementRange(retainTarget: false) > 0.0f
                            ? (record.Target.Coords - hero.Coords).sqrMagnitude
                        : (record.Target.Coords - awarenessCenter).sqrMagnitude;
                bool beyondRetention = distanceSqr
                    > retentionRange * retentionRange;
                bool selected = ReferenceEquals(record.Target, bestTarget);
                if (selected)
                {
                    record.LastSeenAt = now;
                    retainedTarget = record.Target;
                }
                else if (bestTarget == null
                    && !invalid
                    && !beyondRetention
                    && now - record.LastSeenAt <= graceSeconds)
                {
                    retainedTarget = record.Target;
                }
                else
                {
                    AwarenessTargets.Remove(summonId);
                    if (!invalid)
                    {
                        owner.RemoveCombatTarget(record.Target);
                    }
                    if (rangeExpired && plugin != null)
                    {
                        plugin.LogDiagnostic(
                            "Released autonomous target for summon "
                            + summonId
                            + ": target remained outside the summon leash.");
                    }
                }
            }

            NpcElement selectedTarget = bestTarget ?? retainedTarget;
            if (selectedTarget == null)
            {
                ClearAutonomousTargetOverride(summon);
                return true;
            }

            string selectedId = ((Model)selectedTarget).ID;
            AwarenessTargetRecord selectedRecord;
            if (!AwarenessTargets.TryGetValue(summonId, out selectedRecord)
                || !ReferenceEquals(selectedRecord.Target, selectedTarget))
            {
                if (selectedRecord != null
                    && selectedRecord.Target != null
                    && !selectedRecord.Target.HasBeenDiscarded)
                {
                    owner.RemoveCombatTarget(selectedRecord.Target);
                }
                if (!owner.ForceAddCombatTarget(
                        selectedTarget,
                        recalculateTarget: true))
                {
                    ClearAutonomousTargetOverride(summon);
                    return true;
                }
                AwarenessTargets[summonId] = new AwarenessTargetRecord
                {
                    SummonId = summonId,
                    Summon = owner,
                    Target = selectedTarget,
                    LastSeenAt = now,
                    SelectedAt = now
                };
                plugin.LogDiagnostic(
                    "Summon " + summonId + " prioritized hostile " + selectedId
                    + " at autonomous range "
                    + awarenessRange.ToString("0.#") + " m.");
            }
            if (owner.NpcAI != null && !owner.NpcAI.InCombat)
            {
                owner.NpcAI.EnterCombatWith(
                    selectedTarget,
                    forceChange: true);
            }
            if (directedHunt)
            {
                RemoveHuntAttackMoveParticipant(summonId);
            }
            SetAutonomousTargetOverride(
                summon,
                selectedTarget,
                directedHunt);
            return true;
        }

        private static int GetAutonomousTargetAssignmentCount(
            NpcElement target,
            string excludedSummonId)
        {
            int count = 0;
            foreach (KeyValuePair<string, AwarenessTargetRecord> pair
                in AwarenessTargets)
            {
                AwarenessTargetRecord record = pair.Value;
                if (!string.Equals(
                        pair.Key,
                        excludedSummonId,
                        StringComparison.Ordinal)
                    && record != null
                    && ReferenceEquals(record.Target, target))
                {
                    count++;
                }
            }
            return count;
        }

        private static IEnumerable<NpcElement> GetAutonomousTargetCandidates(
            NpcGrid grid,
            Vector3 awarenessCenter,
            float awarenessRange,
            SummonBehavior behavior)
        {
            if (behavior != SummonBehavior.Bulwark)
            {
                return grid.GetNpcsInSphere(awarenessCenter, awarenessRange);
            }

            float now = Time.unscaledTime;
            if (!ReferenceEquals(_bulwarkTargetCandidateGrid, grid)
                || now >= _bulwarkTargetCandidateExpiresAt
                || (_bulwarkTargetCandidateCenter - awarenessCenter).sqrMagnitude
                    > FormationPatrolAnchorUpdateDistance
                        * FormationPatrolAnchorUpdateDistance)
            {
                _bulwarkTargetCandidateGrid = grid;
                _bulwarkTargetCandidateCenter = awarenessCenter;
                _bulwarkTargetCandidateExpiresAt = now
                    + BulwarkTargetCandidateCacheSeconds;
                BulwarkTargetCandidates.Clear();
                foreach (NpcElement candidate in grid.GetNpcsInSphere(
                    awarenessCenter,
                    awarenessRange))
                {
                    BulwarkTargetCandidates.Add(candidate);
                }
            }
            return BulwarkTargetCandidates;
        }

        private static int GetAutonomousTargetPriority(
            NpcElement target,
            NpcElement owner,
            SummonBehavior behavior,
            Hero hero,
            bool hostInCombat,
            bool retainBulwarkTarget,
            bool retainGuardTarget)
        {
            if (target == null)
            {
                return int.MaxValue;
            }
            RecentAttackerRecord attacker;
            bool recentAttacker = RecentAttackers.TryGetValue(
                    ((Model)target).ID,
                    out attacker)
                && attacker.ExpiresAt >= Time.unscaledTime;
            bool targetingProtected = IsHeroOrOwnedSummon(
                target.GetCurrentTarget());
            if (behavior == SummonBehavior.Hunt)
            {
                return recentAttacker
                    ? 0
                    : targetingProtected ? 1 : 2;
            }
            float heroDistanceSqr = hero == null
                ? float.PositiveInfinity
                : (target.Coords - hero.Coords).sqrMagnitude;
            if (behavior == SummonBehavior.Bulwark)
            {
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                float localEngagementRange = plugin != null
                        && plugin.BulwarkLocalEngagementRange != null
                    ? plugin.BulwarkLocalEngagementRange.Value
                    : DefaultBulwarkLocalEngagementRange;
                float targetRetentionRange = plugin != null
                        && plugin.BulwarkTargetRetentionRange != null
                    ? plugin.BulwarkTargetRetentionRange.Value
                    : DefaultBulwarkTargetRetentionRange;
                float playerLeash = plugin != null
                        && plugin.BulwarkPlayerLeash != null
                    ? plugin.BulwarkPlayerLeash.Value
                    : DefaultBulwarkPlayerLeash;
                bool advanceHeld = IsBulwarkAdvanceActive(hero);
                if (advanceHeld)
                {
                    float advanceOwnerHeroDistanceSqr = owner == null || hero == null
                        ? float.PositiveInfinity
                        : (owner.Coords - hero.Coords).sqrMagnitude;
                    if (advanceOwnerHeroDistanceSqr
                        > playerLeash * playerLeash)
                    {
                        return int.MaxValue;
                    }
                    float breachRange = retainBulwarkTarget
                        ? targetRetentionRange
                        : localEngagementRange;
                    if ((target.Coords - owner.Coords).sqrMagnitude
                        > breachRange * breachRange)
                    {
                        return int.MaxValue;
                    }
                    return recentAttacker
                        ? 0
                        : targetingProtected ? 1 : 2;
                }

                float hostRange = playerLeash;
                float ownerHeroDistanceSqr = owner == null || hero == null
                    ? float.PositiveInfinity
                    : (owner.Coords - hero.Coords).sqrMagnitude;
                if (retainBulwarkTarget)
                {
                    if (ownerHeroDistanceSqr > hostRange * hostRange)
                    {
                        return int.MaxValue;
                    }
                    if (recentAttacker)
                    {
                        return 0;
                    }
                    if (targetingProtected)
                    {
                        return 1;
                    }
                }
                else
                {
                    if (heroDistanceSqr <= hostRange * hostRange)
                    {
                        if (recentAttacker)
                        {
                            return 0;
                        }
                        if (targetingProtected)
                        {
                            return 1;
                        }
                    }
                    if (ownerHeroDistanceSqr > hostRange * hostRange)
                    {
                        return int.MaxValue;
                    }
                }
                float localRange = retainBulwarkTarget
                    ? targetRetentionRange
                    : localEngagementRange;
                return (target.Coords - owner.Coords).sqrMagnitude
                        <= localRange * localRange
                    ? 2
                    : int.MaxValue;
            }
            if (recentAttacker)
            {
                return 0;
            }
            if (targetingProtected)
            {
                return 1;
            }
            if (target.NpcAI != null
                && target.NpcAI.InCombat
                && hostInCombat
                && heroDistanceSqr
                    <= GuardMeleeThreatRange * GuardMeleeThreatRange)
            {
                return 2;
            }
            float guardEngagementRange = GetGuardEngagementRange(
                retainGuardTarget);
            if (guardEngagementRange > 0.0f
                && heroDistanceSqr
                    <= guardEngagementRange * guardEngagementRange)
            {
                return 3;
            }
            return int.MaxValue;
        }

        private static float GetGuardEngagementRange(bool retainTarget)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            float engagementRange = plugin != null
                    && plugin.GuardEngagementRange != null
                ? plugin.GuardEngagementRange.Value
                : DefaultGuardEngagementRange;
            engagementRange = Math.Max(0.0f, engagementRange);
            return engagementRange <= 0.0f || !retainTarget
                ? engagementRange
                : engagementRange + GuardEngagementRetentionPadding;
        }

        private static bool IsHostInCombat(Hero hero)
        {
            int frame = Time.frameCount;
            if (_hostCombatCacheFrame == frame
                && ReferenceEquals(_hostCombatCacheHero, hero))
            {
                return _hostCombatCacheValue;
            }
            _hostCombatCacheFrame = frame;
            _hostCombatCacheHero = hero;
            _hostCombatCacheValue = hero != null
                && hero.HeroCombat != null
                && hero.HeroCombat.IsHeroInFight;
            if (!_hostCombatCacheValue && hero != null)
            {
                NpcHeroSummon[] host = GetFormationHost(hero);
                for (int index = 0; index < host.Length; index++)
                {
                    NpcHeroSummon summon = host[index];
                    if (IsOwnedSummon(summon, hero)
                        && summon.ParentModel != null
                        && summon.ParentModel.NpcAI != null
                        && summon.ParentModel.NpcAI.InCombat)
                    {
                        _hostCombatCacheValue = true;
                        break;
                    }
                }
            }
            return _hostCombatCacheValue;
        }

        private static bool HasAutonomousTargetLineOfSight(
            NpcHeroSummon summon,
            Hero hero,
            NpcElement target,
            SummonBehavior behavior,
            bool directedHunt)
        {
            if (hero == null || target == null)
            {
                return false;
            }
            NpcElement summonObserver = summon == null
                ? null
                : summon.ParentModel;
            if (behavior == SummonBehavior.Hunt && directedHunt)
            {
                return summonObserver != null
                    && HasAutonomousTargetLineOfSightFrom(
                        summonObserver,
                        hero,
                        target);
            }
            return HasAutonomousTargetLineOfSightFrom(null, hero, target)
                || (summonObserver != null
                    && HasAutonomousTargetLineOfSightFrom(
                        summonObserver,
                        hero,
                        target));
        }

        private static bool HasAutonomousTargetLineOfSightFrom(
            NpcElement observer,
            Hero hero,
            NpcElement target)
        {
            Vector3 origin = (observer == null ? hero.Coords : observer.Coords)
                + (Vector3.up * 1.2f);
            Vector3 focusPoint = target.Coords + Vector3.up;
            AutonomousLineOfSightKey cacheKey =
                new AutonomousLineOfSightKey
                {
                    ObserverId = observer == null
                        ? null
                        : ((Model)observer).ID,
                    TargetId = ((Model)target).ID
                };
            AutonomousLineOfSightRecord cached;
            if (AutonomousLineOfSightByTarget.TryGetValue(cacheKey, out cached)
                && cached != null
                && cached.ExpiresAt >= Time.unscaledTime
                && (cached.Origin - origin).sqrMagnitude <= 0.0625f
                && (cached.FocusPoint - focusPoint).sqrMagnitude <= 0.0625f)
            {
                return cached.Visible;
            }
            Vector3 offset = focusPoint - origin;
            float distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                if (cached == null)
                {
                    cached = new AutonomousLineOfSightRecord();
                    AutonomousLineOfSightByTarget[cacheKey] = cached;
                }
                cached.Origin = origin;
                cached.FocusPoint = focusPoint;
                cached.Visible = true;
                cached.ExpiresAt = Time.unscaledTime
                    + AutonomousLineOfSightCacheSeconds;
                return true;
            }
            if (!TryConsumeFrameBudget(
                    ref _lineOfSightBudgetFrame,
                    ref _lineOfSightRaycastsThisFrame,
                    MaximumLineOfSightRaycastsPerFrame))
            {
                return cached != null && cached.Visible;
            }
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                offset / distance,
                AutonomousTargetRaycastHits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Collider nearestCollider = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = AutonomousTargetRaycastHits[index];
                if (hit.collider == null)
                {
                    continue;
                }
                Location hitLocation = ResolveHitLocation(hit.collider);
                NpcElement hitNpc = hitLocation == null
                    ? null
                    : hitLocation.TryGetElement<NpcElement>();
                if (hitLocation == hero || IsOwnedHeroSummon(hitNpc))
                {
                    continue;
                }
                if (hit.distance < nearestDistance)
                {
                    nearestCollider = hit.collider;
                    nearestDistance = hit.distance;
                }
            }
            GameObject targetView = target.Controller == null
                ? null
                : target.Controller.AlivePrefab;
            bool visible = nearestCollider == null
                || ResolveHitLocation(nearestCollider) == target
                || (targetView != null
                    && nearestCollider.transform.IsChildOf(targetView.transform));
            if (cached == null)
            {
                cached = new AutonomousLineOfSightRecord();
                AutonomousLineOfSightByTarget[cacheKey] = cached;
            }
            cached.Origin = origin;
            cached.FocusPoint = focusPoint;
            cached.Visible = visible;
            cached.ExpiresAt = Time.unscaledTime
                + AutonomousLineOfSightCacheSeconds;
            return visible;
        }

        private static bool TryConsumeFrameBudget(
            ref int budgetFrame,
            ref int workThisFrame,
            int maximumWork)
        {
            if (budgetFrame != Time.frameCount)
            {
                budgetFrame = Time.frameCount;
                workThisFrame = 0;
            }
            if (workThisFrame >= maximumWork)
            {
                return false;
            }
            workThisFrame++;
            return true;
        }

        private static void SetAutonomousTargetOverride(
            NpcHeroSummon summon,
            NpcElement target,
            bool ignoreHeroLeash = false)
        {
            if (summon == null
                || summon.ParentModel == null
                || target == null
                || HasExplicitCommandTarget(summon))
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            NpcElement trackedTarget;
            OwnedTargetOverrideState current;
            if (AutonomousTargetOverrides.TryGetValue(
                    summonId,
                    out trackedTarget)
                && ReferenceEquals(trackedTarget, target)
                && TryGetOwnedTargetOverride(summon, out current)
                && !current.Explicit
                && ReferenceEquals(current.Target, target)
                && current.Element != null
                && !current.Element.HasBeenDiscarded)
            {
                ReassertOwnedCombatTarget(summon, target);
                RegisterReciprocalServantThreat(summon, target);
                return;
            }
            AutonomousTargetOverrides[summonId] = target;
            SetOwnedTargetOverride(
                summon,
                target,
                5,
                false,
                ignoreHeroLeash);
            ReassertOwnedCombatTarget(summon, target);
            RegisterReciprocalServantThreat(summon, target);
        }

        private static void ClearAutonomousTargetOverride(NpcHeroSummon summon)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            NpcElement trackedTarget;
            if (!AutonomousTargetOverrides.TryGetValue(
                    summonId,
                    out trackedTarget))
            {
                OwnedTargetOverrideState orphaned;
                if (TryGetOwnedTargetOverride(summon, out orphaned)
                    && !orphaned.Explicit)
                {
                    ClearOwnedTargetOverride(summon);
                }
                return;
            }
            AutonomousTargetOverrides.Remove(summonId);
            OwnedTargetOverrideState current;
            if (TryGetOwnedTargetOverride(summon, out current)
                && !current.Explicit
                && ReferenceEquals(current.Target, trackedTarget))
            {
                ClearOwnedTargetOverride(summon, trackedTarget);
            }
        }

        private static bool IsTrackedAutonomousTarget(
            NpcHeroSummon summon,
            NpcElement target)
        {
            NpcElement tracked;
            return summon != null
                && target != null
                && AutonomousTargetOverrides.TryGetValue(
                    ((Model)summon).ID,
                    out tracked)
                && ReferenceEquals(tracked, target);
        }

        private static void PruneRecentAttackers()
        {
            float now = Time.unscaledTime;
            Hero hero = Hero.Current;
            StateRemovalBuffer.Clear();
            foreach (KeyValuePair<string, RecentAttackerRecord> pair
                in RecentAttackers)
            {
                RecentAttackerRecord record = pair.Value;
                if (record == null
                    || record.Target == null
                    || record.Target.HasBeenDiscarded
                    || !record.Target.IsAlive
                    || record.ExpiresAt < now)
                {
                    StateRemovalBuffer.Add(pair.Key);
                }
            }
            foreach (string key in StateRemovalBuffer)
            {
                RecentAttackers.Remove(key);
            }
            StateRemovalBuffer.Clear();
            foreach (KeyValuePair<string, PendingTeleportVfx> pair
                in PendingTeleportVfxBySummon)
            {
                PendingTeleportVfx pending = pair.Value;
                if (pending == null || now - pending.RequestedAt > 10.0f)
                {
                    StateRemovalBuffer.Add(pair.Key);
                }
            }
            foreach (string key in StateRemovalBuffer)
            {
                PendingTeleportVfxBySummon.Remove(key);
            }
            StateRemovalBuffer.Clear();
            foreach (KeyValuePair<string, PendingRecallPlacement> pair
                in PendingRecallPlacements)
            {
                PendingRecallPlacement placement = pair.Value;
                if (placement == null
                    || (!placement.DestinationConsumed
                        && placement.ExpiresAt < now)
                    || (placement.DestinationConsumed
                        && (hero == null
                            || (hero.Coords - placement.HeroOrigin).sqrMagnitude
                                > RecallPlacementHeroMoveReleaseDistance
                                    * RecallPlacementHeroMoveReleaseDistance)))
                {
                    StateRemovalBuffer.Add(pair.Key);
                }
            }
            foreach (string key in StateRemovalBuffer)
            {
                PendingRecallPlacements.Remove(key);
            }
            StateRemovalBuffer.Clear();
            foreach (KeyValuePair<string, float> pair
                in RecallTargetSuppressionUntil)
            {
                if (pair.Value < now)
                {
                    StateRemovalBuffer.Add(pair.Key);
                }
            }
            foreach (string key in StateRemovalBuffer)
            {
                RecallTargetSuppressionUntil.Remove(key);
            }
            StateRemovalBuffer.Clear();
            AutonomousLineOfSightRemovalBuffer.Clear();
            foreach (KeyValuePair<AutonomousLineOfSightKey,
                AutonomousLineOfSightRecord> pair
                in AutonomousLineOfSightByTarget)
            {
                AutonomousLineOfSightRecord cached = pair.Value;
                if (cached == null
                    || cached.ExpiresAt
                        < now - AutonomousLineOfSightRetentionSeconds)
                {
                    AutonomousLineOfSightRemovalBuffer.Add(pair.Key);
                }
            }
            foreach (AutonomousLineOfSightKey key
                in AutonomousLineOfSightRemovalBuffer)
            {
                AutonomousLineOfSightByTarget.Remove(key);
            }
            AutonomousLineOfSightRemovalBuffer.Clear();
        }

        private static bool IsRecallTargetSuppressed(NpcHeroSummon summon)
        {
            float suppressedUntil;
            return summon != null
                && RecallTargetSuppressionUntil.TryGetValue(
                    ((Model)summon).ID,
                    out suppressedUntil)
                && suppressedUntil >= Time.unscaledTime;
        }

        private static void EnforceSummonBehavior(
            NpcHeroSummon summon,
            SummonBehavior behavior)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return;
            }
            ICharacter currentTarget = summon.ParentModel.GetCurrentTarget();
            if (currentTarget == null || HasExplicitCommandTarget(summon))
            {
                return;
            }
            NpcElement npcTarget = currentTarget as NpcElement;
            if (IsTrackedAutonomousTarget(summon, npcTarget)
                || (GetAutonomousTargetPriority(
                        npcTarget,
                        summon.ParentModel,
                        behavior,
                        Hero.Current,
                        IsHostInCombat(Hero.Current),
                        retainBulwarkTarget: false,
                        retainGuardTarget: false)
                        != int.MaxValue
                    && HasAutonomousTargetLineOfSight(
                        summon,
                        Hero.Current,
                        npcTarget,
                        behavior,
                        directedHunt: false)))
            {
                return;
            }
            OwnedTargetOverrideState targetOverride;
            if (TryGetOwnedTargetOverride(summon, out targetOverride)
                && !targetOverride.Explicit
                && ReferenceEquals(targetOverride.Target, npcTarget))
            {
                ClearOwnedTargetOverride(summon, npcTarget);
            }
            summon.ParentModel.RemoveCombatTarget(currentTarget);
        }

        private static bool IsHeroOrOwnedSummon(object target)
        {
            Hero hero = Hero.Current;
            if (hero == null || target == null)
            {
                return false;
            }
            if (ReferenceEquals(target, hero))
            {
                return true;
            }
            NpcElement npc = target as NpcElement;
            return IsOwnedHeroSummon(npc);
        }

        private static void ReadSteelAndBoneAwarenessMultipliers(
            out float sightMultiplier,
            out float aggroMultiplier)
        {
            sightMultiplier = _cachedSteelAndBoneSightMultiplier;
            aggroMultiplier = _cachedSteelAndBoneAggroMultiplier;
            if (Time.unscaledTime < _nextSteelAndBoneAwarenessRefreshAt)
            {
                return;
            }
            _nextSteelAndBoneAwarenessRefreshAt = Time.unscaledTime
                + SteelAndBoneAwarenessCacheSeconds;
            ResolveSteelAndBoneAwarenessApi();
            if (_steelAndBoneSightMultiplierMethod == null
                || _steelAndBoneAggroMultiplierMethod == null)
            {
                return;
            }
            try
            {
                sightMultiplier = Mathf.Clamp(
                    Convert.ToSingle(
                        _steelAndBoneSightMultiplierMethod.Invoke(null, null)),
                    1.0f,
                    2.0f);
                aggroMultiplier = Mathf.Clamp(
                    Convert.ToSingle(
                        _steelAndBoneAggroMultiplierMethod.Invoke(null, null)),
                    1.0f,
                    2.0f);
                _cachedSteelAndBoneSightMultiplier = sightMultiplier;
                _cachedSteelAndBoneAggroMultiplier = aggroMultiplier;
            }
            catch
            {
                _steelAndBoneSightMultiplierMethod = null;
                _steelAndBoneAggroMultiplierMethod = null;
                _steelAndBoneAwarenessUnavailable = true;
                sightMultiplier = 1.0f;
                aggroMultiplier = 1.0f;
                _cachedSteelAndBoneSightMultiplier = 1.0f;
                _cachedSteelAndBoneAggroMultiplier = 1.0f;
            }
        }

        private static void ResolveSteelAndBoneAwarenessApi()
        {
            if (_steelAndBoneSightMultiplierMethod != null
                || _steelAndBoneAwarenessUnavailable)
            {
                return;
            }
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(
                    SteelAndBonePluginGuid,
                    out info)
                || info == null
                || info.Instance == null)
            {
                return;
            }
            Type api = info.Instance.GetType().Assembly.GetType(
                SteelAndBoneAwarenessApiTypeName,
                false);
            FieldInfo version = api == null
                ? null
                : api.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
            if (version == null
                || !object.Equals(version.GetRawConstantValue(), 1))
            {
                _steelAndBoneAwarenessUnavailable = true;
                return;
            }
            _steelAndBoneSightMultiplierMethod = AccessTools.Method(
                api,
                "GetEnemySightRangeMultiplier",
                new Type[0]);
            _steelAndBoneAggroMultiplierMethod = AccessTools.Method(
                api,
                "GetEnemyAggroPersistenceMultiplier",
                new Type[0]);
            _steelAndBoneAwarenessUnavailable =
                _steelAndBoneSightMultiplierMethod == null
                || _steelAndBoneAggroMultiplierMethod == null;
        }

        private static void RemoveAwarenessTargetsForSummon(
            NpcHeroSummon summon,
            NpcElement preservedTarget = null,
            bool preserveOwnedOverride = false)
        {
            if (summon == null)
            {
                return;
            }

            string summonId = ((Model)summon).ID;
            AwarenessTargetRecord record;
            if (AwarenessTargets.TryGetValue(summonId, out record))
            {
                if (record.Summon != null
                    && !record.Summon.HasBeenDiscarded
                    && record.Target != null
                    && !record.Target.HasBeenDiscarded
                    && !ReferenceEquals(record.Target, preservedTarget))
                {
                    record.Summon.RemoveCombatTarget(record.Target);
                }
                AwarenessTargets.Remove(summonId);
            }
            if (preserveOwnedOverride)
            {
                AutonomousTargetOverrides.Remove(summonId);
            }
            else
            {
                ClearAutonomousTargetOverride(summon);
            }
        }

        private static void RemoveAllAwarenessTargets()
        {
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                ClearAutonomousTargetOverride(summon);
            }
            foreach (AwarenessTargetRecord record in AwarenessTargets.Values)
            {
                if (record.Summon != null
                    && !record.Summon.HasBeenDiscarded
                    && record.Target != null
                    && !record.Target.HasBeenDiscarded)
                {
                    record.Summon.RemoveCombatTarget(record.Target);
                }
            }
            AwarenessTargets.Clear();
            AutonomousTargetOverrides.Clear();
        }

        private static void ClearAllOwnedTargetOverrides()
        {
            foreach (OwnedTargetOverrideState state
                in OwnedTargetOverrides.Values.ToArray())
            {
                if (state != null
                    && state.Element != null
                    && !state.Element.HasBeenDiscarded)
                {
                    state.Element.Discard();
                }
            }
            OwnedTargetOverrides.Clear();
        }

        private static void RemoveSpeedTweak(string id)
        {
            CatchUpSpeedState state;
            if (SpeedTweaks.TryGetValue(id, out state))
            {
                DiscardTweak(state.MovementTweak);
                SpeedTweaks.Remove(id);
            }
        }

        private static void ReleaseAllSpawnReadinessLocks()
        {
            float now = Time.unscaledTime;
            foreach (SpawnReadinessState state
                in SpawnReadinessBySummon.Values.ToArray())
            {
                if (state != null
                    && state.Summon != null
                    && now >= state.EarliestReleaseAt)
                {
                    MovementPreventedField.SetValue(state.Summon, false);
                }
            }
            SpawnReadinessBySummon.Clear();
        }

        private static void RemoveBehaviorSpeedState(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            BehaviorSpeedState state;
            if (BehaviorSpeedStates.TryGetValue(id, out state))
            {
                DiscardTweak(state.MovementTweak);
                BehaviorSpeedStates.Remove(id);
            }
        }

        private static void DiscardScalingTweaks(ScalingTweaks tweaks)
        {
            if (tweaks == null)
            {
                return;
            }
            DiscardTweak(tweaks.Melee);
            DiscardTweak(tweaks.Ranged);
            DiscardTweak(tweaks.Magic);
            DiscardTweak(tweaks.Health);
        }

        private static void DiscardTweak(StatTweak tweak)
        {
            if (tweak != null && !((Model)tweak).HasBeenDiscarded)
            {
                ((Model)tweak).Discard();
            }
        }

        private static bool BeforeProjectileHitResult(
            DamageDealingProjectile __instance,
            HitResult hitResult)
        {
            return !ShouldPassThrough(
                __instance,
                hitResult.Collider,
                __instance is MagicProjectile);
        }

        private static bool BeforeProjectileCollider(
            DamageDealingProjectile __instance,
            Collider collider)
        {
            return !ShouldPassThrough(
                __instance,
                collider,
                __instance is MagicProjectile);
        }

        private static bool BeforeMagicGauntletHit(
            CharacterMagicGauntlet __instance,
            ref RaycastHit other)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || __instance == null
                || __instance.Owner == null
                || __instance.Owner.Character != Hero.Current)
            {
                return true;
            }
            return !ShouldPassThroughOwnedSummon(
                other.collider,
                plugin.PlayerAttackPassThrough.Value,
                true);
        }

        private static bool ShouldPassThrough(
            DamageDealingProjectile projectile,
            Collider collider,
            bool isMagic)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || projectile == null
                || projectile.Owner != Hero.Current
                || collider == null)
            {
                return false;
            }

            PlayerAttackPassThroughMode mode =
                plugin.PlayerAttackPassThrough.Value;
            return ShouldPassThroughOwnedSummon(collider, mode, isMagic);
        }

        private static bool ShouldPassThroughOwnedSummon(
            Collider collider,
            PlayerAttackPassThroughMode mode,
            bool isMagic)
        {
            if (mode == PlayerAttackPassThroughMode.Vanilla
                || (mode == PlayerAttackPassThroughMode.MagicOnly && !isMagic))
            {
                return false;
            }
            if (collider == null)
            {
                return false;
            }
            NpcElement npc = VGUtils.TryGetModel<NpcElement>(collider.gameObject);
            if (npc == null || !npc.IsHeroSummon)
            {
                return false;
            }
            if (mode != PlayerAttackPassThroughMode.CombatOnly)
            {
                return true;
            }

            Hero hero = Hero.Current;
            return (hero != null
                    && hero.HeroCombat != null
                    && hero.HeroCombat.IsHeroInFight)
                || (npc.NpcAI != null && npc.NpcAI.InCombat);
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            MethodInfo method = AccessTools.Method(type, name);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            Type[] arguments)
        {
            MethodInfo method = AccessTools.Method(type, name, arguments);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }

        private static Type RequireNestedType(Type type, string name)
        {
            Type nested = type.GetNestedType(name, BindingFlags.NonPublic);
            if (nested == null)
            {
                throw new MissingMemberException(type.FullName, name);
            }
            return nested;
        }
    }
}
