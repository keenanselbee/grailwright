# Eyes in the Dark 0.8.6 consolidated in-game matrix

Candidate:

- Eyes in the Dark `0.8.6`
- Glorious UI `1.7.0` when the integration case calls for it
- Grail Floating Text `1.9.8` when the notification case calls for it
- Tainted Grail Mono patch `1.25`

Run this matrix only against the staged `0.8.6` candidate. Keep Wyrd Hunt,
Custom Timescale, and KS Wyrd Hunt Addon absent except for isolated
incompatibility-notice cases.
For each failure, save the relevant BepInEx log, fix the candidate, rebuild, and
repeat the failed case plus adjacent state transitions.

Use Diagnostics only where requested. Its GFT System summaries are testing
output; the BepInEx log remains the authoritative detailed record.

## A. Startup and optional integrations

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| A1 | Eyes only; load an exterior save | Clean startup; gameplay works without GFT or Glorious | Pending |
| A2 | Eyes + GFT + Glorious; load the same save | All three load without exceptions; Eyes reports the optional integrations | Pending |
| A3 | Eyes + GFT + Wyrd Hunt, main menu only | One GFT System notice says `Wyrd Hunt is flagged as incompatible with Eyes in the Dark.` Neither plugin is altered | Pending |
| A4 | Remove Wyrd Hunt and KS Wyrd Hunt Addon; reload | No incompatibility notice; no legacy meter or addon behavior | Pending |
| A5 | Eyes + GFT + Custom Timescale, main menu only | Exactly one GFT System notice says `Custom Timescale is flagged as incompatible with Eyes in the Dark.` Neither plugin is altered | Pending |

## B. Night state, meter, and boundary

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| B1 | Daylight exterior | Threat meter hidden; no boundary override attachment | Pending |
| B2 | Valid exterior Wyrdnight without Glorious | Meter visible above health, mirrored horizontally and vertically | Pending |
| B3 | Same state with Glorious enabled | Same Eyes-owned meter appears below resource bars; no duplicate | Pending |
| B4 | Disable and re-enable Glorious layout control | Meter falls back above health, then returns below bars without duplication | Pending |
| B5 | Enter a protected outdoor area during Wyrdnight | Meter remains visible; threat decays at the protected rate; no hunt advances | Pending |
| B6 | Enter an interior during Wyrdnight | Meter hides; threat decays slowly instead of resetting | Pending |
| B7 | Exit the interior | Meter returns with retained threat; activity grace prevents an immediate surge or hunt | Pending |
| B8 | Fast travel, portal, and loading-screen transitions | Meter fails hidden during transitions and returns once the exterior state is valid | Pending |
| B9 | HUD rebuild, resolution change, and non-default UI scale | One correctly positioned meter remains readable and attached | Pending |
| B10 | Change boundary color, HDR, radius, thickness, and reactivity | Visuals change; protection behavior, gameplay radius, and mask intensity do not | Pending |

## C. Threat inputs and anti-spam behavior

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| C1 | Remain exposed through measurable world-night progress | Passive threat follows normalized night progress, not elapsed Unity time | Pending |
| C2 | Walk, then sustain sprinting or fast swimming | Walking adds no movement threat; sustained fast movement adds throttled threat | Pending |
| C3 | Deal and receive meaningful combat damage | Damage aggregates within the short window and respects its cap | Pending |
| C3a | Swing into empty space, then strike scenery or a non-damageable object several times | Empty swings add nothing; confirmed impacts add at most one contribution per attack and respect the combat-window cap | Pending |
| C4 | Kill an eligible Wyrd-converted or Wyrd-bound ordinary NPC | One Wyrd-kill threat input is accepted | Pending |
| C5 | Take direct pickups and loot several container/corpse items | Unique acquisitions add capped queued threat; repeated low-value input cannot farm it | Pending |
| C6 | Pause during active cooldowns | Threat windows, GFT cooldowns, warning, recovery, and interior decay do not advance while paused | Pending |
| C7 | Reach each stage in order, then decay downward | Unnoticed, Watched, Hunted, and Marked transitions occur at the documented values | Pending |
| C8 | Reach dawn | Threat, activity windows, nightly budget, and hunt state reset cleanly | Pending |

## D. Presets and world timescales

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| D1 | Apply Uneasy Night in FoA Mod Manager | Gameplay values are written, selector returns to Custom, presentation/diagnostics stay unchanged | Pending |
| D2 | Apply Watchful Night | Recommended defaults are written and selector returns to Custom | Pending |
| D3 | Apply Cursed Night | Higher-pressure values are written and selector returns to Custom without changing presentation | Pending |
| D4 | Observe a representative Watchful night at world timescale `1.0` | Quiet stretches, warning, one active hunt, recovery, and budget behavior remain coherent | Pending |
| D5 | Repeat at `0.5` | Passive baseline follows world progress and total pressure remains within the capped budget design | Pending |
| D6 | Repeat at `0.25` | No catch-up threat, instant encounter, or linear multiplication of hunts | Pending |
| D7 | Repeat at `0.1` | Long night remains playable; initial budget is at most the documented capped bonus | Pending |
| D8 | Measure one default `0.23` day | Daylight lasts 60 real minutes within +/-0.5 minute | Pending |
| D9 | Measure one default `0.413` night | Night lasts 15 real minutes within +/-0.5 minute; complete cycle is about 75 minutes | Pending |
| D10 | Cross dawn and nightfall naturally | The configured phase rate switches within one update without repeated setter logging | Pending |
| D11 | Load and perform phase-changing time skips | The correct day/night rate is active immediately after the new world/phase becomes valid | Pending |
| D12 | Edit both rates live | Each changed active-phase value applies once; the inactive-phase value applies at the next phase | Pending |
| D13 | Disable Dynamic Timescale, then disable Eyes separately | Vanilla duration is restored only when Eyes still owns its last rate | Pending |
| D14 | Let another clock owner change the active rate, then disable Eyes | Eyes does not overwrite the external rate during safe restoration | Pending |
| D15 | Complete representative Watchful nights at the default `0.23/0.413` cycle | Quiet stretches, warnings, hunts, recovery, and roughly 19 percent long-night budget bonus remain coherent | Pending |

## E. Curated director and hunt lifecycle

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| E1 | Level 1 in Horns of the South | Eligible pool contains only Wyrdspirit and pack size is one | Pending |
| E2 | Levels 4, 7, 12, and 20 in Horns; inspect diagnostic weights | Redcap, Corpse Eater, Sharg, and Ogre become eligible gradually; lower tiers retain weight | Pending |
| E3 | Cuanacht at levels 15, 18, 22, and 26 | Native Corpse Eater, Mistling, Sharg, and Ogre become eligible at their exact gates and place on native navigation | Pending |
| E4 | Watchful, level 8+, high threat, sufficient budget | A two-member allowed composition can occur; sidecar is not stronger than primary | Pending |
| E5 | Cursed, level 15+, high threat, sufficient budget | A three-member Wyrdspirit cluster can occur without exceeding cost or copy caps | Pending |
| E6 | Reduce remaining budget near a profile/composition cost | Ineligible members are filtered; the final plan never overspends | Pending |
| E7 | Lose exposure, enter protection, or start unrelated combat during warning | Warning cancels, retains at most half pressure, and spends zero budget | Pending |
| E8 | Cause an invalid or failed member placement | Entire volatile composition is discarded, cost is zero, and retry recovery begins | Pending |
| E9 | Kill a primary while a sidecar survives | Official hunt resolves; surviving sidecar remains an ordinary enemy and no director lock remains | Pending |
| E10 | Watchful: kill the exact official primary | Threat relief is 35 and recovery is 90 active seconds; corpse may remain | Pending |
| E11 | Watchful: sustain 80 m separation for 10 active seconds | Threat relief is 15 and Recently Pursued recovery is 180 active seconds | Pending |
| E12 | Enter an interior during an active hunt | Counts as escape, discards live volatile targets, grants escape rather than kill relief | Pending |
| E13 | Trigger player death, dawn, gameplay load, and exterior scene change in separate hunts | Each resolves once; no duplicate hunt, stale lock, or surprise budget spend | Pending |
| E14 | Complete several hunts in one session | Immediate profile and family repeats are visibly reduced in diagnostic weights | Pending |
| E15 | Make one template fail three times in a diagnostic session | Template becomes session-rejected; an empty pool skips safely | Pending |
| E16 | Forlorn at levels 22, 26, and 30 | Native Redcap, Mistling, and Corpse Eater become eligible at exact gates and place natively | Pending |
| E17 | Sarras at levels 28, 34, and 36 | T5/T6 Wyrdspawn and solo Wyrdheir become eligible at exact gates and place natively | Pending |
| E18 | Inspect repeated regional compositions | Every regional profile has a one-copy limit; only Wyrdspirit clusters; solo profiles never gain sidecars | Pending |
| E19 | Force one member to fail exact Hero combat confirmation after placement | Atomic composition is discarded, budget cost is zero, and failed-placement recovery begins | Pending |
| E20 | Let a nearby hunter disengage while exposed in the same exterior | Native combat is reasserted no sooner than two active seconds apart and no more than three times per member | Pending |
| E21 | Disengage beyond 60 m, while protected, during loading, or after scene change | No reacquisition occurs; normal sustained escape remains available | Pending |

## F. GFT atmosphere and diagnostics

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| F1 | Minimal notification preset | Only committed hunts and hunt outcomes show | Pending |
| F2 | Atmospheric preset | Minimal events plus night begin/end and upward-stage text show | Pending |
| F3 | Detailed preset | Downward stages, protection transitions, and major surges also show | Pending |
| F4 | Repeat each event pool several times | Text varies and the same pool never repeats immediately | Pending |
| F5 | Diagnostics off | No EITD GFT System summaries appear | Pending |
| F6 | Diagnostics on | Concise low-priority System summaries show exact state, threat, budget, filters/weights, composition, cost, placement, and outcomes | Pending |
| F7 | Generate rapid related diagnostic transitions | One immediate collapse lane and active-time cooldown prevent spam or stale queued messages | Pending |
| F8 | Remove or break GFT for an isolated check | Core threat, meter, boundary, and encounters continue; integration failure logs once | Pending |
| F9 | Diagnostics on during continuous exposure, protected decay, and interior decay | One aggregate passive/decay summary appears per ten active seconds and flushes on state/stage/night-end changes | Pending |
| F10 | Temporarily provoke Hero and acquisition listener binding failures | One warning appears per failure episode; retry waits 30 unscaled seconds and retries immediately after Hero/event-system replacement | Pending |

## G. Soak and final log review

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| G1 | Complete a long `0.1`-timescale night with Diagnostics off | No repeated exceptions, per-poll log spam, duplicate meter, or uninterrupted chain of hunts | Pending |
| G2 | Review the full session log | No startup, placement, transition, HUD, boundary, or GFT exception loop; every spent cost has a confirmed composition | Pending |
| G3 | Inspect the staged archive and live candidate version | One top-level folder; only DLL, README, and changelog; assembly reports `0.8.6.0` | Pending |
| G4 | Complete a long diagnostics-on default-cycle soak | No per-frame warnings, passive-threat log flood, repeated clock setters, or stale GFT diagnostics | Pending |

The goal is complete only after every row is Passed or the user explicitly
removes it from scope. Automated contracts support these checks but do not
replace the in-game rows.
