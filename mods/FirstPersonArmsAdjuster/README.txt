First Person Arms Adjuster 0.4.6
================================

Platforms: Windows and Linux through Proton.

First Person Arms Adjuster is an experimental prototype for Tainted Grail:
The Fall of Avalon. It moves the rendered first-person body, arms, weapons, and
held arrows in camera space without changing the world camera or field of view.

Config
------

Config file:
BepInEx/config/ks.tgfoa.first-person-arms-adjuster.cfg

FoA Mod Manager organizes the live controls into General, Position, Equipment
Depth, Advanced - Melee Guards, Advanced - Effects, and Diagnostics. Friendly
labels show metres and signed directions while the stored config keys below
remain unchanged.

Defaults:
Enabled = true
ForwardOffset = 0.30
HorizontalOffset = 0.0
VerticalOffset = 0.0
UseCategoryForwardOffsets = true
AdjustAttachedEffects = true
MeleeForwardOffset = 0.30
BowForwardOffset = 0.10
MagicForwardOffset = 0.30
MitigateHeldMeleeBodyIntrusion = true
HeldMeleeOffsetScale = 1.0
HeldMeleeExtraForwardOffset = -0.05
HeldMeleeExtraVerticalOffset = -0.05
Diagnostics = false

Offsets are measured in meters relative to the camera. Positive ForwardOffset
moves the first-person model farther away, positive HorizontalOffset moves it
right, and positive VerticalOffset moves it up. Changes apply live.

Category offsets are enabled by default so melee and magic use 0.30 while bows
use 0.10. Unarmed and unrecognized equipment continue to use ForwardOffset.

AdjustAttachedEffects moves cached Visual Effect Graph and particle-system
presentation transforms beneath equipped first-person items with the rendered
viewmodel, including effects nested beneath the first-person body hierarchy.
Body-root effects keep their adjusted emitter position between frames and are
compensated during the temporary body render translation so local-space effects
receive the offset once. Gameplay item roots, lights, colliders, rigidbodies,
character controllers, sockets, and projectile origins remain unchanged.
Disable it if another mod intentionally owns the same equipped effect transforms.

Held melee mitigation is enabled by default. While a one-handed, two-handed,
dual-wielded, or alternate melee heavy attack is raised or held, the complete
viewmodel offset eases toward HeldMeleeOffsetScale. The default 1.0 retains the
configured position while enabling the held-only corrections and state blend.
The configured position and corrections ease back after release or cancel.

The same body-intrusion switch also handles the game's dedicated one-handed and
two-handed sprint attacks. When LightAttackForward begins, the complete visual
offset rapidly returns to the vanilla position and stays there for the attack.
It restores the configured position smoothly after the sprint attack ends.

Set HeldMeleeOffsetScale between 0.0 and 1.0 to retain part of the normal
offset during the held pose, or disable MitigateHeldMeleeBodyIntrusion to keep
the configured position throughout heavy attacks.

Some two-handed poses expose body geometry during held attacks.
HeldMeleeExtraForwardOffset and HeldMeleeExtraVerticalOffset therefore apply
after the retained-offset scale. Their default -0.05 forward and -0.05 vertical
correction pulls the charge pose toward the camera near plane and slightly down.
Both accept -0.50 to 0.50 and update live for per-animation tuning.

During a melee weapon's active two-handed grip, the complete presentation offset
eases back to the vanilla position from 45% through 90% of the sheathing
animation. This lets adjusted arms and equipment retreat before the game hides
the first-person viewmodel. One-handed grips, bows, spells, and unarmed states
keep their normal presentation throughout sheathing. Versatile Weapons is
supported automatically: its current grip classification determines whether
the blend applies. If sheathing is interrupted, the configured offset returns
smoothly over 0.20 seconds.

Version 0.4.6 keeps arms, equipment, attached effects, and the temporary body
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
count, and melee FSM state changes with their layer activity plus independent
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

Previous settings
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after
importing.
