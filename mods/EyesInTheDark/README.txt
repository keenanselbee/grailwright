Eyes in the Dark - Wyrdnight Overhaul
=====================================

Version: 1.3.4
Platforms: Windows and Linux through Proton.

Eyes in the Dark is a timescale-aware overhaul of outdoor Wyrdnights in
Tainted Grail: The Fall of Avalon. Inspired by Wyrd Hunt, it combines
probabilistic threat, eerie ambient stalkers, level- and region-appropriate
hunters, mixed encounters, atmospheric feedback, and extended-night pacing.

Current Features
----------------

- Continuous Wyrd Threat from 0 to 100 with four presentation stages:
  Unnoticed, Watched, Hunted, and Marked.
- First-class dynamic world-clock control presented in real minutes. Defaults
  produce approximately 60 minutes of day, six minutes of night at zero
  threat, and 12 minutes at maximum threat without changing gameplay
  Time.timeScale, combat, animations, effects, or pause behavior.
- Passive exposed-night threat based on normalized Wyrdnight progress, so
  changing the world timescale does not multiply the passive baseline.
- Throttled activity threat from sustained sprinting or fast swimming,
  meaningful combat, released projectiles, successful spell casts, confirmed
  melee impacts against scenery or non-damageable objects, eligible Wyrd kills,
  direct world pickups, and items taken from containers or corpses. An arrow,
  throwable, or completed spell adds modest threat even when it misses. Empty
  melee swings add nothing, and each melee attack can add at most one
  environment-impact contribution. All combat activity shares the configured
  cap and response window.
- Moderate protected-area decay and slower active-real-time interior decay.
- Dawn reset, modest load reconstruction, and grace after loading or leaving
  an interior.
- An Eyes-owned, color-configurable Wyrd Threat meter above the vanilla Hero
  HUD. It remains visible outdoors throughout a valid Wyrdnight, including
  protected areas, and hides during daylight, interiors, loading, and
  missing-Hero states.
- Purple and Orange Wyrdness each have separate threat-meter base colors,
  target colors, constant RGB brightness, and maximum color-shift strengths.
  Independent low- and high-threat brightness scales control how strongly the
  meter brightens with threat without changing world visuals.
- The meter uses the game's neutral white bar artwork so configured Purple and
  Orange colors remain clear instead of mixing with the mana bar's blue tint.
  The artwork is mirrored horizontally and vertically. When Glorious
  UI requests its versioned layout contract, Eyes remains the meter owner and
  moves the meter below the resource bars. Its private animated material
  preserves the vanilla Hero-bar shader movement direction despite the mirror.
- A complete configurable Wyrdnight palette for the moon disc, HDR corona,
  directional and volumetric moonlight, full visible-sky tint, fueled
  protection bubble, Wyrd boundary, and threat meter. Purple Wyrdness is the
  default; Orange Wyrdness preserves the game's regional base hues.
- One palette-aware Wyrdnight Brightness setting follows the natural visual
  fade and remains independent of threat. Its default value of 1 maps Purple
  Wyrdness to a 1.75 exposure multiplier plus +0.35 EV, while Orange Wyrdness
  leaves exposure at the native 1 multiplier and 0 EV. Automatic and
  physical-camera exposure add EV, while fixed exposure subtracts it. Eyes
  applies after Light Control and does not modify HDRP post-exposure, gamma,
  colors, indirect diffuse lighting, or global volumes.
- Moon, moonlight, bubble, and boundary colors smoothly move toward a shared
  configurable world target as threat rises. Their low- and high-threat
  brightness scales and maximum color shift are independent of the meter. The
  night-sky color deliberately
  retains its selected base hue. One shared 0.8-to-1.2 threat scale controls
  visual strength across the integrated presentation.
- World lighting and palette changes caused by threat use a configurable
  two-active-second half-life by default. Gameplay threat, the meter, hunts,
  notifications, and dynamic Wyrdnight length still react immediately.
- Configurable three-ring Wyrd boundary with near, middle, and outer visual
  distances, independent brightness and thickness, and a native-style single
  ring fallback. Smooth bounded pulses remain independent of the world threat
  scale; protection, native mask intensity, gameplay detection, thickness, and
  configured radii are never changed dynamically by threat.
- Optional Grail Floating Text notifications with Minimal, Atmospheric, and
  Detailed presets, randomized text pools, immediate-repeat prevention, and
  pause-aware per-lane cooldowns. Dawn text requires a confirmed transition
  from Wyrdnight into daylight. Atmospheric and Detailed claim GFT's built-in
  night-transition event so only Eyes' nightfall and dawn text is shown. They
  also react after two or three accepted battlecries with a separate, longer
  response cooldown.
- Optional Battlecry Voice Tuner integration adds exposed-Wyrdnight threat.
  Repeated cries grant full, half, quarter, and then diminishing threat down to
  a 10 percent floor; 30 active seconds without a cry restores the full gain.
- Optional Blood Magic Expansion integration adds threat only after a corpse
  ritual completes successfully while exposed outdoors during a Wyrdnight.
  Its default 8 threat at average quality scales linearly from 4 to 12.
- A separate ambient-stalker lane between official hunts. One reviewed native
  creature can appear outside the camera, watch from a distance, follow when
  the Hero moves away, and use native Flee movement when deliberately pursued.
  It holds a wider observation buffer, waits briefly before another flee
  episode, and turns hostile if the pursuing Hero closes within 8 meters while
  it is fleeing. Official warnings, hunts, and stalkers are mutually exclusive.
- A 33-profile stalker catalog: 26 ordinary candidates below 50 threat and
  seven high-pressure Sharg, Lost Knight, Finbled Heavy, and Drowned Knight
  candidates from 50 to below 75 threat when Allow Elite Enemies is enabled.
  Player level, exact map, template safety, and three-failure session rejection
  remain hard gates; only Wyrdspirit is universal.
- Every stalker receives a hidden aggression value. Ordinary values range from
  45 to 55 threat and high-pressure values from 70 to 80. Reaching that value
  or attacking the exact stalker makes it hostile. Crowding one to 8 meters
  while it is fleeing also makes it defend itself; only an actual Hero attack
  adds the configured one-time provocation threat.
- Passive stalkers use an owned combat block and hidden HUD/compass presentation
  without changing factions, global perception, or general AI. They are
  volatile and unsaved. Spawns require native walkable verification, connected
  path graphs, Wyrd-protection rejection, and an expanded off-camera margin.
- A passive stalker can disappear only after it is sufficiently distant and
  continuously outside the camera. A hostile stalker never disappears or
  releases the encounter lane because of distance; only death, native discard,
  dawn, loading, an interior, or a scene transition ends Eyes ownership.
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
- Protected outdoor Wyrdnight areas slowly drain threat at the configured
  active-real-time rate and always permit safe rest at a fueled boundary.
  Watchful permits unprotected rest but adds a 45-to-75-percent interruption
  risk as threat rises. Uneasy adds no Eyes risk. Cursed uses 80 to 100 percent
  risk and blocks starting exposed rest after nightfall. Repeated short rests
  accumulate one shared night of exposure instead of rerolling.
- Native sleep interruptions remain authoritative. An Eyes interruption uses
  the native wake flow, queues one official hunt under the normal regional,
  level, budget, and placement rules, and locks further exposed rest until
  dawn. GFT reconciles directly to the final waking phase instead of announcing
  transitions slept through.
- Show Wyrdnight Rest Availability is enabled by default. It greys out the
  fireplace REST button when Eyes' Wyrdnight rules temporarily prevent rest.
  Disabling this presentation setting leaves the button native while the final
  accepted-rest guard and interruption rules remain active.
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
package retains the full Wyrdnight Overhaul name. Its primary sections keep
common choices concise, while detailed gameplay, stalker, boundary, visual,
and diagnostic controls are clearly labeled Advanced.

Defaults:

- Enabled: true
- Show Wyrdnight rest availability: true
- Allow unprotected Wyrdnight rest: true
- Added unprotected-rest interruption chance: 45 percent at zero threat to
  75 percent at maximum threat
- Dynamic timescale: enabled
- Day length: approximately 60 real minutes
- Quiet zero-threat night length: approximately 6 real minutes
- Maximum-threat night length: approximately 12 real minutes
- Live Wyrdnight length: interpolates from 6 to 12 minutes with current threat
- Apply gameplay preset: Custom; current defaults are Watchful Night tuning
- Passive threat per complete exposed night: 20
- Sustained sprint or fast-swim threat per minute: 4
- Maximum combat threat per short window: 2
- Combat response delay: 1.5 active real-time seconds
- Eligible Wyrd kill threat: 5
- Unique acquisition threat per item: 0.75
- Successful Blood Magic corpse ritual threat: 8 at average quality, scaling
  linearly from 4 at zero quality to 12 at maximum quality
- Protected decay per active real-time minute: 4
- Interior decay per active real-time minute: 1
- Maximum load reconstruction by dawn: 8
- Load and interior-exit grace: 15 active real-time seconds
- Show exact threat value: false
- Purple / Orange threat meter base colors: #8032FF / #FFB87A; selected by
  the active Wyrdness palette
- Purple / Orange threat meter red target colors: #FF3028 / #FF3028
- Purple / Orange threat meter brightness: 1.0 / 1.0 on a 0-to-3 range; each
  point applies 3 times the configured RGB before the meter threat scale
- Purple / Orange maximum meter color shift: 0.8 / 0.8
- Meter brightness at zero / maximum threat: 0.8 / 1.2
- Meter offset adjustments: 0, 0 (standalone vanilla-HUD baseline: +9, -9)
- Base nightly encounter budget: 30
- Long-night bonus scale: 0.35
- Maximum long-night bonus: 0.75, for a maximum total budget of 52.5
- Base/threat/night-progress hunt pressure per minute: 0.01, 0.42, 0.08
- Randomized accumulated-pressure threshold: 0.85 to 1.15
- Warning duration: 6 active real-time seconds
- Curated danger costs: 8 to 44 according to native level, durability,
  combat role, elite classification, and pack safety
- Encounter cost multiplier: 1.0
- Maximum encounter size: 2, further capped by player level and profile safety
- Additional hunter chance: 0.55 maximum, rising smoothly with threat
- Allow elite enemies: false; when enabled, reviewed high-pressure stalkers
  use the 50-to-below-75 band, while reviewed official elites still require
  threat greater than 75 percent and can never be sidecars
- Hunter requested spawn distance: 35 meters
- Outdoor escape: 80 meters sustained for 10 active real-time seconds
- Official hunter kill/escape threat relief: 35, 15
- Kill/escape recovery: 90, 180 active real-time seconds
- Failed-placement retry recovery: 30 active real-time seconds
- Ambient stalkers: enabled
- Ambient cooldown: randomized 55 to 165 active seconds at zero threat; the
  upper bound shrinks smoothly to 70 seconds as threat approaches 50
- Stalker provocation threat: 6, applied once to the exact attacked stalker
- Stalker requested spawn distance: 45 to 70 meters
- Passive stalker disappearance: at least 65 meters away and continuously
  outside the camera for 2.5 active seconds
- Boundary customization: enabled
- Boundary style: Three Rings
- Boundary color: #B878FF
- Boundary brightness: 1.0 (internally preserves the vanilla-equivalent HDR
  peak)
- Near ring radius / brightness / thickness: 10 / 0.05 / 0.25
- Middle ring radius / brightness / thickness: 20 / 0.05 / 0.25
- Outer ring radius / brightness / thickness: 30 / 0.05 / 0.25
- Organic boundary pulse: enabled; amount 0.8 (configurable to 1.0);
  independent per-ring duration of 2.5 to 6 seconds before speed scaling
- Wyrdnight visuals: enabled
- Natural visual transition: 60 active real-time seconds. Dusk fades the
  integrated environment and protection-bubble palette from 30 seconds before
  nightfall until 30 seconds after nightfall;
  dawn fading begins during the final 60 real seconds of the Wyrdnight and
  finishes at dawn. Gameplay, world-clock phase, meter visibility, and
  protection state remain immediate.
- Wyrdness palette: Purple Wyrdness; Orange Wyrdness is available
- Wyrdnight brightness: 1; configurable from 0 to 2. At 1, Purple uses 1.75x
  exposure plus +0.35 EV and Orange retains native 1x exposure with 0 EV
- World brightness at zero / maximum threat: 0.8 / 1.2
- Threat lighting smoothing half-life: 2 active real-time seconds
- World threat target color / maximum world color shift: #FF3028 / 0.8
- Moon surface color / tint / HDR intensity: #3200FF / 0.75 / 2
- Moon corona: enabled; color / intensity: #8000FF / 2
- Directional and volumetric moonlight color / tint: #7E47FF / 0.9
- Full Wyrdnight sky tint: enabled; #401C63 / 1.0. This layer uses the sky
  material's _SkyTint property, keeps its configured tint strength while its
  brightness follows the world threat-brightness scale, and never shifts
  toward red. It does not directly alter fog, clouds, terrain lighting, or
  reflections.
- Fueled protection-bubble tint: enabled; #B050FF; body/border intensity 1 / 1
- GFT notifications: enabled
- GFT Wyrd color group: Purple by default; switches live to Orange with the
  Orange Wyrdness palette
- GFT notification preset: Atmospheric
- Detailed exact threat: false
- GFT atmospheric cooldown: 8 active real-time seconds
- GFT battlecry-response cooldown: 15 active real-time seconds
- GFT diagnostic System cooldown: 1 active real-time second
- Diagnostics: false
- Show GFT diagnostics: true; inactive while Diagnostics is false
- Diagnostic threat override: disabled; override value 0
- Diagnostic timescale override: disabled; multiplier 1.0

The final Import Previous Settings tab reports compatible config backups and
provides a conservative one-shot import action.

Version 1.3.1 regenerates configuration because OwnRestMenu and
RestClockLabelFormat were removed when clock and time presentation moved to
Glorious UI, and ShowWyrdnightRestAvailability now controls only the fireplace
REST-button state. Other compatible durable customizations remain eligible for
conservative recovery.

Version 1.1.4 regenerates configuration because PurpleWyrdnessBrightness was
removed and the existing diagnostic GFT System cooldown default changed from
3 seconds to 1 second. Compatible durable customizations remain eligible for
conservative recovery; the retired brightness value is intentionally skipped.

Version 1.0.7 regenerates configuration because the quiet and maximum-threat
Wyrdnight defaults changed from 5/15 to 6/12 minutes and the raw
BoundaryHdrIntensity setting was replaced by normalized BoundaryBrightness.
Compatible durable customizations remain eligible for conservative recovery;
the retired raw boundary value is intentionally not imported.

Version 1.0.6 regenerates configuration because the existing
BattlecryResponseCooldownSeconds default changed from 45 to 15 active seconds.
Genuine customized values remain eligible for conservative recovery.

Version 0.9.8 regenerates configuration because the former DayTimescale and
NightTimescale multipliers were replaced by real-minute day, base-night, and
maximum-threat-night durations. Other compatible durable customizations are
preserved, while the new diagnostic override remains safely disabled.

Version 0.9.7 regenerated configuration because NightSkyAmbientColor now
controls the complete visible-sky tint rather than the narrow night-texture
tint. Compatible customized color and strength values remain eligible for
restoration. Version 0.9.6 regenerated configuration because the former boundary-only
threat toggle, intensity range, and thickness escalation were replaced by the
shared Wyrd visual response. Other compatible durable customizations are
preserved. The retired Purple Moon Test config is not imported.

Diagnostics
-----------

State transitions, threat reconstruction, stage changes, dawn resets, rest
safety and post-sleep reconciliation, pacing,
hazard commitment, official and ambient eligibility filters, final selection
weights, hidden aggression, movement transitions, camera-safe placement,
encounter composition, placement identity, budget spending, hunt resolution,
presentation failures, and accepted activity sources are written to
BepInEx/LogOutput.log. Enable Diagnostics for ten-active-second passive/decay
summaries plus input-queue and load-state detail. Discrete activity, selection,
placement, and outcome events remain immediate. If Grail Floating Text is
installed, Diagnostics also shows
concise low-priority System summaries of meaningful behind-the-scenes changes;
these use immediate delivery and do not queue stale messages through loading
or menus. Unsafe or unknown states remain Inactive and include their reason in
the transition log.

Override World Timescale is a diagnostics-only testing control. When enabled,
it replaces normal dynamic day and Wyrdnight timing with a fixed multiplier of
the vanilla world clock. A value of 1 is vanilla speed, 2 is twice as fast, and
0.5 is half speed. It never changes combat, animation, effect, or pause speed.

Compatibility
-------------

- Grail Floating Text is optional. It provides atmospheric notifications,
  diagnostics-only System summaries, and the soft main-menu notice when Wyrd
  Hunt and Eyes in the Dark are both loaded. Atmospheric and Detailed replace
  GFT's built-in Wyrdnight falls/fades text while active; Minimal and disabled
  Eyes notifications leave GFT's default messages available. Minimal remains
  focused on official hunts; Atmospheric adds only witnessed stalker
  disappearances; Detailed adds sightings, retreats, and escalation flavor
  without revealing a stalker's hidden aggression value.
- Glorious UI is optional. Eyes remains the sole Wyrd Threat meter owner;
  Glorious requests its below-resource-bars layout and can independently
  provide a noon-at-top rest clock with matching time formatting.
- Blood Magic Expansion is optional. Successfully completed exposed-Wyrdnight
  corpse rituals add quality-scaled threat; incomplete or rejected rituals do
  not report completion.
- Wyrd Hunt is flagged as incompatible with Eyes in the Dark. Do not run both
  night directors together.
- Custom Timescale (Nexus mod 76) is flagged as incompatible because it and
  Eyes both modify the GameRealTime weather rate. GFT shows one compatibility
  notice when both are loaded; neither mod is disabled or reconfigured.
  https://www.nexusmods.com/taintedgrailthefallofavalon/mods/76
- KS Wyrd Hunt Addon is retired and should not be installed with Eyes in the
  Dark. Its useful preset and composition ideas now live in this project.
- Purple Moon Test is fully integrated and retired. Do not install its
  standalone DLL alongside Eyes in the Dark.

Known Beta Limits
-----------------

- The native roster is intentionally curated. Only explicitly reviewed elites
  can be enabled; friendlies, summons, bosses, minibosses, challenge, trial,
  story, custom, arena, and hero-summon variants remain excluded.
- Wyrd Threat and active encounters do not add custom save persistence. Loading
  reconstructs only modest dawn-progress threat, and volatile spawned hunters
  and stalkers are excluded from saves.
- No indoor hunts, custom enemies, rewards, bosses, unique actors, summons, or
  third-party encounter-profile framework are included.

Troubleshooting
---------------

- If the meter is missing, confirm that the player is outdoors during a valid
  Wyrdnight and not in a loading or transition state. Indoors it is hidden by
  design while threat decays slowly.
- If no hunt occurs, check protection, unrelated combat, swimming, travel,
  player level, exact map, remaining danger budget, and the BepInEx log.
- If no stalker appears, check the 50/75 threat bands, Allow Elite Enemies,
  current official-hunt lane, player level, exact map, protection, combat, and
  diagnostics. Failed off-camera or path-connected placement is safely retried.
- Enable Diagnostics for concise GFT System summaries and fuller log details.
  Invalid selection or placement safely costs zero.
