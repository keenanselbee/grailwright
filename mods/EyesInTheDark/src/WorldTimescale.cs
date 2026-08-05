using System;
using System.Globalization;
using Awaken.TG.Main.Timing;
using BepInEx.Logging;

namespace EyesInTheDark
{
    internal static class WorldTimescalePolicy
    {
        public const float DayFraction = 0.69f;
        public const float NightFraction = 0.31f;
        public const float MinimumPhaseMinutes = 1f;
        public const float MaximumPhaseMinutes = 600f;
        public const float MinimumNightUpdateMinutes = 0.05f;

        public static float ClampPhaseMinutes(float minutes)
        {
            if (float.IsNaN(minutes))
            {
                return MinimumPhaseMinutes;
            }
            if (float.IsPositiveInfinity(minutes))
            {
                return MaximumPhaseMinutes;
            }
            return Math.Max(
                MinimumPhaseMinutes,
                Math.Min(MaximumPhaseMinutes, minutes));
        }

        public static float CycleMinutesForPhase(
            float phaseMinutes,
            bool night)
        {
            return ClampPhaseMinutes(phaseMinutes)
                / (night ? NightFraction : DayFraction);
        }

        public static float DynamicNightMinutes(
            float baseNightMinutes,
            float maximumThreatNightMinutes,
            float threat)
        {
            float minimum = ClampPhaseMinutes(baseNightMinutes);
            float maximum = Math.Max(
                minimum,
                ClampPhaseMinutes(maximumThreatNightMinutes));
            float normalizedThreat = float.IsNaN(threat)
                ? 0f
                : Math.Max(0f, Math.Min(100f, threat)) / 100f;
            return minimum + (maximum - minimum) * normalizedThreat;
        }

        public static float PhaseDurationMultiplier(
            float vanillaCycleMinutes,
            float phaseMinutes,
            bool night)
        {
            float vanillaPhaseMinutes = Math.Max(
                0.01f,
                vanillaCycleMinutes)
                * (night ? NightFraction : DayFraction);
            return ClampPhaseMinutes(phaseMinutes)
                / vanillaPhaseMinutes;
        }

        public static float RemainingNightRealSeconds(
            float nightProgress,
            float weatherSecondsPerRealSecond)
        {
            if (float.IsNaN(nightProgress)
                || float.IsInfinity(nightProgress)
                || float.IsNaN(weatherSecondsPerRealSecond)
                || float.IsInfinity(weatherSecondsPerRealSecond)
                || weatherSecondsPerRealSecond <= 0f)
            {
                return float.PositiveInfinity;
            }

            float remainingNightFraction =
                (1f - Math.Max(0f, Math.Min(1f, nightProgress)))
                * NightFraction;
            return remainingNightFraction
                * 24f
                * 60f
                * 60f
                / weatherSecondsPerRealSecond;
        }

        public static float ElapsedNightRealSeconds(
            float nightProgress,
            float weatherSecondsPerRealSecond)
        {
            if (float.IsNaN(nightProgress)
                || float.IsInfinity(nightProgress)
                || float.IsNaN(weatherSecondsPerRealSecond)
                || float.IsInfinity(weatherSecondsPerRealSecond)
                || weatherSecondsPerRealSecond <= 0f)
            {
                return float.PositiveInfinity;
            }

            float elapsedNightFraction = Math.Max(
                0f,
                Math.Min(1f, nightProgress))
                * NightFraction;
            return elapsedNightFraction
                * 24f
                * 60f
                * 60f
                / weatherSecondsPerRealSecond;
        }

        public static float RemainingDaylightRealSeconds(
            float dayFraction,
            float weatherSecondsPerRealSecond)
        {
            if (float.IsNaN(dayFraction)
                || float.IsInfinity(dayFraction)
                || float.IsNaN(weatherSecondsPerRealSecond)
                || float.IsInfinity(weatherSecondsPerRealSecond)
                || weatherSecondsPerRealSecond <= 0f
                || dayFraction < NightStateEvaluator.NightEndFraction
                || dayFraction > NightStateEvaluator.NightStartFraction)
            {
                return float.PositiveInfinity;
            }

            return (NightStateEvaluator.NightStartFraction - dayFraction)
                * 24f
                * 60f
                * 60f
                / weatherSecondsPerRealSecond;
        }

        public static bool Approximately(float left, float right)
        {
            float scale = Math.Max(
                1f,
                Math.Max(Math.Abs(left), Math.Abs(right)));
            return Math.Abs(left - right) <= scale * 0.0001f;
        }
    }

    internal sealed class WorldTimescaleController
    {
        private readonly ManualLogSource _log;
        private GameRealTime _clock;
        private float _lastAppliedRate;
        private float _lastVanillaCycleMinutes;
        private float _lastDayMinutes;
        private float _lastBaseNightMinutes;
        private float _lastMaximumThreatNightMinutes;
        private float _lastTargetPhaseMinutes;
        private bool _lastWasNight;
        private bool _ownsRate;
        private bool _failureLogged;

        internal WorldTimescaleController(ManualLogSource log)
        {
            _log = log;
        }

        internal void Update(
            GameRealTime clock,
            float vanillaCycleMinutes,
            bool enabled,
            float dayMinutes,
            float baseNightMinutes,
            float maximumThreatNightMinutes,
            float threat)
        {
            if (!ReferenceEquals(_clock, clock))
            {
                _clock = clock;
                _ownsRate = false;
                _lastAppliedRate = 0f;
                _failureLogged = false;
            }

            if (clock == null
                || clock.HasBeenDiscarded
                || vanillaCycleMinutes <= 0f
                || float.IsNaN(vanillaCycleMinutes)
                || float.IsInfinity(vanillaCycleMinutes))
            {
                return;
            }
            _lastVanillaCycleMinutes = vanillaCycleMinutes;

            if (!enabled)
            {
                RestoreVanilla(vanillaCycleMinutes);
                return;
            }

            bool isNight = clock.WeatherTime.IsNight;
            float safeDayMinutes =
                WorldTimescalePolicy.ClampPhaseMinutes(dayMinutes);
            float safeBaseNightMinutes =
                WorldTimescalePolicy.ClampPhaseMinutes(baseNightMinutes);
            float safeMaximumThreatNightMinutes = Math.Max(
                safeBaseNightMinutes,
                WorldTimescalePolicy.ClampPhaseMinutes(
                    maximumThreatNightMinutes));
            float targetPhaseMinutes = isNight
                ? WorldTimescalePolicy.DynamicNightMinutes(
                    safeBaseNightMinutes,
                    safeMaximumThreatNightMinutes,
                    threat)
                : safeDayMinutes;
            bool settingsUnchanged = isNight
                ? WorldTimescalePolicy.Approximately(
                        safeBaseNightMinutes,
                        _lastBaseNightMinutes)
                    && WorldTimescalePolicy.Approximately(
                        safeMaximumThreatNightMinutes,
                        _lastMaximumThreatNightMinutes)
                : WorldTimescalePolicy.Approximately(
                    safeDayMinutes,
                    _lastDayMinutes);
            bool targetUnchanged = Math.Abs(
                    targetPhaseMinutes - _lastTargetPhaseMinutes)
                < WorldTimescalePolicy.MinimumNightUpdateMinutes;
            bool stateUnchanged = _ownsRate
                && isNight == _lastWasNight
                && settingsUnchanged
                && targetUnchanged;
            if (stateUnchanged)
            {
                return;
            }

            try
            {
                clock.SetWeatherDayDuration(
                    WorldTimescalePolicy.CycleMinutesForPhase(
                        targetPhaseMinutes,
                        isNight));
                _lastAppliedRate = clock.WeatherSecondsPerRealSecond;
                _lastDayMinutes = safeDayMinutes;
                _lastBaseNightMinutes = safeBaseNightMinutes;
                _lastMaximumThreatNightMinutes =
                    safeMaximumThreatNightMinutes;
                _lastTargetPhaseMinutes = targetPhaseMinutes;
                _lastWasNight = isNight;
                _ownsRate = true;
                _failureLogged = false;
                _log.LogInfo(
                    "World timescale applied: phase="
                    + (isNight ? "night" : "day")
                    + "; phaseMinutes="
                    + targetPhaseMinutes.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture)
                    + (isNight
                        ? "; threat="
                            + Math.Max(0f, Math.Min(100f, threat)).ToString(
                                "0.0",
                                CultureInfo.InvariantCulture)
                        : string.Empty)
                    + ".");
            }
            catch (Exception exception)
            {
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    _log.LogWarning(
                        "Could not apply the Eyes world timescale; the current game clock remains active: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        internal void Release(float vanillaCycleMinutes)
        {
            float restoreMinutes = vanillaCycleMinutes > 0f
                && !float.IsNaN(vanillaCycleMinutes)
                && !float.IsInfinity(vanillaCycleMinutes)
                    ? vanillaCycleMinutes
                    : _lastVanillaCycleMinutes;
            RestoreVanilla(restoreMinutes);
            _clock = null;
            _ownsRate = false;
        }

        private void RestoreVanilla(float vanillaCycleMinutes)
        {
            if (!_ownsRate || _clock == null || _clock.HasBeenDiscarded)
            {
                _ownsRate = false;
                return;
            }
            if (vanillaCycleMinutes <= 0f
                || float.IsNaN(vanillaCycleMinutes)
                || float.IsInfinity(vanillaCycleMinutes))
            {
                _ownsRate = false;
                return;
            }

            float currentRate = _clock.WeatherSecondsPerRealSecond;
            if (!WorldTimescalePolicy.Approximately(
                    currentRate,
                    _lastAppliedRate))
            {
                _ownsRate = false;
                _log.LogInfo(
                    "World timescale ownership was released without restoring vanilla because another system changed the clock after Eyes.");
                return;
            }

            try
            {
                _clock.SetWeatherDayDuration(
                    Math.Max(0.01f, vanillaCycleMinutes));
                _log.LogInfo("World timescale restored to the vanilla rate.");
            }
            catch (Exception exception)
            {
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    _log.LogWarning(
                        "Could not restore the vanilla world timescale: "
                        + exception.GetBaseException().Message);
                }
            }
            finally
            {
                _ownsRate = false;
            }
        }
    }
}
