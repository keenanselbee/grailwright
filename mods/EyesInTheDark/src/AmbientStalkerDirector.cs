using System;
using System.Collections.Generic;
using System.Globalization;

namespace EyesInTheDark
{
    internal enum AmbientStalkerBand
    {
        Ordinary,
        HighPressure
    }

    internal enum AmbientStalkerDirectiveKind
    {
        None,
        RequestPlacement
    }

    internal enum AmbientMovementMode
    {
        Observe,
        Follow,
        Flee,
        Hostile
    }

    internal enum AmbientStalkerEscalationCause
    {
        None,
        HiddenThreat,
        HeroAttack,
        ClosePursuit
    }

    internal struct AmbientStalkerTuning
    {
        public bool Enabled;
        public float MinimumCooldownSeconds;
        public float MaximumCooldownSeconds;
        public float MaximumCooldownAtFiftyThreatSeconds;
    }

    internal struct AmbientStalkerFrame
    {
        public bool IsValidWyrdNight;
        public bool IsExposed;
        public bool IsProtected;
        public bool HeroInCombat;
        public bool OfficialEncounterLaneBusy;
        public bool RuntimeBusy;
        public bool AllowHighPressure;
        public bool CanAdvance;
        public float ActiveSeconds;
        public float Threat;
    }

    internal struct AmbientStalkerDirective
    {
        public readonly AmbientStalkerDirectiveKind Kind;
        public readonly float CooldownRemainingSeconds;
        public readonly string Reason;

        public AmbientStalkerDirective(
            AmbientStalkerDirectiveKind kind,
            float cooldownRemainingSeconds,
            string reason)
        {
            Kind = kind;
            CooldownRemainingSeconds = cooldownRemainingSeconds;
            Reason = reason ?? string.Empty;
        }
    }

    internal static class AmbientStalkerPolicy
    {
        public const float OrdinaryThreatCeiling = 50f;
        public const float HighPressureThreatCeiling = 75f;
        public const float OrdinaryAggressionMinimum = 45f;
        public const float OrdinaryAggressionMaximum = 55f;
        public const float HighPressureAggressionMinimum = 70f;
        public const float HighPressureAggressionMaximum = 80f;
        public const float ClosePursuitAggressionDistance = 8f;

        public static bool TryResolveBand(
            float threat,
            bool allowHighPressure,
            out AmbientStalkerBand band)
        {
            float value = FiniteThreat(threat);
            if (value < OrdinaryThreatCeiling)
            {
                band = AmbientStalkerBand.Ordinary;
                return true;
            }
            if (value < HighPressureThreatCeiling
                && allowHighPressure)
            {
                band = AmbientStalkerBand.HighPressure;
                return true;
            }

            band = AmbientStalkerBand.Ordinary;
            return false;
        }

        public static float CooldownUpperBound(
            float threat,
            AmbientStalkerTuning tuning)
        {
            float minimum = FiniteNonNegative(
                tuning.MinimumCooldownSeconds);
            float atZero = Math.Max(
                minimum,
                FiniteNonNegative(tuning.MaximumCooldownSeconds));
            float atFifty = Math.Max(
                minimum,
                FiniteNonNegative(
                    tuning.MaximumCooldownAtFiftyThreatSeconds));
            float progress = Math.Min(
                1f,
                FiniteThreat(threat) / OrdinaryThreatCeiling);
            return atZero + (atFifty - atZero) * progress;
        }

        public static float AggressionThreshold(
            AmbientStalkerBand band,
            double randomUnit)
        {
            double unit = double.IsNaN(randomUnit)
                || double.IsInfinity(randomUnit)
                    ? 0d
                    : Math.Max(0d, Math.Min(1d, randomUnit));
            float minimum = band == AmbientStalkerBand.HighPressure
                ? HighPressureAggressionMinimum
                : OrdinaryAggressionMinimum;
            float maximum = band == AmbientStalkerBand.HighPressure
                ? HighPressureAggressionMaximum
                : OrdinaryAggressionMaximum;
            return minimum + (float)unit * (maximum - minimum);
        }

        public static float RollAggressionThreshold(
            AmbientStalkerBand band,
            double randomUnit)
        {
            return AggressionThreshold(band, randomUnit);
        }

        public static bool ShouldFleeFromApproach(
            float distanceMeters,
            float heroFacingDot,
            float heroSpeed,
            float distanceClosed,
            float sampleSeconds)
        {
            return distanceMeters <= 38f
                && heroFacingDot >= 0.55f
                && heroSpeed >= 2.5f
                && distanceClosed >= 0.15f
                && sampleSeconds >= 0.4f;
        }

        public static AmbientMovementMode NextPassiveMovementMode(
            AmbientMovementMode current,
            bool pursued,
            bool fleeComplete,
            bool observeElapsed)
        {
            if (pursued)
            {
                return AmbientMovementMode.Flee;
            }
            if (current == AmbientMovementMode.Flee)
            {
                return fleeComplete
                    ? AmbientMovementMode.Observe
                    : AmbientMovementMode.Flee;
            }
            if (current == AmbientMovementMode.Observe)
            {
                return observeElapsed
                    ? AmbientMovementMode.Follow
                    : AmbientMovementMode.Observe;
            }
            return current == AmbientMovementMode.Follow
                ? AmbientMovementMode.Follow
                : AmbientMovementMode.Observe;
        }

        public static bool IsViewportPointVisible(
            float x,
            float y,
            float z,
            float margin)
        {
            float safeMargin = Math.Max(
                0f,
                Math.Min(0.25f, margin));
            return z > 0f
                && x >= -safeMargin
                && x <= 1f + safeMargin
                && y >= -safeMargin
                && y <= 1f + safeMargin;
        }

        public static bool CanPassivelyDespawn(
            bool hostile,
            bool currentlyVisible,
            float continuousOffscreenSeconds,
            float requiredOffscreenSeconds,
            float distanceMeters,
            float minimumDistanceMeters,
            bool wasSeen,
            bool lifetimeExpired)
        {
            return !hostile
                && !currentlyVisible
                && continuousOffscreenSeconds
                    >= Math.Max(0.5f, requiredOffscreenSeconds)
                && distanceMeters >= Math.Max(0f, minimumDistanceMeters)
                && (wasSeen || lifetimeExpired);
        }

        public static bool ShouldEscalate(
            float threat,
            float aggressionThreshold,
            bool exactHeroDamage,
            bool alreadyHostile)
        {
            return !alreadyHostile
                && (exactHeroDamage
                    || FiniteThreat(threat)
                        >= FiniteThreat(aggressionThreshold));
        }

        public static bool ShouldEscalateFromClosePursuit(
            AmbientMovementMode movementMode,
            float distanceMeters)
        {
            return movementMode == AmbientMovementMode.Flee
                && !float.IsNaN(distanceMeters)
                && distanceMeters >= 0f
                && distanceMeters
                    <= ClosePursuitAggressionDistance;
        }

        public static bool IsDeliberatePursuit(
            float distanceMeters,
            float previousDistanceMeters,
            float heroHorizontalSpeed,
            float heroFacingDot,
            float accumulatedPursuitSeconds,
            float activeSeconds)
        {
            bool closing = previousDistanceMeters > 0f
                && previousDistanceMeters - distanceMeters >= 0.15f;
            bool candidate = distanceMeters <= 38f
                && heroHorizontalSpeed >= 2.5f
                && heroFacingDot >= 0.55f
                && closing;
            float duration = candidate
                ? Math.Max(0f, accumulatedPursuitSeconds)
                    + Math.Max(0f, activeSeconds)
                : 0f;
            return duration >= 0.4f;
        }

        private static float FiniteThreat(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }
            return float.IsInfinity(value) || value >= 100f
                ? 100f
                : value;
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

    internal sealed class AmbientStalkerDirector
    {
        private readonly Random _random;
        private bool _cooldownScheduled;
        private bool _placementRequested;

        public float CooldownRemainingSeconds { get; private set; }

        public AmbientStalkerDirector(int seed)
        {
            _random = new Random(seed);
        }

        public AmbientStalkerDirective Tick(
            AmbientStalkerFrame frame,
            AmbientStalkerTuning tuning)
        {
            if (!tuning.Enabled)
            {
                return None("disabled");
            }
            if (_placementRequested || frame.RuntimeBusy)
            {
                return None("ambient runtime busy");
            }
            if (frame.OfficialEncounterLaneBusy)
            {
                return None("official encounter lane busy");
            }
            if (!frame.IsValidWyrdNight
                || !frame.IsExposed
                || frame.IsProtected
                || frame.HeroInCombat)
            {
                return None("ambient eligibility paused");
            }

            AmbientStalkerBand ignored;
            if (!AmbientStalkerPolicy.TryResolveBand(
                frame.Threat,
                frame.AllowHighPressure,
                out ignored))
            {
                return None("no ambient threat band");
            }

            if (!_cooldownScheduled)
            {
                Schedule(frame.Threat, tuning);
            }
            if (!frame.CanAdvance || frame.ActiveSeconds <= 0f)
            {
                return None("active clock paused");
            }

            CooldownRemainingSeconds = Math.Min(
                CooldownRemainingSeconds,
                AmbientStalkerPolicy.CooldownUpperBound(
                    frame.Threat,
                    tuning));
            CooldownRemainingSeconds = Math.Max(
                0f,
                CooldownRemainingSeconds
                    - FiniteNonNegative(frame.ActiveSeconds));
            if (CooldownRemainingSeconds > 0f)
            {
                return None("cooldown");
            }

            _placementRequested = true;
            return new AmbientStalkerDirective(
                AmbientStalkerDirectiveKind.RequestPlacement,
                0f,
                "randomized ambient cooldown elapsed");
        }

        public void ConfirmPlacement()
        {
            _placementRequested = false;
            _cooldownScheduled = false;
            CooldownRemainingSeconds = 0f;
        }

        public void FailPlacement(
            float threat,
            AmbientStalkerTuning tuning)
        {
            _placementRequested = false;
            Schedule(threat, tuning);
        }

        public void Resolve(
            float threat,
            AmbientStalkerTuning tuning)
        {
            _placementRequested = false;
            Schedule(threat, tuning);
        }

        public void ResetNight()
        {
            _placementRequested = false;
            _cooldownScheduled = false;
            CooldownRemainingSeconds = 0f;
        }

        private void Schedule(
            float threat,
            AmbientStalkerTuning tuning)
        {
            float minimum = FiniteNonNegative(
                tuning.MinimumCooldownSeconds);
            float maximum = Math.Max(
                minimum,
                AmbientStalkerPolicy.CooldownUpperBound(
                    threat,
                    tuning));
            CooldownRemainingSeconds = minimum
                + (float)_random.NextDouble() * (maximum - minimum);
            _cooldownScheduled = true;
        }

        private AmbientStalkerDirective None(string reason)
        {
            return new AmbientStalkerDirective(
                AmbientStalkerDirectiveKind.None,
                CooldownRemainingSeconds,
                reason);
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

    internal sealed class AmbientStalkerProfile
    {
        public readonly string Id;
        public readonly string TemplateGuid;
        public readonly string TemplateName;
        public readonly string DisplayName;
        public readonly HunterFamily Family;
        public readonly HuntRegion Region;
        public readonly int MinimumPlayerLevel;
        public readonly float Weight;
        public readonly AmbientStalkerBand Band;
        public readonly bool IsUniversal;

        public AmbientStalkerProfile(
            string id,
            string templateGuid,
            string templateName,
            string displayName,
            HunterFamily family,
            HuntRegion region,
            int minimumPlayerLevel,
            float weight,
            AmbientStalkerBand band,
            bool isUniversal = false)
        {
            Id = id;
            TemplateGuid = templateGuid;
            TemplateName = templateName;
            DisplayName = displayName;
            Family = family;
            Region = region;
            MinimumPlayerLevel = minimumPlayerLevel;
            Weight = weight;
            Band = band;
            IsUniversal = isUniversal;
        }
    }

    internal struct AmbientStalkerSelectionContext
    {
        public HuntRegion Region;
        public int PlayerLevel;
        public float Threat;
        public bool AllowHighPressure;
    }

    internal sealed class AmbientStalkerSelection
    {
        public readonly AmbientStalkerProfile Profile;
        public readonly float AggressionThreshold;
        public readonly string FilterSummary;
        public readonly string WeightSummary;
        public readonly string Reason;

        public bool Success
        {
            get { return Profile != null; }
        }

        public AmbientStalkerSelection(
            AmbientStalkerProfile profile,
            float aggressionThreshold,
            string filterSummary,
            string weightSummary,
            string reason)
        {
            Profile = profile;
            AggressionThreshold = aggressionThreshold;
            FilterSummary = filterSummary ?? string.Empty;
            WeightSummary = weightSummary ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }

    internal sealed class AmbientStalkerCatalogDirector
    {
        private const int FailureRejectionCount = 3;
        private const int HistoryLimit = 4;

        private static readonly AmbientStalkerProfile[] Profiles =
        {
            Ordinary("wyrdspirit-stalker", "843643575fa01ba4292e60afb9291fea", "Spec_EnemyMonster_T1_Wyrdspirit", "Wyrdspirit", HunterFamily.Wyrdspirit, HuntRegion.Unknown, 1, 1.2f, true),

            Ordinary("grindylow-stalker-hos", "fa79aaa0bff59484dab2cf35c5ea805c", "Spec_EnemyMonster_T1_Grindylow", "Grindylow", HunterFamily.Grindylow, HuntRegion.HornsOfTheSouth, 5, 1f),
            Ordinary("redcap-stalker-hos", "a32e5074492cce34f89ff0667fdb41b7", "Spec_EnemyMonster_T1_Redcap", "Redcap", HunterFamily.Redcap, HuntRegion.HornsOfTheSouth, 4, 1.1f),
            Ordinary("corpse-eater-stalker-hos", "1a41678c288c2264c8bcfad7a6eb3ba3", "Spec_EnemyMonster_T1_CorpseEater", "Corpse Eater", HunterFamily.CorpseEater, HuntRegion.HornsOfTheSouth, 7, 0.9f),
            Ordinary("mistling-stalker-hos", "db4a2490470378f49be51ff8848541e9", "Spec_EnemyMonster_T2_Mistling_Hos", "Mistling", HunterFamily.Mistling, HuntRegion.HornsOfTheSouth, 10, 1f),
            Ordinary("drowner-stalker-hos", "bb613531c5d3bf5499ea3b8103a4024e", "Spec_EnemyZombie_T1_Drowner", "Drowner", HunterFamily.Zombie, HuntRegion.HornsOfTheSouth, 7, 0.75f),

            Ordinary("grindylow-stalker-cuanacht", "1b0f005502932a54cbc99e4376837125", "Spec_EnemyMonster_T3_Grindylow_Cuanacht", "Cuanacht Grindylow", HunterFamily.Grindylow, HuntRegion.Cuanacht, 15, 1f),
            Ordinary("redcap-stalker-cuanacht", "2f0d374f6ac405648adc3b610d305a61", "Spec_EnemyMonster_T3_Redcap_Cuanacht", "Cuanacht Redcap", HunterFamily.Redcap, HuntRegion.Cuanacht, 15, 1.1f),
            Ordinary("corpse-eater-stalker-cuanacht", "ec6ee283175f87240b6f292697bc9d9c", "Spec_EnemyMonster_T3_CorpseEater_Cuanacht", "Cuanacht Corpse Eater", HunterFamily.CorpseEater, HuntRegion.Cuanacht, 15, 0.9f),
            Ordinary("mistling-stalker-cuanacht", "f673e122b6f7e984fab5758d91f84031", "Spec_EnemyMonster_T3_Mistling_Cuanacht", "Cuanacht Mistling", HunterFamily.Mistling, HuntRegion.Cuanacht, 20, 1f),
            Ordinary("slugholder-stalker-cuanacht", "169a81f342550d245abea12ab926bb49", "Spec_EnemyMonster_T3_SlugholderMage", "Slugholder Mage", HunterFamily.Slugholder, HuntRegion.Cuanacht, 20, 0.75f),
            Ordinary("drowner-stalker-cuanacht", "66f8b8c379a0b64449781232dcbebf70", "Spec_EnemyZombie_T3_DrownerCuanacht", "Cuanacht Drowner", HunterFamily.Zombie, HuntRegion.Cuanacht, 20, 0.7f),

            Ordinary("redcap-stalker-forlorn", "47a85f2bc9a369a488a454d70435caac", "Spec_EnemyMonster_T4_Redcap_Forlorn", "Forlorn Redcap", HunterFamily.Redcap, HuntRegion.Forlorn, 25, 1.1f),
            Ordinary("mistling-stalker-forlorn", "6f7fcf075b9e8f64495fc893f853bceb", "Spec_EnemyMonster_T4_Mistling_Forlorn", "Forlorn Mistling", HunterFamily.Mistling, HuntRegion.Forlorn, 30, 1f),
            Ordinary("bonemask-mage-stalker-forlorn", "32ca9a1e4c52a2644bd1cfb2bfdeaba1", "Spec_EnemyMonster_T4_Bonemask_Mage", "Bonemask Mage", HunterFamily.Bonemask, HuntRegion.Forlorn, 30, 0.85f),
            Ordinary("bonemask-melee-stalker-forlorn", "15fa95ee39d224a47be5c17d489ecbb2", "Spec_EnemyMonster_T4_Bonemask_Melee", "Bonemask Warrior", HunterFamily.Bonemask, HuntRegion.Forlorn, 30, 0.85f),
            Ordinary("corpse-eater-stalker-forlorn", "dfd5303226380e34f8ed8a59db6da5fa", "Spec_EnemyMonster_T5_CorpseEater_Forlorn", "Forlorn Corpse Eater", HunterFamily.CorpseEater, HuntRegion.Forlorn, 40, 0.75f),
            Ordinary("frostbitten-stalker-forlorn", "925680acc37514c4086622e71fa3c13a", "Spec_EnemyMonster_T5_FrostbittenWarrior_Male", "Frostbitten Warrior", HunterFamily.Frostbitten, HuntRegion.Forlorn, 40, 0.7f),

            Ordinary("drowner-stalker-sarras", "bf9643d310c0076468095f825960adc1", "Spec_SoS_EnemyZombie_T3_Drowner", "Sarras Drowner", HunterFamily.Drowned, HuntRegion.Sarras, 25, 0.8f),
            Ordinary("deckhand-stalker-sarras", "c446b238e2f87d34ebb56bd87ce6a8b2", "Spec_SoS_EnemyMonster_T4_DrownedDeckhand", "Drowned Deckhand", HunterFamily.Drowned, HuntRegion.Sarras, 28, 0.85f),
            Ordinary("mariner-stalker-sarras", "629e018fb6cd5c04d880ae6cb4b8bc12", "Spec_SoS_EnemyMonster_T4_DrownedMariner", "Drowned Mariner", HunterFamily.Drowned, HuntRegion.Sarras, 28, 0.75f),
            Ordinary("finbled-light-stalker-sarras", "9257f74ad720b4d4cab2fad445d6eabb", "Spec_SoS_EnemyMonster_T4_Finbled_Light", "Finbled Stalker", HunterFamily.Finbled, HuntRegion.Sarras, 30, 1.1f),
            Ordinary("finbled-javelin-stalker-sarras", "2ae4e298453780f488898d1c8efa40ae", "Spec_SoS_EnemyMonster_T4_Finbled_JavelinThrower", "Finbled Javelin Hunter", HunterFamily.Finbled, HuntRegion.Sarras, 30, 0.8f),
            Ordinary("tadpole-stalker-sarras", "bceaf319958bbf54e8ddb1a4ebda2010", "Spec_SoS_EnemyMonster_T4_Tadpole", "Tadpole", HunterFamily.Tadpole, HuntRegion.Sarras, 30, 0.9f),
            Ordinary("wailcap-stalker-sarras", "9140bd847e233604288a5a66d9d12c3b", "Spec_SoS_EnemyMonster_T4_Wailcap", "Wailcap", HunterFamily.Wailcap, HuntRegion.Sarras, 30, 0.85f),
            Ordinary("tidewraith-stalker-sarras", "f7aca4c4aa9722844977c33da5ad55f1", "Spec_SoS_EnemyMonster_T5_Tidewraith", "Tidewraith", HunterFamily.Tidewraith, HuntRegion.Sarras, 30, 0.75f),

            HighPressure("sharg-stalker-hos", "324e9b5ed131ce34eb12a520cdb2b52a", "Spec_EnemyMonster_T2_ShargHoS", "Sharg", HunterFamily.Sharg, HuntRegion.HornsOfTheSouth, 15, 1f),
            HighPressure("lost-knight-stalker-cuanacht", "4b7066c81a33ff94fb304721a5bc306d", "Spec_EnemyMonster_T3_LostKnight", "Cuanacht Lost Knight", HunterFamily.LostKnight, HuntRegion.Cuanacht, 20, 1.1f),
            HighPressure("sharg-stalker-cuanacht", "a6893b6bbb474aa4aa359fc1cfab3aa8", "Spec_EnemyMonster_T4_ShargCuanacht", "Cuanacht Sharg", HunterFamily.Sharg, HuntRegion.Cuanacht, 30, 0.9f),
            HighPressure("sharg-stalker-forlorn", "4186b8fdcf380fe42981626cb6676927", "Spec_EnemyMonster_T5_ShargSmallerForlorn", "Forlorn Sharg", HunterFamily.Sharg, HuntRegion.Forlorn, 40, 1f),
            HighPressure("finbled-heavy-stalker-sarras", "1c3007f0399936747a3a46080220678c", "Spec_SoS_EnemyMonster_T4_Finbled_Heavy", "Finbled Heavy", HunterFamily.Finbled, HuntRegion.Sarras, 30, 1f),
            HighPressure("drowned-knight-stalker-sarras", "1aa9c02f06e33f140ba8dcdfd8969f65", "Spec_SoS_EnemyMonster_T6_DrownedKnight", "Drowned Knight", HunterFamily.Drowned, HuntRegion.Sarras, 35, 1f),
            HighPressure("drowned-huntress-stalker-sarras", "0babf25cb7633f848b7a3d926ca5b988", "Spec_SoS_EnemyMonster_T6_DrownedKnight_Female", "Drowned Knight Huntress", HunterFamily.Drowned, HuntRegion.Sarras, 35, 1f)
        };

        private readonly Random _random;
        private readonly List<string> _recentProfiles =
            new List<string>();
        private readonly List<HunterFamily> _recentFamilies =
            new List<HunterFamily>();
        private readonly Dictionary<string, int> _failureCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public AmbientStalkerCatalogDirector(int seed)
        {
            _random = new Random(seed);
        }

        public AmbientStalkerSelection Select(
            AmbientStalkerSelectionContext context)
        {
            if (context.Region == HuntRegion.Unknown)
            {
                return Failure("unknown region", "region=unknown");
            }

            AmbientStalkerBand band;
            if (!AmbientStalkerPolicy.TryResolveBand(
                context.Threat,
                context.AllowHighPressure,
                out band))
            {
                return Failure(
                    "no ambient stalker band is active",
                    context.Threat >= 75f
                        ? "threat>=75"
                        : "high-pressure-disabled");
            }

            List<WeightedProfile> pool = new List<WeightedProfile>();
            int regionRejected = 0;
            int levelRejected = 0;
            int bandRejected = 0;
            int sessionRejected = 0;
            for (int index = 0; index < Profiles.Length; index++)
            {
                AmbientStalkerProfile profile = Profiles[index];
                if (profile.Band != band)
                {
                    bandRejected++;
                    continue;
                }
                if (!profile.IsUniversal && profile.Region != context.Region)
                {
                    regionRejected++;
                    continue;
                }
                if (context.PlayerLevel < profile.MinimumPlayerLevel)
                {
                    levelRejected++;
                    continue;
                }
                if (IsSessionRejected(profile.Id))
                {
                    sessionRejected++;
                    continue;
                }

                float weight = Math.Max(0.01f, profile.Weight);
                if (_recentProfiles.Contains(profile.Id))
                {
                    weight *= 0.2f;
                }
                if (_recentFamilies.Contains(profile.Family))
                {
                    weight *= 0.45f;
                }
                pool.Add(new WeightedProfile(profile, weight));
            }

            string filters = "region="
                + regionRejected
                + ",level="
                + levelRejected
                + ",band="
                + bandRejected
                + ",session="
                + sessionRejected;
            if (pool.Count == 0)
            {
                return Failure("regional stalker pool was empty", filters);
            }

            AmbientStalkerProfile selected = Choose(pool);
            return new AmbientStalkerSelection(
                selected,
                AmbientStalkerPolicy.AggressionThreshold(
                    band,
                    _random.NextDouble()),
                filters,
                DescribeWeights(pool),
                string.Empty);
        }

        public void RecordConfirmed(AmbientStalkerProfile profile)
        {
            if (profile == null)
            {
                return;
            }
            _failureCounts.Remove(profile.Id);
            Push(_recentProfiles, profile.Id);
            Push(_recentFamilies, profile.Family);
        }

        public void RecordFailure(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return;
            }
            int count;
            _failureCounts.TryGetValue(profileId, out count);
            _failureCounts[profileId] = count + 1;
        }

        public bool IsSessionRejected(string profileId)
        {
            int count;
            return !string.IsNullOrWhiteSpace(profileId)
                && _failureCounts.TryGetValue(profileId, out count)
                && count >= FailureRejectionCount;
        }

        private AmbientStalkerProfile Choose(
            List<WeightedProfile> pool)
        {
            float total = 0f;
            for (int index = 0; index < pool.Count; index++)
            {
                total += pool[index].Weight;
            }
            float roll = (float)_random.NextDouble() * total;
            for (int index = 0; index < pool.Count; index++)
            {
                roll -= pool[index].Weight;
                if (roll <= 0f)
                {
                    return pool[index].Profile;
                }
            }
            return pool[pool.Count - 1].Profile;
        }

        private static string DescribeWeights(
            List<WeightedProfile> pool)
        {
            string[] values = new string[Math.Min(6, pool.Count)];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = pool[index].Profile.Id
                    + "="
                    + pool[index].Weight.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture);
            }
            return string.Join(",", values)
                + (pool.Count > values.Length ? ",..." : string.Empty);
        }

        private static AmbientStalkerSelection Failure(
            string reason,
            string filters)
        {
            return new AmbientStalkerSelection(
                null,
                0f,
                filters,
                string.Empty,
                reason);
        }

        private static void Push<T>(List<T> list, T value)
        {
            list.Insert(0, value);
            while (list.Count > HistoryLimit)
            {
                list.RemoveAt(list.Count - 1);
            }
        }

        private static AmbientStalkerProfile Ordinary(
            string id,
            string guid,
            string template,
            string name,
            HunterFamily family,
            HuntRegion region,
            int level,
            float weight,
            bool universal = false)
        {
            return new AmbientStalkerProfile(
                id,
                guid,
                template,
                name,
                family,
                region,
                level,
                weight,
                AmbientStalkerBand.Ordinary,
                universal);
        }

        private static AmbientStalkerProfile HighPressure(
            string id,
            string guid,
            string template,
            string name,
            HunterFamily family,
            HuntRegion region,
            int level,
            float weight)
        {
            return new AmbientStalkerProfile(
                id,
                guid,
                template,
                name,
                family,
                region,
                level,
                weight,
                AmbientStalkerBand.HighPressure);
        }

        private sealed class WeightedProfile
        {
            public readonly AmbientStalkerProfile Profile;
            public readonly float Weight;

            public WeightedProfile(
                AmbientStalkerProfile profile,
                float weight)
            {
                Profile = profile;
                Weight = weight;
            }
        }
    }
}
