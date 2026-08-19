using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Statuses.BuildUp;
using Awaken.TG.Main.UI.HUD.AdvancedNotifications.LeftScreen.SpecialItem;
using BepInEx.Configuration;
using HarmonyLib;

namespace GrailFloatingText
{
    public sealed partial class GrailFloatingTextPlugin
    {
        private const string DefaultFoodConsumedEventId = "default-food-consumed";
        private const string DefaultPotionConsumedEventId = "default-potion-consumed";
        private const string DefaultPotionPoisoningEventId = "default-potion-poisoning";
        private const string PotionPoisoningStatusGuid = "60a2ed0287e14c944b53b6ab5870becd";

        private static readonly Regex ConsumableDescriptionTagPattern =
            new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex ConsumableDescriptionWhitespacePattern =
            new Regex("\\s+", RegexOptions.Compiled);

        private ConfigEntry<bool> _notifyFoodConsumed;
        private ConfigEntry<bool> _notifyPotionConsumed;
        private ConfigEntry<bool> _includeConsumableDescription;
        private ConfigEntry<bool> _notifyPotionOverdrinkTrigger;
        private ConfigEntry<bool> _suppressVanillaPotionNotifications;

        private ConsumableUseNotificationState _activeConsumableUseNotification;
        private BuildupStatusActivation _claimedPotionPoisoningAnnouncement;

        private void BindConsumableNotificationConfig()
        {
            const string section = "Default Game Events";
            _notifyFoodConsumed = BindOrdered(
                section,
                "NotifyFoodConsumed",
                true,
                "Show the name of food consumed through Grail Floating Text.");
            _notifyPotionConsumed = BindOrdered(
                section,
                "NotifyPotionConsumed",
                true,
                "Show the name of a potion consumed through Grail Floating Text.");
            _includeConsumableDescription = BindOrdered(
                section,
                "IncludeConsumableDescription",
                false,
                "Append the item's full resolved native description to food and potion consumption text.");
            _notifyPotionOverdrinkTrigger = BindOrdered(
                section,
                "NotifyPotionOverdrinkTrigger",
                true,
                "Show a separate Potion Poisoning line when native overdrink buildup first activates the status. Buildup progress is never shown.");
            _suppressVanillaPotionNotifications = BindOrdered(
                section,
                "SuppressVanillaPotionNotifications",
                true,
                "Hide the native potion-use and Potion Poisoning announcements only when Grail Floating Text successfully shows their replacements.");
        }

        private void PatchConsumableNotifications()
        {
            try
            {
                if (_harmony == null)
                {
                    _harmony = new Harmony(PluginGuid);
                }

                MethodInfo itemUseOriginal = AccessTools.Method(
                    typeof(Item),
                    nameof(Item.Use),
                    Type.EmptyTypes);
                MethodInfo itemUsePrefix = AccessTools.Method(
                    typeof(ItemUseConsumableNotificationPatch),
                    nameof(ItemUseConsumableNotificationPatch.Prefix));
                MethodInfo itemUseFinalizer = AccessTools.Method(
                    typeof(ItemUseConsumableNotificationPatch),
                    nameof(ItemUseConsumableNotificationPatch.Finalizer));
                MethodInfo specialItemOriginal = AccessTools.Method(
                    typeof(SpecialItemNotificationBuffer),
                    nameof(SpecialItemNotificationBuffer.TryToPush),
                    new[] { typeof(Item) });
                MethodInfo specialItemPrefix = AccessTools.Method(
                    typeof(NativeConsumableNotificationPatch),
                    nameof(NativeConsumableNotificationPatch.Prefix));
                MethodInfo poisoningActivationOriginal = AccessTools.Method(
                    typeof(BuildupStatusActivation),
                    "OnBuildupComplete",
                    Type.EmptyTypes);
                MethodInfo poisoningActivationPrefix = AccessTools.Method(
                    typeof(PotionPoisoningActivationPatch),
                    nameof(PotionPoisoningActivationPatch.Prefix));
                MethodInfo buildupOriginal = AccessTools.Method(
                    typeof(BuildupStatus),
                    nameof(BuildupStatus.Buildup),
                    new[] { typeof(float), typeof(bool) });
                MethodInfo buildupPostfix = AccessTools.Method(
                    typeof(PotionPoisoningBuildupCleanupPatch),
                    nameof(PotionPoisoningBuildupCleanupPatch.Postfix));
                MethodInfo nativePoisoningOriginal = AccessTools.Method(
                    typeof(VCHeroStatusAnnouncer),
                    "OnBuildupCompleted",
                    new[] { typeof(BuildupStatus) });
                MethodInfo nativePoisoningPrefix = AccessTools.Method(
                    typeof(NativePotionPoisoningAnnouncementPatch),
                    nameof(NativePotionPoisoningAnnouncementPatch.Prefix));

                if (itemUseOriginal == null
                    || itemUsePrefix == null
                    || itemUseFinalizer == null
                    || specialItemOriginal == null
                    || specialItemPrefix == null
                    || poisoningActivationOriginal == null
                    || poisoningActivationPrefix == null
                    || buildupOriginal == null
                    || buildupPostfix == null
                    || nativePoisoningOriginal == null
                    || nativePoisoningPrefix == null)
                {
                    Logger.LogWarning(
                        PluginName
                        + " could not identify every food, potion, and Potion Poisoning notification target.");
                    return;
                }

                HarmonyMethod itemUsePrefixPatch = new HarmonyMethod(itemUsePrefix);
                itemUsePrefixPatch.after = new[]
                {
                    SteelAndBonePluginGuid
                };
                _harmony.Patch(
                    itemUseOriginal,
                    prefix: itemUsePrefixPatch,
                    finalizer: new HarmonyMethod(itemUseFinalizer));
                _harmony.Patch(specialItemOriginal, prefix: new HarmonyMethod(specialItemPrefix));
                _harmony.Patch(poisoningActivationOriginal, prefix: new HarmonyMethod(poisoningActivationPrefix));
                _harmony.Patch(buildupOriginal, postfix: new HarmonyMethod(buildupPostfix));
                _harmony.Patch(nativePoisoningOriginal, prefix: new HarmonyMethod(nativePoisoningPrefix));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    PluginName
                    + " could not patch food, potion, and Potion Poisoning notifications: "
                    + exception.GetBaseException().Message);
            }
        }

        private ConsumableUseNotificationState BeginConsumableUseNotification(Item item)
        {
            if (item == null
                || item.Template == null
                || item.Character != Hero.Current)
            {
                return null;
            }

            bool isPotion = item.Template.IsPotion;
            bool isFood = !isPotion && item.IsEdible;
            if ((!isPotion || _notifyPotionConsumed == null || !_notifyPotionConsumed.Value)
                && (!isFood || _notifyFoodConsumed == null || !_notifyFoodConsumed.Value))
            {
                return null;
            }

            string text = item.DisplayName;
            if (_includeConsumableDescription != null
                && _includeConsumableDescription.Value)
            {
                string description = NormalizeConsumableDescription(
                    item.DescriptionFor(Hero.Current));
                if (!string.IsNullOrWhiteSpace(description))
                {
                    text += ": " + description;
                }
            }

            ConsumableUseNotificationState state = new ConsumableUseNotificationState
            {
                Item = item,
                IsPotion = isPotion,
                Previous = _activeConsumableUseNotification
            };
            state.NotificationShown = TryShowCore(
                PluginGuid,
                isPotion ? DefaultPotionConsumedEventId : DefaultFoodConsumedEventId,
                text,
                isPotion ? "Blue" : "Orange",
                "Status",
                "Normal",
                string.Empty,
                isPotion ? "potion" : "food",
                GetDurationBucketSeconds(DurationBucket.Short),
                0.25f,
                0.9f);
            _activeConsumableUseNotification = state;
            return state;
        }

        private void EndConsumableUseNotification(ConsumableUseNotificationState state)
        {
            if (state != null && ReferenceEquals(_activeConsumableUseNotification, state))
            {
                _activeConsumableUseNotification = state.Previous;
            }
        }

        private bool IsConsumableHealingClaimed()
        {
            return _activeConsumableUseNotification != null
                && _activeConsumableUseNotification.NotificationShown;
        }

        private bool ShouldSuppressNativeConsumableNotification(Item item)
        {
            ConsumableUseNotificationState state = _activeConsumableUseNotification;
            return state != null
                && state.IsPotion
                && state.NotificationShown
                && _suppressVanillaPotionNotifications != null
                && _suppressVanillaPotionNotifications.Value
                && ReferenceEquals(state.Item, item);
        }

        private bool TryClaimPotionPoisoningAnnouncement(
            BuildupStatusActivation buildupStatus)
        {
            if (buildupStatus == null
                || buildupStatus.Active
                || buildupStatus.Character != Hero.Current
                || buildupStatus.Template == null
                || !string.Equals(
                    buildupStatus.Template.GUID,
                    PotionPoisoningStatusGuid,
                    StringComparison.OrdinalIgnoreCase)
                || _notifyPotionOverdrinkTrigger == null
                || !_notifyPotionOverdrinkTrigger.Value)
            {
                return false;
            }

            bool shown = TryShowCore(
                PluginGuid,
                DefaultPotionPoisoningEventId,
                "Potion Poisoning",
                "Red",
                "Status",
                "High",
                string.Empty,
                "potion",
                GetDurationBucketSeconds(DurationBucket.Medium),
                0.25f,
                0.9f);
            if (shown)
            {
                _claimedPotionPoisoningAnnouncement = buildupStatus;
            }
            return shown;
        }

        private bool ShouldSuppressNativePotionPoisoningAnnouncement(
            BuildupStatus buildupStatus)
        {
            bool claimed = _suppressVanillaPotionNotifications != null
                && _suppressVanillaPotionNotifications.Value
                && ReferenceEquals(
                    _claimedPotionPoisoningAnnouncement,
                    buildupStatus);
            if (claimed)
            {
                _claimedPotionPoisoningAnnouncement = null;
            }
            return claimed;
        }

        private void ClearPotionPoisoningAnnouncementClaim(
            BuildupStatus buildupStatus)
        {
            if (ReferenceEquals(_claimedPotionPoisoningAnnouncement, buildupStatus))
            {
                _claimedPotionPoisoningAnnouncement = null;
            }
        }

        private static string NormalizeConsumableDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            string withoutTags = ConsumableDescriptionTagPattern.Replace(
                description,
                string.Empty);
            return ConsumableDescriptionWhitespacePattern.Replace(
                withoutTags,
                " ").Trim();
        }

        private static void LogConsumableNotificationFailure(
            string stage,
            Exception exception)
        {
            Log?.LogWarning(
                "Consumable notification handling failed at "
                + stage
                + ": "
                + exception.GetBaseException().Message);
        }

        private sealed class ConsumableUseNotificationState
        {
            internal Item Item;
            internal bool IsPotion;
            internal bool NotificationShown;
            internal ConsumableUseNotificationState Previous;
        }

        private static class ItemUseConsumableNotificationPatch
        {
            internal static bool Prefix(Item __instance, ref ConsumableUseNotificationState __state)
            {
                try
                {
                    __state = Instance?.BeginConsumableUseNotification(__instance);
                }
                catch (Exception exception)
                {
                    __state = null;
                    LogConsumableNotificationFailure("Item.Use begin", exception);
                }

                return true;
            }

            internal static Exception Finalizer(
                ConsumableUseNotificationState __state,
                Exception __exception)
            {
                try
                {
                    Instance?.EndConsumableUseNotification(__state);
                }
                catch (Exception exception)
                {
                    LogConsumableNotificationFailure("Item.Use end", exception);
                }
                return __exception;
            }
        }

        private static class NativeConsumableNotificationPatch
        {
            internal static bool Prefix(Item __0)
            {
                try
                {
                    GrailFloatingTextPlugin plugin = Instance;
                    return plugin == null
                        || !plugin.ShouldSuppressNativeConsumableNotification(__0);
                }
                catch (Exception exception)
                {
                    LogConsumableNotificationFailure(
                        "SpecialItemNotificationBuffer.TryToPush",
                        exception);
                    return true;
                }
            }
        }

        private static class PotionPoisoningActivationPatch
        {
            internal static void Prefix(BuildupStatusActivation __instance)
            {
                try
                {
                    Instance?.TryClaimPotionPoisoningAnnouncement(__instance);
                }
                catch (Exception exception)
                {
                    LogConsumableNotificationFailure(
                        "Potion Poisoning activation",
                        exception);
                }
            }
        }

        private static class PotionPoisoningBuildupCleanupPatch
        {
            internal static void Postfix(BuildupStatus __instance)
            {
                try
                {
                    Instance?.ClearPotionPoisoningAnnouncementClaim(__instance);
                }
                catch (Exception exception)
                {
                    LogConsumableNotificationFailure(
                        "Potion Poisoning claim cleanup",
                        exception);
                }
            }
        }

        private static class NativePotionPoisoningAnnouncementPatch
        {
            internal static bool Prefix(BuildupStatus __0)
            {
                try
                {
                    GrailFloatingTextPlugin plugin = Instance;
                    return plugin == null
                        || !plugin.ShouldSuppressNativePotionPoisoningAnnouncement(__0);
                }
                catch (Exception exception)
                {
                    LogConsumableNotificationFailure(
                        "VCHeroStatusAnnouncer.OnBuildupCompleted",
                        exception);
                    return true;
                }
            }
        }
    }
}
