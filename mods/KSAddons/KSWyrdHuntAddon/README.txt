Wyrd Hunt Addon
===================

Version: 1.4.2

Companion addon for Wyrd Hunt 0.4.65.

What it does
------------

- Defaults to Grail Floating Text Wyrd Scent status text with the shared Wyrd icon when Wyrd Hunt status changes.
- If Grail Floating Text is installed, the parent Wyrd Scent meter is hidden by default.
- Can optionally keep the parent Wyrd Scent meter and hide it only when safe or during loading/portal transitions.
- Keeps the Wyrd Scent meter position configurable with offsets.
- Adds configurable weighted random native hunt spawns so the active hunt target matches the monster that actually spawns.
- Adds optional mixed hunt packs: rare extra same-or-lower-tier pack mates spawned beside the primary hunt target.
- Favors weaker sidecars, heavily reduces same-tier and same-family sidecars, and can spawn 1-4 Wyrdspirits when Wyrdspirit is selected as a sidecar.
- Adds one-shot Wyrd Hunt tuning presets for rarer, more focused hunts.
- Guards optional hunt randomizer hooks so HUD notifications and meter hiding still start if an optional Wyrd Hunt method changes.

Config file: ks.tgfoa.wyrd-hunt-addon.cfg

Version 1.4.2 uses ConfigSchemaVersion 5. Older configs are backed up and a
fresh config is generated once so defaults apply cleanly.

Preset behavior:

Hunt Tuning Preset defaults to Custom. Selecting Default, Sparse, Stalker, or
CursedNight immediately writes Wyrd Hunt's own hunt/threat settings in
kane.tgfoa.wyrd-hunt.cfg, saves them, records Last Applied Hunt Tuning Preset,
and resets the selector to Custom. Presets do not lock settings; edit Wyrd
Hunt's individual settings afterward as usual.

Default restores Wyrd Hunt's original tuning. Sparse is rare and calmer.
Stalker is the recommended rare-but-tense hunt profile. CursedNight is the
danger-forward option while still using scene/session caps.

Default HUD settings:

Hunt Tuning Preset = Custom
Scent Meter Mode = NotificationsOnly
Notifications Enabled = true
Notification Text Format = Wyrd Scent: {stage}
Show Scent Number = false

Default floating text examples:

Wyrd Scent: Hunted
Safe from Wyrdness
Exposed to Wyrdness

Requires BepInEx 5 Mono and Wyrd Hunt 0.4.65. Grail Floating Text is an optional
soft dependency for notification text:
https://www.nexusmods.com/taintedgrailthefallofavalon/mods/247

If Grail Floating Text is installed, the parent Wyrd Scent meter is hidden by
default.
