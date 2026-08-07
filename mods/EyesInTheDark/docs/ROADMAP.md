# Eyes in the Dark 1.3.0 Implementation Roadmap

## Objective

Reach a hardened, user-testable `1.3.0` release candidate of **Eyes in the Dark -
Wyrdnight Overhaul** without expanding beyond the product rules in
[DESIGN.md](DESIGN.md).

The roadmap advances through narrow vertical slices. Each milestone must compile
and satisfy its automated contracts before the next begins. Consolidated
in-game acceptance begins only after the `1.3.0` implementation is complete.
Patch releases may fix a milestone, but authored patch versions must remain
below 10; roll to the next minor version instead of using an `X.Y.10` version.

## Working principles

- Keep one plugin assembly and direct classes. Introduce an interface or public
  API member only for a concrete test seam or integration.
- Research and validate exact game state, event, and spawn routes before using
  them in gameplay.
- Prefer native game behavior for placement, combat, loot, and actor lifecycle.
- Fail closed: unknown night, region, hero, template, or placement
  state produces no encounter.
- Do not spend danger budget or reduce threat until a spawn or resolution is
  confirmed.
- Keep gameplay, HUD, boundary visuals, GFT presentation, and integrations
  independently recoverable when an optional part fails.
- Do not add custom save data, custom rewards, custom enemies, indoor hunts,
  broad AI ownership, or compatibility layers before 0.8.3.
- Run focused automated contracts and a clean build after each implementation
  milestone. Do not substitute deferred in-game testing for these checks.
- Treat each milestone's in-game Verification list as an accumulated `0.8.3`
  acceptance matrix. Do not interrupt feature implementation for user smoke
  testing unless the user explicitly requests an earlier diagnostic build.
- After the `0.8.3` feature implementation is complete, stage the consolidated
  user-test build to Vortex, inspect its package, and run the accumulated
  in-game matrices before declaring the beta complete.
- Do not publish or update Nexus unless explicitly requested.

## Milestone summary

| Version | Milestone | Playable outcome |
| --- | --- | --- |
| 0.1.0 | Scaffold | Plugin/package and living design documents exist. |
| 0.2.0 | Night-state foundation | The mod reliably understands when and where it may operate. |
| 0.3.0 | Wyrd Threat vertical slice | Threat rises/decays correctly and the outdoor-night meter works. |
| 0.4.0 | Atmosphere and adaptive pacing | GFT, boundary visuals, and extended-night pacing work without encounters. |
| 0.5.0 | First official hunt | One safe solo encounter completes the full warning-to-recovery loop. |
| 0.6.0 | Curated director | Level/region roster weighting, mixed encounters, and gameplay presets work. |
| 0.7.0 | HUD integration and hardening | Glorious placement, transition resilience, and final config/API surfaces work. |
| 0.8.3 | Dynamic clock and hardening | The initial duration-aware clock, expanded regional roster, and runtime hardening are validated. |
| 0.9.0 | Native roster data pass | Scene-backed regional variety and opt-in high-threat elites are validated. |
| 0.9.2 | Ambient stalkers | Separate suspense lane, native stalking movement, hidden escalation, safe camera lifecycle, and GFT routing are validated. |
| 0.9.3 | Ambient hardening candidate | Provocation accounting and the complete ambient acceptance candidate are validated. |
| 0.9.4 | Pursuit consequences and safe rest | Close pursuit has consequences, protected rest uses native safety, and sleep atmosphere reconciles cleanly. |
| 0.9.5 | Tested visual defaults | The approved meter and layered-boundary presentation becomes the schema-reset baseline. |
| 0.9.6 | Unified Wyrdnight visuals | Purple Moon Test is integrated and one threat response drives world, boundary, and HUD visuals. |
| 0.9.7 | Full-sky tint correction | The palette controls the complete visible Wyrdnight sky without conflicting with Light Control. |
| 0.9.8 | Threat-drawn Wyrdnights | User-facing minute settings, threat-stretched nights, palette-matched GFT, and diagnostic threat control are validated. |
| 0.9.9 | Product identity restoration | Display, assembly, Nexus, and release ZIP identity use Wyrdnight Overhaul. |
| 1.0.0 | Rest gate and visual transitions | Exposed Wyrdnight rest is blocked before the clock screen, and natural dusk/dawn presentation fades smoothly. |
| 1.0.1 | Readable rest clock | The native 24-hour clock clearly labels its orientation and shows the exact palette-matched Wyrdnight sector without changing input or rest behavior. |
| 1.0.2 | Native disabled-rest presentation | Exposed Wyrdnight REST is greyed out without warning spam, protected rest remains available, and an opt-out setting is available. |
| 1.0.3 | Pre-dawn fade and ranged noise | The Wyrd palette finishes fading at dawn, while released projectiles and successful spells create threat without weakening melee hit requirements. |
| 1.0.4 | Rest UI and visual continuity | Exposed REST is disabled at the actual control, the clock uses neutral configurable labels, and short rest/load transitions do not flash vanilla lighting. |
| 1.0.5 | Battlecry response | Optional battlecries add diminishing Wyrd Threat and restrained, separately cooled Wyrdnight atmosphere. |
| 1.0.6 | Clock and visual polish | Quick-use time follows the clock-format preference, dial icons move inside, dusk is centered on nightfall, loaded threat color ramps in, purple brightness is configurable, and battlecry responses use a 15-second default cooldown. |
| 1.0.7 | Config UX and clock baseline | Quiet/max-threat nights use 6/12 minutes, boundary brightness is normalized, and common settings lead clearly labeled advanced tuning. |
| 1.0.8 | Noon-first clock and performance pass | The rest clock uses a complete noon-at-top mapping, popup times follow the selected format, and the Wyrdnight hot paths avoid redundant work. |
| 1.0.9 | Preset-driven risky rest | Unprotected sleep uses cumulative native-integrated interruption risk, can commit one official hunt, and has optional rest-menu ownership. |
| 1.1.0 | Mirrored meter animation correction | Meter artwork stays mirrored while its animated texture retains the vanilla resource-bar direction without changing shared materials. |
| 1.1.1 | Diagnostic timescale override | A fixed vanilla-clock multiplier accelerates transition testing without touching Unity gameplay time or weakening clock ownership safety. |
| 1.1.2 | Palette-preserving brightness | Threat scales selected-color brightness instead of weakening tint, while the original sky emission remains game-owned. |
| 1.1.4 | Exposure and rest-clock correction | Purple uses fixed exposure, the retired brightness control is removed, rest-clock rotation is idempotent, and diagnostics react sooner. |
| 1.1.7 | Mode-aware Purple brightness | Purple adds configurable +0.35 EV exposure compensation after Light Control without touching HDRP post-exposure, gamma, colors, or global volumes. |
| 1.1.8 | Purple indirect diffuse tuning | Purple applies a configurable 1.10 multiplier to the native indirect diffuse result without changing direct or reflected lighting. |
| 1.1.9 | Exposure control and meter cleanup | The 1.2 Purple exposure multiplier becomes configurable, and the ineffective TextureScroller correction is removed pending targeted shader diagnostics. |
| 1.2.0 | Diagnostic hardening candidate | Purple lighting controls refresh the concise diagnostic state, and the acceptance matrix targets the current Battlecry integration. |
| 1.2.1 | Portable packaging | Release archives install correctly through Windows, Vortex, and Linux/Proton paths. |
| 1.2.2 | Threat lighting smoothing | Sudden threat changes ease into world lighting at the existing bounded visual cadence while gameplay and HUD threat remain immediate. |
| 1.2.4 | Palette-aware brightness | One brightness setting maps Purple to 1.75x/+0.35 EV and Orange to native exposure at 1, indirect diffuse stays native, and the meter selects a palette-specific base color. |
| 1.2.5 | Mirrored shader animation correction | The mirrored threat meter preserves the vanilla Hero-bar shader movement direction through an Eyes-owned private material. |
| 1.2.6 | Palette-specific meter brightness | Purple and Orange threat meters expose independent brightness multipliers while retaining the prior 1.5 default. |
| 1.2.7 | Recalibrated meter brightness | Purple and Orange meter brightness controls retain a 0-to-3 range while each point applies 3x RGB. |
| 1.2.8 | Neutral palette-owned meter colors | The meter removes inherited mana blue, and Purple and Orange each own separate base and red target colors. |
| 1.2.9 | Independent world and meter response | World and HUD brightness ranges, target colors, and maximum color shifts are configured independently. |
| 1.3.0 | Complete HUD response independence | The world visual master toggle no longer suppresses independently configured meter behavior. |

## 0.1.0 - Scaffold

Status: complete.

Delivered:

- `EyesInTheDark` package identity and plugin GUID.
- Inert BepInEx plugin with no patches or config.
- Installed-user README and changelog.
- Living design document.
- Repository build, export, and Vortex staging proof.

No gameplay claim belongs to this version.

## 0.2.0 - Night-state foundation

Status: complete. The clean 0.2.2 startup, complete 0.2.3 state matrix, and
0.2.4 diagnostic cleanup/travel-priority smoke were validated in game. The
final fast-travel trace classified the overlap as `Travel` and showed no
active-real-time catch-up.

Purpose: prove the world-state and time inputs before changing gameplay.

Implement:

- Current playable-hero availability.
- Outdoor versus indoor state.
- Valid Wyrdnight versus daylight.
- Protected versus exposed outdoor state.
- Loading, portal, fast-travel, title, death, and scene-transition suppression.
- World-time sampling and normalized Wyrdnight progress.
- Active-real-time clock that stops when gameplay is paused.
- Direct runtime states: `Inactive`, `Roaming`, `Warning`, `ActiveHunt`, and
  `Recovery`, with only `Inactive` and `Roaming` used initially.
- Low-noise transition diagnostics showing why the director is active or
  inactive.

Do not implement:

- Threat changes.
- HUD or boundary changes.
- GFT gameplay messages.
- Enemy selection or spawning.

Exit criteria:

- Outdoor Wyrdnight enters `Roaming` only with a playable hero.
- Daylight, interiors, title/loading scenes, and unknown state remain inactive.
- Pausing does not advance active real time.
- Portal, rest, fast travel, load, and scene changes produce no catch-up work.
- A continuous full day/night transition log shows no false outdoor-night
  activation.

Verification:

- Build and package validation.
- Main menu, new/load game, outdoor day, outdoor Wyrdnight, protected outdoor,
  interior, pause, portal, fast travel, death, and reload checks.
- Diagnostic review for repeated logs or per-frame allocations.

## 0.3.0 - Wyrd Threat vertical slice

Status: implementation, deterministic contracts, build/package inspection,
and Vortex staging complete; the in-game visibility/activity/decay smoke
remains to be validated.

Purpose: make the central resource observable and correctly paced without any
enemy spawning.

Implement:

- Wyrd Threat state from 0 to 100.
- Passive threat based on normalized Wyrdnight progress.
- Proven activity sources, added one at a time: sustained sprint/fast swim,
  meaningful combat events, confirmed melee environment impacts, eligible Wyrd
  kills, and eligible acquisition events.
- Per-source throttling, aggregation, and immediate-repeat protection.
- Moderate decay while protected outdoors.
- Slow active-real-time decay indoors while encounter generation remains off.
- Dawn reset.
- Modest threat reconstruction from current night progress after loading, plus
  a load grace period.
- Threat stages: Unnoticed, Watched, Hunted, and Marked.
- Eyes in the Dark-owned Wyrd Threat meter above the vanilla Hero HUD.
- Meter always visible outdoors during a valid Wyrdnight, including while
  protected, and hidden during daylight, indoors, loading, and missing-hero
  state.
- Optional exact value display and basic offsets.
- First config schema, shared previous-settings recovery infrastructure, and
  conservative config organization.

Do not implement:

- Encounter probability or spawning.
- Mixed packs.
- Glorious repositioning.

Exit criteria:

- The same passive baseline is reached across equivalent full Wyrdnights at
  vanilla and slower world timescales.
- Activity adds threat only through proven events and cannot be trivially
  spammed.
- Entering an interior hides the meter and slowly decays threat without
  resetting it.
- Leaving an interior restores the meter with the current value and a grace
  period.
- Dawn resets the meter and nightly state.
- Config recovery and preservation contracts pass.

Verification:

- Build, Vortex stage, and package inspection.
- Threat-source tests with source-specific logs.
- Outdoor/protected/indoor/daylight visibility matrix.
- World-clock checks at 60-minute daylight and the 5/10/15-minute threat endpoints.
- Config recovery and preservation contract scripts.

## 0.4.0 - Atmosphere and adaptive pacing

Status: implementation, deterministic contracts, clean build, and package
inspection complete. The accumulated GFT, boundary, pause, and long-night
in-game matrix is deferred to the consolidated 0.8.3 candidate pass.

Purpose: establish the non-combat night experience and tune the clocks before
introducing hunters.

Implement:

- Configurable single- or three-ring Wyrd boundary color, HDR intensity,
  per-ring visual radius, brightness, and thickness with documented purple
  defaults.
- Restrained threat-reactive intensity and thickness plus independent smooth,
  bounded pulses.
- GFT optional integration using the shared Wyrd icon and three notification
  presets: Minimal, Atmospheric, and Detailed.
- Randomized built-in text pools by event category with immediate-repeat
  prevention.
- Separate night, threat, and hunt collapse lanes; the hunt lane remains unused
  until 0.5.0.
- Night begin/end, upward stage, downward stage, protection, and major-surge
  routing according to the selected GFT preset.
- Base nightly danger-budget calculation and capped sublinear long-night bonus,
  logged for diagnostics but not spent.
- Active-real-time notification cooldowns that stop while paused.
- Diagnostics-only GFT System summaries on meaningful runtime transitions,
  committed threat-source aggregates, danger-budget initialization, director
  decisions, encounter commitment/failure, and hunt resolution. At this
  milestone the encounter-related routes may remain unused.
- One low-priority `eyes-in-the-dark-diagnostics` collapse lane with immediate
  delivery, concise exact values, repeat suppression, and an active-real-time
  cooldown. Never queue stale diagnostic messages through loading or menus.

Do not implement:

- Enemy spawning.
- Fake hunt warnings.
- Exact encounter probabilities in player-facing text.

Exit criteria:

- GFT absence or API failure does not affect threat, HUD, or boundary behavior.
- Each event pool avoids immediate repeats and respects its notification preset.
- Atmospheric text does not fire per threat point or ordinary repeated action.
- Diagnostics off produces no diagnostic GFT messages. Diagnostics on reports
  useful state changes and decisions without reporting every poll, threat point,
  filtered candidate, or repeated identical reason.
- Boundary radius remains visual-only and never changes dynamically with threat.
- A ten-times-longer Wyrdnight receives only the documented capped budget bonus.

Verification:

- GFT present/absent and Minimal/Atmospheric/Detailed matrix.
- Diagnostics on/off, System collapse, repeat suppression, immediate-only
  delivery, and GFT API-failure isolation checks.
- Boundary single/layered restoration, defaults, per-ring custom values,
  pulse bounds, and threat-reactivity checks.
- Pause and long-night timing checks.
- Config recovery and package validation.

## 0.5.0 - First official hunt

Status: implementation, deterministic contracts, clean Eyes/GFT builds, and
package inspection complete. The full native placement, kill, escape,
transition, and timescale matrix is deferred to the consolidated 0.8.3
candidate pass.

Purpose: complete one safe encounter loop before building a roster.

Implement:

- Accumulated encounter hazard with a randomized target.
- `Roaming -> Warning -> ActiveHunt -> Recovery -> Roaming` transitions.
- One curated, non-unique, level-safe, region-valid native hunter profile.
- A proven native outdoor placement and combat route.
- Placement distance, protected-area, settlement, loading, hero, and existing
  combat gates.
- Warning feedback without exposing the enemy identity or exact probability.
- One official primary target and volatile encounter identity.
- Resolution for official hunter death.
- Sustained escape resolution using tested distance and duration.
- Interior entry as escape, not a kill.
- Dawn, death, reload, invalid target, and failed placement handling.
- Greatest threat reduction for a kill; smaller reduction and longer Recently
  Pursued recovery for escape.
- Spend danger budget only after confirmed successful placement.
- GFT hunt-committed, hunter-killed, and escape text pools.
- The exact Wyrd Hunt/Eyes in the Dark incompatibility notice in GFT using only
  GFT's existing soft main-menu System convention.

Do not implement:

- Additional enemy families.
- Mixed encounters.
- Custom rewards or cleanup ownership.
- Any compatibility behavior inside Eyes in the Dark.

Exit criteria:

- No more than one official hunt can exist.
- A failed or invalid spawn spends no budget and grants no relief.
- Normal unrelated combat defers hunt commitment.
- Kill and escape produce visibly different threat and recovery outcomes.
- Loading and transitions cannot create an instant or duplicate hunt.
- GFT alone reports the exact incompatible loaded pair; neither plugin is
  disabled or altered.

Verification:

- Successful spawn, failed spawn, target death, sustained escape, doorway
  escape, dawn, player death, portal, save/load, and scene-change scenarios.
- Five-minute quiet and 15-minute maximum-threat hunt-cadence comparison.
- GFT incompatibility notice with both plugins loaded and no notice otherwise.

## 0.6.0 - Curated director

Status: implementation, deterministic contracts, config recovery and
preservation contracts, clean build, and package inspection complete. The
regional/level roster, preset/timescale, mixed-placement, primary/sidecar, and
GFT diagnostics in-game matrix is deferred to the consolidated 0.8.3 candidate
pass.

Purpose: expand the proven loop into the intended varied night director.

Implement:

- Curated hunter catalog for supported maps.
- Strict region eligibility plus explicitly reviewed universal Wyrd candidates.
- Minimum player-level and regional gates for every candidate.
- Danger cost, family, solo/primary/sidecar weights, pack limits, and safety
  flags.
- Weighted primary selection driven by threat without hard spawn thresholds.
- Immediate candidate repeat reduction and same-family history penalties.
- Session rejection for repeatedly failing templates.
- Mixed encounters with weaker-sidecar preference, same-tier and same-family
  penalties, and level/preset/budget pack-size caps.
- Curated Wyrdspirit cluster behavior.
- Primary death resolves the official hunt; surviving sidecars become ordinary
  enemies and do not retain the director lock.
- One-shot gameplay templates: Uneasy Night, Watchful Night, and Cursed Night,
  returning the selector to Custom after application.
- Diagnostics for hard-filter reasons, final weights, composition, cost, and
  budget.

Exit criteria:

- Early-level characters cannot receive candidates or pack sizes above their
  configured safety ceiling.
- Strict regional selection never imports an unapproved map-specific enemy.
- High threat raises stronger and larger encounter probability only inside the
  eligible pool.
- No candidate or family repeats excessively across representative nights.
- Presets change gameplay tuning only and preserve HUD, GFT, boundary, and
  diagnostic preferences.
- Empty eligible pools safely skip the encounter.

Verification:

- Early-, mid-, and late-level profiles on every supported map.
- Each preset at the default five-minute quiet and 15-minute maximum night.
- Solo, two-member, larger allowed pack, Wyrdspirit cluster, failed sidecar,
  primary death, and surviving-sidecar scenarios.
- Config recovery, build, stage, and package checks.

## 0.7.0 - HUD integration and hardening

Status: implementation, ownership and duplicate-removal contracts, optional
presentation failure isolation, allocation/log-noise review, clean Eyes and
Glorious builds, and package inspection complete. The HUD rebuild,
resolution/UI-scale, optional-integration presence, and long-night runtime
matrix is deferred to the consolidated 0.8.3 candidate pass.

Purpose: finish ownership boundaries and make the complete system resilient.

Implement:

- Small versioned Eyes in the Dark HUD API required by Glorious UI.
- Glorious detection and request for below-bars placement while Eyes in the
  Dark retains meter creation, updates, visibility, and cleanup.
- Automatic fallback to above-vanilla-HUD placement when the integration is
  absent or fails.
- Removal of the duplicate Wyrd meter implementation from Glorious UI.
- Only the minimum public status snapshot/events proven necessary by actual
  integrations; no generalized modding framework.
- Final configuration descriptions, acceptable ranges, reset reasons, recovery
  rules, and permanent exclusions.
- Allocation and log-noise pass across the night-state, threat, director, HUD,
  boundary, and GFT loops.
- Failure isolation so optional HUD, boundary, or GFT failures cannot stop core
  threat and encounter behavior.

Exit criteria:

- Meter appears above the vanilla Hero HUD without Glorious and below the bars
  when Glorious owns layout.
- Toggling relevant Glorious layout state does not create duplicate meters.
- Integration failure restores the default layout.
- No duplicate Wyrd meter implementation remains active in Glorious.
- A full long night produces no repeated exceptions, per-frame log spam, or
  material periodic allocations from the mod's own loops.

Verification:

- Glorious present/absent, integration success/failure, HUD rebuild, scene
  transition, resolution change, and UI scale checks.
- GFT present/absent alongside both HUD layouts.
- Config contracts, compilation of both affected mods, Vortex staging, and
  package inspection.

## 0.8.3 - Dynamic timescale, regional roster, and hardening

Status: feature implementation, deterministic and randomized acceptance
contracts, shared config contracts, clean Eyes/GFT builds, package
inspection, and Vortex staging complete. The staged `0.8.3` candidate now enters
the consolidated in-game pass below; this milestone and the overall goal remain
incomplete until that pass succeeds and any findings are fixed and retested.

Purpose: make dynamic world-clock ownership first class, expand the native
regional roster, harden encounter ownership, and package the agreed feature set.

Implement and finish:

- Dynamic `GameRealTime` weather-rate ownership presented as a 60-minute day,
  5-minute zero-threat night, and 15-minute maximum-threat night.
- Expanded Cuanacht, Forlorn, and Sarras candidate rosters and mappings.
- Exact post-spawn Hero combat confirmation and bounded reacquisition.
- Listener retry backoff and aggregated continuous-threat diagnostics.
- Removal of dormant progression-tier eligibility plumbing.
- Final initial player-level gates and pack-size ceilings.
- Final hazard curves, danger costs, threat gains/decays, kill relief, escape
  relief, warning duration, and recovery timing.
- Final Uneasy Night, Watchful Night, and Cursed Night templates.
- Final randomized GFT pools and notification-preset routing.
- Final concise diagnostics-only GFT System summaries for runtime state, threat,
  pacing budget, director decisions, selection/spawn failures, encounter
  commitment, and resolution.
- Final default threat-meter and Wyrd-boundary presentation.
- Defensive handling for missing hero, unknown world state, invalid candidate,
  failed placement, lost target, loading, fast travel, portal, death, dawn, and
  save/load.
- Installed-user README, changelog, Nexus metadata copy, and troubleshooting
  based only on verified behavior.
- Deprecation messaging for the retired companion addon in repository/Nexus
  documentation only; no runtime migration or compatibility layer.

### 0.8.3 acceptance criteria

Gameplay:

- Wyrd Threat responds to meaningful player behavior and cannot be trivially
  farmed by repeated low-value input.
- Threat always uses that name in UI, config, GFT, logs, and public surfaces.
- Outdoor Wyrdnights contain suspense and recovery rather than uninterrupted
  sequential spawns.
- Across the default 60-minute day and 6-to-12-minute night, Watchful pacing completes coherently;
  at `0.1`, total pressure increases only within the preset's
  capped long-night design and remains playable.
- Killing an official hunter provides the largest relief; escape provides less
  relief and longer residual pursuit.
- Invalid encounters spend no budget.

Safety and selection:

- New characters are protected by tested level and pack-size ceilings.
- Stronger candidates become available gradually as the player levels.
- Map-specific enemies remain on approved maps; unknown or empty mappings fail
  closed.
- Only curated non-unique native candidates can be selected.
- One official hunt exists at a time.

Presentation:

- The threat meter is always visible outdoors during Wyrdnight and hidden
  during daylight, indoors, loading, and missing-hero state.
- Indoor threat decays slowly and reappears correctly when returning outdoors.
- Glorious repositions the Eyes-owned meter without duplicating it.
- Boundary customization is fully configurable and remains visual-only.
- Minimal, Atmospheric, and Detailed GFT presets route randomized text without
  immediate repeats or message spam.
- Diagnostics produces concise low-priority GFT System summaries with exact
  behind-the-scenes values, while disabled diagnostics produces none and a GFT
  failure cannot affect gameplay.

Reliability:

- The default day, zero-threat night, and maximum-threat night measure within
  0.5 real minute of 60, 5, and 15.
- Dynamic clock switching, live config changes, load/time-skip handling,
  safe disable restoration, and external-override protection pass.
- Live threat and duration changes do not produce repeated clock setters,
  catch-up threat, or linear encounter multiplication.
- Early-, mid-, and late-level scenarios pass on every supported map.
- Day/night, protected/exposed, indoor/outdoor, pause, portal, fast travel,
  death, dawn, save/load, and scene-transition scenarios pass.
- GFT and Glorious presence/absence matrices pass.
- Config recovery and preservation contracts pass.
- Build and package checks pass with one top-level package folder and no source,
  tools, design docs, publishing metadata, or other repository-only files.
- Known residual risks are documented honestly.

### Consolidated in-game test pass

Begin this pass only after the `1.3.0` implementation, automated contracts, and
clean build are complete. Execute every accumulated milestone Verification
matrix against the same candidate build, recording failures and fixes. Rebuild
and repeat affected scenarios after a fix; do not mark the goal complete merely
because all planned features have been written.

Use [TEST-MATRIX.md](TEST-MATRIX.md) as the candidate checklist and record.

## 0.9.0 - Native roster data pass

Status: implementation, isolated Eyes contracts, shared config recovery and
preservation contracts, clean compile, package inspection, and Vortex staging
complete. The consolidated in-game matrix remains pending against the staged
`0.9.0` candidate.

Purpose: replace the small speculative roster with a broad but explicit
allowlist grounded in shipped open-world references and resolved NPC data.

Implemented scope:

- Cross-check every selected profile against the Addressables catalog,
  location-spec bundle, open-world scene references, and extracted NPC-template
  classification and combat statistics.
- Expand the catalog to one universal Wyrdspirit plus 49 map-specific profiles.
- Replace the generic Sarras Wyrdspawn assumptions with actual Sarras Drowner,
  Drowned, Finbled, Tadpole, Wailcap, Tidewraith, and Drowned Knight specs.
- Add `AllowEliteEnemies`, default off. Elites require threat strictly greater
  than `75`, are never sidecars, and retain one-copy limits.
- Make Uneasy and Watchful write elite permission off and Cursed write it on.
- Retain unconditional rejection of bosses, minibosses, friendlies, summons,
  unique/story actors, challenge, trial, custom, arena, and hero-summon specs.
- Recalibrate affected level gates and costs from resolved actor data rather
  than location-spec tier names alone.

Acceptance additions:

- Exact normal and elite gates, costs, roles, region restrictions, and
  one-copy limits pass deterministic contracts.
- At `75` threat every elite is filtered; above `75`, only enabled, level-safe,
  affordable elites from the current map enter the pool.
- Each new native profile places, acquires the Hero, resolves, and cleans up in
  its mapped exterior without a budget leak or transition failure.
- The consolidated [TEST-MATRIX.md](TEST-MATRIX.md) passes against the staged
  `0.9.0` build before the release is considered complete.

## 0.9.2 - Ambient stalker suspense lane

Status: implementation, automated verification, clean packaging, package
inspection, and Vortex staging are complete for `0.9.3`. The expanded in-game
matrix remains required before the goal can be marked complete.

Purpose: fill low- and medium-threat quiet stretches with readable but
unpredictable distant presence instead of turning every encounter opportunity
into combat.

Implemented scope:

- Add a dedicated randomized-cooldown director and exact volatile runtime,
  mutually exclusive with official warning and hunt ownership.
- Curate 26 ordinary profiles below `50` threat and seven high-pressure
  profiles from `50` to below `75` when `AllowEliteEnemies` is enabled.
- Roll hidden `45-55` ordinary and `70-80` high-pressure hostility values.
  Exact Hero damage escalates through a pre-damage listener and adds one-time
  configurable provocation threat.
- Use exact per-Npc combat blocking plus native `Observe`, `FollowMovement`,
  and `Flee` states without faction, global perception, or general AI changes.
- Require walkable verified placement, connected path graphs, no Wyrd repeller,
  and off-camera point plus initialized-renderer validation.
- Permit passive cleanup only after configured distance and continuous camera
  absence. Never release or despawn a hostile stalker because of distance.
- Keep ambient lifecycle isolated from nightly danger budget and official
  kill/escape relief.
- Add additive in-game pacing, placement, disappearance, and provocation
  settings; tune all three one-shot presets without changing schema `6`.
- Add randomized atmosphere pools and restrained GFT routing: Minimal remains
  official-hunt-only, Atmospheric adds witnessed disappearance, and Detailed
  adds sightings, retreats, and escalation flavor without exposing thresholds.
- Add deterministic contracts for bands, cooldowns, rosters, thresholds,
  pursuit, movement, damage escalation, visibility, path safety, cleanup,
  exclusivity, diagnostics, budget isolation, and legacy regression.

Acceptance additions:

- Every profile initializes passive without native premature combat on its
  mapped exterior and becomes hostile at its exact rolled threshold.
- Running deliberately toward each representative body type produces native
  Flee behavior; moving away permits bounded FollowMovement without stacking.
- Turning the camera away never removes a nearby stalker, while a distant
  continuously off-camera passive stalker may disappear cleanly.
- A hostile stalker remains an ordinary live enemy at distance and blocks a
  second Eyes lane until exact lifecycle resolution.
- GFT behavior for Minimal, Atmospheric, Detailed, Diagnostics on/off, absence,
  and rapid event collapse matches the documented routing.
- Every new row in [TEST-MATRIX.md](TEST-MATRIX.md) passes or is explicitly
  removed from scope.

## 0.9.4 - Pursuit consequences and safe rest

Status: implementation and focused contracts complete. Clean packaging,
Vortex staging, and the expanded in-game matrix remain required.

Purpose: make deliberate close pursuit dangerous, keep passive stalking at a
credible distance, and prevent sleep or GFT phase transitions from contradicting
the final waking state.

Implemented scope:

- Keep the existing configurable protected-area decay as the slow safe-state
  threat drain; protected time never adds passive exposure.
- Hold passive stalking around a 20-metre observation buffer and add a
  five-active-second rearm delay after a completed flee episode.
- Escalate the exact stalker when the Hero closes within 8 metres while it is
  fleeing. This is defensive escalation, not an attack, so it adds no special
  provocation threat.
- Patch only the native `RestPopupUI.Rest` action. During an active outdoor
  Wyrdnight, require the native `IsSafelyResting` result, which represents an
  active fueled Wyrd repeller at the Hero's rest point.
- Preserve native daylight rest and native sleep-interruption checks. A Hero at
  a fueled protective point may sleep through the Wyrdnight when no native
  interruption occurs.
- Suppress atmosphere while rest advances world time, then adopt the first
  stable waking phase and protection state without replaying slept-through
  night, stage, protection, hunt, or stalker messages.

Acceptance additions:

- Safe-distance following and flee rearm avoid repeated flee-transition floods.
- Closing to 8 metres during Flee produces exact-Hero combat without awarding
  attack-only provocation threat; remaining outside that boundary stays passive.
- Unprotected active-Wyrdnight Rest is blocked without advancing time, while a
  fueled protective rest point retains native interruption behavior.
- Sleeping across nightfall or dawn produces no contradictory atmosphere and
  at most one diagnostics-only final-phase reconciliation summary.

## 0.9.5 - Tested visual defaults

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and the expanded in-game matrix remain required.

Purpose: turn the approved live meter and boundary tuning into predictable
first-install and upgrade defaults.

Implemented scope:

- Change the threat-meter default to `#8032FF`.
- Set near, middle, and outer radius/intensity/thickness defaults to
  `10 / 0.05 / 0.25`, `20 / 0.05 / 0.25`, and `30 / 0.05 / 0.25`.
- Set minimum threat intensity to `0.8` and boundary pulse amount to `0.8`;
  retain maximum intensity `1.2`, maximum thickness `1.15`, boundary color
  `#B878FF`, and Diagnostics off.
- Advance the config schema because these are material default changes. Back up
  and regenerate the old config, conservatively restore other compatible
  durable customizations, and deliberately retain the new pulse and
  Diagnostics-off defaults instead of restoring their previous custom values.

Acceptance additions:

- A clean config exposes the exact approved defaults in FoA Mod Manager.
- A schema-6 upgrade creates a backup and restores unrelated compatible custom
  settings while pulse becomes `0.8` and Diagnostics becomes false.
- The default boundary remains readable, smoothly animated, and free of radius,
  material, or native-protection side effects.

## 0.9.6 - Unified Wyrdnight visuals

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and the expanded in-game matrix remain required.

Implemented scope:

- Fully integrate Purple Moon Test's moon surface, HDR corona, moonlight,
  night-sky tint, and fueled protection-bubble controls.
- Promote the tested live Purple Moon values to EITD's defaults and retain one
  EITD-owned Diagnostics setting with its safe `false` default.
- Add Purple Wyrdness and region-derived Native Orange palettes.
- Replace boundary-only threat reactivity with a configurable shared visual
  scale of `0.8` at zero threat and `1.2` at 100 threat.
- Smoothly shift moon, moonlight, protection bubble, boundary, and threat meter
  toward configurable red `#FF3028`; explicitly exclude night-sky hue.
- Render the threat meter at `1.5` times RGB brightness before the shared scale.
- Remove threat-driven boundary thickness changes while retaining independent
  per-ring geometry, brightness, and organic pulse.
- Restore captured visual state on daylight, interiors, disablement, and
  teardown without overwriting a later external property change.
- Advance schema to `8`, remove the obsolete boundary threat settings, and
  retire Purple Moon Test only after the integrated package passes automation.

Acceptance additions:

- Purple and Native Orange palettes produce the expected base hues at zero,
  midpoint behavior at 50, and scale/red-shift endpoints at 100 threat.
- The moon surface, corona, and moonlight all shift toward red; night-sky
  color does not.
- Boundary and meter use the same threat response, with no duplicate boundary
  scaling and no threat-driven thickness change.
- Daylight/interior/disable restoration, normal and Ultra Plus systems,
  multiple protection bubbles, loads, time skips, and live config edits work
  without cumulative HDR gain, per-frame warnings, or gameplay changes.
- The package and repository contain no standalone Purple Moon Test, and its
  retired config is absent from the active BepInEx config directory.

## 0.9.7 - Full-sky tint correction

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and the updated in-game matrix remain required.

Implemented scope:

- Confirm through Light Control's shipped DLL that it owns light intensity,
  volumetrics, exposure, and post-exposure, but no sky tint property.
- Move EITD sky coloration from `_NightSkyTint` to `_SkyTint` so it affects the
  complete visible Wyrdnight sky.
- Correct the setting labels and documentation without renaming the durable
  config keys; customized colors and strengths remain valid.
- Preserve external-override-safe restoration and the deliberate exclusion of
  sky color from the threat-driven red shift.
- Advance schema to `9` because the existing color setting now has broader,
  corrected visible-sky semantics.

Acceptance additions:

- The configured palette visibly affects the complete sky and restores the
  captured original `_SkyTint` after daylight, interiors, or disablement.
- Fog, clouds, terrain lighting, and reflections are not described or treated
  as direct outputs of the sky tint.
- Light Control and EITD can run together without repeated sky-property writes
  or ownership contention.

## 0.9.8 - Threat-drawn Wyrdnights

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and the updated in-game matrix remain required.

Implemented scope:

- Replace raw clock multipliers with `DayMinutes`, `BaseNightMinutes`, and
  `MaximumThreatNightMinutes`, defaulting to `60`, `5`, and `15`.
- Interpolate live Wyrdnight duration with current threat while thresholding
  clock writes and preserving native restoration/override safety.
- Base capped sublinear budget capacity on the configured maximum night so a
  fully stretched default night retains the established Watchful bonus.
- Route EITD and GFT-owned Wyrd messages through Purple or Orange according to
  the live Wyrdness palette without conflating color with priority.
- Add an explicit diagnostics-only threat override and forced 0-to-100 value.
- Advance Eyes schema to `10`; keep GFT schema `24` because its integration
  adds no stored setting.

Acceptance additions:

- Measure 60-minute daylight, a 5-minute zero-threat night, and a 15-minute
  continuously forced 100-threat night within the matrix tolerance.
- Change threat during a night and confirm clock duration reacts without
  per-poll setter or log spam.
- Switch palettes live and confirm both Eyes atmosphere and vanilla GFT Wyrd
  messages use the matching configurable color group.
- Force each threat stage, confirm every downstream system responds, disable
  the override, and verify natural threat behavior resumes.

## 0.9.9 - Product identity restoration

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and the consolidated in-game matrix remain required.

Implemented scope:

- Restore the display, assembly, Nexus, and release ZIP identity to
  **Eyes in the Dark - Wyrdnight Overhaul**.
- Retain `EyesInTheDark` as the compact package folder and DLL name, and retain
  the existing plugin GUID, config title, config filename, and schema.

## 1.0.0 - Rest gate and visual transitions

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and the consolidated in-game matrix remain required.

Implemented scope:

- Filter the native rest-availability result so exposed beds and fireplaces do
  not open the clock screen during an active outdoor Wyrdnight. Preserve native
  denials and leave protected and daylight rest under native control.
- Show the exact protective-boundary explanation once per continuous blocked
  episode, with the original Rest action check retained as a failsafe.
- Fade only the integrated environment and fueled protection-bubble palette
  across natural dusk and dawn. Default to `60` active real-time seconds, freeze
  while paused, and restore immediately for confirmed interiors, disablement,
  teardown, or failures. Version 1.0.4 supersedes the original load behavior
  with short-transition continuity.
- Keep config schema `10`; the transition-duration setting is additive.

## 1.0.1 - Readable rest clock

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game visual/input acceptance remain
required.

Implemented scope:

- Preserve the native clockwise 24-hour selection model and label its cardinal
  hours so the dial cannot reasonably be mistaken for a 12-hour clock.
- Replace the generic 12/12 half-circle with an exact runtime-drawn Wyrdnight
  arc spanning approximately 22:05 to 05:31.
- Move the existing moon and sun markers to those phase boundaries and match the
  overlay to the current Wyrdness palette and threat-to-red response.
- Fail open to the usable native rest clock if the expected prefab hierarchy or
  optional rendering layer is unavailable.

## 1.0.2 - Native disabled-rest presentation

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Add `AllowUnprotectedWyrdnightRest`, default `false`, as a durable preference
  independent of the Uneasy, Watchful, and Cursed gameplay presets.
- Keep protected Wyrdnight rest available by default while exposed REST uses
  the game's native greyed-out, inactive presentation.
- Remove the blocked-rest warning panel and episode-tracking state. Retain the
  original Rest action prefix only as a silent failsafe for stale UI or another
  entry route.
- Preserve daylight behavior and every native rest denial. Keep schema `10`
  because the setting is additive.

## 1.0.3 - Pre-dawn fade and ranged noise

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Derive remaining real Wyrdnight time from normalized night progress and the
  current game weather rate.
- Begin the visual fade-out during the final configured transition duration so
  it reaches the restored presentation at dawn. Retain the existing post-dusk
  fade-in and immediate unsafe-context restoration.
- Record modest capped threat for a released Hero projectile and a successfully
  completed spell even when it hits nothing. Skip failed and canceled casts and
  avoid double-counting projectile spells.
- Keep melee attack-start non-threatening; melee still requires meaningful
  damage or one confirmed environment impact per attack.
- Keep schema `10`; no config setting changed identity, type, default, or
  meaning.

## 1.0.4 - Rest UI and visual continuity

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Reapply the final native-plus-Eyes rest result to the actual fireplace REST
  button after initialization, upgraded-bonfire refreshes, and live eligibility
  changes while the menu remains open. Retain the silent final action guard.
- Replace the palette arc and Wyrdnight caption with neutral cardinal labels,
  the moon at the top, and sun at the bottom. Add an additive 12-hour/24-hour
  label-format preference, defaulting to 12-hour labels.
- Keep Wyrdnight visuals active while the rest popup is open. Hold the last
  confirmed presentation through short loading/transition states and prime new
  exterior-night renderers from the authoritative world clock.
- Restore immediately only for confirmed daylight, interiors, title screen,
  disablement, teardown, or visual failure, retaining external-write protection.
- Keep schema `10`; the label-format setting is additive.

## 1.0.5 - Battlecry response

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Expose a versioned soft API for Battlecry Voice Tuner without creating a hard
  dependency in either direction.
- Accept cries only while the Hero is exposed outdoors during a valid active
  Wyrdnight and apply full, half, quarter, then diminishing threat down to a
  10 percent floor.
- Reset the diminishing sequence after 30 active seconds without an accepted
  cry.
- After two or three accepted cries, allow Atmospheric and Detailed GFT presets
  to select one of seven Wyrdnight-response lines, with a separate configurable
  45-active-second default cooldown. Keep Minimal quiet.
- Keep schema `10`; BattlecryResponseCooldownSeconds is additive.

## 1.0.6 - Clock and visual polish

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Apply the existing TwelveHour preference to the quick-use weather clock with
  AM/PM text; leave the game's native quick-use label untouched in
  TwentyFourHour mode.
- Place the neutral rest-clock moon and sun just inside the dial below the
  midnight and noon labels without touching native selection behavior.
- Center the configured dusk transition on nightfall. At the default 60-second
  duration it begins 30 seconds before, is half blended at nightfall, and
  finishes 30 seconds after; retain the existing fade that finishes at dawn.
- Ramp only the loaded threat contribution to presentation color over 10 active
  seconds while keeping authoritative threat, meter fill, stages, gameplay,
  and dynamic night timing immediate.
- Add PurpleWyrdnessBrightness with a 1.2 default and 0.5-to-2.0 range for sky
  emission and HDR moon/world-light color channels. Leave exposure,
  post-exposure, light intensity, dimmers, volumetrics, and Native Orange
  outside Eyes ownership.
- Suppress Eyes diagnostic GFT at the title screen, during loading, and without
  a playable Hero. Preserve GFT-owned compatibility notices.
- Change the battlecry-response notification cooldown default from 45 to 15
  active seconds without changing the battlecry API, diminishing threat,
  randomized response pool, or two-to-three-cry cadence.
- Increment schema from `10` to `11` because the existing battlecry-response
  cooldown default changed. Preserve genuine customized values conservatively.

## 1.0.7 - Config UX and clock baseline

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Change the primary world-clock baseline to a 60-minute day, six-minute quiet
  Wyrdnight, and 12-minute maximum-threat Wyrdnight. This keeps low threat near
  the game's approximately 6.2-minute night and lets maximum threat double it.
- Preserve dynamic interpolation and capped square-root encounter-budget
  scaling; Watchful receives roughly a 14 percent maximum-night capacity bonus.
- Reorder General as master switch, apply-preset-once, ambient stalkers, elite
  enemies, unprotected rest, and time display with unique ordering.
- Keep World Clock, HUD, Boundary Appearance, Wyrdnight Appearance, and
  Notifications concise, then group detailed controls under clearly labeled
  Advanced sections.
- Add units directly to time and distance labels and replace user-facing hazard,
  danger-budget, and sidecar terminology with hunt pressure, encounter budget,
  and additional hunter language.
- Replace raw `BoundaryHdrIntensity` with `BoundaryBrightness`, default `1.0`
  and range `0-3`, converted internally through the preserved `271.529`
  vanilla-equivalent HDR baseline.
- Increment schema from `11` to `12` because the existing night-duration
  defaults changed and the boundary setting was replaced with a new scale.
  Preserve compatible durable settings and skip the retired raw HDR value.
- Preserve the 1.0.6 battlecry API, diminishing threat, randomized response
  pool, two-to-three-cry cadence, and 15-second notification cooldown default.

## 1.0.8 - Noon-first clock and performance pass

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Rotate the rest selector's labels, sun/moon icons, hand, fill, mouse mapping,
  and controller mapping together so noon is at the top without changing the
  selected hour or native rest behavior.
- Format Current time and Resting until with AM/PM when TwelveHour is selected;
  TwentyFourHour continues to leave native text untouched.
- Recalculate Wyrdnight lighting at the existing five-times-per-second state
  cadence, then reapply only cached values after the native per-frame lighting
  update overwrites them.
- Cache parsed visual colors and coalesce environment-lighting refreshes to at
  most four per second while retaining immediate forced restoration.
- Disable the custom layered-boundary pass whenever the native boundary has no
  visible intensity, cache stalker renderers, and remove steady-state camera
  visibility arrays.
- Avoid redundant threat-meter layout and activation writes, poll Hero and
  world subscription state with the existing director cadence, and aggregate
  continuous movement diagnostics instead of logging every poll.

## 1.0.9 - Preset-driven risky rest

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Route added Wyrdnight sleep risk through the native time-skip interruption
  check, preserving native interruptions as the first authority and native wake
  presentation for Eyes interruptions.
- Keep one chance roll and one cumulative exposure threshold per Wyrdnight so
  repeated short rests do not reroll. Accumulate only accepted unprotected
  Wyrdnight overlap.
- Apply preset defaults of 0/0 percent for Uneasy, 45/75 percent for Watchful,
  and 80/100 percent for Cursed. Uneasy and Watchful permit active-night rest;
  Cursed blocks starting it after nightfall while permitting pre-night rest to
  cross the phase boundary.
- Lock further unprotected rest until dawn after an interruption. Fueled
  protective boundaries remain safe and available.
- Queue one immediate official hunt after an Eyes interruption and retain all
  normal selection, budget, placement confirmation, and failure rules. Do not
  duplicate native interruption encounters.
- Add OwnRestMenu, default true. When disabled, restore and leave native rest
  controls, clock layout, labels, and popup formatting untouched while keeping
  a silent final gameplay guard.
- Advance schema to `13` because the existing unprotected-rest default changed
  from false to true. Additive risk and presentation settings remain eligible
  for normal preservation.

## 1.1.0 - Mirrored meter animation correction

Status: superseded by 1.1.9 after live diagnostics showed that the cloned
mana-bar hierarchy had no eligible `TextureScroller`.

Implemented scope:

- Keep the threat meter artwork mirrored horizontally and vertically.
- Reverse only the cloned `TextureScroller` speed axes affected by that mirror
  so its apparent movement matches the vanilla Hero resource bars.
- Give each cloned scroller a private runtime material before initialization so
  the meter cannot change shared vanilla materials.
- Destroy those materials during meter teardown and retain config schema `13`.

The attempted correction was structurally present but never activated on the
tested HUD. Version 1.1.9 removes this dead path and defers a real correction
until the animated renderer and shader properties are identified.

## 1.1.1 - Diagnostic timescale override

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Add diagnostics-only enable and multiplier controls with a `0.01` to `5.0`
  range and a safe default of disabled at `1.0`.
- Apply the multiplier to the vanilla `GameRealTime` world clock even when
  normal Dynamic Timescale is off, while continuing to obey the Eyes master
  switch.
- Keep Unity gameplay time untouched, avoid redundant setters across phase and
  threat changes, and retain safe restoration plus external-owner protection.
- Exclude both testing controls from config recovery and retain schema `13`.

## 1.1.2 - Palette-preserving brightness

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Keep moon-surface, moonlight, and full-sky tint strengths independent of the
  shared threat scale so low threat retains the configured base palette.
- Apply threat brightness and Purple Wyrdness brightness to selected HDR colors
  in linear color space.
- Stop reading, writing, reapplying, or restoring the original skybox emission
  multiplier.
- Advance schema to `14` because PurpleWyrdnessBrightness now controls the
  tinted-sky color rather than original sky emission. Preserve compatible
  customized values conservatively.

## 1.1.4 - Exposure and rest-clock correction

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Remove only `PurpleWyrdnessBrightness`; retain the configurable shared
  minimum and maximum threat visual-strength settings.
- Apply a fixed threat-independent `1.2` Purple exposure multiplier through the
  existing visual fade. Native Orange remains unchanged and Light Control
  exposure multipliers remain additive.
- Capture native rest-clock rotations, assign the same fixed half-turn after
  every native refresh, and restore the native rotations on release.
- Change the existing diagnostics GFT System cooldown default from `3` seconds
  to `1` second.
- Advance schema to `15` because one setting was removed and an existing
  setting default changed.

## 1.1.7 - Mode-aware Purple brightness

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Add `PurpleExposureCompensation` in Wyrdnight Appearance with a `+0.35 EV`
  default and a supported `-2` through `+2 EV` range.
- Apply the value after Light Control through the existing DayNightSystem
  exposure postfix and the existing natural visual fade.
- Add EV to automatic and physical-camera compensation and subtract EV from
  fixed exposure, matching the native exposure mode's sign convention.
- Leave Native Orange, HDRP post-exposure, gamma, colors, and global volumes
  unchanged.
- Keep schema `15` because the setting is additive.

## 1.1.8 - Purple indirect diffuse tuning

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Add `PurpleIndirectDiffuseMultiplier` in Wyrdnight Appearance with a `1.10`
  default and a supported `0` through `3` range.
- Patch the native `HandleIndirectLighting` result and multiply the game-owned
  `indirectDiffuseLightingMultiplier` through the existing natural fade.
- Leave Native Orange, direct moonlight, reflection lighting, reflection-probe
  intensity, exposure, gamma, colors, and global volumes unchanged.
- Keep schema `15` because the setting is additive.

## 1.1.9 - Exposure control and meter cleanup

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Replace the fixed Purple exposure multiplier with
  `PurpleExposureMultiplier`, default `1.2`, range `0` through `3`.
- Keep the multiplier separate from mode-aware EV compensation and indirect
  diffuse lighting.
- Remove the ineffective TextureScroller speed reversal, private material
  allocation, teardown, structural contract, and user-facing success claims.
- Keep the mirrored meter artwork and correct fill-origin behavior unchanged.
- Keep schema `15` because the new setting is additive and the removed runtime
  path owned no configuration.

## 1.2.0 - Diagnostic hardening candidate

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and consolidated in-game acceptance remain required.

Implemented scope:

- Refresh concise visual diagnostics when the Purple exposure multiplier,
  mode-aware EV compensation, or indirect diffuse multiplier changes live.
- Include the exact Purple exposure and indirect diffuse values in that summary.
- Test the optional battlecry integration against Battlecry Voice Tuner 1.0.7.
- Keep schema `15` because no configuration setting or meaning changed.

## 1.3.1 - Glorious UI rest presentation split

Status: implementation and documentation complete. Automated contracts, clean
packaging, Vortex staging, and focused in-game acceptance remain required.

Implemented scope:

- Move the noon-at-top rest clock, dial labels, popup time formatting, and
  quick-menu time formatting into Glorious UI's toggleable Sensible Rest Menu.
- Keep Eyes responsible for Wyrdnight REST-button availability, final gameplay
  rest enforcement, cumulative interruption risk, and post-interruption hunts.
- Replace OwnRestMenu with ShowWyrdnightRestAvailability and remove
  RestClockLabelFormat from Eyes.
- Advance Eyes config schema to `21`; keep Glorious UI schema `1` because its
  three rest-presentation settings are additive.

## Explicitly deferred beyond 1.3.1

- Custom save persistence for threat or active encounters.
- Indoor hunts.
- Custom enemies, models, audio, rewards, or item grants.
- Quest, unique, boss, trial, summon, or unsafe templates.
- Generalized encounter scripting or third-party profile framework.
- Broad guard, civilian, faction, movement, or AI ownership.
- Automatic conflict handling. GFT may report the documented Wyrd Hunt and
  Custom Timescale incompatibilities but never alters either plugin.

## Goal execution rule

When this roadmap is used as the current development goal, execute one
implementation milestone at a time with automated contracts and clean builds,
then run the consolidated in-game pass after the implementation is complete.
Do not mark the goal complete until every applicable acceptance criterion
is either verified or explicitly removed from scope by the user. Failures found
in the consolidated pass require a fix and focused retest; they are not a reason
to omit the affected scenario.
