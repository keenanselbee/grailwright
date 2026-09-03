using System;
using System.Collections.Generic;
using System.Globalization;
using Awaken.TG.MVC;
using Awaken.TG.Main.AI.SummonsAndAllies;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Memories;
using UnityEngine;

namespace SoulAndService
{
    internal static class SoulforgedRuntime
    {
        private sealed class SoulforgedState
        {
            internal NpcHeroSummon Summon;
            internal float OriginalMaximumHealth;
            internal float DamageDealt;
            internal int EarnedRank;
        }

        private const string MemoryContext = "SoulAndService";
        internal const int MaximumRank = 17;
        private static readonly float[] DamageEquivalents =
        {
            2.0f, 4.0f, 6.0f, 8.0f, 11.0f, 14.0f, 17.0f, 20.0f,
            23.0f, 26.0f, 30.0f, 34.0f, 38.0f, 42.0f, 46.0f, 50.0f,
            54.0f
        };
        private static readonly string[] RomanRanks =
        {
            string.Empty, "I", "II", "III", "IV", "V", "VI", "VII",
            "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV",
            "XVI", "XVII"
        };
        private static readonly Dictionary<string, SoulforgedState> States =
            new Dictionary<string, SoulforgedState>();
        private static SoulforgedRankOverride _lastOverride =
            (SoulforgedRankOverride)(-2);
        private static bool _lastEnabled;
        private static float _nextPowerGateCheckAt;

        internal static void Update()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            bool enabled = plugin != null && plugin.IsEnabled;
            bool enabledChanged = enabled != _lastEnabled;
            _lastEnabled = enabled;
            SoulforgedRankOverride current = plugin == null
                || plugin.OverrideSoulforgedRank == null
                    ? SoulforgedRankOverride.Disabled
                    : plugin.OverrideSoulforgedRank.Value;
            bool overrideChanged = current != _lastOverride;
            if (!enabled)
            {
                if (enabledChanged)
                {
                    foreach (SoulforgedState state in States.Values)
                    {
                        RefreshPresentation(state);
                    }
                }
                _lastOverride = current;
                return;
            }
            if (!overrideChanged && Time.unscaledTime < _nextPowerGateCheckAt)
            {
                return;
            }
            _nextPowerGateCheckAt = Time.unscaledTime + 1.0f;
            _lastOverride = current;
            foreach (SoulforgedState state in States.Values)
            {
                AdvanceRanks(state, !overrideChanged);
                RefreshPresentation(state);
            }
        }

        internal static void Shutdown()
        {
            States.Clear();
            _lastOverride = (SoulforgedRankOverride)(-2);
            _lastEnabled = false;
            _nextPowerGateCheckAt = 0.0f;
        }

        internal static void OnSummonInitialized(NpcHeroSummon summon)
        {
            if (!IsOwnedSummon(summon))
            {
                return;
            }
            string id = ((Model)summon).ID;
            SoulforgedState state = new SoulforgedState
            {
                Summon = summon,
                OriginalMaximumHealth = ReadFloat(id, "maximum_health"),
                DamageDealt = Math.Max(0.0f, ReadFloat(id, "damage")),
                EarnedRank = Mathf.Clamp(ReadInt(id, "rank"), 0, MaximumRank)
            };
            States[id] = state;
            RefreshOriginalMaximumHealth(summon, state.OriginalMaximumHealth <= 0.0f);
            ModelExtensions.ListenTo(
                summon.ParentModel,
                HealthElement.Events.OnDamageDealt,
                outcome => OnDamageDealt(state, outcome),
                summon);
            float savedEmpowerment = ReadFloat(id, "empowerment");
            if (savedEmpowerment >= 1.20f)
            {
                SummonRuntime.TryEmpowerSummon(summon, savedEmpowerment);
            }
            summon.ParentModel.OnCompletelyInitialized(
                delegate
                {
                    RefreshOriginalMaximumHealth(summon, false);
                    RefreshPresentation(state);
                });
            RefreshPresentation(state);
        }

        internal static void OnSummonDiscarded(
            NpcHeroSummon summon,
            bool fromDomainDrop)
        {
            if (summon == null)
            {
                return;
            }
            string id = ((Model)summon).ID;
            States.Remove(id);
            if (!fromDomainDrop)
            {
                ClearSavedState(id);
            }
        }

        internal static void SaveEmpowerment(
            NpcHeroSummon summon,
            float combatMultiplier)
        {
            if (summon != null)
            {
                WriteFloat(
                    ((Model)summon).ID,
                    "empowerment",
                    Mathf.Clamp(combatMultiplier, 1.20f, 1.50f));
            }
        }

        internal static void ClearSavedEmpowerment(NpcHeroSummon summon)
        {
            if (summon != null)
            {
                WriteFloat(((Model)summon).ID, "empowerment", 0.0f);
            }
        }

        internal static void GetPersistenceState(
            NpcHeroSummon summon,
            out float originalMaximumHealth,
            out float damageDealt,
            out int earnedRank)
        {
            originalMaximumHealth = 0.0f;
            damageDealt = 0.0f;
            earnedRank = 0;
            if (summon == null)
            {
                return;
            }
            SoulforgedState state;
            if (States.TryGetValue(((Model)summon).ID, out state)
                && state != null)
            {
                originalMaximumHealth = Math.Max(
                    0.0f,
                    state.OriginalMaximumHealth);
                damageDealt = Math.Max(0.0f, state.DamageDealt);
                earnedRank = Mathf.Clamp(state.EarnedRank, 0, MaximumRank);
            }
        }

        internal static void RestorePersistenceState(
            NpcHeroSummon summon,
            float originalMaximumHealth,
            float damageDealt,
            int earnedRank,
            float empowermentMultiplier)
        {
            if (!IsOwnedSummon(summon))
            {
                return;
            }
            string id = ((Model)summon).ID;
            SoulforgedState state;
            if (!States.TryGetValue(id, out state) || state == null)
            {
                OnSummonInitialized(summon);
                if (!States.TryGetValue(id, out state) || state == null)
                {
                    return;
                }
            }
            state.OriginalMaximumHealth = originalMaximumHealth > 0.0f
                ? originalMaximumHealth
                : summon.ParentModel.Health == null
                    ? 0.0f
                    : summon.ParentModel.Health.UpperLimit;
            state.DamageDealt = Math.Max(0.0f, damageDealt);
            state.EarnedRank = Mathf.Clamp(earnedRank, 0, MaximumRank);
            WriteFloat(id, "maximum_health", state.OriginalMaximumHealth);
            WriteFloat(id, "damage", state.DamageDealt);
            WriteInt(id, "rank", state.EarnedRank);
            if (empowermentMultiplier >= 1.20f)
            {
                SummonRuntime.TryEmpowerSummon(
                    summon,
                    Mathf.Clamp(empowermentMultiplier, 1.20f, 1.50f));
            }
            else
            {
                WriteFloat(id, "empowerment", 0.0f);
            }
            AdvanceRanks(state, false);
            RefreshPresentation(state);
        }

        internal static void RefreshOriginalMaximumHealth(
            NpcHeroSummon summon,
            bool force)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return;
            }
            string id = ((Model)summon).ID;
            SoulforgedState state;
            if (!States.TryGetValue(id, out state))
            {
                return;
            }
            float maximum = summon.ParentModel.Health == null
                ? 0.0f
                : summon.ParentModel.Health.UpperLimit;
            if (maximum <= 0.0f
                || (!force && state.OriginalMaximumHealth > 0.0f))
            {
                return;
            }
            state.OriginalMaximumHealth = maximum;
            WriteFloat(id, "maximum_health", maximum);
            AdvanceRanks(state, false);
        }

        internal static int GetEffectiveRank(string summonId)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null || !plugin.IsEnabled)
            {
                return 0;
            }
            if (plugin.OverrideSoulforgedRank != null
                && plugin.OverrideSoulforgedRank.Value
                    != SoulforgedRankOverride.Disabled)
            {
                return Mathf.Clamp(
                    (int)plugin.OverrideSoulforgedRank.Value,
                    0,
                    MaximumRank);
            }
            SoulforgedState state;
            return !string.IsNullOrEmpty(summonId)
                && States.TryGetValue(summonId, out state)
                    ? state.EarnedRank
                    : 0;
        }

        internal static float GetMultiplier(string summonId)
        {
            return 1.0f + (GetEffectiveRank(summonId) * 0.01f);
        }

        internal static float GetVisualSizeMultiplier(string summonId)
        {
            return 1.0f + (GetEffectiveRank(summonId) * 0.005f);
        }

        internal static int GetRealRank(NpcHeroSummon summon)
        {
            if (summon == null)
            {
                return 0;
            }
            SoulforgedState state;
            return States.TryGetValue(((Model)summon).ID, out state)
                && state != null
                    ? Mathf.Clamp(state.EarnedRank, 0, MaximumRank)
                    : 0;
        }

        internal static bool TryReduceRealRanks(
            NpcHeroSummon summon,
            int amount,
            out int previousRank,
            out int currentRank)
        {
            previousRank = GetRealRank(summon);
            currentRank = previousRank;
            if (summon == null || amount <= 0 || previousRank <= 0)
            {
                return false;
            }
            string id = ((Model)summon).ID;
            SoulforgedState state;
            if (!States.TryGetValue(id, out state) || state == null)
            {
                return false;
            }

            currentRank = Math.Max(0, previousRank - amount);
            state.EarnedRank = currentRank;
            state.DamageDealt = currentRank <= 0
                || state.OriginalMaximumHealth <= 0.0f
                    ? 0.0f
                    : state.OriginalMaximumHealth
                        * DamageEquivalents[currentRank - 1];
            WriteInt(id, "rank", state.EarnedRank);
            WriteFloat(id, "damage", state.DamageDealt);
            RefreshPresentation(state);
            return true;
        }

        internal static string GetRankLabel(int rank)
        {
            int clamped = Mathf.Clamp(rank, 0, MaximumRank);
            return clamped <= 0 ? "Base" : RomanRanks[clamped];
        }

        internal static float GetVisualPotential(string summonId, bool empowered)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null || !plugin.IsEnabled)
            {
                return 0.0f;
            }
            return Mathf.Clamp01(
                0.75f * (GetEffectiveRank(summonId) / (float)MaximumRank)
                + (empowered ? 0.25f : 0.0f));
        }

        internal static bool TryGetHoverText(
            NpcHeroSummon summon,
            out string title,
            out string detail)
        {
            title = string.Empty;
            detail = string.Empty;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !IsOwnedSummon(summon)
                || summon.ParentModel.Health == null)
            {
                return false;
            }
            string id = ((Model)summon).ID;
            int rank = GetEffectiveRank(id);
            string displayName = SoulSalvageRuntime.GetSummonDisplayName(summon);
            title = rank <= 0
                ? displayName
                : displayName + " [" + RomanRanks[rank] + "]";
            int healthPercent = Mathf.Clamp(
                Mathf.RoundToInt(summon.ParentModel.Health.Percentage * 100.0f),
                0,
                100);
            bool overridden = plugin != null
                && plugin.OverrideSoulforgedRank != null
                && plugin.OverrideSoulforgedRank.Value
                    != SoulforgedRankOverride.Disabled;
            string progress = overridden
                ? rank <= 0
                    ? "Unranked"
                    : RomanRanks[rank]
                : rank >= MaximumRank
                    ? "MAX"
                    : GetRankProgressPercent(id).ToString(
                        CultureInfo.InvariantCulture) + "%";
            detail = "HP: " + healthPercent.ToString(CultureInfo.InvariantCulture)
                + "% | Rank: " + progress;
            return true;
        }

        private static void OnDamageDealt(
            SoulforgedState state,
            DamageOutcome outcome)
        {
            NpcHeroSummon summon = state == null ? null : state.Summon;
            NpcElement target = outcome.TargetPure as NpcElement;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || SoulProgressionRuntime.GetNecromanticPower() < 40.0f
                || !IsOwnedSummon(summon)
                || outcome.FinalAmount <= 0.0f
                || target == null
                || target.HasBeenDiscarded
                || target.NpcType == NpcType.HeroSummon
                || !WithFactionUtils.WantToFight(summon.ParentModel, target))
            {
                return;
            }
            state.DamageDealt += outcome.FinalAmount;
            string id = ((Model)summon).ID;
            WriteFloat(id, "damage", state.DamageDealt);
            AdvanceRanks(state, true);
        }

        private static void AdvanceRanks(
            SoulforgedState state,
            bool notify)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || state == null
                || state.OriginalMaximumHealth <= 0.0f)
            {
                return;
            }
            int previous = state.EarnedRank;
            float power = SoulProgressionRuntime.GetNecromanticPower();
            while (state.EarnedRank < MaximumRank)
            {
                int next = state.EarnedRank + 1;
                float requiredDamage = state.OriginalMaximumHealth
                    * DamageEquivalents[next - 1];
                float requiredPower = 30.0f + (next * 10.0f);
                if (state.DamageDealt + 0.001f < requiredDamage
                    || power + 0.001f < requiredPower)
                {
                    break;
                }
                state.EarnedRank = next;
            }
            if (state.EarnedRank == previous)
            {
                return;
            }
            string id = ((Model)state.Summon).ID;
            WriteInt(id, "rank", state.EarnedRank);
            RefreshPresentation(state);
            if (notify)
            {
                string name = SoulSalvageRuntime.GetSummonDisplayName(state.Summon);
                string transition = previous <= 0
                    ? RomanRanks[state.EarnedRank]
                    : RomanRanks[previous] + " -> " + RomanRanks[state.EarnedRank];
                SoulProgressionRuntime.ShowSummonCommand(
                    name + ": Soulforged " + transition);
            }
        }

        private static int GetRankProgressPercent(string id)
        {
            SoulforgedState state;
            if (!States.TryGetValue(id, out state)
                || state.OriginalMaximumHealth <= 0.0f
                || state.EarnedRank >= MaximumRank)
            {
                return state != null && state.EarnedRank >= MaximumRank ? 100 : 0;
            }
            float previous = state.EarnedRank <= 0
                ? 0.0f
                : state.OriginalMaximumHealth
                    * DamageEquivalents[state.EarnedRank - 1];
            float next = state.OriginalMaximumHealth
                * DamageEquivalents[state.EarnedRank];
            return Mathf.Clamp(
                Mathf.RoundToInt(Mathf.InverseLerp(previous, next, state.DamageDealt)
                    * 100.0f),
                0,
                100);
        }

        private static void RefreshPresentation(SoulforgedState state)
        {
            if (state == null || !IsOwnedSummon(state.Summon))
            {
                return;
            }
            SummonRuntime.RefreshSoulforgedPresentation(state.Summon);
            ReanimationGlyphRuntime.RefreshForSoulforged(state.Summon);
        }

        private static bool IsOwnedSummon(NpcHeroSummon summon)
        {
            return summon != null
                && !((Model)summon).HasBeenDiscarded
                && summon.ParentModel != null
                && !summon.ParentModel.HasBeenDiscarded
                && summon.ParentModel.IsAlive
                && ReferenceEquals(summon.Ally, Hero.Current);
        }

        private static ContextualFacts GetFacts()
        {
            GameplayMemory memory = World.Services == null
                ? null
                : World.Services.TryGet<GameplayMemory>();
            return memory == null ? null : memory.Context(MemoryContext);
        }

        private static string Key(string id, string value)
        {
            return "soulforged." + id + "." + value;
        }

        private static float ReadFloat(string id, string value)
        {
            ContextualFacts facts = GetFacts();
            return facts == null ? 0.0f : facts.Get(Key(id, value), 0.0f);
        }

        private static int ReadInt(string id, string value)
        {
            ContextualFacts facts = GetFacts();
            return facts == null ? 0 : facts.Get(Key(id, value), 0);
        }

        private static void WriteFloat(string id, string value, float amount)
        {
            ContextualFacts facts = GetFacts();
            if (facts != null)
            {
                facts.Set(Key(id, value), amount);
            }
        }

        private static void WriteInt(string id, string value, int amount)
        {
            ContextualFacts facts = GetFacts();
            if (facts != null)
            {
                facts.Set(Key(id, value), amount);
            }
        }

        private static void ClearSavedState(string id)
        {
            WriteFloat(id, "maximum_health", 0.0f);
            WriteFloat(id, "damage", 0.0f);
            WriteFloat(id, "empowerment", 0.0f);
            WriteInt(id, "rank", 0);
        }
    }
}
