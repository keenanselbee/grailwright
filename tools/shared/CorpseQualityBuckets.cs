using System;

namespace Grailwright.Shared
{
    public enum CorpseQualityTier
    {
        None = 0,
        Meager = 1,
        Worthy = 2,
        Potent = 3,
        Prime = 4
    }

    public enum CorpseQualityThreatClass
    {
        Normal = 0,
        Elite = 1,
        MiniBoss = 2,
        Boss = 3
    }

    public static class CorpseQualityBuckets
    {
        public const float DefaultReferenceKillXp = 700.0f;
        public const float DefaultReferenceMaxHealth = 3400.0f;
        public const float DefaultLevelQualityPerLevel = 0.025f;
        public const float DefaultMaximumLevelQualityAdjustment = 0.075f;
        public const float EliteQualityBonus = 0.10f;
        public const float MiniBossQualityBonus = 0.175f;
        public const float BossMinimumQuality = 0.875f;
        public const float MeagerMaximumQuality = 0.25f;
        public const float WorthyMaximumQuality = 0.50f;
        public const float PotentMaximumQuality = 0.75f;

        public static float CalculateIntrinsicQuality01(
            int nativeTier,
            float killXp,
            float referenceKillXp,
            float maxHealth,
            float referenceMaxHealth,
            out bool hasEvidence,
            out bool usedNativeTier)
        {
            usedNativeTier = TryGetNativeTierQuality01(nativeTier, out float quality01);
            if (usedNativeTier)
            {
                hasEvidence = true;
                return quality01;
            }

            return CalculateQuality01(
                killXp,
                referenceKillXp,
                maxHealth,
                referenceMaxHealth,
                out hasEvidence);
        }

        public static bool TryGetNativeTierQuality01(int nativeTier, out float quality01)
        {
            switch (nativeTier)
            {
                case 0:
                    quality01 = 0.05f;
                    return true;
                case 1:
                    quality01 = 0.125f;
                    return true;
                case 2:
                    quality01 = 0.23f;
                    return true;
                case 3:
                    quality01 = 0.425f;
                    return true;
                case 4:
                    quality01 = 0.625f;
                    return true;
                case 5:
                    quality01 = 0.80f;
                    return true;
                case 6:
                    quality01 = 0.90f;
                    return true;
                case 7:
                    quality01 = 1.0f;
                    return true;
                default:
                    quality01 = 0.0f;
                    return false;
            }
        }

        public static float ApplyThreatClassAdjustment(
            float intrinsicQuality01,
            CorpseQualityThreatClass threatClass)
        {
            intrinsicQuality01 = Clamp01(intrinsicQuality01);
            switch (threatClass)
            {
                case CorpseQualityThreatClass.Elite:
                    return Clamp01(intrinsicQuality01 + EliteQualityBonus);
                case CorpseQualityThreatClass.MiniBoss:
                    return Clamp01(intrinsicQuality01 + MiniBossQualityBonus);
                case CorpseQualityThreatClass.Boss:
                    return Math.Max(intrinsicQuality01, BossMinimumQuality);
                default:
                    return intrinsicQuality01;
            }
        }

        public static float ApplyBoundedRelativeLevelAdjustment(
            float intrinsicQuality01,
            float enemyExpLevel,
            float heroLevel,
            float qualityPerLevel,
            float maximumAdjustment,
            out bool adjusted)
        {
            adjusted = false;
            intrinsicQuality01 = Clamp01(intrinsicQuality01);
            if (enemyExpLevel < 0.0f
                || heroLevel < 0.0f
                || float.IsNaN(enemyExpLevel)
                || float.IsNaN(heroLevel)
                || float.IsInfinity(enemyExpLevel)
                || float.IsInfinity(heroLevel))
            {
                return intrinsicQuality01;
            }

            float perLevel = Math.Max(0.0f, qualityPerLevel);
            float limit = Math.Max(0.0f, maximumAdjustment);
            float levelAdjustment = (enemyExpLevel - heroLevel) * perLevel;
            levelAdjustment = Math.Max(-limit, Math.Min(limit, levelAdjustment));
            adjusted = Math.Abs(levelAdjustment) > 0.0001f;
            return Clamp01(intrinsicQuality01 + levelAdjustment);
        }

        public static float CalculateQuality01(
            float killXp,
            float referenceKillXp,
            float maxHealth,
            float referenceMaxHealth,
            out bool hasEvidence)
        {
            bool hasKillXp = killXp > 0.0f;
            bool hasMaxHealth = maxHealth > 0.0f;
            hasEvidence = hasKillXp || hasMaxHealth;
            if (!hasEvidence)
            {
                return 0.0f;
            }

            float xpQuality = hasKillXp
                ? Clamp01(killXp / Math.Max(1.0f, referenceKillXp))
                : 0.0f;
            float healthQuality = hasMaxHealth
                ? Clamp01(maxHealth / Math.Max(1.0f, referenceMaxHealth))
                : 0.0f;
            return hasKillXp && hasMaxHealth
                ? (xpQuality + healthQuality) * 0.5f
                : hasKillXp ? xpQuality : healthQuality;
        }

        public static CorpseQualityTier GetTier(float quality01, bool hasEvidence)
        {
            if (!hasEvidence || float.IsNaN(quality01) || float.IsInfinity(quality01))
            {
                return CorpseQualityTier.None;
            }

            quality01 = Clamp01(quality01);
            if (quality01 <= MeagerMaximumQuality)
            {
                return CorpseQualityTier.Meager;
            }
            if (quality01 <= WorthyMaximumQuality)
            {
                return CorpseQualityTier.Worthy;
            }
            if (quality01 <= PotentMaximumQuality)
            {
                return CorpseQualityTier.Potent;
            }

            return CorpseQualityTier.Prime;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0.0f)
            {
                return 0.0f;
            }
            if (value >= 1.0f)
            {
                return 1.0f;
            }

            return value;
        }
    }
}
