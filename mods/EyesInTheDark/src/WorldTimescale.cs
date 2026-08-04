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
        public const float MinimumMultiplier = 0.01f;
        public const float MaximumMultiplier = 5f;

        public static float ClampMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier)
                || float.IsInfinity(multiplier))
            {
                return 1f;
            }
            return Math.Max(
                MinimumMultiplier,
                Math.Min(MaximumMultiplier, multiplier));
        }

        public static float EquivalentCycleMinutes(
            float vanillaCycleMinutes,
            float multiplier)
        {
            return Math.Max(0.01f, vanillaCycleMinutes)
                / ClampMultiplier(multiplier);
        }

        public static float PhaseMinutes(
            float vanillaCycleMinutes,
            float multiplier,
            bool night)
        {
            return EquivalentCycleMinutes(
                vanillaCycleMinutes,
                multiplier)
                * (night ? NightFraction : DayFraction);
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
        private float _lastMultiplier;
        private float _lastVanillaCycleMinutes;
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
            float dayMultiplier,
            float nightMultiplier)
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
            float multiplier = WorldTimescalePolicy.ClampMultiplier(
                isNight ? nightMultiplier : dayMultiplier);
            bool stateUnchanged = _ownsRate
                && isNight == _lastWasNight
                && WorldTimescalePolicy.Approximately(
                    multiplier,
                    _lastMultiplier);
            if (stateUnchanged)
            {
                return;
            }

            try
            {
                clock.SetWeatherDayDuration(
                    WorldTimescalePolicy.EquivalentCycleMinutes(
                        vanillaCycleMinutes,
                        multiplier));
                _lastAppliedRate = clock.WeatherSecondsPerRealSecond;
                _lastMultiplier = multiplier;
                _lastWasNight = isNight;
                _ownsRate = true;
                _failureLogged = false;
                _log.LogInfo(
                    "World timescale applied: phase="
                    + (isNight ? "night" : "day")
                    + "; multiplier="
                    + multiplier.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + "; phaseMinutes="
                    + WorldTimescalePolicy.PhaseMinutes(
                        vanillaCycleMinutes,
                        multiplier,
                        isNight).ToString(
                            "0.0",
                            CultureInfo.InvariantCulture)
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
