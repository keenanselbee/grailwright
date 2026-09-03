using System;

namespace TGAllLightsCastShadowsAddon
{
    internal enum ShadowHandoffPhase
    {
        FadeOut,
        FadeIn,
        Complete
    }

    internal struct ShadowHandoffProgress
    {
        internal readonly ShadowHandoffPhase Phase;
        internal readonly float StrengthMultiplier;

        internal ShadowHandoffProgress(
            ShadowHandoffPhase phase,
            float strengthMultiplier)
        {
            Phase = phase;
            StrengthMultiplier = strengthMultiplier;
        }
    }

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
            bool wasActive,
            float selectionRetentionMeters,
            float screenCenterWeight,
            float screenCenterPriorityMeters)
        {
            float effectiveDistance = Math.Max(
                0f,
                distance
                    - (wasActive ? Math.Max(0f, selectionRetentionMeters) : 0f)
                    - Math.Max(0f, Math.Min(1f, screenCenterWeight))
                        * Math.Max(0f, screenCenterPriorityMeters));
            float score = 1000f / (1f + effectiveDistance);
            score += Math.Min(Math.Max(0f, intensity), 8f) * 0.25f;
            score += Math.Min(Math.Max(0f, range), 30f) * 0.05f;
            return score;
        }

        internal static bool IsSphereOutsideFrustumPlane(
            float signedCenterDistance,
            float radius)
        {
            return signedCenterDistance < -Math.Max(0f, radius);
        }

        internal static float CalculateScreenCenterWeight(
            float viewportX,
            float viewportY,
            float viewportDepth)
        {
            if (viewportDepth <= 0f
                || viewportX < 0f
                || viewportX > 1f
                || viewportY < 0f
                || viewportY > 1f)
            {
                return 0f;
            }

            float horizontal = Math.Abs(viewportX - 0.5f);
            float vertical = Math.Abs(viewportY - 0.5f);
            float edgeDistance = Math.Max(horizontal, vertical);
            const float fullWeightEdge = 1f / 6f;
            const float fadeWidth = 1f / 3f;
            if (edgeDistance <= fullWeightEdge)
            {
                return 1f;
            }
            if (edgeDistance >= 0.5f)
            {
                return 0f;
            }
            return Math.Max(
                0f,
                1f - (edgeDistance - fullWeightEdge) / fadeWidth);
        }

        internal static ShadowHandoffProgress ResolveShadowHandoffProgress(
            float elapsedSeconds,
            float durationSeconds)
        {
            if (durationSeconds <= 0f || elapsedSeconds >= durationSeconds)
            {
                return new ShadowHandoffProgress(
                    ShadowHandoffPhase.Complete,
                    1f);
            }

            float halfDuration = durationSeconds * 0.5f;
            float elapsed = Math.Max(0f, elapsedSeconds);
            if (elapsed < halfDuration)
            {
                return new ShadowHandoffProgress(
                    ShadowHandoffPhase.FadeOut,
                    1f - Math.Min(1f, elapsed / halfDuration));
            }
            return new ShadowHandoffProgress(
                ShadowHandoffPhase.FadeIn,
                Math.Min(1f, (elapsed - halfDuration) / halfDuration));
        }

        internal static int ResolveInitialFillActivationLimit(
            bool initialFillPending,
            int batchSize,
            int missingLights)
        {
            int missing = Math.Max(0, missingLights);
            if (!initialFillPending || batchSize <= 0)
            {
                return missing;
            }
            return Math.Min(Math.Max(0, batchSize), missing);
        }

        internal static int ResolveShadowResolutionCap(
            bool generalCapActive,
            int generalCap,
            bool interiorCapActive,
            int interiorCap,
            bool combatCapActive,
            int combatCap)
        {
            int cap = int.MaxValue;
            if (generalCapActive)
            {
                cap = Math.Min(cap, generalCap);
            }
            if (interiorCapActive)
            {
                cap = Math.Min(cap, interiorCap);
            }
            if (combatCapActive)
            {
                cap = Math.Min(cap, combatCap);
            }
            return cap;
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
