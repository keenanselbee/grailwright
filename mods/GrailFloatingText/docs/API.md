# Grail Floating Text API

Use Grail Floating Text as an optional BepInEx dependency:

```csharp
[BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
```

Resolve the API by reflection so your mod still loads when the provider is not installed:

```csharp
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";

private MethodInfo _grailFloatingTextSupportsFeatureMethod;
private MethodInfo _grailFloatingTextTryClaimConsolidatedXpGainMethod;
private MethodInfo _grailFloatingTextTryClaimXpGainMethod;
private MethodInfo _grailFloatingTextTryCancelXpGainClaimMethod;
private MethodInfo _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod;
private MethodInfo _grailFloatingTextTrySetBuiltInEventClaimMethod;
private MethodInfo _grailFloatingTextTryShowCompatibilityNoticeMethod;
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

    _grailFloatingTextSupportsFeatureMethod = AccessTools.Method(
        apiType,
        "SupportsFeature",
        new[] { typeof(string) });

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

    _grailFloatingTextTryCancelXpGainClaimMethod = AccessTools.Method(
        apiType,
        "TryCancelXpGainClaim",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(float)
        });

    _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod = AccessTools.Method(
        apiType,
        "TrySetBuiltInEventPresentationClaim",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(bool)
        });

    _grailFloatingTextTrySetBuiltInEventClaimMethod = AccessTools.Method(
        apiType,
        "TrySetBuiltInEventClaim",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(bool)
        });

    _grailFloatingTextTryShowCompatibilityNoticeMethod = AccessTools.Method(
        apiType,
        "TryShowCompatibilityNotice",
        new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string)
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

    return _grailFloatingTextSupportsFeatureMethod != null ||
        _grailFloatingTextTryClaimConsolidatedXpGainMethod != null ||
        _grailFloatingTextTryClaimXpGainMethod != null ||
        _grailFloatingTextTrySetBuiltInEventPresentationClaimMethod != null ||
        _grailFloatingTextTrySetBuiltInEventClaimMethod != null ||
        _grailFloatingTextTryShowCompatibilityNoticeMethod != null ||
        _grailFloatingTextTryShowDeferredEventMethod != null ||
        _grailFloatingTextTryShowEventWithIconMethod != null ||
        _grailFloatingTextTryShowWithIconMethod != null ||
        _grailFloatingTextTryShowMethod != null;
}
```

Use these wrappers for feature checks, compatibility notices, and the two most common general notification calls. They resolve lazily, return `false` when GFT or the exact overload is unavailable, and never make GFT a hard dependency:

```csharp
private bool SupportsGrailFloatingTextFeature(string feature)
{
    if (string.IsNullOrWhiteSpace(feature)
        || (_grailFloatingTextSupportsFeatureMethod == null && !TryResolveGrailFloatingText()))
    {
        return false;
    }
    if (_grailFloatingTextSupportsFeatureMethod == null)
    {
        return false;
    }

    try
    {
        object result = _grailFloatingTextSupportsFeatureMethod.Invoke(
            null,
            new object[] { feature });
        return result is bool && (bool)result;
    }
    catch
    {
        return false;
    }
}

private bool InvokeTryShowDeferredEvent(
    string sourceId,
    string eventId,
    string text,
    string style,
    string category,
    string priority,
    string collapseKey,
    string iconId,
    string durationBucket,
    string deliveryPoint,
    float fadeSeconds,
    float opacity)
{
    if (_grailFloatingTextTryShowDeferredEventMethod == null
        && !TryResolveGrailFloatingText())
    {
        return false;
    }
    if (_grailFloatingTextTryShowDeferredEventMethod == null)
    {
        return false;
    }

    try
    {
        object result = _grailFloatingTextTryShowDeferredEventMethod.Invoke(
            null,
            new object[]
            {
                sourceId,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                durationBucket,
                deliveryPoint,
                fadeSeconds,
                opacity
            });
        return result is bool && (bool)result;
    }
    catch
    {
        return false;
    }
}

private bool InvokeTryShowCompatibilityNotice(
    string sourceId,
    string conflictId,
    string text,
    string diagnosticDetails)
{
    if (_grailFloatingTextTryShowCompatibilityNoticeMethod == null
        && !TryResolveGrailFloatingText())
    {
        return false;
    }
    if (_grailFloatingTextTryShowCompatibilityNoticeMethod == null)
    {
        return false;
    }

    try
    {
        object result = _grailFloatingTextTryShowCompatibilityNoticeMethod.Invoke(
            null,
            new object[]
            {
                sourceId,
                conflictId,
                text,
                diagnosticDetails
            });
        return result is bool && (bool)result;
    }
    catch
    {
        return false;
    }
}

private bool InvokeTryShowEvent(
    string sourceId,
    string eventId,
    string text,
    string style,
    string category,
    string priority,
    string collapseKey,
    string iconId,
    string durationBucket,
    float fadeSeconds,
    float opacity)
{
    if (_grailFloatingTextTryShowEventWithIconMethod == null
        && !TryResolveGrailFloatingText())
    {
        return false;
    }
    if (_grailFloatingTextTryShowEventWithIconMethod == null)
    {
        return false;
    }

    try
    {
        object result = _grailFloatingTextTryShowEventWithIconMethod.Invoke(
            null,
            new object[]
            {
                sourceId,
                eventId,
                text,
                style,
                category,
                priority,
                collapseKey,
                iconId,
                durationBucket,
                fadeSeconds,
                opacity
            });
        return result is bool && (bool)result;
    }
    catch
    {
        return false;
    }
}
```

For optional versioned behavior, probe the named capability (for example, `CompatibilityNotices`, `OnMainMenuDelivery`, or `BuiltInEventPresentationClaims`) and still verify the exact reflected method before invoking it. A missing feature, missing method, exception, or `false` result should leave your mod on its normal safe path.

QuickWheelPanelApi v15 exposes `GrailFloatingText.QuickWheelPanelApi` for a persistent
right-anchored two-column surface. Call `TrySet` while your quick-wheel owner is
active, `SetTooltipActive` when its normal item tooltip opens or closes, and
`Clear` when the wheel closes. The caller owns the rows and limits; GFT owns
font resolution, configured style colors, built-in icons, layout, and fading.

```text
TrySet(sourceId, leftTitle, leftSubtitle, leftTexts, leftIconIds, leftStyles,
       leftResourceTexts, leftResourceIconIds, leftResourceStyles,
       leftSummaryRowCount,
       rightTitle, rightSubtitle, rightTexts, rightIconIds, rightStyles,
       opacity, tooltipOpacity, fadeSeconds, rightOffset, topOffset, scale,
       panelColumnWidth, columnGap,
       panelBackgroundOpacity, panelBackgroundPadding,
       textShadowEnabled, textShadowOpacity, textShadowOffset, textShadowSoftness,
       textShadowStrength,
       textOutlineEnabled, textOutlineColor, textOutlineOpacity, textOutlineWidth,
       textOutlineStrength, whiteTextOutlineStrengthMultiplier,
       headerColor, subheaderColor)
SetTooltipActive(sourceId, active)
Clear(sourceId)
```

`panelColumnWidth` accepts 160 through 400 reference pixels. `columnGap` accepts
0 through 200 reference pixels and controls the space between the two columns.
`leftSummaryRowCount` declares how many leading entries in `leftTexts` belong to
the character summary. When ordinary rows follow, GFT adds a fixed six-reference-
pixel break before the first one. The optional resource strip remains part of the
summary between its first and later text rows.
`panelBackgroundOpacity` accepts 0 through 1; zero disables the two charcoal
column backplates. `panelBackgroundPadding` accepts 0 through 32 reference pixels.
GFT sizes one simple quad to each column's content and keeps both behind the text
and icons. Each quad receives a separately seeded small procedural texture with
multi-scale mottling, fine fibers, a transparent outer gutter, and softly
irregular feathered edges. Their procedural variation is encoded into a black
alpha mask, so black scenes remain black instead of revealing a lifted gray fill.

`textOutlineWidth` and `textShadowOffset` accept 0 through 16,
`textShadowSoftness` accepts 0 through 1, and both strength values accept 1
through 8. An offset of 0 centers the underlay behind the glyphs so softness and
opacity form a blurred-looking text backing; higher offsets produce a conventional
drop shadow. Supported TextMesh Pro SDF fonts render the outline and underlay
from one cached material-backed glyph mesh. Width and offset are approximate
visual ranges because each font atlas has its own SDF gradient scale. Unsupported
materials use a bounded four-direction outline plus one shadow copy rather than
layered geometry.
`whiteTextOutlineStrengthMultiplier` accepts 0.5 through 2.0 and applies only to
text resolved through the semantic `White` style. Icons, explicit `Pale`, gray,
and other styles retain the caller's configured strength.

API v7 deferred event call shape:

```csharp
TryShowEvent(sourceId, eventId, text, style, category, priority, collapseKey, iconId, durationBucket, deliveryPoint, fadeSeconds, opacity)
```

Delivery points:

- `Immediate`: queue the message now.
- `OnMainMenu`: wait until the interactive title view is active, loading and camera transitions have ended, and the game window is focused.
- `OnLoad`: wait until the hero and world are initialized, loading and camera transitions have ended, no fullscreen video or cutscene covers the view, and the game window is focused.

Deferred messages persist until shown. Their duration starts on the first eligible IMGUI repaint, not when the API call is made.

NotificationApi v13 compatibility notice:

```csharp
TryShowCompatibilityNotice(sourceId, conflictId, text, diagnosticDetails)
```

Use this after your mod has verified a real incompatibility. GFT applies the shared Warning style, System category and duration, High priority, warning icon, stable collapse key, and `OnMainMenu` delivery. `sourceId` should be your plugin GUID, and `conflictId` should be a stable identifier scoped to your mod, such as `incompatible-other-mod`. Keep `text` concise and player-facing. Put the exact detected GUID, DLL, setting, or other evidence in `diagnosticDetails`; GFT writes it to `BepInEx/LogOutput.log`. The call respects `NotifyModCompatibility`, category controls, and per-source controls, and it never disables or changes either mod.

If BepInEx rejects a plugin before its code can run because of a declared hard incompatibility, that plugin cannot call the API. GFT separately translates BepInEx's verified incompatibility dependency errors into the same main-menu notice format, resolves loaded conflicting GUIDs to their plugin names when possible, and tells the player to remove or disable one mod before restarting.

API v8 consolidated XP claim call shape:

```csharp
TryClaimConsolidatedXpGain(sourceId, eventId, consolidationKey, textFormat, style, category, priority, iconId, durationBucket, expectedAmount, fadeSeconds, opacity)
```

Call it immediately before changing the hero XP stat. GFT batches only claims with the same `sourceId`, `consolidationKey`, and presentation, then replaces `{xp}` or `{amount}` in `textFormat` with the summed amount. Use distinct keys for meanings that must remain separate. If API v8 is unavailable, fall back to the API v6 one-shot claim.

NotificationApi v10 XP claim cancellation:

```csharp
TryCancelXpGainClaim(sourceId, eventId, expectedAmount)
```

If the XP mutation fails after reserving an API v6 or v8 claim, call this immediately with the same source, event, and expected amount. GFT removes the newest matching unconsumed claim so it cannot later describe a reward that was not granted.

NotificationApi v12 built-in event presentation claims:

```csharp
TrySetBuiltInEventPresentationClaim(
    sourceId,
    eventId,
    presentationEventId,
    style,
    iconId,
    active)
```

Set `active` to `true` immediately before an exact synchronous built-in event and release it from a `finally` block or Harmony finalizer. While active, the newest source-scoped presentation claim changes that event's event ID, style, and icon without bypassing its enablement, minimum amount, text format, duration, or consolidation settings. Healing batches with different event IDs, styles, or icons remain separate. Suppression through `TrySetBuiltInEventClaim` takes priority when both claim types are active. The Blood Magic healing integration claims `default-healed` as `blood-magic-healed` with the `Red` style and `magic_blood` icon.

NotificationApi v11 built-in event claims:

```csharp
TrySetBuiltInEventClaim(sourceId, eventId, active)
```

Set `active` to `true` while your integration provides a replacement for a GFT built-in event, then set it to `false` when that replacement becomes inactive or your plugin shuts down. GFT suppresses its own presentation while any source claims the event but continues tracking the underlying game state. The Wyrdnight transition event ID is `vanilla-wyrd-night` and covers both nightfall and dawn. The healing event ID is `default-healed`; claim it only around the exact synchronous health mutation your integration replaces or intentionally keeps quiet, and release it from a `finally` block or Harmony finalizer. Claims do not change user configuration.

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
- `style`: `Default`, `Reward`, `Status`, `Wyrd`, `Discovery`, `Combat`, `Rest`, `System`, `Warning`, `Error`, `Critical`, a configured color-group name, or a hex color. Configured group names take priority over HTML named colors, so values such as `Purple`, `Pink`, `Red`, `Blue`, and `Cyan` use their matching settings.
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
- Game events: `rest`, `location`, `parry`, `crime`, `pickpocket`, `weight`, `experience`, `healing`, `corpse_meager`, `corpse_worthy`, `corpse_potent`, `corpse_prime`, `summon`, `necro`.
- Currency: `gold_earned_very_low`, `gold_earned_low`, `gold_earned_medium`, `gold_earned_high`, `gold_earned_very_high`.
- Skills: `one_handed`, `two_handed`, `archery`, `shield`, `unarmed`, `magic`.
- Specific weapons: `one_handed_sword`, `one_handed_axe`, `one_handed_blunt`, `one_handed_dagger`, `one_handed_spear`, `two_handed_sword`, `two_handed_axe`, `two_handed_blunt`, `two_handed_spear`. `one_handed_polearm` and `two_handed_polearm` are accepted aliases for the matching spear icons. Sickle integrations use `one_handed_axe`.
- Magic types: `magic_blood`, `magic_fire`, `magic_cold`, `magic_poison`, `magic_electric`, `magic_pure`, `magic_wet`. Wyrdness uses `wyrd`; Other and unknown magic use `magic`.

Feature probes:

- `ApiVersion13` and `CompatibilityNotices`: provider supports standardized main-menu compatibility warnings through `TryShowCompatibilityNotice` and automatically surfaces verified BepInEx hard-incompatibility rejections.
- `ApiVersion12` and `BuiltInEventPresentationClaims`: provider supports source-scoped restyling of exact built-in events through `TrySetBuiltInEventPresentationClaim`.
- `ApiVersion11` and `BuiltInEventClaims`: provider supports source-isolated ownership of built-in event presentation through `TrySetBuiltInEventClaim`.
- `ApiVersion10` and `XpClaimCancellation`: provider supports canceling an unconsumed XP claim through `TryCancelXpGainClaim`.
- `quick-wheel-panels-v1`: provider supports persistent two-column quick-wheel panels through `QuickWheelPanelApi`.
- `ApiVersion9`: provider includes the NotificationApi surface released alongside the persistent quick-wheel integration.
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
- `HealingNotifications`: provider can show and consolidate eligible hero health gains, with periodic healing controlled separately.
- `VanillaWyrdEvents`: provider can show built-in vanilla Wyrd notifications.

You can also query the installed provider:

```csharp
string[] ids = NotificationApi.GetBuiltInIconIds();
```

Examples:

```csharp
// Show a startup message only after loaded gameplay is fully visible.
InvokeTryShowDeferredEvent(PluginGuid, "startup-ready", text, "System", "System", "High", "", "system", "System", "OnLoad", 0.25f, 0.9f);

// Show a confirmed compatibility warning once the main menu can be used.
InvokeTryShowCompatibilityNotice(PluginGuid, "incompatible-other-mod", text, diagnosticDetails);

// Stack killing-blow reward messages with a skill icon. The default RedEvents config colors this event red.
InvokeTryShowEvent(PluginGuid, "killing-blow", text, "Reward", "Reward", "Normal", "", "archery", "Medium", 0.25f, 0.9f);

// Update one status line in place with the Wyrd icon.
InvokeTryShowEvent(PluginGuid, "wyrd-hunt-status", text, "Wyrd", "Status", "Normal", "wyrd-hunt-status", "wyrd", "Medium", 0.25f, 0.9f);
```
