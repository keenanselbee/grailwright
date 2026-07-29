First Hit Hitch Fix
Version 0.1.2

First Hit Hitch Fix is a standalone BepInEx plugin for Tainted Grail: The Fall
of Avalon. It tries to reduce the large hitch that can happen the first time a
combat hit effect is used in a session.

What it does
------------

The mod warms combat VFX addressables by constructing the game's own disabled
AddressablesPooledInstance objects. This loads and instantiates the effect
without visibly spawning particles in the world.

By default it warms:

- default item-on-surface combat VFX from GameConstants
- default critical, backstab, and death VFX
- NPC-specific hit, critical, backstab, and death VFX as NPCs initialize
- additional combat VFX discovered through the live VFX spawn path

Config path
-----------

BepInEx/config/ks.tgfoa.first-hit-hitch-fix.cfg

Important settings
------------------

Enabled = true
Master switch.

ConfigSchemaVersion = 1
Configuration layout version. Do not edit this manually. If the schema changes
in a future release, First Hit Hitch Fix backs up the old config beside the
active .cfg file, clears stale settings, reloads the config, and saves
regenerated defaults.

WarmDefaultCombatVFX = true
Warms the game's default combat VFX container after gameplay services are ready
and again after scene loads.

WarmNpcCombatVFX = true
Warms NPC-specific hit, critical, backstab, and death VFX when NPCs initialize.

WarmDiscoveredCombatVFX = true
Warms combat VFX references discovered through the normal VFX spawn path.

HoldWarmInstances = true
Keeps disabled warm instances resident. This gives the best chance of avoiding
repeat first-use loading, but uses some extra memory.

MaxWarmInstances = 64
Maximum disabled warm instances the mod will keep. Older entries are released
when the cap is reached.

WarmupSpacingSeconds = 0.1
Delay between queued warmups. Increase this if the warmup itself is too spiky.

Diagnostics = false
Enables diagnostic logging.

LogWarmups = true
When Diagnostics is enabled, logs queued, started, completed, failed, and
released warmups. It also logs settled summaries before shutdown when warmup
activity has gone quiet.

LogDiscoveredVFX = false
When Diagnostics is enabled, logs combat VFX references discovered from live
VFX spawns, including whether each key was queued, started, completed, released,
failed, or still cold when combat requested it.

Testing notes
-------------

For a first test, leave the defaults enabled and fight a few different enemy
types after loading into gameplay. If the hitch improves but does not disappear,
enable Diagnostics and LogDiscoveredVFX. Warmed live effects should report a
completed or otherwise covered warmStatus. Any cold live effect gets a warning
and is queued late for later reuse.

Compatibility
-------------

First Hit Hitch Fix patches combat VFX discovery and NPC initialization. It
does not replace damage, audio, AI, or hit calculation logic. It may overlap
with mods that heavily replace VFXManager or NPC initialization.

Build notes
-----------

Use the repository-level tools/Build-Mod.ps1 script to compile and export the
package. Release zips contain only the runtime payload, README, and changelog.
