KS All Lights Cast Shadows Addon
================================

Version: 2.1.2
Platforms: Windows and Linux through Proton.

Original mod: All Lights Cast Shadows 1.2.0

Short description: A bounded, view-aware companion controller that keeps the
original mod's shadowed-light experience without letting every nearby light
consume performance at once.

The addon keeps All Lights Cast Shadows as the required parent, including its
main toggle and shadow-strength settings, but safely owns light discovery and
selection by default. It selects only a small, stable set of useful point and
spot lights, prioritizes illumination that affects the camera view, and places
permanent limits on light count, range, and estimated shadow-map faces.

This directly targets the severe interior, town-at-night, dungeon, Wyrd Sight,
flicker, and atlas-pressure cases reported by players. Exact range-sphere view
tests, centre-weighted stable ranking, staged initial filling, smooth sequential
shadow handoffs, and optional tightening-only interior limits reduce visible
switching without weakening the permanent safety caps. Existing firelight
protection, reversible atlas caps, outdoor-combat reductions, notifications,
diagnostics, and previous-settings recovery remain included.

Requirements
------------

  Tainted Grail: The Fall of Avalon Mono branch
  BepInEx 5 Mono
  All Lights Cast Shadows 1.2.0 or compatible

How safe selection works
------------------------

When the parent requests a scan, the addon performs one unsorted search for
loaded point and spot lights. It rejects distant and excluded objects before
capturing or changing state, then caches only eligible nearby lights. Lightweight
0.2-second refreshes select from that checked cache rather than searching every
loaded light again.

The default selection can contain at most 16 lights, reach 25 metres, and cost
at most 48 estimated shadow-map faces. Point lights cost six faces and spot
lights cost one. Stricter limits configured in the parent or active combat layer
still win; this addon never uses those settings to exceed its permanent caps.

Lights whose exact range sphere intersects the camera view are preferred. An
eight-metre distance margin, two-metre selection-retention advantage, moderate
screen-centre priority, 0.75-second view-exit grace, and two-light offscreen
reserve keep the set stable while moving or turning. Initial shadows arrive in
batches of four. Later replacements fade one outgoing shadow to zero, transfer
the same budget slot, then fade the incoming shadow in, so their maps never
overlap.

The vanilla HeroLight, Wyrd Sight highlight lights, summons,
character/item/object preview lights, lockpicking lights, and the exact
player-placed portable Bonfire are excluded by default. Existing configurable
bonfire/campfire path exclusions are retained.

State ownership
---------------

Only final selected lights are captured and changed. For each one, the addon
records its native Unity shadow mode and strength plus the HDRP shadow enabled
state, surface and volumetric shadow dimmers, and resolution override state.
Those exact values return when the light leaves selection, the parent disables,
the scene changes, a scan fails, or the addon unloads.

Added volumetric-fog shadows are suppressed by default while ordinary surface
shadows remain. HDRP values are written only when they differ. Shadow rendering
is requested when a light activates or its native shadow state changes; the
authored HDRP shadow update mode is never replaced with Every Frame or otherwise
modified.

The existing atlas guard never raises a lower authored resolution. The default
cap is 256 per face, reduced to 128 during qualifying outdoor combat, then
restored five seconds after combat. Optional combat limits can further lower the
active light count or distance.

The optional interior profile is enabled with values matching the permanent
16-light, 25-metre, 48-face, and 256-resolution ceilings. Lower its values to
tighten only interiors; it never raises a stricter permanent, parent, or combat
limit.

Dawn and dusk directional shadows
---------------------------------

ImproveDawnDuskShadows is an optional feature and is disabled by default. When
enabled, it shortens only the existing DayNightSystem directional-shadow blend,
reducing the long weak-shadow period around the sun/moon handoff. It does not
create or enable another directional light or shadow map, and it does not change
the gameplay clock, timescale, weather schedule, or Eyes in the Dark fades.

Without Eyes in the Dark normalization, ShadowBlendMinutes applies a fixed
number of in-game minutes on each side of dawn and dusk; the default is 10
instead of the game's normal 60. When Eyes in the Dark is installed and
NormalizeForEyesInTheDark is enabled, the addon converts the configured
EyesBlendSecondsPerSide target through the live world-clock rate. The default
30-real-second target is approximately 8 in-game minutes during daylight, 37
during a quiet fast night, and 19 during a maximum-threat night.

Loaded day/night systems are searched only when the feature activates, a
setting changes, or the loaded-scene set changes. Later clock-rate changes reuse
the tracked systems. Each original blend is captured and restored only while the
addon still owns its applied value, so a later change by another component is
left untouched.

Configuration
-------------

Start the game once to generate:

  BepInEx\config\ks.tgfoa.tg-all-lights-cast-shadows-addon.cfg

The current release uses ConfigSchemaVersion 3. These settings are additions,
so existing schema-3 configs remain intact and receive the new defaults. The
Import Previous Settings tab remains available for compatible backups.

Performance defaults:

  UseSafeSelectionController = true (restart required after changing)
  MaximumUpgradedLights = 16
  MaximumDistanceMeters = 25
  MaximumShadowMapFaces = 48
  SuppressAddedVolumetricShadows = true

View Priority defaults:

  HysteresisMeters = 8
  SelectionRetentionMeters = 2
  PreferViewRelevantLights = true
  ScreenCenterPriorityMeters = 4
  SelectionRefreshSeconds = 0.2
  ViewExitDelaySeconds = 0.75
  OffscreenReserveLights = 2
  MaximumSelectionSwapsPerRefresh = 2
  ShadowHandoffSeconds = 0.6
  InitialFillBatchSize = 4

Excluded Lights defaults:

  ProtectBonfireLights = true
  RespectExternalPlayerLightOwnership = true
  ExcludeHeroLight = true
  ExcludeWyrdSightLights = true
  ExcludeSummonLights = true
  ExcludeInterfacePreviewLights = true
  ExcludeLockpickingLights = true
  ExcludePlacedBonfireLights = true
  AdditionalExcludedLightPathFragments =
  VerboseExclusionLogging = false

Interior Performance defaults:

  InteriorPerformanceEnabled = true
  InteriorMaximumUpgradedLights = 16
  InteriorMaximumDistanceMeters = 25
  InteriorMaximumShadowMapFaces = 48
  InteriorPromotedShadowResolution = 256

Shadow Atlas defaults:

  ProtectShadowAtlas = true
  PromotedShadowResolution = 256

Directional Shadows defaults:

  ImproveDawnDuskShadows = false
  ShadowBlendMinutes = 10
  NormalizeForEyesInTheDark = true
  EyesBlendSecondsPerSide = 30

Combat Performance defaults:

  CombatPerformanceEnabled = true
  OutdoorCombatOnly = true
  CombatExitDelaySeconds = 5
  CombatReduceAtlasResolution = true
  CombatShadowResolution = 128
  CombatLimitLightBudget = false
  CombatMaximumUpgradedLights = 30
  CombatLimitDistance = false
  CombatMaximumDistanceMeters = 20

Feedback defaults:

  ShowToggleNotifications = true
  ShowGrailFloatingTextDiagnostics = true
  Diagnostics = false

FoA Mod Manager section order:

  Excluded Lights
  Performance
  View Priority
  Interior Performance
  Shadow Atlas
  Directional Shadows
  Combat Performance
  Notifications
  Diagnostics
  Import Previous Settings

Default configured fire path fragments:

  WyrdNight_Repeller_Bonfire,Repeller_Bonfire,Bonfire,Campfire

Set VerboseExclusionLogging to true only while diagnosing a missed runtime
light. Each excluded path is logged once per scene and can be added to
AdditionalExcludedLightPathFragments.

Diagnostics and notifications
-----------------------------

When Grail Floating Text is installed, parent toggle changes show one System
notification while ShowToggleNotifications is enabled. With Diagnostics on,
the log reports candidates, selected point/spot counts, estimated faces, view
relevance, initial filling, handoffs, interior state, exclusions, atlas caps,
restoration, and combat overrides.
When MageLight owns an active shadowed point light, diagnostics report its six
external faces separately and keep the selected-plus-external total within the
configured permanent face budget.
ShowGrailFloatingTextDiagnostics controls collapsed in-game diagnostic summaries
without disabling detailed log output. Summaries remain suppressed on menus and
during loading or teleport transitions.

Compatibility
-------------

Keep All Lights Cast Shadows installed; this addon declares it as a hard
dependency. Do not install the standalone TGAllLightsCastShadowsSafe replacement
alongside this pair: its selection architecture has been adapted into this addon.

When MageLight or No Player Light is installed, the default
RespectExternalPlayerLightOwnership setting leaves the exact HeroLight
indoor/outdoor hierarchy entirely under that mod. The addon never captures,
enables, activates, or restores those lights. MageLight's active shadowed point
light reserves six faces; No Player Light's disabled object reserves none.
Without either mod, ExcludeHeroLight still prevents the vanilla proximity light
from receiving an unnatural player-following shadow.

MageLight and No Player Light express conflicting player-light choices when
installed together: toggling MageLight on can reactivate the object disabled by
No Player Light after its bounded retry window. The addon reports this combination
but does not decide which external mod wins. Disable or uninstall MageLight if No
Player Light should remain authoritative.

Eyes in the Dark is detected through its plugin GUID. Its live
WeatherSecondsPerRealSecond value is read only while the optional dawn/dusk
feature and normalization are enabled. This keeps the shadow transition aligned
with Eyes' changing day and threat-scaled night rates without taking ownership
of Eyes' clock, colors, exposure, sky, moon, or world-light behavior.

UseSafeSelectionController should remain enabled for the recommended experience.
Turning it off restores the previous compatibility path after the next game
restart, allowing the parent to perform its original broad scan while the addon's
older atlas, combat, exclusion, notification, and restoration hooks remain.

This addon targets All Lights Cast Shadows plugin version 1.2.0. If the parent
updates, review its changelog and verify compatibility before continuing.

Credits
-------

The safe selector, bounded ownership, semantic-exclusion, and restoration
architecture was adapted from TGAllLightsCastShadowsSafe by Nexus Mods user
pupkidze007:

  https://forums.nexusmods.com/profile/194045963-pupkidze007/

Installation
------------

Install the included folder at:

  BepInEx\plugins\TGAllLightsCastShadowsAddon

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart after importing.
