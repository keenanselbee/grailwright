# Grail Floating Text API

Use Grail Floating Text as an optional BepInEx dependency:

```csharp
[BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
```

Resolve the API by reflection so your mod still loads when the provider is not installed:

```csharp
private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";

private MethodInfo _grailFloatingTextTryClaimXpGainMethod;
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

    return _grailFloatingTextTryClaimXpGainMethod != null ||
        _grailFloatingTextTryShowEventWithIconMethod != null ||
        _grailFloatingTextTryShowWithIconMethod != null ||
        _grailFloatingTextTryShowMethod != null;
}
```

API v6 XP claim call shape:

```csharp
TryClaimXpGain(sourceId, eventId, text, style, category, priority, iconId, durationBucket, expectedAmount, fadeSeconds, opacity)
```

Use XP claims immediately before your mod changes the hero XP stat. The next matching XP gain uses the claimed text/event/style/icon instead of the generic XP event, and the claim is consumed without merging multiple XP gains.

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
- `style`: `Default`, `Reward`, `Status`, `Wyrd`, `Discovery`, `Combat`, `Rest`, `System`, `Warning`, `Error`, `Critical`, or a hex color.
- `category`: `General`, `Combat`, `Reward`, `Status`, `System`, or `Debug`.
- `priority`: `Low`, `Normal`, `High`, or `Critical`.
- `collapseKey`: leave blank for stacking event messages; set a stable key for status messages that should update in place.
- `iconId`: blank or `Auto` uses the default icon for the style/category. Use `None` or `Off` to suppress the icon for one message.
- `durationBucket`: `VeryShort`, `Short`, `Medium`, `Long`, or `VeryLong`; blank or unknown values use `Medium`.
- `text`: the exact XP text to show, such as `+42 XP (Worthy)`.
- `expectedAmount`: the XP amount your mod is about to add. Grail Floating Text matches exact amounts first, then very recent claims so game XP multipliers can still display the final adjusted amount.

Built-in icon IDs:

- Core: `general`, `system`, `status`, `wyrd`, `reward`, `combat`, `warning`, `critical`, `debug`.
- Game events: `rest`, `location`, `crime`, `pickpocket`, `weight`, `experience`, `corpse`.
- Skills: `one_handed`, `two_handed`, `archery`, `shield`, `unarmed`, `magic`.

Feature probes:

- `ApiVersion6`: provider supports text-aware XP gain claims through `TryClaimXpGain`.
- `ApiVersion4`: provider supports event IDs and duration buckets through `TryShowEvent`.
- `XpGainClaims`: provider can claim, style, and name the next XP gain.
- `XpNotifications`: provider can take over vanilla XP gain display.
- `EventIds`: provider accepts stable event IDs for configurable color-group routing.
- `DurationBuckets`: provider accepts named duration buckets.
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
// Stack killing-blow reward messages with a skill icon. The default RedEvents config colors this event red.
InvokeTryShowEvent(PluginGuid, "killing-blow", text, "Reward", "Reward", "Normal", "", "archery", "Medium", 0.25f, 0.9f);

// Update one status line in place with the Wyrd icon.
InvokeTryShowEvent(PluginGuid, "wyrd-hunt-status", text, "Wyrd", "Status", "Normal", "wyrd-hunt-status", "wyrd", "Medium", 0.25f, 0.9f);
```
