No Player Light
===============

Version 1.3.4

No Player Light is a standalone BepInEx 5 Mono plugin for Tainted Grail: The
Fall of Avalon.

Short Description
-----------------

Removes the hidden player-centered HeroLight so caves, nights, interiors,
torches, lanterns, ReShade presets, and lighting mods can carry the scene
without a constant artificial glow around the player.

Behavior
--------

The plugin disables the Unity GameObject named HeroLight when one of its
bounded scans finds it.

It scans on startup, scans again after scene loads, and performs short delayed
retries after scene load because the game can create objects after the
scene-loaded event.

After the scene-load retry window finishes, this version stops scanning. It
does not keep a per-frame check, timed fallback poll, config watcher, or
diagnostic object/light scan running in the background.

If the game re-enables or recreates HeroLight later in the same scene after the
retry window has ended, this version may not catch it until the next scene
load.

It does not alter world lights, torches, lanterns, bonfires, shadows, weather,
or post-processing.

Install Shape
-------------

Vortex mod folder payload:

  NoPlayerLight\NoPlayerLight.dll

When installed as a BepInEx plugin mod in Vortex, this payload is placed under:

  BepInEx\plugins\NoPlayerLight\NoPlayerLight.dll

Plugin GUID:

  ks.tgfoa.no-player-light

Configuration
-------------

Version 1.3.4 has no config options. It disables HeroLight when one of its
bounded scans finds it.

Older BepInEx config files from previous versions are ignored by this version
and can be deleted if you do not need them.

More Control
------------

No Player Light is intentionally minimal. If you want more lighting controls,
use Player Inner Light Control:

  https://www.nexusmods.com/taintedgrailthefallofavalon/mods/229
