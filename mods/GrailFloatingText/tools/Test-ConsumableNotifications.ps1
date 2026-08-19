[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$mainSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$consumableSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ConsumableNotifications.cs") -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw

foreach ($requiredToken in @(
    "PatchConsumableNotifications();",
    "BindConsumableNotificationConfig();",
    "IsConsumableHealingClaimed()",
    '"NotifyFoodConsumed",',
    '"NotifyPotionConsumed",',
    '"IncludeConsumableDescription",',
    '"NotifyPotionOverdrinkTrigger",',
    '"SuppressVanillaPotionNotifications",',
    '"default-food-consumed"',
    '"default-potion-consumed"',
    '"default-potion-poisoning"',
    '"Potion Poisoning"',
    '"Blue"',
    '"Gold"',
    '"Orange"',
    '"Red"',
    'isPotion ? "potion" : "food"',
    '"potion"',
    'isPotion ? "Blue" : "Orange"',
    "nameof(Item.Use)",
    "HarmonyMethod itemUsePrefixPatch",
    "itemUsePrefixPatch.after",
    "SteelAndBonePluginGuid",
    "internal static bool Prefix(Item __instance, ref ConsumableUseNotificationState __state)",
    "typeof(SpecialItemNotificationBuffer)",
    "nameof(SpecialItemNotificationBuffer.TryToPush)",
    "state.NotificationShown",
    "ReferenceEquals(state.Item, item)",
    "typeof(BuildupStatusActivation)",
    '"OnBuildupComplete"',
    "buildupStatus.Active",
    '"60a2ed0287e14c944b53b6ab5870becd"',
    "typeof(VCHeroStatusAnnouncer)",
    '"OnBuildupCompleted"')) {
    if ($mainSource.IndexOf($requiredToken, [StringComparison]::Ordinal) -lt 0 -and
        $consumableSource.IndexOf($requiredToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Consumable notification contract is missing $requiredToken."
    }
}

foreach ($settingContract in @(
    '(?s)"NotifyFoodConsumed",\s*true,',
    '(?s)"NotifyPotionConsumed",\s*true,',
    '(?s)"IncludeConsumableDescription",\s*false,',
    '(?s)"NotifyPotionOverdrinkTrigger",\s*true,',
    '(?s)"SuppressVanillaPotionNotifications",\s*true,')) {
    if ($consumableSource -notmatch $settingContract) {
        throw "Consumable notification setting contract failed: $settingContract"
    }
}

foreach ($forbiddenToken in @(
    "BuildupProgress",
    "VCQuickItemTooltipUI",
    "VCQuickSlot",
    "VCQuickLoadout",
    "AdvancedNotificationBuffer<ItemNotification>",
    "ConsumableNotificationDiagnostics")) {
    if ($consumableSource.IndexOf($forbiddenToken, [StringComparison]::Ordinal) -ge 0) {
        throw "Consumable notifications must not depend on $forbiddenToken."
    }
}

if ($manifest.IndexOf(
    '"src/ConsumableNotifications.cs"',
    [StringComparison]::Ordinal) -lt 0) {
    throw "The consumable notification source is missing from mod.json."
}

if ($manifest.IndexOf(
    '"src/ConsumableNotificationDiagnostics.cs"',
    [StringComparison]::Ordinal) -ge 0) {
    throw "The superseded consumable diagnostic source remains in mod.json."
}

Write-Output "Consumable notification contract passed."
