using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Awaken.TG.Assets;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.Main.AI.SummonsAndAllies;
using Awaken.TG.Main.AI.Utils;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Tooltips;
using Awaken.TG.Main.Heroes.Items.Weapons;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Thievery;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Actions;
using Awaken.TG.Main.Locations.Attachments.Elements;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Locations.Views;
using HarmonyLib;
using UnityEngine;

namespace SoulAndService
{
    internal static class SoulSalvageRuntime
    {
        private const string SoulSalvageTemplateGuid =
            "7bdd3a1b62fb53d46b8a28142c18a110";
        private const string GenericRaisedServantPortraitKey =
            "759a3e6e96ddae742ab8cde19fae42f0";
        private const float NativeManaRefundMultiplier = 0.75f;
        private const float HeavyCastManaCostMultiplier = 3.0f;
        private const float SoulSalvageRange = 50.0f;

        private sealed class ReanimationRecord
        {
            internal Location SourceCorpse;
            internal Location RaisedLocation;
            internal NpcElement RaisedNpc;
            internal LocationInteractability SourceInteractability;
            internal string SourceDisplayName;
        }

        private struct CastState
        {
            internal bool IsSoulSalvage;
            internal bool IsHeavy;
            internal Item Item;
        }

        private static readonly Dictionary<string, ReanimationRecord> Reanimations =
            new Dictionary<string, ReanimationRecord>();
        private static readonly List<Location> PendingRaisedDiscards =
            new List<Location>();
        private static readonly Dictionary<string, ItemStats> SoulSalvageItems =
            new Dictionary<string, ItemStats>();
        private static readonly Dictionary<string, StatTweak> HeavyCostTweaks =
            new Dictionary<string, StatTweak>();
        private static readonly HashSet<MagicItemTemplateInfo> LightCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private static readonly HashSet<MagicItemTemplateInfo> HeavyCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private static readonly FieldInfo LocationInitializerField =
            AccessTools.Field(typeof(Location), "_initializer");

        private static bool _lightCastActive;
        private static bool _heavyCastActive;
        private static bool _lightHealthGranted;
        private static NpcHeroSummon _lightTarget;
        private static float _lightOriginalMana;
        private static float _lightHealthFraction;
        private static float _itemRefreshDelay;

        internal static void Patch(Harmony harmony)
        {
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
                RequireMethod(typeof(ItemStats), "OnInitialize"),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterItemStatsInitialized)));
            harmony.Patch(
                RequireMethod(typeof(MagicItemTemplateInfo), "get_MagicDescription"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeGetMagicDescription)));
        }

        internal static void Update()
        {
            UpdateSoulSalvageItems();
            UpdateReanimationDecay(Time.deltaTime);

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
            ClearLightCastState();
        }

        internal static void OnSummonDiscarded(NpcHeroSummon summon)
        {
            if (summon == null)
            {
                return;
            }
            RestoreSourceCorpse(
                ((Model)summon).ID,
                discardRaisedCopy: true,
                showDiagnostic: true);
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
                string destination;
                switch (plugin.SoulSalvageReturn.Value)
                {
                    case SoulSalvageReturnMode.Mana:
                        destination = "Mana";
                        break;
                    case SoulSalvageReturnMode.Health:
                        destination = "Health";
                        break;
                    default:
                        destination = "an even split of Health and Mana";
                        break;
                }
                __result = "Sacrifices a targeted owned summon, returning up to "
                    + plugin.SoulSalvageEssencePercent.Value.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + "% of its original mana investment based on current health as "
                    + destination + ".";
                return false;
            }

            if (!HeavyCastInfos.Contains(__instance))
            {
                return true;
            }

            if (plugin.PermanentReanimations.Value)
            {
                __result = "Raises an eligible ordinary hostile corpse at full Health as a permanent allied summon. The source corpse returns when its service ends.";
                return false;
            }

            float typicalHealth = 200.0f;
            float typicalLifetime = typicalHealth
                / CalculateDecayPerSecond(typicalHealth, plugin);
            __result = "Raises an eligible ordinary hostile corpse at full Health as an allied summon. Borrowed life drains by "
                + plugin.ReanimationHealthDecayPercentPerSecond.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "% maximum Health plus "
                + plugin.ReanimationFlatHealthDecayPerSecond.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + " Health each second. Decay alone lasts at least "
                + FormatDuration(plugin.ReanimationMinimumLifetimeSeconds.Value)
                + " (about " + FormatDuration(typicalLifetime)
                + " for a typical enemy); stronger servants last longer. Combat damage can end service sooner.";
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

        private static void BeforeCastingEnded(
            CastingHand hand,
            bool lightCast,
            out CastState __state)
        {
            __state = default(CastState);
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || hero == null)
            {
                ClearLightCastState();
                return;
            }

            EquipmentSlotType slot = hand == CastingHand.OffHand
                ? EquipmentSlotType.OffHand
                : EquipmentSlotType.MainHand;
            Item item = hero.HeroItems.EquippedItem(slot);
            if (item == null
                || item.Template == null
                || !string.Equals(
                    item.Template.GUID,
                    SoulSalvageTemplateGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                ClearLightCastState();
                return;
            }

            __state = new CastState
            {
                IsSoulSalvage = true,
                IsHeavy = !lightCast,
                Item = item
            };
            _lightCastActive = lightCast;
            _heavyCastActive = !lightCast;
            _lightHealthGranted = false;
            _lightTarget = null;
            _lightOriginalMana = 0.0f;
            _lightHealthFraction = 0.0f;
        }

        private static void AfterCastingEnded(CastState __state)
        {
            try
            {
                if (__state.IsSoulSalvage && __state.IsHeavy)
                {
                    TryRaiseCorpse(__state.Item);
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
            _lightOriginalMana = Math.Max(0.0f, ____manaExpended);
            _lightHealthFraction = __instance.ParentModel != null
                && __instance.ParentModel.Health != null
                    ? Mathf.Clamp01(__instance.ParentModel.Health.Percentage)
                    : 0.0f;

            float essenceFraction = plugin.SoulSalvageEssencePercent.Value / 100.0f;
            float manaAllocation = GetManaAllocation(plugin.SoulSalvageReturn.Value);
            __result = NativeManaRefundMultiplier > 0.0f
                ? ____manaExpended * essenceFraction * manaAllocation
                    / NativeManaRefundMultiplier
                : 0.0f;
            return false;
        }

        private static bool BeforeDestroySummon(NpcHeroSummon __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (_heavyCastActive
                && plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value)
            {
                plugin.LogDiagnostic(
                    "Blocked vanilla heavy Soul Salvage summon sacrifice; heavy cast is reserved for corpse reanimation.");
                return false;
            }
            if (!_lightCastActive
                || _lightHealthGranted
                || plugin == null
                || hero == null
                || __instance == null
                || !ReferenceEquals(__instance, _lightTarget))
            {
                return true;
            }

            _lightHealthGranted = true;
            float essence = _lightOriginalMana
                * _lightHealthFraction
                * (plugin.SoulSalvageEssencePercent.Value / 100.0f);
            float health = essence * GetHealthAllocation(plugin.SoulSalvageReturn.Value);
            if (health > 0.0f)
            {
                hero.Health.IncreaseBy(health);
            }
            plugin.LogDiagnostic(
                "Soul Salvage sacrificed " + ((Model)__instance.ParentModel).ID
                + ": investedMana=" + _lightOriginalMana.ToString("0.##")
                + "; healthFraction=" + _lightHealthFraction.ToString("0.###")
                + "; essence=" + essence.ToString("0.##")
                + "; return=" + plugin.SoulSalvageReturn.Value + ".");
            return true;
        }

        private static float GetManaAllocation(SoulSalvageReturnMode mode)
        {
            if (mode == SoulSalvageReturnMode.Mana)
            {
                return 1.0f;
            }
            return mode == SoulSalvageReturnMode.Split ? 0.5f : 0.0f;
        }

        private static float GetHealthAllocation(SoulSalvageReturnMode mode)
        {
            if (mode == SoulSalvageReturnMode.Health)
            {
                return 1.0f;
            }
            return mode == SoulSalvageReturnMode.Split ? 0.5f : 0.0f;
        }

        private static void TryRaiseCorpse(Item sourceItem)
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
            string rejection;
            if (!TryFindEligibleCorpse(hero, out source, out rejection))
            {
                plugin.LogDiagnostic("Soul Salvage heavy cast raised nothing: " + rejection);
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "Soul Salvage: no eligible corpse - " + rejection + ".");
                return;
            }

            int summonCount = 0;
            foreach (NpcHeroSummon ignored in World.All<NpcHeroSummon>())
            {
                summonCount++;
            }
            int summonLimit = hero.HeroStats.SummonLimit.ModifiedInt
                + plugin.SummonLimitBonus.Value;
            if (summonCount >= summonLimit)
            {
                plugin.LogWarning(
                    "Soul Salvage could not raise " + source.DebugName
                    + " because the summon limit is full.");
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "Soul Salvage: servant limit full ("
                    + summonCount.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + summonLimit.ToString(CultureInfo.InvariantCulture)
                    + ").");
                return;
            }

            LocationInteractability previousInteractability = source.Interactability;
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
                    SourceDisplayName = GetCorpseDisplayName(source)
                };
                source.SetInteractability(LocationInteractability.Hidden);

                npc.OnCompletelyInitialized(
                    delegate
                    {
                        if (npc.HasBeenDiscarded)
                        {
                            return;
                        }
                        raised.RemoveElementsOfType<AliveLocationDeathReward>();
                        raised.RemoveElementsOfType<SearchAction>();
                        raised.RemoveElementsOfType<PickpocketAction>();
                        npc.RemoveElementsOfType<NpcHealthRegeneration>();
                        npc.Health.SetToFull();
                        raised.TriggerVisualScriptingEvent("OnResurrect");

                        float maximumHealth = npc.Health.UpperLimit;
                        float decayPerSecond = CalculateDecayPerSecond(
                            maximumHealth,
                            plugin);
                        float decayLifetime = decayPerSecond > 0.0f
                            ? maximumHealth / decayPerSecond
                            : float.PositiveInfinity;
                        plugin.LogDiagnostic(
                            "Raised a restricted runtime copy of " + source.DebugName
                            + "; maximumHealth="
                            + maximumHealth.ToString("0.##", CultureInfo.InvariantCulture)
                            + "; decayPerSecond="
                            + decayPerSecond.ToString("0.###", CultureInfo.InvariantCulture)
                            + "; decayLifetime="
                            + (plugin.PermanentReanimations.Value
                                ? "permanent"
                                : decayLifetime.ToString("0.##", CultureInfo.InvariantCulture))
                            + "; portrait="
                            + (usedFallbackPortrait
                                ? "generic-skeleton-summon"
                                : "native")
                            + ".");
                        string outcome = "Soul Salvage: raised "
                            + Reanimations[summonId].SourceDisplayName
                            + " at full health"
                            + (plugin.PermanentReanimations.Value
                                ? " permanently"
                                : "; borrowed life about "
                                    + FormatDuration(decayLifetime))
                            + (usedFallbackPortrait
                                ? " (generic portrait used)"
                                : string.Empty)
                            + ".";
                        plugin.ShowSoulSalvageHeavyCastDiagnostic(outcome);
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
                    "Soul Salvage could not create a raised servant: "
                    + exception.GetBaseException().Message);
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "Soul Salvage: reanimation failed - see BepInEx log.");
            }
        }

        private static void UpdateReanimationDecay(float deltaTime)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || plugin.PermanentReanimations.Value
                || deltaTime <= 0.0f
                || Reanimations.Count == 0)
            {
                return;
            }

            foreach (ReanimationRecord record in Reanimations.Values.ToArray())
            {
                NpcElement npc = record.RaisedNpc;
                if (npc == null
                    || ((Model)npc).HasBeenDiscarded
                    || npc.Health == null
                    || npc.HealthElement == null)
                {
                    continue;
                }

                float decay = CalculateDecayPerSecond(
                    npc.Health.UpperLimit,
                    plugin) * deltaTime;
                if (decay <= 0.0f)
                {
                    continue;
                }
                if (npc.Health.ModifiedValue <= decay + 0.001f)
                {
                    npc.HealthElement.Kill();
                }
                else
                {
                    npc.Health.DecreaseBy(decay);
                }
            }
        }

        private static float CalculateDecayPerSecond(
            float maximumHealth,
            SoulAndServicePlugin plugin)
        {
            maximumHealth = Math.Max(0.001f, maximumHealth);
            float percentDrain = maximumHealth
                * Math.Max(
                    0.0f,
                    plugin.ReanimationHealthDecayPercentPerSecond.Value)
                / 100.0f;
            float flatDrain = Math.Max(
                0.0f,
                plugin.ReanimationFlatHealthDecayPerSecond.Value);
            float combinedDrain = percentDrain + flatDrain;
            float minimumLifetime = Math.Max(
                0.001f,
                plugin.ReanimationMinimumLifetimeSeconds.Value);
            float maximumAllowedDrain = maximumHealth / minimumLifetime;
            if (combinedDrain > maximumAllowedDrain)
            {
                float scale = maximumAllowedDrain / combinedDrain;
                percentDrain *= scale;
                flatDrain *= scale;
            }
            return Math.Max(0.0001f, percentDrain + flatDrain);
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds < 120.0f)
            {
                return Math.Round(seconds).ToString(
                    "0",
                    CultureInfo.InvariantCulture) + "s";
            }
            return (seconds / 60.0f).ToString(
                "0.#",
                CultureInfo.InvariantCulture) + "m";
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
                source = candidate;
                rejection = string.Empty;
                return true;
            }
            return false;
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
                    plugin.LogDiagnostic("Soul Salvage restored the source corpse.");
                    if (showDiagnostic)
                    {
                        plugin.ShowSoulSalvageHeavyCastDiagnostic(
                            "Soul Salvage: " + record.SourceDisplayName
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
        }

        private static void ClearLightCastState()
        {
            _lightCastActive = false;
            _heavyCastActive = false;
            _lightHealthGranted = false;
            _lightTarget = null;
            _lightOriginalMana = 0.0f;
            _lightHealthFraction = 0.0f;
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
