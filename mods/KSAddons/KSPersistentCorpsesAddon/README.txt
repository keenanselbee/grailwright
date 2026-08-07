Persistent Corpses Addon
========================

Version: 1.0.8
Platforms: Windows and Linux through Proton.

Original mod: Persistent Corpses 1.0.0

Short description: Conceals restored corpses until their ragdolls settle so
they reappear lying down instead of visibly falling from a standing pose, and
cleans up loaded corpses after long bonfire rests.

Requirements
------------

  Tainted Grail: The Fall of Avalon Mono branch
  BepInEx 5 Mono
  Persistent Corpses 1.0.0 or compatible

How it works
------------

Tainted Grail saves a dead NPC's ground position, but not the final rotations
of its ragdoll bones. When a saved corpse is restored, the game creates the
body in its default standing pose and then enables ragdoll physics.

This addon conceals the restored corpse's standard and Kandra renderers while
leaving its rigidbodies and colliders active. Once the ragdoll has received
enough active physics time and stops moving, the addon reveals it in its new
resting pose. Bodies that keep moving on slopes are revealed when the maximum
settle time is reached.

The timing advances only during active physics updates. Loading screens and
paused gameplay therefore do not consume the settle window before the corpse
can fall.

This version does not add ragdoll bone data to saves. A restored body may settle
into a different pose than the one it had when the game was saved, but the
standing-to-falling sequence is concealed.

After a bonfire rest of at least three actual hours, the addon processes loaded
full corpses gradually. Empty corpses are removed. Corpses with visible loot use
the game's own lightweight replacement body so their items remain available.
Corpses the game cannot safely simplify remain untouched. Beds, scripted rests,
and interrupted rests shorter than the configured threshold do not trigger this
cleanup.

Renderer and rigidbody hierarchy scans stop as soon as each restored body is
fully initialized, reducing short-lived loading overhead without changing its
settle timing or physics.

Configuration
-------------

Start the game once to generate:

  BepInEx\config\ks.tgfoa.persistent-corpses-addon.cfg

Defaults:

  ConfigSchemaVersion = 1
  Enabled = true
  MinimumSettleSeconds = 0.75
  MaximumSettleSeconds = 2
  CleanupAfterLongBonfireRest = true
  MinimumRestHoursForCleanup = 3
  Diagnostics = false

MinimumSettleSeconds is the shortest active-physics window before a sleeping
ragdoll can be shown. MaximumSettleSeconds prevents a body on a slope or in an
unstable collision from remaining invisible indefinitely.

CleanupAfterLongBonfireRest enables loaded-corpse cleanup after a sufficiently
long fireplace rest. MinimumRestHoursForCleanup accepts 1 through 24 hours and
uses the actual completed rest duration.

Version 1.0.8 uses ConfigSchemaVersion 1. Older or unversioned configs are
backed up and regenerated with fresh defaults when the schema changes.

Installation
------------

Install the included folder at:

  BepInEx\plugins\PersistentCorpsesAddon

Keep Persistent Corpses installed. This addon declares it as a hard dependency
and will not run without it.

Compatibility
-------------

The addon patches the game's NpcDummy visual-restoration and fireplace-rest
paths. It does not edit Persistent Corpses, alter loot, disable colliders, or
write additional save data. Long-rest cleanup deliberately invokes the game's
native corpse replacement rules for loaded bodies only.

If a future game update changes NpcDummy restoration, the addon will log a
startup error and disable itself. Set Diagnostics to true to log each concealed
and revealed corpse while testing.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
