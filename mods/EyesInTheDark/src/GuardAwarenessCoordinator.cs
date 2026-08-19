using System;
using System.Collections.Generic;
using System.Linq;
using Awaken.TG.MVC;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.Factions.Markers;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Statuses.Duration;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Setup;
using BepInEx.Logging;
using UnityEngine;

namespace EyesInTheDark
{
    /// <summary>
    /// Lets ordinary nearby guards respond to an exact, live official hunt.
    /// The caller owns the hunt lifecycle and supplies only its known members.
    /// </summary>
    internal sealed class GuardAwarenessCoordinator
    {
        private const float DefaultScanIntervalSeconds = 1f;
        private const float MinimumScanIntervalSeconds = 0.25f;
        private const float MinimumRadiusMeters = 1f;

        private static readonly string[] GuardTemplateBlockTerms =
        {
            "Boss",
            "Unique",
            "Quest",
            "Story",
            "Tutorial",
            "Debug",
            "Challenge",
            "Interaction",
            "Bodyguard"
        };

        private readonly ManualLogSource _log;
        private readonly HashSet<GuardHunterPair> _assistedPairs =
            new HashSet<GuardHunterPair>();
        private readonly HashSet<NpcElement> _assistingGuards =
            new HashSet<NpcElement>();

        private float _scanElapsedSeconds = float.PositiveInfinity;
        private bool _reportedNoEligibleGuard;
        private bool _reportedCapReached;
        private bool _reportedDeferredEngagement;
        private bool _reportedEngagementFailure;
        private bool _reportedScanFailure;

        internal GuardAwarenessCoordinator(ManualLogSource log)
        {
            _log = log;
        }

        /// <summary>
        /// Advances the coordinator with its standard one-second scan cadence.
        /// </summary>
        internal void Tick(
            float activeSeconds,
            Hero hero,
            IList<NpcElement> exactHunters,
            float radiusMeters,
            int maximumGuards)
        {
            Tick(
                activeSeconds,
                hero,
                exactHunters,
                DefaultScanIntervalSeconds,
                radiusMeters,
                maximumGuards);
        }

        /// <summary>
        /// Scans only when this method is called and the caller-supplied active
        /// time reaches the supplied interval. The caller owns reset boundaries.
        /// </summary>
        internal void Tick(
            float activeSeconds,
            Hero hero,
            IList<NpcElement> exactHunters,
            float scanIntervalSeconds,
            float radiusMeters,
            int maximumGuards)
        {
            if (!IsUsableHero(hero)
                || exactHunters == null
                || exactHunters.Count == 0
                || maximumGuards <= 0)
            {
                return;
            }

            if (activeSeconds > 0f
                && !float.IsNaN(activeSeconds)
                && !float.IsInfinity(activeSeconds))
            {
                _scanElapsedSeconds += activeSeconds;
            }

            float interval = Mathf.Max(
                MinimumScanIntervalSeconds,
                IsFinite(scanIntervalSeconds)
                    ? scanIntervalSeconds
                    : DefaultScanIntervalSeconds);
            if (_scanElapsedSeconds < interval)
            {
                return;
            }
            _scanElapsedSeconds = 0f;

            if (_assistingGuards.Count >= maximumGuards)
            {
                ReportCapReached(maximumGuards);
                return;
            }

            try
            {
                float radius = IsFinite(radiusMeters)
                    ? Mathf.Max(MinimumRadiusMeters, radiusMeters)
                    : MinimumRadiusMeters;
                Scan(hero, exactHunters, radius, maximumGuards);
            }
            catch (Exception exception)
            {
                if (!_reportedScanFailure)
                {
                    _reportedScanFailure = true;
                    LogWarning(
                        "Guard awareness scan failed and will retry on a later "
                        + "official-hunt tick: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        /// <summary>
        /// Returns true only for a pair that this coordinator successfully
        /// engaged. FirstHunterRuntime uses this to avoid overriding a valid
        /// guard target with its normal Hero reacquisition.
        /// </summary>
        internal bool IsAssistedEngagement(
            NpcElement hunter,
            NpcElement currentTarget)
        {
            return hunter != null
                && currentTarget != null
                && !currentTarget.HasBeenDiscarded
                && currentTarget.IsAlive
                && !currentTarget.IsDying
                && _assistedPairs.Contains(
                    new GuardHunterPair(currentTarget, hunter));
        }

        /// <summary>
        /// Clears only EITD's in-memory encounter bookkeeping. It deliberately
        /// does not own native combat, faction, or marker cleanup.
        /// </summary>
        internal void Reset(string reason)
        {
            bool hadState = _assistedPairs.Count > 0
                || _assistingGuards.Count > 0;
            _assistedPairs.Clear();
            _assistingGuards.Clear();
            _scanElapsedSeconds = float.PositiveInfinity;
            _reportedNoEligibleGuard = false;
            _reportedCapReached = false;
            _reportedDeferredEngagement = false;
            _reportedEngagementFailure = false;
            _reportedScanFailure = false;

            if (hadState)
            {
                LogDebug(
                    "Guard awareness reset for official-hunt boundary: "
                    + (string.IsNullOrWhiteSpace(reason)
                        ? "unspecified"
                        : reason)
                    + ".");
            }
        }

        private void Scan(
            Hero hero,
            IList<NpcElement> exactHunters,
            float radiusMeters,
            int maximumGuards)
        {
            HashSet<NpcElement> knownHunters =
                new HashSet<NpcElement>(exactHunters);
            List<NpcCandidate> hunters = new List<NpcCandidate>();
            List<NpcCandidate> guards = new List<NpcCandidate>();
            Location[] locations = World.All<Location>().ToArraySlow();

            for (int index = 0; index < locations.Length; index++)
            {
                NpcCandidate candidate;
                if (!TryBuildCandidate(
                    locations[index],
                    hero.Coords,
                    out candidate))
                {
                    continue;
                }

                if (knownHunters.Contains(candidate.Npc))
                {
                    if (IsLiveOfficialHunter(candidate.Npc))
                    {
                        hunters.Add(candidate);
                    }
                }
                else if (candidate.DistanceToHero <= radiusMeters
                    && IsEligibleGuard(candidate))
                {
                    guards.Add(candidate);
                }
            }

            if (hunters.Count == 0)
            {
                return;
            }
            if (guards.Count == 0)
            {
                ReportNoEligibleGuard(radiusMeters, hunters.Count);
                return;
            }

            hunters.Sort(CompareDistanceToHero);
            guards.Sort(CompareDistanceToHero);
            int nextGuardIndex = 0;
            bool engagedOnPass;
            do
            {
                engagedOnPass = false;
                for (int hunterIndex = 0;
                    hunterIndex < hunters.Count;
                    hunterIndex++)
                {
                    if (_assistingGuards.Count >= maximumGuards)
                    {
                        ReportCapReached(maximumGuards);
                        return;
                    }

                    if (TryEngageNextGuard(
                        guards,
                        ref nextGuardIndex,
                        hunters[hunterIndex],
                        radiusMeters))
                    {
                        engagedOnPass = true;
                    }
                }
            }
            while (engagedOnPass
                && _assistingGuards.Count < maximumGuards);
        }

        private bool TryEngageNextGuard(
            List<NpcCandidate> guards,
            ref int nextGuardIndex,
            NpcCandidate hunter,
            float radiusMeters)
        {
            for (int attempt = 0; attempt < guards.Count; attempt++)
            {
                int guardIndex = (nextGuardIndex + attempt) % guards.Count;
                NpcCandidate guard = guards[guardIndex];
                if (_assistingGuards.Contains(guard.Npc))
                {
                    continue;
                }
                nextGuardIndex = (guardIndex + 1) % guards.Count;
                if (!IsWithinGuardAssistRadius(
                        guard,
                        hunter,
                        radiusMeters))
                {
                    continue;
                }
                if (TryEngageGuard(guard, hunter))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryEngageGuard(
            NpcCandidate guard,
            NpcCandidate hunter)
        {
            GuardHunterPair pair = new GuardHunterPair(guard.Npc, hunter.Npc);
            if (_assistedPairs.Contains(pair)
                || _assistingGuards.Contains(guard.Npc)
                || !IsEligibleGuard(guard)
                || !IsLiveOfficialHunter(hunter.Npc))
            {
                return false;
            }

            try
            {
                ICharacter target = hunter.Npc;
                guard.Npc.NpcAI.EnterCombatWith(target, false);
                bool guardInCombat = IsInCombat(guard.Npc);
                bool initialEnterCombat = HasExactHunterTarget(
                    guard.Npc,
                    hunter.Npc);
                bool appliedTemporaryAntagonism = false;
                bool forcedTarget = false;
                if (!guardInCombat)
                {
                    appliedTemporaryAntagonism = TryApplyTemporaryAntagonism(
                        guard.Npc,
                        hunter.Npc);
                    forcedTarget = AITargetingUtils.ForceAddCombatTarget(
                        guard.Npc,
                        target,
                        true);
                    guard.Npc.NpcAI.EnterCombatWith(target, true);
                }

                if (!HasExactHunterTarget(guard.Npc, hunter.Npc))
                {
                    if (!_reportedDeferredEngagement)
                    {
                        _reportedDeferredEngagement = true;
                        LogDebug(
                            "Guard awareness native combat request did not "
                            + "acquire the exact hunter target; will retry "
                            + "without consuming the pair. guard="
                            + guard.Describe()
                            + ", hunter="
                            + hunter.Describe()
                            + ".");
                    }
                    return false;
                }

                _assistedPairs.Add(pair);
                _assistingGuards.Add(guard.Npc);
                LogInfo(
                    "Guard awareness engaged an official hunter: guard="
                    + guard.Describe()
                    + ", hunter="
                    + hunter.Describe()
                    + ", guardDistanceToHero="
                    + guard.DistanceToHero.ToString("0.0")
                    + "m, hunterDistanceToHero="
                    + hunter.DistanceToHero.ToString("0.0")
                    + "m, initialEnterCombat="
                    + initialEnterCombat.ToString()
                    + ", temporaryFactionAntagonism="
                    + appliedTemporaryAntagonism.ToString()
                    + ", forcedTarget="
                    + forcedTarget.ToString()
                    + ", persistence=false.");
                return true;
            }
            catch (Exception exception)
            {
                if (!_reportedEngagementFailure)
                {
                    _reportedEngagementFailure = true;
                    LogWarning(
                        "Guard awareness could not engage a guard; no pair "
                        + "was consumed: "
                        + exception.GetBaseException().Message);
                }
                return false;
            }
        }

        private static bool TryBuildCandidate(
            Location location,
            Vector3 heroPosition,
            out NpcCandidate candidate)
        {
            candidate = null;
            if (location == null || location.HasBeenDiscarded)
            {
                return false;
            }
            float distanceSquared = (location.Coords - heroPosition).sqrMagnitude;
            NpcElement npc = location.TryGetElement<NpcElement>();
            if (npc == null)
            {
                return false;
            }
            if (location.Template == null)
            {
                return false;
            }

            candidate = new NpcCandidate(
                location,
                npc,
                location.Template.name,
                Mathf.Sqrt(distanceSquared));
            return true;
        }

        private static bool IsLiveOfficialHunter(NpcElement npc)
        {
            return npc != null
                && !npc.HasBeenDiscarded
                && npc.IsAlive
                && !npc.IsDying
                && !npc.IsSummonOrAlly
                && npc.NpcAI != null
                && npc.NpcAI.Working
                && npc.CanEnterCombat(false);
        }

        private static bool IsEligibleGuard(NpcCandidate candidate)
        {
            NpcElement npc = candidate.Npc;
            if (npc == null
                || npc.HasBeenDiscarded
                || !npc.IsAlive
                || npc.IsDying
                || npc.IsSummonOrAlly
                || npc.NpcAI == null
                || !npc.NpcAI.Working
                || npc.NpcAI.InCombat
                || !npc.CanEnterCombat(false))
            {
                return false;
            }

            string templateName = candidate.TemplateName;
            if (!templateName.StartsWith(
                    "Spec_NPC_",
                    StringComparison.OrdinalIgnoreCase)
                || templateName.IndexOf(
                    "Guard",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
            for (int index = 0;
                index < GuardTemplateBlockTerms.Length;
                index++)
            {
                if (templateName.IndexOf(
                        GuardTemplateBlockTerms[index],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            NpcAttachment attachment = candidate.Location.Template
                .GetComponent<NpcAttachment>();
            return attachment != null && !attachment.IsUnique;
        }

        private static bool IsWithinGuardAssistRadius(
            NpcCandidate guard,
            NpcCandidate hunter,
            float radiusMeters)
        {
            return guard != null
                && hunter != null
                && Vector3.Distance(
                    guard.Location.Coords,
                    hunter.Location.Coords) <= radiusMeters;
        }

        private static bool TryApplyTemporaryAntagonism(
            NpcElement guard,
            NpcElement hunter)
        {
            try
            {
                if (guard.Faction == hunter.Faction)
                {
                    return false;
                }
                return AntagonismMarker.TryApplySingleton<
                    FactionAntagonism,
                    UntilIdle>(
                        new FactionAntagonism(
                            AntagonismLayer.Default,
                            AntagonismType.To,
                            hunter.Faction,
                            Antagonism.Hostile),
                        new UntilIdle(guard),
                        guard);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsInCombat(NpcElement npc)
        {
            return npc != null
                && !npc.HasBeenDiscarded
                && npc.NpcAI != null
                && npc.NpcAI.InCombat;
        }

        private static bool HasExactHunterTarget(
            NpcElement guard,
            NpcElement hunter)
        {
            return IsInCombat(guard)
                && ReferenceEquals(guard.GetCurrentTarget(), hunter);
        }

        private static bool IsUsableHero(Hero hero)
        {
            return hero != null
                && !hero.HasBeenDiscarded
                && hero.MainViewInitialized
                && hero.IsAlive
                && !hero.IsDying;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int CompareDistanceToHero(
            NpcCandidate left,
            NpcCandidate right)
        {
            return left.DistanceToHero.CompareTo(right.DistanceToHero);
        }

        private void ReportNoEligibleGuard(float radiusMeters, int hunterCount)
        {
            if (_reportedNoEligibleGuard)
            {
                return;
            }
            _reportedNoEligibleGuard = true;
            LogDebug(
                "Guard awareness found "
                + hunterCount
                + " exact official-hunt member(s), but no eligible ordinary "
                + "guard within "
                + radiusMeters.ToString("0.0")
                + "m.");
        }

        private void ReportCapReached(int maximumGuards)
        {
            if (_reportedCapReached)
            {
                return;
            }
            _reportedCapReached = true;
            LogDebug(
                "Guard awareness reached its official-hunt cap: "
                + _assistingGuards.Count
                + "/"
                + maximumGuards
                + " distinct guard(s).");
        }

        private void LogInfo(string message)
        {
            if (_log != null)
            {
                _log.LogInfo(message);
            }
        }

        private void LogDebug(string message)
        {
            if (_log != null)
            {
                _log.LogDebug(message);
            }
        }

        private void LogWarning(string message)
        {
            if (_log != null)
            {
                _log.LogWarning(message);
            }
        }

        private sealed class NpcCandidate
        {
            public readonly Location Location;
            public readonly NpcElement Npc;
            public readonly string TemplateName;
            public readonly float DistanceToHero;

            public NpcCandidate(
                Location location,
                NpcElement npc,
                string templateName,
                float distanceToHero)
            {
                Location = location;
                Npc = npc;
                TemplateName = templateName ?? string.Empty;
                DistanceToHero = distanceToHero;
            }

            public string Describe()
            {
                return TemplateName
                    + ", location="
                    + (string.IsNullOrWhiteSpace(Location.ID)
                        ? "unknown"
                        : Location.ID);
            }
        }

        private sealed class GuardHunterPair : IEquatable<GuardHunterPair>
        {
            private readonly NpcElement _guard;
            private readonly NpcElement _hunter;

            public GuardHunterPair(NpcElement guard, NpcElement hunter)
            {
                _guard = guard;
                _hunter = hunter;
            }

            public bool Equals(GuardHunterPair other)
            {
                return other != null
                    && ReferenceEquals(_guard, other._guard)
                    && ReferenceEquals(_hunter, other._hunter);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as GuardHunterPair);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_guard == null ? 0 : _guard.GetHashCode()) * 397)
                        ^ (_hunter == null ? 0 : _hunter.GetHashCode());
                }
            }
        }
    }
}
