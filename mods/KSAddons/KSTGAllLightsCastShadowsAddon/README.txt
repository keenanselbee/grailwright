All Lights Cast Shadows Addon
=============================

Version: 1.3.0
Platforms: Windows and Linux through Proton.

Original mod: All Lights Cast Shadows 1.2.0

Short description: A companion addon for All Lights Cast Shadows that restores shadow state cleanly, protects bonfire lighting, and reduces HDRP shadow-atlas pressure.

This is a small companion plugin for All Lights Cast Shadows. It fixes
the parent toggle leaving Unity's global shadow quality forced to All after the
nearby light upgrades have been disabled. It also protects selected bonfire
and campfire lights and caps the shadow resolution of parent-promoted point
and spot lights to reduce atlas rescaling, blurry shadows, and flicker.

Combat-aware performance is enabled by default for outdoor fights. Its default
layer only lowers the promoted-light atlas cap from 256 to 128. After combat
has remained over for five seconds, the normal cap returns. Stronger optional
controls can also lower the parent's upgraded-light budget or shorten its
light distance during qualifying combat.

Requirements
------------

  Tainted Grail: The Fall of Avalon Mono branch
  BepInEx 5 Mono
  All Lights Cast Shadows 1.2.0 or compatible

How it works
------------

Before the parent mod applies its light upgrades, this plugin records the
current Unity global shadow-quality value. After the parent restores the tracked
lights, this plugin restores that global value too.

The plugin also shields configured bonfire/campfire light objects before the
parent scan. This keeps the parent mod from forcing shadows onto fire lights
that were authored to illuminate nearby stones or firepit geometry without
being occluded by those same meshes.

The shadow-atlas guard changes only point and spot lights promoted by the
parent mod. A point light consumes six shadow maps, so the default 256-pixel
per-face cap can materially reduce atlas pressure. Each light's original HDRP
resolution override, tier, and override mode are restored when it leaves the
parent budget, the parent is disabled, the guard is disabled, or this addon
unloads. Existing lower explicit resolutions are never raised.

This plugin adds no per-frame light scanning. It samples the hero's combat
state four times per second and requests a parent scan only when combat mode
changes. Optional budget and distance values exist only during that scan, are
clamped so they cannot increase the parent's values, and are restored
immediately afterward. The parent JSON is never edited.

Configuration
-------------

Start the game once to generate:

  BepInEx\config\ks.tgfoa.tg-all-lights-cast-shadows-addon.cfg

The current release uses ConfigSchemaVersion 3. Older configs are backed up
and regenerated once so defaults apply cleanly. Compatible customized settings
are preserved by exact current setting name across future schema resets.

Defaults:

  ProtectBonfireLights = true
  AdditionalExcludedLightPathFragments =
  VerboseExclusionLogging = false
  ProtectShadowAtlas = true
  PromotedShadowResolution = 256
  CombatPerformanceEnabled = true
  OutdoorCombatOnly = true
  CombatExitDelaySeconds = 5
  CombatReduceAtlasResolution = true
  CombatShadowResolution = 128
  CombatLimitLightBudget = false
  CombatMaximumUpgradedLights = 30
  CombatLimitDistance = false
  CombatMaximumDistanceMeters = 20
  ShowToggleNotifications = true
  ShowGrailFloatingTextDiagnostics = true
  Diagnostics = false

FoA Mod Manager section order:

  Excluded Lights
  Shadow Atlas
  Combat Performance
  Notifications
  Diagnostics
  Import Previous Settings

Default excluded light path fragments:

  WyrdNight_Repeller_Bonfire,Repeller_Bonfire,Bonfire,Campfire

Set VerboseExclusionLogging to true only while diagnosing a missed light. It
logs each excluded light path once per scene so exact runtime names can be
added to AdditionalExcludedLightPathFragments.

When Grail Floating Text is installed, toggling the parent shadow mod shows a
System notification even with Diagnostics disabled. Set ShowToggleNotifications
to false to hide these confirmations. Diagnostics logs parent
active-light counts, point and spot counts, estimated shadow-map use, capped
and restored state, and resolution details for newly captured lights.
ShowGrailFloatingTextDiagnostics defaults to true and, only while Diagnostics
is enabled, controls collapsed combat, atlas, and diagnostics-only warning
notices. Disabling it leaves detailed logging active. Atlas summaries remain
suppressed on the main menu and during loading and teleport transitions.

With both diagnostic settings enabled, entering and leaving qualifying combat also shows a
collapsed System notification and logs the active combat overrides. Failures
in the optional parent-config controls are reported separately; atlas reduction
continues independently.

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

This addon does not disable or specially classify the player's HeroLight. Use
No Player Light if that light should be absent.

Version safety
--------------

This addon touches specific All Lights Cast Shadows internals and was
built against All Lights Cast Shadows 1.2.0. Later parent mod updates may
make this addon unnecessary or incompatible. If All Lights Cast Shadows
updates, check its changelog and disable this addon if the same behavior is
fixed upstream or the light scan internals change.

Mod author note
---------------

The All Lights Cast Shadows author is welcome to incorporate this behavior
upstream if desired. This companion addon exists to solve local lighting issues
quickly and is not intended to replace the original mod.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
