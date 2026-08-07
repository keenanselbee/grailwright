# Eyes in the Dark 1.2.8 consolidated in-game matrix

Candidate:

- Eyes in the Dark `1.2.8`
- Battlecry Voice Tuner `1.1.0` when the battlecry cases call for it
- Glorious UI `1.7.1` when the integration case calls for it
- Grail Floating Text `1.10.0` when the notification case calls for it
- Tainted Grail Mono patch `1.25`

Run this matrix only against the staged `1.2.8` candidate. Keep Wyrd Hunt,
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
| B10 | Observe the default layered boundary at normalized brightness 1.0, then change brightness and each ring's radius, brightness, and thickness | The 1.0 default retains the prior vanilla-equivalent HDR peak; three distinct near/middle/outer rings respond independently; protection behavior, gameplay detection, and native mask intensity do not change | Pending |
| B11 | Raise and lower threat, then test pulse amounts 0, 0.8, and 1.0 | Every ring has its own smoothly changing target and timing; threat response remains shared and no ring radius moves; 1.0 stays within dim-to-double brightness | Pending |
| B12 | Pause, resume, select Single Ring, disable customization, then re-enable Three Rings | Pulses freeze while paused; native presentation is restored cleanly in single/disabled modes; no duplicate pass or material leak appears after re-enable | Pending |
| B13 | Review FoA Mod Manager from top to bottom | General contains uniquely ordered master, apply-preset-once, ambient, elite, rest, and time-display controls; World Clock, HUD, Boundary Appearance, Wyrdnight Appearance, and Notifications follow before clearly labeled Advanced groups; units are visible in labels and Import Previous Settings remains last | Pending |
| B14 | During daylight, rest outside a protective boundary without crossing into night | Native rest remains available and no Eyes denial appears | Pending |
| B15 | Apply Cursed Night, then target and repeatedly use an exposed bed or fireplace during an active outdoor Wyrdnight; also trigger an upgraded-bonfire action refresh | REST uses the game's greyed-out, inactive presentation before and after refresh; the clock never opens and Eyes queues no warning message | Pending |
| B16 | During an active Wyrdnight, rest at a fueled protective point | Rest proceeds using the native safe-rest and interruption checks; if nothing interrupts it, the Hero may sleep through the Wyrdnight | Pending |
| B17 | Start from a schema-12 config with the old untouched false unprotected-rest default and genuine customized gameplay, HUD, and sky values | A beside-config backup is created; the current schema adopts the true Watchful rest default and palette-preserving brightness behavior while restoring compatible durable values | Pending |
| B18 | Purple palette at 0, 50, and 100 threat | Moon surface, corona, moonlight, bubble, boundary, and meter retain their configured purple tint strength while brightness scales from 0.8 through 1.0 to 1.2 and red-shifted layers smoothly approach the configured red | Pending |
| B19 | Observe NightSkyAmbientColor at 0, 50, and 100 threat | The complete visible sky retains its configured tint strength and selected purple hue while the tinted color brightness follows the shared scale; original sky emission, fog, clouds, terrain lighting, and reflections are not directly changed | Pending |
| B25 | Compare the same scene with Light Control enabled and disabled | EITD retains visible-sky tint ownership; Light Control independently changes intensity and volumetrics, runs before Eyes for shared exposure paths, and neither mod fights over _SkyTint | Pending |
| B20 | Orange Wyrdness palette at low threat in each open-world region | Moon, corona, moonlight, sky, bubble, and boundary derive their base hue from that region's original game values rather than using a hard-coded orange | Pending |
| B21 | Set distinct Purple and Orange threat-meter base colors, red targets, and brightness values; switch palettes and compare low and high threat | Meter selects the active palette's complete color pair and brightness, responds live across the 0-to-3 range, applies 3x RGB per setting point, retains a clearly visible Purple or Orange base without mana-blue contamination, and shifts smoothly toward its configured red target | Pending |
| B22 | Disable Wyrdnight visuals, enter an interior, return outside, and re-enable | Disablement and the interior restore game-owned environment values immediately; meter and boundary retain their independent ownership without stale palette state | Pending |
| B23 | Change every moon, sky, bubble, scale, and red-shift setting live | The next stable visual update adopts the value without cumulative HDR gain, duplicate materials, or a restart | Pending |
| B24 | Exercise normal and Ultra Plus day/night systems plus two fueled protection bubbles | Every integrated Purple Moon Test layer applies once per live instance and restores safely; protection radius, fuel, mask, timescale, and gameplay remain unchanged | Pending |
| B26 | Remain in one stable exterior through natural dusk and dawn with transition duration 60 | Dusk presentation begins approximately 30 real seconds before nightfall, is half blended at the boundary, and finishes approximately 30 seconds after; dawn presentation begins fading when approximately 60 real seconds remain and finishes at dawn. Phase, timescale, meter, and protection still switch immediately | Pending |
| B27 | Pause halfway through a natural presentation transition, then resume | The visual blend freezes while paused and completes over the remaining active real time without a visible jump | Pending |
| B28 | Load between scenes, enter an interior, disable Eyes, and set transition duration to 0 in isolated checks | Short loads hold the last confirmed presentation; confirmed interiors/disablement restore immediately; zero duration snaps at the natural phase boundary without stale materials or ownership | Pending |
| B29 | Open the rest selector in daylight and at a fueled protective point during Wyrdnight with the default label format | Sun and 12 PM are at top, 6 PM is at right, moon and 12 AM are at bottom, and 6 AM is at left; no Wyrdnight caption, colored arc, glow, or markers appear | Pending |
| B30 | Select every hour repeatedly with mouse, keyboard, and controller while watching the hand, native fill, and Resting until value | The fixed half-day-rotated hand and fill never alternate back to their native orientation, remain aligned with radial selection, keyboard stepping remains native, and Resting until uses h:mm AM/PM | Pending |
| B31 | Select TwentyFourHour labels, then reopen the clock under Purple Wyrdness and Orange Wyrdness | Neutral labels read 00, 06, 12, and 18 in their cardinal positions and remain independent of palette and threat | Pending |
| B32 | Use an exposed rest point during active Watchful Night | REST remains available if the game otherwise permits it; native interruption is checked first and Eyes then applies 45-to-75-percent threat-scaled cumulative risk | Pending |
| B33 | Load directly into an exterior Wyrdnight from the title screen and through a same-night fast travel | The first visible rendered frame retains/applies the Wyrdnight palette without a brighter vanilla flash; no stale purple survives a confirmed daylight or interior destination | Pending |
| B34 | Open and cancel protected rest, then open it again and ACCEPT a time skip | Opening and accepting rest do not flash vanilla lighting before the native fade; final waking daylight or Wyrdnight presentation is correct when the camera returns | Pending |
| B35 | View the quick-use weather clock with TwelveHour selected at midnight, noon, and an evening time | The native world time is shown as 12:xx AM, 12:xx PM, and h:mm PM with no leading zero | Pending |
| B36 | Select TwentyFourHour and rebuild/reopen the quick-use wheel | Eyes leaves the game's native quick-use time text untouched | Pending |
| B37 | Load a save with substantial retained Wyrd Threat directly into a Wyrdnight | Gameplay, stage, meter fill, and night duration use loaded threat immediately while only its world-palette red shift ramps in smoothly over 10 active seconds | Pending |
| B38 | Compare both palettes through dusk and dawn with Wyrdnight Brightness at 1 | Purple maps to 1.75x exposure plus +0.35 EV; Orange leaves exposure native at 1x and 0 EV; both remain independent of threat and follow the presentation fade | Pending |
| B39 | Repeat B38 with Light Control enabled and disabled and Wyrdnight Brightness at 0, 0.5, 1, and 2 | Each palette scales its exposure targets proportionally after Light Control in every native exposure mode; live changes respond cleanly; HDRP post-exposure, gamma, colors, and global volumes remain untouched | Pending |
| B39a | Compare native indirect diffuse lighting across both palettes and every Wyrdnight Brightness value | Eyes never changes indirect diffuse lighting; direct moonlight, reflections, and native indirect-lighting ownership remain untouched | Pending |
| B40 | Set visual transition duration to 0 and cross nightfall | Presentation snaps at the exact phase boundary; no pre-dusk state remains active | Pending |
| B41 | Apply each gameplay preset from General while Diagnostics is enabled | Each selection applies once, reports the chosen result, returns to Custom, preserves clock/presentation settings, and does not collide or reorder unpredictably with Time Display | Pending |
| B42 | Apply Uneasy, Watchful, and Cursed in turn and inspect the three rest settings | Uneasy writes allow true and 0/0 risk; Watchful writes allow true and 45/75; Cursed writes allow false and 80/100; Own Rest Menu is never changed | Pending |
| B43 | Begin exposed Cursed rest shortly before nightfall and request enough time to cross the complete Wyrdnight | The rest request is accepted, the native deterministic nightfall surprise is replaced by cumulative risk, and the 80-to-100-percent model is highly likely to interrupt within Wyrdnight | Pending |
| B44 | Divide the same Watchful Wyrdnight exposure among repeated short rests | Exposure accumulates across the night without fresh chance rolls; canceling the popup adds no exposure | Pending |
| B45 | Cause a native sleep interruption during unprotected Wyrdnight overlap | Native wake behavior remains unchanged, further exposed rest is locked until dawn, and Eyes queues no duplicate official hunt | Pending |
| B46 | Force an Eyes sleep interruption in a valid regional scene | Native wake presentation appears, one official hunt is requested after the transition, and normal level, elite, budget, atomic placement, and zero-cost failure rules remain visible in diagnostics | Pending |
| B47 | After an interruption, try exposed and fueled protected rest before dawn, then try exposed rest after dawn | Exposed rest remains unavailable before dawn, fueled protected rest remains available, and dawn clears the disturbed lock | Pending |
| B48 | Disable Own Rest Menu and reopen beds, campfires, and the rest popup in both 12-hour and 24-hour modes | Eyes leaves button availability, clock orientation, labels, and popup time text native; active Cursed gameplay denial still closes an accepted exposed rest silently | Pending |
| B49 | Compare health, mana, stamina, and threat-meter animation, then rebuild the Hero HUD | Threat artwork remains mirrored and fills correctly; its shader animation moves in the same screen-space direction as the vanilla Hero bars; one private meter material is replaced cleanly during the rebuild without changing or leaking shared materials | Pending |
| B50 | Start from a schema-14 config containing PurpleWyrdnessBrightness and other durable values | A beside-config backup is created; schema 15 skips the retired brightness key, preserves compatible durable values, adopts the 1-second diagnostics default when the old value was untouched, and leaves both diagnostic overrides disabled | Pending |
| B51 | From stable Purple and Orange Wyrdness nights, create sudden 10-point and 20-point threat gains while watching the meter and scene | Gameplay threat and meter fill update immediately; moon, moonlight, protection bubble, and integrated palette approach the new threat color smoothly with the default two-second half-life and no frame-rate spike | Pending |
| B52 | Repeat B51 with Threat Lighting Smoothing at 0, 0.5, 2, and 10 seconds, including pause and resume | Zero applies immediately; positive values follow their configured active-time half-life; pausing freezes the visual transition; no per-frame lighting calculation or warning flood appears | Pending |
| B53 | Start from a schema-15 config with customized legacy Purple exposure, EV compensation, indirect diffuse, and ThreatMeterColor values | A beside-config backup is created; schema 16 skips the retired controls, creates Wyrdnight Brightness at 1 plus separate default Purple and Orange meter colors, and preserves unrelated compatible durable settings | Pending |
| B54 | Start from a schema-16 config containing the former Purple and Orange threat-meter brightness values plus unrelated durable customizations | A beside-config backup is created; schema 17 adopts the recalibrated 1.0 brightness baseline for both palettes instead of importing incompatible same-name values, while unrelated compatible settings remain preserved | Pending |
| B55 | Start from a schema-17 config with customized world ThreatRedColor, meter base colors and brightness values, plus unrelated durable customizations | A beside-config backup is created; schema 18 preserves the customized world red and compatible meter settings, creates both palette-specific meter red targets at #FF3028, and keeps unrelated compatible values | Pending |

## C. Threat inputs and anti-spam behavior

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| C1 | Remain exposed through measurable world-night progress | Passive threat follows normalized night progress, not elapsed Unity time | Pending |
| C2 | Walk, then sustain sprinting or fast swimming | Walking adds no movement threat; sustained fast movement adds throttled threat | Pending |
| C3 | Deal and receive meaningful combat damage | Damage aggregates within the short window and respects its cap | Pending |
| C3a | Swing into empty space, then strike scenery or a non-damageable object several times | Empty swings add nothing; each confirmed impact queues half the combat-window cap, commits after about 1.5 active seconds by default, adds at most once per attack, and respects the combined cap | Pending |
| C4 | Kill an eligible Wyrd-converted or Wyrd-bound ordinary NPC | One Wyrd-kill threat input is accepted | Pending |
| C5 | Take direct pickups and loot several container/corpse items | Unique acquisitions add capped queued threat; repeated low-value input cannot farm it | Pending |
| C6 | Pause during active cooldowns | Threat windows, GFT cooldowns, warning, recovery, and interior decay do not advance while paused | Pending |
| C7 | Reach each stage in order, then decay downward | Unnoticed, Watched, Hunted, and Marked transitions occur at the documented values | Pending |
| C8 | Reach dawn | Threat, activity windows, nightly budget, and hunt state reset cleanly | Pending |
| C9 | During an exposed Wyrdnight, fire arrows and throw projectiles into empty space, complete spells that hit nothing, then cancel or fail casts | Released projectiles and successful casts add modest capped combat threat; failed/canceled casts add nothing | Pending |
| C10 | Swing melee weapons into empty space, then hit scenery and a damageable target | Empty melee swings add nothing; confirmed scenery contact and damage add combat-window threat | Pending |
| C11 | With Battlecry Voice Tuner installed, cry repeatedly while exposed outdoors during a Wyrdnight | Accepted threat follows full, half, quarter, 12.5 percent, then the 10 percent floor | Pending |
| C12 | Wait at least 30 active seconds after the last accepted cry, then cry again | Battlecry threat returns to the full configured amount; paused time does not advance the reset | Pending |

## D. Presets and world timescales

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| D1 | Apply Uneasy Night in FoA Mod Manager | Gameplay values plus 75/210/105 stalker cooldown and 4 provocation threat are written, elites are disabled, selector returns to Custom, and presentation/diagnostics stay unchanged | Pending |
| D2 | Apply Watchful Night | Recommended defaults plus 55/165/70 stalker cooldown and 6 provocation threat are written, elites are disabled, and selector returns to Custom | Pending |
| D3 | Apply Cursed Night | Higher-pressure values, 40/125/55 stalker cooldown, 8 provocation threat, and elite/high-pressure permission are written; selector returns to Custom without changing presentation | Pending |
| D4 | Apply each gameplay preset after customizing all three clock durations | Presets leave DayMinutes, BaseNightMinutes, and MaximumThreatNightMinutes unchanged | Pending |
| D5 | Measure one default day | Daylight lasts 60 real minutes within +/-0.5 minute | Pending |
| D6 | Hold threat at 0 for a complete default night | Night lasts 6 real minutes within +/-0.5 minute, close to the game's approximately 6.2-minute Wyrdnight | Pending |
| D7 | Hold threat at 100 for a complete default night | Night lasts 12 real minutes within +/-0.5 minute | Pending |
| D8 | Set threat to 50, then change it materially during the same night | Requested night duration begins near 9 minutes and reacts within one update step without repeated setter logging | Pending |
| D9 | Remain hidden or protected while threat stays low or drains | Night passes faster as the requested duration moves back toward the 6-minute base | Pending |
| D10 | Cross dawn and nightfall naturally | The configured phase rate switches within one update without repeated setter logging | Pending |
| D11 | Load and perform phase-changing time skips | The correct day/night rate is active immediately after the new world/phase becomes valid | Pending |
| D12 | Edit all three duration settings live | An active phase adopts its new duration once; inactive-phase settings apply when relevant | Pending |
| D13 | Disable Dynamic Timescale, then disable Eyes separately | Vanilla duration is restored only when Eyes still owns its last rate | Pending |
| D14 | Let another clock owner change the active rate, then disable Eyes | Eyes does not overwrite the external rate during safe restoration | Pending |
| D15 | Complete representative Watchful nights at the default 60/6/12 durations | Quiet stretches, warnings, hunts, recovery, and roughly 14 percent maximum-night budget capacity remain coherent | Pending |
| D16 | Configure maximum-threat night below the base night | Runtime safely clamps the maximum to the base and never makes increasing threat shorten the night | Pending |
| D17 | Disable Dynamic Timescale, enable Override World Timescale, and test multipliers 2, 0.5, and 1 | The world clock runs at twice, half, and vanilla speed; phase and threat changes do not rewrite the fixed rate; gameplay time is unchanged | Pending |
| D18 | Disable the timescale override, disable Eyes, and repeat after an external clock change | Dynamic timing resumes once when allowed; Eyes restores vanilla only while it still owns the rate and never overwrites the external change | Pending |

## E. Curated director and hunt lifecycle

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| E1 | Level 1 in Horns of the South | Eligible pool contains only Wyrdspirit and pack size is one | Pending |
| E2 | Horns at levels 4, 5, 6, 7, 8, 10, 15, and 20; inspect diagnostics | Native low-tier monsters and undead enter gradually; Sharg remains filtered unless elites are enabled above 75 threat; Ogre remains solo | Pending |
| E3 | Cuanacht at levels 15, 16, 20, 26, and 30 | Native monster, undead, Lost Knight, Slugholder, Ogre, Sharg, Barnaclator, and Nuckelavee gates and placement match the catalog | Pending |
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
| E16 | Forlorn at levels 25, 30, and 40 with elites disabled | Native Redcap, Mistling, Bonemasks, undead, Frostbitten, smaller Sharg, Skeleton Archer, and Swarm enter at reviewed gates; no elite enters | Pending |
| E17 | Sarras at levels 25, 27, 28, and 30 with elites disabled | Native Drowners, Drowned crew, Finbled roles, Tadpole, Wailcap, and Tidewraith enter at reviewed gates; no generic Wyrdspawn or out-of-map enemy enters | Pending |
| E18 | Inspect repeated regional compositions | Every regional profile has a one-copy limit; only Wyrdspirit clusters; solo profiles never gain sidecars | Pending |
| E19 | Force one member to fail exact Hero combat confirmation after placement | Atomic composition is discarded, budget cost is zero, and failed-placement recovery begins | Pending |
| E20 | Let a nearby hunter disengage while exposed in the same exterior | Native combat is reasserted no sooner than two active seconds apart and no more than three times per member | Pending |
| E21 | Disengage beyond 60 m, while protected, during loading, or after scene change | No reacquisition occurs; normal sustained escape remains available | Pending |
| E22 | Enable elites at exactly 75 threat, then raise threat above 75 | Every elite remains filtered at 75 and becomes eligible immediately above 75, subject to level, region, cost, and session safety | Pending |
| E23 | Disable elites at 100 threat on each supported map | No Elite actor enters the eligible pool; normal high-tier weighting continues | Pending |
| E24 | Cursed at high level and threat above 75 in Horns, Forlorn, and Sarras | Only reviewed regional Sharg, Skeleton, or Drowned Knight elites appear; none is selected as a sidecar and every profile remains one-copy | Pending |
| E25 | Inspect rejected shipped variants through diagnostics/contracts | Boss, miniboss, friendly, summon, story, challenge, trial, custom, arena, and hero-summon variants never enter any pool | Pending |

## F. Ambient stalker lifecycle

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| F1 | Below 50 threat in each supported exterior at the listed level gates | Only the ordinary map roster plus universal Wyrdspirit enters diagnostics; unknown scenes fail closed | Pending |
| F2 | At exactly 50 threat with Allow Elite Enemies off | No new ambient stalker is eligible | Pending |
| F3 | From 50 to below 75 with Allow Elite Enemies on | Only regional high-pressure Sharg, Lost Knight, Finbled Heavy, or Drowned Knight profiles enter at their level gates | Pending |
| F4 | At exactly 75 threat with either toggle state | No new ambient stalker is placed; official-hunt behavior remains available | Pending |
| F5 | Observe several Watchful cooldowns at 0, 25, and near-50 threat | Delays remain randomized; the 55-second minimum holds and the upper bound shrinks from 165 toward 70 seconds | Pending |
| F6 | Let a stalker initialize while Diagnostics is on | Exact profile, map, level, hidden aggression, off-camera placement, and zero budget cost are reported once; no combat starts | Pending |
| F7 | Slowly rotate until a passive stalker enters view | Renderer visibility confirms one sighting; it watches without health bar, compass marker, aggro music, or premature combat | Pending |
| F8 | Move away from a passive stalker | Native FollowMovement shadows the Hero; it returns to Observe before crowding and movement overrides do not stack | Pending |
| F9 | Sprint deliberately toward a representative stalker on each map | Sustained facing, speed, and closing distance trigger native Flee; merely crossing nearby or running away does not | Pending |
| F10 | Raise ordinary threat through several rolled 45-55 values | Each exact stalker becomes hostile only at its hidden value; the value varies across instances | Pending |
| F11 | Raise high-pressure threat through several rolled 70-80 values | Each high-pressure stalker becomes hostile at its own hidden value even if it survives past the 75 spawn ceiling | Pending |
| F12 | Attack the exact passive stalker with melee and ranged Hero damage | The pre-damage hook removes Eyes' passive guards, requests exact-Hero combat immediately, and adds ProvocationThreat once | Pending |
| F13 | Continue damaging the same provoked stalker, then attack a separate stalker that became hostile from threat | Provocation threat is not added again and is never awarded after natural escalation; ordinary combat and eligible Wyrd-kill threat still follow their normal rules | Pending |
| F14 | Look away while the passive stalker is nearby, then while at least 65 m away for 2.5 seconds | Nearby actor remains; distant continuously off-camera actor may vanish. Looking back resets the timer | Pending |
| F15 | Run more than 100 m from a hostile stalker and wait | Eyes does not discard it or release the encounter lane because of distance | Pending |
| F16 | Attempt an official warning while passive/hostile stalker is owned, then attempt a stalker during warning/active hunt | Only one lane advances; the blocked lane spends no budget and emits no duplicate encounter | Pending |
| F17 | Trigger interior, dawn, gameplay load, death, and exterior scene change with a stalker in separate runs | Exact volatile Location cleans safely; no save persistence, stale listener, movement override, or lane lock remains | Pending |
| F18 | Force one ambient template to fail placement three times | No budget is spent; that exact profile is rejected for the session and empty pools fail closed | Pending |
| F19 | Pursue a fleeing stalker but remain beyond 8 m, then close to exactly 8 m | It continues native Flee outside the defensive boundary, then turns hostile at the boundary with exact-Hero acquisition and no attack-only provocation threat | Pending |
| F20 | Let a stalker follow, complete a flee, and continue moving around it | It returns to Observe around the 20 m buffer and cannot begin another flee episode for five active seconds; diagnostics do not repeat a flee transition continuously | Pending |

## G. GFT atmosphere and diagnostics

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| G1 | Minimal notification preset | Only committed official hunts and official outcomes show; stalkers remain visual | Pending |
| G2 | Atmospheric preset | Minimal events plus night begin/end, upward stages, and one witnessed stalker-disappearance line show; sighting and provocation remain implicit | Pending |
| G3 | Detailed preset | Downward stages, protection transitions, major surges, stalker sightings, retreats, and escalation flavor also show; no hidden aggression value is exposed | Pending |
| G4 | Repeat each event pool several times | Text varies and the same pool never repeats immediately | Pending |
| G5 | Diagnostics off | No EITD GFT System summaries appear | Pending |
| G6 | Diagnostics on after entering playable gameplay | Concise low-priority System summaries show exact state, threat, budget, official/ambient filters and weights, hidden aggression, movement, placement, and outcomes | Pending |
| G7 | Generate rapid related diagnostic transitions | Immediate delivery, one diagnostics lane, one stalker atmosphere lane, and active-time cooldowns prevent spam or stale queued messages | Pending |
| G8 | Remove or break GFT for an isolated check | Core threat, meter, boundary, stalkers, and hunts continue; integration failure logs once | Pending |
| G9 | Diagnostics on during continuous exposure, protected decay, and interior decay | One aggregate passive/decay summary appears per ten active seconds and flushes on state/stage/night-end changes | Pending |
| G10 | Temporarily provoke Hero and acquisition listener binding failures | One warning appears per failure episode; retry waits 30 unscaled seconds and retries immediately after Hero/event-system replacement | Pending |
| G11 | Rest across nightfall and dawn with Atmospheric or Detailed notifications | No night-begin, night-end, stage, protection, hunt, or stalker atmosphere from slept-through transitions appears; after waking, Diagnostics may show one final-phase reconciliation summary | Pending |
| G12 | Use Battlecry Voice Tuner with Atmospheric or Detailed, then repeat with Minimal | Atmospheric/Detailed responds after two or three accepted cries from the randomized pool, uses the separate 15-active-second default cooldown before responding again, and Minimal stays quiet | Pending |
| G13 | Trigger Eyes and vanilla GFT Wyrd messages, then switch Purple Wyrdness to Orange Wyrdness live | Every Wyrd atmosphere line uses the configurable Purple group first and Orange group after the switch; priority, duration, and Wyrd icon stay unchanged | Pending |
| G14 | Enable threat override and test 0, 25, 50, 75, and 100, then disable it | Meter, stage, visuals, night duration, ambient eligibility, and hunts follow the forced value; natural gain/relief is suppressed until disable; dawn resets and natural behavior resumes | Pending |
| G15 | Enable Diagnostics and remain at the title screen, enter loading, then load a playable Hero | Eyes emits no diagnostic System message at title/loading/no-Hero; normal diagnostics resume only in playable state, while GFT compatibility notices remain unaffected | Pending |
| G16 | During stable daylight, open and close pause, map, and inventory screens, then load an exterior daylight save; afterward cross dawn naturally | Daylight-only state changes emit no night-end atmosphere; the confirmed Wyrdnight-to-daylight edge emits exactly one randomized dawn line | Pending |

## H. Soak and final log review

| ID | Setup and action | Expected result | Status |
| --- | --- | --- | --- |
| H1 | Complete a 12-minute maximum-threat night with Diagnostics off | No repeated exceptions, per-poll log spam, duplicate meter, uninterrupted chain of hunts, or ambient/official lane overlap | Pending |
| H2 | Review the full session log | No startup, placement, transition, ambient listener, movement, HUD, boundary, or GFT exception loop; every spent cost has a confirmed official composition | Pending |
| H3 | Inspect the staged archive and live candidate version | One top-level folder; only DLL, README, and changelog; assembly reports `1.2.8.0`; no standalone Purple Moon Test package or config remains | Pending |
| H4 | Complete a long diagnostics-on default-cycle soak | No per-frame warnings, passive-threat log flood, repeated clock setters, movement transition flood, or stale GFT diagnostics | Pending |
| H5 | Profile an active Wyrdnight with stable threat and no transition | Visual calculations run about five times per second; environment refreshes do not exceed four per second; the per-frame native-lighting postfix only reapplies cached values | Pending |
| H6 | Compare an absent boundary, an inactive boundary, Native single ring, and active layered rings | The custom layered pass performs no fullscreen draws at zero native intensity; active boundaries still animate and respond within 0.2 seconds | Pending |
| H7 | Observe several stalkers while turning the camera and moving the HUD between layouts | Camera checks do not create repeated renderer/corner-array garbage, the meter does not jitter, and visibility, despawn, and HUD repositioning remain responsive | Pending |

The goal is complete only after every row is Passed or the user explicitly
removes it from scope. Automated contracts support these checks but do not
replace the in-game rows.
