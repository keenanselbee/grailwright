AMBUSH INTEGRITY
================

Version 0.1.8 - experimental stealth test mod

Ambush Integrity tests targeted fixes for unreliable stealth in Tainted
Grail: The Fall of Avalon. It preserves a briefly committed ambush against the
same target, gives the native backstab prompt modest range and timing
forgiveness, makes ordinary footsteps more conspicuous according to armor
weight, and lets truly unwitnessed lethal sneak strikes avoid the victim's
immediate alert broadcast.

0.1.7 features
--------------

- Committed Ambush: locks the exact valid backstab target for 0.45 seconds.
- Backstab forgiveness: 1.2x native interaction range and 0.18 seconds of prompt grace.
- Footstep Awareness: normal Light or unarmored, Medium, and Heavy or Overload steps use 1.2x, 1.6x, and 2.0x native noise strength. With the default native step, that is 0.60, 0.80, and 1.00.
- Clean Executions: suppresses only the dead victim's immediate hit noise and ally notification when no nearby friendly NPC can witness the strike.
- Awareness feedback: optional Searching, Detected, and Hidden Again transitions through Grail Floating Text.
- Backstab-ready API: exposes only the final, current-target eligibility state for Dishonored Dynamic Crosshair's optional dagger overlay.
- Diagnostics: decision-level BepInEx logging plus optional in-game diagnostic messages.

Scope and safety
----------------

This 0.1.7 build affects primary melee hits and normal walking or running
footstep strength. Crouched footstep noise, native noise modifiers, individual
NPC hearing, wall checks, alert buildup, sight-before-combat, and patrol or
investigation behavior remain native. The mod does not increase ranged, magic,
damage-over-time, or secondary-effect damage, and it does not rewrite quests,
cutscenes, boss encounters, perks, or combat AI.

Configuration
-------------

Config file:
  BepInEx\config\ks.tgfoa.ambush-integrity.cfg

Every gameplay experiment and its tuning can be changed independently. Defaults
are deliberately conservative. The final FoA Mod Manager section named
"Import Previous Settings" supports safe imports from compatible config backups.

FoA Mod Manager section order:

  General
  Committed Ambush
  Footstep Awareness
  Clean Executions
  Notifications
  Diagnostics
  Import Previous Settings

Grail Floating Text
-------------------

Grail Floating Text is optional. If installed, Ambush Integrity publishes
semantic awareness and ambush-result events. Without it, all gameplay behavior
and BepInEx diagnostics continue to work.

Compatibility
-------------

- BepInEx 5 Mono is required.
- Dishonored Dynamic Crosshair can show the optional backstab-ready dagger overlay without duplicating stealth rules.
- Steel and Bone composes deterministically: Ambush Integrity scales normal footstep strength by armor while Steel and Bone independently scales hearing range by difficulty. On their shared damage hook, Ambush Integrity adds any preserved sneak bonus first, then Steel and Bone applies its final player-damage and material multipliers regardless of plugin load order.
- Mods that patch the same health, backstab, or NPC alert methods may need load-order testing.

Troubleshooting
---------------

Enable "Diagnostics" in the config and reproduce one encounter. Check the
current BepInEx log for lines containing "Ambush Integrity" or "[diagnostic]".
Diagnostics record effective startup settings, throttled footstep strength,
range and backstab-target state changes, attack classification and bypass
reasons, opportunity lifecycle, witness decisions, awareness transitions, and
Grail Floating Text delivery.
They emit on decisions and transitions rather than every frame.
This is an experimental 0.1.8 build; report the enemy, location, armor tier, attack type,
whether the backstab prompt appeared, and whether another NPC had line of sight.
