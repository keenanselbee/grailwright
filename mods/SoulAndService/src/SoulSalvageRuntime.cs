using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Awaken.TG.Assets;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.Main.AI.SummonsAndAllies;
using Awaken.TG.Main.AI.Utils;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.General.Configs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.LootTables;
using Awaken.TG.Main.Heroes.Items.Tooltips;
using Awaken.TG.Main.Heroes.Items.Weapons;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Thievery;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Actions;
using Awaken.TG.Main.Locations.Attachments.Elements;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Locations.Views;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace SoulAndService
{
    internal static class SoulSalvageRuntime
    {
        private const string SoulSalvageTemplateGuid =
            "7bdd3a1b62fb53d46b8a28142c18a110";
        private const string SoulRendDisplayName = "Soul Rend";
        private const string GenericRaisedServantPortraitKey =
            "759a3e6e96ddae742ab8cde19fae42f0";
        private const string SkeletonSummonVfxKey =
            "0d139743aa2c21d4da0c81fb4e609890";
        private const float NativeManaRefundMultiplier = 0.75f;
        private const float HeavyCastManaCostMultiplier = 2.0f;
        private const float ServantEmpowerHealthThreshold = 0.95f;
        private const float ServantHealingPowerZeroFraction = 0.20f;
        private const float ServantHealingPowerMaximumFraction = 0.50f;
        private const float RaisedSalvageMinimumQualityFactor = 0.65f;
        private const float RaisedSalvageMaximumQualityFactor = 1.50f;
        private const float RaisedSalvageMaximumRefundFraction = 0.75f;
        private const float SoulSalvageRange = 50.0f;
        private const float ReanimationPositionRefreshSeconds = 0.10f;
        private const float ComparableLightSpellBaseDamage = 5.0f;
        private const float SoulRendPowerZeroMultiplier = 0.50f;
        private const float SoulRendPowerNormalMultiplier = 1.00f;
        private const float SoulRendPowerMaximumMultiplier = 2.00f;
        private const float FrayedSoulDurationSeconds = 8.0f;
        private const int FrayedSoulMaximumStacks = 3;
        private const float FrayedSoulChanceBonusPerStack = 0.10f;
        private const float SoulClaimHealthThreshold = 0.40f;
        private const float SoulClaimPowerZeroChance = 0.05f;
        private const float SoulClaimPowerNormalChance = 0.175f;
        private const float SoulClaimPowerMaximumChance = 0.30f;
        private const float SoulClaimAbsoluteChanceCap = 0.35f;

        private sealed class ReanimationRecord
        {
            internal Location SourceCorpse;
            internal Location RaisedLocation;
            internal NpcElement RaisedNpc;
            internal LocationInteractability SourceInteractability;
            internal string SourceDisplayName;
            internal string CorpseFingerprint;
            internal float Quality01;
            internal Grailwright.Shared.CorpseQualityTier QualityTier;
            internal float BindingManaCost;
            internal float ManaReturnedOnSacrifice;
            internal StatTweak QualityHealthTweak;
            internal Vector3 OriginalCoords;
            internal Quaternion OriginalRotation;
            internal Vector3 LastSafeCoords;
            internal Quaternion LastSafeRotation;
            internal bool Sacrificed;
            internal bool DismissedAsRemains;
        }

        private sealed class FrayedSoulState
        {
            internal int Stacks;
            internal float ExpiresAt;
        }

        private sealed class NecroticDamageMarker
        {
        }

        private struct CastState
        {
            internal bool IsSoulSalvage;
            internal bool IsHeavy;
            internal Item Item;
        }

        private static readonly Dictionary<string, ReanimationRecord> Reanimations =
            new Dictionary<string, ReanimationRecord>();
        private static readonly Dictionary<string, FrayedSoulState> FrayedSouls =
            new Dictionary<string, FrayedSoulState>();
        private static readonly ConditionalWeakTable<Damage, NecroticDamageMarker>
            NecroticDamageMarkers =
                new ConditionalWeakTable<Damage, NecroticDamageMarker>();
        private static int _focusedTargetCacheFrame = -1;
        private static bool _focusedTargetCacheFound;
        private static Location _focusedTargetCacheLocation;
        private static NpcHeroSummon _focusedTargetCacheSummon;
        private static readonly List<Location> PendingRaisedDiscards =
            new List<Location>();
        private static readonly Dictionary<string, ItemStats> SoulSalvageItems =
            new Dictionary<string, ItemStats>();
        private static readonly Dictionary<string, StatTweak> HeavyCostTweaks =
            new Dictionary<string, StatTweak>();
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>>
            OptionalPropertyCache =
                new Dictionary<Type, Dictionary<string, PropertyInfo>>();
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>>
            OptionalFieldCache =
                new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static readonly HashSet<MagicItemTemplateInfo> LightCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private static readonly HashSet<MagicItemTemplateInfo> HeavyCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private static readonly FieldInfo LocationInitializerField =
            AccessTools.Field(typeof(Location), "_initializer");
        private static readonly FieldInfo SimplifiedDeadBodyReplacementField =
            AccessTools.Field(typeof(NpcDummy), "_simplifiedDeadBodyReplacementRef");

        private static bool _lightCastActive;
        private static bool _heavyCastActive;
        private static bool _lightHarvestCompleted;
        private static NpcHeroSummon _lightTarget;
        private static NpcHeroSummon _heavyTarget;
        private static float _lightOriginalMana;
        private static float _lightHealthFraction;
        private static float _lightMaximumManaReturn;
        private static float _itemRefreshDelay;
        private static float _nextReanimationPositionRefreshTime;

        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                RequireMethod(
                    typeof(VHeroController),
                    "CastingBegun",
                    new[] { typeof(CastingHand), typeof(bool) }),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeCastingBegun)));
            harmony.Patch(
                RequireMethod(
                    typeof(VHeroController),
                    "CastingCanceled",
                    new[]
                    {
                        typeof(CastingHand),
                        typeof(Item),
                        typeof(bool),
                        typeof(bool)
                    }),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeCastingCanceled)));
            harmony.Patch(
                RequireMethod(
                    typeof(VHeroController),
                    "CastingEnded",
                    new[] { typeof(CastingHand), typeof(bool) }),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeCastingEnded)),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterCastingEnded)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "get_ManaExpended"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeGetManaExpended)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "Destroy"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeDestroySummon)));
            harmony.Patch(
                RequireMethod(typeof(NpcElement), "Destroy"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeDestroyHeavyTarget)));
            harmony.Patch(
                RequireMethod(typeof(HealthElement), "Kill"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeKillHeavyTarget)));
            harmony.Patch(
                RequireMethod(typeof(ItemStats), "OnInitialize"),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterItemStatsInitialized)));
            harmony.Patch(
                RequireMethod(typeof(Item), "get_DisplayName"),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterGetItemDisplayName)));
            harmony.Patch(
                RequireMethod(typeof(MagicItemTemplateInfo), "get_MagicDescription"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeGetMagicDescription)));
        }

        internal static void Update()
        {
            UpdateSoulSalvageItems();
            UpdateReanimationPositions();
            RemoveExpiredFrayedSouls();

            if (PendingRaisedDiscards.Count > 0)
            {
                Location[] pending = PendingRaisedDiscards.ToArray();
                PendingRaisedDiscards.Clear();
                foreach (Location location in pending)
                {
                    if (location != null && !location.HasBeenDiscarded)
                    {
                        location.Discard();
                    }
                }
            }
        }

        internal static void Shutdown()
        {
            foreach (string id in Reanimations.Keys.ToArray())
            {
                RestoreSourceCorpse(
                    id,
                    discardRaisedCopy: true,
                    showDiagnostic: false);
            }
            Update();
            foreach (StatTweak tweak in HeavyCostTweaks.Values.ToArray())
            {
                if (tweak != null && !((Model)tweak).HasBeenDiscarded)
                {
                    tweak.Discard();
                }
            }
            HeavyCostTweaks.Clear();
            SoulSalvageItems.Clear();
            LightCastInfos.Clear();
            HeavyCastInfos.Clear();
            FrayedSouls.Clear();
            ClearLightCastState();
            SoulSalvageAudioRuntime.Shutdown();
        }

        internal static void OnSummonDiscarded(NpcHeroSummon summon)
        {
            if (summon == null)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            ReanimationRecord record;
            if (!Reanimations.TryGetValue(summonId, out record))
            {
                return;
            }
            bool endedInWorld = record.Sacrificed
                || record.DismissedAsRemains
                || (record.RaisedNpc != null && !record.RaisedNpc.IsAlive);
            if (endedInWorld)
            {
                EndServiceAsRemains(summonId, showDiagnostic: true);
            }
            else
            {
                RestoreSourceCorpse(
                    summonId,
                    discardRaisedCopy: true,
                    showDiagnostic: false);
            }
        }

        private static void AfterItemStatsInitialized(ItemStats __instance)
        {
            if (__instance == null || !IsSoulSalvageItem(__instance.ParentModel))
            {
                return;
            }

            Item item = __instance.ParentModel;
            string itemId = ((Model)item).ID;
            RemoveHeavyCostTweak(itemId);
            SoulSalvageItems[itemId] = __instance;
            LightCastInfos.Add(item.LightCastInfo);
            HeavyCastInfos.Add(item.HeavyCastInfo);
            EnsureHeavyCostTweak(itemId, __instance);
        }

        private static void AfterGetItemDisplayName(Item __instance, ref string __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value
                && IsSoulSalvageItem(__instance))
            {
                __result = SoulRendDisplayName;
            }
        }

        private static bool BeforeGetMagicDescription(
            MagicItemTemplateInfo __instance,
            ref string __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value)
            {
                return true;
            }

            if (LightCastInfos.Contains(__instance))
            {
                __result = "Corpses: Harvest for Soul Vigor.\n"
                    + "Servants: Unbind to restore Mana and harvest Soul Vigor."
                    + (plugin.LivingTargetSoulSalvage.Value
                        ? "\nEnemies: Deal Necrotic damage. Repeated hits strengthen Soul Claim."
                        : string.Empty);
                return false;
            }

            if (!HeavyCastInfos.Contains(__instance))
            {
                return true;
            }

            __result = "Corpses: Bind and reanimate."
                + (plugin.LivingTargetSoulSalvage.Value
                    ? "\nWounded enemies: Attempt Soul Claim below 40% Health."
                    : string.Empty)
                + "\nServants: Restore Health; at 95%, Empower at 1,000 Soul Vigor.";
            return false;
        }

        private static void UpdateSoulSalvageItems()
        {
            _itemRefreshDelay -= Time.deltaTime;
            if (_itemRefreshDelay <= 0.0f)
            {
                _itemRefreshDelay = 2.0f;
                foreach (ItemStats stats in World.All<ItemStats>())
                {
                    if (stats != null && IsSoulSalvageItem(stats.ParentModel))
                    {
                        Item item = stats.ParentModel;
                        string itemId = ((Model)item).ID;
                        if (!SoulSalvageItems.ContainsKey(itemId))
                        {
                            SoulSalvageItems[itemId] = stats;
                            LightCastInfos.Add(item.LightCastInfo);
                            HeavyCastInfos.Add(item.HeavyCastInfo);
                        }
                    }
                }
            }

            foreach (string itemId in SoulSalvageItems.Keys.ToArray())
            {
                ItemStats stats = SoulSalvageItems[itemId];
                if (stats == null
                    || ((Model)stats).HasBeenDiscarded
                    || stats.ParentModel == null
                    || ((Model)stats.ParentModel).HasBeenDiscarded)
                {
                    RemoveHeavyCostTweak(itemId);
                    SoulSalvageItems.Remove(itemId);
                    continue;
                }
                EnsureHeavyCostTweak(itemId, stats);
            }
        }

        private static void EnsureHeavyCostTweak(string itemId, ItemStats stats)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            bool shouldApply = plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value;
            StatTweak existing;
            if (HeavyCostTweaks.TryGetValue(itemId, out existing))
            {
                if (existing != null
                    && !((Model)existing).HasBeenDiscarded
                    && shouldApply)
                {
                    return;
                }
                RemoveHeavyCostTweak(itemId);
            }
            if (!shouldApply || stats == null || stats.HeavyCastManaCost == null)
            {
                return;
            }

            StatTweak tweak = StatTweak.Multi(
                stats.HeavyCastManaCost,
                HeavyCastManaCostMultiplier,
                null,
                stats.ParentModel);
            ((Model)tweak).MarkedNotSaved = true;
            HeavyCostTweaks[itemId] = tweak;
        }

        private static void RemoveHeavyCostTweak(string itemId)
        {
            StatTweak tweak;
            if (HeavyCostTweaks.TryGetValue(itemId, out tweak))
            {
                HeavyCostTweaks.Remove(itemId);
                if (tweak != null && !((Model)tweak).HasBeenDiscarded)
                {
                    tweak.Discard();
                }
            }
        }

        private static bool IsSoulSalvageItem(Item item)
        {
            return item != null
                && item.Template != null
                && string.Equals(
                    item.Template.GUID,
                    SoulSalvageTemplateGuid,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNecroticDamageForInterop(object damage)
        {
            Damage typedDamage = damage as Damage;
            if (typedDamage == null)
            {
                return false;
            }

            NecroticDamageMarker marker;
            return NecroticDamageMarkers.TryGetValue(typedDamage, out marker);
        }

        private static void MarkNecroticDamage(Damage damage)
        {
            if (damage != null)
            {
                NecroticDamageMarkers.GetValue(
                    damage,
                    ignored => new NecroticDamageMarker());
            }
        }

        private static void BeforeCastingBegun(
            CastingHand hand,
            bool lightCast)
        {
            BeginSoulRendCast(hand, lightCast);
        }

        private static void BeforeCastingCanceled(Item castingItem)
        {
            if (_lightCastActive
                || _heavyCastActive
                || IsSoulSalvageItem(castingItem))
            {
                ClearLightCastState();
            }
        }

        private static void BeforeCastingEnded(
            CastingHand hand,
            bool lightCast,
            out CastState __state)
        {
            __state = default(CastState);
            Item item;
            if (!TryGetSoulRendCastItem(hand, out item))
            {
                ClearLightCastState();
                return;
            }
            if (_lightCastActive != lightCast
                || _heavyCastActive == lightCast)
            {
                BeginSoulRendCast(hand, lightCast);
            }

            __state = new CastState
            {
                IsSoulSalvage = true,
                IsHeavy = !lightCast,
                Item = item
            };
            if (!lightCast && _heavyTarget == null)
            {
                TryCaptureFocusedHeavyTarget();
            }
        }

        private static void BeginSoulRendCast(
            CastingHand hand,
            bool lightCast)
        {
            Item item;
            if (!TryGetSoulRendCastItem(hand, out item))
            {
                ClearLightCastState();
                return;
            }
            _lightCastActive = lightCast;
            _heavyCastActive = !lightCast;
            _lightHarvestCompleted = false;
            _lightTarget = null;
            _heavyTarget = null;
            _lightOriginalMana = 0.0f;
            _lightHealthFraction = 0.0f;
            _lightMaximumManaReturn = float.PositiveInfinity;
            if (!lightCast)
            {
                TryCaptureFocusedHeavyTarget();
            }
        }

        private static bool TryGetSoulRendCastItem(
            CastingHand hand,
            out Item item)
        {
            item = null;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || hero == null)
            {
                return false;
            }
            EquipmentSlotType slot = hand == CastingHand.OffHand
                ? EquipmentSlotType.OffHand
                : EquipmentSlotType.MainHand;
            item = hero.HeroItems.EquippedItem(slot);
            return IsSoulSalvageItem(item);
        }

        private static void TryCaptureFocusedHeavyTarget()
        {
            Location focusedLocation;
            NpcHeroSummon focusedSummon;
            if (TryFindFocusedSoulTargetCached(
                    out focusedLocation,
                    out focusedSummon)
                && focusedSummon != null)
            {
                _heavyTarget = focusedSummon;
            }
        }

        private static void AfterCastingEnded(CastState __state)
        {
            try
            {
                if (__state.IsSoulSalvage && __state.IsHeavy)
                {
                    if (_heavyTarget != null)
                    {
                        TryServeHeavyTarget(_heavyTarget);
                    }
                    else
                    {
                        TryUseHeavyCast(__state.Item);
                    }
                }
                else if (__state.IsSoulSalvage
                    && !_lightHarvestCompleted
                    && _lightTarget != null)
                {
                    CompleteLightSummonHarvest(_lightTarget);
                }
                else if (__state.IsSoulSalvage
                    && !_lightHarvestCompleted
                    && _lightTarget == null)
                {
                    TryUseLightCast(__state.Item);
                }
            }
            finally
            {
                ClearLightCastState();
            }
        }

        private static bool BeforeGetManaExpended(
            NpcHeroSummon __instance,
            ref float __result,
            float ____manaExpended)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (_heavyCastActive
                && plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value)
            {
                _heavyTarget = __instance;
                __result = 0.0f;
                return false;
            }
            if (!_lightCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || __instance == null)
            {
                return true;
            }

            _lightTarget = __instance;
            ReanimationRecord raisedRecord;
            if (Reanimations.TryGetValue(((Model)__instance).ID, out raisedRecord))
            {
                float corpseQuality = Mathf.Lerp(
                    RaisedSalvageMinimumQualityFactor,
                    RaisedSalvageMaximumQualityFactor,
                    Mathf.Clamp01(raisedRecord.Quality01));
                _lightOriginalMana = Math.Max(
                    0.0f,
                    raisedRecord.BindingManaCost * corpseQuality);
                _lightMaximumManaReturn = Math.Max(
                    0.0f,
                    raisedRecord.BindingManaCost
                    * RaisedSalvageMaximumRefundFraction);
            }
            else
            {
                _lightOriginalMana = Math.Max(0.0f, ____manaExpended);
                _lightMaximumManaReturn = float.PositiveInfinity;
            }
            _lightHealthFraction = __instance.ParentModel != null
                && __instance.ParentModel.Health != null
                    ? Mathf.Clamp01(__instance.ParentModel.Health.Percentage)
                    : 0.0f;

            float manaReturnFraction =
                plugin.SoulSalvageManaReturnPercent.Value / 100.0f;
            __result = NativeManaRefundMultiplier > 0.0f
                ? Math.Min(
                    _lightOriginalMana
                        * _lightHealthFraction
                        * manaReturnFraction,
                    _lightMaximumManaReturn)
                    / NativeManaRefundMultiplier
                : 0.0f;
            CompleteLightSummonHarvest(__instance);
            return false;
        }

        private static bool BeforeDestroySummon(NpcHeroSummon __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (_heavyCastActive
                && plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value
                && __instance != null)
            {
                if (_heavyTarget == null)
                {
                    TryCaptureFocusedHeavyTarget();
                }
                if (ReferenceEquals(_heavyTarget, __instance))
                {
                    plugin.LogDiagnostic(
                        "Blocked vanilla heavy Soul Rend summon sacrifice; resolving friendly Empower instead.");
                    return false;
                }
            }
            if (__instance != null)
            {
                ReanimationRecord endingRecord;
                if (Reanimations.TryGetValue(
                        ((Model)__instance).ID,
                        out endingRecord))
                {
                    endingRecord.DismissedAsRemains = true;
                }
            }
            if (!_lightCastActive
                || _lightHarvestCompleted
                || plugin == null
                || hero == null
                || __instance == null
                || !ReferenceEquals(__instance, _lightTarget))
            {
                return true;
            }

            CompleteLightSummonHarvest(__instance);
            return true;
        }

        private static bool BeforeDestroyHeavyTarget(NpcElement __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcElement target = _heavyTarget == null
                ? null
                : _heavyTarget.ParentModel;
            if (!_heavyCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || target == null
                || !ReferenceEquals(target, __instance))
            {
                return true;
            }

            plugin.LogDiagnostic(
                "Blocked vanilla heavy Soul Rend NPC destroy; resolving friendly Empower instead.");
            return false;
        }

        private static bool BeforeKillHeavyTarget(HealthElement __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcElement target = _heavyTarget == null
                ? null
                : _heavyTarget.ParentModel;
            if (!_heavyCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || target == null
                || !ReferenceEquals(target.HealthElement, __instance))
            {
                return true;
            }

            plugin.LogDiagnostic(
                "Blocked vanilla heavy Soul Rend servant kill; resolving friendly Empower instead.");
            return false;
        }

        private static void CompleteLightSummonHarvest(NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (_lightHarvestCompleted
                || plugin == null
                || hero == null
                || summon == null
                || !ReferenceEquals(summon, _lightTarget))
            {
                return;
            }

            _lightHarvestCompleted = true;
            string summonId = ((Model)summon).ID;
            ReanimationRecord raisedRecord;
            float soulVigorAward = 0.0f;
            Grailwright.Shared.CorpseQualityTier qualityTier;
            Grailwright.Shared.CorpseQualityTier audioTier;
            bool hasAudioPosition = false;
            Vector3 audioPosition = Vector3.zero;
            string displayName;
            bool raisedSacrifice = Reanimations.TryGetValue(
                summonId,
                out raisedRecord);
            if (raisedSacrifice)
            {
                raisedRecord.Sacrificed = true;
                qualityTier = raisedRecord.QualityTier;
                audioTier = raisedRecord.QualityTier;
                displayName = raisedRecord.SourceDisplayName;
            }
            else
            {
                soulVigorAward = SoulProgressionRuntime.HarvestOrdinarySummon();
                qualityTier = Grailwright.Shared.CorpseQualityTier.None;
                Location summonLocation = summon.ParentModel == null
                    ? null
                    : summon.ParentModel.ParentModel;
                if (summonLocation != null)
                {
                    hasAudioPosition = true;
                    audioPosition = summonLocation.Coords;
                }
                audioTier = Grailwright.Shared.CorpseQualityBuckets.GetTier(
                    CalculateQuality01(summonLocation, summon.ParentModel),
                    true);
                displayName = summonLocation == null
                    ? "summon"
                    : GetCorpseDisplayName(summonLocation);
            }
            float manaReturned = _lightOriginalMana
                * _lightHealthFraction
                * (plugin.SoulSalvageManaReturnPercent.Value / 100.0f);
            manaReturned = Math.Min(manaReturned, _lightMaximumManaReturn);
            if (raisedSacrifice)
            {
                raisedRecord.ManaReturnedOnSacrifice = manaReturned;
            }
            else
            {
                SoulProgressionRuntime.ShowSoulVigorHarvest(
                    displayName,
                    qualityTier,
                    soulVigorAward,
                    manaReturned);
                SoulProgressionRuntime.ShowCommandUnlocksAfterSummonHarvest(
                    soulVigorAward);
            }
            if (!raisedSacrifice)
            {
                SoulSalvageAudioRuntime.Play(
                    audioTier,
                    hasAudioPosition,
                    audioPosition);
            }
            plugin.LogDiagnostic(
                "Soul Rend unbound " + summonId
                + ": investedMana=" + _lightOriginalMana.ToString("0.##")
                + "; healthFraction=" + _lightHealthFraction.ToString("0.###")
                + "; manaReturned=" + manaReturned.ToString("0.##")
                + "; soulVigor=" + (raisedSacrifice
                    ? "pending remains"
                    : soulVigorAward.ToString("0.##")) + ".");
        }

        private static void TryServeHeavyTarget(NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcElement npc = summon == null ? null : summon.ParentModel;
            if (plugin == null
                || npc == null
                || npc.HasBeenDiscarded
                || !npc.IsAlive
                || npc.Health == null)
            {
                return;
            }

            float power = SoulProgressionRuntime.GetNecromanticPower();
            float maximumHealth = npc.AliveStats != null
                    && npc.AliveStats.MaxHealth != null
                ? Math.Max(
                    npc.Health.UpperLimit,
                    npc.AliveStats.MaxHealth.ModifiedValue)
                : npc.Health.UpperLimit;
            if (maximumHealth <= 0.0f)
            {
                plugin.LogDiagnostic(
                    "Heavy Soul Rend could not resolve the servant's maximum Health.");
                return;
            }

            float beforeHealth = Mathf.Clamp(
                npc.Health.ModifiedValue,
                0.0f,
                maximumHealth);
            float beforeFraction = beforeHealth / maximumHealth;
            bool empowerEligibleHealth = beforeFraction
                >= ServantEmpowerHealthThreshold;
            float healingFraction = Mathf.Lerp(
                ServantHealingPowerZeroFraction,
                ServantHealingPowerMaximumFraction,
                Mathf.Clamp01(power / 200.0f));
            float missingHealth = Math.Max(0.0f, maximumHealth - beforeHealth);
            float requestedHealing = empowerEligibleHealth
                ? missingHealth
                : Math.Min(missingHealth, maximumHealth * healingFraction);
            if (requestedHealing > 0.001f)
            {
                npc.Health.IncreaseBy(requestedHealing);
            }
            float appliedHealing = Math.Max(
                0.0f,
                Math.Min(maximumHealth, npc.Health.ModifiedValue) - beforeHealth);

            bool alreadyEmpowered = SummonRuntime.IsEmpoweredSummon(summon);
            bool empowered = false;
            float multiplier = 1.0f;
            if (empowerEligibleHealth
                && power >= SoulProgressionRuntime.EmpowermentPower
                && !alreadyEmpowered)
            {
                float roll = UnityEngine.Random.value;
                multiplier = 1.20f + (0.30f * roll * roll);
                empowered = SummonRuntime.TryEmpowerSummon(
                    summon,
                    multiplier);
            }

            plugin.LogDiagnostic(
                "Heavy Soul Rend servant service: summon="
                + ((Model)summon).ID
                + "; power=" + power.ToString("0.##", CultureInfo.InvariantCulture)
                + "; health=" + beforeHealth.ToString("0.##", CultureInfo.InvariantCulture)
                + "/" + maximumHealth.ToString("0.##", CultureInfo.InvariantCulture)
                + "; thresholdEligible=" + empowerEligibleHealth
                + "; requestedHealing=" + requestedHealing.ToString("0.##", CultureInfo.InvariantCulture)
                + "; appliedHealing=" + appliedHealing.ToString("0.##", CultureInfo.InvariantCulture)
                + "; alreadyEmpowered=" + alreadyEmpowered
                + "; empowered=" + empowered + ".");

            if (appliedHealing <= 0.001f && !empowered)
            {
                if (!empowerEligibleHealth)
                {
                    plugin.LogDiagnostic(
                        "Heavy Soul Rend found an injured servant, but no Health could be restored.");
                }
                else if (power < SoulProgressionRuntime.EmpowermentPower)
                {
                    plugin.LogDiagnostic(
                        "The servant is whole, but Empower requires 100 Necromantic Power.");
                }
                else if (alreadyEmpowered)
                {
                    plugin.LogDiagnostic("The servant is already Empowered.");
                }
                return;
            }

            SpawnNecromanticSummonVfx(summon.ParentModel);
            float appliedPercent = 100.0f * appliedHealing / maximumHealth;
            if (empowered && appliedHealing > 0.001f)
            {
                SoulProgressionRuntime.ShowSummonCommand(
                    "Servant Restored and Empowered: "
                    + multiplier.ToString("0.00", CultureInfo.InvariantCulture)
                    + "x");
            }
            else if (empowered)
            {
                SoulProgressionRuntime.ShowSummonCommand(
                    "Servant Empowered: "
                    + multiplier.ToString("0.00", CultureInfo.InvariantCulture)
                    + "x");
            }
            else
            {
                SoulProgressionRuntime.ShowSummonCommand(
                    "Servant Restored: +"
                    + appliedPercent.ToString("0.#", CultureInfo.InvariantCulture)
                    + "% Health");
            }
        }

        internal static void SpawnNecromanticSummonVfx(NpcElement npc)
        {
            if (npc == null || npc.HasBeenDiscarded)
            {
                return;
            }
            Location location = npc.ParentModel;
            PrefabPool.InstantiateAndReturn(
                new ShareableARAssetReference(SkeletonSummonVfxKey),
                npc.Coords,
                location == null ? Quaternion.identity : location.Rotation).Forget();
        }

        private static void TryUseLightCast(Item sourceItem)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null || hero == null)
            {
                return;
            }
            Location corpse;
            string corpseRejection;
            if (TryFindEligibleCorpse(hero, out corpse, out corpseRejection))
            {
                TryHarvestCorpse(corpse);
                return;
            }

            if (plugin.LivingTargetSoulSalvage.Value)
            {
                Location targetLocation;
                NpcElement target;
                Collider hitCollider;
                string livingRejection;
                if (TryFindEligibleLivingTarget(
                        hero,
                        out targetLocation,
                        out target,
                        out hitCollider,
                        out livingRejection))
                {
                    ApplySoulRend(hero, target, sourceItem, hitCollider);
                    return;
                }
                plugin.LogDiagnostic(
                    "Soul Rend light cast found no eligible target: corpse="
                    + corpseRejection + "; living=" + livingRejection + ".");
                return;
            }

            plugin.LogDiagnostic(
                "Soul Rend light cast harvested nothing: " + corpseRejection);
        }

        private static void TryHarvestCorpse(Location corpse)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null || corpse == null)
            {
                return;
            }
            float quality01 = CalculateQuality01(corpse, null);
            Grailwright.Shared.CorpseQualityTier tier =
                Grailwright.Shared.CorpseQualityBuckets.GetTier(quality01, true);
            string fingerprint = GetCorpseFingerprint(corpse);
            string displayName = GetCorpseDisplayName(corpse);
            SoulProgressionRuntime.CorpseHarvestReceipt harvestReceipt;
            if (!SoulProgressionRuntime.TryHarvestCorpse(
                    fingerprint,
                    tier,
                    out harvestReceipt))
            {
                plugin.LogWarning(
                    "Soul Rend could not save Soul Vigor for " + displayName
                    + "; the corpse was left unchanged.");
                return;
            }
            if (!TryCreateRemains(
                    corpse,
                    corpse.Coords,
                    corpse.Rotation,
                    out string failure))
            {
                SoulProgressionRuntime.RollbackCorpseHarvest(harvestReceipt);
                plugin.LogWarning(
                    "Soul Rend could not simplify " + displayName + ": " + failure);
                return;
            }
            SoulProgressionRuntime.ShowSoulVigorHarvest(
                displayName,
                tier,
                harvestReceipt.Award,
                0.0f);
            SoulProgressionRuntime.ShowCommandUnlocksAfterCorpseHarvest(
                harvestReceipt);
            SoulSalvageAudioRuntime.Play(tier, true, corpse.Coords);
            plugin.LogDiagnostic(
                "Soul Rend harvested " + displayName
                + "; quality=" + tier
                + "; soulVigor=" + harvestReceipt.Award.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + ".");
        }

        private static void ApplySoulRend(
            Hero hero,
            NpcElement target,
            Item sourceItem,
            Collider hitCollider)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || hero == null
                || target == null
                || target.HealthElement == null
                || sourceItem == null
                || sourceItem.ItemStats == null)
            {
                return;
            }

            float power = SoulProgressionRuntime.GetNecromanticPower();
            float powerMultiplier = GetSoulRendPowerMultiplier(power);
            int itemLevel = sourceItem.Level == null
                ? 0
                : Math.Max(0, sourceItem.Level.ModifiedInt);
            float comparableDamage = ComparableLightSpellBaseDamage + itemLevel;
            Damage.GetStatModifiers(
                hero,
                sourceItem.ItemStats,
                out float statMultiplier,
                out float linearModifier);
            DamageParameters parameters = DamageParameters.Default;
            parameters.CanBeCritical = false;
            parameters.Critical = false;
            parameters.DamageTypeData = new RuntimeDamageTypeData(
                DamageType.MagicalHitSource,
                DamageSubType.GenericMagical);
            parameters.PoiseDamage = 0.0f;
            parameters.ForceDamage = 0.0f;
            parameters.RagdollForce = 0.0f;
            parameters.Position = hitCollider == null
                ? target.Coords
                : hitCollider.ClosestPoint(target.Coords);
            Vector3 direction = target.Coords - hero.Coords;
            parameters.Direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;

            Damage damage = new Damage(
                    parameters,
                    hero,
                    target,
                    new RawDamageData(
                        comparableDamage,
                        statMultiplier * powerMultiplier,
                        linearModifier))
                .WithItem(sourceItem)
                .WithHitCollider(hitCollider);
            MarkNecroticDamage(damage);
            target.HealthElement.TakeDamage(damage);

            int stacks = 0;
            if (target.IsAlive && !((Model)target).HasBeenDiscarded)
            {
                string targetId = ((Model)target).ID;
                FrayedSoulState state;
                if (!FrayedSouls.TryGetValue(targetId, out state)
                    || state.ExpiresAt <= Time.unscaledTime)
                {
                    state = new FrayedSoulState();
                    FrayedSouls[targetId] = state;
                }
                state.Stacks = Math.Min(
                    FrayedSoulMaximumStacks,
                    state.Stacks + 1);
                state.ExpiresAt = Time.unscaledTime + FrayedSoulDurationSeconds;
                stacks = state.Stacks;
            }

            plugin.LogDiagnostic(
                "Soul Rend hit " + GetCorpseDisplayName(target.ParentModel)
                + "; comparableDamage="
                + comparableDamage.ToString("0.##", CultureInfo.InvariantCulture)
                + "; power=" + power.ToString("0.##", CultureInfo.InvariantCulture)
                + "; multiplier="
                + powerMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                + "; finalDamage="
                + damage.Amount.ToString("0.##", CultureInfo.InvariantCulture)
                + "; frayedStacks="
                + stacks.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private static float GetSoulRendPowerMultiplier(float power)
        {
            float safePower = Mathf.Clamp(power, 0.0f, 200.0f);
            return safePower <= 100.0f
                ? Mathf.Lerp(
                    SoulRendPowerZeroMultiplier,
                    SoulRendPowerNormalMultiplier,
                    safePower / 100.0f)
                : Mathf.Lerp(
                    SoulRendPowerNormalMultiplier,
                    SoulRendPowerMaximumMultiplier,
                    (safePower - 100.0f) / 100.0f);
        }

        private static void TryUseHeavyCast(Item sourceItem)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || hero == null
                || hero.VHeroController == null
                || hero.VHeroController.Raycaster == null)
            {
                return;
            }

            Location source;
            string corpseRejection;
            if (TryFindEligibleCorpse(hero, out source, out corpseRejection))
            {
                TryRaiseCorpse(
                    sourceItem,
                    source,
                    bindingAlreadyWon: false,
                    summonLimitAlreadyChecked: false);
                return;
            }

            if (plugin.LivingTargetSoulSalvage.Value)
            {
                Location targetLocation;
                NpcElement target;
                Collider hitCollider;
                string livingRejection;
                if (TryFindEligibleLivingTarget(
                        hero,
                        out targetLocation,
                        out target,
                        out hitCollider,
                        out livingRejection))
                {
                    TryClaimLivingTarget(
                        hero,
                        targetLocation,
                        target,
                        sourceItem,
                        hitCollider);
                    return;
                }
                plugin.LogDiagnostic(
                    "Soul Rend heavy cast found no eligible target: corpse="
                    + corpseRejection + "; living=" + livingRejection + ".");
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "Soul Rend: no eligible target - "
                    + livingRejection + ".");
                return;
            }

            plugin.LogDiagnostic(
                "Soul Rend heavy cast raised nothing: " + corpseRejection);
            plugin.ShowSoulSalvageHeavyCastDiagnostic(
                "Soul Rend: no eligible corpse - " + corpseRejection + ".");
        }

        private static void TryClaimLivingTarget(
            Hero hero,
            Location targetLocation,
            NpcElement target,
            Item sourceItem,
            Collider hitCollider)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || hero == null
                || targetLocation == null
                || target == null
                || target.Health == null
                || target.HealthElement == null)
            {
                return;
            }

            if (!TryGetSummonCapacity(
                    hero,
                    plugin,
                    out int summonCount,
                    out int summonLimit))
            {
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "Soul Rend: servant limit full ("
                    + summonCount.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + summonLimit.ToString(CultureInfo.InvariantCulture)
                    + ").");
                return;
            }

            float healthFraction = Mathf.Clamp01(target.Health.Percentage);
            string displayName = GetCorpseDisplayName(targetLocation);
            if (healthFraction > SoulClaimHealthThreshold)
            {
                SoulProgressionRuntime.ShowSoulClaimFeedback(
                    "The soul is still too firmly bound to " + displayName + ".",
                    highPriority: false);
                plugin.LogDiagnostic(
                    "Soul Claim rejected " + displayName + "; health="
                    + (healthFraction * 100.0f).ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + "% exceeds the 40% threshold.");
                return;
            }

            string targetId = ((Model)target).ID;
            int frayedStacks = ConsumeFrayedSoulStacks(targetId);
            float power = SoulProgressionRuntime.GetNecromanticPower();
            float healthVulnerability = SoulClaimHealthThreshold <= 0.0f
                ? 0.0f
                : Mathf.Clamp01(
                    (SoulClaimHealthThreshold - healthFraction)
                    / SoulClaimHealthThreshold);
            float powerChance = GetSoulClaimPowerChance(power);
            float quality01 = CalculateQuality01(targetLocation, target);
            Grailwright.Shared.CorpseQualityTier qualityTier =
                Grailwright.Shared.CorpseQualityBuckets.GetTier(quality01, true);
            float qualityFactor = GetSoulClaimQualityFactor(qualityTier);
            float chance = Mathf.Min(
                SoulClaimAbsoluteChanceCap,
                healthVulnerability
                    * powerChance
                    * qualityFactor
                    * (1.0f
                        + (FrayedSoulChanceBonusPerStack * frayedStacks)));
            float roll = UnityEngine.Random.value;
            if (roll >= chance)
            {
                SoulProgressionRuntime.ShowSoulClaimFeedback(
                    SoulProgressionRuntime.GetSoulClaimFailureMessage(),
                    highPriority: false);
                plugin.LogDiagnostic(
                    "Soul Claim resisted by " + displayName
                    + "; health="
                    + healthFraction.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; tier=" + qualityTier
                    + "; power=" + power.ToString("0.##", CultureInfo.InvariantCulture)
                    + "; frayedStacks="
                    + frayedStacks.ToString(CultureInfo.InvariantCulture)
                    + "; chance="
                    + (chance * 100.0f).ToString("0.##", CultureInfo.InvariantCulture)
                    + "%; roll="
                    + (roll * 100.0f).ToString("0.##", CultureInfo.InvariantCulture)
                    + "%.");
                return;
            }

            DamageParameters parameters = DamageParameters.Default;
            parameters.CanBeCritical = false;
            parameters.Critical = false;
            parameters.IgnoreArmor = true;
            parameters.Inevitable = true;
            parameters.IsHeavyAttack = true;
            parameters.PoiseDamage = 0.0f;
            parameters.ForceDamage = 0.0f;
            parameters.RagdollForce = 0.0f;
            parameters.DamageTypeData = new RuntimeDamageTypeData(
                DamageType.MagicalHitSource,
                DamageSubType.GenericMagical);
            parameters.Position = hitCollider == null
                ? target.Coords
                : hitCollider.ClosestPoint(target.Coords);
            Vector3 direction = target.Coords - hero.Coords;
            parameters.Direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            float lethalDamage = Math.Max(
                    1.0f,
                    Math.Max(target.Health.ModifiedValue, target.Health.UpperLimit))
                * 100.0f;
            Damage claimDamage = new Damage(
                    parameters,
                    hero,
                    target,
                    new RawDamageData(lethalDamage))
                .WithItem(sourceItem)
                .WithHitCollider(hitCollider);
            target.HealthElement.TakeDamage(claimDamage);
            if (target.IsAlive || !targetLocation.HasElement<Corpse>())
            {
                SoulProgressionRuntime.ShowSoulClaimFeedback(
                    "The soul tore loose, but the body refused your command.",
                    highPriority: false);
                plugin.LogWarning(
                    "Soul Claim won its roll against " + displayName
                    + " but the native killing-damage path did not produce a corpse.");
                return;
            }

            SoulProgressionRuntime.ShowSoulClaimFeedback(
                displayName + "'s soul is yours.",
                highPriority: true);
            plugin.LogDiagnostic(
                "Soul Claim succeeded against " + displayName
                + "; tier=" + qualityTier
                + "; chance="
                + (chance * 100.0f).ToString("0.##", CultureInfo.InvariantCulture)
                + "%; roll="
                + (roll * 100.0f).ToString("0.##", CultureInfo.InvariantCulture)
                + "%. Reanimating through the protected corpse lifecycle.");
            TryRaiseCorpse(
                sourceItem,
                targetLocation,
                bindingAlreadyWon: true,
                summonLimitAlreadyChecked: true);
        }

        private static float GetSoulClaimPowerChance(float power)
        {
            float safePower = Mathf.Clamp(power, 0.0f, 200.0f);
            return safePower <= 100.0f
                ? Mathf.Lerp(
                    SoulClaimPowerZeroChance,
                    SoulClaimPowerNormalChance,
                    safePower / 100.0f)
                : Mathf.Lerp(
                    SoulClaimPowerNormalChance,
                    SoulClaimPowerMaximumChance,
                    (safePower - 100.0f) / 100.0f);
        }

        private static float GetSoulClaimQualityFactor(
            Grailwright.Shared.CorpseQualityTier tier)
        {
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    return 0.85f;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    return 0.65f;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    return 0.45f;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    return 1.0f;
            }
        }

        private static bool TryGetSummonCapacity(
            Hero hero,
            SoulAndServicePlugin plugin,
            out int summonCount,
            out int summonLimit)
        {
            summonCount = 0;
            foreach (NpcHeroSummon ignored in World.All<NpcHeroSummon>())
            {
                summonCount++;
            }
            summonLimit = hero.HeroStats.SummonLimit.ModifiedInt
                + SoulProgressionRuntime.GetProgressionSummonLimitBonus()
                + plugin.SummonLimitBonus.Value;
            return summonCount < summonLimit;
        }

        private static int ConsumeFrayedSoulStacks(string targetId)
        {
            FrayedSoulState state;
            if (string.IsNullOrEmpty(targetId)
                || !FrayedSouls.TryGetValue(targetId, out state))
            {
                return 0;
            }
            FrayedSouls.Remove(targetId);
            return state.ExpiresAt > Time.unscaledTime
                ? Math.Min(FrayedSoulMaximumStacks, Math.Max(0, state.Stacks))
                : 0;
        }

        private static void RemoveExpiredFrayedSouls()
        {
            if (FrayedSouls.Count == 0)
            {
                return;
            }
            float now = Time.unscaledTime;
            foreach (string targetId in FrayedSouls
                .Where(pair => pair.Value == null || pair.Value.ExpiresAt <= now)
                .Select(pair => pair.Key)
                .ToArray())
            {
                FrayedSouls.Remove(targetId);
            }
        }

        private static void TryRaiseCorpse(
            Item sourceItem,
            Location source,
            bool bindingAlreadyWon,
            bool summonLimitAlreadyChecked)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null || hero == null || source == null)
            {
                return;
            }

            if (!summonLimitAlreadyChecked
                && !TryGetSummonCapacity(
                    hero,
                    plugin,
                    out int summonCount,
                    out int summonLimit))
            {
                plugin.LogWarning(
                    "Soul Rend could not raise " + source.DebugName
                    + " because the summon limit is full.");
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "Soul Rend: servant limit full ("
                    + summonCount.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + summonLimit.ToString(CultureInfo.InvariantCulture)
                    + ").");
                return;
            }

            float quality01 = CalculateQuality01(source, null);
            Grailwright.Shared.CorpseQualityTier qualityTier =
                Grailwright.Shared.CorpseQualityBuckets.GetTier(quality01, true);
            string corpseFingerprint = GetCorpseFingerprint(source);
            float bindingProgress01;
            float bindingResistance;
            if (!bindingAlreadyWon
                && !SoulProgressionRuntime.ApplyBindingAttempt(
                corpseFingerprint,
                qualityTier,
                out bindingProgress01,
                out bindingResistance))
            {
                string flavor = SoulProgressionRuntime.GetBindingFailureMessage(
                    corpseFingerprint,
                    Mathf.RoundToInt(bindingProgress01 * 1000.0f));
                SoulProgressionRuntime.ShowBindingFailure(flavor);
                plugin.LogDiagnostic(
                    "Soul binding resisted by " + source.DebugName
                    + "; tier=" + qualityTier
                    + "; progress="
                    + bindingProgress01.ToString("0.###", CultureInfo.InvariantCulture)
                    + "; resistance="
                    + bindingResistance.ToString("0.##", CultureInfo.InvariantCulture)
                    + "; power="
                    + SoulProgressionRuntime.GetNecromanticPower().ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + ".");
                return;
            }

            LocationInteractability previousInteractability = source.Interactability;
            float bindingManaCost = GetHeavyCastManaCost(sourceItem);
            Location raised = null;
            try
            {
                source.TriggerVisualScriptingEvent("OnResurrectStarted");
                raised = source.Template.SpawnLocation(source.Coords, source.Rotation);
                ((Model)raised).MarkedNotSaved = true;
                NpcElement raisedNpc = raised.Element<NpcElement>();
                bool usedFallbackPortrait = EnsureRaisedServantPortrait(raisedNpc);
                NpcElement npc = SummonUtils.InitializeSummon(
                    raised,
                    hero,
                    sourceItem,
                    0.0f,
                    0.0f,
                    null);
                ((Model)npc).MarkedNotSaved = true;
                npc.AddMarkerElement<PreventExpRewardMarker>();
                raised.RemoveElementsOfType<AliveLocationDeathReward>();
                raised.RemoveElementsOfType<SearchAction>();
                raised.RemoveElementsOfType<PickpocketAction>();

                NpcHeroSummon summon = npc.Element<NpcHeroSummon>();
                ((Model)summon).MarkedNotSaved = true;
                string summonId = ((Model)summon).ID;
                Reanimations[summonId] = new ReanimationRecord
                {
                    SourceCorpse = source,
                    RaisedLocation = raised,
                    RaisedNpc = npc,
                    SourceInteractability = previousInteractability,
                    SourceDisplayName = GetCorpseDisplayName(source),
                    CorpseFingerprint = corpseFingerprint,
                    Quality01 = quality01,
                    QualityTier = qualityTier,
                    BindingManaCost = bindingManaCost,
                    OriginalCoords = source.Coords,
                    OriginalRotation = source.Rotation,
                    LastSafeCoords = raised.Coords,
                    LastSafeRotation = raised.Rotation
                };
                source.SetInteractability(LocationInteractability.Hidden);
                PrefabPool.InstantiateAndReturn(
                    new ShareableARAssetReference(SkeletonSummonVfxKey),
                    source.Coords,
                    source.Rotation).Forget();

                npc.OnCompletelyInitialized(
                    delegate
                    {
                        try
                        {
                            if (npc.HasBeenDiscarded)
                            {
                                RestoreSourceCorpse(
                                    summonId,
                                    discardRaisedCopy: true,
                                    showDiagnostic: false);
                                return;
                            }
                            ReanimationRecord record;
                            if (!Reanimations.TryGetValue(summonId, out record))
                            {
                                return;
                            }
                            raised.RemoveElementsOfType<AliveLocationDeathReward>();
                            raised.RemoveElementsOfType<SearchAction>();
                            raised.RemoveElementsOfType<PickpocketAction>();
                            npc.RemoveElementsOfType<NpcHealthRegeneration>();
                            float qualityHealthMultiplier =
                                SoulProgressionRuntime.GetQualityHealthMultiplier(
                                    record.QualityTier);
                            if (npc.AliveStats != null
                                && npc.AliveStats.MaxHealth != null
                                && Math.Abs(qualityHealthMultiplier - 1.0f) > 0.0001f)
                            {
                                record.QualityHealthTweak = StatTweak.Multi(
                                    npc.AliveStats.MaxHealth,
                                    qualityHealthMultiplier,
                                    null,
                                    npc);
                                ((Model)record.QualityHealthTweak).MarkedNotSaved = true;
                            }
                            npc.Health.SetToFull();
                            raised.TriggerVisualScriptingEvent("OnResurrect");

                            float maximumHealth = npc.Health.UpperLimit;
                            float necromanticPower =
                                SoulProgressionRuntime.GetNecromanticPower();
                            float retainedHealthFraction =
                                SoulProgressionRuntime.RollRaisedHealthFraction(
                                    necromanticPower);
                            float retainedHealth = maximumHealth * retainedHealthFraction;
                            if (npc.Health.ModifiedValue > retainedHealth)
                            {
                                npc.Health.DecreaseBy(
                                    npc.Health.ModifiedValue - retainedHealth);
                            }
                            plugin.LogDiagnostic(
                                "Raised a restricted runtime copy of " + source.DebugName
                                + "; quality=" + record.QualityTier
                                + " (" + record.Quality01.ToString("0.###", CultureInfo.InvariantCulture) + ")"
                                + "; maximumHealth="
                                + maximumHealth.ToString("0.##", CultureInfo.InvariantCulture)
                                + "; retainedHealth="
                                + retainedHealthFraction.ToString("0.###", CultureInfo.InvariantCulture)
                                + "; power="
                                + necromanticPower.ToString("0.##", CultureInfo.InvariantCulture)
                                + "; upkeep="
                                + SummonRuntime.GetUpkeepPercentPerMinute(
                                    (int)World.All<NpcHeroSummon>().Count(),
                                    SoulProgressionRuntime.GetNecromanticPower())
                                    .ToString("0.###", CultureInfo.InvariantCulture)
                                + "% max health per minute"
                                + "; portrait="
                                + (usedFallbackPortrait
                                    ? "generic-skeleton-summon"
                                    : "native")
                                + ".");
                            string outcome = "Soul Rend: raised "
                                + record.SourceDisplayName
                                + " at "
                                + (retainedHealthFraction * 100.0f).ToString(
                                    "0",
                                    CultureInfo.InvariantCulture)
                                + "% health (Power "
                                + necromanticPower.ToString(
                                    "0.##",
                                    CultureInfo.InvariantCulture)
                                + ")"
                                + (usedFallbackPortrait
                                    ? " (generic portrait used)"
                                    : string.Empty)
                                + ".";
                            plugin.ShowSoulSalvageHeavyCastDiagnostic(outcome);
                            SoulProgressionRuntime.CommitSuccessfulBinding(
                                record.CorpseFingerprint);
                            SoulProgressionRuntime.ShowResurrection(
                                record.SourceDisplayName,
                                record.QualityTier);
                        }
                        catch (Exception exception)
                        {
                            RestoreSourceCorpse(
                                summonId,
                                discardRaisedCopy: true,
                                showDiagnostic: false);
                            plugin.LogWarning(
                                "Soul Rend could not finish initializing a raised servant: "
                                + exception.GetBaseException().Message);
                            plugin.ShowSoulSalvageHeavyCastDiagnostic(
                                "Soul Rend: reanimation failed - source corpse restored; see BepInEx log.");
                        }
                    });
            }
            catch (Exception exception)
            {
                if (raised != null && !raised.HasBeenDiscarded)
                {
                    raised.Discard();
                }
                if (source != null && !source.HasBeenDiscarded)
                {
                    source.SetInteractability(previousInteractability);
                }
                plugin.LogWarning(
                    "Soul Rend could not create a raised servant: "
                    + exception.GetBaseException().Message);
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "Soul Rend: reanimation failed - see BepInEx log.");
            }
        }

        private static bool EnsureRaisedServantPortrait(NpcElement npc)
        {
            SpriteReference portrait = npc == null ? null : npc.NpcIcon;
            if (portrait == null || portrait.IsSet)
            {
                return false;
            }

            portrait.arSpriteReference =
                new ARAssetReference(GenericRaisedServantPortraitKey);
            return true;
        }

        private static string GetCorpseDisplayName(Location source)
        {
            try
            {
                string displayName = source == null ? string.Empty : source.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName.Replace("\r", " ").Replace("\n", " ").Trim();
                }
            }
            catch
            {
            }

            return source == null || string.IsNullOrWhiteSpace(source.DebugName)
                ? "corpse"
                : source.DebugName;
        }

        private static bool TryFindEligibleCorpse(
            Hero hero,
            out Location source,
            out string rejection)
        {
            source = null;
            rejection = "no corpse was under the crosshair";
            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                SoulSalvageRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                VLocation view = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                if (candidate == null || candidate.HasBeenDiscarded)
                {
                    rejection = "the line of sight was blocked before a corpse";
                    return false;
                }
                if (!TryValidateEligibleCorpse(hero, candidate, out rejection))
                {
                    return false;
                }
                source = candidate;
                rejection = string.Empty;
                return true;
            }
            return false;
        }

        private static bool TryFindEligibleLivingTarget(
            Hero hero,
            out Location source,
            out NpcElement npc,
            out Collider hitCollider,
            out string rejection)
        {
            source = null;
            npc = null;
            hitCollider = null;
            rejection = "no living enemy was under the crosshair";
            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                SoulSalvageRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                VLocation view = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                if (candidate == null || candidate.HasBeenDiscarded)
                {
                    rejection = "the line of sight was blocked before a living enemy";
                    return false;
                }
                if (!TryValidateEligibleLivingTarget(
                        hero,
                        candidate,
                        out NpcElement candidateNpc,
                        out rejection))
                {
                    return false;
                }
                source = candidate;
                npc = candidateNpc;
                hitCollider = hit.collider;
                rejection = string.Empty;
                return true;
            }
            return false;
        }

        private static bool TryValidateEligibleLivingTarget(
            Hero hero,
            Location candidate,
            out NpcElement npc,
            out string rejection)
        {
            npc = candidate == null ? null : candidate.TryGetElement<NpcElement>();
            if (npc == null || !npc.IsAlive || npc.HealthElement == null)
            {
                rejection = "the targeted location is not a living enemy";
                return false;
            }
            if (npc.IsSummon || npc.HasElement<NpcHeroSummon>())
            {
                rejection = "living summons use Soul Rend's sacrifice effect";
                return false;
            }
            if (candidate.Template == null)
            {
                rejection = "that enemy has no reusable location template";
                return false;
            }
            if (!IsRuntimeSpawned(candidate))
            {
                rejection = "authored scene and persistent NPCs are protected";
                return false;
            }
            NpcTemplate npcTemplate = npc.Template
                ?? NpcTemplate.FromNpcOrDummy(candidate);
            if (npcTemplate == null || npcTemplate.NpcType != NpcType.Normal)
            {
                rejection = "bosses, minibosses, and unresolved NPC templates are protected";
                return false;
            }
            if (npc.StoryOnDeath != null || HasProtectedRuntimeIdentity(candidate))
            {
                rejection = "named, scripted, quest, merchant, guard, and companion NPCs are protected";
                return false;
            }
            if (!npc.IsHostileTo(hero))
            {
                rejection = "only ordinary hostile living enemies are eligible";
                return false;
            }
            rejection = string.Empty;
            return true;
        }

        private static bool TryValidateEligibleCorpse(
            Hero hero,
            Location candidate,
            out string rejection)
        {
            Corpse corpse;
            if (!candidate.TryGetElement<Corpse>(out corpse) || corpse == null)
            {
                rejection = "the targeted location is not a corpse";
                return false;
            }
            if (Reanimations.Values.Any(record => record.SourceCorpse == candidate))
            {
                rejection = "that corpse is already serving";
                return false;
            }
            if (candidate.Template == null)
            {
                rejection = "that corpse has no reusable location template";
                return false;
            }
            if (!IsRuntimeSpawned(candidate))
            {
                rejection = "authored scene and persistent NPC corpses are protected";
                return false;
            }
            NpcTemplate npcTemplate = NpcTemplate.FromNpcOrDummy(candidate);
            if (npcTemplate == null || npcTemplate.NpcType != NpcType.Normal)
            {
                rejection = "bosses, minibosses, and unresolved NPC templates are protected";
                return false;
            }
            if (HasProtectedRuntimeIdentity(candidate))
            {
                rejection = "named, scripted, quest, merchant, guard, and companion corpses are protected";
                return false;
            }
            if (corpse.Faction == null
                || hero.Faction == null
                || !corpse.Faction.IsHostileTo(hero.Faction))
            {
                rejection = "only ordinary hostile corpses are eligible";
                return false;
            }
            rejection = string.Empty;
            return true;
        }

        private static bool IsRuntimeSpawned(Location candidate)
        {
            return candidate != null
                && LocationInitializerField != null
                && LocationInitializerField.GetValue(candidate)
                    is RuntimeLocationInitializer;
        }

        private static bool HasProtectedRuntimeIdentity(Location candidate)
        {
            if (candidate.HasElement<GameplayUniqueLocation>())
            {
                return true;
            }
            if (candidate.Template != null)
            {
                foreach (Component component in candidate.Template.GetComponents<Component>())
                {
                    string componentType = component == null
                        ? string.Empty
                        : component.GetType().FullName ?? component.GetType().Name;
                    if (string.Equals(
                            componentType,
                            "Unity.VisualScripting.ScriptMachine",
                            StringComparison.Ordinal)
                        || string.Equals(
                            componentType,
                            "Unity.VisualScripting.Variables",
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            string[] protectedTerms =
            {
                "Story",
                "Quest",
                "Dialogue",
                "Merchant",
                "Trade",
                "Guard",
                "Companion",
                "Unique"
            };
            foreach (Element element in candidate.AllElements())
            {
                string typeName = element == null
                    ? string.Empty
                    : element.GetType().FullName ?? element.GetType().Name;
                if (protectedTerms.Any(term =>
                    typeName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }
            if (candidate.Spec != null)
            {
                foreach (Component component in candidate.Spec.GetComponents<Component>())
                {
                    string typeName = component == null
                        ? string.Empty
                        : component.GetType().FullName ?? component.GetType().Name;
                    if (protectedTerms.Any(term =>
                        typeName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void UpdateReanimationPositions()
        {
            if (Time.unscaledTime < _nextReanimationPositionRefreshTime)
            {
                return;
            }
            _nextReanimationPositionRefreshTime = Time.unscaledTime
                + ReanimationPositionRefreshSeconds;
            foreach (ReanimationRecord record in Reanimations.Values)
            {
                Location raised = record.RaisedLocation;
                if (raised == null || raised.HasBeenDiscarded)
                {
                    continue;
                }
                Vector3 coords = raised.Coords;
                if (float.IsNaN(coords.x)
                    || float.IsNaN(coords.y)
                    || float.IsNaN(coords.z)
                    || float.IsInfinity(coords.x)
                    || float.IsInfinity(coords.y)
                    || float.IsInfinity(coords.z))
                {
                    continue;
                }
                record.LastSafeCoords = coords;
                record.LastSafeRotation = raised.Rotation;
            }
        }

        private static void EndServiceAsRemains(
            string summonId,
            bool showDiagnostic)
        {
            ReanimationRecord record;
            if (!Reanimations.TryGetValue(summonId, out record))
            {
                return;
            }

            SoulProgressionRuntime.CorpseHarvestReceipt harvestReceipt = null;
            string failure = string.Empty;
            bool harvestReady = !record.Sacrificed
                || SoulProgressionRuntime.TryHarvestCorpse(
                    record.CorpseFingerprint,
                    record.QualityTier,
                    out harvestReceipt);
            bool simplified = harvestReady && TryCreateRemains(
                    record.SourceCorpse,
                    record.LastSafeCoords,
                    record.LastSafeRotation,
                    out failure);
            if (!harvestReady)
            {
                failure = "Soul Vigor could not be saved";
            }
            if (!simplified
                && harvestReady
                && (record.LastSafeCoords - record.OriginalCoords).sqrMagnitude
                    > 0.01f)
            {
                simplified = TryCreateRemains(
                    record.SourceCorpse,
                    record.OriginalCoords,
                    record.OriginalRotation,
                    out failure);
            }
            if (!simplified && harvestReceipt != null)
            {
                SoulProgressionRuntime.RollbackCorpseHarvest(harvestReceipt);
            }

            Reanimations.Remove(summonId);
            if (!simplified
                && record.SourceCorpse != null
                && !record.SourceCorpse.HasBeenDiscarded)
            {
                record.SourceCorpse.SetInteractability(
                    record.SourceInteractability);
                record.SourceCorpse.TriggerVisualScriptingEvent("OnDeath");
            }
            if (record.RaisedLocation != null
                && !record.RaisedLocation.HasBeenDiscarded
                && !PendingRaisedDiscards.Contains(record.RaisedLocation))
            {
                PendingRaisedDiscards.Add(record.RaisedLocation);
            }
            if (record.QualityHealthTweak != null
                && !((Model)record.QualityHealthTweak).HasBeenDiscarded)
            {
                ((Model)record.QualityHealthTweak).Discard();
            }

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                if (simplified)
                {
                    if (harvestReceipt != null)
                    {
                        SoulProgressionRuntime.ShowSoulVigorHarvest(
                            record.SourceDisplayName,
                            record.QualityTier,
                            harvestReceipt.Award,
                            record.ManaReturnedOnSacrifice);
                        SoulProgressionRuntime.ShowCommandUnlocksAfterCorpseHarvest(
                            harvestReceipt);
                    }
                    if (record.Sacrificed)
                    {
                        SoulSalvageAudioRuntime.Play(
                            record.QualityTier,
                            true,
                            record.LastSafeCoords);
                    }
                    plugin.LogDiagnostic(
                        "Soul Rend ended " + record.SourceDisplayName
                        + "'s service as simplified remains at its last position.");
                    if (showDiagnostic)
                    {
                        plugin.ShowSoulSalvageHeavyCastDiagnostic(
                            "Soul Rend: " + record.SourceDisplayName
                            + "'s service ended; remains were left behind.");
                    }
                }
                else
                {
                    plugin.LogWarning(
                        "Soul Rend could not create remains for "
                        + record.SourceDisplayName + ": " + failure
                        + ". The original corpse was restored.");
                }
            }
        }

        private static bool TryCreateRemains(
            Location source,
            Vector3 coords,
            Quaternion rotation,
            out string failure)
        {
            failure = string.Empty;
            if (source == null || source.HasBeenDiscarded)
            {
                failure = "the source corpse was unavailable";
                return false;
            }
            NpcDummy dummy = source.TryGetElement<NpcDummy>();
            GameConstants constants = World.Services == null
                ? null
                : World.Services.TryGet<GameConstants>();
            if (dummy == null || constants == null
                || constants.DefaultDeadBodyReplacedPrefab == null)
            {
                failure = "the native corpse replacement data was unavailable";
                return false;
            }

            Location remains = null;
            try
            {
                List<ItemSpawningDataRuntime> items =
                    new List<ItemSpawningDataRuntime>();
                SearchAction search = source.TryGetElement<SearchAction>();
                if (search != null)
                {
                    search.GetAllItems(items);
                }
                ShareableARAssetReference replacement =
                    SimplifiedDeadBodyReplacementField == null
                        ? null
                        : SimplifiedDeadBodyReplacementField.GetValue(dummy)
                            as ShareableARAssetReference;
                ARAssetReference visual = replacement != null && replacement.IsSet
                    ? replacement.Get()
                    : constants.DefaultDeadBodyReplacedVisualPrefab;
                remains = constants.DefaultDeadBodyReplacedPrefab.SpawnLocation(
                    coords,
                    Quaternion.Euler(0.0f, rotation.eulerAngles.y, 0.0f),
                    Vector3.one,
                    visual,
                    source.DisplayName);
                if (remains == null || remains.HasBeenDiscarded)
                {
                    failure = "the native remains prefab did not spawn";
                    return false;
                }
                remains.AddElement(new SearchAction(items, false));
                if (dummy.Template != null
                    && dummy.Template.corpseVFX != null
                    && dummy.Template.corpseVFX.IsSet)
                {
                    remains.AddElement(new DeadBodyMarkerVFX(dummy.Template.corpseVFX));
                }
                IWithFaction faction = source.TryGetElement<IWithFaction>();
                if (faction != null && faction.Faction != null)
                {
                    remains.AddElement(
                        new SimpleFactionProvider(faction.Faction.Template));
                }
                remains.AddElement<DiscardReplacementBodyElement>();
                source.Discard();
                return true;
            }
            catch (Exception exception)
            {
                if (remains != null && !remains.HasBeenDiscarded)
                {
                    remains.Discard();
                }
                failure = exception.GetBaseException().Message;
                return false;
            }
        }

        private static void RestoreSourceCorpse(
            string summonId,
            bool discardRaisedCopy,
            bool showDiagnostic)
        {
            ReanimationRecord record;
            if (!Reanimations.TryGetValue(summonId, out record))
            {
                return;
            }
            Reanimations.Remove(summonId);
            if (record.SourceCorpse != null && !record.SourceCorpse.HasBeenDiscarded)
            {
                record.SourceCorpse.SetInteractability(record.SourceInteractability);
                record.SourceCorpse.TriggerVisualScriptingEvent("OnDeath");
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogDiagnostic("Soul Rend restored the source corpse.");
                    if (showDiagnostic)
                    {
                        plugin.ShowSoulSalvageHeavyCastDiagnostic(
                            "Soul Rend: " + record.SourceDisplayName
                            + "'s service ended; source corpse restored.");
                    }
                }
            }
            if (discardRaisedCopy
                && record.RaisedLocation != null
                && !record.RaisedLocation.HasBeenDiscarded
                && !PendingRaisedDiscards.Contains(record.RaisedLocation))
            {
                PendingRaisedDiscards.Add(record.RaisedLocation);
            }
            if (record.QualityHealthTweak != null
                && !((Model)record.QualityHealthTweak).HasBeenDiscarded)
            {
                ((Model)record.QualityHealthTweak).Discard();
            }
        }

        private static void ClearLightCastState()
        {
            _lightCastActive = false;
            _heavyCastActive = false;
            _lightHarvestCompleted = false;
            _lightTarget = null;
            _heavyTarget = null;
            _lightOriginalMana = 0.0f;
            _lightHealthFraction = 0.0f;
            _lightMaximumManaReturn = float.PositiveInfinity;
        }

        internal static int GetFocusedTargetStateForInterop(
            bool requireRelevantSpell)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || (requireRelevantSpell && !IsSoulSalvageEquipped()))
            {
                return (int)SoulSalvageFocusedTargetState.None;
            }

            Location location;
            NpcHeroSummon summon;
            if (!TryFindFocusedSoulTargetCached(out location, out summon))
            {
                return (int)SoulSalvageFocusedTargetState.None;
            }
            return summon == null
                ? (int)SoulSalvageFocusedTargetState.Corpse
                : (int)SoulSalvageFocusedTargetState.ActiveSummon;
        }

        internal static float GetFocusedTargetQuality01ForInterop()
        {
            Location location;
            NpcHeroSummon summon;
            if (!TryFindFocusedSoulTargetCached(out location, out summon))
            {
                return 0.0f;
            }
            if (summon != null)
            {
                ReanimationRecord record;
                if (Reanimations.TryGetValue(((Model)summon).ID, out record))
                {
                    return record.Quality01;
                }
                return CalculateQuality01(location, summon.ParentModel);
            }
            return CalculateQuality01(location, null);
        }

        internal static int GetFocusedTargetQualityTierForInterop()
        {
            if (GetFocusedTargetStateForInterop(true)
                == (int)SoulSalvageFocusedTargetState.None)
            {
                return (int)Grailwright.Shared.CorpseQualityTier.None;
            }
            return (int)Grailwright.Shared.CorpseQualityBuckets.GetTier(
                GetFocusedTargetQuality01ForInterop(),
                true);
        }

        internal static float GetFocusedBindingProgress01ForInterop()
        {
            Location location;
            NpcHeroSummon summon;
            if (!TryFindFocusedSoulTargetCached(out location, out summon)
                || summon != null)
            {
                return 0.0f;
            }
            float quality = CalculateQuality01(location, null);
            Grailwright.Shared.CorpseQualityTier tier =
                Grailwright.Shared.CorpseQualityBuckets.GetTier(quality, true);
            return SoulProgressionRuntime.GetBindingProgress01(
                GetCorpseFingerprint(location),
                tier);
        }

        private static bool IsSoulSalvageEquipped()
        {
            Hero hero = Hero.Current;
            if (hero == null || hero.HeroItems == null)
            {
                return false;
            }
            return IsSoulSalvageItem(
                    hero.HeroItems.EquippedItem(EquipmentSlotType.MainHand))
                || IsSoulSalvageItem(
                    hero.HeroItems.EquippedItem(EquipmentSlotType.OffHand));
        }

        private static bool TryFindFocusedSoulTarget(
            out Location location,
            out NpcHeroSummon summon)
        {
            location = null;
            summon = null;
            Hero hero = Hero.Current;
            if (hero == null
                || hero.VHeroController == null
                || hero.VHeroController.Raycaster == null)
            {
                return false;
            }

            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                SoulSalvageRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                VLocation view = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                if (candidate == null || candidate.HasBeenDiscarded)
                {
                    return false;
                }

                NpcElement npc = candidate.TryGetElement<NpcElement>();
                NpcHeroSummon candidateSummon = npc == null
                    ? null
                    : npc.TryGetElement<NpcHeroSummon>();
                if (candidateSummon != null
                    && candidateSummon.Ally == hero
                    && npc.IsAlive)
                {
                    location = candidate;
                    summon = candidateSummon;
                    return true;
                }

                Corpse corpse = candidate.TryGetElement<Corpse>();
                if (corpse == null)
                {
                    return false;
                }
                string rejection;
                if (TryValidateEligibleCorpse(hero, candidate, out rejection))
                {
                    location = candidate;
                    return true;
                }
                return false;
            }
            return false;
        }

        private static bool TryFindFocusedSoulTargetCached(
            out Location location,
            out NpcHeroSummon summon)
        {
            int frame = Time.frameCount;
            if (_focusedTargetCacheFrame != frame)
            {
                _focusedTargetCacheFrame = frame;
                _focusedTargetCacheFound = TryFindFocusedSoulTarget(
                    out _focusedTargetCacheLocation,
                    out _focusedTargetCacheSummon);
            }
            location = _focusedTargetCacheLocation;
            summon = _focusedTargetCacheSummon;
            return _focusedTargetCacheFound;
        }

        private static float GetHeavyCastManaCost(Item item)
        {
            return item != null
                && item.ItemStats != null
                && item.ItemStats.HeavyCastManaCost != null
                    ? Math.Max(0.0f, item.ItemStats.HeavyCastManaCost.ModifiedValue)
                    : 30.0f;
        }

        private static string GetCorpseFingerprint(Location source)
        {
            string templateGuid = source != null && source.Template != null
                ? source.Template.GUID
                : string.Empty;
            Vector3 position = source == null ? Vector3.zero : source.Coords;
            return string.Join(
                "|",
                templateGuid ?? string.Empty,
                Mathf.RoundToInt(position.x * 4.0f).ToString(CultureInfo.InvariantCulture),
                Mathf.RoundToInt(position.y * 4.0f).ToString(CultureInfo.InvariantCulture),
                Mathf.RoundToInt(position.z * 4.0f).ToString(CultureInfo.InvariantCulture),
                GetCorpseDisplayName(source));
        }

        private static float CalculateQuality01(Location source, NpcElement knownNpc)
        {
            NpcElement npc = knownNpc;
            if (npc == null && source != null)
            {
                npc = source.TryGetElement<NpcElement>();
            }
            object template = npc != null
                ? (object)npc.Template
                : source == null ? null : NpcTemplate.FromNpcOrDummy(source);
            int nativeTier;
            bool hasNativeTier = TryReadNativeTier(template, out nativeTier)
                || TryReadNativeTier(source == null ? null : source.Template, out nativeTier);
            float killXp = ReadFirstPositiveFloat(
                npc,
                template,
                source,
                "ExpReward",
                "KillExp",
                "KillXp",
                "ExperienceReward");
            float maximumHealth = npc != null && npc.Health != null
                ? Math.Max(0.0f, npc.Health.UpperLimit)
                : ReadFirstPositiveFloat(
                    source,
                    template,
                    null,
                    "MaxHealth",
                    "MaximumHealth",
                    "Health");
            bool hasEvidence;
            bool usedNativeTier;
            float quality = Grailwright.Shared.CorpseQualityBuckets
                .CalculateIntrinsicQuality01(
                    hasNativeTier ? nativeTier : -1,
                    killXp,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceKillXp,
                    maximumHealth,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceMaxHealth,
                    out hasEvidence,
                    out usedNativeTier);
            if (!hasEvidence)
            {
                quality = 0.20f;
            }

            Grailwright.Shared.CorpseQualityThreatClass threat =
                ReadThreatClass(template);
            quality = Grailwright.Shared.CorpseQualityBuckets
                .ApplyThreatClassAdjustment(quality, threat);
            float enemyLevel = ReadFirstPositiveFloat(
                npc,
                template,
                source,
                "ExpLevel",
                "Level",
                "CharacterLevel");
            float heroLevel = Hero.Current == null
                ? -1.0f
                : ReadFirstPositiveFloat(
                    Hero.Current.HeroStats,
                    Hero.Current,
                    null,
                    "CharacterLevel",
                    "Level",
                    "ExpLevel");
            bool adjusted;
            return Grailwright.Shared.CorpseQualityBuckets
                .ApplyBoundedRelativeLevelAdjustment(
                    quality,
                    enemyLevel <= 0.0f ? -1.0f : enemyLevel,
                    heroLevel <= 0.0f ? -1.0f : heroLevel,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultLevelQualityPerLevel,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultMaximumLevelQualityAdjustment,
                    out adjusted);
        }

        private static bool TryReadNativeTier(object owner, out int nativeTier)
        {
            nativeTier = -1;
            IEnumerable tags = GetMemberValue(owner, "Tags") as IEnumerable;
            if (tags == null || tags is string)
            {
                return false;
            }
            foreach (object value in tags)
            {
                string tag = value as string;
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }
                for (int tier = 0; tier <= 7; tier++)
                {
                    if (string.Equals(
                        tag,
                        "Tier:" + tier.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                    {
                        nativeTier = tier;
                        return true;
                    }
                }
            }
            return false;
        }

        private static Grailwright.Shared.CorpseQualityThreatClass ReadThreatClass(
            object owner)
        {
            object value = GetMemberValue(owner, "NpcType");
            string name = value == null ? string.Empty : value.ToString();
            if (string.Equals(name, "Boss", StringComparison.Ordinal))
            {
                return Grailwright.Shared.CorpseQualityThreatClass.Boss;
            }
            if (string.Equals(name, "MiniBoss", StringComparison.Ordinal))
            {
                return Grailwright.Shared.CorpseQualityThreatClass.MiniBoss;
            }
            return string.Equals(name, "Elite", StringComparison.Ordinal)
                ? Grailwright.Shared.CorpseQualityThreatClass.Elite
                : Grailwright.Shared.CorpseQualityThreatClass.Normal;
        }

        private static float ReadFirstPositiveFloat(
            object first,
            object second,
            object third,
            params string[] names)
        {
            object[] owners = { first, second, third };
            foreach (object owner in owners)
            {
                foreach (string name in names)
                {
                    object value = GetMemberValue(owner, name);
                    if (value is Stat)
                    {
                        float statValue = ((Stat)value).ModifiedValue;
                        if (statValue > 0.0f)
                        {
                            return statValue;
                        }
                    }
                    try
                    {
                        float numeric = value == null
                            ? 0.0f
                            : Convert.ToSingle(value, CultureInfo.InvariantCulture);
                        if (numeric > 0.0f)
                        {
                            return numeric;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return 0.0f;
        }

        private static object GetMemberValue(object owner, string name)
        {
            if (owner == null)
            {
                return null;
            }
            Type type = owner.GetType();
            PropertyInfo property = GetPropertySilent(type, name);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(owner, null);
                }
                catch
                {
                }
            }
            FieldInfo field = GetFieldSilent(type, name);
            if (field != null)
            {
                try
                {
                    return field.GetValue(owner);
                }
                catch
                {
                }
            }
            return null;
        }

        private static PropertyInfo GetPropertySilent(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            Dictionary<string, PropertyInfo> members;
            if (!OptionalPropertyCache.TryGetValue(type, out members))
            {
                members = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
                OptionalPropertyCache[type] = members;
            }
            PropertyInfo property;
            if (members.TryGetValue(name, out property))
            {
                return property;
            }
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;
            Type current = type;
            while (current != null)
            {
                property = current.GetProperty(name, flags);
                if (property != null)
                {
                    break;
                }
                current = current.BaseType;
            }
            members[name] = property;
            return property;
        }

        private static FieldInfo GetFieldSilent(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            Dictionary<string, FieldInfo> members;
            if (!OptionalFieldCache.TryGetValue(type, out members))
            {
                members = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
                OptionalFieldCache[type] = members;
            }
            FieldInfo field;
            if (members.TryGetValue(name, out field))
            {
                return field;
            }
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;
            Type current = type;
            while (current != null)
            {
                field = current.GetField(name, flags);
                if (field != null)
                {
                    break;
                }
                current = current.BaseType;
            }
            members[name] = field;
            return field;
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            MethodInfo method = AccessTools.Method(type, name);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            Type[] arguments)
        {
            MethodInfo method = AccessTools.Method(type, name, arguments);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }
    }
}
