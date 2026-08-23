using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Interactions;
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
        private const float BaseSummonAwarenessRange = 30.0f;
        private const float BaseLostTargetGraceSeconds = 3.0f;
        private const float FormationCommandHoldSeconds = 0.45f;
        private const float RecallCommandHoldSeconds = 2.0f;
        private const float BehaviorCommandHoldSeconds = 0.45f;
        private const float RecentAttackerMemorySeconds = 6.0f;
        private const float GuardMeleeThreatRange = 8.0f;
        private const float GuardFormationInnerRadius = 4.5f;
        private const float GuardFormationRingSpacing = 1.5f;
        private const float GuardAnchorTolerance = 1.0f;
        private const float GuardLeaderMovingSpeed = 0.15f;
        private const float GuardAnchorRebaseDistance = 1.5f;
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
        private const float BulwarkDefenseRange = 6.0f;
        private const float BulwarkCombatLeash = 8.0f;
        private const float BulwarkAnchorTolerance = 0.5f;
        private const float BulwarkFormationInnerRadius = 3.5f;
        private const float BulwarkFormationRingSpacing = 1.0f;
        private const int BulwarkFormationSlotsPerRing = 4;
        private const float HuntFormationInnerRadius = 5.5f;
        private const float HuntFormationRingSpacing = 2.0f;
        private const int HuntFormationSlotsPerRing = 6;
        private const float HuntAnchorTolerance = 0.5f;
        private const float HuntAnchorRebaseDistance = 2.0f;
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
        private const float RecallTargetSuppressionSeconds = 3.0f;
        private const float RecallPlacementLifetimeSeconds = 10.0f;
        private const float RecallPlacementInnerRadius = 3.5f;
        private const float RecallPlacementRingSpacing = 2.25f;
        private const float RecallPlacementMinimumSpacing = 2.25f;
        private const float RecallPlacementMaximumSnapDistance = 2.0f;
        private const float RecallPlacementHeroMoveReleaseDistance = 2.0f;
        private const float RecallAnchorTolerance = 1.0f;
        private const float RecallPlacementArcStartDegrees = 45.0f;
        private const float RecallPlacementArcDegrees = 270.0f;
        private const float RecallSingleSideMinimumDegrees = 75.0f;
        private const float RecallSingleSideMaximumDegrees = 135.0f;
        private const int RecallPlacementsPerRing = 6;
        private const int RecallPlacementAttempts = 12;
        private const float AutonomousLineOfSightCacheSeconds = 0.25f;
        private const float BulwarkTargetCandidateCacheSeconds = 0.10f;
        private const float FormationPatrolAnchorUpdateDistance = 0.10f;
        private const float FormationLeaderTravelDeadZone = 1.0f;
        private const float FormationLeaderMovementStartSeconds = 0.25f;
        private const float FormationLeaderMovementStopSpeed = 0.15f;
        private const float FormationLeaderSettleSeconds = 0.20f;
        private const float SteelAndBoneAwarenessCacheSeconds = 1.0f;
        private const float AutonomousTargetMinimumCommitmentSeconds = 1.75f;
        private const float AutonomousTargetSwitchDistanceRatio = 0.80f;
        private const float NoviceAiDecisionInterval = 0.75f;
        private const float AiDecisionIntervalRefreshSeconds = 0.50f;
        private const float ControllerRefreshSeconds = 0.10f;
        private const float ControlDiagnosticMinimumIntervalSeconds = 0.25f;
        private const float TransientStatePruneIntervalSeconds = 0.25f;
        private const float StandardCommandFeedbackSeconds = 0.675f;
        private const float ExtendedCommandFeedbackSeconds = 1.35f;
        private const float FormationCommandMinimumAimRadius = 0.35f;
        private const float FormationCommandMaximumAimRadius = 1.25f;
        private const float HeldSummonCombatLeash = 8.0f;
        private const float SteelAndBoneTransferFraction = 0.80f;
        private const float UpkeepIntervalSeconds = 1.0f;
        private const float SwarmDurationSeconds = 5.0f;
        private const float SwarmMovementMultiplier = 1.25f;
        private const float SwarmFirstHitMultiplier = 1.25f;
        private const float MaximumCommandMovementMultiplier = 1.50f;
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
        private const string SummonGuardCommandId = "summon_guard";
        private const string SummonBulwarkCommandId = "summon_bulwark";
        private const string SummonHuntCommandId = "summon_hunt";

        private sealed class CollisionPair
        {
            internal Collider SummonCollider;
            internal Collider HeroCollider;
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
            internal bool DestinationConsumed;
        }

        private sealed class AutonomousLineOfSightRecord
        {
            internal Vector3 Origin;
            internal Vector3 FocusPoint;
            internal bool Visible;
            internal float ExpiresAt;
        }

        private sealed class HeldSummonState
        {
            internal Vector3 Anchor;
        }

        private sealed class GuardIdleState
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

        private sealed class HuntIdleState
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

        private sealed class FormationCommandViewCache
        {
            internal GameObject ViewObject;
            internal Renderer[] Renderers;
        }

        private sealed class SwarmState
        {
            internal NpcElement Target;
            internal float ExpiresAt;
            internal float MovementMultiplier;
            internal StatTweak MovementTweak;
        }

        private sealed class EmpowermentState
        {
            internal float Multiplier;
            internal float MovementMultiplier;
            internal StatTweak MovementTweak;
            internal Transform VisualRoot;
            internal Vector3 OriginalLocalScale;
            internal Vector3 OriginalLocalPosition;
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

        private static readonly Dictionary<string, List<CollisionPair>> CollisionPairs =
            new Dictionary<string, List<CollisionPair>>();
        private static readonly Dictionary<string, StatTweak> SpeedTweaks =
            new Dictionary<string, StatTweak>();
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
        private static readonly Dictionary<string, PendingTeleportVfx>
            PendingTeleportVfxBySummon =
                new Dictionary<string, PendingTeleportVfx>();
        private static readonly Dictionary<string, PendingRecallPlacement>
            PendingRecallPlacements =
                new Dictionary<string, PendingRecallPlacement>();
        private static readonly Dictionary<string, float>
            RecallTargetSuppressionUntil =
                new Dictionary<string, float>();
        private static readonly Dictionary<string, AutonomousLineOfSightRecord>
            AutonomousLineOfSightByTarget =
                new Dictionary<string, AutonomousLineOfSightRecord>();
        private static readonly Dictionary<string, NpcElement>
            ExplicitCommandTargets =
                new Dictionary<string, NpcElement>();
        private static readonly Dictionary<string, HeldSummonState>
            HeldSummons =
                new Dictionary<string, HeldSummonState>();
        private static readonly Dictionary<string, GuardIdleState>
            GuardIdleStates =
                new Dictionary<string, GuardIdleState>();
        private static readonly Dictionary<string, HuntIdleState>
            HuntIdleStates =
                new Dictionary<string, HuntIdleState>();
        private static readonly HashSet<string> HuntIdleMoverIds =
            new HashSet<string>();
        private static readonly Dictionary<string, FormationCommandViewCache>
            FormationCommandViewCaches =
                new Dictionary<string, FormationCommandViewCache>();
        private static readonly Dictionary<string, SwarmState> SwarmStates =
            new Dictionary<string, SwarmState>();
        private static readonly Dictionary<string, EmpowermentState>
            EmpowermentStates =
                new Dictionary<string, EmpowermentState>();
        private static readonly Dictionary<string, Vector3>
            FormationPatrolAnchors =
                new Dictionary<string, Vector3>();
        private static readonly Dictionary<int, float>
            LocomotionPlaybackMultipliers =
                new Dictionary<int, float>();
        private static readonly Dictionary<string, float>
            NextControllerRefreshBySummon =
                new Dictionary<string, float>();
        private static readonly Dictionary<string, string>
            LastControlDiagnosticBySummon =
                new Dictionary<string, string>();
        private static readonly Dictionary<string, float>
            NextControlDiagnosticBySummon =
                new Dictionary<string, float>();
        private static readonly RaycastHit[] FormationCommandRaycastHits =
            new RaycastHit[32];
        private static readonly RaycastHit[] AutonomousTargetRaycastHits =
            new RaycastHit[32];
        private static float _nextCollisionRefreshTime;
        private static float _nextTransientStatePruneTime;
        private static float _nextAiDecisionIntervalRefreshTime;
        private static float _cachedAiDecisionInterval = NoviceAiDecisionInterval;
        private static int _formationHostCacheFrame = -1;
        private static Hero _formationHostCacheHero;
        private static NpcHeroSummon[] _formationHostCache =
            new NpcHeroSummon[0];
        private static int _hostCombatCacheFrame = -1;
        private static Hero _hostCombatCacheHero;
        private static bool _hostCombatCacheValue;
        private static MethodInfo _steelAndBoneSightMultiplierMethod;
        private static MethodInfo _steelAndBoneAggroMultiplierMethod;
        private static bool _steelAndBoneAwarenessUnavailable;
        private static float _nextSteelAndBoneAwarenessRefreshAt;
        private static float _cachedSteelAndBoneSightMultiplier = 1.0f;
        private static float _cachedSteelAndBoneAggroMultiplier = 1.0f;
        private static NpcGrid _bulwarkTargetCandidateGrid;
        private static Vector3 _bulwarkTargetCandidateCenter;
        private static float _bulwarkTargetCandidateExpiresAt;
        private static NpcElement[] _bulwarkTargetCandidates =
            new NpcElement[0];
        private static MethodInfo _battlecryTryPlayCommandMethod;
        private static bool _battlecryCommandApiUnavailable;
        private static SummonCommandInteractable _commandInteractable;
        private static string _lastFormationFocusDiagnostic = string.Empty;
        private static bool _takeAllItemsHeld;
        private static bool _formationCommandArmedForRelease;
        private static bool _recallCommandAttemptedForHold;
        private static float _takeAllItemsPressedAt;
        private static bool _behaviorCommandHeld;
        private static bool _behaviorCommandAttemptedForHold;
        private static float _behaviorCommandPressedAt;
        private static float _commandFeedbackEndsAt;
        private static SummonCommandState _lastCommandState;
        private static float _lastCommandPulseSeconds =
            StandardCommandFeedbackSeconds;
        private static int _commandSequence;
        private static string _guardIdleMoverId;
        private static float _nextIdleHostAttemptAt;
        private static bool _hasBulwarkForward;
        private static Vector3 _bulwarkForward = Vector3.forward;
        private static bool _hasGuardForward;
        private static Vector3 _guardForward = Vector3.forward;
        private static Hero _formationFacingHero;
        private static int _formationLeaderMotionFrame = -1;
        private static Hero _formationLeaderMotionHero;
        private static Vector3 _formationLeaderAnchor;
        private static bool _hasFormationLeaderAnchor;
        private static bool _formationLeaderMoving;
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

        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "UnityUpdate"),
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
                TeleportToAllyMethod,
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeTeleportToAlly)));
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
            }
            UpdateBehaviorCommandHold();
            UpdateTakeAllItemsHold();
            UpdateCommandOverride(plugin);
            PruneHeldSummons();

            if (!plugin.IsEnabled)
            {
                ClearAllServantPowerStates();
                RestoreAllCollisionPairs();
                RemoveAllAwarenessTargets();
                ExplicitCommandTargets.Clear();
                RecentAttackers.Clear();
                PendingTeleportVfxBySummon.Clear();
                PendingRecallPlacements.Clear();
                RecallTargetSuppressionUntil.Clear();
                AutonomousLineOfSightByTarget.Clear();
                GuardIdleStates.Clear();
                HuntIdleStates.Clear();
                HuntIdleMoverIds.Clear();
                FormationPatrolAnchors.Clear();
                LastControlDiagnosticBySummon.Clear();
                NextControlDiagnosticBySummon.Clear();
                _guardIdleMoverId = null;
                _nextIdleHostAttemptAt = 0.0f;
                ResetFormationLeaderMotion();
                ResetBehaviorCommandHold();
                return;
            }

            UpdateFormationLeaderMotion(Hero.Current);

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
            RestoreAllCollisionPairs();
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                RestorePatrolRadius(summon);
            }
            StabilizedPatrols.Clear();
            RemoveAllAwarenessTargets();
            ExplicitCommandTargets.Clear();
            RecentAttackers.Clear();
            PendingTeleportVfxBySummon.Clear();
            PendingRecallPlacements.Clear();
            RecallTargetSuppressionUntil.Clear();
            AutonomousLineOfSightByTarget.Clear();
            HeldSummons.Clear();
            GuardIdleStates.Clear();
            HuntIdleStates.Clear();
            HuntIdleMoverIds.Clear();
            FormationPatrolAnchors.Clear();
            _bulwarkTargetCandidateExpiresAt = 0.0f;
            _bulwarkTargetCandidates = new NpcElement[0];
            _guardIdleMoverId = null;
            _nextIdleHostAttemptAt = 0.0f;
            _hasBulwarkForward = false;
            _bulwarkForward = Vector3.forward;
            _hasGuardForward = false;
            _guardForward = Vector3.forward;
            _formationFacingHero = null;
            ResetFormationLeaderMotion();
            FormationCommandViewCaches.Clear();
            NextControllerRefreshBySummon.Clear();
            LastControlDiagnosticBySummon.Clear();
            NextControlDiagnosticBySummon.Clear();
            ClearAllServantPowerStates();
            ResetTakeAllItemsHold();
            ResetBehaviorCommandHold();
            foreach (StatTweak tweak in SpeedTweaks.Values.ToArray())
            {
                DiscardTweak(tweak);
            }
            SpeedTweaks.Clear();
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
            _formationHostCacheFrame = -1;
            _formationHostCacheHero = null;
            _formationHostCache = new NpcHeroSummon[0];
            _hostCombatCacheFrame = -1;
            _hostCombatCacheHero = null;
            _hostCombatCacheValue = false;
            _nextSteelAndBoneAwarenessRefreshAt = 0.0f;
            _cachedSteelAndBoneSightMultiplier = 1.0f;
            _cachedSteelAndBoneAggroMultiplier = 1.0f;
            _bulwarkTargetCandidateGrid = null;
            _bulwarkTargetCandidateExpiresAt = 0.0f;
            _bulwarkTargetCandidates = new NpcElement[0];
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
            if (EmpowermentStates.ContainsKey(id))
            {
                return false;
            }

            NpcElement npc = summon.ParentModel;
            EmpowermentState state = new EmpowermentState
            {
                Multiplier = Mathf.Clamp(multiplier, 1.20f, 1.50f)
            };
            state.MovementMultiplier = Mathf.Sqrt(state.Multiplier);
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
            EnsureEmpowermentVisualEnforcer(npc.Controller);
            ApplyEmpowermentVisual(npc.Controller, state);

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogDiagnostic(
                    "Empowered summon " + id + " at "
                    + state.Multiplier.ToString("0.###")
                    + "x size, outgoing damage, and resistance.");
            }
            return true;
        }

        internal static bool IsEmpoweredSummon(NpcHeroSummon summon)
        {
            return summon != null
                && EmpowermentStates.ContainsKey(((Model)summon).ID);
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
            NpcHeroSummon[] summons = hero == null
                ? new NpcHeroSummon[0]
                : World.All<NpcHeroSummon>()
                    .Where(summon => IsOwnedSummon(summon, hero))
                    .ToArray();
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
            float movementMultiplier = Math.Min(
                SwarmMovementMultiplier,
                MaximumCommandMovementMultiplier / empowermentMovement);
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
        }

        private static void UpdateSwarmStates()
        {
            foreach (KeyValuePair<string, SwarmState> pair
                in SwarmStates.ToArray())
            {
                SwarmState state = pair.Value;
                if (state.Target == null
                    || state.Target.HasBeenDiscarded
                    || !state.Target.IsAlive
                    || Time.time >= state.ExpiresAt)
                {
                    ClearSwarm(pair.Key);
                }
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
            Transform visualRoot = GetEmpowermentVisualRoot(controller);
            if (visualRoot == null)
            {
                LogEmpowermentVisualRootFailure(controller, state);
                return;
            }
            if (!ReferenceEquals(state.VisualRoot, visualRoot))
            {
                RestoreEmpowermentVisual(state);
                state.VisualRoot = visualRoot;
                state.OriginalLocalScale = visualRoot.localScale;
                state.OriginalLocalPosition = visualRoot.localPosition;
            }

            Vector3 expectedScale = Vector3.Scale(
                state.OriginalLocalScale,
                Vector3.one * state.Multiplier);
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
            if (!ReferenceEquals(visualRoot, controller.transform))
            {
                return visualRoot;
            }
            if (controller.Animator != null
                && !ReferenceEquals(
                    controller.Animator.transform,
                    controller.transform))
            {
                return controller.Animator.transform;
            }
            return controller.RootMotion != null
                && !ReferenceEquals(
                    controller.RootMotion.transform,
                    controller.transform)
                    ? controller.RootMotion.transform
                    : null;
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
                if (renderer == null
                    || string.Equals(
                        renderer.GetType().Name,
                        "ParticleSystemRenderer",
                        StringComparison.Ordinal)
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
            plugin.LogDiagnostic(
                "Empower visual correction " + state.VisualDiagnosticCount
                + "/3: root=" + GetTransformPath(state.VisualRoot)
                + "; controller=" + GetTransformPath(controller.transform)
                + "; beforeLocal=" + FormatVector3(beforeScale)
                + "; expectedLocal=" + FormatVector3(expectedScale)
                + "; actualLocal=" + FormatVector3(state.VisualRoot.localScale)
                + "; lossy=" + FormatVector3(state.VisualRoot.lossyScale)
                + "; renderers=" + rendererCount
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
                if (renderer == null
                    || string.Equals(
                        renderer.GetType().Name,
                        "ParticleSystemRenderer",
                        StringComparison.Ordinal)
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
            if (plugin == null
                || !plugin.IsEnabled
                || !(ally is NpcHeroSummon))
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
            return _cachedAiDecisionInterval;
        }

        private static float GetSpawnRecoverySeconds()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin != null && plugin.IsEnabled
                ? plugin.SpawnRecoverySeconds.Value
                : 1.5f;
        }

        private static bool BeforeTeleportToAlly(NpcAlly __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcHeroSummon summon = __instance as NpcHeroSummon;
            if (plugin == null
                || !plugin.IsEnabled
                || summon == null
                || summon.ParentModel == null
                || summon.ParentModel.HasBeenDiscarded)
            {
                return true;
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
            return true;
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
                SoulSalvageRuntime.SpawnNecromanticSummonVfx(summon.ParentModel);
                return;
            }
            pending.OutstandingRequests--;
            if (pending.OutstandingRequests <= 0)
            {
                PendingTeleportVfxBySummon.Remove(summonId);
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
            if (placement == null
                || placement.DestinationConsumed
                || placement.ExpiresAt < Time.unscaledTime)
            {
                if (placement == null || placement.ExpiresAt < Time.unscaledTime)
                {
                    PendingRecallPlacements.Remove(summonId);
                }
                return;
            }
            destination.position = placement.Position;
            placement.DestinationConsumed = true;
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
                || !placement.DestinationConsumed)
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

            HeldSummonState heldState;
            if (HeldSummons.TryGetValue(((Model)summon).ID, out heldState))
            {
                FormationPatrolAnchors.Remove(((Model)summon).ID);
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
            if (!HasExplicitCommandTarget(summon)
                && TryGetRecallAnchor(summon, out recallAnchor))
            {
                FormationPatrolAnchors.Remove(((Model)summon).ID);
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
            bool hasExplicitCommandTarget = HasExplicitCommandTarget(summon);
            bool usesHuntFormation = behavior == SummonBehavior.Hunt;
            if (!usesGuardFormation || hasExplicitCommandTarget)
            {
                ClearGuardIdleState(((Model)summon).ID);
            }
            if (!usesHuntFormation || hasExplicitCommandTarget)
            {
                ClearHuntIdleState(((Model)summon).ID);
            }
            if ((behavior == SummonBehavior.Bulwark
                    || usesGuardFormation
                    || usesHuntFormation)
                && !hasExplicitCommandTarget)
            {
                float anchorTolerance = 0.0f;
                bool gentleIdleMovement = false;
                Vector3 anchor;
                if (behavior == SummonBehavior.Bulwark)
                {
                    anchor = GetBulwarkAnchor(summon);
                    anchorTolerance = BulwarkAnchorTolerance;
                }
                else if (behavior == SummonBehavior.Hunt)
                {
                    anchor = GetHuntIdleAnchor(
                        summon,
                        out anchorTolerance,
                        out gentleIdleMovement);
                }
                else
                {
                    anchor = GetGuardIdleAnchor(
                        summon,
                        out anchorTolerance,
                        out gentleIdleMovement);
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
                    TeleportToAllyMethod.Invoke(__instance, arguments);
                    return false;
                }

                StabilizedPatrols.Add(((Model)summon).ID);
                patrol.UpdateRadius(anchorTolerance);
                UpdateFormationPatrolPlace(summon, patrol, anchor);
                patrol.UpdateVelocityScheme(
                    gentleIdleMovement
                        ? VelocityScheme.Walk
                        : GetBulwarkVelocityScheme(
                            summon.Ally,
                            anchorDistanceSqr));
                return false;
            }

            FormationPatrolAnchors.Remove(((Model)summon).ID);

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

        private static bool HasExplicitCommandTarget(NpcHeroSummon summon)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return false;
            }
            HeroSummonTargetOverride command =
                summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
            string id = ((Model)summon).ID;
            NpcElement target;
            if (!ExplicitCommandTargets.TryGetValue(id, out target)
                || target == null
                || target.HasBeenDiscarded
                || !target.IsAlive
                || command == null
                || !ReferenceEquals(command.Target, target))
            {
                ExplicitCommandTargets.Remove(id);
                return false;
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
            RemoveAwarenessTargetsForSummon(summon, target);
            if (previousTarget != null
                && !previousTarget.HasBeenDiscarded
                && !ReferenceEquals(previousTarget, target))
            {
                summon.ParentModel.RemoveCombatTarget(previousTarget);
            }
            ExplicitCommandTargets[summonId] = target;
            HeroSummonTargetOverride.AddSummonTargetOverrideElement(
                summon,
                target,
                10);
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
            HeroSummonTargetOverride targetOverride =
                summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
            ICharacter currentTarget = summon.ParentModel.GetCurrentTarget();
            string signature =
                SoulProgressionRuntime.GetSummonBehavior()
                + "|held=" + IsHeld(summon)
                + "|combat=" + (summon.ParentModel.NpcAI != null
                    && summon.ParentModel.NpcAI.InCombat)
                + "|current=" + GetControlTargetId(currentTarget)
                + "|explicit=" + GetControlTargetId(explicitTarget)
                + "|autonomous=" + GetControlTargetId(autonomousTarget)
                + "|override=" + GetControlTargetId(
                    targetOverride == null ? null : targetOverride.Target);
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
            int frame = Time.frameCount;
            if (_formationHostCacheFrame != frame
                || !ReferenceEquals(_formationHostCacheHero, hero))
            {
                _formationHostCacheFrame = frame;
                _formationHostCacheHero = hero;
                _formationHostCache = hero == null
                    ? new NpcHeroSummon[0]
                    : World.All<NpcHeroSummon>()
                        .Where(candidate => IsOwnedSummon(candidate, hero))
                        .OrderBy(candidate => ((Model)candidate).ID)
                        .ToArray();
            }
            return _formationHostCache;
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
            GuardIdleState state;
            if (!GuardIdleStates.TryGetValue(summonId, out state))
            {
                state = new GuardIdleState();
                GuardIdleStates[summonId] = state;
            }

            bool heroMoving = IsFormationLeaderMoving(hero);
            bool hostInCombat = IsHostInCombat(hero);
            if (heroMoving || hostInCombat)
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

        private static void ScheduleNextGuardIdleWander(GuardIdleState state)
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
            GuardIdleState state)
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
            GuardIdleState state;
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
            HuntIdleState state;
            if (!HuntIdleStates.TryGetValue(summonId, out state))
            {
                state = new HuntIdleState();
                HuntIdleStates[summonId] = state;
            }

            bool heroMoving = IsFormationLeaderMoving(hero);
            bool hostInCombat = IsHostInCombat(hero);
            if (heroMoving || hostInCombat)
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

        private static void ScheduleNextHuntIdleWander(HuntIdleState state)
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
            HuntIdleState state)
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
            HuntIdleState state;
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

        private static void ResetFormationLeaderMotion()
        {
            _formationLeaderMotionFrame = -1;
            _formationLeaderMotionHero = null;
            _formationLeaderAnchor = Vector3.zero;
            _hasFormationLeaderAnchor = false;
            _formationLeaderMoving = false;
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
                _formationLeaderAnchor = hero.Coords;
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
                _formationLeaderAnchor = hero.Coords;
                _formationLeaderStoppedAt = -1.0f;
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
            if (hero == null || summon == null)
            {
                return summon == null || summon.ParentModel == null
                    ? Vector3.zero
                    : summon.ParentModel.Coords;
            }

            NpcHeroSummon[] host = GetFormationHost(hero);
            int index = Math.Max(0, Array.IndexOf(host, summon));
            int ring = index / HuntFormationSlotsPerRing;
            int firstInRing = ring * HuntFormationSlotsPerRing;
            int countInRing = Math.Min(
                HuntFormationSlotsPerRing,
                host.Length - firstInRing);
            int slot = index - firstInRing;
            float angle = countInRing <= 1
                ? 180.0f
                : 90.0f + ((360.0f * slot) / countInRing);
            float distance = HuntFormationInnerRadius
                + (HuntFormationRingSpacing * ring);
            return GetFormationLeaderAnchor(hero)
                + (Quaternion.AngleAxis(angle, Vector3.up)
                    * Vector3.forward * distance);
        }

        private static Vector3 GetBulwarkAnchor(NpcHeroSummon summon)
        {
            Hero hero = Hero.Current;
            if (hero == null || summon == null)
            {
                return summon == null || summon.ParentModel == null
                    ? Vector3.zero
                    : summon.ParentModel.Coords;
            }
            EnsureFormationFacingHero(hero);

            NpcHeroSummon[] host = GetFormationHost(hero);
            int index = Array.IndexOf(host, summon);
            if (index < 0)
            {
                index = 0;
            }
            int ring = index / BulwarkFormationSlotsPerRing;
            int firstInRing = ring * BulwarkFormationSlotsPerRing;
            int countInRing = Math.Min(
                BulwarkFormationSlotsPerRing,
                host.Length - firstInRing);
            int slot = index - firstInRing;
            float halfArc = countInRing == 2
                ? 25.0f
                : countInRing == 3
                    ? 45.0f
                    : 60.0f;
            float angle = countInRing <= 1
                ? 0.0f
                : Mathf.Lerp(-halfArc, halfArc, slot / (countInRing - 1.0f));
            float distance = BulwarkFormationInnerRadius
                + (BulwarkFormationRingSpacing * ring);
            Vector3 movementForward = hero.HorizontalVelocity;
            movementForward.y = 0.0f;
            if (IsFormationLeaderMoving(hero)
                && movementForward.magnitude >= BulwarkLeaderMovingSpeed)
            {
                _bulwarkForward = movementForward.normalized;
                _hasBulwarkForward = true;
            }
            else if (!_hasBulwarkForward)
            {
                Vector3 initialForward = hero.VHeroController == null
                    ? Vector3.forward
                    : hero.VHeroController.transform.forward;
                initialForward.y = 0.0f;
                _bulwarkForward = initialForward.sqrMagnitude <= 0.0001f
                    ? Vector3.forward
                    : initialForward.normalized;
                _hasBulwarkForward = true;
            }
            return GetFormationLeaderAnchor(hero)
                + (Quaternion.AngleAxis(angle, Vector3.up)
                    * _bulwarkForward * distance);
        }

        private static Vector3 GetGuardAnchor(NpcHeroSummon summon)
        {
            Hero hero = Hero.Current;
            if (hero == null || summon == null)
            {
                return summon == null || summon.ParentModel == null
                    ? Vector3.zero
                    : summon.ParentModel.Coords;
            }
            EnsureFormationFacingHero(hero);

            NpcHeroSummon[] host = GetFormationHost(hero);
            int index = Math.Max(0, Array.IndexOf(host, summon));
            int ring = index / 5;
            int firstInRing = ring * 5;
            int countInRing = Math.Min(5, host.Length - firstInRing);
            int slot = index - firstInRing;
            float angle = countInRing <= 1
                ? 180.0f
                : Mathf.Lerp(105.0f, 255.0f, slot / (countInRing - 1.0f));
            float distance = GuardFormationInnerRadius
                + (GuardFormationRingSpacing * ring);
            Vector3 movementForward = hero.HorizontalVelocity;
            movementForward.y = 0.0f;
            if (IsFormationLeaderMoving(hero)
                && movementForward.magnitude >= GuardLeaderMovingSpeed)
            {
                _guardForward = movementForward.normalized;
                _hasGuardForward = true;
            }
            else if (!_hasGuardForward)
            {
                Vector3 initialForward = hero.VHeroController == null
                    ? Vector3.forward
                    : hero.VHeroController.transform.forward;
                initialForward.y = 0.0f;
                _guardForward = initialForward.sqrMagnitude <= 0.0001f
                    ? Vector3.forward
                    : initialForward.normalized;
                _hasGuardForward = true;
            }
            return GetFormationLeaderAnchor(hero)
                + (Quaternion.AngleAxis(angle, Vector3.up) * _guardForward * distance);
        }

        private static void EnsureFormationFacingHero(Hero hero)
        {
            if (ReferenceEquals(_formationFacingHero, hero))
            {
                return;
            }
            _formationFacingHero = hero;
            _hasBulwarkForward = false;
            _bulwarkForward = Vector3.forward;
            _hasGuardForward = false;
            _guardForward = Vector3.forward;
        }

        private static VelocityScheme GetBulwarkVelocityScheme(
            ICharacter leader,
            float anchorDistanceSqr)
        {
            float leaderSpeed = leader == null
                ? 0.0f
                : leader.HorizontalVelocity.magnitude;
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

            HeroSummonTargetOverride existingOverride =
                summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
            if (existingOverride != null)
            {
                if (existingOverride.Target != null)
                {
                    return;
                }
                existingOverride.Discard();
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
                    SoulProgressionRuntime.GetSummonBehavior(),
                    hero,
                    IsHostInCombat(hero)) == int.MaxValue)
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
            PlayerInput input = hero.VHeroController == null
                ? null
                : hero.VHeroController.Input;
            return input != null
                && input.GetButtonHeld(KeyBindings.Gameplay.Sprint);
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
                || hero == null
                || target == null
                || target.HasBeenDiscarded
                || !target.IsAlive
                || target.IsUnconscious
                || target.IsHeroSummon)
            {
                return false;
            }

            float commandRange = GetTargetingRange(plugin);
            if ((target.Coords - hero.Coords).sqrMagnitude
                > commandRange * commandRange)
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
                    string rendererType = renderer == null
                        ? string.Empty
                        : renderer.GetType().Name;
                    if (renderer == null
                        || string.Equals(
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
                            StringComparison.Ordinal))
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
            out string diagnostic)
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
                if (!CanIssueFormationCommand(hero, directSummon))
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
                HeroSummonTargetOverride explicitTarget =
                    summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
                if (explicitTarget != null)
                {
                    explicitTarget.Discard();
                }
                ExplicitCommandTargets.Remove(summonId);
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

            List<NpcHeroSummon> summons = World.All<NpcHeroSummon>()
                .Where(summon => IsOwnedSummon(summon, hero)
                    && summon.ParentModel != null
                    && summon.ParentModel.Movement != null)
                .OrderBy(summon => ((Model)summon).ID)
                .ToList();
            Pathfinding.GraphNode heroNode = AstarPath.active == null
                ? null
                : AstarPath.active.GetNearest(
                    hero.Coords,
                    Pathfinding.NNConstraint.Walkable).node;
            float recallRotation = UnityEngine.Random.Range(-15.0f, 15.0f);
            List<Vector3> reservedPlacements = new List<Vector3>();
            int recalled = 0;
            for (int index = 0; index < summons.Count; index++)
            {
                NpcHeroSummon summon = summons[index];
                string summonId = ((Model)summon).ID;
                ClearIdleMovementState(summonId);
                RecallTargetSuppressionUntil[summonId] =
                    Time.unscaledTime + RecallTargetSuppressionSeconds;
                ExplicitCommandTargets.Remove(summonId);
                AutonomousTargetOverrides.Remove(summonId);
                RemoveAwarenessTargetsForSummon(summon);
                ClearSwarm(summonId);
                SetSummonHeld(summon, false);
                HeroSummonTargetOverride targetOverride =
                    summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
                if (targetOverride != null)
                {
                    targetOverride.Discard();
                }
                summon.ParentModel.ForceEndCombat();
                LogSummonControlState(summon, "recall");

                Vector3 recallPlacement;
                if (TryFindRecallPlacement(
                        hero,
                        heroNode,
                        index,
                        summons.Count,
                        recallRotation,
                        reservedPlacements,
                        out recallPlacement))
                {
                    PendingRecallPlacements[summonId] =
                        new PendingRecallPlacement
                        {
                            Position = recallPlacement,
                            HeroOrigin = hero.Coords,
                            ExpiresAt = Time.unscaledTime
                                + RecallPlacementLifetimeSeconds,
                            DestinationConsumed = false
                        };
                    reservedPlacements.Add(recallPlacement);
                }
                else
                {
                    PendingRecallPlacements.Remove(summonId);
                }
                float distanceSqr =
                    (hero.Coords - summon.ParentModel.Coords).sqrMagnitude;
                object[] arguments =
                {
                    distanceSqr,
                    TeleportContext.AllyRanAway,
                    Vector3.zero
                };
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

        private static bool TryFindRecallPlacement(
            Hero hero,
            Pathfinding.GraphNode heroNode,
            int summonIndex,
            int summonCount,
            float recallRotation,
            List<Vector3> reservedPlacements,
            out Vector3 placement)
        {
            placement = Vector3.zero;
            if (hero == null
                || heroNode == null
                || AstarPath.active == null)
            {
                return false;
            }

            int ring = summonIndex / RecallPlacementsPerRing;
            int firstInRing = ring * RecallPlacementsPerRing;
            int countInRing = Math.Min(
                RecallPlacementsPerRing,
                summonCount - firstInRing);
            int slot = summonIndex - firstInRing;
            float cellDegrees = RecallPlacementArcDegrees / countInRing;
            float baseAngle = countInRing == 1
                ? (UnityEngine.Random.value < 0.5f
                    ? UnityEngine.Random.Range(
                        RecallSingleSideMinimumDegrees,
                        RecallSingleSideMaximumDegrees)
                    : UnityEngine.Random.Range(
                        360.0f - RecallSingleSideMaximumDegrees,
                        360.0f - RecallSingleSideMinimumDegrees))
                    + recallRotation
                : RecallPlacementArcStartDegrees
                    + ((slot + 0.5f) * cellDegrees)
                    + recallRotation;
            float angleJitter = Math.Min(12.0f, cellDegrees * 0.18f);
            baseAngle += UnityEngine.Random.Range(-angleJitter, angleJitter);
            float baseRadius = RecallPlacementInnerRadius
                + (ring * RecallPlacementRingSpacing)
                + UnityEngine.Random.Range(-0.35f, 0.35f);
            Vector3 forward = hero.VHeroController == null
                ? Vector3.forward
                : hero.VHeroController.transform.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            for (int attempt = 0; attempt < RecallPlacementAttempts; attempt++)
            {
                int offsetStep = (attempt + 1) / 2;
                float angleOffset = attempt == 0
                    ? 0.0f
                    : (attempt % 2 == 1 ? 1.0f : -1.0f)
                        * 18.0f * offsetStep;
                float radius = baseRadius + ((attempt / 6) * 1.25f);
                Vector3 candidate = hero.Coords
                    + (Quaternion.AngleAxis(
                            baseAngle + angleOffset,
                            Vector3.up)
                        * forward * radius);
                Pathfinding.NNInfo nearest =
                    AstarPath.active.GetNearest(
                    candidate,
                    Pathfinding.NNConstraint.Walkable);
                if (nearest.node == null
                    || !Pathfinding.PathUtilities.IsPathPossible(
                        heroNode,
                        nearest.node))
                {
                    continue;
                }
                Vector3 snapped = nearest.position;
                if ((snapped - candidate).sqrMagnitude
                        > RecallPlacementMaximumSnapDistance
                            * RecallPlacementMaximumSnapDistance
                    || Math.Abs(snapped.y - hero.Coords.y) > 4.0f
                    || (snapped - hero.Coords).sqrMagnitude < 4.0f
                    || reservedPlacements.Any(existing =>
                        (existing - snapped).sqrMagnitude
                            < RecallPlacementMinimumSpacing
                                * RecallPlacementMinimumSpacing))
                {
                    continue;
                }
                placement = snapped;
                return true;
            }
            return false;
        }

        private static void PruneHeldSummons()
        {
            if (HeldSummons.Count == 0)
            {
                return;
            }

            HashSet<string> activeIds = new HashSet<string>(
                World.All<NpcHeroSummon>()
                    .Where(summon => IsOwnedSummon(summon, Hero.Current))
                    .Select(summon => ((Model)summon).ID));
            foreach (string summonId in HeldSummons.Keys.ToArray())
            {
                if (!activeIds.Contains(summonId))
                {
                    HeldSummons.Remove(summonId);
                }
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
                FormationPatrolAnchors.Remove(((Model)summon).ID);
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
            Vector3 anchor)
        {
            if (summon == null || patrol == null)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            Vector3 previousAnchor;
            if (FormationPatrolAnchors.TryGetValue(
                    summonId,
                    out previousAnchor)
                && (previousAnchor - anchor).sqrMagnitude
                    <= FormationPatrolAnchorUpdateDistance
                        * FormationPatrolAnchorUpdateDistance)
            {
                return;
            }
            FormationPatrolAnchors[summonId] = anchor;
            patrol.UpdatePlace(anchor);
        }

        private sealed class SummonCommandInteractable : IInteractableWithHero
        {
            private readonly NpcElement _target;
            private readonly NpcHeroSummon _summon;
            private readonly GameObject _viewObject;
            private readonly SummonCommandAction _action;
            private readonly string _feedbackText;
            private readonly bool _soulRendHover;

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
            }

            internal NpcElement Target => _target;

            internal NpcHeroSummon Summon => _summon;

            internal SummonCommandState Kind { get; private set; }

            internal bool IsFeedback => !string.IsNullOrEmpty(_feedbackText);

            internal bool IsSoulRendHover => _soulRendHover;

            internal string FeedbackText => _feedbackText;

            internal GameObject ViewObject => _viewObject;

            public bool Interactable => IsFeedback
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

            public string DisplayName => string.Empty;

            public GameObject InteractionVSGameObject => _viewObject;

            public Vector3 InteractionPosition => IsFeedback
                ? Hero.Current == null ? Vector3.zero : Hero.Current.Coords
                : _target != null
                ? _target.Coords
                : _summon == null || _summon.ParentModel == null
                    ? Vector3.zero
                    : _summon.ParentModel.Coords;

            public IEnumerable<IHeroAction> AvailableActions(Hero hero)
            {
                yield return _action;
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

            internal SummonCommandAction(
                NpcElement target,
                NpcHeroSummon summon,
                SummonCommandState kind,
                string actionName = null,
                bool feedbackOnly = false)
            {
                _target = target;
                _summon = summon;
                _kind = kind;
                _actionName = actionName;
                _feedbackOnly = feedbackOnly;
            }

            public bool IsValidAction => _feedbackOnly
                || (_kind == SummonCommandState.Attack
                    ? _target != null && !_target.HasBeenDiscarded
                    : _summon != null && !_summon.HasBeenDiscarded);

            public bool IsIllegal => false;

            public bool IsFeedbackOnly => _feedbackOnly;

            public InfoFrame ActionFrame =>
                new InfoFrame(DefaultActionName, true);

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
                if (_kind == SummonCommandState.Attack)
                {
                    return CanCommandSummons(hero, _target)
                        && CommandSummons(hero, _target) > 0;
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
            }

            public void EndInteraction(
                Hero hero,
                IInteractableWithHero interactable)
            {
            }

            public ActionAvailability GetAvailability(
                Hero hero,
                IInteractableWithHero interactable)
            {
                bool available = _feedbackOnly
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
                if (raycaster != null
                    && Time.unscaledTime < _commandFeedbackEndsAt)
                {
                    return;
                }
                ClearCommandOverride();
            }
            if (_behaviorCommandHeld)
            {
                ClearCommandOverride();
                return;
            }
            if (!plugin.IsEnabled
                || ((!plugin.AttackCommandPrompt.Value)
                    && (plugin.FormationCommands == null
                        || !plugin.FormationCommands.Value))
                || raycaster == null)
            {
                ClearCommandOverride();
                return;
            }

            RaycastCheck detection = NpcDetectionField == null
                ? null
                : NpcDetectionField.GetValue(raycaster) as RaycastCheck;
            raycaster.GetViewRay(out Vector3 origin, out Vector3 direction);

            if (!IsTargetCommandModifierHeld(plugin, hero)
                || (!HasAttackCommandControl()
                    && !HasIndividualFormationControl()))
            {
                ClearCommandOverride();
                return;
            }

            if (plugin.FormationCommands != null
                && plugin.FormationCommands.Value
                && HasIndividualFormationControl())
            {
                if (TryFindFocusedFormationSummon(
                        hero,
                        detection,
                        origin,
                        direction,
                        GetTargetingRange(plugin),
                        out NpcHeroSummon summon,
                        out GameObject summonViewObject,
                        out string diagnostic))
                {
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

            if (detection == null)
            {
                ClearCommandOverride();
                return;
            }

            if (!plugin.AttackCommandPrompt.Value)
            {
                ClearCommandOverride();
                return;
            }

            Collider collider = detection.Detected(
                origin,
                direction,
                GetTargetingRange(plugin));
            VLocation view = collider == null
                ? null
                : collider.GetComponentInParent<LocationParent>()
                    ?.GetComponentInChildren<VLocation>();
            NpcElement target = view == null || view.Target == null
                ? null
                : view.Target.TryGetElement<NpcElement>();
            if (!CanCommandSummons(hero, target))
            {
                ClearCommandOverride();
                return;
            }
            if (_commandInteractable != null
                && ReferenceEquals(_commandInteractable.Target, target))
            {
                return;
            }
            ClearCommandOverride();
            _commandInteractable = new SummonCommandInteractable(
                target,
                collider.gameObject);
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

            if (!ShouldOwnTakeAllHoldForInterop())
            {
                ResetTakeAllItemsHold();
                return true;
            }

            if (evt is UIKeyDownAction)
            {
                _takeAllItemsHeld = true;
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
                    && !_recallCommandAttemptedForHold;
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
            if (evt is UIKeyUpAction && _behaviorCommandHeld)
            {
                ResetBehaviorCommandHold();
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
                if (!CanStartBehaviorCommandHold())
                {
                    ResetBehaviorCommandHold();
                    return true;
                }
                _behaviorCommandHeld = true;
                _behaviorCommandAttemptedForHold = false;
                _behaviorCommandPressedAt = Time.unscaledTime;
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

        private static bool CanStartBehaviorCommandHold()
        {
            Hero hero = Hero.Current;
            PlayerInput input = hero == null || hero.VHeroController == null
                ? null
                : hero.VHeroController.Input;
            return CanMaintainBehaviorCommandHold()
                && input != null
                && input.GetButtonHeld(KeyBindings.Gameplay.Sprint)
                && _commandInteractable == null
                && !hero.VHeroController.Raycaster.GetAvailableActions().Any();
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
            return World.All<NpcHeroSummon>()
                .Any(summon => IsOwnedSummon(summon, hero));
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
            CycleSummonBehavior();
        }

        private static void UpdateTakeAllItemsHold()
        {
            if (!_takeAllItemsHeld)
            {
                return;
            }
            if (!ShouldOwnTakeAllHoldForInterop())
            {
                ResetTakeAllItemsHold();
                return;
            }

            float elapsed = Time.unscaledTime - _takeAllItemsPressedAt;
            if (elapsed >= FormationCommandHoldSeconds)
            {
                _formationCommandArmedForRelease = true;
            }
            if (!_recallCommandAttemptedForHold
                && elapsed >= RecallCommandHoldSeconds
                && HasRecallCommandControl())
            {
                _recallCommandAttemptedForHold = true;
                _formationCommandArmedForRelease = false;
                RecallHost(Hero.Current);
            }
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
            FormationPatrolAnchors.Clear();
            _bulwarkTargetCandidateExpiresAt = 0.0f;
            _bulwarkTargetCandidates = new NpcElement[0];
            _guardIdleMoverId = null;
            _nextIdleHostAttemptAt = 0.0f;
            _hasBulwarkForward = false;
            _bulwarkForward = Vector3.forward;
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
            _formationCommandArmedForRelease = false;
            _recallCommandAttemptedForHold = false;
            _takeAllItemsPressedAt = 0.0f;
        }

        private static void ResetBehaviorCommandHold()
        {
            _behaviorCommandHeld = false;
            _behaviorCommandAttemptedForHold = false;
            _behaviorCommandPressedAt = 0.0f;
        }

        internal static bool ShouldOwnTakeAllHoldForInterop()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || plugin.FormationCommands == null
                || !plugin.FormationCommands.Value
                || !HasGlobalFormationControl()
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || Time.timeScale <= 0.0f)
            {
                return false;
            }

            return World.All<NpcHeroSummon>()
                .Any(summon => IsOwnedSummon(summon, hero));
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
            HeroSummonTargetOverride explicitTarget =
                summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
            if (explicitTarget != null
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
            __instance.ParentModel.OnCompletelyInitialized(
                delegate
                {
                    ApplyPlayerPassThrough(__instance);
                });
            SoulSalvageRuntime.OnSummonInitialized(__instance);

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
            ApplyPlayerPassThrough(__instance);
        }

        private static void ApplyPlayerPassThrough(NpcHeroSummon summon)
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
            List<CollisionPair> pairs;
            if (!CollisionPairs.TryGetValue(id, out pairs))
            {
                pairs = new List<CollisionPair>();
                CollisionPairs[id] = pairs;
            }

            Collider heroCollider = hero.VHeroController.Controller;
            foreach (Collider collider in summon.ParentModel.Controller.AlivePrefab
                .GetComponentsInChildren<Collider>(true))
            {
                if (collider == null
                    || collider.isTrigger
                    || ReferenceEquals(collider, heroCollider)
                    || pairs.Any(pair => pair.SummonCollider == collider
                        && pair.HeroCollider == heroCollider))
                {
                    continue;
                }
                Physics.IgnoreCollision(heroCollider, collider, true);
                pairs.Add(new CollisionPair
                {
                    HeroCollider = heroCollider,
                    SummonCollider = collider
                });
            }
        }

        private static void RestoreCollisionPairs(string id)
        {
            List<CollisionPair> pairs;
            if (!CollisionPairs.TryGetValue(id, out pairs))
            {
                return;
            }
            foreach (CollisionPair pair in pairs)
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
                && plugin.PersistentServants.Value)
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
            if (__instance == null || __instance.Npc == null || !__instance.Npc.IsHeroSummon)
            {
                return;
            }
            string summonId = GetSummonId(__instance.Npc);
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
            UpdateCatchUpSpeed(__instance, plugin);
            UpdateEmpoweredPresentation(__instance);
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
            SoulAndServicePlugin plugin)
        {
            NpcElement npc = controller.Npc;
            string id = GetSummonId(npc);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            bool shouldBoost = false;
            float multiplier = 1.0f;
            Hero hero = Hero.Current;
            if (plugin != null
                && plugin.IsEnabled
                && hero != null
                && npc.NpcAI != null
                && !npc.NpcAI.InCombat
                && npc.GetCurrentTarget() == null)
            {
                float threshold = plugin.TrotDistance.Value;
                shouldBoost = (hero.Coords - npc.Coords).sqrMagnitude
                    > threshold * threshold;
                multiplier = plugin.CatchUpSpeedMultiplier.Value;
            }

            if (!shouldBoost || multiplier <= 1.0f)
            {
                RemoveSpeedTweak(id);
                return;
            }

            if (!SpeedTweaks.ContainsKey(id)
                && npc.CharacterStats != null
                && npc.CharacterStats.MovementSpeedMultiplier != null)
            {
                StatTweak tweak = StatTweak.Multi(
                    npc.CharacterStats.MovementSpeedMultiplier,
                    multiplier,
                    null,
                    npc);
                ((Model)tweak).MarkedNotSaved = true;
                SpeedTweaks[id] = tweak;
            }
        }

        private static void UpdateEmpoweredPresentation(NpcController controller)
        {
            NpcElement npc = controller.Npc;
            string id = GetSummonId(npc);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
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
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (SpeedTweaks.ContainsKey(id)
                && plugin != null
                && plugin.CatchUpSpeedMultiplier != null)
            {
                playback *= plugin.CatchUpSpeedMultiplier.Value;
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

        private static void AfterSummonDiscard(NpcHeroSummon __instance)
        {
            if (__instance == null)
            {
                return;
            }
            string id = ((Model)__instance).ID;
            StabilizedPatrols.Remove(id);
            FormationPatrolAnchors.Remove(id);
            ExplicitCommandTargets.Remove(id);
            AutonomousTargetOverrides.Remove(id);
            HeldSummons.Remove(id);
            ClearIdleMovementState(id);
            FormationCommandViewCaches.Remove(id);
            NextControllerRefreshBySummon.Remove(id);
            LastControlDiagnosticBySummon.Remove(id);
            NextControlDiagnosticBySummon.Remove(id);
            PendingTeleportVfxBySummon.Remove(id);
            PendingRecallPlacements.Remove(id);
            RecallTargetSuppressionUntil.Remove(id);
            RestoreCollisionPairs(id);
            RemoveSpeedTweak(id);
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
            SoulSalvageRuntime.OnSummonDiscarded(__instance);
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
                EmpowermentState receiverEmpowerment;
                if (EmpowermentStates.TryGetValue(
                        GetSummonId(receiver),
                        out receiverEmpowerment))
                {
                    dmgModifier /= receiverEmpowerment.Multiplier;
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
                EmpowermentState dealerEmpowerment;
                if (EmpowermentStates.TryGetValue(
                        GetSummonId(dealer),
                        out dealerEmpowerment))
                {
                    dmgModifier *= dealerEmpowerment.Multiplier;
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
            float awarenessRange = behavior == SummonBehavior.Bulwark
                ? BulwarkDefenseRange
                : BaseSummonAwarenessRange
                    * Math.Max(1.0f, transferredSight);
            if (held)
            {
                awarenessRange = Math.Min(
                    awarenessRange,
                    HeldSummonCombatLeash);
            }
            float retentionRange = behavior == SummonBehavior.Bulwark
                ? BulwarkCombatLeash
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
                    : owner.Coords;
            bool hostInCombat = IsHostInCombat(hero);
            NpcElement bestTarget = null;
            int bestPriority = int.MaxValue;
            float bestDistanceSqr = float.PositiveInfinity;
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
                    || !WithFactionUtils.WantToFight(owner, target)
                    || !HasAutonomousTargetLineOfSight(
                        summon,
                        hero,
                        target,
                        behavior))
                {
                    continue;
                }
                int priority = GetAutonomousTargetPriority(
                    target,
                    behavior,
                    hero,
                    hostInCombat);
                if (priority == int.MaxValue)
                {
                    continue;
                }
                float distanceSqr =
                    (target.Coords - awarenessCenter).sqrMagnitude;
                if (ReferenceEquals(target, committedTarget))
                {
                    committedTargetVisible = true;
                    committedPriority = priority;
                    committedDistanceSqr = distanceSqr;
                }
                if (priority < bestPriority
                    || (priority == bestPriority
                        && distanceSqr < bestDistanceSqr))
                {
                    bestTarget = target;
                    bestPriority = priority;
                    bestDistanceSqr = distanceSqr;
                }
            }

            if (committedRecord != null
                && committedTargetVisible
                && !ReferenceEquals(bestTarget, committedTarget)
                && (bestTarget == null
                    || bestPriority > committedPriority
                    || (bestPriority == committedPriority
                        && (now - committedRecord.SelectedAt
                                < AutonomousTargetMinimumCommitmentSeconds
                            || bestDistanceSqr
                                >= committedDistanceSqr
                                    * AutonomousTargetSwitchDistanceRatio
                                    * AutonomousTargetSwitchDistanceRatio))))
            {
                bestTarget = committedTarget;
                bestPriority = committedPriority;
                bestDistanceSqr = committedDistanceSqr;
            }

            NpcElement retainedTarget = null;
            AwarenessTargetRecord record;
            if (AwarenessTargets.TryGetValue(summonId, out record))
            {
                bool invalid = record.Target == null
                    || record.Target.HasBeenDiscarded
                    || !record.Target.IsAlive
                    || record.Target.IsUnconscious
                    || !WithFactionUtils.WantToFight(owner, record.Target)
                    || GetAutonomousTargetPriority(
                        record.Target,
                        behavior,
                        hero,
                        hostInCombat) == int.MaxValue;
                float distanceSqr = invalid
                    ? float.PositiveInfinity
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
            SetAutonomousTargetOverride(summon, selectedTarget);
            return true;
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
                _bulwarkTargetCandidates = grid.GetNpcsInSphere(
                        awarenessCenter,
                        awarenessRange)
                    .ToArray();
            }
            return _bulwarkTargetCandidates;
        }

        private static int GetAutonomousTargetPriority(
            NpcElement target,
            SummonBehavior behavior,
            Hero hero,
            bool hostInCombat)
        {
            if (target == null)
            {
                return int.MaxValue;
            }
            if (behavior == SummonBehavior.Hunt)
            {
                return 0;
            }
            RecentAttackerRecord attacker;
            bool recentAttacker = RecentAttackers.TryGetValue(
                    ((Model)target).ID,
                    out attacker)
                && attacker.ExpiresAt >= Time.unscaledTime;
            bool targetingProtected = IsHeroOrOwnedSummon(
                target.GetCurrentTarget());
            float heroDistanceSqr = hero == null
                ? float.PositiveInfinity
                : (target.Coords - hero.Coords).sqrMagnitude;
            if (behavior == SummonBehavior.Bulwark)
            {
                if (heroDistanceSqr
                    > BulwarkDefenseRange * BulwarkDefenseRange)
                {
                    return int.MaxValue;
                }
                if (recentAttacker)
                {
                    return 0;
                }
                return targetingProtected ? 1 : int.MaxValue;
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
            return int.MaxValue;
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
                _hostCombatCacheValue = GetFormationHost(hero).Any(
                    summon => IsOwnedSummon(summon, hero)
                        && summon.ParentModel != null
                        && summon.ParentModel.NpcAI != null
                        && summon.ParentModel.NpcAI.InCombat);
            }
            return _hostCombatCacheValue;
        }

        private static bool HasAutonomousTargetLineOfSight(
            NpcHeroSummon summon,
            Hero hero,
            NpcElement target,
            SummonBehavior behavior)
        {
            if (hero == null || target == null)
            {
                return false;
            }
            NpcElement summonObserver = summon == null
                ? null
                : summon.ParentModel;
            if (behavior == SummonBehavior.Hunt)
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
            string targetId = ((Model)target).ID;
            string cacheKey = observer == null
                ? "host|" + targetId
                : ((Model)observer).ID + "|" + targetId;
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
                AutonomousLineOfSightByTarget[cacheKey] =
                    new AutonomousLineOfSightRecord
                    {
                        Origin = origin,
                        FocusPoint = focusPoint,
                        Visible = true,
                        ExpiresAt = Time.unscaledTime
                            + AutonomousLineOfSightCacheSeconds
                    };
                return true;
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
            AutonomousLineOfSightByTarget[cacheKey] =
                new AutonomousLineOfSightRecord
                {
                    Origin = origin,
                    FocusPoint = focusPoint,
                    Visible = visible,
                    ExpiresAt = Time.unscaledTime
                        + AutonomousLineOfSightCacheSeconds
                };
            return visible;
        }

        private static void SetAutonomousTargetOverride(
            NpcHeroSummon summon,
            NpcElement target)
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
            HeroSummonTargetOverride current =
                summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
            if (AutonomousTargetOverrides.TryGetValue(
                    summonId,
                    out trackedTarget)
                && ReferenceEquals(trackedTarget, target)
                && current != null
                && ReferenceEquals(current.Target, target))
            {
                return;
            }
            ClearAutonomousTargetOverride(summon);
            AutonomousTargetOverrides[summonId] = target;
            HeroSummonTargetOverride.AddSummonTargetOverrideElement(
                summon,
                target,
                5);
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
                return;
            }
            AutonomousTargetOverrides.Remove(summonId);
            HeroSummonTargetOverride current =
                summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
            if (!ExplicitCommandTargets.ContainsKey(summonId)
                && current != null
                && ReferenceEquals(current.Target, trackedTarget))
            {
                current.Discard();
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
            foreach (string key in RecentAttackers.Keys.ToArray())
            {
                RecentAttackerRecord record = RecentAttackers[key];
                if (record == null
                    || record.Target == null
                    || record.Target.HasBeenDiscarded
                    || !record.Target.IsAlive
                    || record.ExpiresAt < now)
                {
                    RecentAttackers.Remove(key);
                }
            }
            foreach (string key in PendingTeleportVfxBySummon.Keys.ToArray())
            {
                PendingTeleportVfx pending = PendingTeleportVfxBySummon[key];
                if (pending == null || now - pending.RequestedAt > 10.0f)
                {
                    PendingTeleportVfxBySummon.Remove(key);
                }
            }
            foreach (string key in PendingRecallPlacements.Keys.ToArray())
            {
                PendingRecallPlacement placement =
                    PendingRecallPlacements[key];
                if (placement == null
                    || (!placement.DestinationConsumed
                        && placement.ExpiresAt < now)
                    || (placement.DestinationConsumed
                        && (hero == null
                            || (hero.Coords - placement.HeroOrigin).sqrMagnitude
                                > RecallPlacementHeroMoveReleaseDistance
                                    * RecallPlacementHeroMoveReleaseDistance)))
                {
                    PendingRecallPlacements.Remove(key);
                }
            }
            foreach (string key in RecallTargetSuppressionUntil.Keys.ToArray())
            {
                if (RecallTargetSuppressionUntil[key] < now)
                {
                    RecallTargetSuppressionUntil.Remove(key);
                }
            }
            foreach (string key in AutonomousLineOfSightByTarget.Keys.ToArray())
            {
                AutonomousLineOfSightRecord cached =
                    AutonomousLineOfSightByTarget[key];
                if (cached == null || cached.ExpiresAt < now)
                {
                    AutonomousLineOfSightByTarget.Remove(key);
                }
            }
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
                || (HasAutonomousTargetLineOfSight(
                        summon,
                        Hero.Current,
                        npcTarget,
                        behavior)
                    && GetAutonomousTargetPriority(
                        npcTarget,
                        behavior,
                        Hero.Current,
                        IsHostInCombat(Hero.Current))
                        != int.MaxValue))
            {
                return;
            }
            HeroSummonTargetOverride targetOverride =
                summon.ParentModel.TryGetElement<HeroSummonTargetOverride>();
            if (targetOverride != null)
            {
                targetOverride.Discard();
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
            NpcElement preservedTarget = null)
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
            ClearAutonomousTargetOverride(summon);
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

        private static void RemoveSpeedTweak(string id)
        {
            StatTweak tweak;
            if (SpeedTweaks.TryGetValue(id, out tweak))
            {
                DiscardTweak(tweak);
                SpeedTweaks.Remove(id);
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
