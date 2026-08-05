using System;

namespace EyesInTheDark
{
    internal enum DirectorState
    {
        Inactive,
        Roaming,
        Warning,
        ActiveHunt,
        Recovery
    }

    internal enum InactiveReason
    {
        None,
        NoPlayableHero,
        HeroDead,
        TitleScreen,
        Loading,
        Transition,
        Travel,
        Resting,
        SceneUnknown,
        SceneNotReady,
        WorldTimeUnknown,
        WyrdNightUnknown,
        NotOutdoor,
        WyrdNightNotAllowed,
        NightStateMismatch,
        Daylight
    }

    internal struct NightObservation
    {
        public bool HasPlayableHero;
        public bool HeroAlive;
        public bool AtTitleScreen;
        public bool IsLoading;
        public bool IsTransitioning;
        public bool IsTraveling;
        public bool IsResting;
        public bool SceneKnown;
        public bool SceneInitialized;
        public bool HasWorldTime;
        public bool HasHeroNightState;
        public bool IsOutdoor;
        public bool AllowsWyrdNight;
        public bool IsPrologue;
        public bool GameSaysNight;
        public bool HeroSaysNight;
    }

    internal struct NightStateDecision
    {
        public readonly DirectorState State;
        public readonly InactiveReason Reason;

        public NightStateDecision(DirectorState state, InactiveReason reason)
        {
            State = state;
            Reason = reason;
        }
    }

    internal static class NightStateEvaluator
    {
        public const float NightStartFraction = 0.92f;
        public const float NightEndFraction = 0.23f;

        public static NightStateDecision Evaluate(NightObservation observation)
        {
            if (!observation.HasPlayableHero)
            {
                return Inactive(InactiveReason.NoPlayableHero);
            }

            if (!observation.HeroAlive)
            {
                return Inactive(InactiveReason.HeroDead);
            }

            if (observation.AtTitleScreen)
            {
                return Inactive(InactiveReason.TitleScreen);
            }

            if (observation.IsLoading)
            {
                return Inactive(InactiveReason.Loading);
            }

            if (observation.IsTraveling)
            {
                return Inactive(InactiveReason.Travel);
            }

            if (observation.IsTransitioning)
            {
                return Inactive(InactiveReason.Transition);
            }

            if (observation.IsResting)
            {
                return Inactive(InactiveReason.Resting);
            }

            if (!observation.SceneKnown)
            {
                return Inactive(InactiveReason.SceneUnknown);
            }

            if (!observation.SceneInitialized)
            {
                return Inactive(InactiveReason.SceneNotReady);
            }

            if (!observation.HasWorldTime)
            {
                return Inactive(InactiveReason.WorldTimeUnknown);
            }

            if (!observation.HasHeroNightState)
            {
                return Inactive(InactiveReason.WyrdNightUnknown);
            }

            if (!observation.IsOutdoor)
            {
                return Inactive(InactiveReason.NotOutdoor);
            }

            if (!observation.AllowsWyrdNight || observation.IsPrologue)
            {
                return Inactive(InactiveReason.WyrdNightNotAllowed);
            }

            if (observation.GameSaysNight != observation.HeroSaysNight)
            {
                return Inactive(InactiveReason.NightStateMismatch);
            }

            if (!observation.GameSaysNight)
            {
                return Inactive(InactiveReason.Daylight);
            }

            return new NightStateDecision(DirectorState.Roaming, InactiveReason.None);
        }

        public static float NormalizeNightProgress(float dayFraction, bool isNight)
        {
            if (!isNight || float.IsNaN(dayFraction) || float.IsInfinity(dayFraction))
            {
                return 0f;
            }

            float elapsed = dayFraction > NightStartFraction
                ? dayFraction - NightStartFraction
                : (1f - NightStartFraction) + dayFraction;
            float duration = (1f - NightStartFraction) + NightEndFraction;
            float progress = elapsed / duration;

            if (progress < 0f)
            {
                return 0f;
            }

            return progress > 1f ? 1f : progress;
        }

        public static bool CanAdvanceActiveClock(NightObservation observation, bool isPaused)
        {
            return !isPaused
                && observation.HasPlayableHero
                && observation.HeroAlive
                && !observation.AtTitleScreen
                && !observation.IsLoading
                && !observation.IsTransitioning
                && !observation.IsTraveling
                && !observation.IsResting
                && observation.SceneKnown
                && observation.SceneInitialized;
        }

        public static bool IsActiveWyrdnightPhaseForRest(
            NightObservation observation)
        {
            return observation.HasPlayableHero
                && observation.HeroAlive
                && !observation.AtTitleScreen
                && !observation.IsLoading
                && !observation.IsTransitioning
                && !observation.IsTraveling
                && observation.SceneKnown
                && observation.SceneInitialized
                && observation.HasWorldTime
                && observation.HasHeroNightState
                && observation.IsOutdoor
                && observation.AllowsWyrdNight
                && !observation.IsPrologue
                && observation.GameSaysNight
                && observation.HeroSaysNight;
        }

        public static bool CanBeginRest(
            bool featureEnabled,
            bool allowUnprotectedWyrdnightRest,
            NightObservation observation,
            bool isSafelyResting)
        {
            return !featureEnabled
                || !IsActiveWyrdnightPhaseForRest(observation)
                || allowUnprotectedWyrdnightRest
                || isSafelyResting;
        }

        public static bool IsStableAfterRest(
            NightObservation observation)
        {
            return observation.HasPlayableHero
                && observation.HeroAlive
                && !observation.AtTitleScreen
                && !observation.IsLoading
                && !observation.IsTransitioning
                && !observation.IsTraveling
                && !observation.IsResting
                && observation.SceneKnown
                && observation.SceneInitialized
                && observation.HasWorldTime
                && observation.HasHeroNightState;
        }

        public static bool ShouldShowThreatMeter(NightStateDecision decision)
        {
            return decision.State == DirectorState.Roaming
                && decision.Reason == InactiveReason.None;
        }

        private static NightStateDecision Inactive(InactiveReason reason)
        {
            return new NightStateDecision(DirectorState.Inactive, reason);
        }
    }

    internal sealed class ActiveRealTimeClock
    {
        private readonly float _maximumStepSeconds;

        public double Seconds { get; private set; }

        public ActiveRealTimeClock(float maximumStepSeconds)
        {
            if (maximumStepSeconds <= 0f
                || float.IsNaN(maximumStepSeconds)
                || float.IsInfinity(maximumStepSeconds))
            {
                throw new ArgumentOutOfRangeException("maximumStepSeconds");
            }

            _maximumStepSeconds = maximumStepSeconds;
        }

        public bool Advance(float unscaledDeltaSeconds, bool canAdvance)
        {
            if (!canAdvance
                || unscaledDeltaSeconds <= 0f
                || unscaledDeltaSeconds > _maximumStepSeconds
                || float.IsNaN(unscaledDeltaSeconds)
                || float.IsInfinity(unscaledDeltaSeconds))
            {
                return false;
            }

            Seconds += unscaledDeltaSeconds;
            return true;
        }
    }
}
