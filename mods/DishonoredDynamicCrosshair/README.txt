Dishonored Dynamic Crosshair
============================

Version 3.5.0
Platforms: Windows and Linux through Proton.

Configurable PNG reticles for Tainted Grail: The Fall of Avalon.

Plugin identity:
  Name: Dishonored Dynamic Crosshair
  DLL: DishonoredDynamicCrosshair.dll
  GUID: ks.tgfoa.dishonored-dynamic-crosshair
  Version: 3.5.0

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
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_0.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_1.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_2.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_3.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_5.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_6.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_7.png
  BepInEx\plugins\DishonoredDynamicCrosshair\dot.png
  BepInEx\plugins\DishonoredDynamicCrosshair\stealth_eye_0.png through stealth_eye_10.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_0.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_1.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_2.png
  BepInEx\plugins\DishonoredDynamicCrosshair\custom_reticle_bloodmagic_3.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_backstab.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_campfire.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_digging.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_fishing.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_hand.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_lockpick.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_lumbering.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_mining.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_mount.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_read.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_rest.png
  BepInEx\plugins\DishonoredDynamicCrosshair\interaction_talk.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_weakspot_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_critical_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_3_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_2_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_0_overlay.png
  BepInEx\plugins\DishonoredDynamicCrosshair\hitmarker_killingblow_1_overlay.png

Configuration is generated after the game starts:
  BepInEx\config\ks.tgfoa.dishonored-dynamic-crosshair.cfg

Version 3.5.0 uses ConfigSchemaVersion 18. This release changes no stored
setting, so existing version-18 configurations remain current.
On first launch from an older schema, the previous config is backed up beside
the active config as a dated .bak file and fresh defaults are generated.
Reticle PNG paths, sizes, scales, colors, opacities, size mode, Blood Magic
quality scaling, and crouch-indicator visual tuning survive schema resets.
Behavioral and diagnostic settings receive fresh defaults.

Design Goal
-----------

The mod replaces the vanilla crosshair with a small set of readable controls:
choose when the reticle appears, choose the reticle PNGs, choose the shared
colors and opacity, replace interaction prompts with contextual icons, and
optionally add Blood Magic Expansion corpse feedback, Ambush Integrity
backstab readiness, or Steel and Bone hit markers. More technical behavior is
kept in the Advanced section.

Configuration Sections
----------------------

Core
  Enabled
  Preset

Reticles
  ReticleSizePixels
  ShowCenterDot
  GeneralSprite
  BowSprite
  MagicSprite
  BloodMagicSprite
  BowScale
  MagicScale
  BloodMagicScale

Colors and Opacity
  DefaultColor
  HostileColor
  NonHostileColor
  IdleOpacity
  TargetOpacity
  MountedOpacityMultiplier

Interaction Icons
  Enabled
  IconScale
  IconOpacity
  CrosshairOpacityWhileActive
  HideVanillaInteractionKeyPrompts
  VanillaTextVerticalOffset

Blood Magic
  Mode
  RequireRelevantBloodSpell
  BloodMagicQualityCrosshairsEnabled
  UseCorpseQualityScale
  MaximumQualityScale
  UsableCorpseColor

Steel and Bone Hit Markers
  Enabled
  IncludeSummonAttacks
  KillingBlowOverlaysEnabled
  SizeMultiplier
  DamageOverTimeSizeMultiplier
  KillingBlowSizeMultiplier
  DurationMultiplier
  KillingBlowDurationMultiplier

Ambush Integrity
  BackstabReadyOverlayEnabled
  BackstabReadyColor

Advanced
  MagicDetection
  UseGeneralWhenHandsDown
  RangeMultiplier
  HostilityRefreshIntervalSeconds
  SizeMode
  TextureFiltering
  ShowCrouchIndicator
  CrouchIndicatorOpacityMultiplier
  CrouchIndicatorVerticalOffset
  HideVanillaReticles

Diagnostics
  LogBloodMagicScaleDiagnostics

Import Previous Settings
  CurrentSchema
  AvailableBackupSchema
  ImportPreviousSettingsNow

Contexts
--------

Context priority is BloodMagic corpse override, Bow, Magic, then General.
Steel and Bone hit markers temporarily replace the outer context reticle but
render in a dedicated layer above the center dot or crouch-awareness eye.
IncludeSummonAttacks defaults to true. Summon markers cannot replace an active
hero marker, while hero markers always replace summon markers. Disable it to
show feedback only for the hero's own attacks.
Routine interaction icons render above the reticle and awareness eye but below
hit markers. Hit feedback temporarily suppresses a routine interaction icon.
Ambush Integrity's backstab-ready state adds its own topmost overlay above the
active reticle, routine interaction icon, and all hit-marker feedback.

Bow uses the same IsRanged classification as the game's bow crosshair.
MagicDetection defaults to CastMagicOnly for aimed magic and can be changed
to AnyMagic. Bow and Magic use the General custom_reticle.png by default at
0.9x and 1.1x, while their sprite paths and scales remain configurable. A
missing Bow, Magic, or BloodMagic PNG falls back to the general PNG when
possible. All reticle PNG files reload automatically after they are
replaced.

Center Dot
----------

ShowCenterDot defaults to true. General, Bow, Magic, and BloodMagic all use
dot.png at the base ReticleSizePixels size before context multipliers, while
still following the selected SizeMode and reference-height scaling. Bow,
Magic, Blood Magic, and corpse-quality multipliers do not change the dot.
Blood Magic Expansion and Soul and Service quality reticles suppress the
ordinary dot so their corpse silhouettes remain clear.
While the custom crouch-awareness eye is
active, frames 0 and 1 remain dotless; from frame 2 through frame 10 the shared
dot becomes the eye's pupil even when ShowCenterDot is false. The pupil uses
the same context-independent size and follows the eye's color, opacity, and
vertical offset. Direct
nonlethal hitmarker.png feedback covers the ordinary dot, while the complete
eye and pupil remain underneath every hit-marker layer. Dot and eye assets hot
reload with the other PNGs.

Interaction Icons
-----------------

Interaction icons are enabled by default and follow the exact action selected
by the game's interaction HUD. Mining, lumbering, fishing, digging, reading,
talking, resting in a bed or bedroll, mounting, and using a campfire or bonfire
have dedicated PNGs. Soul and Service commands use
interaction_command_attack.png, interaction_command_hold.png,
interaction_command_follow.png, or interaction_command_behavior.png. Attack and
Swarm share the attack icon. Attack, Swarm, and individual Hold/Follow pulse for
0.675 seconds. Hold All, Follow All, and Behavior
pulse for 1.35 seconds. Items, containers, doors, gathering, searching, and
other ordinary or unknown interactions use interaction_hand.png.
While a corpse, chest, or other container's quick-loot panel is open, a
non-empty container uses the hand icon and an empty container uses no routine
interaction icon.

Any currently locked door, container, or other location uses
interaction_lockpick.png even when the lock is key-only, broken, or cannot be
picked. Illegal actions and pickpocketing use the hand icon in the same dark red
as killing-blow feedback. Lock state takes priority over the illegal color.

Only one routine interaction icon is shown. It uses a fixed square based on
ReticleSizePixels instead of Bow, Magic, Blood Magic, or corpse-quality scales.
IconScale defaults to 1.1 and IconOpacity defaults to 0.8. While active,
it dims the ordinary reticle, dot, and crouch-awareness eye according to
CrosshairOpacityWhileActive, which defaults to 0. Hit markers temporarily
hide the routine icon and remain undimmed. The backstab indicator overrides all
routine icons and remains the topmost layer.

HideVanillaInteractionKeyPrompts defaults to true. It removes the complete E, F,
or controller-button container so the prompt background fits the remaining
action text without empty space. Locked and blocked explanations remain visible;
vanilla hold-progress graphics inside that button container are hidden with it.
VanillaTextVerticalOffset defaults to -120 UI units, moving the prompt upward
from the game's default just-below-center position. The offset follows the HUD
canvas scaling across resolutions and the player's HUD Scale setting. Turning
suppression or the plugin off restores the vanilla button container. All interaction
PNGs use the standard 512x512 canvas and hot reload while the game is running.

Ambush Integrity Backstab Ready
-------------------------------

When Ambush Integrity is installed, Dishonored reads its versioned state API
and shows interaction_backstab.png only while the exact current target passes Ambush
Integrity's final backstab eligibility result. The 512x512 image uses the same
reticle canvas as the other PNGs at a fixed 1.0 scale, with no special scaling
or activation pulse.

BackstabReadyOverlayEnabled controls the optional integration.
BackstabReadyColor defaults to the killing-blow dark red #8C0003FF. The
indicator renders above every reticle and hit-marker layer; while it is active,
all underlying crosshair elements render at half their normal opacity.
Replacing the PNG while the game is running hot-reloads the overlay. The state
disappears on target changes, eligibility loss, or stale data. Committed Ambush
does not falsely claim that the backstab action itself is available.

Without Ambush Integrity, the integration remains inactive and the normal
reticles are unchanged.

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

Equipment Context
-----------------

Dishonored normally chooses General, Bow, or Magic from the equipped items.
When Versatile Weapons - Dynamic Grip is installed, Dishonored ignores the
item in any hand that Versatile Weapons currently suppresses. A one-handed
melee weapon using a two-handed grip therefore returns to the General reticle
and default melee scale while its paired magic hand is stowed, then restores
the Magic reticle when that hand becomes active again.

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

ReticleSizePixels defaults to 80. General uses that size directly. Bow and
Magic reuse the General reticle by default at 0.9x and 1.1x, while BloodMagic
applies its own scale multiplier. SizeMode defaults to Reference1440p, which
preserves the authored 2560x1440 appearance and scales every visual by screen
height: 80 becomes 60 physical pixels at 1080p, remains 80 at any 1440p
resolution including ultrawide, and becomes 120 at 4K. ScreenPixels keeps the
configured size fixed in physical pixels. UIUnits follows the game's HUD
canvas and HUD Scale. Live resolution and HUD-scale changes refresh the full
presentation immediately.

TextureFiltering defaults to MipmappedTrilinear. Runtime mipmaps are generated
for all reticle PNGs and trilinear filtering is used when they are displayed
smaller than their source resolution. Bilinear preserves the legacy behavior
without mipmaps.

Steel and Bone Hit Markers
--------------------------

When Steel and Bone 3.9.4 or newer is installed, successful outgoing hero-side
damage can temporarily replace the outer context reticle with contextual hit
feedback. IncludeSummonAttacks defaults to true. Summon feedback remains lower
priority and cannot replace an active hero marker; hero feedback always replaces
a summon marker. Disable it to show only the hero's own attacks. The dedicated
layer stays above the center dot or stealth eye.
Dishonored Dynamic Crosshair keeps its existing target colors and reticle
behavior unchanged when Steel and Bone is absent.

The numbered frame reports the material result:
  hitmarker_0.png            Zero damage or immunity
  hitmarker_1.png            Extreme resistance below x0.35
  hitmarker_2.png            Strong resistance from x0.35 to below x0.70
  hitmarker_3.png            Mild resistance from x0.70 to below x0.95
  custom_reticle.png         Neutral from x0.95 through x1.05 in every context
  hitmarker_5.png            Mild weakness above x1.05 through x1.10
  hitmarker_6.png            Strong weakness above x1.10 through x1.20
  hitmarker_7.png            Extreme weakness above x1.20

Direct nonlethal hits add a central diamond over the base result marker. The
diamond uses the hit's calculated color, covers the ordinary center dot, stays
above the stealth eye, and is omitted for damage-over-time ticks and all
killing blows:
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
  hitmarker_killingblow_3_overlay.png
  hitmarker_killingblow_2_overlay.png
  hitmarker_killingblow_0_overlay.png
  hitmarker_killingblow_1_overlay.png

Frames are selected from Steel and Bone's actual effectiveness multiplier.
All marker layers use Steel and Bone's final damage-number color. The latest hit
wins within the same source priority and restarts the timer. Hero hits replace
summon markers immediately, while summon hits wait for an active hero marker to
finish. Missing
numbered frames fall back to neutral custom_reticle.png; missing overlays
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
blocked, bloodless, and spent corpses, select custom_reticle_bloodmagic_0.png,
custom_reticle_bloodmagic_1.png, custom_reticle_bloodmagic_2.png, or
custom_reticle_bloodmagic_3.png for Meager, Worthy, Potent, or Prime. Usable or channeling corpses use
UsableCorpseColor and can also scale from 1x to MaximumQualityScale. Unavailable
corpses retain their tier shape at 1x and use the ordinary DefaultColor and
IdleOpacity. An
unregistered corpse without a resolved tier uses custom_reticle_bloodmagic_0.png,
the same asset as Meager.
Living enemies keep the normal hostile or magic reticle. Set
BloodMagicQualityCrosshairsEnabled to false to retain the single fallback
appearance.

The quality curve intentionally keeps weak corpses near normal Magic size and
allows strong corpses to grow toward the maximum. The dead zone and curve are
internal tuning values in 2.8.3, not user-facing config options.

Soul and Service
----------------

When Soul Salvage is equipped, eligible corpses and active owned summons use
the same Meager, Worthy, Potent, and Prime quality silhouettes as Blood Magic.
They are tinted #22A886 Necrotic green, matched to the perceptual brightness of the
default Blood Magic red, and use the established quality scale. Soul and
Service owns eligibility and quality; no reticle is shown when another spell
is equipped.

Crouch Indicator
----------------

The custom eleven-frame crouch-awareness eye replaces the vanilla indicator
while preserving the game's awareness calculation. stealth_eye_0.png is a
closed center line with the longest horizontal awareness lines outside the
reticle. Frames 1 through 9 progressively open the eye while retracting those
lines toward the center. stealth_eye_10.png is fully open with no awareness
lines. The eye itself remains small enough to fit inside the Bow reticle.

CrouchIndicatorOpacityMultiplier defaults to 1, making the custom eye and pupil
match the active crosshair opacity exactly. Lower values retain all dynamic
crosshair fading while making the complete awareness indicator proportionally
fainter. Blood Magic corpse reticles and routine interaction icons hide the
complete awareness eye and pupil while their presentation is active.
CrouchIndicatorVerticalOffset defaults to 0. Positive values move the complete
custom indicator lower; negative values move it higher. The offset is
intentionally uncapped. Disabling ShowCrouchIndicator hides both the custom and
vanilla indicators.

Vanilla Reticles
----------------

HideVanillaReticles controls the game's default, melee, bow, and item-provided
reticles together. It is enabled by default while this plugin is enabled.
Disabling or unloading the plugin restores vanilla activation and the original
crouch indicator. While disabled, the custom reticle and background polling
pause. While enabled, the plugin owns crosshair visibility and does not use the
game's general crosshair setting as a visibility rule.

Compatibility
-------------

Blood Magic Expansion 2.7.6 or newer is supported for optional tiered
blood-magic corpse reticle feedback through
`BloodMagicExpansion.BloodMagicApi` v9.
Older BloodMagicApi versions are not supported.

Soul and Service is supported through `SoulAndService.SoulAndServiceApi` v5 for
automatic green quality reticles over Soul Salvage corpses and active owned
summons. Attack, Swarm, Hold, and Follow interactions use their matching command icons, and
successful commands pulse the matching icon for the duration published by Soul
and Service.

Steel and Bone 3.9.4 or newer is supported for optional contextual hit-marker
and corpse-tier killing-blow feedback through
`SteelAndBone.SteelAndBoneHitFeedbackApi` v6. Older API versions are not supported. Damage
numbers may be disabled in Steel and Bone without disabling the hit markers.

Versatile Weapons - Dynamic Grip is supported through its optional
hand-suppression API. Stowed equipment does not keep a Bow or Magic reticle
context active, and ordinary behavior remains unchanged when the mod is absent.

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
