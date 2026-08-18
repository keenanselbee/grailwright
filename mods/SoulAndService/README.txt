Soul and Service - Summon Overhaul 0.3.5
================================================

Soul and Service makes hero summons responsive, close-following servants while
preserving the game's own combat identity. It also gives Soul Salvage a focused
necromantic role.

Default behavior
----------------

- Summon AI updates every 0.25 seconds and recovers 0.10 seconds after spawning.
- Idle summons trot at 4 m, run at 8 m, and use the native safe teleport at 35 m.
- A 1.25x catch-up speed applies only out of combat.
- Uncommitted summons can adopt a hostile target under the hero's crosshair.
- "Summon Pass-Through" prevents owned summons from pushing or trapping the hero.
- Confirmed hero magic projectiles and magic-gauntlet contacts pass through owned
  summons; arrows and bespoke scripted ray spells retain vanilla collision.
- Summons remain through rest, with no default increase to the summon limit.
- Replacement summons recover missing Invocation of Might scaling only when the
  outgoing summon proves that the native effect is active.
- Summon idle loops play at 60% volume; combat, hurt, and death sounds are untouched.
- Soul Salvage light cast returns 50% of invested essence at full summon health,
  split evenly between health and mana by default.
- Soul Salvage heavy cast raises an ordinary runtime-spawned hostile corpse at 50%
  health for 120 seconds. Authored scene NPCs, bosses, minibosses, friendly corpses,
  and unresolved templates are rejected. The original corpse is hidden and restored
  when the servant ends.
- PermanentReanimations is OFF by default. Enabling it removes the duration for
  the current play session, but still creates an unsaved copy rather than reviving
  an original quest NPC.

Configuration
-------------

Config file: BepInEx/config/ks.tgfoa.soul-and-service.cfg

All timings, distances, target range, pass-through behavior, summon-limit bonus,
idle volume, Soul Salvage return mode, essence percentage, servant health, servant
duration, and permanent reanimations can be configured. Settings are also visible
in FoA Mod Manager. The final Import Previous Settings tab safely imports compatible
customized values after a future config reset.

Compatibility
-------------

This mod intentionally replaces Avalon Summons, Better Summon, and the temporary
Summon Pass-Through test plugin. Remove or disable those plugins before loading.

Steel and Bone is compatible. Soul and Service does not add a late flat damage
multiplier, so summon attacks continue through the game's normal damage types and
Steel and Bone's material rules.

Raised servants are runtime copies, grant no XP or scripted death reward, and are
not saved. Heavy cast accepts only runtime-spawned ordinary hostiles, excluding the
authored scene locations used by persistent NPCs. The feature should still be
treated as an initial-release feature and tested on expendable generic enemies
before long play sessions.

Troubleshooting
---------------

Enable the Diagnostics setting in the Diagnostics section and inspect the newest
BepInEx LogOutput.log.
Do not install this DLL alongside Avalon Summons, Better Summon, or the temporary
pass-through test DLL.
