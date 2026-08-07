# Grail Floating Text API

Use Grail Floating Text as an optional BepInEx dependency:

```csharp
[BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
```

Resolve the API by reflection so your mod still loads when the provider is not installed:

```csharp
private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";

private MethodInfo _grailFloatingTextTryClaimConsolidatedXpGainMethod;
private MethodInfo _grailFloatingTextTryClaimXpGainMethod;
private MethodInfo _grailFloatingTextTryShowDeferredEventMethod;
private MethodInfo _grailFloatingTextTryShowEventWithIconMethod;
private MethodInfo _grailFloatingTextTryShowWithIconMethod;
private MethodInfo _grailFloatingTextTryShowMethod;

private bool TryResolveGrailFloatingText()
{
    PluginInfo pluginInfo;
    if (!Chainloader.PluginInfos.TryGetValue(GrailFloatingTextPluginGuid, out pluginInfo) ||
        pluginInfo == null ||
        pluginInfo.Instance == null)
    {
        return false;
    }

    Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(GrailFloatingTextApiTypeName, false);
    if (apiType == null)
    {
        return false;
    }

    _grailFloatingTextTryClaimConsolidatedXpGainMethod = AccessTools.Method(
        apiType,
        "TryClaimConsolidatedXpGain",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(float)
        });

    _grailFloatingTextTryClaimXpGainMethod = AccessTools.Method(
        apiType,
        "TryClaimXpGain",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(float)
        });

    _grailFloatingTextTryShowDeferredEventMethod = AccessTools.Method(
        apiType,
        "TryShowEvent",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(float),
            typeof(float)
        });

    _grailFloatingTextTryShowEventWithIconMethod = AccessTools.Method(
        apiType,
        "TryShowEvent",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(float),
            typeof(float)
        });

    _grailFloatingTextTryShowWithIconMethod = AccessTools.Method(
        apiType,
        "TryShow",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(float)
        });

    _grailFloatingTextTryShowMethod = AccessTools.Method(
        apiType,
        "TryShow",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(float)
        });

    return _grailFloatingTextTryClaimConsolidatedXpGainMethod != null ||
        _grailFloatingTextTryClaimXpGainMethod != null ||
        _grailFloatingTextTryShowDeferredEventMethod != null ||
        _grailFloatingTextTryShowEventWithIconMethod != null ||
        _grailFloatingTextTryShowWithIconMethod != null ||
        _grailFloatingTextTryShowMethod != null;
}
```

API v9 also exposes `GrailFloatingText.QuickWheelPanelApi` for a persistent
right-anchored two-column surface. Call `TrySet` while your quick-wheel owner is
active, `SetTooltipActive` when its normal item tooltip opens or closes, and
`Clear` when the wheel closes. The caller owns the rows and limits; GFT owns
font resolution, configured style colors, built-in icons, layout, and fading.

```text
TrySet(sourceId, leftTitle, leftSubtitle, leftTexts, leftIconIds, leftStyles,
       rightTitle, rightSubtitle, rightTexts, rightIconIds, rightStyles,
       opacity, tooltipOpacity, fadeSeconds, rightOffset, topOffset, scale)
SetTooltipActive(sourceId, active)
Clear(sourceId)
```

API v7 deferred event call shape:

```csharp
TryShowEvent(sourceId, eventId, text, style, category, priority, collapseKey, iconId, durationBucket, deliveryPoint, fadeSeconds, opacity)
```

Delivery points:

- `Immediate`: queue the message now.
- `OnMainMenu`: wait until the interactive title view is active, loading and camera transitions have ended, and the game window is focused.
- `OnLoad`: wait until the hero and world are initialized, loading and camera transitions have ended, no fullscreen video or cutscene covers the view, and the game window is focused.

Deferred messages persist until shown. Their duration starts on the first eligible IMGUI repaint, not when the API call is made.

API v8 consolidated XP claim call shape:

```csharp
TryClaimConsolidatedXpGain(sourceId, eventId, consolidationKey, textFormat, style, category, priority, iconId, durationBucket, expectedAmount, fadeSeconds, opacity)
```

Call it immediately before changing the hero XP stat. GFT batches only claims with the same `sourceId`, `consolidationKey`, and presentation, then replaces `{xp}` or `{amount}` in `textFormat` with the summed amount. Use distinct keys for meanings that must remain separate. If API v8 is unavailable, fall back to the API v6 one-shot claim.

API v6 one-shot XP claim call shape:

```csharp
TryClaimXpGain(sourceId, eventId, text, style, category, priority, iconId, durationBucket, expectedAmount, fadeSeconds, opacity)
```

The next matching XP gain uses the claimed text/event/style/icon instead of the generic XP event. API v6 claims remain separate.

API v4 event-aware call shape:

```csharp
TryShowEvent(sourceId, eventId, text, style, category, priority, collapseKey, iconId, durationBucket, fadeSeconds, opacity)
```

API v3 call shape:

```csharp
TryShow(sourceId, text, style, category, priority, collapseKey, iconId, durationSeconds, fadeSeconds, opacity)
```

API v2 calls still work:

```csharp
TryShow(sourceId, text, style, category, priority, collapseKey, durationSeconds, fadeSeconds, opacity)
```

Recommended values:

- `sourceId`: your plugin GUID.
- `eventId`: stable event token, such as `killing-blow` or `wyrd-hunt-status`, used for configurable color-group routing.
- `style`: `Default`, `Reward`, `Status`, `Wyrd`, `Discovery`, `Combat`, `Rest`, `System`, `Warning`, `Error`, `Critical`, a configured color-group name, or a hex color. Configured group names take priority over HTML named colors, so values such as `Purple`, `Red`, and `Blue` use their matching settings.
- `category`: `General`, `Combat`, `Reward`, `Status`, `System`, or `Debug`.
- `priority`: `Low`, `Normal`, `High`, or `Critical`.
- `collapseKey`: leave blank for stacking event messages; set a stable key for status messages that should update in place.
- `iconId`: blank or `Auto` uses the default icon for the style/category. Use `None` or `Off` to suppress the icon for one message. Blank per-group icon-color overrides inherit the resolved text color; a configured override tints only the foreground icon.
- `durationBucket`: `VeryShort`, `Short`, `Medium`, `Long`, `VeryLong`, or `System`; their defaults are 3, 3.5, 4, 4.5, 5, and 10 seconds respectively. Blank or unknown values use `Medium`. `System` is intended for startup, config-reset, and load-time error messages.
- `deliveryPoint`: `Immediate`, `OnMainMenu`, or `OnLoad`.
- `fadeSeconds`: fixed fade-in duration and the Medium-duration fade-out baseline. Fade-out scales with total display duration from 60% to 200% of this value and never exceeds half the message lifetime. With the recommended `0.25`, Medium fades out over 0.25 seconds and System over 0.5 seconds.
- `text`: the exact XP text to show, such as `+42 XP (Worthy)`.
- `consolidationKey`: a stable key scoped to `sourceId`; only matching keys from the same source can merge.
- `textFormat`: consolidated XP text containing `{xp}` or `{amount}`, such as `+{xp} XP (Worthy)`.
- `expectedAmount`: the XP amount your mod is about to add. Grail Floating Text matches exact amounts first, then very recent claims so game XP multipliers can still display the final adjusted amount.

Built-in icon IDs:

- Core: `general`, `system`, `status`, `wyrd`, `reward`, `combat`, `warning`, `critical`, `debug`.
- Game events: `rest`, `location`, `parry`, `crime`, `pickpocket`, `weight`, `experience`, `corpse`.
- Skills: `one_handed`, `two_handed`, `archery`, `shield`, `unarmed`, `magic`.

Feature probes:

- `ApiVersion9` and `quick-wheel-panels-v1`: provider supports persistent two-column quick-wheel panels through `QuickWheelPanelApi`.
- `ApiVersion8`: provider supports source-isolated XP consolidation through `TryClaimConsolidatedXpGain`.
- `XpConsolidation`: provider can queue XP and consolidate compatible generic or opted-in claimed gains.
- `ApiVersion7`: provider supports deferred delivery through the API v7 `TryShowEvent` overload.
- `DeferredDelivery`: provider accepts deferred delivery points.
- `OnMainMenuDelivery`: provider can wait for the usable main menu.
- `OnLoadDelivery`: provider can wait for fully visible loaded gameplay.
- `ApiVersion6`: provider supports text-aware XP gain claims through `TryClaimXpGain`.
- `ApiVersion4`: provider supports event IDs and duration buckets through `TryShowEvent`.
- `XpGainClaims`: provider can claim, style, and name the next XP gain.
- `XpNotifications`: provider can take over vanilla XP gain display.
- `EventIds`: provider accepts stable event IDs for configurable color-group routing.
- `DurationBuckets`: provider accepts named duration buckets.
- `SystemDuration`: provider accepts the configurable `System` duration bucket.
- `ColorGroups`: provider supports configurable event color groups.
- `DefaultGameEvents`: provider includes built-in game-event notifications.
- `RestEvents`: provider can show built-in rest-duration notifications.
- `CombatDefenseEvents`: provider can show built-in block/parry notifications.
- `EncumbranceEvents`: provider can show built-in encumbrance state notifications.
- `LocationClearEvents`: provider can show built-in location-cleared notifications.
- `CrimeEvents`: provider can show built-in crime, bounty, and pickpocket notifications.
- `CombatHitEvents`: provider can show built-in weak spot and sneak attack notifications.
- `VanillaWyrdEvents`: provider can show built-in vanilla Wyrd notifications.

You can also query the installed provider:

```csharp
string[] ids = NotificationApi.GetBuiltInIconIds();
```

Examples:

```csharp
// Show a startup message only after loaded gameplay is fully visible.
InvokeTryShowDeferredEvent(PluginGuid, "startup-ready", text, "System", "System", "High", "", "system", "System", "OnLoad", 0.25f, 0.9f);

// Show a critical compatibility warning once the main menu can be used.
InvokeTryShowDeferredEvent(PluginGuid, "compatibility-warning", text, "Warning", "System", "Critical", "", "warning", "System", "OnMainMenu", 0.25f, 1.0f);

// Stack killing-blow reward messages with a skill icon. The default RedEvents config colors this event red.
InvokeTryShowEvent(PluginGuid, "killing-blow", text, "Reward", "Reward", "Normal", "", "archery", "Medium", 0.25f, 0.9f);

// Update one status line in place with the Wyrd icon.
InvokeTryShowEvent(PluginGuid, "wyrd-hunt-status", text, "Wyrd", "Status", "Normal", "wyrd-hunt-status", "wyrd", "Medium", 0.25f, 0.9f);
```
