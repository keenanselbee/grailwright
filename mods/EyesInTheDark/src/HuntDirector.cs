using System;

namespace EyesInTheDark
{
    internal enum HuntDirectiveKind
    {
        None,
        WarningCommitted,
        RequestPlacement,
        WarningCancelled,
        RecoveryEnded
    }

    internal enum HuntResolution
    {
        None,
        HunterKilled,
        Escaped,
        InteriorEscape,
        Dawn,
        PlayerDeath,
        GameplayLoad,
        LostTarget,
        PlacementFailed
    }

    internal struct HuntTuning
    {
        public float BaseHazardPerMinute;
        public float ThreatHazardPerMinute;
        public float NightProgressHazardPerMinute;
        public float MinimumHazardTarget;
        public float MaximumHazardTarget;
        public float WarningSeconds;
        public float KillRecoverySeconds;
        public float EscapeRecoverySeconds;
        public float FailedPlacementRecoverySeconds;
        public float HunterDangerCost;
    }

    internal struct HuntFrame
    {
        public bool IsValidWyrdNight;
        public bool IsExposed;
        public bool IsProtected;
        public bool HeroInUnrelatedCombat;
        public bool CanAdvance;
        public float ActiveSeconds;
        public float Threat;
        public float NightProgress;
        public float RemainingDangerBudget;
    }

    internal struct HuntDirective
    {
        public readonly HuntDirectiveKind Kind;
        public readonly DirectorState PreviousState;
        public readonly DirectorState CurrentState;
        public readonly float Pressure;
        public readonly float Target;
        public readonly string Reason;

        public HuntDirective(
            HuntDirectiveKind kind,
            DirectorState previousState,
            DirectorState currentState,
            float pressure,
            float target,
            string reason)
        {
            Kind = kind;
            PreviousState = previousState;
            CurrentState = currentState;
            Pressure = pressure;
            Target = target;
            Reason = reason ?? string.Empty;
        }
    }

    internal sealed class HuntDirector
    {
        private readonly Random _random;
        private bool _placementRequested;

        public DirectorState State { get; private set; }
        public float HazardPressure { get; private set; }
        public float HazardTarget { get; private set; }
        public float WarningRemainingSeconds { get; private set; }
        public float RecoveryRemainingSeconds { get; private set; }
        public HuntResolution LastResolution { get; private set; }

        public HuntDirector(int seed)
        {
            _random = new Random(seed);
            State = DirectorState.Roaming;
        }

        public HuntDirective Tick(HuntFrame frame, HuntTuning tuning)
        {
            EnsureTarget(tuning);

            if (State == DirectorState.ActiveHunt)
            {
                return None();
            }

            float activeSeconds = frame.CanAdvance
                ? FiniteNonNegative(frame.ActiveSeconds)
                : 0f;
            if (State == DirectorState.Recovery)
            {
                if (activeSeconds <= 0f)
                {
                    return None();
                }

                RecoveryRemainingSeconds = Math.Max(
                    0f,
                    RecoveryRemainingSeconds - activeSeconds);
                if (RecoveryRemainingSeconds > 0f)
                {
                    return None();
                }

                DirectorState previous = State;
                State = DirectorState.Roaming;
                LastResolution = HuntResolution.None;
                return Result(
                    HuntDirectiveKind.RecoveryEnded,
                    previous,
                    "recovery elapsed");
            }

            bool eligible = frame.IsValidWyrdNight
                && frame.IsExposed
                && !frame.IsProtected
                && !frame.HeroInUnrelatedCombat
                && frame.RemainingDangerBudget
                    >= FiniteNonNegative(tuning.HunterDangerCost);

            if (State == DirectorState.Warning)
            {
                if (!eligible)
                {
                    DirectorState previous = State;
                    State = DirectorState.Roaming;
                    _placementRequested = false;
                    WarningRemainingSeconds = 0f;
                    HazardPressure = Math.Min(
                        HazardPressure,
                        HazardTarget * 0.5f);
                    ResetTarget(tuning);
                    return Result(
                        HuntDirectiveKind.WarningCancelled,
                        previous,
                        DescribeIneligible(frame, tuning));
                }

                if (!_placementRequested && activeSeconds > 0f)
                {
                    WarningRemainingSeconds = Math.Max(
                        0f,
                        WarningRemainingSeconds - activeSeconds);
                    if (WarningRemainingSeconds <= 0f)
                    {
                        _placementRequested = true;
                        return Result(
                            HuntDirectiveKind.RequestPlacement,
                            State,
                            "warning elapsed");
                    }
                }

                return None();
            }

            if (!eligible || activeSeconds <= 0f)
            {
                return None();
            }

            float threat = Clamp01(frame.Threat / 100f);
            float progress = Clamp01(frame.NightProgress);
            float perMinute = FiniteNonNegative(
                tuning.BaseHazardPerMinute)
                + FiniteNonNegative(tuning.ThreatHazardPerMinute)
                    * (float)Math.Pow(threat, 1.5d)
                + FiniteNonNegative(
                    tuning.NightProgressHazardPerMinute)
                    * progress;
            HazardPressure += perMinute * activeSeconds / 60f;
            if (HazardPressure < HazardTarget)
            {
                return None();
            }

            DirectorState oldState = State;
            State = DirectorState.Warning;
            WarningRemainingSeconds = Math.Max(
                0f,
                FiniteNonNegative(tuning.WarningSeconds));
            _placementRequested = false;
            return Result(
                HuntDirectiveKind.WarningCommitted,
                oldState,
                "hazard target reached");
        }

        public void ConfirmPlacement()
        {
            State = DirectorState.ActiveHunt;
            WarningRemainingSeconds = 0f;
            _placementRequested = false;
        }

        public void FailPlacement(HuntTuning tuning)
        {
            State = DirectorState.Recovery;
            HazardPressure = 0f;
            WarningRemainingSeconds = 0f;
            RecoveryRemainingSeconds = FiniteNonNegative(
                tuning.FailedPlacementRecoverySeconds);
            LastResolution = HuntResolution.PlacementFailed;
            _placementRequested = false;
            ResetTarget(tuning);
        }

        public void Resolve(
            HuntResolution resolution,
            HuntTuning tuning)
        {
            HazardPressure = 0f;
            WarningRemainingSeconds = 0f;
            _placementRequested = false;
            LastResolution = resolution;
            ResetTarget(tuning);

            if (resolution == HuntResolution.Dawn
                || resolution == HuntResolution.GameplayLoad
                || resolution == HuntResolution.PlayerDeath)
            {
                State = DirectorState.Roaming;
                RecoveryRemainingSeconds = 0f;
                return;
            }

            State = DirectorState.Recovery;
            RecoveryRemainingSeconds = resolution
                    == HuntResolution.HunterKilled
                ? FiniteNonNegative(tuning.KillRecoverySeconds)
                : resolution == HuntResolution.Escaped
                    || resolution == HuntResolution.InteriorEscape
                    ? FiniteNonNegative(tuning.EscapeRecoverySeconds)
                    : FiniteNonNegative(
                        tuning.FailedPlacementRecoverySeconds);
        }

        public void ResetNight(HuntTuning tuning)
        {
            State = DirectorState.Roaming;
            HazardPressure = 0f;
            WarningRemainingSeconds = 0f;
            RecoveryRemainingSeconds = 0f;
            LastResolution = HuntResolution.None;
            _placementRequested = false;
            ResetTarget(tuning);
        }

        private void EnsureTarget(HuntTuning tuning)
        {
            if (HazardTarget <= 0f)
            {
                ResetTarget(tuning);
            }
        }

        private void ResetTarget(HuntTuning tuning)
        {
            float minimum = Math.Max(
                0.01f,
                FiniteNonNegative(tuning.MinimumHazardTarget));
            float maximum = Math.Max(
                minimum,
                FiniteNonNegative(tuning.MaximumHazardTarget));
            HazardTarget = minimum
                + (float)_random.NextDouble() * (maximum - minimum);
        }

        private HuntDirective None()
        {
            return Result(
                HuntDirectiveKind.None,
                State,
                string.Empty);
        }

        private HuntDirective Result(
            HuntDirectiveKind kind,
            DirectorState previous,
            string reason)
        {
            return new HuntDirective(
                kind,
                previous,
                State,
                HazardPressure,
                HazardTarget,
                reason);
        }

        private static string DescribeIneligible(
            HuntFrame frame,
            HuntTuning tuning)
        {
            if (!frame.IsValidWyrdNight)
            {
                return "invalid Wyrdnight state";
            }
            if (frame.IsProtected)
            {
                return "protected area";
            }
            if (!frame.IsExposed)
            {
                return "not exposed";
            }
            if (frame.HeroInUnrelatedCombat)
            {
                return "unrelated combat";
            }
            if (frame.RemainingDangerBudget
                < FiniteNonNegative(tuning.HunterDangerCost))
            {
                return "danger budget exhausted";
            }
            return "unknown eligibility failure";
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }
            return float.IsInfinity(value) || value >= 1f
                ? 1f
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
}
