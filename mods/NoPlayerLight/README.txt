No Player Light
===============

Version 1.2.1

No Player Light is a standalone BepInEx 5 Mono plugin for Tainted Grail: The
Fall of Avalon.

Short Description
-----------------

Removes the hidden player-centered HeroLight so caves, nights, interiors,
torches, lanterns, ReShade presets, and lighting mods can carry the scene
without a constant artificial glow around the player.

Behavior
--------

By default, the plugin disables the Unity GameObject named HeroLight when it is
found.

It scans on startup, scans again after scene loads, and performs short delayed
retries after scene load because the game can create objects after the
scene-loaded event.

Once HeroLight is found, the reference is cached so it can be disabled again
quickly if the same object is reactivated. A slow fallback lookup runs every
8 seconds in case the game creates a replacement HeroLight later.

Set DisableHeroLight to false to leave HeroLight enabled. If the plugin already
disabled the cached HeroLight object and the setting is changed through a live
BepInEx config editor, the plugin re-enables that cached object.

It does not alter world lights, torches, lanterns, bonfires, shadows, weather,
or post-processing.

Diagnostics
-----------

Runtime scan logging is enabled by default and writes to
BepInEx\LogOutput.log.

The diagnostics log:

  - exact GameObject.Find("HeroLight") matches before they are disabled
  - active GameObjects whose names contain HeroLight, Light_HeroLight, or
    Spotlight_Hero
  - active Light components whose names or hierarchy paths contain those same
    fragments, including enabled state, type, intensity, range, shadows, layer,
    hierarchy path, and component list
  - inactive child objects and child Light components under the exact
    HeroLight object before it is disabled

Repeated matches are logged once per scene so the log stays readable. If no
matches are active, the plugin logs that too, with a 60-second cooldown. If an
exact HeroLight object is found, the plugin suppresses the later no-match line
for that same scan because the object was intentionally disabled first.

Install Shape
-------------

Vortex mod folder payload:

  NoPlayerLight\NoPlayerLight.dll

When installed as a BepInEx plugin mod in Vortex, this payload is placed under:

  BepInEx\plugins\NoPlayerLight\NoPlayerLight.dll

Plugin GUID:

  ks.tgfoa.no-player-light

If upgrading from KS No Player Light, disable or remove the old
KSNoPlayerLight package before enabling this one. They use the same BepInEx
plugin GUID.

Configuration
-------------

Version 1.2.1 uses ConfigSchemaVersion 3. Older configs are backed up and a
fresh config is generated once so defaults apply cleanly.

BepInEx creates the config file here after the plugin has loaded once:

  BepInEx\config\ks.tgfoa.no-player-light.cfg

HeroLight behavior is controlled by:

  [1. Core]
  ConfigSchemaVersion = 3
  DisableHeroLight = true

Set DisableHeroLight to false to enable the player HeroLight.

Diagnostics are controlled by:

  [Diagnostics]
  EnableRuntimeScan = true

Set EnableRuntimeScan to false to stop the runtime diagnostic object/light
logging.
