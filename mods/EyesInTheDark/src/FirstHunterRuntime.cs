using System;
using System.Collections.Generic;
using Awaken.TG.MVC;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.Factions.Markers;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Locations.Spawners;
using Awaken.TG.Main.Templates;
using Awaken.TG.Main.Wyrdnessing;
using BepInEx.Logging;
using UnityEngine;

namespace EyesInTheDark
{
    internal enum HunterRuntimeEventKind
    {
        None,
        PlacementConfirmed,
        PlacementFailed,
        HunterKilled,
        Escaped,
        LostTarget
    }

    internal struct HunterRuntimeEvent
    {
        public readonly HunterRuntimeEventKind Kind;
        public readonly string Reason;
        public readonly string LocationId;
        public readonly string ProfileId;

        public HunterRuntimeEvent(
            HunterRuntimeEventKind kind,
            string reason,
            string locationId,
            string profileId)
        {
            Kind = kind;
            Reason = reason ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
        }
    }

    internal sealed class FirstHunterRuntime
    {
        private const float MinimumVerifiedDistanceMeters = 20f;
        private const float MinimumMemberSeparationMeters = 7f;
        private const float InitializationTimeoutSeconds = 12f;
        private const int PositionAttemptsPerMember = 4;
        private const float ReacquisitionIntervalSeconds = 2f;
        private const float ReacquisitionDistanceMeters = 60f;
        private const int MaximumReacquisitionAttemptsPerMember = 3;

        private readonly ManualLogSource _log;
        private readonly System.Random _random;
        private readonly List<SpawnedMember> _members =
            new List<SpawnedMember>();

        private HuntEncounterPlan _plan;
        private HunterRuntimeEvent _pendingEvent;
        private float _initializationSeconds;
        private float _escapeSeconds;
        private float _reacquisitionSeconds;
        private bool _initializing;
        private bool _active;

        public bool IsInitializing
        {
            get { return _initializing; }
        }

        public bool IsActive
        {
            get { return _active; }
        }

        public string ActiveLocationId
        {
            get
            {
                return _members.Count == 0
                    ? "unknown"
                    : SafeLocationId(_members[0].Location);
            }
        }

        public string LastFailedProfileId { get; private set; }

        public void CopyLiveMembers(List<NpcElement> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            if (!_active)
            {
                return;
            }

            for (int index = 0; index < _members.Count; index++)
            {
                NpcElement npc = _members[index].Npc;
                if (npc != null
                    && !npc.HasBeenDiscarded
                    && npc.IsAlive
                    && !npc.IsDying)
                {
                    destination.Add(npc);
                }
            }
        }

        public FirstHunterRuntime(
            ManualLogSource log,
            int seed)
        {
            _log = log;
            _random = new System.Random(seed);
        }

        public bool TryStart(
            Hero hero,
            HuntEncounterPlan plan,
            float spawnDistanceMeters,
            out string reason)
        {
            LastFailedProfileId = string.Empty;
            if (_initializing || _active)
            {
                reason = "an official hunt already exists";
                return false;
            }
            if (plan == null || plan.Count == 0)
            {
                reason = "the encounter plan was empty";
                return false;
            }
            if (!TryValidateHero(hero, out reason))
            {
                return false;
            }

            WyrdnessService wyrdness = World.Services == null
                ? null
                : World.Services.TryGet<WyrdnessService>();
            if (wyrdness == null)
            {
                reason = "the native Wyrdness service is unavailable";
                return false;
            }

            _plan = plan;
            float distance = Mathf.Clamp(
                spawnDistanceMeters,
                MinimumVerifiedDistanceMeters,
                60f);
            float centerAngle = -150f
                + (float)_random.NextDouble() * 300f;

            try
            {
                for (int index = 0; index < plan.Members.Count; index++)
                {
                    HunterProfile profile = plan.Members[index];
                    LastFailedProfileId = profile.Id;
                    LocationTemplate template = ResolveTemplate(profile);
                    Vector3 verified;
                    float verifiedDistance;
                    if (!TryVerifyPosition(
                        hero,
                        wyrdness,
                        template,
                        distance,
                        centerAngle,
                        index,
                        plan.Count,
                        out verified,
                        out verifiedDistance,
                        out reason))
                    {
                        CleanupLiveLocations();
                        ClearReferences();
                        return false;
                    }

                    Vector3 facing = hero.Coords - verified;
                    facing.y = 0f;
                    Quaternion rotation = facing.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(facing)
                        : hero.Rotation;
                    Location location = template.SpawnLocation(
                        verified,
                        rotation);
                    if (location == null || location.HasBeenDiscarded)
                    {
                        reason = profile.Id
                            + " native placement returned no live Location";
                        CleanupLiveLocations();
                        ClearReferences();
                        return false;
                    }

                    location.MarkedNotSaved = true;
                    _members.Add(new SpawnedMember(
                        profile,
                        location,
                        verifiedDistance));
                }

                _initializing = true;
                _initializationSeconds = 0f;
                _escapeSeconds = 0f;
                _reacquisitionSeconds = 0f;
                for (int index = 0; index < _members.Count; index++)
                {
                    SpawnedMember member = _members[index];
                    ModelUtils.AfterFullyInitialized(
                        member.Location,
                        delegate { OnLocationInitialized(member); },
                        null);
                }

                if (!_initializing && !_active
                    && _pendingEvent.Kind
                        == HunterRuntimeEventKind.PlacementFailed)
                {
                    HunterRuntimeEvent failure = _pendingEvent;
                    _pendingEvent = new HunterRuntimeEvent();
                    LastFailedProfileId = failure.ProfileId;
                    reason = failure.Reason;
                    return false;
                }

                LastFailedProfileId = string.Empty;
                reason = plan.DescribeComposition()
                    + " native placement requested; waiting for "
                    + plan.Count
                    + " exact Location/Npc initialization(s)";
                return true;
            }
            catch (Exception exception)
            {
                reason = "native encounter placement failed for "
                    + (string.IsNullOrEmpty(LastFailedProfileId)
                        ? "unknown profile"
                        : LastFailedProfileId)
                    + ": "
                    + exception.GetBaseException().Message;
                CleanupLiveLocations();
                ClearReferences();
                return false;
            }
        }

        public void Tick(
            float activeSeconds,
            Hero hero,
            bool allowReacquisition,
            Func<NpcElement, NpcElement, bool> isAssistedEngagement,
            bool allowEscape,
            float escapeDistanceMeters,
            float escapeSustainSeconds)
        {
            float delta = FiniteNonNegative(activeSeconds);
            if (_initializing)
            {
                _initializationSeconds += delta;
                if (AnyLocationMissing())
                {
                    FailPlacement(
                        "a placed Location disappeared before encounter initialization",
                        FirstMissingProfileId());
                }
                else if (_initializationSeconds
                    >= InitializationTimeoutSeconds)
                {
                    FailPlacement(
                        "encounter Location/Npc initialization timed out",
                        FirstUninitializedProfileId());
                }
                return;
            }

            if (!_active || _members.Count == 0)
            {
                return;
            }

            SpawnedMember primary = _members[0];
            if (primary.Npc != null
                && !primary.Npc.HasBeenDiscarded
                && (!primary.Npc.IsAlive || primary.Npc.IsDying))
            {
                QueueEvent(
                    HunterRuntimeEventKind.HunterKilled,
                    "the exact official primary hunter died",
                    primary.Profile.Id);
                ReleaseReferences(false);
                return;
            }

            if (primary.Location == null
                || primary.Location.HasBeenDiscarded
                || primary.Npc == null
                || primary.Npc.HasBeenDiscarded)
            {
                QueueEvent(
                    HunterRuntimeEventKind.LostTarget,
                    "the exact official primary hunter Location was lost",
                    primary.Profile.Id);
                ReleaseReferences(false);
                return;
            }

            AdvanceReacquisition(
                delta,
                hero,
                allowReacquisition,
                isAssistedEngagement);

            if (!allowEscape
                || hero == null
                || hero.HasBeenDiscarded
                || delta <= 0f)
            {
                _escapeSeconds = 0f;
                return;
            }

            float distance = Vector3.Distance(
                hero.Coords,
                primary.Location.Coords);
            if (distance < Math.Max(
                MinimumVerifiedDistanceMeters,
                escapeDistanceMeters))
            {
                _escapeSeconds = 0f;
                return;
            }

            _escapeSeconds += delta;
            if (_escapeSeconds >= Math.Max(1f, escapeSustainSeconds))
            {
                QueueEvent(
                    HunterRuntimeEventKind.Escaped,
                    "hero remained "
                    + distance.ToString("0.0")
                    + "m away for "
                    + _escapeSeconds.ToString("0.0")
                    + " active seconds",
                    primary.Profile.Id);
                ReleaseReferences(true);
            }
        }

        private void AdvanceReacquisition(
            float activeSeconds,
            Hero hero,
            bool allowReacquisition,
            Func<NpcElement, NpcElement, bool> isAssistedEngagement)
        {
            if (!allowReacquisition
                || hero == null
                || hero.HasBeenDiscarded
                || activeSeconds <= 0f)
            {
                _reacquisitionSeconds = 0f;
                return;
            }

            _reacquisitionSeconds += activeSeconds;
            if (_reacquisitionSeconds < ReacquisitionIntervalSeconds)
            {
                return;
            }
            _reacquisitionSeconds = 0f;

            for (int index = 0; index < _members.Count; index++)
            {
                SpawnedMember member = _members[index];
                NpcElement currentNpcTarget = member.Npc == null
                    ? null
                    : member.Npc.GetCurrentTarget() as NpcElement;
                if (member.ReacquisitionAttempts
                        >= MaximumReacquisitionAttemptsPerMember
                    || member.Location == null
                    || member.Location.HasBeenDiscarded
                    || member.Npc == null
                    || member.Npc.HasBeenDiscarded
                    || member.Npc.NpcAI == null
                    || Vector3.Distance(
                        hero.Coords,
                        member.Location.Coords)
                        > ReacquisitionDistanceMeters
                    || (currentNpcTarget != null
                        && isAssistedEngagement != null
                        && isAssistedEngagement(
                            member.Npc,
                            currentNpcTarget))
                    || HasExactHeroTarget(member, hero))
                {
                    continue;
                }

                member.ReacquisitionAttempts++;
                try
                {
                    member.Npc.NpcAI.EnterCombatWith(hero, true);
                    _log.LogDebug(
                        "Reasserted native Hero combat for official hunter "
                        + member.Profile.Id
                        + "; attempt="
                        + member.ReacquisitionAttempts
                        + "/"
                        + MaximumReacquisitionAttemptsPerMember
                        + ".");
                }
                catch (Exception exception)
                {
                    _log.LogWarning(
                        "Official hunter reacquisition failed for "
                        + member.Profile.Id
                        + ": "
                        + exception.GetBaseException().Message);
                }
            }
        }

        public bool IsOfficialHunter(NpcElement npc)
        {
            return _active
                && _members.Count > 0
                && npc != null
                && ReferenceEquals(npc, _members[0].Npc);
        }

        public bool TryConsumeEvent(out HunterRuntimeEvent runtimeEvent)
        {
            runtimeEvent = _pendingEvent;
            if (runtimeEvent.Kind == HunterRuntimeEventKind.None)
            {
                return false;
            }

            _pendingEvent = new HunterRuntimeEvent();
            return true;
        }

        public void ConfirmOfficialKill()
        {
            if (!_active || _members.Count == 0)
            {
                return;
            }

            QueueEvent(
                HunterRuntimeEventKind.HunterKilled,
                "the hero killed the exact official primary hunter",
                _members[0].Profile.Id);
            ReleaseReferences(false);
        }

        public void Cancel(string reason, bool discardLiveTargets)
        {
            if (!_initializing && !_active)
            {
                return;
            }

            if (discardLiveTargets)
            {
                CleanupLiveLocations();
            }
            _log.LogInfo(
                "Official hunter runtime cancelled: "
                + (string.IsNullOrWhiteSpace(reason)
                    ? "unspecified"
                    : reason)
                + ".");
            ClearReferences();
            _pendingEvent = new HunterRuntimeEvent();
        }

        private void OnLocationInitialized(SpawnedMember member)
        {
            if (!_initializing
                || member.Location == null
                || member.Location.HasBeenDiscarded)
            {
                return;
            }

            NpcElement npc = member.Location.TryGetElement<NpcElement>();
            if (npc == null)
            {
                FailPlacement(
                    member.Profile.Id
                        + " initialized Location contained no NpcElement",
                    member.Profile.Id);
                return;
            }

            try
            {
                npc.OnCompletelyInitialized(
                    delegate(NpcElement initialized)
                    {
                        OnNpcInitialized(member, initialized);
                    });
            }
            catch (Exception exception)
            {
                FailPlacement(
                    member.Profile.Id
                        + " could not attach its Npc initialization callback: "
                        + exception.GetBaseException().Message,
                    member.Profile.Id);
            }
        }

        private void OnNpcInitialized(
            SpawnedMember member,
            NpcElement npc)
        {
            if (!_initializing
                || npc == null
                || npc.HasBeenDiscarded
                || member.Location == null
                || member.Location.HasBeenDiscarded)
            {
                return;
            }

            string reason;
            Hero hero = Hero.Current;
            if (!TryValidateHero(hero, out reason))
            {
                FailPlacement(
                    "hero became invalid during encounter placement: "
                        + reason,
                    member.Profile.Id);
                return;
            }

            if (npc.IsSummonOrAlly
                || !npc.IsHostileToHero()
                || !npc.CanEnterCombat(false)
                || npc.NpcAI == null)
            {
                FailPlacement(
                    member.Profile.Id
                        + " failed hostile, non-ally, or combat-capable validation",
                    member.Profile.Id);
                return;
            }

            member.Npc = npc;
            TryActivateEncounter(hero);
        }

        private void TryActivateEncounter(Hero hero)
        {
            for (int index = 0; index < _members.Count; index++)
            {
                if (_members[index].Npc == null)
                {
                    return;
                }
            }

            try
            {
                for (int index = 0; index < _members.Count; index++)
                {
                    _members[index].Npc.NpcAI.EnterCombatWith(hero);
                }

                for (int index = 0; index < _members.Count; index++)
                {
                    if (!HasExactHeroTarget(_members[index], hero))
                    {
                        FailPlacement(
                            "native combat entry did not acquire the exact Hero target",
                            _members[index].Profile.Id);
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                FailPlacement(
                    "native combat entry failed: "
                        + exception.GetBaseException().Message,
                    FirstUninitializedProfileId());
                return;
            }

            _initializing = false;
            _active = true;
            QueueEvent(
                HunterRuntimeEventKind.PlacementConfirmed,
                "all exact hostile encounter members initialized and entered native combat",
                _plan == null ? string.Empty : _plan.Primary.Id);
        }

        private static bool HasExactHeroTarget(
            SpawnedMember member,
            Hero hero)
        {
            return member != null
                && member.Npc != null
                && !member.Npc.HasBeenDiscarded
                && member.Npc.NpcAI != null
                && member.Npc.NpcAI.InCombat
                && ReferenceEquals(member.Npc.GetCurrentTarget(), hero);
        }

        private LocationTemplate ResolveTemplate(HunterProfile profile)
        {
            LocationTemplate template = new TemplateReference(
                profile.TemplateGuid).Get<LocationTemplate>(null);
            if (template == null
                || !string.Equals(
                    template.GUID,
                    profile.TemplateGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    profile.Id
                    + " did not resolve to its reviewed template identity");
            }
            return template;
        }

        private bool TryVerifyPosition(
            Hero hero,
            WyrdnessService wyrdness,
            LocationTemplate template,
            float distance,
            float centerAngle,
            int memberIndex,
            int memberCount,
            out Vector3 verified,
            out float verifiedDistance,
            out string reason)
        {
            verified = Vector3.zero;
            verifiedDistance = 0f;
            for (int attempt = 0;
                attempt < PositionAttemptsPerMember;
                attempt++)
            {
                float spread = memberCount <= 1
                    ? 0f
                    : (memberIndex - (memberCount - 1) * 0.5f) * 16f;
                float jitter = attempt == 0
                    ? 0f
                    : -24f + (float)_random.NextDouble() * 48f;
                Vector3 direction = Quaternion.AngleAxis(
                    centerAngle + spread + jitter,
                    Vector3.up) * hero.Rotation * Vector3.forward;
                Vector3 requested = hero.Coords
                    + direction * (distance + attempt * 3f);
                verified = BaseLocationSpawner.VerifyPosition(
                    requested,
                    template,
                    true);
                verifiedDistance = Vector3.Distance(
                    hero.Coords,
                    verified);
                if (verifiedDistance < MinimumVerifiedDistanceMeters
                    || wyrdness.IsInRepeller(verified)
                    || IsTooCloseToPlacedMember(verified))
                {
                    continue;
                }

                reason = string.Empty;
                return true;
            }

            reason = "native verification could not find a safe, separated position";
            return false;
        }

        private bool IsTooCloseToPlacedMember(Vector3 position)
        {
            for (int index = 0; index < _members.Count; index++)
            {
                Location location = _members[index].Location;
                if (location != null
                    && !location.HasBeenDiscarded
                    && Vector3.Distance(location.Coords, position)
                        < MinimumMemberSeparationMeters)
                {
                    return true;
                }
            }
            return false;
        }

        private void FailPlacement(
            string reason,
            string profileId)
        {
            string locationId = DescribeLocationIds();
            LastFailedProfileId = profileId ?? string.Empty;
            CleanupLiveLocations();
            ClearReferences();
            _pendingEvent = new HunterRuntimeEvent(
                HunterRuntimeEventKind.PlacementFailed,
                reason,
                locationId,
                profileId);
        }

        private void QueueEvent(
            HunterRuntimeEventKind kind,
            string reason,
            string profileId)
        {
            _pendingEvent = new HunterRuntimeEvent(
                kind,
                reason,
                DescribeLocationIds(),
                profileId);
        }

        private void ReleaseReferences(bool discardLiveTargets)
        {
            if (discardLiveTargets)
            {
                CleanupLiveLocations();
            }
            ClearReferences();
        }

        private void CleanupLiveLocations()
        {
            for (int index = 0; index < _members.Count; index++)
            {
                try
                {
                    Location location = _members[index].Location;
                    if (location != null
                        && !location.HasBeenDiscarded)
                    {
                        location.Discard();
                    }
                }
                catch (Exception exception)
                {
                    _log.LogWarning(
                        "Could not discard a volatile official-hunt Location: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private void ClearReferences()
        {
            _initializing = false;
            _active = false;
            _initializationSeconds = 0f;
            _escapeSeconds = 0f;
            _reacquisitionSeconds = 0f;
            _plan = null;
            _members.Clear();
        }

        private bool AnyLocationMissing()
        {
            for (int index = 0; index < _members.Count; index++)
            {
                Location location = _members[index].Location;
                if (location == null || location.HasBeenDiscarded)
                {
                    return true;
                }
            }
            return false;
        }

        private string FirstMissingProfileId()
        {
            for (int index = 0; index < _members.Count; index++)
            {
                Location location = _members[index].Location;
                if (location == null || location.HasBeenDiscarded)
                {
                    return _members[index].Profile.Id;
                }
            }
            return string.Empty;
        }

        private string FirstUninitializedProfileId()
        {
            for (int index = 0; index < _members.Count; index++)
            {
                if (_members[index].Npc == null)
                {
                    return _members[index].Profile.Id;
                }
            }
            return _members.Count == 0
                ? string.Empty
                : _members[0].Profile.Id;
        }

        private string DescribeLocationIds()
        {
            if (_members.Count == 0)
            {
                return "unknown";
            }

            string[] ids = new string[_members.Count];
            for (int index = 0; index < _members.Count; index++)
            {
                ids[index] = SafeLocationId(_members[index].Location);
            }
            return string.Join(",", ids);
        }

        private static bool TryValidateHero(
            Hero hero,
            out string reason)
        {
            if (hero == null || hero.HasBeenDiscarded)
            {
                reason = "hero is unavailable";
                return false;
            }
            if (!hero.MainViewInitialized)
            {
                reason = "hero view is not ready";
                return false;
            }
            if (!hero.IsAlive || hero.IsDying)
            {
                reason = "hero is not alive";
                return false;
            }
            if (hero.IsPortaling)
            {
                reason = "hero is portaling";
                return false;
            }
            if (hero.IsSwimming)
            {
                reason = "hero is swimming";
                return false;
            }
            if (hero.IsSafeFromWyrdness)
            {
                reason = "hero is protected from Wyrdness";
                return false;
            }
            if (hero.HasElement<PacifistMarker>())
            {
                reason = "hero is in a native pacifist safe zone";
                return false;
            }
            if (hero.HeroCombat != null
                && hero.HeroCombat.IsHeroInFight)
            {
                reason = "hero is already in unrelated combat";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static string SafeLocationId(Location location)
        {
            try
            {
                return location == null
                    || location.HasBeenDiscarded
                    || string.IsNullOrWhiteSpace(location.ID)
                        ? "unknown"
                        : location.ID;
            }
            catch
            {
                return "unknown";
            }
        }

        private static float FiniteNonNegative(float value)
        {
            return value > 0f
                && !float.IsNaN(value)
                && !float.IsInfinity(value)
                    ? value
                    : 0f;
        }

        private sealed class SpawnedMember
        {
            public readonly HunterProfile Profile;
            public readonly Location Location;
            public readonly float VerifiedDistance;
            public NpcElement Npc;
            public int ReacquisitionAttempts;

            public SpawnedMember(
                HunterProfile profile,
                Location location,
                float verifiedDistance)
            {
                Profile = profile;
                Location = location;
                VerifiedDistance = verifiedDistance;
            }
        }
    }
}
