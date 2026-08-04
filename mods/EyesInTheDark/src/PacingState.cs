using System;

namespace EyesInTheDark
{
    internal struct PacingTuning
    {
        public float BaseDangerBudget;
        public float LongNightBonusScale;
        public float MaximumLongNightBonus;
    }

    internal struct NightBudgetSnapshot
    {
        public readonly float WorldDurationMultiplier;
        public readonly float BonusFraction;
        public readonly float InitialBudget;

        public NightBudgetSnapshot(
            float worldDurationMultiplier,
            float bonusFraction,
            float initialBudget)
        {
            WorldDurationMultiplier = worldDurationMultiplier;
            BonusFraction = bonusFraction;
            InitialBudget = initialBudget;
        }
    }

    internal sealed class NightPacingState
    {
        public bool IsInitialized { get; private set; }
        public float InitialBudget { get; private set; }
        public float RemainingBudget { get; private set; }
        public float WorldDurationMultiplier { get; private set; }
        public float LongNightBonusFraction { get; private set; }

        public NightBudgetSnapshot BeginNight(
            float worldDurationMultiplier,
            PacingTuning tuning)
        {
            float durationMultiplier = Clamp(
                worldDurationMultiplier,
                1f,
                100f);
            float baseBudget = ClampNonNegative(
                tuning.BaseDangerBudget);
            float bonusScale = ClampNonNegative(
                tuning.LongNightBonusScale);
            float maximumBonus = Clamp(
                tuning.MaximumLongNightBonus,
                0f,
                5f);
            float sublinearBonus = (float)Math.Sqrt(
                durationMultiplier) - 1f;
            float bonusFraction = Math.Min(
                maximumBonus,
                Math.Max(0f, sublinearBonus) * bonusScale);

            IsInitialized = true;
            WorldDurationMultiplier = durationMultiplier;
            LongNightBonusFraction = bonusFraction;
            InitialBudget = baseBudget * (1f + bonusFraction);
            RemainingBudget = InitialBudget;
            return new NightBudgetSnapshot(
                WorldDurationMultiplier,
                LongNightBonusFraction,
                InitialBudget);
        }

        public void Reset()
        {
            IsInitialized = false;
            InitialBudget = 0f;
            RemainingBudget = 0f;
            WorldDurationMultiplier = 1f;
            LongNightBonusFraction = 0f;
        }

        public bool TrySpend(
            float cost,
            out float previousBudget,
            out float remainingBudget)
        {
            previousBudget = RemainingBudget;
            remainingBudget = RemainingBudget;
            float safeCost = ClampNonNegative(cost);
            if (!IsInitialized
                || safeCost <= 0f
                || safeCost > RemainingBudget)
            {
                return false;
            }

            RemainingBudget = Math.Max(0f, RemainingBudget - safeCost);
            remainingBudget = RemainingBudget;
            return true;
        }

        public void Refund(float cost)
        {
            if (!IsInitialized)
            {
                return;
            }

            RemainingBudget = Math.Min(
                InitialBudget,
                RemainingBudget + ClampNonNegative(cost));
        }

        private static float ClampNonNegative(float value)
        {
            return float.IsNaN(value) || value <= 0f
                ? 0f
                : float.IsInfinity(value) ? 100000f : value;
        }

        private static float Clamp(
            float value,
            float minimum,
            float maximum)
        {
            if (float.IsNaN(value))
            {
                return minimum;
            }

            if (float.IsPositiveInfinity(value))
            {
                return maximum;
            }

            if (float.IsNegativeInfinity(value))
            {
                return minimum;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
