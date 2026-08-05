using System;
using System.Collections.Generic;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.AI.Combat.Attachments;
using Awaken.TG.Main.AI.Movement.States;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
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
using Pathfinding;
using UnityEngine;

namespace EyesInTheDark
{
    internal enum AmbientStalkerRuntimeEventKind
    {
        None,
        PlacementConfirmed,
        PlacementFailed,
        Sighted,
        Fled,
        PassiveDespawned,
        Escalated,
        HostileKilled,
        LostTarget
    }

    internal struct AmbientStalkerRuntimeEvent
    {
        public readonly AmbientStalkerRuntimeEventKind Kind;
        public readonly string ProfileId;
        public readonly string DisplayName;
        public readonly string LocationId;
        public readonly string Reason;
        public readonly float AggressionThreshold;
        public readonly float DistanceMeters;
        public readonly bool WasSeen;
        public readonly bool ProvokedByHero;
        public readonly AmbientStalkerEscalationCause EscalationCause;

        public AmbientStalkerRuntimeEvent(
            AmbientStalkerRuntimeEventKind kind,
            AmbientStalkerProfile profile,
            string locationId,
            string reason,
            float aggressionThreshold,
            float distanceMeters,
            bool wasSeen,
            bool provokedByHero = false,
            AmbientStalkerEscalationCause escalationCause =
                AmbientStalkerEscalationCause.None)
        {
            Kind = kind;
            ProfileId = profile == null
                ? string.Empty
                : profile.Id;
            DisplayName = profile == null
                ? string.Empty
                : profile.DisplayName;
            LocationId = locationId ?? string.Empty;
            Reason = reason ?? string.Empty;
            AggressionThreshold = aggressionThreshold;
            DistanceMeters = distanceMeters;
            WasSeen = wasSeen;
            ProvokedByHero = provokedByHero;
            EscalationCause = escalationCause;
        }
    }

    internal sealed class AmbientStalkerRuntime : IListenerOwner
    {
        private const float MinimumVerifiedDistanceMeters = 25f;
        private const float InitializationTimeoutSeconds = 12f;
        private const int PositionAttempts = 10;
        private const float VisibilityConfirmationSeconds = 0.35f;
        private const float CombatReassertionSeconds = 0.5f;
        private const int MaximumCombatReassertions = 3;
        private const float PassiveObserveDistanceMeters = 20f;
        private const float FleeRearmSeconds = 5f;

        private readonly ManualLogSource _log;
        private readonly System.Random _random;
        private readonly Queue<AmbientStalkerRuntimeEvent> _events =
            new Queue<AmbientStalkerRuntimeEvent>();

        private AmbientStalkerProfile _profile;
        private Location _location;
        private NpcElement _npc;
        private Renderer[] _renderers;
        private BlockEnterCombatMarker _ownedCombatBlock;
        private HideEnemyFromPlayer _ownedHiddenPresentation;
        private IEventListener _damageListener;
        private MovementState _ownedMovementState;
        private float _aggressionThreshold;
        private float _initializationSeconds;
        private float _visibilitySeconds;
        private float _offscreenSeconds;
        private float _lifetimeSeconds;
        private float _maximumLifetimeSeconds;
        private float _movementSeconds;
        private float _observeSeconds;
        private float _fleeSeconds;
        private float _fleeRearmSeconds;
        private float _combatReassertionRemainingSeconds;
        private float _approachSampleSeconds;
        private float _approachStartDistance;
        private Vector3 _approachStartHeroPosition;
        private bool _initializing;
        private bool _active;
        private bool _hostile;
        private bool _wasSeen;
        private bool _provocationThreatApplied;
        private int _combatReassertions;
        private int _fleeCount;
        private AmbientMovementMode _movementMode;

        public bool IsInitializing
        {
            get { return _initializing; }
        }

        public bool IsActive
        {
            get { return _active; }
        }

        public bool IsPassive
        {
            get { return _active && !_hostile; }
        }

        public bool IsHostile
        {
            get { return _active && _hostile; }
        }

        public bool IsBusy
        {
            get { return _initializing || _active; }
        }

        public bool CanReceiveEvents
        {
            get { return _initializing || _active; }
        }

        public AmbientMovementMode MovementMode
        {
            get { return _movementMode; }
        }

        public string ActiveProfileId
        {
            get
            {
                return _profile == null
                    ? string.Empty
                    : _profile.Id;
            }
        }

        public float AggressionThreshold
        {
            get { return _aggressionThreshold; }
        }

        public string LastFailedProfileId { get; private set; }

        public AmbientStalkerRuntime(
            ManualLogSource log,
            int seed)
        {
            _log = log;
            _random = new System.Random(seed);
        }

        public bool TryStart(
            Hero hero,
            AmbientStalkerSelection selection,
            float minimumSpawnDistanceMeters,
            float maximumSpawnDistanceMeters,
            out string reason)
        {
            LastFailedProfileId = string.Empty;
            if (_initializing || _active)
            {
                reason = "an ambient stalker already exists";
                return false;
            }
            if (selection == null
                || !selection.Success
                || selection.Profile == null)
            {
                reason = "the ambient profile was empty";
                return false;
            }
            if (!TryValidateHero(hero, out reason))
            {
                return false;
            }

            Camera camera = TryGetHeroCamera(hero);
            if (camera == null)
            {
                reason = "the active Hero camera is unavailable";
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

            AmbientStalkerProfile profile = selection.Profile;
            _profile = profile;
            LastFailedProfileId = profile.Id;
            try
            {
                LocationTemplate template = ResolveTemplate(
                    profile);
                Vector3 verified;
                float verifiedDistance;
                if (!TryVerifyOffscreenPosition(
                    hero,
                    camera,
                    wyrdness,
                    template,
                    minimumSpawnDistanceMeters,
                    maximumSpawnDistanceMeters,
                    out verified,
                    out verifiedDistance,
                    out reason))
                {
                    ClearReferences();
                    return false;
                }

                Vector3 facing = hero.Coords - verified;
                facing.y = 0f;
                Quaternion rotation = facing.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(facing)
                    : hero.Rotation;
                _location = template.SpawnLocation(
                    verified,
                    rotation);
                if (_location == null || _location.HasBeenDiscarded)
                {
                    reason = profile.Id
                        + " native placement returned no live Location";
                    CleanupLiveLocation();
                    ClearReferences();
                    return false;
                }

                _location.MarkedNotSaved = true;
                _aggressionThreshold = selection.AggressionThreshold;
                _initializing = true;
                _initializationSeconds = 0f;
                _approachStartDistance = verifiedDistance;
                _approachStartHeroPosition = hero.Coords;
                ModelUtils.AfterFullyInitialized(
                    _location,
                    OnLocationInitialized,
                    null);

                if (!_initializing && !_active
                    && _events.Count > 0
                    && _events.Peek().Kind
                        == AmbientStalkerRuntimeEventKind.PlacementFailed)
                {
                    AmbientStalkerRuntimeEvent failure = _events.Dequeue();
                    LastFailedProfileId = failure.ProfileId;
                    reason = failure.Reason;
                    return false;
                }

                LastFailedProfileId = string.Empty;
                reason = profile.DisplayName
                    + " off-camera native placement requested at "
                    + verifiedDistance.ToString("0.0")
                    + "m";
                return true;
            }
            catch (Exception exception)
            {
                reason = "ambient placement failed for "
                    + profile.Id
                    + ": "
                    + exception.GetBaseException().Message;
                CleanupLiveLocation();
                ClearReferences();
                return false;
            }
        }

        public void Tick(
            float activeSeconds,
            Hero hero,
            float threat,
            float minimumDisappearDistanceMeters,
            float offCameraDisappearSeconds)
        {
            float delta = FiniteNonNegative(activeSeconds);
            if (_initializing)
            {
                _initializationSeconds += delta;
                if (_location == null || _location.HasBeenDiscarded)
                {
                    FailPlacement(
                        "the volatile stalker Location disappeared during initialization");
                }
                else if (_initializationSeconds
                    >= InitializationTimeoutSeconds)
                {
                    FailPlacement(
                        "ambient Location/Npc initialization timed out");
                }
                return;
            }

            if (!_active)
            {
                return;
            }
            if (_location == null
                || _location.HasBeenDiscarded
                || _npc == null
                || _npc.HasBeenDiscarded)
            {
                QueueEvent(
                    AmbientStalkerRuntimeEventKind.LostTarget,
                    "the exact volatile stalker Location was lost",
                    0f);
                ReleaseReferences(false);
                return;
            }
            if (!_npc.IsAlive || _npc.IsDying)
            {
                QueueEvent(
                    AmbientStalkerRuntimeEventKind.HostileKilled,
                    "the exact ambient stalker died",
                    DistanceToHero(hero));
                ReleaseReferences(false);
                return;
            }

            float distance = DistanceToHero(hero);
            if (_hostile)
            {
                AdvanceHostile(
                    delta,
                    hero,
                    distance);
                return;
            }

            if (AmbientStalkerPolicy.ShouldEscalateFromClosePursuit(
                _movementMode,
                distance))
            {
                Escalate(
                    hero,
                    "the Hero closed within the fleeing stalker's defensive distance",
                    AmbientStalkerEscalationCause.ClosePursuit);
                return;
            }

            if (AmbientStalkerPolicy.ShouldEscalate(
                threat,
                _aggressionThreshold,
                false,
                _hostile))
            {
                Escalate(
                    hero,
                    "hidden threat threshold reached",
                    AmbientStalkerEscalationCause.HiddenThreat);
                return;
            }

            Camera camera = TryGetHeroCamera(hero);
            if (camera == null)
            {
                return;
            }

            bool onScreen = IsLocationVisible(
                _location,
                camera,
                0f);
            if (onScreen)
            {
                _offscreenSeconds = 0f;
                _visibilitySeconds += delta;
                if (!_wasSeen
                    && _visibilitySeconds
                        >= VisibilityConfirmationSeconds)
                {
                    _wasSeen = true;
                    QueueEvent(
                        AmbientStalkerRuntimeEventKind.Sighted,
                        "renderer bounds remained in the Hero camera",
                        distance);
                }
            }
            else
            {
                _visibilitySeconds = 0f;
                _offscreenSeconds += delta;
            }

            _lifetimeSeconds += delta;
            AdvancePassiveMovement(
                delta,
                hero,
                distance);

            float minimumDistance = Mathf.Clamp(
                minimumDisappearDistanceMeters,
                15f,
                80f);
            float offCameraRequired = Math.Max(
                0.5f,
                offCameraDisappearSeconds);
            bool witnessedDeparture = _wasSeen
                && _offscreenSeconds >= offCameraRequired;
            bool lifetimeExpired = _lifetimeSeconds
                >= _maximumLifetimeSeconds;
            if (AmbientStalkerPolicy.CanPassivelyDespawn(
                _hostile,
                onScreen,
                _offscreenSeconds,
                offCameraRequired,
                distance,
                minimumDistance,
                _wasSeen,
                lifetimeExpired))
            {
                QueueEvent(
                    AmbientStalkerRuntimeEventKind.PassiveDespawned,
                    witnessedDeparture
                        ? "passive stalker left continuously outside the camera"
                        : "unseen passive stalker lifetime elapsed off-camera",
                    distance);
                ReleaseReferences(true);
            }
        }

        public bool TryProvoke(
            NpcElement target,
            Hero hero,
            out bool applyThreat,
            out string reason)
        {
            applyThreat = false;
            if (!_active
                || _hostile
                || target == null
                || !ReferenceEquals(target, _npc))
            {
                reason = _hostile
                    ? "the exact ambient stalker was already hostile"
                    : "damage target was not the exact passive ambient stalker";
                return false;
            }

            if (!_provocationThreatApplied)
            {
                _provocationThreatApplied = true;
                applyThreat = true;
            }
            if (AmbientStalkerPolicy.ShouldEscalate(
                0f,
                _aggressionThreshold,
                true,
                _hostile))
            {
                Escalate(
                    hero,
                    "the Hero attacked the exact stalker",
                    AmbientStalkerEscalationCause.HeroAttack);
            }
            reason = "exact ambient stalker provoked";
            return true;
        }

        public bool IsExactStalker(NpcElement npc)
        {
            return _active
                && npc != null
                && ReferenceEquals(npc, _npc);
        }

        public void ConfirmKilled(NpcElement npc)
        {
            if (!_active || !ReferenceEquals(npc, _npc))
            {
                return;
            }
            QueueEvent(
                AmbientStalkerRuntimeEventKind.HostileKilled,
                "the Hero killed the exact ambient stalker",
                0f);
            ReleaseReferences(false);
        }

        public bool TryConsumeEvent(
            out AmbientStalkerRuntimeEvent runtimeEvent)
        {
            if (_events.Count == 0)
            {
                runtimeEvent = new AmbientStalkerRuntimeEvent();
                return false;
            }
            runtimeEvent = _events.Dequeue();
            return true;
        }

        public void Cancel(
            string reason,
            bool discardLiveTarget)
        {
            if (!_initializing && !_active)
            {
                return;
            }
            if (discardLiveTarget)
            {
                CleanupLiveLocation();
            }
            _log.LogInfo(
                "Ambient stalker runtime cancelled: "
                + (string.IsNullOrWhiteSpace(reason)
                    ? "unspecified"
                    : reason)
                + ".");
            ClearReferences();
            _events.Clear();
        }

        internal static bool IsViewportPointVisible(
            Vector3 viewportPoint,
            float margin)
        {
            return AmbientStalkerPolicy.IsViewportPointVisible(
                viewportPoint.x,
                viewportPoint.y,
                viewportPoint.z,
                margin);
        }

        private void OnLocationInitialized()
        {
            if (!_initializing
                || _location == null
                || _location.HasBeenDiscarded)
            {
                return;
            }

            NpcElement npc = _location.TryGetElement<NpcElement>();
            if (npc == null)
            {
                FailPlacement(
                    "initialized stalker Location contained no NpcElement");
                return;
            }
            try
            {
                npc.OnCompletelyInitialized(OnNpcInitialized);
            }
            catch (Exception exception)
            {
                FailPlacement(
                    "could not attach the stalker Npc initialization callback: "
                    + exception.GetBaseException().Message);
            }
        }

        private void OnNpcInitialized(NpcElement npc)
        {
            if (!_initializing
                || npc == null
                || npc.HasBeenDiscarded
                || _location == null
                || _location.HasBeenDiscarded)
            {
                return;
            }

            string reason;
            Hero hero = Hero.Current;
            if (!TryValidateHero(hero, out reason))
            {
                FailPlacement(
                    "Hero became invalid during ambient placement: "
                    + reason);
                return;
            }
            if (npc.IsSummonOrAlly
                || !npc.IsHostileToHero()
                || !npc.CanEnterCombat(false)
                || npc.NpcAI == null
                || npc.NpcAI.InCombat
                || npc.Movement == null
                || npc.HealthElement == null
                || npc.HasElement<BlockEnterCombatMarker>())
            {
                FailPlacement(
                    "ambient profile failed hostile, non-ally, combat, movement, health, or clean-marker validation");
                return;
            }

            Camera camera = TryGetHeroCamera(hero);
            _renderers = _location.MainView == null
                ? new Renderer[0]
                : _location.MainView.transform
                    .GetComponentsInChildren<Renderer>();
            if (camera == null
                || IsLocationVisible(_location, camera, 0.04f))
            {
                FailPlacement(
                    camera == null
                        ? "Hero camera disappeared during ambient placement"
                        : "verified stalker renderer initialized inside the camera margin");
                return;
            }

            _npc = npc;
            try
            {
                _ownedCombatBlock =
                    npc.AddElement<BlockEnterCombatMarker>();
                if (!_location.HasElement<HideEnemyFromPlayer>())
                {
                    _ownedHiddenPresentation = _location.AddElement(
                        new HideEnemyFromPlayer(true));
                }
                _damageListener = ModelExtensions.ListenTo(
                    npc.HealthElement,
                    HealthElement.Events.BeforeDamageTaken,
                    OnBeforeDamageTaken,
                    this);
                if (!TryChangeMovement(
                    AmbientMovementMode.Observe,
                    hero,
                    out reason))
                {
                    FailPlacement(reason);
                    return;
                }
            }
            catch (Exception exception)
            {
                FailPlacement(
                    "ambient passive-state initialization failed: "
                    + exception.GetBaseException().Message);
                return;
            }

            _initializing = false;
            _active = true;
            _hostile = false;
            _movementMode = AmbientMovementMode.Observe;
            _observeSeconds = RandomRange(8f, 16f);
            _maximumLifetimeSeconds = RandomRange(55f, 90f);
            _approachStartDistance = DistanceToHero(hero);
            _approachStartHeroPosition = hero.Coords;
            QueueEvent(
                AmbientStalkerRuntimeEventKind.PlacementConfirmed,
                "exact hostile native actor initialized off-camera in Observe",
                _approachStartDistance);
        }

        private void OnBeforeDamageTaken(Damage damage)
        {
            if (!_active
                || _hostile
                || damage == null
                || !ReferenceEquals(
                    damage.DamageDealerPure,
                    Hero.Current))
            {
                return;
            }

            bool ignored;
            string ignoredReason;
            TryProvoke(
                _npc,
                Hero.Current,
                out ignored,
                out ignoredReason);
        }

        private void AdvancePassiveMovement(
            float delta,
            Hero hero,
            float distance)
        {
            if (hero == null
                || hero.HasBeenDiscarded
                || delta <= 0f)
            {
                return;
            }

            _movementSeconds += delta;
            _fleeRearmSeconds = Math.Max(
                0f,
                _fleeRearmSeconds - delta);
            _approachSampleSeconds += delta;
            bool pursued = false;
            if (_approachSampleSeconds >= 0.75f)
            {
                Vector3 toStalker = _location.Coords - hero.Coords;
                toStalker.y = 0f;
                Vector3 look = hero.VHeroController == null
                    ? hero.Rotation * Vector3.forward
                    : hero.VHeroController.LookDirection;
                look.y = 0f;
                float facingDot = toStalker.sqrMagnitude <= 0.001f
                    || look.sqrMagnitude <= 0.001f
                        ? -1f
                        : Vector3.Dot(
                            look.normalized,
                            toStalker.normalized);
                float heroTravel = Vector3.Distance(
                    hero.Coords,
                    _approachStartHeroPosition);
                float heroSpeed = heroTravel
                    / Math.Max(0.01f, _approachSampleSeconds);
                float distanceClosed = _approachStartDistance
                    - distance;
                pursued = _fleeRearmSeconds <= 0f
                    && AmbientStalkerPolicy.ShouldFleeFromApproach(
                        distance,
                        facingDot,
                        heroSpeed,
                        distanceClosed,
                        _approachSampleSeconds);
                _approachSampleSeconds = 0f;
                _approachStartDistance = distance;
                _approachStartHeroPosition = hero.Coords;
            }

            bool fleeComplete = _movementMode
                    == AmbientMovementMode.Flee
                && (distance >= 38f || _fleeSeconds >= 7f);
            bool observeElapsed = _movementMode
                    == AmbientMovementMode.Observe
                && _movementSeconds >= _observeSeconds;
            AmbientMovementMode desired =
                AmbientStalkerPolicy.NextPassiveMovementMode(
                    _movementMode,
                    pursued,
                    fleeComplete,
                    observeElapsed);
            if (_movementMode == AmbientMovementMode.Follow
                && distance <= PassiveObserveDistanceMeters)
            {
                desired = AmbientMovementMode.Observe;
            }

            if (_movementMode == AmbientMovementMode.Flee)
            {
                _fleeSeconds += delta;
            }
            if (desired == _movementMode)
            {
                return;
            }

            bool completedFlee = _movementMode
                    == AmbientMovementMode.Flee
                && desired != AmbientMovementMode.Flee;
            string reason;
            if (!TryChangeMovement(desired, hero, out reason))
            {
                _log.LogWarning(
                    "Ambient stalker movement transition failed: "
                    + reason);
                return;
            }

            _movementMode = desired;
            _movementSeconds = 0f;
            if (completedFlee)
            {
                _fleeRearmSeconds = FleeRearmSeconds;
            }
            if (desired == AmbientMovementMode.Flee)
            {
                _fleeSeconds = 0f;
                _fleeCount++;
                QueueEvent(
                    AmbientStalkerRuntimeEventKind.Fled,
                    "the Hero deliberately closed on the passive stalker; flee "
                    + _fleeCount,
                    distance);
            }
            else if (desired == AmbientMovementMode.Observe)
            {
                _observeSeconds = RandomRange(7f, 14f);
            }
        }

        private void Escalate(
            Hero hero,
            string cause,
            AmbientStalkerEscalationCause escalationCause)
        {
            if (!_active || _hostile)
            {
                return;
            }

            _hostile = true;
            _movementMode = AmbientMovementMode.Hostile;
            _combatReassertions = 0;
            _combatReassertionRemainingSeconds = 0f;
            DisposeDamageListener();
            ReleaseOwnedMovement();
            ReleasePassiveGuards();
            bool acquired = TryEnterCombat(hero);
            QueueEvent(
                AmbientStalkerRuntimeEventKind.Escalated,
                cause
                    + (acquired
                        ? "; exact Hero target acquired"
                        : "; native combat acquisition pending"),
                DistanceToHero(hero),
                escalationCause
                    == AmbientStalkerEscalationCause.HeroAttack,
                escalationCause);
        }

        private void AdvanceHostile(
            float delta,
            Hero hero,
            float distance)
        {
            if (!HasExactHeroTarget(hero)
                && _combatReassertions
                    < MaximumCombatReassertions)
            {
                _combatReassertionRemainingSeconds -= delta;
                if (_combatReassertionRemainingSeconds <= 0f)
                {
                    _combatReassertions++;
                    TryEnterCombat(hero);
                    _combatReassertionRemainingSeconds =
                        CombatReassertionSeconds;
                }
            }

            // Hostile stalkers remain owned until death, native discard,
            // dawn, loading, or a scene transition. Distance alone must
            // never make a hostile actor disappear or free another lane.
        }

        private bool TryEnterCombat(Hero hero)
        {
            if (hero == null
                || hero.HasBeenDiscarded
                || _npc == null
                || _npc.HasBeenDiscarded
                || _npc.NpcAI == null)
            {
                return false;
            }
            try
            {
                _npc.NpcAI.EnterCombatWith(hero, true);
                return HasExactHeroTarget(hero);
            }
            catch (Exception exception)
            {
                _log.LogWarning(
                    "Ambient stalker combat escalation failed: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool HasExactHeroTarget(Hero hero)
        {
            return hero != null
                && !hero.HasBeenDiscarded
                && _npc != null
                && !_npc.HasBeenDiscarded
                && _npc.NpcAI != null
                && _npc.NpcAI.InCombat
                && ReferenceEquals(_npc.GetCurrentTarget(), hero);
        }

        private bool TryChangeMovement(
            AmbientMovementMode mode,
            Hero hero,
            out string reason)
        {
            if (_npc == null
                || _npc.HasBeenDiscarded
                || _npc.Movement == null)
            {
                reason = "the exact stalker movement controller is unavailable";
                return false;
            }
            try
            {
                ReleaseOwnedMovement();
                switch (mode)
                {
                    case AmbientMovementMode.Observe:
                        _ownedMovementState = new Observe();
                        break;
                    case AmbientMovementMode.Follow:
                        _ownedMovementState = new FollowMovement(
                            hero,
                            18f,
                            _location.Coords,
                            90f);
                        break;
                    case AmbientMovementMode.Flee:
                        _ownedMovementState = new Flee(hero);
                        break;
                    default:
                        reason = "hostile movement belongs to native combat AI";
                        return false;
                }
                _npc.Movement.ChangeMainState(
                    _ownedMovementState);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = mode
                    + " failed: "
                    + exception.GetBaseException().Message;
                return false;
            }
        }

        private LocationTemplate ResolveTemplate(
            AmbientStalkerProfile profile)
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

        private bool TryVerifyOffscreenPosition(
            Hero hero,
            Camera camera,
            WyrdnessService wyrdness,
            LocationTemplate template,
            float configuredMinimumDistance,
            float configuredMaximumDistance,
            out Vector3 verified,
            out float verifiedDistance,
            out string reason)
        {
            verified = Vector3.zero;
            verifiedDistance = 0f;
            float minimumDistance = Mathf.Clamp(
                configuredMinimumDistance,
                MinimumVerifiedDistanceMeters,
                100f);
            float maximumDistance = Mathf.Clamp(
                configuredMaximumDistance,
                minimumDistance,
                100f);
            Vector3 cameraForward = camera.transform.forward;
            cameraForward.y = 0f;
            if (cameraForward.sqrMagnitude <= 0.001f)
            {
                cameraForward = hero.Rotation * Vector3.forward;
            }
            cameraForward.Normalize();

            for (int attempt = 0; attempt < PositionAttempts; attempt++)
            {
                float angle = 105f
                    + (float)_random.NextDouble() * 150f;
                float distance = minimumDistance
                    + (float)_random.NextDouble()
                        * (maximumDistance - minimumDistance);
                Vector3 direction = Quaternion.AngleAxis(
                    angle,
                    Vector3.up) * cameraForward;
                Vector3 requested = hero.Coords
                    + direction * (distance + attempt * 2f);
                verified = BaseLocationSpawner.VerifyPosition(
                    requested,
                    template,
                    true);
                verifiedDistance = Vector3.Distance(
                    hero.Coords,
                    verified);
                if (verifiedDistance < minimumDistance
                    || verifiedDistance > maximumDistance + 8f
                    || wyrdness.IsInRepeller(verified)
                    || !HasConnectedPath(
                        hero.Coords,
                        verified)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            verified + Vector3.up * 1.2f),
                        0.04f))
                {
                    continue;
                }

                reason = string.Empty;
                return true;
            }

            reason = "native verification found no navigable off-camera position";
            return false;
        }

        private static bool HasConnectedPath(
            Vector3 heroPosition,
            Vector3 stalkerPosition)
        {
            if (AstarPath.active == null)
            {
                return false;
            }

            GraphNode heroNode = AstarPath.active.GetNearest(
                heroPosition,
                NNConstraint.Walkable).node;
            GraphNode stalkerNode = AstarPath.active.GetNearest(
                stalkerPosition,
                NNConstraint.Walkable).node;
            return heroNode != null
                && stalkerNode != null
                && PathUtilities.IsPathPossible(
                    heroNode,
                    stalkerNode);
        }

        private static Camera TryGetHeroCamera(Hero hero)
        {
            try
            {
                return hero == null
                    || hero.HasBeenDiscarded
                    || hero.VHeroController == null
                        ? null
                        : hero.VHeroController.MainCamera;
            }
            catch
            {
                return null;
            }
        }

        private bool IsLocationVisible(
            Location location,
            Camera camera,
            float margin)
        {
            if (location == null
                || location.HasBeenDiscarded
                || location.MainView == null
                || camera == null)
            {
                return false;
            }

            Renderer[] renderers = _renderers;
            if (renderers == null)
            {
                renderers = location.MainView.transform
                    .GetComponentsInChildren<Renderer>();
                _renderers = renderers;
            }
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }
                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                if (IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(min.x, min.y, min.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(max.x, min.y, min.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(min.x, max.y, min.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(max.x, max.y, min.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(min.x, min.y, max.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(max.x, min.y, max.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(min.x, max.y, max.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(
                            new Vector3(max.x, max.y, max.z)),
                        margin)
                    || IsViewportPointVisible(
                        camera.WorldToViewportPoint(bounds.center),
                        margin))
                {
                    return true;
                }
            }
            return renderers.Length == 0
                && IsViewportPointVisible(
                    camera.WorldToViewportPoint(
                        location.Coords + Vector3.up * 1.2f),
                    margin);
        }

        private static bool TryValidateHero(
            Hero hero,
            out string reason)
        {
            if (hero == null || hero.HasBeenDiscarded)
            {
                reason = "Hero is unavailable";
                return false;
            }
            if (!hero.MainViewInitialized)
            {
                reason = "Hero view is not ready";
                return false;
            }
            if (!hero.IsAlive || hero.IsDying)
            {
                reason = "Hero is not alive";
                return false;
            }
            if (hero.IsPortaling || hero.IsSwimming)
            {
                reason = "Hero is traveling or swimming";
                return false;
            }
            if (hero.IsSafeFromWyrdness
                || hero.HasElement<PacifistMarker>())
            {
                reason = "Hero is protected from Wyrdness";
                return false;
            }
            if (hero.HeroCombat != null
                && hero.HeroCombat.IsHeroInFight)
            {
                reason = "Hero is already in unrelated combat";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void FailPlacement(string reason)
        {
            AmbientStalkerProfile failedProfile = _profile;
            string locationId = SafeLocationId(_location);
            float failedThreshold = _aggressionThreshold;
            LastFailedProfileId = ActiveProfileId;
            CleanupLiveLocation();
            ClearReferences();
            _events.Enqueue(new AmbientStalkerRuntimeEvent(
                AmbientStalkerRuntimeEventKind.PlacementFailed,
                failedProfile,
                locationId,
                reason,
                failedThreshold,
                0f,
                false));
        }

        private void QueueEvent(
            AmbientStalkerRuntimeEventKind kind,
            string reason,
            float distance,
            bool provokedByHero = false,
            AmbientStalkerEscalationCause escalationCause =
                AmbientStalkerEscalationCause.None)
        {
            _events.Enqueue(new AmbientStalkerRuntimeEvent(
                kind,
                _profile,
                SafeLocationId(_location),
                reason,
                _aggressionThreshold,
                distance,
                _wasSeen,
                provokedByHero,
                escalationCause));
        }

        private void ReleaseReferences(bool discardLiveTarget)
        {
            if (discardLiveTarget)
            {
                CleanupLiveLocation();
            }
            ClearReferences();
        }

        private void CleanupLiveLocation()
        {
            DisposeDamageListener();
            ReleaseOwnedMovement();
            ReleasePassiveGuards();
            try
            {
                if (_location != null
                    && !_location.HasBeenDiscarded)
                {
                    _location.Discard();
                }
            }
            catch (Exception exception)
            {
                _log.LogWarning(
                    "Could not discard a volatile ambient-stalker Location: "
                    + exception.GetBaseException().Message);
            }
        }

        private void ReleaseOwnedMovement()
        {
            if (_ownedMovementState != null
                && _npc != null
                && !_npc.HasBeenDiscarded
                && _npc.Movement != null)
            {
                try
                {
                    _npc.Movement.ResetMainState(
                        _ownedMovementState);
                }
                catch
                {
                }
            }
            _ownedMovementState = null;
        }

        private void ReleasePassiveGuards()
        {
            if (_ownedCombatBlock != null
                && !_ownedCombatBlock.HasBeenDiscarded)
            {
                _ownedCombatBlock.Discard();
            }
            if (_ownedHiddenPresentation != null
                && !_ownedHiddenPresentation.HasBeenDiscarded)
            {
                _ownedHiddenPresentation.Discard();
            }
            _ownedCombatBlock = null;
            _ownedHiddenPresentation = null;
        }

        private void DisposeDamageListener()
        {
            if (World.EventSystem != null)
            {
                World.EventSystem.TryDisposeListener(
                    ref _damageListener);
                World.EventSystem.RemoveAllListenersOwnedBy(
                    this,
                    true);
            }
            else
            {
                _damageListener = null;
            }
        }

        private void ClearReferences()
        {
            DisposeDamageListener();
            _profile = null;
            _location = null;
            _npc = null;
            _renderers = null;
            _ownedCombatBlock = null;
            _ownedHiddenPresentation = null;
            _ownedMovementState = null;
            _aggressionThreshold = 0f;
            _initializationSeconds = 0f;
            _visibilitySeconds = 0f;
            _offscreenSeconds = 0f;
            _lifetimeSeconds = 0f;
            _maximumLifetimeSeconds = 0f;
            _movementSeconds = 0f;
            _observeSeconds = 0f;
            _fleeSeconds = 0f;
            _fleeRearmSeconds = 0f;
            _combatReassertionRemainingSeconds = 0f;
            _approachSampleSeconds = 0f;
            _approachStartDistance = 0f;
            _approachStartHeroPosition = Vector3.zero;
            _initializing = false;
            _active = false;
            _hostile = false;
            _wasSeen = false;
            _provocationThreatApplied = false;
            _combatReassertions = 0;
            _fleeCount = 0;
            _movementMode = AmbientMovementMode.Observe;
        }

        private float DistanceToHero(Hero hero)
        {
            return hero == null
                || hero.HasBeenDiscarded
                || _location == null
                || _location.HasBeenDiscarded
                    ? float.PositiveInfinity
                    : Vector3.Distance(
                        hero.Coords,
                        _location.Coords);
        }

        private float RandomRange(float minimum, float maximum)
        {
            return minimum
                + (float)_random.NextDouble()
                    * (maximum - minimum);
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
    }
}
