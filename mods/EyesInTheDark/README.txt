Eyes in the Dark - Wyrdnight Encounters
=======================================

Version: 0.9.1

Eyes in the Dark is a timescale-aware overhaul of outdoor Wyrdnights in
Tainted Grail: The Fall of Avalon. Inspired by Wyrd Hunt, it combines
probabilistic threat, level- and region-appropriate hunters, mixed encounters,
atmospheric feedback, and extended-night pacing.

Current Features
----------------

- Continuous Wyrd Threat from 0 to 100 with four presentation stages:
  Unnoticed, Watched, Hunted, and Marked.
- First-class dynamic world-clock control with separate day and night weather
  multipliers. Defaults produce approximately 60 real minutes of day and 15
  real minutes of night without changing gameplay Time.timeScale, combat,
  animations, effects, or pause behavior.
- Passive exposed-night threat based on normalized Wyrdnight progress, so
  changing the world timescale does not multiply the passive baseline.
- Throttled activity threat from sustained sprinting or fast swimming,
  meaningful combat, confirmed melee impacts against scenery or non-damageable
  objects, eligible Wyrd kills, direct world pickups, and items taken from
  containers or corpses. Empty swings add nothing, and each attack can add at
  most one environment-impact contribution. A confirmed object impact adds
  half the configured combat-window cap and is committed after the configurable
  combat response delay.
- Moderate protected-area decay and slower active-real-time interior decay.
- Dawn reset, modest load reconstruction, and grace after loading or leaving
  an interior.
- An Eyes-owned, color-configurable Wyrd Threat meter above the vanilla Hero
  HUD. It remains visible outdoors throughout a valid Wyrdnight, including
  protected areas, and hides during daylight, interiors, loading, and
  missing-Hero states.
- The meter artwork is mirrored horizontally and vertically. When Glorious
  UI requests its versioned layout contract, Eyes remains the meter owner and
  moves the meter below the resource bars.
- Configurable three-ring Wyrd boundary with near, middle, and outer visual
  distances, independent brightness and thickness, and a native-style single
  ring fallback. Smooth bounded pulses and subtle Wyrd Threat response change
  only brightness and thickness; protection, native mask intensity, gameplay
  detection, and configured radii are never changed dynamically.
- Optional Grail Floating Text notifications with Minimal, Atmospheric, and
  Detailed presets, randomized text pools, immediate-repeat prevention, and
  pause-aware per-lane cooldowns.
- A capped square-root long-night danger-budget bonus based on the game's
  actual world-clock rate. Danger cost is spent only after a hunter's native
  placement and combat entry have been confirmed.
- Accumulated encounter hazard driven by threat, night progress, quiet time,
  eligibility, and remaining danger budget. A randomized target creates
  uncertainty without frequent independent random rolls or fixed spawn
  thresholds.
- A 50-profile level- and region-aware director: one universal Wyrdspirit
  fallback plus 49 map-specific shipped enemies cross-checked against native
  location specs, open-world scene references, and NPC-template data. Horns,
  Cuanacht, Forlorn, and Sarras each receive a varied native roster; unknown
  scenes and empty regional pools fail closed.
- Threat-weighted normal-enemy selection without spawn thresholds, plus an
  explicit greater-than-75 eligibility gate for enabled elites, immediate
  profile and family repeat penalties, and session rejection after three
  failures from the same template.
- Solo and mixed encounters with weaker-sidecar preference, level and budget
  caps, and curated two- or three-member Wyrdspirit clusters. Placement is
  atomic: if any planned member fails validation, every volatile member is
  discarded and the encounter costs zero.
- The exact primary actor owns the official hunt. Killing it resolves the hunt
  and releases surviving sidecars as ordinary enemies; unrelated creatures are
  never matched by template alone.
- One-shot Uneasy Night, Watchful Night, and Cursed Night gameplay templates.
  Applying one writes only threat and encounter tuning, then returns the
  selector to Custom without touching HUD, GFT, boundary, or diagnostics.
  Uneasy and Watchful disable elite enemies; Cursed enables reviewed elites,
  which still require Wyrd Threat greater than 75 percent.
- Protected areas, native pacifist safe zones, unrelated combat, swimming,
  travel, loading, and invalid placement prevent a hunt from spawning.
- Every spawned member must enter combat and acquire the exact Hero before the
  atomic composition is confirmed. A nearby disengaged official hunter may
  receive at most three native combat reacquisition requests, two active
  seconds apart and only while the Hero remains exposed in the same exterior.
- Killing the official hunter grants the greatest threat relief. Sustained
  outdoor escape or entering an interior grants less relief and a longer
  Recently Pursued recovery. Failed placement spends no danger budget.
- When Diagnostics is enabled, optional concise GFT System notifications show
  useful internal state, threat-source, pacing, eligible-pool, final-weight,
  composition, cost, budget, placement, and resolution summaries. Full detail
  remains in the BepInEx log, and GFT absence or failure cannot stop gameplay.
- HUD, native-boundary, and GFT presentation failures are isolated from core
  threat and encounter behavior and logged once. Exact meter text updates only
  when its rounded value changes, and boundary colors are reparsed only after
  their configured text changes.

Requirements
------------

- Tainted Grail: The Fall of Avalon on the Mono branch.
- BepInEx 5 Mono.

Plugin GUID: ks.tgfoa.eyes-in-the-dark

Configuration
-------------

Config path:
BepInEx/config/ks.tgfoa.eyes-in-the-dark.cfg

The FoA Mod Manager config title is simply "Eyes in the Dark"; the installed
package retains the full Wyrdnight Encounters name.

Defaults:

- Enabled: true
- Dynamic timescale: enabled
- Day timescale: 0.23, approximately 60 real minutes of daylight
- Night timescale: 0.413, approximately 15 real minutes of night
- Complete reference cycle: approximately 75 real minutes
- Apply gameplay preset: Custom; current defaults are Watchful Night tuning
- Passive threat per complete exposed night: 20
- Sustained sprint or fast-swim threat per minute: 4
- Maximum combat threat per short window: 2
- Combat response delay: 1.5 active real-time seconds
- Eligible Wyrd kill threat: 5
- Unique acquisition threat per item: 0.75
- Protected decay per active real-time minute: 4
- Interior decay per active real-time minute: 1
- Maximum load reconstruction by dawn: 8
- Load and interior-exit grace: 15 active real-time seconds
- Show exact threat value: false
- Threat meter color: #B878FF
- Meter offset adjustments: 0, 0 (standalone vanilla-HUD baseline: +9, -9)
- Base nightly danger budget: 30
- Long-night bonus scale: 0.35
- Maximum long-night bonus: 0.75, for a maximum total budget of 52.5
- Base/threat/night-progress hazard per minute: 0.01, 0.42, 0.08
- Randomized accumulated-hazard target: 0.85 to 1.15
- Warning duration: 6 active real-time seconds
- Curated danger costs: 8 to 44 according to native level, durability,
  combat role, elite classification, and pack safety
- Danger cost multiplier: 1.0
- Maximum encounter size: 2, further capped by player level and profile safety
- Sidecar chance: 0.55 maximum, rising smoothly with threat
- Allow elite enemies: false; when enabled, reviewed elites require threat
  greater than 75 percent and can never be sidecars
- Hunter requested spawn distance: 35 meters
- Outdoor escape: 80 meters sustained for 10 active real-time seconds
- Official hunter kill/escape threat relief: 35, 15
- Kill/escape recovery: 90, 180 active real-time seconds
- Failed-placement retry recovery: 30 active real-time seconds
- Boundary customization: enabled
- Boundary style: Three Rings
- Boundary color: #B878FF
- Boundary HDR intensity: 271.529 (vanilla-equivalent peak brightness)
- Near ring radius / brightness / thickness: 12 / 0.35 / 0.08
- Middle ring radius / brightness / thickness: 22 / 0.60 / 0.14
- Outer ring radius / brightness / thickness: 32 / 1.0 / 0.25
- Boundary threat reactivity: Subtle
- Minimum/maximum threat intensity multipliers: 1.0, 1.2
- Maximum threat thickness multiplier: 1.15
- Organic boundary pulse: enabled; amount 0.12 (configurable to 1.0);
  independent per-ring duration of 2.5 to 6 seconds before speed scaling
- GFT notifications: enabled
- GFT notification preset: Atmospheric
- Detailed exact threat: false
- GFT atmospheric cooldown: 8 active real-time seconds
- GFT diagnostic System cooldown: 3 active real-time seconds
- Diagnostics: false

The final Import Previous Settings tab reports compatible config backups and
provides a conservative one-shot import action.

Diagnostics
-----------

State transitions, threat reconstruction, stage changes, dawn resets, pacing,
hazard commitment, eligibility filters, final selection weights, encounter
composition, placement identity, budget spending, hunt resolution,
presentation failures, and accepted activity sources are written to
BepInEx/LogOutput.log. Enable Diagnostics for ten-active-second passive/decay
summaries plus input-queue and load-state detail. Discrete activity, selection,
placement, and outcome events remain immediate. If Grail Floating Text is
installed, Diagnostics also shows
concise low-priority System summaries of meaningful behind-the-scenes changes;
these use immediate delivery and do not queue stale messages through loading
or menus. Unsafe or unknown states remain Inactive and include their reason in
the transition log.

Compatibility
-------------

- Grail Floating Text is optional. It provides atmospheric notifications,
  diagnostics-only System summaries, and the soft main-menu notice when Wyrd
  Hunt and Eyes in the Dark are both loaded.
- Glorious UI is optional. Eyes remains the sole Wyrd Threat meter owner;
  Glorious requests only the below-resource-bars layout.
- Wyrd Hunt is flagged as incompatible with Eyes in the Dark. Do not run both
  night directors together.
- Custom Timescale (Nexus mod 76) is flagged as incompatible because it and
  Eyes both modify the GameRealTime weather rate. GFT shows one compatibility
  notice when both are loaded; neither mod is disabled or reconfigured.
  https://www.nexusmods.com/taintedgrailthefallofavalon/mods/76
- KS Wyrd Hunt Addon is retired and should not be installed with Eyes in the
  Dark. Its useful preset and composition ideas now live in this project.

Known Beta Limits
-----------------

- The native roster is intentionally curated. Only explicitly reviewed elites
  can be enabled; friendlies, summons, bosses, minibosses, challenge, trial,
  story, custom, arena, and hero-summon variants remain excluded.
- Wyrd Threat and active encounters do not add custom save persistence. Loading
  reconstructs only modest dawn-progress threat, and volatile spawned hunters
  are excluded from saves.
- No indoor hunts, custom enemies, rewards, bosses, unique actors, summons, or
  third-party encounter-profile framework are included.

Troubleshooting
---------------

- If the meter is missing, confirm that the player is outdoors during a valid
  Wyrdnight and not in a loading or transition state. Indoors it is hidden by
  design while threat decays slowly.
- If no hunt occurs, check protection, unrelated combat, swimming, travel,
  player level, exact map, remaining danger budget, and the BepInEx log.
- Enable Diagnostics for concise GFT System summaries and fuller log details.
  Invalid selection or placement safely costs zero.
