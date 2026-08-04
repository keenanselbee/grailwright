Wyrdsoul Reserve
Version 1.0.4

Wyrdsoul Reserve adds three diamond-shaped overflow vessels beside the Wyrd Power
indicator. Together they store one additional full Wyrd Power bar.

How it works
------------

- Wyrd Power gained through gameplay fills the main bar first.
- Overflow fills the lower-left reserve first, then climbs toward the upper-right.
- Each reserve holds one third of the main bar. Three full reserves double the
  total available Wyrd Power.
- The upper-right-most reserve containing charge drains first.
- Reserves cannot be activated or spent directly.
- After Wyrd Power is inactive for five seconds, stored energy pours back into
  the main bar. It does not transfer while Wyrd Power is active.
- Manually cancelling Wyrd Power preserves the main bar's remaining charge.
- Every activation costs 3% of the main bar by default to prevent free repeated
  activation effects.
- Passive regeneration produces one main bar over 20 minutes of active gameplay
  by default. Native Wyrd nights multiply that trickle by 3.
- Paused menus, loading screens, and time outside live gameplay do not regenerate
  charge. There is no offline catch-up.

The reserve state is stored inside each game save. Loading an older save restores
the reserve charge recorded in that save. New saves without Wyrdsoul Reserve data
begin empty.

HUD assets
----------

The ten placeholder frames are under:

  BepInEx/plugins/WyrdsoulReserve/reserve-icons/reserve-0.png through reserve-9.png

Frame 0 is empty and frame 9 is full. All three diamonds reuse this shared set.
Replacement art must remain transparent PNG, use the same filenames, and ideally
remain square so the configured icon size does not distort it.

Configuration
-------------

Config file:

  BepInEx/config/ks.tgfoa.wyrdsoul-reserve.cfg

Defaults:

- Enabled: true
- Activation cost: 3%
- Recharge/transfer delay: 5 seconds
- Passive main-bar recharge time: 20 minutes
- Wyrd-night passive multiplier: 3x
- Reserve gain efficiency: 100%
- Transfer time per full reserve: 0.75 seconds
- Reserve HUD offset: 0, 0
- Reserve HUD scale: 1
- Reserve icon size: 42 pixels
- Diagnostics: false

Config schema changes back up and regenerate incompatible layouts. The final
Import Previous Settings tab can conservatively recover compatible customized
values from the newest supported backup.

Compatibility
-------------

Wyrdsoul Reserve anchors its HUD root beneath the live vanilla Wyrd indicator. It
therefore inherits Glorious UI's ownership, position, scaling, fade, screen
anchor, and HUD reconstruction without moving Glorious's frame itself.

The mod patches WyrdSkillActivation and only intercepts changes to the live
hero's Wyrd duration stat. Loading and discarded-model guards prevent scene
transitions from touching stale stats. Another mod that replaces Wyrd Power
resource, activation, or save behavior may conflict.

Grail Floating Text is optional. When installed, critical startup failures are
reported in game; full details remain in the BepInEx log.

Troubleshooting
---------------

If the diamonds are missing, verify all ten reserve PNGs are installed beside
WyrdsoulReserve.dll and inspect the BepInEx log. HUD position and scale can be tuned
without changing Glorious UI's settings. Enable Diagnostics only while tracing
overflow, transfer, passive recharge, HUD attachment, or save behavior.
