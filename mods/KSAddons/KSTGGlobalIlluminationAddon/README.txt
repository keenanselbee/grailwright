KS Global Illumination Addon
============================

Version: 0.1.9
Platforms: Windows and Linux through Proton.

Original mod: Global Illumination 1.0.0

Short description: Adds contextual indoor and outdoor quality tiers plus a conservative 60 FPS adaptive mode to Global Illumination.

This companion addon keeps Global Illumination's current visual settings as
the Full profile. By default, new scenes start at Performance and Adaptive mode
raises quality one tier at a time when sustained gameplay performance meets its
target. Interiors can recover to Full while exteriors stop at Balanced.

No All Lights Cast Shadows behavior is included. That mod and its KS addon
remain completely separate.

Requirements
------------

  Tainted Grail: The Fall of Avalon Mono branch
  BepInEx 5 Mono
  Global Illumination 1.0.0 or compatible

Default profiles
----------------

Full:

  Uses the parent mod's current settings unchanged. With the original defaults,
  this keeps 0.3 diffuse, 0.3 reflection, 8 samples, 2 bounces, and full
  resolution.

Balanced:

  Preserves the parent mod's diffuse and reflection values, caps samples at 4,
  caps bounces at 1, and uses half-resolution screen-space GI.

Performance:

  Temporarily disables screen-space GI while preserving the parent mod's
  indirect diffuse and reflection multipliers.

Adaptive behavior
-----------------

Adaptive is the default mode. StartAtPerformance is enabled by default, so each
new scene begins at Performance and raises quality only after sustained FPS
meets the target. The separate Adaptive Presets section defaults interiors to
Full and exteriors to Balanced; each preset is the highest tier Adaptive may
reach in that environment. Either can be set to Full, Balanced, or Performance.

RememberSceneTier takes priority when returning to a scene during the same game
session, resuming its last successful tier without relearning it. Disable
StartAtPerformance to begin previously unseen scenes at their contextual preset
instead.

With the default 60 FPS target, the addon steps down one tier after smoothed
gameplay FPS remains below 54 for four seconds. It steps back up only after FPS
remains at least 59 for 30 seconds, with a 15-second cooldown between changes.

FPS sampling pauses during loading transitions, paused or unfocused play, and
invalid long frames. The addon waits five seconds after entering a scene and
remembers the last successful tier for each scene during the current session.

Grail Floating Text notifications
---------------------------------

When Grail Floating Text is installed, toggling Global Illumination shows a
System notification confirming its actual runtime state, including changes
from its hotkey and JSON reloads. Set ShowToggleNotifications to false to hide
these confirmations.

ShowGrailFloatingTextDiagnostics defaults to true and, only while Diagnostics
is enabled, controls adaptive FPS-driven tier notices with the new tier and
smoothed FPS. Disabling it leaves detailed BepInEx logging active. Parent toggle
confirmations remain independently controlled by ShowToggleNotifications.

Configuration
-------------

Start the game once to generate:

  BepInEx\config\ks.tgfoa.tg-global-illumination-addon.cfg

Common entries:

  Enabled = true
  Mode = Adaptive
  TargetFps = 60
  ShowToggleNotifications = true
  ShowGrailFloatingTextDiagnostics = true
  InteriorPreset = Full
  ExteriorPreset = Balanced
  StartAtPerformance = true
  RememberSceneTier = true
  SampleWindowSeconds = 5
  DowngradeMarginFps = 6
  DowngradeHoldSeconds = 4
  UpgradeMarginFps = 1
  UpgradeHoldSeconds = 30
  ChangeCooldownSeconds = 15
  SceneWarmupSeconds = 5
  Diagnostics = false

FoA Mod Manager section order:

  General
  Adaptive Presets
  Adaptive Tuning
  Notifications
  Diagnostics
  Import Previous Settings

Mode can also be set to Full, Balanced, or Performance to hold one tier in all
locations.

The addon changes only the parent mod's in-memory runtime configuration. It
does not edit TGGlobalIllumination.json. Parent settings are restored when the
addon is disabled, unloaded, or outside active gameplay. If the parent JSON is
reloaded, its new values become the Full profile.

Installation
------------

Install the included folder at:

  BepInEx\plugins\TGGlobalIlluminationAddon

Keep Global Illumination installed. This plugin declares it as a hard
dependency and will not run without it.

Version safety
--------------

This addon touches specific Global Illumination internals and was built
against plugin version 1.0.0. If the parent mod updates, check its changelog and
disable this addon if the same behavior is added upstream or its internals
change.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after
importing.
