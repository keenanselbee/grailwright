using System;
using System.Collections.Generic;
using Awaken.TG.MVC;
using Awaken.TG.Main.AI.SummonsAndAllies;
using Awaken.TG.Main.Heroes;
using UnityEngine;

namespace SoulAndService
{
    internal enum FormationPurpose
    {
        None = 0,
        Guard = 1,
        Hunt = 2,
        BulwarkCloseGuard = 3,
        BulwarkAdvance = 4,
        Recall = 5,
        HuntAttackMove = 6
    }

    internal static class SummonFormationCoordinator
    {
        private const float DefaultNavigationRadius = 0.5f;
        private const float MinimumNavigationRadius = 0.25f;
        private const float MaximumNavigationRadius = 4.0f;
        private const float PhysicalClearance = 0.35f;
        private const float MinimumReservedSpacing = 1.5f;
        private const float ProgressDistance = 0.10f;
        private const int MaximumRecoveryProbeHostsPerFrame = 2;
        private const float RecallInnerRadius = 3.5f;
        private const float RecallRingSpacing = 2.25f;
        private const float RecallArcStartDegrees = 75.0f;
        private const float RecallArcDegrees = 210.0f;
        private const int RecallSlotsPerRing = 7;
        private const int RecallPlacementAttempts = 12;
        private const float RecallMaximumSnapDistance = 2.0f;
        private static readonly int[] RecallSlotOrder =
        {
            3, 2, 4, 1, 5, 0, 6
        };

        internal sealed class MemberState
        {
            internal string SummonId;
            internal NpcHeroSummon Summon;
            internal FormationPurpose Purpose;
            internal int StableSlotId;
            internal int SeenGeneration;
            internal float NavigationRadius;
            internal Vector3 DesiredAnchor;
            internal Vector3 ResolvedAnchor;
            internal Vector3 LastProgressPosition;
            internal Vector3 LastAppliedAnchor;
            internal float LastDistance;
            internal float LastProgressAt;
            internal float FallbackUntil;
            internal bool HasAnchor;
            internal bool HasAppliedAnchor;
            internal bool ReportedFallback;
            internal bool ArrivalEligible;
            internal bool Satisfied;
            internal bool Suspended;
        }

        private sealed class LargestFirstComparer : IComparer<NpcHeroSummon>
        {
            internal static readonly LargestFirstComparer Instance =
                new LargestFirstComparer();

            public int Compare(NpcHeroSummon left, NpcHeroSummon right)
            {
                float leftRadius = ReadNavigationRadius(left);
                float rightRadius = ReadNavigationRadius(right);
                int radiusComparison = rightRadius.CompareTo(leftRadius);
                if (radiusComparison != 0)
                {
                    return radiusComparison;
                }
                string leftId = left == null ? string.Empty : ((Model)left).ID;
                string rightId = right == null ? string.Empty : ((Model)right).ID;
                return StringComparer.Ordinal.Compare(leftId, rightId);
            }
        }

        private static readonly Dictionary<string, MemberState> Members =
            new Dictionary<string, MemberState>();
        private static readonly List<string> RemovalBuffer =
            new List<string>();
        private static readonly List<NpcHeroSummon> NewMemberBuffer =
            new List<NpcHeroSummon>();
        private static readonly Vector3[] RecoveryCandidateBuffer =
            new Vector3[6];

        private static Hero _hero;
        private static int _hostGeneration;
        private static int _recoveryProbeFrame = -1;
        private static int _recoveryProbeHostsThisFrame;

        internal static int MemberCount => Members.Count;

        internal static void Synchronize(
            Hero hero,
            NpcHeroSummon[] host)
        {
            if (!ReferenceEquals(_hero, hero))
            {
                Reset();
                _hero = hero;
            }

            unchecked
            {
                _hostGeneration++;
            }
            if (_hostGeneration == 0)
            {
                _hostGeneration = 1;
            }

            NewMemberBuffer.Clear();
            if (host != null)
            {
                for (int index = 0; index < host.Length; index++)
                {
                    NpcHeroSummon summon = host[index];
                    if (!IsUsable(summon))
                    {
                        continue;
                    }
                    string summonId = ((Model)summon).ID;
                    MemberState state;
                    if (!Members.TryGetValue(summonId, out state))
                    {
                        NewMemberBuffer.Add(summon);
                        continue;
                    }
                    state.Summon = summon;
                    state.SeenGeneration = _hostGeneration;
                    state.NavigationRadius = ReadNavigationRadius(summon);
                }
            }

            NewMemberBuffer.Sort(LargestFirstComparer.Instance);
            for (int index = 0; index < NewMemberBuffer.Count; index++)
            {
                NpcHeroSummon summon = NewMemberBuffer[index];
                string summonId = ((Model)summon).ID;
                Members[summonId] = new MemberState
                {
                    SummonId = summonId,
                    Summon = summon,
                    StableSlotId = FindNextStableSlotId(),
                    SeenGeneration = _hostGeneration,
                    NavigationRadius = ReadNavigationRadius(summon)
                };
            }
            NewMemberBuffer.Clear();

            RemovalBuffer.Clear();
            foreach (KeyValuePair<string, MemberState> pair in Members)
            {
                if (pair.Value.SeenGeneration != _hostGeneration)
                {
                    RemovalBuffer.Add(pair.Key);
                }
            }
            for (int index = 0; index < RemovalBuffer.Count; index++)
            {
                Members.Remove(RemovalBuffer[index]);
            }
            RemovalBuffer.Clear();
        }

        internal static Vector3 GetRadialAnchor(
            NpcHeroSummon summon,
            FormationPurpose purpose,
            Vector3 origin,
            Vector3 forward,
            float innerRadius,
            float ringSpacing,
            int slotsPerRing)
        {
            MemberState state = GetOrCreateState(summon);
            if (state == null || slotsPerRing <= 0)
            {
                return origin;
            }
            SetPurpose(state, purpose);

            forward.y = 0.0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            int activeSlot = GetDenseSlotRank(state, false);
            int ring = activeSlot / slotsPerRing;
            int slot = activeSlot % slotsPerRing;
            float requiredSpacing = GetRequiredSpacing(state, false);
            float angleStep = GetSmallestAngleStep(purpose, slotsPerRing);
            float requiredRadius = angleStep <= 0.01f
                ? innerRadius
                : requiredSpacing
                    / (2.0f * Mathf.Sin(angleStep * 0.5f * Mathf.Deg2Rad));
            float distance = Math.Max(innerRadius, requiredRadius)
                + ring * Math.Max(ringSpacing, requiredSpacing);
            float angle = GetSlotAngle(purpose, slot, slotsPerRing);
            Vector3 anchor = origin
                + Quaternion.AngleAxis(angle, Vector3.up) * forward * distance;
            state.DesiredAnchor = anchor;
            return anchor;
        }

        internal static Vector3 ResolveAnchor(
            NpcHeroSummon summon,
            FormationPurpose purpose,
            Vector3 desiredAnchor,
            float anchorTolerance,
            float blockedSeconds,
            float fallbackSeconds,
            float probeDistance,
            float maximumSnapDistance,
            bool latchArrival,
            float resumeDistance)
        {
            MemberState state = GetOrCreateState(summon);
            if (state == null || !IsUsable(summon))
            {
                return desiredAnchor;
            }
            SetPurpose(state, purpose);
            state.Suspended = false;

            float now = Time.unscaledTime;
            Vector3 position = summon.ParentModel.Coords;
            float desiredDistance = Vector3.Distance(position, desiredAnchor);
            if (!state.HasAnchor)
            {
                InitializeAnchorState(state, position, desiredAnchor, now);
                return desiredAnchor;
            }

            if (latchArrival && state.Satisfied)
            {
                if (desiredDistance <= resumeDistance)
                {
                    return state.ResolvedAnchor;
                }
                state.Satisfied = false;
                InitializeAnchorState(state, position, desiredAnchor, now);
            }

            state.DesiredAnchor = desiredAnchor;
            if (state.FallbackUntil > now)
            {
                return state.ResolvedAnchor;
            }
            if (state.FallbackUntil > 0.0f)
            {
                InitializeAnchorState(state, position, desiredAnchor, now);
            }
            else
            {
                state.ResolvedAnchor = desiredAnchor;
            }

            float distance = Vector3.Distance(position, state.ResolvedAnchor);
            float settleDistance = Math.Max(
                anchorTolerance,
                Math.Max(ProgressDistance, state.NavigationRadius));
            if (distance <= settleDistance)
            {
                state.Satisfied = latchArrival;
                RecordProgress(state, position, distance, now);
                return state.ResolvedAnchor;
            }

            Vector3 movement = position - state.LastProgressPosition;
            Vector3 previousDirection = state.ResolvedAnchor
                - state.LastProgressPosition;
            float directionalProgress = previousDirection.sqrMagnitude
                    <= 0.0001f
                ? 0.0f
                : Vector3.Dot(
                    movement,
                    previousDirection.normalized);
            bool progressed = directionalProgress >= ProgressDistance
                || state.LastDistance - distance >= ProgressDistance;
            if (progressed)
            {
                RecordProgress(state, position, distance, now);
                return state.ResolvedAnchor;
            }
            if (now - state.LastProgressAt < blockedSeconds
                || !TryConsumeRecoveryProbe())
            {
                return state.ResolvedAnchor;
            }

            Vector3 fallbackAnchor;
            bool resolvedFallback = TryResolveFallbackAnchor(
                    state,
                    position,
                    desiredAnchor,
                    probeDistance,
                    maximumSnapDistance,
                    out fallbackAnchor);
            if (!resolvedFallback)
            {
                fallbackAnchor = position;
            }
            state.ResolvedAnchor = fallbackAnchor;
            state.ArrivalEligible = resolvedFallback;
            state.LastProgressPosition = position;
            state.LastDistance = Vector3.Distance(position, fallbackAnchor);
            state.LastProgressAt = now;
            state.FallbackUntil = now + fallbackSeconds;
            if (!state.ReportedFallback)
            {
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogDiagnostic(resolvedFallback
                        ? "Formation coordinator accepted a reachable temporary "
                            + purpose.ToString() + " slot for " + state.SummonId
                            + " after " + blockedSeconds.ToString("0.##")
                            + " seconds without progress."
                        : "Formation coordinator paused " + state.SummonId
                            + " after no reachable temporary "
                            + purpose.ToString() + " slot was available.");
                }
                state.ReportedFallback = true;
            }
            return fallbackAnchor;
        }

        internal static bool ShouldApplyPatrolAnchor(
            NpcHeroSummon summon,
            Vector3 anchor,
            float updateDistance)
        {
            MemberState state = GetOrCreateState(summon);
            if (state == null)
            {
                return true;
            }
            if (state.HasAppliedAnchor
                && (state.LastAppliedAnchor - anchor).sqrMagnitude
                    <= updateDistance * updateDistance)
            {
                return false;
            }
            state.LastAppliedAnchor = anchor;
            state.HasAppliedAnchor = true;
            return true;
        }

        internal static bool TryReserveRecallPlacement(
            NpcHeroSummon summon,
            Hero hero,
            Pathfinding.GraphNode heroNode,
            float rotation,
            out Vector3 placement)
        {
            placement = Vector3.zero;
            MemberState state = GetOrCreateState(summon);
            if (state == null
                || hero == null
                || heroNode == null
                || AstarPath.active == null)
            {
                return false;
            }
            SetPurpose(state, FormationPurpose.Recall);
            int activeSlot = GetDenseSlotRank(state, true);
            float requiredSpacing = GetRequiredSpacing(state, true);
            if (!TryResolveRecallPlacement(
                    hero,
                    heroNode,
                    activeSlot,
                    rotation,
                    requiredSpacing,
                    candidate => IsReservedOrOccupied(state, candidate),
                    out placement))
            {
                return false;
            }
            InitializeAnchorState(
                state,
                summon.ParentModel.Coords,
                placement,
                Time.unscaledTime);
            return true;
        }

        internal static bool TryReserveRestoredPlacement(
            Hero hero,
            Pathfinding.GraphNode heroNode,
            int slotIndex,
            ICollection<Vector3> reservedPlacements,
            out Vector3 placement)
        {
            placement = Vector3.zero;
            if (reservedPlacements == null
                || !TryResolveRecallPlacement(
                    hero,
                    heroNode,
                    slotIndex,
                    0.0f,
                    MinimumReservedSpacing,
                    candidate => IsRestoredPlacementOccupied(
                        candidate,
                        reservedPlacements),
                    out placement))
            {
                return false;
            }
            reservedPlacements.Add(placement);
            return true;
        }

        private static bool TryResolveRecallPlacement(
            Hero hero,
            Pathfinding.GraphNode heroNode,
            int activeSlot,
            float rotation,
            float requiredSpacing,
            Func<Vector3, bool> isOccupied,
            out Vector3 placement)
        {
            placement = Vector3.zero;
            if (hero == null
                || heroNode == null
                || activeSlot < 0
                || AstarPath.active == null)
            {
                return false;
            }
            Vector3 forward = hero.VHeroController == null
                ? Vector3.forward
                : hero.VHeroController.transform.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            int ring = activeSlot / RecallSlotsPerRing;
            int slot = activeSlot % RecallSlotsPerRing;
            int orderedSlot = RecallSlotOrder[slot];
            float cellDegrees = RecallArcDegrees / RecallSlotsPerRing;
            float baseAngle = RecallArcStartDegrees
                + (orderedSlot + 0.5f) * cellDegrees + rotation;
            float requiredRadius = requiredSpacing
                / (2.0f * Mathf.Sin(cellDegrees * 0.5f * Mathf.Deg2Rad));
            float baseRadius = Math.Max(RecallInnerRadius, requiredRadius)
                + ring * Math.Max(RecallRingSpacing, requiredSpacing);

            for (int attempt = 0; attempt < RecallPlacementAttempts; attempt++)
            {
                int offsetStep = (attempt + 1) / 2;
                float angleOffset = attempt == 0
                    ? 0.0f
                    : (attempt % 2 == 1 ? 1.0f : -1.0f)
                        * 18.0f * offsetStep;
                float radius = baseRadius + (attempt / 6) * 1.25f;
                Vector3 candidate = hero.Coords
                    + Quaternion.AngleAxis(
                        baseAngle + angleOffset,
                        Vector3.up) * forward * radius;
                Pathfinding.NNInfo nearest = AstarPath.active.GetNearest(
                    candidate,
                    Pathfinding.NNConstraint.Walkable);
                if (nearest.node == null
                    || !Pathfinding.PathUtilities.IsPathPossible(
                        heroNode,
                        nearest.node)
                    || (nearest.position - candidate).sqrMagnitude
                        > RecallMaximumSnapDistance
                            * RecallMaximumSnapDistance
                    || Math.Abs(nearest.position.y - hero.Coords.y) > 4.0f
                    || (nearest.position - hero.Coords).sqrMagnitude < 4.0f
                    || (isOccupied != null && isOccupied(nearest.position)))
                {
                    continue;
                }
                placement = nearest.position;
                return true;
            }
            return false;
        }

        private static bool IsRestoredPlacementOccupied(
            Vector3 candidate,
            IEnumerable<Vector3> reservedPlacements)
        {
            float spacingSqr = MinimumReservedSpacing
                * MinimumReservedSpacing;
            foreach (Vector3 reserved in reservedPlacements)
            {
                if ((reserved - candidate).sqrMagnitude < spacingSqr)
                {
                    return true;
                }
            }
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                if (summon != null
                    && !summon.HasBeenDiscarded
                    && summon.ParentModel != null
                    && !summon.ParentModel.HasBeenDiscarded
                    && (summon.ParentModel.Coords - candidate).sqrMagnitude
                        < spacingSqr)
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsAtResolvedAnchor(
            NpcHeroSummon summon,
            FormationPurpose purpose,
            float tolerance)
        {
            MemberState state;
            return TryGetState(summon, out state)
                && state.Purpose == purpose
                && state.HasAnchor
                && state.ArrivalEligible
                && IsUsable(summon)
                && (summon.ParentModel.Coords - state.ResolvedAnchor).sqrMagnitude
                    <= tolerance * tolerance;
        }

        internal static void Suspend(string summonId)
        {
            MemberState state;
            if (string.IsNullOrEmpty(summonId)
                || !Members.TryGetValue(summonId, out state))
            {
                return;
            }
            state.Purpose = FormationPurpose.None;
            state.Suspended = true;
            ResetNavigationState(state);
            state.HasAppliedAnchor = false;
        }

        internal static void Remove(string summonId)
        {
            if (!string.IsNullOrEmpty(summonId))
            {
                Members.Remove(summonId);
            }
        }

        internal static void InvalidateAppliedAnchor(string summonId)
        {
            MemberState state;
            if (!string.IsNullOrEmpty(summonId)
                && Members.TryGetValue(summonId, out state))
            {
                state.HasAppliedAnchor = false;
            }
        }

        internal static void Reset()
        {
            Members.Clear();
            RemovalBuffer.Clear();
            NewMemberBuffer.Clear();
            _hero = null;
            _hostGeneration = 0;
            _recoveryProbeFrame = -1;
            _recoveryProbeHostsThisFrame = 0;
        }

        private static MemberState GetOrCreateState(NpcHeroSummon summon)
        {
            if (!IsUsable(summon))
            {
                return null;
            }
            string summonId = ((Model)summon).ID;
            MemberState state;
            if (Members.TryGetValue(summonId, out state))
            {
                state.Summon = summon;
                state.NavigationRadius = ReadNavigationRadius(summon);
                return state;
            }
            state = new MemberState
            {
                SummonId = summonId,
                Summon = summon,
                StableSlotId = FindNextStableSlotId(),
                SeenGeneration = _hostGeneration,
                NavigationRadius = ReadNavigationRadius(summon)
            };
            Members[summonId] = state;
            return state;
        }

        private static bool TryGetState(
            NpcHeroSummon summon,
            out MemberState state)
        {
            state = null;
            return summon != null
                && Members.TryGetValue(((Model)summon).ID, out state);
        }

        private static int FindNextStableSlotId()
        {
            int highestSlot = -1;
            foreach (MemberState state in Members.Values)
            {
                if (state.StableSlotId > highestSlot)
                {
                    highestSlot = state.StableSlotId;
                }
            }
            return highestSlot == int.MaxValue
                ? Members.Count
                : highestSlot + 1;
        }

        private static int GetDenseSlotRank(
            MemberState member,
            bool includeSuspended)
        {
            int rank = 0;
            foreach (MemberState other in Members.Values)
            {
                if (!ReferenceEquals(other, member)
                    && IsUsable(other.Summon)
                    && (includeSuspended || !other.Suspended)
                    && other.StableSlotId < member.StableSlotId)
                {
                    rank++;
                }
            }
            return rank;
        }

        private static void SetPurpose(
            MemberState state,
            FormationPurpose purpose)
        {
            if (state.Purpose == purpose)
            {
                return;
            }
            state.Purpose = purpose;
            state.Suspended = false;
            ResetNavigationState(state);
            state.HasAppliedAnchor = false;
        }

        private static void ResetNavigationState(MemberState state)
        {
            state.DesiredAnchor = Vector3.zero;
            state.ResolvedAnchor = Vector3.zero;
            state.LastProgressPosition = Vector3.zero;
            state.LastDistance = 0.0f;
            state.LastProgressAt = 0.0f;
            state.FallbackUntil = 0.0f;
            state.HasAnchor = false;
            state.ReportedFallback = false;
            state.ArrivalEligible = false;
            state.Satisfied = false;
        }

        private static void InitializeAnchorState(
            MemberState state,
            Vector3 position,
            Vector3 desiredAnchor,
            float now)
        {
            state.DesiredAnchor = desiredAnchor;
            state.ResolvedAnchor = desiredAnchor;
            state.LastProgressPosition = position;
            state.LastDistance = Vector3.Distance(position, desiredAnchor);
            state.LastProgressAt = now;
            state.FallbackUntil = 0.0f;
            state.HasAnchor = true;
            state.ArrivalEligible = true;
        }

        private static void RecordProgress(
            MemberState state,
            Vector3 position,
            float distance,
            float now)
        {
            state.LastProgressPosition = position;
            state.LastDistance = distance;
            state.LastProgressAt = now;
            state.ReportedFallback = false;
        }

        private static bool TryResolveFallbackAnchor(
            MemberState state,
            Vector3 summonPosition,
            Vector3 desiredAnchor,
            float probeDistance,
            float maximumSnapDistance,
            out Vector3 resolvedAnchor)
        {
            resolvedAnchor = desiredAnchor;
            if (AstarPath.active == null)
            {
                return false;
            }
            Pathfinding.GraphNode sourceNode = AstarPath.active.GetNearest(
                summonPosition,
                Pathfinding.NNConstraint.Walkable).node;
            if (sourceNode == null)
            {
                return false;
            }

            Vector3 approach = desiredAnchor - summonPosition;
            approach.y = 0.0f;
            if (approach.sqrMagnitude <= 0.001f)
            {
                approach = Vector3.forward;
            }
            else
            {
                approach.Normalize();
            }
            Vector3 side = Vector3.Cross(Vector3.up, approach).normalized;
            if ((state.StableSlotId & 1) != 0)
            {
                side = -side;
            }
            RecoveryCandidateBuffer[0] = desiredAnchor - approach * probeDistance;
            RecoveryCandidateBuffer[1] = desiredAnchor + side * probeDistance;
            RecoveryCandidateBuffer[2] = desiredAnchor - side * probeDistance;
            RecoveryCandidateBuffer[3] = desiredAnchor
                - approach * probeDistance + side * probeDistance;
            RecoveryCandidateBuffer[4] = desiredAnchor
                - approach * probeDistance - side * probeDistance;
            RecoveryCandidateBuffer[5] = desiredAnchor
                - approach * probeDistance * 2.0f;

            float bestOffsetSqr = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < RecoveryCandidateBuffer.Length; index++)
            {
                Vector3 candidate = RecoveryCandidateBuffer[index];
                Pathfinding.NNInfo nearest = AstarPath.active.GetNearest(
                    candidate,
                    Pathfinding.NNConstraint.Walkable);
                if (nearest.node == null
                    || !Pathfinding.PathUtilities.IsPathPossible(
                        sourceNode,
                        nearest.node)
                    || (nearest.position - candidate).sqrMagnitude
                        > maximumSnapDistance * maximumSnapDistance
                    || IsReservedOrOccupied(state, nearest.position))
                {
                    continue;
                }
                float offsetSqr =
                    (nearest.position - desiredAnchor).sqrMagnitude;
                if (offsetSqr >= bestOffsetSqr)
                {
                    continue;
                }
                bestOffsetSqr = offsetSqr;
                resolvedAnchor = nearest.position;
                found = true;
            }
            return found;
        }

        private static bool IsReservedOrOccupied(
            MemberState member,
            Vector3 candidate)
        {
            foreach (MemberState other in Members.Values)
            {
                if (ReferenceEquals(other, member)
                    || !IsUsable(other.Summon))
                {
                    continue;
                }
                float spacing = Math.Max(
                    MinimumReservedSpacing,
                    member.NavigationRadius + other.NavigationRadius
                        + PhysicalClearance);
                if ((other.Summon.ParentModel.Coords - candidate).sqrMagnitude
                        < spacing * spacing
                    || (other.HasAnchor
                        && (other.ResolvedAnchor - candidate).sqrMagnitude
                            < spacing * spacing))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryConsumeRecoveryProbe()
        {
            if (_recoveryProbeFrame != Time.frameCount)
            {
                _recoveryProbeFrame = Time.frameCount;
                _recoveryProbeHostsThisFrame = 0;
            }
            if (_recoveryProbeHostsThisFrame
                >= MaximumRecoveryProbeHostsPerFrame)
            {
                return false;
            }
            _recoveryProbeHostsThisFrame++;
            return true;
        }

        private static float GetRequiredSpacing(
            MemberState state,
            bool includeSuspended)
        {
            float maximumRadius = state.NavigationRadius;
            foreach (MemberState other in Members.Values)
            {
                if (IsUsable(other.Summon)
                    && (includeSuspended || !other.Suspended))
                {
                    maximumRadius = Math.Max(
                        maximumRadius,
                        other.NavigationRadius);
                }
            }
            return Math.Max(
                MinimumReservedSpacing,
                state.NavigationRadius + maximumRadius + PhysicalClearance);
        }

        private static float GetSmallestAngleStep(
            FormationPurpose purpose,
            int slotsPerRing)
        {
            if (slotsPerRing <= 1)
            {
                return 0.0f;
            }
            switch (purpose)
            {
                case FormationPurpose.Guard:
                case FormationPurpose.BulwarkCloseGuard:
                    return 150.0f / (slotsPerRing - 1.0f);
                case FormationPurpose.BulwarkAdvance:
                    return 30.0f;
                default:
                    return 360.0f / slotsPerRing;
            }
        }

        private static float GetSlotAngle(
            FormationPurpose purpose,
            int slot,
            int slotsPerRing)
        {
            if (slotsPerRing <= 1)
            {
                return purpose == FormationPurpose.BulwarkAdvance
                    ? 0.0f
                    : 180.0f;
            }
            if (purpose == FormationPurpose.Guard
                || purpose == FormationPurpose.BulwarkCloseGuard)
            {
                return GetCenterOutAngle(slot, slotsPerRing, 180.0f, 37.5f);
            }
            if (purpose == FormationPurpose.BulwarkAdvance)
            {
                return GetCenterOutAngle(slot, slotsPerRing, 0.0f, 30.0f);
            }
            float step = 360.0f / slotsPerRing;
            return GetCenterOutAngle(slot, slotsPerRing, 180.0f, step);
        }

        private static float GetCenterOutAngle(
            int slot,
            int slotsPerRing,
            float center,
            float step)
        {
            if (slotsPerRing % 2 == 0)
            {
                int pair = slot / 2;
                float offset = (pair + 0.5f) * step;
                return center + ((slot & 1) == 0 ? -offset : offset);
            }
            if (slot <= 0)
            {
                return center;
            }
            int magnitude = (slot + 1) / 2;
            float sign = (slot & 1) == 1 ? -1.0f : 1.0f;
            return center + sign * magnitude * step;
        }

        private static float ReadNavigationRadius(NpcHeroSummon summon)
        {
            if (!IsUsable(summon))
            {
                return DefaultNavigationRadius;
            }
            try
            {
                float radius = summon.ParentModel.Radius
                    * SummonRuntime.GetCombinedVisualSizeMultiplier(summon);
                if (!float.IsNaN(radius) && !float.IsInfinity(radius))
                {
                    return Mathf.Clamp(
                        radius,
                        MinimumNavigationRadius,
                        MaximumNavigationRadius);
                }
            }
            catch
            {
            }
            return DefaultNavigationRadius;
        }

        private static bool IsUsable(NpcHeroSummon summon)
        {
            return summon != null
                && !summon.HasBeenDiscarded
                && summon.ParentModel != null
                && !summon.ParentModel.HasBeenDiscarded;
        }
    }
}
