KS Better Volumetric Fog Addon
===============================

Version: 0.1.3
Platforms: Windows and Linux through Proton.

Original mod: Better Volumetric Fog 1.0.2-mono, which loads in BepInEx as
plugin version 1.0.0.

Short description: Removes the parent's recurring all-resources fog search and
uses its cleaner volumetrics only where they matter most by default.

This companion addon keeps Better Volumetric Fog's visual improvement focused
on interiors. It applies the parent's Low quality there and restores the
game-authored volumetric settings outdoors, during loading, and outside active
gameplay.

The addon also replaces the parent's repeated global search for every loaded
HDRP Fog object with a cache. The cache is seeded once and updated when HDRP
registers volumes, reloads stacks, adds fog components, or changes default
profiles. The parent's own backup, restoration, hotkey, JSON reload, and
quality-application behavior remain responsible for the actual fog settings.

Requirements
------------

  Tainted Grail: The Fall of Avalon Mono branch
  BepInEx 5 Mono
  Better Volumetric Fog 1.0.2-mono or compatible

Default behavior
----------------

  Better Volumetric Fog is applied only in verified interiors.
  Interior quality is Low: 30 percent screen resolution and 32 slices.
  Exteriors, loading screens, and uncertain contexts use vanilla volumetrics.
  Fog discovery uses the event-fed cache instead of repeated global searches.
  The parent TGVolumetricFix.json file is never edited.

Low is intentionally the default. It retains the most useful cleanup around
interior lamps, candles, and window shafts while using far less of the 3D
volumetric buffer than Medium.

Configuration
-------------

Start the game once to generate:

  BepInEx\config\ks.tgfoa.tg-volumetric-fix-addon.cfg

Common entries:

  Enabled = true
  InteriorsOnly = true
  Quality = Low
  OptimizeFogDiscovery = true
  ShowToggleNotifications = true
  Diagnostics = false

FoA Mod Manager section order:

  General
  Visuals
  Performance
  Notifications
  Diagnostics
  Import Previous Settings

Set Quality to Medium, High, or Ultra only when the GPU has enough headroom.
Set InteriorsOnly to false to use the selected quality in all playable areas.

Turning Enabled off returns control to Better Volumetric Fog's own JSON and
global behavior. Turning OptimizeFogDiscovery off restores the parent's
original all-resources search while leaving the contextual behavior active.

Grail Floating Text notifications
---------------------------------

When Grail Floating Text is installed, toggling the parent mod shows one
System notification for each actual runtime state change. Enabled messages
identify the default interior-only scope. Set ShowToggleNotifications to false
to hide these confirmations. Interior and exterior transitions are never
announced.

Installation
------------

Install the included folder at:

  BepInEx\plugins\TGVolumetricFixAddon

Keep Better Volumetric Fog installed. This plugin declares it as a hard
dependency and will not run without it.

Compatibility and restoration
-----------------------------

The addon temporarily changes only the parent's in-memory Quality value during
an allowed application. It restores that value immediately and never edits
TGVolumetricFix.json.

The parent's exact backups restore the game-authored Fog values and override
states when leaving an interior, disabling the parent, or unloading the addon.

This addon validates the Better Volumetric Fog 1.0.2-mono runtime types and
method signatures because that file reports internal plugin version 1.0.0. If
the parent mod updates, check its changelog and disable this addon if the same
behavior is added upstream or its internals change.

Troubleshooting
---------------

If volumetrics still cost too much indoors, use the parent's hotkey to compare
the active effect with vanilla and check that Quality is Low in this addon's
config. Enable Diagnostics for environment, cache, application, and restoration
messages in the BepInEx log.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after
importing.
