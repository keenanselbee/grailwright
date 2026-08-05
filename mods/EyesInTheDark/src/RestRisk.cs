using System;

namespace EyesInTheDark
{
    internal struct RestRiskWindow
    {
        public float RequestedHours;
        public float OverlapStartHours;
        public float OverlapHours;

        public bool HasWyrdnightOverlap
        {
            get { return OverlapHours > 0f; }
        }
    }

    internal struct RestRiskDecision
    {
        public readonly bool InterruptedByEyes;
        public readonly bool InterruptedByNative;
        public readonly float HoursUntilInterrupt;
        public readonly float ExposureBefore;
        public readonly float ExposureAfter;
        public readonly float Chance;

        public RestRiskDecision(
            bool interruptedByEyes,
            bool interruptedByNative,
            float hoursUntilInterrupt,
            float exposureBefore,
            float exposureAfter,
            float chance)
        {
            InterruptedByEyes = interruptedByEyes;
            InterruptedByNative = interruptedByNative;
            HoursUntilInterrupt = hoursUntilInterrupt;
            ExposureBefore = exposureBefore;
            ExposureAfter = exposureAfter;
            Chance = chance;
        }
    }

    internal static class RestRiskPolicy
    {
        public const float NightStartHour = 22.08f;
        public const float NightEndHour = 5.52f;
        public const float NightHours =
            (24f - NightStartHour) + NightEndHour;

        public static bool TryCreateWindow(
            float dayFraction,
            float requestedHours,
            out RestRiskWindow window)
        {
            window = new RestRiskWindow();
            if (!IsFinite(dayFraction)
                || !IsFinite(requestedHours)
                || requestedHours <= 0f)
            {
                return false;
            }

            float startHour = NormalizeDayFraction(dayFraction) * 24f;
            float endHour = startHour + Math.Min(24f, requestedHours);
            for (int dayOffset = -1; dayOffset <= 1; dayOffset++)
            {
                float nightStart = dayOffset * 24f + NightStartHour;
                float nightEnd = (dayOffset + 1) * 24f + NightEndHour;
                float overlapStart = Math.Max(startHour, nightStart);
                float overlapEnd = Math.Min(endHour, nightEnd);
                if (overlapEnd <= overlapStart)
                {
                    continue;
                }

                window.RequestedHours = requestedHours;
                window.OverlapStartHours = overlapStart - startHour;
                window.OverlapHours = overlapEnd - overlapStart;
                return true;
            }

            return false;
        }

        public static float Chance(
            float threat,
            float chanceAtZeroThreat,
            float chanceAtMaximumThreat)
        {
            float normalizedThreat = Clamp01(threat / 100f);
            float low = Clamp01(chanceAtZeroThreat / 100f);
            float high = Clamp01(chanceAtMaximumThreat / 100f);
            return low + (high - low) * normalizedThreat;
        }

        private static float NormalizeDayFraction(float value)
        {
            value %= 1f;
            return value < 0f ? value + 1f : value;
        }

        private static float Clamp01(float value)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                return 0f;
            }
            return value >= 1f ? 1f : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class RestRiskTracker
    {
        private readonly Random _random;
        private bool _initialized;
        private float _chanceRoll;
        private float _exposureThreshold;

        public float Exposure { get; private set; }
        public bool Disturbed { get; private set; }

        public RestRiskTracker(int seed)
        {
            _random = new Random(seed);
        }

        public RestRiskDecision Evaluate(
            RestRiskWindow window,
            float threat,
            float chanceAtZeroThreat,
            float chanceAtMaximumThreat,
            bool nativeInterrupted,
            float nativeHoursUntilInterrupt)
        {
            EnsureInitialized();
            float chance = RestRiskPolicy.Chance(
                threat,
                chanceAtZeroThreat,
                chanceAtMaximumThreat);
            float before = Exposure;
            float availableOverlap = window.OverlapHours;
            if (nativeInterrupted)
            {
                float actualEnd = Math.Max(0f, nativeHoursUntilInterrupt);
                availableOverlap = Math.Max(
                    0f,
                    Math.Min(
                        window.OverlapStartHours + window.OverlapHours,
                        actualEnd)
                    - window.OverlapStartHours);
            }

            float exposureAdded = Math.Max(
                0f,
                availableOverlap / RestRiskPolicy.NightHours);
            float after = Math.Min(1f, before + exposureAdded);
            if (nativeInterrupted)
            {
                Exposure = after;
                if (availableOverlap > 0f)
                {
                    Disturbed = true;
                }
                return new RestRiskDecision(
                    false,
                    true,
                    nativeHoursUntilInterrupt,
                    before,
                    after,
                    chance);
            }

            bool shouldInterrupt = !Disturbed
                && _chanceRoll < chance
                && after >= _exposureThreshold;
            if (!shouldInterrupt)
            {
                Exposure = after;
                return new RestRiskDecision(
                    false,
                    false,
                    0f,
                    before,
                    after,
                    chance);
            }

            float requiredExposure = Math.Max(
                0f,
                _exposureThreshold - before);
            float hoursIntoOverlap = requiredExposure
                * RestRiskPolicy.NightHours;
            float minimumInsideNight = Math.Min(
                0.1f,
                window.OverlapHours * 0.5f);
            hoursIntoOverlap = Math.Max(
                minimumInsideNight,
                Math.Min(
                    window.OverlapHours - minimumInsideNight,
                    hoursIntoOverlap));
            float interruptAt = window.OverlapStartHours + hoursIntoOverlap;
            Exposure = Math.Min(
                1f,
                before + hoursIntoOverlap / RestRiskPolicy.NightHours);
            Disturbed = true;
            return new RestRiskDecision(
                true,
                false,
                interruptAt,
                before,
                Exposure,
                chance);
        }

        public void Reset()
        {
            _initialized = false;
            _chanceRoll = 0f;
            _exposureThreshold = 0f;
            Exposure = 0f;
            Disturbed = false;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _chanceRoll = (float)_random.NextDouble();
            _exposureThreshold = 0.08f
                + (float)_random.NextDouble() * 0.84f;
        }
    }
}
