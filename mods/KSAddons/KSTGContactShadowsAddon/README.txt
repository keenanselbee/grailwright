KS Contact Shadows Addon
========================

Version: 0.1.3
Platforms: Windows and Linux through Proton.

Original mod: Contact Shadows 1.0.0-mono

Short description: Stabilizes Contact Shadows around a bounded set of nearby interior lights and restores every touched runtime setting when inactive.

This companion addon replaces the parent mod's broad per-light scan with a
stable, bounded controller. Contact shadows are enabled for up to four nearby,
visible point or spot lights by default. Valid selections are held briefly and
a challenger must be materially stronger than the weakest selected light
before it can take over, reducing rapid changes that can present as flicker in
dense structures.

The default behavior is interior-only because contact shadows provide their
most useful grounding around nearby local lights while outdoor directional
lighting has less benefit and a larger visual footprint.

Requirements
------------

  Tainted Grail: The Fall of Avalon Mono branch
  BepInEx 5 Mono
  Contact Shadows 1.0.0-mono or compatible

Default behavior
----------------

  Enables contact shadows only in interiors.
  Selects up to four active point or spot lights within 15 meters.
  Ignores inactive, disabled, zero-intensity, and zero-range lights.
  Holds a valid light for at least one second.
  Switches only when another light scores at least 25 percent better.
  Refreshes the active scene light cache every five seconds.
  Uses 16 samples, a 20-meter visual fade distance, 0.075 ray length,
  and 0.6 opacity.

The addon changes only runtime values. It never edits TGContactShadows.json.
It records and restores every selected light's exact HDRP contact-shadow state,
each touched camera's contact-shadow frame settings, and the parent mod's
temporary global volume whenever the effect becomes inactive.

Configuration
-------------

Start the game once to generate:

  BepInEx\config\ks.tgfoa.tg-contact-shadows-addon.cfg

Common entries:

  Enabled = true
  InteriorsOnly = true
  MaximumContactShadowLights = 4
  MaximumLightDistanceMeters = 15
  MinimumLightHoldSeconds = 1
  SwitchAdvantagePercent = 25
  CandidateRefreshSeconds = 5
  ContactShadowMaxDistance = 20
  SampleCount = 16
  Length = 0.075
  Opacity = 0.6
  ShowToggleNotifications = true
  Diagnostics = false

Set InteriorsOnly to false to use the same configured light budget outdoors.
Directional and area lights remain excluded because the addon intentionally
favors nearby local point/spot lighting.

SampleCount = 8 is the recommended manual performance alternative. Lower
sample counts may look more pixelated, so 16 remains the default.

Grail Floating Text notifications
---------------------------------

When Grail Floating Text is installed, toggling the parent Contact Shadows mod
shows one System notification for each actual runtime state change. Enabled
messages identify the default interior-only scope. Set ShowToggleNotifications
to false to hide these confirmations. Interior and exterior transitions are
never announced.

Installation
------------

Install the included folder at:

  BepInEx\plugins\TGContactShadowsAddon

Keep Contact Shadows installed. This plugin declares it as a hard dependency
and will not run without it.

Compatibility and limitations
-----------------------------

The addon is compatible with the separate KS All Lights Cast Shadows Addon and
KS Global Illumination Addon. Each continues to manage its own effect.

Contact shadows are screen-space. Geometry outside the camera depth buffer
cannot contribute, so some close-camera disappearance or edge artifacts may
remain. The addon is designed to reduce dominant-light switching and overly
broad activation rather than promise that every HDRP artifact can be removed.

Version safety
--------------

This addon touches specific Contact Shadows internals and was built against
the 1.0.0-mono file, which loads as plugin version 1.0.0. If the parent mod
updates, check its changelog and disable this addon if the same behavior is
added upstream or its internals change.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after
importing.
