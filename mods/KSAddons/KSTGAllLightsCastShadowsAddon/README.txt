TG All Lights Cast Shadows Addon
==============================

Version: 1.1.8

Original mod: TG All Lights Cast Shadows 1.2.0

Short description: A companion addon for TG All Lights Cast Shadows that restores shadow state cleanly and keeps bonfire lighting from being over-shadowed.

This is a small companion plugin for TG All Lights Cast Shadows. It fixes
the F7 toggle leaving Unity's global shadow quality forced to All after the
nearby light upgrades have been disabled, and it prevents selected bonfire
or campfire lights from being upgraded into shadow-casting lights.

Requirements
------------

  Tainted Grail: The Fall of Avalon Mono branch
  BepInEx 5 Mono
  TG All Lights Cast Shadows 1.2.0 or compatible

How it works
------------

Before the parent mod applies its light upgrades, this plugin records the
current Unity global shadow-quality value. After F7 restores the tracked
lights, this plugin restores that global value too.

The plugin also shields configured bonfire/campfire light objects before the
parent scan. This keeps the parent mod from forcing shadows onto fire lights
that were authored to illuminate nearby stones or firepit geometry without
being occluded by those same meshes.

This plugin adds no per-frame scanning. It only runs around the parent mod's
own light scan and does not change the parent mod's distance, budget, shadow
mode, or strength settings.

Configuration
-------------

Start the game once to generate:

  BepInEx\config\ks.tgfoa.tg-all-lights-cast-shadows-addon.cfg

Version 1.1.8 uses ConfigSchemaVersion 2. Older configs are backed up and a
fresh config is generated once so defaults apply cleanly. Built-in bonfire and
campfire exclusions are now code-owned. Add custom names to
AdditionalExcludedLightPathFragments; that manual list is preserved by exact
current setting name across future schema resets.

Default excluded light path fragments:

  WyrdNight_Repeller_Bonfire,Repeller_Bonfire,Bonfire,Campfire

Set VerboseExclusionLogging to true only while diagnosing a missed light. It
logs each excluded light path once per scene so exact runtime names can be
added to AdditionalExcludedLightPathFragments.

Installation
------------

Install the included folder at:

  BepInEx\plugins\TGAllLightsCastShadowsAddon

Keep TGAllLightsCastShadows installed. This plugin declares it as a hard
dependency and will not run without it.

The plugin logs the captured and restored global shadow-quality values in
BepInEx\LogOutput.log. Remove this companion if a future version of the
parent light mod restores QualitySettings.shadows and provides its own
per-light exclusion support.

Version safety
--------------

This addon touches specific TG All Lights Cast Shadows internals and was
built against TG All Lights Cast Shadows 1.2.0. Later parent mod updates may
make this addon unnecessary or incompatible. If TG All Lights Cast Shadows
updates, check its changelog and disable this addon if the same behavior is
fixed upstream or the light scan internals change.

Mod author note
---------------

The TG All Lights Cast Shadows author is welcome to incorporate this behavior
upstream if desired. This companion addon exists to solve local lighting issues
quickly and is not intended to replace the original mod.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
