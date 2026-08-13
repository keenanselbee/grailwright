Dishonored Dynamic Crosshair
============================

Version 3.1.4
Platforms: Windows and Linux through Proton.

Configurable PNG reticles for Tainted Grail: The Fall of Avalon.

Plugin identity:
  Name: Dishonored Dynamic Crosshair
  DLL: DishonoredDynamicCrosshair.dll
  GUID: ks.tgfoa.dishonored-dynamic-crosshair
  Version: 3.1.4

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
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_0.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_1.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_2.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_3.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_4.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_5.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_6.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_7.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bow.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_magic.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_meager.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_worthy.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_potent.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_prime.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_weakspot_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_critical_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_meager_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_worthy_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_potent_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_prime_overlay.png

Configuration is generated after the game starts:
  BepInEx\config\ks.tgfoa.dishonored-dynamic-crosshair.cfg

Version 3.1.4 uses ConfigSchemaVersion 9. The schema last changed because the
default Steel and Bone hit-marker KillingBlowSizeMultiplier increased from
1.2x to 1.3x. Existing configs are backed up and regenerated; untouched old defaults
receive the new value while compatible customized settings remain eligible for
automatic recovery. On first launch from an older
schema, the previous config is backed up beside the active config as a dated
.bak file and fresh defaults are generated. Reticle PNG paths, sizes, scales,
colors, opacities, size mode, Blood Magic quality scaling, and crouch-indicator
visual tuning survive schema resets. Behavioral and diagnostic settings receive
fresh defaults.

Design Goal
-----------

The mod replaces the vanilla crosshair with a small set of readable controls:
choose when the reticle appears, choose the reticle PNGs, choose the shared
colors and opacity, and optionally add Blood Magic Expansion corpse feedback
or Steel and Bone hit markers. More technical behavior is kept in the Advanced
section.

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
  BloodMagicQualityCrosshairsEnabled
  UseCorpseQualityScale
  MaximumQualityScale
  UsableCorpseColor

5. Steel and Bone Hit Markers
  Enabled
  KillingBlowOverlaysEnabled
  SizeMultiplier
  DamageOverTimeSizeMultiplier
  KillingBlowSizeMultiplier
  DurationMultiplier
  KillingBlowDurationMultiplier

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
and Magic. Blood Magic uses UsableCorpseColor for usable corpses. Blocked,
bloodless, and spent corpses use the ordinary DefaultColor and IdleOpacity.

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

Steel and Bone Hit Markers
--------------------------

When Steel and Bone 3.3.9 or newer is installed, successful outgoing player
damage can temporarily replace the current reticle with contextual hit
feedback. Dishonored Dynamic Crosshair keeps its existing target colors and
reticle behavior unchanged when Steel and Bone is absent.

The numbered frame reports the material result:
  custom_reticle_0.png       Zero damage or immunity
  custom_reticle_1.png       Extreme resistance below x0.35
  custom_reticle_2.png       Strong resistance from x0.35 to below x0.70
  custom_reticle_3.png       Mild resistance from x0.70 to below x0.95
  custom_reticle_4.png       Neutral from x0.95 through x1.05; General default
  custom_reticle_5.png       Mild weakness above x1.05 through x1.10
  custom_reticle_6.png       Strong weakness above x1.10 through x1.20
  custom_reticle_7.png       Extreme weakness above x1.20

Direct nonlethal hits add a central diamond over the base result marker. The
diamond uses the hit's calculated color and is omitted for damage-over-time
ticks and all killing blows:
  hitmarker.png

Weak-spot and critical feedback are independent overlays above that diamond
and can appear together over any base result:
  hitmarker_weakspot_overlay.png
  hitmarker_critical_overlay.png

A killing blow adds one corpse-quality overlay after those layers, so its
Meager, Worthy, Potent, or Prime result remains visible over a simultaneous
weak spot or critical. Nonlethal weak-spot and critical hits retain Steel and
Bone's calculated red-shifted color. A lethal hit instead turns the base frame
and every visible overlay dark red (#8C0003). KillingBlowDurationMultiplier defaults to
1.5x the normal marker duration, after which Meager, Worthy, Potent, and Prime
apply another 1.00x, 1.33x, 1.67x, or 2.00x respectively. These overlays are enabled independently through
KillingBlowOverlaysEnabled:
  hitmarker_killingblow_meager_overlay.png
  hitmarker_killingblow_worthy_overlay.png
  hitmarker_killingblow_potent_overlay.png
  hitmarker_killingblow_prime_overlay.png

Frames are selected from Steel and Bone's actual effectiveness multiplier.
All marker layers use Steel and Bone's final damage-number color. A new hit
immediately replaces the active marker and restarts its timer. Missing
numbered frames fall back to neutral custom_reticle_4.png; missing overlays
are simply skipped. All numbered frames and overlays hot reload like the
normal reticle assets.

SizeMultiplier defaults to 1.15x ReticleSizePixels and intentionally ignores
BowScale, MagicScale, BloodMagicScale, and corpse-quality scaling.
DamageOverTimeSizeMultiplier replaces that size for Bleed, Poison, Burn, and
Breath tick markers and defaults to 1.1x ReticleSizePixels.
KillingBlowSizeMultiplier replaces both normal and damage-over-time sizing for
the complete killing-blow marker composition and defaults to 1.3x
ReticleSizePixels.
DurationMultiplier defaults to 1x Steel and Bone's final damage-number
duration, including its critical and direct-melee duration adjustments. The
marker snaps into place, settles quickly, and fades during its final quarter.

Blood Magic
-----------

BloodMagic is optional integration with Blood Magic Expansion API v9. Blood
Magic Expansion 2.7.6 or newer is required for unavailable corpse tiers.
When Blood Magic Expansion is not loaded, Dishonored Dynamic Crosshair caches
that unavailable state for the current game session instead of repeatedly
searching for the optional API.

No focused corpse shows no blood reticle. Registered corpses, including
blocked, bloodless, and spent corpses, select custom_reticle_bloodmagic_meager.png,
custom_reticle_bloodmagic_worthy.png, custom_reticle_bloodmagic_potent.png, or
custom_reticle_bloodmagic_prime.png. Usable or channeling corpses use
UsableCorpseColor and can also scale from 1x to MaximumQualityScale. Unavailable
corpses retain their tier shape at 1x and use the ordinary DefaultColor and
IdleOpacity. An
unregistered corpse without a resolved tier uses custom_reticle_bloodmagic.png.
Living enemies keep the normal hostile or magic reticle. Set
BloodMagicQualityCrosshairsEnabled to false to retain the single fallback
appearance.

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

Blood Magic Expansion 2.7.6 or newer is supported for optional tiered
blood-magic corpse reticle feedback through
`BloodMagicExpansion.BloodMagicApi` v9.
Older BloodMagicApi versions are not supported.

Steel and Bone 3.3.9 or newer is supported for optional contextual hit-marker
and corpse-tier killing-blow feedback through
`SteelAndBone.SteelAndBoneHitFeedbackApi` v5. API v4 is not supported. Damage
numbers may be disabled in Steel and Bone without disabling the hit markers.

Owrocc ModifyColors can remain installed because this plugin owns the colors
of its separate Unity UI Image. Owrocc ModifyCrosshair is redundant and
should be removed after this plugin is confirmed working.

Do not deploy older reticle DLLs beside DishonoredDynamicCrosshair.dll.
Older DLLs can use a different plugin identity and patch the same UI.

Build from this folder:
  MSBuild.exe src\DishonoredDynamicCrosshair.csproj /p:Configuration=Release

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
