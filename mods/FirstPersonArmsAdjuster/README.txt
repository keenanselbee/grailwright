First Person Arms Adjuster 0.8.6
================================

Platforms: Windows and Linux through Proton.

First Person Arms Adjuster is an experimental prototype for Tainted Grail:
The Fall of Avalon. It moves the rendered first-person body, arms, weapons, and
held arrows in camera space without changing the world camera or field of view.
It also provides camera-only first-person head bob with optional render-only
viewmodel follow that never changes the gameplay arm and body rig.

Config
------

Config file:
BepInEx/config/ks.tgfoa.first-person-arms-adjuster.cfg

FoA Mod Manager and the generated config organize the live controls into
General, Position, Equipment Depth, Advanced - Retraction Profile, Head Bob,
Advanced - Animation Guards, Advanced - Effects,
Diagnostics, and the final Import Previous Settings section.
Friendly labels show metres and signed directions. Guard controls now share one
raw section as well as one FoA Mod Manager section.

Defaults:
Enabled = true
ForwardOffset = 0.30
HorizontalOffset = 0.0
VerticalOffset = 0.0
ShoulderRetraction = 0.05
SpineRetractionPercent = 0
Spine1RetractionPercent = 0
Spine2RetractionPercent = 100
LeftShoulderRetractionPercent = 100
RightShoulderRetractionPercent = 100
UpperArmRetractionPercent = 30
ForearmRetractionPercent = 20
LowerTorsoRetractionPercent = 0
ChestHelperRetractionPercent = 0
ShoulderFixRetractionPercent = 0
NativeClothRetractionPercent = 0
TorsoRendererRetractionPercent = 50
TestRetractionBoneName =
TestBoneRetractionPercent = 0
UseCategoryForwardOffsets = true
AdjustAttachedEffects = true
EnableHeadBob = true
HeadBobPreset = Balanced
HeadBobSmoothness = 0.7
SprintEmphasis = 0.75
HeadBobSpeedPercent = 75
StabilizeViewmodelDuringHeadBob = true
ViewmodelHeadBobFollowPercent = 100
SuppressMotionBlurDuringHeadBob = false
TemporalSafeHeadBobTiming = true
MeleeForwardOffset = 0.30
BowForwardOffset = 0.30
MagicForwardOffset = 0.30
EnableAnimationGuards = true
MitigateHeldMeleeBodyIntrusion = true
EnableDodgeGuard = true
EnableSheathingGuard = true
EnableBowDrawGuard = true
BowDrawMaximumOffsetPercent = 33
UseSharedGuardTarget = true
SharedMoveTowardVanillaPercent = 50
HeldMeleeOffsetScale = 1.0
HeldMeleeExtraForwardOffset = -0.05
HeldMeleeExtraVerticalOffset = -0.05
Diagnostics = false

Offsets are measured in meters relative to the camera. Positive ForwardOffset
moves the first-person model farther away, positive HorizontalOffset moves it
right, and positive VerticalOffset moves it up. Changes apply live.

Category offsets are enabled by default so melee, bows, and magic use 0.30.
Unarmed and unrecognized equipment continue to use ForwardOffset.

ShoulderRetraction is a render-only correction for torso and
shoulder geometry that becomes visible during first-person poses. Values from
0.02 to 0.04 remain useful for light correction, while the default 0.05 provides
a moderate correction during normal poses. It retracts Spine, Spine1,
Spine2, and the shoulder region toward or behind the camera, tapers through the
upper arms and forearms, and reaches zero at the hands so held weapons retain
their configured position. The correction is independent of equipment depth
and remains independent while FPAA's attack, sheathing, and interaction guards
adjust the complete presentation. Dodge guarding temporarily raises retraction
toward 0.25 metres, then restores the configured value. It does not change the
live skeleton, aim, physics, projectiles, camera, or third person. Upgrading to
0.7.1 regenerates this stronger setting at 0; set it again after the restart.

Advanced - Retraction Profile exposes the percentage of the master distance
applied to each matched region. Spine, Spine1, and Spine2 are independent;
left and right shoulders are independent; upper arms and forearms use shared
percentages. Every value updates live from 0 to 200 percent. At a master value
of 0.25, 200 percent moves that region 0.50 metres. Defaults use the validated
0/0/100/100/100/30/20 profile, and hands remain fixed at zero. Keep neighboring percentages
reasonably close when possible because large discontinuities can stretch
vertices blended across adjacent bones.

Four off-by-default grouped controls cover native FppArms geometry outside the
normal taper. LowerTorsoRetractionPercent targets Hips;
ChestHelperRetractionPercent targets both Breast_Base and Breast pairs;
ShoulderFixRetractionPercent targets both ShoulderFix helpers; and
NativeClothRetractionPercent targets every contiguous Cloth_Skirt bone. Each
accepts 0 to 400 percent of the master distance and is additive with the normal
profile. Native cloth movement is skipped if a future rig does not store the
matching bones in one contiguous range, preventing unrelated bones from being
moved accidentally.

TorsoRendererRetractionPercent controls the complete native torso-garment
renderer identified during isolation testing as Cloth2. FPAA prefers its Torso
mesh or material name and uses the second Cloth renderer only as a compatibility
fallback. Its default 50 percent moves that renderer by half the master Shoulder
Retraction distance; the live 0 to 400 percent range permits stronger or weaker
correction. FPAA gives only this renderer a dedicated render rig, so the arms,
hands, weapons, and other cloth renderers keep their normal bone profile. The
master ShoulderRetraction defaults to 0.05, producing 0.025 metres of dedicated
torso-renderer correction outside dodges.

When visible geometry does not respond to the standard profile, enable
Diagnostics with ShoulderRetraction above 0. FPAA logs every native Kandra rig,
its complete indexed bone list, and each renderer path and rig. Copy one exact
bone name into TestRetractionBoneName, then raise TestBoneRetractionPercent from
0 while watching the problem pose. The test accepts 0 to 400 percent and is
additive when the selected bone already belongs to the normal profile. Change
one bone at a time. Avoid hand, finger, and weapon-socket names so the diagnostic
does not move held equipment. Clear the name and return the percentage to 0
after identifying the mesh owner.

Head bob applies distance-driven vertical and side-to-side movement only while
the main first-person camera renders, then restores the camera. It does not
change the gameplay arm and body rig, camera depth, aim, or third-person camera.
The game's
Accessibility / Head Bob setting is the global master switch; FPAA suppresses
the game's arm-moving first-person bob and substitutes this camera-only motion.
Choose Subtle, Balanced, or Strong with HeadBobPreset. HeadBobSmoothness eases
changes in cadence and vertical/lateral strength over 0.02 to 0.18 seconds; it
does not filter or weaken the steady jogging waveform. SprintEmphasis uses the
Hero's native sprint state. Its default 0.75 adds about 56% movement and 19%
raw cadence while sprinting; 0 disables the sprint bonus. Vertical cadence
remains distance-driven and follows a continuous soft-knee curve as it
approaches 3.2 cycles per second while walking and up to 4.2 while sprinting.
This prevents rapid in-place stepping without flattening ordinary acceleration
or preset differences into an abrupt fixed cadence. HeadBobSpeedPercent scales
that soft-limited result from 50% to 150% without changing movement strength,
player speed, or SprintEmphasis; the default 75% slows the normal gait cadence.

ViewmodelHeadBobFollowPercent controls how much of the camera-only translation
is shared in exact camera space by the first-person arms and held presentation.
The default 100 removes the motion caused by FPAA bob, and the default-enabled
StabilizeViewmodelDuringHeadBob toggle enforces the same exact compensation.
Disable stabilization and lower the percentage to restore relative movement. It
cannot remove movement authored into native weapon animations. The shared
render-only offset keeps arms, equipment, effects, culling, and integrations
aligned and never changes aim, attacks, colliders, projectiles, or the camera's
own motion. Enable
SuppressMotionBlurDuringHeadBob if HDRP motion blur makes the moving view look
soft. It affects only the main camera's movement on frames with visible FPAA
bob, preserves moving-object blur, and restores the previous value immediately
after each render.

TemporalSafeHeadBobTiming is enabled by default to avoid temporal blur.
FPAA applies the same camera-only bob immediately before HDRP
records its current and previous camera matrices, rather than at the later
camera-render callback. This can reduce TAA, DLSS, or FSR smearing without
changing the bob path, viewmodel position, render quality, or render-pass
count. Disable it to restore the established timing instantly.

When the pause menu stops the hero controller update but continues rendering,
FPAA refreshes a missing presentation-offset snapshot from the current
first-person arms pivot. This keeps arms, equipment, effects, and integrations
at their configured positions without allowing a render callback to replace
the authoritative post-camera-rotation snapshot during normal gameplay.

AdjustAttachedEffects moves cached Visual Effect Graph and particle-system
presentation transforms beneath equipped first-person items with the rendered
viewmodel, including effects nested beneath the first-person body hierarchy.
Body-root effects keep their adjusted emitter position between frames and are
compensated during the temporary body render translation so local-space effects
receive the offset once. Gameplay item roots, lights, colliders, rigidbodies,
character controllers, sockets, and projectile origins remain unchanged.
Disable it if another mod intentionally owns the same equipped effect transforms.

Advanced - Animation Guards provides a master switch plus independent attack,
dodge, all-equipment sheathing, and bow-draw toggles. Shared targeting is enabled
by default and makes every normal melee attack and sheathing state use
SharedMoveTowardVanillaPercent. At 0 the configured FPAA position remains, at 50
half remains at the strongest point, and at 100 the viewmodel reaches vanilla.
Overlapping guards use the strongest influence instead of multiplying together.
Shared mode overrides held-melee retained-scale and extra-correction tuning.
Dodge guarding instead changes only Shoulder Retraction.

The bow-draw guard follows normalized pull progress so its correction enters
gradually while the arrow is nocked and holds through the held pose. Release
restoration begins immediately after the native normalized 0.05 projectile-fire
threshold and eases over 0.40 seconds; cancellation uses the same gradual return.
Its default 33 percent ceiling
follows BowForwardOffset dynamically: a bow depth of 0.30 allows about 0.10
metres of positive FPAA depth during draw. It can only reduce FPAA's added depth
and does not change bow animation, aim, or projectile origin.

Attack guarding is enabled by default. With shared targeting, every normal melee
light and heavy phase, including initial, chained, tired, forward, charged,
held, release, and alternate states, eases toward the common target. Disabling
shared targeting restores the established held-heavy and forward-attack coverage
with HeldMeleeOffsetScale and the held-only corrections.

During every forward, backward, sideways, and diagonal dodge, native dash
callbacks immediately drive Shoulder Retraction toward the 0.25 metre maximum
with a fast 0.06-second ease-out. The active dash state refreshes a short hold
through rapid chained or redirected dodges, then retraction eases back over 0.20
seconds. The complete presentation offset, arms, weapons, and attached effects
do not move toward vanilla.

Set HeldMeleeOffsetScale between 0.0 and 1.0 to retain part of the normal
offset during the held pose, or disable MitigateHeldMeleeBodyIntrusion to keep
the configured position throughout heavy attacks.

Some two-handed poses expose body geometry during held attacks.
HeldMeleeExtraForwardOffset and HeldMeleeExtraVerticalOffset therefore apply
after the retained-offset scale. Their default -0.05 forward and -0.05 vertical
correction pulls the charge pose toward the camera near plane and slightly down.
Both accept -0.50 to 0.50 and update live for per-animation tuning.

During normal or alternate sheathing, the complete presentation offset eases
toward the configured guard target from 45% through 90% of the animation. This
covers melee, dual-wield, bows, and magic through their shared unequip states.
If sheathing is interrupted, the configured offset returns smoothly over 0.20
seconds.

Version 0.8.6 changes the default bow-draw ceiling to 33 percent and clears
reusable presentation-scan buffers during scene transitions. Version 0.8.3 makes dodge detection event-driven and anchors gradual bow
restoration to the exact projectile-fire point. Version 0.8.2 adds dynamic dodge retraction, smoother guard transitions, and the
new 0.30 bow depth, 67 percent draw ceiling, and 0.05 retraction defaults while
consolidating animation-guard settings. Version 0.8.1 adopts the validated presentation defaults, expands shared guard
coverage to all normal melee attacks and all equipment sheathing, combines
overlapping guards by strongest influence, and adds a dynamic bow-draw ceiling.
Version 0.7.9 adds consolidated animation-guard toggles and an opt-in shared
move-toward-vanilla target while preserving every existing default behavior.
Version 0.7.8 replaces the temporary renderer-isolation test with a permanent
independently weighted correction for the confirmed Cloth2 torso renderer.
Version 0.7.6 keeps an unavailable or already-disabled isolation target quiet
until its renderer state changes. Version 0.7.5 adds safe one-at-a-time native body and cloth renderer isolation
for identifying geometry that does not respond to any rig bone. Version 0.7.4 adds permanent off-by-default lower-torso, chest-helper,
shoulder-fix, and native-cloth groups. Version 0.7.3 adds exact-name native bone testing plus complete indexed bone
and renderer diagnostics for geometry outside the normal profile. Version 0.7.2 adds live per-region spine, shoulder, and arm retraction tuning
with asymmetric shoulder control and matched-bone diagnostics. Version 0.7.1 extends shoulder retraction through the chest and behind the
vanilla position while keeping hands and weapons fixed. Version 0.7.0 adds
targeted render-only shoulder retraction without moving the hands or held
weapons. Version 0.6.9 adds exact camera-space viewmodel stabilization and adjustable
head-bob speed without changing bob strength. Version 0.6.8 adds adjustable
render-only viewmodel follow so camera bob can
remain expressive without moving arms and held visuals as heavily. Version
0.6.7 gives jogging a continuous soft cadence limit and smooths gait
response without attenuating or distorting the completed waveform. Version
0.6.6 keeps the shared presentation offset active while the pause menu
stops gameplay updates. Version 0.6.5 limits high-speed vertical cadence to a natural walk-to-sprint
range without changing bob strength, smoothing, or temporal rendering. Version
0.6.4 adds an off-by-default temporal-safe head-bob timing test so HDRP
can capture the bobbed camera pose before temporal processing. Version 0.6.3 bases the shared presentation offset on the current first-person
arms pivot instead of the rendered camera's delayed transform, keeping rapid
left-right turns aligned without interpolation, viewmodel lag, or separation
between arms, equipment, effects, and optional integrations. Version 0.6.2 adds
optional render-scoped motion-blur suppression for FPAA head bob without
changing the default presentation. Version 0.5.9 makes the dodge midpoint configurable and
keeps chained dodge transitions continuous. Version 0.5.8 adds the animation-timed dodge offset
transition. Version 0.5.6 aligns the stored sections with the established FoA Mod Manager
layout and removes their numeric prefixes. Version 0.5.5 removes the old native-bob and locomotion-guard path and presents
the camera-only system as one four-option Head Bob section. Version 0.5.4 enforces the vanilla accessibility gate immediately before every
alternate-motion render. Version 0.5.3 gates alternate motion behind the vanilla Accessibility / Head
Bob setting while leaving FPAA's native-bob switch independent and off by
default. Version 0.5.2 integrates the retired standalone Immersive Camera Motion system
as FPAA's default render-only alternative, adds three intensity presets,
smoothing, and stronger sprint emphasis, and leaves the full native bob plus
locomotion guard available in its own off-by-default tab. Version 0.5.1 matches
locomotion guard engagement and release at 0.40 seconds.
Version 0.5.0 lengthened the locomotion guard release to 1.00 second for a gentler
return. Version 0.4.9 gates the locomotion guard behind active vanilla first-person head
bob. Version 0.4.8 adopts the tested 0.5 head-bob and 0.75 retained-depth defaults,
groups the related controls, and greatly softens guard transitions. Version 0.4.7 adds the grounded locomotion depth guard and first-person native
head-bob control. Version 0.4.6 keeps arms, equipment, attached effects, and the temporary body
render translation on one immutable per-frame camera-space offset so rapid look
movement cannot pull hands away from weapons. Version 0.4.5 adds the dedicated
sprint-attack transition guard for both melee grips. Version 0.4.4 leaves VFPB's
camera-anchored torso and legs at their native
placement and applies the offset directly at the game's first-person Kandra
render-collection stage. This keeps the visible arms aligned across perspective order,
equipment changes, and scene reloads without moving the full-body overlay into
the camera. Version 0.4.3's unsafe whole-overlay translation has been removed.
Version 0.4.2 limits the sheathing transition blend to active two-handed melee
grips and follows Versatile Weapons grip changes. Version 0.4.1 introduced the
transition blend. Version 0.4.0 reorganizes
FoA Mod Manager with friendly labels, units, explicit
ordering, separate basic and advanced sections, and clearer equipment/fallback
guidance without changing stored config keys. Version 0.3.9 fixes body-root
effect discovery and render compensation so
vanilla torch flames and embers follow the adjusted torch without receiving the
offset twice. Version 0.3.8 introduced targeted equipped-effect alignment.
Version 0.3.7 fixes two-handed held-heavy detection when the melee FSM's
layer-activity flag lags its visible animation and adds detailed melee-state
diagnostics. Version 0.3.6 adopts the tuned held-melee scale and corrections as defaults.
Version 0.3.5 adds a cached visual-offset API so optional presentation effects
can follow the rendered first-person model without moving gameplay sockets.
Version 0.3.3 adds the held-only beyond-vanilla correction. Version 0.3.2 adds
the held-melee body-intrusion guard. Version 0.3.0 converts
the shared camera-space weapon translation into each
linked Drake entity's own transform space. Multi-part and two-handed weapons
therefore receive one consistent world-space offset even while their child
transforms rotate during animation, reducing viewmodel jitter without temporal
smoothing or camera lag.

Bonfire handling still eases to the vanilla position, remains there through
submenus and the complete controller stand-up transition, then eases back in
without changing configured values. A conservative timed fallback is used only
if the game's transition signal is unavailable.

The mod retains the visually complete 0.2.0 approach. It offsets the
current Kandra first-person bone regions, matching Kandra culling data, linked
Drake equipment, ordinary hierarchy content, and cached equipped presentation
effects together. It retains later
scene-transition and per-frame rig refresh safeguards, but does not install
special full-draw hand handling or bow-state diagnostic hooks.

Start with ForwardOffset values between 0.20 and 0.35. Set all three offsets to
0 or set Enabled to false to restore the vanilla presentation.

Prototype notes
---------------

The offset is applied from one late-frame camera sample to the current Kandra
first-person bone regions, their culling data, linked Drake render entities
beneath both hand sockets, and ordinary hierarchy content. The current rig and
equipment regions are refreshed repeatedly so stale indices or creature rigs
cannot be reused. Scene changes clear cached rig, renderer, and ECS references
and briefly pause native offsets while the replacement scene settles.
The mod does not alter the live animation skeleton, weapon sockets, world FOV,
aiming, projectile origins, hit detection, physics, or the saved player
position.

This restored implementation writes presentation-only Kandra and Drake render
data. It does not move the live animation skeleton, hand sockets, fire point,
or projectile origin, but it remains an experimental native-rendering mod.

Melee mitigation uses the game's active animation states rather than movement
or camera-bob heuristics. HeavyAttackStart and HeavyAttackWait drive the held
corrections, while LightAttackForward drives the sprint-attack vanilla return.
Bow and spell charging remain independent.

Because the game uses one first-person body hierarchy, visible torso or leg
geometry may move with the arms. Test melee weapons, bows, shields, magic, item
use, swimming, and mounted gameplay before treating the prototype as final.
Released arrows originate from the unmodified gameplay fire point, so a small
visual transition can remain when the held arrow becomes a projectile. The
previous experimental full-draw hand-only correction has been removed.

When Diagnostics is enabled, the log reports the resolved hierarchy, Kandra
rig, culling and linked Drake equipment paths, cached presentation-effect
count, camera-only head bob, and melee FSM state changes with their layer activity plus independent
held-heavy and sprint-attack mitigation results.

Compatibility
-------------

Grail Floating Text is optional. If installed, it can show critical load errors
in game, with details in BepInEx/LogOutput.log.

Blood Magic Expansion 2.4.3 and newer can optionally consume the visual-offset
API so its blood-spell hand lights stay aligned with adjusted first-person arms.
The integration is cached and allocation-free during normal frame updates.

VFPB - Visible First Person Body remains independently anchored to the camera.
First Person Arms Adjuster moves the native first-person arms and held equipment
without translating VFPB's torso and legs.

True Third Person suppresses native head bob in both perspectives by default.
First Person Arms Adjuster recovers the underlying Accessibility / Head Bob
state for its camera-only first-person motion and leaves third person under
True Third Person's control.

Previous settings
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after
importing.
