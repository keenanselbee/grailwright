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

        private const string MemoryContext = "SoulAndService";
        private const string InitializedKey = "soul_vigor.initialized";
        private const string SoulVigorKey = "soul_vigor.total";
        private const string MeagerHarvestsKey = "soul_vigor.harvests.meager";
        private const string WorthyHarvestsKey = "soul_vigor.harvests.worthy";
        private const string PotentHarvestsKey = "soul_vigor.harvests.potent";
        private const string PrimeHarvestsKey = "soul_vigor.harvests.prime";
        private const string SummonBehaviorKey = "soul_vigor.summon_behavior";
        private const float SoulVigorAtNormalMaximumPower = 1000.0f;
        private const float SoulVigorAtAbsoluteMaximumPower = 5000.0f;
        internal const float AttackCommandPower = 10.0f;
        internal const float IndividualFormationPower = 20.0f;
        internal const float GlobalFormationPower = 30.0f;
        internal const float BehaviorCommandPower = 50.0f;
        internal const float RecallCommandPower = 70.0f;
        internal const float SwarmCommandPower = 90.0f;
        internal const float EmpowermentPower = 100.0f;
        internal const float MaximumCommandCapacityPower = 150.0f;
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
        private static readonly string[] SoulClaimFailureMessages =
        {
            "The soul recoils from your grasp.",
            "The living spirit knots itself against your command.",
            "Your claim slips from the wounded soul.",
            "The mortal tether bends, but does not break.",
            "The soul shudders and clings to its flesh."
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
            if (GetNecromanticPower() < BehaviorCommandPower)
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
            return stored >= (int)SummonBehavior.Guard
                && stored <= (int)SummonBehavior.Hunt
                    ? (SummonBehavior)stored
                    : SummonBehavior.Guard;
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
            SummonBehavior current = GetSummonBehavior();
            behavior = current == SummonBehavior.Guard
                ? SummonBehavior.Bulwark
                : current == SummonBehavior.Bulwark
                    ? SummonBehavior.Hunt
                    : SummonBehavior.Guard;
            facts.Set(SummonBehaviorKey, (int)behavior);
            return true;
        }

        internal static float GetSummonDamageMultiplier()
        {
            float power = GetNecromanticPower();
            return power <= 100.0f
                ? Mathf.Lerp(0.75f, 1.25f, power / 100.0f)
                : Mathf.Lerp(1.25f, 1.50f, (power - 100.0f) / 100.0f);
        }

        internal static float GetSummonDamageTakenMultiplier()
        {
            float power = GetNecromanticPower();
            return power <= 100.0f
                ? Mathf.Lerp(1.25f, 0.75f, power / 100.0f)
                : Mathf.Lerp(0.75f, 0.50f, (power - 100.0f) / 100.0f);
        }

        internal static int GetProgressionSummonLimitBonus()
        {
            float power = GetNecromanticPower();
            if (power >= MaximumCommandCapacityPower)
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
            return UnityEngine.Random.Range(lower, upper);
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
            progress01 = 0.0f;
            resistance = GetBindingResistance(corpseFingerprint, tier);
            ContextualFacts facts = GetFacts();
            if (facts == null || string.IsNullOrEmpty(corpseFingerprint))
            {
                return false;
            }

            EnsureInitialized(facts);
            string key = BindingKey(corpseFingerprint);
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
                progress += GetBindingIncrement(corpseFingerprint, attempt)
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
                RollSoulVigorValue(tier, quality01),
                out receipt);
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
            Grailwright.Shared.CorpseQualityTier tier)
        {
            string text = displayName + " reanimated";
            TryShowGft(
                "soul-resurrection",
                text,
                GetCorpseIconId(tier),
                "High",
                string.Empty);
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
            ShowCommandUnlocks(
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
            ShowCommandUnlocks(Math.Max(0.0f, after - award), after);
        }

        private static void ShowCommandUnlocks(float before, float after)
        {
            float beforePower = GetNecromanticPowerFromSoulVigor(before);
            float afterPower = GetNecromanticPowerFromSoulVigor(after);
            if (beforePower < AttackCommandPower
                && afterPower >= AttackCommandPower)
            {
                TryShowGft(
                    "soul-command-attack-unlocked",
                    "Your servants heed your command: Attack.",
                    "necro",
                    "High",
                    string.Empty);
            }
            if (beforePower < IndividualFormationPower
                && afterPower >= IndividualFormationPower)
            {
                TryShowGft(
                    "soul-command-formation-unlocked",
                    "Your will can anchor a single servant: Hold and Follow.",
                    "necro",
                    "High",
                    string.Empty);
            }
            if (beforePower < GlobalFormationPower
                && afterPower >= GlobalFormationPower)
            {
                TryShowGft(
                    "soul-command-global-unlocked",
                    "Your command reaches the whole host: Hold All and Follow All.",
                    "necro",
                    "High",
                    string.Empty);
            }
            if (beforePower < BehaviorCommandPower
                && afterPower >= BehaviorCommandPower)
            {
                TryShowGft(
                    "soul-command-behavior-unlocked",
                    "Your will shapes the host: Guard, Bulwark, and Hunt.",
                    "necro",
                    "High",
                    string.Empty);
            }
            if (beforePower < RecallCommandPower
                && afterPower >= RecallCommandPower)
            {
                TryShowGft(
                    "soul-command-recall-unlocked",
                    "Your will can recall the scattered host.",
                    "necro",
                    "High",
                    string.Empty);
            }
            if (beforePower < SwarmCommandPower
                && afterPower >= SwarmCommandPower)
            {
                TryShowGft(
                    "soul-command-swarm-unlocked",
                    "Your host surges at your command: Swarm.",
                    "necro",
                    "High",
                    string.Empty);
            }
            if (beforePower < EmpowermentPower
                && afterPower >= EmpowermentPower)
            {
                TryShowGft(
                    "soul-empowerment-unlocked",
                    "Your will sustains the host and can Empower a servant.",
                    "necro",
                    "High",
                    string.Empty);
            }
            if (beforePower < MaximumCommandCapacityPower
                && afterPower >= MaximumCommandCapacityPower)
            {
                TryShowGft(
                    "soul-command-capacity-mastered",
                    "Your overmastered will can sustain a still greater host.",
                    "necro",
                    "High",
                    string.Empty);
            }
        }

        internal static string GetSoulClaimFailureMessage()
        {
            return SoulClaimFailureMessages[UnityEngine.Random.Range(
                0,
                SoulClaimFailureMessages.Length)];
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
            string durationBucket = "Medium")
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
                        "Necrotic",
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
