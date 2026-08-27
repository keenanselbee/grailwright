using System;

namespace TGAllLightsCastShadowsAddon
{
    internal static class SafeShadowSelectionRules
    {
        internal static bool IsWithinSelectionDistance(
            float distance,
            bool wasActive,
            float maximumDistance,
            float hysteresis)
        {
            float allowed = maximumDistance + (wasActive ? hysteresis : 0f);
            return distance <= allowed;
        }

        internal static bool IsEffectivelyViewRelevant(
            bool intersectsView,
            bool wasActive,
            float secondsSinceIntersection,
            float exitDelaySeconds)
        {
            return intersectsView
                || (wasActive
                    && secondsSinceIntersection >= 0f
                    && secondsSinceIntersection <= exitDelaySeconds);
        }

        internal static float CalculateCandidateScore(
            float distance,
            float intensity,
            float range,
            bool wasActive)
        {
            float score = 1000f / (1f + Math.Max(0f, distance));
            score += Math.Min(Math.Max(0f, intensity), 8f) * 0.25f;
            score += Math.Min(Math.Max(0f, range), 30f) * 0.05f;
            return wasActive ? score + 5f : score;
        }

        internal static int ShadowMapFaceCost(bool isPointLight)
        {
            return isPointLight ? 6 : 1;
        }

        internal static int AvailableShadowMapFaces(
            int maximumFaces,
            int externalFaces)
        {
            return Math.Max(0, maximumFaces - Math.Max(0, externalFaces));
        }

        internal static int ResolveDawnDuskBlendMinutes(
            int configuredMinutes,
            bool normalizeToRealSeconds,
            float targetRealSeconds,
            float weatherSecondsPerRealSecond)
        {
            int fallback = Math.Max(1, Math.Min(120, configuredMinutes));
            if (!normalizeToRealSeconds
                || float.IsNaN(targetRealSeconds)
                || float.IsInfinity(targetRealSeconds)
                || targetRealSeconds <= 0f
                || float.IsNaN(weatherSecondsPerRealSecond)
                || float.IsInfinity(weatherSecondsPerRealSecond)
                || weatherSecondsPerRealSecond <= 0f)
            {
                return fallback;
            }
            int converted = (int)Math.Round(
                targetRealSeconds * weatherSecondsPerRealSecond / 60f,
                MidpointRounding.AwayFromZero);
            return Math.Max(1, Math.Min(120, converted));
        }

        internal static bool FitsSelectionBudget(
            int selectedLights,
            int selectedFaces,
            int candidateFaces,
            int maximumLights,
            int maximumFaces)
        {
            return selectedLights < Math.Max(0, maximumLights)
                && selectedFaces + Math.Max(0, candidateFaces)
                    <= Math.Max(0, maximumFaces);
        }
    }
}
