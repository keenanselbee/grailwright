Dishonored Dynamic Crosshair
============================

Configurable PNG reticles for Tainted Grail: The Fall of Avalon.

Plugin identity:
  Name: Dishonored Dynamic Crosshair
  DLL: DishonoredDynamicCrosshair.dll
  GUID: ks.tgfoa.dishonored-dynamic-crosshair
  Version: 2.8.3

Required game version:
  Tainted Grail: The Fall of Avalon v1.25 / Patch 1.25
  Steam beta branch: mono
  Mono branch build: 24270691
  BepInEx 5 Mono 5.4.23.3

This plugin is compiled for the managed Mono game assemblies. It is not
compatible with the normal public IL2CPP branch (build 23527157) or an
IL2CPP BepInEx installation.

The package folder is named DishonoredDynamicCrosshair. The plugin name, DLL,
GUID, and generated config all use the Dishonored Dynamic Crosshair identity.

Deployment files:
  BepInEx\plugins\DishonoredDynamicCrosshair\DishonoredDynamicCrosshair.dll
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bow.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_magic.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic.png

Configuration is generated after the game starts:
  BepInEx\config\ks.tgfoa.dishonored-dynamic-crosshair.cfg

Version 2.8.3 uses ConfigSchemaVersion 3. On first launch from an older
schema, the previous config is backed up beside the active config as a dated
.bak file and fresh defaults are generated. Future releases keep user settings
unless another configuration change requires the schema version to increase.

Design Goal
-----------

The mod replaces the vanilla crosshair with a small set of readable controls:
choose when the reticle appears, choose the reticle PNGs, choose the shared
colors and opacity, and optionally enable Blood Magic Expansion corpse feedback. More
technical behavior is kept in the Advanced section.

Configuration Sections
----------------------

1. Core
  Enabled
  Preset

2. Reticles
  ReticleSizePixels
  GeneralSprite
  BowSprite
  MagicSprite
  BloodMagicSprite
  BowScale
  MagicScale
  BloodMagicScale

3. Colors and Opacity
  DefaultColor
  HostileColor
  NonHostileColor
  IdleOpacity
  TargetOpacity
  MountedOpacityMultiplier

4. Blood Magic
  Mode
  RequireRelevantBloodSpell
  UseCorpseQualityScale
  MaximumQualityScale
  UsableCorpseColor

5. Advanced
  MagicDetection
  UseGeneralWhenHandsDown
  RangeMultiplier
  HostilityRefreshIntervalSeconds
  SizeMode
  TextureFiltering
  ShowCrouchIndicator
  CrouchIndicatorOpacity
  HideVanillaReticles

6. Diagnostics
  LogBloodMagicScaleDiagnostics

Contexts
--------

Context priority is BloodMagic corpse override, Bow, Magic, then General.

Bow uses the same IsRanged classification as the game's bow crosshair.
MagicDetection defaults to CastMagicOnly for aimed magic and can be changed
to AnyMagic. A missing Bow, Magic, or BloodMagic PNG falls back to the general
PNG when possible. All reticle PNG files reload automatically after they are
replaced.

UseGeneralWhenHandsDown defaults to true. When the game hides the hero's
weapons, the plugin uses the General reticle even if a bow or magic item is
still equipped. Set it to false if equipped-item context should remain active.

Presets
-------

  AlwaysVisible (default)
    General, Bow, and Magic are always visible and use smart target colors.

  TargetOnly
    General, Bow, and Magic are hidden unless a hostile, friendly, or neutral
    NPC is targeted.

  CombatReady
    General is target-only. Bow and Magic remain visible.

  HostilesOnly
    All contexts appear only over hostile NPCs.

Presets control visibility only. They do not overwrite sprites, colors,
opacity, size, Blood Magic behavior, or crouch settings.

Target Detection
----------------

RangeMultiplier defaults to 1.2, extending the game's NPC target-detection
raycast by 20 percent. This controls hostile/non-hostile coloring and the
game's associated health-bar targeting. It does not extend interaction
distance because the game uses a separate interaction raycast. Set it to 1
for vanilla range.

HostilityRefreshIntervalSeconds controls how often the currently hovered NPC
is re-evaluated and defaults to 0.1 seconds.

Colors and Opacity
------------------

DefaultColor, HostileColor, and NonHostileColor are shared by General, Bow,
and Magic. Blood Magic uses the normal Magic/default color path for blocked,
bloodless, or spent corpses, and UsableCorpseColor for usable corpses.

Colors use Unity HTML format:
  #RRGGBB
  #RRGGBBAA

Default colors:
  DefaultColor = #FFFFFFFF
  HostileColor = #E8583CFF
  NonHostileColor = #8DD57AFF
  UsableCorpseColor = #E8583CFF

IdleOpacity defaults to 0.1. TargetOpacity defaults to 0.3 for hostile,
friendly, and neutral targets. MountedOpacityMultiplier defaults to 0, hiding
custom reticles while the hero is mounted. Set it to 1 to keep reticles
visible on mounts.

Appearance
----------

ReticleSizePixels defaults to 80. General uses that size directly. Bow, Magic,
and BloodMagic then apply their scale multipliers. SizeMode defaults to
ScreenPixels, which compensates for the HUD canvas scale so the final reticle
size is measured in physical screen pixels. UIUnits preserves canvas-relative
sizing.

TextureFiltering defaults to MipmappedTrilinear. Runtime mipmaps are generated
for all reticle PNGs and trilinear filtering is used when they are displayed
smaller than their source resolution. Bilinear preserves the legacy behavior
without mipmaps.

Blood Magic
-----------

BloodMagic is optional integration with Blood Magic Expansion API v4. Blood
Magic Expansion 2.0.0 or newer is required.
When Blood Magic Expansion is not loaded, Dishonored Dynamic Crosshair caches
that unavailable state for the current game session instead of repeatedly
searching for the optional API.

No focused corpse shows no blood reticle. Blocked, bloodless, or spent corpses
show custom_reticle_bloodmagic.png with the normal Magic/default color path at
1x. Usable or channeling corpses use UsableCorpseColor and can scale from 1x
to MaximumQualityScale from corpse quality. Quality changes size only, not
color. Living enemies keep the normal hostile or magic reticle.

The quality curve intentionally keeps weak corpses near normal Magic size and
allows strong corpses to grow toward the maximum. The dead zone and curve are
internal tuning values in 2.8.3, not user-facing config options.

Crouch Indicator
----------------

The crouching and detection indicator remains enabled by default. Its
CrouchIndicatorOpacity defaults to 0.15 and is applied to the whole indicator
without replacing the game's internal detection colors or animations.
CrouchIndicatorVerticalOffset defaults to 0. Positive values move the whole
indicator lower; negative values move it higher. The offset is intentionally
uncapped.

Vanilla Reticles
----------------

HideVanillaReticles controls the game's default, melee, bow, and item-provided
reticles together. It is enabled by default while this plugin is enabled.
Disabling or unloading the plugin restores vanilla activation and crouch
indicator opacity. While disabled, the custom reticle and background polling
pause. While enabled, the plugin owns crosshair visibility and does not use the
game's general crosshair setting as a visibility rule.

Compatibility
-------------

Blood Magic Expansion 2.0.0 or newer is supported for optional blood-magic
corpse reticle feedback through `BloodMagicExpansion.BloodMagicApi` v4.

Owrocc ModifyColors can remain installed because this plugin owns the colors
of its separate Unity UI Image. Owrocc ModifyCrosshair is redundant and
should be removed after this plugin is confirmed working.

Do not deploy older reticle DLLs beside DishonoredDynamicCrosshair.dll.
Older DLLs can use a different plugin identity and patch the same UI.

Build from this folder:
  MSBuild.exe src\DishonoredDynamicCrosshair.csproj /p:Configuration=Release
