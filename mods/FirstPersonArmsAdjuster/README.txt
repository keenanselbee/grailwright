First Person Arms Adjuster 0.3.1
================================

Platforms: Windows and Linux through Proton.

First Person Arms Adjuster is an experimental prototype for Tainted Grail:
The Fall of Avalon. It moves the rendered first-person body, arms, weapons, and
held arrows in camera space without changing the world camera or field of view.

Config
------

Config file:
BepInEx/config/ks.tgfoa.first-person-arms-adjuster.cfg

Defaults:
Enabled = true
ForwardOffset = 0.30
HorizontalOffset = 0.0
VerticalOffset = 0.0
UseCategoryForwardOffsets = true
MeleeForwardOffset = 0.30
BowForwardOffset = 0.10
MagicForwardOffset = 0.30
Diagnostics = false

Offsets are measured in meters relative to the camera. Positive ForwardOffset
moves the first-person model farther away, positive HorizontalOffset moves it
right, and positive VerticalOffset moves it up. Changes apply live.

Category offsets are enabled by default so melee and magic use 0.30 while bows
use 0.10. Unarmed and unrecognized equipment continue to use ForwardOffset.

Version 0.3.0 converts the shared camera-space weapon translation into each
linked Drake entity's own transform space. Multi-part and two-handed weapons
therefore receive one consistent world-space offset even while their child
transforms rotate during animation, reducing viewmodel jitter without temporal
smoothing or camera lag.

Fireplace handling still eases to the vanilla position, remains there through
submenus and the complete controller stand-up transition, then eases back in
without changing configured values. A conservative timed fallback is used only
if the game's transition signal is unavailable.

The mod retains the visually complete 0.2.0 approach. It offsets the
current Kandra first-person bone regions, matching Kandra culling data, linked
Drake equipment, and ordinary hierarchy content together. It retains later
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

Because the game uses one first-person body hierarchy, visible torso or leg
geometry may move with the arms. Test melee weapons, bows, shields, magic, item
use, swimming, and mounted gameplay before treating the prototype as final.
Released arrows originate from the unmodified gameplay fire point, so a small
visual transition can remain when the held arrow becomes a projectile. The
previous experimental full-draw hand-only correction has been removed.

When Diagnostics is enabled, the log reports the resolved hierarchy plus the
Kandra rig, culling, and linked Drake equipment paths receiving the offset.

Compatibility
-------------

Grail Floating Text is optional. If installed, it can show critical load errors
in game, with details in BepInEx/LogOutput.log.

Previous settings
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after
importing.
