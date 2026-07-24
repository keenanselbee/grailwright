More Weapon Loadouts Addon
========================

Version: 0.1.3

Original mod: More Weapon Loadouts 1.5.0 from Owrocc HUD Tweaks file 60.2

Short description: A small companion addon for More Weapon Loadouts that improves slot 1 reliability after loading and routes vanilla loadout keys into virtual slots.

Small companion addon for Owrocc More Weapon Loadouts.

What it changes
---------------

Some save archives can contain duplicate VirtualLoadouts.json entries. In the
observed broken save, the first entry was empty while a later entry contained
the real slot 1 data. If the game returns the stale empty entry, More Weapon
Loadouts can think slot 1 is unbound after loading and fall back to fists.

This plugin patches VirtualLoadouts.json reads so duplicate save entries prefer
the useful/latest entry.

It also redirects vanilla gameplay weapon loadout keys to More Weapon Loadouts
virtual slots by default:

  vanilla 1 -> MWS slot 1
  vanilla 2 -> MWS slot 2
  vanilla 3 -> MWS slot 3
  vanilla 4 -> MWS slot 4

Version 0.1.2 coordinates this redirect with More Weapon Loadouts' own slot
shortcuts. If both mods see the same key press, this addon still
blocks the vanilla action but lets More Weapon Loadouts activate the slot once.
This prevents a second same-slot activation from immediately hiding the weapon
or spell that was just raised. Controller and rebound vanilla inputs still use
the addon redirect when no matching MWS shortcut is down.

Configuration
-------------

Generated at:

  BepInEx\config\ks.tgfoa.more-weapon-loadouts-addon.cfg

Version 0.1.3 uses ConfigSchemaVersion 1. Older configs are backed up and a
fresh config is generated once so defaults apply cleanly.

Defaults:

  FixDuplicateVirtualLoadoutEntries = true
  RedirectVanillaWeaponLoadoutKeys = true
  ReprimeAutoTrackAfterLoad = true
  ReapplyCurrentVirtualSlotAfterLoad = false

Only enable ReapplyCurrentVirtualSlotAfterLoad if the first slot still comes
back visually wrong after loading. It actively re-applies the saved current
virtual slot after a short delay.

Persistence note
----------------

With More Weapon Loadouts' default persistence settings, virtual loadouts are
kept in memory while playing and written into VirtualLoadouts.json when the
game saves. Binding or auto-tracking a set, then quitting before a game save,
can lose that latest virtual-loadout change.

Version safety
--------------

This addon touches specific More Weapon Loadouts internals and was built
against More Weapon Loadouts 1.5.0. Later parent mod updates may make this
addon unnecessary or incompatible. If Owrocc updates More Weapon Loadouts,
check the parent mod changelog and disable this addon if the issue is fixed
upstream or the loadout behavior changes.

Mod author note
---------------

Owrocc is welcome to incorporate this behavior upstream if desired. This
companion addon exists to solve a local loadout issue quickly and is not intended to
replace the original mod.
