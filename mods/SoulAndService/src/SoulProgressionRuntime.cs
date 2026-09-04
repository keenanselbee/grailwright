using System;
using System.Globalization;
using System.Reflection;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.Main.Memories;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SoulAndService
{
    internal static class SoulProgressionRuntime
    {
        internal sealed class CorpseHarvestReceipt
        {
            internal ContextualFacts Facts;
            internal string HarvestKey;
            internal string HarvestCountKey;
            internal float BeforeSoulVigor;
            internal int BeforeHarvested;
            internal int BeforeHarvestCount;
            internal float Award;
        }

        internal sealed class HighSoulDrainReceipt
        {
            internal ContextualFacts Facts;
            internal string RemainingKey;
            internal float BeforeSoulVigor;
            internal int BeforeRemaining;
            internal int Award;
        }

        private const string MemoryContext = "SoulAndService";
        private const string InitializedKey = "soul_vigor.initialized";
        private const string SoulVigorKey = "soul_vigor.total";
        private const string MeagerHarvestsKey = "soul_vigor.harvests.meager";
        private const string WorthyHarvestsKey = "soul_vigor.harvests.worthy";
        private const string PotentHarvestsKey = "soul_vigor.harvests.potent";
        private const string PrimeHarvestsKey = "soul_vigor.harvests.prime";
        private const int HighSoulPoolPortions = 6;
        private const string SummonBehaviorKey = "soul_vigor.summon_behavior";
        private const float SoulVigorAtNormalMaximumPower = 1000.0f;
        private const float SoulVigorAtAbsoluteMaximumPower = 5000.0f;
        internal const float AttackCommandPower = 10.0f;
        internal const float IndividualFormationPower = 20.0f;
        internal const float GlobalFormationPower = 30.0f;
        internal const float BehaviorCommandPower = 50.0f;
        internal const float BulwarkBehaviorPower = 60.0f;
        internal const float RecallCommandPower = 70.0f;
        internal const float SwarmCommandPower = 90.0f;
        internal const float EmpowermentPower = 100.0f;
        internal const float MaximumSummonCapacityPower = 150.0f;
        internal const float RaiseAllPower = 200.0f;
        internal const float GuardDamageMultiplier = 1.05f;
        internal const float GuardDamageTakenMultiplier = 0.95f;
        internal const float BulwarkDamageTakenMultiplier = 0.85f;
        internal const float HuntDamageMultiplier = 1.10f;
        private const string DeedsPluginGuid = "ks.tgfoa.deeds-of-avalon";
        private const string DeedsApiTypeName = "DeedsOfAvalon.StatisticsApi";
        private const string GrailFloatingTextPluginGuid =
            "ks.tgfoa.grail-floating-text";
        private const string GrailFloatingTextApiTypeName =
            "GrailFloatingText.NotificationApi";

        private static readonly string[] BindingFailureMessages =
        {
            "This soul stirs, but does not yet answer.",
            "The spirit strains against its mortal tether.",
            "A cold will resists your command.",
            "The corpse shudders as the soul draws near.",
            "The dead hear you, but remain beyond your grasp.",
            "Necromantic threads tighten around the reluctant soul."
        };
        private static MethodInfo _deedsRecordStatisticsMethod;
        private static MethodInfo _gftTryShowEventMethod;
        private static float _lastReportedSoulVigor = -1.0f;
        private static int _lastReportedMeagerHarvests = -1;
        private static int _lastReportedWorthyHarvests = -1;
        private static int _lastReportedPotentHarvests = -1;
        private static int _lastReportedPrimeHarvests = -1;
        private static float _nextProgressionSyncTime;
        private static bool _deedsUnavailable;
        private static bool _gftUnavailable;

        internal static void Update()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || Time.unscaledTime < _nextProgressionSyncTime)
            {
                return;
            }

            _nextProgressionSyncTime = Time.unscaledTime + 1.0f;
            float soulVigor = GetSoulVigor();
            ContextualFacts facts = GetFacts();
            int meager = GetBindingCount(
                facts,
                Grailwright.Shared.CorpseQualityTier.Meager);
            int worthy = GetBindingCount(
                facts,
                Grailwright.Shared.CorpseQualityTier.Worthy);
            int potent = GetBindingCount(
                facts,
                Grailwright.Shared.CorpseQualityTier.Potent);
            int prime = GetBindingCount(
                facts,
                Grailwright.Shared.CorpseQualityTier.Prime);
            if (soulVigor >= 0.0f
                && (Math.Abs(soulVigor - _lastReportedSoulVigor) > 0.0001f
                    || meager != _lastReportedMeagerHarvests
                    || worthy != _lastReportedWorthyHarvests
                    || potent != _lastReportedPotentHarvests
                    || prime != _lastReportedPrimeHarvests))
            {
                ReportProgressionToDeeds(
                    soulVigor,
                    meager,
                    worthy,
                    potent,
                    prime);
            }
        }

        internal static float GetSoulVigor()
        {
            ContextualFacts facts = GetFacts();
            if (facts == null)
            {
                return 0.0f;
            }

            EnsureInitialized(facts);
            float storedSoulVigor = Math.Max(
                0.0f,
                Mathf.Round(facts.Get(SoulVigorKey, 0.0f)));
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin != null
                && plugin.OverrideSoulVigor != null
                && plugin.OverrideSoulVigor.Value
                    ? Math.Max(
                        0.0f,
                        Mathf.Round(
                        plugin.SoulVigorOverrideValue == null
                            ? 0.0f
                            : plugin.SoulVigorOverrideValue.Value))
                    : storedSoulVigor;
        }

        internal static float GetNecromanticPower()
        {
            return GetNecromanticPowerFromSoulVigor(GetSoulVigor());
        }

        internal static float GetNecromanticPowerFromSoulVigor(float soulVigor)
        {
            float safeSoulVigor = Math.Max(0.0f, soulVigor);
            if (safeSoulVigor <= SoulVigorAtNormalMaximumPower)
            {
                float x = Mathf.Clamp01(
                    safeSoulVigor / SoulVigorAtNormalMaximumPower);
                return (10.0f * x * x * x) - (70.0f * x * x) + (160.0f * x);
            }

            float y = Mathf.Clamp01(
                (safeSoulVigor - SoulVigorAtNormalMaximumPower)
                / (SoulVigorAtAbsoluteMaximumPower
                    - SoulVigorAtNormalMaximumPower));
            return 100.0f + (100.0f * y);
        }

        internal static SummonBehavior GetSummonBehavior()
        {
            float power = GetNecromanticPower();
            if (power < BehaviorCommandPower)
            {
                return SummonBehavior.Guard;
            }
            ContextualFacts facts = GetFacts();
            if (facts == null)
            {
                return SummonBehavior.Guard;
            }
            EnsureInitialized(facts);
            int stored = facts.Get(SummonBehaviorKey, (int)SummonBehavior.Guard);
            SummonBehavior behavior = stored >= (int)SummonBehavior.Guard
                && stored <= (int)SummonBehavior.Hunt
                    ? (SummonBehavior)stored
                    : SummonBehavior.Guard;
            return behavior == SummonBehavior.Bulwark
                    && power < BulwarkBehaviorPower
                ? SummonBehavior.Guard
                : behavior;
        }

        internal static bool TryCycleSummonBehavior(
            out SummonBehavior behavior)
        {
            behavior = SummonBehavior.Guard;
            if (GetNecromanticPower() < BehaviorCommandPower)
            {
                return false;
            }
            ContextualFacts facts = GetFacts();
            if (facts == null)
            {
                return false;
            }
            EnsureInitialized(facts);
            float power = GetNecromanticPower();
            SummonBehavior current = GetSummonBehavior();
            behavior = current == SummonBehavior.Guard
                ? SummonBehavior.Hunt
                : current == SummonBehavior.Hunt
                    && power >= BulwarkBehaviorPower
                    ? SummonBehavior.Bulwark
                    : SummonBehavior.Guard;
            facts.Set(SummonBehaviorKey, (int)behavior);
            return true;
        }

        internal static float GetSummonDamageMultiplier()
        {
            float power = GetNecromanticPower();
            float progressionMultiplier = power <= 100.0f
                ? Mathf.Lerp(0.75f, 1.25f, power / 100.0f)
                : Mathf.Lerp(1.25f, 1.50f, (power - 100.0f) / 100.0f);
            if (power < BehaviorCommandPower)
            {
                return progressionMultiplier;
            }
            SummonBehavior behavior = GetSummonBehavior();
            return progressionMultiplier * (behavior == SummonBehavior.Hunt
                ? HuntDamageMultiplier
                : behavior == SummonBehavior.Guard
                    ? GuardDamageMultiplier
                    : 1.0f);
        }

        internal static float GetSummonDamageTakenMultiplier()
        {
            float power = GetNecromanticPower();
            float progressionMultiplier = power <= 100.0f
                ? Mathf.Lerp(1.25f, 0.75f, power / 100.0f)
                : Mathf.Lerp(0.75f, 0.50f, (power - 100.0f) / 100.0f);
            if (power < BehaviorCommandPower)
            {
                return progressionMultiplier;
            }
            SummonBehavior behavior = GetSummonBehavior();
            return progressionMultiplier * (behavior == SummonBehavior.Bulwark
                ? BulwarkDamageTakenMultiplier
                : behavior == SummonBehavior.Guard
                    ? GuardDamageTakenMultiplier
                    : 1.0f);
        }

        internal static int GetProgressionSummonLimitBonus()
        {
            float power = GetNecromanticPower();
            if (power >= MaximumSummonCapacityPower)
            {
                return 3;
            }
            if (power >= EmpowermentPower)
            {
                return 2;
            }
            return power >= BehaviorCommandPower ? 1 : 0;
        }

        internal static float RollRaisedHealthFraction(float power)
        {
            float lower = 0.40f + (0.0030f * power);
            float upper = 0.60f + (0.0020f * power);
            lower = Mathf.Clamp01(lower);
            upper = Mathf.Clamp(upper, lower, 1.0f);
            float rolled = UnityEngine.Random.Range(lower, upper);
            return Mathf.Clamp01(
                rolled
                * SoulAndServicePlugin.GetEffectiveBalanceTuning()
                    .RaisedStartingHealthMultiplier);
        }

        internal static float GetQualityHealthMultiplier(
            Grailwright.Shared.CorpseQualityTier tier)
        {
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    return 0.90f;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    return 1.05f;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    return 1.20f;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    return 0.75f;
            }
        }

        internal static bool ApplyBindingAttempt(
            string corpseFingerprint,
            Grailwright.Shared.CorpseQualityTier tier,
            out float progress01,
            out float resistance)
        {
            return ApplyBindingAttempt(
                corpseFingerprint,
                GetBindingResistance(corpseFingerprint, tier),
                out progress01,
                out resistance);
        }

        internal static bool ApplyHighSoulBindingAttempt(
            string corpseFingerprint,
            int serviceCycle,
            float baseResistance,
            out float progress01,
            out float resistance)
        {
            string cycleFingerprint = corpseFingerprint
                + ":service-cycle:"
                + Math.Max(0, serviceCycle).ToString(
                    CultureInfo.InvariantCulture);
            float random01 = StableUnit(cycleFingerprint + ":resistance");
            return ApplyBindingAttempt(
                cycleFingerprint,
                Math.Max(1.0f, baseResistance)
                    * Mathf.Lerp(0.90f, 1.10f, random01),
                out progress01,
                out resistance);
        }

        internal static void CommitHighSoulBinding(
            string corpseFingerprint,
            int serviceCycle)
        {
            CommitSuccessfulBinding(
                corpseFingerprint
                + ":service-cycle:"
                + Math.Max(0, serviceCycle).ToString(
                    CultureInfo.InvariantCulture));
        }

        private static bool ApplyBindingAttempt(
            string bindingFingerprint,
            float bindingResistance,
            out float progress01,
            out float resistance)
        {
            progress01 = 0.0f;
            resistance = bindingResistance;
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(bindingFingerprint))
            {
                return false;
            }

            EnsureInitialized(facts);
            string key = BindingKey(bindingFingerprint);
            if (facts.Get(key + ".bound", 0) != 0)
            {
                progress01 = 1.0f;
                return true;
            }

            int attempt = Math.Max(0, facts.Get(key + ".attempts", 0));
            float progress = Math.Max(0.0f, facts.Get(key + ".progress", 0.0f));
            float power = GetNecromanticPower();
            if (power >= 199.999f)
            {
                progress = resistance;
            }
            else
            {
                progress += GetBindingIncrement(bindingFingerprint, attempt)
                    + (0.75f * power);
            }

            facts.Set(key + ".attempts", attempt == int.MaxValue ? attempt : attempt + 1);
            facts.Set(key + ".progress", progress);
            progress01 = resistance <= 0.0001f
                ? 1.0f
                : Mathf.Clamp01(progress / resistance);
            if (progress + 0.0001f < resistance)
            {
                return false;
            }

            return true;
        }

        internal static void CommitSuccessfulBinding(string corpseFingerprint)
        {
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return;
            }
            EnsureInitialized(facts);
            string key = BindingKey(corpseFingerprint);
            facts.Set(key + ".bound", 1);
        }

        internal static bool TryHarvestCorpse(
            string corpseFingerprint,
            Grailwright.Shared.CorpseQualityTier tier,
            float quality01,
            out CorpseHarvestReceipt receipt)
        {
            return TryHarvestCorpse(
                corpseFingerprint,
                tier,
                ApplySoulVigorRewardMultiplier(
                    GetOrRollCorpseSoulVigorValue(
                        corpseFingerprint,
                        tier,
                        quality01)),
                out receipt);
        }

        internal static bool TryHarvestCorpse(
            string corpseFingerprint,
            string harvestIdentity,
            Grailwright.Shared.CorpseQualityTier tier,
            float quality01,
            out CorpseHarvestReceipt receipt)
        {
            return TryHarvestCorpse(
                harvestIdentity,
                tier,
                ApplySoulVigorRewardMultiplier(
                    GetOrRollCorpseSoulVigorValue(
                        corpseFingerprint,
                        tier,
                        quality01)),
                out receipt);
        }

        internal static int GetOrInitializeHighSoulVigorPool(
            string corpseFingerprint,
            int extractionValue)
        {
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return 0;
            }

            EnsureInitialized(facts);
            string key = HighSoulPoolKey(corpseFingerprint);
            int initialized = facts.Get(key + ".initialized", int.MinValue);
            if (initialized != int.MinValue && initialized != 0)
            {
                int storedExtraction = facts.Get(key + ".extraction", 0);
                if (storedExtraction <= 0)
                {
                    return 0;
                }
                int maximum = SaturatingMultiply(
                    storedExtraction,
                    HighSoulPoolPortions);
                int storedRemaining = facts.Get(key + ".remaining", 0);
                int remaining = Mathf.Clamp(storedRemaining, 0, maximum);
                if (remaining != storedRemaining)
                {
                    facts.Set(key + ".remaining", remaining);
                }
                return remaining;
            }

            int partialExtraction = facts.Get(
                key + ".extraction",
                int.MinValue);
            int partialRemaining = facts.Get(
                key + ".remaining",
                int.MinValue);
            if (initialized != int.MinValue
                || partialExtraction != int.MinValue
                || partialRemaining != int.MinValue)
            {
                facts.Set(key + ".extraction", 0);
                facts.Set(key + ".remaining", 0);
                facts.Set(key + ".initialized", 1);
                return 0;
            }

            extractionValue = Mathf.Clamp(extractionValue, 1, 1000);
            int initialPool = SaturatingMultiply(
                extractionValue,
                HighSoulPoolPortions);
            facts.Set(key + ".extraction", extractionValue);
            facts.Set(key + ".remaining", initialPool);
            facts.Set(key + ".initialized", 1);
            return initialPool;
        }

        internal static int GetHighSoulExtractionValue(
            string corpseFingerprint,
            int fallbackExtractionValue)
        {
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return Math.Max(1, fallbackExtractionValue);
            }
            string key = HighSoulPoolKey(corpseFingerprint);
            int stored = facts.Get(key + ".extraction", 0);
            return stored > 0
                ? stored
                : Math.Max(1, fallbackExtractionValue);
        }

        internal static bool TryGetExistingHighSoulVigorPool(
            string corpseFingerprint,
            int expectedExtractionValue,
            out int remaining)
        {
            remaining = 0;
            ContextualFacts facts = GetFacts();
            if (facts == null
                || string.IsNullOrEmpty(corpseFingerprint)
                || expectedExtractionValue <= 0)
            {
                return false;
            }

            EnsureInitialized(facts);
            string key = HighSoulPoolKey(corpseFingerprint);
            if (facts.Get(key + ".initialized", int.MinValue) != 1
                || facts.Get(key + ".extraction", int.MinValue)
                    != expectedExtractionValue)
            {
                return false;
            }
            int storedRemaining = facts.Get(key + ".remaining", int.MinValue);
            int maximum = SaturatingMultiply(
                expectedExtractionValue,
                HighSoulPoolPortions);
            if (storedRemaining < 0
                || storedRemaining > maximum
                || storedRemaining % expectedExtractionValue != 0)
            {
                return false;
            }
            remaining = storedRemaining;
            return true;
        }

        internal static bool TryDrainHighSoulVigorPool(
            string corpseFingerprint,
            int extractionValue,
            out HighSoulDrainReceipt receipt)
        {
            receipt = null;
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return false;
            }

            try
            {
                int remaining = GetOrInitializeHighSoulVigorPool(
                    corpseFingerprint,
                    extractionValue);
                if (remaining <= 0)
                {
                    return false;
                }
                string remainingKey = HighSoulPoolKey(corpseFingerprint)
                    + ".remaining";
                int stableExtraction = GetHighSoulExtractionValue(
                    corpseFingerprint,
                    extractionValue);
                int consumed = Math.Min(remaining, stableExtraction);
                int award = ApplySoulVigorRewardMultiplier(consumed);
                float beforeSoulVigor = Math.Max(
                    0.0f,
                    Mathf.Round(facts.Get(SoulVigorKey, 0.0f)));
                receipt = new HighSoulDrainReceipt
                {
                    Facts = facts,
                    RemainingKey = remainingKey,
                    BeforeSoulVigor = beforeSoulVigor,
                    BeforeRemaining = remaining,
                    Award = award
                };
                facts.Set(
                    SoulVigorKey,
                    SaturatingAdd(beforeSoulVigor, award));
                facts.Set(remainingKey, Math.Max(0, remaining - consumed));
                InvalidateReportedProgression();
                return true;
            }
            catch (Exception exception)
            {
                if (receipt != null)
                {
                    TryRestoreHighSoulDrain(receipt);
                    receipt = null;
                }
                LogProgressionWarning(
                    "High-soul Vigor could not be saved: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        internal static bool TryConsumeHighSoulServicePortion(
            string corpseFingerprint,
            int extractionValue,
            out HighSoulDrainReceipt receipt)
        {
            receipt = null;
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return false;
            }

            try
            {
                if (!TryGetExistingHighSoulVigorPool(
                        corpseFingerprint,
                        extractionValue,
                        out int remaining)
                    || remaining <= 0)
                {
                    return false;
                }
                string remainingKey = HighSoulPoolKey(corpseFingerprint)
                    + ".remaining";
                receipt = new HighSoulDrainReceipt
                {
                    Facts = facts,
                    RemainingKey = remainingKey,
                    BeforeSoulVigor = Math.Max(
                        0.0f,
                        Mathf.Round(facts.Get(SoulVigorKey, 0.0f))),
                    BeforeRemaining = remaining,
                    Award = 0
                };
                facts.Set(
                    remainingKey,
                    Math.Max(0, remaining - extractionValue));
                return true;
            }
            catch (Exception exception)
            {
                if (receipt != null)
                {
                    TryRestoreHighSoulDrain(receipt);
                    receipt = null;
                }
                LogProgressionWarning(
                    "High-soul service depletion could not be saved: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        internal static int GetHighSoulServiceCycle(
            string corpseFingerprint,
            int extractionValue)
        {
            int stableExtraction = GetHighSoulExtractionValue(
                corpseFingerprint,
                extractionValue);
            int remaining = GetOrInitializeHighSoulVigorPool(
                corpseFingerprint,
                extractionValue);
            int remainingPortions = Mathf.Clamp(
                Mathf.CeilToInt(remaining / (float)Math.Max(1, stableExtraction)),
                0,
                HighSoulPoolPortions);
            return HighSoulPoolPortions - remainingPortions;
        }

        internal static void RollbackHighSoulDrain(HighSoulDrainReceipt receipt)
        {
            if (receipt != null && !TryRestoreHighSoulDrain(receipt))
            {
                LogProgressionWarning(
                    "High-soul Vigor rollback failed after remains could not be created.");
            }
        }

        internal static bool TryHarvestCorpse(
            string corpseFingerprint,
            Grailwright.Shared.CorpseQualityTier tier,
            int award,
            out CorpseHarvestReceipt receipt)
        {
            receipt = null;
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return false;
            }
            try
            {
                EnsureInitialized(facts);
                string harvestKey = HarvestKey(corpseFingerprint);
                int beforeHarvested = facts.Get(harvestKey, 0);
                if (beforeHarvested != 0)
                {
                    return false;
                }
                string countKey = HarvestCountKey(tier);
                float beforeSoulVigor = Math.Max(
                    0.0f,
                    Mathf.Round(facts.Get(SoulVigorKey, 0.0f)));
                int beforeHarvestCount = Math.Max(0, facts.Get(countKey, 0));
                award = Math.Max(0, award);
                receipt = new CorpseHarvestReceipt
                {
                    Facts = facts,
                    HarvestKey = harvestKey,
                    HarvestCountKey = countKey,
                    BeforeSoulVigor = beforeSoulVigor,
                    BeforeHarvested = beforeHarvested,
                    BeforeHarvestCount = beforeHarvestCount,
                    Award = award
                };
                facts.Set(SoulVigorKey, SaturatingAdd(beforeSoulVigor, award));
                facts.Set(
                    countKey,
                    beforeHarvestCount == int.MaxValue
                        ? int.MaxValue
                        : beforeHarvestCount + 1);
                facts.Set(harvestKey, 1);
                InvalidateReportedProgression();
                return true;
            }
            catch (Exception exception)
            {
                if (receipt != null)
                {
                    TryRestoreCorpseHarvest(receipt);
                    receipt = null;
                }
                LogProgressionWarning(
                    "Soul Vigor could not be saved: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        internal static void RollbackCorpseHarvest(CorpseHarvestReceipt receipt)
        {
            if (receipt != null && !TryRestoreCorpseHarvest(receipt))
            {
                LogProgressionWarning(
                    "Soul Vigor rollback failed after remains could not be created.");
            }
        }

        internal static bool IsCorpseHarvested(string corpseFingerprint)
        {
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return false;
            }
            EnsureInitialized(facts);
            return facts.Get(HarvestKey(corpseFingerprint), 0) != 0;
        }

        internal static int GetOrRollCorpseSoulVigorValue(
            string corpseFingerprint,
            Grailwright.Shared.CorpseQualityTier tier,
            float quality01)
        {
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return RollSoulVigorValue(tier, quality01);
            }

            EnsureInitialized(facts);
            string key = CorpseSoulVigorKey(corpseFingerprint);
            int stored = Math.Max(0, facts.Get(key, 0));
            if (stored > 0)
            {
                return stored;
            }

            int rolled = RollSoulVigorValue(tier, quality01);
            facts.Set(key, rolled);
            return rolled;
        }

        internal static int RollSoulVigorValue(
            Grailwright.Shared.CorpseQualityTier tier,
            float quality01)
        {
            int minimum;
            int maximum;
            int nominal;
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    minimum = 7;
                    maximum = 11;
                    nominal = 9;
                    break;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    minimum = 12;
                    maximum = 18;
                    nominal = 15;
                    break;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    minimum = 24;
                    maximum = 36;
                    nominal = 30;
                    break;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    minimum = 2;
                    maximum = 4;
                    nominal = 3;
                    break;
            }

            float tierStart = tier == Grailwright.Shared.CorpseQualityTier.Meager
                ? 0.0f
                : tier == Grailwright.Shared.CorpseQualityTier.Worthy
                    ? Grailwright.Shared.CorpseQualityBuckets.MeagerMaximumQuality
                    : tier == Grailwright.Shared.CorpseQualityTier.Potent
                        ? Grailwright.Shared.CorpseQualityBuckets.WorthyMaximumQuality
                        : Grailwright.Shared.CorpseQualityBuckets.PotentMaximumQuality;
            float tierEnd = tier == Grailwright.Shared.CorpseQualityTier.Meager
                ? Grailwright.Shared.CorpseQualityBuckets.MeagerMaximumQuality
                : tier == Grailwright.Shared.CorpseQualityTier.Worthy
                    ? Grailwright.Shared.CorpseQualityBuckets.WorthyMaximumQuality
                    : tier == Grailwright.Shared.CorpseQualityTier.Potent
                        ? Grailwright.Shared.CorpseQualityBuckets.PotentMaximumQuality
                        : 1.0f;
            float qualityBias = tierEnd <= tierStart
                ? 0.5f
                : Mathf.Clamp01((quality01 - tierStart) / (tierEnd - tierStart));
            float totalWeight = 0.0f;
            for (int value = minimum; value <= maximum; value++)
            {
                int centerWeight = Math.Min(
                    value - minimum + 1,
                    maximum - value + 1);
                float position = maximum == minimum
                    ? 0.5f
                    : (float)(value - minimum) / (maximum - minimum);
                totalWeight += centerWeight
                    * Mathf.Lerp(0.80f, 1.20f, 1.0f - Math.Abs(position - qualityBias));
            }
            float roll = UnityEngine.Random.value * totalWeight;
            int rolled = nominal;
            for (int value = minimum; value <= maximum; value++)
            {
                int centerWeight = Math.Min(
                    value - minimum + 1,
                    maximum - value + 1);
                float position = maximum == minimum
                    ? 0.5f
                    : (float)(value - minimum) / (maximum - minimum);
                roll -= centerWeight
                    * Mathf.Lerp(0.80f, 1.20f, 1.0f - Math.Abs(position - qualityBias));
                if (roll <= 0.0f)
                {
                    rolled = value;
                    break;
                }
            }

            float masteryBonus = nominal
                * 0.05f
                * Mathf.Clamp01(GetNecromanticPower() / 200.0f);
            int wholeBonus = Mathf.FloorToInt(masteryBonus);
            if (UnityEngine.Random.value < masteryBonus - wholeBonus)
            {
                wholeBonus++;
            }
            return rolled + wholeBonus;
        }

        internal static int GetNominalCorpseSoulVigorValue(
            Grailwright.Shared.CorpseQualityTier tier)
        {
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    return 9;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    return 15;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    return 30;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    return 3;
            }
        }

        internal static int GetScaledCorpseSoulVigorAward(
            int baseAward,
            float rewardFraction)
        {
            int fullAward = ApplySoulVigorRewardMultiplier(baseAward);
            return Math.Max(
                0,
                Mathf.FloorToInt(
                    fullAward * Mathf.Clamp01(rewardFraction) + 0.5f));
        }

        internal static bool TrySpendSoulVigor(
            int amount,
            out int before,
            out int after)
        {
            before = Mathf.RoundToInt(GetSoulVigor());
            after = before;
            amount = Math.Max(0, amount);
            if (amount == 0)
            {
                return true;
            }
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null
                && plugin.OverrideSoulVigor != null
                && plugin.OverrideSoulVigor.Value)
            {
                return before >= amount;
            }
            ContextualFacts facts = GetFacts();
            if (facts == null || before < amount)
            {
                return false;
            }
            after = before - amount;
            facts.Set(SoulVigorKey, after);
            InvalidateReportedProgression();
            return true;
        }

        internal static int RestoreSoulVigor(int amount)
        {
            amount = Math.Max(0, amount);
            ContextualFacts facts = GetFacts();
            if (facts == null || amount == 0)
            {
                return 0;
            }
            EnsureInitialized(facts);
            float before = Math.Max(0.0f, Mathf.Round(facts.Get(SoulVigorKey, 0.0f)));
            AddSoulVigor(facts, amount);
            InvalidateReportedProgression();
            float after = Math.Max(
                0.0f,
                Mathf.Round(facts.Get(SoulVigorKey, 0.0f)));
            return Mathf.RoundToInt(after - before);
        }

        private static int ApplySoulVigorRewardMultiplier(int award)
        {
            award = Math.Max(0, award);
            if (award == 0)
            {
                return 0;
            }
            float multiplier = SoulAndServicePlugin
                .GetEffectiveBalanceTuning()
                .SoulVigorRewardMultiplier;
            return Math.Max(
                1,
                Mathf.FloorToInt(award * multiplier + 0.5f));
        }

        internal static float GetBindingProgress01(
            string corpseFingerprint,
            Grailwright.Shared.CorpseQualityTier tier)
        {
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return 0.0f;
            }
            EnsureInitialized(facts);
            string key = BindingKey(corpseFingerprint);
            if (facts.Get(key + ".bound", 0) != 0)
            {
                return 1.0f;
            }
            float resistance = GetBindingResistance(corpseFingerprint, tier);
            return resistance <= 0.0001f
                ? 0.0f
                : Mathf.Clamp01(facts.Get(key + ".progress", 0.0f) / resistance);
        }

        internal static float GetHighSoulBindingProgress01(
            string corpseFingerprint,
            int serviceCycle,
            float baseResistance)
        {
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return 0.0f;
            }
            EnsureInitialized(facts);
            string cycleFingerprint = corpseFingerprint
                + ":service-cycle:"
                + Math.Max(0, serviceCycle).ToString(
                    CultureInfo.InvariantCulture);
            string key = BindingKey(cycleFingerprint);
            if (facts.Get(key + ".bound", 0) != 0)
            {
                return 1.0f;
            }
            float resistance = Math.Max(1.0f, baseResistance)
                * Mathf.Lerp(
                    0.90f,
                    1.10f,
                    StableUnit(cycleFingerprint + ":resistance"));
            return Mathf.Clamp01(
                facts.Get(key + ".progress", 0.0f) / resistance);
        }

        internal static string GetBindingFailureMessage(
            string corpseFingerprint,
            int attempt)
        {
            uint value = StableHash(corpseFingerprint + ":message:" + attempt);
            return BindingFailureMessages[value % BindingFailureMessages.Length];
        }

        internal static void ShowBindingFailure(string text)
        {
            TryShowGft(
                "soul-binding-progress",
                text,
                "necro",
                "Normal",
                "soul-binding-progress");
        }

        internal static void ShowResurrection(
            string displayName,
            Grailwright.Shared.CorpseQualityTier tier,
            int vigorCost)
        {
            string text = displayName + " reanimated"
                + (vigorCost > 0
                    ? ": -" + vigorCost.ToString(CultureInfo.InvariantCulture)
                        + " Soul Vigor"
                    : string.Empty);
            TryShowGft(
                "soul-resurrection",
                text,
                GetCorpseIconId(tier),
                "High",
                string.Empty);
        }

        internal static void ShowSummonCreated(string displayName, int vigorCost)
        {
            if (vigorCost <= 0)
            {
                return;
            }
            TryShowGft(
                "soul-summoning",
                displayName + " summoned: -"
                    + vigorCost.ToString(CultureInfo.InvariantCulture)
                    + " Soul Vigor",
                "necro",
                "Normal",
                string.Empty,
                "Status",
                "Short");
        }

        internal static void ShowInsufficientSoulVigor(int vigorCost)
        {
            TryShowGft(
                "soul-vigor-required",
                "Requires " + vigorCost.ToString(CultureInfo.InvariantCulture)
                    + " Soul Vigor",
                "necro",
                "Normal",
                "soul-vigor-required",
                "Status",
                "Short",
                "Warning");
        }

        internal static void ShowSoulVigorHarvest(
            string displayName,
            Grailwright.Shared.CorpseQualityTier tier,
            float award,
            float manaReturned)
        {
            if (award <= 0.0f && manaReturned <= 0.0f)
            {
                return;
            }
            string text = string.Empty;
            if (manaReturned > 0.0f)
            {
                text = "+"
                    + manaReturned.ToString("0", CultureInfo.InvariantCulture)
                    + " Mana";
            }
            if (award > 0.0f)
            {
                if (text.Length > 0)
                {
                    text += " | ";
                }
                text += "+"
                    + award.ToString("0", CultureInfo.InvariantCulture)
                    + " Soul Vigor";
            }
            TryShowGft(
                "soul-vigor-harvest",
                text,
                tier == Grailwright.Shared.CorpseQualityTier.None
                    ? "necro"
                    : GetCorpseIconId(tier),
                "High",
                string.Empty,
                "Reward",
                "Short");

            if (award > 0.0f)
            {
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    float total = GetSoulVigor();
                    string source = tier
                            == Grailwright.Shared.CorpseQualityTier.None
                        ? "an ordinary servant"
                        : "a " + tier + " corpse";
                    plugin.LogDiagnostic(
                        "Gained "
                        + award.ToString("0.###", CultureInfo.InvariantCulture)
                        + " Soul Vigor from " + source
                        + "; total="
                        + total.ToString("0.###", CultureInfo.InvariantCulture)
                        + ", Necromantic Power="
                        + GetNecromanticPowerFromSoulVigor(total).ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                        + "; source=" + displayName + ".");
                }
            }
        }

        internal static void ShowCommandUnlocksAfterCorpseHarvest(
            CorpseHarvestReceipt receipt)
        {
            if (receipt == null || receipt.Award <= 0.0f)
            {
                return;
            }
            ShowSoulVigorThresholdMessages(
                receipt.BeforeSoulVigor,
                SaturatingAdd(receipt.BeforeSoulVigor, receipt.Award));
        }

        internal static void ShowCommandUnlocksAfterSummonHarvest(float award)
        {
            if (award <= 0.0f)
            {
                return;
            }
            ContextualFacts facts = GetFacts();
            if (facts == null)
            {
                return;
            }
            float after = Math.Max(0.0f, facts.Get(SoulVigorKey, 0.0f));
            ShowSoulVigorThresholdMessages(Math.Max(0.0f, after - award), after);
        }

        internal static void ShowSoulVigorWanesAfterSpend(int before, int after)
        {
            if (after >= before)
            {
                return;
            }
            ShowSoulVigorThresholdMessages(before, after);
        }

        private static void ShowSoulVigorThresholdMessages(float before, float after)
        {
            if (IsSoulVigorOverrideActive())
            {
                return;
            }
            float beforePower = GetNecromanticPowerFromSoulVigor(before);
            float afterPower = GetNecromanticPowerFromSoulVigor(after);
            if (beforePower < AttackCommandPower
                && afterPower >= AttackCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-attack",
                    "Necromantic Power rises: Attack commands are available.",
                    true);
            }
            if (beforePower >= AttackCommandPower
                && afterPower < AttackCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-attack",
                    "Necromantic Power wanes: Attack commands are unavailable.",
                    false);
            }
            if (beforePower < IndividualFormationPower
                && afterPower >= IndividualFormationPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-formation",
                    "Necromantic Power rises: individual Hold and Follow commands are available.",
                    true);
            }
            if (beforePower >= IndividualFormationPower
                && afterPower < IndividualFormationPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-formation",
                    "Necromantic Power wanes: individual Hold and Follow commands are unavailable.",
                    false);
            }
            if (beforePower < GlobalFormationPower
                && afterPower >= GlobalFormationPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-global",
                    "Necromantic Power rises: Hold All and Follow All are available.",
                    true);
            }
            if (beforePower >= GlobalFormationPower
                && afterPower < GlobalFormationPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-global",
                    "Necromantic Power wanes: Hold All and Follow All are unavailable.",
                    false);
            }
            if (beforePower < BehaviorCommandPower
                && afterPower >= BehaviorCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-behavior",
                    "Necromantic Power rises: Guard and Hunt behavior control is available; Summon Capacity bonus is +1.",
                    true);
            }
            if (beforePower >= BehaviorCommandPower
                && afterPower < BehaviorCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-behavior",
                    "Necromantic Power wanes: Guard and Hunt behavior control is unavailable; Summon Capacity bonus is lost.",
                    false);
            }
            if (beforePower < BulwarkBehaviorPower
                && afterPower >= BulwarkBehaviorPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-bulwark",
                    "Necromantic Power rises: Bulwark behavior is available.",
                    true);
            }
            if (beforePower >= BulwarkBehaviorPower
                && afterPower < BulwarkBehaviorPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-bulwark",
                    "Necromantic Power wanes: Bulwark is unavailable; Guard takes its place.",
                    false);
            }
            if (beforePower < RecallCommandPower
                && afterPower >= RecallCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-recall",
                    "Necromantic Power rises: Recall Host is available.",
                    true);
            }
            if (beforePower >= RecallCommandPower
                && afterPower < RecallCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-recall",
                    "Necromantic Power wanes: Recall Host is unavailable.",
                    false);
            }
            if (beforePower < SwarmCommandPower
                && afterPower >= SwarmCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-swarm",
                    "Necromantic Power rises: Swarm commands are available.",
                    true);
            }
            if (beforePower >= SwarmCommandPower
                && afterPower < SwarmCommandPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-swarm",
                    "Necromantic Power wanes: Swarm is unavailable; Attack takes its place.",
                    false);
            }
            if (beforePower < EmpowermentPower
                && afterPower >= EmpowermentPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-empowerment",
                    "Necromantic Power rises: Empower is available; servant upkeep ends; Summon Capacity bonus is +2.",
                    true);
            }
            if (beforePower >= EmpowermentPower
                && afterPower < EmpowermentPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-empowerment",
                    "Necromantic Power wanes: Empower is unavailable; servant upkeep resumes; Summon Capacity bonus is +1.",
                    false);
            }
            if (beforePower < MaximumSummonCapacityPower
                && afterPower >= MaximumSummonCapacityPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-summon-capacity",
                    "Necromantic Power rises: Summon Capacity bonus is +3.",
                    true);
            }
            if (beforePower >= MaximumSummonCapacityPower
                && afterPower < MaximumSummonCapacityPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-summon-capacity",
                    "Necromantic Power wanes: Summon Capacity bonus falls to +2.",
                    false);
            }
            if (beforePower < RaiseAllPower
                && afterPower >= RaiseAllPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-raise-all",
                    "Necromantic Power rises: Raise All is available.",
                    true);
            }
            if (beforePower >= RaiseAllPower
                && afterPower < RaiseAllPower)
            {
                ShowSoulVigorThresholdMessage(
                    "soul-command-raise-all",
                    "Necromantic Power wanes: Raise All is unavailable.",
                    false);
            }
        }

        private static void ShowSoulVigorThresholdMessage(
            string eventId,
            string text,
            bool rises)
        {
            TryShowGft(
                eventId + (rises ? "-rises" : "-wanes"),
                text,
                "necro",
                "High",
                string.Empty,
                rises ? "Reward" : "Status",
                "Medium");
        }

        private static bool IsSoulVigorOverrideActive()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin != null
                && plugin.OverrideSoulVigor != null
                && plugin.OverrideSoulVigor.Value;
        }

        internal static void ShowSoulClaimFeedback(
            string text,
            bool highPriority)
        {
            TryShowGft(
                "soul-claim",
                text,
                "necro",
                highPriority ? "High" : "Normal",
                "soul-claim");
        }

        internal static void ShowSummonCommand(string text)
        {
            TryShowGft(
                "summon-command",
                text,
                "necro",
                "Normal",
                "summon-command");
        }

        internal static void ShowServantSoulRendStage(
            string eventId,
            string summonId,
            string text)
        {
            string collapseKey = string.IsNullOrEmpty(summonId)
                ? "servant-soul-rend"
                : "servant-soul-rend-" + summonId;
            TryShowGft(
                eventId,
                text,
                "necro",
                "High",
                collapseKey,
                "Status",
                "Short");
        }

        internal static string GetCorpseIconId(
            Grailwright.Shared.CorpseQualityTier tier)
        {
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    return "corpse_worthy";
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    return "corpse_potent";
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    return "corpse_prime";
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    return "corpse_meager";
            }
        }

        private static float GetBindingResistance(
            string corpseFingerprint,
            Grailwright.Shared.CorpseQualityTier tier)
        {
            float baseline;
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    baseline = 70.0f;
                    break;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    baseline = 115.0f;
                    break;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    baseline = 170.0f;
                    break;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    baseline = 35.0f;
                    break;
            }
            float random01 = StableUnit(corpseFingerprint + ":resistance");
            return baseline * Mathf.Lerp(0.90f, 1.10f, random01);
        }

        private static float GetBindingIncrement(string corpseFingerprint, int attempt)
        {
            return Mathf.Lerp(
                30.0f,
                50.0f,
                StableUnit(
                    corpseFingerprint
                    + ":attempt:"
                    + attempt.ToString(CultureInfo.InvariantCulture)));
        }

        private static bool TryRestoreCorpseHarvest(CorpseHarvestReceipt receipt)
        {
            if (receipt == null || receipt.Facts == null)
            {
                return false;
            }
            try
            {
                receipt.Facts.Set(SoulVigorKey, receipt.BeforeSoulVigor);
                receipt.Facts.Set(
                    receipt.HarvestCountKey,
                    receipt.BeforeHarvestCount);
                receipt.Facts.Set(receipt.HarvestKey, receipt.BeforeHarvested);
                InvalidateReportedProgression();
                return true;
            }
            catch (Exception exception)
            {
                LogProgressionWarning(
                    "Soul Vigor rollback could not restore save facts: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private static bool TryRestoreHighSoulDrain(HighSoulDrainReceipt receipt)
        {
            if (receipt == null
                || receipt.Facts == null
                || string.IsNullOrEmpty(receipt.RemainingKey))
            {
                return false;
            }
            try
            {
                receipt.Facts.Set(SoulVigorKey, receipt.BeforeSoulVigor);
                receipt.Facts.Set(
                    receipt.RemainingKey,
                    Math.Max(0, receipt.BeforeRemaining));
                InvalidateReportedProgression();
                return true;
            }
            catch (Exception exception)
            {
                LogProgressionWarning(
                    "High-soul Vigor rollback could not restore save facts: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private static int GetBindingCount(
            ContextualFacts facts,
            Grailwright.Shared.CorpseQualityTier tier)
        {
            return facts == null
                ? 0
                : Math.Max(0, facts.Get(HarvestCountKey(tier), 0));
        }

        private static string HarvestCountKey(
            Grailwright.Shared.CorpseQualityTier tier)
        {
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    return WorthyHarvestsKey;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    return PotentHarvestsKey;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    return PrimeHarvestsKey;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    return MeagerHarvestsKey;
            }
        }

        private static string BindingKey(string corpseFingerprint)
        {
            return "soul_binding."
                + StableHash(corpseFingerprint).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static string HarvestKey(string corpseFingerprint)
        {
            return "harvest."
                + StableHash(corpseFingerprint).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static string CorpseSoulVigorKey(string corpseFingerprint)
        {
            return "native_soul."
                + StableHash(corpseFingerprint).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static string HighSoulPoolKey(string corpseFingerprint)
        {
            return "high_soul."
                + StableHash(corpseFingerprint).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static void AddSoulVigor(ContextualFacts facts, float award)
        {
            float before = Math.Max(
                0.0f,
                Mathf.Round(facts.Get(SoulVigorKey, 0.0f)));
            facts.Set(SoulVigorKey, SaturatingAdd(before, award));
            InvalidateReportedProgression();
        }

        private static void InvalidateReportedProgression()
        {
            _lastReportedSoulVigor = -1.0f;
            _lastReportedMeagerHarvests = -1;
            _lastReportedWorthyHarvests = -1;
            _lastReportedPotentHarvests = -1;
            _lastReportedPrimeHarvests = -1;
        }

        private static void LogProgressionWarning(string message)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogWarning(message);
            }
        }

        private static float StableUnit(string value)
        {
            return (StableHash(value) & 0x00FFFFFFu) / 16777215.0f;
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string safe = value ?? string.Empty;
                for (int i = 0; i < safe.Length; i++)
                {
                    hash ^= safe[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static float SaturatingAdd(float left, float right)
        {
            double sum = Math.Max(0.0f, left) + Math.Max(0.0f, right);
            return double.IsNaN(sum) || sum <= 0.0
                ? 0.0f
                : sum >= float.MaxValue ? float.MaxValue : (float)sum;
        }

        private static int SaturatingMultiply(int left, int right)
        {
            long product = (long)Math.Max(0, left) * Math.Max(0, right);
            return product >= int.MaxValue ? int.MaxValue : (int)product;
        }

        private static ContextualFacts GetFacts()
        {
            Services services = World.Services;
            GameplayMemory memory = services == null
                ? null
                : services.TryGet<GameplayMemory>();
            return memory == null ? null : memory.Context(MemoryContext);
        }

        private static void EnsureInitialized(ContextualFacts facts)
        {
            if (facts != null && facts.Get(InitializedKey, 0) == 0)
            {
                facts.Set(SoulVigorKey, Math.Max(0.0f, facts.Get(SoulVigorKey, 0.0f)));
                facts.Set(InitializedKey, 1);
            }
        }

        private static void ReportProgressionToDeeds(
            float soulVigor,
            int meager,
            int worthy,
            int potent,
            int prime)
        {
            ResolveDeedsBridge();
            if (_deedsRecordStatisticsMethod == null)
            {
                return;
            }
            try
            {
                object result = _deedsRecordStatisticsMethod.Invoke(
                    null,
                    new object[]
                    {
                        SoulAndServicePlugin.PluginGuid,
                        soulVigor,
                        GetNecromanticPowerFromSoulVigor(soulVigor),
                        meager,
                        worthy,
                        potent,
                        prime
                    });
                if (result is bool && (bool)result)
                {
                    _lastReportedSoulVigor = soulVigor;
                    _lastReportedMeagerHarvests = meager;
                    _lastReportedWorthyHarvests = worthy;
                    _lastReportedPotentHarvests = potent;
                    _lastReportedPrimeHarvests = prime;
                }
            }
            catch (Exception exception)
            {
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogWarning(
                        "Deeds of Avalon Soul Vigor reporting failed: "
                        + exception.GetBaseException().Message);
                }
                _deedsRecordStatisticsMethod = null;
            }
        }

        private static void ResolveDeedsBridge()
        {
            if (_deedsRecordStatisticsMethod != null || _deedsUnavailable)
            {
                return;
            }
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(DeedsPluginGuid, out info)
                || info == null
                || info.Instance == null)
            {
                return;
            }
            Type api = info.Instance.GetType().Assembly.GetType(
                DeedsApiTypeName,
                false);
            FieldInfo version = api == null
                ? null
                : api.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
            int apiVersion = version == null
                ? 0
                : Convert.ToInt32(
                    version.GetRawConstantValue(),
                    CultureInfo.InvariantCulture);
            if (apiVersion < 7)
            {
                _deedsUnavailable = true;
                return;
            }
            _deedsRecordStatisticsMethod = AccessTools.Method(
                api,
                "TryRecordSoulVigorStatistics",
                new[]
                {
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int)
                });
            _deedsUnavailable = _deedsRecordStatisticsMethod == null;
        }

        private static bool TryShowGft(
            string eventId,
            string text,
            string iconId,
            string priority,
            string collapseKey,
            string category = "Status",
            string durationBucket = "Medium",
            string style = "Necrotic")
        {
            ResolveGftBridge();
            if (_gftTryShowEventMethod == null)
            {
                return false;
            }
            try
            {
                object result = _gftTryShowEventMethod.Invoke(
                    null,
                    new object[]
                    {
                        SoulAndServicePlugin.PluginGuid,
                        eventId,
                        text,
                        style,
                        category,
                        priority,
                        collapseKey,
                        iconId,
                        durationBucket,
                        0.25f,
                        0.95f
                    });
                return result is bool && (bool)result;
            }
            catch
            {
                _gftTryShowEventMethod = null;
                return false;
            }
        }

        private static void ResolveGftBridge()
        {
            if (_gftTryShowEventMethod != null || _gftUnavailable)
            {
                return;
            }
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(
                    GrailFloatingTextPluginGuid,
                    out info)
                || info == null
                || info.Instance == null)
            {
                return;
            }
            Type api = info.Instance.GetType().Assembly.GetType(
                GrailFloatingTextApiTypeName,
                false);
            _gftTryShowEventMethod = api == null
                ? null
                : AccessTools.Method(
                    api,
                    "TryShowEvent",
                    new[]
                    {
                        typeof(string), typeof(string), typeof(string),
                        typeof(string), typeof(string), typeof(string),
                        typeof(string), typeof(string), typeof(string),
                        typeof(float), typeof(float)
                    });
            _gftUnavailable = _gftTryShowEventMethod == null;
        }
    }
}
