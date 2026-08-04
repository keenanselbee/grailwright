# Eyes in the Dark 0.8.3 Implementation Roadmap

## Objective

Reach a hardened, user-testable `0.8.3` beta of **Eyes in the Dark -
Wyrdnight Encounters** without expanding beyond the product rules in
[DESIGN.md](DESIGN.md).

The roadmap advances through narrow vertical slices. Each milestone must compile
and satisfy its automated contracts before the next begins. Consolidated
in-game acceptance begins only after the `0.8.3` implementation is complete.
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
| 0.8.3 | Dynamic clock and hardening | The 60/15 clock, expanded regional roster, and runtime hardening are validated. |
| 0.9.0 | Native roster data pass | Scene-backed regional variety and opt-in high-threat elites are validated. |

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
- World timescale checks at `1.0`, `0.5`, `0.25`, and `0.1`.
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
- Vanilla and `0.1` timescale hunt-cadence comparison.
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
- Each preset at vanilla and `0.1` timescale.
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

- Dynamic `GameRealTime` weather-rate ownership balanced around a `0.23` day
  and `0.413` night, approximately 60/15 real minutes.
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
- At the `0.23/0.413` reference cycle, Watchful pacing completes coherently;
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

- The default day and night measure within 0.5 real minute of 60 and 15.
- Dynamic clock switching, live config changes, load/time-skip handling,
  safe disable restoration, and external-override protection pass.
- `1.0`, `0.5`, `0.25`, and `0.1` world-timescale regressions pass.
- Early-, mid-, and late-level scenarios pass on every supported map.
- Day/night, protected/exposed, indoor/outdoor, pause, portal, fast travel,
  death, dawn, save/load, and scene-transition scenarios pass.
- GFT and Glorious presence/absence matrices pass.
- Config recovery and preservation contracts pass.
- Build and package checks pass with one top-level package folder and no source,
  tools, design docs, publishing metadata, or other repository-only files.
- Known residual risks are documented honestly.

### Consolidated in-game test pass

Begin this pass only after the `0.9.0` implementation, automated contracts, and
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

## Explicitly deferred beyond 0.9.0

- Custom save persistence for threat or active encounters.
- Indoor hunts.
- Custom enemies, models, audio, rewards, or item grants.
- Quest, unique, boss, trial, summon, or unsafe templates.
- Generalized encounter scripting or third-party profile framework.
- Broad guard, civilian, faction, movement, or AI ownership.
- Automatic conflict handling. GFT may report the documented Wyrd Hunt and
  Custom Timescale incompatibilities but never alters either plugin.

## Goal execution rule

When this roadmap is used as the 0.9.0 development goal, execute one
implementation milestone at a time with automated contracts and clean builds,
then run the consolidated in-game pass after the `0.8.3` implementation is
complete. Do not mark the goal complete until every `0.8.3` acceptance criterion
is either verified or explicitly removed from scope by the user. Failures found
in the consolidated pass require a fix and focused retest; they are not a reason
to omit the affected scenario.
